// SOURCE-ONLY tests for the complete historical V3 helper, not whole-save acceptance.
// Authority: final-native-graph-01/project/Assets/Gamesim/Simulation/
// EpisodeSocialHistoryValidation.cs:8-65 (ce9b8c44c5e3579a01b3941b2190a79ef02c2575946e72a70b46745275611bd8).
// Fixtures are explicit local component data; no live factory, parser, current DTO or game API.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Data = Gamesim.Persistence.Frozen28Data;

namespace Gamesim.Persistence.Frozen28Components.Tests
{
    [TestFixture]
    public sealed class FrozenV28SocialHistoryTests
    {
        private const string PersonaStorage = "Invalid persona history or scores.";
        private const string PersonaCounts = "Persona scores must match the recorded choices.";
        private const string PersonaLabel = "Persona label does not match the ordered score history.";
        private const string DiaryCounters = "Invalid diary counters or receipts.";
        private const string DiaryIdentity = "Unsupported resolved diary identity.";
        private const string DiaryLinks = "Diary history must refer to once-per-week resolved reflections.";
        private const string PendingDiary = "Invalid or stale pending diary reflection.";
        private const string JuryLedger = "Invalid jury impression ledger.";
        private const string JuryAverage = "Jury impression average does not match the recorded jurors.";
        private const string OathStorage = "Invalid structured loyalty declaration.";
        private const string Opportunities = "Invalid or duplicate loyalty opportunity.";
        private const string OathMilestone = "A native loyalty declaration requires its recorded milestone.";

        [Test]
        public void V3_MinimalExplicitComponentPassesWithoutRepairingUnusedNullFields()
        {
            var state = ValidState();
            Check(state, true, null);
            Assert.That(state.relationships, Is.Null, "This fixture is not a complete save.");
            Assert.That(state.npcSocial, Is.Null, "The V3 component must not fill unrelated state.");
            Assert.That(state.story, Is.Null);
        }

        [TestCase("nullPersona")]
        [TestCase("nullScores")]
        [TestCase("missingScore")]
        [TestCase("extraScore")]
        [TestCase("nullScore")]
        [TestCase("unknownPersona")]
        [TestCase("duplicatePersona")]
        [TestCase("negativeScore")]
        [TestCase("score101")]
        [TestCase("nullHistory")]
        [TestCase("history101")]
        [TestCase("nullHistoryRow")]
        [TestCase("unknownHistoryPersona")]
        [TestCase("historyWeekZero")]
        [TestCase("historyFutureWeek")]
        public void V3_PersonaStorageRefusals(string scenario)
        {
            var s = ValidState();
            Check(s, true, null);
            switch (scenario)
            {
                case "nullPersona": s.playerPersona = null; break;
                case "nullScores": s.playerPersona.scores = null; break;
                case "missingScore": s.playerPersona.scores.RemoveAt(4); break;
                case "extraScore": s.playerPersona.scores.Add(new Data.WebPersonaScore { persona = "Neutral", score = 0 }); break;
                case "nullScore": s.playerPersona.scores[0] = null; break;
                case "unknownPersona": s.playerPersona.scores[0].persona = "Unknown"; break;
                case "duplicatePersona": s.playerPersona.scores[4].persona = "Neutral"; break;
                case "negativeScore": s.playerPersona.scores[0].score = -1; break;
                case "score101": s.playerPersona.scores[0].score = 101; break;
                case "nullHistory": s.playerPersona.history = null; break;
                case "history101": s.playerPersona.history = Enumerable.Range(1, 101).Select(i => new Data.WebPersonaHistory { persona = "Neutral", week = i }).ToList(); break;
                case "nullHistoryRow": s.playerPersona.history.Add(null); break;
                case "unknownHistoryPersona": s.playerPersona.history.Add(new Data.WebPersonaHistory { persona = "Unknown", week = 1 }); break;
                case "historyWeekZero": s.playerPersona.history.Add(new Data.WebPersonaHistory { persona = "Neutral", week = 0 }); break;
                case "historyFutureWeek": s.playerPersona.history.Add(new Data.WebPersonaHistory { persona = "Neutral", week = 5 }); break;
                default: throw new ArgumentException("Unknown authored scenario.");
            }
            Check(s, false, PersonaStorage);
        }

