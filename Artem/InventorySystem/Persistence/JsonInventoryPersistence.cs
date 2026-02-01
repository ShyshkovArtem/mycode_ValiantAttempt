// ============================================================================
// PERSISTENCE (basic JSON — swap with save system, if will have it :) )
// ============================================================================
namespace RPG.Adapters
{
    using RPG.Inventory;
    using RPG.Items;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using UnityEngine;


    public class JsonInventoryPersistence : IInventoryPersistence
    {
        private static string PathFor(GameObject owner) => System.IO.Path.Combine(Application.persistentDataPath, $"inventory_{owner.GetInstanceID()}.json");


        public void Save(GameObject owner, IReadOnlyList<ItemInstance> items)
        {
            var json = JsonUtility.ToJson(new Wrapper { Items = items.ToList() }, true);
            File.WriteAllText(PathFor(owner), json, Encoding.UTF8);
        }
        public List<ItemInstance> Load(GameObject owner)
        {
            var path = PathFor(owner);
            if (!File.Exists(path)) return new List<ItemInstance>();
            var json = File.ReadAllText(path, Encoding.UTF8);
            var wrapper = JsonUtility.FromJson<Wrapper>(json);
            return wrapper?.Items ?? new List<ItemInstance>();
        }
        [Serializable] private class Wrapper { public List<ItemInstance> Items; }
    }
}
