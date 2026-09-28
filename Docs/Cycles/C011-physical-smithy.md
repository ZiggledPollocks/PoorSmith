# C011 — Physical smithy and shared gathering/crafting inventory

## Authorized scope

The user requested town BlackSmith quick interaction to enter a decorated map, with equipment rack, chest, bed and crafting door following the smithy plan, and gathering/crafting tools and items connected. Notion was read only. Reference: https://app.notion.com/p/3e7da1bd22db809bb1f1c300fa70856f (equipment crafting system/smithy); parent https://app.notion.com/p/3b6da1bd22db80d4a8e3ec22cd0569e5. Previous approved decisions remain: assimilation is health; day gathering/night crafting; provisional balance labeled; no lava theme; facility goods remain coming soon.

## Implementation

- Saved Resources/SmithyInterior prefab, spawned by CampaignController. The existing player, input, collision physics and Cinemachine camera traverse a decorated room at (-252,62). SampleScene is byte-identical; no live scene reload or user Tilemap regeneration.
- BlackSmith F fades into the room. Four trigger stations use the existing quick-interaction contract: equipment rack, storage chest, bed, crafting door. The left doorway automatically fades back to the town smithy without confirmation.
- Panels use the existing additive BlacksmithShop scene and crafting domain. Closing/back returns to the physical room. The standalone UI home remains a fallback.
- Rack has weapon, shield (arrow for bow), armor and two gathering tool slots, with existing click/drag transfer, two-hand constraints, aggregate statistics and hover tooltips. Character paper doll is not invented; the plan permits omission without sprites.
- Chest retains bag/storage lists, quality, search/categories, insertion order, click/hold/right-transfer/drag and discard. Day nap becomes night; night sleep advances one morning. Both heal and display day/debt transition; cancellation preserves time.
- SmithingItemBridge transfers ownership between field ItemData inventory and smith catalog stacks, including crafted equipment and quality. Original high-quality material IDs remain valid; other items use smith:<catalog-id>:<quality>. Version 1 old saves remain readable. Older binaries do not necessarily understand newly saved IDs.
- GatheringEquipmentBridge converts legacy tier state to one equipped axe/pick only once, guarded by toolsLinked. Rack ownership drives actual PlayerToolController slots; unequip removes the tool and saves that absence. Shop T2/T3 tool purchases create bag items for manual rack equipment per the rack plan. Prior tools return to storage.
- Existing original tool icons are used, with serialized UI sprite fallbacks. Generated item carry weight is 1 kg until planned values exist and is labeled provisional; known materials retain original weights. Existing approved weapon baseline and quality multipliers remain.
- Over-capacity export preserves the remaining stack in the smith bag; no automatic discard. Recipe/minigame tools (knife/plane/saw etc.) remain existing crafting station selections, because the plan has no owned-item purchase/consumption rules for them.

## Reproduction

Play SampleScene, return to town, stand beside BlackSmith and press F. Walk on the indoor floor; press F beside rack/chest/bed/crafting door. Use chest to place carried crafting materials in storage. Buy tools at the equipment store, move them from bag to storage and equip at the rack; return to gathering and check tool tier. Craft and equip a weapon or put it in the bag; close the panel and travel to the forest. Walk left to leave the smithy. Save/load should retain interior position, quality stacks and unequipped tools.

## Verification scope

Offline runtime and Editor compilation, isolated Play Mode with real Input System F/D events, panel listeners and inventory operations, saved prefab references, camera/fade/floor traversal, shared tool tiers and item quality, day/night sleep, save/load through scene reload. Renders are inspected at 1920x1080. UI listener invocation is not a physical mouse raycast test. No player build, every-resolution UI pass, all crafting recipes/combat enemies, or process-restart restoration is claimed. Existing C010 evidence is historical and is not relabeled as C011.


## Recorded result

Isolated Unity 6000.3.11f1 run 5d8e53fe5eb2: 38/38 Play Mode checks, zero runtime/compiler errors. Runtime and Editor offline compilation passed. Original SampleScene is byte-identical; all 5382 baseline inputs were checked before installation. Six captures inspected. Final verification reused the isolated Library after copying and hashing all source inputs; the preceding fresh import run is retained separately. Strict runner overall status remains FAILED because the isolated copy cleared the existing dynamic menu-font cache. Semantic review proves only generated cache data changed; the original font/source and user persistence are intact. The failed result is retained; no validator guard was relaxed.
