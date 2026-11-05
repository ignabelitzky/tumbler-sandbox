using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public static class ControlsLightingAuthoring
{
    public static void Survey()
    {
        try
        {
            var lines=new List<string>();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Main Menu.unity");
            foreach(var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            foreach(var rect in root.GetComponentsInChildren<RectTransform>(true))
            {
                if(!PathName(rect).Contains("Controls"))continue;
                var text=rect.GetComponent<TMP_Text>();
                var grid=rect.GetComponent<GridLayoutGroup>();
                var layout=rect.GetComponent<VerticalLayoutGroup>();
                lines.Add(PathName(rect)+" active="+rect.gameObject.activeSelf+" anchor="+rect.anchorMin+"/"+rect.anchorMax+" size="+rect.sizeDelta+" pos="+rect.anchoredPosition+
                    (text!=null?" TEXT="+text.text.Replace("\n"," / ")+" fontSize="+text.fontSize+" auto="+text.enableAutoSizing:"")+
                    (grid!=null?" GRID cell="+grid.cellSize+" spacing="+grid.spacing+" constraint="+grid.constraint+":"+grid.constraintCount:"")+
                    (layout!=null?" VERTICAL spacing="+layout.spacing:""));
            }
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity");
            var car=GameObject.Find("Tumbler");
            foreach(var ps in car.GetComponentsInChildren<ParticleSystem>(true))
                lines.Add("PARTICLES "+PathName(ps.transform)+" localRoot="+car.transform.InverseTransformPoint(ps.transform.position)+" loop="+ps.main.loop+" playOnAwake="+ps.main.playOnAwake+" emission="+ps.emission.enabled+" rate="+ps.emission.rateOverTime.constant);
            Directory.CreateDirectory("TestResults/ControlsLighting");
            File.WriteAllLines("TestResults/ControlsLighting/survey.txt",lines);
            EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    [Serializable] public class Row { public string action,keyboard,xbox,inputAction; }
    [Serializable] public class Catalog { public Row[] rows;public string[] notes; }
    [Serializable] public class Preservation { public string[] menuAllowedIds, sandboxAllowedIds; public string vehicleTransformId; }
    static T Find<T>(string name) where T:Component
    {
        return Resources.FindObjectsOfTypeAll<T>().Single(c=>c.name==name && c.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(c));
    }
    static void Set(UnityEngine.Object target,string field,UnityEngine.Object value)
    { var so=new SerializedObject(target);so.FindProperty(field).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo(); }
    public static void Build()
    {
        try
        {
            Directory.CreateDirectory("TestResults/ControlsLighting");
            var catalog=JsonUtility.FromJson<Catalog>(File.ReadAllText("docs/controls-catalog.json"));
            var scene=EditorSceneManager.OpenScene("Assets/_Project/Scenes/Main Menu.unity");
            File.Copy("Assets/_Project/Scenes/Main Menu.unity", "TestResults/ControlsLighting/OriginalMenu.unity", true);
            var overlay=Find<RectTransform>("Controls overlay");
            var allowed=overlay.GetComponentsInChildren<Component>(true)
                .Select(c=>GlobalObjectId.GetGlobalObjectIdSlow(c).targetObjectId.ToString())
                .Concat(overlay.GetComponentsInChildren<Transform>(true).Select(t=>GlobalObjectId.GetGlobalObjectIdSlow(t.gameObject).targetObjectId.ToString())).ToArray();
            var layout=Find<RectTransform>("Controls layout");layout.sizeDelta=new Vector2(1200,940);
            var templates=new[]{Find<TextMeshProUGUI>("Control / 0 / 0"),Find<TextMeshProUGUI>("Control / 0 / 1"),Find<TextMeshProUGUI>("Control / 0 / 2")};
            var separator=Find<Image>("Separator / 0");
            for(int row=0;row<catalog.rows.Length;row++)
            {
                string[] values={catalog.rows[row].action,catalog.rows[row].keyboard.ToUpperInvariant(),catalog.rows[row].xbox.ToUpperInvariant()};
                for(int column=0;column<3;column++)
                {
                    string name="Control / "+row+" / "+column;
                    var existing=layout.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault(t=>t.name==name);
                    var text=existing!=null?existing:UnityEngine.Object.Instantiate(templates[column],layout);
                    text.name=name;text.text=values[column];text.fontSize=column==0?19:16;text.enableAutoSizing=false;
                    text.rectTransform.anchoredPosition=new Vector2(column==0?0:column==1?500:900,-250-row*35);
                    text.rectTransform.sizeDelta=new Vector2(column==0?480:column==1?390:300,34);
                    text.raycastTarget=false;
                }
                string separatorName="Separator / "+row;
                var image=layout.GetComponentsInChildren<Image>(true).FirstOrDefault(t=>t.name==separatorName);
                if(image==null)image=UnityEngine.Object.Instantiate(separator,layout);
                image.name=separatorName;image.rectTransform.anchoredPosition=new Vector2(0,-275-row*35);
            }
            var notes=Find<TextMeshProUGUI>("Recovery note");notes.text=string.Join("\n",catalog.notes);notes.fontSize=16;
            notes.alignment=TextAlignmentOptions.TopLeft;notes.lineSpacing=6;
            notes.rectTransform.anchorMin=notes.rectTransform.anchorMax=new Vector2(0,1);
            notes.rectTransform.pivot=new Vector2(0,1);notes.rectTransform.anchoredPosition=new Vector2(0,-766);notes.rectTransform.sizeDelta=new Vector2(1200,80);
            var back=Find<Button>("Back to menu");var br=back.GetComponent<RectTransform>();br.pivot=new Vector2(0,1);br.anchoredPosition=new Vector2(0,-875);
            Find<TextMeshProUGUI>("Column / KEYBOARD").text="KEYBOARD / MOUSE";
            Find<TextMeshProUGUI>("Controls / Intro").text="Every gameplay control. Keyboard and Xbox are available together.";
            var navigation=overlay.GetComponent<ControlsOverlayNavigation>();
            if(navigation==null)navigation=overlay.gameObject.AddComponent<ControlsOverlayNavigation>();
            Set(navigation,"layout",layout);Set(navigation,"backButton",back);Set(navigation,"controlsButton",Find<Button>("Controls"));
            EditorSceneManager.SaveScene(scene,"Assets/_ControlsLightingWork/ExpandedMenu.unity");
            File.Copy("Assets/_ControlsLightingWork/ExpandedMenu.unity","TestResults/ControlsLighting/ExpandedMenu.unity",true);
            allowed=allowed.Concat(OwnedIds(overlay.gameObject)).Distinct().ToArray();

            scene=EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity");
            File.Copy("Assets/_Project/Scenes/Sandbox.unity", "TestResults/ControlsLighting/OriginalSandbox.unity", true);
            var car=GameObject.Find("Tumbler");var transformId=GlobalObjectId.GetGlobalObjectIdSlow(car.transform).targetObjectId.ToString();
            if(car.transform.Find("Turbine fire illumination")==null)
            {
            var root=new GameObject("Turbine fire illumination");root.transform.SetParent(car.transform,false);
            var lighting=root.AddComponent<TurbineFireLighting>();
            var idle=car.transform.Find("Engine/NormalFire").GetComponent<ParticleSystem>();
            var turbo=car.GetComponent<Boost>().Particles;
            Light idleLamp=FireLight("Idle flame glow",root.transform,new Vector3(0,-.18f,-3.10f),new Color(1,.28f,.045f));
            Light turboLamp=FireLight("Turbo flame glow",root.transform,new Vector3(0,-.02f,-3.65f),new Color(1,.43f,.10f));
            Set(lighting,"idleFlame",idle);Set(lighting,"turboFlame",turbo);
            Set(lighting,"idleLight",idleLamp);Set(lighting,"turboLight",turboLamp);Set(lighting,"systems",car.GetComponent<TumblerSystems>());
            }
            EditorSceneManager.SaveScene(scene,"Assets/_ControlsLightingWork/ExpandedSandbox.unity");
            File.Copy("Assets/_ControlsLightingWork/ExpandedSandbox.unity","TestResults/ControlsLighting/ExpandedSandbox.unity",true);
            File.WriteAllText("TestResults/ControlsLighting/allowed.json",JsonUtility.ToJson(new Preservation{menuAllowedIds=allowed,vehicleTransformId=transformId,sandboxAllowedIds=OwnedIds(car.transform.Find("Turbine fire illumination").gameObject).Concat(new[]{transformId}).ToArray()},true));
            EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static string[] OwnedIds(GameObject root) => root.GetComponentsInChildren<Component>(true)
        .Select(c=>GlobalObjectId.GetGlobalObjectIdSlow(c).targetObjectId.ToString())
        .Concat(root.GetComponentsInChildren<Transform>(true).Select(t=>GlobalObjectId.GetGlobalObjectIdSlow(t.gameObject).targetObjectId.ToString())).Distinct().ToArray();
    static Light FireLight(string name,Transform parent,Vector3 local,Color color)
    {
        var g=new GameObject(name);g.transform.SetParent(parent,false);g.transform.localPosition=local;
        var l=g.AddComponent<Light>();l.type=LightType.Point;l.lightUnit=LightUnit.Lumen;
        l.color=color;l.range=7;l.intensity=0;l.enabled=false;l.shadows=LightShadows.Soft;
        l.shadowBias=.05f;l.shadowNormalBias=.15f;g.AddComponent<HDAdditionalLightData>().SetShadowResolution(256);
        return l;
    }
    static string PathName(Transform t)=>t.parent==null?t.name:PathName(t.parent)+"/"+t.name;
}
