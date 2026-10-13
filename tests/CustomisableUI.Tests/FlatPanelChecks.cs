using System;
using System.Linq;
using MCM.Abstractions;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

namespace CustomisableUI.Tests
{
    // The flat panel style: what the settings turn into, the colours it produces, which sprites it will and won't turn into a
    // plain rectangle, and what it does to the material. Putting a fill on the screen needs the running engine (it makes a
    // texture), so the tests stand in for it; that part is covered by trying it in the game.
    internal static class FlatPanelChecks
    {
        private static Color Rgb(string hex)
        {
            Swatches.TryParseHex(hex, out Color c);
            return c;
        }

        private static bool Same(Color a, Color b, float tolerance = 0.01f)
        {
            return Math.Abs(a.Red - b.Red) <= tolerance && Math.Abs(a.Green - b.Green) <= tolerance &&
                   Math.Abs(a.Blue - b.Blue) <= tolerance && Math.Abs(a.Alpha - b.Alpha) <= tolerance;
        }

        private static ModSettings Flat(string swatch, float strength = 1f, float brightness = 1f, float depth = 0.5f, float opacity = 1f)
        {
            var s = new ModSettings();
            s.PanelStyle.SelectedIndex = 1;
            s.PanelColour.SelectedIndex = Swatches.IndexOf(swatch);
            s.PanelStrength = strength;
            s.PanelBrightness = brightness;
            s.PanelDepth = depth;
            s.PanelOpacity = opacity;
            return s;
        }

        // A sprite as the game's sprite data makes them, with or without stretchable (nine-patch) borders.
        private static Sprite GameSprite(string name, bool ninePatch)
        {
            var part = new SpritePart(name, new SpriteCategory("ui_general", 12, false), 256, 256) { SheetID = 1 };
            SpriteNinePatchParameters nine = ninePatch ? new SpriteNinePatchParameters(10, 10, 10, 10) : default(SpriteNinePatchParameters);
            return new SpriteGeneric(name, part, in nine);
        }

        private static SimpleRectangle Box(float w, float h)
        {
            return new SimpleRectangle { X = 0f, Y = 0f, X2 = w, Y2 = h };
        }

        public static void Run()
        {
            Check.Section("Flat panels");

            Settings();
            Colours();
            Levels();
            Material();
            Flatten();
            Outlines();
            Readability();
        }

        // ---- the settings ----------------------------------------------------------------------------------------------
        private static void Settings()
        {
            var defaults = new ModSettings();
            Check.Equal(0, defaults.PanelStyle.SelectedIndex, "panels are textured unless the player asks for flat");
            Check.That(!ThemeSnapshot.From(defaults).PanelFlat, "...so a fresh install changes nothing");

            var noColour = new ModSettings();
            noColour.PanelStyle.SelectedIndex = 1;
            Check.That(!ThemeSnapshot.From(noColour).PanelFlat, "flat with the vanilla colour has nothing to fill with, so panels stay as they are");

            Check.That(ThemeSnapshot.From(Flat("Navy")).PanelFlat, "flat with a panel colour is flat");
            Check.That(!ThemeSnapshot.From(Flat("Navy", strength: 0f)).PanelFlat, "...but not with the colour at 0% strength");

            var textured = Flat("Navy");
            textured.PanelStyle.SelectedIndex = 0;
            Check.That(!ThemeSnapshot.From(textured).PanelFlat, "choosing textured turns it off again");

            Check.Near(1f, ThemeSnapshot.From(Flat("Navy", depth: 7f)).PanelDepth, 0f, "the depth can't go above 100%");
            Check.Near(0f, ThemeSnapshot.From(Flat("Navy", depth: -1f)).PanelDepth, 0f, "...or below 0%");
            Check.Equal(2, ModSettings.PanelStyleChoices.Length, "there are two styles to choose from");
        }

