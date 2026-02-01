using System;


    public static class UIEvents
    {
        public static event Action InventoryChanged;
        public static event Action EquipmentChanged;
        public static event Action<EquipmentItem> ItemSelected; // from RIGHT panel

        public static void RaiseInventoryChanged() => InventoryChanged?.Invoke();
        public static void RaiseEquipmentChanged() => EquipmentChanged?.Invoke();
        public static void RaiseItemSelected(EquipmentItem item) => ItemSelected?.Invoke(item);
    }



