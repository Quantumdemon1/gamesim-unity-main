using System.Collections;
using System.IO;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        private string intentListenerId;

        private IEnumerator InstallIntentHouse(bool fresh = true, int size = 8, bool campaign = false)
        {
            yield return InstallTalkingHouse(size, campaign, s =>
            {
                EpisodeEngine.EnableWeek(s); EpisodeEngine.EnableStory(s);
                s.economyRulesVersion = fresh ? 1 : 0;
            });
            intentListenerId = Listener(director.Snapshot).id;
            yield return TalkTo(intentListenerId);
            yield return EnterConversationCategory(EpisodeDirector.ConnectCategoryCaption);
        }

        private void AssertIntentDurable(EpisodeState expected)
        {
            Assert.That(JsonUtility.ToJson(director.Snapshot), Is.EqualTo(JsonUtility.ToJson(expected)));
            Assert.That(new EpisodeSaveStore(director.SavePath).TryLoad(out var disk, out string error), Is.True, error);
            Assert.That(JsonUtility.ToJson(disk), Is.EqualTo(JsonUtility.ToJson(expected)));
        }

        private void AssertIntentCommand(EpisodeState before, EpisodeCommandKind kind, string target, string text = null,
            string secondTargetId = null)
        {
            var after = director.Snapshot;
            Assert.That(after.acceptedCommandIds.Count, Is.EqualTo(before.acceptedCommandIds.Count + 1));
            Assert.That(after.acceptedCommandIds.Take(before.acceptedCommandIds.Count), Is.EqualTo(before.acceptedCommandIds));
            var command = new EpisodeCommand { id = after.acceptedCommandIds.Last(), actorId = before.playerId,
                expectedRevision = before.revision, expectedPhase = before.phase, kind = kind, targetId = target,
                secondTargetId = secondTargetId, text = text };
            var replay = new EpisodeEngine(before).Apply(command);
            Assert.That(replay.accepted, Is.True, replay.reason);
            AssertIntentDurable(replay.state);
        }

        [UnityTest]
        public IEnumerator ConversationIntent_FreshRootHasSixNonoverlappingCategoriesAndConnectKeepsThePlainTalkChoice()
        {
            yield return InstallIntentHouse(size: 16);
            foreach (var textSize in TextSizes.All)
            {
                yield return ApplyTextSize(textSize);
                yield return TalkTo(intentListenerId);
                var before = director.Snapshot;
                var dial = ActiveRect(EpisodeHud.DialName);
                Assert.That(dial, Is.Not.Null);
                var petals = dial.GetComponentsInChildren<Button>().Where(b => b.IsActive()).ToArray();
                Assert.That(petals.Length, Is.EqualTo(6));
                foreach (string caption in PetalCaptions)
                    Assert.That(ButtonWithCaption(caption).transform.IsChildOf(dial), Is.True, caption);
                Assert.That(ButtonWithCaptionOrNull("Spend time together"), Is.Null, "The root holds categories, not action choices.");
                Canvas.ForceUpdateCanvases(); yield return null;
                for (int a = 0; a < petals.Length; a++)
                for (int b = a + 1; b < petals.Length; b++)
                    Assert.That(ScreenRect((RectTransform)petals[a].transform).Overlaps(ScreenRect((RectTransform)petals[b].transform)), Is.False);
                yield return AssertKeyboardRing("Fresh six-petal conversation " + TextSizes.Name(textSize), ModalRoot);
                yield return EnterConversationCategory(EpisodeDirector.ConnectCategoryCaption);
                var plain = ButtonWithCaption("Spend time together");
                Assert.That(plain.IsInteractable(), Is.True);
                Assert.That(TagOn(ButtonWithCaption(EpisodeHud.PersonalChatCaption)), Does.Contain(EpisodeDirector.LearnTag));
                Assert.That(TagOn(ButtonWithCaption(EpisodeHud.RelationshipBuildingCaption)), Does.Contain(EpisodeDirector.WarmthTag));
                Assert.That(ActiveRect(EpisodeHud.DialName), Is.Null);
                Assert.That(JsonUtility.ToJson(director.Snapshot), Is.EqualTo(JsonUtility.ToJson(before)));
            }
        }

        [UnityTest]
        public IEnumerator ConversationIntent_LegacyUsesTheSameCategoriesAndKeepsItsOriginalPersonalWarmthTag()
        {
            yield return InstallIntentHouse(fresh: false);
            yield return KeyboardSubmit(EpisodeDirector.ConversationTopicsBackCaption);
            var dial = ActiveRect(EpisodeHud.DialName);
            Assert.That(dial.GetComponentsInChildren<Button>().Count(b => b.IsActive()), Is.EqualTo(6));
            foreach (string caption in PetalCaptions)
                Assert.That(ButtonWithCaption(caption).transform.IsChildOf(dial), Is.True, caption);
            yield return EnterConversationCategory(EpisodeDirector.ConnectCategoryCaption);
            Assert.That(TagOn(ButtonWithCaption(EpisodeHud.PersonalChatCaption)), Does.Contain(EpisodeDirector.WarmthTag));
        }

        [UnityTest]
        public IEnumerator ConversationIntent_PersonalButtonLearnsTwoFactsOnceAndPersistsTheExactEngineOutcome()
        {
            yield return InstallIntentHouse();
            var before = director.Snapshot;
            var click = ButtonWithCaption(EpisodeHud.PersonalChatCaption).onClick;
            click.Invoke();
            var after = director.Snapshot;
            Assert.That(Lore.Learned(after, intentListenerId).Count, Is.EqualTo(2));
            Assert.That(after.windowActions[Windows.AfterEviction], Is.EqualTo(1));
            AssertIntentCommand(before, EpisodeCommandKind.PersonalChat, intentListenerId);
            click.Invoke(); AssertIntentDurable(after);
            director.LoadNow(); HoldTheHouseForTheFixture();
            AssertIntentDurable(after);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ConversationIntent_PlainTalkMovedToRowKeepsItsCommandAndDurableCost()
        {
            yield return InstallIntentHouse();
            var before = director.Snapshot;
            yield return KeyboardSubmit("Spend time together");
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(before.revision + 1));
            Assert.That(after.windowActions[Windows.AfterEviction], Is.EqualTo(1));
            AssertIntentCommand(before, EpisodeCommandKind.Talk, intentListenerId);
        }

        [UnityTest]
        public IEnumerator ConversationIntent_ClosingTheConversationNeverLearnsOrSpends()
        {
            yield return InstallIntentHouse();
            var before = director.Snapshot;
            byte[] original = File.ReadAllBytes(director.SavePath);
            director.ClosePanels(); yield return null;
            Assert.That(JsonUtility.ToJson(director.Snapshot), Is.EqualTo(JsonUtility.ToJson(before)));
            Assert.That(File.ReadAllBytes(director.SavePath), Is.EqualTo(original));
        }

        [UnityTest]
        public IEnumerator ConversationIntent_FailedPersonalSavePublishesNoLoreAndKeepsTheSlot()
        {
            yield return InstallIntentHouse();
            var before = director.Snapshot;
            byte[] original = File.ReadAllBytes(director.SavePath);
            using (new FileStream(director.SavePath, FileMode.Open, FileAccess.Read, FileShare.None))
                ButtonWithCaption(EpisodeHud.PersonalChatCaption).onClick.Invoke();
            Assert.That(JsonUtility.ToJson(director.Snapshot), Is.EqualTo(JsonUtility.ToJson(before)));
            Assert.That(File.ReadAllBytes(director.SavePath), Is.EqualTo(original));
            Assert.That(Lore.Learned(director.Snapshot, intentListenerId), Is.Empty);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ConversationIntent_CampaignUsesTheSameLorePayoffAndItsOwnWindowSeat()
        {
            yield return InstallIntentHouse(campaign: true);
            var before = director.Snapshot;
            ButtonWithCaption(EpisodeHud.PersonalChatCaption).onClick.Invoke();
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(before.revision + 1));
            Assert.That(after.windowActions[Windows.AfterVeto], Is.EqualTo(before.windowActions[Windows.AfterVeto] + 1));
            Assert.That(after.windowActions[Windows.AfterEviction], Is.EqualTo(before.windowActions[Windows.AfterEviction]));
            Assert.That(Lore.Learned(after, intentListenerId).Count, Is.EqualTo(2));
            AssertIntentDurable(after);
            yield return null;
        }

        /// <summary>The first voter in the open campaign board's order who passes <paramref name="wanted"/>, with a card on the screen and a body to walk to.</summary>
        private ContestantState CampaignBoardVoter(EpisodeState state, System.Func<ContestantState, bool> wanted)
        {
            var bodies = SceneComponents<Gamesim.House.HouseNpc>().Where(npc => npc.gameObject.activeInHierarchy).Select(npc => npc.Id).ToList();
            var voter = EpisodeEngine.Voters(state).FirstOrDefault(v => !v.isPlayer && bodies.Contains(v.id) && wanted(v)
                && ButtonWithCaptionOrNull(EpisodeHud.CastTalkCaption(v.name.Split(' ')[0])) != null);
            Assert.That(voter, Is.Not.Null, "A voter on the board with a body to walk to.");
            return voter;
        }

        /// <summary>
        /// "Talk to" on a voter's card, pressed as the player presses it: the board closes and the
        /// walk begins knowing what the player came for; then the conversation opens beside them, as
        /// arriving opens it.
        /// </summary>
        private IEnumerator TalkFromTheCampaignBoard(ContestantState voter, string intent)
        {
            ButtonWithCaption(EpisodeHud.CastTalkCaption(voter.name.Split(' ')[0])).onClick.Invoke();
            yield return null;
            Assert.That(director.IsPhasePanelOpen, Is.False, "Talking closes the campaign,");
            Assert.That(director.WalkingToId == voter.id || director.TalkingToId == voter.id, Is.True, "walks over to " + voter.name + ",");
            Assert.That(director.ConversationIntent, Is.EqualTo(intent), "and knows what the player came for.");
            if (!director.IsConversationOpen)
                yield return OpenNearbyNpc(SceneComponents<Gamesim.House.HouseNpc>().First(npc => npc.Id == voter.id && npc.gameObject.activeInHierarchy));
            Assert.That(director.TalkingToId, Is.EqualTo(voter.id), "The conversation is open with them.");
            if (director.IsConversationExchangePlaying)
            {
                ButtonWithCaption(EpisodeHud.ConversationSkipCaption).onClick.Invoke();
                yield return null;
            }
        }

        /// <summary>Back to the topics from the page the board's talk opened on: the six petals, the intent let go and nothing spent.</summary>
        private IEnumerator BackToTheTopicsFromTheBoardsTalk(string where)
        {
            var before = director.Snapshot;
            ButtonWithCaption(EpisodeDirector.ConversationTopicsBackCaption).onClick.Invoke();
            yield return null;
            var dial = ActiveRect(EpisodeHud.DialName);
            Assert.That(dial, Is.Not.Null, where + ": back to the topics is the wheel,");
            foreach (string caption in PetalCaptions)
                Assert.That(ButtonWithCaption(caption).transform.IsChildOf(dial), Is.True, caption);
            Assert.That(director.CurrentConversationCategory, Is.Null, where + ": no category is open,");
            Assert.That(director.ConversationIntent, Is.Null, where + ": and the intent is let go.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision), where + ": the way there and back commits nothing.");
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// Off the block, "Talk to" on a voter's campaign card opens the conversation on Learn and
        /// share with the vote question on it (UI-UX plan D9), where it used to open on the six topics.
        /// </summary>
        [UnityTest]
        public IEnumerator ConversationIntent_ACampaignVotersTalkOpensOnTheVoteQuestion()
        {
            yield return InstallCampaign(46);
            yield return OpenStation();
            var state = director.Snapshot;
            Assert.That(state.nominees, Does.Not.Contain(state.playerId), "The player is off the block.");
            var voter = CampaignBoardVoter(state, v => !EpisodeEngine.AskedThisWeek(state, v.id));
            yield return TalkFromTheCampaignBoard(voter, EpisodeDirector.IntentAsk);
            const string where = "A voter's talk from the campaign board";
            Assert.That(director.CurrentConversationCategory, Is.EqualTo(EpisodeDirector.LearnCategoryCaption), where + " opens on Learn and share,");
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.AskVoteCaption), Is.Not.Null, where + ": the vote question is on it,");
            Assert.That(ActiveRect(EpisodeHud.DialName), Is.Null, where + ": not the topics.");
            yield return BackToTheTopicsFromTheBoardsTalk(where);
        }

        /// <summary>
        /// On the block, "Talk to" on a voter's campaign card opens the conversation on This week's
        /// game with the plea to keep you on it (UI-UX plan D9), where it used to open on the six topics.
        /// </summary>
        [UnityTest]
        public IEnumerator ConversationIntent_ANomineesCampaignTalkOpensOnThePlea()
        {
            yield return InstallCampaign(46, state =>
            {
                EpisodeEngine.EnableLevers(state);
                state.nominees[1] = state.playerId;   // the veto lineup already seats everyone in a six-house
            });
            yield return OpenStation();
            var state = director.Snapshot;
            Assert.That(state.nominees, Does.Contain(state.playerId), "The player is on the block.");
            var voter = CampaignBoardVoter(state, v => StrategyRules.CanBeAskedForTheirVote(state, v.id));
            yield return TalkFromTheCampaignBoard(voter, EpisodeDirector.IntentPlea);
            const string where = "A nominee's talk from the campaign board";
            Assert.That(director.CurrentConversationCategory, Is.EqualTo(EpisodeDirector.GameCategoryCaption), where + " opens on This week's game,");
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.LobbyAskCaption(state, voter.name, LobbyAsk.Vote, state.playerId)), Is.Not.Null,
                where + ": the plea is on it,");
            Assert.That(ActiveRect(EpisodeHud.DialName), Is.Null, where + ": not the topics.");
            yield return BackToTheTopicsFromTheBoardsTalk(where);
        }
    }
}
