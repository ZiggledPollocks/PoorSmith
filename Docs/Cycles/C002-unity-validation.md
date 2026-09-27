# C002 — Unity validation, milestone M1

Approved: 2026-09-23. User approved M1 construction and first validation of the saved working tree, including uncommitted changes. M2/UI and M3/change-scoped gameplay validation were subsequently approved on 2026-09-23. See C002-runtime-validation.md; the requirements below describe historical M1 scope.

## Requirements

- Isolate current disk Assets, Packages and ProjectSettings; preserve the original and unsaved editor state.
- Check Windows64 editor import/compilation using the exact installed project Unity version.
- Require positive, run-specific completion evidence and a successful process result.
- Record source identity, dirty state, included hashes, actual command, diagnostics, outcomes and limitations.
- Prove failure detection with a disposable intentional compile error and meaningful guard tests.
- Update execution documentation, current handoff and the Korean learning wiki/log.

## Implementation decisions

PowerShell is the user entry point; Python 3.10+ standard library manages file hashing, copying, process ownership, timeouts and JSON reports. A C# compile probe lives as a text template in Tools and is injected into the isolated copy only. This implements the planned completion marker without triggering a script import in the live project. Scripts and their behavior are reviewable in Tools/Validation.

Each run creates a unique external directory. Local packages and reparse-point inputs are rejected rather than copied incorrectly. Registry package resolution may use Unity caches/network. Output retention is manual; no deletion feature is added. Root IDE-generated files and the live Library are excluded.

## Exclusions

No gameplay fix, scene generation, UI smoke invocation, Play Mode, player build, visual/performance check, package upgrade, commit or push. The compiler may reveal pre-existing game errors; report them without expanding scope.

## Acceptance criteria

1. Guard tests reject overlap, existing targets, missing executables and unsupported dependencies, and never pass missing/stale markers or timeout.
2. Consistent source/copy manifests are produced, with drift detection before and after the run.
3. The first real editor compile produces an attributable pass/fail/blocked outcome.
4. A separate real Unity negative control identifies the deliberate compiler error and preserves source files.
5. Final evidence and documentation distinguish compile validation from gameplay validation.

Status: M1 completed. Guard tests: 12 passed. Final normal compile: passed. Real negative control: deliberate compiler error detected. Original snapshot inputs preserved. Observed results and run paths are recorded in Docs/Validation.md and Docs/Evidence/M1-validation-results.json. M2/M3 are now implemented and executed; latest runtime result: passed. See C002-runtime-validation.md and ../Evidence/M2-M3-validation-results.json.
