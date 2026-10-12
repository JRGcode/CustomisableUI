// Custom backgrounds: the feature's settings, resolved into plain values. Overview: README.md in this folder.

namespace CustomisableUI.Backgrounds
{
    internal enum BackgroundKind
    {
        // Behind the main menu.
        MainMenu,

        // On the loading screens (and the few menus that reuse the game's loading pictures).
        Loading,
    }

    // What the loading screens show. The order is the order of the dropdown (ModSettings.LoadingBackgroundChoices).
    internal enum LoadingBackgroundMode
    {
        // Only the player's pictures.
        Mine,

        // The player's and the game's, mixed: about half the loading screens show one of the player's.
        Both,

        // Only the game's own: the player's pictures are not used for loading screens.
        Game,
    }

    // The feature's settings as the draw hooks want them, read once when the snapshot is built (ThemeSnapshot.Background),
    // so the hooks never touch MCM objects.
    internal struct BackgroundStyle
    {
        // "Use custom backgrounds" (off, or the vanilla-look hotkey, means none of the feature's pictures are shown).
        public bool Enabled;

        // Stretch a picture to the area instead of cropping it to fit.
        public bool Stretch;

        public LoadingBackgroundMode LoadingScreens;

        // True if the loading screens may show the player's pictures at all.
        public bool UsesLoadingPictures => Enabled && LoadingScreens != LoadingBackgroundMode.Game;

        public static BackgroundStyle From(ModSettings settings)
        {
            int loading = settings.LoadingScreens.SelectedIndex;
            return new BackgroundStyle
            {
                Enabled = settings.CustomBackgrounds,
                Stretch = settings.BackgroundFit.SelectedIndex == 1,
                LoadingScreens = loading == 1 ? LoadingBackgroundMode.Both
                    : loading == 2 ? LoadingBackgroundMode.Game
                    : LoadingBackgroundMode.Mine,
            };
        }
    }
}
