using System;
using System.IO;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;
using EngineTexture = TaleWorlds.Engine.Texture;

namespace CustomisableUI
{
    // A plain white sprite: what a flat panel is drawn with. Every pixel is white, so whatever part of it the draw call
    // reads, multiplying by the material's colour gives exactly that colour. (Sprite is abstract and the draw call reads only
    // the texture and the two corners of the part of it to show.)
    //
    // The edged versions are white in the middle with a thin, see-through black border, drawn as a nine-patch like the game's
    // own frames: the border keeps its thickness however big the panel is. It is part of the picture, so a panel with an edge
    // is still one draw call, exactly like one without.
    internal sealed class SolidSprite : Sprite
    {
        // The border, in pixels of the picture (scaled with the interface when drawn).
        public const int EdgeWidth = 2;

        private readonly TaleWorlds.TwoDimension.Texture _texture;

        public SolidSprite(TaleWorlds.TwoDimension.Texture texture, int size, bool edged)
            : base(edged ? "cui_flat_fill_edged" : "cui_flat_fill", size, size,
                edged ? new SpriteNinePatchParameters(EdgeWidth, EdgeWidth, EdgeWidth, EdgeWidth) : default(SpriteNinePatchParameters))
        {
            _texture = texture;
            Edged = edged;
        }

        public bool Edged { get; }

        public override TaleWorlds.TwoDimension.Texture Texture => _texture;

        // A plain fill reads only the middle of its picture, away from the edges, where the texture filter could only ever
        // find more white. An edged one needs all of it.
        public override Vec2 GetMinUvs()
        {
            return Edged ? new Vec2(0f, 0f) : new Vec2(0.25f, 0.25f);
        }

        public override Vec2 GetMaxUvs()
        {
            return Edged ? new Vec2(1f, 1f) : new Vec2(0.75f, 0.75f);
        }
    }

    // The flat panel style: instead of tinting the game's panel artwork (which is multiplied with dark art, so colours need
    // brightness compensation), panels are drawn as a plain fill of the chosen colour, with a thin edge so that neighbouring
    // panels can be told apart. The draw hook (DrawSpritePatch) asks TryFlatten for each sprite it has decided is a panel; if
    // the answer is yes the sprite has been swapped for the plain fill, and Stylers.ApplyToFlatPanel then colours it. If the
    // answer is no, or the fill isn't ready, the panel is tinted as usual, so the worst case is the textured look.
    internal static class FlatPanels
    {
        // 4x4 white. Made into an engine texture the first time it is wanted.
        private const string FillPng =
            "iVBORw0KGgoAAAANSUhEUgAAAAQAAAAECAYAAACp8Z5+AAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAAPSURBVBhXY/iPBhhIFwAAeDw/wZciT7UAAAAASUVORK5CYII=";

        // 6x6: white, with a 2-pixel border of black at 25%, 40% and 60% (the subtle, normal and strong edge).
        private static readonly string[] EdgedPng =
        {
            null,
            "iVBORw0KGgoAAAANSUhEUgAAAAYAAAAGCAYAAADgzO9IAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAAdSURBVBhXY2BgYHDAgTEEUCX+QwHxElgwhgAYAwD4QBfxR9EMYAAAAABJRU5ErkJggg==",
            "iVBORw0KGgoAAAANSUhEUgAAAAYAAAAGCAYAAADgzO9IAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAAdSURBVBhXY2BgYEjDgTEEUCX+QwHxElgwhgAYAwBVfhyxeVv5ywAAAABJRU5ErkJggg==",
            "iVBORw0KGgoAAAANSUhEUgAAAAYAAAAGCAYAAADgzO9IAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAAdSURBVBhXY2BgYJiJA2MIoEr8hwLiJbBgDAEwBgAqLCMRKX3vawAAAABJRU5ErkJggg==",
        };

        // The strongest edge level (the settings offer 0 = none up to this).
        internal const int MaxEdgeLevel = 3;

        // A panel smaller than this (in pixels, either way) is too small for an edge to make sense: it gets a plain fill.
        private const float EdgeMinSize = 10f;

        // Art that isn't a surface: gradients, shadows and glows are drawn over other things (the game's popups draw one over
        // their text and buttons), and are see-through where they fade out. As a solid rectangle they would hide what is
        // under them, so they are never made flat. Neither are illustrations, which are pictures, not panels.
        private static readonly string[] OverlayWords =
        {
            "gradient", "overlay", "vignette", "fade", "shine", "mask", "shadow", "glow", "cover", "blur", "highlight", "particle",
            "illustration",
        };

        // Fixed-size artwork is only treated as a surface when its name says it is one.
        private static readonly string[] SurfaceWords = { "canvas", "background", "panel", "popup", "bg" };

        private static readonly SolidSprite[] Edged = new SolidSprite[MaxEdgeLevel + 1];
        private static readonly bool[] EdgedFailed = new bool[MaxEdgeLevel + 1];
        private static volatile bool _fillFailed;

