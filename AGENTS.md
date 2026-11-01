# batterMap working agreements

Approved 2026-09-23. Apply these rules to work in this repository.

## Start here

- Read [Docs/README.md](Docs/README.md) and [Docs/CURRENT.md](Docs/CURRENT.md) at the start of project work. Read [Docs/Collaboration.md](Docs/Collaboration.md) for the detailed approved policies.
- Retrieve only relevant specifications, source, architecture and wiki rationale. Do not reread the entire history by default.
- For implementation, check the current branch, existing changes, target scenes and affected assets. Preserve unrelated work.

## Language and learning

- Accept Korean requests and discuss requirements, design and results in Korean. Write AI-facing rules and execution specifications in English; keep Obsidian learning material in Korean.
- Preserve the original Korean intent and exact identifiers. Do not promise better model performance merely from using English.
- Explain programming concepts through definitions, principles, step-by-step reasoning and concrete project examples. Distinguish verified facts, inference and unknowns.

## Scope and joint design

- Explanation, analysis and comparison requests do not authorize product-code changes. Explicit implementation requests authorize work within their scope.
- A small change must meet all five conditions: clear behavior and values; unchanged component responsibilities/data structures/interactions; bounded behavioral impact; compatible saves, scenes, prefabs and Inspector settings without migration; identifiable reproduction, expected result and verification scope.
- For an authorized small change, inspect, implement, verify and explain without another design approval. File count and line count do not determine classification.
- Jointly design changes involving unresolved game rules, component responsibilities, cross-feature behavior, save formats/IDs, shared settings/assets, major dependencies, or meaningful alternatives.
- Investigate before classifying when evidence is insufficient. If a small fix reveals a material design decision, explain it and resolve that decision before dependent implementation.
- Execute already approved designs without repeatedly requesting the same approval. Discuss newly discovered choices outside that scope.

## Execution discipline - Approved 2026-11-01

Apply to analysis, implementation, design, testing, builds and documentation. Remove unsupported repetition without reducing agreed scope, quality or required verification.

1. Continue from existing requirements, decisions, edits and evidence. New messages and context compaction do not restart the task. Inspect current implementation before replaying an old plan.
2. Before repeating an action, identify its previous result, what relevant code/data/environment changed, and what new information or failed behavior this run will check. Without a material change or distinct diagnostic purpose, do not repeat it. Changing command, tool, filename or test data cosmetically is not a new approach.
3. Maintain a brief task-appropriate progress record using the existing plan/log: completed and remaining items, failure causes and attempted remedies, verification results and the relevant code/environment state, blocked items and resume conditions. Update at meaningful milestones, not every tool call. Reuse recorded evidence.
4. Diagnose actual conditions and effective settings before editing. Make hypothesis-driven changes; do not cycle between solutions A and B or repeatedly rewrite the same code without new evidence. Record failed approaches and their causes; revisit them only when relevant conditions change.
5. Do not rerun an unchanged failed operation. A relevant fix or genuinely different diagnostic hypothesis permits another attempt. After three failed repair/verification cycles for the same root cause, stop that approach; do not reset the count by renaming commands or files. Analyze accumulated evidence and adopt a materially different approach, or mark that item blocked with the exact prerequisite/user decision. Continue independent work. This limit ends an ineffective approach, not the entire authorized task.
6. Define pass criteria first; verify changed behavior and immediate dependencies. Reuse passes while relevant inputs remain unchanged. After a fix, rerun the reproduction and affected neighbors. Repeat whole suites/builds only for a justified integration milestone, shared change, new failure or explicit requirement. Broaden checks for save compatibility, shared services, item/currency conservation or demonstrated cross-feature risk. Never omit required checks merely to avoid repetition.
7. Unavailable connection attempts have a cumulative 30-second budget per unchanged environment unless a specific agreed policy applies. Stop unchanged retries afterward; resume only after a meaningful environment change or user request. This is NOT a timeout for a functioning build/test/import. Do not launch duplicate jobs or restart a progressing long-running operation just because it is slow.
8. Reuse relevant file reads, searches, logs, fixtures and test environments. Reread only for changed content, new dependencies or context recovery; narrow searches/log output to the failure. Refresh stale dependencies instead of rebuilding the environment without cause. Do not repeat backups, commits or pushes without relevant changes; these rules grant no additional Git authorization.
9. For design work, identify the concrete defect before changing layout/color/size. Preserve approved decisions. Once agreed usability and visual criteria pass, move on; do not redesign a passing result through preference changes without a new requirement or observable defect.
10. If repeated investigation/execution produces no new edit, evidence or causal understanding, stop that approach and inspect the last productive state and blocker. Switch to independent work or a materially different diagnosis. If no valid path remains, report the blocker and what would unlock it. Distinguish genuine stagnation from a healthy long-running operation. Do not substitute repetitive status messages or unsupported 'almost done' claims for progress.
11. Distinguish implementation, compilation, actual execution, visual review and native-input verification. Never mark skipped/unavailable checks passed, an exhausted retry budget resolved, or required incomplete work fully complete. Preserve completed work and identify only the remaining gaps.
12. Close concisely with achieved results, evidence, failed/unperformed checks, blocked items and concrete resume conditions/next actions. Avoid repeating prior explanations. Persist toward the authorized outcome while respecting these limits and protected user work.