        // ---- the colour a flat panel ends up ---------------------------------------------------------------------------
        private static void Colours()
        {
            Color navy = Rgb("#2A3F8F");
            Color window = Stylers.FlatPanelStart(ThemeSnapshot.From(Flat("Navy")).Panel);

            Check.That(Same(window, navy), "a window is exactly the colour that was picked");
            Check.That(Same(Stylers.FlatPanelColour(window, 0, 1f), navy), "...at the window level, whatever the depth setting");

            Color half = Stylers.FlatPanelStart(ThemeSnapshot.From(Flat("Navy", strength: 0.5f)).Panel);
            Check.Near(0.08f + (navy.Red - 0.08f) * 0.5f, half.Red, 0.002f, "half strength is halfway from a neutral dark to the colour");

            Color level1 = Stylers.FlatPanelColour(window, 1, 1f);
            Color level2 = Stylers.FlatPanelColour(window, 2, 1f);
            Check.That(Contrast.Luminance(window) < Contrast.Luminance(level1) && Contrast.Luminance(level1) < Contrast.Luminance(level2),
                "a dark colour gets lighter the deeper a panel is nested");
            Check.Near(navy.Red + (1f - navy.Red) * 2f * Stylers.DepthStep, level2.Red, 0.002f, "...by a step of " + Stylers.DepthStep + " toward white per level at full depth");
            Check.That(Same(Stylers.FlatPanelColour(window, 2, 0f), navy), "with the depth at 0% every level is the same colour");

            Color pale = Stylers.FlatPanelStart(ThemeSnapshot.From(Flat("Parchment")).Panel);
            Check.That(Contrast.Luminance(Stylers.FlatPanelColour(pale, 2, 1f)) < Contrast.Luminance(pale),
                "a pale colour gets darker the deeper a panel is nested");

            Color white = Stylers.FlatPanelStart(ThemeSnapshot.From(Flat("White")).Panel);
            Color deepest = Stylers.FlatPanelColour(white, 2, 1f);
            Check.That(deepest.Red <= 1f && deepest.Red > 0.5f, "white panels stay light at the deepest level");

            // A mid-tone can't be read with light or dark text, so with "keep text readable" it is nudged just far enough.
            ThemeSnapshot teal = ThemeSnapshot.From(Flat("Teal"));
            Color exactTeal = Rgb("#2A9A98");
            Check.That(!Same(teal.PanelFill, exactTeal, 0.005f), "a mid-tone panel colour is moved a little so that text can be read on it");
            Check.That(Contrast.Luminance(teal.PanelFill) < Contrast.Luminance(exactTeal), "...darker, since light text suits teal better than dark");
            Check.That(Contrast.MinRatio(Contrast.LightText, teal.PanelSurface, teal.PanelSurface) >= teal.ContrastTarget - 0.05f,
                "...until text reaches the contrast target (" + teal.ContrastTarget + ":1)");
            Check.That(Contrast.Luminance(teal.PanelFill) > Contrast.Luminance(exactTeal) * 0.4f, "...and not much further than that");

            ModSettings exact = Flat("Teal");
            exact.KeepTextReadable = false;
            Check.That(Same(ThemeSnapshot.From(exact).PanelFill, exactTeal), "with 'keep text readable' off the colour is exactly as picked");
            Check.That(Same(ThemeSnapshot.From(Flat("Navy")).PanelFill, navy), "a colour text can already be read on is not touched");
            Check.That(Same(ThemeSnapshot.From(Flat("Parchment")).PanelFill, Rgb("#E8D5A8")), "...and neither is a pale one");

            ModSettings strict = Flat("Slate");
            strict.ContrastTarget.SelectedIndex = 2;
            ThemeSnapshot strictSnapshot = ThemeSnapshot.From(strict);
            Check.That(Contrast.Luminance(strictSnapshot.PanelFill) < Contrast.Luminance(ThemeSnapshot.From(Flat("Slate")).PanelFill) ||
                       Contrast.Luminance(strictSnapshot.PanelFill) > Contrast.Luminance(ThemeSnapshot.From(Flat("Slate")).PanelFill),
                "a stricter contrast target moves a mid-tone further");
        }

