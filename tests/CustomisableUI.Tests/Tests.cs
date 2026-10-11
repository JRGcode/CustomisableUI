using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using HarmonyLib;
using MCM.Abstractions;
using MCM.Abstractions.Attributes;
using MCM.Common;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

namespace CustomisableUI.Tests
{
    internal static class Tests
    {
        // ---- helpers -----------------------------------------------------------------------------------------------

        private static Color Rgb(string hex, float alpha = 1f)
        {
            Swatches.TryParseHex(hex, out Color c);
            return new Color(c.Red, c.Green, c.Blue, alpha);
        }

        private static void Pick(Dropdown<string> dropdown, string swatch)
        {
            dropdown.SelectedIndex = Swatches.IndexOf(swatch);
        }

        private static bool Same(Color a, Color b, float tolerance = 0.01f)
        {
            return Math.Abs(a.Red - b.Red) <= tolerance && Math.Abs(a.Green - b.Green) <= tolerance &&
                   Math.Abs(a.Blue - b.Blue) <= tolerance && Math.Abs(a.Alpha - b.Alpha) <= tolerance;
        }

        private static string Hex(Color c)
        {
            return Swatches.ToHex(c);
        }

        private static ThemeSnapshot Theme(ModSettings s)
        {
            return ThemeSnapshot.From(s);
        }

        private static int Count(Dictionary<UiRole, int> counts, UiRole role)
        {
            return counts.TryGetValue(role, out int n) ? n : 0;
        }

        private static TextMaterial TextOf(Color colour)
        {
            return new TextMaterial { Color = colour };
        }

        // ---- swatches ----------------------------------------------------------------------------------------------

        public static void SwatchesChecks()
        {
            Check.Section("Colour swatches and hex");

            Check.That(CustomisableUI.Swatches.TryParseHex("#2A3F8F", out Color c) && Same(c, new Color(42 / 255f, 63 / 255f, 143 / 255f, 1f)), "#RRGGBB parses");
            Check.That(CustomisableUI.Swatches.TryParseHex("2A3F8F", out _), "RRGGBB without # parses");
            Check.That(CustomisableUI.Swatches.TryParseHex("#FFF", out Color white) && Same(white, new Color(1f, 1f, 1f, 1f)), "#RGB shorthand parses");
            Check.That(!CustomisableUI.Swatches.TryParseHex("#12", out _), "a half-typed code is rejected");
            Check.That(!CustomisableUI.Swatches.TryParseHex("not a colour", out _), "text is rejected");
            Check.That(!CustomisableUI.Swatches.TryParseHex(null, out _), "null is rejected");

            Color sky = Rgb("#5AA8E8");
            Check.Equal("#5AA8E8FF", CustomisableUI.Swatches.ToHex(sky), "ToHex writes the game's #RRGGBBFF form");
            Check.That(Same(Color.ConvertStringToColor(CustomisableUI.Swatches.ToHex(sky)), sky), "ToHex round-trips through the game's own parser");

            Check.Equal(0, CustomisableUI.Swatches.IndexOf("no such swatch"), "an unknown name falls back to Vanilla");
            Check.That(CustomisableUI.Swatches.IndexOf("Black") > 0, "named swatches have non-zero indexes");
            Check.Equal(1, CustomisableUI.Swatches.TextIndexOf(CustomisableUI.Swatches.Auto), "the text list has Auto in second place");

            Check.That(!CustomisableUI.Swatches.TryResolve(0, "#112233", out _), "index 0 means vanilla: nothing to apply");
            int custom = CustomisableUI.Swatches.AllNames().Count() - 1;
            Check.That(CustomisableUI.Swatches.TryResolve(custom, "#112233", out Color picked) && Same(picked, Rgb("#112233")), "the last index uses the custom hex");
            Check.That(!CustomisableUI.Swatches.TryResolve(custom, "garbage", out _), "a bad custom hex resolves to nothing");

            Check.Equal(CustomisableUI.Swatches.TextChoice.Vanilla, CustomisableUI.Swatches.ResolveText(0, "#112233", out _), "text index 0 is vanilla");
            Check.Equal(CustomisableUI.Swatches.TextChoice.Auto, CustomisableUI.Swatches.ResolveText(1, "#112233", out _), "text index 1 is Auto");
            Check.Equal(CustomisableUI.Swatches.TextChoice.Colour, CustomisableUI.Swatches.ResolveText(2, "#112233", out _), "text index 2 is the first named colour");
        }

        // ---- contrast ----------------------------------------------------------------------------------------------

        public static void ContrastChecks()
        {
            Check.Section("Contrast");

            Check.Near(1f, CustomisableUI.Contrast.Luminance(new Color(1f, 1f, 1f, 1f)), 0.001f, "white has luminance 1");
            Check.Near(0f, CustomisableUI.Contrast.Luminance(new Color(0f, 0f, 0f, 1f)), 0.001f, "black has luminance 0");
            Check.Near(21f, CustomisableUI.Contrast.Ratio(1f, 0f), 0.01f, "white on black is 21:1");
            Check.That(Same(CustomisableUI.Contrast.LightText, CustomisableUI.Contrast.Best(0.01f, 0.01f)), "dark surfaces get light text");
            Check.That(Same(CustomisableUI.Contrast.DarkText, CustomisableUI.Contrast.Best(0.7f, 0.7f)), "light surfaces get dark text");
            Check.Near(3f, CustomisableUI.Contrast.TargetFor(0), 0f, "target 0 is 3:1");
            Check.Near(4.5f, CustomisableUI.Contrast.TargetFor(1), 0f, "target 1 is 4.5:1");
            Check.Near(7f, CustomisableUI.Contrast.TargetFor(2), 0f, "target 2 is 7:1");
            Check.Near(7f, CustomisableUI.Contrast.TargetFor(99), 0f, "an out-of-range target is clamped");
        }

