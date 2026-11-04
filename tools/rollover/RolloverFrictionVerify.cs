using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class RolloverFrictionVerify
{
    const string Flag = "RolloverFrictionVerify";
    const string Folder = "TestResults/RolloverFriction";
    static Rigidbody body;
    static WheelCollider[] wheels;
    static TumblerChassisFriction grip;
    static MeshCollider hull;
    static BoxCollider roof;
    static List<string> checks = new List<string>();
    static List<Trial> trials = new List<Trial>();
    static readonly Vector3 Origin = new Vector3(240, 100, 440);
    [Serializable] public class Trial
    {
        public string name, configuration;
        public Vector3 start, end, initialVelocity;
        public Quaternion initialRotation;
        public float distance, finalSpeed, peakAngular, angularTravel, rollTravel, maxRise;
        public int wheelContacts;
    }
    [Serializable] public class Report { public string unity; public Trial[] trials; }
    static RolloverFrictionVerify() { EditorApplication.update += Tick; }
    static void Check(bool ok, string label)
    {
        if (!ok) throw new Exception(label);
        checks.Add("PASS " + label);
    }
    public static void Run()
    {
        Directory.CreateDirectory(Folder);
        File.Delete(Folder + "/failed.txt");
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity");
        SessionState.SetBool(Flag, true); EditorApplication.EnterPlaymode();
    }
    static void Pose(Quaternion rotation, float drop)
    {
        body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
        body.position = Origin + Vector3.up * 5; body.rotation = rotation;
        body.transform.SetPositionAndRotation(body.position, rotation);
        foreach (var w in wheels) { w.motorTorque = 0; w.brakeTorque = 0; w.rotationSpeed = 0; w.steerAngle = 0; }
        Physics.SyncTransforms();
        float min = body.GetComponentsInChildren<Collider>().Where(c=>c.enabled && !c.isTrigger).Min(c=>c.bounds.min.y);
        body.position += Vector3.up * (Origin.y - min + drop);
        body.transform.position = body.position; Physics.SyncTransforms(); body.WakeUp();
    }
    static void Simulate(int frames)
    {
        for (int i=0;i<frames;i++) { if(grip.enabled) grip.SendMessage("FixedUpdate"); Physics.Simulate(.02f); }
    }
    static Trial Measure(string name, bool improved, float roll, Vector3 velocity, float drop, bool settle)
    {
        grip.enabled = improved;
        Pose(Quaternion.Euler(0,0,roll), drop);
        if(settle) Simulate(150);
        if(grip.enabled) grip.SendMessage("FixedUpdate");
        // Reanchor after settling to pair exact starts, independently of tiny settling drift.
        if(settle)
        {
            body.position = new Vector3(Origin.x, body.position.y, Origin.z);
            body.transform.position = body.position; Physics.SyncTransforms();
        }
        body.linearVelocity = velocity; body.angularVelocity = Vector3.zero; body.WakeUp();
        var t=new Trial { name=name,configuration=improved?"scrape grip":"original materials",start=body.position,
            initialRotation=body.rotation,initialVelocity=velocity };
        var previous=body.position;
        for(int i=0;i<250;i++)
        {
            var oldRotation=body.rotation;
            Simulate(1);
            t.distance+=Vector3.ProjectOnPlane(body.position-previous,Vector3.up).magnitude;
            t.angularTravel+=Quaternion.Angle(oldRotation,body.rotation);
            t.rollTravel+=Mathf.Abs(Vector3.Dot(body.angularVelocity,body.transform.forward))*.02f*Mathf.Rad2Deg;
            t.peakAngular=Mathf.Max(t.peakAngular,body.angularVelocity.magnitude);
            t.maxRise=Mathf.Max(t.maxRise,body.position.y-t.start.y);
            previous=body.position;
        }
        t.end=body.position;t.finalSpeed=Vector3.ProjectOnPlane(body.linearVelocity,Vector3.up).magnitude;
        t.wheelContacts=wheels.Count(w=>w.isGrounded);trials.Add(t);return t;
    }
    static void Pair(string name,float roll,Vector3 v,float drop,bool settle)
    {
        var a=Measure(name,false,roll,v,drop,settle);var b=Measure(name,true,roll,v,drop,settle);
        // Settled side poses can diverge physically before velocity injection; drop trials
        // use exact starts. Only assert start pairing for the latter and the stable roof.
        if(!settle || roll==180)
        {
            Check(Vector3.Distance(a.start,b.start)<.01f,name+": matched initial positions");
            Check(Quaternion.Angle(a.initialRotation,b.initialRotation)<.5f,name+": matched initial orientations");
        }
        Check(float.IsFinite(b.distance)&&b.peakAngular<30,name+": finite bounded native response");
        if(roll==180 && settle)
        {
            Check(b.distance<a.distance*.85f,name+": roof scrape reduces five-second sliding distance by at least 15%");
            Check(b.finalSpeed<=a.finalSpeed+.05f,name+": roof scrape stops or lowers residual speed");
        }
        if(roll==0 && !settle)
            Check(Mathf.Abs(a.distance-b.distance)<.05f && Mathf.Abs(a.finalSpeed-b.finalSpeed)<.05f,name+": wheels-down coast unchanged");
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying||Time.frameCount<10)return;
        SessionState.SetBool(Flag,false);
        try
        {
            body=GameObject.Find("Tumbler").GetComponent<Rigidbody>();
            grip=body.GetComponent<TumblerChassisFriction>();
            Check(grip!=null,"Existing impact calibration creates chassis friction at runtime");
            foreach(var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if(mb!=grip && !(mb is TumblerImpactPhysics))mb.enabled=false;
            wheels=body.GetComponentsInChildren<WheelCollider>();
            hull=body.transform.Find("Body").GetComponent<MeshCollider>();roof=hull.GetComponent<BoxCollider>();
            body.constraints=RigidbodyConstraints.None;body.interpolation=RigidbodyInterpolation.None;
            Physics.simulationMode=SimulationMode.Script;
            var com=body.centerOfMass;var inertia=body.inertiaTensor;var mass=body.mass;
            grip.enabled=false;var originalHull=hull.sharedMaterial;var originalRoof=roof.sharedMaterial;
            var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="Temporary rollover asphalt";
            ground.transform.position=Origin-Vector3.up*.5f;ground.transform.localScale=new Vector3(600,1,600);
            var groundMat=new PhysicsMaterial("Temporary asphalt") {staticFriction=.6f,dynamicFriction=.6f,bounciness=0};
            ground.GetComponent<Collider>().sharedMaterial=groundMat;
            Physics.SyncTransforms();
            grip.enabled=true;Pose(Quaternion.identity,2);grip.SendMessage("FixedUpdate");
            Check(hull.sharedMaterial==originalHull,"Upright belly retains its exact glide material");
            Check(roof.sharedMaterial!=originalRoof && roof.sharedMaterial.dynamicFriction==.65f,"Upper body uses native scrape material");
            Pose(Quaternion.Euler(0,0,90),2);grip.SendMessage("FixedUpdate");
            Check(hull.sharedMaterial.dynamicFriction==.65f,"Sideways lower hull gains scrape grip");
            Pose(Quaternion.Euler(0,0,180),2);grip.SendMessage("FixedUpdate");
            Check(hull.sharedMaterial.dynamicFriction==.65f,"Inverted lower hull retains scrape grip");
            Pose(Quaternion.identity,2);grip.SendMessage("FixedUpdate");
            Check(hull.sharedMaterial==originalHull,"Returning upright restores belly glide");
            grip.enabled=false;
            Check(hull.sharedMaterial==originalHull && roof.sharedMaterial==originalRoof,"Disable restores original shared materials immediately");
            Check(body.mass==mass && body.centerOfMass==com && body.inertiaTensor==inertia,"Mass, COM and inertia remain unchanged");
            foreach(float speed in new[]{10f,20f})
            {
                Pair("roof settled forward "+speed,180,Vector3.forward*speed,.05f,true);
                Pair("roof settled lateral "+speed,180,Vector3.right*speed,.05f,true);
                foreach(float roll in new[]{60f,90f,120f,180f})
                {
                    Pair("drop forward "+roll+" "+speed,roll,new Vector3(0,-4,speed),1,false);
                    Pair("drop lateral "+roll+" "+speed,roll,new Vector3(speed,-4,0),1,false);
                }
            }
            Pair("upright landing control",0,new Vector3(0,-4,15),1,false);
            foreach(var name in new[]{"drop forward 90 10","drop lateral 90 10","drop forward 90 20","drop lateral 90 20"})
            {
                var pair=trials.Where(t=>t.name==name).ToArray();
                Check(pair[1].distance<pair[0].distance*.4f,name+": side landing no longer glides for most of the observation window");
            }
            var sideways=trials.Where(t=>t.name=="drop lateral 90 20").ToArray();
            Check(sideways[1].rollTravel>sideways[0].rollTravel+360,"Fast lateral side landing produces additional native roll travel");
            foreach(var group in trials.Where(t=>t.name.StartsWith("drop") && t.name.Contains("180")).GroupBy(t=>t.name))
            {
                var pair=group.ToArray();
                Check(pair[1].distance<pair[0].distance*.85f,group.Key+": roof-first landing reduces sliding distance");
            }
            grip.enabled=true;
            Pose(Quaternion.Euler(0,0,90),5);body.linearVelocity=Vector3.right*15;grip.SendMessage("FixedUpdate");
            var velocity=body.linearVelocity;
            Simulate(10);
            Check(Mathf.Abs(body.linearVelocity.x-velocity.x)<.2f,"Scrape grip does not brake the car while airborne");
            Check(body.linearVelocity.y < -1.5f,"Scrape grip leaves gravity free to act");
            Check(body.constraints==RigidbodyConstraints.None,"All body axes remain free during landing trials");
            File.WriteAllLines(Folder+"/checks.txt",checks);
            File.WriteAllText(Folder+"/trials.json",JsonUtility.ToJson(new Report{unity=Application.unityVersion,trials=trials.ToArray()},true));
            Physics.simulationMode=SimulationMode.FixedUpdate;
            Object.DestroyImmediate(ground);Object.DestroyImmediate(groundMat);
            EditorApplication.Exit(0);
        }
        catch(Exception e)
        {
            File.WriteAllLines(Folder+"/checks.txt",checks);
            File.WriteAllText(Folder+"/trials.json",JsonUtility.ToJson(new Report{unity=Application.unityVersion,trials=trials.ToArray()},true));
            File.WriteAllText(Folder+"/failed.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);
        }
    }
}