        // ---- how deep a panel counts as being ----------------------------------------------------------------------------
        private static void Levels()
        {
            Check.Equal(0, Stylers.DepthLevel(1f), "a full-screen panel is a window");
            Check.Equal(0, Stylers.DepthLevel(Stylers.WindowFraction), "...and so is one a fifth of the screen");
            Check.Equal(1, Stylers.DepthLevel(Stylers.WindowFraction - 0.01f), "a panel smaller than that sits inside a window");
            Check.Equal(1, Stylers.DepthLevel(Stylers.InnerFraction), "...down to a twenty-fifth of the screen");
            Check.Equal(2, Stylers.DepthLevel(Stylers.InnerFraction - 0.001f), "anything smaller (rows, slots) is deeper still");
            Check.Equal(2, Stylers.DepthLevel(0f), "a panel with no area is the deepest");

            // ScreenDimmingChecks has set the screen to 1920x1080.
            Check.Near(1f, RoleClassifier.ScreenFraction(Box(1920f, 1080f)), 0.0001f, "a screen-sized area covers the screen");
            Check.Near(0.25f, RoleClassifier.ScreenFraction(Box(960f, 540f)), 0.0001f, "a quarter-size area covers a quarter");
            Check.Near(1f, RoleClassifier.ScreenFraction(Box(4000f, 3000f)), 0.0001f, "an area bigger than the screen is all of it");
        }

        // ---- what the draw does to the material ----------------------------------------------------------------------------
        private static void Material()
        {
            Color navy = Rgb("#2A3F8F");
            ThemeSnapshot theme = ThemeSnapshot.From(Flat("Navy", brightness: 1.6f, opacity: 0.5f));

            var material = new SimpleMaterial { Color = new Color(0.9f, 0.8f, 0.7f, 0.8f) };
            Stylers.ApplyToFlatPanel(theme, material, 1f);
            Check.That(Same(material.Color, new Color(navy.Red, navy.Green, navy.Blue, 0.8f)),
                "the layer takes the exact colour, whatever it was, and keeps its own transparency");
            Check.Near(1f + 0.6f * 0.3f, material.ColorFactor, 0.001f, "brightness is eased, so presets made for textured art don't over-brighten");
            Check.Near(0.5f, material.AlphaFactor, 0.001f, "opacity applies");

            var inner = new SimpleMaterial { Color = new Color(1f, 1f, 1f, 1f) };
            Stylers.ApplyToFlatPanel(theme, inner, 0.01f);
            Check.That(Contrast.Luminance(inner.Color) > Contrast.Luminance(navy), "a small panel is drawn a step lighter than a window");

            ModSettings graded = Flat("Navy");
            graded.HueShift = 90;
            var hue = new SimpleMaterial { Color = new Color(1f, 1f, 1f, 1f) };
            Stylers.ApplyToFlatPanel(ThemeSnapshot.From(graded), hue, 1f);
            Check.Near(0.25f, hue.HueFactor, 0.001f, "colour grading still applies to flat panels");

            // The strongest the slider goes is gentler than on textured art.
            Check.Near(1f + 1.5f * 0.3f, Stylers.FlatBrightness(ThemeSnapshot.From(Flat("Navy", brightness: 2.5f)).Panel), 0.001f, "the brightness slider's top is 1.45x for a flat fill");
        }

