using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RPG.Adapters
{
    public class InventorySlotView : MonoBehaviour
    {
        public Image icon;
        public TMP_Text count;            // optional
        public GameObject selectedFrame;  // assign the child in prefab
        [HideInInspector] public string instanceId; // set by presenter
    }
}
