using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public static class FlightControls
{
    [Serializable] public class Row {public string action,keyboard,xbox,inputAction;}
    [Serializable] public class Catalog {public Row[] rows;public string[] notes;}
    [Serializable] public class Allowed {public string[] ids;}
    public static void Build()
    {
        try
        {
            Directory.CreateDirectory("TestResults/Flight");var scene=EditorSceneManager.OpenScene("Assets/_Project/Scenes/Main Menu.unity");
            var overlay=Resources.FindObjectsOfTypeAll<RectTransform>().Single(t=>t.name=="Controls overlay"&&t.gameObject.scene==scene);
            var layout=overlay.GetComponentsInChildren<RectTransform>(true).Single(t=>t.name=="Controls layout");
            var before=overlay.GetComponentsInChildren<Component>(true).Select(c=>GlobalObjectId.GetGlobalObjectIdSlow(c).targetObjectId.ToString()).Concat(overlay.GetComponentsInChildren<Transform>(true).Select(t=>GlobalObjectId.GetGlobalObjectIdSlow(t.gameObject).targetObjectId.ToString())).ToArray();
            var catalog=JsonUtility.FromJson<Catalog>(File.ReadAllText("docs/controls-catalog.json"));
            var templates=Enumerable.Range(0,3).Select(c=>layout.GetComponentsInChildren<TextMeshProUGUI>(true).Single(t=>t.name=="Control / 0 / "+c)).ToArray();
            var separator=layout.GetComponentsInChildren<Image>(true).Single(t=>t.name=="Separator / 0");
            layout.sizeDelta=new Vector2(1200,1010);
            for(int i=0;i<catalog.rows.Length;i++)
            {
                var row=catalog.rows[i];string[] values={row.action,row.keyboard.ToUpperInvariant(),row.xbox.ToUpperInvariant()};
                for(int j=0;j<3;j++)
                {
                    var text=layout.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault(t=>t.name=="Control / "+i+" / "+j);
                    if(text==null)text=Object.Instantiate(templates[j],layout);text.name="Control / "+i+" / "+j;text.text=values[j];text.rectTransform.anchoredPosition=new Vector2(j==0?0:j==1?500:900,-250-i*32);text.rectTransform.sizeDelta=new Vector2(j==0?480:j==1?390:300,31);text.raycastTarget=false;
                }
                var line=layout.GetComponentsInChildren<Image>(true).FirstOrDefault(t=>t.name=="Separator / "+i);if(line==null)line=Object.Instantiate(separator,layout);line.name="Separator / "+i;line.rectTransform.anchoredPosition=new Vector2(0,-275-i*32);
            }
            var notes=layout.GetComponentsInChildren<TextMeshProUGUI>(true).Single(t=>t.name=="Recovery note");notes.text=string.Join("\n",catalog.notes);notes.rectTransform.anchoredPosition=new Vector2(0,-790);notes.rectTransform.sizeDelta=new Vector2(1200,105);
            var back=overlay.GetComponentsInChildren<Button>(true).Single(t=>t.name=="Back to menu");back.GetComponent<RectTransform>().anchoredPosition=new Vector2(0,-930);
            EditorSceneManager.SaveScene(scene,"Assets/_FlightWork/MenuUI.unity");File.Copy("Assets/_FlightWork/MenuUI.unity","TestResults/Flight/MenuUI.unity",true);
            var after=overlay.GetComponentsInChildren<Component>(true).Select(c=>GlobalObjectId.GetGlobalObjectIdSlow(c).targetObjectId.ToString()).Concat(overlay.GetComponentsInChildren<Transform>(true).Select(t=>GlobalObjectId.GetGlobalObjectIdSlow(t.gameObject).targetObjectId.ToString()));
            File.WriteAllText("TestResults/Flight/menu-allowed.json",JsonUtility.ToJson(new Allowed{ids=before.Concat(after).Distinct().ToArray()},true));EditorApplication.Exit(0);
        }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
