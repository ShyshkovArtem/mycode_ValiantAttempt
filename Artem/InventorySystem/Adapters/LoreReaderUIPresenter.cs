using RPG.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RPG.Adapters
{
    public class LoreReaderUIPresenter : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private Button closeButton;

        private GameObject _prevSelected;
        private PlayerInput _playerInput;

        // expose state
        public bool IsOpen => panelRoot && panelRoot.activeSelf;
        public float LastClosedTime { get; private set; }

        private void Awake()
        {
            InventoryEvents.Subscribe<ShowLoreRequested>(OnShowLore);

            if (closeButton)
                closeButton.onClick.AddListener(Close);

            _playerInput = FindFirstObjectByType<PlayerInput>();

            if (panelRoot)
                panelRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            InventoryEvents.Unsubscribe<ShowLoreRequested>(OnShowLore);

            if (closeButton)
                closeButton.onClick.RemoveListener(Close);
        }

        private void OnShowLore(ShowLoreRequested evt)
        {
            if (!panelRoot) return;

            if (titleText)
            {
                titleText.text = string.IsNullOrWhiteSpace(evt.Title) ? "Lore" : evt.Title;
                titleText.textWrappingMode = TextWrappingModes.NoWrap;
                titleText.overflowMode = TextOverflowModes.Ellipsis;
            }

            if (bodyText)
            {
                bodyText.textWrappingMode = TextWrappingModes.Normal;
                bodyText.overflowMode = TextOverflowModes.Overflow;
                bodyText.text = string.IsNullOrWhiteSpace(evt.Body) ? "No text." : evt.Body;
            }

            _prevSelected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;

            panelRoot.SetActive(true);

            // Lock menu input so Equip/Inventory/Skills tabs & LB/RB can't switch away
            UIMenuManager.SetInputLocked(true);

            // Switch to UI action map (if you use separate maps)
            _playerInput?.SwitchCurrentActionMap("UI");

            // Do NOT auto-select Close, so A doesn't immediately close panel
            if (EventSystem.current)
                EventSystem.current.SetSelectedGameObject(null);
        }

        public void Close()
        {
            if (!panelRoot) return;

            panelRoot.SetActive(false);
            LastClosedTime = Time.unscaledTime;

            // Unlock menu input again: tabs & LB/RB work now
            UIMenuManager.SetInputLocked(false);

            // Switch back to gameplay (or inventory) action map
            _playerInput?.SwitchCurrentActionMap("Gameplay");

            if (_prevSelected)
                EventSystem.current?.SetSelectedGameObject(_prevSelected);
        }
    }
}
