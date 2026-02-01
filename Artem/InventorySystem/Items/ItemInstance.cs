using System;
using UnityEngine;

namespace RPG.Items
{
    //A concrete runtime instance of an item (unique id, durability, etc.)
    [Serializable]
    public struct ItemInstance
    {
        public string DefinitionId;   // reference to ItemDefinition.Id
        public Guid InstanceId;       // unique per stack entry
        public int Quantity;          // count in this stack entry
        public float Durability;      // optional 0..1

        public ItemInstance(string defId, int qty)
        {
            DefinitionId = defId;
            InstanceId = Guid.NewGuid();
            Quantity = qty;
            Durability = 1f;
        }
    }
}
