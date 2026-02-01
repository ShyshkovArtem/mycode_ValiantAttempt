using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>
    /// Handles left-stick / d-pad / WASD navigation between the main menu buttons
    /// (Play / Settings / Quit) when the main menu panel is active.
    ///
    /// Gamepad "Submit" (A / Cross) is handled by the EventSystem + UI Input Module,
    /// so this script only moves the current selection.
    /// </summary>
    public class MainMenuNav : MonoBehaviour
    {
        [Header("Buttons root (parent of Play / Settings / Quit)")]
        [SerializeField] private Transform buttonsRoot;

        [Header("Input")]
        [Tooltip("Reference to UI/Navigate (Vector2) action in your UI action map.")]
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

            // Focus first button when the menu becomes active.
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

            // Grab all Selectables under the root
            buttonsRoot.GetComponentsInChildren(true, _buttons);
            _buttons.RemoveAll(b => b == null);

            _currentIndex = Mathf.Clamp(_currentIndex, 0, Mathf.Max(0, _buttons.Count - 1));
        }

        private void OnNavigatePerformed(InputAction.CallbackContext ctx)
        {
            _navInput = ctx.ReadValue<Vector2>();
            if (_navInput.sqrMagnitude < 0.25f)
                return;

            if (_buttons.Count == 0) return;

            if (!_navHeld)
            {
                // First press = immediate move
                PerformNavigationStep(_navInput);
                _navHeld = true;
                _nextNavTime = Time.unscaledTime + initialRepeatDelay;
            }
            // Held: repeats handled in Update()
        }

        private void OnNavigateCanceled(InputAction.CallbackContext ctx)
        {
            _navInput = Vector2.zero;
            _navHeld = false;
        }

        private void PerformNavigationStep(Vector2 input)
        {
            if (_buttons.Count == 0) return;

            // Vertical list: up/down preferred, but left/right also move
            int delta;
            if (Mathf.Abs(input.y) >= Mathf.Abs(input.x))
                delta = input.y > 0 ? -1 : +1;  // up = previous, down = next
            else
                delta = input.x > 0 ? +1 : -1;  // right / left

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
            if (!selectable ||
                !selectable.gameObject.activeInHierarchy ||
                !selectable.interactable)
                return;

            if (EventSystem.current)
                EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }
    }
}
