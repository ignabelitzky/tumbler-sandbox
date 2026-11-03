using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Explicit authoring only; never regenerates or edits the Sandbox automatically.</summary>
public static class ImpactGroundBuilder
{
 static Transform root;
 static Material Mat(string n)=>AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Environment/UndergroundFacility/"+n+".mat");
 static Transform Group(string n){var g=new GameObject(n).transform;g.SetParent(root,false);return g;}
 static GameObject Box(Transform p,string n,Vector3 pos,Vector3 size,string mat="Concrete",Vector3 angles=default)
 {
  var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=size;g.transform.localEulerAngles=angles;g.GetComponent<Renderer>().sharedMaterial=Mat(mat);return g;
 }
 static void Sign(Transform p,string text,Vector3 pos,float width=30)
 {
  Box(p,"Sign backing",pos,new Vector3(width,4,.25f),"Steel");
  var t=new GameObject("Label: "+text).AddComponent<TextMeshPro>();t.transform.SetParent(p,false);t.transform.localPosition=pos+new Vector3(0,0,-.16f);t.rectTransform.sizeDelta=new Vector2(width-1,3.5f);t.text=text;t.fontSize=6;t.alignment=TextAlignmentOptions.Center;t.color=new Color(1,.68f,.18f);t.enableAutoSizing=true;t.fontSizeMin=1;t.fontSizeMax=6;
 }
 static void Paint(Transform p,string n,float x,float z,float w=10)=>Box(p,n,new Vector3(x,.012f,z),new Vector3(w,.015f,.15f),"LanePaint").GetComponent<Collider>().enabled=false;
 static void Wedge(Transform p,string n,float x,float z,float w,float len,float height)
 {
  // Closed convex wedge: slope starts flush and ends at the specified height.
  var m=new Mesh{name=n};m.vertices=new[]{new Vector3(-w/2,0,-len/2),new Vector3(w/2,0,-len/2),new Vector3(-w/2,0,len/2),new Vector3(w/2,0,len/2),new Vector3(-w/2,height,len/2),new Vector3(w/2,height,len/2)};
  m.triangles=new[]{0,2,3,0,3,1,0,1,5,0,5,4,2,4,5,2,5,3,0,4,2,1,3,5};var triangles=m.triangles;Array.Reverse(triangles);m.triangles=triangles;m.RecalculateNormals();m.RecalculateBounds();
  string path="Assets/_Project/Physics/Impact_"+n+".asset";AssetDatabase.CreateAsset(m,path);
  var g=new GameObject(n,typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));g.transform.SetParent(p,false);g.transform.localPosition=new Vector3(x,0,z);g.GetComponent<MeshFilter>().sharedMesh=m;g.GetComponent<MeshRenderer>().sharedMaterial=Mat("Concrete");g.GetComponent<MeshCollider>().sharedMesh=m;
 }
 public static void Build()
 {
  EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity");
  if(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/ImpactPhysicsTestGround.prefab")!=null)throw new Exception("Prefab already exists; refusing to overwrite");
  root=new GameObject("Impact Physics Test Ground").transform;root.gameObject.AddComponent<ImpactGroundReset>();
  var a=Group("00 Access and platform");
  Box(a,"Independent platform",new Vector3(0,-.5f,0),new Vector3(360,1,360),"Road");
  // World origin is assigned when the single prefab instance is appended to the scene.
  Box(a,"Independent access road",new Vector3(190,-.5f,170),new Vector3(20,1,80),"Road");
  Box(a,"Access turn",new Vector3(100,-.5f,190),new Vector3(200,1,20),"Road");
  Sign(a,"TUMBLER — IMPACT & PHYSICS TEST GROUND",new Vector3(0,4,-172),75);
  var low=Group("01 Low obstacle impacts");Sign(low,"01 LOW IMPACTS | FIXED | 10 / 30 / 60 km/h",new Vector3(-95,4,-165),140);
  float[] hs={.05f,.10f,.15f,.20f,.30f,.50f,.70f};
  for(int i=0;i<hs.Length;i++)
  {
   float x=-155+i*20;float h=hs[i];
   Box(low,"Vertical "+h.ToString("F2")+" m FIXED",new Vector3(x,h/2,-50),new Vector3(8,h,1));
   Wedge(low,"Bevel_"+i,x,-110,8,2,h);
   Sign(low,(h*100).ToString("F0")+" cm FIXED\nBEVEL -110 / VERTICAL -50\nStart 10 km/h",new Vector3(x+5,2.5f,-148),8);
   foreach(float z in new[]{-140f,-130f,-120f,-90f,-80f,-70f})Paint(low,"Approach reference",x,z,8);
  }
  var walls=Group("02 Rigid wall collisions");Sign(walls,"02 RIGID WALLS | FIXED | START 10 km/h",new Vector3(95,4,-165),140);
  string[] wn={"Frontal wall","Wide concrete wall","Angled 30","Angled 45","Lateral contact","Front corner"};
  for(int i=0;i<6;i++){float x=35+i*24;Box(walls,wn[i],new Vector3(x+(i==4?4:0),1.5f,-45),new Vector3(i==1?18:i==4?1:8,3,i==4?15:2),"Concrete",new Vector3(0,i==2?30:i==3?45:0,0));Sign(walls,wn[i]+"\nFIXED 10 / 30 km/h",new Vector3(x,3,-145),14);Paint(walls,"Start",x,-125);Paint(walls,"Brake reference",x,-75);}
  var mov=Group("03 Movable objects");Sign(mov,"03 MOVABLE OBJECTS | 10 / 30 km/h",new Vector3(-100,4,8),140);
  string[] names={"Light crate","Heavy crate","Barrel","Concrete block","Barricade"};float[] masses={25,250,80,900,400};
  for(int i=0;i<5;i++)
  {
   float x=-150+i*28;var g=Box(mov,names[i],new Vector3(x,.75f,70),new Vector3(1.5f,1.5f,1.5f),i<2?"Steel":"Concrete");
   if(i==2){UnityEngine.Object.DestroyImmediate(g);g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=names[i];g.transform.SetParent(mov,false);g.transform.localPosition=new Vector3(x,.75f,70);g.transform.localScale=new Vector3(1.1f,.75f,1.1f);g.GetComponent<Renderer>().sharedMaterial=Mat("Steel");}
   if(i==4){g.transform.localScale=new Vector3(5,1.5f,1);}
   var rb=g.AddComponent<Rigidbody>();rb.mass=masses[i];rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;rb.solverIterations=12;rb.solverVelocityIterations=4;
   Sign(mov,names[i]+"\nMOVABLE "+masses[i]+" kg",new Vector3(x,3,20),18);Paint(mov,"Start",x,30);
  }
  var susp=Group("04 Suspension and uneven ground");Sign(susp,"04 SUSPENSION | 10 / 30 km/h",new Vector3(95,4,8),140);
  for(int i=0;i<6;i++){Box(susp,"Progressive step "+i,new Vector3(40,.025f*(i+1),30+i*9),new Vector3(9,.05f*(i+1),2));Box(susp,"Left/right articulation "+i,new Vector3(72+(i%2==0?-1.6f:1.6f),.12f,30+i*9),new Vector3(1.3f,.24f,3));}
  // Actual depressions in an ADDED raised lane; never carve existing geometry.
  Wedge(susp,"Pothole_entry",110,30,10,12,.4f);
  for(int i=0;i<4;i++){Box(susp,"Pothole lane deck "+i,new Vector3(110,.2f,40+i*10),new Vector3(10,.4f,7));Box(susp,"Pothole bottom "+i,new Vector3(110,.025f,45+i*10),new Vector3(10,.05f,3));}
  Box(susp,"Asymmetric cross slope",new Vector3(150,1,58),new Vector3(12,.4f,55),"Concrete",new Vector3(0,0,8));
  var stab=Group("05 Weight transfer and stability");Sign(stab,"05 WEIGHT TRANSFER | BRAKE REFERENCES 10m",new Vector3(-95,4,100),150);
  for(int i=0;i<13;i++){var mark=Box(stab,"10 m braking reference "+i,new Vector3(-160+i*10,.012f,110),new Vector3(.15f,.015f,10),"LanePaint");mark.GetComponent<Collider>().enabled=false;}
  for(int i=0;i<12;i++){float angle=i*15*Mathf.Deg2Rad;Paint(stab,"Wide curve",-110+30*Mathf.Cos(angle),135+30*Mathf.Sin(angle),1);}
  for(int i=0;i<5;i++)Box(stab,"Direction marker",new Vector3(-135+i*24,.35f,125+(i%2)*24),new Vector3(.5f,.7f,.5f),"SafetyAmber");
  Box(stab,"Lateral slope 8 deg",new Vector3(-25,1.1f,140),new Vector3(14,.3f,50),"Concrete",new Vector3(0,0,8));
  var jump=Group("06 Jumps and landings");Sign(jump,"06 JUMPS | 30 km/h FIRST | LANDING + RUNOUT",new Vector3(95,4,100),140);
  Wedge(jump,"Moderate_launch",45,135,14,18,1.2f);Box(jump,"Landing",new Vector3(45,.1f,158),new Vector3(20,.2f,24));
  Wedge(jump,"Inclined_braking",95,140,14,28,2);Box(jump,"Incline crest transition",new Vector3(95,1.9f,160),new Vector3(14,.2f,12));
  Wedge(jump,"Small_drop_entry",145,125,12,10,.3f);Box(jump,"Small drop",new Vector3(145,.15f,140),new Vector3(12,.3f,20));
  PrefabUtility.SaveAsPrefabAsset(root.gameObject,"Assets/_Project/Prefabs/ImpactPhysicsTestGround.prefab");UnityEngine.Object.DestroyImmediate(root.gameObject);AssetDatabase.SaveAssets();EditorApplication.Exit(0);
 }
}
