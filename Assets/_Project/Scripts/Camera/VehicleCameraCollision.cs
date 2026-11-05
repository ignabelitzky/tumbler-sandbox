using Unity.Cinemachine;
using UnityEngine;

/// <summary>Keep the orbit on the visible side of tunnel walls/roof and perimeter barriers.</summary>
public sealed class VehicleCameraCollision : CinemachineExtension
{
    public Rigidbody Vehicle { get; set; }
    private bool lookBack, viewChanged;
    public bool LookBack
    {
        get => lookBack;
        set { if (lookBack != value) { lookBack = value; viewChanged = true; } }
    }
    private Vector3 lastPlanarForward = Vector3.forward;
    private readonly RaycastHit[] hits = new RaycastHit[64];
    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Body || Vehicle == null) return;
        Vector3 focus = Vehicle.position + Vector3.up;
        if (LookBack)
        {
            Vector3 heading = Vector3.ProjectOnPlane(Vehicle.transform.forward, Vector3.up);
            if (heading.sqrMagnitude > .01f) lastPlanarForward = heading.normalized;
            state.RawPosition = Vehicle.position + lastPlanarForward * 5.8f + Vector3.up * 2.9f;
        }
        Vector3 offset = state.RawPosition - focus;
        float distance = offset.magnitude;
        if (distance < .01f) return;
        Vector3 direction = offset / distance;
        int count = Physics.SphereCastNonAlloc(focus, .25f, direction, hits, distance,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float allowed = distance;
        for (int i = 0; i < count; i++)
            if (hits[i].collider != null && hits[i].collider.attachedRigidbody != Vehicle)
                allowed = Mathf.Min(allowed, Mathf.Max(.35f, hits[i].distance - .12f));
        state.RawPosition = focus + direction * allowed;
        if (LookBack || viewChanged)
        {
            // Reset only aim damping; resetting the full camera would recenter the user's saved orbit axes.
            var composer = vcam.GetComponent<CinemachineRotationComposer>();
            Vector3 aim = state.ReferenceLookAt - state.RawPosition;
            if (composer != null && aim.sqrMagnitude > .001f)
                composer.ForceCameraPosition(state.RawPosition, Quaternion.LookRotation(aim, Vector3.up));
            viewChanged = false;
        }
    }
}
