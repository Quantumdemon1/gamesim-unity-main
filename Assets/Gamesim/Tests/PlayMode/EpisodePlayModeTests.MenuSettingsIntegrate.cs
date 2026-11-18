using System.Collections;
using System.Reflection;
using Gamesim.Episode;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Menu slice 4, as merged: the main menu's "Load a season" opens Settings on Saves even while a
    /// save cannot be read (blocked recovery), whose Settings would otherwise open on Advanced.
    ///
    /// <para>The only save here is the fixture's own, in its temporary save root (LoadIsolatedEpisode).
    /// The case holds the house first, and leaves recovery by loading that save again.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator MenuSettingsIntegrate_LoadASeasonInRecoveryOpensSaves()
        {
            HoldTheHouseForTheFixture();
            director.ClosePanels();
            yield return Frames(2);
            director.SaveNow();
            typeof(EpisodeDirector).GetMethod("EnterRecoveryLock", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(director, null);
            Assert.That(director.SeasonInProgress, Is.False, "The house is in blocked recovery,");
            director.OpenMainMenu();
            yield return Frames(2);
            Assert.That(CastButtons(MainMenu.LoadCaption), Has.Length.EqualTo(1), "and the main menu offers \"Load a season\".");

            CastButtons(MainMenu.LoadCaption)[0].onClick.Invoke();
            yield return Frames(2);
            Assert.That(Menu().IsShowing, Is.False);
            Assert.That(director.AreSettingsSectionsOpen, Is.True, "It opens Settings,");
            Assert.That(director.SettingsSectionShown, Is.EqualTo(SettingsSection.Saves), "on Saves, not on Advanced where recovery opens them.");

            director.LoadNow();
            yield return null;
        }
    }
}
