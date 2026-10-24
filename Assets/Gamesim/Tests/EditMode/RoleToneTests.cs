using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The role moments' tone, stakes and quotes (ROLE-MOMENTS-PLAN R1, §3.0): <see cref="RoleTone"/>'s ladder row by
    /// row and in its order, the roles each tone applies to, the situation word and the player's read, the stakes
    /// table, and <see cref="RoleQuotes"/>' lines and keyed pick. The rider tones read only what the week has fixed,
    /// so answering a card and deciding the veto never move them; nothing the player cannot see moves anything here;
    /// a reload shows the same card. Nothing plays any of it yet. Pure simulation and JSON, so the Unity-free subset
    /// runs it as well as the editor.
    /// </summary>
    public sealed class RoleToneTests
    {
        private const string Nominee = RoleTone.Role.Nominee, HoH = RoleTone.Role.HeadOfHousehold,
            Holder = RoleTone.Role.VetoHolder, Courting = RoleTone.Role.Courting;

        // ------------------------------------------------------------------ the ladder, row by row

        /// <summary>Each row's rule on a nominee in week 3, one condition at a time, and each line just missed.</summary>
        [TestCase("you-nominated-them", RoleTone.Bitter)]
        [TestCase("you-broke-your-word", RoleTone.Bitter)]
        [TestCase("you-broke-a-promise", RoleTone.Bitter)]
        [TestCase("you-broke-an-oath", RoleTone.Bitter)]
        [TestCase("you-nominated-them-this-week", RoleTone.Pragmatic)]
        [TestCase("you-broke-your-word-this-week", RoleTone.Pragmatic)]
        [TestCase("you-broke-an-older-word-this-week", RoleTone.Pragmatic)]
        [TestCase("you-broke-an-older-promise-this-week", RoleTone.Pragmatic)]
        [TestCase("you-broke-an-oath-this-week", RoleTone.Pragmatic)]
        [TestCase("you-kept-your-word", RoleTone.Pragmatic)]
        [TestCase("they-broke-their-word", RoleTone.Pragmatic)]
        [TestCase("threatening", RoleTone.Threatening)]
        [TestCase("threatening-at-the-line", RoleTone.Threatening)]
        [TestCase("bold-not-vengeful", RoleTone.Pragmatic)]
        [TestCase("on-the-block-before", RoleTone.Desperate)]
        [TestCase("replacement", RoleTone.Desperate)]
        [TestCase("endgame", RoleTone.Desperate)]
        [TestCase("seven-in-the-house", RoleTone.Pragmatic)]
        [TestCase("close", RoleTone.Friendly)]
        [TestCase("allied", RoleTone.Friendly)]
        [TestCase("reading-short-of-close", RoleTone.Pragmatic)]
        [TestCase("earnest", RoleTone.Earnest)]
        [TestCase("warm-short", RoleTone.Pragmatic)]
        [TestCase("steady", RoleTone.Calculated)]
        [TestCase("steady-at-the-line", RoleTone.Calculated)]
        [TestCase("dishonest", RoleTone.Calculated)]
        [TestCase("nothing", RoleTone.Pragmatic)]
        public void EachRowOfTheLadderReadsItsOwnRule(string condition, string tone)
        {
            var s = Fixture(out string speaker, condition);
            Assert.That(RoleTone.Of(s, speaker, Nominee), Is.EqualTo(tone), condition);
        }

        /// <summary>The conditions that light one row each, in the ladder's order, with the tone each lights.</summary>
        private static readonly string[] RowConditions = { "you-broke-your-word", "threatening", "endgame", "close", "earnest", "steady" };
        private static readonly string[] RowTones =
            { RoleTone.Bitter, RoleTone.Threatening, RoleTone.Desperate, RoleTone.Friendly, RoleTone.Earnest, RoleTone.Calculated };

        private static IEnumerable<TestCaseData> EveryPairOfRows()
        {
            for (int high = 0; high < RowConditions.Length; high++)
                for (int low = high + 1; low < RowConditions.Length; low++)
                    yield return new TestCaseData(RowConditions[high], RowConditions[low], RowTones[high])
                        .SetName("TheHigherRowWins(" + RowTones[high] + " over " + RowTones[low] + ")");
        }

        /// <summary>A houseguest every pair of rows matches wears the higher: the first match wins, so no two rows may change places.</summary>
        [TestCaseSource(nameof(EveryPairOfRows))]
        public void TheHigherOfTwoMatchingRowsWins(string higher, string lower, string tone)
        {
            var s = Fixture(out string speaker, higher, lower);
            Assert.That(Holds(s, speaker, higher) && Holds(s, speaker, lower), Is.True, "Both rows match " + higher + " with " + lower + ".");
            Assert.That(RoleTone.Of(s, speaker, Nominee), Is.EqualTo(tone));
        }

        /// <summary>With every row matching, taking the top row away each time walks the ladder down to its default.</summary>
        [Test]
        public void TheLadderIsBitterThreateningDesperateFriendlyEarnestCalculatedPragmatic()
        {
            Assert.That(RoleTone.Ladder, Is.EqualTo(new[] { RoleTone.Bitter, RoleTone.Threatening, RoleTone.Desperate,
                RoleTone.Friendly, RoleTone.Earnest, RoleTone.Calculated, RoleTone.Pragmatic }));
            for (int top = 0; top <= RowConditions.Length; top++)
            {
                var s = Fixture(out string speaker, RowConditions.Skip(top).ToArray());
                string expected = top < RowTones.Length ? RowTones[top] : RoleTone.Pragmatic;
                Assert.That(RoleTone.Of(s, speaker, Nominee), Is.EqualTo(expected), "Without the top " + top + " rows.");
            }
        }

        // ------------------------------------------------------------------ the roles each tone applies to

        [TestCase(HoH, "endgame", RoleTone.Pragmatic)]
        [TestCase(HoH, "on-the-block-before", RoleTone.Pragmatic)]
        [TestCase(HoH, "threatening", RoleTone.Threatening)]
        [TestCase(HoH, "you-nominated-them", RoleTone.Bitter)]
        [TestCase(Holder, "threatening", RoleTone.Pragmatic)]
        [TestCase(Holder, "endgame", RoleTone.Pragmatic)]
        [TestCase(Holder, "you-broke-your-word", RoleTone.Bitter)]
        [TestCase(Holder, "close", RoleTone.Friendly)]
        [TestCase(Courting, "endgame", RoleTone.Desperate)]
        [TestCase(Courting, "on-the-block-before", RoleTone.Desperate)]
        [TestCase(Courting, "replacement", RoleTone.Pragmatic)]
        [TestCase(Courting, "threatening", RoleTone.Threatening)]
        [TestCase(Nominee, "replacement", RoleTone.Desperate)]
        public void ATonePassedOverForARoleFallsToTheNextRow(string role, string condition, string tone)
        {
            var s = Fixture(out string speaker, condition);
            Assert.That(RoleTone.Of(s, speaker, role), Is.EqualTo(tone), role + ", " + condition);
        }

        [Test]
        public void EachRoleWearsTheTonesItsTableRowsAllow()
        {
            Assert.That(RoleTone.TonesFor(Nominee), Is.EqualTo(RoleTone.Ladder));
            Assert.That(RoleTone.TonesFor(Courting), Is.EqualTo(RoleTone.Ladder));
            Assert.That(RoleTone.TonesFor(HoH), Is.EqualTo(RoleTone.Ladder.Where(t => t != RoleTone.Desperate)));
            Assert.That(RoleTone.TonesFor(Holder), Is.EqualTo(RoleTone.Ladder.Where(t => t != RoleTone.Desperate && t != RoleTone.Threatening)));
            Assert.That(RoleTone.TonesFor("bystander"), Is.Empty);
            var s = Fixture(out string speaker, "nothing");
            Assert.That(RoleTone.Of(s, speaker, "bystander"), Is.Null, "A role this does not know has no tone.");
            Assert.That(RoleTone.Of(s, s.playerId, Nominee), Is.Null, "The player has no tone of their own.");
            Assert.That(RoleTone.Of(s, "nobody", Nominee), Is.Null);
            Assert.That(RoleTone.Of(null, speaker, Nominee), Is.Null);
        }

        // ------------------------------------------------------------------ the second label

        [TestCase(Nominee, "nothing", "First time on the block")]
        [TestCase(Nominee, "on-the-block-before", "On the block again")]
        [TestCase(Nominee, "you-nominated-them", "On the block again")]
        [TestCase(Nominee, "you-nominated-them-this-week", "First time on the block")]
        [TestCase(Nominee, "endgame", "Endgame: 6 left")]
        [TestCase(Nominee, "seven-in-the-house", "First time on the block")]
        [TestCase(Courting, "nothing", "Never nominated")]
        [TestCase(Courting, "on-the-block-before", "Nominated before")]
        [TestCase(Courting, "endgame", "Endgame: 6 left")]
        [TestCase(HoH, "endgame", "Head of Household")]
        [TestCase(Holder, "on-the-block-before", "Holds the veto")]
        public void TheSituationWordSaysWhereTheyStandThisWeek(string role, string condition, string word)
        {
            var s = Fixture(out string speaker, condition);
            Assert.That(RoleTone.Situation(s, speaker, role), Is.EqualTo(word), role + ", " + condition);
        }

        /// <summary>
        /// The endgame is the house as the week began, so the eviction that ends the week moves neither the word nor the
        /// tone a card wore: six who started the week are six, though one has gone.
        /// </summary>
        [Test]
        public void TheEndgameIsTheHouseAsTheWeekBegan()
        {
            var s = Fixture(out string speaker, "endgame", "evicted-this-week");
            Assert.That(s.Active.Count(), Is.EqualTo(5));
            Assert.That(RoleTone.HouseThisWeek(s), Is.EqualTo(6));
            Assert.That(RoleTone.Situation(s, speaker, Nominee), Is.EqualTo("Endgame: 6 left"));
            Assert.That(RoleTone.Of(s, speaker, Nominee), Is.EqualTo(RoleTone.Desperate));

            var seven = Fixture(out string other, "seven-in-the-house", "evicted-this-week");
            Assert.That(seven.Active.Count(), Is.EqualTo(6), "Six in the house now,");
            Assert.That(RoleTone.Endgame(seven), Is.False, "but seven began the week: not the endgame yet.");
            Assert.That(RoleTone.Of(seven, other, Nominee), Is.EqualTo(RoleTone.Pragmatic));
        }

        [TestCase(ReplyCards.CloseReading, RoleTone.Close)]
        [TestCase(ReplyCards.CloseReading - 0.1, RoleTone.Neutral)]
        [TestCase(0, RoleTone.Neutral)]
        [TestCase(ReplyCards.HostileReading + 0.1, RoleTone.Neutral)]
        [TestCase(ReplyCards.HostileReading, RoleTone.Hostile)]
        public void YourReadIsThePlayersOwnReading(double score, string read)
        {
            var s = Fixture(out string speaker, "nothing");
            Set(s, s.playerId, speaker, score);
            Assert.That(RoleTone.Read(s, speaker), Is.EqualTo(read));
            Assert.That(RoleTone.ReadLine(s, speaker), Is.EqualTo("Your read: " + read));
            s.alliances.Add(Pact(s.playerId, speaker));
            Assert.That(RoleTone.Read(s, speaker), Is.EqualTo(RoleTone.Close), "A pact with the player reads close, whatever the score.");
        }

        // ------------------------------------------------------------------ the stakes

        [TestCase(RoleTone.Bitter, -3, -5, RoleTone.Riders.EasesTheirGrudge, 30)]
        [TestCase(RoleTone.Threatening, -2, -4, RoleTone.Riders.HoldsAGrudge, 30)]
        [TestCase(RoleTone.Desperate, -3, -5, RoleTone.Riders.OwesAFavour, 0)]
        [TestCase(RoleTone.Friendly, -1, -3, null, 0)]
        [TestCase(RoleTone.Earnest, -2, -4, null, 0)]
        [TestCase(RoleTone.Calculated, 0, -1, null, 0)]
        [TestCase(RoleTone.Pragmatic, -1, -3, null, 0)]
        public void EachToneStakesTheTablesNumbers(string tone, double decline, double lapse, string rider, double amount)
        {
            var stake = RoleTone.Stakes(tone);
            Assert.That(stake, Is.Not.Null, tone);
            Assert.That((stake.tone, stake.decline, stake.lapse, stake.rider, stake.riderAmount), Is.EqualTo((tone, decline, lapse, rider, amount)));
            Assert.That(stake.lapse, Is.LessThan(stake.decline), "Silence costs more than a no.");
        }

        [Test]
        public void EveryToneHasOneStakeAndOnlyTheRiderTonesCarryARider()
        {
            Assert.That(RoleTone.StakesTable.Select(row => row.tone), Is.EqualTo(RoleTone.Ladder));
            Assert.That(RoleTone.StakesTable.Where(row => row.rider != null).Select(row => row.tone), Is.EqualTo(RoleTone.RiderTones));
            Assert.That(RoleTone.Ladder.Take(RoleTone.RiderTones.Length), Is.EqualTo(RoleTone.RiderTones),
                "The rider tones are checked first, so no live input can reach past them.");
            Assert.That(RoleTone.Stakes("Sulky"), Is.Null);
        }

        // ------------------------------------------------------------------ fixed for the week

        /// <summary>
        /// A rider tone reads nothing the week can move: the player's live reading of them (across both of its lines),
        /// a pact with the player, and every record this week can add - the player nominating them, breaking or keeping
        /// a word to them, an eviction - leave it where it was, in every role it applies to.
        /// </summary>
        [TestCase("you-nominated-them", RoleTone.Bitter)]
        [TestCase("you-broke-your-word", RoleTone.Bitter)]
        [TestCase("threatening", RoleTone.Threatening)]
        [TestCase("on-the-block-before", RoleTone.Desperate)]
        [TestCase("endgame", RoleTone.Desperate)]
        [TestCase("replacement", RoleTone.Desperate)]
        public void ARiderToneReadsOnlyWhatTheWeekHasFixed(string condition, string tone)
        {
            var roles = condition == "replacement" ? new[] { Nominee } : RoleTone.Role.All.Where(role => RoleTone.AppliesTo(tone, role)).ToArray();
            string[] thisWeek = { null, "you-nominated-them-this-week", "you-broke-your-word-this-week", "you-broke-an-older-word-this-week",
                "you-broke-an-older-promise-this-week", "you-broke-an-oath-this-week", "you-kept-your-word-this-week", "evicted-this-week" };
            double[] readings = { -100, ReplyCards.HostileReading, 0, ReplyCards.CloseReading - 0.1, ReplyCards.CloseReading, 100 };
            int checkedCards = 0;
            foreach (string role in roles)
                foreach (string record in thisWeek)
                    foreach (double reading in readings)
                        foreach (bool allied in new[] { false, true })
                        {
                            string speaker;
                            var s = record == null ? Fixture(out speaker, condition) : Fixture(out speaker, condition, record);
                            Set(s, s.playerId, speaker, reading);
                            if (allied) s.alliances.Add(Pact(s.playerId, speaker));
                            Assert.That(RoleTone.Of(s, speaker, role), Is.EqualTo(tone), role + ", " + record + ", " + reading + ", allied " + allied);
                            checkedCards++;
                        }
            Assert.That(checkedCards, Is.GreaterThan(0));
        }

        /// <summary>
        /// A word the player breaks this week counts from the next, whenever it was made and whatever it was: a deal and a
        /// promise made in week 1 and broken now, and an oath broken now (whose record keeps no week it was made), are on
        /// the Your word page as the player's own breach, and only the week each broke in keeps it from reading Bitter
        /// until the week turns: a promise made last week and broken at this week's nominations, a deal broken at this
        /// week's veto.
        /// </summary>
        [TestCase("you-broke-an-older-word-this-week", CommitmentsRead.Kinds.Deal, 1)]
        [TestCase("you-broke-an-older-promise-this-week", CommitmentsRead.Kinds.Promise, 1)]
        [TestCase("you-broke-an-oath-this-week", CommitmentsRead.Kinds.Oath, 0)]
        public void AWordBrokenThisWeekCountsFromTheNextWheneverItWasMade(string condition, string kind, int made)
        {
            var s = Fixture(out string speaker, condition);
            var broken = CommitmentsRead.With(s, speaker).Single(c => c.outcome == CommitmentsRead.Outcomes.Broken);
            Assert.That((broken.kind, broken.brokenById, broken.week), Is.EqualTo((kind, s.playerId, made)),
                "On the page as the player's own breach, made before this week.");
            Assert.That(RoleTone.YouBrokeYourWordBefore(s, speaker), Is.False, "It broke this week.");
            foreach (string role in RoleTone.Role.All)
                Assert.That(RoleTone.Of(s, speaker, role), Is.EqualTo(RoleTone.Pragmatic), role + " this week");

            s.week = Week + 1;
            Assert.That(RoleTone.YouBrokeYourWordBefore(s, speaker), Is.True, "Next week it is a word broken before.");
            foreach (string role in RoleTone.Role.All)
                Assert.That(RoleTone.Of(s, speaker, role), Is.EqualTo(RoleTone.Bitter), role + " next week");
        }

        /// <summary>
        /// Through the public engine: the player holds the veto in a season at Roles, both nominees ask, the first of them
        /// Threatening. Answering one card leaves every other card's tone where it was, and deciding the veto - which
        /// breaks the word the player gave - moves no tone either. The broken word reads Bitter from the next week on.
        /// </summary>
        [Test]
        public void AnsweringAndDecidingTheVetoMoveNoRiderToneWithinTheWeek()
        {
            var engine = Meeting(out string first, out string second);
            var meeting = engine.Snapshot;
            string hoh = meeting.hohId;
            var asks = meeting.deals.Where(d => d.type == DealKind.VetoUse && d.status == DealStatus.Proposed && d.recipientId == meeting.playerId).ToList();
            Assert.That(asks.Select(d => d.proposerId), Is.EquivalentTo(new[] { first, second }), "Both nominees asked.");
            var cards = new[] { (first, Nominee), (second, Nominee), (hoh, HoH) };
            var before = Tones(meeting, cards);
            Assert.That(before[0], Is.EqualTo(RoleTone.Threatening));
            Assert.That(before.Count(tone => RoleTone.RiderTones.Contains(tone)), Is.GreaterThanOrEqualTo(1));

            var accept = engine.Apply(Command(engine.Snapshot, EpisodeCommandKind.RespondToDeal, asks.Single(d => d.proposerId == first).id, null, EpisodeEngine.AcceptDeal));
            Assert.That(accept.accepted, Is.True, accept.reason);
            var afterAccept = Tones(accept.state, cards);
            Assert.That(afterAccept.Skip(1), Is.EqualTo(before.Skip(1)), "Answering the first card moves no other card's tone.");

            var decline = engine.Apply(Command(engine.Snapshot, EpisodeCommandKind.RespondToDeal, asks.Single(d => d.proposerId == second).id, null, "decline"));
            Assert.That(decline.accepted, Is.True, decline.reason);
            var afterDecline = Tones(decline.state, cards);
            Assert.That(new[] { afterDecline[0], afterDecline[2] }, Is.EqualTo(new[] { afterAccept[0], afterAccept[2] }),
                "Answering the second card moves no other card's tone.");

            var at = engine.Snapshot;
            var veto = Command(at, EpisodeCommandKind.ResolveVeto, second, EpisodeEngine.ReplacementCandidates(at).First().id);
            veto.useVeto = true;
            var decided = engine.Apply(veto);
            Assert.That(decided.accepted, Is.True, decided.reason);
            var broken = decided.state.deals.Single(d => d.id == asks.Single(a => a.proposerId == first).id);
            Assert.That((broken.status, broken.brokenById, broken.settledWeek), Is.EqualTo((DealStatus.Broken, at.playerId, at.week)),
                "Saving the other nominee broke the word the player gave this week.");
            var afterVeto = Tones(decided.state, cards);

            var steps = new[] { before, afterAccept, afterDecline, afterVeto };
            for (int card = 0; card < cards.Length; card++)
            {
                var seen = steps.Select(step => step[card]).ToList();
                if (RoleTone.RiderTones.Contains(seen[0]))
                    Assert.That(seen, Is.All.EqualTo(seen[0]), cards[card] + ": a rider tone holds from the card to the decision.");
                else
                    Assert.That(seen.Any(tone => RoleTone.RiderTones.Contains(tone)), Is.False, cards[card] + ": a live tone never becomes a rider tone.");
            }
            Assert.That(RoleTone.YouBrokeYourWordBefore(decided.state, first), Is.False, "This week's breach counts from the next.");

            // The rest of the week - the campaign, the vote, the reveal that puts the breach on the record, the eviction -
            // moves no rider tone either.
            for (int i = 0; i < 400 && engine.Snapshot.week == at.week; i++)
            {
                var result = engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot));
                Assert.That(result.accepted, Is.True, result.reason);
                if (result.state.week != at.week) break;
                var now = Tones(result.state, cards);
                for (int card = 0; card < cards.Length; card++)
                    if (RoleTone.RiderTones.Contains(before[card]))
                        Assert.That(now[card], Is.EqualTo(before[card]), cards[card] + " at " + result.state.phase + ", " + result.state.evictionStage);
            }
            var nextWeek = engine.Snapshot;
            Assert.That(nextWeek.week, Is.EqualTo(at.week + 1));
            Assert.That(RoleTone.YouBrokeYourWordBefore(nextWeek, first), Is.True);
            Assert.That(RoleTone.Of(nextWeek, first, Courting), Is.EqualTo(RoleTone.Bitter), "The word broken last week reads Bitter now.");
        }

        // ------------------------------------------------------------------ what the player cannot see

        /// <summary>
        /// The knowledge gate: on a real season at the veto meeting, every houseguest's view of everybody - the player
        /// included - their grudges, moods, stress and memories, pacts and deals the player is not in, and the season's
        /// generator are all moved, and no tone, situation word, read or line moves, in any role or moment. Nothing the
        /// card reads was crossed: the player's own scores, pacts and record, traits and the ceremonies stand. The
        /// control moves what the card does read, and the card moves.
        /// </summary>
        [Test]
        public void NothingThePlayerCannotSeeMovesACard()
        {
            var s = Meeting(out _, out _).Snapshot;
            var before = Cards(s);
            var leakable = s.Active.Where(c => !c.isPlayer && !s.Allied(s.playerId, c.id) && s.Score(s.playerId, c.id) < ReplyCards.CloseReading
                && !RoleTone.RiderTones.Contains(RoleTone.Of(s, c.id, Nominee))).ToList();
            Assert.That(leakable, Is.Not.Empty, "Some card would read Friendly if the houseguest's own view leaked into it.");

            var hidden = s.Clone();
            Hide(hidden);
            Assert.That(Cards(hidden), Is.EqualTo(before));

            var control = s.Clone();
            Set(control, control.playerId, leakable[0].id, 100);
            Assert.That(RoleTone.Of(control, leakable[0].id, Nominee), Is.EqualTo(RoleTone.Friendly), "The player's own reading is what the card reads.");
            Assert.That(RoleTone.Read(control, leakable[0].id), Is.EqualTo(RoleTone.Close));
        }

        /// <summary>A houseguest's view of the player, from one end to the other, and a grudge against them, never reach the card.</summary>
        [Test]
        public void TheirViewOfThePlayerNeverReachesTheCard()
        {
            foreach (double view in new[] { -100, ReplyCards.HostileReading, 0, ReplyCards.CloseReading, 100 })
                foreach (string role in RoleTone.Role.All)
                {
                    var s = Fixture(out string speaker, "nothing");
                    Set(s, speaker, s.playerId, view);
                    s.story.grudges.Add(new GrudgeState { holderId = speaker, targetId = s.playerId, cause = GrudgeCauses.Story, severity = 100, originWeek = 1, count = 1 });
                    Assert.That(RoleTone.Of(s, speaker, role), Is.EqualTo(RoleTone.Pragmatic), role + " at their view " + view);
                    Assert.That(RoleTone.Read(s, speaker), Is.EqualTo(RoleTone.Neutral), "their view " + view);
                }
        }

        // ------------------------------------------------------------------ reloads, re-renders and purity

        /// <summary>
        /// A save and a load, a copy and a second render each show the very same cards, and reading them changes nothing.
        /// The load here is Newtonsoft's own; <c>RoleTonePersistenceTests</c> goes through the shipping store, which the
        /// subset leaves out.
        /// </summary>
        [Test]
        public void AReloadOrAReRenderShowsTheSameCard()
        {
            var s = Meeting(out _, out _).Snapshot;
            string json = JsonConvert.SerializeObject(s);
            var cards = Cards(s);
            Assert.That(JsonConvert.SerializeObject(s), Is.EqualTo(json), "Reading the cards neither mutates the season nor draws from it.");
            Assert.That(Cards(s), Is.EqualTo(cards), "A second render.");
            var reloaded = JsonConvert.DeserializeObject<EpisodeState>(json, new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace });
            Assert.That(Cards(reloaded), Is.EqualTo(cards), "A reload.");
            Assert.That(Cards(s.Clone()), Is.EqualTo(cards), "A copy.");
        }

        // ------------------------------------------------------------------ the lines

        [Test]
        public void EveryToneAMomentsSpeakerCanWearHasExactlyThreeLines()
        {
            Assert.That(RoleQuotes.Moments, Is.EquivalentTo(new[] { RoleQuotes.VetoAsk, RoleQuotes.KeepTheBlock, RoleQuotes.Pitch, RoleQuotes.HolderOffer, RoleQuotes.QuietTears }));
            foreach (string moment in RoleQuotes.Moments)
            {
                string role = RoleQuotes.RoleOf(moment);
                Assert.That(role, Is.Not.Null, moment);
                Assert.That(RoleQuotes.TonesWithLines(moment), Is.EqualTo(RoleTone.TonesFor(role)), moment + ": a line for every tone its speaker can wear, and none for a tone they cannot.");
                foreach (string tone in RoleTone.TonesFor(role))
                {
                    var lines = RoleQuotes.Lines(moment, tone);
                    Assert.That(lines.Count, Is.EqualTo(RoleQuotes.LinesPerPair), moment + ", " + tone);
                    Assert.That(lines.Distinct().Count(), Is.EqualTo(lines.Count), moment + ", " + tone);
                }
            }
            Assert.That(RoleQuotes.Lines(RoleQuotes.HolderOffer, RoleTone.Threatening), Is.Empty, "The holder is never Threatening (§3.0's table).");
            Assert.That(RoleQuotes.Lines(RoleQuotes.KeepTheBlock, RoleTone.Desperate), Is.Empty);
            Assert.That(RoleQuotes.Lines("speech", RoleTone.Bitter), Is.Empty);
            var every = AllLines().Select(line => line.text).ToList();
            Assert.That(every.Distinct().Count(), Is.EqualTo(every.Count), "No line is said at two moments or for two tones.");
        }

        [Test]
        public void NoLineRunsPastFifteenWords()
        {
            foreach (var line in AllLines())
                Assert.That(RoleQuotes.WordCount(line.text), Is.InRange(1, RoleQuotes.MostWords), line.ToString());
            Assert.That(RoleQuotes.WordCount("Put {TARGET} up."), Is.EqualTo(4), "A target counts as the most it can say.");
        }

        private static readonly Regex Digits = new Regex(@"\d");
        private static readonly Regex NumberWords = new Regex(@"\b(zero|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|twenty|hundred|twice|half|dozen|percent)\b", RegexOptions.IgnoreCase);
        private static readonly Regex TermWords = new Regex(@"\b(final|finale|finals|vote|votes|voted|voting|voter|bloc|blocs|safety|jury|juror|jurors|evict\w*|in return)\b", RegexOptions.IgnoreCase);
        private static readonly Regex PromiseWords = new Regex(@"\b(owe you|i'll owe|i promise|promise you|i swear|my word|guarantee\w*|count on me|i'll vote|favou?rs?|pay you back|make it up to you)\b", RegexOptions.IgnoreCase);
        private static readonly Regex ThreatWords = new Regex(@"(won't forget|regret|enemy|watch your back|you'd better|a problem|personally|be crossing|come after|coming for|pay for|remember that|not done)", RegexOptions.IgnoreCase);

        /// <summary>
        /// A line never states terms - the terms line does - so no number, no price kind and no vote; it promises only
        /// what its moment's rider keeps (a Desperate nominee's favour, on the ask for the veto); and only a Threatening
        /// houseguest threatens.
        /// </summary>
        [Test]
        public void NoLineStatesTermsOrPromisesWhatItsRiderDoesNotKeep()
        {
            foreach (var line in AllLines())
            {
                string text = line.text;
                Assert.That(Digits.IsMatch(text), Is.False, line + ": no figure.");
                Assert.That(NumberWords.IsMatch(text), Is.False, line + ": no number.");
                Assert.That(TermWords.IsMatch(text), Is.False, line + ": no term of the row.");
                bool favour = RoleQuotes.TakesTheRiders(line.moment) && RoleTone.Stakes(line.tone).rider == RoleTone.Riders.OwesAFavour;
                if (PromiseWords.IsMatch(text)) Assert.That(favour, Is.True, line + ": a promise its rider does not keep.");
                if (ThreatWords.IsMatch(text)) Assert.That(line.tone, Is.EqualTo(RoleTone.Threatening), line + ": only a Threatening houseguest threatens.");
            }
            Assert.That(AllLines().Any(line => PromiseWords.IsMatch(line.text)), Is.True, "The favour the Desperate rider writes is said.");
            Assert.That(AllLines().Count(line => ThreatWords.IsMatch(line.text)), Is.GreaterThan(1), "The lint sees the threats it is there to keep in place.");
        }

        [Test]
        public void OnlyAPitchNamesATargetAndEveryLineIsASentence()
        {
            foreach (var line in AllLines())
            {
                bool named = line.text.Contains(RoleQuotes.Target);
                if (named) Assert.That(line.moment, Is.EqualTo(RoleQuotes.Pitch), line + ": only a pitch recommends somebody.");
                Assert.That(line.text.Replace(RoleQuotes.Target, "").IndexOfAny(new[] { '{', '}', '"', '“', '”' }), Is.EqualTo(-1), line.ToString());
                Assert.That(char.IsUpper(line.text[0]) || line.text.StartsWith(RoleQuotes.Target, StringComparison.Ordinal), Is.True, line.ToString());
                Assert.That(".!?".IndexOf(line.text[line.text.Length - 1]) >= 0, Is.True, line.ToString());
                Assert.That(line.text, Is.EqualTo(line.text.Trim()));
            }
        }

        /// <summary>The plan's own lines stand where they are true in every state their tone comes from.</summary>
        [TestCase(RoleQuotes.VetoAsk, RoleTone.Threatening, "Leave me up there and I won't forget it.")]
        [TestCase(RoleQuotes.VetoAsk, RoleTone.Desperate, "Save me and I'll owe you. I mean it.")]
        [TestCase(RoleQuotes.VetoAsk, RoleTone.Friendly, "We're in this together. Pull me off the block.")]
        [TestCase(RoleQuotes.VetoAsk, RoleTone.Earnest, "I'm not asking for a miracle, just a chance to stay.")]
        [TestCase(RoleQuotes.VetoAsk, RoleTone.Calculated, "Run the numbers. Me staying is better for your game.")]
        [TestCase(RoleQuotes.VetoAsk, RoleTone.Pragmatic, "I know we haven't always seen eye to eye, but save me.")]
        [TestCase(RoleQuotes.KeepTheBlock, RoleTone.Bitter, "You've crossed me before. Don't do it again with that veto.")]
        [TestCase(RoleQuotes.KeepTheBlock, RoleTone.Threatening, "Use that veto and you'll have made yourself a problem.")]
        [TestCase(RoleQuotes.KeepTheBlock, RoleTone.Friendly, "I put them up for a reason. Trust my game.")]
        [TestCase(RoleQuotes.KeepTheBlock, RoleTone.Earnest, "I thought hard about this block. Please leave it as it is.")]
        [TestCase(RoleQuotes.KeepTheBlock, RoleTone.Calculated, "I'm asking you not to change my nominations.")]
        [TestCase(RoleQuotes.KeepTheBlock, RoleTone.Pragmatic, "Keep it simple. Leave the nominations alone.")]
        [TestCase(RoleQuotes.Pitch, RoleTone.Threatening, "Put me up and you'd better win next week too.")]
        [TestCase(RoleQuotes.Pitch, RoleTone.Friendly, "You know I'm with you. Put {TARGET} up and keep me off it.")]
        [TestCase(RoleQuotes.Pitch, RoleTone.Calculated, "{TARGET} is the bigger problem for both of us.")]
        [TestCase(RoleQuotes.Pitch, RoleTone.Pragmatic, "I'd put {TARGET} up. Keep me safe.")]
        [TestCase(RoleQuotes.HolderOffer, RoleTone.Bitter, "You put me through it before. This will cost you.")]
        [TestCase(RoleQuotes.HolderOffer, RoleTone.Friendly, "I want to pull you off. I just need something back.")]
        [TestCase(RoleQuotes.HolderOffer, RoleTone.Earnest, "I'd like to use it on you. Meet me halfway.")]
        [TestCase(RoleQuotes.HolderOffer, RoleTone.Calculated, "I could use it on you. Here's what it costs.")]
        [TestCase(RoleQuotes.HolderOffer, RoleTone.Pragmatic, "It's yours, for a price.")]
        public void ThePlansOwnLineIsKept(string moment, string tone, string line) =>
            Assert.That(RoleQuotes.Lines(moment, tone), Has.Member(line));

        // ------------------------------------------------------------------ the pick

        /// <summary>The line is the keyed pick on the occasion: the same occasion the same line, every line in reach, and no draw.</summary>
        [Test]
        public void TheLineIsTheKeyedPickOnTheOccasion()
        {
            var s = Fixture(out string speaker, "nothing");
            string before = JsonConvert.SerializeObject(s);
            var lines = RoleQuotes.Lines(RoleQuotes.VetoAsk, RoleTone.Pragmatic);
            var shown = new HashSet<string>();
            for (int n = 1; n <= 60; n++)
            {
                string occasion = NpcDeals.VetoAskPrefix + n;
                int index = StoryRandom.Index(s, "role-quote:" + occasion, 3);
                Assert.That(RoleQuotes.Index(s, occasion), Is.EqualTo(index));
                string line = RoleQuotes.Line(s, RoleQuotes.VetoAsk, RoleTone.Pragmatic, occasion);
                Assert.That(line, Is.EqualTo(lines[index]));
                Assert.That(RoleQuotes.Of(s, RoleQuotes.VetoAsk, speaker, occasion), Is.EqualTo(line), "The speaker's own tone picks the pair.");
                Assert.That(RoleQuotes.Line(s, RoleQuotes.VetoAsk, RoleTone.Pragmatic, occasion), Is.EqualTo(line));
                shown.Add(line);
            }
            Assert.That(shown, Is.EquivalentTo(lines), "Sixty occasions reach all three lines.");
            Assert.That(JsonConvert.SerializeObject(s), Is.EqualTo(before), "The pick neither stores nor draws.");
            Assert.That(RoleQuotes.Line(s, RoleQuotes.VetoAsk, RoleTone.Pragmatic, null), Is.Null);
            Assert.That(RoleQuotes.Line(s, RoleQuotes.HolderOffer, RoleTone.Desperate, "deal-veto-1"), Is.Null, "No line for a pair the catalogue does not hold.");
            Assert.That(RoleQuotes.Of(s, RoleQuotes.VetoAsk, s.playerId, "deal-veto-1"), Is.Null);
            Assert.That(RoleQuotes.Of(s, "speech", speaker, "deal-veto-1"), Is.Null);
        }

        [Test]
        public void APitchNamesItsTargetByFirstNameOrAsSomeoneElse()
        {
            var s = Fixture(out string speaker, "nothing");
            var target = s.contestants.First(c => !c.isPlayer && c.id != speaker);
            string first = FinalistRead.FirstName(target.name);
            Assert.That(RoleQuotes.Named(s, "{TARGET} is the bigger problem for both of us.", target.id), Is.EqualTo(first + " is the bigger problem for both of us."));
            Assert.That(RoleQuotes.Named(s, "{TARGET} is the bigger problem for both of us.", null), Is.EqualTo("Someone else is the bigger problem for both of us."));
            Assert.That(RoleQuotes.Named(s, "I'd put {TARGET} up. Keep me safe.", "nobody"), Is.EqualTo("I'd put someone else up. Keep me safe."));
            Assert.That(RoleQuotes.Named(s, "Keep it simple.", target.id), Is.EqualTo("Keep it simple."));
            foreach (var line in AllLines().Where(l => l.moment == RoleQuotes.Pitch))
                Assert.That(RoleQuotes.Named(s, line.text, target.id).Split(' ').Length, Is.LessThanOrEqualTo(RoleQuotes.MostWords), line.ToString());
        }

        /// <summary>
        /// A pitch that recommends nobody never says "someone else": every pitch tone has a line that names nobody, and with
        /// no target the keyed pick steps on from a line that names one to the next that does not. With a target, the pick
        /// is the keyed one, named.
        /// </summary>
        [Test]
        public void APitchThatRecommendsNobodySaysALineThatNamesNobody()
        {
            var s = Fixture(out string speaker, "nothing");
            var target = s.contestants.First(c => !c.isPlayer && c.id != speaker);
            Assert.That(RoleQuotes.TonesWithLines(RoleQuotes.Pitch), Is.EqualTo(RoleTone.Ladder));
            foreach (string tone in RoleQuotes.TonesWithLines(RoleQuotes.Pitch))
            {
                var lines = RoleQuotes.Lines(RoleQuotes.Pitch, tone);
                var nameless = lines.Where(line => !RoleQuotes.NamesTheTarget(line)).ToList();
                Assert.That(nameless, Is.Not.Empty, tone + ": a line for a pitch that recommends nobody.");
                var shown = new HashSet<string>();
                for (int n = 1; n <= 60; n++)
                {
                    string occasion = "pitch-card-" + n;
                    int index = RoleQuotes.Index(s, occasion);
                    Assert.That(RoleQuotes.Line(s, RoleQuotes.Pitch, tone, occasion, target.id), Is.EqualTo(RoleQuotes.Named(s, lines[index], target.id)),
                        tone + ", " + occasion + ": the keyed pick, named.");
                    int next = index;
                    while (RoleQuotes.NamesTheTarget(lines[next])) next = (next + 1) % lines.Count;
                    foreach (string nobody in new[] { null, "", "nobody" })
                    {
                        string line = RoleQuotes.Line(s, RoleQuotes.Pitch, tone, occasion, nobody);
                        Assert.That(line, Is.EqualTo(lines[next]), tone + ", " + occasion + ", target " + (nobody ?? "null"));
                        Assert.That(line, Does.Not.Contain(RoleQuotes.SomeoneElse).IgnoreCase, tone + ", " + occasion);
                        Assert.That(RoleQuotes.NamesTheTarget(line), Is.False, tone + ", " + occasion);
                    }
                    shown.Add(RoleQuotes.Line(s, RoleQuotes.Pitch, tone, occasion));
                }
                Assert.That(shown, Is.EquivalentTo(nameless), tone + ": every line that names nobody is in reach.");
            }
        }

        // ------------------------------------------------------------------ fixtures

        private const int Week = 3;

        private static EpisodeState AtRoles(uint seed = 1111)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
            ShippedRules.ApplyFresh(s);
            s.story.rulesVersion = StoryRules.Roles;
            return s;
        }

        /// <summary>
        /// A season at Roles in week 3, its first houseguest made to order: no traits, never on the block, nothing on the
        /// record between them and the player, and the player reading them at 0 - then each condition added.
        /// </summary>
        private static EpisodeState Fixture(out string speaker, params string[] conditions)
        {
            var s = AtRoles();
            s.week = Week;
            var npcs = s.contestants.Where(c => !c.isPlayer).ToList();
            var who = npcs[0];
            speaker = who.id;
            foreach (var c in s.contestants) { c.nominationWeeks.Clear(); c.timesNominated = 0; }
            who.traits = new List<string>();
            s.ledger.power.Clear();
            s.alliances.RemoveAll(a => a.members.Contains(s.playerId));
            Set(s, s.playerId, speaker, 0);
            foreach (string condition in conditions) Add(s, who, npcs, condition);
            return s;
        }

        private static void Add(EpisodeState s, ContestantState who, List<ContestantState> npcs, string condition)
        {
            switch (condition)
            {
                case "nothing": break;
                case "you-nominated-them":
                    Power(s, 1).hohId = s.playerId; Power(s, 1).nominees = new List<string> { who.id, npcs[2].id }; OnTheBlock(who, 1); break;
                case "you-nominated-them-this-week": Power(s, Week).hohId = s.playerId; OnTheBlock(who, Week); break;
                case "on-the-block-before": Power(s, 1).hohId = npcs[1].id; Power(s, 1).nominees = new List<string> { who.id, npcs[2].id }; OnTheBlock(who, 1); break;
                case "replacement":
                    var power = Power(s, Week);
                    power.hohId = npcs[1].id; power.vetoHolderId = npcs[2].id; power.vetoUsed = true;
                    power.savedId = npcs[3].id; power.replacementId = who.id;
                    OnTheBlock(who, Week); break;
                case "you-broke-your-word": s.deals.Add(Word(s, who.id, DealStatus.Broken, s.playerId, 1, 2)); break;
                case "you-broke-your-word-this-week": s.deals.Add(Word(s, who.id, DealStatus.Broken, s.playerId, Week, Week)); break;
                case "you-broke-an-older-word-this-week": s.deals.Add(Word(s, who.id, DealStatus.Broken, s.playerId, 1, Week)); break;
                case "you-broke-a-promise": s.promises.Add(Promise(s, who.id, 1, 2)); break;
                case "you-broke-an-older-promise-this-week": s.promises.Add(Promise(s, who.id, 1, Week)); break;
                case "you-broke-an-oath": OathBroken(s, who, 2); break;
                case "you-broke-an-oath-this-week": OathBroken(s, who, Week); break;
                case "you-kept-your-word": s.deals.Add(Word(s, who.id, DealStatus.Fulfilled, null, 1, 2)); break;
                case "you-kept-your-word-this-week": s.deals.Add(Word(s, who.id, DealStatus.Fulfilled, null, Week, Week)); break;
                case "they-broke-their-word": s.deals.Add(Word(s, who.id, DealStatus.Broken, who.id, 1, 2)); break;
                case "threatening": who.traits.AddRange(new[] { "Competitive", "Stubborn" }); break;
                case "threatening-at-the-line": who.traits.AddRange(new[] { "Competitive", "Loyal" }); break;
                case "bold-not-vengeful": who.traits.Add("Competitive"); break;
                case "earnest": who.traits.Add("Emotional"); break;
                case "warm-short": who.traits.Add("Social"); break;
                case "steady": who.traits.AddRange(new[] { "Analytical", "Strategic" }); break;
                case "steady-at-the-line": who.traits.Add("Analytical"); break;
                case "dishonest": who.traits.Add("Deceptive"); break;
                case "endgame": npcs[5].status = ContestantStatus.Jury; npcs[6].status = ContestantStatus.Jury; break;
                case "seven-in-the-house": npcs[6].status = ContestantStatus.Jury; break;
                case "evicted-this-week": npcs[4].status = ContestantStatus.Evicted; Power(s, Week).evicteeId = npcs[4].id; break;
                case "close": Set(s, s.playerId, who.id, ReplyCards.CloseReading); break;
                case "reading-short-of-close": Set(s, s.playerId, who.id, ReplyCards.CloseReading - 0.1); break;
                case "allied": s.alliances.Add(Pact(s.playerId, who.id)); break;
                default: Assert.Fail("Unknown condition " + condition); break;
            }
        }

        /// <summary>Whether a row's own condition holds of the houseguest, read by the rule's public parts and thresholds.</summary>
        private static bool Holds(EpisodeState s, string speaker, string condition)
        {
            var axes = Personality.Of(s.Find(speaker));
            switch (condition)
            {
                case "you-broke-your-word": return RoleTone.YouBrokeYourWordBefore(s, speaker);
                case "threatening": return axes.Bold >= RoleTone.ThreateningBold && axes.Vengeful >= RoleTone.ThreateningVengeful;
                case "endgame": return RoleTone.Endgame(s);
                case "close": return ReplyCards.ReadsAsClose(s, speaker);
                case "earnest": return axes.Warm >= RoleTone.EarnestWarm;
                case "steady": return axes.Steady >= RoleTone.CalculatedSteady;
                default: Assert.Fail("Unknown condition " + condition); return false;
            }
        }

        private static PowerRow Power(EpisodeState s, int week)
        {
            var row = s.ledger.power.FirstOrDefault(p => p.week == week);
            if (row == null) s.ledger.power.Add(row = new PowerRow { week = week });
            return row;
        }

        private static void OnTheBlock(ContestantState who, int week)
        {
            who.nominationWeeks.Add(week);
            who.timesNominated++;
        }

        /// <summary>A word between the player and a houseguest, settled as the commitment rules settle one: an information swap the player offered.</summary>
        private static DealState Word(EpisodeState s, string with, string status, string brokenBy, int week, int settled) => new DealState
        {
            id = "deal-word-" + s.nextSequence++, type = DealKind.InformationSharing, proposerId = s.playerId, recipientId = with,
            status = status, week = week, trustImpact = DealKind.DefaultTrust(DealKind.InformationSharing),
            brokenById = brokenBy, settledWeek = settled,
        };

        /// <summary>
        /// A promise the player made them and broke, settled as the commitment rules settle one: alliance loyalty, which a
        /// nomination breaks. The page reads its settlement off the record (<see cref="CommitmentReferences.FindPromise"/>).
        /// </summary>
        private static PromiseState Promise(EpisodeState s, string to, int week, int settled) => new PromiseState
        {
            id = "promise-word-" + s.nextSequence++, fromId = s.playerId, toId = to, kind = PromiseKind.AllianceLoyalty,
            status = PromiseStatus.Broken, week = week, brokenById = s.playerId, settledWeek = settled,
        };

        /// <summary>The player's loyalty oath to them, broken by the player's nomination: the oath leaves the list, and its rule writes the breach on the player's arc with them.</summary>
        private static void OathBroken(EpisodeState s, ContestantState who, int week) =>
            s.relationshipArcs.Add(new RelationshipArcState { npcId = who.id, npcName = who.name, weeklyHistory = new List<ArcHistory>
                { new ArcHistory { week = week, delta = -25, reason = "Broke loyalty oath by nominating " + who.name + " in week " + week } } });

        private static AllianceState Pact(string a, string b) =>
            new AllianceState { id = "alliance-role-tone-" + a + "-" + b, name = "The Test Pact", members = new List<string> { a, b }, active = true };

        private static void Set(EpisodeState s, string from, string to, double score)
        {
            var edge = s.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (edge == null) s.relationships.Add(edge = new RelationshipState { fromId = from, toId = to });
            edge.score = score;
        }

        /// <summary>
        /// A season at Roles played through the public engine to week one's veto competition, with the player neither Head of
        /// Household nor on the block. The player then holds the veto, as WaitingOnYouTests' fixture hands it over; both
        /// nominees are warm enough to ask, and the first is Threatening by their traits. The advance into the meeting
        /// files the asks, as the engine always does.
        /// </summary>
        internal static EpisodeEngine Meeting(out string first, out string second)
        {
            for (uint seed = 7001; seed < 7041; seed++)
            {
                var walk = new EpisodeEngine(AtRoles(seed));
                for (int i = 0; i < 200 && walk.Snapshot.phase != EpisodePhase.Veto; i++)
                {
                    var step = walk.Apply(EpisodeEngineTests.NextCommand(walk.Snapshot));
                    Assert.That(step.accepted, Is.True, "seed " + seed + ": " + step.reason);
                }
                var s = walk.Snapshot;
                if (s.phase != EpisodePhase.Veto || s.competitionResolved || s.hohId == s.playerId || s.nominees.Contains(s.playerId)) continue;
                first = s.nominees[0]; second = s.nominees[1];
                s.Find(first).traits = new List<string> { "Competitive", "Stubborn" };
                Set(s, first, s.playerId, 40); Set(s, second, s.playerId, 40);
                s.vetoHolderId = s.playerId;
                if (!s.vetoPlayers.Contains(s.playerId)) s.vetoPlayers[s.vetoPlayers.Count - 1] = s.playerId;
                s.competitionResolved = true;
                s.competitionScores = s.vetoPlayers.Select((id, i) => new CompetitionScore { contestantId = id, score = id == s.playerId ? 100 : i }).ToList();
                var engine = new EpisodeEngine(s);
                var meeting = engine.Apply(Command(s, EpisodeCommandKind.Advance));
                Assert.That(meeting.accepted, Is.True, "seed " + seed + ": " + meeting.reason);
                Assert.That(meeting.state.phase, Is.EqualTo(EpisodePhase.VetoMeeting));
                Assert.That(EpisodeEngine.RolesOn(meeting.state), Is.True);
                return engine;
            }
            Assert.Fail("No seed reached a veto the player could be handed.");
            first = second = null;
            return null;
        }

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind, string target = null, string second = null, string text = null) =>
            new EpisodeCommand
            {
                id = "role-tone-" + kind + "-" + s.revision + "-" + target, actorId = s.playerId, kind = kind,
                targetId = target, secondTargetId = second, text = text, expectedRevision = s.revision, expectedPhase = s.phase,
            };

        private static string[] Tones(EpisodeState s, (string id, string role)[] cards) =>
            cards.Select(card => RoleTone.Of(s, card.id, card.role)).ToArray();

        /// <summary>Every houseguest's card in every role and at every moment: tone, situation, read and line, as text.</summary>
        internal static List<string> Cards(EpisodeState s)
        {
            var cards = new List<string>();
            var npcs = s.contestants.Where(c => !c.isPlayer).ToList();
            for (int n = 0; n < npcs.Count; n++)
            {
                string id = npcs[n].id, target = npcs[(n + 1) % npcs.Count].id;
                foreach (string role in RoleTone.Role.All)
                    cards.Add(id + " " + role + ": " + RoleTone.Of(s, id, role) + " | " + RoleTone.Situation(s, id, role) + " | " + RoleTone.ReadLine(s, id));
                foreach (string moment in RoleQuotes.Moments)
                    cards.Add(id + " " + moment + ": " + RoleQuotes.Of(s, moment, id, "occasion-" + moment + "-" + id, target));
            }
            return cards;
        }

        /// <summary>
        /// Moves everything a card must not read: every houseguest's view of everybody, the player included; their
        /// grudges, moods, stress and memories; pacts and deals among themselves; the season's generator.
        /// </summary>
        private static void Hide(EpisodeState s)
        {
            var npcs = s.contestants.Where(c => !c.isPlayer).ToList();
            int flip = 0;
            foreach (var edge in s.relationships.Where(r => r.fromId != s.playerId)) edge.score = flip++ % 2 == 0 ? 100 : -100;
            foreach (var npc in npcs)
            {
                Set(s, npc.id, s.playerId, 100);
                npc.mood = flip++ % 2 == 0 ? "Happy" : "Angry";
                npc.stressLevel = "Overwhelmed";
                s.story.grudges.Add(new GrudgeState { holderId = npc.id, targetId = s.playerId, cause = GrudgeCauses.Story, severity = 100, originWeek = 1, count = 1 });
                s.memories.Add(new MemoryState { ownerId = npc.id, subjectId = s.playerId, text = "Lied to me in week 1.", week = 1, isPrivate = true });
            }
            s.alliances.Add(Pact(npcs[0].id, npcs[1].id));
            s.deals.Add(new DealState
            {
                id = "deal-hidden-" + s.nextSequence++, type = DealKind.InformationSharing, proposerId = npcs[2].id, recipientId = npcs[3].id,
                status = DealStatus.Broken, week = 1, trustImpact = DealTrust.Medium, brokenById = npcs[2].id, settledWeek = 1,
            });
            s.randomState ^= 0x5bd1e995u;
        }

        private sealed class QuoteLine
        {
            public string moment, tone, text;
            public override string ToString() => moment + " / " + tone + ": \"" + text + "\"";
        }

        private static IEnumerable<QuoteLine> AllLines() =>
            RoleQuotes.Moments.SelectMany(moment => RoleTone.Ladder.SelectMany(tone =>
                RoleQuotes.Lines(moment, tone).Select(text => new QuoteLine { moment = moment, tone = tone, text = text })));
    }
}
