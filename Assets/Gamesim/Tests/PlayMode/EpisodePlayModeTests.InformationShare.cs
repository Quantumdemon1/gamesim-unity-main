using System.Collections;
using System.IO;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Persistence;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        private IEnumerator InstallSharingHouse(int count = 3, int size = 8, bool rules = true, bool campaign = false, bool longText = false, bool beforeVeto = false)
        {
            yield return InstallTalkingHouse(size, campaign, s =>
            {
                EpisodeEngine.EnableWeek(s); s.economyRulesVersion = rules ? 1 : 0;
                if (beforeVeto) { s.phase = EpisodePhase.VetoMeeting; s.vetoResolved = false; }
                s.memories.Clear();
                for (int i = 0; i < count; i++)
                {
                    string text = "Known memory " + i + " <b>literal</b>";
                    if (longText) text += new string('x', InformationShareChoice.MemoryLimit - text.Length - " 😀".Length) + " 😀";
                    s.memories.Add(new MemoryState { ownerId = s.playerId, subjectId = s.playerId,
                        week = s.week, isPrivate = true, text = text });
                }
                string other = s.Active.Last(c => !c.isPlayer).id;
                s.memories.Add(new MemoryState { ownerId = other, subjectId = s.playerId, week = s.week,
                    text = "NPC-PRIVATE-NOT-FOR-THE-PICKER", isPrivate = true });
                s.memories.Add(new MemoryState { ownerId = s.playerId, subjectId = other, week = s.week,
                    text = s.Find(other).name + " broke a Vote promise.", isPrivate = true });
            });
            yield return TalkTo(Listener(director.Snapshot).id);
        }

        private IEnumerator OpenInformationShare()
        {
            if (director.GetComponentInChildren<EpisodeHud>().PointerHeld) yield return WaitOutThePointerHold();
            ButtonWithCaption(EpisodeDirector.ShareInformationCaption).onClick.Invoke();
            Assert.That(director.GetComponentInChildren<EpisodeHud>().PointerHeld, Is.True);
            yield return WaitOutThePointerHold();
            Assert.That(director.IsChoosingSharedInformation, Is.True);
            Assert.That(director.GetComponentInChildren<EpisodeHud>().PointerHeld, Is.False);
            Assert.That(ButtonWithCaption(EpisodeDirector.ShareInformationCancelCaption).IsInteractable(), Is.True);
        }

        private string SharingReviewCaption(int number) => EpisodeDirector.ShareMemoryReviewCaption(number, director.Snapshot.Find(director.Snapshot.playerId).name);

        private void AssertSharingUnchanged(EpisodeState before)
        {
            Assert.That(JsonUtility.ToJson(director.Snapshot), Is.EqualTo(JsonUtility.ToJson(before)));
        }

        private void AssertSharedMemory(EpisodeState before, string recipient, InformationShareChoice.Option option)
        {
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(before.revision + 1));
            Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(EpisodeEngine.SocialActionsSpent(before) + 1));
            var learned = after.memories.Last();
            Assert.That((learned.ownerId, learned.subjectId, learned.text, learned.isPrivate),
                Is.EqualTo((recipient, option.SubjectId, option.ReceivedText, true)));
            Assert.That(new EpisodeSaveStore(director.SavePath).TryLoad(out var disk, out var error), Is.True, error);
            Assert.That(JsonUtility.ToJson(disk), Is.EqualTo(JsonUtility.ToJson(after)));
            Assert.That(director.IsChoosingSharedInformation, Is.False);
        }

        [UnityTest]
        public IEnumerator InformationShare_AnOlderChosenMemoryIsReviewedAndDurablySharedInsteadOfTheLatest()
        {
            yield return InstallSharingHouse();
            var before = director.Snapshot;
            string recipient = Listener(before).id;
            var options = InformationShareChoice.Open(before, recipient).Options;
            Assert.That(options.Count, Is.EqualTo(3));
            Assert.That(TagOn(ButtonWithCaption(EpisodeDirector.ShareInformationCaption)), Is.EqualTo(EpisodeDirector.WarmthTag));
            yield return OpenInformationShare();
            AssertSharingUnchanged(before);
            Assert.That(ShownText(), Does.Not.Contain("NPC-PRIVATE-NOT-FOR-THE-PICKER").And.Not.Contain("broke a Vote promise"));
            for (int i = 1; i <= 3; i++) Assert.That(ButtonWithCaption(SharingReviewCaption(i)), Is.Not.Null);
            Assert.That(ButtonWithCaptionOrNull(EpisodeDirector.ShareInformationConfirmCaption), Is.Null);
            ButtonWithCaption(SharingReviewCaption(3)).onClick.Invoke();
            AssertSharingUnchanged(before);
            yield return WaitOutThePointerHold();
            Assert.That(director.GetComponentsInChildren<TMP_Text>().Single(t => t.name == EpisodeDirector.ShareMemoryTextName).text,
                Is.EqualTo(options[2].Text));
            var shown = director.GetComponentsInChildren<TMP_Text>().Single(t => t.name == EpisodeDirector.ShareReceiptTextName);
            Assert.That(shown.text, Is.EqualTo(options[2].ReceivedText));
            Assert.That(shown.richText, Is.False);
            var confirm = ButtonWithCaption(EpisodeDirector.ShareInformationConfirmCaption).onClick;
            confirm.Invoke();
            AssertSharedMemory(before, recipient, options[2]);
            var committed = director.Snapshot;
            confirm.Invoke(); AssertSharingUnchanged(committed);
        }

        [UnityTest]
        public IEnumerator InformationShare_BackCancelReopenAndOldOpenersCannotShareOrReplaceTheCurrentChoice()
        {
            yield return InstallSharingHouse();
            var before = director.Snapshot;
            var oldOpen = ButtonWithCaption(EpisodeDirector.ShareInformationCaption).onClick;
            yield return OpenInformationShare();
            ButtonWithCaption(SharingReviewCaption(1)).onClick.Invoke();
            yield return WaitOutThePointerHold();
            var oldConfirm = ButtonWithCaption(EpisodeDirector.ShareInformationConfirmCaption).onClick;
            ButtonWithCaption(EpisodeDirector.ShareInformationBackCaption).onClick.Invoke();
            oldConfirm.Invoke(); AssertSharingUnchanged(before);
            yield return WaitOutThePointerHold();
            ButtonWithCaption(SharingReviewCaption(2)).onClick.Invoke();
            oldConfirm.Invoke(); AssertSharingUnchanged(before);
            yield return WaitOutThePointerHold();
            var oldCancel = ButtonWithCaption(EpisodeDirector.ShareInformationCancelCaption).onClick;
            oldCancel.Invoke();
            Assert.That(director.IsChoosingSharedInformation, Is.False);
            oldOpen.Invoke();
            Assert.That(director.IsChoosingSharedInformation, Is.False);
            yield return OpenInformationShare();
            oldConfirm.Invoke(); oldCancel.Invoke();
            Assert.That(director.IsChoosingSharedInformation, Is.True);
            AssertSharingUnchanged(before);
        }

        [UnityTest]
        public IEnumerator InformationShare_AllThirtyMemoriesAreKeyboardReachableAcrossPagesAtBothTextSizes()
        {
            yield return InstallSharingHouse(30, 16);
            foreach (bool larger in new[] { false, true })
            {
                yield return ApplyTextSize(larger);
                yield return TalkTo(Listener(director.Snapshot).id);
                var before = director.Snapshot;
                yield return KeyboardSubmit(EpisodeDirector.ShareInformationCaption);
                Assert.That(EventSystem.current.currentSelectedGameObject?.name, Is.EqualTo(EpisodeDirector.ShareInformationCancelCaption));
                for (int page = 0; page < 5; page++)
                {
                    yield return WaitOutThePointerHold();
                    for (int i = 1; i <= EpisodeDirector.ShareInformationPageSize; i++)
                        Assert.That(ButtonWithCaption(SharingReviewCaption(page * EpisodeDirector.ShareInformationPageSize + i)).IsInteractable(), Is.True);
                    yield return AssertKeyboardRing("Information memories page " + page + " / large " + larger, ModalRoot);
                    AssertSharingUnchanged(before);
                    if (page < 4) yield return KeyboardSubmit(EpisodeDirector.ShareInformationNextCaption);
                    else Assert.That(ButtonWithCaptionOrNull(EpisodeDirector.ShareInformationNextCaption), Is.Null);
                }
                yield return KeyboardSubmit(SharingReviewCaption(30));
                Assert.That(EventSystem.current.currentSelectedGameObject?.name, Is.EqualTo(EpisodeDirector.ShareInformationCancelCaption),
                    "Review defaults to Cancel, so a repeated Enter cannot share.");
                yield return WaitOutThePointerHold();
                yield return AssertKeyboardRing("Memory confirmation", ModalRoot);
                yield return KeyboardSubmit(EpisodeDirector.ShareInformationCancelCaption);
                AssertSharingUnchanged(before);
            }
        }

        [UnityTest]
        public IEnumerator InformationShare_LongMemoryShowsTheExactBoundedReceiptBeforeItIsCommitted()
        {
            yield return InstallSharingHouse(1, longText: true);
            var before = director.Snapshot;
            string recipient = Listener(before).id;
            var option = InformationShareChoice.Open(before, recipient).Options.Single();
            Assert.That(option.Shortened, Is.True);
            yield return OpenInformationShare();
            ButtonWithCaption(SharingReviewCaption(1)).onClick.Invoke();
            yield return WaitOutThePointerHold();
            var original = director.GetComponentsInChildren<TMP_Text>().Single(t => t.name == EpisodeDirector.ShareMemoryTextName);
            var receipt = director.GetComponentsInChildren<TMP_Text>().Single(t => t.name == EpisodeDirector.ShareReceiptTextName);
            Assert.That(original.text, Is.EqualTo(option.Text));
            Assert.That(receipt.text, Is.EqualTo(option.ReceivedText));
            Assert.That(original.richText || receipt.richText, Is.False);
            Assert.That(ShownText(), Does.Contain("exact excerpt below"));
            AssertSharingUnchanged(before);
            yield return KeyboardSubmit(EpisodeDirector.ShareInformationConfirmCaption);
            AssertSharedMemory(before, recipient, option);
        }

        [UnityTest]
        public IEnumerator InformationShare_EmptyKnowledgeShowsAnExitAndNeverOffersConfirmation()
        {
            yield return InstallSharingHouse(0);
            var before = director.Snapshot;
            yield return OpenInformationShare();
            Assert.That(ShownText(), Does.Contain("no eligible personal memories"));
            Assert.That(ButtonWithCaptionOrNull(EpisodeDirector.ShareInformationConfirmCaption), Is.Null);
            ButtonWithCaption(EpisodeDirector.ShareInformationCancelCaption).onClick.Invoke();
            AssertSharingUnchanged(before);
        }

        [UnityTest]
        public IEnumerator InformationShare_EscapeAndAnInterveningCommandRetireAConfirmation()
        {
            yield return InstallSharingHouse();
            var before = director.Snapshot;
            yield return OpenInformationShare();
            ButtonWithCaption(SharingReviewCaption(1)).onClick.Invoke();
            yield return WaitOutThePointerHold();
            var oldConfirm = ButtonWithCaption(EpisodeDirector.ShareInformationConfirmCaption).onClick;
            yield return PressKey(Key.Escape);
            Assert.That(director.IsChoosingSharedInformation, Is.False);
            oldConfirm.Invoke(); AssertSharingUnchanged(before);
            yield return TalkTo(Listener(before).id);
            yield return OpenInformationShare();
            ButtonWithCaption(SharingReviewCaption(1)).onClick.Invoke();
            yield return WaitOutThePointerHold();
            oldConfirm = ButtonWithCaption(EpisodeDirector.ShareInformationConfirmCaption).onClick;
            var result = director.Submit(new EpisodeCommand { id = "intervening-share-question", actorId = before.playerId,
                expectedRevision = before.revision, expectedPhase = before.phase, kind = EpisodeCommandKind.AskForIntel, targetId = Listener(before).id });
            Assert.That(result.accepted, Is.True, result.reason);
            var committed = director.Snapshot;
            oldConfirm.Invoke(); AssertSharingUnchanged(committed);
            Assert.That(director.IsChoosingSharedInformation, Is.False);
        }

        [UnityTest]
        public IEnumerator InformationShare_SameRevisionLoadAndSceneReloadDiscardTheUnsavedSelection()
        {
            yield return InstallSharingHouse();
            yield return OpenInformationShare();
            ButtonWithCaption(SharingReviewCaption(1)).onClick.Invoke();
            yield return WaitOutThePointerHold();
            var before = director.Snapshot;
            var oldConfirm = ButtonWithCaption(EpisodeDirector.ShareInformationConfirmCaption).onClick;
            director.LoadNow(); HoldTheHouseForTheFixture();
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision));
            var reloaded = director.Snapshot;
            oldConfirm.Invoke(); AssertSharingUnchanged(reloaded);
            Assert.That(director.IsChoosingSharedInformation, Is.False);
            yield return TalkTo(Listener(reloaded).id);
            yield return OpenInformationShare();
            ButtonWithCaption(SharingReviewCaption(1)).onClick.Invoke();
            yield return WaitOutThePointerHold();
            oldConfirm = ButtonWithCaption(EpisodeDirector.ShareInformationConfirmCaption).onClick;
            yield return ReloadEpisode(); HoldTheHouseForTheFixture();
            reloaded = director.Snapshot;
            oldConfirm.Invoke(); AssertSharingUnchanged(reloaded);
            Assert.That(director.IsChoosingSharedInformation, Is.False);
        }

        [UnityTest]
        public IEnumerator InformationShare_AFailedSaveDoesNotPublishTheSecretOrRetryOnAnOldClick()
        {
            yield return InstallSharingHouse();
            yield return OpenInformationShare();
            ButtonWithCaption(SharingReviewCaption(1)).onClick.Invoke();
            yield return WaitOutThePointerHold();
            var before = director.Snapshot;
            string path = director.SavePath;
            byte[] original = File.ReadAllBytes(path);
            var confirm = ButtonWithCaption(EpisodeDirector.ShareInformationConfirmCaption).onClick;
            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None)) confirm.Invoke();
            AssertSharingUnchanged(before);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
            Assert.That(director.IsChoosingSharedInformation, Is.False);
            confirm.Invoke(); AssertSharingUnchanged(before);
            // Recovery is a fresh deliberate action, not a stranded or automatically retried UI.
            Assert.That(director.SeasonInProgress, Is.False, "An unreadable primary requires explicit validated recovery.");
            director.LoadNow(); HoldTheHouseForTheFixture();
            Assert.That(director.SeasonInProgress, Is.True);
            AssertIntentDurable(before);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
            confirm.Invoke(); AssertSharingUnchanged(before);
            yield return TalkTo(Listener(before).id);
            yield return OpenInformationShare();
            ButtonWithCaption(SharingReviewCaption(1)).onClick.Invoke();
            yield return WaitOutThePointerHold();
            var option = InformationShareChoice.Open(before, Listener(before).id).Options[0];
            ButtonWithCaption(EpisodeDirector.ShareInformationConfirmCaption).onClick.Invoke();
            AssertSharedMemory(before, Listener(before).id, option);
        }

        [UnityTest]
        public IEnumerator InformationShare_CampaignAndLegacyConversationAdaptersKeepTheirSeparateContracts()
        {
            yield return InstallSharingHouse(campaign: true);
            var before = director.Snapshot;
            string recipient = Listener(before).id;
            var option = InformationShareChoice.Open(before, recipient).Options.Last();
            yield return OpenInformationShare();
            ButtonWithCaption(SharingReviewCaption(3)).onClick.Invoke();
            yield return WaitOutThePointerHold();
            ButtonWithCaption(EpisodeDirector.ShareInformationConfirmCaption).onClick.Invoke();
            AssertSharedMemory(before, recipient, option);
            Assert.That(director.Snapshot.phase, Is.EqualTo(EpisodePhase.Campaign));
            yield return InstallSharingHouse(rules: false);
            before = director.Snapshot;
            ButtonWithCaption(EpisodeDirector.ShareInformationCaption).onClick.Invoke();
            Assert.That(director.IsChoosingSharedInformation, Is.False);
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision + 1), "An old season keeps its one-step legacy share.");
        }

        [UnityTest]
        public IEnumerator InformationShare_PostNominationSharingSpendsOnlyThatWindowsOneAction()
        {
            yield return InstallSharingHouse(campaign: true, beforeVeto: true);
            var before = director.Snapshot;
            string recipient = Listener(before).id;
            var option = InformationShareChoice.Open(before, recipient).Options.Last();
            yield return OpenInformationShare();
            ButtonWithCaption(SharingReviewCaption(3)).onClick.Invoke();
            yield return WaitOutThePointerHold();
            ButtonWithCaption(EpisodeDirector.ShareInformationConfirmCaption).onClick.Invoke();
            AssertSharedMemory(before, recipient, option);
            Assert.That(director.Snapshot.windowActions[Windows.AfterNominations], Is.EqualTo(1));
            Assert.That(InformationShareChoice.Open(director.Snapshot, recipient), Is.Null);
            yield return null;
        }
    }
}
