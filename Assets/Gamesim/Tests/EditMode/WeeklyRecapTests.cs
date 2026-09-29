using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Reading a week back out of the record.
    ///
    /// <para>The recap derives everything from committed state, so the test that matters most is
    /// that it agrees with the season it describes. A recap that says the wrong person was evicted
    /// is worse than no recap, because the player has no other place to check.</para>
    /// </summary>
    public sealed class WeeklyRecapTests
    {
        // ---------------------------------------------------------------- against a real season

        /// <summary>
        /// Every week of a played season, checked against the state rather than against itself.
        /// </summary>
        [Test]
        public void EveryWeekOfAPlayedSeasonMatchesWhatTheSeasonRecords()
        {
            var state = PlayedSeason(5u);
            var weeks = WeeklyRecap.Season(state);
            Assert.That(weeks, Is.Not.Empty, "A finished season should have weeks to recap.");
            Assert.That(weeks.Select(w => w.week), Is.Ordered, "Oldest first.");

            foreach (var recap in weeks)
            {
                var evictedThisWeek = state.events
                    .Where(e => e.week == recap.week && (e.kind == "eviction" || e.kind == "final-eviction"))
                    .ToList();
                if (evictedThisWeek.Count == 0)
                {
                    Assert.That(recap.evicted, Is.Null, "week " + recap.week);
                }
                else
                {
                    Assert.That(recap.evicted, Is.Not.Null, "week " + recap.week + ": " + evictedThisWeek[0].text);
                    // A week's eviction line opens with the evictee; the final eviction's opens with the
                    // Head of Household who chose, and names the evictee in its second sentence.
                    if (evictedThisWeek[0].kind == "final-eviction")
                    {
                        Assert.That(evictedThisWeek[0].text, Does.Contain(". " + recap.evicted), "week " + recap.week + ": the second sentence's.");
                        Assert.That(recap.evictedId, Is.EqualTo(state.ledger.power.Single(p => p.week == recap.week).evicteeId), "and the ledger's.");
                    }
                    else Assert.That(evictedThisWeek[0].text, Does.StartWith(recap.evicted));
                    Assert.That(state.contestants.Any(c => c.name == recap.evicted), Is.True);
                }

                CollectionAssert.AreEquivalent(
                    state.contestants.Where(c => c.nominationWeeks.Contains(recap.week)).Select(c => c.name).ToList(),
                    recap.nominees, "week " + recap.week + " nominees");

                if (recap.headOfHousehold != null)
                    Assert.That(state.contestants.Any(c => c.name == recap.headOfHousehold), Is.True,
                        "week " + recap.week + ": '" + recap.headOfHousehold + "' is not in this cast.");
                if (recap.vetoHolder != null)
                    Assert.That(state.contestants.Any(c => c.name == recap.vetoHolder), Is.True,
                        "week " + recap.week + ": '" + recap.vetoHolder + "' is not in this cast.");
            }
        }

        /// <summary>
        /// The competition line is the one sentence the engine does not open with a name, and it was
        /// being read by two different parsers until they were made one.
        /// </summary>
        [Test]
        public void TheHeadOfHouseholdIsReadOutOfTheCompetitionLine()
        {
            var state = PlayedSeason(5u);
            var first = state.events.First(e => e.kind == "competition" && e.phase == EpisodePhase.HoH);
            Assert.That(first.text, Does.StartWith("Competition winner:"));

            var recap = WeeklyRecap.Build(state, first.week);
            Assert.That(recap.headOfHousehold, Is.Not.Null);
            Assert.That(first.text, Does.Contain(recap.headOfHousehold));
            Assert.That(recap.headOfHousehold, Does.Not.Contain("·").And.Not.EndWith("."));
        }

        [Test]
        public void ARecapReadsTheSameEverySeasonPlayedTheSameWay()
        {
            var first = WeeklyRecap.Season(PlayedSeason(9u)).Select(Describe).ToList();
            var second = WeeklyRecap.Season(PlayedSeason(9u)).Select(Describe).ToList();
            CollectionAssert.AreEqual(first, second);
        }

        [Test]
        public void ReadingTheRecordDoesNotChangeIt()
        {
            var state = PlayedSeason(3u);
            uint random = state.randomState;
            long sequence = state.nextSequence;
            int events = state.events.Count, revision = state.revision;

            WeeklyRecap.Season(state);
            WeeklyRecap.Build(state, 1);
            WeeklyRecap.Ready(state);

            Assert.That(state.randomState, Is.EqualTo(random));
            Assert.That(state.nextSequence, Is.EqualTo(sequence));
            Assert.That(state.events.Count, Is.EqualTo(events));
            Assert.That(state.revision, Is.EqualTo(revision));
            Assert.That(EpisodeValidation.TryValidate(state, out string error), Is.True, error);
        }

        [Test]
        public void EveryBallotTheRevealShowedIsInTheRecap()
        {
            var state = PlayedSeason(5u);
            int week = state.events.First(e => e.kind == "vote-reveal").week;
            var shown = state.events.Where(e => e.week == week && e.kind == "vote-reveal")
                .OrderBy(e => e.sequence).Select(e => e.text).ToList();

            CollectionAssert.AreEqual(shown, WeeklyRecap.Build(state, week).ballots);
        }

        // ---------------------------------------------------------------- the parsers

        [Test]
        public void AWinnerIsReadOutOfItsSentenceAndNothingElseIs()
        {
            Assert.That(WeeklyRecap.WinnerName("Competition winner: Maya Chen · Mental."), Is.EqualTo("Maya Chen"));
            Assert.That(WeeklyRecap.WinnerName("Competition winner: Maya Chen"), Is.EqualTo("Maya Chen"));
            Assert.That(WeeklyRecap.WinnerName("Competition winner: Maya Chen."), Is.EqualTo("Maya Chen"));
            Assert.That(WeeklyRecap.WinnerName("Nothing to read here"), Is.Null);
            Assert.That(WeeklyRecap.WinnerName("Competition winner:"), Is.Null);
            Assert.That(WeeklyRecap.WinnerName(null), Is.Null);
            Assert.That(WeeklyRecap.WinnerName(""), Is.Null);
        }

        /// <summary>
        /// The sentence this exists to get right: "Maya nominates Taylor" is about Maya. Matching
        /// anywhere in the line rather than at the start would answer Taylor.
        /// </summary>
        [Test]
        public void ALineIsAboutWhoeverItOpensWith()
        {
            var state = Season(5u, 8);
            var cast = state.contestants.Take(2).Select(c => c.name).ToArray();

            Assert.That(WeeklyRecap.Subject(state, cast[0] + " is evicted and joins the jury."), Is.EqualTo(cast[0]));
            Assert.That(WeeklyRecap.Subject(state, cast[0] + " nominates " + cast[1] + "."), Is.EqualTo(cast[0]));
            Assert.That(WeeklyRecap.Subject(state, "The house voted."), Is.Null);
            Assert.That(WeeklyRecap.Subject(state, null), Is.Null);
            Assert.That(WeeklyRecap.Subject(null, "anything"), Is.Null);
        }

        /// <summary>A shorter name that prefixes a longer one must not win the match.</summary>
        [Test]
        public void TheLongerNameWinsWhereOneNamePrefixesAnother()
        {
            var state = Season(5u, 8);
            state.contestants[0].name = "Jamie";
            state.contestants[1].name = "Jamie Roberts";
            Assert.That(WeeklyRecap.Subject(state, "Jamie Roberts is evicted."), Is.EqualTo("Jamie Roberts"));
            Assert.That(WeeklyRecap.Subject(state, "Jamie is evicted."), Is.EqualTo("Jamie"));
        }

        // ---------------------------------------------------------------- the veto

        [Test]
        public void AVetoThatWasNotUsedIsNotTheSameAsNoVetoMeeting()
        {
            var state = Season(5u, 8);
            Assert.That(WeeklyRecap.Build(state, 1).vetoUsed, Is.Null,
                "No meeting happened, which is not a decision either way.");

            state.events.Add(Line(state, 1, EpisodePhase.VetoMeeting, "veto",
                "Maya Chen declines to use the veto. Nominations stand."));
            Assert.That(WeeklyRecap.Build(state, 1).vetoUsed, Is.False);

            state.events.RemoveAt(state.events.Count - 1);
            state.events.Add(Line(state, 1, EpisodePhase.VetoMeeting, "veto",
                "Maya Chen saves Taylor Kim; Casey Wilson is the replacement nominee."));
            Assert.That(WeeklyRecap.Build(state, 1).vetoUsed, Is.True);
        }

        /// <summary>The log addresses the player in the second person, so both readings must work.</summary>
        [Test]
        public void ThePlayerDecliningTheVetoReadsTheSameAsAHouseguestDoingIt()
        {
            var state = Season(5u, 8);
            state.events.Add(Line(state, 1, EpisodePhase.VetoMeeting, "veto",
                "You decline to use the veto. Nominations stand."));
            Assert.That(WeeklyRecap.Build(state, 1).vetoUsed, Is.False);
        }

        // ---------------------------------------------------------------- what the player is told

        /// <summary>
        /// A recap is a screen the player reads, so it may only tell them what their character
        /// knows. The rest of the house's private feelings are not theirs.
        /// </summary>
        [Test]
        public void ARecapNeverReportsARelationshipThePlayerIsNotPartOf()
        {
            var state = Season(5u, 8);
            var cast = state.contestants.Where(c => !c.isPlayer).ToList();

            // A large private movement between two houseguests, and one involving the player.
            Move(state, cast[0].id, cast[1].id, 1, 60);
            Move(state, cast[2].id, state.playerId, 1, -40);

            var moves = WeeklyRecap.Build(state, 1).relationships;
            Assert.That(moves, Has.Count.EqualTo(1));
            Assert.That(moves[0].otherId, Is.EqualTo(cast[2].id));
            Assert.That(moves.Any(m => m.otherId == cast[0].id || m.otherId == cast[1].id), Is.False,
                "Two houseguests falling out privately is not the player's to read.");
        }

        [Test]
        public void ASmallWeekOfDriftIsNotWorthMentioning()
        {
            var state = Season(5u, 8);
            var npc = state.contestants.First(c => !c.isPlayer);
            Move(state, npc.id, state.playerId, 1, WeeklyRecap.NotableMove - 1);
            Assert.That(WeeklyRecap.Build(state, 1).relationships, Is.Empty);

            Move(state, npc.id, state.playerId, 1, 1);
            Assert.That(WeeklyRecap.Build(state, 1).relationships, Has.Count.EqualTo(1));
        }

        /// <summary>How they read you and how you read them are different facts.</summary>
        [Test]
        public void TheRecapSaysWhichDirectionARelationshipMoved()
        {
            var state = Season(5u, 8);
            var npc = state.contestants.First(c => !c.isPlayer);
            Move(state, npc.id, state.playerId, 1, 40);

            var theirs = WeeklyRecap.Build(state, 1).relationships.Single();
            Assert.That(theirs.aboutYou, Is.True);
            Assert.That(theirs.Line, Is.EqualTo(npc.name + " warmed to you"));

            state.relationships.Single(r => r.fromId == npc.id && r.toId == state.playerId).events.Clear();
            Move(state, state.playerId, npc.id, 1, -40);
            var yours = WeeklyRecap.Build(state, 1).relationships.Single();
            Assert.That(yours.aboutYou, Is.False);
            Assert.That(yours.Line, Is.EqualTo("You cooled on " + npc.name));
        }

        [Test]
        public void ARecapNeverRepeatsSomebodyElsesPrivateMoment()
        {
            var state = Season(5u, 8);
            var cast = state.contestants.Where(c => !c.isPlayer).ToList();
            var theirs = Line(state, 1, EpisodePhase.Social, "promise-outcome", "A private falling out.");
            theirs.audienceIds.Add(cast[0].id);
            state.events.Add(theirs);
            state.events.Add(Line(state, 1, EpisodePhase.Social, "promise-outcome", "Something the house saw."));

            var moments = WeeklyRecap.Build(state, 1).moments;
            Assert.That(moments, Has.Count.EqualTo(1));
            Assert.That(moments[0], Is.EqualTo("Something the house saw."));
        }

        /// <summary>
        /// A deal or an alliance between two other houseguests is logged to them alone, and the
        /// recap's Deals and Alliances were the one place it could still reach the player.
        /// </summary>
        [Test]
        public void ADealOrAllianceBetweenTwoOtherHouseguestsNeverReachesTheRecap()
        {
            var state = Season(5u, 8);
            var cast = state.contestants.Where(c => !c.isPlayer).ToList();
            foreach (var kind in new[] { "deal-outcome", "deal", "alliance" })
            {
                var theirs = Line(state, 1, EpisodePhase.Social, kind, "Their " + kind + ".");
                theirs.audienceIds.Add(cast[0].id);
                theirs.audienceIds.Add(cast[1].id);
                state.events.Add(theirs);
                var yours = Line(state, 1, EpisodePhase.Social, kind, "Your " + kind + ".");
                yours.audienceIds.Add(state.playerId);
                state.events.Add(yours);
            }
            var recap = WeeklyRecap.Build(state, 1);
            Assert.That(recap.deals, Is.EqualTo(new[] { "Your deal-outcome.", "Your deal." }));
            Assert.That(recap.alliances, Is.EqualTo(new[] { "Your alliance." }));
        }

        /// <summary>
        /// Looking back at a week from the jury is not the week the season ended: the end is said in
        /// the week the ledger says the player left, and the veto that saved them is that week's.
        /// </summary>
        [Test]
        public void LookingBackAtAWeekSaysWhatHappenedThatWeek()
        {
            var state = Season(5u, 8);
            var you = state.Find(state.playerId);
            var cast = state.contestants.Where(c => !c.isPlayer).ToList();
            state.week = 3;
            you.status = ContestantStatus.Jury;
            you.nominationWeeks.Add(1);
            you.nominationWeeks.Add(3);
            state.ledger.power.Add(new PowerRow { week = 1, hohId = cast[0].id, vetoHolderId = cast[1].id, vetoUsed = true, savedId = you.id,
                replacementId = cast[2].id, nominees = new List<string> { cast[2].id, cast[3].id }, evicteeId = cast[3].id, tally = new List<int> { 4, 2 } });
            state.ledger.power.Add(new PowerRow { week = 3, hohId = cast[1].id, nominees = new List<string> { you.id, cast[4].id }, evicteeId = you.id, tally = new List<int> { 5, 1 } });
            string first = WeeklyRecap.Build(state, 1).yourWeek;
            Assert.That(first, Does.Contain("the veto took you back down"), "Week one's veto saved them.");
            Assert.That(first, Does.Not.Contain("Your season ended here."), "Week one was not the end.");
            Assert.That(WeeklyRecap.Build(state, 3).yourWeek, Does.Contain("Your season ended here."));
        }

        /// <summary>
        /// The redesigned recap's facts come from the ledger's row for the week: the ids, the block,
        /// the evictee and their place, the vote split, the key moments, and the house named from
        /// what the player saw - never a number the player could not know.
        /// </summary>
        [Test]
        public void TheWeeksLedgerGivesTheRecapItsFacts()
        {
            var state = PlayedSeason(5u);
            foreach (var recap in WeeklyRecap.Season(state))
            {
                var power = state.ledger.power.FirstOrDefault(p => p.week == recap.week);
                if (power == null) continue;
                Assert.That(recap.hohId, Is.EqualTo(power.hohId), "week " + recap.week);
                Assert.That(recap.evictedId, Is.EqualTo(power.evicteeId), "week " + recap.week);
                Assert.That(recap.block, Is.EqualTo(power.nominees), "week " + recap.week);
                if (recap.evictedId != null)
                {
                    Assert.That(recap.placement, Is.EqualTo(Gamesim.Persistence.CareerLedger.Placement(state, state.Find(recap.evictedId))), "week " + recap.week);
                    Assert.That(recap.keyMoments.Last().kind, Is.EqualTo("eviction"), "week " + recap.week);
                    Assert.That(recap.exitRecord, Is.Not.Empty, "week " + recap.week);
                }
                var (against, others) = WeeklyRecap.Split(recap);
                if (recap.votes.Count > 0 && !recap.votes.Any(v => v.tieBreak) && power.tally.Count > 0)
                    Assert.That(against + others, Is.EqualTo(power.tally.Sum()), "week " + recap.week + ": every ballot the reveal read.");
                Assert.That(recap.temperature, Is.Not.Empty, "week " + recap.week);
                foreach (var reading in recap.temperature)
                    Assert.That(reading.evidence, Is.Not.Empty, "week " + recap.week + ": " + reading.label + " rests on something the player saw.");
                if (recap.evicteeQuote != null)
                    Assert.That(state.events.Any(e => e.week == recap.week && e.kind == "eviction-speech" && e.text.Contains(recap.evicteeQuote))
                        || state.evictionSpeeches.Any(x => x.text == recap.evicteeQuote), Is.True, "week " + recap.week + ": their own words from the block.");
                if (recap.finalDecision)
                    Assert.That(recap.nominated, Is.Empty, "week " + recap.week + ": the last Head of Household chose between two; nobody was nominated.");
            }
            Assert.That(WeeklyRecap.Season(state).Last().remaining, Is.EqualTo(2), "The season's last week leaves its two finalists in the house.");
        }

        /// <summary>
        /// How the house took to the player is the house's: the record keeps it, and the week's
        /// story tells only the player's own feelings.
        /// </summary>
        [Test]
        public void HowTheHouseTookToYouStaysOutOfTheWeeksStory()
        {
            var state = Season(5u, 8);
            var npc = state.contestants.First(c => !c.isPlayer);
            Move(state, npc.id, state.playerId, 1, 40);
            var recap = WeeklyRecap.Build(state, 1);
            Assert.That(recap.relationships.Single().aboutYou, Is.True, "The record keeps it.");
            Assert.That(recap.yourWeek ?? "", Does.Not.Contain("warm").And.Not.Contain("liked").And.Not.Contain("enemies"));
            Move(state, state.playerId, npc.id, 1, 40);
            Assert.That(WeeklyRecap.Build(state, 1).yourWeek, Does.Contain("You warmed to the house this week."));
        }

        [Test]
        public void ThePlayersOwnWeekIsDescribedFromWhatTheStateRecords()
        {
            var state = Season(5u, 8);
            var you = state.Find(state.playerId);
            you.nominationWeeks.Add(1);
            state.nominees = new List<string> { you.id, state.contestants.First(c => !c.isPlayer).id };

            string told = WeeklyRecap.Build(state, 1).yourWeek;
            Assert.That(told, Does.Contain("on the block"));

            var quiet = WeeklyRecap.Build(Season(5u, 8), 1).yourWeek;
            Assert.That(quiet, Is.EqualTo("A quiet week for you."));
        }

        // ---------------------------------------------------------------- when it is shown

        [Test]
        public void AWeekIsOnlyReadyOnceItsVoteIsIn()
        {
            var state = Season(5u, 8);
            Assert.That(WeeklyRecap.Ready(state), Is.False);

            state.evictionResolved = true;
            Assert.That(WeeklyRecap.Ready(state), Is.False, "Resolved is not the same as recorded.");

            state.events.Add(Line(state, state.week, EpisodePhase.Eviction, "eviction",
                "Taylor Kim is evicted and joins the jury."));
            Assert.That(WeeklyRecap.Ready(state), Is.True);
        }

        [Test]
        public void ASeasonThatHasNotVotedYetHasNothingToRecap()
        {
            var state = Season(5u, 8);
            Assert.That(WeeklyRecap.Season(state).Any(w => w.evicted != null), Is.False);
            Assert.That(WeeklyRecap.Build(state, 99).Empty, Is.True);
            Assert.That(WeeklyRecap.Build(state, 0).Empty, Is.True);
            Assert.That(WeeklyRecap.Build(null, 1).Empty, Is.True);
        }

        [Test]
        public void AHeadlineAlwaysSaysSomething()
        {
            var state = PlayedSeason(5u);
            foreach (var recap in WeeklyRecap.Season(state))
            {
                Assert.That(recap.Headline, Is.Not.Null.And.Not.Empty);
                Assert.That(recap.Headline, Does.Contain(recap.week.ToString()).Or.Contain(recap.evicted ?? " "));
            }
            Assert.That(new WeeklyRecap.Week { week = 4 }.Headline, Is.EqualTo("Week 4."));
        }

        // ---------------------------------------------------------------- fixtures

        private static EpisodeState Season(uint seed, int size) =>
            SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size }, seed);

        private static EpisodeState PlayedSeason(uint seed)
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(seed));
            for (int guard = 0; guard < 400 && engine.Snapshot.phase != EpisodePhase.Finished; guard++)
            {
                var result = engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot));
                Assert.That(result.accepted, Is.True, result.reason);
            }
            var finished = engine.Snapshot;
            Assert.That(finished.phase, Is.EqualTo(EpisodePhase.Finished), "The fixture needs a whole season.");
            return finished;
        }

        private static EpisodeEvent Line(EpisodeState state, int week, EpisodePhase phase, string kind, string text)
        {
            var entry = new EpisodeEvent
            {
                sequence = (int)state.nextSequence++, week = week, phase = phase, kind = kind, text = text,
            };
            return entry;
        }

        private static void Move(EpisodeState state, string from, string to, int week, double impact)
        {
            var edge = state.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (edge == null)
            {
                edge = new RelationshipState { fromId = from, toId = to };
                state.relationships.Add(edge);
            }
            edge.events.Add(new RelationshipEventState
            {
                sequence = (int)state.nextSequence++, week = week, type = "fixture",
                description = "fixture", impactScore = impact,
            });
        }

        private static string Describe(WeeklyRecap.Week w) => string.Join("|",
            w.week.ToString(), w.headOfHousehold, w.vetoHolder, w.evicted,
            string.Join(",", w.nominees), w.vetoUsed?.ToString() ?? "none",
            string.Join(",", w.ballots), string.Join(",", w.relationships.Select(m => m.Line)),
            string.Join(",", w.moments), w.yourWeek);
    }
}
