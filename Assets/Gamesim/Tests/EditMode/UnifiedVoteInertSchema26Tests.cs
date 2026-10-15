using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>Current-producer compatibility for the inert26 leaves; not retained historical26 or native Vote evidence.</summary>
    public sealed class UnifiedVoteInertSchema26Tests
    {
        [TestCase(false)] [TestCase(true)]
        public void ActualFactoriesCreateOnlyEmptyInertStorage(bool builder)
        {
            var s = builder ? SeasonBuilder.Create(new SeasonBuilder.Choice(), 2505) : ContentCatalog.Create(2505);
            Accepted(s); Assert.That(s.schemaVersion, Is.EqualTo(28));
            Assert.That(s.unifiedCommitmentRulesVersion, Is.Zero); Assert.That(s.unifiedHearingRulesVersion, Is.Zero);
            Assert.That(s.unifiedCommitments, Is.Empty); Assert.That(s.unifiedVoteReveals, Is.Not.Null.And.Empty);
            var clone = s.Clone(); Assert.That(clone.unifiedVoteReveals, Is.Not.SameAs(s.unifiedVoteReveals));
            clone.unifiedVoteReveals.Add(null); Assert.That(s.unifiedVoteReveals, Is.Empty); Refused(clone);
        }

        [TestCase(false)] [TestCase(true)]
        public void ActualPublicSafetyWriterKeepsBothExtensionsNullAndArchiveEmpty(bool hearing)
        {
            var s = Promise(hearing); Accepted(s);
            var row = s.unifiedCommitments.Single(r => r.origin == UnifiedCommitments.PlayerPromise);
            Assert.That(row.kind, Is.EqualTo(UnifiedCommitments.Safety));
            Assert.That(row.targetId, Is.Null); Assert.That(row.subtype, Is.Null); Assert.That(s.unifiedVoteReveals, Is.Empty);
            Assert.That(s.unifiedHearingRulesVersion, Is.EqualTo(hearing ? 1 : 0));
            Assert.That(s.revision, Is.EqualTo(1)); Assert.That(s.acceptedCommandIds, Has.Count.EqualTo(1));
            Assert.That(s.promises.Any(p => p.kind == PromiseKind.Safety), Is.False);
        }

        [TestCase("targetId", "")] [TestCase("targetId", " ")] [TestCase("targetId", "known")]
        [TestCase("subtype", "")] [TestCase("subtype", " ")] [TestCase("subtype", "vote_save")]
        public void SafetyRejectsEveryNonNullFutureScalarWithoutRepair(string field, string value)
        {
            var s = Promise(false); Accepted(s);
            var row = s.unifiedCommitments.Single(r => r.origin == UnifiedCommitments.PlayerPromise);
            typeof(UnifiedCommitmentState).GetField(field).SetValue(row, value == "known" ? row.beneficiaryId : value);
            Refused(s);
        }

        [TestCase(0, "null")] [TestCase(0, "null-frame")] [TestCase(0, "frame")]
        [TestCase(1, "null")] [TestCase(1, "null-frame")] [TestCase(1, "frame")]
        [TestCase(2, "null")] [TestCase(2, "null-frame")] [TestCase(2, "frame")]
        public void AllSupportedRecordedModesRefuseAnyNonemptyOrNullArchive(int mode, string defect)
        {
            // Fixture mode2 means canonical Safety1/hearing1, NEVER proposed Vote mode2.
            var s = mode == 0 ? ContentCatalog.Create(2505) : Promise(mode == 2); Accepted(s);
            if (defect == "null") s.unifiedVoteReveals = null;
            else s.unifiedVoteReveals.Add(defect == "null-frame" ? null : new UnifiedVoteRevealState());
            Refused(s);
        }

        /// <summary>
        /// Flipped at vote family V6 (this was "proposed Vote authority still cannot be constructed"): mode 2 is the unified vote
        /// rules a fresh season plays, judged by its own complete core and never converted to. A season relabelled 2 by hand is
        /// held to that core as it stands: this one, canonical Safety only and before any reveal, is field for field a mode-2
        /// season, and is taken. Nothing in the game relabels a season (the lead's decision D6); 3 stays unknown and refused.
        /// </summary>
        [TestCase(false)] [TestCase(true)]
        public void ASeasonRelabelledTwoIsJudgedByTheVoteCoreAndAnUnknownModeIsRefused(bool hearing)
        {
            var s = Promise(hearing); Accepted(s);
            var two = s.Clone(); two.unifiedCommitmentRulesVersion = 2;
            Accepted(two);
            Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(two, out var error), Is.True, "The complete Vote core takes it. " + error);
            var three = s.Clone(); three.unifiedCommitmentRulesVersion = 3; Refused(three);
        }

        [Test]
        public void FutureStorageLeavesHaveOnlyTheReviewedFields()
        {
            Assert.That(typeof(UnifiedVoteRevealState).GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name),
                Is.EquivalentTo(new[] { "week", "ballots" }));
            Assert.That(typeof(UnifiedVoteBallotState).GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name),
                Is.EquivalentTo(new[] { "voterId", "targetId" }));
            Assert.That(typeof(EpisodeState).GetField("unifiedVoteReveals").FieldType, Is.EqualTo(typeof(List<UnifiedVoteRevealState>)));
        }

        [Test]
        public void FutureLeafCloneIsDeepButDoesNotClaimAnAcceptedSavedReveal()
        {
            var original = new UnifiedVoteRevealState { week = 7, ballots = new List<UnifiedVoteBallotState> {
                new UnifiedVoteBallotState { voterId = "voter", targetId = "target" } } };
            var clone = original.Clone(); Assert.That(clone, Is.Not.SameAs(original));
            Assert.That(clone.ballots, Is.Not.SameAs(original.ballots)); Assert.That(clone.ballots[0], Is.Not.SameAs(original.ballots[0]));
            clone.week = 8; clone.ballots[0].voterId = "changed"; clone.ballots[0].targetId = "other"; clone.ballots.Add(null);
            Assert.That(original.week, Is.EqualTo(7)); Assert.That(original.ballots, Has.Count.EqualTo(1));
            Assert.That(original.ballots[0].voterId, Is.EqualTo("voter")); Assert.That(original.ballots[0].targetId, Is.EqualTo("target"));
        }

        [TestCase("root-null")] [TestCase("null-frame")] [TestCase("null-ballots")] [TestCase("null-ballot")] [TestCase("complete")]
        public void EpisodeClonePreservesCorruptNullsAndDetachesAllFutureLayers(string shape)
        {
            var s = ContentCatalog.Create(2505); Accepted(s);
            if (shape == "root-null") s.unifiedVoteReveals = null;
            else if (shape == "null-frame") s.unifiedVoteReveals.Add(null);
            else s.unifiedVoteReveals.Add(new UnifiedVoteRevealState { week = 1, ballots = shape == "null-ballots" ? null
                : new List<UnifiedVoteBallotState> { shape == "null-ballot" ? null : new UnifiedVoteBallotState { voterId = s.playerId, targetId = s.Active.First(c => !c.isPlayer).id } } });
            string before = Json(s); var clone = s.Clone(); Assert.That(Json(clone), Is.EqualTo(before));
            if (shape == "root-null") Assert.That(clone.unifiedVoteReveals, Is.Null);
            else
            {
                Assert.That(clone.unifiedVoteReveals, Is.Not.SameAs(s.unifiedVoteReveals));
                if (shape == "null-frame") Assert.That(clone.unifiedVoteReveals[0], Is.Null);
                else
                {
                    Assert.That(clone.unifiedVoteReveals[0], Is.Not.SameAs(s.unifiedVoteReveals[0]));
                    if (shape == "null-ballots") Assert.That(clone.unifiedVoteReveals[0].ballots, Is.Null);
                    else
                    {
                        Assert.That(clone.unifiedVoteReveals[0].ballots, Is.Not.SameAs(s.unifiedVoteReveals[0].ballots));
                        if (shape == "null-ballot") Assert.That(clone.unifiedVoteReveals[0].ballots[0], Is.Null);
                        else { Assert.That(clone.unifiedVoteReveals[0].ballots[0], Is.Not.SameAs(s.unifiedVoteReveals[0].ballots[0])); clone.unifiedVoteReveals[0].ballots[0].targetId = "changed"; }
                    }
                }
                clone.unifiedVoteReveals.Add(null);
            }
            Assert.That(Json(s), Is.EqualTo(before)); Refused(s); Refused(clone);
        }

        [Test]
        public void CanonicalScalarCloneCopiesFutureFieldsWithoutMakingThemLawfulSafety()
        {
            var s = Promise(false); var row = s.unifiedCommitments.Single(r => r.origin == UnifiedCommitments.PlayerPromise);
            row.targetId = row.beneficiaryId; row.subtype = "vote_save"; var clone = row.Clone();
            Assert.That(clone.targetId, Is.EqualTo(row.targetId)); Assert.That(clone.subtype, Is.EqualTo(row.subtype));
            clone.targetId = null; clone.subtype = null; Assert.That(row.targetId, Is.Not.Null); Assert.That(row.subtype, Is.EqualTo("vote_save")); Refused(s);
        }

        private static EpisodeState Promise(bool hearing)
        {
            var s = ContentCatalog.Create(2505);
            Assert.That(s.unifiedCommitments, Is.Empty); Assert.That(s.promises.Any(p => p.kind == PromiseKind.Safety), Is.False);
            s.competitionRulesVersion = CompetitionRules.Current; s.haveNotRulesStartWeek = s.strategyRulesStartWeek = 1;
            EpisodeEngine.EnableStory(s); EpisodeEngine.EnableRead(s); EpisodeEngine.EnableLevers(s); EpisodeEngine.EnableWeek(s);
            EpisodeEngine.EnableEconomy(s); EpisodeEngine.EnableAgency(s); EpisodeEngine.EnableFinale(s); EpisodeEngine.EnableCommitments(s);
            s.unifiedCommitmentRulesVersion = 1; s.unifiedHearingRulesVersion = hearing ? 1 : 0;
            var engine = new EpisodeEngine(s); string before = Json(s);
            var command = new EpisodeCommand { id = "inert26-promise", actorId = s.playerId, expectedRevision = s.revision,
                expectedPhase = s.phase, kind = EpisodeCommandKind.PromiseSafety, targetId = s.Active.First(c => !c.isPlayer).id };
            var result = engine.Apply(command); Assert.That(result.accepted, Is.True, result.reason); Assert.That(result.duplicate, Is.False);
            Assert.That(Json(s), Is.EqualTo(before)); Assert.That(Json(result.state), Is.EqualTo(Json(engine.Snapshot))); return result.state;
        }
        private static void Accepted(EpisodeState s)
        { string before = Json(s); Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error); Assert.That(Json(s), Is.EqualTo(before)); }
        private static void Refused(EpisodeState s)
        {
            string before = Json(s); Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.False);
            Assert.That(error, Is.Not.Empty); Assert.That(error, Does.Not.Contain("Unsupported episode schema"));
            Assert.Throws<ArgumentException>(() => new EpisodeEngine(s)); Assert.That(Json(s), Is.EqualTo(before));
        }
        private static string Json(EpisodeState s) => JsonConvert.SerializeObject(typeof(EpisodeState)
            .GetFields(BindingFlags.Public | BindingFlags.Instance).ToDictionary(f => f.Name, f => f.GetValue(s)));
    }
}
