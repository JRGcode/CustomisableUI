using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

namespace CustomisableUI
{
    internal enum UiRole
    {
        // Icons, portraits, banners, bars, cursors... anything not clearly chrome. Never recoloured by default.
        Other,
        Panel,
        Frame,
        Button,
    }

    // Decides which role a drawn sprite plays. Gauntlet gives sprites no "this is a panel" flag, so the decision is
    // made from the names of the brush and sprite being drawn, which follow consistent conventions in the base game
    // ("...Background", "...Frame", "...Button"). Rules in UiRoles.txt (see RoleOverrides) are checked first.
    internal static class RoleClassifier
    {
        private static readonly ConcurrentDictionary<Key, UiRole> Cache = new ConcurrentDictionary<Key, UiRole>();
        private static readonly ConcurrentDictionary<string, bool> BackgroundNames = new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);

        // Artwork, bars and effects are never chrome, whichever of the two names gives them away. Leaving something
        // vanilla is always safer than recolouring it wrongly (a tinted health bar or glow changes its meaning).
        //
        // "portrait" is deliberately not here: sprites such as SPRecruitment\portrait_relation are the slot a
        // portrait sits in, i.e. chrome. Actual character portraits are drawn from textures, not named sprites.
        private static readonly string[] ArtworkSprites =
        {
            "icon", "flag", "banner", "avatar", "arrow_", "coin", "crest", "emblem", "cursor", "divider", "loading",
            "progress", "checkmark", "fill", "glow", "particle", "highlight", "shadow", "scrollbar", "scroller",
            "scroll_button",
        };
        private static readonly string[] ArtworkBrushes =
        {
            "icon", "banner", "scroll", "divider", "healthbar", "fillbar", "progress", "marker", "compass",
            "crosshair", "nameplate", "particle",
        };

        private static readonly string[] ButtonWords =
        {
            "button", "collapser", "dropdown", "checkbox", "knob", "slider_canvas", "slider_frame", "entry",
        };
        private static readonly string[] FrameWords = { "frame", "border", "outline" };
        private static readonly string[] PanelBrushWords =
        {
            "background", "panel", "popup", "tooltip", "window", "container", "canvas", "card", "backdrop",
            "tuple", "listbg", "itembg", "textbox", "slot", "header", "tabbar",
        };
        private static readonly string[] PanelSpriteWords =
        {
            "panel", "background", "popup", "canvas", "hintbg", "stone_texture", "tuple", "slot", "dent", "portrait",
            "header", "tabbar", "card", "tooltip",
        };

        // A sprite covering at least this fraction of the screen in both directions counts as full-screen.
        private const float FullScreenFraction = 0.9f;

        // Full-screen sprites at least this opaque are real screen backgrounds, not dimming overlays.
        private const float OpaqueAlpha = 0.98f;

        private static float _screenWidth;
        private static float _screenHeight;
        private static long _screenCheckedAt;

        // A widget-attached sprite smaller than this (in pixels) is a decoration, not a panel.
        private const float LargeBackgroundMinWidth = 250f;
        private const float LargeBackgroundMinHeight = 160f;

        private struct Key : IEquatable<Key>
        {
            private readonly string _brush;
            private readonly string _sprite;

            public Key(string brush, string sprite)
            {
                _brush = brush;
                _sprite = sprite;
            }

            public bool Equals(Key other)
            {
                return string.Equals(_brush, other._brush, StringComparison.Ordinal) &&
                       string.Equals(_sprite, other._sprite, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is Key other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((_brush != null ? _brush.GetHashCode() : 0) * 397) ^ (_sprite != null ? _sprite.GetHashCode() : 0);
                }
            }
        }

        public static UiRole Classify(string brushName, string spriteName)
        {
            var key = new Key(brushName, spriteName);
            if (Cache.TryGetValue(key, out UiRole cached))
            {
                return cached;
            }

            UiRole role = Decide(brushName ?? string.Empty, spriteName ?? string.Empty);
            Cache[key] = role;
            return role;
        }

        // Plain widgets that carry their own sprite (rather than a brush) have no brush name to go on, so the many
        // large flat rectangles and stretchable (nine-patch) sprites that make up window backgrounds would all fall
        // through as "unknown". Size separates those from bars, dividers and highlights. Anything whose name marks
        // it as artwork is still left alone.
        public static bool IsLargeBackgroundCandidate(TaleWorlds.TwoDimension.Sprite sprite)
        {
            string name = sprite.Name ?? string.Empty;
            bool flatFill = name.StartsWith("BlankWhite", StringComparison.OrdinalIgnoreCase);
            if (!flatFill && !sprite.NinePatchParameters.IsValid)
            {
                return false;
            }
            return AllowsBackground(name);
        }

        // The name-only half of that decision, which is the expensive half (a couple of dozen substring searches) and
        // is asked for every widget-attached sprite on every frame. A name never changes its answer until UiRoles.txt
        // is reloaded, so each is worked out once.
        internal static bool AllowsBackground(string name)
        {
            return BackgroundNames.GetOrAdd(name, NameAllowsBackground);
        }

        private static bool NameAllowsBackground(string name)
        {
            return !ContainsAny(name, ArtworkSprites) && !RoleOverrides.TryMatch(string.Empty, name, out _);
        }

