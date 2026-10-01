# batterMap project memory

Adopted: 2026-09-23, cycle C001. These documents are concise execution references; Korean learning material lives in Obsidian.

## Entry points

- [Current handoff](CURRENT.md): status, unresolved decisions, next action.
- [Architecture](Architecture.md): inspected responsibilities and source boundaries.
- [Validation](Validation.md): evidence, untested procedures, isolation prerequisites.
- [Approved collaboration policies](Collaboration.md): detailed approved policies; the repository AGENTS.md supplies the concise entry rules.
- [C001 adoption specification](Cycles/C001-project-memory.md).
- [C002 M1 Unity validation](Cycles/C002-unity-validation.md): implemented runner and verified compile/failure-detection evidence.
- [C002 M2/M3 runtime validation](Cycles/C002-runtime-validation.md): saved UI and inventory checks with isolated persistence.
- [C006 per-action player callbacks](Cycles/C006-per-action-callbacks.md): action-specific handlers and lifecycle wiring.
- [C005 callback-only player input](Cycles/C005-callback-input.md): Input System wheel action and callback state handling.
- [C004 script architecture](Cycles/C004-script-architecture.md): full first-party disposition, folder/pattern decisions and verification limits.
- [C004 evidence](Evidence/C004-script-refactor.json).
- [Source snapshot identifiers](Evidence/source-manifest.json).

- [C009 authored town](Cycles/C009-authored-town.md): saved object routes, fades and trade UI.
- [C053 authored title and death return](Cycles/C053-title-scene-and-death-return.md): first build scene with saved menu/settings UI and direct town respawn button.

## Learning notes

Local vault root: `C:/Users/Master/Documents/Obsidian Vault`.
Vault-relative entry: `Files/batterground/Wiki/00_프로젝트_홈.md`.
Systems: `Files/batterground/Wiki/시스템`.
Log: `Files/batterground/Wiki/변경로그.md`.
Decisions: `Files/batterground/Wiki/설계결정`.

These are location references for this machine, not verified cross-application clickable links. Preserve existing notes outside Wiki. The current source-mirrored code analysis begins at `Files/batterground/코드해체분석기/00_프로젝트_스크립트_지도.md`.

## Retrieval and maintenance

Read CURRENT and only the specifications/source relevant to the request. Consult related wiki rationale when useful. Do not reread all history by default. Keep intended behavior, current implementation, and actual verification distinct. Update references affected by a change and append one Korean log line; detailed learning updates follow the approved policy. If the vault is unavailable, record pending updates in CURRENT rather than claim synchronization.

No background synchronization or automation is configured. The repository AGENTS.md now routes project work to these entry points. Final operational rules were approved and installed on 2026-09-23.

- [C010 town planning](Cycles/C010-town-planning.md): town layout, stores, inventory and living smithy; tool equip decision pending.

- [C011 physical smithy](Cycles/C011-physical-smithy.md): real interior, quick stations, shared tools/items and saves. Supersedes the C010 tool-equip pending note.

- [C012 alloy combat and equipment shop](Cycles/C012-alloys-shop.md): damage precision, blood/fire effects and planned purchases/dialogue.

- [C013 field map structure](Cycles/C013-field-map-structure.md): map-only forest/cave test scene and isolated Play Mode verification.

- [C014 inverse cave mask](Cycles/C014-inverse-cave-mask.md): sketch-gray traversable cave voids and exact colliding white walls.

- [C015 player-fit cave](Cycles/C015-player-fit-cave.md): narrower three-band cave, concise walls and player-scale route approximation.

- [C016 field-map test player](Cycles/C016-field-test-player.md): safe saved-player copy and follow camera.

- [C017 branched PixelFantasy cave](Cycles/C017-branched-palette-cave.md): sketch-inspired passages and palette-facing rock.

- [C018 field shortcuts and prefab life](Cycles/C018-field-shortcuts-life.md): cave crossings, field prefabs and entrance background transition.

- [C019 one-way field platforms](Cycles/C019-one-way-field-platforms.md): cave ledges, S drop and camera-following backgrounds.
- [C020 field travel and authored visuals](Cycles/C020-field-travel-authored-visuals.md): first switchback foothold, slope animation, town-field fades and edit-time smithy/player visuals.
- [C021 staggered corner footholds](Cycles/C021-staggered-corner-footholds.md): two more x/y-offset ledges and actual-player jump/roll route up and down.
- [C022 corner foothold y order](Cycles/C022-corner-foothold-y-order.md): current lower/middle/corner art y coordinates -12/-13/-14 and reverified player route.
- [C023 town/field bounds and prompt](Cycles/C023-town-field-bounds-confirmation.md): corrected town walls and matching Yes/No travel confirmation in both scenes.
- [C024 playable gathering field](Cycles/C024-playable-field-parity.md): SampleScene camera behavior, field bounds, background switching and non-crafting campaign state in the field scene.
- [C025 field boundary and barrel fix](Cycles/C025-field-boundary-and-barrels.md): contact travel prompt, empty field fauna/resources, forest backdrop coverage and stationary town façade barrels.
- [C026 scene-local player handoff](Cycles/C026-scene-player-handoff.md): prepared in-memory transfer, durable save and failure-safe town/field travel.
- [C027 slope, entrance and loop audit](Cycles/C027-slope-entrance-loop-audit.md): marked slope idle/jump fixes, field backdrop blend and read-only Notion gap assessment.
- [C028 town and field map UI](Cycles/C028-town-field-map-ui.md): discovered-area maps in town/forest/cave and smithy façade player marker.
- [C039 forest/cave entrance fade](Cycles/C039-field-entrance-fade.md): level mouth terrain, same-scene fade landings, interior-art separation and isolated Play Mode/visual checks.
- [C029 player Aseprite visuals](Cycles/C029-player-visuals.md): paired body/hand animation in the saved player scenes.
- [C030 CSV content source](Cycles/C030-csv-content.md): Notion-backed item values, catalog/recipe tables, field views and guarded existing drops.
