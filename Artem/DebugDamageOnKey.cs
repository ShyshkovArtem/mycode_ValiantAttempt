using UnityEngine;

public class DebugDamageOnKey : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] KeyCode key = KeyCode.K;

    [Header("Damage")]
    [SerializeField] int damageAmount = 10;

    [Header("Target")]
    [SerializeField] ResourceSystem target;         // Drag your player here
    [SerializeField] bool findPlayerByTag = true;   // Or auto-find by tag "Player"
    [SerializeField] string playerTag = "Player";

    void Awake()
    {
        if (!target && findPlayerByTag)
        {
            var obj = GameObject.FindGameObjectWithTag(playerTag);
            if (obj) target = obj.GetComponent<ResourceSystem>();
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(key))
        {
            if (target)
                target.ApplyHealthChange(null, damageAmount, SkillType.OffensiveSkill);
            else
                Debug.LogWarning("[DebugDamageOnKey] No ResourceSystem target set/found.");
        }
    }
}
