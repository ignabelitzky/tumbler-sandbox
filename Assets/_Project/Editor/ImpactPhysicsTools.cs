using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class ImpactPhysicsTools
{
 public static void Inspect()
 {
  Directory.CreateDirectory("TestResults");
  EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity"); Physics.SyncTransforms();
  using(var f=new StreamWriter("TestResults/impact-inventory.txt"))
  {
   foreach(var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
   {
    var cs=root.GetComponentsInChildren<Collider>(); if(cs.Length==0)continue;
    var b=cs[0].bounds;foreach(var c in cs)b.Encapsulate(c.bounds);
    f.WriteLine(root.name+" | "+b.min+" .. "+b.max);
   }
   var rb=GameObject.Find("Tumbler").GetComponent<Rigidbody>();f.WriteLine("COM "+rb.centerOfMass+" world "+rb.worldCenterOfMass+" inertia "+rb.inertiaTensor+" rotation "+rb.inertiaTensorRotation+" CCD "+rb.collisionDetectionMode+" depen "+rb.maxDepenetrationVelocity);
   
   foreach(var w in rb.GetComponentsInChildren<WheelCollider>())f.WriteLine(w.name+" pos "+rb.transform.InverseTransformPoint(w.transform.position)+" center "+w.center+" radius "+w.radius+" travel "+w.suspensionDistance+" sprung "+w.sprungMass+" k "+w.suspensionSpring.spring+" d "+w.suspensionSpring.damper+" app "+w.forceAppPointDistance);
  }
  EditorApplication.Exit(0);
 }
}
