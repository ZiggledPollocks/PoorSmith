# Tutorial hammer, HUD chrome and regular Korean text — 2026-11-02

Owner requested direct original-project changes, recommended defaults, no questions and minimal validation.

- World ego hammer uses GameArtSkin.egoHammer after the reveal. Resting hammer height is 50% of the player body SpriteRenderer world bounds (2.22 world-unit fallback only when unavailable); existing reveal/save flags and item data are unchanged.
- Tool-rack overview/station now references original source/CraftingUI/crafting_2. Ordinary tool icon uses its hammer rectangle; no ego art in the normal hammer slot. Original textures/import metadata preserved.
- TitleBackdrop artwork references source/Main/titlebackground1, preserving buttons, root and aspect ratio.
- 622 serialized TMP texts audited: normal style and 400 weight. Shared FullTextLayout enforces Hangeuljaemin4 Regular for dynamic UI, preserving dedicated symbol fonts. HUD factories and template copying also use normal style.
- Tutorial objective/skip/gold occupy separate bands from field status/region/arrows and town date. Normal town gold hides while the tutorial copy owns its presentation. Mini dialogue avoids visible action target rectangles, small UI images, skip and gold as well as existing texts/buttons; current recipe information and input targets are unchanged.

Validation: original Unity Roslyn compiler with existing Editor response file, separate output directory: 0 errors, 9 existing warnings. Serialized identities and callback method names preserved; title/rack references and regular styles checked. This is source compilation, not Unity import or live screen acceptance. No Play, captures, native input or Windows build per owner speed request. All HUD/tutorial states are NOT visually certified; actual image overlap remains unverified. Connection retries and reload questions were avoided.

Recovery and exact changed-file hashes: C:/Users/Master/Documents/Codex/UIHudHammer_20261102 (before/, changes.json, compile.log, verification.json). No commit/push, scene reload or texture regeneration.

Designated Obsidian handoff log unavailable; Korean log update pending.
