using UnityEngine;

namespace RPG.Adapters
{
    [RequireComponent(typeof(InventoryOwner))]
    public class BindInventoryToUIFromPlayer : MonoBehaviour
    {
        private void Start()
        {
            var presenter = FindFirstObjectByType<InventoryUIPresenter>(FindObjectsInactive.Include);
            if (presenter != null)
            {
                presenter.SetOwner(gameObject);
            }
        }
    }
}
