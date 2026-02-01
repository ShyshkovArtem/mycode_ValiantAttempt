using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

public class CinemachineCameraPriorityManager : MonoBehaviour
{
    [System.Serializable]
    public class CameraRule
    {
        public CinemachineCamera cam;
        public int priority;           // outside/baseline priority
        public bool usesIsoMapping;    // mapping when THIS camera wins
    }

    [Header("Outside (baseline) profile")]
    public List<CameraRule> outsideProfile = new List<CameraRule>();

    [Header("Fallbacks")]
    [Tooltip("Priority to assign to any known camera that isn't mentioned by the active area or the outside profile.")]
    public int outsideFallbackPriority = 0;

    [Header("Who is the player?")]
    public string playerTag = "Player";
    public PlayerController playerController;

    private readonly List<CinemachineAreaProfile> _activeAreas = new List<CinemachineAreaProfile>();
    private readonly Dictionary<CinemachineCamera, int> _original = new Dictionary<CinemachineCamera, int>();
    private readonly HashSet<CinemachineCamera> _knownCams = new HashSet<CinemachineCamera>();

    private CinemachineBrain _brain;

    void Awake()
    {
        if (!playerController)
        {
            var tagged = GameObject.FindWithTag(playerTag);
            if (tagged) playerController = tagged.GetComponentInParent<PlayerController>();
        }

        var main = Camera.main;
        if (main) _brain = main.GetComponent<CinemachineBrain>();

        // Cache originals + seed known set from outside profile
        foreach (var r in outsideProfile)
        {
            if (!r.cam) continue;
            _knownCams.Add(r.cam);
            if (!_original.ContainsKey(r.cam))
                _original[r.cam] = r.cam.Priority;
        }
    }

    void OnEnable()
    {
#if CINEMACHINE_3_OR_NEWER
        if (_brain) _brain.CameraActivated += OnCameraActivated;
#endif
        Recompute();
    }

    void OnDisable()
    {
#if CINEMACHINE_3_OR_NEWER
        if (_brain) _brain.CameraActivated -= OnCameraActivated;
#endif
        // Restore priorities we changed (polite on domain unload)
        foreach (var kvp in _original)
        {
            if (kvp.Key) kvp.Key.Priority = kvp.Value;
        }
    }

    public void RegisterArea(CinemachineAreaProfile area)
    {
        if (!_activeAreas.Contains(area))
        {
            _activeAreas.Add(area);

            // Learn about the area's cameras
            foreach (var r in area.insideProfile)
            {
                if (!r.cam) continue;
                _knownCams.Add(r.cam);
                if (!_original.ContainsKey(r.cam))
                    _original[r.cam] = r.cam.Priority;
            }

            Recompute();
        }
    }

    public void UnregisterArea(CinemachineAreaProfile area)
    {
        if (_activeAreas.Remove(area))
        {
            Recompute();
        }
    }

    public void Recompute()
    {
        // Decide which area wins (if any)
        CinemachineAreaProfile winner = GetWinningArea();

        // Build a desired-state map of camera -> (priority, usesIso)
        var map = new Dictionary<CinemachineCamera, (int priority, bool usesIso)>();

        // 1) Outside baseline
        foreach (var r in outsideProfile)
        {
            if (!r.cam) continue;
            map[r.cam] = (r.priority, r.usesIsoMapping);
        }

        // 2) Winner overrides with its inside profile
        if (winner != null)
        {
            foreach (var r in winner.insideProfile)
            {
                if (!r.cam) continue;
                map[r.cam] = (r.priorityInside, r.usesIsoMapping);
            }
        }

        // 3) Apply target priorities
        foreach (var kvp in map)
        {
            if (kvp.Key) kvp.Key.Priority = kvp.Value.priority;
        }

        // 4) **Critical**: any known camera *not* in the map gets pushed to fallback outside priority
        foreach (var cam in _knownCams)
        {
            if (!cam) continue;
            if (!map.ContainsKey(cam))
            {
                cam.Priority = outsideFallbackPriority;
            }
        }

        // 5) Set mapping to match the highest-priority camera from the map (or fallback)
        ApplyMappingFromMap(map);
    }

    CinemachineAreaProfile GetWinningArea()
    {
        if (_activeAreas.Count == 0) return null;

        int bestOrder = int.MinValue;
        CinemachineAreaProfile winner = null;

        for (int i = 0; i < _activeAreas.Count; i++)
        {
            var a = _activeAreas[i];
            if (!a) continue;

            if (a.areaOrder > bestOrder)
            {
                bestOrder = a.areaOrder;
                winner = a;
            }
            else if (a.areaOrder == bestOrder)
            {
                // Tie ? most recently entered (later in the list)
                winner = a;
            }
        }
        return winner;
    }

    void ApplyMappingFromMap(Dictionary<CinemachineCamera, (int priority, bool usesIso)> map)
    {
        if (!playerController) return;

        CinemachineCamera bestCam = null;
        int bestP = int.MinValue;
        bool isoForBest = false;

        foreach (var kvp in map)
        {
            var cam = kvp.Key;
            var data = kvp.Value;
            if (!cam) continue;

            if (data.priority > bestP)
            {
                bestP = data.priority;
                bestCam = cam;
                isoForBest = data.usesIso;
            }
        }

        // If nothing in map (edge case), default to third person (or change if you prefer)
        //.SetIsoMapping(isoForBest);
    }

#if CINEMACHINE_3_OR_NEWER
    // Keep mapping perfectly in sync with the camera Cinemachine actually activates after blends
    void OnCameraActivated(CinemachineBrain brain, CinemachineCamera newCam, CinemachineCamera prevCam)
    {
        if (!playerController || newCam == null) return;

        // Check winner (inside) then outside profile
        var winner = GetWinningArea();
        if (winner != null)
        {
            foreach (var r in winner.insideProfile)
            {
                if (r.cam == newCam)
                {
                    playerController.SetIsoMapping(r.usesIsoMapping);
                    return;
                }
            }
        }

        foreach (var r in outsideProfile)
        {
            if (r.cam == newCam)
            {
                playerController.SetIsoMapping(r.usesIsoMapping);
                return;
            }
        }
    }
#endif
}
