using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RPG.Equipment
{
    public class UIEquipmentList : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Transform contentRoot;
        [SerializeField] private UIEquipmentListItem itemPrefab;
        [SerializeField] private Button equipButton;
        [SerializeField] private Button dropButton;
        [SerializeField] private Button unequipButton;  // optional, X

        [Header("Input")]
        [SerializeField] private InputActionReference navigateAction;  // UI/Navigate (stick/WASD)
        [SerializeField] private InputActionReference equipAction;     // UI/Submit (A)
        [SerializeField] private InputActionReference dropAction;      // UI/Cancel (B)
        [SerializeField] private InputActionReference unequipAction;   // UI/Unequip (Y)

        [Header("Repeat")]
        [SerializeField] private float initialRepeatDelay = 0.35f;
        [SerializeField] private float repeatRate = 0.12f;

        private readonly List<UIEquipmentListItem> _rows = new();
        private UIEquipmentListItem _selectedRow;
        private int _selectedIndex = -1;
        private EquipmentItem _selectedItem;

        // when selection is invalidated by equip/drop, we remember where to go next
        private int _pendingSelectIndex = -1;

        // nav state
        private Vector2 _navInput;
        private bool _navHeld;
        private float _nextNavTime;

        private List<EquipmentItem> Items => EquipInv.Instance != null ? EquipInv.Instance.Items : null;

        // --------------------------------------------------------------------
        // Lifecycle
        // --------------------------------------------------------------------

        void OnEnable()
        {
            if (equipButton) equipButton.onClick.AddListener(OnEquip);
            if (dropButton) dropButton.onClick.AddListener(OnDrop);

            UIEvents.InventoryChanged += Rebuild;

            if (navigateAction != null)
            {
                navigateAction.action.performed += OnNavigatePerformed;
                navigateAction.action.canceled += OnNavigateCanceled;
            }

            if (equipAction != null)
                equipAction.action.performed += OnEquipAction;

            if (dropAction != null)
                dropAction.action.performed += OnDropAction;

            if (unequipAction != null)
                unequipAction.action.performed += OnUnequipAction;
                unequipAction.action.Enable(); // Added by Sami: Enabling the action, so we can use it with steam-input.

            UIMenuManager.InstanceOpened += OnMenuOpened;

            StartCoroutine(DeferredInitialBuild());
        }

        void OnDisable()
        {
            UIEvents.InventoryChanged -= Rebuild;

            if (equipButton) equipButton.onClick.RemoveListener(OnEquip);
            if (dropButton) dropButton.onClick.RemoveListener(OnDrop);

            if (navigateAction != null)
            {
                navigateAction.action.performed -= OnNavigatePerformed;
                navigateAction.action.canceled -= OnNavigateCanceled;
            }

            if (equipAction != null)
                equipAction.action.performed -= OnEquipAction;
            if (dropAction != null)
                dropAction.action.performed -= OnDropAction;
            if (unequipAction != null)
                unequipAction.action.performed -= OnUnequipAction;
                unequipAction.action.Disable(); // Added by Sami

            UIMenuManager.InstanceOpened -= OnMenuOpened;
        }

        private IEnumerator DeferredInitialBuild()
        {
            yield return null;
            float t = 1f;
            while ((EquipInv.Instance == null || EquipInv.Instance.Items == null) && t > 0f)
            {
                yield return null;
                t -= Time.unscaledDeltaTime;
            }
            Rebuild();
        }

        private void Update()
        {
            // Only navigate when Equipment tab is active and not locked
            if (!UIMenuManager.HasInstance ||
                UIMenuManager.Instance.Current != MenuType.Equipment ||
                UIMenuManager.Instance.InputLocked)
                return;

            if (!_navHeld) return;
            if (_navInput.sqrMagnitude < 0.25f) return;
            if (_rows.Count == 0) return;

            if (Time.unscaledTime >= _nextNavTime)
            {
                StepNavigation(_navInput);
                _nextNavTime = Time.unscaledTime + repeatRate;
            }
        }

        private void OnMenuOpened(MenuType type)
        {
            if (type != MenuType.Equipment) return;

            // When equipment panel is opened, ensure we have a valid selection
            if (_rows.Count == 0)
                Rebuild();
            else if (_selectedRow == null && _rows.Count > 0)
                SelectRow(_rows[0]);
            else if (_selectedRow != null)
                FocusRow(_selectedRow);
        }

        // --------------------------------------------------------------------
        // Build / Rebuild
        // --------------------------------------------------------------------

        public void Rebuild()
        {
            if (!contentRoot || !itemPrefab) return;
            var items = Items;
            if (items == null) return;

            foreach (Transform c in contentRoot) Destroy(c.gameObject);
            _rows.Clear();
            _selectedRow = null;
            _selectedItem = null;

            // create rows
            for (int i = 0; i < items.Count; i++)
            {
                var row = Instantiate(itemPrefab, contentRoot);
                int idx = i; // capture
                row.Bind(items[i], idx, () => SelectRow(row));
                _rows.Add(row);
            }

            // choose selection index
            int targetIndex = 0;
            if (_pendingSelectIndex >= 0)
                targetIndex = Mathf.Clamp(_pendingSelectIndex, 0, Mathf.Max(0, _rows.Count - 1));

            if (_rows.Count > 0)
            {
                SelectRow(_rows[targetIndex]);
            }
            else
            {
                _selectedIndex = -1;
                _selectedItem = null;
                UpdateButtons();
            }

            _pendingSelectIndex = -1;
        }

        private void SelectRow(UIEquipmentListItem row)
        {
            if (row == null) return;

            _selectedRow = row;
            _selectedIndex = row.Index;
            _selectedItem = row.BoundItem;

            foreach (var r in _rows)
                r.SetSelected(r == _selectedRow);

            // make sure EventSystem focus matches for highlight / A button etc
            FocusRow(row);

            UIEvents.RaiseItemSelected(_selectedItem);
            UpdateButtons();
        }

        private void FocusRow(UIEquipmentListItem row)
        {
            if (row == null) return;

            var btn = row.GetComponent<Button>();
            if (btn && EventSystem.current)
                EventSystem.current.SetSelectedGameObject(btn.gameObject);
        }

        private void UpdateButtons()
        {
            bool hasSelection = _selectedRow != null && _selectedItem != null && _selectedIndex >= 0;

            if (equipButton)
                equipButton.interactable = hasSelection &&
                                           EquipmentManager.Instance != null &&
                                           EquipmentManager.Instance.CanEquip(_selectedItem);

            if (dropButton)
                dropButton.interactable = hasSelection;

            if (unequipButton)
                unequipButton.interactable = true; // your own logic if needed
        }

        // --------------------------------------------------------------------
        // Equip / Drop logic
        // --------------------------------------------------------------------

        private void OnEquip()
        {
            if (_selectedRow == null || _selectedItem == null || _selectedIndex < 0) return;

            int currentIndex = _selectedIndex;
            EquipmentManager.Instance.Equip(_selectedItem, _selectedIndex);

            // After equip, item is removed, so next item will now be at same index
            _pendingSelectIndex = currentIndex;

            // Rebuild will be called by UIEvents.InventoryChanged ? will auto-select next
        }

        private void OnDrop()
        {
            if (_selectedRow == null || _selectedIndex < 0) return;

            int currentIndex = _selectedIndex;
            EquipInv.Instance.RemoveAt(_selectedIndex);

            _pendingSelectIndex = currentIndex;
            // Rebuild called by UIEvents.InventoryChanged
        }

        // --------------------------------------------------------------------
        // Input bindings for A/B/X
        // --------------------------------------------------------------------

        private void OnEquipAction(InputAction.CallbackContext ctx)
        {
            if (!CanUseInventoryActions()) return;
            OnEquip();
        }

        private void OnDropAction(InputAction.CallbackContext ctx)
        {
            if (!CanUseInventoryActions()) return;
            OnDrop();
        }

        private void OnUnequipAction(InputAction.CallbackContext ctx)
        {
            if (!IsEquipmentMenuActive()) return;
            if (unequipButton && unequipButton.interactable)
                unequipButton.onClick.Invoke();
        }

        private bool IsEquipmentMenuActive()
        {
            return UIMenuManager.HasInstance &&
                   UIMenuManager.Instance.Current == MenuType.Equipment &&
                   !UIMenuManager.Instance.InputLocked;
        }

        private bool CanUseInventoryActions()
        {
            return IsEquipmentMenuActive() &&
                   _selectedRow != null &&
                   _selectedItem != null &&
                   _selectedIndex >= 0;
        }

        // --------------------------------------------------------------------
        // Navigation (stick / WASD)
        // --------------------------------------------------------------------

        private void OnNavigatePerformed(InputAction.CallbackContext ctx)
        {
            if (!UIMenuManager.HasInstance ||
                UIMenuManager.Instance.Current != MenuType.Equipment ||
                UIMenuManager.Instance.InputLocked)
                return;

            _navInput = ctx.ReadValue<Vector2>();
            if (_navInput.sqrMagnitude < 0.25f)
                return;

            if (_rows.Count == 0) return;

            if (!_navHeld)
            {
                StepNavigation(_navInput);
                _navHeld = true;
                _nextNavTime = Time.unscaledTime + initialRepeatDelay;
            }
        }

        private void OnNavigateCanceled(InputAction.CallbackContext ctx)
        {
            _navHeld = false;
            _navInput = Vector2.zero;
        }

        private void StepNavigation(Vector2 input)
        {
            if (_rows.Count == 0) return;

            int delta;
            if (Mathf.Abs(input.y) >= Mathf.Abs(input.x))
                delta = input.y > 0 ? -1 : +1;  // up/down
            else
                delta = input.x > 0 ? +1 : -1;  // left/right

            int current = 0;
            if (_selectedRow != null)
                current = Mathf.Clamp(_rows.IndexOf(_selectedRow), 0, _rows.Count - 1);

            int newIndex = current + delta;
            if (newIndex < 0) newIndex = _rows.Count - 1;
            if (newIndex >= _rows.Count) newIndex = 0;

            SelectRow(_rows[newIndex]);
        }
    }
}
