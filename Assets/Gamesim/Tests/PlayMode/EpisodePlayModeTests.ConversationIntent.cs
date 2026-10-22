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
    }
}
