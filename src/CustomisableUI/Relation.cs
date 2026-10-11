using System;
using TaleWorlds.Library;

namespace CustomisableUI
{
    // How something relates to the player. Same-faction and allied both count as Ally.
    internal enum Relation
    {
        None,
        Ally,
        Enemy,
        Neutral,
    }

    // The game already decides who is an ally, an enemy or neutral and bakes the answer into the colours it draws
    // with. Rather than re-deriving that from the campaign state, this reads the decision back from the colours.
    internal static class RelationLookup
    {
        private struct Known
        {
            public readonly Color Colour;
            public readonly Relation Relation;

            public Known(int argb, Relation relation)
            {
                Colour = Color.FromUint(unchecked((uint)argb));
                Relation = relation;
            }
        }

        // The colours of party nameplates on the campaign map: PartyNameplateVM's static constructor, as ARGB, with
        // the game's own choice of which applies (it checks war first, then alliance, then same faction). Each has a
        // variant for parties that belong to an army. The player's own party (-7545916 and its army variant) is left
        // out on purpose, so it keeps its colour.
        private static readonly Known[] PartyNameplate =
        {
            new Known(-9316796, Relation.Ally),       // PositiveIndicator: same faction
            new Known(-6162565, Relation.Ally),       // PositiveArmyIndicator
            new Known(-15507252, Relation.Ally),      // AllianceIndicator
            new Known(-15490612, Relation.Ally),      // AllianceArmyIndicator
            new Known(-2734522, Relation.Enemy),      // NegativeIndicator: at war
            new Known(-35467, Relation.Enemy),        // NegativeArmyIndicator
            new Known(-3090200, Relation.Neutral),    // NeutralIndicator
            new Known(-394241, Relation.Neutral),     // NeutralArmyIndicator
        };

        // The hex the game writes is rounded to whole 1/255ths, so match with a little room.
        private const float Tolerance = 0.02f;

        // Which relation a party nameplate's text colour stands for, or None for any other colour (including the
        // player's own party).
        public static Relation OfPartyNameplateColour(Color c)
        {
            foreach (Known known in PartyNameplate)
            {
                if (Math.Abs(c.Red - known.Colour.Red) <= Tolerance &&
                    Math.Abs(c.Green - known.Colour.Green) <= Tolerance &&
                    Math.Abs(c.Blue - known.Colour.Blue) <= Tolerance)
                {
                    return known.Relation;
                }
            }
            return Relation.None;
        }

        // Several of the game's relation brushes are drawn from one sprite in three colours, one per relation, and
        // the draw call can't say which layer it is. The colours are fixed in the game's brush data, so the relation
        // can be read back from them:
        //   settlement nameplates   neutral #000000   ally #245E05   enemy #870707
        //   formation markers       ally #42F456      player #41F4E2  enemy #F44141
        // "Player" (cyan) is deliberately not matched, so the player's own formation keeps its colour.
        public static Relation OfVanillaColour(Color c)
        {
            float max = Math.Max(c.Red, Math.Max(c.Green, c.Blue));
            if (max < 0.08f)
            {
                return Relation.Neutral;
            }
            if (c.Green > c.Red * 1.3f && c.Green > c.Blue * 1.3f)
            {
                return Relation.Ally;
            }
            if (c.Red > c.Green * 1.8f && c.Red > c.Blue * 1.8f)
            {
                return Relation.Enemy;
            }
            return Relation.None;
        }
    }
}
