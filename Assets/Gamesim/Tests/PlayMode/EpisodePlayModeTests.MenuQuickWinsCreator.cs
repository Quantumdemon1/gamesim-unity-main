using System.Collections;
using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The creator's Settings gear (menu quick win 12): Settings open over the season, and closing
    /// them puts the creator back on the step it was on with the draft as it stood. Leaving
    /// Settings for somewhere else does not.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private Button CreatorGear() => Creator().GetComponentsInChildren<Button>()
            .Single(button => button.gameObject.activeInHierarchy && button.GetComponentInChildren<TMPro.TMP_Text>()?.text == CharacterCreator.SettingsCaption);

        [UnityTest]
        public IEnumerator QuickWin12_TheCreatorsGearOpensSettingsAndClosingThemReturnsToTheSameStep()
        {
            yield return OpenCreator();
            CastButtons("Identity").Single().onClick.Invoke();
            yield return null;
            CreatorEntryName("Gear Robin");
            yield return null;
            CreatorGear().onClick.Invoke();
            yield return null; yield return null;
            Assert.That(Creator().IsShowing, Is.False, "The creator steps aside for Settings,");
            Assert.That(director.IsPanelOpen, Is.True, "which are open.");

            director.ClosePanels();
            yield return null; yield return null; yield return null;
            Assert.That(Creator().IsShowing, Is.True, "Closing Settings comes back to the creator,");
            Assert.That(Creator().Page, Is.EqualTo("Identity"), "on the same step,");
            Assert.That(Creator().Mode, Is.EqualTo(CharacterCreator.EntryMode.Detailed));
            Assert.That(Creator().Draft.Name, Is.EqualTo("Gear Robin"), "with the draft as it was.");
            Assert.That(director.IsPanelOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator QuickWin12_SettingsLeftForTheMainMenuDoNotBringTheCreatorBack()
        {
            yield return OpenCreator();
            CreatorGear().onClick.Invoke();
            yield return null; yield return null;
            Assert.That(Creator().IsShowing, Is.False);
            director.OpenMainMenu();
            yield return null; yield return null; yield return null;
            Assert.That(Menu().IsShowing, Is.True, "The player asked for the menu,");
            Assert.That(Creator().IsShowing, Is.False, "and the creator does not come back over it.");
        }
    }
}
