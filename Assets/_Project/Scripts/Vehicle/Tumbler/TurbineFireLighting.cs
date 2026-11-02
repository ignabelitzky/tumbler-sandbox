using UnityEngine;

/// <summary>Two bounded warm lights follow actual flame particles, including their fade-out.</summary>
[DefaultExecutionOrder(150)]
public sealed class TurbineFireLighting : MonoBehaviour
{
    [SerializeField] private ParticleSystem idleFlame;
    [SerializeField] private ParticleSystem turboFlame;
    [SerializeField] private Light idleLight;
    [SerializeField] private Light turboLight;
    [SerializeField] private TumblerSystems systems;
    [SerializeField, Min(0)] private float idleLumens = 450;
    [SerializeField, Min(0)] private float turboLumens = 2400;
    public Light IdleLight => idleLight;
    public Light TurboLight => turboLight;
    public ParticleSystem IdleFlame => idleFlame;
    public ParticleSystem TurboFlame => turboFlame;

    private void Awake()
    {
        if (systems == null) systems = GetComponentInParent<TumblerSystems>();
        Dark();
    }
    private void OnEnable() => Dark();
    private void OnDisable() => Dark();
    private void LateUpdate()
    {
        if (!isActiveAndEnabled || (systems != null && systems.IsStealth)) { Dark(); return; }
        if (GameNavigation.IsPaused) return;
        Refresh(idleFlame, idleLight, idleLumens, 3.6f, 1.1f);
        Refresh(turboFlame, turboLight, turboLumens, 7f, 4.7f);
    }
    private static float FlameStrength(ParticleSystem flame)
    {
        // Smoke/embers are deliberately not sources. A stopped emitter can still
        // contain visible flame particles, so its remaining tail keeps contributing.
        if (flame == null || !flame.gameObject.activeInHierarchy || flame.particleCount == 0) return 0;
        float expected = Mathf.Max(1, flame.emission.rateOverTime.constantMax * flame.main.startLifetime.constantMax * .6f);
        return Mathf.Clamp01(flame.particleCount / expected);
    }
    private static void Refresh(ParticleSystem flame, Light light, float lumens, float range, float seed)
    {
        if (light == null) return;
        float strength = FlameStrength(flame);
        if (strength <= 0) { light.intensity = 0; light.enabled = false; return; }
        float flicker = Mathf.Lerp(.9f, 1.1f, Mathf.PerlinNoise(seed, Time.time * 12));
        float target = lumens * strength * flicker;
        light.intensity = Mathf.Lerp(light.intensity, target, 1 - Mathf.Exp(-18 * Time.deltaTime));
        light.range = range;
        light.enabled = light.intensity > .01f;
    }
    private void Dark()
    {
        if (idleLight != null) { idleLight.intensity = 0; idleLight.enabled = false; }
        if (turboLight != null) { turboLight.intensity = 0; turboLight.enabled = false; }
    }
}
