# C043 field warp stones and offering statue placement — 2026-09-29

## Design source and behavior

Read-only Notion references: [gathering-field revisions](https://app.notion.com/p/3dada1bd22db803999ecdb1d5faaa535), [exploration system](https://app.notion.com/p/3bfda1bd22db80d6a704e7beb7f2cecb), and [return UI](https://app.notion.com/p/3bcda1bd22db80e4a08ec016be7c4af3). The sketch places a surface warp, an upper cave warp near the unlock reward, and two deep-cave warps. The upper assimilation statue sits just inside the cave mouth; the middle and deep statues remain at their authored locations.

`FieldMapStructureTest.unity` now contains four saved instances of the existing warp prefabs, one saved `wind_crystal_chest` instance, and all three pre-existing `AssissZone` prefab instances. They are grouped under `Field Interactions`. The chest grants the persistent `CampaignState.warpUnlocked` flag once and becomes unavailable after its stable ID enters `openedChests`. A field warp gains a persistent entry in `unlockedWarps` when first used. All IDs are unique. No save format change or Notion edit was made.

In the gathering scene, F interaction opens a dedicated warp map after the crystal is acquired. The map shows the player's position, activated warp destinations and a home icon. Hover enlarges a destination. Selecting it opens Yes/No confirmation; No returns to the map, Yes closes both panels and fades to the selected field position or uses the existing field-to-town transition. The ordinary exploration map remains read-only. The earlier user instruction that tool-independent objects use F only takes precedence over the older Notion suggestion to click warp stones.

## Verification

- Unity 6000.3.11f1 isolated runtime assembly compiled with zero errors; pre-existing warnings remain.
- Isolated Editor loaded the saved field scene and verified four warp prefab instances, one reward chest, three statues, eight distinct persistent IDs, required colliders and the field HUD (`FIELD_WARP_VALIDATED`).
- Isolated Play Mode verified one-time crystal reward, activation persistence, destination filtering, confirmation cancel, same-scene fade arrival, restored time scale and input, plus home warp to `SampleScene` with the crystal retained (`C043_WARP_RUNTIME_PASS`, `C043_WARP_HOME_PASS`).
- Five Play Mode captures (all four warp regions and the cave-mouth statue) were inspected. The eight field landmark instances needed sorting-order overrides to appear in front of terrain.

Physical keyboard/mouse input, full path traversal and player build were not exercised. The copied Editor also emitted its existing SearchDatabase startup exception, unrelated to these checks. No commit or push.
