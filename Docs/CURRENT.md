# Field player camera initial alignment — 2026-09-29

`FieldMapStructureTest` Main Camera began 31.09 world units left of its player and virtual camera, unlike `SampleScene`'s crafting/town player camera. Its saved x position now matches the field player/virtual camera, giving both scenes the same initial player-relative offset `(0, 2.08)` at orthographic size 8. Composer, look-ahead, confiner and camera size were already equivalent and remain unchanged. An isolated Unity Editor check loaded both saved scenes and passed the offset/size comparison; an isolated field Play Mode check also passed the player-relative `(0, 2.08)` framing at startup. Manual rendered travel/transition review and player build remain open. No commit/push.

# Tool reach adjustment — 2026-09-29

The three `tools.csv` reach values and matching `ToolData` assets increased by 0.2 world units: Sword 2.5→2.7, Axe 2.05→2.25 and Pickaxe 2.25→2.45. No IDs, damage or attack speed changed. The final CSV was imported in an isolated Unity 6000.3.11f1 copy: 3 tool assets loaded, 3,182 recipe checks passed, and Editor exited successfully. Physical play feel and hit-range traversal remain unverified. No commit/push.

# C051 inventory drag transfer — 2026-09-29

Town bag/chest are visible together and town delivery plus blacksmith bag/chest slots have cursor-follow drag previews and matching destination highlight/release checks. Whole stacks still move through `InventoryService`; old click/quantity/reorder flows remain. Isolated C# and Unity Editor compilation passed, as did focused Unity Play Mode pointer-event probes for valid and invalid releases at normal and zero time scale. Physical mouse use in the saved screens, rendered layout and player build remain open. See [C051](Cycles/C051-inventory-drag-transfer.md). No scene/prefab/save migration or commit/push.

# C050 smithy script study notes — 2026-09-29

The Obsidian code-analysis area has a `Scripts/Blacksmith` folder with 34 per-script pages: all 28 smithy C# files, two shared inventory helpers and four field inventory/item/tool contracts. Roslyn and a separate documentation check confirmed 341 method/constructor/property sections, 34 source hashes and relative links at the C050 snapshot. C051 subsequently changed three of those scripts; their pages now mark the earlier hash and function analysis as a snapshot. C050 was a static documentation task; Unity Play Mode, Inspector wiring and build were not run for that task. See [C050](Cycles/C050-smithy-script-explanations.md). No commit/push.

# JinHo script readability pass — 2026-09-29

The C# files under `Assets/JinHo/Scripts` and `Assets/JinHo/Crafting/Scripts` were reviewed for comments and indentation. Three inventory-related files (`InventoryService.cs`, `InventorySlotView.cs`, `ItemDropTarget.cs`) were excluded because they are being edited separately. Responsibility comments were added to 38 files; Roslyn formatting was applied to 83 selected files, including expanded statement layout in 13 crafting scripts. A Roslyn token comparison against the pre-edit snapshot passed for all 83 files, so executable C# tokens are unchanged. No Unity Play Mode or player build was run for this nonfunctional edit.

# C049 shared inventory ledger — 2026-09-29

Field and smithy inventories now use one stack-mutation core while preserving their existing serialized types, bag/chest roles, item IDs and version-one saves. Isolated Unity inventory ownership, weight, overflow and JSON checks passed, along with 3,182 crafting assertions. Full scene input/UI and build remain open. See [C049](Cycles/C049-shared-inventory-ledger.md). No commit/push.

# C048 unified item and tool CSV source — 2026-09-29

The town–field–smithy loop now imports gameplay item/tool values from six CSV files, including all 16 field items and the three base tool templates. Existing IDs, GUIDs, saves, scene references and Unity art remain. Isolated Unity import, 3,182 recipe assertions and a focused item/weight/tool bridge probe passed; physical play and build remain open. See [C048](Cycles/C048-unified-item-tool-csv.md). No commit/push.

# C046 field death returns to town main menu — 2026-09-29

Fatal damage in `FieldMapStructureTest` now immediately saves an empty carried bag, the existing 4% gold penalty, full health and the authored town respawn. The death screen waits for **메인 화면으로 나가기** instead of automatically returning to gameplay; its button uses the existing field→town fade and opens the town main menu. **게임 시작** resumes in town at normal speed. Existing chest, equipment, item IDs, version-1 save format, normal travel, and non-field death behavior remain. An isolated Unity Play Mode probe passed the button/menu/Play flow, save payload and loss checks; physical pointer input, rendered fade, process restart and build remain open. See [C046](Cycles/C046-field-death-to-town-menu.md) and [evidence](Evidence/C046-field-death.json). No commit/push.

# C045 monster Aseprite sprites — 2026-09-29

The user-provided `slime.aseprite` and `bat.aseprite` frames now drive the sprites and state animations of both base and campaign spawn prefabs for MossSlime and VampireBat. The user chose each source's `damage` tag for death because neither has a death tag. Import density keeps first-frame widths near their prior PNG size without changing prefab root scales. Existing AI, stats, collider fitting, drops and spawn identities remain. Isolated Unity compiled and Play Mode passed frame/state checks on all four prefabs plus all affected field spawn targets (slime 4/4, bat 4/4). Aseprite frame contact sheets were inspected; live Game View combat and player build remain open. See [C045](Cycles/C045-monster-aseprite.md) and [evidence](Evidence/C045-monster-aseprite.json). No commit/push.