        [TestCase("zeroHistory")]
        [TestCase("oneChoiceStillNeutral")]
        [TestCase("thresholdTwo")]
        [TestCase("originalTieOrder")]
        [TestCase("reorderedTieOrder")]
        [TestCase("wrongCount")]
        [TestCase("wrongLabel")]
        [TestCase("scoreAndHistory100")]
        public void V3_PersonaMeaningAndStoredTieOrder(string scenario)
        {
            var s = ValidState();
            Check(s, true, null);
            string error = null;
            switch (scenario)
            {
                case "zeroHistory": break;
                case "oneChoiceStillNeutral": AddHistory(s, "Remorseful"); break;
                case "thresholdTwo": AddHistory(s, "Remorseful", "Remorseful"); s.playerPersona.current = "Remorseful"; break;
                case "originalTieOrder": AddHistory(s, "Remorseful", "Ruthless", "Remorseful", "Ruthless"); s.playerPersona.current = "Remorseful"; break;
                case "reorderedTieOrder":
                    AddHistory(s, "Remorseful", "Ruthless", "Remorseful", "Ruthless");
                    var ruthless = s.playerPersona.scores[2];
                    s.playerPersona.scores.RemoveAt(2); s.playerPersona.scores.Insert(0, ruthless);
                    s.playerPersona.current = "Ruthless";
                    break;
                case "wrongCount": s.playerPersona.scores[1].score = 1; error = PersonaCounts; break;
                case "wrongLabel": AddHistory(s, "Remorseful", "Remorseful"); s.playerPersona.current = "Ruthless"; error = PersonaLabel; break;
                case "scoreAndHistory100": AddHistory(s, Enumerable.Repeat("Remorseful", 100).ToArray()); s.playerPersona.current = "Remorseful"; break;
                default: throw new ArgumentException("Unknown authored scenario.");
            }
            Check(s, error == null, error);
        }

        [TestCase("socialNegative")]
        [TestCase("social1001")]
        [TestCase("compNegative")]
        [TestCase("comp1001")]
        [TestCase("lastNegative")]
        [TestCase("lastFuture")]
        [TestCase("nullReceipts")]
        [TestCase("receipts101")]
        [TestCase("nullReceipt")]
        [TestCase("blankReceipt")]
        [TestCase("longReceipt")]
        [TestCase("duplicateReceipt")]
        [TestCase("social1000")]
        [TestCase("comp1000")]
        public void V3_DiaryCounterAndReceiptBounds(string scenario)
        {
            var s = ValidState();
            Check(s, true, null);
            bool valid = false;
            switch (scenario)
            {
                case "socialNegative": s.phaseEventSocialBonus = -1; break;
                case "social1001": s.phaseEventSocialBonus = 1001; break;
                case "compNegative": s.phaseEventCompBonus = -1; break;
                case "comp1001": s.phaseEventCompBonus = 1001; break;
                case "lastNegative": s.lastDiaryRoomWeek = -1; break;
                case "lastFuture": s.lastDiaryRoomWeek = 5; break;
                case "nullReceipts": s.resolvedDiaryIds = null; break;
                case "receipts101": s.resolvedDiaryIds = Enumerable.Repeat("diary-post_eviction-1", 101).ToList(); break;
                case "nullReceipt": s.resolvedDiaryIds.Add(null); break;
                case "blankReceipt": s.resolvedDiaryIds.Add(" "); break;
                case "longReceipt": s.resolvedDiaryIds.Add(new string('x', 161)); break;
                case "duplicateReceipt": s.resolvedDiaryIds.Add("diary-post_eviction-1"); s.resolvedDiaryIds.Add("diary-post_eviction-1"); break;
                case "social1000": s.phaseEventSocialBonus = 1000; valid = true; break;
                case "comp1000": s.phaseEventCompBonus = 1000; valid = true; break;
                default: throw new ArgumentException("Unknown authored scenario.");
            }
            Check(s, valid, valid ? null : DiaryCounters);
        }

