using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

public static partial class OrganicSandbox
{
    public const string Folder="Assets/_Project/Environment/OrganicSandbox";
    public const string Prefab=Folder+"/OrganicSandbox.prefab";
    public const string Results="TestResults/OrganicSandbox";
    static GameObject root;
    static Material road,gravel,dirt,sand,rock,steel,amber,white;
    public static readonly List<Route> Routes=new List<Route>();
    [Serializable] public class Route
    {
        public string name; public float width; public Vector3[] points; public Vector3[] up;
        public Vector3 Forward(int i) => (points[Mathf.Min(i+1,points.Length-1)]-points[Mathf.Max(0,i-1)]).normalized;
        public Vector3 Right(int i) => Vector3.Cross(up[i],Forward(i)).normalized;
        public float Length { get {float s=0;for(int i=1;i<points.Length;i++)s+=Vector3.Distance(points[i-1],points[i]);return s;} }
    }
    [Serializable] public class RouteFile { public Route[] routes; }
    static GameObject Child(string name,GameObject parent=null)
    {var g=new GameObject(name);g.transform.SetParent((parent??root).transform,false);return g;}
    static void EnsureFolder(string p)
    {if(AssetDatabase.IsValidFolder(p))return;int n=p.LastIndexOf('/');EnsureFolder(p.Substring(0,n));AssetDatabase.CreateFolder(p.Substring(0,n),p.Substring(n+1));}
    static Material Mat(string name,Color c,float smoothness=0.1f)
    {
        var m=new Material(Shader.Find("HDRP/Lit")){name=name,enableInstancing=true};
        m.SetColor("_BaseColor",c);m.SetFloat("_Smoothness",smoothness);
        m.SetFloat("_DoubleSidedEnable",1);m.SetFloat("_CullMode",0);m.SetFloat("_CullModeForward",0);
        AssetDatabase.CreateAsset(m,Folder+"/"+name+".mat");return m;
    }
    public sealed class MeshMaker
    {
        public readonly List<Vector3> v=new List<Vector3>(); public readonly List<int> tri=new List<int>();public readonly List<Vector2> uv=new List<Vector2>();
        public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 n)
        {
            if(Vector3.Dot(Vector3.Cross(b-a,c-a),n)<0){var t=b;b=d;d=t;}
            int s=v.Count;v.AddRange(new[]{a,b,c,d});
            uv.AddRange(new[]{new Vector2(a.x,a.z)*.18f,new Vector2(b.x,b.z)*.18f,new Vector2(c.x,c.z)*.18f,new Vector2(d.x,d.z)*.18f});
            tri.AddRange(new[]{s,s+1,s+2,s,s+2,s+3});
        }
        public void Bar(Vector3 a,Vector3 b,float radius)
        {
            Vector3 f=(b-a).normalized;Vector3 r=Vector3.Cross(f,Mathf.Abs(f.y)>.95f?Vector3.right:Vector3.up).normalized*radius;Vector3 u=Vector3.Cross(f,r).normalized*radius;
            Quad(a-r-u,a+r-u,b+r-u,b-r-u,-u);Quad(a+r-u,a+r+u,b+r+u,b+r-u,r);
            Quad(a+r+u,a-r+u,b-r+u,b+r+u,u);Quad(a-r+u,a-r-u,b-r-u,b-r+u,-r);
        }
        public Mesh Build(string name)
        {var m=new Mesh{name=name,indexFormat=v.Count>65000?IndexFormat.UInt32:IndexFormat.UInt16};m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(tri,0);m.RecalculateNormals();m.RecalculateBounds();return m;}
    }
    static GameObject MeshObject(string name,Mesh mesh,Material mat,GameObject parent,bool collide=true)
    {
        AssetDatabase.CreateAsset(mesh,Folder+"/"+name+".asset");var g=Child(name,parent);
        g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=mat;
        if(collide)g.AddComponent<MeshCollider>().sharedMesh=mesh;
        if(name.Contains("natural shoulders"))g.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
        g.isStatic=true;return g;
    }
    static GameObject MeshObject(string name,MeshMaker mesh,Material mat,GameObject parent,bool collide=true)=>MeshObject(name,mesh.Build(name),mat,parent,collide);
    static Mesh Grid(string name,int rows,int cols,Func<int,int,Vector3> point,Func<int,Vector3> normal,float uvScale=.2f)
    {
        var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();float distance=0;
        for(int i=0;i<rows;i++)
        {
            if(i>0)distance+=Vector3.Distance(point(i,0),point(i-1,0));
            for(int j=0;j<cols;j++){v.Add(point(i,j));uv.Add(new Vector2(j/(float)(cols-1)*Vector3.Distance(point(i,0),point(i,cols-1)),distance)*uvScale);}
        }
        for(int i=0;i<rows-1;i++)for(int j=0;j<cols-1;j++)
        {
            int a=i*cols+j,b=a+cols,c=b+1,d=a+1;
            if(Vector3.Dot(Vector3.Cross(v[b]-v[a],v[d]-v[a]),normal(i))>0)t.AddRange(new[]{a,b,c,a,c,d});
            else t.AddRange(new[]{a,d,c,a,c,b});
        }
        var m=new Mesh{name=name,indexFormat=v.Count>65000?IndexFormat.UInt32:IndexFormat.UInt16};m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();return m;
    }
    static Route MakeRoute(string name,int steps,float width,Func<float,Vector3> p,Func<float,Vector3> up=null)
    {
        var r=new Route{name=name,width=width,points=new Vector3[steps+1],up=new Vector3[steps+1]};
        for(int i=0;i<=steps;i++){float t=i/(float)steps;r.points[i]=p(t);r.up[i]=up!=null?up(t):Vector3.up;}
        Routes.Add(r);return r;
    }
    static GameObject Ribbon(Route r,Material material,GameObject parent,Vector2 grip,bool markings=false)
    {
        var mesh=Grid(r.name,r.points.Length,5,(i,j)=>r.points[i]+r.Right(i)*((j/4f-.5f)*r.width),i=>r.up[i]);
        var g=MeshObject(r.name,mesh,material,parent);if(r.name!="Loop 18m" && r.name!="Loop 26m")g.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;var s=g.AddComponent<DrivingSurface>();s.forwardGrip=grip.x;s.sidewaysGrip=grip.y;
        if(r.name!="Loop 18m" && r.name!="Loop 26m" && r.name!="Long tunnel continuous road")
        {
            var shoulder=new MeshMaker();for(int i=0;i<r.points.Length-1;i++)foreach(int side in new[]{-1,1})
            {
                Vector3 a=r.points[i]+r.Right(i)*(r.width*.5f*side),b=r.points[i+1]+r.Right(i+1)*(r.width*.5f*side);
                shoulder.Quad(a,b,b+r.Right(i+1)*(2.5f*side)-Vector3.up*.65f,a+r.Right(i)*(2.5f*side)-Vector3.up*.65f,Vector3.up);
            }
            MeshObject(r.name+" natural shoulders",shoulder,gravel,parent,true);
        }
        if(markings)
        {
            var b=new MeshMaker();for(int i=0;i<r.points.Length-1;i++) foreach(int side in new[]{-1,1})
            {
                float a=(r.width*.5f-.35f)*side,bv=a+.12f;
                b.Quad(r.points[i]+r.Right(i)*a+r.up[i]*.018f,r.points[i+1]+r.Right(i+1)*a+r.up[i+1]*.018f,
                       r.points[i+1]+r.Right(i+1)*bv+r.up[i+1]*.018f,r.points[i]+r.Right(i)*bv+r.up[i]*.018f,r.up[i]);
            }
            MeshObject(r.name+" edge lines",b,amber,parent,false);
        }
        return g;
    }
    public static Vector3 Cat(Vector3[] p,float t,bool closed=false)
    {
        float f=Mathf.Clamp01(t)*(closed?p.Length:p.Length-1);int i=Mathf.Min(Mathf.FloorToInt(f),closed?p.Length-1:p.Length-2);float u=f-i;
        Func<int,Vector3> get=k=>p[closed?(k%p.Length+p.Length)%p.Length:Mathf.Clamp(k,0,p.Length-1)];
        return .5f*((2*get(i))+(-get(i-1)+get(i+1))*u+(2*get(i-1)-5*get(i)+4*get(i+1)-get(i+2))*u*u+(-get(i-1)+3*get(i)-3*get(i+1)+get(i+2))*u*u*u);
    }
    static float Smooth(float x){x=Mathf.Clamp01(x);return x*x*x*(x*(x*6-15)+10);}
    static float Rect(float x,float z,float cx,float cz,float hx,float hz,float blend=12)
    {return 1-Smooth(Mathf.Max(Mathf.Abs(x-cx)-hx,Mathf.Abs(z-cz)-hz)/blend);}
}
