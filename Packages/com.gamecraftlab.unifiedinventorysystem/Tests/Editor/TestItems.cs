using System.Collections.Generic;
using UnityEngine;

namespace GameCraftLab.UnifiedInventorySystem.Tests
{
    /// <summary>Creates runtime ItemDefinitions for tests and destroys them afterwards.</summary>
    internal sealed class TestItems
    {
        private readonly List<Object> _created = new List<Object>();

        public ItemDefinition Def(string id, string pattern = "X", int maxStack = 1, bool canRotate = true, params string[] tags)
        {
            var def = ItemDefinition.Create(id, ShapePattern.Parse(pattern), maxStack, canRotate, null, null, tags);
            _created.Add(def);
            return def;
        }

        public ItemInstance Item(string id, string pattern = "X", int maxStack = 1, bool canRotate = true, int count = 1)
            => new ItemInstance(Def(id, pattern, maxStack, canRotate), count);

        public void DestroyAll()
        {
            foreach (var obj in _created)
                if (obj) Object.DestroyImmediate(obj);
            _created.Clear();
        }
    }
}
