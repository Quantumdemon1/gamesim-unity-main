using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The season report's tapes (UI-UX-PASS-PLAN J0; decision 8): every eviction ballot of a
    /// finished season, read from the record - each ballot's own line while the log holds it, and
    /// what the ledger still proves once it does not - with a ballot marked a lie exactly where a
    /// claim the reveal judged said the other name. Played seasons hold the table to the ballots
    /// the engine counted at each reveal; hand-built weeks hold what the ledger proves after the
    /// log has rolled past a week; and nothing reads a ballot before the finale.
    /// </summary>
    public sealed class SeasonBallotsTests
    {
        // ---------------------------------------------------------------- played seasons

        /// <summary>
        /// An eight-house played to its end, with what the B0 sentinel's fixture gives the reveal to
        /// judge: before every vote the player is told two ballots - one true, one a lie - and
        /// overhears a third voter say the name they will not vote, so each reveal judges a kept
        /// claim and two lies, told and overheard. <paramref name="counted"/> keeps the box the
        /// reveal counted, week by week: the engine's ballots, the Head of Household's tie-break
        /// among them.
        /// </summary>
        private static EpisodeState Played(uint seed, Dictionary<int, List<VoteState>> counted)
        {
            var engine = new EpisodeEngine(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed));
            for (int guard = 0; guard < 400 && engine.Snapshot.phase != EpisodePhase.Finished; guard++)
            {
                var now = engine.Snapshot;
                if (now.phase == EpisodePhase.Eviction && now.evictionStage == EvictionStage.Voting && !now.evictionResolved && now.votes.Count == 0 && now.nominees.Count == 2)
                {
                    var voters = EpisodeEngine.Voters(now).Where(v => !v.isPlayer).ToArray();
                    string Other(string id) => now.nominees.First(nominee => nominee != id);
                    if (voters.Length >= 1)
                        now.ledger.claims.Add(new ClaimRow { week = now.week, voterId = voters[0].id, targetId = EpisodeEngine.ProjectBallot(now, voters[0].id).selectedNomineeId, source = ClaimSource.Told });
                    if (voters.Length >= 2)
                        now.ledger.claims.Add(new ClaimRow { week = now.week, voterId = voters[1].id, targetId = Other(EpisodeEngine.ProjectBallot(now, voters[1].id).selectedNomineeId), source = ClaimSource.Told });
                    if (voters.Length >= 3)
                        now.ledger.claims.Add(new ClaimRow { week = now.week, voterId = voters[2].id, targetId = Other(EpisodeEngine.ProjectBallot(now, voters[2].id).selectedNomineeId), source = ClaimSource.Overheard });
                    engine = new EpisodeEngine(now);
                }
                var result = engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot));
                Assert.That(result.accepted, Is.True, result.reason);
                if (result.state.phase == EpisodePhase.Eviction && result.state.evictionResolved && !now.evictionResolved)
                    counted[result.state.week] = result.state.votes.Select(vote => vote.Clone()).ToList();
            }
            Assert.That(engine.Snapshot.phase, Is.EqualTo(EpisodePhase.Finished), "Seed " + seed + " plays to its finale.");
            return engine.Snapshot;
        }

        [TestCase(1u)]
        [TestCase(2u)]
        [TestCase(3u)]
        [TestCase(4u)]
        [TestCase(5u)]
        [TestCase(6u)]
        public void TheTapesAreTheBallotsTheEngineCountedInAPlayedSeason(uint seed)
        {
            var counted = new Dictionary<int, List<VoteState>>();
            var s = Played(seed, counted);
            var tapes = SeasonBallots.Read(s);
            Assert.That(counted, Is.Not.Empty, "Seed " + seed + " has reveals to read.");
            int complete = 0;
            foreach (var pair in counted)
            {
                string where = "Seed " + seed + ", week " + pair.Key + ": ";
                var week = tapes.SingleOrDefault(w => w.week == pair.Key);
                Assert.That(week, Is.Not.Null, where + "every reveal is on the tapes.");
                var power = s.ledger.power.Last(p => p.week == pair.Key);
                Assert.That(week.final, Is.False);
                Assert.That(week.evictedId, Is.EqualTo(power.evicteeId), where + "who went,");
                Assert.That(week.nominees, Is.EqualTo(power.nominees), where + "from which block,");
                Assert.That(week.tally, Is.EqualTo(power.tally), where + "by the count the reveal read.");
                Assert.That(week.hohId, Is.EqualTo(power.hohId));

                // Every ballot the tapes name is the ballot the engine counted; none is missed but
                // the ones the record no longer places, and those are counted.
                var house = pair.Value.Where(vote => vote.voterId != power.hohId).ToList();
                foreach (var row in week.rows)
                    Assert.That(row.targetId, Is.EqualTo(pair.Value.Single(vote => vote.voterId == row.voterId).targetId),
                        where + s.Find(row.voterId).name + "'s ballot on the tapes is the one the engine counted (" + row.source + ").");
                Assert.That(week.rows.Count(row => !row.tieBreak) + week.Missing, Is.EqualTo(house.Count), where + "every house ballot placed or counted.");
                foreach (var nominee in week.nominees)
                    Assert.That(week.Side(nominee).Count() + week.MissingAgainst(nominee), Is.EqualTo(week.Against(nominee)), where + "each side as the count has it.");

                // The Head of Household votes only to break a tie, and that vote is the tie-break.
                Assert.That(week.tieBroken, Is.EqualTo(power.tally[0] == power.tally[1]));
                Assert.That(week.rows.Where(row => row.tieBreak).Select(row => row.voterId), Is.EqualTo(week.tieBroken ? new[] { power.hohId } : new string[0]),
                    where + "the tie-break marked, and only it.");
                if (week.tieBroken) Assert.That(week.TieBreak.targetId, Is.EqualTo(power.evicteeId));

                // While the log still holds the week's lines, the tapes are whole.
                bool linesKept = s.events.Count(e => e.week == pair.Key && e.kind == "vote-reveal") == pair.Value.Count;
                if (linesKept)
                {
                    Assert.That(week.Complete, Is.True, where + "the log holds every line, so every ballot is on the tapes.");
                    Assert.That(week.rows.Select(row => row.voterId + ">" + row.targetId), Is.EquivalentTo(pair.Value.Select(vote => vote.voterId + ">" + vote.targetId)), where);
                    complete++;
                }
            }
            Assert.That(complete, Is.GreaterThan(0), "Seed " + seed + ": at least one week read whole, or the test proves only a subset.");

            // The last eviction: the final Head of Household's one vote, after every house vote.
            var last = tapes.Last();
            var finalRow = s.ledger.power.Last();
            Assert.That(last.final, Is.True, "The final eviction closes the tapes.");
            Assert.That(last.week, Is.EqualTo(finalRow.week));
            Assert.That(last.rows.Select(row => row.voterId + ">" + row.targetId), Is.EqualTo(new[] { finalRow.hohId + ">" + finalRow.evicteeId }));
            Assert.That(last.FinalChoice, Is.Not.Null);
            Assert.That(tapes.Count, Is.EqualTo(counted.Count + 1), "A week on the tapes for every eviction, and nothing else.");
        }

        /// <summary>
        /// LIED is the lie told to the player's face and nothing else: a told claim the ballot went
        /// against is marked, with what they told the player; an overheard lean the ballot went
        /// against - the engine wrote the voter's lean as it stood when heard - is a vote that
        /// changed, said in its own words with no mark; a claim kept, or none, says nothing.
        /// </summary>
        [Test]
        public void OnlyALieToldToThePlayerIsMarkedAndAnOverheardLeanTheVoteWentAgainstIsSaidPlainly()
        {
            int lies = 0, changed = 0, kept = 0, quiet = 0;
            for (uint seed = 1; seed <= 6; seed++)
            {
                var counted = new Dictionary<int, List<VoteState>>();
                var s = Played(seed, counted);
                foreach (var week in SeasonBallots.Read(s).Where(w => !w.final))
                    foreach (var row in week.rows)
                    {
                        string where = "Seed " + seed + ", week " + week.week + ", " + s.Find(row.voterId).name + ": ";
                        string ballot = counted[week.week].Single(vote => vote.voterId == row.voterId).targetId;
                        var claims = s.ledger.claims.Where(k => k.week == week.week && k.voterId == row.voterId && k.status != ClaimStatus.Open).ToList();
                        bool toldOtherwise = claims.Any(k => k.source == ClaimSource.Told && k.targetId != ballot);
                        bool heardOtherwise = claims.Any(k => k.source != ClaimSource.Told && k.targetId != ballot);
                        Assert.That(row.Lied, Is.EqualTo(toldOtherwise), where + "marked a lie exactly where a claim told to the player named the other nominee.");
                        Assert.That(row.Changed != null, Is.EqualTo(heardOtherwise && !toldOtherwise), where + "a lean overheard the vote went against, said plainly.");
                        if (row.Lied)
                        {
                            lies++;
                            Assert.That(row.Lie.source, Is.EqualTo(ClaimSource.Told));
                            Assert.That(row.Lie.saidId, Is.Not.EqualTo(ballot), where + "the lie names what they said, not what they did.");
                            Assert.That(claims.Any(k => k.targetId == row.Lie.saidId && k.source == ClaimSource.Told && k.status == ClaimStatus.Lied), Is.True,
                                where + "and it is the ledger's own claim, as the reveal judged it.");
                            Assert.That(SeasonBallots.LieWords(s, row), Is.EqualTo("Told you they'd evict " + (row.Lie.saidId == s.playerId ? "you" : s.Find(row.Lie.saidId).name)));
                            Assert.That(SeasonBallots.ChangedWords(s, row), Is.Null, where + "the lie says it; nothing more.");
                            continue;
                        }
                        Assert.That(SeasonBallots.LieWords(s, row), Is.Null, where + "no lie, no lie line.");
                        if (row.Changed != null)
                        {
                            changed++;
                            Assert.That(row.Changed.source, Is.EqualTo(ClaimSource.Overheard), "The fixture's only other claims are overheard.");
                            Assert.That(row.Changed.saidId, Is.Not.EqualTo(ballot));
                            Assert.That(SeasonBallots.ChangedWords(s, row), Is.EqualTo("Overheard saying they'd evict "
                                + (row.Changed.saidId == s.playerId ? "you" : s.Find(row.Changed.saidId).name) + "; voted the other way"));
                        }
                        else
                        {
                            Assert.That(SeasonBallots.ChangedWords(s, row), Is.Null, where + "nothing said against the ballot, nothing to say.");
                            if (claims.Count > 0) kept++;
                            else quiet++;
                        }
                    }
            }
            Assert.That(lies, Is.GreaterThan(0), "A lie told to the player's face, caught and marked.");
            Assert.That(changed, Is.GreaterThan(0), "An overheard lean the vote went against, said without a mark.");
            Assert.That(kept, Is.GreaterThan(0), "A claim kept says nothing.");
            Assert.That(quiet, Is.GreaterThan(0), "A ballot nobody said anything about says nothing.");
        }

        // ---------------------------------------------------------------- hand-built weeks

        /// <summary>
        /// A six-house whose season is over, with week one's count on the record and no ballot's
        /// line left on the log: the first houseguest held the house, the second and third were on
        /// the block. The house's voters that week were the player and the fourth and fifth.
        /// </summary>
        private static EpisodeState RolledOff(int against, int others, uint seed = 31)
        {
            var s = ContentCatalog.Create(seed);
            s.week = 2;
            s.phase = EpisodePhase.Finished;
            var npcs = Npcs(s);
            s.ledger.power.Add(new PowerRow
            {
                week = 1, hohId = npcs[0].id, evicteeId = against >= others ? npcs[1].id : npcs[2].id,
                nominees = new List<string> { npcs[1].id, npcs[2].id }, tally = new List<int> { against, others },
            });
            return s;
        }

        private static ContestantState[] Npcs(EpisodeState s) => s.contestants.Where(c => !c.isPlayer).ToArray();

        private static void Line(EpisodeState s, int week, string voterId, string targetId, bool inTheOpen = false) =>
            s.events.Add(new EpisodeEvent
            {
                sequence = s.nextSequence++, week = week, phase = EpisodePhase.Eviction, kind = "vote-reveal",
                text = s.Find(voterId).name + " voted to evict " + (targetId == s.playerId ? "you" : s.Find(targetId).name) + ". They had their reasons.",
                audienceIds = inTheOpen ? new List<string>() : new List<string> { voterId },
            });

        [Test]
        public void AWeekTheLogHasForgottenKeepsWhatTheLedgerProvesAndCountsTheRest()
        {
            var s = RolledOff(2, 1);
            var npcs = Npcs(s);
            s.ledger.ballots.Add(new BallotRow { week = 1, voterId = s.playerId, targetId = npcs[1].id });

            var week = SeasonBallots.Read(s).Single();
            Assert.That(week.rows.Select(row => (row.voterId, row.targetId, row.source)), Is.EqualTo(new[] { (s.playerId, npcs[1].id, SeasonBallots.Sources.Own) }),
                "The player's own row outlives the log.");
            Assert.That(week.Complete, Is.False);
            Assert.That(week.Missing, Is.EqualTo(2), "A 2-1 count with one placed leaves one each way: counted, never guessed,");
            Assert.That(week.MissingAgainst(npcs[1].id), Is.EqualTo(1));
            Assert.That(week.MissingAgainst(npcs[2].id), Is.EqualTo(1));
            Assert.That(week.unplaced, Is.EqualTo(new[] { npcs[3].id, npcs[4].id }), "and whose they are, the house's voters being certain.");
            Assert.That(SeasonBallots.MissingWords(s, week), Is.EqualTo("Not on the record: " + npcs[3].name + " and " + npcs[4].name + "."));
            Assert.That(SeasonBallots.MissingSideWords(1), Is.EqualTo("1 ballot not on the record."));
            Assert.That(SeasonBallots.MissingSideWords(2), Is.EqualTo("2 ballots not on the record."));

            // A lie the reveal caught names the ballot cast - the other nominee - and the count
            // proves the last one over everything placed.
            s.ledger.claims.Add(new ClaimRow { week = 1, voterId = npcs[3].id, targetId = npcs[1].id, source = ClaimSource.Told, status = ClaimStatus.Lied });
            week = SeasonBallots.Read(s).Single();
            Assert.That(week.Complete, Is.True);
            Assert.That(week.unplaced, Is.Empty);
            var liar = week.rows.Single(row => row.voterId == npcs[3].id);
            Assert.That((liar.targetId, liar.source, liar.Lied), Is.EqualTo((npcs[2].id, SeasonBallots.Sources.Claim, true)));
            Assert.That(liar.Lie.saidId, Is.EqualTo(npcs[1].id));
            Assert.That(SeasonBallots.LieWords(s, liar), Is.EqualTo("Told you they'd evict " + npcs[1].name));
            var last = week.rows.Single(row => row.voterId == npcs[4].id);
            Assert.That((last.targetId, last.source, last.Lied), Is.EqualTo((npcs[1].id, SeasonBallots.Sources.Count, false)), "Proven by the count, and no claim, so no mark.");
            Assert.That(week.rows.Select(row => row.voterId), Is.EqualTo(new[] { s.playerId, npcs[3].id, npcs[4].id }.OrderBy(id => s.contestants.FindIndex(c => c.id == id))),
                "The house in the cast's order.");
            Assert.That(week.Side(npcs[1].id).Select(row => row.voterId), Is.EquivalentTo(new[] { s.playerId, npcs[4].id }));
            Assert.That(week.Side(npcs[2].id).Select(row => row.voterId), Is.EqualTo(new[] { npcs[3].id }));
        }

        [Test]
        public void TheBallotsOwnLinesAreReadPrivateOrInTheOpenAndTheCountProvesTheRest()
        {
            var s = RolledOff(2, 1);
            var npcs = Npcs(s);
            Line(s, 1, npcs[3].id, npcs[2].id);
            var week = SeasonBallots.Read(s).Single();
            Assert.That(week.Complete, Is.True, "The minority's ballot from its line; the count puts the other two against the one who went.");
            Assert.That(week.rows.Single(row => row.voterId == npcs[3].id).source, Is.EqualTo(SeasonBallots.Sources.Line));
            Assert.That(week.rows.Where(row => row.voterId != npcs[3].id).Select(row => (row.targetId, row.source)),
                Is.All.EqualTo((npcs[1].id, SeasonBallots.Sources.Count)));

            // A save from before ballots went private kept every line in the open.
            var older = RolledOff(2, 1);
            Line(older, 1, npcs[3].id, npcs[2].id, inTheOpen: true);
            Line(older, 1, older.playerId, npcs[1].id, inTheOpen: true);
            week = SeasonBallots.Read(older).Single();
            Assert.That(week.rows.Single(row => row.voterId == npcs[3].id).source, Is.EqualTo(SeasonBallots.Sources.Revealed));
            Assert.That(week.rows.Single(row => row.voterId == older.playerId).targetId, Is.EqualTo(npcs[1].id));
            Assert.That(week.Complete, Is.True);

            // Lines of another week, or of another kind, are not this week's ballots.
            var other = RolledOff(2, 1);
            Line(other, 2, npcs[3].id, npcs[2].id);
            other.events.Add(new EpisodeEvent { week = 1, kind = "private-vote", text = "Your eviction vote was recorded.", audienceIds = new List<string> { npcs[4].id } });
            Assert.That(SeasonBallots.Read(other).Single().rows, Is.Empty);
        }

        [Test]
        public void ATieBrokenWeekMarksTheHeadOfHouseholdsDecidingVote()
        {
            var s = ContentCatalog.Create(31);
            s.week = 2;
            s.phase = EpisodePhase.Finished;
            var npcs = Npcs(s);
            // The fifth houseguest left before the vote by the record, so the house's voters were the
            // player and the fourth: one each way, and the first, holding the house, broke it.
            npcs[4].status = ContestantStatus.Evicted;
            s.ledger.power.Add(new PowerRow
            {
                week = 1, hohId = npcs[0].id, evicteeId = npcs[1].id,
                nominees = new List<string> { npcs[1].id, npcs[2].id }, tally = new List<int> { 1, 1 },
            });
            s.ledger.ballots.Add(new BallotRow { week = 1, voterId = s.playerId, targetId = npcs[2].id });
            Line(s, 1, npcs[3].id, npcs[1].id);
            var week = SeasonBallots.Read(s).Single();
            Assert.That(week.tieBroken, Is.True);
            Assert.That(week.Complete, Is.True);
            Assert.That(week.TieBreak, Is.Not.Null, "The deciding vote is on the tapes,");
            Assert.That((week.TieBreak.voterId, week.TieBreak.targetId), Is.EqualTo((npcs[0].id, npcs[1].id)), "the Head of Household's, against the one who went,");
            Assert.That(week.rows.Last(), Is.SameAs(week.TieBreak), "after the house's,");
            Assert.That(week.Side(npcs[1].id).Select(row => row.voterId), Is.EqualTo(new[] { npcs[3].id }), "and outside the house's count.");
            Assert.That(SeasonBallots.Eyebrow(week), Is.EqualTo("WEEK 1 · TIE BROKEN"));
            // In the words the house heard it in: the engine's eviction line, from the week's own Head of Household.
            Assert.That(SeasonBallots.CountWords(s, week), Is.EqualTo("By a vote of 1 to 1; " + npcs[0].name + " broke the tie."));
            s.hohId = npcs[3].id;
            Assert.That(SeasonBallots.CountWords(s, week), Is.EqualTo(EpisodeEngine.CountTail(s, npcs[1].id, new[] { (npcs[1].id, 1), (npcs[2].id, 1) }).Replace(npcs[3].name, npcs[0].name)),
                "The engine's own tail, with the week's Head of Household where it reads today's.");
            Assert.That(SeasonBallots.TieBreakWords(s, week), Is.EqualTo("Head of Household: broke the tie to evict " + npcs[1].name + "."));
        }

        [Test]
        public void TheFinalEvictionIsTheLastHeadOfHouseholdsSoleVote()
        {
            var s = RolledOff(3, 0);
            var npcs = Npcs(s);
            s.ledger.power.Add(new PowerRow { week = 2, hohId = npcs[3].id, evicteeId = npcs[4].id, nominees = new List<string> { s.playerId, npcs[4].id } });
            var tapes = SeasonBallots.Read(s);
            Assert.That(tapes.Select(week => week.week), Is.EqualTo(new[] { 1, 2 }), "Oldest first.");
            Assert.That(tapes[0].Complete, Is.True, "A unanimous count proves every ballot.");
            Assert.That(tapes[0].rows.Select(row => row.targetId), Is.All.EqualTo(npcs[1].id));
            var final = tapes[1];
            Assert.That(final.final, Is.True);
            Assert.That(final.rows.Select(row => (row.voterId, row.targetId, row.finalChoice)), Is.EqualTo(new[] { (npcs[3].id, npcs[4].id, true) }));
            Assert.That(final.KeptId, Is.EqualTo(s.playerId));
            Assert.That(final.Sides, Is.Empty, "No house vote, so no sides.");
            Assert.That(SeasonBallots.Eyebrow(final), Is.EqualTo("WEEK 2 · FINAL EVICTION"));
            Assert.That(SeasonBallots.Title(s, final), Is.EqualTo(npcs[4].name + " evicted"));
            Assert.That(SeasonBallots.CountWords(s, final), Is.EqualTo("The final Head of Household's sole vote."));
            Assert.That(SeasonBallots.FinalWords(s, final), Is.EqualTo("Final Head of Household: took you to the final two."));
        }

        [Test]
        public void NothingIsReadBeforeTheFinaleAndReadingWritesNothing()
        {
            var s = RolledOff(2, 1);
            var npcs = Npcs(s);
            Line(s, 1, npcs[3].id, npcs[2].id);
            s.ledger.claims.Add(new ClaimRow { week = 1, voterId = npcs[4].id, targetId = npcs[2].id, source = ClaimSource.Told, status = ClaimStatus.Lied });
            foreach (EpisodePhase phase in Enum.GetValues(typeof(EpisodePhase)))
            {
                s.phase = phase;
                if (phase == EpisodePhase.Finished) continue;
                Assert.That(SeasonBallots.Open(s), Is.False, phase + ": sealed.");
                Assert.That(SeasonBallots.Read(s), Is.Empty, phase + ": the tapes read nothing before the finale.");
            }
            s.phase = EpisodePhase.Finished;
            Assert.That(SeasonBallots.Open(s), Is.True);
            string before = JsonConvert.SerializeObject(s);
            var week = SeasonBallots.Read(s).Single();
            Assert.That(week.Complete, Is.True);
            Assert.That(JsonConvert.SerializeObject(s), Is.EqualTo(before), "Reading the tapes writes nothing.");
            Assert.That(SeasonBallots.Read(null), Is.Empty);
            Assert.That(SeasonBallots.Open(null), Is.False);
        }

        [Test]
        public void TheTablesWordsSayTheCountAndTheLiesAsTheHouseDoes()
        {
            var s = RolledOff(2, 1);
            var npcs = Npcs(s);
            var week = SeasonBallots.Read(s).Single();
            Assert.That(SeasonBallots.Eyebrow(week), Is.EqualTo("WEEK 1"));
            Assert.That(SeasonBallots.Title(s, week), Is.EqualTo(npcs[1].name + " evicted"));
            Assert.That(SeasonBallots.CountWords(s, week), Is.EqualTo("By a vote of 2 to 1."));
            Assert.That(SeasonBallots.SideHeading(s, week, npcs[1].id), Is.EqualTo("TO EVICT " + FinalistRead.FirstName(npcs[1].name).ToUpperInvariant() + " (2)"));
            Assert.That(SeasonBallots.SideHeading(s, week, npcs[2].id), Is.EqualTo("TO EVICT " + FinalistRead.FirstName(npcs[2].name).ToUpperInvariant() + " (1)"));
            Assert.That(week.Sides, Is.EqualTo(new[] { npcs[1].id, npcs[2].id }), "The one who went first.");

            var single = RolledOff(1, 0);
            Assert.That(SeasonBallots.CountWords(single, SeasonBallots.Read(single).Single()), Is.EqualTo("By a single vote."));

            // The player on the block and sent home: spoken to, never named.
            var you = RolledOff(2, 1);
            var block = you.ledger.power[0];
            block.nominees = new List<string> { you.playerId, Npcs(you)[2].id };
            block.evicteeId = you.playerId;
            var yours = SeasonBallots.Read(you).Single();
            Assert.That(SeasonBallots.Title(you, yours), Is.EqualTo("You were evicted"));
            Assert.That(SeasonBallots.SideHeading(you, yours, you.playerId), Is.EqualTo("TO EVICT YOU (2)"));
            Assert.That(yours.Sides.First(), Is.EqualTo(you.playerId));

            // A lean the vote went against, heard or reported, is said plainly, in its source's words, and is no lie.
            var row = new SeasonBallots.Row { voterId = npcs[3].id, targetId = npcs[2].id };
            row.said.Add(new SeasonBallots.Said { saidId = npcs[1].id, source = ClaimSource.Ally, lied = true });
            Assert.That(row.Lied, Is.False, "An ally's account the vote went against is not a lie told to the player.");
            Assert.That(SeasonBallots.LieWords(s, row), Is.Null);
            Assert.That(SeasonBallots.ChangedWords(s, row), Is.EqualTo("Told the pact they'd evict " + npcs[1].name + "; voted the other way"),
                "An ally's account is their own word to the pact (C6).");
            row.said.Insert(0, new SeasonBallots.Said { saidId = s.playerId, source = ClaimSource.Overheard, lied = true });
            Assert.That(row.Lied, Is.False, "Nor is a lean overheard.");
            Assert.That(SeasonBallots.ChangedWords(s, row), Is.EqualTo("Overheard saying they'd evict you; voted the other way"), "The surest source speaks for it.");

            // A lie told to the player's face is the mark, and says it alone.
            row.said.Insert(0, new SeasonBallots.Said { saidId = npcs[1].id, source = ClaimSource.Told, lied = true });
            Assert.That(row.Lied, Is.True);
            Assert.That(SeasonBallots.LieWords(s, row), Is.EqualTo("Told you they'd evict " + npcs[1].name));
            Assert.That(row.Changed, Is.Null);
            Assert.That(SeasonBallots.ChangedWords(s, row), Is.Null);

            // A claim kept says nothing.
            row.said.Clear();
            row.said.Add(new SeasonBallots.Said { saidId = npcs[2].id, source = ClaimSource.Told, lied = false });
            row.said.Add(new SeasonBallots.Said { saidId = npcs[2].id, source = ClaimSource.Overheard, lied = false });
            Assert.That(row.Lied, Is.False);
            Assert.That(SeasonBallots.LieWords(s, row), Is.Null, "A claim kept is no lie,");
            Assert.That(SeasonBallots.ChangedWords(s, row), Is.Null, "and no change of vote.");
        }

        /// <summary>
        /// An eviction the ledger has no week for - a season begun before the ledger kept them - is
        /// not left out in silence: the houseguests' own statuses count more evictions than the
        /// tapes hold weeks, and the intro says the record is not whole, as it does for a week whose
        /// lines are gone.
        /// </summary>
        [Test]
        public void AnEvictionWithNoWeekOnTheLedgerLeavesTheTapesNotWhole()
        {
            var s = RolledOff(3, 0);
            var npcs = Npcs(s);
            npcs[1].status = ContestantStatus.Jury;
            var tapes = SeasonBallots.Read(s);
            Assert.That(tapes.Single().Complete, Is.True, "Week one is whole: a unanimous count proves it.");
            Assert.That(SeasonBallots.WeeksNotOnRecord(s, tapes), Is.Zero);
            Assert.That(SeasonBallots.Whole(s, tapes), Is.True);
            Assert.That(SeasonBallots.IntroFor(s, tapes), Is.EqualTo(SeasonBallots.Intro));

            // The one who survived week one's block went in a week the ledger never kept.
            npcs[2].status = ContestantStatus.Evicted;
            tapes = SeasonBallots.Read(s);
            Assert.That(tapes.Select(week => week.week), Is.EqualTo(new[] { 1 }), "The tapes read the weeks the ledger kept,");
            Assert.That(tapes.Single().Complete, Is.True);
            Assert.That(SeasonBallots.WeeksNotOnRecord(s, tapes), Is.EqualTo(1), "and count the eviction they do not hold,");
            Assert.That(SeasonBallots.Whole(s, tapes), Is.False);
            Assert.That(SeasonBallots.IntroFor(s, tapes), Is.EqualTo(SeasonBallots.Intro + " " + SeasonBallots.IntroIncomplete), "so the intro says the record is not whole.");

            // Production's removals are no eviction.
            npcs[2].status = ContestantStatus.Expelled;
            Assert.That(SeasonBallots.Whole(s, SeasonBallots.Read(s)), Is.True);

            // A week that has lost lines is not whole either.
            var forgotten = RolledOff(2, 1);
            Assert.That(SeasonBallots.IntroFor(forgotten, SeasonBallots.Read(forgotten)), Is.EqualTo(SeasonBallots.Intro + " " + SeasonBallots.IntroIncomplete));
        }
    }
}
