using System.Collections;
using System.IO;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Persistence;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Owner, 2026-10-25: never lose the plea (DIALOGUE-PLAN §5.5). In a season on dialogue rules 1 a desperate plea made
    /// while the conversation cannot start - here, while a house moment waits on an answer - is the one-shot plea a season
    /// without dialogue makes, through the player's real controls: the conversation category, the vote ask, the panel
    /// saying there is no conversation now and why, the "Desperate plea" button with its short tag, the press, and its
    /// result, which is the chip-off season's same press field for field. Then, the house answered, the next voter's
    /// desperate plea opens the conversation again. Rules 1 used to lock the button instead, its tag CanStart's reason.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private const string HouseMomentRefusal = "Answer the existing house moment first.";
        private string fallbackOtherVoter;

        [UnityTest]
        public IEnumerator DialogueCampaign_ARefusedConversationLeavesTheDesperatePleaAsTheOneShotPlea()
        {
            yield return InstallDialogueFallbackCampaign();
            string voter = dialogueUiVoter, name = director.Snapshot.Find(voter).name;
            yield return TalkTo(voter);
            yield return EnterConversationCategory(EpisodeDirector.GameCategoryCaption);
            var source = director.Snapshot;
            byte[] bytes = File.ReadAllBytes(director.SavePath);
            ButtonWithCaption(EpisodeHud.LobbyAskCaption(source, name, LobbyAsk.Vote, source.playerId)).onClick.Invoke();
            yield return null;
            AssertDialogueUiUnchanged(source, bytes);

            // The panel says there is no conversation now, in CanStart's words, and that the plea is the one-shot plea.
            Assert.That(ShownText(), Does.Contain("No conversation with " + name + " now. " + HouseMomentRefusal
                + " A desperate plea is the ordinary one-shot plea, for one action."), "The panel says why there is no talk.");
            Assert.That(ShownText(), Does.Not.Contain("replies and final plea cost no further actions"), "and promises none.");
            string caption = EpisodeHud.LobbyApproachCaption(LobbyApproach.Emotional, true);
            var plea = ButtonWithCaption(caption); // Visible, interactable and reachable: the plea is not locked.
            string word = KnownOdds.Plea(source, voter, LobbyAsk.Vote, source.playerId, LobbyApproach.Emotional).word;
            // D1b's review: " · no conversation now" ran to 192 in its chip's 184 at text size 1.0 and lost "now"; the
            // shorter words sit on one line for every odds word, and the tag must draw whole.
            Assert.That(TagWords(plea), Is.EqualTo(word + " · no talk now"), "The tag adds only that there is no talk now.");
            Assert.That(EpisodeDirector.NoConversationTag, Is.EqualTo("no talk now"));
            Assert.That(TagWords(FindButton(EpisodeHud.LobbyApproachCaption(LobbyApproach.Strategic, true))),
                Is.EqualTo(KnownOdds.Plea(source, voter, LobbyAsk.Vote, source.playerId, LobbyApproach.Strategic).word), "The other approaches are as ever.");
            AssertEveryLabelDraws((RectTransform)plea.transform, "The desperate plea's row");
            AssertTagDrawsWhole(plea);

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
            expected.dialogueRulesVersion = 1;
            AssertEquivalent(expected, after);
            yield return EnterConversationCategory(EpisodeDirector.GameCategoryCaption);
            Assert.That(ShownText(), Does.Contain("You have had your plea with " + name + ". They were " + lobby.response + "."), "The panel says the plea is made.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(after.revision), "Reading it commits nothing.");
            yield return ReloadDialogueUiBytes();

            // The conversation is not spent: the house's moments answered with their own neutral exits, as the pilot's
            // fixtures answer them, the next voter's desperate plea opens it.
            var engine = new EpisodeEngine(director.Snapshot.Clone());
            CloseDialogueUiOldAsks(engine);
            var answered = engine.Snapshot;
            Assert.That(DialogueRules.CanStart(answered, fallbackOtherVoter, out error), Is.True, error);
            new EpisodeSaveStore(director.SavePath).Save(answered);
            yield return ReloadEpisode();
            HoldTheHouseForTheFixture();
            yield return SkipReveals();
            AssertEquivalent(answered, director.Snapshot);
            string other = director.Snapshot.Find(fallbackOtherVoter).name;
            yield return TalkTo(fallbackOtherVoter);
            yield return EnterConversationCategory(EpisodeDirector.GameCategoryCaption);
            var free = director.Snapshot;
            ButtonWithCaption(EpisodeHud.LobbyAskCaption(free, other, LobbyAsk.Vote, free.playerId)).onClick.Invoke();
            yield return null;
            Assert.That(ShownText(), Does.Contain("replies and final plea cost no further actions"), "With the house free the plea is a conversation again,");
            Assert.That(ShownText(), Does.Not.Contain("No conversation with"));
            Assert.That(TagWords(FindButton(caption)), Is.EqualTo(KnownOdds.Plea(free, fallbackOtherVoter, LobbyAsk.Vote, free.playerId, LobbyApproach.Emotional).word));
            var beforeEntry = director.Snapshot;
            ButtonWithCaption(caption).onClick.Invoke();
            yield return Frames(2);
            var entered = director.Snapshot;
            Assert.That(entered.revision, Is.EqualTo(beforeEntry.revision + 1));
            Assert.That(DialogueRules.Active(entered)?.npcId, Is.EqualTo(fallbackOtherVoter), "and the press opens it,");
            Assert.That(DialogueUiRemaining(entered), Is.EqualTo(DialogueUiRemaining(beforeEntry) - 1), "for the plea's one action.");
            Assert.That(entered.lobbies.Count, Is.EqualTo(beforeEntry.lobbies.Count), "No plea yet: the conversation holds it.");
        }

        /// <summary>
        /// That the tag's words sit whole in its chip, logged for the lead's reading too: the chip is at most 196 wide at text size
        /// 1.0. D1b's first words, "about even · no conversation now", measured 192 in Inter at twelve, so "now" wrapped onto a
        /// second line the 22-high chip cuts off while the label's text still read whole. The shorter words measure 139 at the
        /// most against the chip's 166 inside, so the tag sits on one line and draws every glyph; this fails the run if not.
        /// </summary>
        private static void AssertTagDrawsWhole(Button row)
        {
            var tag = row.transform.Find("Tag");
            var label = tag != null ? tag.GetComponentInChildren<TMP_Text>(true) : null;
            Assert.That(label, Is.Not.Null, "The desperate plea's row carries its tag.");
            Canvas.ForceUpdateCanvases();
            label.ForceMeshUpdate(true);
            int glyphs = label.text.Count(c => !char.IsWhiteSpace(c));
            int drawn = Enumerable.Range(0, label.textInfo.characterCount).Count(i => label.textInfo.characterInfo[i].isVisible);
            string fit = "Fallback tag '" + label.text + "': box " + label.rectTransform.rect.size.ToString("0.0") + ", font "
                + label.fontSize.ToString("0.#") + ", lines " + label.textInfo.lineCount + ", truncated " + label.isTextTruncated
                + ", drawn " + drawn + " of " + glyphs + " glyphs.";
            TestContext.Out.WriteLine(fit);
            Assert.That(label.isTextTruncated, Is.False, fit);
            Assert.That(label.textInfo.lineCount, Is.EqualTo(1), fit);
            Assert.That(drawn, Is.EqualTo(glyphs), fit);
        }

        /// <summary>
        /// A nominated Campaign on dialogue rules 1 with a house moment waiting on an answer, two actions left and two voters
        /// whose conversation waits on it, neither of them in an open beat: the first season of 24 that the public walk brings
        /// there, enrolled by the literal. Nothing past the enrolment is assigned by hand.
        /// </summary>
        private IEnumerator InstallDialogueFallbackCampaign()
        {
            HoldTheHouseForTheFixture();
            EpisodeState fixture = null;
            for (uint seed = 1; seed <= 24 && fixture == null; seed++)
            {
                var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
                ShippedRules.ApplyFresh(fresh);
                Assert.That(fresh.dialogueRulesVersion, Is.Zero);
                fresh.dialogueRulesVersion = 1; // The declared pilot opt-in, as every pilot fixture's.
                var engine = new EpisodeEngine(fresh);
                for (int step = 0; step < 400; step++)
                {
                    var s = engine.Snapshot;
                    if (s.phase == EpisodePhase.Finished || s.Find(s.playerId)?.status != ContestantStatus.Active) break;
                    if (s.phase == EpisodePhase.Campaign && !s.evictionResolved && s.nominees.Contains(s.playerId) && DialogueUiRemaining(s) >= 2)
                    {
                        var open = s.houseEvents.Where(e => !e.resolved && HouseEventKind.Asks(e.kind)).ToList();
                        var waiting = EpisodeEngine.Voters(s).Where(c => !c.isPlayer).Select(c => c.id)
                            .Where(id => StrategyRules.CanLobby(s, id, LobbyAsk.Vote, s.playerId, out _)
                                && !DialogueRules.CanStart(s, id, out string why) && why == HouseMomentRefusal
                                && !open.Any(e => e.involvedIds.Contains(id) || e.cast.Any(role => role.contestantId == id)))
                            .ToList();
                        if (open.Count > 0 && waiting.Count >= 2)
                        {
                            fixture = s;
                            dialogueUiVoter = waiting[0];
                            fallbackOtherVoter = waiting[1];
                            TestContext.Out.WriteLine("Plea fallback UI witness: seed " + seed + ", week " + s.week + " campaign, revision " + s.revision + ".");
                            break;
                        }
                    }
                    var applied = engine.Apply(NextCommand(s));
                    if (!applied.accepted) break;
                }
            }
            Assert.That(fixture, Is.Not.Null, "No nominated campaign of 24 seeds met an open house moment with two actions and two voters waiting on it; no state was fabricated.");
            Assert.That(EpisodeValidation.TryValidate(fixture, out string error), Is.True, error);
            Assert.That(DialogueRules.PleaWithoutConversation(fixture, dialogueUiVoter, out string reason), Is.True);
            Assert.That(reason, Is.EqualTo(HouseMomentRefusal));
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            yield return ReloadEpisode();
            HoldTheHouseForTheFixture();
            yield return SkipReveals();
            AssertEquivalent(fixture, director.Snapshot);
            AssertDialogueUiDisk(director.Snapshot);
        }
    }
}
