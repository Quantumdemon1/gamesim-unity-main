using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Pick, then commit (UI-UX-PLAN O1a, O1b, D5; EpisodeDirector.PickThenConfirm.cs): at the veto
    /// meeting a press picks the choice, lit in place, and only the one button pinned under the
    /// screen, named by the result, commits it; Escape clears a pick and leaves the screen up; and a
    /// notebook page opened over a decision goes back to it with the picks as they were left.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// The commit under a decision screen after the press that picked the choice: it names the
        /// pick's result, and pressing it is the one command.
        /// </summary>
        private IEnumerator CommitThePick()
        {
            string caption = director.DecisionPickCommitCaption;
            Assert.That(caption, Is.Not.Null, "A pick waits under its commit.");
            ButtonWithCaption(caption).onClick.Invoke();
            yield return null; yield return null;
        }

        [UnityTest]
        public IEnumerator PickThenConfirm_TheHoldersSaveIsPickedLitClearedByEscapeAndCommittedOnlyByTheNamedButton()
        {
            yield return InstallStrategySeason(84, state => AtVetoMeeting(state, playerHolds: true));
            HoldTheHouseForTheFixture();
            yield return OpenStation();
            yield return null;
            var before = director.Snapshot;
            int revision = before.revision;
            string saved = before.nominees[0];
            string saveCaption = EpisodeDirector.VetoSaveCaption(before, saved);
            string commit = DecisionPick.VetoCommitCaption(before, true, saved, null);
            Assert.That(director.DecisionPickCommitCaption, Is.Null, "Nothing is picked on opening.");
            Assert.That(ButtonWithCaptionOrNull(commit), Is.Null, "and no commit stands under the screen.");

            ButtonWithCaption(saveCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "The first press commits nothing.");
            Assert.That(director.Snapshot.vetoResolved, Is.False);
            Assert.That(director.DecisionPickCommitCaption, Is.EqualTo(commit), "The commit names the save picked.");
            Assert.That(FindButton(saveCaption).transform.Find("Picked"), Is.Not.Null, "The save picked is lit in place.");

            yield return PressKey(Key.Escape);
            yield return null;
            Assert.That(director.IsPhasePanelOpen, Is.True, "Escape clears the pick before it closes the screen,");
            Assert.That(director.DecisionPickCommitCaption, Is.Null, "and the pick is gone,");
            Assert.That(ButtonWithCaptionOrNull(commit), Is.Null, "with its commit.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "Clearing commits nothing.");

            ButtonWithCaption("Do not use the veto").onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.DecisionPickCommitCaption, Is.EqualTo(DecisionPick.VetoCommitCaption(before, false, null, null)),
                "Another choice replaces the pick.");
            ButtonWithCaption(saveCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "Picking again commits nothing.");

            yield return CommitThePick();
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(revision + 1), "The commit is one command,");
            Assert.That(after.vetoResolved, Is.True, "the veto used,");
            Assert.That(after.nominees, Does.Not.Contain(saved), "on the nominee picked.");
            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator PickThenConfirm_TheNotebookOverADecisionGoesBackToItWithThePicks()
        {
            // The nominations: two names picked, the notebook opened over them, and back.
            yield return InstallNomination(8, true, NominationBlock.Open);
            yield return OpenStation();
            yield return null; yield return null;
            var candidates = EpisodeEngine.NominationCandidates(director.Snapshot).Take(2).ToList();
            foreach (var candidate in candidates) ButtonWithCaption(candidate.name).onClick.Invoke();
            yield return null;
            string selected = "Selected: " + candidates[0].name + " and " + candidates[1].name;
            Assert.That(ActiveRect(EpisodeHud.SelectedNomineesName).GetComponent<TMPro.TMP_Text>().text, Is.EqualTo(selected));
            director.ShowNotebookSection(EpisodeDirector.NotebookSection.Notes);
            yield return null; yield return null;
            Assert.That(director.IsPhasePanelOpen, Is.False, "The notebook is up.");
            Assert.That(director.DecisionPeekBackCaption, Is.EqualTo(WayOut.BackTo(EpisodeDirector.NominationsPlace)));
            ButtonWithCaption(WayOut.BackTo(EpisodeDirector.NominationsPlace)).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.IsPhasePanelOpen, Is.True, "The way back is the nominations again,");
            Assert.That(ActiveRect(EpisodeHud.SelectedNomineesName).GetComponent<TMPro.TMP_Text>().text, Is.EqualTo(selected),
                "with both names still picked.");
            Assert.That(director.Snapshot.nominees, Is.Empty, "Nothing was committed.");
            ButtonWithCaption("Commit nominations").onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.Snapshot.nominees, Is.EquivalentTo(candidates.Select(candidate => candidate.id)), "The picks are the ones committed.");
            director.ClosePanels();
            yield return null;

            // The veto meeting: a save picked, the notebook's votes opened over it, and Escape back.
            yield return InstallStrategySeason(84, state => AtVetoMeeting(state, playerHolds: true));
            HoldTheHouseForTheFixture();
            yield return OpenStation();
            yield return null;
            var before = director.Snapshot;
            ButtonWithCaption(EpisodeDirector.VetoSaveCaption(before, before.nominees[1])).onClick.Invoke();
            yield return null; yield return null;
            string commit = DecisionPick.VetoCommitCaption(before, true, before.nominees[1], null);
            Assert.That(director.DecisionPickCommitCaption, Is.EqualTo(commit));
            director.ShowNotebookSection(EpisodeDirector.NotebookSection.Votes);
            yield return null; yield return null;
            Assert.That(director.DecisionPeekBackCaption, Is.EqualTo(WayOut.BackTo(EpisodeDirector.VetoMeetingPlace)));
            yield return PressKey(Key.Escape);
            yield return null;
            Assert.That(director.IsPhasePanelOpen, Is.True, "Escape on the notebook goes back to the meeting,");
            Assert.That(director.DecisionPickCommitCaption, Is.EqualTo(commit), "with the save still picked.");
            Assert.That(director.Snapshot.vetoResolved, Is.False, "Nothing was committed.");
            yield return CommitThePick();
            Assert.That(director.Snapshot.vetoResolved, Is.True);
            Assert.That(director.Snapshot.nominees, Does.Not.Contain(before.nominees[1]), "The save is the one picked before the look.");
            director.ClosePanels();
            yield return null;
        }
    }
}
