using RPG.Events;
using RPG.Items;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;



public class QuickUsePotionsBinderRaw : MonoBehaviour
{
    [Header("Owner")]
    [SerializeField] private GameObject owner;

    [Header("Potion IDs")]
    [SerializeField] private string healthPotionId = "potion_health_small";
    [SerializeField] private string manaPotionId = "potion_mana_small";

    [Header("Health Slot")]
    [SerializeField] private Image healthIcon;
    [SerializeField] private TMP_Text healthCount;

    [Header("Mana Slot")]
    [SerializeField] private Image manaIcon;
    [SerializeField] private TMP_Text manaCount;

    [Header("Options")]
    [SerializeField] private Sprite fallbackIcon;
    [SerializeField] private bool hideWhenZero = true;

    private IReadOnlyList<ItemStack> _lastSnapshot;

    private void Awake()
    {
        owner = FindFirstObjectByType<PlayerController>().gameObject;
        InventoryEvents.Subscribe<InventoryChanged>(OnInventoryChanged);
    }

    private void OnDestroy()
    {
        InventoryEvents.Unsubscribe<InventoryChanged>(OnInventoryChanged);
    }

    private void OnEnable()
    {
        InventoryEvents.Publish(new InventorySyncRequested(owner));
        if (_lastSnapshot != null) Rebuild(_lastSnapshot);
    }

    private void OnInventoryChanged(InventoryChanged evt)
    {
        if (owner != null && evt.Owner != owner) return;
        _lastSnapshot = evt.Snapshot;
        if (isActiveAndEnabled) Rebuild(_lastSnapshot);
    }

    private void Rebuild(IReadOnlyList<ItemStack> snapshot)
    {
        BindOne(snapshot, healthPotionId, healthIcon, healthCount);
        BindOne(snapshot, manaPotionId, manaIcon, manaCount);
    }

    private void BindOne(IReadOnlyList<ItemStack> snapshot, string id, Image iconImg, TMP_Text countTxt)
    {
        int total = 0;
        Sprite icon = null;

        if (snapshot != null)
        {
            for (int i = 0; i < snapshot.Count; i++)
            {
                var s = snapshot[i];
                var def = s.Def; if (def == null) continue;
                if (!string.Equals(def.Id, id, StringComparison.OrdinalIgnoreCase)) continue;

                total += Mathf.Max(0, s.Instance.Quantity);
                if (icon == null && def.Icon != null) icon = def.Icon;
            }
        }

        if (iconImg)
        {
            if (icon != null)
            {
                iconImg.sprite = icon;
                iconImg.color = Color.white;  // fully visible
            }
            else
            {
                iconImg.sprite = fallbackIcon;
                iconImg.color = new Color(1, 1, 1, 0); // transparent
            }

            iconImg.enabled = !hideWhenZero || total > 0;

            if (hideWhenZero && iconImg.transform.parent)
                iconImg.transform.parent.gameObject.SetActive(total > 0);
        }

        if (countTxt)
            countTxt.text = total > 1 ? total.ToString() : (total == 1 ? "1" : "");
    }
}
