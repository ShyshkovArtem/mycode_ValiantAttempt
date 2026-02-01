// LEFT PANEL
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.Equipment { 
public class UIPaperDoll : MonoBehaviour
{
    [System.Serializable]
    private struct SlotWidget
    {
        public EquipmentSlot Slot;
        public Image IconImage;        // square image on your figure
        public Button UnequipButton;   // optional; can share same image button
    }

    [SerializeField] private SlotWidget[] slots;
    [SerializeField] private Button unequipAllButton;
    void OnEnable()
    {
        UIEvents.EquipmentChanged += Refresh;
        Refresh();
        foreach (var sw in slots)
            if (sw.UnequipButton) sw.UnequipButton.onClick.AddListener(() => OnUnequip(sw.Slot));

        if (unequipAllButton) unequipAllButton.onClick.AddListener(OnUnequipAll);
        UpdateActionsInteractable();

    }

    void OnDisable()
    {
        UIEvents.EquipmentChanged -= Refresh;

        foreach (var sw in slots)
            if (sw.UnequipButton) sw.UnequipButton.onClick.RemoveAllListeners();

        if (unequipAllButton) unequipAllButton.onClick.RemoveListener(OnUnequipAll);
    }

    void Refresh()
    {
        foreach (var sw in slots)
        {
            var item = EquipmentManager.Instance.Equipped[sw.Slot];
            if (sw.IconImage)
            {
                sw.IconImage.sprite = item ? item.Icon : null;
                sw.IconImage.enabled = item != null;
            }
        }
        UpdateActionsInteractable();
    }

    void OnUnequip(EquipmentSlot slot)
    {
        EquipmentManager.Instance.Unequip(slot);
        UpdateActionsInteractable();
    }

    void OnUnequipAll()
    {
        EquipmentManager.Instance.UnequipAll(addToInventory: true);
        UpdateActionsInteractable();
    }

    void UpdateActionsInteractable()
    {
        if (!unequipAllButton) return;
        bool anyEquipped = EquipmentManager.Instance.Equipped.Values.Any(v => v != null);
        unequipAllButton.interactable = anyEquipped;
    }
}
}
