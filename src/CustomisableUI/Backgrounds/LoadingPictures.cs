// Custom backgrounds: the loading screens. Overview: README.md in this folder.

using System;
using TaleWorlds.TwoDimension;

namespace CustomisableUI.Backgrounds
{
    // Which picture a loading screen shows. The game's loading window draws one full-screen sprite from a set of them; this
    // recognises those sprites, decides once per loading screen whether to show one of the player's pictures instead, and
    // says which. The swap itself happens in the draw hook (BackgroundDraw.Prepare).
    internal static class LoadingPictures
    {
        // The sprite categories the loading screens draw from: the base game's ("loading_01" to "loading_12"), and War
        // Sails' own when it is installed (its loading manager swaps the category, and its pictures are named after
        // cultures: "empire", "nord"...).
        private static readonly string[] Categories = { "ui_loading", "ui_naval_loading" };

        private static readonly object Gate = new object();
        private static readonly Random Chooser = new Random();

        // What the loading screen on show was given, decided when it began (see BeginScreen).
        private static bool _screenUsesMine;
        private static int _screenPick;
        private static int _lastPick = -1;

        // True while the loading window's own widget is being drawn, so its picture can be told from the same sprites used
        // elsewhere. Set by LoadingWindowRenderPatch.
        [ThreadStatic]
        internal static bool InLoadingWindow;

        // Which of the game's loading pictures a sprite is, counting from 0, or -1 if it isn't one. The sprite's sheet
        // number gives its place in its set, whichever set it is.
        internal static int Slot(Sprite sprite)
        {
            SpritePart part = (sprite as SpriteGeneric)?.SpritePart;
            string category = part?.Category?.Name;
            if (category == null)
            {
                return -1;
            }

            foreach (string known in Categories)
            {
                if (string.Equals(category, known, StringComparison.Ordinal))
                {
                    return part.SheetID > 0 ? part.SheetID - 1 : 0;
                }
            }
            return -1;
        }

        internal static bool ScreenUsesMine
        {
            get
            {
                lock (Gate)
                {
                    return _screenUsesMine;
                }
            }
        }

        internal static int ScreenPick
        {
            get
            {
                lock (Gate)
                {
                    return _screenPick;
                }
            }
        }

        internal static void Reset()
        {
            lock (Gate)
            {
                _screenUsesMine = false;
                _lastPick = -1;
            }
        }

        // A loading screen has been given a new picture by the game. Decides, for the whole of this screen, whether it
        // shows one of the player's pictures and which, so it doesn't change while the screen is up. Never the same one
        // twice running when there is a choice.
        public static void BeginScreen(ThemeSnapshot theme)
        {
            LoadingBackgroundMode mode = theme.Background.LoadingScreens;
            int count = theme.Background.UsesLoadingPictures ? BackgroundImages.Images(BackgroundKind.Loading).Count : 0;

            lock (Gate)
            {
                _screenUsesMine = count > 0 && (mode == LoadingBackgroundMode.Mine || Chooser.Next(2) == 0);
                if (!_screenUsesMine)
                {
                    return;
                }

                int pick = Chooser.Next(count);
                if (count > 1 && pick == _lastPick)
                {
                    pick = (pick + 1 + Chooser.Next(count - 1)) % count;
                }
                _screenPick = pick;
                _lastPick = pick;
            }
        }

        // The player's picture to draw in place of this sprite, or null to leave the game's.
        public static FileSprite ReplacementFor(BackgroundStyle style, Sprite sprite)
        {
            int slot = Slot(sprite);
            if (slot < 0 || !style.UsesLoadingPictures)
            {
                return null;
            }

            if (InLoadingWindow)
            {
                // A loading screen shows the picture decided when it began.
                int pick;
                lock (Gate)
                {
                    if (!_screenUsesMine)
                    {
                        return null;
                    }
                    pick = _screenPick;
                }
                return BackgroundImages.Pick(BackgroundKind.Loading, pick);
            }

            // The few other places that reuse the game's loading pictures (behind some campaign menus, the profile
            // screen) get one of the player's by the game's own picture number.
            return BackgroundImages.Pick(BackgroundKind.Loading, slot);
        }
    }
}
