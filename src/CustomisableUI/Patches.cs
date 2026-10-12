using System;
using System.Collections.Generic;
using CustomisableUI.Backgrounds;
using HarmonyLib;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.TwoDimension;

namespace CustomisableUI
{
    // Gauntlet's own draw path, hooked at the last moment before something is drawn:
    //   * BrushRenderer.Render        remembers which brush is being drawn, so DrawSprite can see it.
    //   * TwoDimensionDrawContext.DrawSprite   recolours panels, frames and buttons.
    //   * BrushRenderer / Style .CreateTextMaterial   recolours text (plain and rich text respectively).
    //   * FontFactory.GetMappedFontForLocalization    swaps fonts.
    //   * Text.FontSize setter        scales text.
    // The custom backgrounds feature has its own hooks, in Backgrounds\BackgroundPatches.cs.
    // Each hook is wrapped so that a failure is logged once and can never take the UI down with it.

    internal static class PatchGuard
    {
        private static int _failures;

        // After this many failures something is badly wrong; stop touching the UI and let vanilla run.
        public static bool Tripped => _failures >= 25;

        public static void Report(string hook, Exception ex)
        {
            _failures++;
            Log.Error(hook, ex);
        }
    }

    internal static class UiContext
    {
        // True while BrushRenderer.Render is drawing a brush's layers, or Widget.OnRender is drawing the sprite
        // attached directly to a widget (<Widget Sprite="..." Color="..."/> in a prefab).
        //
        // The game reuses draw materials from a pool without resetting them, so a change made to a material is
        // only safe if whoever drew with it re-sets every field this mod adjusts (colour, colour factor, alpha, hue,
        // saturation, value) on the next use; otherwise the change would be left behind on the pooled object and
        // compound frame after frame. These two paths both re-set all of them. Other sprite draw paths do not, so
        // sprites are only styled from inside one of these two.
        [ThreadStatic]
        public static bool InBrushRender;

        [ThreadStatic]
        public static bool InWidgetRender;

        [ThreadStatic]
        public static string CurrentBrush;

        // The brush's current style ("Default", "Hovered", "Invalid", "Positive"...): the state it is being drawn in.
        [ThreadStatic]
        public static string CurrentStyle;
    }

    // Lists every distinct (brush, sprite) pair with the role it was given. Only active with "Write a debug log" on.
    internal static class DebugRecorder
    {
        private static readonly HashSet<string> Seen = new HashSet<string>();

        // A free-form line, written once however often it is reported.
        public static void Note(string message)
        {
            lock (Seen)
            {
                if (!Seen.Add(message))
                {
                    return;
                }
            }
            Log.Info(message);
        }

        // Each (path, brush, sprite, role) is written once, with the size and alpha of the first time it was seen.
        public static void Record(string path, string brush, string sprite, UiRole role, float width, float height, float alpha)
        {
            string line = path + ": brush='" + brush + "' sprite='" + sprite + "' -> " + role;
            lock (Seen)
            {
                if (!Seen.Add(line))
                {
                    return;
                }
            }
            Log.Info(line + "  size=" + (int)width + "x" + (int)height + " alpha=" + alpha.ToString("0.00"));
        }
    }

    [HarmonyPatch(typeof(BrushRenderer), nameof(BrushRenderer.Render))]
    internal static class BrushRendererRenderPatch
    {
        private static void Prefix(BrushRenderer __instance)
        {
            UiContext.InBrushRender = true;
            UiContext.CurrentBrush = __instance.Brush?.Name;
            UiContext.CurrentStyle = __instance.CurrentStyle?.Name;
        }

        // A finalizer rather than a postfix, so the flag is cleared even if drawing throws.
        private static void Finalizer()
        {
            UiContext.InBrushRender = false;
            UiContext.CurrentBrush = null;
            UiContext.CurrentStyle = null;
        }
    }

    // The sprite a widget draws for itself. Prefabs attach sprites directly to plain widgets all over the UI (big
    // panel backgrounds, headers, dividers), and those never go through a brush.
    [HarmonyPatch(typeof(Widget), "OnRender")]
    internal static class WidgetRenderPatch
    {
        private static void Prefix()
        {
            UiContext.InWidgetRender = true;
        }

        private static void Finalizer()
        {
            UiContext.InWidgetRender = false;
        }
    }