## Unity preservation and completion

- Preserve existing source edits, unsaved scenes, user-placed Tilemaps, Inspector values and asset references. Disk scene contents do not prove the editor has no unsaved changes.
- Before scene reloads, replacement or generator reruns, establish the affected scope and how user work is preserved. Continue independent work if that state cannot be inspected.
- Treat code creation, component attachment/reference wiring, scene/prefab persistence, compilation and runtime/visual verification as separate completion items. Complete integration required by the authorized change.
- Report what actually ran. Compilation alone does not verify visual, physics, camera or combat behavior. Provide reproduction steps and remaining checks when runtime verification is unavailable.
- Select checks appropriate to the change and affected neighbors. Consult [Docs/Validation.md](Docs/Validation.md); plan with Tools/Validation/Invoke-Validation.ps1 -Action plan, then run the reviewed scope with -Action run (safe reusable mode by default). The legacy UIIntegrationSmokeTest is superseded and must not be used as the current entry point.

- Follow the focused validation policy approved 2026-11-01: identify actual scene/prefab Inspector values and pass criteria first; reuse isolated fixtures; compile and exercise only the changed behavior and immediate neighbors in one Play session where practical. Stop after passing; rerun only failed/affected checks after fixes. Broaden coverage for shared services, saves, inventory/currency integrity or demonstrated cross-feature risk. Limit an unavailable Unity connection attempt to30 seconds, then report runtime checks as unverified and finish independent checks. See [Docs/Validation.md](Docs/Validation.md#focused-validation-policy--approved-2026-11-01).
- For simple numeric-only changes, inspect effective values, authored overrides and limits (including Inspector ranges and runtime clamps) together; update the required values and limits in one pass, then run one short targeted validation pass and stop when it passes. Compile if code changed. Repeat only failed/affected checks; broaden scope only if the change reveals wider risk.

- Standing user confirmation (2026-11-01): outside agent-controlled Editor sessions, assume batterMap edits are saved and Play Mode is off; do not repeatedly ask. Preserve any directly observed contradictory state.
- After final validation in the isolated `p` project, apply the reviewed changes to batterMap; compare source targets for concurrent edits first, copy only the validated manifest, preserve user saves, and verify equality. Do not leave completed implementation only in p.

## Git

- Verify the current branch and preserve other branches and unrelated changes. Historical use of JinHo_1 is context, not a permanent branch command.
- Commit and push only when requested or explicitly authorized for the current task. Past permission is not standing authorization. Review the intended contents first.

## Documentation and handoff

- Repository Docs hold concise English execution references, current architecture, verification instructions and agreed major-cycle specifications. The Obsidian handoff folder holds Korean orientation, current system context and chronological handoff logs.
- Vault root on this PC: `C:/Users/Master/Documents/Obsidian Vault`. Handoff root: `Files/batterground/인수인계`; entry: `00_시작.md`; log: `변경로그.md`. The former `Wiki` and `코드해체분석기` folders were retired on 2026-10-04 at the owner's request.
- Preserve original notes directly under `Files/batterground`; link to them instead of rewriting, moving or deleting them.
- Append one dated Korean line per logical change, stating the change and actual verification status. Small changes normally need no new detailed article, but update any existing handoff statement made inaccurate.
- For major changes or explicit learning requests, update the related handoff page with responsibilities, flow, principles/examples, alternatives/rationale and verification limits. Reuse pages rather than duplicate them per change.
- Distinguish intended behavior, implemented behavior and verified behavior. Learning examples are not approved feature requests.
- Update affected Docs and CURRENT for handoff. If the vault is unavailable, record pending updates rather than claim completion. No continuous synchronization is implied.
- Close with achieved requirements, relevant changes, verification results/gaps and remaining work. Scale explanation to the change.

## Persistent validation default — 2026-11-01

- Select scope and inspect valid existing evidence before expensive execution. Unknown/shared-data risk broadens checks; required checks cannot be suppressed.
- Prefer registered reusable environments and exact-content evidence. Fresh is for documented clean/import/corruption/incompatibility needs.
- See Tools/Validation/README.md for ownership, incremental sync, retry ledger, artifacts, compatibility commands and exit codes.
- Keep newly passed/reused/failed/blocked/unverified distinct. Required visual/native/build/audio acceptance remains required.
