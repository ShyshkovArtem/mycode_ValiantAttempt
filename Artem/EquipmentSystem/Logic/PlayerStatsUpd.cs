using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bridges equipment and Character's combined stats.
/// - Base stats come from Character.characterData (base*).
/// - Current stats are the Character.combined* fields.
/// - You can preview equipment or apply it to the character.

public class PlayerStatsUpd : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private Character playerCharacter;        // The player Character
    [SerializeField] private bool autoFindCharacter = true;

    // ---- Public API ---------------------------------------------------------

    //Base stats (from CharacterDataSO, without equipment/buffs)
    public Dictionary<StatType, int> Base => ReadBaseFromCharacterData();

    //Current live stats from Character.combined
    public Dictionary<StatType, int> Current => ReadCombinedFromCharacter();


    /// Calculates totals as if these equipped items were applied
    /// (starting from base stats, ignoring live combined values).
    public Dictionary<StatType, int> CalculateWith(Dictionary<EquipmentSlot, EquipmentItem> equipped)
    {
        var totals = new Dictionary<StatType, int>(Base);
        // here should be logic for changing stats (apply equipment/buffs/etc.)
        ApplyEquipmentSet(totals, equipped);
        return totals;
    }

    /// Previews totals if `candidate` were equipped (replacing what’s in the same slot).
    public Dictionary<StatType, int> PreviewWith(EquipmentItem candidate)
    {
        // Start from current equipment -> recompute from BASE to avoid drift
        var equipped = new Dictionary<EquipmentSlot, EquipmentItem>(EquipmentManager.Instance.Equipped);

        if (candidate != null)
            equipped[candidate.Slot] = candidate; // replace same-slot item

        return CalculateWith(equipped);
    }

    /// Recalculates from BASE + currently equipped and writes the result
    /// into Character.combined* (authoritative live values).
    /// Call this after Equip/Unequip.
    public void ApplyEquipmentToCharacter()
    {
        if (playerCharacter == null) { Debug.LogWarning("[PlayerStatsUpd] Character missing."); return; }
        if (EquipmentManager.Instance == null || EquipmentManager.Instance.Equipped == null)
        {
            // Will be called again when EM announces Ready
            Debug.LogWarning("[PlayerStatsUpd] EquipmentManager not ready yet.");
            return;
        }

        // compute from base + equipped (use your CalculateWith implementation)
        var totals = CalculateWith(EquipmentManager.Instance.Equipped);
        WriteTotalsToCharacter(totals);

        if (playerCharacter.navMeshAgent != null)
        {
            playerCharacter.navMeshAgent.speed = playerCharacter.combinedMovementSpeed;
            playerCharacter.navMeshAgent.acceleration = playerCharacter.combinedMovementSpeed * 2;
        }

        // Only raise if EquipmentManager does NOT already raise this after equip/unequip
        // UIEvents.RaiseEquipmentChanged();
    }

    // ---- Unity lifecycle ----------------------------------------------------

    void Awake()
    {
        if (!playerCharacter && autoFindCharacter)
            playerCharacter = FindFirstObjectByType<PlayerController>().GetComponent<Character>();
    }

    void OnEnable()
    {
        // When equipment changes (via UI), recompute & apply to Character.
        UIEvents.EquipmentChanged += ApplyEquipmentToCharacter;
        // Listen to EquipmentManager readiness
        EquipmentManager.Ready += ApplyEquipmentToCharacter;

        // Also try now (in case EM is already alive)
        StartCoroutine(WaitAndApplyOnce());
    }

    void OnDisable()
    {
        UIEvents.EquipmentChanged -= ApplyEquipmentToCharacter;
        EquipmentManager.Ready -= ApplyEquipmentToCharacter;
    }

    private IEnumerator WaitAndApplyOnce()
    {
        // Wait up to 2 seconds for EquipmentManager to appear
        float timeout = 2f;
        while ((EquipmentManager.Instance == null || EquipmentManager.Instance.Equipped == null) && timeout > 0f)
        {
            yield return null;
            timeout -= Time.unscaledDeltaTime;
        }

        if (EquipmentManager.Instance != null && EquipmentManager.Instance.Equipped != null)
            ApplyEquipmentToCharacter();
        else
            Debug.LogError("[PlayerStatsUpd] EquipmentManager still not ready after timeout.");
    }

    // ---- Internals ----------------------------------------------------------

    bool EnsureCharacter()
    {
        if (playerCharacter != null) return true;
        Debug.LogWarning("[PlayerStats] Character reference is missing.");
        return false;
    }

    Dictionary<StatType, int> ReadBaseFromCharacterData()
    {
        EnsureCharacter();
        var data = playerCharacter != null ? playerCharacter.characterData : null;
        if (data == null)
        {
            // Fallback to zero base if data is missing
            return new Dictionary<StatType, int>
            {
                { StatType.HP, 0 }, { StatType.MP, 0 },
                { StatType.HPReg, 0 }, { StatType.MPReg, 0 },
                { StatType.STR, 0 }, { StatType.VIT, 0 },
                { StatType.INT, 0 }, { StatType.MOV, 0 }
            };
        }

        return new Dictionary<StatType, int>
        {
            { StatType.HP,         data.baseHealth },
            { StatType.MP,           data.baseMana },
            { StatType.HPReg,    data.baseHealthRegen },
            { StatType.MPReg,      data.baseManaRegen },
            { StatType.STR,       data.baseStrength },
            { StatType.VIT,       data.baseVitality },
            { StatType.INT,   data.baseIntelligence },
            { StatType.MOV,  data.baseMovementSpeed },
        };
    }

    Dictionary<StatType, int> ReadCombinedFromCharacter()
    {
        EnsureCharacter();
        if (playerCharacter == null)
            return new Dictionary<StatType, int>();

        return new Dictionary<StatType, int>
        {
            { StatType.HP,         playerCharacter.combinedHealth },
            { StatType.MP,           playerCharacter.combinedMana },
            { StatType.HPReg,    playerCharacter.combinedHealthRegen },
            { StatType.MPReg,      playerCharacter.combinedManaRegen },
            { StatType.STR,       playerCharacter.combinedStrength },
            { StatType.VIT,       playerCharacter.combinedVitality },
            { StatType.INT,   playerCharacter.combinedIntelligence },
            { StatType.MOV,  playerCharacter.combinedMovementSpeed },
        };
    }

    void ApplyEquipmentSet(Dictionary<StatType, int> totals, Dictionary<EquipmentSlot, EquipmentItem> equipped)
    {
        if (equipped == null) return;
        foreach (var kv in equipped)
        {
            var item = kv.Value;
            if (item == null || item.Modifiers == null) continue;

            foreach (var mod in item.Modifiers)
            {
                if (!totals.ContainsKey(mod.Type)) totals[mod.Type] = 0;
                totals[mod.Type] += mod.Value;
            }
        }

        // Extend here with buffs/debuffs, percent modifiers, caps, etc.
        // e.g., totals[StatType.Health] = ApplyPercent(totals[StatType.Health], bonusPercent);
        // --> // here should be logic for changing stats
    }

    void WriteTotalsToCharacter(Dictionary<StatType, int> totals)
    {
        if (!EnsureCharacter()) return;

        int Get(StatType t) => totals.TryGetValue(t, out var v) ? v : 0;

        playerCharacter.combinedHealth = Get(StatType.HP);
        playerCharacter.combinedMana = Get(StatType.MP);
        playerCharacter.combinedHealthRegen = Get(StatType.HPReg);
        playerCharacter.combinedManaRegen = Get(StatType.MPReg);
        playerCharacter.combinedStrength = Get(StatType.STR);
        playerCharacter.combinedVitality = Get(StatType.VIT);
        playerCharacter.combinedIntelligence = Get(StatType.INT);
        playerCharacter.combinedMovementSpeed = Get(StatType.MOV);
    }
}
