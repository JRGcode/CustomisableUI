using System;
using TaleWorlds.Library;

namespace CustomisableUI
{
    // Decides whether text should be light or dark for the panel and button colours in use, using the WCAG
    // relative-luminance contrast ratio.
    //
    // What a themed surface will really look like depends on each sprite's artwork, which the mod can't see, so the
    // surfaces here are estimates built from two cases that bracket it:
    //   * flat fills (most large backgrounds) become the chosen colour, toned down and lightly brightened; and
    //   * textured art (headers, buttons, frames) multiplies the chosen colour with dark vanilla artwork.
    // The estimate only has to land on the right side of "is this surface light or dark", which it does comfortably for
    // everything except mid-tones, where either text colour is a reasonable choice.
    internal static class Contrast
    {
        public static readonly Color LightText = new Color(0.96f, 0.92f, 0.83f, 1f);
        public static readonly Color DarkText = new Color(0.07f, 0.07f, 0.07f, 1f);

        // Roughly the colour of the game's default text. Used to judge whether vanilla text survives a lighter theme.
        public static readonly Color VanillaText = new Color(0.96f, 0.88f, 0.77f, 1f);

        // The contrast ratios the player can ask for: WCAG large-text minimum, AA for normal text, and AAA.
        public static readonly float[] Targets = { 3f, 4.5f, 7f };

        // The ratio for a selection from the contrast dropdown (0 = 3:1, 1 = 4.5:1, 2 = 7:1).
        public static float TargetFor(int index)
        {
            return Targets[index < 0 ? 0 : index >= Targets.Length ? Targets.Length - 1 : index];
        }

        // Luminance of an unthemed panel or button: the base game's UI is dark.
        private const float VanillaSurface = 0.04f;

        // Typical encoded brightness (0..1) of the vanilla chrome artwork a tint is multiplied with.
        private const float ArtworkLevel = 0.30f;

        public static float PanelSurface(RoleStyle panel)
        {
            float flat = panel.Tint
                ? Luminance(Scale(Stylers.FlatFill(new Color(0f, 0f, 0f, 1f), panel), Stylers.FlatBrightness(panel)))
                : 0f;
            return Math.Max(flat, TexturedSurface(panel));
        }

        public static float ButtonSurface(RoleStyle button)
        {
            return TexturedSurface(button);
        }

        // The better of light and dark text: the one whose worst contrast across the given surfaces is highest.
        public static Color Best(float panelSurface, float buttonSurface)
        {
            return MinRatio(LightText, panelSurface, buttonSurface) >= MinRatio(DarkText, panelSurface, buttonSurface)
                ? LightText
                : DarkText;
        }

        public static float MinRatio(Color text, float panelSurface, float buttonSurface)
        {
            float l = Luminance(text);
            return Math.Min(Ratio(l, panelSurface), Ratio(l, buttonSurface));
        }

        public static float Ratio(float a, float b)
        {
            float hi = Math.Max(a, b);
            float lo = Math.Min(a, b);
            return (hi + 0.05f) / (lo + 0.05f);
        }

        // WCAG relative luminance of an sRGB colour (components 0..1; values outside that range are clamped).
        public static float Luminance(Color c)
        {
            return 0.2126f * Linear(c.Red) + 0.7152f * Linear(c.Green) + 0.0722f * Linear(c.Blue);
        }

        private static float TexturedSurface(RoleStyle style)
        {
            if (!style.Tint)
            {
                return Math.Min(1f, VanillaSurface * style.Brightness);
            }

            float level = ArtworkLevel * style.Brightness;
            return Luminance(new Color(
                level * (1f + (style.Colour.Red - 1f) * style.Strength),
                level * (1f + (style.Colour.Green - 1f) * style.Strength),
                level * (1f + (style.Colour.Blue - 1f) * style.Strength),
                1f));
        }

        private static Color Scale(Color c, float factor)
        {
            return new Color(c.Red * factor, c.Green * factor, c.Blue * factor, c.Alpha);
        }

        private static float Linear(float v)
        {
            v = v < 0f ? 0f : v > 1f ? 1f : v;
            return v <= 0.04045f ? v / 12.92f : (float)Math.Pow((v + 0.055f) / 1.055f, 2.4);
        }
    }
}
