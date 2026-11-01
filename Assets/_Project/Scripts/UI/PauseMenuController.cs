using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>A scene-local pause overlay. Vehicle input wrappers stay alive, but their consumers are gated.</summary>
[DefaultExecutionOrder(-100)]
public sealed class PauseMenuController : MonoBehaviour
{
    [SerializeField] private TMP_FontAsset menuFont;
    public static PauseMenuController Instance { get; private set; }
    public bool IsPaused => GameNavigation.IsPaused;
    private Canvas canvas;
    private EventSystem events;
    private InputSystemUIInputModule uiModule;
    private Button resumeButton;
    private bool resumePending, previousCursorVisible, previousAudioPause, previousModuleEnabled;
    private bool ownsEventSystem, addedModule;
    private float previousTimeScale;
    private CursorLockMode previousCursorLock;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        GameNavigation.RestoreSceneState();
        GameNavigation.WaitingForMenuRelease = ResumeButtonsHeld();
        BuildMenu();
    }

    private void BuildMenu()
    {
        canvas = RuntimeMenuUI.Canvas("Pause Menu", gameObject.scene);
        var shade = RuntimeMenuUI.Panel("Shade", canvas.transform, new Color(0, 0, 0, .72f),
            Vector2.zero, Vector2.zero);
        RuntimeMenuUI.Stretch(shade.rectTransform);
        var panel = RuntimeMenuUI.Panel("Panel", shade.transform, new Color(.055f, .065f, .075f, .98f),
            new Vector2(600, 480), Vector2.zero);
        RuntimeMenuUI.Text("Title", panel.transform, menuFont, "PAUSED", 36,
            new Color(.84f, .64f, .34f), new Vector2(500, 60), new Vector2(0, 174));
        RuntimeMenuUI.Text("Subtitle", panel.transform, menuFont, "TUMBLER SANDBOX", 16,
            new Color(.56f, .61f, .66f), new Vector2(500, 30), new Vector2(0, 127));
        resumeButton = RuntimeMenuUI.Button("Resume", panel.transform, menuFont, 44, Resume);
        var mainMenu = RuntimeMenuUI.Button("Main Menu", panel.transform, menuFont, -36, ReturnToMainMenu);
        var exit = RuntimeMenuUI.Button("Exit Game", panel.transform, menuFont, -116, ExitGame);
        var buttons = new[] { resumeButton, mainMenu, exit };
        for (int i = 0; i < buttons.Length; i++)
        {
            var navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnUp = buttons[(i + 2) % 3], selectOnDown = buttons[(i + 1) % 3] };
            buttons[i].navigation = navigation;
        }
        RuntimeMenuUI.Text("Hint", panel.transform, menuFont, "ESC / START  ·  RESUME", 16,
            new Color(.56f, .61f, .66f), new Vector2(500, 40), new Vector2(0, -198));
        canvas.gameObject.SetActive(false);

        events = FindFirstObjectByType<EventSystem>();
        if (events == null)
        {
            var root = new GameObject("Pause EventSystem", typeof(EventSystem));
            root.transform.SetParent(transform, false);
            events = root.GetComponent<EventSystem>();
            ownsEventSystem = true;
        }
        uiModule = events.GetComponent<InputSystemUIInputModule>();
        if (uiModule == null) { uiModule = events.gameObject.AddComponent<InputSystemUIInputModule>(); addedModule = true; }
        previousModuleEnabled = uiModule.enabled;
        if (ownsEventSystem || addedModule) uiModule.enabled = false;
    }

    public void TogglePause()
    {
        if (resumePending) return;
        if (IsPaused) Resume();
        else Pause();
    }

    public void Pause()
    {
        if (IsPaused) return;
        previousTimeScale = Time.timeScale;
        previousAudioPause = AudioListener.pause;
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        GameNavigation.IsPaused = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        canvas.gameObject.SetActive(true);
        previousModuleEnabled = uiModule.enabled;
        uiModule.enabled = true;
        events.SetSelectedGameObject(null);
        events.SetSelectedGameObject(resumeButton.gameObject);
    }

    public void Resume()
    {
        if (!IsPaused) return;
        // Wait for Submit/Start/Escape release so UI confirmation cannot become boost or handbrake.
        resumePending = true;
    }

    private void LateUpdate()
    {
        if (!IsPaused)
        {
            // Enter Sandbox may have been submitted with the same A/Space used for driving.
            if (GameNavigation.WaitingForMenuRelease && !ResumeButtonsHeld())
            {
                GameNavigation.WaitingForMenuRelease = false;
                FindFirstObjectByType<TumblerController>()?.RefreshDrivingInput();
            }
            return;
        }
        if (resumePending)
        {
            if (ResumeButtonsHeld()) return;
            RestorePauseState();
            // Refresh cached controls before the next physics step (keys can change while paused).
            FindFirstObjectByType<TumblerController>()?.RefreshDrivingInput();
        }
        else if (events.currentSelectedGameObject == null)
            events.SetSelectedGameObject(resumeButton.gameObject);
    }

    private static bool ResumeButtonsHeld()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.enterKey.isPressed || keyboard.numpadEnterKey.isPressed ||
            keyboard.spaceKey.isPressed || keyboard.escapeKey.isPressed)) return true;
        if (Mouse.current != null && Mouse.current.leftButton.isPressed) return true;
        foreach (var pad in Gamepad.all)
            if (pad.buttonSouth.isPressed || pad.startButton.isPressed) return true;
        return false;
    }

    private void RestorePauseState()
    {
        resumePending = false;
        if (!IsPaused) return;
        GameNavigation.IsPaused = false;
        Time.timeScale = previousTimeScale;
        AudioListener.pause = previousAudioPause;
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
        if (canvas != null) canvas.gameObject.SetActive(false);
        if (events != null) events.SetSelectedGameObject(null);
        if (uiModule != null) uiModule.enabled = previousModuleEnabled;
    }

    public void ReturnToMainMenu() => GameNavigation.LoadScene(GameNavigation.MainMenuScene);
    public void ExitGame() => GameNavigation.ExitGame();
    public void PrepareToLeave() => RestorePauseState();
    private void OnDisable() { if (Instance == this) RestorePauseState(); }
    private void OnDestroy()
    {
        if (Instance != this) return;
        RestorePauseState();
        if (!ownsEventSystem && addedModule && uiModule != null) Destroy(uiModule);
        if (canvas != null) Destroy(canvas.gameObject);
        Instance = null;
    }
}
