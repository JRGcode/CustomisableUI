// Custom backgrounds: this feature's part of the settings screen ("11. Backgrounds"). Overview: README.md in this folder.

using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Common;

// This is the same namespace as the rest of ModSettings, not CustomisableUI.Backgrounds: a class can only be split across
// files within one namespace.
namespace CustomisableUI
{
    // ModSettings is one class in two files, so the options that belong to custom backgrounds sit with the rest of that
    // feature's code. MCM reads the attributes off the properties wherever in the class they are declared. The values are
    // read into a BackgroundStyle (BackgroundStyle.From) when the theme snapshot is built.
    public sealed partial class ModSettings
    {
        private const string BackgroundsGroup = "{=CUI_Group_Backgrounds}11. Backgrounds";

        // The order matters: BackgroundStyle reads index 1 as "stretch".
        internal static readonly string[] BackgroundFitChoices =
        {
            "{=CUI_BackgroundFit_Fill}Fill the screen (crops the edges)",
            "{=CUI_BackgroundFit_Stretch}Stretch to fit (may distort)",
        };

        // The order matters: BackgroundStyle reads the index as Mine, Both, Game.
        internal static readonly string[] LoadingBackgroundChoices =
        {
            "{=CUI_LoadingScreens_Mine}My pictures only",
            "{=CUI_LoadingScreens_Both}My pictures and the game's, mixed",
            "{=CUI_LoadingScreens_Game}The game's pictures only (none of mine)",
        };

        [SettingPropertyBool("{=CUI_CustomBackgrounds}Use custom backgrounds", Order = 0, RequireRestart = false,
            HintText = "{=CUI_CustomBackgrounds_Hint}Shows your own pictures instead of the game's. Put PNG or JPG files in the " +
                       "mod's Backgrounds folder: MainMenu for the main menu, Loading for the loading screens (and the few " +
                       "menus that reuse them). New files are picked up the next time the screen appears. Nothing changes " +
                       "while the folders are empty. Off, or the hotkey, brings back the game's own.")]
        [SettingPropertyGroup(BackgroundsGroup, GroupOrder = 10)]
        public bool CustomBackgrounds { get; set; } = true;

        [SettingPropertyDropdown("{=CUI_LoadingScreens}Loading screens", Order = 1, RequireRestart = false,
            HintText = "{=CUI_LoadingScreens_Hint}Which pictures the loading screens show, when there are pictures in the " +
                       "Backgrounds Loading folder. Mixed shows one of yours on about half the loading screens and one of " +
                       "the game's on the rest. Needs Use custom backgrounds to be on.")]
        [SettingPropertyGroup(BackgroundsGroup, GroupOrder = 10)]
        public Dropdown<string> LoadingScreens { get; set; } = new Dropdown<string>(LoadingBackgroundChoices, 0);

        [SettingPropertyDropdown("{=CUI_BackgroundFit}Picture fit", Order = 2, RequireRestart = false,
            HintText = "{=CUI_BackgroundFit_Hint}How a picture that is a different shape from the screen is fitted. Fill keeps " +
                       "it undistorted and crops the edges; Stretch shows all of it, squashed to the screen's shape.")]
        [SettingPropertyGroup(BackgroundsGroup, GroupOrder = 10)]
        public Dropdown<string> BackgroundFit { get; set; } = new Dropdown<string>(BackgroundFitChoices, 0);
    }
}
