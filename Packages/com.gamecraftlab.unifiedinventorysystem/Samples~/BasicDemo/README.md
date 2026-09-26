# Basic Demo

1. Create an empty scene.
2. Add an empty GameObject and attach `BasicDemo`.
3. Press Play.

Three containers driven by the same engine:

* **Basic** — every item is one cell, stacks of potions and coins.
* **Attaché case (RE4)** — multi-cell weapons, rotation.
* **Hold (Dredge)** — irregular hull and fish shapes. Press **D** to damage a random cell: damaged cells refuse items
  and a fish sitting there is lost (that decision lives in the demo, not in the package). **F** repairs.

Mouse: drag to move, right click or **R** to rotate while dragging, **Esc** to cancel. Drop onto the same kind of
stackable item to merge stacks.
Gamepad: d-pad moves, **A** picks up / drops, **Y** rotates, **B** cancels, **LB / RB** switch container.
**S** saves to `Application.persistentDataPath`, **L** loads.
