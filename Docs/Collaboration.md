# Approved collaboration policies

Approved policies, finalized 2026-09-23 after the user approved scope, Unity preservation, completion criteria and Git handling. The repository AGENTS.md is the concise entry point. Exact locations are Docs in the repository and Files/batterground/Wiki in the existing Obsidian vault. Preserve original notes outside Wiki.

## 0. Language Policy — Approved

- Accept Korean requests; keep questions, design discussions, explanations, and completion reports in Korean.
- Write AI-facing instructions, Skills, and technical design and verification specifications in English.
- Convert agreed requirements for complex implementation work into concise English execution specifications. Do not separately translate every simple question or duplicate entire documents in both languages by default.
- Preserve the original Korean intent and latest user decisions. English specifications must not override them. Clarify material ambiguity instead of silently interpreting it through translation.
- Preserve exact identifiers, file names, Unity object names, references, and numeric constraints.
- When needed, map project-specific Korean terms to agreed English equivalents in a glossary; do not invent translations that change game semantics.
- Keep Obsidian learning notes and human-oriented explanations in Korean, with English terminology where useful.
- Explain significant English-document changes in Korean for review.
- Do not promise improved accuracy, token usage, or speed from English alone. Evaluate benefits using observed omissions, rework, and review effort.
- This policy governs observable communication and artifacts, not a guaranteed internal reasoning language.

## 1.1 Change Classification — Approved

Classify implementation work by unresolved design decisions and behavioral impact, not lines of code, file count, or investigation time. Explanation and analysis requests remain non-mutating.

### Small change: all conditions must hold

1. The requested behavior, target, and relevant values are clear; no material choice remains unresolved.
2. The change preserves existing component responsibilities, data structures, and system interaction patterns.
3. Its impact is bounded: it does not change game rules or behavior outside the requested scope.
4. Existing save data, scene/prefab references, and Inspector settings remain compatible without migration or reset.
5. Reproduction steps, expected results, and a suitable verification scope can be identified.

For an authorized small change, inspect → implement → verify → explain without an additional design approval. Several files may still constitute one small change. A new feature that fully follows an existing pattern may qualify if all conditions hold.

### Joint design: any trigger is sufficient

- New game rules require decisions, such as upgrade probabilities, failure penalties, or resource consumption.
- Component responsibilities or system interaction patterns must change, such as introducing a shared monster state machine.
- Shared behavior across features changes, such as player/monster/item collision policy.
- Save formats or data identity schemes change, such as item IDs or save schemas.
- Shared assets or project settings have cross-feature consequences, such as the physics collision matrix or common Input Actions structure.
- Major dependencies or development foundations change, such as Unity/package upgrades or framework adoption.
- Meaningful alternatives require user choice, such as rejecting an overweight pickup versus allowing partial pickup.

Inspect → explain impact and alternatives → agree on the design → implement. Do not infer that every new feature requires a major design discussion.

### Investigation needed

If evidence is insufficient, investigate relevant code, scene configuration, and reproduction conditions before classifying the change. Do not silently choose a material design alternative. Continue independent, unaffected investigation while awaiting clarification.

If a presumed small fix reveals a joint-design trigger, explain the finding and resolve that design decision before dependent implementation. Long investigation time alone does not make a change large.

### Previously agreed designs

Implement an already agreed design within its authorized scope regardless of size. Do not request approval again for the same design. Discuss newly discovered decisions that exceed that scope.

### Preservation and authorization are separate

Small-change classification does not authorize overwriting unsaved scenes, deleting user edits or assets, resetting save data, switching branches, committing, or pushing. Check the current request's authorization and actual state for these actions; do not repeat approval when already authorized.

## 8.1 Change Log and Learning Wiki — Approved

- Record every change in a one-line chronological log. Include the date, what changed, and verification status; add the reason when useful. Include identifiers or paths only when they help traceability.
- Never describe an unperformed check as passed. State verification gaps explicitly, even in a short log entry.
- For a small change as defined in section 1.1, the log is normally sufficient; do not create a new detailed learning article by default.
- If any change makes an existing wiki statement inaccurate, update that statement even when the change is small. This does not require a new learning article.
- For major design changes or explicit learning requests, create or update a system/concept wiki page with detailed explanation: the problem, current responsibilities and structure, execution flow, alternatives and decision rationale, relevant principles and project examples, and verification results and limitations.
- Explicit learning requests justify detailed explanation even for small changes.
- Reuse and update existing wiki pages rather than creating a separate article for every major change. Keep their main explanations aligned with the current implementation, and link relevant log entries to them.
- Preserve important previous decisions and why they were superseded in decision records or document history; do not leave obsolete designs presented as current behavior.
- When implementing an already documented and agreed design, supplement that explanation with implementation results and verification instead of repeating the same learning material.
- Keep human-facing Obsidian logs and learning explanations in Korean under section 0. AI-facing specifications remain English.
- This policy governs documentation depth, not implementation quality or verification rigor.

Suggested log format: `Date | Change and optional reason | Verification status | Optional wiki link`.

## 8.2 Repository Documentation and Obsidian Responsibilities — Approved

- Keep concise English execution references in the repository: agent working rules, setup and verification instructions, current architecture, and agreed task specifications and acceptance criteria. Keep them versioned with the code where applicable.
- Use Obsidian as the user's Korean study wiki: system responsibilities and execution flows, principles and concrete examples, alternatives and design rationale, concept links, important historical decisions, and one-line change logs.
- Organize learning pages by system or concept, not one standalone article per modification. Keep each main page aligned with current implementation and link to relevant decision history and chronological logs.
- Separate evidence of current behavior (code, scenes, prefabs, and runtime observations) from intended behavior (the latest agreed specification). A mismatch may indicate a bug or stale documentation; investigate rather than automatically treating either as correct.
- Use project AGENTS.md for agent working rules once installed, agreed specifications for implementation intent, Obsidian decision records for detailed rationale and learning, and Obsidian logs for change chronology. Do not treat learning examples or proposed designs as approved implementation requirements.
- During design, capture actionable agreements in the repository specification and explain alternatives and principles in the relevant wiki. During implementation, consult the specification and code, and retrieve wiki rationale when relevant.
- After implementation, update affected execution references and wiki statements, add the one-line log, and provide detailed learning updates according to section 8.1. Do not rewrite unrelated pages or create a full bilingual mirror by default.
- Connect related repository documents, wiki pages, and code locations with verified links or location references. Include relevant source locations and a review date or code revision in system pages so stale explanations can be recognized.
- When implementing an already explained design, add implementation outcomes and verification evidence instead of duplicating the explanation.
- If Obsidian is inaccessible, do not claim it was updated. Identify pending updates and preserve the intended content in an accessible handoff artifact when useful.
- The approved update workflow is change-scoped maintenance, not blanket copying, continuous background synchronization, or automatic migration of existing notes.


## Final operational agreements — Approved 2026-09-23

- Explanation and analysis requests do not change product code. Implement explicit requests within their approved scope; discuss unresolved material design choices first.
- Preserve unsaved Unity scenes, user-placed Tilemaps, Inspector values and pre-existing source/asset edits. Establish preservation before reload, replacement or generation.
- Distinguish code writing, component/reference integration, saved scene/prefab state, compilation and actual play/visual validation. Report checks not performed and remaining reproduction steps honestly.
- Check current branch and existing changes. Commit and push only when requested or explicitly authorized in the current task; preserve unrelated work and other branches.

These were explicitly approved after the final four-item review in this conversation. Historical C001 documents remain evidence of their original adoption stage. This installation does not authorize any of the suggested gameplay follow-up fixes.
