using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Cinemachine;

public static partial class ExpansionAuthoring
{
    public static void Survey()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox.unity");
            var lines = new System.Collections.Generic.List<string>();
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var rs = root.GetComponentsInChildren<Renderer>();
                Bounds bounds = rs.Length > 0 ? rs[0].bounds : new Bounds(root.transform.position, Vector3.zero);
                foreach (var r in rs.Skip(1)) bounds.Encapsulate(r.bounds);
                lines.Add(root.name + " position=" + root.transform.position + " bounds=" + bounds + " colliders=" + root.GetComponentsInChildren<Collider>().Length);
            }
            foreach (var t in UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
                lines.Add("TERRAIN " + AssetDatabase.GetAssetPath(t.terrainData) + " position=" + t.transform.position + " size=" + t.terrainData.size +
                    " heightRes=" + t.terrainData.heightmapResolution + " holesRes=" + t.terrainData.holesResolution +
                    " samples=" + string.Join(",", new[] {new Vector3(0,0,0),new Vector3(200,0,0),new Vector3(0,0,-150)}.Select(p=>t.SampleHeight(p))));
            foreach (var c in UnityEngine.Object.FindObjectsByType<CinemachineOrbitalFollow>(FindObjectsSortMode.None))
                lines.Add("CAMERA " + c.name + " id=" + GlobalObjectId.GetGlobalObjectIdSlow(c) + " style=" + c.OrbitStyle + " radius=" + c.Radius + " center=" + c.Orbits.Center.Radius + "," + c.Orbits.Center.Height);
            Directory.CreateDirectory("TestResults/SandboxExpansion");
            File.WriteAllLines("TestResults/SandboxExpansion/survey.txt", lines);
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
