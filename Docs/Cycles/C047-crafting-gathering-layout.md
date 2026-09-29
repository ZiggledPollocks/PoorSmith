# C047 Crafting and gathering asset layout — 2026-09-29

The user requested two distinct feature folders under `Assets/JinHo`, including scripts and related assets, while sharing genuinely common crafting/gathering code.

## Ownership

- `Assets/JinHo/Crafting`: former Blacksmith art, CSV/catalog, editor tools, fonts, prefabs, scene, UI/domain scripts, tests and documentation. `Integration` contains the former SmithingLoop scripts and editor checks. Its `Resources` folder contains `SmithingLoopContent` and the smithy interior prefab.
- `Assets/JinHo/Gathering`: `FieldMapStructureTest` scene, field resource interactables and harvest workflow, resource spawn zone, field item inventory and drops, data contracts, field inventory UI, gathering items/tool/drop tables, resource-node and drop prefabs, and the resource-zone setup editor tool.
- `Assets/JinHo/Shared/Inventory/InventoryRequirements.cs`: positive quantity, overflow and batch scaling checks shared by field consumption and smithy recipe filling. Their different inventory models, weight and equipment rules remain separate; the existing bridge transfers ownership between them.

All moved assets retain their `.meta` GUIDs. Active literal asset paths, CSV asset paths and both build scene paths were updated. `Resources.Load` keys remain `SmithingLoopContent` and `SmithyInterior`.

## Validation

Static checks: old runtime/editor asset paths absent; all 2,861 `.meta` GUIDs unique; 129 moved `.meta` GUIDs and the field-scene GUID match Git HEAD. An isolated Unity 6000.3.11f1 final-state compile/import passed with no C# or asset-import errors; `SmithingContentValidation` found 16 field item IDs and 129 enabled recipes. A separate isolated Unity inventory probe passed duplicate-cost field consumption, insufficient-quantity preservation, duplicate-ingredient smithy filling and multiplication-overflow rejection. After the field-scene move, another isolated Unity run loaded 445 field objects with zero missing scripts, loaded the smithy scene and resolved both `Resources.Load` targets. The first standard snapshot run compiled successfully but reported `source_drift` because the last prefab move and helper edit occurred after it copied source; final checks used a hash-matched updated isolated copy. Scene Play Mode, live Editor refresh, physical input and player build were not run for this refactor.
