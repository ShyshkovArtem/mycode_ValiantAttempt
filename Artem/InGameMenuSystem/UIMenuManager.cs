using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class UIMenuManager : MonoBehaviour
{
    public static UIMenuManager Instance { get; private set; }

    [Header("Panels (assign root GameObjects)")]
    [SerializeField] private GameObject generalPanel;
    [SerializeField] private GameObject equipmentPanel;
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private GameObject skillsPanel;

    [Header("Tabs (optional)")]
    [SerializeField] private UITabBar tabBar;

    [Header("Input (optional)")]
    [SerializeField] private InputActionReference toggleEquipmentAction; // I D-Pad Up
    [SerializeField] private InputActionReference toggleInventoryAction; // B D-Pad Down
    [SerializeField] private InputActionReference toggleGeneralAction;   // ESC/Start
    [SerializeField] private InputActionReference toggleSkillsAction;    // Y(keyboard)
    [SerializeField] private InputActionReference closeAllMenusAction;   // Y gamepad

    [Header("Cursor")]
    [SerializeField] private bool showCursorWhenOpen = true;

    private readonly Dictionary<MenuType, GameObject> _panels = new();
    private MenuType _current = MenuType.None;

    // Pause management
    private bool _pausedByMenus = false;
    private float _savedTimeScale = 1f;

    public static event Action<MenuType> InstanceOpened;
    public static event Action<MenuType> InstanceClosed;

    // Lock to prevent menu switching when a modal (like lore) is open
    private bool _inputLocked = false;
    public bool InputLocked => _inputLocked;
    public static bool HasInstance => Instance != null;

    private Action<InputAction.CallbackContext> _onEquipPerformed;
    private Action<InputAction.CallbackContext> _onInvPerformed;
    private Action<InputAction.CallbackContext> _onGenPerformed;
    private Action<InputAction.CallbackContext> _onSkilPerformed;
    private Action<InputAction.CallbackContext> _onCloseAllPerformed;   

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (equipmentPanel) _panels[MenuType.Equipment] = equipmentPanel;
        if (inventoryPanel) _panels[MenuType.Inventory] = inventoryPanel;
        if (generalPanel) _panels[MenuType.General] = generalPanel;
        if (skillsPanel) _panels[MenuType.Skills] = skillsPanel;

        foreach (var kv in _panels) kv.Value.SetActive(false);
        ApplyPauseState(false); // ensure unpaused at start

        if (showCursorWhenOpen)     //Hide the cursor at game start
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }

    void OnEnable()
    {
        _onEquipPerformed = _ => HandleToggle(MenuType.Equipment);
        _onInvPerformed = _ => HandleToggle(MenuType.Inventory);
        _onGenPerformed = _ => HandleToggle(MenuType.General);
        _onSkilPerformed = _ => HandleToggle(MenuType.Skills);
        _onCloseAllPerformed = _ => HandleCloseAll();             

        if (toggleEquipmentAction?.action != null)
        {
            toggleEquipmentAction.action.performed += _onEquipPerformed;
            toggleEquipmentAction.action.Enable();
        }
        if (toggleInventoryAction?.action != null)
        {
            toggleInventoryAction.action.performed += _onInvPerformed;
            toggleInventoryAction.action.Enable();
        }
        if (toggleGeneralAction?.action != null)
        {
            toggleGeneralAction.action.performed += _onGenPerformed;
            toggleGeneralAction.action.Enable();
        }
        if (toggleSkillsAction?.action != null)
        {
            toggleSkillsAction.action.performed += _onSkilPerformed;
            toggleSkillsAction.action.Enable();
        }

        // bind close-all action (Y gamepad)
        if (closeAllMenusAction?.action != null)
        {
            closeAllMenusAction.action.performed += _onCloseAllPerformed;
            closeAllMenusAction.action.Enable();
        }

        toggleEquipmentAction.action.performed += ctx =>
            Debug.Log($"[Menu] Equip performed by {ctx.control?.device?.displayName} / {ctx.control?.path}");

        toggleInventoryAction.action.performed += ctx =>
            Debug.Log($"[Menu] Inventory performed by {ctx.control?.device?.displayName} / {ctx.control?.path}");

        toggleGeneralAction.action.performed += ctx =>
            Debug.Log($"[Menu] General performed by {ctx.control?.device?.displayName} / {ctx.control?.path}");
    }

    void OnDisable()
    {
        if (toggleEquipmentAction?.action != null)
        {
            toggleEquipmentAction.action.performed -= _onEquipPerformed;
            toggleEquipmentAction.action.Disable();
        }
        if (toggleInventoryAction?.action != null)
        {
            toggleInventoryAction.action.performed -= _onInvPerformed;
            toggleInventoryAction.action.Disable();
        }
        if (toggleGeneralAction?.action != null)
        {
            toggleGeneralAction.action.performed -= _onGenPerformed;
            toggleGeneralAction.action.Disable();
        }
        if (toggleSkillsAction?.action != null)
        {
            toggleSkillsAction.action.performed -= _onSkilPerformed;
            toggleSkillsAction.action.Disable();
        }

        // unbind close-all action
        if (closeAllMenusAction?.action != null)
        {
            closeAllMenusAction.action.performed -= _onCloseAllPerformed;
            closeAllMenusAction.action.Disable();
        }
    }

    // ---- Public API ----

    public void Toggle(MenuType type)
    {
        if (_inputLocked) return;          // <-- block tab clicks that call Toggle
        if (_current == type) CloseAll();
        else Open(type);
    }

    public void Open(MenuType type)
    {
        if (_inputLocked) return;          // <-- block tab buttons that call Open

        if (_current == type) return;

        bool wasOpen = _current != MenuType.None; // already in a menu?

        // close all
        foreach (var kv in _panels) kv.Value.SetActive(false);

        if (_panels.TryGetValue(type, out var go))
        {
            go.SetActive(true);
            _current = type;

            // Only pause when opening the FIRST menu
            if (!wasOpen) ApplyPauseState(true);

            InstanceOpened?.Invoke(type);
            tabBar?.SetActiveTab(type);
        }
        else
        {
            _current = MenuType.None;
            ApplyPauseState(false);
            tabBar?.SetActiveTab(MenuType.None);
        }
    }

    public void CloseAll()
    {
        foreach (var kv in _panels) kv.Value.SetActive(false);

        var closedType = _current;
        _current = MenuType.None;

        ApplyPauseState(false);
        InstanceClosed?.Invoke(closedType);
        tabBar?.SetActiveTab(MenuType.None);
    }

    public MenuType Current => _current;

    // ---- Helpers ----

    public static void SetInputLocked(bool locked)
    {
        if (!HasInstance) return;
        Instance._inputLocked = locked;
    }

    private void HandleToggle(MenuType type)
    {
        // Block LB/RB / D-pad toggles when input is locked (e.g., lore open)
        if (_inputLocked) return;
        Toggle(type);
    }

    // Y gamepad closes all menus when any is open
    private void HandleCloseAll()
    {
        if (_inputLocked) return;                 // respect modal lock
        if (_current == MenuType.None) return;    // nothing open, do nothing

        CloseAll();
    }

    private void ApplyPauseState(bool open)
    {
        if (open)
        {
            if (!_pausedByMenus)
            {
                _savedTimeScale = Time.timeScale <= 0f ? 1f : Time.timeScale;
                Time.timeScale = 0f;
                _pausedByMenus = true;

                if (showCursorWhenOpen)
                {
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.None;
                }
            }

            ShowTabBar(true);
        }
        else
        {
            if (_pausedByMenus)
            {
                Time.timeScale = _savedTimeScale <= 0f ? 1f : _savedTimeScale;
                _pausedByMenus = false;

                if (showCursorWhenOpen)
                {
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;
                }
            }

            ShowTabBar(false);
        }
    }

    private void ShowTabBar(bool visible)
    {
        if (tabBar && tabBar.gameObject.activeSelf != visible)
            tabBar.gameObject.SetActive(visible);
    }

    /// <summary>
    /// Closes all current menus and loads a new scene by name.
    /// Keeps the cursor visible (for main menus and UI scenes).
    /// </summary>
    public void LoadSceneByName(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("[UIMenuManager] LoadSceneByName called with an empty scene name!");
            return;
        }

        // Close all open UI panels before leaving the scene
        CloseAll();

        // Ensure game is unpaused before switching scenes
        if (Time.timeScale == 0f)
            Time.timeScale = 1f;

        // Make sure the cursor is visible and free (so main menu is usable)
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        Debug.Log($"[UIMenuManager] Loading scene: {sceneName}");
        SceneManager.LoadScene(sceneName);
    }
}
