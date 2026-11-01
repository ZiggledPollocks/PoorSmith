# C007: field and blacksmith integration

Date: 2026-09-27. User-authorized implementation, based on the current saved working trees.

## Agreed behavior

- Keep assimilation as health: zero means death; offerings restore it.
- Deliver gathering -> return to smithy -> craft/equip -> return to the existing field.
- Sales, debt and a complete day/economy cycle are outside this slice.
- Missing weapon stats use the existing Sword damage (10) and speed (2) as temporary baselines. Quality changes damage, not attack speed. Missing armor/alloy values remain unset and are identified in the UI.
- Notion is read-only. No Notion edits or permission changes were made.

## Ownership and flow

`SmithingLoop` bootstraps only a field containing `InventorySystem`, using `Resources/SmithingLoopContent.asset`. It freezes the field clock, gates field input, hides its cameras/canvases and loads the existing BlacksmithShop scene additively. Field objects and the saved SampleScene are preserved. Shop animations use unscaled time. Departure restores the same field instance.

Material links translate existing numeric ItemData IDs to the blacksmith string IDs. A single inventory transaction consumes the mapped field stacks; the shop receives the same quantities. Unknown resources remain in the field inventory. The shop has no demonstration supplies in hosted mode. Resources moved into its bag are returned on departure only when mapped and within capacity; failures remain owned by the shop. Crafted equipment remains in the shared rack and melee weapon stats are applied to a runtime clone of the existing Sword slot. Existing ToolData assets and IDs are not rewritten.

The standalone blacksmith keeps its own save file. The integrated game uses a separate `smithing-loop-v1.json`, atomically storing field inventory and blacksmith storage, recipes, fuel and equipment together. The automatic-save option gates automatic writes; the explicit bottom-right save button performs manual progress saving. World respawns, field position and health are session state, not claimed as persistent world saves. Invalid/future saves block overwriting. Legacy player-state files retain their schema and are protected independently.

## Fixes

- Failed workbench combinations return each original ingredient with 50% probability, preserving quality; other stations retain their byproducts.
- Notion's strict `greater than` mastery thresholds resolve as 6/31 successes for materials and 4/11 for equipment. Level-three bonuses roll per output item: 5% extra material or 10% one-grade equipment upgrade. Finest may upgrade to Master.
- Successful heated metal processing continues to the next station with its output selected.
- Resource acquisition remains recorded after consumption; next-step hints, hidden-node suppression, focus navigation, a separate discovered-recipe list and material placement are available.
- Offerings reserve selections without removing inventory ownership. Cancel, disable and repeated return cannot lose or duplicate reserved resources; commit validates/consumes the whole cost once and heals.
- All health mutation paths share one alive-to-dead notification.
- Player loading distinguishes missing, valid, invalid, unsupported, unknown-tool and I/O failure; failed loads prevent later automatic/manual overwrite. File writes use replace-with-backup.
- Boss disable cancels attacks/wind and clears coroutine state; arena contact refresh handles changed player life state.

## Known data and scope limits

The linked Notion pages include older prototype recipes and newer recipe definitions that disagree. Existing detailed 129 active recipes and their explicit stroke ranges were retained; ambiguous/retired theme recipes were not silently re-enabled. The newer workbench failure rule supersedes the prototype's tangled-byproduct rule. Alloy n/m tables and several equipment stat tables contained no readable values. Bow projectile gameplay, balanced armor/alloy combat and final art/sound are not supplied by this slice. Basic melee swords are the verified complete loop.

Do not run scene generators on the live project to install this feature. Keep SampleScene and its Tilemaps intact. No commit/push is authorized by this task.

## Validation

Results and exact limitations are recorded in the task's verification report and CURRENT entry. Compilation, domain assertions, runtime controller assertions, visual captures and Windows build results must be reported separately. The runtime checks call real components and UI button handlers; they do not establish physical keyboard/mouse traversal or a complete manual combat playthrough.


Verified 2026-09-27 on Unity 6000.3.11f1: standalone blacksmith recipe assertions 3,182 and new domain assertions 22 passed. Fresh isolated Play Mode run `19844ae414ee` passed all 388 assertions, with zero compiler/runtime errors, no unexpected copy mutations, no source drift, and unchanged pre-existing persistence in the tested namespace. Test-only company/product identities kept real project saves out of these runs. The source was a saved snapshot of the working trees; unsaved live-editor state was not inspected or reloaded.

Windows64 development build from run `9f0e85c117c8` succeeded (0 errors, 3 build warnings). The built player restored the previously equipped wood sword, entered/left the shop, and saved successfully (exit 0). The latter copy contains validation-only harness files; these are not installed in the game projects. Gameplay source is identical to the final passing run. Build warnings include unused existing fields and unavailable optional Unity Cloud symbol upload; a transient duplicate EventSystem warning occurs during additive scene loading. Physical input, full boss combat and all recipe-specific visuals remain outside these automated assertions.
