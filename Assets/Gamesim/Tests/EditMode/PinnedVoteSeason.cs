using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>How an actual Vote promise or deal was born, observed across the public command that wrote it.</summary>
    internal sealed class ProspectiveVoteOwner
    {
        internal string Origin;
        internal int BindingWeek, FirstWeek;
        internal ProspectiveVoteOwner Clone() => new ProspectiveVoteOwner { Origin = Origin, BindingWeek = BindingWeek, FirstWeek = FirstWeek };
    }

    /// <summary>
    /// A season played through the public engine in which the only constructed facts are NPC ballots,
    /// pinned on a detached snapshot that is never written back and installed by building the engine
    /// again, which runs public validation. Everything else - the NPC batch, the tally and tie-break,
    /// promise, oath and deal settlement, the claims' verdicts, the reveal's records, the season's
    /// rolls - is the real engine's, from real commands. Nothing here writes a claim, a ledger row, a
    /// memory, an event, a status, a role or the random stream.
    ///
    /// <para>Two ways in, because the batch reveals in the same command when the player does not vote:
    /// <see cref="PinCast"/> changes only the target of ballots the real batch already cast (the player
    /// still owes theirs, so the box is unrevealed); <see cref="PinBox"/>, for a week the player does not
    /// vote in, casts every NPC ballot before the Advance that would otherwise cast and count them in one
    /// step - those ballots carry <see cref="PinnedReason"/> and no private line of their own.</para>
    ///
    /// <para>Every command is checked as the staged prospective fixtures checked it: the source and the
    /// command are not mutated, a refusal leaves the engine as it was, an accepted command moves the
    /// revision by one and leaves a publicly valid season. Each completed regular reveal is recorded as a
    /// frame of the actual box, and each Vote promise or deal is recorded at its birth with the origin
    /// its identity and links name; a birth this observer cannot order clears <see cref="Supported"/>.</para>
    /// </summary>
    internal sealed class PinnedVoteSeason
    {
        internal const string PinnedReason = "A ballot the test fixture pinned.";

        internal readonly uint Seed;
        internal readonly List<UnifiedVoteRevealState> Frames = new List<UnifiedVoteRevealState>();
        internal readonly Dictionary<string, ProspectiveVoteOwner> Owners = new Dictionary<string, ProspectiveVoteOwner>(StringComparer.Ordinal);
        /// <summary>Every pinned ballot, as "week:voter=target", in the order it was pinned.</summary>
        internal readonly List<string> Pins = new List<string>();
        internal bool Supported = true;
        internal string FirstUnsupported;
        internal int Accepted;
        private EpisodeEngine engine;

        internal PinnedVoteSeason(uint seed, EpisodeState fresh)
        {
            Seed = seed;
            engine = new EpisodeEngine(fresh);
        }

        /// <summary>A detached copy of the season as it stands.</summary>
        internal EpisodeState State => engine.Snapshot;

        /// <summary>The staged fixtures' season: every rule a director season has, under the commitment rules, in the given unified mode.</summary>
        internal static EpisodeState Fresh(uint seed, int mode = 1, int size = 8)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size, Roster = CastTemplates.Roster.Regular }, seed);
            Assert.That(s.contestants.Count, Is.EqualTo(size));
            Assert.That(s.promises.Count, Is.Zero); Assert.That(s.deals.Count, Is.Zero);
            s.competitionRulesVersion = CompetitionRules.Current; s.haveNotRulesStartWeek = 1; s.strategyRulesStartWeek = 1;
            EpisodeEngine.EnableStory(s); EpisodeEngine.EnableRead(s); EpisodeEngine.EnableLevers(s); EpisodeEngine.EnableWeek(s);
            EpisodeEngine.EnableEconomy(s); EpisodeEngine.EnableAgency(s); EpisodeEngine.EnableFinale(s); EpisodeEngine.EnableCommitments(s);
            s.unifiedCommitmentRulesVersion = mode; s.unifiedHearingRulesVersion = mode;
            Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);
            return s;
        }

        /// <summary>Applies one public command and observes it. A refusal leaves the engine unchanged.</summary>
        internal CommandResult Apply(EpisodeCommand command)
        {
            var before = engine.Snapshot;
            string stateImage = Json(before), commandImage = Json(command);
            var result = engine.Apply(command);
            Assert.That(Json(before), Is.EqualTo(stateImage));
            Assert.That(Json(command), Is.EqualTo(commandImage));
            if (!result.accepted)
            {
                Assert.That(Json(engine.Snapshot), Is.EqualTo(stateImage));
                return result;
            }
            Assert.That(result.duplicate, Is.False);
            Assert.That(result.state.revision, Is.EqualTo(before.revision + 1));
            Assert.That(EpisodeValidation.TryValidate(result.state, out var error), Is.True, error);
            Accepted++;
            bool reveal = before.phase == EpisodePhase.Eviction && !before.evictionResolved
                && result.state.phase == EpisodePhase.Eviction && result.state.evictionResolved;
            if (reveal) Frames.Add(new UnifiedVoteRevealState { week = result.state.week, ballots = result.state.votes
                .Select(ballot => new UnifiedVoteBallotState { voterId = ballot.voterId, targetId = ballot.targetId }).ToList() });
            Observe(before, result.state, command, reveal);
            return result;
        }

        /// <summary>Applies a command the fixture needs accepted.</summary>
        internal EpisodeState Step(EpisodeCommand command)
        {
            var result = Apply(command);
            Assert.That(result.accepted && !result.duplicate, Is.True, command.kind + ": " + result.reason);
            return result.state;
        }

        internal static bool OpenVote(EpisodeState s) => s.pendingDiary == null && s.phase == EpisodePhase.Eviction
            && !s.evictionResolved && s.evictionStage == EvictionStage.Voting && s.nominees.Count == 2;

        internal static bool PlayerVotes(EpisodeState s) => EpisodeEngine.Voters(s).Any(person => person.id == s.playerId);

        internal static IEnumerable<string> NpcVoters(EpisodeState s) =>
            EpisodeEngine.Voters(s).Where(person => person.id != s.playerId).Select(person => person.id);

        /// <summary>
        /// The real NPC batch: the Advance that casts every missing NPC ballot and, with the player's
        /// still owed, returns before the count (EpisodeEngine's Eviction Advance).
        /// </summary>
        internal void RunNpcBatch()
        {
            var s = State;
            Assert.That(OpenVote(s) && PlayerVotes(s) && !s.votes.Any(ballot => ballot.voterId == s.playerId), Is.True,
                "The real batch runs at an open vote the player still owes a ballot in.");
            var after = Step(EpisodeEngineTests.Command(s, EpisodeCommandKind.Advance));
            Assert.That(after.evictionResolved, Is.False, "The batch does not count while the player's ballot is owed.");
            Assert.That(NpcVoters(after).All(id => after.votes.Any(ballot => ballot.voterId == id)), Is.True, "The batch cast every NPC ballot.");
        }

        /// <summary>
        /// After the real batch: changes the target of the named NPC ballots, and nothing else, on a
        /// detached snapshot, and builds the engine again from it.
        /// </summary>
        internal void PinCast(IReadOnlyDictionary<string, string> targets)
        {
            var pinned = State;
            Assert.That(OpenVote(pinned) && !pinned.votes.Any(ballot => ballot.voterId == pinned.playerId), Is.True,
                "Ballots are pinned between the real batch and the player's own.");
            foreach (var pair in targets.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                Assert.That(pair.Key, Is.Not.EqualTo(pinned.playerId), "The player's ballot is the player's command, never pinned.");
                Assert.That(pinned.nominees, Does.Contain(pair.Value));
                var ballot = pinned.votes.SingleOrDefault(vote => vote.voterId == pair.Key);
                Assert.That(ballot, Is.Not.Null, "Only a ballot the real batch cast is pinned: " + pair.Key);
                ballot.targetId = pair.Value;
                Pins.Add(pinned.week + ":" + pair.Key + "=" + pair.Value);
            }
            engine = new EpisodeEngine(pinned);
        }

        /// <summary>
        /// For a week the player does not vote in: casts every NPC voter's ballot, as named, on a detached
        /// snapshot before the Advance that would cast and count them in one command.
        /// </summary>
        internal void PinBox(IReadOnlyDictionary<string, string> targets)
        {
            var pinned = State;
            var voters = NpcVoters(pinned).ToList();
            Assert.That(OpenVote(pinned) && !PlayerVotes(pinned) && pinned.votes.Count == 0, Is.True,
                "A whole box is pinned only before any ballot, in a week the player does not vote in.");
            Assert.That(targets.Keys.OrderBy(id => id, StringComparer.Ordinal), Is.EqualTo(voters.OrderBy(id => id, StringComparer.Ordinal)),
                "A pinned box names every NPC voter and nobody else.");
            foreach (string voter in voters)
            {
                Assert.That(pinned.nominees, Does.Contain(targets[voter]));
                pinned.votes.Add(new VoteState { voterId = voter, targetId = targets[voter], reason = PinnedReason });
                Pins.Add(pinned.week + ":" + voter + "=" + targets[voter]);
            }
            engine = new EpisodeEngine(pinned);
        }

        /// <summary>The player's real ballot.</summary>
        internal EpisodeState CastVote(string target)
        {
            var command = EpisodeEngineTests.Command(State, EpisodeCommandKind.CastVote);
            command.targetId = target;
            return Step(command);
        }

        /// <summary>The real Advance that counts the box and reveals.</summary>
        internal EpisodeState Reveal()
        {
            var after = Step(EpisodeEngineTests.Command(State, EpisodeCommandKind.Advance));
            Assert.That(after.evictionResolved, Is.True, "The Advance after every ballot reveals.");
            return after;
        }

        /// <summary>
        /// Plays the open vote with every NPC ballot against <paramref name="evict"/>: pinned after the
        /// real batch, with the player's own real ballot against the same nominee, when the player votes;
        /// a pinned box otherwise. Returns the revealed season.
        /// </summary>
        internal EpisodeState PlayPinnedVote(string evict)
        {
            var s = State;
            Assert.That(OpenVote(s) && s.nominees.Contains(evict), Is.True);
            var targets = NpcVoters(s).ToDictionary(id => id, id => evict, StringComparer.Ordinal);
            if (PlayerVotes(s))
            {
                RunNpcBatch();
                PinCast(targets);
                CastVote(evict);
            }
            else PinBox(targets);
            return Reveal();
        }

        /// <summary>The first gate a birth failed; the season is not a supported projection source after it.</summary>
        private void Unsupported(string reason)
        {
            Supported = false;
            if (FirstUnsupported == null) FirstUnsupported = "week " + State.week + ": " + reason;
        }

        private void Observe(EpisodeState before, EpisodeState after, EpisodeCommand command, bool reveal)
        {
            foreach (var promise in after.promises.Where(row => row.kind == PromiseKind.Vote))
                if (!Owners.ContainsKey(promise.id))
                {
                    // No ordering claim is inferred for a creation inside the reveal callback.
                    if (reveal || promise.week != after.week) Unsupported("A Vote promise birth has no supported ordering: " + promise.id);
                    if (before.promises.Any(row => row.id == promise.id)) Unsupported("A Vote promise was not observed at birth: " + promise.id);
                    string origin = promise.id.StartsWith("promise-npc-", StringComparison.Ordinal) ? UnifiedCommitments.NpcPromise
                        : promise.targetId == null ? UnifiedCommitments.StoryPromise : UnifiedCommitments.PlayerPromise;
                    Owners.Add(promise.id, new ProspectiveVoteOwner { Origin = origin, BindingWeek = promise.week,
                        FirstWeek = promise.week + (origin == UnifiedCommitments.StoryPromise && Frames.Any(frame => frame.week == promise.week) ? 1 : 0) });
                }
            foreach (var deal in after.deals.Where(row => KnownBallots.IsVoteDeal(row.type)))
            {
                if (!Owners.TryGetValue(deal.id, out var observed))
                {
                    if (reveal || deal.week != after.week || before.deals.Any(row => row.id == deal.id))
                        Unsupported("A Vote deal birth has no supported ordering: " + deal.id);
                    string origin = DealOrigin(after, deal);
                    if (origin == null) { Unsupported("A Vote deal's owner is unknown: " + deal.id); continue; }
                    bool unbound = origin == UnifiedCommitments.NpcOffer && deal.status == DealStatus.Proposed;
                    bool late = origin == UnifiedCommitments.NpcOffer || origin == UnifiedCommitments.StoryDeal
                        || origin == UnifiedVoteFamilyValidation.VetoAskPrice;
                    observed = new ProspectiveVoteOwner { Origin = origin, BindingWeek = unbound ? 0 : deal.week,
                        FirstWeek = unbound ? 0 : deal.week + (late && Frames.Any(frame => frame.week == deal.week) ? 1 : 0) };
                    Owners.Add(deal.id, observed);
                }
                if (observed.BindingWeek == 0 && deal.status == DealStatus.Active)
                {
                    var old = before.deals.FirstOrDefault(row => row.id == deal.id);
                    if (old?.status != DealStatus.Proposed || command.kind != EpisodeCommandKind.RespondToDeal
                        || command.targetId != deal.id || command.text != EpisodeEngine.AcceptDeal)
                        Unsupported("An offer's binding was not the player's observed yes: " + deal.id);
                    observed.BindingWeek = after.week;
                    observed.FirstWeek = after.week + (Frames.Any(frame => frame.week == after.week) ? 1 : 0);
                }
            }
        }

        /// <summary>The origin a Vote deal's own identity and links name.</summary>
        internal static string DealOrigin(EpisodeState s, DealState row)
        {
            if (row.id.StartsWith("deal-player-", StringComparison.Ordinal)) return UnifiedCommitments.PlayerDeal;
            if (row.id.StartsWith("deal-npc-", StringComparison.Ordinal)) return UnifiedCommitments.NpcDeal;
            if (row.id.StartsWith(NpcDeals.OfferPrefix, StringComparison.Ordinal)) return UnifiedCommitments.NpcOffer;
            if (row.id.StartsWith("deal-story-", StringComparison.Ordinal)) return UnifiedCommitments.StoryDeal;
            if (row.id.StartsWith("deal-lobby-", StringComparison.Ordinal)) return UnifiedVoteFamilyValidation.VoteLobby;
            if (row.id.StartsWith(Negotiation.CounterDealPrefix, StringComparison.Ordinal)) return UnifiedCommitments.CounterDeal;
            if (!row.id.StartsWith(Negotiation.PricePrefix, StringComparison.Ordinal)) return null;
            var bought = CommitmentReferences.FindDeal(s, row.linkedDealId);
            if (bought?.id.StartsWith(Negotiation.CounterDealPrefix, StringComparison.Ordinal) == true) return UnifiedCommitments.CounterPrice;
            if (bought?.id.StartsWith("deal-veto-", StringComparison.Ordinal) == true) return UnifiedVoteFamilyValidation.VetoAskPrice;
            return bought?.id.StartsWith("deal-player-", StringComparison.Ordinal) == true && bought.type == DealKind.VetoUse
                ? UnifiedVoteFamilyValidation.OwnVetoPrice : null;
        }

        /// <summary>
        /// The detached explicit-2 projection of a mode-1 season: every actual Vote promise and deal moves,
        /// with its observed owner, ID, terms, links and terminal stamps, into a canonical row; the archive
        /// is the recorded frames. No row is dropped or inferred and the source is not touched.
        /// </summary>
        internal static EpisodeState Project(EpisodeState source, IReadOnlyDictionary<string, ProspectiveVoteOwner> owners,
            IEnumerable<UnifiedVoteRevealState> frames)
        {
            string before = Json(source); var s = source.Clone();
            s.unifiedCommitmentRulesVersion = UnifiedVoteFamilyValidation.Version;
            s.unifiedVoteReveals = frames.Select(frame => frame.Clone()).ToList();
            foreach (var raw in s.promises.Where(row => row.kind == PromiseKind.Vote).ToArray())
            {
                Assert.That(owners.TryGetValue(raw.id, out var observed), Is.True,
                    "Every actual Vote promise keeps its observed owner, not inferred history: " + raw.id);
                var row = new UnifiedCommitmentState { id = raw.id, kind = UnifiedVoteTogether.Vote,
                    sourcePolicy = UnifiedCommitments.PromisePolicy, origin = observed.Origin,
                    makerId = raw.fromId, beneficiaryId = raw.toId, createdWeek = raw.week, expiresWeek = raw.expiresWeek,
                    status = raw.status == PromiseStatus.Fulfilled ? DealStatus.Fulfilled : raw.status == PromiseStatus.Broken
                        ? DealStatus.Broken : raw.status == PromiseStatus.Expired ? DealStatus.Expired : DealStatus.Active,
                    trustImpact = raw.impact, targetId = raw.targetId, settledWeek = raw.settledWeek, brokenById = raw.brokenById,
                    voteBindingWeek = observed.BindingWeek, voteFirstRevealWeek = observed.FirstWeek };
                if (row.status == DealStatus.Broken) row.settlementEffectKey = UnifiedVoteHistory.Key(row, row.settledWeek);
                s.unifiedCommitments.Add(row);
            }
            foreach (var raw in s.deals.Where(row => KnownBallots.IsVoteDeal(row.type)).ToArray())
            {
                Assert.That(owners.TryGetValue(raw.id, out var observed), Is.True,
                    "Every actual Vote deal keeps its observed owner, not inferred history: " + raw.id);
                var row = UnifiedVoteAdmission.FromDeal(raw, observed.Origin, observed.BindingWeek, observed.FirstWeek);
                row.settledWeek = raw.settledWeek; row.brokenById = raw.brokenById;
                if (row.status == DealStatus.Broken) row.settlementEffectKey = UnifiedVoteHistory.Key(row, row.settledWeek);
                s.unifiedCommitments.Add(row);
            }
            s.promises.RemoveAll(row => row.kind == PromiseKind.Vote);
            s.deals.RemoveAll(row => KnownBallots.IsVoteDeal(row.type));
            Assert.That(Json(source), Is.EqualTo(before));
            return s;
        }

        internal static string Json(object value) => JsonConvert.SerializeObject(value);
    }
}
