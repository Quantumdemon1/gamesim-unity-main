using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    public sealed class EpisodeVotingBlocTests
    {
        private static string FixturePath =>
#if UNITY_EDITOR
            Path.Combine(UnityEngine.Application.dataPath, "Gamesim", "Tests", "EditMode", "Fixtures", "WebLegalBlocWitness.json");
#else
            Path.Combine(AppContext.BaseDirectory, "WebLegalBlocWitness.json");
#endif
        private static JObject Fixture() => JObject.Parse(File.ReadAllText(FixturePath));
        private static T Read<T>(JToken token) => token.ToObject<T>(JsonSerializer.Create(new JsonSerializerSettings
            { ObjectCreationHandling = ObjectCreationHandling.Replace, MissingMemberHandling = MissingMemberHandling.Error }));
        private static string Json(object value) => JsonConvert.SerializeObject(value);
        private static EpisodeCommand Command(EpisodeState state, EpisodeCommandKind kind) => EpisodeEngineTests.Command(state, kind);
        private static void Apply(EpisodeEngine engine, EpisodeCommand command)
        {
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True, command.kind + " in " + command.expectedPhase + ": " + result.reason);
        }
        private static EpisodeEngine Witness(int? rulesWeek = null)
        {
            var fixture = Fixture(); var initial = ContentCatalog.Create((uint)fixture["seed"]);
            if (rulesWeek.HasValue) initial.blocRulesStartWeek = rulesWeek.Value;
            var engine = new EpisodeEngine(initial);
            foreach (var item in fixture["commands"]) Apply(engine, Read<EpisodeCommand>(item));
            return engine;
        }
        private static WebNativeEvictionRoundPlan Plan(EpisodeState state) => WebNativeEvictionRound.Evaluate(state,
            EpisodeEngine.Voters(state).Where(actor => !actor.isPlayer && !state.votes.Any(v => v.voterId == actor.id)).Select(actor => actor.id));
        private static WebVoteOptions CastOrderedOptions(EpisodeState state, string id)
        {
            var options = WebEvictionVoting.FromNative(state, id);
            options.nominees = state.contestants.Where(actor => state.nominees.Contains(actor.id))
                .Select(actor => options.nominees.Single(nominee => nominee.id == actor.id)).ToList();
            return options;
        }
        private static void AssertPrivateStateUnchanged(EpisodeState before, EpisodeState after)
        {
            var a = JObject.FromObject(before); var b = JObject.FromObject(after);
            foreach (string field in new[] { "revision", "nextSequence", "acceptedCommandIds", "events", "votes" })
            { a.Remove(field); b.Remove(field); }
            Assert.That(JToken.DeepEquals(a, b), Is.True, "Preparing private ballots changed unrelated authoritative state.");
        }

        [Test]
        public void LegalPactReplayMatchesOriginalSourceAndChangesTargetThroughPrivatePressure()
        {
            var fixture = Fixture(); var state = Witness().Snapshot;
            Assert.That(state.schemaVersion, Is.EqualTo(6));
            Assert.That((int)fixture["state"]["schemaVersion"], Is.EqualTo(5), "Keep the original witness unchanged.");
            Assert.That(JToken.DeepEquals(JObject.FromObject(state.npcSocial), JObject.FromObject(NpcSocialState.Create(state.seed))), Is.True,
                "The explicit command replay must not silently run background conversations.");
            var historicalView = JObject.FromObject(state); historicalView.Remove("npcSocial");
            historicalView["schemaVersion"] = fixture["state"]["schemaVersion"].DeepClone();
            WebVotingBlocParityTests.Equivalent(fixture["state"], historicalView, "legal command replay");
            Assert.That(state.phase, Is.EqualTo(EpisodePhase.Eviction));
            Assert.That(state.votes, Is.Empty);
            Assert.That(state.alliances.Single().members, Is.EquivalentTo(new[] { "player", "maya-hassan" }));
            Assert.That(EpisodeEngine.Voters(state).Select(actor => actor.id), Does.Contain("player").And.Contain("maya-hassan"));
            var before = Json(state); var plan = Plan(state);
            foreach (var decision in fixture["decisions"])
            {
                string id = (string)decision["voterId"];
                var evaluation = plan.evaluations.Single(item => item.voterId == id);
                WebVotingBlocParityTests.Equivalent(decision["expected"], JObject.FromObject(evaluation), "source coordinated " + id);
                WebVotingBlocParityTests.Equivalent(decision["uncoordinated"],
                    JObject.FromObject(WebEvictionVoting.Evaluate(CastOrderedOptions(state, id))), "source free " + id);
                Assert.That(WebEvictionVoting.ExplainNative(state, evaluation), Is.EqualTo((string)decision["publicReason"]));
            }
            var maya = plan.evaluations.Single(item => item.voterId == "maya-hassan");
            Assert.That(maya.selectedNomineeId, Is.EqualTo("casey-wilson"));
            Assert.That(WebEvictionVoting.Evaluate(CastOrderedOptions(state, "maya-hassan")).selectedNomineeId, Is.EqualTo("taylor-kim"));
            Assert.That(maya.privateReasonCodes.First(), Is.EqualTo("blocPressure"));
            Assert.That(maya.publicReasonCodes, Does.Not.Contain("blocPressure"));
            Assert.That(Json(state), Is.EqualTo(before), "Separate round RNG and detached plans must not mutate the episode.");
        }

        [Test]
        public void PrivateNpcBatchPreservesRandomnessAndSocialStateUntilHumanBallotAndReveal()
        {
            var engine = Witness(); var before = engine.Snapshot; var plan = Plan(before);
            var command = Command(before, EpisodeCommandKind.Advance); Apply(engine, command);
            var after = engine.Snapshot;
            Assert.That(after.evictionResolved, Is.False);
            Assert.That(after.votes, Has.Count.EqualTo(plan.evaluations.Count));
            Assert.That(after.votes.Select(vote => vote.voterId), Does.Not.Contain(before.playerId));
            AssertPrivateStateUnchanged(before, after);
            foreach (var vote in after.votes)
            {
                var expected = plan.evaluations.Single(item => item.voterId == vote.voterId);
                Assert.That(vote.targetId, Is.EqualTo(expected.selectedNomineeId));
                Assert.That(vote.reason, Is.EqualTo(WebEvictionVoting.ExplainNative(before, expected)));
                Assert.That(vote.reason, Does.Not.Contain("My alliance needs").And.Not.Contain("blocPressure"));
            }
            foreach (var added in after.events.Skip(before.events.Count))
            { Assert.That(added.kind, Is.EqualTo("private-vote")); Assert.That(added.audienceIds, Has.Count.EqualTo(1)); }
            string committed = Json(after);
            Assert.That(engine.Apply(command).duplicate, Is.True);
            Assert.That(Json(engine.Snapshot), Is.EqualTo(committed));
            var stale = Command(after, EpisodeCommandKind.Advance); stale.expectedRevision--;
            Assert.That(engine.Apply(stale).accepted, Is.False);
            Assert.That(Json(engine.Snapshot), Is.EqualTo(committed));
            var waiting = engine.Apply(Command(after, EpisodeCommandKind.Advance));
            Assert.That(waiting.accepted, Is.False);
            Assert.That(waiting.reason, Is.EqualTo("Cast your eviction vote first."));
            Assert.That(Json(engine.Snapshot), Is.EqualTo(committed), "Waiting without a new ballot is an atomic rejection.");
            Assert.That(Json(engine.Snapshot.votes), Is.EqualTo(Json(after.votes)), "Waiting again cannot reevaluate committed NPC ballots.");
            Assert.That(Json(engine.Snapshot.events), Is.EqualTo(Json(after.events)), "Waiting again cannot add vote or consequence events.");
            var reload = new EpisodeEngine(Read<EpisodeState>(JObject.FromObject(engine.Snapshot)));
            var human = Command(reload.Snapshot, EpisodeCommandKind.CastVote); human.targetId = before.nominees[0]; Apply(reload, human);
            var reveal = Command(reload.Snapshot, EpisodeCommandKind.Advance); Apply(reload, reveal);
            var resolved = reload.Snapshot;
            Assert.That(resolved.evictionResolved, Is.True);
            Assert.That(resolved.events.Count(e => e.kind == "vote-reveal"), Is.EqualTo(resolved.votes.Count));
            string resolvedJson = Json(resolved);
            Assert.That(reload.Apply(reveal).duplicate, Is.True);
            Assert.That(Json(reload.Snapshot), Is.EqualTo(resolvedJson), "Duplicate reveal must not settle anything twice.");
            var serialized = JObject.FromObject(resolved);
            Assert.That(serialized.Properties().Select(p => p.Name).Where(name => name.IndexOf("bloc", StringComparison.OrdinalIgnoreCase) >= 0),
                Is.EqualTo(new[] { "blocRulesStartWeek" }), "Only the rules cohort, not private plans, belongs in saves.");
        }

        [Test]
        public void CastingHumanVoteFirstDoesNotChangeCoordinationOrRewriteItsReceipt()
        {
            var engine = Witness(); var before = engine.Snapshot; var expected = Plan(before);
            var human = Command(before, EpisodeCommandKind.CastVote); human.targetId = before.nominees[0]; Apply(engine, human);
            var ballot = Json(engine.Snapshot.votes.Single());
            Assert.That(Json(Plan(engine.Snapshot).coordination), Is.EqualTo(Json(expected.coordination)));
            Apply(engine, Command(engine.Snapshot, EpisodeCommandKind.Advance));
            Assert.That(engine.Snapshot.evictionResolved, Is.True);
            Assert.That(Json(engine.Snapshot.votes.Single(v => v.voterId == before.playerId)), Is.EqualTo(ballot));
            foreach (var item in expected.evaluations)
                Assert.That(engine.Snapshot.votes.Single(v => v.voterId == item.voterId).targetId, Is.EqualTo(item.selectedNomineeId));
        }

        [Test]
        public void RestoredCommittedNpcBallotIsNeverOverwrittenByNewPlan()
        {
            var state = Witness().Snapshot;
            // Persistence regression fixture: deliberately retain a valid historical ballot that
            // differs from the new rules. This is not the legal-gameplay parity witness above.
            state.votes.Add(new VoteState { voterId = "maya-hassan", targetId = "taylor-kim", reason = "Previously committed public explanation" });
            var original = Json(state.votes.Single()); var random = state.randomState;
            var full = WebNativeEvictionRound.Evaluate(state, EpisodeEngine.Voters(state).Where(c => !c.isPlayer).Select(c => c.id));
            var missing = Plan(state);
            Assert.That(Json(full.coordination), Is.EqualTo(Json(missing.coordination)), "Committed members still participate in source draw ordering.");
            var engine = new EpisodeEngine(state); Apply(engine, Command(state, EpisodeCommandKind.Advance));
            Assert.That(Json(engine.Snapshot.votes.Single(v => v.voterId == "maya-hassan")), Is.EqualTo(original));
            Assert.That(engine.Snapshot.randomState, Is.EqualTo(random));
            Assert.That(engine.Snapshot.events.Skip(state.events.Count).Count(), Is.EqualTo(1));
        }

        [TestCase(false)] [TestCase(true)]
        public void CapturedOldWeekUsesLegacyEvaluatorIncludingOriginalNomineeOrder(bool reverseNominees)
        {
            var state = Witness(2).Snapshot;
            if (reverseNominees) state.nominees.Reverse();
            var expected = EpisodeEngine.Voters(state).Where(c => !c.isPlayer).Select(c => WebEvictionVoting.EvaluateNative(state, c.id)).ToArray();
            var engine = new EpisodeEngine(state); Apply(engine, Command(state, EpisodeCommandKind.Advance));
            foreach (var evaluation in expected)
            {
                var vote = engine.Snapshot.votes.Single(v => v.voterId == evaluation.voterId);
                Assert.That(vote.targetId, Is.EqualTo(evaluation.selectedNomineeId));
                Assert.That(vote.reason, Is.EqualTo(WebEvictionVoting.ExplainNative(state, evaluation)));
            }
            Assert.That(engine.Snapshot.randomState, Is.EqualTo(state.randomState));
        }

        [Test]
        public void MigratedCohortEnablesNewRoundOnlyAtFollowingWeekWithoutNewRandomStream()
        {
            var engine = Witness(2);
            for (int guard = 0; guard < 100 && !(engine.Snapshot.week == 2 && engine.Snapshot.phase == EpisodePhase.Eviction); guard++)
                Apply(engine, EpisodeEngineTests.NextCommand(engine.Snapshot));
            var state = engine.Snapshot;
            Assert.That(state.week, Is.EqualTo(2)); Assert.That(state.phase, Is.EqualTo(EpisodePhase.Eviction));
            var expected = Plan(state); var random = state.randomState;
            Apply(engine, Command(state, EpisodeCommandKind.Advance));
            foreach (var evaluation in expected.evaluations)
                Assert.That(engine.Snapshot.votes.Single(v => v.voterId == evaluation.voterId).targetId, Is.EqualTo(evaluation.selectedNomineeId));
            // This check isolates coordination even if an NPC-only tally resolves and a post-
            // eviction diary is prepared: the adapter itself is wholly independent of native RNG.
            Assert.That(state.randomState, Is.EqualTo(random));
            Assert.That(Json(Plan(state)), Is.EqualTo(Json(expected)));
        }

        [Test]
        public void NoAllianceNewRoundStillUsesSourceCastOrderAndNoDirective()
        {
            var initial = ContentCatalog.Create(4); var engine = new EpisodeEngine(initial);
            for (int guard = 0; guard < 80 && engine.Snapshot.phase != EpisodePhase.Eviction; guard++)
                Apply(engine, EpisodeEngineTests.NextCommand(engine.Snapshot));
            var state = engine.Snapshot; Assert.That(state.alliances, Is.Empty);
            var plan = Plan(state); Assert.That(plan.coordination.directives, Is.Empty);
            foreach (var evaluation in plan.evaluations)
                Assert.That(Json(evaluation), Is.EqualTo(Json(WebEvictionVoting.Evaluate(CastOrderedOptions(state, evaluation.voterId)))));
            Apply(engine, Command(state, EpisodeCommandKind.Advance));
            foreach (var evaluation in plan.evaluations)
                Assert.That(engine.Snapshot.votes.Single(v => v.voterId == evaluation.voterId).targetId, Is.EqualTo(evaluation.selectedNomineeId));
        }

        [Test]
        public void LegalNpcHohTieBreakUsesCastOrderedSourceVoteWithoutBlocDirective()
        {
            EpisodeEngine engine = null;
            for (uint seed = 1; seed <= 40 && engine == null; seed++)
            {
                var candidate = new EpisodeEngine(ContentCatalog.Create(seed));
                for (int guard = 0; guard < 90 && !(candidate.Snapshot.week == 2 && candidate.Snapshot.phase == EpisodePhase.Eviction); guard++)
                    Apply(candidate, EpisodeEngineTests.NextCommand(candidate.Snapshot));
                var snapshot = candidate.Snapshot;
                if (snapshot.week == 2 && snapshot.phase == EpisodePhase.Eviction && snapshot.hohId != snapshot.playerId &&
                    EpisodeEngine.Voters(snapshot).Count() == 2 && EpisodeEngine.Voters(snapshot).Any(c => c.isPlayer)) engine = candidate;
            }
            Assert.That(engine, Is.Not.Null, "The bounded legal seed set must include a regular human ballot opposite an NPC HoH.");
            var state = engine.Snapshot; var npc = Plan(state).evaluations.Single();
            var predictedTie = WebNativeEvictionRound.Evaluate(state, new[] { state.hohId });
            Assert.That(predictedTie.coordination.directives.Select(d => d.voterId), Does.Not.Contain(state.hohId));
            var expected = predictedTie.evaluations.Single();
            Assert.That(Json(expected), Is.EqualTo(Json(WebEvictionVoting.Evaluate(CastOrderedOptions(state, state.hohId)))));
            var human = Command(state, EpisodeCommandKind.CastVote);
            human.targetId = state.nominees.Single(id => id != npc.selectedNomineeId); Apply(engine, human);
            Apply(engine, Command(engine.Snapshot, EpisodeCommandKind.Advance));
            var result = engine.Snapshot;
            Assert.That(result.evictionResolved, Is.True);
            Assert.That(result.votes, Has.Count.EqualTo(3));
            var tie = result.votes.Single(v => v.voterId == state.hohId);
            Assert.That(tie.targetId, Is.EqualTo(expected.selectedNomineeId));
            Assert.That(tie.reason, Is.EqualTo("HoH tie-break: " + WebEvictionVoting.ExplainNative(state, expected)));
            Assert.That(tie.reason, Does.Not.Contain("blocPressure").And.Not.Contain("My alliance needs"));
        }

        [Test]
        public void FinaleAndJuryRemainIdenticalForBothRulesCohortsAndOldTextIsNotRewritten()
        {
            var engine = Witness();
            for (int guard = 0; guard < 130 && engine.Snapshot.phase != EpisodePhase.FinalHoHPart1; guard++)
                Apply(engine, EpisodeEngineTests.NextCommand(engine.Snapshot));
            var original = engine.Snapshot; Assert.That(original.phase, Is.EqualTo(EpisodePhase.FinalHoHPart1));
            string events = Json(original.events);
            var oldCohort = original.Clone(); oldCohort.blocRulesStartWeek = oldCohort.week + 1;
            var a = new EpisodeEngine(original); var b = new EpisodeEngine(oldCohort);
            Assert.That(Json(a.Snapshot.events), Is.EqualTo(events)); Assert.That(Json(b.Snapshot.events), Is.EqualTo(events));
            for (int guard = 0; guard < 60 && a.Snapshot.phase != EpisodePhase.Finished; guard++)
            {
                var command = EpisodeEngineTests.NextCommand(a.Snapshot); Apply(a, command); Apply(b, command);
                var left = JObject.FromObject(a.Snapshot); var right = JObject.FromObject(b.Snapshot);
                left.Remove("blocRulesStartWeek"); right.Remove("blocRulesStartWeek");
                Assert.That(JToken.DeepEquals(left, right), Is.True, "Bloc cohort must not alter final HoH, final eviction, questioning or jury voting.");
            }
            Assert.That(a.Snapshot.phase, Is.EqualTo(EpisodePhase.Finished));
            Assert.That(a.Snapshot.events.Last().text, Does.StartWith("Gamesim winner: "));
            Assert.That(a.Snapshot.events.Skip(original.events.Count).Any(e => e.text.Contains("You wins") || e.text.Contains("You is")), Is.False);
            Assert.That(Json(a.Snapshot.events.Take(original.events.Count).ToList()), Is.EqualTo(events), "Never rewrite historical saved prose.");
        }
    }
}
