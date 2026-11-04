using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MenuController : MonoBehaviour
{
    [SerializeField] private Button enterSandboxButton;
    [SerializeField] private Texture2D developerLogo;
    [SerializeField] private TMP_FontAsset creditFont;
    private RectTransform credit;

    private void Awake()
    {
        GameNavigation.RestoreSceneState();
        var events = FindFirstObjectByType<EventSystem>();
        if (events != null)
        {
            var navigation = events.GetComponent<MenuInputNavigation>();
            if (navigation == null) navigation = events.gameObject.AddComponent<MenuInputNavigation>();
            navigation.SetDefaultSelection(enterSandboxButton);
        }
        BuildDeveloperCredit();
    }

    private void BuildDeveloperCredit()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null || developerLogo == null || creditFont == null) return;
        credit = RuntimeMenuUI.Rect("Phosphor Developer Credit", canvas.transform,
            new Vector2(376, 64), new Vector2(-32, 24));
        credit.anchorMin = credit.anchorMax = new Vector2(1, 0);
        credit.pivot = new Vector2(1, 0);
        var icon = RuntimeMenuUI.Rect("Phosphor Icon", credit, new Vector2(64, 64),
            new Vector2(-156, 0)).gameObject.AddComponent<RawImage>();
        icon.texture = developerLogo;
        icon.raycastTarget = false;
        var label = RuntimeMenuUI.Text("Developer Credit", credit, creditFont,
            "Develop by Ignacio Belitzky", 18, new Color(.82f, .87f, .85f),
            new Vector2(280, 64), new Vector2(36, 0));
        label.alignment = TextAlignmentOptions.Right;
        label.enableAutoSizing = true;
        label.fontSizeMin = 14;
        label.fontSizeMax = 18;
    }

    private void OnDestroy() { if (credit != null) Destroy(credit.gameObject); }

    public void ExitGame() => GameNavigation.ExitGame();

    public void LoadScene(string sceneName)
    {
        GameNavigation.LoadScene(sceneName);
    }
}
