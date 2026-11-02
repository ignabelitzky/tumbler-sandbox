using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Installed temporarily under Assets by release.py. Excluded from Players.
public static class LocalReleaseBuild
{
    public static void Run()
    {
        try
        {
            if (Application.unityVersion != "6000.6.7f1")
                throw new Exception("Install the exact repository Editor version.");
            var target = Environment.GetEnvironmentVariable("TUMBLER_PLATFORM") == "linux-x64"
                ? BuildTarget.StandaloneLinux64 : BuildTarget.StandaloneWindows64;
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
                throw new Exception("Install the target Build Support module in Unity Hub.");
            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone) != ScriptingImplementation.Mono2x)
                throw new Exception("The Fedora release path requires Mono. Review Player settings separately.");
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0 || scenes.Any(s => !File.Exists(s)))
                throw new Exception("Missing enabled build scenes.");
            var output = Environment.GetEnvironmentVariable("TUMBLER_OUTPUT");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = scenes, target = target, locationPathName = output, options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded || report.summary.totalErrors != 0)
                throw new Exception("Player build failed: " + report.summary.result);
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(output), "build-complete.txt"), Application.unityVersion);
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }
}
