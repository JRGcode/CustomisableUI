# Changelog

## Unreleased

New
- **Flat panels.** A new *Panel style* setting. *Flat* draws panels as a plain rectangle of exactly the colour you pick,
  instead of tinting the game's dark artwork, so colours come out as chosen and need no brightness compensation. Panels
  inside a window (popups, lists, rows) are drawn a step lighter (darker for a pale colour) so they stand out; *Flat panel
  depth* sets how much. Each flat panel also gets a thin, slightly darker edge so neighbouring panels can be told apart;
  *Flat panel outline* sets how strong it is. Frames and buttons are unchanged. Textured stays the default. Art the game draws over
  other things (gradients, shadows) and illustrations are never made flat.
- With *Keep text readable* on, a flat panel colour that no text can be read on (a mid-tone such as teal or slate) is moved
  toward black or white by just enough to reach the contrast target. Turn that off for the exact colour.

## 1.3.0

New
- **Custom backgrounds.** Put PNG or JPG pictures in `Backgrounds\MainMenu` or `Backgrounds\Loading` inside the mod and
  they are shown behind the main menu and on the loading screens (and the few menus that reuse the loading pictures).
  Nothing changes while the folders are empty. See the README.txt in each folder.
- **Loading screens setting.** Choose whether the loading screens show only your pictures, yours mixed with the game's,
  or only the game's. Works with War Sails, which has its own set of loading pictures. A *Backgrounds* group in the
  settings holds this, the on/off switch and *Picture fit* (fill or stretch).

Changed
- The custom backgrounds code is kept together in `src/CustomisableUI/Backgrounds`, with its own README.

## 1.2.0

New
- **Translation support.** Every setting name, explanation, group title, colour name, preset and the hotkey message can now
  be translated by adding a language folder under `Module/ModuleData/Languages`, with no code changes. Untranslated text
  stays in English. See `docs/TRANSLATING.md`. No translations are included yet.

## 1.1.0

New
- **Crosshair colours**: crosshair, blocked state, hit marker and kill/headshot marker, plus opacity.
- **Good / bad / warning colours**: one palette for gains, losses and warnings (text, and the inventory, party and crafting
  slider fills). The colour-blind palette now feeds the same system, and an explicit colour beats it.
- **Text shadow**: a soft dark halo behind text, with a colour.
- **Hotkey** (default Ctrl+Shift+U): switch between your theme and the vanilla look from anywhere.
- **Ally, enemy and neutral colours** for settlement bars, party names and troop counts on the map, party tooltips and
  battle markers.
- **Accessibility**: seven *Accessible: ...* presets, a minimum text contrast (3:1 / 4.5:1 / 7:1), *Text colour: Auto*, and a
  *Keep text readable* rule.
- **Compatibility** option *Keep colours added by other mods*, and `UiRoles.txt` rules.
- A test suite (`tests/`) that runs against the real game assemblies.

Changed
- Dropdowns, slider tracks and knobs, checkboxes and list entries now count as buttons.
- Flat colour fills use a toned-down colour and feel less of the brightness slider.
- Translucent full-screen layers (pause, autosave) are never themed.
- Party nameplate colours are recognised rather than patched in the game's nameplate classes.
- Crash diagnostics: a failing text lookup and any unhandled exception now write a full call stack to the log.
- Faster on busy screens: whether a widget's sprite can be a panel background is worked out once per sprite name instead of
  on every frame.

## 1.0.0

First version: panel, frame, button and text colours, fonts and text size, colour grading, presets.
