using UnityEngine;

namespace RPG.Items
{
    public enum HealAmountMode { Flat, PercentOfMax }

    [CreateAssetMenu(menuName = "RPG/Items/Actions/Use Health Potion")]
    public class UseHealthPotionAction : ItemAction
    {
        [Header("Amount")]
        public HealAmountMode AmountMode = HealAmountMode.Flat;

        [Min(1)] public int HealAmount = 25;           // Flat points
        [Range(0f, 1f)] public float HealPercent = 0.30f; // 0..1 of MaxHealth when Percent mode
        public bool RoundUpPercent = true;             // Ceil vs. round for percent math

        [Tooltip("If true, prevents using when target is already at full HP.")]
        public bool BlockWhenAtFull = true;

        public override bool CanExecute(ItemContext ctx)
        {
            // Must be a consumable and must have a healable target
            if (ctx.Stack.Def == null || ctx.Stack.Def.Category != ItemCategory.Consumable) return false;

            var rs = ResolveTarget(ctx);
            if (rs == null) return false;

            if (BlockWhenAtFull)
                return rs.GetHealthPercentage() < 1f && !rs.IsDead();

            return !rs.IsDead();
        }

        public override void Execute(ItemContext ctx)
        {
            var rs = ResolveTarget(ctx);
            if (rs == null)
            {
                Debug.LogWarning("[UseHealthPotionAction] No valid ResourceSystem target.");
                return;
            }

            // --- Compute heal amount (flat or % of current max HP) ---
            int heal = HealAmount;
            if (AmountMode == HealAmountMode.PercentOfMax)
            {
                int maxHp = Mathf.Max(1, rs.MaxHealth);
                float raw = maxHp * Mathf.Clamp01(HealPercent);
                heal = RoundUpPercent ? Mathf.Max(1, Mathf.CeilToInt(raw))
                                      : Mathf.Max(1, Mathf.RoundToInt(raw));
            }

            // Apply heal via your ResourceSystem API (your RS will add Intelligence etc. as designed)
            rs.ApplyHealthChange(ctx.User.GetComponent<Character>(), heal, SkillType.SupportiveSkill);

            // Publish inventory event(s) and consume one if configured
            if (ctx.Stack.Def.DestroyOnUse)
            {
                RPG.Events.InventoryEvents.Publish(new RPG.Events.ItemUsed(ctx.User, ConsumeOne(ctx.Stack)));
            }
            else
            {
                RPG.Events.InventoryEvents.Publish(new RPG.Events.ItemUsed(ctx.User, ctx.Stack));
            }
        }

        // --- Helpers ---

        private ResourceSystem ResolveTarget(ItemContext ctx)
        {
            // Priority: explicit ResourceSystem -> GameObject target -> user
            if (ctx.Extra is ResourceSystem directRs) return directRs;

            if (ctx.Extra is GameObject goExtra)
            {
                var rs = goExtra.GetComponent<ResourceSystem>();
                if (rs) return rs;
            }

            if (ctx.User)
            {
                var rs = ctx.User.GetComponent<ResourceSystem>();
                if (rs) return rs;
            }

            return null;
        }

        private ItemStack ConsumeOne(ItemStack stack)
        {
            var inst = stack.Instance;
            inst.Quantity = Mathf.Max(0, inst.Quantity - 1);
            return new ItemStack(inst);
        }
    }
}
