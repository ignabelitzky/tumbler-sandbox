using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class ControlsLightingVerify
{
    const string Flag="ControlsLightingVerify";
    const string Folder="TestResults/ControlsLighting";
    static readonly List<string> checks=new List<string>();
    static int stage,lastFrame;
    static double next,started;
    static Keyboard keyboard;
    static Gamepad pad;
    static InputSettings originalSettings,restoredSettings,testSettings;
    static TurbineFireLighting lighting;
    static TumblerSystems systems;
    static Boost boost;
    static float idlePeak,turboPeak,pausedIdle,pausedTurbo;
    static bool settingsInstalled;
    static ControlsLightingVerify(){EditorApplication.playModeStateChanged+=State;EditorApplication.update+=Tick;}
    static void Check(bool value,string label)
    { if(!value)throw new Exception(label);checks.Add("PASS "+label);File.WriteAllLines(Folder+"/checks.txt",checks); }
    static T Find<T>(string name) where T:Component =>
        Object.FindObjectsByType<T>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(c=>c.name==name);
    public static void Run()
    {
        Directory.CreateDirectory(Folder);File.Delete(Folder+"/failed.txt");File.Delete(Folder+"/complete.txt");File.Delete(Folder+"/checks.txt");
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Main Menu.unity");
        SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();
    }
    static void State(PlayModeStateChange state)
    { if(SessionState.GetBool(Flag,false) && state==PlayModeStateChange.EnteredPlayMode){stage=0;lastFrame=-1;started=EditorApplication.timeSinceStartup;} }
    static void Keys(params Key[] keys){InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));InputSystem.Update();}
    static void Pad(GamepadState state){InputSystem.QueueStateEvent(pad,state);InputSystem.Update();}
    static void SetupInput()
    {
        originalSettings=InputSystem.settings;restoredSettings=Object.Instantiate(originalSettings);testSettings=Object.Instantiate(originalSettings);
        InputSystem.settings=testSettings;testSettings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;
        testSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        testSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        keyboard=InputSystem.AddDevice<Keyboard>();pad=InputSystem.AddDevice<Gamepad>();settingsInstalled=true;
    }
    static void RestoreInput()
    {
        if(!settingsInstalled)return;
        InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(pad);
        InputSystem.settings=originalSettings!=null?originalSettings:restoredSettings;
        if(InputSystem.settings!=restoredSettings)Object.DestroyImmediate(restoredSettings);
        Object.DestroyImmediate(testSettings);settingsInstalled=false;
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying||Time.frameCount<8)return;
        if(Time.frameCount==lastFrame)return;lastFrame=Time.frameCount;
        if(EditorApplication.timeSinceStartup-started>90){Fail(new Exception("Controls/light verification timed out at "+stage));return;}
        if(EditorApplication.timeSinceStartup<next)return;
        try
        {
            switch(stage++)
            {
                case 0:
                    SetupInput();Find<Button>("Controls").onClick.Invoke();
                    var data=JsonUtility.FromJson<ControlsLightingAuthoring.Catalog>(File.ReadAllText("docs/controls-catalog.json"));
                    for(int row=0;row<data.rows.Length;row++)
                    {
                        var values=new[]{data.rows[row].action,data.rows[row].keyboard.ToUpperInvariant(),data.rows[row].xbox.ToUpperInvariant()};
                        for(int col=0;col<3;col++)
                        {
                            var text=Find<TextMeshProUGUI>("Control / "+row+" / "+col);text.ForceMeshUpdate();
                            Check(text.text==values[col],"Visible controls match catalog row "+row+" column "+col);
                            Check(text.preferredWidth<=text.rectTransform.rect.width+1 && text.preferredHeight<=text.rectTransform.rect.height+1,
                                "Control label fits its cell "+row+"/"+col);
                        }
                    }
                    Check(Find<RectTransform>("Controls overlay").gameObject.activeInHierarchy,"Controls button opens the complete page");
                    Check(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject==Find<Button>("Back to menu").gameObject,"Controls page selects its Back button for gamepad/keyboard");
                    Keys(Key.Escape);break;
                case 1:
                    Keys();Check(!Find<RectTransform>("Controls overlay").gameObject.activeInHierarchy,"Escape returns from Controls");
                    Find<Button>("Controls").onClick.Invoke();Pad(new GamepadState().WithButton(GamepadButton.East));break;
                case 2:
                    Pad(new GamepadState());Check(!Find<RectTransform>("Controls overlay").gameObject.activeInHierarchy,"Xbox B returns from Controls");
                    RestoreInput();SceneManager.LoadScene("Sandbox");next=EditorApplication.timeSinceStartup+.8;break;
                case 3:
                    lighting=Object.FindFirstObjectByType<TurbineFireLighting>();
                    systems=Object.FindFirstObjectByType<TumblerSystems>();boost=Object.FindFirstObjectByType<Boost>();
                    var orbitInput=Object.FindFirstObjectByType<Unity.Cinemachine.CinemachineInputAxisController>();
                    var look=orbitInput.Controllers.First(c=>c.Name=="Look Orbit X").Input.InputAction.action;
                    Check(look.bindings.Any(b=>b.path=="<Gamepad>/rightStick") && look.bindings.Any(b=>b.path=="<Pointer>/delta"),
                        "Camera orbit guide matches actual Cinemachine mouse/right-stick bindings");
                    Check(lighting!=null && lighting.IdleFlame.name=="NormalFire" && lighting.TurboFlame==boost.Particles,"Lighting references actual normal/turbo flame emitters, not smoke");
                    Check(lighting.GetComponentsInChildren<Light>().Length==2,"Only two bounded turbine light sources");
                    Check(lighting.IdleLight.lightUnit==UnityEngine.Rendering.LightUnit.Lumen && lighting.TurboLight.lightUnit==UnityEngine.Rendering.LightUnit.Lumen,"Turbine intensity uses explicit lumen units");
                    Check(lighting.IdleLight.shadows!=LightShadows.None && lighting.TurboLight.shadows!=LightShadows.None,"Both flames cast real environmental shadows");
                    lighting.IdleFlame.Play(true);lighting.IdleFlame.Simulate(1,true,false,false);lighting.IdleFlame.Play(true);
                    next=EditorApplication.timeSinceStartup+.5;break;
                case 4:
                    idlePeak=lighting.IdleLight.intensity;
                    Check(lighting.IdleLight.enabled && idlePeak>10 && idlePeak<=500,"Visible idle flame produces a bounded warm glow");
                    Check(!lighting.TurboLight.enabled && lighting.TurboLight.intensity==0,"Idle does not create turbo light");
                    boost.enabled=false;
                    typeof(Boost).GetMethod("AdvanceReserve",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(boost,new object[]{.05f,true,false});
                    lighting.TurboFlame.Simulate(.6f,true,false,false);lighting.TurboFlame.Play(true);
                    next=EditorApplication.timeSinceStartup+.5;break;
                case 5:
                    turboPeak=lighting.TurboLight.intensity;
                    Check(lighting.TurboLight.enabled && turboPeak>idlePeak*2 && turboPeak<2700,"Turbo flame produces a stronger bounded glow");
                    Check(lighting.TurboLight.range>lighting.IdleLight.range,"Turbo illumination reaches farther than idle");
                    typeof(Boost).GetMethod("AdvanceReserve",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(boost,new object[]{.02f,false,false});
                    lighting.SendMessage("LateUpdate");
                    Check(!boost.IsBoostActive && lighting.TurboFlame.particleCount>0 && lighting.TurboLight.enabled,
                        "Remaining flame particles keep glow after releasing turbo");
                    typeof(GameNavigation).GetProperty("IsPaused").SetValue(null,true);Time.timeScale=0;
                    pausedIdle=lighting.IdleLight.intensity;pausedTurbo=lighting.TurboLight.intensity;
                    next=EditorApplication.timeSinceStartup+.3;break;
                case 6:
                    Check(lighting.IdleLight.intensity==pausedIdle && lighting.TurboLight.intensity==pausedTurbo,"Pause freezes flame lighting without flicker");
                    systems.SetStealth(true);lighting.SendMessage("LateUpdate");
                    Check(!lighting.IdleLight.enabled && !lighting.TurboLight.enabled && lighting.IdleLight.intensity==0 && lighting.TurboLight.intensity==0,"Stealth extinguishes both lights even while paused");
                    typeof(GameNavigation).GetProperty("IsPaused").SetValue(null,false);Time.timeScale=1;
                    systems.SetStealth(false);
                    lighting.IdleFlame.Play(true);lighting.IdleFlame.Simulate(1,true,false,false);lighting.IdleFlame.Play(true);
                    next=EditorApplication.timeSinceStartup+.5;break;
                case 7:
                    Check(lighting.IdleLight.enabled,"Exiting stealth restores lighting when normal flame is visible");
                    Check(!lighting.TurboLight.enabled,"Exiting stealth does not resurrect a stale turbo glow");
                    lighting.IdleFlame.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                    lighting.TurboFlame.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                    next=EditorApplication.timeSinceStartup+.1;break;
                case 8:
                    Check(!lighting.IdleLight.enabled && !lighting.TurboLight.enabled,"No flame particles means no turbine glow");
                    lighting.IdleFlame.Play(true);lighting.IdleFlame.Simulate(.5f,true,false,false);lighting.IdleFlame.Play(true);
                    next=EditorApplication.timeSinceStartup+.3;break;
                case 9:
                    lighting.enabled=false;
                    Check(!lighting.IdleLight.enabled && !lighting.TurboLight.enabled,"Disabling lighting clears both sources immediately");
                    File.WriteAllText(Folder+"/complete.txt","PASS "+checks.Count+" native controls and turbine-light assertions");
                    SessionState.SetBool(Flag,false);EditorApplication.Exit(0);break;
            }
        }
        catch(Exception e){Fail(e);}
    }
    static void Fail(Exception e)
    { RestoreInput();SessionState.SetBool(Flag,false);Time.timeScale=1;Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/failed.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1); }
}
