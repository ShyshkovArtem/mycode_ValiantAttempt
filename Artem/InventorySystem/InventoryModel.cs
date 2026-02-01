// ============================================================================
// INVENTORY — domain model 
// ============================================================================
namespace RPG.Inventory
{
    using System.Linq;
    using RPG.Items;
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    public interface IInventoryPersistence
    {
        void Save(GameObject owner, IReadOnlyList<ItemInstance> items);
        List<ItemInstance> Load(GameObject owner);
    }


    [Serializable]
    public class InventoryModel
    {
        public int Capacity;
        private readonly List<ItemStack> _stacks = new();
        public IReadOnlyList<ItemStack> Stacks => _stacks;


        public InventoryModel(int capacity) { Capacity = capacity; }


        public int Count(string defId) => _stacks.Where(s => s.Instance.DefinitionId == defId).Sum(s => s.Instance.Quantity);

        public bool TryAdd(ItemStack incoming)
        {
            var defId = incoming.Instance.DefinitionId;
            var def = incoming.Def;

            // Merge into existing stacks up to MaxStack
            for (int i = 0; i < _stacks.Count && incoming.Instance.Quantity > 0; i++)
            {
                var existing = _stacks[i];                       // copy value (struct)
                if (existing.Instance.DefinitionId != defId) continue;
                if (def.MaxStack <= 1) break;

                int space = def.MaxStack - existing.Instance.Quantity;
                if (space <= 0) continue;

                int move = Mathf.Min(space, incoming.Instance.Quantity);

                // modify the copy's instance
                var exInst = existing.Instance;
                exInst.Quantity += move;
                existing = new ItemStack(exInst);                // rebuild ItemStack (struct)

                _stacks[i] = existing;                           // write back into the list
                incoming.Instance.Quantity -= move;
            }

            // Add new stack entries as needed
            while (incoming.Instance.Quantity > 0)
            {
                if (_stacks.Count >= Capacity) return false; // not enough room
                int move = Mathf.Min(def.MaxStack, incoming.Instance.Quantity);
                _stacks.Add(new ItemStack(new ItemInstance(defId, move)));
                incoming.Instance.Quantity -= move;
            }
            return true;
        }


        public bool TryRemove(Guid instanceId, int qty, out Items.ItemStack removed)
        {
            for (int i = 0; i < _stacks.Count; i++)
            {
                var s = _stacks[i];
                if (s.Instance.InstanceId != instanceId) continue;

                string defId = s.Instance.DefinitionId; // capture before mutation
                int take = Mathf.Min(qty, s.Instance.Quantity);

                var inst = s.Instance;
                inst.Quantity -= take;

                removed = new Items.ItemStack(new Items.ItemInstance(defId, take));

                if (inst.Quantity <= 0) _stacks.RemoveAt(i);
                else _stacks[i] = new Items.ItemStack(inst);

                // ?? pack remaining stacks of this item
                Consolidate(defId);
                return true;
            }
            removed = default;
            return false;
        }


        private void Consolidate(string defId)
        {
            // Collect indices of stacks with the same definition
            var idxs = new List<int>();
            for (int i = 0; i < _stacks.Count; i++)
                if (_stacks[i].Instance.DefinitionId == defId) idxs.Add(i);

            if (idxs.Count <= 1) return;

            // Total quantity and get definition (for MaxStack)
            int total = 0;
            Items.ItemDefinition def = null;
            for (int k = 0; k < idxs.Count; k++)
            {
                var s = _stacks[idxs[k]];
                total += s.Instance.Quantity;
                if (def == null) def = s.Def;
            }
            if (def == null) return; // safety

            int max = Mathf.Max(1, def.MaxStack);

            // Fill first stack
            int firstIdx = idxs[0];
            var first = _stacks[firstIdx];
            var firstInst = first.Instance;
            int firstQty = Mathf.Min(max, total);
            firstInst.Quantity = firstQty;
            _stacks[firstIdx] = new Items.ItemStack(firstInst);
            total -= firstQty;

            // How many extra stacks we actually need after packing
            int neededExtra = (total + max - 1) / max; // 0..N

            // Fill following needed stacks (reusing existing ones)
            int p = 1;
            for (; p <= neededExtra && p < idxs.Count; p++)
            {
                int qty = Mathf.Min(max, total);
                var st = _stacks[idxs[p]];
                var inst = st.Instance;
                inst.Quantity = qty;
                _stacks[idxs[p]] = new Items.ItemStack(inst);
                total -= qty;
            }

            // Remove any leftover stacks we no longer need
            for (int r = idxs.Count - 1; r >= p; r--)
                _stacks.RemoveAt(idxs[r]);
        }


        public bool TryFind(Guid instanceId, out ItemStack stack)
        {
            foreach (var s in _stacks) if (s.Instance.InstanceId == instanceId) { stack = s; return true; }
            stack = default; return false;
        }
    }
}