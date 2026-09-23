using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// The diary room with nothing of the player's to decide (Refinement Kit 6's preview 05):
        /// the record's tab opens first and says so, the values the room keeps are rows of their
        /// own, the long rules wait behind a disclosure, and the line under the frame says where
        /// the player is - it used to go on telling them to walk in and press E. Reading every tab
        /// and opening the rules changes nothing.
        /// </summary>
        [UnityTest]
        public IEnumerator Diary_TheRecordIsThreeTabsAndReadingThemChangesNothing()
        {
            yield return InstallDiaryFixture(state => state.phase == EpisodePhase.HoH && state.pendingDiary == null
                && state.Find(state.playerId).status == ContestantStatus.Active, "a competition with nothing of the player's to decide");
            var before = director.Snapshot;
            player.Agent.speed = 25; player.Agent.acceleration = 100;
            director.GoToDiary();
            Assert.That(ActiveDiaryText(), Does.Contain("Walk to the private room"), "The way there says how to get in.");
            yield return WaitForDiaryWalk();
            Assert.That(director.TryOpenDiary(), Is.True);
            yield return WaitForDiarySeating();
            Canvas.ForceUpdateCanvases();

            string text = ActiveDiaryText();
            Assert.That(text, Does.Not.Contain("Walk to the private room"), "Inside, the line says where the player is.");
            Assert.That(text, Does.Contain(EpisodeDirector.DiaryInsideMessage));
            var status = ActiveRect(EpisodeHud.DiaryStatusName);
            Assert.That(status, Is.Not.Null, "The record's tab opens first when there is nothing to decide.");
            Assert.That(status.GetComponentsInChildren<TMP_Text>().Select(t => t.text), Does.Contain("No private decision pending"));
            foreach (var row in new[] { "Study preparation", "Diary persona", "Recorded jury impression" })
                Assert.That(ActiveRect(EpisodeHud.DiaryRecordRowPrefix + row), Is.Not.Null, row + " is a row of the record.");
            Assert.That(ActiveRect(EpisodeHud.DiaryRecordRowPrefix + "Study preparation").GetComponentsInChildren<TMP_Text>()
                .Select(t => t.text), Does.Contain(before.playerStudyBonus + " / 5"));
            if (before.jurySentiment.jurors.Count == 0)
                Assert.That(ActiveRect(EpisodeHud.DiaryRecordRowPrefix + "Recorded jury impression").GetComponentsInChildren<TMP_Text>()
                    .Select(t => t.text), Does.Contain("None yet"), "No jury is no record, not a zero.");
            Assert.That(text, Does.Not.Contain("not precision play or final HoH"), "The long rules are behind the disclosure.");

            ButtonWithCaption(EpisodeHud.DiaryRulesCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(ActiveDiaryText(), Does.Contain("not precision play or final HoH"));
            Assert.That(ActiveDiaryText(), Does.Contain("an impression, not a promise"));
            if (Application.isBatchMode) yield return CaptureFraming("diary-record");

            ButtonWithCaption(EpisodeDirector.DiaryMemoriesTabCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(ActiveRect(EpisodeHud.DiaryStatusName), Is.Null);
            ButtonWithCaption(EpisodeDirector.DiaryPendingTabCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(ActiveRect(EpisodeHud.DiaryNothingPendingName), Is.Not.Null, "Nothing pending is said, not invented.");

            var column = ActiveRect("Episode panel");
            foreach (var label in column.GetComponentsInChildren<TMP_Text>())
            {
                label.ForceMeshUpdate();
                Assert.That(label.isTextOverflowing, Is.False, "'" + label.text + "' fits its box.");
            }
            AssertEquivalent(before, director.Snapshot);
            director.ClosePanels();
            yield return null;
            Assert.That(ActiveDiaryText(), Does.Not.Contain(EpisodeDirector.DiaryInsideMessage), "and once out, it no longer says in.");
            AssertEquivalent(before, director.Snapshot);
        }
    }
}
