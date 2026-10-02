using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The evicted houseguest's walk out through the opening's front door, and the jury back in the
    /// living room on finale night.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>Plays until an eviction's reveal is up with a houseguest - not the player - leaving.</summary>
        private IEnumerator PlayUntilAHouseguestLeaves()
        {
            var reveal = SceneComponents<VoteReveal>().Single();
            for (int attempt = 0; attempt < 6; attempt++)
            {
                yield return PlayUntilTheVoteReveal(reveal);
                if (director.DepartingId != null) break;
                // The player was the one evicted: they have no walk out. Play on to the next.
                reveal.Cancel();
                yield return Frames(2);
            }
            Assert.That(director.DepartingId, Is.Not.Null, "A houseguest was evicted.");
        }

        private bool DoorSetUp() => SceneRoot(OpeningDoorSet.RootName) != null;

        private GameObject SceneRoot(string name) =>
            director.gameObject.scene.GetRootGameObjects().FirstOrDefault(root => root.name == name);

        [UnityTest]
        public IEnumerator WalkOut_TheEvictedWalksOutThroughTheFrontDoor()
        {
            director.WalkOutsInBatchRuns = true;
            yield return PlayUntilAHouseguestLeaves();
            string leaving = director.DepartingId;
            var body = SceneComponents<HouseNpc>().Single(npc => npc.Id == leaving);
            Assert.That(director.WalkingOutId, Is.Null, "Nobody walks while the reveal is up.");
            SceneComponents<VoteReveal>().Single().Cancel();
            yield return Frames(3);
            Assert.That(director.WalkingOutId, Is.EqualTo(leaving), "Once the cards are done, the evicted walks out,");
            Assert.That(body.gameObject.activeInHierarchy, Is.True, "still in the house while they go.");

            float began = Time.realtimeSinceStartup;

            // The strip waits for the door: played as the walk began, "glares at you from the
            // doorway" ran over a body crossing the yard (UI-UX-PASS-PLAN W0, sweep-show 10).
            var sting = SceneComponents<CeremonySting>().Single();
            string StingHeadline() => sting.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(text => text.name == "Sting headline")?.text;
            bool goodbyeBeforeTheDoor = false;
            float by = Time.realtimeSinceStartup + 40f;
            while (!director.WalkOutAtTheDoor && director.WalkingOutId != null && Time.realtimeSinceStartup < by)
            {
                goodbyeBeforeTheDoor |= sting.IsPlaying && StingHeadline() == "GOODBYE";
                yield return null;
            }
            Assert.That(director.WalkOutAtTheDoor, Is.True, "They reach the front door, and it opens for them.");
            Assert.That(goodbyeBeforeTheDoor, Is.False, "Nothing said goodbye while they crossed the yard.");
            Assert.That(DoorSetUp(), Is.True, "The opening's door is up for them.");
            Assert.That(Flat(body.transform.position, new Vector3(-1.6f, 0f, 14.1f)), Is.LessThan(1.5f), "They walked to it across the yard.");
            Assert.That(sting.IsPlaying && StingHeadline() == "GOODBYE", Is.True, "The strip says goodbye as the door opens for them,");
            var strip = sting.GetComponentsInChildren<TMP_Text>(true).Select(text => text.text).ToList();
            string firstName = director.Snapshot.Find(leaving).name.Split(' ')[0];
            Assert.That(strip, Has.Some.EqualTo(EpisodeDirector.GoodbyeLine(director.Snapshot, leaving)), "with the doorway's words, where the body is,");
            Assert.That(strip.Any(text => text.StartsWith(firstName + " ") && text.EndsWith("They'll be waiting in the jury house.")), Is.True,
                "what they do at the door, and where they are going.");
            // And the sting's frame has the door in it (UI-UX-PASS-PLAN W0): the doorway's middle at
            // head height, through the projection the frame is drawn with. The set's root stands at
            // its layout's origin - the world's, for the yard - so the doorway is what is projected.
            var door = SceneRoot(OpeningDoorSet.RootName).GetComponent<OpeningDoorSet>();
            var doorway = door.Layout.DoorCentre + Vector3.up * 1.3f;
            var doorSeen = InTheFrame(cameraRig.ViewCamera, doorway);
            Assert.That(InsideTheFrame(doorSeen), Is.True, "The door is in the frame the goodbye plays on: the doorway at " + doorway.ToString("F2") + " -> "
                + (doorSeen.HasValue ? doorSeen.Value.ToString("F2") : "behind the lens") + ".");

            // Through the door and off the deck behind the facade, on their own feet.
            float widest = 0f;
            var last = body.transform.position;
            while (director.WalkingOutId != null && Time.realtimeSinceStartup - began < 60f)
            {
                if (door != null) widest = Mathf.Max(widest, door.Openness);
                last = body.transform.position;
                yield return null;
            }
            Assert.That(director.WalkingOutId, Is.Null, "The walk out ends");
            Assert.That(Time.realtimeSinceStartup - began, Is.LessThan(EpisodeDirector.WalkOutSeconds),
                "when they arrive, not when the house gives up on them.");
            Assert.That(widest, Is.GreaterThanOrEqualTo(0.35f), "The door swung open for them,");
            Assert.That(Flat(last, new Vector3(-5.3f, 0f, 13.8f)), Is.LessThan(1.5f), "and they walked through it to the deck behind the facade,");
            Assert.That(body.gameObject.activeInHierarchy, Is.False, "and they are gone.");
            // Destroyed at the end of the frame it came down in.
            yield return null;
            Assert.That(DoorSetUp(), Is.False, "The door comes down with them.");
            Assert.That(director.DepartingId, Is.Null);
        }

        /// <summary>
        /// A press skips the walk out, and they go as they always did - still saying goodbye. The
        /// line waits for the door now (UI-UX-PASS-PLAN W0), so a walk skipped short of it said
        /// nothing at all, a juror's "They'll be waiting in the jury house." included; it says its
        /// line as it goes, worded for a body that never reached the door.
        /// </summary>
        [UnityTest]
        public IEnumerator WalkOut_APressLetsThemGo()
        {
            director.WalkOutsInBatchRuns = true;
            yield return PlayUntilAHouseguestLeaves();
            string leaving = director.DepartingId;
            var body = SceneComponents<HouseNpc>().Single(npc => npc.Id == leaving);
            SceneComponents<VoteReveal>().Single().Cancel();
            yield return Frames(3);
            Assert.That(director.WalkingOutId, Is.EqualTo(leaving));
            // On their way - the walk's first route taken - and past the guard that keeps the press
            // that closed the last card from skipping this too.
            var motion = body.GetComponent<HouseNpcMotion>();
            yield return WaitFor(() => motion.LeaseId != null && motion.LeaseId.StartsWith("departure:"), 5f, "The walk out takes its first route.");
            float settle = Time.realtimeSinceStartup + 0.5f;
            while (Time.realtimeSinceStartup < settle) yield return null;
            Assert.That(director.WalkOutAtTheDoor, Is.False, "They are still short of the door when the press comes.");
            if (testKeyboard == null) testKeyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Enter));
            yield return null;
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            yield return Frames(2);
            Assert.That(director.WalkingOutId, Is.Null, "A press skips the walk out,");
            Assert.That(body.gameObject.activeInHierarchy, Is.False, "and they go as they always did.");
            Assert.That(DoorSetUp(), Is.False);

            var sting = SceneComponents<CeremonySting>().Single();
            var strip = sting.GetComponentsInChildren<TMP_Text>(true).Select(text => text.text).ToList();
            Assert.That(sting.IsPlaying, Is.True, "They still say goodbye as they go:");
            Assert.That(strip, Has.Some.EqualTo("GOODBYE"), "the strip says goodbye,");
            Assert.That(strip, Has.Some.EqualTo(EpisodeDirector.GoodbyeLine(director.Snapshot, leaving, EpisodeDirector.GoodbyeMoment.Standing)),
                "with the words for a body that never reached the door.");
            if (director.Snapshot.Find(leaving).status == ContestantStatus.Jury)
                Assert.That(strip.Any(text => text.EndsWith("They'll be waiting in the jury house.")), Is.True, "A juror says where they are going.");
        }

        [UnityTest]
        public IEnumerator WalkOut_ThePressThatClosesTheLastCardDoesNotSkipIt()
        {
            director.WalkOutsInBatchRuns = true;
            yield return PlayUntilAHouseguestLeaves();
            string leaving = director.DepartingId;
            var reveal = SceneComponents<VoteReveal>().Single();
            if (testKeyboard == null) testKeyboard = InputSystem.AddDevice<Keyboard>();
            // The card goes and presses land on the walk out's first frames: the press that closed the
            // card, as far as the walk can tell, whichever of the two reads the keyboard first.
            reveal.Cancel();
            for (int press = 0; press < 3; press++)
            {
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Enter));
                yield return null;
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
                yield return null;
            }
            Assert.That(director.WalkingOutId, Is.EqualTo(leaving), "The press that closed the card does not skip the walk out as well,");
            float settle = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < settle) yield return null;
            Assert.That(director.WalkingOutId, Is.EqualTo(leaving), "and they are still walking.");
        }

        [UnityTest]
        public IEnumerator WalkOut_TheWeekGoesOnAroundTheirWalk()
        {
            director.WalkOutsInBatchRuns = true;
            yield return PlayUntilAHouseguestLeaves();
            string leaving = director.DepartingId;
            var body = SceneComponents<HouseNpc>().Single(npc => npc.Id == leaving);
            SceneComponents<VoteReveal>().Single().Cancel();
            yield return Frames(3);
            Assert.That(director.WalkingOutId, Is.EqualTo(leaving));
            float began = Time.realtimeSinceStartup;

            // Straight into the social week: the house's own world runs around them as they go.
            for (int step = 0; step < 6 && (director.Snapshot.phase != EpisodePhase.Social || director.Snapshot.pendingDiary != null); step++)
            {
                var result = director.Submit(NextCommand(director.Snapshot));
                Assert.That(result.accepted, Is.True, result.reason);
                yield return null;
            }
            Assert.That(NpcSocialState.IsEligible(director.Snapshot), Is.True, "The social week is under way.");
            float social = Time.realtimeSinceStartup + 1.5f;
            while (Time.realtimeSinceStartup < social && director.WalkingOutId != null) yield return null;
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null, "The house's world runs beside the walk out,");
            Assert.That(director.WalkingOutId, Is.EqualTo(leaving), "and they are still on their way.");

            // A panel pauses the house, and a decision commits under it: neither stops the one walking out.
            var motion = body.GetComponent<HouseNpcMotion>();
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            yield return null;
            Assert.That(motion.State, Is.Not.EqualTo(HouseNpcMotionState.Paused), "The panel pauses the house, not the one walking out of it,");
            var onward = director.Submit(NextCommand(director.Snapshot));
            Assert.That(onward.accepted, Is.True, onward.reason);
            yield return null;
            Assert.That(motion.State, Is.Not.EqualTo(HouseNpcMotionState.Paused), "nor does a decision made under it.");
            yield return WaitFor(() => director.WalkingOutId == null, 45f, "The walk out ends.");
            Assert.That(Time.realtimeSinceStartup - began, Is.LessThan(EpisodeDirector.WalkOutSeconds),
                "They arrived, rather than the house giving up on them.");
            Assert.That(body.gameObject.activeInHierarchy, Is.False);
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null);
        }

        /// <summary>
        /// A whole season with its walk outs on. Who leaves at the final eviction is the house's to
        /// decide, and its world ticks in real time, so that eviction is checked here only when a
        /// houseguest leaves; the final-two card's test pins it.
        /// </summary>
        [UnityTest]
        public IEnumerator WalkOut_EveryWeeklyEvictionWalksOutAndTheRecapFollows()
        {
            director.WalkOutsInBatchRuns = true;
            int walked = 0;
            for (int count = 0; director.Snapshot.phase != EpisodePhase.Finished && count < 200; count++)
            {
                yield return ContinueCompetitionResults(byKeyboard: false);
                var before = director.Snapshot;
                var result = director.Submit(NextCommand(before));
                Assert.That(result.accepted, Is.True, before.phase + ": " + result.reason);
                yield return null;
                string leaving = director.DepartingId;
                if (leaving == null) continue;
                // The cards about it, skipped as a player skips them.
                yield return SkipReveals();
                foreach (var card in SceneComponents<CeremonyTakeover>()) card.Cancel();
                foreach (var sting in SceneComponents<CeremonySting>()) sting.Cancel();
                yield return Frames(3);
                var body = SceneComponents<HouseNpc>().Single(npc => npc.Id == leaving);
                if (before.phase == EpisodePhase.FinalEviction)
                {
                    Assert.That(director.WalkingOutId, Is.Null, "The final eviction opens finale night: nobody walks out of the door,");
                    Assert.That(body.gameObject.activeInHierarchy, Is.True, "the new juror joins the jury");
                    Assert.That(HouseRoomQuery.TryCreate(director.gameObject.scene, out var rooms, out var why), Is.True, why);
                    Assert.That(rooms.TryLocate(body.transform.position, 0.35f, out var room) ? room : "nowhere", Is.EqualTo("Living"),
                        "in the living room.");
                }
                else
                {
                    Assert.That(director.WalkingOutId, Is.EqualTo(leaving), before.phase + " in week " + before.week + ": the evicted walks out.");
                    Assert.That(director.IsWeeklyRecapOpen, Is.False, "The week's recap waits for the walk out,");
                    walked++;
                    director.SkipWalkOut();
                    yield return null;
                    Assert.That(body.gameObject.activeInHierarchy, Is.False);
                    for (int wait = 0; wait < 5 && !director.IsWeeklyRecapOpen; wait++) yield return null;
                    Assert.That(director.IsWeeklyRecapOpen, Is.True, "and follows it.");
                    director.ClosePanels();
                    yield return null;
                }
            }
            Assert.That(director.Snapshot.phase, Is.EqualTo(EpisodePhase.Finished));
            // Three weekly evictions, and the player can be only one of them.
            Assert.That(walked, Is.GreaterThanOrEqualTo(2), "The weekly evictions walked out.");
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null, "The house's world came through every walk out.");
        }

        [UnityTest]
        public IEnumerator WalkOut_ACompetitionStartedMidWalkLetsThemGo()
        {
            director.WalkOutsInBatchRuns = true;
            yield return PlayUntilAHouseguestLeaves();
            string leaving = director.DepartingId;
            var body = SceneComponents<HouseNpc>().Single(npc => npc.Id == leaving);
            SceneComponents<VoteReveal>().Single().Cancel();
            yield return Frames(3);
            Assert.That(director.WalkingOutId, Is.EqualTo(leaving));
            // On to the next competition while they walk: the week does not wait for them.
            for (int step = 0; step < 8 && !EpisodeEngine.IsCompetition(director.Snapshot.phase); step++)
            {
                var result = director.Submit(NextCommand(director.Snapshot));
                Assert.That(result.accepted, Is.True, result.reason);
                yield return null;
            }
            Assert.That(EpisodeEngine.IsCompetition(director.Snapshot.phase), Is.True, "The next competition is up.");
            Assert.That(director.WalkingOutId, Is.EqualTo(leaving), "They are still walking out as the week moves on,");
            Assert.That(body.gameObject.activeInHierarchy, Is.True, "still in the house: the commits since did not take them.");
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            Assert.That(director.WalkingOutId, Is.EqualTo(leaving), "Still walking while the briefing is read,");
            ButtonWithCaption("Practice this competition").onClick.Invoke();
            yield return null;
            Assert.That(director.IsChallengeActive, Is.True, "and when the competition starts");
            Assert.That(director.WalkingOutId, Is.Null, "the walk out gives way to it: the competition takes the yard,");
            Assert.That(body.gameObject.activeInHierarchy, Is.False, "and the evicted go as they always went.");
            Assert.That(DoorSetUp(), Is.False);
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null);
        }

        [UnityTest]
        public IEnumerator WalkOut_ABodyThatCannotWalkIsLetGoAndTheHouseCarriesOn()
        {
            director.WalkOutsInBatchRuns = true;
            yield return PlayUntilAHouseguestLeaves();
            string leaving = director.DepartingId;
            var body = SceneComponents<HouseNpc>().Single(npc => npc.Id == leaving);
            // Somewhere no room holds: their body cannot take its navigation back for the walk.
            body.transform.position = new Vector3(60f, 0f, 60f);
            Physics.SyncTransforms();
            SceneComponents<VoteReveal>().Single().Cancel();
            yield return WaitFor(() => director.WalkingOutId == null, 10f, "The walk out gives up on them.");
            Assert.That(body.gameObject.activeInHierarchy, Is.False, "They go as they always did,");
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null, "and the house's own world carries on without them.");
            Assert.That(DoorSetUp(), Is.False, "No door went up for a walk that never started.");
        }

        [UnityTest]
        public IEnumerator WalkOut_ABatchRunWalksNobodyOutUnlessAsked()
        {
            Assert.That(director.WalkOutsInBatchRuns, Is.False, "A batch run does not walk anybody out unless a test asks, as the opening does not stage.");
            yield return PlayUntilAHouseguestLeaves();
            string leaving = director.DepartingId;
            var body = SceneComponents<HouseNpc>().Single(npc => npc.Id == leaving);
            SceneComponents<VoteReveal>().Single().Cancel();
            yield return Frames(3);
            Assert.That(director.WalkingOutId, Is.Null);
            Assert.That(body.gameObject.activeInHierarchy, Is.False, "They go when the card does, as before.");
        }

        [UnityTest]
        public IEnumerator Jury_FinaleNightHasTheJuryInTheLivingRoom()
        {
            HoldTheHouseForTheFixture();
            yield return InstallFinaleFixture(true);
            yield return Frames(2);
            var state = director.Snapshot;
            var jurors = state.contestants.Where(c => !c.isPlayer && c.status == ContestantStatus.Jury).Select(c => c.id).ToList();
            Assert.That(jurors, Is.Not.Empty);
            var bodies = SceneComponents<HouseNpc>().Where(npc => jurors.Contains(npc.Id)).ToList();
            Assert.That(bodies.Count(body => body.gameObject.activeInHierarchy), Is.EqualTo(jurors.Count), "Every juror is back in the house for the finale.");
            Assert.That(HouseRoomQuery.TryCreate(director.gameObject.scene, out var rooms, out var why), Is.True, why);
            var living = director.gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HouseRoomMarker>(true))
                .First(marker => marker.RoomName == "Living").transform.position;
            foreach (var body in bodies)
            {
                Assert.That(rooms.TryLocate(body.transform.position, 0.35f, out var room) ? room : "nowhere", Is.EqualTo("Living"), body.Id + " stands in the living room,");
                Assert.That(rooms.HasCapsuleClearance(body.transform.position, 0.35f, 1.9f, body.transform), Is.True, "clear of its furniture,");
                var toMiddle = living - body.transform.position; toMiddle.y = 0f;
                Assert.That(Vector3.Dot(body.transform.forward, toMiddle.normalized), Is.GreaterThan(0.7f), "facing its middle.");
            }
            var everyone = SceneComponents<HouseNpc>().Where(npc => npc.gameObject.activeInHierarchy).Select(npc => npc.transform.position)
                .Append(player.transform.position).ToList();
            for (int i = 0; i < everyone.Count; i++)
                for (int j = i + 1; j < everyone.Count; j++)
                    Assert.That(Flat(everyone[i], everyone[j]), Is.GreaterThanOrEqualTo(0.9f), "Nobody stands on anybody.");
        }

        [UnityTest]
        public IEnumerator Jury_TheJuryIsNotInTheHouseBeforeFinaleNight()
        {
            HoldTheHouseForTheFixture();
            yield return InstallDiaryFixture(state => state.phase == EpisodePhase.Social && state.contestants.Any(c => c.status == ContestantStatus.Jury),
                "a week after an eviction");
            yield return Frames(2);
            var state = director.Snapshot;
            foreach (var juror in state.contestants.Where(c => c.status == ContestantStatus.Jury))
                Assert.That(SceneComponents<HouseNpc>().Any(npc => npc.Id == juror.id && npc.gameObject.activeInHierarchy), Is.False,
                    juror.name + " is in the jury house until the finale.");
        }
    }
}
