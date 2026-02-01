using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class EquipmentSaveData
{
    public List<string> inventoryItemIds = new();           // all items in inventory
    public List<EquippedSlotEntry> equipped = new();         // per-slot mapping
}

[Serializable]
public class EquippedSlotEntry
{
    public string slotName;   // EquipmentSlot enum name (e.g., "Head")
    public string itemId;     // EquipmentItem.ItemId or "" if empty
}

public static class EquipmentSaveSystem
{
    private const string KEY = "EQUIPMENT_SAVE_V1";

    public static void Save(EquipmentManager em, ItemDatabase db)
    {
        if (em == null || db == null)
        {
            Debug.LogWarning("[EquipmentSaveSystem] Save skipped (missing refs).");
            return;
        }

        var data = new EquipmentSaveData();

        // Inventory
        if (EquipInv.Instance != null && EquipInv.Instance.Items != null)
        {
            foreach (var item in EquipInv.Instance.Items)
                if (item != null && !string.IsNullOrEmpty(item.ItemId))
                    data.inventoryItemIds.Add(item.ItemId);
        }

        // Equipped
        foreach (var kv in em.Equipped)
        {
            var entry = new EquippedSlotEntry
            {
                slotName = kv.Key.ToString(),
                itemId = kv.Value != null ? kv.Value.ItemId : ""
            };
            data.equipped.Add(entry);
        }

        var json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(KEY, json);
        PlayerPrefs.Save();
        // Debug.Log($"[EquipmentSaveSystem] Saved: {json}");
    }

    public static bool TryLoad(EquipmentManager em, ItemDatabase db)
    {
        if (em == null || db == null) { Debug.LogWarning("[EquipmentSaveSystem] Load skipped (missing refs)."); return false; }
        if (!PlayerPrefs.HasKey(KEY)) return false;

        var json = PlayerPrefs.GetString(KEY);
        var data = JsonUtility.FromJson<EquipmentSaveData>(json);
        if (data == null) return false;

        // Clear current state
        EquipInv.Instance?.Items?.Clear();
        foreach (var slot in Enum.GetValues(typeof(EquipmentSlot)))
            em.Equipped[(EquipmentSlot)slot] = null;

        // Rebuild inventory
        if (EquipInv.Instance != null && EquipInv.Instance.Items != null)
        {
            foreach (var id in data.inventoryItemIds)
            {
                var item = db.GetById(id);
                if (item != null) EquipInv.Instance.Items.Add(item);
                else Debug.LogWarning($"[EquipmentSaveSystem] Missing item for id '{id}'");
            }
        }

        // Rebuild equipped
        foreach (var e in data.equipped)
        {
            if (!Enum.TryParse<EquipmentSlot>(e.slotName, out var slot)) continue;
            var item = string.IsNullOrEmpty(e.itemId) ? null : db.GetById(e.itemId);
            em.Equipped[slot] = item;
        }

        // Recompute stats
        em.GetComponent<EquipmentManager>()?.playerStatsUpd?.ApplyEquipmentToCharacter();

        // Notify UI
        UIEvents.RaiseInventoryChanged();
        UIEvents.RaiseEquipmentChanged();

        // Debug.Log($"[EquipmentSaveSystem] Loaded: {json}");
        return true;
    }

    public static void Clear()
    {
        PlayerPrefs.DeleteKey(KEY);
    }
}
