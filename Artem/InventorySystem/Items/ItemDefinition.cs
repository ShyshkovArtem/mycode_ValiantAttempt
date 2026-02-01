using System.Collections.Generic;
using UnityEngine;

namespace RPG.Items
{
    public enum ItemCategory { Lore, Consumable, Key, Misc, Equipment }

    [CreateAssetMenu(menuName = "RPG/Items/Item Definition")]
    public class ItemDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string Id;                 // Stable string id for savefiles
        public string DisplayName;
        public Sprite Icon;
        public ItemCategory Category;

        [Header("UI/Description")]
        [Tooltip("Which action's Description should be shown in the UI. 0 = first action.")]
        [SerializeField] private int primaryActionIndex = 0;
        [TextArea][SerializeField] private string overrideDescription;

        [Header("Stacking & Rules")]
        [Min(1)] public int MaxStack = 1; // 1 for non-stackables; >1 for consumables
        public bool DestroyOnUse;         // e.g., consume potion

        [Header("Behavior")]
        public List<ItemAction> Actions = new();

        [Header("Lore")]
        public TextAsset LoreText;        // Optional, for books/notes

        public ItemAction PrimaryAction
        {
            get
            {
                if (Actions == null || Actions.Count == 0) return null;
                var i = Mathf.Clamp(primaryActionIndex, 0, Actions.Count - 1);
                return Actions[i];
            }
        }

        /// What the UI should show:
        /// 1) overrideDescription, 2) Primary Action's Description, 3) Lore text, 4) fallback by category.
        public string GetDescriptionForUI()
        {
            if (!string.IsNullOrWhiteSpace(overrideDescription))
                return overrideDescription;

            var actionDesc = PrimaryAction != null ? PrimaryAction.Description : null;
            if (!string.IsNullOrWhiteSpace(actionDesc))
                return actionDesc;

            if (LoreText && !string.IsNullOrWhiteSpace(LoreText.text))
                return LoreText.text;

            return Category switch
            {
                ItemCategory.Consumable => "A consumable item.",
                ItemCategory.Equipment => "An equippable item.",
                ItemCategory.Key => "A key item.",
                ItemCategory.Lore => "A readable lore item.",
                _ => "No description.",
            };
        }

        private void OnValidate()
        {
            if (Actions == null || Actions.Count == 0) { primaryActionIndex = 0; return; }
            primaryActionIndex = Mathf.Clamp(primaryActionIndex, 0, Actions.Count - 1);
        }
    }
}