        public static bool IsLargeBackground(TaleWorlds.TwoDimension.Sprite sprite, TaleWorlds.TwoDimension.SimpleRectangle box)
        {
            return IsLargeBackgroundCandidate(sprite) &&
                   box.Width >= LargeBackgroundMinWidth && box.Height >= LargeBackgroundMinHeight;
        }

        // The translucent full-screen layers the game lays over the world while paused, saving, loading and so on.
        // They are flat fills too, so without this they would be tinted like any other panel and colour the whole
        // screen. Opaque full-screen fills (screen backgrounds such as the Clan screen's) are not caught by this.
        public static bool IsFullScreenDim(TaleWorlds.TwoDimension.SimpleRectangle box, float alpha)
        {
            if (alpha >= OpaqueAlpha)
            {
                return false;
            }

            RefreshScreenSize();
            return _screenWidth > 0f && _screenHeight > 0f &&
                   box.Width >= _screenWidth * FullScreenFraction &&
                   box.Height >= _screenHeight * FullScreenFraction;
        }

        // The resolution is read at most about once a second; it only changes when the player changes video settings.
        private static void RefreshScreenSize()
        {
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            if (_screenWidth > 0f && now - _screenCheckedAt < System.Diagnostics.Stopwatch.Frequency)
            {
                return;
            }

            _screenCheckedAt = now;
            try
            {
                _screenWidth = TaleWorlds.Engine.Screen.RealScreenResolutionWidth;
                _screenHeight = TaleWorlds.Engine.Screen.RealScreenResolutionHeight;
            }
            catch (Exception ex)
            {
                _screenWidth = 0f;
                _screenHeight = 0f;
                Log.Error("Reading screen resolution", ex);
            }
        }

        // Forget past decisions, e.g. after UiRoles.txt is reloaded.
        public static void ClearCache()
        {
            Cache.Clear();
            BackgroundNames.Clear();
        }

        private static UiRole Decide(string brush, string sprite)
        {
            if (RoleOverrides.TryMatch(brush, sprite, out UiRole overridden))
            {
                return overridden;
            }

            if (ContainsAny(sprite, ArtworkSprites) || ContainsAny(brush, ArtworkBrushes))
            {
                return UiRole.Other;
            }
            if (ContainsAny(brush, ButtonWords) || ContainsAny(sprite, ButtonWords))
            {
                return UiRole.Button;
            }
            if (ContainsAny(sprite, FrameWords) || ContainsAny(brush, FrameWords))
            {
                return UiRole.Frame;
            }
            if (ContainsAny(brush, PanelBrushWords) || ContainsAny(sprite, PanelSpriteWords))
            {
                return UiRole.Panel;
            }
            return UiRole.Other;
        }

        internal static bool ContainsAny(string text, string[] words)
        {
            foreach (string word in words)
            {
                if (text.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }
    }

    // UiRoles.txt, next to SubModule.xml, lets players correct a misclassified element without a rebuild:
    //
    //     # brush:<text in the brush name> = panel | frame | button | other
    //     brush:Inventory.ItemTuple = panel
    //     sprite:slick_frame = frame
    //
    // The first matching line wins, and these rules run before the built-in ones.
    internal static class RoleOverrides
    {
        private struct Rule
        {
            public bool OnBrush;
            public string Text;
            public UiRole Role;
        }

        private static List<Rule> _rules = new List<Rule>();

        public static void Load(string path)
        {
            var rules = new List<Rule>();
            try
            {
                if (File.Exists(path))
                {
                    foreach (string raw in File.ReadAllLines(path))
                    {
                        if (TryParse(raw, out Rule rule))
                        {
                            rules.Add(rule);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Reading UiRoles.txt", ex);
            }

            _rules = rules;
            RoleClassifier.ClearCache();
            Log.Info("Loaded " + rules.Count + " role override rule(s) from " + path);
        }

        public static bool TryMatch(string brush, string sprite, out UiRole role)
        {
            foreach (Rule rule in _rules)
            {
                string target = rule.OnBrush ? brush : sprite;
                if (target.IndexOf(rule.Text, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    role = rule.Role;
                    return true;
                }
            }
            role = UiRole.Other;
            return false;
        }

        private static bool TryParse(string line, out Rule rule)
        {
            rule = default(Rule);
            line = line.Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                return false;
            }

            int equals = line.LastIndexOf('=');
            int colon = line.IndexOf(':');
            if (equals < 0 || colon < 0 || colon > equals)
            {
                return false;
            }

            string kind = line.Substring(0, colon).Trim();
            string text = line.Substring(colon + 1, equals - colon - 1).Trim();
            string roleName = line.Substring(equals + 1).Trim();
            if (text.Length == 0 || !Enum.TryParse(roleName, true, out UiRole role))
            {
                return false;
            }

            if (kind.Equals("brush", StringComparison.OrdinalIgnoreCase))
            {
                rule = new Rule { OnBrush = true, Text = text, Role = role };
                return true;
            }
            if (kind.Equals("sprite", StringComparison.OrdinalIgnoreCase))
            {
                rule = new Rule { OnBrush = false, Text = text, Role = role };
                return true;
            }
            return false;
        }
    }
}
