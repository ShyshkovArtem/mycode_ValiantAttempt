using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fades any object with a FadingObject component that blocks the camera's view to the player,
/// with predictive/early fading for roofs & overhangs. Works with perspective or orthographic cameras.
/// </summary>
public class FadeObjectBlockingObject : MonoBehaviour
{
    // ---------------------------- Inspector ----------------------------

    [Header("References")]
    [SerializeField] private LayerMask LayerMask;
    [SerializeField] private Transform Target;
    [SerializeField] private Camera Camera;

    [Header("Fade Settings")]
    [SerializeField, Range(0f, 1f)] private float FadedAlpha = 0.33f;
    [SerializeField] private bool RetainShadows = true;
    [SerializeField] private Vector3 TargetPositionOffset = Vector3.up;
    [SerializeField, Min(0f)] private float FadeSpeed = 1f;

    [Header("Predictive Lead (helps roofs fade before entering)")]
    [Tooltip("How far ahead of the target (meters) to check for occluders.")]
    [SerializeField, Min(0f)] private float LeadDistance = 2.0f;

    [Tooltip("How high above the target (meters) to bias checks (catch roofs).")]
    [SerializeField, Min(0f)] private float LeadHeight = 1.25f;

    [Tooltip("If true, use target velocity (Rigidbody/CharacterController) for lead dir when moving.")]
    [SerializeField] private bool UseVelocityForLead = true;
    [SerializeField, Min(0f)] private float VelocityThreshold = 0.15f;

    [Header("Detection Mode")]
    [Tooltip("Use a camera-aligned corridor (frustum slice) sweep. Best for iso/top-down.")]
    [SerializeField] private bool UseFrustumCorridor = true;

    [Tooltip("Fallback: use thicker rays (sphere casts) + small cone samples.")]
    [SerializeField] private bool UseSphereCastFallback = true;

    [Header("Frustum Corridor (camera-aligned)")]
    [SerializeField, Range(0.02f, 0.5f)] private float CorridorWidthViewport = 0.20f;
    [SerializeField, Range(0.02f, 0.5f)] private float CorridorHeightViewport = 0.24f;
    [SerializeField, Range(2, 16)] private int CorridorSteps = 6;

    [Header("Sphere Cast Fallback")]
    [SerializeField, Min(0f)] private float CastRadius = 0.4f;
    [SerializeField, Range(0, 6)] private int ExtraConeSamples = 2;
    [SerializeField, Range(0f, 20f)] private float ConeAngleDeg = 6f;

    [Header("Performance")]
    [Tooltip("Size of the non-alloc OverlapBox buffer used per slab. Increase for denser scenes.")]
    [SerializeField, Min(8)] private int OverlapBufferSize = 64;

    [Header("Read Only Data")]
    [SerializeField] private List<FadingObject> ObjectsBlockingView = new List<FadingObject>(); // inspector view only

    // ---------------------------- Runtime state ----------------------------

    // Internal set mirrors ObjectsBlockingView to avoid duplicates & speed lookups (Unity doesn't serialize HashSet).
    private readonly HashSet<FadingObject> _blockingSet = new HashSet<FadingObject>();

    private readonly Dictionary<FadingObject, Coroutine> RunningCoroutines = new Dictionary<FadingObject, Coroutine>();

    // Reusable buffers (no per-frame allocs)
    private RaycastHit[] _hits;            // for sphere casts
    private Collider[] _overlapBuf;        // for overlap boxes

    private Coroutine _loop;

    // Shader property IDs (avoid string hashing every frame)
    private static readonly int _SrcBlend = Shader.PropertyToID("_SrcBlend");
    private static readonly int _DstBlend = Shader.PropertyToID("_DstBlend");
    private static readonly int _ZWrite = Shader.PropertyToID("_ZWrite");
    private static readonly int _Surface = Shader.PropertyToID("_Surface");

    // ---------------------------- Lifecycle ----------------------------

    private void Awake()
    {
        // allocate once, allow user to scale via inspector
        _hits = new RaycastHit[32];
        _overlapBuf = new Collider[Mathf.Max(8, OverlapBufferSize)];
    }

    private void OnEnable()
    {
        if (_loop == null) _loop = StartCoroutine(CheckForObjects());
    }

