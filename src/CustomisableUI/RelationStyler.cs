using System;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

namespace CustomisableUI
{
    // The colours chosen for allies, enemies and neutrals, and which parts of the UI they apply to.
    internal struct RelationStyle
    {
        public bool HasAlly;
        public bool HasEnemy;
        public bool HasNeutral;
        public Color Ally;
        public Color Enemy;
        public Color Neutral;

        // Settlement relation bars on the campaign map.
        public bool Bars;

        // Party tooltips (troop counts for allies, enemies and neutrals).
        public bool Tooltips;

        // In-battle formation markers, siege engine markers and friendly/enemy name text.
        public bool Battle;

        // Party and settlement names on the campaign map (replacing the faction colour they are drawn in).
        public bool Names;

        public bool Any => HasAlly || HasEnemy || HasNeutral;

        public bool TryGet(Relation relation, out Color colour)
        {
            switch (relation)
            {
                case Relation.Ally:
                    colour = Ally;
                    return HasAlly;
                case Relation.Enemy:
                    colour = Enemy;
                    return HasEnemy;
                case Relation.Neutral:
                    colour = Neutral;
                    return HasNeutral;
                default:
                    colour = default(Color);
                    return false;
            }
        }
    }

    // Recolours the parts of the UI the game itself colours by relation. Which brush is which comes from the base
    // game's brush files (Native\GUI\Brushes\Mission.xml and Tooltip.xml, SandBox\GUI\Brushes\Nameplates.xml).
    internal static class RelationStyler
    {
        // The settlement bars sit behind white name text, and the game's own versions are dark (#245E05, #870707).
        // Swatch colours are mid-tones, so they are toned down the same way as flat fills.
        private const float BarDarkening = Stylers.FlatFillDarkening;

        // Brush names have a "(Clone)" suffix at runtime, so everything here matches on the start of the name.
        private static bool Is(string brush, string name)
        {
            return brush.StartsWith(name, StringComparison.Ordinal);
        }

        // Returns true if the sprite belongs to a relation brush and was handled (recoloured or deliberately left).
        public static bool TryApplyToSprite(ThemeSnapshot theme, SimpleMaterial material, string brush)
        {
            RelationStyle styles = theme.Relations;
            if (!styles.Any || brush == null)
            {
                return false;
            }

            if (styles.Bars &&
                (Is(brush, "SettlementCastleBrush") || Is(brush, "SettlementVillageBrush") || Is(brush, "SettlementTownBrush")))
            {
                Recolour(material, RelationLookup.OfVanillaColour(material.Color), styles, BarDarkening);
                return true;
            }

            if (!styles.Battle)
            {
                return false;
            }
            if (Is(brush, "FormationMarker.TeamType"))
            {
                Recolour(material, RelationLookup.OfVanillaColour(material.Color), styles, 1f);
                return true;
            }
            if (Is(brush, "Mission.SiegeEngine.Marker.Ally"))
            {
                Recolour(material, Relation.Ally, styles, 1f);
                return true;
            }
            if (Is(brush, "Mission.SiegeEngine.Marker.Enemy"))
            {
                Recolour(material, Relation.Enemy, styles, 1f);
                return true;
            }
            return false;
        }

        // Returns true if the text belongs to a relation brush and was recoloured (or is deliberately left to the
        // colour the relation hooks gave it).
        public static bool TryApplyToText(ThemeSnapshot theme, TextMaterial material, string brush, string style)
        {
            RelationStyle styles = theme.Relations;
            if (!styles.Any || brush == null)
            {
                return false;
            }

            // Party names, speeds and troop counts on the campaign map are drawn in a colour the game chooses by
            // relation (see RelationLookup.PartyNameplate). Recognise that colour and swap in the chosen one. Either
            // way these are left out of the general text recolouring, so a colour picked under "Text" doesn't flatten
            // them to a single colour.
            if (styles.Names && (Is(brush, "PartyNameplate.FullName.Text") || Is(brush, "Map.Party.Mencount.Number")))
            {
                Relation seen = RelationLookup.OfPartyNameplateColour(material.Color);
                if (theme.DebugLogging)
                {
                    DebugRecorder.Note("party nameplate text " + Swatches.ToHex(material.Color) + " -> " + seen +
                                       (styles.TryGet(seen, out _) ? " (recoloured)" : " (left as the game draws it)"));
                }
                if (styles.TryGet(seen, out Color chosen))
                {
                    material.Color = new Color(chosen.Red, chosen.Green, chosen.Blue, material.Color.Alpha);
                }
                return true;
            }

            Relation relation = Relation.None;
            if (styles.Tooltips)
            {
                if (Is(brush, "Tooltip.AllyTroop.Text"))
                {
                    relation = Relation.Ally;
                }
                else if (Is(brush, "Tooltip.EnemyTroop.Text"))
                {
                    relation = Relation.Enemy;
                }
                else if (Is(brush, "Tooltip.NeutralTroop.Text"))
                {
                    relation = Relation.Neutral;
                }
            }
            if (relation == Relation.None && styles.Battle && Is(brush, "NameMarker.TextBrush"))
            {
                relation = style == "Enemy" ? Relation.Enemy : style == "Friendly" ? Relation.Ally : Relation.None;
            }

            if (relation == Relation.None || !styles.TryGet(relation, out Color target))
            {
                return false;
            }

            material.Color = new Color(target.Red, target.Green, target.Blue, material.Color.Alpha);
            return true;
        }

        private static void Recolour(SimpleMaterial material, Relation relation, RelationStyle styles, float scale)
        {
            if (relation == Relation.None || !styles.TryGet(relation, out Color target))
            {
                return;
            }
            material.Color = new Color(target.Red * scale, target.Green * scale, target.Blue * scale, material.Color.Alpha);
        }
    }
}
