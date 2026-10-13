using System;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

namespace CustomisableUI
{
    // Applies a ThemeSnapshot to the materials the UI is about to draw with.
    internal static class Stylers
    {
        // Sprites drawn as a flat fill of their layer colour. Multiplying one of these by a tint would just darken it,
        // so for them the tint replaces the colour (keeping the layer's own transparency).
        internal static bool IsFlatFill(string spriteName)
        {
            return spriteName != null && spriteName.StartsWith("BlankWhite", StringComparison.OrdinalIgnoreCase);
        }

        // The colours in the swatch list are mid-tones meant to be multiplied with dark artwork. Used as the colour of
        // a flat fill they come out far brighter than the dark UI around them (a forest-green tooltip turns neon), so
        // flat fills use a toned-down version of the chosen colour...
        internal const float FlatFillDarkening = 0.6f;

        // ...and only feel a fraction of the brightness slider, because the slider mainly exists to compensate for
        // dark artwork, which a flat fill doesn't have.
        internal const float FlatBrightnessInfluence = 0.3f;

        internal static float FlatBrightness(RoleStyle style)
        {
            return 1f + (style.Brightness - 1f) * FlatBrightnessInfluence;
        }

        // What a flat fill that currently has colour `current` becomes under `style`'s tint.
        internal static Color FlatFill(Color current, RoleStyle style)
        {
            Color target = new Color(
                style.Colour.Red * FlatFillDarkening,
                style.Colour.Green * FlatFillDarkening,
                style.Colour.Blue * FlatFillDarkening,
                1f);
            return Blend(current, target, style.Strength);
        }

        public static void ApplyToSprite(ThemeSnapshot theme, SimpleMaterial material, UiRole role, string spriteName)
        {
            if (role == UiRole.Other && !theme.GradeEverything)
            {
                return;
            }

            if (role != UiRole.Other)
            {
                RoleStyle style = theme.StyleFor(role);
                bool flat = IsFlatFill(spriteName);
                if (style.Tint)
                {
                    Color current = material.Color;
                    material.Color = flat
                        ? FlatFill(current, style)
                        : Multiply(current, style.Colour, style.Strength);
                }
                float brightness = flat ? FlatBrightness(style) : style.Brightness;
                if (brightness != 1f)
                {
                    material.ColorFactor *= brightness;
                }
                if (style.Opacity != 1f)
                {
                    material.AlphaFactor *= style.Opacity;
                }
            }

            ApplyGrading(theme, material);
        }

        private static void ApplyGrading(ThemeSnapshot theme, SimpleMaterial material)
        {
            if (theme.HasGrading)
            {
                material.HueFactor += theme.Hue;
                material.SaturationFactor = Clamp(material.SaturationFactor + theme.Saturation, -1f, 1f);
                material.ValueFactor = Clamp(material.ValueFactor + theme.Value, -1f, 1f);
            }
        }

        // ---- flat panels (see FlatPanels) ------------------------------------------------------------------------

        // The colour a panel starts from before the chosen colour is blended in (what "0% strength" means for a flat fill).
        private const float FlatPanelBase = 0.08f;

        // How far a nested panel is moved toward white (or black, for a pale panel) per level, at full "flat panel depth".
        internal const float DepthStep = 0.10f;

        // Colours at or above this relative luminance count as pale: nested panels go darker rather than lighter.
        private const float PaleSurface = 0.35f;

        // Panels this fraction of the screen or more are windows; smaller ones sit inside a window (level 1), and the
        // smallest (rows, slots) inside those (level 2). Size is the only thing the draw hook can see of nesting.
        internal const float WindowFraction = 0.20f;
        internal const float InnerFraction = 0.04f;

        internal static int DepthLevel(float screenFraction)
        {
            return screenFraction >= WindowFraction ? 0 : screenFraction >= InnerFraction ? 1 : 2;
        }

