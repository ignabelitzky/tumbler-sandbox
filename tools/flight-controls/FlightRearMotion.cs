using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Unity.Cinemachine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// Observe the real player loop after the Brain positions the output camera.
[InitializeOnLoad]
public static class FlightRearMotion
{
    const string Folder = "TestResults/Flight";
    const string Flag = "RearMotionVerification";
    [Serializable] public class Sample
    {
        public int fps, frames;
        public float initialKph, maxKph, maxFollowError, maxAimErrorDegrees, minFrontDistance = 100,
            maxViewportOffset, maxPhysicsRenderSeparation;
    }
    [Serializable] public class Report { public string unity, mode; public Sample[] samples; }
    static readonly int[] Rates = { 30, 60, 120 };
    static readonly float[] Speeds = { 0, 55, 72.2f };
    static readonly List<Sample> samples = new List<Sample>();
    static readonly List<string> trace = new List<string>();
    static TumblerController car;
    static Rigidbody body;
    static Gamepad pad;
    static InputSettings originalInput, testInput;
    static SandboxCameraModes modes;
    static int scenario, startFrame, lastFrame;
    static bool running;
    static string mode;
    static FlightRearMotion() { EditorApplication.update += Tick; }
    public static void Baseline() { Run("before"); }
    public static void Verify() { Run("after"); }
    static void Run(string value)
    {
        Directory.CreateDirectory(Folder);
        File.Delete(Folder + "/rear-motion-failed.txt");
        EditorSceneManager.playModeStartScene = null;
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity");
        SessionState.SetString(Flag, value);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || Time.frameCount < 40) return;
        if (!running && SessionState.GetString(Flag, "") != "")
        {
            try
            {
                mode = SessionState.GetString(Flag, ""); SessionState.SetString(Flag, ""); running = true;
                car = Object.FindFirstObjectByType<TumblerController>(); body = car.GetComponent<Rigidbody>();
                originalInput = InputSystem.settings; testInput = Object.Instantiate(originalInput);
                InputSystem.settings = testInput;
                testInput.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                testInput.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                pad = InputSystem.AddDevice<Gamepad>();
                modes = Object.FindFirstObjectByType<SandboxCameraModes>();
                var axes = Object.FindFirstObjectByType<CinemachineInputAxisController>(); if (axes != null) axes.enabled = false;
                foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.gameObject.SetActive(false);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Temporary camera acceleration runway";
                floor.transform.position = new Vector3(0, 999, 0); floor.transform.localScale = new Vector3(2000, 2, 4000);
                body.interpolation = RigidbodyInterpolation.Interpolate;
                trace.Add("scenario,frame,time,kph,render_z,physics_z,follow_error,aim_error,viewport_x,viewport_y");
                StartScenario(); CinemachineCore.CameraUpdatedEvent.AddListener(Observe);
            }
            catch (Exception e) { Fail(e); }
        }
        if (!running || lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        InputSystem.QueueStateEvent(pad, new GamepadState { rightTrigger = 1,
            leftStick = scenario == 1 && Time.frameCount - startFrame > Rates[scenario] * 2 ? new Vector2(.13f, 0) : Vector2.zero
        }.WithButton(GamepadButton.LeftShoulder).WithButton(GamepadButton.South));
    }
    static void StartScenario()
    {
        Time.captureDeltaTime = 1f / Rates[scenario]; Time.fixedDeltaTime = .02f;
        body.position = new Vector3(0, 1001.08f, -600); body.rotation = Quaternion.identity;
        body.transform.SetPositionAndRotation(body.position, body.rotation);
        body.linearVelocity = Vector3.forward * Speeds[scenario]; body.angularVelocity = Vector3.zero;
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        car.ResetTransmission(); Physics.SyncTransforms();
        Object.FindFirstObjectByType<CinemachineCamera>().PreviousStateIsValid = false;
        startFrame = Time.frameCount;
        samples.Add(new Sample { fps = Rates[scenario], initialKph = Speeds[scenario] * 3.6f });
    }
    static void Observe(CinemachineBrain brain)
    {
        if (!running || brain.OutputCamera != Camera.main) return;
        try
        {
            int frame = Time.frameCount - startFrame;
            if (frame < Rates[scenario]) return; // Allow LB activation and SmartUpdate target classification.
            var camera = brain.OutputCamera; var sample = samples[scenario];
            if (!modes.IsLookingBack) throw new Exception("LB was not held during acceleration");
            Vector3 anchor = body.transform.position;
            Vector3 heading = Vector3.ProjectOnPlane(body.transform.forward, Vector3.up).normalized;
            Vector3 expected = anchor + heading * 5.8f + Vector3.up * 2.9f;
            float error = Vector3.Distance(camera.transform.position, expected);
            float aim = Vector3.Angle(camera.transform.forward, anchor - camera.transform.position);
            Vector3 viewport = camera.WorldToViewportPoint(anchor);
            sample.frames++; sample.maxKph = Mathf.Max(sample.maxKph, body.linearVelocity.magnitude * 3.6f);
            sample.maxFollowError = Mathf.Max(sample.maxFollowError, error);
            sample.maxAimErrorDegrees = Mathf.Max(sample.maxAimErrorDegrees, aim);
            sample.minFrontDistance = Mathf.Min(sample.minFrontDistance, Vector3.Dot(camera.transform.position - anchor, heading));
            sample.maxViewportOffset = Mathf.Max(sample.maxViewportOffset, Vector2.Distance(new Vector2(viewport.x, viewport.y), Vector2.one * .5f));
            sample.maxPhysicsRenderSeparation = Mathf.Max(sample.maxPhysicsRenderSeparation, Vector3.Distance(body.position, anchor));
            trace.Add($"{scenario},{frame},{Time.time:F4},{body.linearVelocity.magnitude * 3.6f:F3},{anchor.z:F4},{body.position.z:F4},{error:F5},{aim:F5},{viewport.x:F5},{viewport.y:F5}");
            if (frame == Rates[scenario] * 3) Capture(camera, $"rear-motion-{mode}-{Rates[scenario]}fps");
            if (frame < Rates[scenario] * 4) return;
            if (++scenario < Rates.Length) { StartScenario(); return; }
            File.WriteAllText(Folder + "/rear-motion-" + mode + ".json", JsonUtility.ToJson(new Report { unity = Application.unityVersion, mode = mode, samples = samples.ToArray() }, true));
            File.WriteAllLines(Folder + "/rear-motion-" + mode + ".csv", trace);
            if (mode == "after") foreach (var s in samples)
                if (s.frames < s.fps * 2 || s.maxFollowError > .03f || s.maxAimErrorDegrees > .3f || s.minFrontDistance < 5.7f || s.maxViewportOffset > .006f)
                    throw new Exception("Rear view lost its rendered vehicle anchor at " + s.fps + " FPS: " + JsonUtility.ToJson(s));
            running = false; CinemachineCore.CameraUpdatedEvent.RemoveListener(Observe);
            Time.captureDeltaTime = 0; InputSystem.RemoveDevice(pad); InputSystem.settings = originalInput != null ? originalInput : Object.Instantiate(testInput);
            Object.DestroyImmediate(testInput); EditorApplication.Exit(0);
        }
        catch (Exception e) { Fail(e); }
    }
    static void Capture(Camera camera, string name)
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
        var target = new RenderTexture(1440, 900, 24, RenderTextureFormat.ARGB32); target.Create();
        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
        var previous = RenderTexture.active; RenderTexture.active = target;
        var texture = new Texture2D(1440, 900, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0); texture.Apply();
        File.WriteAllBytes(Folder + "/" + name + ".png", texture.EncodeToPNG());
        RenderTexture.active = previous; Object.DestroyImmediate(texture); target.Release(); Object.DestroyImmediate(target);
    }
    static void Fail(Exception e)
    {
        running = false; CinemachineCore.CameraUpdatedEvent.RemoveListener(Observe); Time.captureDeltaTime = 0;
        File.WriteAllText(Folder + "/rear-motion-failed.txt", e.ToString()); Debug.LogException(e); EditorApplication.Exit(1);
    }
}
