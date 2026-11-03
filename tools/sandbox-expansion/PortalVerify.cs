using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PortalVerify
{
    static int count;
    static void Check(bool valid,string message)
    { if(!valid)throw new Exception(message);count++; }
    public static void Run()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity");Physics.SyncTransforms();
            ExpansionAuthoring.CreateRoute();var route=ExpansionAuthoring.Route;
            var terrain=UnityEngine.Object.FindFirstObjectByType<Terrain>();var data=terrain.terrainData;
            int n=data.holesResolution;var holes=data.GetHoles(0,0,n,n);
            float dx=data.size.x/n,dz=data.size.z/n;
            int start=Enumerable.Range(0,route.Count-1).First(i=>route[i].y+.04f < -7.2f && route[i+1].y+.04f < -7.2f);
            float roofZ=route[start].z;
            foreach(float center in new[]{100f,242f})
            {
                // Every removed terrain cell outside the intentional roadway has solid support.
                for(int z=0;z<n;z++)for(int x=0;x<n;x++)
                {
                    float wx=terrain.transform.position.x+(x+.5f)*dx;
                    float wz=terrain.transform.position.z+(z+.5f)*dz;
                    if(holes[z,x] || Mathf.Abs(wx-center)>15)continue;
                    if(Mathf.Abs(wx-center)<7 && wz< -40 && wz>roofZ)continue;
                    Check(Physics.Raycast(new Vector3(wx,2,wz),Vector3.down,out var hit,2.15f,~0,QueryTriggerInteraction.Ignore),"Unsealed terrain cell at "+wx+","+wz);
                }
                foreach(float side in new[]{-1f,1f})
                    Check(Physics.Raycast(new Vector3(center+side*8.5f,.35f,-39),Vector3.back,out var cap,2,~0,QueryTriggerInteraction.Ignore)
                        && cap.collider.name=="Tunnel walls and closed roof","Missing exposed wall end cap at "+center+" side "+side);
                foreach(float lane in new[]{-5f,0,5f})
                for(float z=-38;z>=-96;z-=.5f)
                {
                    Check(Physics.Raycast(new Vector3(center+lane,1,z),Vector3.down,out var road,15,~0,QueryTriggerInteraction.Ignore),"Unsupported entry lane");
                    if(z< -40 && z>roofZ)
                        Check(road.collider.name=="Continuous tunnel road","Entry lane obstructed at "+center+","+z);
                }
                // Look above the arch from the approach: the headwall must hide the hollow roof.
                Check(Physics.Raycast(new Vector3(center,.3f,roofZ+1),Vector3.back,out var facade,2,~0,QueryTriggerInteraction.Ignore)
                    && facade.collider.name=="Tunnel walls and closed roof","Missing arch headwall at "+center);
            }
            File.WriteAllText("TestResults/SandboxExpansion/portal-checks.txt","PASS "+count+" portal support, end-cap, headwall and unobstructed ramp checks\n");
            EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
