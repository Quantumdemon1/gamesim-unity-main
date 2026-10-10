using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The story's reactions in the house (the art brief's R4): a fallout acted out by the parts
    /// its beat names, a removal's nearest onlookers, the winner shocked at the jury's result and
    /// then winning, and the final eviction's juror and finalist. Asked of the bodies' recorded
    /// beats, which the primitive rig keeps exactly as a UMA body does.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private CharacterPresentation StoryBody(string id) =>
            SceneComponents<HouseNpc>().Single(npc => npc.Id == id).GetComponent<CharacterPresentation>();

        /// <summary>
        /// A season in the campaign (the block set) where the house has turned on one of its own: a legal history
        /// with the story system switched on, three houseguests holding grudges past forty against a
        /// fourth, and the arc started by the engine's own seam - its opener is the meeting that
        /// writes the blow-up's fallout.
        /// </summary>
        private IEnumerator InstallTheHouseTurnsFixture()
        {
            EpisodeState fixture = null;
            for (uint seed = 1; seed <= 60 && fixture == null; seed++)
            {
                var engine = new EpisodeEngine(ContentCatalog.Create(seed));
                for (int guard = 0; guard < 120; guard++)
                {
                    var current = engine.Snapshot;
                    if (current.phase == EpisodePhase.Campaign && current.Active.Count() >= 5 && current.pendingDiary == null
                        && !HouseEvents.Ready(current) && current.Find(current.playerId).status == ContestantStatus.Active)
                    {
                        EpisodeEngine.EnableStory(current, current.week);
                        var npcs = current.Active.Where(actor => !actor.isPlayer).OrderBy(actor => actor.id, System.StringComparer.Ordinal).ToList();
                        var target = npcs.FirstOrDefault(actor => actor.id != current.hohId);
                        if (target == null) break;
                        foreach (var holder in npcs.Where(actor => actor.id != target.id).Take(3))
                            Grudges.Add(current, holder.id, target.id, 50, GrudgeCauses.Story);
                        if (EpisodeEngine.StartStory(current, "the-house-turns", StoryAnchors.BlockSet)
                            && EpisodeEngine.OpenStoryBeats(current).Any(beat => beat.contentId == "the-house-turns:turns")) fixture = current;
                        break;
                    }
                    if (current.phase == EpisodePhase.Finished) break;
                    Assert.That(engine.Apply(NextCommand(current)).accepted, Is.True);
                }
            }
            Assert.That(fixture, Is.Not.Null, "No bounded legal fixture where the house turns on somebody found.");
            director.SuspendNpcAutonomyForDiagnostics();
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            yield return ReloadEpisode();
            director.SuspendNpcAutonomyForDiagnostics();
            AssertEquivalent(fixture, director.Snapshot);
            yield return null; yield return null;
        }

        /// <summary>
        /// The house turns on somebody, and the player sees it: the three who turned are furious, and
        /// the one they turned on storms off - by the parts the beat's own text names, nothing else.
        /// </summary>
        [UnityTest]
        public IEnumerator StoryReactions_TheHouseTurningIsActedOutByItsParts()
        {
            yield return InstallTheHouseTurnsFixture();
            var beat = EpisodeEngine.OpenStoryBeats(director.Snapshot).Single(item => item.contentId == "the-house-turns:turns");
            string Part(string role) => beat.cast.Single(part => part.role == role).contestantId;
            Assert.That(director.OpenSceneCard(beat.id), Is.True, "The meeting opens where the player stands.");
            yield return null;
            var option = beat.choices.First(c => !c.locked && !c.pickPerson && !c.conduct && !c.costsAction);
            ButtonWithCaption(EpisodeHud.EventChoiceCaption(option.label)).onClick.Invoke();
            yield return Frames(2);
            Assert.That(EpisodeEngine.OpenStoryBeats(director.Snapshot).Any(item => item.id == beat.id), Is.False, "The answer committed.");
            Assert.That(SceneComponents<CeremonyTakeover>().Any(card => card.IsPlaying && card.PlayingKind == StoryLog.Blowup), Is.True,
                "The blow-up gets its card.");
            foreach (var role in new[] { "AGGRESSOR1", "AGGRESSOR2", "AGGRESSOR3" })
                Assert.That(StoryBody(Part(role)).LastReaction, Is.EqualTo(CharacterPresentation.Reaction.Furious), role + " is furious.");
            Assert.That(StoryBody(Part("TARGET")).LastReaction, Is.EqualTo(CharacterPresentation.Reaction.StormOff),
                "The one the house turned on storms off.");
            foreach (var card in SceneComponents<CeremonyTakeover>()) card.Cancel();
            yield return Frames(2);
        }

        /// <summary>
        /// Production removes somebody: they leave in tears, and the houseguest standing a metre away
        /// is shocked - while one across the house, out of sight of it, is not.
        /// </summary>
        [UnityTest]
        public IEnumerator StoryReactions_ARemovalsNearestOnlookersAreShocked()
        {
            yield return InstallRemovalAtFourFixture();
            var state = director.Snapshot;
            string removed = state.story.pendingRemovalId;
            var others = state.Active.Where(actor => !actor.isPlayer && actor.id != removed).Select(actor => actor.id).ToList();
            Assert.That(others.Count, Is.GreaterThanOrEqualTo(2), "Two houseguests besides the one leaving.");
            var removedBody = SceneComponents<HouseNpc>().Single(npc => npc.Id == removed);
            var near = SceneComponents<HouseNpc>().Single(npc => npc.Id == others[0]);
            var far = SceneComponents<HouseNpc>().Single(npc => npc.Id == others[1]);
            Assert.That(PutBodyAt(near, PointBeside(removedBody.transform.position, 1f)), Is.True, "One stands beside the one leaving,");
            Assert.That(PutBodyAt(far, PointAway(removedBody.transform.position, 6f)), Is.True, "and one well across the house.");
            yield return null;
            Assert.That(Flat(far.transform.position, removedBody.transform.position), Is.GreaterThan(StoryReactions.OnlookerRadius));
            yield return OpenFinalePanel();
            ButtonWithCaption("Begin the next competition").onClick.Invoke();
            yield return Frames(2);
            Assert.That(director.Snapshot.Find(removed).status, Is.EqualTo(ContestantStatus.Expelled));
            Assert.That(removedBody.GetComponent<CharacterPresentation>().LastReaction, Is.EqualTo(CharacterPresentation.Reaction.Tearful),
                "The one removed leaves in tears.");
            Assert.That(near.GetComponent<CharacterPresentation>().LastReaction, Is.EqualTo(CharacterPresentation.Reaction.Shocked),
                "The houseguest beside them is shocked.");
            Assert.That(far.GetComponent<CharacterPresentation>().LastReaction, Is.Not.EqualTo(CharacterPresentation.Reaction.Shocked),
                "The one across the house only turns to look.");
            foreach (var card in SceneComponents<CeremonyTakeover>()) card.Cancel();
            yield return Frames(2);
        }

        /// <summary>
        /// The season's winner waits for the jury card's result (MOCKUP-PASS-PLAN M36, decision D6):
        /// nothing as the votes begin to be read, the shock of it as the result shows, and the win
        /// after it.
        /// </summary>
        [UnityTest]
        public IEnumerator StoryReactions_TheWinnerIsShockedAtTheResultAndThenWins()
        {
            HoldTheHouseForTheFixture();
            yield return InstallFinaleFixture(false);
            director.SetCeremonyPace(CeremonyPace.Quick);
            try
            {
                yield return OpenFinalePanel();
                ButtonWithCaption(EpisodeHud.JurySkipCaption).onClick.Invoke();
                yield return Frames(2);
                ButtonWithCaption(EpisodeHud.SpeechContinueCaption).onClick.Invoke();
                yield return Frames(2);
                var before = director.Snapshot;
                ButtonWithCaption("Vote for " + before.Active.First().name + " to win").onClick.Invoke();
                yield return Frames(2);
                ButtonWithCaption("Continue episode").onClick.Invoke();
                yield return Frames(2);
                var state = director.Snapshot;
                Assert.That(state.phase, Is.EqualTo(EpisodePhase.Finished));
                var reveal = SceneComponents<JuryReveal>().Single();
                Assert.That(reveal.IsPlaying && !reveal.ShowingResult, Is.True, "The jury is being read.");
                var winner = StoryBody(state.winnerId);
                Assert.That(winner.LastReaction, Is.Not.EqualTo(CharacterPresentation.Reaction.Won), "The winner does not celebrate as the reading starts.");
                Assert.That(winner.LastReaction, Is.Not.EqualTo(CharacterPresentation.Reaction.Shocked));

                float deadline = Time.realtimeSinceStartup + 90f;
                while (!reveal.ShowingResult && reveal.IsPlaying && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(reveal.ShowingResult, Is.True, "The card reached its result.");
                yield return Frames(2);
                Assert.That(winner.LastReaction, Is.EqualTo(CharacterPresentation.Reaction.Shocked), "The winner is shocked at the result.");
                Assert.That(director.QueuedStoryReactions, Is.EqualTo(1), "and the win is waiting on the clock.");
                deadline = Time.realtimeSinceStartup + EpisodeDirector.WinnerShockSeconds + 0.5f;
                while (winner.LastReaction != CharacterPresentation.Reaction.Won && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(winner.LastReaction, Is.EqualTo(CharacterPresentation.Reaction.Won), "Then wins, within three seconds of the result.");
            }
            finally { director.SetCeremonyPace(CeremonyPace.Suspenseful); }
            yield return SkipReveals();
        }

        /// <summary>
        /// The final Head of Household's choice: the new juror weeps, and the finalist chosen to sit
        /// beside the Head of Household is shocked.
        /// </summary>
        [UnityTest]
        public IEnumerator StoryReactions_TheFinalEvictionsJurorWeepsAndTheChosenFinalistIsShocked()
        {
            HoldTheHouseForTheFixture();
            yield return InstallDiaryFixture(state => state.phase == EpisodePhase.FinalEviction && state.hohId == state.playerId,
                "the player as final Head of Household");
            yield return OpenFinalePanel();
            var state = director.Snapshot;
            var cut = state.Active.First(actor => !actor.isPlayer);
            var kept = state.Active.Single(actor => !actor.isPlayer && actor.id != cut.id);
            ButtonWithCaption(FinalChoiceWords.CaptionToEvict(state, cut.id)).onClick.Invoke();
            yield return Frames(2);
            Assert.That(director.Snapshot.Find(cut.id).status, Is.EqualTo(ContestantStatus.Jury));
            Assert.That(StoryBody(cut.id).LastReaction, Is.EqualTo(CharacterPresentation.Reaction.Tearful), "The new juror weeps.");
            Assert.That(StoryBody(kept.id).LastReaction, Is.EqualTo(CharacterPresentation.Reaction.Shocked), "The chosen finalist is shocked.");
            foreach (var card in SceneComponents<CeremonyTakeover>()) card.Cancel();
            foreach (var sting in SceneComponents<CeremonySting>()) sting.Cancel();
            yield return Frames(3);
        }

        /// <summary>A walkable point about <paramref name="distance"/> from a body, on the floor.</summary>
        private static Vector3 PointBeside(Vector3 from, float distance)
        {
            for (int step = 0; step < 16; step++)
            {
                float angle = step * 22.5f * Mathf.Deg2Rad;
                var wanted = from + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                if (NavMesh.SamplePosition(wanted, out var hit, .3f, NavMesh.AllAreas) && Flat(hit.position, from) <= distance + .3f) return hit.position;
            }
            Assert.Fail("No floor beside the body.");
            return from;
        }

        /// <summary>A walkable point at least <paramref name="distance"/> from a body.</summary>
        private static Vector3 PointAway(Vector3 from, float distance)
        {
            for (float reach = distance; reach < distance + 8f; reach += 1f)
                for (int step = 0; step < 16; step++)
                {
                    float angle = step * 22.5f * Mathf.Deg2Rad;
                    var wanted = from + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * reach;
                    if (NavMesh.SamplePosition(wanted, out var hit, .5f, NavMesh.AllAreas) && Flat(hit.position, from) >= distance) return hit.position;
                }
            Assert.Fail("No floor far enough from the body.");
            return from;
        }
    }
}
