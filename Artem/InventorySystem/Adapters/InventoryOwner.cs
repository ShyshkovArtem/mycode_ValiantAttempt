namespace RPG.Adapters
{
    using RPG.Inventory;
    using UnityEngine;


    // Attach to the Player (or any entity) to own an InventoryService
    public class InventoryOwner : MonoBehaviour
    {
        [SerializeField, Min(1)] private int capacity = 24;
        private InventoryService _service;


        private void Awake()
        {
            _service = new InventoryService(gameObject, capacity, new JsonInventoryPersistence());
        }
        private void OnDestroy() { _service?.Dispose(); }
    }
}
