using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class ControlsLightingPreview
{
    const string Flag="ControlsLightingPreview";
    const string Folder="TestResults/ControlsLighting/Previews";
    static int stage,lastFrame;
    static double next;
    static Camera camera;
    static TurbineFireLighting lighting;
    static ControlsLightingPreview(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        Directory.CreateDirectory(Folder);
        File.Delete(Folder+"/layout.txt");File.Delete(Folder+"/complete.txt");
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Main Menu.unity");
        SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying||Time.frameCount<12||Time.frameCount==lastFrame)return;
        lastFrame=Time.frameCount;
        if(EditorApplication.timeSinceStartup<next)return;
        try
        {
            switch(stage++)
            {
                case 0:
                    Object.FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(b=>b.name=="Controls").onClick.Invoke();
                    camera=Camera.main;camera.nearClipPlane=.01f;
                    var canvas=Object.FindFirstObjectByType<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.2f;
                    Capture("controls-1080",1920,1080,true);
                    Capture("controls-720",1280,720,true);
                    Capture("controls-ultrawide",2560,1080,true);
                    Capture("controls-4by3",1280,960,true);
                    SceneManager.LoadScene("Sandbox");next=EditorApplication.timeSinceStartup+1.2;break;
                case 1:
                    var body=Object.FindFirstObjectByType<TumblerController>().GetComponent<Rigidbody>();
                    body.GetComponent<TumblerController>().enabled=false;
                    body.GetComponent<Boost>().enabled=false;body.isKinematic=true;
                    body.transform.SetPositionAndRotation(new Vector3(100,-11,-166),Quaternion.LookRotation(Vector3.back));
                    body.position=body.transform.position;body.rotation=body.transform.rotation;Physics.SyncTransforms();
                    camera=Camera.main;camera.GetComponent<Unity.Cinemachine.CinemachineBrain>().enabled=false;
                    camera.transform.position=new Vector3(95.5f,-8.8f,-158.5f);
                    camera.transform.LookAt(new Vector3(100,-11.5f,-162.5f));
                    foreach(var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))c.gameObject.SetActive(false);
                    body.GetComponent<TumblerSystems>().SetLightMode(TumblerSystems.LightMode.Off);
                    lighting=body.GetComponentInChildren<TurbineFireLighting>();
                    lighting.IdleFlame.Simulate(1,true,false,false);lighting.IdleFlame.Play(true);
                    next=EditorApplication.timeSinceStartup+1;break;
                case 2:
                    Capture("idle-glow",1280,720);
                    lighting.IdleLight.enabled=false;lighting.TurboLight.enabled=false;
                    Capture("idle-flame-only",1280,720);
                    lighting.IdleLight.enabled=true;
                    typeof(Boost).GetMethod("AdvanceReserve",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(lighting.GetComponentInParent<Boost>(),new object[]{.05f,true,false});
                    lighting.TurboFlame.Simulate(.6f,true,false,false);lighting.TurboFlame.Play(true);
                    next=EditorApplication.timeSinceStartup+.8;break;
                case 3:
                    Capture("turbo-glow",1280,720);
                    lighting.IdleLight.enabled=false;lighting.TurboLight.enabled=false;
                    Capture("turbo-flame-only",1280,720);
                    lighting.GetComponentInParent<TumblerSystems>().SetStealth(true);
                    next=EditorApplication.timeSinceStartup+.3;break;
                case 4:
                    Capture("turbine-stealth",1280,720);
                    File.WriteAllText(Folder+"/complete.txt","PASS native UI bounds and HDRP turbine snapshots");
                    SessionState.SetBool(Flag,false);EditorApplication.Exit(0);break;
            }
        }
        catch(Exception e){SessionState.SetBool(Flag,false);Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Capture(string name,int width,int height,bool ui=false)
    {
        var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);rt.Create();camera.targetTexture=rt;
        Canvas.ForceUpdateCanvases();
        if(ui)
        {
            var canvas=Object.FindFirstObjectByType<Canvas>();canvas.GetComponent<CanvasScaler>().SendMessage("Update");
            Canvas.ForceUpdateCanvases();
            var nav=Object.FindFirstObjectByType<ControlsOverlayNavigation>();nav.SendMessage("Fit");Canvas.ForceUpdateCanvases();
        }
        foreach(var text in Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))text.ForceMeshUpdate();
        for(int i=0;i<10;i++)RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});
        if(ui)
        {
            var texts=Object.FindFirstObjectByType<ControlsOverlayNavigation>().GetComponentsInChildren<TMP_Text>();
            foreach(var text in texts)
            {
                var corners=new Vector3[4];text.rectTransform.GetWorldCorners(corners);
                foreach(var corner in corners)
                {
                    Vector2 p=RectTransformUtility.WorldToScreenPoint(camera,corner);
                    if(p.x < -1 || p.x > width+1 || p.y < -1 || p.y > height+1)throw new Exception("Clipped control text at "+name+": "+text.name+" "+p);
                }
                if(text.preferredWidth>text.rectTransform.rect.width+1 || text.preferredHeight>text.rectTransform.rect.height+1)
                    throw new Exception("Overflowing control text: "+name+" "+text.name);
            }
            File.AppendAllText(Folder+"/layout.txt","PASS "+name+" all "+texts.Length+" labels visible and fitting\n");
        }
        var previous=RenderTexture.active;RenderTexture.active=rt;
        var tex=new Texture2D(width,height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();
        File.WriteAllBytes(Folder+"/"+name+".png",tex.EncodeToPNG());
        RenderTexture.active=previous;camera.targetTexture=null;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);
    }
}
