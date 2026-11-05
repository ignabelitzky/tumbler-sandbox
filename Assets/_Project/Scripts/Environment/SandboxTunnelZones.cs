using System.Collections.Generic;
using UnityEngine;

/// <summary>Additional covered routes share the existing camera service and restore its selected preset on exit.</summary>
public sealed class SandboxTunnelZones : MonoBehaviour
{
    [SerializeField] private BoxCollider[] zones = new BoxCollider[0];
    private static readonly HashSet<SandboxTunnelZones> active = new HashSet<SandboxTunnelZones>();
    private void OnEnable() => active.Add(this);
    private void OnDisable() => active.Remove(this);
    public static bool Contains(Vector3 position)
    {
        foreach (var group in active)
        {
            if (group == null || !group.isActiveAndEnabled) continue;
            foreach (var zone in group.zones)
            {
                if (zone == null || !zone.enabled || !zone.gameObject.activeInHierarchy) continue;
                Vector3 p = zone.transform.InverseTransformPoint(position) - zone.center;
                Vector3 h = zone.size * .5f;
                if (Mathf.Abs(p.x) <= h.x && Mathf.Abs(p.y) <= h.y && Mathf.Abs(p.z) <= h.z) return true;
            }
        }
        return false;
    }
}
