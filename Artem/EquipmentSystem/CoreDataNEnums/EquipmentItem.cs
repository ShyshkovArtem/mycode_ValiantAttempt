using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "RPG/Equipment Item")]
public class EquipmentItem : ScriptableObject
{
    public string ItemId;        // unique id (GUID/string)
    public string DisplayName;
    public Sprite Icon;
    public EquipmentSlot Slot;
    public List<StatModifier> Modifiers = new(); // e.g., +5 Attack, +2 Defense
}