        // The plain fill. The tests put one here in place of the engine's.
        internal static SolidSprite Fill { get; set; }

        // The fill with an edge of the given level (1 to MaxEdgeLevel), or null if it isn't ready.
        internal static SolidSprite EdgedFill(int level)
        {
            return level >= 1 && level <= MaxEdgeLevel ? Edged[level] : null;
        }

        internal static void SetEdgedFill(int level, SolidSprite sprite)
        {
            Edged[level] = sprite;
        }

        // Called every frame from the game's main thread (the engine only makes textures there). Does nothing until the
        // flat style is in use, then makes what it needs once.
        public static void Tick(ThemeSnapshot theme)
        {
            if (!theme.PanelFlat)
            {
                return;
            }

            if (Fill == null && !_fillFailed)
            {
                try
                {
                    TaleWorlds.TwoDimension.Texture texture = MakeTexture(FillPng, out int size);
                    Fill = new SolidSprite(texture, size, edged: false);
                    Log.Info("Flat panels: ready");
                }
                catch (Exception ex)
                {
                    _fillFailed = true;
                    Log.Error("Creating the flat panel fill", ex);
                }
            }

            int level = theme.PanelOutline;
            if (level >= 1 && level <= MaxEdgeLevel && Edged[level] == null && !EdgedFailed[level])
            {
                try
                {
                    TaleWorlds.TwoDimension.Texture texture = MakeTexture(EdgedPng[level], out int size);
                    Edged[level] = new SolidSprite(texture, size, edged: true);
                }
                catch (Exception ex)
                {
                    EdgedFailed[level] = true;
                    Log.Error("Creating the flat panel edge " + level, ex);
                }
            }
        }

        private static TaleWorlds.TwoDimension.Texture MakeTexture(string png, out int size)
        {
            EngineTexture engine = EngineTexture.CreateFromMemory(Convert.FromBase64String(png));
            if (engine == null || engine.Width <= 0 || engine.Height <= 0)
            {
                throw new InvalidDataException("the game could not read a flat panel picture");
            }

            size = engine.Width;
            return new TaleWorlds.TwoDimension.Texture(new TaleWorlds.Engine.GauntletUI.EngineTexture(engine));
        }

        // Prepares a panel sprite for a flat draw. Returns false to leave the panel textured: the fill isn't ready, or the
        // sprite is not a surface that can sensibly become a plain rectangle. `box` is the area it is being drawn into,
        // `brush` the name of the brush drawing it (null for a widget's own sprite) and `edgeLevel` how strong an edge to
        // give it (0 for none).
        public static bool TryFlatten(ref Sprite sprite, string brush, SimpleMaterial material, SimpleRectangle box, int edgeLevel)
        {
            string name = sprite.Name ?? string.Empty;
            bool wantsEdge = edgeLevel > 0 && box.Width >= EdgeMinSize && box.Height >= EdgeMinSize;
            SolidSprite edged = wantsEdge ? EdgedFill(edgeLevel) : null;

            if (Stylers.IsFlatFill(name))
            {
                // The game's own plain fills already are what this wants. A rectangular one that is to have an edge is
                // swapped for the edged fill; a round one, or one that needs no edge, only needs the colour.
                if (edged != null && name.IndexOf("Circle", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    Use(ref sprite, material, edged);
                }
                return true;
            }

            SolidSprite target = edged ?? Fill;
            if (target == null || material.OverlayEnabled || !CanFlatten(sprite, brush, box.Width, box.Height))
            {
                return false;
            }

            Use(ref sprite, material, target);
            return true;
        }

        // Draws the panel with `fill`. The material still describes the artwork that was replaced, so it is told about the
        // fill instead. (Both draw paths that reach here set this again, with everything else, the next time they use the
        // material.)
        private static void Use(ref Sprite sprite, SimpleMaterial material, SolidSprite fill)
        {
            sprite = fill;
            material.NinePatchParameters = fill.NinePatchParameters.IsValid ? fill.NinePatchParameters : SpriteNinePatchParameters.Empty;
        }

        // Stretchable (nine-patch) sprites are the game's panel surfaces and become plain rectangles. Fixed-size artwork does
        // too, if its name says it is a background and it is drawn big enough to be one. Anything that is drawn over other
        // things (see OverlayWords) never does.
        internal static bool CanFlatten(Sprite sprite, string brush, float width, float height)
        {
            string name = sprite.Name ?? string.Empty;
            if (RoleClassifier.ContainsAny(name, OverlayWords) || RoleClassifier.ContainsAny(brush ?? string.Empty, OverlayWords))
            {
                return false;
            }

            if (sprite.NinePatchParameters.IsValid)
            {
                return true;
            }

            int slash = name.LastIndexOfAny(new[] { '\\', '/' });
            string last = slash >= 0 ? name.Substring(slash + 1) : name;
            return RoleClassifier.ContainsAny(last, SurfaceWords) &&
                   width >= RoleClassifier.LargeBackgroundMinWidth && height >= RoleClassifier.LargeBackgroundMinHeight;
        }
    }
}
