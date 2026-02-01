using UnityEngine;
using UnityEngine.EventSystems;

namespace RPG.UI
{
    public class UIButtonHighlight : MonoBehaviour,
        ISelectHandler, IDeselectHandler,
        IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private GameObject selectedFrame;

        private void OnEnable()
        {
            // If this button is already the selected object when it becomes active,
            // make sure the frame matches that state.
            if (selectedFrame && EventSystem.current &&
                EventSystem.current.currentSelectedGameObject == gameObject)
            {
                selectedFrame.SetActive(true);
            }
            else if (selectedFrame)
            {
                selectedFrame.SetActive(false);
            }
        }

        private void OnDisable()
        {
            // Always turn off on disable so we never keep "ghost" frames
            if (selectedFrame) selectedFrame.SetActive(false);
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (selectedFrame) selectedFrame.SetActive(true);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            if (selectedFrame) selectedFrame.SetActive(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (selectedFrame) selectedFrame.SetActive(true);

            // Sync EventSystem selection so gamepad & mouse stay in sync
            EventSystem.current?.SetSelectedGameObject(gameObject);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // Only hide frame if this button is NOT currently selected
            if (EventSystem.current &&
                EventSystem.current.currentSelectedGameObject == gameObject)
                return;

            if (selectedFrame) selectedFrame.SetActive(false);
        }
    }
}
