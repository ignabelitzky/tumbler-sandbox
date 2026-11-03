using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public static class SmokePreview
{
 public static void Run()
 {
  try
  {
   var shader=Shader.Find("Tumbler/Tire Smoke HDRP");if(shader==null)throw new Exception("Missing smoke shader");
   const string path="Assets/_Project/Resources/TireSmokeHDRP.mat";
   var material=AssetDatabase.LoadAssetAtPath<Material>(path);
   if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}else material.shader=shader;
   AssetDatabase.SaveAssets();
   EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity");
   var car=GameObject.Find("Tumbler");car.GetComponent<TumblerEffects>().SendMessage("Awake");
   foreach(var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))c.enabled=false;
   foreach(var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))canvas.gameObject.SetActive(false);
   var camera=new GameObject("Temporary smoke inspection").AddComponent<Camera>();camera.enabled=false;camera.nearClipPlane=.1f;camera.farClipPlane=1000;camera.fieldOfView=55;
   var hd=camera.gameObject.AddComponent<HDAdditionalCameraData>();hd.clearColorMode=HDAdditionalCameraData.ClearColorMode.Sky;
   var exposure=new GameObject("Temporary smoke exposure").AddComponent<Volume>();exposure.isGlobal=true;exposure.priority=1000;exposure.sharedProfile=ScriptableObject.CreateInstance<VolumeProfile>();var ev=exposure.sharedProfile.Add<Exposure>(true);ev.mode.Override(ExposureMode.Fixed);ev.fixedExposure.Override(11.5f);
   var smokes=car.GetComponentsInChildren<ParticleSystem>().Where(p=>p.GetComponent<ParticleSystemRenderer>().sharedMaterial==material).ToArray();
   if(smokes.Length!=6)throw new Exception("Expected six HDRP smoke emitters");
   var wheels=car.GetComponentsInChildren<WheelCollider>();
   for(int i=0;i<smokes.Length;i++)
   {
    var smoke=smokes[i];smoke.transform.position=wheels[i].transform.position+Vector3.down*wheels[i].radius;
    smoke.randomSeed=(uint)(123+i);for(int puff=0;puff<6;puff++){smoke.Emit(12);smoke.Simulate(.25f,false,false,true);}
   }
   if(smokes.Sum(p=>p.particleCount)==0)throw new Exception("No particles in preview");
   camera.transform.position=car.transform.position+new Vector3(-5,2.4f,-8);camera.transform.LookAt(car.transform.position+Vector3.up*.2f);
   var rt=new RenderTexture(1280,720,24,RenderTextureFormat.ARGBFloat);rt.Create();camera.targetTexture=rt;
   for(int i=0;i<12;i++)RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});
   RenderTexture.active=rt;var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();
   int magenta=texture.GetPixels32().Count(p=>p.r>200&&p.b>120&&p.g<100);if(magenta>10)throw new Exception("Magenta pixels in HDRP capture: "+magenta);
   Directory.CreateDirectory("TestResults/Smoke");File.WriteAllBytes("TestResults/Smoke/preview.png",texture.EncodeToPNG());
   if(!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new Exception("Unsupported or failed smoke shader");
   File.WriteAllText("TestResults/Smoke/checks.txt","PASS native HDRP GPU render, supported shader, no shader errors, six assigned emitters\n");
   EditorApplication.Exit(0);
  }catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}
 }
}
