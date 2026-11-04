using System.Collections.Generic;
using UnityEngine;

/// <summary>Native contact grip for side/roof landings, preserving upright underbody glide.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
public sealed class TumblerChassisFriction : MonoBehaviour
{
    [SerializeField, Range(0, 1)] private float scrapeStaticFriction = .8f;
    [SerializeField, Range(0, 1)] private float scrapeDynamicFriction = .65f;
    private struct Surface
    {
        public Collider Collider;
        public PhysicsMaterial Original;
        public bool Underbody;
    }
    private readonly List<Surface> surfaces = new List<Surface>();
    private PhysicsMaterial scrapeMaterial;
    private bool tipped;
    private void OnEnable()
    {
        var body = GetComponent<Rigidbody>();
        var hull = transform.Find("Body")?.GetComponent<MeshCollider>();
        if (hull == null) return;
        scrapeMaterial = new PhysicsMaterial("Runtime Tumbler chassis scrape")
        {
            staticFriction = scrapeStaticFriction, dynamicFriction = scrapeDynamicFriction,
            frictionCombine = PhysicsMaterialCombine.Average,
            bounciness = 0, bounceCombine = PhysicsMaterialCombine.Minimum,
            hideFlags = HideFlags.HideAndDontSave
        };
        foreach (var collider in GetComponentsInChildren<Collider>(true))
        {
            if (collider is WheelCollider || collider.isTrigger || collider.attachedRigidbody != body) continue;
            surfaces.Add(new Surface { Collider = collider, Original = collider.sharedMaterial,
                Underbody = collider.sharedMaterial == hull.sharedMaterial });
        }
        tipped = false;
        UpdateGrip();
    }
    private void FixedUpdate() => UpdateGrip();
    private void UpdateGrip()
    {
        if (scrapeMaterial == null) return;
        // Hysteresis prevents material chatter near the side-contact transition.
        // Use gravity rather than world Y so a rotated gravity field remains coherent.
        if (Physics.gravity.sqrMagnitude > .0001f)
        {
            float alignment = Vector3.Dot(transform.up, -Physics.gravity.normalized);
            tipped = tipped ? alignment < .75f : alignment < .65f;
        }
        foreach (var surface in surfaces)
        {
            if (surface.Collider == null) continue;
            var desired = surface.Underbody && !tipped ? surface.Original : scrapeMaterial;
            if (surface.Collider.sharedMaterial != desired) surface.Collider.sharedMaterial = desired;
        }
    }
    private void OnDisable()
    {
        foreach (var surface in surfaces)
            if (surface.Collider != null && surface.Collider.sharedMaterial == scrapeMaterial)
                surface.Collider.sharedMaterial = surface.Original;
        surfaces.Clear();
        if (scrapeMaterial != null) Destroy(scrapeMaterial);
        scrapeMaterial = null;
        tipped = false;
    }
}
