namespace RPG.Adapters
{
    using RPG.Events;
    using RPG.Items;
    using UnityEngine;


    // Place on item pickup prefabs in the world
    public class ItemPickup : MonoBehaviour
    {
        public ItemDefinition Definition;
        [Min(1)] public int Quantity = 1;


        private void OnTriggerEnter(Collider other)
        {
            var owner = other.GetComponent<InventoryOwner>();
            if (!owner) return;
            var stack = new ItemStack(new ItemInstance(Definition.Id, Quantity));
            RPG.Events.InventoryEvents.Publish(new ItemPickupRequested(owner.gameObject, stack));
            // Optionally listen for ItemPickupResolved to destroy this pickup
            Destroy(gameObject);
        }
    }
    }