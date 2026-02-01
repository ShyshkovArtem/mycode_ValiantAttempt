using UnityEngine;
using RPG.Equipment; // for UIEvents

public class ResourceSystem : MonoBehaviour, IDamageable
{
    private Character user;
    public int currentHealth;

    [SerializeField] private GameObject healthBar;
    [SerializeField] private bool debugMode;
    [SerializeField] bool isDead = false;

    // ===== Policy for reacting to max HP changes (from equipment, buffs, etc.) =====
    public enum MaxHealthChangePolicy
    {
        KeepAbsoluteClampDown, // keep current; if above new max, clamp down
        KeepPercentage,        // keep the same % of max (uses previous max for correct math)
        SetToMaxOnIncrease     // if max increases -> heal to full; if decreases -> clamp
    }

    [Header("Max Health Change")]
    [SerializeField] private MaxHealthChangePolicy onMaxHealthChanged = MaxHealthChangePolicy.KeepAbsoluteClampDown;

    // Cache the previous max so % math is correct
    private int _prevMaxHealth = 0;

    // Dynamic Max: prefer combined (affected by equipment), fallback to base
    public int MaxHealth
    {
        get
        {
            if (user == null) return 1;
            int combined = user.combinedHealth;
            int baseMax = (user.characterData != null) ? user.characterData.baseHealth : 1;
            return Mathf.Max(1, combined > 0 ? combined : baseMax);
        }
    }

    // ------------------------------------------------------------------------

    private void Awake()
    {
        user = GetComponent<Character>();

        if (!healthBar)
        {
            var hb = GetComponentInChildren<HealthBar>();
            if (hb) healthBar = hb.gameObject;
        }

        currentHealth = MaxHealth;
        _prevMaxHealth = MaxHealth; // seed cache
    }

    private void OnEnable()
    {
        UIEvents.EquipmentChanged += HandleMaxHealthMaybeChanged;
    }

    private void OnDisable()
    {
        UIEvents.EquipmentChanged -= HandleMaxHealthMaybeChanged;
    }

    // ------------------------------------------------------------------------

    public void ApplyHealthChange(Character source, int amount, SkillType skillType)
    {
        int trueAmount = CalculateSkillPower(source, amount, skillType);

        switch (skillType)
        {
            case SkillType.OffensiveSkill: CalculateDamage(source, trueAmount); break;
            case SkillType.SupportiveSkill: CalculateHealing(trueAmount); break;
            default:
                if (debugMode) Debug.Log($"{name} received unsupported SkillType: {skillType}");
                break;
        }

        user.characterEventBusHandler.TriggerHealthChanged(currentHealth);
        user.characterEventBusHandler.TriggerHealthChangePercentage(GetHealthPercentage());

        if (debugMode) Debug.Log($"{name} applied {skillType}: {amount}, new HP: {currentHealth}/{MaxHealth}");
    }
    int CalculateSkillPower(Character source, int amount, SkillType skillType)
    {
        int trueAmount = 0;

        switch (skillType)
        {
            case SkillType.OffensiveSkill:
                trueAmount = amount + source.combinedStrength;
                break;
            case SkillType.SupportiveSkill:
                trueAmount = amount + source.combinedIntelligence;
                break;
            default:
                if (debugMode) Debug.LogWarning($"{name} received unsupported SkillType for calculation: {skillType}");
                break;
        }
        return trueAmount;
    }

    private void CalculateDamage(Character source, int amount)
    {
        if (debugMode) Debug.Log($"{name} taking damage: {amount}");

        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, MaxHealth);

        user.characterEventBusHandler.TriggerDamageTaken(amount);

