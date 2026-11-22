using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>One trivia question: its words, its choices and which choice is right.</summary>
    public sealed class TriviaQuestion
    {
        /// <summary>The fact the question asks about, as a key ("hoh-2", "evicted-3"): no two questions of a quiz ask the same one.</summary>
        public string Fact { get; }

        /// <summary>The week the fact is from.</summary>
        public int Week { get; }

        public string Text { get; }
        public IReadOnlyList<string> Choices { get; }

        /// <summary>The index of the right choice in <see cref="Choices"/>.</summary>
        public int Answer { get; }

        public string AnswerText => Choices[Answer];

        public TriviaQuestion(string fact, int week, string text, IReadOnlyList<string> choices, int answer)
        { Fact = fact; Week = week; Text = text; Choices = choices; Answer = answer; }
    }

    /// <summary>
    /// The trivia competition (competition rules 6): when a season may deal it, what it asks and how the player's answers
    /// become their result.
    ///
    /// <para><b>Only what the player saw.</b> Every question is made from the season ledger's power rows
    /// (<see cref="PowerRow"/>) of the weeks before this one: who won Head of Household and the Power of Veto, whom the
    /// veto saved and who went up in their place, who sat on the block on eviction night, who was evicted, by the count
    /// the eviction line showed, and in what order. Each of those was shown on screen at a ceremony. Nothing else is
    /// read: no ballot (who voted for whom is the voter's own), no alliance, no claim, no standing, no plan, no backdoor
    /// target, no relationship, no event line. The only other things read are the cast's names and statuses and the
    /// season's seed. The questions are about this game and nothing else.</para>
    ///
    /// <para><b>Only further along in a season</b> (the owner's ruling of 21 November 2026): from week
    /// <see cref="EarliestWeek"/>, after <see cref="EvictionsFirst"/> evictions, and only when <see cref="FactsNeeded"/>
    /// different facts can be asked about, twice the <see cref="Questions"/> a quiz asks. A season that never gets there
    /// never deals it.</para>
    ///
    /// <para><b>Fixed by the season.</b> A competition's questions and the order of their choices are drawn from a
    /// generator keyed to the season's seed, the week and the phase, never from the season's own generator; the facts
    /// come from the saved ledger. The same competition asks the same quiz from its briefing to its commit and after a
    /// save and reload, and no field is saved for it.</para>
    ///
    /// <para><b>The housemates' results</b> are rolled as every mental competition's are (rules 4's weighted runner:
    /// mental six tenths, luck two, endurance and social one each, times the roll): a good memory is the mental
    /// statistic. <b>The player's result</b> is the share of questions answered right (<see cref="Performance"/>), which
    /// the engine weighs as it weighs every minigame's; an equal score goes to the earlier entry in the field, as in
    /// every competition.</para>
    /// </summary>
    public static class CompetitionTrivia
    {
        /// <summary>The first week a trivia competition may be dealt in.</summary>
        public const int EarliestWeek = 4;

        /// <summary>The evictions that must have happened before it.</summary>
        public const int EvictionsFirst = 3;

        /// <summary>The questions one competition asks.</summary>
        public const int Questions = 5;

        /// <summary>The different facts the season must hold before it is dealt: twice the questions.</summary>
        public const int FactsNeeded = 10;

        /// <summary>The choices a question offers at most, and the fewest it is asked with.</summary>
        public const int MostChoices = 4, FewestChoices = 3;

        /// <summary>The choice that says the veto was kept.</summary>
        public const string NobodyChoice = "Nobody: the veto was not used";

        /// <summary>Whether this competition may be the trivia: the season's own week.</summary>
        public static bool Eligible(EpisodeState state) => state != null && Eligible(state, state.week);

        /// <summary>
        /// The rule, in one place: competition rules 6 or later, week <see cref="EarliestWeek"/> or later,
        /// <see cref="EvictionsFirst"/> evictions in the weeks before it, and <see cref="FactsNeeded"/> different facts
        /// to ask about. Reads only the weeks before <paramref name="week"/>, so the answer cannot change during it.
        /// </summary>
        public static bool Eligible(EpisodeState state, int week)
        {
            if (state?.ledger?.power == null || state.contestants == null) return false;
            if (state.competitionRulesVersion < CompetitionRules.Trivia || week < EarliestWeek) return false;
            if (Rows(state, week).Count(row => !string.IsNullOrEmpty(row.evicteeId)) < EvictionsFirst) return false;
            return Facts(state, week, titles: false).Select(fact => fact.Key).Distinct().Count() >= FactsNeeded;
        }

        /// <summary>How many different facts the weeks before <paramref name="week"/> can be asked about.</summary>
        public static int FactCount(EpisodeState state, int week) =>
            state?.ledger?.power == null || state.contestants == null ? 0
            : Facts(state, week, titles: false).Select(fact => fact.Key).Distinct().Count();

        /// <summary>This competition's quiz: empty when it may not be the trivia.</summary>
        public static IReadOnlyList<TriviaQuestion> Quiz(EpisodeState state) =>
            state == null ? Array.Empty<TriviaQuestion>() : Quiz(state, state.week, state.phase);

        /// <summary>The quiz of the competition at <paramref name="week"/> and <paramref name="phase"/>: <see cref="Questions"/> questions, each about a different fact.</summary>
        public static IReadOnlyList<TriviaQuestion> Quiz(EpisodeState state, int week, EpisodePhase phase)
        {
            if (!Eligible(state, week)) return Array.Empty<TriviaQuestion>();
            var draw = new SeededRandom(SeededRandom.HashSeed(state.seed.ToString(CultureInfo.InvariantCulture)
                + "/" + week.ToString(CultureInfo.InvariantCulture) + "/" + phase + "/trivia-v6"));
            var picked = new List<Candidate>();
            var asked = new HashSet<string>();
            var given = new HashSet<string>();
            foreach (var fact in draw.Shuffle(Facts(state, week, titles: true)))
            {
                if (picked.Count == Questions) break;
                // No two questions about one fact, and none whose words give another's answer away.
                if (asked.Contains(fact.Key) || given.Contains(fact.Key) || fact.Gives.Any(asked.Contains)) continue;
                picked.Add(fact); asked.Add(fact.Key); given.UnionWith(fact.Gives);
            }
            var quiz = new List<TriviaQuestion>();
            foreach (var fact in picked)
            {
                List<string> choices;
                if (fact.Fixed != null) choices = fact.Fixed.ToList();
                else
                {
                    choices = new List<string> { fact.Answer };
                    foreach (var wrong in fact.Near.Concat(draw.Shuffle(fact.Others)))
                    {
                        if (choices.Count == MostChoices) break;
                        if (!choices.Contains(wrong)) choices.Add(wrong);
                    }
                }
                var shown = draw.Shuffle(choices);
                quiz.Add(new TriviaQuestion(fact.Key, fact.Week, fact.Text, Array.AsReadOnly(shown), Array.IndexOf(shown, fact.Answer)));
            }
            return quiz.AsReadOnly();
        }

        /// <summary>
        /// The right answers among <paramref name="chosen"/>: one index into each question's choices, in the quiz's order.
        /// A question left unanswered (a missing entry, -1, or an index out of range) is not right.
        /// </summary>
        public static int RightAnswers(IReadOnlyList<TriviaQuestion> quiz, IReadOnlyList<int> chosen)
        {
            if (quiz == null || chosen == null) return 0;
            int right = 0;
            for (int i = 0; i < quiz.Count && i < chosen.Count; i++)
                if (chosen[i] == quiz[i].Answer) right++;
            return right;
        }

        /// <summary>The player's performance, 0 to 1: the share of the questions answered right, every question worth the same.</summary>
        public static double Performance(IReadOnlyList<TriviaQuestion> quiz, IReadOnlyList<int> chosen) =>
            quiz == null || quiz.Count == 0 ? 0 : RightAnswers(quiz, chosen) / (double)quiz.Count;

        // ---------------------------------------------------------------- the facts

        /// <summary>A question before its choices are dealt.</summary>
        private sealed class Candidate
        {
            public string Key, Text, Answer;
            public int Week;
            /// <summary>Wrong choices that belong first (the other nominee for an evictee); then <see cref="Others"/>, shuffled.</summary>
            public List<string> Near = new List<string>();
            public List<string> Others = new List<string>();
            /// <summary>A question whose choices are always these (the veto's: the two nominees and nobody).</summary>
            public string[] Fixed;
            /// <summary>The facts the question's own words give away.</summary>
            public string[] Gives = Array.Empty<string>();
        }

        /// <summary>The ledger's power rows of the weeks before <paramref name="week"/>, one a week (the last), in week order.</summary>
        private static List<PowerRow> Rows(EpisodeState state, int week) =>
            state.ledger.power.Where(row => row != null && row.week >= 1 && row.week < week)
                .GroupBy(row => row.week).Select(rows => rows.Last()).OrderBy(row => row.week).ToList();

        private static List<Candidate> Facts(EpisodeState state, int week, bool titles)
        {
            var facts = new List<Candidate>();
            var rows = Rows(state, week);
            string Name(string id)
            {
                if (string.IsNullOrEmpty(id)) return null;
                var name = state.Find(id)?.name;
                return string.IsNullOrWhiteSpace(name) ? null : name;
            }
            // Who was in the house in a week: everyone not evicted in an earlier week, and never anyone expelled.
            List<string> InHouse(int at) => state.contestants
                .Where(c => c != null && c.status != ContestantStatus.Expelled && Name(c.id) != null)
                .Where(c => !rows.Any(row => row.week < at && row.evicteeId == c.id))
                .Select(c => c.id).ToList();
            void Add(Candidate fact)
            {
                if (fact.Answer == null) return;
                fact.Near = fact.Near.Where(n => n != null && n != fact.Answer).Distinct().ToList();
                fact.Others = fact.Others.Where(n => n != null && n != fact.Answer && !fact.Near.Contains(n)).Distinct().ToList();
                int choices = fact.Fixed?.Distinct().Count() ?? 1 + fact.Near.Count + fact.Others.Count;
                if (choices >= FewestChoices) facts.Add(fact);
            }
            string Played(int at, EpisodePhase phase)
            {
                if (!titles) return null;
                var dealt = CompetitionDefinitions.Version6(state, at, phase);
                return dealt?.Title;
            }
            List<string> Names(IEnumerable<string> ids) => ids.Select(Name).Where(n => n != null).ToList();

            bool ordered = state.ledger.power.Count < SeasonLedger.MostRows;
            int evictions = 0;
            foreach (var row in rows)
            {
                int w = row.week;
                string wk = w.ToString(CultureInfo.InvariantCulture);
                var house = InHouse(w);
                if (Name(row.hohId) != null)
                {
                    string title = Played(w, EpisodePhase.HoH);
                    Add(new Candidate
                    {
                        Key = "hoh-" + wk, Week = w, Answer = Name(row.hohId),
                        Text = title != null ? "Who won " + title + ", the Head of Household competition in week " + wk + "?"
                            : "Who won Head of Household in week " + wk + "?",
                        Others = Names(house.Where(id => id != row.hohId)),
                    });
                }
                if (Name(row.vetoHolderId) != null)
                {
                    string title = Played(w, EpisodePhase.Veto);
                    Add(new Candidate
                    {
                        Key = "veto-" + wk, Week = w, Answer = Name(row.vetoHolderId),
                        Text = title != null ? "Who won " + title + ", the Power of Veto competition in week " + wk + "?"
                            : "Who won the Power of Veto in week " + wk + "?",
                        Others = Names(house.Where(id => id != row.vetoHolderId)),
                    });
                }
                var block = row.nominees?.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList() ?? new List<string>();
                bool regular = block.Count == 2 && row.nominees.Count == 2 &&!string.IsNullOrEmpty(row.evicteeId) && block.Contains(row.evicteeId);
                // The veto meeting: the block before it, and whom the veto took off it, or nobody.
                if (regular && Name(row.vetoHolderId) != null)
                {
                    List<string> before = null;
                    if (!row.vetoUsed) before = block;
                    else if (!string.IsNullOrEmpty(row.savedId) && !string.IsNullOrEmpty(row.replacementId)
                        && block.Contains(row.replacementId) && !block.Contains(row.savedId))
                        before = block.Select(id => id == row.replacementId ? row.savedId : id).ToList();
                    if (before != null && before.All(id => Name(id) != null))
                    {
                        string saved = row.vetoUsed ? Name(row.savedId) : NobodyChoice;
                        Add(new Candidate
                        {
                            Key = "saved-" + wk, Week = w, Answer = saved,
                            Text = "Who did the Power of Veto save in week " + wk + "?",
                            Fixed = Names(before).Concat(new[] { NobodyChoice }).ToArray(),
                        });
                    }
                    if (row.vetoUsed && before != null && Name(row.replacementId) != null)
                        Add(new Candidate
                        {
                            Key = "replacement-" + wk, Week = w, Answer = Name(row.replacementId),
                            Text = "Who went on the block as the replacement nominee in week " + wk + "?",
                            Others = Names(house.Where(id => id != row.replacementId && id != row.hohId && id != row.vetoHolderId && id != row.savedId)),
                        });
                }
                if (Name(row.evicteeId) == null) continue;
                evictions++;
                string evicted = "evicted-" + wk;
                var partner = regular ? block.First(id => id != row.evicteeId) : null;
                Add(new Candidate
                {
                    Key = evicted, Week = w, Answer = Name(row.evicteeId),
                    Text = "Who was evicted in week " + wk + "?",
                    Near = Names(new[] { partner }),
                    Others = Names(house.Where(id => id != row.evicteeId && id != row.hohId)),
                });
                if (ordered && evictions <= Ordinals.Length)
                    Add(new Candidate
                    {
                        Key = evicted, Week = w, Answer = Name(row.evicteeId),
                        Text = "Who was the " + Ordinals[evictions - 1] + " houseguest evicted this season?",
                        // The houseguests evicted just before and just after are the near misses.
                        Near = Names(rows.Where(other => Math.Abs(other.week - w) == 1).Select(other => other.evicteeId)),
                        Others = Names(house.Where(id => id != row.evicteeId && id != row.hohId)),
                    });
                if (partner != null && Name(partner) != null)
                    Add(new Candidate
                    {
                        Key = "block-" + wk, Week = w, Answer = Name(partner),
                        Text = "Who sat on the block beside " + Name(row.evicteeId) + " on eviction night in week " + wk + "?",
                        Others = Names(house.Where(id => id != row.evicteeId && id != partner && id != row.hohId)),
                        Gives = new[] { evicted },
                    });
                // The count the eviction line showed ("By a vote of 4 to 2"); a single vote shows no count.
                if (regular && row.tally != null && row.tally.Count == 2)
                {
                    int against = row.tally[block.IndexOf(row.evicteeId)], others = row.tally[1 - block.IndexOf(row.evicteeId)];
                    if (against >= others && others >= 0 && against + others >= 2)
                    {
                        int total = against + others;
                        Add(new Candidate
                        {
                            Key = "vote-" + wk, Week = w, Answer = Count(against, others),
                            Text = "By what vote was " + Name(row.evicteeId) + " evicted in week " + wk + "?",
                            Near = Splits(total).Where(split => split != Count(against, others)).ToList(),
                            Others = Splits(total + 1).Concat(total > 2 ? Splits(total - 1) : Enumerable.Empty<string>()).ToList(),
                            Gives = new[] { evicted },
                        });
                    }
                }
            }
            return facts;
        }

        private static readonly string[] Ordinals =
            { "first", "second", "third", "fourth", "fifth", "sixth", "seventh", "eighth", "ninth", "tenth", "eleventh", "twelfth",
              "thirteenth", "fourteenth", "fifteenth", "sixteenth" };

        private static string Count(int against, int others) =>
            against.ToString(CultureInfo.InvariantCulture) + " to " + others.ToString(CultureInfo.InvariantCulture);

        /// <summary>Every count a vote of <paramref name="total"/> could have evicted by, most lopsided first.</summary>
        private static IEnumerable<string> Splits(int total)
        {
            for (int against = total; against * 2 >= total; against--) yield return Count(against, total - against);
        }
    }
}
