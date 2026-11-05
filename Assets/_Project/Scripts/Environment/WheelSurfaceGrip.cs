using UnityEngine;

/// <summary>Scales tire stiffness on marked surfaces while retaining controller slip and drive settings.</summary>
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public sealed class WheelSurfaceGrip : MonoBehaviour
{
    [SerializeField] private Transform vehicleRoot;
    private WheelCollider[] wheels;
    private float[] forwardBase;
    private float[] sidewaysBase;

    private void OnEnable()
    {
        if (vehicleRoot == null) return;
        wheels = vehicleRoot.GetComponentsInChildren<WheelCollider>(true);
        forwardBase = new float[wheels.Length];
        sidewaysBase = new float[wheels.Length];
        for (int i = 0; i < wheels.Length; i++)
        {
            forwardBase[i] = wheels[i].forwardFriction.stiffness;
            sidewaysBase[i] = wheels[i].sidewaysFriction.stiffness;
        }
    }

    private void FixedUpdate()
    {
        if (wheels == null) return;
        for (int i = 0; i < wheels.Length; i++)
        {
            if (wheels[i] == null) continue;
            DrivingSurface surface = null;
            if (wheels[i].GetGroundHit(out WheelHit hit) && hit.collider != null)
                surface = hit.collider.GetComponentInParent<DrivingSurface>();
            Vector2 grip = surface == null ? Vector2.one : new Vector2(surface.forwardGrip, surface.sidewaysGrip);
            if (surface == null && wheels[i].GetGroundHit(out WheelHit terrainHit) && terrainHit.collider != null)
            {
                var terrainSurface = terrainHit.collider.GetComponent<TerrainDrivingSurface>();
                if (terrainSurface != null) grip = terrainSurface.GripAt(terrainHit.point);
            }
            SetStiffness(i, Mathf.Clamp(grip.x, 0.05f, 1.5f), Mathf.Clamp(grip.y, 0.05f, 1.5f));
        }
    }

    private void SetStiffness(int i, float forward, float sideways)
    {
        var f = wheels[i].forwardFriction;
        var s = wheels[i].sidewaysFriction;
        f.stiffness = forwardBase[i] * forward;
        s.stiffness = sidewaysBase[i] * sideways;
        wheels[i].forwardFriction = f;
        wheels[i].sidewaysFriction = s;
    }

    private void OnDisable()
    {
        if (wheels == null) return;
        for (int i = 0; i < wheels.Length; i++)
            if (wheels[i] != null) SetStiffness(i, 1f, 1f);
        wheels = null;
    }
}
