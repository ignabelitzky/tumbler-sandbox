using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class BurnoutVerify
{
 static int frames; const string Flag="BurnoutVerify";
 static BurnoutVerify(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode)frames=0;};}
 public static void Run(){EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity");SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();}
 static int checks;
 static void Check(bool value,string label){if(!value)throw new Exception(label);checks++;}
 static void Tick()
 {
  if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying||frames++<4)return;
  SessionState.SetBool(Flag,false);
  try
  {
   var car=GameObject.Find("Tumbler");var body=car.GetComponent<Rigidbody>();
   foreach(var mb in car.GetComponentsInChildren<MonoBehaviour>())mb.enabled=false;
   var all=car.GetComponentsInChildren<WheelCollider>();var front=all.Where(w=>car.transform.InverseTransformPoint(w.transform.position).z>0).ToArray();var rear=all.Except(front).ToArray();
   Check(front.Length==2&&rear.Length==4,"Six wheels");
   Physics.simulationMode=SimulationMode.Script;body.position=new Vector3(-280,1,-250);body.rotation=Quaternion.identity;body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;Physics.SyncTransforms();
   for(int i=0;i<150;i++)Physics.Simulate(.02f);
   Check(all.All(w=>w.isGrounded),"All wheels contact platform");
   var burn=new TumblerBurnout(front,rear);
   body.linearVelocity=Vector3.forward;burn.UpdateState(true,body,true);Check(!burn.Active,"Moving entry rejected");
   body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;burn.UpdateState(false,body,true);burn.UpdateState(true,body,true);Check(burn.Active,"Stationary combination enters");
   foreach(var mode in new[]{TumblerController.driveType.FrontWheelDrive,TumblerController.driveType.RearWheelDrive,TumblerController.driveType.AllWheelDrive})
   {
    burn.Apply(mode,1);Check(all.Count(w=>burn.IsBurning(w,mode))==(mode==TumblerController.driveType.FrontWheelDrive?2:mode==TumblerController.driveType.RearWheelDrive?4:6),"Driven contacts "+mode);
   }
   body.linearVelocity=Vector3.forward*5;burn.UpdateState(true,body,true);Check(burn.Active,"Movement after entry retains latch");
   burn.UpdateState(false,body,true);burn.Apply(TumblerController.driveType.AllWheelDrive,0);Check(!burn.Active,"Release stops");
   body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;burn.UpdateState(true,body,true);burn.UpdateState(true,body,false);Check(!burn.Active,"Airborne cancels");
   burn.UpdateState(true,body,true);Check(!burn.Active,"Landing with held combination cannot restart");
   burn.UpdateState(false,body,true);burn.UpdateState(true,body,true);
   body.position+=Vector3.up*15;Physics.SyncTransforms();for(int i=0;i<3;i++)Physics.Simulate(.02f);
   Check(all.All(w=>!burn.IsBurning(w,TumblerController.driveType.AllWheelDrive)),"Airborne wheels never burn");
   var settings=UnityEngine.Object.Instantiate(InputSystem.settings);settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;settings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;InputSystem.settings=settings;
   var keyboard=InputSystem.AddDevice<Keyboard>();InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.S));InputSystem.Update();Check(TumblerBurnout.BothDirectionsHeld(),"Keyboard opposite inputs read independently");
   InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));InputSystem.Update();Check(!TumblerBurnout.BothDirectionsHeld(),"Single input rejected");InputSystem.RemoveDevice(keyboard);
   var pad=InputSystem.AddDevice<Gamepad>();InputSystem.QueueStateEvent(pad,new GamepadState{leftTrigger=1,rightTrigger=1});InputSystem.Update();Check(TumblerBurnout.BothDirectionsHeld(),"Xbox trigger combination");
   var controller=car.GetComponent<TumblerController>();var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
   var modeField=typeof(TumblerController).GetField("<currentDriveMode>k__BackingField",flags);
   var turns=new System.Collections.Generic.List<string>();
   foreach(var mode in new[]{TumblerController.driveType.FrontWheelDrive,TumblerController.driveType.RearWheelDrive,TumblerController.driveType.AllWheelDrive})foreach(float direction in new[]{-1f,1f})
   {
    controller.enabled=false;foreach(var w in all){w.motorTorque=0;w.brakeTorque=0;w.steerAngle=0;w.rotationSpeed=0;}
    body.position=new Vector3(-280,1,-250);body.rotation=Quaternion.identity;body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;Physics.SyncTransforms();for(int i=0;i<150;i++)Physics.Simulate(.02f);
    body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;modeField.SetValue(controller,mode);controller.enabled=true;controller.ResetTransmission();
    InputSystem.QueueStateEvent(pad,new GamepadState());InputSystem.Update();controller.SendMessage("RefreshDrivingInput");controller.SendMessage("FixedUpdate");Physics.Simulate(.02f);
    body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;
    InputSystem.QueueStateEvent(pad,new GamepadState{leftTrigger=1,rightTrigger=1,leftStick=new Vector2(direction,0)});InputSystem.Update();controller.SendMessage("RefreshDrivingInput");
    float yaw=0;
    for(int i=0;i<250;i++){controller.SendMessage("FixedUpdate");Physics.Simulate(.02f);yaw+=body.angularVelocity.y*.02f*Mathf.Rad2Deg;}
    Check(yaw*direction>10,"Physical steering response "+mode+" direction "+direction+": "+yaw+" active="+controller.IsBurnoutActive+" steer="+typeof(TumblerController).GetField("steerInput",flags).GetValue(controller)+" velocity="+body.linearVelocity+" rpm="+all[0].rpm);turns.Add(mode+" steer="+direction+" yaw="+yaw+" deg / 5 s");controller.enabled=false;
   }
   Directory.CreateDirectory("TestResults/Burnout");File.WriteAllLines("TestResults/Burnout/steering.txt",turns);
   var effects=car.GetComponent<TumblerEffects>();
   var smokes=(ParticleSystem[])typeof(TumblerEffects).GetField("wheelSmokes",flags).GetValue(effects);
   Check(smokes.Length==6&&smokes.All(v=>v!=null),"Smoke supports all six tires");
   Check(smokes.All(v=>v.GetComponent<ParticleSystemRenderer>().sharedMaterial.shader.name=="Tumbler/Tire Smoke HDRP"),"All smoke uses dedicated HDRP shader");
   Check(smokes.All(v=>v.main.maxParticles==256&&!v.main.startSize3D),"Smoke size and particle budgets bounded");
   foreach(var w in all){w.motorTorque=0;w.brakeTorque=0;w.rotationSpeed=0;w.steerAngle=0;}
   body.position=new Vector3(-280,1,-250);body.rotation=Quaternion.identity;body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;Physics.SyncTransforms();for(int i=0;i<150;i++)Physics.Simulate(.02f);
   controller.enabled=true;InputSystem.QueueStateEvent(pad,new GamepadState());InputSystem.Update();controller.SendMessage("RefreshDrivingInput");controller.SendMessage("FixedUpdate");body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;
   InputSystem.QueueStateEvent(pad,new GamepadState{leftTrigger=1,rightTrigger=1});InputSystem.Update();controller.SendMessage("RefreshDrivingInput");controller.SendMessage("FixedUpdate");
   foreach(var mode in new[]{TumblerController.driveType.FrontWheelDrive,TumblerController.driveType.RearWheelDrive,TumblerController.driveType.AllWheelDrive})
   {
    modeField.SetValue(controller,mode);foreach(var smoke in smokes)smoke.Clear();
    for(int i=0;i<20;i++)effects.SendMessage("FixedUpdate");
    Check(smokes.Count(smoke=>smoke.particleCount>0)==(mode==TumblerController.driveType.FrontWheelDrive?2:mode==TumblerController.driveType.RearWheelDrive?4:6),"Actual smoke emission by drive mode "+mode+" active="+controller.IsBurnoutActive+" grounded="+all.Count(w=>w.isGrounded)+" dt="+Time.deltaTime+" counts="+string.Join(",",smokes.Select(v=>v.particleCount)));
   }
   foreach(var smoke in smokes)smoke.Clear();body.position+=Vector3.up*15;Physics.SyncTransforms();for(int i=0;i<3;i++)Physics.Simulate(.02f);for(int i=0;i<20;i++)effects.SendMessage("FixedUpdate");Check(smokes.All(smoke=>smoke.particleCount==0),"Actual airborne smoke suppressed");
   InputSystem.RemoveDevice(pad);
   Physics.simulationMode=SimulationMode.FixedUpdate;Directory.CreateDirectory("TestResults/Burnout");File.WriteAllText("TestResults/Burnout/checks.txt","PASS "+checks+" native burnout checks\n");EditorApplication.Exit(0);
  }catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}
 }
}
