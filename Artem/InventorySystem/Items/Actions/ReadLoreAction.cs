using UnityEngine;

namespace RPG.Items
{
    [CreateAssetMenu(menuName = "RPG/Items/Actions/Read Lore")]
    public class ReadLoreAction : ItemAction
    {
        public override bool CanExecute(ItemContext ctx) =>
            ctx.Stack.Def && ctx.Stack.Def.Category == ItemCategory.Lore;

        public override void Execute(ItemContext ctx)
        {
            var def = ctx.Stack.Def;
            var title = def ? def.DisplayName : "Lore";
            var body = (def && def.LoreText) ? def.LoreText.text
                       : (!string.IsNullOrWhiteSpace(Description) ? Description : "No text.");

            // Show lore panel (your UI listens for this event)
            RPG.Events.InventoryEvents.Publish(
                new RPG.Events.ShowLoreRequested(ctx.User, title, body)
            );

            // Not consumed; optionally still publish ItemUsed for analytics:
            // RPG.Events.InventoryEvents.Publish(new RPG.Events.ItemUsed(ctx.User, ctx.Stack));
        }
    }
}