        [TestCase("other-4", false)]
        [TestCase("diary-post_eviction-", false)]
        [TestCase("diary-post_eviction-text", false)]
        [TestCase("diary-post_eviction-0", false)]
        [TestCase("diary-post_eviction-5", false)]
        [TestCase("diary-post_eviction-04", false)]
        [TestCase("diary-post_eviction-+4", false)]
        [TestCase("diary-post_eviction- 4", false)]
        [TestCase("diary-post_eviction-4 ", false)]
        [TestCase("diary-post_eviction-2147483648", false)]
        [TestCase("Diary-post_eviction-4", false)]
        [TestCase("diary-post_eviction-4", true)]
        public void V3_ResolvedDiaryIdentitiesRemainCanonical(string id, bool valid)
        {
            var s = ValidState();
            Check(s, true, null);
            s.resolvedDiaryIds.Add(id); s.lastDiaryRoomWeek = 4;
            Check(s, valid, valid ? null : DiaryIdentity);
        }

        [TestCase("missingHistoryReceipt")]
        [TestCase("duplicateHistoryWeek")]
        [TestCase("lastWeekMismatch")]
        [TestCase("unorderedReceipts")]
        public void V3_DiaryHistoryReferencesAndOrder(string scenario)
        {
            var s = ValidState();
            Check(s, true, null);
            bool valid = false;
            switch (scenario)
            {
                case "missingHistoryReceipt": AddHistory(s, "Neutral"); s.resolvedDiaryIds.Clear(); s.lastDiaryRoomWeek = 0; break;
                case "duplicateHistoryWeek":
                    AddHistory(s, "Neutral", "Neutral");
                    s.playerPersona.history[1].week = 1;
                    s.resolvedDiaryIds.RemoveAt(1); s.lastDiaryRoomWeek = 1;
                    break;
                case "lastWeekMismatch": s.resolvedDiaryIds.Add("diary-post_eviction-1"); break;
                case "unorderedReceipts":
                    s.playerPersona.history.Add(new Data.WebPersonaHistory { persona = "Remorseful", week = 2 });
                    s.playerPersona.scores[1].score = 1;
                    s.resolvedDiaryIds.Add("diary-post_eviction-4"); s.resolvedDiaryIds.Add("diary-post_eviction-2");
                    s.lastDiaryRoomWeek = 4; valid = true;
                    break;
                default: throw new ArgumentException("Unknown authored scenario.");
            }
            Check(s, valid, valid ? null : DiaryLinks);
        }

        [TestCase("social")]
        [TestCase("resolvedEviction")]
        [TestCase("wrongTrigger")]
        [TestCase("futureWeek")]
        [TestCase("wrongIdentity")]
        [TestCase("nominee")]
        [TestCase("missingEvictee")]
        [TestCase("activeEvictee")]
        [TestCase("evictedNotJury")]
        [TestCase("inactivePlayer")]
        [TestCase("alreadyResolved")]
        [TestCase("wrongPhase")]
        [TestCase("unresolvedEviction")]
        public void V3_PendingDiaryEligibility(string scenario)
        {
            var s = PendingState();
            Check(s, true, null);
            bool valid = false;
            switch (scenario)
            {
                case "social": valid = true; break;
                case "resolvedEviction": s.phase = Data.EpisodePhase.Eviction; s.evictionResolved = true; valid = true; break;
                case "wrongTrigger": s.pendingDiary.trigger = "other"; break;
                case "futureWeek": s.pendingDiary.week = 5; break;
                case "wrongIdentity": s.pendingDiary.id = "diary-post_eviction-3"; break;
                case "nominee": s.pendingDiary.isNominee = true; break;
                case "missingEvictee": s.pendingDiary.evictedId = "missing"; break;
                case "activeEvictee": ActorById(s, "j").status = Data.ContestantStatus.Active; break;
                case "evictedNotJury": ActorById(s, "j").status = Data.ContestantStatus.Evicted; break;
                case "inactivePlayer": ActorById(s, "p").status = Data.ContestantStatus.Jury; break;
                case "alreadyResolved": s.resolvedDiaryIds.Add("diary-post_eviction-4"); s.lastDiaryRoomWeek = 4; break;
                case "wrongPhase": s.phase = Data.EpisodePhase.HoH; break;
                case "unresolvedEviction": s.phase = Data.EpisodePhase.Eviction; s.evictionResolved = false; break;
                default: throw new ArgumentException("Unknown authored scenario.");
            }
            Check(s, valid, valid ? null : PendingDiary);
        }

