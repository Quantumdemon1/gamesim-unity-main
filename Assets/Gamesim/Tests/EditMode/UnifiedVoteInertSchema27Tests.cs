using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>Pure inert27 DTO/public-core controls; no disk, native or enabled Vote evidence.</summary>
    public sealed class UnifiedVoteInertSchema27Tests
    {
        [TestCase(false)] [TestCase(true)]
        public void ActualFactoriesRetainDisabledEmptyStorageAtCurrent27(bool builder)
        {
            var s = builder ? SeasonBuilder.Create(new SeasonBuilder.Choice(), 2505) : ContentCatalog.Create(2505);
            Accepted(s); Assert.That(s.schemaVersion, Is.EqualTo(28));
            Assert.That(s.unifiedCommitmentRulesVersion, Is.Zero); Assert.That(s.unifiedHearingRulesVersion, Is.Zero);
            Assert.That(s.unifiedCommitments, Is.Not.Null.And.Empty); Assert.That(s.unifiedVoteReveals, Is.Not.Null.And.Empty);
            Assert.That(s.unifiedHearingEvidence, Is.Empty); Assert.That(s.unifiedHearingReceipts, Is.Empty);
        }

        [TestCase(false, false)] [TestCase(true, false)] [TestCase(false, true)] [TestCase(true, true)]
        public void ActualPublicSafetyOwnersNeverBindFutureVoteChronology(bool hearing, bool deal)
        {
            var s = Write(hearing, deal); Accepted(s); Assert.That(s.schemaVersion, Is.EqualTo(28));
            var row = s.unifiedCommitments.Single(r => r.origin == (deal ? UnifiedCommitments.PlayerDeal : UnifiedCommitments.PlayerPromise));
            Assert.That(row.kind, Is.EqualTo(UnifiedCommitments.Safety)); Assert.That(row.voteBindingWeek, Is.Zero);
            Assert.That(row.voteFirstRevealWeek, Is.Zero); Assert.That(row.targetId, Is.Null); Assert.That(row.subtype, Is.Null);
            Assert.That(s.unifiedVoteReveals, Is.Empty); Assert.That(s.acceptedCommandIds, Is.Not.Empty);
            Assert.That(s.unifiedHearingRulesVersion, Is.EqualTo(hearing ? 1 : 0));
            Assert.That(s.promises.Any(p => p.kind == PromiseKind.Safety), Is.False);
            Assert.That(s.deals.Any(d => d.type == DealKind.SafetyAgreement), Is.False);
        }

        [TestCase("voteBindingWeek", -1, false)] [TestCase("voteBindingWeek", 1, false)]
        [TestCase("voteBindingWeek", 2, false)] [TestCase("voteBindingWeek", int.MinValue, false)] [TestCase("voteBindingWeek", int.MaxValue, false)]
        [TestCase("voteFirstRevealWeek", -1, false)] [TestCase("voteFirstRevealWeek", 1, false)]
        [TestCase("voteFirstRevealWeek", 2, false)] [TestCase("voteFirstRevealWeek", int.MinValue, false)] [TestCase("voteFirstRevealWeek", int.MaxValue, false)]
        [TestCase("voteBindingWeek", -1, true)] [TestCase("voteBindingWeek", 1, true)]
        [TestCase("voteBindingWeek", 2, true)] [TestCase("voteBindingWeek", int.MinValue, true)] [TestCase("voteBindingWeek", int.MaxValue, true)]
        [TestCase("voteFirstRevealWeek", -1, true)] [TestCase("voteFirstRevealWeek", 1, true)]
        [TestCase("voteFirstRevealWeek", 2, true)] [TestCase("voteFirstRevealWeek", int.MinValue, true)] [TestCase("voteFirstRevealWeek", int.MaxValue, true)]
        public void EveryNonzeroChronologyScalarRefusesWithoutNormalization(string field, int value, bool hearing)
        {
            var s = Write(hearing, false); var row = s.unifiedCommitments.Single(r => r.origin == UnifiedCommitments.PlayerPromise);
            Assert.That(row.createdWeek, Is.EqualTo(1), "The actual current/future marker controls are week1/week2, not assigned chronology.");
            typeof(UnifiedCommitmentState).GetField(field).SetValue(row, value); Refused(s);
        }

        [TestCase("voteBindingWeek")] [TestCase("voteFirstRevealWeek")]
        public void RowCloneCopiesInvalidChronologyAndKeepsTheOriginalDetached(string field)
        {
            var s = Write(false, false); var row = s.unifiedCommitments.Single(r => r.origin == UnifiedCommitments.PlayerPromise);
            var member = typeof(UnifiedCommitmentState).GetField(field); member.SetValue(row, 7);
            string before = Json(s); var clone = row.Clone(); Assert.That(clone, Is.Not.SameAs(row));
            Assert.That((int)member.GetValue(clone), Is.EqualTo(7)); member.SetValue(clone, 0);
            Assert.That((int)member.GetValue(row), Is.EqualTo(7)); Assert.That(Json(s), Is.EqualTo(before)); Refused(s);
        }

        [TestCase(false)] [TestCase(true)]
        public void EpisodeClonePreservesBothInvalidScalarsWithoutRepair(bool hearing)
        {
            var s = Write(hearing, false); var row = s.unifiedCommitments.Single(r => r.origin == UnifiedCommitments.PlayerPromise);
            row.voteBindingWeek = -4; row.voteFirstRevealWeek = 6; string before = Json(s); var clone = s.Clone();
            Assert.That(Json(clone), Is.EqualTo(before)); Assert.That(clone.unifiedCommitments, Is.Not.SameAs(s.unifiedCommitments));
            var copied = clone.unifiedCommitments.Single(r => r.id == row.id); Assert.That(copied, Is.Not.SameAs(row));
            Refused(clone); copied.voteBindingWeek = copied.voteFirstRevealWeek = 0;
            Assert.That(Json(s), Is.EqualTo(before)); Refused(s);
        }

        [TestCase(-1, false)] [TestCase(2, false)] [TestCase(3, false)]
        [TestCase(-1, true)] [TestCase(2, true)] [TestCase(3, true)]
        public void NewStorageHeaderNeverEnablesUnknownFamilyAuthority(int authority, bool hearing)
        { var s = Write(hearing, false); s.unifiedCommitmentRulesVersion = authority; Refused(s); }

        [TestCase(false, false)] [TestCase(false, true)] [TestCase(true, false)] [TestCase(true, true)]
        public void NonemptyOrNullRevealArchiveCannotBeLaunderedByZeroMarkers(bool hearing, bool nullArchive)
        {
            var s = Write(hearing, false); if (nullArchive) s.unifiedVoteReveals = null;
            else s.unifiedVoteReveals.Add(new UnifiedVoteRevealState { week = s.week, ballots = new List<UnifiedVoteBallotState>() });
            Refused(s);
        }

        [Test]
        public void TheAdditiveMarkerFieldsAreExactlyTwoIntegersNotPersistedReceipts()
        {
            var fields = typeof(UnifiedCommitmentState).GetFields(BindingFlags.Public | BindingFlags.Instance);
            Assert.That(fields, Has.Length.EqualTo(19));
            foreach (string name in new[] { "voteBindingWeek", "voteFirstRevealWeek" })
                Assert.That(fields.Single(f => f.Name == name).FieldType, Is.EqualTo(typeof(int)));
            Assert.That(typeof(UnifiedVoteRevealState).GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name),
                Is.EquivalentTo(new[] { "week", "ballots" }));
            Assert.That(typeof(UnifiedVoteBallotState).GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name),
                Is.EquivalentTo(new[] { "voterId", "targetId" }));
        }

        private static EpisodeState Fresh(uint seed, bool hearing)
        {
            var s = ContentCatalog.Create(seed); Assert.That(s.unifiedCommitments, Is.Empty);
            Assert.That(s.promises.Any(p => p.kind == PromiseKind.Safety), Is.False);
            Assert.That(s.deals.Any(d => d.type == DealKind.SafetyAgreement), Is.False);
            s.competitionRulesVersion = CompetitionRules.Current; s.haveNotRulesStartWeek = s.strategyRulesStartWeek = 1;
            EpisodeEngine.EnableStory(s); EpisodeEngine.EnableRead(s); EpisodeEngine.EnableLevers(s); EpisodeEngine.EnableWeek(s);
            EpisodeEngine.EnableEconomy(s); EpisodeEngine.EnableAgency(s); EpisodeEngine.EnableFinale(s); EpisodeEngine.EnableCommitments(s);
            // Test-only fresh selection on asserted empty stores, never a conversion of existing history.
            s.unifiedCommitmentRulesVersion = 1; s.unifiedHearingRulesVersion = hearing ? 1 : 0; Accepted(s); return s;
        }
        private static EpisodeState Write(bool hearing, bool deal)
        {
            // At most32 seeds/4 real commands per prospective partner branch. No roles,
            // phases, nominations, RNG or command receipts are assigned by the fixture.
            for (uint seed = 2505; seed < 2537; seed++)
            {
                var engine = new EpisodeEngine(Fresh(seed, hearing));
                if (deal)
                {
                    Step(engine, Command(engine.Snapshot, EpisodeCommandKind.Advance));
                    var compete = Command(engine.Snapshot, EpisodeCommandKind.Compete); compete.performance = 1; Step(engine, compete);
                    if (engine.Snapshot.hohId != engine.Snapshot.playerId) continue;
                    Step(engine, Command(engine.Snapshot, EpisodeCommandKind.Advance));
                    Assert.That(engine.Snapshot.phase, Is.EqualTo(EpisodePhase.Nomination));
                }
                var context = engine.Snapshot;
                foreach (var partner in context.Active.Where(c => !c.isPlayer))
                {
                    var branch = new EpisodeEngine(context); var command = Command(context, deal ? EpisodeCommandKind.ProposeDeal : EpisodeCommandKind.PromiseSafety);
                    command.targetId = partner.id; if (deal) command.text = DealKind.SafetyAgreement;
                    var result = Step(branch, command);
                    if (result.unifiedCommitments.Any(r => r.origin == (deal ? UnifiedCommitments.PlayerDeal : UnifiedCommitments.PlayerPromise))) return result;
                    Assert.That(deal, Is.True, "Only a genuine proposal can be declined or countered without creating the requested agreement.");
                }
            }
            Assert.Fail("No actual accepted public Safety owner in the declared bounded producer search."); return null;
        }
        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind) => new EpisodeCommand {
            id = "inert27-" + s.revision + "-" + kind, actorId = s.playerId, kind = kind,
            expectedRevision = s.revision, expectedPhase = s.phase };
        private static EpisodeState Step(EpisodeEngine engine, EpisodeCommand command)
        {
            var s = engine.Snapshot; string before = Json(s); var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True, result.reason); Assert.That(result.duplicate, Is.False);
            Assert.That(result.state.revision, Is.EqualTo(s.revision + 1)); Assert.That(result.state.acceptedCommandIds.Last(), Is.EqualTo(command.id));
            Assert.That(Json(s), Is.EqualTo(before)); Assert.That(Json(result.state), Is.EqualTo(Json(engine.Snapshot))); Accepted(result.state); return result.state;
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
