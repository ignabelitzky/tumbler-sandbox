using System;
using System.IO;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public static class FacilityPreview
{
    static Camera camera;
    const string Folder="TestResults/SandboxExpansion/Previews";
    public static void Run()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity");
            foreach(var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))c.enabled=false;
            foreach(var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))canvas.gameObject.SetActive(false);
            camera=new GameObject("Temporary inspection camera").AddComponent<Camera>();camera.enabled=false;
            camera.fieldOfView=60;camera.nearClipPlane=.1f;camera.farClipPlane=1800;
            camera.gameObject.AddComponent<HDAdditionalCameraData>().clearColorMode=HDAdditionalCameraData.ClearColorMode.Sky;
            var body=UnityEngine.Object.FindFirstObjectByType<TumblerController>().GetComponent<Rigidbody>();
            var modes=UnityEngine.Object.FindFirstObjectByType<SandboxCameraModes>();modes.SendMessage("Awake");modes.SendMessage("OnEnable");
            var orbit=UnityEngine.Object.FindFirstObjectByType<CinemachineOrbitalFollow>();
            for(int i=0;i<3;i++)
            {
                PositionFromRig(orbit);Capture(new[]{"camera-close","camera-medium","camera-far"}[i]);
                modes.CycleMode();
            }
            camera.transform.position=new Vector3(100,8,-12);camera.transform.LookAt(new Vector3(100,-6,-95));Capture("portal");
            camera.transform.position=new Vector3(330,160,-330);camera.transform.LookAt(new Vector3(170,-2,-130));Capture("surface-overview");
            modes.SendMessage("OnDisable");
            EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void PositionFromRig(CinemachineOrbitalFollow orbit)
    {
        orbit.VirtualCamera.PreviousStateIsValid=false;orbit.VirtualCamera.UpdateCameraState(Vector3.up,-1);
        var state=orbit.VirtualCamera.State;
        camera.transform.SetPositionAndRotation(state.GetFinalPosition(),state.GetFinalOrientation());
    }
    static void Capture(string name)
    {
        const int width=1280,height=720;
        var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);rt.Create();
        camera.targetTexture=rt;
        foreach(var t in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))t.ForceMeshUpdate();
        VolumeManager.instance.Update(camera.transform,1);
        for(int i=0;i<12;i++)RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});
        var previous=RenderTexture.active;RenderTexture.active=rt;
        var tex=new Texture2D(width,height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();
        File.WriteAllBytes(Folder+"/"+name+".png",tex.EncodeToPNG());
        RenderTexture.active=previous;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(tex);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
    }
}
