using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class ClassicFixes
{
    const string Flag="ClassicFixes";
    static readonly BindingFlags Fields=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static readonly List<string> checks=new List<string>();
    static Gamepad pad;
    static ClassicFixes(){EditorApplication.update+=Tick;}
    public static void Run(){Directory.CreateDirectory(ClassicSandbox.Results);EditorSceneManager.playModeStartScene=null;EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity");SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();}
    static void Call(object value,string method,params object[] args)=>value.GetType().GetMethod(method,Fields).Invoke(value,args);
    static void Set(object value,string name,object data)=>value.GetType().GetField(name,Fields).SetValue(value,data);
    static T Get<T>(object value,string name)=>(T)value.GetType().GetField(name,Fields).GetValue(value);
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks.Add("PASS "+message);}
    static void Press(GamepadState state){InputSystem.QueueStateEvent(pad,state);InputSystem.Update();}
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying||Time.frameCount<50)return;SessionState.SetBool(Flag,false);
        try
        {
            Physics.simulationMode=SimulationMode.Script;
            typeof(GameNavigation).GetProperty("IsPaused").GetSetMethod(true).Invoke(null,new object[]{false});typeof(GameNavigation).GetProperty("WaitingForMenuRelease",BindingFlags.Static|BindingFlags.NonPublic).GetSetMethod(true).Invoke(null,new object[]{false});
            var car=Object.FindFirstObjectByType<TumblerController>();var body=car.GetComponent<Rigidbody>();var wheels=body.GetComponentsInChildren<WheelCollider>();
            body.interpolation=RigidbodyInterpolation.None;body.position=new Vector3(0,500,0);body.rotation=Quaternion.identity;body.transform.SetPositionAndRotation(body.position,body.rotation);body.linearVelocity=Vector3.forward*60;body.angularVelocity=Vector3.zero;Physics.SyncTransforms();
            var old=InputSystem.settings;var settings=Object.Instantiate(old);InputSystem.settings=settings;settings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;pad=InputSystem.AddDevice<Gamepad>();
            Press(new GamepadState{leftTrigger=1});Call(car,"RefreshDrivingInput");Call(car,"FixedUpdate");
            Check(Mathf.Abs(body.linearVelocity.z-60)<.001f,"LT at 216 km/h does not instantly cut forward velocity");Check(wheels.Sum(w=>w.motorTorque)< -100,"Opposing motor torque remains available above reverse speed limit");
            body.linearVelocity=Vector3.back*30;Press(new GamepadState{rightTrigger=1});Call(car,"RefreshDrivingInput");Call(car,"FixedUpdate");Check(Mathf.Abs(body.linearVelocity.z+80/3.6f)<.01f,"Reverse governor follows actual backward travel");
            body.linearVelocity=body.angularVelocity=Vector3.zero;body.rotation=Quaternion.Euler(40,25,110);body.transform.rotation=body.rotation;Physics.SyncTransforms();Press(new GamepadState().WithButton(GamepadButton.North));Physics.Simulate(.02f);
            Check(Vector3.Dot(body.transform.up,Vector3.up)>.999f&&Mathf.Abs(body.transform.forward.y)<.001f,"Y removes both pitch and roll from a tilted recovery");
            Press(new GamepadState());body.linearVelocity=Vector3.zero;body.rotation=Quaternion.Euler(-90,35,0);body.transform.rotation=body.rotation;Physics.SyncTransforms();Press(new GamepadState().WithButton(GamepadButton.North));Physics.Simulate(.02f);Check(body.transform.up.y>.999f,"Vertical-heading recovery uses a stable horizontal fallback");
            Press(new GamepadState{rightTrigger=1});Call(car,"RefreshDrivingInput");Call(car,"FixedUpdate");Call(car,"FixedUpdate");Check(wheels.Sum(w=>Mathf.Abs(w.motorTorque))>100,"Drive outputs active before shutdown test");car.enabled=false;
            Check(wheels.All(w=>w.motorTorque==0&&w.brakeTorque==0&&w.steerAngle==0),"Disabling controller clears motor, brakes and steering");car.enabled=true;
            var systems=body.GetComponent<TumblerSystems>();var particles=Get<ParticleSystem[]>(systems,"turbineParticles");var active=particles.Where(p=>p!=null&&p.isPlaying).ToArray();Check(active.Length>=1,"Idle turbine particles active before stealth test");
            systems.SetStealth(true);systems.enabled=false;systems.enabled=true;Check(!systems.IsStealth&&active.All(p=>p.isPlaying),"Component disable/enable restores captured turbine particles");
            systems.SetStealth(true);body.gameObject.SetActive(false);body.gameObject.SetActive(true);Check(!systems.IsStealth&&active.All(p=>p.isPlaying),"Whole-vehicle reactivation restores deferred turbine particles");
            Check(!body.GetComponent<Boost>().IsBoostActive,"Restoring stealth cannot ignite turbo");
            car.enabled=false;foreach(var w in wheels)w.motorTorque=w.brakeTorque=w.steerAngle=0;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(0,99,0);floor.transform.localScale=new Vector3(100,2,100);
            body.constraints=RigidbodyConstraints.FreezeAll;body.position=new Vector3(0,101.08f,0);body.rotation=Quaternion.identity;body.transform.SetPositionAndRotation(body.position,body.rotation);body.linearVelocity=body.angularVelocity=Vector3.zero;Physics.SyncTransforms();for(int i=0;i<10;i++)Physics.Simulate(.02f);
            var audio=body.GetComponent<EngineAudio>();var sources=Get<AudioSource[]>(audio,"tireScreechSounds");var audioWheels=Get<WheelCollider[]>(audio,"tireColliders");Set(audio,"currentSpeed",10f);Set(car,"isHandbrakeActive",true);Call(audio,"UpdateTireScreech");Check(sources.Any(p=>p.isPlaying),"Grounded tire sources start while handbraking");
            body.rotation=Quaternion.Euler(18,0,0);body.transform.rotation=body.rotation;Physics.SyncTransforms();Physics.Simulate(.02f);Call(audio,"UpdateTireScreech");
            Check(audioWheels.Any(w=>!w.isGrounded)&&audioWheels.Any(w=>w.isGrounded),"Audio fixture changes support between axles");
            Check(Enumerable.Range(0,sources.Length).All(i=>sources[i].isPlaying==audioWheels[i].isGrounded),"Each tire source follows its current contact, including new contacts");
            Set(car,"isHandbrakeActive",false);Call(audio,"UpdateTireScreech");Check(sources.All(p=>!p.isPlaying),"Handbrake release stops all tire sources");
            InputSystem.RemoveDevice(pad);InputSystem.settings=old!=null?old:Object.Instantiate(settings);Object.DestroyImmediate(settings);Physics.simulationMode=SimulationMode.FixedUpdate;
            File.WriteAllLines(ClassicSandbox.Results+"/fix-checks.txt",checks);EditorApplication.Exit(0);
        }
        catch(Exception e){File.WriteAllLines(ClassicSandbox.Results+"/fix-failed.txt",checks.Concat(new[]{e.ToString()}));Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
