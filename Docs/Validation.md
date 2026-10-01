# In-game save menu, death return and player info button — 2026-10-01

`Assembly-CSharp.csproj` and `Assembly-CSharp-Editor.csproj` built with zero errors after the UI changes; a final runtime rebuild also passed. Static inspection found 24 unique YAML object IDs and no missing local references in `CampaignSaves.prefab`. Source search found no death-screen initial-menu button or callback; only `DeathReturnTownButton` is created. The save screen's `SaveTab` and `LoadTab` guides match the runtime control names; the modal and main-menu button copy the in-game menu palette. UITestScene's existing real-save protection still hides `MainMenuSaves`. Play Mode and rendered Game View were not run per user instruction, so pointer navigation, text fit and actual layout remain unverified.

# Recipe graph prerequisite arrows — 2026-10-01

Runtime and Editor C# assemblies built with zero errors after adding two-segment child-end arrowheads. A final runtime rebuild after adjusting right-to-left link endpoints also passed with zero errors. `DrawGraph` creates an arrowhead for every distinct active ingredient-to-output link, and `RefreshFocus` enables only arrowheads whose link is in the selected vertex's prerequisite chain; `Select(null)` disables them. The manual recipe-map verification asserts both states but was not run due to the user's no-Play-Mode instruction. Rendered geometry, pointer behavior and line overlaps remain unverified.

# Recipe graph focus contrast and background deselection — 2026-10-01

`Assembly-CSharp.csproj` and `Assembly-CSharp-Editor.csproj` built with zero errors (25 and 39 warnings). Source inspection confirms the viewport retains its raycastable image and now carries `RecipeGraphBackgroundClick`; graph edge images do not intercept raycasts, while vertex buttons retain their own click handler. `RefreshFocus` sets stronger selected/ancestor colors, outline distances, edge thickness and unrelated alpha, then restores defaults on `Select(null)`. The existing manual verification now asserts focus, background clear and refocus, but was not run due to the user's no-Play-Mode instruction. Actual pointer hit testing, drag versus click behavior and rendered contrast remain unverified.

# Workbench batch crafting and unused material return — 2026-10-01

`Assembly-CSharp.csproj` and `Assembly-CSharp-Editor.csproj` built with zero errors (25 and 39 warnings). Source inspection confirms workbench `Batches` uses the minimum floor division across required ingredients, `Finish` consumes `recipeBatches × requirement` before returning the remainder to the chest, and `CanBegin` emits `아이템을 더 넣으세요` for a partial matching workbench recipe. Other station batch matching remains exact. `git diff --check` passed for the changed domain sources. No Play Mode or rendered UI check was run per user instruction; actual pointer entry, saved chest contents and output counts in a running game remain unverified.

# Recipe graph connections, focus and locked clues — 2026-10-01

The Editor log reported `CanvasGroup.set_alpha` from `RecipeBookView.RefreshFocus`; both C# assemblies compiled before and after the source fix, so this was a runtime UI exception rather than a compiler diagnostic. After the fix, `Assembly-CSharp.csproj` and `Assembly-CSharp-Editor.csproj` each built with zero errors (25 and 39 warnings); the subsequent acquired-output ancestry adjustment also compiled in the runtime assembly with zero errors. Static inspection of enabled recipe CSV rows found 59 recipes, 66 distinct required item nodes, and 97 distinct ingredient-to-output links; every link endpoint exists in `RecipeGraphLayout` and the item catalog. The manual `RecipeBookVerification` expectations cover selecting a visible locked child, showing its historical clue, and retaining that selection when reopening the map. It was not run because the user excludes Play Mode verification. Editor recompilation, Game View appearance, physical pointer clicks and runtime exception disappearance remain unverified.

# Unity validation

## Smithy interior button and UI text — 2026-10-01

`Assembly-CSharp.csproj` built with zero errors and 25 warnings after deleting the `LeaveSmithy` creation and builder code. Source inspection found no `대장간 실내로` label or `LeaveSmithy` object. The existing home exit and panel Back still call `SmithingLoop.Travel()`. Field bag slot construction has no quantity or fallback TMP elements; town inventory slots remove the row count TMP only outside delivery mode. `InventorySlotView` keeps a separate preview font so drag quantity still renders without an on-slot label. `RuntimeUIFactory.FitText` changes only TMP font limits and margins. Targeted `git diff --check` passed. Play Mode and actual layout/drag verification were excluded by user instruction.


## Town inventory shop-style panels — 2026-10-01

`Assembly-CSharp.csproj` built with zero errors and 25 warnings. Static checks found 52 unique YAML object IDs, no missing local references, and one each of `TownBagPanel`, `TownChestPanel`, `TownItems`, and `TownChestItems` in the editable `TownInventory` layout prefab. Source inspection confirms the existing five-column grids and transfer handlers remain connected, while only the town inventory receives the shop palette. Play Mode was excluded by user instruction; rendered panel layering and actual drag transfer remain unverified.


## Field assimilation intervals — 2026-10-01

`Assembly-CSharp.csproj` built with zero errors and 25 warnings. Source inspection confirms 5/4/3.5/3 second intervals and +1 assimilation per completed interval, with floor transitions using the same -21/-39/-59 y boundaries as the cave mask. The saved field scene contains no `AssimilatelZone` component. Play Mode was excluded by user instruction; verify the visible gauge after each full interval in the forest and each cave floor, then cross a floor boundary and verify the new timer begins at zero.


## Recipe graph and workbench input — 2026-10-01

`Assembly-CSharp.csproj` and `Assembly-CSharp-Editor.csproj` built with zero errors (four and 18 warnings on the final checks). One intermediate Editor build overlapped unrelated combat-code edits and failed to resolve `SwordReachArc2D`; the repeat build passed after its definition was present. Static checks found no active recipe-category buttons, pin-goal UI, gathering-goal HUD, selected recipe goal API, or workshop `DemoData` object. The workshop prefab has 12 unique YAML object IDs and no missing local references. `CraftingService.Batches` aggregates counts by item ID; `InventoryService.Select` requires an enabled recipe containing all currently selected workbench item IDs before adding another. `CraftingService.CanBegin` is read-only and used by the Start button and `Begin`. `git diff --check` passed for the changed tracked C# files. Play Mode was not run at the user's request, so rendered lock states, drag/drop, exact pointer behavior and production saves remain unverified.

## Shop sale layout and forest-to-town bag — 2026-10-01

`Assembly-CSharp.csproj` built with zero errors and four warnings. Static inspection confirmed `TryPrepareSceneTravel` and `LoadShopSession` import to `smith.bag`, the sale prefab contains one `TradeBag`, `TradeChest`, `SaleDrop` and `ConfirmTrade` guide each, and its 48 YAML object IDs are unique with no missing local references. Play Mode is excluded by user instruction; visual spacing, pointer drag/drop, save/reload and both travel routes remain unverified at runtime.

## Title text and latest-eight save list — 2026-10-01

The runtime C# assembly compiled with zero errors and four warnings. Static checks confirmed the main title prefab has no Help, Subtitle, AutoSaveHeader or AutoSaveStatus objects; the Save List button anchors at x=.69–.94/y=.44–.54; the list prefab retains eight rows, has the requested question and Yes/No labels, and has no dangling local hierarchy/component references. An isolated .NET harness compiled the actual `AutoSaveHistory` and `FileTextStore` sources and saved ten times on day 1: eight snapshots remained (sequences 10–3), and the primary save matched sequence 10. Play Mode was not run per the user's instruction; rendered layout, pointer clicks, confirmation/cancellation and failure-path pruning remain unverified.

## Earlier title save-list screen — 2026-10-01

The previous title save-list implementation compiled with zero errors and 25 warnings. Its scene YAML contained eight unique history button references. The day-grouped, left-side layout described in that earlier check was superseded above; it was not visually verified.

## Empty tool HUD slots — 2026-10-01

`dotnet build Assembly-CSharp.csproj --no-restore -v:q -clp:ErrorsOnly` passed with zero errors and 25 warnings using an isolated output path. The first sandboxed attempt could not write Unity's `Temp/obj`, and the isolated-intermediate attempt lacked generated dependency DLLs; the approved unsandboxed retry completed. Source inspection confirms empty icons at UI creation, null icons for empty equipped slots, and a null icon for the no-weapon branch fallback. A Unity Game View or player build was not run; verify by unequipping each tool and then equipping a sword, axe and pickaxe.

## Recipe graph ancestry and details — 2026-10-01

`dotnet build Assembly-CSharp.csproj --no-restore` passed with zero errors and 25 warnings. The existing manual `RecipeBookVerification` category expectation was updated, but its Play Mode menu action was not run. Mouse selection, rendered color contrast, ancestor edge routes and detail panel layout remain unverified in Game View.


## Field visual cleanup — 2026-10-01

`dotnet build Assembly-CSharp.csproj --no-restore` passed with zero errors and 25 warnings. A scoped source search found no remaining `HeldToolVisual` creation/refresh or `FieldEquipment` panel code in the two changed classes. The rendered field Game View was not opened for this change; visual absence in a new Play Mode session remains unverified.


## Town trade, inventory, recipe and combat UI — 2026-10-01

The runtime `Assembly-CSharp.csproj` built with zero errors and 25 warnings, including Unity package and existing project warnings. Unity 6000.3.11f1 refreshed the live project, entered Play Mode at `TitleScene`, displayed the title menu, and showed zero Console errors and warnings. Play Mode was stopped afterward. This confirms compile and title startup only; the changed shop, drag/drop, field bag/equipment, recipe, crafting scroll, swing and golem paths were not reached in Game View. No player build was run.


