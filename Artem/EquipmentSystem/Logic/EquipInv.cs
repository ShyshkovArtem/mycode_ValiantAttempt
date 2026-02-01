using System.Collections.Generic;
using UnityEngine;


    public class EquipInv : MonoBehaviour
    {
        public static EquipInv Instance { get; private set; }
        public List<EquipmentItem> Items = new();   // what shows in the RIGHT panel

    void Awake() => Instance = this;



    public void Add(EquipmentItem item)
        {
            Items.Add(item);
            UIEvents.RaiseInventoryChanged();
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= Items.Count) return;
            Items.RemoveAt(index);
            UIEvents.RaiseInventoryChanged();
        }

    public void Clear()
    {
        Items.Clear();
        UIEvents.RaiseInventoryChanged();
    }

}

