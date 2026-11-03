using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class FacilityVerify
{
    const string Flag="SandboxExpansionVerification";
    const string Folder="TestResults/SandboxExpansion";
    static readonly List<string> checks=new List<string>();
    static int frame;
    static FacilityVerify() { EditorApplication.playModeStateChanged+=State;EditorApplication.update+=Tick; }
    static void Check(bool valid,string label)
    {
        if(!valid)throw new Exception(label);
        checks.Add("PASS "+label);
    }
    public static void Run()
    {
        try
        {
            Directory.CreateDirectory(Folder);File.Delete(Folder+"/failed.txt");File.Delete(Folder+"/play.txt");
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity");
            Check(Application.unityVersion=="6000.6.7f1","Exact repository Editor");
            var root=GameObject.Find("Underground Light Lab and Perimeter");
            Check(root!=null && root.GetComponent<SandboxCameraModes>()!=null,"Saved scene loads the complete facility prefab");
            var terrain=Object.FindFirstObjectByType<Terrain>();
            Check(AssetDatabase.GetAssetPath(terrain.terrainData)==ExpansionAuthoring.Folder+"/TunnelTerrain.asset","Scene terrain and collision use the tunnel-specific clone");
            Check(Object.FindFirstObjectByType<TerrainCollider>().terrainData==terrain.terrainData,"Terrain renderer/collider share the same holes");
            ExpansionAuthoring.CreateRoute();Physics.SyncTransforms();
            var route=ExpansionAuthoring.Route;
            Check(route.First().y==0 && route.Last().y==0 && route.Min(p=>p.y)==-12,"Two surface portals and a 12 m deep underground route");
            Check(route.Any(p=>p.x>130 && p.x<210 && Mathf.Abs(p.z+212)<.1f),"Buried cross passage joins two 90 degree bends");
            for(int i=1;i<route.Count-1;i+=3)
            {
                var p=route[i];var r=ExpansionAuthoring.Right(i);
                foreach(float lane in new[]{-5f,0,5f})
                {
                    Check(Physics.Raycast(p+r*lane+Vector3.up*1.5f,Vector3.down,out var hit,3,~0,QueryTriggerInteraction.Ignore) &&
                        hit.collider.name=="Continuous tunnel road" && Mathf.Abs(hit.point.y-p.y-.04f)<.12f && hit.normal.y>.95f,
                        "Continuous wheel support across route sample "+i+" lane "+lane);
                }
                if(p.y < -11.5f)
                {
                    Check(Physics.Raycast(p+Vector3.up*2,Vector3.up,out var roof,6,~0,QueryTriggerInteraction.Ignore) &&
                        roof.collider.name=="Tunnel walls and closed roof","Closed underground roof at "+i);
                    Check(Physics.Raycast(p+Vector3.up*2,r,out var wall,8,~0,QueryTriggerInteraction.Ignore) &&
                        wall.collider.name=="Tunnel walls and closed roof","Containing tunnel wall at "+i);
                }
                if(p.y < -1 && p.y > -6)
                    Check(Physics.Raycast(new Vector3(p.x,2,p.z),Vector3.down,out var entry,12,~0,QueryTriggerInteraction.Ignore) &&
                        entry.collider.name=="Continuous tunnel road","Terrain opening clears descending/ascending ramp at "+i);
            }
            var fences=root.GetComponentsInChildren<BoxCollider>().Where(c=>c.name.StartsWith("Continuous containment")).ToArray();
            Check(fences.Length==4 && fences.All(c=>!c.isTrigger && c.size.y>=2),"Four uninterrupted physical perimeter barriers");
            foreach(var dir in new[]{Vector3.right,Vector3.left,Vector3.forward,Vector3.back})
                Check(Physics.Raycast(dir*490+Vector3.up,dir,out var fence,12,~0,QueryTriggerInteraction.Ignore) &&
                    fence.collider.name.StartsWith("Continuous containment"),"Barrier blocks the platform edge "+dir);
            var lights=root.GetComponentsInChildren<Light>();
            Check(lights.Length>=12 && lights.All(l=>l.intensity>0 && l.intensity<1500),"Dim functional tunnel fixtures remain below headlight intensity");
            Check(root.GetComponentsInChildren<Volume>().All(v=>!v.isGlobal && v.sharedProfile!=null &&
                v.sharedProfile.TryGet<Exposure>(out var e) && e.fixedExposure.value==8.5f &&
                v.sharedProfile.TryGet<IndirectLightingController>(out var indirect) && indirect.indirectDiffuseLightingMultiplier.value<.1f),
                "Persisted local tunnel volumes contain actual exposure and ambient overrides");
            File.WriteAllLines(Folder+"/edit.txt",checks);checks.Clear();
            SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();
        }
        catch(Exception e){Fail(e);}
    }
    static void State(PlayModeStateChange state)
    {
        if(SessionState.GetBool(Flag,false) && state==PlayModeStateChange.EnteredPlayMode)frame=0;
    }
    static void StepCamera(SandboxCameraModes camera,float deltaTime)
    {
        typeof(SandboxCameraModes).GetMethod("AdvanceTransition",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
            .Invoke(camera,new object[]{deltaTime});
    }
    static void Move(Rigidbody body,Vector3 p)
    {
        body.position=p;body.transform.position=p;body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;
        Physics.SyncTransforms();
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying||Time.frameCount<6||GameNavigation.GameplayInputBlocked)return;
        if(frame++!=0)return;
        try
        {
            var camera=Object.FindFirstObjectByType<SandboxCameraModes>();
            var orbit=Object.FindFirstObjectByType<CinemachineOrbitalFollow>();
            var controller=Object.FindFirstObjectByType<TumblerController>();
            var body=controller.GetComponent<Rigidbody>();
            var home=body.position;
            Vector3 originalInertia=body.inertiaTensor;
            Quaternion originalInertiaRotation=body.inertiaTensorRotation;
            Check(camera.SelectedMode==SandboxCameraModes.ViewMode.Close,"First preset is close");
            Check(Mathf.Abs(orbit.Orbits.Center.Height-2.2f)<.001f,"Close camera looks over the vehicle roof");
            var radii=new List<float>();
            foreach(var expected in new[]{SandboxCameraModes.ViewMode.Close,SandboxCameraModes.ViewMode.Medium,SandboxCameraModes.ViewMode.Far})
            {
                Check(camera.SelectedMode==expected && camera.EffectiveMode==expected,"Cycle selects "+expected);
                StepCamera(camera,2);
                radii.Add(orbit.Orbits.Center.Radius);
                camera.CycleMode();
            }
            Check(radii.Zip(new[]{5.8f,10f,16f},(a,b)=>Mathf.Abs(a-b)<.001f).All(v=>v),"Actual ThreeRing radii are close 5.8 / medium 10 / far 16 m");
            Check(camera.SelectedMode==SandboxCameraModes.ViewMode.Close,"Three modes cycle back to close");
            ExpansionAuthoring.CreateRoute();
            foreach(var selected in new[]{SandboxCameraModes.ViewMode.Close,SandboxCameraModes.ViewMode.Medium,SandboxCameraModes.ViewMode.Far})
            {
                Move(body,home);camera.RefreshTunnelState();
                while(camera.SelectedMode!=selected)camera.CycleMode();
                Move(body,new Vector3(170,1,-212));camera.RefreshTunnelState();
                Check(!camera.IsInsideTunnel && camera.EffectiveMode==selected,"Driving on the surface above the buried tunnel keeps "+selected);
                foreach(var p in ExpansionAuthoring.Route.Where((p,i)=>i%3==0))
                {
                    Move(body,p+Vector3.up);camera.RefreshTunnelState();
                    Check(camera.IsInsideTunnel && camera.EffectiveMode==SandboxCameraModes.ViewMode.Close,"Every portal/ramp/curve segment forces close from "+selected+" at "+p);
                    camera.CycleMode();
                    Check(camera.SelectedMode==selected,"Cycling inside preserves pre-entry selection "+selected);
                }
                Move(body,new Vector3(242,1,-25));camera.RefreshTunnelState();
                Check(!camera.IsInsideTunnel && camera.EffectiveMode==selected,"Surface exit restores "+selected);
                foreach(var p in ExpansionAuthoring.Route.AsEnumerable().Reverse().Where((p,i)=>i%12==0))
                { Move(body,p+Vector3.up);camera.RefreshTunnelState();Check(camera.EffectiveMode==SandboxCameraModes.ViewMode.Close,"Reverse entry stays close"); }
                Move(body,home);camera.RefreshTunnelState();
                Check(!camera.IsInsideTunnel && camera.EffectiveMode==selected,"Teleport/reset restores "+selected);
            }
            Time.timeScale=0;
            var saved=camera.SelectedMode;
            typeof(GameNavigation).GetProperty("IsPaused").SetValue(null,true);
            camera.CycleMode();
            Check(camera.SelectedMode==saved,"Pause blocks camera input");
            typeof(GameNavigation).GetProperty("IsPaused").SetValue(null,false);Time.timeScale=1;
            var cycle=(InputAction)typeof(SandboxCameraModes).GetField("cycle",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(camera);
            Check(cycle.bindings.Any(b=>b.path=="<Keyboard>/c") && cycle.bindings.Any(b=>b.path=="<Gamepad>/dpad/down"),"C and D-pad Down bindings coexist with original gameplay input");
            // Exercise the actual camera action with independent virtual devices.
            var original=InputSystem.settings;var restored=Object.Instantiate(original);var test=Object.Instantiate(original);
            InputSystem.settings=test;test.updateMode=InputSettings.UpdateMode.ProcessEventsManually;
            test.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            test.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            var keyboard=InputSystem.AddDevice<Keyboard>();var pad=InputSystem.AddDevice<Gamepad>();
            saved=camera.SelectedMode;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.C));InputSystem.Update();
            Check(camera.SelectedMode!=saved,"Real keyboard C action cycles the camera");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
            saved=camera.SelectedMode;
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadDown));InputSystem.Update();
            Check(camera.SelectedMode!=saved,"Real virtual Gamepad D-pad Down action cycles the camera");
            InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(pad);
            InputSystem.settings=original!=null?original:restored;if(InputSystem.settings!=restored)Object.DestroyImmediate(restored);Object.DestroyImmediate(test);
            // Actual six-wheel contacts on both ramps and each 90-degree bend.
            foreach(var script in body.GetComponentsInChildren<MonoBehaviour>())script.enabled=false;
            Physics.simulationMode=SimulationMode.Script;body.constraints=RigidbodyConstraints.FreezePositionX|RigidbodyConstraints.FreezePositionZ|RigidbodyConstraints.FreezeRotation;
            foreach(int index in new[]{15,35,80,110,160,200,220})
            {
                Vector3 p=ExpansionAuthoring.Route[index];
                Vector3 direction=ExpansionAuthoring.Route[index+1]-ExpansionAuthoring.Route[index-1];
                body.rotation=Quaternion.LookRotation(direction,Vector3.up);body.transform.rotation=body.rotation;
                Move(body,p+Vector3.up*1.1f);
                foreach(var w in body.GetComponentsInChildren<WheelCollider>()){w.motorTorque=0;w.brakeTorque=0;w.rotationSpeed=0;}
                for(int i=0;i<100;i++)Physics.Simulate(.02f);
                var wheels=body.GetComponentsInChildren<WheelCollider>();
                Check(wheels.All(w=>w.GetGroundHit(out var hit) && hit.collider.name=="Continuous tunnel road"),"All six actual wheels contact the road at ramp/curve sample "+index);
                camera.RefreshTunnelState();
                orbit.VirtualCamera.PreviousStateIsValid=false;orbit.VirtualCamera.UpdateCameraState(Vector3.up,-1);
                Vector3 eye=orbit.VirtualCamera.State.GetFinalPosition();
                Check(!Physics.OverlapSphere(eye,.15f,~0,QueryTriggerInteraction.Ignore).Any(c=>c.name=="Tunnel walls and closed roof"),"Camera stays out of tunnel concrete at "+index);
            }
            Physics.simulationMode=SimulationMode.FixedUpdate;
            DriveWholeTunnel(body,controller,camera,originalInertia,originalInertiaRotation);
            Move(body,home);camera.RefreshTunnelState();
            camera.enabled=false;
            Check(!camera.IsInsideTunnel && Mathf.Abs(orbit.Orbits.Center.Radius-10)<.001f,"Disabling the feature clears override and restores original camera rig");
            camera.enabled=true;
            File.WriteAllLines(Folder+"/play.txt",checks);
            SessionState.SetBool(Flag,false);EditorApplication.Exit(0);
        }
        catch(Exception e){Fail(e);}
    }
    static void DriveWholeTunnel(Rigidbody body,TumblerController controller,SandboxCameraModes camera,Vector3 inertia,Quaternion inertiaRotation)
    {
        var route=ExpansionAuthoring.Route;
        Move(body,new Vector3(100,1,-30));
        body.rotation=Quaternion.LookRotation(Vector3.back);body.transform.rotation=body.rotation;
        body.constraints=RigidbodyConstraints.None;body.interpolation=RigidbodyInterpolation.None;
        body.inertiaTensor=inertia;body.inertiaTensorRotation=inertiaRotation;
        controller.ResetTransmission();
        while(camera.SelectedMode!=SandboxCameraModes.ViewMode.Far)camera.CycleMode();
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
        var type=typeof(TumblerController);
        Action<string,object> set=(n,v)=>type.GetField(n,flags).SetValue(controller,v);
        var steering=type.GetMethod("GetMaxSteeringAngle",flags);
        set("isHandbrakeActive",false);
        Physics.SyncTransforms();Physics.simulationMode=SimulationMode.Script;
        int cursor=0,step=0;bool entered=false,exited=false;float maxDeviation=0;
        for(;step<10000;step++)
        {
            float best=float.MaxValue;
            for(int k=cursor;k<Mathf.Min(route.Count,cursor+18);k++)
            {
                var d=Vector3.ProjectOnPlane(body.position-route[k],Vector3.up);
                if(d.sqrMagnitude<best){best=d.sqrMagnitude;cursor=k;}
            }
            maxDeviation=Mathf.Max(maxDeviation,Mathf.Sqrt(best));
            Vector3 target=cursor>=route.Count-5?route.Last()+Vector3.forward*18:route[Mathf.Min(route.Count-1,cursor+4)];
            Vector3 local=body.transform.InverseTransformDirection(Vector3.ProjectOnPlane(target-body.position,Vector3.up));
            float look=Mathf.Max(4,new Vector2(local.x,local.z).magnitude);
            float angle=Mathf.Atan2(2*4.513f*local.x,look*look)*Mathf.Rad2Deg;
            float speed=Vector3.ProjectOnPlane(body.linearVelocity,Vector3.up).magnitude;
            float allowed=(float)steering.Invoke(controller,new object[]{speed});
            set("steerInput",Mathf.Clamp(angle/allowed,-1,1));
            set("currentPower",1400f*Mathf.Clamp((5-speed)*.45f,0,.75f));
            set("currentBrakeForce",Mathf.Clamp((speed-5)*500,0,800));
            controller.SendMessage("FixedUpdate");Physics.Simulate(.02f);
            camera.RefreshTunnelState();StepCamera(camera,.02f);
            if(camera.IsInsideTunnel)
            {
                entered=true;
                if(camera.EffectiveMode!=SandboxCameraModes.ViewMode.Close)throw new Exception("Driven tunnel camera did not stay close");
            }
            if(cursor>route.Count-6 && body.position.z>-28)
            {
                exited=!camera.IsInsideTunnel;break;
            }
            if(body.position.y < -16 || body.transform.up.y < .6f)
                throw new Exception("Vehicle lost tunnel roadway at sample "+cursor+" position="+body.position);
        }
        File.WriteAllText(Folder+"/drive.txt","steps="+step+" progress="+cursor+"/"+route.Count+
            " constraints="+body.constraints+" inertia="+body.inertiaTensor+" position="+body.position+" heading="+body.rotation.eulerAngles+" speed="+body.linearVelocity+" maxDistanceToRouteSample="+maxDeviation+" entered="+entered+" exited="+exited);
        Check(entered && exited && step<10000,"Actual Tumbler drives both ramps and both 90-degree curves back to the surface");
        Check(camera.EffectiveMode==SandboxCameraModes.ViewMode.Far,"Continuous physical tunnel traversal restores the original far camera");
        Physics.simulationMode=SimulationMode.FixedUpdate;
    }

    static void Fail(Exception e)
    {
        SessionState.SetBool(Flag,false);Physics.simulationMode=SimulationMode.FixedUpdate;
        Directory.CreateDirectory(Folder);File.WriteAllLines(Folder+"/failed.txt",checks.Concat(new[]{e.ToString()}));
        Debug.LogException(e);EditorApplication.Exit(1);
    }
}
