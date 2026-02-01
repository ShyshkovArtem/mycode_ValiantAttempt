using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    Character controller;
    [SerializeField] Slider healthSlider;
    [SerializeField] CanvasGroup canvasGroup;

    [SerializeField] bool debugMode;

    void Start()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (controller == null) controller = GetComponentInParent<Character>();
        controller.characterEventBusHandler.OnHealthChangePercentage += UpdateHealth;
        Hide();
    }

    public void UpdateHealth(float healthPercentage)
    {
        if (debugMode) Debug.Log("Updating health bar to: " + healthPercentage * 100 + "%");

        healthSlider.value = healthPercentage;
        if (healthPercentage < 1f)
        {
            if (debugMode) Debug.Log("Showing health bar.");
            Show();
        }
        else
        {
            if (debugMode) Debug.Log("Hiding health bar.");
            Hide();
        }
    }

    void Show()
    {
        canvasGroup.alpha = 1f;
    }

    void Hide()
    {
        canvasGroup.alpha = 0f;
    }

    void OnDestroy()
    {
        //controller.characterEventHandler.OnHealthChangePercentage -= UpdateHealth;
    }
}