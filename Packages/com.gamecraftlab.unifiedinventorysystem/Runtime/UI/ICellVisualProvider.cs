using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameCraftLab.UnifiedInventorySystem.UI
{
    /// <summary>
    /// Lets the game decide how a cell looks (damaged, locked...) without the view knowing the game's states.
    /// </summary>
    public interface ICellVisualProvider
    {
        /// <summary>Return true and a color to override the default cell color.</summary>
        bool TryGetCellColor(InventoryGrid grid, Vector2Int cell, out Color color);

        /// <summary>Raise when colors may have changed; bound views refresh.</summary>
        event Action Changed;
    }

    /// <summary>Colors cells by the state stored in a <see cref="CellStateManager{TState}"/>.</summary>
    public sealed class CellStateColorProvider<TState> : ICellVisualProvider, IDisposable where TState : struct, Enum
    {
        private readonly CellStateManager<TState> _states;
        private readonly Dictionary<TState, Color> _colors = new Dictionary<TState, Color>();

        public event Action Changed;

        public CellStateColorProvider(CellStateManager<TState> states)
        {
            _states = states ?? throw new ArgumentNullException(nameof(states));
            _states.StateChanged += OnStateChanged;
        }

        public CellStateColorProvider<TState> Map(TState state, Color color)
        {
            _colors[state] = color;
            Changed?.Invoke();
            return this;
        }

        public bool TryGetCellColor(InventoryGrid grid, Vector2Int cell, out Color color)
            => _colors.TryGetValue(_states.GetState(cell), out color);

        public void Dispose() => _states.StateChanged -= OnStateChanged;

        private void OnStateChanged(Vector2Int cell, TState state) => Changed?.Invoke();
    }
}
