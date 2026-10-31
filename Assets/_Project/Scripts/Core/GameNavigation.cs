using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Explicit application exit and scene transitions; Escape is handled by the pause menu.</summary>
public static class GameNavigation
{
    public const string IntroScene = "Intro";
    public const string MainMenuScene = "Main Menu";
    public const string SandboxScene = "Sandbox";
    public static bool IsPaused { get; internal set; }
    internal static bool WaitingForMenuRelease { get; set; }
    public static bool GameplayInputBlocked => IsPaused || WaitingForMenuRelease;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { IsPaused = false; WaitingForMenuRelease = false; }

    public static void RestoreSceneState()
    {
        IsPaused = false;
        WaitingForMenuRelease = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public static void LoadScene(string sceneName)
    {
        PauseMenuController.Instance?.PrepareToLeave();
        RestoreSceneState();
        SceneManager.LoadScene(sceneName);
    }

    public static void ExitGame()
    {
        PauseMenuController.Instance?.PrepareToLeave();
        RestoreSceneState();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
