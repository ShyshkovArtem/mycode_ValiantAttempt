
namespace RPG.Inventory { 

    using RPG.Events;
    using RPG.Items;
    using System.Linq;
    using UnityEngine;

    public class InventoryService
    {
        private readonly GameObject _owner;
        private readonly InventoryModel _model;
        private readonly IInventoryPersistence _persistence;


        public InventoryService(GameObject owner, int capacity, IInventoryPersistence persistence)
        {
            _owner = owner; _model = new InventoryModel(capacity); _persistence = persistence;
            Restore();
            RPG.Events.InventoryEvents.Subscribe<RPG.Events.InventorySyncRequested>(OnSyncRequested);
            RPG.Events.InventoryEvents.Subscribe<RPG.Events.ItemPickupRequested>(OnPickupRequested);
            RPG.Events.InventoryEvents.Subscribe<RPG.Events.ItemUseRequested>(OnUseRequested);
            RPG.Events.InventoryEvents.Subscribe<RPG.Events.ItemDropRequested>(OnDropRequested);
        }


        public void Dispose()
        {
            RPG.Events.InventoryEvents.Unsubscribe<RPG.Events.InventorySyncRequested>(OnSyncRequested);
            RPG.Events.InventoryEvents.Unsubscribe<RPG.Events.ItemPickupRequested>(OnPickupRequested);
            RPG.Events.InventoryEvents.Unsubscribe<RPG.Events.ItemUseRequested>(OnUseRequested);
            RPG.Events.InventoryEvents.Unsubscribe<RPG.Events.ItemDropRequested>(OnDropRequested);
        }

        private void Restore()
        {
            var saved = _persistence?.Load(_owner);
            if (saved != null)
            {
                foreach (var inst in saved)
                    _model.TryAdd(new ItemStack(inst));
                PublishChanged();
            }
        }

        private void OnSyncRequested(InventorySyncRequested evt)
        {
            if (evt.Owner != null && evt.Owner != _owner) return; // ignore others
            PublishChanged(); // sends current model snapshot
        }

        private void Persist()
        {
            _persistence?.Save(_owner, _model.Stacks.Select(s => s.Instance).ToList());
        }

        private void OnPickupRequested(ItemPickupRequested evt)
        {
            if (evt.Picker != _owner) return;
            var incoming = evt.Stack;
            bool ok = _model.TryAdd(incoming);
            if (ok)
            {
                PublishChanged();
                Persist();
                InventoryEvents.Publish(new ItemPickupResolved(_owner, evt.Stack));
            }
            else
            {
                // Optional: publish a failure event
            }
        }

        private void OnUseRequested(RPG.Events.ItemUseRequested evt)
        {
            if (evt.User != _owner) return;
            if (!_model.TryFind(evt.InstanceId, out var stack))
            {
                Debug.LogWarning($"[Inventory] Use: item {evt.InstanceId} not found for owner {_owner.name}");
                return;
            }

            bool executed = false;
            foreach (var action in stack.Def.Actions)
            {
                if (action == null) continue;
                if (!action.CanExecute(new RPG.Items.ItemContext(_owner, stack))) continue;

                action.Execute(new RPG.Items.ItemContext(_owner, stack));
                executed = true;
                break;
            }
            if (!executed)
                Debug.LogWarning($"[Inventory] Use: no action executed for {stack.Def.DisplayName}");

            if (stack.Def.DestroyOnUse)
            {
                _model.TryRemove(stack.Instance.InstanceId, 1, out _);
                PublishChanged();
                Persist();
            }
        }


        private void OnDropRequested(RPG.Events.ItemDropRequested evt)
        {
            if (evt.Owner != _owner) return;

            if (_model.TryRemove(evt.InstanceId, evt.Quantity, out var removed))
            {
                PublishChanged();
                Persist();
                RPG.Events.InventoryEvents.Publish(new RPG.Events.ItemDropped(_owner, removed));
                // (optional) spawn a world pickup here
            }
            else
            {
                Debug.LogWarning($"[Inventory] Drop: item {evt.InstanceId} not found for owner {_owner.name}");
            }
        }

        private void PublishChanged()
        {
            RPG.Events.InventoryEvents.Publish(
                new RPG.Events.InventoryChanged(_owner, _model.Stacks.ToList())
            );
        }
    }
}