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

## Unity preservation and completion

- Preserve existing source edits, unsaved scenes, user-placed Tilemaps, Inspector values and asset references. Disk scene contents do not prove the editor has no unsaved changes.
- Before scene reloads, replacement or generator reruns, establish the affected scope and how user work is preserved. Continue independent work if that state cannot be inspected.
- Treat code creation, component attachment/reference wiring, scene/prefab persistence, compilation and runtime/visual verification as separate completion items. Complete integration required by the authorized change.
- Report what actually ran. Compilation alone does not verify visual, physics, camera or combat behavior. Provide reproduction steps and remaining checks when runtime verification is unavailable.
- Select checks appropriate to the change and affected neighbors. Consult [Docs/Validation.md](Docs/Validation.md); use Tools/Validation/Invoke-RuntimeValidation.ps1 for the approved saved-UI and inventory checks. The legacy UIIntegrationSmokeTest is superseded and must not be used as the current entry point.

## Git

- Verify the current branch and preserve other branches and unrelated changes. Historical use of JinHo_1 is context, not a permanent branch command.
- Commit and push only when requested or explicitly authorized for the current task. Past permission is not standing authorization. Review the intended contents first.

## Documentation and handoff

- Repository Docs hold concise English execution references, current architecture, verification instructions and agreed major-cycle specifications. Wiki pages hold Korean learning, rationale, decisions and chronological logs.
- Vault root on this PC: `C:/Users/Master/Documents/Obsidian Vault`. Wiki root: `Files/batterground/Wiki`; entry: `00_프로젝트_홈.md`; log: `변경로그.md`; systems: `시스템`; decisions: `설계결정`.
- Preserve original batterground notes outside Wiki; link to them instead of rewriting, moving or deleting them.
- Append one dated Korean line per logical change, stating the change and actual verification status. Small changes normally need no new detailed article, but update any existing wiki statement made inaccurate.
- For major changes or explicit learning requests, update the related system/concept page with responsibilities, flow, principles/examples, alternatives/rationale and verification limits. Reuse pages rather than duplicate them per change.
- Distinguish intended behavior, implemented behavior and verified behavior. Learning examples are not approved feature requests.
- Update affected Docs and CURRENT for handoff. If the vault is unavailable, record pending updates rather than claim completion. No continuous synchronization is implied.
- Close with achieved requirements, relevant changes, verification results/gaps and remaining work. Scale explanation to the change.
