#if GAMESIM_ALLSTAR_CANDIDATE
using System.Collections;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator AlumniSetup_DefaultsToHistoriesAndCommitsThePlayersChoice()
        {
            yield return OpenCastScreen();
            SeasonBuilder.Choice chosen = null;
            CastScreen().Show(choice => chosen = choice, () => { });
            CastButtons(CastTemplates.RosterName(CastTemplates.Roster.AllStars)).Single().onClick.Invoke();
            yield return null;

            var toggle = CastScreen().GetComponentsInChildren<Toggle>()
                .Single(control => control.name == CastSelect.PlayerHistoriesControlName);
            Assert.That(toggle.isOn, Is.True, "Past connections are on for a new All-Star setup.");
            toggle.isOn = false;
            CastButtons(CastSelect.StartCaption).Single().onClick.Invoke();
            Assert.That(chosen, Is.Not.Null);
            Assert.That(chosen.PlayerHistories, Is.False, "Starting passes the checkbox through the existing choice.");
            Assert.That(chosen.Copy().PlayerHistories, Is.False);
        }

        [UnityTest]
        public IEnumerator AlumniSetup_CreatorHandoffKeepsHistoriesOff()
        {
            yield return OpenCastScreen();
            SeasonBuilder.Choice creatorChoice = null;
            CastScreen().Show(choice => { }, () => { }, (choice, draft) => creatorChoice = choice);
            CastButtons(CastTemplates.RosterName(CastTemplates.Roster.AllStars)).Single().onClick.Invoke();
            yield return null;

            var toggle = CastScreen().GetComponentsInChildren<Toggle>()
                .Single(control => control.name == CastSelect.PlayerHistoriesControlName);
            toggle.isOn = false;
            CastButtons(CharacterCreator.CreateCaption).Single().onClick.Invoke();
            Assert.That(creatorChoice, Is.Not.Null);
            Assert.That(creatorChoice.PlayerHistories, Is.False, "Opening character creation must not restore the default.");
            var retained = creatorChoice.Copy();
            creatorChoice.PlayerHistories = true;
            Assert.That(retained.PlayerHistories, Is.False, "The creator's retained copy keeps the selected value.");
        }
    }
}
#endif
