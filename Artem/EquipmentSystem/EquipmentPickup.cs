using UnityEngine;

namespace RPG.Equipment
{
    [RequireComponent(typeof(Collider))]
    public class EquipmentPickup : MonoBehaviour
    {
        [Tooltip("Which item this pickup represents")]
        public EquipmentItem item;

        [Tooltip("Should the pickup be consumed on collection?")]
        public bool destroyOnPickup = true;

        private void Reset()
        {
            // Auto configure collider as trigger
            var col = GetComponent<Collider>();
            if (col) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            // Check if it's the player
            var player = other.GetComponent<PlayerController>();
            if (player == null) return;

            if (item == null)
            {
                Debug.LogWarning("[ItemPickup] No EquipmentItem assigned!");
                return;
            }

            // Add to inventory
            if (EquipInv.Instance != null)
            {
                EquipInv.Instance.Add(item);
                UIEvents.RaiseInventoryChanged();
                Debug.Log($"[ItemPickup] {item.DisplayName} picked up!");
            }

            // Remove world object if consumed
            if (destroyOnPickup) Destroy(gameObject);
        }
    }
}
