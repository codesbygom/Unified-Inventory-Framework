namespace GameCraftLab.UnifiedInventorySystem
{
    public enum PlacementResult
    {
        Success,
        OutOfContainer,   // at least one cell doesn't exist in this grid
        Blocked,          // overlaps another item
        RejectedByRule,   // an IPlacementValidator said no
        AlreadyPlaced,    // the item is already in this grid (use TryMove)
        NotInGrid,        // the item isn't in this grid (for moves)
        InvalidRotation,  // rotation index doesn't exist for this item
        NoFreeSpace       // auto-placement found no spot
    }
}
