using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Controls;

public class InputDeviceUISwitcher : MonoBehaviour
{
    [Header("Keyboard & Mouse UI Objects")]
    [SerializeField] private List<GameObject> keyboardMouseObjects = new();

    [Header("Gamepad UI Objects")]
    [SerializeField] private List<GameObject> gamepadObjects = new();

    [SerializeField] private bool debugLog = false;

    private enum DeviceType { KeyboardMouse, Gamepad }
    private DeviceType _currentDevice = DeviceType.KeyboardMouse;

    private void OnEnable()
    {
        InputSystem.onEvent += OnInputEvent;
        ApplyDevice(_currentDevice);
    }

    private void OnDisable()
    {
        InputSystem.onEvent -= OnInputEvent;
    }

    private void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
    {
        if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>())
            return;

        if (device == null)
            return;

        // ? STOP: ?? ????????????? ?????? ???? ???? ???????? ??????? ??????
        if (!HasRealButtonPress(device, eventPtr))
            return;

        if (debugLog)
            Debug.Log($"[DeviceSwitch] Device={device}, Layout={device.layout}, Type={device.GetType().Name}");

        if (device is Keyboard || device is Mouse)
        {
            SetDevice(DeviceType.KeyboardMouse);
            return;
        }

        string layout = device.layout.ToLower();

        // Your controller appears as Joystick / HID ? treat it as gamepad
        if (device is Gamepad ||
            device is Joystick ||
            layout.Contains("gamepad") ||
            layout.Contains("joystick") ||
            layout.Contains("hid"))
        {
            SetDevice(DeviceType.Gamepad);
            return;
        }
    }

    /// <summary>
    /// Detects if any button on the device was *actually pressed* in this event.
    /// Works in all Unity versions.
    /// </summary>
    private bool HasRealButtonPress(InputDevice device, InputEventPtr evt)
    {
        foreach (var ctrl in device.allControls)
        {
            if (ctrl is ButtonControl button)
            {
                float value = button.ReadValueFromEvent(evt);
                if (value > 0.5f)  // actual press, no noise
                    return true;
            }
        }
        return false;
    }

    private void SetDevice(DeviceType newDevice)
    {
        if (_currentDevice == newDevice)
            return;

        _currentDevice = newDevice;

        if (debugLog)
            Debug.Log($"[DeviceSwitch] Switched to: {newDevice}");

        ApplyDevice(newDevice);
    }

    private void ApplyDevice(DeviceType device)
    {
        bool isGamepad = device == DeviceType.Gamepad;

        foreach (var o in gamepadObjects)
            if (o) o.SetActive(isGamepad);

        foreach (var o in keyboardMouseObjects)
            if (o) o.SetActive(!isGamepad);
    }
}