# C044 F-key field travel — 2026-09-29

The `FieldMapStructureTest` west forest exit and both forest/cave passage sides now require the player's F quick interaction. Contact alone no longer opens the town confirmation or automatically switches the cave backdrop. The saved wall, the existing town Yes/No modal, same-scene cave fade/landing/camera transition, and scene-to-town fade remain. Two authored passage blockers prevent walking past the F gates. Isolated Unity 6000.3.11f1 compilation and Play Mode passed gate targeting, No/Yes town travel, both cave directions, collision barriers and time restoration. The batch Editor logged an unrelated SearchDatabase startup exception. Physical F/key movement, rendered fade frames, live Editor and player build remain open. See [C044](Cycles/C044-field-gates.md) and [evidence](Evidence/C044-field-gates.json). No commit/push.

# C043 field warp stones and statue placement — 2026-09-29

Four saved warp prefab instances and one permanent crystal chest now live in FieldMapStructureTest; the existing upper statue moved to the cave-mouth plateau while all three stable IDs remain. F interaction opens a destination-only warp map after chest unlock. Confirmation starts a same-scene fade or the existing town scene fade. Isolated Unity compiled, loaded the scene, and passed Play Mode chest/warp/home tests. Five Play Mode captures were inspected after field-only renderer ordering; physical F/mouse traversal, full path traversal and build remain open. See [C043](Cycles/C043-field-warps.md). No commit/push.

# C042 offering click routing and hit-stop pause — 2026-09-29

Tool-independent world objects now use F interaction only; left-click target selection and sword area hits require a tool-dependent `IInteractable`. Before the fix, isolated field Play Mode reproduced offering close restoring `Time.timeScale=0.06` after combat hit-stop. The same probe now restores 1.0 and confirms altar=F-only, tree=axe-click, no-tool click rejected. Script compilation passed; physical mouse/F/ESC and full game loop remain open. See [C042](Cycles/C042-offering-click-and-pause.md). No commit/push.

# C041 tile-aligned cave steps — 2026-09-29

`FieldMapStructureTest` now groups equal-height neighboring PixelFantasy cave tiles into 44 horizontal one-way treads (moss 12, stone 23, upper landing 9), replacing the three diagonal collider surfaces from C018/C040. Artwork and unrelated scene objects remain intact. Isolated Unity loaded all treads; Play Mode physics passed landing, upward pass-through and relanding at one tread in each region, and a runtime-fitted player capture aligned with the moss tile top. Manual keyboard traversal, roll feel and player build remain open. See [C041](Cycles/C041-tile-aligned-cave-steps.md). No commit/push.

# C040 camera-edge polish and delivery list — 2026-09-29

Camera size remains 8. Town, smithy and field ground/rock art now cover the visible camera margins; three one-way slopes sit closer to their visible steps; cave return activates nearer the mouth; delivery transfers retain scroll rows and selection with feedback. Isolated Unity compilation and 631 saved-scene/prefab checks passed, a runtime-fitted slope capture was inspected, and a Play Mode delivery UI probe retained scroll root, row, selection and count. Manual keyboard traversal, pointer dragging, visible fade frames and player build remain open. See [C040](Cycles/C040-camera-and-delivery.md). No commit/push.

# C039 forest/cave entrance fade — 2026-09-29

`FieldMapStructureTest` now has a level cave-mouth floor, a black fade and short same-scene landings in both directions. The forest approach keeps the cave mouth sprite but hides interior rock; the cave approach keeps only cave background/rock in view. Saved upper-mouth positions from the previous geometry land safely on the new cave plateau. Isolated Unity Play Mode passed 46 terrain/transition/camera/input/save-position assertions, four rendered approach/inside captures were inspected, and runtime/Editor C# compilation passed. Physical keyboard traversal, intermediate rendered fade frames, live Editor and player build remain open. See [C039](Cycles/C039-field-entrance-fade.md) and [evidence](Evidence/C039-field-entrance.json). No Notion edit, commit or push.

# C038 licensing diagnosis and runtime verification — 2026-09-28

The Unity license was already valid: only sandboxed verification processes could not connect to the Licensing Client IPC channel. An ordinary Windows launch of the isolated copied project connected immediately. Play Mode then exposed and fixed a repeated gathering-tool application bug that dropped the saved selected T2 axe on scene reload. The corrected isolated runs passed 14/14 tool/scene-transfer assertions, a six-wood harvest→town crafting/equip→field return, and 25/25 saved-scene hierarchy/bounds/background/platform assertions. Runtime and Editor Roslyn compilation passed with zero errors. Physical input, rendered Game View, natural combat/respawn and player build remain open; copied Editor startup had unrelated package/SearchDatabase diagnostics. See [C038](Cycles/C038-license-and-runtime-validation.md) and [evidence](Evidence/C038-runtime-validation.json). No live project Play, commit or push.

# C037 production scene cleanup — 2026-09-28

