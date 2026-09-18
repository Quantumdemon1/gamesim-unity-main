using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Deals being kept and broken.
    ///
    /// <para>The point of this file is the first test in it. Three systems have been reading
    /// <see cref="DealStatus.Broken"/> since they were written — the NPC ladder docks fifteen, the
    /// player's table docks twelve, the eviction vote docks thirty-five — and until resolution
    /// existed every one of them read zero forever. A deal you could make and never fail is not a
    /// deal.</para>
    /// </summary>
    public sealed class DealResolutionTests
    {
        [Test]
        public void BreakingADealIsSomethingTheRestOfTheHouseCanFinallySee()
        {
            var state = Warm(Season(3u, 8), 50);
            var cast = Npcs(state);
            // The evaluator needs a block to read, and the person whose word is in question has to
            // be on it — what is under test is whether the break reaches the ballot at all.
            state.hohId = cast[3].id;
            state.nominees = new List<string> { cast[0].id, cast[2].id };
            var pact = Add(state, cast[0].id, cast[1].id, DealKind.SafetyAgreement, "d1");

            Assert.That(NpcDeals.BrokenDeals(state, cast[0].id), Is.Zero);
            double before = NpcDeals.Adjusted(state, cast[1].id, cast[0].id);
            double vote = Obligation(state, cast[1].id, cast[0].id);

            pact.status = DealStatus.Broken;

            Assert.That(NpcDeals.BrokenDeals(state, cast[0].id), Is.EqualTo(1));
            Assert.That(NpcDeals.Adjusted(state, cast[1].id, cast[0].id),
                Is.EqualTo(before - NpcDeals.BrokenDealPenalty).Within(0.001));
            Assert.That(Obligation(state, cast[1].id, cast[0].id), Is.LessThan(vote),
                "The eviction vote has scored a broken deal at −35 since it was written.");
        }

        // ---------------------------------------------------------------- the four rules

        [Test]
        public void ASafetyPactIsBrokenByNominatingTheOtherPartyAndNothingElse()
        {
            var deal = Deal("a", "b", DealKind.SafetyAgreement);
            Assert.That(DealResolution.Nomination(deal, "a", new[] { "b", "c" }), Is.EqualTo(DealStatus.Broken));
            Assert.That(DealResolution.Nomination(deal, "b", new[] { "a", "c" }), Is.EqualTo(DealStatus.Broken));
            Assert.That(DealResolution.Nomination(deal, "a", new[] { "c", "d" }), Is.Null,
                "A safety pact has no way to be KEPT by a nomination; it can only be broken.");
            Assert.That(DealResolution.Nomination(deal, "z", new[] { "a", "b" }), Is.Null,
                "Somebody else's ceremony is not their pact to break.");
        }

        /// <summary>
        /// The source marks this precedence explicitly, so it is pinned explicitly: hitting the
        /// agreed target fulfils the deal even where the same ceremony also puts the partner up.
        /// </summary>
        [Test]
        public void HittingTheAgreedTargetKeepsTheDealEvenWhenThePartnerGoesUpBesideThem()
        {
            var deal = Deal("a", "b", DealKind.TargetAgreement, "t");
            Assert.That(DealResolution.Nomination(deal, "a", new[] { "t", "c" }), Is.EqualTo(DealStatus.Fulfilled));
            Assert.That(DealResolution.Nomination(deal, "a", new[] { "t", "b" }), Is.EqualTo(DealStatus.Fulfilled),
                "The target went up. That the partner did too does not undo it.");
            Assert.That(DealResolution.Nomination(deal, "a", new[] { "b", "c" }), Is.EqualTo(DealStatus.Broken));
            Assert.That(DealResolution.Nomination(deal, "a", new[] { "c", "d" }), Is.Null);
        }

        [Test]
        public void ATargetAgreementNamingNobodyAnswersToNoNomination()
        {
            var deal = Deal("a", "b", DealKind.TargetAgreement);
            Assert.That(DealResolution.Nomination(deal, "a", new[] { "b", "c" }), Is.Null);
        }

        /// <summary>
        /// The trap the source warns about: saving the partner clears their nomination, so a rule
        /// that re-read the block would find them already safe and mark the deal unresolved.
        /// </summary>
        [Test]
        public void SavingThePartnerKeepsTheCommitmentEvenThoughItAlsoLiftsThemOffTheBlock()
        {
            var deal = Deal("a", "b", DealKind.VetoUse);
            Assert.That(DealResolution.Veto(deal, "a", "b", true, partnerIsNominated: false),
                Is.EqualTo(DealStatus.Fulfilled));
            Assert.That(DealResolution.Veto(deal, "a", "c", true, partnerIsNominated: true),
                Is.EqualTo(DealStatus.Broken), "Saving somebody else while the partner sits there is a break.");
            Assert.That(DealResolution.Veto(deal, "a", null, false, partnerIsNominated: true),
                Is.EqualTo(DealStatus.Broken), "Declining to use it at all is a break.");
            Assert.That(DealResolution.Veto(deal, "a", null, false, partnerIsNominated: false), Is.Null,
                "A partner who was never on the block needed no saving.");
            Assert.That(DealResolution.Veto(deal, "z", "b", true, true), Is.Null);
            Assert.That(DealResolution.Veto(Deal("a", "b", DealKind.Partnership), "a", "c", true, true), Is.Null);
        }

        [Test]
        public void AVotingBlockIsKeptByVotingTheSameWayAndBrokenByNot()
        {
            var deal = Deal("a", "b", DealKind.VoteTogether);
            Assert.That(DealResolution.VoteTogether(deal, Votes(("a", "x"), ("b", "x"))),
                Is.EqualTo(DealStatus.Fulfilled));
            Assert.That(DealResolution.VoteTogether(deal, Votes(("a", "x"), ("b", "y"))),
                Is.EqualTo(DealStatus.Broken));
            Assert.That(DealResolution.VoteTogether(deal, Votes(("a", "x"))), Is.Null,
                "One of them did not vote, so they agreed nothing either way.");
            Assert.That(DealResolution.VoteTogether(Deal("a", "b", DealKind.Partnership),
                Votes(("a", "x"), ("b", "y"))), Is.Null);
        }

        [Test]
        public void AFinalTwoIsSettledByWhoTheLastHeadOfHouseholdTakes()
        {
            var deal = Deal("a", "b", DealKind.FinalTwo);
            Assert.That(DealResolution.FinalSelection(deal, "a", "b"), Is.EqualTo(DealStatus.Fulfilled));
            Assert.That(DealResolution.FinalSelection(deal, "a", "c"), Is.EqualTo(DealStatus.Broken));
            Assert.That(DealResolution.FinalSelection(deal, "z", "b"), Is.Null);
        }

        /// <summary>A <c>vote_save</c> is weighed by the vote, never marked kept or broken by it.</summary>
        [Test]
        public void AVoteToSaveIsWeighedByTheBallotRatherThanSettledByIt()
        {
            var state = Season(3u, 8);
            var cast = Npcs(state);
            state.nominees = new List<string> { cast[1].id, cast[2].id };
            var deal = Add(state, cast[1].id, cast[0].id, DealKind.VoteSave, "d");
            deal.targetId = cast[1].id;
            state.votes.Add(new VoteState { voterId = cast[0].id, targetId = cast[1].id, reason = "against it" });

            Assert.That(DealResolution.Verdicts(state, DealResolution.Votes, null), Is.Empty,
                "The source resolves four actions and this is not one of them; the vote evaluator "
                + "scores the same promise at +35 instead, and marking it here would double-count it.");
            Assert.That(deal.status, Is.EqualTo(DealStatus.Active));
        }

        [Test]
        public void OnlyABindingDealIsEverSettled()
        {
            var state = Season(3u, 8);
            var cast = Npcs(state);
            foreach (string status in new[] { DealStatus.Proposed, DealStatus.Declined,
                         DealStatus.Expired, DealStatus.Broken, DealStatus.Fulfilled })
            {
                state.deals.Clear();
                var deal = Add(state, cast[0].id, cast[1].id, DealKind.SafetyAgreement, "d");
                deal.status = status;
                Assert.That(DealResolution.Verdicts(state, DealResolution.Nominates, cast[0].id,
                    new List<string> { cast[1].id }), Is.Empty, status);
            }
        }

        // ---------------------------------------------------------------- what settling writes

        [Test]
        public void BreakingYourWordCostsNearlyTwiceWhatKeepingItEarns()
        {
            var critical = Deal("a", "b", DealKind.VetoUse);
            var ordinary = Deal("a", "b", DealKind.Partnership);
            var slight = Deal("a", "b", DealKind.InformationSharing);

            Assert.That(DealResolution.Impact(critical, DealStatus.Broken), Is.EqualTo(-45).Within(0.001));
            Assert.That(DealResolution.Impact(critical, DealStatus.Fulfilled), Is.EqualTo(24).Within(0.001));
            Assert.That(DealResolution.Impact(ordinary, DealStatus.Broken), Is.EqualTo(-22.5).Within(0.001));
            Assert.That(DealResolution.Impact(slight, DealStatus.Broken), Is.EqualTo(-15).Within(0.001));
            Assert.That(DealResolution.Impact(slight, DealStatus.Broken),
                Is.EqualTo(DealResolution.Impact(critical, DealStatus.Broken) / 3).Within(0.001),
                "Walking away from the block costs three times what walking away from gossip does.");
        }

        // ---------------------------------------------------------------- through the engine

        [Test]
        public void NominatingSomebodyYouPromisedNotToBreaksThePactInTheSeason()
        {
            var engine = SeasonAtNominations(out var state, out var hoh, out var victim);
            var pact = Add(Live(engine), hoh, victim, DealKind.SafetyAgreement, "deal-pact");
            double before = Live(engine).Score(victim, hoh);

            Nominate(engine, victim);

            var after = engine.Snapshot;
            Assert.That(after.deals.Single(d => d.id == "deal-pact").status, Is.EqualTo(DealStatus.Broken));
            Assert.That(after.Score(victim, hoh), Is.LessThan(before),
                "The wronged party thinks less of whoever broke it — not the other way round.");
            Assert.That(after.events.Any(e => e.kind == "deal-outcome"), Is.True);
            Assert.That(after.relationships.SelectMany(r => r.events).Any(e => e.type == "deal_broken"), Is.True);
            Assert.That(EpisodeValidation.TryValidate(after, out string error), Is.True, error);
        }

        [Test]
        public void WordGetsAroundThatSomebodyBrokeTheirWord()
        {
            int spread = 0, trials = 0;
            for (uint seed = 1; seed <= 24; seed++)
            {
                var engine = SeasonAtNominations(out var state, out var hoh, out var victim, seed);
                Add(Live(engine), hoh, victim, DealKind.SafetyAgreement, "deal-pact");
                Nominate(engine, victim);
                var after = engine.Snapshot;
                if (after.deals.Single(d => d.id == "deal-pact").status != DealStatus.Broken) continue;
                trials++;
                if (after.relationships.SelectMany(r => r.events).Any(e => e.type == "heard_about_betrayal")) spread++;
            }
            Assert.That(trials, Is.GreaterThan(0));
            Assert.That(spread, Is.GreaterThan(0),
                "Two in five bystanders hear; across two dozen seasons some of them should have.");
        }

        [Test]
        public void SettlingADealSpendsOnlyTheSeasonsOwnGenerator()
        {
            var first = SeasonAtNominations(out _, out string hoh, out string victim, 9u);
            var second = SeasonAtNominations(out _, out _, out _, 9u);
            Add(Live(first), hoh, victim, DealKind.SafetyAgreement, "deal-pact");
            Add(Live(second), hoh, victim, DealKind.SafetyAgreement, "deal-pact");

            Nominate(first, victim);
            Nominate(second, victim);

            Assert.That(first.Snapshot.randomState, Is.EqualTo(second.Snapshot.randomState));
            Assert.That(first.Snapshot.Score(victim, hoh), Is.EqualTo(second.Snapshot.Score(victim, hoh)));
        }

        // ---------------------------------------------------------------- fixtures

        private static EpisodeState Season(uint seed, int size) =>
            SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size }, seed);

        private static List<ContestantState> Npcs(EpisodeState state) =>
            state.Active.Where(c => !c.isPlayer).ToList();

        private static EpisodeState Warm(EpisodeState state, double score)
        {
            foreach (var edge in state.relationships) edge.score = score;
            return state;
        }

        private static DealState Deal(string a, string b, string type, string about = null) => new DealState
        {
            id = "d", type = type, proposerId = a, recipientId = b, targetId = about,
            status = DealStatus.Active, week = 1, trustImpact = DealKind.DefaultTrust(type),
        };

        private static DealState Add(EpisodeState state, string a, string b, string type, string id)
        {
            var deal = new DealState
            {
                id = id, type = type, proposerId = a, recipientId = b, status = DealStatus.Active,
                week = state.week, trustImpact = DealKind.DefaultTrust(type),
            };
            state.deals.Add(deal);
            return deal;
        }

        private static List<VoteState> Votes(params (string voter, string target)[] rows) =>
            rows.Select(r => new VoteState { voterId = r.voter, targetId = r.target, reason = "" }).ToList();

        private static double Obligation(EpisodeState state, string voterId, string aboutId)
        {
            var scenario = WebEvictionVoting.FromNative(state, voterId);
            var deals = scenario.state.deals.Where(d => d.proposerId == voterId || d.recipientId == voterId);
            // The evaluator's own reading, reached the same way it reaches it.
            return deals.Sum(d => d.status == DealStatus.Broken
                                  && (d.proposerId == aboutId || d.recipientId == aboutId) ? -35 : 0);
        }

        /// <summary>
        /// A season parked at the nomination ceremony with the player holding the power.
        ///
        /// <para>Placed rather than played. Driving a real season here would hand the power to
        /// whoever won it, and these tests are about what a ceremony does to a deal — not about who
        /// happened to be Head of Household on seed five.</para>
        /// </summary>
        private static EpisodeEngine SeasonAtNominations(out EpisodeState state, out string hohId,
            out string victimId, uint seed = 5u)
        {
            state = Warm(Season(seed, 10), 40);
            state.phase = EpisodePhase.Nomination;
            state.hohId = state.playerId;
            state.competitionResolved = true;
            hohId = state.playerId;
            victimId = EpisodeEngine.NominationCandidates(state).First().id;
            return new EpisodeEngine(state);
        }

        private static void Nominate(EpisodeEngine engine, string victimId)
        {
            var live = engine.Snapshot;
            string second = EpisodeEngine.NominationCandidates(live).First(c => c.id != victimId).id;
            var applied = engine.Apply(new EpisodeCommand
            {
                id = "nominate-" + live.revision, actorId = live.playerId,
                kind = EpisodeCommandKind.Nominate, targetId = victimId, secondTargetId = second,
                expectedRevision = live.revision, expectedPhase = live.phase,
            });
            Assert.That(applied.accepted, Is.True, applied.reason);
        }

        private static EpisodeState Live(EpisodeEngine engine) => (EpisodeState)typeof(EpisodeEngine)
            .GetField("current", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .GetValue(engine);
    }
}
