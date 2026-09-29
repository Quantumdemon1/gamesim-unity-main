using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator CompetitionResultOpenedByDirectCommandOwnsWorldInputAndRestoresTheHouse()
        {
            Assert.That(director.IsPanelOpen, Is.False);
            Assert.That(director.Submit(NextCommand(director.Snapshot)).accepted, Is.True);
            var before = director.Snapshot;
            Assert.That(director.Submit(NextCommand(before)).accepted, Is.True);
            Assert.That(SceneComponents<CompetitionResult>().Single().IsPlaying, Is.True);
            Assert.That(player.InputEnabled, Is.False, "Result ownership must not depend on entering through a phase panel.");
            Assert.That(cameraRig.ControlsEnabled, Is.False);
            yield return ContinueCompetitionResults(byKeyboard: false);
            Assert.That(director.IsPanelOpen, Is.False);
            Assert.That(player.InputEnabled, Is.True);
            Assert.That(cameraRig.ControlsEnabled, Is.True);
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision + 1));
        }

        [UnityTest]
        public IEnumerator CompetitionMenuKeysAffectOnlyTheAttemptAndPreserveTheBriefing()
        {
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            ButtonWithCaption("Begin the next competition").onClick.Invoke();
            yield return null;
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            var before = director.Snapshot;
            ButtonWithCaption("Practice this competition").onClick.Invoke();
            yield return null;
            var screen = SceneComponents<CompetitionGameScreen>().Single();
            Assert.That(screen.IsShowing, Is.True);
            yield return PressKey(Key.Escape);
            Assert.That(screen.IsShowing, Is.False);
            Assert.That(director.IsPanelOpen, Is.True, "Escape returns to briefing without closing the phase panel underneath.");
            Assert.That(player.InputEnabled, Is.False);
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision));

            ButtonWithCaption("Practice this competition").onClick.Invoke();
            yield return null;
            var pad = TestGamepad();
            yield return PressPad(pad, GamepadButton.Start);
            Assert.That(screen.Paused, Is.True, "Start pauses the attempt instead of cancelling it.");
            Assert.That(screen.IsShowing, Is.True);
            yield return PressPad(pad, GamepadButton.East);
            Assert.That(screen.IsShowing, Is.False);
            Assert.That(director.IsPanelOpen, Is.True);
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision));
            Assert.That(director.Snapshot.randomState, Is.EqualTo(before.randomState));
        }

        /// <summary>
        /// A competition being played, the frame to judge against mockup-05: the game's cards over
        /// the yard, with the house's chrome standing aside for it. A practice attempt, so nothing
        /// it does is committed.
        /// </summary>
        [UnityTest]
        public IEnumerator Competition_CapturesThePracticeForReview()
        {
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            ButtonWithCaption("Begin the next competition").onClick.Invoke();
            yield return null;
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            var before = director.Snapshot;
            ButtonWithCaption("Practice this competition").onClick.Invoke();
            yield return null;
            var screen = SceneComponents<CompetitionGameScreen>().Single();
            Assert.That(screen.IsShowing, Is.True);
            if (Application.isBatchMode) yield return CaptureFraming("competition-assembly");
            // Past the walk to the stations and the count-in, into the game itself.
            if (screen.IsAssembling)
                screen.GetComponentsInChildren<UnityEngine.UI.Button>().First(button => button.name == "Continue to competition").onClick.Invoke();
            float playing = Time.realtimeSinceStartup + 3.6f;
            while (Time.realtimeSinceStartup < playing && screen.IsShowing) yield return null;
            Assert.That(screen.IsShowing, Is.True, "The practice should still be in play.");

            // While it is played the competition is the whole screen: the house's HUD stands down,
            // and the game's own cards say who is in the field and how the player is doing.
            yield return null;
            Assert.That(ActiveRect(IconRail.RootName), Is.Null, "The house's chrome stands down while the game is played.");
            Assert.That(ActiveRect(CastRail.RootName), Is.Null);
            var field = Gamesim.Simulation.EpisodeEngine.CompetitionPlayers(before).ToArray();
            var texts = screen.GetComponentsInChildren<TMPro.TMP_Text>();
            string listed = texts.Single(text => text.name == "Competition field").text;
            foreach (var actor in field)
                Assert.That(listed, Does.Contain(HudPrimitives.WithYou(actor.name, actor.id == before.playerId)),
                    "The field card lists everyone competing: '" + listed + "'.");
            string progress = texts.Single(text => text.name == "Progress").text;
            Assert.That(progress, Does.Contain("pairs").Or.Contain("hits").Or.Contain("Effort"),
                "The player's progress is the game's own measure: '" + progress + "'.");
            // And the board is most of what is on screen.
            var board = screen.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Game surface");
            var area = ScreenRect(board);
            Assert.That(area.width * area.height, Is.GreaterThan(Screen.width * Screen.height * .45f),
                "The board takes most of the frame: " + area + ".");
            if (Application.isBatchMode && screen.IsShowing) yield return CaptureFraming("competition-practice");
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision), "A practice commits nothing.");
        }

        /// <summary>
        /// The arena's sign of the award and the discipline reads from the deck (MOCKUP-PASS-PLAN M2).
        /// It was turned half round, so it read mirrored from the competition camera and every seat,
        /// and it hung across the centre gate, whose neon ran through the words. A practice attempt
        /// stages the arena and commits nothing; batch runs frame the sign for the look sheet.
        /// </summary>
        [UnityTest]
        public IEnumerator Competition_TheYardSignReadsFromTheDeckOverTheCentreGate()
        {
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            ButtonWithCaption("Begin the next competition").onClick.Invoke();
            yield return null;
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            var before = director.Snapshot;
            ButtonWithCaption("Practice this competition").onClick.Invoke();
            yield return null; yield return null;

            var sign = SceneComponents<TextMeshPro>()
                .SingleOrDefault(text => text.name == EpisodeDirector.CompetitionSignName && text.gameObject.activeInHierarchy);
            Assert.That(sign, Is.Not.Null, "The practice stages the arena, and the arena its sign.");
            Assert.That(sign.text, Is.Not.Empty, "The sign names the award and the discipline.");
            // A TextMeshPro face reads from behind its forward: toward the deck, the sign's forward
            // points away from it, along +z, the way the backdrop's lit face looks and the
            // competition camera looks.
            Assert.That(Vector3.Dot(sign.transform.forward, Vector3.forward), Is.GreaterThan(0.99f),
                "The sign faces the deck, not the backdrop behind it: forward " + sign.transform.forward + ".");
            Assert.That(Vector3.Dot(sign.transform.position - player.transform.position, sign.transform.forward), Is.GreaterThan(0f),
                "The player, on their way to the station, is on the side it reads from.");

            var gates = SceneComponents<Transform>()
                .Where(part => part.name.StartsWith(EpisodeDirector.CompetitionGateName, System.StringComparison.Ordinal)
                    && part.GetComponentsInChildren<Renderer>().Length > 0)
                .ToList();
            Assert.That(gates, Is.Not.Empty, "The yard is dressed with the course's gates.");
            var centre = gates.OrderBy(gate => Mathf.Abs(gate.position.x - sign.transform.position.x)).First();
            float gateTop = centre.GetComponentsInChildren<Renderer>().Max(renderer => renderer.bounds.max.y);
            sign.ForceMeshUpdate();
            float foot = sign.transform.TransformPoint(new Vector3(0f, sign.textBounds.min.y, 0f)).y;
            Assert.That(foot, Is.GreaterThan(gateTop),
                "The words stand clear of the centre gate's head: their foot at " + foot.ToString("0.00")
                + ", the gate's top at " + gateTop.ToString("0.00") + ".");

            if (Application.isBatchMode)
            {
                // The game's screen stands over the yard while the house walks to its stations; the
                // sign is this frame's subject, so the screen steps out of it and comes back after.
                var game = SceneComponents<CompetitionGameScreen>().SingleOrDefault();
                var surface = game != null ? game.GetComponent<Canvas>() : null;
                if (surface != null) surface.enabled = false;
                yield return CaptureSet("competition-yard-sign", sign.transform.position + Vector3.down * 1.4f, 10f, 6f, 0f);
                if (surface != null) surface.enabled = true;
            }
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision), "A practice commits nothing.");
        }
    }
}