`SampleScene` now owns only the authored town and smithy interior; its duplicate gathering forest/cave, wind theme, old gathering Tilemap and spawners were removed. `FieldMapStructureTest` owns forest/cave and has functional Hierarchy groups while preserving all 363 existing GameObjects and component IDs. The town confiner points to town bounds and `TownSceneIntegration` tolerates the removed legacy `ForestOut`. Sixty-nine authoring-only cave background sections are under an `EditorOnly` parent; the runtime template stays direct. Static scene/reference/transform checks and offline Unity-reference runtime/Editor C# compilation passed with zero errors. Isolated Unity Editor and Play Mode attempts were blocked by Licensing Client IPC failure, so scene load, gameplay, visuals and build remain unverified. See [C037](Cycles/C037-scene-production-cleanup.md) and [evidence](Evidence/C037-scene-structure.json). No commit/push.

# C033 field Play Mode validation — 2026-09-28

An isolated non-batch Unity 6000.3.11f1 editor ran the saved field scene in Play Mode. All 68/68 resource and 12/12 monster targets spawned; all 12 monsters began in their assigned forest/cave region and recovered after forced cross-boundary Rigidbody2D displacement. Six tree interactions produced six drops and six automatically picked-up wood; faded travel delivered it to the town chest. In Play Mode the crafting domain used that wood to build and equip a wood sword, then faded back to the field with sword damage 15 and one spare crafted plank. Probe completion codes were 0. Direct component/domain calls were used for harvest, craft and travel; physical input, smithy UI/bed gating, natural combat knockback, respawn timer expiry, visual Game View and build remain open. The copied editor logged Visual Scripting package-cache and SearchDatabase startup exceptions, so the runs are not zero-error editor evidence. See [C033](Cycles/C033-field-play-validation.md) and [evidence](Evidence/C033-field-play-validation.json). Live project was not played or saved; Notion read only; no commit/push.

# C032 field spawns and forest/cave confinement — 2026-09-28

`FieldMapStructureTest` now has the five authored resource zones (68 target resources), seven monster zones (deer 3, bats 4, moss slimes 4, stone golem 1), an item-drop spawner, player-scale resource prefabs and per-slot Notion respawn intervals. Spawned deer are confined to the forest; bats, moss slimes and the single golem are confined to the cave using the existing entrance/depth boundary, including after knockback. Targeted C# compilation and static scene, GUID, drop-chain and cave-candidate checks passed. Isolated Unity Play Mode could not start because Licensing Client repeatedly disconnected; spawning, AI confinement and the complete gather→return→craft→depart loop remain runtime unverified. See [C032](Cycles/C032-field-spawns.md) and [evidence](Evidence/C032-field-spawns.json). Notion was read only; no commit/push.

# C031 fall-damage distance — 2026-09-28

PlayerMovement now adds 2 world units to the serialized safeFallHeight before calculating landing damage. The three saved player scenes each retain their Inspector value of 6, making their effective safe fall 8; damagePerFallUnit remains 2. An isolated C# project build passed with zero errors. Physics landing in Play Mode, visual feedback and a player build remain unverified. No scene or save asset was edited.

# C030 CSV content source — 2026-09-28

Read-only Notion resource, crafted-item, weapon, armor, trophy, failure and equipment-shop tables now feed the five CSV tables under `Assets/Blacksmith/Data/Csv`. They retain 202 existing IDs, 189 recipes, 350 ingredients, nine field item views and 14 existing drop quantities. Explicit Notion prices, combat values, T1/T2 tool purchases and shield cooldowns are applied; 20 attack intervals are converted from seconds per attack to attacks per second for `ToolData`. Unspecified T3, upgrade and debt values remain provisional. Isolated Unity compile/import and 3,182 recipe checks passed; focused economy, combat-data and shield checks passed. Live Editor refresh, physical combat, rendered UI and full loop remain unverified. See [C030](Cycles/C030-csv-content.md). Notion was read only; no commit/push.

# C029 player Aseprite visuals — 2026-09-28

The town, field and campaign player scenes now save the body and hand sprites from `Assets/source/Player` with a paired controller. Four source motions are used directly through generated paired clips; jump, fall, roll, hurt and death use explicit visual placeholders because the Aseprite files have no dedicated frames for them. Isolated Unity compilation, saved-scene reload and all nine animation-state samples passed in three scenes. Rendered Game View, physical input and build remain unverified. See [C029](Cycles/C029-player-visuals.md) and [evidence](Evidence/C029-player-visuals.json).

# C028 town, forest and cave map UI — 2026-09-28

The existing town map is now reachable beside the bag, with an indoor smithy player represented at its town façade while gameplay position stays inside. The field map opens beside the lower-left controls and uses the same saved discovery cells for both forest and cave; only explored terrain/landmarks appear. Isolated Unity Play Mode passed town/smithy and forest/deep-cave map assertions, and runtime C# compiled with zero errors. Rendered Game View, physical input and build remain unverified. See [C028](Cycles/C028-town-field-map-ui.md) and [evidence](Evidence/C028-town-field-map-ui.json). Notion was read only; no scene/prefab or save schema changed.

# C027 slope footholds, forest/cave blend and loop audit — 2026-09-28

`PlayerMovement` now holds an idle player on marked sloped one-way platforms and keeps jump grace available while running uphill; S-to-drop, rolls and ordinary terrain retain their paths. The field entrance now spatially blends its existing repeating forest/cave backdrops instead of exposing a hard mask seam. The isolated runtime C# assembly built with zero errors. Unity Play Mode did not start because the batch editor could not connect to Licensing Client, so the movement and rendered blend still need in-editor verification. The prior C025 field has zero resource/monster instances; C026 proved scene/player transfer using injected materials. Thus the intended gather→return→craft/equip→depart loop is not currently playable through ordinary gathering. See [C027](Cycles/C027-slope-entrance-loop-audit.md) and [evidence](Evidence/C027-slope-entrance-loop-audit.json). Notion was read only; no scene assets or saves changed.

