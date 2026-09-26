using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameCraftLab.UnifiedInventorySystem
{
    /// <summary>
    /// Static data of an item type (every sword shares one). Not sealed: games can derive
    /// their own definitions (e.g. WeaponDefinition) to add fields.
    /// </summary>
    [CreateAssetMenu(menuName = "Grid Inventory/Item Definition", fileName = "NewItem", order = 0)]
    public class ItemDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("Stable id used by save files and the item database. Falls back to the asset name.")]
        private string itemId;

        [SerializeField]
        private string displayName;

        [SerializeField, TextArea]
        private string description;

        [SerializeField]
        private Sprite icon;

        [SerializeField, Tooltip("Cells the item covers. Paint them in the inspector.")]
        private List<Vector2Int> shape = new List<Vector2Int> { Vector2Int.zero };

        [SerializeField]
        private bool canRotate = true;

        [SerializeField, Min(1)]
        private int maxStackSize = 1;

        [SerializeField, Tooltip("Free-form tags for your own placement rules (e.g. \"weapon\", \"fish\").")]
        private List<string> tags = new List<string>();

        [NonSerialized]
        private IReadOnlyList<CellShape> _rotations;

        public string ItemId => string.IsNullOrEmpty(itemId) ? name : itemId;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public bool CanRotate => canRotate;
        public int MaxStackSize => Mathf.Max(1, maxStackSize);
        public bool IsStackable => MaxStackSize > 1;
        public IReadOnlyList<string> Tags => tags;

        /// <summary>The raw painted cells, as stored in the asset.</summary>
        public IReadOnlyList<Vector2Int> ShapeCells => shape;

        /// <summary>Distinct rotations; index = clockwise quarter turns. Only one entry when the item can't rotate.</summary>
        public IReadOnlyList<CellShape> Rotations => _rotations ?? (_rotations = BuildRotations());

        public int RotationCount => Rotations.Count;
        public CellShape BaseShape => Rotations[0];

        public CellShape GetShape(int rotation)
        {
            var rotations = Rotations;
            if (rotation < 0 || rotation >= rotations.Count)
                throw new ArgumentOutOfRangeException(nameof(rotation), $"{ItemId} has {rotations.Count} rotation(s).");
            return rotations[rotation];
        }

        public bool IsValidRotation(int rotation) => rotation >= 0 && rotation < RotationCount;

        public bool HasTag(string tag)
        {
            for (int i = 0; i < tags.Count; i++)
                if (string.Equals(tags[i], tag, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        /// <summary>Creates a definition at runtime (tests, procedural items, samples). Not saved as an asset.</summary>
        public static ItemDefinition Create(string id, IEnumerable<Vector2Int> shape, int maxStackSize = 1,
            bool canRotate = true, string displayName = null, Sprite icon = null, params string[] tags)
            => Create<ItemDefinition>(id, shape, maxStackSize, canRotate, displayName, icon, tags);

        /// <inheritdoc cref="Create"/>
        public static T Create<T>(string id, IEnumerable<Vector2Int> shape, int maxStackSize = 1,
            bool canRotate = true, string displayName = null, Sprite icon = null, params string[] tags)
            where T : ItemDefinition
        {
            var def = CreateInstance<T>();
            def.name = id;
            def.itemId = id;
            def.displayName = displayName;
            def.icon = icon;
            def.shape = new List<Vector2Int>(shape);
            def.canRotate = canRotate;
            def.maxStackSize = Mathf.Max(1, maxStackSize);
            def.tags = new List<string>(tags ?? Array.Empty<string>());
            def.Sanitize();
            return def;
        }

        protected virtual void OnValidate()
        {
            Sanitize();
        }

        private void Sanitize()
        {
            maxStackSize = Mathf.Max(1, maxStackSize);
            if (shape == null || shape.Count == 0)
                shape = new List<Vector2Int> { Vector2Int.zero };
            _rotations = null; // shape or rotation flag may have changed
        }

        private IReadOnlyList<CellShape> BuildRotations()
        {
            var baseShape = shape != null && shape.Count > 0 ? new CellShape(shape) : CellShape.Single;
            return canRotate ? baseShape.ComputeUniqueRotations() : new[] { baseShape };
        }
    }
}
