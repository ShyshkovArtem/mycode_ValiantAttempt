using UnityEngine;

[CreateAssetMenu(menuName = "RPG/Skill")]
public class SkillData : ScriptableObject
{
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Stats")]
    public float cooldown;
    public float power;
    public float range;
}
