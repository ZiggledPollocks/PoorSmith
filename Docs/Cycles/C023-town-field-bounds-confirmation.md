# C023 town/field bounds and symmetric travel confirmation — 2026-09-28

## Result

`SampleScene` keeps its saved `Town/Bounds/camerBounds` for the Cinemachine confiner. Its town camera rectangle is centered at (-86, 69), size (94, 20). The existing `playerLeftBounds` and `playerRightBounds` colliders previously had large offsets, placing their actual collision surfaces outside the town. They now sit at x -133.5 and -38.5, centered on their transforms, with non-trigger Ground-layer walls. The town's ground and both forest entrances remain in place.

The same saved `FieldTravelPrompt` prefab is present in `SampleScene` and `FieldMapStructureTest`. Quick interaction at either town ForestIn gate or the field return gate opens `이동하시겠습니까?` with `Yes` and `No`. `No` restores time/input state and stays in the current scene; `Yes` uses the existing save, item-transfer and black-fade scene travel. The separate `NotionCampaign` in-scene gate retains its earlier behavior.

## Verification

An isolated Unity 6000.3.11f1 Play Mode test confirmed the authored town wall centers/layers, active town Cinemachine bounding shape, the exact popup text, `No` cancellation in both directions, `Yes` travel in both directions, gold preservation, two Wood transferred to the smithy chest, and restored time/input state after each fade. Saved scene/prefab authoring completed and the Play Mode round trip passed. See [C023 evidence](../Evidence/C023-town-field-travel.json).

The batch editor also emitted known copied-Library Visual Scripting package-cache and Unity Search indexing exceptions; these did not prevent scene loading or the completed assertions. Physical keyboard use, rendered popup/fade appearance, camera composition at every town edge and a player build were not checked. The original open Unity editor was not reloaded. No Notion text was changed and no commit or push was made.
