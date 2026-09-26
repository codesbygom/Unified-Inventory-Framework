using System;
using System.Collections.Generic;

namespace GameCraftLab.UnifiedInventorySystem
{
    /// <summary>
    /// Several grids side by side under string ids (backpack pockets, a Dredge hold + engine bay, player + chest...).
    /// Adds the things that span grids: auto-adding with stacking, transfers, counting, save/load by id.
    /// Not a singleton — create as many as you need.
    /// </summary>
    public sealed class InventorySystem
    {
        private readonly List<string> _order = new List<string>();
        private readonly Dictionary<string, InventoryGrid> _grids = new Dictionary<string, InventoryGrid>();

        /// <summary>Container ids in the order they were added (also the auto-add priority).</summary>
        public IReadOnlyList<string> ContainerIds => _order;

        /// <summary>Raised after any grid in the system changed, or a container was added/removed.</summary>
        public event Action Changed;

        // ---------- containers ----------

        public InventoryGrid AddContainer(string id, InventoryGrid grid)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Container id is empty.", nameof(id));
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (_grids.ContainsKey(id)) throw new ArgumentException($"Container '{id}' already exists.", nameof(id));

            _grids.Add(id, grid);
            _order.Add(id);
            grid.Changed += RaiseChanged;
            RaiseChanged();
            return grid;
        }

        public InventoryGrid AddContainer(ContainerDefinition definition, params IPlacementValidator[] validators)
        {
            if (!definition) throw new ArgumentNullException(nameof(definition));
            return AddContainer(definition.Id, new InventoryGrid(definition, validators));
        }

        public bool RemoveContainer(string id)
        {
            if (id == null || !_grids.TryGetValue(id, out var grid))
                return false;

            grid.Changed -= RaiseChanged;
            _grids.Remove(id);
            _order.Remove(id);
            RaiseChanged();
            return true;
        }

        public InventoryGrid GetContainer(string id)
            => id != null && _grids.TryGetValue(id, out var grid) ? grid : null;

        public bool TryGetContainer(string id, out InventoryGrid grid)
        {
            grid = GetContainer(id);
            return grid != null;
        }

        public string GetContainerId(InventoryGrid grid)
        {
            foreach (var id in _order)
                if (_grids[id] == grid)
                    return id;
            return null;
        }

        public IEnumerable<InventoryGrid> Grids
        {
            get
            {
                foreach (var id in _order)
                    yield return _grids[id];
            }
        }

        // ---------- items ----------

        /// <summary>The grid holding this item, or null.</summary>
        public InventoryGrid FindContainerOf(ItemInstance item)
        {
            foreach (var id in _order)
                if (_grids[id].Contains(item))
                    return _grids[id];
            return null;
        }

        public IEnumerable<ItemInstance> AllItems()
        {
            foreach (var id in _order)
                foreach (var item in _grids[id].Items)
                    yield return item;
        }

        public int CountOf(ItemDefinition definition)
        {
            int total = 0;
            foreach (var item in AllItems())
                if (item.Definition == definition)
                    total += item.StackCount;
            return total;
        }

        /// <summary>
        /// Adds <paramref name="amount"/> of an item: first tops up existing stacks, then places new stacks
        /// in the first grid with room (containers in order).
        /// </summary>
        /// <param name="created">Optional list that receives newly created instances.</param>
        /// <returns>How many could NOT be added (0 = everything fit).</returns>
        public int AddItem(ItemDefinition definition, int amount = 1, List<ItemInstance> created = null)
        {
            if (!definition) throw new ArgumentNullException(nameof(definition));
            if (amount <= 0) return 0;

            if (definition.IsStackable)
            {
                foreach (var item in AllItems())
                {
                    if (amount == 0) break;
                    if (item.Definition != definition || item.SpaceInStack == 0) continue;

                    int moved = Math.Min(amount, item.SpaceInStack);
                    item.SetStackCount(item.StackCount + moved);
                    amount -= moved;
                }
            }

            while (amount > 0)
            {
                var instance = new ItemInstance(definition, Math.Min(amount, definition.MaxStackSize));
                if (!TryAddItem(instance))
                    break;

                amount -= instance.StackCount;
                created?.Add(instance);
            }

            return amount;
        }

        /// <summary>Auto-places an existing instance in the first grid with room. No stacking.</summary>
        public bool TryAddItem(ItemInstance item, bool allowRotation = true)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (FindContainerOf(item) != null) return false;

            foreach (var id in _order)
                if (_grids[id].TryAutoPlace(item, allowRotation) == PlacementResult.Success)
                    return true;
            return false;
        }

        public bool RemoveItem(ItemInstance item)
        {
            var grid = FindContainerOf(item);
            return grid != null && grid.Remove(item);
        }

        /// <summary>Removes up to <paramref name="amount"/> of an item across all grids (smallest stacks first).</summary>
        /// <returns>How many were removed.</returns>
        public int RemoveAmount(ItemDefinition definition, int amount)
        {
            if (!definition || amount <= 0) return 0;

            var stacks = new List<ItemInstance>();
            foreach (var item in AllItems())
                if (item.Definition == definition)
                    stacks.Add(item);
            stacks.Sort((a, b) => a.StackCount.CompareTo(b.StackCount));

            int removed = 0;
            foreach (var stack in stacks)
            {
                if (removed == amount) break;

                int take = Math.Min(amount - removed, stack.StackCount);
                if (take == stack.StackCount)
                    RemoveItem(stack);
                else
                    stack.SetStackCount(stack.StackCount - take);
                removed += take;
            }
            return removed;
        }

        /// <summary>
        /// Moves an item to <paramref name="placement"/> in <paramref name="target"/>, from whatever grid holds it
        /// (or from nowhere). Same grid = move. Atomic: on failure the item stays where it was.
        /// </summary>
        public PlacementResult TryTransfer(ItemInstance item, InventoryGrid target, Placement placement)
            => TryTransfer(item, FindContainerOf(item), target, placement);

        /// <inheritdoc cref="TryTransfer(ItemInstance, InventoryGrid, Placement)"/>
        /// <remarks>Works with grids outside this system too.</remarks>
        public static PlacementResult TryTransfer(ItemInstance item, InventoryGrid source, InventoryGrid target, Placement placement)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (target == null) throw new ArgumentNullException(nameof(target));

            if (source == target)
                return target.TryMove(item, placement);

            var check = target.CanPlace(item, placement);
            if (check != PlacementResult.Success)
                return check;

            Placement original = default;
            bool hadSource = source != null && source.TryGetPlacement(item, out original);
            if (hadSource)
                source.Remove(item);

            var result = target.TryPlace(item, placement);
            if (result != PlacementResult.Success && hadSource)
                source.TryPlace(item, original); // a validator changed its mind between check and place — roll back

            return result;
        }

        private void RaiseChanged() => Changed?.Invoke();
    }
}
