# Global UI full text — 2026-11-02

## Approved scope
Use Hangeuljaemin4 for product UI. Remove generated ellipsis/truncation and auto-shrink; retain authored dialogue punctuation, typewriter timing, pagination and game rules. Buttons and short labels display full content without internal scrolling. Grow only necessary text/control/panel regions. Long ordinary non-button bodies may use local wheel paging when existing scrolling is unavailable. Preserve recipe graph geometry and existing scroll/input ownership. User explicitly requested minimal validation and speed.

## Implementation
FullTextLayout binds existing and dynamically created uGUI text, checks Korean font/atlas compatibility, uses cached text measurements, reserves layout minima and expands controls/panels only when needed. Button captions use available horizontal gaps and remain nonpaged. Input editing, recipe vertex geometry, existing dialogue pages and hidden quantities retain their owners. Symbol fonts stay dedicated for symbol-only content.

Common text creation, authored-template typography, release text-size preferences, campaign text containment and recipe labels now use the same policy.169 product UI prefab/scene files were scanned;589 TMP text components found;419 components in48 assets needed saved setting changes. Object IDs, scripts, enabled/active values and serialized callback method names were checked unchanged. No gameplay/settings/save schema changes.

Owner-reported follow-ups: title font references were already correct but39 title/save/help texts retained Bold. They now use Regular, including existing title instances after script reload. Key buttons had7-percent fixed widths;24 saved buttons in3 UI assets now have12-percent defaults. ControlSettingsController measures both binding columns and row heights from actual text/font size, keeps headers aligned and caches measurements. Large-text scaling is reapplied correctly by typography guides; authored sizes survive script reload.

## Actual verification
Final original-project runtime and Editor assemblies compiled successfully after the latest fixes. Main font resolved to Hangeuljaemin4-Regular SDF. Static preservation checks passed. Earlier namespace/foreach-scope compile failures were corrected; stale assembly results are rejected by source/DLL timestamp guards.

Per latest user instruction, no new Play session, whole-UI capture loop, native-input test or Windows build was run. Do not claim all layouts or extreme strings visually passed. Existing-Play UI normalization is implemented; the final evidence records that no existing Play UI was refreshed because Play was off. Evidence and recovery files: C:/Users/Master/Documents/Codex/UIFullText_20261102/.

Obsidian handoff update pending (existing designated log unavailable in prior handoff).
