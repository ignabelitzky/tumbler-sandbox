using System;
using System.IO;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[InitializeOnLoad]
public static class FacilityPlayPreview
{
    const string Flag="FacilityPlayPreview";
    const string Folder="TestResults/SandboxExpansion/Previews";
    static int stage;
    static double next;
    static Camera camera;
    static TumblerSystems systems;
    static FacilityPlayPreview(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity");
        SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying||Time.frameCount<15)return;
        try
        {
            if(stage==0)
            {
                var controller=UnityEngine.Object.FindFirstObjectByType<TumblerController>();
                var body=controller.GetComponent<Rigidbody>();
                controller.enabled=false;body.GetComponent<Boost>().enabled=false;
                body.isKinematic=true;body.position=new Vector3(100,-11,-166);body.rotation=Quaternion.LookRotation(Vector3.back);
                body.transform.SetPositionAndRotation(body.position,body.rotation);Physics.SyncTransforms();
                var orbit=UnityEngine.Object.FindFirstObjectByType<CinemachineOrbitalFollow>();orbit.HorizontalAxis.Value=180;
                UnityEngine.Object.FindFirstObjectByType<SandboxCameraModes>().RefreshTunnelState();
                orbit.VirtualCamera.PreviousStateIsValid=false;orbit.VirtualCamera.UpdateCameraState(Vector3.up,-1);
                camera=Camera.main;camera.GetComponent<CinemachineBrain>().enabled=false;
                camera.transform.position=body.position-body.transform.forward*5.8f+Vector3.up*2.2f;
                camera.transform.LookAt(body.position);
                foreach(var c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))c.gameObject.SetActive(false);
                systems=body.GetComponent<TumblerSystems>();systems.SetLightMode(TumblerSystems.LightMode.Off);
                stage=1;next=EditorApplication.timeSinceStartup+1;return;
            }
            if(EditorApplication.timeSinceStartup<next)return;
            Capture(new[]{"tunnel-lights-off","tunnel-low-beams","tunnel-high-beams","tunnel-stealth"}[stage-1]);
            if(stage==1)systems.SetLightMode(TumblerSystems.LightMode.Low);
            if(stage==2)systems.SetLightMode(TumblerSystems.LightMode.High);
            if(stage==3)systems.SetStealth(true);
            if(stage++==4){SessionState.SetBool(Flag,false);EditorApplication.Exit(0);}
            next=EditorApplication.timeSinceStartup+.6;
        }
        catch(Exception e){SessionState.SetBool(Flag,false);Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Capture(string name)
    {
        Directory.CreateDirectory(Folder);
        const int width=1280,height=720;
        var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);rt.Create();camera.targetTexture=rt;
        for(int i=0;i<8;i++)RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});
        var previous=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(width,height,TextureFormat.RGB24,false);
        tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();File.WriteAllBytes(Folder+"/"+name+".png",tex.EncodeToPNG());
        RenderTexture.active=previous;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(tex);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
    }
}
