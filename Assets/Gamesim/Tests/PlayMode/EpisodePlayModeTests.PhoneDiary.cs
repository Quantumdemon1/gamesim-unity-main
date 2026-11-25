using System.Collections;
using System.IO;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Persistence;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        private Button PhoneReviewAction(string caption) => director.GetComponentInChildren<PhoneMenuPage>()
            .GetComponentsInChildren<Button>().Single(b => b.name == caption);

        [UnityTest]
        public IEnumerator PhoneDiary_AlternateVoteDiscardsFirstDraftAndConfirmSavesOnlyOnce()
        {
            yield return InstallDiaryFixture(s => s.phase == EpisodePhase.Eviction && !s.evictionResolved
                && s.evictionStage == EvictionStage.Voting && EpisodeEngine.Voters(s).Any(v => v.isPlayer)
                && !s.votes.Any(v => v.voterId == s.playerId), "eligible mobile ballot");
            director.GetComponent<EpisodeHud>().UsePhoneDecisionLayout = true;
            yield return OpenDiaryFixturePanel();
            var before = director.Snapshot;
            var bytes = File.ReadAllBytes(director.SavePath);
            string a = before.nominees[0], b = before.nominees[1];
            ButtonWithCaption("Vote to evict " + before.Find(a).name).onClick.Invoke();
            var stale = PhoneReviewAction(EpisodeHud.DiaryConfirmCaption).onClick;
            PhoneReviewAction("Choose " + before.Find(b).name + " instead").onClick.Invoke();
            stale.Invoke();
            AssertEquivalent(before, director.Snapshot);
            Assert.That(File.ReadAllBytes(director.SavePath), Is.EqualTo(bytes));
            Assert.That(PhoneReviewAction(before.Find(b).name).interactable, Is.False);
            var confirm = PhoneReviewAction(EpisodeHud.DiaryConfirmCaption).onClick;
            confirm.Invoke();
            var after = director.Snapshot;
            confirm.Invoke();
            AssertEquivalent(after, director.Snapshot);
            Assert.That(after.votes.Count(v => v.voterId == before.playerId), Is.EqualTo(1));
            Assert.That(after.votes.Single(v => v.voterId == before.playerId).targetId, Is.EqualTo(b));
            Assert.That(after.phase, Is.EqualTo(before.phase));
            Assert.That(after.evictionResolved, Is.False);
            Assert.That(new EpisodeSaveStore(director.SavePath).TryLoad(out var saved, out var message), Is.True, message);
            AssertEquivalent(after, saved);
        }

        [UnityTest]
        public IEnumerator PhoneDiary_CancelNominationPreservesBothStateAndSave()
        {
            yield return InstallDiaryFixture(s => s.phase == EpisodePhase.Nomination && s.hohId == s.playerId
                && s.nominees.Count == 0, "player nomination pair for phone");
            director.GetComponent<EpisodeHud>().UsePhoneDecisionLayout = true;
            yield return OpenDiaryFixturePanel();
            var before = director.Snapshot;
            var bytes = File.ReadAllBytes(director.SavePath);
            yield return ReviewDiaryNominations(before);
            var hud = director.GetComponent<EpisodeHud>();
            var heldConfirm = PhoneReviewAction(EpisodeHud.DiaryConfirmCaption).onClick;
            var heldBack = PhoneMenuTouchTests.PhoneExit(director, WayOut.BackObjectName, EpisodeHud.DiaryCancelCaption).onClick;
            var heldClose = PhoneMenuTouchTests.PhoneExit(director, WayOut.CloseObjectName, WayOut.CloseCaption).onClick;
            hud.HoldForReveal(true);
            heldConfirm.Invoke();
            heldBack.Invoke();
            heldClose.Invoke();
            Assert.That(director.GetComponentInChildren<PhoneMenuPage>(), Is.Null);
            Assert.That(director.HasDiaryDecisionDraft, Is.True);
            hud.HoldForReveal(false);
            EventSystem.current.SetSelectedGameObject(null);
            RenderHudForTheCurrentCanvas();
            yield return null;
            yield return null;
            var cancel = PhoneReviewAction(WayOut.BackObjectName);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(cancel.gameObject));
            EventSystem.current.SetSelectedGameObject(cancel.gameObject);
            RenderHudForTheCurrentCanvas();
            yield return null;
            yield return null;
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(PhoneReviewAction(WayOut.BackObjectName).gameObject));
            var stale = PhoneReviewAction(EpisodeHud.DiaryConfirmCaption).onClick;
            PhoneReviewAction(WayOut.BackObjectName).onClick.Invoke();
            stale.Invoke();
            Assert.That(director.HasDiaryDecisionDraft, Is.False);
            AssertEquivalent(before, director.Snapshot);
            Assert.That(File.ReadAllBytes(director.SavePath), Is.EqualTo(bytes));
        }
    }
}