# C026 scene-local player state handoff — 2026-09-28

Town and field retain their separately authored Player objects. `SmithingLoop.TryPrepareSceneTravel` now clones the current progress and inventory, applies the bag/chest transfer on the clone, captures health/position and writes a validated snapshot before the fade. The source Player is unchanged during preparation or a failed save. A static, scene-targeted JSON handoff restores the new scene Player from memory; the disk snapshot remains for durable progress. Source autosave is suspended during handoff, and a canceled load start restores the prior durable save. Isolated Play Mode passed forced save failure in both directions, prepared-transfer cancellation, a corrupted-disk memory handoff and health/gold/bag/equipment/material round-trip. The C025 boundary travel regression also passed. See [C026](Cycles/C026-scene-player-handoff.md) and [evidence](Evidence/C026-scene-player-handoff.json). The official runtime runner is being evaluated separately; Game View, physical keyboard travel and a player build remain unverified.

# C025 field boundary travel and empty gathering map — 2026-09-28

`FieldMapStructureTest` now opens the existing Yes/No town-travel prompt when the player touches the solid west forest wall; the quick-interaction return object is gone. All 30 scene monster/resource instances were removed while three assimilation statues remain. The forest background was raised 2 world units to cover the top of the followed camera viewport; forest and cave still use the same repeating background components as `SampleScene`. Four decorative town façade barrels in `SampleScene` now have static Rigidbody2D bodies and zero gravity, matching their intentionally disabled colliders. Isolated Unity Play Mode passed six forest/cave camera viewport coverage positions, boundary contact/No/recontact/Yes fade travel, and stable town barrels. See [C025](Cycles/C025-field-boundary-and-barrels.md) and [evidence](Evidence/C025-field-boundary-and-barrels.json). Game View rendering, physical keyboard traversal and player build remain unverified. The live editor was not reloaded; no Notion text, commit or push was changed.

# C024 playable gathering field — 2026-09-28

`FieldMapStructureTest` now uses SampleScene's Cinemachine follow/look-ahead pattern, saved forest/cave camera rectangles and five physical outer walls. Existing forest/cave backgrounds switch with the player; the field player now has shared crafted-equipment combat, exploration/altar state, HUD, autosave and death return without field crafting. Isolated Unity Play Mode passed round-trip travel, five camera-edge positions, region backgrounds/HUD and death recovery. See [C024](Cycles/C024-playable-field-parity.md) and [evidence](Evidence/C024-playable-field.json). Visual capture crashed in batch mode; manual visual/keyboard review and build remain open.

# C023 town/field bounds and symmetric travel confirmation — 2026-09-28

`SampleScene` town walls now collide at the actual town edges and its saved camera rectangle confines Cinemachine. A shared authored prompt asks `이동하시겠습니까?` with Yes/No in both directions between town and `FieldMapStructureTest`; Yes keeps the existing saved fade transfer, No cancels. Isolated Unity Play Mode passed both cancellation and round-trip checks. See [C023](Cycles/C023-town-field-bounds-confirmation.md) and [evidence](Evidence/C023-town-field-travel.json). Manual keyboard, visual review and build remain open.

# C022 first-switchback art y-coordinate order — 2026-09-28

`First switchback lower art` now sits at y -12, `First switchback middle art` at y -13, and the existing `First switchback corner art` stays at y -14. The two moved one-way colliders align with their art; old art cells at y -17/-16 are empty. Isolated Unity saved/reloaded the scene, verified jump/landing/S drop at both moved ledges, and drove the authored player from right cave ground through corner→middle→lower and back through middle→corner→ground using jump plus observed roll movement over 242 physics frames. The C021 coordinates below describe the prior state. Physical keyboard feel, rendered Game View and build remain unverified. The live editor was not reloaded. See Cycles/C022-corner-foothold-y-order.md and Evidence/C022-corner-foothold-y-order.json. No Notion edit, runtime-code change, commit or push.

# C021 staggered first-switchback footholds — 2026-09-28

Two more separate one-way ledges now step across the first far-right cave turn at x 100–102/y -17 and x 104–106/y -16, leading to the existing x 108–110/y -14 ledge. The collider-free PixelFantasy art and saved PolygonCollider2D/PlatformEffector2D pairs keep side passage open; the field scene now has 11 marked one-way platforms. Isolated Unity saved/reloaded the scene, verified jump/landing/S drop on each new ledge, and drove the actual saved player from cave ground through all three ledges and back to ground using jump plus observed roll movement over 212 fixed physics frames. The controls were injected in batch mode, so physical keyboard feel, rendered Game View and build remain unverified. The live editor was not reloaded. See Cycles/C021-staggered-corner-footholds.md and Evidence/C021-staggered-corner-footholds.json. No Notion edit, runtime-code change, commit or push.

# C020 field travel and authored visuals — 2026-09-28