        // The colour a flat window is before anything adjusts it: the chosen colour exactly, at full strength. Lower
        // strengths fade it toward a neutral dark.
        internal static Color FlatPanelStart(RoleStyle style)
        {
            return Blend(new Color(FlatPanelBase, FlatPanelBase, FlatPanelBase, 1f), style.Colour, style.Strength);
        }

        // The colour of a flat panel at a nesting level (0 = a window), given the colour of a window.
        internal static Color FlatPanelColour(Color window, int level, float depth)
        {
            if (level <= 0 || depth <= 0f)
            {
                return window;
            }

            float toward = Contrast.Luminance(window) >= PaleSurface ? 0f : 1f;
            return Blend(window, new Color(toward, toward, toward, 1f), Math.Min(1f, DepthStep * level * depth));
        }

        // Styles a panel that is being drawn as a plain fill. `screenFraction` is how much of the screen it covers.
        public static void ApplyToFlatPanel(ThemeSnapshot theme, SimpleMaterial material, float screenFraction)
        {
            RoleStyle style = theme.Panel;
            Color fill = FlatPanelColour(theme.PanelFill, DepthLevel(screenFraction), theme.PanelDepth);

            // Keeps the layer's own transparency, as every other styler does.
            material.Color = new Color(fill.Red, fill.Green, fill.Blue, material.Color.Alpha);

            float brightness = FlatBrightness(style);
            if (brightness != 1f)
            {
                material.ColorFactor *= brightness;
            }
            if (style.Opacity != 1f)
            {
                material.AlphaFactor *= style.Opacity;
            }
            ApplyGrading(theme, material);
        }

        // brushName and styleName identify the text being drawn where the game says (plain text drawn through a
        // brush); rich text passes null for both.
        public static void ApplyToText(ThemeSnapshot theme, TextMaterial material, string brushName, string styleName)
        {
            // Text the game colours by relation (ally / enemy / neutral) takes the chosen relation colour, and is
            // then left out of the general text recolouring so that colour isn't undone.
            bool byRelation = RelationStyler.TryApplyToText(theme, material, brushName, styleName);

            TextStyle style = theme.Text;
            if (style.IsNoOp)
            {
                return;
            }

            // Text the game marks as good, bad or warning (by style name, or by being strongly green or red) is left
            // alone, or recoloured with the player's good / bad / warning colours or the colour-blind-safe pair. With
            // "recolour like other text" it is treated as ordinary text instead.
            Color current = material.Color;
            StatusKind status = byRelation || style.Status == StatusMode.Recolour
                ? StatusKind.None
                : ClassifyStatus(styleName, current, theme.Statuses);
            if (byRelation)
            {
                // Keep the relation colour exactly as chosen.
            }
            else if (status != StatusKind.None)
            {
                if (theme.Statuses.TryGet(status, out Color replacement))
                {
                    material.Color = new Color(replacement.Red, replacement.Green, replacement.Blue, current.Alpha);
                }
            }
            else if (theme.KeepColours && IsClearlyColoured(current))
            {
                // Coloured text another mod has set (rarity or quality, say). Left as it is.
            }
            else if (style.Tint)
            {
                material.Color = Blend(current, style.Colour, style.Strength);
            }

            if (style.Brightness != 1f)
            {
                material.ColorFactor *= style.Brightness;
            }
            if (style.OutlineAmount > 0f)
            {
                material.OutlineAmount = Math.Max(material.OutlineAmount, style.OutlineAmount);
                material.OutlineColor = style.OutlineColour;
            }
            if (style.ShadowAmount > 0f)
            {
                // The text shader's "glow" is a blurred copy of the glyphs drawn behind them, nudged by ShadowOffset
                // in the direction of ShadowAngle. Blur widens it into a soft halo; the offset makes it read as a
                // drop shadow rather than a glow.
                material.GlowColor = new Color(style.ShadowColour.Red, style.ShadowColour.Green, style.ShadowColour.Blue, 1f);
                material.Blur = Math.Max(material.Blur, style.ShadowAmount);
                material.ShadowOffset = Math.Max(material.ShadowOffset, 0.35f * style.ShadowAmount);
                material.ShadowAngle = 90f;
            }
        }