        // ---- the MCM screen ----------------------------------------------------------------------------------------

        public static void SettingsChecks()
        {
            Check.Section("Settings screen (as MCM sees it)");

            var settings = new ModSettings();
            Type discoverer = typeof(ModSettings).BaseType.Assembly.GetType("MCM.Implementation.AttributeSettingsPropertyDiscoverer", true);
            object instance = Activator.CreateInstance(discoverer);
            List<object> found = ((IEnumerable)discoverer.GetMethod("GetProperties").Invoke(instance, new object[] { settings })).Cast<object>().ToList();
            string[] ids = found.Select(p => (string)p.GetType().GetProperty("Id").GetValue(p)).ToArray();

            int declared = typeof(ModSettings).GetProperties().Count(p => p.GetCustomAttributes(typeof(BaseSettingPropertyAttribute), true).Any());
            Check.Equal(declared, found.Count, "MCM lists every property that carries a setting attribute");
            Check.That(found.Count >= 60, "the screen has its full set of options (" + found.Count + ")");

            foreach (string expected in new[]
            {
                "Enabled", "KeepOtherModColours", "ToggleHotkey", "PanelColour", "FrameColour", "ButtonColour", "TextColour",
                "ContrastTarget", "ShadowStrength", "MainFont", "HueShift", "AllyColour", "RelationNameplates",
                "GoodColour", "BadColour", "WarningColour", "StatusColours", "CrosshairColour", "BlockedColour",
                "HitMarkerColour", "KillMarkerColour", "CrosshairOpacity",
            })
            {
                Check.That(ids.Contains(expected), "option '" + expected + "' is on the screen");
            }

            Check.That(found.All(p => !string.IsNullOrEmpty(Convert.ToString(p.GetType().GetProperty("GroupName").GetValue(p)))),
                "every option sits in a group");

            // Defaults must change nothing: a fresh install looks like vanilla.
            ThemeSnapshot defaults = Theme(settings);
            Check.That(!defaults.Panel.Tint && !defaults.Frame.Tint && !defaults.Button.Tint, "defaults leave panels, frames and buttons alone");
            Check.That(defaults.Text.IsNoOp, "defaults leave text alone");
            Check.That(!defaults.Relations.Any && !defaults.Statuses.Any && !defaults.Crosshair.Any, "defaults leave relation, status and crosshair colours alone");
            Check.That(defaults.MainFont == null && defaults.DecorativeFont == null && defaults.TextScale == 1f, "defaults leave fonts and size alone");
            Check.That(!defaults.HasGrading && !defaults.KeepColours, "defaults leave grading and compatibility off");
            Check.Equal(1, settings.ToggleHotkey.SelectedIndex, "the hotkey defaults to Ctrl+Shift+U");
        }

        // ---- presets -----------------------------------------------------------------------------------------------

        public static void PresetsChecks()
        {
            Check.Section("Presets (readability)");

            var settings = new ModSettings();
            List<ISettingsPreset> presets = settings.GetBuiltInPresets().ToList();
            Check.That(presets.Count >= 22, "all the presets are present (" + presets.Count + ")");
            Check.Equal(presets.Count, presets.Select(p => p.Id).Distinct().Count(), "preset ids are unique");
            Check.Equal(presets.Count, presets.Select(p => p.Name).Distinct().Count(), "preset names are unique");

            int accessible = 0;
            foreach (ISettingsPreset preset in presets)
            {
                if (preset.Name.Contains("Default"))
                {
                    continue;
                }

                var loaded = (ModSettings)preset.LoadPreset();
                ThemeSnapshot snapshot = Theme(loaded);
                Color text = snapshot.Text.Tint ? snapshot.Text.Colour : CustomisableUI.Contrast.VanillaText;
                float ratio = CustomisableUI.Contrast.MinRatio(text, snapshot.PanelSurface, snapshot.ButtonSurface);

                // 0.05 of slack: the surfaces are estimates.
                Check.That(ratio + 0.05f >= snapshot.ContrastTarget,
                    "'" + preset.Name + "' text contrast " + ratio.ToString("0.0") + ":1 meets its target " + snapshot.ContrastTarget + ":1");

                if (preset.Name.StartsWith("Accessible:", StringComparison.Ordinal))
                {
                    accessible++;
                    Check.That(snapshot.ContrastTarget >= 4.5f, "'" + preset.Name + "' is held to at least WCAG AA");
                }
            }

            Check.That(accessible >= 7, "the accessibility group is complete (" + accessible + ")");

            // The colour-blind preset must really swap the red/green pair, for text and for the relation colours.
            ISettingsPreset cb = presets.First(p => p.Name.Contains("Colour-blind"));
            ThemeSnapshot cbTheme = Theme((ModSettings)cb.LoadPreset());
            Check.That(cbTheme.Statuses.HasGood && cbTheme.Statuses.HasBad, "colour-blind preset sets good and bad colours");
            Check.That(cbTheme.Relations.HasAlly && cbTheme.Relations.HasEnemy && cbTheme.Relations.HasNeutral, "colour-blind preset sets ally, enemy and neutral colours");
        }

