using System;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Vote family V2: the internal exact-mode-2 engine seam. The same Apply - its guards, its Execute body,
    /// its detached candidate - with the complete prospective Vote core at the input and at the output in
    /// place of public validation. Public mode 2 stays refused; the seam is internal and reached here through
    /// the reflection facade, as the editor's test assembly must reach it.
    /// </summary>
    public sealed class UnifiedVoteEngineSeamTests
    {
        private static EpisodeState Mode1() => PinnedVoteSeason.Fresh(3);

        private static EpisodeState Mode2()
        {
            var s = Mode1(); s.unifiedCommitmentRulesVersion = UnifiedVoteFamilyValidation.Version;
            Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(s, out var error), Is.True, error);
            return s;
        }

        private static string Npc(EpisodeState s) => s.contestants.First(c => !c.isPlayer && c.status == ContestantStatus.Active).id;

        [Test]
        public void TheSeamTakesExactlyModeTwoAndOwnsADetachedCopy()
        {
            var s = Mode2();
            Assert.Throws<ArgumentException>(() => new EpisodeEngine(s), "The public constructor still refuses mode 2.");
            Assert.That(EpisodeValidation.TryValidate(s, out _), Is.False);

            string image = PinnedVoteSeason.Json(s);
            var engine = ProspectiveVoteFacade.Engine(s);
            Assert.That(PinnedVoteSeason.Json(engine.Snapshot), Is.EqualTo(image), "The seam holds the state it was given,");
            s.week = 99; s.contestants.Clear();
            Assert.That(PinnedVoteSeason.Json(engine.Snapshot), Is.EqualTo(image), "as a copy: the caller's object never reaches it,");
            var snapshot = engine.Snapshot; snapshot.revision = 77;
            Assert.That(engine.Snapshot.revision, Is.Not.EqualTo(77), "and its snapshots are copies too.");

            foreach (int mode in new[] { 0, 1, 3 })
            {
                var other = Mode1(); other.unifiedCommitmentRulesVersion = mode;
                if (mode == 0) { other.unifiedHearingRulesVersion = 0; other.unifiedHearingEvidence.Clear(); other.unifiedHearingReceipts.Clear(); }
                var thrown = Assert.Throws<ArgumentException>(() => ProspectiveVoteFacade.Engine(other));
                Assert.That(thrown.Message, Does.Contain("Prospective Vote requires its explicit version"), "Mode " + mode + " is not the seam's.");
            }
            Assert.Throws<ArgumentException>(() => ProspectiveVoteFacade.Engine(null));
        }

        /// <summary>The input is held to the complete core, not to the version number: a raw Vote mirror is refused.</summary>
        [Test]
        public void TheSeamValidatesItsInputWithTheCompleteCore()
        {
            var s = Mode2();
            s.promises.Add(new PromiseState { id = "promise-" + (s.nextSequence - 1), fromId = s.playerId, toId = Npc(s),
                kind = PromiseKind.Vote, status = PromiseStatus.Active, week = s.week, expiresWeek = s.week });
            var thrown = Assert.Throws<ArgumentException>(() => ProspectiveVoteFacade.Engine(s));
            Assert.That(thrown.Message, Does.Contain("cannot have writable raw mirrors"));
        }

        [Test]
        public void ACommandThroughTheSeamCommitsOnlyACoreValidCandidate()
        {
            var s = Mode2();
            var engine = ProspectiveVoteFacade.Engine(s);
            var talk = ProspectiveVoteTwins.Command(s, EpisodeCommandKind.Talk, Npc(s));
            var result = engine.Apply(talk);
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(result.duplicate, Is.False);
            Assert.That(result.state.revision, Is.EqualTo(s.revision + 1));
            Assert.That(result.state.acceptedCommandIds.Last(), Is.EqualTo(talk.id));
            Assert.That(result.state.unifiedCommitmentRulesVersion, Is.EqualTo(UnifiedVoteFamilyValidation.Version), "Still mode 2,");
            Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(result.state, out var error), Is.True, error);
            Assert.That(EpisodeValidation.TryValidate(result.state, out _), Is.False, "and still not a public season.");

            // The same command on the public mode-1 game draws, mints and logs the same.
            var legacy = new EpisodeEngine(Mode1()).Apply(talk);
            Assert.That(legacy.accepted, Is.True, legacy.reason);
            Assert.That((result.state.randomState, result.state.nextSequence, result.state.events.Last().text),
                Is.EqualTo((legacy.state.randomState, legacy.state.nextSequence, legacy.state.events.Last().text)));
        }

        /// <summary>A duplicate command is recognised, and changes nothing: no second effect, draw, id or line.</summary>
        [Test]
        public void ADuplicateCommandThroughTheSeamChangesNothing()
        {
            var s = Mode2();
            var engine = ProspectiveVoteFacade.Engine(s);
            var talk = ProspectiveVoteTwins.Command(s, EpisodeCommandKind.Talk, Npc(s));
            var first = engine.Apply(talk);
            Assert.That(first.accepted, Is.True, first.reason);
            var again = talk; again.expectedRevision = first.state.revision;
            var second = engine.Apply(again);
            Assert.That(second.duplicate, Is.True);
            Assert.That(second.accepted, Is.False);
            Assert.That(PinnedVoteSeason.Json(second.state), Is.EqualTo(PinnedVoteSeason.Json(first.state)), "The duplicate changes nothing.");
            Assert.That(PinnedVoteSeason.Json(engine.Snapshot), Is.EqualTo(PinnedVoteSeason.Json(first.state)));
        }

        /// <summary>Every guard the public Apply has, the seam has, before anything is executed.</summary>
        [TestCase("stale revision")] [TestCase("wrong phase")] [TestCase("wrong actor")] [TestCase("unknown kind")]
        [TestCase("no id")] [TestCase("long id")] [TestCase("control text")] [TestCase("performance")]
        public void TheSeamKeepsEveryPublicGuard(string defect)
        {
            var s = Mode2();
            var engine = ProspectiveVoteFacade.Engine(s);
            var command = ProspectiveVoteTwins.Command(s, EpisodeCommandKind.Talk, Npc(s));
            string reason;
            switch (defect)
            {
                case "stale revision": command.expectedRevision = s.revision + 1; reason = "The episode changed"; break;
                case "wrong phase": command.expectedPhase = EpisodePhase.Campaign; reason = "The episode changed"; break;
                case "wrong actor": command.actorId = Npc(s); reason = "Only the local player's command channel"; break;
                case "unknown kind": command.kind = (EpisodeCommandKind)9999; reason = "Unknown command."; break;
                case "no id": command.id = " "; reason = "A bounded command ID is required."; break;
                case "long id": command.id = new string('x', 161); reason = "A bounded command ID is required."; break;
                case "control text": command.text = "a\u0001b"; reason = "Text must be at most 2,000 characters"; break;
                default: command.performance = double.NaN; reason = "Competition input must be between zero and one."; break;
            }
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.False);
            Assert.That(result.reason, Does.StartWith(reason));
            Assert.That(PinnedVoteSeason.Json(engine.Snapshot), Is.EqualTo(PinnedVoteSeason.Json(s)), "Nothing is executed.");
            // The public mode-1 game refuses the same command in the same words.
            var legacy = new EpisodeEngine(Mode1()).Apply(command);
            Assert.That(legacy.accepted, Is.False);
            Assert.That(legacy.reason, Is.EqualTo(result.reason));
        }

        /// <summary>
        /// The output is the complete core's: a candidate the core refuses is rejected whole, its draws, ids and
        /// lines discarded with it. Here - constructed - the season's sequence stands at the save format's bound,
        /// so the conversation's line carries the candidate past it: nothing in the command itself checks, only
        /// the candidate's validation, which the public mode-1 game shares and refuses in the same words.
        /// (Before vote family V3b this was a promise of safety, which mode 2 then wrote raw; it writes it
        /// canonically now - UnifiedSafetyModeTwoTests.)
        /// </summary>
        [Test]
        public void ACandidateTheCoreRefusesIsRejectedWholeAndNothingIsInstalled()
        {
            var s = Mode2(); s.nextSequence = 1000000;
            Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(s, out var valid), Is.True, "Fixture: the bound itself is lawful. " + valid);
            var talk = ProspectiveVoteTwins.Command(s, EpisodeCommandKind.Talk, Npc(s));
            var result = ProspectiveVoteFacade.Engine(s).Apply(talk);
            Assert.That(result.accepted, Is.False);
            Assert.That(result.reason, Does.StartWith("Candidate rejected: "));
            ProspectiveVoteTwins.AssertUntouched(s, result, "A refused candidate");
            var mode1 = Mode1(); mode1.nextSequence = 1000000;
            var legacy = new EpisodeEngine(mode1).Apply(talk);
            Assert.That(legacy.accepted, Is.False);
            Assert.That(legacy.reason, Is.EqualTo(result.reason), "The public mode-1 game refuses the same candidate in the same words.");
        }

        /// <summary>
        /// The seam's trusted NPC operation holds its candidate to the same core (EpisodeEngine.PrepareNpcOperation):
        /// a clock second on a mode-2 season is prepared - never installed - as a core-valid, still-not-public
        /// candidate, as the public mode-1 game prepares the same second.
        /// </summary>
        [Test]
        public void TheSeamPreparesAnNpcOperationUnderTheCompleteCore()
        {
            var s = Mode2();
            var engine = ProspectiveVoteFacade.Engine(s);
            NpcOperationRequest Tick(EpisodeState state) => new NpcOperationRequest
            {
                kind = NpcOperationKind.Tick, sessionId = state.sessionId, expectedRevision = state.revision, expectedPhase = state.phase,
                expectedClockTick = state.npcSocial.clockTick, targetClockTick = state.npcSocial.clockTick + 1, freeRoamReady = true,
            };
            string before = PinnedVoteSeason.Json(engine.Snapshot);
            var result = engine.PrepareNpcOperation(Tick(s));
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(PinnedVoteSeason.Json(engine.Snapshot), Is.EqualTo(before), "Prepared, never installed.");
            Assert.That((result.candidate.npcSocial.clockTick, result.candidate.revision), Is.EqualTo((s.npcSocial.clockTick + 1, s.revision + 1)));
            Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(result.candidate, out var error), Is.True, error);
            Assert.That(EpisodeValidation.TryValidate(result.candidate, out _), Is.False, "Still not a public season.");
            var legacy = new EpisodeEngine(Mode1()).PrepareNpcOperation(Tick(Mode1()));
            Assert.That(legacy.accepted, Is.True, legacy.reason);
            Assert.That(legacy.candidate.npcSocial.clockTick, Is.EqualTo(result.candidate.npcSocial.clockTick));
        }

        /// <summary>No public path reaches the seam: it is one internal factory, and the public constructor is the one there was.</summary>
        [Test]
        public void TheSeamIsNotReachableFromPublicCode()
        {
            var factory = typeof(EpisodeEngine).GetMethod("ProspectiveVote", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(factory, Is.Not.Null);
            Assert.That(factory.IsPublic, Is.False);
            Assert.That(factory.IsAssembly, Is.True, "Internal to the simulation.");
            var constructors = typeof(EpisodeEngine).GetConstructors(BindingFlags.Instance | BindingFlags.Public);
            Assert.That(constructors.Select(c => string.Join(",", c.GetParameters().Select(p => p.ParameterType.Name))),
                Is.EqualTo(new[] { nameof(EpisodeState) }), "The public constructor is the one there was.");
            var flag = typeof(EpisodeEngine).GetField("prospectiveVote", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(flag, Is.Not.Null);
            Assert.That(flag.IsPrivate && flag.IsInitOnly, Is.True, "Fixed at construction, by the engine alone.");
            Assert.That(typeof(EpisodeEngine).GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(p => p.Name), Is.EqualTo(new[] { nameof(EpisodeEngine.Snapshot) }), "Nothing public reads or sets it.");
        }
    }
}
