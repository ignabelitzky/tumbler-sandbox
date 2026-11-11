using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Cinemachine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class ClassicVerify
{
    const string Flag="ClassicVerify";
    static readonly List<string> checks=new List<string>();
    static Vector3 originalInertia;
    static Quaternion originalInertiaRotation;
    static readonly BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static ClassicVerify(){EditorApplication.update+=Tick;}
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks.Add("PASS "+name);}
    static OrganicSandbox.Route[] Routes()=>JsonUtility.FromJson<OrganicSandbox.RouteFile>(File.ReadAllText(ClassicSandbox.Folder+"/Routes.json")).routes.Where(r=>GameObject.Find(r.name)!=null).ToArray();
    public static void Run()
    {
        try
        {
            checks.Clear();Directory.CreateDirectory(ClassicSandbox.Results);File.Delete(ClassicSandbox.Results+"/failed.txt");
            EditorSceneManager.playModeStartScene=null;EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity");Physics.SyncTransforms();
            var root=GameObject.Find("Classic flowing proving grounds");Check(root!=null,"Classic ground attached");
            Check(GameObject.Find("Organic proving grounds")==null,"Previous stunt-map instance replaced");
            Check(Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None).Length==1,"Exactly one active terrain");
            Check(GameObject.Find("Continuous tunnel road")!=null,"Original tunnel retained");
            Check(Object.FindObjectsByType<SandboxTunnelZones>(FindObjectsSortMode.None).Length==0,"No second tunnel zone group");
            Check(!Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Any(t=>t.name.Contains("Loop 18m")||t.name.Contains("Loop 26m")||t.name.Contains("banked oval")||t.name.Contains("Long four bend")),"No active deathloops, caged oval or new winding tunnel");
            Check(root.GetComponentsInChildren<Transform>().All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0),"No missing scripts in the map");
            Check(root.GetComponentsInChildren<MeshCollider>().All(c=>c.sharedMesh!=null),"Every solid map mesh has collision data");
            foreach(string name in new[]{"curved_ramp","square_ramp"})Check(GameObject.Find("Original "+name)!=null,"Retained original ramp: "+name);
            foreach(string name in new[]{"Original ramp_45","Original circular_bump","Original triangular_bump","Open handling circuit","Low sandstone outcrops","Flush bump markings","Medium rolling tabletop"})
                Check(GameObject.Find(name)==null,"Removed playground clutter: "+name);
            foreach(string name in new[]{"West perimeter","East perimeter","South perimeter","North perimeter"})
                Check(GameObject.Find(name)?.GetComponent<BoxCollider>()?.enabled==true,"Solid visible boundary: "+name);
            foreach(var route in Routes())for(int i=5;i<route.points.Length-5;i+=19)
            {
                var point=route.points[i];Check(Physics.Raycast(point+Vector3.up*2.5f,Vector3.down,out var hit,3.2f,~0,QueryTriggerInteraction.Ignore),"Solid road "+route.name+" #"+i);
                Check(Mathf.Abs(hit.point.y-point.y)<.5f,"No terrain intrusion or unexpected crossing "+route.name+" #"+i+" hit="+hit.collider.name+" delta="+(hit.point.y-point.y));
                Check(hit.normal.y>.75f,"Driveable normal "+route.name+" #"+i);
            }
            File.WriteAllLines(ClassicSandbox.Results+"/edit-checks.txt",checks);checks.Clear();SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();
        }
        catch(Exception e){Fail(e);}
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying||Time.frameCount<60)return;SessionState.SetBool(Flag,false);
        try
        {
            var controller=Object.FindFirstObjectByType<TumblerController>();var body=controller.GetComponent<Rigidbody>();var wheels=body.GetComponentsInChildren<WheelCollider>();
            originalInertia=body.inertiaTensor;originalInertiaRotation=body.inertiaTensorRotation;
            Check(wheels.Length==6,"Six original wheels");var camera=Object.FindFirstObjectByType<SandboxCameraModes>();
            body.position=new Vector3(100,-10,-150);body.transform.position=body.position;Physics.SyncTransforms();camera.RefreshTunnelState();Check(camera.IsInsideTunnel,"Original tunnel selects close camera");
            body.position=new Vector3(-210,1,0);body.transform.position=body.position;Physics.SyncTransforms();camera.RefreshTunnelState();Check(!camera.IsInsideTunnel,"Former second tunnel is now open terrain");
            var grip=Object.FindFirstObjectByType<WheelSurfaceGrip>();Check(grip!=null&&typeof(WheelSurfaceGrip).GetField("vehicleRoot",Fields).GetValue(grip)!=null,"Surface grip bound to real vehicle");
            var terrain=Object.FindFirstObjectByType<TerrainDrivingSurface>();foreach(var point in new[]{new Vector3(-340,0,200),new Vector3(-220,0,-270),new Vector3(50,0,100)})
            {var value=terrain.GripAt(point);Check(value.x>=.5f&&value.x<=1&&value.y>=.4f&&value.y<=1,"Terrain grip matches painted layers");}
            foreach(var component in body.GetComponentsInChildren<MonoBehaviour>())component.enabled=false;
            controller.SendMessage("OnEnable");body.interpolation=RigidbodyInterpolation.None;Physics.simulationMode=SimulationMode.Script;Time.fixedDeltaTime=.02f;
            foreach(var test in new[]{("Progressive speed bumps",5.5f),("Alternating wheel articulation",4.5f),("Rolling gravel and shallow hollows",6f),("Low rolling tabletop",10f)})Drive(body,controller,grip,Routes().Single(r=>r.name==test.Item1),test.Item2);
            var original=JsonUtility.FromJson<OrganicSandbox.Route>(File.ReadAllText("Assets/_Project/Environment/UndergroundFacility/Route.json"));original.name="Original tunnel";original.width=14;var fullRoute=original.points.ToList();var exit=fullRoute.Last();for(int i=1;i<=24;i++)fullRoute.Add(exit+Vector3.forward*i);original.points=fullRoute.ToArray();original.up=Enumerable.Repeat(Vector3.up,original.points.Length).ToArray();Drive(body,controller,grip,original,4.5f);Check(body.position.z> -27,"Original tunnel traversal clears the complete exit portal");
            Physics.simulationMode=SimulationMode.FixedUpdate;File.WriteAllLines(ClassicSandbox.Results+"/play-checks.txt",checks);File.WriteAllText(ClassicSandbox.Results+"/complete.txt","PASS classic layout, original tunnel, terrain grip and real wheel-driven traversals.\n");EditorApplication.Exit(0);
        }
        catch(Exception e){Fail(e);}
    }
    static void Set(TumblerController car,string name,object value)=>typeof(TumblerController).GetField(name,Fields).SetValue(car,value);
    static void Drive(Rigidbody body,TumblerController car,WheelSurfaceGrip grip,OrganicSandbox.Route route,float speed)
    {
        var points=route.points;body.constraints=RigidbodyConstraints.None;body.position=points[0]+Vector3.up*1.1f;body.rotation=Quaternion.LookRotation(points[1]-points[0],Vector3.up);body.transform.SetPositionAndRotation(body.position,body.rotation);body.linearVelocity=body.angularVelocity=Vector3.zero;
        foreach(var w in body.GetComponentsInChildren<WheelCollider>()){w.motorTorque=w.brakeTorque=w.steerAngle=w.rotationSpeed=0;}car.ResetTransmission();Physics.SyncTransforms();
        body.constraints=RigidbodyConstraints.FreezeRotation|RigidbodyConstraints.FreezePositionX|RigidbodyConstraints.FreezePositionZ;for(int i=0;i<100;i++)Physics.Simulate(.02f);body.constraints=RigidbodyConstraints.None;body.inertiaTensor=originalInertia;body.inertiaTensorRotation=originalInertiaRotation;body.linearVelocity=body.transform.forward*speed;
        Set(car,"currentTurnAngle",0f);int cursor=0;float maxDistance=0;int minContacts=6;bool passed=false;var trace=new List<string>{"time,sample,kph,x,y,z,wheels,up_y,yaw,steer"};
        var steering=typeof(TumblerController).GetMethod("GetMaxSteeringAngle",Fields);
        for(int step=0;step<18000;step++)
        {
            float best=float.MaxValue;for(int k=Mathf.Max(0,cursor-3);k<Mathf.Min(points.Length,cursor+30);k++){float distance=(body.position-(points[k]+Vector3.up*.7f)).sqrMagnitude;if(distance<best){best=distance;cursor=k;}}
            maxDistance=Mathf.Max(maxDistance,Mathf.Sqrt(best));int target=cursor;while(target<points.Length-1&&Vector3.Distance(points[target],points[cursor])<10)target++;
            Vector3 local=body.transform.InverseTransformDirection(points[target]-body.position);float look=Mathf.Max(3,new Vector2(local.x,local.z).magnitude);float angle=Mathf.Atan2(2*4.513f*local.x,look*look)*Mathf.Rad2Deg;
            float velocity=body.linearVelocity.magnitude;float allowed=(float)steering.Invoke(car,new object[]{Vector3.ProjectOnPlane(body.linearVelocity,Vector3.up).magnitude});
            Set(car,"steerInput",Mathf.Clamp(angle/allowed,-1,1));Set(car,"currentPower",1400f*Mathf.Clamp((speed-velocity)*.5f,0,1));Set(car,"currentBrakeForce",Mathf.Clamp((velocity-speed)*700,0,1000));Set(car,"isHandbrakeActive",false);
            car.SendMessage("FixedUpdate");grip.SendMessage("FixedUpdate");body.GetComponent<TumblerChassisFriction>()?.SendMessage("FixedUpdate");Physics.Simulate(.02f);
            int contacts=body.GetComponentsInChildren<WheelCollider>().Count(w=>w.isGrounded);minContacts=Mathf.Min(minContacts,contacts);
            if(step%10==0)trace.Add($"{step*.02f:F2},{cursor},{velocity*3.6f:F2},{body.position.x:F3},{body.position.y:F3},{body.position.z:F3},{contacts},{body.transform.up.y:F3},{body.rotation.eulerAngles.y:F3},{body.GetComponentsInChildren<WheelCollider>()[0].steerAngle:F3}");
            if(cursor>=points.Length-8){passed=true;break;}
            if(body.position.y< -20||Mathf.Sqrt(best)>15||body.transform.up.y<.3f)break;
        }
        string label=route.name.Replace(' ','-');File.WriteAllLines(ClassicSandbox.Results+"/drive-"+label+".csv",trace);
        File.WriteAllText(ClassicSandbox.Results+"/drive-"+label+".txt",$"passed={passed} progress={cursor}/{points.Length} requested_kph={speed*3.6f:F1} max_distance={maxDistance:F2} min_grounded_wheels={minContacts} end={body.position}\n");
        Check(passed,"Real six-wheel traversal: "+route.name);
    }
    static void Fail(Exception e){SessionState.SetBool(Flag,false);Physics.simulationMode=SimulationMode.FixedUpdate;Directory.CreateDirectory(ClassicSandbox.Results);File.WriteAllLines(ClassicSandbox.Results+"/failed.txt",checks.Concat(new[]{e.ToString()}));Debug.LogException(e);EditorApplication.Exit(1);}
}