    [HarmonyPatch(typeof(TwoDimensionDrawContext), nameof(TwoDimensionDrawContext.DrawSprite))]
    internal static class DrawSpritePatch
    {
        // Returns false only to skip the draw, which is how a custom background goes away when it is switched off.
        private static bool Prefix(ref Sprite sprite, SimpleMaterial material, ref Rectangle2D rectangle)
        {
            bool viaBrush = UiContext.InBrushRender;
            if (!(viaBrush || UiContext.InWidgetRender) || PatchGuard.Tripped || sprite == null || material == null)
            {
                return true;
            }

            try
            {
                ThemeSnapshot theme = ThemeState.Current;

                // [Backgrounds] The player's own pictures, and the game's loading pictures they stand in for, are
                // handled by the custom backgrounds feature (see Backgrounds\README.md) and are never themed.
                if (BackgroundDraw.Concerns(sprite))
                {
                    return BackgroundDraw.Prepare(theme, ref sprite, ref rectangle);
                }

                if (!theme.Enabled)
                {
                    return true;
                }

                string brush = viaBrush ? UiContext.CurrentBrush : null;

                // Sprites the game colours by relation (settlement bars, battle markers) have their own colours.
                if (viaBrush && RelationStyler.TryApplyToSprite(theme, material, brush))
                {
                    return true;
                }

                // So do the crosshair and the slider fills that show a gain or a loss.
                if (viaBrush && SemanticStyler.TryApplyToSprite(theme, material, brush, UiContext.CurrentStyle))
                {
                    return true;
                }

                UiRole role = RoleClassifier.Classify(brush, sprite.Name);

                // The bounding box costs a little, so it is only worked out when a decision needs it.
                SimpleRectangle box = default(SimpleRectangle);
                bool haveBox = false;
                float alpha = material.Color.Alpha * material.AlphaFactor;

                if (!viaBrush && role == UiRole.Other && RoleClassifier.IsLargeBackgroundCandidate(sprite))
                {
                    box = rectangle.GetBoundingBox();
                    haveBox = true;
                    if (RoleClassifier.IsLargeBackground(sprite, box))
                    {
                        role = UiRole.Panel;
                    }
                }

                if (role != UiRole.Other)
                {
                    if (!haveBox)
                    {
                        box = rectangle.GetBoundingBox();
                        haveBox = true;
                    }

                    // Dimming layers (pause, autosave...) keep the game's own look.
                    if (RoleClassifier.IsFullScreenDim(box, alpha))
                    {
                        role = UiRole.Other;
                    }
                }

                // Something already drawn in a clearly coloured shade (item rows coloured by quality, say) is left alone
                // when asked: theming it would flatten a colour that another mod is using to carry meaning.
                bool keptColour = theme.KeepColours && role != UiRole.Other && Stylers.IsClearlyColoured(material.Color);

                if (theme.DebugLogging)
                {
                    if (!haveBox)
                    {
                        box = rectangle.GetBoundingBox();
                    }
                    DebugRecorder.Record((viaBrush ? "brush" : "widget") + (keptColour ? " (kept: coloured)" : string.Empty),
                        brush, sprite.Name, role, box.Width, box.Height, alpha);
                }

                if (!keptColour)
                {
                    Stylers.ApplyToSprite(theme, material, role, sprite.Name);
                }
            }
            catch (Exception ex)
            {
                PatchGuard.Report("DrawSprite", ex);
            }

            return true;
        }
    }

    // Text drawn by TextWidget.
    [HarmonyPatch(typeof(BrushRenderer), nameof(BrushRenderer.CreateTextMaterial))]
    internal static class BrushTextMaterialPatch
    {
        private static void Postfix(BrushRenderer __instance, TextMaterial __result)
        {
            StyleText(__result, __instance.Brush?.Name, __instance.CurrentStyle?.Name);
        }

        internal static void StyleText(TextMaterial material, string brushName, string styleName)
        {
            if (PatchGuard.Tripped || material == null)
            {
                return;
            }

            try
            {
                ThemeSnapshot theme = ThemeState.Current;
                if (theme.Enabled)
                {
                    Stylers.ApplyToText(theme, material, brushName, styleName);
                }
            }
            catch (Exception ex)
            {
                PatchGuard.Report("CreateTextMaterial", ex);
            }
        }
    }

    // Text drawn by RichTextWidget (conversations, tooltips, the encyclopedia...).
    [HarmonyPatch(typeof(Style), nameof(Style.CreateTextMaterial))]
    internal static class StyleTextMaterialPatch
    {
        private static void Postfix(TextMaterial __result)
        {
            // Rich text carries no brush, so there is no brush or style name to go on.
            BrushTextMaterialPatch.StyleText(__result, null, null);
        }
    }

    [HarmonyPatch(typeof(FontFactory), nameof(FontFactory.GetMappedFontForLocalization))]
    internal static class FontMappingPatch
    {
        private static void Postfix(FontFactory __instance, string englishFontName, ref Font __result)
        {
            if (PatchGuard.Tripped)
            {
                return;
            }

            try
            {
                ThemeSnapshot theme = ThemeState.Current;
                if (!theme.Enabled)
                {
                    return;
                }

                string replacement = theme.ReplacementFor(englishFontName);
                if (replacement == null || replacement == englishFontName)
                {
                    return;
                }

                // Other languages map the Latin fonts onto CJK ones. The Latin replacements have no glyphs for those
                // scripts, so only swap when the game is using the font it was asked for.
                if (!ReferenceEquals(__result, __instance.GetFont(englishFontName)))
                {
                    return;
                }

                Font font = __instance.GetFont(replacement);
                if (font != null)
                {
                    __result = font;
                }
            }
            catch (Exception ex)
            {
                PatchGuard.Report("GetMappedFontForLocalization", ex);
            }
        }
    }

    [HarmonyPatch(typeof(Text), nameof(Text.FontSize), MethodType.Setter)]
    internal static class TextSizePatch
    {
        private static void Prefix(ref float value)
        {
            if (PatchGuard.Tripped)
            {
                return;
            }

            try
            {
                ThemeSnapshot theme = ThemeState.Current;
                if (theme.Enabled && theme.TextScale != 1f)
                {
                    value *= theme.TextScale;
                }
            }
            catch (Exception ex)
            {
                PatchGuard.Report("Text.FontSize", ex);
            }
        }
    }
}
