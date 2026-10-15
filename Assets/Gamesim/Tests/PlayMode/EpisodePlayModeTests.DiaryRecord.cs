using System.Collections;
using System.IO;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
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
            HoldTheHouseForTheFixture();
            yield return PressKey(Key.LeftCtrl);
            Assert.That(director.HintsForPad, Is.False, "The fixture has selected the keyboard's actual glossary.");
            var before = director.Snapshot;
            string beforeJson = JsonUtility.ToJson(before);
            byte[] beforeSave = File.ReadAllBytes(director.SavePath);
            void AssertReadingUnchanged(string where)
            {
                Assert.That(JsonUtility.ToJson(director.Snapshot), Is.EqualTo(beforeJson), where + ": the complete state is unchanged.");
                Assert.That(File.ReadAllBytes(director.SavePath), Is.EqualTo(beforeSave), where + ": the saved bytes are unchanged.");
            }
            player.Agent.speed = 25; player.Agent.acceleration = 100;
            director.GoToDiary();
            string travelLine = InputGlossary.DiaryWayLine(director.LastTravel == EpisodeDirector.TravelKind.Warp, director.HintsForPad);
            Assert.That(director.StatusMessage, Is.EqualTo(travelLine), "The way there uses the complete actual input glossary.");
            Assert.That(ActiveDiaryText(), Does.Contain(travelLine), "The way there says how to get in.");
            Canvas.ForceUpdateCanvases();
            var travelStatus = ActiveRect("Status").GetComponentsInChildren<TMP_Text>().Single();
            Assert.That(travelStatus.text, Is.EqualTo(travelLine), "The full glossary sentence reaches the visible status surface.");
            travelStatus.ForceMeshUpdate();
            Assert.That(travelStatus.isTextOverflowing, Is.False, "The travel guidance fits its box.");
            Assert.That(travelStatus.isTextTruncated, Is.False, "The complete travel guidance is drawn.");
            AssertReadingUnchanged("After requesting diary travel");
            yield return WaitForDiaryWalk();
            AssertReadingUnchanged("After arriving at the diary");
            Assert.That(director.TryOpenDiary(), Is.True);
            yield return WaitForDiarySeating();
            Canvas.ForceUpdateCanvases();

            string text = ActiveDiaryText();
            Assert.That(text, Does.Not.Contain(travelLine), "Inside, the line says where the player is.");
            Assert.That(text, Does.Contain(EpisodeDirector.DiaryInsideMessage));
            AssertReadingUnchanged("After opening and seating in the diary");
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
            Assert.That(text, Does.Not.Contain(EpisodeDirector.StudyUse(before)), "The long rules are behind the disclosure.");

            ButtonWithCaption(EpisodeHud.DiaryRulesCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(ActiveDiaryText(), Does.Contain(EpisodeDirector.StudyUse(before)),
                "Where preparation counts, as the season's competition rules have it.");
            Assert.That(ActiveDiaryText(), Does.Contain("an impression, not a promise"));
            AssertReadingUnchanged("After opening the rules");
            if (Application.isBatchMode) yield return CaptureFraming("diary-record");

            ButtonWithCaption(EpisodeDirector.DiaryMemoriesTabCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(ActiveRect(EpisodeHud.DiaryStatusName), Is.Null);
            AssertReadingUnchanged("After browsing memories");
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
            AssertReadingUnchanged("After browsing the pending tab");
            director.ClosePanels();
            yield return null;
            Assert.That(ActiveDiaryText(), Does.Not.Contain(EpisodeDirector.DiaryInsideMessage), "and once out, it no longer says in.");
            AssertEquivalent(before, director.Snapshot);
            AssertReadingUnchanged("After closing the diary");
        }
    }
}
