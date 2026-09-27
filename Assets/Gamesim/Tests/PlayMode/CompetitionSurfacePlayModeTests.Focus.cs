using System.Collections;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class CompetitionSurfacePlayModeTests
    {
        /// <summary>
        /// A stray click on the frame's glass deselects everything. The HUD used to put the keyboard
        /// back; with it stood down for the whole attempt, the game does it itself - on the board
        /// while playing, on Pause while paused - so the arrows and the pad keep working.
        /// </summary>
        [UnityTest]
        public IEnumerator Focus_AStrayClickDoesNotStrandTheKeyboard()
        {
            CreateInput();
            var run = new MiniGameRun(CompetitionMiniGames.Kind.Reaction, 77, 3);
            screen.Show(run, "Competition", "Player", true, i => { }, () => { }, d => { }, () => { }, () => screen.Hide());
            yield return null; screen.AdvanceReady(4); yield return null;
            AssertSelected("Game surface");

            EventSystem.current.SetSelectedGameObject(null);
            yield return null;
            AssertSelected("Game surface");

            screen.TogglePause();
            yield return null;
            EventSystem.current.SetSelectedGameObject(null);
            yield return null;
            AssertSelected("Pause competition");
            screen.Hide();
        }
    }
}
