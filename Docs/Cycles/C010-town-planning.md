# C010 town, stores and smithy planning

## Approved scope

Read-only Notion hub `3b4da1bd22db8009a51ccbaf1a467557` and its town, pawnshop, equipment shop, smithy, time and delivery pages were compared with the saved SampleScene. The attached Korean UI/map guide requires independently composed sprites, meshes, tiles, controls and live data. It does not authorize Notion edits. No Notion content was changed.

User approved left-to-right pawnshop → equipment shop → smithy → facility shop. The empty facility specification is implemented as a building and dialogue with products coming soon; no fuel transaction. Earlier decisions remain: assimilation is health, daytime gathering and nighttime crafting, existing provisional balance explicitly identified, menu and automatic saves retained, no play-HUD smithy/save shortcuts.

## Implemented

- SampleScene Town subtree: existing building transforms reordered, independent wall/roof/window/door/icon facade prefabs, reusable forest layers and village props, real Tilemap ground with a ground collider, town camera/boundary updates, forest portals at both ends, delivery chest and town warp beside smithy. The saved gathering field, player and other scene roots are not regenerated.
- TownDialogue: typewriter reveal, Space reveal/next line, cyclic greetings, transaction state across buy/sell views, farewell followed by Space exit. Facility dialogue and storefront show coming soon. Generic NPC dialogue copy is provisional; original image-only portrait/dialogue art is not claimed to be reproduced exactly.
- Pawnshop keeps 5×5 slots, left/right item popups, quantities, half-price sales and actual sold-stock buyback. Shop keeps filters and insufficient-money feedback; all bag tiers stay visible, owned stages have no price and cannot be repurchased; next-stage ordering is enforced.
- TownHud: animated gold count, day/night semicircle with Day and debt countdown, inventory button. The existing Inventory input opens a town panel with name/count/price and no gathering weight UI. Date is absent from the gathering HUD and crafting stations.
- TownStorageUI: shared smith bag/chest and campaign delivery lists; one/whole-stack transfer, drag into occupied or empty viewports, next-morning sale proceeds cell and single collection. No second inventory owner was introduced.
- SmithyHomeView: independently composed living interior with window, equipment rack, chest, crafting-room door, bed and left exit. Click or A/D + F controls existing views. Walking to the left exits without confirmation. This is a lane inside the existing point-and-click smithy UI with the field player paused, not a new physics-based interior scene. Crafting room retains the existing five stations and shared crafting/equipment systems.
- White hover outlines for interior controls and inventory; day/night ambient background tint; existing bedtime transitions show day/debt information.

## Read-only source coverage

Town `3e1da1bd22db8052aa83fea3f9ed553e`; shop `3e1da1bd22db80b0904fe57b4e0b305a`; pawn `3e1da1bd22db8094b8d0e9e501156d00`; facility `3e1da1bd22db80ec9dfad11c12ea8dbf` (empty); smithy `3e7da1bd22db809bb1f1c300fa70856f`; time `3e1da1bd22db80b1bc56efb546f59061`; delivery `3e1da1bd22db808db395fa03f5e70593`; warp UI `3bcda1bd22db80e4a08ec016be7c4af3`.

Crafting overview `3bdda1bd22db80009100c53b4511a55a`, workshop layout `3d1da1bd22db800da828c1c12eacecbc`, and all five station UI pages were read. Existing domain code already provides selection limits, material return, recipe access, auto craft, strokes, five anvil hit zones, quench gauge, timing and quality. C010 does not re-certify all recipes or rewrite this domain. Final hand/NPC art, sound, detailed discovery-scroll effects and cinematic transitions remain presentation gaps; existing functional feedback is retained. Lava is excluded.

## Open decision

Purchased pickaxe/axe items are specified to enter the bag. Whether to auto-equip or require user selection remains unanswered. The current playable upgrade transaction is retained while this decision is pending; item definitions/metadata are staged preparation and are not sold as inventory items yet. This is a known remaining planning mismatch, not a completed feature. Do not infer a new equipment ownership or migration rule from the preparation data.

## Verification

Recorded after the final fresh isolated run in `Docs/Evidence/C010-validation.json`. Runtime/Editor compilation, saved component wiring, Input System keyboard events, transactions, physical movement along a town segment and 1920×1080 runtime renders are separate evidence. Original editor remains untouched; preserve unsaved editor work before reopening the changed saved scene. No new player build, all-resolution review, full-map traversal, process-restart restore or physical mouse raycast test is claimed. No commit/push for C010.

Fresh run `d02acf8aae0a`: 46/46 checks, zero runtime/compiler errors, source and user persistence preserved. Eight captures inspected.

Strict runner overall status remains failed: the isolated copy cleared the existing dynamic menu-font atlas. Semantic comparison confirmed only generated glyph/character/atlas caches changed; font configuration/references, source font and user persistence are intact. No validator protections were relaxed and the original failure record is retained. See C010-font-cache-review.json.
