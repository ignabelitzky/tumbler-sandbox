using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Small scene-local uGUI factory shared by the intro and pause overlay.</summary>
internal static class RuntimeMenuUI
{
    internal static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    internal static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    internal static Canvas Canvas(string name, Scene scene)
    {
        // Screen-space overlay canvases belong at the scene root, outside world-space transforms.
        var root = Rect(name, null, Vector2.zero, Vector2.zero);
        SceneManager.MoveGameObjectToScene(root.gameObject, scene);
        var canvas = root.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = root.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        root.gameObject.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    internal static Image Panel(string name, Transform parent, Color color, Vector2 size, Vector2 position)
    {
        var image = Rect(name, parent, size, position).gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    internal static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font,
        string value, int size, Color color, Vector2 bounds, Vector2 position)
    {
        var text = Rect(name, parent, bounds, position).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    internal static Button Button(string name, Transform parent, TMP_FontAsset font,
        float y, UnityAction action)
    {
        var image = Panel(name, parent, Color.white, new Vector2(500, 64), new Vector2(0, y));
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = new Color(1, 1, 1, .055f);
        colors.highlightedColor = new Color(1, 1, 1, .15f);
        colors.selectedColor = new Color(.84f, .64f, .34f, .42f);
        colors.pressedColor = new Color(.84f, .64f, .34f, .6f);
        colors.fadeDuration = .1f;
        button.colors = colors;
        Text("Label", image.transform, font, name, 26, new Color(.94f, .95f, .96f),
            new Vector2(460, 58), Vector2.zero);
        button.onClick.AddListener(action);
        return button;
    }
}
