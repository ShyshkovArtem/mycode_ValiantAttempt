using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UIEquipmentListItem : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private GameObject selectionFrame;

    public EquipmentItem BoundItem { get; private set; }
    public int Index { get; private set; }

    public void Bind(EquipmentItem item, int index, UnityAction onClick)
    {
        BoundItem = item;
        Index = index;

        if (nameText) nameText.text = item ? item.DisplayName : "(null)";
        if (iconImage)
        {
            iconImage.sprite = item ? item.Icon : null;
            iconImage.enabled = item && item.Icon != null;
            iconImage.preserveAspect = true;
        }
        if (selectionFrame) selectionFrame.SetActive(false);

        if (button)
        {
            button.onClick.RemoveAllListeners();
            if (onClick != null) button.onClick.AddListener(onClick);
        }
    }

    public void SetSelected(bool selected)
    {
        if (selectionFrame) selectionFrame.SetActive(selected);
    }
}
