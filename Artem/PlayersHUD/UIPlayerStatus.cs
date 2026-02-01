using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.Equipment
{
    /// <summary>
    /// Player HUD for Health, Mana and Player Icon.
    /// - Current Health from ResourceSystem.currentHealth
    /// - Max Health from Character.combinedHealth (fallback: characterData.baseHealth)
    /// - Mana uses Character.combinedMana for now (until you add a mana system)
    /// - Smooth slider animation (unscaled) and gradient coloring
    /// - Auto refreshes max values on equipment changes
    /// </summary>
    public class UIPlayerStatus : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Character playerCharacter;           // Player Character
        [SerializeField] private ResourceSystem resourceSystem; // ResourceSystem
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Slider manaSlider;
        [SerializeField] private Image playerIcon;              // Optional portrait

        [Header("Optional Texts")]
        [SerializeField] private TMP_Text healthText;           // "cur / max"
        [SerializeField] private TMP_Text manaText;             // "cur / max"
        [SerializeField] private TMP_Text nameText;             // character name

        [Header("Fill Images (optional)")]
        [SerializeField] private Image healthFill;              // assign the Slider's Fill image
        [SerializeField] private Image manaFill;

        [Header("Gradients (optional)")]
        [SerializeField] private Gradient healthGradient;       // left=low, right=full
        [SerializeField] private Gradient manaGradient;

        [Header("Animation")]
        [SerializeField, Range(0f, 20f)] private float smoothSpeed = 10f; // higher = snappier

        // Internals
        private int _maxHealth;
        private int _maxMana;
        private float _displayHealth;
        private float _displayMana;

        void Awake()
        {
            // Attempt auto-wiring if fields not set
            if (!playerCharacter) playerCharacter = FindFirstObjectByType<PlayerController>().GetComponent<Character>();
            if (!resourceSystem) resourceSystem = FindFirstObjectByType<PlayerController>().GetComponent<ResourceSystem>();

            // Initial max setup
            RefreshMaxFromCharacter();
            SnapToCurrent();
        }

        void OnEnable()
        {
            // Update max values when equipment changes (gear changes combined stats)
            UIEvents.EquipmentChanged += OnEquipmentChanged;

            // Initial repaint (in case UI was enabled after systems)
            RefreshMaxFromCharacter();
            SnapToCurrent();
            ApplyOnce(); // write initial values to UI
        }

        void OnDisable()
        {
            UIEvents.EquipmentChanged -= OnEquipmentChanged;
        }

        void Update()
        {
            // Pull live currents
            int curHp = GetCurrentHealth();
            int curMp = GetCurrentMana(); // placeholder until you add a mana resource

            // Smooth towards target using unscaledDeltaTime (works when game paused by timescale)
            float k = 1f - Mathf.Exp(-smoothSpeed * Time.unscaledDeltaTime);
            _displayHealth = Mathf.Lerp(_displayHealth, curHp, k);
            _displayMana = Mathf.Lerp(_displayMana, curMp, k);

            ApplyOnce();
        }

        // --- Public helpers ------------------------------------------------------

        //Set the portrait/icon sprite (optional)
        public void SetPlayerIcon(Sprite sprite)
        {
            if (!playerIcon) return;
            playerIcon.sprite = sprite;
            playerIcon.enabled = sprite != null;
            playerIcon.preserveAspect = true;
        }

        //Read max stats from Character (combined preferred)
        public void RefreshMaxFromCharacter()
        {
            if (!playerCharacter) return;

            // Prefer combined stats (affected by equipment). Fallback to base from data.
            int combinedMaxHp = Mathf.Max(0, playerCharacter.combinedHealth);
            int baseMaxHp = (playerCharacter.characterData != null) ? playerCharacter.characterData.baseHealth : 0;
            _maxHealth = (combinedMaxHp > 0) ? combinedMaxHp : baseMaxHp;

            // For mana we only have combined on Character (no ResourceSystem given).
            int combinedMaxMp = Mathf.Max(0, playerCharacter.combinedMana);
            _maxMana = (combinedMaxMp > 0) ? combinedMaxMp : 0;

            if (nameText) nameText.text = playerCharacter.name;
        }

        //Snap the displayed values to current (no smoothing)
        public void SnapToCurrent()
        {
            _displayHealth = GetCurrentHealth();
            _displayMana = GetCurrentMana();
        }

        // --- Internals -----------------------------------------------------------

        private void OnEquipmentChanged()
        {
            // Gear changed ? max values likely changed
            RefreshMaxFromCharacter();
            // Keep current display; smoothing will converge
        }

        private int GetCurrentHealth()
        {
            if (resourceSystem)
            {
                // Clamp to max just in case (since ResourceSystem clamps vs base currently)
                int cur = resourceSystem.currentHealth;
                return Mathf.Clamp(cur, 0, Mathf.Max(1, _maxHealth));
            }

            // Fallback (no resource system yet): use max
            return _maxHealth;
        }

        private int GetCurrentMana()
        {
            // We don't have a mana resource yet, so mirror max for now
            // When we add a ManaResourceSystem, read its current value here.
            return _maxMana;
        }

        private void ApplyOnce()
        {
            // Ensure sane maxes
            int maxHp = Mathf.Max(1, _maxHealth);
            int maxMp = Mathf.Max(0, _maxMana);

            // Sliders
            if (healthSlider)
            {
                healthSlider.maxValue = maxHp;
                healthSlider.value = Mathf.Clamp(_displayHealth, 0, maxHp);
            }
            if (manaSlider)
            {
                manaSlider.maxValue = Mathf.Max(1, maxMp);
                manaSlider.value = Mathf.Clamp(_displayMana, 0, manaSlider.maxValue);
            }

            // Texts
            if (healthText) healthText.text = $"{Mathf.RoundToInt(Mathf.Clamp(_displayHealth, 0, maxHp))} / {maxHp}";
            if (manaText && maxMp > 0) manaText.text = $"{Mathf.RoundToInt(Mathf.Clamp(_displayMana, 0, maxMp))} / {maxMp}";
            else if (manaText && maxMp <= 0) manaText.text = "";

            // Fill coloring (optional)
            if (healthFill)
            {
                float t = Mathf.Clamp01((_maxHealth > 0) ? (_displayHealth / _maxHealth) : 0f);
                if (healthGradient != null && healthGradient.colorKeys.Length > 0)
                    healthFill.color = healthGradient.Evaluate(t);
            }
            if (manaFill && _maxMana > 0)
            {
                float t = Mathf.Clamp01((_maxMana > 0) ? (_displayMana / _maxMana) : 0f);
                if (manaGradient != null && manaGradient.colorKeys.Length > 0)
                    manaFill.color = manaGradient.Evaluate(t);
            }
        }
    }
}
