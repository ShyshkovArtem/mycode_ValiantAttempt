using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RPG.Adapters
{
    public class InventoryPanelController : MonoBehaviour
    {
        [Header("Panel Root")]
        [SerializeField] private GameObject inventoryRoot; // assign Inventory_Canvas or root panel

        [Header("Presenter")]
        [SerializeField] private InventoryUIPresenter presenter; // handles list/details binding

        [Header("Lore Panel")]
        [SerializeField] private LoreReaderUIPresenter loreReader; // << assign in Inspector

        [Header("Actions")]
        [SerializeField] private InputActionReference toggleAction;  // Gameplay/ToggleInventory (not used here)
        [SerializeField] private InputActionReference submitAction;  // UI/Submit  (A)
        [SerializeField] private InputActionReference cancelAction;  // UI/Cancel  (B)

        [Header("Focus")]
        [SerializeField] private Selectable firstSelect; // optional

        private void OnEnable()
        {
            UIMenuManager.InstanceOpened += OnMenuOpened;
            UIMenuManager.InstanceClosed += OnMenuClosed;

            if (submitAction != null)
            {
                submitAction.action.performed += OnSubmit;
                submitAction.action.Enable();
            }

            if (cancelAction != null)
            {
                cancelAction.action.performed += OnCancel;
                cancelAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            if (UIMenuManager.HasInstance)
            {
                UIMenuManager.InstanceOpened -= OnMenuOpened;
                UIMenuManager.InstanceClosed -= OnMenuClosed;
            }

            if (submitAction != null)
            {
                submitAction.action.performed -= OnSubmit;
                submitAction.action.Disable();
            }

            if (cancelAction != null)
            {
                cancelAction.action.performed -= OnCancel;
                cancelAction.action.Disable();
            }
        }

        private void Start()
        {
            if (!presenter)
                presenter = GetComponentInChildren<InventoryUIPresenter>(true);
            if (!loreReader)
                loreReader = GetComponentInChildren<LoreReaderUIPresenter>(true);
        }

        private void OnMenuOpened(MenuType type)
        {
            if (type != MenuType.Inventory) return;

            if (inventoryRoot) inventoryRoot.SetActive(true);

            presenter?.OpenWithDefaultSelection();

            if (EventSystem.current && inventoryRoot)
            {
                var target = firstSelect
                             ? firstSelect.gameObject
                             : inventoryRoot.GetComponentInChildren<Selectable>(true)?.gameObject;
                EventSystem.current.SetSelectedGameObject(target);
            }
        }

        private void OnMenuClosed(MenuType type)
        {
            if (type != MenuType.Inventory) return;

            if (inventoryRoot) inventoryRoot.SetActive(false);

            if (EventSystem.current)
                EventSystem.current.SetSelectedGameObject(null);
        }

        // ------------ Gamepad / keyboard shortcuts ------------

        private void OnSubmit(InputAction.CallbackContext _)
        {
            // Only act if the INVENTORY menu is currently open
            if (!UIMenuManager.HasInstance || UIMenuManager.Instance.Current != MenuType.Inventory)
                return;
            if (!presenter) return;

            // If lore is open, ignore A for inventory
            if (loreReader != null && loreReader.IsOpen)
                return;

            // If lore JUST closed this frame from the same A press, also ignore
            if (loreReader != null && (Time.unscaledTime - loreReader.LastClosedTime) < 0.05f)
                return;

            Debug.Log("[InventoryPanelController] Submit (A) pressed -> UseSelected");
            presenter.UseSelected();   // Use selected item (potion or book)
        }

        private void OnCancel(InputAction.CallbackContext _)
        {
            // Only act if the INVENTORY menu is currently open
            if (!UIMenuManager.HasInstance || UIMenuManager.Instance.Current != MenuType.Inventory)
                return;
            if (!presenter) return;

            // If lore is open, B should close the lore panel, NOT drop
            if (loreReader != null && loreReader.IsOpen)
            {
                Debug.Log("[InventoryPanelController] Cancel (B) pressed -> Close lore");
                loreReader.Close();
                return;
            }

            Debug.Log("[InventoryPanelController] Cancel (B) pressed -> DropSelected");
            presenter.DropSelected();  // Drop 1 of selected item
        }
    }
}
