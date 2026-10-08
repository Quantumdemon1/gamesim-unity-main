using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// E5's native, deterministic block-speech policy. These are simulation and detached JSON
    /// replay tests, not SaveStore, Unity UI, native-player or human acceptance evidence.
    /// Controlled evaluator cases explicitly remove the web factors to isolate the new term;
    /// command-flow cases retain the production vote evaluator and real phase transitions.
    /// </summary>
    public sealed class BlockSpeechInfluenceTests
    {
        private static readonly string[] WebFactors = { "relationship", "threat", "alliance", "deal",
            "strategicValue", "history", "personality", "memory", "persona", "blocPressure" };

        private static string Json(object value) => JsonConvert.SerializeObject(value);
        private static EpisodeState RoundTrip(EpisodeState s) => JsonConvert.DeserializeObject<EpisodeState>(Json(s),
            new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace });
        private static string Npc(EpisodeState s, int index = 0) => s.contestants.Where(c => !c.isPlayer).ElementAt(index).id;
        private static void Valid(EpisodeState s) => Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);

        private static EpisodeState House(int size = 8, bool playerOnBlock = true)
        {
            var s = EconomyRulesTests.Fresh(size);
            s.strategyRulesStartWeek = 1;
            s.agencyRulesStartWeek = 1;
            EpisodeEngine.EnableRead(s);
            EpisodeEngine.EnableCommitments(s);
            s.phase = EpisodePhase.Eviction;
            s.evictionStage = EvictionStage.Speeches;
            s.hohId = Npc(s, 1);
            s.vetoHolderId = s.hohId;
            s.vetoPlayers = s.Active.Take(EpisodeEngine.VetoPlayerCount(size)).Select(c => c.id).ToList();
            s.vetoResolved = true;
            s.nominees = new List<string> { playerOnBlock ? s.playerId : Npc(s, 2), Npc(s) };
            Valid(s);
            return s;
        }

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind, string text = null,
            string approach = null, string target = null) => new EpisodeCommand
        {
            id = "block-speech-" + s.revision + "-" + kind,
            actorId = s.playerId, expectedRevision = s.revision, expectedPhase = s.phase,
            kind = kind, text = text, secondTargetId = approach, targetId = target,
        };

        private static EpisodeState Apply(EpisodeState s, EpisodeCommandKind kind, string text = null,
            string approach = null, string target = null)
        {
            var result = new EpisodeEngine(s).Apply(Command(s, kind, text, approach, target));
            Assert.That(result.accepted, Is.True, result.reason);
            Valid(result.state);
            return result.state;
        }

        private static EpisodeState Deliver(EpisodeState s, string text = "Please keep me in the house.",
            string approach = LobbyApproach.Emotional) => Apply(s, EpisodeCommandKind.SubmitEvictionSpeech, text, approach);

        private static void RejectedUnchanged(EpisodeState s, EpisodeCommand command)
        {
            var engine = new EpisodeEngine(s);
            string before = Json(engine.Snapshot), input = Json(s);
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.False);
            Assert.That(Json(result.state), Is.EqualTo(before));
            Assert.That(Json(engine.Snapshot), Is.EqualTo(before));
            Assert.That(Json(s), Is.EqualTo(input));
        }

        // An explicitly synthetic delivered speech for isolated two-nominee arithmetic. Real
        // command tests below separately establish that production writes this exact contract.
        private static void AddSpeech(EpisodeState s, string speaker, string approach, string text = "Please hear my case.")
        {
            var speech = new EvictionSpeechState { speakerId = speaker, week = s.week,
                text = text, isPlayerAuthored = speaker == s.playerId };
            s.evictionSpeeches.Add(speech);
            s.events.Add(new EpisodeEvent { sequence = s.nextSequence++, week = s.week,
                phase = EpisodePhase.Eviction, kind = BlockSpeeches.EventPrefix + approach,
                text = BlockSpeeches.ReceiptText(speech), audienceIds = BlockSpeeches.Audience(s, speaker).ToList() });
        }

        private static WebVoteOptions Isolate(EpisodeState s, string voter, double firstScore = 0, bool keepBloc = false)
        {
            var options = WebEvictionVoting.FromNative(s, voter);
            options.omittedFactors = WebFactors.Where(code => !keepBloc || code != "blocPressure").ToList();
            options.storyTerms.Clear();
            options.obligations.Clear();
            if (firstScore != 0) options.obligations.Add(new WebVoteObligation
                { nomineeId = s.nominees[0], code = "fixture-baseline", value = firstScore });
            return options;
        }

        private static WebVoteFactor Speech(WebVoteEvaluation evaluation, string nominee) =>
            evaluation.nomineeEvaluations.Single(n => n.nomineeId == nominee).factors.Single(f => f.code == BlockSpeeches.FactorCode);

        private static void FillHistoryBehindReceipts(EpisodeState s)
        {
            s.events.RemoveAll(e => !e.kind.StartsWith(BlockSpeeches.EventPrefix, StringComparison.Ordinal));
            while (s.events.Count < 256)
                s.events.Add(new EpisodeEvent { sequence = s.nextSequence++, week = s.week,
                    phase = s.phase, kind = "fixture-history", text = "A bounded ordinary history entry." });
            Valid(s);
        }

        [Test]
        public void PublicContractKeepsTheFourExplicitApproachesAndSmallNativeBounds()
        {
            Assert.That(BlockSpeeches.Approaches, Is.EquivalentTo(LobbyApproach.All));
            Assert.That(BlockSpeeches.Quiet, Is.EqualTo("quiet"));
            Assert.That(BlockSpeeches.FactorCode, Is.EqualTo("speech"));
            Assert.That(BlockSpeeches.EventPrefix, Is.EqualTo("block-speech:"));
            Assert.That(BlockSpeeches.MostInfluence, Is.EqualTo(4));
            Assert.That(BlockSpeeches.PersuadableMargin, Is.EqualTo(20));
            Assert.That(BlockSpeeches.ReceiptText(new EvictionSpeechState { text = string.Empty }), Is.EqualTo("No speech was given."));
            Assert.That(typeof(EvictionSpeechState).GetFields().Select(f => f.Name),
                Is.EquivalentTo(new[] { "speakerId", "text", "week", "isPlayerAuthored" }), "No silent schema23 DTO expansion.");
        }

        [TestCase(8, "emotional")] [TestCase(8, "strategic")] [TestCase(8, "deal")] [TestCase(8, "pressure")]
        [TestCase(16, "emotional")] [TestCase(16, "strategic")] [TestCase(16, "deal")] [TestCase(16, "pressure")]
        public void RealDeliverySavesOneExactPublicReceiptWithoutSpendingAnActionOrRandomDraw(int size, string approach)
        {
            var before = House(size); string input = Json(before);
            string prose = "Keep me.\nI can still help this house.\tThank you.";
            var after = Deliver(before, "  " + prose + "  ", approach);
            var speech = after.evictionSpeeches.Single();
            var receipt = BlockSpeeches.Receipt(after, speech);
            Assert.That(speech.text, Is.EqualTo(prose));
            Assert.That(BlockSpeeches.Approach(after, speech), Is.EqualTo(approach));
            Assert.That(receipt, Is.Not.Null);
            Assert.That(receipt.kind, Is.EqualTo(BlockSpeeches.EventPrefix + approach));
            Assert.That(receipt.text, Is.EqualTo(prose));
            Assert.That(receipt.audienceIds, Is.EqualTo(new[] { before.playerId }.Concat(before.Active.Where(c => !c.isPlayer).Select(c => c.id))));
            Assert.That(after.events.Count, Is.EqualTo(before.events.Count + 1));
            Assert.That(after.nextSequence, Is.EqualTo(before.nextSequence + 1), "Replace the old speech log, never append a second receipt.");
            Assert.That(after.randomState, Is.EqualTo(before.randomState));
            Assert.That(after.socialActions, Is.EqualTo(before.socialActions));
            Assert.That(after.outOfPhaseSocialActions, Is.EqualTo(before.outOfPhaseSocialActions));
            Assert.That(after.windowActions, Is.EqualTo(before.windowActions));
            Assert.That(Json(after.relationships), Is.EqualTo(Json(before.relationships)));
            Assert.That(Json(after.promises), Is.EqualTo(Json(before.promises)), "A rhetorical deal is not a new promise.");
            Assert.That(Json(after.deals), Is.EqualTo(Json(before.deals)));
            Assert.That(after.schemaVersion, Is.EqualTo(27));
            Assert.That(Json(before), Is.EqualTo(input));
        }

        [TestCase(null)] [TestCase("")] [TestCase(" \r\n\t ")]
        public void SilentDeliveryHasOneNonemptyQuietReceiptAndNoVoteInfluence(string text)
        {
            var s = Deliver(House(), text, null);
            var speech = s.evictionSpeeches.Single();
            Assert.That(speech.text, Is.Empty);
            Assert.That(BlockSpeeches.Approach(s, speech), Is.EqualTo(BlockSpeeches.Quiet));
            Assert.That(BlockSpeeches.Receipt(s, speech).text, Is.EqualTo("No speech was given."));
            var evaluation = WebEvictionVoting.Evaluate(Isolate(s, Npc(s, 2)));
            Assert.That(evaluation.nomineeEvaluations.Select(n => Speech(evaluation, n.nomineeId).value), Is.All.EqualTo(0));
        }

        [Test]
        public void OmittedApproachDefaultsOnlyNonblankLegacyCommandsToEmotional()
        {
            var s = Deliver(House(), "The exact words are not silently classified as pressure.", null);
            Assert.That(BlockSpeeches.Approach(s, s.evictionSpeeches.Single()), Is.EqualTo(LobbyApproach.Emotional));
        }

        [Test]
        public void TheWholeTwoThousandCharacterSpeechSurvivesDetachedJsonReplay()
        {
            string prose = new string('x', 1000) + "\n" + new string('y', 999);
            var s = Deliver(House(), prose, LobbyApproach.Strategic);
            var replay = RoundTrip(s);
            Valid(replay);
            Assert.That(Json(replay), Is.EqualTo(Json(s)));
            Assert.That(replay.evictionSpeeches.Single().text, Is.EqualTo(prose));
            Assert.That(BlockSpeeches.Receipt(replay, replay.evictionSpeeches.Single()).text, Is.EqualTo(prose));
        }

        [TestCase("unknown-approach")] [TestCase("unknown-blank")] [TestCase("overlong")]
        [TestCase("control")] [TestCase("actor")] [TestCase("revision")] [TestCase("expected-phase")]
        [TestCase("voting")] [TestCase("not-nominee")]
        public void InvalidSpeechAuthorityRejectsAtomically(string boundary)
        {
            var s = House(); var command = Command(s, EpisodeCommandKind.SubmitEvictionSpeech, "Hear me.", LobbyApproach.Emotional);
            switch (boundary)
            {
                case "unknown-approach": command.secondTargetId = "guaranteed-votes"; break;
                case "unknown-blank": command.secondTargetId = "guaranteed-votes"; command.text = ""; break;
                case "overlong": command.text = new string('x', 2001); break;
                case "control": command.text = "bad\u0001input"; break;
                case "actor": command.actorId = Npc(s); break;
                case "revision": command.expectedRevision++; break;
                case "expected-phase": command.expectedPhase = EpisodePhase.Social; break;
                case "voting": s.evictionStage = EvictionStage.Voting; break;
                case "not-nominee": s.nominees[0] = Npc(s, 2); break;
            }
            RejectedUnchanged(s, command);
        }

        [Test]
        public void AReplayOrANewCommandCannotDeliverTheSameSpeakersSpeechTwice()
        {
            var initial = House(); var command = Command(initial, EpisodeCommandKind.SubmitEvictionSpeech, "Hear me.", LobbyApproach.Deal);
            var engine = new EpisodeEngine(initial);
            var first = engine.Apply(command); Assert.That(first.accepted, Is.True, first.reason);
            string after = Json(first.state);
            var duplicate = engine.Apply(command);
            Assert.That(duplicate.duplicate, Is.True);
            Assert.That(Json(duplicate.state), Is.EqualTo(after));
            var replay = RoundTrip(first.state);
            RejectedUnchanged(replay, Command(replay, EpisodeCommandKind.SubmitEvictionSpeech, "Try again.", LobbyApproach.Pressure));
            Assert.That(replay.events.Count(e => e.kind.StartsWith(BlockSpeeches.EventPrefix, StringComparison.Ordinal)), Is.EqualTo(1));
        }

        [TestCase("emotional", "", 1)] [TestCase("emotional", "Social", 2.5)]
        [TestCase("emotional", "Social,Loyal,Emotional", 4)]
        [TestCase("emotional", "Strategic,Analytical,Manipulative,Stubborn,Strategic", -4)]
        [TestCase("strategic", "Analytical,Intuitive", 4)] [TestCase("strategic", "Emotional,Impulsive", -1)]
        [TestCase("deal", "Flexible", 2.5)] [TestCase("deal", "Loyal,Stubborn", -1)]
        [TestCase("pressure", "Impulsive", 2.5)] [TestCase("pressure", "Stubborn,Confrontational,Strategic,Analytical", -3)]
        public void ContentAndListenerTraitsProduceTheDocumentedBoundedPrivateTerm(string approach, string traits, double expected)
        {
            var s = Deliver(House(), "An explicit approach, not keyword scoring.", approach);
            string voter = Npc(s, 2);
            s.Find(voter).traits = traits.Length == 0 ? new List<string>() : traits.Split(',').ToList();
            Valid(s);
            string before = Json(s);
            var evaluation = WebEvictionVoting.Evaluate(Isolate(s, voter));
            var term = Speech(evaluation, s.playerId);
            Assert.That(term.value, Is.EqualTo(expected).Within(1e-9));
            Assert.That(term.value, Is.InRange(-4, 4));
            Assert.That(term.visibility, Is.EqualTo("private"));
            Assert.That(Speech(evaluation, s.nominees[1]).value, Is.Zero);
            Assert.That(evaluation.publicReasonCodes, Does.Not.Contain(BlockSpeeches.FactorCode));
            Assert.That(VoteRead.FactorKnown(s, voter, s.playerId, term), Is.False);
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(-20.001, false)] [TestCase(-20, false)] [TestCase(-19.999, true)]
        [TestCase(0, true)] [TestCase(19.999, true)] [TestCase(20, false)] [TestCase(20.001, false)]
        public void OnlyTheSpeechFreeBallotMarginBelowTwentyIsPersuadable(double baseline, bool moves)
        {
            var s = Deliver(House()); string voter = Npc(s, 2);
            s.Find(voter).traits = new List<string> { "Social" };
            var evaluation = WebEvictionVoting.Evaluate(Isolate(s, voter, baseline));
            Assert.That(Speech(evaluation, s.playerId).value, Is.EqualTo(moves ? 2.5 : 0));
            Assert.That(evaluation.nomineeEvaluations.Single(n => n.nomineeId == s.playerId).score,
                Is.EqualTo(baseline + (moves ? 2.5 : 0)).Within(1e-9));
        }

        [TestCase(0, 0)] [TestCase(30, 2.5)]
        public void TheRealBlocDirectiveIsIncludedBeforeThePersuadabilityDecision(double firstScore, double expected)
        {
            var s = Deliver(House()); string voter = Npc(s, 2);
            s.Find(voter).traits = new List<string> { "Social" };
            var options = Isolate(s, voter, firstScore, keepBloc: true);
            options.blocDirective = new WebVoteBlocDirective { directive = "bloc_vote", targetNomineeId = s.playerId, allianceId = "fixture-bloc" };
            var evaluation = WebEvictionVoting.Evaluate(options);
            Assert.That(evaluation.nomineeEvaluations.Single(n => n.nomineeId == s.playerId).factors.Single(f => f.code == "blocPressure").value, Is.EqualTo(-40));
            Assert.That(Speech(evaluation, s.playerId).value, Is.EqualTo(expected), "Baseline margin is |firstScore - 40|, not |firstScore|.");
        }

        [Test]
        public void BothSpeechesUseTheSameBaselineAndTheirCombinedMarginShiftIsAtMostEight()
        {
            var s = House(); string voter = Npc(s, 2);
            s.Find(voter).traits = new List<string> { "Social", "Loyal", "Emotional", "Stubborn", "Confrontational", "Strategic", "Analytical", "Strategic" };
            AddSpeech(s, s.nominees[0], LobbyApproach.Emotional);
            AddSpeech(s, s.nominees[1], LobbyApproach.Pressure);
            Valid(s);
            var options = Isolate(s, voter, 19);
            var forward = WebEvictionVoting.Evaluate(options);
            options.nominees.Reverse();
            var reverse = WebEvictionVoting.Evaluate(options);
            foreach (var nominee in s.nominees)
            {
                Assert.That(Speech(reverse, nominee).value, Is.EqualTo(Speech(forward, nominee).value));
                Assert.That(Speech(forward, nominee).value, Is.Not.Zero, "Neither speech may push the second speech across its eligibility threshold.");
            }
            double first = forward.nomineeEvaluations.Single(n => n.nomineeId == s.nominees[0]).score;
            double second = forward.nomineeEvaluations.Single(n => n.nomineeId == s.nominees[1]).score;
            Assert.That(Math.Abs((first - second) - 19), Is.LessThanOrEqualTo(8));
            Assert.That(reverse.selectedNomineeId, Is.EqualTo(forward.selectedNomineeId));
        }

        [Test]
        public void AnOpposingNomineesAllyDoesNotAcquireSpeechInfluence()
        {
            var s = Deliver(House()); string voter = Npc(s, 2);
            s.alliances.Add(new AllianceState { id = "speech-opponent-pact", name = "Opponent pact",
                members = new List<string> { voter, s.nominees[1] } });
            Valid(s);
            Assert.That(Speech(WebEvictionVoting.Evaluate(Isolate(s, voter)), s.playerId).value, Is.Zero);
        }

        [Test]
        public void AReceiptlessSchema23SpeechIsNeverBackfilledOrGivenAnEffect()
        {
            var s = House();
            s.evictionSpeeches.Add(new EvictionSpeechState { speakerId = s.playerId, week = s.week,
                isPlayerAuthored = true, text = "strategic emotional deal pressure" });
            Valid(s);
            string before = Json(s);
            Assert.That(BlockSpeeches.Receipt(s, s.evictionSpeeches.Single()), Is.Null);
            Assert.That(Speech(WebEvictionVoting.Evaluate(Isolate(s, Npc(s, 2))), s.playerId).value, Is.Zero);
            Assert.That(Json(s), Is.EqualTo(before));
            var voting = Apply(s, EpisodeCommandKind.Advance);
            Assert.That(BlockSpeeches.Receipt(voting, voting.evictionSpeeches.Single(x => x.isPlayerAuthored)), Is.Null);
        }

        [TestCase("legacy")] [TestCase("week-off")] [TestCase("week-delayed")]
        [TestCase("levers-off")] [TestCase("levers-delayed")] [TestCase("agency-off")] [TestCase("agency-delayed")]
        [TestCase("strategy-off")] [TestCase("strategy-delayed")]
        public void EveryRequiredRuleBoundaryKeepsTheOldSpeechAndTenFactorPath(string boundary)
        {
            var s = House();
            switch (boundary)
            {
                case "legacy": s.economyRulesVersion = 0; break;
                case "week-off": s.weekRulesStartWeek = 0; break;
                case "week-delayed": s.weekRulesStartWeek = 2; break;
                case "levers-off": s.leverRulesStartWeek = 0; break;
                case "levers-delayed": s.leverRulesStartWeek = 2; break;
                case "agency-off": s.agencyRulesStartWeek = 0; break;
                case "agency-delayed": s.agencyRulesStartWeek = 2; break;
                case "strategy-off": s.strategyRulesStartWeek = 0; break;
                case "strategy-delayed": s.strategyRulesStartWeek = 2; break;
            }
            Assert.That(BlockSpeeches.RulesOn(s), Is.False);
            var baseline = WebEvictionVoting.EvaluateNative(s, Npc(s, 2));
            var spoken = Deliver(s, "Same words, unchanged old rules.", LobbyApproach.Pressure);
            Assert.That(spoken.events.Last().kind, Is.EqualTo("eviction-speech"));
            Assert.That(spoken.events.Last().text, Is.EqualTo("You addressed the house from the block."));
            Assert.That(spoken.events.Last().audienceIds, Is.Empty);
            Assert.That(BlockSpeeches.Receipt(spoken, spoken.evictionSpeeches.Single()), Is.Null);
            Assert.That(Json(WebEvictionVoting.EvaluateNative(spoken, Npc(s, 2))), Is.EqualTo(Json(baseline)));
            Assert.That(baseline.nomineeEvaluations.All(n => n.factors.Count == 10), Is.True);
            var oldNpcWords = HouseDialogue.EvictionPlea(spoken, spoken.nominees[1]);
            var voting = Apply(spoken, EpisodeCommandKind.Advance);
            Assert.That(voting.evictionSpeeches.Single(x => !x.isPlayerAuthored).text, Is.EqualTo(oldNpcWords));
            Assert.That(voting.events.Where(e => e.kind == "eviction-speech").Last().text,
                Is.EqualTo(voting.Find(voting.nominees[1]).name + ": " + oldNpcWords));
        }

        [TestCase(8)] [TestCase(16)]
        public void RealNpcDeliveryKeepsTheOriginalWordsAndAddsTheMatchingApproachClosing(int size)
        {
            var s = House(size, playerOnBlock: false);
            string[] original = s.nominees.Select(id => HouseDialogue.EvictionPlea(s, id)).ToArray();
            var voting = Apply(s, EpisodeCommandKind.Advance);
            Assert.That(voting.evictionStage, Is.EqualTo(EvictionStage.Voting));
            Assert.That(voting.evictionSpeeches, Has.Count.EqualTo(2));
            for (int index = 0; index < s.nominees.Count; index++)
            {
                var speech = voting.evictionSpeeches.Single(x => x.speakerId == s.nominees[index]);
                string approach = BlockSpeeches.NpcApproach(s.Find(speech.speakerId));
                Assert.That(speech.text, Is.EqualTo(original[index] + BlockSpeeches.NpcClosing(approach)));
                Assert.That(BlockSpeeches.Approach(voting, speech), Is.EqualTo(approach));
                Assert.That(BlockSpeeches.Receipt(voting, speech).audienceIds,
                    Is.EqualTo(BlockSpeeches.Audience(s, speech.speakerId)));
            }
            Assert.That(voting.nextSequence, Is.EqualTo(s.nextSequence + 3), "Two replacement speech logs and the existing voting-stage log.");
            Assert.That(voting.randomState, Is.EqualTo(s.randomState));
        }

        [TestCase("player")] [TestCase("hoh")] [TestCase("nominee")]
        public void HearingASpeechDoesNotMakeAnIneligibleActorAnNpcVoter(string role)
        {
            var s = Deliver(House());
            string voter = role == "player" ? s.playerId : role == "hoh" ? s.hohId : s.nominees[1];
            var options = Isolate(s, voter);
            Assert.That(options.speechHearer, Is.False);
            var evaluation = WebEvictionVoting.Evaluate(options);
            Assert.That(evaluation.nomineeEvaluations.Select(n => Speech(evaluation, n.nomineeId).value), Is.All.EqualTo(0));
        }

        [Test]
        public void AnUnheardAppealCannotMoveAVoteEvenWithOtherwiseReceptiveInputs()
        {
            var s = Deliver(House()); string voter = Npc(s, 2);
            var options = Isolate(s, voter);
            Assert.That(options.speechAppeals.Single().heard, Is.True);
            options.speechAppeals.Single().heard = false;
            Assert.That(Speech(WebEvictionVoting.Evaluate(options), s.playerId).value, Is.Zero);
            Assert.That(BlockSpeeches.Receipt(s, s.evictionSpeeches.Single()).audienceIds, Does.Contain(voter),
                "Only the ephemeral isolated hearing input was changed; no valid saved receipt was forged.");
        }

        [Test]
        public void TheSpectatorHearsTheBroadcastButNeverAcquiresABallotOrInfluence()
        {
            var s = House(playerOnBlock: false);
            s.Find(s.playerId).status = ContestantStatus.Evicted;
            s.vetoPlayers = s.Active.Take(EpisodeEngine.VetoPlayerCount(s.Active.Count())).Select(c => c.id).ToList();
            Valid(s);
            var voting = Apply(s, EpisodeCommandKind.Advance);
            foreach (var speech in voting.evictionSpeeches)
            {
                var audience = BlockSpeeches.Receipt(voting, speech).audienceIds;
                Assert.That(audience, Is.EqualTo(new[] { speech.speakerId }
                    .Concat(s.Active.Where(c => c.id != speech.speakerId).Select(c => c.id)).Concat(new[] { s.playerId })));
                Assert.That(audience.Count(id => id == s.playerId), Is.EqualTo(1));
                Assert.That(BlockSpeeches.PublicLine(voting, BlockSpeeches.Receipt(voting, speech)),
                    Is.EqualTo(voting.Find(speech.speakerId).name + ": " + speech.text));
            }
            Assert.That(EpisodeEngine.Voters(voting).Any(c => c.isPlayer), Is.False);
            Assert.That(WebEvictionVoting.FromNative(voting, s.playerId).speechHearer, Is.False);
            RejectedUnchanged(voting, Command(voting, EpisodeCommandKind.CastVote, target: voting.nominees[0]));
        }

        [Test]
        public void APlayerBallotAndAnAlreadyCastNpcBallotRemainTheirExactCommittedChoices()
        {
            var s = Apply(House(playerOnBlock: false), EpisodeCommandKind.Advance);
            string chosen = s.nominees[1];
            var afterPlayer = Apply(s, EpisodeCommandKind.CastVote, target: chosen);
            Assert.That(afterPlayer.votes.Single(v => v.voterId == s.playerId).targetId, Is.EqualTo(chosen));
            string npc = EpisodeEngine.Voters(afterPlayer).First(v => !v.isPlayer).id;
            string projectedBefore = Json(EpisodeEngine.ProjectBallot(afterPlayer, npc));
            string heldChoice = afterPlayer.nominees[0];
            afterPlayer.votes.Add(new VoteState { voterId = npc, targetId = heldChoice, reason = "An already committed private choice." });
            Valid(afterPlayer);
            Assert.That(Json(EpisodeEngine.ProjectBallot(afterPlayer, npc)), Is.EqualTo(projectedBefore),
                "Casting a private ballot must not remove a speech term from projections.");
            var resolved = Apply(afterPlayer, EpisodeCommandKind.Advance);
            Assert.That(resolved.votes.Single(v => v.voterId == s.playerId).targetId, Is.EqualTo(chosen));
            Assert.That(resolved.votes.Single(v => v.voterId == npc).targetId, Is.EqualTo(heldChoice));
            Assert.That(resolved.votes.Single(v => v.voterId == npc).reason, Is.EqualTo("An already committed private choice."));
        }

        [Test]
        public void APlayerHohTieBreakIsStillAnExplicitUnmodifiedChoice()
        {
            var s = House(7, playerOnBlock: false); s.hohId = s.playerId;
            s = Apply(s, EpisodeCommandKind.Advance);
            var voters = EpisodeEngine.Voters(s).ToArray(); Assert.That(voters, Has.Length.EqualTo(4));
            for (int i = 0; i < voters.Length; i++)
                s.votes.Add(new VoteState { voterId = voters[i].id, targetId = s.nominees[i % 2], reason = "A private tied ballot." });
            Valid(s); Assert.That(EpisodeEngine.NeedsPlayerTieBreak(s), Is.True);
            string selected = s.nominees[1];
            var voted = Apply(s, EpisodeCommandKind.CastVote, target: selected);
            var resolved = Apply(voted, EpisodeCommandKind.Advance);
            Assert.That(resolved.votes.Single(v => v.voterId == s.playerId).targetId, Is.EqualTo(selected));
            Assert.That(resolved.Find(selected).status, Is.EqualTo(ContestantStatus.Jury));
        }

        [Test]
        public void HiddenPayoffAndSusceptibilityNeverChangeThePublicReadOrItsUnknownCount()
        {
            var s = Deliver(House()); string voter = Npc(s, 2);
            s.Find(voter).traits = new List<string> { "Social", "Loyal", "Emotional" };
            var receptive = Isolate(s, voter);
            var first = WebEvictionVoting.Evaluate(receptive);
            Assert.That(Speech(first, s.playerId).value, Is.EqualTo(4));
            string read = Json(VoteRead.ReadVoter(s, voter, first));
            foreach (string condition in new[] { "firm", "unheard", "opponent-ally", "hostile", "quiet", "no-receipt" })
            {
                var options = Isolate(s, voter, condition == "firm" ? 25 : 0);
                if (condition == "firm") options.obligations[0].code = "threat"; // An explicitly hidden baseline term.
                if (condition == "unheard") options.speechAppeals[0].heard = false;
                if (condition == "opponent-ally") options.speechAppeals[0].opponentAlly = true;
                if (condition == "hostile") options.voter.traits = new List<string> { "Strategic", "Analytical", "Manipulative", "Stubborn", "Strategic" };
                if (condition == "quiet") options.speechAppeals[0].approach = BlockSpeeches.Quiet;
                if (condition == "no-receipt") options.speechAppeals.Clear();
                var result = WebEvictionVoting.Evaluate(options);
                foreach (string nominee in s.nominees)
                {
                    Assert.That(Speech(result, nominee).visibility, Is.EqualTo("private"));
                    Assert.That(VoteRead.FactorKnown(s, voter, nominee, Speech(result, nominee)), Is.False);
                }
                // The firm fixture's one additional private baseline term is deliberately removed
                // before comparing the read, leaving only the resulting speech susceptibility.
                if (condition == "firm") foreach (var n in result.nomineeEvaluations) n.factors.RemoveAll(f => f.code == "threat");
                Assert.That(Json(VoteRead.ReadVoter(s, voter, result)), Is.EqualTo(read), condition);
                Assert.That(result.publicReasonCodes, Does.Not.Contain(BlockSpeeches.FactorCode));
            }
            var known = VoteRead.ReadVoter(s, voter, first);
            Assert.That(known.unknownTerms, Is.EqualTo(1));
            Assert.That(known.knownTerms, Does.Not.Contain(BlockSpeeches.FactorCode));
        }

        [Test]
        public void FixedUnknownSpeechPresenceDoesNotRequireASpeechOrRevealWhichNpcHasVoted()
        {
            var s = House(); string voter = Npc(s, 2);
            var none = WebEvictionVoting.Evaluate(Isolate(s, voter));
            Assert.That(none.nomineeEvaluations.Select(n => Speech(none, n.nomineeId).value), Is.All.EqualTo(0));
            var read = VoteRead.ReadVoter(s, voter, none);
            Assert.That(read.unknownTerms, Is.EqualTo(1));
            Assert.That(read.knownTerms, Does.Not.Contain(BlockSpeeches.FactorCode));
        }

        [TestCase("unknown-approach")] [TestCase("text-mismatch")] [TestCase("quiet-nonblank")]
        [TestCase("wrong-phase")] [TestCase("wrong-speaker")] [TestCase("missing-speech")]
        [TestCase("audience-duplicate")] [TestCase("audience-unknown")] [TestCase("audience-missing")]
        [TestCase("audience-reordered")] [TestCase("duplicate-receipt")] [TestCase("legacy")]
        [TestCase("week-off")] [TestCase("before-week-rules")] [TestCase("strategy-off")]
        [TestCase("agency-off")] [TestCase("levers-off")]
        [TestCase("before-speeches")] [TestCase("campaign-stage")]
        public void MalformedTypedReceiptsCannotBecomeSavedVotingAuthority(string boundary)
        {
            var s = Deliver(House()); var receipt = BlockSpeeches.Receipt(s, s.evictionSpeeches.Single());
            switch (boundary)
            {
                case "unknown-approach": receipt.kind = BlockSpeeches.EventPrefix + "buy-votes"; break;
                case "text-mismatch": receipt.text += " Different text."; break;
                case "quiet-nonblank": receipt.kind = BlockSpeeches.EventPrefix + BlockSpeeches.Quiet; break;
                case "wrong-phase": receipt.phase = EpisodePhase.Campaign; break;
                case "wrong-speaker": receipt.audienceIds.Reverse(); break;
                case "missing-speech": s.evictionSpeeches.Clear(); break;
                case "audience-duplicate": receipt.audienceIds[2] = receipt.audienceIds[1]; break;
                case "audience-unknown": receipt.audienceIds[2] = "not-in-the-house"; break;
                case "audience-missing": receipt.audienceIds.RemoveAt(2); break;
                case "audience-reordered":
                    string old = receipt.audienceIds[1]; receipt.audienceIds[1] = receipt.audienceIds[2]; receipt.audienceIds[2] = old; break;
                case "duplicate-receipt": var copy = receipt.Clone(); copy.sequence = s.nextSequence++; s.events.Add(copy); break;
                case "legacy": s.economyRulesVersion = 0; break;
                case "week-off": s.weekRulesStartWeek = 0; break;
                case "before-week-rules": s.weekRulesStartWeek = 2; break;
                case "strategy-off": s.strategyRulesStartWeek = 0; break;
                case "agency-off": s.agencyRulesStartWeek = 0; break;
                case "levers-off": s.leverRulesStartWeek = 0; break;
                case "before-speeches": s.evictionStage = EvictionStage.Interaction; break;
                case "campaign-stage": s.phase = EpisodePhase.Campaign; s.evictionStage = EvictionStage.Interaction; break;
            }
            string before = Json(s);
            Assert.That(EpisodeValidation.TryValidate(s, out _), Is.False, boundary);
            Assert.Throws<ArgumentException>(() => new EpisodeEngine(s));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(8)] [TestCase(16)]
        public void CurrentSpeechReceiptsSurviveFullHistoryVotingResultsAndSocialThenUnpinNextWeek(int size)
        {
            var s = Deliver(House(size));
            int sequence = BlockSpeeches.Receipt(s, s.evictionSpeeches.Single()).sequence;
            FillHistoryBehindReceipts(s);
            Assert.That(s.events[0].sequence, Is.EqualTo(sequence));
            var voting = Apply(s, EpisodeCommandKind.Advance);
            Assert.That(voting.events, Has.Count.EqualTo(256));
            Assert.That(voting.events.Any(e => e.sequence == sequence), Is.True);
            Assert.That(voting.evictionSpeeches.All(speech => BlockSpeeches.Receipt(voting, speech) != null), Is.True);
            var resolved = Apply(voting, EpisodeCommandKind.Advance);
            Assert.That(resolved.evictionResolved, Is.True);
            Assert.That(resolved.evictionSpeeches.All(speech => BlockSpeeches.ProtectedReceipt(resolved, BlockSpeeches.Receipt(resolved, speech))), Is.True);
            var social = Apply(resolved, EpisodeCommandKind.Advance);
            Assert.That(social.phase, Is.EqualTo(EpisodePhase.Social));
            Assert.That(social.week, Is.EqualTo(s.week));
            Assert.That(social.events.Any(e => e.sequence == sequence), Is.True);
            var engine = new EpisodeEngine(RoundTrip(social));
            for (int i = 0; i < 8 && engine.Snapshot.week == s.week; i++)
            {
                var step = engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot));
                Assert.That(step.accepted, Is.True, step.reason);
            }
            var next = engine.Snapshot;
            Assert.That(next.week, Is.EqualTo(s.week + 1));
            Assert.That(next.evictionSpeeches, Is.Empty);
            Assert.That(next.events.Any(e => e.sequence == sequence), Is.False, "Cleared speeches no longer pin last week's oldest events.");
            Valid(next);
        }

        [Test]
        public void HistoricalReceiptsRemainValidButAreInertAfterTheirSpeechRowsAreCleared()
        {
            var s = Deliver(House()); var receipt = BlockSpeeches.Receipt(s, s.evictionSpeeches.Single()).Clone();
            s.week++;
            s.evictionSpeeches.Clear();
            Valid(s);
            Assert.That(BlockSpeeches.ProtectedReceipt(s, receipt), Is.False);
            var evaluation = WebEvictionVoting.Evaluate(Isolate(s, Npc(s, 2)));
            Assert.That(evaluation.nomineeEvaluations.Select(n => Speech(evaluation, n.nomineeId).value), Is.All.EqualTo(0));
            string before = Json(s);
            Assert.That(Json(RoundTrip(s)), Is.EqualTo(before));
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [Test]
        public void SpeechReadsDoNotConsumeEvenAnInjectedTieBreakerUntilTheFinalScoresTie()
        {
            var s = Deliver(House()); string voter = Npc(s, 2);
            s.Find(voter).traits = new List<string>();
            var options = Isolate(s, voter);
            int calls = 0; string before = Json(s);
            var decided = WebEvictionVoting.Evaluate(options, () => { calls++; return 0.25; });
            Assert.That(Speech(decided, s.playerId).value, Is.EqualTo(1));
            Assert.That(calls, Is.Zero, "The speech-free tie is only a baseline, never a preliminary ballot.");
            options.obligations.Add(new WebVoteObligation { nomineeId = s.playerId, code = "fixture-baseline", value = -1 });
            WebEvictionVoting.Evaluate(options, () => { calls++; return 0.25; });
            Assert.That(calls, Is.EqualTo(1), "Exactly the existing final-score tie draw remains.");
            Assert.That(Json(s), Is.EqualTo(before));
        }

        [TestCase(8)] [TestCase(16)]
        public void TypedPublicSpeechKeepsItsSpeakerAndJuryQuoteAfterTheRealWeekRollsOver(int size)
        {
            var s = Apply(House(size, playerOnBlock: false), EpisodeCommandKind.Advance);
            var recorded = s.evictionSpeeches.Select(x => x.Clone()).ToArray();
            foreach (var speech in recorded)
            {
                var receipt = BlockSpeeches.Receipt(s, speech);
                Assert.That(StoryText.Log(s, receipt), Is.EqualTo(s.Find(speech.speakerId).name + ": " + speech.text));
                Assert.That(JuryHouseRead.Plea(s, speech.speakerId)?.words, Is.EqualTo(speech.text));
            }
            s = Apply(s, EpisodeCommandKind.CastVote, target: s.nominees[0]);
            s = Apply(s, EpisodeCommandKind.Advance);
            s = Apply(s, EpisodeCommandKind.Advance);
            int oldWeek = s.week;
            var engine = new EpisodeEngine(s);
            for (int i = 0; i < 8 && engine.Snapshot.week == oldWeek; i++)
            {
                var result = engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot));
                Assert.That(result.accepted, Is.True, result.reason);
            }
            var next = engine.Snapshot;
            Assert.That(next.week, Is.EqualTo(oldWeek + 1));
            Assert.That(next.evictionSpeeches, Is.Empty);
            Valid(next);
            foreach (var speech in recorded)
                Assert.That(JuryHouseRead.Plea(next, speech.speakerId)?.words, Is.EqualTo(speech.text));
        }
    }
}
