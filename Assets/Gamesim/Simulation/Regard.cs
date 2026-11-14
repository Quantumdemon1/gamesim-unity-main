using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// How one houseguest sees the player (plan 32 §J, "The house talks back"): their faith in you, 0 to 100, the fear your
    /// wins put in them, and the reasons the player already has the evidence for. A pure reading, stored nowhere, and built
    /// only from what that houseguest can know.
    ///
    /// <para>It is never fed into <see cref="ThreatAssessment"/>'s totals or ranked targets or into <see cref="NpcDeals"/>,
    /// and in a weekly NPC turn it decides no NPC's own choice and moves no score: through <see cref="Candour"/> it only words
    /// what a houseguest tells the player.</para>
    ///
    /// <para>Each term carries the story-rules level that introduced it and reads 0 below that level, so a later stack never
    /// changes an earlier stack's answers. S1 builds the rules-12 terms (<see cref="StoryRules.Manners"/>); the
    /// talked-about-them term (13) lands with the talkers and the asking-around term (14) with the quiet asks.</para>
    /// </summary>
    public static class Regard
    {
        /// <summary>One houseguest's reading of the player.</summary>
        public sealed class Reading
        {
            public double faith, fear;
            /// <summary>The terms that moved faith, by name, for the tests and the profile's evidence; never shown as numbers.</summary>
            public readonly List<KeyValuePair<string, double>> terms = new List<KeyValuePair<string, double>>();
            /// <summary>Reasons in words, only where the player already has the evidence (§J, "reasons").</summary>
            public readonly List<string> reasons = new List<string>();
            public double Term(string name) => terms.Where(t => t.Key == name).Sum(t => t.Value);
        }

        /// <summary>The terms' names (§J's table).</summary>
        public static class Terms
        {
            public const string Trust = "trust", View = "view", Grudge = "grudge";
            public const string Pact = "pact", Bond = "bond", Saved = "saved", PutUp = "put-up", LeftUp = "left-up";
            public const string Broken = "broken-word", DoubleDealing = "double-dealing", RivalPact = "rival-pact", MatePact = "mate-pact";
            public const string SeenWithRival = "seen-with-rival", SeenWithAlly = "seen-with-ally";
            public const string Respect = "respect", Floater = "floater", TookTheVeto = "took-the-veto", ThrewIt = "threw-it", HeadToHead = "head-to-head";
            public const string Time = "time", Style = "style";
        }

        public const double PactBonus = 20, BondBonus = 15, NemesisCost = -25, BetrayedBondCost = -10;
        public const double SavedPerWeek = 10, SavedCap = 20;
        public const double PutUpPerWeek = -8, PutUpFaded = -4, PutUpCap = -24;
        public const int PutUpFadesAfterWeeks = 3;
        public const double LeftUpPerWeek = -5, LeftUpCap = -10;
        public const double BrokenPerHearing = -8, BrokenCap = -24, DoubleDealingCost = -10;
        public const double RivalPactCost = -10, MatePactBonus = 5, SeenWithRivalCost = -5, SeenWithAllyBonus = 3;
        public const int SeenWitnessMemories = 2;
        public const double RespectPerWin = 3, RespectCap = 9, FloaterCost = -5, TookTheVetoPerWeek = -5, TookTheVetoCap = -10;
        public const int FloaterFromWeek = 5, ThrewItWithinWeeks = 3;
        public const double ThrewItCost = -8, HeadToHeadEnvy = -3, HeadToHeadRespect = 2;
        public const double TimeCap = 5, StyleTerm = 5;
        public const double FearPerHoh = 8, FearPerVeto = 6, FearCap = 40;

        /// <summary>The prefix of the head-to-head records the engine writes from 12 (<c>h2h:{week}:{kind}:{runnerUpId}</c>, §F).</summary>
        public const string HeadToHeadPrefix = "h2h:";

        /// <summary>The houseguest's reading of the player; an empty reading for the player, nobody, or a season without one.</summary>
        public static Reading Of(EpisodeState s, string npcId)
        {
            var r = new Reading();
            var npc = s?.Find(npcId);
            var player = s?.Find(s.playerId);
            if (npc == null || player == null || npc.isPlayer) return r;

            double T = ThreatAssessment.TrustScore(s, s.playerId, npcId);
            double V = s.Score(npcId, s.playerId);
            double G = Grudges.Severity(s, npcId, s.playerId);
            Add(r, Terms.Trust, T);
            Add(r, Terms.View, 0.2 * V);
            Add(r, Terms.Grudge, -0.4 * G);
            r.fear = Math.Min(FearCap, FearPerHoh * player.hohWins + FearPerVeto * player.vetoWins);

            if (EpisodeEngine.StoryAt(s, StoryRules.Manners)) Manners(s, npc, player, r);

            r.faith = Math.Max(0, Math.Min(100, r.terms.Sum(t => t.Value)));
            return r;
        }

        /// <summary>Faith alone.</summary>
        public static double Faith(EpisodeState s, string npcId) => Of(s, npcId).faith;

        private static void Add(Reading r, string name, double value)
        {
            if (value != 0) r.terms.Add(new KeyValuePair<string, double>(name, value));
        }

        // ---------------------------------------------------------------- the rules-12 terms (S1)

        private static void Manners(EpisodeState s, ContestantState npc, ContestantState player, Reading r)
        {
            string id = npc.id, me = s.playerId;
            var axes = Personality.Of(npc);
            bool competitive = npc.traits.Contains("Competitive");

            // pact: a C6 ally.
            if (EpisodeEngine.SharesIntel(s, id)) Add(r, Terms.Pact, PactBonus);

            // bond: partner, confidant or ride-or-die; a nemesis; a betrayed bond.
            double bond = 0;
            if (Bonds.Partner(s, id, me) || Bonds.Holds(s, id, me, BondKinds.Confidant) || Bonds.Holds(s, id, me, BondKinds.RideOrDie)) bond += BondBonus;
            if (Bonds.Nemesis(s, id, me)) bond += NemesisCost;
            if (BondKinds.All.Any(kind => Bonds.Betrayed(s, id, me, kind))) bond += BetrayedBondCost;
            Add(r, Terms.Bond, bond);

            // The power record, a week once each: past weeks from their power rows, this week's live block where no row says it yet.
            var weeks = s.ledger.power.Select(p => p.week).Concat(new[] { s.week }).Distinct().OrderBy(w => w).ToList();
            double saved = 0, putUp = 0, leftUp = 0, tookTheVeto = 0;
            foreach (int w in weeks)
            {
                var row = s.ledger.power.LastOrDefault(p => p.week == w);
                bool hoh = w == s.week ? s.hohId == me : row?.hohId == me;
                // record: put them up (nominated, or the replacement), each week once, fading after three weeks.
                if (hoh && npc.nominationWeeks.Contains(w))
                    putUp += s.week - w >= PutUpFadesAfterWeeks ? PutUpFaded : PutUpPerWeek;
                if (row == null) continue;
                bool holder = row.vetoHolderId == me;
                if (!holder) continue;
                // record: saved them.
                if (row.vetoUsed && row.savedId == id) { saved += SavedPerWeek; continue; }
                // record: left them up - on the final block, not as the replacement, who went up only after the veto was used.
                var block = row.nominees != null && row.nominees.Count > 0 ? row.nominees : w == s.week ? s.nominees : new List<string>();
                if (block.Contains(id) && row.replacementId != id)
                {
                    leftUp += LeftUpPerWeek;
                    // comp: took the veto they needed.
                    if (competitive || axes.Vengeful >= 2) tookTheVeto += TookTheVetoPerWeek;
                }
            }
            Add(r, Terms.Saved, Math.Min(SavedCap, saved));
            Add(r, Terms.PutUp, Math.Max(PutUpCap, putUp));
            Add(r, Terms.LeftUp, Math.Max(LeftUpCap, leftUp));
            if (saved > 0) r.reasons.Add("Your veto saved them");
            foreach (int w in weeks.Where(w => (w == s.week ? s.hohId == me : s.ledger.power.LastOrDefault(p => p.week == w)?.hohId == me) && npc.nominationWeeks.Contains(w)))
                r.reasons.Add("You put them up in week " + w);
            if (tookTheVeto < 0) r.reasons.Add("You took the veto they needed");

            // ties: heard of a broken word.
            int heard = YourWord.Breaches(s).Count(f => YourWord.HeardBy(s, f).Contains(id));
            Add(r, Terms.Broken, Math.Max(BrokenCap, BrokenPerHearing * heard));
            if (heard > 0) r.reasons.Add(heard == 1 ? "They heard you broke your word" : "They heard you broke your word " + heard + " times");

            // ties: double-dealing, the permanent receipt on their view of the player.
            var edge = s.relationships.FirstOrDefault(e => e.fromId == id && e.toId == me);
            if (edge != null && edge.events.Any(e => e.type == StoryReceipts.DoubleDealt))
            {
                Add(r, Terms.DoubleDealing, DoubleDealingCost);
                r.reasons.Add("They know you kept another alliance from them");
            }

            // ties: a pact of yours they can see, with their rival or their pact-mate.
            var mine = s.alliances.Where(a => a.active && a.members.Contains(me) && Knowledge.AllianceVisibleTo(s, a, id)).ToList();
            if (mine.Any(a => AllianceLeaks.HasRival(s, id, a))) Add(r, Terms.RivalPact, RivalPactCost);
            if (mine.Any(a => a.members.Any(m => m != me && m != id && s.Allied(id, m)))) Add(r, Terms.MatePact, MatePactBonus);
            foreach (var pact in mine.Where(a => !a.members.Contains(id) && PlayerKnowsTheyKnow(s, a, id)))
            {
                var others = pact.members.Where(m => m != me).Select(m => s.Find(m)?.name).Where(n => n != null).ToList();
                if (others.Count > 0) r.reasons.Add("They know you're with " + string.Join(" and ", others.Select(FinalistRead.FirstName)));
            }

            // ties: seen with their rival, or their ally: two witnessed room acts with them this season, or an invite up this week.
            bool seenWithRival = false, seenWithAlly = false;
            foreach (var other in s.contestants.Where(c => c.id != id && c.id != me))
            {
                if (!SeenWith(s, id, other)) continue;
                if (IsRival(s, id, other.id)) seenWithRival = true;
                else if (s.Allied(id, other.id)) seenWithAlly = true;
            }
            if (seenWithRival) Add(r, Terms.SeenWithRival, SeenWithRivalCost);
            if (seenWithAlly) Add(r, Terms.SeenWithAlly, SeenWithAllyBonus);

            // comp: respect for a winner who never put them up.
            int wins = player.hohWins + player.vetoWins;
            if ((competitive || axes.Bold >= 2) && wins > 0 && putUp == 0) Add(r, Terms.Respect, Math.Min(RespectCap, RespectPerWin * wins));
            // comp: a floater.
            if ((competitive || axes.Bold >= 2 || npc.traits.Contains("Strategic")) && s.week >= FloaterFromWeek && wins == 0
                && player.nominationWeeks.Count == 0 && player.timesNominated == 0)
                Add(r, Terms.Floater, FloaterCost);
            Add(r, Terms.TookTheVeto, Math.Max(TookTheVetoCap, tookTheVeto));
            // comp: "threw it" - from the public placements only, never the private throw line.
            if ((competitive || npc.traits.Contains("Analytical") || npc.traits.Contains("Intuitive")) && wins >= 1 && ThrewIt(s, id))
                Add(r, Terms.ThrewIt, ThrewItCost);
            // comp: head to head, the runner-up to the player's win within three weeks.
            if (s.story.cooldowns.Any(c => c.untilWeek > s.week && c.key.StartsWith(HeadToHeadPrefix, StringComparison.Ordinal) && c.key.EndsWith(":" + id, StringComparison.Ordinal)))
            {
                if (competitive || axes.Vengeful >= 2) Add(r, Terms.HeadToHead, HeadToHeadEnvy);
                else if (axes.Steady >= 2) Add(r, Terms.HeadToHead, HeadToHeadRespect);
            }

            // time together: their own rapport with the player.
            var contact = s.story.contacts.FirstOrDefault(c => c.npcId == id);
            if (contact != null) Add(r, Terms.Time, Math.Min(TimeCap, Math.Floor(contact.rapport / 2.0)));

            // style: the player's top approach in the story cycles they were cast in.
            string top = TopStyle(s, id);
            if (top != null) Add(r, Terms.Style, StyleTerm * Personality.Reception(npc, top));
        }

        /// <summary>Whether the houseguest counts somebody a rival: a grudge or a score at the leak rules' rival lines.</summary>
        public static bool IsRival(EpisodeState s, string npcId, string otherId) =>
            Grudges.Severity(s, npcId, otherId) >= AllianceLeaks.RivalGrudge || s.Score(npcId, otherId) <= AllianceLeaks.RivalScore;

        /// <summary>
        /// Whether the houseguest saw the player with somebody: two or more witness memories of the player's room acts naming
        /// them this season (matched by the acts' fixed phrasing), or the player invited them up to the HoH room this week.
        /// </summary>
        public static bool SeenWith(EpisodeState s, string npcId, ContestantState other)
        {
            if (other == null) return false;
            string line = "Saw you stand up for " + other.name + ".";
            int seen = s.memories.Count(m => m.ownerId == npcId && m.subjectId == s.playerId && m.text == line);
            if (seen >= SeenWitnessMemories) return true;
            return s.story.cooldowns.Any(c => c.key == "invite:" + s.week + ":" + other.id && c.untilWeek > s.week);
        }

        /// <summary>
        /// Whether the player can know this houseguest knows of the pact: its fact is public, or it has no fact (the legacy
        /// rule in <see cref="Knowledge.AllianceVisibleTo"/>), or the double-dealing line told the player they found out.
        /// </summary>
        private static bool PlayerKnowsTheyKnow(EpisodeState s, AllianceState pact, string npcId)
        {
            var fact = Knowledge.Of(s, FactKinds.Alliance, pact.id);
            if (fact == null || fact.visibility == FactVisibility.Public) return true;
            var edge = s.relationships.FirstOrDefault(e => e.fromId == npcId && e.toId == s.playerId);
            return edge != null && edge.events.Any(e => e.type == StoryReceipts.DoubleDealt) && fact.knowers.Contains(npcId);
        }

        /// <summary>
        /// "Threw it": within the last three weeks the player, with a win to their name, finished in the bottom two of a veto
        /// the houseguest or their pact-mate needed (nominated that week), and the veto missed them.
        /// </summary>
        private static bool ThrewIt(EpisodeState s, string npcId)
        {
            foreach (var row in s.ledger.competitions.Where(c => c.kind == EpisodePhase.Veto.ToString() && c.placement > 0
                         && c.field >= 3 && c.placement >= c.field - 1 && s.week - c.week < ThrewItWithinWeeks))
            {
                var needed = s.contestants.Where(c => (c.id == npcId || s.Allied(npcId, c.id)) && c.nominationWeeks.Contains(row.week)).Select(c => c.id).ToList();
                if (needed.Count == 0) continue;
                var power = s.ledger.power.LastOrDefault(p => p.week == row.week);
                string holder = power?.vetoHolderId ?? (row.week == s.week ? s.vetoHolderId : null);
                string savedId = power != null && power.vetoUsed ? power.savedId : null;
                if (holder == null || needed.Contains(holder) || (savedId != null && needed.Contains(savedId))) continue;
                return true;
            }
            return false;
        }

        /// <summary>The player's most-taken approach in the story cycles the houseguest was cast in; null when none was taken.</summary>
        public static string TopStyle(EpisodeState s, string npcId)
        {
            var tally = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var cycle in s.storylines.Where(c => c.cast != null && c.cast.Any(x => x.contestantId == npcId)))
            {
                var template = StoryCatalog.Find(cycle.templateId);
                if (template == null || cycle.path == null) continue;
                foreach (var step in cycle.path)
                {
                    string approach = template.Beat(step.beatId)?.Option(step.optionId)?.approach;
                    if (!Personality.Approach.IsKnown(approach)) continue;
                    tally[approach] = tally.TryGetValue(approach, out int n) ? n + 1 : 1;
                }
            }
            if (tally.Count == 0) return null;
            int most = tally.Values.Max();
            return Personality.Approach.All.First(a => tally.TryGetValue(a, out int n) && n == most);
        }
    }
}