The first far-right cave switchback now has its own three-cell one-way foothold. Marked platform animation follows directional and Shift input, including slopes. The saved town ForestIn confirmation travels with fade to `FieldMapStructureTest`; its Forest Return To Town gate fades back, and collected materials are stored in the smithy chest. The field scene is enabled in Build Settings. SmithyInterior prefab instances are saved in SampleScene and NotionCampaign, and active-scene player sprites/Animator/controllers are present before Play; runtime creation of those visuals was removed. Isolated Unity saved/reloaded the three scenes and passed the first-corner physics, platform animation and actual gate-confirmation round-trip probes, including two Wood delivered to the chest. Full manual keyboard traversal, fade visual timing and player build remain unverified. The source editor was not reloaded; preserve unsaved scene edits before reopening. See Cycles/C020-field-travel-authored-visuals.md and Evidence/C020-field-travel.json. Notion was read only; no commit/push.

# C019 one-way field platforms and scrolling backgrounds — 2026-09-28

In `FieldMapStructureTest.unity`, five thin cave ledges have separate one-way polygons and no duplicate wall tile collision. Three existing slope shortcuts now use the same one-way surface. The field-test player can stand on them, pass through sides/undersides, and press S to drop. Forest and cave backdrops repeat with the camera; the deep-left cave keeps cave art. Isolated Unity saved/reloaded the scene and passed physics/input/background probes. Full authored-player keyboard traversal, fade timing, animal AI and a build remain unverified. Live Unity was not reloaded. See Cycles/C019-one-way-field-platforms.md and Evidence/C019-field-platforms.json. No commit/push.

# C018 field shortcuts and prefab life — 2026-09-28

Only `FieldMapStructureTest.unity` changed. The upper item landing and the moss-slime/stone-golem crossings now have three smooth, solid slope-prefab routes. The forest has 13 tree/thicket/deer instances; the cave has 20 resource/creature/AssissZone instances, with no warp stone. The existing SampleScene entrance background transition switches the saved forest/cave background groups. Isolated Unity Play Mode verified the full cave mask, collision samples, more than 90 player-sized slope clearances, all 33 prefab links, three usable cave altars, background selection and six captures. Input-driven traversal, gradual fade, animal AI and a player build remain unverified. SampleScene/Notion are unchanged; no commit/push. See Cycles/C018-field-shortcuts-life.md and Evidence/C018-field-shortcuts-life.json.

# C017 branched PixelFantasy cave — 2026-09-28

`FieldMapStructureTest.unity` now includes the saved `SampleScene` player copy with persistence disabled, a bounded follow camera and a branched, player-scale cave based on the sketch. PixelFantasy_Caves_1.0 faces the colliding wall edge; the original cave backdrop shows through empty corridors. Isolated Unity Play Mode verified all mask cells, 2D physics samples, player spawn/camera, six captures and unchanged forest. Offline reachability was 559/559 grounded positions. Interactive keyboard traversal and player build remain unverified. `SampleScene` and Notion are unchanged; no commit/push. See Cycles/C016-field-test-player.md, Cycles/C017-branched-palette-cave.md and their Evidence files.

# C015 player-fit cave — 2026-09-28

`FieldMapStructureTest.unity` now uses measured SampleScene player dimensions for a narrower three-band cave. An offline route model reaches 368/368 grounded positions; isolated Unity Play Mode verified all mask cells, sampled physics points, camera bounds, six captures and an identical forest image. Actual player controls/build remain unverified. `SampleScene` and Notion are unchanged. No commit/push. See Cycles/C015-player-fit-cave.md and Evidence/C015-player-fit-cave.json.

# C014 inverse cave mask — 2026-09-28

`Assets/Scenes/FieldMapStructureTest.unity` now interprets the attached sketch correctly: widened gray cave regions are empty traversable spaces showing the existing cave backdrop; white regions are colliding rock walls filled to the camera bounds. Visible rock and exact-size collision use separate Tilemaps. Forest Play capture is byte-identical to C013. Isolated Unity Play Mode cell, physics-point, camera and visual checks passed. No player traversal or build. See Cycles/C014-inverse-cave-mask.md and Evidence/C014-inverse-cave-mask.json. Notion read only; no commit/push.

# C013 field map structure test — 2026-09-28

`Assets/Scenes/FieldMapStructureTest.unity` is an isolated forest-to-cave geometry reference based on the read-only Notion sketch and current project art. It contains 12 connected routes, existing forest/cave backdrops and cave entrance, plus a bounded pan/zoom preview camera; no gameplay objects. Unity isolated Play Mode structure and five visual captures passed. `SampleScene` was byte-identical before/after; no live-editor scene reload, build-scene change, commit or push. See Cycles/C013-field-map-structure.md and Evidence/C013-field-map-structure.json.

# C012 alloy combat and equipment shop — 2026-09-28

Blood and fire effects are connected to direct combat and arrow hits. Damage, health and saves retain tenths. Shop purchases create T1–T3 tool items for manual equipment; bags upgrade capacity; categories, selection, failure/success feedback and three planned farewells are implemented. Fire remains provisional 3/sec for 5 sec. Scenes/prefabs/Inspector assets unchanged; Notion read only; no commit/push or player build. See Cycles/C012-alloys-shop.md and Evidence/C012-validation.json.

Unity 6000.3.11f1 isolated Play Mode: 85/85 checks, zero runtime/compiler errors. Runtime and Editor offline compilation passed. Verified C011 physical smithy regression, real monster damage, blood/guard/body armor effects, fire refresh/coexistence/expiry, actual projectile collision and live burn Update, 0.6-health save/scene reload, shop filtering/selection/receipts/failed purchases/bag capacity and three farewell branches. Isolated Library reused with input hashes verified. Strict overall result remains FAILED: only generated glyph/character/atlas caches in two isolated dynamic font assets changed. Semantic cache review passed; source inputs and user persistence are preserved. No validation guard was relaxed.

