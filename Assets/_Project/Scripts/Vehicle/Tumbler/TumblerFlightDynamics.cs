using UnityEngine;

/// <summary>Energy absorption when an upright jump lands on its wheels.</summary>
public sealed class TumblerFlightDynamics
{
    private readonly Rigidbody body;
    private readonly WheelCollider[] wheels;
    private readonly JointSpring[] originalSprings;
    private bool enabled, bodyContact;
    private float airTime, landingTime, pendingLanding, impactSpeed;
    private Vector3 previousPosition;
    public bool IsAirborne { get; private set; }

    public TumblerFlightDynamics(Rigidbody body, WheelCollider[] wheels)
    {
        this.body = body;
        this.wheels = wheels;
        originalSprings = new JointSpring[wheels.Length];
        for (int i = 0; i < wheels.Length; i++) originalSprings[i] = wheels[i].suspensionSpring;
    }

    public void Enable()
    {
        if (enabled) return;
        enabled = true;
        for (int i = 0; i < wheels.Length; i++)
        {
            var spring = originalSprings[i];
            // Each wheel uses its own supported mass; keep spring stiffness, travel and ride height.
            spring.damper = Mathf.Max(spring.damper, 1.9f * Mathf.Sqrt(spring.spring * wheels[i].sprungMass));
            wheels[i].suspensionSpring = spring;
        }
        Reset();
    }

    public void Disable()
    {
        if (enabled)
            for (int i = 0; i < wheels.Length; i++)
                if (wheels[i] != null) wheels[i].suspensionSpring = originalSprings[i];
        enabled = false;
        Reset();
    }

    public void Reset()
    {
        airTime = landingTime = pendingLanding = impactSpeed = 0;
        IsAirborne = bodyContact = false;
        previousPosition = body.position;
    }

    public void ReportBodyContact()
    {
        bodyContact = true;
        if (!enabled || body.isKinematic || landingTime <= 0) return;
        // Collision callbacks run after the solver. Absorb a hard impact's rebound before the next frame.
        int contacts = GetSupport(out Vector3 normal, out Vector3 surfaceVelocity);
        if (IsUprightSupport(contacts, normal)) AbsorbRebound(normal, surfaceVelocity);
    }

    public void Step(float dt)
    {
        if (!enabled || body.isKinematic || dt <= 0) return;
        // Sleeping bodies do not receive OnCollisionStay; resting on the roof is still supported contact.
        if (body.IsSleeping()) { Reset(); return; }
        if ((body.position - previousPosition).sqrMagnitude > 400) Reset();
        previousPosition = body.position;
        int contacts = GetSupport(out Vector3 normal, out Vector3 surfaceVelocity);
        float previousAirTime = airTime;
        IsAirborne = contacts == 0 && !bodyContact;
        bodyContact = false;
        if (IsAirborne)
        {
            airTime += dt;
            impactSpeed = Mathf.Max(impactSpeed, -body.linearVelocity.y);
            pendingLanding = 0;
            landingTime = 0;
            return;
        }

        airTime = 0;
        if (previousAirTime >= .12f && impactSpeed > 3) pendingLanding = .25f;
        else pendingLanding = Mathf.Max(0, pendingLanding - dt);
        bool uprightLanding = IsUprightSupport(contacts, normal);
        if (pendingLanding > 0 && uprightLanding)
        {
            landingTime = .8f;
            pendingLanding = 0;
        }
        landingTime = Mathf.Max(0, landingTime - dt);
        if (landingTime > 0 && uprightLanding) AbsorbRebound(normal, surfaceVelocity);
        if (contacts > 0 && pendingLanding == 0 && landingTime == 0) impactSpeed = 0;
    }

    private int GetSupport(out Vector3 normal, out Vector3 surfaceVelocity)
    {
        int contacts = 0;
        normal = surfaceVelocity = Vector3.zero;
        foreach (var wheel in wheels)
        {
            if (!wheel.GetGroundHit(out WheelHit hit) || hit.collider == null) continue;
            contacts++;
            normal += hit.normal;
            var platform = hit.collider.attachedRigidbody;
            if (platform != null && platform != body) surfaceVelocity += platform.GetPointVelocity(hit.point);
        }
        if (contacts > 0) { normal.Normalize(); surfaceVelocity /= contacts; }
        return contacts;
    }

    private bool IsUprightSupport(int contacts, Vector3 normal) =>
        contacts >= 3 && normal.y > .5f && Vector3.Dot(body.transform.up, normal) > .75f;

    private void AbsorbRebound(Vector3 normal, Vector3 surfaceVelocity)
    {
        // Remove only excess outward speed. Tangential momentum, gravity and free flight are unchanged.
        float outward = Vector3.Dot(body.linearVelocity - surfaceVelocity, normal);
        if (outward > .6f) body.linearVelocity -= normal * (outward - .6f);
    }

}
