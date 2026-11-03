using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.HighDefinition;

/// <summary>Camera-only HDRP night sensor. Does not emit light or edit scene exposure.</summary>
[DisallowMultipleComponent]
public sealed class StealthNightVision : MonoBehaviour
{
    [SerializeField, Min(.01f)] private float activationTime = .18f;
    private CustomPassVolume sensor;
    private Material material;
    private float weight;
    private static readonly int Strength = Shader.PropertyToID("_Strength");
    public bool IsActive => sensor != null && sensor.enabled;
    public float Weight => weight;
    private void Awake()
    {
        var shader = Resources.Load<Shader>("StealthNightVision");
        if (shader == null || !shader.isSupported)
        { Debug.LogError("Night vision requires the HDRP stealth sensor shader.", this); enabled = false; return; }
        material = new Material(shader) { name = "Runtime stealth sensor", hideFlags = HideFlags.HideAndDontSave };
        material.SetFloat(Strength, 0);
        var effect = new GameObject("Stealth night vision");
        effect.transform.SetParent(transform, false);
        sensor = effect.AddComponent<CustomPassVolume>();
        sensor.isGlobal = true; sensor.priority = 2000;
        sensor.injectionPoint = CustomPassInjectionPoint.AfterPostProcess;
        sensor.targetCamera = Camera.main;
        sensor.customPasses.Add(new SensorPass { name = "Stealth sensor", Material = material });
        sensor.enabled = false;
    }
    public void SetActive(bool active)
    {
        if (sensor == null) return;
        sensor.enabled = active && isActiveAndEnabled;
        if (!sensor.enabled) { weight = 0; material.SetFloat(Strength, 0); }
    }
    private void LateUpdate()
    {
        if (!IsActive) return;
        // Restrict the effect to the gameplay camera, including when it is recreated.
        if (sensor.targetCamera == null) sensor.targetCamera = Camera.main;
        weight = Mathf.MoveTowards(weight, 1, Time.unscaledDeltaTime / activationTime);
        material.SetFloat(Strength, weight);
    }
    [System.Serializable]
    private sealed class SensorPass : CustomPass
    {
        public Material Material;
        private RTHandle source;
        protected override bool executeInSceneView => false;
        protected override void Setup(ScriptableRenderContext context, CommandBuffer cmd)
        {
            source = RTHandles.Alloc(Vector2.one, slices: TextureXR.slices,
                dimension: TextureXR.dimension, colorFormat: GraphicsFormat.R16G16B16A16_SFloat,
                useDynamicScale: true, name: "Night vision source");
        }
        protected override void Execute(CustomPassContext ctx)
        {
            if (Material == null) return;
            HDUtils.BlitCameraTexture(ctx.cmd, ctx.cameraColorBuffer, source);
            ctx.propertyBlock.SetTexture("_SensorSource", source);
            CoreUtils.SetRenderTarget(ctx.cmd, ctx.cameraColorBuffer, ClearFlag.None);
            CoreUtils.DrawFullScreen(ctx.cmd, Material, ctx.propertyBlock, 0);
        }
        protected override void Cleanup() { source?.Release(); source = null; }
    }
    private void OnDisable() => SetActive(false);
    private void OnDestroy()
    {
        if (sensor != null) Destroy(sensor.gameObject);
        if (material != null) Destroy(material);
    }
}
