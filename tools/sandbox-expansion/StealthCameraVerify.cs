using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class StealthCameraVerify
{
    const string Flag="StealthCameraVerify";
    const string Folder="TestResults/SandboxExpansion";
    static readonly List<string> checks=new List<string>();
    static int stage;
    static double next;
    static TumblerSystems systems;
    static StealthNightVision vision;
    static Rigidbody body;
    static Camera camera;
    static void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add("PASS "+label);}
    static StealthCameraVerify(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        File.Delete(Folder+"/stealth-camera-failed.txt");
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity");
        SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();
    }
    static void Step(SandboxCameraModes modes,float dt)=>typeof(SandboxCameraModes).GetMethod("AdvanceTransition",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(modes,new object[]{dt});
    static void Place(Vector3 p)
    {body.position=p;body.transform.position=p;Physics.SyncTransforms();}
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying||Time.frameCount<20||GameNavigation.GameplayInputBlocked)return;
        try
        {
            if(stage==0)
            {
                var controller=Object.FindFirstObjectByType<TumblerController>();body=controller.GetComponent<Rigidbody>();
                controller.enabled=false;body.GetComponent<Boost>().enabled=false;body.isKinematic=true;
                systems=body.GetComponent<TumblerSystems>();vision=body.GetComponent<StealthNightVision>();
                Check(vision!=null&&!vision.IsActive&&vision.Weight==0,"Normal mode starts with night vision fully disabled");
                var modes=Object.FindFirstObjectByType<SandboxCameraModes>();
                var orbit=Object.FindFirstObjectByType<CinemachineOrbitalFollow>();
                Place(new Vector3(0,1,0));modes.RefreshTunnelState();Step(modes,2);
                while(modes.SelectedMode!=SandboxCameraModes.ViewMode.Close){modes.CycleMode();Step(modes,2);}
                foreach(float target in new[]{10f,16f,5.8f})
                {
                    float start=orbit.Orbits.Center.Radius;modes.CycleMode();
                    Check(Mathf.Abs(orbit.Orbits.Center.Radius-start)<.0001f&&modes.IsTransitioning,"Camera switch starts at current radius without a cut");
                    float previous=start;
                    for(int i=0;i<5;i++)
                    {
                        Step(modes,.15f);float current=orbit.Orbits.Center.Radius;
                        Check(current>Mathf.Min(start,target)&&current<Mathf.Max(start,target),"Intermediate camera distance lies between presets");
                        Check((target-start)*(current-previous)>0,"Blend advances monotonically");previous=current;
                    }
                    float paused=orbit.Orbits.Center.Radius;Step(modes,0);
                    Check(orbit.Orbits.Center.Radius==paused,"Paused blend holds its position");
                    Step(modes,2);Check(Mathf.Abs(orbit.Orbits.Center.Radius-target)<.001f&&!modes.IsTransitioning,"Blend reaches the exact preset");
                }
                modes.CycleMode();Step(modes,.3f);float interrupted=orbit.Orbits.Center.Radius;modes.CycleMode();
                Check(Mathf.Abs(interrupted-orbit.Orbits.Center.Radius)<.0001f,"Rapid repeated camera input retargets from the current position");Step(modes,2);
                Place(new Vector3(100,1,-28));modes.RefreshTunnelState();Step(modes,2);
                orbit.VirtualCamera.PreviousStateIsValid=true;
                Place(new Vector3(100,0,-40));modes.RefreshTunnelState();
                Check(modes.IsInsideTunnel&&modes.EffectiveMode==SandboxCameraModes.ViewMode.Close&&modes.IsTransitioning,"Tunnel entry starts a faster close-camera blend");
                Check(orbit.VirtualCamera.PreviousStateIsValid,"Ordinary tunnel entry preserves Cinemachine tracking history");
                Step(modes,.35f);Check(Mathf.Abs(orbit.Orbits.Center.Radius-5.8f)<.001f,"Tunnel blend reaches close within 0.35 seconds");
                Place(new Vector3(100,1,-28));modes.RefreshTunnelState();
                Check(modes.IsTransitioning&&modes.SelectedMode==SandboxCameraModes.ViewMode.Far,"Tunnel exit smoothly restores the previous far selection");Step(modes,2);
                Place(new Vector3(242,0,-40));modes.RefreshTunnelState();
                Check(!modes.IsTransitioning&&!orbit.VirtualCamera.PreviousStateIsValid,"Teleport snaps safely and invalidates stale camera state");
                modes.enabled=false;Check(Mathf.Abs(orbit.Orbits.Center.Radius-10)<.001f,"Disabling restores the original camera rig");modes.enabled=true;
                Time.timeScale=1;
                Place(new Vector3(100,-11,-166));body.rotation=Quaternion.LookRotation(Vector3.back);body.transform.rotation=body.rotation;
                camera=Camera.main;camera.GetComponent<CinemachineBrain>().enabled=false;
                camera.transform.position=body.position+Vector3.forward*5.8f+Vector3.up*2.2f;camera.transform.LookAt(body.position);
                foreach(var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))c.gameObject.SetActive(false);
                systems.SetLightMode(TumblerSystems.LightMode.Off);
                stage=1;next=EditorApplication.timeSinceStartup+1;return;
            }
            if(EditorApplication.timeSinceStartup<next)return;
            if(stage==1)
            {
                Capture("normal-tunnel");systems.SetStealth(true);
                Check(vision.IsActive,"Entering stealth automatically enables night vision");
                Check(body.GetComponentsInChildren<Light>(true).All(l=>!l.enabled),"Night vision emits no vehicle light in stealth");
                next=EditorApplication.timeSinceStartup+2;stage=2;return;
            }
            if(stage==2)
            {
                Check(vision.Weight==1,"Night vision fades in to full intensity");
                Capture("night-vision-tunnel");systems.SetStealth(false);
                Check(!vision.IsActive&&vision.Weight==0,"Normal mode immediately removes every night-vision override");
                next=EditorApplication.timeSinceStartup+1;stage=3;return;
            }
            if(stage==3)
            {
                Capture("normal-restored");
                Place(new Vector3(0,1,0));camera.transform.position=new Vector3(0,4,-7);camera.transform.LookAt(new Vector3(0,1,15));
                systems.SetStealth(true);next=EditorApplication.timeSinceStartup+2;stage=4;return;
            }
            if(stage==4)
            {
                Capture("night-vision-daylight");systems.enabled=false;
                Check(!vision.IsActive&&vision.Weight==0,"Disabling vehicle systems clears night vision immediately");
                systems.enabled=true;systems.SetStealth(true);systems.SetStealth(false);
                Check(!vision.IsActive,"Repeated stealth toggles leave normal rendering clean");
                var effect=body.transform.Find("Stealth night vision").GetComponent<CustomPassVolume>();
                Check(effect.targetCamera==camera&&effect.injectionPoint==CustomPassInjectionPoint.AfterPostProcess,"Night sensor affects only the gameplay camera and leaves scene exposure untouched");
                File.WriteAllLines(Folder+"/stealth-camera-checks.txt",checks);
                SessionState.SetBool(Flag,false);EditorApplication.Exit(0);
            }
        }
        catch(Exception e)
        {
            File.WriteAllText(Folder+"/stealth-camera-failed.txt",e.ToString());
            SessionState.SetBool(Flag,false);Debug.LogException(e);EditorApplication.Exit(1);
        }
    }
    static void Capture(string name)
    {
        const int w=1280,h=720;
        var rt=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32);rt.Create();camera.targetTexture=rt;
        for(int i=0;i<16;i++)RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});
        var previous=RenderTexture.active;RenderTexture.active=rt;
        var tex=new Texture2D(w,h,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();
        File.WriteAllBytes(Folder+"/Previews/"+name+".png",tex.EncodeToPNG());
        if(name.StartsWith("night-vision"))
        {
            var pixels=tex.GetPixels32();
            int visible=pixels.Count(p=>p.g>30);
            int invalid=pixels.Count(p=>p.g>30 && (p.r>p.g*.8f || p.b>p.g*.8f));
            Check(visible>pixels.Length*.25f,"Night sensor reveals surfaces across the image: "+name);
            Check(invalid<pixels.Length*.01f,"Night image has consistent phosphor colour without buffer artifacts: "+name);
            Check(!ShaderUtil.GetShaderMessages(Resources.Load<Shader>("StealthNightVision")).Any(m=>m.severity.ToString()=="Error"),"Night shader compiles without errors");
        }
        RenderTexture.active=previous;camera.targetTexture=null;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);
    }
}