# C011 physical smithy — 2026-09-27

BlackSmith F now enters a real decorated room; rack/chest/bed/crafting door use F. Left exit automatically fades to town. Rack tool ownership drives gathering slots, crafted items/quality transfer between inventories and scene-reload saves. Shop tools are owned bag items for manual equipment; this supersedes C010's pending tool question. Use SampleScene and Resources/SmithyInterior prefab. The scene is unchanged and the prefab spawns during Play. Notion read only; no commit/push/player build. See Cycles/C011-physical-smithy.md and Evidence/C011-validation.json.

Isolated Unity 6000.3.11f1 run 5d8e53fe5eb2: 38/38 Play Mode checks, zero runtime/compiler errors. Runtime and Editor offline compilation passed. Original SampleScene is byte-identical; all 5382 baseline inputs were checked before installation. Six captures inspected. Final verification reused the isolated Library after copying and hashing all source inputs; the preceding fresh import run is retained separately. Strict runner overall status remains FAILED because the isolated copy cleared the existing dynamic menu-font cache. Semantic review proves only generated cache data changed; the original font/source and user persistence are intact. The failed result is retained; no validator guard was relaxed.

# C010 town planning implementation — 2026-09-27

Start SampleScene. Town is reordered pawnshop → equipment shop → smithy → facility shop with composed facades, forest background, ground, two exits, delivery and warp. New living smithy connects equipment/chest/bed/crafting room/left exit. NPC dialogue, owned bag tiers, town day/gold HUD and inventory/delivery UI use existing state. Facility products are coming soon per user decision. Tool purchase itemization remains pending manual-versus-auto-equip decision; current tool upgrade behavior remains. See Cycles/C010-town-planning.md and Evidence/C010-validation.json. Fresh isolated Play Mode passed 46/46; source/user persistence preserved. Preserve unsaved editor work before reopening saved scene. Notion read-only; no C010 commit/push or player build.

Strict runner overall status remains failed: the isolated copy cleared the existing dynamic menu-font atlas. Semantic comparison confirmed only generated glyph/character/atlas caches changed; font configuration/references, source font and user persistence are intact. No validator protections were relaxed and the original failure record is retained. See C010-font-cache-review.json.

# C009 authored town and trade UI — 2026-09-27

Start with Assets/Scenes/SampleScene.unity, now first in build scenes. User-saved ForestIn/ForestOut use quick F interaction, confirmation and fade; BlackSmith/pawnshop/Store open the existing smithy and new data-bound trade presentation. Play HUD smithy/save shortcuts are removed; smithy exit, automatic saves and ESC settings save/load remain. Existing terrain/art/scene objects are preserved. Notion was read-only. See Cycles/C009-authored-town.md and Evidence/C009-validation.json. Fresh isolated Play Mode passed 31/31; source and user persistence unchanged. No C009 player build or physical full-map playthrough. The live original editor was not reloaded; reopen the saved scene after preserving any new unsaved editor changes. No C009 commit/push.

# C008 Notion campaign — 2026-09-27

The user authorized non-lava features and provisional missing balance. Start with Assets/Campaign/Scenes/NotionCampaign.unity. Day gathering → return → bed/nap → night crafting/equipping → sleep → next-morning delivery collection and departure. Existing scenes and unrelated edits are preserved. Notion was read-only. See Cycles/C008-notion-campaign.md for implemented scope, temporary values, remaining features and verification limits; Evidence/C008-validation.json records actual results.

# C007 smithing loop — 2026-09-27

Implemented the user-approved gathering / smithing / equipping / return loop on the saved batterMap base. Assimilation remains health and offerings heal. Notion was read-only. See Cycles/C007-smithing-loop.md for ownership, persistence, temporary combat values and limitations. Exact validation evidence is in the task output report; do not treat earlier C002/C003 results as validation of this change.

# Current handoff

## 2026-09-26 per-action player callbacks (C006)

PlayerInputHandler now has individual InputAction callback methods for 11 Player actions instead of one onActionTriggered dispatcher. Registration/removal is paired across OnEnable/OnDisable and rebound in Start after PlayerInput action initialization. C005 ToolScroll, modal gating, Hold-Interact timing and public consume methods remain. Offline runtime/Editor Roslyn compilation and static subscription symmetry passed; Unity import and Play Mode are not_run. See Cycles/C006-per-action-callbacks.md and Evidence/C006-per-action-callbacks.json.

## 2026-09-26 callback-only player input (C005)

PlayerInputHandler now uses one C# `PlayerInput.onActionTriggered` callback path, with no input polling in its Update. PlayerInteraction now gets attack press/hold/release exclusively from this handler. A `Player/ToolScroll` PassThrough Axis action binds `<Mouse>/scroll/y`. The Hold-based Interact action uses started to preserve immediate quick interaction; held Move/Sprint/Jump values survive modal UI transitions behind blocked public getters. Existing action entries, component/inputactions GUIDs and public consume APIs remain. Offline runtime/Editor compilation and static action diff passed. Unity import and Play Mode input behavior remain unverified. See Cycles/C005-callback-input.md and Evidence/C005-callback-input.json.

