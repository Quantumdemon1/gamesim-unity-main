using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Vote family V5f: the player-facing pages read mode 2 as they read mode 1 - the commitments page and its warnings
    /// (<see cref="CommitmentsRead"/>: the commitments, those at stake in a decision, what a decision would break by the rules
    /// and by the engine's own run, and the warning's words), the pact page (<see cref="AllianceRead"/>) and what a houseguest
    /// says (<see cref="HouseDialogue"/>). Each is an inventory, row by row, as mode 1 reads it. The runtime readers V5f moves
    /// (the goodbye's tone, the recap's carried word, the verification's lookups) are the editor's
    /// (<c>ModeTwoRuntimeReaderTests</c>), on the fixtures here.
    /// </summary>
    public sealed class ModeTwoPageReaderTests
    {
        private static readonly string[] Kinds =
            { CommitmentsRead.DecisionKinds.Nominate, CommitmentsRead.DecisionKinds.Veto, CommitmentsRead.DecisionKinds.Vote, CommitmentsRead.DecisionKinds.FinalEviction };

        /// <summary>
        /// The pages at one of a walk's moments (<see cref="ModeTwoReaderParityTests"/>): the commitments, each houseguest's, what
        /// each kind of decision has at stake, what the decisions open now would break by the rules and the warning that says so,
        /// what the first of them would break by the engine's run, the pact page and every houseguest's lines. A reader that
        /// throws must throw alike.
        /// </summary>
        internal static void CheckPages(EpisodeState mode1, EpisodeState mode2, string where)
        {
            void Same(string reader, Func<EpisodeState, object> read) =>
                Assert.That(Read(mode2, read), Is.EqualTo(Read(mode1, read)), where + ": " + reader + " reads mode 2 as mode 1.");
            var others = mode1.contestants.Where(c => !c.isPlayer).Select(c => c.id).ToList();
            var decisions = Decisions(mode1);
            Same("CommitmentsRead.Of", s => CommitmentsRead.Of(s));
            Same("CommitmentsRead.With", s => others.Select(id => CommitmentsRead.With(s, id)).ToList());
            Same("CommitmentsRead.AtStake", s => Kinds.Select(kind => CommitmentsRead.AtStake(s, kind)).ToList());
            Same("CommitmentsRead.ByTheRules", s => CommitmentsRead.ByTheRules(s, decisions));
            Same("CommitmentsRead.Warning", s => CommitmentsRead.Warning(s, CommitmentsRead.ByTheRules(s, decisions)));
            Same("CommitmentsRead.WouldBreak", s => decisions.Where(d => CommitmentsRead.AtStake(s, d.kind)).Take(3)
                .Select(d => CommitmentsRead.WouldBreak(s, d)).ToList());
            Same("AllianceRead", s => AllianceRead.Read(s));
            Same("HouseDialogue", s => others.Select(id => new[] { HouseDialogue.Greeting(s, id), HouseDialogue.EvictionPlea(s, id),
                HouseDialogue.Response(s, id), HouseDialogue.TalkAcknowledgement(s, id) }).ToList());
        }

        /// <summary>The decisions a page can ask about now: a few nominations, the veto each way, each ballot, each final choice.</summary>
        private static List<CommitmentsRead.Decision> Decisions(EpisodeState s)
        {
            var list = new List<CommitmentsRead.Decision>();
            var candidates = EpisodeEngine.NominationCandidates(s).Select(c => c.id).Take(4).ToList();
            for (int i = 0; i < candidates.Count; i++)
                for (int j = i + 1; j < candidates.Count; j++) list.Add(CommitmentsRead.Decision.Nominate(candidates[i], candidates[j]));
            string replacement = EpisodeEngine.ReplacementCandidates(s).Select(c => c.id).FirstOrDefault();
            list.Add(CommitmentsRead.Decision.Veto(false));
            foreach (string nominee in s.nominees)
            {
                list.Add(CommitmentsRead.Decision.Veto(true, nominee, replacement));
                list.Add(CommitmentsRead.Decision.Vote(nominee));
            }
            foreach (var person in s.Active.Where(c => c.id != s.hohId)) list.Add(CommitmentsRead.Decision.FinalEviction(person.id));
            return list;
        }

        /// <summary>A reader's answer as JSON, or what it threw.</summary>
        private static string Read(EpisodeState s, Func<EpisodeState, object> read)
        {
            try { return Json(read(s)); }
            catch (Exception error) { return "throws " + error.GetType().Name + ": " + error.Message; }
        }

        // ------------------------------------------------------------ the flip pair (pattern P3)

        /// <summary>The player-facing pages V5f moved (also read of the word's flip pair, <see cref="ModeTwoVotePromiseFlipTests"/>).</summary>
        internal static object Reader(string reader, EpisodeState s, string partner)
        {
            var decisions = Decisions(s);
            switch (reader)
            {
                case "CommitmentsRead.Of": return CommitmentsRead.Of(s);
                case "CommitmentsRead.With": return CommitmentsRead.With(s, partner);
                case "CommitmentsRead.AtStake": return Kinds.Select(kind => CommitmentsRead.AtStake(s, kind)).ToList();
                case "CommitmentsRead.ByTheRules": return CommitmentsRead.ByTheRules(s, decisions);
                case "CommitmentsRead.Warning": return CommitmentsRead.Warning(s, CommitmentsRead.ByTheRules(s, decisions));
                case "AllianceRead": return AllianceRead.Read(s);
                case "HouseDialogue":
                    return new[] { HouseDialogue.Greeting(s, partner), HouseDialogue.EvictionPlea(s, partner), HouseDialogue.Response(s, partner),
                        HouseDialogue.TalkAcknowledgement(s, partner) };
                default: throw new ArgumentException(reader);
            }
        }

        [TestCase("CommitmentsRead.Of")] [TestCase("CommitmentsRead.With")] [TestCase("CommitmentsRead.AtStake")]
        [TestCase("CommitmentsRead.ByTheRules")] [TestCase("CommitmentsRead.Warning")] [TestCase("AllianceRead")] [TestCase("HouseDialogue")]
        public void APageShowsNothingOfAHiddenBallot(string reader)
        {
            var pair = ModeTwoReaderSweep.Flip(false);
            pair.AssertBlind(reader, s => Reader(reader, s, pair.PartnerId));
        }

        /// <summary>The control: the commitments page tells how the deal ended once the player was told, and the reveal judged it.</summary>
        [TestCase("CommitmentsRead.Of")] [TestCase("CommitmentsRead.With")]
        public void TheCommitmentsPageShowsWhatThePlayerWasToldAndTheRevealJudged(string reader)
        {
            var pair = ModeTwoReaderSweep.Flip(true);
            pair.AssertControl(reader, s => Reader(reader, s, pair.PartnerId));
        }

        // ------------------------------------------------------------ the runtime readers' fixtures

        /// <summary>
        /// A first campaign's vote-to-evict deal, struck with a voter by the real command: the mode-1 season, which holds it raw,
        /// and its mode-2 twin, which holds it as a canonical Vote row. Both Active, neither in the twin's raw list.
        /// </summary>
        internal static (EpisodeState mode1, EpisodeState mode2, string partner, string dealId) StruckVoteDeal()
        {
            var s = ModeTwoReaderSweep.Campaign();
            string partner = PinnedVoteSeason.NpcVoters(s).First();
            s = ModeTwoReaderSweep.Strike(s, partner, DealKind.VoteEvict, s.nominees[0]);
            string id = s.deals.Single(d => d.type == DealKind.VoteEvict && d.recipientId == partner).id;
            return (s, ProspectiveVoteTwins.Twin(ProspectiveVoteTwins.Valid(s)), partner, id);
        }

        /// <summary>
        /// A voting bloc with the Head of Household, past a reveal they cast no ballot in: the box does not decide it, so it still
        /// binds in both modes as the week closes (mode 1 raw, mode 2 canonical).
        /// </summary>
        internal static (ModeTwoReaderSweep.Revealed reveal, string hoh, string blocId) UndecidedBloc()
        {
            var s = ModeTwoReaderSweep.Campaign();
            string hoh = s.hohId, x = s.nominees[0];
            s = ModeTwoReaderSweep.Strike(s, hoh, DealKind.VoteTogether);
            string id = s.deals.Single(d => d.type == DealKind.VoteTogether && d.recipientId == hoh).id;
            // Every voter votes x out: no tie, so the Head of Household casts nothing.
            var reveal = ModeTwoReaderSweep.Reveal(s, x, PinnedVoteSeason.NpcVoters(s).ToDictionary(v => v, v => x, StringComparer.Ordinal));
            return (reveal, hoh, id);
        }

        [Test]
        public void AStruckVoteDealIsFoundThroughTheViewsInBothModes()
        {
            var (mode1, mode2, partner, id) = StruckVoteDeal();
            Assert.That(mode2.deals.Any(d => d.id == id), Is.False, "Fixture: mode 2 holds the vote deal canonical only.");
            Assert.That(mode2.unifiedCommitments.Single(row => row.id == id).status, Is.EqualTo(DealStatus.Active));
            Assert.That(Json(CommitmentReferences.FindDeal(mode2, id)), Is.EqualTo(Json(CommitmentReferences.FindDeal(mode1, id))));
            Assert.That(CommitmentReferences.RawDeals(mode2).Select(d => d.id), Does.Contain(id), "The raw view holds it, as mode 1's list does.");
            Assert.That(Json(CommitmentsRead.With(mode2, partner)), Is.EqualTo(Json(CommitmentsRead.With(mode1, partner))));
        }

        /// <summary>
        /// The pre-ballot warning (BallotRules) reads mode 1's raw rows - mode 2's vote deal among them: a ballot for the other
        /// nominee breaks the vote-to-evict deal by the player's own ballot, and both modes warn of it alike.
        /// </summary>
        [Test]
        public void TheBallotsWarningNamesAVoteDealInBothModes()
        {
            var (mode1, mode2, partner, id) = StruckVoteDeal();
            var legacy = new EpisodeEngine(ProspectiveVoteTwins.Valid(mode1));
            var twin = ProspectiveVoteFacade.Engine(mode2);
            EpisodeEngineTests.OpenTheVote(legacy);
            EpisodeEngineTests.OpenTheVote(twin);
            var open1 = legacy.Snapshot; var open2 = twin.Snapshot;
            string spare = open1.nominees.First(n => n != open1.deals.Single(d => d.id == id).targetId);
            var ballot = CommitmentsRead.Decision.Vote(spare);
            var warned = CommitmentsRead.ByTheRules(open1, ballot);
            Assert.That(warned.Select(b => b.id), Does.Contain(id), "Fixture: mode 1 warns the ballot breaks the deal.");
            Assert.That(Json(CommitmentsRead.ByTheRules(open2, ballot)), Is.EqualTo(Json(warned)), "Mode 2 warns of the canonical deal alike.");
            Assert.That(Json(CommitmentsRead.WouldBreak(open2, ballot)), Is.EqualTo(Json(CommitmentsRead.WouldBreak(open1, ballot))));
        }

        [Test]
        public void ABlocTheBoxDidNotDecideStillBindsAsTheWeekCloses()
        {
            var (reveal, hoh, id) = UndecidedBloc();
            Assert.That(reveal.Mode2.evictionResolved, Is.True);
            Assert.That(reveal.Mode1.deals.Single(d => d.id == id).status, Is.EqualTo(DealStatus.Active), "Fixture: mode 1's bloc binds on.");
            Assert.That(reveal.Mode2.unifiedCommitments.Single(row => row.id == id).status, Is.EqualTo(DealStatus.Active), "And mode 2's.");
            int Carried(EpisodeState s) => CommitmentReferences.Promises(s).Count(p => p.status == PromiseStatus.Active && (p.fromId == s.playerId || p.toId == s.playerId))
                + CommitmentReferences.Deals(s).Count(d => d.status == DealStatus.Active && (d.proposerId == s.playerId || d.recipientId == s.playerId));
            Assert.That(Carried(reveal.Mode2), Is.EqualTo(Carried(reveal.Mode1)));
            Assert.That(Carried(reveal.Mode2), Is.GreaterThan(0));
            Assert.That(Json(CommitmentsRead.With(reveal.Mode2, hoh)), Is.EqualTo(Json(CommitmentsRead.With(reveal.Mode1, hoh))));
        }

        private static string Json(object value) => ModeTwoReaderSweep.Json(value);
    }
}
