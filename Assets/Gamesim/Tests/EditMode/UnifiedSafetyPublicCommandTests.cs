using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Actual public enabled-Safety transactions. Versions are selected only on an asserted
    /// raw-Safety-empty fresh factory; every later role, nomination, status, settlement, fact,
    /// command receipt and revision comes from EpisodeEngine.Apply. No private Execute or
    /// writable legacy mirror is used. These synchronous pure tests establish neither native
    /// SaveStore round trips nor director/Unity/desktop acceptance.
    /// </summary>
    public sealed class UnifiedSafetyPublicCommandTests
    {
        private const uint FirstSeed = 2505, EndSeed = 2537;

        [TestCase(false, false)] [TestCase(false, true)]
        [TestCase(true, false)] [TestCase(true, true)]
        public void FreshFactoryModesEnterThePublicEngineWithoutMutatingTheirInputs(bool builtRoster, bool hearing)
        {
            var s = Fresh(FirstSeed, hearing, builtRoster);
            string before = Json(s);
            var engine = new EpisodeEngine(s);
            Assert.That(Json(s), Is.EqualTo(before));
            Assert.That(Json(engine.Snapshot), Is.EqualTo(before));
            Assert.That(s.contestants, Has.Count.EqualTo(builtRoster ? 8 : 6));
            Assert.That(YourWord.On(s), Is.True);
            Assert.That(s.unifiedHearingRulesVersion, Is.EqualTo(hearing ? 1 : 0));
            Assert.That(s.unifiedHearingEvidence, Is.Empty);
            Assert.That(s.unifiedHearingReceipts, Is.Empty);
            s.contestants.Clear();
            s.acceptedCommandIds.Add("not-committed");
            s.unifiedCommitments.Add(null);
            Assert.That(Json(engine.Snapshot), Is.EqualTo(before), "The constructor owns a detached valid snapshot.");
        }

        [TestCase(false)] [TestCase(true)]
        public void PublicCommitReturnsOneRevisionAndDetachedCanonicalReceipt(bool hearing)
        {
            var engine = new EpisodeEngine(Fresh(FirstSeed, hearing));
            var before = engine.Snapshot;
            var command = Command(before, EpisodeCommandKind.PromiseSafety);
            command.targetId = Other(before);
            var result = Commit(engine, command);
            var committed = engine.Snapshot;
            var row = committed.unifiedCommitments.Single(r => r.origin == UnifiedCommitments.PlayerPromise);
            Assert.That(row.id, Is.EqualTo("promise-" + before.nextSequence));
            Assert.That(row.makerId, Is.EqualTo(before.playerId));
            Assert.That(row.beneficiaryId, Is.EqualTo(command.targetId));
            Assert.That(row.status, Is.EqualTo(DealStatus.Active));
            Assert.That(committed.windowActions[Windows.AfterEviction], Is.EqualTo(before.windowActions[Windows.AfterEviction] + 1));
            string authority = Json(committed);
            result.state.unifiedCommitments[0].beneficiaryId = "tampered";
            result.state.contestants.Clear();
            result.state.acceptedCommandIds.Clear();
            committed.unifiedCommitments.Clear();
            committed.relationships.Clear();
            command.targetId = "tampered-command";
            Assert.That(Json(engine.Snapshot), Is.EqualTo(authority), "Neither returned state, snapshot nor command remains writable authority.");
            Assert.That(Json(before), Is.Not.EqualTo(authority));
        }

        [TestCase(false)] [TestCase(true)]
        public void ACommittedCommandIsIdempotentEvenWithAnObsoleteRevisionOrChangedPayload(bool hearing)
        {
            var engine = new EpisodeEngine(Fresh(FirstSeed, hearing));
            var command = Command(engine.Snapshot, EpisodeCommandKind.PromiseSafety);
            command.targetId = Other(engine.Snapshot);
            Commit(engine, command);
            string before = Json(engine.Snapshot);
            command.actorId = "another-actor";
            command.kind = EpisodeCommandKind.Nominate;
            command.targetId = "another-target";
            var duplicate = engine.Apply(command);
            Assert.That(duplicate.accepted, Is.False);
            Assert.That(duplicate.duplicate, Is.True);
            Assert.That(Json(duplicate.state), Is.EqualTo(before));
            Assert.That(Json(engine.Snapshot), Is.EqualTo(before));
            duplicate.state.unifiedCommitments.Clear();
            Assert.That(Json(engine.Snapshot), Is.EqualTo(before));
        }

        [TestCase(false, "null")] [TestCase(true, "null")]
        [TestCase(false, "missing-id")] [TestCase(true, "missing-id")]
        [TestCase(false, "long-id")] [TestCase(true, "long-id")]
        [TestCase(false, "stale-revision")] [TestCase(true, "stale-revision")]
        [TestCase(false, "stale-phase")] [TestCase(true, "stale-phase")]
        [TestCase(false, "actor")] [TestCase(true, "actor")]
        [TestCase(false, "kind")] [TestCase(true, "kind")]
        [TestCase(false, "nan")] [TestCase(true, "nan")]
        [TestCase(false, "infinity")] [TestCase(true, "infinity")]
        [TestCase(false, "negative-performance")] [TestCase(true, "negative-performance")]
        [TestCase(false, "high-performance")] [TestCase(true, "high-performance")]
        [TestCase(false, "long-text")] [TestCase(true, "long-text")]
        [TestCase(false, "control-text")] [TestCase(true, "control-text")]
        [TestCase(false, "unknown-target")] [TestCase(true, "unknown-target")]
        [TestCase(false, "self-target")] [TestCase(true, "self-target")]
        [TestCase(false, "active-obligation")] [TestCase(true, "active-obligation")]
        [TestCase(false, "wrong-decision")] [TestCase(true, "wrong-decision")]
        public void PublicRefusalPreservesAllStateIncludingRngHistoryBudgetsAndCanonicalRows(bool hearing, string defect)
        {
            var engine = new EpisodeEngine(Fresh(FirstSeed, hearing));
            var first = Command(engine.Snapshot, EpisodeCommandKind.PromiseSafety);
            first.targetId = Other(engine.Snapshot);
            Commit(engine, first);
            var s = engine.Snapshot;
            var command = Command(s, EpisodeCommandKind.PromiseSafety);
            command.targetId = s.Active.First(c => !c.isPlayer && c.id != first.targetId).id;
            switch (defect)
            {
                case "null": command = null; break;
                case "missing-id": command.id = " "; break;
                case "long-id": command.id = new string('x', 161); break;
                case "stale-revision": command.expectedRevision--; break;
                case "stale-phase": command.expectedPhase = EpisodePhase.HoH; break;
                case "actor": command.actorId = first.targetId; break;
                case "kind": command.kind = (EpisodeCommandKind)int.MaxValue; break;
                case "nan": command.performance = double.NaN; break;
                case "infinity": command.performance = double.PositiveInfinity; break;
                case "negative-performance": command.performance = -0.01; break;
                case "high-performance": command.performance = 1.01; break;
                case "long-text": command.text = new string('x', 2001); break;
                case "control-text": command.text = "unsupported\u0001control"; break;
                case "unknown-target": command.targetId = "not-in-this-cast"; break;
                case "self-target": command.targetId = s.playerId; break;
                case "active-obligation": command.targetId = first.targetId; break;
                case "wrong-decision": command.kind = EpisodeCommandKind.Nominate; break;
                default: Assert.Fail("Unknown control."); break;
            }
            string before = Json(s), input = JsonConvert.SerializeObject(command);
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.False, defect);
            Assert.That(result.duplicate, Is.False, defect);
            Assert.That(result.reason, Is.Not.Empty);
            Assert.That(Json(result.state), Is.EqualTo(before));
            Assert.That(Json(engine.Snapshot), Is.EqualTo(before));
            Assert.That(JsonConvert.SerializeObject(command), Is.EqualTo(input));
            result.state.unifiedCommitments.Clear();
            Assert.That(Json(engine.Snapshot), Is.EqualTo(before));
            // A refusal does not poison the current command channel or spend its last move-in seat.
            var valid = Command(engine.Snapshot, EpisodeCommandKind.PromiseSafety);
            valid.targetId = s.Active.First(c => !c.isPlayer && c.id != first.targetId).id;
            Commit(engine, valid);
        }

        [TestCase(true, false)] [TestCase(true, true)]
        [TestCase(false, false)] [TestCase(false, true)]
        public void PublicSafetyCreationKeepsItsActualSourceFamilyAndNoRawMirror(bool promise, bool hearing)
        {
            var owned = OwnedHoH(promise, hearing);
            var s = owned.Engine.Snapshot;
            var row = Row(owned);
            Assert.That(row.id, Is.EqualTo((promise ? "promise-" : "deal-player-") + owned.BeforeCreation.nextSequence));
            Assert.That(row.sourcePolicy, Is.EqualTo(promise ? UnifiedCommitments.PromisePolicy : UnifiedCommitments.DealPolicy));
            Assert.That(row.origin, Is.EqualTo(promise ? UnifiedCommitments.PlayerPromise : UnifiedCommitments.PlayerDeal));
            Assert.That(row.reciprocal, Is.EqualTo(!promise));
            Assert.That(row.makerId, Is.EqualTo(s.playerId));
            Assert.That(row.beneficiaryId, Is.EqualTo(owned.Other));
            Assert.That(row.createdWeek, Is.EqualTo(owned.BeforeCreation.week));
            Assert.That(row.expiresWeek, Is.EqualTo(row.createdWeek + (promise ? 1 : 0)));
            Assert.That(DealStatus.Binds(row.status), Is.True);
            Assert.That(s.windowActions[Windows.AfterHoH], Is.EqualTo(owned.BeforeCreation.windowActions[Windows.AfterHoH] + 1));
            Assert.That(s.acceptedCommandIds.Last(), Is.EqualTo(owned.CreationCommand));
            if (!promise)
                Assert.That(s.ledger.opportunities.Any(o => o.id == row.id && o.kind == OpportunityKinds.Deal
                    && o.response == OpportunityResponse.Taken), Is.True);
            AcceptedUnchanged(s);
        }

        [TestCase(true, false)] [TestCase(true, true)]
        [TestCase(false, false)] [TestCase(false, true)]
        public void ARealPublicNominationSettlesSafetyAndRetainsPromiseZeroDealOneFactPolicy(bool promise, bool hearing)
        {
            var owned = OwnedHoH(promise, hearing);
            var before = owned.Engine.Snapshot;
            var nominate = Command(before, EpisodeCommandKind.Nominate);
            nominate.targetId = owned.Other;
            nominate.secondTargetId = EpisodeEngine.NominationCandidates(before).First(c => c.id != owned.Other).id;
            Commit(owned.Engine, nominate);
            var s = owned.Engine.Snapshot;
            var row = Row(owned);
            Assert.That(row.status, Is.EqualTo(DealStatus.Broken));
            Assert.That(row.brokenById, Is.EqualTo(s.playerId));
            Assert.That(row.settledWeek, Is.EqualTo(s.week));
            Assert.That(row.settlementEffectKey, Does.Contain("nomination"));
            Assert.That(s.hohId, Is.EqualTo(s.playerId));
            Assert.That(s.nominees, Has.Member(owned.Other));
            Assert.That(s.Find(owned.Other).nominationWeeks, Has.Member(s.week));
            Assert.That(s.competitionScores.Any(score => score.contestantId == s.playerId), Is.True);
            // The permanent PowerRow is written by the actual veto/reveal owners, not nomination.
            AssertEmission(owned, promise, hearing);
            int settled = s.week;
            s = Reach(owned.Engine, x => x.week > settled, 160);
            Assert.That(Row(owned).settledWeek, Is.EqualTo(settled));
            Assert.That(s.ledger.power.Single(p => p.week == settled).hohId, Is.EqualTo(s.playerId));
            AcceptedUnchanged(s);
        }

        [TestCase(true, false)] [TestCase(true, true)]
        [TestCase(false, false)] [TestCase(false, true)]
        public void ARealPublicPlayerVetoReplacementBreaksTheCorrectDuty(bool promise, bool hearing)
        {
            Owned played = null;
            for (uint seed = FirstSeed; seed < EndSeed && played == null; seed++)
            {
                var owned = OwnedHoH(promise, hearing, seed);
                if (owned == null) continue;
                NominateOthers(owned);
                var s = Reach(owned.Engine, x => x.phase == EpisodePhase.VetoMeeting, 16);
                if (s.vetoHolderId != s.playerId || EpisodeEngine.VetoIsLockedAtFinalFour(s)
                    || !EpisodeEngine.ReplacementCandidates(s).Any(c => c.id == owned.Other)) continue;
                var veto = Command(s, EpisodeCommandKind.ResolveVeto);
                veto.useVeto = true;
                veto.targetId = s.nominees[0];
                veto.secondTargetId = owned.Other;
                Commit(owned.Engine, veto);
                played = owned;
            }
            Assert.That(played, Is.Not.Null, "The bounded seed controls must actually win an available player veto, not assign one.");
            var after = played.Engine.Snapshot;
            var row = Row(played);
            var power = after.ledger.power.Single(p => p.week == row.settledWeek);
            Assert.That(power.hohId, Is.EqualTo(after.playerId));
            Assert.That(power.vetoHolderId, Is.EqualTo(after.playerId));
            Assert.That(power.vetoUsed, Is.True);
            Assert.That(power.replacementId, Is.EqualTo(played.Other));
            Assert.That(after.vetoResolved, Is.True);
            Assert.That(after.nominees, Has.Member(played.Other));
            Assert.That(row.status, Is.EqualTo(DealStatus.Broken));
            Assert.That(row.settlementEffectKey, Does.Contain("replacement"));
            AssertEmission(played, promise, hearing);
        }

        [TestCase(true, false)] [TestCase(true, true)]
        [TestCase(false, false)] [TestCase(false, true)]
        public void TheCompletedPublicVetoSparesADealWithoutInventingPromiseFulfillment(bool promise, bool hearing)
        {
            var owned = OwnedHoH(promise, hearing);
            NominateOthers(owned);
            var s = Reach(owned.Engine, x => x.phase == EpisodePhase.VetoMeeting, 16);
            Commit(owned.Engine, Next(s, owned.Other));
            s = owned.Engine.Snapshot;
            var row = Row(owned);
            Assert.That(s.vetoResolved, Is.True);
            Assert.That(s.nominees, Has.No.Member(owned.Other));
            Assert.That(s.Find(owned.Other).nominationWeeks, Has.No.Member(s.week));
            Assert.That(row.status, Is.EqualTo(promise ? DealStatus.Active : DealStatus.Fulfilled));
            Assert.That(row.settledWeek, Is.EqualTo(promise ? 0 : s.week));
            Assert.That(row.brokenById, Is.Null);
            Assert.That(row.settlementEffectKey, Is.Null);
            Assert.That(s.story.facts.Where(f => f.kind == FactKinds.BrokenWord && f.refId == row.id), Is.Empty);
            Assert.That(s.unifiedHearingEvidence.Where(e => e.fact.refId == row.id), Is.Empty);
            AcceptedUnchanged(s);
        }

        [TestCase(false)] [TestCase(true)]
        public void ActualPublicWeekTurnsExpireAnUnbrokenPromiseWithoutFabricatingASettlement(bool hearing)
        {
            Owned expired = null;
            for (uint seed = FirstSeed; seed < EndSeed && expired == null; seed++)
            {
                var owned = OwnedHoH(true, hearing, seed);
                if (owned == null) continue;
                int term = Row(owned).expiresWeek;
                Reach(owned.Engine, x => x.week > term || x.phase == EpisodePhase.Finished, 160, owned.Other);
                if (Row(owned).status == DealStatus.Expired && owned.Engine.Snapshot.week > term) expired = owned;
            }
            Assert.That(expired, Is.Not.Null, "A real protected promise must reach its source week-turn expiry within the bounded played controls.");
            var s = expired.Engine.Snapshot;
            var row = Row(expired);
            Assert.That(row.sourcePolicy, Is.EqualTo(UnifiedCommitments.PromisePolicy));
            Assert.That(row.settledWeek, Is.Zero);
            Assert.That(row.brokenById, Is.Null);
            Assert.That(row.settlementEffectKey, Is.Null);
            Assert.That(s.story.facts.Where(f => f.kind == FactKinds.BrokenWord && f.refId == row.id), Is.Empty);
            Assert.That(s.unifiedHearingEvidence.Where(e => e.fact.refId == row.id), Is.Empty);
            AcceptedUnchanged(s);
        }

        [TestCase("c0-off")] [TestCase("c0-scheduled")]
        [TestCase("story-off")] [TestCase("story-scheduled")] [TestCase("pre-bonds")]
        [TestCase("canonical-off")] [TestCase("canonical-unknown")] [TestCase("canonical-null")]
        [TestCase("hearing-unknown")] [TestCase("hearing-negative")]
        [TestCase("evidence-null")] [TestCase("receipts-null")]
        [TestCase("disabled-evidence")] [TestCase("disabled-receipt")]
        public void CurrentPublicGateRefusesOffScheduledUnknownOrCorruptAuthorityWithoutRepairingIt(string defect)
        {
            var engine = new EpisodeEngine(Fresh(FirstSeed, true));
            var promise = Command(engine.Snapshot, EpisodeCommandKind.PromiseSafety);
            promise.targetId = Other(engine.Snapshot);
            Commit(engine, promise);
            var s = engine.Snapshot;
            string committed = Json(s);
            switch (defect)
            {
                case "c0-off": s.commitmentRulesStartWeek = 0; break;
                case "c0-scheduled": s.commitmentRulesStartWeek = s.week + 1; break;
                case "story-off": s.story.rulesStartWeek = 0; break;
                case "story-scheduled": s.story.rulesStartWeek = s.week + 1; break;
                case "pre-bonds": s.story.rulesVersion = StoryRules.Bonds - 1; break;
                case "canonical-off": s.unifiedCommitmentRulesVersion = 0; break;
                // 3, not 2: since vote family V6 mode 2 is the unified vote rules, a known mode with its own core.
                case "canonical-unknown": s.unifiedCommitmentRulesVersion = 3; break;
                case "canonical-null": s.unifiedCommitments = null; break;
                case "hearing-unknown": s.unifiedHearingRulesVersion = 2; break;
                case "hearing-negative": s.unifiedHearingRulesVersion = -1; break;
                case "evidence-null": s.unifiedHearingEvidence = null; break;
                case "receipts-null": s.unifiedHearingReceipts = null; break;
                case "disabled-evidence":
                    s.unifiedHearingRulesVersion = 0;
                    s.unifiedHearingEvidence.Add(new UnifiedHearingEvidenceState()); break;
                case "disabled-receipt":
                    s.unifiedHearingRulesVersion = 0;
                    s.unifiedHearingReceipts.Add(new UnifiedHearingReceiptState()); break;
                default: Assert.Fail("Unknown control."); break;
            }
            string corrupt = Json(s);
            Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(() => new EpisodeEngine(s), Throws.ArgumentException);
            Assert.That(Json(s), Is.EqualTo(corrupt), "Public refusal never clears, normalizes or activates authority.");
            Assert.That(Json(engine.Snapshot), Is.EqualTo(committed));
            Commit(engine, Next(engine.Snapshot));
        }

        [TestCase(false, false)] [TestCase(false, true)]
        [TestCase(true, false)] [TestCase(true, true)]
        public void EveryPublicCommandReloadsDeterministicallyThroughWholeWeeksAndTheFinishedSeason(bool builtRoster, bool hearing)
        {
            var initial = Fresh(FirstSeed, hearing, builtRoster);
            var a = new EpisodeEngine(initial);
            var b = new EpisodeEngine(initial);
            var promise = Command(a.Snapshot, EpisodeCommandKind.PromiseSafety);
            promise.targetId = Other(a.Snapshot);
            Commit(a, promise); Commit(b, promise);
            var proposal = Command(a.Snapshot, EpisodeCommandKind.ProposeDeal);
            proposal.targetId = a.Snapshot.Active.First(c => !c.isPlayer && c.id != promise.targetId).id;
            proposal.text = DealKind.SafetyAgreement;
            Commit(a, proposal); Commit(b, proposal);
            b = new EpisodeEngine(b.Snapshot);
            int played = 0;
            var weeks = new HashSet<int> { a.Snapshot.week };
            while (a.Snapshot.phase != EpisodePhase.Finished && played++ < 512)
            {
                var command = Next(a.Snapshot);
                Commit(a, command); Commit(b, command);
                b = new EpisodeEngine(b.Snapshot);
                Assert.That(Json(a.Snapshot), Is.EqualTo(Json(b.Snapshot)), "Same public command, receipt and complete reloaded authority.");
                weeks.Add(a.Snapshot.week);
            }
            var finished = a.Snapshot;
            Assert.That(finished.phase, Is.EqualTo(EpisodePhase.Finished));
            Assert.That(finished.winnerId, Is.Not.Empty);
            Assert.That(finished.runnerUpId, Is.Not.EqualTo(finished.winnerId));
            Assert.That(weeks.Count, Is.GreaterThan(1), "Coverage includes real week turns, not merely a staged final phase.");
            Assert.That(finished.unifiedCommitments.Any(r => r.origin == UnifiedCommitments.PlayerPromise), Is.True);
            Assert.That(finished.acceptedCommandIds.Count, Is.LessThanOrEqualTo(256));
            AcceptedUnchanged(finished);
            string before = Json(finished);
            var rejected = a.Apply(Command(finished, EpisodeCommandKind.Advance));
            Assert.That(rejected.accepted, Is.False);
            Assert.That(rejected.duplicate, Is.False);
            Assert.That(Json(a.Snapshot), Is.EqualTo(before));
        }

        [TestCase(false)] [TestCase(true)]
        public void DisabledFactorySeasonsStillFinishWithoutAcquiringCanonicalOrHearingAuthority(bool builtRoster)
        {
            var initial = BaseFresh(FirstSeed, builtRoster);
            var engine = new EpisodeEngine(initial);
            var s = Reach(engine, x => x.phase == EpisodePhase.Finished, 512);
            Assert.That(s.unifiedCommitmentRulesVersion, Is.Zero);
            Assert.That(s.unifiedCommitments, Is.Empty);
            Assert.That(s.unifiedHearingRulesVersion, Is.Zero);
            Assert.That(s.unifiedHearingEvidence, Is.Empty);
            Assert.That(s.unifiedHearingReceipts, Is.Empty);
            Assert.That(s.winnerId, Is.Not.Empty);
            AcceptedUnchanged(s);
        }

        private sealed class Owned
        {
            public EpisodeEngine Engine;
            public EpisodeState BeforeCreation;
            public string Id, Other, CreationCommand;
        }

        private static EpisodeState BaseFresh(uint seed, bool builtRoster)
        {
            var s = builtRoster ? SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed) : ContentCatalog.Create(seed);
            s.competitionRulesVersion = CompetitionRules.Current;
            s.haveNotRulesStartWeek = 1; s.strategyRulesStartWeek = 1;
            EpisodeEngine.EnableStory(s); EpisodeEngine.EnableRead(s); EpisodeEngine.EnableLevers(s);
            EpisodeEngine.EnableWeek(s); EpisodeEngine.EnableEconomy(s); EpisodeEngine.EnableAgency(s);
            EpisodeEngine.EnableFinale(s); EpisodeEngine.EnableCommitments(s);
            AcceptedUnchanged(s);
            Assert.That(s.unifiedCommitmentRulesVersion, Is.Zero);
            Assert.That(s.unifiedCommitments, Is.Empty);
            Assert.That(s.unifiedHearingRulesVersion, Is.Zero);
            Assert.That(s.unifiedHearingEvidence, Is.Empty);
            Assert.That(s.unifiedHearingReceipts, Is.Empty);
            Assert.That(s.promises.Any(p => p.kind == PromiseKind.Safety), Is.False);
            Assert.That(s.deals.Any(d => d.type == DealKind.SafetyAgreement), Is.False);
            return s;
        }

        private static EpisodeState Fresh(uint seed, bool hearing, bool builtRoster = false)
        {
            var s = BaseFresh(seed, builtRoster);
            // Explicit test-only fresh opt-in. No existing season, legacy row or audience is converted.
            s.unifiedCommitmentRulesVersion = UnifiedCommitments.ProspectiveVersion;
            s.unifiedHearingRulesVersion = hearing ? UnifiedCommitmentHearings.ProspectiveVersion : 0;
            AcceptedUnchanged(s);
            return s;
        }

        private static Owned OwnedHoH(bool promise, bool hearing, uint? onlySeed = null)
        {
            uint first = onlySeed ?? FirstSeed, end = onlySeed.HasValue ? first + 1 : EndSeed;
            for (uint seed = first; seed < end; seed++)
            {
                var engine = new EpisodeEngine(Fresh(seed, hearing));
                Commit(engine, Command(engine.Snapshot, EpisodeCommandKind.Advance));
                var compete = Command(engine.Snapshot, EpisodeCommandKind.Compete); compete.performance = 1;
                Commit(engine, compete);
                if (engine.Snapshot.hohId != engine.Snapshot.playerId) continue;
                Commit(engine, Command(engine.Snapshot, EpisodeCommandKind.Advance));
                var before = engine.Snapshot;
                Assert.That(before.phase, Is.EqualTo(EpisodePhase.Nomination));
                Assert.That(before.nominees, Is.Empty);
                var other = before.Active.FirstOrDefault(c => !c.isPlayer && UnifiedCommitments.Binding(before, before.playerId, c.id).Count == 0);
                if (other == null) continue;
                var priorIds = new HashSet<string>(before.unifiedCommitments.Select(r => r.id), StringComparer.Ordinal);
                var create = Command(before, promise ? EpisodeCommandKind.PromiseSafety : EpisodeCommandKind.ProposeDeal);
                create.targetId = other.id;
                if (!promise) create.text = DealKind.SafetyAgreement;
                Commit(engine, create);
                var row = engine.Snapshot.unifiedCommitments.SingleOrDefault(r => !priorIds.Contains(r.id)
                    && r.origin == (promise ? UnifiedCommitments.PlayerPromise : UnifiedCommitments.PlayerDeal)
                    && r.makerId == before.playerId && r.beneficiaryId == other.id);
                // A lawful proposal can be declined. Search separate real seeds; never turn its no into yes.
                if (row == null) continue;
                return new Owned { Engine = engine, BeforeCreation = before, Id = row.id, Other = other.id, CreationCommand = create.id };
            }
            if (onlySeed.HasValue) return null;
            Assert.Fail("No actual player-HoH Safety creation was found within the fixed 32-seed source controls.");
            return null;
        }

        private static UnifiedCommitmentState Row(Owned owned) => owned.Engine.Snapshot.unifiedCommitments.Single(r => r.id == owned.Id);

        private static void NominateOthers(Owned owned)
        {
            var s = owned.Engine.Snapshot;
            var nominees = EpisodeEngine.NominationCandidates(s).Where(c => c.id != owned.Other).Take(2).ToArray();
            Assert.That(nominees, Has.Length.EqualTo(2));
            var command = Command(s, EpisodeCommandKind.Nominate);
            command.targetId = nominees[0].id; command.secondTargetId = nominees[1].id;
            Commit(owned.Engine, command);
        }

        private static void AssertEmission(Owned owned, bool promise, bool hearing)
        {
            var s = owned.Engine.Snapshot;
            var row = Row(owned);
            var facts = s.story.facts.Where(f => f.kind == FactKinds.BrokenWord && f.refId == row.id).ToArray();
            Assert.That(facts, Has.Length.EqualTo(promise ? 0 : 1));
            var evidence = s.unifiedHearingEvidence.Where(e => e.fact.refId == row.id).ToArray();
            Assert.That(evidence, Has.Length.EqualTo(!promise && hearing ? 1 : 0));
            if (!promise && hearing)
            {
                var initial = s.unifiedHearingReceipts.Single(r => r.factId == facts[0].id && r.kind == UnifiedCommitmentHearings.Initial);
                Assert.That(initial.listenerId, Is.EqualTo(owned.Other));
                Assert.That(initial.heardWeek, Is.EqualTo(row.settledWeek));
                Assert.That(evidence[0].fact.id, Is.EqualTo(facts[0].id));
                Assert.That(evidence[0].fact.knowers, Has.Member(s.playerId));
                Assert.That(evidence[0].fact.knowers, Has.Member(owned.Other));
            }
            AcceptedUnchanged(s);
        }

        private static EpisodeState Reach(EpisodeEngine engine, Func<EpisodeState, bool> reached, int bound, string protectedId = null)
        {
            for (int i = 0; i < bound && !reached(engine.Snapshot); i++) Commit(engine, Next(engine.Snapshot, protectedId));
            var s = engine.Snapshot;
            Assert.That(reached(s), Is.True, "Actual public progression did not reach its requested boundary within " + bound + " commands.");
            return s;
        }

        private static EpisodeCommand Next(EpisodeState s, string protectedId = null)
        {
            var command = Command(s, EpisodeCommandKind.Advance);
            if (s.pendingDiary != null)
            { command.kind = EpisodeCommandKind.SkipDiary; command.targetId = s.pendingDiary.id; }
            else if (EpisodeEngine.IsCompetition(s.phase) && !s.competitionResolved && EpisodeEngine.CompetitionPlayers(s).Any(c => c.isPlayer))
            { command.kind = EpisodeCommandKind.Compete; command.performance = 1; }
            else if (s.phase == EpisodePhase.Nomination && s.nominees.Count == 0 && s.hohId == s.playerId)
            {
                var candidates = EpisodeEngine.NominationCandidates(s).Where(c => c.id != protectedId).Take(2).ToArray();
                if (candidates.Length != 2) candidates = EpisodeEngine.NominationCandidates(s).Take(2).ToArray();
                command.kind = EpisodeCommandKind.Nominate; command.targetId = candidates[0].id; command.secondTargetId = candidates[1].id;
            }
            else if (s.phase == EpisodePhase.VetoMeeting && !s.vetoResolved)
            {
                string saved = s.vetoHolderId == s.playerId ? null : EpisodeEngine.NpcVetoSave(s);
                if (s.vetoHolderId == s.playerId || (s.hohId == s.playerId && saved != null))
                {
                    command.kind = EpisodeCommandKind.ResolveVeto;
                    command.useVeto = saved != null; command.targetId = saved;
                    command.secondTargetId = saved == null ? null : EpisodeEngine.ReplacementCandidates(s).First(c => c.id != protectedId).id;
                    // A legal player-held self-save improves survival, never assigns the role or replacement.
                    // When an NPC is HoH the production owner chooses its replacement from this null input.
                    if (s.vetoHolderId == s.playerId && s.nominees.Contains(s.playerId)
                        && !EpisodeEngine.VetoIsLockedAtFinalFour(s) && EpisodeEngine.ReplacementCandidates(s).Any())
                    { command.useVeto = true; command.targetId = s.playerId; command.secondTargetId = null; }
                }
            }
            else if (s.phase == EpisodePhase.Eviction && s.evictionStage == EvictionStage.Speeches && s.nominees.Contains(s.playerId)
                && !s.evictionSpeeches.Any(e => e.speakerId == s.playerId))
            { command.kind = EpisodeCommandKind.SubmitEvictionSpeech; command.text = "I own the game I played."; }
            else if (s.phase == EpisodePhase.Eviction && !s.evictionResolved
                && (s.evictionStage == EvictionStage.Voting || s.evictionStage == EvictionStage.Tiebreaker)
                && !s.votes.Any(v => v.voterId == s.playerId)
                && (EpisodeEngine.Voters(s).Any(c => c.isPlayer) || EpisodeEngine.NeedsPlayerTieBreak(s)))
            { command.kind = EpisodeCommandKind.CastVote; command.targetId = s.nominees[0]; }
            else if (s.phase == EpisodePhase.FinalEviction && s.hohId == s.playerId)
            { command.kind = EpisodeCommandKind.FinalEvict; command.targetId = s.Active.First(c => !c.isPlayer).id; }
            else if (s.phase == EpisodePhase.JuryQuestioning && !s.juryExchanges[s.juryQuestionIndex].completed)
            {
                var q = s.juryExchanges[s.juryQuestionIndex];
                command.kind = EpisodeCommandKind.AnswerJury;
                command.targetId = q.finalistId == s.playerId ? q.questionerId : q.finalistId;
                command.secondTargetId = q.finalistId == s.playerId ? FinaleQuestions.Offered(q.category, q.receiptKind)[0]
                    : WebJuryQuestioning.GetJurorQuestionOptions(s.juryQuestionIndex)[0].tone;
            }
            else if (s.phase == EpisodePhase.FinalSpeeches && s.Active.Any(c => c.isPlayer) && !s.finalSpeeches.Any(e => e.speakerId == s.playerId))
            { command.kind = EpisodeCommandKind.SubmitSpeech; command.text = FinalArgument.Speech(s); }
            else if (s.phase == EpisodePhase.Jury && !s.Active.Any(c => c.isPlayer) && !s.votes.Any(v => v.voterId == s.playerId))
            { command.kind = EpisodeCommandKind.CastVote; command.targetId = s.Active.First().id; }
            return command;
        }

        private static string Other(EpisodeState s) => s.Active.First(c => !c.isPlayer).id;

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind) => new EpisodeCommand
        {
            id = "public-safety-" + s.revision + "-" + kind, kind = kind, actorId = s.playerId,
            expectedRevision = s.revision, expectedPhase = s.phase,
        };

        private static CommandResult Commit(EpisodeEngine engine, EpisodeCommand command)
        {
            var before = engine.Snapshot;
            string unchanged = Json(before), input = JsonConvert.SerializeObject(command);
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True, "Seed " + before.seed + ", " + before.phase + ", " + command.kind + ": " + result.reason);
            Assert.That(result.duplicate, Is.False);
            Assert.That(result.state.revision, Is.EqualTo(before.revision + 1));
            Assert.That(result.state.acceptedCommandIds.Last(), Is.EqualTo(command.id));
            Assert.That(result.state.acceptedCommandIds.Count, Is.EqualTo(Math.Min(256, before.acceptedCommandIds.Count + 1)));
            Assert.That(Json(result.state), Is.EqualTo(Json(engine.Snapshot)));
            Assert.That(Json(before), Is.EqualTo(unchanged));
            Assert.That(JsonConvert.SerializeObject(command), Is.EqualTo(input));
            if (UnifiedCommitments.RulesOn(result.state))
            {
                Assert.That(result.state.promises.Any(p => p.kind == PromiseKind.Safety), Is.False);
                Assert.That(result.state.deals.Any(d => d.type == DealKind.SafetyAgreement), Is.False);
            }
            AcceptedUnchanged(result.state);
            return result;
        }

        private static void AcceptedUnchanged(EpisodeState s)
        {
            string before = Json(s);
            Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);
            Assert.That(Json(s), Is.EqualTo(before), "The shared full validator remains read-only.");
        }

        private static string Json(EpisodeState s) => s == null ? "null" : JsonConvert.SerializeObject(typeof(EpisodeState)
            .GetFields(BindingFlags.Public | BindingFlags.Instance).ToDictionary(f => f.Name, f => f.GetValue(s)));
    }
}
