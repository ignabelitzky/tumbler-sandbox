using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Object=UnityEngine.Object;

public static partial class OrganicSandbox
{
    public static void Build()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity");
            if(AssetDatabase.IsValidFolder(Folder))throw new Exception("Generated assets already exist; inspect before explicitly rebuilding.");
            EnsureFolder(Folder);Routes.Clear();Materials();root=new GameObject("Organic proving grounds");
            BuildRoads();BuildLoops();BuildOval();BuildLongTunnel();HarmonizeJunctions();BuildTerrain();BuildScenery();
            var grip=root.AddComponent<WheelSurfaceGrip>();
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Prefab);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefab,out string guid,out long rootId);
            var storedGrip=prefab.GetComponent<WheelSurfaceGrip>();
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(storedGrip,out string ignored,out long gripId);
            Directory.CreateDirectory(Results);
            File.WriteAllText(Results+"/generated.json","{\"prefab_guid\":\""+guid+"\",\"root_id\":"+rootId+",\"grip_id\":"+gripId+"}");
            File.WriteAllText(Folder+"/Routes.json",JsonUtility.ToJson(new RouteFile{routes=Routes.ToArray()},true));
            AssetDatabase.SaveAssets();EditorApplication.Exit(0);
        }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static Texture2D NoiseTexture(string name,Color low,Color high,int seed)
    {
        int n=256;var t=new Texture2D(n,n,TextureFormat.RGB24,true){name=name,wrapMode=TextureWrapMode.Repeat};var px=new Color[n*n];
        var random=new System.Random(seed);
        for(int z=0;z<n;z++)for(int x=0;x<n;x++)
        {
            float u=x/(float)n,v=z/(float)n;
            // Periodic noise wraps without conspicuous seams, with fine grain on top.
            float broad=.5f+.025f*Mathf.Sin(2*Mathf.PI*u)*Mathf.Cos(4*Mathf.PI*v)+.02f*Mathf.Cos(6*Mathf.PI*u+4*Mathf.PI*v);
            float grain=(float)random.NextDouble();px[z*n+x]=Color.Lerp(low,high,Mathf.Clamp01(broad*.55f+grain*.45f));
        }
        t.SetPixels(px);t.Apply();string path=Folder+"/"+name+".png";File.WriteAllBytes(path,t.EncodeToPNG());Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.wrapMode=TextureWrapMode.Repeat;importer.anisoLevel=8;importer.mipmapEnabled=true;importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static void Materials()
    {
        road=Mat("Weathered asphalt",new Color(.32f,.30f,.27f),.16f);
        gravel=Mat("Crushed limestone",Color.white);dirt=Mat("Ochre earth",Color.white);sand=Mat("Dune sand",Color.white);rock=Mat("Weathered sandstone",new Color(.46f,.35f,.24f));
        steel=Mat("Graphite steel",new Color(.12f,.14f,.145f),.3f);amber=Mat("Warm edge paint",new Color(.82f,.56f,.24f));white=Mat("Faded chalk",new Color(.65f,.65f,.57f));
        road.SetTexture("_BaseColorMap",NoiseTexture("Asphalt grain",new Color(.30f,.30f,.28f),new Color(.46f,.44f,.40f),1));
        gravel.SetTexture("_BaseColorMap",NoiseTexture("Limestone grain",new Color(.28f,.25f,.21f),new Color(.56f,.51f,.42f),2));
        dirt.SetTexture("_BaseColorMap",NoiseTexture("Earth grain",new Color(.28f,.18f,.11f),new Color(.51f,.35f,.20f),3));
        sand.SetTexture("_BaseColorMap",NoiseTexture("Sand grain",new Color(.46f,.33f,.19f),new Color(.72f,.57f,.36f),4));
        rock.SetTexture("_BaseColorMap",NoiseTexture("Rock grain",new Color(.45f,.43f,.38f),new Color(.79f,.73f,.61f),5));
        foreach(var m in new[]{road,gravel,dirt,sand,rock})EditorUtility.SetDirty(m);
    }
    static void BuildRoads()
    {
        var roads=Child("Flowing roads and terrain trials");
        var controls=new[]{new Vector3(0,.14f,55),new Vector3(-60,1.5f,140),new Vector3(-260,5,210),new Vector3(-370,8,335),new Vector3(-225,11,422),new Vector3(-30,8,395),new Vector3(130,3,335),new Vector3(155,.14f,175),new Vector3(80,.14f,70)};
        var main=MakeRoute("Scenic asphalt circuit",1550,14,t=>{var p=Cat(controls,t,true);p.y=Mathf.Max(.14f,p.y);return p;});
        Ribbon(main,road,roads,Vector2.one,true);
        var dirtControls=new[]{new Vector3(-80,1,150),new Vector3(-145,7,235),new Vector3(-250,15,295),new Vector3(-255,20,360),new Vector3(-145,18,365),new Vector3(-80,9,290),new Vector3(-100,4,220),new Vector3(-80,1,150)};
        Ribbon(MakeRoute("Ridge dirt climb",700,12,t=>Cat(dirtControls,t)),dirt,roads,new Vector2(.67f,.55f));
        Ribbon(MakeRoute("Gravel suspension run",400,14,t=>new Vector3(-355+160*t,8f+2.5f*Mathf.Sin(Mathf.PI*t)+.42f*Mathf.Pow(Mathf.Sin(t*Mathf.PI*8),2)*Mathf.Sin(Mathf.PI*t),285+28*Mathf.Sin(t*Mathf.PI))),gravel,roads,new Vector2(.78f,.68f));
        Ribbon(MakeRoute("Soft sand meander",550,18,t=>new Vector3(-395+180*t,6f+3.5f*Mathf.Sin(Mathf.PI*t),150+50*Mathf.Sin(2*Mathf.PI*t))),sand,roads,new Vector2(.54f,.46f));
        // Long smooth tabletops can be driven slowly or launched at speed; landings remain solid, never blind gaps.
        foreach(var spec in new[]{new Vector3(-45,3.5f,280),new Vector3(55,6,240)})
        {
            string name=spec.x<0?"Low flowing tabletop":"High flowing tabletop";
            Ribbon(MakeRoute(name,150,15,t=>{float z=(t-.5f)*90;float h=spec.y*Smooth((z+40)/27)*(1-Smooth((z-6)/34));return new Vector3(spec.x,.14f+h,spec.z+z);}),gravel,roads,new Vector2(.85f,.8f),true);
        }
        Ribbon(MakeRoute("Skid bowl approach",170,17,t=>new Vector3(0,.14f,60+120*t)),road,roads,Vector2.one);
        var bowl=Grid("Shallow handling bowl",97,25,(i,j)=>{float a=2*Mathf.PI*i/96;float rr=8+j/24f*37;return new Vector3(80+rr*Mathf.Cos(a),.14f+3.2f*Mathf.Pow((rr-8)/37,2),130+rr*Mathf.Sin(a));},i=>Vector3.up);
        MeshObject("Shallow handling bowl",bowl,road,roads).AddComponent<DrivingSurface>();
        // Broad connector aprons leave the spawn/reset lane and the original tunnel mouths unobstructed.
        Ribbon(MakeRoute("West tunnel access",260,17,t=>Cat(new[]{new Vector3(0,.14f,50),new Vector3(-90,.14f,100),new Vector3(-185,.14f,180),new Vector3(-210,.14f,160),new Vector3(-210,.14f,110)},t)),road,roads,Vector2.one);
        Ribbon(MakeRoute("Original tunnel access",170,18,t=>new Vector3(100,.14f,55-95*t)),road,roads,Vector2.one);
        Ribbon(MakeRoute("Original tunnel return",190,18,t=>new Vector3(242,.14f,-40+150*t)),road,roads,Vector2.one);
        Ribbon(MakeRoute("Oval entry",170,18,t=>new Vector3(140+82*t,.14f,245)),road,roads,Vector2.one);
    }
    static void BuildLoops()
    {
        var group=Child("Twin high speed deathloops");
        foreach(var spec in new[]{new Vector2(-45,18),new Vector2(35,26)})
        {
            float cx=spec.x,radius=spec.y;
            string name=radius<20?"Loop 18m":"Loop 26m";
            var loop=MakeRoute(name,480,16,t=>{float a=t*Mathf.PI*2;return new Vector3(cx+(Smooth(t)-.5f)*22,.14f+radius*(1-Mathf.Cos(a)),-290+radius*Mathf.Sin(a));},t=>new Vector3(0,Mathf.Cos(t*Mathf.PI*2),-Mathf.Sin(t*Mathf.PI*2)));
            Ribbon(loop,road,group,Vector2.one,true);
            Ribbon(MakeRoute(name+" run-up",190,16,t=>new Vector3(cx-11,.14f,Mathf.Lerp(-475,-290,t))),road,group,Vector2.one,true);
            Ribbon(MakeRoute(name+" run-out",210,16,t=>new Vector3(cx+11,.14f,Mathf.Lerp(-290,-75,t))),road,group,Vector2.one,true);
            var rails=new MeshMaker();var supports=new MeshMaker();
            for(int i=0;i<loop.points.Length-1;i++)foreach(int side in new[]{-1,1})
            {
                Vector3 a=loop.points[i]+loop.Right(i)*(side*8),b=loop.points[i+1]+loop.Right(i+1)*(side*8);
                // A low continuous curb has a real collider; rails sit outside the 16m running surface.
                rails.Quad(a,b,b+loop.up[i+1]*.65f,a+loop.up[i]*.65f,-loop.Right(i)*side);
                if(i%60==0 && loop.points[i].y>4)
                    supports.Bar(a-loop.up[i]*.18f,new Vector3(cx+side*34,0,a.z),.22f);
            }
            MeshObject(name+" continuous curbs",rails,steel,group);MeshObject(name+" support lattice",supports,steel,group,false);
            // Low distance chevrons replace vertical instruction boards.
            var marks=new MeshMaker();for(int k=0;k<5;k++)
            {float z=-430+k*22;Vector3 p=new Vector3(cx-11,.16f,z);marks.Quad(p+new Vector3(-.2f,0,-2),p+new Vector3(-.2f,0,2),p+new Vector3(.2f,0,2),p+new Vector3(.2f,0,-2),Vector3.up);}
            MeshObject(name+" ground markers",marks,amber,group,false);
        }
        Ribbon(MakeRoute("Loop staging return",350,21,t=>new Vector3(Mathf.Lerp(-95,90,t),.14f,-460)),gravel,group,new Vector2(.9f,.85f));
        Ribbon(MakeRoute("Loop arrival sweep",500,18,t=>Cat(new[]{new Vector3(0,.14f,20),new Vector3(-100,.14f,-55),new Vector3(-105,.14f,-260),new Vector3(-95,.14f,-460)},t)),road,group,Vector2.one,true);
    }
    public static Vector3 OvalCenter(float t)
    {
        // Capsule: 140m straights and two tangent-continuous 68m-radius half circles.
        float r=68,straight=140,total=2*straight+2*Mathf.PI*r,d=t*total;
        if(d<straight)return new Vector3(338,0,175+d);
        d-=straight;if(d<Mathf.PI*r){float a=d/r;return new Vector3(270+r*Mathf.Cos(a),0,315+r*Mathf.Sin(a));}
        d-=Mathf.PI*r;if(d<straight)return new Vector3(202,0,315-d);
        d-=straight;float b=Mathf.PI+d/r;return new Vector3(270+r*Mathf.Cos(b),0,175+r*Mathf.Sin(b));
    }
    static Vector3 OvalOut(int i,int steps=800)
    {
        var p=OvalCenter(i/(float)steps);var f=(OvalCenter(Mathf.Min(i+1,steps)/(float)steps)-OvalCenter(Mathf.Max(i-1,0)/(float)steps)).normalized;
        return Vector3.Cross(Vector3.up,f).normalized;
    }
    public static Vector3 OvalPoint(int i,int j)
    {float t=j/28f;var p=OvalCenter(i/800f);float bank=p.x<270?Smooth((Mathf.Abs(p.z-245)-18)/45):1;return p+OvalOut(i)*(-14+28*t)+Vector3.up*(.14f+18*t*t*bank);}
    static void BuildOval()
    {
        var group=Child("Banked horseshoe oval and catch cage");
        var mesh=Grid("Progressively banked oval",801,29,OvalPoint,i=>Vector3.up);
        var ovalObject=MeshObject("Progressively banked oval",mesh,road,group);ovalObject.AddComponent<DrivingSurface>();ovalObject.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
        var safety=new MeshMaker();var wires=new MeshMaker();var ribs=new MeshMaker();var paint=new MeshMaker();
        for(int i=0;i<800;i++)
        {
            Vector3 a=OvalPoint(i,28),b=OvalPoint(i+1,28),ia=OvalPoint(i,0),ib=OvalPoint(i+1,0),outward=OvalOut(i);
            // Continuous outer catch panel and overhead collision net. The inside edge has two broad access gaps.
            bool entry=a.x<250 && Mathf.Abs(a.z-245)<20;
            if(!entry)safety.Quad(a,b,b+Vector3.up*7,a+Vector3.up*7,-outward);
            if(!entry)safety.Quad(ia+Vector3.up*25,ib+Vector3.up*25,b+Vector3.up*7,a+Vector3.up*7,Vector3.down);
            bool gate=ia.x<230 && Mathf.Abs(ia.z-245)<24;
            if(!gate)safety.Quad(ia,ia+Vector3.up*1.2f,ib+Vector3.up*1.2f,ib,outward);
            for(int h=1;h<=7 && !entry;h++)wires.Bar(a+Vector3.up*h,b+Vector3.up*h,.035f);
            if(i%6==0 && !entry)
            {
                ribs.Bar(a,a+Vector3.up*7,.12f);ribs.Bar(a+Vector3.up*7,ia+Vector3.up*25,.10f);
                if(i%24==0)ribs.Bar(a-Vector3.up*.2f,new Vector3(a.x,0,a.z),.20f);
            }
            if(i%3==0 && !entry)wires.Bar(a+Vector3.up*7,ia+Vector3.up*25,.025f);
            foreach(int j in new[]{1,14,27})paint.Quad(OvalPoint(i,j)+Vector3.up*.025f,OvalPoint(i+1,j)+Vector3.up*.025f,OvalPoint(i+1,j)+outward*.12f+Vector3.up*.025f,OvalPoint(i,j)+outward*.12f+Vector3.up*.025f,Vector3.up);
        }
        var panel=MeshObject("Continuous oval catch collision",safety,steel,group);panel.GetComponent<MeshRenderer>().enabled=false;
        MeshObject("Oval steel safety net",wires,steel,group,false);MeshObject("Oval structural ribs",ribs,steel,group);
        MeshObject("Oval painted guide lanes",paint,amber,group,false);
    }
    static void BuildLongTunnel()
    {
        var group=Child("Long four bend mountain tunnel");
        var route=MakeRoute("Long tunnel continuous road",780,17,t=>new Vector3(-210+58*Mathf.Sin(4*Mathf.PI*t)*Mathf.Pow(Mathf.Sin(Mathf.PI*t),2),-14*Smooth(t/.15f)*Smooth((1-t)/.15f)+.06f,110-500*t));
        Ribbon(route,road,group,Vector2.one,true);
        var shell=new MeshMaker();var ribs=new MeshMaker();var marker=new MeshMaker();var zones=new List<BoxCollider>();
        var cross=new[]{new Vector2(-8.5f,4),new Vector2(-7.6f,6.2f),new Vector2(-5,7.6f),new Vector2(0,8.1f),new Vector2(5,7.6f),new Vector2(7.6f,6.2f),new Vector2(8.5f,4)};
        var dim=ScriptableObject.CreateInstance<VolumeProfile>();var exp=dim.Add<Exposure>();exp.mode.Override(ExposureMode.Fixed);exp.fixedExposure.Override(9.5f);
        var indirect=dim.Add<IndirectLightingController>();indirect.indirectDiffuseLightingMultiplier.Override(.015f);indirect.reflectionLightingMultiplier.Override(.04f);
        AssetDatabase.CreateAsset(dim,Folder+"/Long tunnel lighting.asset");foreach(var c in dim.components)AssetDatabase.AddObjectToAsset(c,dim);
        for(int i=0;i<route.points.Length-1;i++)
        {
            Vector3 a=route.points[i],b=route.points[i+1],ra=route.Right(i),rb=route.Right(i+1);
            bool covered=a.y<-8.2f && b.y<-8.2f;
            foreach(int side in new[]{-1,1})
            {
                Vector3 aa=a+ra*8.5f*side,bb=b+rb*8.5f*side;
                float ha=covered?4:Mathf.Max(1,1-a.y),hb=covered?4:Mathf.Max(1,1-b.y);
                shell.Quad(aa,aa+Vector3.up*ha,bb+Vector3.up*hb,bb,-ra*side);
                shell.Quad(aa+Vector3.up*ha,aa+ra*side*2+Vector3.up*ha,bb+rb*side*2+Vector3.up*hb,bb+Vector3.up*hb,Vector3.up);
                if(covered && i%10==0)marker.Bar(aa+Vector3.up*1.3f,bb+Vector3.up*1.3f,.05f);
            }
            if(covered)
            {
                for(int k=0;k<cross.Length-1;k++)
                {
                    var aa=a+ra*cross[k].x+Vector3.up*cross[k].y;var ab=a+ra*cross[k+1].x+Vector3.up*cross[k+1].y;
                    var ba=b+rb*cross[k].x+Vector3.up*cross[k].y;var bb=b+rb*cross[k+1].x+Vector3.up*cross[k+1].y;
                    shell.Quad(aa,ab,bb,ba,Vector3.down-ra*cross[k].x*.08f);
                    if(i%20==0)ribs.Bar(aa,ab,.13f);
                }
                if(i%32==0)
                {
                    var light=Child("Recessed amber tunnel light "+i,group);light.transform.position=a+Vector3.up*7.3f;light.transform.rotation=Quaternion.LookRotation(Vector3.down);
                    var l=light.AddComponent<Light>();l.type=LightType.Spot;l.range=16;l.spotAngle=115;l.intensity=1800;l.color=new Color(1,.72f,.42f);l.shadows=LightShadows.None;light.AddComponent<HDAdditionalLightData>();
                }
            }
            if(i%4==0)
            {
                int end=Mathf.Min(i+4,route.points.Length-1);Vector3 e=route.points[end];var g=Child("Long tunnel camera zone "+i,group);g.transform.position=(a+e)*.5f+Vector3.up*3;
                Vector3 f=e-a;f.y=0;g.transform.rotation=Quaternion.LookRotation(f);
                var c=g.AddComponent<BoxCollider>();c.isTrigger=true;c.size=new Vector3(18,10,f.magnitude+5);zones.Add(c);
                if(a.y<-8){var v=g.AddComponent<Volume>();v.sharedProfile=dim;v.isGlobal=false;v.priority=65;v.blendDistance=3;}
            }
        }
        MeshObject("Long tunnel stone shell",shell,rock,group);MeshObject("Long tunnel arch ribs",ribs,steel,group,false);
        var glow=Mat("Subtle tunnel guide glow",new Color(.4f,.24f,.10f));glow.SetColor("_EmissiveColor",new Color(32,16,4));
        MeshObject("Long tunnel amber guides",marker,glow,group,false);
        var service=group.AddComponent<SandboxTunnelZones>();var so=new SerializedObject(service);var array=so.FindProperty("zones");array.arraySize=zones.Count;
        for(int i=0;i<zones.Count;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=zones[i];so.ApplyModifiedPropertiesWithoutUndo();
    }
}
