using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The seasons the rules-2 HUD's PlayMode tests (EpisodePlayModeTests.DialogueRules2Hud) are installed from, proven
    /// headlessly first (DIALOGUE-PLAN D2's HUD lines; no PlayMode test reached a rules-2 conversation before them).
    ///
    /// <para>Each witness is a shipped season of a pinned seed and house size (SeasonBuilder.Create, then
    /// ShippedRules.ApplyFresh), enrolled on dialogue rules 2 by the literal 2, as every rules-2 test enrols it, and walked a
    /// pinned number of steps by the PlayMode suite's own public walk: EpisodePlayModeTests.NextCommand, copied here command
    /// for command, ids included. Where a test needs it the walk then answers the house's asks with their own neutral exits
    /// and asks voters where their vote is, through EpisodePlayModeTests.DialogueUiCommand's ids. The PlayMode installers
    /// replay exactly these walks instead of searching; each case here asserts the stop state its PlayMode test needs and
    /// what entering the conversation there draws, through the HUD's own public plans. Past the enrolment and the story
    /// rules' hold at threads (<see cref="Start"/>) nothing is assigned by hand, and nothing reads the chip's version. Which
    /// houseguest is the voter is derived, never named.</para>
    ///
    /// <para>The public rescue has no walked witness: in 1,800 walked seasons (seeds 1 to 300, houses of 8, 10 and 12, this
    /// walk and EpisodeEngineTests.NextCommand's) no houseguest ever used the veto to save the player. Its season is the
    /// retained carrier the rules-1 citing test loads (week 5; the voter saved the player in week 2), read here through
    /// the very literals the PlayMode installer lifts it with, and enrolled on rules 2.</para>
    ///
    /// <para>The two refusals only rules 2 gives, <see cref="DialogueRules.BudgetRefusal"/> (32 started conversations) and
    /// <see cref="DialogueRules.RoomRefusal"/> (92 storylines or 396 house events), have no witness: no walked season of 64
    /// (houses of 8 and 12, seeds 1 to 32) came near them (at most 29 storylines and 35 house events), and a conversation
    /// needs the player nominated, so no season can start 32. A hand-built history that long would not be a lawful season.</para>
    /// </summary>
    public sealed class DialogueRules2HudWitnessTests
    {
        private const string HouseMoment = "Answer the existing house moment first.";

        /// <summary>(a) The fallback plea: the plain walk of seed 2, a house of eight, 26 steps: week 2's nominated Campaign.</summary>
        internal const uint FallbackSeed = 2;
        internal const int FallbackHouse = 8, FallbackSteps = 26;

        /// <summary>(b) The voter's word: the plain walk of seed 1, a house of eight, 10 steps: week 1's nominated Campaign, then the asks.</summary>
        internal const uint WordSeed = 1;
        internal const int WordHouse = 8, WordSteps = 10;

        /// <summary>(d) The densest four-option reply: the asking walk of seed 28, a house of eight, 25 steps: week 2's nominated Campaign.</summary>
        internal const uint LieSeed = 28;
        internal const int LieHouse = 8, LieSteps = 25;

        // ---------------------------------------------------------------- the PlayMode suite's walk, copied

        /// <summary>
        /// The shipped season of a seed, enrolled on dialogue rules 2 by the literal 2, and held at story rules 9, threads, by
        /// name (STORY-RULES-10-PLAN §8): the pinned walks were proven on the story rules every new season started on before
        /// story rules 10's enrolment, and Blame changes what a houseguest's used veto writes, which a walk into week 2 may
        /// meet. The PlayMode installers hold it the same way.
        /// </summary>
        internal static EpisodeState Start(uint seed, int house)
        {
            var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = house }, seed);
            ShippedRules.ApplyFresh(fresh);
            Assert.That(fresh.dialogueRulesVersion, Is.Zero, "A shipped season starts on dialogue rules 0.");
            fresh.story.rulesVersion = StoryRules.Threads;
            fresh.dialogueRulesVersion = 2;
            Assert.That(fresh.schemaVersion, Is.GreaterThanOrEqualTo(DialogueRules.Rules2Schema), "Rules 2 is defined from schema 30.");
            Assert.That(DialogueRules.On(fresh), Is.True);
            return fresh;
        }

        /// <summary>EpisodePlayModeTests.NextCommand, command for command and id for id: the walk the PlayMode installers replay.</summary>
        internal static EpisodeCommand PlayModeNext(EpisodeState state)
        {
            var command = new EpisodeCommand
            {
                id = "play-mode-" + state.revision, actorId = state.playerId,
                expectedRevision = state.revision, expectedPhase = state.phase, kind = EpisodeCommandKind.Advance
            };
            if (state.pendingDiary != null)
            {
                command.kind = EpisodeCommandKind.SkipDiary;
                command.targetId = state.pendingDiary.id;
                return command;
            }
            if (EpisodeEngine.IsCompetition(state.phase) && !state.competitionResolved && EpisodeEngine.CompetitionPlayers(state).Any(actor => actor.isPlayer))
            {
                command.kind = EpisodeCommandKind.Compete;
                command.performance = 0.5;
            }
            else if (state.phase == EpisodePhase.Nomination && state.nominees.Count == 0 && state.hohId == state.playerId)
            {
                command.kind = EpisodeCommandKind.Nominate;
                var candidates = EpisodeEngine.NominationCandidates(state).ToArray();
                command.targetId = candidates[0].id;
                command.secondTargetId = candidates[1].id;
            }
            else if (state.phase == EpisodePhase.VetoMeeting && !state.vetoResolved
                && (state.vetoHolderId == state.playerId || state.hohId == state.playerId && EpisodeEngine.NpcVetoSave(state) != null))
            {
                command.kind = EpisodeCommandKind.ResolveVeto;
                var replacement = EpisodeEngine.ReplacementCandidates(state).FirstOrDefault();
                command.useVeto = replacement != null && !EpisodeEngine.VetoIsLockedAtFinalFour(state);
                command.targetId = command.useVeto ? state.vetoHolderId == state.playerId ? state.nominees[0] : EpisodeEngine.NpcVetoSave(state) : null;
                command.secondTargetId = replacement?.id;
            }
            else if (state.phase == EpisodePhase.Eviction && state.evictionStage == EvictionStage.Speeches
                && state.nominees.Contains(state.playerId)
                && !state.evictionSpeeches.Any(speech => speech.speakerId == state.playerId))
            {
                command.kind = EpisodeCommandKind.SubmitEvictionSpeech;
                command.text = "";
            }
            else if (state.phase == EpisodePhase.Eviction && !state.evictionResolved
                && (state.evictionStage == EvictionStage.Voting || state.evictionStage == EvictionStage.Tiebreaker)
                && !state.votes.Any(vote => vote.voterId == state.playerId)
                && (EpisodeEngine.Voters(state).Any(actor => actor.isPlayer) || EpisodeEngine.NeedsPlayerTieBreak(state)))
            {
                command.kind = EpisodeCommandKind.CastVote;
                command.targetId = state.nominees[0];
            }
            else if (state.phase == EpisodePhase.FinalEviction && state.hohId == state.playerId)
            {
                command.kind = EpisodeCommandKind.FinalEvict;
                command.targetId = state.Active.First(actor => !actor.isPlayer).id;
            }
            else if (state.phase == EpisodePhase.JuryQuestioning && !state.juryExchanges[state.juryQuestionIndex].completed)
            {
                var exchange = state.juryExchanges[state.juryQuestionIndex];
                command.kind = EpisodeCommandKind.AnswerJury;
                command.targetId = exchange.finalistId == state.playerId ? exchange.questionerId : exchange.finalistId;
                command.secondTargetId = exchange.finalistId == state.playerId ? "A" : "neutral";
            }
            else if (state.phase == EpisodePhase.FinalSpeeches && state.Active.Any(actor => actor.isPlayer)
                && !state.finalSpeeches.Any(speech => speech.speakerId == state.playerId))
            {
                command.kind = EpisodeCommandKind.SubmitSpeech;
                command.text = "";
            }
            else if (state.phase == EpisodePhase.Jury && !state.Active.Any(actor => actor.isPlayer)
                && !state.votes.Any(vote => vote.voterId == state.playerId))
            {
                command.kind = EpisodeCommandKind.CastVote;
                command.targetId = state.Active.First().id;
            }
            return command;
        }

        /// <summary>EpisodePlayModeTests.DialogueUiCommand: the installers' own command ids.</summary>
        internal static EpisodeCommand FixtureCommand(EpisodeState s, EpisodeCommandKind kind) => new EpisodeCommand
        {
            id = "dialogue-ui-fixture-" + s.revision + "-" + kind, actorId = s.playerId,
            expectedRevision = s.revision, expectedPhase = s.phase, kind = kind
        };

        internal static EpisodeState Apply(EpisodeEngine engine, EpisodeCommand command)
        {
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True, command.kind + ": " + result.reason);
            Assert.That(result.duplicate, Is.False);
            Assert.That(EpisodeValidation.TryValidate(result.state, out string error), Is.True, error);
            return result.state;
        }

        /// <summary>The plain walk: <paramref name="steps"/> commands of the PlayMode walk from the enrolled start.</summary>
        internal static EpisodeEngine PlainWalk(uint seed, int house, int steps)
        {
            var engine = new EpisodeEngine(Start(seed, house));
            for (int step = 0; step < steps; step++) Apply(engine, PlayModeNext(engine.Snapshot));
            return engine;
        }

        /// <summary>
        /// The asking walk: the plain walk, except that the first time it meets each week's Campaign with a vote to ask about
        /// it answers the house's asks and asks every voter where their vote is, so the reveal can catch a lie told to the
        /// player. At step <paramref name="steps"/> it stops in that Campaign with the asks answered and no voter asked yet.
        /// </summary>
        internal static EpisodeEngine AskingWalk(uint seed, int house, int steps)
        {
            var engine = new EpisodeEngine(Start(seed, house));
            var asked = new HashSet<int>();
            for (int step = 0; ; step++)
            {
                var s = engine.Snapshot;
                bool fresh = s.phase == EpisodePhase.Campaign && VoteRead.Available(s) && !s.evictionResolved && asked.Add(s.week);
                if (fresh) CloseAsks(engine);
                if (step == steps)
                {
                    Assert.That(fresh, Is.True, "The asking walk stops where it first meets a Campaign.");
                    return engine;
                }
                if (fresh)
                    foreach (string voter in NpcVoters(engine.Snapshot).ToArray())
                        if (!EpisodeEngine.AskedThisWeek(engine.Snapshot, voter)) AskVote(engine, voter);
                Apply(engine, PlayModeNext(engine.Snapshot));
            }
        }

        /// <summary>The house's asks answered with their own neutral exits, never a dialogue conversation's.</summary>
        internal static void CloseAsks(EpisodeEngine engine)
        {
            for (int answer = 0; answer < 24; answer++)
            {
                var s = engine.Snapshot;
                var item = s.houseEvents.FirstOrDefault(e => !e.resolved && HouseEventKind.Asks(e.kind));
                if (item == null) return;
                Assert.That(item.contentId == null || (!item.contentId.StartsWith(DialogueCampaignWord.TemplateId + ":", StringComparison.Ordinal)
                    && !item.contentId.StartsWith(DialogueCampaignV1.TemplateId + ":", StringComparison.Ordinal)), Is.True,
                    "Cleanup answers the house's asks, never a conversation.");
                var command = FixtureCommand(s, item.IsStory ? EpisodeCommandKind.ProgressStoryline : EpisodeCommandKind.ResolveHouseEvent);
                command.targetId = item.id;
                if (item.IsStory)
                {
                    var lapse = item.choices.Single(c => c.optionId == item.lapseOptionId);
                    Assert.That(lapse.locked || lapse.costsAction || lapse.pickPerson, Is.False, "The neutral exit costs nothing.");
                    command.secondTargetId = lapse.optionId;
                }
                else command.text = item.choices.First().label;
                Apply(engine, command);
            }
            Assert.Fail("The house's asks did not close within 24 neutral answers.");
        }

        internal static void AskVote(EpisodeEngine engine, string voter)
        {
            var command = FixtureCommand(engine.Snapshot, EpisodeCommandKind.AskVote);
            command.targetId = voter;
            Apply(engine, command);
        }

        internal static IEnumerable<string> NpcVoters(EpisodeState s) => EpisodeEngine.Voters(s).Where(c => !c.isPlayer).Select(c => c.id);

        internal static int Remaining(EpisodeState s) => EpisodeEngine.SocialActionBudget(s) - EpisodeEngine.SocialActionsSpent(s);

        /// <summary>The claim the voter told the player this week, or null.</summary>
        internal static ClaimRow Told(EpisodeState s, string voter) =>
            s.ledger.claims.LastOrDefault(k => k.week == s.week && k.voterId == voter && k.source == ClaimSource.Told);

        /// <summary>The HUD's "Desperate plea" press while the conversation can start: TryBuildEntryCommand's own plan.</summary>
        private static EpisodeState Enter(EpisodeEngine engine, string voter)
        {
            var s = engine.Snapshot;
            Assert.That(DialogueRules.PleaWithoutConversation(s, voter, out string why), Is.False, "The press is the conversation: " + why);
            Assert.That(DialogueRules.TryBuildEntryCommand(s, voter, out var plan), Is.True, "The HUD's own entry plan.");
            Assert.That(plan.text, Is.EqualTo(DialogueCampaignWord.TemplateId), "Rules 2 opens the vote-word tree.");
            plan.id = "dialogue-r2-hud-entry-" + s.revision;
            var entered = Apply(engine, plan);
            Assert.That(Remaining(entered), Is.EqualTo(Remaining(s) - 1), "The entry is the plea's one action.");
            return entered;
        }

        /// <summary>A tile's press on the open conversation's node: TryBuildChoiceCommand's own plan.</summary>
        private static EpisodeState Choose(EpisodeEngine engine, string option)
        {
            var s = engine.Snapshot;
            var cycle = DialogueRules.Cycle(s, DialogueRules.Active(s));
            Assert.That(DialogueRules.TryBuildChoiceCommand(s, cycle.eventId, option, out var plan), Is.True, option + " at " + cycle.beatId);
            plan.id = "dialogue-r2-hud-choice-" + s.revision;
            return Apply(engine, plan);
        }

        private static HouseEventState Node(EpisodeState s, out StorylineState cycle)
        {
            var open = DialogueRules.Active(s);
            Assert.That(open, Is.Not.Null, "A conversation is open.");
            cycle = DialogueRules.Cycle(s, open);
            Assert.That(cycle.templateId, Is.EqualTo(DialogueCampaignWord.TemplateId));
            string eventId = cycle.eventId;
            return s.houseEvents.Single(e => e.id == eventId);
        }

        /// <summary>The words EpisodeDirector.StatementLine draws for the voter's word.</summary>
        internal static string StatementLine(EpisodeState s, DialogueRules.StatementView word) => word.TargetId == s.playerId
            ? s.Find(word.SpeakerId).name + " told you this week they are voting you out."
            : s.Find(word.SpeakerId).name + " told you this week they are voting out " + s.Find(word.TargetId).name + ".";

        // ---------------------------------------------------------------- (a) the fallback plea

        /// <summary>
        /// (a) A nominated Campaign whose conversations wait on a house moment, with two actions left: the panel's desperate
        /// plea there is the ordinary one-shot plea, saying why in rules 2's own words, and its press is the chip-off season's
        /// press field for field (the PlayMode test presses it through the HUD and compares the same twin).
        /// </summary>
        [Test]
        public void TheFallbackWitnessIsANominatedCampaignWhoseConversationsWaitOnAHouseMoment()
        {
            if (DialogueRules2Season.HeldBelowItsSchema()) return;
            var engine = PlainWalk(FallbackSeed, FallbackHouse, FallbackSteps);
            var s = engine.Snapshot;
            Assert.That((s.week, s.phase, s.evictionResolved), Is.EqualTo((2, EpisodePhase.Campaign, false)), "Week 2's Campaign.");
            Assert.That(s.nominees, Does.Contain(s.playerId), "The player is on the block.");
            Assert.That(Remaining(s), Is.GreaterThanOrEqualTo(2), "Two actions are left.");
            var open = s.houseEvents.Where(e => !e.resolved && HouseEventKind.Asks(e.kind)).ToList();
            Assert.That(open, Is.Not.Empty, "A house moment waits on an answer.");
            var waiting = NpcVoters(s)
                .Where(id => StrategyRules.CanLobby(s, id, LobbyAsk.Vote, s.playerId, out _)
                    && !DialogueRules.CanStart(s, id, out string why) && why == HouseMoment
                    && !open.Any(e => e.involvedIds.Contains(id) || e.cast.Any(role => role.contestantId == id)))
                .ToList();
            Assert.That(waiting.Count, Is.GreaterThanOrEqualTo(2), "Voters whose conversation waits on the moment, in none of it themselves.");
            string voter = waiting[0];
            Assert.That(DialogueRules.TryBuildEntryCommand(s, voter, out _), Is.False);
            Assert.That(DialogueRules.PleaWithoutConversation(s, voter, out string reason), Is.True, "Never lose the plea.");
            Assert.That(reason, Is.EqualTo(HouseMoment), "The panel's reason, in CanStart's words.");
            TestContext.Out.WriteLine("Rules-2 fallback witness: seed " + FallbackSeed + ", house " + FallbackHouse + ", " + FallbackSteps
                + " steps, week " + s.week + " campaign, revision " + s.revision + ", " + waiting.Count + " voters waiting, " + Remaining(s) + " actions.");

            // The one-shot press, and the chip-off twin's same press.
            var press = FixtureCommand(s, EpisodeCommandKind.Lobby);
            press.targetId = voter; press.secondTargetId = s.playerId; press.text = LobbyAsk.Encode(LobbyAsk.Vote, LobbyApproach.Emotional);
            var twin = s.Clone();
            twin.dialogueRulesVersion = 0;
            Assert.That(EpisodeValidation.TryValidate(twin, out string error), Is.True, error);
            var off = new EpisodeEngine(twin).Apply(press);
            Assert.That(off.accepted, Is.True, off.reason);
            var after = Apply(engine, press);
            Assert.That(after.dialogue.episodes, Is.Empty, "No conversation that time.");
            Assert.That(Remaining(after), Is.EqualTo(Remaining(s) - 1), "for its one action.");
            var expected = off.state.Clone();
            expected.dialogueRulesVersion = 2;
            Assert.That(DialogueRules2Season.Json(after), Is.EqualTo(DialogueRules2Season.Json(expected)), "Field for field the chip-off season's press.");
        }

        // ---------------------------------------------------------------- (b) the voter's word, for and against

        /// <summary>The word witness's stop: the walk, the asks answered, then every voter who can be talked to asked, in the house's order.</summary>
        internal static EpisodeEngine WordStop(out string word, out string against)
        {
            var engine = PlainWalk(WordSeed, WordHouse, WordSteps);
            var s = engine.Snapshot;
            Assert.That((s.week, s.phase), Is.EqualTo((1, EpisodePhase.Campaign)), "Week 1's Campaign.");
            Assert.That(s.nominees, Does.Contain(s.playerId), "The player is on the block.");
            CloseAsks(engine);
            var talkable = NpcVoters(engine.Snapshot).Where(id => DialogueRules.CanStart(engine.Snapshot, id, out _)).ToArray();
            foreach (string voter in talkable) AskVote(engine, voter);
            var asked = engine.Snapshot;
            word = talkable.FirstOrDefault(id => Told(asked, id) != null && Told(asked, id).targetId != asked.playerId);
            against = talkable.FirstOrDefault(id => Told(asked, id)?.targetId == asked.playerId);
            return engine;
        }

        /// <summary>
        /// (b) Two voters who told the player where their vote is: one for the player (their word is on the reply, and
        /// holding them to it is a tile), one against (the reply says so, and there is no word to hold them to). The
        /// citation is free and draws nothing from the season's stream; the line goes with the reply node; the plea after a
        /// citation is the Strategic plea's; and the second conversation still has an action to open with.
        /// </summary>
        [Test]
        public void TheWordWitnessHasAVoterForThePlayerAndOneAgainstInOneCampaign()
        {
            if (DialogueRules2Season.HeldBelowItsSchema()) return;
            var engine = WordStop(out string word, out string against);
            var s = engine.Snapshot;
            Assert.That(word, Is.Not.Null, "A voter told the player they vote out somebody else.");
            Assert.That(against, Is.Not.Null, "A voter told the player they vote the player out.");
            Assert.That(Remaining(s), Is.GreaterThanOrEqualTo(2), "An action for each conversation.");
            Assert.That(DialogueRules.CanStart(s, word, out string why), Is.True, why);
            Assert.That(DialogueRules.CanStart(s, against, out why), Is.True, why);
            TestContext.Out.WriteLine("Rules-2 word witness: seed " + WordSeed + ", house " + WordHouse + ", " + WordSteps + " steps, week "
                + s.week + " campaign, revision " + s.revision + ", " + Remaining(s) + " actions.");

            var entered = Enter(engine, word);
            var reply = Node(entered, out var cycle);
            Assert.That(DialogueCampaignWord.AnswerOf(cycle.beatId), Is.EqualTo(DialogueCampaignWord.Word), "Their word, as they gave it.");
            Assert.That(reply.choices.Select(c => c.optionId), Is.EqualTo(new[] { DialogueCampaignWord.CiteWord, DialogueCampaignWord.Appeal,
                DialogueCampaignWord.Press, DialogueCampaignWord.Leave }), "Four options, the word first.");
            Assert.That(DialogueRules.TryReadStatement(entered, cycle.eventId, out var view), Is.True, "The HUD's public line reads.");
            Assert.That((view.SpeakerId, view.TargetId, view.Week), Is.EqualTo((word, Told(s, word).targetId, s.week)));
            Assert.That(view.TargetId, Is.Not.EqualTo(s.playerId));
            Assert.That(DialogueRules.TryReadPublicRescue(entered, cycle.eventId, out _), Is.False, "No rescue beside a word.");
            TestContext.Out.WriteLine("The reply's line: " + StatementLine(entered, view));

            var cited = Choose(engine, DialogueCampaignWord.CiteWord);
            Assert.That(Remaining(cited), Is.EqualTo(Remaining(entered)), "The citation is free.");
            Assert.That(cited.randomState, Is.EqualTo(entered.randomState), "and draws nothing from the season's stream.");
            var evidence = DialogueRules.Active(cited).evidence.Single();
            Assert.That(evidence.kind, Is.EqualTo(DialogueCampaignWord.StatementEvidence));
            Node(cited, out cycle);
            Assert.That(cycle.beatId, Does.StartWith(DialogueCampaignWord.FinishPrefix), "At the finish node,");
            Assert.That(DialogueRules.TryReadStatement(cited, cycle.eventId, out _), Is.False, "the line is the reply's alone.");
            var pleaded = Choose(engine, DialogueCampaignWord.Plea);
            Assert.That(DialogueRules.Active(pleaded), Is.Null, "The plea closes the conversation,");
            Assert.That(pleaded.lobbies.Single(l => l.week == pleaded.week && l.deciderId == word).approach, Is.EqualTo(LobbyApproach.Strategic),
                "as the Strategic plea after a citation,");
            Assert.That(Remaining(pleaded), Is.EqualTo(Remaining(entered)), "for no further action.");

            var second = Enter(engine, against);
            reply = Node(second, out cycle);
            Assert.That(DialogueCampaignWord.AnswerOf(cycle.beatId), Is.EqualTo(DialogueCampaignWord.Against), "They tell the player straight.");
            Assert.That(reply.choices.Any(c => c.optionId == DialogueCampaignWord.CiteWord), Is.False, "There is no word to hold them to.");
            Assert.That(DialogueRules.TryReadStatement(second, cycle.eventId, out view), Is.True);
            Assert.That(view.TargetId, Is.EqualTo(s.playerId));
            TestContext.Out.WriteLine("The second reply's line: " + StatementLine(second, view) + " Options: "
                + string.Join(", ", reply.choices.Select(c => c.optionId)) + ".");
            var left = Choose(engine, DialogueCampaignWord.Leave);
            Assert.That(DialogueRules.Active(left), Is.Null, "Leaving ends it.");
            Assert.That(left.storylines.Single(c => c.id == cycle.id).endingId, Is.EqualTo("neutral"));
        }

        // ---------------------------------------------------------------- (d) the densest four-option reply

        /// <summary>The lie witness's stop: the asking walk, then the voter the reveal caught lying asked where their vote is.</summary>
        internal static EpisodeEngine LieStop(out string liar)
        {
            var engine = AskingWalk(LieSeed, LieHouse, LieSteps);
            var s = engine.Snapshot;
            Assert.That((s.week, s.phase, s.evictionResolved), Is.EqualTo((2, EpisodePhase.Campaign, false)), "Week 2's Campaign.");
            Assert.That(s.nominees, Does.Contain(s.playerId), "The player is on the block.");
            var liars = NpcVoters(s).Where(id => DialogueRules.CanStart(s, id, out _)
                && s.ledger.claims.Any(k => k.voterId == id && k.week < s.week && k.status == ClaimStatus.Lied && k.source == ClaimSource.Told)).ToArray();
            Assert.That(liars, Is.Not.Empty, "A voter the reveal caught lying to the player can be talked to.");
            liar = liars[0];
            AskVote(engine, liar);
            return engine;
        }

        /// <summary>
        /// (d) The tallest reply the HUD draws: a voter the reveal caught lying to the player in week 1 gives their word in
        /// week 2, so the reply offers four tiles - their word, the personal appeal, the lie (some risk, "It can backfire.")
        /// and the exit - with the word's line above them.
        /// </summary>
        [Test]
        public void TheFourOptionWitnessIsACaughtLiarsWordWithTheLieBesideIt()
        {
            if (DialogueRules2Season.HeldBelowItsSchema()) return;
            var engine = LieStop(out string liar);
            var s = engine.Snapshot;
            var claim = Told(s, liar);
            Assert.That(claim, Is.Not.Null, "They told the player where their vote is,");
            Assert.That(claim.targetId, Is.Not.EqualTo(s.playerId), "and it was not the player.");
            Assert.That(Remaining(s), Is.GreaterThanOrEqualTo(1));
            TestContext.Out.WriteLine("Rules-2 four-option witness: seed " + LieSeed + ", house " + LieHouse + ", " + LieSteps + " steps (asking walk), week "
                + s.week + " campaign, revision " + s.revision + ", " + Remaining(s) + " actions.");

            var entered = Enter(engine, liar);
            var reply = Node(entered, out var cycle);
            Assert.That(cycle.beatId, Is.EqualTo(DialogueCampaignWord.ReplyBeat(DialogueCampaignWord.Word, false, true)), "reply-word-lie.");
            Assert.That(reply.choices.Select(c => c.optionId), Is.EqualTo(new[] { DialogueCampaignWord.CiteWord, DialogueCampaignWord.Appeal,
                DialogueCampaignWord.CiteLie, DialogueCampaignWord.Leave }), "Four options.");
            Assert.That(reply.choices.Select(c => c.label), Is.EqualTo(new[] { DialogueCampaignWord.CiteWordLabel, DialogueCampaignWord.AppealLabel,
                DialogueCampaignWord.CiteLieLabel, DialogueCampaignV1.LeaveLabel }));
            Assert.That(reply.choices.Select(c => c.risk), Is.EqualTo(new[] { HouseEventRisk.Low, HouseEventRisk.Low, HouseEventRisk.Medium, HouseEventRisk.Low }));
            var lie = reply.choices.Single(c => c.optionId == DialogueCampaignWord.CiteLie);
            Assert.That(StoryText.Description(entered, reply, lie), Does.EndWith("It can backfire."), "The lie's tile warns, in its description.");
            Assert.That(reply.choices.Any(c => c.locked || c.costsAction), Is.False, "Every reply is free and open.");
            Assert.That(DialogueRules.TryReadStatement(entered, cycle.eventId, out var view), Is.True, "The word's line is drawn above the tiles.");
            TestContext.Out.WriteLine("The reply's line: " + StatementLine(entered, view) + " Descriptions: "
                + string.Join(" | ", reply.choices.Select(c => StoryText.Description(entered, reply, c))));

            var cited = Choose(engine, DialogueCampaignWord.CiteLie);
            Assert.That(Remaining(cited), Is.EqualTo(Remaining(entered)), "Bringing up the lie is free,");
            Assert.That(cited.randomState, Is.EqualTo(entered.randomState), "draws nothing from the season's stream,");
            Assert.That(DialogueRules.Active(cited).evidence.Single().kind, Is.EqualTo(DialogueCampaignWord.MemoryEvidence), "and cites the memory.");
        }

        // ---------------------------------------------------------------- (c) the public rescue

        private const string CarrierSha = "D3E0C73F5A10BCD85ED37766AE462B6435E588DA08D7E2FFAADF7C70F5BD4D71";

        private sealed class PublicFieldsOnly : DefaultContractResolver
        {
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization serialization) =>
                type.GetFields(BindingFlags.Instance | BindingFlags.Public).Select(f => base.CreateProperty(f, MemberSerialization.Fields)).ToList();
        }

        /// <summary>
        /// (c)'s season: the EditMode suite's retained public-route carrier, its bytes and hash checked, lifted to schema 30 by
        /// the literals EpisodePlayModeTests.CiteCarrierAtTheLiveSchema adds (the pooled fields off, the header 30), read
        /// through its public fields only, and enrolled on rules 2 by the literal 2. Nothing else is edited.
        /// </summary>
        internal static EpisodeState RescueCarrier(out string voter)
        {
            string path = Path.Combine(SourceRoot(), "Tests", "EditMode", "Fixtures", "DialoguePublicRescueV2", "preentry-carrier.json");
            Assert.That(File.Exists(path), Is.True, "The reviewed carrier; no generation fallback.");
            var carrier = JObject.Parse(File.ReadAllText(path, new UTF8Encoding(false, true)));
            Assert.That((string)carrier["kind"], Is.EqualTo("immutable-diagnostic-public-field-dto-carrier-not-savewire"));
            byte[] bytes = Convert.FromBase64String((string)carrier["base64"]);
            using (var hash = SHA256.Create())
                Assert.That(BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", ""), Is.EqualTo(CarrierSha));
            JObject lifted;
            using (var reader = new JsonTextReader(new StringReader(new UTF8Encoding(false, true).GetString(bytes)))
                       { DateParseHandling = DateParseHandling.None, FloatParseHandling = FloatParseHandling.Double, MaxDepth = 64 })
            {
                lifted = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                Assert.That(reader.Read(), Is.False, "Exactly one captured JSON object.");
            }
            Assert.That((int)lifted["schemaVersion"], Is.EqualTo(29), "The carrier is a schema-29 capture.");
            lifted.Add("alumniRulesVersion", 0); lifted.Add("alumniCatalogEdition", JValue.CreateNull()); lifted.Add("tuningRulesVersion", 0);
            lifted["schemaVersion"] = 30;
            var s = JsonConvert.DeserializeObject<EpisodeState>(lifted.ToString(Formatting.None), new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None, DateParseHandling = DateParseHandling.None,
                ObjectCreationHandling = ObjectCreationHandling.Replace, FloatParseHandling = FloatParseHandling.Double,
                MaxDepth = 64, ContractResolver = new PublicFieldsOnly(),
            });
            Assert.That((s.week, s.phase, s.dialogueRulesVersion), Is.EqualTo((5, EpisodePhase.Campaign, 0)));
            s.dialogueRulesVersion = 2;
            Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);
            var voters = NpcVoters(s).ToArray();
            Assert.That(voters, Has.Length.EqualTo(1), "The campaign's one voter.");
            voter = voters[0];
            var rescue = s.ledger.power.Single(p => p.week == 2);
            Assert.That((rescue.vetoUsed, rescue.vetoHolderId, rescue.savedId), Is.EqualTo((true, voter, s.playerId)), "They used the veto to save the player in week 2.");
            return s;
        }

        /// <summary>
        /// (c) The voter who used the veto to save the player: rules 2's reply offers the rescue first among four tiles and the
        /// HUD's public record reads there, in the words the PlayMode test draws. Citing it is free and draws nothing from the
        /// season's stream; rules 2 reads the record at the reply alone, so the finish node draws none; and the plea after it is
        /// the Strategic plea's.
        /// </summary>
        [Test]
        public void TheRescueWitnessIsTheRetainedCarrierAndItsReplyDrawsThePublicRecord()
        {
            if (DialogueRules2Season.HeldBelowItsSchema()) return;
            var s = RescueCarrier(out string voter);
            Assert.That(DialogueRules.CanStart(s, voter, out string why), Is.True, why);
            Assert.That(Remaining(s), Is.GreaterThanOrEqualTo(1));
            var engine = new EpisodeEngine(s);
            var entered = Enter(engine, voter);
            var reply = Node(entered, out var cycle);
            Assert.That(DialogueCampaignWord.OffersRescue(cycle.beatId), Is.True, "The rescue is a fact the player holds.");
            Assert.That(reply.choices.Count, Is.EqualTo(4), "Four tiles.");
            Assert.That(reply.choices[0].optionId, Is.EqualTo(DialogueCampaignWord.CiteRescue), "The rescue first,");
            Assert.That(reply.choices.Any(c => c.optionId == DialogueCampaignWord.CiteWord), Is.False, "and no word beside it.");
            Assert.That(DialogueRules.TryReadPublicRescue(entered, cycle.eventId, out var view), Is.True, "The HUD's public line reads.");
            Assert.That((view.Week, view.HolderId, view.SavedId), Is.EqualTo((2, voter, s.playerId)));
            Assert.That(DialogueRules.TryReadStatement(entered, cycle.eventId, out _), Is.False, "No word was given.");
            TestContext.Out.WriteLine("Rules-2 rescue witness: the carrier at week " + s.week + ", revision " + s.revision + ", node " + cycle.beatId
                + " [" + string.Join(", ", reply.choices.Select(c => c.optionId)) + "], " + Remaining(s) + " actions. The line: Week " + view.Week + ": "
                + s.Find(view.HolderId).name + " used the veto to save " + s.Find(view.SavedId).name + "; " + s.Find(view.ReplacementId).name + " went on the block.");

            var cited = Choose(engine, DialogueCampaignWord.CiteRescue);
            Assert.That(Remaining(cited), Is.EqualTo(Remaining(entered)), "The citation is free,");
            Assert.That(cited.randomState, Is.EqualTo(entered.randomState), "draws nothing from the season's stream,");
            var evidence = DialogueRules.Active(cited).evidence.Single();
            Assert.That((evidence.kind, evidence.power.week), Is.EqualTo((DialogueCampaignWord.PowerEvidence, 2)), "and keeps the public record.");
            Node(cited, out cycle);
            Assert.That(cycle.beatId, Does.StartWith(DialogueCampaignWord.FinishPrefix));
            Assert.That(DialogueRules.TryReadPublicRescue(cited, cycle.eventId, out _), Is.False, "Rules 2 reads the record at the reply alone.");
            var pleaded = Choose(engine, DialogueCampaignWord.Plea);
            Assert.That(DialogueRules.Active(pleaded), Is.Null, "The plea closes the conversation,");
            var lobby = pleaded.lobbies.Single(l => l.week == pleaded.week && l.deciderId == voter);
            Assert.That((lobby.ask, lobby.subjectId, lobby.approach), Is.EqualTo((LobbyAsk.Vote, pleaded.playerId, LobbyApproach.Strategic)), "as the Strategic plea,");
            Assert.That(Remaining(pleaded), Is.EqualTo(Remaining(entered)), "for no further action.");
            var path = pleaded.storylines.Single(c => c.id == cycle.id);
            Assert.That(path.path.Select(p => p.optionId), Is.EqualTo(new[] { DialogueCampaignWord.OpenCase, DialogueCampaignWord.CiteRescue, DialogueCampaignWord.Plea }));
            Assert.That(path.endingId, Is.EqualTo("answered"));
        }

        /// <summary>Assets/Gamesim, found upward from the working directory (the Unity project) or the test binary (Tools/SimulationTests).</summary>
        private static string SourceRoot()
        {
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string binary = typeof(DialogueRules2HudWitnessTests).Assembly.Location;
            var starts = new List<string> { Directory.GetCurrentDirectory() };
            if (!string.IsNullOrEmpty(binary)) starts.Add(Path.GetDirectoryName(binary));
            foreach (string start in starts)
            {
                string at = start;
                for (int depth = 0; depth < 12 && !string.IsNullOrEmpty(at); depth++)
                {
                    string candidate = Path.Combine(at, "Assets", "Gamesim");
                    if (File.Exists(Path.Combine(candidate, "Simulation", "SeasonBuilder.cs"))) { found.Add(WithoutLongPathPrefix(Path.GetFullPath(candidate))); break; }
                    string next = Path.GetDirectoryName(at);
                    if (next == at) break;
                    at = next;
                }
            }
            Assert.That(found, Has.Count.EqualTo(1), "Exactly one Assets/Gamesim source tree above the test: " + string.Join(", ", found));
            return found.Single();
        }

        private static string WithoutLongPathPrefix(string path) =>
            path.StartsWith(@"\\?\UNC\", StringComparison.Ordinal) ? @"\\" + path.Substring(8)
            : path.StartsWith(@"\\?\", StringComparison.Ordinal) ? path.Substring(4) : path;
    }
}