        // ---- which sprites become plain rectangles ---------------------------------------------------------------------------
        private static void Flatten()
        {
            SolidSprite saved = FlatPanels.Fill;
            try
            {
                var fill = new SolidSprite(new TaleWorlds.TwoDimension.Texture(new BackgroundChecks.FakeTexture()), 4, edged: false);
                Check.That(fill.Width == 4 && fill.Texture != null && fill.NinePatchParameters.IsValid == false, "the fill is a small, plain, non-stretching picture");
                Vec2 min = fill.GetMinUvs();
                Vec2 max = fill.GetMaxUvs();
                Check.That(min.x > 0f && min.y > 0f && max.x < 1f && max.y < 1f, "...of which only the middle is ever read");

                // The fill isn't ready yet: nothing changes.
                FlatPanels.Fill = null;
                Sprite panel = GameSprite("popup_canvas_9", ninePatch: true);
                Sprite before = panel;
                Check.That(!FlatPanels.TryFlatten(ref panel, null, new SimpleMaterial(), Box(800f, 600f), 0) && ReferenceEquals(panel, before),
                    "before the fill is ready a panel is left to be tinted");

                FlatPanels.Fill = fill;

                // A stretchable panel becomes the fill, and stops being described as stretchable.
                var material = new SimpleMaterial { NinePatchParameters = new SpriteNinePatchParameters(10, 10, 10, 10) };
                panel = GameSprite("popup_canvas_9", ninePatch: true);
                Check.That(FlatPanels.TryFlatten(ref panel, "Popup.Background", material, Box(800f, 600f), 0), "a stretchable panel can be flat");
                Check.That(ReferenceEquals(panel, fill), "...and is drawn with the fill");
                Check.That(!material.NinePatchParameters.IsValid, "...which is not described as having stretchable borders");

                // The game's own plain fills only need a colour.
                Sprite plain = GameSprite("BlankWhiteSquare_9", ninePatch: true);
                Sprite plainBefore = plain;
                Check.That(FlatPanels.TryFlatten(ref plain, null, new SimpleMaterial(), Box(800f, 600f), 0) && ReferenceEquals(plain, plainBefore),
                    "one of the game's plain fills is kept as it is");
                Sprite circle = GameSprite("BlankWhiteCircle", ninePatch: false);
                Sprite circleBefore = circle;
                Check.That(FlatPanels.TryFlatten(ref circle, null, new SimpleMaterial(), Box(40f, 40f), 0) && ReferenceEquals(circle, circleBefore),
                    "...so a round one stays round");

                // Fixed-size artwork only when its name says it is a surface, and it is drawn big enough to be one.
                Sprite art = GameSprite("StdAssets/Popup/canvas", ninePatch: false);
                Check.That(FlatPanels.TryFlatten(ref art, null, new SimpleMaterial(), Box(512f, 645f), 0) && ReferenceEquals(art, fill),
                    "the popup's fixed-size canvas, drawn large, is a surface and can be flat");
                art = GameSprite("StdAssets/Popup/canvas", ninePatch: false);
                Sprite artBefore = art;
                Check.That(!FlatPanels.TryFlatten(ref art, null, new SimpleMaterial(), Box(200f, 60f), 0) && ReferenceEquals(art, artBefore),
                    "...but the same art drawn small is left alone");
                art = GameSprite("header_ornament", ninePatch: false);
                artBefore = art;
                Check.That(!FlatPanels.TryFlatten(ref art, null, new SimpleMaterial(), Box(900f, 500f), 0) && ReferenceEquals(art, artBefore),
                    "fixed-size artwork that isn't named like a surface is left alone however big it is");
                art = GameSprite("stone_texture", ninePatch: false);
                artBefore = art;
                Check.That(!FlatPanels.TryFlatten(ref art, null, new SimpleMaterial(), Box(900f, 500f), 0) && ReferenceEquals(art, artBefore),
                    "...as is a texture");

                // Things drawn over other things must stay see-through. The game's popup draws canvas_gradient over its
                // text and buttons; as a solid rectangle it hid them.
                foreach (string overlay in new[] { "StdAssets/Popup/canvas_gradient","popup_gradient_background_9", "left_panel_coverup", "stone_texture_overlay", "panel_shadow_9", "SPGeneral/background_illustration_clans" })
                {
                    Sprite over = GameSprite(overlay, ninePatch: overlay.EndsWith("_9", StringComparison.Ordinal));
                    Sprite overBefore = over;
                    Check.That(!FlatPanels.TryFlatten(ref over, null, new SimpleMaterial(), Box(900f, 600f), 0) && ReferenceEquals(over, overBefore),
                        "'" + overlay + "' is an overlay and is never made solid");
                }
                Sprite fromBrush = GameSprite("some_panel_9", ninePatch: true);
                Sprite fromBrushBefore = fromBrush;
                Check.That(!FlatPanels.TryFlatten(ref fromBrush, "Popup.Gradient.Overlay", new SimpleMaterial(), Box(900f, 600f), 0) && ReferenceEquals(fromBrush, fromBrushBefore),
                    "a brush with an overlay-style name is never made solid either");

                // An overlay on the layer may be a mask that shapes it, so such a layer isn't touched.
                Sprite masked = GameSprite("popup_canvas_9", ninePatch: true);
                Sprite maskedBefore = masked;
                Check.That(!FlatPanels.TryFlatten(ref masked, null, new SimpleMaterial { OverlayEnabled = true }, Box(800f, 600f), 0) && ReferenceEquals(masked, maskedBefore),
                    "a layer with an overlay (it may be a mask) is left as it is");
            }
            finally
            {
                FlatPanels.Fill = saved;
            }
        }

        // ---- the edge of a flat panel ----------------------------------------------------------------------------------------

