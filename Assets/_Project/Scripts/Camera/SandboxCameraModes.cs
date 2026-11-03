using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Three orbit sizes; tunnel proximity temporarily overrides the selected size.</summary>
[DefaultExecutionOrder(-90)]
public sealed class SandboxCameraModes : MonoBehaviour
{
    public enum ViewMode { Close, Medium, Far }
    [SerializeField] private CinemachineOrbitalFollow orbit;
    [SerializeField] private Rigidbody vehicle;
    [SerializeField] private BoxCollider[] tunnelZones = new BoxCollider[0];
    [SerializeField] private ViewMode selectedMode = ViewMode.Close;
    [SerializeField] private CinemachineBlendDefinition transition =
        new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, .9f);
    [SerializeField, Min(.01f)] private float tunnelTransitionTime = .35f;
    private Cinemachine3OrbitRig.Settings fromOrbits, targetOrbits;
    private float fromRadius, targetRadius, blendElapsed, blendDuration;
    private ViewMode targetMode;
    private bool hasTarget;
    public bool IsTransitioning => hasTarget && blendElapsed < blendDuration;
    private InputAction cycle;
    private Cinemachine3OrbitRig.Settings originalOrbits;
    private float originalRadius;
    private VehicleCameraCollision collision;
    private bool ownsCollision, initialized;
    private Vector3 lastVehiclePosition;
    public ViewMode SelectedMode => selectedMode;
    public ViewMode EffectiveMode => IsInsideTunnel ? ViewMode.Close : selectedMode;
    public bool IsInsideTunnel { get; private set; }
    public BoxCollider[] TunnelZones => tunnelZones;

    private void Awake()
    {
        if (orbit == null) orbit = FindFirstObjectByType<CinemachineOrbitalFollow>();
        if (vehicle == null)
        {
            var controller = FindFirstObjectByType<TumblerController>();
            if (controller != null) vehicle = controller.GetComponent<Rigidbody>();
        }
        if (orbit == null || vehicle == null)
        { Debug.LogError("Sandbox camera modes need the vehicle and Cinemachine orbit.", this); enabled = false; return; }
        originalOrbits = orbit.Orbits;
        originalRadius = orbit.Radius;
        initialized = true;
        lastVehiclePosition = vehicle.position;
        collision = orbit.GetComponent<VehicleCameraCollision>();
        if (collision == null) { collision = orbit.gameObject.AddComponent<VehicleCameraCollision>(); ownsCollision = true; }
        collision.Vehicle = vehicle;
        cycle = new InputAction("Cycle camera", InputActionType.Button, "<Keyboard>/c");
        cycle.AddBinding("<Gamepad>/dpad/down");
        cycle.performed += OnCycle;
        RefreshTunnelState();
    }

    private void OnEnable() { cycle?.Enable(); if (initialized) RefreshTunnelState(); }
    private void OnDisable()
    {
        cycle?.Disable();
        IsInsideTunnel = false;
        hasTarget = false;
        if (initialized && orbit != null) { orbit.Orbits = originalOrbits; orbit.Radius = originalRadius; }
        if (collision != null && ownsCollision) collision.enabled = false;
    }
    private void OnDestroy()
    {
        cycle?.Dispose();
        if (ownsCollision && collision != null) Destroy(collision);
    }
    private void OnCycle(InputAction.CallbackContext context) => CycleMode();
    public void CycleMode()
    {
        RefreshTunnelState();
        if (GameNavigation.GameplayInputBlocked || IsInsideTunnel) return;
        selectedMode = (ViewMode)(((int)selectedMode + 1) % 3);
        ApplyMode();
    }
    private void Update()
    {
        RefreshTunnelState();
        AdvanceTransition(Time.deltaTime);
    }

    public void RefreshTunnelState()
    {
        if (!initialized || orbit == null || vehicle == null) return;
        // Query all overlapping zones together: no wheel-trigger races at segment seams,
        // and teleport/reset/disable/return-to-menu cannot leave a stale override.
        bool teleported = (vehicle.position - lastVehiclePosition).sqrMagnitude > 40f * 40f;
        lastVehiclePosition = vehicle.position;
        IsInsideTunnel = false;
        foreach (var zone in tunnelZones)
        {
            if (zone == null || !zone.enabled || !zone.gameObject.activeInHierarchy) continue;
            Vector3 p = zone.transform.InverseTransformPoint(vehicle.position) - zone.center;
            Vector3 half = zone.size * .5f;
            if (Mathf.Abs(p.x) <= half.x && Mathf.Abs(p.y) <= half.y && Mathf.Abs(p.z) <= half.z)
            { IsInsideTunnel = true; break; }
        }
        ApplyMode(teleported);
        if (teleported) orbit.VirtualCamera.PreviousStateIsValid = false;
    }
    private void ApplyMode(bool snap = false)
    {
        if (collision != null && ownsCollision) collision.enabled = true;
        if (hasTarget && targetMode == EffectiveMode && !snap) return;
        bool first = !hasTarget;
        hasTarget = true; targetMode = EffectiveMode;
        // Scale all three rings: the existing rig uses ThreeRing, whose Radius field
        // alone does not change orbit size. Preserve its aim, input axes and damping.
        float scale = EffectiveMode == ViewMode.Close ? .58f : EffectiveMode == ViewMode.Medium ? 1f : 1.6f;
        var rings = originalOrbits;
        rings.Top.Radius *= scale; rings.Top.Height *= scale;
        rings.Center.Radius *= scale; rings.Center.Height *= scale;
        rings.Bottom.Radius *= scale; rings.Bottom.Height *= scale;
        if (EffectiveMode == ViewMode.Close)
        {
            // A close camera must look over the roof, not into the rear bodywork.
            rings.Top.Height = 3.7f; rings.Center.Height = 2.2f; rings.Bottom.Height = 1.1f;
        }
        fromOrbits = orbit.Orbits; fromRadius = orbit.Radius;
        targetOrbits = rings; targetRadius = originalRadius * scale;
        blendElapsed = 0;
        blendDuration = IsInsideTunnel ? Mathf.Min(tunnelTransitionTime, transition.BlendTime) : transition.BlendTime;
        if (first || snap || !Application.isPlaying || blendDuration <= 0)
            AdvanceTransition(blendDuration);
    }
    private void AdvanceTransition(float deltaTime)
    {
        if (!hasTarget || orbit == null) return;
        blendElapsed = Mathf.Min(blendDuration, blendElapsed + Mathf.Max(0, deltaTime));
        float t = blendDuration <= 0 ? 1 : blendElapsed / blendDuration;
        if (transition.BlendCurve != null) t = Mathf.Clamp01(transition.BlendCurve.Evaluate(t));
        var rings = targetOrbits;
        rings.Top.Radius = Mathf.Lerp(fromOrbits.Top.Radius, targetOrbits.Top.Radius, t);
        rings.Top.Height = Mathf.Lerp(fromOrbits.Top.Height, targetOrbits.Top.Height, t);
        rings.Center.Radius = Mathf.Lerp(fromOrbits.Center.Radius, targetOrbits.Center.Radius, t);
        rings.Center.Height = Mathf.Lerp(fromOrbits.Center.Height, targetOrbits.Center.Height, t);
        rings.Bottom.Radius = Mathf.Lerp(fromOrbits.Bottom.Radius, targetOrbits.Bottom.Radius, t);
        rings.Bottom.Height = Mathf.Lerp(fromOrbits.Bottom.Height, targetOrbits.Bottom.Height, t);
        orbit.Orbits = rings; orbit.Radius = Mathf.Lerp(fromRadius, targetRadius, t);
    }
}
