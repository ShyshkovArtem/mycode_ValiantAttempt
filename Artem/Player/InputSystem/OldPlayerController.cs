using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public class OldPlayerController : Character
{
    #region Types

    [Serializable]
    public struct Mapping
    {
        public bool swapAxes;
        public bool invertX;
        public bool invertY;
    }

    public enum SlotTargetMode { EnemyClosest, Self, AllyClosest }

    private enum MoveMode { Iso, ThirdPerson }

    #endregion

    #region Config - Movement

    [Header("Movement")]
    [Tooltip("Ground-plane movement speed (units/sec).")]
    [SerializeField] private float speed = 5f;

    [Tooltip("Jump apex height (meters).")]
    [SerializeField] private float jumpHeight = 2f;

    [Tooltip("Gravity applied per second (negative = down).")]
    [SerializeField] private float gravity = -9.8f;

    [Header("Camera")]
    [SerializeField] private Camera mainCamera;

    [Header("Mappings")]
    public Mapping isoMapping;
    public Mapping thirdPersonMapping;

    [Header("Model Rotation")]
    [Tooltip("Optional visual child transform to rotate. Defaults to this transform.")]
    [SerializeField] private Transform modelRoot;

    [Tooltip("How quickly the model turns toward the move direction.")]
    [SerializeField] private float turnSpeed = 10f;

    [Header("Iso Tuning")]
    [Tooltip("Aligns iso movement with on-screen axes using ground-plane sampling.")]
    [SerializeField] private bool useRaycastAlignedIso = true;

    [Tooltip("Viewport step for iso sampling.")]
    [SerializeField, Range(0.001f, 0.05f)] private float isoViewportStep = 0.01f;

    [Header("Mode")]
    [SerializeField] private MoveMode activeMode = MoveMode.ThirdPerson;

    #endregion

    #region Config - Abilities / Hotbar

    [Header("Abilities / Hotbar")]
    [Tooltip("Skill slots used by Ability1..4 input callbacks.")]
    private SkillDataSO[] slots = Array.Empty<SkillDataSO>();

    [Tooltip("Targeting mode for each slot.")]
    [SerializeField]
    private SlotTargetMode[] slotModes =
    {
        SlotTargetMode.EnemyClosest,
        SlotTargetMode.EnemyClosest,
        SlotTargetMode.EnemyClosest,
        SlotTargetMode.EnemyClosest
    };

    [Tooltip("If true, releasing a held key cancels the in-progress cast for that slot.")]
    [SerializeField] private bool releaseCancelsCasting = true;

    #endregion

    #region State

    //private CharacterController characterController;
    private Vector2 moveInput;
    private Vector3 velocity;

    // Tracks whether each slot key is currently held
    private readonly bool[] slotHeld = new bool[4];

    // Lazy cache of SkillSystem
    private SkillSystem _ss;
    private SkillSystem SS => _ss ? _ss : (_ss = (skillSystem ? skillSystem : GetComponent<SkillSystem>()));

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        if (!mainCamera) mainCamera = Camera.main;
        if (!modelRoot) modelRoot = transform; // safe fallback
    }

    float lastTime = 0f; // Sami's code for footsteps (Remove later when SFXHandler is properly implemented.)

    private void Update()
    {
        UpdateCastingHold();
        UpdateMovement();

        // Quick hack to get footsteps working with CharacterController. (Sami Code stars here)
        //Debug.Log("MoveInput: " + moveInput);
        if (moveInput == Vector2.zero && audioSource.isPlaying)
        {
            audioSource.Stop();
            //Debug.Log("AudioSource stopped.");
        }

        if (moveInput != Vector2.zero)
        {
            //Debug.Log("Player is moving.");
            //TestingAudioPlayer.Instance.PlayFootsteps(this, characterData.characterSFXData.footsteps.oneShotSfxAudio[0]);
            //Debug.Log("AudioSource is playing.");

            //Debug.Log("PlayFootsteps is working.");

            //Debug.Log("AudioSource is playing clip.");
            float currentTime = audioSource.time;

            // Detect reset from end → start (loop)
            if (currentTime < lastTime)
            {
                //characterSFXHandler.PlayAudioClip(characterData.characterSFXData.footsteps);
            }
            //Debug.Log("Everything is working fine.");
            lastTime = currentTime;
        }
        // Sami's code ends here. (Remove this after SFXHandler is properly implemented.)
    }


    #endregion

    #region Input Callbacks (wired via PlayerInput)

    public void Ability1(InputAction.CallbackContext ctx) => HandleAbilityInput(0, ctx);
    public void Ability2(InputAction.CallbackContext ctx) => HandleAbilityInput(1, ctx);
    public void Ability3(InputAction.CallbackContext ctx) => HandleAbilityInput(2, ctx);
    public void Ability4(InputAction.CallbackContext ctx) => HandleAbilityInput(3, ctx);

    public void Move(InputAction.CallbackContext ctx) => moveInput = ctx.ReadValue<Vector2>();

    public void Jump(InputAction.CallbackContext ctx)
    {
        if (ctx.performed && characterController.isGrounded)
            velocity.y = Mathf.Sqrt(jumpHeight * gravity * -2f);
    }

    #endregion

    #region Abilities

    private void HandleAbilityInput(int index, InputAction.CallbackContext ctx)
    {
        var data = GetSlotSkill(index); // may be null

        if (ctx.started)
        {
            slotHeld[index] = true;
            //characterEventHandler?.TriggerSkillSlotStarted(index, data);
        }

        if (ctx.performed)
        {
            //characterEventHandler?.TriggerSkillSlotPerformed(index, data);

            // Instant skills: fire once on tap
            if (data && data.skillCastTime <= 0f)
                UseSlot(index);
            // Cast-time skills are advanced while held (UpdateCastingHold)
        }

        if (ctx.canceled)
        {
            slotHeld[index] = false;

            // Optional release-to-cancel
            if (releaseCancelsCasting && data && data.skillCastTime > 0f)
            {
                var handler = skillSystem ? skillSystem.SkillHandler : null;
                if (handler != null && handler.castingHandler.isCasting)
                {
                    // cancel only if we were casting THIS slot's skill (safest)
                    if (handler.castingHandler.CastingSkill == data)
                        handler.castingHandler.Interrupt();
                }
            }

            //characterEventHandler?.TriggerSkillSlotCanceled(index, data);
        }
    }

    private void SyncSlotsFromCharacterData()
    {
        var list = SkillData; // from base: CharacterData.skillData
        if (list == null || list.Count == 0)
        {
            slots = Array.Empty<SkillDataSO>();
            slotModes = Array.Empty<SlotTargetMode>();
            if (controllerDebugMode) Debug.LogWarning($"{name}: CharacterData has no skills.");
            return;
        }

        int count = list.Count;
        if (slots.Length != count) slots = new SkillDataSO[count];
        if (slotModes.Length != count) slotModes = new SlotTargetMode[count];

        for (int i = 0; i < count; i++)
        {
            slots[i] = list[i];

            // Default targeting; customize if your SkillDataSO exposes hints.
            // slotModes[i] = SlotTargetMode.EnemyClosest;
            // Example (pseudo):
            // if (list[i].TargetsSelf) slotModes[i] = SlotTargetMode.Self;
            // else if (list[i].TargetsAllies) slotModes[i] = SlotTargetMode.AllyClosest;
        }

        if (controllerDebugMode) Debug.Log($"{name}: Hotbar synced from CharacterData ({count} skills).");
    }

    public void TriggerSlot(int index) => UseSlot(index);
    /*
    public void SetSlot(int index, SkillDataSO data, SlotTargetMode mode = SlotTargetMode.EnemyClosest)
    {
        if (index < 0) return;

        if (index >= slots.Length) Array.Resize(ref slots, index + 1);
        slots[index] = data;

        if (index >= slotModes.Length) Array.Resize(ref slotModes, index + 1);
        slotModes[index] = mode;
    }*/

    private void UpdateCastingHold()
    {
        // While a key is held, repeatedly try cast-time skills so the existing SkillHandler
        // can advance its casting loop without modifying that code.
        for (int i = 0; i < slots.Length; i++)
        {
            if (!slotHeld[i]) continue;
            var data = GetSlotSkill(i);
            if (!data || data.skillCastTime <= 0f) continue;

            UseSlot(i);
        }
    }

    private void UseSlot(int index)
    {
        var target = ResolveTarget(index);
        //SS.TryToUseSkill(target); // SkillSystem handles null/range/cooldown
    }

    private Character ResolveTarget(int index)
    {
        var mode = GetSlotMode(index);
        switch (mode)
        {
            case SlotTargetMode.Self:
                return this;

            case SlotTargetMode.AllyClosest:
                return GetClosestFromList(objectDetectionSystem?.GetAllies(), excludeSelf: true);

            case SlotTargetMode.EnemyClosest:
            default:
                return GetClosestFromList(objectDetectionSystem?.GetEnemies(), excludeSelf: false);
        }
    }

    private Character GetClosestFromList(System.Collections.Generic.List<Character> list, bool excludeSelf)
    {
        if (list == null || list.Count == 0) return null;

        Character best = null;
        float bestSqr = float.MaxValue;
        Vector3 pos = transform.position;

        foreach (var bc in list)
        {
            if (!bc) continue;
            if (excludeSelf && bc == this) continue;

            var hs = bc.resourceSystem;
            if (hs != null && hs.IsDead()) continue;

            float sqr = (bc.transform.position - pos).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = bc;
            }
        }
        return best;
    }

    private SkillDataSO GetSlotSkill(int index) =>
        (index >= 0 && index < slots.Length) ? slots[index] : null;

    private SlotTargetMode GetSlotMode(int index) =>
        (index >= 0 && index < slotModes.Length) ? slotModes[index] : SlotTargetMode.EnemyClosest;

    #endregion

    #region Movement

    private void UpdateMovement()
    {
        // 1) Build basis vectors aligned to camera or iso screen axes
        ComputeBasis(out Vector3 right, out Vector3 upGround);

        // 2) Apply mapping
        Mapping m = (activeMode == MoveMode.Iso) ? isoMapping : thirdPersonMapping;
        float x = moveInput.x, y = moveInput.y;
        if (m.swapAxes) { (x, y) = (y, x); }
        if (m.invertX) x = -x;
        if (m.invertY) y = -y;

        // 3) Ground-plane motion
        Vector3 move = upGround * y + right * x;
        if (move.sqrMagnitude > 1f) move.Normalize();

        characterController.Move(move * speed * Time.deltaTime);

        // 4) Gravity
        if (characterController.isGrounded && velocity.y < 0f) velocity.y = -2f;
        velocity.y += gravity * Time.deltaTime;
        characterController.Move(velocity * Time.deltaTime);

        // 5) Rotate visual
        RotateModel(move);
    }

    private void ComputeBasis(out Vector3 right, out Vector3 upGround)
    {
        Transform cam = mainCamera ? mainCamera.transform : transform;

        if (activeMode == MoveMode.ThirdPerson)
        {
            Vector3 fwd = cam.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-6f) fwd = transform.forward;
            fwd.Normalize();

            right = Vector3.Cross(Vector3.up, fwd).normalized;
            upGround = fwd;
            return;
        }

        // Iso mode
        if (useRaycastAlignedIso && mainCamera)
        {
            float groundY = transform.position.y;
            Plane ground = new Plane(Vector3.up, new Vector3(0f, groundY, 0f));

            Vector3 pC = GroundHitFromViewport(0.5f, 0.5f, ground, mainCamera, transform.position);
            float step = isoViewportStep;
            Vector3 pR = GroundHitFromViewport(0.5f + step, 0.5f, ground, mainCamera, pC + mainCamera.transform.right);
            Vector3 pU = GroundHitFromViewport(0.5f, 0.5f + step, ground, mainCamera, pC + mainCamera.transform.up);

            Vector3 rightRaw = pR - pC;
            Vector3 upRaw = pU - pC;

            right = Vector3.ProjectOnPlane(rightRaw, Vector3.up);
            if (right.sqrMagnitude < 1e-6f) right = Vector3.right;
            right.Normalize();

            Vector3 upProj = Vector3.ProjectOnPlane(upRaw, Vector3.up);
            upProj -= Vector3.Project(upProj, right);
            if (upProj.sqrMagnitude < 1e-6f) upProj = Vector3.Cross(Vector3.up, right);
            upProj.Normalize();
            if (Vector3.Dot(upProj, upRaw) < 0f) upProj = -upProj;

            upGround = upProj;
            return;
        }

        // Simple fallback iso: camera.right projected + orthogonal up
        right = Vector3.ProjectOnPlane(cam.right, Vector3.up);
        if (right.sqrMagnitude < 1e-6f) right = Vector3.right;
        right.Normalize();

        upGround = Vector3.Cross(Vector3.up, right);
        if (upGround.sqrMagnitude < 1e-6f) upGround = Vector3.forward;
        upGround.Normalize();

        Vector3 camUpFlat = Vector3.ProjectOnPlane(cam.up, Vector3.up).normalized;
        if (Vector3.Dot(upGround, camUpFlat) < 0f) upGround = -upGround;
    }

    public void SetIsoMapping(bool useIso)
    {
#if true
        activeMode = useIso ? MoveMode.Iso : MoveMode.ThirdPerson;
#else
    activeMode = useIso ? MoveMode.Iso : MoveMode.ThirdPerson;
#endif
    }

    private void RotateModel(in Vector3 move)
    {
        if (!modelRoot || move.sqrMagnitude <= 0.0001f) return;

        Vector3 lookDir = new Vector3(move.x, 0f, move.z);
        if (lookDir.sqrMagnitude <= 0.0001f) return;

        Quaternion target = Quaternion.LookRotation(lookDir, Vector3.up);
        modelRoot.rotation = Quaternion.Slerp(modelRoot.rotation, target, turnSpeed * Time.deltaTime);
    }

    private static Vector3 GroundHitFromViewport(float vx, float vy, Plane ground, Camera cam, Vector3 fallback)
    {
        Ray r = cam.ViewportPointToRay(new Vector3(vx, vy, 0f));
        return ground.Raycast(r, out float dist) ? r.GetPoint(dist) : fallback;
    }

    #endregion
}
