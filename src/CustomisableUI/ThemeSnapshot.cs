using System;
using TaleWorlds.Library;

namespace CustomisableUI
{
    // What one UI element role (panels, frames, buttons) should look like.
    internal struct RoleStyle
    {
        public bool Tint;
        public Color Colour;
        public float Strength;
        public float Brightness;
        public float Opacity;

        public bool IsNoOp => !Tint && Brightness == 1f && Opacity == 1f;

        public static RoleStyle Resolve(int swatchIndex, string hex, float strength, float brightness, float opacity)
        {
            var style = new RoleStyle
            {
                Strength = Clamp01(strength),
                Brightness = brightness,
                Opacity = opacity,
            };
            style.Tint = Swatches.TryResolve(swatchIndex, hex, out style.Colour) && style.Strength > 0f;
            return style;
        }

        private static float Clamp01(float value)
        {
            return value < 0f ? 0f : value > 1f ? 1f : value;
        }
    }

    // What to do with red and green "status" text (losses and gains, warnings).
    internal enum StatusMode
    {
        // Leave it red and green, as the game draws it (unless a good/bad/warning colour is chosen).
        Vanilla,

        // Swap for orange and blue, which stay distinguishable with red-green colour blindness.
        ColourBlindSafe,

        // Recolour it like any other text.
        Recolour,
    }

    internal enum StatusKind
    {
        None,

        // Gains, bonuses, positive values.
        Good,

        // Losses, penalties, negative values.
        Bad,

        // The game's warning text.
        Warning,
    }

    // The colours that stand for good, bad and warning, where one has been chosen (by the player, or by the
    // colour-blind-safe palette). Used for text and for the sprites the game colours the same way.
    internal struct StatusStyle
    {
        public bool HasGood;
        public bool HasBad;
        public bool HasWarning;
        public Color Good;
        public Color Bad;
        public Color Warning;

        public bool Any => HasGood || HasBad || HasWarning;

        public bool TryGet(StatusKind kind, out Color colour)
        {
            switch (kind)
            {
                case StatusKind.Good:
                    colour = Good;
                    return HasGood;
                case StatusKind.Bad:
                    colour = Bad;
                    return HasBad;
                case StatusKind.Warning:
                    colour = Warning;
                    return HasWarning;
                default:
                    colour = default(Color);
                    return false;
            }
        }
    }

    // Colours for the battle crosshair and its markers.
    internal struct CrosshairStyle
    {
        public bool HasLine;
        public bool HasBlocked;
        public bool HasHit;
        public bool HasKill;
        public Color Line;
        public Color Blocked;
        public Color Hit;
        public Color Kill;
        public float Opacity;

        public bool Any => HasLine || HasBlocked || HasHit || HasKill || Opacity < 1f;
    }

    internal struct TextStyle
    {
        public bool Tint;
        public Color Colour;
        public float Strength;
        public StatusMode Status;

        // True when good/bad/warning text will be recoloured (see ThemeSnapshot.Statuses).
        public bool RemapsStatus;
        public float Brightness;
        public float OutlineAmount;
        public Color OutlineColour;
        public float ShadowAmount;
        public Color ShadowColour;

        public bool IsNoOp => !Tint && Brightness == 1f && OutlineAmount <= 0f && ShadowAmount <= 0f && !RemapsStatus;
    }

    // An immutable, ready-to-use reading of the settings. The draw hooks run hundreds of times a frame, so they read
    // this instead of poking at MCM objects.
    internal sealed class ThemeSnapshot
    {
        public static readonly ThemeSnapshot Disabled = new ThemeSnapshot();

        public bool Enabled;
        public bool DebugLogging;

        public RoleStyle Panel;
        public RoleStyle Frame;
        public RoleStyle Button;
        public TextStyle Text;

        // Estimated luminance (0 black .. 1 white) of themed panels and buttons; see Contrast.
        public float PanelSurface;
        public float ButtonSurface;

        // The minimum contrast ratio text is held to when "keep text readable" is on.
        public float ContrastTarget = 4.5f;

        // Colours for allies, enemies and neutrals, and where they apply.
        public RelationStyle Relations;

        // Colours for good, bad and warning, and for the battle crosshair.
        public StatusStyle Statuses;
        public CrosshairStyle Crosshair;

        // Leave anything already drawn in a clearly coloured shade alone (see Stylers.IsClearlyColoured).
        public bool KeepColours;

        // Font names as the game knows them, or null to leave the vanilla font alone.
        public string MainFont;
        public string DecorativeFont;
        public float TextScale = 1f;

        // Colour grading, in the units the UI shader uses (hue is a fraction of a full turn).
        public float Hue;
        public float Saturation;
        public float Value;
        public bool GradeEverything;

