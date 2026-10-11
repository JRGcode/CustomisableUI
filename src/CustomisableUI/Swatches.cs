using System;
using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.Library;

namespace CustomisableUI
{
    // The named colours offered in every colour dropdown, plus parsing for the "custom hex" box next to each one.
    // Index 0 always means "leave the vanilla colour alone" and the last index means "use the hex code".
    internal static class Swatches
    {
        public const string Vanilla = "Vanilla (no change)";
        public const string Custom = "Custom (use hex code)";

        // Text only: light or dark, whichever reads better on the chosen panel and button colours.
        public const string Auto = "Auto (light or dark to suit panels)";

        public enum TextChoice
        {
            Vanilla,
            Auto,
            Colour,
        }

        private struct Swatch
        {
            public readonly string Name;
            public readonly string Hex;

            public Swatch(string name, string hex)
            {
                Name = name;
                Hex = hex;
            }
        }

        private static readonly Swatch[] Named =
        {
            new Swatch("White", "#FFFFFF"),
            new Swatch("Cream", "#F5EBD3"),
            new Swatch("Parchment", "#E8D5A8"),
            new Swatch("Gold", "#E0B040"),
            new Swatch("Amber", "#D98A1F"),
            new Swatch("Copper", "#B8673A"),
            new Swatch("Crimson", "#C03040"),
            new Swatch("Burgundy", "#8A2430"),
            new Swatch("Rose", "#D86A8A"),
            new Swatch("Plum", "#7A3A78"),
            new Swatch("Royal Purple", "#6A4AC0"),
            new Swatch("Navy", "#2A3F8F"),
            new Swatch("Royal Blue", "#3A64D8"),
            new Swatch("Sky Blue", "#5AA8E8"),
            new Swatch("Teal", "#2A9A98"),
            new Swatch("Forest Green", "#3E8A44"),
            new Swatch("Olive", "#7A8A3A"),
            new Swatch("Steel", "#8C98A8"),
            new Swatch("Slate", "#6B7685"),
            new Swatch("Charcoal", "#44464C"),
            new Swatch("Black", "#101010"),

            // The Okabe-Ito colour-blind-safe palette. Appended after the original swatches so that saved selections
            // made before these existed keep pointing at the same colours.
            new Swatch("Yellow", "#F0E442"),
            new Swatch("Orange", "#E69F00"),
            new Swatch("Vermillion", "#D55E00"),
            new Swatch("Bluish Green", "#009E73"),
            new Swatch("Reddish Purple", "#CC79A7"),
            new Swatch("Deep Blue", "#0072B2"),
        };

        private static readonly string[] AllNamesCache = BuildNames(includeAuto: false);
        private static readonly string[] TextNamesCache = BuildNames(includeAuto: true);

        // A fresh list each call: MCM's dropdown owns the list it is given.
        public static IEnumerable<string> AllNames()
        {
            return (string[])AllNamesCache.Clone();
        }

        // Same as AllNames, with "Auto" added as the second entry (index 1).
        public static IEnumerable<string> TextNames()
        {
            return (string[])TextNamesCache.Clone();
        }

        public static int IndexOf(string name)
        {
            int index = Array.IndexOf(AllNamesCache, name);
            return index < 0 ? 0 : index;
        }

        public static int TextIndexOf(string name)
        {
            int index = Array.IndexOf(TextNamesCache, name);
            return index < 0 ? 0 : index;
        }

        // Resolves a selection from the text colour dropdown (the TextNames list).
        public static TextChoice ResolveText(int index, string customHex, out Color colour)
        {
            colour = new Color(1f, 1f, 1f, 1f);
            if (index <= 0)
            {
                return TextChoice.Vanilla;
            }
            if (index == 1)
            {
                return TextChoice.Auto;
            }

            bool resolved = index >= TextNamesCache.Length - 1
                ? TryParseHex(customHex, out colour)
                : TryParseHex(Named[index - 2].Hex, out colour);
            return resolved ? TextChoice.Colour : TextChoice.Vanilla;
        }

        // False means "vanilla": the caller should not recolour anything.
        public static bool TryResolve(int index, string customHex, out Color colour)
        {
            colour = new Color(1f, 1f, 1f, 1f);
            if (index <= 0)
            {
                return false;
            }
            if (index >= AllNamesCache.Length - 1)
            {
                return TryParseHex(customHex, out colour);
            }
            return TryParseHex(Named[index - 1].Hex, out colour);
        }

        // "#RRGGBBFF", the form the game uses for colour strings in view-models.
        public static string ToHex(Color colour)
        {
            return "#" + ToByte(colour.Red).ToString("X2", CultureInfo.InvariantCulture)
                       + ToByte(colour.Green).ToString("X2", CultureInfo.InvariantCulture)
                       + ToByte(colour.Blue).ToString("X2", CultureInfo.InvariantCulture)
                       + "FF";
        }

        private static int ToByte(float component)
        {
            return (int)Math.Round(Math.Max(0f, Math.Min(1f, component)) * 255f);
        }

        // Accepts "#RRGGBB", "RRGGBB", "#RGB" and "RGB". Anything else is rejected, so a half-typed code in the
        // settings box simply leaves the previous look in place instead of flashing a wrong colour.
        public static bool TryParseHex(string text, out Color colour)
        {
            colour = new Color(1f, 1f, 1f, 1f);
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string hex = text.Trim().TrimStart('#');
            if (hex.Length == 3)
            {
                hex = string.Concat(hex[0], hex[0], hex[1], hex[1], hex[2], hex[2]);
            }
            if (hex.Length != 6 ||
                !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
            {
                return false;
            }

            colour = new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
            return true;
        }

        private static string[] BuildNames(bool includeAuto)
        {
            var names = new List<string> { Vanilla };
            if (includeAuto)
            {
                names.Add(Auto);
            }
            foreach (Swatch swatch in Named)
            {
                names.Add(swatch.Name);
            }
            names.Add(Custom);
            return names.ToArray();
        }
    }
}