        [TestCase("nullContainer")]
        [TestCase("nullRows")]
        [TestCase("overCeiling")]
        [TestCase("nullRow")]
        [TestCase("unknownIdentity")]
        [TestCase("activeIdentity")]
        [TestCase("nullName")]
        [TestCase("blankName")]
        [TestCase("name101")]
        [TestCase("name100")]
        [TestCase("sentimentLow")]
        [TestCase("sentimentHigh")]
        [TestCase("sentimentNaN")]
        [TestCase("sentimentInfinity")]
        [TestCase("atCeiling")]
        [TestCase("duplicateIdentity")]
        public void V3_JuryStorageBounds(string scenario)
        {
            var s = JuryState();
            Check(s, true, null);
            var row = s.jurySentiment.jurors[0];
            bool valid = false;
            switch (scenario)
            {
                case "nullContainer": s.jurySentiment = null; break;
                case "nullRows": s.jurySentiment.jurors = null; break;
                case "overCeiling": s.jurySentiment.jurors.Add(Juror("j")); s.jurySentiment.jurors.Add(Juror("j")); break;
                case "nullRow": s.jurySentiment.jurors[0] = null; break;
                case "unknownIdentity": row.jurorId = "missing"; break;
                case "activeIdentity": row.jurorId = "a"; break;
                case "nullName": row.jurorName = null; break;
                case "blankName": row.jurorName = " "; break;
                case "name101": row.jurorName = new string('x', 101); break;
                case "name100": row.jurorName = new string('x', 100); valid = true; break;
                case "sentimentLow": row.sentiment = -101; break;
                case "sentimentHigh": row.sentiment = 101; break;
                case "sentimentNaN": row.sentiment = double.NaN; break;
                case "sentimentInfinity": row.sentiment = double.PositiveInfinity; break;
                case "atCeiling": ActorById(s, "b").status = Data.ContestantStatus.Evicted; s.phase = Data.EpisodePhase.Jury; s.jurySentiment.jurors.Add(Juror("b")); valid = true; break;
                case "duplicateIdentity": s.jurySentiment.jurors.Add(Juror("j")); break;
                default: throw new ArgumentException("Unknown authored scenario.");
            }
            Check(s, valid, valid ? null : JuryLedger);
        }

        [TestCase("nullEvents")]
        [TestCase("emptyEvents")]
        [TestCase("events513")]
        [TestCase("events512")]
        [TestCase("nullEvent")]
        [TestCase("weekNegative")]
        [TestCase("weekFuture")]
        [TestCase("weekZero")]
        [TestCase("weekCurrent")]
        [TestCase("deltaLow")]
        [TestCase("deltaHigh")]
        [TestCase("deltaNaN")]
        [TestCase("reasonNull")]
        [TestCase("reason4001")]
        [TestCase("reason4000")]
        [TestCase("reasonBlank")]
        [TestCase("delta200")]
        [TestCase("deltaMinus200")]
        [TestCase("deltaInfinity")]
        public void V3_JuryEventBounds(string scenario)
        {
            var s = JuryState();
            Check(s, true, null);
            var row = s.jurySentiment.jurors[0];
            var entry = row.events[0];
            bool valid = false;
            switch (scenario)
            {
                case "nullEvents": row.events = null; break;
                case "emptyEvents": row.events.Clear(); break;
                case "events513": row.events = Enumerable.Range(0, 513).Select(i => JuryEvent()).ToList(); break;
                case "events512": row.events = Enumerable.Range(0, 512).Select(i => JuryEvent()).ToList(); valid = true; break;
                case "nullEvent": row.events[0] = null; break;
                case "weekNegative": entry.week = -1; break;
                case "weekFuture": entry.week = 5; break;
                case "weekZero": entry.week = 0; valid = true; break;
                case "weekCurrent": entry.week = 4; valid = true; break;
                case "deltaLow": entry.delta = -201; break;
                case "deltaHigh": entry.delta = 201; break;
                case "deltaNaN": entry.delta = double.NaN; break;
                case "reasonNull": entry.reason = null; break;
                case "reason4001": entry.reason = new string('x', 4001); break;
                case "reason4000": entry.reason = new string('x', 4000); valid = true; break;
                case "reasonBlank": entry.reason = " "; break;
                case "delta200": entry.delta = 200; valid = true; break;
                case "deltaMinus200": entry.delta = -200; valid = true; break;
                case "deltaInfinity": entry.delta = double.NegativeInfinity; break;
                default: throw new ArgumentException("Unknown authored scenario.");
            }
            Check(s, valid, valid ? null : JuryLedger);
        }

