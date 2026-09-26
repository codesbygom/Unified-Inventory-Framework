using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace GameCraftLab.UnifiedInventorySystem.Tests
{
    public class InventorySystemTests
    {
        private TestItems _items;

        [SetUp] public void SetUp() => _items = new TestItems();
        [TearDown] public void TearDown() => _items.DestroyAll();

        [Test]
        public void AddItem_SplitsIntoStacksAndTopsUpExistingOnes()
        {
            var system = new InventorySystem();
            system.AddContainer("bag", new InventoryGrid(4, 4));
            var arrow = _items.Def("arrow", maxStack: 10);

            Assert.AreEqual(0, system.AddItem(arrow, 15));
            CollectionAssert.AreEquivalent(new[] { 10, 5 }, system.AllItems().Select(i => i.StackCount).ToArray());

            Assert.AreEqual(0, system.AddItem(arrow, 7));
            CollectionAssert.AreEquivalent(new[] { 10, 10, 2 }, system.AllItems().Select(i => i.StackCount).ToArray());
            Assert.AreEqual(22, system.CountOf(arrow));
        }

        [Test]
        public void AddItem_ReturnsWhatDidNotFit_AndSpillsIntoTheNextContainer()
        {
            var system = new InventorySystem();
            system.AddContainer("pocket", new InventoryGrid(1, 1));
            system.AddContainer("bag", new InventoryGrid(1, 1));
            var rock = _items.Def("rock");
            var created = new List<ItemInstance>();

            Assert.AreEqual(1, system.AddItem(rock, 3, created));
            Assert.AreEqual(2, created.Count);
            Assert.AreSame(system.GetContainer("pocket"), system.FindContainerOf(created[0]));
            Assert.AreSame(system.GetContainer("bag"), system.FindContainerOf(created[1]));
        }

        [Test]
        public void RemoveAmount_TakesFromSmallestStacksFirst()
        {
            var system = new InventorySystem();
            system.AddContainer("bag", new InventoryGrid(4, 4));
            var coin = _items.Def("coin", maxStack: 50);
            system.AddItem(coin, 50);
            system.AddItem(coin, 5);          // 50 + 5

            Assert.AreEqual(8, system.RemoveAmount(coin, 8));
            CollectionAssert.AreEquivalent(new[] { 47 }, system.AllItems().Select(i => i.StackCount).ToArray());
            Assert.AreEqual(47, system.RemoveAmount(coin, 100));
            Assert.AreEqual(0, system.AllItems().Count());
        }

        [Test]
        public void TryTransfer_BetweenGrids()
        {
            var system = new InventorySystem();
            var a = system.AddContainer("a", new InventoryGrid(2, 2));
            var b = system.AddContainer("b", new InventoryGrid(2, 2));
            var item = _items.Item("gem");
            a.TryPlace(item, Vector2Int.zero);

            Assert.AreEqual(PlacementResult.Success, system.TryTransfer(item, b, new Placement(Vector2Int.one)));
            Assert.IsFalse(a.Contains(item));
            Assert.AreSame(item, b.GetItemAt(Vector2Int.one));
        }

        [Test]
        public void TryTransfer_Failure_LeavesItemInSource()
        {
            var system = new InventorySystem();
            var a = system.AddContainer("a", new InventoryGrid(2, 2));
            var b = system.AddContainer("b", new InventoryGrid(1, 1));
            var bar = _items.Item("bar", "XX", canRotate: false);
            a.TryPlace(bar, Vector2Int.zero);

            Assert.AreEqual(PlacementResult.OutOfContainer, system.TryTransfer(bar, b, new Placement(Vector2Int.zero)));
            Assert.IsTrue(a.Contains(bar));
        }

        [Test]
        public void TryTransfer_SameGrid_IsAMove()
        {
            var system = new InventorySystem();
            var a = system.AddContainer("a", new InventoryGrid(3, 1));
            var item = _items.Item("gem");
            a.TryPlace(item, Vector2Int.zero);

            Assert.AreEqual(PlacementResult.Success, system.TryTransfer(item, a, new Placement(new Vector2Int(2, 0))));
            Assert.AreSame(item, a.GetItemAt(new Vector2Int(2, 0)));
        }

        [Test]
        public void AddContainer_DuplicateIdThrows()
        {
            var system = new InventorySystem();
            system.AddContainer("a", new InventoryGrid(1, 1));
            Assert.Throws<ArgumentException>(() => system.AddContainer("a", new InventoryGrid(1, 1)));
        }

        [Test]
        public void Changed_BubblesUpFromGrids()
        {
            var system = new InventorySystem();
            var grid = system.AddContainer("a", new InventoryGrid(1, 1));
            int changed = 0;
            system.Changed += () => changed++;

            grid.TryPlace(_items.Item("gem"), Vector2Int.zero);
            system.RemoveContainer("a");
            grid.Clear();                               // no longer part of the system

            Assert.AreEqual(2, changed);
        }

        // ---------- stacking helpers ----------

        [Test]
        public void Merge_PartialAndFull()
        {
            var grid = new InventoryGrid(2, 1);
            var potion = _items.Def("potion", maxStack: 5);
            var a = new ItemInstance(potion, 4);
            var b = new ItemInstance(potion, 3);
            grid.TryPlace(a, Vector2Int.zero);
            grid.TryPlace(b, Vector2Int.right);

            Assert.AreEqual(1, ItemStacking.Merge(b, a, grid, out var depleted));
            Assert.IsFalse(depleted);
            Assert.AreEqual((5, 2), (a.StackCount, b.StackCount));

            a.SetStackCount(1);
            Assert.AreEqual(2, ItemStacking.Merge(b, a, grid, out depleted));
            Assert.IsTrue(depleted);
            Assert.IsFalse(grid.Contains(b));
            Assert.AreEqual(3, a.StackCount);
        }

        [Test]
        public void Merge_DifferentItems_DoesNothing()
        {
            var a = new ItemInstance(_items.Def("x", maxStack: 5));
            var b = new ItemInstance(_items.Def("y", maxStack: 5));
            Assert.AreEqual(0, ItemStacking.Merge(a, b, null, out _));
        }

        [Test]
        public void Split_CreatesNewUnplacedStack()
        {
            var stack = new ItemInstance(_items.Def("ammo", maxStack: 30), 30);
            var half = ItemStacking.Split(stack, 12);

            Assert.AreEqual((18, 12), (stack.StackCount, half.StackCount));
            Assert.AreNotEqual(stack.InstanceId, half.InstanceId);
            Assert.Throws<ArgumentOutOfRangeException>(() => ItemStacking.Split(stack, 18));
        }

        [Test]
        public void StackCount_OutOfRangeThrows()
        {
            var item = new ItemInstance(_items.Def("ammo", maxStack: 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => item.SetStackCount(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => item.SetStackCount(11));
        }
    }
}
