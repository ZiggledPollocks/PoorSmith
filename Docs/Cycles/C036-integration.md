# C036 — unified gathering, crafting and town progress

Date: 2026-09-28. Scope: the playable `SampleScene` ↔ `FieldMapStructureTest` loop and the additive smithy. Notion was read only. The [smithy plan](https://app.notion.com/p/2f99c4aa4b9c8330bdd5011700308da3) specifies a carried bag and a separate storage chest, so both ownership locations remain. The [crafting plan](https://app.notion.com/p/39d9c4aa4b9c83cfa8d1018296a404b2) expects the same items in the crafting inventory and chest.

## Behavior and data flow

- All 14 drop-prefab references in the field resource/monster `ResourceData` assets resolve to one of the 16 `SmithingLoopContent.materials` item links. These preserve field `ItemData` identity while the bridge transfers ownership to catalog `Stack` IDs. Gathered items move into the chest on field→town; bag contents move back to the field on town→field. The transfer remains atomic with the C026 prepared snapshot.
- The existing catalog `town_axe_*` and `town_pick_*` IDs now become the runtime `ToolData.ToolId` of the corresponding equipped field tool. A purchased tool stays in the smith bag until equipped at the rack, as specified. The tool's tier, name, slot and saved equipment entry still drive field behavior. The equipped weapon likewise receives its catalog ID.
- `SmithingLoop.Progress.selectedToolId` now carries the selected tool through save/reload and scene handoff. The integrated loop suppresses `PlayerSaveSystem`'s separate `player-state-v1.json` read/write, which could otherwise restore an unrelated tool selection. That component continues to serve standalone player scenes. Existing version-1 integrated saves without `selectedToolId` remain readable and keep the default selection; existing separate player saves are left untouched.
- `SmithingLoopContent.itemArt` holds the 35 existing smithy UI sprite references. Generated field `ItemData` for crafted items resolves icons from this serialized content even when the field scene has no `BlacksmithView`. Build content validation rejects catalog sprite keys absent from this shared art set.

The catalog and recipe CSV files, existing IDs, item weights, bag/chest contents, equipment rules, scene assets and save version were not migrated. The smithy mini-game tools remain station choices; the Notion plan does not give them field ownership or purchase rules.

## Verification

- Isolated `Assembly-CSharp` and `Assembly-CSharp-Editor` builds: zero errors (package and existing-project warnings remain).
- Static audit: 14/14 resource/monster drop prefab references link to catalog items; all 13 catalog sprite keys resolve among the 35 serialized art references or the two linked tool sprites; original/staged file hashes were checked before installing eight files, then installed hashes were verified. `git diff --check` passed on the affected paths.
- An isolated Play Mode probe was prepared to buy/equip a T2 axe, save/reload selected tool, travel town↔field and check crafted item icons. Two non-batch Unity 6000.3.11f1 launches stopped at Licensing Client initialization before entering Play Mode. Therefore the new behavior has **not** passed an actual post-change Play Mode test. C033's earlier gather→return→craft/equip→depart pass is historical evidence, not a pass for C036.

Next manual/isolated check: in the equipment shop buy the T2 axe, equip at the rack, select it, save/reload, enter the field and verify tier/selection; carry a crafted plank and inspect its icon after field reload; return with harvested materials and verify no duplicated bag/chest stacks. Physical UI input, visual Game View, process restart and player build remain unverified.

## C038 follow-up

The previously blocked Play Mode check now passes in an isolated non-batch Unity Editor launched outside Codex's IPC-restricted sandbox. A first run found a selected-tool reset during redundant equipment application; `GatheringEquipmentBridge.Apply` was fixed and the T2 axe purchase/equip/save/reload/town-field-town run passed 14 assertions. See `C038-license-and-runtime-validation.md`. The original C036 result remains the historical evidence for that cycle.
