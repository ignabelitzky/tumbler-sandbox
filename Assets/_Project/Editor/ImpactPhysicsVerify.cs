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
  public float movableMass,movableDisplacement,movableSpeed;
  public Vector3 initialPosition,finalPosition;
  public Quaternion initialRotation;
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
 static Trial Measure(string name,Vector3 start,float speed,string config,float observationSeconds=2f)
 {
  Pose(start);for(int i=0;i<150;i++)Physics.Simulate(.02f);
  // Reanchor after settling: tire coast during settling must not change approach distance.
  // Baseline measured flat-platform rest height, with identical orientation and wheel spin.
  Pose(new Vector3(start.x,.9708383f,start.z));
  var r=new Trial{scenario=name,configuration=config,requestedKph=speed,initialPosition=body.position,initialRotation=body.rotation};
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
   if(contactStep>=0&&i>=contactStep+Mathf.RoundToInt(observationSeconds/.02f))break;
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
 static void Recovery()
 {
  var fixtures=new List<GameObject>();var origin=new Vector3(240,100,440);
  Action<Vector3,Vector3> box=(pos,size)=>{var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.transform.position=origin+pos;g.transform.localScale=size;fixtures.Add(g);};
  box(new Vector3(0,-.5f,0),new Vector3(.65f,1,40));box(new Vector3(0,.03f,13.4f),new Vector3(.65f,.06f,20));
  body.constraints=RigidbodyConstraints.FreezePositionX|RigidbodyConstraints.FreezeRotation;
  Pose(origin+new Vector3(0,.74f,0));for(int i=0;i<70;i++)Physics.Simulate(.02f);float start=body.position.z;
  for(int i=0;i<150;i++){body.AddForce(Vector3.forward*9000);Physics.Simulate(.02f);}
  float curbTravel=body.position.z-start;if(curbTravel<2.5f)throw new Exception("Front face regressed 6 cm underbody rail recovery: "+curbTravel);
  foreach(var g in fixtures)UnityEngine.Object.DestroyImmediate(g);fixtures.Clear();
  box(new Vector3(0,-1,-60),new Vector3(20,2,120));box(new Vector3(0,-2.5f,60),new Vector3(20,2,120));
  var controller=body.GetComponent<TumblerController>();var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
  var power=typeof(TumblerController).GetField("currentPower",flags);var mode=typeof(TumblerController).GetField("<currentDriveMode>k__BackingField",flags);
  var lines=new List<string>{"PASS enabled front face retains 6 cm rail climb: "+curbTravel};
  foreach(var drive in new[]{TumblerController.driveType.AllWheelDrive,TumblerController.driveType.FrontWheelDrive,TumblerController.driveType.RearWheelDrive})
  {
   body.constraints=RigidbodyConstraints.FreezePositionX|RigidbodyConstraints.FreezeRotationY|RigidbodyConstraints.FreezeRotationZ;
   Pose(origin+new Vector3(0,1.1f,.9f));body.rotation=Quaternion.Euler(12,0,0);body.transform.rotation=body.rotation;Physics.SyncTransforms();
   mode.SetValue(controller,drive);power.SetValue(controller,0f);for(int i=0;i<100;i++)Physics.Simulate(.02f);
   power.SetValue(controller,1200f);for(int i=0;i<250;i++){controller.SendMessage("FixedUpdate");Physics.Simulate(.02f);}
   if(body.position.z<origin.z+3.5f||body.position.y<origin.y-1)throw new Exception("Front face ledge regression "+drive+" "+(body.position-origin));
   lines.Add("PASS enabled front face forward ledge escape "+drive+": "+(body.position-origin));
  }
  body.constraints=RigidbodyConstraints.None;
  foreach(var g in fixtures)UnityEngine.Object.DestroyImmediate(g);
  File.WriteAllLines("TestResults/ImpactPhysics/recovery-checks.txt",lines);
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
   if(config!="baseline")
   {
    var type=Type.GetType("TumblerImpactPhysics, Assembly-CSharp",true);
    var calibration=body.GetComponent(type) as Behaviour;
    if(calibration==null)body.gameObject.AddComponent(type);else calibration.enabled=true;
   }
   Directory.CreateDirectory("TestResults/ImpactPhysics");
   using(var f=new StreamWriter("TestResults/ImpactPhysics/"+config+"-setup.txt"))
   {
    f.WriteLine("COM "+body.centerOfMass+" inertia "+body.inertiaTensor+" depen "+body.maxDepenetrationVelocity+" solver "+body.solverIterations+"/"+body.solverVelocityIterations);
    foreach(var w in wheels)f.WriteLine(w.name+" local "+body.transform.InverseTransformPoint(w.transform.position)+" r "+w.radius+" sprung "+w.sprungMass+" k "+w.suspensionSpring.spring+" d "+w.suspensionSpring.damper+" travel "+w.suspensionDistance+" app "+w.forceAppPointDistance);
   }
   if(config=="recovery")Recovery();
   else
   {
   for(int lane=0;lane<7;lane++)foreach(float speed in new[]{10f,30f,60f,100f})
   {
    float x=-280-155+lane*20;
    // Separate starts and isolated colliders prevent the other low obstacle from contaminating the trial.
    SelectLow(lane,false);Measure("vertical-"+lane,new Vector3(x,1.2f,-250-63),speed,config);
    SelectLow(lane,true);Measure("bevel-"+lane,new Vector3(x,1.2f,-250-123),speed,config);
   }
   SelectLow(-1,false);
   foreach(float speed in new[]{10f,30f,60f})Measure("wall-frontal",new Vector3(-245,1.2f,-310),speed,config);
   foreach(float speed in new[]{30f,60f})Measure("jump",new Vector3(-235,1.2f,-130),speed,config,speed==30?5:3);
   for(int wall=1;wall<6;wall++)foreach(float speed in new[]{10f,30f})
    Measure("wall-"+wall,new Vector3(-245+wall*24+(wall==4?2.5f:wall==5?4.7f:0),1.2f,-308),speed,config);
   var reset=UnityEngine.Object.FindAnyObjectByType<ImpactGroundReset>();
   var movable=reset.GetComponentsInChildren<Rigidbody>();
   foreach(float speed in new[]{10f,30f})for(int i=0;i<5;i++)
   {
    reset.RestoreObjects();Physics.SyncTransforms();
    var target=movable.Single(r=>Mathf.Abs(r.position.x-(-430+i*28))<.01f);
    var targetStart=target.position;
    var movingTrial=Measure("movable-"+i,new Vector3(-430+i*28,1.2f,-193),speed,config);
    movingTrial.movableMass=target.mass;movingTrial.movableDisplacement=Vector3.Distance(target.position,targetStart);movingTrial.movableSpeed=target.linearVelocity.magnitude;
   }
   var original=movable.Select(r=>r.position).ToArray();reset.RestoreObjects();
   var restored=movable.Select(r=>r.position).ToArray();var carPosition=body.position;
   for(int i=0;i<movable.Length;i++){movable[i].position+=Vector3.right*3;movable[i].linearVelocity=Vector3.one;}
   reset.RestoreObjects();
   if(!movable.Select((r,i)=>Vector3.Distance(r.position,restored[i])<.001f&&r.linearVelocity==Vector3.zero).All(v=>v)||body.position!=carPosition)throw new Exception("Local reset regression");
   File.WriteAllText("TestResults/ImpactPhysics/"+config+"-runtime-checks.txt","PASS local reset restores all five fixture poses and velocities, leaves vehicle unchanged\n");
   }
   // Free fall regression: no wheels/force can pin the vehicle to the surface.
   Pose(new Vector3(-280,10,-250));Physics.Simulate(.02f);if(body.linearVelocity.y>-.15f)throw new Exception("Gravity regression");
   File.WriteAllText("TestResults/ImpactPhysics/"+config+".json",JsonUtility.ToJson(new Report{unity=Application.unityVersion,conditions="0.02 s; 3 s settle, 2 s observation after approach reference (up to 10 s timeout); free six-wheel vehicle; coast, no motor/brake/boost/downforce; same poses; peaks cover full observation; jump windows 5 s at 30 / 3 s at 60; post-settle canonical pose, orientation, zero wheel spin",trials=trials.ToArray()},true));
   Physics.simulationMode=SimulationMode.FixedUpdate;EditorApplication.Exit(0);
  }catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}
 }
}
