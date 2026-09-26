using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameCraftLab.UnifiedInventorySystem
{
    /// <summary>
    /// An immutable set of cells describing the footprint of an item.
    /// Cells are normalized on construction so the top-left of the bounds is (0,0).
    /// Coordinates: x grows right, y grows down.
    /// </summary>
    public sealed class CellShape : IReadOnlyCollection<Vector2Int>, IEquatable<CellShape>
    {
        private readonly HashSet<Vector2Int> _cells;
        private readonly int _hash;

        /// <summary>Width of the shape's bounding box, in cells.</summary>
        public int Width { get; }

        /// <summary>Height of the shape's bounding box, in cells.</summary>
        public int Height { get; }

        /// <summary>Number of cells the shape covers.</summary>
        public int Count => _cells.Count;

        /// <summary>Creates a shape from any set of cells. Duplicates are ignored, position is normalized.</summary>
        /// <exception cref="ArgumentException">The shape has no cells.</exception>
        public CellShape(IEnumerable<Vector2Int> cells)
        {
            if (cells == null) throw new ArgumentNullException(nameof(cells));

            var raw = new HashSet<Vector2Int>(cells);
            if (raw.Count == 0)
                throw new ArgumentException("A shape needs at least one cell.", nameof(cells));

            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var c in raw)
            {
                if (c.x < minX) minX = c.x;
                if (c.y < minY) minY = c.y;
                if (c.x > maxX) maxX = c.x;
                if (c.y > maxY) maxY = c.y;
            }

            var offset = new Vector2Int(minX, minY);
            _cells = new HashSet<Vector2Int>();
            foreach (var c in raw)
                _cells.Add(c - offset);

            Width = maxX - minX + 1;
            Height = maxY - minY + 1;
            _hash = ComputeHash(_cells);
        }

        /// <summary>A 1x1 shape.</summary>
        public static CellShape Single { get; } = new CellShape(new[] { Vector2Int.zero });

        /// <summary>A filled rectangle of the given size.</summary>
        public static CellShape Rectangle(int width, int height)
        {
            if (width < 1 || height < 1)
                throw new ArgumentOutOfRangeException(width < 1 ? nameof(width) : nameof(height), "Size must be at least 1.");

            var cells = new List<Vector2Int>(width * height);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    cells.Add(new Vector2Int(x, y));
            return new CellShape(cells);
        }

        public bool Contains(Vector2Int cell) => _cells.Contains(cell);

        /// <summary>Allocation-free enumerator for foreach.</summary>
        public HashSet<Vector2Int>.Enumerator GetEnumerator() => _cells.GetEnumerator();

        IEnumerator<Vector2Int> IEnumerable<Vector2Int>.GetEnumerator() => _cells.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _cells.GetEnumerator();

        /// <summary>
        /// Returns this shape rotated 90° clockwise (as seen on screen with y pointing down).
        /// The result is already normalized.
        /// </summary>
        public CellShape RotateCW()
        {
            var rotated = new List<Vector2Int>(_cells.Count);
            foreach (var c in _cells)
                rotated.Add(RotateCellCW(c, Height));
            return new CellShape(rotated);
        }

        /// <summary>
        /// Rotates a single cell of a normalized shape of the given height 90° clockwise.
        /// Useful to keep track of a specific cell (e.g. the one under the cursor) while rotating.
        /// </summary>
        public static Vector2Int RotateCellCW(Vector2Int cell, int shapeHeight)
            => new Vector2Int(shapeHeight - 1 - cell.y, cell.x);

        /// <summary>
        /// The distinct rotations of this shape, starting with the shape itself.
        /// Index i is the shape turned i quarter-turns clockwise.
        /// A square has 1, a straight bar 2, an L-shape 4.
        /// </summary>
        public IReadOnlyList<CellShape> ComputeUniqueRotations()
        {
            var result = new List<CellShape> { this };
            var current = this;
            for (int i = 1; i < 4; i++)
            {
                current = current.RotateCW();
                if (current.Equals(this))
                    break;
                result.Add(current);
            }
            return result;
        }

        public bool Equals(CellShape other)
        {
            if (ReferenceEquals(this, other)) return true;
            if (other is null || other._hash != _hash || other.Count != Count) return false;
            return _cells.SetEquals(other._cells);
        }

        public override bool Equals(object obj) => obj is CellShape other && Equals(other);

        public override int GetHashCode() => _hash;

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            for (int y = 0; y < Height; y++)
            {
                if (y > 0) sb.Append('/');
                for (int x = 0; x < Width; x++)
                    sb.Append(_cells.Contains(new Vector2Int(x, y)) ? 'X' : '.');
            }
            return sb.ToString();
        }

        // Order-independent hash so equal sets always hash equally.
        private static int ComputeHash(HashSet<Vector2Int> cells)
        {
            int sum = 0, xor = 0;
            foreach (var c in cells)
            {
                int h = c.x * 73856093 ^ c.y * 19349663;
                sum += h;
                xor ^= h;
            }
            return (sum * 397) ^ xor ^ cells.Count;
        }
    }
}
