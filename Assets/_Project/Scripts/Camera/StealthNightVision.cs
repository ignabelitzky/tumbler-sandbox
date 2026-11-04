using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.HighDefinition;

/// <summary>Camera-only HDRP blue surface sonar. Does not emit light or edit scene exposure.</summary>
[DisallowMultipleComponent]
public sealed class StealthNightVision : MonoBehaviour
{
    [SerializeField, Min(.01f)] private float activationTime = .18f;
    private CustomPassVolume sensor;
    private Material material, vehicleMaskMaterial;
    private float weight;
    private static readonly int Origin = Shader.PropertyToID("_SonarOrigin");
    private static readonly int Clock = Shader.PropertyToID("_SonarTime");
    private float elapsed;
    private static readonly int Strength = Shader.PropertyToID("_Strength");
    public bool IsActive => sensor != null && sensor.enabled;
    public float Weight => weight;
    private void Awake()
    {
        var shader = Resources.Load<Shader>("StealthNightVision");
        if (shader == null || !shader.isSupported)
        { Debug.LogError("Night vision requires the HDRP stealth sensor shader.", this); enabled = false; return; }
        var maskShader = Resources.Load<Shader>("StealthVehicleMask");
        if (maskShader == null || !maskShader.isSupported)
        { Debug.LogError("Night sonar requires the vehicle mask shader.", this); enabled = false; return; }
        vehicleMaskMaterial = new Material(maskShader) { name = "Runtime sonar vehicle mask", hideFlags = HideFlags.HideAndDontSave };
        material = new Material(shader) { name = "Runtime stealth sensor", hideFlags = HideFlags.HideAndDontSave };
        material.SetFloat(Strength, 0);
        var effect = new GameObject("Stealth night vision");
        effect.transform.SetParent(transform, false);
        sensor = effect.AddComponent<CustomPassVolume>();
        sensor.isGlobal = true; sensor.priority = 2000;
        sensor.injectionPoint = CustomPassInjectionPoint.AfterPostProcess;
        sensor.targetCamera = Camera.main;
        sensor.customPasses.Add(new SensorPass { name = "Stealth sensor", Material = material, MaskMaterial = vehicleMaskMaterial, Vehicle = transform });
        sensor.enabled = false;
    }
    public void SetActive(bool active)
    {
        if (sensor == null) return;
        bool wasActive = sensor.enabled;
        sensor.enabled = active && isActiveAndEnabled;
        if (sensor.enabled && !wasActive) elapsed = 0;
        if (!sensor.enabled) { weight = 0; material.SetFloat(Strength, 0); }
    }
    private void LateUpdate()
    {
        if (!IsActive) return;
        // Restrict the effect to the gameplay camera, including when it is recreated.
        sensor.targetCamera = Camera.main;
        weight = Mathf.MoveTowards(weight, 1, Time.unscaledDeltaTime / activationTime);
        material.SetFloat(Strength, weight);
        elapsed += Time.deltaTime;
        material.SetFloat(Clock, elapsed);
        material.SetVector(Origin, transform.position);
    }
    [System.Serializable]
    private sealed class SensorPass : CustomPass
    {
        public Material Material, MaskMaterial;
        public Transform Vehicle;
        private Renderer[] vehicleRenderers;
        private int[] submeshCounts;
        private RTHandle source, vehicleMask;
        protected override bool executeInSceneView => false;
        protected override void Setup(ScriptableRenderContext context, CommandBuffer cmd)
        {
            vehicleRenderers = Vehicle.GetComponentsInChildren<Renderer>(true);
            submeshCounts = new int[vehicleRenderers.Length];
            for (int i = 0; i < vehicleRenderers.Length; i++)
                if (vehicleRenderers[i] is MeshRenderer || vehicleRenderers[i] is SkinnedMeshRenderer)
                    submeshCounts[i] = vehicleRenderers[i].sharedMaterials.Length;
            vehicleMask = RTHandles.Alloc(Vector2.one, slices: TextureXR.slices,
                dimension: TextureXR.dimension, colorFormat: GraphicsFormat.R8_UNorm,
                useDynamicScale: true, name: "Sonar vehicle mask");
            source = RTHandles.Alloc(Vector2.one, slices: TextureXR.slices,
                dimension: TextureXR.dimension, colorFormat: GraphicsFormat.R16G16B16A16_SFloat,
                useDynamicScale: true, name: "Night vision source");
        }
        protected override void Execute(CustomPassContext ctx)
        {
            if (Material == null || ctx.hdCamera.camera != Camera.main) return;
            // Draw only visible vehicle geometry against the scene depth: no layer changes,
            // bounding-box holes in the environment, or masks through nearby obstacles.
            CoreUtils.SetRenderTarget(ctx.cmd, vehicleMask, ctx.cameraDepthBuffer, ClearFlag.Color, Color.clear);
            for (int i = 0; i < vehicleRenderers.Length; i++)
            {
                var renderer = vehicleRenderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.forceRenderingOff) continue;
                for (int submesh = 0; submesh < submeshCounts[i]; submesh++)
                    ctx.cmd.DrawRenderer(renderer, MaskMaterial, submesh, 0);
            }
            HDUtils.BlitCameraTexture(ctx.cmd, ctx.cameraColorBuffer, source);
            ctx.propertyBlock.SetTexture("_SensorSource", source);
            ctx.propertyBlock.SetTexture("_VehicleMask", vehicleMask);
            CoreUtils.SetRenderTarget(ctx.cmd, ctx.cameraColorBuffer, ClearFlag.None);
            CoreUtils.DrawFullScreen(ctx.cmd, Material, ctx.propertyBlock, 0);
        }
        protected override void Cleanup() { source?.Release(); source = null; vehicleMask?.Release(); vehicleMask = null; }
    }
    private void OnDisable() => SetActive(false);
    private void OnDestroy()
    {
        if (sensor != null) Destroy(sensor.gameObject);
        if (material != null) Destroy(material);
        if (vehicleMaskMaterial != null) Destroy(vehicleMaskMaterial);
    }
}
