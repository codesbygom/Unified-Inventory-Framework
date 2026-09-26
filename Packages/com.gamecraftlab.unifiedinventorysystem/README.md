# Unified Inventory System

One grid inventory engine for every style:

| Style | Example | How |
|---|---|---|
| One item = one cell | Skyrim-like lists on a grid | 1×1 shapes in a rectangle |
| Multi-cell items + rotation | Resident Evil 4 attaché case | rectangular shapes, `canRotate` |
| Irregular items in irregular containers | Dredge | any shape, any container cells, cell states (damage) |

## Architecture

```
            ┌──────────────────────────────┐
            │  InventorySystem             │  several grids by id, stacking, transfers
            └──────────────┬───────────────┘
                           ▼
            ┌──────────────────────────────┐
            │  InventoryGrid               │  geometry only: which cells exist, what covers them
            └──────────────┬───────────────┘
                           │ knows only this interface
                           ▼
            ┌──────────────────────────────┐
            │  «IPlacementValidator»       │  every other rule
            └──────┬───────────────┬───────┘
                   ▼               ▼
   CellStateManager<TState>     your own validators
   state → «ICellStrategy»
```

* **Core** (`GameCraftLab.UnifiedInventorySystem`) is pure C#: no MonoBehaviour, no UI. Everything is unit-tested.
* **UI** (`GameCraftLab.UnifiedInventorySystem.UI`) is a separate assembly on top of the core (uGUI).
* **Editor** tools live in their own editor-only assembly.
* Nothing is a singleton. Create as many grids/systems as you like and wire them however your game does (singleton, DI, plain references).

Coordinates: `(0,0)` is the top-left cell, **y grows downward**.

## Quick start

```csharp
using GameCraftLab.UnifiedInventorySystem;

var sword  = ItemDefinition.Create("sword",  ShapePattern.Parse("X/X/X"));
var arrows = ItemDefinition.Create("arrows", ShapePattern.Parse("X"), maxStackSize: 20);

var backpack = new InventoryGrid(8, 5);
var item = new ItemInstance(sword);

backpack.TryPlace(item, new Vector2Int(0, 0));            // PlacementResult.Success
backpack.TryRotate(item, 1);                              // now horizontal
backpack.TryMove(item, new Vector2Int(2, 3));
backpack.TryAutoPlace(new ItemInstance(arrows, 12));      // first free spot, rotating if needed
backpack.Remove(item);
```

In a real project you create `ItemDefinition` assets (**Create > Grid Inventory > Item Definition**) and paint their shape in the inspector.

### Results instead of exceptions

Everything a player can cause returns a `PlacementResult`:
`Success`, `OutOfContainer`, `Blocked`, `RejectedByRule`, `AlreadyPlaced`, `NotInGrid`, `InvalidRotation`, `NoFreeSpace`.
Exceptions are only thrown for programming errors (null arguments, invalid stack counts).

### Shapes

`ShapePattern` is a compact text format: rows separated by `/`, `X` = filled, `.` = empty.

```
"X./X./XX"   →   X.        (an L)
                 X.
                 XX
```

`CellShape.ComputeUniqueRotations()` detects symmetry: a square has 1 rotation, a bar 2, an L 4.
A placement's `Rotation` is the number of clockwise quarter turns.

## Irregular containers

```csharp
var hold = new InventoryGrid(ShapePattern.Parse(".XXXX./XXXXXX/.XX.XX"));
```

Or create a **Container Definition** asset and paint it (inspector, or **Tools > Grid Inventory > Container Shape Painter**), then `new InventoryGrid(definition)`.

## Placement rules

The grid itself only guarantees: *cells exist* and *items don't overlap*. Everything else is a rule you plug in.

### One-off rules

```csharp
var topRowLocked = new DelegatePlacementValidator((grid, cell, item) => cell.y > 0);
var grid = new InventoryGrid(6, 4, topRowLocked);
```

### Per-cell states (damaged, locked, weapon-only…)

Your game defines the states; the package maps each state to a strategy.

