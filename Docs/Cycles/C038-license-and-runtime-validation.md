# C038 licensing diagnosis and scene-loop Play Mode validation — 2026-09-28

The saved `SampleScene` and `FieldMapStructureTest` scene cleanup is now exercised in an isolated Unity 6000.3.11f1 Editor. No user license reactivation was necessary. The system Licensing Client continued to answer other Unity Editors, while a Codex-sandboxed validation process could not connect to its named IPC channel. Launching only the isolated copied project outside that sandbox established the client handshake immediately. The live project and user saves were not played or written.

The first integration run exposed a real tool selection bug: after saving a selected purchased T2 axe, a reload restored it in `SmithingLoop.Start`, then `CampaignController.Start` reapplied the same gathering equipment. `GatheringEquipmentBridge.Apply` unnecessarily destroyed/recreated the occupied tool slot. `PlayerToolController.SetToolSlot(null)` selected the first available tool before the axe was rebuilt. `GatheringEquipmentBridge` now skips reconstruction when the catalog item ID and tier already match, and restores the previous selected ID after a genuine replacement when that ID still exists. This preserves the existing bag/chest and save formats.

The corrected isolated Play Mode runs completed:

- C036 integration: 14 assertions passed. A T2 axe was bought, equipped, selected and saved; a town-scene reload restored it; town→field→town travel kept its catalog ID and tier; a crafted item had field art. Completion marker `C036_INTEGRATION_PASS`.
- Field game loop: six tree drops were automatically collected, delivered to the town chest, used to craft and equip a wood sword, and carried back to the field with one plank. The field sword damage was 15. Completion marker `FIELD_LOOP_DONE code=0`.
- C038 scene probe: 25 assertions passed for functional roots, removal of old town gathering/wind roots, town and field camera bounds, smithy prefab, cave backdrop preview disabled during Play, three one-way slopes, and a resource zone collider/spawner. Completion marker `C038_SCENE_PROBE_PASS`.
- Offline Unity-reference Roslyn runtime and Editor compilation passed with zero errors; pre-existing warnings remain.

These runs used direct test calls for harvest, purchase, crafting, travel and scene inspection. They did not verify physical keyboard/mouse input, actual slope traversability, rendered Game View alignment, natural monster combat/respawn timing, a player build or measured frame performance. The copied editor logged Visual Scripting package-cache import, SearchDatabase and Package Manager authentication exceptions; the successful probe markers therefore are not a zero-error Editor startup claim. See `Docs/Evidence/C038-runtime-validation.json` for scene/source hashes, scope and results.

The earlier C036 and C037 documents record the validation state at the time of those cycles. This C038 result supersedes their Play Mode blocker for the checks listed above.
