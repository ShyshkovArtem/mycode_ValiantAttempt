using UnityEngine;

namespace RPG.Items
{
    [CreateAssetMenu(menuName = "RPG/Items/Actions/Use Key")]
    public class UseKeyAction : ItemAction
    {
        public override bool CanExecute(ItemContext ctx) =>
            ctx.Stack.Def && ctx.Stack.Def.Category == ItemCategory.Key;

        public override void Execute(ItemContext ctx)
        {
            // Gameplay should decide if the key fits the lock/door via ctx.Extra
            RPG.Events.InventoryEvents.Publish(new RPG.Events.ItemUsed(ctx.User, ctx.Stack));
        }
    }
}
