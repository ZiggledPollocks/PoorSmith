# Experiment journal and owned inventory — 2026-11-01

Scope: factual crafting history, owned-list display/splitting, partial world pickup and offscreen resource respawn. No recipe/economy/resource numeric retuning or CSV edits.

- Open **실험 기록** in smithy controls or the field bag. Only accepted attempts are recorded. Inputs, actual actions, result/returns and fuel consumed are factual observations; hidden conditions and failure explanations are never calculated. Identical signatures aggregate, latest200 distinct signatures are retained. A loaded pending attempt becomes an unfinished record, never an inferred success/failure.
- Owned bag/chest, crafting ingredients, delivery source and trade-owned lists support name search, sort and real catalog group filtering. Submit the added search with Enter/focus loss. Existing crafting filters remain. Shop products and recipe graph are unchanged.
- **분할** on a stack, or **수량 분할** in the field bag, creates a same-source/same-quality stack. Enter a positive integer smaller than the source count. Cancel/invalid/stale references do not consume items. Existing bridge/transfer stacking rules may later merge equivalent stacks.
- World pickup commits only the integer quantity fitting the existing weight tolerance. Remainders stay on the same drop. A full bag stops attraction and returns an attracted remainder to its physical origin; freeing capacity permits retry. Purchases/rewards/ordinary transfers keep their original all-or-nothing rules. Recovered arrows retain their existing equipment reload path without double-adding.
- Initial resource filling is separate. Depleted slots retain their original due time and retry only valid offscreen placement; no visible/overlap fallback. Target count, respawn interval, harvest/tier/drop rules remain unchanged. The legacy serialized offscreen flag is retained for compatibility; depleted resources now always obey the approved offscreen policy.

## Editing

Existing `GameArtSkin`, `ScreenLayoutTemplate`, `UIElementTemplate` are reused. New guides under `Assets/JinHo/Resources/UI/Screens/Layouts`: `OwnedItemTools`, `OwnedQuantityDialog`, `ExperimentJournal`. Screen-specific additions live in FieldBag, TownInventory, DeliveryBox, SmithyChestOverlay, SmithySelectionOverlay, WeaponShopBuy/Sell and PawnShop. Guides own presentation only; item references, live quantities and callbacks remain in screen owners. `OwnedRecordHeight` measures actual inner text width when width/text changes and reserves vertical inset for long scrollable entries.

## Persistence and regression checks

Optional `SaveData.experiments` / `pendingExperiment` fields preserve the existing save version. Legacy saves start with empty history, not reconstructed mastery history. Journal failures are isolated from the crafting transaction. `CraftingService.AttemptChanged` hands accepted operations to the existing snapshot writer; cancel/finish resolves only once.

`Tools/Validation/RecipeGatheringRegression.cs` is an in-memory script runner check, outside Assets. Run only in a stopped isolated Editor whose company name is `BatterMapValidation`:

```powershell
unity command script run --project-path <isolated-project> --file <repository>/Tools/Validation/RecipeGatheringRegression.cs --entry RecipeGatheringDomainChecks.Run --format json
```

It creates temporary objects/in-memory data and writes `%TEMP%/BatterMapRecipeGatheringValidation/domain.json`; it never loads ordinary player saves. Tests cover partial capacity, all-or-nothing neighbor, split conservation, projection identity, accepted/rejected crafting, grouped/capped journal, cancellation/fuel, pending recovery and legacy data; existing20 transaction regressions also run. Runtime-specific probes and QA player/save identity remain outside the original Assets.

Evidence and limits: `Docs/RecipeGathering_Report_2026-11-01.txt`. Prepared state and synthetic callbacks are distinct from native player input.
