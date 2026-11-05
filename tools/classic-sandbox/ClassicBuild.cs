using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// Explicit authoring in a disposable checkout. Gameplay loads the resulting prefab.
public static partial class ClassicSandbox
{
    public const string Folder = "Assets/_Project/Environment/ClassicSandbox";
    public const string Results = "TestResults/ClassicSandbox";
    const string Legacy = "Assets/_Project/Environment/OrganicSandbox/";
    static GameObject root;
    static readonly List<OrganicSandbox.Route> routes = new List<OrganicSandbox.Route>();
    static Material asphalt, gravel, dirt, sand, rock, paint;
    static TerrainData terrainData;
    [Serializable] public class Generated { public string prefab_guid; public long root_id, grip_id; }

    static bool rebuild;
    public static void Rebuild() { rebuild=true; Build(); }

    public static void Build()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity");
            if (AssetDatabase.IsValidFolder(Folder) && !rebuild) throw new Exception("Classic assets already exist; use Rebuild only in a disposable checkout.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Project/Environment", "ClassicSandbox");
            asphalt = Material("Weathered asphalt"); gravel = Material("Crushed limestone"); dirt = Material("Ochre earth");
            sand = Material("Dune sand"); rock = Material("Weathered sandstone"); paint = Material("Warm edge paint");
            root = new GameObject("Classic flowing proving grounds"); routes.Clear();
            BuildRoads(); BuildSuspensionTrials(); BuildRamps(); BuildTerrain(); BuildScenery();
            var grip = root.AddComponent<WheelSurfaceGrip>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Folder + "/ClassicSandbox.prefab");
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefab, out string guid, out long rootId);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefab.GetComponent<WheelSurfaceGrip>(), out string unused, out long gripId);
            Directory.CreateDirectory(Results);
            File.WriteAllText(Results + "/generated.json", JsonUtility.ToJson(new Generated { prefab_guid = guid, root_id = rootId, grip_id = gripId }, true));
            File.WriteAllText(Folder + "/Routes.json", JsonUtility.ToJson(new OrganicSandbox.RouteFile { routes = routes.ToArray() }, true));
            AssetDatabase.SaveAssets();
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static Material Material(string name) => AssetDatabase.LoadAssetAtPath<Material>(Legacy + name + ".mat");
    static GameObject Group(string name) { var g = new GameObject(name); g.transform.SetParent(root.transform, false); return g; }
    static float Smooth(float value) { value = Mathf.Clamp01(value); return value * value * value * (value * (value * 6 - 15) + 10); }
    static float Rect(float x, float z, float cx, float cz, float hx, float hz, float blend = 14) =>
        1 - Smooth(Mathf.Max((Mathf.Abs(x - cx) - hx) / blend, (Mathf.Abs(z - cz) - hz) / blend));
    static GameObject MeshObject(string name, Mesh mesh, Material material, GameObject parent, bool solid = true)
    {
        string path=Folder + "/" + name + ".asset";
        var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(saved==null) AssetDatabase.CreateAsset(mesh,path);
        else { EditorUtility.CopySerialized(mesh,saved); Object.DestroyImmediate(mesh); mesh=saved; EditorUtility.SetDirty(saved); }
        var g = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); g.transform.SetParent(parent.transform, false);
        g.GetComponent<MeshFilter>().sharedMesh = mesh; g.GetComponent<MeshRenderer>().sharedMaterial = material;
        if (solid) g.AddComponent<MeshCollider>().sharedMesh = mesh;
        g.isStatic = true; return g;
    }
    static Mesh Grid(string name, int rows, int columns, Func<int, int, Vector3> point)
    {
        var mesh = new OrganicSandbox.MeshMaker();
        for (int i = 0; i < rows - 1; i++) for (int j = 0; j < columns - 1; j++)
            mesh.Quad(point(i, j), point(i + 1, j), point(i + 1, j + 1), point(i, j + 1), Vector3.up);
        return mesh.Build(name);
    }
    static OrganicSandbox.Route Route(string name, int steps, float width, Func<float, Vector3> position)
    {
        var r = new OrganicSandbox.Route { name = name, width = width, points = new Vector3[steps + 1], up = new Vector3[steps + 1] };
        for (int i = 0; i <= steps; i++) { r.points[i] = position(i / (float)steps); r.up[i] = Vector3.up; }
        routes.Add(r); return r;
    }
    static void Ribbon(OrganicSandbox.Route r, Material material, GameObject parent, Vector2 grip, Func<int, float, float> uneven = null)
    {
        int columns = uneven == null ? 5 : 17;
        var mesh = Grid(r.name, r.points.Length, columns, (i, j) =>
        {
            float x = (j / (float)(columns - 1) - .5f) * r.width;
            return r.points[i] + r.Right(i) * x + Vector3.up * (uneven == null ? 0 : uneven(i, x));
        });
        var g = MeshObject(r.name, mesh, material, parent); g.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        var surface = g.AddComponent<DrivingSurface>(); surface.forwardGrip = grip.x; surface.sidewaysGrip = grip.y;
        var shoulder = new OrganicSandbox.MeshMaker();
        for (int i = 0; i < r.points.Length - 1; i++) foreach (int side in new[] { -1, 1 })
        {
            var a = r.points[i] + r.Right(i) * (r.width * .5f * side); var b = r.points[i + 1] + r.Right(i + 1) * (r.width * .5f * side);
            shoulder.Quad(a, b, b + r.Right(i + 1) * (2.5f * side) - Vector3.up * .4f, a + r.Right(i) * (2.5f * side) - Vector3.up * .4f, Vector3.up);
        }
        MeshObject(r.name + " blended shoulders", shoulder.Build(r.name + " shoulders"), gravel, parent);
    }
    static Vector3 Along(Vector3 start, float heading, float length, float t) => start + Quaternion.Euler(0, heading, 0) * Vector3.forward * (length * t);

    static void BuildRoads()
    {
        var group = Group("Flowing access roads");
        var controls = new[] { new Vector3(0,.12f,50), new Vector3(-70,.25f,130), new Vector3(-210,.5f,170),
            new Vector3(-315,1.2f,100), new Vector3(-340,1.5f,-40), new Vector3(-275,.6f,-165),
            new Vector3(-140,.2f,-200), new Vector3(-40,.12f,-115), new Vector3(-10,.12f,5) };
        Ribbon(Route("Open handling circuit", 1600, 18, t => OrganicSandbox.Cat(controls,t,true)), asphalt, group, Vector2.one);
        Ribbon(Route("Tunnel west approach", 240, 18, t => OrganicSandbox.Cat(new[] {new Vector3(0,.12f,40),new Vector3(55,.12f,35),new Vector3(100,.12f,5),new Vector3(100,0,-40)},t)), asphalt, group, Vector2.one);
        Ribbon(Route("Tunnel east return", 400, 18, t => OrganicSandbox.Cat(new[] {new Vector3(242,0,-40),new Vector3(242,.12f,20),new Vector3(195,.12f,70),new Vector3(90,.12f,70),new Vector3(0,.12f,40)},t)), asphalt, group, Vector2.one);
        Ribbon(Route("North ramp access", 300, 17, t => OrganicSandbox.Cat(new[] {new Vector3(-70,.25f,130),new Vector3(-100,.25f,185),new Vector3(-110,.25f,230),new Vector3(-160,.25f,230),new Vector3(-240,.25f,220),new Vector3(-315,1.2f,100)},t)), gravel, group, new Vector2(.85f,.8f));
        Ribbon(Route("West ramp approach",260,17,t=>OrganicSandbox.Cat(new[]{new Vector3(-275,.6f,-165),new Vector3(-350,.14f,-175),new Vector3(-390,.14f,-120)},t)),gravel,group,new Vector2(.85f,.8f));
        // A broad, rounded spawn apron blends into both tunnel approaches.
        var verts = new List<Vector3> { new Vector3(0,.11f,40) }; var tri = new List<int>(); var uv = new List<Vector2> {Vector2.zero};
        for (int i = 0; i < 80; i++) { float a = i * Mathf.PI * 2 / 80; var p = new Vector3(Mathf.Cos(a)*36,.11f,40+Mathf.Sin(a)*29); verts.Add(p); uv.Add(new Vector2(p.x,p.z)*.18f); }
        for (int i = 0; i < 80; i++) tri.AddRange(new[]{0,(i+1)%80+1,i+1});
        var apron = new Mesh{name="Rounded spawn apron"}; apron.SetVertices(verts); apron.SetUVs(0,uv); apron.SetTriangles(tri,0); apron.RecalculateNormals(); apron.RecalculateBounds();
        MeshObject("Rounded spawn apron",apron,asphalt,group).AddComponent<DrivingSurface>();
    }
    static float Bump(float distance, float center, float length, float height)
    {
        float t = (distance - center) / (length * .5f);
        return Mathf.Abs(t) >= 1 ? 0 : height * .5f * (1 + Mathf.Cos(Mathf.PI*t));
    }
    static void BuildSuspensionTrials()
    {
        var group = Group("Speed bumps and uneven ground");
        var bumps = Route("Progressive speed bumps", 400, 15, t => {
            var p = Along(new Vector3(-40,.14f,85),-108,100,t); float d=t*100;
            p.y += Bump(d,22,5,.16f)+Bump(d,41,6,.24f)+Bump(d,62,7,.32f)+Bump(d,83,8,.42f); return p; });
        Ribbon(bumps,asphalt,group,Vector2.one);
        var marks = new OrganicSandbox.MeshMaker();
        foreach(float d in new[]{22f,41f,62f,83f})
        {
            int i=Mathf.RoundToInt(d/100*400);var p=bumps.points[i]+Vector3.up*.02f;var f=bumps.Forward(i);var right=bumps.Right(i);
            foreach(int side in new[]{-1,1}) marks.Quad(p+right*(side*6)-f*.22f,p+right*(side*6)+f*.22f,p+right*(side*.6f)+f*.22f,p+right*(side*.6f)-f*.22f,Vector3.up);
        }
        MeshObject("Flush bump markings",marks.Build("Flush bump markings"),paint,group,false);
        var articulation = Route("Alternating wheel articulation", 360, 14, t => {
            var p=Along(new Vector3(-245,.14f,-115),70,90,t); p.y+=.02f; return p; });
        Ribbon(articulation,gravel,group,new Vector2(.78f,.68f),(i,x)=>{
            float t=i/360f;float edge=1-Smooth((Mathf.Abs(x)-4.5f)/2.5f);
            float gate=Smooth(t/.13f)*Smooth((1-t)/.13f);
            return .24f*gate*edge*Mathf.Sin(t*Mathf.PI*16+(x<0?Mathf.PI:0))*Smooth(Mathf.Abs(x)/1.1f); });
        var ripples = Route("Rolling gravel and shallow hollows", 420, 17, t => {
            var p=Along(new Vector3(-270,.14f,35),25,105,t);p.y+=.24f*Mathf.Pow(Mathf.Sin(t*Mathf.PI*7),2)*Smooth(t/.12f)*Smooth((1-t)/.12f);return p; });
        Ribbon(ripples,gravel,group,new Vector2(.78f,.68f));
        Ribbon(Route("Dirt meander",600,16,t=>OrganicSandbox.Cat(new[]{new Vector3(-310,1.2f,105),new Vector3(-350,2.1f,180),new Vector3(-320,3.6f,245),new Vector3(-235,2.2f,280),new Vector3(-165,.25f,230)},t)),dirt,group,new Vector2(.67f,.55f));
        Ribbon(Route("Sand handling curve",400,21,t=>OrganicSandbox.Cat(new[]{new Vector3(-280,.3f,-165),new Vector3(-330,.6f,-230),new Vector3(-265,1,-295),new Vector3(-170,.5f,-275),new Vector3(-140,.2f,-200)},t)),sand,group,new Vector2(.54f,.46f));
    }
    static void BuildRamps()
    {
        var group = Group("Classic ramps and open landings");
        foreach(var spec in new[]{new Vector4(-110,230,0,2.3f),new Vector4(-390,-120,12,4.2f)})
        {
            string name=spec.w<3?"Low rolling tabletop":"Medium rolling tabletop";
            var route=Route(name,480,17,t=>{
                var p=Along(new Vector3(spec.x,.14f,spec.y),spec.z,120,t);float d=t*120;
                p.y+=spec.w*Smooth((d-22)/28)*(1-Smooth((d-70)/32));return p;});
            Ribbon(route,asphalt,group,Vector2.one);
        }
        RestoreLegacyObstacle("ramp_45",new Vector3(-282,.13f,-48),-15,group);
        RestoreLegacyObstacle("curved_ramp",new Vector3(-202,.13f,82),35,group);
        RestoreLegacyObstacle("square_ramp",new Vector3(-102,.13f,-124),-28,group);
        RestoreLegacyObstacle("circular_bump",new Vector3(-82,.13f,15),18,group);
        RestoreLegacyObstacle("triangular_bump",new Vector3(-108,.13f,8),-12,group);
    }
    static void RestoreLegacyObstacle(string name, Vector3 position, float yaw, GameObject parent)
    {
        var original=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(t=>t.name==name && t.gameObject.scene.IsValid());
        var copy=Object.Instantiate(original.gameObject);copy.name="Original "+name;copy.transform.SetParent(parent.transform,true);
        copy.transform.rotation=Quaternion.Euler(0,yaw,0)*original.rotation;copy.transform.position=position;copy.SetActive(true);
        foreach(var rb in copy.GetComponentsInChildren<Rigidbody>(true))Object.DestroyImmediate(rb);
        foreach(var behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true))Object.DestroyImmediate(behaviour);
        var renderers=copy.GetComponentsInChildren<Renderer>(true);foreach(var renderer in renderers)renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>asphalt).ToArray();
        foreach(var filter in copy.GetComponentsInChildren<MeshFilter>(true))
            if(filter.sharedMesh!=null && filter.GetComponent<Collider>()==null)filter.gameObject.AddComponent<MeshCollider>().sharedMesh=filter.sharedMesh;
        Physics.SyncTransforms();var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
        copy.transform.position+=Vector3.up*(position.y-bounds.min.y);
        foreach(var collider in copy.GetComponentsInChildren<Collider>(true))collider.enabled=true;
        copy.AddComponent<DrivingSurface>();copy.isStatic=true;
    }
}
