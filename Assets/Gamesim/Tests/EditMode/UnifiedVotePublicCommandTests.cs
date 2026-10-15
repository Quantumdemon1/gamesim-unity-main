using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Vote family V6: the unified vote rules as the game ships them. A fresh season - <see cref="SeasonBuilder.Create"/> and
    /// <see cref="ShippedRules.ApplyFresh"/>, as the director starts one - is mode 2, and it plays through the public engine alone:
    /// <c>new EpisodeEngine</c> and <c>Apply</c>, the constructor and validation every caller uses, no seam and no facade. The busy
    /// player's promises and deals, its answers to the house's offers, the house's own words and deals, every regular reveal and
    /// its archive frame, the endings, the final eviction and the jury's verdict all go through public commands, and every state
    /// a command leaves is a public season. Replayed command for command it is the same season, byte for byte. A season in mode 1
    /// stays mode 1 through its commands, and one in mode 0 stays mode 0: no command converts a season.
    ///
    /// <para>The creators a busy walk does not reach by itself - a story's word or deal, a lobby's, a counter's two rows, a veto's
    /// price - are driven by their own fixtures in the V3 to V5 tests through <see cref="ProspectiveVoteTwins.Both"/>, which since
    /// V6 also plays each command through the public engine on the same twin and holds it to the seam's result, byte for byte.</para>
    /// </summary>
    public sealed class UnifiedVotePublicCommandTests
    {
        /// <summary>A fresh season as the director starts one.</summary>
        internal static EpisodeState Fresh(uint seed, int size = 8)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size }, seed);
            ShippedRules.ApplyFresh(s);
            return s;
        }

        /// <summary>What a public season reached.</summary>
        internal sealed class Played
        {
            internal EpisodeState Fresh, Final;
            internal readonly List<EpisodeCommand> Commands = new List<EpisodeCommand>();
            internal readonly List<string> States = new List<string>();
            internal readonly SortedSet<string> Origins = new SortedSet<string>(StringComparer.Ordinal), Endings = new SortedSet<string>(StringComparer.Ordinal);
            internal int RegularReveals, Frames;
            internal bool FinalEviction;
        }

        private static readonly Dictionary<string, Played> Cache = new Dictionary<string, Played>(StringComparer.Ordinal);

        /// <summary>
        /// One season walked through the public engine to the jury's verdict by the busy player (<see cref="ModeTwoReaderSweep.Busy"/>),
        /// the plain walk moving it on wherever the busy player is refused. Every accepted state is checked as it is reached.
        /// </summary>
        internal static Played Play(uint seed, int size)
        {
            string key = seed + "/" + size;
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var played = new Played { Fresh = Fresh(seed, size) };
            Assert.That(played.Fresh.unifiedCommitmentRulesVersion, Is.EqualTo(UnifiedVoteFamilyValidation.Version), "A fresh season is mode 2.");
            var engine = new EpisodeEngine(played.Fresh);
            for (int step = 0; step < 4000 && engine.Snapshot.phase != EpisodePhase.Finished; step++)
            {
                var s = engine.Snapshot;
                var applied = Step(engine, seed, out var used);
                played.Commands.Add(used);
                Observe(played, s, applied.state, "seed " + seed + " size " + size + " week " + s.week + " " + s.phase + " " + used.kind);
            }
            played.Final = engine.Snapshot;
            Assert.That(played.Final.phase, Is.EqualTo(EpisodePhase.Finished), "seed " + seed + ": the season reaches the jury's verdict.");
            TestContext.Out.WriteLine("seed " + seed + " size " + size + ": " + played.Commands.Count + " commands, " + played.RegularReveals
                + " regular reveals, origins " + string.Join(", ", played.Origins) + "; endings " + string.Join(", ", played.Endings) + ".");
            Cache[key] = played;
            return played;
        }

        /// <summary>
        /// One accepted public command: the eager player's word or deal on the vote (<see cref="Eager"/>), else the busy player's move
        /// (<see cref="ModeTwoReaderSweep.Busy"/>), else the plain walk's. A refused attempt must leave the engine as it was.
        /// </summary>
        internal static CommandResult Step(EpisodeEngine engine, uint seed, out EpisodeCommand used)
        {
            var s = engine.Snapshot;
            string image = PinnedVoteSeason.Json(s);
            for (int attempt = 0; attempt < 5; attempt++)
            {
                var own = attempt < 2 ? Eager(s, attempt) : ModeTwoReaderSweep.Busy(s, seed, attempt - 2);
                if (own == null) continue;
                var tried = engine.Apply(own);
                if (tried.accepted) { used = own; return tried; }
                Assert.That(PinnedVoteSeason.Json(engine.Snapshot), Is.EqualTo(image), "A refusal installs nothing.");
            }
            used = ModeTwoReaderSweep.Next(s);
            var next = engine.Apply(used);
            Assert.That(next.accepted, Is.True, "seed " + seed + " week " + s.week + " " + s.phase + " " + used.kind + ": " + next.reason);
            return next;
        }

        /// <summary>
        /// The eager player: answers an offer waiting on them (one in three declined), and in a campaign they vote in, with an action
        /// left, gives an ordinary voter their word to evict a nominee (attempt 0) or proposes a vote deal - to evict, to save, to vote
        /// together, by the week - to another (attempt 1). Choices keyed on the week and revision, never the season's stream.
        /// </summary>
        internal static EpisodeCommand Eager(EpisodeState s, int attempt)
        {
            if (s.pendingDiary != null || s.Find(s.playerId)?.status != ContestantStatus.Active) return null;
            var offer = NpcDeals.Pending(s).FirstOrDefault();
            if (offer != null)
                return attempt == 0 ? Command(s, EpisodeCommandKind.RespondToDeal, offer.id, null, s.revision % 3 == 0 ? "decline" : EpisodeEngine.AcceptDeal) : null;
            if (s.phase != EpisodePhase.Campaign || s.nominees.Count != 2 || !EpisodeEngine.Voters(s).Any(v => v.isPlayer)
                || EpisodeEngine.SocialActionsSpent(s) >= EpisodeEngine.SocialActionBudget(s)) return null;
            var voters = EpisodeEngine.Voters(s).Where(v => !v.isPlayer).Select(v => v.id).ToList();
            if (voters.Count == 0) return null;
            if (attempt == 0)
            {
                string to = voters[s.week % voters.Count];
                return Command(s, EpisodeCommandKind.PromiseVote, to, s.nominees[s.week % 2]);
            }
            string with = voters[(s.week + 1) % voters.Count];
            var kinds = new[] { DealKind.VoteEvict, DealKind.VoteSave, DealKind.VoteTogether };
            for (int k = 0; k < kinds.Length; k++)
            {
                string kind = kinds[(s.week + k) % kinds.Length];
                string about = kind == DealKind.VoteTogether ? null : s.nominees[(s.week + k) % 2];
                if (PlayerDeals.CanPropose(s, with, kind, about, out _)) return Command(s, EpisodeCommandKind.ProposeDeal, with, about, kind);
            }
            return null;
        }

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind, string target, string second, string text = null) =>
            new EpisodeCommand
            {
                id = "v6-public-" + s.revision + "-" + kind + "-" + target + "-" + second, actorId = s.playerId, kind = kind,
                targetId = target, secondTargetId = second, text = text, expectedRevision = s.revision, expectedPhase = s.phase,
            };

        private static void Observe(Played played, EpisodeState before, EpisodeState after, string where)
        {
            Assert.That(after.unifiedCommitmentRulesVersion, Is.EqualTo(UnifiedVoteFamilyValidation.Version), where + ": mode 2 throughout.");
            Assert.That(EpisodeValidation.TryValidate(after, out var error), Is.True, where + ": a public season. " + error);
            Assert.That(after.promises.Any(p => p.kind == PromiseKind.Vote) || after.deals.Any(d => KnownBallots.IsVoteDeal(d.type)), Is.False,
                where + ": no raw Vote row; the family's rows are canonical.");
            foreach (var row in after.unifiedCommitments.Where(r => r.kind == UnifiedVoteTogether.Vote))
            {
                played.Origins.Add(row.origin);
                if (row.status != DealStatus.Active && row.status != DealStatus.Proposed) played.Endings.Add(row.status);
            }
            bool reveal = before.phase == EpisodePhase.Eviction && !before.evictionResolved && after.evictionResolved;
            if (reveal)
            {
                played.RegularReveals++;
                Assert.That(after.unifiedVoteReveals.Last().week, Is.EqualTo(after.week), where + ": the reveal published its frame.");
                Assert.That(after.unifiedVoteReveals.Last().ballots.Select(b => b.voterId + ">" + b.targetId),
                    Is.EqualTo(after.votes.Select(v => v.voterId + ">" + v.targetId)), where + ": the frame is the private box, in its order.");
            }
            played.Frames = after.unifiedVoteReveals.Count;
            if (before.phase == EpisodePhase.FinalEviction && after.phase != EpisodePhase.FinalEviction) played.FinalEviction = true;
            played.States.Add(Hash(after));
        }

        /// <summary>A state's digest: SHA-256 of its JSON, so a replay compares every state without holding a season's worth of them.</summary>
        private static string Hash(EpisodeState s)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(PinnedVoteSeason.Json(s))));
        }

        /// <summary>The seasons: seed 6 is a player who stays to the end at both sizes, seed 2 one the house evicts in week two.</summary>
        private static readonly (uint seed, int size)[] Seasons = { (2u, 8), (6u, 8), (6u, 12) };

        [TestCase(2u, 8)] [TestCase(6u, 8)] [TestCase(6u, 12)]
        public void AFreshSeasonPlaysModeTwoThroughThePublicEngineToTheJurysVerdict(uint seed, int size)
        {
            var played = Play(seed, size);
            var s = played.Final;
            Assert.That(played.RegularReveals, Is.GreaterThan(0));
            Assert.That(played.Frames, Is.EqualTo(played.RegularReveals), "One archive frame for every regular reveal, and no other.");
            Assert.That(played.FinalEviction, Is.True, "The final Head of Household's eviction, by public command.");
            Assert.That(s.winnerId, Is.Not.Null);
            Assert.That(s.votes.Count, Is.GreaterThan(0), "The jury voted.");
            Assert.That(s.contestants.Count(c => c.status == ContestantStatus.Winner), Is.EqualTo(1));
            Assert.That(s.unifiedCommitments.Count(r => r.kind == UnifiedVoteTogether.Vote), Is.GreaterThan(0), "The season held Vote rows.");
            Assert.That(s.unifiedCommitments.Where(r => r.kind == UnifiedVoteTogether.Vote).All(r => r.status != DealStatus.Active || r.expiresWeek == 0),
                Is.True, "Nothing finite is left binding at the end.");
        }

        /// <summary>
        /// What the seasons reach, together: the player's own vote promises and deals, the house's vote offers they answered, the
        /// house's own words and deals between its houseguests, and every ending - kept, broken, expired, declined.
        /// </summary>
        [Test]
        public void TheSeasonsReachTheCreatorsAndEndingsThePlayerAndTheHouseMake()
        {
            var origins = new SortedSet<string>(StringComparer.Ordinal); var endings = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var (seed, size) in Seasons)
            {
                var played = Play(seed, size);
                origins.UnionWith(played.Origins); endings.UnionWith(played.Endings);
            }
            TestContext.Out.WriteLine("Origins: " + string.Join(", ", origins) + ". Endings: " + string.Join(", ", endings) + ".");
            Assert.That(origins, Is.SupersetOf(new[] { UnifiedCommitments.PlayerPromise, UnifiedCommitments.PlayerDeal, UnifiedCommitments.NpcOffer,
                UnifiedCommitments.NpcPromise, UnifiedCommitments.NpcDeal }));
            Assert.That(endings, Is.SupersetOf(new[] { DealStatus.Fulfilled, DealStatus.Broken, DealStatus.Expired, DealStatus.Declined }));
        }

        [Test]
        public void ReplayedCommandForCommandTheSeasonIsTheSameByteForByte()
        {
            var played = Play(6u, 8);
            var engine = new EpisodeEngine(Fresh(6u, 8));
            Assert.That(PinnedVoteSeason.Json(engine.Snapshot), Is.EqualTo(PinnedVoteSeason.Json(played.Fresh)), "The same fresh season.");
            for (int i = 0; i < played.Commands.Count; i++)
            {
                var result = engine.Apply(played.Commands[i]);
                Assert.That(result.accepted, Is.True, "command " + i + ": " + result.reason);
                Assert.That(Hash(result.state), Is.EqualTo(played.States[i]), "command " + i + " (" + played.Commands[i].kind + ")");
            }
            Assert.That(PinnedVoteSeason.Json(engine.Snapshot), Is.EqualTo(PinnedVoteSeason.Json(played.Final)));
        }

        /// <summary>
        /// No command converts a season: one recorded in mode 1 (D6: mode-1 saves stay mode 1 forever) keeps its raw Vote rows and no
        /// archive through its commands, and one in mode 0 stays mode 0 - the busy player's commitments, its reveals and all.
        /// </summary>
        [TestCase(0)] [TestCase(1)]
        public void AnOlderModesSeasonStaysInItsModeThroughCommands(int mode)
        {
            var engine = new EpisodeEngine(PinnedVoteSeason.Fresh(2u, mode));
            bool vote = false;
            for (int step = 0; step < 4000 && engine.Snapshot.phase != EpisodePhase.Finished; step++)
            {
                var s = engine.Snapshot;
                CommandResult applied = null;
                for (int attempt = 0; attempt < 3 && applied == null; attempt++)
                {
                    var own = ModeTwoReaderSweep.Busy(s, 2u, attempt);
                    if (own == null) break;
                    var tried = engine.Apply(own);
                    if (tried.accepted) applied = tried;
                }
                applied = applied ?? engine.Apply(ModeTwoReaderSweep.Next(s));
                Assert.That(applied.accepted, Is.True, applied.reason);
                var after = applied.state;
                Assert.That((after.unifiedCommitmentRulesVersion, after.unifiedVoteReveals.Count), Is.EqualTo((mode, 0)), "Mode " + mode + " throughout.");
                Assert.That(after.unifiedCommitments.Any(r => r.kind == UnifiedVoteTogether.Vote), Is.False, "No canonical Vote row.");
                vote |= after.promises.Any(p => p.kind == PromiseKind.Vote) || after.deals.Any(d => KnownBallots.IsVoteDeal(d.type));
            }
            Assert.That(engine.Snapshot.phase, Is.EqualTo(EpisodePhase.Finished));
            Assert.That(vote, Is.True, "The season held raw Vote rows, as its mode keeps them.");
        }

        /// <summary>
        /// The NPC world's operation under mode 2 (EpisodeEngine.PrepareNpcOperation, as the director drives it): at each free-time
        /// window of a public season, a clock second is prepared - never installed - as a public mode-2 candidate.
        /// </summary>
        [Test]
        public void AnNpcOperationIsPreparedUnderModeTwoByThePublicEngine()
        {
            var engine = new EpisodeEngine(Fresh(6u, 8));
            int prepared = 0;
            for (int step = 0; step < 4000 && engine.Snapshot.phase != EpisodePhase.Finished && prepared < 5; step++)
            {
                var s = engine.Snapshot;
                if (NpcSocialState.IsEligible(s))
                {
                    var request = new NpcOperationRequest
                    {
                        kind = NpcOperationKind.Tick, sessionId = s.sessionId, expectedRevision = s.revision, expectedPhase = s.phase,
                        expectedClockTick = s.npcSocial.clockTick, targetClockTick = s.npcSocial.clockTick + 1, freeRoamReady = true,
                    };
                    var result = engine.PrepareNpcOperation(request);
                    Assert.That(result.accepted, Is.True, "week " + s.week + " " + s.phase + ": " + result.reason);
                    Assert.That(PinnedVoteSeason.Json(engine.Snapshot), Is.EqualTo(PinnedVoteSeason.Json(s)), "Prepared, never installed.");
                    Assert.That(result.candidate.unifiedCommitmentRulesVersion, Is.EqualTo(UnifiedVoteFamilyValidation.Version));
                    Assert.That(EpisodeValidation.TryValidate(result.candidate, out var error), Is.True, error);
                    Assert.That(result.candidate.npcSocial.clockTick, Is.EqualTo(s.npcSocial.clockTick + 1));
                    prepared++;
                }
                Step(engine, 6u, out _);
            }
            Assert.That(prepared, Is.EqualTo(5), "Five free-time windows prepared a second.");
        }

        /// <summary>The scene's fallback season (ContentCatalog.Create, EpisodeDirector) is not a fresh shipped season: it stays mode 0.</summary>
        [Test]
        public void TheScenesFallbackSeasonStaysModeZero()
        {
            var s = ContentCatalog.Create(2511);
            Assert.That((s.unifiedCommitmentRulesVersion, s.unifiedHearingRulesVersion), Is.EqualTo((0, 0)));
            Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);
        }

        private static string Json(object value) => JsonConvert.SerializeObject(value);
    }
}
