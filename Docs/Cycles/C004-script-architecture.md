# C004 script architecture and folder organization

Approved request (2026-09-26): inspect every script in batterMap, apply useful design patterns, and organize code into folders. Preserve existing game rules, serialized component fields, GUIDs, save schemas, scene/prefab links and unrelated edits. The previous movement-mode work is the baseline.

## Scope and decisions

The 215 C# files under Assets were inventoried: 84 first-party scripts and 131 vendored/sample scripts. Vendor/sample scripts remain in their supplied paths to preserve update ownership. Every first-party file received an explicit disposition in the Korean audit report; no-op decisions are intentional. All 84 source paths were accounted for, 59 components/contracts were relocated and 22 existing scripts were refactored. Seventeen new source files and 47 new folder assets were added. Every relocated script retained its original `.meta` file byte-for-byte.

- Interfaces (`IBehaviourState`, `ITextStore`, `ISwordTargetQuery`) live in `Assets/JinHo/Scripts/Assets`, beside the existing movement contract.
- Resource `MonoBehaviour` components retain fields and per-resource tool/tier/drop settings; `ResourceHarvestWorkflow` owns the repeated interaction sequence. Do not serialize the helper.
- Four monster stateful controllers use one `BehaviourStateMachine` each. Their concrete nested states live in species-specific partial files and retain the original behavior. `LivingPlayerTarget` and `SpriteFrameClock` share exact repeated calculations. AggMonster, WindSpirit and StormBoss retain their distinct implementations.
- `ISwordTargetQuery` chooses thrust or swing geometry. An unexpected style value retains the previous fallback circle without swing-arc filtering.
- `ITextStore` has file and PlayerPrefs adapters. Existing JSON version, keys, writes and load-failure policy remain. This change does not make file replacement atomic.
- `RuntimeUIFactory` shares object, image, text and rect creation. Controllers own their font lifecycle and screen rules.
- `SpawnGeometry` and `BackgroundBounds` share only identical spatial queries. Spawn quotas, random candidate choice, camera filtering and X/XY background layout remain local.
- Existing movement modes, inventory events and ScriptableObject data continue their established contracts. Additional pattern layers in these classes would add indirection without removing a current duplicate policy.

## Validation and limitations

Offline Roslyn compilation of the runtime and Editor assembly response files completed with 0 errors; the response files used the installed Unity 6000.3.11f1 references, but the Unity editor was not started for C004. Static comparison of 84 serialized declarations and all 59 moved `.meta` files passed; staged source/meta files and all 131 vendor C# hashes matched post-install. The attempted standalone behavioral comparison executable threw an unhandled .NET exception; no result from it is counted as a pass. The user saw a possible Windows 0xe0434352 dialog; that code alone does not identify the exact process. Play Mode, real scene/prefab import, UI visuals, physics interactions, persistence across restarts and player builds remain unverified. Prior C003 Play Mode results do not validate this C004 refactor.

The previous Unity validation retry was rejected by automatic approval review because original source/save drift made preservation unverifiable. Do not retry that same validation under this cycle without resolving the original-project activity and obtaining the required approval. Manual checks: open the saved scene without overwriting user changes; inspect script references, harvest each resource with relevant tool tiers, exercise monster state transitions and sword attack styles, settings save/load, inventory/offering screens and resource/monster spawning. Review compile/import Console errors before gameplay conclusions.
