using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GameCraftLab.UnifiedInventorySystem.Editor
{
    /// <summary>
    /// Clickable cell grid used by the item inspector and the container painter.
    /// Click toggles a cell; click-drag paints (or erases, if the first cell was filled).
    /// </summary>
    internal static class ShapeGridDrawer
    {
        private static readonly Color FilledColor = new Color(0.30f, 0.65f, 1.00f);
        private static readonly Color EmptyColor = new Color(0.22f, 0.22f, 0.22f);
        private static readonly Color HoverTint = new Color(1f, 1f, 1f, 0.15f);

        private static int s_paintControl;
        private static bool s_paintValue;

        /// <summary>Draws the grid and applies edits to <paramref name="cells"/>. Returns true when changed.</summary>
        public static bool Draw(HashSet<Vector2Int> cells, int width, int height, float cellSize = 24f, float gap = 2f)
        {
            int id = GUIUtility.GetControlID(FocusType.Passive);
            float totalW = width * cellSize + (width - 1) * gap;
            float totalH = height * cellSize + (height - 1) * gap;
            var area = GUILayoutUtility.GetRect(totalW, totalH, GUILayout.ExpandWidth(false));

            var e = Event.current;
            bool changed = false;

            Vector2Int? hovered = null;
            if (area.Contains(e.mousePosition))
            {
                var local = e.mousePosition - area.position;
                var cell = new Vector2Int(Mathf.FloorToInt(local.x / (cellSize + gap)), Mathf.FloorToInt(local.y / (cellSize + gap)));
                if (cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < height)
                    hovered = cell;
            }

            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown when e.button == 0 && hovered.HasValue:
                    s_paintControl = id;
                    s_paintValue = !cells.Contains(hovered.Value);
                    changed |= Apply(cells, hovered.Value, s_paintValue);
                    GUIUtility.hotControl = id;
                    e.Use();
                    break;

                case EventType.MouseDrag when GUIUtility.hotControl == id && s_paintControl == id:
                    if (hovered.HasValue) changed |= Apply(cells, hovered.Value, s_paintValue);
                    e.Use();
                    break;

                case EventType.MouseUp when GUIUtility.hotControl == id:
                    GUIUtility.hotControl = 0;
                    s_paintControl = 0;
                    e.Use();
                    break;

                case EventType.MouseMove:
                    if (area.Contains(e.mousePosition)) HandleUtility.Repaint();
                    break;

                case EventType.Repaint:
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            var c = new Vector2Int(x, y);
                            var r = new Rect(area.x + x * (cellSize + gap), area.y + y * (cellSize + gap), cellSize, cellSize);
                            EditorGUI.DrawRect(r, cells.Contains(c) ? FilledColor : EmptyColor);
                            if (hovered == c) EditorGUI.DrawRect(r, HoverTint);
                        }
                    }
                    break;
            }

            return changed;
        }

        /// <summary>Small read-only preview of a shape.</summary>
        public static void DrawPreview(CellShape shape, float cellSize = 10f, float gap = 1f)
        {
            float w = shape.Width * cellSize + (shape.Width - 1) * gap;
            float h = shape.Height * cellSize + (shape.Height - 1) * gap;
            var area = GUILayoutUtility.GetRect(w, h, GUILayout.ExpandWidth(false));
            if (Event.current.type != EventType.Repaint) return;

            for (int y = 0; y < shape.Height; y++)
                for (int x = 0; x < shape.Width; x++)
                {
                    var r = new Rect(area.x + x * (cellSize + gap), area.y + y * (cellSize + gap), cellSize, cellSize);
                    EditorGUI.DrawRect(r, shape.Contains(new Vector2Int(x, y)) ? FilledColor : EmptyColor);
                }
        }

        public static void ReadCells(SerializedProperty list, HashSet<Vector2Int> into)
        {
            into.Clear();
            for (int i = 0; i < list.arraySize; i++)
                into.Add(list.GetArrayElementAtIndex(i).vector2IntValue);
        }

        public static void WriteCells(SerializedProperty list, IEnumerable<Vector2Int> cells)
        {
            var sorted = new List<Vector2Int>(cells);
            sorted.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
            list.arraySize = sorted.Count;
            for (int i = 0; i < sorted.Count; i++)
                list.GetArrayElementAtIndex(i).vector2IntValue = sorted[i];
        }

        /// <summary>Smallest canvas that shows every cell plus one spare row/column to grow into.</summary>
        public static Vector2Int SuggestCanvas(IEnumerable<Vector2Int> cells, int minSize)
        {
            int w = minSize, h = minSize;
            foreach (var c in cells)
            {
                w = Mathf.Max(w, c.x + 2);
                h = Mathf.Max(h, c.y + 2);
            }
            return new Vector2Int(w, h);
        }

        private static bool Apply(HashSet<Vector2Int> cells, Vector2Int cell, bool fill)
            => fill ? cells.Add(cell) : cells.Remove(cell);
    }
}
