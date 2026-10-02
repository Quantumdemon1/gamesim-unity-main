using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.House;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
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
        /// Asking for the diary room cancels a walk to a houseguest.
        ///
        /// <para>The R key's own version of the contract above, and it has the same teeth:
        /// <c>GoToDiary</c> sets its own destination and its own status line, so a click that
        /// outlived it did not merely linger — it overwrote the walk to the diary room, or opened a
        /// conversation on the way there under a lower third promising the diary.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator NpcClick_AskingForTheDiaryRoomCancelsAWalkToAHouseguest()
        {
            director.ClosePanels();
            yield return null;
            Assert.That(director.HasDiaryRoom, Is.True,
                "Without a diary room GoToDiary returns before it cancels anything, and this would pass on nothing.");

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

            director.GoToDiary();
            yield return null;
            Assert.That(director.WalkingToId, Is.Null,
                "Asking for the diary room must let go of the walk to " + wanted.DisplayName
                + ". A pending click on a houseguest used to survive the R key and pull the player "
                + "straight back out of the diary trip.");

            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// Is this screen point bare walkable floor, by every question the controller asks on the
        /// way to its walk branch?
        ///
        /// <para>Each of those questions is a way for a synthetic click to be swallowed in silence,
        /// which is what makes a floor-click test so easy to write wrongly: the HUD claims the
        /// point, a seated body claims the ray, the ray reaches a prop whose interaction anchor
        /// shares a parent with the floor beneath it, or it lands on the player's own collider. In
        /// every one of those cases the click is consumed, nothing moves, and an assertion about
        /// what the click cancelled would be about nothing at all.</para>
        /// </summary>
        private bool IsBareFloorAt(Vector2 screen, out RaycastHit floor)
        {
            floor = default;
            var scene = player.gameObject.scene;
            if (EventSystem.current != null)
            {
                var over = new List<RaycastResult>();
                EventSystem.current.RaycastAll(
                    new PointerEventData(EventSystem.current) { position = screen }, over);
                if (over.Count > 0) return false;
            }

            var ray = cameraRig.ViewCamera.ScreenPointToRay(screen);
            if (HouseSeatPresentation.TryPickNpc(scene, ray, out _)) return false;
            if (!Physics.Raycast(ray, out floor, 500f, HouseLayers.Pick, QueryTriggerInteraction.Ignore)) return false;
            if (floor.collider.GetComponentInParent<HousePlayerController>() != null) return false;
            if (floor.collider.GetComponentInParent<HouseNpc>() != null) return false;
            if (HouseFurniture.AtProp(scene, floor.transform) != null) return false;
            return floor.collider.GetComponentInParent<HouseWalkable>() != null;
        }

        /// <summary>
        /// The clearest reachable patch of bare floor in the shot as it stands, with the longest
        /// walk to it.
        ///
        /// <para>Chosen with a margin rather than a single point, because the camera is following a
        /// running player: the world under a fixed screen point drifts between the frame this is
        /// picked on and the frame the button goes down. A candidate whose eight neighbours are all
        /// floor too survives that drift; one sitting on the edge of a rug does not.</para>
        ///
        /// <para>Reachability is checked here rather than asserted afterwards for the same reason
        /// the other checks are: an unreachable point makes <c>TryMoveTo</c> fail, and a floor click
        /// that sets no destination is a click the game deliberately ignores.</para>
        /// </summary>
        private Vector2 AimAtBareFloor(out Vector3 target)
        {
            Assert.That(Gamesim.Presentation.CeremonyOverlays.OnScreen, Is.False,
                "A ceremony card is on screen, and every click under one is dropped by design.");
            Assert.That(player.InputEnabled, Is.True, "The controller reads no clicks at all while input is off.");

            var rect = cameraRig.ViewCamera.pixelRect;
            var route = new NavMeshPath();
            const float Margin = 12f;
            Vector2 best = default;
            target = default;
            float furthest = -1f;

            for (int ix = 1; ix < 16; ix++)
            for (int iy = 1; iy < 16; iy++)
            {
                var screen = new Vector2(rect.xMin + rect.width * ix / 16f, rect.yMin + rect.height * iy / 16f);
                if (!IsBareFloorAt(screen, out var hit)) continue;

                bool clear = true;
                for (int dx = -1; dx <= 1 && clear; dx++)
                for (int dy = -1; dy <= 1 && clear; dy++)
                    if (dx != 0 || dy != 0)
                        clear = IsBareFloorAt(screen + new Vector2(dx * Margin, dy * Margin), out _);
                if (!clear) continue;

                if (!NavMesh.SamplePosition(hit.point, out var sample, .6f, player.Agent.areaMask)) continue;
                if (!player.Agent.CalculatePath(sample.position, route)
                    || route.status != NavMeshPathStatus.PathComplete) continue;

                float distance = Vector3.Distance(player.transform.position, hit.point);
                if (distance <= furthest) continue;
                furthest = distance; best = screen; target = hit.point;
            }

            Assert.That(furthest, Is.GreaterThan(1f),
                "No clear, reachable patch of bare floor anywhere in this shot, so there is no floor click to make.");
            return best;
        }

        /// <summary>
        /// Clicking the floor cancels a walk to a houseguest.
        ///
        /// <para>The plainest possible statement that the player wants to be somewhere else, and for
        /// its whole life it changed nothing: the controller declared a <c>DestinationChosen</c>
        /// event, the director subscribed <c>CancelTravel</c> to it, and no line anywhere ever
        /// raised it. The walk branch moved the player and returned. So you clicked a houseguest,
        /// changed your mind, clicked the floor, watched the player set off where you asked — and
        /// then the errand nobody had let go of re-aimed the path on the very next tick and dragged
        /// them back across the house.</para>
        ///
        /// <para>The wiring read as complete from either end, which is why nothing caught it: the
        /// subscription is there in <c>TickHouseActivities</c>, the handler is there, and the event
        /// is there. Only the raise was missing, and an event nobody raises is indistinguishable
        /// from one nobody listens to until something presses the button.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator NpcClick_ClickingTheFloorCancelsAWalkToAHouseguest()
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

                // Hold the shot still before measuring anything. Starting a walk hands the rig a new
                // subject - the player - and it eases off the houseguest it was framing onto them,
                // so a screen point measured on one frame names a different patch of the house two
                // frames later. That is what makes this click so easy to test wrongly: the press
                // lands on a wall, the controller returns without a sound, and the only thing left
                // to look at is an intent that was never cancelled. ClearSubject pins the desired
                // focus to where the camera already is, so the ray measured here is the ray pressed
                // on. The click under test re-acquires the player itself.
                cameraRig.ClearSubject();
                yield return null;

                var screen = AimAtBareFloor(out var target);
                Assert.That(Vector3.Distance(target, wanted.transform.position), Is.GreaterThan(3f),
                    "That patch of floor is inside talking range of " + wanted.DisplayName
                    + ", so the walk could end in a conversation on its own and cancel itself.");

                // Move first, and give the UI module a frame to see the pointer where it now is.
                // The controller asks EventSystem.IsPointerOverGameObject, which reads the module's
                // OWN tracked pointer rather than the position carried on the event, so a press that
                // arrives in the same frame as the move is judged against wherever the pointer was
                // standing before it - and the HUD-free point just measured counts for nothing.
                InputSystem.QueueStateEvent(mouse, new MouseState { position = screen });
                yield return null;
                yield return null;
                if (EventSystem.current != null)
                    Assert.That(EventSystem.current.IsPointerOverGameObject(), Is.False,
                        "The UI module still believes the pointer is over the HUD, so this click will be dropped.");

                // The walk has to still be running, or there is nothing left for the click to cancel.
                Assert.That(director.WalkingToId, Is.EqualTo(wanted.Id),
                    "The walk to " + wanted.DisplayName + " ended before the floor was ever clicked.");

                // And the point has to still be floor on the frame the button goes down. Measured
                // again rather than trusted, because every way this click can be swallowed is
                // silent, and a stale aim is the one that cost five attempts to find.
                Assert.That(IsBareFloorAt(screen, out var aimed), Is.True,
                    "The shot moved after the aim was taken: " + screen + " is no longer bare floor.");
                Assert.That(Vector3.Distance(aimed.point, target), Is.LessThan(.2f),
                    "The shot moved after the aim was taken: " + screen + " now names "
                    + aimed.point.ToString("0.0") + " rather than " + target.ToString("0.0") + ".");
                var before = player.Agent.destination;

                InputSystem.QueueStateEvent(mouse, new MouseState { position = screen }.WithButton(MouseButton.Left));
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = screen });
                yield return null;

                // That the click ARRIVED is asserted before what it cancelled. A click swallowed on
                // the way to the walk branch cancels nothing and proves nothing, and a test that
                // only looked at the intent would read that silence as a pass.
                Assert.That(director.TalkingToId, Is.Null,
                    "The floor click opened a conversation instead of moving anybody.");
                Assert.That(Vector3.Distance(player.Agent.destination, target), Is.LessThan(.75f),
                    "The floor click never reached the walk branch: the destination is still "
                    + before.ToString("0.0") + ", not the " + target.ToString("0.0") + " that was clicked.");

                Assert.That(director.WalkingToId, Is.Null,
                    "Clicking the floor must let go of the walk to " + wanted.DisplayName
                    + ". Nothing raised DestinationChosen, so the errand outlived the click, re-aimed "
                    + "the path on the next tick and dragged the player back.");
            }
            finally { InputSystem.RemoveDevice(mouse); }

            player.StopHere();
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// Photographs the conversation panel so what it says can be judged from a frame.
        ///
        /// <para>Everything this panel gained recently was verified by assertion and not by looking
        /// at it - the standing word, the signed trust, the remaining actions, the stakes tag on
        /// every deal row. An assertion that a string is present says nothing about whether it is
        /// legible, whether it fits, or whether it collides with the thing beside it, and the last
        /// time that shortcut was taken on this project it hid twelve portraits rendering as black
        /// squares.</para>
        ///
        /// <para>Both frames show the panel (UI-UX-PASS-PLAN Z0): the six-house close-up stands its
        /// lens against a prop, and with the HUD drawn in the scene a metre out the prop stood where
        /// the conversation's column was, so the frames said nothing about its copy (the play
        /// sweep's row 3). Its ground shows in every ninth of the column now.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator Conversation_CapturesThePanelForReview()
        {
            if (!Application.isBatchMode) yield break;
            director.ClosePanels();
            yield return null;

            var npc = SceneComponents<HouseNpc>().First(actor => actor.gameObject.activeInHierarchy);
            WarpPlayer(npc.transform.position
                + (player.transform.position - npc.transform.position).normalized * 1.4f);
            yield return null;
            Assert.That(director.TryOpenNpc(npc.Id), Is.True, "There is nothing to photograph if it never opened.");
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;

            // Report what the frame is expected to contain, so a reader of the capture knows what
            // they are looking for rather than guessing at it.
            var state = director.Snapshot;
            Debug.Log("[Gamesim] Conversation panel - " + npc.DisplayName
                + ", standing " + Gamesim.Presentation.RelationshipWeb.StandingWord(
                    Gamesim.Presentation.RelationshipWeb.KindOf(state, npc.Id))
                + " " + state.Score(state.playerId, npc.Id).ToString("+0;-0;0")
                + ", deal rows " + director.GetComponentsInChildren<TMPro.TMP_Text>(true)
                    .Count(label => label.gameObject.activeInHierarchy && label.text.Contains("stakes")));

            yield return CaptureFraming("conversation-panel",
                panel: (() => LastActive(Gamesim.Episode.EpisodeHud.ConversationColumnName), "The conversation's column"));
            AssertNothingInThePanelIsClipped("with the topics showing");

            // And again at the foot of the scroll, because the deal rows are down there. The first
            // frame showed a header that was verified and nine stakes tags that were not: they were
            // all below the fold, which is itself worth knowing - everything this panel gained is
            // behind a scroll the player has to find.
            var scroll = director.GetComponentsInChildren<UnityEngine.UI.ScrollRect>(true)
                .FirstOrDefault(rect => rect.gameObject.activeInHierarchy && rect.vertical);
            if (scroll != null)
            {
                scroll.verticalNormalizedPosition = 0f;
                Canvas.ForceUpdateCanvases();
                yield return null;
                yield return CaptureFraming("conversation-panel-deals",
                    panel: (() => LastActive(Gamesim.Episode.EpisodeHud.ConversationColumnName), "The conversation's column"));
                AssertNothingInThePanelIsClipped("with the deals showing");
            }

            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// Nothing on the conversation panel is cut off.
        ///
        /// <para>The global sweep that checks for clipped copy - <c>Accessibility_NoCopyIsClipped
        /// AtEitherTextSize</c> - opens the notebook and never reaches this panel, so every label on
        /// it has been unguarded for its whole life. That is how a stakes tag came to overlap itself
        /// and run off the edge truncated mid-word: the assertions all passed, because the string
        /// was present and correct, and only a captured frame showed it.</para>
        /// </summary>
        private void AssertNothingInThePanelIsClipped(string moment)
        {
            Canvas.ForceUpdateCanvases();
            var clipped = director.GetComponentsInChildren<TMPro.TMP_Text>(true)
                .Where(label => label.gameObject.activeInHierarchy && !string.IsNullOrEmpty(label.text))
                .Where(label => { label.ForceMeshUpdate(); return label.isTextOverflowing; })
                .Select(label => "'" + label.text + "'")
                .ToArray();
            Assert.That(clipped, Is.Empty,
                "On the conversation panel " + moment + ", this copy is cut off: "
                + string.Join(" | ", clipped));
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
