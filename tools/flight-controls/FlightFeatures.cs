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
using Unity.Cinemachine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class FlightFeatures
{
    const string Folder="TestResults/Flight";const string Flag="FlightFeatureTests";
    static TumblerController car;static Rigidbody body;static WheelCollider[] wheels;static Gamepad pad;static Keyboard keyboard;static TumblerFlightDynamics flight;
    static bool waitingMenu;
    static readonly List<string> checks=new List<string>();static InputSettings oldInput,testInput;
    static FlightFeatures(){EditorApplication.update+=Tick;}
    public static void Run(){File.Delete(Folder+"/feature-failed.txt");File.Delete(Folder+"/features-complete.txt");EditorSceneManager.playModeStartScene=null;EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity");SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();}
    static void Check(bool b,string s){if(!b)throw new Exception(s);checks.Add("PASS "+s);}
    static void Press(Vector2 stick,bool yaw=false,bool rear=false)
    {
        var state=new GamepadState{leftStick=stick};if(yaw)state=state.WithButton(GamepadButton.West);if(rear)state=state.WithButton(GamepadButton.LeftShoulder);
        InputSystem.QueueStateEvent(pad,state);InputSystem.Update();car.SendMessage("RefreshDrivingInput");
    }
    static void Step(int count=1){for(int i=0;i<count;i++){car.SendMessage("FixedUpdate");Physics.Simulate(.01f);}}
    static void Reset(float height=150,Quaternion? rotation=null)
    {
        Press(Vector2.zero);body.position=new Vector3(0,height,0);body.rotation=rotation??Quaternion.identity;body.transform.SetPositionAndRotation(body.position,body.rotation);body.linearVelocity=body.angularVelocity=Vector3.zero;body.constraints=RigidbodyConstraints.None;car.ResetTransmission();flight.Reset();
        foreach(var w in wheels){w.motorTorque=w.brakeTorque=w.steerAngle=w.rotationSpeed=0;}
        Physics.SyncTransforms();
    }
    static Vector3 Rate()=>body.transform.InverseTransformDirection(body.angularVelocity);
    static void Tick()
    {
        if(waitingMenu && EditorApplication.isPlaying)
        {
            waitingMenu=false;
            try { CaptureControls();File.WriteAllLines(Folder+"/feature-checks.txt",checks);File.WriteAllText(Folder+"/features-complete.txt","PASS natural airborne physics, camera and controls UI verification.\n");EditorApplication.Exit(0); }
            catch(Exception e){File.WriteAllLines(Folder+"/feature-failed.txt",checks.Concat(new[]{e.ToString()}));Debug.LogException(e);EditorApplication.Exit(1);}
            return;
        }
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying||Time.frameCount<50)return;SessionState.SetBool(Flag,false);
        try
        {
            Directory.CreateDirectory(Folder);oldInput=InputSystem.settings;testInput=Object.Instantiate(oldInput);InputSystem.settings=testInput;testInput.updateMode=InputSettings.UpdateMode.ProcessEventsManually;testInput.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;testInput.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            pad=InputSystem.AddDevice<Gamepad>();keyboard=InputSystem.AddDevice<Keyboard>();
            car=Object.FindFirstObjectByType<TumblerController>();body=car.GetComponent<Rigidbody>();wheels=body.GetComponentsInChildren<WheelCollider>();
            foreach(var m in body.GetComponentsInChildren<MonoBehaviour>())if(!(m is TumblerImpactPhysics)&&!(m is TumblerChassisFriction))m.enabled=false;
            car.SendMessage("OnEnable");flight=(TumblerFlightDynamics)typeof(TumblerController).GetField("flight",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(car);
            body.interpolation=RigidbodyInterpolation.None;Time.fixedDeltaTime=.01f;Physics.simulationMode=SimulationMode.Script;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(0,99,0);floor.transform.localScale=new Vector3(200,2,200);
            Check(wheels.Length==6,"Original six wheel colliders retained");
            foreach(var w in wheels){var s=w.suspensionSpring;float ratio=s.damper/(2*Mathf.Sqrt(s.spring*w.sprungMass));Check(ratio>=.94f,"Near-critical damping uses actual sprung mass: "+w.name);}
            foreach (var stick in new[]{Vector2.up, Vector2.down, Vector2.left, Vector2.right, Vector2.one})
            {
                Reset(); Press(stick); Step(60);
                Check(car.IsAirborne && body.angularVelocity.magnitude < .01f, "Left stick does not orient the airborne vehicle: " + stick);
            }
            Reset(); Press(Vector2.right, true); Step(60);
            Check(body.angularVelocity.magnitude < .01f, "X plus stick has no aerial yaw control");
            Reset(); InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S)); InputSystem.Update(); car.SendMessage("RefreshDrivingInput"); Step(60);
            Check(body.angularVelocity.magnitude < .01f, "Keyboard has no aerial pitch control"); InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
            Reset(150, Quaternion.Euler(30, 20, 40)); body.angularVelocity = new Vector3(.4f, .6f, .7f); Step(40); Vector3 neutralRate = body.angularVelocity;
            Reset(150, Quaternion.Euler(30, 20, 40)); body.angularVelocity = new Vector3(.4f, .6f, .7f); Press(Vector2.one, true); Step(40);
            Check(neutralRate.magnitude > .8f && Vector3.Distance(body.angularVelocity, neutralRate) < .005f, "Natural aerial rotation persists equally with neutral or held input");
            Reset(101.08f); Step(250); Check(wheels.Count(w=>w.isGrounded)>=4, "Vehicle retains stable wheel support and landing damping");
            floor.SetActive(false);Reset(1.1f);body.position=new Vector3(0,1.1f,40);body.transform.position=body.position;body.isKinematic=true;Physics.SyncTransforms();
            CameraChecks();
            File.WriteAllLines(Folder+"/feature-checks.txt",checks);File.WriteAllText(Folder+"/features-complete.txt","PASS no aerial controls, damping, rear view, restoration and collision checks.\n");
            InputSystem.RemoveDevice(pad);InputSystem.RemoveDevice(keyboard);InputSystem.settings=oldInput!=null?oldInput:Object.Instantiate(testInput);Object.DestroyImmediate(testInput);Physics.simulationMode=SimulationMode.FixedUpdate;waitingMenu=true;UnityEngine.SceneManagement.SceneManager.LoadScene("Main Menu");
        }catch(Exception e){File.WriteAllLines(Folder+"/feature-failed.txt",checks.Concat(new[]{e.ToString()}));Debug.LogException(e);Physics.simulationMode=SimulationMode.FixedUpdate;EditorApplication.Exit(1);}
    }
    static void CameraChecks()
    {
        var mode=Object.FindFirstObjectByType<SandboxCameraModes>();var orbit=Object.FindFirstObjectByType<CinemachineOrbitalFollow>();var collision=orbit.GetComponent<VehicleCameraCollision>();
        var inputs=orbit.GetComponent<CinemachineInputAxisController>();if(inputs!=null)inputs.enabled=false;
        while(mode.SelectedMode!=SandboxCameraModes.ViewMode.Far)mode.CycleMode();
        orbit.HorizontalAxis.Value=26;orbit.VerticalAxis.Value=17.5f;
        orbit.VirtualCamera.InternalUpdateCameraState(Vector3.up,.02f);
        Press(Vector2.zero,false,true);mode.SendMessage("Update");orbit.VirtualCamera.InternalUpdateCameraState(Vector3.up,.02f);
        var state=orbit.VirtualCamera.State;Vector3 delta=state.GetFinalPosition()-body.position;
        Check(mode.IsLookingBack&&mode.SelectedMode==SandboxCameraModes.ViewMode.Far&&mode.EffectiveMode==SandboxCameraModes.ViewMode.Close,"LB temporarily selects close rear view and retains far preset");
        Check(Vector3.Dot(delta,body.transform.forward)>5&&delta.y>2.5f&&delta.y<3.3f,"Rear camera is in front at the close-camera distance and height: "+delta);
        Check(Vector3.Dot(state.GetFinalOrientation()*Vector3.forward,body.transform.forward)<-.85f,"Rear camera looks from the front towards the back");
        Capture("rear-view",state);
        mode.CycleMode();Check(mode.SelectedMode==SandboxCameraModes.ViewMode.Far,"Camera cycling while holding LB cannot overwrite saved preset");
        Press(Vector2.zero);mode.SendMessage("Update");orbit.VirtualCamera.InternalUpdateCameraState(Vector3.up,.02f);
        Check(!mode.IsLookingBack&&mode.EffectiveMode==SandboxCameraModes.ViewMode.Far,"Releasing LB restores the selected camera preset");
        Check(Mathf.Abs(orbit.HorizontalAxis.Value-26)<.1f&&Mathf.Abs(orbit.VerticalAxis.Value-17.5f)<.1f,"Saved camera orbit axes survive rear-view release");
        Capture("chase-restored",orbit.VirtualCamera.State);
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.B));InputSystem.Update();mode.SendMessage("Update");Check(mode.IsLookingBack,"Keyboard B supplies the same held rear view");InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();mode.SendMessage("Update");
        Press(Vector2.zero,false,true);mode.SendMessage("Update");
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Temporary rear-view collision wall";wall.transform.position=body.position+Vector3.forward*3+Vector3.up*2;wall.transform.localScale=new Vector3(10,8,.3f);Physics.SyncTransforms();orbit.VirtualCamera.InternalUpdateCameraState(Vector3.up,.02f);
        Check(orbit.VirtualCamera.State.GetFinalPosition().z<body.position.z+2.7f,"Rear view retains the existing camera collision protection");Object.DestroyImmediate(wall);
        typeof(GameNavigation).GetProperty("IsPaused").GetSetMethod(true).Invoke(null,new object[]{true});mode.SendMessage("Update");Check(!mode.IsLookingBack&&!collision.LookBack,"Pause clears the rear-view override");typeof(GameNavigation).GetProperty("IsPaused").GetSetMethod(true).Invoke(null,new object[]{false});
        mode.SendMessage("Update");mode.enabled=false;Check(!mode.IsLookingBack&&!collision.LookBack,"Disabling camera service clears held rear view");mode.enabled=true;Press(Vector2.zero);mode.SendMessage("Update");
        body.position=new Vector3(100,-10,-150);body.transform.position=body.position;Physics.SyncTransforms();Press(Vector2.zero,false,true);mode.SendMessage("Update");Check(mode.IsInsideTunnel&&mode.EffectiveMode==SandboxCameraModes.ViewMode.Close,"LB works with the original tunnel close-camera override");
        Press(Vector2.zero);mode.SendMessage("Update");Check(mode.IsInsideTunnel&&mode.EffectiveMode==SandboxCameraModes.ViewMode.Close,"Releasing LB inside a tunnel keeps its close-camera override");
        body.position=new Vector3(0,1.1f,40);body.transform.position=body.position;Physics.SyncTransforms();mode.SendMessage("Update");Check(!mode.IsInsideTunnel&&mode.EffectiveMode==SandboxCameraModes.ViewMode.Far,"Surface exit restores the pre-LB preset");
    }
    static void CaptureControls()
    {
        var button=Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(b=>b.name=="Controls");button.onClick.Invoke();
        var overlay=Object.FindFirstObjectByType<ControlsOverlayNavigation>();var camera=Camera.main;var canvas=overlay.GetComponentInParent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.2f;camera.nearClipPlane=.01f;
        foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,720)})
        {
            var rt=new RenderTexture(size.x,size.y,24,RenderTextureFormat.ARGB32);rt.Create();camera.targetTexture=rt;
            Canvas.ForceUpdateCanvases();canvas.GetComponent<UnityEngine.UI.CanvasScaler>().SendMessage("Update");overlay.SendMessage("Fit");Canvas.ForceUpdateCanvases();
            var texts=overlay.GetComponentsInChildren<TMPro.TMP_Text>();
            foreach(var text in texts)
            {
                text.ForceMeshUpdate();var corners=new Vector3[4];text.rectTransform.GetWorldCorners(corners);
                foreach(var corner in corners){var p=RectTransformUtility.WorldToScreenPoint(camera,corner);Check(p.x>=-1&&p.x<=size.x+1&&p.y>=-1&&p.y<=size.y+1,"Control text fits "+size+" "+text.name);}
                Check(text.preferredWidth<=text.rectTransform.rect.width+1 && text.preferredHeight<=text.rectTransform.rect.height+1,"No control label overflow "+size+" "+text.name);
            }
            if(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null)
            {
                for(int i=0;i<12;i++)RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});var prior=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,size.x,size.y),0,0);tex.Apply();File.WriteAllBytes(Folder+"/controls-"+size.y+".png",tex.EncodeToPNG());RenderTexture.active=prior;Object.DestroyImmediate(tex);
            }
            camera.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);
        }
    }

    static void Capture(string name,CameraState state)
    {
        if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)return;
        foreach(var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))c.gameObject.SetActive(false);
        var go=new GameObject("Temporary verified camera");var cam=go.AddComponent<Camera>();cam.enabled=false;cam.fieldOfView=60;cam.farClipPlane=2000;cam.gameObject.AddComponent<HDAdditionalCameraData>();cam.transform.SetPositionAndRotation(state.GetFinalPosition(),state.GetFinalOrientation());
        var rt=new RenderTexture(1440,900,24,RenderTextureFormat.ARGB32);rt.Create();cam.targetTexture=rt;
        for(int i=0;i<12;i++)RenderPipeline.SubmitRenderRequest(cam,new RenderPipeline.StandardRequest{destination=rt});
        var prior=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(1440,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1440,900),0,0);image.Apply();File.WriteAllBytes(Folder+"/"+name+".png",image.EncodeToPNG());RenderTexture.active=prior;cam.targetTexture=null;Object.DestroyImmediate(image);rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(go);
    }
}
