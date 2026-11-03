using UnityEngine;
/// <summary>Opt-in measurement helper used only by the native verifier, never added to the scene.</summary>
public sealed class ImpactMeasurementProbe : MonoBehaviour
{
    public float peakImpulse;
    private void OnCollisionEnter(Collision collision) => peakImpulse = Mathf.Max(peakImpulse, collision.impulse.magnitude);
    private void OnCollisionStay(Collision collision) => peakImpulse = Mathf.Max(peakImpulse, collision.impulse.magnitude);
}
