using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static partial class OrganicSandbox
{
    static TerrainData data;
    static float[,] height;
    static float Ground(float x,float z)
    {
        float u=(x+500)/1000f,v=(z+500)/1000f;
        float h=3.4f+5*Mathf.PerlinNoise(u*4.8f+2.4f,v*4.8f+9.1f)+2*Mathf.PerlinNoise(u*13+5,v*13+2);
        h+=22*Mathf.Exp(-((x+230)*(x+230)/(150*150f)+(z-330)*(z-330)/(140*140f)));
        h+=12*Mathf.Exp(-((x+290)*(x+290)/(95*95f)+(z+150)*(z+150)/(170*170f)));
        h+=4*Mathf.Sin(x*.045f+z*.012f)*Mathf.Exp(-((x+375)*(x+375)/(80*80f)+(z-100)*(z-100)/(130*130f)));
        // A dry wash cuts across the north; shaped approaches keep the driving routes readable.
        h-=7*Mathf.Exp(-Mathf.Pow((z-305-18*Mathf.Sin(x*.02f))/22,2))*Mathf.Exp(-x*x/(145*145f));
        h=Mathf.Max(-4,h);
        float flat=0;
        flat=Mathf.Max(flat,Rect(x,z,15,45,50,40,35)); // spawn and reset
        flat=Mathf.Max(flat,Rect(x,z,171,-110,102,148,22)); // original tunnel and roof
        flat=Mathf.Max(flat,Rect(x,z,-5,-275,100,205,22)); // loop straight approaches
        flat=Mathf.Max(flat,Rect(x,z,270,245,105,174,18)); // oval and entry apron
        foreach(float zz in new[]{110f,-390f})flat=Mathf.Max(flat,Rect(x,z,-210,zz,32,35,20));
        h=Mathf.Lerp(h,0,flat);
        h*=1-Smooth((Mathf.Max(Mathf.Abs(x),Mathf.Abs(z))-445)/38);
        return h;
    }
    static void BuildTerrain()
    {
        var original=Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(t=>t.name=="Terrain");
        data=Object.Instantiate(original.terrainData);data.name="Organic canyon terrain";
        int oldHolesSize=data.holesResolution;var oldHoles=data.GetHoles(0,0,oldHolesSize,oldHolesSize);
        data.heightmapResolution=513;data.size=new Vector3(1000,80,1000);data.alphamapResolution=512;
        int n=data.heightmapResolution;float step=1000f/(n-1);height=new float[n,n];
        var best=new float[n,n];var roadH=new float[n,n];var influence=new float[n,n];
        for(int z=0;z<n;z++)for(int x=0;x<n;x++)best[z,x]=float.MaxValue;
        foreach(var r in Routes)
        {
            if(r.name=="Loop 18m" || r.name=="Loop 26m")continue;
            foreach(var p in r.points)
            {
                if(r.name=="Long tunnel continuous road" && p.y<0)continue;
                float rad=r.width*.5f+28;int ax=Mathf.Max(0,Mathf.FloorToInt((p.x-rad+500)/step)),bx=Mathf.Min(n-1,Mathf.CeilToInt((p.x+rad+500)/step));
                int az=Mathf.Max(0,Mathf.FloorToInt((p.z-rad+500)/step)),bz=Mathf.Min(n-1,Mathf.CeilToInt((p.z+rad+500)/step));
                for(int z=az;z<=bz;z++)for(int x=ax;x<=bx;x++)
                {
                    float d=Vector2.Distance(new Vector2(x*step-500,z*step-500),new Vector2(p.x,p.z));
                    if(d<rad && d<best[z,x]){best[z,x]=d;roadH[z,x]=Mathf.Max(-.35f,p.y-.55f);influence[z,x]=1-Smooth((d-r.width*.5f-4)/24);}
                }
            }
        }
        for(int z=0;z<n;z++)for(int x=0;x<n;x++)
        {float wx=x*step-500,wz=z*step-500;float h=Mathf.Lerp(Ground(wx,wz),roadH[z,x],influence[z,x]);float clampedZ=Mathf.Clamp(wz,175,315);float delta=new Vector2(wx-270,wz-clampedZ).magnitude-68;
            if(delta>=-14 && delta<49)
            {
                float gate=wx<270?Smooth((Mathf.Abs(wz-245)-18)/45):1;
                float oval=delta<=14?18*Mathf.Pow((delta+14)/28,2)-.25f:18*(1-Smooth((delta-14)/35));
                h=Mathf.Lerp(h,oval*gate,1-Smooth((delta-35)/14));
            }
            height[z,x]=(h+8)/80;}
        data.SetHeights(0,0,height);
        var materials=new[]{gravel,dirt,rock,sand};var layers=new TerrainLayer[4];
        for(int i=0;i<4;i++)
        {
            var layer=new TerrainLayer{name=materials[i].name,diffuseTexture=(Texture2D)materials[i].GetTexture("_BaseColorMap"),tileSize=new Vector2(i==2?13:11,i==2?13:11),smoothness=.05f};
            AssetDatabase.CreateAsset(layer,Folder+"/"+layer.name+".terrainlayer");layers[i]=layer;
        }
        data.terrainLayers=layers;int an=data.alphamapResolution;var alpha=new float[an,an,4];
        for(int z=0;z<an;z++)for(int x=0;x<an;x++)
        {
            float wx=x/(float)(an-1)*1000-500,wz=z/(float)(an-1)*1000-500;
            float noisy=Mathf.PerlinNoise(wx*.018f+20,wz*.018f+30);
            float dune=Smooth(((-wx-280)/100))*Smooth((190-Mathf.Abs(wz-130))/120);
            float earth=Mathf.Clamp01(.35f+noisy*.55f-dune*.8f);
            float slope=data.GetSteepness(x/(float)(an-1),z/(float)(an-1));
            float stone=Mathf.Clamp01((slope-18)/30);
            alpha[z,x,0]=(1-dune)*(1-earth)*(1-stone);alpha[z,x,1]=earth*(1-dune)*(1-stone);alpha[z,x,2]=stone;alpha[z,x,3]=dune*(1-stone);
            float sum=0;for(int k=0;k<4;k++)sum+=alpha[z,x,k];for(int k=0;k<4;k++)alpha[z,x,k]/=sum;
        }
        data.SetAlphamaps(0,0,alpha);
        // Retain both original portals verbatim, then cut only the new route's open trenches.
        int hn=data.holesResolution;var holes=new bool[hn,hn];
        for(int z=0;z<hn;z++)for(int x=0;x<hn;x++)holes[z,x]=oldHoles[Mathf.Min(oldHolesSize-1,z*oldHolesSize/hn),Mathf.Min(oldHolesSize-1,x*oldHolesSize/hn)];
        var route=Routes.Single(r=>r.name=="Long tunnel continuous road");float hs=1000f/hn;
        for(int i=0;i<route.points.Length;i++)
        {
            Vector3 p=route.points[i];if(p.y>=-.02f || p.y < -8.5f)continue;
            int ax=Mathf.Max(0,Mathf.FloorToInt((p.x-9.5f+500)/hs)),bx=Mathf.Min(hn-1,Mathf.CeilToInt((p.x+9.5f+500)/hs));
            int az=Mathf.Max(0,Mathf.FloorToInt((p.z-3+500)/hs)),bz=Mathf.Min(hn-1,Mathf.CeilToInt((p.z+3+500)/hs));
            for(int z=az;z<=bz;z++)for(int x=ax;x<=bx;x++)holes[z,x]=false;
        }
        data.SetHoles(0,0,holes);AssetDatabase.CreateAsset(data,Folder+"/OrganicTerrain.asset");
        var g=Terrain.CreateTerrainGameObject(data);g.name="Sculpted desert terrain";g.transform.SetParent(root.transform);g.transform.position=new Vector3(-500,-8,-500);
        var terrain=g.GetComponent<Terrain>();terrain.materialTemplate=original.materialTemplate;terrain.heightmapPixelError=3;terrain.basemapDistance=1400;terrain.drawInstanced=true;
        g.AddComponent<TerrainDrivingSurface>();g.isStatic=true;
    }
    static float SampleGround(float x,float z)=>data.GetInterpolatedHeight((x+500)/1000,(z+500)/1000)-8;
    static void BuildScenery()
    {
        var backdrop=Grid("Distant desert ridges",385,33,(i,j)=>
        {
            float a=2*Mathf.PI*i/384;Vector2 d=new Vector2(Mathf.Cos(a),Mathf.Sin(a));d/=Mathf.Max(Mathf.Abs(d.x),Mathf.Abs(d.y));
            float radius=500+j/32f*2100;Vector2 p=d*radius;
            float h=-.25f+Smooth(j/5f)*(14+115*Mathf.PerlinNoise(p.x*.0016f+7,p.y*.0016f+11));
            return new Vector3(p.x,h,p.y);
        },i=>Vector3.up,.05f);
        MeshObject("Distant desert ridges",backdrop,rock,root,false);
        var group=Child("Natural rock outcrops");var mesh=new MeshMaker();var random=new System.Random(417);
        int made=0;
        for(int attempt=0;attempt<650 && made<85;attempt++)
        {
            float x=(float)random.NextDouble()*900-450,z=(float)random.NextDouble()*880-440;
            if(Rect(x,z,30,-265,125,215)>0 || Rect(x,z,171,-110,110,155)>0 || Rect(x,z,270,245,112,180)>0 || Rect(x,z,0,45,70,55)>0)continue;
            if(Routes.Any(r=>r.points.Where((p,i)=>i%8==0).Any(p=>Vector2.Distance(new Vector2(p.x,p.z),new Vector2(x,z))<r.width*.5f+16)))continue;
            float size=3+(float)random.NextDouble()*7;Vector3 center=new Vector3(x,SampleGround(x,z)-.4f,z);
            int sides=7;var ring=new Vector3[sides];var top=new Vector3[sides];float yaw=(float)random.NextDouble()*6.28f;
            for(int k=0;k<sides;k++)
            {float a=yaw+k*2*Mathf.PI/sides;ring[k]=center+new Vector3(Mathf.Cos(a)*size,0,Mathf.Sin(a)*size*.7f);top[k]=center+new Vector3(Mathf.Cos(a)*size*.65f,size*(.7f+(float)random.NextDouble()*.2f),Mathf.Sin(a)*size*.45f);}
            for(int k=0;k<sides;k++)
            {int j=(k+1)%sides;mesh.Quad(ring[k],top[k],top[j],ring[j],ring[k]-center);mesh.Quad(top[k],center+Vector3.up*size,center+Vector3.up*size,top[j],Vector3.up);}
            made++;
        }
        MeshObject("Low sandstone formations",mesh,rock,group);
        // Flush start bars at the spawn are readable without introducing upright obstacles.
        var paint=new MeshMaker();for(int i=-3;i<=3;i++)
            paint.Quad(new Vector3(i*2,.022f,48),new Vector3(i*2,.022f,49),new Vector3(i*2+1,.022f,49),new Vector3(i*2+1,.022f,48),Vector3.up);
        MeshObject("Flush spawn markings",paint,white,group,false);
    }
}
