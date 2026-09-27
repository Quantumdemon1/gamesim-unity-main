using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The Have-Nots, and the veto's prizes and punishments: two Big Brother traditions neither build
    /// of the reference has, so the design is this port's.
    ///
    /// <para><b>Have-Nots.</b> The last out of each Head of Household competition live on slop and
    /// cold showers until the next one: three of them in a house of nine or more, two in six to
    /// eight, one in five, and none from the final four. They are tired - a point off their score in
    /// the week's veto - and a player among them has one fewer conversation. Finishing low has a
    /// cost now, and a throw is a gamble with it.</para>
    ///
    /// <para><b>The veto's prizes.</b> The veto competition's runner-up wins a prize and its last
    /// finisher takes a punishment, from short lists the season's seed picks between each week:
    /// cash, a pass on next week's Have-Nots, or a luxury night (a conversation more); a place among
    /// next week's Have-Nots, an alarm every hour of the night (a conversation fewer), or a costume
    /// until the eviction. A conversation is the player's currency, so the luxury and the alarm
    /// change only a player's week; for anyone else they are the house's story.</para>
    ///
    /// <para>Nothing here draws on the season's generator: the Have-Nots come from committed
    /// standings, and each week's prize and punishment from a hash of the seed and the week. A
    /// season plays them from <see cref="EpisodeState.haveNotRulesStartWeek"/>; 0 is a season
    /// that never does.</para>
    /// </summary>
    public static class HaveNots
    {
        /// <summary>What a Have-Not loses off their veto score: slop and cold showers take it out of you.</summary>
        public const double VetoPenalty = 1;

        public const string EventKind = "have-nots", PrizeEventKind = "veto-prize";

        /// <summary>Whether this season plays Have-Nots and veto prizes in the week it is in.</summary>
        public static bool Apply(EpisodeState s) => s != null && s.haveNotRulesStartWeek > 0 && s.week >= s.haveNotRulesStartWeek;

        /// <summary>How many Have-Nots a house of this size names: none from the final four.</summary>
        public static int Count(int active) => active >= 9 ? 3 : active >= 6 ? 2 : active >= 5 ? 1 : 0;

        /// <summary>Whether a houseguest is one of this week's Have-Nots.</summary>
        public static bool Is(EpisodeState s, string id) =>
            s != null && id != null && s.haveNots.Contains(id) && s.Find(id)?.status == ContestantStatus.Active;

        /// <summary>This week's Have-Nots still in the house, in the order they were named.</summary>
        public static IEnumerable<string> Current(EpisodeState s) =>
            s == null ? Enumerable.Empty<string>() : s.haveNots.Where(id => Is(s, id));

        /// <summary>What a Have-Not loses in this competition: the veto's point, and nothing anywhere else.</summary>
        public static double Penalty(EpisodeState s, string id) =>
            s != null && s.phase == EpisodePhase.Veto && Is(s, id) ? VetoPenalty : 0;

        /// <summary>The conversations a player loses while they are a Have-Not.</summary>
        public static int ActionCost(EpisodeState s) => Is(s, s?.playerId) ? 1 : 0;

        /// <summary>
        /// Names the week's Have-Nots from the Head of Household competition just committed: the
        /// lowest scores, the new Head of Household and anyone holding a pass excepted, and anyone
        /// the veto's punishment sent. Replaces last week's; clears the passes and punishments it used.
        /// </summary>
        public static void Assign(EpisodeState s)
        {
            if (s == null || s.phase != EpisodePhase.HoH) return;
            s.haveNots.Clear();
            if (!Apply(s)) { s.haveNotPasses.Clear(); s.punishedHaveNots.Clear(); return; }
            int count = Count(s.Active.Count());
            var chosen = new List<string>();
            if (count > 0)
            {
                var passes = new HashSet<string>(s.haveNotPasses, StringComparer.Ordinal);
                // Lowest first; on a tie the earlier entry, as the winner's own order breaks ties.
                chosen.AddRange(s.competitionScores.OrderBy(score => score.score).Select(score => score.contestantId)
                    .Where(id => id != s.hohId && !passes.Contains(id) && s.Find(id)?.status == ContestantStatus.Active)
                    .Take(count));
                foreach (var id in s.punishedHaveNots)
                    if (id != s.hohId && !chosen.Contains(id) && s.Find(id)?.status == ContestantStatus.Active) chosen.Add(id);
            }
            s.haveNots.AddRange(chosen);
            s.haveNotPasses.Clear();
            s.punishedHaveNots.Clear();
            if (chosen.Count == 0) return;
            EpisodeEngine.Log(s, EventKind, "This week's Have-Nots: " + Names(s, chosen)
                + ". Slop, cold showers and the Have-Not room until the next Head of Household, and a point off their veto.");
        }

        /// <summary>
        /// Hands out the veto competition's prize and punishment from its committed standings: the
        /// runner-up's prize in a field of three or more, the last finisher's punishment in four or more.
        /// </summary>
        public static void AwardVetoPrizes(EpisodeState s)
        {
            if (s == null || s.phase != EpisodePhase.Veto || !Apply(s)) return;
            var ordered = s.competitionScores.OrderByDescending(score => score.score).Select(score => score.contestantId).ToList();
            if (ordered.Count >= 3) Award(s, ordered[1], PrizeFor(s.seed, s.week));
            if (ordered.Count >= 4) Award(s, ordered[ordered.Count - 1], PunishmentFor(s.seed, s.week));
        }

        private static void Award(EpisodeState s, string id, Prize prize)
        {
            s.vetoPrizes.Add(new VetoPrizeState { week = s.week, contestantId = id, prizeId = prize.Id });
            bool player = id == s.playerId;
            switch (prize.Id)
            {
                case PassId: if (!s.haveNotPasses.Contains(id)) s.haveNotPasses.Add(id); break;
                case PunishedId: if (!s.punishedHaveNots.Contains(id)) s.punishedHaveNots.Add(id); break;
                case LuxuryId:
                case AlarmId:
                    if (player) s.activeModifiers.Add(new StoryModifierState
                    {
                        id = prize.Id + "-week-" + s.week.ToString(CultureInfo.InvariantCulture), name = prize.Title, description = prize.Effect,
                        weeksLeft = 2, socialBonus = prize.Id == LuxuryId ? StoryModifiers.PointsPerAction : -StoryModifiers.PointsPerAction,
                    });
                    break;
            }
            string name = player ? "You" : Name(s, id);
            EpisodeEngine.Log(s, PrizeEventKind, prize.Punishment
                ? name + " finished last in the veto" + (player ? ": " : " and takes a punishment: ") + prize.Line + "."
                : name + " came second in the veto" + (player ? " and won " : " and won ") + prize.Line + ".");
        }

        // ---------------------------------------------------------------- the lists

        public const string CashId = "cash", PassId = "have-not-pass", LuxuryId = "luxury-night";
        public const string PunishedId = "have-not", AlarmId = "the-alarm", CostumeId = "costume";

        /// <summary>A prize or a punishment: its words in the house's story, and what it does.</summary>
        public sealed class Prize
        {
            public string Id { get; }
            public string Title { get; }
            /// <summary>The words after "won" or the punishment's colon in the house's story.</summary>
            public string Line { get; }
            /// <summary>What it does, in the player's words, where it does anything.</summary>
            public string Effect { get; }
            public bool Punishment { get; }
            internal Prize(string id, string title, string line, string effect, bool punishment)
            { Id = id; Title = title; Line = line; Effect = effect; Punishment = punishment; }
        }

        public static readonly Prize[] Prizes =
        {
            new Prize(CashId, "$5,000", "$5,000", "A cheque for $5,000. It buys nothing in the house.", false),
            new Prize(PassId, "Have-Not pass", "a Have-Not pass", "Safe from next week's Have-Nots.", false),
            new Prize(LuxuryId, "Luxury night", "a luxury night out of the house", "One more conversation until the end of next week.", false),
        };

        public static readonly Prize[] Punishments =
        {
            new Prize(PunishedId, "Have-Not", "a Have-Not next week, whatever the competition says", "A Have-Not next week, whatever the competition says.", true),
            new Prize(AlarmId, "The alarm", "an alarm every hour of the night", "One fewer conversation until the end of next week.", true),
            new Prize(CostumeId, "The costume", "a banana suit until the eviction", "A banana suit until the eviction.", true),
        };

        public static Prize Find(string id) => Prizes.Concat(Punishments).FirstOrDefault(prize => prize.Id == id);

        /// <summary>This week's prize: picked from the season's seed and the week, spending none of its generator.</summary>
        public static Prize PrizeFor(uint seed, int week) => Prizes[Pick(seed, week, "veto-prize", Prizes.Length)];

        /// <summary>This week's punishment, the same way.</summary>
        public static Prize PunishmentFor(uint seed, int week) => Punishments[Pick(seed, week, "veto-punishment", Punishments.Length)];

        private static int Pick(uint seed, int week, string salt, int count) =>
            (int)(SeededRandom.HashSeed(seed.ToString(CultureInfo.InvariantCulture) + "/" + week.ToString(CultureInfo.InvariantCulture) + "/" + salt) % (uint)count);

        private static string Name(EpisodeState s, string id) => s.Find(id)?.name ?? "Unknown housemate";

        private static string Names(EpisodeState s, IList<string> ids)
        {
            var names = ids.Select(id => id == s.playerId ? "you" : Name(s, id)).ToList();
            return names.Count == 1 ? names[0] : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[names.Count - 1];
        }
    }

    /// <summary>One prize or punishment the veto handed out: who, when, and which.</summary>
    [Serializable]
    public sealed class VetoPrizeState
    {
        public int week;
        public string contestantId, prizeId;
        public VetoPrizeState Clone() => (VetoPrizeState)MemberwiseClone();
    }
}
