using System;
using UnityEngine;

namespace RPG.Items
{
    //Facade for UI/events; resolves definition lazily from Resources/Items.
    [Serializable]
    public struct ItemStack
    {
        public ItemInstance Instance;

        [NonSerialized] private ItemDefinition _cachedDef;

        public ItemStack(ItemInstance inst)
        {
            Instance = inst;
            _cachedDef = null;
        }

        public ItemDefinition Def
        {
            get
            {
                if (_cachedDef == null)
                {
                    var defId = Instance.DefinitionId;
                    var all = Resources.LoadAll<ItemDefinition>("Items"); // ONLY from Resources/Items

                    for (int i = 0; i < all.Length; i++)
                    {
                        var d = all[i];
                        if (d != null && d.Id == defId) { _cachedDef = d; break; }
                    }

                    if (_cachedDef == null)
                    {
                        Debug.LogError($"[ItemStack] ItemDefinition not found for Id='{defId}'. " +
                                       "Ensure an ItemDefinition asset with that Id exists under 'Resources/Items'.");
                    }
                }
                return _cachedDef;
            }
        }
    }
}