        // ---- the panel / frame / button decision -------------------------------------------------------------------

        public static void ClassifierChecks()
        {
            Check.Section("What counts as a panel, frame or button");

            RoleOverrides.Load(Path.Combine(Path.GetTempPath(), "no-such-UiRoles.txt"));

            var cases = new[]
            {
                // Buttons and controls, including those the first version missed (names are the runtime form, "(Clone)").
                ("SPOptions.Dropdown.Center(Clone)", @"SPGeneral\SPOptions\standart_dropdown_with_arrows_mid", UiRole.Button),
                ("SPOptions.Dropdown.Left.Button(Clone)", @"SPGeneral\SPOptions\standart_dropdown_with_arrows_left", UiRole.Button),
                ("MCM.Slider.Canvas(Clone)", @"SPGeneral\SPOptions\standart_slider_canvas", UiRole.Button),
                ("MCM.Slider.Handle(Clone)", @"SPGeneral\SPOptions\standart_slider_knob", UiRole.Button),
                ("MCM.Input.Bool.Empty(Clone)", @"StdAssets\checkbox_empty", UiRole.Button),
                ("MCM.Setting.Item(Clone)", "mcm_entry_selected", UiRole.Button),
                ("Popup.Done.Button(Clone)", @"StdAssets\Popup\done_button", UiRole.Button),
                ("MapBar.Left.Button(Clone)", @"MapBar\bottom_left_button4_party", UiRole.Button),
                ("Clan.Management.Collapser", @"Clan\clan_collapser", UiRole.Button),

                // Frames and panels.
                ("Frame1Brush", "Frame1_frame", UiRole.Frame),
                ("Tooltip.Frame", @"General\TooltipHint\tooltip_frame", UiRole.Frame),
                ("Recruitment.Troop.Brush", @"SPGeneral\SPRecruitment\portrait_relation", UiRole.Panel),
                ("Inventory.Tooltip.Background", "inventory_tooltip_9", UiRole.Panel),
                (null, @"StdAssets\top_header", UiRole.Panel),
                (null, @"StdAssets\scroll_header", UiRole.Panel),
                (null, @"StdAssets\Popup\canvas", UiRole.Panel),
                (null, @"General\TooltipHint\tooltip_title_base", UiRole.Panel),

                // Things that must never be themed: bars, fills, icons, banners, dividers, scrollbars, arrows.
                ("MCM.Slider.Filler(Clone)", @"SPGeneral\SPOptions\standart_slider_fill", UiRole.Other),
                ("Party.Scrollbar.Handle", "BlankWhiteSquare_9", UiRole.Other),
                ("DefaultBrush(Clone)", @"General\Icons\Coin@2x", UiRole.Other),
                ("Nameplate.FlatBanner.Party(Clone)", @"StdAssets\banner_flat", UiRole.Other),
                ("ButtonLeftArrowBrush1(Clone)", @"StdAssets\arrow_pointing_left", UiRole.Other),
                (null, @"StdAssets\Popup\divider", UiRole.Other),
                (null, @"General\TooltipHint\tooltip_divider_simple", UiRole.Other),
                (null, "loadingBar_9", UiRole.Other),
                (null, "BlankWhiteSquare_9", UiRole.Other),
            };
            foreach (var (brush, sprite, expected) in cases)
            {
                Check.Equal(expected, RoleClassifier.Classify(brush, sprite), (brush ?? "(widget)") + " | " + sprite);
            }

            // UiRoles.txt rules run before the built-in guesses.
            string rules = Path.Combine(Path.GetTempPath(), "customisable-ui-test-roles-" + Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllLines(rules, new[] { "# a comment", "brush:Test.Fancy = panel", "sprite:some_icon_thing = button", "garbage line", "brush:Other.One = notarole" });
            try
            {
                RoleOverrides.Load(rules);
                Check.Equal(UiRole.Panel, RoleClassifier.Classify("Test.Fancy.Brush", "x"), "a brush rule in UiRoles.txt is honoured");
                Check.Equal(UiRole.Button, RoleClassifier.Classify("whatever", "some_icon_thing"), "a sprite rule beats the built-in 'icon' exclusion");
                Check.Equal(UiRole.Other, RoleClassifier.Classify("Other.One", "x"), "a rule with an unknown role is ignored");
            }
            finally
            {
                File.Delete(rules);
                RoleOverrides.Load(Path.Combine(Path.GetTempPath(), "no-such-UiRoles.txt"));
            }

            // Whether a widget-attached sprite's name allows it to be a large background is cached per name, so it must
            // follow UiRoles.txt when that is reloaded.
            Check.That(RoleClassifier.AllowsBackground("StdAssets/window_bg_9"), "an ordinary background name is allowed");
            Check.That(!RoleClassifier.AllowsBackground("General/Icons/Coin@2x"), "artwork names are never backgrounds");
            Check.That(!RoleClassifier.AllowsBackground("progress_bar_9"), "bars are never backgrounds");

            string backgroundRules = Path.Combine(Path.GetTempPath(), "customisable-ui-test-bg-" + Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllLines(backgroundRules, new[] { "sprite:window_bg = other" });
            try
            {
                RoleOverrides.Load(backgroundRules);
                Check.That(!RoleClassifier.AllowsBackground("StdAssets/window_bg_9"), "a UiRoles.txt rule keeps a sprite from being a background");
            }
            finally
            {
                File.Delete(backgroundRules);
                RoleOverrides.Load(Path.Combine(Path.GetTempPath(), "no-such-UiRoles.txt"));
            }
            Check.That(RoleClassifier.AllowsBackground("StdAssets/window_bg_9"), "removing the rule restores the sprite");
        }

        // ---- chrome styling ----------------------------------------------------------------------------------------

        public static void StylingChecks()
        {
            Check.Section("Styling panels, frames, buttons and text");

            var s = new ModSettings();
            Pick(s.PanelColour, "Navy");
            s.PanelBrightness = 1.6f;
            s.PanelOpacity = 0.5f;
            s.HueShift = 90;
            ThemeSnapshot theme = Theme(s);
            Color navy = Rgb("#2A3F8F");

            var textured = new SimpleMaterial { Color = new Color(1f, 1f, 1f, 1f) };
            Stylers.ApplyToSprite(theme, textured, UiRole.Panel, "panel_background_9");
            Check.That(Same(textured.Color, navy, 0.01f), "textured art is multiplied by the tint");
            Check.Near(1.6f, textured.ColorFactor, 0.001f, "brightness lifts textured art by the full amount");
            Check.Near(0.5f, textured.AlphaFactor, 0.001f, "opacity applies");
            Check.Near(0.25f, textured.HueFactor, 0.001f, "a 90 degree hue shift is a quarter turn");

            var flat = new SimpleMaterial { Color = new Color(0f, 0f, 0f, 0.5f) };
            Stylers.ApplyToSprite(theme, flat, UiRole.Panel, "BlankWhiteSquare_9");
            Check.That(Same(flat.Color, new Color(navy.Red * 0.6f, navy.Green * 0.6f, navy.Blue * 0.6f, 0.5f), 0.01f),
                "a flat fill takes a toned-down version of the colour and keeps its own transparency");
            Check.Near(1f + 0.6f * 0.3f, flat.ColorFactor, 0.001f, "a flat fill feels only part of the brightness slider");

            var other = new SimpleMaterial { Color = new Color(1f, 1f, 1f, 1f) };
            Stylers.ApplyToSprite(theme, other, UiRole.Other, "some_icon");
            Check.That(Same(other.Color, new Color(1f, 1f, 1f, 1f)) && other.ColorFactor == 1f && other.AlphaFactor == 1f && other.HueFactor == 0f,
                "anything that isn't chrome is left alone");

            // Text.
            var t = new ModSettings();
            t.TextColour.SelectedIndex = CustomisableUI.Swatches.TextIndexOf("Cream");
            t.OutlineThickness = 0.4f;
            ThemeSnapshot textTheme = Theme(t);
            TextMaterial ordinary = TextOf(new Color(0.60f, 0.45f, 0.85f, 0.9f));
            Stylers.ApplyToText(textTheme, ordinary, "DefaultBrush(Clone)", "Default");
            Check.That(Same(ordinary.Color, new Color(0.96f, 0.92f, 0.83f, 0.9f), 0.01f), "text takes the chosen colour and keeps its alpha");
            Check.Near(0.4f, ordinary.OutlineAmount, 0.001f, "the outline is applied");

            var auto = new ModSettings();
            auto.TextColour.SelectedIndex = 1;
            Pick(auto.PanelColour, "Parchment");
            auto.PanelBrightness = 2.1f;
            Pick(auto.ButtonColour, "Cream");
            auto.ButtonBrightness = 2.4f;
            Check.That(Same(CustomisableUI.Contrast.DarkText, Theme(auto).Text.Colour), "Auto text turns dark on a light theme");
        }

        // ---- good / bad / warning ----------------------------------------------------------------------------------

        public static void StatusColoursChecks()
        {
            Check.Section("Good, bad and warning colours");

            Check.Equal(StatusKind.Good, Stylers.KindOfStyle("Positive"), "'Positive' is good");
            Check.Equal(StatusKind.Good, Stylers.KindOfStyle("Bonus"), "'Bonus' is good");
            Check.Equal(StatusKind.Bad, Stylers.KindOfStyle("Negative"), "'Negative' is bad");
            Check.Equal(StatusKind.Bad, Stylers.KindOfStyle("Penalty"), "'Penalty' is bad");
            Check.Equal(StatusKind.Warning, Stylers.KindOfStyle("Warning"), "'Warning' is a warning");
            Check.Equal(StatusKind.None, Stylers.KindOfStyle("Increasing"), "'Increasing' describes direction, not good or bad");
            Check.Equal(StatusKind.None, Stylers.KindOfStyle(null), "no style is nothing");

            Color red = Rgb("#D81919");
            Color green = Rgb("#33CC3F");
            Color cream = Rgb("#F4E1C4");
            Check.Equal(StatusKind.Bad, Stylers.ClassifyHue(red), "strongly red text is bad");
            Check.Equal(StatusKind.Good, Stylers.ClassifyHue(green), "strongly green text is good");
            Check.Equal(StatusKind.None, Stylers.ClassifyHue(cream), "cream is not a status colour");
            Check.Equal(StatusKind.Bad, Stylers.ClassifyStatus("Warning", red, new StatusStyle()), "red warning text counts as bad when no warning colour is set");
            Check.Equal(StatusKind.Warning, Stylers.ClassifyStatus("Warning", red, new StatusStyle { HasWarning = true }), "warning text is a warning once a colour is set");

            var s = new ModSettings();
            Pick(s.GoodColour, "Sky Blue");
            Pick(s.BadColour, "Vermillion");
            ThemeSnapshot theme = Theme(s);
            Color sky = Rgb("#5AA8E8");
            Color vermillion = Rgb("#D55E00");

            TextMaterial positive = TextOf(new Color(cream.Red, cream.Green, cream.Blue, 0.8f));
            Stylers.ApplyToText(theme, positive, "Clan.Stat.Name.Text", "Positive");
            Check.That(Same(positive.Color, new Color(sky.Red, sky.Green, sky.Blue, 0.8f)), "text in the game's 'Positive' style takes the good colour (alpha kept)");

            TextMaterial penalty = TextOf(red);
            Stylers.ApplyToText(theme, penalty, "Crafting.Property.Value.Text", "Penalty");
            Check.That(Same(penalty.Color, vermillion), "text in the 'Penalty' style takes the bad colour");

            TextMaterial greenText = TextOf(green);
            Stylers.ApplyToText(theme, greenText, null, null);
            Check.That(Same(greenText.Color, sky), "green text with no style takes the good colour by hue");

            TextMaterial ordinary = TextOf(cream);
            Stylers.ApplyToText(theme, ordinary, "DefaultBrush", "Default");
            Check.That(Same(ordinary.Color, cream), "ordinary text is untouched");

            // The colour-blind palette on its own, and a custom colour winning over it.
            var cb = new ModSettings();
            cb.StatusColours.SelectedIndex = 1;
            TextMaterial cbRed = TextOf(red);
            Stylers.ApplyToText(Theme(cb), cbRed, null, null);
            Check.That(Same(cbRed.Color, new Color(0.90f, 0.62f, 0f, 1f)), "colour-blind safe turns red text orange on a dark theme");

            Pick(cb.BadColour, "Yellow");
            TextMaterial both = TextOf(red);
            Stylers.ApplyToText(Theme(cb), both, null, null);
            Check.That(Same(both.Color, Rgb("#F0E442")), "a colour picked explicitly beats the colour-blind palette");

            // "Recolour like other text" treats status text as ordinary text again.
            var recolour = new ModSettings();
            recolour.StatusColours.SelectedIndex = 2;
            Pick(recolour.GoodColour, "Sky Blue");
            Check.That(!Theme(recolour).Statuses.Any, "'recolour like other text' switches the status palette off");

            // Sprites: the inventory transfer slider's fill.
            var fill = new SimpleMaterial { Color = new Color(0.2f, 0.7f, 0.2f, 1f) };
            Check.That(SemanticStyler.TryApplyToSprite(theme, fill, "Inventory.Tuple.Slider.ChangeFill(Clone)", "Positive") && Same(fill.Color, sky),
                "the inventory slider fill takes the good colour for a gain");
            var other = new SimpleMaterial { Color = new Color(1f, 1f, 1f, 1f) };
            Check.That(!SemanticStyler.TryApplyToSprite(theme, other, "Popup.Frame(Clone)", "Default"), "an unrelated brush is not claimed");
        }

        // ---- crosshair ---------------------------------------------------------------------------------------------

        public static void CrosshairChecks()
        {
            Check.Section("Crosshair");

            var s = new ModSettings();
            Pick(s.CrosshairColour, "Yellow");
            Pick(s.BlockedColour, "Orange");
            Pick(s.HitMarkerColour, "Sky Blue");
            Pick(s.KillMarkerColour, "Vermillion");
            s.CrosshairOpacity = 0.5f;
            ThemeSnapshot theme = Theme(s);

            SimpleMaterial Draw(string brush, string style)
            {
                var m = new SimpleMaterial { Color = new Color(1f, 1f, 1f, 0.67f) };
                SemanticStyler.TryApplyToSprite(theme, m, brush, style);
                return m;
            }

            SimpleMaterial line = Draw("Crosshair.Top(Clone)", "Default");
            Check.That(Same(line.Color, new Color(Rgb("#F0E442").Red, Rgb("#F0E442").Green, Rgb("#F0E442").Blue, 0.67f)), "crosshair lines take the crosshair colour (alpha kept)");
            Check.Near(0.5f, line.AlphaFactor, 0.001f, "crosshair opacity applies");
            Check.That(Same(Draw("Crosshair.Left.Square(Clone)", "Invalid").Color, new Color(Rgb("#E69F00").Red, Rgb("#E69F00").Green, Rgb("#E69F00").Blue, 0.67f)), "the 'Invalid' style takes the blocked colour");
            Check.That(Same(Draw("Crosshair.HitMarker(Clone)", "Show").Color, new Color(Rgb("#5AA8E8").Red, Rgb("#5AA8E8").Green, Rgb("#5AA8E8").Blue, 0.67f)), "the hit marker takes the hit colour");
            Check.That(Same(Draw("Crosshair.HitMarker(Clone)", "ShowDeath").Color, new Color(Rgb("#D55E00").Red, Rgb("#D55E00").Green, Rgb("#D55E00").Blue, 0.67f)), "a kill takes the kill colour");
            Check.That(Same(Draw("Crosshair.HeadshotMarker(Clone)", "Show").Color, new Color(Rgb("#D55E00").Red, Rgb("#D55E00").Green, Rgb("#D55E00").Blue, 0.67f)), "the headshot marker takes the kill colour");

            ThemeSnapshot vanilla = Theme(new ModSettings());
            var untouched = new SimpleMaterial { Color = new Color(1f, 0f, 0f, 0.67f) };
            Check.That(!SemanticStyler.TryApplyToSprite(vanilla, untouched, "Crosshair.Top(Clone)", "Invalid") && untouched.AlphaFactor == 1f,
                "with nothing chosen the crosshair is not touched at all");

            // Each colour is independent: choosing only the blocked colour must not recolour the normal crosshair, and
            // choosing only the normal colour must leave the blocked (red) crosshair as the game draws it.
            var onlyBlocked = new ModSettings();
            Pick(onlyBlocked.BlockedColour, "Orange");
            ThemeSnapshot blockedTheme = Theme(onlyBlocked);
            var normalLine = new SimpleMaterial { Color = new Color(1f, 1f, 1f, 0.67f) };
            SemanticStyler.TryApplyToSprite(blockedTheme, normalLine, "Crosshair.Top(Clone)", "Default");
            Check.That(Same(normalLine.Color, new Color(1f, 1f, 1f, 0.67f)), "choosing only the blocked colour leaves the normal crosshair alone");
            var blockedLine = new SimpleMaterial { Color = new Color(1f, 0f, 0f, 0.67f) };
            SemanticStyler.TryApplyToSprite(blockedTheme, blockedLine, "Crosshair.Top(Clone)", "Invalid");
            Check.That(Same(blockedLine.Color, new Color(Rgb("#E69F00").Red, Rgb("#E69F00").Green, Rgb("#E69F00").Blue, 0.67f)), "choosing only the blocked colour recolours the blocked crosshair");

            var onlyLine = new ModSettings();
            Pick(onlyLine.CrosshairColour, "Yellow");
            ThemeSnapshot lineTheme = Theme(onlyLine);
            var stillRed = new SimpleMaterial { Color = new Color(1f, 0f, 0f, 0.67f) };
            SemanticStyler.TryApplyToSprite(lineTheme, stillRed, "Crosshair.Top(Clone)", "Invalid");
            Check.That(Same(stillRed.Color, new Color(1f, 0f, 0f, 0.67f)), "choosing only the normal colour leaves the blocked crosshair red");
        }

        // ---- text shadow -------------------------------------------------------------------------------------------

        public static void TextShadowChecks()
        {
            Check.Section("Text shadow");

            var s = new ModSettings();
            s.ShadowStrength = 0.6f;
            TextMaterial shadowed = TextOf(Rgb("#F4E1C4"));
            Stylers.ApplyToText(Theme(s), shadowed, "DefaultBrush", "Default");
            Check.That(Same(shadowed.GlowColor, new Color(Rgb("#101010").Red, Rgb("#101010").Green, Rgb("#101010").Blue, 1f), 0.01f), "the shadow takes the shadow colour");
            Check.Near(0.6f, shadowed.Blur, 0.001f, "the shadow's softness follows the strength");
            Check.That(shadowed.ShadowOffset > 0f && shadowed.ShadowAngle == 90f, "the shadow is nudged downward");

            // Text that already has a stronger vanilla shadow keeps it.
            TextMaterial already = TextOf(Rgb("#F4E1C4"));
            already.Blur = 1f;
            Stylers.ApplyToText(Theme(s), already, "DefaultBrush", "Default");
            Check.Near(1f, already.Blur, 0.001f, "an existing stronger blur is not weakened");

            TextMaterial off = TextOf(Rgb("#F4E1C4"));
            Stylers.ApplyToText(Theme(new ModSettings()), off, "DefaultBrush", "Default");
            Check.That(off.Blur == 0f && off.ShadowOffset == 0f, "at 0% the text is untouched");
        }

        // ---- ally / enemy / neutral --------------------------------------------------------------------------------

        public static void RelationsChecks()
        {
            Check.Section("Ally, enemy and neutral colours");

            // Read back from the colours the game's own brush files use.
            foreach (var (hex, expected) in new[]
            {
                ("#000000", Relation.Neutral), ("#245E05", Relation.Ally), ("#870707", Relation.Enemy),
                ("#42F456", Relation.Ally), ("#41F4E2", Relation.None), ("#F44141", Relation.Enemy), ("#DFA84F", Relation.None),
            })
            {
                Check.Equal(expected, RelationLookup.OfVanillaColour(Rgb(hex)), "vanilla colour " + hex);
            }

            // Party nameplates: the game's own colours, read from the game itself rather than copied here.
            Type nameplate = typeof(SandBox.ViewModelCollection.Nameplate.PartyNameplateVM);
            foreach (var (field, expected) in new[]
            {
                ("PositiveIndicator", Relation.Ally), ("PositiveArmyIndicator", Relation.Ally),
                ("AllianceIndicator", Relation.Ally), ("AllianceArmyIndicator", Relation.Ally),
                ("NegativeIndicator", Relation.Enemy), ("NegativeArmyIndicator", Relation.Enemy),
                ("NeutralIndicator", Relation.Neutral), ("NeutralArmyIndicator", Relation.Neutral),
                ("MainPartyIndicator", Relation.None), ("MainPartyArmyIndicator", Relation.None),
            })
            {
                string value = (string)nameplate.GetField(field, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null);
                Check.Equal(expected, RelationLookup.OfPartyNameplateColour(Color.ConvertStringToColor(value)), "the game's own " + field + " (" + value + ")");
            }

            var s = new ModSettings();
            Pick(s.AllyColour, "Sky Blue");
            Pick(s.EnemyColour, "Vermillion");
            Pick(s.NeutralColour, "Steel");
            ThemeSnapshot theme = Theme(s);
            Color sky = Rgb("#5AA8E8");

            // Party names: recoloured, and kept clear of the general text colour.
            TextMaterial partyName = TextOf(Color.ConvertStringToColor(
                (string)nameplate.GetField("NegativeIndicator", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null)));
            Stylers.ApplyToText(theme, partyName, "PartyNameplate.FullName.Text(Clone)", "Default");
            Check.That(Same(partyName.Color, Rgb("#D55E00")), "an enemy party's name takes the enemy colour");

            TextMaterial mainParty = TextOf(Color.ConvertStringToColor(
                (string)nameplate.GetField("MainPartyIndicator", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null)));
            Color before = mainParty.Color;
            Stylers.ApplyToText(theme, mainParty, "PartyNameplate.FullName.Text(Clone)", "Default");
            Check.That(Same(mainParty.Color, before), "the player's own party keeps its colour");

            // Sprites: settlement bars and battle markers, using the runtime "(Clone)" brush names.
            SimpleMaterial Bar(string brush, string hex)
            {
                var m = new SimpleMaterial { Color = Rgb(hex) };
                RelationStyler.TryApplyToSprite(theme, m, brush);
                return m;
            }
            Check.That(Same(Bar("SettlementCastleBrush(Clone)", "#245E05").Color, new Color(sky.Red * 0.6f, sky.Green * 0.6f, sky.Blue * 0.6f, 1f)), "an allied castle's bar takes the ally colour, toned down");
            Check.That(Same(Bar("FormationMarker.TeamType(Clone)", "#F44141").Color, Rgb("#D55E00")), "an enemy formation marker takes the enemy colour");
            Check.That(Same(Bar("FormationMarker.TeamType(Clone)", "#41F4E2").Color, Rgb("#41F4E2")), "the player's own formation marker keeps its colour");
            Check.That(Same(Bar("Mission.SiegeEngine.Marker.Enemy(Clone)", "#A83232").Color, Rgb("#D55E00")), "an enemy siege engine marker takes the enemy colour");

            TextMaterial tooltip = TextOf(Rgb("#32BA30", 0.8f));
            Stylers.ApplyToText(theme, tooltip, "Tooltip.AllyTroop.Text(Clone)", "Default");
            Check.That(Same(tooltip.Color, new Color(sky.Red, sky.Green, sky.Blue, 0.8f)), "an ally troop count in a tooltip takes the ally colour");

            TextMaterial friendly = TextOf(Rgb("#4EE04C"));
            Stylers.ApplyToText(theme, friendly, "NameMarker.TextBrush(Clone)", "Friendly");
            Check.That(Same(friendly.Color, sky), "a friendly name marker in battle takes the ally colour");

            // Nothing chosen: nothing changes.
            ThemeSnapshot vanilla = Theme(new ModSettings());
            var untouched = new SimpleMaterial { Color = Rgb("#870707") };
            Check.That(!RelationStyler.TryApplyToSprite(vanilla, untouched, "SettlementTownBrush(Clone)") && Same(untouched.Color, Rgb("#870707")),
                "with the three colours on Vanilla nothing is recoloured");
        }

        // ---- keeping other mods' colours ---------------------------------------------------------------------------

        public static void KeepColoursChecks()
        {
            Check.Section("Keeping colours added by other mods");

            foreach (var (hex, expected) in new[]
            {
                ("#0F3A5C", true), ("#14401C", true), ("#5C4308", true), ("#4A0C0C", true),
                ("#2A2520", false), ("#404040", false), ("#000000", false), ("#FFFFFF", false), ("#F4E1C4", false),
            })
            {
                Check.Equal(expected, Stylers.IsClearlyColoured(Rgb(hex)), "is " + hex + " clearly coloured?");
            }
            Check.That(!Stylers.IsClearlyColoured(new Color(0.9f, 0.1f, 0.1f, 0.01f)), "a nearly invisible colour doesn't count");

            var s = new ModSettings();
            s.TextColour.SelectedIndex = CustomisableUI.Swatches.TextIndexOf("Cream");
            TextMaterial rarity = TextOf(Rgb("#4A90E2"));
            Stylers.ApplyToText(Theme(s), rarity, "DefaultBrush", "Default");
            Check.That(Same(rarity.Color, Rgb("#F5EBD3")), "with the option off, coloured text is recoloured like the rest");

            s.KeepOtherModColours = true;
            TextMaterial kept = TextOf(Rgb("#4A90E2"));
            Stylers.ApplyToText(Theme(s), kept, "DefaultBrush", "Default");
            Check.That(Same(kept.Color, Rgb("#4A90E2")), "with the option on, another mod's coloured text is left alone");

            TextMaterial plain = TextOf(Rgb("#F4E1C4"));
            Stylers.ApplyToText(Theme(s), plain, "DefaultBrush", "Default");
            Check.That(Same(plain.Color, Rgb("#F5EBD3")), "with the option on, ordinary text is still recoloured");
        }

        // ---- the pause / autosave dimming layer --------------------------------------------------------------------

        public static void ScreenDimmingChecks()
        {
            Check.Section("Full-screen dimming layers");

            // Pretend the engine runs at 1920x1080 (the properties are backed by private static fields).
            Type screen = typeof(TaleWorlds.Engine.Screen);
            screen.GetField("<RealScreenResolutionWidth>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, 1920f);
            screen.GetField("<RealScreenResolutionHeight>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, 1080f);

            SimpleRectangle Box(float w, float h)
            {
                return new SimpleRectangle { X = 0f, Y = 0f, X2 = w, Y2 = h };
            }

            Check.That(RoleClassifier.IsFullScreenDim(Box(1920, 1080), 0.6f), "a translucent full-screen layer is a dimming layer");
            Check.That(RoleClassifier.IsFullScreenDim(Box(1920, 1080), 0.35f), "an autosave-style faint layer too");
            Check.That(RoleClassifier.IsFullScreenDim(Box(1800, 1000), 0.7f), "94% of the screen still counts as full-screen");
            Check.That(!RoleClassifier.IsFullScreenDim(Box(1920, 1080), 1f), "an opaque full-screen layer is a real screen background");
            Check.That(!RoleClassifier.IsFullScreenDim(Box(1000, 700), 0.6f), "a popup-sized translucent panel is not");
            Check.That(!RoleClassifier.IsFullScreenDim(Box(1920, 120), 0.6f), "a full-width strip is not");
        }

        // ---- against the game's real data --------------------------------------------------------------------------

        public static void GameDataChecks()
        {
            Check.Section("Against the game's own brush data");

            var counts = new Dictionary<UiRole, int>();
            int pairs = 0;
            foreach (string module in new[] { "Native", "SandBox" })
            {
                foreach (string file in Directory.GetFiles(Path.Combine(Program.GameFolder, "Modules", module, "GUI", "Brushes"), "*.xml"))
                {
                    XDocument document;
                    try
                    {
                        document = XDocument.Load(file);
                    }
                    catch
                    {
                        continue;
                    }

                    foreach (XElement brush in document.Descendants("Brush"))
                    {
                        string brushName = (string)brush.Attribute("Name");
                        foreach (XElement layer in brush.Descendants("BrushLayer"))
                        {
                            string sprite = (string)layer.Attribute("Sprite");
                            if (string.IsNullOrEmpty(sprite))
                            {
                                continue;
                            }

                            pairs++;
                            UiRole role = RoleClassifier.Classify(brushName, sprite);
                            counts[role] = counts.TryGetValue(role, out int n) ? n + 1 : 1;

                            // Invariants that must hold for every brush in the game.
                            if (sprite.IndexOf("progress", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                sprite.IndexOf("slider_fill", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                sprite.IndexOf("scrollbar", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                Check.That(role == UiRole.Other, "bar or scrollbar sprite stays vanilla: " + brushName + " | " + sprite);
                            }
                        }
                    }
                }
            }

            Check.That(pairs > 2000, "read the game's brush files (" + pairs + " brush/sprite pairs)");
            Check.That(Count(counts, UiRole.Panel) > 200, "a healthy number of panels are found (" + Count(counts, UiRole.Panel) + ")");
            Check.That(Count(counts, UiRole.Button) > 300, "a healthy number of buttons are found (" + Count(counts, UiRole.Button) + ")");
            Check.That(Count(counts, UiRole.Frame) > 50, "a healthy number of frames are found (" + Count(counts, UiRole.Frame) + ")");
            Check.That(Count(counts, UiRole.Other) > Count(counts, UiRole.Panel), "the cautious default holds: most sprites are left alone");
        }

        // ---- the Harmony hooks -------------------------------------------------------------------------------------

        public static void HooksChecks()
        {
            Check.Section("Harmony hooks bind to the game's real methods");

            var types = (Type[])typeof(SubModule).GetField("PatchTypes", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            const string owner = "CustomisableUI.Tests";
            var harmony = new Harmony(owner);
            try
            {
                foreach (Type type in types)
                {
                    try
                    {
                        harmony.CreateClassProcessor(type).Patch();
                        Check.That(true, type.Name + " binds");
                    }
                    catch (Exception ex)
                    {
                        Check.That(false, type.Name + " failed to bind: " + ex.Message.Split('\n')[0]);
                    }
                }

                Check.Equal(types.Length, harmony.GetPatchedMethods().Count(), "every hook patched its own method");
            }
            finally
            {
                harmony.UnpatchAll(owner);
            }
        }
    }
}
