# UI test scene

Open `Assets/UITest/UITestScene.unity` and enter Play Mode. The left-side test
buttons open the named production UI screen, including the town and field
player-information views. Escape closes the current screen; E also closes the
town-inventory and field-bag previews.
The selector's save-history entry opens the new settings tab with the in-memory
test fixture; its rows cannot load or write game progress.
The project scene is separate from `Assets/JinHo` and is not in the build list.

## Data

`Data/UITestSave.json` is the only progress fixture. The scene reads it as a
`TextAsset` and parses it into memory before passing its `smith` data to the real
`BlacksmithController`. It contains every catalog item at count 99 in the
chest, bag and field, and one craft for every enabled recipe. Edit `gold`,
`day`, `night`, `bagTier`, `equipment`, stacks, recipe `crafts`, `resultItemId`, or other
serialized fields in this JSON, then re-enter Play Mode. There is no runtime
code path that writes this fixture.

The campaign preview uses the game's `CampaignUI`, `CampaignTradeUI`,
`TownStorageUI`, `TownHud`, `TownDialogue`, and `PlayerInfoUI`. The smithy preview uses the
copied BlacksmithShop scene and its real `BlacksmithController` and
`BlacksmithView`. Field bag, offering, health gauge and HUD use their real
runtime controllers. The integrated menu uses the production `GameUIController`
and a test-only copy of the GameUI prefab with its root initially inactive;
the copy retains the same production component scripts. Its settings store is
replaced with an in-memory store before the prefab is activated. Settings
values come from `settingsJson` in the fixture. Control rebind actions are
unavailable in the test because they write directly to PlayerPrefs.

The settings save-history tab reuses the production title-list prefab. It
shows one in-memory fixture row and seven empty rows; none can load or write
game progress, and the test scene does not enumerate real saves.

The test's fake `SmithingLoop` never starts its save lifecycle and has
`SaveBlocked=true`. The blacksmith session writer is a no-op. Known save,
load, quit, and return-to-title buttons are disabled where production UI creates
them. The Editor-only God Mode button is also disabled in this scene because
it creates a writable session. The separate campaign manual save/load screen
and title-scene history screen are not offered here because those controllers
read ordinary save paths. The settings history tab uses the fixture instead.

The `GameUIReadOnly.prefab` copy should be refreshed from the production
`Assets/JinHo/Resources/UI/GameUI.prefab` if that prefab's authored hierarchy
changes. Script changes are shared automatically.


## Ending-only preview (2026-10-12)

Open `EndingTestScene.unity` independently, then enter Play. Ending 1/2 use the real ending dialogue and StoryDialogue prefab. Next/Space advances, held S speeds up. Credits now close dialogue and open the real fullscreen ending card; both card buttons return to test selection. Esc interrupts either dialogue or card back to selection. No gameplay services or save writer are installed. The scene is not in the release build list. See Docs/Evidence/EndingTestScene_2026-10-12.json for actual verification and the native-key injection limitation.
