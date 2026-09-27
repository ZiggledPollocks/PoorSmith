# C002 M2/M3 — runtime validation specification

Approved 2026-09-23: both M2 and M3. Continue M1 isolation without changing gameplay rules.

## M2 acceptance

- Open saved SampleScene and load its saved GameUI resource prefab. Never rebuild UI assets as preparation.
- Start at the main menu with gameplay paused and input blocked; Play resumes.
- Exercise settings from menu and gameplay, inventory open/close, modal exclusivity, and input restoration after the close frame.
- Check live action rebinding, font references, audio application and a save/load round trip after changing the in-memory value.
- Render menu, settings tabs and inventory canvases for visual inspection. A canvas render is not a full Game View or physical mouse test.
- Assign unique company/product identity before launching the isolated editor; verify project/run/persistence identity before entering Play Mode.
- Preserve original saved Assets/Packages/ProjectSettings and existing user JSON/PlayerPrefs; report concurrent drift rather than overwrite it.

## M3 first scoped checks

Inventory weight/count regression: base weight 100 produces limit 120; weight-2 items exercise 118, 120 and 122. Verify exact-limit acceptance, atomic overweight rejection, stack merging, partial/final removal, invalid requests and notification counts. Fixtures are in-memory objects in the copied project, never saved assets.

M3 is a reusable change-scoped policy, not a claim that all gameplay is verified. Physical pickup/drop, offering return, combat, boss phases, portals, death, movement, save corruption and player builds are outside this first execution.

## Failure policy

Repair test/plumbing defects within scope. Record game defects with reproduction and a proposed fix before changing product behavior. A failed assertion must not be weakened to match the current implementation. Preserve failed trial evidence.

## Implementation

Tools/Validation/Invoke-RuntimeValidation.ps1 runs runtime_validation.py. M1's snapshot/process helpers are reused; M1's command stays unchanged. RuntimeProbe.cs.txt is injected only into the copy. The legacy UIIntegrationSmokeTest is superseded for this workflow and remains uninvoked. Only copied companyName/productName differ intentionally; unexpected imported input mutations fail the run.

Windows-only. Graphics-capable batch mode omits -nographics; asynchronous Play Mode owns termination, so -quit is omitted. Runtime assertions produce JSON and a run-specific completion marker. No UI/resource generator, original-editor interaction, commit or push.

## Execution status

Implementation and actual run completed; latest overall result: passed. Guard tests: 17 passed. Recorded assertions: 236/236 passed; inventory: 10/10. See ../Validation.md and ../Evidence/M2-M3-validation-results.json. Broader M3 scenarios remain change-scoped follow-ups.
