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
        /// The first mode-2 projection a walk of seeds 1..32 reaches that <paramref name="match"/> accepts, with the mode-1 state
        /// it projects - a fixture the real engine played, never a built one - or a failure naming the seeds tried.
        /// </summary>
        internal static (EpisodeState mode1, EpisodeState mode2) Find(string what, Func<EpisodeState, EpisodeState, bool> match, int size = 8)
        {
            for (uint seed = 1; seed <= 32; seed++)
            {
                EpisodeState foundMode1 = null, foundMode2 = null;
                Walk(seed, (mode1, mode2, where) =>
                {
                    if (foundMode2 == null && match(mode1, mode2)) { foundMode1 = mode1; foundMode2 = mode2; }
                }, size, true, () => foundMode2 != null);
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
            switch (Pick(s, seed, 3 + attempt * 5, 9))
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

        /// <summary>Builds the pair (or, <paramref name="told"/>, the control pair) from <see cref="FlipCampaign"/>.</summary>
        internal static FlipPair Flip(bool told)
        {
            var s = FlipCampaign();
            var voters = PinnedVoteSeason.NpcVoters(s).ToList();
            var pair = new FlipPair { PartnerId = voters[0], OtherVoterId = voters[1], EvictId = s.nominees[0], SpareId = s.nominees[1], Told = told };
            s.randomState = ProspectiveVoteTwins.Draw(true, PlayerDeals.AcceptanceChance(s, pair.PartnerId, DealKind.VoteEvict, pair.EvictId));
            s = Accepted(s, ProspectiveVoteTwins.Command(s, EpisodeCommandKind.ProposeDeal, pair.PartnerId, pair.EvictId, DealKind.VoteEvict));
            pair.DealId = s.deals.Single(d => d.type == DealKind.VoteEvict && d.recipientId == pair.PartnerId && d.status == DealStatus.Active).id;
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
            return pair;
        }

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

        /// <summary>A list's JSON, element for element: the form two readers' answers are compared in.</summary>
        internal static string Json(object value) => PinnedVoteSeason.Json(value);
    }
}
