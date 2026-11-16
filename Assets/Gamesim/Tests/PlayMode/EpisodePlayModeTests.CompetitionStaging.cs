using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The staging families (competition staging, slice 3): a memory, dice, word or skill game watched,
    /// simulated or thrown commits at once, then plays in the yard from the committed standings as its
    /// family's show - a podium quiz at the consoles, hands at work at the tables, or a run on the
    /// course - and ends on its standings card with the yard put back. A press skips, even mid-run;
    /// reduced motion holds a still field; sixteen finish within the bound; and the committed result is
    /// the one the engine commits without any show. Waits are bounded in real seconds (the show runs
    /// on the unscaled clock); shows are asked for, as a batch run stages none.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private CharacterPresentation StagingVisualOf(string id)
        {
            if (id == director.Snapshot.playerId) return player.GetComponent<CharacterPresentation>();
            var npc = SceneComponents<HouseNpc>().FirstOrDefault(actor => actor != null && actor.Id == id);
            return npc != null ? npc.GetComponent<CharacterPresentation>() : null;
        }

        private static CompetitionApparatus.Family StagingInstrumentOf(string kind)
            => kind == "Mental" ? CompetitionApparatus.Family.PairConsole : kind == "Luck" ? CompetitionApparatus.Family.DiceTray
                : kind == "Social" ? CompetitionApparatus.Family.WordConsole : CompetitionApparatus.Family.Signals;

        /// <summary>
        /// Presses the briefing's control of this caption and follows the family's show to its card:
        /// committed at once, the card held, the course deployed, the field at its instruments working
        /// them, the beats in the committed order, then the card, the yard put back and nobody left
        /// running, working or leased. Skips with a real press once a course runner is out, when asked.
        /// </summary>
        private IEnumerator CommitAndFollowTheStaging(string kind, CompetitionShowFamily family, string caption, bool skipMidRun = false)
        {
            var before = director.Snapshot;
            var field = EpisodeEngine.CompetitionPlayers(before).Select(actor => actor.id).ToList();
            ButtonWithCaption(caption).onClick.Invoke();
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(before.revision + 1), caption + ": the result commits at once, as it always has.");
            Assert.That(director.IsCompetitionShowRunning, Is.True, kind + ": the committed result is staged in the yard.");
            Assert.That(director.CompetitionShowFamilyRunning, Is.EqualTo(family), kind + " is staged as a " + family + " show.");
            Assert.That(CompetitionCardPlaying(), Is.False, "The standings card waits for the show.");
            AssertDeployedYard(kind, kind + ": while the show is staged");
            var standings = CommittedStandings(after);
            var instrumentKind = StagingInstrumentOf(kind);
            int mostAtWork = 0;
            float skipAt = -1f, deadline = Time.realtimeSinceStartup + 60f;
            yield return null;
            Assert.That(director.CeremonySkipShowing, Is.True, "The skip chip says a press moves the show on.");
            while (director.IsCompetitionShowRunning && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(CompetitionCardPlaying(), Is.False, "No card over the show.");
                Assert.That(director.Snapshot.revision, Is.EqualTo(after.revision), "The show commits nothing.");
                // The equipment is out and the field is at it: an instrument stands only while its body is at its station.
                mostAtWork = Mathf.Max(mostAtWork, SceneComponents<CompetitionApparatus>()
                    .Count(rig => rig.gameObject.activeInHierarchy && rig.Instrument == instrumentKind && rig.ShownFace != CompetitionApparatus.ShowFace.Ready));
                var running = director.LastCompetitionShowFamily;
                if (skipMidRun && running != null && running.Ran.Count > 0)
                {
                    if (skipAt < 0f) skipAt = Time.realtimeSinceStartup + .6f;
                    else if (Time.realtimeSinceStartup >= skipAt) { yield return PressKey(Key.Enter); break; }
                }
                yield return null;
            }
            Assert.That(director.IsCompetitionShowRunning, Is.False, "The show ends within its bound.");
            var show = director.LastCompetitionShow;
            var record = director.LastCompetitionShowFamily;
            Assert.That(show, Is.Not.Null); Assert.That(record, Is.Not.Null, "The beats began.");
            Assert.That(show.CardPlayed, Is.True, "The show ends on its card.");
            Assert.That(show.Skipped, Is.EqualTo(skipMidRun));
            Assert.That(show.Standings, Is.EqualTo(standings), "The show plays the committed standings.");
            Assert.That(show.Stationed, Is.EquivalentTo(field), "Every competitor of the committed field - and only they - took a station.");
            Assert.That(record.Family, Is.EqualTo(family));
            var staged = standings.Where(id => !show.LeftOut.Contains(id)).ToList();
            Assert.That(record.Staged, Is.EqualTo(staged), "Those at their stations, in the standings' order.");
            Assert.That(staged, Does.Contain(standings[0]), "The winner is never left out.");
            Assert.That(mostAtWork, Is.GreaterThanOrEqualTo(2), "The field stood at its " + instrumentKind + " instruments, showing the beats.");
            if (SceneComponents<CharacterPresentation>().Any(visual => visual.CanAct(CharacterPresentation.BodyActivity.Posing)))
                Assert.That(record.Pressed, Is.Not.Empty, "Hands worked the instruments.");
            if (skipMidRun)
            {
                Assert.That(record.Ran, Is.Not.Empty, "The press came while a runner was out on the course.");
                Assert.That(record.Finished.Count, Is.LessThan(staged.Count), "The press cut the show short.");
            }
            else
            {
                switch (family)
                {
                    case CompetitionShowFamily.Podium:
                        Assert.That(record.Outs, Is.EqualTo(staged.Skip(1).Reverse().ToList()), "Wrong answers put the field out from the bottom of the standings up.");
                        Assert.That(show.Drops, Is.EqualTo(record.Outs), "Each one out lets go of their console and walks aside.");
                        Assert.That(record.Order, Is.EqualTo(record.Outs.Concat(new[] { standings[0] }).ToList()));
                        Assert.That(record.Rounds, Is.InRange(1, CompetitionShowTimeline.MostRounds));
                        Assert.That(show.Cheered, Is.EqualTo(standings[0]), "The winner cheers");
                        Assert.That(show.CheerOffset, Is.LessThan(.6f), "at their own console.");
                        break;
                    case CompetitionShowFamily.Table:
                        Assert.That(record.Finished, Is.EqualTo(staged.Take(staged.Count - 1).ToList()), "They finish in the standings' order, the winner first.");
                        Assert.That(record.TimeCalledOn, Is.EqualTo(staged.Last()), "Time is called on the last place.");
                        Assert.That(show.Cheered, Is.EqualTo(standings[0]));
                        Assert.That(show.CheerOffset, Is.LessThan(.6f), "The winner cheers at their own tray.");
                        Assert.That(record.TurnedToWinner, Is.True, "The field turns to the winner before the card.");
                        Assert.That(show.Drops, Is.Empty, "Nobody walks away from a table.");
                        break;
                    case CompetitionShowFamily.Course:
                        Assert.That(record.Ran, Is.Not.Empty, "Competitors run the course.");
                        Assert.That(record.Ran, Is.SubsetOf(staged));
                        Assert.That(record.Finished, Is.EqualTo(staged.Where(id => !record.DidNotFinish.Contains(id)).ToList()),
                            "Home in the standings' order; nobody is put ahead of anyone above them.");
                        if (record.DidNotFinish.Count > 0) Debug.Log("[Gamesim competition staging test] did not finish: " + string.Join(", ", record.DidNotFinish));
                        Assert.That(record.Finished, Does.Contain(standings[0]), "The winner gets home.");
                        Assert.That(show.Cheered, Is.EqualTo(standings[0]));
                        Assert.That(show.CheerOffset, Is.LessThan(.6f), "The winner cheers at their own post.");
                        Assert.That(record.TurnedToWinner, Is.True);
                        break;
                }
            }
            // Nobody keeps the run gait or the table's hand work after the show.
            foreach (var id in record.Ran.Concat(record.Worked))
            {
                var visual = StagingVisualOf(id);
                if (visual == null) continue;
                if (id != after.playerId) Assert.That(visual.IsRunning, Is.False, id + " stops running when the show ends.");
                Assert.That(visual.Activity, Is.Not.EqualTo(CharacterPresentation.BodyActivity.Cooking), id + " stops working when the show ends.");
            }
            Assert.That(CompetitionCardPlaying(), Is.True, "The standings card follows the show.");
            Assert.That(director.CompetitionYardPutAway, Is.Empty);
            AssertEverydayYard(kind + (skipMidRun ? ", skipped," : "") + " once the show is over");
            yield return null;
            Assert.That(SceneComponents<CompetitionApparatus>(), Is.Empty, "No instrument is left in the yard.");
            Assert.That(SceneComponents<Transform>().Any(node => node.name == "Competition stage runtime"), Is.False, "No stage is left in the yard.");
            Assert.That(NpcRead<HashSet<Transform>>("competitionRouteOwners"), Is.Empty);
            Assert.That(NpcRead<bool>("competitionArenaStaging"), Is.False);
            Assert.That(NpcRead<HouseMeetingCoordinator>("npcMeetings").HasCompetitionStage, Is.False, "Every houseguest is let go.");
            Assert.That(player.HasActivityOwner, Is.False, "The player's body is let go.");
            Assert.That(SceneComponents<CompetitionInstrumentPose>().Any(pose => pose.ShowPress), Is.False, "No hand is left on an instrument.");
            Assert.That(director.Snapshot.randomState, Is.EqualTo(after.randomState), "The show draws nothing from the season.");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator CompetitionStaging_ASimulatedMemoryHoHIsAPodiumQuizFromItsStandings()
        {
            yield return InstallShowAtFirstHoH("Mental");
            yield return CommitAndFollowTheStaging("Mental", CompetitionShowFamily.Podium, EpisodeHud.SimulateCompetitionCaption);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator CompetitionStaging_AThrownDiceHoHIsATableShowWithTheHandsAtWork()
        {
            yield return InstallShowAtFirstHoH("Luck");
            yield return CommitAndFollowTheStaging("Luck", CompetitionShowFamily.Table, EpisodeHud.ThrowCompetitionCaption);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator CompetitionStaging_ASimulatedSkillHoHIsRunOnTheCourse()
        {
            yield return InstallShowAtFirstHoH("Skill");
            yield return CommitAndFollowTheStaging("Skill", CompetitionShowFamily.Course, EpisodeHud.SimulateCompetitionCaption);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator CompetitionStaging_APressSkipsACourseMidRunAndPutsEverythingBack()
        {
            yield return InstallShowAtFirstHoH("Skill");
            yield return CommitAndFollowTheStaging("Skill", CompetitionShowFamily.Course, EpisodeHud.SimulateCompetitionCaption, skipMidRun: true);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator CompetitionStaging_ReducedMotionHoldsAStillPodiumAndThenTheCard()
        {
            yield return InstallShowAtFirstHoH("Mental", reduced: true);
            var earlier = director.LastCompetitionShowFamily;
            var field = EpisodeEngine.CompetitionPlayers(director.Snapshot).Select(actor => actor.id).ToList();
            ButtonWithCaption(EpisodeHud.SimulateCompetitionCaption).onClick.Invoke();
            Assert.That(director.IsCompetitionShowRunning, Is.True, "Reduced motion still places the field.");
            Assert.That(director.CompetitionShowFamilyRunning, Is.EqualTo(CompetitionShowFamily.Podium));
            AssertDeployedYard("Mental", "for the still podium");
            float held = -1f, deadline = Time.realtimeSinceStartup + 60f;
            while (director.IsCompetitionShowRunning && Time.realtimeSinceStartup < deadline)
            {
                if (held < 0f && director.CompetitionShowPhase == EpisodeDirector.CompetitionShowStep.Holding) held = Time.realtimeSinceStartup;
                Assert.That(director.CompetitionShowDrops, Is.Empty, "Nobody goes out under reduced motion.");
                Assert.That(SceneComponents<CompetitionApparatus>().All(rig => rig.ShownFace == CompetitionApparatus.ShowFace.Ready), Is.True, "No beat is played.");
                Assert.That(SceneComponents<CompetitionInstrumentPose>().Any(pose => pose.ShowPress), Is.False, "No hand presses.");
                Assert.That(CompetitionCardPlaying(), Is.False);
                yield return null;
            }
            Assert.That(director.IsCompetitionShowRunning, Is.False);
            Assert.That(held, Is.GreaterThan(0f), "The field held its still pose before the card.");
            Assert.That(Time.realtimeSinceStartup - held, Is.GreaterThanOrEqualTo(CompetitionShowPlan.ReducedHoldSeconds - .05f));
            var record = director.LastCompetitionShow;
            Assert.That(record.Reduced, Is.True);
            Assert.That(record.Cheered, Is.Null, "No cheer under reduced motion.");
            Assert.That(record.CardPlayed, Is.True);
            Assert.That(record.Stationed, Is.EquivalentTo(field));
            Assert.That(director.LastCompetitionShowFamily, Is.SameAs(earlier), "No beat began.");
            Assert.That(CompetitionCardPlaying(), Is.True, "The card follows the still hold.");
            AssertEverydayYard("after the still podium");
            yield return null;
            Assert.That(SceneComponents<CompetitionApparatus>(), Is.Empty);
        }

        [UnityTest, Timeout(400000)]
        public IEnumerator CompetitionStaging_SixteenOnTheCourseFinishWithinTheHardBound()
        {
            const int houseSize = 16;
            var initial = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = houseSize }, SeedOpeningWith("Skill"));
            Assert.That(houseSize, Is.LessThanOrEqualTo(EpisodeValidation.MaximumCast));
            // As the large-cast apparatus fixture does: distinct legal contestants up to sixteen, saved and reloaded.
            while (initial.contestants.Count < houseSize)
            {
                int index = initial.contestants.Count;
                initial.contestants.Add(new ContestantState
                {
                    id = "competition-extra-" + index, name = "Competition Guest " + (index + 1), pronouns = "they/them", homeRoom = "Living",
                    motive = "Stored large-cast competition fixture.", status = ContestantStatus.Active,
                    traits = new List<string> { "Social" }, stats = new ContestantStats(),
                    appearance = initial.contestants[1 + (index % 4)].appearance?.Clone(),
                });
            }
            Assert.That(EpisodeValidation.TryValidate(initial, out var reason), Is.True, reason);
            new EpisodeSaveStore(director.SavePath).Save(initial); yield return ReloadEpisode(); yield return WaitForNpcRuntimeBinding();
            WarpPlayer(director.StationPosition); Assert.That(director.TryOpenPhasePanel(), Is.True);
            ButtonWithCaption("Begin the next competition").onClick.Invoke(); yield return null;
            WarpPlayer(director.StationPosition); Assert.That(director.TryOpenPhasePanel(), Is.True); yield return null;
            AskForTheShow();
            yield return BuildTheWorldForTheShow();
            var before = director.Snapshot;
            Assert.That(EpisodeEngine.CompetitionPlayers(before).Count(), Is.EqualTo(houseSize), "Sixteen compete.");
            ButtonWithCaption(EpisodeHud.SimulateCompetitionCaption).onClick.Invoke();
            float started = Time.realtimeSinceStartup;
            var after = director.Snapshot;
            Assert.That(director.IsCompetitionShowRunning, Is.True);
            var plan = CompetitionShowPlan.For(after, before.phase, "Skill", CompetitionDefinitions.For(before).Id);
            float bound = plan.HardSeconds(false, false);
            Assert.That(bound, Is.LessThanOrEqualTo(27f));
            while (director.IsCompetitionShowRunning && Time.realtimeSinceStartup < started + 60f) yield return null;
            float took = Time.realtimeSinceStartup - started;
            Assert.That(director.IsCompetitionShowRunning, Is.False);
            Assert.That(took, Is.LessThanOrEqualTo(bound + .5f), "The show gives way to its card by its hard bound: " + took.ToString("0.0") + " s of " + bound.ToString("0.0") + ".");
            Assert.That(director.LastCompetitionShow.CardPlayed, Is.True);
            Assert.That(CompetitionCardPlaying(), Is.True);
            AssertEverydayYard("after sixteen ran the course");
            Assert.That(NpcRead<HouseMeetingCoordinator>("npcMeetings").HasCompetitionStage, Is.False);
        }

        /// <summary>The competition's result as the standings card reads it, and the generator after it.</summary>
        private static string StagingResult(EpisodeState state)
            => string.Join(";", state.competitionScores.Select(entry => entry.contestantId + "=" + entry.score.ToString("R")))
                + "|hoh=" + state.hohId + "|random=" + state.randomState + "|phase=" + state.phase;

        /// <summary>What the engine commits for a simulated competition from <paramref name="origin"/>, with no show and no director.</summary>
        private static string EngineSimulates(EpisodeState origin)
        {
            var result = new EpisodeEngine(origin.Clone()).Apply(new EpisodeCommand
            {
                id = "competition-staging-replay", actorId = origin.playerId, expectedPhase = origin.phase,
                expectedRevision = origin.revision, kind = EpisodeCommandKind.SimulateCompetition,
            });
            Assert.That(result.accepted, Is.True, result.reason);
            return StagingResult(result.state);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator CompetitionStaging_TheResultIsTheSameWithShowsOnAndOff()
        {
            yield return InstallShowAtFirstHoH("Luck");
            var origin = director.Snapshot.Clone();
            yield return CommitAndFollowTheStaging("Luck", CompetitionShowFamily.Table, EpisodeHud.SimulateCompetitionCaption);
            var shown = director.Snapshot;
            Assert.That(shown.revision, Is.EqualTo(origin.revision + 1));
            Assert.That(StagingResult(shown), Is.EqualTo(EngineSimulates(origin)), "With the show on, the commit is the engine's own.");

            // The same season again, the shows switched off: the card at once, and the same result.
            new EpisodeSaveStore(director.SavePath).Save(origin);
            yield return ReloadEpisode();
            director.CompetitionShows = false;
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            yield return null;
            var again = director.Snapshot.Clone();
            ButtonWithCaption(EpisodeHud.SimulateCompetitionCaption).onClick.Invoke();
            var unshown = director.Snapshot;
            Assert.That(unshown.revision, Is.EqualTo(again.revision + 1));
            Assert.That(director.IsCompetitionShowRunning, Is.False, "No show with the shows off.");
            Assert.That(StagingResult(unshown), Is.EqualTo(EngineSimulates(again)), "With the show off, the commit is the engine's own.");
            if (StagingResult(again.Clone()) == StagingResult(origin.Clone()) && again.revision == origin.revision)
                Assert.That(StagingResult(unshown), Is.EqualTo(StagingResult(shown)), "The same season commits the same standings, show or no show.");
            yield return null; yield return null;
            Assert.That(CompetitionCardPlaying(), Is.True, "The card plays at once.");
            director.CompetitionShows = true;
        }
    }
}
