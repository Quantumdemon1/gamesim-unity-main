using System.Collections.Generic;

namespace Gamesim.Presentation
{
    /// <summary>
    /// What stands in the yard when (competition yard, slice 2): every piece the yard's course and
    /// production dressing are authored with, and whether it is put away in everyday life - free
    /// time, the ceremonies, every moment no competition is being played or staged - or stays.
    /// <see cref="CompetitionYardDressing"/> is the one owner that acts on this list: the pieces put
    /// away here are deployed again for a competition, as its set, from the moment the player starts
    /// the ranked game or practice, and for a competition's staged show, and are put away when that
    /// presentation ends.
    ///
    /// <para>The pieces are found by the names the authoring passes give them (HouseSetPieces.Course
    /// and Podiums, HouseRoomFinish's competition yard, HouseAmenitiesAuthoring, the entrance), so
    /// neither the scene file nor an editor pass changes. It names no Unity type: the Unity-free
    /// suite reads it as the editor does.</para>
    /// </summary>
    public static class CompetitionYardPieces
    {
        /// <summary>Where a piece is in everyday life.</summary>
        public enum Everyday
        {
            /// <summary>Drawn and standing, as authored: part of the house's television set.</summary>
            Stays,
            /// <summary>Not drawn and not in anyone's way, its colliders with it: competition equipment.</summary>
            PutAway,
        }

        /// <summary>One authored piece (or one kind of piece, where the yard has several).</summary>
        public sealed class Piece
        {
            /// <summary>The name its authoring gives it, without the editor's copy number or a " (set)" suffix.</summary>
            public readonly string Name;
            /// <summary>Where it is authored, under the house's root (for the reader).</summary>
            public readonly string Where;
            /// <summary>How many the yard holds.</summary>
            public readonly int Count;
            /// <summary>Whether it is put away in everyday life.</summary>
            public readonly Everyday State;
            /// <summary>Whether it carries a collider (a fitted layer-8 Collision proxy or a box), so the NavMesh was baked around it.</summary>
            public readonly bool Collides;
            /// <summary>Why, in the owner's terms.</summary>
            public readonly string Reason;

            public Piece(string name, string where, int count, Everyday state, bool collides, string reason)
            { Name = name; Where = where; Count = count; State = state; Collides = collides; Reason = reason; }
        }

        /// <summary>The stacks: three neon pegs of rings the owner calls the tower of Hanoi.</summary>
        public const string Stack = "bb_set_comp_stack";

        /// <summary>The podiums' name stem, for the authored "(set)" models and the box blocks they stand on.</summary>
        public const string Podium = "Competition podium";

        /// <summary>Every piece, put away or staying, in the order the owner reads them.</summary>
        public static readonly IReadOnlyList<Piece> All = new[]
        {
            new Piece(Stack, "Set Pieces/Competition course", 3, Everyday.PutAway, true,
                "The neon stacks of rings (the tower of Hanoi): competition equipment standing in the middle of the yard."),
            new Piece("bb_set_comp_crate", "Set Pieces/Competition course", 3, Everyday.PutAway, true,
                "The crates of spare rings beside the stacks: competition equipment."),
            new Piece("bb_set_comp_lane", "Set Pieces/Competition course", 3, Everyday.PutAway, false,
                "The three neon lanes painted across the yard: the course's own floor."),
            new Piece("Start line", "Room Finish/Competition yard", 3, Everyday.PutAway, false,
                "The lanes' start markings go with the lanes."),
            new Piece("Finish line", "Room Finish/Competition yard", 3, Everyday.PutAway, false,
                "The lanes' finish markings go with the lanes."),
            new Piece("bb_set_comp_gate", "Set Pieces/Competition course", 3, Everyday.PutAway, false,
                "The neon gates at the head of the lanes: course equipment."),
            new Piece("bb_set_sign_hoh", "Set Pieces/Competition course", 1, Everyday.PutAway, false,
                "Competition lettering (HOH) over the backdrop."),
            new Piece("bb_set_sign_pillars", "Set Pieces/Competition course", 1, Everyday.PutAway, false,
                "Competition lettering beside it."),
            new Piece(Podium, "Set Pieces/Podiums, and the box blocks under the house's root", 3, Everyday.PutAway, true,
                "The contestants' podiums: competition furniture standing in the yard."),
            new Piece("bb_set_comp_backdrop", "Set Pieces/Competition course", 1, Everyday.Stays, false,
                "Stays: it closes the back of the yard as the television set's wall, the house's public yard display "
                + "(the week, the Head of Household, the veto) hangs on its face, and the competition's briefing frames it."),
            new Piece("Yard display", "Room Finish/Competition yard", 1, Everyday.Stays, false,
                "Stays: the house's public display, on the backdrop's face."),
            new Piece("Flight case", "Room Finish/Competition yard", 3, Everyday.Stays, false,
                "Stays: production's road cases by the west fence, outside the field."),
            new Piece("bb_set_entrancearch", "Set Pieces/Entrance", 1, Everyday.Stays, false,
                "Stays: the roped entrance is how the house announces itself."),
            new Piece("bb_set_stanchion", "Set Pieces/Entrance", 6, Everyday.Stays, false,
                "Stays: the entrance's brass stanchions."),
            new Piece("bb_set_velvetrope", "Set Pieces/Entrance", 4, Everyday.Stays, false,
                "Stays: the entrance's velvet ropes."),
            new Piece("bb_set_lighttower", "Set Pieces/House amenities", 2, Everyday.Stays, true,
                "Stays: the television set's light towers, at the yard's edge."),
            new Piece("bb_set_studiocam", "Set Pieces/House amenities", 1, Everyday.Stays, true,
                "Stays: the television set's studio camera, at the yard's edge."),
        };

        /// <summary>
        /// A node's name without the editor's copy number - " (2)" - or the podium pass's " (set)", and
        /// without a trailing number of its own ("Competition podium 3"): the name its authoring gives.
        /// </summary>
        public static string Stem(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            int cut = name.IndexOf(" (", System.StringComparison.Ordinal);
            string stem = cut > 0 ? name.Substring(0, cut) : name;
            int end = stem.Length;
            while (end > 0 && char.IsDigit(stem[end - 1])) end--;
            if (end < stem.Length && end > 1 && stem[end - 1] == ' ') stem = stem.Substring(0, end - 1);
            return stem;
        }

        /// <summary>The listed piece a node of this name is, or null for anything else in the yard.</summary>
        public static Piece Find(string name)
        {
            string stem = Stem(name);
            foreach (var piece in All) if (piece.Name == stem) return piece;
            return null;
        }

        /// <summary>Whether a node of this name is a piece put away in everyday life.</summary>
        public static bool PutAwayInEveryday(string name)
        {
            var piece = Find(name);
            return piece != null && piece.State == Everyday.PutAway;
        }
    }
}