## Held tool visual and bow arrow count — 2026-10-01

Roslyn parsed 317 project C# syntax trees and reported zero syntax or semantic errors after the change. The authored player scenes contain `PlayerHand`, and the player animation clips still bind that renderer's `m_Sprite`; the added `HeldToolVisual` is a separate runtime child. `FieldHud` reads the same `CampaignCombat.ArrowCount` that `Fire()` decrements. No Unity Play Mode or rendered Game View inspection was completed for the new grip offset, icon size, layer order, bow-only label placement, equipment change, scene transition, or player build. Check these in an isolated scene or the live Editor after preserving unsaved work.


## Increasing assimilation and restored offerings — 2026-10-01

`dotnet build Assembly-CSharp.csproj --no-restore -v:q` passed with zero errors (the final incremental build reported four warnings). An isolated Unity 6000.3.11f1 Play Mode probe called the actual `TakeDamage(25)` path and passed: new player 0 and alive, damage 25, offering two Wood through `AssimilationOfferingUIController.CommitOffering()` consumed both items and reduced assimilation to 23, legacy remaining-health 60 restored as assimilation 40, and one death event at 100. Evidence: `C:/Users/Master/Documents/Codex/assimilation-direction-probe-20261001.txt`. The test did not use a physical pointer or the authored statue scene, travel between scenes, inspect the Game View, or make a player build. The source scene files and ordinary save files were not edited.

## Field drop sprites and unified recipe map — 2026-10-01

Six gathering drop prefabs now use the same sprite selected by crafting's item ID/art fallback: wood, coal, stone, ore, branch and floating ore. The five small dedicated icons use SpriteRenderer Sliced sizing to retain their previous world footprint without changing transforms or colliders. The other six drops retain their distinct field sprites because crafting currently has only a shared leather placeholder for them. An isolated Unity 6000.3.11f1 Play Mode probe loaded all six changed prefabs and verified their sprite assets and the five preserved renderer sizes. It also opened the smithy recipe map and verified one full-screen graph, four resource roots, no category tabs or fog component, hidden undiscovered plank, and a newly discovered plank node and link. Evidence: `C:/Users/Master/Documents/Codex/recipe-sprite-probe-20261001.txt`. `dotnet build Assembly-CSharp.csproj --no-restore` passed with zero errors. Physical clicking, final Game View composition and a player build remain unverified.

## Town trade eligibility — 2026-10-01

An isolated Unity 6000.3.11f1 Play Mode probe loaded `SampleScene` and confirmed that the imported catalog has six `town_` axes/pickaxes classified as tools. `CampaignTradeUI.Owned` listed a tool for shop sale and an item for pawn pledge, excluding each from the opposite list. `CampaignEconomy` rejected tool pledges and item/weapon/core sales without changing gold or inventory; it accepted tool sale, item pledge and repayment, legacy tool-loan repayment, discovered-item purchase and tool purchase. Evidence: `C:/Users/Master/Documents/Codex/trade-rules-probe-20261001.txt`. Unity compiled the scoped code; physical drag, rendered Game-view layout and an installed-player run were not checked.

## Editor-only God Mode — 2026-10-01

An isolated Unity 6000.3.11f1 Play Mode probe granted 99 of each of 205 imported catalog IDs to the chest, discovered all 59 enabled recipes without changing an existing seven-craft mastery record, preserved the bag, and rejected both a second grant and a preflight stack overflow. Direct town→field→town loads and a hosted smithy entry retained the grant. SHA-256 comparisons found every ordinary EditorPlayMode file unchanged after the grant, automatic and manual saves, scene transitions, and Play Mode exit. Normal and Development Windows builds from a synchronized copy of all current Assets, Packages and ProjectSettings each succeeded for five enabled scenes with zero build errors; both resulting Assembly-CSharp.dll files lacked the editor button and session type. Evidence: `C:/Users/Master/Documents/Codex/god-mode-probe-20261001.txt` and `C:/Users/Master/Documents/Codex/god-mode-build-20261001.txt`. The script-reload reinstallation code compiled, but its active-session reload behavior, live Game-view layout and physical click were not directly exercised.

## Cave upper-screen lighting relief — 2026-09-30

An isolated Unity 6000.3.11f1 project loaded the saved `FieldMapStructureTest` with cave ambient 0.05 and one authored elliptical player light. Play Mode rendered 1280×720 camera captures at middle and deep cave positions (`cave-middle-ellipse-ambient005.png`, `cave-deep-ellipse-ambient005.png`). Both were visually inspected against the prior ambient 0 capture and the overbright 0.08 trial; the upper cave contour is visible without washing out the dark background as much as 0.08. `CaveFloorProbe` passed upper/deep mask enable and forest mask disable checks. Evidence is in `C:/Users/Master/Documents/Codex/UV/d7ef7f756b00/evidence/`. These camera renders omit the ScreenSpaceOverlay mask and HUD, so the actual final composite, continuous player movement, live Editor and player build were not verified.


## Field stair ascent and landmark spawn exclusion — 2026-09-30

An isolated Unity 6000.3.11f1 copy compiled the changed C# with no errors and reloaded `FieldMapStructureTest` with three `FieldStairChain` markers, 11 Upper Item Landing treads, the first art cells at y -23/-22 and unique scene object IDs. Authored stair tops are horizontal (normal y=1); their one-cell height/run approximates 45 degrees, below `minGroundNormalY=0.65`'s 49.46-degree limit. The original Upper entrance rose three units from the adjacent solid floor, so it required two intermediate one-way levels; the saved floor-to-landing sequence is now -23→-22→-21→-20. The nine separate hairpin footholds were not marked.

Play Mode on the saved player, using injected `PlayerInputHandler` movement state, recorded eight cases: Moss uphill left foot -26→-24, downhill right -24→-27.17 and uphill left while running -26→-24; Golem uphill left while running -39→-35.88 and downhill right -36→-39.17; Upper uphill right from the solid floor -23→-18.21, downhill left -19→-23 and uphill right while running -23→-18.14. In the Upper uphill cases the player was approaching the next -18 tread, so that last height is not an overshoot assertion. A clone of an authored PlatformEffector2D tread allowed sideways passage (x -3→3.2 at foot 19.25), upward passage followed by landing (maximum foot 20.04; settled 20.01 on top 20), and S drop (standing true, collision ignored immediately, foot fell to 15.81 and collision later restored). The actual scene's three statues and four warp stones use Interactable trigger bounds matching their rendered XY bounds. A focused physics probe returned overlap true at a trigger and false after moving it away.

These checks used an isolated project, repositioning and injected input states. Physical keyboard control, a continuous manual route through every tread, high-wall and roll/knockback Play Mode cases, actual resource population around every landmark, Game View appearance, live Editor behavior and a player build remain unverified. Evidence: `C:/Users/Master/Documents/Codex/UV/d7ef7f756b00/evidence/stair-cases.txt`, `stair-oneway.txt`, `resource-blocker.txt`, `landmark-bounds.txt` and `stair-scene-validate.txt` in that directory.


## Field HUD and cave lighting gameplay follow-up — 2026-09-30

The isolated Unity 6000.3.11f1 `FieldGameplayProbe` entered active gameplay in `FieldMapStructureTest`. Map and bag button `onClick` calls opened their respective windows and closing each restored `Time.timeScale`; the unselected goal remained hidden, a selected enabled recipe appeared, and a 9/10-weight test bag turned red (r=0.65, g=0.15). The test restored the original isolated inventory and capacity afterward. Play Mode checks confirmed the one elliptical cave light followed the player and the camera framed upper, middle and deep cave positions. Four 1280×720 camera captures (three cave, one forest) were inspected; outside the elliptical cave light the adjacent floors were dark. These camera renders exclude the screen-space HUD, so its visual layout was not inspected in Game View; its hierarchy and click behavior were checked in Play Mode. A forest return set ambient intensity to 1 and disabled the cave light. A test-only injected move state moved the player 3.87 units and the camera 5.16 units in the forest. This verifies player movement and camera follow while bypassing keyboard binding. The synthetic keyboard reported its D key pressed and bound to `Player/Move`, but the batch-mode action stayed at zero, so physical keyboard traversal and every intermediate camera frame remain unverified. The deep-cave anchor landed lower on a real slope before its capture; this screenshot verifies that lower view, not the requested starting coordinate. No runtime C# compile errors were logged. The isolated Editor emitted a `UnityEditor.Search.SearchDatabase` startup exception outside the passing gameplay assertions. Evidence: `C:/Users/Master/Documents/Codex/UV/d7ef7f756b00/evidence/field-gameplay-probe.txt` and `field-ellipse-{upper,middle,deep,forest}.png` in the same directory. No live Editor or player build was run.

## Field HUD cleanup and elliptical cave sight — 2026-09-30

In an isolated Unity 6000.3.11f1 copy, Play Mode loaded `FieldMapStructureTest` and asserted that the map and bag buttons remained, `FieldRisk` was absent, and the unselected `GatheringGoal` was inactive. The cave probe also confirmed cave state, ambient 0 and one enabled player-following elliptical light. A 1280×720 middle-cave Game View capture was inspected: horizontal light fades smoothly, while terrain outside the ellipse is unlit at the screen edge. Scene YAML has one 24-vertex ellipse and no side-light references; `git diff --check` passed. The deep-cave teleport capture retained stale camera framing, so adjacent-floor visibility during real movement, selected-goal transitions, bag button input and a player build remain unverified.