        private static SolidSprite Edged()
        {
            return new SolidSprite(new TaleWorlds.TwoDimension.Texture(new BackgroundChecks.FakeTexture()), 6, edged: true);
        }

        private static void Outlines()
        {
            // The settings.
            Check.Equal(2, new ModSettings().PanelOutline.SelectedIndex, "flat panels get a normal edge unless it is turned off");
            Check.Equal(FlatPanels.MaxEdgeLevel + 1, ModSettings.PanelOutlineChoices.Length, "the edge is off, subtle, normal or strong");
            ModSettings off = Flat("Navy");
            off.PanelOutline.SelectedIndex = 0;
            Check.Equal(0, ThemeSnapshot.From(off).PanelOutline, "the snapshot has no edge when it is off");
            Check.Equal(2, ThemeSnapshot.From(Flat("Navy")).PanelOutline, "...and a normal one by default");
            ModSettings wild = Flat("Navy");
            wild.PanelOutline.SelectedIndex = 99;
            Check.Equal(FlatPanels.MaxEdgeLevel, ThemeSnapshot.From(wild).PanelOutline, "an out-of-range choice is the strongest");

            // The picture it is drawn from: white with a stretchable, see-through border; one draw call, like any panel.
            SolidSprite edged = Edged();
            Check.That(edged.Edged && edged.NinePatchParameters.IsValid && edged.NinePatchParameters.LeftWidth == SolidSprite.EdgeWidth,
                "the edged fill is a nine-patch with a 2-pixel border");
            Vec2 min = edged.GetMinUvs();
            Vec2 max = edged.GetMaxUvs();
            Check.That(min.x == 0f && min.y == 0f && max.x == 1f && max.y == 1f, "...of which all is read, border included");

            SolidSprite savedFill = FlatPanels.Fill;
            var savedEdged = new SolidSprite[FlatPanels.MaxEdgeLevel + 1];
            for (int i = 1; i <= FlatPanels.MaxEdgeLevel; i++)
            {
                savedEdged[i] = FlatPanels.EdgedFill(i);
            }
            try
            {
                var fill = new SolidSprite(new TaleWorlds.TwoDimension.Texture(new BackgroundChecks.FakeTexture()), 4, edged: false);
                FlatPanels.Fill = fill;
                for (int i = 1; i <= FlatPanels.MaxEdgeLevel; i++)
                {
                    FlatPanels.SetEdgedFill(i, null);
                }
                FlatPanels.SetEdgedFill(2, edged);

                // With an edge asked for and ready, a panel is drawn with the edged fill, as a nine-patch.
                var material = new SimpleMaterial();
                Sprite panel = GameSprite("popup_canvas_9", ninePatch: true);
                Check.That(FlatPanels.TryFlatten(ref panel, null, material, Box(800f, 600f), 2) && ReferenceEquals(panel, edged),
                    "a panel with an edge asked for is drawn with the edged fill");
                Check.That(material.NinePatchParameters.IsValid && material.NinePatchParameters.LeftWidth == SolidSprite.EdgeWidth,
                    "...and the material describes its border");

                // Not asked for, or not ready, or too small: the plain fill.
                panel = GameSprite("popup_canvas_9", ninePatch: true);
                material = new SimpleMaterial { NinePatchParameters = new SpriteNinePatchParameters(10, 10, 10, 10) };
                Check.That(FlatPanels.TryFlatten(ref panel, null, material, Box(800f, 600f), 0) && ReferenceEquals(panel, fill) && !material.NinePatchParameters.IsValid,
                    "with the edge off a panel gets the plain fill, with no border");
                panel = GameSprite("popup_canvas_9", ninePatch: true);
                Check.That(FlatPanels.TryFlatten(ref panel, null, new SimpleMaterial(), Box(800f, 600f), 3) && ReferenceEquals(panel, fill),
                    "an edge that isn't ready yet means the plain fill, not a panel left textured");
                panel = GameSprite("popup_canvas_9", ninePatch: true);
                Check.That(FlatPanels.TryFlatten(ref panel, null, new SimpleMaterial(), Box(8f, 8f), 2) && ReferenceEquals(panel, fill),
                    "a panel too small for an edge gets the plain fill");
                Check.That(FlatPanels.EdgedFill(0) == null && FlatPanels.EdgedFill(FlatPanels.MaxEdgeLevel + 1) == null, "there is no edged fill outside the levels");

                // The game's own plain fills take the edge too, if they are rectangles.
                Sprite plain = GameSprite("BlankWhiteSquare_9", ninePatch: true);
                Check.That(FlatPanels.TryFlatten(ref plain, null, new SimpleMaterial(), Box(800f, 600f), 2) && ReferenceEquals(plain, edged),
                    "one of the game's square plain fills is swapped for the edged fill when it is to have an edge");
                plain = GameSprite("BlankWhiteSquare_9", ninePatch: true);
                Sprite plainBefore = plain;
                Check.That(FlatPanels.TryFlatten(ref plain, null, new SimpleMaterial(), Box(800f, 600f), 0) && ReferenceEquals(plain, plainBefore),
                    "...and is left as it is when it isn't");
                Sprite circle = GameSprite("BlankWhiteCircle", ninePatch: false);
                Sprite circleBefore = circle;
                Check.That(FlatPanels.TryFlatten(ref circle, null, new SimpleMaterial(), Box(300f, 300f), 2) && ReferenceEquals(circle, circleBefore),
                    "a round one never gets a square edge");
                plain = GameSprite("BlankWhiteSquare_9", ninePatch: true);
                plainBefore = plain;
                Check.That(FlatPanels.TryFlatten(ref plain, null, new SimpleMaterial(), Box(300f, 4f), 2) && ReferenceEquals(plain, plainBefore),
                    "...and a thin line is not turned into an edged panel");

                // Overlays stay out of it however the edge is set.
                Sprite over = GameSprite("StdAssets/Popup/canvas_gradient", ninePatch: false);
                Sprite overBefore = over;
                Check.That(!FlatPanels.TryFlatten(ref over, null, new SimpleMaterial(), Box(900f, 600f), 2) && ReferenceEquals(over, overBefore),
                    "an overlay is still never made solid when the panel has an edge");
            }
            finally
            {
                FlatPanels.Fill = savedFill;
                for (int i = 1; i <= FlatPanels.MaxEdgeLevel; i++)
                {
                    FlatPanels.SetEdgedFill(i, savedEdged[i]);
                }
            }
        }

