using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameCraftLab.UnifiedInventorySystem
{
    /// <summary>
    /// Text format for shapes: rows separated by '/', 'X' or '#' is a filled cell, '.' (or anything else) is empty.
    /// Example: "XX/X." is an L of three cells. Used by code, tests and the CSV/JSON importer.
    /// </summary>
    public static class ShapePattern
    {
        public static List<Vector2Int> Parse(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                throw new ArgumentException("Pattern is empty.", nameof(pattern));

            var cells = new List<Vector2Int>();
            var rows = pattern.Trim().Split('/', '|', '\n');
            for (int y = 0; y < rows.Length; y++)
            {
                var row = rows[y].Trim();
                for (int x = 0; x < row.Length; x++)
                {
                    char ch = row[x];
                    if (ch == 'X' || ch == 'x' || ch == '#')
                        cells.Add(new Vector2Int(x, y));
                }
            }

            if (cells.Count == 0)
                throw new ArgumentException($"Pattern '{pattern}' has no filled cells.", nameof(pattern));
            return cells;
        }

        public static CellShape ParseShape(string pattern) => new CellShape(Parse(pattern));

        public static string ToPattern(IEnumerable<Vector2Int> cells) => new CellShape(cells).ToString();
    }
}