```csharp
public enum HoldCell { Normal, Damaged, Locked, WeaponOnly }

var states = new CellStateManager<HoldCell>();
states.Register(HoldCell.Damaged,    (g, c, i) => false);
states.Register(HoldCell.Locked,     (g, c, i) => false);
states.Register(HoldCell.WeaponOnly, (g, c, i) => i.Definition.HasTag("weapon"));

var hold = new InventoryGrid(8, 5, states);
states.SetState(new Vector2Int(2, 1), HoldCell.Damaged);
```

Rules are only checked **when placing**. Changing a state never touches items already in the grid —
what happens to them is your game's decision:

```csharp
var victim = hold.GetItemAt(cell);
if (victim != null && victim.Definition.HasTag("fish"))
    hold.Remove(victim);                   // Dredge: the fish is lost
```

For bigger strategies implement `ICellStrategy`; for a completely different rule system implement `IPlacementValidator`.

## Several containers, stacking

```csharp
var inventory = new InventorySystem();
inventory.AddContainer("backpack", new InventoryGrid(8, 5));
inventory.AddContainer("pocket",   new InventoryGrid(2, 2));

int leftover = inventory.AddItem(arrows, 55);   // tops up existing stacks, then fills free space
inventory.RemoveAmount(arrows, 10);
inventory.CountOf(arrows);
inventory.TryTransfer(item, inventory.GetContainer("pocket"), new Placement(Vector2Int.zero));
```

`ItemStacking.Merge` / `ItemStacking.Split` handle stacks explicitly.

## Save / load

```csharp
InventorySerializer.SaveToFile(inventory, path);

var report = InventorySerializer.LoadFromFile(inventory, path, itemDatabase);
if (report.HasWarnings) Debug.LogWarning(report);
```

* `ItemDatabase` (asset) maps ids to definitions. Fill it with **Tools > Grid Inventory > Rebuild Item Database**.
  For Addressables or mods implement `IItemDefinitionLookup` instead.
* Loading never throws on bad data: unknown items and missing containers are skipped and reported; items whose saved
  slot no longer fits are auto-placed.
* Pass a factory to `Restore` to rebuild your own `ItemInstance` subclass.
* Cell states are yours: save `CellStateManager.NonDefaultStates` alongside.

## UI (uGUI)

```csharp
var interaction = canvas.gameObject.AddComponent<InventoryInteraction>();   // one per screen

var view = viewObject.AddComponent<InventoryGridView>();
viewObject.AddComponent<MouseInputProvider>();          // before Bind
view.Bind(backpack, interaction);

canvas.gameObject.AddComponent<GamepadCursorInputProvider>().Interaction = interaction;
```

* `InventoryGridView` draws only cells that exist, keeps item views in sync through grid events and shows green/red previews.
* `InventoryInteraction` owns pick up / rotate / drop for every grid on screen, so dragging between grids just works.
  The held item stays in its grid until it's dropped — cancelling needs no rollback.
* Input is abstracted by `IInventoryInputProvider`. Mouse: drag, right click or **R** rotates, **Esc** cancels.
  Gamepad (Input System package): d-pad, **South** pick/drop, **North** rotate, **East** cancel, shoulders switch grid.
* Color cells by state with `view.CellVisuals = new CellStateColorProvider<HoldCell>(states).Map(HoldCell.Damaged, Color.red);`
* No prefabs required. Assign an `ItemView` prefab on the grid view to customize the look.

## Editor tools

| Where | What |
|---|---|
| Item Definition inspector | paint the shape, preview unique rotations |
| Container Definition inspector | paint which cells exist |
| Tools > Grid Inventory > Container Shape Painter | same, in a window, with "Save As" |
| Tools > Grid Inventory > Rebuild Item Database | scans the project for every Item Definition |
| Tools > Grid Inventory > Import Items from CSV or JSON | bulk create/update Item Definitions |

CSV header for the importer:

```
itemId,displayName,description,shape,canRotate,maxStackSize,tags,icon
sword,Sword,A sharp one,X/X/X,true,1,weapon,Assets/Icons/sword.png
arrow,Arrow,,X,false,20,ammo;stackable,
```

## Sample

Package Manager > Unified Inventory System > Samples > **Basic Demo** > Import. Add `BasicDemo` to an empty
GameObject in an empty scene and press Play: basic, RE4 and Dredge-style containers side by side.

## Tests

Window > General > Test Runner > **EditMode** > `GameCraftLab.UnifiedInventorySystem.Editor.Tests`.
