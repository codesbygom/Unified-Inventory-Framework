using System.Collections.Generic;
using UnityEngine;

namespace GameCraftLab.UnifiedInventorySystem
{
    /// <summary>Resolves item ids to definitions when loading. Implement it yourself for Addressables, mods, etc.</summary>
    public interface IItemDefinitionLookup
    {
        bool TryGet(string itemId, out ItemDefinition definition);
    }

    /// <summary>
    /// Asset mapping itemId → ItemDefinition. Fill it with Tools > Grid Inventory > Rebuild Item Database.
    /// </summary>
    [CreateAssetMenu(menuName = "Grid Inventory/Item Database", fileName = "ItemDatabase", order = 2)]
    public sealed class ItemDatabase : ScriptableObject, IItemDefinitionLookup
    {
        [SerializeField]
        private List<ItemDefinition> items = new List<ItemDefinition>();

        [System.NonSerialized]
        private Dictionary<string, ItemDefinition> _byId;

        public IReadOnlyList<ItemDefinition> Items => items;

        public bool TryGet(string itemId, out ItemDefinition definition)
        {
            if (_byId == null) BuildLookup();
            if (itemId != null && _byId.TryGetValue(itemId, out definition))
                return true;
            definition = null;
            return false;
        }

        public ItemDefinition Get(string itemId) => TryGet(itemId, out var def) ? def : null;

        /// <summary>Runtime creation (tests, procedural content). Not saved as an asset.</summary>
        public static ItemDatabase Create(IEnumerable<ItemDefinition> definitions)
        {
            var db = CreateInstance<ItemDatabase>();
            db.items = new List<ItemDefinition>(definitions);
            return db;
        }

        private void OnEnable() => _byId = null;
        private void OnValidate() => _byId = null;

        private void BuildLookup()
        {
            _byId = new Dictionary<string, ItemDefinition>();
            foreach (var def in items)
            {
                if (!def) continue;
                if (_byId.ContainsKey(def.ItemId))
                {
                    Debug.LogWarning($"[ItemDatabase] Duplicate item id '{def.ItemId}' ({def.name}). Only the first one is used.", this);
                    continue;
                }
                _byId.Add(def.ItemId, def);
            }
        }
    }
}