## Softer cave sight falloff — 2026-09-30

In the isolated Unity 6000.3.11f1 copy `UV/d7ef7f756b00`, Play Mode confirmed the player entered the cave, ambient intensity was 0.12 and three cave-following lights were enabled. The middle-cave 1280×720 Game View capture was inspected after testing ambient 0.02, 0.06 and 0.12; 0.12 retained a dark neighboring area while showing a softer transition and faint nearby terrain. The scene keeps the existing wide/shallow shapes and now serializes 1.5-unit light falloff. The attempted deep-cave capture after a direct player teleport had stale camera framing and is not visual verification of deep-cave traversal. Normal input traversal, live Editor and player build are unverified.

## Day/night torches and horizontal cave sight — 2026-09-30

An isolated temporary C# project included `WorldTorchDayNight.cs` and `FieldCaveLighting.cs` and built with zero errors. Static checks found unique scene/prefab object IDs, one torch controller in each affected asset, all authored flame/light names, and three cave freeform shapes with widths 15/17/15 units and heights 3.6/4.4/3.6 units. `git diff --check` passed. The standard isolated town validation `UV/d7ef7f756b00` stopped before assertions because its existing `CampaignValidate.cs` assigns a float to an int at line 93; no original source was changed. A focused Unity Edit Mode probe in that copy then loaded `SampleScene`, `SmithyInterior` and `FieldMapStructureTest`, toggled town/smithy light and flame off→on→off, and confirmed all three freeform light shapes. The source repository remained untouched by the probe. Check actual town and smithy Game Views by switching day to night and back; verify fixtures remain but flames and local light turn off during day. In the field cave, walk the upper, middle and deep switchback corridors and both turns to inspect light coverage and adjacent-floor visibility. Live Play Mode, saved-time transitions and visual coverage are unverified.

## Town, smithy and cave lighting — 2026-09-29

An offline build of `Assembly-CSharp.csproj`, temporarily including the new `FieldCaveLighting.cs` in the generated project file and restoring that file afterward, passed with zero errors and four existing warnings. The authored `SampleScene`, `FieldMapStructureTest` and `SmithyInterior` YAML have unique object IDs and no missing local references in the new records; diff whitespace validation passed. The isolated Unity 6000.3.11f1 run `UV/41b4bc53d3ca` timed out during Licensing Client initialization before scene import or Play Mode assertions, with no unexpected copy mutation, source drift or user-persistence change. Actual Game View brightness, cave/forest switch, player light follow, torch proportions, frame cost and player build remain unverified. Verify the town around x=-121..-50/y=62, smithy interior around local x=-17,-3,14/y=8, and both sides of the field cave entrance when the Editor can run.


## Wrong-tool and low-tier harvest feedback — 2026-09-29

`Assembly-CSharp.csproj` built with zero errors and four existing warnings. Source checks confirmed incompatible tool types remain outside `ResourceHarvestWorkflow.Interact`, while under-tier attempts retain their existing rare-drop branch and do not increment depletion. The isolated Unity Play Mode runner remains unavailable because its last attempt timed out at Licensing Client initialization; rebound timing, visible text placement, held-click repetition and player build have not been verified in Game View.


## Field guidance and hit feedback — 2026-09-29

`Assembly-CSharp.csproj` compiled with zero errors (four existing warnings), and the changed-file diff passed whitespace validation. An isolated Unity 6000.3.11f1 attempt `UV/343be1b4143e` timed out during Licensing Client initialization before Play Mode assertions; its preservation guard reported no source drift, unexpected copy mutations or user-persistence changes. The generated `Assembly-CSharp-Editor.csproj` could not be built standalone because its Unity.Burst project-reference graph is cyclic; this did not affect the runtime assembly result. Physical bag/map clicks, recipe-goal save/restart, encounter detection, offering hold timing, resource/monster audiovisual feedback, Game View layout and player build remain unverified.

## Editor game-save isolation and title restart — 2026-09-29

`dotnet build Assembly-CSharp.csproj` and `Assembly-CSharp-Editor.csproj` with intermediate/output files outside the project passed with zero errors (four and fourteen warnings respectively on the final incremental builds). Static checks confirmed the authored title menu/subtitle/start references, all three game-save owners using `GameSavePaths`, and title Start requesting the town spawn and resetting a saved debt game-over. The historical validation probes were updated to resolve saves through the same helper. The isolated town Play Mode attempt `UV/1e910b57bca6` timed out while Unity Licensing Client refused connection; no assertions ran and there were no compiler errors or unexpected isolated-project mutations. Its preservation guard observed the live Editor's `EditorPlayMode/smithing-loop-v1.json` and `.bak` change during the attempt. Those live files were left untouched. An actual Editor/player path round trip, title button click, saved-data restore, process restart, Game View and player build remain unverified.

## C053 authored title scene and death return — 2026-09-29

An isolated Unity 6000.3.11f1 Editor run compiled the new title/death code and loaded the authored `TitleScene` at build index zero. It verified the saved menu, settings, buttons and EventSystem, ten title-controller references and zero missing scripts. An isolated Play Mode probe used a separate validation product/persistence identity, but stalled after Unity's `SearchDatabase` startup exception before assertions. The Start→town and death→town click flows, rendered title/death layouts, fade, persistent restart and player build remain unverified. See [C053](Cycles/C053-title-scene-and-death-return.md).

## C039 forest/cave entrance fade — 2026-09-29

An isolated Unity 6000.3.11f1 Play Mode run passed 46 checks for the entrance plateau, retained roof clearance, cave/forest art and backdrop selection, fade-time input pause, both landing positions, camera confiner switching, restored time/input and legacy upper-mouth save placement. Four 1280×720 Game View captures of the forest approach/mouth and cave interior/return were rendered and inspected. Offline runtime and Editor C# compilation passed with zero errors. The copied Editor logged package-cache/Search diagnostics outside the passing probes. Actual keyboard traversal, intermediate fade frames, natural AI/resource behavior at the passage, live Editor and player build remain unverified. See [C039](Cycles/C039-field-entrance-fade.md) and [evidence](Evidence/C039-field-entrance.json).

## C031 fall-damage distance — 2026-09-28

An isolated `Assembly-CSharp.csproj` build passed with zero errors after syncing the changed PlayerMovement source. Saved scene values were checked: SampleScene, FieldMapStructureTest and NotionCampaign each retain safeFallHeight 6 and damagePerFallUnit 2, giving an effective safe fall of 8 with the added 2-unit grace. Unity Play Mode landing, the live Editor and a player build were not run.


## C028 town, forest and cave map UI — 2026-09-28

The isolated runtime assembly built with zero errors. Unity 6000.3.11f1 isolated Play Mode passed a saved town probe for the map button, explored smithy icon and indoor player marker at the town façade. A saved field probe passed the map button, visited forest and deep-cave cells, cave entrance/player markers, and opening/closing with time/input restoration. See [C028 evidence](Evidence/C028-town-field-map-ui.json). The copied Library logged package-cache and Unity Search startup exceptions; no map assertion or C# compiler error occurred. Rendered Game View appearance, physical mouse/keyboard operation and player build remain unverified.

## C027 slope and forest/cave transition — 2026-09-28

The modified runtime assembly compiled in an isolated copy with `dotnet build Assembly-CSharp.csproj` (zero errors), and the two installed script diffs passed `git diff --check`. A dedicated isolated Unity Play Mode probe for idle slope drift, uphill running/jump and entrance alpha positions was prepared, but the batch editor repeatedly failed to connect to Licensing Client and produced no probe result. No post-change physics, rendered transition, keyboard run or player build passed in this cycle. C025's zero resources/monsters and C026's injected-material scene round trip are prior evidence only; the ordinary gather→return→craft/equip→depart loop is not currently complete. See [C027 evidence](Evidence/C027-slope-entrance-loop-audit.json).

## C026 scene-local player state handoff — 2026-09-28

An isolated Unity 6000.3.11f1 Play Mode probe forced save-write failure for both town→field and field→town and verified that the source Player, bag/chest and previous durable save stayed unchanged. It also tested canceling a prepared transfer, restoring the previous save and blocking source autosave during handoff. A successful trip deliberately corrupted the disk snapshot before scene load; the destination restored from the memory handoff and repaired the durable save. Health, gold, a Medium-quality equipped sword, carried Wood, delivered Wood/Stone, input/time and the return position survived the round trip. A 105-unit inventory accepted exactly 21 × 5-unit items, then rejected the next item. The C025 forest wall contact/No/recontact/Yes fade and background/barrel probe passed again. See [C026 evidence](Evidence/C026-scene-player-handoff.json). Unity batch compilation had zero C# errors; copied Library package-cache/search startup exceptions remain unrelated to these passed assertions. The official saved-UI/inventory runner was attempted separately, but Unity Licensing Client connection failure prevented the probe from starting; the audit preserved source and user saves. Visible fade, physical keyboard travel and a player build remain unverified.

## C025 field boundary prompt, repeating backdrop and stationary barrels — 2026-09-28

