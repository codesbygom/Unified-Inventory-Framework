using System;
using UnityEngine;

namespace GameCraftLab.UnifiedInventorySystem.UI
{
    /// <summary>
    /// Reports what the player did, in device-neutral terms. <see cref="InventoryInteraction"/> listens
    /// to every registered provider and turns these into pick up / rotate / drop.
    /// Write your own for touch, a custom input map, VR, AI tests...
    /// </summary>
    public interface IInventoryInputProvider
    {
        /// <summary>True when releasing the primary button drops (mouse drag). False for press-to-pick, press-to-drop (gamepad).</summary>
        bool DropOnRelease { get; }

        /// <summary>True when the provider reports a free screen position (mouse), so the held item follows it smoothly.</summary>
        bool IsPointerBased { get; }

        /// <summary>The cell under the cursor changed. View is null when the cursor isn't over any grid.</summary>
        event Action<IInventoryInputProvider, InventoryGridView, Vector2Int> CellHovered;

        /// <summary>Screen position of a pointer. Only pointer-based providers raise it.</summary>
        event Action<IInventoryInputProvider, Vector2> PointerMoved;

        event Action<IInventoryInputProvider> PrimaryPressed;
        event Action<IInventoryInputProvider> PrimaryReleased;
        event Action<IInventoryInputProvider> RotatePressed;
        event Action<IInventoryInputProvider> CancelPressed;
    }
}
