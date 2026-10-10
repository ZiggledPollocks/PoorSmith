# Assets layout

Updated 2026-10-04 after the owner chose `source` for incoming source art and asked to consolidate `Campaign` into `JinHo`.

## Current locations

| Location | Purpose |
| --- | --- |
| `Assets/source` | Imported/source visual packs and their supplied files. Preserve vendor layout when updating a pack. |
| `Assets/JinHo/Campaign` | Campaign, town, field and travel code, authored art/data/prefabs and Editor builders formerly under top-level `Assets/Campaign`. |
| `Assets/JinHo/Crafting`, `Gathering`, `Scripts`, `Shared` | Existing game feature and shared code/data. |
| `Assets/JinHo/Scenes` | All five enabled gameplay/build scenes. `EditorBuildSettings.asset` defines their order. |
| `Assets/JinHo/Prefabs` | Shared monster and tilemap prefabs formerly under singular `JinHo/Prefab`. Feature-owned prefabs remain with their feature. |
| `Assets/SettingsMenuUI`, `Settings`, `TextMesh Pro`, `Tile Palettes` | Existing settings/UI or package-oriented assets; not part of this migration. |

New imported visual source assets go under `Assets/source`. Game-owned scene/prefab/data assets remain under the owning `JinHo` feature. There is no new `.asmdef`: assembly boundaries need a separate dependency review before being introduced.

## Migration and maintenance

The top-level `Assets/Campaign` and `Assets/Scenes` paths, `Assets/JinHo/Prefab`, and the feature-local scene folders were retired. Asset and scene `.meta` files moved with their assets; Unity GUIDs are unchanged. Hard-coded paths in runtime scripts, Editor builders, project settings, build profiles and validation templates were updated. Historical cycle documentation may still mention the old paths; it records the layout at that time.

When moving another asset, keep its `.meta`, search for path-string references in `Assets`, `ProjectSettings` and `Tools`, then verify build-scene entries and scene/prefab references. Avoid rerunning scene generators on authored scenes without preserving the current scene first.
