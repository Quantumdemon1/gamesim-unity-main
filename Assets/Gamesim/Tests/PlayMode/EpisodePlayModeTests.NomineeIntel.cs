using System.Collections;
using System.IO;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Persistence;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        private IEnumerator InstallNomineeQuestion(int size = 8, bool rules = true, bool beforeVeto = false)
        {
            yield return InstallTalkingHouse(size, true, s =>
            {
                EpisodeEngine.EnableWeek(s);
                // Fixture construction only. Production selects this exclusively in StartSeason.
                s.economyRulesVersion = rules ? 1 : 0;
                if (beforeVeto) { s.phase = EpisodePhase.VetoMeeting; s.vetoResolved = false; }
            });
            yield return TalkTo(Listener(director.Snapshot).id);
        }

        private IEnumerator OpenNomineeQuestion()
        {
            ButtonWithCaption(EpisodeDirector.NomineeIntelPickerCaption).onClick.Invoke();
            yield return null;
            Assert.That(director.ConversationPicker, Is.EqualTo(EpisodeDirector.NomineeIntelPickerCaption));
        }

        private void AssertIntelUnchanged(EpisodeState before)
        {
            Assert.That(JsonUtility.ToJson(director.Snapshot), Is.EqualTo(JsonUtility.ToJson(before)),
                "A view-only or obsolete callback cannot spend, roll, learn or publish.");
        }

        private void AssertChosenIntel(EpisodeState before, string listener, string target)
        {
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(before.revision + 1));
            Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(EpisodeEngine.SocialActionsSpent(before) + 1));
            var standing = after.ledger.standings.Last();
            Assert.That((standing.fromId, standing.toId, standing.source), Is.EqualTo((listener, target, ClaimSource.Told)));
            var generic = new EpisodeEngine(before).Apply(new EpisodeCommand { id = "compare-untargeted", actorId = before.playerId,
                expectedPhase = before.phase, expectedRevision = before.revision, kind = EpisodeCommandKind.AskForIntel, targetId = listener });
            Assert.That(generic.accepted, Is.True, generic.reason);
            Assert.That(after.randomState, Is.EqualTo(generic.state.randomState));
            Assert.That(new EpisodeSaveStore(director.SavePath).TryLoad(out var disk, out string error), Is.True, error);
            Assert.That(JsonUtility.ToJson(disk), Is.EqualTo(JsonUtility.ToJson(after)), "The chosen answer is the durable state, not only a view.");
            Assert.That(director.ConversationPicker, Is.Null);
        }

        [UnityTest]
        public IEnumerator NomineeIntel_EachNomineeButtonCommitsOnlyThatQuestionAndPersistsTheAnswer()
        {
            for (int index = 0; index < 2; index++)
            {
                yield return InstallNomineeQuestion();
                var before = director.Snapshot;
                string listener = Listener(before).id, target = before.nominees[index];
                yield return OpenNomineeQuestion();
                AssertIntelUnchanged(before);
                foreach (string id in before.nominees)
                    Assert.That(ButtonWithCaption(EpisodeDirector.NomineeIntelCaption(before.Find(id).name)).IsInteractable(), Is.True);
                var click = ButtonWithCaption(EpisodeDirector.NomineeIntelCaption(before.Find(target).name)).onClick;
                click.Invoke();
                AssertChosenIntel(before, listener, target);
                var committed = director.Snapshot;
                click.Invoke();
                AssertIntelUnchanged(committed);
            }
        }

        [UnityTest]
        public IEnumerator NomineeIntel_CancelReopenAndAnotherPickerRetireOldChoicesAndOpeners()
        {
            yield return InstallNomineeQuestion();
            var before = director.Snapshot;
            string caption = EpisodeDirector.NomineeIntelCaption(before.Find(before.nominees[0]).name);
            var oldOpen = ButtonWithCaption(EpisodeDirector.NomineeIntelPickerCaption).onClick;
            yield return OpenNomineeQuestion();
            var oldPick = ButtonWithCaption(caption).onClick;
            ButtonWithCaption(EpisodeDirector.NomineeIntelPickerCaption).onClick.Invoke();
            Assert.That(director.ConversationPicker, Is.Null);
            oldPick.Invoke(); oldOpen.Invoke();
            Assert.That(director.ConversationPicker, Is.Null, "An old opener cannot reopen a canceled view.");
            AssertIntelUnchanged(before);
            yield return OpenNomineeQuestion();
            oldPick.Invoke();
            Assert.That(director.ConversationPicker, Is.EqualTo(EpisodeDirector.NomineeIntelPickerCaption));
            AssertIntelUnchanged(before);
            var switched = ButtonWithCaption(caption).onClick;
            ButtonWithCaption(EpisodeDirector.VentPickerCaption).onClick.Invoke();
            switched.Invoke();
            Assert.That(director.ConversationPicker, Is.EqualTo(EpisodeDirector.VentPickerCaption));
            AssertIntelUnchanged(before);
        }

        [UnityTest]
        public IEnumerator NomineeIntel_KeyboardCanOpenReachBothNomineesAndCancelAtBothTextSizes()
        {
            yield return InstallNomineeQuestion(16);
            foreach (bool larger in new[] { false, true })
            {
                yield return ApplyTextSize(larger);
                yield return TalkTo(Listener(director.Snapshot).id);
                var before = director.Snapshot;
                yield return KeyboardSubmit(EpisodeDirector.NomineeIntelPickerCaption);
                yield return null;
                var grid = ActiveRect(EpisodeHud.PersonPickerPrefix + EpisodeDirector.NomineeIntelPickerCaption);
                Assert.That(grid, Is.Not.Null);
                var controls = grid.GetComponentsInChildren<Button>().Where(b => b.IsActive()).ToList();
                Assert.That(controls.Select(b => b.name), Is.EquivalentTo(before.nominees
                    .Select(id => EpisodeDirector.NomineeIntelCaption(before.Find(id).name))));
                foreach (var control in controls)
                {
                    Assert.That(control.IsInteractable(), Is.True);
                    Assert.That(control.navigation.mode, Is.Not.EqualTo(Navigation.Mode.None));
                }
                yield return AssertKeyboardRing("Targeted nominee question at " + (larger ? "large" : "standard") + " text", ModalRoot);
                yield return KeyboardSubmit(EpisodeDirector.NomineeIntelPickerCaption);
                Assert.That(director.ConversationPicker, Is.Null);
                AssertIntelUnchanged(before);
            }
        }

        [UnityTest]
        public IEnumerator NomineeIntel_EscapeAndAnInterveningCommandRetireTheChoice()
        {
            yield return InstallNomineeQuestion();
            yield return OpenNomineeQuestion();
            var before = director.Snapshot;
            string listener = Listener(before).id;
            string caption = EpisodeDirector.NomineeIntelCaption(before.Find(before.nominees[0]).name);
            var oldPick = ButtonWithCaption(caption).onClick;
            yield return PressKey(Key.Escape);
            Assert.That(director.IsPanelOpen, Is.False);
            oldPick.Invoke(); AssertIntelUnchanged(before);
            yield return TalkTo(listener);
            Assert.That(director.ConversationPicker, Is.Null);
            yield return OpenNomineeQuestion();
            oldPick = ButtonWithCaption(caption).onClick;
            ButtonWithCaption("Ask what they have heard").onClick.Invoke();
            var committed = director.Snapshot;
            Assert.That(committed.revision, Is.EqualTo(before.revision + 1));
            oldPick.Invoke(); AssertIntelUnchanged(committed);
        }

        [UnityTest]
        public IEnumerator NomineeIntel_SameRevisionLoadAndSceneReplacementCannotReuseAQuestion()
        {
            yield return InstallNomineeQuestion();
            yield return OpenNomineeQuestion();
            var before = director.Snapshot;
            var oldPick = ButtonWithCaption(EpisodeDirector.NomineeIntelCaption(before.Find(before.nominees[0]).name)).onClick;
            director.LoadNow(); HoldTheHouseForTheFixture();
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision));
            var reloaded = director.Snapshot;
            oldPick.Invoke(); AssertIntelUnchanged(reloaded);
            yield return TalkTo(Listener(reloaded).id);
            yield return OpenNomineeQuestion();
            oldPick = ButtonWithCaption(EpisodeDirector.NomineeIntelCaption(reloaded.Find(reloaded.nominees[0]).name)).onClick;
            yield return ReloadEpisode(); HoldTheHouseForTheFixture();
            reloaded = director.Snapshot;
            oldPick.Invoke(); AssertIntelUnchanged(reloaded);
        }

        [UnityTest]
        public IEnumerator NomineeIntel_FailedSaveDoesNotRevealOrRetryTheQuestion()
        {
            yield return InstallNomineeQuestion();
            yield return OpenNomineeQuestion();
            var before = director.Snapshot;
            string path = director.SavePath;
            byte[] original = File.ReadAllBytes(path);
            var click = ButtonWithCaption(EpisodeDirector.NomineeIntelCaption(before.Find(before.nominees[0]).name)).onClick;
            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None)) click.Invoke();
            AssertIntelUnchanged(before);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
            Assert.That(director.ConversationPicker, Is.Null);
            click.Invoke(); AssertIntelUnchanged(before);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NomineeIntel_LegacyAndMoveInConversationsKeepTheGenericQuestionOnly()
        {
            yield return InstallNomineeQuestion(rules: false);
            Assert.That(ButtonWithCaptionOrNull(EpisodeDirector.NomineeIntelPickerCaption), Is.Null);
            Assert.That(ButtonWithCaption("Ask what they have heard"), Is.Not.Null);
            yield return InstallTalkingHouse(8, false, s => { EpisodeEngine.EnableWeek(s); s.economyRulesVersion = 1; });
            yield return TalkTo(Listener(director.Snapshot).id);
            Assert.That(ButtonWithCaptionOrNull(EpisodeDirector.NomineeIntelPickerCaption), Is.Null);
            Assert.That(ButtonWithCaption("Ask what they have heard"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator NomineeIntel_ThePostNominationWindowUsesItsOwnSingleAction()
        {
            yield return InstallNomineeQuestion(beforeVeto: true);
            var before = director.Snapshot;
            string listener = Listener(before).id, target = before.nominees[0];
            yield return OpenNomineeQuestion();
            ButtonWithCaption(EpisodeDirector.NomineeIntelCaption(before.Find(target).name)).onClick.Invoke();
            AssertChosenIntel(before, listener, target);
            Assert.That(director.Snapshot.windowActions[Windows.AfterNominations], Is.EqualTo(1));
        }
    }
}
