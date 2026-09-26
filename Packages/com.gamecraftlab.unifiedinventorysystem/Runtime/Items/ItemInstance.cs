using System;

namespace GameCraftLab.UnifiedInventorySystem
{
    /// <summary>
    /// A concrete item living in the game (one sword, one stack of ammo).
    /// Where it sits is owned by the grid (see <see cref="Placement"/>), not by the item.
    /// Not sealed: games can derive to add per-instance data such as durability.
    /// </summary>
    public class ItemInstance
    {
        private int _stackCount;

        /// <summary>Unique id of this instance, used for save/load.</summary>
        public string InstanceId { get; }

        public ItemDefinition Definition { get; }

        public int StackCount => _stackCount;
        public int MaxStackSize => Definition.MaxStackSize;
        public int SpaceInStack => MaxStackSize - _stackCount;

        public event Action<ItemInstance> StackCountChanged;

        public ItemInstance(ItemDefinition definition, int stackCount = 1, string instanceId = null)
        {
            Definition = definition ? definition : throw new ArgumentNullException(nameof(definition));
            InstanceId = string.IsNullOrEmpty(instanceId) ? Guid.NewGuid().ToString("N") : instanceId;
            ValidateCount(stackCount);
            _stackCount = stackCount;
        }

        /// <exception cref="ArgumentOutOfRangeException">Count is below 1 or above the max stack size.</exception>
        public void SetStackCount(int count)
        {
            ValidateCount(count);
            if (count == _stackCount) return;
            _stackCount = count;
            StackCountChanged?.Invoke(this);
        }

        public CellShape GetShape(int rotation) => Definition.GetShape(rotation);

        public bool CanStackWith(ItemInstance other)
            => other != null && !ReferenceEquals(other, this) && other.Definition == Definition && Definition.IsStackable;

        public override string ToString() => $"{Definition.ItemId} x{_stackCount} ({InstanceId})";

        private void ValidateCount(int count)
        {
            if (count < 1 || count > MaxStackSize)
                throw new ArgumentOutOfRangeException(nameof(count), $"Stack count must be 1..{MaxStackSize} for {Definition.ItemId}.");
        }
    }
}
