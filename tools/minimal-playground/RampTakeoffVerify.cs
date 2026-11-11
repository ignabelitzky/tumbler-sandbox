using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Real six-wheel, unconstrained Rigidbody comparison on the retained square ramp.
// No rail, force, rotation correction or camera/UI objects are added to gameplay.
[InitializeOnLoad]
public static class RampTakeoffVerify
{
    const string Flag = "RampTakeoffVerify";
    const string Folder = "TestResults/RampTakeoff";
    static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static Rigidbody body;
    static TumblerController car;
    static WheelCollider[] wheels, front, rear;
    static float[] calibratedPoints;
    static TumblerFlightDynamics dynamics;
    static RampTakeoffVerify() { EditorApplication.update += Tick; }

    [Serializable] public class Trial
    {
        public string name;
        public float speed, peakRearOnlyPitchRate, releasePitchRate, airTime, finalUp;
        public int rearOnlySteps;
        public bool clearedRamp;
    }
    [Serializable] public class Report
    {
        public string unity;
        public float timestep;
        public Trial[] before, after;
    }
    public static void Run()
    {
        Directory.CreateDirectory(Folder);
        File.Delete(Folder + "/complete.txt");
        File.Delete(Folder + "/failed.txt");
        EditorSceneManager.playModeStartScene = null;
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity");
        SessionState.SetBool(Flag, true);
        EditorApplication.EnterPlaymode();
    }
    static void Set(string field, object value) => typeof(TumblerController).GetField(field, Fields).SetValue(car, value);
    static void Tick()
    {
        if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying || Time.frameCount < 60) return;
        SessionState.SetBool(Flag, false);
        try
        {
            car = Object.FindFirstObjectByType<TumblerController>();
            body = car.GetComponent<Rigidbody>();
            front = (WheelCollider[])typeof(TumblerController).GetField("frontWheelsColliders", Fields).GetValue(car);
            rear = (WheelCollider[])typeof(TumblerController).GetField("rearWheelsColliders", Fields).GetValue(car);
            wheels = front.Concat(rear).ToArray();
            if (front.Length != 2 || rear.Length != 4 || body.constraints != RigidbodyConstraints.None)
                throw new Exception("Expected the unconstrained two-front/four-rear production vehicle");
            foreach (var mb in body.GetComponentsInChildren<MonoBehaviour>())
                if (!(mb is TumblerImpactPhysics) && !(mb is TumblerChassisFriction)) mb.enabled = false;
            dynamics = (TumblerFlightDynamics)typeof(TumblerController).GetField("flight", Fields).GetValue(car);
            dynamics.Enable();
            calibratedPoints = wheels.Select(w => w.forceAppPointDistance).ToArray();
            body.interpolation = RigidbodyInterpolation.None;
            Physics.simulationMode = SimulationMode.Script;
            Time.fixedDeltaTime = .02f;
            var ramp = GameObject.Find("Original square_ramp");
            if (ramp == null) throw new Exception("The square ramp is missing");
            var report = new Report { unity = Application.unityVersion, timestep = .02f };
            var before = new List<Trial>(); var after = new List<Trial>();
            foreach (bool calibrated in new[] { false, true })
                foreach (float speed in new[] { 5f, 12f, 22f })
                    foreach (int direction in new[] { 1, -1 })
                    {
                        var result = Drive(ramp, speed, direction, calibrated);
                        (calibrated ? after : before).Add(result);
                    }
            report.before = before.ToArray(); report.after = after.ToArray();
            File.WriteAllText(Folder + "/comparison.json", JsonUtility.ToJson(report, true));
            // An inconclusive fixture must never count as a successful takeoff fix.
            if (!after.All(t => t.clearedRamp) || !after.Any(t => t.rearOnlySteps > 0 && t.airTime > .1f))
                throw new Exception("Fixture did not complete every traversal and reproduce a rear-only takeoff; inspect traces");
            float oldRate = before.Where(t => t.speed >= 12).Sum(t => t.peakRearOnlyPitchRate);
            float newRate = after.Where(t => t.speed >= 12).Sum(t => t.peakRearOnlyPitchRate);
            if (oldRate <= .01f || newRate >= oldRate * .9f || after.Any(t => t.finalUp < .75f))
                throw new Exception("Ramp acceptance requires measured pitch reduction and upright landings; inspect comparison.json");
            File.WriteAllText(Folder + "/complete.txt", "PASS paired real-square-ramp traversals and rear-only takeoff comparison\n");
            Physics.simulationMode = SimulationMode.FixedUpdate;
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            File.WriteAllText(Folder + "/failed.txt", e.ToString());
            Debug.LogException(e);
            Physics.simulationMode = SimulationMode.FixedUpdate;
            EditorApplication.Exit(1);
        }
    }
    static Trial Drive(GameObject ramp, float speed, int direction, bool calibrated)
    {
        Vector3 forward = Vector3.ProjectOnPlane(ramp.transform.forward, Vector3.up).normalized * direction;
        var bounds = ramp.GetComponent<Renderer>().bounds;
        float extent = Mathf.Abs(forward.x) * bounds.extents.x + Mathf.Abs(forward.z) * bounds.extents.z;
        Vector3 start = bounds.center - forward * (extent + 20);
        body.position = new Vector3(0, 5000, 0); body.transform.position = body.position;
        Physics.SyncTransforms();
        if (!Physics.Raycast(start + Vector3.up * 100, Vector3.down, out var hit, 200, ~0, QueryTriggerInteraction.Ignore))
            throw new Exception("No approach surface for square ramp");
        body.position = hit.point + Vector3.up * 1.1f;
        body.rotation = Quaternion.LookRotation(forward, Vector3.up);
        body.transform.SetPositionAndRotation(body.position, body.rotation);
        body.linearVelocity = body.angularVelocity = Vector3.zero;
        body.constraints = RigidbodyConstraints.None;
        car.ResetTransmission();
        for (int i = 0; i < wheels.Length; i++)
        {
            wheels[i].forceAppPointDistance = calibrated ? calibratedPoints[i] : 0;
            wheels[i].suspensionExpansionLimited = calibrated;
            wheels[i].motorTorque = wheels[i].brakeTorque = wheels[i].steerAngle = wheels[i].rotationSpeed = 0;
        }
        Physics.SyncTransforms();
        // Free settling preserves the actual inertia and all rotational freedoms.
        for (int i = 0; i < 120; i++) Physics.Simulate(.02f);
        body.linearVelocity = forward * speed;
        var trial = new Trial { name = (calibrated ? "after" : "before") + "-" + speed + "-" + direction, speed = speed };
        var trace = new List<string> { "time,x,y,z,kph,front_contacts,rear_contacts,pitch_rate,up_y" };
        bool wasRearOnly = false;
        for (int step = 0; step < 1000; step++)
        {
            float along = Vector3.Dot(body.linearVelocity, forward);
            Set("currentPower", 1400f * Mathf.Clamp01((speed - along) * .5f));
            Set("currentBrakeForce", Mathf.Clamp((along - speed) * 700, 0, 1000));
            Set("steerInput", 0f); Set("currentTurnAngle", 0f); Set("isHandbrakeActive", false);
            car.SendMessage("FixedUpdate");
            Object.FindFirstObjectByType<WheelSurfaceGrip>()?.SendMessage("FixedUpdate");
            body.GetComponent<TumblerChassisFriction>()?.SendMessage("FixedUpdate");
            Physics.Simulate(.02f);
            int f = front.Count(w => w.isGrounded), r = rear.Count(w => w.isGrounded);
            float progress = Vector3.Dot(body.position - bounds.center, forward);
            float pitch = Mathf.Abs(Vector3.Dot(body.angularVelocity, body.transform.right));
            bool rearOnly = f == 0 && r == 4 && progress > 0;
            if (rearOnly) { trial.rearOnlySteps++; trial.peakRearOnlyPitchRate = Mathf.Max(trial.peakRearOnlyPitchRate, pitch); }
            if (f + r == 0) { trial.airTime += .02f; if (wasRearOnly) trial.releasePitchRate = pitch; }
            wasRearOnly = rearOnly;
            if (step % 2 == 0) trace.Add($"{step*.02f:F2},{body.position.x:F3},{body.position.y:F3},{body.position.z:F3},{along*3.6f:F2},{f},{r},{pitch:F4},{body.transform.up.y:F3}");
            if (progress > extent + 35 && f + r >= 4) { trial.clearedRamp = true; break; }
            if (body.position.y < -20 || body.transform.up.y < .3f) break;
        }
        trial.finalUp = body.transform.up.y;
        File.WriteAllLines(Folder + "/" + trial.name + ".csv", trace);
        return trial;
    }
}
