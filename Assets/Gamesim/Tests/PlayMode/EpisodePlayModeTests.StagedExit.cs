using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The eviction's exit, staged (PACK8-PASS-PLAN C2, MOCKUP-PASS-PLAN M19): the result taken in
    /// the chairs, the evicted standing to say goodbye to the house, the house watching them walk
    /// out slowly, the door opened without its flare, shut behind them and held on before their
    /// body goes, and the chrome aside the whole way. A press goes straight to the shut door, and
    /// nothing the commit decided changes.
    ///
    /// <para>Every wait is bounded in real seconds, as the stage's own tests are; a span the code
    /// measures - the hold on the shut door - is measured on its own unscaled clock.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private static HouseNpc HouseguestBody(string id) => SceneComponents<HouseNpc>().Single(npc => npc.Id == id);

        private static CharacterPresentation Presentation(Component body) => body != null ? body.GetComponent<CharacterPresentation>() : null;

        /// <summary>Read-only endpoint observations of the peace's embrace, not a reconstructed inside-call refusal.</summary>
        private string HugEndpoint(string firstId, string secondId)
        {
            var bodies = SceneComponents<HouseNpc>().Where(body => body.Id == firstId || body.Id == secondId).ToArray();
            var first = bodies.FirstOrDefault(body => body.Id == firstId)?.GetComponent<CharacterPresentation>();
            var second = bodies.FirstOrDefault(body => body.Id == secondId)?.GetComponent<CharacterPresentation>();
            string Side(string id, CharacterPresentation body)
            {
                if (body == null) return id + ": missing presentation";
                var seat = body.GetComponent<HouseSeatPresentation>();
                var motion = body.GetComponent<HouseNpcMotion>();
                var animator = body.VisualRoot != null ? body.VisualRoot.GetComponentInChildren<Animator>() : null;
                return id + ": primitive=" + body.IsPrimitiveBody + ", supports=" + body.Supports(CharacterPresentation.Reaction.Embrace)
                    + ", assembling=" + body.IsBodyAssembling + ", dressing=" + body.IsChangingOutfit
                    + ", seated=" + body.IsSeated + ", seatActive=" + (seat != null && seat.Active)
                    + ", seatExiting=" + (seat != null && seat.IsExiting) + ", activity=" + body.Activity
                    + ", still=" + body.IsStill + ", reacting=" + body.IsReacting + ", last=" + body.LastReaction
                    + ", root=" + body.transform.position.ToString("F3")
                    + ", visual=" + (body.VisualRoot != null ? body.VisualRoot.position.ToString("F3") : "missing")
                    + ", animator=" + (animator != null ? animator.isHuman + "/" + animator.isActiveAndEnabled : "missing")
                    + ", motion=" + (motion != null ? motion.State + ", lease=" + motion.LeaseId + ", parked=" + motion.IsParked
                        + ", route=" + motion.RouteLength.ToString("F3") + ", destination=" + motion.ReservedDestination.ToString("F3")
                        + ", arrival=" + motion.ArrivalFailure + ", routeFailure=" + motion.LastRouteFailure : "none");
            }
            var worldField = typeof(EpisodeDirector).GetField("npcMeetings",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var meetings = worldField?.GetValue(director) as HouseMeetingCoordinator;
            var pair = director.CurrentEmbrace;
            return "Endpoint only at frame " + Time.frameCount + ", unscaled=" + Time.unscaledTime.ToString("F3")
                + ", delta=" + Time.deltaTime.ToString("F3") + ", timeScale=" + Time.timeScale
                + ", phase=" + director.CeremonyStagePhase + ", card=" + director.IsSceneCardOpen
                + ", panel=" + director.IsPanelOpen + ", pendingFirst=" + director.PendingEmbraceFirst
                + ", world=" + (meetings != null ? "scene=" + meetings.SceneStageKey + ", held=" + meetings.SceneHeldForEmbrace : "none")
                + ", worldDiagnostic=" + director.NpcAutonomyDiagnostic
                + "\n" + Side(firstId, first) + "\n" + Side(secondId, second)
                + "\nflatRootGap=" + (first != null && second != null ? Flat(first.transform.position, second.transform.position).ToString("F3") : "unknown")
                + ", currentRefusal=" + EmbracePair.Refusal(first, second, director.Snapshot.playerId)
                + ", pair=" + (pair != null ? pair.Stage + ", fired=" + pair.Fired + ", released=" + pair.Released
                    + ", reason=" + pair.ReleaseReason : "none");
        }

        private IEnumerator WaitForHugEndpoint(System.Func<bool> condition, float seconds,
            string what, string firstId, string secondId)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < until) yield return null;
            bool reached = condition();
            Assert.That(reached, Is.True, what + (reached ? "" : "\n" + HugEndpoint(firstId, secondId)));
        }

        /// <summary>
        /// The goodbye is noncontact (the owner's decision): no embrace in the scene and neither of
        /// the two playing one. Null while that holds; otherwise the first time it did not, said.
        /// </summary>
        private static string GoodbyeEmbraceSeen(CharacterPresentation leaving, CharacterPresentation staying, string when)
        {
            var pairs = Object.FindObjectsByType<EmbracePair>(FindObjectsSortMode.None);
            if (pairs.Length > 0)
                return when + ": an embrace is in the scene (" + string.Join(", ", pairs.Select(pair => pair.name)) + ") at frame " + Time.frameCount;
            foreach (var body in new[] { leaving, staying })
                if (body != null && body.LastReaction == CharacterPresentation.Reaction.Embrace)
                    return when + ": " + body.CharacterId + "'s last beat is an embrace, at frame " + Time.frameCount;
            return null;
        }

        /// <summary>
        /// The result is taken in the chairs, and the goodbye comes between the card and the walk:
        /// the evicted stand and face the house, every seated head turns to them, whoever sits
        /// claps, the camera is on their face, their line is on the strip, the chrome is aside and
        /// the warm key light has left the red chairs for their face, from the camera's side - and
        /// only once it is over do they walk, slowly.
        /// </summary>
        [UnityTest]
        public IEnumerator StagedExit_TheGoodbyeComesBetweenTheCardAndTheWalk()
        {
            yield return InstallStagedSeason(52, AtEviction);
            yield return PlayUntilTheCeremony();
            Assert.That(director.IsCeremonyStaged, Is.True, "The eviction is staged in the living room.");
            Assert.That(director.CeremonyStageKind, Is.EqualTo(CeremonySting.EvictionKind));
            var committed = director.Snapshot;
            var farewellGraph = UvPayload(committed);
            string evicted = director.DepartingId;
            Assert.That(evicted, Is.Not.Null, "A houseguest is evicted.");
            string survivor = committed.nominees.Single(id => id != evicted);
            // Not skipped: the card waits for the nominees in red, and the result is taken there.
            yield return WaitFor(() => director.CeremonyStagePhase == EpisodeDirector.CeremonyStageStep.Playing,
                EpisodeDirector.SummonsHardSeconds(CeremonyPace.Suspenseful) + 6f, "the vote plays once the nominees have taken the hot seats");
            Assert.That(director.EvictionKeyLightShowing, Is.True, "A warm key light is on the red chairs from the card's start.");
            bool Seated(string id)
            {
                var seat = HouseguestBody(id).GetComponent<HouseSeatPresentation>();
                return seat != null && seat.Active;
            }
            yield return WaitFor(() => Seated(evicted) && Seated(survivor), 8f, "both nominees sit in red");

            // The result read, and its cue played: the room takes it where it sits.
            var vote = SceneComponents<VoteReveal>().Single();
            var leaving = HouseguestBody(evicted);
            var staying = HouseguestBody(survivor);
            float readAt = Time.unscaledTime;
            float cueDelay = Mathf.Min(1.5f, vote.ResultHoldSeconds * 0.4f) / Mathf.Max(1f, vote.SpeedMultiplier);
            float observeBy = Time.realtimeSinceStartup + vote.ResultHoldSeconds;
            bool wasHeld = vote.Held;
            // A wall-clock sleep could finish before the director's next unscaled cue tick, or
            // after the result had already closed on one slow frame. Hold the card's reading
            // frame while observing the actual reaction; the stage's cues keep their own clock.
            // Restore its clock before testing the card-down -> goodbye -> walk handover below.
            vote.Held = true;
            try
            {
                vote.SkipToResult();
                bool TookTheResult()
                {
                    var look = Presentation(leaving).LookTarget;
                    return look != null && look.name.StartsWith("Ceremony look mark");
                }
                while (!TookTheResult() && vote.IsPlaying
                    && director.CeremonyStagePhase == EpisodeDirector.CeremonyStageStep.Playing
                    && Time.realtimeSinceStartup < observeBy) yield return null;
            }
            finally { vote.Held = wasHeld; }
            Assert.That(vote.IsPlaying && vote.ShowingResult, Is.True, "The result is still up.");
            var leavingLook = Presentation(leaving).LookTarget;
            Assert.That(leavingLook != null && leavingLook.name.StartsWith("Ceremony look mark"), Is.True,
                "The evicted take the result head down, in the chair: looking at " + (leavingLook != null ? leavingLook.name : "nothing")
                + "; stage clock advanced " + (Time.unscaledTime - readAt).ToString("F2") + " s, cue due after " + cueDelay.ToString("F2")
                + " s. " + director.CeremonyStageReport("seated result reaction"));
            Assert.That(Time.unscaledTime - readAt, Is.GreaterThanOrEqualTo(cueDelay - 0.01f),
                "The seated reaction follows the result's reading beat, rather than firing before its cue.");
            var survivorSeat = staying.GetComponent<HouseSeatPresentation>();
            Assert.That(survivorSeat != null && survivorSeat.Active, Is.True, "The survivor stays seated,");
            Assert.That(Presentation(staying).LastReaction, Is.Not.EqualTo(CharacterPresentation.Reaction.Won),
                "with no fist pump in front of the one going.");
            // Nobody looks down for a ballot they cast: the result never says who voted to keep the
            // evicted (UI-UX-PASS-PLAN B0). Every other houseguest turns to the one going, whatever
            // their ballot was.
            foreach (var npc in SceneComponents<HouseNpc>().Where(each => each.gameObject.activeInHierarchy && each.Id != evicted && each.Id != survivor))
            {
                var look = Presentation(npc).LookTarget;
                Assert.That(look, Is.SameAs(leaving.transform),
                    npc.Id + " turns to the one going, whatever they voted: looking at " + (look != null ? look.name : "nothing") + ".");
            }

            // The card down: the goodbye.
            float closedAt = Time.unscaledTime;
            vote.Cancel();
            yield return null;
            Assert.That(director.CeremonyStagePhase, Is.EqualTo(EpisodeDirector.CeremonyStageStep.Goodbye), "The evicted stand to say goodbye,");
            Assert.That(director.WalkingOutId, Is.Null, "and nobody walks yet.");
            Assert.That(Hud.IsHeldForReveal, Is.True, "The chrome stays aside,");
            Assert.That(PlatesUp(), Is.Empty, "the plates stay down,");
            Assert.That(director.CeremonySkipShowing, Is.True, "the chip names the press that moves it on,");
            Assert.That(director.EvictionKeyLightShowing, Is.True, "and the key light is still on.");
            Assert.That(Presentation(leaving).IsSeated, Is.False, "They are up out of the chair.");
            // The key light leaves the red chairs for their face: it began on or before this frame and
            // swings over 0.3 s. From then on, on every frame of the goodbye read here, it is aimed at
            // their head from a little above the eyes, near enough to light the face, in the chairs' warm
            // color and no brighter - and, on a frame whose camera has landed, from the camera's side.
            float keyOnTheFaceBy = Time.unscaledTime + 0.35f;
            int keyReadsFromTheLens = 0;
            string KeyOffTheFace(bool fromTheLens)
            {
                var key = director.transform.Find(EpisodeDirector.EvictionKeyLightName);
                var lamp = key != null ? key.GetComponent<Light>() : null;
                if (lamp == null || !lamp.enabled) return "no key light is on";
                var bone = Presentation(leaving).HeadBone;
                var head = bone != null ? bone.position : leaving.transform.position + Vector3.up * 1.55f;
                var lens = cameraRig.ViewCamera.transform.position;
                var toKey = key.position - head;
                var off = new List<string>();
                if (fromTheLens && Vector3.Dot(toKey.normalized, (lens - head).normalized) <= 0.5f) off.Add("not on the camera's side of the face");
                if (Vector3.Angle(key.forward, head - key.position) >= 8f) off.Add("not aimed at the head");
                if (toKey.magnitude < 1.6f || toKey.magnitude > 2.6f) off.Add("not 1.6 to 2.6 m from the head");
                if (toKey.y < 0.1f || toKey.y > 1f) off.Add("not a little above the eyes");
                if (lamp.intensity > 6f) off.Add("brighter than on the chairs");
                if (Mathf.Abs(lamp.color.r - 1f) > 0.01f || Mathf.Abs(lamp.color.g - 0.86f) > 0.01f || Mathf.Abs(lamp.color.b - 0.66f) > 0.01f)
                    off.Add("not the chairs' warm color");
                return off.Count == 0 ? null : string.Join(", ", off) + " (key " + key.position.ToString("F2") + ", head " + head.ToString("F2")
                    + ", lens " + lens.ToString("F2") + ", intensity " + lamp.intensity.ToString("F2") + ")";
            }
            void WatchTheKey(string when)
            {
                if (Time.unscaledTime < keyOnTheFaceBy || director.CeremonyStagePhase != EpisodeDirector.CeremonyStageStep.Goodbye) return;
                bool landed = cameraRig.HasArrived();
                Assert.That(KeyOffTheFace(landed), Is.Null, "The key light is on the evicted's face " + when + ", not on the empty chair:");
                if (landed) keyReadsFromTheLens++;
            }
            // The goodbye is noncontact (the owner's decision): watched on every frame from the card
            // going down to the house getting up - the farewell's own frames among them - not only at the end.
            string embraceSeen = null;
            void WatchTheGoodbye(string when)
            {
                if (embraceSeen == null) embraceSeen = GoodbyeEmbraceSeen(Presentation(leaving), Presentation(staying), when);
            }
            WatchTheGoodbye("as the goodbye began");
            // Owner-selected NONCONTACT shorter greeting, not the old full hug; the player is never borrowed.
            bool embraces = survivor != director.Snapshot.playerId && evicted != director.Snapshot.playerId;
            foreach (var npc in SceneComponents<HouseNpc>().Where(each => each.gameObject.activeInHierarchy && each.Id != evicted
                && !(embraces && each.Id == survivor)))
            {
                var visual = Presentation(npc);
                if (!visual.IsSeated) continue;
                Assert.That(visual.LookTarget, Is.SameAs(leaving.transform), npc.Id + " looks at them from the seat,");
                if (visual.SupportsSeated(CharacterPresentation.Reaction.Cheered))
                    Assert.That(visual.LastReaction, Is.EqualTo(CharacterPresentation.Reaction.Cheered), npc.Id + " and claps them out.");
            }
            foreach (var npc in SceneComponents<HouseNpc>().Where(each => each.gameObject.activeInHierarchy && each.Id != evicted))
                if (!Presentation(npc).IsSeated)
                    Assert.That(Presentation(npc).LastReaction, Is.Not.EqualTo(CharacterPresentation.Reaction.Cheered), npc.Id + " stands, and only looks.");
            Assert.That(Presentation(player).LastReaction, Is.Not.EqualTo(CharacterPresentation.Reaction.Cheered), "The player's body is the player's.");
            if (embraces)
                yield return WaitForFarewellEndpoint(() =>
                    {
                        WatchTheGoodbye("while the one who stays stood");
                        return !survivorSeat.Active && (director.GoodbyeFarewell != null
                            || Presentation(staying).IsPrimitiveBody && Presentation(leaving).IsPrimitiveBody
                            && Presentation(staying).LastReaction == CharacterPresentation.Reaction.Farewell
                            && Presentation(leaving).LastReaction == CharacterPresentation.Reaction.Farewell);
                    },
                    2f, "the one who stays stands for the short reciprocal farewell", evicted, survivor);
            else Assert.That(survivorSeat.Active, Is.True, "The survivor, the player's body, is still in red.");
            if (director.GoodbyeFarewell != null)
            {
                // The farewell's own frames are watched too, each as it plays: an embrace begun and let
                // go inside it would leave nothing for a later look to find (S2D-R3).
                var farewell = ObserveFarewellToIdle(director.GoodbyeFarewell, closedAt + EpisodeDirector.GoodbyeSeconds, farewellGraph);
                while (farewell.MoveNext())
                {
                    WatchTheGoodbye("during the farewell");
                    Assert.That(embraceSeen, Is.Null, "The farewell is the short noncontact one: " + embraceSeen);
                    WatchTheKey("during the farewell");
                    yield return farewell.Current;
                }
            }
            // Read from a landed camera at least once: on the push in when nobody stands for the
            // farewell, and on the farewell's own shot or the face after it when somebody does.
            while ((Time.unscaledTime < keyOnTheFaceBy || keyReadsFromTheLens == 0)
                && director.CeremonyStagePhase == EpisodeDirector.CeremonyStageStep.Goodbye
                && Time.unscaledTime < closedAt + EpisodeDirector.GoodbyeSeconds - 0.5f)
            {
                WatchTheGoodbye("while the key light found their face");
                WatchTheKey("as the camera settled on them");
                yield return null;
            }
            WatchTheKey("once the camera settled on them");
            Assert.That(keyReadsFromTheLens, Is.GreaterThan(0),
                "The key light was read on the evicted's face from a landed camera during the goodbye. " + director.CeremonyStageReport("goodbye key light"));
            var face = leaving.transform.position + Vector3.up * 1.55f;
            Assert.That(cameraRig.HasShot, Is.True, "The camera is the goodbye's,");
            Assert.That(Vector3.Distance(cameraRig.DesiredFocus, face), Is.LessThan(0.3f), "on the evicted's face.");
            var strip = SceneComponents<CeremonySting>().Single().GetComponentsInChildren<TMP_Text>(true).Select(text => text.text).ToList();
            string standing = EpisodeDirector.GoodbyeLine(director.Snapshot, evicted, EpisodeDirector.GoodbyeMoment.Standing);
            Assert.That(strip, Has.Some.EqualTo(standing), "The goodbye line plays here, not at the door,");
            Assert.That(standing, Does.Not.Contain("door"), "worded for where they are - standing before the house - not for the doorway (UI-UX-PASS-PLAN W0).");
            Assert.That(strip, Has.None.EqualTo(EpisodeDirector.GoodbyeLine(director.Snapshot, evicted)), "The doorway's words wait for a body at the door.");

            // Then the walk, and not before the goodbye is over.
            yield return WaitFor(() =>
                {
                    WatchTheGoodbye("before the walk");
                    return director.WalkingOutId != null;
                }, EpisodeDirector.GoodbyeSeconds + 1f, "the goodbye gives way to the walk out");
            WatchTheGoodbye("as the walk began");
            Assert.That(embraceSeen, Is.Null,
                "Nobody embraces at the goodbye: it is the short noncontact farewell, and the 3.8 s embrace is the peace's alone.");
            Assert.That(director.GoodbyeFarewell, Is.Null, "The short transaction was cleaned before the departure acquired its route.");
            Assert.That(Newtonsoft.Json.Linq.JToken.DeepEquals(farewellGraph, UvPayload(director.Snapshot)), Is.True,
                "Standing, greeting, camera and departure handover change no saved field, ballot knowledge, receipt or RNG.");
            foreach (var id in new[] { evicted, survivor })
            {
                var agent = HouseguestBody(id).GetComponent<HouseNpcMotion>();
                Assert.That(agent == null || !agent.IsParked, Is.True, "and " + id + "'s agent is unparked.");
            }
            float goodbye = Time.unscaledTime - closedAt;
            Assert.That(goodbye, Is.InRange(EpisodeDirector.GoodbyeSeconds - 0.1f, EpisodeDirector.GoodbyeSeconds + 0.5f),
                "The goodbye holds about four seconds.");
            Assert.That(director.WalkingOutId, Is.EqualTo(evicted));
            Assert.That(director.WalkOutIsStaged, Is.True, "The walk out is the stage's,");
            Assert.That(director.CeremonyStagePhase, Is.EqualTo(EpisodeDirector.CeremonyStageStep.Release), "the house keeps its seats to watch,");
            Assert.That(Hud.IsHeldForReveal, Is.True, "the chrome is still aside,");
            Assert.That(director.EvictionKeyLightShowing, Is.False, "and the key light went with the goodbye.");
            var motion = leaving.GetComponent<HouseNpcMotion>();
            yield return WaitFor(() =>
                {
                    WatchTheGoodbye("on the walk out");
                    return motion.LeaseId != null && motion.LeaseId.StartsWith("departure:");
                }, 3f, "the walk out takes its first route");
            Assert.That(motion.Speed, Is.LessThanOrEqualTo(EpisodeDirector.StagedWalkSpeed + 0.05f), "They walk slowly while the house watches.");
            // The walk has its own key light from its first route, aimed at their head from the
            // camera's side, where the goodbye's no longer reaches.
            Assert.That(director.WalkingOutId, Is.EqualTo(evicted), "They are still walking,");
            Assert.That(director.transform.Find(EpisodeDirector.WalkOutKeyLightName), Is.Not.Null, "and the walk has a key light of its own,");
            Assert.That(WalkKeyOffTheHead(leaving, cameraRig.HasArrived(), out _), Is.Null, "on their head from the camera's side:");
            Assert.That(director.EvictionKeyLightShowing, Is.False, "the goodbye's stays off.");

            director.SkipWalkOut();
            yield return WaitFor(() =>
                {
                    WatchTheGoodbye("as the door shut");
                    return !director.IsCeremonyStaged;
                }, 3f, "the house gets up");
            yield return null;
            WatchTheGoodbye("once the house got up");
            Assert.That(embraceSeen, Is.Null, "and nobody embraces after it either.");
            Assert.That(Hud.IsHeldForReveal, Is.False, "The chrome is back once the door is shut behind them,");
            Assert.That(director.transform.Find(EpisodeDirector.EvictionKeyLightName), Is.Null, "the key light is gone,");
            Assert.That(ExitLightsOn(), Is.Empty, "no light of the exit's is left on after the press,");
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null, "and the house's world runs on.");
            Assert.That(motion.Speed, Is.EqualTo(HouseNpcMotion.DefaultSpeed), "The pace is the house's own again.");
        }

        /// <summary>
        /// Why the staged walk's own key light is not on the departing head from the camera's side,
        /// or null. A key gone dark is no fault - a wall between the head and the lens puts it out
        /// rather than paint the wall - and <paramref name="lit"/> says whether it was read lit.
        /// </summary>
        private string WalkKeyOffTheHead(Component body, bool fromTheLens, out bool lit)
        {
            lit = false;
            var key = director.transform.Find(EpisodeDirector.WalkOutKeyLightName);
            var lamp = key != null ? key.GetComponent<Light>() : null;
            if (lamp == null) return "there is no walk-out key light";
            if (lamp.type != LightType.Spot || lamp.shadows != LightShadows.None || lamp.range > 4.5f)
                return "the walk-out key is not an unshadowed spot reaching 4.5 m at most (" + lamp.type + ", " + lamp.shadows + ", range " + lamp.range.ToString("F2") + ")";
            if (!lamp.enabled) return null;
            lit = true;
            var bone = Presentation(body).HeadBone;
            var head = bone != null ? bone.position : body.transform.position + Vector3.up * 1.55f;
            var lens = cameraRig.ViewCamera.transform.position;
            var toKey = key.position - head;
            var off = new List<string>();
            if (fromTheLens && Vector3.Dot(toKey.normalized, (lens - head).normalized) <= 0.5f) off.Add("not on the camera's side of the head");
            if (Vector3.Angle(key.forward, head - key.position) >= 8f) off.Add("not aimed at the head");
            return off.Count == 0 ? null : string.Join(", ", off) + " (key " + key.position.ToString("F2") + ", head " + head.ToString("F2")
                + ", lens " + lens.ToString("F2") + ") at frame " + Time.frameCount;
        }

        /// <summary>The exit's lights still on: the walk's key, and the door's spill over the threshold.</summary>
        private static string[] ExitLightsOn() => Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
            .Where(lamp => lamp != null && lamp.enabled
                && (lamp.name == EpisodeDirector.WalkOutKeyLightName || lamp.name == OpeningDoorSet.BurstLightName))
            .Select(lamp => lamp.name + " under " + lamp.transform.root.name).ToArray();

        /// <summary>The brightest channel the door's backing is drawn in, or -1 when it has none.</summary>
        private static float BackingBrightest(GameObject root)
        {
            var plane = root.GetComponentsInChildren<Renderer>(true).LastOrDefault(each => each.name == OpeningDoorSet.OutsideLightName);
            var material = plane != null ? plane.sharedMaterial : null;
            if (material == null) return -1f;
            var colour = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.color;
            return colour.maxColorComponent;
        }

        /// <summary>
        /// The door opens on the door shot, shuts behind them while their body is still in the
        /// house, and the shot holds on the shut door for its whole hold; only then does their body
        /// go, behind a dip, and the door with it. The chrome is aside until then. The yard's door,
        /// the fallback: it is built only at the cut to it, where the living room's stands closed
        /// from the goodbye (StagedExit_TheLivingRoomsDoorLetsThemOut). The light beyond the open
        /// door is a lit room, not a white sheet; the walk has its own key light on their head from
        /// the camera's side until the door closes, the goodbye's staying off; and no light of the
        /// exit's is left on once they are gone.
        /// </summary>
        [UnityTest]
        public IEnumerator StagedExit_TheDoorShutsBehindThemBeforeTheyGo()
        {
            yield return InstallStagedSeason(52, AtEviction);
            director.WalkOutThrough = EpisodeDirector.WalkOutDoor.Yard;
            yield return PlayTheStagedEvictionToItsWalkOut();
            string leaving = stagedLeaving;
            var body = HouseguestBody(leaving);
            var motion = body.GetComponent<HouseNpcMotion>();
            Assert.That(director.WalkOutIsStaged, Is.True, "The walk out is the stage's.");
            Assert.That(DoorSetUp(), Is.False, "The door is not standing in the yard while the house watches them go.");

            float widest = 0f, shutAt = 0f, backing = -1f;
            bool sawOpen = false, onTheDoor = false, shut = false, inTheHouseWhenShut = false, heldThrough = true;
            bool goodbyeKeyOn = false;
            string walkKeyOff = null;
            int walkKeyReads = 0, walkKeyReadsFromTheLens = 0;
            float by = Time.realtimeSinceStartup + EpisodeDirector.WalkOutSeconds + 2f;
            while (director.WalkingOutId != null && Time.realtimeSinceStartup < by)
            {
                var root = SceneRoot(OpeningDoorSet.RootName);
                var door = root != null ? root.GetComponent<OpeningDoorSet>() : null;
                if (door != null)
                {
                    if (!sawOpen && door.IsOpen)
                    {
                        sawOpen = true;
                        // The opening's door shot, whose focus the push-in keeps.
                        onTheDoor = Vector3.Distance(cameraRig.DesiredFocus, DoorShotFocusForTests) < 0.2f;
                        backing = BackingBrightest(root);
                    }
                    widest = Mathf.Max(widest, door.Openness);
                    if (!shut && widest >= 0.35f && !door.IsOpen && door.Openness <= 0.01f)
                    {
                        shut = true;
                        shutAt = Time.unscaledTime;
                        inTheHouseWhenShut = body.gameObject.activeInHierarchy;
                    }
                }
                goodbyeKeyOn |= director.EvictionKeyLightShowing;
                // From the walk's first route until the door is told to close behind them.
                bool closing = door != null && widest >= 0.35f && !door.IsOpen;
                if (walkKeyOff == null && director.WalkingOutId == leaving && !closing
                    && motion != null && motion.LeaseId != null && motion.LeaseId.StartsWith("departure:"))
                {
                    bool landed = cameraRig.HasArrived();
                    walkKeyOff = WalkKeyOffTheHead(body, landed, out bool lit);
                    if (lit) walkKeyReads++;
                    if (lit && landed) walkKeyReadsFromTheLens++;
                }
                heldThrough &= Hud.IsHeldForReveal;
                yield return null;
            }
            Assert.That(director.WalkingOutId, Is.Null, "The walk out ends");
            Assert.That(director.IsTravelDipShowing, Is.True, "behind a dip.");
            float held = Time.unscaledTime - shutAt;
            Assert.That(sawOpen, Is.True, "The door opened for them,");
            Assert.That(onTheDoor, Is.True, "with the camera on the door,");
            Assert.That(widest, Is.GreaterThanOrEqualTo(0.35f), "wide enough to walk through,");
            Assert.That(shut, Is.True, "and shut again behind them");
            Assert.That(inTheHouseWhenShut, Is.True, "while their body was still in the house:");
            Assert.That(held, Is.GreaterThanOrEqualTo(EpisodeDirector.DoorHoldSeconds - 0.05f), "the shot held on the shut door before they went.");
            Assert.That(heldThrough, Is.True, "The chrome was aside for the whole walk.");
            Assert.That(backing, Is.InRange(0f, 0.8f), "The light beyond the open door is a lit room, no brighter than 0.8, not a white sheet.");
            Assert.That(goodbyeKeyOn, Is.False, "The goodbye's key light stayed off for the walk;");
            Assert.That(walkKeyOff, Is.Null, "the walk's own key light is on their head from the camera's side until the door closes:");
            Assert.That(walkKeyReadsFromTheLens, Is.GreaterThan(0),
                "and it was read lit from a landed camera (" + walkKeyReads + " lit reads in all).");
            Assert.That(body.gameObject.activeInHierarchy, Is.False, "They are gone,");
            yield return null;
            Assert.That(DoorSetUp(), Is.False, "and the door with them,");
            Assert.That(ExitLightsOn(), Is.Empty, "and no light of the exit's is left on.");
            yield return WaitFor(() => !director.IsCeremonyStaged, 3f, "the house gets up");
            yield return null;
            Assert.That(Hud.IsHeldForReveal, Is.False, "The chrome is back.");
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null, "The house's world runs on.");
        }

        /// <summary>
        /// A press on the walk goes straight to the door shut behind them, as one on the goodbye
        /// does (CeremonyStage_TheSkipStepsThroughAnEvictionAndItsWalkOut): the body goes under the
        /// dip, the door with it, and nothing the commit decided changes.
        /// </summary>
        [UnityTest]
        public IEnumerator StagedExit_APressOnTheWalkGoesStraightToTheShutDoor()
        {
            yield return InstallStagedSeason(52, AtEviction);
            yield return PlayTheStagedEvictionToItsWalkOut();
            string leaving = stagedLeaving;
            var body = HouseguestBody(leaving);
            var committed = director.Snapshot;
            // Past the guard that keeps the press already in flight from ending the walk.
            yield return RealSeconds(0.5f);
            Assert.That(director.WalkingOutId, Is.EqualTo(leaving), "They are walking.");
            yield return PressKey(Key.Enter);
            Assert.That(director.WalkingOutId, Is.Null, "A press ends the walk out:");
            Assert.That(director.DepartingId, Is.Null, "nobody is leaving any more,");
            Assert.That(director.StagedExitRunning, Is.False, "the exit is over,");
            Assert.That(body.gameObject.activeInHierarchy, Is.False, "their body is gone,");
            Assert.That(director.IsTravelDipShowing, Is.True, "behind a dip.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(committed.revision), "Skipping commits nothing:");
            Assert.That(director.Snapshot.Find(leaving).status, Is.EqualTo(committed.Find(leaving).status), "the outcome is the committed one.");
            yield return null;
            Assert.That(DoorSetUp(), Is.False, "The door is struck.");
            yield return WaitFor(() => !director.IsCeremonyStaged, 3f, "the house gets up");
            yield return null;
            Assert.That(Hud.IsHeldForReveal, Is.False, "The chrome is back.");
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null);
        }

        /// <summary>Eviction night with the player on the block beside the second houseguest; the first is Head of Household.</summary>
        private static void AtEvictionWithThePlayerOnTheBlock(EpisodeState state)
        {
            AtEviction(state);
            var npcs = state.Active.Where(actor => !actor.isPlayer).Select(actor => actor.id).ToList();
            state.nominees = new List<string> { state.playerId, npcs[1] };
            // The night already past its speeches, with every regular voter's ballot against the
            // player, so the vote the engine counts evicts them. A seed search over the fixture as it
            // was found no season from 52 that did, even with the house's scores turned against the
            // player (measured 2026-10-01): the ballot is not the scores alone. The test is about the
            // exit, not the vote.
            foreach (var speaker in state.nominees)
                state.evictionSpeeches.Add(new EvictionSpeechState
                {
                    speakerId = speaker, week = state.week, isPlayerAuthored = speaker == state.playerId,
                    text = "I'd like to stay.",
                });
            state.evictionStage = EvictionStage.Voting;
            foreach (var voter in EpisodeEngine.Voters(state))
                state.votes.Add(new VoteState { voterId = voter.id, targetId = state.playerId, reason = "The fixture's ballot." });
        }

        /// <summary>
        /// The first season from <paramref name="from"/> on, shaped by <paramref name="shape"/>, in
        /// which the house's next legal decisions evict the player at this eviction. Played on the
        /// engine alone, as <see cref="SeasonThePlayerSurvives"/> is; the director plays the same
        /// decisions, and the house's world is paused on eviction night.
        /// </summary>
        private static uint SeasonThePlayerIsEvicted(uint from, System.Action<EpisodeState> shape)
        {
            for (uint seed = from; seed < from + 60; seed++)
            {
                var engine = new EpisodeEngine(StrategySeason(seed, shape));
                int week = engine.Snapshot.week;
                for (int guard = 0; guard < 16; guard++)
                {
                    var state = engine.Snapshot;
                    if (state.Find(state.playerId).status != ContestantStatus.Active) return seed;
                    if (state.phase != EpisodePhase.Eviction || state.week != week) break;
                    if (!engine.Apply(NextCommand(state)).accepted) break;
                }
            }
            Assert.Fail("No season from seed " + from + " evicts the player on the block.");
            return from;
        }

        /// <summary>
        /// The player as the evicted nominee: both red chairs are held at the card's start, the
        /// player's among them, and their eviction is the season's turn to the jury as it was - no
        /// goodbye, and no walk out.
        /// </summary>
        [UnityTest]
        public IEnumerator StagedExit_TheEvictedPlayerHasNoGoodbyeAndNoWalk()
        {
            uint seed = SeasonThePlayerIsEvicted(52, AtEvictionWithThePlayerOnTheBlock);
            yield return InstallStagedSeason(seed, AtEvictionWithThePlayerOnTheBlock);
            var block = director.Snapshot.nominees.ToList();
            Assert.That(block, Does.Contain(director.Snapshot.playerId), "The player is on the block.");
            yield return PlayUntilTheCeremony();
            Assert.That(director.IsCeremonyStaged, Is.True, "The eviction is staged in the living room.");
            Assert.That(director.Snapshot.Find(director.Snapshot.playerId).status, Is.Not.EqualTo(ContestantStatus.Active),
                "The player is evicted, as the engine alone played season " + seed + ".");
            Assert.That(director.DepartingId, Is.Null, "The player is never the house's to walk out.");
            yield return WaitFor(() => director.CeremonyStagePhase == EpisodeDirector.CeremonyStageStep.Playing,
                EpisodeDirector.SummonsHardSeconds(CeremonyPace.Suspenseful) + 6f, "the vote plays");
            var start = StageReport("card start, read by the test");
            foreach (var id in block)
                Assert.That(start, Does.Contain("  " + id + " -> " + CeremonySeating.HotSeat + " "), id + " holds a red chair at the card's start:\n" + start);

            yield return SkipReveals();
            bool saidGoodbye = false, walked = false;
            float by = Time.realtimeSinceStartup + 3f;
            while (director.IsCeremonyStaged && Time.realtimeSinceStartup < by)
            {
                saidGoodbye |= director.CeremonyStagePhase == EpisodeDirector.CeremonyStageStep.Goodbye;
                walked |= director.WalkingOutId != null;
                yield return null;
            }
            Assert.That(director.IsCeremonyStaged, Is.False, "The house gets up once the card is down.");
            Assert.That(saidGoodbye, Is.False, "There is no goodbye,");
            Assert.That(walked, Is.False, "and no walk out.");
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null);
        }

        /// <summary>The look sheet's frames of the exit: the goodbye in the living room, the door open with the evicted at it, and the door shut behind them. Batch runs only.</summary>
        [UnityTest]
        public IEnumerator StagedExit_CapturesTheGoodbyeTheDoorAndTheShutDoor()
        {
            if (!Application.isBatchMode) Assert.Ignore("This stock-camera capture requires the batch qualification viewport.");
            yield return InstallStagedSeason(52, AtEviction);
            yield return PlayUntilTheCeremony();
            Assert.That(director.IsCeremonyStaged, Is.True, "The eviction is staged in the living room.");
            director.SkipCeremonySummons();
            yield return WaitFor(() => director.CeremonyStagePhase == EpisodeDirector.CeremonyStageStep.Playing, 3f, "the vote plays");
            yield return SkipVoteToGoodbye();
            string evicted = director.DepartingId;
            var sting = SceneComponents<CeremonySting>().Single();
            // Past the push-in (2.05 s), inside the goodbye. The strip says what the picture shows -
            // the evicted standing before the house - and not "from the doorway" (UI-UX-PASS-PLAN
            // W0, sweep-show 10). Read as the wait ends, before the capture's own frames: the strip
            // runs its 2.6 s on the unscaled clock, which a capture's long frame spends.
            yield return RealSeconds(2.1f);
            Assert.That(sting.IsPlaying, Is.True, "The strip is up on the goodbye's frame,");
            var words = sting.GetComponentsInChildren<TMP_Text>(true).Select(text => text.text).ToList();
            Assert.That(words, Has.Some.EqualTo("GOODBYE"), "saying goodbye,");
            Assert.That(words, Has.Some.EqualTo(EpisodeDirector.GoodbyeLine(director.Snapshot, evicted, EpisodeDirector.GoodbyeMoment.Standing)),
                "with the words for where they are: standing, not at the door.");
            // The goodbye belongs to the stage and frames the named subject's room; the walk out draws the whole house.
            AssertGoodbyeRoom(HouseguestBody(evicted), "The goodbye");
            yield return CaptureFraming("walk-out-goodbye", settle: false);
            yield return WaitFor(() => director.WalkOutAtTheDoor, EpisodeDirector.GoodbyeSeconds + EpisodeDirector.WalkOutSeconds, "the door opens for them");
            yield return RealSeconds(0.6f);
            Rs6bAssertTheWalkOutDrawsTheWholeHouse("The door open, the evicted at it");
            yield return CaptureFraming("walk-out-door", settle: false);
            OpeningDoorSet Door() { var root = SceneRoot(OpeningDoorSet.RootName); return root != null ? root.GetComponent<OpeningDoorSet>() : null; }
            yield return WaitFor(() => Door() == null || (!Door().IsOpen && Door().Openness <= 0.01f), EpisodeDirector.WalkOutSeconds, "the door shuts behind them");
            Assert.That(Door(), Is.Not.Null, "The shot holds on the shut door.");
            Rs6bAssertTheWalkOutDrawsTheWholeHouse("The door shut behind them");
            yield return CaptureFraming("walk-out-shut", settle: false);
            yield return WaitFor(() => !director.StagedExitRunning, EpisodeDirector.WalkOutSeconds, "the exit ends");
            yield return WaitFor(() => !director.IsCeremonyStaged, 3f, "the house gets up");
        }

        private IEnumerator SkipVoteToGoodbye()
        {
            string evicted = director.DepartingId;
            Assert.That(evicted, Is.Not.Null.And.Not.Empty);
            yield return SkipReveals();
            // A skipped summons can leave the departing houseguest dressing in another room.
            // The director keeps the vote shot until that actor can appear in the goodbye.
            yield return WaitFor(() => director.CeremonyStagePhase != EpisodeDirector.CeremonyStageStep.Playing,
                CharacterPresentation.AssemblyTimeoutSeconds, "the vote hands off when the departing body is ready");
            Assert.That(director.CeremonyStagePhase, Is.EqualTo(EpisodeDirector.CeremonyStageStep.Goodbye), "The goodbye.");
            Assert.That(director.DepartingId, Is.EqualTo(evicted), "The goodbye belongs to the evicted houseguest.");
        }

        // The normal close shot draws Living. With a skipped summons the shot owns only
        // the named evictee's actual room; the gathering record remains where the actor was.
        private void AssertGoodbyeRoom(HouseNpc leaving, string when)
        {
            string room = director.WalkerRoomOf(leaving.transform);
            Assert.That(room, Is.Not.Null.And.Not.Empty, when);
            if (room == EpisodeDirector.CeremonyStageRoom(CeremonySting.EvictionKind))
                Rs6aAssertTheStageDrawsItsRoom(when);
            else
            {
                Assert.That(director.IsCeremonyStaged && cameraRig.HasShot, Is.True, when);
                Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.Ceremony), when);
                var actual = RoomSet.Of(room);
                Assert.That(cameraRig.Rooms, Is.EqualTo(actual), when + ": the close shot names the evictee's room.");
                Rs6aAssertTheViewIs(actual, when);
            }
            Assert.That(director.IsBodyDrawn(leaving.transform), Is.True, when + ": the evictee is included by the body pass.");
        }

        [UnityTest]
        public IEnumerator StagedExit_TheSeatedEvicteeIsVisibleThroughTheGoodbyeAndHandback()
        {
            yield return CheckGoodbyeTake(false);
        }

        [UnityTest]
        public IEnumerator StagedExit_TheOffRoomEvicteeIsVisibleThroughTheGoodbyeAndHandback()
        {
            yield return CheckGoodbyeTake(true);
        }

        private IEnumerator CheckGoodbyeTake(bool skipSummons)
        {
            if (!Application.isBatchMode) Assert.Ignore("This stock-camera capture requires the batch qualification viewport.");
            yield return InstallStagedSeason(52, AtEviction);
            yield return PlayUntilTheCeremony();
            string id = director.DepartingId;
            var leaving = HouseguestBody(id);
            Assert.That(id, Is.EqualTo("taylor-kim"));
            Assert.That(director.Snapshot.Find(id).name, Does.StartWith("Taylor"));
            if (skipSummons) director.SkipCeremonySummons();
            yield return WaitFor(() => director.CeremonyStagePhase == EpisodeDirector.CeremonyStageStep.Playing,
                EpisodeDirector.SummonsHardSeconds(CeremonyPace.Suspenseful) + 6f, "the vote card opens");
            HouseSeatPresentation CurrentSeat() => leaving.GetComponent<HouseSeatPresentation>();
            var hot = CeremonySeating.Anchors(director.gameObject.scene, CeremonySeating.HotSeat);
            Assert.That(hot.Count, Is.EqualTo(2));
            if (!skipSummons)
            {
                yield return WaitFor(() => CurrentSeat() != null && CurrentSeat().Settled, 8f, "the named evictee sits in red on the unskipped summons");
                var hips = HipsOf(leaving);
                Vector3 seatedAt = hips != null ? hips.position : CurrentSeat().VisualFeet;
                int assigned = director.Snapshot.nominees.IndexOf(id);
                Assert.That(assigned, Is.InRange(0, hot.Count - 1));
                Assert.That(Flat(seatedAt, hot[assigned].Position), Is.LessThan(.5f), "Taylor sits in their assigned red chair.");
            }
            var visual = Presentation(leaving);
            string stageRoom = EpisodeDirector.CeremonyStageRoom(CeremonySting.EvictionKind);
            bool located = HouseRoomQuery.TryCreate(director.gameObject.scene, out var floors, out var floorReason);
            Assert.That(located, Is.True, floorReason);
            string voteRoom = director.WalkerRoomOf(leaving.transform);
            bool seatedAtVote = CurrentSeat() != null && CurrentSeat().Active;
            bool drawnAtVote = director.IsBodyDrawn(leaving.transform);
            if (skipSummons)
            {
                Assert.That(seatedAtVote, Is.False, "This take requires the actual skipped, unseated evictee.");
                Assert.That(RoomSet.Of(voteRoom).IsEmpty, Is.False);
                Assert.That(voteRoom, Is.Not.EqualTo(stageRoom));
                Assert.That(floors.TryLocate(leaving.transform.position, .05f, out var floorAtVote), Is.True);
                Assert.That(floorAtVote, Is.EqualTo(voteRoom), "Taylor's actual feet and the room tracker agree before the skipped goodbye.");
            }
            else Assert.That(voteRoom, Is.EqualTo(EpisodeDirector.CeremonyStageRoom(CeremonySting.EvictionKind)));

            string folder = GoodbyeTakeFolder(skipSummons);
            var rows = new StringBuilder("frame\twhen\tunscaledSeconds\tgoodbyeSeconds\tid\tname\tstatus\tvoteRoom\tseatedAtVote\tdrawnAtVote\tactorRoom\tviewRooms\tscopeSource\tcullingMask\tstage\tshot\tdip\troot\teye\theadViewport\theadChanged\theadTested\ttorsoChanged\ttorsoTested\tmeshRenderers\tmaskIncluded\tactorDrawn\thudHeld\n");
            var cameraTrace = new StringBuilder("frame\tgoodbyeSeconds\ttrackerRoom\tfloorRoom\tseatActive\tvisualSeated\trequestedBoomM\tappliedBoomM\ttravelling\tviewRooms\tcullingMask\tactorDrawn\n");
            var handback = new StringBuilder("frame\tunscaledSeconds\tsource\tphase\tstagedExit\tstageActive\thudHeld\tdip\tshot\ttravelling\tcameraControl\tplayerInput\tpanel\tweeklyRecap\treturnButtonFrame\n");
            string lastHandoffState = null;
            int recapFrame = -1, returnButtonFrame = -1;
            var pictures = new List<(string path, Texture2D image)>();
            var issues = new List<string>();
            var host = new GameObject("Stock eviction goodbye observer");
            var probe = host.AddComponent<FrameFootProbe>();
            var camera = cameraRig.ViewCamera;
            var lens = new CaptureLens(camera, 960, 540);
            float began = -1f;
            // The longest frame seen in the goodbye: every timing window below widens by it, so one
            // stretched frame (a busy machine) cannot fail a take whose cut, push and landing are right.
            float longestFrame = 0f, landedAt = -1f;
            int portraits = 0, entryFrame = -1, cutLanded = -1, pushBegan = -1, pushMoving = -1, pushLanded = -1;
            bool released = false, free = false, lostOffRoom = false;
            Exception observerFailure = null;
            try
            {
                yield return null;
                yield return lens.MakeSureTheCanvasesAreDrawn();
                var sting = SceneComponents<CeremonySting>().Single();
                string standing = EpisodeDirector.GoodbyeLine(director.Snapshot, id, EpisodeDirector.GoodbyeMoment.Standing);
                string[] moments = { "early", "held", "late" };
                float[] at = { .20f, 2.10f, 3.45f };
                void Sample(string moment, bool portrait)
                {
                    int frame = Time.frameCount;
                    var image = lens.Read();
                    pictures.Add((Path.Combine(folder, moment + "-frame-" + frame + ".png"), image));
                    var root = leaving.transform;
                    var room = director.WalkerRoomOf(root);
                    var head = visual.HeadBone;
                    var parts = visual.VisualRoot == null ? Array.Empty<Renderer>()
                        : visual.VisualRoot.GetComponentsInChildren<Renderer>().Where(r => (r is MeshRenderer || r is SkinnedMeshRenderer)
                            && r.enabled && !r.forceRenderingOff && r.gameObject.activeInHierarchy).ToArray();
                    int included = parts.Count(r => (camera.cullingMask & (1 << r.gameObject.layer)) != 0);
                    var headPoint = head != null ? head.position : parts.FirstOrDefault(r => r.name == "Face")?.bounds.center;
                    Vector2? headView = headPoint.HasValue ? InTheFrame(camera, headPoint.Value) : null;
                    var headCounts = (changed: 0, tested: 0);
                    var torsoCounts = (changed: 0, tested: 0);
                    if (portrait)
                    {
                        if (skipSummons && (!floors.TryLocate(root.position, .05f, out var floorRoom)
                            || floorRoom != voteRoom || room != voteRoom || room == stageRoom || visual.IsSeated || CurrentSeat()?.Active == true))
                            issues.Add(moment + ": Taylor was not the original standing, off-room subject in both room observations.");
                        if (skipSummons && moment == "early" && (pushBegan < 0 || !cameraRig.IsTravelling))
                            issues.Add("The early portrait did not show the timed push to 2.4 m.");
                        if (skipSummons && moment != "early" && (pushLanded < 0 || cameraRig.IsTravelling || Mathf.Abs(cameraRig.AppliedDistance - 2.4f) > .07f))
                            issues.Add(moment + ": the room-correct goodbye push had not landed at 2.4 m.");
                        if (director.DepartingId != id || visual.CharacterId != id || director.CeremonyStagePhase != EpisodeDirector.CeremonyStageStep.Goodbye)
                            issues.Add(moment + ": the named subject or active goodbye changed.");
                        if (moment != "late" && (!sting.IsPlaying || !sting.GetComponentsInChildren<TMP_Text>(false).Any(t => t.text == standing)))
                            issues.Add(moment + ": Taylor's standing goodbye was absent from the strip.");
                        try { AssertGoodbyeRoom(leaving, moment); }
                        catch (AssertionException ex) { issues.Add(ex.Message); }
                        var chest = visual.VisualRoot != null ? visual.VisualRoot.GetComponentInChildren<Animator>() : null;
                        var upper = chest != null && chest.isInitialized && chest.isHuman
                            ? chest.GetBoneTransform(HumanBodyBones.Chest) ?? chest.GetBoneTransform(HumanBodyBones.Spine) : null;
                        Vector3? torsoPoint = upper != null ? upper.position : parts.FirstOrDefault(r => r.name == "Torso")?.bounds.center;
                        Texture2D without = null;
                        try
                        {
                            foreach (var part in parts) part.forceRenderingOff = true;
                            without = lens.Read();
                            var actual = image.GetPixels32();
                            var masked = without.GetPixels32();
                            if (headPoint.HasValue)
                                headCounts = GoodbyeContribution(camera, image.width, image.height, actual, masked, headPoint.Value, .068f);
                            if (torsoPoint.HasValue)
                                torsoCounts = GoodbyeContribution(camera, image.width, image.height, actual, masked, torsoPoint.Value, .10f);
                        }
                        finally
                        {
                            foreach (var part in parts) if (part != null) part.forceRenderingOff = false;
                            if (without != null) Object.Destroy(without);
                        }
                        if (headCounts.tested < 25 || headCounts.changed < .4f * headCounts.tested
                            || torsoCounts.tested < 25 || torsoCounts.changed < .4f * torsoCounts.tested || Time.frameCount != frame)
                            issues.Add(moment + ": the named actor did not supply the required head and torso pixels.");
                    }
                    if (moment == "first-free-camera")
                    {
                        var playerRoom = director.WalkerRoomOf(player.transform);
                        if (cameraRig.FocusedSubject != null && cameraRig.FocusedSubject != player.transform
                            || !director.ViewedRooms.Contains(playerRoom) || !director.IsBodyDrawn(player.transform))
                            issues.Add("The first normal view did not return to the player's room and body.");
                        var pixels = image.GetPixels32();
                        int darkest = 765, brightest = 0;
                        for (int y = image.height / 4; y < image.height * 3 / 4; y += 7)
                            for (int x = image.width / 4; x < image.width * 3 / 4; x += 7)
                            {
                                var c = pixels[y * image.width + x];
                                int lit = c.r + c.g + c.b;
                                darkest = Math.Min(darkest, lit); brightest = Math.Max(brightest, lit);
                            }
                        if (brightest - darkest < 35) issues.Add("The first normal view had no readable scene contrast.");
                    }
                    rows.AppendLine(FormattableString.Invariant($"{frame}\t{moment}\t{Time.unscaledTime:F5}\t{Time.unscaledTime - began:F5}\t{id}\t{director.Snapshot.Find(id).name}\t{director.Snapshot.Find(id).status}\t{voteRoom}\t{seatedAtVote}\t{drawnAtVote}\t{room}\t{director.ViewedRooms}\t{director.CurrentRoomScopeSource}\t{camera.cullingMask}\t{director.CeremonyStagePhase}\t{cameraRig.HasShot}\t{director.IsTravelDipShowing}\t{root.position:F3}\t{camera.transform.position:F3}\t{headView}\t{headCounts.changed}\t{headCounts.tested}\t{torsoCounts.changed}\t{torsoCounts.tested}\t{parts.Length}\t{included}\t{director.IsBodyDrawn(root)}\t{Hud.IsHeldForReveal}"));
                }
                probe.Foot = () =>
                {
                    if (observerFailure != null || free) return;
                    try
                    {
                        if (director.CeremonyStagePhase == EpisodeDirector.CeremonyStageStep.Goodbye)
                        {
                            float requested = cameraRig.DesiredDistance, applied = cameraRig.AppliedDistance;
                            bool travelling = cameraRig.IsTravelling;
                            string room = director.WalkerRoomOf(leaving.transform);
                            bool onFloor = floors.TryLocate(leaving.transform.position, .05f, out var floor);
                            bool seated = CurrentSeat()?.Active == true;
                            if (began < 0f)
                            {
                                began = Time.unscaledTime;
                                entryFrame = Time.frameCount;
                                if (!cameraRig.HasShot || Mathf.Abs(requested - 3.1f) > .025f)
                                    issues.Add("The first authored goodbye frame did not request the 3.1 m standing cut.");
                                if (skipSummons && (seated || visual.IsSeated))
                                    issues.Add("Taylor was seated when the skipped goodbye began.");
                            }
                            float elapsed = Time.unscaledTime - began;
                            longestFrame = Mathf.Max(longestFrame, Time.unscaledDeltaTime);
                            cameraTrace.AppendLine(FormattableString.Invariant($"{Time.frameCount}\t{elapsed:F5}\t{room}\t{floor}\t{seated}\t{visual.IsSeated}\t{requested:F5}\t{applied:F5}\t{travelling}\t{director.ViewedRooms}\t{camera.cullingMask}\t{director.IsBodyDrawn(leaving.transform)}"));
                            if (skipSummons && !lostOffRoom && (!onFloor || floor != voteRoom || room != voteRoom || room == stageRoom || seated || visual.IsSeated))
                            {
                                lostOffRoom = true;
                                issues.Add($"Taylor left the off-room, standing fallback on goodbye frame {Time.frameCount}.");
                            }
                            if (cutLanded < 0 && pushBegan < 0 && !travelling && Mathf.Abs(requested - 3.1f) <= .025f && Mathf.Abs(applied - 3.1f) <= .07f)
                            {
                                cutLanded = Time.frameCount;
                                try { AssertGoodbyeRoom(leaving, "standing goodbye cut at frame " + cutLanded); }
                                catch (AssertionException ex) { issues.Add("Standing goodbye cut: " + ex.Message); }
                            }
                            // On a gathered humanoid farewell the director can take the pair shot.
                            // The unseated off-room subject gets the standing push 0.05 s after the cut.
                            if (skipSummons && Mathf.Abs(requested - 2.4f) <= .025f)
                            {
                                if (pushBegan < 0)
                                {
                                    pushBegan = Time.frameCount;
                                    // The cue fires on the first frame past 0.05 s, so one long frame can carry it that much later.
                                    if (cutLanded < entryFrame || pushBegan <= cutLanded || elapsed < .045f || elapsed > .05f + longestFrame + .1f)
                                        issues.Add("The 2.4 m push did not follow a completed 3.1 m cut on its scheduled cue.");
                                }
                                if (travelling && applied > 2.47f && applied < 3.03f) pushMoving = Time.frameCount;
                                if (!travelling && pushLanded < 0 && Mathf.Abs(applied - 2.4f) <= .07f)
                                {
                                    pushLanded = Time.frameCount;
                                    landedAt = elapsed;
                                    if (elapsed < 1.85f || elapsed > LatestGoodbyePushLanding(longestFrame) || pushMoving <= pushBegan || pushLanded <= pushMoving)
                                        issues.Add("The two-second goodbye push did not travel between its two physical boom marks on time.");
                                }
                            }
                            if (portraits < at.Length && elapsed >= at[portraits])
                            {
                                // The held portrait is of the landed push, which lands only about 50 ms before its mark:
                                // on the off-room take it waits for the landing, until the latest the landing may come.
                                bool forTheLanding = skipSummons && moments[portraits] == "held";
                                bool waiting = forTheLanding && pushLanded < 0 && elapsed <= LatestGoodbyePushLanding(longestFrame);
                                float due = forTheLanding && pushLanded >= 0 ? Mathf.Max(at[portraits], landedAt) : at[portraits];
                                if (!waiting)
                                {
                                    if (elapsed > due + .35f + longestFrame) issues.Add(moments[portraits] + ": the capture missed its part of the hold.");
                                    Sample(moments[portraits++], true);
                                }
                            }
                        }
                        else if (began >= 0f)
                        {
                            string state = $"{director.CurrentRoomScopeSource}\t{director.CeremonyStagePhase}\t{director.StagedExitRunning}\t{director.IsCeremonyStaged}\t{Hud.IsHeldForReveal}\t{director.IsTravelDipShowing}\t{cameraRig.HasShot}\t{cameraRig.IsTravelling}\t{cameraRig.ControlsEnabled}\t{player.InputEnabled}\t{director.IsPanelOpen}\t{director.IsWeeklyRecapOpen}\t{returnButtonFrame}";
                            if (lastHandoffState != state)
                            {
                                handback.AppendLine(FormattableString.Invariant($"{Time.frameCount}\t{Time.unscaledTime:F5}\t{state}"));
                                lastHandoffState = state;
                            }
                            if (cameraRig.HasShot || director.CurrentRoomScopeSource != EpisodeDirector.RoomScopeSource.FreePlay) return;
                            if (!released) { released = true; Sample("ownership-return", false); }
                            if (director.IsWeeklyRecapOpen && recapFrame < 0)
                            {
                                recapFrame = Time.frameCount;
                                Sample("weekly-recap", false);
                            }
                            if (!director.StagedExitRunning && !director.IsCeremonyStaged && !Hud.IsHeldForReveal
                                && !director.IsTravelDipShowing && !cameraRig.IsTravelling && cameraRig.ControlsEnabled
                                && !director.IsPanelOpen && player.InputEnabled)
                            { free = true; Sample("first-free-camera", false); }
                        }
                    }
                    catch (Exception ex) { observerFailure = ex; }
                };
                yield return SkipReveals();
                // An eviction can open its weekly recap after the walkout. Its own Return button
                // restores input. Keep observing across the click, including the first eligible LateUpdate.
                // Keep the timeout in this iterator so its finally block flushes the frames on failure.
                float deadline = Time.realtimeSinceStartup + EpisodeDirector.GoodbyeSeconds + EpisodeDirector.WalkOutSeconds + 8f;
                while (!free && observerFailure == null && Time.realtimeSinceStartup < deadline)
                {
                    if (recapFrame >= 0 && returnButtonFrame < 0 && director.IsWeeklyRecapOpen)
                    {
                        var back = ButtonWithCaption(WeeklyRecapScreen.ContinueCaption);
                        Assert.That(back.IsInteractable(), Is.True, "The displayed weekly recap offers Return to the house.");
                        returnButtonFrame = Time.frameCount;
                        back.onClick.Invoke();
                    }
                    yield return null;
                }
                Assert.That(observerFailure, Is.Null, observerFailure?.ToString());
                Assert.That(free, Is.True, "The first ordinary stock frame after the exit releases the camera, dip and chrome. Last handback state: " + lastHandoffState);
                Assert.That(portraits, Is.EqualTo(3));
                Assert.That(entryFrame, Is.GreaterThanOrEqualTo(0));
                if (skipSummons)
                    Assert.That(cutLanded >= entryFrame && pushBegan > cutLanded && pushMoving > pushBegan && pushLanded > pushMoving,
                        Is.True, "The off-room take needs an actual cut, moving boom and landing on separate production frames.");
                Assert.That(released && free, Is.True);
                Assert.That(issues, Is.Empty);
            }
            finally
            {
                probe.Foot = null;
                // The stock reads already own their pixels. Write each item independently before
                // releasing the lens so a rejected later frame or a teardown problem cannot discard them.
                var writeFailures = new List<Exception>();
                void WriteRows(string name, StringBuilder lines)
                {
                    try
                    {
                        using (var file = new FileStream(Path.Combine(folder, name), FileMode.CreateNew, FileAccess.Write))
                        using (var log = new StreamWriter(file)) log.Write(lines.ToString());
                    }
                    catch (Exception ex) { writeFailures.Add(new IOException(name, ex)); }
                }
                try
                {
                    WriteRows("handback.tsv", handback);
                    WriteRows("goodbye-boom.tsv", cameraTrace);
                    WriteRows("stock-camera.tsv", rows);
                    foreach (var picture in pictures)
                    {
                        try
                        {
                            var png = picture.image.EncodeToPNG();
                            using (var file = new FileStream(picture.path, FileMode.CreateNew, FileAccess.Write))
                                file.Write(png, 0, png.Length);
                        }
                        catch (Exception ex) { writeFailures.Add(new IOException(picture.path, ex)); }
                    }
                    if (writeFailures.Count > 0) throw new AggregateException("Incomplete stock goodbye evidence.", writeFailures);
                }
                finally
                {
                    foreach (var picture in pictures) Object.Destroy(picture.image);
                    Object.Destroy(host);
                    lens.Dispose();
                }
            }
        }

        private static (int changed, int tested) GoodbyeContribution(Camera camera, int width, int height,
            Color32[] plate, Color32[] masked, Vector3 point, float halfSize)
        {
            var middle = InTheFrame(camera, point);
            var edge = InTheFrame(camera, point + camera.transform.up * halfSize);
            if (!middle.HasValue || !edge.HasValue) return (0, 0);
            int radius = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(edge.Value.y - middle.Value.y) * height));
            int x = Mathf.RoundToInt(middle.Value.x * (width - 1)), y = Mathf.RoundToInt(middle.Value.y * (height - 1));
            if (x - radius < 0 || x + radius >= width || y - radius < 0 || y + radius >= height) return (0, 0);
            int changed = 0, tested = 0;
            for (int row = y - radius; row <= y + radius; row++)
                for (int column = x - radius; column <= x + radius; column++)
                {
                    int i = row * width + column;
                    var a = plate[i]; var b = masked[i]; tested++;
                    if (Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b) >= 24) changed++;
                }
            return (changed, tested);
        }

        /// <summary>
        /// The latest goodbye second at which the observer may see the 2 s push land at 2.4 m. The rig
        /// counts the push from the frame before its cue (before 0.05 s), so it lands by 2.05 s plus one
        /// frame; the observer and the rig both read in LateUpdate, in no fixed order, so the landing can
        /// be seen one frame later still. The 0.3 s is the old window's room for the boom to settle.
        /// </summary>
        private static float LatestGoodbyePushLanding(float longestFrame) => 2.05f + .3f + 2f * longestFrame;

        private static string GoodbyeTakeFolder(bool skipped)
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--gamesim-save-root");
            string run = index >= 0 && index + 1 < args.Length ? args[index + 1]
                : Path.Combine(Application.temporaryCachePath, "gamesim-goodbye-" + Guid.NewGuid().ToString("N"));
            string name = skipped ? "staged-goodbye-skipped" : "staged-goodbye-normal";
            string folder = Path.GetFullPath(Path.Combine(run, name));
            // A rerun against the same run root keeps the earlier take and writes beside it.
            for (int take = 2; Directory.Exists(folder); take++)
                folder = Path.GetFullPath(Path.Combine(run, name + "-" + take));
            Directory.CreateDirectory(folder);
            Debug.Log("[Gamesim] stock goodbye camera evidence -> " + folder);
            return folder;
        }

        // ---------------------------------------------------------------- the living room's door (M23)

        /// <summary>
        /// The prototype's planter, if it still stands in the living room's exit doorway: the scene
        /// edit (HouseLivingGallery.StrikeTheDoorway) and the rebake have not run on this copy.
        /// </summary>
        private static Transform PlanterInTheLivingDoorway() => SceneComponents<Transform>().FirstOrDefault(node =>
            node.gameObject.activeInHierarchy && (node.name == "Planter" || node.name == "Foliage")
            && Mathf.Abs(node.position.x + 12f) < 0.6f && Mathf.Abs(node.position.z + 8f) < 0.6f);

        /// <summary>
        /// The probe of the living room's exit doorway (PACK8-PASS-PLAN C2, M23), run on the D:
        /// copy before the door is switched on. It measures the floor as the walk out would use it
        /// and logs every measure: at each mark, the NavMesh's height, the room, the capsule's
        /// clearance, what a body there would overlap and the nearest edge; how far west the mesh
        /// runs; both red chairs' routes to the vestibule, sampled every 0.1 m for a rise, the
        /// planter's bed or a collider; and what stands in the leaves' swing. It asserts the marks
        /// on the living room's floor, the mesh to x -13.05, both routes complete and the vestibule
        /// flat - once the prototype planter is struck and the house rebaked; until then it says so.
        /// </summary>
        [UnityTest]
        public IEnumerator StagedExit_TheLivingRoomsDoorwayIsClearAndFlat()
        {
            yield return InstallStagedSeason(52, AtEviction);
            var scene = director.gameObject.scene;
            Assert.That(HouseRoomQuery.TryCreate(scene, out var rooms, out var why), Is.True, why);
            var filter = new NavMeshQueryFilter { agentTypeID = player.Agent.agentTypeID, areaMask = player.Agent.areaMask };
            int solid = (1 << 0) | (1 << HouseLayers.Furniture);
            var physics = scene.GetPhysicsScene();
            var overlaps = new Collider[8];
            Physics.SyncTransforms();
            var report = new List<string> { "Living room exit door probe:" };
            var failures = new List<string>();
            string Touching(Vector3 feet)
            {
                int count = physics.OverlapCapsule(feet + Vector3.up * 0.35f, feet + Vector3.up * (1.9f - 0.35f), 0.35f, overlaps, solid, QueryTriggerInteraction.Ignore);
                return count == 0 ? "nothing" : string.Join(", ", overlaps.Take(count).Select(hit => hit.name + " (layer " + hit.gameObject.layer + ")"));
            }

            var marks = new[]
            {
                new Vector3(-13.15f, 0f, -8.5f), new Vector3(-13.15f, 0f, -9.0f), new Vector3(-11.2f, 0f, -9.0f),
                new Vector3(-11.2f, 0f, -8.5f), new Vector3(-12.6f, 0f, -8.5f),
            };
            foreach (var mark in marks)
            {
                string line = "  " + mark.ToString("F2") + ":";
                if (NavMesh.SamplePosition(mark, out var onMesh, 0.25f, filter)) line += " NavMesh y " + onMesh.position.y.ToString("F3");
                else { line += " no NavMesh within 0.25 m"; failures.Add(mark.ToString("F2") + " has no NavMesh within 0.25 m"); }
                if (rooms.TrySampleFloor(mark, 0.35f, filter, 0.25f, out var floor, out var room))
                {
                    bool clear = rooms.HasCapsuleClearance(floor, 0.35f, 1.9f, player.transform);
                    line += ", floor " + floor.ToString("F3") + " in the " + room + " room, clearance " + clear
                        + (clear ? "" : " (" + rooms.LastFailure + ")") + ", overlapping " + Touching(floor);
                    if (NavMesh.FindClosestEdge(floor, out var edge, filter)) line += ", nearest edge " + edge.distance.ToString("F2") + " m away";
                    if (room != "Living") failures.Add(mark.ToString("F2") + " is in the " + room + " room");
                }
                else
                {
                    line += " no floor (" + rooms.LastFailure + ")";
                    failures.Add(mark.ToString("F2") + " has no floor to stand on");
                }
                report.Add(line);
            }

            // How far west the mesh runs along the doorway's middle.
            if (NavMesh.SamplePosition(new Vector3(-11.2f, 0f, -8.5f), out var inside, 0.5f, filter))
            {
                NavMesh.Raycast(inside.position, new Vector3(-14.2f, inside.position.y, -8.5f), out var westEdge, filter);
                report.Add("  the mesh's west edge along z -8.5: x " + westEdge.position.x.ToString("F2"));
                if (westEdge.position.x > -13.05f) failures.Add("the mesh stops at x " + westEdge.position.x.ToString("F2") + ", east of -13.05");
            }
            else
            {
                report.Add("  the mesh's west edge: not measured, (-11.20, -8.50) is off the NavMesh");
                failures.Add("the floor in front of the door is off the NavMesh");
            }

            // Both red chairs' routes to the vestibule.
            var vestibuleMark = new Vector3(-13.15f, 0f, -8.5f);
            bool vestibuleOnMesh = NavMesh.SamplePosition(vestibuleMark, out var vestibule, 0.25f, filter);
            if (vestibuleOnMesh && vestibule.position.y >= 0.06f)
                failures.Add("the vestibule's floor stands " + vestibule.position.y.ToString("F3") + " m up: a body there would float");
            var planterBed = new Bounds(new Vector3(-12f, 0.3f, -8f), new Vector3(1f, 0.6f, 1f));
            foreach (var seat in CeremonySeating.Anchors(scene, CeremonySeating.HotSeat))
            {
                string from = "  from " + seat.VenueId + " " + seat.Slot + "'s approach";
                if (!vestibuleOnMesh || !NavMesh.SamplePosition(seat.Approach, out var start, 0.5f, filter))
                { report.Add(from + ": no route measured"); failures.Add(from.Trim() + " has no route measured"); continue; }
                var path = new NavMeshPath();
                NavMesh.CalculatePath(start.position, vestibule.position, filter, path);
                var corners = path.corners;
                float length = 0f;
                var flags = new List<string>();
                for (int i = 1; i < corners.Length; i++)
                {
                    float leg = Vector3.Distance(corners[i - 1], corners[i]);
                    length += leg;
                    for (float along = 0f; along <= leg; along += 0.1f)
                    {
                        var at = Vector3.Lerp(corners[i - 1], corners[i], leg > 0f ? along / leg : 0f);
                        if (NavMesh.SamplePosition(at, out var under, 0.3f, filter) && under.position.y > 0.06f) flags.Add("rises to y " + under.position.y.ToString("F2") + " at " + at.ToString("F2"));
                        if (planterBed.Contains(new Vector3(at.x, 0.3f, at.z))) flags.Add("crosses the planter's bed at " + at.ToString("F2"));
                        string touching = Touching(at);
                        if (touching != "nothing") flags.Add("touches " + touching + " at " + at.ToString("F2"));
                    }
                }
                report.Add(from + " " + start.position.ToString("F2") + ": " + path.status + ", " + length.ToString("F2") + " m, corners "
                    + string.Join(" ", corners.Select(corner => corner.ToString("F2"))));
                foreach (var flag in flags.Distinct().Take(12)) report.Add("    " + flag);
                if (path.status != NavMeshPathStatus.PathComplete) failures.Add(from.Trim() + " to the vestibule is " + path.status);
            }

            // What stands in the leaves' swing, a quarter-disc in front of each hinge.
            var living = DoorLayout.Living;
            float sweepX = (living.FacadeFrontX + living.SweepFrontX) * 0.5f, sweepDepth = living.SweepFrontX - living.FacadeFrontX;
            foreach (var (fromZ, toZ) in new[] { (living.ApertureMinZ, living.DoorCentre.z), (living.DoorCentre.z, living.ApertureMaxZ) })
            {
                var swing = new Bounds(new Vector3(sweepX, 1.3f, (fromZ + toZ) * 0.5f), new Vector3(sweepDepth, 2.6f, toZ - fromZ));
                var props = SceneComponents<Renderer>().Where(prop => prop.enabled && prop.gameObject.activeInHierarchy
                    && prop.bounds.size.x <= 3f && prop.bounds.size.z <= 3f && swing.Intersects(prop.bounds)).Select(prop => prop.name).Distinct().ToList();
                report.Add("  in the leaves' swing from z " + fromZ.ToString("F2") + " to " + toZ.ToString("F2") + ": "
                    + (props.Count == 0 ? "nothing drawn" : string.Join(", ", props)));
            }
            string measured = string.Join("\n", report);
            Debug.Log(measured);

            var planter = PlanterInTheLivingDoorway();
            if (planter != null)
                Assert.Ignore("The living room's doorway still has the prototype's " + planter.name + " in it: HouseLivingGallery.BuildFromCommandLine strikes it"
                    + " and rebakes (PACK8-PASS-PLAN C2), and the door waits for that.\n" + measured
                    + (failures.Count > 0 ? "\nIt would fail on: " + string.Join("; ", failures) : ""));
            Assert.That(failures, Is.Empty, measured);
        }

        /// <summary>
        /// The living room's door, switched on (MOCKUP-PASS-PLAN M23): it goes up closed at the
        /// living room's origin for the goodbye, with the props in its doorway hidden; the evicted
        /// walk to it, through it into the vestibule, and it shuts behind them while their body is
        /// still in the house; their body goes behind it, and the props come back with the strike.
        /// </summary>
        [UnityTest]
        public IEnumerator StagedExit_TheLivingRoomsDoorLetsThemOut()
        {
            yield return InstallStagedSeason(52, AtEviction);
            director.WalkOutThrough = EpisodeDirector.WalkOutDoor.Living;
            yield return PlayUntilTheCeremony();
            Assert.That(director.IsCeremonyStaged, Is.True, "The eviction is staged in the living room.");
            string evicted = director.DepartingId;
            Assert.That(evicted, Is.Not.Null, "A houseguest is evicted.");
            director.SkipCeremonySummons();
            yield return WaitFor(() => director.CeremonyStagePhase == EpisodeDirector.CeremonyStageStep.Playing, 3f, "the vote plays");
            yield return SkipVoteToGoodbye();
            if (director.WalkOutDoorInUse != EpisodeDirector.WalkOutDoor.Living)
            {
                var planter = PlanterInTheLivingDoorway();
                if (planter != null)
                    Assert.Ignore("The living room's door waits for its doorway to be cleared and rebaked: the prototype's " + planter.name
                        + " stands in it (StagedExit_TheLivingRoomsDoorwayIsClearAndFlat measures it).");
                Assert.Fail("The living room's door was refused although its doorway is clear; the log names why.");
            }
            var root = SceneRoot(OpeningDoorSet.RootName);
            Assert.That(root, Is.Not.Null, "The living room's door is up for the goodbye,");
            Assert.That(Vector3.Distance(root.transform.position, DoorLayout.Living.Origin), Is.LessThan(0.001f), "at the living room's origin,");
            var door = root.GetComponent<OpeningDoorSet>();
            Assert.That(door.Layout.Name, Is.EqualTo(DoorLayout.Living.Name));
            Assert.That(door.IsOpen || door.Openness > 0f, Is.False, "closed.");
            var hidden = director.WalkOutDoorHides.ToList();
            foreach (var prop in hidden) Assert.That(prop.enabled, Is.False, prop.name + " is hidden while the door stands in its place.");

            yield return WaitFor(() => director.WalkingOutId != null, EpisodeDirector.GoodbyeSeconds + 2f, "the goodbye gives way to the walk out");
            var body = HouseguestBody(evicted);
            float deepest = float.MaxValue, widest = 0f;
            bool shut = false, inTheHouseWhenShut = false;
            float by = Time.realtimeSinceStartup + EpisodeDirector.WalkOutSeconds + 2f;
            while (director.WalkingOutId != null && Time.realtimeSinceStartup < by)
            {
                deepest = Mathf.Min(deepest, body.transform.position.x);
                if (door != null)
                {
                    widest = Mathf.Max(widest, door.Openness);
                    if (!shut && widest >= 0.35f && !door.IsOpen && door.Openness <= 0.01f)
                    {
                        shut = true;
                        inTheHouseWhenShut = body.gameObject.activeInHierarchy;
                    }
                }
                yield return null;
            }
            Assert.That(director.WalkingOutId, Is.Null, "The walk out ends");
            Assert.That(widest, Is.GreaterThanOrEqualTo(0.35f), "The door opened for them,");
            Assert.That(deepest, Is.LessThanOrEqualTo(-12.85f), "they walked through it into the vestibule,");
            Assert.That(shut, Is.True, "and it shut behind them");
            Assert.That(inTheHouseWhenShut, Is.True, "before their body went.");
            Assert.That(body.gameObject.activeInHierarchy, Is.False, "They are gone.");
            yield return null;
            Assert.That(DoorSetUp(), Is.False, "The door is struck,");
            foreach (var prop in hidden) Assert.That(prop != null && prop.enabled, Is.True, "and what it hid is back.");
            Assert.That(director.WalkOutDoorHides, Is.Empty);
            yield return WaitFor(() => !director.IsCeremonyStaged, 3f, "the house gets up");
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null);
        }

        /// <summary>
        /// The living room's door asked for with its doorway taken: the probe at the goodbye refuses
        /// it, nothing goes up in the living room, and the walk goes by the yard's front door.
        /// </summary>
        [UnityTest]
        public IEnumerator StagedExit_TheLivingRoomsDoorFallsBackToTheYards()
        {
            yield return InstallStagedSeason(52, AtEviction);
            director.WalkOutThrough = EpisodeDirector.WalkOutDoor.Living;
            // Somebody's worth of furniture standing in the vestibule.
            var blocker = new GameObject("Something in the doorway");
            SceneManager.MoveGameObjectToScene(blocker, director.gameObject.scene);
            blocker.layer = HouseLayers.Furniture;
            blocker.transform.position = new Vector3(-13.15f, 0f, -8.5f);
            var capsule = blocker.AddComponent<CapsuleCollider>();
            capsule.radius = 0.35f; capsule.height = 1.9f; capsule.center = Vector3.up * 0.95f;
            Physics.SyncTransforms();
            // Once the doorway is cleared and rebaked, the refusal is the blocker's. Until then the
            // vestibule's raised floor refuses the door before the blocker is asked, and the probe
            // (StagedExit_TheLivingRoomsDoorwayIsClearAndFlat) is the test that reports it.
            if (PlanterInTheLivingDoorway() == null)
                LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("the living room's door is refused - no room to stand at "));
            yield return PlayUntilTheCeremony();
            Assert.That(director.IsCeremonyStaged, Is.True, "The eviction is staged in the living room.");
            director.SkipCeremonySummons();
            yield return WaitFor(() => director.CeremonyStagePhase == EpisodeDirector.CeremonyStageStep.Playing, 3f, "the vote plays");
            yield return SkipVoteToGoodbye();
            Assert.That(director.WalkOutDoorInUse, Is.EqualTo(EpisodeDirector.WalkOutDoor.Yard), "The living room's door is refused with its doorway taken,");
            Assert.That(DoorSetUp(), Is.False, "and nothing goes up in the living room.");
            Object.Destroy(blocker);

            yield return WaitFor(() => director.WalkingOutId != null, EpisodeDirector.GoodbyeSeconds + 2f, "the goodbye gives way to the walk out");
            Assert.That(director.WalkOutIsStaged, Is.True, "The walk is the stage's,");
            yield return WaitFor(DoorSetUp, EpisodeDirector.WalkOutSeconds, "the yard's door goes up at the dip");
            Assert.That(SceneRoot(OpeningDoorSet.RootName).transform.position, Is.EqualTo(DoorLayout.Yard.Origin), "at the yard's front door.");
            Assert.That(director.WalkOutDoorInUse, Is.EqualTo(EpisodeDirector.WalkOutDoor.Yard));
            director.SkipWalkOut();
            yield return WaitFor(() => !director.IsCeremonyStaged, 3f, "the house gets up");
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null);
        }

        /// <summary>An unstaged walk out goes by the yard's front door whatever the switch says: the living room's is the staged exit's alone.</summary>
        [UnityTest]
        public IEnumerator StagedExit_AnUnstagedWalkOutGoesByTheYardWhateverTheSwitch()
        {
            director.WalkOutsInBatchRuns = true;
            director.WalkOutThrough = EpisodeDirector.WalkOutDoor.Living;
            yield return PlayUntilAHouseguestLeaves();
            string leaving = director.DepartingId;
            SceneComponents<VoteReveal>().Single().Cancel();
            yield return Frames(3);
            Assert.That(director.WalkingOutId, Is.EqualTo(leaving), "The evicted walk out,");
            Assert.That(director.WalkOutIsStaged, Is.False, "unstaged,");
            Assert.That(director.WalkOutDoorInUse, Is.EqualTo(EpisodeDirector.WalkOutDoor.Yard), "by the yard's door,");
            yield return WaitFor(DoorSetUp, 5f, "which goes up for them");
            Assert.That(SceneRoot(OpeningDoorSet.RootName).transform.position, Is.EqualTo(DoorLayout.Yard.Origin), "in the yard.");
            director.SkipWalkOut();
            yield return null;
            Assert.That(DoorSetUp(), Is.False);
        }
    }
}
