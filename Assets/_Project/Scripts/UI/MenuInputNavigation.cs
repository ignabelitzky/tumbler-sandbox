using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Scene-local UI actions and selection recovery, independent of PlayerInput device pairing.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(EventSystem))]
[DefaultExecutionOrder(-200)]
public sealed class MenuInputNavigation : MonoBehaviour
{
    public event Action CancelRequested;
    private EventSystem events;
    private InputSystemUIInputModule module;
    private InputActionAsset actions;
    private InputAction cancel;
    private Selectable defaultSelection;
    private bool cancelPending;
    private readonly List<InputActionReference> references = new List<InputActionReference>();

    private void Awake()
    {
        events = GetComponent<EventSystem>();
        module = GetComponent<InputSystemUIInputModule>();
        if (module == null) module = gameObject.AddComponent<InputSystemUIInputModule>();
        var source = Resources.Load<InputActionAsset>("MenuInput");
        if (source == null)
        {
            Debug.LogError("Missing Resources/MenuInput.inputactions", this);
            enabled = false;
            return;
        }
        // UI must accept every connected device, including pads connected after scene load.
        // Use a private copy so gameplay PlayerInput cannot apply a device or scheme filter to it.
        actions = Instantiate(source);
        bool wasEnabled = module.enabled;
        module.enabled = false;
        module.UnassignActions();
        module.actionsAsset = actions;
        module.move = Reference("Navigate");
        module.submit = Reference("Submit");
        module.cancel = Reference("Cancel");
        module.point = Reference("Point");
        module.leftClick = Reference("Click");
        module.rightClick = Reference("RightClick");
        module.middleClick = Reference("MiddleClick");
        module.scrollWheel = Reference("ScrollWheel");
        module.deselectOnBackgroundClick = false;
        module.moveRepeatDelay = .4f;
        module.moveRepeatRate = .1f;
        events.sendNavigationEvents = true;
        cancel = actions.FindAction("UI/Cancel", true);
        module.enabled = wasEnabled;
    }

    private InputActionReference Reference(string name)
    {
        var reference = InputActionReference.Create(actions.FindAction("UI/" + name, true));
        references.Add(reference);
        return reference;
    }

    private void OnEnable()
    {
        if (actions == null) return;
        cancel.performed += OnCancel;
        actions.Enable();
        RecoverSelection();
    }

    private void OnDisable()
    {
        cancelPending = false;
        if (actions == null) return;
        cancel.performed -= OnCancel;
        actions.Disable();
    }

    private void OnCancel(InputAction.CallbackContext context) => cancelPending = true;

    public void SetDefaultSelection(Selectable target)
    {
        defaultSelection = target;
        events.firstSelectedGameObject = target != null ? target.gameObject : null;
        if (target != null && target.IsActive() && target.IsInteractable() && !events.alreadySelecting)
            events.SetSelectedGameObject(target.gameObject);
    }

    private void LateUpdate()
    {
        RecoverSelection();
        // Run Cancel after the input module, so a page change cannot process the same
        // input twice or submit a button on the newly opened page.
        if (!cancelPending) return;
        cancelPending = false;
        CancelRequested?.Invoke();
    }

    public void RecoverSelection()
    {
        if (events == null || events.alreadySelecting || !module.isActiveAndEnabled) return;
        var selected = events.currentSelectedGameObject;
        var selectable = selected != null ? selected.GetComponent<Selectable>() : null;
        if (selectable != null && selectable.IsActive() && selectable.IsInteractable()) return;
        if (defaultSelection != null && defaultSelection.IsActive() && defaultSelection.IsInteractable())
            events.SetSelectedGameObject(defaultSelection.gameObject);
    }

    private void OnApplicationFocus(bool focused) { if (focused) RecoverSelection(); }

    private void OnDestroy()
    {
        if (actions == null) return;
        if (module != null && module.actionsAsset == actions)
        {
            module.enabled = false;
            module.UnassignActions();
        }
        foreach (var reference in references) Destroy(reference);
        Destroy(actions);
    }
}