        [TestCase("emptyNonzero")]
        [TestCase("positiveHalf")]
        [TestCase("negativeHalf")]
        [TestCase("negativeMidpoint")]
        [TestCase("wrongAverage")]
        [TestCase("overallNaN")]
        [TestCase("overallHigh")]
        [TestCase("positiveBoundary")]
        [TestCase("negativeBoundary")]
        public void V3_JuryAveragePreservesJavascriptRounding(string scenario)
        {
            var s = JuryState();
            Check(s, true, null);
            var row = s.jurySentiment.jurors[0];
            string error = null;
            switch (scenario)
            {
                case "emptyNonzero": s = ValidState(); s.jurySentiment.overallSentiment = 1; error = JuryAverage; break;
                case "positiveHalf": row.sentiment = 0.5; s.jurySentiment.overallSentiment = 1; break;
                case "negativeHalf": row.sentiment = -0.5; s.jurySentiment.overallSentiment = 0; break;
                case "negativeMidpoint":
                    row.sentiment = -37;
                    s.contestants.Add(Actor("k", false, Data.ContestantStatus.Jury));
                    var second = Juror("k"); second.sentiment = -38; s.jurySentiment.jurors.Add(second);
                    s.jurySentiment.overallSentiment = -37;
                    break;
                case "wrongAverage": row.sentiment = 20; s.jurySentiment.overallSentiment = 19; error = JuryAverage; break;
                case "overallNaN": s.jurySentiment.overallSentiment = double.NaN; error = JuryLedger; break;
                case "overallHigh": s.jurySentiment.overallSentiment = 101; error = JuryLedger; break;
                case "positiveBoundary": row.sentiment = 100; s.jurySentiment.overallSentiment = 100; break;
                case "negativeBoundary": row.sentiment = -100; s.jurySentiment.overallSentiment = -100; break;
                default: throw new ArgumentException("Unknown authored scenario.");
            }
            // Event deltas intentionally do not sum to every row's sentiment: V3 has no such rule.
            Check(s, error == null, error);
        }

        [TestCase("activeTarget")]
        [TestCase("inactiveTarget")]
        [TestCase("nullList")]
        [TestCase("nullRow")]
        [TestCase("wrongPlayer")]
        [TestCase("unknownTarget")]
        [TestCase("playerTarget")]
        [TestCase("weekZero")]
        [TestCase("futureWeek")]
        [TestCase("timestampZero")]
        [TestCase("timestampNext")]
        [TestCase("duplicateTarget")]
        [TestCase("overCeiling")]
        public void V3_OathStorageAndInactiveTargets(string scenario)
        {
            var s = OathState();
            Check(s, true, null);
            var row = s.loyaltyOaths[0];
            bool valid = false;
            switch (scenario)
            {
                case "activeTarget": row.week = 4; row.timestamp = 9; s.loyaltyOaths.Add(Oath("b")); s.shownOathMilestones.Add("b"); valid = true; break;
                case "inactiveTarget": ActorById(s, "a").status = Data.ContestantStatus.Jury; s.phase = Data.EpisodePhase.Jury; valid = true; break;
                case "nullList": s.loyaltyOaths = null; break;
                case "nullRow": s.loyaltyOaths[0] = null; break;
                case "wrongPlayer": row.playerId = "b"; break;
                case "unknownTarget": row.targetId = "missing"; break;
                case "playerTarget": row.targetId = "p"; break;
                case "weekZero": row.week = 0; break;
                case "futureWeek": row.week = 5; break;
                case "timestampZero": row.timestamp = 0; break;
                case "timestampNext": row.timestamp = 10; break;
                case "duplicateTarget": s.loyaltyOaths.Add(Oath("a")); break;
                case "overCeiling": s.loyaltyOaths.Add(Oath("a")); s.loyaltyOaths.Add(Oath("a")); break;
                default: throw new ArgumentException("Unknown authored scenario.");
            }
            Check(s, valid, valid ? null : OathStorage);
        }

