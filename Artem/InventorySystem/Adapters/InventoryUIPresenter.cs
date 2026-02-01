using RPG.Events;
using RPG.Items;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RPG.Adapters
{
    public class InventoryUIPresenter : MonoBehaviour
    {
        [Header("Grid")]
        [SerializeField] private Transform gridParent;
        [SerializeField] private GameObject slotPrefab; // slot root should have a Button; children: "Icon" (Image), optional "Count" (TMP_Text)
        [SerializeField] private Sprite fallbackIcon;

        [Header("Details Panel")]
        [SerializeField] private GameObject detailsRoot;     // whole right-side panel parent
        [SerializeField] private Image detailIcon;
        [SerializeField] private TMP_Text detailName;
        [SerializeField] private TMP_Text detailDesc;
        [SerializeField] private Button detailUseButton;
        [SerializeField] private Button detailDropButton;

        private GameObject _owner;

        [SerializeField] private InputActionReference navigateAction; // UI/Navigate
        [SerializeField] private float initialRepeatDelay = 0.35f;
        [SerializeField] private float repeatRate = 0.12f;

        private Vector2 _navInput;
        private float _nextNavTime;
        private bool _navHeld;

        // cache latest inventory and selection
        private IReadOnlyList<ItemStack> _lastSnapshot;
        private Guid _selectedId = Guid.Empty;

        private void Awake()
        {
            // listen for inventory changes for the entire lifetime
            InventoryEvents.Subscribe<InventoryChanged>(OnInvChanged);

            // wire detail buttons once
            if (detailUseButton) detailUseButton.onClick.AddListener(OnClickUse);
            if (detailDropButton) detailDropButton.onClick.AddListener(OnClickDrop);
        }

        private void Update()
        {
            // Only navigate when INVENTORY menu is open
            if (!UIMenuManager.HasInstance || UIMenuManager.Instance.Current != MenuType.Inventory)
                return;

            if (!_navHeld) return;
            if (_navInput.sqrMagnitude < 0.25f) return;

            if (Time.unscaledTime >= _nextNavTime)
            {
                PerformNavigationStep(_navInput);
                _nextNavTime = Time.unscaledTime + repeatRate;
            }
        }



        private void OnDestroy()
        {
            InventoryEvents.Unsubscribe<InventoryChanged>(OnInvChanged);
            if (detailUseButton) detailUseButton.onClick.RemoveListener(OnClickUse);
            if (detailDropButton) detailDropButton.onClick.RemoveListener(OnClickDrop);
        }

        private void OnEnable()
        {
            // if inventory panel was toggled on, render from cache
            if (_lastSnapshot != null) BuildUI(_lastSnapshot);
            RPG.Events.InventoryEvents.Publish(new InventorySyncRequested(_owner));
            UpdateDetails();

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

        public void SetOwner(GameObject owner)
        {
            _owner = owner;
            RPG.Events.InventoryEvents.Publish(new InventorySyncRequested(_owner));
            if (isActiveAndEnabled && _lastSnapshot != null) BuildUI(_lastSnapshot);
            UpdateDetails();
        }

        // Call this from your toggle controller when opening the UI so details are shown immediately
        public void OpenWithDefaultSelection()
        {
            EnsureValidSelection();
            if (isActiveAndEnabled && _lastSnapshot != null) BuildUI(_lastSnapshot);
            UpdateDetails();
        }

        // -------- event handling --------
        private void OnInvChanged(InventoryChanged evt)
        {
            if (_owner != null && evt.Owner != _owner) return;

            _lastSnapshot = evt.Snapshot; // cache latest
            if (isActiveAndEnabled) BuildUI(_lastSnapshot);

            EnsureValidSelection();
            UpdateDetails();
        }

        // -------- build grid --------
        private void BuildUI(IReadOnlyList<ItemStack> snapshot)
        {
            if (!gridParent || !slotPrefab) return;

            bool keepTemplate = slotPrefab.scene.IsValid() && slotPrefab.transform.IsChildOf(gridParent);
            foreach (Transform child in gridParent)
            {
                if (keepTemplate && child.gameObject == slotPrefab) continue;
                Destroy(child.gameObject);
            }

            for (int i = 0; i < snapshot.Count; i++)
            {
                var s = snapshot[i];
                var go = Instantiate(slotPrefab, gridParent);
                go.name = $"Slot_{i}_{(s.Def ? s.Def.DisplayName : s.Instance.DefinitionId)}";

                // icon
                var icon = go.transform.Find("Icon")?.GetComponent<Image>();
                if (icon) icon.sprite = s.Def && s.Def.Icon ? s.Def.Icon : fallbackIcon;

                // optional count text
                var count = go.transform.Find("Count")?.GetComponent<TMP_Text>();
                if (count)
                {
                    bool stacked = s.Def && s.Def.MaxStack > 1;
                    count.gameObject.SetActive(stacked);
                    if (stacked) count.text = s.Instance.Quantity.ToString();
                }

                // selected frame
                var frameTf = go.transform.Find("SelectedFrame");
                if (frameTf) frameTf.gameObject.SetActive(s.Instance.InstanceId == _selectedId);

                // slot button
                var btn = go.GetComponent<Button>() ?? go.gameObject.AddComponent<Button>();
                var capturedId = s.Instance.InstanceId;

                // store instance id on the slot for navigation
                var meta = go.GetComponent<RPG.Adapters.InventorySlotMeta>()
                           ?? go.gameObject.AddComponent<RPG.Adapters.InventorySlotMeta>();
                meta.InstanceId = capturedId.ToString();

                // mouse click selection
                btn.onClick.AddListener(() =>
                {
                    Select(capturedId);
                    RefreshSelectionFrames();
                    if (EventSystem.current)
                        EventSystem.current.SetSelectedGameObject(go);
                });

                // optional: button color tweak
                var colors = btn.colors;
                colors.normalColor = capturedId == _selectedId ? new Color(1f, 1f, 1f, 0.95f)
                                                               : new Color(1f, 1f, 1f, 0.75f);
                btn.colors = colors;
            }
        }



        // -------- details panel --------
        private void UpdateDetails()
        {
            bool hasSelection = TryGetSelected(out var stack);
            if (detailsRoot) detailsRoot.SetActive(hasSelection);

            if (!hasSelection)
            {
                if (detailIcon) detailIcon.sprite = fallbackIcon;
                if (detailName) detailName.text = "";
                if (detailDesc) detailDesc.text = "";
                SetDetailButtons(false, false);
                return;
            }

            var def = stack.Def;
            if (detailIcon) detailIcon.sprite = def && def.Icon ? def.Icon : fallbackIcon;
            if (detailName) detailName.text = def ? def.DisplayName : stack.Instance.DefinitionId;

            // Pull the real description from the ItemDefinition helper
            string desc = def ? def.GetDescriptionForUI() : "";
            if (detailDesc) detailDesc.text = string.IsNullOrWhiteSpace(desc) ? "No description." : desc;


            // Use is enabled if any action CanExecute
            bool canUse = false;
            if (def && def.Actions != null)
            {
                var ctx = new ItemContext(_owner, stack);
                for (int i = 0; i < def.Actions.Count; i++)
                {
                    var a = def.Actions[i];
                    if (a != null && a.CanExecute(ctx)) { canUse = true; break; }
                }
            }
            bool canDrop = stack.Instance.Quantity > 0;

            SetDetailButtons(canUse, canDrop);
        }

        private void SetDetailButtons(bool canUse, bool canDrop)
        {
            if (detailUseButton) detailUseButton.interactable = canUse;
            if (detailDropButton) detailDropButton.interactable = canDrop;
        }

        // -------- selection & actions --------
        private void Select(Guid id)
        {
            _selectedId = id;
            UpdateDetails();
            RefreshSelectionFrames();
        }

        private void RefreshSelectionFrames()
        {
            if (!gridParent) return;

            foreach (Transform child in gridParent)
            {
                var frame = child.Find("SelectedFrame");
                if (!frame) continue;

                var meta = child.GetComponent<RPG.Adapters.InventorySlotMeta>();
                bool isSelected = false;

                if (meta != null && System.Guid.TryParse(meta.InstanceId, out var id))
                    isSelected = (id == _selectedId);

                frame.gameObject.SetActive(isSelected);
            }
        }



        private bool TryGetSelected(out ItemStack stack)
        {
            stack = default;
            if (_lastSnapshot == null || _lastSnapshot.Count == 0) return false;

            // if no selection yet, pick first
            if (_selectedId == Guid.Empty)
                _selectedId = _lastSnapshot[0].Instance.InstanceId;

            for (int i = 0; i < _lastSnapshot.Count; i++)
            {
                var s = _lastSnapshot[i];
                if (s.Instance.InstanceId == _selectedId) { stack = s; return true; }
            }
            return false;
        }

        private void EnsureValidSelection()
        {
            if (_lastSnapshot == null || _lastSnapshot.Count == 0) { _selectedId = Guid.Empty; return; }

            // if current selection is gone, choose first
            for (int i = 0; i < _lastSnapshot.Count; i++)
                if (_lastSnapshot[i].Instance.InstanceId == _selectedId) return;

            _selectedId = _lastSnapshot[0].Instance.InstanceId;
        }



        private void OnNavigatePerformed(InputAction.CallbackContext ctx)
        {
            if (!UIMenuManager.HasInstance || UIMenuManager.Instance.Current != MenuType.Inventory)
                return;

            _navInput = ctx.ReadValue<Vector2>();

            if (_navInput.sqrMagnitude < 0.25f)
                return;

            // If this is a NEW press (we weren't holding before), do an immediate step
            if (!_navHeld)
            {
                PerformNavigationStep(_navInput);
                _navHeld = true;
                _nextNavTime = Time.unscaledTime + initialRepeatDelay; // wait before starting repeat
            }
            else
            {
                // Already holding, just update direction; repeats handled in Update()
                // no immediate extra step here
            }
        }


        private void OnNavigateCanceled(InputAction.CallbackContext ctx)
        {
            _navInput = Vector2.zero;
            _navHeld = false;
        }

        private void PerformNavigationStep(Vector2 input)
        {
            if (_lastSnapshot == null || _lastSnapshot.Count == 0)
                return;

            int delta;

            // Horizontal navigation first (more natural for inventory grids)
            if (Mathf.Abs(input.x) >= Mathf.Abs(input.y))
                delta = input.x > 0 ? +1 : -1;
            else
                delta = input.y > 0 ? -1 : +1;

            // get current index
            int currentIndex = 0;
            if (_selectedId != Guid.Empty)
            {
                for (int i = 0; i < _lastSnapshot.Count; i++)
                {
                    if (_lastSnapshot[i].Instance.InstanceId == _selectedId)
                    {
                        currentIndex = i;
                        break;
                    }
                }
            }

            int newIndex = currentIndex + delta;
            if (newIndex < 0) newIndex = _lastSnapshot.Count - 1;
            if (newIndex >= _lastSnapshot.Count) newIndex = 0;

            // update selection
            var newStack = _lastSnapshot[newIndex];
            _selectedId = newStack.Instance.InstanceId;

            UpdateDetails();
            RefreshSelectionFrames();

            // move UI-pointer to the selected slot
            if (EventSystem.current && gridParent)
            {
                int slotIndex = 0;
                Transform target = null;

                foreach (Transform child in gridParent)
                {
                    var meta = child.GetComponent<RPG.Adapters.InventorySlotMeta>();
                    if (meta == null) continue;

                    if (slotIndex == newIndex)
                    {
                        target = child;
                        break;
                    }

                    slotIndex++;
                }

                if (target != null)
                    EventSystem.current.SetSelectedGameObject(target.gameObject);
            }
        }


        public void OnClickUse()
        {
            if (!TryGetSelected(out var stack)) return;
            InventoryEvents.Publish(new ItemUseRequested(_owner, stack.Instance.InstanceId));
            Debug.Log("[InventoryUIPresenter] UseSelected invoked via detailUseButton 111");
        }

        private void OnClickDrop()
        {
            if (!TryGetSelected(out var stack)) return;
            InventoryEvents.Publish(new ItemDropRequested(_owner, stack.Instance.InstanceId, 1));
        }

        public void UseSelected()
        {
            // If we have a Use button in the details panel, use it as the single source of truth
            if (detailUseButton != null)
            {
                // If the button is disabled, treat that as "cannot use"
                if (!detailUseButton.interactable)
                    return;

                // This will go through exactly the same logic as mouse click
                detailUseButton.onClick.Invoke();
                return;
            }

            // Fallback path if for some reason there's no detailUseButton wired
            if (!TryGetSelected(out var stack)) return;

            var def = stack.Def;
            if (!def || def.Actions == null || def.Actions.Count == 0)
                return;

            // Same CanExecute logic we use in UpdateDetails()
            bool canUse = false;
            var ctx = new ItemContext(_owner, stack);
            foreach (var a in def.Actions)
            {
                if (a != null && a.CanExecute(ctx))
                {
                    canUse = true;
                    break;
                }
            }

            if (!canUse) return;

            InventoryEvents.Publish(new ItemUseRequested(_owner, stack.Instance.InstanceId));
        }



        public void DropSelected()
        {
            if (!TryGetSelected(out var stack)) return;
            InventoryEvents.Publish(new ItemDropRequested(_owner, stack.Instance.InstanceId, 1));
        }

    }
}
