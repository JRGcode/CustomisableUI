// Custom backgrounds: what the shared draw hook calls. Overview: README.md in this folder.

using TaleWorlds.TwoDimension;

namespace CustomisableUI.Backgrounds
{
    // The two calls DrawSpritePatch (Patches.cs) makes into this feature for every sprite it is about to draw.
    internal static class BackgroundDraw
    {
        // True for the sprites this feature takes an interest in: its own pictures, and the game's loading pictures. Cheap,
        // because it runs for every sprite drawn.
        public static bool Concerns(Sprite sprite)
        {
            return sprite is FileSprite || LoadingPictures.Slot(sprite) >= 0;
        }

        // Swaps one of the game's loading pictures for one of the player's when the settings and the loading screen's own
        // decision say so, and fits a picture to the area it is about to fill. Returns false to skip the draw, which is how
        // a picture goes away when custom backgrounds are switched off (or the vanilla-look hotkey is pressed).
        public static bool Prepare(ThemeSnapshot theme, ref Sprite sprite, ref Rectangle2D rectangle)
        {
            BackgroundStyle style = theme.Background;

            var custom = sprite as FileSprite;
            if (custom == null)
            {
                custom = LoadingPictures.ReplacementFor(style, sprite);
                if (custom == null)
                {
                    return true;
                }
                sprite = custom;
            }
            else if (!style.Enabled)
            {
                return false;
            }

            SimpleRectangle box = rectangle.GetBoundingBox();
            Fit(custom, box.Width, box.Height, style.Stretch);
            return true;
        }

        // Shapes the picture for an area of the given size, ready for the draw call that follows.
        internal static void Fit(FileSprite picture, float areaWidth, float areaHeight, bool stretch)
        {
            picture.PrepareDraw(areaWidth, areaHeight, stretch);
        }
    }
}