        [TestCase("activeOpportunity")]
        [TestCase("atCeiling")]
        [TestCase("inactiveMilestone")]
        [TestCase("nullOpportunities")]
        [TestCase("nullMilestones")]
        [TestCase("opportunityOverCeiling")]
        [TestCase("milestoneOverCeiling")]
        [TestCase("unknownMilestone")]
        [TestCase("playerMilestone")]
        [TestCase("duplicateMilestone")]
        [TestCase("unknownOpportunity")]
        [TestCase("playerOpportunity")]
        [TestCase("inactiveOpportunity")]
        [TestCase("missingMilestone")]
        [TestCase("alreadySworn")]
        [TestCase("duplicateOpportunity")]
        [TestCase("oathWithoutMilestone")]
        public void V3_OpportunitiesAndMilestones(string scenario)
        {
            var s = ValidState();
            Check(s, true, null);
            s.oathOpportunities.Add("a"); s.shownOathMilestones.Add("a");
            bool valid = false;
            string error = Opportunities;
            switch (scenario)
            {
                case "activeOpportunity": valid = true; break;
                case "atCeiling": s.oathOpportunities.Add("b"); s.shownOathMilestones.Add("b"); valid = true; break;
                case "inactiveMilestone": s.oathOpportunities.Clear(); ActorById(s, "a").status = Data.ContestantStatus.Evicted; s.phase = Data.EpisodePhase.Jury; valid = true; break;
                case "nullOpportunities": s.oathOpportunities = null; break;
                case "nullMilestones": s.shownOathMilestones = null; break;
                case "opportunityOverCeiling": s.oathOpportunities.Add("b"); s.oathOpportunities.Add("a"); break;
                case "milestoneOverCeiling": s.shownOathMilestones.Add("b"); s.shownOathMilestones.Add("a"); break;
                case "unknownMilestone": s.shownOathMilestones[0] = "missing"; break;
                case "playerMilestone": s.shownOathMilestones[0] = "p"; break;
                case "duplicateMilestone": s.shownOathMilestones.Add("a"); break;
                case "unknownOpportunity": s.oathOpportunities[0] = "missing"; break;
                case "playerOpportunity": s.oathOpportunities[0] = "p"; break;
                case "inactiveOpportunity": ActorById(s, "a").status = Data.ContestantStatus.Evicted; break;
                case "missingMilestone": s.shownOathMilestones.Clear(); break;
                case "alreadySworn": s.loyaltyOaths.Add(Oath("a")); break;
                case "duplicateOpportunity": s.oathOpportunities.Add("a"); break;
                case "oathWithoutMilestone": s.loyaltyOaths.Add(Oath("b")); error = OathMilestone; break;
                default: throw new ArgumentException("Unknown authored scenario.");
            }
            Check(s, valid, valid ? null : error);
        }

        [TestCase("schemaZero")]
        [TestCase("schema29")]
        [TestCase("weekZero")]
        [TestCase("unknownPhase")]
        [TestCase("unknownCompetitionVersion")]
        [TestCase("negativeRevision")]
        public void V3_DoesNotPretendToOwnGeneralCoreValidation(string scenario)
        {
            var s = ValidState();
            Check(s, true, null);
            switch (scenario)
            {
                case "schemaZero": s.schemaVersion = 0; break;
                case "schema29": s.schemaVersion = 29; break;
                case "weekZero": s.week = 0; break;
                case "unknownPhase": s.phase = (Data.EpisodePhase)999; break;
                case "unknownCompetitionVersion": s.competitionRulesVersion = 99; break;
                case "negativeRevision": s.revision = -1; break;
                default: throw new ArgumentException("Unknown authored scenario.");
            }
            Check(s, true, null);
        }

        [Test]
        public void V3_NullRootRetainsHistoricalExceptionInsteadOfInventedGuard()
        {
            string error = "sentinel";
            Assert.Throws<NullReferenceException>(() => FrozenV28SocialHistory.TryValidateV3(null, out error));
            Assert.That(error, Is.Null, "The historical helper clears error before dereferencing the root.");
        }

        [Test]
        public void V3_ActualDependencyExceptionUnwindsWithoutMutatingTheObjectGraph()
        {
            var s = PendingState();
            Check(s, true, null);
            s.contestants.Remove(ActorById(s, "p"));
            var witness = new GraphWitness(s);
            string error = "sentinel";
            bool unwound = false;
            Assert.Throws<NullReferenceException>(() =>
            {
                try { FrozenV28SocialHistory.TryValidateV3(s, out error); }
                finally { witness.AssertUnchanged(s); unwound = true; }
            });
            Assert.That(unwound, Is.True);
            Assert.That(error, Is.Null);
        }

