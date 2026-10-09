using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Vote family V4: the regular reveal settles the canonical Vote rows under mode 2 and publishes its archive
    /// frame; the Rule2 plan runs each decided row's source effects once; and the week's turn, the house's deal
    /// pass, a departure, a production removal and a voided price end what no reveal decided. Everything runs
    /// through the internal engine seam (<see cref="ProspectiveVoteFacade.Engine"/>) on the exact mode-2 twin of
    /// a public mode-1 season, and every case compares the mode-2 result with the mode-1 result after projection
    /// (<see cref="ProspectiveVoteTwins.AssertProjection"/>) wherever the two rules agree.
    ///
    /// <para>Where they deliberately do not, the case says so and pins the difference exactly: Rule2 groups the
    /// directed consequences of overlapping rows at one reveal (one wins each polarity, actor and partner; only an
    /// owner runs its memory, line, Story and witness lanes). Every reveal is seen as tapes - the views, the
    /// record, the memories, the lines and the draws.</para>
    ///
    /// <para>One difference is no rule but a reader still to move (vote family V5): the Story grudge's threat
    /// scaling (ThreatAssessment.ReputationThreat) counts raw promises, and canonical breaches only under mode 1,
    /// so in mode 2 it counts no canonical Vote or Safety breach at all - not the reveal's own, which the approved
    /// Rule2 policy excludes, and not an earlier one, which it does not exclude. A first reveal cannot tell the two
    /// apart; V5 moves the reader and passes the reveal's own exclusions to it.</para>
    ///
    /// <para>Fixtures are seasons walked with real public commands; the constructed facts are the ones
    /// <see cref="ProspectiveVoteTwins"/> and <see cref="PinnedVoteSeason"/> allow - a relationship score, the
    /// season's next draw, NPC ballots pinned after the real batch - and, for a production removal, the conduct
    /// row production's own ladder reads (as <see cref="OrdinaryVoterRemovalTests"/> builds it).</para>
    /// </summary>
    public sealed class UnifiedVoteSettlementTests
    {
        // ------------------------------------------------------------ the reveal, played twice

        /// <summary>
        /// One reveal played twice from one moment: the public mode-1 season and its exact mode-2 twin through the
        /// seam, the same ballot and the same counting Advance through both.
        /// </summary>
        private sealed class Reveal
        {
            internal EpisodeState Pinned, Cast, CastTwin;
            internal Dictionary<string, ProspectiveVoteOwner> Owners;
            internal CommandResult Legacy, Prospective;
            internal EpisodeCommand Command;
            internal UnifiedVoteRevealState Frame;
            internal EpisodeState Projection => PinnedVoteSeason.Project(Legacy.state, Owners, new[] { Frame });
            internal EpisodeState After => Prospective.state;
        }

        private static EpisodeState campaign, houseCampaign, evenCampaign;

        /// <summary>A first campaign the player votes in beside at least three houseguests, nothing of theirs spent.</summary>
        private static EpisodeState Campaign() => (campaign ??= ProspectiveVoteTwins.Find("a first campaign the player votes in with three houseguests",
            seed => ProspectiveVoteTwins.Walk(seed, s => s.phase == EpisodePhase.Campaign
                && EpisodeEngine.Voters(s).Any(v => v.isPlayer) && EpisodeEngine.Voters(s).Count() >= 4))).Clone();

        /// <summary>A seven-person house's first campaign the player votes in: four ordinary voters, so the house can tie.</summary>
        private static EpisodeState EvenCampaign() => (evenCampaign ??= ProspectiveVoteTwins.Find("a seven-person first campaign the player votes in",
            seed =>
            {
                var engine = new EpisodeEngine(PinnedVoteSeason.Fresh(seed, size: 7));
                for (int step = 0; step < 400; step++)
                {
                    var s = engine.Snapshot;
                    if (ProspectiveVoteTwins.Revealed(s)) return null;
                    if (s.phase == EpisodePhase.Campaign) return EpisodeEngine.Voters(s).Any(v => v.isPlayer) && EpisodeEngine.Voters(s).Count() == 4 ? s : null;
                    if (!engine.Apply(EpisodeEngineTests.NextCommand(s)).accepted) return null;
                }
                return null;
            })).Clone();

        /// <summary>
        /// A first campaign after the house's real passes struck a nominee's vote bargain with a voter and put one
        /// to the player, the player still an ordinary voter. Constructed as the creator cases construct it: every
        /// houseguest reads the player at 20, each nominee and one voter read each other at 45.
        /// </summary>
        private static EpisodeState HouseCampaign() => (houseCampaign ??= ProspectiveVoteTwins.Find("a first campaign with the house's vote bargains",
            seed =>
            {
                var s = ProspectiveVoteTwins.Walk(seed, x => x.phase == EpisodePhase.VetoMeeting && x.vetoResolved && x.hohId != x.playerId
                    && !x.nominees.Contains(x.playerId));
                if (s == null) return null;
                foreach (var npc in s.contestants.Where(c => !c.isPlayer && c.status == ContestantStatus.Active))
                    ProspectiveVoteTwins.Set(s, npc.id, s.playerId, 20);
                var voters = NpcVoters(s);
                for (int i = 0; i < 2; i++)
                {
                    ProspectiveVoteTwins.Set(s, s.nominees[i], voters[i], 45);
                    ProspectiveVoteTwins.Set(s, voters[i], s.nominees[i], 45);
                }
                var after = Step(s, ProspectiveVoteTwins.Command(s, EpisodeCommandKind.Advance));
                bool bargain = after.deals.Any(d => d.id.StartsWith("deal-npc-", StringComparison.Ordinal) && d.type == DealKind.VoteSave);
                bool offer = after.deals.Any(d => d.id.StartsWith(NpcDeals.OfferPrefix, StringComparison.Ordinal) && d.type == DealKind.VoteSave
                    && d.status == DealStatus.Proposed);
                return after.phase == EpisodePhase.Campaign && bargain && offer ? after : null;
            })).Clone();

        private static List<string> NpcVoters(EpisodeState s) => EpisodeEngine.Voters(s).Where(v => !v.isPlayer).Select(v => v.id).ToList();

        /// <summary>A public command the fixture needs accepted, on a fresh public engine.</summary>
        private static EpisodeState Step(EpisodeState s, EpisodeCommand command)
        {
            var result = new EpisodeEngine(ProspectiveVoteTwins.Valid(s)).Apply(command);
            Assert.That(result.accepted, Is.True, command.kind + ": " + result.reason);
            return result.state;
        }

        /// <summary>The player's vote promise, made in the real command.</summary>
        private static EpisodeState Promise(EpisodeState s, string to, string evict) =>
            Step(s, ProspectiveVoteTwins.Command(s, EpisodeCommandKind.PromiseVote, to, evict));

        /// <summary>The player's vote deal, proposed in the real command and agreed by the season's next draw.</summary>
        private static EpisodeState Deal(EpisodeState s, string with, string type, string about = null)
        {
            Assert.That(PlayerDeals.CanPropose(s, with, type, about, out var why), Is.True, why);
            s.randomState = ProspectiveVoteTwins.Draw(true, PlayerDeals.AcceptanceChance(s, with, type, about));
            var after = Step(s, ProspectiveVoteTwins.Command(s, EpisodeCommandKind.ProposeDeal, with, about, type));
            Assert.That(after.deals.Count(d => d.type == type && d.status == DealStatus.Active && d.recipientId == with), Is.EqualTo(1),
                "Fixture: the deal was struck.");
            return after;
        }

        /// <summary>The id of the player's one deal of this type with this houseguest.</summary>
        private static string DealId(EpisodeState s, string with, string type) =>
            s.deals.Single(d => d.type == type && d.proposerId == s.playerId && d.recipientId == with).id;

        /// <summary>
        /// From a campaign: the real Advances to the open vote and the real NPC batch, NPC ballots then pinned as
        /// <paramref name="pins"/> names them (anybody unnamed keeps the batch's real ballot), and the player's
        /// ballot and the counting Advance played through both games.
        /// </summary>
        private static Reveal Play(EpisodeState s, string playerVote, IReadOnlyDictionary<string, string> pins)
        {
            var engine = new EpisodeEngine(ProspectiveVoteTwins.Valid(s));
            EpisodeEngineTests.OpenTheVote(engine);
            var open = engine.Snapshot;
            Assert.That(PinnedVoteSeason.OpenVote(open) && PinnedVoteSeason.PlayerVotes(open), Is.True, "Fixture: an open vote the player owes a ballot in.");
            var batch = engine.Apply(EpisodeEngineTests.Command(open, EpisodeCommandKind.Advance));
            Assert.That(batch.accepted && !batch.state.evictionResolved, Is.True, batch.reason);
            var pinned = batch.state;
            foreach (var pin in pins)
            {
                Assert.That(pinned.nominees, Does.Contain(pin.Value));
                pinned.votes.Single(vote => vote.voterId == pin.Key).targetId = pin.Value;
            }
            var reveal = new Reveal { Pinned = ProspectiveVoteTwins.Valid(pinned), Owners = ProspectiveVoteTwins.Owners(pinned) };
            var legacy = new EpisodeEngine(pinned);
            var prospective = ProspectiveVoteFacade.Engine(ProspectiveVoteTwins.Twin(pinned));
            var cast = ProspectiveVoteTwins.Command(pinned, EpisodeCommandKind.CastVote, playerVote);
            var castLegacy = legacy.Apply(cast);
            var castProspective = prospective.Apply(cast);
            Assert.That(castLegacy.accepted && castProspective.accepted, Is.True, castLegacy.reason + " / " + castProspective.reason);
            reveal.Cast = castLegacy.state; reveal.CastTwin = castProspective.state;
            reveal.Command = ProspectiveVoteTwins.Command(castLegacy.state, EpisodeCommandKind.Advance, tag: "reveal");
            reveal.Legacy = legacy.Apply(reveal.Command);
            reveal.Prospective = prospective.Apply(reveal.Command);
            Assert.That(reveal.Legacy.accepted, Is.True, reveal.Legacy.reason);
            Assert.That(reveal.Prospective.accepted, Is.True, "The mode-2 reveal settles: " + reveal.Prospective.reason);
            Assert.That(reveal.Legacy.state.evictionResolved && reveal.After.evictionResolved, Is.True, "Both counted the box.");
            reveal.Frame = new UnifiedVoteRevealState { week = reveal.Legacy.state.week, ballots = reveal.Legacy.state.votes
                .Select(v => new UnifiedVoteBallotState { voterId = v.voterId, targetId = v.targetId }).ToList() };
            Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(reveal.After, out var error), Is.True, error);
            Published(reveal);
            return reveal;
        }

        private static Dictionary<string, string> Pins(params (string voter, string target)[] pins) =>
            pins.ToDictionary(pin => pin.voter, pin => pin.target, StringComparer.Ordinal);

        private static UnifiedCommitmentState Row(EpisodeState s, string id) => s.unifiedCommitments.Single(row => row.id == id);

        /// <summary>The reveal's frame: the actual private box, in its order, appended once, and nothing else of the archive moved.</summary>
        private static void Published(Reveal reveal)
        {
            var archive = reveal.After.unifiedVoteReveals;
            Assert.That(archive.Take(archive.Count - 1).Select(PinnedVoteSeason.Json), Is.EqualTo(reveal.CastTwin.unifiedVoteReveals.Select(PinnedVoteSeason.Json)),
                "Earlier frames are untouched.");
            Assert.That(archive.Count, Is.EqualTo(reveal.CastTwin.unifiedVoteReveals.Count + 1), "One frame, appended once.");
            Assert.That(PinnedVoteSeason.Json(archive.Last()), Is.EqualTo(PinnedVoteSeason.Json(reveal.Frame)), "The frame is the actual box, in its order.");
        }

        /// <summary>A decided row's terminal stamp: the verdict, this week, the breaker the source names, a breach's identity.</summary>
        private static void Stamped(EpisodeState s, string id, string status, string brokenBy = null)
        {
            var row = Row(s, id);
            Assert.That((row.status, row.settledWeek, row.brokenById), Is.EqualTo((status, s.week, brokenBy)), id + ": its verdict, this week.");
            Assert.That(row.settlementEffectKey, Is.EqualTo(status == DealStatus.Broken ? UnifiedVoteHistory.Key(row, s.week) : null),
                id + ": a breach names its policy, duty, parties and week; a keep carries no identity.");
        }

        // ------------------------------------------------------------ tapes

        /// <summary>The commitment tape a reveal wrote: its outcome records, outcome lines and outcome memories, in sequence order.</summary>
        private sealed class Tape
        {
            internal readonly List<string> Ledger = new List<string>(), Lines = new List<string>(), Memories = new List<string>();
            internal uint Draw;
            internal long Sequence;
        }

        private static readonly string[] OutcomeTypes = { "promise-kept", "promise-broken", "deal_fulfilled", "deal_broken", "heard_about_betrayal" };

        private static Tape TapeOf(EpisodeState before, EpisodeState after)
        {
            var tape = new Tape { Draw = after.randomState, Sequence = after.nextSequence - before.nextSequence };
            foreach (var entry in after.relationships.SelectMany(r => r.events.Select(e => (r, e)))
                         .Where(x => x.e.sequence >= before.nextSequence && OutcomeTypes.Contains(x.e.type)).OrderBy(x => x.e.sequence))
                tape.Ledger.Add(entry.r.fromId + ">" + entry.r.toId + " " + entry.e.type + " " + entry.e.impactScore + (entry.e.decayable ? "" : " held"));
            foreach (var line in after.events.Where(e => e.sequence >= before.nextSequence && (e.kind == "promise-outcome" || e.kind == "deal-outcome")))
                tape.Lines.Add(line.kind + " [" + string.Join(",", line.audienceIds) + "] " + line.text);
            var outcomeTexts = new HashSet<string>(after.events.Where(e => e.sequence >= before.nextSequence
                && (e.kind == "promise-outcome" || e.kind == "deal-outcome")).Select(e => e.text));
            var old = before.memories.Select(m => m.ownerId + ">" + m.subjectId + " " + m.week + " " + m.text).ToList();
            foreach (var memory in after.memories.Where(m => m.week == after.week && outcomeTexts.Contains(m.text)))
            {
                string key = memory.ownerId + ">" + memory.subjectId + " " + memory.week + " " + memory.text;
                if (old.Remove(key)) continue;
                tape.Memories.Add(memory.ownerId + ">" + memory.subjectId + " " + memory.text);
            }
            return tape;
        }

        private static double Moved(EpisodeState before, EpisodeState after, string from, string to) => after.Score(from, to) - before.Score(from, to);

        // ------------------------------------------------------------ single rows: the source's own settlement

        /// <summary>
        /// The player's vote promise, kept at the reveal: the canonical row stamped Fulfilled with no breaker, the
        /// frame published, and the whole result the mode-1 result with its rows canonical - the beneficiary's
        /// view and permanent record of the maker, both memories and the maker's line, nothing drawn.
        /// </summary>
        [Test]
        public void AKeptVotePromiseSettlesAsModeOneSettlesIt()
        {
            var s = Campaign();
            string to = NpcVoters(s)[0], evict = s.nominees[0];
            s = Promise(s, to, evict);
            string id = s.promises.Single(p => p.kind == PromiseKind.Vote && p.fromId == s.playerId).id;
            var reveal = Play(s, evict, Pins());
            Stamped(reveal.After, id, DealStatus.Fulfilled);
            var tape = TapeOf(reveal.CastTwin, reveal.After);
            Assert.That(tape.Ledger, Is.EqualTo(new[] { to + ">" + s.playerId + " promise-kept 15 held" }));
            Assert.That(tape.Lines, Is.EqualTo(new[] { "promise-outcome [" + s.playerId + "] You fulfilled a Vote promise." }));
            Assert.That(tape.Memories, Is.EquivalentTo(new[] { to + ">" + s.playerId + " You fulfilled a Vote promise.",
                s.playerId + ">" + to + " You fulfilled a Vote promise." }));
            Assert.That(Moved(reveal.CastTwin, reveal.After, to, s.playerId), Is.EqualTo(15), "The beneficiary's view of the maker.");
            ProspectiveVoteTwins.AssertProjection(reveal.Projection, reveal.After, "A kept vote promise");
        }

        /// <summary>
        /// The player's vote promise, broken: stamped Broken by its maker with its breach identity; the view, record,
        /// memories, line and the witness loop's draws as mode 1 writes them. The one value apart is the Story
        /// grudge's threat scaling, a reader still to move (vote family V5): mode 1's raw store counts the promise it
        /// has just broken, mode 2's reader counts no canonical breach, so mode 1's grudge can only be the heavier.
        /// </summary>
        [Test]
        public void ABrokenVotePromiseSettlesAsModeOneSettlesItSaveTheThreatReadersGap()
        {
            var s = Campaign();
            string to = NpcVoters(s)[0], promised = s.nominees[0], other = s.nominees[1];
            s = Promise(s, to, promised);
            string id = s.promises.Single(p => p.kind == PromiseKind.Vote && p.fromId == s.playerId).id;
            var reveal = Play(s, other, Pins());
            Stamped(reveal.After, id, DealStatus.Broken, s.playerId);
            var tape = TapeOf(reveal.CastTwin, reveal.After);
            Assert.That(tape.Ledger, Is.EqualTo(new[] { to + ">" + s.playerId + " promise-broken -31 held" }));
            Assert.That(tape.Lines, Is.EqualTo(new[] { "promise-outcome [" + s.playerId + "] You broke a Vote promise." }));
            Assert.That(tape.Draw, Is.EqualTo(reveal.Legacy.state.randomState), "The witness loop draws as mode 1 draws it.");
            Assert.That(tape.Draw, Is.Not.EqualTo(reveal.CastTwin.randomState), "Fixture: the witness loop drew.");
            AssertParityButTheThreatReadersGap(reveal, "A broken vote promise", to, s.playerId);
        }

        /// <summary>
        /// A voting bloc both parties decide at once (UnifiedVoteTogether): kept or broken, stamped with no sole
        /// breaker; both views (never the player's own), both directions of the record and the pair's line without
        /// the player; no memory, Story hook or spread. Exactly mode 1's.
        /// </summary>
        [TestCase(true)] [TestCase(false)]
        public void AVotingBlocSettlesForBothPartiesAsModeOneSettlesIt(bool kept)
        {
            var s = Campaign();
            string npc = NpcVoters(s)[0], x = s.nominees[0], y = s.nominees[1];
            s = Deal(s, npc, DealKind.VoteTogether);
            string id = DealId(s, npc, DealKind.VoteTogether);
            var reveal = Play(s, x, Pins((npc, kept ? x : y)));
            Stamped(reveal.After, id, kept ? DealStatus.Fulfilled : DealStatus.Broken);
            var tape = TapeOf(reveal.CastTwin, reveal.After);
            string type = kept ? "deal_fulfilled 12" : "deal_broken -22.5 held";
            Assert.That(tape.Ledger, Is.EqualTo(new[] { s.playerId + ">" + npc + " " + type, npc + ">" + s.playerId + " " + type }));
            Assert.That(tape.Lines.Single(), Does.StartWith("deal-outcome [" + npc + "] "), "The pair's line, without the player.");
            Assert.That(tape.Memories, Is.Empty);
            Assert.That(Moved(reveal.CastTwin, reveal.After, s.playerId, npc), Is.Zero, "The player's own view never moves on a ballot's verdict.");
            Assert.That(Moved(reveal.CastTwin, reveal.After, npc, s.playerId), Is.EqualTo(kept ? 12 : -22.5));
            ProspectiveVoteTwins.AssertProjection(reveal.Projection, reveal.After, "A voting bloc, " + (kept ? "kept" : "broken"));
        }

        /// <summary>
        /// A targeted vote deal (UnifiedVoteObligations), every way its two ballots can fall: the first keeper in
        /// proposer-then-recipient order keeps it; a sole breaker breaks it (the player's view masked when they are
        /// the one wronged, but the record kept); two breakers break it with nobody named. Each is mode 1's.
        /// </summary>
        [TestCase(DealKind.VoteEvict, 0, 0, DealStatus.Fulfilled, "player")]
        [TestCase(DealKind.VoteEvict, 0, 1, DealStatus.Broken, "npc")]
        [TestCase(DealKind.VoteEvict, 1, 1, DealStatus.Broken, null)]
        [TestCase(DealKind.VoteSave, 1, 1, DealStatus.Fulfilled, "player")]
        [TestCase(DealKind.VoteSave, 0, 1, DealStatus.Broken, "player")]
        [TestCase(DealKind.VoteSave, 1, 0, DealStatus.Broken, "npc")]
        public void ATargetedVoteDealSettlesByItsFirstDecidingPartyAsModeOneSettlesIt(string type, int playerVote, int npcVote,
            string status, string actor)
        {
            var s = Campaign();
            string npc = NpcVoters(s)[0], target = s.nominees[0];
            s = Deal(s, npc, type, target);
            string id = DealId(s, npc, type);
            var reveal = Play(s, s.nominees[playerVote], Pins((npc, s.nominees[npcVote])));
            string actorId = actor == "player" ? s.playerId : actor == "npc" ? npc : null;
            Stamped(reveal.After, id, status, status == DealStatus.Broken ? actorId : null);
            var tape = TapeOf(reveal.CastTwin, reveal.After);
            if (actorId == null)
                Assert.That(tape.Memories, Is.Empty, "Nobody in particular broke it: no memory.");
            else
            {
                string wronged = actorId == s.playerId ? npc : s.playerId;
                Assert.That(tape.Lines.Single(), Does.StartWith("deal-outcome [" + actorId + "] "), "The deciding party's own line.");
                Assert.That(tape.Memories, Is.EqualTo(new[] { wronged + ">" + actorId + " " + tape.Lines.Single().Substring(tape.Lines.Single().IndexOf("] ") + 2) }));
                if (wronged == s.playerId)
                    Assert.That(Moved(reveal.CastTwin, reveal.After, s.playerId, npc), Is.Zero, "The player wronged by a ballot: their view waits for the ballot.");
            }
            ProspectiveVoteTwins.AssertProjection(reveal.Projection, reveal.After, type + " " + status);
        }

        /// <summary>
        /// A row the box does not decide stays as it stood: a voting bloc with a nominee, who casts no ballot; and the
        /// house's own vote promises, made by nominees, who never vote. The frame is published all the same.
        /// </summary>
        [Test]
        public void ARowTheBoxDoesNotDecideStaysActive()
        {
            var s = Campaign();
            string nominee = s.nominees[1];
            s = Deal(s, nominee, DealKind.VoteTogether);
            string id = DealId(s, nominee, DealKind.VoteTogether);
            var houses = s.promises.Where(p => p.kind == PromiseKind.Vote && p.id.StartsWith("promise-npc-", StringComparison.Ordinal)).Select(p => p.id).ToList();
            Assert.That(houses, Is.Not.Empty, "Fixture: the house's nominees gave their word on the vote.");
            var reveal = Play(s, s.nominees[0], Pins());
            foreach (string row in houses.Concat(new[] { id }))
                Assert.That((Row(reveal.After, row).status, Row(reveal.After, row).settledWeek), Is.EqualTo((DealStatus.Active, 0)), row + " stands undecided.");
            ProspectiveVoteTwins.AssertProjection(reveal.Projection, reveal.After, "Rows the box does not decide");
        }

        /// <summary>
        /// A tied house: the houseguest Head of Household breaks the tie, and their ballot - in the archived frame,
        /// out of the ordinary tally - decides the player's voting bloc with them, as mode 1's does.
        /// </summary>
        [Test]
        public void AHeadOfHouseholdsTieBreakBallotDecidesTheirBlocAndIsInTheFrame()
        {
            var s = EvenCampaign();
            string hoh = s.hohId, x = s.nominees[0], y = s.nominees[1];
            s = Deal(s, hoh, DealKind.VoteTogether);
            string id = DealId(s, hoh, DealKind.VoteTogether);
            var voters = NpcVoters(s);
            Assert.That(voters.Count, Is.EqualTo(3), "Fixture: the player and three houseguests can tie.");
            var reveal = Play(s, x, Pins((voters[0], x), (voters[1], y), (voters[2], y)));
            Assert.That(reveal.Frame.ballots.Any(b => b.voterId == hoh), Is.True, "Fixture: the tie went to the Head of Household.");
            Assert.That(reveal.After.ledger.power.Single(p => p.week == s.week).tally, Is.EqualTo(new[] { 2, 2 }),
                "The ordinary tally leaves the deciding ballot out.");
            Assert.That(Row(reveal.After, id).status, Is.Not.EqualTo(DealStatus.Active), "The tie-break ballot decided the bloc.");
            ProspectiveVoteTwins.AssertProjection(reveal.Projection, reveal.After, "A tie-break's bloc");
        }

        /// <summary>
        /// The reveal's Story hook reads the canonical vote deals as mode 1's read its raw ones: a houseguest Head of
        /// Household whose real target survived resents a vote-deal partner of this week who voted the other nominee
        /// out - here the player, with an undecided voting bloc between them - as mode 1's resents them.
        /// </summary>
        [Test]
        public void AHeadOfHouseholdResentsAVoteDealPartnerWhoseBallotSparedTheirTarget()
        {
            var s = Campaign();
            string hoh = s.hohId;
            s = Deal(s, hoh, DealKind.VoteTogether);
            string target = s.nominees.OrderBy(id => s.Score(hoh, id)).ThenBy(id => id, StringComparer.Ordinal).First();
            string other = s.nominees.Single(id => id != target);
            Assert.That(s.Allied(hoh, s.playerId), Is.False, "Fixture: only the deal makes the player one meant to be with them.");
            var reveal = Play(s, other, Pins(NpcVoters(s).Select(v => (v, other)).ToArray()));
            Assert.That(reveal.After.ledger.power.Single(p => p.week == s.week).evicteeId, Is.EqualTo(other), "Fixture: the Head of Household's target survived.");
            Assert.That(reveal.After.story.grudges.Any(g => g.holderId == hoh && g.targetId == s.playerId && g.cause == GrudgeCauses.VotedAgainst), Is.True,
                "The Head of Household resents the player their ballot.");
            ProspectiveVoteTwins.AssertProjection(reveal.Projection, reveal.After, "A Head of Household's resentment");
        }

        /// <summary>
        /// The house's own rows at one reveal: a nominee's vote bargain with a voter (an NPC deal, decided by the
        /// voter's ballot, its spread drawn), and the player's yes to a nominee's offer (an accepted offer, its breach
        /// one step heavier) - each mode 1's, the Story grudge between houseguests included.
        /// </summary>
        [TestCase(0)] [TestCase(1)]
        public void TheHousesBargainsAndAnAcceptedOfferSettleAsModeOneSettlesThem(int playerVote)
        {
            var s = HouseCampaign();
            var offer = s.deals.First(d => d.id.StartsWith(NpcDeals.OfferPrefix, StringComparison.Ordinal) && d.type == DealKind.VoteSave
                && d.status == DealStatus.Proposed);
            s = Step(s, ProspectiveVoteTwins.Command(s, EpisodeCommandKind.RespondToDeal, offer.id, text: EpisodeEngine.AcceptDeal));
            var bargains = s.deals.Where(d => d.id.StartsWith("deal-npc-", StringComparison.Ordinal) && d.type == DealKind.VoteSave).ToList();
            // Each nominee asked to be kept: a keep votes the other nominee out, a breach votes them out.
            string asked = offer.proposerId, other = s.nominees.Single(id => id != asked);
            string Against(string nominee) => playerVote == 0 ? s.nominees.Single(id => id != nominee) : nominee;
            Assert.That(bargains.Select(b => b.recipientId).Distinct().Count(), Is.EqualTo(bargains.Count), "Fixture: one bargain a voter.");
            var pins = Pins(bargains.Select(b => (b.recipientId, Against(b.proposerId))).ToArray());
            var reveal = Play(s, Against(asked), pins);
            Stamped(reveal.After, offer.id, playerVote == 0 ? DealStatus.Fulfilled : DealStatus.Broken, playerVote == 0 ? null : s.playerId);
            foreach (var bargain in bargains)
                Assert.That(Row(reveal.After, bargain.id).status, Is.EqualTo(playerVote == 0 ? DealStatus.Fulfilled : DealStatus.Broken), bargain.id);
            if (playerVote == 1)
                Assert.That(TapeOf(reveal.CastTwin, reveal.After).Ledger, Does.Contain(asked + ">" + s.playerId + " deal_broken -30 held"),
                    "The accepted offer's breach weighs one step heavier.");
            ProspectiveVoteTwins.AssertProjection(reveal.Projection, reveal.After, "The house's bargains and an accepted offer");
        }

        /// <summary>
        /// The deal lane meets the raw and the canonical deals in the one order mode 1's single list met them: a
        /// partnership with a nominee (raw, judged by the player's ballot) struck before or after a voting bloc
        /// (canonical) settles first or second, its line, record and memory in their mode-1 places either way.
        /// </summary>
        [TestCase(true)] [TestCase(false)]
        public void RawAndCanonicalDealsSettleInTheOrderTheyWereStruck(bool partnershipFirst)
        {
            var s = Campaign();
            string nominee = s.nominees[1], npc = NpcVoters(s)[0], x = s.nominees[0];
            s = partnershipFirst ? Deal(Deal(s, nominee, DealKind.Partnership), npc, DealKind.VoteTogether)
                : Deal(Deal(s, npc, DealKind.VoteTogether), nominee, DealKind.Partnership);
            var reveal = Play(s, nominee, Pins((npc, x)));
            var lines = TapeOf(reveal.CastTwin, reveal.After).Lines;
            Assert.That(lines.Count, Is.EqualTo(2), "Fixture: the partnership and the bloc both settled.");
            Assert.That(lines[partnershipFirst ? 0 : 1], Does.Contain(DealKind.Title(DealKind.Partnership).ToLowerInvariant()), "Struck first, settled first.");
            ProspectiveVoteTwins.AssertProjection(reveal.Projection, reveal.After, "A partnership and a bloc");
        }

        // ------------------------------------------------------------ Rule2: overlapping rows at one reveal

        /// <summary>
        /// The player's vote promise and voting bloc with one houseguest, both kept. The houseguest's view of the
        /// player is one consequence, and the promise's +15 outranks the bloc's +12: it moves once, and the record
        /// holds the promise's entry alone. The player's view of the houseguest is the bloc's alone (masked, as a
        /// ballot's verdict always is), and its record the bloc's. Each row owns a consequence, so each says its
        /// own line; only the promise has memories. Nothing is drawn. Mode 1 writes the view and record twice.
        /// </summary>
        [Test]
        public void APromiseAndABlocKeptTogetherMoveTheBeneficiaryOnceByTheStronger()
        {
            var s = Campaign();
            string npc = NpcVoters(s)[0], x = s.nominees[0];
            s = Promise(s, npc, x);
            s = Deal(s, npc, DealKind.VoteTogether);
            string promise = s.promises.Single(p => p.kind == PromiseKind.Vote && p.fromId == s.playerId).id, bloc = DealId(s, npc, DealKind.VoteTogether);
            var reveal = Play(s, x, Pins((npc, x)));
            Stamped(reveal.After, promise, DealStatus.Fulfilled);
            Stamped(reveal.After, bloc, DealStatus.Fulfilled);
            string p = s.playerId;
            var tape = TapeOf(reveal.CastTwin, reveal.After);
            Assert.That(tape.Ledger, Is.EqualTo(new[] { npc + ">" + p + " promise-kept 15 held", p + ">" + npc + " deal_fulfilled 12" }));
            Assert.That(tape.Lines.Count, Is.EqualTo(2));
            Assert.That(tape.Lines[0], Is.EqualTo("promise-outcome [" + p + "] You fulfilled a Vote promise."));
            Assert.That(tape.Lines[1], Does.StartWith("deal-outcome [" + npc + "] "));
            Assert.That(tape.Memories, Is.EquivalentTo(new[] { npc + ">" + p + " You fulfilled a Vote promise.", p + ">" + npc + " You fulfilled a Vote promise." }));
            Assert.That(Moved(reveal.CastTwin, reveal.After, npc, p), Is.EqualTo(15), "Once, by the stronger.");
            Assert.That(Moved(reveal.CastTwin, reveal.After, p, npc), Is.Zero, "The player's own view, masked.");
            Assert.That(tape.Draw, Is.EqualTo(reveal.CastTwin.randomState), "Nothing drawn.");

            var mode1 = TapeOf(reveal.Cast, reveal.Legacy.state);
            Assert.That(mode1.Ledger, Is.EqualTo(new[] { npc + ">" + p + " promise-kept 15 held", p + ">" + npc + " deal_fulfilled 12", npc + ">" + p + " deal_fulfilled 12" }),
                "Mode 1 records the same view twice,");
            Assert.That(Moved(reveal.Cast, reveal.Legacy.state, npc, p), Is.EqualTo(27), "and moves it twice.");
            Assert.That(mode1.Sequence - tape.Sequence, Is.EqualTo(1), "Mode 2 mints one entry fewer.");
        }

        /// <summary>
        /// The player's vote promise and voting bloc with one houseguest, both broken by the player's ballot. The
        /// houseguest's view of the player is one breach, and the promise's -31 is the more severe: it alone moves the
        /// view, holds the record, raises the Story grudge and draws the witness loop. The bloc's other direction - the
        /// player's view of the houseguest - is its alone (masked), so it keeps that record and its pair line: a
        /// collective row winning one of its two edges. Mode 1 moves and records the houseguest's view twice.
        /// </summary>
        [Test]
        public void APromiseAndABlocBrokenTogetherAreOneBreachOwnedByTheMoreSevere()
        {
            var s = Campaign();
            string npc = NpcVoters(s)[0], x = s.nominees[0], y = s.nominees[1];
            s = Promise(s, npc, x);
            s = Deal(s, npc, DealKind.VoteTogether);
            string promise = s.promises.Single(q => q.kind == PromiseKind.Vote && q.fromId == s.playerId).id, bloc = DealId(s, npc, DealKind.VoteTogether);
            var reveal = Play(s, y, Pins((npc, x)));
            string p = s.playerId;
            Stamped(reveal.After, promise, DealStatus.Broken, p);
            Stamped(reveal.After, bloc, DealStatus.Broken);
            var tape = TapeOf(reveal.CastTwin, reveal.After);
            Assert.That(tape.Ledger.Where(e => !e.Contains("heard_about_betrayal")),
                Is.EqualTo(new[] { npc + ">" + p + " promise-broken -31 held", p + ">" + npc + " deal_broken -22.5 held" }));
            Assert.That(tape.Lines[0], Is.EqualTo("promise-outcome [" + p + "] You broke a Vote promise."));
            Assert.That(tape.Lines[1], Does.StartWith("deal-outcome [" + npc + "] "));
            Assert.That(Moved(reveal.CastTwin, reveal.After, npc, p), Is.EqualTo(-31), "One breach, the more severe.");
            Assert.That(reveal.After.story.grudges.Count(g => g.holderId == npc && g.targetId == p), Is.EqualTo(1));
            Assert.That(tape.Draw, Is.EqualTo(reveal.Legacy.state.randomState), "The witness loop draws as mode 1's; the bloc draws nothing in either.");
            Assert.That(Moved(reveal.Cast, reveal.Legacy.state, npc, p), Is.EqualTo(-53.5), "Mode 1 moves the one view twice.");
        }

        /// <summary>
        /// A voting bloc and a vote-to-evict deal with one houseguest, both kept: two +12 consequences of the
        /// houseguest's view of the player - an equal grade, which the ordinal row id settles, whatever the order the
        /// rows are met in. The winner moves the view; each edge of the record goes to the stronger entry, the mirror
        /// of a named deal included, by the same id. A row that owns no consequence writes nothing but its stamp.
        /// </summary>
        [TestCase(true)] [TestCase(false)]
        public void EqualConsequencesAreSettledByTheOrdinalRowId(bool evictFirst)
        {
            var s = Campaign();
            string npc = NpcVoters(s)[0], x = s.nominees[0];
            s = evictFirst ? Deal(Deal(s, npc, DealKind.VoteEvict, x), npc, DealKind.VoteTogether)
                : Deal(Deal(s, npc, DealKind.VoteTogether), npc, DealKind.VoteEvict, x);
            string evict = DealId(s, npc, DealKind.VoteEvict), bloc = DealId(s, npc, DealKind.VoteTogether);
            Assert.That(string.CompareOrdinal(evict, bloc) < 0, Is.EqualTo(evictFirst), "Fixture: the first struck has the smaller id.");
            var reveal = Play(s, x, Pins((npc, x)));
            string p = s.playerId;
            Stamped(reveal.After, evict, DealStatus.Fulfilled);
            Stamped(reveal.After, bloc, DealStatus.Fulfilled);
            var tape = TapeOf(reveal.CastTwin, reveal.After);
            Assert.That(Moved(reveal.CastTwin, reveal.After, npc, p), Is.EqualTo(12), "Once.");
            // One entry an edge, in the owner's own order: a named deal writes the partner's record first, a bloc the proposer's.
            Assert.That(tape.Ledger, evictFirst
                ? Is.EqualTo(new[] { npc + ">" + p + " deal_fulfilled 12", p + ">" + npc + " deal_fulfilled 12" })
                : Is.EqualTo(new[] { p + ">" + npc + " deal_fulfilled 12", npc + ">" + p + " deal_fulfilled 12" }), "One entry an edge.");
            if (evictFirst)
            {
                // The named deal wins the view and both edges (its mirror among them); the bloc owns the player's own view.
                Assert.That(tape.Lines.Count, Is.EqualTo(2));
                Assert.That(tape.Lines[0], Does.StartWith("deal-outcome [" + p + "] You honoured a "), "The named keep, met first.");
                Assert.That(tape.Lines[1], Does.StartWith("deal-outcome [" + npc + "] "), "The bloc's pair line.");
                Assert.That(tape.Memories.Single(), Does.StartWith(npc + ">" + p + " You honoured a "));
            }
            else
            {
                // The bloc wins both views and both edges; the named deal owns nothing: its stamp, no line, no memory.
                Assert.That(tape.Lines.Single(), Does.StartWith("deal-outcome [" + npc + "] "));
                Assert.That(tape.Memories, Is.Empty);
            }
            Assert.That(Moved(reveal.Cast, reveal.Legacy.state, npc, p), Is.EqualTo(24), "Mode 1 moves the one view twice.");
        }

        /// <summary>
        /// A promise kept and a deal broken by the same ballot, with the same houseguest: a keep and a breach are
        /// different consequences, and Rule2 never lets one absorb the other - both apply, exactly as in mode 1, the
        /// breach's Story hook and spread included.
        /// </summary>
        [Test]
        public void AKeepAndABreachOfOnePairAreBothApplied()
        {
            var s = Campaign();
            string npc = NpcVoters(s)[0], x = s.nominees[0], y = s.nominees[1];
            s = Promise(s, npc, x);
            s = Deal(s, npc, DealKind.VoteSave, x);
            string promise = s.promises.Single(q => q.kind == PromiseKind.Vote && q.fromId == s.playerId).id, save = DealId(s, npc, DealKind.VoteSave);
            var reveal = Play(s, x, Pins((npc, y)));
            Stamped(reveal.After, promise, DealStatus.Fulfilled);
            Stamped(reveal.After, save, DealStatus.Broken, s.playerId);
            Assert.That(Moved(reveal.CastTwin, reveal.After, npc, s.playerId), Is.EqualTo(15 - 22.5));
            ProspectiveVoteTwins.AssertProjection(reveal.Projection, reveal.After, "A keep and a breach of one pair");
        }

        /// <summary>
        /// Two vote deals one party breaks with one ballot - a vote-to-keep and a vote-to-evict with the same
        /// houseguest - are one breach of one word: equal in grade, owned by the ordinal row id. Broken by the player,
        /// the houseguest's view and record move once and the Story grudge and reckoning are raised once (mode 1
        /// stacks two grudges). Broken by the houseguest, against the player: the player's view is masked, the record
        /// kept once, and the betrayal spread is drawn once - mode 1 draws it twice.
        /// </summary>
        [TestCase(true)] [TestCase(false)]
        public void TwoDealsBrokenByOneBallotAreOneBreach(bool playerBreaks)
        {
            var s = Campaign();
            string npc = NpcVoters(s)[0], x = s.nominees[0], y = s.nominees[1];
            // Keep x and evict y: one duty, to vote y out. Its breaker votes x out.
            s = Deal(Deal(s, npc, DealKind.VoteSave, x), npc, DealKind.VoteEvict, y);
            string save = DealId(s, npc, DealKind.VoteSave), evict = DealId(s, npc, DealKind.VoteEvict);
            string p = s.playerId, breaker = playerBreaks ? p : npc, wronged = playerBreaks ? npc : p;
            var reveal = Play(s, playerBreaks ? x : y, Pins((npc, playerBreaks ? y : x)));
            Stamped(reveal.After, save, DealStatus.Broken, breaker);
            Stamped(reveal.After, evict, DealStatus.Broken, breaker);
            var tape = TapeOf(reveal.CastTwin, reveal.After);
            var mode1 = TapeOf(reveal.Cast, reveal.Legacy.state);
            Assert.That(tape.Ledger.Where(e => !e.Contains("heard_about_betrayal")),
                Is.EqualTo(new[] { wronged + ">" + breaker + " deal_broken -22.5 held", breaker + ">" + wronged + " deal_broken 0 held" }), "One breach on the record.");
            Assert.That(tape.Lines.Single(), Does.StartWith("deal-outcome [" + breaker + "] "), "One line, the owner's: " + string.Compare(save, evict, StringComparison.Ordinal));
            Assert.That(tape.Lines.Single(), Does.Contain(DealKind.Title(string.CompareOrdinal(save, evict) < 0 ? DealKind.VoteSave : DealKind.VoteEvict).ToLowerInvariant()),
                "The owner is the smaller id.");
            Assert.That(tape.Memories.Count, Is.EqualTo(1));
            Assert.That(mode1.Lines.Count, Is.EqualTo(2), "Mode 1 settles each.");
            if (playerBreaks)
            {
                Assert.That(Moved(reveal.CastTwin, reveal.After, npc, p), Is.EqualTo(-22.5));
                Assert.That(reveal.After.story.grudges.Single(g => g.holderId == npc && g.targetId == p).count, Is.EqualTo(1), "One grudge raised.");
                Assert.That(reveal.Legacy.state.story.grudges.Single(g => g.holderId == npc && g.targetId == p).count, Is.EqualTo(2), "Mode 1 stacks two.");
                Assert.That(tape.Draw, Is.EqualTo(reveal.CastTwin.randomState), "A ballot's breach by the player spreads nothing.");
            }
            else
            {
                Assert.That(Moved(reveal.CastTwin, reveal.After, p, npc), Is.Zero, "The player's view waits for the ballot.");
                int witnesses = reveal.CastTwin.Active.Count(c => c.id != npc && !c.isPlayer);
                Assert.That(witnesses, Is.GreaterThan(0));
                int heard = tape.Ledger.Count(e => e.Contains("heard_about_betrayal"));
                int heard1 = mode1.Ledger.Count(e => e.Contains("heard_about_betrayal"));
                // Every witness draws whether they hear, and a hearer draws the cost: one spread's draws, against mode 1's two.
                Assert.That(tape.Draw, Is.EqualTo(Advance(reveal.CastTwin.randomState, witnesses + heard)), "One spread drawn.");
                Assert.That(reveal.Legacy.state.randomState, Is.EqualTo(Advance(reveal.Cast.randomState, 2 * witnesses + heard1)), "Mode 1 draws two.");
            }
        }

        // ------------------------------------------------------------ once, across duplicates and reloads

        private static EpisodeState RoundTrip(EpisodeState s) => JsonConvert.DeserializeObject<EpisodeState>(PinnedVoteSeason.Json(s),
            new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace });

        /// <summary>
        /// A reveal's effects happen once. The same command again is a duplicate that changes nothing; so it is after a
        /// save and reload, which the season survives exactly; and the following commands - the night's results, the
        /// social week, its turn - never settle a decided row again: its stamp, the frame and the outcome records,
        /// lines and memories the reveal wrote stand as they are, and no later command writes another for them.
        /// </summary>
        [Test]
        public void ARevealsEffectsHappenOnceAcrossADuplicateAndAReload()
        {
            var s = Campaign();
            string npc = NpcVoters(s)[0], other = NpcVoters(s)[1], x = s.nominees[0], y = s.nominees[1];
            s = Promise(s, npc, x);
            s = Deal(s, other, DealKind.VoteEvict, y);
            var reveal = Play(s, y, Pins((other, x)));
            var decided = reveal.After.unifiedCommitments.Where(row => row.kind == UnifiedVoteTogether.Vote && row.settledWeek == s.week).ToList();
            Assert.That(decided.Count, Is.EqualTo(2), "Fixture: a broken promise and a broken deal, its spread drawn.");
            string after = PinnedVoteSeason.Json(reveal.After);

            var engine = ProspectiveVoteFacade.Engine(reveal.After);
            var duplicate = engine.Apply(reveal.Command);
            Assert.That(duplicate.duplicate, Is.True, "The same command again is a duplicate,");
            Assert.That(PinnedVoteSeason.Json(engine.Snapshot), Is.EqualTo(after), "and changes nothing.");

            var reloaded = RoundTrip(reveal.After);
            Assert.That(PinnedVoteSeason.Json(reloaded), Is.EqualTo(after), "The season survives a save and reload exactly.");
            var again = ProspectiveVoteFacade.Engine(reloaded);
            Assert.That(again.Apply(reveal.Command).duplicate, Is.True, "After a reload the reveal is still a duplicate.");

            var outcomes = TapeOf(reveal.CastTwin, reveal.After);
            var live = reveal.After;
            for (int step = 0; step < 6 && live.week == s.week; step++)
            {
                var command = EpisodeEngineTests.NextCommand(live);
                var first = engine.Apply(command);
                var second = again.Apply(command);
                Assert.That(first.accepted && second.accepted, Is.True, command.kind + ": " + first.reason + " / " + second.reason);
                Assert.That(PinnedVoteSeason.Json(second.state), Is.EqualTo(PinnedVoteSeason.Json(first.state)), "The reloaded season plays on identically.");
                var tape = TapeOf(live, first.state);
                Assert.That(tape.Lines.Concat(tape.Memories).Where(text => outcomes.Lines.Concat(outcomes.Memories)
                    .Any(old => old.Substring(old.IndexOf(' ') + 1) == text.Substring(text.IndexOf(' ') + 1))), Is.Empty,
                    command.kind + ": no decided row is settled again.");
                Assert.That(tape.Ledger.Where(e => !e.Contains("heard_about_betrayal")), Is.Empty, command.kind + ": no outcome is recorded again.");
                live = first.state;
            }
            Assert.That(live.week, Is.EqualTo(s.week + 1), "Fixture: the week turned.");
            foreach (var row in decided)
                Assert.That(PinnedVoteSeason.Json(Row(live, row.id)), Is.EqualTo(PinnedVoteSeason.Json(row)), row.id + "'s stamp stands.");
            Assert.That(PinnedVoteSeason.Json(live.unifiedVoteReveals), Is.EqualTo(PinnedVoteSeason.Json(reveal.After.unifiedVoteReveals)), "The archive stands.");
        }

        /// <summary>
        /// The reload invariant needs no new field: a decided row's terminal stamp and its published frame prove the
        /// settlement. A season whose frame is published but whose decided row reads Active again - the state that
        /// would let a reveal's effects run twice - is refused by the core; so is one whose row is stamped with no
        /// frame to prove it, or whose frame is gone.
        /// </summary>
        [Test]
        public void ASettlementIsProvedByItsStampAndItsFrameAndNothingElse()
        {
            var s = Campaign();
            string npc = NpcVoters(s)[0], x = s.nominees[0];
            s = Promise(s, npc, x);
            string id = s.promises.Single(p => p.kind == PromiseKind.Vote && p.fromId == s.playerId).id;
            var reveal = Play(s, x, Pins());

            var unsettled = reveal.After.Clone();
            var row = Row(unsettled, id);
            row.status = DealStatus.Active; row.settledWeek = 0;
            Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(unsettled, out var error), Is.False, "A frame that decided an Active row.");
            Assert.That(error, Is.EqualTo("An Active/Expired Vote row cannot hide an earlier mandatory verdict."));

            var unproved = reveal.After.Clone();
            unproved.unifiedVoteReveals.RemoveAt(unproved.unifiedVoteReveals.Count - 1);
            Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(unproved, out error), Is.False, "A completed reveal with no frame.");

            var reversed = reveal.After.Clone();
            reversed.unifiedVoteReveals.Last().ballots.Single(b => b.voterId == reveal.After.playerId).targetId = s.nominees[1];
            Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(reversed, out error), Is.False, "A frame that is not the actual box.");
        }

        // ------------------------------------------------------------ the endings

        /// <summary>Both games carried past a reveal: the public season observing new rows' owners, and the twin through the seam.</summary>
        private sealed class Carried
        {
            internal PinnedVoteSeason Season;
            internal EpisodeEngine Engine;
            internal EpisodeState Legacy => Season.State;
            internal EpisodeState Prospective => Engine.Snapshot;
        }

        private static Carried Carry(Reveal reveal)
        {
            var season = new PinnedVoteSeason(0, reveal.Legacy.state);
            foreach (var pair in reveal.Owners) season.Owners.Add(pair.Key, pair.Value.Clone());
            season.Frames.Add(reveal.Frame);
            return new Carried { Season = season, Engine = ProspectiveVoteFacade.Engine(reveal.After) };
        }

        /// <summary>The next command of the public season's own walk, through both games: accepted by both, and equal after projection.</summary>
        private static EpisodeState Next(Carried carried, string what, EpisodeCommand command = null)
        {
            command ??= EpisodeEngineTests.NextCommand(carried.Legacy);
            var legacy = carried.Season.Apply(command);
            var prospective = carried.Engine.Apply(command);
            Assert.That(legacy.accepted, Is.True, what + " " + command.kind + ": " + legacy.reason);
            Assert.That(prospective.accepted, Is.True, what + " " + command.kind + " through the seam: " + prospective.reason);
            Assert.That(carried.Season.Supported, Is.True, carried.Season.FirstUnsupported);
            Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(prospective.state, out var error), Is.True, error);
            ProspectiveVoteTwins.AssertProjection(PinnedVoteSeason.Project(legacy.state, carried.Season.Owners, carried.Season.Frames),
                prospective.state, what + " " + command.kind + " (week " + legacy.state.week + ", " + legacy.state.phase + ")");
            return prospective.state;
        }

        private static string Status(EpisodeState s, string id) => Row(s, id).status;

        /// <summary>The night's results given way to the social week, its diary answered: both games, step by step.</summary>
        private static EpisodeState ToSocial(Carried carried)
        {
            var s = carried.Prospective;
            for (int step = 0; step < 4 && (s.phase != EpisodePhase.Social || s.pendingDiary != null); step++)
                s = Next(carried, "The night's results give way");
            Assert.That(s.phase == EpisodePhase.Social && s.pendingDiary == null, Is.True, "Fixture: the social week opened.");
            return s;
        }

        /// <summary>
        /// The week's turn ends what no reveal decided of a promise's week: the house's own vote promises, whose
        /// nominee makers cast no ballot, stand through the reveal and the night's results, and expire as the
        /// social week closes and the week turns - nothing kept or broken, nothing written but the status - as
        /// mode 1's raw promises do. A voting bloc with a nominee, undecided, is a deal: it outlives the turn.
        /// </summary>
        [Test]
        public void TheWeeksTurnEndsAPromiseNoBallotDecided()
        {
            var s = Campaign();
            s = Deal(s, s.nominees[1], DealKind.VoteTogether);
            string bloc = DealId(s, s.nominees[1], DealKind.VoteTogether);
            var houses = s.promises.Where(p => p.kind == PromiseKind.Vote && p.id.StartsWith("promise-npc-", StringComparison.Ordinal)).Select(p => p.id).ToList();
            var reveal = Play(s, s.nominees[0], Pins(NpcVoters(s).Select(v => (v, s.nominees[0])).ToArray()));
            var carried = Carry(reveal);
            int week = s.week;
            while (carried.Prospective.week == week)
            {
                foreach (string id in houses) Assert.That(Status(carried.Prospective, id), Is.EqualTo(DealStatus.Active), id + " stands within its week.");
                var after = Next(carried, "To the week's turn");
                if (after.week == week) continue;
                foreach (string id in houses)
                    Assert.That((Status(after, id), Row(after, id).settledWeek), Is.EqualTo((DealStatus.Expired, 0)), id + " expires as the week turns.");
                Assert.That(Status(after, bloc), Is.EqualTo(DealStatus.Active), "A deal waits for the house's deal pass.");
            }
        }

        /// <summary>
        /// A departure ends every deal still binding the evictee, as a party or as the one it names: the voting bloc
        /// with the nominee voted out - undecided, since a nominee casts no ballot - and the house's unanswered vote
        /// offer from them both expire as the night's results give way to the social week, exactly as mode 1's raw
        /// deals do (EndWithTheEvictee), before the house's own passes read them.
        /// </summary>
        [Test]
        public void ADepartureEndsEveryDealStillBindingTheEvictee()
        {
            var s = HouseCampaign();
            var offer = s.deals.First(d => d.id.StartsWith(NpcDeals.OfferPrefix, StringComparison.Ordinal) && d.type == DealKind.VoteSave
                && d.status == DealStatus.Proposed);
            string evictee = offer.proposerId;
            s = Deal(s, evictee, DealKind.VoteTogether);
            string bloc = DealId(s, evictee, DealKind.VoteTogether);
            var reveal = Play(s, evictee, Pins(NpcVoters(s).Select(v => (v, evictee)).ToArray()));
            Assert.That(reveal.After.Find(evictee).status, Is.EqualTo(ContestantStatus.Jury), "Fixture: the offer's nominee is voted out.");
            Assert.That((Status(reveal.After, bloc), Status(reveal.After, offer.id)), Is.EqualTo((DealStatus.Active, DealStatus.Proposed)),
                "At the night's results both still stand: the departure ending belongs to the next Advance.");
            var carried = Carry(reveal);
            var social = ToSocial(carried);
            Assert.That((Status(social, bloc), Status(social, offer.id)), Is.EqualTo((DealStatus.Expired, DealStatus.Expired)), "Both end with the evictee.");
            Assert.That((Row(social, bloc).settledWeek, Row(social, offer.id).settledWeek), Is.EqualTo((0, 0)), "No verdict.");
        }

        /// <summary>
        /// The house's deal pass ends a deal past its week, and an offer the player never answered: the voting bloc
        /// with the surviving nominee and the house's other vote offer stand through the week's turn and expire as the
        /// next week's campaign opens - mode 1's own boundary (NpcDeals.Expire), at mode 1's own command.
        /// </summary>
        [Test]
        public void TheHousesDealPassEndsADealPastItsWeekAndAnUnansweredOffer()
        {
            var s = HouseCampaign();
            var offers = s.deals.Where(d => d.id.StartsWith(NpcDeals.OfferPrefix, StringComparison.Ordinal) && d.status == DealStatus.Proposed).ToList();
            string survivor = s.nominees.First(id => offers.Any(o => o.proposerId == id)), evictee = s.nominees.Single(id => id != survivor);
            s = Deal(s, survivor, DealKind.VoteTogether);
            string bloc = DealId(s, survivor, DealKind.VoteTogether);
            var pending = offers.Where(o => o.proposerId == survivor).Select(o => o.id).ToList();
            Assert.That(pending, Is.Not.Empty, "Fixture: the surviving nominee's offer waits unanswered.");
            // Each bargain's voter keeps their nominee, the rest vote the evictee out: no vote deal is broken.
            var bargains = s.deals.Where(d => d.id.StartsWith("deal-npc-", StringComparison.Ordinal) && d.type == DealKind.VoteSave).ToList();
            var pins = NpcVoters(s).ToDictionary(v => v, v => evictee, StringComparer.Ordinal);
            foreach (var b in bargains) pins[b.recipientId] = s.nominees.Single(id => id != b.proposerId);
            Assert.That(pins.Values.Count(t => t == evictee) + 1, Is.GreaterThan(pins.Values.Count(t => t == survivor)), "Fixture: the evictee goes.");
            var reveal = Play(s, evictee, pins);
            Assert.That(bargains.All(b => Status(reveal.After, b.id) == DealStatus.Fulfilled), Is.True, "Fixture: every bargain kept.");
            var carried = Carry(reveal);
            int week = s.week;
            for (int step = 0; step < 60; step++)
            {
                var before = carried.Prospective;
                var after = Next(carried, "To next week's campaign");
                bool pass = before.week == week + 1 && after.phase == EpisodePhase.Campaign && before.phase != EpisodePhase.Campaign;
                foreach (string id in pending.Concat(new[] { bloc }))
                    Assert.That(Status(after, id), Is.EqualTo(pass ? DealStatus.Expired : Status(before, id)), id + (pass ? " ends at the deal pass." : " stands until it."));
                if (pass) return;
            }
            Assert.Fail("Fixture: next week's campaign never opened.");
        }

        /// <summary>
        /// A row bound after its week's reveal is next week's: the player says yes to the surviving nominee's vote
        /// offer at the night's results, the box already counted. Its first reveal floor is the next week's, so the
        /// frame just published - in which the player's ballot did keep that nominee - cannot decide it; its term is
        /// this week's, so no later frame can either. It stands Active, unstamped, through the week's turn and ends at
        /// next week's deal pass, as mode 1's raw deal ends - mode 1's at every command.
        /// </summary>
        [Test]
        public void AnOfferAnsweredAfterTheRevealIsNextWeeksAndEndsAtItsDealPass()
        {
            var s = HouseCampaign();
            var offers = s.deals.Where(d => d.id.StartsWith(NpcDeals.OfferPrefix, StringComparison.Ordinal) && d.status == DealStatus.Proposed).ToList();
            string survivor = s.nominees.First(id => offers.Any(o => o.proposerId == id && o.type == DealKind.VoteSave)), evictee = s.nominees.Single(id => id != survivor);
            string answered = offers.First(o => o.proposerId == survivor && o.type == DealKind.VoteSave).id;
            // As the deal-pass case pins the box: each bargain's voter keeps their nominee, the rest vote the evictee out.
            var bargains = s.deals.Where(d => d.id.StartsWith("deal-npc-", StringComparison.Ordinal) && d.type == DealKind.VoteSave).ToList();
            var pins = NpcVoters(s).ToDictionary(v => v, v => evictee, StringComparer.Ordinal);
            foreach (var b in bargains) pins[b.recipientId] = s.nominees.Single(id => id != b.proposerId);
            Assert.That(pins.Values.Count(t => t == evictee) + 1, Is.GreaterThan(pins.Values.Count(t => t == survivor)), "Fixture: the evictee goes.");
            var reveal = Play(s, evictee, pins);
            Assert.That(Status(reveal.After, answered), Is.EqualTo(DealStatus.Proposed), "Fixture: the offer is still unanswered at the night's results.");
            var carried = Carry(reveal);
            int week = s.week;
            var yes = Next(carried, "The player says yes after the reveal",
                ProspectiveVoteTwins.Command(carried.Legacy, EpisodeCommandKind.RespondToDeal, answered, text: EpisodeEngine.AcceptDeal));
            var row = Row(yes, answered);
            Assert.That((row.status, row.voteBindingWeek, row.voteFirstRevealWeek, row.expiresWeek, row.settledWeek),
                Is.EqualTo((DealStatus.Active, week, week + 1, week, 0)), "Bound this week, first judged next week, its term this week's.");
            Assert.That(yes.unifiedVoteReveals.Single(frame => frame.week == week).ballots.Single(b => b.voterId == yes.playerId).targetId,
                Is.EqualTo(evictee), "Fixture: the published frame would have kept the survivor - had it been eligible.");
            Assert.That(yes.ledger.opportunities.Single(o => o.id == answered).response, Is.EqualTo(OpportunityResponse.Taken));
            for (int step = 0; step < 60; step++)
            {
                var before = carried.Prospective;
                var after = Next(carried, "To next week's campaign");
                bool pass = before.week == week + 1 && after.phase == EpisodePhase.Campaign && before.phase != EpisodePhase.Campaign;
                Assert.That((Status(after, answered), Row(after, answered).settledWeek), Is.EqualTo((pass ? DealStatus.Expired : DealStatus.Active, 0)),
                    answered + (pass ? " ends at the deal pass, no verdict." : " stands until it."));
                if (pass) return;
            }
            Assert.Fail("Fixture: next week's campaign never opened.");
        }

        /// <summary>
        /// Production's removal of a party ends their vote rows as it ends their raw ones (StoryHooks.Expel): the
        /// house's vote promise its nominee made, undecided, and the player's voting bloc with them expire as the
        /// social window closes - before the week's turn would have ended the promise on its own. So does a deal that
        /// only names them: a story's vote-to-evict deal between the player and another houseguest, struck in the
        /// social week after the reveal (so next week's), its target the one production removes.
        /// </summary>
        [Test]
        public void AProductionRemovalEndsTheRemovedPartysRows()
        {
            var s = Campaign();
            string removed = s.nominees[1];
            var promise = s.promises.First(p => p.kind == PromiseKind.Vote && p.fromId == removed);
            s = Deal(s, removed, DealKind.VoteTogether);
            string bloc = DealId(s, removed, DealKind.VoteTogether);
            var reveal = Play(s, s.nominees[0], Pins(NpcVoters(s).Select(v => (v, s.nominees[0])).ToArray()));
            var carried = Carry(reveal);
            var social = ToSocial(carried);
            Assert.That(social.phase == EpisodePhase.Social && Production.RemovalWindow(social), Is.True, "Fixture: production's removal window is open.");
            var legacy = carried.Legacy;
            var twin = carried.Prospective;
            // Constructed, as StoryDeal files it (the swing-vote beats strike a vote-to-evict naming the player's pick):
            // the raw deal in mode 1, the canonical row through the Vote store's own admission in mode 2, one id spent.
            string swing = social.Active.First(c => !c.isPlayer && c.id != removed).id, story = "deal-story-" + social.nextSequence;
            Assert.That(twin.nextSequence, Is.EqualTo(legacy.nextSequence));
            legacy.deals.Add(PlayerDeals.Draft(legacy, swing, DealKind.VoteEvict, removed, story)); legacy.nextSequence++;
            Assert.That(ProspectiveVoteFacade.StoreTryAddDeal(twin, PlayerDeals.Draft(twin, swing, DealKind.VoteEvict, removed, story),
                UnifiedCommitments.StoryDeal, out var admission), Is.True, admission);
            twin.nextSequence++;
            Assert.That((Row(twin, story).targetId, Row(twin, story).voteBindingWeek, Row(twin, story).voteFirstRevealWeek),
                Is.EqualTo((removed, s.week, s.week + 1)), "Fixture: struck after the reveal, it names the one removed and is next week's.");
            // Constructed, as OrdinaryVoterRemovalTests constructs it: two strikes, the last the week before; production's
            // own ladder decides the third and schedules the removal - on both copies alike.
            void Schedule(EpisodeState copy)
            {
                var conduct = Production.For(copy, removed, true);
                conduct.strikes = 2; conduct.lastStrikeWeek = copy.week - 1;
                Assert.That(Production.Strike(copy, removed, "fixture"), Is.EqualTo(3));
                Assert.That(copy.story.pendingRemovalId, Is.EqualTo(removed));
            }
            Schedule(legacy); Schedule(twin);
            var observed = carried.Season;
            carried = new Carried { Season = new PinnedVoteSeason(0, ProspectiveVoteTwins.Valid(legacy)), Engine = ProspectiveVoteFacade.Engine(twin) };
            foreach (var pair in observed.Owners) carried.Season.Owners.Add(pair.Key, pair.Value.Clone());
            carried.Season.Owners.Add(story, new ProspectiveVoteOwner { Origin = UnifiedCommitments.StoryDeal, BindingWeek = s.week, FirstWeek = s.week + 1 });
            carried.Season.Frames.AddRange(observed.Frames);
            Assert.That((Status(twin, promise.id), Status(twin, bloc), Status(twin, story)), Is.EqualTo((DealStatus.Active, DealStatus.Active, DealStatus.Active)));
            var after = Next(carried, "The social window closes");
            Assert.That(after.Find(removed).status, Is.EqualTo(ContestantStatus.Expelled));
            Assert.That(after.story.removals.Single(r => r.contestantId == removed).week, Is.EqualTo(s.week), "Removed before the week turned.");
            Assert.That((Status(after, promise.id), Status(after, bloc)), Is.EqualTo((DealStatus.Expired, DealStatus.Expired)));
            Assert.That((Status(after, story), Row(after, story).settledWeek), Is.EqualTo((DealStatus.Expired, 0)), "The deal naming them ends with them.");
        }

        /// <summary>
        /// A price voided: the player names a vote-to-keep-them price for their veto, then breaks their word on the
        /// veto. The canonical price lapses - nobody broke it, so no view, record or draw - and the pair are told, as
        /// mode 1 voids its raw price (VoidThePrice). A ballot's breach voids nothing: that is the recipes' own rule.
        /// </summary>
        [Test]
        public void ABrokenVetoWordVoidsItsCanonicalPrice()
        {
            var s = ProspectiveVoteTwins.Find("a first veto meeting the player holds the veto at",
                seed => ProspectiveVoteTwins.Walk(seed, x => x.phase == EpisodePhase.VetoMeeting && !x.vetoResolved && x.vetoHolderId == x.playerId
                    && x.nominees.Any(id => id != x.playerId) && EpisodeEngine.ReplacementCandidates(x).Any(), EpisodePhase.Veto));
            string target = s.nominees.First(id => id != s.playerId);
            s.randomState = ProspectiveVoteTwins.Draw(true, Negotiation.Chance(s, target, Negotiation.VetoForAPrice, false));
            string priceId = Negotiation.PricePrefix + s.nextSequence;
            var priced = ProspectiveVoteTwins.Both(ProspectiveVoteTwins.Valid(s),
                ProspectiveVoteTwins.Command(s, EpisodeCommandKind.Negotiate, target, text: Negotiation.VetoPriceMove(DealKind.VoteSave)));
            Assert.That(priced.prospective.accepted, Is.True, priced.prospective.reason);
            var mode1 = priced.legacy.state;
            Assert.That(Status(priced.prospective.state, priceId), Is.EqualTo(DealStatus.Active), "Fixture: the price stands, owed for the veto.");
            // The player's word broken: the veto goes unused on the nominee who paid for it.
            var resolve = ProspectiveVoteTwins.Command(mode1, EpisodeCommandKind.ResolveVeto, tag: "unused");
            resolve.useVeto = false;
            var legacy = new EpisodeEngine(mode1).Apply(resolve);
            var prospective = ProspectiveVoteFacade.Engine(priced.prospective.state).Apply(resolve);
            Assert.That(legacy.accepted && prospective.accepted, Is.True, legacy.reason + " / " + prospective.reason);
            var price = Row(prospective.state, priceId);
            Assert.That((price.status, price.settledWeek, price.brokenById), Is.EqualTo((DealStatus.Expired, 0, (string)null)), "Voided: lapsed, not broken.");
            Assert.That(prospective.state.events.Last(e => e.kind == "deal-outcome").audienceIds, Is.EquivalentTo(new[] { target, s.playerId }),
                "The pair are told.");
            ProspectiveVoteTwins.AssertProjection(PinnedVoteSeason.Project(legacy.state, ProspectiveVoteTwins.Owners(mode1), Array.Empty<UnifiedVoteRevealState>()),
                prospective.state, "A broken veto word voids its price");
        }

        private static EpisodeState onTheBlock;

        /// <summary>
        /// A first reveal the player cast no ballot in: the open vote with the player on the block, its whole box
        /// pinned against the other nominee, and the counting Advance played through the seam on the mode-2 twin.
        /// </summary>
        private static EpisodeState RevealOnTheBlock() => (onTheBlock ??= ProspectiveVoteTwins.Find("a first reveal with the player on the block",
            seed =>
            {
                var s = ProspectiveVoteTwins.Walk(seed, x => PinnedVoteSeason.OpenVote(x) && x.nominees.Contains(x.playerId) && x.votes.Count == 0);
                if (s == null) return null;
                string other = s.nominees.Single(id => id != s.playerId);
                foreach (string voter in PinnedVoteSeason.NpcVoters(s).ToList())
                    s.votes.Add(new VoteState { voterId = voter, targetId = other, reason = PinnedVoteSeason.PinnedReason });
                var twin = ProspectiveVoteTwins.Twin(ProspectiveVoteTwins.Valid(s));
                var result = ProspectiveVoteFacade.Engine(twin).Apply(EpisodeEngineTests.Command(twin, EpisodeCommandKind.Advance));
                Assert.That(result.accepted, Is.True, "The reveal through the seam: " + result.reason);
                Assert.That(result.state.evictionResolved && result.state.Find(s.playerId).status == ContestantStatus.Active, Is.True,
                    "Fixture: the box counted, the player kept.");
                return result.state;
            })).Clone();

        /// <summary>
        /// The one void a reveal's own week can write: a counter's voting-bloc price whose payer - the player - was put
        /// on the block after it was struck (the replacement nominee), so no ballot decides the price, and whose payee,
        /// an ordinary voter, lied about their vote: the bought information deal is broken by the payee when the reveal
        /// judges their Told claim (BreakTheDealsOfLyingPartners), after the reveal's verdicts. The price is voided in
        /// that week (VoidThePrice), and the core accepts it Expired there - and refuses it Active, an ending outlived.
        /// A constructed control on a real post-reveal season the player sat out on the block: the counter's two rows
        /// are added as AnswerCounter files them, the information deal as the reveal breaks it; no command produced them
        /// here, and the claim itself is not constructed (the core reads the broken deal, not the claim).
        /// </summary>
        [TestCase(DealStatus.Expired, true)] [TestCase(DealStatus.Active, false)]
        public void AVoidInTheRevealsOwnWeekIsAnEndingTheCoreAccepts(string priceStatus, bool accepted)
        {
            var copy = RevealOnTheBlock();
            var frame = copy.unifiedVoteReveals.Last();
            string player = copy.playerId, payee = PinnedVoteSeason.NpcVoters(copy).First();
            Assert.That((frame.week, copy.nominees.Contains(player)), Is.EqualTo((copy.week, true)), "Fixture: this week's frame, the player on the block.");
            Assert.That(frame.ballots.Any(b => b.voterId == payee) && !frame.ballots.Any(b => b.voterId == player), Is.True,
                "Fixture: the payee voted, the player did not, so nothing decided the price.");
            long sequence = copy.nextSequence++;
            string boughtId = Negotiation.CounterDealPrefix + sequence, priceId = Negotiation.PricePrefix + sequence;
            copy.deals.Add(new DealState { id = boughtId, type = DealKind.InformationSharing, proposerId = player, recipientId = payee,
                status = DealStatus.Broken, brokenById = payee, settledWeek = copy.week, week = copy.week, expiresWeek = 0,
                trustImpact = DealKind.DefaultTrust(DealKind.InformationSharing), linkedDealId = priceId });
            copy.unifiedCommitments.Add(new UnifiedCommitmentState { id = priceId, kind = UnifiedVoteTogether.Vote, sourcePolicy = UnifiedCommitments.DealPolicy,
                origin = UnifiedCommitments.CounterPrice, makerId = player, beneficiaryId = payee, reciprocal = true, subtype = DealKind.VoteTogether,
                createdWeek = copy.week, expiresWeek = copy.week, status = priceStatus, trustImpact = DealKind.DefaultTrust(DealKind.VoteTogether),
                linkedCommitmentId = boughtId, voteBindingWeek = copy.week, voteFirstRevealWeek = copy.week });
            Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(copy, out var error), Is.EqualTo(accepted), error);
            if (!accepted) Assert.That(error, Is.EqualTo("An Active Vote row cannot survive an already-published source departure/void ending."));
        }

        /// <summary>
        /// The final eviction is a departure too: a voting bloc the player struck at the final four with a nominee -
        /// undecided there, since a nominee casts no ballot, and past its week at the final three, where no deal pass
        /// runs - ends with its partner when the final Head of Household sends them to the jury, and the core holds
        /// after it. The bloc ends before the command's reconcile, which reads it at the status it ended from (Active),
        /// so it stays on the record as taken - as it already was, since the player's own proposal marks a deal taken
        /// as it is struck. A mode-2 season through the seam, the bloc's draw and the player's choices its only steering.
        /// </summary>
        [Test]
        public void TheFinalEvictionEndsADealStillBindingItsEvictee()
        {
            string partner = null, bloc = null;
            EpisodeState ended = null;
            ProspectiveVoteTwins.Find("a final eviction of the player's final-four bloc partner", seed =>
            {
                var engine = ProspectiveVoteFacade.Engine(PinnedVoteSeason.Project(PinnedVoteSeason.Fresh(seed),
                    new Dictionary<string, ProspectiveVoteOwner>(), Array.Empty<UnifiedVoteRevealState>()));
                string with = null, id = null;
                for (int step = 0; step < 2000; step++)
                {
                    var s = engine.Snapshot;
                    if (s.phase == EpisodePhase.Finished || s.Find(s.playerId).status != ContestantStatus.Active) return null;
                    var command = EpisodeEngineTests.NextCommand(s);
                    bool steered = true;
                    if (id == null && s.phase == EpisodePhase.Campaign && s.Active.Count() == 4 && PinnedVoteSeason.PlayerVotes(s))
                    {
                        with = s.nominees[0];
                        if (!PlayerDeals.CanPropose(s, with, DealKind.VoteTogether, null, out _)) return null;
                        s.randomState = ProspectiveVoteTwins.Draw(true, PlayerDeals.AcceptanceChance(s, with, DealKind.VoteTogether, null));
                        engine = ProspectiveVoteFacade.Engine(s);
                        id = "deal-player-" + s.nextSequence;
                        command = ProspectiveVoteTwins.Command(s, EpisodeCommandKind.ProposeDeal, with, null, DealKind.VoteTogether);
                    }
                    else if (id != null && command.kind == EpisodeCommandKind.CastVote && s.phase == EpisodePhase.Eviction) command.targetId = s.nominees.Single(n => n != with);
                    else if (id != null && command.kind == EpisodeCommandKind.FinalEvict) command.targetId = with;
                    else steered = false;
                    var result = engine.Apply(command);
                    // The walk's own stock answers can miss a season's offered choices (mode 1 refuses them alike): a miss.
                    if (!steered && !result.accepted) return null;
                    Assert.That(result.accepted, Is.True, "week " + s.week + " " + s.phase + " " + command.kind + ": " + result.reason);
                    if (command.kind == EpisodeCommandKind.ProposeDeal)
                        Assert.That(Status(result.state, id), Is.EqualTo(DealStatus.Active), "Fixture: the bloc was struck.");
                    if (id == null || result.state.phase != EpisodePhase.JuryQuestioning && result.state.phase != EpisodePhase.FinalSpeeches) continue;
                    if (result.state.ledger.power.Last().evicteeId != with) return null;
                    Assert.That(s.phase, Is.EqualTo(EpisodePhase.FinalEviction));
                    Assert.That(Status(s, id), Is.EqualTo(DealStatus.Active), "Fixture: the bloc still bound its partner going into the final eviction.");
                    partner = with; bloc = id; ended = result.state;
                    return result.state;
                }
                return null;
            });
            Assert.That(Status(ended, bloc), Is.EqualTo(DealStatus.Expired), "The bloc ended with " + partner + ".");
            Assert.That(Row(ended, bloc).settledWeek, Is.Zero, "No verdict: the final choice is not a ballot.");
            Assert.That(ended.ledger.opportunities.Single(o => o.id == bloc).response, Is.EqualTo(OpportunityResponse.Taken),
                "A deal the player took stays on the record as taken.");
        }

        /// <summary>
        /// The final eviction ends the evictee's rows before its reconcile, where mode 1 ends them after it - so the
        /// reconcile reads each at the status it ended from, and a row nobody marked taken goes on the record as mode
        /// 1's does: an offer the evictee put to the player at the final four and the player never answered is
        /// ignored there, not expired (or a story's deal, struck without that mark, taken). The whole command equals
        /// mode 1's after projection. A public mode-1 season walked to its final eviction - the final four's ballot
        /// and the final choice steered to keep and then evict the one whose row stands - and its twin projected
        /// there with the owners and frames the walk observed, the one command through both. Constructed: the box
        /// pinned whenever the player is on the block, and the final four's nominees' view of the player (40), set
        /// before the campaign opens so the house's own pass puts its vote offers to them.
        /// </summary>
        [Test]
        public void TheFinalEvictionsReconcileReadsWhatItEndedAsModeOneReadsIt()
        {
            string evictee = null, open = null;
            CommandResult legacy = null, prospective = null;
            PinnedVoteSeason season = null;
            ProspectiveVoteTwins.Find("a final eviction of a finalist whose vote row with the player nobody marked taken", seed =>
            {
                var walk = new PinnedVoteSeason(seed, PinnedVoteSeason.Fresh(seed));
                bool warmed = false;
                for (int step = 0; step < 2000; step++)
                {
                    var s = walk.State;
                    if (s.phase == EpisodePhase.Finished || s.Find(s.playerId).status != ContestantStatus.Active || !walk.Supported) return null;
                    // The player on the block is kept: the whole box pinned against the other nominee.
                    if (PinnedVoteSeason.OpenVote(s) && s.nominees.Contains(s.playerId) && s.votes.Count == 0)
                    {
                        walk.PinBox(PinnedVoteSeason.NpcVoters(s).ToDictionary(id => id, id => s.nominees.Single(n => n != s.playerId), StringComparer.Ordinal));
                        continue;
                    }
                    // The final four's block set, the player off it: its nominees read the player warmly enough to put
                    // a vote to them as the campaign opens - the house's own pass files the offers.
                    if (!warmed && s.phase == EpisodePhase.VetoMeeting && s.vetoResolved && s.Active.Count() == 4 && !s.nominees.Contains(s.playerId))
                    {
                        foreach (string nominee in s.nominees) ProspectiveVoteTwins.Set(s, nominee, s.playerId, 40);
                        var warm = new PinnedVoteSeason(seed, ProspectiveVoteTwins.Valid(s));
                        foreach (var pair in walk.Owners) warm.Owners.Add(pair.Key, pair.Value.Clone());
                        warm.Frames.AddRange(walk.Frames);
                        walk = warm; warmed = true;
                        continue;
                    }
                    var command = EpisodeEngineTests.NextCommand(s);
                    if (command.kind == EpisodeCommandKind.Compete && s.Active.Count() == 3) command.performance = 1;
                    // At the final four the player keeps whoever's row stands, where their ballot can.
                    if (command.kind == EpisodeCommandKind.CastVote && s.phase == EpisodePhase.Eviction && s.Active.Count() == 4)
                    {
                        string kept = s.nominees.FirstOrDefault(id => Unmarked(s, id).Any());
                        if (kept != null) command.targetId = s.nominees.Single(id => id != kept);
                    }
                    if (command.kind == EpisodeCommandKind.FinalEvict)
                    {
                        command.targetId = s.Active.Where(c => c.id != s.hohId && !c.isPlayer).Select(c => c.id)
                            .FirstOrDefault(id => Unmarked(s, id).Any());
                        if (command.targetId == null) return null;
                    }
                    EpisodeState twin = null;
                    Dictionary<string, List<string>> unmarked = null;
                    if (s.phase == EpisodePhase.FinalEviction)
                    {
                        twin = PinnedVoteSeason.Project(s, walk.Owners, walk.Frames);
                        Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(twin, out var error), Is.True, "The projected final eviction: " + error);
                        unmarked = s.Active.Where(c => !c.isPlayer).ToDictionary(c => c.id, c => Unmarked(s, c.id).Select(d => d.id).ToList());
                    }
                    var result = walk.Apply(command);
                    // The walk's own stock answers can miss a season's offered choices: a miss.
                    if (!result.accepted) return null;
                    if (twin == null || result.state.phase == EpisodePhase.FinalEviction) continue;
                    string gone = result.state.ledger.power.Last().evicteeId;
                    if (gone == null || !unmarked.TryGetValue(gone, out var rows) || rows.Count == 0) return null;
                    legacy = result;
                    prospective = ProspectiveVoteFacade.Engine(twin).Apply(command);
                    evictee = gone; open = rows[0]; season = walk;
                    return prospective.state;
                }
                return null;
            });
            Assert.That(prospective.accepted, Is.True, "The final eviction through the seam: " + prospective.reason);
            var row = Row(prospective.state, open);
            Assert.That((row.status, row.settledWeek), Is.EqualTo((DealStatus.Expired, 0)), open + " ended with " + evictee + ", no verdict.");
            var mode1 = legacy.state.ledger.opportunities.Single(o => o.id == open);
            var mode2 = prospective.state.ledger.opportunities.Single(o => o.id == open);
            Assert.That((mode2.response, mode2.note), Is.EqualTo((mode1.response, mode1.note)), "Recorded as mode 1 records it.");
            Assert.That(mode2.response, Is.Not.EqualTo(OpportunityResponse.Expired), "Read at the status it ended from.");
            ProspectiveVoteTwins.AssertProjection(PinnedVoteSeason.Project(legacy.state, season.Owners, season.Frames), prospective.state,
                "The final eviction of " + evictee);
        }

        /// <summary>
        /// The player's vote rows with this houseguest still binding and not on the record as taken: an offer never
        /// answered, or a deal struck without the mark (a story's).
        /// </summary>
        private static IEnumerable<DealState> Unmarked(EpisodeState s, string with) =>
            s.deals.Where(d => KnownBallots.IsVoteDeal(d.type) && DealStatus.Binds(d.status)
                && (d.proposerId == with && d.recipientId == s.playerId || d.proposerId == s.playerId && d.recipientId == with)
                && !s.ledger.opportunities.Any(o => o.id == d.id && o.response == OpportunityResponse.Taken));

        // ------------------------------------------------------------ whole seasons

        /// <summary>
        /// A mode-2 season plays from its first night to its jury's verdict through the seam, every command the public
        /// walk would send accepted: each reveal settles and publishes, and every week's turn, deal pass, departure and
        /// final eviction ends what it should, the complete core holding after every command.
        /// </summary>
        [TestCase(1u)] [TestCase(2u)] [TestCase(3u)] [TestCase(4u)] [TestCase(5u)]
        public void AModeTwoSeasonPlaysToItsEndThroughTheSeam(uint seed)
        {
            var fresh = PinnedVoteSeason.Fresh(seed);
            var engine = ProspectiveVoteFacade.Engine(PinnedVoteSeason.Project(fresh, new Dictionary<string, ProspectiveVoteOwner>(),
                Array.Empty<UnifiedVoteRevealState>()));
            for (int step = 0; step < 2000; step++)
            {
                var s = engine.Snapshot;
                if (s.phase == EpisodePhase.Finished)
                {
                    Assert.That(s.unifiedVoteReveals.Count, Is.EqualTo(s.ledger.power.Count(p => p.tally.Count == 2)), "A frame for every regular reveal.");
                    Assert.That(s.unifiedCommitments.Where(row => row.kind == UnifiedVoteTogether.Vote).All(row => !DealStatus.Binds(row.status) || row.expiresWeek == 0),
                        Is.True, "Nothing finite still binds.");
                    return;
                }
                var command = EpisodeEngineTests.NextCommand(s);
                var result = engine.Apply(command);
                Assert.That(result.accepted, Is.True, "week " + s.week + " " + s.phase + " " + command.kind + ": " + result.reason);
            }
            Assert.Fail("The season did not finish.");
        }

        /// <summary>
        /// Seasons whose walks meet no reader still to move (vote family V5) equal mode 1's after projection at every one of
        /// their commands, reveals, endings and the final eviction included, to the jury's verdict. Since V5b every seed of
        /// 1..16 in a house of 8 but seed 4, whose threat reader V5c moves; ModeTwoSeasonSweepTests plays seeds 1..32 in every
        /// house size, the busy player's too, and names where each that differs first does.
        /// </summary>
        [TestCase(1u)] [TestCase(2u)] [TestCase(3u)] [TestCase(5u)] [TestCase(6u)] [TestCase(7u)] [TestCase(8u)] [TestCase(9u)]
        [TestCase(10u)] [TestCase(11u)] [TestCase(12u)] [TestCase(13u)] [TestCase(14u)] [TestCase(15u)] [TestCase(16u)]
        public void ASeasonWithoutReaderGapsEqualsModeOneToTheFinish(uint seed)
        {
            var fresh = PinnedVoteSeason.Fresh(seed);
            var season = new PinnedVoteSeason(seed, fresh);
            var engine = ProspectiveVoteFacade.Engine(PinnedVoteSeason.Project(fresh, new Dictionary<string, ProspectiveVoteOwner>(),
                Array.Empty<UnifiedVoteRevealState>()));
            for (int step = 0; step < 2000; step++)
            {
                var s = season.State;
                if (s.phase == EpisodePhase.Finished) return;
                var command = ModeTwoReaderSweep.Next(s);
                var legacy = season.Apply(command);
                var prospective = engine.Apply(command);
                Assert.That(legacy.accepted && prospective.accepted, Is.True, command.kind + ": " + legacy.reason + " / " + prospective.reason);
                Assert.That(season.Supported, Is.True, season.FirstUnsupported);
                ProspectiveVoteTwins.AssertProjection(PinnedVoteSeason.Project(legacy.state, season.Owners, season.Frames), prospective.state,
                    "Week " + s.week + " " + s.phase + " " + command.kind);
            }
            Assert.Fail("The season did not finish.");
        }

        /// <summary>The season's stream after <paramref name="draws"/> more draws.</summary>
        private static uint Advance(uint state, int draws)
        {
            for (int n = 0; n < draws; n++) { var rng = new SeededRandom(state); rng.NextDouble(); state = rng.State; }
            return state;
        }

        // ------------------------------------------------------------ helpers for the reader still to move (V5)

        /// <summary>
        /// The reveal equals mode 1's but for the wronged party's grudge against the breaker. Its threat scaling
        /// (ThreatAssessment.ReputationThreat) is a reader vote family V5 moves: it counts raw promises, and canonical
        /// breaches only under mode 1, so in mode 2 it counts none - neither this reveal's breach (which the approved
        /// Rule2 policy excludes) nor an earlier one (which it does not). At a first reveal, as here, the two coincide:
        /// mode 1 counts the promise it has just broken. The same holder, target, cause, week and count; a severity
        /// lighter by what one broken promise adds to the breaker's reputation - three threat points, scaled by 60/200
        /// and rounded: never more than one. Not a Rule2 difference: V5 replaces it with the exclusion itself.
        /// </summary>
        private static void AssertParityButTheThreatReadersGap(Reveal reveal, string what, string holder, string breaker)
        {
            var expected = reveal.Projection;
            var mode1 = expected.story.grudges.Single(g => g.holderId == holder && g.targetId == breaker);
            var mode2 = reveal.After.story.grudges.Single(g => g.holderId == holder && g.targetId == breaker);
            Assert.That((mode2.cause, mode2.originWeek, mode2.count), Is.EqualTo((mode1.cause, mode1.originWeek, mode1.count)), what + ": the same grudge.");
            Assert.That(mode1.severity - mode2.severity, Is.InRange(0, 1), what + ": mode 2's threat reader counts no canonical breach (V5).");
            mode1.severity = mode2.severity;
            ProspectiveVoteTwins.AssertProjection(expected, reveal.After, what);
        }
    }
}
