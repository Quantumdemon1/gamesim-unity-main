using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Actual Unity-dependent readers and built controls. This fixture is not a pure harness or
    /// a complete season lifecycle; native execution is required before claiming it passed.
    /// </summary>
    public sealed class UnifiedSafetySurfaceTests
    {
        [TestCase(39, false)]
        [TestCase(40, true)]
        public void ActualDealCeilingIncludesCanonicalHistoricalNpcAndDeclinedDealRows(int total, bool expected)
        {
            var s = State(); string who = Other(s);
            for (int i = 0; i < total - 1; i++)
            {
                var row = Deal(s, "canonical-" + i, i % 2 == 0 ? s.playerId : who, i % 2 == 0 ? who : Other(s, 1));
                row.status = i % 3 == 0 ? DealStatus.Declined : DealStatus.Expired;
                s.unifiedCommitments.Add(row);
            }
            s.deals.Add(new DealState { id = "legacy-final", type = DealKind.FinalTwo, proposerId = who,
                recipientId = Other(s, 1), status = DealStatus.Broken, week = 1 });
            for (int i = 0; i < 4; i++) s.unifiedCommitments.Add(Promise(s, "promise-" + i, s.playerId, who));
            string before = Json(s);
            Assert.That(CommitmentReferences.DealCount(s), Is.EqualTo(total));
            Assert.That(EpisodeDirector.PastTheDealCeiling(s), Is.EqualTo(expected));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void DealCeilingDoesNotOpenBeforeTheRecordedDealRulesStartOrForMissingState()
        {
            var s = State(); s.dealRulesStartWeek = s.week + 1;
            for (int i = 0; i < PlayerDeals.PlayerDealCeiling; i++) s.unifiedCommitments.Add(Deal(s, "deal-" + i, s.playerId, Other(s)));
            string before = Json(s);
            Assert.That(EpisodeDirector.PastTheDealCeiling(s), Is.False);
            Assert.That(EpisodeDirector.PastTheDealCeiling(null), Is.False);
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(39, false)]
        [TestCase(40, true)]
        public void LegacyDealCeilingKeepsItsOriginalHistoricalCount(int total, bool expected)
        {
            var s = State(); s.unifiedCommitmentRulesVersion = 0;
            for (int i = 0; i < total; i++) s.deals.Add(new DealState { id = "legacy-" + i,
                type = DealKind.SafetyAgreement, proposerId = s.playerId, recipientId = Other(s), status = DealStatus.Declined });
            string before = Json(s);
            Assert.That(EpisodeDirector.PastTheDealCeiling(s), Is.EqualTo(expected));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void ActualProfileRecordsIncludeCanonicalPromiseInSourceOrderWithCreationWeekAndNoMirrors()
        {
            var s = State(); string who = Other(s); string first = s.Find(who).name.Split(' ')[0];
            s.promises.Add(new PromiseState { id = "legacy-information", fromId = s.playerId, toId = who,
                kind = PromiseKind.Information, status = PromiseStatus.Fulfilled, week = 2, settledWeek = 3 });
            s.unifiedCommitments.Add(Promise(s, "canonical-safety", s.playerId, who, 1));
            string before = Json(s); var lines = ProfileRecords(s, who, first);
            Assert.That(lines.Skip(1), Is.EqualTo(new[] {
                "Week 2: You promised " + first + " information · kept",
                "Week 1: You promised " + first + " safety · still standing",
            }));
            lines.Clear(); Assert.That(ProfileRecords(s, who, first), Has.Count.EqualTo(3));
            Assert.That(s.promises.Count, Is.EqualTo(1)); Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void ActualProfileRecordsBoundBothPromiseDirectionsButExcludeOtherPairsAndDealPolicy()
        {
            var s = State(); string who = Other(s); string first = s.Find(who).name.Split(' ')[0];
            s.unifiedCommitments.Add(Promise(s, "incoming", who, s.playerId));
            s.unifiedCommitments.Add(Promise(s, "private-promise", who, Other(s, 1)));
            s.unifiedCommitments.Add(Deal(s, "own-deal-not-promise", s.playerId, who));
            s.promises.Add(new PromiseState { id = "private-final", fromId = who, toId = Other(s, 1),
                kind = PromiseKind.FinalTwo, status = PromiseStatus.Active, week = s.week });
            string before = Json(s); var lines = ProfileRecords(s, who, first);
            Assert.That(lines.Skip(1), Is.EqualTo(new[] { "Week 3: " + first + " promised you safety · still standing" }));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void ActualProfileRecordsCarryEvaluatedBrokenStatusWithoutInventingSettlementWeekLabels()
        {
            var s = State(); string who = Other(s); string first = s.Find(who).name.Split(' ')[0];
            s.unifiedCommitments.Add(Promise(s, "breach", s.playerId, who, 2)); Break(s, who);
            string before = Json(s); var lines = ProfileRecords(s, who, first);
            Assert.That(lines.Skip(1), Is.EqualTo(new[] { "Week 2: You promised " + first + " safety · broken" }));
            Assert.That(s.unifiedCommitments.Single().settledWeek, Is.EqualTo(3));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void BuiltRelationshipDetailsShowActualCanonicalPromiseAndBindingDealOnly()
        {
            var s = State(); string who = Other(s);
            s.unifiedCommitments.Add(Promise(s, "promise", s.playerId, who));
            s.unifiedCommitments.Add(Deal(s, "deal", s.playerId, who));
            s.unifiedCommitments.Add(Deal(s, "declined", s.playerId, who));
            s.unifiedCommitments.Last().status = DealStatus.Declined;
            string before = Json(s); string copy = RelationshipCopy(s, who);
            Assert.That(copy, Does.Contain("You promised safety · Active"));
            Assert.That(copy.Split('\n').Count(line => line.Contains(DealKind.Title(DealKind.SafetyAgreement))), Is.EqualTo(1));
            Assert.That(copy, Does.Not.Contain("No deals, promises or alliances between you."));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void BuiltRelationshipDetailsDoNotLeakPrivateOtherPairPromisesDealsTrustOrMemories()
        {
            var s = State(); string who = Other(s);
            s.unifiedCommitments.Add(Promise(s, "own", s.playerId, who));
            string baseline = RelationshipCopy(s, who);
            s.unifiedCommitments.Add(Promise(s, "other-pair", who, Other(s, 1)));
            s.unifiedCommitments.Add(Deal(s, "other-deal", who, Other(s, 1)));
            s.promises.Add(new PromiseState { id = "secret-final", fromId = who, toId = Other(s, 1),
                kind = PromiseKind.FinalTwo, status = PromiseStatus.Active, week = s.week });
            s.memories.Add(new MemoryState { ownerId = who, subjectId = s.playerId, week = s.week,
                isPrivate = true, text = "SECRET_PRIVATE_MEMORY" });
            foreach (var edge in s.relationships.Where(row => row.fromId != s.playerId)) edge.score = -92;
            string before = Json(s);
            Assert.That(RelationshipCopy(s, who), Is.EqualTo(baseline));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        /// <summary>
        /// The built details' "between you" lines tell a houseguest's vote promise's ending as their notes tell it (vote family
        /// V6's review, finding 2; <see cref="KnownBallots.PromiseOutcomeKnown"/>, decision 4): "unresolved" while the ballot that
        /// ended it is hidden from the player, the ending once they know that ballot - before mode 2
        /// (<see cref="DecisionContextTests.LegacyVoteWord"/>) and in mode 2's flip pair (<see cref="ModeTwoReaderSweep.Flip"/>).
        /// </summary>
        [Test]
        public void BuiltRelationshipDetailsTellAVotePromisesEndingOnlyWhereItsBallotIsKnown()
        {
            const string Word = " promised you a vote · ";
            var legacy = DecisionContextTests.LegacyVoteWord();
            Assert.That(RelationshipCopy(legacy.blind, legacy.promiser), Does.Contain(Word + KnownBallots.Unresolved).And.Not.Contain(Word + "Broken"));
            Assert.That(RelationshipCopy(legacy.told, legacy.promiser), Does.Contain(Word + "Broken").And.Not.Contain(Word + KnownBallots.Unresolved));

            var blind = ModeTwoReaderSweep.Flip(false, word: true);
            foreach (var s in new[] { blind.Kept, blind.Broken })
            {
                string before = Json(s);
                Assert.That(RelationshipCopy(s, blind.PartnerId), Does.Contain(Word + KnownBallots.Unresolved).And.Not.Contain(Word + "Broken"),
                    "Mode 2, the ballot hidden.");
                Assert.That(Json(s), Is.EqualTo(before));
            }
            var told = ModeTwoReaderSweep.Flip(true, word: true);
            foreach (var s in new[] { told.Kept, told.Broken })
                Assert.That(RelationshipCopy(s, told.PartnerId), Does.Contain(Word + "Broken"), "Mode 2, the ballot told and judged.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DecisionCandidateUsesOwnCanonicalPromiseDirectionAndSourceStatus(bool incoming)
        {
            var s = State(); string who = Other(s);
            s.unifiedCommitments.Add(Promise(s, "own-promise", incoming ? who : s.playerId, incoming ? s.playerId : who));
            string before = Json(s); var read = DecisionContext.ForCandidate(s, who);
            Assert.That(read.Promises, Is.EqualTo((incoming ? "Promised to you: " : "You promised: ") + "safety · active"));
            Assert.That(read.Deals, Is.Null, "A unilateral promise cannot manufacture a reciprocal deal.");
            Assert.That(s.promises, Is.Empty); Assert.That(s.deals, Is.Empty);
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void DecisionCandidateCarriesEvaluatedBrokenStatusWithoutInventingADeal()
        {
            var s = State(); string who = Other(s);
            s.unifiedCommitments.Add(Promise(s, "broken", s.playerId, who)); Break(s, who);
            string before = Json(s);
            var read = DecisionContext.ForCandidate(s, who);
            Assert.That(read.Promises, Is.EqualTo("You promised: safety · broken"));
            Assert.That(read.Deals, Is.Null); Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void DecisionCandidateKeepsCanonicalAndLegacyProvenanceOrderWithoutClaimingInterleavedChronology()
        {
            var s = State(); string who = Other(s);
            s.promises.Add(Legacy(s, "legacy-vote", who, PromiseKind.Vote, PromiseStatus.Active, 3, 0));
            s.promises.Add(Legacy(s, "legacy-final", who, PromiseKind.FinalTwo, PromiseStatus.Fulfilled, 1, 2));
            s.unifiedCommitments.Add(Promise(s, "canonical-old", s.playerId, who, 1));
            var incoming = Promise(s, "canonical-incoming", who, s.playerId, 2); incoming.status = DealStatus.Expired;
            s.unifiedCommitments.Add(incoming); string before = Json(s);
            Assert.That(DecisionContext.ForCandidate(s, who).Promises,
                Is.EqualTo("Promised to you: safety · expired\nYou promised: safety · active\n+2 other promises between you"));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DecisionCandidateLegacyCopyAndOrderingRemainExactWithOrWithoutEnabledEmptyAuthority(bool enabled)
        {
            var s = State(); if (!enabled) s.unifiedCommitmentRulesVersion = 0; string who = Other(s);
            var first = Legacy(s, "first", who, PromiseKind.Vote, PromiseStatus.Active, 3, 0);
            var last = Legacy(s, "last", who, PromiseKind.FinalTwo, PromiseStatus.Fulfilled, 1, 2);
            last.fromId = who; last.toId = s.playerId;
            s.promises.AddRange(new[] { first, last }); string before = Json(s);
            Assert.That(DecisionContext.ForCandidate(s, who).Promises,
                Is.EqualTo("Promised to you: final two · fulfilled\nYou promised: a vote · active"));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void PrivateOtherPairsCannotChangeCandidateCopyOrKnownEventSummary()
        {
            var s = State(); string who = Other(s);
            s.unifiedCommitments.Add(Promise(s, "direct", s.playerId, who));
            var before = DecisionContext.ForCandidate(s, who); string eventBefore = DecisionContext.KnownEventSummary(s);
            s.unifiedCommitments.Add(Promise(s, "private-safety", who, Other(s, 1)));
            s.unifiedCommitments.Add(Deal(s, "private-deal", who, Other(s, 1)));
            s.promises.Add(new PromiseState { id = "private-final", fromId = who, toId = Other(s, 1),
                kind = PromiseKind.FinalTwo, status = PromiseStatus.Active, week = s.week });
            s.events.Add(new EpisodeEvent { kind = "private-plan", text = "PRIVATE_INFORMATION_MARKER",
                audienceIds = new List<string> { who }, week = s.week });
            foreach (var edge in s.relationships.Where(row => row.fromId != s.playerId)) edge.score = -93;
            string image = Json(s); var after = DecisionContext.ForCandidate(s, who);
            Assert.That(after.Promises, Is.EqualTo(before.Promises)); Assert.That(after.Deals, Is.EqualTo(before.Deals));
            Assert.That(after.Relationship, Is.EqualTo(before.Relationship)); Assert.That(after.Record, Is.EqualTo(before.Record));
            Assert.That(DecisionContext.KnownEventSummary(s), Is.EqualTo(eventBefore));
            Assert.That(Json(s), Is.EqualTo(image));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DecisionDealPolicyStaysInDealsAndDoesNotBecomeAPromise(bool incoming)
        {
            var s = State(); string who = Other(s);
            s.unifiedCommitments.Add(Deal(s, "deal", incoming ? who : s.playerId, incoming ? s.playerId : who));
            string before = Json(s); var read = DecisionContext.ForCandidate(s, who);
            Assert.That(read.Promises, Is.EqualTo("No promises recorded between you."));
            Assert.That(read.Deals, Does.Contain("Safety deal").And.Contain("agreed"));
            Assert.That(read.Deals, Does.Contain(incoming ? "they offered" : "you proposed"));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void DecisionPreviewAndRepeatedDialogueAreDetachedAndNeverAdvanceSavedState()
        {
            var s = State(); string who = Other(s);
            s.unifiedCommitments.Add(Promise(s, "promise", s.playerId, who));
            s.unifiedCommitments.Add(Deal(s, "deal", s.playerId, who));
            string before = Json(s); var expected = DecisionContext.ForCandidate(s, who);
            expected.Character.name = "Not a saved rename";
            for (int i = 0; i < 12; i++)
            {
                var read = DecisionContext.ForCandidate(s, who);
                Assert.That(read.Character, Is.Not.SameAs(s.Find(who)));
                Assert.That(read.Character.name, Is.EqualTo(s.Find(who).name));
                Assert.That(read.Promises, Is.EqualTo(expected.Promises)); Assert.That(read.Deals, Is.EqualTo(expected.Deals));
                foreach (EpisodeCommandKind action in Enum.GetValues(typeof(EpisodeCommandKind))) HouseDialogue.Response(s, who, action);
            }
            Assert.That(Json(s), Is.EqualTo(before));
        }

        private static EpisodeState State()
        {
            var s = ContentCatalog.Create(17); s.week = 3; s.hohId = s.playerId; s.dealRulesStartWeek = 1;
            s.unifiedCommitmentRulesVersion = 1; s.commitmentRulesStartWeek = 1; s.strategyRulesStartWeek = 1; return s;
        }

        private static string Other(EpisodeState s, int index = 0) => s.contestants.Where(c => c.id != s.playerId).Skip(index).First().id;

        private static PromiseState Legacy(EpisodeState s, string id, string who, PromiseKind kind, PromiseStatus status, int created, int settled)
            => new PromiseState { id = id, fromId = s.playerId, toId = who, kind = kind, status = status,
                week = created, settledWeek = settled, expiresWeek = 0 };

        private static UnifiedCommitmentState Promise(EpisodeState s, string id, string from, string to, int created = 3) => new UnifiedCommitmentState {
            id = id, kind = UnifiedCommitments.Safety, sourcePolicy = UnifiedCommitments.PromisePolicy, origin = UnifiedCommitments.StoryPromise,
            makerId = from, beneficiaryId = to, createdWeek = created, expiresWeek = created + 1,
            status = DealStatus.Active, trustImpact = DealTrust.Medium,
        };

        private static UnifiedCommitmentState Deal(EpisodeState s, string id, string from, string to) => new UnifiedCommitmentState {
            id = id, kind = UnifiedCommitments.Safety, sourcePolicy = UnifiedCommitments.DealPolicy, origin = UnifiedCommitments.StoryDeal,
            makerId = from, beneficiaryId = to, createdWeek = s.week, expiresWeek = s.week,
            reciprocal = true, status = DealStatus.Active, trustImpact = DealTrust.Medium,
        };

        private static void Break(EpisodeState s, string who)
        {
            var evaluation = UnifiedCommitments.EvaluateNomination(s, "nomination", s.playerId, new[] { who });
            Assert.That(evaluation.Changes, Is.Not.Empty);
            foreach (var change in evaluation.Changes)
                s.unifiedCommitments[s.unifiedCommitments.FindIndex(row => row.id == change.Record.id)] = change.Record.Clone();
        }

        private static List<string> ProfileRecords(EpisodeState s, string who, string first)
        {
            var host = new GameObject("Isolated profile reader"); host.SetActive(false);
            try
            {
                var director = host.AddComponent<EpisodeDirector>();
                var method = typeof(EpisodeDirector).GetMethod("ProfileRecords", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.That(method, Is.Not.Null, "Call the actual profile reader, not a test-only DTO seam.");
                try { return (List<string>)method.Invoke(director, new object[] { s, s.Find(who), first }); }
                catch (TargetInvocationException error) { throw error.InnerException ?? error; }
            }
            finally { Object.DestroyImmediate(host); }
        }

        private static string RelationshipCopy(EpisodeState s, string who)
        {
            RelationshipWeb.ClearSelection();
            var host = new GameObject("Isolated relationship viewport", typeof(RectTransform), typeof(VerticalLayoutGroup));
            try
            {
                var viewport = (RectTransform)host.transform; viewport.sizeDelta = new Vector2(1160f, 490f);
                var settingsType = Type.GetType("TMPro.TMP_Settings, Unity.TextMeshPro", true);
                var fontProperty = settingsType.GetProperty("defaultFontAsset", BindingFlags.Public | BindingFlags.Static);
                Assert.That(fontProperty, Is.Not.Null, "Read the real installed TMP project setting, not a font substitute.");
                var font = fontProperty.GetValue(null, null);
                Assert.That(font, Is.Not.Null, "The actual project font is required; do not stub the built control.");
                RelationshipWeb.Select(s, who);
                var build = typeof(RelationshipWeb).GetMethod("Build", BindingFlags.Public | BindingFlags.Static);
                Assert.That(build, Is.Not.Null, "Invoke the actual installed relationship builder with its real font.");
                RectTransform root;
                try
                {
                    root = (RectTransform)build.Invoke(null, new object[] { viewport, s, 1f, font,
                        new Func<string, Texture>(_ => null), new Action<string>(_ => { }), 490f, 1160f, null });
                }
                catch (TargetInvocationException error) { throw error.InnerException ?? error; }
                var column = root.GetComponentsInChildren<RectTransform>(true).Single(rect => rect.name == RelationshipWeb.ColumnName);
                Assert.That(root.GetComponentsInChildren<Button>(true).Length, Is.GreaterThanOrEqualTo(s.Active.Count()));
                var textType = Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro", true);
                var textProperty = textType.GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                Assert.That(textProperty, Is.Not.Null, "Read the actual rendered TMP labels, not a parallel display model.");
                return string.Join("\n", column.GetComponentsInChildren(textType, true)
                    .Select(label => (string)textProperty.GetValue(label, null)));
            }
            finally { Object.DestroyImmediate(host); RelationshipWeb.ClearSelection(); }
        }

        private static string Json(object value) => JsonConvert.SerializeObject(value);
    }
}
