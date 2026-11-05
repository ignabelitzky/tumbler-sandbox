using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static partial class OrganicSandbox
{
    static void HarmonizeJunctions()
    {
        var selected=Routes.Where(r=>r.name!="Loop 18m" && r.name!="Loop 26m" && r.name!="Long tunnel continuous road").ToArray();
        var junctions=new List<Vector3>();
        for(int a=0;a<selected.Length;a++)for(int b=a+1;b<selected.Length;b++)
        {
            var ra=selected[a];var rb=selected[b];
            for(int i=0;i<ra.points.Length;i+=8)for(int j=0;j<rb.points.Length;j+=8)
            {
                Vector3 pa=ra.points[i],pb=rb.points[j];float d=Vector2.Distance(new Vector2(pa.x,pa.z),new Vector2(pb.x,pb.z));
                if(d>(ra.width+rb.width)*.5f+2)continue;
                Vector3 center=(pa+pb)*.5f;center.y=Mathf.Min(pa.y,pb.y);
                if(!junctions.Any(p=>Vector2.Distance(new Vector2(p.x,p.z),new Vector2(center.x,center.z))<17))junctions.Add(center);
            }
        }
        foreach(var r in selected)
        {
            for(int i=0;i<r.points.Length;i++)
            {
                Vector3 p=r.points[i];float weight=0,alt=0;
                foreach(var center in junctions)
                {float d=Vector2.Distance(new Vector2(p.x,p.z),new Vector2(center.x,center.z));float w=1-Smooth((d-23)/30);weight+=w;alt+=w*center.y;}
                if(weight>0)p.y=Mathf.Lerp(p.y,alt/weight,Mathf.Clamp01(weight));r.points[i]=p;
            }
            var mesh=Grid(r.name,r.points.Length,5,(i,j)=>r.points[i]+r.Right(i)*((j/4f-.5f)*r.width),i=>r.up[i]);
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/"+r.name+".asset");EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);
            var shoulder=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/"+r.name+" natural shoulders.asset");
            if(shoulder!=null)
            {
                var edge=new MeshMaker();for(int i=0;i<r.points.Length-1;i++)foreach(int side in new[]{-1,1})
                {var a=r.points[i]+r.Right(i)*(r.width*.5f*side);var b=r.points[i+1]+r.Right(i+1)*(r.width*.5f*side);edge.Quad(a,b,b+r.Right(i+1)*(2.5f*side)-Vector3.up*.65f,a+r.Right(i)*(2.5f*side)-Vector3.up*.65f,Vector3.up);}
                var replacement=edge.Build(r.name+" shoulders");EditorUtility.CopySerialized(replacement,shoulder);Object.DestroyImmediate(replacement);EditorUtility.SetDirty(shoulder);
            }
            var oldPaint=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/"+r.name+" edge lines.asset");
            if(oldPaint!=null)
            {
                var mark=new MeshMaker();for(int i=0;i<r.points.Length-1;i++)foreach(int side in new[]{-1,1})
                {float edge=(r.width*.5f-.35f)*side;mark.Quad(r.points[i]+r.Right(i)*edge+r.up[i]*.018f,r.points[i+1]+r.Right(i+1)*edge+r.up[i+1]*.018f,r.points[i+1]+r.Right(i+1)*(edge+.12f)+r.up[i+1]*.018f,r.points[i]+r.Right(i)*(edge+.12f)+r.up[i]*.018f,r.up[i]);}
                var replacement=mark.Build(r.name+" edge lines");EditorUtility.CopySerialized(replacement,oldPaint);Object.DestroyImmediate(replacement);EditorUtility.SetDirty(oldPaint);
            }
        }
    }
}
