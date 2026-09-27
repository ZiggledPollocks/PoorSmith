# Inspected architecture

Reviewed 2026-09-23 against the working tree identified in Evidence/source-manifest.json. Static evidence only; scene integration and runtime behavior are not exhaustively validated.

## Environment

Unity 6000.3.11f1. Manifest: Input System 1.19.0, Cinemachine 3.1.7, URP 17.3.0, Test Framework 1.6.0. SampleScene is enabled in build settings; MapScene also exists. Do not infer the editor's unsaved scene from these disk files.

## Responsibilities

| Source under Assets | Responsibility and relevant dependencies |
|---|---|
| JinHo/Scripts/Assets and Assets/Data | ItemData, ResourceData, ToolData, interaction/resource/state/storage/target-query contracts |
| JinHo/Scripts/player/Input/PlayerInputHandler.cs | Input state, one-shot consumption, UI blocking |
| JinHo/Scripts/player/Movement/PlayerMovement.cs | Movement coordination and command application, input timers, roll, ground sensing and fall damage; serialized settings compatibility |
| JinHo/Scripts/Assets/IMovementMode.cs | Common calculation-only movement contract |
| JinHo/Scripts/player/Movement | Normal/Flame/UpDraft calculations, context/commands, runtime settings, environment registry and contact adapters |
| JinHo/Scripts/player/Tools/PlayerToolController.cs and PlayerToolLoadout.cs | Selected tool and initial slot configuration |
| JinHo/Scripts/player/Interaction/PlayerInteraction.cs | Tool-aware interaction, quick interaction, sword attack geometry |
| JinHo/Scripts/InteractObj and Item | Resource interaction → drop spawning → automatic pickup → inventory mutation |
| JinHo/Scripts/UI | Game modal coordination, inventory/offering screens, gauges, prompts, tool HUD |
| SettingsMenuUI/Scripts | Input rebinding, sound/game settings, tabs, scrolling, settings persistence |
| JinHo/Scripts/Combat | Monster-specific behavior, spawning, hit feedback, health displays, storm boss phases |
| JinHo/Scripts/Physics/CharacterPhysics2D.cs | Shared physical reactions including knockback |
| JinHo/Scripts/Zone | Resource spawning, assimilation/offering interaction, updraft |
| JinHo/Scripts/World/Camera/CameraLookAhead.cs and background scripts | Camera tracking and environmental visuals |
| JinHo/Scripts/player/Persistence/PlayerSaveSystem.cs | Version 1 JSON with currentToolId and equippedArmorId, not a full-world save |
| JinHo/Editor and SettingsMenuUI/Editor | Mutating setup/generation and batch-only UI smoke test |

## Important current facts

- Inventory addition rejects the entire requested amount when overweight; it does not partially accept it. MaxWeight is initialized to settingsWeight × 1.2.
- Tool changes publish CurrentToolChanged; inventory changes publish InventoryChanged. Direct references, singleton access, and scene/name searches also exist.
- Tree source defaults are a drop opportunity every 2 interactions and depletion after 6; historical requests and serialized asset overrides can differ.
- Offering capacity is 5 item types, not 5 total units. Bulk return currently ignores TryAddItem failure before clearing the basket.
- PlayerAssimilate.IsDead checks zero or lower. Direct Assimilate and TakeDamage have different event paths.
- PlayerSaveSystem.Start saves when Load returns false, including error or unsupported-version cases; preservation policy needs investigation before changes.
- GameUIController and GameUI prefab currently enable the start menu. Old UI_INTEGRATION_GUIDE and initial smoke-test expectations are stale on this point.
- Boss orb attacks lock the boss at home; dive attacks move the body. Rectangle coordinates use the boss collider and sampled ground.

## Learning mapping

The wiki system pages 01–12 map the overall structure, items, input, movement, combat, monsters, boss, UI, persistence, world, language concepts, and collaboration. Source catalog includes all 73 inspected C# files and links to the 58 preserved detailed code notes. Inferred tradeoffs in those pages are not historical user-approved decisions.

## C003 movement update — 2026-09-26

See Cycles/C003-movement-modes.md and Evidence/C003-movement-results.json. UpDraftZone supplies its mode through generic environment registration; FlameContactSensor adapts the existing Flame layer. Membership and selected movement are separate: jump/fall suppression survives roll overrides. Modes are plain per-player C# objects, never independent FixedUpdate writers. PlayerMovement remains the existing animation/portal/boss integration facade. CharacterPhysics2D retains shared knockback/contact correction.

## C004 script architecture — 2026-09-26

See [C004 architecture](Cycles/C004-script-architecture.md) and [evidence](Evidence/C004-script-refactor.json). Resource harvesting uses composition, four monster controllers share transition mechanics via State, sword targeting uses Strategy, persistence uses text-store adapters, and runtime UI primitives use a Factory. Common sprite timing, player target lookup, spawn geometry and background bounds are extracted services. Existing inspector fields and moved script GUIDs were preserved. This is an offline compile/static-validation result, not a C004 gameplay result.

## C005 callback-only player input — 2026-09-26

[PlayerInputHandler](../Assets/JinHo/Scripts/player/Input/PlayerInputHandler.cs) received action phases through one `PlayerInput.onActionTriggered` subscription in C005; C006 replaced it with direct per-action subscriptions. Continuous values are cached and UI-gated; one-shot values retain consume methods. PlayerInteraction no longer polls the mouse button for attack, using the handler flags while still reading pointer position for targeting. `Player/ToolScroll` lives in [InputSystem_Actions.inputactions](../Assets/InputSystem_Actions.inputactions). The InputAction asset keeps previous IDs/bindings; the scene's serialized notification behavior remains 0 and the handler selects C# events at runtime. See [C005 specification](Cycles/C005-callback-input.md).

## C006 per-action callbacks — 2026-09-26

[PlayerInputHandler](../Assets/JinHo/Scripts/player/Input/PlayerInputHandler.cs) now directly subscribes each Player action to its corresponding method (`OnMove`, `OnSprint`, `OnJump`, `OnInteract`, `OnRoll`, `OnAttack`, `OnInventory`, `OnToolScroll`, `OnToolSlot1/2/3`). Subscribe/unsubscribe phases are paired, and Start rebinds after PlayerInput's action asset initialization. The C005 state and UI boundary rules remain. See [C006 specification](Cycles/C006-per-action-callbacks.md).
