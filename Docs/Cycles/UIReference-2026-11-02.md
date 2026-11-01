# Representative UI reference — 2026-11-02

Status: original-project prototype; first bag/storage review only. The owner explicitly switched from isolated candidates to direct batterMap editing. No whole-UI rollout approval yet.

- Restrained Wood: low-saturation brown, cream text, brass selection. Six columns per bag/storage list; slot90 reference, gap12; window1560x970 reference after first actual capture.
- Shared Korean font references and object/script identities preserved. Local restore files: C:/Users/Master/Documents/Codex/UIReference_20261102/direct-before/.
- Button captions must be fully visible without scrolling, ellipsis or clipping. Short text also displays fully. Long non-button body text may scroll; authored dialogue punctuation is preserved.
- First original UITest capture at1280x720,1440x900,1920x1080 succeeded. Actual render revealed insufficient detail height and toolbar Ellipsis defaults. Corrected detail height and owned-toolbar text settings. Second actual1280x720 render shows the complete selected wood name/count/value/description. Audit still reports Ellipsis on some toolbar labels; resolve final presentation ownership before accepting text policy. The final screen changed to1468x842 during capture, so1920x1080 acceptance is unverified despite its filename.
- Editor-only UIReferencePreview captures the memory-only UITest bag, refuses dirty scenes or existing Play sessions, and restores the previous saved scene setup. It never exits the original Editor or changes PlayerSettings. Native input, Windows build and remaining three representative screens are unverified.
- Validation entry plan ran via validation_workflow.py. Its broad existing dirty-tree dependencies were not treated as task-specific passes. No broad suite/build rerun for this unapproved presentation prototype.

Next: inspect corrected real captures, obtain owner feedback on first screen, refine other three representative screens, then obtain approval before expansion. Obsidian handoff update pending.


## Approved brown revision — 2026-11-02

Applied dark Wood tab buttons and cream regular captions to bag/storage, including SplitStack. Shared Hangeuljaemin4 now used by BlacksmithView.Text; opted-in prefab typography copies font and fontStyle. Buttons use explicit prefab caption guides with Normal wrapping and Overflow, not Ellipsis. Existing callbacks, item data and slot quantities unchanged.

Clear disables children then uses deferred Destroy. Path-based application can target the previous inactive SplitStack within the same frame. Anchored OwnedItemActions now applies the existing guide to its fresh button and label references; no gameplay transaction change. Unsupported surface-guard/SpriteSwap hypotheses were reverted.

Final real Game View captures have PNG-confirmed sizes1280x720,1440x900,1920x1080. All three recorded SplitStack as ColorTint/T_tab_unselected. No text bounds or Ellipsis violations found for the visible selected-wood screen. Korean glyphs and full visible captions/details reviewed in1280 and1920 images. This does not prove every item/translation/state or other screen. Native input and Windows build not run for this prototype iteration. Prior scene restored; memory-only fixture used.

Evidence: C:/Users/Master/Documents/Codex/UIReference_20261102/brown-final-evidence.json. Related original recovery files: brown-before/. Obsidian handoff update pending.


## Remaining three representative screens — 2026-11-02

Applied the approved restrained dark Wood/cream/Hangeuljaemin4 presentation to WeaponShopBuy, SmithySelectionOverlay/HUD and SettingsMenu guides. Product list, selected product information and owned items remain separate. Owned details now has a reserved lower-left area. Crafting keeps original station illustrations and has distinct automatic-recipe, material, selected-material and refusal-hint regions. Settings has compact rows and an in-window four-action footer. Existing game rules, prices, recipe conditions, callbacks and settings ranges remain owned by their controllers.

Caption guides preserve full text; search's explicit ellipsis and common KeyboardFooter Ellipsis were removed. Added a decorative backdrop to the existing crafting refusal hint. Fresh split-button references receive the selection HUD guide to avoid deferred-Destroy lookup ambiguity. Explicit ColorTint image guides clear previous SpriteSwap override art.

Final current-source compilation/import and one memory-only UITest session produced9 exact-size PNGs (three screens x1280x720/1440x900/1920x1080). No current visible text-mode/height violations detected. Actual1280 captures reviewed. Browsing/product selection only; no transaction, crafting or setting commit. Native input, Windows build, every item and all settings-tab states are not claimed. Screenshot/capture errors during authoring were fixed; prior failed results remain archived, not counted as passes. Evidence: C:/Users/Master/Documents/Codex/UIReference_20261102/three-screens/final-evidence.json.

Next authorized scope: after these four representative screens, apply Korean font + no UI-generated ellipsis/clipping + appropriate control/panel sizing to all game UI. Keep Hangeuljaemin4 and authored dialogue punctuation. Buttons and short text must be fully visible without scrolling; only long non-button bodies may scroll. This broad text phase is not implemented or verified yet; no further approval is required for that already requested text scope. Whole-UI visual redesign retains the earlier representative-review gate. Obsidian handoff update pending.
