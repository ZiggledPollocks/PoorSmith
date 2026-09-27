# C008 Notion campaign (lava excluded)

User intent: implement most linked non-lava systems on the existing batterMap integration. Notion is strictly read-only. Attached UI/map guidance is a design reference, not authorization for external actions. Keep original scenes and all unrelated working-tree edits. No commits or pushes.

Approved decisions: assimilation remains health; offerings heal. Day is gathering, night crafting; bed skips day or advances night to morning. Missing values may use visibly labelled temporary balance. Existing sword baseline remains damage 10, 2 attacks/second; convert any Notion interval-factor inputs at their boundary instead of silently changing existing ToolData units.

## Boundaries

Create a separate NotionCampaign scene, preserving SampleScene and its unsaved editor state. Reuse player, input, movement, resource prefabs/workflow, monster controllers, UI settings, blacksmith service, and SmithingLoop ownership. Build saved Tilemaps and individual objects, never flatten the map/UI into one image. Lava is absent from the new map and routes; existing unrelated lava source/assets remain untouched.

Extend the existing integrated progress snapshot with additive campaign data (currency, deliveries, pawn stock, upgrades, chest/altar/warp IDs, exploration, position). Preserve version-1 files and reject corrupt/unknown references. Save slots contain the same validated snapshot; do not make another independent inventory. Basic terrain/prop geometry derives from the field blueprint and wind diagram; existing tile/sprite art supplies reusable appearance. Illustrative maps have no extractable terrain tiles, so record reused art rather than pretending pixel-exact extraction.

## UI element plan

| Element | Static/dynamic | Data/input/result | Owner |
|---|---|---|---|
| Campaign HUD | panels/icons static, all numbers dynamic | date/night, animated gold, debt, map/save buttons | CampaignUI observes SmithingLoop and CampaignState |
| Town shop | slot/paper sprites static | select goods; inspect; purchase one upgrade; reject insufficient gold | CampaignEconomy + tool/inventory APIs |
| Pawn/delivery | repeated item row prefab | select/count; transact; morning settlement; click proceeds | CampaignEconomy |
| Dialogue/confirm | panel static, text dynamic | timed text, space skip, yes/no; prevent gameplay leakage | CampaignUI modal owner |
| Map/warp | parchment/icon sprites static | explored world cells, current position, discovered landmarks; destination confirmation | CampaignExploration + CampaignUI |
| Save slots | repeated row prefab | automatic + four manual, day/playtime, explicit save/load confirmation | SmithingLoop progress serializer |

## Map element plan

| Region/element | Notion evidence | Composition / flow / test |
|---|---|---|
| Surface/cave/deep cave | field revision 3dada1bd22db803999ecdb1d5faaa535 | branched continuous Tilemap terrain, colliders, reusable resource/monster prefabs; walk/jump/camera bounds tests |
| Wind temple | 3bfda1bd22db800cbce7ece4fadcb9bb | separate spatial area in same scene, platforms/updrafts, entry and boss; no lava branch |
| Town | 3e1da1bd22db8052aa83fea3f9ed553e | individual pawn/equipment/smith/facility buildings, chest/warp and gates, existing player interaction |
| Cave transition | field/UI sources | back/front layers and region colour/background, explicit enter/exit state, camera limits |
| Treasure | field revision | stable-ID chest prefab, one-time wind crystal, opened state persisted across sessions |
| Warp/altar | return/assimilation UI | stable-ID interactable prefabs; map confirmation, day-limited altar use |
| Respawn | field revision | resource timers 120/240/300 sec; offscreen-only monster countdown 120/180/420 sec; paused while visible |

Temporary balance starts at weekly debt 100G, tool upgrade 120G×current tier, bag 150G×tier (+50kg/tier), facility 200G×tier, arrows 5G, missing armor 4. Expose in CampaignRules and mark provisional in UI. No invented finished quest/ending/cutscene art. Final report must separate implemented features, tested interactions, visual differences and remaining optional work.

## Implemented behavior

