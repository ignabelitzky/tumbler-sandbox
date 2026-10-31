using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Original Phosphor artwork on its approved background; only the Intro scene runs this.</summary>
public sealed class IntroController : MonoBehaviour
{
    [SerializeField] private Texture2D logo;
    [SerializeField] private Font signatureFont;
    private CanvasGroup content;
    private Canvas canvas;
    private TMP_FontAsset runtimeFont;
    private static readonly Color Background = new Color32(7, 28, 19, 255);

    private void Awake()
    {
        GameNavigation.RestoreSceneState();
        canvas = RuntimeMenuUI.Canvas("Developer Intro", gameObject.scene);
        var background = RuntimeMenuUI.Panel("Phosphor Background", canvas.transform,
            Background, Vector2.zero, Vector2.zero);
        RuntimeMenuUI.Stretch(background.rectTransform);
        var group = RuntimeMenuUI.Rect("Brand", background.transform, Vector2.zero, Vector2.zero);
        RuntimeMenuUI.Stretch(group);
        content = group.gameObject.AddComponent<CanvasGroup>();
        content.alpha = 0f;
        runtimeFont = TMP_FontAsset.CreateFontAsset(signatureFont);
        var image = RuntimeMenuUI.Rect("Phosphor Original", group, new Vector2(420, 420),
            new Vector2(0, 44)).gameObject.AddComponent<RawImage>();
        image.texture = logo;
        image.color = Color.white;
        image.raycastTarget = false;
        RuntimeMenuUI.Text("Developer Signature", group, runtimeFont, "Developed by Ignacio Belitzky",
            34, new Color32(245, 250, 247, 255), new Vector2(900, 70), new Vector2(0, -220));
        RuntimeMenuUI.Text("Skip Hint", group, runtimeFont, "Press any key, click or controller button to skip",
            16, new Color32(168, 192, 177, 255), new Vector2(900, 40), new Vector2(0, -294));
        var cameraObject = new GameObject("Intro Camera", typeof(Camera));
        cameraObject.transform.SetParent(transform, false);
        var camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Background;
        camera.cullingMask = 0;
    }

    private IEnumerator Start()
    {
        // Preload while the brand is visible; retain it if loading takes longer than the presentation.
        var menu = SceneManager.LoadSceneAsync(GameNavigation.MainMenuScene);
        if (menu == null) { Debug.LogError("Main Menu must be enabled in the scene list.", this); yield break; }
        menu.allowSceneActivation = false;
        float elapsed = 0f;
        bool skip = false;
        while (elapsed < 3f || menu.progress < .9f)
        {
            elapsed += Time.unscaledDeltaTime;
            content.alpha = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / .6f));
            if (elapsed >= .45f && SkipPressed()) skip = true;
            if (skip && menu.progress >= .9f) break;
            yield return null;
        }
        float alpha = content.alpha;
        for (float fade = 0; fade < .6f; fade += Time.unscaledDeltaTime)
        {
            content.alpha = alpha * (1f - Mathf.SmoothStep(0, 1, fade / .6f));
            yield return null;
        }
        menu.allowSceneActivation = true;
    }

    private static bool SkipPressed()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
        var mouse = Mouse.current;
        if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame ||
            mouse.middleButton.wasPressedThisFrame)) return true;
        foreach (var pad in Gamepad.all)
            foreach (var control in pad.allControls)
                if (control is ButtonControl button && button.wasPressedThisFrame) return true;
        return false;
    }

    private void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject);
        if (runtimeFont == null) return;
        foreach (var texture in runtimeFont.atlasTextures) if (texture != null) Destroy(texture);
        if (runtimeFont.material != null) Destroy(runtimeFont.material);
        Destroy(runtimeFont);
    }
}