        [Test]
        public void V3_RepeatedCallsPreserveSharedListReferences()
        {
            var s = JuryState();
            s.contestants.Add(Actor("k", false, Data.ContestantStatus.Evicted));
            var second = Juror("k"); second.events = s.jurySentiment.jurors[0].events;
            s.jurySentiment.jurors.Add(second);
            var witness = new GraphWitness(s);
            for (int i = 0; i < 3; i++) Check(s, true, null);
            Assert.That(second.events, Is.SameAs(s.jurySentiment.jurors[0].events));
            witness.AssertUnchanged(s);
        }

        private static Data.EpisodeState ValidState()
        {
            // Only this component's dependencies are populated. Unrelated null fields stay null;
            // this is deliberately not a manufactured claim of complete historical save validity.
            return new Data.EpisodeState
            {
                schemaVersion = 28, competitionRulesVersion = 4, sessionId = "v3-component", seed = 1,
                randomState = 0, revision = 0, week = 4, nextSequence = 10, phase = Data.EpisodePhase.Social,
                playerId = "p", contestants = new List<Data.ContestantState>
                {
                    Actor("p", true, Data.ContestantStatus.Active), Actor("a", false, Data.ContestantStatus.Active),
                    Actor("b", false, Data.ContestantStatus.Active)
                },
                playerPersona = new Data.WebPersonaState
                {
                    current = "Neutral", scores = new List<Data.WebPersonaScore>
                    {
                        new Data.WebPersonaScore { persona = "Neutral", score = 0 },
                        new Data.WebPersonaScore { persona = "Remorseful", score = 0 },
                        new Data.WebPersonaScore { persona = "Ruthless", score = 0 },
                        new Data.WebPersonaScore { persona = "Calculated", score = 0 },
                        new Data.WebPersonaScore { persona = "Social Butterfly", score = 0 }
                    },
                    history = new List<Data.WebPersonaHistory>()
                },
                phaseEventSocialBonus = 0, phaseEventCompBonus = 0, lastDiaryRoomWeek = 0,
                resolvedDiaryIds = new List<string>(), pendingDiary = null,
                jurySentiment = new Data.WebJurySentimentState { overallSentiment = 0, jurors = new List<Data.WebJurorSentiment>() },
                loyaltyOaths = new List<Data.WebOathRecord>(), oathOpportunities = new List<string>(), shownOathMilestones = new List<string>()
            };
        }

        private static Data.ContestantState Actor(string id, bool player, Data.ContestantStatus status)
        {
            return new Data.ContestantState
            {
                id = id, name = "Actor " + id, isPlayer = player, status = status,
                age = 30, mood = "Neutral", stressLevel = "Normal", traits = new List<string>(), nominationWeeks = new List<int>(),
                stats = new Data.ContestantStats { physical = 5, mental = 5, endurance = 5, social = 5, luck = 5, competition = 5, strategic = 5, loyalty = 5 }
            };
        }

        private static Data.ContestantState ActorById(Data.EpisodeState state, string id) => state.contestants.Single(x => x.id == id);

        private static void AddHistory(Data.EpisodeState s, params string[] personas)
        {
            s.week = Math.Max(s.week, personas.Length);
            for (int i = 0; i < personas.Length; i++)
            {
                int week = i + 1;
                s.playerPersona.history.Add(new Data.WebPersonaHistory { persona = personas[i], week = week });
                s.playerPersona.scores.Single(x => x.persona == personas[i]).score++;
                s.resolvedDiaryIds.Add("diary-post_eviction-" + week.ToString(CultureInfo.InvariantCulture));
            }
            s.lastDiaryRoomWeek = personas.Length;
            // The expected label is specified literally by each test, never computed as its oracle.
        }

        private static Data.EpisodeState PendingState()
        {
            var s = ValidState();
            s.contestants.Add(Actor("j", false, Data.ContestantStatus.Jury));
            s.pendingDiary = new Data.DiaryPromptState { id = "diary-post_eviction-4", trigger = "post_eviction", evictedId = "j", week = 4, isNominee = false };
            return s;
        }

