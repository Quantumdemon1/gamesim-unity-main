using System;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>Actual composite nomination, reputation, jury and finalist readers; rule 1 is not activated.</summary>
    public sealed class UnifiedSafetyReaderTests
    {
        [TestCase(false, true, null, true, 30d)]
        [TestCase(false, true, Negotiation.Remind, true, 35d)]
        [TestCase(false, true, Negotiation.Demand, true, 52.5d)]
        [TestCase(false, true, Negotiation.Threaten, true, 70d)]
        [TestCase(true, false, null, true, 35d)]
        [TestCase(true, true, null, true, 35d)]
        [TestCase(true, true, Negotiation.Demand, true, 52.5d)]
        [TestCase(true, true, Negotiation.Threaten, true, 70d)]
        [TestCase(true, true, null, false, 30d)]
        [TestCase(false, true, Negotiation.Threaten, false, 30d)]
        public void ActualNominationUsesTheMaximumOfStoryAndStrategy(bool deal, bool promise, string approach, bool strategy, double expected)
        {
            var s = State(); s.strategyRulesStartWeek = strategy ? 1 : 0;
            if (deal) s.unifiedCommitments.Add(Deal(s, "deal"));
            if (promise) s.unifiedCommitments.Add(Promise(s, "promise"));
            if (approach != null) Hold(s, approach);
            string before = Json(s);
            Assert.That(EpisodeEngine.NominationWeight(s, s.hohId, s.playerId), Is.EqualTo(12 + expected));
            Assert.That(StoryConsumers.NominationPreference(s, s.hohId, s.playerId), Is.EqualTo(42));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(false, 0, 0d)] [TestCase(false, 4, 0d)] [TestCase(false, 1, 30d)]
        [TestCase(true, 0, 35d)] [TestCase(true, 4, 35d)] [TestCase(true, 1, 35d)]
        public void StoryOffOrNotStartedDoesNotInventItsWordBonus(bool deal, int starts, double expected)
        {
            var s = State(); s.story.rulesStartWeek = starts;
            s.unifiedCommitments.Add(deal ? Deal(s, "deal") : Promise(s, "promise"));
            Assert.That(EpisodeEngine.NominationWeight(s, s.hohId, s.playerId), Is.EqualTo(12 + expected));
        }

        [TestCase(false, DealStatus.Proposed)] [TestCase(false, DealStatus.Declined)]
        [TestCase(false, DealStatus.Expired)] [TestCase(false, DealStatus.Fulfilled)]
        [TestCase(false, DealStatus.Broken)] [TestCase(true, DealStatus.Expired)]
        [TestCase(true, DealStatus.Broken)]
        public void OnlyActiveSafetyProtectsAndNoReaderChangesItsStatus(bool promise, string status)
        {
            var s = State(); var row = promise ? Promise(s, "promise") : Deal(s, "deal");
            s.unifiedCommitments.Add(row);
            if (status == DealStatus.Broken) Break(s, "nomination", s.playerId);
            else { row.status = status; if (status == DealStatus.Fulfilled) row.settledWeek = s.week; }
            string before = Json(s);
            Assert.That(StoryConsumers.TheirWord(s, s.hohId, s.playerId), Is.False);
            Assert.That(EpisodeEngine.NominationWeight(s, s.hohId, s.playerId), Is.EqualTo(12));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(false, 30d)] [TestCase(true, 0d)]
        public void AUnilateralPromiseDoesNotManufactureReverseConsent(bool reverse, double expected)
        {
            var s = State(); var row = Promise(s, "promise");
            if (reverse) { row.makerId = s.playerId; row.beneficiaryId = s.hohId; }
            s.unifiedCommitments.Add(row);
            Assert.That(StoryConsumers.PromisedSafety(s, s.hohId, s.playerId), Is.EqualTo(!reverse));
            Assert.That(EpisodeEngine.NominationWeight(s, s.hohId, s.playerId), Is.EqualTo(12 + expected));
        }

        [TestCase(true, true, 77d)] [TestCase(true, false, 47d)]
        [TestCase(false, true, 42d)] [TestCase(false, false, 12d)]
        public void LegacyNominationRetainsItsAdditivePolicy(bool deal, bool promise, double expected)
        {
            var s = State(); s.unifiedCommitmentRulesVersion = 0;
            if (deal) s.deals.Add(new DealState { id = "legacy-deal", type = DealKind.SafetyAgreement,
                proposerId = s.hohId, recipientId = s.playerId, status = DealStatus.Active });
            if (promise) s.promises.Add(new PromiseState { id = "legacy-promise", kind = PromiseKind.Safety,
                fromId = s.hohId, toId = s.playerId, status = PromiseStatus.Active });
            string before = Json(s);
            Assert.That(EpisodeEngine.NominationWeight(s, s.hohId, s.playerId), Is.EqualTo(expected));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(1)] [TestCase(2)] [TestCase(8)]
        public void OverlappingEvidenceIsOneReputationJuryAndFinalistIncident(int copies)
        {
            var s = State(); double reputation = ThreatAssessment.Assess(s, Other(s), s.hohId).Reputation;
            s.unifiedCommitments.Add(Promise(s, "promise"));
            for (int i = 0; i < copies; i++) s.unifiedCommitments.Add(Deal(s, "deal-" + i));
            Break(s, "nomination", s.playerId); string before = Json(s);
            var incident = UnifiedCommitmentHistory.Breaches(s).Single();
            Assert.That(incident.EvidenceIds.Count, Is.EqualTo(copies + 1));
            Assert.That(incident.EffectOwnerId, Is.EqualTo("promise"), "The source promise has the strongest relationship consequence.");
            Assert.That(ThreatAssessment.Assess(s, Other(s), s.hohId).Reputation, Is.EqualTo(reputation + 3));
            Assert.That(WebJuryVoting.Obligations(s, s.playerId, s.hohId), Is.EqualTo(-20));
            Assert.That(FinalistRead.TowardYou(s, s.hohId).value, Is.EqualTo("Broke a safety commitment to you"));
            s.unifiedCommitments.Reverse();
            Assert.That(UnifiedCommitmentHistory.Breaches(s).Single().EffectOwnerId, Is.EqualTo("promise"));
            s.unifiedCommitments.Reverse(); Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(false)] [TestCase(true)]
        public void SeparateDecisionOrWeekIsNotCollapsedIntoTheFirstIncident(bool nextWeek)
        {
            var s = State(); s.unifiedCommitments.Add(Promise(s, "promise-a")); Break(s, "nomination", s.playerId);
            if (nextWeek) s.week++;
            s.unifiedCommitments.Add(Promise(s, "promise-b")); Break(s, nextWeek ? "nomination" : "replacement", s.playerId);
            Assert.That(UnifiedCommitmentHistory.Breaches(s).Count, Is.EqualTo(2));
            Assert.That(FinalistRead.TowardYou(s, s.hohId).value, Is.EqualTo("Broke 2 safety commitments to you"));
            Assert.That(WebJuryVoting.Obligations(s, s.playerId, s.hohId), Is.EqualTo(-40));
        }

        [TestCase("arbitrary")] [TestCase("actor")] [TestCase("wronged")]
        [TestCase("week")] [TestCase("zero-padding")] [TestCase("trailing")]
        public void HistoricalReadersRefuseConflatedOrMalformedIncidentIdentity(string corruption)
        {
            var s = State(); s.unifiedCommitments.Add(Promise(s, "promise"));
            s.unifiedCommitments.Add(Deal(s, "deal")); Break(s, "nomination", s.playerId);
            var row = s.unifiedCommitments.Single(x => x.id == "deal");
            switch (corruption)
            {
                case "arbitrary": row.settlementEffectKey = "safety:arbitrary"; break;
                case "actor": row.brokenById = s.playerId; break;
                case "wronged": row.beneficiaryId = Other(s); break;
                case "week": row.settledWeek--; break;
                case "zero-padding": row.settlementEffectKey = row.settlementEffectKey.Replace("safety:3:", "safety:03:"); break;
                default: row.settlementEffectKey += "x"; break;
            }
            string before = Json(s);
            Assert.Throws<ArgumentException>(() => UnifiedCommitmentHistory.Breaches(s));
            Assert.Throws<ArgumentException>(() => ThreatAssessment.Assess(s, Other(s), s.hohId));
            Assert.Throws<ArgumentException>(() => WebJuryVoting.Obligations(s, s.playerId, s.hohId));
            Assert.Throws<ArgumentException>(() => FinalistRead.TowardYou(s, s.hohId));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void ReturnedEvidenceAndRecordsCannotBecomeASecondWriter()
        {
            var s = State(); s.unifiedCommitments.Add(Promise(s, "promise")); Break(s, "nomination", s.playerId);
            string before = Json(s); var rows = UnifiedCommitmentHistory.Records(s); rows[0].status = DealStatus.Active;
            Assert.Throws<NotSupportedException>(() => ((System.Collections.Generic.IList<UnifiedCommitmentState>)rows).Clear());
            var incident = UnifiedCommitmentHistory.Breaches(s).Single();
            Assert.Throws<NotSupportedException>(() => ((System.Collections.Generic.IList<string>)incident.EvidenceIds).Clear());
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(false, DealTrust.Low, -25d)] [TestCase(false, DealTrust.Medium, -37.5d)]
        [TestCase(true, DealTrust.Low, -37.5d)] [TestCase(true, DealTrust.Medium, -50d)]
        public void IsolatedDealKeepsItsSourceJuryWeightIncludingAcceptedOffers(bool accepted, string trust, double expected)
        {
            var s = State(); var row = Deal(s, "deal"); row.trustImpact = trust;
            if (accepted) row.origin = UnifiedCommitments.NpcOffer;
            s.unifiedCommitments.Add(row); Break(s, "nomination", s.playerId);
            Assert.That(WebJuryVoting.Obligations(s, s.playerId, s.hohId), Is.EqualTo(expected));
        }

        [TestCase(1)] [TestCase(2)] [TestCase(8)]
        public void KeptOverlappingPactsGiveOneStrongestPositiveJuryReward(int copies)
        {
            var s = State();
            for (int i = 0; i < copies; i++)
            {
                var row = Deal(s, "deal-" + i); row.status = DealStatus.Fulfilled; row.settledWeek = s.week;
                s.unifiedCommitments.Add(row);
            }
            Assert.That(WebJuryVoting.Obligations(s, s.playerId, s.hohId), Is.EqualTo(30));
            Assert.That(UnifiedCommitmentHistory.Breaches(s), Is.Empty);
        }

        [TestCase(false)] [TestCase(true)]
        public void TheWrongedPersonAndAThirdPartyDoNotReceiveTheBreachPenalty(bool thirdParty)
        {
            var s = State(); s.unifiedCommitments.Add(Promise(s, "promise")); Break(s, "nomination", s.playerId);
            string target = thirdParty ? Other(s) : s.playerId;
            double before = ThreatAssessment.Assess(s, s.hohId, target).Reputation;
            Assert.That(WebJuryVoting.Obligations(s, s.hohId, target), Is.Zero);
            Assert.That(FinalistRead.TowardYou(s, target).value, Is.EqualTo("Nothing on the record"));
            s.unifiedCommitments.Clear();
            Assert.That(ThreatAssessment.Assess(s, s.hohId, target).Reputation, Is.EqualTo(before));
        }

        [Test]
        public void CanonicalReadPathsRejectACompetingLegacySafetyAuthority()
        {
            var s = State(); s.unifiedCommitments.Add(Deal(s, "canonical"));
            s.deals.Add(new DealState { id = "mirror", type = DealKind.SafetyAgreement, status = DealStatus.Active });
            string before = Json(s);
            Assert.Throws<ArgumentException>(() => StoryConsumers.TheirWord(s, s.hohId, s.playerId));
            Assert.Throws<ArgumentException>(() => UnifiedCommitmentHistory.Records(s));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(false)] [TestCase(true)]
        public void CurrentDecisionDoesNotAmplifyItsOwnGrudgeButPriorIncidentsStillCount(bool twoNominees)
        {
            var s = State(); s.hohId = s.playerId;
            string first = s.contestants.First(c => c.id != s.playerId).id;
            string second = s.contestants.Where(c => c.id != s.playerId).Skip(1).First().id;
            s.week = 2; var prior = Promise(s, "prior"); prior.makerId = s.playerId; prior.beneficiaryId = first;
            s.unifiedCommitments.Add(prior); Break(s, "nomination", first); s.week = 3;
            var row = Promise(s, "current-a"); row.makerId = s.playerId; row.beneficiaryId = first;
            s.unifiedCommitments.Add(row);
            if (twoNominees)
            {
                var other = Promise(s, "current-b"); other.makerId = s.playerId; other.beneficiaryId = second;
                s.unifiedCommitments.Add(other);
            }
            var expected = s.Clone();
            foreach (string id in new[] { "current-a", twoNominees ? "current-b" : null }.Where(id => id != null))
                Invoke("SettlePromise", expected, CommitmentReferences.FindPromise(expected, id), PromiseStatus.Broken);
            Invoke("ResolveUnifiedSafetyNomination", s, "nomination", s.playerId,
                twoNominees ? new[] { first, second } : new[] { first });
            var actualImage = JObject.FromObject(s); actualImage.Remove("unifiedCommitments");
            var expectedImage = JObject.FromObject(expected); expectedImage.Remove("unifiedCommitments");
            Assert.That(actualImage.ToString(Formatting.None), Is.EqualTo(expectedImage.ToString(Formatting.None)));
            Assert.That(UnifiedCommitmentHistory.Breaches(s).Count, Is.EqualTo(twoNominees ? 3 : 2));
            Assert.That(s.story.grudges, Is.Not.Empty);
        }

        private static void Invoke(string name, params object[] arguments)
        {
            var method = typeof(EpisodeEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, name);
            try { method.Invoke(null, arguments); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }

        private static EpisodeState State()
        {
            var s = ContentCatalog.Create(17); s.week = 3; s.phase = EpisodePhase.Nomination;
            s.hohId = s.contestants.First(c => c.id != s.playerId).id;
            s.strategyRulesStartWeek = 1; s.commitmentRulesStartWeek = 1; s.unifiedCommitmentRulesVersion = 1;
            s.agencyRulesStartWeek = 0; s.story.rulesStartWeek = 1; s.story.rulesVersion = StoryRules.Grudges;
            s.relationships.Single(r => r.fromId == s.hohId && r.toId == s.playerId).score = 12;
            return s;
        }
        private static UnifiedCommitmentState Promise(EpisodeState s, string id) => new UnifiedCommitmentState {
            id = id, kind = UnifiedCommitments.Safety, sourcePolicy = UnifiedCommitments.PromisePolicy,
            origin = UnifiedCommitments.StoryPromise, makerId = s.hohId, beneficiaryId = s.playerId,
            createdWeek = s.week, expiresWeek = s.week + 1, status = DealStatus.Active, trustImpact = DealTrust.Medium,
        };
        private static UnifiedCommitmentState Deal(EpisodeState s, string id) => new UnifiedCommitmentState {
            id = id, kind = UnifiedCommitments.Safety, sourcePolicy = UnifiedCommitments.DealPolicy,
            origin = UnifiedCommitments.StoryDeal, makerId = s.hohId, beneficiaryId = s.playerId, reciprocal = true,
            createdWeek = s.week, expiresWeek = s.week, status = DealStatus.Active, trustImpact = DealTrust.Medium,
        };
        private static void Break(EpisodeState s, string decision, string nominee)
        {
            var evaluation = UnifiedCommitments.EvaluateNomination(s, decision, s.hohId, new[] { nominee });
            foreach (var change in evaluation.Changes)
            {
                int index = s.unifiedCommitments.FindIndex(row => row.id == change.Record.id);
                s.unifiedCommitments[index] = change.Record.Clone();
            }
        }
        private static string Other(EpisodeState s) => s.contestants.First(c => c.id != s.playerId && c.id != s.hohId).id;
        private static void Hold(EpisodeState s, string approach) => RelationshipLedger.RecordOneWay(s, s.hohId, s.playerId,
            Negotiation.HeldType(PromiseKind.Safety, approach), 0, "Called in.");
        private static string Json(EpisodeState s) => JsonConvert.SerializeObject(s.GetType()
            .GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(field => field.Name, StringComparer.Ordinal)
            .ToDictionary(field => field.Name, field => field.GetValue(s), StringComparer.Ordinal));
    }
}