        // Moves the colour toward the target by `strength`, keeping the original transparency.
        internal static Color Blend(Color from, Color to, float strength)
        {
            return new Color(
                from.Red + (to.Red - from.Red) * strength,
                from.Green + (to.Green - from.Green) * strength,
                from.Blue + (to.Blue - from.Blue) * strength,
                from.Alpha);
        }

        // Multiplies by the tint, eased by `strength` (0 = unchanged, 1 = full tint).
        private static Color Multiply(Color from, Color tint, float strength)
        {
            return new Color(
                from.Red * (1f + (tint.Red - 1f) * strength),
                from.Green * (1f + (tint.Green - 1f) * strength),
                from.Blue * (1f + (tint.Blue - 1f) * strength),
                from.Alpha);
        }

        // "Clearly coloured": a real hue with some brightness. Black, white, greys and the muted creams and browns the
        // base UI is mostly made of fall below it; the blues, greens, golds and reds a mod would use to mark quality or
        // rarity are above it.
        private const float ColouredMinSaturation = 0.40f;
        private const float ColouredMinValue = 0.10f;

        internal static bool IsClearlyColoured(Color c)
        {
            if (c.Alpha < 0.05f)
            {
                return false;
            }

            float max = Math.Max(c.Red, Math.Max(c.Green, c.Blue));
            float min = Math.Min(c.Red, Math.Min(c.Green, c.Blue));
            return max >= ColouredMinValue && (max - min) / max >= ColouredMinSaturation;
        }

        // The game marks good, bad and warning text with named brush styles. These are the names it uses (found in the
        // base game's brush files). "Increasing" and "Decreasing" are deliberately absent: they describe direction, not
        // whether the change is good.
        internal static StatusKind KindOfStyle(string styleName)
        {
            switch (styleName)
            {
                case "Positive":
                case "Bonus":
                case "Good":
                    return StatusKind.Good;
                case "Negative":
                case "Penalty":
                case "Bad":
                    return StatusKind.Bad;
                case "Warning":
                    return StatusKind.Warning;
                default:
                    return StatusKind.None;
            }
        }

        // Text counts as good/bad/warning if the game says so by style name, otherwise if it is strongly green or red.
        // A warning style only counts as one if a warning colour is set; without it, warning text (usually red) is
        // treated as the red it is.
        internal static StatusKind ClassifyStatus(string styleName, Color c, StatusStyle palette)
        {
            StatusKind named = KindOfStyle(styleName);
            if (named != StatusKind.None && (named != StatusKind.Warning || palette.HasWarning))
            {
                return named;
            }
            return ClassifyHue(c);
        }

        internal static StatusKind ClassifyHue(Color c)
        {
            float max = Math.Max(c.Red, Math.Max(c.Green, c.Blue));
            float min = Math.Min(c.Red, Math.Min(c.Green, c.Blue));
            float delta = max - min;
            if (max < 0.35f || delta / max < 0.55f)
            {
                return StatusKind.None;
            }

            float hue;
            if (max == c.Red)
            {
                hue = 60f * (((c.Green - c.Blue) / delta + 6f) % 6f);
            }
            else if (max == c.Green)
            {
                hue = 60f * ((c.Blue - c.Red) / delta + 2f);
            }
            else
            {
                hue = 60f * ((c.Red - c.Green) / delta + 4f);
            }

            if (hue < 20f || hue > 340f)
            {
                return StatusKind.Bad;
            }
            return hue > 80f && hue < 160f ? StatusKind.Good : StatusKind.None;
        }

        private static float Clamp(float value, float min, float max)
        {
            return value < min ? min : value > max ? max : value;
        }
    }
}
