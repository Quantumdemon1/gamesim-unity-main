using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Gamesim.Episode;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The HUD of a conversation on dialogue rules 2 (DIALOGUE-PLAN D2), through the player's real controls, at each text
    /// size: the plea panel when the conversation is refused, the voter's word drawn on the reply, the public rescue drawn on
    /// the reply, and the tallest reply rules 2 draws, four tiles with the word's line above them. Before these no PlayMode
    /// test reached a rules-2 conversation; every Dialogue* installer enrols the literal 1.
    ///
    /// <para>Every season here is a shipped season enrolled on rules 2 by the literal 2, never the chip's version, held at
    /// story rules 9 by name since story rules 10's enrolment (the walks were proven before it), and replays a walk proven
    /// headlessly first: DialogueRules2HudWitnessTests (EditMode, in the subset) pins each seed, house
    /// size and step count, copies this suite's NextCommand command for command, and asserts the stop state each test below
    /// needs. The installers replay the walk and assert the same stop, so a walk that moves fails here in words rather than
    /// searching on blind. The rescue is the EditMode suite's own retained carrier (the rules-1 citing test's), enrolled on
    /// rules 2: no walked season of 1,800 tried met a voter who had used the veto on the player.</para>
    ///
    /// <para>Text size: a reload reads the isolated save root's default preferences and comes back at the standard text, so
    /// the size is chosen after every load, as a player chooses it (ApplyTextSize), and the HUD's scale is asserted. At each
    /// size every label box the panel draws stands at least 1.3 times the largest size its words are drawn at (Inter draws
    /// nothing in a box under 1.21 of it) and draws every glyph: on the plea panel everything from its heading to its way
    /// back (the question, the odds note, the no-talk paragraph, each approach's caption and tag); on a reply everything from
    /// the beat's eyebrow to its last tile (the title, the narrative, the line, "What do you do?", and each tile's caption,
    /// risk word, description and foot). Controls are found by their captions; the house is held, as in the neighbouring
    /// dialogue tests, and a press's revision is measured from a snapshot taken with the conversation's panel already open
    /// (OpenRules2Entry's <c>rules2Opened</c>, the plea panel's <c>pre</c>).</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        // The witnesses, pinned and proven by DialogueRules2HudWitnessTests: (a) the fallback, (b) the word, (d) the four tiles.
        private const uint Rules2FallbackSeed = 2, Rules2WordSeed = 1, Rules2LieSeed = 28;
        private const int Rules2FallbackHouse = 8, Rules2FallbackSteps = 26;
        private const int Rules2WordHouse = 8, Rules2WordSteps = 10;
        private const int Rules2LieHouse = 8, Rules2LieSteps = 25;

        private string rules2Voter, rules2Against;

        /// <summary>The state OpenRules2Entry read with the voter's panel open, before the vote ask: what the entry's press is measured from.</summary>
        private EpisodeState rules2Opened;

        // ---------------------------------------------------------------- (i) the fallback plea panel

        /// <summary>
        /// Owner, 2026-10-25: never lose the plea. In a rules-2 season whose conversations wait on a house moment, the panel
        /// says there is no conversation now and why, in rules 2's own words, the desperate plea is the one-shot plea with its
        /// short tag, and its press is the chip-off season's press field for field. The panel's paragraphs and every plea
        /// row's tag are laid out whole at the text size. The two refusals only rules 2 gives (the season's 32 conversations,
        /// the house's room) have no lawful season to draw them from: DialogueRules2HudWitnessTests says why.
        /// </summary>
        [UnityTest]
        public IEnumerator DialogueCampaign_Rules2_ARefusedConversationLeavesTheOneShotPleaAndSaysWhyAtEveryTextSize(
            [Values(TextSize.Standard, TextSize.Larger, TextSize.Largest)] TextSize size)
        {
            yield return InstallRules2Fallback(size);
            float scale = TextSizes.Scale(size);
            string at = " at the " + TextSizes.Name(size) + " text";
            string voter = rules2Voter, name = director.Snapshot.Find(voter).name;
            yield return TalkTo(voter);
            yield return EnterConversationCategory(EpisodeDirector.GameCategoryCaption);
            var source = director.Snapshot;
            byte[] bytes = File.ReadAllBytes(director.SavePath);
            string ask = EpisodeHud.LobbyAskCaption(source, name, LobbyAsk.Vote, source.playerId);
            ButtonWithCaption(ask).onClick.Invoke();
            yield return null;
            AssertDialogueUiUnchanged(source, bytes);

            string line = EpisodeDirector.NoConversationLine(name, HouseMomentRefusal);
            Assert.That(line, Is.EqualTo("No conversation with " + name + " now. " + HouseMomentRefusal
                + " A desperate plea is the ordinary one-shot plea, for one action."));
            Assert.That(ShownText(), Does.Contain(line), "The panel says why there is no talk, in rules 2's own words" + at + ".");
            Assert.That(ShownText(), Does.Not.Contain("replies and final plea cost no further actions"), "and promises none.");
            string caption = EpisodeHud.LobbyApproachCaption(LobbyApproach.Emotional, true);
            var plea = ButtonWithCaption(caption); // Visible, interactable and reachable: the plea is not locked.
            string word = KnownOdds.Plea(source, voter, LobbyAsk.Vote, source.playerId, LobbyApproach.Emotional).word;
            Assert.That(TagWords(plea), Is.EqualTo(word + " · " + EpisodeDirector.NoConversationTag), "The tag adds only that there is no talk now.");
            Assert.That(TagWords(FindButton(EpisodeHud.LobbyApproachCaption(LobbyApproach.Strategic, true))),
                Is.EqualTo(KnownOdds.Plea(source, voter, LobbyAsk.Vote, source.playerId, LobbyApproach.Strategic).word), "The other approaches are as ever.");

            Assert.That(director.GetComponent<EpisodeHud>().FontScale, Is.EqualTo(scale).Within(.001f), "The panel is drawn" + at + ".");
            var failures = new List<string>();
            Rules2LabelWhole(Rules2ShownLabel(line), "The no-talk paragraph" + at, failures);
            var question = Rules2ShownLabel(ask + ". How do you put it?");
            Rules2LabelWhole(question, "The plea's question" + at, failures);
            foreach (string approach in LobbyApproach.All)
            {
                var row = FindButton(EpisodeHud.LobbyApproachCaption(approach, true));
                PleaTagFits(row, scale, approach + "'s tag" + at, failures);
                var tag = row.transform.Find("Tag");
                Rules2LabelWhole(tag != null ? tag.GetComponentInChildren<TMP_Text>(true) : null, approach + "'s tag" + at, failures);
            }
            // Every label the plea panel draws, from its heading (drawn just above the question) to its way back: the
            // heading, the question, the odds note, the no-talk paragraph, each approach's caption and tag, "Ask something else".
            var panel = question.transform.parent;
            int first = question.transform.GetSiblingIndex() - 1;
            var heading = first >= 0 ? panel.GetChild(first).GetComponent<TMP_Text>() : null;
            Assert.That(heading != null && heading.text == EpisodeDirector.LobbyHeading(source), Is.True,
                "The plea panel's heading stands over its question" + at + ".");
            var back = FindButton(EpisodeHud.LobbyBackCaption);
            Assert.That(back.transform.parent, Is.SameAs(panel), "The way back is the plea panel's last row" + at + ".");
            var panelLabels = Rules2LabelsBetween(panel, first, back.transform.GetSiblingIndex());
            foreach (string copy in LobbyApproach.All.Select(approach => EpisodeHud.LobbyApproachCaption(approach, true))
                .Concat(new[] { EpisodeHud.LobbyBackCaption, line, question.text, heading.text }))
                Assert.That(panelLabels.Count(label => label.text == copy), Is.EqualTo(1), "'" + copy + "' is one of the plea panel's labels" + at + ".");
            Assert.That(panelLabels.Count(label => label.name == EpisodeHud.OddsNoteName), Is.EqualTo(1), "The odds note is one of them" + at + ".");
            foreach (var label in panelLabels) Rules2LabelWhole(label, "The plea panel" + at, failures);
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
            AssertEveryLabelDraws((RectTransform)plea.transform, "The desperate plea's row" + at);

            var pre = director.Snapshot;
            plea.onClick.Invoke();
            yield return Frames(2);
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(pre.revision + 1), "One command.");
            Assert.That(after.dialogue.episodes, Is.Empty, "No conversation that time.");
            Assert.That(DialogueRules.Active(after), Is.Null);
            var lobby = after.lobbies.Last();
            Assert.That((lobby.week, lobby.phase, lobby.deciderId, lobby.ask, lobby.subjectId, lobby.approach),
                Is.EqualTo((pre.week, EpisodePhase.Campaign, voter, LobbyAsk.Vote, pre.playerId, LobbyApproach.Emotional)), "The desperate plea, made.");
            Assert.That(DialogueUiRemaining(after), Is.EqualTo(DialogueUiRemaining(pre) - 1), "for its one action.");

            // Field for field what the chip-off season's same press makes: the same state with dialogue rules 0, the same command.
            var twin = pre.Clone();
            twin.dialogueRulesVersion = 0;
            Assert.That(EpisodeValidation.TryValidate(twin, out string error), Is.True, error);
            var offPress = new EpisodeCommand
            {
                id = after.acceptedCommandIds.Last(), actorId = pre.playerId, expectedRevision = pre.revision, expectedPhase = pre.phase,
                kind = EpisodeCommandKind.Lobby, targetId = voter, secondTargetId = pre.playerId, text = LobbyAsk.Encode(LobbyAsk.Vote, LobbyApproach.Emotional),
            };
            var off = new EpisodeEngine(twin).Apply(offPress);
            Assert.That(off.accepted, Is.True, off.reason);
            var expected = off.state.Clone();
            expected.dialogueRulesVersion = 2;
            AssertEquivalent(expected, after);
        }

        // ---------------------------------------------------------------- (ii) the voter's word

        /// <summary>
        /// The voter who told the player they vote somebody else out: their word is drawn on the reply, in words, above four
        /// tiles with "Hold them to their word" first, and again after a reload at the text size chosen again. Holding them to
        /// it is free and draws nothing from the season's stream, the line goes with the reply node, and the plea after it is
        /// the Strategic plea's. Then the voter who told the player they vote the player out: the reply says so, with no word
        /// to hold them to. Each line and every tile laid out whole at the text size.
        /// </summary>
        [UnityTest]
        public IEnumerator DialogueCampaign_Rules2_TheVotersWordIsDrawnOnTheReplyForAndAgainstYouAtEveryTextSize(
            [Values(TextSize.Standard, TextSize.Larger, TextSize.Largest)] TextSize size)
        {
            yield return InstallRules2Word(size);
            float scale = TextSizes.Scale(size);
            string at = " at the " + TextSizes.Name(size) + " text";
            var before = director.Snapshot;
            var claim = Rules2Told(before, rules2Voter);
            string line = before.Find(rules2Voter).name + " told you this week they are voting out " + before.Find(claim.targetId).name + ".";
            Assert.That(ShownText(), Does.Not.Contain(line), "The word is the conversation's, not the house's.");

            yield return OpenRules2Entry(rules2Voter);
            var entered = director.Snapshot;
            Assert.That(entered.revision, Is.EqualTo(rules2Opened.revision + 1), "One command opened the conversation.");
            Assert.That(DialogueUiRemaining(entered), Is.EqualTo(DialogueUiRemaining(rules2Opened) - 1), "for the plea's one action.");
            var episode = entered.dialogue.episodes.Single();
            var cycle = entered.storylines.Single(c => c.id == episode.id);
            Assert.That(cycle.templateId, Is.EqualTo(DialogueCampaignWord.TemplateId), "Rules 2's vote-word tree.");
            Assert.That(DialogueCampaignWord.AnswerOf(cycle.beatId), Is.EqualTo(DialogueCampaignWord.Word), "They give their word.");
            Assert.That(DialogueRules.TryReadStatement(entered, cycle.eventId, out var view), Is.True);
            Assert.That((view.SpeakerId, view.TargetId), Is.EqualTo((rules2Voter, claim.targetId)));
            AssertRules2Reply(line, 4, DialogueCampaignWord.CiteWordLabel, scale, "The word's reply" + at);
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.EventChoiceCaption(DialogueCampaignWord.CiteRescueLabel)), Is.Null, "No rescue beside a word.");

            yield return ReloadDialogueUiBytes();
            yield return ApplyRules2TextSize(size);
            yield return TalkTo(rules2Voter);
            AssertRules2Reply(line, 4, DialogueCampaignWord.CiteWordLabel, scale, "The word's reply after a reload" + at);

            ButtonWithCaption(EpisodeHud.EventChoiceCaption(DialogueCampaignWord.CiteWordLabel)).onClick.Invoke();
            yield return Frames(2);
            var cited = director.Snapshot;
            Assert.That(cited.revision, Is.EqualTo(entered.revision + 1));
            Assert.That(cited.windowActions, Is.EqualTo(entered.windowActions), "Holding them to their word is free,");
            Assert.That(cited.randomState, Is.EqualTo(entered.randomState), "draws nothing from the season's stream,");
            Assert.That(cited.lobbies.Count, Is.EqualTo(entered.lobbies.Count), "and is no plea.");
            Assert.That(cited.dialogue.episodes.Single().evidence.Single().kind, Is.EqualTo(DialogueCampaignWord.StatementEvidence));
            Assert.That(cited.storylines.Single(c => c.id == episode.id).beatId, Does.StartWith(DialogueCampaignWord.FinishPrefix));
            Assert.That(ShownText(), Does.Not.Contain(line), "The line is the reply's: at the finish node it is gone.");

            ButtonWithCaption(EpisodeHud.EventChoiceCaption(DialogueCampaignV1.PleaLabel)).onClick.Invoke();
            yield return Frames(2);
            var closed = director.Snapshot;
            Assert.That(closed.revision, Is.EqualTo(cited.revision + 1));
            Assert.That(closed.windowActions, Is.EqualTo(cited.windowActions), "The plea the entry paid for costs nothing more.");
            Assert.That(DialogueRules.Active(closed), Is.Null, "The plea closes the conversation.");
            var path = closed.storylines.Single(c => c.id == episode.id);
            Assert.That(path.path.Select(p => p.optionId), Is.EqualTo(new[] { DialogueCampaignWord.OpenCase, DialogueCampaignWord.CiteWord, DialogueCampaignWord.Plea }));
            Assert.That(path.endingId, Is.EqualTo("answered"));
            var lobby = closed.lobbies.Single(l => l.week == closed.week && l.deciderId == rules2Voter);
            Assert.That((lobby.ask, lobby.subjectId, lobby.approach), Is.EqualTo((LobbyAsk.Vote, closed.playerId, LobbyApproach.Strategic)),
                "A plea after a citation is the Strategic plea's roll.");

            // The voter against the player, in the same campaign: the reply says so, and there is no word to hold them to.
            string against = closed.Find(rules2Against).name + " told you this week they are voting you out.";
            Assert.That(Rules2Told(closed, rules2Against).targetId, Is.EqualTo(closed.playerId));
            yield return OpenRules2Entry(rules2Against);
            var second = director.Snapshot;
            Assert.That(second.revision, Is.EqualTo(rules2Opened.revision + 1), "One command opened the second conversation.");
            var secondEpisode = second.dialogue.episodes.Single(e => e.npcId == rules2Against);
            var secondCycle = second.storylines.Single(c => c.id == secondEpisode.id);
            Assert.That(DialogueCampaignWord.AnswerOf(secondCycle.beatId), Is.EqualTo(DialogueCampaignWord.Against), "They tell the player straight.");
            int options = second.houseEvents.Single(e => e.id == secondCycle.eventId).choices.Count;
            AssertRules2Reply(against, options, DialogueCampaignWord.AppealLabel, scale, "The reply against you" + at);
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.EventChoiceCaption(DialogueCampaignWord.CiteWordLabel)), Is.Null, "No word to hold them to.");
            ButtonWithCaption(EpisodeHud.EventChoiceCaption(DialogueCampaignV1.LeaveLabel)).onClick.Invoke();
            yield return Frames(2);
            Assert.That(DialogueRules.Active(director.Snapshot), Is.Null, "Leaving it there ends it.");
            Assert.That(director.Snapshot.storylines.Single(c => c.id == secondEpisode.id).endingId, Is.EqualTo("neutral"));
        }

        // ---------------------------------------------------------------- (iii) the public rescue

        /// <summary>
        /// The voter who used the veto to save the player in week 2: rules 2's reply offers "Remind them of the rescue" and
        /// draws the public record above it, in words, laid out whole at the text size, and again after a reload. Citing it is
        /// free and draws nothing; rules 2 draws the record at the reply node alone (V2 kept it at its finish), and the plea
        /// after it is the Strategic plea's.
        /// </summary>
        [UnityTest]
        public IEnumerator DialogueCampaign_Rules2_ThePublicRescueIsDrawnOnTheReplyAndCitingItIsTheStrategicPleaAtEveryTextSize(
            [Values(TextSize.Standard, TextSize.Larger, TextSize.Largest)] TextSize size)
        {
            yield return InstallRules2Rescue(size);
            float scale = TextSizes.Scale(size);
            string at = " at the " + TextSizes.Name(size) + " text";
            var before = director.Snapshot;
            var rescue = before.ledger.power.Single(p => p.week == 2);
            string record = "Week 2: " + before.Find(rescue.vetoHolderId).name + " used the veto to save "
                + before.Find(rescue.savedId).name + "; " + before.Find(rescue.replacementId).name + " went on the block.";
            Assert.That(ShownText(), Does.Not.Contain(record), "The record is the conversation's, not the house's.");

            yield return OpenRules2Entry(rules2Voter);
            var entered = director.Snapshot;
            Assert.That(entered.revision, Is.EqualTo(rules2Opened.revision + 1), "One command opened the conversation.");
            Assert.That(DialogueUiRemaining(entered), Is.EqualTo(DialogueUiRemaining(rules2Opened) - 1), "for the plea's one action.");
            var episode = entered.dialogue.episodes.Single();
            var cycle = entered.storylines.Single(c => c.id == episode.id);
            Assert.That(cycle.templateId, Is.EqualTo(DialogueCampaignWord.TemplateId), "Rules 2's vote-word tree, not V2's.");
            Assert.That(DialogueCampaignWord.OffersRescue(cycle.beatId), Is.True, "The rescue is a fact the player holds.");
            Assert.That(DialogueRules.TryReadPublicRescue(entered, cycle.eventId, out var view), Is.True, "The HUD's public line reads.");
            Assert.That((view.Week, view.HolderId, view.SavedId), Is.EqualTo((2, rules2Voter, entered.playerId)));
            AssertRules2Reply(record, 4, DialogueCampaignWord.CiteRescueLabel, scale, "The rescue's reply" + at);
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.EventChoiceCaption(DialogueCampaignWord.CiteWordLabel)), Is.Null, "No word beside the rescue.");

            yield return ReloadDialogueUiBytes();
            yield return ApplyRules2TextSize(size);
            yield return TalkTo(rules2Voter);
            AssertRules2Reply(record, 4, DialogueCampaignWord.CiteRescueLabel, scale, "The rescue's reply after a reload" + at);

            ButtonWithCaption(EpisodeHud.EventChoiceCaption(DialogueCampaignWord.CiteRescueLabel)).onClick.Invoke();
            yield return Frames(2);
            var cited = director.Snapshot;
            Assert.That(cited.revision, Is.EqualTo(entered.revision + 1));
            Assert.That(cited.windowActions, Is.EqualTo(entered.windowActions), "The citation is free,");
            Assert.That(cited.randomState, Is.EqualTo(entered.randomState), "draws nothing from the season's stream,");
            Assert.That(cited.lobbies.Count, Is.EqualTo(entered.lobbies.Count), "and is no plea.");
            var evidence = cited.dialogue.episodes.Single().evidence.Single();
            Assert.That(evidence.kind, Is.EqualTo(DialogueCampaignWord.PowerEvidence));
            Assert.That(evidence.power.week, Is.EqualTo(2));
            Assert.That(cited.storylines.Single(c => c.id == episode.id).beatId, Does.StartWith(DialogueCampaignWord.FinishPrefix));
            Assert.That(ShownText(), Does.Not.Contain(record), "Rules 2 draws the record at the reply alone.");

            ButtonWithCaption(EpisodeHud.EventChoiceCaption(DialogueCampaignV1.PleaLabel)).onClick.Invoke();
            yield return Frames(2);
            var closed = director.Snapshot;
            Assert.That(closed.revision, Is.EqualTo(cited.revision + 1));
            Assert.That(closed.windowActions, Is.EqualTo(cited.windowActions), "The plea the entry paid for costs nothing more.");
            Assert.That(DialogueRules.Active(closed), Is.Null, "The plea closes the conversation.");
            var path = closed.storylines.Single(c => c.id == episode.id);
            Assert.That(path.path.Select(p => p.optionId), Is.EqualTo(new[] { DialogueCampaignWord.OpenCase, DialogueCampaignWord.CiteRescue, DialogueCampaignWord.Plea }));
            Assert.That(path.endingId, Is.EqualTo("answered"));
            var lobby = closed.lobbies.Single(l => l.week == closed.week && l.deciderId == rules2Voter);
            Assert.That((lobby.ask, lobby.subjectId, lobby.approach), Is.EqualTo((LobbyAsk.Vote, closed.playerId, LobbyApproach.Strategic)),
                "A cited plea is the Strategic plea's roll.");
        }

        // ---------------------------------------------------------------- (iv) the tallest reply

        /// <summary>
        /// The tallest reply rules 2 draws: a voter the reveal caught lying to the player in week 1 gives their word in week 2,
        /// so the reply draws the word's line above four tiles, two to a row - "Hold them to their word", "Make it personal",
        /// "Bring up the last lie" (some risk; its description ends "It can backfire.") and "Leave it there" - each with its
        /// caption, risk word and description whole at the text size. Bringing up the lie is free and cites the memory.
        /// </summary>
        [UnityTest]
        public IEnumerator DialogueCampaign_Rules2_AFourOptionReplyDrawsEveryTileAndDescriptionWholeAtEveryTextSize(
            [Values(TextSize.Standard, TextSize.Larger, TextSize.Largest)] TextSize size)
        {
            yield return InstallRules2Lie(size);
            float scale = TextSizes.Scale(size);
            string at = " at the " + TextSizes.Name(size) + " text";
            var before = director.Snapshot;
            var claim = Rules2Told(before, rules2Voter);
            string line = before.Find(rules2Voter).name + " told you this week they are voting out " + before.Find(claim.targetId).name + ".";

            yield return OpenRules2Entry(rules2Voter);
            var entered = director.Snapshot;
            Assert.That(entered.revision, Is.EqualTo(rules2Opened.revision + 1), "One command opened the conversation.");
            var episode = entered.dialogue.episodes.Single(e => e.npcId == rules2Voter && e.week == entered.week);
            var cycle = entered.storylines.Single(c => c.id == episode.id);
            Assert.That(cycle.beatId, Is.EqualTo(DialogueCampaignWord.ReplyBeat(DialogueCampaignWord.Word, false, true)), "The word, with the lie beside it.");
            var reply = entered.houseEvents.Single(e => e.id == cycle.eventId);
            Assert.That(reply.choices.Select(c => c.label), Is.EqualTo(new[] { DialogueCampaignWord.CiteWordLabel, DialogueCampaignWord.AppealLabel,
                DialogueCampaignWord.CiteLieLabel, DialogueCampaignV1.LeaveLabel }), "Four tiles.");
            AssertRules2Reply(line, 4, DialogueCampaignWord.CiteLieLabel, scale, "The four-tile reply" + at);
            foreach (var choice in reply.choices)
            {
                var tile = ButtonWithCaption(EpisodeHud.EventChoiceCaption(choice.label));
                var words = tile.GetComponentsInChildren<TMP_Text>().Select(t => t.text).ToList();
                string description = StoryText.Description(entered, reply, choice);
                Assert.That(words, Does.Contain(description), "'" + choice.label + "' says what it means" + at + ".");
                Assert.That(words, Does.Contain(EpisodeHud.RiskTag(choice.risk)), "and how far it could rebound, in a word.");
            }
            var lie = reply.choices.Single(c => c.optionId == DialogueCampaignWord.CiteLie);
            Assert.That(StoryText.Description(entered, reply, lie), Does.EndWith("It can backfire."), "The lie's tile warns in its description,");
            Assert.That(EpisodeHud.RiskTag(lie.risk), Is.EqualTo("some risk"), "and in its risk word.");

            ButtonWithCaption(EpisodeHud.EventChoiceCaption(DialogueCampaignWord.CiteLieLabel)).onClick.Invoke();
            yield return Frames(2);
            var cited = director.Snapshot;
            Assert.That(cited.revision, Is.EqualTo(entered.revision + 1));
            Assert.That(cited.windowActions, Is.EqualTo(entered.windowActions), "Bringing up the lie is free,");
            Assert.That(cited.randomState, Is.EqualTo(entered.randomState), "draws nothing from the season's stream,");
            Assert.That(cited.dialogue.episodes.Single(e => e.id == episode.id).evidence.Single().kind, Is.EqualTo(DialogueCampaignWord.MemoryEvidence),
                "and cites the memory the reveal left.");
        }

        // ---------------------------------------------------------------- installers: the witnesses' walks, replayed

        /// <summary>
        /// The shipped season of a seed enrolled on rules 2 by the literal 2, held at story rules 9, threads, by name as its
        /// witness holds it (DialogueRules2HudWitnessTests.Start, STORY-RULES-10-PLAN §8), walked by this suite's own NextCommand.
        /// </summary>
        private static EpisodeEngine Rules2PlainWalk(uint seed, int house, int steps)
        {
            var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = house }, seed);
            ShippedRules.ApplyFresh(fresh);
            Assert.That(fresh.dialogueRulesVersion, Is.Zero);
            fresh.story.rulesVersion = StoryRules.Threads; // The witnesses' walks were proven before story rules 10's enrolment.
            fresh.dialogueRulesVersion = 2; // The rules-2 enrolment by its literal, never the chip's version.
            Assert.That(DialogueRules.On(fresh), Is.True, "Rules 2 is on at the live schema.");
            var engine = new EpisodeEngine(fresh);
            for (int step = 0; step < steps; step++) DialogueUiApply(engine, NextCommand(engine.Snapshot));
            return engine;
        }

        /// <summary>
        /// The asking walk (DialogueRules2HudWitnessTests.AskingWalk): the plain walk, except that the first time it meets
        /// each week's Campaign with a vote to ask about it answers the house's asks and asks every voter where their vote is.
        /// At step <paramref name="steps"/> it stops in that Campaign with the asks answered and no voter asked yet.
        /// </summary>
        private static EpisodeEngine Rules2AskingWalk(uint seed, int house, int steps)
        {
            var engine = Rules2PlainWalk(seed, house, 0);
            var asked = new HashSet<int>();
            for (int step = 0; ; step++)
            {
                var s = engine.Snapshot;
                bool fresh = s.phase == EpisodePhase.Campaign && VoteRead.Available(s) && !s.evictionResolved && asked.Add(s.week);
                if (fresh) Rules2CloseAsks(engine);
                if (step == steps)
                {
                    Assert.That(fresh, Is.True, "The asking walk stops where it first meets a Campaign.");
                    return engine;
                }
                if (fresh)
                    foreach (string voter in EpisodeEngine.Voters(engine.Snapshot).Where(c => !c.isPlayer).Select(c => c.id).ToArray())
                        if (!EpisodeEngine.AskedThisWeek(engine.Snapshot, voter)) Rules2AskVote(engine, voter);
                DialogueUiApply(engine, NextCommand(engine.Snapshot));
            }
        }

        /// <summary>The house's asks answered with their own neutral exits, never a conversation of either dialogue version.</summary>
        private static void Rules2CloseAsks(EpisodeEngine engine)
        {
            for (int answer = 0; answer < 24; answer++)
            {
                var s = engine.Snapshot;
                var item = s.houseEvents.FirstOrDefault(e => !e.resolved && HouseEventKind.Asks(e.kind));
                if (item == null) return;
                Assert.That(item.contentId == null || (!item.contentId.StartsWith(DialogueCampaignWord.TemplateId + ":", System.StringComparison.Ordinal)
                    && !item.contentId.StartsWith(DialogueCampaignV1.TemplateId + ":", System.StringComparison.Ordinal)), Is.True,
                    "Cleanup answers the house's asks, never a conversation.");
                var command = DialogueUiCommand(s, item.IsStory ? EpisodeCommandKind.ProgressStoryline : EpisodeCommandKind.ResolveHouseEvent);
                command.targetId = item.id;
                if (item.IsStory)
                {
                    var lapse = item.choices.Single(c => c.optionId == item.lapseOptionId);
                    Assert.That(lapse.locked || lapse.costsAction || lapse.pickPerson, Is.False);
                    command.secondTargetId = lapse.optionId;
                }
                else command.text = item.choices.First().label;
                DialogueUiApply(engine, command);
            }
            Assert.Fail("The house's asks did not close within 24 neutral answers; no history was cleared.");
        }

        private static void Rules2AskVote(EpisodeEngine engine, string voter)
        {
            var command = DialogueUiCommand(engine.Snapshot, EpisodeCommandKind.AskVote);
            command.targetId = voter;
            DialogueUiApply(engine, command);
        }

        private static ClaimRow Rules2Told(EpisodeState s, string voter) =>
            s.ledger.claims.LastOrDefault(k => k.week == s.week && k.voterId == voter && k.source == ClaimSource.Told);

        /// <summary>(a)'s stop: week 2's nominated Campaign, a house moment open, two actions, voters whose conversation waits on it.</summary>
        private IEnumerator InstallRules2Fallback(TextSize size)
        {
            HoldTheHouseForTheFixture();
            var s = Rules2PlainWalk(Rules2FallbackSeed, Rules2FallbackHouse, Rules2FallbackSteps).Snapshot;
            Assert.That((s.week, s.phase, s.evictionResolved), Is.EqualTo((2, EpisodePhase.Campaign, false)), "The witness's stop (DialogueRules2HudWitnessTests).");
            Assert.That(s.nominees, Does.Contain(s.playerId));
            Assert.That(DialogueUiRemaining(s), Is.GreaterThanOrEqualTo(2));
            var open = s.houseEvents.Where(e => !e.resolved && HouseEventKind.Asks(e.kind)).ToList();
            Assert.That(open, Is.Not.Empty, "A house moment waits on an answer.");
            var waiting = EpisodeEngine.Voters(s).Where(c => !c.isPlayer).Select(c => c.id)
                .Where(id => StrategyRules.CanLobby(s, id, LobbyAsk.Vote, s.playerId, out _)
                    && !DialogueRules.CanStart(s, id, out string why) && why == HouseMomentRefusal
                    && !open.Any(e => e.involvedIds.Contains(id) || e.cast.Any(role => role.contestantId == id)))
                .ToList();
            Assert.That(waiting.Count, Is.GreaterThanOrEqualTo(2), "Voters whose conversation waits on the moment, in none of it themselves.");
            rules2Voter = waiting[0];
            Assert.That(DialogueRules.PleaWithoutConversation(s, rules2Voter, out string reason), Is.True);
            Assert.That(reason, Is.EqualTo(HouseMomentRefusal));
            yield return InstallRules2Season(s, size);
        }

        /// <summary>(b)'s stop: week 1's nominated Campaign, the asks answered, every voter who can be talked to asked where their vote is.</summary>
        private IEnumerator InstallRules2Word(TextSize size)
        {
            HoldTheHouseForTheFixture();
            var engine = Rules2PlainWalk(Rules2WordSeed, Rules2WordHouse, Rules2WordSteps);
            Assert.That((engine.Snapshot.week, engine.Snapshot.phase), Is.EqualTo((1, EpisodePhase.Campaign)), "The witness's stop (DialogueRules2HudWitnessTests).");
            Assert.That(engine.Snapshot.nominees, Does.Contain(engine.Snapshot.playerId));
            Rules2CloseAsks(engine);
            var talkable = EpisodeEngine.Voters(engine.Snapshot).Where(c => !c.isPlayer).Select(c => c.id)
                .Where(id => DialogueRules.CanStart(engine.Snapshot, id, out _)).ToArray();
            foreach (string voter in talkable) Rules2AskVote(engine, voter);
            var s = engine.Snapshot;
            rules2Voter = talkable.FirstOrDefault(id => Rules2Told(s, id) != null && Rules2Told(s, id).targetId != s.playerId);
            rules2Against = talkable.FirstOrDefault(id => Rules2Told(s, id)?.targetId == s.playerId);
            Assert.That(rules2Voter, Is.Not.Null, "A voter told the player they vote somebody else out.");
            Assert.That(rules2Against, Is.Not.Null, "A voter told the player they vote the player out.");
            Assert.That(DialogueUiRemaining(s), Is.GreaterThanOrEqualTo(2), "An action for each conversation.");
            yield return InstallRules2Season(s, size);
        }

        /// <summary>(d)'s stop: the asking walk's week-2 Campaign, then the voter the reveal caught lying asked where their vote is.</summary>
        private IEnumerator InstallRules2Lie(TextSize size)
        {
            HoldTheHouseForTheFixture();
            var engine = Rules2AskingWalk(Rules2LieSeed, Rules2LieHouse, Rules2LieSteps);
            var s = engine.Snapshot;
            Assert.That((s.week, s.phase, s.evictionResolved), Is.EqualTo((2, EpisodePhase.Campaign, false)), "The witness's stop (DialogueRules2HudWitnessTests).");
            Assert.That(s.nominees, Does.Contain(s.playerId));
            rules2Voter = EpisodeEngine.Voters(s).Where(c => !c.isPlayer).Select(c => c.id).FirstOrDefault(id => DialogueRules.CanStart(s, id, out _)
                && s.ledger.claims.Any(k => k.voterId == id && k.week < s.week && k.status == ClaimStatus.Lied && k.source == ClaimSource.Told));
            Assert.That(rules2Voter, Is.Not.Null, "A voter the reveal caught lying to the player can be talked to.");
            Rules2AskVote(engine, rules2Voter);
            s = engine.Snapshot;
            Assert.That(Rules2Told(s, rules2Voter)?.targetId, Is.Not.Null.And.Not.EqualTo(s.playerId), "They gave their word, and not against the player.");
            yield return InstallRules2Season(s, size);
        }

        /// <summary>
        /// (c): the EditMode suite's retained public-route source (week 5's campaign; the voter used the veto to save the player
        /// in week 2), read as the rules-1 citing test reads it - the immutable carrier, its bytes and hash checked, its public
        /// fields only, lifted through schema 30's own literals - and enrolled on rules 2 by the literal 2, nothing else.
        /// </summary>
        private IEnumerator InstallRules2Rescue(TextSize size)
        {
            HoldTheHouseForTheFixture();
            string path = Path.Combine(Application.dataPath, "Gamesim/Tests/EditMode/Fixtures/DialoguePublicRescueV2/preentry-carrier.json");
            Assert.That(File.Exists(path), Is.True, "The reviewed carrier; no generation fallback.");
            var carrier = JObject.Parse(File.ReadAllText(path, new UTF8Encoding(false, true)));
            byte[] bytes = System.Convert.FromBase64String((string)carrier["base64"]);
            using (var sha = SHA256.Create())
                Assert.That(System.BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""), Is.EqualTo(CiteCarrierSha));
            var fixture = JsonConvert.DeserializeObject<EpisodeState>(CiteCarrierAtTheLiveSchema(new UTF8Encoding(false, true).GetString(bytes)), new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None, DateParseHandling = DateParseHandling.None,
                ObjectCreationHandling = ObjectCreationHandling.Replace, FloatParseHandling = FloatParseHandling.Double,
                MaxDepth = 64, ContractResolver = new CitePublicFieldsOnly(),
            });
            Assert.That((fixture.week, fixture.phase, fixture.dialogueRulesVersion), Is.EqualTo((5, EpisodePhase.Campaign, 0)));
            fixture.dialogueRulesVersion = 2; // The rules-2 enrolment by its literal; no role, AP, RNG or history edit.
            var voters = EpisodeEngine.Voters(fixture).Where(c => !c.isPlayer).ToArray();
            Assert.That(voters, Has.Length.EqualTo(1));
            rules2Voter = voters[0].id;
            Assert.That(fixture.ledger.power.Single(p => p.week == 2).vetoHolderId, Is.EqualTo(rules2Voter));
            Assert.That(fixture.ledger.power.Single(p => p.week == 2).savedId, Is.EqualTo(fixture.playerId));
            Assert.That(DialogueRules.CanStart(fixture, rules2Voter, out string error), Is.True, error);
            yield return InstallRules2Season(fixture, size);
        }

        /// <summary>The season saved and loaded by the director as any season is, then the text size chosen as a player chooses it.</summary>
        private IEnumerator InstallRules2Season(EpisodeState fixture, TextSize size)
        {
            Assert.That(fixture.dialogueRulesVersion, Is.EqualTo(2));
            Assert.That(EpisodeValidation.TryValidate(fixture, out string error), Is.True, error);
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            yield return ReloadEpisode();
            HoldTheHouseForTheFixture();
            yield return SkipReveals();
            AssertEquivalent(fixture, director.Snapshot);
            AssertDialogueUiDisk(director.Snapshot);
            Assert.That(director.GetComponent<EpisodeHud>().FontScale, Is.EqualTo(1f).Within(.001f), "A load comes back at the standard text.");
            yield return ApplyRules2TextSize(size);
            AssertEquivalent(fixture, director.Snapshot);
        }

        /// <summary>The size chosen in the settings; asserted on the HUD that draws the panels. A reload resets it, so it follows every load.</summary>
        private IEnumerator ApplyRules2TextSize(TextSize size)
        {
            yield return ApplyTextSize(size);
            Assert.That(director.TextSize, Is.EqualTo(size));
            Assert.That(director.GetComponent<EpisodeHud>().FontScale, Is.EqualTo(TextSizes.Scale(size)).Within(.001f),
                "The HUD draws at the " + TextSizes.Name(size) + " text.");
        }

        /// <summary>
        /// The real way in: the voter, the conversation category, the vote ask, then "Desperate plea", which starts the talk.
        /// The state is read once the voter's panel is open (<see cref="rules2Opened"/>): opening a panel flushes the house's
        /// ticks, so a press is measured from there, never from before the panel.
        /// </summary>
        private IEnumerator OpenRules2Entry(string voter)
        {
            yield return TalkTo(voter);
            yield return EnterConversationCategory(EpisodeDirector.GameCategoryCaption);
            var source = rules2Opened = director.Snapshot;
            byte[] bytes = File.ReadAllBytes(director.SavePath);
            ButtonWithCaption(EpisodeHud.LobbyAskCaption(source, source.Find(voter).name, LobbyAsk.Vote, source.playerId)).onClick.Invoke();
            yield return null;
            AssertDialogueUiUnchanged(source, bytes);
            Assert.That(ShownText(), Does.Contain("replies and final plea cost no further actions"), "The house is free: the press is the conversation.");
            ButtonWithCaption(EpisodeHud.LobbyApproachCaption(LobbyApproach.Emotional, true)).onClick.Invoke();
            yield return Frames(2);
        }

        // ---------------------------------------------------------------- layout

        /// <summary>
        /// The open reply on screen: <paramref name="line"/> drawn once as its paragraph, <paramref name="pressable"/> a live
        /// tile, the <paramref name="options"/> tiles two to a row in cells 96 times the scale high, and every label the reply
        /// draws laid out whole at the text <paramref name="scale"/>, from the beat's eyebrow to its last tile: the eyebrow,
        /// the title, the narrative, the line, "What do you do?", and each tile's caption, risk word, description and foot.
        /// </summary>
        private void AssertRules2Reply(string line, int options, string pressable, float scale, string where)
        {
            Assert.That(director.GetComponent<EpisodeHud>().FontScale, Is.EqualTo(scale).Within(.001f), where + ": drawn at the text size.");
            Assert.That(ShownText(), Does.Contain(line), where + ": the line is drawn, in words.");
            Assert.That(ButtonWithCaption(EpisodeHud.EventChoiceCaption(pressable)), Is.Not.Null, where + ": '" + pressable + "' is pressable.");
            var failures = new List<string>();
            var said = Rules2ShownLabel(line);
            Rules2LabelWhole(said, where + ", the line", failures);
            Canvas.ForceUpdateCanvases();
            var grid = ActiveRect(EpisodeHud.StoryChoicesName);
            Assert.That(grid, Is.Not.Null, where + ": the reply's tiles are on screen.");
            var layout = grid.GetComponent<GridLayoutGroup>();
            Assert.That(layout.constraintCount, Is.EqualTo(2), where + ": two tiles to a row.");
            Assert.That(layout.cellSize.y, Is.EqualTo(96f * scale).Within(.01f), where + ": cells 96 high at the text scale.");
            var tiles = grid.GetComponentsInChildren<Button>().Where(tile => tile.IsActive()).ToList();
            Assert.That(tiles.Count, Is.EqualTo(options), where + ": a tile for each option.");

            // The beat's head, found in the reply's own column: the title nearest above the tiles, the eyebrow just over it.
            var s = director.Snapshot;
            var open = DialogueRules.Active(s);
            Assert.That(open, Is.Not.Null, where + ": a conversation is open.");
            var item = s.houseEvents.Single(e => e.id == s.storylines.Single(c => c.id == open.id).eventId);
            string title = StoryText.Title(item), eyebrow = (StoryText.Eyebrow(item) ?? "STORY").ToUpperInvariant();
            var column = grid.parent;
            Assert.That(said.transform.parent, Is.SameAs(column), where + ": the line is the reply's paragraph.");
            int last = grid.GetSiblingIndex();
            var head = Enumerable.Range(0, last).Select(index => column.GetChild(index)).Where(child => child.gameObject.activeInHierarchy)
                .Select(child => child.GetComponent<TMP_Text>()).LastOrDefault(label => label != null && label.text == title);
            Assert.That(head != null, Is.True, where + ": the beat's title '" + title + "' heads the reply.");
            int first = head.transform.GetSiblingIndex() - 1;
            var mark = first >= 0 ? column.GetChild(first).GetComponent<TMP_Text>() : null;
            Assert.That(mark != null && mark.text == eyebrow, Is.True, where + ": the eyebrow '" + eyebrow + "' stands over the title.");
            var labels = Rules2LabelsBetween(column, first, last);
            Assert.That(labels.Contains(said), Is.True, where + ": the line is drawn between the beat's head and its tiles.");
            foreach (var tile in tiles)
                Assert.That(tile.GetComponentsInChildren<TMP_Text>().Where(text => text.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(text.text))
                    .All(text => labels.Contains(text)), Is.True, where + ": the tile '" + tile.name + "' is swept.");
            foreach (var label in labels) Rules2LabelWhole(label, where, failures);
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        /// <summary>Every live label with words in the children <paramref name="first"/> to <paramref name="last"/> of <paramref name="column"/>, nested ones too.</summary>
        private static List<TMP_Text> Rules2LabelsBetween(Transform column, int first, int last) =>
            Enumerable.Range(first, last - first + 1).Select(index => column.GetChild(index)).Where(child => child.gameObject.activeInHierarchy)
                .SelectMany(child => child.GetComponentsInChildren<TMP_Text>())
                .Where(label => label.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(label.text)).ToList();

        /// <summary>The one label on screen showing exactly <paramref name="text"/>.</summary>
        private TMP_Text Rules2ShownLabel(string text)
        {
            var labels = director.GetComponentsInChildren<TMP_Text>(true).Where(label => label.gameObject.activeInHierarchy && label.text == text).ToList();
            Assert.That(labels, Has.Count.EqualTo(1), "'" + text + "' is drawn once.");
            return labels[0];
        }

        /// <summary>
        /// A label of a rules-2 panel: a box at least 1.3 times the largest size its words are drawn at (Inter's line is
        /// 1.21 of it, and a shorter box draws nothing while the text still reads right), nothing overflowing or cut, every
        /// glyph drawn. Logged for the lead's reading; a miss joins <paramref name="failures"/>.
        /// </summary>
        private static void Rules2LabelWhole(TMP_Text label, string where, List<string> failures)
        {
            if (label == null) { failures.Add(where + ": no label"); return; }
            Canvas.ForceUpdateCanvases();
            label.ForceMeshUpdate(true);
            float size = label.enableAutoSizing ? label.fontSizeMax : label.fontSize;
            float box = label.rectTransform.rect.height;
            int glyphs = label.text.Count(c => !char.IsWhiteSpace(c));
            int drawn = Enumerable.Range(0, label.textInfo.characterCount).Count(i => label.textInfo.characterInfo[i].isVisible);
            string fit = where + ": '" + label.text + "' in a box " + label.rectTransform.rect.size.ToString("0.0") + " for words of "
                + size.ToString("0.#") + " (drawn at " + label.fontSize.ToString("0.#") + "), lines " + label.textInfo.lineCount
                + ", overflowing " + label.isTextOverflowing + ", truncated " + label.isTextTruncated + ", drawn " + drawn + " of " + glyphs + " glyphs";
            TestContext.Out.WriteLine(fit);
            if (box < size * 1.3f - .5f || label.isTextOverflowing || label.isTextTruncated || drawn != glyphs) failures.Add(fit);
        }
    }
}
