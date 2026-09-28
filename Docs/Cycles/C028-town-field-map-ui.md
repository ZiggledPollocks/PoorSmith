# C028 town, forest and cave map UI — 2026-09-28

## Result

The existing town map button now appears beside the inventory button in the authored town HUD, including while the player is physically inside the smithy. The town map uses the saved town camera bounds and the current `visitedCells` exploration data. Entering the smithy no longer records its distant interior coordinates as a map discovery: `CampaignController.MapPosition` maps the indoor player to the smithy's town façade for discovery and the player marker. Gameplay position, camera and save position stay at the actual interior coordinates.

`FieldHud` now opens a matching, read-only exploration map for the same `FieldMapStructureTest` scene's forest and cave. A map button sits beside the bottom-left HUD controls. The field map uses the existing forest/cave camera bounds, saved visited cells and visible Tilemaps; it shows the current player marker, discovered cave/town entrances, assimilation statues and any future authored warp/theme entrances. All landmarks are hidden until their positions are explored. Both map views show only discovered terrain, refresh the player marker while open, close with Back or Escape and restore paused time/input. Existing warp travel confirmation remains in `CampaignUI`.

The [Notion map UI](https://app.notion.com/p/3bfda1bd22db8073a55df56f25c05c9e) specifies a button beside the bag, Back to close, an initially blank map, progressively revealed areas and player/altar/warp/theme-entrance icons. The current field scene has no warp or theme-entrance objects, so their icons appear only if those objects are later authored; no placeholder destination or warp mechanic was invented. No save schema, scene, prefab, terrain, map art or Notion page was changed.

## Verification

The isolated runtime C# assembly built with zero errors. Isolated Unity 6000.3.11f1 Play Mode passed the saved `SampleScene` town probe: the map button and explored smithy landmark existed, and the indoor player marker projected to the smithy façade instead of the interior. A second Play Mode probe passed the saved `FieldMapStructureTest` field button, explored forest and deep-cave cells, cave entrance marker, player marker, map opening/closing and time/input restoration. Unity's copied Library reported package-cache and Search startup exceptions unrelated to the passed map assertions. A rendered Game View, physical mouse/keyboard input and a player build were not checked. See [C028 evidence](../Evidence/C028-town-field-map-ui.json).
