using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameCraftLab.UnifiedInventorySystem
{
    /// <summary>
    /// Stores a game-defined state per cell and delegates placement checks
    /// to the strategy registered for that state.
    /// Plug it into a grid as an IPlacementValidator.
    /// Rules are only checked when placing: changing a state never touches items already in the grid.
    /// </summary>
    public sealed class CellStateManager<TState> : IPlacementValidator where TState : struct, Enum
    {
        // Only cells whose state differs from the default are stored.
        private readonly Dictionary<Vector2Int, TState> _states = new Dictionary<Vector2Int, TState>();
        private readonly Dictionary<TState, ICellStrategy> _strategies = new Dictionary<TState, ICellStrategy>();

        public TState DefaultState { get; }

        /// <summary>Cells whose state isn't the default. Handy for saving.</summary>
        public IReadOnlyDictionary<Vector2Int, TState> NonDefaultStates => _states;

        public event Action<Vector2Int, TState> StateChanged;

        public CellStateManager(TState defaultState = default)
        {
            DefaultState = defaultState;
        }

        /// <summary>Registers (or replaces) the strategy for a state.</summary>
        public void Register(TState state, ICellStrategy strategy)
        {
            _strategies[state] = strategy ?? throw new ArgumentNullException(nameof(strategy));
        }

        /// <summary>Registers a lambda as the strategy for a state.</summary>
        public void Register(TState state, Func<InventoryGrid, Vector2Int, ItemInstance, bool> canOccupy)
            => Register(state, new DelegateCellStrategy(canOccupy));

        public void Unregister(TState state) => _strategies.Remove(state);

        public TState GetState(Vector2Int cell)
            => _states.TryGetValue(cell, out var s) ? s : DefaultState;

        public void SetState(Vector2Int cell, TState state)
        {
            if (EqualityComparer<TState>.Default.Equals(GetState(cell), state))
                return;

            if (EqualityComparer<TState>.Default.Equals(state, DefaultState))
                _states.Remove(cell);
            else
                _states[cell] = state;

            StateChanged?.Invoke(cell, state);
        }

        /// <summary>Resets every cell to the default state.</summary>
        public void ClearStates()
        {
            if (_states.Count == 0) return;
            var cells = new List<Vector2Int>(_states.Keys);
            _states.Clear();
            foreach (var cell in cells)
                StateChanged?.Invoke(cell, DefaultState);
        }

        bool IPlacementValidator.CanOccupy(InventoryGrid grid, Vector2Int cell, ItemInstance item)
        {
            // No strategy registered for this state = no restriction.
            return !_strategies.TryGetValue(GetState(cell), out var strategy)
                   || strategy.CanOccupy(grid, cell, item);
        }
    }
}
