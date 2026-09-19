using System.Collections;
using Gamesim.House;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// A season larger than the six the house was built for, seated, bound, saved and reloaded.
    ///
    /// <para>The standalone verifier found this: after a new season from the cast screen and a
    /// reload, every houseguest stood still and the status read "Housemate activity is paused:
    /// Identity rebinding requires an explicitly unbound active owner, disabled owned agent and
    /// valid identity/index." The identity and the agent were fine; the index was not. The motion
    /// owner still refused any cast slot above four - the six-person slice's bound - long after the
    /// cast screen learned to seat sixteen, and a cast whose houseguests landed on later roots
    /// could never bind again.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator NpcRuntime_ASeasonSeatedBeyondTheSixthSlotBindsAndSurvivesAReload()
        {
            yield return WaitForNpcRuntimeBinding();

            director.StartSeason(new SeasonBuilder.Choice { HouseSize = 12 });
            Assert.That(director.Snapshot.contestants, Has.Count.EqualTo(12), "The cast screen can seat twelve.");
            // Sixteen is the largest house the cast screen seats; the player takes one seat, and
            // slots count from zero, so the last houseguest's slot is fourteen.
            Assert.That(HouseNpcMotion.MaxCastIndex, Is.GreaterThanOrEqualTo(16 - 2),
                "Every non-player slot of the largest house must be bindable.");
            yield return null; yield return null;
            yield return WaitForNpcRuntimeBinding();

            director.SaveNow();
            director.LoadNow();
            Assert.That(director.StatusMessage, Does.StartWith("Local episode loaded and validated."), director.StatusMessage);
            yield return null; yield return null;
            yield return WaitForNpcRuntimeBinding();
            Assert.That(director.StatusMessage, Does.Not.Contain("Housemate activity is paused"), director.StatusMessage);
            Assert.That(director.NpcAutonomyReady, Is.True, director.NpcAutonomyDiagnostic);
        }
    }
}
