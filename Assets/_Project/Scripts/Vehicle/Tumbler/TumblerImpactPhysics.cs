using UnityEngine;

/// <summary>Local contact calibration; preserves mass properties and the beveled sliding hull.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
public sealed class TumblerImpactPhysics : MonoBehaviour
{
    [SerializeField, Range(6, 30)] private int positionIterations = 12;
    [SerializeField, Range(1, 12)] private int velocityIterations = 4;
    [SerializeField, Min(.1f)] private float depenetrationLimit = 5f;
    private Rigidbody body;
    private GameObject frontFace;
    private TumblerChassisFriction chassisFriction;
    private int oldIterations, oldVelocityIterations;
    private float oldDepenetration;
    private bool captured;
    private void OnEnable()
    {
        body = GetComponent<Rigidbody>();
        oldIterations = body.solverIterations;
        oldVelocityIterations = body.solverVelocityIterations;
        oldDepenetration = body.maxDepenetrationVelocity;
        captured = true;
        ApplyCalibration();
    }
    [ContextMenu("Apply impact calibration")]
    public void ApplyCalibration()
    {
        if (!Application.isPlaying || !captured) return;
        body.solverIterations = positionIterations;
        body.solverVelocityIterations = velocityIterations;
        body.maxDepenetrationVelocity = depenetrationLimit;
        if (frontFace == null)
        {
            var hull = transform.Find("Body").GetComponent<MeshCollider>();
            if (hull == null || hull.sharedMesh == null) return;
            var bounds = hull.sharedMesh.bounds;
            frontFace = new GameObject("Front impact contact face");
            frontFace.transform.SetParent(hull.transform, false);
            var face = frontFace.AddComponent<BoxCollider>();
            face.size = new Vector3(2.8f, .20f, .20f);
            face.center = new Vector3(bounds.center.x, bounds.min.y + .20f, bounds.max.z - .10f);
            face.sharedMaterial = hull.sharedMaterial;
        }
        frontFace.SetActive(true);
        chassisFriction = GetComponent<TumblerChassisFriction>();
        if (chassisFriction == null) chassisFriction = gameObject.AddComponent<TumblerChassisFriction>();
        chassisFriction.enabled = true;
    }
    private void OnDisable()
    {
        if (!captured || body == null) return;
        body.solverIterations = oldIterations;
        body.solverVelocityIterations = oldVelocityIterations;
        body.maxDepenetrationVelocity = oldDepenetration;
        if (frontFace != null) frontFace.SetActive(false);
        if (chassisFriction != null) chassisFriction.enabled = false;
        captured = false;
    }
}