        public bool HasGrading => Hue != 0f || Saturation != 0f || Value != 0f;

        public RoleStyle StyleFor(UiRole role)
        {
            switch (role)
            {
                case UiRole.Panel: return Panel;
                case UiRole.Frame: return Frame;
                case UiRole.Button: return Button;
                default: return default(RoleStyle);
            }
        }

        // The vanilla font a given vanilla font name should be swapped for, or null.
        public string ReplacementFor(string vanillaFontName)
        {
            if (string.IsNullOrEmpty(vanillaFontName))
            {
                return null;
            }
            if (vanillaFontName.StartsWith("FiraSansExtraCondensed", StringComparison.Ordinal))
            {
                return MainFont;
            }
            if (vanillaFontName == "Galahad")
            {
                return DecorativeFont;
            }
            return null;
        }

        public static ThemeSnapshot From(ModSettings s)
        {
            if (!s.Enabled)
            {
                return Disabled;
            }

            var snapshot = new ThemeSnapshot
            {
                Enabled = true,
                DebugLogging = s.DebugLogging,
                KeepColours = s.KeepOtherModColours,
                Panel = RoleStyle.Resolve(s.PanelColour.SelectedIndex, s.PanelHex, s.PanelStrength, s.PanelBrightness, s.PanelOpacity),
                Frame = RoleStyle.Resolve(s.FrameColour.SelectedIndex, s.FrameHex, s.FrameStrength, s.FrameBrightness, 1f),
                Button = RoleStyle.Resolve(s.ButtonColour.SelectedIndex, s.ButtonHex, s.ButtonStrength, s.ButtonBrightness, 1f),
                MainFont = FontName(s.MainFont.SelectedIndex),
                DecorativeFont = FontName(s.DecorativeFont.SelectedIndex),
                TextScale = s.TextScale,
                Hue = s.HueShift / 360f,
                Saturation = s.Saturation / 100f,
                Value = s.Brightness / 100f,
                GradeEverything = s.GradeEverything,
            };

            snapshot.PanelSurface = Contrast.PanelSurface(snapshot.Panel);
            snapshot.ButtonSurface = Contrast.ButtonSurface(snapshot.Button);
            snapshot.ContrastTarget = Contrast.TargetFor(s.ContrastTarget.SelectedIndex);

            snapshot.Relations = new RelationStyle
            {
                HasAlly = Swatches.TryResolve(s.AllyColour.SelectedIndex, s.AllyHex, out Color ally),
                HasEnemy = Swatches.TryResolve(s.EnemyColour.SelectedIndex, s.EnemyHex, out Color enemy),
                HasNeutral = Swatches.TryResolve(s.NeutralColour.SelectedIndex, s.NeutralHex, out Color neutral),
                Ally = ally,
                Enemy = enemy,
                Neutral = neutral,
                Bars = s.RelationBars,
                Tooltips = s.RelationTooltips,
                Battle = s.RelationBattle,
                Names = s.RelationNameplates,
            };

            snapshot.Crosshair = new CrosshairStyle
            {
                HasLine = Swatches.TryResolve(s.CrosshairColour.SelectedIndex, s.CrosshairHex, out Color line),
                HasBlocked = Swatches.TryResolve(s.BlockedColour.SelectedIndex, s.BlockedHex, out Color blocked),
                HasHit = Swatches.TryResolve(s.HitMarkerColour.SelectedIndex, s.HitMarkerHex, out Color hit),
                HasKill = Swatches.TryResolve(s.KillMarkerColour.SelectedIndex, s.KillMarkerHex, out Color kill),
                Line = line,
                Blocked = blocked,
                Hit = hit,
                Kill = kill,
                Opacity = Math.Max(0f, Math.Min(1f, s.CrosshairOpacity)),
            };

            var text = new TextStyle
            {
                Strength = Math.Max(0f, Math.Min(1f, s.TextStrength)),
                Status = s.StatusColours.SelectedIndex == 1 ? StatusMode.ColourBlindSafe
                    : s.StatusColours.SelectedIndex == 2 ? StatusMode.Recolour
                    : StatusMode.Vanilla,
                Brightness = s.TextBrightness,
                OutlineAmount = s.OutlineThickness,
                ShadowAmount = Math.Max(0f, Math.Min(1f, s.ShadowStrength)),
            };
            ResolveTextColour(s, snapshot, ref text);
            ResolveStatusColours(s, snapshot, ref text);
            if (text.OutlineAmount > 0f && !Swatches.TryResolve(s.OutlineColour.SelectedIndex, s.OutlineHex, out text.OutlineColour))
            {
                // "Vanilla" outline colour with a thickness set: fall back to black, the usual readable choice.
                text.OutlineColour = new Color(0f, 0f, 0f, 1f);
            }
            if (text.ShadowAmount > 0f && !Swatches.TryResolve(s.ShadowColour.SelectedIndex, s.ShadowHex, out text.ShadowColour))
            {
                text.ShadowColour = new Color(0f, 0f, 0f, 1f);
            }
            snapshot.Text = text;

            return snapshot;
        }