## 2026-09-26 project script refactor (C004)

All 215 Assets C# files inventoried (84 project-owned, 131 vendor/sample). The 84 project scripts were each dispositioned; 59 relocated into functional folders, 22 refactored, 17 new implementation/contract files added. Source and .meta hashes checked before/after installation, serialized declarations preserved, vendor sources unchanged. Runtime and Editor assembly offline compilation passed with zero errors. Unity import/Play Mode were not run. The standalone comparison harness failed with an unhandled .NET exception and supplies no gameplay evidence; it may explain the user-observed 0xe0434352 dialog. See Cycles/C004-script-architecture.md, Evidence/C004-script-refactor.json and the Korean full-file audit report in the Codex deliverables.

Next: verify Unity reimport, scene/prefab component references and behavior in a controlled editor session; do not count C003 runtime results as C004 validation. The previous auto-review rejection of a Unity retry due original source/save drift remains relevant. Preserve any user editor work while validating.

## 2026-09-26 movement mode refactor (C003)

Implemented the approved common movement contract, source/handle-based environment registration, and extraction of UpDraft, Flame and normal movement. Interface: Assets/JinHo/Scripts/Assets/IMovementMode.cs. Other new production classes: Assets/JinHo/Scripts/player/Movement. Existing PlayerMovement and UpDraftZone components retain their paths, GUIDs and all 49 serialized fields; no scene/prefab migration. Existing roll direction changes and commented grounded-roll guard preserved.

PlayerMovement selects IMovementMode and applies MovementCommand. Environment membership policies handle jump/fall suppression and gravity independently of roll/knockback. Flame priority and newest equal-priority UpDraft selection remain; UpDraft exit preserves Y velocity. Zone contacts track multiple colliders, cleanup and re-enable. Physics remains velocity-based; AddForce support is only a command-contract extension, not a gameplay conversion. Roll, ground sensing, input timing, fall damage and CharacterPhysics2D remain in their existing scope.

Validation: Unity 6000.3.11f1 isolated compilation and Play Mode assertions passed 300/300, including 64 movement checks, plus 17 runner guards. Overall run is BLOCKED: source drift (SampleScene and dynamic font) and user persistence drift were detected. Do not claim preservation or an overall pass. A separate live original-project Unity process was observed; the probe's marker confirms its separate project/persistence namespace, but the writer of the drift is not proven. Auto-review rejected a retry; no retry ran and no original changes were restored. Evidence: Docs/Evidence/C003-movement-results.json; full run: C:/Users/Master/Documents/Codex/UV/8aca78039e5d/evidence. Movement checks use deterministic calculations and synchronous real-component/callback integration; actual keyboard traversal, collision-driven trigger traversal, visual feel and player builds remain unverified.

Next: obtain user approval for the rejected validation retry, with the original project's editing/play/build activity paused, to establish an unchanged baseline. Also manually traverse saved UpDraft/Flame regions, verify jump/coyote/buffer feel, roll while overlapping environments, then portal and knockback transitions. See Cycles/C003-movement-modes.md. Further force-based movement or extracting roll/ground/fall systems is outside this implemented scope.


## 2026-09-26 roll direction update

PlayerMovement now chooses roll direction from MoveInput.x (threshold 0.01); with no horizontal input it uses the animation renderer's flipX. Pointer position and rigidbody velocity no longer select roll direction. Existing local changes, including the commented grounded-roll guard, were preserved. No scene or Inspector migration is needed.

Validation: isolated Unity 6000.3.11f1 Windows64 editor compilation passed; no compiler/import errors and original inputs preserved. Evidence: C:/Users/Master/Documents/Codex/UV/f8b7942dc7aa/evidence/result.json. Play Mode and player build were not run. Remaining manual check: roll with left/right input while the mouse is on the opposite side, then release horizontal input and roll while facing each direction.


Updated: 2026-09-23. Latest work: C002 M2 runtime harness and first M3 inventory checks implemented/executed; overall result passed. C001 and M1 remain complete.

## Scope and state

The user approved Docs in the repository and a new Wiki beneath the existing Obsidian batterground folder, with existing notes preserved. First learning coverage includes all locally identified project history rather than only inventory.

C001 is complete: documents are installed and content/preservation checks passed. See Evidence/installation-report.md. C002 M1 subsequently added Tools/Validation and verified isolated editor compilation plus deliberate-failure detection. See Validation.md. C002 M2/M3 subsequently executed Play Mode on a saved copy; latest result passed. No gameplay code was changed and no mutating UI/resource generators were invoked.

## Source baseline

Branch: JinHo_1. HEAD: f21ee84cbb3c9f1f220a901c76a1ec361fcef059. The working tree already contained changes in:

- Assets/JinHo/Scripts/player/PlayerInputHandler.cs
- Assets/SettingsMenuUI/Editor/SettingsMenuUIBuilder.cs
- Assets/SettingsMenuUI/Fonts/NanumGothic Dynamic SDF.asset
- Assets/SettingsMenuUI/Scripts/ControlSettingsController.cs
- Assets/SettingsMenuUI/Scripts/GameState.cs

Preserve these changes. The source manifest describes the working tree, not HEAD alone. Recheck branch and status before future work; this is not authorization to commit or push.

## Open matters and next action

