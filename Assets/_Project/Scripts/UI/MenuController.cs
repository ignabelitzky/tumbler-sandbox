using UnityEngine;

public class MenuController : MonoBehaviour
{
    private void Awake() => GameNavigation.RestoreSceneState();

    public void ExitGame() => GameNavigation.ExitGame();

    public void LoadScene(string sceneName)
    {
        GameNavigation.LoadScene(sceneName);
    }
}
