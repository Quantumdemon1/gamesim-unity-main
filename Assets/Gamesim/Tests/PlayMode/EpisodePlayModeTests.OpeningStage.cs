using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The opening's front door, staged for real in the house: the cast placed behind a facade in
    /// the west yard, walked through the door one at a time, and put back where the season starts
    /// them when the show is skipped. Every wait is bounded in real seconds.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private static readonly Vector3 RevealMarkForTests = new Vector3(-1.6f, 0f, 14.1f);
        private static readonly Vector3 DeckMarkForTests = new Vector3(-5.3f, 0f, 13.8f);
        // Where a houseguest waits for the door to give, in the vestibule behind the leaves (EpisodeDirector.OpeningStage's DoorMark).
        private static readonly Vector3 DoorMarkForTests = new Vector3(-3.85f, 0f, 13.8f);
        private static readonly Vector3[] DoorEyes = { new Vector3(2.1f, 1.85f, 13.8f), new Vector3(1.1f, 1.85f, 13.8f) };

        // The door shot and the push-in the eyes above belong to: level, looking west along the yard
        // at the front door from 4.1 m, then a metre closer as it opens (EpisodeDirector.OpeningStage).
        private static readonly Vector3 DoorShotFocusForTests = new Vector3(-2.0f, 1.85f, 13.8f);
        private const float DoorShotDistanceForTests = 4.1f;
        private const float PushInDistanceForTests = 3.1f;

        private static IEnumerator WaitFor(Func<bool> condition, float seconds, string what)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(condition(), Is.True, what);
        }

        private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        /// <summary>
        /// Waits for the frame a door reveal is built around - the camera landed on the door shot and
        /// the houseguest's name and face fully up under it, with the door still shut - and asserts
        /// that is what is on screen, so a capture taken next is a picture of the closed door.
        ///
        /// <para>The door beat cuts to its shot in a hundredth of a second, which is about twenty
        /// batchmode frames: a capture taken on the frame the houseguest was named photographed the
        /// whole-house overview the camera was leaving, and was committed as the closed-door frame.
        /// The lower third arrives from 1.5 s and has its face by 2.3 s; the door gives at 2.8 s for a
        /// house of eight or fewer, so the frame lasts half a second, which this waits into rather
        /// than guessing at. A door that opens first fails here instead of being photographed.</para>
        /// </summary>
        private IEnumerator WaitForTheClosedDoorFrame(OpeningSequence opening, OpeningDoorSet set, string guestId)
        {
            float until = Time.realtimeSinceStartup + 8f;
            while (!set.IsOpen && !ClosedDoorFrameIsUp(opening, guestId) && Time.realtimeSinceStartup < until) yield return null;

            Assert.That(set.IsOpen, Is.False, "The door is still shut once the camera has landed and the lower third is in: the closed-door frame exists.");
            Assert.That(opening.CurrentBeat, Is.EqualTo(OpeningBeat.Intro));
            Assert.That(opening.CurrentGuestId, Is.EqualTo(guestId), "It is this houseguest's reveal.");
            Assert.That(cameraRig.HasShot, Is.True, "The camera holds the door beat's shot, not the house's own framing.");
            Assert.That(cameraRig.IsTravelling, Is.False, "The camera is no longer travelling from the view it left.");
            Assert.That(cameraRig.HasArrived(0.1f), Is.True, "The camera has landed.");
            Assert.That(Vector3.Distance(cameraRig.DesiredFocus, DoorShotFocusForTests), Is.LessThan(0.1f), "It landed on the front door.");
            Assert.That(cameraRig.DesiredDistance, Is.EqualTo(DoorShotDistanceForTests).Within(0.1f), "from the door shot's distance, before the push-in.");
            Assert.That(Vector3.Angle(cameraRig.ViewCamera.transform.forward, Vector3.left), Is.LessThan(5f),
                "The lens looks level along the yard at the door, not down on the whole house.");
            Assert.That(cameraRig.LensOrthographic, Is.LessThan(0.01f), "and through the shot's perspective lens, not the overview's.");

            var scrim = SequenceNode(opening, "Scrim");
            Assert.That(scrim, Is.Not.Null);
            Assert.That(scrim.GetComponent<UnityEngine.UI.Image>().enabled, Is.False, "Nothing is drawn over the house: the door beat's ground is clear.");
            var third = SequenceNode(opening, "Lower third");
            Assert.That(third, Is.Not.Null, "The houseguest's lower third is up.");
            Assert.That(third.GetComponent<CanvasGroup>().alpha, Is.GreaterThanOrEqualTo(0.99f), "The lower third is fully in.");
            var portrait = third.Find("Portrait");
            Assert.That(portrait, Is.Not.Null, "The lower third carries a portrait.");
            Assert.That(portrait.GetComponent<CanvasGroup>().alpha, Is.GreaterThanOrEqualTo(0.99f), "Its face has faded in.");
            Assert.That(portrait.localScale.x, Is.GreaterThanOrEqualTo(0.99f), "and grown to its size.");
        }

        /// <summary>
        /// Records anybody standing where the leaves swing - within a body's width of their back face,
        /// in the doorway - while the door is less than half open. The leaves shut flush and rattle
        /// shut for 0.3 s before they move, and the walk through used to start 0.2 s after the door
        /// was told to open, so every houseguest walked through a closed leaf; a leaf half open has
        /// swung its edge clear of the middle of the doorway.
        ///
        /// <para>Where a body stands is where its hips are, not where its root is: the stage walks the
        /// root, but a take can carry the mesh away from it, and one did - the walk-stop take left a
        /// houseguest on the door mark with their hips 0.8 m ahead of their root, at x -3.04 with the
        /// root at -3.85, standing in the shut door while the root read 0.3 m clear of the leaves. So
        /// the leaves are watched against the Humanoid hips, and a body with no Humanoid rig is watched
        /// at its root. And while the door is shut, anybody on the door mark whose hips are more than
        /// 0.35 m from their root is recorded in <paramref name="offTheirFeet"/>: a body waiting at a
        /// door stands over its feet, and one that does not is a take moving the mesh where the stage
        /// cannot see it. Each body is recorded once for each, the first frame it is seen.</para>
        /// </summary>
        private IEnumerator WatchForWalkersInTheLeaves(List<string> inTheLeaves, List<string> offTheirFeet)
        {
            const float leafThickness = 0.08f, body = 0.25f, reach = 1.2f, overTheFeet = 0.35f, onTheMark = 0.6f;
            var hips = new Dictionary<Component, Transform>();
            var recorded = new HashSet<string>();
            while (true)
            {
                var set = Object.FindFirstObjectByType<OpeningDoorSet>();
                if (set != null)
                {
                    // Read the actual leaf meshes at every angle as well as the original closed-
                    // doorway guard. A fixed half-open cutoff missed bodies clipping a leaf later
                    // in its swing; this observation is independent of the runtime's route query.
                    var leaves = set.GetComponentsInChildren<MeshFilter>().Where(mesh => mesh.name == "Leaf").ToArray();
                    var bodies = SceneComponents<HouseNpc>().Where(npc => npc.gameObject.activeInHierarchy)
                        .Select(npc => (npc.DisplayName, (Component)npc)).ToList();
                    if (player != null) bodies.Add(("the player", player));
                    foreach (var (name, who) in bodies)
                    {
                        var root = who.transform.position;
                        var pelvis = HumanoidHips(who, hips);
                        var at = pelvis != null ? pelvis.position : root;
                        bool inClosedDoorway = set.Openness < 0.5f && at.z > OpeningDoorSet.ApertureMinZ && at.z < OpeningDoorSet.ApertureMaxZ
                            && at.x > OpeningDoorSet.FacadeFrontX - leafThickness - body && at.x < OpeningDoorSet.FacadeFrontX + reach;
                        bool touchesLeaf = leaves.Any(mesh => mesh.sharedMesh != null && Flat(at,
                            mesh.transform.TransformPoint(mesh.sharedMesh.bounds.ClosestPoint(mesh.transform.InverseTransformPoint(at)))) < body);
                        if ((inClosedDoorway || touchesLeaf) && recorded.Add("leaves " + name))
                            inTheLeaves.Add(name + " at x " + at.x.ToString("0.00") + (pelvis != null ? " (hips; root at x " + root.x.ToString("0.00") + ")" : " (root)")
                                            + " with the door " + set.Openness.ToString("0.00") + " open");
                        if (set.Openness < 0.01f && pelvis != null && Flat(root, DoorMarkForTests) < onTheMark
                            && Flat(at, root) > overTheFeet && recorded.Add("feet " + name))
                            offTheirFeet.Add(name + " on the door mark with their hips " + Flat(at, root).ToString("0.00") + " m from their root (hips at x "
                                             + at.x.ToString("0.00") + ", root at x " + root.x.ToString("0.00") + ") while the door is shut");
                    }
                }
                yield return null;
            }
        }

        /// <summary>
        /// A body's Humanoid hips, looked up once and kept while they live; null for a body with no
        /// active Humanoid animator, which is looked for again the next time it is asked about.
        /// </summary>
        private static Transform HumanoidHips(Component body, Dictionary<Component, Transform> known)
        {
            if (known.TryGetValue(body, out var hips) && hips != null) return hips;
            var animator = body.GetComponentsInChildren<Animator>().FirstOrDefault(rig => rig.isHuman && rig.isActiveAndEnabled);
            hips = animator != null ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
            known[body] = hips;
            return hips;
        }

        private bool ClosedDoorFrameIsUp(OpeningSequence opening, string guestId) =>
            opening.CurrentGuestId == guestId && cameraRig.HasShot && cameraRig.HasArrived(0.1f) && !cameraRig.IsTravelling
            && LowerThirdHasArrived(opening);

        /// <summary>Whether the newest lower third is in: the whole faded up, and its face grown to size and opaque.</summary>
        private static bool LowerThirdHasArrived(OpeningSequence opening)
        {
            var third = SequenceNode(opening, "Lower third");
            var group = third != null ? third.GetComponent<CanvasGroup>() : null;
            var portrait = third != null ? third.Find("Portrait") : null;
            var face = portrait != null ? portrait.GetComponent<CanvasGroup>() : null;
            return group != null && group.alpha >= 0.99f && face != null && face.alpha >= 0.99f && portrait.localScale.x >= 0.99f;
        }

        /// <summary>
        /// Whether the line from an eye at the door shot to a point behind the facade is stopped by
        /// the set: by the facade itself, or - through the doorway - by the vestibule, whose far
        /// wall of light closes it. The set has no colliders, so this is geometry on its published
        /// measurements rather than a raycast.
        /// </summary>
        private static bool HiddenBehindTheFacade(Vector3 eye, Vector3 target)
        {
            if (target.x >= OpeningDoorSet.FacadeFrontX) return false;
            float t = (OpeningDoorSet.FacadeFrontX - eye.x) / (target.x - eye.x);
            var hit = eye + (target - eye) * t;
            bool onFacade = hit.z >= OpeningDoorSet.FacadeMinZ && hit.z <= OpeningDoorSet.FacadeMaxZ && hit.y >= 0f && hit.y <= OpeningDoorSet.FacadeHeight;
            bool inDoorway = hit.z > OpeningDoorSet.ApertureMinZ && hit.z < OpeningDoorSet.ApertureMaxZ && hit.y >= 0f && hit.y < OpeningDoorSet.ApertureHeight;
            if (onFacade && !inDoorway) return true;
            if (!inDoorway) return false;
            // Through the doorway, anything short of the far wall of light stands in the vestibule,
            // in view once the door opens. Anything beyond it is closed off: a line through the
            // doorway ends on the far wall, or leaves by a side flat or the ceiling before it.
            return target.x < OpeningDoorSet.VestibuleFarX;
        }

        /// <summary>
        /// The premiere through the front door: the house is placed behind the facade out of
        /// sight, the player comes through first, the door opens and they walk to the mark in front
        /// of the lens, and the first houseguest after them does the same and then walks off out of
        /// shot to where the house gathers. The frames are photographed for review.
        /// </summary>
        [UnityTest]
        public IEnumerator OpeningStage_TheFrontDoorRevealWalksAHouseguestIn()
        {
            var opening = director.Opening;
            var state = director.Snapshot;
            string playerId = state.playerId;
            string firstGuest = state.Active.First(person => !person.isPlayer).id;

            director.PlayOpeningForVerification(stage: true, holdHeadless: true);
            Assert.That(opening.IsPlaying, Is.True, "The opening plays.");
            yield return WaitFor(() => director.IsOpeningStaged, 40f, "The house is placed behind the front door once every body is built.");
            Assert.That(GameObject.Find(OpeningDoorSet.RootName), Is.Not.Null, "The front door is standing in the yard.");

            var waiting = SceneComponents<HouseNpc>().Where(npc => npc.gameObject.activeInHierarchy).ToArray();
            Assert.That(waiting, Is.Not.Empty);
            foreach (var npc in waiting)
            {
                Assert.That(npc.transform.position.x, Is.LessThan(-5.5f), npc.DisplayName + " waits behind the facade.");
                foreach (var eye in DoorEyes)
                    foreach (float height in new[] { 0.1f, 1.7f })
                    {
                        var point = new Vector3(npc.transform.position.x, height, npc.transform.position.z);
                        Assert.That(HiddenBehindTheFacade(eye, point), Is.True,
                            npc.DisplayName + " cannot be seen waiting, from " + eye + " at " + height + " m.");
                    }
            }
            Assert.That(Flat(player.transform.position, DeckMarkForTests), Is.LessThan(0.6f), "The player waits on deck, first through the door.");

            // Every frame from here on: nobody stands where a leaf swings while it is still in the way,
            // and nobody waits at the shut door with their body somewhere other than over their feet.
            var throughClosedLeaves = new List<string>();
            var offTheirFeet = new List<string>();
            var watch = director.StartCoroutine(WatchForWalkersInTheLeaves(throughClosedLeaves, offTheirFeet));

            yield return WaitFor(() => opening.CurrentGuestId == playerId, 30f, "The player is revealed first.");
            var set = Object.FindFirstObjectByType<OpeningDoorSet>();
            Assert.That(set, Is.Not.Null);
            // Not on the frame the player is named: the camera is still leaving the overview then.
            yield return WaitForTheClosedDoorFrame(opening, set, playerId);
            if (Application.isBatchMode)
            {
                // The door is read while the photographed frame is current, from the capture's own
                // inspection, not after it returns: the door gives on its own clock half a second
                // after this frame arrives, nothing here holds it, and the capture's work - a render, a
                // readback, a PNG written and every pixel checked - is taken by that clock as one
                // frame, so a slow one could open the door after a shut door was photographed.
                bool shutWhenTaken = false;
                yield return CaptureFraming("opening-reveal-closed", settle: false, inspect: frame =>
                    shutWhenTaken = !set.IsOpen && set.Openness < 0.01f);
                Assert.That(shutWhenTaken, Is.True, "The door was still shut, its leaves unmoved, when the frame was taken.");
            }
            else Assert.That(set.IsOpen, Is.False, "The door is still shut on the closed-door frame.");
            yield return WaitFor(() => set.IsOpen, 12f, "The door opens for the player.");
            yield return WaitFor(() => set.Openness > 0.6f, 3f, "and swings wide.");
            // The hints over the door frames stand on grounds (UI-UX-PASS-PLAN S0): the Continue
            // hint on the skip pill's row, no longer inside the lit doorway, and the count on its chip.
            var hint = SequenceNode(opening, "Hint");
            var counterChip = SequenceNode(opening, "Counter chip");
            var pill = SequenceButtons(opening, OpeningSequence.SkipCaption).Single(button => button.IsActive()).transform;
            Assert.That(hint, Is.Not.Null, "The Continue hint is on the door frame.");
            Assert.That(counterChip, Is.Not.Null, "The reveal's count is on its chip.");
            AssertOnAGround(hint.Find("Label"), "The Continue hint over the open door", shown: true);
            AssertOnAGround(SequenceLabels(counterChip).Single(label => label.name == "Counter").transform, "The reveal's count over the open door", shown: true);
            if (Application.isBatchMode)
                yield return CaptureFraming("opening-reveal-open", settle: false, inspect: frame =>
                {
                    AssertGroundIsDrawn(frame, hint.Find(OpeningSequence.GroundName), pill, "The Continue hint's ground on the skip pill's row");
                    AssertGroundIsDrawn(frame, counterChip.Find(OpeningSequence.GroundName), pill, "The reveal's count's chip");
                });
            yield return WaitFor(() => Flat(player.transform.position, RevealMarkForTests) < 0.6f, 10f, "The player walks through the door to the mark.");
            if (Application.isBatchMode)
                yield return CaptureFraming("opening-reveal-mark", settle: false, inspect: frame =>
                    AssertGroundIsDrawn(frame, hint.Find(OpeningSequence.GroundName), pill, "The Continue hint's ground beside the player on the mark"));

            yield return WaitFor(() => opening.CurrentGuestId == firstGuest, 15f, "The first houseguest follows the player.");
            var guest = SceneComponents<HouseNpc>().Single(npc => npc.Id == firstGuest);
            yield return WaitFor(() => Flat(guest.transform.position, RevealMarkForTests) < 0.6f, 15f, guest.DisplayName + " walks through the door to the mark.");
            float settle = Time.realtimeSinceStartup + 0.6f;
            while (Time.realtimeSinceStartup < settle) yield return null;
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(guest.transform.eulerAngles.y, 90f)), Is.LessThan(35f), guest.DisplayName + " turns to the lens on the mark.");
            if (Application.isBatchMode) yield return CaptureFraming("opening-reveal-guest", settle: false);
            yield return WaitFor(() => player.transform.position.x > 3.0f, 20f, "The player walks off out of shot to where the house gathers, behind the camera.");
            director.StopCoroutine(watch);
            Assert.That(throughClosedLeaves, Is.Empty, "Nobody walks through a leaf that has not swung out of the way.");
            Assert.That(offTheirFeet, Is.Empty, "Everybody waiting at the shut door stands over their feet: no take carries the body ahead of where the stage put it.");

            opening.Skip();
            yield return null;
            yield return null;
            Assert.That(opening.IsPlaying, Is.False);
            Assert.That(GameObject.Find(OpeningDoorSet.RootName), Is.Null, "The front door comes down when the opening ends.");
            Assert.That(director.IsOpeningStaged, Is.False);
        }

        /// <summary>
        /// Skipping mid-reveal puts everybody back where the season starts them - behind black, by
        /// the load's own placement - and the introductions still play. The house is itself again
        /// once they are skipped too.
        /// </summary>
        [UnityTest]
        public IEnumerator OpeningStage_SkippingPutsEveryoneBackWhereTheSeasonStarts()
        {
            var opening = director.Opening;
            var home = SceneComponents<HouseNpc>().Where(npc => npc.gameObject.activeInHierarchy)
                .ToDictionary(npc => npc.Id, npc => npc.transform.position);
            var playerHome = player.transform.position;
            string firstGuest = director.Snapshot.Active.First(person => !person.isPlayer).id;

            director.PlayOpeningForVerification(stage: true, holdHeadless: true);
            yield return WaitFor(() => director.IsOpeningStaged, 40f, "The house is placed behind the front door.");
            yield return WaitFor(() => opening.CurrentGuestId == firstGuest, 40f, "The reveals reach the first houseguest.");

            director.SkipOpening();
            yield return WaitFor(() => opening.IsMeeting, 10f, "Skipping the show stops at the introductions.");
            foreach (var npc in SceneComponents<HouseNpc>().Where(npc => npc.gameObject.activeInHierarchy))
                Assert.That(Flat(npc.transform.position, home[npc.Id]), Is.LessThan(0.6f), npc.DisplayName + " is back where the season started them.");
            Assert.That(Flat(player.transform.position, playerHome), Is.LessThan(0.6f), "and so is the player.");
            Assert.That(GameObject.Find(OpeningDoorSet.RootName), Is.Null, "The front door is gone.");
            CollectionAssert.AreEqual(OpeningBeat.InOrder.Take(4), director.Snapshot.openingBeatsSeen, "Every beat of the show is recorded; the introductions are not.");

            opening.SkipIntroductions();
            yield return WaitFor(() => !opening.IsPlaying, 5f, "Skipping the introductions ends the opening.");
            yield return WaitFor(() => director.NpcAutonomyReady, 5f, "The house binds its people again and carries on.");
        }

        /// <summary>Whether a point of the frame, as <see cref="InTheFrame"/> gives it, is inside the picture.</summary>
        private static bool InsideTheFrame(Vector2? at) =>
            at.HasValue && at.Value.x > 0f && at.Value.x < 1f && at.Value.y > 0f && at.Value.y < 1f;

        /// <summary>
        /// A houseguest framed for their introduction stays in the frame while they answer
        /// (UI-UX-PASS-PLAN S0, sweep-show 24: the reaction's shot held on an empty corner and a
        /// lamp). Two halves. Nobody walks under the card: the stage holds everybody for the
        /// introductions, and the body framed is where it was framed into the reaction. And the shot
        /// holds on the body: carried 1.5 m to one side mid-reaction - as a take carries a mesh from
        /// its root, the drift the sweep saw - the shot's focus follows it within 0.6 s, the key
        /// light with it, and the body is back in the frame once the camera lands. Without the hold
        /// nothing moves the rig's focus once the card is framed (the introduction's framing is the
        /// only other shot under the opening), so the second half fails on the warp; the drift's
        /// own cause on a UMA body is read at the staged capture's step 11.
        /// </summary>
        [UnityTest]
        public IEnumerator OpeningStage_TheIntroductionHoldsTheHouseguestInFrame()
        {
            var opening = director.Opening;
            string firstGuest = director.Snapshot.Active.First(person => !person.isPlayer).id;

            director.PlayOpeningForVerification(stage: true, holdHeadless: true, armSeconds: 0f);
            yield return WaitFor(() => director.IsOpeningStaged, 40f, "The house is placed behind the front door.");
            yield return WaitFor(() => opening.CurrentGuestId != null, 40f, "The reveals begin.");
            director.SkipOpening();
            yield return WaitFor(() => opening.IsMeeting, 10f, "Skipping the show stops at the introductions.");
            yield return WaitFor(() => MeetCardIsSettled(opening, firstGuest), 8f, "The first houseguest's card is up and the camera has landed on them.");
            Assert.That(director.IsOpeningStaged, Is.True, "The stage still has the house through the introductions.");

            var body = SceneComponents<HouseNpc>().Single(npc => npc.Id == firstGuest);
            var framedAt = body.transform.position;
            Vector3 Drawn() { var hips = HipsOf(body); return hips != null ? hips.position : body.transform.position + Vector3.up * 0.95f; }
            string Where(Vector2? at) => at.HasValue ? at.Value.ToString("F2") : "behind the lens";
            Assert.That(InsideTheFrame(InTheFrame(cameraRig.ViewCamera, Drawn())), Is.True, "They are framed where they stand: " + Where(InTheFrame(cameraRig.ViewCamera, Drawn())));

            SequenceButtons(opening, "Calculated").Single().onClick.Invoke();
            yield return WaitFor(() => FadedIn(SequenceNode(opening, "Answer")), 3f, "The answer comes up where the question was.");
            float answered = Time.realtimeSinceStartup;
            // Into the reaction, their root watched the whole way.
            float drift = 0f;
            while (Time.realtimeSinceStartup < answered + 0.4f) { drift = Mathf.Max(drift, Flat(body.transform.position, framedAt)); yield return null; }
            var seen = InTheFrame(cameraRig.ViewCamera, Drawn());
            Assert.That(InsideTheFrame(seen), Is.True, "Into the reaction their body is in the frame: hips at " + Drawn().ToString("F2") + " -> " + Where(seen) + ".");
            Assert.That(drift, Is.LessThan(0.3f), "They stay where they were framed: no walk home starts under the card (moved " + drift.ToString("F2") + " m).");

            // Mid-reaction the body is carried 1.5 m to one side, to floor the agent can stand on.
            var agent = body.GetComponent<NavMeshAgent>();
            Assert.That(agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh, Is.True, body.DisplayName + " stands on the floor with their navigation bound.");
            var side = cameraRig.ViewCamera.transform.right;
            side.y = 0f;
            side.Normalize();
            var from = body.transform.position;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            // Either side takes the hips out of the part of the frame the card leaves clear: past its
            // right margin one way, behind the card the other. The nearest of 1.5 m that has floor.
            Vector3? to = null;
            foreach (float reach in new[] { 1.5f, 1.35f, 1.2f })
            {
                foreach (var direction in new[] { side, -side })
                    if (NavMesh.SamplePosition(from + direction * reach, out var hit, 0.3f, filter) && Flat(hit.position, from) > 1.15f) { to = hit.position; break; }
                if (to.HasValue) break;
            }
            Assert.That(to.HasValue, Is.True, "There is floor 1.2 to 1.5 m to one side of " + body.DisplayName + " at " + from.ToString("F2") + ".");
            var lamp = director.transform.Find("Introduction key light");
            Assert.That(lamp, Is.Not.Null, "The introduction's key light is up.");
            var focusBefore = cameraRig.DesiredFocus;
            var lampBefore = lamp.position;
            var drawnBefore = Drawn();
            Assert.That(agent.Warp(to.Value), Is.True, "The body is moved.");
            Physics.SyncTransforms();
            var moved = Drawn() - drawnBefore;
            Assert.That(moved.magnitude, Is.GreaterThan(1.1f), "Their hips moved with them: " + moved.ToString("F2") + ".");
            var followed = focusBefore + moved;
            float by = Time.realtimeSinceStartup + 0.6f;
            while (Time.realtimeSinceStartup < by && Vector3.Distance(cameraRig.DesiredFocus, followed) > 0.3f) yield return null;
            Assert.That(Vector3.Distance(cameraRig.DesiredFocus, followed), Is.LessThan(0.3f),
                "The shot follows the body within 0.6 s: aimed at " + cameraRig.DesiredFocus.ToString("F2") + " for hips moved " + moved.ToString("F2")
                + " from a focus of " + focusBefore.ToString("F2") + ".");
            Assert.That(Vector3.Distance(lamp.position, lampBefore + moved), Is.LessThan(0.3f),
                "The key light goes with the shot: at " + lamp.position.ToString("F2") + ", from " + lampBefore.ToString("F2") + ".");
            yield return WaitFor(() => !cameraRig.IsTravelling && cameraRig.HasArrived(0.1f), 1f, "The camera lands on them again.");
            seen = InTheFrame(cameraRig.ViewCamera, Drawn());
            Assert.That(InsideTheFrame(seen), Is.True, "Once the shot has followed, their body is in the frame: hips at " + Drawn().ToString("F2") + " -> " + Where(seen) + ".");
            Assert.That(opening.CurrentGuestId, Is.EqualTo(firstGuest), "It is still their card.");

            opening.SkipIntroductions();
            yield return WaitFor(() => !opening.IsPlaying, 5f, "Skipping the introductions ends the opening.");
            yield return WaitFor(() => director.NpcAutonomyReady, 5f, "The house binds its people again and carries on.");
        }
    }
}
