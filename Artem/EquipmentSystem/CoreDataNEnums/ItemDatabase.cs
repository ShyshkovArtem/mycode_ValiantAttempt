// ItemDatabase.cs
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "RPG/Item Database")]
public class ItemDatabase : ScriptableObject
{
    public List<EquipmentItem> allItems = new();

    private Dictionary<string, EquipmentItem> _byId;

    void OnEnable()
    {
        _byId = new Dictionary<string, EquipmentItem>();
        foreach (var it in allItems)
        {
            if (!it) continue;
            if (string.IsNullOrEmpty(it.ItemId))
                Debug.LogWarning($"[ItemDatabase] Item '{it.name}' has empty ItemId!");
            else if (_byId.ContainsKey(it.ItemId))
                Debug.LogWarning($"[ItemDatabase] Duplicate ItemId '{it.ItemId}' on '{it.name}'");
            else
                _byId[it.ItemId] = it;
        }
    }

    public EquipmentItem GetById(string id)
    {
        if (string.IsNullOrEmpty(id) || _byId == null) return null;
        return _byId.TryGetValue(id, out var item) ? item : null;
    }
}
