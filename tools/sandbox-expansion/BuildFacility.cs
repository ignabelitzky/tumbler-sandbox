using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;

public static partial class ExpansionAuthoring
{
    public const string Folder = "Assets/_Project/Environment/UndergroundFacility";
    public const string PrefabPath = Folder + "/UndergroundFacility.prefab";
    public static readonly List<Vector3> Route = new List<Vector3>();
    const float Depth = -12f;
    static Material concrete, asphalt, steel, paint, amber, lamp;
    static GameObject root, structure, details;
    static MeshBuilder floor, shell, trims;

    public static void Build()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity");
            if (AssetDatabase.IsValidFolder(Folder)) throw new Exception("Generated folder already exists; inspect it before rebuilding.");
            EnsureFolder(Folder);
            concrete = Material("Concrete", new Color(.24f,.26f,.28f), .08f);
            asphalt = Material("Road", new Color(.075f,.085f,.10f), .03f);
            steel = Material("Steel", new Color(.14f,.19f,.22f), .35f);
            paint = Material("LanePaint", new Color(.8f,.72f,.35f), .08f);
            amber = Material("SafetyAmber", new Color(.95f,.49f,.07f), .1f);
            lamp = Material("WarmDiffuser", new Color(1,.73f,.43f), .2f, new Color(220,135,55));
            root = new GameObject("Underground Light Lab and Perimeter");
            structure = Child("Tunnel Structure", root);
            details = Child("Road Markings and Safety Details", root);
            floor = new MeshBuilder(); shell = new MeshBuilder(); trims = new MeshBuilder();
            CreateRoute();
            BuildTunnel();
            MeshObject("Continuous tunnel road", floor, asphalt, structure, true);
            MeshObject("Tunnel walls and closed roof", shell, concrete, structure, true);
            MeshObject("Lane lines and wall ribs", trims, paint, details, false);
            BuildBarriers();
            BuildSigns();
            BuildGuidance();
            var camera = root.AddComponent<SandboxCameraModes>();
            BuildZones(camera);
            CreateTerrainHoles();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefab, out string prefabGuid, out long rootId);
            var terrain = AssetDatabase.LoadAssetAtPath<TerrainData>(Folder + "/TunnelTerrain.asset");
            string terrainGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(terrain));
            Directory.CreateDirectory("TestResults/SandboxExpansion");
            File.WriteAllText("TestResults/SandboxExpansion/generated.json",
                "{\"prefab_guid\":\"" + prefabGuid + "\",\"root_id\":" + rootId +
                ",\"terrain_guid\":\"" + terrainGuid + "\",\"route_samples\":" + Route.Count + "}");
            File.WriteAllText(Folder + "/Route.json", JsonUtility.ToJson(new RouteData { points=Route.ToArray() }, true));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    [Serializable] public class RouteData { public Vector3[] points; }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0,slash));
        AssetDatabase.CreateFolder(path.Substring(0,slash),path.Substring(slash+1));
    }
    static Material Material(string name, Color color, float smooth, Color? emission=null)
    {
        var m = new Material(Shader.Find("HDRP/Lit")) { name = name, enableInstancing = true };
        m.SetColor("_BaseColor", color); m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_DoubleSidedEnable",1); m.SetFloat("_CullMode",0); m.SetFloat("_CullModeForward",0);
        if (emission.HasValue) m.SetColor("_EmissiveColor", emission.Value);
        AssetDatabase.CreateAsset(m,Folder+"/"+name+".mat");
        return m;
    }
    static GameObject Child(string name, GameObject parent)
    {
        var g = new GameObject(name); g.transform.SetParent(parent.transform,false); return g;
    }
    static void Line(Vector3 a, Vector3 b, int count)
    {
        for(int i=Route.Count==0?0:1;i<=count;i++) Route.Add(Vector3.Lerp(a,b,i/(float)count));
    }
    public static void CreateRoute()
    {
        Route.Clear();
        for(int i=0;i<=45;i++)
        {
            float t=i/45f;
            Route.Add(new Vector3(100,Depth*Mathf.SmoothStep(0,1,t),-40-90*t));
        }
        Line(new Vector3(100,Depth,-130),new Vector3(100,Depth,-180),25);
        for(int i=1;i<=28;i++)
        {
            float a=Mathf.PI + Mathf.PI*.5f*i/28;
            Route.Add(new Vector3(132+32*Mathf.Cos(a),Depth,-180+32*Mathf.Sin(a)));
        }
        Line(new Vector3(132,Depth,-212),new Vector3(210,Depth,-212),39);
        for(int i=1;i<=28;i++)
        {
            float a=-Mathf.PI*.5f+Mathf.PI*.5f*i/28;
            Route.Add(new Vector3(210+32*Mathf.Cos(a),Depth,-180+32*Mathf.Sin(a)));
        }
        Line(new Vector3(242,Depth,-180),new Vector3(242,Depth,-130),25);
        for(int i=1;i<=45;i++)
        {
            float t=i/45f;
            Route.Add(new Vector3(242,Depth*(1-Mathf.SmoothStep(0,1,t)),-130+90*t));
        }
    }
    public static Vector3 Right(int i)
    {
        Vector3 forward=Route[Mathf.Min(i+1,Route.Count-1)]-Route[Mathf.Max(i-1,0)];
        return Vector3.Cross(Vector3.up,forward).normalized;
    }
    static void BuildTunnel()
    {
        var fixtures=Child("Dim tunnel lighting",root);
        for(int i=0;i<Route.Count-1;i++)
        {
            Vector3 a=Route[i]+Vector3.up*.04f,b=Route[i+1]+Vector3.up*.04f;
            Vector3 ra=Right(i),rb=Right(i+1);
            floor.Quad(a-ra*10,b-rb*10,b+rb*10,a+ra*10,Vector3.up);
            bool covered=a.y < -7.2f && b.y < -7.2f;
            foreach(int sign in new[]{-1,1})
            {
                float ha=covered?4.2f:Mathf.Max(4.2f,.7f-a.y);
                float hb=covered?4.2f:Mathf.Max(4.2f,.7f-b.y);
                Vector3 aa=a+ra*(7*sign),bb=b+rb*(7*sign);
                shell.Quad(aa,aa+Vector3.up*ha,bb+Vector3.up*hb,bb,-ra*sign);
                // Thick retaining walls and caps close off the trench and cast sunlight shadows.
                Vector3 ao=a+ra*(10*sign),bo=b+rb*(10*sign);
                shell.Quad(aa+Vector3.up*ha,ao+Vector3.up*ha,bo+Vector3.up*hb,bb+Vector3.up*hb,Vector3.up);
                shell.Quad(ao,bo,bo+Vector3.up*hb,ao+Vector3.up*ha,ra*sign);
            }
            if(covered)
            {
                var cross=new[]{new Vector2(-7,4.2f),new Vector2(-6.5f,5.2f),new Vector2(-5.2f,6),new Vector2(0,6.5f),new Vector2(5.2f,6),new Vector2(6.5f,5.2f),new Vector2(7,4.2f)};
                for(int k=0;k<cross.Length-1;k++)
                {
                    Vector3 p=a+ra*cross[k].x+Vector3.up*cross[k].y;
                    Vector3 q=a+ra*cross[k+1].x+Vector3.up*cross[k+1].y;
                    Vector3 r=b+rb*cross[k+1].x+Vector3.up*cross[k+1].y;
                    Vector3 s=b+rb*cross[k].x+Vector3.up*cross[k].y;
                    shell.Quad(p,q,r,s,Vector3.down-ra*(cross[k].x*.1f));
                    shell.Quad(p+Vector3.up*.7f,s+Vector3.up*.7f,r+Vector3.up*.7f,q+Vector3.up*.7f,Vector3.up);
                }
                if(i%9==0) Fixture(fixtures,(a+b)*.5f,ra,(i/9)%2==0?1:-1);
            }
            if(i%3!=2) trims.Quad(a-ra*.07f+Vector3.up*.01f,b-rb*.07f+Vector3.up*.01f,b+rb*.07f+Vector3.up*.01f,a+ra*.07f+Vector3.up*.01f,Vector3.up);
            foreach(int sign in new[]{-1,1})
            {
                trims.Quad(a+ra*(6.45f*sign)+Vector3.up*.01f,b+rb*(6.45f*sign)+Vector3.up*.01f,b+rb*(6.56f*sign)+Vector3.up*.01f,a+ra*(6.56f*sign)+Vector3.up*.01f,Vector3.up);
                if(i%4==0 && covered)
                {
                    Quaternion rot=Quaternion.LookRotation(b-a,Vector3.up);
                    trims.Box(a+ra*(6.92f*sign)+Vector3.up*2.1f,new Vector3(.10f,4.2f,.22f),rot);
                }
            }
        }
    }
    static void Fixture(GameObject parent,Vector3 p,Vector3 right,int side)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name="Recessed warm guide lamp";
        g.transform.SetParent(parent.transform);g.transform.position=p+right*(5.6f*side)+Vector3.up*5.1f;
        g.transform.localScale=new Vector3(.65f,.12f,1.4f);g.GetComponent<Renderer>().sharedMaterial=lamp;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        var light=new GameObject("Dim warm pool");light.transform.SetParent(parent.transform);
        light.transform.position=g.transform.position-Vector3.up*.15f;
        light.transform.rotation=Quaternion.LookRotation(Vector3.down-right*(side*.25f));
        var l=light.AddComponent<Light>();l.type=LightType.Spot;l.spotAngle=100;l.innerSpotAngle=65;
        l.range=10;l.color=new Color(1,.72f,.44f);l.intensity=900;l.shadows=LightShadows.None;
        light.AddComponent<HDAdditionalLightData>();
    }
    static void MeshObject(string name,MeshBuilder builder,Material material,GameObject parent,bool collider)
    {
        var mesh=builder.Build(name);AssetDatabase.CreateAsset(mesh,Folder+"/"+name+".asset");
        var g=Child(name,parent);g.AddComponent<MeshFilter>().sharedMesh=mesh;
        var r=g.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.TwoSided;
        if(collider)g.AddComponent<MeshCollider>().sharedMesh=mesh;
        g.isStatic=true;
    }
    static void BuildBarriers()
    {
        var g=Child("Platform perimeter containment",root);var baseMesh=new MeshBuilder();var railMesh=new MeshBuilder();
        foreach(int sign in new[]{-1,1})
        {
            baseMesh.Box(new Vector3(0,.35f,498*sign),new Vector3(996,.7f,.7f),Quaternion.identity);
            baseMesh.Box(new Vector3(498*sign,.35f,0),new Vector3(.7f,.7f,996),Quaternion.identity);
            foreach(float y in new[]{1.10f,1.85f})
            {
                railMesh.Box(new Vector3(0,y,498*sign),new Vector3(996,.24f,.22f),Quaternion.identity);
                railMesh.Box(new Vector3(498*sign,y,0),new Vector3(.22f,.24f,996),Quaternion.identity);
            }
            for(float pos=-496;pos<=496;pos+=8)
            {
                railMesh.Box(new Vector3(pos,1.25f,498*sign),new Vector3(.18f,1.8f,.22f),Quaternion.identity);
                railMesh.Box(new Vector3(498*sign,1.25f,pos),new Vector3(.22f,1.8f,.18f),Quaternion.identity);
            }
            foreach(bool east in new[]{false,true})
            {
                var side=Child("Continuous containment "+(east?"X":"Z")+sign,g);
                var c=side.AddComponent<BoxCollider>();
                c.center=east?new Vector3(498*sign,1.1f,0):new Vector3(0,1.1f,498*sign);
                c.size=east?new Vector3(.7f,2.2f,996.7f):new Vector3(996.7f,2.2f,.7f);
            }
        }
        MeshObject("Containment concrete plinths",baseMesh,concrete,g,false);
        MeshObject("Containment steel rails and posts",railMesh,steel,g,false);
    }
    static void Sign(string title,string subtitle,Vector3 position,Vector3 heading)
    {
        var g=Child(title,details);g.transform.SetPositionAndRotation(position,Quaternion.LookRotation(heading));
        var panel=GameObject.CreatePrimitive(PrimitiveType.Cube);panel.name="Sign back";panel.transform.SetParent(g.transform,false);
        panel.transform.localPosition=new Vector3(0,0,.12f);panel.transform.localScale=new Vector3(13,1.7f,.18f);
        panel.GetComponent<Renderer>().sharedMaterial=steel;Object.DestroyImmediate(panel.GetComponent<Collider>());
        var text=Child("Legible sign",g).AddComponent<TextMeshPro>();text.text=title+"\n"+subtitle;
        text.font=TMP_Settings.defaultFontAsset;text.fontSize=3.1f;text.color=new Color(1,.83f,.42f);
        text.alignment=TextAlignmentOptions.Center;text.rectTransform.sizeDelta=new Vector2(12.5f,1.55f);
        text.renderer.sharedMaterial=text.font.material;
    }
    static void BuildSigns()
    {
        Sign("UNDERGROUND LIGHT LAB","DIM LIGHTS / TWO 90-DEGREE BENDS / 40 KM/H",new Vector3(100,6.6f,-40),Vector3.back);
        Sign("LIGHT LAB / REVERSE ENTRY","CAMERA AUTO CLOSE / C OR D-PAD DOWN",new Vector3(242,6.6f,-40),Vector3.back);
        foreach(float x in new[]{100f,242f})
        {
            var posts=new MeshBuilder();
            foreach(int side in new[]{-1,1}) posts.Box(new Vector3(x+side*7.3f,3.2f,-40),new Vector3(.3f,6.4f,.3f),Quaternion.identity);
            MeshObject("Portal posts "+x,posts,steel,details,true);
        }
        Sign("TUNNEL TEST ROUTE","FOLLOW AMBER ARROWS / HEADLIGHTS L / STEALTH V",new Vector3(38,3.6f,25),Vector3.right);
        var arrows=new MeshBuilder();
        foreach(var item in new[]{(new Vector3(35,.06f,35),Vector3.right),(new Vector3(70,.06f,35),Vector3.right),(new Vector3(100,.06f,10),Vector3.back),(new Vector3(100,.06f,-15),Vector3.back)})
        {
            Vector3 p=item.Item1,f=item.Item2,r=Vector3.Cross(Vector3.up,f);
            arrows.Quad(p-r*.4f-f*2,p-r*.4f+f,p+r*.4f+f,p+r*.4f-f*2,Vector3.up);
            arrows.Triangle(p-r*1.5f+f,p+f*3,p+r*1.5f+f,Vector3.up);
        }
        MeshObject("Surface route arrows",arrows,amber,details,false);
    }
    static void BuildGuidance()
    {
        var guides=new MeshBuilder();
        for(int i=0;i<Route.Count-1;i+=4)
        {
            if(Route[i].y > -7.2f)continue;
            Vector3 p=Route[i],r=Right(i),f=Vector3.Cross(r,Vector3.up);
            foreach(int side in new[]{-1,1})
            {
                Vector3 c=p+r*(6.97f*side)+Vector3.up*1.25f;
                guides.Quad(c-f*.8f,c-f*.8f+Vector3.up*.10f,c+f*.8f+Vector3.up*.10f,c+f*.8f,-r*side);
            }
        }
        var material=Material("DimGuidance",new Color(.08f,.18f,.15f),.1f,new Color(28,60,48));
        MeshObject("Dim wall guide markers",guides,material,root,false);
    }
    public static void AddGuidance()
    {
        try
        {
            root=PrefabUtility.LoadPrefabContents(PrefabPath);
            if(root.transform.Find("Dim wall guide markers")!=null)throw new Exception("Guidance already exists.");
            CreateRoute();BuildGuidance();
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void BuildZones(SandboxCameraModes modes)
    {
        var zones=new List<BoxCollider>();var profile=ScriptableObject.CreateInstance<VolumeProfile>();
        var exposure=profile.Add<Exposure>(true);exposure.mode.Override(ExposureMode.Fixed);exposure.fixedExposure.Override(8.5f);
        var indirect=profile.Add<IndirectLightingController>(true);
        indirect.indirectDiffuseLightingMultiplier.Override(.015f);indirect.reflectionLightingMultiplier.Override(.04f);
        AssetDatabase.CreateAsset(profile,Folder+"/DimTunnel.profile.asset");
        foreach(var component in profile.components) AssetDatabase.AddObjectToAsset(component,profile);
        var parent=Child("Camera proximity and local lighting zones",root);
        for(int i=0;i<Route.Count-1;i+=3)
        {
            int end=Mathf.Min(i+3,Route.Count-1);
            Vector3 a=Route[i],b=Route[end],center=(a+b)*.5f;
            var g=Child("Tunnel zone "+i,parent);g.transform.position=center+Vector3.up*3;
            // Horizontal boxes contain the sloping roadway with generous vertical overlap.
            Vector3 heading=b-a;heading.y=0;g.transform.rotation=Quaternion.LookRotation(heading);
            var box=g.AddComponent<BoxCollider>();box.isTrigger=true;
            box.size=new Vector3(15.5f,10,heading.magnitude+8);
            zones.Add(box);
            if(center.y < -7)
            {
                var volume=g.AddComponent<Volume>();volume.isGlobal=false;volume.sharedProfile=profile;
                volume.priority=60;volume.blendDistance=4;volume.weight=1;
            }
        }
        var so=new SerializedObject(modes);var prop=so.FindProperty("tunnelZones");prop.arraySize=zones.Count;
        for(int i=0;i<zones.Count;i++)prop.GetArrayElementAtIndex(i).objectReferenceValue=zones[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    public static void SaveLightingProfile()
    {
        try
        {
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(Folder+"/DimTunnel.profile.asset");
            profile.components.Clear();
            var exposure=profile.Add<Exposure>(true);exposure.mode.Override(ExposureMode.Fixed);exposure.fixedExposure.Override(8.5f);
            var indirect=profile.Add<IndirectLightingController>(true);
            indirect.indirectDiffuseLightingMultiplier.Override(.015f);indirect.reflectionLightingMultiplier.Override(.04f);
            foreach(var component in profile.components)AssetDatabase.AddObjectToAsset(component,profile);
            EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void CreateTerrainHoles()
    {
        var terrain=Object.FindFirstObjectByType<Terrain>();
        if(!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(terrain.terrainData),Folder+"/TunnelTerrain.asset")) throw new Exception("Terrain clone failed");
        var data=AssetDatabase.LoadAssetAtPath<TerrainData>(Folder+"/TunnelTerrain.asset");
        int n=data.holesResolution;var holes=data.GetHoles(0,0,n,n);
        for(int z=0;z<n;z++)for(int x=0;x<n;x++)
        {
            float wx=terrain.transform.position.x+(x+.5f)*data.size.x/n;
            float wz=terrain.transform.position.z+(z+.5f)*data.size.z/n;
            bool portal=(Mathf.Abs(wx-100)<9 || Mathf.Abs(wx-242)<9) && wz < -39 && wz > -96;
            if(portal)holes[z,x]=false;
        }
        data.SetHoles(0,0,holes);EditorUtility.SetDirty(data);
    }

    sealed class MeshBuilder
    {
        readonly List<Vector3> vertices=new List<Vector3>();
        readonly List<int> triangles=new List<int>();readonly List<Vector2> uv=new List<Vector2>();
        public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 normal)
        {
            if(Vector3.Dot(Vector3.Cross(b-a,c-a),normal)<0) { var t=b;b=d;d=t; }
            int start=vertices.Count;vertices.AddRange(new[]{a,b,c,d});uv.AddRange(new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right});
            triangles.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
        }
        public void Triangle(Vector3 a,Vector3 b,Vector3 c,Vector3 normal)
        {
            if(Vector3.Dot(Vector3.Cross(b-a,c-a),normal)<0) {var t=b;b=c;c=t;}
            int start=vertices.Count;vertices.AddRange(new[]{a,b,c});uv.AddRange(new[]{Vector2.zero,Vector2.up,Vector2.one});triangles.AddRange(new[]{start,start+1,start+2});
        }
        public void Box(Vector3 center,Vector3 size,Quaternion rotation)
        {
            Vector3 x=rotation*Vector3.right*size.x*.5f,y=rotation*Vector3.up*size.y*.5f,z=rotation*Vector3.forward*size.z*.5f;
            Quad(center-x-y-z,center-x+y-z,center+x+y-z,center+x-y-z,-z);
            Quad(center-x-y+z,center+x-y+z,center+x+y+z,center-x+y+z,z);
            Quad(center-x-y-z,center-x-y+z,center-x+y+z,center-x+y-z,-x);
            Quad(center+x-y-z,center+x+y-z,center+x+y+z,center+x-y+z,x);
            Quad(center-x+y-z,center-x+y+z,center+x+y+z,center+x+y-z,y);
            Quad(center-x-y-z,center+x-y-z,center+x-y+z,center-x-y+z,-y);
        }
        public Mesh Build(string name)
        {
            var m=new Mesh{name=name,indexFormat=vertices.Count>65000?IndexFormat.UInt32:IndexFormat.UInt16};
            m.SetVertices(vertices);m.SetTriangles(triangles,0);m.SetUVs(0,uv);m.RecalculateNormals();m.RecalculateBounds();return m;
        }
    }
}