        // ---- text stays readable on flat panels -----------------------------------------------------------------------------
        private static void Readability()
        {
            // Flat panels are exactly the chosen colour, so the readable-text estimate has to follow that rather than the
            // textured art it was built for.
            ThemeSnapshot navy = ThemeSnapshot.From(Flat("Navy"));
            Check.Near(Contrast.Luminance(Rgb("#2A3F8F")) * 1f, navy.PanelSurface, 0.05f, "a flat navy panel's surface is navy's luminance");

            ThemeSnapshot parchment = ThemeSnapshot.From(Flat("Parchment"));
            Check.That(parchment.PanelSurface > 0.5f, "a flat parchment panel is a light surface (" + parchment.PanelSurface.ToString("0.00") + ")");

            var texturedParchment = Flat("Parchment");
            texturedParchment.PanelStyle.SelectedIndex = 0;
            Check.That(ThemeSnapshot.From(texturedParchment).PanelSurface < parchment.PanelSurface,
                "...far lighter than the same colour on dark textured art");

            // Every preset, switched to flat, still gets text that meets its own contrast target.
            int checkedPresets = 0;
            foreach (ISettingsPreset preset in new ModSettings().GetBuiltInPresets())
            {
                if (preset.Name.Contains("Default"))
                {
                    continue;
                }

                var loaded = (ModSettings)preset.LoadPreset();
                loaded.PanelStyle.SelectedIndex = 1;
                ThemeSnapshot snapshot = ThemeSnapshot.From(loaded);
                Color text = snapshot.Text.Tint ? snapshot.Text.Colour : Contrast.VanillaText;
                float ratio = Contrast.MinRatio(text, snapshot.PanelSurface, snapshot.ButtonSurface);
                Check.That(ratio + 0.05f >= snapshot.ContrastTarget,
                    "'" + Translations.Plain(preset.Name) + "' made flat: text contrast " + ratio.ToString("0.0") + ":1 meets " + snapshot.ContrastTarget + ":1");
                checkedPresets++;
            }
            Check.That(checkedPresets >= 22, "all the presets were tried flat (" + checkedPresets + ")");
        }
    }
}
