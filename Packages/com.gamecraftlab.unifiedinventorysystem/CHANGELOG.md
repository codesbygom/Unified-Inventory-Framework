# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-09-26

First reference implementation.

### Added
- `CellShape` (normalized, immutable, unique-rotation detection) and `ShapePattern` text format.
- `InventoryGrid`: placement, move/rotate, removal, auto-placement, per-cell placement preview, events.
- Placement rules: `IPlacementValidator`, `CellStateManager<TState>` + `ICellStrategy`, lambda helpers.
- `InventorySystem`: multiple containers, stacking-aware `AddItem`, `RemoveAmount`, `TryTransfer`.
- `ItemDefinition` / `ContainerDefinition` / `ItemDatabase` ScriptableObjects, `ItemInstance`, `ItemStacking`.
- JSON save/load (`InventorySerializer`) with load reports and custom item factories.
- uGUI layer: `InventoryGridView`, `ItemView`, `InventoryInteraction`, mouse and gamepad input providers.
- Editor: item shape painter inspector, container painter (inspector + window), item database builder, CSV/JSON importer.
- EditMode tests and the Basic Demo sample.