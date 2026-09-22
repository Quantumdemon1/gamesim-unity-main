using System.Collections;
using System.Linq;
using Gamesim.House;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Clicking a houseguest talks to them.
    ///
    /// <para>For the whole life of this build the one gesture every player tries on a person did
    /// nothing you could act on. It fell through the walk check first — a person is not a walkable
    /// surface — and then it moved the camera, which is better than nothing and is still not what
    /// clicking on somebody asks for. The only route into a conversation was to walk yourself
    /// inside 2.8 metres and press E, and nothing in the suite noticed because no test had ever
    /// pressed a mouse button on a body.</para>
    ///
    /// <para>These drive the real <see cref="HousePlayerController.Update"/> through the input
    /// system rather than calling the handler, because the handler is not the part that was
    /// broken — the routing was.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>Points the camera at a target and waits for the shot to settle on it.</summary>
        private IEnumerator FrameOn(Transform subject)
        {
            cameraRig.FocusSubject(subject);
            float deadline = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < deadline
                   && !(cameraRig.HasShot && cameraRig.HasArrived(.2f) && !cameraRig.IsTravelling))
                yield return null;
            yield return null;
        }

        /// <summary>Presses and releases the left button over a screen point, as a player would.</summary>
        private IEnumerator ClickAt(Mouse mouse, Vector2 screen)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = screen });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = screen }.WithButton(MouseButton.Left));
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = screen });
            yield return null;
        }

        /// <summary>
        /// The click lands on the body and nothing else: the HUD is not over it and the ray reaches
        /// that houseguest's own collider. Asserted rather than assumed, because a click that
        /// silently hit a wall would make every assertion below vacuous.
        /// </summary>
        private Vector2 AimAt(HouseNpc npc)
        {
            var camera = cameraRig.ViewCamera;
            var chest = npc.transform.position + Vector3.up * 1.0f;
            var screen = (Vector2)camera.WorldToScreenPoint(chest);
            Assert.That(camera.WorldToScreenPoint(chest).z, Is.GreaterThan(0f),
                npc.DisplayName + " is behind the camera, so this test would be clicking on nothing.");

            if (EventSystem.current != null)
            {
                var over = new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(
                    new PointerEventData(EventSystem.current) { position = screen }, over);
                Assert.That(over, Is.Empty, "The HUD is over " + npc.DisplayName + ", so the world would never see the click.");
            }

            Assert.That(Physics.Raycast(camera.ScreenPointToRay(screen), out var hit, 500f,
                HouseLayers.Pick, QueryTriggerInteraction.Ignore), Is.True, "The aim ray hits nothing at all.");
            Assert.That(hit.collider.GetComponentInParent<HouseNpc>(), Is.SameAs(npc),
                "The aim ray reaches " + (hit.collider.GetComponentInParent<HouseNpc>()?.DisplayName ?? hit.collider.name)
                + " rather than " + npc.DisplayName + ".");
            return screen;
        }

        [UnityTest]
        public IEnumerator NpcClick_ClickingAHouseguestWithinReachOpensTheConversation()
        {
            director.ClosePanels();
            yield return null;
            var npc = SceneComponents<HouseNpc>().First(actor => actor.gameObject.activeInHierarchy);

            // Frame on THEM, not on the player. Houseguests walk the house on their own, so a
            // camera pointed at where the player is standing loses the target while it settles -
            // which is how the first version of this test failed: it warped into range, waited six
            // seconds for the shot, and by then the houseguest had walked off. Following them keeps
            // them centred and in view however far they wander.
            yield return FrameOn(npc.transform);

            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                // Step beside them at the last possible moment, for the same reason.
                WarpPlayer(npc.transform.position
                    + (player.transform.position - npc.transform.position).normalized * 1.4f);
                yield return null;
                float gap = Vector3.Distance(player.transform.position, npc.transform.position);
                Assert.That(gap, Is.LessThan(2.8f),
                    npc.DisplayName + " is " + gap.ToString("0.0") + " m away, so this is not the in-reach case.");

                var screen = AimAt(npc);
                Assert.That(director.TalkingToId, Is.Null, "Nothing should be open before the click.");
                yield return ClickAt(mouse, screen);

                Assert.That(director.TalkingToId, Is.EqualTo(npc.Id),
                    "Clicking " + npc.DisplayName + " should open a conversation with them. It used to "
                    + "move the camera and return, so the only way in was to walk into range and press E.");
                Assert.That(director.IsPanelOpen, Is.True);
            }
            finally { InputSystem.RemoveDevice(mouse); }

            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// A click out of reach walks you there and opens on arrival — on the houseguest that was
        /// pointed at, not on whoever the walk happened to end up nearest.
        ///
        /// <para>That last clause is the whole reason the intent is remembered rather than
        /// recomputed. Two houseguests standing together are both inside the 2.8 m reach, so a walk
        /// that simply asked "who is nearest now" would open the wrong conversation about half the
        /// time — the same failure the episode screen already had, and for the same reason.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator NpcClick_AHouseguestOutOfReachIsWalkedToAndIsStillTheOneYouTalkTo()
        {
            director.ClosePanels();
            yield return null;

            var everyone = SceneComponents<HouseNpc>().Where(actor => actor.gameObject.activeInHierarchy).ToArray();
            Assert.That(everyone.Length, Is.GreaterThanOrEqualTo(2), "This needs a second houseguest to be wrong about.");

            // The furthest from the player is the one to click; the nearest is the distraction.
            var wanted = everyone.OrderByDescending(actor =>
                (actor.transform.position - player.transform.position).sqrMagnitude).First();
            yield return FrameOn(wanted.transform);

            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                var screen = AimAt(wanted);
                float before = Vector3.Distance(player.transform.position, wanted.transform.position);
                Assert.That(before, Is.GreaterThan(2.8f), "This case only exists beyond talking range.");

                yield return ClickAt(mouse, screen);
                Assert.That(director.TalkingToId, Is.Null,
                    "Out of reach, the click must not teleport a conversation into existence.");
                Assert.That(player.Agent.hasPath, Is.True,
                    "It should have set off towards " + wanted.DisplayName + " instead.");

                // The gait, pinned deterministically, because the chase itself is not: whether the
                // target happens to be walking is luck, and this is the property that decides
                // whether a moving one can ever be caught. A houseguest walks at 2.2 m/s and so
                // does the player, so a chase at walking pace closes nothing. The first version
                // chose its gait from the REMAINING route and so dropped back to a walk under eight
                // metres - exactly where the gap still had to close - and paced the target there
                // until it timed out. Every run of this test passed or failed on whether that
                // houseguest stood still.
                Assert.That(player.IsRunning, Is.True,
                    "The player must RUN to somebody out of reach. At walking pace the gap never closes.");

                // The walk has to survive them moving. Every houseguest in this house walks around
                // on its own, so a path aimed once at where somebody was standing arrives at an
                // empty patch of floor; the director re-aims as they drift.

                float deadline = Time.realtimeSinceStartup + 30f;
                while (Time.realtimeSinceStartup < deadline && director.TalkingToId == null) yield return null;

                // The failure message carries the state, because the interesting failure here is
                // not "it timed out" but WHY: a chase that never converged, an intent given up, or
                // a path that could not be built. The first version of this walked at the same
                // 2.2 m/s the houseguest does, so anybody walking away could never be caught.
                Assert.That(director.TalkingToId, Is.EqualTo(wanted.Id),
                    "The walk should end in a conversation with " + wanted.DisplayName + ". Instead: "
                    + (director.TalkingToId == null ? "no conversation" : "a conversation with somebody else")
                    + " after " + Vector3.Distance(player.transform.position, wanted.transform.position).ToString("0.0")
                    + " m, still walking to " + (director.WalkingToId ?? "nobody")
                    + ", path " + player.Agent.pathStatus
                    + ", panel open " + director.IsPanelOpen + ".");
            }
            finally { InputSystem.RemoveDevice(mouse); }

            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// Asking for the episode screen cancels a walk to a houseguest.
        ///
        /// <para>The same contract as the floor click and deliberately tested without any camera
        /// geometry, because the floor-click version can only run when the frame happens to contain
        /// a clear patch of floor. This one always runs. It is also the case with teeth: GoToStation
        /// sets its own destination and its own status line, so a click that outlived it did not
        /// merely linger - it overwrote the path to the screen, or opened a conversation on the way
        /// there while the lower third promised the episode screen.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator NpcClick_AskingForTheEpisodeScreenCancelsAWalkToAHouseguest()
        {
            director.ClosePanels();
            yield return null;

            var wanted = SceneComponents<HouseNpc>()
                .Where(actor => actor.gameObject.activeInHierarchy)
                .OrderByDescending(actor => (actor.transform.position - player.transform.position).sqrMagnitude)
                .First();
            yield return FrameOn(wanted.transform);

            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                yield return ClickAt(mouse, AimAt(wanted));
                Assert.That(director.WalkingToId, Is.EqualTo(wanted.Id),
                    "The click should have started a walk to " + wanted.DisplayName + ".");
            }
            finally { InputSystem.RemoveDevice(mouse); }

            director.GoToStation();
            yield return null;
            Assert.That(director.WalkingToId, Is.Null,
                "Asking for the episode screen must let go of the walk to " + wanted.DisplayName
                + ". It used to survive, overwrite the path to the screen, and open a conversation "
                + "on the way under a status line promising the screen.");

            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// The conversation header says where you stand and what you have left to spend.
        ///
        /// <para>Neither was reachable from this panel. The five-band standing vocabulary has one
        /// call site in the whole game and it is a stat tile on a notebook page; the remaining-actions
        /// chip lives in the objective card, which the conversation layout hides — so it was on
        /// screen right up until the moment it mattered.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator NpcClick_TheConversationHeaderStatesStandingAndWhatIsLeftToSpend()
        {
            director.ClosePanels();
            yield return null;
            var npc = SceneComponents<HouseNpc>().First(actor => actor.gameObject.activeInHierarchy);
            WarpPlayer(npc.transform.position
                + (player.transform.position - npc.transform.position).normalized * 1.4f);
            yield return null;
            Assert.That(director.TryOpenNpc(npc.Id), Is.True);
            yield return null;
            Canvas.ForceUpdateCanvases();

            string copy = string.Join("\n", director.GetComponentsInChildren<TMPro.TMP_Text>(true)
                .Where(label => label.gameObject.activeInHierarchy)
                .Select(label => label.text));

            var state = director.Snapshot;
            string standing = Gamesim.Presentation.RelationshipWeb.StandingWord(
                Gamesim.Presentation.RelationshipWeb.KindOf(state, npc.Id));
            Assert.That(copy, Does.Contain(standing),
                "The panel that decides how to treat somebody never said how they were being treated.");
            Assert.That(copy, Does.Match(@"\d+ actions? left"),
                "and it never said how many actions were left to spend on them.");

            director.ClosePanels();
            yield return null;
        }
    }
}
