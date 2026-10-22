using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The counterpart-case harness of vote family V5 (pattern P2): a public mode-1 season walked to its end by a busy
    /// scripted player through <see cref="PinnedVoteSeason"/>, and at every accepted command the mode-1 state and its
    /// exact mode-2 projection (<see cref="PinnedVoteSeason.Project"/>, with the owners and frames the walk observed)
    /// handed to a check - a reader's mode-1 answer against its answer on the projection. Nothing is pinned: every
    /// ballot, draw and line is the real engine's.
    ///
    /// <para>The busy player makes the commitments the readers read - vote promises, vote, safety and final two deals,
    /// answers to the house's offers, safety promises, and now and then asks a voter how they will vote - each chosen
    /// by a coin keyed on the season's revision and the seed, never the season's stream; whatever it is refused, the
    /// plain walk (<see cref="EpisodeEngineTests.NextCommand"/>) moves the season on. A walk stops where a birth no
    /// longer has an observed owner (<see cref="PinnedVoteSeason.Supported"/>), so a projection never infers one.</para>
    /// </summary>
    internal static class ModeTwoReaderSweep
    {
        /// <summary>What a walk reached.</summary>
        internal sealed class Walked
        {
            internal uint Seed;
            internal int Commands, VotePromises, VoteDeals, Reveals, VoteOffers;
            internal bool Finished;
            internal string Unsupported;
            public override string ToString() => "seed " + Seed + ": " + Commands + " commands, " + VotePromises + " vote promises, "
                + VoteDeals + " vote deals (" + VoteOffers + " offered to the player), " + Reveals + " reveals"
                + (Finished ? ", finished" : "") + (Unsupported != null ? ", stopped: " + Unsupported : "");
        }

        /// <summary>
        /// Walks one seed and checks every accepted command: <paramref name="check"/> gets the mode-1 state, its mode-2
        /// projection and where the walk is. The walk ends at the jury's verdict, or where its births stop being observed.
        /// </summary>
        internal static Walked Walk(uint seed, Action<EpisodeState, EpisodeState, string> check, int size = 8, bool busy = true, Func<bool> done = null)
        {
            var season = new PinnedVoteSeason(seed, PinnedVoteSeason.Fresh(seed, size: size));
            var walked = new Walked { Seed = seed };
            for (int step = 0; step < 3000 && (done == null || !done()); step++)
            {
                var s = season.State;
                if (s.phase == EpisodePhase.Finished) { walked.Finished = true; break; }
                CommandResult applied = null;
                for (int attempt = 0; busy && attempt < 3 && applied == null; attempt++)
                {
                    var own = Busy(s, seed, attempt);
                    if (own == null) break;
                    var tried = season.Apply(own);
                    if (tried.accepted) applied = tried;
                }
                if (applied == null)
                {
                    var next = Next(s);
                    applied = season.Apply(next);
                    Assert.That(applied.accepted, Is.True, "seed " + seed + " week " + s.week + " " + s.phase + " " + next.kind + ": " + applied.reason);
                }
                walked.Commands++;
                if (!season.Supported) { walked.Unsupported = season.FirstUnsupported; break; }
                var mode1 = applied.state;
                check(mode1, PinnedVoteSeason.Project(mode1, season.Owners, season.Frames),
                    "seed " + seed + " week " + mode1.week + " " + mode1.phase + " after " + (applied.state.revision) + " commands");
            }
            var last = season.State;
            walked.VotePromises = last.promises.Count(p => p.kind == PromiseKind.Vote);
            walked.VoteDeals = last.deals.Count(d => KnownBallots.IsVoteDeal(d.type));
            walked.VoteOffers = last.deals.Count(d => KnownBallots.IsVoteDeal(d.type) && d.id.StartsWith(NpcDeals.OfferPrefix, StringComparison.Ordinal));
            walked.Reveals = season.Frames.Count;
            TestContext.Out.WriteLine(walked);
            return walked;
        }

        /// <summary>
        /// One season played twice, command by command: the public mode-1 game through <see cref="PinnedVoteSeason"/>, and the
        /// mode-2 game through the internal engine seam from the same fresh season, each given the command the mode-1 game's
        /// player chose. Stops at the first command after which the mode-2 state is not the mode-1 state's projection, and
        /// says where - "week W Phase Kind: the first differing fields" - or "finished" at the jury's verdict.
        /// </summary>
        internal static string Lockstep(uint seed, int size, bool busy)
        {
            var fresh = PinnedVoteSeason.Fresh(seed, size: size);
            var season = new PinnedVoteSeason(seed, fresh);
            var engine = ProspectiveVoteFacade.Engine(PinnedVoteSeason.Project(fresh, new Dictionary<string, ProspectiveVoteOwner>(),
                Array.Empty<UnifiedVoteRevealState>()));
            for (int step = 0; step < 3000; step++)
            {
                var s = season.State;
                if (s.phase == EpisodePhase.Finished) return "finished";
                EpisodeCommand command = null;
                for (int attempt = 0; busy && attempt < 3 && command == null; attempt++)
                {
                    var own = Busy(s, seed, attempt);
                    if (own == null) break;
                    if (new EpisodeEngine(s).Apply(own).accepted) command = own;
                }
                command = command ?? Next(s);
                var legacy = season.Apply(command);
                var prospective = Observed(engine, command, "seed " + seed + " week " + s.week + " " + s.phase + " " + command.kind);
                string where = "week " + s.week + " " + s.phase + " " + command.kind;
                Assert.That(legacy.accepted, Is.True, "seed " + seed + " " + where + ": " + legacy.reason);
                if (!prospective.accepted) return where + ": refused in mode 2: " + prospective.reason;
                if (!season.Supported) return where + ": unsupported " + season.FirstUnsupported;
                var projection = PinnedVoteSeason.Project(legacy.state, season.Owners, season.Frames);
                var differences = Differences(projection, prospective.state);
                if (differences.Count == 0) continue;
                string designed = Designed(projection, prospective.state, differences);
                if (designed == null) return where + ": " + string.Join(", ", differences.Select(d => d.Split('[', ':')[0]).Distinct());
                // A designed difference: from here the two seasons may part, so mode 2 plays on by itself, to the finish.
                return "designed at " + where + ": " + designed + "; mode 2 then " + PlayOn(engine, seed, busy);
            }
            return "unfinished";
        }

        /// <summary>
        /// A mode-2 command with the walk observer on (vote family V5e): every reveal plan it ran is recorded, and once the command
        /// is through, the owners the archive rebuilds (<see cref="AssertOwnersRebuilt"/>) must be the plan's.
        /// </summary>
        internal static CommandResult Observed(EpisodeEngine engine, EpisodeCommand command, string where)
        {
            var plans = new List<UnifiedVoteIncident>();
            CommandResult result;
            using (ProspectiveVoteFacade.ObservePlans(plans)) result = engine.Apply(command);
            if (result.accepted && plans.Count > 0) AssertOwnersRebuilt(result.state, plans, where);
            return result;
        }

        /// <summary>
        /// The walk observer's check (vote family V5e): past a reveal, <see cref="UnifiedVoteHistory.Incidents"/> and
        /// <see cref="UnifiedVoteHistory.Fulfillments"/> of its week are the groups its plan selected - owner, actor, the one
        /// it was given to, evidence, deal and consequence, in the plan's order - and the row-level <see cref="UnifiedVoteHistory.Breaches"/>
        /// are the archive's incidents, every week.
        /// </summary>
        internal static void AssertOwnersRebuilt(EpisodeState after, IReadOnlyList<UnifiedVoteIncident> plans, string where)
        {
            foreach (int week in plans.Select(group => group.Week).Distinct())
            {
                var plan = plans.Where(group => group.Week == week).ToList();
                Assert.That(Json(UnifiedVoteHistory.Incidents(after).Where(group => group.Week == week)), Is.EqualTo(Json(plan.Where(group => !group.Kept))),
                    where + ": the week's rebuilt breaches are its plan's.");
                Assert.That(Json(UnifiedVoteHistory.Fulfillments(after).Where(group => group.Week == week)), Is.EqualTo(Json(plan.Where(group => group.Kept))),
                    where + ": the week's rebuilt fulfillments are its plan's.");
            }
            Assert.That(Json(UnifiedVoteHistory.Breaches(after)), Is.EqualTo(Json(UnifiedVoteHistory.Incidents(after))),
                where + ": the rows' incidents are the archive's.");
        }

        /// <summary>One command played through both games of a lockstep walk: the mode-1 result's projection and the mode-2 result.</summary>
        internal sealed class Injected
        {
            internal EpisodeState Before, Projection, Mode2;
            internal CommandResult Legacy, Prospective;
            internal EpisodeCommand Command;
        }

        /// <summary>
        /// A lockstep walk (<see cref="Lockstep"/>) that, at the first moment <paramref name="inject"/> names a command for the
        /// mode-1 state, plays that command through both games and returns them; null where the walk parts from mode 1 first,
        /// or finishes without one.
        /// </summary>
        internal static Injected LockstepUntil(uint seed, int size, bool busy, Func<EpisodeState, EpisodeCommand> inject)
        {
            var fresh = PinnedVoteSeason.Fresh(seed, size: size);
            var season = new PinnedVoteSeason(seed, fresh);
            var engine = ProspectiveVoteFacade.Engine(PinnedVoteSeason.Project(fresh, new Dictionary<string, ProspectiveVoteOwner>(),
                Array.Empty<UnifiedVoteRevealState>()));
            for (int step = 0; step < 3000; step++)
            {
                var s = season.State;
                if (s.phase == EpisodePhase.Finished) return null;
                var injected = inject(s);
                EpisodeCommand command = injected;
                for (int attempt = 0; command == null && busy && attempt < 3; attempt++)
                {
                    var own = Busy(s, seed, attempt);
                    if (own == null) break;
                    if (new EpisodeEngine(s).Apply(own).accepted) command = own;
                }
                command = command ?? Next(s);
                var legacy = season.Apply(command);
                var prospective = Observed(engine, command, "seed " + seed + " week " + s.week + " " + s.phase + " " + command.kind);
                if (injected != null)
                    return new Injected { Before = s, Command = command, Legacy = legacy, Prospective = prospective, Mode2 = prospective.state,
                        Projection = legacy.accepted ? PinnedVoteSeason.Project(legacy.state, season.Owners, season.Frames) : null };
                if (!legacy.accepted || !prospective.accepted || !season.Supported) return null;
                if (Differences(PinnedVoteSeason.Project(legacy.state, season.Owners, season.Frames), prospective.state).Count > 0) return null;
            }
            return null;
        }

        /// <summary>
        /// What Rule2 writes once where mode 1 writes it for each row: the line (events, and the ids it mints), the memory, the
        /// relationship and its record (relationships, ledger), the Story lanes, and the witness loop's draws (randomState). A
        /// difference anywhere else - a row, a phase, a ballot - is no Rule2 overlap however the week's rows settled.
        /// </summary>
        private static readonly HashSet<string> Rule2Writes = new HashSet<string>(StringComparer.Ordinal)
            { "events", "nextSequence", "memories", "relationships", "ledger", "story", "randomState" };

        /// <summary>
        /// The designed difference a reveal's states show, or null: a Rule2 overlap (vote family V4) - a pair's two rows decided
        /// the same way this week, and nothing differs but what Rule2 writes (<see cref="Rule2Writes"/>) - or the approved
        /// current-reveal exclusion (vote family V5c) - nothing differs but the Story grudges a breaker of this reveal's own Vote
        /// rows drew, each the same grudge, at most one lighter in mode 2, whose threat scaling left this reveal's breach out of
        /// the breaker's reputation.
        /// </summary>
        internal static string Designed(EpisodeState projection, EpisodeState prospective, List<string> differences)
        {
            // Rule2 (vote family V4, the approved overlap policy): the reveal decided two rows of one pair the same way, and
            // settles their consequences once - its line, memory, record and Story lanes run for the owner alone.
            if (prospective.unifiedCommitments.Where(r => r.kind == UnifiedVoteTogether.Vote && r.settledWeek == prospective.week
                    && (r.status == DealStatus.Fulfilled || r.status == DealStatus.Broken))
                .GroupBy(r => r.status + "|" + string.Join("|", new[] { r.makerId, r.beneficiaryId }.OrderBy(id => id, StringComparer.Ordinal)))
                .Any(group => group.Count() > 1))
                return differences.All(d => Rule2Writes.Contains(d.Split('[', ':')[0])) ? "a Rule2 overlap" : null;
            if (differences.Any(d => !d.StartsWith("story", StringComparison.Ordinal))) return null;
            var one = projection.story.Clone(); var two = prospective.story.Clone();
            var left = one.grudges; var right = two.grudges;
            if (left.Count != right.Count) return null;
            var breakers = new HashSet<string>(prospective.unifiedCommitments.Where(r => r.kind == UnifiedVoteTogether.Vote && r.status == DealStatus.Broken
                && r.settledWeek == prospective.week && r.brokenById != null).Select(r => r.brokenById), StringComparer.Ordinal);
            for (int i = 0; i < left.Count; i++)
            {
                if (Json(left[i]) == Json(right[i])) continue;
                double lighter = left[i].severity - right[i].severity;
                if (left[i].holderId != right[i].holderId || left[i].targetId != right[i].targetId || left[i].cause != right[i].cause
                    || left[i].originWeek != right[i].originWeek || left[i].count != right[i].count || lighter <= 0 || lighter > 1
                    || !breakers.Contains(right[i].targetId)) return null;
                right[i].severity = left[i].severity;
            }
            one.grudges.Clear(); two.grudges.Clear();
            return Json(left) == Json(right) && Json(one) == Json(two) ? "the current-reveal exclusion" : null;
        }

        /// <summary>The mode-2 game alone from here, the same player, to the jury's verdict: "finished", or where it was refused.</summary>
        private static string PlayOn(EpisodeEngine engine, uint seed, bool busy)
        {
            for (int step = 0; step < 3000; step++)
            {
                var s = engine.Snapshot;
                if (s.phase == EpisodePhase.Finished) return "finished";
                CommandResult applied = null;
                for (int attempt = 0; busy && attempt < 3 && applied == null; attempt++)
                {
                    var own = Busy(s, seed, attempt);
                    if (own == null) break;
                    var tried = Observed(engine, own, "seed " + seed + " week " + s.week + " " + s.phase + " " + own.kind);
                    if (tried.accepted) applied = tried;
                }
                if (applied != null) continue;
                var next = Next(s);
                var result = Observed(engine, next, "seed " + seed + " week " + s.week + " " + s.phase + " " + next.kind);
                if (!result.accepted) return "refused week " + s.week + " " + s.phase + " " + next.kind + ": " + result.reason;
            }
            return "unfinished";
        }

        /// <summary>
        /// The first mode-2 projection a walk of seeds 1..32 reaches that <paramref name="match"/> accepts, with the mode-1 state
        /// it projects - a fixture the real engine played, never a built one - or a failure naming the seeds tried.
        /// </summary>
        internal static (EpisodeState mode1, EpisodeState mode2) Find(string what, Func<EpisodeState, EpisodeState, bool> match, int size = 8, bool busy = true)
        {
            for (uint seed = 1; seed <= 32; seed++)
            {
                EpisodeState foundMode1 = null, foundMode2 = null;
                Walk(seed, (mode1, mode2, where) =>
                {
                    if (foundMode2 == null && match(mode1, mode2)) { foundMode1 = mode1; foundMode2 = mode2; }
                }, size, busy, () => foundMode2 != null);
                if (foundMode2 != null)
                {
                    TestContext.Out.WriteLine(what + ": seed " + seed + ", week " + foundMode2.week + ".");
                    return (foundMode1.Clone(), foundMode2.Clone());
                }
            }
            Assert.Fail("No walk of seeds 1..32 reaches " + what + ".");
            return (null, null);
        }

        /// <summary>
        /// The busy player's move, or none: answers an offer waiting on them first, then in the social or the campaign
        /// spends an action on a commitment while any are left. A coin keyed on the revision, week, seed and attempt picks.
        /// </summary>
        internal static EpisodeCommand Busy(EpisodeState s, uint seed, int attempt)
        {
            if (s.pendingDiary != null) return null;
            var me = s.Find(s.playerId);
            if (me == null || me.status != ContestantStatus.Active) return null;
            if (attempt == 0)
            {
                var offer = NpcDeals.Pending(s).FirstOrDefault();
                if (offer != null)
                    return Command(s, EpisodeCommandKind.RespondToDeal, offer.id, null, Pick(s, seed, 1, 4) == 0 ? "decline" : EpisodeEngine.AcceptDeal);
            }
            bool social = s.phase == EpisodePhase.Social, campaign = s.phase == EpisodePhase.Campaign;
            if (!social && !campaign) return null;
            if (EpisodeEngine.SocialActionsSpent(s) >= EpisodeEngine.SocialActionBudget(s)) return null;
            var npcs = s.Active.Where(c => !c.isPlayer).ToList();
            if (npcs.Count == 0) return null;
            var a = npcs[Pick(s, seed, 2 + attempt * 5, npcs.Count)];
            bool voter = EpisodeEngine.Voters(s).Any(v => v.id == s.playerId);
            switch (Pick(s, seed, 3 + attempt * 5, 11))
            {
                case 0:
                case 1:
                    if (!campaign || !voter) return null;
                    var nominee = s.nominees.Where(id => id != a.id).ToList();
                    return nominee.Count == 0 ? null : Command(s, EpisodeCommandKind.PromiseVote, a.id, nominee[Pick(s, seed, 4, nominee.Count)]);
                case 2:
                case 3:
                case 4:
                {
                    var kinds = PlayerDeals.Available(s, a.id);
                    var vote = kinds.Where(KnownBallots.IsVoteDeal).ToList();
                    var pool = vote.Count > 0 && Pick(s, seed, 5, 3) > 0 ? vote : kinds;
                    if (pool.Count == 0) return null;
                    string kind = pool[Pick(s, seed, 6 + attempt, pool.Count)];
                    string about = kind == DealKind.TargetAgreement ? PlayerDeals.Subjects(s, a.id).FirstOrDefault()
                        : kind == DealKind.VoteSave || kind == DealKind.VoteEvict
                            ? s.nominees.FirstOrDefault(id => id != a.id && id != s.playerId && PlayerDeals.CanPropose(s, a.id, kind, id, out _)) : null;
                    return Command(s, EpisodeCommandKind.ProposeDeal, a.id, about, kind);
                }
                case 5: return Command(s, EpisodeCommandKind.PromiseSafety, a.id);
                case 6:
                    if (!campaign || !VoteRead.Available(s)) return null;
                    var unasked = EpisodeEngine.Voters(s).FirstOrDefault(v => !v.isPlayer && !EpisodeEngine.AskedThisWeek(s, v.id));
                    return unasked == null ? null : Command(s, EpisodeCommandKind.AskVote, unasked.id);
                case 7: return Command(s, EpisodeCommandKind.PromiseFinalTwo, a.id);
                // A conversation, which may start a story with them (EpisodeEngine.TryStartFromConversation).
                case 8: return Command(s, EpisodeCommandKind.SmallTalk, a.id);
                case 9: return Command(s, EpisodeCommandKind.DiscussGame, a.id);
                default: return null;
            }
        }

        // ------------------------------------------------------------ the flip pair (pattern P3)

        /// <summary>
        /// Two mode-2 seasons from one moment, right after the reveal, that differ only in a ballot the player cannot
        /// know: their partner's on a vote deal they struck together. In <see cref="Kept"/> the partner voted the deal's
        /// way and in <see cref="Broken"/> the other way; a second voter flipped the other way round, so the tally and the
        /// evictee are the same. With <see cref="Told"/> the player had asked the partner, through a real AskVote, and holds
        /// a Told claim the reveal judged - kept in one, not in the other: the control, which a reader may show.
        /// </summary>
        internal sealed class FlipPair
        {
            internal EpisodeState Kept, Broken;
            internal string PartnerId, OtherVoterId, DealId, EvictId, SpareId;
            /// <summary>With <see cref="Flip(bool, bool)"/>'s word: the partner's vote promise to the player, broken in both.</summary>
            internal string WordId;
            internal bool Told;

            /// <summary>A player-facing reader gives byte-identical answers on the two: it shows nothing of the hidden ballot.</summary>
            internal void AssertBlind(string reader, Func<EpisodeState, object> read)
            {
                Assert.That(Told, Is.False, "The blind pair holds no claim.");
                Assert.That(Json(read(Broken)), Is.EqualTo(Json(read(Kept))), reader + " shows nothing of a ballot the player cannot know.");
            }

            /// <summary>The control: with the player told, the reader's answers differ.</summary>
            internal void AssertControl(string reader, Func<EpisodeState, object> read)
            {
                Assert.That(Told, Is.True, "The control pair holds the Told claim.");
                Assert.That(Json(read(Broken)), Is.Not.EqualTo(Json(read(Kept))), reader + " shows what the player was told and the reveal judged.");
            }
        }

        private static EpisodeState flipCampaign;

        /// <summary>A first campaign the player votes in beside four houseguests, a vote deal with the first of them open to propose.</summary>
        private static EpisodeState FlipCampaign() => (flipCampaign ??= ProspectiveVoteTwins.Find("a first campaign the player votes in beside four houseguests",
            seed => ProspectiveVoteTwins.Walk(seed, s => s.phase == EpisodePhase.Campaign && PinnedVoteSeason.PlayerVotes(s)
                && PinnedVoteSeason.NpcVoters(s).Count() >= 4 && s.nominees.Count == 2
                && PlayerDeals.CanPropose(s, PinnedVoteSeason.NpcVoters(s).First(), DealKind.VoteEvict, s.nominees[0], out _)))).Clone();

        /// <summary>
        /// Builds the pair (or, <paramref name="told"/>, the control pair) from <see cref="FlipCampaign"/>. With <paramref name="word"/>
        /// the partner has also given the player their word on the vote, as a story gives it (EpisodeEngine.StoryPromise: no target, so
        /// any ballot of theirs breaks it): filed as a story files it, its id the season's next sequence, consumed, on the public mode-1
        /// season, which public validation accepts. Their one ballot then breaks the word in both seasons, and in <see cref="FlipPair.Broken"/>
        /// the deal with it - one Rule2 incident there, two groups in <see cref="FlipPair.Kept"/> (the V5 review's finding 8).
        /// </summary>
        internal static FlipPair Flip(bool told, bool word = false)
        {
            var s = FlipCampaign();
            var voters = PinnedVoteSeason.NpcVoters(s).ToList();
            var pair = new FlipPair { PartnerId = voters[0], OtherVoterId = voters[1], EvictId = s.nominees[0], SpareId = s.nominees[1], Told = told };
            s.randomState = ProspectiveVoteTwins.Draw(true, PlayerDeals.AcceptanceChance(s, pair.PartnerId, DealKind.VoteEvict, pair.EvictId));
            s = Accepted(s, ProspectiveVoteTwins.Command(s, EpisodeCommandKind.ProposeDeal, pair.PartnerId, pair.EvictId, DealKind.VoteEvict));
            pair.DealId = s.deals.Single(d => d.type == DealKind.VoteEvict && d.recipientId == pair.PartnerId && d.status == DealStatus.Active).id;
            if (word)
            {
                pair.WordId = "promise-" + s.nextSequence;
                s.promises.Add(new PromiseState { id = pair.WordId, fromId = pair.PartnerId, toId = s.playerId, kind = PromiseKind.Vote,
                    status = PromiseStatus.Active, week = s.week, expiresWeek = s.week });
                s.nextSequence++;
                s = ProspectiveVoteTwins.Valid(s);
            }
            if (told)
            {
                // Warm enough to answer at all, as the negotiation fixtures set a view; the answer is the real command's.
                ProspectiveVoteTwins.Set(s, pair.PartnerId, s.playerId, 40);
                s = Accepted(s, ProspectiveVoteTwins.Command(s, EpisodeCommandKind.AskVote, pair.PartnerId));
                Assert.That(s.ledger.claims.Any(k => k.week == s.week && k.voterId == pair.PartnerId && k.source == ClaimSource.Told), Is.True,
                    "Fixture: the partner told the player a vote.");
            }
            else Assert.That(s.ledger.claims.Any(k => k.week == s.week && k.voterId == pair.PartnerId), Is.False, "Fixture: the player holds no claim of the partner's.");
            var engine = new EpisodeEngine(ProspectiveVoteTwins.Valid(s));
            EpisodeEngineTests.OpenTheVote(engine);
            var batch = engine.Apply(EpisodeEngineTests.Command(engine.Snapshot, EpisodeCommandKind.Advance));
            Assert.That(batch.accepted && !batch.state.evictionResolved, Is.True, batch.reason);
            pair.Kept = Reveal(batch.state, pair, true);
            pair.Broken = Reveal(batch.state, pair, false);
            Assert.That(pair.Kept.ledger.power.Last().evicteeId, Is.EqualTo(pair.EvictId));
            Assert.That(pair.Broken.ledger.power.Last().evicteeId, Is.EqualTo(pair.EvictId));
            Assert.That(Json(pair.Broken.ledger.power.Last().tally), Is.EqualTo(Json(pair.Kept.ledger.power.Last().tally)), "The same tally.");
            var kept = pair.Kept.unifiedCommitments.Single(r => r.id == pair.DealId);
            var broken = pair.Broken.unifiedCommitments.Single(r => r.id == pair.DealId);
            Assert.That((kept.status, broken.status, broken.brokenById), Is.EqualTo((DealStatus.Fulfilled, DealStatus.Broken, pair.PartnerId)),
                "The partner's ballot kept the deal in one and broke it in the other.");
            if (word)
                foreach (var state in new[] { pair.Kept, pair.Broken })
                {
                    var promise = state.unifiedCommitments.Single(r => r.id == pair.WordId);
                    Assert.That((promise.origin, promise.status, promise.brokenById), Is.EqualTo((UnifiedCommitments.StoryPromise, DealStatus.Broken, pair.PartnerId)),
                        "The partner's ballot broke their word in both.");
                }
            return pair;
        }

        // ------------------------------------------------------------ a reveal played twice

        /// <summary>One reveal from one moment: the public mode-1 game, its mode-2 twin through the seam, and the mode-1 result's projection.</summary>
        internal sealed class Revealed
        {
            internal EpisodeState Mode1, Mode2, Projection, Cast, CastTwin;
        }

        /// <summary>
        /// From a campaign the player votes in: the real Advances to the open vote and the real NPC batch, the named NPC ballots
        /// pinned, and the player's ballot and the counting Advance played through both games.
        /// </summary>
        internal static Revealed Reveal(EpisodeState campaign, string playerVote, IReadOnlyDictionary<string, string> pins)
        {
            var engine = new EpisodeEngine(ProspectiveVoteTwins.Valid(campaign));
            EpisodeEngineTests.OpenTheVote(engine);
            var batch = engine.Apply(EpisodeEngineTests.Command(engine.Snapshot, EpisodeCommandKind.Advance));
            Assert.That(batch.accepted && !batch.state.evictionResolved, Is.True, batch.reason);
            var pinned = batch.state;
            foreach (var pin in pins) pinned.votes.Single(v => v.voterId == pin.Key).targetId = pin.Value;
            var owners = ProspectiveVoteTwins.Owners(pinned);
            var legacy = new EpisodeEngine(ProspectiveVoteTwins.Valid(pinned));
            var twin = ProspectiveVoteFacade.Engine(ProspectiveVoteTwins.Twin(pinned));
            var cast = ProspectiveVoteTwins.Command(pinned, EpisodeCommandKind.CastVote, playerVote);
            var revealed = new Revealed { Cast = legacy.Apply(cast).state, CastTwin = twin.Apply(cast).state };
            var count = ProspectiveVoteTwins.Command(revealed.Cast, EpisodeCommandKind.Advance, tag: "reveal");
            var mode1 = legacy.Apply(count);
            var mode2 = twin.Apply(count);
            Assert.That(mode1.accepted && mode2.accepted && mode1.state.evictionResolved && mode2.state.evictionResolved, Is.True,
                mode1.reason + " / " + mode2.reason);
            revealed.Mode1 = mode1.state; revealed.Mode2 = mode2.state;
            var frame = new UnifiedVoteRevealState { week = mode1.state.week, ballots = mode1.state.votes
                .Select(v => new UnifiedVoteBallotState { voterId = v.voterId, targetId = v.targetId }).ToList() };
            revealed.Projection = PinnedVoteSeason.Project(mode1.state, owners, new[] { frame });
            return revealed;
        }

        /// <summary>The player's deal, proposed in the real command and agreed by the season's next draw.</summary>
        internal static EpisodeState Strike(EpisodeState s, string with, string type, string about = null)
        {
            Assert.That(PlayerDeals.CanPropose(s, with, type, about, out var why), Is.True, why);
            s = s.Clone();
            s.randomState = ProspectiveVoteTwins.Draw(true, PlayerDeals.AcceptanceChance(s, with, type, about));
            var after = Accepted(s, ProspectiveVoteTwins.Command(s, EpisodeCommandKind.ProposeDeal, with, about, type));
            Assert.That(after.deals.Count(d => d.type == type && d.status == DealStatus.Active && d.recipientId == with && d.targetId == about),
                Is.EqualTo(1), "Fixture: the deal was struck.");
            return after;
        }

        /// <summary>A first campaign the player votes in beside four houseguests (the flip pair's).</summary>
        internal static EpisodeState Campaign() => FlipCampaign();

        /// <summary>The reveal through the mode-2 seam: every NPC ballot pinned after the real batch, the player's own real ballot.</summary>
        private static EpisodeState Reveal(EpisodeState batch, FlipPair pair, bool kept)
        {
            var pinned = batch.Clone();
            foreach (var ballot in pinned.votes.Where(v => v.voterId != pinned.playerId))
                ballot.targetId = ballot.voterId == pair.PartnerId ? (kept ? pair.EvictId : pair.SpareId)
                    : ballot.voterId == pair.OtherVoterId ? (kept ? pair.SpareId : pair.EvictId) : pair.EvictId;
            var engine = ProspectiveVoteFacade.Engine(ProspectiveVoteTwins.Twin(ProspectiveVoteTwins.Valid(pinned)));
            var cast = engine.Apply(ProspectiveVoteTwins.Command(engine.Snapshot, EpisodeCommandKind.CastVote, pair.EvictId));
            Assert.That(cast.accepted, Is.True, cast.reason);
            var reveal = engine.Apply(ProspectiveVoteTwins.Command(cast.state, EpisodeCommandKind.Advance, tag: "reveal"));
            Assert.That(reveal.accepted && reveal.state.evictionResolved, Is.True, reveal.reason);
            return reveal.state;
        }

        private static EpisodeState Accepted(EpisodeState s, EpisodeCommand command)
        {
            var result = new EpisodeEngine(ProspectiveVoteTwins.Valid(s)).Apply(command);
            Assert.That(result.accepted, Is.True, command.kind + ": " + result.reason);
            return result.state;
        }

        /// <summary>The plain walk's command, answering a juror as the finale's rules offer it (the first response) once they are on.</summary>
        internal static EpisodeCommand Next(EpisodeState s)
        {
            var c = EpisodeEngineTests.NextCommand(s);
            if (c.kind == EpisodeCommandKind.AnswerJury && EpisodeEngine.FinaleOn(s))
            {
                var exchange = s.juryExchanges[s.juryQuestionIndex];
                if (exchange.finalistId == s.playerId) c.secondTargetId = FinaleQuestions.Offered(exchange.category, exchange.receiptKind)[0];
            }
            return c;
        }

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind, string target = null, string second = null, string text = null) =>
            new EpisodeCommand
            {
                id = "v5-sweep-" + s.revision + "-" + kind + "-" + target + "-" + second, actorId = s.playerId, kind = kind,
                targetId = target, secondTargetId = second, text = text, expectedRevision = s.revision, expectedPhase = s.phase,
            };

        /// <summary>A coin that has nothing to do with the engine's stream.</summary>
        private static int Pick(EpisodeState s, uint seed, int salt, int n) =>
            n <= 0 ? 0 : (int)((((uint)s.revision * 2654435761u) ^ (seed * 40503u) ^ ((uint)salt * 2246822519u) ^ ((uint)s.week * 97u)) % 100003u) % n;

        /// <summary>
        /// Where a mode-2 state differs from the mode-1 state's projection, without asserting: each differing top-level field
        /// and its first differing element. Empty when equal (the form <see cref="ProspectiveVoteTwins.AssertProjection"/> asserts).
        /// </summary>
        internal static List<string> Differences(EpisodeState projection, EpisodeState prospective)
        {
            var expected = Normal(projection);
            var actual = Normal(prospective);
            var found = new List<string>();
            foreach (var name in expected.Properties().Select(p => p.Name))
            {
                var left = expected[name]; var right = actual[name];
                if (Newtonsoft.Json.Linq.JToken.DeepEquals(left, right)) continue;
                string where = name;
                if (left is Newtonsoft.Json.Linq.JArray l && right is Newtonsoft.Json.Linq.JArray r)
                    for (int i = 0; i < Math.Max(l.Count, r.Count); i++)
                        if (i >= l.Count || i >= r.Count || !Newtonsoft.Json.Linq.JToken.DeepEquals(l[i], r[i]))
                        {
                            where = name + "[" + i + "]: mode 1 " + (i < l.Count ? Short(l[i]) : "none") + " / mode 2 " + (i < r.Count ? Short(r[i]) : "none");
                            break;
                        }
                found.Add(where);
            }
            return found;
        }

        private static string Short(Newtonsoft.Json.Linq.JToken token)
        {
            string text = token.ToString(Newtonsoft.Json.Formatting.None);
            return text.Length > 300 ? text.Substring(0, 300) + "..." : text;
        }

        private static Newtonsoft.Json.Linq.JObject Normal(EpisodeState s)
        {
            var copy = s.Clone();
            copy.unifiedCommitments = copy.unifiedCommitments.OrderBy(row => row.id, StringComparer.Ordinal).ToList();
            return Newtonsoft.Json.Linq.JObject.FromObject(copy);
        }

        /// <summary>A list's JSON, element for element: the form two readers' answers are compared in.</summary>
        internal static string Json(object value) => PinnedVoteSeason.Json(value);
    }
}
