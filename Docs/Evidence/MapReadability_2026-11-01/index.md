# Outdoor map evidence — 2026-11-01

Actual isolated Game View captures. Prepared positions/synthetic input are distinguished in the report. Source application hashes: [applied.json](applied.json).

|Area|Before|Final candidate|
|---|---|---|
|Town ground|[Before](before/town-2.png)|[After](final/town-center-1440.png)|
|Town exit|[Before](before/town-3.png)|[After](final/town-exit-1440.png)|
|Forest|[Before](before/field-0.png)|[After](final/forest-altar-1440.png)|
|Cave platforms/resources|[Before](before/field-6.png)|[After](final/cave-middle-1440.png)|
|Tutorial entrance|[Earlier entrance frame](before/tutorial-wakeup.png)|[1280x720](final/tutorial-mouth-1280.png), [1920x1080](final/tutorial-mouth-1920.png)|

The before/tutorial-wakeup filename is misleading: asynchronous capture recorded the later entrance pose, not the wakeup start. It is excluded from a pose-matched wakeup comparison. Random spawns/actor positions differ across sessions; they are not evidence of changed authored counts.

Other final captures: [Actual intro passer](final/intro-passer.png), [actual cave arrival](final/cave-entrance-1440.png), [map structure/warps](final/field-map-1440.png).

Pass results: [tour/movement](final-tour.json), [zero altar](altar-zero.json), [positive altar](altar-positive.json), [fresh reload](resume-neighbors.json), [NPC](intro-normal.json), [skip](intro-skip.json), [resources/F entrance/map](world-neighbors.json).

Structural proof: [scene/prefab audit](structural-audit.json), [new references](reference-audit.json), [save/CSV preservation](preservation-check.json). No Windows/native-input/full-route completion claim.
