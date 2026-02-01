using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class SkillPanel : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private List<SkillData> skills;

    [Header("UI - Slots")]
    [SerializeField] private Transform slotsParent;
    [SerializeField] private SkillSlot slotPrefab;

    [Header("UI - Info")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text cooldownText;
    [SerializeField] private TMP_Text powerText;
    [SerializeField] private TMP_Text rangeText;
    [SerializeField] private Image bigIcon;

    [Header("Input (UI/Navigate)")]
    [SerializeField] private InputActionReference navigateAction;   // <-- assign UI/Navigate here
    [SerializeField] private float initialRepeatDelay = 0.35f;      
    [SerializeField] private float repeatRate = 0.12f;

    private SkillSlot selectedSlot;

    // Runtime list of slots for navigation
    private readonly List<SkillSlot> _slots = new();

    // Navigation state
    private Vector2 _navInput;
    private float _nextNavTime;
    private bool _navHeld;
    private int _selectedIndex = 0;

    private void OnEnable()
    {
        if (navigateAction != null)
        {
            navigateAction.action.performed += OnNavigatePerformed;
            navigateAction.action.canceled += OnNavigateCanceled;
        }
    }

    private void OnDisable()
    {
        if (navigateAction != null)
        {
            navigateAction.action.performed -= OnNavigatePerformed;
            navigateAction.action.canceled -= OnNavigateCanceled;
        }
    }

    private void Update()
    {
        // Only navigate when the Skills menu is the active one
        if (!UIMenuManager.HasInstance || UIMenuManager.Instance.Current != MenuType.Skills)
            return;

        if (!_navHeld) return;
        if (_navInput.sqrMagnitude < 0.25f) return;
        if (_slots.Count == 0) return;

        if (Time.unscaledTime >= _nextNavTime)
        {
            PerformNavigationStep(_navInput);
            _nextNavTime = Time.unscaledTime + repeatRate;
        }
    }


    private void Start()
    {
        Build();

        if (slotsParent.childCount > 0)
        {
            var first = slotsParent.GetChild(0).GetComponent<SkillSlot>();
            _selectedIndex = 0;
            OnSlotClicked(first);
            FocusSlot(first);
        }
    }

    private void Build()
    {
        _slots.Clear();

        foreach (Transform c in slotsParent)
            Destroy(c.gameObject);

        foreach (var data in skills)
        {
            var slot = Instantiate(slotPrefab, slotsParent);
            slot.Init(data, this);
            _slots.Add(slot);
        }
    }

    public void OnSlotClicked(SkillSlot slot)
    {
        if (selectedSlot == slot) return;

        if (selectedSlot != null)
            selectedSlot.SetSelected(false);

        selectedSlot = slot;
        selectedSlot.SetSelected(true);

        // keep index in sync for navigation
        _selectedIndex = _slots.IndexOf(selectedSlot);

        var d = slot.Data;
        titleText.text = d.displayName;
        descriptionText.text = d.description;
        cooldownText.text = $"Cooldown: {d.cooldown:F1}s";
        powerText.text = $"Power: {d.power:F0}";
        rangeText.text = $"Range: {d.range:F1}m";

        if (bigIcon)
        {
            bigIcon.enabled = true;
            bigIcon.sprite = d.icon;
        }
    }

    // ---------- Navigation ----------

    private void OnNavigatePerformed(InputAction.CallbackContext ctx)
    {
        // Only navigate when the Skills menu is the active one
        if (!UIMenuManager.HasInstance || UIMenuManager.Instance.Current != MenuType.Skills)
            return;

        _navInput = ctx.ReadValue<Vector2>();
        if (_navInput.sqrMagnitude < 0.25f)
            return;

        if (_slots.Count == 0) return;

        if (!_navHeld)
        {
            // first press: one step immediately
            PerformNavigationStep(_navInput);
            _navHeld = true;
            _nextNavTime = Time.unscaledTime + initialRepeatDelay;
        }
        else
        {
            // already holding; repeats handled in Update()
        }
    }


    private void OnNavigateCanceled(InputAction.CallbackContext ctx)
    {
        _navInput = Vector2.zero;
        _navHeld = false;
    }


    private void PerformNavigationStep(Vector2 input)
    {
        if (_slots.Count == 0) return;

        // vertical list: up/down primarily
        int delta;
        if (Mathf.Abs(input.y) >= Mathf.Abs(input.x))
            delta = input.y > 0 ? -1 : +1; // up = previous, down = next
        else
            delta = input.x > 0 ? +1 : -1; // allow left/right if you want

        int newIndex = _selectedIndex + delta;

        if (newIndex < 0) newIndex = _slots.Count - 1;
        if (newIndex >= _slots.Count) newIndex = 0;

        _selectedIndex = newIndex;

        var slot = _slots[_selectedIndex];
        OnSlotClicked(slot);   // reuse existing selection logic
        FocusSlot(slot);       // move Unity’s selected object for button visuals
    }

    private void FocusSlot(SkillSlot slot)
    {
        if (!slot) return;

        var btn = slot.GetComponent<Button>() ?? slot.GetComponentInChildren<Button>();
        if (btn && EventSystem.current)
            EventSystem.current.SetSelectedGameObject(btn.gameObject);
    }
}
