# C033 field spawn and game-loop Play Mode validation — 2026-09-28

The current saved `FieldMapStructureTest` assets were copied into an isolated Unity 6000.3.11f1 project. Unity ran with a unique validation product identity, leaving the live editor and its save data untouched. The non-batch editor connected to Licensing Client and executed Play Mode probes in the copy.

## Observed results

1. All five resource zones reached their targets: 18 tree/thicket, 24 stone across two zones, 14 coal and 12 iron, totaling **68/68**. All seven monster zones reached **12/12**: deer 3, vampire bats 4, moss slimes 4 and one stone golem. Every live monster had a region limiter, and none began in the wrong forest/cave region.
2. In Play Mode, a probe placed each of the 12 monster Rigidbody2D objects across the forest/cave boundary and assigned outward velocity. On subsequent frames, **12/12** were back in the correct region and within three world units of their pre-test position. This validates forced-displacement recovery; organic AI pursuit and real combat knockback were not independently driven.
3. Six interactions with one spawned tree produced six item drops. Automatic pickup placed six wood in the field player's inventory. `FieldSceneTravel.BeginToTown` faded to `SampleScene`, and all six wood reached the smith chest.
4. In Play Mode, the actual `InventoryService` and `CraftingService` used that harvested wood to make three planks, a wood long blade, wood blocks, a handle and a wood sword. The sword was equipped, one spare plank was moved to the bag, and `FieldSceneTravel.BeginToField` faded back. The field player carried the plank and had the crafted wood sword in the live tool slots with damage **15**.

The probes called `TreeInteractable.Interact`, crafting-domain methods and scene-travel methods directly while Play Mode was running. They did not use physical keyboard/mouse input or drive the smithy UI and bed through its controls. They also did not wait for the 2–7 minute respawn timers, inspect rendered Game View art or build a player executable.

The isolated editor emitted startup exceptions from an incomplete copied Visual Scripting package cache and Unity SearchDatabase. The three gameplay probes completed with code 0; those editor exceptions mean this is not a clean, zero-error editor run. No exception in the inspected logs pointed to the field spawning, confinement, harvesting, crafting or travel scripts. The live project scene was not reloaded or played; its current `Golem Final Zone` position at x=81.6 was included in the isolated copy. No Notion text, commit or push was changed.
