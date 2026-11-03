using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class ImpactPhysicsVerify
{
 const string Flag="ImpactPhysicsMeasure";
 static Rigidbody body;static WheelCollider[] wheels;static int frames;
 [Serializable] public class Trial
 {
  public string scenario,configuration,outcome;
  public float requestedKph,preKph,postKph,finalKph,maxVertical,maxComY,comRise,maxPitch,maxRoll,maxAngular,peakImpulse,clearance;
  public int minContacts=6,finalContacts;
  public Vector3 initialPosition,finalPosition;
 }
 [Serializable] public class Report{public string unity,conditions;public Trial[] trials;}
 static List<Trial> trials=new List<Trial>();
 static ImpactPhysicsVerify(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode)frames=0;};EditorApplication.update+=Tick;}
 public static void Run()
 {
  EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity");
  SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();
 }
 static float Tilt(float f)=>Mathf.Abs(Mathf.DeltaAngle(0,f));
 static void Pose(Vector3 p)
 {
  body.position=p;body.rotation=Quaternion.identity;body.transform.SetPositionAndRotation(p,Quaternion.identity);
  body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;
  foreach(var w in wheels){w.motorTorque=0;w.brakeTorque=0;w.steerAngle=0;w.rotationSpeed=0;}
  Physics.SyncTransforms();body.WakeUp();
 }
 static Trial Measure(string name,Vector3 start,float speed,string config)
 {
  Pose(start);for(int i=0;i<150;i++)Physics.Simulate(.02f);
  var r=new Trial{scenario=name,configuration=config,requestedKph=speed,initialPosition=body.position};
  float baseY=body.worldCenterOfMass.y;
  r.clearance=body.GetComponentsInChildren<MeshCollider>().First(c=>c.convex).bounds.min.y-.03f;
  float contactZ=start.z+13-3.5f; int contactStep=-1;float previousSpeed=speed;
  body.linearVelocity=Vector3.forward*(speed/3.6f);r.preKph=body.linearVelocity.magnitude*3.6f;
  var probe=body.gameObject.GetComponent<ImpactMeasurementProbe>()??body.gameObject.AddComponent<ImpactMeasurementProbe>();probe.peakImpulse=0;
  for(int i=0;i<500;i++)
  {
   Physics.Simulate(.02f);
   float now=body.linearVelocity.magnitude*3.6f;
   if(contactStep<0&&body.position.z>=contactZ){contactStep=i;r.preKph=previousSpeed;}
   if(i==contactStep+10&&contactStep>=0)r.postKph=now;
   previousSpeed=now;
   r.maxVertical=Mathf.Max(r.maxVertical,body.linearVelocity.y);r.maxComY=Mathf.Max(r.maxComY,body.worldCenterOfMass.y);
   var e=body.rotation.eulerAngles;r.maxPitch=Mathf.Max(r.maxPitch,Tilt(e.x));r.maxRoll=Mathf.Max(r.maxRoll,Tilt(e.z));r.maxAngular=Mathf.Max(r.maxAngular,body.angularVelocity.magnitude);
   r.minContacts=Mathf.Min(r.minContacts,wheels.Count(w=>w.isGrounded));
   if(contactStep>=0&&i>=contactStep+100)break;
  }
  r.comRise=r.maxComY-baseY;r.finalKph=body.linearVelocity.magnitude*3.6f;r.finalContacts=wheels.Count(w=>w.isGrounded);r.finalPosition=body.position;r.peakImpulse=probe.peakImpulse;
  r.outcome=r.maxRoll>80||r.maxPitch>80?"large rotation / possible rollover":r.comRise>.8f?"airborne / elevated":r.finalKph<2?"stopped":"continued";
  trials.Add(r);return r;
 }
 static void SelectLow(int lane,bool bevel)
 {
  var low=UnityEngine.Object.FindAnyObjectByType<ImpactGroundReset>().transform.Find("01 Low obstacle impacts");
  foreach(var c in low.GetComponentsInChildren<Collider>(true))
   if(c.name.StartsWith("Vertical ")||c.name.StartsWith("Bevel_"))c.enabled=lane<0||(bevel?c.name=="Bevel_"+lane:c.name=="Vertical "+new[]{.05f,.10f,.15f,.20f,.30f,.50f,.70f}[lane].ToString("F2")+" m FIXED");
 }
 static void Tick()
 {
  if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling||frames++<3)return;
  SessionState.SetBool(Flag,false);
  try
  {
   body=GameObject.Find("Tumbler").GetComponent<Rigidbody>();wheels=body.GetComponentsInChildren<WheelCollider>();
   if(wheels.Length!=6)throw new Exception("Expected six active wheels");
   foreach(var mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))mb.enabled=false;
   body.constraints=RigidbodyConstraints.None;body.interpolation=RigidbodyInterpolation.None;Physics.simulationMode=SimulationMode.Script;
   string config=Environment.GetEnvironmentVariable("IMPACT_CONFIG")??"baseline";
   if(config!="baseline")body.gameObject.AddComponent(Type.GetType("TumblerImpactPhysics, Assembly-CSharp",true));
   Directory.CreateDirectory("TestResults/ImpactPhysics");
   using(var f=new StreamWriter("TestResults/ImpactPhysics/"+config+"-setup.txt"))
   {
    f.WriteLine("COM "+body.centerOfMass+" inertia "+body.inertiaTensor+" depen "+body.maxDepenetrationVelocity+" solver "+body.solverIterations+"/"+body.solverVelocityIterations);
    foreach(var w in wheels)f.WriteLine(w.name+" local "+body.transform.InverseTransformPoint(w.transform.position)+" r "+w.radius+" sprung "+w.sprungMass+" k "+w.suspensionSpring.spring+" d "+w.suspensionSpring.damper+" travel "+w.suspensionDistance+" app "+w.forceAppPointDistance);
   }
   for(int lane=0;lane<7;lane++)foreach(float speed in new[]{10f,30f,60f,100f})
   {
    float x=-280-155+lane*20;
    // Separate starts isolate each shape; five-second observation ends before the next obstacle at low speed.
    SelectLow(lane,false);Measure("vertical-"+lane,new Vector3(x,1.2f,-250-63),speed,config);
    SelectLow(lane,true);Measure("bevel-"+lane,new Vector3(x,1.2f,-250-123),speed,config);
   }
   SelectLow(-1,false);
   foreach(float speed in new[]{10f,30f,60f})Measure("wall-frontal",new Vector3(-245,1.2f,-310),speed,config);
   Measure("jump",new Vector3(-235,1.2f,-130),30,config);
   // Free fall regression: no wheels/force can pin the vehicle to the surface.
   Pose(new Vector3(-280,10,-250));Physics.Simulate(.02f);if(body.linearVelocity.y>-.15f)throw new Exception("Gravity regression");
   File.WriteAllText("TestResults/ImpactPhysics/"+config+".json",JsonUtility.ToJson(new Report{unity=Application.unityVersion,conditions="0.02 s; 3 s settle, 2 s observation after approach reference (up to 10 s timeout); free six-wheel vehicle; coast, no motor/brake/boost/downforce; same poses; peaks cover full observation (later contacts may contribute at high speeds)",trials=trials.ToArray()},true));
   Physics.simulationMode=SimulationMode.FixedUpdate;EditorApplication.Exit(0);
  }catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}
 }
}
