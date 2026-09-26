using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameCraftLab.UnifiedInventorySystem
{
    /// <summary>
    /// Runtime state of one container. Owns geometry only: which cells exist and what covers them.
    /// Every other rule comes from <see cref="IPlacementValidator"/>s.
    /// Coordinates: (0,0) is the top-left cell, y grows downward.
    /// Pure C#: no MonoBehaviour, fully unit-testable.
    /// </summary>
    public sealed class InventoryGrid
    {
        // What the grid remembers about a placed item. The shape is stored so Remove/Move always
        // free exactly the cells that were taken, even if the definition is edited while playing.
        private readonly struct PlacedItem
        {
            public readonly Placement Placement;
            public readonly CellShape Shape;

            public PlacedItem(Placement placement, CellShape shape)
            {
                Placement = placement;
                Shape = shape;
            }
        }

        // Key = cell exists (keys never change after construction). Value = item covering it, null = empty.
        private readonly Dictionary<Vector2Int, ItemInstance> _cells = new Dictionary<Vector2Int, ItemInstance>();

        // Item -> where it is. Lets Remove/Move touch only the item's own cells.
        private readonly Dictionary<ItemInstance, PlacedItem> _placed = new Dictionary<ItemInstance, PlacedItem>();

        private readonly List<IPlacementValidator> _validators = new List<IPlacementValidator>();

        /// <summary>Bounding box of all cells. For rendering and scanning; not every cell inside it has to exist.</summary>
        public RectInt Bounds { get; }

        public int CellCount => _cells.Count;
        public int ItemCount => _placed.Count;
        public IReadOnlyCollection<Vector2Int> Cells => _cells.Keys;
        public IReadOnlyCollection<ItemInstance> Items => _placed.Keys;
        public IReadOnlyList<IPlacementValidator> Validators => _validators;

        public event Action<ItemInstance> ItemAdded;
        public event Action<ItemInstance> ItemRemoved;
        public event Action<ItemInstance, Placement, Placement> ItemMoved;

        /// <summary>Raised once after any change (add, remove, move, clear). Simplest hook for UI refresh.</summary>
        public event Action Changed;

        /// <exception cref="ArgumentException">No cells given.</exception>
        public InventoryGrid(IEnumerable<Vector2Int> validCells, params IPlacementValidator[] validators)
        {
            if (validCells == null) throw new ArgumentNullException(nameof(validCells));

            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var pos in validCells)
            {
                _cells[pos] = null;
                if (pos.x < minX) minX = pos.x;
                if (pos.y < minY) minY = pos.y;
                if (pos.x > maxX) maxX = pos.x;
                if (pos.y > maxY) maxY = pos.y;
            }

            if (_cells.Count == 0)
                throw new ArgumentException("A grid needs at least one cell.", nameof(validCells));

            Bounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);

            if (validators != null)
                foreach (var v in validators)
                    AddValidator(v);
        }

        public InventoryGrid(int width, int height, params IPlacementValidator[] validators)
            : this(Rectangle(width, height), validators)
        {
        }

        public InventoryGrid(ContainerDefinition definition, params IPlacementValidator[] validators)
            : this(definition ? definition.Cells : throw new ArgumentNullException(nameof(definition)), validators)
        {
        }

        // ---------- validators ----------

        public void AddValidator(IPlacementValidator validator)
        {
            if (validator == null) throw new ArgumentNullException(nameof(validator));
            _validators.Add(validator);
        }

        public bool RemoveValidator(IPlacementValidator validator) => _validators.Remove(validator);

        // ---------- queries ----------

        public bool Exists(Vector2Int cell) => _cells.ContainsKey(cell);

        public bool IsFree(Vector2Int cell) => _cells.TryGetValue(cell, out var item) && item == null;

        public ItemInstance GetItemAt(Vector2Int cell)
            => _cells.TryGetValue(cell, out var item) ? item : null;

        public bool Contains(ItemInstance item) => item != null && _placed.ContainsKey(item);

        public bool TryGetPlacement(ItemInstance item, out Placement placement)
        {
            if (item != null && _placed.TryGetValue(item, out var entry))
            {
                placement = entry.Placement;
                return true;
            }
            placement = default;
            return false;
        }

        /// <summary>Grid cells covered by an item in this grid (empty if it isn't here).</summary>
        public IEnumerable<Vector2Int> GetOccupiedCells(ItemInstance item)
        {
            if (item == null || !_placed.TryGetValue(item, out var entry))
                yield break;
            foreach (var offset in entry.Shape)
                yield return entry.Placement.Origin + offset;
        }

        public int CountFreeCells()
        {
            int free = 0;
            foreach (var kv in _cells)
                if (kv.Value == null) free++;
            return free;
        }

        public PlacementResult CanPlace(ItemInstance item, Vector2Int origin, int rotation = 0, ItemInstance ignore = null)
            => CanPlace(item, new Placement(origin, rotation), ignore);

        /// <param name="ignore">Item treated as absent (the one being moved), so it doesn't block itself.</param>
        public PlacementResult CanPlace(ItemInstance item, Placement placement, ItemInstance ignore = null)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (!item.Definition.IsValidRotation(placement.Rotation))
                return PlacementResult.InvalidRotation;

            var shape = item.GetShape(placement.Rotation);
            var origin = placement.Origin;

            // Pass 1 — every cell must exist.
            foreach (var offset in shape)
                if (!_cells.ContainsKey(origin + offset))
                    return PlacementResult.OutOfContainer;

            // Pass 2 — no overlap.
            foreach (var offset in shape)
            {
                var occupant = _cells[origin + offset];
                if (occupant != null && occupant != ignore)
                    return PlacementResult.Blocked;
            }

            // Pass 3 — game rules. Only runs when geometry is fine.
            if (_validators.Count > 0)
            {
                foreach (var offset in shape)
                {
                    var target = origin + offset;
                    for (int i = 0; i < _validators.Count; i++)
                        if (!_validators[i].CanOccupy(this, target, item))
                            return PlacementResult.RejectedByRule;
                }
            }

            return PlacementResult.Success;
        }

        /// <summary>
        /// Like CanPlace, but checks every cell (no early exit) so the UI can color each one.
        /// Fills <paramref name="results"/> (cleared first) and returns the same overall result CanPlace would.
        /// </summary>
        public PlacementResult GetPlacementPreview(ItemInstance item, Placement placement, List<CellPreview> results,
            ItemInstance ignore = null)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (results == null) throw new ArgumentNullException(nameof(results));

            results.Clear();
            if (!item.Definition.IsValidRotation(placement.Rotation))
                return PlacementResult.InvalidRotation;

            bool anyOut = false, anyBlocked = false, anyRejected = false;
            foreach (var offset in item.GetShape(placement.Rotation))
            {
                var target = placement.Origin + offset;

                if (!_cells.TryGetValue(target, out var occupant))
                {
                    results.Add(new CellPreview(target, CellPreviewStatus.OutOfContainer));
                    anyOut = true;
                }
                else if (occupant != null && occupant != ignore)
                {
                    results.Add(new CellPreview(target, CellPreviewStatus.Blocked, occupant));
                    anyBlocked = true;
                }
                else if (!PassesValidators(target, item))
                {
                    results.Add(new CellPreview(target, CellPreviewStatus.RejectedByRule));
                    anyRejected = true;
                }
                else
                {
                    results.Add(new CellPreview(target, CellPreviewStatus.Valid));
                }
            }

            if (anyOut) return PlacementResult.OutOfContainer;
            if (anyBlocked) return PlacementResult.Blocked;
            if (anyRejected) return PlacementResult.RejectedByRule;
            return PlacementResult.Success;
        }

        /// <summary>
        /// Finds the first free spot, scanning rows top to bottom, left to right.
        /// Tries the unrotated shape everywhere before trying other rotations.
        /// </summary>
        public bool TryFindPlacement(ItemInstance item, out Placement placement, bool allowRotation = true,
            ItemInstance ignore = null)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            int rotations = allowRotation ? item.Definition.RotationCount : 1;
            for (int r = 0; r < rotations; r++)
            {
                var shape = item.GetShape(r);
                for (int y = Bounds.yMin; y <= Bounds.yMax - shape.Height; y++)
                {
                    for (int x = Bounds.xMin; x <= Bounds.xMax - shape.Width; x++)
                    {
                        var candidate = new Placement(new Vector2Int(x, y), r);
                        if (CanPlace(item, candidate, ignore) == PlacementResult.Success)
                        {
                            placement = candidate;
                            return true;
                        }
                    }
                }
            }

            placement = default;
            return false;
        }

        // ---------- mutations ----------

        public PlacementResult TryPlace(ItemInstance item, Vector2Int origin, int rotation = 0)
            => TryPlace(item, new Placement(origin, rotation));

        public PlacementResult TryPlace(ItemInstance item, Placement placement)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (_placed.ContainsKey(item))
                return PlacementResult.AlreadyPlaced;

            var result = CanPlace(item, placement);
            if (result != PlacementResult.Success)
                return result;

            Occupy(item, placement, item.GetShape(placement.Rotation));
            ItemAdded?.Invoke(item);
            Changed?.Invoke();
            return PlacementResult.Success;
        }

        /// <summary>Places the item in the first free spot (see <see cref="TryFindPlacement"/>).</summary>
        public PlacementResult TryAutoPlace(ItemInstance item, bool allowRotation = true)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (_placed.ContainsKey(item))
                return PlacementResult.AlreadyPlaced;

            return TryFindPlacement(item, out var placement, allowRotation)
                ? TryPlace(item, placement)
                : PlacementResult.NoFreeSpace;
        }

        /// <summary>Moves and/or rotates an item already in this grid. Atomic: on failure nothing changes.</summary>
        public PlacementResult TryMove(ItemInstance item, Placement to)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (!_placed.TryGetValue(item, out var entry))
                return PlacementResult.NotInGrid;

            var from = entry.Placement;
            if (from == to)
                return PlacementResult.Success;

            var result = CanPlace(item, to, ignore: item);
            if (result != PlacementResult.Success)
                return result;

            Vacate(item, entry);
            Occupy(item, to, item.GetShape(to.Rotation));
            ItemMoved?.Invoke(item, from, to);
            Changed?.Invoke();
            return PlacementResult.Success;
        }

        public PlacementResult TryMove(ItemInstance item, Vector2Int origin)
            => TryGetPlacement(item, out var p) ? TryMove(item, p.WithOrigin(origin)) : PlacementResult.NotInGrid;

        /// <summary>Rotates in place (same origin).</summary>
        public PlacementResult TryRotate(ItemInstance item, int rotation)
            => TryGetPlacement(item, out var p) ? TryMove(item, p.WithRotation(rotation)) : PlacementResult.NotInGrid;

        public bool Remove(ItemInstance item)
        {
            if (item == null || !_placed.TryGetValue(item, out var entry))
                return false;

            Vacate(item, entry);
            ItemRemoved?.Invoke(item);
            Changed?.Invoke();
            return true;
        }

        public void Clear()
        {
            if (_placed.Count == 0) return;

            var items = new List<ItemInstance>(_placed.Keys);
            foreach (var item in items)
                Vacate(item, _placed[item]);
            foreach (var item in items)
                ItemRemoved?.Invoke(item);
            Changed?.Invoke();
        }

        // ---------- internals ----------

        private bool PassesValidators(Vector2Int cell, ItemInstance item)
        {
            for (int i = 0; i < _validators.Count; i++)
                if (!_validators[i].CanOccupy(this, cell, item))
                    return false;
            return true;
        }

        private void Occupy(ItemInstance item, Placement placement, CellShape shape)
        {
            foreach (var offset in shape)
                _cells[placement.Origin + offset] = item;
            _placed[item] = new PlacedItem(placement, shape);
        }

        private void Vacate(ItemInstance item, PlacedItem entry)
        {
            foreach (var offset in entry.Shape)
                _cells[entry.Placement.Origin + offset] = null;
            _placed.Remove(item);
        }

        private static IEnumerable<Vector2Int> Rectangle(int width, int height)
        {
            if (width < 1 || height < 1)
                throw new ArgumentOutOfRangeException(width < 1 ? nameof(width) : nameof(height), "Size must be at least 1.");

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    yield return new Vector2Int(x, y);
        }
    }
}
