# C032 authored field spawns and region confinement — 2026-09-28

## Scope

`FieldMapStructureTest` keeps the user's existing resource/deer BoxCollider2D zone transforms and adds one spawner to each. TreeZone alternates tree and thicket (18 targets); both stone zones have 12 targets; coal and iron zones have 14 and 12. All five resource prefabs were resized to approximately the saved player height, with the cave ore appearances kept distinct. Each resource slot begins its respawn delay when destroyed. Tree/thicket/stone use 120 seconds, coal 240, iron 300. The scene also has an `ItemDropSpawner` so harvested resource data can produce existing pickup prefabs.

Later C035 update: the five prefab renderer heights are now 3.36–3.40 world units against the saved player's 2.2168; this supersedes the earlier approximately player-height sizing. Spawned resource components receive the scene `ItemDropSpawner` directly, including TreeZone's thicket. Isolated Play Mode retained 68/68 resources and confirmed tree/thicket drops; the original C032 verification limits below describe the earlier run.

The deer zone targets three deer. Six cave zones target four vampire bats, four moss slimes and one stone golem. The one golem slot is the only golem spawn in this scene. Monster slot timers start after death, advance only while the original spawn position is off camera, pause while visible, and use 120 seconds for deer, 180 for bat/slime and 420 for golem. Spawn positions must be off camera, inside their BoxCollider2D, clear of solid terrain and separated from living monsters. Valid candidates are sampled against actual cave corridor surfaces, not the full box interior.

## Forest/cave boundary

`CaveEntranceBackgroundTransition.IsCaveWorldPosition` exposes the existing player background boundary for arbitrary world coordinates: cave begins on the entrance's right side or below the configured cave depth. It does not change player background behavior. The field spawner checks all four visual-bounds corners before creation. Each spawned deer receives `FieldMonsterRegionLimiter2D` in forest mode; bats, moss slimes and the golem receive cave mode. The limiter checks every enabled, non-trigger collider corner after movement and knockback, restores the last allowed position if a monster crosses, and blocks velocity predicted to cross at the next physics step. It confines monsters to forest/cave regions rather than to their small spawn boxes. If a prefab collider is outside its assigned region on creation, the spawner rejects that instance.

## Verification and limits

The changed C# files passed a targeted .NET compile against installed Unity DLLs with zero errors. Scene/prefab GUIDs, serialized counts, forest/cave assignments and the five resource-to-pickup chains passed static checks. Cave-mask sampling found traversable surface candidates for all cave spawn boxes. The code path from field player tools/inventory through pickups, return gate and smithing bootstrap is present.

The isolated Unity editor could not connect to Licensing Client and never entered Play Mode. Therefore actual spawned counts, AI boundary collisions, damage/harvest interactions, round-trip inventory transfer and the complete game loop are **not runtime verified**. Manual follow-up: open the saved field scene after preserving unsaved editor work, watch both borders while luring/knocking each species, mine each resource, return to town, craft/equip, and depart again. No Notion text, commit or push was changed.
