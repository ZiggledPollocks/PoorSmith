# Gameplay and tutorial UI presentation — 2026-11-02

Owner expanded scope to character-controlled locations and all tutorial UI, excluding the crafting screen itself. Prior explicit minimal-validation/no-question instruction retained. Original project edited; recovery snapshots and exact-file manifest in C:/Users/Master/Documents/Codex/GameplayUI_20261102.

Implemented:
- Explicit non-crafting screen allowlist: field/town HUD, bags, equipment, player info, maps, trading/delivery, pause/settings/death/confirmation surfaces. Existing Wood sprites receive dark brown surfaces, cream neutral text, restrained brass lines and short button state transitions. Unique illustrations, gauge fill, curve shape, item states and slot interaction remain owned by original components. Matching saved guides updated where applicable.
- All Story-created dialogue, mini guidance, goal/skip, purchase, confirmation and result UI receive the same presentation. Fixed accidental additional 80px height on stretched story buttons and separated full-dialogue body from its next button. Crafting tutorial instructions are included; Smithy screen guides and crafting gestures remain unchanged.
- Mini guidance now uses its actual configured size for avoidance, searches more candidate positions without crossing controls, and reserves action targets/HUD controls. Long action instructions can grow their panel; actual speech retains existing pagination, so FullTextLayout does not independently grow paginated Speech/MiniSpeech text.
- Floating tutorial windows clamp to viewport safe area after the existing text fitter. No camera, gameplay collider, save ID, recipe information or rules changed.
- Before intro logo: only the existing tutorial money HUD is visible among HUD elements. Other town/field/gauge/tool/info/interaction HUD owners hide and restore using shared live state. Required dialogue, purchase and skip controls remain available. During logo the existing money-HUD suppression remains. Full-width tutorial dialogue/confirmation also suppresses irrelevant underlying gameplay HUD.
- Separated information-entry placement from tutorial money plate; normal tutorial currency does not double-render the town gold readout.

Validation: source-only Unity Roslyn compilation passed; 23 prefab identity/callback/active/raycast lists preserved; non-overlapping normalized gold/info and goal/skip bands checked for the three supported reference sizes. No rendered screenshots, native input, Play or Windows build were run per speed request. This is not visual certification of all UI states, and Editor import is unverified. No connection retries, reload requests, Git commit/push or separate Editor launch.

Designated Obsidian handoff log unavailable in prior verified state; Korean handoff update pending. No duplicate vault structure created.

## Native review continuation and Park Deoksu — 2026-11-02

Owner expanded scope to crafting/workstation UI and requires actual product scenes (not UITestScene), contrast/readability checks and three resolutions. This supersedes the earlier crafting-screen exclusion and source-only acceptance. Full audit remains incomplete.

Observed and fixed the oversized town Info/Map/Bag entries with compact Wood guides; original SampleScene before/after 1280x720 captures exist under GameplayUI_20261102/computer-use. Info opens/closes by pointer. Interior Info readability/empty space, all other families, tutorial normal/skip and remaining resolutions are pending. Escape delivery did not close Info in the observed session; cause is not yet established.

Park Deoksu now uses the existing npc.aseprite references in Story/PasserAnimations for large/mini portraits and Visitor art. Walking samples existing motion within the unchanged 0.8-second visit. No story/economy/position/Passer changes. Original Editor import compiled and SampleScene Play rendered the large portrait and visitor idle at 1920x1080. This was a presentation reproduction, not normal tutorial progression. Mini/walk/exit and actual tutorial-flow checks remain unverified after external Play termination. Evidence: outputs/npc-tutorial in the current Codex task, original backup in GameplayUI_20261102/npc-before. No commit/push.
