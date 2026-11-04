using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Make the controls page's Back hint work with keyboard and gamepad.</summary>
public sealed class ControlsOverlayNavigation : MonoBehaviour
{
    [SerializeField] private Button backButton;
    [SerializeField] private Button controlsButton;
    [SerializeField] private RectTransform layout;
    private MenuInputNavigation navigation;
    private void OnEnable()
    {
        if (layout == null) layout = transform.Find("Controls layout") as RectTransform;
        Canvas.willRenderCanvases += Fit;
        Fit();
        var events = FindFirstObjectByType<EventSystem>();
        navigation = events != null ? events.GetComponent<MenuInputNavigation>() : null;
        if (navigation != null)
        {
            navigation.SetDefaultSelection(backButton);
            navigation.CancelRequested += Back;
        }
    }
    private void OnDisable()
    {
        Canvas.willRenderCanvases -= Fit;
        if (navigation == null) return;
        navigation.CancelRequested -= Back;
        navigation.SetDefaultSelection(controlsButton);
    }
    private void OnRectTransformDimensionsChange() { if (isActiveAndEnabled) Fit(); }
    private void Fit()
    {
        if (layout == null) return;
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        Vector2 available = ((RectTransform)canvas.transform).rect.size - Vector2.one * 64;
        Vector2 size = layout.rect.size;
        if (available.x <= 0 || available.y <= 0 || size.x <= 0 || size.y <= 0) return;
        float scale = Mathf.Min(1, available.x / size.x, available.y / size.y);
        if (Mathf.Abs(layout.localScale.x - scale) > .0001f) layout.localScale = Vector3.one * scale;
    }
    private void Back()
    {
        if (backButton == null) return;
        backButton.onClick.Invoke();
        if (controlsButton != null && controlsButton.gameObject.activeInHierarchy) controlsButton.Select();
    }
}
