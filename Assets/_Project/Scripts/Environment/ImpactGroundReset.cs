using UnityEngine;

/// <summary>Local, explicit reset of this facility's movable fixtures only. No input bindings.</summary>
public sealed class ImpactGroundReset : MonoBehaviour
{
    private Rigidbody[] bodies;
    private Vector3[] positions;
    private Quaternion[] rotations;
    private void Awake()
    {
        bodies = GetComponentsInChildren<Rigidbody>(true);
        positions = new Vector3[bodies.Length];
        rotations = new Quaternion[bodies.Length];
        for (int i = 0; i < bodies.Length; i++)
        {
            positions[i] = transform.InverseTransformPoint(bodies[i].position);
            rotations[i] = Quaternion.Inverse(transform.rotation) * bodies[i].rotation;
        }
    }
    [ContextMenu("Restore movable test objects")]
    public void RestoreObjects()
    {
        if (!Application.isPlaying || bodies == null) return;
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] == null) continue;
            bodies[i].linearVelocity = Vector3.zero;
            bodies[i].angularVelocity = Vector3.zero;
            bodies[i].position = transform.TransformPoint(positions[i]);
            bodies[i].rotation = transform.rotation * rotations[i];
            bodies[i].Sleep();
        }
    }
}