        // Picks the text colour. "Auto" chooses light or dark for the panel and button colours. With "keep text
        // readable" on, a chosen colour (or the vanilla one) that would have poor contrast against those colours is
        // swapped for light or dark as well, so a light panel never ends up with pale text, nor a dark one with black.
        private static void ResolveTextColour(ModSettings s, ThemeSnapshot snapshot, ref TextStyle text)
        {
            Color best = Contrast.Best(snapshot.PanelSurface, snapshot.ButtonSurface);
            Swatches.TextChoice choice = Swatches.ResolveText(s.TextColour.SelectedIndex, s.TextHex, out Color picked);

            switch (choice)
            {
                case Swatches.TextChoice.Auto:
                    text.Tint = text.Strength > 0f;
                    text.Colour = best;
                    break;

                case Swatches.TextChoice.Colour:
                    text.Tint = text.Strength > 0f;
                    text.Colour = s.KeepTextReadable && Contrast.MinRatio(picked, snapshot.PanelSurface, snapshot.ButtonSurface) < snapshot.ContrastTarget
                        ? best
                        : picked;
                    break;

                default:
                    // Vanilla text is left alone unless the theme has made it hard to read.
                    if (s.KeepTextReadable &&
                        Contrast.MinRatio(Contrast.VanillaText, snapshot.PanelSurface, snapshot.ButtonSurface) < snapshot.ContrastTarget)
                    {
                        text.Tint = true;
                        text.Strength = 1f;
                        text.Colour = best;
                    }
                    break;
            }
        }

        // Colour-blind-safe replacements for red and green text, from the Okabe-Ito palette (designed to be
        // distinguishable under all common forms of colour blindness): vermillion/orange for "bad" and blue for
        // "good". Light variants for dark panels, deeper variants for light ones.
        private static readonly Color SafeNegativeLight = new Color(0.90f, 0.62f, 0.00f, 1f);
        private static readonly Color SafePositiveLight = new Color(0.34f, 0.71f, 0.91f, 1f);
        private static readonly Color SafeNegativeDark = new Color(0.69f, 0.27f, 0.00f, 1f);
        private static readonly Color SafePositiveDark = new Color(0.00f, 0.38f, 0.62f, 1f);

        // Builds the good / bad / warning palette: the colour-blind-safe pair if that is selected, then any colour the
        // player picked explicitly on top. "Recolour like other text" turns the palette off, so status text is simply
        // treated as ordinary text.
        private static void ResolveStatusColours(ModSettings s, ThemeSnapshot snapshot, ref TextStyle text)
        {
            var palette = new StatusStyle();

            if (text.Status != StatusMode.Recolour)
            {
                if (text.Status == StatusMode.ColourBlindSafe)
                {
                    // Dark text means the surfaces are light, so the deeper variants are the readable ones.
                    bool lightSurfaces = Contrast.Luminance(Contrast.Best(snapshot.PanelSurface, snapshot.ButtonSurface)) < 0.5f;
                    palette.HasGood = palette.HasBad = true;
                    palette.Good = lightSurfaces ? SafePositiveDark : SafePositiveLight;
                    palette.Bad = lightSurfaces ? SafeNegativeDark : SafeNegativeLight;
                }

                if (Swatches.TryResolve(s.GoodColour.SelectedIndex, s.GoodHex, out Color good))
                {
                    palette.HasGood = true;
                    palette.Good = good;
                }
                if (Swatches.TryResolve(s.BadColour.SelectedIndex, s.BadHex, out Color bad))
                {
                    palette.HasBad = true;
                    palette.Bad = bad;
                }
                if (Swatches.TryResolve(s.WarningColour.SelectedIndex, s.WarningHex, out Color warning))
                {
                    palette.HasWarning = true;
                    palette.Warning = warning;
                }
            }

            snapshot.Statuses = palette;
            text.RemapsStatus = palette.Any;
        }

        // The dropdown lists "Vanilla" first and then the fonts in ModSettings.FontChoices order.
        private static string FontName(int index)
        {
            switch (index)
            {
                case 1: return "FiraSansExtraCondensed-Light";
                case 2: return "FiraSansExtraCondensed-Regular";
                case 3: return "FiraSansExtraCondensed-Medium";
                case 4: return "Galahad";
                default: return null;
            }
        }
    }
}
