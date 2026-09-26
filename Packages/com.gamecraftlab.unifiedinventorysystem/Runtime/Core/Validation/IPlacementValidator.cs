using UnityEngine;

namespace GameCraftLab.UnifiedInventorySystem
{
    /// <summary>
    /// The grid's only extension point for placement rules.
    /// The grid calls this for every cell an item would cover (cells that exist and are free);
    /// return false to block placement.
    /// </summary>
    public interface IPlacementValidator
    {
        bool CanOccupy(InventoryGrid grid, Vector2Int cell, ItemInstance item);
    }
}
