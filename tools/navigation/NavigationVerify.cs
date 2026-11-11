using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Copied into a temporary Editor folder by verify-navigation.py; never shipped in the Player.
[InitializeOnLoad]
public static class NavigationVerify
{
    const string Flag = "TumblerNavigationVerification";
    static string Folder => Environment.GetEnvironmentVariable("TUMBLER_NAVIGATION_RESULTS") ?? "TestResults/Navigation";
    static readonly Queue<(Func<bool> Ready, Action Run)> Steps = new Queue<(Func<bool>, Action)>();
    static double started;
    static int lastFrame;
    static InputSettings originalInputSettings, testInputSettings;
    static Keyboard keyboard;
    static Gamepad pad;
    static PauseMenuController pause;
    static TumblerController car;
    static TumblerSystems systems;
    static Boost boost;
    static Rigidbody body;
    static Vector3 pausedPosition;
    static TumblerController.driveType pausedMode;
    static TumblerSystems.LightMode pausedLights;
    static bool pausedStealth;
    static float pausedReserve;

    static NavigationVerify()
    {
        EditorApplication.playModeStateChanged += State;
        EditorApplication.update += Tick;
        Application.logMessageReceived += Log;
    }

    public static void Run()
    {
        // Let startup delay calls (including Search index initialization) finish
        // before attributing Editor logs to the gameplay test session.
        double requested = EditorApplication.timeSinceStartup;
        EditorApplication.CallbackFunction beginWhenIdle = null;
        beginWhenIdle = () =>
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.timeSinceStartup - requested < 3) return;
            EditorApplication.update -= beginWhenIdle;
            Begin();
        };
        EditorApplication.update += beginWhenIdle;
    }

    static void Begin()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            foreach (var name in new[] { "checks.txt", "failed.txt", "complete.txt" }) File.Delete(Path.Combine(Folder, name));
            Check(Application.unityVersion == "6000.6.7f1", "Exact Unity 6000.6.7f1 editor");
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Check(scenes.SequenceEqual(new[] { "Intro", "Main Menu", "Sandbox" }.Select(s => "Assets/_Project/Scenes/" + s + ".unity")), "Enabled scene order");
            SessionState.SetString(Flag, "menu");
            StartPlay();
        }
        catch (Exception error) { Fail(error); }
    }

    static void StartPlay()
    {
        SessionState.SetBool(Flag + "ExitExpected", false);
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Intro.unity");
        started = EditorApplication.timeSinceStartup;
        EditorApplication.EnterPlaymode();
    }

    static void State(PlayModeStateChange state)
    {
        if (string.IsNullOrEmpty(SessionState.GetString(Flag, ""))) return;
        try
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                started = EditorApplication.timeSinceStartup;
                lastFrame = -1;
                originalInputSettings = InputSystem.settings;
                testInputSettings = Object.Instantiate(originalInputSettings);
                InputSystem.settings = testInputSettings;
                testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                testInputSettings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
                PrepareSteps(SessionState.GetString(Flag, "") == "menu");
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                if (originalInputSettings != null) InputSystem.settings = originalInputSettings;
                if (testInputSettings != null) Object.DestroyImmediate(testInputSettings);
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                Check(SessionState.GetBool(Flag + "ExitExpected", false), "Only explicit Exit stops Play Mode");
                if (SessionState.GetString(Flag, "") == "menu")
                {
                    SessionState.SetString(Flag, "sandbox");
                    EditorApplication.delayCall += StartPlay;
                }
                else
                {
                    SessionState.EraseString(Flag);
                    File.WriteAllText(Path.Combine(Folder, "complete.txt"), "PASS real Unity Play Mode navigation; Player execution still pending.\n");
                    EditorApplication.Exit(0);
                }
            }
        }
        catch (Exception error) { Fail(error); }
    }

    static void Step(Action action) => Steps.Enqueue((() => true, action));
    static void Wait(string scene, Action action) => Steps.Enqueue((() => SceneManager.GetActiveScene().name == scene, action));
    static T Find<T>() where T : Object => Object.FindFirstObjectByType<T>();
    static Button Button(string name) => Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(b => b.name == name);
    static void Keys(params Key[] keys) { InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); InputSystem.Update(); }
    static void Pad(GamepadState value) { InputSystem.QueueStateEvent(pad, value); InputSystem.Update(); }
    static GamepadState Press(GamepadButton button) => new GamepadState().WithButton(button);

    static void PrepareSteps(bool menuExitRun)
    {
        Steps.Clear();
        Steps.Enqueue((() => Find<IntroController>() != null && EditorApplication.timeSinceStartup - started > .8, () =>
        {
            keyboard = InputSystem.AddDevice<Keyboard>();
            pad = InputSystem.AddDevice<Gamepad>();
            var signature = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None).Single(t => t.name == "Developer Signature");
            Check(signature.text == "Developed by Ignacio Belitzky", "Exact developer signature");
            Check(AssetDatabase.GetAssetPath(signature.font.sourceFontFile).EndsWith("Phosphor/Inter-Regular.ttf"), "Original Inter source font used by TMP");
            var logo = Object.FindObjectsByType<RawImage>(FindObjectsSortMode.None).Single(i => i.name == "Phosphor Original");
            Check(logo.rectTransform.sizeDelta.x == logo.rectTransform.sizeDelta.y, "Square logo without distortion");
            Check(AssetDatabase.GetAssetPath(logo.texture).EndsWith("Phosphor/phosphor-primary_2048.png"), "Original PNG reference loaded");
            if (!menuExitRun) Keys(Key.Space);
        }));
        Wait("Main Menu", () =>
        {
            Check(Find<IntroController>() == null && Time.timeScale == 1f && !GameNavigation.IsPaused, "Intro unloaded and Main Menu normalized");
            if (menuExitRun) Check(EditorApplication.timeSinceStartup - started >= 3.3, "Natural intro duration");
            Keys();
            Check(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1, "One Main Menu EventSystem");
        });
        if (menuExitRun)
        {
            Step(() =>
            {
                var module = EventSystem.current.GetComponent<InputSystemUIInputModule>();
                Check(module.actionsAsset.name.StartsWith("MenuInput") && module.actionsAsset.devices == null &&
                    module.actionsAsset.bindingMask == null, "Dedicated UI actions are not restricted to a paired device");
                Check(!module.deselectOnBackgroundClick, "Background click cannot deselect menu navigation");
                Check(EventSystem.current.currentSelectedGameObject == Button("Enter Sandbox").gameObject, "Controller-only initial selection");
                var label = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None).Single(t => t.name == "Developer Credit");
                Check(label.text == "<size=14>Developed by</size>\n<b>Ignacio Belitzky</b>" && !label.raycastTarget, "Correct two-line non-interactive developer credit");
                Check(label.alignment == TextAlignmentOptions.Center && label.lineSpacing == 6f,
                    "Developer credit is internally centered with a modest extra line gap");
                var icon = Object.FindObjectsByType<RawImage>(FindObjectsSortMode.None).Single(i => i.name == "Phosphor Icon");
                Check(AssetDatabase.GetAssetPath(icon.texture).EndsWith("Phosphor/phosphor-primary_2048.png") &&
                    icon.rectTransform.sizeDelta.x == icon.rectTransform.sizeDelta.y && !icon.raycastTarget, "Original square Phosphor icon");
                var credit = (RectTransform)label.transform.parent;
                Check(credit.anchorMin == new Vector2(.067f, 0) && credit.anchorMax == credit.anchorMin &&
                    credit.pivot == Vector2.zero && credit.anchoredPosition.x == 0 && credit.anchoredPosition.y > 0, "Credit replaces the bottom-left author label");
                var oldAuthor = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(t => t.name == "Footer / Author");
                Check(!oldAuthor.gameObject.activeInHierarchy && string.IsNullOrEmpty(oldAuthor.text), "Old standalone author label removed");
                Pad(Press(GamepadButton.DpadDown));
            });
            Step(() => { Check(EventSystem.current.currentSelectedGameObject == Button("Controls").gameObject, "D-pad selects Controls"); Pad(new GamepadState()); });
            Step(() => Pad(Press(GamepadButton.South)));
            Step(() => { Check(Button("Back to menu").gameObject.activeInHierarchy && EventSystem.current.currentSelectedGameObject == Button("Back to menu").gameObject, "A opens Controls and selects Back"); Pad(new GamepadState()); });
            Step(() => Pad(Press(GamepadButton.East)));
            Step(() => { Check(Button("Enter Sandbox").gameObject.activeInHierarchy && EventSystem.current.currentSelectedGameObject == Button("Controls").gameObject, "B closes Controls and restores selection"); Pad(new GamepadState()); });
            Step(() => EventSystem.current.SetSelectedGameObject(null));
            Step(() => Check(EventSystem.current.currentSelectedGameObject == Button("Controls").gameObject, "Lost selection recovers without mouse or keyboard input"));
            Mouse mouse = null;
            Step(() =>
            {
                mouse = InputSystem.AddDevice<Mouse>();
                InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(Screen.width / 2f, 4) }.WithButton(MouseButton.Left));
                InputSystem.Update();
            });
            Step(() => { Check(EventSystem.current.currentSelectedGameObject == Button("Controls").gameObject, "Mouse background click preserves controller selection"); InputSystem.RemoveDevice(mouse); });
            Step(() => Keys(Key.DownArrow));
            Step(() => { Check(EventSystem.current.currentSelectedGameObject == Button("Exit").gameObject, "Keyboard arrows navigate after controller and mouse"); Keys(); });
            Step(() => Keys(Key.UpArrow));
            Step(() => { Check(EventSystem.current.currentSelectedGameObject == Button("Controls").gameObject, "Keyboard can restore Controls selection"); Keys(); });
            Step(() => { InputSystem.RemoveDevice(pad); pad = InputSystem.AddDevice<Gamepad>(); Pad(new GamepadState()); });
            Step(() => Pad(new GamepadState { leftStick = Vector2.up }));
            Step(() => { Check(EventSystem.current.currentSelectedGameObject == Button("Enter Sandbox").gameObject, "Reconnected gamepad navigates immediately with stick"); Pad(new GamepadState()); });
            Step(() => Pad(Press(GamepadButton.DpadDown)));
            Step(() => Pad(new GamepadState()));
            Step(() => Pad(Press(GamepadButton.South)));
            Step(() => { Check(Button("Back to menu").gameObject.activeInHierarchy, "Reconnected controller submits Controls"); Pad(new GamepadState()); });
            Step(() => Pad(Press(GamepadButton.South)));
            Step(() => { Check(Button("Enter Sandbox").gameObject.activeInHierarchy, "A activates Back without immediately reopening Controls"); Pad(new GamepadState()); });
            Step(() => Pad(Press(GamepadButton.DpadDown)));
            Step(() => { Check(EventSystem.current.currentSelectedGameObject == Button("Exit").gameObject, "Controller selects explicit Exit"); Pad(new GamepadState()); });
            Step(() => { SessionState.SetBool(Flag + "ExitExpected", true); Pad(Press(GamepadButton.South)); });
            return;
        }
        Step(() => Pad(Press(GamepadButton.South)));
        Wait("Sandbox", () =>
        {
            Find<PlayerInput>().actions.devices = new InputDevice[] { keyboard, pad };
            pause = Find<PauseMenuController>(); car = Find<TumblerController>(); systems = Find<TumblerSystems>();
            boost = Find<Boost>(); body = car.GetComponent<Rigidbody>();
            Check(pause != null && Object.FindObjectsByType<PauseMenuController>(FindObjectsSortMode.None).Length == 1, "One pause owner");
            Check(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1, "One Sandbox EventSystem");
            Check(Find<IntroController>() == null, "Sandbox never replays Intro");
        });
        Step(() => { Check(!boost.IsBoostActive, "Menu Submit cannot leak into turbo"); Pad(new GamepadState()); });
        Step(() => Keys(Key.W));
        Step(() => Keys(Key.W, Key.Escape));
        Step(() =>
        {
            Check(pause.IsPaused && Time.timeScale == 0 && AudioListener.pause, "Escape pauses physics and audio instead of exiting");
            Check(EventSystem.current.currentSelectedGameObject == Button("Resume").gameObject, "Resume receives focus");
            pausedPosition = body.position; pausedReserve = boost.Reserve01; pausedLights = systems.SelectedLights;
            pausedStealth = systems.IsStealth; pausedMode = car.currentDriveMode;
            Keys(Key.R, Key.F, Key.E, Key.L, Key.V, Key.LeftShift);
        });
        Step(() =>
        {
            Check(body.position == pausedPosition && boost.Reserve01 == pausedReserve && systems.SelectedLights == pausedLights &&
                systems.IsStealth == pausedStealth && car.currentDriveMode == pausedMode, "Paused driving/reset/flip/lighting/stealth/turbo inputs have no effects");
            Keys(); Pad(Press(GamepadButton.Start));
        });
        Step(() => { Check(pause.IsPaused, "Resume waits for Start release"); Pad(new GamepadState()); });
        Step(() => Check(!pause.IsPaused && Time.timeScale == 1 && !AudioListener.pause, "Start resumes and restores state"));
        Step(() => Pad(Press(GamepadButton.Start)));
        Step(() => Pad(new GamepadState()));
        Step(() => Pad(Press(GamepadButton.East)));
        Step(() => { Check(pause.IsPaused, "B resume waits for Cancel release"); Pad(new GamepadState()); });
        Step(() => Check(!pause.IsPaused, "B returns from pause without leaking held input"));
        Step(() => Pad(Press(GamepadButton.Start)));
        Step(() => { Check(pause.IsPaused, "Start opens pause"); Pad(new GamepadState()); });
        Step(() => Pad(new GamepadState { leftStick = Vector2.down }));
        Step(() => { Check(EventSystem.current.currentSelectedGameObject == Button("Main Menu").gameObject, "Stick navigates to Main Menu"); Pad(new GamepadState()); });
        Step(() => Pad(Press(GamepadButton.South)));
        Wait("Main Menu", () =>
        {
            Check(Find<PauseMenuController>() == null && Find<TumblerController>() == null && Find<IntroController>() == null,
                "Return unloads Sandbox with no Intro replay");
            Check(Time.timeScale == 1 && !AudioListener.pause && Cursor.visible && Cursor.lockState == CursorLockMode.None, "Menu restores time/audio/cursor");
            Pad(new GamepadState());
        });
        Step(() => Pad(Press(GamepadButton.South)));
        Wait("Sandbox", () => { Find<PlayerInput>().actions.devices = new InputDevice[] { keyboard, pad }; pause = Find<PauseMenuController>(); Check(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1, "Re-entry has no duplicate EventSystems"); Keys(Key.Escape); });
        Step(() => { Keys(); Pad(new GamepadState()); });
        Step(() => Pad(Press(GamepadButton.South)));
        Step(() => { Check(pause.IsPaused, "Resume Submit held cannot drive vehicle"); Pad(new GamepadState()); });
        Step(() => { Check(!pause.IsPaused, "Resume button restores driving"); Keys(Key.Escape); });
        Step(() => { Keys(); Pad(Press(GamepadButton.DpadUp)); });
        Step(() => { Check(EventSystem.current.currentSelectedGameObject == Button("Exit Game").gameObject, "Pause D-pad selects Exit Game"); Pad(new GamepadState()); });
        Step(() => { SessionState.SetBool(Flag + "ExitExpected", true); Pad(Press(GamepadButton.South)); });
    }

    static void Tick()
    {
        if (string.IsNullOrEmpty(SessionState.GetString(Flag, "")) || !EditorApplication.isPlaying) return;
        try
        {
            if (EditorApplication.timeSinceStartup - started > 180) throw new Exception("Navigation verification timed out");
            if (Time.frameCount == lastFrame || Steps.Count == 0 || !Steps.Peek().Ready()) return;
            lastFrame = Time.frameCount;
            InputSystem.Update();
            Steps.Dequeue().Run();
        }
        catch (Exception error) { Fail(error); }
    }

    static void Check(bool valid, string description)
    {
        if (!valid) throw new Exception(description);
        File.AppendAllText(Path.Combine(Folder, "checks.txt"), "PASS " + description + "\n");
    }
    static void Log(string message, string trace, LogType type)
    {
        if (!string.IsNullOrEmpty(SessionState.GetString(Flag, "")) && (type == LogType.Exception || type == LogType.Error))
            Fail(new Exception(message + "\n" + trace));
    }
    static void Fail(Exception error)
    {
        SessionState.EraseString(Flag);
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "failed.txt"), error.ToString());
        EditorApplication.Exit(1);
    }

    public static void Build()
    {
        var requested = Environment.GetEnvironmentVariable("TUMBLER_NAVIGATION_BUILD_TARGET");
        if (string.IsNullOrEmpty(requested))
        {
            // GameCI forwards its platform as a command-line argument, not a host environment variable.
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-customBuildTarget");
            requested = index >= 0 && index + 1 < args.Length ? args[index + 1] : "Linux";
        }
        BuildTarget target;
        switch (requested)
        {
            case "Windows": case "StandaloneWindows64": target = BuildTarget.StandaloneWindows64; break;
            case "Linux": case "StandaloneLinux64": target = BuildTarget.StandaloneLinux64; break;
            default: throw new Exception("Unsupported navigation build target: " + requested);
        }
        if (Application.unityVersion != "6000.6.7f1") throw new Exception("Use exact Unity 6000.6.7f1");
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
            throw new Exception("Install the matching Unity standalone build module: " + target);
        var output = target == BuildTarget.StandaloneWindows64 ? "Builds/NavigationSmoke/Windows/Tumbler Sandbox.exe" : "Builds/NavigationSmoke/Linux/TumblerSandbox.x86_64";
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            target = target, options = BuildOptions.Development,
            locationPathName = output
        });
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "build.json"), JsonUtility.ToJson(new CompilationReport {
            unity = Application.unityVersion, platform = target.ToString(), output = output,
            result = result.summary.result.ToString(), errors = result.summary.totalErrors,
            bytes = result.summary.totalSize.ToString(), playerExecution = "NOT_RUN"
        }, true));
        EditorApplication.Exit(result.summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    [Serializable]
    sealed class CompilationReport
    {
        public string unity, platform, output, result, bytes, playerExecution;
        public int errors;
    }
}
