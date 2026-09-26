using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace GameCraftLab.UnifiedInventorySystem.Tests
{
    public class CellStateManagerTests
    {
        private enum HoldCell { Normal, Damaged, Locked, WeaponOnly }

        private sealed class BlockAll : ICellStrategy
        {
            public bool CanOccupy(InventoryGrid grid, Vector2Int cell, ItemInstance item) => false;
        }

        private TestItems _items;

        [SetUp] public void SetUp() => _items = new TestItems();
        [TearDown] public void TearDown() => _items.DestroyAll();

        [Test]
        public void NoStrategy_NoRestriction()
        {
            var states = new CellStateManager<HoldCell>();
            states.SetState(new Vector2Int(0, 0), HoldCell.Damaged);   // no strategy registered for Damaged
            var grid = new InventoryGrid(2, 2, states);

            Assert.AreEqual(PlacementResult.Success, grid.TryPlace(_items.Item("a"), Vector2Int.zero));
        }

        [Test]
        public void RegisteredStrategy_BlocksOnlyItsCells()
        {
            var states = new CellStateManager<HoldCell>();
            states.Register(HoldCell.Damaged, new BlockAll());
            states.SetState(new Vector2Int(1, 0), HoldCell.Damaged);
            var grid = new InventoryGrid(3, 1, states);

            Assert.AreEqual(PlacementResult.RejectedByRule, grid.CanPlace(_items.Item("bar", "XX"), Vector2Int.zero));
            Assert.AreEqual(PlacementResult.Success, grid.CanPlace(_items.Item("gem"), new Vector2Int(2, 0)));
        }

        [Test]
        public void LambdaStrategy_CanLookAtTheItem()
        {
            var states = new CellStateManager<HoldCell>();
            states.Register(HoldCell.WeaponOnly, (g, c, item) => item.Definition.HasTag("weapon"));
            states.SetState(Vector2Int.zero, HoldCell.WeaponOnly);
            var grid = new InventoryGrid(1, 1, states);

            var fish = new ItemInstance(_items.Def("fish", tags: "fish"));
            var sword = new ItemInstance(_items.Def("sword", tags: "Weapon"));

            Assert.AreEqual(PlacementResult.RejectedByRule, grid.CanPlace(fish, Vector2Int.zero));
            Assert.AreEqual(PlacementResult.Success, grid.CanPlace(sword, Vector2Int.zero));
        }

        [Test]
        public void SetState_DefaultIsNotStored_AndEventsOnlyOnRealChanges()
        {
            var states = new CellStateManager<HoldCell>();
            var events = new List<(Vector2Int, HoldCell)>();
            states.StateChanged += (c, s) => events.Add((c, s));
            var cell = new Vector2Int(2, 3);

            states.SetState(cell, HoldCell.Locked);
            states.SetState(cell, HoldCell.Locked);   // no-op
            Assert.AreEqual(1, states.NonDefaultStates.Count);

            states.SetState(cell, HoldCell.Normal);
            Assert.AreEqual(0, states.NonDefaultStates.Count);
            Assert.AreEqual(HoldCell.Normal, states.GetState(cell));
            Assert.AreEqual(2, events.Count);
        }

        [Test]
        public void CustomDefaultState()
        {
            var states = new CellStateManager<HoldCell>(HoldCell.Locked);
            states.Register(HoldCell.Locked, new BlockAll());
            var grid = new InventoryGrid(2, 1, states);

            Assert.AreEqual(PlacementResult.RejectedByRule, grid.CanPlace(_items.Item("a"), Vector2Int.zero));
            states.SetState(Vector2Int.zero, HoldCell.Normal);   // "unlock" one cell
            Assert.AreEqual(PlacementResult.Success, grid.CanPlace(_items.Item("b"), Vector2Int.zero));
        }

        [Test]
        public void ChangingState_NeverTouchesPlacedItems()
        {
            var states = new CellStateManager<HoldCell>();
            states.Register(HoldCell.Damaged, new BlockAll());
            var grid = new InventoryGrid(2, 1, states);
            var fish = _items.Item("fish");
            grid.TryPlace(fish, Vector2Int.zero);

            states.SetState(Vector2Int.zero, HoldCell.Damaged);

            Assert.AreSame(fish, grid.GetItemAt(Vector2Int.zero), "what happens to the item is the game's call");
            Assert.IsTrue(grid.Remove(fish), "removing from a damaged cell is always allowed");
            Assert.AreEqual(PlacementResult.RejectedByRule, grid.TryPlace(fish, Vector2Int.zero));
        }

        [Test]
        public void ClearStates_ResetsAndNotifies()
        {
            var states = new CellStateManager<HoldCell>();
            states.SetState(Vector2Int.zero, HoldCell.Damaged);
            states.SetState(Vector2Int.one, HoldCell.Locked);
            int events = 0;
            states.StateChanged += (c, s) => events++;

            states.ClearStates();

            Assert.AreEqual(0, states.NonDefaultStates.Count);
            Assert.AreEqual(2, events);
        }

        [Test]
        public void Register_NullThrows()
        {
            var states = new CellStateManager<HoldCell>();
            Assert.Throws<ArgumentNullException>(() => states.Register(HoldCell.Damaged, (ICellStrategy)null));
        }
    }
}