    private void OnDisable()
    {
        if (_loop != null)
        {
            StopCoroutine(_loop);
            _loop = null;
        }

        // Stop any running fades
        foreach (var kv in RunningCoroutines)
        {
            if (kv.Value != null) StopCoroutine(kv.Value);
        }
        RunningCoroutines.Clear();

        // Instantly reset any faded objects to opaque and restore alpha
        foreach (var fo in ObjectsBlockingView)
        {
            if (fo == null || fo.Materials == null) continue;
            foreach (var m in fo.Materials)
            {
                if (!m) continue;
                SetAlpha(m, fo.InitialAlpha);
                SetOpaque(m);
            }
        }

        ObjectsBlockingView.Clear();
        _blockingSet.Clear();
    }

    // ---------------------------- Main Loop ----------------------------

    private IEnumerator CheckForObjects()
    {
        var frameHits = new HashSet<FadingObject>();

        while (true)
        {
            if (!Camera || !Target)
            {
                yield return null;
                continue;
            }

            frameHits.Clear();

            Vector3 camPos = Camera.transform.position;
            Vector3 tgtBase = Target.transform.position + TargetPositionOffset;

            // lead direction: velocity if available and above threshold, else target.forward
            Vector3 leadDir = GetLeadDirection();
            Vector3 leadPoint = tgtBase + (leadDir * LeadDistance) + (Vector3.up * LeadHeight);

            // ---- Primary: Frustum corridor sweep (camera-aligned) ----
            if (UseFrustumCorridor)
            {
                CastFrustumCorridor(Camera, camPos, leadPoint,
                    CorridorWidthViewport, CorridorHeightViewport, CorridorSteps, LayerMask, frameHits);

                // Safety: smaller corridor directly to target body/feet
                CastFrustumCorridor(Camera, camPos, tgtBase,
                    CorridorWidthViewport * 0.8f, CorridorHeightViewport * 0.8f,
                    Mathf.Max(3, CorridorSteps - 2), LayerMask, frameHits);
            }

            // ---- Fallback/augment: Sphere casts (thick rays) with small cone ----
            if (UseSphereCastFallback)
            {
                CastAndAccumulate(camPos, leadPoint, frameHits);
                CastConeSamples(camPos, leadPoint, frameHits);
                CastAndAccumulate(camPos, tgtBase, frameHits);
            }

            // Fade OUT all hits this frame
            foreach (var fo in frameHits) EnsureFadeOut(fo);

            // Fade IN anything no longer hit
            FadeObjectsNoLongerHit(frameHits);

            yield return null;
        }
    }

    // ---------------------------- Detection Helpers ----------------------------

    private void CastAndAccumulate(Vector3 from, Vector3 to, HashSet<FadingObject> hitSet)
    {
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist <= 0.0001f) return;
        dir /= dist;

