using System;

namespace GameCraftLab.UnifiedInventorySystem
{
    /// <summary>Stack helpers. Stacking lives outside the grid: the grid only deals with space.</summary>
    public static class ItemStacking
    {
        /// <summary>
        /// Moves as much as fits from <paramref name="source"/> into <paramref name="target"/>.
        /// When the whole source fits, the source is removed from <paramref name="sourceGrid"/> (if given)
        /// and <paramref name="sourceDepleted"/> is true: discard that instance.
        /// </summary>
        /// <returns>The amount moved.</returns>
        public static int Merge(ItemInstance source, ItemInstance target, InventoryGrid sourceGrid, out bool sourceDepleted)
        {
            sourceDepleted = false;
            if (source == null || target == null || !source.CanStackWith(target))
                return 0;

            int moved = Math.Min(source.StackCount, target.SpaceInStack);
            if (moved <= 0)
                return 0;

            target.SetStackCount(target.StackCount + moved);

            if (moved == source.StackCount)
            {
                sourceDepleted = true;
                sourceGrid?.Remove(source);
            }
            else
            {
                source.SetStackCount(source.StackCount - moved);
            }
            return moved;
        }

        /// <summary>Takes <paramref name="amount"/> out of a stack into a new, unplaced instance.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Amount must leave at least one in the source.</exception>
        public static ItemInstance Split(ItemInstance source, int amount)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (amount < 1 || amount >= source.StackCount)
                throw new ArgumentOutOfRangeException(nameof(amount), $"Split amount must be 1..{source.StackCount - 1}.");

            source.SetStackCount(source.StackCount - amount);
            return new ItemInstance(source.Definition, amount);
        }
    }
}
