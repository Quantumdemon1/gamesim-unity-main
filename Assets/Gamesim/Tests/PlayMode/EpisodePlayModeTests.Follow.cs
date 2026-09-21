using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Camera Phase 2 (MASTER-PLAN §3.E): a portrait on the cast rail follows that houseguest, a
    /// ring rides under whoever is followed and a chip names them, and Tab cycles the house when
    /// no panel is open - and does nothing to the camera when one is, because inside a panel Tab
    /// belongs to the keyboard ring.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator Follow_ACastRailPortraitFollowsThatHouseguestWithARingAndAChip()
        {
            var maya = SceneComponents<HouseNpc>().Single(npc => npc.Id == ContentCatalog.MayaId);
            var rail = director.GetComponentsInChildren<RectTransform>().First(t => t.name == CastRail.RootName);
            var entry = rail.Cast<Transform>().Select(t => t.GetComponent<Button>()).First(b => b != null && b.name == maya.DisplayName);
            entry.onClick.Invoke();
            // Reference equality throughout: NUnit would otherwise compare two Transforms as the
            // collections of children they enumerate, and every body has the same children.
            Assert.That(cameraRig.FocusedSubject, Is.SameAs(maya.transform), "The camera follows the portrait's houseguest.");
            yield return null;
            var ring = GameObject.Find(FollowRing.RingName);
            Assert.That(ring, Is.Not.Null, "A ring marks who is followed.");
            Assert.That(Vector3.Distance(new Vector3(ring.transform.position.x, 0, ring.transform.position.z),
                new Vector3(maya.transform.position.x, 0, maya.transform.position.z)), Is.LessThan(0.05f), "under their feet");
            // The mockups mark the followed houseguest over the head as well as under the feet,
            // because a floor ring is a small ellipse behind the furniture at the overview camera's
            // height and a diamond at head height clears the sofa backs.
            var diamond = GameObject.Find(FollowRing.DiamondName);
            Assert.That(diamond, Is.Not.Null, "A diamond marks who is followed, over their head.");
            Assert.That(diamond.transform.position.y - maya.transform.position.y, Is.GreaterThan(1.8f),
                "and it floats clear of a houseguest's head rather than through it");
            Assert.That(Vector3.Distance(new Vector3(diamond.transform.position.x, 0, diamond.transform.position.z),
                new Vector3(maya.transform.position.x, 0, maya.transform.position.z)), Is.LessThan(0.05f),
                "and rides directly over them");

            var chip = director.GetComponentsInChildren<RectTransform>().FirstOrDefault(t => t.name == EpisodeHud.FollowChipName);
            Assert.That(chip, Is.Not.Null, "and a chip names them");
            Assert.That(chip.GetComponentInChildren<TMP_Text>().text, Does.Contain(maya.DisplayName.ToUpperInvariant()));

            entry.onClick.Invoke();
            yield return null;
            Assert.That(cameraRig.FocusedSubject, Is.Null, "The same portrait again lets go.");
            Assert.That(GameObject.Find(FollowRing.RingName), Is.Null, "and the ring goes with it");
            Assert.That(GameObject.Find(FollowRing.DiamondName), Is.Null, "and so does the diamond");
            Assert.That(director.GetComponentsInChildren<RectTransform>().Any(t => t.name == EpisodeHud.FollowChipName), Is.False, "and so does the chip");
        }

        /// <summary>
        /// Following somebody must not push the world's speech bubbles off the top of the screen.
        ///
        /// <para>The follow chip is anchored top-centre, under the house pill, and the safe-bounds
        /// calculation counted it among the BOTTOM cards - so it raised the floor to 834 while the
        /// ceiling was 796, and Rect.MinMaxRect handed back an inverted rect. Every ambient
        /// conversation bubble was then clamped to a line above the top bar, for as long as the
        /// player was following anyone. No test caught it because none of them followed anyone
        /// first, which is the whole lesson.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator Follow_DoesNotInvertTheBoundsTheWorldsSpeechBubblesLiveIn()
        {
            var hud = director.GetComponentInChildren<EpisodeHud>();
            Assert.That(hud, Is.Not.Null);

            var before = hud.WorldCaptionSafeBounds;
            Assert.That(before.height, Is.GreaterThan(0f), "with nobody followed");

            var maya = SceneComponents<HouseNpc>().Single(npc => npc.Id == ContentCatalog.MayaId);
            director.FollowHouseguest(maya.Id);
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();

            Assert.That(director.FollowedId, Is.EqualTo(maya.Id), "the fixture must actually be following.");
            var chip = director.GetComponentsInChildren<RectTransform>()
                .FirstOrDefault(rect => rect.name == EpisodeHud.FollowChipName);
            Assert.That(chip, Is.Not.Null, "and the chip must be on screen, or this proves nothing.");

            var during = hud.WorldCaptionSafeBounds;
            Assert.That(during.height, Is.GreaterThan(0f),
                "Following somebody inverted the caption bounds: " + during);
            Assert.That(during.yMax, Is.LessThanOrEqualTo(before.yMax + .001f),
                "the chip may lower the ceiling, never raise it");
            Assert.That(during.yMin, Is.EqualTo(before.yMin).Within(.001f),
                "and a top-anchored chip must not move the floor at all");
        }

        [UnityTest]
        public IEnumerator Follow_BracketsCycleTheHouseOutsideAPanelAndLeaveTheCameraAloneInsideOne()
        {
            if (testKeyboard == null) testKeyboard = InputSystem.AddDevice<Keyboard>();
            // Tab only reaches the camera while no HUD control is focused, and every HUD rebuild
            // re-selects one. Autonomy is one thing that rebuilds it, so the houseguests stop acting
            // on their own here - suspending keeps every body in the scene (it drops the meeting
            // world, not the housemates), so the cycle this test walks is the same cycle. It is not
            // the whole story on its own - see the wait below for the render that actually did it.
            director.SuspendNpcAutonomyForDiagnostics();
            director.ClosePanels();
            yield return null; yield return null;
            var active = director.Snapshot.contestants.Where(c => c.status == ContestantStatus.Active).Select(c => c.id).ToList();
            Assume.That(active.Count, Is.GreaterThan(2));

            yield return PressKey(Key.RightBracket);
            var first = cameraRig.FocusedSubject;
            Assert.That(first, Is.Not.Null, "] starts following the first active houseguest.");
            yield return PressKey(Key.RightBracket);
            var second = cameraRig.FocusedSubject;
            Assert.That(second, Is.Not.Null.And.Not.SameAs(first), "] again moves to the next.");
            yield return PressKey(Key.LeftBracket);
            Assert.That(cameraRig.FocusedSubject, Is.SameAs(first), "[ goes back.");

            // Tab is the same cycle for a mouse player who clicked the house and so has no control
            // focused, and the HUD keeps a control focused whenever it can.
            //
            // What made this flake, and it is not what it looks like. A full HUD render destroys every
            // control and re-selects one by name, and the director orders a render whenever another
            // houseguest's body finishes assembling - `CharacterPresentation.BodiesCompleted`, which
            // UMA advances on its own schedule. Land one of those inside the two frames of the key
            // press and the HUD holds the focus again, so Tab goes to the keyboard ring instead of
            // the house and the camera never moves. The assertion was right; the moment was not.
            //
            // So wait for the cast to actually finish, and ask each body rather than watch the clock:
            // a batchmode frame is well under a millisecond, so "quiet for a few frames" would mean
            // quiet for no time at all, and a second of waiting would only be a longer guess. A body
            // holding a stand-in has a render still coming; none holding one means none is coming.
            cameraRig.ClearSubject();
            float bodyDeadline = Time.realtimeSinceStartup + 30f;
            while (SceneComponents<CharacterPresentation>().Any(body => body.IsBodyAssembling)
                && Time.realtimeSinceStartup < bodyDeadline) yield return null;
            Assert.That(SceneComponents<CharacterPresentation>().Where(body => body.IsBodyAssembling).Select(body => body.name),
                Is.Empty, "The cast never finished assembling, so every frame here still has a render coming.");
            yield return null;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            yield return null;
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
            {
                yield return PressKey(Key.Tab);
                Assert.That(cameraRig.FocusedSubject, Is.Not.Null, "With nothing focused, Tab cycles the house too.");
                cameraRig.ClearSubject();
            }

            director.OpenSettings();
            yield return null;
            var before = cameraRig.FocusedSubject;
            yield return PressKey(Key.RightBracket);
            yield return PressKey(Key.Tab);
            Assert.That(ReferenceEquals(cameraRig.FocusedSubject, before), Is.True, "Inside a panel neither ] nor Tab touches the camera.");
            director.ClosePanels();
            cameraRig.ClearSubject();
        }
    }
}
