using System.Collections.Generic;
using UnityEngine;

namespace GameCraftLab.UnifiedInventorySystem
{
    /// <summary>
    /// Asset describing which cells a container has: a full rectangle (basic / RE4 style)
    /// or any shape (Dredge style). Build a runtime grid from it with <c>new InventoryGrid(definition)</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Grid Inventory/Container Definition", fileName = "NewContainer", order = 1)]
    public sealed class ContainerDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("Stable id used by save files. Falls back to the asset name.")]
        private string containerId;

        [SerializeField]
        private string displayName;

        [SerializeField]
        private List<Vector2Int> cells = new List<Vector2Int>();

        public string Id => string.IsNullOrEmpty(containerId) ? name : containerId;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public IReadOnlyList<Vector2Int> Cells => cells;

        /// <summary>Creates a rectangular container at runtime (not saved as an asset).</summary>
        public static ContainerDefinition CreateRectangle(string id, int width, int height)
        {
            var list = new List<Vector2Int>(width * height);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    list.Add(new Vector2Int(x, y));
            return Create(id, list);
        }

        /// <summary>Creates a container of any shape at runtime (not saved as an asset).</summary>
        public static ContainerDefinition Create(string id, IEnumerable<Vector2Int> cells)
        {
            var def = CreateInstance<ContainerDefinition>();
            def.name = id;
            def.containerId = id;
            def.cells = new List<Vector2Int>(new HashSet<Vector2Int>(cells));
            return def;
        }

        private void Reset()
        {
            cells = new List<Vector2Int>();
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 6; x++)
                    cells.Add(new Vector2Int(x, y));
        }
    }
}
