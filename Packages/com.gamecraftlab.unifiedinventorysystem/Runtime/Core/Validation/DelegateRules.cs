using System;
using UnityEngine;

namespace GameCraftLab.UnifiedInventorySystem
{
    /// <summary>A cell strategy backed by a lambda, for one-liners.</summary>
    public sealed class DelegateCellStrategy : ICellStrategy
    {
        private readonly Func<InventoryGrid, Vector2Int, ItemInstance, bool> _canOccupy;

        public DelegateCellStrategy(Func<InventoryGrid, Vector2Int, ItemInstance, bool> canOccupy)
        {
            _canOccupy = canOccupy ?? throw new ArgumentNullException(nameof(canOccupy));
        }

        public bool CanOccupy(InventoryGrid grid, Vector2Int cell, ItemInstance item) => _canOccupy(grid, cell, item);
    }

    /// <summary>A placement validator backed by a lambda, for one-liners.</summary>
    public sealed class DelegatePlacementValidator : IPlacementValidator
    {
        private readonly Func<InventoryGrid, Vector2Int, ItemInstance, bool> _canOccupy;

        public DelegatePlacementValidator(Func<InventoryGrid, Vector2Int, ItemInstance, bool> canOccupy)
        {
            _canOccupy = canOccupy ?? throw new ArgumentNullException(nameof(canOccupy));
        }

        public bool CanOccupy(InventoryGrid grid, Vector2Int cell, ItemInstance item) => _canOccupy(grid, cell, item);
    }
}
