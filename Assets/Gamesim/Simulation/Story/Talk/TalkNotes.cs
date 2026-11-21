using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The notebook's two free-talk blocks on a housemate's page (slice S1): "What you know about them", the facts the player has
    /// learned by layer, and "This week, from them", the dated "where they stand" snapshot. Pure, for the screen; empty at talk
    /// rules 0, where nothing is shown.
    /// </summary>
    public static class TalkNotes
    {
        public const string WhatYouKnowHeading = "What you know about them";
        public const string FromThemHeading = "This week, from them";

        /// <summary>The layers' headings, 1 to 3 (spec 3.7), and the arcs' secrets (depth 4).</summary>
        public const string WhoTheyAre = "Who they are", Background = "Their background", Drives = "What drives them", Secrets = "What they keep quiet";

        /// <summary>One layer's learned facts, shallowest first.</summary>
        public sealed class Layer
        {
            /// <summary>1 who they are, 2 their background, 3 what drives them, 4 what they keep quiet (learned through an arc).</summary>
            public int Number;
            public string Heading;
            public List<string> Facts = new List<string>();
        }

        /// <summary>The "where they stand" snapshot and its week.</summary>
        public sealed class Standing
        {
            public int Week;
            public string Line;

            /// <summary>Taken this week; an older snapshot is shown dated and greyed.</summary>
            public bool Current;

            /// <summary>"Week 3", for the date beside it.</summary>
            public string WeekLabel => "Week " + Week;
        }

        /// <summary>"What you know about them": the layers holding at least one learned fact, in layer order; empty when off or nothing is known.</summary>
        public static List<Layer> WhatYouKnow(EpisodeState s, string npcId)
        {
            var layers = new List<Layer>();
            if (!FreeTalk.On(s) || string.IsNullOrEmpty(npcId)) return layers;
            foreach (var fact in Lore.Learned(s, npcId))
            {
                int number = Math.Max(1, Math.Min(4, fact.depth));
                var layer = layers.FirstOrDefault(l => l.Number == number);
                if (layer == null) layers.Add(layer = new Layer { Number = number, Heading = HeadingOf(number) });
                layer.Facts.Add(fact.text);
            }
            return layers.OrderBy(l => l.Number).ToList();
        }

        /// <summary>"This week, from them": the latest snapshot, or null when off or none was taken.</summary>
        public static Standing FromThem(EpisodeState s, string npcId)
        {
            if (!FreeTalk.On(s)) return null;
            var contact = FreeTalk.Contact(s, npcId);
            if (contact == null || contact.standingWeek <= 0 || string.IsNullOrEmpty(contact.standing)) return null;
            return new Standing { Week = contact.standingWeek, Line = contact.standing, Current = contact.standingWeek == s.week };
        }

        // ------------------------------------------------------------------ slice S2 (talk rules 2): stage, chemistry, your read

        /// <summary>The labels before the housemate page's two slice-2 words: "Friendship: Friend", "Chemistry: easy company".</summary>
        public const string FriendshipLabel = "Friendship", ChemistryLabel = "Chemistry";

        /// <summary>A housemate's friendship stage and chemistry, for their page.</summary>
        public sealed class Bond
        {
            public FriendStage Stage;

            /// <summary>"Stranger" to "Ride or die".</summary>
            public string StageWord;

            /// <summary>The chemistry in effect (Neither until it shows).</summary>
            public ChemistryBand Chemistry;

            /// <summary>The chemistry's word, or null before their third counted talk (then nothing is shown for it).</summary>
            public string ChemistryWord;

            /// <summary>"Friendship: Friend".</summary>
            public string StageLine => FriendshipLabel + ": " + StageWord;

            /// <summary>"Chemistry: kindred", or null while it is not shown.</summary>
            public string ChemistryLine => ChemistryWord == null ? null : ChemistryLabel + ": " + ChemistryWord;
        }

        /// <summary>A housemate's stage and chemistry for their page, or null below talk rules 2 or for somebody who is not a housemate (nothing shown).</summary>
        public static Bond BondWith(EpisodeState s, string npcId)
        {
            if (!TalkChemistry.On(s) || string.IsNullOrEmpty(npcId)) return null;
            var npc = s.Find(npcId);
            if (npc == null || npc.isPlayer) return null;
            var stage = TalkFriendship.Stage(s, npcId);
            return new Bond
            {
                Stage = stage, StageWord = TalkFriendship.Word(stage),
                Chemistry = TalkChemistry.Band(s, npcId), ChemistryWord = TalkChemistry.WordFor(s, npcId),
            };
        }

        /// <summary>
        /// The player's own line, "The house reads you as easy to talk to.", or null below talk rules 2. When the player's own card was
        /// built from an All-Stars template, whatever it is now named, the Spark word is "a bold talker", never "a flirt" (rulings R7
        /// and R14).
        /// </summary>
        public static string HouseReadsYou(EpisodeState s)
        {
            string playerClass = TalkLean.PlayerClass(s);
            return playerClass == null ? null : TalkLean.LineFor(playerClass, FreeTalk.IsAllStar(s.Find(s.playerId)));
        }

        /// <summary>A layer's heading.</summary>
        public static string HeadingOf(int layer) =>
            layer == 1 ? WhoTheyAre : layer == 2 ? Background : layer == 3 ? Drives : layer == 4 ? Secrets : "";
    }
}
