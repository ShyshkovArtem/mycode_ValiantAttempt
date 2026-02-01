using System;
using System.Collections;
using System.Collections.Generic;
using RPG.Events;
using RPG.Items;
using UnityEngine;

public class ClearOnLoad : MonoBehaviour
{
    [Header("Owner (whose inventory/equipment to clear)")]
    [Tooltip("If null, the script will still attempt equipment clear via singleton. For inventory, set the owner used by your InventoryService.")]
    public GameObject owner;

    [Header("What to clear?")]
    public bool clearInventory = false;
    public bool clearEquipment = false;

    [Header("Inventory Mode")]
    [Tooltip("If true, clears entire inventory. If false, uses filters below.")]
    public bool clearAllInventory = false;

    [Tooltip("If set, these ItemDefinition Ids will be cleared (e.g., \"potion_health_small\"")]
    public List<string> itemIdsToClear = new List<string>();

    [Tooltip("Filter by item categories (used only when Clear All is OFF).")]
    public bool clearLore = false;
    public bool clearConsumables = false;
    public bool clearKeys = false;
    public bool clearMisc = false;
    public bool clearEquipmentItems = false;

    [Header("Timing")]
    [Tooltip("Delay to allow singletons/services to initialize before clearing.")]
    public float delay = 0.1f;

    private void Start()
    {
        if (clearInventory || clearEquipment)
            StartCoroutine(ClearRoutine());
    }

    private IEnumerator ClearRoutine()
    {
        // Wait a moment for services/singletons to come up
        yield return new WaitForSeconds(delay);

        if (clearInventory)
            yield return ClearInventoryRoutine();

        if (clearEquipment)
            ClearEquipment();
    }

    // ----------------- INVENTORY -----------------

    /// <summary>
    /// Clears inventory either entirely or based on filters by publishing drop requests
    /// for each matching stack in the latest snapshot.
    /// </summary>
    private IEnumerator ClearInventoryRoutine()
    {
        // Request a fresh snapshot
        IReadOnlyList<ItemStack> snapshot = null;
        void OnInv(InventoryChanged e)
        {
            if (owner != null && e.Owner != owner) return;
            snapshot = e.Snapshot;
        }

        InventoryEvents.Subscribe<InventoryChanged>(OnInv);
        InventoryEvents.Publish(new InventorySyncRequested(owner));

        // Wait up to a short time for the snapshot (usually immediate)
        float t = 0f;
        while (snapshot == null && t < 1.0f)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        InventoryEvents.Unsubscribe<InventoryChanged>(OnInv);

        if (snapshot == null)
        {
            Debug.LogWarning("[ClearOnLoad] Inventory snapshot was not received; skipping inventory clear.");
            yield break;
        }

        if (clearAllInventory)
        {
            // Drop everything
            for (int i = 0; i < snapshot.Count; i++)
            {
                var s = snapshot[i];
                InventoryEvents.Publish(new ItemDropRequested(owner, s.Instance.InstanceId, s.Instance.Quantity));
            }
            Debug.Log("[ClearOnLoad] Inventory cleared: ALL items dropped.");
            yield break;
        }

        // Build matchers based on category flags and ids
        bool MatchByCategory(ItemDefinition def)
        {
            if (!def) return false;
            switch (def.Category)
            {
                case ItemCategory.Lore: return clearLore;
                case ItemCategory.Consumable: return clearConsumables;
                case ItemCategory.Key: return clearKeys;
                case ItemCategory.Misc: return clearMisc;
                case ItemCategory.Equipment: return clearEquipmentItems;
                default: return false;
            }
        }

        bool MatchById(ItemDefinition def)
        {
            if (!def || itemIdsToClear == null || itemIdsToClear.Count == 0) return false;
            for (int i = 0; i < itemIdsToClear.Count; i++)
            {
                var id = itemIdsToClear[i];
                if (!string.IsNullOrWhiteSpace(id) &&
                    string.Equals(def.Id, id, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        int removed = 0;

        for (int i = 0; i < snapshot.Count; i++)
        {
            var s = snapshot[i];
            var def = s.Def;

            bool match = MatchById(def) || MatchByCategory(def);

            if (match)
            {
                InventoryEvents.Publish(new ItemDropRequested(owner, s.Instance.InstanceId, s.Instance.Quantity));
                removed += s.Instance.Quantity;
            }
        }

        Debug.Log($"[ClearOnLoad] Inventory cleared (filtered). Removed total items: {removed}.");
    }

    // ----------------- EQUIPMENT -----------------

    private void ClearEquipment()
    {
        if (EquipmentManager.Instance != null)
        {
            EquipmentManager.Instance.UnequipAll(addToInventory: false);  // remove without returning to inventory
            EquipInv.Instance.Clear();
            UIEvents.RaiseEquipmentChanged();
            Debug.Log("[ClearOnLoad] Equipment cleared via EquipmentManager.");
            return;
        }
    }
}
