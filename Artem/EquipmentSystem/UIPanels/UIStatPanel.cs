using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RPG.Equipment
{

    /// <summary>
    /// Middle panel: shows current stats from Character.combined*
    /// and previews deltas when an inventory item is selected.
    /// </summary>
    public class UIStatPanel : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Transform rowsRoot;      // Vertical layout parent
        [SerializeField] private UIStatRow rowPrefab;     // Row with 3 TMP fields

        [Header("Source")]
        [SerializeField] private Character character;     // Player character
        [SerializeField] private bool autoFindCharacter = true;

        // Fixed display order for readability
        private readonly string[] _order = new[] {
        "HP", "MP",
        "HPReg", "MPReg",
        "STR", "VIT", "INT",
        "MOV"
    };

        void Awake()
        {
            if (!character && autoFindCharacter)
                //character = FindFirstObjectByType<Character>(); // Sami: I think I found the problem why the player stats were not updating correctly.
                character = FindFirstObjectByType<PlayerController>().GetComponent<Character>();
        }

        void OnEnable()
        {
            RedrawCurrent();
            // These events are optional but recommended:
            // - EquipmentChanged: when you actually equip/unequip
            // - ItemSelected: when you single-select an item in the right panel
            UIEvents.EquipmentChanged += RedrawCurrent;
            UIEvents.ItemSelected += RedrawPreview;
        }

        void OnDisable()
        {
            UIEvents.EquipmentChanged -= RedrawCurrent;
            UIEvents.ItemSelected -= RedrawPreview;
        }

        // -------- Rendering --------

        void Clear()
        {
            if (!rowsRoot) return;
            for (int i = rowsRoot.childCount - 1; i >= 0; i--)
                Destroy(rowsRoot.GetChild(i).gameObject);
        }

        void RedrawCurrent()
        {
            if (!character || !rowsRoot || !rowPrefab) return;

            Clear();
            var current = ReadFromCharacter(character);

            foreach (var key in _order)
            {
                int val = current.TryGetValue(key, out var v) ? v : 0;
                var row = Instantiate(rowPrefab, rowsRoot);
                row.Set(key, val, val, 0); // no delta on current view
            }
        }

        void RedrawPreview(EquipmentItem candidate)
        {
            if (!character || !rowsRoot || !rowPrefab)
            {
                RedrawCurrent();
                return;
            }

            var current = ReadFromCharacter(character);
            var preview = new Dictionary<string, int>(current);

            if (candidate != null)
            {
                // Optional: remove equipped item in the same slot from the preview first
                if (EquipmentManager.Instance != null &&
                    EquipmentManager.Instance.Equipped.TryGetValue(candidate.Slot, out var equippedInSameSlot) &&
                    equippedInSameSlot != null)
                {
                    ApplyItemModifiers(preview, equippedInSameSlot, -1); // remove old
                }

                // Apply candidate item
                ApplyItemModifiers(preview, candidate, +1);

                // NOTE: The actual Character.combined* fields should be updated for real
                // on equip/unequip; this panel only previews that effect.
                // --> // here should be logic for changing stats (in your equip system)
            }

            // Draw rows with deltas
            Clear();
            foreach (var key in _order)
            {
                current.TryGetValue(key, out int cur);
                preview.TryGetValue(key, out int nxt);
                int delta = nxt - cur;

                var row = Instantiate(rowPrefab, rowsRoot);
                row.Set(key, cur, nxt, delta);
            }
        }

        // -------- Helpers --------

        // Pulls numbers from Character.combined* into a display dictionary
        Dictionary<string, int> ReadFromCharacter(Character c)
        {
            return new Dictionary<string, int>
        {
            { "HP",          c.combinedHealth },
            { "MP",            c.combinedMana },
            { "HPReg",    c.combinedHealthRegen },
            { "MPReg",      c.combinedManaRegen },
            { "STR",        c.combinedStrength },
            { "VIT",        c.combinedVitality },
            { "INT",    c.combinedIntelligence },
            { "MOV",  c.combinedMovementSpeed },
        };
        }

        // Applies or removes an item's modifiers to a preview dictionary.
        // Expects your EquipmentItem.Modifiers to target these stat names.
        void ApplyItemModifiers(Dictionary<string, int> dict, EquipmentItem item, int sign)
        {
            if (item == null) return;
            if (item.Modifiers == null) return;

            foreach (var mod in item.Modifiers)
            {
                // Map your StatType to the Character stat names used in this panel.
                // Extend the switch with any additional StatType values you use.
                string key = MapStatTypeToDisplayKey(mod.Type);
                if (string.IsNullOrEmpty(key)) continue;

                if (!dict.ContainsKey(key)) dict[key] = 0;
                dict[key] += sign * mod.Value;
            }
        }

        // Central place to translate your StatType to our display keys.
        // Add cases to match your actual StatType enum (Health, Mana, Strength, etc).
        string MapStatTypeToDisplayKey(StatType type)
        {
            switch (type)
            {
                // Common names – tweak to match your enum
                case StatType.HP: return "HP";
                case StatType.STR: return "STR";
                case StatType.HPReg: return "HPReg";
                case StatType.MPReg: return "MPReg";
                case StatType.VIT: return "VIT";
                case StatType.INT: return "INT";
                case StatType.MOV: return "MOV";


                default: return null;
            }
        }
    }
}
