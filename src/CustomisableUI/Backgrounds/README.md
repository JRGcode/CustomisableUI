# Custom backgrounds (code map)

Everything for the **background and loading screen replacer** is in this folder, in the `CustomisableUI.Backgrounds`
namespace. If you want to read, change or remove that feature, you shouldn't need to look anywhere else except the handful
of marked places listed under [Where it touches the rest of the mod](#where-it-touches-the-rest-of-the-mod).

For players, the feature is: drop PNG or JPG files into `Backgrounds\MainMenu` or `Backgrounds\Loading` inside the mod, and
they replace the main menu's background and the loading screen pictures. See the README.txt in each of those folders and
the "Custom backgrounds" section of the main README.

## The files

| File | What it is |
| --- | --- |
| `BackgroundStyle.cs` | The feature's settings as plain values (`BackgroundStyle`), plus the `BackgroundKind` and `LoadingBackgroundMode` enums. Held by `ThemeSnapshot.Background`. |
| `ModSettings.Backgrounds.cs` | The feature's options on the MCM screen ("11. Backgrounds"). A second file of the partial `ModSettings` class. |
| `BackgroundImages.cs` | The player's picture files: listing the folders, loading a file into an engine texture, the "main thread only" rule, loading ahead of time. |
| `FileSprite.cs` | A `Sprite` backed by a picture file. Works out, at draw time, which part of the picture fills the area (crop or stretch). |
| `LoadingPictures.cs` | The loading screens: which sprites are the game's loading pictures, and the once-per-screen decision about what to show. |
| `MainMenuBackground.cs` | Adds a full-screen picture widget behind the main menu. |
| `BackgroundDraw.cs` | The two calls the shared draw hook makes into the feature for every sprite: `Concerns` and `Prepare`. |
| `BackgroundPatches.cs` | The feature's Harmony hooks, and `BackgroundFeature`: the one class the rest of the mod uses (its hook list and per-frame tick). |

The tests are `tests/CustomisableUI.Tests/BackgroundChecks.cs`.

## How it works

**Main menu.** The game's main menu background is not a sprite: it is a video the engine plays behind the interface
(`MBInitialScreenBase`, from `Videos\initial_menu\<name>\` in each module), so it can't be recoloured or replaced from the UI
layer. Instead `InitialScreenBackgroundPatch` waits for `GauntletInitialScreen.OnInitialize` to build its Gauntlet layer, and
`MainMenuBackground` puts a full-screen picture widget at the bottom of that layer's widget tree. The video, and its music,
carry on underneath.

**Loading screens.** The loading window (`LoadingWindowWidget`) draws one full-screen sprite from a set of pictures:

- `ui_loading` (`loading_01` to `loading_12`) in the base game, or
- `ui_naval_loading` (named `aserai`, `empire`, `nord`, `pirate`...) when War Sails is installed, because its
  `GauntletNavalLoadingWindowManager` swaps the category.

`LoadingPictures.Slot` recognises these by the sprite's **category** and uses its sheet number as its place in the set
(matching on names alone misses War Sails' set). The same sprites also back a couple of other places (the campaign menu
fallback `GameMenuBackground`, the profile selection screen), which get a picture by slot number.

Each time the game gives the loading window a new picture, `LoadingImagePatch` calls `LoadingPictures.BeginScreen`, which
decides for that whole screen whether to show one of the player's pictures (per the *Loading screens* setting: mine only,
mixed about half and half, or the game's only) and which, avoiding the same one twice running.
`LoadingWindowRenderPatch` notes when the loading widget itself is being drawn, so that decision applies only there.

**Drawing.** The swap happens in the shared `DrawSpritePatch` (`Patches.cs`): it asks `BackgroundDraw.Concerns` about each
sprite and, for the feature's, hands over to `BackgroundDraw.Prepare`, which swaps the sprite for a `FileSprite` and fits it
to the area being drawn. Returning false from there skips the draw, which is how a picture goes away when the setting is off
or the vanilla-look hotkey is pressed. `FileSprite` is a `Sprite` subclass: the draw call only reads its texture and two UV
corners, so nothing else is needed.

**Threads.** The engine's textures are only made on the game's main thread, but a loading screen is drawn while the game is
busy loading, often from another thread. So `BackgroundImages.Tick` (run every frame from `SubModule.OnApplicationTick`)
loads the loading pictures ahead of time, one at a time, and `BackgroundImages.Load` refuses to create a texture off the main
thread. A picture that isn't ready is simply not used that time.

## Where it touches the rest of the mod

Every place outside this folder that knows about the feature is marked with a `[Backgrounds]` comment (search for it):

| File | The touch |
| --- | --- |
| `SubModule.cs` | `PatchTypes` includes `BackgroundFeature.PatchTypes`, and `OnApplicationTick` calls `BackgroundFeature.Tick`. |
| `Patches.cs` | `DrawSpritePatch` calls `BackgroundDraw.Concerns` / `Prepare` first. It takes the sprite by `ref` and returns a bool for this. |
| `ThemeSnapshot.cs` | One field, `Background` (a `BackgroundStyle`), set by `BackgroundStyle.From(settings)`. |
| `ModSettings.cs` | The class is `partial`. |
| `Module\Backgrounds\` | The folders players put pictures in, each with a README.txt. `.gitignore` keeps their pictures out of the repository. |
| `Module\ModuleData\Languages\EN\strings.xml` | The feature's text, like everything else (regenerated from the code; see `docs/TRANSLATING.md`). |
| `tests\...\Program.cs`, `Tests.cs`, `Translations.cs` | Run `BackgroundChecks`; the hook-binding check counts distinct target methods; the string extractor finds every `*Choices` list. |

## Removing it

Delete this folder, `Module\Backgrounds\`, `tests\CustomisableUI.Tests\BackgroundChecks.cs`, then take out the marked lines in
`SubModule.cs`, `Patches.cs` (the first block in `DrawSpritePatch.Prefix`, and the `ref`/bool signature if you like),
`ThemeSnapshot.cs` and `ModSettings.cs` (the `partial` keyword), and regenerate the English strings.

## Trying it in the game

Put a picture in each folder and start the game. `Documents\Mount and Blade II Bannerlord\Logs\CustomisableUI.log` has a
`Custom background loaded: <file> (<width>x<height>)` line for each picture the engine accepted, `Main menu background: ...`
each time the main menu picks one, or an error naming the file that failed. Turn on *Write a debug log* in the settings for
more.
