# Interior generation — pending rights confirmation

Status: BLOCKED, not executed. The image generation tool is available. No image was generated or imported, and no existing interior artwork was replaced. Do not treat this prompt draft as an output asset.

The owner requested checking asset conditions before reference-based generation. Unity Asset Store Terms3.8 restrict AI-input use without express consent; the package source/any separate applicable permission remains unconfirmed. The Cainos product page permits game use/modification generally but does not establish this project's AI-reference permission.

Sources checked2026-11-01: https://unity.com/legal/as-terms ; https://cainos.itch.io/pixel-art-platformer-village-house ; https://docs.cainos.net/pixel-art-platformer-village-house/setup-guide

## Measured existing world room

SampleScene/SmithyInterior, origin(-252,62). Ground contact y62. Actual standing Game camera orthographic8, y66; at1440x900 viewport25.6x16. Observed camera centers x[-265.70,-238.30], combined visible x[-278.5,-225.5], y[58,74]. Wider-aspect/jump/shake overscan must be checked again before the final image mapping.

|Existing object|World x|Baseline y|
|---|---|---|
|Exit door|-275|floor62, graphic center64.60|
|Window|-267|center68.20|
|Rack|-260|floor62|
|Chest|-252|floor62|
|Crafting door|-243|floor62, frame center65.20|
|Bed|-233|floor62, graphic center63.40|

BackWall50x12 at(-252,68), floor graphic50x2 at(-252,61), pillars x[-277,-268,-257,-246,-236,-227], crossbeams y[62.4,72.7,73.7]. All station triggers/camera/ground/entry and exit coordinates must remain unchanged.

Actual movable world interior identified: the smithy. The workshop is an existing crafting presentation mode entered from CraftingDoor; its controls and unique illustration remain unchanged. If it is included in the eventual background replacement, measure its background/hotspot layers separately rather than replacing a flattened image that contains functional equipment. Legacy Home UI and shop transaction/dialogue panels are not new rooms.

## Reference plan (do not send until permission confirmed)

- Selected exterior: Assets/source/Cainos/Pixel Art Platformer - Village House/Prefab/PF House04.prefab, original native component atlas and corresponding preset.
- Native palette/material language: subdued tan timber, grey/beige stone/plaster, sharp32PPU-style pixel edges. Keep original asset files intact.
- Room baseline screenshots: Docs/Evidence/CainosBuildings_2026-11-01/before/interior-{0,2,4}.png. These contain UI/furniture for spatial measurement, not requested image content.

## Unexecuted prompt draft

Produce a wide orthographic side-view pixel-art blacksmith architectural interior background. After permission is established, use the selected exterior ONLY as a material/palette/pixel-craft reference, never as a roof pasted indoors. Render structural wall, timber beams/columns and continuous floor; subdued contrast behind the existing player and functional furniture. Design to the measured world floor y62 and existing doorway/rack/chest/workshop/bed coordinates listed above, with overscan around the camera range. Keep the exit/window/workshop openings visually neutral so the existing foreground sprites supply their actual forms. Do not draw any duplicate or fake doors, workbenches, racks, chests, beds, furnaces, flame, player, NPC, tools, HUD, text or buttons. No perspective convergence, blurry edges or photorealistic texture. No new functional furniture or apparent routes. Prefer one architectural layer; add transparency/layers only if necessary.

After generation: inspect raster and pixel edges, iterate unsuitable output; keep fixed furniture/door sprites separate; map only visual layers, align floor, check all camera ends and3 aspect ratios. Preserve collision/IDs/UI layout. Store selected output and meta under a new project-owned art folder; record actual tool prompt/reference/output paths. This folder/output does not exist yet.
