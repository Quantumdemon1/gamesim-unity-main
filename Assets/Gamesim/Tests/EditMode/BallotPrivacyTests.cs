using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The privacy sentinel over every reader the simulation offers the screens (UI-UX-PASS-PLAN
    /// B0): on played seasons, a ballot the player cannot place is never printed - no line of Your
    /// week, the houseguest notes, Your word, the finalist read, the jury house, the finale's
    /// receipts or the player-visible log names that voter beside the nominee they voted against.
    /// The true ballots come from the private lines the engine logs to each voter, which a test may
    /// read and a screen never does.
    ///
    /// <para>Where it stops (UI-UX-PASS-PLAN J0, decision 8): the season report's table of every
    /// eviction ballot, <see cref="SeasonBallots"/>, is the one reader that names a ballot the
    /// player never learned, and it opens only when the season is over. This sentinel holds the
    /// readers above to the rule on finished seasons, every reveal of the season on the record by
    /// then (the PlayMode sentinel holds the screens at a reveal mid-season); the table is not
    /// among them, and instead is read at every state a played season passes through before the
    /// finale and found to read nothing (<see cref="TheTapesAreSealedUntilTheFinaleAndOpenThere"/>).</para>
    /// </summary>
    public sealed class BallotPrivacyTests
    {
        /// <summary>The verbs a line says a ballot with, after the voter's name in the same sentence.</summary>
        private static readonly string[] BallotVerbs = { "voted", "votes", "is voting", "broke", "kept", "honoured", "followed", "ignored", "lied", "defected", "fell out" };

        /// <summary>
        /// Lines that are a record of what was said, never a ballot read, by the words they open
        /// with: a voter's own stated lean on a whip count ("Says: evict") and the claims the player
        /// gathered ("Overheard", and an ally's own word at a meeting of the player's pact, "Told the
        /// pact" - ACTIONS-DEALS-ALLIANCES-PLAN C6). Nothing else is excused by its words; the jury's
        /// ballots at the finale are public (decision 7) and are left out by their kind.
        /// </summary>
        private static readonly string[] AccountPrefixes = { "Says: evict", "Overheard", "Told the pact" };

        /// <summary>The kinds of line the finale reads in the open: the jury's ballots and the crowning (decision 7).</summary>
        private static readonly string[] PublicFinaleKinds = { "jury-vote", "jury-tie", "winner" };

        /// <summary>
        /// An eight-house played to its end - the catalogue's six-house has three voters a week, and
        /// two claims with the count's proof place all three - and, before every reveal, what the
        /// PlayMode sentinel's fixture gives the engine to judge: two claims the player was told, one
        /// true and one a lie, and a vote promise from a third voter for the nominee they will not vote
        /// against, so the record holds lies caught and verdicts the player cannot place.
        /// <paramref name="each"/>, where given, sees every state the season passes through.
        /// </summary>
        private static EpisodeState Played(uint seed, Action<EpisodeState> each = null)
        {
            var engine = new EpisodeEngine(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed));
            for (int guard = 0; guard < 400 && engine.Snapshot.phase != EpisodePhase.Finished; guard++)
            {
                var now = engine.Snapshot;
                if (now.phase == EpisodePhase.Eviction && now.evictionStage == EvictionStage.Voting && !now.evictionResolved && now.votes.Count == 0 && now.nominees.Count == 2)
                {
                    var voters = EpisodeEngine.Voters(now).Where(v => !v.isPlayer).ToArray();
                    if (voters.Length >= 2)
                    {
                        string first = EpisodeEngine.ProjectBallot(now, voters[0].id).selectedNomineeId;
                        now.ledger.claims.Add(new ClaimRow { week = now.week, voterId = voters[0].id, targetId = first, source = ClaimSource.Told });
                        string second = EpisodeEngine.ProjectBallot(now, voters[1].id).selectedNomineeId;
                        now.ledger.claims.Add(new ClaimRow { week = now.week, voterId = voters[1].id, targetId = now.nominees.First(id => id != second), source = ClaimSource.Told });
                    }
                    if (voters.Length >= 3 && !now.promises.Any(p => p.status == PromiseStatus.Active && p.fromId == voters[2].id && p.toId == now.playerId && p.kind == PromiseKind.Vote))
                    {
                        string third = EpisodeEngine.ProjectBallot(now, voters[2].id).selectedNomineeId;
                        now.promises.Add(new PromiseState { id = "sentinel-promise-" + now.week, fromId = voters[2].id, toId = now.playerId, targetId = now.nominees.First(id => id != third),
                            kind = PromiseKind.Vote, status = PromiseStatus.Active, week = now.week, expiresWeek = now.week });
                    }
                    engine = new EpisodeEngine(now);
                }
                var result = engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot));
                Assert.That(result.accepted, Is.True);
                each?.Invoke(result.state);
            }
            Assert.That(engine.Snapshot.phase, Is.EqualTo(EpisodePhase.Finished));
            return engine.Snapshot;
        }

        /// <summary>The true ballots the engine logged to their voters, by week: voter, target. Never the player's.</summary>
        private static List<(int week, string voterId, string targetId)> PrivateBallots(EpisodeState s)
        {
            var ballots = new List<(int, string, string)>();
            foreach (var line in s.events.Where(e => e.kind == "vote-reveal" && e.audienceIds != null && e.audienceIds.Count == 1 && e.audienceIds[0] != s.playerId))
            {
                var read = KnownBallots.ReadRevealLine(s, line.text, null);
                if (read?.voterId != null && read.targetId != null) ballots.Add((line.week, read.voterId, read.targetId));
            }
            return ballots;
        }

        /// <summary>
        /// Whether a line says the voter's ballot: in any sentence of it, the voter's name as the
        /// sentence's subject - the first houseguest it names - followed by a ballot verb ("Casey
        /// Wilson voted", "Casey Wilson broke", "Casey Wilson was with you at the call, then voted"),
        /// with the nominee it went against named anywhere in the line. A name in object position
        /// ("Jamie told you Casey and voted Riley") is not the subject. The words that say a ballot is
        /// not known (KnownBallots.Unresolved) are not a ballot said.
        /// </summary>
        private static bool NamesTheBallot(EpisodeState s, string line, string voterId, string targetId)
        {
            if (string.IsNullOrEmpty(line)) return false;
            string voter = s.Find(voterId)?.name, target = s.Find(targetId)?.name;
            if (voter == null || target == null) return false;
            if (AccountPrefixes.Any(prefix => line.StartsWith(prefix, StringComparison.Ordinal))) return false;
            string said = line.Replace(KnownBallots.Unresolved, string.Empty);
            string targetWord = targetId == s.playerId ? "you" : FinalistRead.FirstName(target);
            if (said.IndexOf(targetWord, StringComparison.Ordinal) < 0) return false;
            foreach (var sentence in Sentences(said))
            {
                int at = sentence.IndexOf(voter, StringComparison.Ordinal);
                if (at < 0 || !LeadsTheSentence(s, sentence, at)) continue;
                string after = sentence.Substring(at + voter.Length);
                if (BallotVerbs.Any(verb => after.IndexOf(" " + verb, StringComparison.Ordinal) >= 0)) return true;
            }
            return false;
        }

        /// <summary>Whether the name at <paramref name="at"/> is the first houseguest the sentence names: its subject, as the engine writes a ballot's line.</summary>
        private static bool LeadsTheSentence(EpisodeState s, string sentence, int at) =>
            !s.contestants.Any(c => !string.IsNullOrEmpty(c.name) && sentence.IndexOf(c.name, StringComparison.Ordinal) is int first && first >= 0 && first < at);

        /// <summary>A line's sentences: split at a full stop, a question mark or an exclamation mark followed by a space, and at a break.</summary>
        private static IEnumerable<string> Sentences(string line) =>
            line.Split(new[] { ". ", "? ", "! ", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        /// <summary>
        /// Every line the simulation's readers offer the screens, for the player, dated "Week N: "
        /// where the reader dates it (a week's page, a note, a memory), as the screens date them.
        /// </summary>
        private static List<string> ReaderLines(EpisodeState s)
        {
            var lines = new List<string>();
            foreach (int week in Enumerable.Range(1, s.week)) lines.AddRange(YourWeek.Build(s, week).Lines.Select(l => "Week " + week + ": " + l));
            foreach (var actor in s.contestants.Where(c => !c.isPlayer))
            {
                foreach (var note in HouseguestNotes.For(s, actor.id))
                {
                    lines.Add("Week " + note.week + ": " + note.text);
                    if (note.brief != null) lines.Add("Week " + note.week + ": " + note.brief);
                    if (note.compact != null) lines.Add("Week " + note.week + ": " + note.compact);
                }
                lines.AddRange(CommitmentsRead.With(s, actor.id).Select(CommitmentsRead.Line));
            }
            foreach (var finalist in FinalistRead.Others(s))
            {
                var read = FinalistRead.Read(s, finalist.id);
                if (read == null) continue;
                lines.AddRange(read.Facts.Select(fact => fact.label + ": " + fact.value));
                lines.AddRange(read.jurors.Select(lean => finalist.name + " · " + lean.name + " · " + lean.reason));
            }
            var house = JuryHouseRead.Read(s);
            if (house != null) lines.AddRange(house.highlights);
            foreach (var juror in FinalistRead.Jurors(s))
            {
                var read = JuryHouseRead.ReadJuror(s, juror.id);
                lines.AddRange(read.knows); lines.AddRange(read.highlights); lines.Add(read.reason);
            }
            foreach (var exchange in s.juryExchanges)
            {
                lines.Add(FinaleQuestions.ReceiptLine(s, exchange));
                lines.Add(FinaleQuestions.Kicker(s, exchange));
            }
            lines.AddRange(s.events.Where(e => (e.audienceIds == null || e.audienceIds.Count == 0 || e.audienceIds.Contains(s.playerId)) && !PublicFinaleKinds.Contains(e.kind))
                .Select(e => "Week " + e.week + ": " + e.text));
            // The player's memories as every screen prints them (the notebook's story and notes, the
            // diary room, a profile, the web's secrets all read this one list).
            lines.AddRange(KnownBallots.PlayerMemories(s).Select(m => "Week " + m.week + ": " + m.text));
            return lines.Where(line => line != null).ToList();
        }

        /// <summary>A line as the engine wrote it, without the "Week 3: " or "Week 3 · " a screen dates it with.</summary>
        private static string Unprefixed(string line)
        {
            if (line == null || !line.StartsWith("Week ", StringComparison.Ordinal)) return line;
            int colon = line.IndexOf(": ", StringComparison.Ordinal), dot = line.IndexOf(" · ", StringComparison.Ordinal);
            int cut = colon >= 0 && (dot < 0 || colon < dot) ? colon + 2 : dot >= 0 ? dot + 3 : -1;
            return cut >= 0 ? line.Substring(cut) : line;
        }

        /// <summary>The week a line is dated to by its "Week 3: " or "Week 3 · ", or null where it carries none.</summary>
        private static int? WeekOf(string line)
        {
            if (line == null || !line.StartsWith("Week ", StringComparison.Ordinal)) return null;
            int end = 5;
            while (end < line.Length && char.IsDigit(line[end])) end++;
            return end > 5 && end < line.Length && (line[end] == ':' || line[end] == ' ') ? int.Parse(line.Substring(5, end - 5)) : (int?)null;
        }

        [Test]
        public void NoReaderNamesABallotThePlayerCannotPlace()
        {
            int unknown = 0, seasons = 0, lies = 0, withheld = 0;
            for (uint seed = 1; seed <= 12; seed++)
            {
                var s = Played(seed);
                var ballots = PrivateBallots(s);
                if (ballots.Count == 0) continue;
                seasons++;
                lies += s.ledger.claims.Count(k => k.status == ClaimStatus.Lied);
                withheld += s.memories.Count(m => m.ownerId == s.playerId && KnownBallots.TellsAnUnknownBallot(s, m.subjectId, m.text, m.week));
                var lines = ReaderLines(s);
                foreach (var (week, voterId, targetId) in ballots)
                {
                    var sheet = KnownBallots.Read(s, week);
                    if (sheet.Knows(voterId)) continue;
                    unknown++;
                    foreach (var line in lines)
                    {
                        // A line dated to another week is that week's: held against that week's ballots.
                        if (WeekOf(line) is int dated && dated != week) continue;
                        Assert.That(NamesTheBallot(s, line, voterId, targetId), Is.False,
                            "Seed " + seed + ", week " + week + ": " + s.Find(voterId).name + "'s ballot is not the player's to know, and a reader says it: \"" + line + "\"");
                        // The engine's own verdict line for a vote deal or a vote promise with them tells the ballot by itself.
                        Assert.That(KnownBallots.TellsAnUnknownBallot(s, voterId, Unprefixed(line), week), Is.False,
                            "Seed " + seed + ", week " + week + ": " + s.Find(voterId).name + "'s ballot is not the player's to know, and a reader tells its verdict: \"" + line + "\"");
                    }
                }
            }
            Assert.That(seasons, Is.GreaterThan(0), "Seasons with ballots on the record.");
            Assert.That(unknown, Is.GreaterThan(0), "At least one ballot the player could not place, or the sentinel guards nothing.");
            Assert.That(lies, Is.GreaterThan(0), "At least one lie caught at a reveal, or the sentinel never sees a judged claim.");
            Assert.That(withheld, Is.GreaterThan(0), "At least one verdict the player cannot place, or the memory readers guard nothing.");
        }

        /// <summary>
        /// The sentinel's one exception, and where it begins (UI-UX-PASS-PLAN J0, decision 8). At
        /// every state a played season passes through before the jury crowns its winner, the
        /// season report's table is sealed and reads not one ballot, so nothing it draws can name a
        /// ballot the player cannot place; at the finale it opens and reads every ballot the record
        /// holds - the ones the player never placed among them, each as the engine logged it.
        /// </summary>
        [Test]
        public void TheTapesAreSealedUntilTheFinaleAndOpenThere()
        {
            int sealedStates = 0, opened = 0, neverLearned = 0;
            for (uint seed = 1; seed <= 6; seed++)
            {
                var s = Played(seed, state =>
                {
                    if (state.phase == EpisodePhase.Finished) return;
                    Assert.That(SeasonBallots.Open(state), Is.False, "Week " + state.week + ", " + state.phase + ": the tapes are sealed.");
                    Assert.That(SeasonBallots.Read(state), Is.Empty, "Week " + state.week + ", " + state.phase + ": the table reads no ballot before the finale.");
                    sealedStates++;
                });
                Assert.That(SeasonBallots.Open(s), Is.True, "Seed " + seed + ": the finale opens the tapes.");
                var tapes = SeasonBallots.Read(s);
                foreach (var (week, voterId, targetId) in PrivateBallots(s))
                {
                    var row = tapes.Single(w => w.week == week).rows.SingleOrDefault(r => r.voterId == voterId);
                    Assert.That(row?.targetId, Is.EqualTo(targetId), "Seed " + seed + ", week " + week + ": " + s.Find(voterId).name + "'s ballot is on the tapes as it was cast.");
                    opened++;
                    if (!KnownBallots.Read(s, week).Knows(voterId)) neverLearned++;
                }
            }
            Assert.That(sealedStates, Is.GreaterThan(100), "Every state before the finale was read sealed.");
            Assert.That(opened, Is.GreaterThan(0));
            Assert.That(neverLearned, Is.GreaterThan(0), "The finale names ballots the player never placed, or decision 8 opens nothing.");
        }

        /// <summary>A finalist's lean of "voted to evict them" rests on a ballot the player knows, and the jury house's "the count showed it" on one the juror could.</summary>
        [Test]
        public void TheFinalistReadAndTheJuryHouseRestOnKnownBallots()
        {
            int leans = 0;
            for (uint seed = 1; seed <= 12; seed++)
            {
                var s = Played(seed);
                foreach (var finalist in FinalistRead.Others(s))
                {
                    var read = FinalistRead.Read(s, finalist.id);
                    if (read == null) continue;
                    foreach (var lean in read.jurors.Where(l => l.reason == "voted to evict them"))
                    {
                        leans++;
                        Assert.That(KnownBallots.Weeks(s).Any(week => KnownBallots.Read(s, week).TargetOf(finalist.id) == lean.jurorId), Is.True,
                            "Seed " + seed + ": " + finalist.name + "'s ballot against " + lean.name + " is one the player knows.");
                    }
                }
                foreach (var juror in FinalistRead.Jurors(s))
                {
                    var read = JuryHouseRead.ReadJuror(s, juror.id);
                    Assert.That(read.knows.Concat(read.highlights), Has.None.Contains("in the open"));
                    foreach (var line in read.knows.Where(k => k.StartsWith("You voted to evict them", StringComparison.Ordinal)))
                        Assert.That(s.ledger.ballots.Any(b => b.voterId == s.playerId && b.targetId == juror.id && JuryHouseRead.CouldKnowYourBallot(s, b.week, juror.id)), Is.True, line);
                }
            }
            Assert.That(leans, Is.GreaterThanOrEqualTo(0));
        }

        /// <summary>
        /// The reveal logs no line of its own (rule 3): the story mints its cycle and house-event ids
        /// from the season's sequence and keys its draws on those ids, so one more event at the reveal
        /// would re-roll every recorded season from its first eviction. The sequence after each of
        /// the first two reveals and the ids the story holds after the second are pinned to what
        /// dbe6199 - the commit before this slice - produced for the same seeds under the same
        /// driver; the reveal's own events are the public eviction line, carrying the count, and one
        /// private line a ballot.
        /// </summary>
        [TestCase(1u, 53, 144, "story-55,story-57,story-59,story-63,story-124", "house-event-60,house-event-64,house-event-91,house-event-125")]
        [TestCase(2u, 53, 143, "story-55,story-57,story-59,story-63,story-103,story-124", "house-event-60,house-event-64,house-event-89,house-event-125")]
        [TestCase(3u, 53, 147, "story-55,story-57,story-59,story-63,story-102,story-105,story-124", "house-event-60,house-event-64,house-event-87,house-event-103,house-event-106,house-event-125")]
        public void TheRevealLogsNoLineOfItsOwnSoARecordedSeasonsStoryDoesNotMove(uint seed, int afterFirst, int afterSecond, string stories, string houseEvents)
        {
            var start = ContentCatalog.Create(seed);
            EpisodeEngine.EnableStory(start); EpisodeEngine.EnableRead(start); EpisodeEngine.EnableLevers(start); EpisodeEngine.EnableWeek(start); EpisodeEngine.EnableAgency(start);
            var engine = new EpisodeEngine(start);
            for (int reveal = 1; reveal <= 2; reveal++)
            {
                EpisodeState before = null, after = null;
                for (int guard = 0; guard < 400 && after == null; guard++)
                {
                    var now = engine.Snapshot;
                    var result = engine.Apply(EpisodeEngineTests.NextCommand(now));
                    Assert.That(result.accepted, Is.True, result.reason);
                    if (result.state.phase == EpisodePhase.Eviction && result.state.evictionResolved && !now.evictionResolved) { before = now; after = result.state; }
                }
                Assert.That(after, Is.Not.Null, "Seed " + seed + " reaches reveal " + reveal + ".");
                var added = after.events.Where(e => e.sequence >= before.nextSequence).ToList();
                Assert.That(added.Count(e => e.kind == "eviction" && e.audienceIds.Count == 0), Is.EqualTo(1), "One public eviction line,");
                Assert.That(added.Single(e => e.kind == "eviction").text, Does.Contain(" the jury. By a vote of ").Or.Contain(" the jury. By a single vote."), "carrying the count;");
                Assert.That(added.Count(e => e.kind == "vote-reveal"), Is.EqualTo(after.votes.Count), "a private line a ballot;");
                Assert.That(added.Where(e => e.kind == "vote-reveal").All(e => e.audienceIds.Count == 1), Is.True);
                Assert.That(added.Any(e => e.kind == "vote-tally"), Is.False, "and nothing of the reveal's own.");
                Assert.That(after.nextSequence, Is.EqualTo(reveal == 1 ? afterFirst : afterSecond),
                    "Seed " + seed + ", reveal " + reveal + ": the sequence dbe6199 left; the story's ids are minted from it.");
            }
            Assert.That(string.Join(",", engine.Snapshot.storylines.Select(x => x.id)), Is.EqualTo(stories), "The story's cycles, as dbe6199 minted them.");
            Assert.That(string.Join(",", engine.Snapshot.houseEvents.Select(x => x.id)), Is.EqualTo(houseEvents), "The house's events, as dbe6199 minted them.");
        }

        /// <summary>The verdict lines the player is told read unresolved until the ballot is known, and resolve once it is (decision 4).</summary>
        [Test]
        public void AVerdictIsUnresolvedUntilTheBallotIsKnownAndResolvesOnceItIs()
        {
            var s = ContentCatalog.Create(31);
            s.week = 2;
            var npcs = s.contestants.Where(c => !c.isPlayer).ToArray();
            s.ledger.power.Add(new PowerRow { week = 1, hohId = npcs[0].id, evicteeId = npcs[1].id, nominees = new List<string> { npcs[1].id, npcs[2].id }, tally = new List<int> { 2, 1 } });
            s.ledger.ballots.Add(new BallotRow { week = 1, voterId = s.playerId, targetId = npcs[1].id });
            string you = s.Find(s.playerId).name, them = npcs[3].name;
            var promise = new PromiseState { id = "p", fromId = npcs[3].id, toId = s.playerId, targetId = npcs[1].id, kind = PromiseKind.Vote, status = PromiseStatus.Broken, week = 1, expiresWeek = 1 };
            s.promises.Add(promise);
            // A vote to evict the one the player voted against: the player kept their side, so the
            // other's ballot settled it, and that ballot is not theirs to know.
            var deal = new DealState { id = "d", type = DealKind.VoteEvict, proposerId = npcs[3].id, recipientId = s.playerId, targetId = npcs[1].id, status = DealStatus.Broken, week = 1, expiresWeek = 1 };
            s.deals.Add(deal);
            s.memories.Add(new MemoryState { ownerId = s.playerId, subjectId = npcs[3].id, week = 1, text = them + " broke a Vote promise.", isPrivate = true });
            // The ending on the player's own record, dated to the reveal's week.
            s.week = 1;
            RelationshipLedger.Record(s, s.playerId, npcs[3].id, YourWeek.DealBroken, -15, them + " broke a vote to evict with " + you + ".");
            s.week = 2;

            var notes = HouseguestNotes.For(s, npcs[3].id);
            Assert.That(notes.Select(n => n.text), Has.Some.EqualTo(FinalistRead.FirstName(them) + " promised you a vote · " + KnownBallots.Unresolved));
            Assert.That(notes.Select(n => n.text), Has.Some.EqualTo(FinalistRead.FirstName(them) + " put a vote to evict to you (about " + npcs[1].name + ") · " + KnownBallots.Unresolved));
            Assert.That(notes.Select(n => n.text), Has.None.EqualTo(them + " broke a Vote promise."), "The memory of the ending is withheld with the ballot.");
            var word = CommitmentsRead.With(s, npcs[3].id);
            Assert.That(word.Select(c => c.status), Is.All.EqualTo(KnownBallots.Unresolved));
            Assert.That(word.Select(c => c.outcome), Is.All.EqualTo(CommitmentsRead.Outcomes.Unresolved));
            Assert.That(word.Select(c => c.brokenById), Is.All.Null);
            var mine = YourWeek.Build(s, 1).word;
            Assert.That(mine, Has.Count.EqualTo(2), "The deal and the promise.");
            Assert.That(mine.Select(l => l.verdict), Is.All.EqualTo(YourWeek.Verdicts.NotKnown));
            Assert.That(mine.Select(l => l.text), Is.All.Contains(KnownBallots.Unresolved));

            // Told, and caught at the reveal: the ballot is known, and every verdict with it.
            s.ledger.claims.Add(new ClaimRow { week = 1, voterId = npcs[3].id, targetId = npcs[1].id, source = ClaimSource.Told, status = ClaimStatus.Lied });
            Assert.That(KnownBallots.Knows(s, 1, npcs[3].id), Is.True);
            Assert.That(HouseguestNotes.For(s, npcs[3].id).Select(n => n.text), Has.Some.EqualTo(FinalistRead.FirstName(them) + " promised you a vote · broken")
                .And.Some.EqualTo(them + " broke a Vote promise."));
            word = CommitmentsRead.With(s, npcs[3].id);
            Assert.That(word.Single(c => c.kind == CommitmentsRead.Kinds.Promise).status, Is.EqualTo("broken by them"));
            Assert.That(word.Single(c => c.kind == CommitmentsRead.Kinds.Deal).status, Is.EqualTo("broken by them"));
            mine = YourWeek.Build(s, 1).word;
            Assert.That(mine.Select(l => l.verdict), Is.All.EqualTo(YourWeek.Verdicts.Broken));
            Assert.That(mine.Select(l => l.text), Is.EquivalentTo(new[] { them + " broke the vote-to-evict deal with you.", them + " broke their vote promise to you." }));

            // A vote deal the player's own ballot broke is theirs to know whatever the other voted.
            s.ledger.claims.Clear();
            var mine2 = new DealState { id = "m", type = DealKind.VoteSave, proposerId = npcs[4].id, recipientId = s.playerId, targetId = npcs[1].id, status = DealStatus.Broken, week = 1, expiresWeek = 1 };
            s.deals.Add(mine2);
            Assert.That(CommitmentsRead.With(s, npcs[4].id).Single().status, Is.EqualTo("broken by you"), "You voted to evict the one you agreed to keep.");
        }
    }
}
