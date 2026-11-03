using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class ExpansionAuthoring
{
    static void BuildPortalAprons()
    {
        var terrain=UnityEngine.Object.FindFirstObjectByType<Terrain>();
        var data=AssetDatabase.LoadAssetAtPath<TerrainData>(Folder+"/TunnelTerrain.asset");int n=data.holesResolution;
        var holes=data.GetHoles(0,0,n,n);var apron=new MeshBuilder();
        float dx=data.size.x/n,dz=data.size.z/n;
        int roofIndex=Enumerable.Range(0,Route.Count-1).First(i=>Route[i].y+.04f < -7.2f && Route[i+1].y+.04f < -7.2f);
        float roofZ=Route[roofIndex].z;
        foreach(float center in new[]{100f,242f})
        {
            float left=float.PositiveInfinity,right=float.NegativeInfinity,back=float.PositiveInfinity,front=float.NegativeInfinity;
            for(int z=0;z<n;z++)for(int x=0;x<n;x++)
            {
                float wx=terrain.transform.position.x+(x+.5f)*dx;
                if(holes[z,x] || Mathf.Abs(wx-center)>15)continue;
                left=Mathf.Min(left,terrain.transform.position.x+x*dx);
                right=Mathf.Max(right,terrain.transform.position.x+(x+1)*dx);
                back=Mathf.Min(back,terrain.transform.position.z+z*dz);
                front=Mathf.Max(front,terrain.transform.position.z+(z+1)*dz);
            }
            if(float.IsInfinity(left))throw new Exception("Missing portal terrain opening");
            // Cover the actual cell boundaries, overlapping the terrain to hide its raster edge.
            left=Mathf.Min(left-.15f,center-10);right=Mathf.Max(right+.15f,center+10);
            back-=.15f;front=Mathf.Max(front+.15f,-39.8f);
            Surface(apron,left,right,-40,front,.04f);
            Surface(apron,left,center-7,back,-40,.02f);
            Surface(apron,center+7,right,back,-40,.02f);
            Surface(apron,center-7,center+7,back,roofZ,.02f);
        }
        MeshObject("Portal terrain seam aprons",apron,concrete,structure,true);
    }
    static void Surface(MeshBuilder mesh,float x0,float x1,float z0,float z1,float y)
    {
        if(x1<=x0 || z1<=z0)throw new Exception("Invalid portal seam bounds");
        mesh.Quad(new Vector3(x0,y,z0),new Vector3(x0,y,z1),new Vector3(x1,y,z1),new Vector3(x1,y,z0),Vector3.up);
    }
    public static void RepairPortals()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity");
            root=PrefabUtility.LoadPrefabContents(PrefabPath);
            structure=root.transform.Find("Tunnel Structure").gameObject;
            details=root.transform.Find("Road Markings and Safety Details").gameObject;
            concrete=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Concrete.mat");
            lamp=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/WarmDiffuser.mat");
            CreateRoute();floor=new MeshBuilder();shell=new MeshBuilder();trims=new MeshBuilder();
            // Build only the new structural mesh; keep the saved lights and road untouched.
            BuildTunnel(false);
            var target=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Tunnel walls and closed roof.asset");
            var replacement=shell.Build(target.name);EditorUtility.CopySerialized(replacement,target);
            UnityEngine.Object.DestroyImmediate(replacement);EditorUtility.SetDirty(target);
            var existing=structure.transform.Find("Portal terrain seam aprons");
            if(existing!=null)UnityEngine.Object.DestroyImmediate(existing.gameObject);
            BuildPortalAprons();
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
