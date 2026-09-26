using UnityEngine;

namespace GameCraftLab.UnifiedInventorySystem.UI
{
    /// <summary>
    /// Converts between grid cells and UI positions. "Content space" is measured from the
    /// top-left corner of the grid rect: x grows right, y grows UP (uGUI convention), so rows go to negative y.
    /// </summary>
    public sealed class GridCoordinateConverter
    {
        public Vector2 CellSize { get; }
        public Vector2 Spacing { get; }

        /// <summary>Grid cell drawn at the top-left corner (the grid's Bounds.min).</summary>
        public Vector2Int BoundsMin { get; }

        /// <summary>Distance from one cell to the next.</summary>
        public Vector2 Pitch => CellSize + Spacing;

        public GridCoordinateConverter(Vector2 cellSize, Vector2 spacing, Vector2Int boundsMin)
        {
            CellSize = new Vector2(Mathf.Max(1f, cellSize.x), Mathf.Max(1f, cellSize.y));
            Spacing = new Vector2(Mathf.Max(0f, spacing.x), Mathf.Max(0f, spacing.y));
            BoundsMin = boundsMin;
        }

        /// <summary>Top-left corner of a cell, in content space.</summary>
        public Vector2 CellToLocal(Vector2Int cell)
            => new Vector2((cell.x - BoundsMin.x) * Pitch.x, -(cell.y - BoundsMin.y) * Pitch.y);

        /// <summary>Center of a cell, in content space.</summary>
        public Vector2 CellCenterToLocal(Vector2Int cell)
            => CellToLocal(cell) + new Vector2(CellSize.x * 0.5f, -CellSize.y * 0.5f);

        /// <summary>Offset (relative to an item's top-left) of the center of one of its shape cells.</summary>
        public Vector2 ShapeCellCenterOffset(Vector2Int shapeCell)
            => new Vector2(shapeCell.x * Pitch.x + CellSize.x * 0.5f, -(shapeCell.y * Pitch.y + CellSize.y * 0.5f));

        /// <summary>Cell under a content-space point. Spacing belongs to the cell before it.</summary>
        public Vector2Int LocalToCell(Vector2 local)
            => new Vector2Int(Mathf.FloorToInt(local.x / Pitch.x), Mathf.FloorToInt(-local.y / Pitch.y)) + BoundsMin;

        /// <summary>Pixel size of a block of cells, spacing included between them.</summary>
        public Vector2 SizeFor(int width, int height)
            => new Vector2(width * CellSize.x + (width - 1) * Spacing.x, height * CellSize.y + (height - 1) * Spacing.y);

        /// <summary>Top-left corner of <paramref name="rect"/> in its own local space.</summary>
        public static Vector2 TopLeft(RectTransform rect) => new Vector2(rect.rect.xMin, rect.rect.yMax);

        /// <summary>Cell under a screen point. False when the point isn't over the rect's plane.</summary>
        public bool ScreenToGridCell(RectTransform rect, Vector2 screenPosition, Camera eventCamera, out Vector2Int cell)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPosition, eventCamera, out var local))
            {
                cell = default;
                return false;
            }
            cell = LocalToCell(local - TopLeft(rect));
            return true;
        }

        /// <summary>World position of a cell's top-left corner.</summary>
        public Vector3 CellToWorld(RectTransform rect, Vector2Int cell)
            => rect.TransformPoint(TopLeft(rect) + CellToLocal(cell));

        /// <summary>Screen position of a cell's center.</summary>
        public Vector2 CellToScreenPosition(RectTransform rect, Vector2Int cell, Camera eventCamera)
            => RectTransformUtility.WorldToScreenPoint(eventCamera, rect.TransformPoint(TopLeft(rect) + CellCenterToLocal(cell)));
    }
}
