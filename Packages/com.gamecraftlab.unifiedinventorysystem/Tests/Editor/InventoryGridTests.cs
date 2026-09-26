using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace GameCraftLab.UnifiedInventorySystem.Tests
{
    public class InventoryGridTests
    {
        private TestItems _items;

        [SetUp] public void SetUp() => _items = new TestItems();
        [TearDown] public void TearDown() => _items.DestroyAll();

        private static Vector2Int V(int x, int y) => new Vector2Int(x, y);

        // ---------- construction ----------

        [Test]
        public void Rectangle_HasAllCellsAndBounds()
        {
            var grid = new InventoryGrid(4, 3);

            Assert.AreEqual(12, grid.CellCount);
            Assert.AreEqual(new RectInt(0, 0, 4, 3), grid.Bounds);
            Assert.IsTrue(grid.Exists(V(3, 2)));
            Assert.IsFalse(grid.Exists(V(4, 0)));
        }

        [Test]
        public void IrregularGrid_BoundsCoverAllCells()
        {
            var grid = new InventoryGrid(ShapePattern.Parse("XX./..X"));
            Assert.AreEqual(new RectInt(0, 0, 3, 2), grid.Bounds);
            Assert.AreEqual(3, grid.CellCount);
        }

        [Test]
        public void EmptyCells_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => new InventoryGrid(new Vector2Int[0]));
        }

        // ---------- placing ----------

        [Test]
        public void TryPlace_OccupiesEveryShapeCell()
        {
            var grid = new InventoryGrid(4, 4);
            var item = _items.Item("l", "X./X./XX");

            Assert.AreEqual(PlacementResult.Success, grid.TryPlace(item, V(1, 1)));
            CollectionAssert.AreEquivalent(new[] { V(1, 1), V(1, 2), V(1, 3), V(2, 3) }, grid.GetOccupiedCells(item).ToArray());
            Assert.AreSame(item, grid.GetItemAt(V(2, 3)));
            Assert.IsNull(grid.GetItemAt(V(2, 1)), "hole inside the bounding box stays free");
            Assert.AreEqual(12, grid.CountFreeCells());
        }

        [Test]
        public void TryPlace_OutsideGrid_IsOutOfContainer()
        {
            var grid = new InventoryGrid(3, 3);
            Assert.AreEqual(PlacementResult.OutOfContainer, grid.TryPlace(_items.Item("bar", "XXX"), V(1, 0)));
            Assert.AreEqual(0, grid.ItemCount);
        }

        [Test]
        public void TryPlace_Overlap_IsBlocked()
        {
            var grid = new InventoryGrid(3, 3);
            grid.TryPlace(_items.Item("a", "XX"), V(0, 0));
            Assert.AreEqual(PlacementResult.Blocked, grid.TryPlace(_items.Item("b", "X/X"), V(1, 0)));
        }

        [Test]
        public void TryPlace_Twice_IsAlreadyPlaced()
        {
            var grid = new InventoryGrid(3, 3);
            var item = _items.Item("a");
            grid.TryPlace(item, V(0, 0));
            Assert.AreEqual(PlacementResult.AlreadyPlaced, grid.TryPlace(item, V(2, 2)));
        }

        [Test]
        public void IrregularGrid_ItemCannotCoverAMissingCell()
        {
            // XXX
            // X.X   <- (1,1) doesn't exist
            // XXX
            var grid = new InventoryGrid(ShapePattern.Parse("XXX/X.X/XXX"));

            Assert.AreEqual(PlacementResult.OutOfContainer, grid.TryPlace(_items.Item("sq", "XX/XX"), V(0, 0)));
            Assert.AreEqual(PlacementResult.Success, grid.TryPlace(_items.Item("ring", "XXX/X.X/XXX"), V(0, 0)));
        }

        [Test]
        public void ItemWithHole_LetsAnotherItemUseTheHole()
        {
            var grid = new InventoryGrid(3, 3);
            grid.TryPlace(_items.Item("ring", "XXX/X.X/XXX"), V(0, 0));
            Assert.AreEqual(PlacementResult.Success, grid.TryPlace(_items.Item("gem"), V(1, 1)));
        }

        // ---------- rotation ----------

        [Test]
        public void Rotation_LetsATallItemFitAWideGrid()
        {
            var grid = new InventoryGrid(3, 1);
            var bar = _items.Item("bar", "X/X/X");

            Assert.AreEqual(PlacementResult.OutOfContainer, grid.TryPlace(bar, V(0, 0), 0));
            Assert.AreEqual(PlacementResult.Success, grid.TryPlace(bar, V(0, 0), 1));
        }

        [Test]
        public void NonRotatable_OtherRotationIsInvalid()
        {
            var grid = new InventoryGrid(3, 3);
            var item = _items.Item("fixed", "XX", canRotate: false);
            Assert.AreEqual(PlacementResult.InvalidRotation, grid.TryPlace(item, V(0, 0), 1));
        }

        [Test]
        public void TryRotate_InPlace()
        {
            var grid = new InventoryGrid(3, 3);
            var bar = _items.Item("bar", "XXX");
            grid.TryPlace(bar, V(0, 0));

            Assert.AreEqual(PlacementResult.Success, grid.TryRotate(bar, 1));
            CollectionAssert.AreEquivalent(new[] { V(0, 0), V(0, 1), V(0, 2) }, grid.GetOccupiedCells(bar).ToArray());
            Assert.IsTrue(grid.IsFree(V(1, 0)));
        }

        // ---------- moving / removing ----------

        [Test]
        public void TryMove_CanOverlapItsOwnOldCells()
        {
            var grid = new InventoryGrid(4, 1);
            var bar = _items.Item("bar", "XXX");
            grid.TryPlace(bar, V(0, 0));

            Assert.AreEqual(PlacementResult.Success, grid.TryMove(bar, V(1, 0)));
            Assert.IsTrue(grid.IsFree(V(0, 0)));
            Assert.AreSame(bar, grid.GetItemAt(V(3, 0)));
        }

        [Test]
        public void TryMove_Blocked_LeavesItemWhereItWas()
        {
            var grid = new InventoryGrid(4, 1);
            var a = _items.Item("a");
            var b = _items.Item("b");
            grid.TryPlace(a, V(0, 0));
            grid.TryPlace(b, V(3, 0));

            Assert.AreEqual(PlacementResult.Blocked, grid.TryMove(a, V(3, 0)));
            Assert.AreSame(a, grid.GetItemAt(V(0, 0)));
        }

        [Test]
        public void TryMove_NotInGrid()
        {
            var grid = new InventoryGrid(2, 2);
            Assert.AreEqual(PlacementResult.NotInGrid, grid.TryMove(_items.Item("a"), new Placement(V(0, 0))));
        }

        [Test]
        public void Remove_FreesCells()
        {
            var grid = new InventoryGrid(2, 2);
            var item = _items.Item("sq", "XX/XX");
            grid.TryPlace(item, V(0, 0));

            Assert.IsTrue(grid.Remove(item));
            Assert.AreEqual(4, grid.CountFreeCells());
            Assert.IsFalse(grid.Contains(item));
            Assert.IsFalse(grid.Remove(item));
        }

        [Test]
        public void Clear_RemovesEverything()
        {
            var grid = new InventoryGrid(3, 3);
            grid.TryPlace(_items.Item("a"), V(0, 0));
            grid.TryPlace(_items.Item("b"), V(1, 1));
            int removed = 0;
            grid.ItemRemoved += _ => removed++;

            grid.Clear();

            Assert.AreEqual(0, grid.ItemCount);
            Assert.AreEqual(2, removed);
            Assert.AreEqual(9, grid.CountFreeCells());
        }

        // ---------- auto placement ----------

        [Test]
        public void TryAutoPlace_FillsRowByRow()
        {
            var grid = new InventoryGrid(2, 2);
            var a = _items.Item("a");
            var b = _items.Item("b");
            grid.TryAutoPlace(a);
            grid.TryAutoPlace(b);

            Assert.AreSame(a, grid.GetItemAt(V(0, 0)));
            Assert.AreSame(b, grid.GetItemAt(V(1, 0)));
        }

        [Test]
        public void TryAutoPlace_RotatesWhenNeeded()
        {
            var grid = new InventoryGrid(3, 1);
            var bar = _items.Item("bar", "X/X/X");

            Assert.AreEqual(PlacementResult.Success, grid.TryAutoPlace(bar));
            grid.TryGetPlacement(bar, out var p);
            Assert.AreEqual(1, p.Rotation);
        }

        [Test]
        public void TryAutoPlace_Full_IsNoFreeSpace()
        {
            var grid = new InventoryGrid(1, 1);
            grid.TryAutoPlace(_items.Item("a"));
            Assert.AreEqual(PlacementResult.NoFreeSpace, grid.TryAutoPlace(_items.Item("b")));
        }

        // ---------- preview ----------

        [Test]
        public void Preview_ReportsEveryCellAndMatchesCanPlace()
        {
            var grid = new InventoryGrid(3, 2);
            var blocker = _items.Item("blocker");
            grid.TryPlace(blocker, V(2, 0));
            var bar = _items.Item("bar", "XXX");
            var preview = new List<CellPreview>();
            var placement = new Placement(V(1, 0));

            var result = grid.GetPlacementPreview(bar, placement, preview);

            Assert.AreEqual(grid.CanPlace(bar, placement), result);
            Assert.AreEqual(PlacementResult.OutOfContainer, result);
            Assert.AreEqual(3, preview.Count, "no early exit");
            Assert.AreEqual(CellPreviewStatus.Valid, preview.Single(c => c.Cell == V(1, 0)).Status);
            var blocked = preview.Single(c => c.Cell == V(2, 0));
            Assert.AreEqual(CellPreviewStatus.Blocked, blocked.Status);
            Assert.AreSame(blocker, blocked.Blocker);
            Assert.AreEqual(CellPreviewStatus.OutOfContainer, preview.Single(c => c.Cell == V(3, 0)).Status);
        }

        [Test]
        public void Preview_IgnoresTheMovingItem()
        {
            var grid = new InventoryGrid(3, 1);
            var bar = _items.Item("bar", "XX");
            grid.TryPlace(bar, V(0, 0));
            var preview = new List<CellPreview>();

            Assert.AreEqual(PlacementResult.Success, grid.GetPlacementPreview(bar, new Placement(V(1, 0)), preview, ignore: bar));
        }

        // ---------- validators ----------

        [Test]
        public void Validator_CanReject()
        {
            var noTopRow = new DelegatePlacementValidator((g, cell, item) => cell.y > 0);
            var grid = new InventoryGrid(2, 2, noTopRow);

            Assert.AreEqual(PlacementResult.RejectedByRule, grid.TryPlace(_items.Item("a"), V(0, 0)));
            Assert.AreEqual(PlacementResult.Success, grid.TryPlace(_items.Item("b"), V(0, 1)));
        }

        [Test]
        public void Validator_IsNotAskedAboutMissingOrBlockedCells()
        {
            var asked = new List<Vector2Int>();
            var spy = new DelegatePlacementValidator((g, cell, item) => { asked.Add(cell); return true; });
            var grid = new InventoryGrid(2, 1, spy);
            grid.TryPlace(_items.Item("a"), V(0, 0));
            asked.Clear();

            grid.CanPlace(_items.Item("bar", "XXX"), V(0, 0));   // out of container
            grid.CanPlace(_items.Item("b"), V(0, 0));            // blocked

            CollectionAssert.IsEmpty(asked);
        }

        [Test]
        public void RemoveValidator_StopsRejecting()
        {
            var rejectAll = new DelegatePlacementValidator((g, c, i) => false);
            var grid = new InventoryGrid(1, 1, rejectAll);
            grid.RemoveValidator(rejectAll);
            Assert.AreEqual(PlacementResult.Success, grid.TryPlace(_items.Item("a"), V(0, 0)));
        }

        // ---------- events ----------

        [Test]
        public void Events_FireOncePerChange()
        {
            var grid = new InventoryGrid(3, 3);
            var item = _items.Item("a");
            int added = 0, removed = 0, moved = 0, changed = 0;
            Placement from = default, to = default;
            grid.ItemAdded += _ => added++;
            grid.ItemRemoved += _ => removed++;
            grid.ItemMoved += (_, f, t) => { moved++; from = f; to = t; };
            grid.Changed += () => changed++;

            grid.TryPlace(item, V(0, 0));
            grid.TryPlace(item, V(1, 1));          // fails: no event
            grid.TryMove(item, V(2, 2));
            grid.Remove(item);

            Assert.AreEqual((1, 1, 1, 3), (added, moved, removed, changed));
            Assert.AreEqual(new Placement(V(0, 0)), from);
            Assert.AreEqual(new Placement(V(2, 2)), to);
        }
    }
}
