// Custom backgrounds: a picture from a file, in a form Gauntlet can draw. Overview: README.md in this folder.

using System;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

namespace CustomisableUI.Backgrounds
{
    // A picture from a file on disk, drawable by Gauntlet where it would draw one of its own sprites. Sprite is abstract and
    // the draw call reads only three things from it, which is all this supplies: the texture, and the two corners of the
    // part of it to show. The corners are worked out at draw time from the shape of the area being filled, so the same
    // picture fills a 16:9 loading screen and an ultrawide main menu without stretching.
    internal sealed class FileSprite : Sprite
    {
        // The shape of the area the next draw fills, and whether to stretch rather than crop. Set by BackgroundDraw.Fit
        // just before the draw call that reads them, on the same thread.
        [ThreadStatic]
        private static float _areaAspect;

        [ThreadStatic]
        private static bool _stretch;

        private readonly TaleWorlds.TwoDimension.Texture _texture;

        public FileSprite(string name, TaleWorlds.TwoDimension.Texture texture, int width, int height)
            : base(name, width, height, default(SpriteNinePatchParameters))
        {
            _texture = texture;
        }

        public override TaleWorlds.TwoDimension.Texture Texture => _texture;

        public void PrepareDraw(float areaWidth, float areaHeight, bool stretch)
        {
            _areaAspect = areaHeight > 0f ? areaWidth / areaHeight : 0f;
            _stretch = stretch;
        }

        public override Vec2 GetMinUvs()
        {
            Corners(out float minU, out float minV, out _, out _);
            return new Vec2(minU, minV);
        }

        public override Vec2 GetMaxUvs()
        {
            Corners(out _, out _, out float maxU, out float maxV);
            return new Vec2(maxU, maxV);
        }

        private void Corners(out float minU, out float minV, out float maxU, out float maxV)
        {
            Cover(Width, Height, _stretch ? 0f : _areaAspect, out minU, out minV, out maxU, out maxV);
        }

        // The part of a picture that fills an area of the given shape without distortion: all of it when the shapes match,
        // otherwise the middle of it. An area aspect of 0 means "all of it" (stretched to the area).
        internal static void Cover(float imageWidth, float imageHeight, float areaAspect,
            out float minU, out float minV, out float maxU, out float maxV)
        {
            minU = 0f;
            minV = 0f;
            maxU = 1f;
            maxV = 1f;
            if (areaAspect <= 0f || imageWidth <= 0f || imageHeight <= 0f)
            {
                return;
            }

            float imageAspect = imageWidth / imageHeight;
            if (imageAspect > areaAspect)
            {
                // Wider than the area: show the middle, left to right.
                float visible = areaAspect / imageAspect;
                minU = (1f - visible) / 2f;
                maxU = 1f - minU;
            }
            else if (imageAspect < areaAspect)
            {
                // Taller than the area: show the middle, top to bottom.
                float visible = imageAspect / areaAspect;
                minV = (1f - visible) / 2f;
                maxV = 1f - minV;
            }
        }
    }
}
