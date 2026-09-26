using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace GameCraftLab.UnifiedInventorySystem.Tests
{
    public class InventorySerializerTests
    {
        private TestItems _items;
        private ItemDefinition _sword, _arrow, _gem;
        private ItemDatabase _db;

        [SetUp]
        public void SetUp()
        {
            _items = new TestItems();
            _sword = _items.Def("sword", "X/X/X");
            _arrow = _items.Def("arrow", maxStack: 20);
            _gem = _items.Def("gem");
            _db = ItemDatabase.Create(new[] { _sword, _arrow, _gem });
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_db);
            _items.DestroyAll();
        }

        private static InventorySystem NewSystem()
        {
            var system = new InventorySystem();
            system.AddContainer("backpack", new InventoryGrid(4, 3));
            system.AddContainer("pocket", new InventoryGrid(2, 1));
            return system;
        }

        [Test]
        public void RoundTrip_ThroughJson_RestoresEverything()
        {
            var original = NewSystem();
            var sword = new ItemInstance(_sword);
            var arrows = new ItemInstance(_arrow, 17);
            var gem = new ItemInstance(_gem);
            original.GetContainer("backpack").TryPlace(sword, new Vector2Int(1, 0), 1);
            original.GetContainer("backpack").TryPlace(arrows, new Vector2Int(0, 2));
            original.GetContainer("pocket").TryPlace(gem, new Vector2Int(1, 0));

            var json = InventorySerializer.ToJson(InventorySerializer.Capture(original));
            var loaded = NewSystem();
            var report = InventorySerializer.Restore(loaded, InventorySerializer.FromJson(json), _db);

            Assert.IsFalse(report.HasWarnings, report.ToString());
            Assert.AreEqual(3, report.ItemsRestored);

            var loadedSword = loaded.GetContainer("backpack").GetItemAt(new Vector2Int(3, 0));
            Assert.AreEqual(sword.InstanceId, loadedSword.InstanceId);
            loaded.GetContainer("backpack").TryGetPlacement(loadedSword, out var p);
            Assert.AreEqual(new Placement(new Vector2Int(1, 0), 1), p);

            Assert.AreEqual(17, loaded.GetContainer("backpack").GetItemAt(new Vector2Int(0, 2)).StackCount);
            Assert.AreSame(_gem, loaded.GetContainer("pocket").GetItemAt(new Vector2Int(1, 0)).Definition);
        }

        [Test]
        public void Restore_ReplacesExistingContents()
        {
            var system = NewSystem();
            system.GetContainer("pocket").TryPlace(new ItemInstance(_gem), Vector2Int.zero);
            var empty = InventorySerializer.Capture(NewSystem());

            InventorySerializer.Restore(system, empty, _db);

            Assert.AreEqual(0, system.AllItems().Count());
        }

        [Test]
        public void Restore_UnknownItemAndContainer_AreReportedNotThrown()
        {
            var data = new InventorySaveData();
            data.containers.Add(new ContainerSaveData
            {
                containerId = "backpack",
                items = { new ItemSaveData { itemId = "deleted-item" }, new ItemSaveData { itemId = "gem" } }
            });
            data.containers.Add(new ContainerSaveData { containerId = "old-chest", items = { new ItemSaveData { itemId = "gem" } } });

            var report = InventorySerializer.Restore(NewSystem(), data, _db);

            Assert.AreEqual(1, report.ItemsRestored);
            Assert.AreEqual(2, report.Warnings.Count);
        }

        [Test]
        public void Restore_SlotNoLongerValid_AutoPlacesWithWarning()
        {
            var data = new InventorySaveData();
            data.containers.Add(new ContainerSaveData
            {
                containerId = "pocket",
                items = { new ItemSaveData { itemId = "gem", x = 9, y = 9 } }   // container shrank since the save
            });
            var system = NewSystem();

            var report = InventorySerializer.Restore(system, data, _db);

            Assert.AreEqual(1, report.ItemsRestored);
            Assert.AreEqual(1, report.Warnings.Count);
            Assert.AreSame(_gem, system.GetContainer("pocket").GetItemAt(Vector2Int.zero).Definition);
        }

        [Test]
        public void Restore_CustomFactory_BuildsSubclasses()
        {
            var original = NewSystem();
            original.GetContainer("pocket").TryPlace(new ItemInstance(_gem), Vector2Int.zero);
            var data = InventorySerializer.Capture(original);
            var loaded = NewSystem();

            InventorySerializer.Restore(loaded, data, _db, (def, saved) => new DurableItem(def, saved.instanceId));

            Assert.IsInstanceOf<DurableItem>(loaded.GetContainer("pocket").GetItemAt(Vector2Int.zero));
        }

        private sealed class DurableItem : ItemInstance
        {
            public DurableItem(ItemDefinition def, string id) : base(def, 1, id) { }
        }
    }
}
