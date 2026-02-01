using UnityEngine;

namespace RPG.Items
{
    //Command pattern base for item behaviors
    public abstract class ItemAction : ScriptableObject
    {
        [TextArea] public string Description;

        public abstract bool CanExecute(ItemContext ctx);
        public abstract void Execute(ItemContext ctx); // Should publish ItemUsed if consumed/applied
    }

    //Execution context passed to actions
    public readonly struct ItemContext
    {
        public readonly GameObject User;   // the actor using the item
        public readonly ItemStack Stack;   // which stack was used
        public readonly object Extra;      // optional target (GameObject/Component/anything)

        public ItemContext(GameObject user, ItemStack stack, object extra = null)
        {
            User = user; Stack = stack; Extra = extra;
        }
    }
}
