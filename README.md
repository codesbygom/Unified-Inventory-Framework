# Unified Inventory System

**One grid inventory engine for every style, built for Unity.**

![Unified Inventory System, concept illustration](docs/preview.png)

*Concept illustration. An in-engine screenshot is coming.*

A reusable, universal inventory system for Unity. The same core handles three very different inventory styles, so you pick the rules instead of rewriting the engine:

| Style | Example | How |
|---|---|---|
| One item = one cell | Skyrim-like lists on a grid | 1×1 shapes in a rectangle |
| Multi-cell items + rotation | Resident Evil 4 attaché case | rectangular shapes, `canRotate` |
| Irregular items in irregular containers | Dredge | any shape, any container cells, cell states such as damage |

## Features

- **Pure C# core** (`InventoryGrid`, `CellShape`, `InventorySystem`): no MonoBehaviour required, covered by EditMode unit tests.
- **Pluggable placement rules** through `IPlacementValidator`, plus `CellStateManager` with per-cell strategies (damaged, locked, weapon-only...).
- **Stacking, auto-placement and transfers** between several containers.
- **JSON save/load** with an item database; bad data is reported instead of throwing.
- **uGUI views** with mouse drag and drop, rotation, and a gamepad cursor (Input System).
- **Editor tools**: shape painter, container painter, item database builder, CSV/JSON importer.
- **No singletons**: wire it into your game however you like (DI, plain references, your own singleton).

## Install

In Unity, open **Window > Package Manager > + > Add package from git URL** and paste:

```
https://github.com/codesbygom/Unified-Inventory-Framework.git?path=/Packages/com.gamecraftlab.unifiedinventorysystem
```

The package targets Unity 6 (`6000.6`) and depends on `com.unity.ugui`.

## Quick start

```csharp
using GameCraftLab.UnifiedInventorySystem;

var sword  = ItemDefinition.Create("sword",  ShapePattern.Parse("X/X/X"));
var arrows = ItemDefinition.Create("arrows", ShapePattern.Parse("X"), maxStackSize: 20);

var backpack = new InventoryGrid(8, 5);
var item = new ItemInstance(sword);

backpack.TryPlace(item, new Vector2Int(0, 0));        // PlacementResult.Success
backpack.TryRotate(item, 1);                          // now horizontal
backpack.TryMove(item, new Vector2Int(2, 3));
backpack.TryAutoPlace(new ItemInstance(arrows, 12));  // first free spot, rotating if needed
```

Shapes use a compact text format: rows separated by `/`, `X` filled, `.` empty. Everything a player can cause returns a `PlacementResult` instead of throwing.

## Try the demo

Package Manager > Unified Inventory System > Samples > **Basic Demo** > Import. Add `BasicDemo` to an empty GameObject in an empty scene and press Play: basic, RE4-style and Dredge-style containers side by side.

## Documentation

The full guide (architecture, irregular containers, placement rules, save/load, UI, editor tools, CSV format) is in the [package README](Packages/com.gamecraftlab.unifiedinventorysystem/README.md). Release notes are in the [changelog](Packages/com.gamecraftlab.unifiedinventorysystem/CHANGELOG.md).

## Tests

Window > General > Test Runner > **EditMode** > `GameCraftLab.UnifiedInventorySystem.Editor.Tests`.

## Status

Version 0.1.0, the first reference implementation. The API may still change.
