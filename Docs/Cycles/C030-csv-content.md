# C030 CSV content source — 2026-09-28

## Scope and authority

Notion was read only. The linked [resource](https://app.notion.com/p/2829c4aa4b9c8200b28001334cdd98a3?v=a229c4aa4b9c8319a94c081c533026a4), [crafted item](https://app.notion.com/p/0259c4aa4b9c83b89cb001940efeca42?v=e319c4aa4b9c83a691e40808c4193fb9), [weapon](https://app.notion.com/p/8519c4aa4b9c83eabda5014535c69067?v=0619c4aa4b9c829a9fc388f78771d21a), [armor](https://app.notion.com/p/c589c4aa4b9c831db4a8819a2bf3f79c?v=be89c4aa4b9c8317a577888fdeaa14cf), [trophy](https://app.notion.com/p/a859c4aa4b9c836dbc0d81f0030420ad?v=e809c4aa4b9c82acba6d087c8b3c72ae), [failure](https://app.notion.com/p/ca29c4aa4b9c834eacf481d5da9373d9?v=e069c4aa4b9c83d7928a88f435ec4c46) and [equipment shop](https://app.notion.com/p/96c9c4aa4b9c8218b41581a4342e990b?v=9299c4aa4b9c8344a4df088c27b75a69) tables have 93 relevant rows. Their explicit values map to 92 distinct existing catalog IDs because the branch is both a resource and the fallback weapon. The 189 crafting routes came from the existing [Notion recipe snapshot](https://app.notion.com/p/8879c4aa4b9c831884ad815a79adf309). Remaining catalog values and 14 drop quantities came from current Unity assets. Each CSV row records value provenance; missing design numbers remain provisional or unset.

## Data flow

`Assets/Blacksmith/Data/Csv/` is the editable source:

- `items.csv`: 202 existing catalog IDs. Notion supplies 92 item prices/stat records, 89 descriptions, 20 weapon attack intervals, 16 armor defense values, five shield cooldown/reduction pairs and four tool purchase prices. The resource branch sale price (5) takes precedence over its separate default-weapon row (0); both sources are annotated. Unspecified T3 tool prices retain the provisional rule.
- `recipes.csv`: 189 routes, stations, process counts, enablement and provenance.
- `recipe_ingredients.csv`: 350 ordered ingredient rows with recipe and item foreign keys.
- `field_items.csv`: nine existing `ItemData` assets with Notion weight and assimilation reduction: five gathering resources and four monster trophies. The feather row has no existing field asset; its catalog description/price are included without inventing an item ID or prefab.
- `resource_drops.csv`: 14 existing `ResourceData` entries with prefab GUID guards and quantities. These quantities are marked `existing_asset`, not Notion values.

`Blacksmith/Import CSV content` validates the whole dataset, then writes the existing `TestCatalog.asset`, nine `ItemData` assets and the drop quantity arrays. Scenes, prefabs, their GUIDs, item IDs and saves do not change. The playable-scene builder calls the same import. The earlier `NotionRecipes.json` is a historical snapshot and is no longer the import source. Runtime uses serialized Unity assets, not CSV file I/O.

The Notion weapon speed column behaves as seconds per attack: 0.5–0.6 for daggers versus 1.8–2.1 for hammers. `items.csv` preserves the raw value in `notionAttackIntervalSeconds`; the importer converts to `ToolData`'s attacks-per-second unit with `1 / interval`. The Notion attack number drives equipped melee weapons and arrows. The default branch weapon uses its catalog attack/speed instead of hardcoded provisional stats. Five shields now use their Notion full-block cooldown and in-cooldown reduction; unspecified shields retain the prior provisional 50% reduction.

The importer rejects malformed CSV, duplicate/missing IDs, missing ingredient references, negative counts, invalid enum values, invalid shield/interval values, mismatched field IDs, changed drop prefab GUIDs and incomplete drop lists before changing assets. Saved item and recipe IDs cannot be removed silently. Notion's tool T1/T2 purchase prices are read from catalog `buyPrice`, while the separate sale prices drive the pawnshop base value.

## Verification and limits

On an isolated Unity 6000.3.11f1 project, import compiled and logged `CSV_CONTENT_IMPORT_OK 202 items, 189 recipes, 350 ingredients, 9 field items, 14 drops`. The recipe verifier passed 3,182 checks with 129 active routes. Focused economy/combat-data checks passed for resource/crafted prices, weapon/armor stats, attack-interval conversion, T1/T2 tool purchases, T3 provisional fallback, trophy assimilation and shield cooldown/reduction. A duplicate-ID test failed before any catalog write and left its SHA-256 unchanged. Unity export of the minimal serialized catalog matched the fully imported 202-item/189-recipe data exactly. The isolated editor also logged a nonfatal stale Visual Scripting package-cache validation warning. Live Editor refresh, rendered UI, physical combat and full game loop were not exercised in this migration.

`FieldMapStructureTest` still has no resource or monster instances after the earlier map-only request. The drop CSV governs existing `ResourceData` assets but cannot be exercised through field gathering until those prefabs are placed again.

The Notion resource and default-weapon branch prices conflict (5 versus 0); resource sale price 5 is retained. The trophy table spells the core “콜렘의 핵”, while the existing saved catalog ID and display label use “골렘의 핵”; the ID/label remain stable. Shield damage is applied after the existing armor-defense formula because Notion gives defense points but does not define a new damage formula. Missing upgrade/debt scalars remain in the provisional `CampaignRules.asset`.
