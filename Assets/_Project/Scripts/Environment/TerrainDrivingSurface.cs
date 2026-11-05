using UnityEngine;

/// <summary>Cached terrain-layer weights let visual sand, dirt, rock and gravel use their actual tire grip.</summary>
[RequireComponent(typeof(Terrain))]
public sealed class TerrainDrivingSurface : MonoBehaviour
{
    [SerializeField] private Vector2[] layerGrip = { new Vector2(.78f,.68f), new Vector2(.67f,.55f), new Vector2(.92f,.85f), new Vector2(.54f,.46f) };
    private Terrain terrain;
    private float[,,] weights;
    private void Awake()
    {
        terrain = GetComponent<Terrain>();
        var d = terrain.terrainData;
        weights = d.GetAlphamaps(0,0,d.alphamapWidth,d.alphamapHeight);
    }
    public Vector2 GripAt(Vector3 worldPosition)
    {
        if (weights == null) return Vector2.one;
        var d = terrain.terrainData;
        Vector3 p = worldPosition - terrain.transform.position;
        int x = Mathf.Clamp(Mathf.RoundToInt(p.x / d.size.x * (weights.GetLength(1)-1)),0,weights.GetLength(1)-1);
        int z = Mathf.Clamp(Mathf.RoundToInt(p.z / d.size.z * (weights.GetLength(0)-1)),0,weights.GetLength(0)-1);
        Vector2 grip = Vector2.zero;
        for (int i=0;i<weights.GetLength(2);i++) grip += (i<layerGrip.Length ? layerGrip[i] : Vector2.one) * weights[z,x,i];
        return grip;
    }
}