        int hitCount = Physics.SphereCastNonAlloc(from, CastRadius, dir, _hits, dist, LayerMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            var fo = GetFadingObjectFromHit(_hits[i]);
            if (fo != null) hitSet.Add(fo);
        }
    }

    private void CastConeSamples(Vector3 from, Vector3 to, HashSet<FadingObject> hitSet)
    {
        int pairs = Mathf.Min(ExtraConeSamples, 3);
        if (pairs <= 0) return;

        Vector3 dir = (to - from);
        float dist = dir.magnitude;
        if (dist <= 0.0001f) return;
        dir /= dist;

        // Orthonormal basis around the ray
        Vector3 right = Vector3.Cross(Vector3.up, dir);
        if (right.sqrMagnitude < 1e-6f) right = Vector3.Cross(Vector3.forward, dir);
        right.Normalize();
        Vector3 up = Vector3.Cross(dir, right).normalized;

        float tan = Mathf.Tan(ConeAngleDeg * Mathf.Deg2Rad);

        for (int i = 0; i < pairs; i++)
        {
            // 4 offsets (no array alloc)
            Vector3 d1 = (dir + right * tan).normalized;
            Vector3 d2 = (dir - right * tan).normalized;
            Vector3 d3 = (dir + up * tan).normalized;
            Vector3 d4 = (dir - up * tan).normalized;

            CastAndAccumulate(from, from + d1 * dist, hitSet);
            CastAndAccumulate(from, from + d2 * dist, hitSet);
            CastAndAccumulate(from, from + d3 * dist, hitSet);
            CastAndAccumulate(from, from + d4 * dist, hitSet);
        }
    }

    /// <summary>
    /// Sweeps a camera-aligned corridor (thin frustum slice) from 'from' to 'to'
    /// and accumulates FadingObjects intersecting it. Works in Perspective or Ortho.
    /// Uses OverlapBoxNonAlloc to avoid allocations.
    /// </summary>
    private void CastFrustumCorridor(
        Camera cam,
        Vector3 from, Vector3 to,
        float widthViewport, float heightViewport,
        int steps, LayerMask mask,
        HashSet<FadingObject> hitSet)
    {
        if (!cam || steps < 2) return;

        widthViewport = Mathf.Clamp01(widthViewport);
        heightViewport = Mathf.Clamp01(heightViewport);
        Quaternion boxRotation = cam.transform.rotation;

        float pathLen = Vector3.Distance(from, to);
        float slabDepth = Mathf.Max(0.05f, pathLen / steps) * 0.6f;

        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            Vector3 pos = Vector3.Lerp(from, to, t);

            // Depth along camera forward
            float d = Vector3.Dot(pos - cam.transform.position, cam.transform.forward);
            if (d < 0f) continue;

            Vector3 halfExtents;
            if (cam.orthographic)
            {
                float halfH = cam.orthographicSize * heightViewport;
                float halfW = cam.orthographicSize * cam.aspect * widthViewport;
                halfExtents = new Vector3(halfW, halfH, slabDepth);
            }
            else
            {
                float halfHFull = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * d;
                float halfWFull = halfHFull * cam.aspect;
                float halfH = halfHFull * heightViewport;
                float halfW = halfWFull * widthViewport;
                halfExtents = new Vector3(halfW, halfH, slabDepth);
            }

            int count = Physics.OverlapBoxNonAlloc(pos, halfExtents, _overlapBuf, boxRotation, mask, QueryTriggerInteraction.Ignore);
            for (int c = 0; c < count; c++)
            {
                var col = _overlapBuf[c];
                if (!col) continue;
                if (col.TryGetComponent(out FadingObject fo))
                    hitSet.Add(fo);
            }
        }
    }

    // ---------------------------- Fade State Management ----------------------------

    /// <summary>Ensure the object begins fading OUT (to transparent).</summary>
    private void EnsureFadeOut(FadingObject fo)
    {
        if (fo == null) return;

        if (_blockingSet.Add(fo))
            ObjectsBlockingView.Add(fo); // keep inspector in sync

        if (RunningCoroutines.TryGetValue(fo, out var existing) && existing != null)
            StopCoroutine(existing);

        RunningCoroutines[fo] = StartCoroutine(FadeObjectOut(fo));
    }

    /// <summary>Fade back any object not hit this frame.</summary>
    private void FadeObjectsNoLongerHit(HashSet<FadingObject> frameHits)
    {
        if (_blockingSet.Count == 0) return;

        // gather to remove
        var toRemove = new List<FadingObject>();

        foreach (var fo in _blockingSet)
        {
            if (fo == null || !frameHits.Contains(fo))
            {
                if (fo != null)
                {
                    if (RunningCoroutines.TryGetValue(fo, out var existing) && existing != null)
                        StopCoroutine(existing);

                    RunningCoroutines[fo] = StartCoroutine(FadeObjectIn(fo));
                }
                toRemove.Add(fo);
            }
        }

        // remove from tracking
        foreach (var fo in toRemove)
        {
            _blockingSet.Remove(fo);
            ObjectsBlockingView.Remove(fo);
        }
    }

    private IEnumerator FadeObjectOut(FadingObject fo)
    {
        if (fo == null || fo.Materials == null || fo.Materials.Count == 0)
        {
            yield break;
        }

        foreach (Material m in fo.Materials)
            SetTransparent(m, RetainShadows);

        float current = fo.Materials[0].color.a; // read directly; Unity maps to shader's color
        while (current > FadedAlpha)
        {
            current = Mathf.MoveTowards(current, FadedAlpha, Mathf.Max(0f, FadeSpeed) * Time.deltaTime);
            foreach (Material m in fo.Materials) SetAlpha(m, current);
            yield return null;
        }

        RunningCoroutines.Remove(fo);
    }

    private IEnumerator FadeObjectIn(FadingObject fo)
    {
        if (fo == null || fo.Materials == null || fo.Materials.Count == 0)
        {
            yield break;
        }

        float current = fo.Materials[0].color.a;
        while (current < fo.InitialAlpha)
        {
            current = Mathf.MoveTowards(current, fo.InitialAlpha, Mathf.Max(0f, FadeSpeed) * Time.deltaTime);
            foreach (Material m in fo.Materials) SetAlpha(m, current);
            yield return null;
        }

        foreach (Material m in fo.Materials)
            SetOpaque(m);

        RunningCoroutines.Remove(fo);
    }

    private FadingObject GetFadingObjectFromHit(RaycastHit hit)
    {
        return hit.collider ? hit.collider.GetComponent<FadingObject>() : null;
    }

    // ---------------------------- Material helpers ----------------------------

    private static void SetTransparent(Material m, bool keepShadows)
    {
        m.SetInt(_SrcBlend, (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt(_DstBlend, (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt(_ZWrite, 0);
        m.SetInt(_Surface, 1);

        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        m.SetShaderPassEnabled("DepthOnly", false);
        m.SetShaderPassEnabled("SHADOWCASTER", keepShadows);

        m.SetOverrideTag("RenderType", "Transparent");

        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
    }

    private static void SetOpaque(Material m)
    {
        m.SetInt(_SrcBlend, (int)UnityEngine.Rendering.BlendMode.One);
        m.SetInt(_DstBlend, (int)UnityEngine.Rendering.BlendMode.Zero);
        m.SetInt(_ZWrite, 1);
        m.SetInt(_Surface, 0);

        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;

        m.SetShaderPassEnabled("DepthOnly", true);
        m.SetShaderPassEnabled("SHADOWCASTER", true);

        m.SetOverrideTag("RenderType", "Opaque");

        m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
    }

    private static void SetAlpha(Material m, float a)
    {
        // Using Material.color ensures correct mapping to _BaseColor/_Color internally.
        var c = m.color;
        c.a = a;
        m.color = c;
    }

    // ---------------------------- Utilities ----------------------------

    private Vector3 GetLeadDirection()
    {
        if (!UseVelocityForLead || Target == null) return Target ? Target.forward : Vector3.forward;

        // Try Rigidbody
        if (Target.TryGetComponent<Rigidbody>(out var rb))
        {
            Vector3 v = rb.linearVelocity;
            v.y = 0f;
            if (v.sqrMagnitude > VelocityThreshold * VelocityThreshold)
                return v.normalized;
        }

        // Try CharacterController (approximate)
        if (Target.TryGetComponent<CharacterController>(out var cc))
        {
            Vector3 v = cc.velocity;
            v.y = 0f;
            if (v.sqrMagnitude > VelocityThreshold * VelocityThreshold)
                return v.normalized;
        }

        // Fallback to forward
        return Target.forward;
    }

#if UNITY_EDITOR
    // Optional gizmo to help tune the corridor (draws at runtime only in editor)
    private void OnDrawGizmosSelected()
    {
        if (!Camera || !Target || !UseFrustumCorridor) return;

        Vector3 camPos = Camera.transform.position;
        Vector3 tgtBase = Target.transform.position + TargetPositionOffset;
        Vector3 leadDir = GetLeadDirection();
        Vector3 leadPoint = tgtBase + (leadDir * LeadDistance) + (Vector3.up * LeadHeight);

        // mirror the computation from CastFrustumCorridor for visualization
        Quaternion rot = Camera.transform.rotation;
        int steps = Mathf.Max(2, CorridorSteps);
        float pathLen = Vector3.Distance(camPos, leadPoint);
        float slabDepth = Mathf.Max(0.05f, pathLen / steps) * 0.6f;

        Gizmos.color = new Color(0f, 1f, 1f, 0.25f);
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            Vector3 pos = Vector3.Lerp(camPos, leadPoint, t);
            float d = Vector3.Dot(pos - Camera.transform.position, Camera.transform.forward);
            if (d < 0f) continue;

            Vector3 halfExtents;
            if (Camera.orthographic)
            {
                float halfH = Camera.orthographicSize * CorridorHeightViewport;
                float halfW = Camera.orthographicSize * Camera.aspect * CorridorWidthViewport;
                halfExtents = new Vector3(halfW, halfH, slabDepth);
            }
            else
            {
                float halfHFull = Mathf.Tan(Camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * d;
                float halfWFull = halfHFull * Camera.aspect;
                float halfH = halfHFull * CorridorHeightViewport;
                float halfW = halfWFull * CorridorWidthViewport;
                halfExtents = new Vector3(halfW, halfH, slabDepth);
            }

            Matrix4x4 m = Matrix4x4.TRS(pos, rot, Vector3.one);
            Gizmos.matrix = m;
            Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);
        }
        Gizmos.matrix = Matrix4x4.identity;
    }
#endif
}
