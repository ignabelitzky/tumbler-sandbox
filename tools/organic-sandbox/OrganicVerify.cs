using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Unity.Cinemachine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class OrganicVerify
{
    const string Flag="OrganicVerify";
    const string Out="TestResults/OrganicSandbox";
    static readonly List<string> checks=new List<string>();
    static int frame;
    static Vector3 originalInertia;static Quaternion originalInertiaRotation;
    static OrganicSandbox.Route[] routes;
    static OrganicVerify(){EditorApplication.update+=Tick;}
    static void Check(bool b,string s){if(!b)throw new Exception(s);checks.Add("PASS "+s);}
    public static void Run()
    {
        try
        {
            Directory.CreateDirectory(Out);checks.Clear();foreach(var p in Directory.GetFiles(Out,"inverted-*.png"))File.Delete(p);
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity");
            routes=JsonUtility.FromJson<OrganicSandbox.RouteFile>(File.ReadAllText(OrganicSandbox.Folder+"/Routes.json")).routes;
            var root=GameObject.Find("Organic proving grounds");Check(root!=null,"Organic prefab loaded");
            CaptureViews();
            Check(GameObject.Find("Driving Test Ground")==null,"Old boards and isolated test display inactive");
            Check(Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None).Length==1,"One active sculpted terrain");
            var terrain=Object.FindFirstObjectByType<Terrain>();Check(terrain.terrainData.terrainLayers.Length==4,"Four blended terrain surfaces");
            Check(root.GetComponentsInChildren<Transform>().All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0),"No missing scripts");
            Check(root.GetComponentsInChildren<MeshCollider>().All(c=>c.sharedMesh!=null && c.sharedMesh.vertexCount>0),"All solid courses have valid collision meshes");
            Physics.SyncTransforms();int sampled=0;
            foreach(var r in routes)for(int i=3;i<r.points.Length-3;i+=13)
            {
                var p=r.points[i];var up=r.up[i];
                Check(Physics.Raycast(p+up*2.5f,-up,out RaycastHit hit,3.5f,~0,QueryTriggerInteraction.Ignore),"Road collision "+r.name+" #"+i);
                Check(Vector3.Distance(hit.point,p)<.32f,"No collider gap or terrain intrusion "+r.name+" #"+i+" hit="+hit.collider.name+" offset="+Vector3.Distance(hit.point,p));
                Check(Vector3.Dot(hit.normal,up)>.75f,"Driveable normal "+r.name+" #"+i);sampled++;
            }
            // Real solid catch surfaces at the top, bottom and both straights of the oval.
            for(int i=0;i<800;i+=31)
            {
                Vector3 p=OrganicSandbox.OvalPoint(i,14);
                Check(Physics.Raycast(p+Vector3.up*2,Vector3.down,out var hit,3,~0,QueryTriggerInteraction.Ignore) && hit.collider.name=="Progressively banked oval","Banked oval running surface #"+i);
            }
            Check(GameObject.Find("Continuous tunnel road")!=null,"Original tunnel structure retained");
            File.WriteAllLines(Out+"/edit-checks.txt",checks);checks.Clear();
            SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();
        }catch(Exception e){Fail(e);}
    }
    public static void VerifyOriginalTunnel()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity");
        SessionState.SetBool("OrganicOriginalTunnelOnly",true);SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying||Time.frameCount<80||Time.frameCount==frame)return;
        frame=Time.frameCount;SessionState.SetBool(Flag,false);
        try
        {
            if(SessionState.GetBool("OrganicOriginalTunnelOnly",false))
            {
                SessionState.SetBool("OrganicOriginalTunnelOnly",false);
                var original=JsonUtility.FromJson<OrganicSandbox.Route>(File.ReadAllText("Assets/_Project/Environment/UndergroundFacility/Route.json"));
                original.name="Original tunnel";original.width=14;original.up=Enumerable.Repeat(Vector3.up,original.points.Length).ToArray();
                var car=Object.FindFirstObjectByType<TumblerController>();var rb=car.GetComponent<Rigidbody>();originalInertia=rb.inertiaTensor;originalInertiaRotation=rb.inertiaTensorRotation;
                foreach(var component in rb.GetComponentsInChildren<MonoBehaviour>())component.enabled=false;
                rb.interpolation=RigidbodyInterpolation.None;Physics.simulationMode=SimulationMode.Script;Time.fixedDeltaTime=.01f;
                Drive(rb,car,original,4.5f,"original-tunnel",true);Check(!checks.Any(s=>s.StartsWith("FAIL ")),"Original tunnel remains fully driveable");
                Physics.simulationMode=SimulationMode.FixedUpdate;File.WriteAllLines(Out+"/original-tunnel-checks.txt",checks);EditorApplication.Exit(0);return;
            }
            routes=JsonUtility.FromJson<OrganicSandbox.RouteFile>(File.ReadAllText(OrganicSandbox.Folder+"/Routes.json")).routes;
            CaptureViews();
            var controller=Object.FindFirstObjectByType<TumblerController>();var body=controller.GetComponent<Rigidbody>();var wheels=body.GetComponentsInChildren<WheelCollider>();
            Check(wheels.Length==6,"Original six wheel vehicle");originalInertia=body.inertiaTensor;originalInertiaRotation=body.inertiaTensorRotation;
            var camera=Object.FindFirstObjectByType<SandboxCameraModes>();
            var tunnel=routes.Single(r=>r.name=="Long tunnel continuous road");
            foreach(int i in new[]{20,100,220,390,560,680,750})
            {body.position=tunnel.points[i]+Vector3.up;Physics.SyncTransforms();camera.RefreshTunnelState();Check(camera.IsInsideTunnel,"New tunnel selects close camera #"+i);}
            body.position=new Vector3(0,1,40);Physics.SyncTransforms();camera.RefreshTunnelState();Check(!camera.IsInsideTunnel,"Surface/reset restores selected camera");
            var newZones=Object.FindFirstObjectByType<SandboxTunnelZones>();newZones.enabled=false;body.position=tunnel.points[390]+Vector3.up;Physics.SyncTransforms();camera.RefreshTunnelState();Check(!camera.IsInsideTunnel,"Disabled new tunnel cannot leave stale camera override");newZones.enabled=true;
            // Validate visual terrain types feed the same existing per-wheel stiffness service.
            var grip=Object.FindFirstObjectByType<WheelSurfaceGrip>();Check(grip!=null&&grip.isActiveAndEnabled,"Surface grip service active on rebuilt course");
            Check(typeof(WheelSurfaceGrip).GetField("vehicleRoot",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(grip)!=null,"Grip service bound to actual Tumbler");
            var terrainSurface=Object.FindFirstObjectByType<TerrainDrivingSurface>();
            foreach(var p in new[]{new Vector3(-390,0,130),new Vector3(-130,0,340),new Vector3(410,0,-200)})
            {Vector2 g=terrainSurface.GripAt(p);Check(g.x>=.5f&&g.x<=1&&g.y>=.4f&&g.y<=1,"Blended terrain grip bounded at "+p);}
            foreach(var s in body.GetComponentsInChildren<MonoBehaviour>())s.enabled=false;
            foreach(var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))canvas.gameObject.SetActive(false);
            controller.SendMessage("OnEnable"); // Include the current runtime suspension and flight service.
            Physics.simulationMode=SimulationMode.Script;body.interpolation=RigidbodyInterpolation.None;
            Vector3 gravity=Physics.gravity;float dt=Time.fixedDeltaTime;Time.fixedDeltaTime=.01f;
            try
            {
                SettleContacts(body,wheels,tunnel,new[]{40,170,330,480,650,740});
                foreach(var name in new[]{"Low flowing tabletop","High flowing tabletop","Gravel suspension run","Ridge dirt climb"})
                    SettleContacts(body,wheels,routes.Single(r=>r.name==name),new[]{20,65,100});
                Physics.gravity=gravity;
                Drive(body,controller,tunnel,4.5f,"new-tunnel",true);
                var oval = new OrganicSandbox.Route{name="Oval physical lap",width=28,points=new Vector3[801],up=new Vector3[801]};
                for(int i=0;i<=800;i++)
                {
                    oval.points[i]=OrganicSandbox.OvalPoint(i,14);
                    Vector3 f=OrganicSandbox.OvalPoint(Mathf.Min(i+1,800),14)-OrganicSandbox.OvalPoint(Mathf.Max(0,i-1),14);
                    oval.up[i]=Vector3.Cross(f,OrganicSandbox.OvalPoint(i,15)-OrganicSandbox.OvalPoint(i,13)).normalized;
                }
                Drive(body,controller,oval,20f,"banked-oval",true);
                Drive(body,controller,routes.Single(r=>r.name=="Oval entry"),6f,"oval-access",true);
                // Each loop test uses real wheel/suspension/body dynamics and steering; it never snaps or drives a kinematic vehicle around a curve.
                foreach(string name in new[]{"Loop 18m","Loop 26m"})
                {
                    var loop=routes.Single(r=>r.name==name);Drive(body,controller,loop,55f,name.Replace(' ','-'),true);
                    Drive(body,controller,loop,40f,name.Replace(' ','-')+"-144kph",false);
                    Drive(body,controller,loop,65f,name.Replace(' ','-')+"-234kph",false);
                }
            }
            finally{Physics.gravity=gravity;Time.fixedDeltaTime=dt;Physics.simulationMode=SimulationMode.FixedUpdate;}
            body.isKinematic=true;body.position=new Vector3(0,80,0);
            Physics.simulationMode=SimulationMode.Script;
            for(int index=0;index<800;index+=170)
            {
                Vector3 edge=OrganicSandbox.OvalPoint(index,28);if(edge.x<250 && Mathf.Abs(edge.z-245)<25)continue;
                Vector3 outward=Vector3.ProjectOnPlane(OrganicSandbox.OvalPoint(index,28)-OrganicSandbox.OvalPoint(index,27),Vector3.up).normalized;
                var probe=GameObject.CreatePrimitive(PrimitiveType.Sphere);probe.name="Temporary catch collision probe";probe.transform.position=edge-outward*2+Vector3.up*2;probe.transform.localScale=Vector3.one*1.2f;
                var rb=probe.AddComponent<Rigidbody>();rb.mass=1000;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;rb.linearVelocity=outward*15;
                Physics.SyncTransforms();for(int k=0;k<70;k++)Physics.Simulate(.01f);
                Check(Vector3.Dot(rb.position-edge,outward)<1,"Actual dynamic collision contained by oval catch panel #"+index);Object.DestroyImmediate(probe);
            }
            Physics.simulationMode=SimulationMode.FixedUpdate;
            File.WriteAllLines(Out+"/play-checks.txt",checks);Check(!checks.Any(s=>s.StartsWith("FAIL ")),"All required physical traversals completed");File.WriteAllText(Out+"/complete.txt","PASS geometry, camera, terrain grip, contacts, native drive and visual capture.\n");
            EditorApplication.Exit(0);
        }catch(Exception e){Fail(e);}
    }
    static void Reset(Rigidbody body,Vector3 p,Quaternion q)
    {
        body.constraints=RigidbodyConstraints.None;body.position=p;body.rotation=q;body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;
        body.transform.SetPositionAndRotation(p,q);
        foreach(var w in body.GetComponentsInChildren<WheelCollider>()){w.motorTorque=0;w.brakeTorque=0;w.steerAngle=0;w.rotationSpeed=0;}
        Physics.SyncTransforms();
    }
    static void SettleContacts(Rigidbody body,WheelCollider[] wheels,OrganicSandbox.Route route,int[] samples)
    {
        foreach(int i0 in samples)
        {
            int i=Mathf.Min(i0,route.points.Length-3);var f=route.Forward(i);var up=Vector3.Cross(f,route.Right(i)).normalized;
            Reset(body,route.points[i]+up*1.15f,Quaternion.LookRotation(f,up));body.constraints=RigidbodyConstraints.FreezeRotation;
            Physics.gravity=-up*9.81f;
            for(int k=0;k<160;k++)Physics.Simulate(.01f);
            Check(wheels.Count(w=>w.GetGroundHit(out var h)&&h.collider!=null)>=4,"Actual wheel contacts on "+route.name+" #"+i);
        }
    }
    static void Drive(Rigidbody body,TumblerController controller,OrganicSandbox.Route route,float speed,string label,bool required)
    {
        var points=route.points.ToList();var up=route.up.ToList();
        bool loop=label.StartsWith("Loop");
        if(loop)
        {
            Vector3 start=points[0];for(int k=1;k<=30;k++){points.Insert(0,start-Vector3.forward*k);up.Insert(0,Vector3.up);}
            Vector3 end=points.Last();for(int k=1;k<=40;k++){points.Add(end+Vector3.forward*k);up.Add(Vector3.up);}
        }
        if(!loop)
        {
            Vector3 end=points.Last();Vector3 forward=(end-points[points.Count-2]).normalized;Vector3 normal=up.Last();
            for(int k=1;k<=20;k++){points.Add(end+forward*k);up.Add(normal);}
        }
        Reset(body,points[0]+up[0]*1.05f,Quaternion.LookRotation(points[1]-points[0],up[0]));
        body.constraints=RigidbodyConstraints.FreezePositionX|RigidbodyConstraints.FreezePositionZ|RigidbodyConstraints.FreezeRotation;
        for(int k=0;k<160;k++)Physics.Simulate(.01f);body.constraints=RigidbodyConstraints.None;
        body.inertiaTensor=originalInertia;body.inertiaTensorRotation=originalInertiaRotation;body.linearVelocity=body.transform.forward*speed;controller.ResetTransmission();
        var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;var type=typeof(TumblerController);
        Action<string,object> set=(n,v)=>type.GetField(n,flags).SetValue(controller,v);
        var steering=type.GetMethod("GetMaxSteeringAngle",flags);set("currentTurnAngle",0f);int cursor=0,steps=0;float worst=0,maxHeight=body.position.y;int minWheels=6;bool passed=false,inverted=false;
        var trace=new List<string>{"time,sample,speed_kph,x,y,z,wheels,up_y"};
        for(;steps<(loop?3500:30000);steps++)
        {
            float best=float.MaxValue;
            for(int k=Mathf.Max(0,cursor-2);k<Mathf.Min(points.Count,cursor+(loop?40:22));k++)
            {float d=(body.position-(points[k]+up[k]*.65f)).sqrMagnitude;if(d<best){best=d;cursor=k;}}
            worst=Mathf.Max(worst,Mathf.Sqrt(best));
            int target=cursor;float ahead=loop?9:11;
            while(target<points.Count-1 && Vector3.Distance(points[target],points[cursor])<ahead)target++;
            Vector3 local=body.transform.InverseTransformDirection(points[target]-body.position);
            float look=Mathf.Max(3,new Vector2(local.x,local.z).magnitude);float angle=Mathf.Atan2(2*4.513f*local.x,look*look)*Mathf.Rad2Deg;
            float velocity=body.linearVelocity.magnitude;float allowed=(float)steering.Invoke(controller,new object[]{Vector3.ProjectOnPlane(body.linearVelocity,Vector3.up).magnitude});
            set("steerInput",Mathf.Clamp(angle/allowed,-1,1));set("currentPower",1400f*Mathf.Clamp((speed-velocity)*.5f,0,1));set("currentBrakeForce",loop?0f:Mathf.Clamp((velocity-speed)*700,0,1000));set("isHandbrakeActive",false);
            controller.SendMessage("FixedUpdate");
            Object.FindFirstObjectByType<WheelSurfaceGrip>().SendMessage("FixedUpdate");Physics.Simulate(.01f);
            maxHeight=Mathf.Max(maxHeight,body.position.y);if(body.transform.up.y<-.8f)inverted=true;
            int contact=body.GetComponentsInChildren<WheelCollider>().Count(w=>w.isGrounded);minWheels=Mathf.Min(minWheels,contact);
            if(loop && speed==55f && cursor>=235 && cursor<270 && !File.Exists(Out+"/inverted-"+label+".png"))
            {
                var cam=new GameObject("Temporary loop proof camera").AddComponent<Camera>();cam.enabled=false;cam.fieldOfView=45;cam.farClipPlane=1800;cam.gameObject.AddComponent<HDAdditionalCameraData>();
                Capture(cam,"inverted-"+label,body.position+new Vector3(22,-12,-5),body.position);Object.DestroyImmediate(cam.gameObject);
            }
            if(steps%20==0)trace.Add($"{steps*.01f:F2},{cursor},{velocity*3.6f:F1},{body.position.x:F2},{body.position.y:F2},{body.position.z:F2},{contact},{body.transform.up.y:F3}");
            if(cursor>=points.Count-8){passed=!loop||inverted;break;}
            if(body.position.y < -18 || Mathf.Sqrt(best)>16 || (!loop&&body.transform.up.y<.3f))break;
        }
        File.WriteAllLines(Out+"/drive-"+label+".csv",trace);
        File.WriteAllText(Out+"/drive-"+label+".txt",$"passed={passed} entry_kph={speed*3.6f:F0} progress={cursor}/{points.Count} simulated_seconds={steps*.01f:F2} max_distance={worst:F2} min_grounded_wheels={minWheels} inverted={inverted} peak_height={maxHeight:F2} end={body.position}\n");
        if(required)checks.Add((passed?"PASS ":"FAIL ")+"Real six-wheel traversal "+label+" completed "+route.Length.ToString("F0")+"m");
        else checks.Add((passed?"PASS ":"OBSERVATION ")+label+" physical test at "+(speed*3.6f)+" km/h: "+(passed?"completed":"did not complete; see drive trace"));
    }
    static void CaptureViews()
    {
        var previousCameras=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);var previousCanvas=Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        var cameraEnabled=previousCameras.Select(c=>c.enabled).ToArray();
        foreach(var c in previousCameras)c.enabled=false;
        foreach(var c in previousCanvas)c.gameObject.SetActive(false);
        var cam=new GameObject("Temporary organic inspection camera").AddComponent<Camera>();cam.enabled=false;cam.fieldOfView=60;cam.farClipPlane=2200;cam.nearClipPlane=.12f;
        cam.gameObject.AddComponent<HDAdditionalCameraData>().clearColorMode=HDAdditionalCameraData.ClearColorMode.Sky;
        Capture(cam,"overview",new Vector3(-560,360,-580),new Vector3(0,0,30));
        Capture(cam,"loops",new Vector3(-125,42,-405),new Vector3(0,20,-285));
        Capture(cam,"oval",new Vector3(100,88,100),new Vector3(270,7,260));
        Capture(cam,"terrain",new Vector3(-405,48,225),new Vector3(-130,10,320));
        Capture(cam,"new-tunnel",new Vector3(-230,5,150),new Vector3(-210,-3,75));
        Capture(cam,"original-tunnel",new Vector3(83,8,-16),new Vector3(100,-4,-85));
        Object.DestroyImmediate(cam.gameObject);
        for(int i=0;i<previousCameras.Length;i++)previousCameras[i].enabled=cameraEnabled[i];foreach(var c in previousCanvas)c.gameObject.SetActive(true);
    }
    static void Capture(Camera cam,string name,Vector3 eye,Vector3 target)
    {
        cam.transform.position=eye;cam.transform.LookAt(target);var rt=new RenderTexture(1440,900,24,RenderTextureFormat.ARGB32);rt.Create();cam.targetTexture=rt;
        for(int i=0;i<16;i++)RenderPipeline.SubmitRenderRequest(cam,new RenderPipeline.StandardRequest{destination=rt});
        var prior=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(1440,900,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1440,900),0,0);tex.Apply();File.WriteAllBytes(Out+"/"+name+".png",tex.EncodeToPNG());RenderTexture.active=prior;cam.targetTexture=null;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);
    }
    static void Fail(Exception e){SessionState.SetBool(Flag,false);Physics.simulationMode=SimulationMode.FixedUpdate;Directory.CreateDirectory(Out);File.WriteAllLines(Out+"/failed.txt",checks.Concat(new[]{e.ToString()}));Debug.LogException(e);EditorApplication.Exit(1);}
}