- New startup scene: Assets/Campaign/Scenes/NotionCampaign.unity. Original SampleScene and BlacksmithShop scene bytes are preserved. Town, continuous surface/cave/deep-cave routes, wind platforms/updrafts and arena use saved Tilemaps, colliders, prefabs and existing movement/combat.
- CampaignState extends the existing atomic SmithingLoop snapshot. CampaignEconomy owns transfers, prices, pawn stock, delayed delivery collection, upgrades and weekly debt. CampaignUI only requests transactions. CampaignController coordinates world IDs, death/return, upgrades and date transitions.
- Gather by day; nap in the smithy to craft at night; sleep at night to advance the morning. Next-morning delivery proceeds stay in the chest until collected. Weekly debt takes gold then randomly seizes stored/bag/delivery goods; unpaid debt carries and the third warning ends the game.
- Equipment and pawn shops have reusable row buttons, details, quantities and atomic transactions. NPC typewriter dialogue supports Space skip. Menus use actual TMP text, images, buttons and scroll areas.
- One-time crystal chest, explored-cell map with world-derived terrain and extracted landmark icons, discovered warp destinations, daily altar lock/reset, saved position/health/rewards/currency/upgrades. Automatic plus four manual slots share the same serializer; load rejects unknown references and preserves files on failure. Save/discard quit choices are separate.
- Overweight allowance is five percent; carrying above base capacity slows movement by ten percent. Under-tier tools have per-attempt 5% (tier one) or 10% (tier two) drops and never deplete higher-tier resources. Axe/pick swings query nearby contacts. Sword/dagger/hammer reuse weapon geometry; bow charges, spends equipped arrows and falls back to a branch when empty. Keys 1/2/3 select weapon/pickaxe/axe in the campaign; tool HUD buttons also select them.
- Wind region movement bonus is additive with provisional wind armor. Blood weapon hits heal provisionally. Armor defense and shield mitigation apply to existing integer health. Wind spirits drop three particles. Boss exit confirmation resets HP/phase on acceptance; first/repeat boss rewards differ.

## Temporary values and explicit limits

Known raw prices: wood 10, branch 5, stone 9, coal 15, ore 20 G. Missing prices use CampaignRules: equipment 80, crafted material 30, other 15; arrows 5. Existing explicit catalog prices remain. Quality multipliers apply; pawn pays floor(50%) and charges 100% for buyback. Armor default 4, blood weapon heal 1, wind armor +3% per piece, shield blocks 50%; these are provisional and the HUD/details say so. Integer health rounds damage upward; the source's tenth-point damage representation is not implemented.

The map is a compact playable interpretation, not the blueprint's proposed 15-minute traversal at full scale. Town structures are reusable generated geometry; forest/cave art comes from installed assets. Wind distant backdrop and map icons derive from read-only Notion image assets. This is not a claim of pixel-identical final art. No lava route is placed. Existing lava source/assets remain for preservation.

Remaining: manual F/click pickup with 0.7-second hover priority (existing automatic attraction retained), complete blood-armor retaliation/fire-alloy status effects, final facility upgrade design (currently marked temporary fuel refill), polished merchant portraits/animations and some hover/filter details, long-form map scale/content pacing. Respawn timers/live enemy HP are not persisted; revisiting via scene reload repopulates them. Equipped items are excluded from debt seizure; stored/bag/delivery goods are eligible. Home warp unlock is represented by the first boss reward flag. No final balance, complete manual map traversal or full manual boss fight is claimed.

## Verification

See Docs/Evidence/C008-validation.json and the task C008 report for the final executed results and preservation checks. Use Tools/CampaignValidation/Invoke-RuntimeValidation.ps1 for the new campaign. Its wrapper reuses the approved snapshot/persistence-isolation runner, injects CampaignValidate only into a copy, waits for quiet import, and requires matching positive completion evidence. The legacy SampleScene suite remains a separate scope. Input device injection verifies callbacks and physical movement, not a human input session. Shop actions invoke real UI button listeners; screenshots are offscreen renders of the actual scene and canvases. Source generators refuse execution outside an isolated validation identity.
