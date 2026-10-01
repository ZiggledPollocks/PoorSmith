# C054 — responsibility and dependency audit

## Phase 1: current structure

The current `Assets` tree contains 317 C# files. The existing Korean code map identifies 185 first-party scripts; the other 132 are vendor/sample or otherwise outside that map. `C052-script-organization.md` already moved the 43 Campaign runtime scripts into `Core`, `Combat`, `Travel`, `Field`, `Town`, `World`, and `UI`, retaining their `.meta` GUIDs. `JinHo/Crafting`, `JinHo/Gathering`, shared inventory, player, combat, spawning, and settings are already grouped by feature. The working tree has extensive unrelated source, scene, prefab, and asset changes; a broad move would make reference review less reliable.

The dependency path is UI/input → campaign/field/crafting orchestration → domain rules and state → persistence. Unity events, serialized component references, and runtime `GetComponent`/`Find` calls cross these folders. The 185 per-script code-analysis notes record 2,267 function/property sections and now include static caller, callee, assignment, and engine-call graphs. Those relationships are not evidence of runtime execution.

## Phase 2: candidate decisions before editing

| Candidate | Decision | Reason and risk |
|---|---|---|
| Four private `SetLayerRecursively` implementations in `ResourceSpawnZone2D`, `MonsterSpawnManager2D`, `FieldMonsterSpawnZone2D`, and `ItemDropSpawner` | Extract one `SpawnHierarchyLayers.SetRecursively(GameObject, int)` helper; retain all four MonoBehaviours and their public/serialized data | Their recursive behavior is the same spawn-time policy. The private methods have no serialized UnityEvent or external API references. Keep each caller's current `layer >= 0` condition. |
| Eight resource interactables | Keep separate | `ResourceHarvestWorkflow` already centralizes harvesting; the remaining components carry distinct serialized tier, drop, sound, and resource identity. A base component would migrate Inspector fields. |
| Monster controllers and state files | Keep separate | The shared state machine and targeting helpers already handle exact common logic. Species have different state transitions, animation, combat, and spawn rules. |
| `FieldMapPreviewCamera` and `FieldMapTestPlayerCamera` | Keep separate | Both lack saved-scene references, but `Tools/MapReference/*.cs.txt` refers to their component types. One pans for editing; the other follows a test player. Search-only deletion would break those workflows. |
| Town/field camera bounds and return/passage gates | Keep separate | They use different scenes, lifecycles, and destinations; the field camera uses Cinemachine while the campaign bounds clamp a Camera. |
| Generic UI factory and feature-specific UI controllers | Keep separate | View creation is shared; menu state and gameplay policies remain in the owning feature. |

No folder moves or class deletions are planned in this pass. The existing feature-oriented folders are already within a useful depth, and no candidate has a proved dead-code status across scene, prefab, editor-tool, and runtime use. Large controllers such as `SmithingLoop`, `BlacksmithController`, and `CampaignController` merit later focused responsibility reviews, but their current shared save/scene/UI state and concurrent edits make a broad split unsafe here.

## Phase 3: reference boundary for the chosen extraction

The four existing script GUIDs occur in saved scenes/prefabs (respectively 2, 2, 1, and 3 serialized assets, counting the Unity recovery scene). The change does not move, rename, or remove any component class, serialized field, script asset, or `.meta`. It replaces only calls to private recursive methods. Text search found no `m_MethodName: SetLayerRecursively` in saved scenes/prefabs/assets. Spawned children receive the same layer as before; invocation remains guarded by each caller's existing conditions. The new helper is a plain static C# class with no Inspector or scene reference.

## Phase 4 and 5: implementation and checks

Implemented the shared layer helper and replaced the four private call sites without moving or renaming their MonoBehaviours. Existing `layer >= 0` guards remain at the callers. The helper has no serialized fields or scene component identity. Runtime and Editor `dotnet build` each completed with zero errors (four and fourteen warnings respectively). The first runtime attempt started before Unity regenerated its project file and could not see the new source; the second attempt passed after regeneration. A scoped whitespace check passed. Gameplay, scene instantiation, and visual results remain unverified; no broad refactor was performed. No commit or push.
