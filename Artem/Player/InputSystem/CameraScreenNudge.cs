using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public class CameraScreenNudgeMinimal : MonoBehaviour
{
    [System.Serializable]
    public class CamBinding
    {
        public CinemachineCamera cam;
        [Tooltip("If true, this vcam also accepts mouse input for nudging.")]
        public bool allowMouse = false;

        [HideInInspector] public Transform originalFollow;
    }

    [Header("References")]
    [Tooltip("All vcams that can become active (priority switching, area volumes, etc).")]
    public List<CamBinding> vcams = new List<CamBinding>();
    [Tooltip("Fallback anchor if a vcam has no original Follow.")]
    public Transform playerTarget;

    [Header("Nudge")]
    [Tooltip("Max world-space nudge distance at full input.")]
    public float worldRange = 2f;
    [Tooltip("Smooth time for proxy motion (seconds).")]
    public float smoothTime = 0.08f;

    [Header("Keyboard (Arrows)")]
    public bool enableArrowKeys = true;
    public float arrowKeySensitivity = 1.0f;

    [Header("Gamepad Right Stick (Legacy Input Manager)")]
    public bool enableRightStick = true;
    public string rightStickXAxis = "CameraHorizontal";
    public string rightStickYAxis = "CameraVertical";
    [Range(0f, 1f)] public float rightStickDeadzone = 0.15f;
    public float rightStickSensitivity = 1.0f;

    [Header("Mouse")]
    [Tooltip("Per-camera; applied only if that vcam's 'allowMouse' is true.")]
    public float mouseSensitivity = 0.01f;

    // Internals
    Transform _proxy;
    Vector3 _deltaWorld, _deltaVel;

    CinemachineBrain _brain;
    CamBinding _current;       // current active/redirected binding
    Transform _anchor;         // original follow or fallback

#if CINEMACHINE_3_OR_NEWER
    void OnCameraActivated(CinemachineBrain brain, CinemachineCamera newCam, CinemachineCamera prevCam)
    {
        var next = FindBinding(newCam);
        if (next == _current) return;

        Restore(_current);
        RedirectToProxy(next);
    }
#endif

    void OnEnable()
    {
        // Proxy
        if (_proxy == null)
        {
            _proxy = new GameObject("CM_NudgeProxy").transform;
            _proxy.position = Vector3.zero;
            _proxy.rotation = Quaternion.identity;
        }

        // Cache originals once
        for (int i = 0; i < vcams.Count; i++)
        {
            var b = vcams[i];
            if (b?.cam == null) continue;
            if (b.originalFollow == null) b.originalFollow = b.cam.Follow;
        }

        // Brain hook
        var main = Camera.main;
        if (main) _brain = main.GetComponent<CinemachineBrain>();

#if CINEMACHINE_3_OR_NEWER
        if (_brain) _brain.CameraActivated += OnCameraActivated;
#endif
        RedirectToProxy(GetMostRelevantBinding());
    }

    void OnDisable()
    {
#if CINEMACHINE_3_OR_NEWER
        if (_brain) _brain.CameraActivated -= OnCameraActivated;
#endif
        Restore(_current);
        _current = null;
        _anchor = null;
    }

    void Update()
    {
        if (_current == null) return;

        // --- Gather input: arrows + right stick + (optional per-camera) mouse ---
        Vector2 input = Vector2.zero;

        if (enableArrowKeys)
        {
            float kx = 0f, ky = 0f;
            if (Input.GetKey(KeyCode.LeftArrow)) kx -= 1f;
            if (Input.GetKey(KeyCode.RightArrow)) kx += 1f;
            if (Input.GetKey(KeyCode.DownArrow)) ky -= 1f;
            if (Input.GetKey(KeyCode.UpArrow)) ky += 1f;

            if (kx != 0f || ky != 0f)
                input += new Vector2(kx, ky) * arrowKeySensitivity;
        }

        if (enableRightStick)
        {
            float rx = 0f, ry = 0f;
            if (!string.IsNullOrEmpty(rightStickXAxis)) rx = Input.GetAxisRaw(rightStickXAxis);
            if (!string.IsNullOrEmpty(rightStickYAxis)) ry = Input.GetAxisRaw(rightStickYAxis);

            Vector2 rs = new Vector2(rx, ry);
            if (rs.magnitude < rightStickDeadzone) rs = Vector2.zero;
            if (rs != Vector2.zero) input += rs * rightStickSensitivity;
        }

        if (_current.allowMouse)
        {
            float mx = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
            float my = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;
            if (Mathf.Abs(mx) > 0.0001f || Mathf.Abs(my) > 0.0001f)
                input += new Vector2(mx, my);
        }

        if (input.sqrMagnitude > 1f) input.Normalize();

        // Camera-space axes (right/up) from current render camera
        Transform camXf = Camera.main ? Camera.main.transform : transform;
        Vector3 right = camXf.right;
        Vector3 up = camXf.up;

        // Desired world-space nudge
        Vector3 desiredWorld = (right * input.x + up * input.y) * worldRange;

        // Smooth and apply
        _deltaWorld = Vector3.SmoothDamp(_deltaWorld, desiredWorld, ref _deltaVel, smoothTime);

        if (_proxy && _anchor)
        {
            _proxy.position = _anchor.position + _deltaWorld;
            _proxy.rotation = _anchor.rotation;
        }
    }

    // --- Helpers ---
    CamBinding GetMostRelevantBinding()
    {
        CamBinding best = null;
        int bestP = int.MinValue;

        for (int i = 0; i < vcams.Count; i++)
        {
            var b = vcams[i];
            var c = b?.cam;
            if (!c || !c.isActiveAndEnabled) continue;
            if (c.Priority > bestP)
            {
                bestP = c.Priority;
                best = b;
            }
        }
        return best;
    }

    CamBinding FindBinding(CinemachineCamera cam)
    {
        if (!cam) return null;
        for (int i = 0; i < vcams.Count; i++)
            if (vcams[i].cam == cam) return vcams[i];
        return null;
    }

    void RedirectToProxy(CamBinding b)
    {
        if (b == null) return;

        _anchor = b.originalFollow != null ? b.originalFollow : playerTarget;
        if (_anchor == null) _anchor = transform;

        if (_proxy == null)
            _proxy = new GameObject("CM_NudgeProxy").transform;

        _proxy.SetParent(null, true);
        _proxy.position = _anchor.position;
        _proxy.rotation = _anchor.rotation;

        b.cam.Follow = _proxy;
        _current = b;

        // reset nudge state when swapping cams
        _deltaWorld = Vector3.zero;
        _deltaVel = Vector3.zero;
    }

    void Restore(CamBinding b)
    {
        if (b == null) return;
        b.cam.Follow = b.originalFollow;
    }

    // Optional runtime helpers
    public void AddCamera(CinemachineCamera cam, bool allowMouse = false)
    {
        if (!cam) return;
        vcams.Add(new CamBinding { cam = cam, allowMouse = allowMouse, originalFollow = cam.Follow });
    }

    public void RemoveCamera(CinemachineCamera cam)
    {
        if (!cam) return;
        for (int i = vcams.Count - 1; i >= 0; --i)
        {
            if (vcams[i].cam == cam)
            {
                if (_current == vcams[i]) Restore(_current);
                vcams.RemoveAt(i);
                break;
            }
        }
    }
}
