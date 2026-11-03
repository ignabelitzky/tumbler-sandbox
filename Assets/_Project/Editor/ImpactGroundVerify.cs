using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ImpactGroundVerify
{
 static int checks;
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
 public static void Run()
 {
  try
  {
   EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity");Physics.SyncTransforms();
   var root=UnityEngine.Object.FindAnyObjectByType<ImpactGroundReset>();Check(root!=null,"Facility exists");
   Check(root.name=="Impact Physics Test Ground","Root name");
   Check(root.transform.position==new Vector3(-280,.03f,-250),"Position");
   Check(root.transform.childCount==7,"Access plus six sectors");
   Check(root.GetComponentsInChildren<Transform>().All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0),"No missing scripts");
   Check(root.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterial!=null),"Materials assigned");
   Check(root.GetComponentsInChildren<Rigidbody>().Length==5,"Five dynamic fixtures");
   var low=root.transform.Find("01 Low obstacle impacts");
   float[] hs={.05f,.1f,.15f,.2f,.3f,.5f,.7f};
   for(int i=0;i<7;i++)
   {
    var x=-435+i*20;
    Check(Physics.Raycast(new Vector3(x,3,-300),Vector3.down,out var hit,5)&&Mathf.Abs(hit.point.y-(.03f+hs[i]))<.005f,"Vertical height "+i);
    Check(hit.rigidbody==null,"Static obstacle "+i);
    Check(Physics.Raycast(new Vector3(x,3,-360),Vector3.down,out hit,5)&&Mathf.Abs(hit.point.y-(.03f+hs[i]/2))<.005f,"Bevel slope top "+i);
   }
   // Sample the interior before/after disabling the new root: only original terrain may remain.
   root.gameObject.SetActive(false);Physics.SyncTransforms();
   for(int x=-450;x<=-120;x+=15)for(int z=-420;z<=-80;z+=15)
   {
    var hits=Physics.RaycastAll(new Vector3(x,10,z),Vector3.down,12);
    Check(hits.All(h=>h.collider is TerrainCollider),"No original structure in facility cell "+x+","+z);
   }
   foreach(var p in new[]{new Vector3(-80,10,40),new Vector3(-80,10,-30),new Vector3(-180,10,-60),new Vector3(-280,10,-60)})
    Check(Physics.RaycastAll(p,Vector3.down,12).All(h=>h.collider is TerrainCollider),"Access clear");
   root.gameObject.SetActive(true);Physics.SyncTransforms();
   Directory.CreateDirectory("TestResults/ImpactPhysics");File.WriteAllText("TestResults/ImpactPhysics/fixture-checks.txt","PASS "+checks+" native asset, height, isolation and access checks\n");EditorApplication.Exit(0);
  }catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}
 }
}
