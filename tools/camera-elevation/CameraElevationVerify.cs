using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class CameraElevationVerify
{
    const string Flag="CameraElevationVerify";
    const string Folder="TestResults/CameraElevation";
    static List<string> checks=new List<string>();
    static List<View> views=new List<View>();
    static Camera camera;
    static CinemachineOrbitalFollow orbit;
    static SandboxCameraModes modes;
    static Rigidbody body;
    [Serializable] public class View {public string mode;public Vector3 eye;public float elevation,downwardPitch,radius;}
    [Serializable] public class Report {public View[] views;}
    static CameraElevationVerify(){EditorApplication.update+=Tick;}
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add("PASS "+label);}
    public static void Run()
    {
        Directory.CreateDirectory(Folder);File.Delete(Folder+"/failed.txt");
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity");
        SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();
    }
    static void Step(float dt)=>typeof(SandboxCameraModes).GetMethod("AdvanceTransition",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(modes,new object[]{dt});
    static Vector3 UpdateCamera()
    {
        orbit.VirtualCamera.PreviousStateIsValid=false;
        orbit.VirtualCamera.UpdateCameraState(Vector3.up,-1);
        var state=orbit.VirtualCamera.State;
        camera.transform.SetPositionAndRotation(state.GetFinalPosition(),state.GetFinalOrientation());
        return camera.transform.position;
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying||Time.frameCount<20)return;
        SessionState.SetBool(Flag,false);
        try
        {
            var controller=Object.FindFirstObjectByType<TumblerController>();body=controller.GetComponent<Rigidbody>();
            controller.enabled=false;body.GetComponent<Boost>().enabled=false;body.isKinematic=true;
            body.position=new Vector3(0,.97f,0);body.rotation=Quaternion.identity;
            body.transform.SetPositionAndRotation(body.position,body.rotation);Physics.SyncTransforms();
            orbit=Object.FindFirstObjectByType<CinemachineOrbitalFollow>();modes=Object.FindFirstObjectByType<SandboxCameraModes>();
            camera=Camera.main;camera.GetComponent<CinemachineBrain>().enabled=false;
            // Capture inspection views without stale teleport motion vectors blurring
            // the car. This temporary volume is never saved into the gameplay scene.
            var preview=new GameObject("Temporary camera capture volume").AddComponent<Volume>();
            preview.isGlobal=true;preview.priority=10000;preview.sharedProfile=ScriptableObject.CreateInstance<VolumeProfile>();
            preview.sharedProfile.Add<MotionBlur>(true).intensity.Override(0);
            Object.FindFirstObjectByType<CinemachineInputAxisController>().enabled=false;
            foreach(var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))c.gameObject.SetActive(false);
            modes.RefreshTunnelState();Step(2);
            while(modes.SelectedMode!=SandboxCameraModes.ViewMode.Close){modes.CycleMode();Step(2);}
            float neutral=orbit.VerticalAxis.Center;
            var original=(Cinemachine3OrbitRig.Settings)typeof(SandboxCameraModes).GetField("originalOrbits",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(modes);
            foreach(var mode in new[]{SandboxCameraModes.ViewMode.Close,SandboxCameraModes.ViewMode.Medium,SandboxCameraModes.ViewMode.Far})
            {
                Check(modes.SelectedMode==mode,"Expected preset selected: "+mode);
                orbit.VerticalAxis.Value=neutral;orbit.HorizontalAxis.Value=0;
                var eye=UpdateCamera();
                float elevation=eye.y-body.position.y;
                float radius=Vector3.ProjectOnPlane(eye-body.position,Vector3.up).magnitude;
                float pitch=Mathf.Asin(-camera.transform.forward.y)*Mathf.Rad2Deg;
                Check(elevation>2.7f,"Neutral camera is elevated above vehicle pivot: "+mode);
                Check(pitch>22 && pitch<33,"Neutral view points naturally down toward vehicle: "+mode+" pitch="+pitch);
                var target=camera.WorldToViewportPoint(body.position);
                Check(target.z>0 && Mathf.Abs(target.x-.5f)<.05f && Mathf.Abs(target.y-.5f)<.05f,"Gameplay aim keeps vehicle centered: "+mode);
                views.Add(new View{mode=mode.ToString(),eye=eye,elevation=elevation,downwardPitch=pitch,radius=radius});
                Capture(mode.ToString().ToLowerInvariant());
                foreach(float yaw in new[]{-90f,90f,180f})
                {
                    orbit.HorizontalAxis.Value=yaw;eye=UpdateCamera();
                    Check(eye.y-body.position.y>2.7f,"Manual horizontal orbit retains elevated view: "+mode+" yaw="+yaw);
                }
                orbit.HorizontalAxis.Value=0;
                foreach(float axis in new[]{orbit.VerticalAxis.Range.x,orbit.VerticalAxis.Range.y})
                {
                    orbit.VerticalAxis.Value=axis;eye=UpdateCamera();
                    Check(eye.y-body.position.y>1.5f,"Manual low/high orbit remains above floor: "+mode+" axis="+axis);
                }
                orbit.VerticalAxis.Value=neutral;
                var start=orbit.Orbits;modes.CycleMode();
                Check(orbit.Orbits.Center.Height==start.Center.Height,"Preset change starts at current elevation without a cut: "+mode);
                Step(.45f);
                float mid=orbit.Orbits.Center.Height;Step(0);
                Check(orbit.Orbits.Center.Height==mid,"Paused transition preserves elevation: "+mode);
                Step(2);
            }
            // Preserve elevated close view and obstacle protection inside the real tunnel.
            while(modes.SelectedMode!=SandboxCameraModes.ViewMode.Far){modes.CycleMode();Step(2);}
            foreach(var p in new[]{new Vector3(100,0,-40),new Vector3(100,-11,-166)})
            {
                body.position=p;body.transform.position=p;Physics.SyncTransforms();modes.RefreshTunnelState();Step(2);
                Check(modes.IsInsideTunnel && modes.EffectiveMode==SandboxCameraModes.ViewMode.Close,"Tunnel still selects close preset: "+p);
                var eye=UpdateCamera();
                Check(!Physics.OverlapSphere(eye,.15f,~0,QueryTriggerInteraction.Ignore).Any(c=>c.name=="Tunnel walls and closed roof"),"Elevated camera remains outside tunnel concrete: "+p);
            }
            modes.enabled=false;
            Check(orbit.Orbits.Center.Height==original.Center.Height && orbit.Orbits.Top.Height==original.Top.Height && orbit.Orbits.Bottom.Height==original.Bottom.Height,"Disabling restores all authored orbit heights");
            File.WriteAllLines(Folder+"/checks.txt",checks);File.WriteAllText(Folder+"/views.json",JsonUtility.ToJson(new Report{views=views.ToArray()},true));
            EditorApplication.Exit(0);
        }
        catch(Exception e){File.WriteAllLines(Folder+"/checks.txt",checks);File.WriteAllText(Folder+"/failed.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Capture(string name)
    {
        const int w=1280,h=720;
        var rt=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32);rt.Create();camera.targetTexture=rt;
        for(int i=0;i<16;i++)RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});
        var old=RenderTexture.active;RenderTexture.active=rt;
        var tex=new Texture2D(w,h,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();
        File.WriteAllBytes(Folder+"/"+name+".png",tex.EncodeToPNG());
        RenderTexture.active=old;camera.targetTexture=null;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);
    }
}
