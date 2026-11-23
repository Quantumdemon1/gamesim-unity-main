using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Competition rules 6, the trivia competition: when a season may deal it (only further along, the owner's ruling of
    /// 21 November 2026), what it asks (only what the player was shown), how the player's answers become their result,
    /// that it is the same quiz after a save and reload, and that a season under rules 5 deals and plays exactly what it
    /// did. Unity-free, so the subset runs it.
    /// </summary>
    public sealed class CompetitionTriviaTests
    {
        private static readonly JsonSerializerSettings Reload = new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace };

        private static readonly string[] Cast = { "Avery", "Blair", "Casey", "Dana", "Ellis", "Flynn", "Gray", "Harper", "Indy", "Jules" };

        /// <summary>
        /// A season at <paramref name="week"/> with the ledger's power rows for weeks 1 to 3 (all public: the HoH, the
        /// veto, the meeting, the block, the evictee, the count). Week 2's veto was used on the player and Gray went up.
        /// </summary>
        private static EpisodeState Built(int week, int rules = CompetitionRules.Trivia, uint seed = 77)
        {
            var s = new EpisodeState { seed = seed, week = week, phase = EpisodePhase.HoH, competitionRulesVersion = rules, playerId = "c0" };
            for (int i = 0; i < Cast.Length; i++)
                s.contestants.Add(new ContestantState { id = "c" + i, name = Cast[i], isPlayer = i == 0, status = ContestantStatus.Active });
            Row(s, 1, "c1", "c4", false, null, null, new[] { "c9", "c6" }, new[] { 4, 1 }, "c9");
            Row(s, 2, "c2", "c5", true, "c0", "c6", new[] { "c8", "c6" }, new[] { 3, 2 }, "c8");
            Row(s, 3, "c3", "c4", false, null, null, new[] { "c5", "c7" }, new[] { 0, 5 }, "c7");
            return s;
        }

        private static PowerRow Row(EpisodeState s, int week, string hoh, string veto, bool used, string saved, string replacement,
            string[] block, int[] tally, string evictee)
        {
            var row = new PowerRow
            {
                week = week, hohId = hoh, vetoHolderId = veto, vetoUsed = used, savedId = saved, replacementId = replacement,
                nominees = block.ToList(), tally = tally.ToList(), evicteeId = evictee,
            };
            s.ledger.power.Add(row);
            if (evictee != null) s.Find(evictee).status = ContestantStatus.Jury;
            return row;
        }

        private static string Lines(IEnumerable<TriviaQuestion> quiz) =>
            string.Join("\n", quiz.Select(q => q.Fact + "|" + q.Week + "|" + q.Text + "|" + string.Join(";", q.Choices) + "|" + q.Answer));

        // ---------------------------------------------------------------- only further along in a season

        [Test]
        public void TheRuleIsWeekFourThreeEvictionsAndTenFacts()
        {
            Assert.That((CompetitionTrivia.EarliestWeek, CompetitionTrivia.EvictionsFirst, CompetitionTrivia.Questions, CompetitionTrivia.FactsNeeded),
                Is.EqualTo((4, 3, 5, 10)));
            Assert.That(CompetitionRules.Trivia, Is.EqualTo(6));
            Assert.That(CompetitionRules.Current, Is.EqualTo(CompetitionRules.Trivia), "Enrolled since the trivia's switch-on: new seasons start on rules 6.");
            Assert.That(CompetitionRules.Newest, Is.EqualTo(CompetitionRules.Trivia));

            var ready = Built(4);
            Assert.That(CompetitionTrivia.FactCount(ready, 4), Is.EqualTo(19), "weeks 1 and 3: 6 facts each; week 2 adds the replacement");
            Assert.That(CompetitionTrivia.Eligible(ready), Is.True);
            Assert.That(CompetitionTrivia.Eligible(Built(3)), Is.False, "Never before week 4.");
            Assert.That(CompetitionTrivia.Eligible(Built(4, CompetitionRules.RingTower)), Is.False, "Never under rules 5.");

            var twoGone = Built(4);
            twoGone.ledger.power[2].evicteeId = null;
            Assert.That(CompetitionTrivia.Eligible(twoGone), Is.False, "Never before three evictions.");

            var thin = Built(4);
            foreach (var row in thin.ledger.power) { row.hohId = null; row.vetoHolderId = null; row.nominees.Clear(); row.tally.Clear(); }
            Assert.That(CompetitionTrivia.FactCount(thin, 4), Is.EqualTo(3), "Only the three evictions are left to ask about.");
            Assert.That(CompetitionTrivia.Eligible(thin), Is.False, "Never with fewer than ten facts.");
            Assert.That(CompetitionTrivia.Quiz(thin), Is.Empty);
        }

        [Test]
        public void RulesSixDealsTriviaOnlyInPlaceOfALateMemoryBoard()
        {
            int trivia = 0;
            for (uint seed = 1; seed <= 300; seed++)
                for (int week = 1; week <= 12; week++)
                    foreach (var phase in new[] { EpisodePhase.HoH, EpisodePhase.Veto, EpisodePhase.FinalHoHPart1, EpisodePhase.FinalHoHPart2, EpisodePhase.FinalHoHPart3 })
                    {
                        var five = CompetitionDefinitions.Version5(seed, week, phase);
                        var state = Built(week, CompetitionRules.Trivia, seed);
                        state.phase = phase;
                        var six = CompetitionDefinitions.For(state);
                        bool board = five == CompetitionDefinitions.HouseMemory || five == CompetitionDefinitions.FirstImpressions;
                        bool weekly = phase == EpisodePhase.HoH || phase == EpisodePhase.Veto;
                        if (board && weekly && week >= 4) { Assert.That(six, Is.SameAs(CompetitionDefinitions.Trivia), seed + "/" + week + "/" + phase); trivia++; }
                        else Assert.That(six, Is.SameAs(five), seed + "/" + week + "/" + phase + ": rules 5's game");
                        state.competitionRulesVersion = CompetitionRules.RingTower;
                        Assert.That(CompetitionDefinitions.For(state), Is.SameAs(five), "A rules-5 season never deals it.");
                    }
            Assert.That(trivia, Is.GreaterThan(0));
            Assert.That(CompetitionDefinitions.Trivia.Category, Is.EqualTo(CompetitionRules.Mental), "Rolled as a mental game.");
            Assert.That(CompetitionDefinitions.All.Contains(CompetitionDefinitions.Trivia), Is.False, "Not a console game.");
            Assert.That(CompetitionDefinitions.Catalogue.Last(), Is.SameAs(CompetitionDefinitions.Trivia));
            Assert.That(CompetitionDefinitions.Catalogue.Select(d => d.Id).Distinct().Count(), Is.EqualTo(CompetitionDefinitions.Catalogue.Count));
        }

        [Test]
        public void TheValidationTakesRulesSixAndRefusesSeven()
        {
            var state = ContentCatalog.Create(9);
            state.competitionRulesVersion = CompetitionRules.Trivia;
            Assert.That(EpisodeValidation.TryValidate(state, out var reason), Is.True, reason);
            state.competitionRulesVersion = CompetitionRules.Trivia + 1;
            Assert.That(EpisodeValidation.TryValidate(state, out _), Is.False);
        }

        // ---------------------------------------------------------------- what it asks

        [Test]
        public void AQuizAsksFiveDifferentPublicFactsWithTheirRightAnswers()
        {
            var expected = new Dictionary<string, string>
            {
                ["hoh-1"] = "Blair", ["hoh-2"] = "Casey", ["hoh-3"] = "Dana", ["veto-1"] = "Ellis", ["veto-2"] = "Flynn", ["veto-3"] = "Ellis",
                ["saved-1"] = CompetitionTrivia.NobodyChoice, ["saved-2"] = "Avery", ["saved-3"] = CompetitionTrivia.NobodyChoice,
                ["replacement-2"] = "Gray", ["evicted-1"] = "Jules", ["evicted-2"] = "Indy", ["evicted-3"] = "Harper",
                ["block-1"] = "Gray", ["block-2"] = "Gray", ["block-3"] = "Flynn", ["vote-1"] = "4 to 1", ["vote-2"] = "3 to 2", ["vote-3"] = "5 to 0",
            };
            var kinds = new HashSet<string>();
            for (uint seed = 1; seed <= 200; seed++)
                foreach (var phase in new[] { EpisodePhase.HoH, EpisodePhase.Veto })
                {
                    var state = Built(5, CompetitionRules.Trivia, seed);
                    state.phase = phase;
                    var quiz = CompetitionTrivia.Quiz(state);
                    Assert.That(quiz.Count, Is.EqualTo(CompetitionTrivia.Questions));
                    Assert.That(quiz.Select(q => q.Fact).Distinct().Count(), Is.EqualTo(quiz.Count), "One question a fact.");
                    foreach (var q in quiz)
                    {
                        Assert.That(expected.ContainsKey(q.Fact), Is.True, q.Fact);
                        Assert.That(q.AnswerText, Is.EqualTo(expected[q.Fact]), q.Text);
                        Assert.That(q.Choices.Count, Is.InRange(CompetitionTrivia.FewestChoices, CompetitionTrivia.MostChoices), q.Text);
                        Assert.That(q.Choices.Distinct().Count(), Is.EqualTo(q.Choices.Count), q.Text);
                        Assert.That(q.Week, Is.InRange(1, 3));
                        kinds.Add(q.Fact.Split('-')[0] + (q.Text.Contains("houseguest evicted this season") ? "-order" : ""));
                        // A question that names who was evicted in a week never sits beside the question that asks it.
                        if (q.Fact.StartsWith("vote-") || q.Fact.StartsWith("block-"))
                            Assert.That(quiz.Any(other => other.Fact == "evicted-" + q.Week), Is.False, q.Text);
                    }
                }
            Assert.That(kinds, Is.EquivalentTo(new[] { "hoh", "veto", "saved", "replacement", "evicted", "evicted-order", "block", "vote" }));
            var named = CompetitionTrivia.Quiz(Built(5, CompetitionRules.Trivia, 3)).Concat(CompetitionTrivia.Quiz(Built(5, CompetitionRules.Trivia, 4)));
            Assert.That(named.Where(q => q.Fact.StartsWith("hoh-")).All(q => q.Text.StartsWith("Who won ") && q.Text.Contains(", the Head of Household competition in week ")), Is.True,
                "A won competition is asked by the name of the game that was played.");
        }

        /// <summary>
        /// The deny test: nothing the player was not shown reaches a question. Every ballot, claim, alliance, standing,
        /// plan, reply, call and chance on the ledger, the ballots themselves, the alliances, the relationships, the event
        /// log and the backdoor plans are filled with something to give away, and the quiz does not move; nor does a row of
        /// the week being played. Changing a public fact does move it.
        /// </summary>
        [Test]
        public void NothingThePlayerWasNotShownReachesAQuestion()
        {
            for (uint seed = 1; seed <= 40; seed++)
            {
                var plain = Built(5, CompetitionRules.Trivia, seed);
                string before = Lines(CompetitionTrivia.Quiz(plain));
                var secret = Built(5, CompetitionRules.Trivia, seed);
                var l = secret.ledger;
                l.ballots.Add(new BallotRow { week = 2, voterId = "c3", targetId = "c6", readBefore = "c6", correct = true });
                l.claims.Add(new ClaimRow { week = 2, voterId = "c4", targetId = "c8", source = ClaimSource.Overheard });
                l.alliances.Add(new AllianceRow { id = "alliance-npc-1", why = "npc", startedWeek = 1 });
                l.standings.Add(new StandingRow { week = 2, fromId = "c9", toId = "c0", source = ClaimSource.Juror, score = -40 });
                l.plans.Add(new PactPlanRow { week = 3, allianceId = "alliance-npc-1", targetId = "c5", throughId = "c3" });
                l.replies.Add(new ReplyRow { week = 2, cardId = "r", kind = "k", fromId = "c2", listenerId = "c0", replyKey = "x" });
                l.calls.Add(new BlocCallRow { week = 3, allianceId = "alliance-npc-1", callerId = "c4", targetId = "c7" });
                l.opportunities.Add(new OpportunityRow { id = "deal-1", kind = "deal", week = 2, note = "Indy" });
                foreach (var row in l.power) { row.backdoorTargetId = "c5"; row.backdoorResult = "missed"; }
                secret.votes.Add(new VoteState { voterId = "c1", targetId = "c2", reason = "hidden" });
                secret.alliances.Add(new AllianceState { id = "alliance-npc-1", name = "Secret Six", members = new List<string> { "c1", "c2" }, active = true });
                secret.relationships.Add(new RelationshipState { fromId = "c1", toId = "c2", score = 90 });
                secret.events.Add(new EpisodeEvent { sequence = 1, week = 2, kind = "vote-reveal", text = "Blair voted to evict Gray.", audienceIds = new List<string> { "c1" } });
                // The week being played: nothing it holds is asked yet.
                Row(secret, 5, "c6", "c6", true, "c5", "c1", new[] { "c1", "c2" }, new[] { 2, 1 }, null);
                Assert.That(Lines(CompetitionTrivia.Quiz(secret)), Is.EqualTo(before), "seed " + seed);
            }
            uint asksHoh = 1;
            while (CompetitionTrivia.Quiz(Built(5, CompetitionRules.Trivia, asksHoh)).All(q => q.Fact != "hoh-1")) asksHoh++;
            var moved = Built(5, CompetitionRules.Trivia, asksHoh);
            moved.ledger.power[0].hohId = "c2";
            var asked = CompetitionTrivia.Quiz(moved).Single(q => q.Fact == "hoh-1");
            Assert.That(asked.AnswerText, Is.EqualTo("Casey"), "A public fact is read: the week-1 Head of Household changed, and so did the answer.");
        }

        [Test]
        public void TheQuizIsTheSameAfterASaveAndReload()
        {
            var state = Built(6);
            state.phase = EpisodePhase.Veto;
            string quiz = Lines(CompetitionTrivia.Quiz(state));
            Assert.That(quiz, Is.Not.Empty);
            Assert.That(Lines(CompetitionTrivia.Quiz(state.Clone())), Is.EqualTo(quiz));
            var reloaded = JsonConvert.DeserializeObject<EpisodeState>(JsonConvert.SerializeObject(state), Reload);
            Assert.That(Lines(CompetitionTrivia.Quiz(reloaded)), Is.EqualTo(quiz));
            Assert.That(Lines(CompetitionTrivia.Quiz(state)), Is.EqualTo(quiz), "Asking does not change it.");
            state.phase = EpisodePhase.HoH;
            Assert.That(Lines(CompetitionTrivia.Quiz(state)), Is.Not.EqualTo(quiz), "Each competition deals its own.");
        }

        // ---------------------------------------------------------------- the player's result

        [Test]
        public void ThePlayersResultIsTheShareOfRightAnswers()
        {
            var quiz = CompetitionTrivia.Quiz(Built(4));
            var right = quiz.Select(q => q.Answer).ToList();
            Assert.That(CompetitionTrivia.Performance(quiz, right), Is.EqualTo(1));
            var wrong = quiz.Select(q => (q.Answer + 1) % q.Choices.Count).ToList();
            Assert.That(CompetitionTrivia.Performance(quiz, wrong), Is.EqualTo(0));
            var three = right.Take(3).Concat(new[] { -1, 99 }).ToList();
            Assert.That(CompetitionTrivia.RightAnswers(quiz, three), Is.EqualTo(3));
            Assert.That(CompetitionTrivia.Performance(quiz, three), Is.EqualTo(.6).Within(1e-12));
            Assert.That(CompetitionTrivia.Performance(quiz, right.Take(2).ToList()), Is.EqualTo(.4).Within(1e-12), "An unanswered question is not right.");
            Assert.That(CompetitionTrivia.Performance(Array.Empty<TriviaQuestion>(), right), Is.EqualTo(0));
        }

        // ---------------------------------------------------------------- a played season

        /// <summary>A seed whose first memory board under rules 5 falls in week 4 or 5, so a short season reaches it.</summary>
        private static uint SeedWithALateBoard(out int week, out EpisodePhase phase)
        {
            for (uint seed = 1; seed < 2000; seed++)
            {
                var first = FirstBoard(seed);
                if (first.week < 4 || first.week > 5) continue;
                week = first.week; phase = first.phase;
                return seed;
            }
            throw new InvalidOperationException("No seed with a late memory board.");
        }

        private static (int week, EpisodePhase phase) FirstBoard(uint seed)
        {
            for (int week = 1; week <= 12; week++)
                foreach (var phase in new[] { EpisodePhase.HoH, EpisodePhase.Veto })
                {
                    var five = CompetitionDefinitions.Version5(seed, week, phase);
                    if (five == CompetitionDefinitions.HouseMemory || five == CompetitionDefinitions.FirstImpressions) return (week, phase);
                }
            return (99, EpisodePhase.HoH);
        }

        /// <summary>
        /// The rules change only the game: one season played by the same commands under rules 5 and rules 6 commits the
        /// same scores, draws the same rolls and ends each step on the same state but for the rules' own number and the
        /// line that names the game dealt; and rules 6 dealt the trivia, in week 4 or later, with a quiz of the season's
        /// own public events that a save and reload keeps.
        /// </summary>
        [Test]
        public void ASeasonUnderRulesSixCommitsWhatRulesFiveWouldAndAsksItsOwnEvents()
        {
            uint seed = SeedWithALateBoard(out int boardWeek, out EpisodePhase boardPhase);
            var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 10 }, seed);
            ShippedRules.ApplyFresh(fresh);
            Assert.That(fresh.competitionRulesVersion, Is.EqualTo(CompetitionRules.Trivia), "Enrolled: a season the game starts plays rules 6.");
            var held = fresh.Clone();
            held.competitionRulesVersion = CompetitionRules.RingTower; // The same season held at rules 5 by name.
            var six = new EpisodeEngine(fresh);
            var five = new EpisodeEngine(held);
            bool dealt = false;
            for (int step = 0; step < 4000 && six.Snapshot.week <= boardWeek && six.Snapshot.phase != EpisodePhase.Finished; step++)
            {
                var s = six.Snapshot;
                if (EpisodeEngine.IsCompetition(s.phase) && !s.competitionResolved
                    && CompetitionDefinitions.For(s) == CompetitionDefinitions.Trivia && !dealt)
                {
                    dealt = true;
                    Assert.That(s.week, Is.GreaterThanOrEqualTo(CompetitionTrivia.EarliestWeek));
                    Assert.That((s.week, s.phase), Is.EqualTo((boardWeek, boardPhase)), "Trivia takes the late memory board's place.");
                    var quiz = CompetitionTrivia.Quiz(s);
                    Assert.That(quiz.Count, Is.EqualTo(CompetitionTrivia.Questions));
                    var rows = s.ledger.power.Where(r => r.week < s.week).ToList();
                    foreach (var q in quiz)
                    {
                        var row = rows.Last(r => r.week == q.Week);
                        string kind = q.Fact.Substring(0, q.Fact.LastIndexOf('-'));
                        string id = kind == "hoh" ? row.hohId : kind == "veto" ? row.vetoHolderId : kind == "evicted" ? row.evicteeId
                            : kind == "replacement" ? row.replacementId : kind == "block" ? row.nominees.Single(n => n != row.evicteeId)
                            : kind == "saved" ? (row.vetoUsed ? row.savedId : null) : null;
                        string want = kind == "vote" ? null : id == null ? CompetitionTrivia.NobodyChoice : s.Find(id).name;
                        if (want != null) Assert.That(q.AnswerText, Is.EqualTo(want), q.Text);
                    }
                    var reloaded = JsonConvert.DeserializeObject<EpisodeState>(JsonConvert.SerializeObject(s), Reload);
                    Assert.That(Lines(CompetitionTrivia.Quiz(reloaded)), Is.EqualTo(Lines(quiz)), "The same quiz after a save and reload.");
                }
                Assert.That(CompetitionDefinitions.For(five.Snapshot), Is.Not.SameAs(CompetitionDefinitions.Trivia));
                var command = EpisodeEngineTests.NextCommand(s);
                var a = six.Apply(command);
                var b = five.Apply(command);
                Assert.That(a.accepted, Is.True, a.reason);
                Assert.That(b.accepted, Is.True, b.reason);
                Assert.That(Normal(six.Snapshot), Is.EqualTo(Normal(five.Snapshot)), "step " + step + ": " + command.kind);
            }
            Assert.That(dealt, Is.True, "seed " + seed + ": rules 6 dealt the trivia in week " + boardWeek);
            Assert.That(six.Snapshot.events.Any(e => e.kind == "competition-definition" && e.text.StartsWith("season-trivia-v6", StringComparison.Ordinal)), Is.True);
            Assert.That(five.Snapshot.events.Any(e => e.kind == "competition-definition" && e.text.StartsWith("season-trivia-v6", StringComparison.Ordinal)), Is.False);
        }

        /// <summary>The state as JSON with the rules' number and the dealt game's line taken out.</summary>
        internal static string Normal(EpisodeState state)
        {
            var o = JObject.FromObject(state);
            o["competitionRulesVersion"] = 0;
            foreach (var entry in (JArray)o["events"])
                if ((string)entry["kind"] == "competition-definition") entry["text"] = "";
            return o.ToString(Formatting.None);
        }
    }
}
