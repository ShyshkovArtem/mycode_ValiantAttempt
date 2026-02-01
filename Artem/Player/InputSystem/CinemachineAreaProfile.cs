using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

[RequireComponent(typeof(Collider))]
public class CinemachineAreaProfile : MonoBehaviour
{
    [System.Serializable]
    public class CameraRule
    {
        public CinemachineCamera cam;
        public int priorityInside = 20;
        public bool usesIsoMapping = false;
    }

    [Header("Area Order (who wins in overlaps)")]
    public int areaOrder = 0;

    [Header("Inside profile (for this area only)")]
    public List<CameraRule> insideProfile = new List<CameraRule>();

    [Header("Who can trigger")]
    public string playerTag = "Player";

    [Header("Manager (optional if there is only one in scene)")]
    public CinemachineCameraPriorityManager manager;

    Collider _col;

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

    void Awake()
    {
        _col = GetComponent<Collider>();
        if (!manager)
            manager = Object.FindFirstObjectByType<CinemachineCameraPriorityManager>();

    }

    void OnEnable()
    {
        if (manager && IsPlayerInsideAtStart())
            manager.RegisterArea(this);
    }

    void OnDisable()
    {
        if (manager) manager.UnregisterArea(this);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;
        if (manager) manager.RegisterArea(this);
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other)) return;
        if (manager) manager.UnregisterArea(this);
    }

    bool IsPlayer(Collider c)
    {
        if (c.CompareTag(playerTag)) return true;
        var t = c.transform;
        while (t != null)
        {
            if (t.CompareTag(playerTag)) return true;
            t = t.parent;
        }
        return false;
    }

    bool IsPlayerInsideAtStart()
    {
        var player = GameObject.FindWithTag(playerTag);
        if (!player || !_col) return false;
        var pc = player.GetComponentInParent<OldPlayerController>();
        if (!pc) return false;

        Vector3 p = pc.transform.position;
        Vector3 cp = _col.ClosestPoint(p);
        return (cp - p).sqrMagnitude < 1e-6f;
    }
}
