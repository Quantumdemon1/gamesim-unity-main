using System.Collections;
using System.Linq;
using System.Reflection;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// A storm-off walked (the art brief's R6): the one the house turned on plays the beat, and once
    /// the fallout's card has gone walks to a room away from the row - their own, or the Bedroom, or
    /// the Yard (decision D4) - stands with their back to it a while, and is the house's again. Asked
    /// with the house's world live, the clock pinned, and the scene's card left open, so the house's
    /// own conversations hold still and only the storm moves.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private string stormTarget, stormFrom;

        private HouseMeetingCoordinator StormCoordinator() =>
            (HouseMeetingCoordinator)typeof(EpisodeDirector).GetField("npcMeetings", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(director);

        /// <summary>The house turns on somebody, the player answers, and the fallout's card is cut short.</summary>
        private IEnumerator TurnTheHouse(bool storms)
        {
            Application.runInBackground = true;
            Time.captureDeltaTime = 1f / 30f;
            yield return InstallTheHouseTurnsFixture(suspend: false);
            director.StormOffsInBatchRuns = storms;
            director.StormSulkSeconds = 1.5f;
            director.BuildNpcWorldForDiagnostics();
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null);
            for (int frame = 0; frame < 300 && !director.NpcAutonomyReady; frame++) yield return null;
            Assert.That(director.NpcAutonomyReady, Is.True, "The house's world is up.");
            var beat = EpisodeEngine.OpenStoryBeats(director.Snapshot).Single(item => item.contentId == "the-house-turns:turns");
            stormTarget = beat.cast.Single(part => part.role == "TARGET").contestantId;
            stormFrom = EpisodeDirector.VenueRoom(beat.venue);
            Assert.That(director.OpenSceneCard(beat.id), Is.True, "The meeting opens where the player stands.");
            yield return null;
            var option = beat.choices.First(c => !c.locked && !c.pickPerson && !c.conduct && !c.costsAction);
            ButtonWithCaption(EpisodeHud.EventChoiceCaption(option.label)).onClick.Invoke();
            yield return Frames(2);
            Assert.That(StoryBody(stormTarget).LastReaction, Is.EqualTo(CharacterPresentation.Reaction.StormOff),
                "The one the house turned on storms off.");
            Assert.That(director.StormingId, Is.Null, "Nobody walks while the fallout's card is up.");
            foreach (var card in SceneComponents<CeremonyTakeover>()) card.Cancel();
        }

        // The storm's beats wait on the unscaled clock, which the pinned frame time does not move: a
        // batchmode frame is a fraction of a millisecond, so these waits are in real seconds.
        private IEnumerator UntilTheStormWalks()
        {
            yield return WaitFor(() => director.StormNow == EpisodeDirector.StormStage.Walking, 5f, "The walk began once the card had gone.");
            Assert.That(director.StormingId, Is.EqualTo(stormTarget));
            Assert.That(director.StormLeaseHeld, Is.True, "The house holds the walk by a storm lease of its own.");
        }

        /// <summary>
        /// The whole storm: the beat, the walk under its own lease to the room decision D4 names, the
        /// hips arriving on that room's floor, the sulk, and the body the house's again.
        /// </summary>
        [UnityTest]
        public IEnumerator StormOff_TheOneTheHouseTurnedOnStormsToARoomOfTheirOwnAndSulks()
        {
            try
            {
                yield return TurnTheHouse(storms: true);
                yield return UntilTheStormWalks();
                string expected = EpisodeDirector.StormRoomFor(director.Snapshot.Find(stormTarget).homeRoom, stormFrom);
                Assert.That(director.StormRoom, Is.EqualTo(expected), "Their own room, or the Bedroom, or the Yard, away from the " + stormFrom + ".");
                Assert.That(expected, Is.Not.EqualTo(stormFrom), "Never the room the row was in.");
                yield return WaitFor(() => director.StormNow != EpisodeDirector.StormStage.Walking, 60f, "The walk ends.");
                Assert.That(director.StormNow, Is.EqualTo(EpisodeDirector.StormStage.Sulking), "They arrived and sulk.");
                var body = ActBody(stormTarget);
                Assert.That(RoomAt(body), Is.EqualTo(expected), "Their hips stand on the " + expected + "'s floor.");
                Assert.That(director.StormLeaseHeld, Is.True, "The house holds them through the sulk.");
                yield return WaitFor(() => director.StormNow == EpisodeDirector.StormStage.None, 6f, "The sulk ends,");
                Assert.That(director.AnyStormLease, Is.False, "and the body is the house's again.");
            }
            finally { Time.captureDeltaTime = 0f; }
        }

        /// <summary>A ceremony's summons has a better claim than a storm: it takes the body mid-walk, and the storm is over.</summary>
        [UnityTest]
        public IEnumerator StormOff_ACeremonysSummonsTakesTheBodyMidWalk()
        {
            try
            {
                yield return TurnTheHouse(storms: true);
                yield return UntilTheStormWalks();
                yield return Frames(10);
                var coordinator = StormCoordinator();
                Assert.That(coordinator.IsStorming(stormTarget), Is.True);
                var at = ActBody(stormTarget).position;
                // The nearest place a ceremony could summon them to that has a route: the first
                // summons takes them from the storm, whether or not its route is found.
                bool joined = false;
                string reason = null;
                foreach (var place in SceneComponents<HouseInteractionAnchor>()
                    .Where(anchor => anchor.isActiveAndEnabled && anchor.gameObject.scene == director.gameObject.scene)
                    .OrderBy(anchor => (anchor.transform.position - at).sqrMagnitude).Take(12))
                    if (coordinator.JoinCeremonyStage(stormTarget, place, out reason)) { joined = true; break; }
                Assert.That(joined, Is.True, "A ceremony summoned them: " + reason);
                Assert.That(coordinator.IsStorming(stormTarget), Is.False, "The summons took them from the storm.");
                yield return null;
                Assert.That(director.StormingId, Is.Null, "and the storm is over.");
                Assert.That(director.AnyStormLease, Is.False);
                coordinator.EndCeremonyStage();
                yield return Frames(30);
                Assert.That(director.StormingId, Is.Null, "It does not start again once the ceremony lets go.");
            }
            finally { Time.captureDeltaTime = 0f; }
        }

        /// <summary>
        /// A storm commits nothing: with the scene's card open the house's own conversations hold, and
        /// the revision does not move while the storm walks. A reload then drops it whole: no storm,
        /// no storm lease.
        /// </summary>
        [UnityTest]
        public IEnumerator StormOff_CommitsNothingAndAReloadDropsIt()
        {
            try
            {
                yield return TurnTheHouse(storms: true);
                yield return UntilTheStormWalks();
                int revision = director.Snapshot.revision;
                yield return Frames(60);
                Assert.That(director.StormNow, Is.Not.EqualTo(EpisodeDirector.StormStage.None), "Still storming.");
                Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "The storm commits nothing.");
                yield return ReloadEpisode();
                yield return Frames(3);
                Assert.That(director.StormingId, Is.Null, "A reload drops the storm.");
                Assert.That(director.AnyStormLease, Is.False, "and leaves no storm lease.");
            }
            finally { Time.captureDeltaTime = 0f; }
        }

        /// <summary>A batch run walks no storms unless it asks: the beat plays, and nobody leaves the room.</summary>
        [UnityTest]
        public IEnumerator StormOff_IsOffInBatchRunsByDefault()
        {
            try
            {
                Assert.That(director.StormOffsInBatchRuns, Is.False, "Off unless a test asks.");
                yield return TurnTheHouse(storms: false);
                // Past the beat's 0.9 s, when a storm would have set off.
                float until = Time.realtimeSinceStartup + 1.5f;
                while (Time.realtimeSinceStartup < until) { Assert.That(director.StormingId, Is.Null, "No storm begins."); yield return null; }
                Assert.That(director.StormingId, Is.Null, "No storm walked.");
                Assert.That(director.AnyStormLease, Is.False, "and no storm lease was taken.");
            }
            finally { Time.captureDeltaTime = 0f; }
        }
    }
}
