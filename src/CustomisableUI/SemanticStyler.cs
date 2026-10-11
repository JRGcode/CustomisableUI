using System;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

namespace CustomisableUI
{
    // Sprites the game colours to mean something other than "chrome": the battle crosshair, and the slider fills that
    // show a gain or a loss. Which brush is which comes from the base game's brush files (Native\GUI\Brushes\
    // Crosshair.xml and the inventory, party and crafting brushes). The brush's current *style* ("Invalid", "Show",
    // "Positive"...) says what state it is in.
    internal static class SemanticStyler
    {
        private static bool Is(string brush, string name)
        {
            return brush.StartsWith(name, StringComparison.Ordinal);
        }

        // Returns true if the sprite belongs to one of these brushes and was handled (recoloured or deliberately left).
        public static bool TryApplyToSprite(ThemeSnapshot theme, SimpleMaterial material, string brush, string style)
        {
            if (brush == null)
            {
                return false;
            }

            CrosshairStyle crosshair = theme.Crosshair;
            if (crosshair.Any && Is(brush, "Crosshair."))
            {
                ApplyCrosshair(crosshair, material, brush, style);
                return true;
            }

            StatusStyle statuses = theme.Statuses;
            if (statuses.Any && IsStatusFill(brush))
            {
                StatusKind kind = Stylers.KindOfStyle(style);
                if (kind != StatusKind.None && statuses.TryGet(kind, out Color colour))
                {
                    material.Color = new Color(colour.Red, colour.Green, colour.Blue, material.Color.Alpha);
                }
                return true;
            }

            return false;
        }

        // The six line brushes are white, and turn red in the "Invalid" style (the prefab binds that to the target
        // being invalid). The hit marker is white, red when it shows a kill ("ShowDeath"); the headshot marker is red.
        private static void ApplyCrosshair(CrosshairStyle crosshair, SimpleMaterial material, string brush, string style)
        {
            bool has;
            Color colour;
            if (Is(brush, "Crosshair.HitMarker"))
            {
                bool kill = style == "ShowDeath";
                has = kill ? crosshair.HasKill : crosshair.HasHit;
                colour = kill ? crosshair.Kill : crosshair.Hit;
            }
            else if (Is(brush, "Crosshair.HeadshotMarker"))
            {
                has = crosshair.HasKill;
                colour = crosshair.Kill;
            }
            else
            {
                bool blocked = style == "Invalid";
                has = blocked ? crosshair.HasBlocked : crosshair.HasLine;
                colour = blocked ? crosshair.Blocked : crosshair.Line;
            }

            if (has)
            {
                material.Color = new Color(colour.Red, colour.Green, colour.Blue, material.Color.Alpha);
            }
            if (crosshair.Opacity < 1f)
            {
                material.AlphaFactor *= crosshair.Opacity;
            }
        }

        // The sliders that preview a change: the inventory and party transfer sliders (green for a gain, red for a
        // loss) and the crafting property slider (bonus and penalty).
        private static bool IsStatusFill(string brush)
        {
            return Is(brush, "Inventory.Tuple.Slider.ChangeFill") ||
                   Is(brush, "Party.TroopTuple.Slider.ChangeFill") ||
                   Is(brush, "Crafting.Property.Slider.FillTarget");
        }
    }
}
