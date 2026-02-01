using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>
    /// Handles left-stick / WASD navigation between the menu buttons
    /// (Resume / Options / Main Menu) when the Menu/General panel is open.
    /// </summary>
    public class MenuPanelNavigator : MonoBehaviour
    {
        [Header("Buttons root (parent of Resume / Options / Main Menu)")]
        [SerializeField] private Transform buttonsRoot;

        [Header("Input")]
        [SerializeField] private InputActionReference navigateAction; // UI/Navigate (Vector2)

        [Header("Repeat")]
        [SerializeField] private float initialRepeatDelay = 0.35f;
        [SerializeField] private float repeatRate = 0.12f;

        private readonly List<Selectable> _buttons = new();
        private int _currentIndex = 0;

        private Vector2 _navInput;
        private float _nextNavTime;
        private bool _navHeld;

        private void Awake()
        {
            if (!buttonsRoot)
                buttonsRoot = transform;

            RefreshButtons();
        }

        private void OnEnable()
        {
            RefreshButtons();

            if (navigateAction != null)
            {
                navigateAction.action.performed += OnNavigatePerformed;
                navigateAction.action.canceled += OnNavigateCanceled;
            }

            // focus first button when the panel becomes active
            SelectIndex(_currentIndex);
        }

        private void OnDisable()
        {
            if (navigateAction != null)
            {
                navigateAction.action.performed -= OnNavigatePerformed;
                navigateAction.action.canceled -= OnNavigateCanceled;
            }
        }

        private void Update()
        {
            // Only navigate when the MENU / GENERAL panel is the active one
            if (!UIMenuManager.HasInstance || UIMenuManager.Instance.Current != MenuType.General)
                return;

            if (!_navHeld) return;
            if (_navInput.sqrMagnitude < 0.25f) return;
            if (_buttons.Count == 0) return;

            if (Time.unscaledTime >= _nextNavTime)
            {
                PerformNavigationStep(_navInput);
                _nextNavTime = Time.unscaledTime + repeatRate;
            }
        }

        private void RefreshButtons()
        {
            _buttons.Clear();
            if (!buttonsRoot) return;

            // find all Selectables directly under this root (or children, if you prefer)
            buttonsRoot.GetComponentsInChildren(true, _buttons);
            _buttons.RemoveAll(b => b == null);
            _currentIndex = Mathf.Clamp(_currentIndex, 0, Mathf.Max(0, _buttons.Count - 1));
        }

        private void OnNavigatePerformed(InputAction.CallbackContext ctx)
        {
            // Only react if Menu tab is currently open
            if (!UIMenuManager.HasInstance || UIMenuManager.Instance.Current != MenuType.General)
                return;

            _navInput = ctx.ReadValue<Vector2>();
            if (_navInput.sqrMagnitude < 0.25f)
                return;

            if (_buttons.Count == 0) return;

            if (!_navHeld)
            {
                // first press = immediate move
                PerformNavigationStep(_navInput);
                _navHeld = true;
                _nextNavTime = Time.unscaledTime + initialRepeatDelay;
            }
            else
            {
                // held; repeats handled in Update()
            }
        }

        private void OnNavigateCanceled(InputAction.CallbackContext ctx)
        {
            _navInput = Vector2.zero;
            _navHeld = false;
        }

        private void PerformNavigationStep(Vector2 input)
        {
            if (_buttons.Count == 0) return;

            // vertical list: up/down mainly, but left/right also move
            int delta;
            if (Mathf.Abs(input.y) >= Mathf.Abs(input.x))
                delta = input.y > 0 ? -1 : +1;   // up = previous, down = next
            else
                delta = input.x > 0 ? +1 : -1;   // right / left

            int newIndex = _currentIndex + delta;
            if (newIndex < 0) newIndex = _buttons.Count - 1;
            if (newIndex >= _buttons.Count) newIndex = 0;

            SelectIndex(newIndex);
        }

        private void SelectIndex(int index)
        {
            if (_buttons.Count == 0) return;

            _currentIndex = Mathf.Clamp(index, 0, _buttons.Count - 1);
            var selectable = _buttons[_currentIndex];
            if (!selectable || !selectable.gameObject.activeInHierarchy || !selectable.interactable)
                return;

            if (EventSystem.current)
                EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }
    }
}