An isolated Unity 6000.3.11f1 copy compiled and saved both scenes. Play Mode checked the repeating forest/cave background bounds against the camera viewport at six positions, found zero field resource/monster components and all three assimilation statues, then exercised the west forest wall contact prompt: No left the player in the field without immediately reopening; leaving and touching the wall again reopened it; Yes completed the existing fade travel to town. Four town façade barrel Rigidbody2D components remained static at their saved positions. See [C025 evidence](Evidence/C025-field-boundary-and-barrels.json). This does not verify rendered seams, physical keyboard movement, visual fade timing or a player build. The live editor was not reloaded.

## C024 playable gathering field — 2026-09-28

An isolated Unity 6000.3.11f1 copy saved and reloaded the field scene with a Cinemachine player-follow rig, two region camera bounds and five physical outer walls. Play Mode verified round-trip travel, field combat-tool activation and used-altar gating. Five forest/cave positions kept the full camera viewport within the active bounds and switched the background/HUD. A fatal hit applied the 4% gold and lost-bag penalty, returned to town and restored health, time and input. See [C024 evidence](Evidence/C024-playable-field.json). A batch-mode URP render request crashed before a screenshot; the non-rendering rerun passed. Manual keyboard, visible screen composition, full combat/resource scenarios and player build remain untested.

## C023 town/field bounds and travel confirmation — 2026-09-28

An isolated Unity 6000.3.11f1 copy saved both scene prompt instances and repaired town wall colliders. Play Mode verified the active Cinemachine town boundary, both wall centers/layers, exact Yes/No dialog, No cancellation in both scenes, Yes round trip through the existing fades, preserved gold, two Wood transferred to the chest and restored time/input state. See [C023 evidence](Evidence/C023-town-field-travel.json). Manual keyboard interaction, rendered UI/fade, all camera-edge compositions and a player build remain untested. Copied-Library package-cache/search startup exceptions occurred outside the passed assertions.

## C022 first-switchback art y-coordinate order — 2026-09-28

An isolated Unity 6000.3.11f1 copy saved/reloaded the field scene and verified lower/middle/corner art at tile y -12/-13/-14, with two one-way colliders moved to matching heights and six old art cells removed. Player-sized jump/landing/S-drop probes passed for both moved ledges. The actual saved player's movement and roll code then traversed right cave ground → corner → middle → lower → middle → corner → ground for 242 fixed physics frames, observing roll in both directions. Input state was injected in batch mode; physical keyboard feel, rendered Game View and build were not checked. See [C022 evidence](Evidence/C022-corner-foothold-y-order.json).

## C021 staggered first-switchback footholds — 2026-09-28

An isolated Unity 6000.3.11f1 copy saved/reloaded the field scene, checked two new x/y-offset one-way colliders and six collider-free PixelFantasy art cells, and passed jump/landing/S-drop physics on both. A second Play Mode probe used the authored player's movement and roll implementation with actual 2D physics for 212 fixed frames, reaching ground → lower → middle → existing corner ledge → middle → lower → ground. Roll state was observed on ascent and descent. Input state was injected in batch mode; physical keyboard hardware, visual Game View and player build remain untested. See [C021 evidence](Evidence/C021-staggered-corner-footholds.json).

## C020 field travel and preauthored visuals — 2026-09-28

An isolated Unity 6000.3.11f1 copy passed three-scene saved/reloaded authoring checks, first-switchback jump/landing/S-drop physics, field-platform Run → Walk → Idle selection, and a Play Mode trip from the saved town forest gate's confirmation through the field return gate. Two Wood transferred from the field inventory to the smithy chest; gold and UI/time state survived the round trip. Results: [C020 evidence](Evidence/C020-field-travel.json). These probes did not provide complete physical-keyboard traversal, rendered fade review or a player build. The copied Library reported package-cache/search startup exceptions; no C# compiler errors were found. The live editor was not reloaded.

Updated 2026-09-23. M1 remains verified. M2 UI and first M3 inventory checks are approved, implemented and executed; latest overall runtime result: passed. See the M2/M3 section below.

## Verified usage

From the repository root in PowerShell:

```powershell
& ./Tools/Validation/Invoke-UnityValidation.ps1
```

Requirements: Windows, Git, Python 3.10+ available as python (or supply -PythonPath), installed Unity matching ProjectSettings/ProjectVersion.txt, and available Unity license/package access. Actual tested Unity: 6000.3.11f1. Default run root: current user's Documents/Codex/UV. Use a short path; the runner rejects project prefixes longer than 64 characters to reduce Windows importer path-limit failures. This bound is a guard, not proof that all future package filenames will fit.

The runner snapshots saved Assets, Packages and ProjectSettings including .meta and uncommitted/untracked files. It excludes Library, Temp, UserSettings, .git and generated root IDE files. Unsaved editor memory is not included. External local packages and reparse points are rejected pending explicit support.

The Python runner writes a unique snapshot and SHA-256 manifest, injects a completion probe only into the copy, launches batch/no-graphics Unity for Windows64 editor compilation, and records exit status plus positive completion evidence. No UI generator, scene Play Mode or player build is invoked. CompileProbe.cs.txt stays outside the live Assets folder. See [tool usage](../Tools/Validation/README.md).

## M1 historical results

| Check | Result |
|---|---|
| Guard/outcome tests | 12 passed; overlap, existing target, missing executable, path length, unsupported dependency, source drift, timeout, stale/missing marker, compiler/import failure and recovered licensing warning |
| Real normal run | passed; exit 0, matching completion marker, no C# compiler errors or asset-import failures |
| Real negative control | deliberate #error detected; compile failed as expected, no completion marker, negative_control_detected=true |
| Original disk inputs | all 4,956 selected input files preserved in both final runs |
| UI / Play Mode / player build / visual behavior | not_run |

Normal run: `C:/Users/Master/Documents/Codex/UV/262e31168003` (297.54 seconds including copying/hashing).
Negative run: `C:/Users/Master/Documents/Codex/UV/5ae46b49ec59` (200.07 seconds).
Structured evidence: [M1 results](Evidence/M1-validation-results.json). Full logs/manifests remain in each run's evidence directory.

Initial trial: a longer output path caused package asset-import DirectoryNotFoundException errors even though the C# completion marker was reached. The initial classifier also mistook a recovered startup licensing warning for a final blocker. We shortened the default path, added path-length and import-error guards, corrected licensing classification and added regression tests, then ran the final trials. Original project packages were not upgraded or edited. Initial logs remain preserved.

## Result semantics and limitations

Pass requires exit 0, the current run/project/version/Windows64-target marker, a completion log line, no detected compiler/import failures, no changes to original snapshot files and no source drift. Compiler failure is failed; prerequisite failures/timeouts are blocked; unselected stages are not_run. Concurrent user changes invalidate attribution without being overwritten.

The report includes command, version, Git context, file hashes, elapsed time, diagnostics and evidence locations. A fresh unique output directory is used every time. No automatic deletion or cache reuse is implemented. First import is relatively expensive.

M1 does not execute scene gameplay save hooks. Reviewed source startup hooks skip the UI generator in batch mode. This is not a general security sandbox: new Editor initialization/plugins need side-effect review. Disk source preservation is hash-verified; normal gameplay save behavior and save isolation during Play Mode remain M2 concerns.

The compile target is Windows64 editor compilation, not all platform symbols, a player build, a visual check or gameplay success. Some no-graphics shader warnings may appear and are outside visual validation.

## Validator self-check commands

```powershell
python -B -X utf8 -m unittest discover -s Tools/Validation -p test_unity_validation.py -v
& ./Tools/Validation/Invoke-UnityValidation.ps1 -NegativeControl
```

The negative-control command returns 0 when the expected compiler failure is correctly detected and source is preserved. Its recorded Unity compile status remains failed. Never present this as a passing game compile.

## Legacy UI test — superseded

Assets/JinHo/Editor/UIIntegrationSmokeTest.cs is batch-only, invokes SettingsMenuUIBuilder, writes assets/settings, opens SampleScene and enters Play Mode. Its initial immediate-gameplay assertion disagrees with the enabled start menu. Separate saved-asset checks from generator checks, reconcile expectations and isolate all persistence before using it. Do not run it on the live editor project.

ResourceSpawnZoneSceneSetup also has mutating setup methods. No such setup methods ran during M1.

## Additional change-scoped candidates

Select only relevant scenarios: UI modal transitions and rebinding; inventory weight boundaries and pickup; offering return/zero-assimilation semantics; movement/roll/slopes/fall; monster collision and boss re-entry; portal fade/bounds/themes; isolated save corruption/version handling. Record target scene, overrides, steps, expected/observed outcomes and visual evidence. These are future candidates, not current test passes or authorized gameplay fixes.

