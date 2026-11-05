using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;

public static class ClassicPreview
{
    public static void Run()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity");Directory.CreateDirectory(ClassicSandbox.Results);
            var root=GameObject.Find("Classic flowing proving grounds");if(root==null)throw new Exception("Classic proving ground is not attached");
            foreach(var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))c.enabled=false;
            foreach(var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))c.gameObject.SetActive(false);
            var volume=new GameObject("Temporary sharp capture").AddComponent<Volume>();volume.isGlobal=true;volume.priority=10000;volume.sharedProfile=ScriptableObject.CreateInstance<VolumeProfile>();volume.sharedProfile.Add<MotionBlur>(true).intensity.Override(0);
            var cam=new GameObject("Temporary classic inspection camera").AddComponent<Camera>();cam.enabled=false;cam.fieldOfView=60;cam.farClipPlane=2500;cam.nearClipPlane=.12f;
            cam.gameObject.AddComponent<HDAdditionalCameraData>().clearColorMode=HDAdditionalCameraData.ClearColorMode.Sky;
            Capture(cam,"overview",new Vector3(-535,300,-400),new Vector3(-95,0,40));
            Capture(cam,"classic-ramps",new Vector3(-335,35,-18),new Vector3(-215,3,95));
            Capture(cam,"speed-bumps",new Vector3(-28,6,105),new Vector3(-100,.2f,63));
            Capture(cam,"uneven-ground",new Vector3(-125,14,-55),new Vector3(-215,.2f,-104));
            Capture(cam,"tabletops",new Vector3(-185,30,195),new Vector3(-110,2,290));
            Capture(cam,"original-tunnel",new Vector3(82,7,-13),new Vector3(102,-4,-76));
            var lines=root.GetComponentsInChildren<Collider>().Select(c=>c.name+" "+c.bounds.min+" .. "+c.bounds.max).ToArray();File.WriteAllLines(ClassicSandbox.Results+"/collider-bounds.txt",lines);
            EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Capture(Camera cam,string name,Vector3 eye,Vector3 target)
    {
        cam.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye,Vector3.up));
        var rt=new RenderTexture(1440,900,24,RenderTextureFormat.ARGB32);rt.Create();cam.targetTexture=rt;
        for(int i=0;i<16;i++)RenderPipeline.SubmitRenderRequest(cam,new RenderPipeline.StandardRequest{destination=rt});
        var prior=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(1440,900,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1440,900),0,0);tex.Apply();File.WriteAllBytes(ClassicSandbox.Results+"/"+name+".png",tex.EncodeToPNG());RenderTexture.active=prior;cam.targetTexture=null;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);
    }
}
