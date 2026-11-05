using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class ClassicSandbox
{
    static float Ground(float x, float z)
    {
        float h=.5f+2.2f*Mathf.PerlinNoise(x*.005f+17,z*.005f+31)+.35f*Mathf.Sin(x*.025f+z*.012f);
        h=Mathf.Lerp(h,0,Rect(x,z,-100,35,150,180,65));
        h=Mathf.Lerp(h,0,Rect(x,z,171,-110,104,150,25)); // Preserve the original tunnel mouths and cover.
        h=Mathf.Lerp(h,0,Rect(x,z,0,40,65,45,22));
        foreach(var position in new[]{new Vector2(-282,-48),new Vector2(-202,82),new Vector2(-102,-124),new Vector2(-82,15),new Vector2(-108,8)})
            h=Mathf.Lerp(h,0,Rect(x,z,position.x,position.y,24,28,14));
        h*=1-Smooth((Mathf.Max(Mathf.Abs(x),Mathf.Abs(z))-445)/45);
        return h;
    }
    static void BuildTerrain()
    {
        var original=Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(t=>t.name=="Terrain");
        var source=original.terrainData;int oldSize=source.holesResolution;var oldHoles=source.GetHoles(0,0,oldSize,oldSize);
        terrainData=Object.Instantiate(source);terrainData.name="Classic rolling terrain";terrainData.heightmapResolution=513;
        terrainData.size=new Vector3(1000,40,1000);terrainData.alphamapResolution=256;
        int n=terrainData.heightmapResolution;float spacing=1000f/(n-1);var height=new float[n,n];var best=new float[n,n];var roadHeight=new float[n,n];var influence=new float[n,n];
        for(int z=0;z<n;z++)for(int x=0;x<n;x++)best[z,x]=float.MaxValue;
        foreach(var route in routes)foreach(var p in route.points)
        {
            float radius=route.width*.5f+18;
            int ax=Mathf.Max(0,Mathf.FloorToInt((p.x-radius+500)/spacing)),bx=Mathf.Min(n-1,Mathf.CeilToInt((p.x+radius+500)/spacing));
            int az=Mathf.Max(0,Mathf.FloorToInt((p.z-radius+500)/spacing)),bz=Mathf.Min(n-1,Mathf.CeilToInt((p.z+radius+500)/spacing));
            for(int z=az;z<=bz;z++)for(int x=ax;x<=bx;x++)
            {
                float distance=Vector2.Distance(new Vector2(x*spacing-500,z*spacing-500),new Vector2(p.x,p.z));
                if(distance<radius && distance<best[z,x])
                {
                    best[z,x]=distance;roadHeight[z,x]=Mathf.Max(-.28f,p.y-.42f);
                    influence[z,x]=1-Smooth((distance-route.width*.5f-3)/15);
                }
            }
        }
        for(int z=0;z<n;z++)for(int x=0;x<n;x++)
        {
            float wx=x*spacing-500,wz=z*spacing-500;
            float h=Mathf.Lerp(Ground(wx,wz),roadHeight[z,x],influence[z,x]);height[z,x]=(h+8)/40;
        }
        terrainData.SetHeights(0,0,height);
        var names=new[]{"Crushed limestone","Ochre earth","Weathered sandstone","Dune sand"};
        terrainData.terrainLayers=names.Select(name=>AssetDatabase.LoadAssetAtPath<TerrainLayer>(Legacy+name+".terrainlayer")).ToArray();
        int a=terrainData.alphamapResolution;var alpha=new float[a,a,4];
        for(int z=0;z<a;z++)for(int x=0;x<a;x++)
        {
            float wx=x/(float)(a-1)*1000-500,wz=z/(float)(a-1)*1000-500;
            float patch=Mathf.PerlinNoise(wx*.013f+21,wz*.013f+38);float soft=Smooth((.62f-patch)/.42f);
            float slope=terrainData.GetSteepness(x/(float)(a-1),z/(float)(a-1));float stone=Mathf.Clamp01((slope-16)/26);
            alpha[z,x,0]=(1-soft)*.55f*(1-stone);alpha[z,x,1]=(1-soft)*.45f*(1-stone);alpha[z,x,2]=stone;alpha[z,x,3]=soft*(1-stone);
        }
        terrainData.SetAlphamaps(0,0,alpha);
        int hsize=terrainData.holesResolution;var holes=new bool[hsize,hsize];
        for(int z=0;z<hsize;z++)for(int x=0;x<hsize;x++)holes[z,x]=oldHoles[Mathf.Min(oldSize-1,z*oldSize/hsize),Mathf.Min(oldSize-1,x*oldSize/hsize)];
        terrainData.SetHoles(0,0,holes);
        string path=Folder+"/ClassicTerrain.asset";var saved=AssetDatabase.LoadAssetAtPath<TerrainData>(path);
        if(saved==null)AssetDatabase.CreateAsset(terrainData,path);
        else { EditorUtility.CopySerialized(terrainData,saved);Object.DestroyImmediate(terrainData);terrainData=saved;EditorUtility.SetDirty(saved); }
        var g=Terrain.CreateTerrainGameObject(terrainData);g.name="Gentle classic terrain";g.transform.SetParent(root.transform);g.transform.position=new Vector3(-500,-8,-500);
        var terrain=g.GetComponent<Terrain>();terrain.materialTemplate=original.materialTemplate;terrain.heightmapPixelError=3;terrain.basemapDistance=1400;terrain.drawInstanced=true;
        g.AddComponent<TerrainDrivingSurface>();g.isStatic=true;
    }
    static void BuildScenery()
    {
        var group=Group("Low natural scenery");
        var distant=new GameObject("Distant desert ridges",typeof(MeshFilter),typeof(MeshRenderer));distant.transform.SetParent(group.transform,false);
        distant.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Legacy+"Distant desert ridges.asset");distant.GetComponent<MeshRenderer>().sharedMaterial=rock;distant.isStatic=true;
        var mesh=new OrganicSandbox.MeshMaker();var random=new System.Random(881);int count=0;
        for(int attempt=0;attempt<500 && count<38;attempt++)
        {
            float x=(float)random.NextDouble()*850-425,z=(float)random.NextDouble()*760-380;
            if(Rect(x,z,-140,25,230,310)>0 || Rect(x,z,171,-110,120,175)>0 || Rect(x,z,90,60,220,80)>0)continue;
            if(routes.Any(r=>r.points.Where((p,i)=>i%10==0).Any(p=>Vector2.Distance(new Vector2(p.x,p.z),new Vector2(x,z))<r.width*.5f+28)))continue;
            float size=2+(float)random.NextDouble()*3;var center=new Vector3(x,terrainData.GetInterpolatedHeight((x+500)/1000,(z+500)/1000)-8-.25f,z);
            const int sides=7;var ring=new Vector3[sides];var top=new Vector3[sides];
            for(int k=0;k<sides;k++){float angle=k*Mathf.PI*2/sides;ring[k]=center+new Vector3(Mathf.Cos(angle)*size,0,Mathf.Sin(angle)*size*.75f);top[k]=center+new Vector3(Mathf.Cos(angle)*size*.6f,size*.8f,Mathf.Sin(angle)*size*.45f);}
            for(int k=0;k<sides;k++){int j=(k+1)%sides;mesh.Quad(ring[k],top[k],top[j],ring[j],ring[k]-center);mesh.Quad(top[k],center+Vector3.up*size,center+Vector3.up*size,top[j],Vector3.up);}count++;
        }
        MeshObject("Low sandstone outcrops",mesh.Build("Low sandstone outcrops"),rock,group);
    }
}
