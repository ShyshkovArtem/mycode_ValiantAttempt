using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


// ============================================================================
// EVENTS (strongly?typed, decoupled)
// ============================================================================

namespace RPG.Events
{
    public interface IGameEvent { }


    public static class InventoryEvents
    {
        private static readonly Dictionary<Type, Delegate> _handlers = new();


        public static void Subscribe<T>(Action<T> handler) where T : IGameEvent
        {
            if (_handlers.TryGetValue(typeof(T), out var del))
                _handlers[typeof(T)] = Delegate.Combine(del, handler);
            else
                _handlers[typeof(T)] = handler;
        }


        public static void Unsubscribe<T>(Action<T> handler) where T : IGameEvent
        {
            if (_handlers.TryGetValue(typeof(T), out var del))
            {
                var currentDel = Delegate.Remove(del, handler);
                if (currentDel == null) _handlers.Remove(typeof(T));
                else _handlers[typeof(T)] = currentDel;
            }
        }


        public static void Publish<T>(T evt) where T : IGameEvent
        {
            if (_handlers.TryGetValue(typeof(T), out var del))
                (del as Action<T>)?.Invoke(evt);
        }
    }

    // Event types

    public readonly struct InventorySyncRequested : IGameEvent
    {
        public readonly GameObject Owner; // who to sync for (null = any)
        public InventorySyncRequested(GameObject owner) { Owner = owner; }
    }

    public readonly struct ShowLoreRequested : IGameEvent
    {
        public readonly GameObject User;
        public readonly string Title;
        public readonly string Body;
        public ShowLoreRequested(GameObject user, string title, string body)
        {
            User = user; Title = title; Body = body;
        }
    }

    public readonly struct ItemPickupRequested : IGameEvent
    {
        public readonly GameObject Picker; // who is picking
        public readonly RPG.Items.ItemStack Stack; // what & how many
        public ItemPickupRequested(GameObject picker, RPG.Items.ItemStack stack) { Picker = picker; Stack = stack; }
    }


    public readonly struct ItemPickupResolved : IGameEvent
    {
        public readonly GameObject Picker;
        public readonly RPG.Items.ItemStack Accepted;
        public ItemPickupResolved(GameObject picker, RPG.Items.ItemStack accepted) { Picker = picker; Accepted = accepted; }
    }


    public readonly struct InventoryChanged : IGameEvent
    {
        public readonly GameObject Owner;
        public readonly IReadOnlyList<RPG.Items.ItemStack> Snapshot;
        public InventoryChanged(GameObject owner, IReadOnlyList<RPG.Items.ItemStack> snapshot) { Owner = owner; Snapshot = snapshot; }
    }


    public readonly struct ItemUseRequested : IGameEvent
    {
        public readonly GameObject User;
        public readonly Guid InstanceId;
        public ItemUseRequested(GameObject user, Guid instanceId) { User = user; InstanceId = instanceId; }
    }

    public readonly struct ItemUsed : IGameEvent
    {
        public readonly GameObject User;
        public readonly RPG.Items.ItemStack ResultingStack;
        public ItemUsed(GameObject user, RPG.Items.ItemStack resulting) { User = user; ResultingStack = resulting; }
    }


    public readonly struct ItemDropRequested : IGameEvent
    {
        public readonly GameObject Owner;
        public readonly Guid InstanceId;
        public readonly int Quantity;
        public ItemDropRequested(GameObject owner, Guid instanceId, int quantity) { Owner = owner; InstanceId = instanceId; Quantity = quantity; }
    }


    public readonly struct ItemDropped : IGameEvent
    {
        public readonly GameObject Owner;
        public readonly RPG.Items.ItemStack Dropped;
        public ItemDropped(GameObject owner, RPG.Items.ItemStack dropped) { Owner = owner; Dropped = dropped; }
    }


    // Equipment hooks
    public readonly struct EquipmentChanged : IGameEvent
    {
        public readonly GameObject Owner;
        public EquipmentChanged(GameObject owner) { Owner = owner; }
    }
}