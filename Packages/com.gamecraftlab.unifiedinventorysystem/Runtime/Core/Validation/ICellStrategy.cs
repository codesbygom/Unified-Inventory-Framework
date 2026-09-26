using UnityEngine;

namespace GameCraftLab.UnifiedInventorySystem
{
    /// <summary>
    /// Behaviour of one cell state, registered in a <see cref="CellStateManager{TState}"/>.
    /// Separate from IPlacementValidator on purpose: a strategy can't be handed to the grid
    /// directly, and a manager can't be registered as a strategy.
    /// </summary>
    public interface ICellStrategy
    {
        bool CanOccupy(InventoryGrid grid, Vector2Int cell, ItemInstance item);
    }
}
