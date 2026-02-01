// SkillSlot.cs
using UnityEngine;
using UnityEngine.UI;

public class SkillSlot : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Image frameImage;

    [Header("Frame states")]
    [SerializeField] private Sprite frameNormal;   // default frame
    [SerializeField] private Sprite frameSelected; // highlighted frame (like your first slot)

    public SkillData Data { get; private set; }

    private SkillPanel owner;

    public void Init(SkillData data, SkillPanel ownerPanel)
    {
        Data = data;
        owner = ownerPanel;
        iconImage.sprite = data.icon;
        SetSelected(false);
        GetComponent<Button>().onClick.RemoveAllListeners();
        GetComponent<Button>().onClick.AddListener(OnClick);
    }

    public void SetSelected(bool selected)
    {
        frameImage.sprite = selected ? frameSelected : frameNormal;
    }

    private void OnClick()
    {
        owner.OnSlotClicked(this);
    }
}
