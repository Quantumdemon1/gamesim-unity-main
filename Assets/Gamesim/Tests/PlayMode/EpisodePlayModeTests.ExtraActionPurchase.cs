using System;
using System.Collections;
using System.IO;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Persistence;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        private void AssertPurchaseUnchanged(EpisodeState before, string reason)
        {
            Assert.That(JsonUtility.ToJson(director.Snapshot), Is.EqualTo(JsonUtility.ToJson(before)), reason);
        }

        private IEnumerator OpenBridgePicker()
        {
            ButtonWithCaption(EpisodeHud.BuyBurnOneCaption).onClick.Invoke();
            // Observe the guard at the actual replacement, not after an arbitrarily slow UMA frame
            // has legitimately outlived its real-time duration.
            Assert.That(director.GetComponentInChildren<EpisodeHud>().PointerHeld, Is.True);
            yield return null; yield return null;
            Assert.That(director.IsChoosingActionPurchase, Is.True);
            Assert.That(ButtonWithCaption(EpisodeDirector.BurnBridgeCancelCaption).IsInteractable(), Is.True);
        }

        private void AssertChosenPurchase(EpisodeState before, string target)
        {
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(before.revision + 1));
            Assert.That(after.boughtActionPoints, Is.EqualTo(before.boughtActionPoints + 1));
            Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(EpisodeEngine.SocialActionsSpent(before)));
            foreach (var npc in before.Active.Where(c => !c.isPlayer))
                Assert.That(after.Score(after.playerId, npc.id), npc.id == target
                    ? Is.LessThan(before.Score(before.playerId, npc.id)) : Is.EqualTo(before.Score(before.playerId, npc.id)), npc.name);
            Assert.That(new EpisodeSaveStore(director.SavePath).TryLoad(out var disk, out var error), Is.True, error);
            Assert.That(disk.revision, Is.EqualTo(after.revision));
            Assert.That(disk.boughtActionPoints, Is.EqualTo(after.boughtActionPoints));
            Assert.That(Goodwill(disk), Is.EqualTo(Goodwill(after)));
            Assert.That(disk.randomState, Is.EqualTo(after.randomState));
            Assert.That(director.IsChoosingActionPurchase, Is.False);
        }

        [UnityTest]
        public IEnumerator ExtraActionPurchase_BoardShowsTheWholeChoiceAndCommitsOnlyTheChosenPerson()
        {
            yield return InstallFreeTime(8, FreeTimeWaiting.Nothing);
            yield return OpenStation();
            var before = director.Snapshot;
            yield return OpenBridgePicker();
            AssertPurchaseUnchanged(before, "Opening the picker spends nothing and draws no random target.");
            foreach (var npc in before.Active.Where(c => !c.isPlayer))
                Assert.That(ButtonWithCaption(EpisodeDirector.BurnBridgeTargetCaption(npc.name)).IsInteractable(), Is.True);
            Assert.That(ButtonWithCaptionOrNull(EpisodeDirector.BurnBridgeTargetCaption(before.Find(before.playerId).name)), Is.Null);
            var chosen = before.Active.Last(c => !c.isPlayer);
            var click = ButtonWithCaption(EpisodeDirector.BurnBridgeTargetCaption(chosen.name)).onClick;
            click.Invoke();
            AssertChosenPurchase(before, chosen.id);
            var committed = director.Snapshot;
            click.Invoke();
            AssertPurchaseUnchanged(committed, "A retained double click cannot buy a second action.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExtraActionPurchase_CancelAndReopenRejectsCallbacksFromTheCanceledView()
        {
            yield return InstallFreeTime(8, FreeTimeWaiting.Nothing);
            yield return OpenStation();
            var before = director.Snapshot;
            byte[] originalSave = File.ReadAllBytes(director.SavePath);
            yield return OpenBridgePicker();
            var npc = before.Active.Last(c => !c.isPlayer);
            var oldPick = ButtonWithCaption(EpisodeDirector.BurnBridgeTargetCaption(npc.name)).onClick;
            var oldCancel = ButtonWithCaption(EpisodeDirector.BurnBridgeCancelCaption).onClick;
            oldCancel.Invoke();
            Assert.That(director.IsChoosingActionPurchase, Is.False);
            AssertPurchaseUnchanged(before, "Cancel changes neither budget, relationships nor RNG.");
            Assert.That(File.ReadAllBytes(director.SavePath), Is.EqualTo(originalSave));
            yield return OpenBridgePicker();
            oldPick.Invoke();
            oldCancel.Invoke();
            AssertPurchaseUnchanged(before, "An old callback has no authority in a new view at the same revision.");
            Assert.That(director.IsChoosingActionPurchase, Is.True, "The old cancel cannot close the new view.");
            ButtonWithCaption(EpisodeDirector.BurnBridgeTargetCaption(npc.name)).onClick.Invoke();
            AssertChosenPurchase(before, npc.id);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExtraActionPurchase_AnotherCommittedRevisionInvalidatesTheSelection()
        {
            yield return InstallFreeTime(8, FreeTimeWaiting.Nothing);
            yield return OpenStation();
            var before = director.Snapshot;
            yield return OpenBridgePicker();
            var oldPick = ButtonWithCaption(EpisodeDirector.BurnBridgeTargetCaption(before.Active.Last(c => !c.isPlayer).name)).onClick;
            var result = director.Submit(new EpisodeCommand { id = "intervening-purchase", actorId = before.playerId,
                expectedPhase = before.phase, expectedRevision = before.revision, kind = EpisodeCommandKind.BuyActionPoint,
                text = WebSocialVocabulary.SpreadAll });
            Assert.That(result.accepted, Is.True, result.reason);
            var now = director.Snapshot;
            oldPick.Invoke();
            Assert.That(director.IsChoosingActionPurchase, Is.False);
            AssertPurchaseUnchanged(now, "Stale UI cannot spend from the newly committed revision.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExtraActionPurchase_HouseguestScreenUsesTheSameExplicitChoiceAndReturnsThere()
        {
            yield return InstallFreeTime(8, FreeTimeWaiting.Nothing);
            yield return OpenStation();
            var person = director.Snapshot.Active.First(c => !c.isPlayer);
            director.OpenHouseguestScreen(person.id);
            var before = director.Snapshot;
            yield return OpenBridgePicker();
            ButtonWithCaption(EpisodeDirector.BurnBridgeCancelCaption).onClick.Invoke();
            Assert.That(director.HouseguestScreenFor, Is.EqualTo(person.id));
            AssertPurchaseUnchanged(before, "Cancel returns to the houseguest screen without spending.");
            yield return OpenBridgePicker();
            var chosen = before.Active.Last(c => !c.isPlayer);
            ButtonWithCaption(EpisodeDirector.BurnBridgeTargetCaption(chosen.name)).onClick.Invoke();
            AssertChosenPurchase(before, chosen.id);
            Assert.That(director.HouseguestScreenFor, Is.EqualTo(person.id));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExtraActionPurchase_CampaignMoreWaysOffersTheChoiceAndPreservesTheCampaign()
        {
            yield return InstallCampaign(46);
            HoldTheHouseForTheFixture();
            yield return OpenStation();
            ButtonWithCaption(EpisodeDirector.CampaignMoreCaption).onClick.Invoke();
            var before = director.Snapshot;
            yield return OpenBridgePicker();
            var chosen = before.Active.Last(c => !c.isPlayer);
            ButtonWithCaption(EpisodeDirector.BurnBridgeTargetCaption(chosen.name)).onClick.Invoke();
            AssertChosenPurchase(before, chosen.id);
            Assert.That(director.Snapshot.phase, Is.EqualTo(EpisodePhase.Campaign));
            Assert.That(ButtonWithCaption(EpisodeDirector.CampaignLessCaption).IsInteractable(), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExtraActionPurchase_FinalThreeFreeTimeDoesNotLoseTheChoice()
        {
            yield return InstallFreeTime(3, FreeTimeWaiting.Nothing);
            yield return OpenStation();
            var before = director.Snapshot;
            yield return OpenBridgePicker();
            Assert.That(director.GetComponentsInChildren<Button>().Count(b => b.IsActive()
                && before.Active.Where(c => !c.isPlayer).Any(c => b.name == EpisodeDirector.BurnBridgeTargetCaption(c.name))), Is.EqualTo(2));
            var chosen = before.Active.Last(c => !c.isPlayer);
            ButtonWithCaption(EpisodeDirector.BurnBridgeTargetCaption(chosen.name)).onClick.Invoke();
            AssertChosenPurchase(before, chosen.id);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExtraActionPurchase_FullCastKeyboardDefaultsToCancelAndReachesEveryTargetAtBothTextSizes()
        {
            yield return InstallFreeTime(16, FreeTimeWaiting.Nothing);
            foreach (bool larger in new[] { false, true })
            {
                yield return ApplyTextSize(larger);
                yield return OpenStation();
                var before = director.Snapshot;
                yield return KeyboardSubmit(EpisodeHud.BuyBurnOneCaption);
                yield return null; yield return null;
                Assert.That(EventSystem.current.currentSelectedGameObject?.name, Is.EqualTo(EpisodeDirector.BurnBridgeCancelCaption),
                    "A second Enter on the opener must cancel, never spend.");
                foreach (var npc in before.Active.Where(c => !c.isPlayer))
                    Assert.That(ButtonWithCaption(EpisodeDirector.BurnBridgeTargetCaption(npc.name)).IsInteractable(), Is.True);
                yield return AssertKeyboardRing("Extra action purchase at " + (larger ? "large" : "standard") + " text", ModalRoot);
                AssertPurchaseUnchanged(before, "Keyboard traversal does not buy anything.");
                yield return KeyboardSubmit(EpisodeDirector.BurnBridgeCancelCaption);
                AssertPurchaseUnchanged(before, "Keyboard cancel does not buy anything.");
                director.ClosePanels();
            }
        }

        [UnityTest]
        public IEnumerator ExtraActionPurchase_EscapeAndSceneReloadInvalidateRetainedCallbacks()
        {
            yield return InstallFreeTime(8, FreeTimeWaiting.Nothing);
            yield return OpenStation();
            yield return OpenBridgePicker();
            var before = director.Snapshot;
            string caption = EpisodeDirector.BurnBridgeTargetCaption(before.Active.Last(c => !c.isPlayer).name);
            var oldPick = ButtonWithCaption(caption).onClick;
            yield return PressKey(Key.Escape);
            Assert.That(director.IsPanelOpen, Is.False);
            oldPick.Invoke();
            AssertPurchaseUnchanged(before, "Escape retires the pending purchase.");
            yield return OpenStation();
            yield return OpenBridgePicker();
            oldPick = ButtonWithCaption(caption).onClick;
            yield return ReloadEpisode();
            HoldTheHouseForTheFixture();
            var reloaded = director.Snapshot;
            Assert.That(director.IsChoosingActionPurchase, Is.False, "View state is not saved.");
            oldPick.Invoke();
            AssertPurchaseUnchanged(reloaded, "A destroyed director's callback cannot spend from the reloaded slot.");
        }

        [UnityTest]
        public IEnumerator ExtraActionPurchase_ReloadingTheSameSlotRetiresTheViewEvenAtTheSameRevision()
        {
            yield return InstallFreeTime(8, FreeTimeWaiting.Nothing);
            yield return OpenStation();
            yield return OpenBridgePicker();
            var before = director.Snapshot;
            var oldPick = ButtonWithCaption(EpisodeDirector.BurnBridgeTargetCaption(before.Active.Last(c => !c.isPlayer).name)).onClick;
            director.LoadNow();
            HoldTheHouseForTheFixture();
            Assert.That(director.IsChoosingActionPurchase, Is.False);
            var reloaded = director.Snapshot;
            Assert.That(reloaded.sessionId, Is.EqualTo(before.sessionId));
            Assert.That(reloaded.revision, Is.EqualTo(before.revision));
            oldPick.Invoke();
            AssertPurchaseUnchanged(reloaded, "Re-loading identical saved state still retires the view's authority.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExtraActionPurchase_LockedSaveDoesNotPublishOrReplayThePurchase()
        {
            yield return InstallFreeTime(8, FreeTimeWaiting.Nothing);
            yield return OpenStation();
            yield return OpenBridgePicker();
            var before = director.Snapshot;
            string path = director.SavePath;
            byte[] original = File.ReadAllBytes(path);
            var click = ButtonWithCaption(EpisodeDirector.BurnBridgeTargetCaption(before.Active.Last(c => !c.isPlayer).name)).onClick;
            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None)) click.Invoke();
            AssertPurchaseUnchanged(before, "A failed durable save publishes no goodwill charge or extra action.");
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original), "The original slot is retained byte-for-byte.");
            Assert.That(director.IsChoosingActionPurchase, Is.False);
            click.Invoke();
            AssertPurchaseUnchanged(before, "Releasing the file lock does not authorize a retained callback to retry.");
            yield return null;
        }
    }
}
