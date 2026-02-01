using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class UITabBar : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button inventoryButton;
    [SerializeField] private Button equipmentButton;
    [SerializeField] private Button generalButton;
    [SerializeField] private Button skillsButton;

    [Header("Shared Sprites")]
    [Tooltip("Sprite used by inactive tabs.")]
    [SerializeField] private Sprite normalSprite;
    [Tooltip("Sprite used by the currently active tab.")]
    [SerializeField] private Sprite selectedSprite;

    [Header("Input (same asset as PlayerController)")]
    [SerializeField] private InputActionAsset inputActions;

    private MenuType _current = MenuType.None;

    // Order of tabs for LB/RB cycling
    private readonly MenuType[] tabOrder = new[]
    {
        MenuType.General,
        MenuType.Inventory,
        MenuType.Equipment,
        MenuType.Skills
    };

    private InputAction nextTabAction;
    private InputAction prevTabAction;

    private void Awake()
    {
        if (inputActions == null)
        {
            Debug.LogWarning($"{nameof(UITabBar)}: InputActions not assigned. LB/RB tab switching will be disabled.");
            return;
        }

        // Try to use a "UI" map first, otherwise fall back to "Player"
        InputActionMap tabMap =
            inputActions.FindActionMap("UI", throwIfNotFound: false) ??
            inputActions.FindActionMap("Player", throwIfNotFound: false);

        if (tabMap == null)
        {
            Debug.LogWarning($"{nameof(UITabBar)}: No 'UI' or 'Player' action map found in InputActions. LB/RB tab switching will be disabled.");
            return;
        }

        nextTabAction = tabMap.FindAction("NextTab", throwIfNotFound: false);
        prevTabAction = tabMap.FindAction("PrevTab", throwIfNotFound: false);

        if (nextTabAction == null || prevTabAction == null)
        {
            Debug.LogWarning($"{nameof(UITabBar)}: 'NextTab' and/or 'PrevTab' actions not found in map '{tabMap.name}'. LB/RB tab switching will be disabled.");
        }
    }

    private void OnEnable()
    {
        if (nextTabAction != null)
        {
            nextTabAction.performed += OnNextTab;
            nextTabAction.Enable(); // Added by Sami: Enabling the action, so we can use it with steam-input.
            // We do NOT Enable() the map here – assume your global code handles that
        }

        if (prevTabAction != null)
        {
            prevTabAction.performed += OnPrevTab;
            prevTabAction.Enable(); // Added by Sami: Enabling the action, so we can use it with steam-input.
        }
    }

    private void OnDisable()
    {
        if (nextTabAction != null)
            nextTabAction.performed -= OnNextTab;
            nextTabAction.Disable(); // Added by Sami

        if (prevTabAction != null)
            prevTabAction.performed -= OnPrevTab;
            prevTabAction.Disable(); // Added by Sami
    }

    private void Start()
    {
        // Mouse / keyboard / gamepad Submit will all trigger these listeners
        if (inventoryButton) inventoryButton.onClick.AddListener(() => OpenTab(MenuType.Inventory));
        if (equipmentButton) equipmentButton.onClick.AddListener(() => OpenTab(MenuType.Equipment));
        if (generalButton) generalButton.onClick.AddListener(() => OpenTab(MenuType.General));
        if (skillsButton) skillsButton.onClick.AddListener(() => OpenTab(MenuType.Skills));

        UpdateSprites();
    }

    private void OnNextTab(InputAction.CallbackContext ctx)
    {
        ChangeTab(+1);
    }

    private void OnPrevTab(InputAction.CallbackContext ctx)
    {
        ChangeTab(-1);
    }

    private void ChangeTab(int direction)
    {
        // BLOCK LB/RB while modal UI (lore) is open
        if (UIMenuManager.HasInstance && UIMenuManager.Instance.InputLocked)
            return;

        int currentIndex = System.Array.IndexOf(tabOrder, _current);
        if (currentIndex < 0) currentIndex = 0;

        int newIndex = (currentIndex + direction + tabOrder.Length) % tabOrder.Length;
        OpenTab(tabOrder[newIndex]);
    }

    private void OpenTab(MenuType type)
    {
        // BLOCK mouse/gamepad click while modal UI is open
        if (UIMenuManager.HasInstance && UIMenuManager.Instance.InputLocked)
            return;

        UIMenuManager.Instance.Open(type);
        SetActiveTab(type);
    }

    public void SetActiveTab(MenuType type)
    {
        // Prevent visual switching while lore panel is open
        if (UIMenuManager.HasInstance && UIMenuManager.Instance.InputLocked)
            return;

        _current = type;
        UpdateSprites();
    }

    private void UpdateSprites()
    {
        if (inventoryButton && inventoryButton.image)
            inventoryButton.image.sprite = (_current == MenuType.Inventory) ? selectedSprite : normalSprite;

        if (equipmentButton && equipmentButton.image)
            equipmentButton.image.sprite = (_current == MenuType.Equipment) ? selectedSprite : normalSprite;

        if (generalButton && generalButton.image)
            generalButton.image.sprite = (_current == MenuType.General) ? selectedSprite : normalSprite;

        if (skillsButton && skillsButton.image)
            skillsButton.image.sprite = (_current == MenuType.Skills) ? selectedSprite : normalSprite;
    }
}
