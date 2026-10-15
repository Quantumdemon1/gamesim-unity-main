using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    public sealed class NpcInvitationTests
    {
        private static EpisodeState State(int mode = 0)
        {
            var state = ContentCatalog.Create(412);
            state.phase = EpisodePhase.Campaign; state.week = 3; state.dealRulesStartWeek = 1;
            state.unifiedCommitmentRulesVersion = mode;
            return state;
        }

        private static DealState Offer(EpisodeState state, string type = DealKind.InformationSharing, string id = "invitation-offer")
        {
            var row = new DealState { id = id, type = type, proposerId = state.Active.First(c => !c.isPlayer).id,
                recipientId = state.playerId, week = state.week, expiresWeek = state.week,
                status = DealStatus.Proposed, trustImpact = DealKind.DefaultTrust(type) };
            state.deals.Add(row); return row;
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void CanonicalSafetyOffersAppearInEverySupportedAuthorityMode(int mode)
        {
            var state = State(mode);
            string npc = state.Active.First(c => !c.isPlayer).id;
            if (mode == 0) Offer(state, DealKind.SafetyAgreement);
            else state.unifiedCommitments.Add(new UnifiedCommitmentState { id = "canonical-invitation",
                kind = UnifiedCommitments.Safety, sourcePolicy = UnifiedCommitments.DealPolicy,
                origin = UnifiedCommitments.NpcOffer, makerId = npc, beneficiaryId = state.playerId,
                reciprocal = true, createdWeek = state.week, expiresWeek = state.week,
                status = DealStatus.Proposed, trustImpact = DealTrust.High });
            string before = JsonConvert.SerializeObject(state);
            var invitation = NpcInvitation.Available(state).Single();
            Assert.That(invitation.NpcId, Is.EqualTo(npc));
            Assert.That(invitation.OpeningLine, Is.EqualTo("Can we talk about keeping each other off the block?"));
            Assert.That(invitation.IsCurrent(state), Is.True);
            Assert.That(JsonConvert.SerializeObject(state), Is.EqualTo(before));
        }

        [Test]
        public void ExistingUrgencyOrderAndActualOfferedTargetDetermineTheInvitation()
        {
            var state = State(); Offer(state);
            var urgent = Offer(state, DealKind.VoteSave, "urgent-offer");
            urgent.targetId = urgent.proposerId;
            var rows = NpcInvitation.Available(state);
            Assert.That(rows[0].RecordId, Is.EqualTo("urgent-offer"));
            Assert.That(rows[0].OpeningLine, Is.EqualTo("Can we talk about your vote to keep me?"));
            Assert.That(rows[1].RecordId, Is.EqualTo("invitation-offer"));
        }

        [Test]
        public void RenderingAndClockRevisionsDoNotChangeRandomStreamsOrSavedRecords()
        {
            var state = State(); Offer(state);
            string before = JsonConvert.SerializeObject(state);
            var invitation = NpcInvitation.Available(state).Single();
            for (int i = 0; i < 20; i++)
            {
                Assert.That(NpcInvitation.Available(state).Single().OpeningLine, Is.EqualTo(invitation.OpeningLine));
                Assert.That(invitation.IsCurrent(state), Is.True);
            }
            Assert.That(JsonConvert.SerializeObject(state), Is.EqualTo(before));
            state.revision++;
            Assert.That(invitation.IsCurrent(state), Is.True, "A clock revision does not answer the standing offer.");
        }

        [TestCase(DealStatus.Active)] [TestCase(DealStatus.Declined)] [TestCase(DealStatus.Expired)]
        public void AnsweredOrExpiredOffersNeverProduceInvitations(string status)
        {
            var state = State(); var row = Offer(state);
            var invitation = NpcInvitation.Available(state).Single(); row.status = status;
            Assert.That(NpcInvitation.Available(state), Is.Empty);
            Assert.That(invitation.IsCurrent(state), Is.False);
        }

        [TestCase("session")] [TestCase("phase")] [TestCase("week")] [TestCase("sender")]
        [TestCase("recipient")] [TestCase("target")] [TestCase("terms")] [TestCase("removed")]
        public void OldInvitationLosesAuthorityWhenItsContextOrExactTermsChange(string change)
        {
            var state = State(); var row = Offer(state);
            var invitation = NpcInvitation.Available(state).Single();
            switch (change)
            {
                case "session": state.sessionId += "-loaded"; break;
                case "phase": state.phase = EpisodePhase.Nomination; break;
                case "week": state.week++; break;
                case "sender": state.Find(row.proposerId).status = ContestantStatus.Evicted; break;
                case "recipient": row.recipientId = row.proposerId; break;
                case "target": row.targetId = state.playerId; break;
                case "terms": row.type = DealKind.FinalTwo; break;
                default: state.deals.Clear(); break;
            }
            Assert.That(invitation.IsCurrent(state), Is.False);
        }

        [TestCase(EpisodePhase.HoH)] [TestCase(EpisodePhase.Veto)] [TestCase(EpisodePhase.Eviction)]
        [TestCase(EpisodePhase.FinalEviction)] [TestCase(EpisodePhase.Finished)]
        public void CompetitionCeremonyAndFinishedPhasesNeverInvite(EpisodePhase phase)
        {
            var state = State(); Offer(state); state.phase = phase;
            Assert.That(NpcInvitation.Available(state), Is.Empty);
        }

        [Test]
        public void UnsupportedOffersAndNonPlayerRecipientsRemainOutsideTheView()
        {
            var state = State(); var row = Offer(state); row.type = "unknown";
            Assert.That(NpcInvitation.Available(state), Is.Empty);
            row.type = DealKind.SafetyAgreement; row.recipientId = row.proposerId;
            Assert.That(NpcInvitation.Available(state), Is.Empty);
            Assert.That(NpcInvitation.Available(null), Is.Empty);
        }

        [Test]
        public void DepartedNamedTargetsDoNotInviteOrRewriteTheSavedQuestion()
        {
            var state = State(); var row = Offer(state, DealKind.TargetAgreement);
            row.targetId = state.Active.First(person => !person.isPlayer && person.id != row.proposerId).id;
            var invitation = NpcInvitation.Available(state).Single();
            string original = JsonConvert.SerializeObject(state);
            foreach (var status in new[] { ContestantStatus.Evicted, ContestantStatus.Jury })
            {
                var changed = state.Clone(); changed.Find(row.targetId).status = status;
                string before = JsonConvert.SerializeObject(changed);
                string pendingBefore = JsonConvert.SerializeObject(NpcDeals.Pending(changed).Single());
                uint gameRandom = changed.randomState, npcRandom = changed.npcSocial.randomState;
                Assert.That(NpcInvitation.Available(changed), Is.Empty, status.ToString());
                Assert.That(invitation.IsCurrent(changed), Is.False, status.ToString());
                var pending = NpcDeals.Pending(changed).Single();
                Assert.That(pending.id, Is.EqualTo(row.id));
                Assert.That(pending.status, Is.EqualTo(DealStatus.Proposed), "The historical question stays on the record.");
                Assert.That(JsonConvert.SerializeObject(pending), Is.EqualTo(pendingBefore));
                Assert.That(changed.randomState, Is.EqualTo(gameRandom));
                Assert.That(changed.npcSocial.randomState, Is.EqualTo(npcRandom));
                Assert.That(JsonConvert.SerializeObject(changed), Is.EqualTo(before), "The reader changes no saved fields.");
            }
            Assert.That(JsonConvert.SerializeObject(state), Is.EqualTo(original));
        }

        [Test]
        public void PrivateScoresMotivesAndBallotsDoNotChangeTheSpokenOffer()
        {
            var state = State(); var row = Offer(state, DealKind.VoteEvict);
            row.targetId = state.Active.Last(c => !c.isPlayer).id;
            string words = NpcInvitation.Available(state).Single().OpeningLine;
            foreach (var edge in state.relationships) edge.score = -97;
            state.Find(row.proposerId).motive = "PRIVATE SECRET PLAN";
            Assert.That(NpcInvitation.Available(state).Single().OpeningLine, Is.EqualTo(words));
            Assert.That(words, Does.Contain(state.Find(row.targetId).name));
            Assert.That(words, Does.Not.Contain("PRIVATE"));
        }
    }
}
