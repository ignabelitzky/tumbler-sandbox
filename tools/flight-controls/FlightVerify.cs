using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class FlightVerify
{
    const string Folder="TestResults/Flight";const string Flag="FlightVerification";
    static Rigidbody body;static TumblerController controller;static WheelCollider[] wheels;
    [Serializable] public class Drop{public float height,horizontalSpeed,peakReboundSpeed,reboundHeight,settleTime,impactSpeed,finalSpeed,finalUp;public string name;public int airborneFramesAfterContact;}
    [Serializable] public class Comparison{public float timestep;public Drop[] before,after;}
    [Serializable] public class Report{public string unity;public Drop[] drops;public string[] suspension;}
    static FlightVerify(){EditorApplication.update+=Tick;}
    public static void Baseline(){Run("baseline");}
    public static void Verify(){Run("after");}
    public static void CompareDefaultStep(){Run("default-step");}
    static void Run(string mode)
    {
        Directory.CreateDirectory(Folder);File.Delete(Folder+"/failed.txt");EditorSceneManager.playModeStartScene=null;EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity");SessionState.SetString(Flag,mode);EditorApplication.EnterPlaymode();
    }
    static void Set(string name,object value)=>typeof(TumblerController).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).SetValue(controller,value);
    static void Step()
    {
        controller.SendMessage("FixedUpdate");
        var grip=Object.FindFirstObjectByType<WheelSurfaceGrip>();if(grip!=null&&grip.enabled)grip.SendMessage("FixedUpdate");
        var hull=body.GetComponent<TumblerChassisFriction>();if(hull!=null&&hull.enabled)hull.SendMessage("FixedUpdate");
        Physics.Simulate(Time.fixedDeltaTime);
    }
    static void Reset(float y,float speed,Quaternion rotation)
    {
        body.constraints=RigidbodyConstraints.None;body.position=new Vector3(0,y,0);body.rotation=rotation;body.transform.SetPositionAndRotation(body.position,rotation);body.linearVelocity=Vector3.forward*speed;body.angularVelocity=Vector3.zero;
        controller.ResetTransmission();Set("currentPower",0f);Set("currentBrakeForce",0f);Set("steerInput",0f);Set("currentTurnAngle",0f);Set("isHandbrakeActive",false);
        foreach(var w in wheels){w.motorTorque=0;w.brakeTorque=0;w.steerAngle=0;w.rotationSpeed=0;}
        Physics.SyncTransforms();
    }
    static Drop DropFrom(float height,float speed,float pitch=0,float roll=0)
    {
        Reset(101.08f+height,speed,Quaternion.Euler(pitch,0,roll));
        var d=new Drop{name=$"{height}m-{speed*3.6f}kph-p{pitch}-r{roll}",height=height,horizontalSpeed=speed};
        bool touched=false,rebounded=false;float elapsed=0,minHeight=float.MaxValue,maxHeight=-1,quiet=0;var trace=new List<string>{"time,y,vertical_speed,horizontal_speed,wheels,pitch,roll"};
        for(int i=0;i<700;i++)
        {
            float before=body.linearVelocity.y;Step();int contact=wheels.Count(w=>w.isGrounded);
            if(!touched&&contact>=2){touched=true;d.impactSpeed=-before;}
            if(touched)
            {
                elapsed+=Time.fixedDeltaTime;minHeight=Mathf.Min(minHeight,body.position.y);
                if(body.linearVelocity.y>0){rebounded=true;d.peakReboundSpeed=Mathf.Max(d.peakReboundSpeed,body.linearVelocity.y);}
                if(rebounded)maxHeight=Mathf.Max(maxHeight,body.position.y);
                if(contact==0)d.airborneFramesAfterContact++;
                if(contact>=4&&Mathf.Abs(body.linearVelocity.y)<.15f&&body.angularVelocity.magnitude<.15f)quiet+=Time.fixedDeltaTime;else quiet=0;
                if(d.settleTime==0 && quiet>.4f)d.settleTime=elapsed;
            }
            if(i%2==0)trace.Add($"{i*Time.fixedDeltaTime:F3},{body.position.y:F4},{body.linearVelocity.y:F4},{Vector3.ProjectOnPlane(body.linearVelocity,Vector3.up).magnitude:F4},{contact},{body.rotation.eulerAngles.x:F3},{body.rotation.eulerAngles.z:F3}");
        }
        d.reboundHeight=maxHeight<0?0:Mathf.Max(0,maxHeight-minHeight);d.finalSpeed=Vector3.ProjectOnPlane(body.linearVelocity,Vector3.up).magnitude;d.finalUp=body.transform.up.y;
        File.WriteAllLines(Folder+"/"+SessionState.GetString(Flag, "baseline")+"-"+d.name+".csv",trace);return d;
    }
    static void Tick()
    {
        string mode=SessionState.GetString(Flag,"");if(mode==""||!EditorApplication.isPlaying||Time.frameCount<40)return;
        try
        {
            controller=Object.FindFirstObjectByType<TumblerController>();body=controller.GetComponent<Rigidbody>();wheels=body.GetComponentsInChildren<WheelCollider>();
            foreach(var mb in body.GetComponentsInChildren<MonoBehaviour>())if(!(mb is TumblerImpactPhysics)&&!(mb is TumblerChassisFriction))mb.enabled=false;
            var flightField=typeof(TumblerController).GetField("flight",BindingFlags.Instance|BindingFlags.NonPublic);
            var dynamics=flightField?.GetValue(controller);
            if(mode!="default-step")dynamics?.GetType().GetMethod("Enable").Invoke(dynamics,null);
            body.interpolation=RigidbodyInterpolation.None;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Temporary landing asphalt";floor.transform.position=new Vector3(0,99,0);floor.transform.localScale=new Vector3(1600,2,1600);
            floor.GetComponent<Collider>().sharedMaterial=new PhysicsMaterial("Landing test asphalt"){bounciness=0,staticFriction=.65f,dynamicFriction=.65f};
            Time.fixedDeltaTime=.01f;Physics.simulationMode=SimulationMode.Script;
            string[] spring=wheels.Select(w=>$"{w.name}: sprungMass={w.sprungMass} spring={w.suspensionSpring.spring} damper={w.suspensionSpring.damper} travel={w.suspensionDistance} target={w.suspensionSpring.targetPosition} radius={w.radius}").ToArray();
            if(mode=="default-step")
            {
                Time.fixedDeltaTime=.02f;
                var before=new List<Drop>();var after=new List<Drop>();
                SessionState.SetString(Flag,"before-default");foreach(float h in new[]{5f,10f})foreach(float speed in new[]{0f,20f})before.Add(DropFrom(h,speed));
                dynamics.GetType().GetMethod("Enable").Invoke(dynamics,null);
                SessionState.SetString(Flag,"after-default");foreach(float h in new[]{5f,10f})foreach(float speed in new[]{0f,20f})after.Add(DropFrom(h,speed));
                File.WriteAllText(Folder+"/default-timestep-comparison.json",JsonUtility.ToJson(new Comparison{timestep=.02f,before=before.ToArray(),after=after.ToArray()},true));
                for(int i=0;i<before.Count;i++)if(after[i].peakReboundSpeed>before[i].peakReboundSpeed*.7f || after[i].reboundHeight>before[i].reboundHeight*.8f || after[i].finalUp<.98f)throw new Exception("Default 50Hz landing acceptance "+after[i].name);
                File.WriteAllText(Folder+"/default-timestep-comparison.json",JsonUtility.ToJson(new Comparison{timestep=.02f,before=before.ToArray(),after=after.ToArray()},true));SessionState.SetString(Flag,"");Physics.simulationMode=SimulationMode.FixedUpdate;EditorApplication.Exit(0);return;
            }
            var drops=new List<Drop>();foreach(float h in new[]{5f,10f})foreach(float s in new[]{0f,20f})drops.Add(DropFrom(h,s));
            drops.Add(DropFrom(7.5f,15,8,6));
            if(mode=="after")
            {
                var baseline=JsonUtility.FromJson<Report>(File.ReadAllText(File.Exists(Folder+"/baseline-drops.json")?Folder+"/baseline-drops.json":"docs/flight-camera-landing/baseline-drops.json"));
                var checks=new List<string>();
                foreach(var d in drops)
                {
                    var previous=baseline.drops.Single(b=>b.name==d.name);
                    if(d.peakReboundSpeed>=previous.peakReboundSpeed*.6f || d.reboundHeight>=previous.reboundHeight*.75f || d.finalUp<.98f || d.settleTime<=0 || d.settleTime>1.3f)throw new Exception("Landing improvement acceptance: "+d.name);
                    checks.Add("PASS "+d.name+": rebound speed and height reduced; settles upright within 1.3 seconds");
                }
                File.WriteAllLines(Folder+"/landing-checks.txt",checks);
            }
            File.WriteAllText(Folder+"/"+mode+"-drops.json",JsonUtility.ToJson(new Report{unity=Application.unityVersion,drops=drops.ToArray(),suspension=spring},true));
            Physics.simulationMode=SimulationMode.FixedUpdate;SessionState.SetString(Flag,"");EditorApplication.Exit(0);
        }catch(Exception e){File.WriteAllText(Folder+"/failed.txt",e.ToString());Debug.LogException(e);SessionState.SetString(Flag,"");EditorApplication.Exit(1);}
    }
}