Reference: [Unity 6.3 command-line arguments](https://docs.unity3d.com/6000.3/Documentation/Manual/EditorCommandLineArguments.html).

## M2/M3 actual execution — 2026-09-23

Both milestones were approved. Runtime harness implementation is complete. Latest run: **passed**, reason `runtime_completion_confirmed`; 236/236 recorded assertions passed (including 10/10 M3 inventory checks). Guard tests: 17 passed. Runtime duration including snapshot/import: 351.49 seconds. Original source preserved: True; user persistence preserved: True.

Run: `C:/Users/Master/Documents/Codex/UV/bea366455a09`. Structured evidence: [M2/M3 results](Evidence/M2-M3-validation-results.json). Detailed scope: [runtime specification](Cycles/C002-runtime-validation.md).

```powershell
& ./Tools/Validation/Invoke-RuntimeValidation.ps1
```

The first run (285fc64ca59b) passed the UI flow and all 10 inventory model checks, but correctly failed the overall no-error requirement due to ArgumentOutOfRangeException in UnityEditor.Search.SearchDatabase during editor initialization. No game code appears in that exception stack. The harness now allows an eight-second quiet import/compilation interval before entering Play Mode. The retry result above is observed evidence; it does not prove the precise editor-race cause or permanent elimination. Both trial logs are retained. All project inputs and original user persistence remained unchanged in the first run too.

Company/product names are changed only in the copied ProjectSettings before launch. Persistent JSON and Windows PlayerPrefs use a unique test namespace. Copied Assets are not rebuilt; unexpected changes to existing snapshot inputs fail the run. Every run retains its own test data; no automatic cleanup occurs.

The current runtime probe supersedes the legacy UIIntegrationSmokeTest entry point. It is a Tools text template, injected into the copy only. Five 1920x1080 canvas renders cover menu, display settings, controls, sound and empty inventory. These are isolated canvas views rather than complete Game View captures. Input request injection does not verify physical keyboard-to-inventory action wiring; direct controller calls do not verify mouse raycasts or every button listener. M3 covers inventory weight/count/events, not every gameplay system. Player builds, unsaved editor state, combat, movement physics, portals, offering, save corruption and process-restart persistence remain unverified.

## C003 movement refactor — 2026-09-26

Isolated Unity editor compilation and Play Mode assertions passed: 300/300 checks, including 64 movement assertions. Runner guards: 17 passed. Overall run BLOCKED by original source and user persistence drift; preservation not established. Auto-review rejected retry and no retry executed. A separate original Unity process was present; drift attribution is unproven. MovementCases.cs.txt is injected beside RuntimeProbe.cs.txt; the completion classifier requires its completion assertion. Cases cover ground/air/reversal/wind/jump/gravity numbers, Flame release/resistance, UpDraft target/float behavior, generic mode dispatch, priority/idempotence/invalid handles, destroyed sources, multi-collider exit, unchanged Y exit velocity, overlapping regions, roll/knockback, and disable/re-enable cleanup.

Evidence: [C003 result](Evidence/C003-movement-results.json). Full run: `C:/Users/Master/Documents/Codex/UV/8aca78039e5d`. Integration checks invoke real component callbacks synchronously in Play Mode on disposable objects; they do not validate physical keyboard or actual physics-trigger traversal. Visual behavior and player builds remain not_run. No live scene reload or save occurred.

## C004 offline script architecture check — 2026-09-26

After 84 first-party script dispositions, the installed source matched the staged hashes and all 59 moved script `.meta` files matched their originals byte-for-byte. Offline Roslyn compilation against the installed Unity references passed for Assembly-CSharp and Assembly-CSharp-Editor (0 errors). This did not start the Unity editor or validate asset import, scene references, gameplay, visuals, save restart or builds. A separate standalone behavior-comparison executable failed with an unhandled .NET exception and must not be cited as a passing test. See [C004 evidence](Evidence/C004-script-refactor.json).

## C005 callback input — 2026-09-26

Installed Unity-reference Roslyn compilation passed for Assembly-CSharp and Assembly-CSharp-Editor (0 errors). The staged inputactions JSON preserved all existing Player actions and bindings and added only ToolScroll plus its mouse-wheel binding, with unique IDs. Source, interaction component and inputactions GUID metadata stayed unchanged. PlayerInteraction button polling was removed; the complete staged runtime and Editor code compiled with zero errors. This does not establish Unity asset import, mouse-wheel direction, keyboard/gamepad event phases or UI blocking at runtime. See [C005 scope](Cycles/C005-callback-input.md).

## C006 per-action callback refactor — 2026-09-26

Runtime and Editor offline Roslyn assembly compilation passed with zero errors. Static checks found all 11 action-specific callbacks with matching subscription/removal phases, unchanged public consume methods and serialized toolController field, and no Update/global dispatcher. The original script GUID metadata was unchanged. This is not Unity Play Mode or actual input-device verification. See [C006 specification](Cycles/C006-per-action-callbacks.md).


## C008 campaign — 2026-09-27

Use Tools/CampaignValidation/Invoke-RuntimeValidation.ps1 for the new saved NotionCampaign scene. The existing Tools/Validation entry remains the SampleScene suite. C008 checks economy, input/movement, day/night, real shop buttons, additive smithy travel, bow consumption/fallback, boss reset, map rewards, spawn counters and slot reload. See Evidence/C008-validation.json for the executed run and source/save preservation; do not substitute earlier C007 evidence. Windows player build and two-process restart evidence are recorded separately in the C008 handoff report.


## C009 authored town — 2026-09-27

Use Tools/TownValidation/Invoke-RuntimeValidation.ps1 for SampleScene town interactions. Fresh run 49c05a6df906 passed 31/31 with no errors; source, user saves and preferences preserved; no unexpected copied-input changes. Actual F input events exercise ForestIn/ForestOut/BlackSmith/pawnshop/Store. Checks cover fade opacity/input lock/arrival/fall reset, 5×5 sales and exact prices, exhausted buyback stock, shop filters/insufficient funds/bag capacity, no HUD shortcuts, smithy exit, night departure restriction and menu manual save. Settings entry uses the existing controller API; pointer clicks use button listeners. Three runtime renders were inspected at 1920×1080. This is not physical input/raycast, full terrain traversal, all-resolution or player-build evidence. See Evidence/C009-validation.json. The C008 template was adapted for asynchronous return and renamed shop rows; the full C008 suite was not rerun for this change.


## C010 town UI and map

Tools/TownValidation/Invoke-RuntimeValidation.ps1 now executes the C010 suite. Run d02acf8aae0a passed 46/46; no source/user persistence drift. It covers C009 routes/fades/trades/save access plus NPC farewell, owned bag tiers, living smithy/rack/workshop/left exit, day graphic wiring, real D-input town ground traversal, facility coming-soon, delivery whole stack and empty-target drag, single proceeds collection and E-input town inventory. Button listeners and pointer handlers are invoked programmatically; this is not a physical mouse raycast test. Eight renders at 1920×1080 were inspected. The capture adapter temporarily moves overlay canvases above world sorting layers and restores all canvas settings. No new build, full-map, all-resolution or restart-restoration evidence. C008/C009 evidence is historical. See Evidence/C010-validation.json.

Strict runner overall status remains failed: the isolated copy cleared the existing dynamic menu-font atlas. Semantic comparison confirmed only generated glyph/character/atlas caches changed; font configuration/references, source font and user persistence are intact. No validator protections were relaxed and the original failure record is retained. See C010-font-cache-review.json.


## C011 smithy integration

Tools/SmithyValidation/Invoke-RuntimeValidation.ps1 runs the C011 suite. Isolated Unity 6000.3.11f1 run 5d8e53fe5eb2: 38/38 Play Mode checks, zero runtime/compiler errors. Runtime and Editor offline compilation passed. Original SampleScene is byte-identical; all 5382 baseline inputs were checked before installation. Six captures inspected. Final verification reused the isolated Library after copying and hashing all source inputs; the preceding fresh import run is retained separately. Strict runner overall status remains FAILED because the isolated copy cleared the existing dynamic menu-font cache. Semantic review proves only generated cache data changed; the original font/source and user persistence are intact. The failed result is retained; no validator guard was relaxed. The probe uses real F/D input events; UI listeners and inventory methods are invoked programmatically. Actual mouse raycasts, every resolution, every crafting recipe, full combat and player build remain untested. Scene reload restores a test save, not a process-restart test.


## C012 alloy/shop checks

Run `Tools/AlloyValidation/Invoke-RuntimeValidation.ps1` for a fresh isolated test copy. Unity 6000.3.11f1 isolated Play Mode: 85/85 checks, zero runtime/compiler errors. Runtime and Editor offline compilation passed. Verified C011 physical smithy regression, real monster damage, blood/guard/body armor effects, fire refresh/coexistence/expiry, actual projectile collision and live burn Update, 0.6-health save/scene reload, shop filtering/selection/receipts/failed purchases/bag capacity and three farewell branches. Isolated Library reused with input hashes verified. Strict overall result remains FAILED: only generated glyph/character/atlas caches in two isolated dynamic font assets changed. Semantic cache review passed; source inputs and user persistence are preserved. No validation guard was relaxed. UI button listeners are invoked programmatically. Guard uses an Input System mouse-state event; projectile tests use the actual prefab collider. Burn timer boundary checks also call Tick deterministically. No exhaustive enemy AI, every resolution, physical mouse playthrough, process-restart save test or player build is claimed.

## C019 field platforms and background — 2026-09-28

The isolated Unity 6000.3.11f1 scene-save/reload checks passed for five distinct ledges, 20 collider-free visual tile cells, eight one-way polygons, one S-drop controller and forest/cave loops. A Play Mode player-sized capsule physics probe passed upward, horizontal, landing and S-drop cases, the existing upper landing, three background states and cave recycling. Forest/deep-cave captures were inspected. The probe manually advanced physics and moved the camera; full authored-player traversal, fade timing and a build were not run. The copied Library emitted package-cache/search startup exceptions despite passing results; see Evidence/C019-field-platforms.json.

## C029 player Aseprite visuals — 2026-09-28

In an isolated Unity 6000.3.11f1 copy, `PlayerVisualAuthoring.Build` completed and saved the three player scenes. `C029VerifyPlayerVisuals.Run` reopened each scene and sampled all nine Animator states, checking both sprite sources and matching frame names; all 27 state/scene samples passed. Unity editor C# compilation had no errors. The copied Library reported an unrelated Visual Scripting package-cache exception. A rendered Game View, real keyboard input, physics traversal with the new fitted collider and player build were not checked. See [C029 evidence](Evidence/C029-player-visuals.json).

## C032 field spawn and boundary checks — 2026-09-28

Targeted C# compilation passed with zero errors using installed Unity 6000.3.11f1 DLLs and the existing runtime assembly. Static checks confirmed five resource zones/68 targets, seven monster zones/12 targets with deer forest and all cave species cave, one golem target, all resource pickup references and cave-mask surface candidates. These do not establish that all targets spawn during Play or that Rigidbody2D, AI and knockback respect the boundary at every physics step. The isolated Unity editor could not connect to Licensing Client and did not enter Play Mode. Before claiming the gameplay loop works, run the manual field→town→smithy→field traversal and boundary/harvest checks described in [C032](Cycles/C032-field-spawns.md).

## C033 current field Play Mode results — 2026-09-28

The isolated non-batch Unity editor connected to Licensing Client and completed three Play Mode probes with code 0. Actual scene components produced 68/68 resources, 12/12 monsters, 12/12 correct initial forest/cave assignments and 12/12 forced cross-boundary returns. The real harvest/drop/automatic-pickup and two fade scene transfers delivered six wood to the town chest; crafting-domain operations produced and equipped a wood sword, and the returned field player's tool had damage 15. One crafted plank also returned in the player's bag. The tests directly invoked resource, crafting and travel methods in Play Mode. Do not treat them as physical keyboard/mouse, bed/night UI, natural AI knockback, timer-expiry, visual or build checks. The copied Library logged Visual Scripting package-cache and SearchDatabase startup exceptions; no clean-editor result is claimed. Details: [C033](Cycles/C033-field-play-validation.md), [evidence](Evidence/C033-field-play-validation.json).
# C034 quick-interaction hand animation — 2026-09-28

The isolated runtime C# project built with zero errors. In isolated SampleScene Play Mode, a valid tool-independent quick interaction called through the PlayerInteraction entry point completed exactly once; Animator entered `Attack` and its saved `PlayerHand` SpriteRenderer advanced through `playerhand_sp.aseprite` frames 24, 25 and 26. The first probe exposed an existing Animator evaluation-order bug; after the fix, the repeat passed. This does not verify hardware F/mouse input, rendered overlap, every UI interaction, or a player build. The copied editor still reports unrelated package-cache/search startup exceptions.
# C035 resource spawner links and scale — 2026-09-28

An isolated C# project build passed with zero errors. Unity 6000.3.11f1 Play Mode loaded the saved `FieldMapStructureTest` scene and populated all five resource zones to 68/68. Every spawned TreeZone resource held the scene `ItemDropSpawner`; directly harvesting a spawned tree and thicket generated six and four `ItemDropInteractable` instances respectively. Asset renderer heights were tree 3.36, thicket 3.40, and stone/coal/steel 3.3728 world units against the saved player height 2.216784. A first uniform-scale run reached only 13/18 TreeZone resources; reducing horizontal sprite scale while retaining the larger height restored 18/18. Physical interaction input, rendered sprite appearance, natural pickup and a player build remain unverified.

# C036 integrated tool selection and item art — 2026-09-28

The isolated `Assembly-CSharp` and `Assembly-CSharp-Editor` builds completed with zero errors. A static audit verified all 14 field resource/monster drop prefab references are covered by the 16 field-to-catalog links, and all 13 catalog sprite keys resolve in the shared art set. The eight installed files were guarded by original hashes and verified after installation; `git diff --check` passed. Two non-batch Unity 6000.3.11f1 attempts stopped before Play Mode because Licensing Client initialization failed. The prepared C036 buy/equip/save-reload/town-field probe did not run. See [C036 scope and manual check](Cycles/C036-integration.md); C033 remains previous-loop evidence only.

## C038 isolated licensing and scene validation — 2026-09-28

On this Windows host, a Unity Editor launched within Codex's filesystem sandbox could not connect to the already running Unity Licensing Client named IPC channel. The client answered other Editors; launching **only the isolated copied project** through the ordinary Windows process context connected immediately. Do not infer account-license expiration from the sandboxed `Connection to channel LicenseClient-Master refused` line alone, and do not reset the user's license or terminate their Editors for this condition. C038 Play Mode probes cover selected T2 tool persistence and town/field transfer, the gather→craft→field loop, and saved-scene hierarchy/camera/background/platform wiring. They use direct calls and do not replace input, Game View, build or clean-startup checks. See [C038](Cycles/C038-license-and-runtime-validation.md).
# Field cave floor occlusion and map-only markers — 2026-09-30

An isolated Unity 6000.3.11f1 copy compiled the new floor controller and map code with zero C# errors. Its Edit Mode probe found the authored `CaveFloorOcclusion` and four cave-palette art tiles at x54–57/y-40, sorting order 10; all scene object IDs remained unique. Play Mode confirmed that the floor-mask canvas is enabled in upper/deep cave and disabled in forest, the original cave light retains intensity 1.12 and shape falloff 5, and a rebuilt field map contains no altar, entrance, exit or player markers. After forcing the batch-mode camera to the player (Cinemachine remained at its forest position following a test teleport), the captured Game View showed `Golem hairpin foothold A` art under the player: `C:/Users/Master/Documents/Codex/UV/d7ef7f756b00/evidence/cave-floor-foothold.png`. The Play Mode report is `cave-floor-probe.txt` in the same directory. `Camera.Render()` excludes Screen Space Overlay UI, so the floor-mask screenshots do not verify its final composite or transitions while walking. The map-button input path, live Editor and player build were not exercised by this focused probe. `git diff --check` reports only pre-existing trailing whitespace in an unrelated generated font asset.

## Stone Golem deep-cave replacement — 2026-09-30

Isolated Unity 6000.3.11f1 (`C:/Users/Master/Documents/Codex/UV/d7ef7f756b00/project`) compiled and ran the authored field scene with one fixed golem. A focused Play Mode probe passed: no proactive attack while dormant; ordinary `TakeDamage` ignored; pickaxe 13 damage, awake transition and three visual-only falling chips; five-step walk at 2.4 units/s; close-range slam 25 damage once and 1.25x character-physics knockback (6.25 horizontal in the probe); ranged rock harmless in flight, 20 damage once on landing; return along the cave route to the fixed spawn without healing; death cleanup and exactly one core drop; shortened offscreen timer respawned one fresh dormant golem at the same X. The saved field timer remains 420 seconds and was not waited out. Editor import found all 93 Aseprite frames and six tags; both golem prefab variants have the six frame groups and projectile reference, and the projectile visual points to `StoneOre.png`. Five cave camera renders were inspected for idle, awake, slam, throw and death poses. Evidence: `evidence/golem-combat.txt`, `golem-author.txt`, `golem-spawn.txt`, `golem-visual.txt`, and `golem-*.png` in that isolated workspace.

The probe called the interaction path directly and used a synthetic player target; real mouse aim/tool input, manual movement around the blocking collider, the complete authored respawn wait, Screen Space Overlay composite, live Editor and standalone player build remain unverified. The Notion URL was inaccessible here, so the provided written specification is the checked design source.

## Stone Golem attack tuning — 2026-09-30

In isolated Unity 6000.3.11f1, both prefab variants imported with rock impact radius 2.2, cooldown 0.65, initial walk 4.5 and repeat walk 2.8 seconds. The StoneOre projectile prefab imported at scale 0.14. The authored field Play Mode combat, return, death/drop and shortened respawn probe passed after these changes. A separate landing-boundary probe applied 20 damage once at a 2.15-unit collider distance, no damage at 2.45 units, and none during flight. Physical player input and a standalone build remain unverified.

# New art and workshop UI — 2026-09-30

- PASS (isolated Unity import): 53 matching item icon Sprite resources, eight shared UI Sprite resources, four player scene references (including rack idle), eight animation clips and both VampireBat prefab frame arrays. Source tags: player idle/walk/run/attack, bat flying/damage/attack/idle/dead.
- PASS (isolated Unity Play Mode): workshop background and five overlay Sprite bounds, facility and chest navigation, pre-existing workbench station screen, recipe icon, representative item icons, field bag button with no number and white-to-red tint after filling the bag.
- PASS (visual asset composite): the six 196×108 forge PNG layers align as a single workshop picture. This is a source-art composite, not a live Game View capture.
- UNVERIFIED: live Editor Game View appearance, physical pointer interactions and player build.
- Evidence: `C:/Users/Master/Documents/Codex/UV/d7ef7f756b00/evidence/new-art-validation.txt`, `new-ui-probe.txt`, `new-art-play.txt`, `workshop-composite.png`.

# Field scene relocation path — 2026-09-30

- PASS (static): `Assets/Scenes/FieldMapStructureTest.unity` and `.meta` exist with the original GUID `226af1e445de8cd49a1a75253bf6f526`; the enabled build entry matches. No active C# or map-reference tool retains the old gathering-folder path.
- PASS (isolated Unity Play Mode): the updated `FieldSceneTravel.FieldScenePath` is loadable and `SceneManager.LoadScene` reaches the moved field scene with 11 roots. Evidence: `C:/Users/Master/Documents/Codex/UV/d7ef7f756b00/evidence/scene-move.txt`.
- UNVERIFIED: full town→field fade/save handoff in the live Editor, physical input, and player build.
# Published Notion usability pass — 2026-09-30

Read-only planning comparison covered the published system hub, database hub, field UI, crafting UI, town/time, resource and monster pages. Existing project gameplay decisions, including prior Codex changes, were retained when they differed. `dotnet build Assembly-CSharp.csproj --no-restore -v:q` completed with zero errors and four existing runtime warnings. The six edited C# files passed scoped `git diff --check`; the unrelated scene/font changes still cause full-tree whitespace warnings. Static scene data puts TownGround top at y=62 and ForestIn center at y=64; the serialized capsule suggested a 0.49-unit foot gap, but `SpriteColliderAutoFit2D` resizes it during Play Mode, making the actual arrival gap about 1.88 units before correction. The 3.5-unit maximum permits this correction while retaining an upper bound. An isolated Unity 6000.3.11f1 Play Mode probe passed direct town start (foot gap 0.005), direct field start (foot gap 0.005), town→field travel (arrival gap 0.005), quit-without-save confirmation/cancel, HUD band anchors and pointer-event simulated 0.4-second bag tooltip. Evidence: `C:/Users/Master/Documents/Codex/UV/55b78198fd83/evidence/report-final.txt` and `report-fixed3.txt`. Rendered HUD overlap at 1920×1080 and smaller resolutions, physical pointer/keyboard input, full fade/save round trip, naturalness of every scene object and a player build remain unverified.
# World object size audit — 2026-09-30

- PASS (isolated Unity 6000.3.11f1 Editor): measured 474 SpriteRenderers across SampleScene, FieldMapStructureTest, smithy interior and active world prefab sets. Player art is 1.54×2.22 world units. The active resource, monster, town and smithy objects were compared by role; only the new bat import was a clear size outlier.
- PASS (source/import): `bat (2).aseprite` bytes and GUID are preserved. Its import PPU is 18, matching the earlier bat source and attack-effect import; prefab root scales and serialized gameplay values are unchanged.
- PASS (isolated Unity Play Mode): the field VampireBat body art is 1.13×0.87 with fitted collider 0.81×0.77; the campaign spawn variant is 0.81×0.63 with fitted collider 0.59×0.55. Both exceed the prior near-invisible sizes 0.20×0.16 and 0.15×0.11. The smaller collision shape follows the existing 72% width / 88% height auto-fit policy.
- PASS (test-only visual): the isolated world-camera PNG places the resized field bat beside the player in the forest and shows a legible smaller flying creature. This is a relative-size comparison, not an authored cave encounter.
- UNVERIFIED: live Editor Game View aesthetics, every animation-frame outline, physical combat, and player build. The audit does not claim a visual inspection of every Tilemap tile or screen-space UI element; those are not independent world object scales.
- Evidence: `C:/Users/Master/Documents/Codex/UV/55b78198fd83/evidence/size-audit.csv`, `size-audit-bat18.csv`, `bat-size-play.txt`, and `bat-player-scale.png`.
# Pawnshop and equipment-shop validation — 2026-09-30

`dotnet build Assembly-CSharp.csproj --no-restore` with output under `C:/Users/Master/Documents/Codex/BuildChecks/PawnShopFinal4` completed with zero errors (43 warnings, mostly Unity packages and existing unused fields). `git diff --check` passed for the changed implementation files. An initial isolated Play Mode runner copied the saved Assets, Packages, and ProjectSettings to `C:/Users/Master/Documents/Codex/UV/0db988d867ec/project` and changed only its company/product identity plus an injected test probe. Unity 6000.3.11f1 repeatedly lost its license-client connection before the probe's completion marker; the isolated test process was stopped after 646.6 seconds. The user then instructed that future Play Mode checks attempt connection for no more than 30 seconds, then skip and record **unverified** when unavailable. A fresh near-final implementation copy at `C:/Users/Master/Documents/Codex/UV/368c231286d7/project` followed that limit: Unity produced no completion marker in 30 seconds, the subprocess timed out, and the source snapshot showed no drift. Subsequent morning-receipt and skipped-week settlement changes compiled but were not retried in Unity because the external licensing blocker had not changed. Both Play Mode outcomes are **blocked, no-marker**, not passed. Evidence: each run's `evidence/result.json` and `unity.log`.

The probe was prepared to exercise saved-recipe gating, shop and bag prices, bag upgrades, partial sale, collateral and same-day repayment, simple interest/carryover, weekly-debt priority, equipped weapon/arrow sale, old JSON without loans, and town trade-tab construction. None of these Play Mode assertions ran. Physical pointer drag/highlight/cancel, rendered layout, scene travel persistence, and player build also remain unverified. The user's live Editor and saves were not used for this check.

# Player information panel — 2026-09-30

Runtime and Editor C# builds: zero errors. An isolated Unity 6000.3.11f1 Play Mode probe passed panel construction in SampleScene and FieldMapStructureTest, modal pause/close, empty and multiple loans, changed unpaid interest, repayment removal, weekly carry/settlement, and manual slot save/reload without same-day interest mutation. Evidence: `C:/Users/Master/Documents/Codex/player-info-probe-20260930.txt` and `player-info-data-probe-20260930.txt`. Anchor coordinates keep the button below town gold/field status and move the field goal right of it. Rendered Game View appearance, physical pointer/Esc operation, and player build remain unverified.

# Equipped tool HUD icons — 2026-10-01

Runtime and Editor C# builds passed with zero errors. An isolated Unity 6000.3.11f1 Play Mode probe checked visible HUD Image.sprite values for wood sword→hammer→bow, unselected weapon replacement while axe was selected, tier-2 axe/pickaxe, selection index, unequip to branch art, missing-icon fallback, manual slot reload and direct FieldMapStructureTest load. Evidence: `C:/Users/Master/Documents/Codex/tool-icon-probe-20261001.txt` and `tool-icon-probe-unity-2.log`. The project has shared `linked_axe`/`linked_pick` art but no per-tier sprites. Actual Game View appearance, physical button input and fade travel were not checked.

# Tool rack stroke removal — 2026-10-01

The pre-change WebGL build completed successfully at 02:13:08 before source editing. After removing the tool-rack stroke ranges, `dotnet build Assembly-CSharp.csproj` and `Assembly-CSharp-Editor.csproj` passed with zero errors using isolated output paths under `C:/Users/Master/Documents/Codex/BuildChecks/ToolStrokes*`. The Editor validator now checks one and eleven valid tool gestures, and rejection of zero gestures; these validator cases were updated but not executed in Unity. Static inspection found no active tool-rack recipes with the same ingredients and tool. Post-change Play Mode, physical gestures, rendered UI, and player build remain unverified.
# Equipment quality and recipe-edge verification — 2026-10-01

Static CSV check: 59 active recipes use 66 distinct ingredient/output IDs; all 66 have authored graph nodes. `dotnet build Assembly-CSharp.csproj --no-restore -v:q -clp:ErrorsOnly` passed with zero errors and four warnings. In an isolated Unity 6000.3.11f1 Play Mode run, `RecipeQualityGraphProbe` confirmed that an undiscovered output is hidden, discovery of the four-ingredient stone-arrow recipe reveals all four input nodes and edges, an undiscovered alternative edge remains hidden, material quality is neutral, and weapon quality still applies. Evidence: `C:/Users/Master/Documents/Codex/recipe-quality-graph-probe-20261001.txt` and `recipe-quality-graph-unity-20261001.log`. The first attempt failed to compile only because the temporary probe reused a local variable name; its correction passed in the second attempt. The isolated Editor's SearchDatabase startup exception did not stop the successful probe. Physical mouse input, visual composition in Game View, legacy-save migration in a real play session and player build remain unverified.
# C054 architecture refactor validation — 2026-10-01

Roslyn extraction of the current 318 Assets C# files found zero syntax and semantic errors. Runtime and Editor `dotnet build` each passed with zero errors (four and fourteen warnings). The first runtime attempt preceded Unity project-file regeneration; the repeated build passed. An isolated Unity 6000.3.11f1 batch import exited with code 0 in under 30 seconds; it did not run an authored scene, instantiate spawn zones, or visually inspect Game View. These gameplay checks remain **unverified**. Existing scene/prefab GUID references were not edited.
# C055 code learning documentation verification — 2026-10-01

Fresh Roslyn scan: 186 first-party files from 318 Assets C# files; zero syntax and semantic errors. Generated documentation: 206 Markdown files, 2,270 method/accessor sections, 436 Mermaid diagrams; every source SHA-256 and relative Markdown link passed the staged validator. The generated area was copied into the existing Obsidian path. Unity Play Mode and rendered Obsidian graph display were not used to verify these explanations. The earlier C054 isolated Unity batch import exited successfully within 30 seconds, but did not test in-scene spawning.

# C056 called-function documentation verification — 2026-10-01

The same source snapshot produced 4,916 call explanations in 1,538 method sections. The staged validator passed source hashes, method counts, relative links, and Markdown fences with zero errors. Source and documentation were compared again before publication. Unity Play Mode was not run because this change affects documentation only; rendered Obsidian appearance remains unverified.
# New-game starter weapon verification — 2026-10-01

Runtime and Editor C# builds passed with zero errors (four and fourteen warnings). The saved catalog `branch` attack value is 5. `Sword.asset` also has damage 5 and an icon reference to `TypeSword_0`; `GameUI.prefab` serializes the same sprite into `ToolSelectionHUD.swordSprite`; the HUD uses it only when a present weapon has no item icon. Both authored player scenes include a Sword tool slot. Static extraction found zero syntax and semantic errors, and all 206 Obsidian docs passed hash/link checks. Play Mode Game View, title-to-town, and save/relaunch remain **unverified**.

# Town-to-forest arrival verification — 2026-10-01

`dotnet build Assembly-CSharp.csproj --no-restore -v:q -clp:ErrorsOnly` passed with zero errors and four warnings after both travel changes. Static inspection of the saved field scene found the cave entrance at x=28 with crossing offset -1, so the staged and final arrival x=0 is on the forest side of the x=27 boundary. `git diff --check` passed for the touched source files. An actual town→forest→cave→town→forest Play Mode round trip and Game View observation were not run; behavior across those transitions is **unverified**.
# Screen layout prefabs and isolated UI test scene — 2026-10-01

The runtime `Assembly-CSharp.csproj` built with zero errors and 25 warnings after `ScreenLayoutTemplate` excluded smithy stage root positioning. Static checks confirmed the nine requested prefab selections and standalone `UITestScene` script reference. Unity Play Mode and Game View were not run. To review, open `Assets/Scenes/UITestScene.unity` alone, enter Play Mode and inspect all nine sidebar entries, weapon shop tabs, sample transfers and label/scroll placement. Then inspect the corresponding production screens in their game scenes; mock test data does not validate actual transactions or drag/drop.

# Crafting failure preservation verification — 2026-10-01

After the fuel correction, `dotnet build Assembly-CSharp.csproj --no-restore -v:q -clp:ErrorsOnly` passed with zero errors and four warnings; `Assembly-CSharp-Editor.csproj` passed with zero errors and 14 warnings. The existing Editor domain validators now expect full material return, no failure output, and furnace fuel consumption, but were not executed in Unity. Static inspection found one failure-return branch in `CraftingService.Finish` and the failure result panel contains only `실패했습니다.` plus its confirmation control. A Play Mode run, visual result capture, and save/reopen after failure remain unverified.
# Real UI test scene — 2026-10-01

`Assembly-CSharp.csproj` compiled with zero errors after replacing mock guide controls with production UI controller calls. Static scene checks found 49 unique YAML object IDs, the saved camera, blacksmith Canvas/controller, EventSystem, test controller and `loadSavedGame: 0`. No other scene file was edited by this task. The available computer-use surface exposed no native Unity app, so Play Mode/Game View, physical pointer input, trades and drag/drop were not verified. Open `UITestScene` alone, enter Play Mode, select all nine screens, inspect the Game View with F10, and exercise real controls. Do not infer production save behavior from this memory-only test.
# UITestScene Stack type ambiguity — 2026-10-01

The current regenerated `Assembly-CSharp.csproj` initially failed with two CS0104 errors at `UITestSceneController.cs:92-93`. Both sample item stacks require the `Blacksmith.Stack` domain type, rather than `System.Collections.Stack`; qualifying both constructors fixed the errors. Final runtime and Editor builds passed with zero errors (four and fourteen warnings). No Unity Play Mode or Game View check was run.

# UITestScene production HUD and item grant — 2026-10-01

Runtime and Editor C# builds passed with zero errors (four and fourteen warnings). Static inspection confirms the standalone scene still disables blacksmith save loading; its temporary `SmithingLoop` blocks saving, the town/field UI components use disabled world-state controllers, and the editor God Mode overlay returns early in `UITestScene`. The selector grant updates only the two temporary chests. Play Mode, rendered HUD/map layout, button clicks, inventory drag/drop and save isolation under live interaction remain unverified.

# Pawn shop wording — 2026-10-01

Search found no remaining `담보 대출`, `담보대출` or `대출하기` text in `CampaignTradeUI.cs`. Runtime C# build passed with zero errors and four existing warnings. Rendered pawn shop copy was not checked in Unity Game View.

# Iron, byproduct and loot icons — 2026-10-01

Static checks compared source and `Resources/ItemIcons` PNG SHA-256 values for 38 mapped item IDs, found 38 distinct GUIDs and zero mismatches, and verified four linked loot assets plus four drop prefabs reference the expected Sprite GUID/file ID. The two iron hammer IDs already contain identical source pixels. No code changed, so C# compilation was not repeated. Unity import and rendered sprites were not checked; Play Mode is excluded by user preference.

# Town trade filters and purchase inventory — 2026-10-01

`Assembly-CSharp.csproj` built with zero errors and four warnings after the final filter adjustment. Static inspection found no `대화 끝내기` text in the merchant dialogue source. The `WeaponShopBuy` layout has 44 unique YAML object IDs, no missing child IDs and an `OwnedInventory` placement guide. Pawn rejects tool types while allowing equipped non-tool items; shop resale rejects non-tools and equipped sources. Rendered layout, pointer interaction and save round trips were not checked; Play Mode is excluded by user preference.

# Field resource and pickup proportions — 2026-10-01

Static YAML checks covered all 12 gathering drop prefabs: SpriteRenderer draw mode and dimensions, calculated world height 0.56–0.74 and width below 1.1. Four tree/thicket node and campaign-spawn prefab heights calculate to 3.6–4.34 against the player’s approximately 2.2-unit sprite height. Two broken attribute-stone drop sprite references were replaced with the existing stone sprite and different tints. `git diff --check` passed. No C# changed. Unity import, final visible alpha bounds, collision feel and Play Mode were not checked.

# Assimilation HUD scene restore — 2026-10-01

Code inspection found `LiquidCircleGaugeHUD.Awake` creates and binds the gauge before `FieldSceneState.Start` restores saved assimilation. The restore event formerly animated the display from the temporary zero. `FieldSceneState` and `CampaignController` now call `SyncRestoredValue` after `RestoreRemainingHealth`, which invokes an immediate source sync. `Assembly-CSharp.csproj` built with zero errors; targeted `git diff --check` passed. The actual town→forest fade, value continuity and saved-game behavior remain unverified in Unity because Play Mode is excluded by user instruction.

# Deer movement and deerZone bounds — 2026-10-01

Static inspection confirmed `FieldMapStructureTest.unity` has a `deerZone` BoxCollider2D (62.95846 units wide) and `FieldMonsterSpawnZone2D` instantiates the Deer prefab beneath that zone. The controller reads its parent spawn area, reserves the entire body width inside both X edges and clamps horizontal knockback overflow. Both deer prefabs serialize new speed/acceleration/braking/boundary values. `Assembly-CSharp.csproj` compiled with zero errors; targeted `git diff --check` passed. Actual deer patrol, flee motion, edge turn timing and sprite playback remain unverified in Play Mode per user instruction.

# Player reverse-direction response — 2026-10-01

Static search found `turnAccelerationMultiplier: 2` in all three authored player scenes and default `2f` in PlayerMovement. NormalMovementMode multiplies acceleration only while target and current X velocity have opposite signs; walk/run caps remain 5/7.5. `Assembly-CSharp.csproj` built with zero errors and 25 existing warnings. Whole-file `git diff --check` is blocked by unrelated trailing whitespace in existing SampleScene changes; no Play Mode was run.

# Stone golem head drop — 2026-10-01

Static inspection confirms DeadState captures the head position before disabling the Rigidbody2D/collider and starting death frames, then SpawnCore uses that snapshot after the death animation. Both golem prefabs serialize zero additive drop offset. `Assembly-CSharp.csproj` built with zero errors and 25 existing warnings; targeted `git diff --check` passed. Actual sprite alignment, gravity landing and pickup were not checked in Play Mode by user request.

# Title continue button without a save — 2026-10-01

Static check: `RefreshSavePresentation` disables Continue and sets its `CanvasGroup.alpha` to 0.45 when there is no valid save; a valid save restores alpha 1 and enables the button. `Assembly-CSharp.csproj` built with zero errors and 43 warnings; targeted `git diff --check` passed. Confirm visually with an empty save directory, then with one valid save, when runtime UI checks are allowed. Play Mode was not run by user instruction.
# UITestScene retirement — 2026-10-01

`UITestScene` and its scene-only controller have been deleted. Historical UITestScene checks below remain as evidence of earlier work, but instructions to open or run that scene are no longer current. Review the production screens in their own scenes instead. The legacy `UIIntegrationSmokeTest` targets `SampleScene` and remains in the project; do not treat it as a UITestScene-only script. Play Mode remains excluded by user instruction.

# Shared inventory drag grid feedback — 2026-10-01

`Assembly-CSharp.csproj` compiled with zero errors and 25 warnings; targeted `git diff --check` passed. Static inspection covered the common drag preview, selection and target-border code; exact empty-cell targeting in smithy chest/bag; quantity overrides for one crafting ingredient/fuel and one trade-stage item; and decorative non-raycasting empty slots in town, delivery and trade grids. Physical pointer targeting, scrolling from an occupied slot, visual border contrast, drag cancellation and transfer outcomes remain unverified because Play Mode was not run by user request.

# Shrink-only UI text fitting — 2026-10-01

`Assembly-CSharp.csproj` and `Assembly-CSharp-Editor.csproj` built with zero errors (25 and 39 warnings). Static review confirmed `FitText` fixes `fontSizeMax` at the authored size, sets a lower bound at 72% or 10 pt, disables text-container auto sizing, and does not mutate RectTransforms. Shared creators and direct text creation paths in the title, game UI, smithy, field HUD, maps, inventory, offering, trade and storage now use it. No Play Mode or rendered layout check was run; actual line wrapping, minimum-font readability and localization remain unverified.
