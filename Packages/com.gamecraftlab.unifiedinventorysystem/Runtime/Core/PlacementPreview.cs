using UnityEngine;

namespace GameCraftLab.UnifiedInventorySystem
{
    public enum CellPreviewStatus
    {
        Valid,
        OutOfContainer,
        Blocked,
        RejectedByRule
    }

    /// <summary>Status of one cell of a would-be placement, for green/red highlighting.</summary>
    public readonly struct CellPreview
    {
        public readonly Vector2Int Cell;
        public readonly CellPreviewStatus Status;

        /// <summary>The item occupying the cell when Status is Blocked, otherwise null.</summary>
        public readonly ItemInstance Blocker;

        public CellPreview(Vector2Int cell, CellPreviewStatus status, ItemInstance blocker = null)
        {
            Cell = cell;
            Status = status;
            Blocker = blocker;
        }
    }
}
