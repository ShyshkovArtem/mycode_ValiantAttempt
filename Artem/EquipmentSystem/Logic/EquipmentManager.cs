using System;
using System.Collections.Generic;
using UnityEngine;

public class EquipmentManager : MonoBehaviour
{
    public static EquipmentManager Instance { get; private set; }
    public static event Action Ready;

    public Dictionary<EquipmentSlot, EquipmentItem> Equipped = new();

    [SerializeField] public PlayerStatsUpd playerStatsUpd;
    [SerializeField] private ItemDatabase itemDatabase;
    [SerializeField] private bool autoSave = true;

    void Awake()
    {
        Instance = this;

        foreach (EquipmentSlot s in Enum.GetValues(typeof(EquipmentSlot)))
            Equipped[s] = null;

        if (!playerStatsUpd) playerStatsUpd = FindFirstObjectByType<PlayerStatsUpd>();

        Ready?.Invoke();
    }

    void Start()
    {
        StartCoroutine(LoadAfterOneFrame());
    }

    private System.Collections.IEnumerator LoadAfterOneFrame()
    {
        yield return null;                    // let singletons wake
        if (itemDatabase != null)
            EquipmentSaveSystem.TryLoad(this, itemDatabase);
    }

    // ========================== Equip logic ==================================

    public bool CanEquip(EquipmentItem item) => item != null;

    public void Equip(EquipmentItem item, int inventoryIndex)
    {
        if (!CanEquip(item)) return;

        var slot = item.Slot;

        if (Equipped[slot] != null)
            EquipInv.Instance.Add(Equipped[slot]);

        Equipped[slot] = item;
        EquipInv.Instance.RemoveAt(inventoryIndex);

        playerStatsUpd?.ApplyEquipmentToCharacter();
        UIEvents.RaiseEquipmentChanged();
        UIEvents.RaiseInventoryChanged();

        if (autoSave && itemDatabase) EquipmentSaveSystem.Save(this, itemDatabase);
    }

    public void Unequip(EquipmentSlot slot)
    {
        var item = Equipped[slot];
        if (item == null) return;

        EquipInv.Instance.Add(item);
        Equipped[slot] = null;

        playerStatsUpd?.ApplyEquipmentToCharacter();
        UIEvents.RaiseEquipmentChanged();
        UIEvents.RaiseInventoryChanged();

        if (autoSave && itemDatabase) EquipmentSaveSystem.Save(this, itemDatabase);
    }

    public void UnequipAll(bool addToInventory = true)
    {
        bool changed = false;

        foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
        {
            var item = Equipped[slot];
            if (item == null) continue;

            if (addToInventory && EquipInv.Instance != null)
                EquipInv.Instance.Add(item);

            Equipped[slot] = null;
            changed = true;
        }

        if (!changed) return;

        playerStatsUpd?.ApplyEquipmentToCharacter();
        UIEvents.RaiseEquipmentChanged();
        UIEvents.RaiseInventoryChanged();

        if (autoSave && itemDatabase) EquipmentSaveSystem.Save(this, itemDatabase);
    }

    public void Drop(EquipmentItem item, int inventoryIndex)
    {
        if (EquipInv.Instance.Items.Contains(item))
        {
            EquipInv.Instance.RemoveAt(inventoryIndex);
        }
        else
        {
            foreach (var kvp in Equipped)
                if (kvp.Value == item) { Unequip(kvp.Key); break; }
        }

        if (autoSave && itemDatabase) EquipmentSaveSystem.Save(this, itemDatabase);
    }

    void OnApplicationQuit()
    {
        if (autoSave && itemDatabase) EquipmentSaveSystem.Save(this, itemDatabase);
    }
}