        private static Data.WebJurySentimentEvent JuryEvent() => new Data.WebJurySentimentEvent { week = 0, delta = 0, reason = "Recorded impression" };
        private static Data.WebJurorSentiment Juror(string id) => new Data.WebJurorSentiment
        {
            jurorId = id, jurorName = "Juror " + id, sentiment = 0, events = new List<Data.WebJurySentimentEvent> { JuryEvent() }
        };
        private static Data.EpisodeState JuryState()
        {
            var s = ValidState();
            s.contestants.Add(Actor("j", false, Data.ContestantStatus.Jury));
            s.jurySentiment.jurors.Add(Juror("j"));
            return s;
        }
        private static Data.WebOathRecord Oath(string target) => new Data.WebOathRecord { playerId = "p", targetId = target, week = 1, timestamp = 1 };
        private static Data.EpisodeState OathState()
        {
            var s = ValidState(); s.loyaltyOaths.Add(Oath("a")); s.shownOathMilestones.Add("a"); return s;
        }

        private static void Check(Data.EpisodeState state, bool expected, string expectedError)
        {
            var witness = new GraphWitness(state);
            try
            {
                string error = "sentinel";
                bool accepted = FrozenV28SocialHistory.TryValidateV3(state, out error);
                Assert.That(accepted, Is.EqualTo(expected));
                Assert.That(error, Is.EqualTo(expectedError));
            }
            finally { witness.AssertUnchanged(state); }
        }

        // Reflection is used solely as an immutability witness over the fixed local DTO fields,
        // never to derive the validity oracle, vocabulary, bounds, or expected results.
        private sealed class GraphWitness
        {
            private readonly object root;
            private readonly List<NodeWitness> nodes = new List<NodeWitness>();
            internal GraphWitness(object root)
            {
                this.root = root;
                var seen = new HashSet<object>(ReferenceComparer.Instance);
                var pending = new Stack<object>();
                if (root != null) pending.Push(root);
                while (pending.Count != 0)
                {
                    var current = pending.Pop();
                    if (current == null || current is string || current.GetType().IsValueType || !seen.Add(current)) continue;
                    var node = new NodeWitness(current); nodes.Add(node);
                    foreach (var child in node.Values) if (child != null) pending.Push(child);
                }
            }
            internal void AssertUnchanged(object actual)
            {
                Assert.That(actual, Is.SameAs(root));
                foreach (var node in nodes) node.AssertUnchanged();
            }
        }

        private sealed class NodeWitness
        {
            private readonly object original;
            private readonly IList list;
            private readonly FieldInfo[] fields;
            internal readonly object[] Values;
            internal NodeWitness(object original)
            {
                this.original = original;
                list = original as IList;
                if (list != null) { Values = list.Cast<object>().ToArray(); return; }
                if (original.GetType().Namespace != "Gamesim.Persistence.Frozen28Data")
                    throw new InvalidOperationException("The fixture witness accepts only fixed local data and lists.");
                fields = original.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public);
                Values = fields.Select(field => field.GetValue(original)).ToArray();
            }
            internal void AssertUnchanged()
            {
                if (list != null)
                {
                    Assert.That(list.Count, Is.EqualTo(Values.Length));
                    for (int i = 0; i < Values.Length; i++) AssertSlot(list[i], Values[i]);
                }
                else for (int i = 0; i < fields.Length; i++) AssertSlot(fields[i].GetValue(original), Values[i]);
            }
            private static void AssertSlot(object actual, object expected)
            {
                if (expected == null) { Assert.That(actual, Is.Null); return; }
                Assert.That(actual, Is.Not.Null);
                Assert.That(actual.GetType(), Is.EqualTo(expected.GetType()));
                if (expected is double)
                    Assert.That(BitConverter.DoubleToInt64Bits((double)actual), Is.EqualTo(BitConverter.DoubleToInt64Bits((double)expected)));
                else if (expected is float)
                    CollectionAssert.AreEqual(BitConverter.GetBytes((float)expected), BitConverter.GetBytes((float)actual));
                else if (expected.GetType().IsValueType) Assert.That(actual, Is.EqualTo(expected));
                else Assert.That(actual, Is.SameAs(expected));
            }
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceComparer Instance = new ReferenceComparer();
            bool IEqualityComparer<object>.Equals(object left, object right) => ReferenceEquals(left, right);
            int IEqualityComparer<object>.GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }
    }
}
