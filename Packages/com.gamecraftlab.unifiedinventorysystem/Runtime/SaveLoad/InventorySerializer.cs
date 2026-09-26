using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GameCraftLab.UnifiedInventorySystem
{
    /// <summary>What went wrong while loading. Loading never throws for bad data; it skips and reports.</summary>
    public sealed class LoadReport
    {
        private readonly List<string> _warnings = new List<string>();

        public IReadOnlyList<string> Warnings => _warnings;
        public bool HasWarnings => _warnings.Count > 0;
        public int ItemsRestored { get; internal set; }

        internal void Warn(string message) => _warnings.Add(message);

        public override string ToString()
            => HasWarnings ? $"{ItemsRestored} item(s) restored, {_warnings.Count} warning(s):\n- " + string.Join("\n- ", _warnings)
                           : $"{ItemsRestored} item(s) restored.";
    }

    /// <summary>Converts inventories to/from <see cref="InventorySaveData"/> and JSON.</summary>
    public static class InventorySerializer
    {
        public const int CurrentVersion = 1;

        /// <summary>Creates the instance for a saved item. Swap it to rebuild your own ItemInstance subclass.</summary>
        public delegate ItemInstance ItemFactory(ItemDefinition definition, ItemSaveData data);

        public static readonly ItemFactory DefaultFactory =
            (def, data) => new ItemInstance(def, Mathf.Clamp(data.stackCount, 1, def.MaxStackSize), data.instanceId);

        // ---------- capture ----------

        public static InventorySaveData Capture(InventorySystem system)
        {
            if (system == null) throw new ArgumentNullException(nameof(system));

            var data = new InventorySaveData();
            foreach (var id in system.ContainerIds)
                data.containers.Add(Capture(id, system.GetContainer(id)));
            return data;
        }

        public static ContainerSaveData Capture(string containerId, InventoryGrid grid)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));

            var data = new ContainerSaveData { containerId = containerId };
            foreach (var item in grid.Items)
            {
                grid.TryGetPlacement(item, out var p);
                data.items.Add(new ItemSaveData
                {
                    itemId = item.Definition.ItemId,
                    instanceId = item.InstanceId,
                    stackCount = item.StackCount,
                    x = p.Origin.x,
                    y = p.Origin.y,
                    rotation = p.Rotation
                });
            }
            return data;
        }

        // ---------- restore ----------

        /// <summary>
        /// Clears every container of <paramref name="system"/> that appears in the data and refills it.
        /// Containers in the system but not in the data are left untouched.
        /// </summary>
        public static LoadReport Restore(InventorySystem system, InventorySaveData data, IItemDefinitionLookup items,
            ItemFactory factory = null)
        {
            if (system == null) throw new ArgumentNullException(nameof(system));
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (items == null) throw new ArgumentNullException(nameof(items));

            var report = new LoadReport();
            if (data.version > CurrentVersion)
                report.Warn($"Save version {data.version} is newer than supported version {CurrentVersion}.");

            foreach (var container in data.containers)
            {
                var grid = system.GetContainer(container.containerId);
                if (grid == null)
                {
                    report.Warn($"Container '{container.containerId}' not found; its {container.items.Count} item(s) were skipped.");
                    continue;
                }
                RestoreInto(grid, container, items, factory, report);
            }
            return report;
        }

        /// <summary>Clears <paramref name="grid"/> and fills it from <paramref name="data"/>.</summary>
        public static LoadReport Restore(InventoryGrid grid, ContainerSaveData data, IItemDefinitionLookup items,
            ItemFactory factory = null)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (items == null) throw new ArgumentNullException(nameof(items));

            var report = new LoadReport();
            RestoreInto(grid, data, items, factory, report);
            return report;
        }

        private static void RestoreInto(InventoryGrid grid, ContainerSaveData data, IItemDefinitionLookup items,
            ItemFactory factory, LoadReport report)
        {
            factory = factory ?? DefaultFactory;
            grid.Clear();

            foreach (var saved in data.items)
            {
                if (!items.TryGet(saved.itemId, out var def) || !def)
                {
                    report.Warn($"[{data.containerId}] Unknown item id '{saved.itemId}'.");
                    continue;
                }

                var instance = factory(def, saved);
                var result = grid.TryPlace(instance, new Placement(new Vector2Int(saved.x, saved.y), saved.rotation));
                if (result == PlacementResult.Success)
                {
                    report.ItemsRestored++;
                    continue;
                }

                // Layout changed since the save (shape edited, container resized...): keep the item if it fits elsewhere.
                if (grid.TryAutoPlace(instance) == PlacementResult.Success)
                {
                    report.ItemsRestored++;
                    report.Warn($"[{data.containerId}] '{saved.itemId}' couldn't go back to ({saved.x},{saved.y}) ({result}); auto-placed instead.");
                }
                else
                {
                    report.Warn($"[{data.containerId}] '{saved.itemId}' didn't fit anywhere ({result}); dropped.");
                }
            }
        }

        // ---------- JSON / files ----------

        public static string ToJson(InventorySaveData data, bool prettyPrint = true) => JsonUtility.ToJson(data, prettyPrint);

        public static InventorySaveData FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("JSON is empty.", nameof(json));
            return JsonUtility.FromJson<InventorySaveData>(json) ?? new InventorySaveData();
        }

        public static void SaveToFile(InventorySystem system, string path, bool prettyPrint = true)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, ToJson(Capture(system), prettyPrint));
        }

        public static LoadReport LoadFromFile(InventorySystem system, string path, IItemDefinitionLookup items,
            ItemFactory factory = null)
            => Restore(system, FromJson(File.ReadAllText(path)), items, factory);
    }
}