        if (currentHealth <= 0)
        {
            if (debugMode) Debug.Log($"{name} has been destroyed.");
            user.characterEventBusHandler.TriggerDeathEvent();
            OnDeath(source);
        }
    }

    public void CalculateHealing(int amount)
    {
        if (debugMode) Debug.Log($"{name} healing: {amount}");

        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, MaxHealth);

        user.characterEventBusHandler.TriggerHealingTaken(amount);
    }

    public void OnRevive()
    {
        if (!isDead) return;

        currentHealth = 1; // or MaxHealth, your call
        if (healthBar) healthBar.SetActive(true);

        CharacterListManager.AddCharacterToList(user);

        if (GameplaySystemsManager.Instance.gameMode == GameMode.Use3D)
        {
            user.navMeshAgent.enabled = true;
            user.characterHitCollider3D.enabled = true;
        }
        else
        {
            user.navMeshAgent.enabled = true;
            user.characterHitCollider2D.enabled = true;
            GetComponent<Renderer>().sortingLayerName = "Characters";
        }

        isDead = false;
        if (debugMode) Debug.Log($"{name} has been revived!");
    }

    private void OnDeath(Character source)
    {
        if (isDead) return;
        isDead = true;

        if (debugMode) Debug.Log($"{name} defeated!");

        CharacterListManager.RemoveCharacterFromList(user);
        if (debugMode) Debug.Log($"{name} removed from CharacterListManager. Total: {CharacterListManager.GetCharacterCount()}");

        user.characterController.enabled = false;
        if (healthBar) healthBar.SetActive(false);
        user.navMeshAgent.enabled = false;

        if (GameplaySystemsManager.Instance.gameMode == GameMode.Use3D)
        {
            var anim = GetComponent<CharacterAnimationController>();
            anim.ragdollHandler.SetRagdollActive(true);

            if (source != null)
            {
                const float force = 10f;
                Vector3 dir = (user.transform.position - source.transform.position);
                dir.y = 0f; dir.Normalize();
                foreach (var rb in anim.ragdollHandler.ragdollBoneRigidbodies)
                    rb.AddForce(dir * force, ForceMode.Impulse);
            }

            user.characterHitCollider3D.enabled = false;
        }
        else
        {
            user.characterHitCollider2D.enabled = false;
            GetComponent<Renderer>().sortingLayerName = "Ground";
        }
    }

    // ------------------------------------------------------------------------

    public float GetHealthPercentage()
    {
        int max = MaxHealth;
        return max > 0 ? (float)currentHealth / max : 0f;
    }

    public int GetHealthPercentageInt() => Mathf.RoundToInt(GetHealthPercentage() * 100f);

    public bool IsDead() => isDead;

    // === React to equipment-driven max HP changes ============================

    private void HandleMaxHealthMaybeChanged()
    {
        int newMax = Mathf.Max(1, MaxHealth);
        int prevMax = (_prevMaxHealth > 0) ? _prevMaxHealth : newMax;

        switch (onMaxHealthChanged)
        {
            case MaxHealthChangePolicy.KeepAbsoluteClampDown:
                currentHealth = Mathf.Clamp(currentHealth, 0, newMax);
                break;

            case MaxHealthChangePolicy.KeepPercentage:
                {
                    // Keep the same ratio using the *previous* max
                    float prevPercent = prevMax > 0 ? (float)currentHealth / prevMax : 1f;
                    currentHealth = Mathf.RoundToInt(prevPercent * newMax);
                    currentHealth = Mathf.Clamp(currentHealth, 0, newMax);
                    break;
                }

            case MaxHealthChangePolicy.SetToMaxOnIncrease:
                {
                    if (newMax > prevMax) currentHealth = newMax; // heal to full on increase
                    else currentHealth = Mathf.Clamp(currentHealth, 0, newMax);
                    break;
                }
        }

        // Notify listeners/UI
        user.characterEventBusHandler.TriggerHealthChanged(currentHealth);
        user.characterEventBusHandler.TriggerHealthChangePercentage(GetHealthPercentage());

        // Update cache AFTER applying
        _prevMaxHealth = newMax;

        if (debugMode)
            Debug.Log($"[ResourceSystem] Max change ? prev:{prevMax} new:{newMax} cur:{currentHealth} policy:{onMaxHealthChanged}");
    }
}