Final operational agreements and M1/M2/M3 are approved. M2/M3 implementation and execution are recorded in Cycles/C002-runtime-validation.md and Evidence/M2-M3-validation-results.json. Latest overall status: passed; assertions 236/236, M3 inventory 10/10, guard tests 17 passed. Original saved inputs and user persistence preserved: True / True.

Use Tools/Validation/Invoke-RuntimeValidation.ps1. Do not invoke the legacy UIIntegrationSmokeTest for the current workflow. First trial encountered an editor-internal SearchDatabase exception; the subsequent run uses a pre-Play quiet interval. Retain this history, and do not equate successful UI assertions alone with an error-free overall run.

Next: use this validation cycle for the next requested feature/fix, adding only affected-neighbor gameplay scenarios. M3 currently covers inventory model boundaries, not the entire game. No new game rule is pending merely to adopt the harness. Product fixes still require their proper scope/design agreement. Static candidates remain offering bulk-return failure handling, save-load failure policy, assimilation zero/death semantics, boss/portal re-entry and physical pickup/drop. None is authorized as a product change by these test approvals.
# C034 player hand animation for quick interaction — 2026-09-28

Valid F-key and mouse quick interactions now start the existing paired `Attack` clip from `player_sp` and `playerhand_sp`, including interactions made without an equipped tool. `PlayerAnimationController` preserves the action through Animator's first evaluation frame so movement `Idle` cannot immediately overwrite it. Gathering and sword actions retain their existing calls; scene, clip, Inspector and save assets are unchanged. An isolated C# build passed with zero errors. An isolated Unity 6000.3.11f1 SampleScene Play Mode probe observed `playerhand_sp` frames 24, 25 and 26 during one completed quick interaction. The probe invoked the quick-interaction method directly; physical F/mouse input, rendered Game View, menus in all scenes and player build remain unverified. No live scene reload, commit or push.
# C035 resource drop links and larger field resources — 2026-09-28

`ResourceSpawnZone2D` now resolves the scene `ItemDropSpawner` and injects it into spawned resource components through `IResourceDropSpawnerReceiver`; all eight resource interactables support the link. Thicket also resolves a missing spawner on interaction, protecting existing non-zone instances. The five field resource prefabs (tree, thicket, stone, coal, steel) are now visibly larger than the saved player: 3.36–3.40 versus 2.2168 world units in renderer height. Tree/thicket horizontal scale was kept compact enough for the existing zone capacity. Isolated Unity C# build passed with zero errors; Play Mode populated all 68/68 resources, verified every TreeZone child had the scene spawner, and produced six tree plus four thicket drops. The live scene was not opened or saved; actual input, rendered appearance and player build remain unverified. No commit or push.

# C036 unified gathering/crafting progress — 2026-09-28

Read-only Notion confirms a carried bag plus smithy chest. The existing 14 field drop references map to catalog items; C036 now gives equipped field tools and weapons catalog IDs, stores selected tool in the integrated snapshot, prevents the separate player-state file from competing in the integrated scenes, and resolves crafted carry-item icons in the field. Existing version-1 saves remain readable; no scene or CSV was regenerated. Isolated runtime/Editor C# builds and static data/art audits passed. Two Unity Play Mode launches failed at Licensing Client initialization, so buy/equip/reload/scene-travel behavior after C036 awaits runtime verification. See [C036](Cycles/C036-integration.md). Notion was read only; no commit/push.
# C047 crafting and gathering folders — 2026-09-29

Crafting and gathering scripts plus related assets now live in separate `Assets/JinHo/Crafting` and `Assets/JinHo/Gathering` folders; the latter also owns `FieldMapStructureTest.unity`. `Assets/JinHo/Shared/Inventory/InventoryRequirements.cs` centralizes positive-count, overflow, availability and batch-scaling checks used by field consumption and smithy recipe filling. Existing scene/prefab/script `.meta` GUIDs and inventory/save models were preserved. Literal asset paths, CSV rows and `EditorBuildSettings` were updated. Static checks found no old active paths, no duplicate GUIDs and 129 moved GUIDs matching Git HEAD. A hash-matched isolated Unity 6000.3.11f1 copy compiled/imported without errors; smithing content validation found 16 field IDs and 129 enabled recipes, and a focused inventory probe passed aggregated field consumption and duplicate-ingredient smithy filling. A final isolated run loaded the moved field scene (445 objects, zero missing scripts), the smithy scene and both Resources assets. Scene Play Mode, live Editor refresh and build remain unverified. See [C047](Cycles/C047-crafting-gathering-layout.md) and [evidence](Evidence/C047-crafting-gathering-layout.json). No commit/push.

# Field drop sprites and unified recipe map — 2026-10-01

The six field drop prefabs with matching crafting art now render the same sprites as their crafting item IDs. Five dedicated 64–80 px icons keep the prior world footprint through SpriteRenderer sizing; drop transforms, colliders, item IDs and pickup behavior remain unchanged. The six drops whose crafting icon is still the shared leather placeholder retain their distinct field sprites. The smithy recipe map now uses one full-screen, two-axis scroll canvas for all four authored routes, without category tabs or black fog. Undiscovered recipe nodes and their connections are hidden; newly discovered nodes and links animate into view. Recipe details and material placement remain accessible after selecting a node. An isolated Unity Play Mode probe and C# build passed; visual Game View inspection, manual input and player build remain unverified. See `Docs/Validation.md`.
