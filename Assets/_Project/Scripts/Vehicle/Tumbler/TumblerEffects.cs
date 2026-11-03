using UnityEngine;

[DefaultExecutionOrder(50)]
public class TumblerEffects : MonoBehaviour
{
    [Header("Tumbler Settings")]
    [SerializeField] private GameObject tumbler;
    private TumblerController tumblerController;
    private Rigidbody vehicleBody;

    [Header("Wheel References")]
    [SerializeField] private float[] tireMarkWidths;
    [SerializeField] private ParticleSystem[] tireSmokes;
    [SerializeField] private WheelCollider[] tireColliders;

    [Header("Subtle Contact Marks")]
    [SerializeField] private Material skidmarkMaterial;
    [SerializeField, Min(0.1f)] private float minimumSpeed = 1.5f;
    [SerializeField, Min(0.01f)] private float sidewaysSlipThreshold = 0.15f;
    [SerializeField, Min(0.01f)] private float brakingSlipThreshold = 0.08f;
    [SerializeField, Range(0f, 0.5f)] private float maximumOpacity = 0.24f;
    [SerializeField, Min(1f)] private float markLifetime = 25f;
    [SerializeField, Min(0.1f)] private float fadeDuration = 8f;
    [SerializeField, Range(0.1f, 0.5f)] private float sampleSpacing = 0.2f;
    [SerializeField, Range(0.003f, 0.03f)] private float surfaceOffset = 0.008f;

    [SerializeField, Min(1f)] private float burnoutSmokeRate = 60f;

    private SurfaceSkidmarks marks;
    private float[] widths, smokeRemainders;
    private ParticleSystem[] wheelSmokes;
    private Vector3 previousVehiclePosition;

