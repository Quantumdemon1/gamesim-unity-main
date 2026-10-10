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
        /// writes the blow-up's fallout. Week two at the earliest: in the first week only the
        /// first-night arc casts (EpisodeEngine.BeforeTheFirstAsk). Nothing waits on the week's house
        /// event: in the campaign one is always ready, and the probe of this search found no season
        /// otherwise. With <paramref name="suspend"/> off the house's world stays
        /// live (a storm-off walks in it), and the snapshot is not compared, because the world may
        /// commit a tick of its own as the season loads.
        /// </summary>
        private IEnumerator InstallTheHouseTurnsFixture(bool suspend = true)
        {
            EpisodeState fixture = null;
            for (uint seed = 1; seed <= 60 && fixture == null; seed++)
            {
                var engine = new EpisodeEngine(ContentCatalog.Create(seed));
                for (int guard = 0; guard < 120; guard++)
                {
                    var current = engine.Snapshot;
                    if (current.phase == EpisodePhase.Campaign && current.week >= 2 && current.Active.Count() >= 5 && current.pendingDiary == null
                        && current.Find(current.playerId).status == ContestantStatus.Active)
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
            if (suspend) director.SuspendNpcAutonomyForDiagnostics();
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            yield return ReloadEpisode();
            if (suspend)
            {
                director.SuspendNpcAutonomyForDiagnostics();
                AssertEquivalent(fixture, director.Snapshot);
            }
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

        /// <summary>
        /// A feud the player can end: two houseguests with a grudge past forty, and whichever of the
        /// row's two arcs casts them (the kitchen blow-up for a volatile pair, the silent treatment
        /// otherwise) started by the engine's own seam. Kept only where the player's peace holds -
        /// the option is a check, and the season's own dice decide it - tried on a copy first.
        /// </summary>
        private string peaceEventId, peaceOptionId, peaceFirstId, peaceSecondId;

        private IEnumerator InstallPeaceFixture()
        {
            EpisodeState fixture = null;
            for (uint seed = 1; seed <= 80 && fixture == null; seed++)
            {
                var engine = new EpisodeEngine(ContentCatalog.Create(seed));
                for (int guard = 0; guard < 120 && fixture == null; guard++)
                {
                    var current = engine.Snapshot;
                    // Nothing waits on the week's house event: in the campaign one is always ready.
                    if (current.phase == EpisodePhase.Campaign && current.week >= 2 && current.Active.Count() >= 4 && current.pendingDiary == null
                        && current.Find(current.playerId).status == ContestantStatus.Active)
                    {
                        foreach (var arc in new[] { "kitchen-blowup", "cold-shoulder" })
                        {
                            var trial = new EpisodeEngine(current).Snapshot;
                            EpisodeEngine.EnableStory(trial, trial.week);
                            var npcs = trial.Active.Where(actor => !actor.isPlayer).OrderBy(actor => actor.id, System.StringComparer.Ordinal).ToList();
                            Grudges.Add(trial, npcs[0].id, npcs[1].id, 50, GrudgeCauses.Story);
                            Grudges.Add(trial, npcs[1].id, npcs[0].id, 50, GrudgeCauses.Story);
                            if (!EpisodeEngine.StartStory(trial, arc, StoryAnchors.BlockSet)) continue;
                            var beat = EpisodeEngine.OpenStoryBeats(trial).FirstOrDefault(item => item.contentId == arc + (arc == "kitchen-blowup" ? ":words" : ":silent"));
                            var option = beat?.choices.FirstOrDefault(c => c.optionId == (arc == "kitchen-blowup" ? "calm" : "truce") && !c.locked);
                            if (option == null) continue;
                            var tried = new EpisodeEngine(trial).Apply(new EpisodeCommand
                            {
                                id = "peace-trial", actorId = trial.playerId, kind = EpisodeCommandKind.ProgressStoryline, expectedPhase = trial.phase,
                                expectedRevision = trial.revision, targetId = beat.id, secondTargetId = option.optionId,
                            });
                            var line = tried.accepted ? tried.state.storylines.FirstOrDefault(x => x.id == beat.cycleId) : null;
                            if (line == null || line.endingId != StoryReactions.Peace) continue;
                            fixture = trial;
                            peaceEventId = beat.id; peaceOptionId = option.optionId;
                            peaceFirstId = beat.cast.First(part => part.role == "HOTHEAD").contestantId;
                            peaceSecondId = beat.cast.First(part => part.role == "TARGET").contestantId;
                            break;
                        }
                        break;
                    }
                    if (current.phase == EpisodePhase.Finished) break;
                    Assert.That(engine.Apply(NextCommand(current)).accepted, Is.True);
                }
            }
            Assert.That(fixture, Is.Not.Null, "No bounded legal fixture where the player's peace holds found.");
            director.SuspendNpcAutonomyForDiagnostics();
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            yield return ReloadEpisode();
            director.SuspendNpcAutonomyForDiagnostics();
            AssertEquivalent(fixture, director.Snapshot);
            yield return null; yield return null;
        }

        /// <summary>
        /// The player brings a feud to an end, and once the scene's card closes the two who made up
        /// embrace - the parts the beat names, at the ending the player saw. The embrace commits
        /// nothing: with the house's own clock held, the revision does not move.
        /// </summary>
        [UnityTest]
        public IEnumerator StoryReactions_TwoWhoMakePeaceEmbraceOnceTheCardCloses()
        {
            yield return InstallPeaceFixture();
            var beat = EpisodeEngine.OpenStoryBeats(director.Snapshot).Single(item => item.id == peaceEventId);
            var option = beat.choices.Single(c => c.optionId == peaceOptionId);
            Assert.That(director.OpenSceneCard(beat.id), Is.True);
            yield return null;
            ButtonWithCaption(EpisodeHud.EventChoiceCaption(option.label)).onClick.Invoke();
            yield return Frames(2);
            var line = director.Snapshot.storylines.Single(x => x.id == beat.cycleId);
            Assert.That(line.endingId, Is.EqualTo(StoryReactions.Peace), "The player made peace between them.");
            Assert.That(StoryBody(peaceFirstId).LastReaction, Is.Not.EqualTo(CharacterPresentation.Reaction.Embrace),
                "Nothing while the card still says what came of it.");
            int revision = director.Snapshot.revision;
            ButtonWithCaption(EpisodeDirector.SceneCardDoneCaption).onClick.Invoke();
            yield return Frames(3);
            Assert.That(StoryBody(peaceFirstId).LastReaction, Is.EqualTo(CharacterPresentation.Reaction.Embrace), "The one who started it embraces,");
            Assert.That(StoryBody(peaceSecondId).LastReaction, Is.EqualTo(CharacterPresentation.Reaction.Embrace), "and so does the other.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "The embrace commits nothing.");
            Assert.That(director.PendingEmbraceFirst, Is.Null, "and nothing is left waiting.");
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
