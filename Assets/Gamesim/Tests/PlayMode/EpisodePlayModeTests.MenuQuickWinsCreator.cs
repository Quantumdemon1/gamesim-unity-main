using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine.InputSystem;
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

        /// <summary>
        /// The creator on its Identity step with a name typed, then its gear pressed: Settings up
        /// over the season and the creator stepped aside.
        /// </summary>
        private IEnumerator GearIntoSettingsFromIdentity(string name)
        {
            CastButtons("Identity").Single().onClick.Invoke();
            yield return null;
            CreatorEntryName(name);
            yield return null;
            CreatorGear().onClick.Invoke();
            yield return null; yield return null;
            Assert.That(Creator().IsShowing, Is.False, "The creator steps aside for Settings,");
            Assert.That(director.IsPanelOpen, Is.True, "which are open.");
        }

        private void AssertCreatorBackAsItWas(string name, string how)
        {
            Assert.That(Creator().IsShowing, Is.True, "Closing Settings " + how + " comes back to the creator,");
            Assert.That(Creator().Page, Is.EqualTo("Identity"), "on the same step,");
            Assert.That(Creator().Mode, Is.EqualTo(CharacterCreator.EntryMode.Detailed));
            Assert.That(Creator().Draft.Name, Is.EqualTo(name), "with the draft as it was.");
            Assert.That(director.IsPanelOpen, Is.False);
        }

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

        // The ways a player closes Settings: the panel's own Close control, Escape and the pad's B.
        // OpenCreator reaches the cast screen through Settings' own New season row, which leaves
        // Settings open under it, as they are for a player who starts a new season from Settings:
        // the gear used to lose its way back there.

        [UnityTest]
        public IEnumerator QuickWin12_SettingsClosedWithTheirCloseControlBringTheCreatorBack()
        {
            yield return OpenCreator();
            yield return GearIntoSettingsFromIdentity("Close Robin");
            ButtonWithCaption(WayOut.CloseCaption).onClick.Invoke();
            yield return Frames(3);
            AssertCreatorBackAsItWas("Close Robin", "with their Close control");
        }

        [UnityTest]
        public IEnumerator QuickWin12_SettingsClosedWithEscapeBringTheCreatorBack()
        {
            yield return OpenCreator();
            yield return GearIntoSettingsFromIdentity("Escape Robin");
            yield return PressKey(Key.Escape);
            yield return Frames(2);
            AssertCreatorBackAsItWas("Escape Robin", "with Escape");
        }

        [UnityTest]
        public IEnumerator QuickWin12_SettingsClosedWithThePadsBackBringTheCreatorBack()
        {
            yield return OpenCreator();
            yield return GearIntoSettingsFromIdentity("Pad Robin");
            yield return PressBack();
            yield return Frames(2);
            AssertCreatorBackAsItWas("Pad Robin", "with the pad's B");
        }

        /// <summary>The creator reached from the main menu's New season, with no Settings open under it, comes back too.</summary>
        [UnityTest]
        public IEnumerator QuickWin12_TheGearOnACreatorFromTheMainMenuComesBackToo()
        {
            director.OpenMainMenu();
            yield return Frames(2);
            CastButtons(MainMenu.NewSeasonCaption).Single().onClick.Invoke();
            yield return Frames(2);
            Assert.That(CastScreen().IsShowing, Is.True, "The menu's New season opens the cast screen.");
            CastButtons(CharacterCreator.CreateCaption).Single().onClick.Invoke();
            yield return Frames(2);
            CastButtons(CharacterCreator.DetailedCaption)[0].onClick.Invoke();
            yield return Frames(2);
            Assert.That(Creator().Mode, Is.EqualTo(CharacterCreator.EntryMode.Detailed));
            yield return GearIntoSettingsFromIdentity("Menu Robin");
            ButtonWithCaption(WayOut.CloseCaption).onClick.Invoke();
            yield return Frames(3);
            AssertCreatorBackAsItWas("Menu Robin", "opened over a creator from the main menu");
        }

        /// <summary>
        /// Settings left for a new season, through their own row, and then come back to by backing
        /// out of the cast screen: closing them now does not bring back the creator the player left.
        /// </summary>
        [UnityTest]
        public IEnumerator QuickWin12_SettingsLeftForANewSeasonDoNotBringTheCreatorBack()
        {
            yield return OpenCreator();
            yield return GearIntoSettingsFromIdentity("Left Robin");
            ButtonWithCaption(EpisodeDirector.NewSeasonInNewSlotCaption).onClick.Invoke();
            yield return Frames(2);
            Assert.That(CastScreen().IsShowing, Is.True, "The player asked for a new season,");
            yield return PressKey(Key.Escape);
            yield return Frames(2);
            Assert.That(CastScreen().IsShowing, Is.False, "backed out of its cast screen,");
            Assert.That(director.IsPanelOpen, Is.True, "and is in Settings again.");
            ButtonWithCaption(WayOut.CloseCaption).onClick.Invoke();
            yield return Frames(3);
            Assert.That(Creator().IsShowing, Is.False, "Closing them does not bring back the creator they left,");
            Assert.That(director.IsPanelOpen, Is.False);
        }
    }
}