    private void Awake()
    {
        if (tumbler == null || !tumbler.TryGetComponent(out tumblerController) ||
            !tumbler.TryGetComponent(out vehicleBody))
        {
            Debug.LogError("TumblerEffects requires its vehicle controller and Rigidbody.", this);
            enabled = false;
            return;
        }
        widths = new float[tireColliders.Length];
        for (int i = 0; i < tireColliders.Length; i++)
        {
            widths[i] = tireMarkWidths != null && i < tireMarkWidths.Length
                ? Mathf.Clamp(tireMarkWidths[i], 0.12f, 0.7f) : 0.35f;
        }
        wheelSmokes = new ParticleSystem[tireColliders.Length];
        for (int i=2;i<wheelSmokes.Length;i++) if(i-2<tireSmokes.Length) wheelSmokes[i]=tireSmokes[i-2];
        // Reuse the established smoke material/settings for the previously unsupported front axle.
        for(int i=0;i<2&&i<wheelSmokes.Length;i++)
            if(tireSmokes.Length>0&&tireSmokes[0]!=null)wheelSmokes[i]=Instantiate(tireSmokes[0],transform);
        smokeRemainders = new float[wheelSmokes.Length];
        var smokeMaterial = Resources.Load<Material>("TireSmokeHDRP");
        foreach (var smoke in wheelSmokes)
        {
            if (smoke == null) continue;
            var main = smoke.main;
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.25f, .55f);
            main.startSize3D = false;
            main.startSize = new ParticleSystem.MinMaxCurve(.35f, .65f);
            main.startColor = new Color(.42f, .44f, .46f, .24f);
            main.maxParticles = 256;
            var sheet = smoke.textureSheetAnimation; sheet.enabled = false;
            var bySpeed = smoke.sizeBySpeed; bySpeed.enabled = false;
            var tintBySpeed = smoke.colorBySpeed; tintBySpeed.enabled = false;
            var noise = smoke.noise; noise.enabled = false;
            var force = smoke.forceOverLifetime; force.enabled = false;
            var trails = smoke.trails; trails.enabled = false;
            var color = smoke.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .12f), new GradientAlphaKey(.5f, .65f), new GradientAlphaKey(0, 1) });
            color.color = fade;
            var size = smoke.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .65f, 1, 3f));
            var shape = smoke.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .12f;
            var velocity = smoke.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-.12f, .12f); velocity.y = new ParticleSystem.MinMaxCurve(.25f, .5f); velocity.z = new ParticleSystem.MinMaxCurve(-.12f, .12f);
            var renderer = smoke.GetComponent<ParticleSystemRenderer>();
            renderer.SetActiveVertexStreams(new System.Collections.Generic.List<ParticleSystemVertexStream> { ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV });
            renderer.pivot = Vector3.zero;
            renderer.enabled = true;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.minParticleSize = 0; renderer.maxParticleSize = .16f;
            if (smokeMaterial != null) renderer.sharedMaterial = smokeMaterial;
            else Debug.LogError("Missing HDRP tire smoke material", this);
            var emission = smoke.emission;
            emission.enabled = false;
            main.playOnAwake = false;
            main.loop = true;
            smoke.gameObject.SetActive(true);
            smoke.Play();
        }
        if (skidmarkMaterial != null)
        {
            // A separate scene root keeps deposited vertices fixed when the car moves, rolls or resets.
            marks = new GameObject("Surface Skidmarks").AddComponent<SurfaceSkidmarks>();
            marks.Initialize(skidmarkMaterial, tireColliders.Length, sampleSpacing, surfaceOffset,
                markLifetime, fadeDuration);
        }
        previousVehiclePosition = vehicleBody.position;
    }

    private void FixedUpdate()
    {
        if (vehicleBody == null) return;
        float expectedTravel = vehicleBody.linearVelocity.magnitude * Time.deltaTime;
        if (Vector3.Distance(previousVehiclePosition, vehicleBody.position) > Mathf.Max(3f, expectedTravel * 3f))
        {
            if (marks != null) marks.BreakAll();
        }
        previousVehiclePosition = vehicleBody.position;

        for (int i = 0; i < tireColliders.Length; i++)
        {
            var wheel = tireColliders[i];
            if (wheel == null || !wheel.GetGroundHit(out WheelHit hit) || hit.collider == null)
            {
                StopWheel(i);
                continue;
            }
            // Tire effects follow that wheel, even when the other axle is airborne.
            float speed = Vector3.ProjectOnPlane(vehicleBody.GetPointVelocity(hit.point), hit.normal).magnitude;
            float intensity = tumblerController.IsBurnoutActive ? (tumblerController.IsWheelBurning(wheel) ? 1f : 0f) : ContactIntensity(hit.sidewaysSlip, hit.forwardSlip, wheel.brakeTorque, speed);
            if (intensity <= 0f || (hit.collider.attachedRigidbody != null && !hit.collider.attachedRigidbody.isKinematic))
            {
                StopWheel(i);
                continue;
            }
            if (marks != null) marks.AddContact(i, hit, widths[i], intensity * maximumOpacity);
            EmitSmoke(i, hit, intensity);
        }
    }

    private float ContactIntensity(float sidewaysSlip, float forwardSlip, float brakeTorque, float speed)
    {
        if (speed < minimumSpeed) return 0f;
        float lateral = Mathf.InverseLerp(sidewaysSlipThreshold, 0.65f, Mathf.Abs(sidewaysSlip));
        float braking = brakeTorque > 1f
            ? Mathf.InverseLerp(brakingSlipThreshold, 0.65f, Mathf.Abs(forwardSlip)) : 0f;
        return Mathf.SmoothStep(0f, 1f, Mathf.Max(lateral, braking));
    }

    private void EmitSmoke(int wheel, WheelHit hit, float intensity)
    {
        int i = wheel;
        if (i < 0 || i >= wheelSmokes.Length || wheelSmokes[i] == null) return;
        var smoke = wheelSmokes[i];
        smoke.transform.position = hit.point + hit.normal * 0.04f;
        float rate = tumblerController.IsBurnoutActive ? burnoutSmokeRate : 18f;
        smokeRemainders[i] += Mathf.Max(0f, intensity - 0.35f) * rate * Time.fixedDeltaTime;
        int count = Mathf.FloorToInt(smokeRemainders[i]);
        if (count == 0) return;
        smokeRemainders[i] -= count;
        smoke.Emit(count);
    }

    private void StopWheel(int wheel)
    {
        if (marks != null) marks.BreakTrail(wheel);
        int smoke = wheel;
        if (smoke >= 0 && smoke < smokeRemainders.Length) smokeRemainders[smoke] = 0f;
    }

    private void OnDisable()
    {
        if (marks != null) marks.BreakAll();
    }

    private void OnDestroy()
    {
        if (marks != null) Destroy(marks.gameObject);
    }
}
