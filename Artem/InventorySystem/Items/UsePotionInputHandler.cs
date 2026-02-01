using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using RPG.Items;
using RPG.Events;

namespace RPG.Inventory
{
    [RequireComponent(typeof(Adapters.InventoryOwner))]
    public sealed class UsePotionInputHandler : MonoBehaviour
    {
        [Header("Input (Unity Input System)")]
        public InputActionReference usePotionAction;

        // Keep the exact same delegate instance for Subscribe/Unsubscribe
        private Action<RPG.Events.InventoryChanged> _onInventoryChangedHandler;

        private System.Collections.Generic.List<ItemStack> _snapshot =
            new System.Collections.Generic.List<ItemStack>();

        private Adapters.InventoryOwner _owner;

        private void Awake()
        {
            _owner = GetComponent<Adapters.InventoryOwner>();
            // Bind the delegate once so the compiler knows the exact type
            _onInventoryChangedHandler = OnInventoryChanged;
        }

        private void OnEnable()
        {
            if (usePotionAction != null)
            {
                usePotionAction.action.performed += OnUsePotionPerformed;
                usePotionAction.action.Enable();
            }

            InventoryEvents.Subscribe(_onInventoryChangedHandler);   // explicit delegate
            InventoryEvents.Publish(new InventorySyncRequested(gameObject));
        }

        private void OnDisable()
        {
            if (usePotionAction != null)
            {
                usePotionAction.action.performed -= OnUsePotionPerformed;
                usePotionAction.action.Disable();
            }

            InventoryEvents.Unsubscribe(_onInventoryChangedHandler); // same instance
        }

        // Fully-qualify the parameter type to match the delegate exactly
        private void OnInventoryChanged(RPG.Events.InventoryChanged evt)
        {
            if (evt.Owner != gameObject) return;
            _snapshot = evt.Snapshot?.ToList() ?? new System.Collections.Generic.List<ItemStack>();
        }

        private void OnUsePotionPerformed(InputAction.CallbackContext ctx)
        {
            foreach (var stack in _snapshot)
            {
                var def = stack.Def;
                if (def == null) continue;
                if (def.Category != ItemCategory.Consumable) continue;
                if (stack.Instance.Quantity <= 0) continue;

                foreach (var action in def.Actions)
                {
                    if (action is UseHealthPotionAction hpAction)
                    {
                        var ctxObj = new ItemContext(gameObject, stack);
                        if (hpAction.CanExecute(ctxObj))
                        {
                            InventoryEvents.Publish(new ItemUseRequested(gameObject, stack.Instance.InstanceId));
                            return;
                        }
                    }
                }
            }

            Debug.Log($"[Input] No usable health potion found for '{gameObject.name}'");
        }
    }
}
