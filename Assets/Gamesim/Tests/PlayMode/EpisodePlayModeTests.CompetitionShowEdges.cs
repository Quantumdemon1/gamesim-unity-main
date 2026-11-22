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
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The competition show's rough edges in the built yard (competition show edges): on the course
    /// the player's own houseguest runs their leg as the others run theirs; and at the quick pace a
    /// field of sixteen all reach their stations before the show starts, every runner has a leg, and
    /// no leg runs through another station or meets another leg. The timing and the legs' geometry are
    /// pinned for every cast size in the Unity-free CompetitionShowEdgesTests; these two read the
    /// bodies. Waits are bounded in real seconds (the show runs on the unscaled clock); shows are
    /// asked for, as a batch run stages none.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest, Timeout(300000)]
        public IEnumerator CompetitionShowEdges_YourHouseguestRunsTheCourseLegAsTheOthersDo()
        {
            yield return InstallShowAtFirstHoH("Skill");
            var before = director.Snapshot;
            string me = before.playerId;
            Assert.That(EpisodeEngine.CompetitionPlayers(before).Any(actor => actor.id == me), Is.True, "The fixture puts the player in the field.");
            ButtonWithCaption(EpisodeHud.SimulateCompetitionCaption).onClick.Invoke();
            Assert.That(director.CompetitionShowFamilyRunning, Is.EqualTo(CompetitionShowFamily.Course), "A skill game is run on the course.");
            int ranFrames = 0, walkedFrames = 0;
            float deadline = Time.realtimeSinceStartup + 60f;
            while (director.IsCompetitionShowRunning && Time.realtimeSinceStartup < deadline)
            {
                var running = director.LastCompetitionShowFamily;
                // From the moment the player sets off on their leg until they finish (or do not) at their post.
                if (running != null && running.Ran.Contains(me) && !running.Finished.Contains(me) && !running.DidNotFinish.Contains(me))
                {
                    if (player.IsRunning) ranFrames++; else walkedFrames++;
                }
                yield return null;
            }
            Assert.That(director.IsCompetitionShowRunning, Is.False, "The show ends within its bound.");
            var record = director.LastCompetitionShowFamily;
            Assert.That(record, Is.Not.Null, "The beats began.");
            Assert.That(record.Ran, Does.Contain(me), "Your houseguest ran a leg of the course.");
            Assert.That(ranFrames, Is.GreaterThan(0), "and ran it");
            Assert.That(walkedFrames, Is.EqualTo(0), "the whole way, as the others run theirs.");
            Assert.That(player.IsRunning, Is.False, "and stops running when the show is over.");
            Assert.That(director.LastCompetitionShow.CardPlayed, Is.True);
        }

        [UnityTest, Timeout(400000)]
        public IEnumerator CompetitionShowEdges_SixteenAtTheQuickPaceAllReachTheFieldAndRunClearLegs()
        {
            const int houseSize = 16;
            var initial = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = houseSize }, SeedOpeningWith("Skill"));
            // As the sixteen on the course do: distinct legal contestants up to sixteen, saved and reloaded.
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
            director.SetCeremonyPace(CeremonyPace.Quick);
            try
            {
                var before = director.Snapshot;
                Assert.That(EpisodeEngine.CompetitionPlayers(before).Count(), Is.EqualTo(houseSize), "Sixteen compete.");
                ButtonWithCaption(EpisodeHud.SimulateCompetitionCaption).onClick.Invoke();
                Assert.That(director.IsCompetitionShowRunning, Is.True);
                Assert.That(director.CompetitionShowFamilyRunning, Is.EqualTo(CompetitionShowFamily.Course));
                float deadline = Time.realtimeSinceStartup + 60f;
                while (director.IsCompetitionShowRunning && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(director.IsCompetitionShowRunning, Is.False, "The show ends within its bound.");
                var show = director.LastCompetitionShow;
                var record = director.LastCompetitionShowFamily;
                Assert.That(show.CardPlayed, Is.True); Assert.That(show.Skipped, Is.False);
                Assert.That(show.Stationed.Count, Is.EqualTo(houseSize), "Every competitor took a station.");
                Assert.That(show.LeftOut, Is.Empty, "At the quick pace the whole field of sixteen reached its stations before the show started.");
                Assert.That(show.AssembledSeconds, Is.LessThanOrEqualTo(show.AssemblyWaitSeconds), "inside the wait for it.");
                Assert.That(show.WalkOutSeconds, Is.LessThanOrEqualTo(CompetitionShowPlan.MostWalkOutSeconds(true) + 1e-3f));
                Assert.That(record, Is.Not.Null, "The beats began.");
                Assert.That(record.Staged.Count, Is.EqualTo(houseSize));
                Assert.That(record.LegFrom.Keys, Is.EquivalentTo(record.Staged), "Every staged runner has a post.");
                // A post whose yard is blocked by scenery runs no leg, as before; the two rows are what this case reads.
                var noLeg = record.Staged.Where(id => !record.LegTo.ContainsKey(id)).ToList();
                if (noLeg.Count > 0) Debug.Log("[Gamesim competition edges test] no leg for: " + string.Join(", ", noLeg));
                float backRow = record.LegFrom.Values.Max(post => post.z);
                Assert.That(record.LegFrom.Values.Any(post => post.z < backRow - 1f), Is.True, "Sixteen stand in two rows.");
                Assert.That(record.Ran, Is.Not.Empty, "The field runs the course.");
                Assert.That(record.Ran, Is.SubsetOf(record.LegTo.Keys), "Only a runner with a leg ran.");
                // No leg runs through another's station or meets another leg (narrower than any body, to read the layout, not the agents).
                const float body = .2f;
                float reach = CompetitionStageFootprint.Reach(StagingInstrumentOf("Skill"));
                var ids = record.Staged.Where(id => record.LegTo.ContainsKey(id)).ToList();
                foreach (var id in ids)
                {
                    var from = new CompetitionCourseLegs.Spot(record.LegFrom[id].x, record.LegFrom[id].z);
                    var to = new CompetitionCourseLegs.Spot(record.LegTo[id].x, record.LegTo[id].z);
                    foreach (var other in record.Staged)
                    {
                        if (other == id) continue;
                        var post = record.LegFrom[other];
                        var station = CompetitionCourseLegs.Box.Station(post.x, post.z, body, reach, CompetitionStageFootprint.HalfWidth);
                        Assert.That(CompetitionCourseLegs.SegmentBoxDistance(from, to, station), Is.GreaterThanOrEqualTo(body),
                            id + "'s leg " + from + " to " + to + " runs clear of " + other + "'s station " + station + ".");
                        if (!record.LegTo.ContainsKey(other)) continue;
                        var otherTo = new CompetitionCourseLegs.Spot(record.LegTo[other].x, record.LegTo[other].z);
                        Assert.That(CompetitionCourseLegs.SegmentDistance(from, to, new CompetitionCourseLegs.Spot(post.x, post.z), otherTo), Is.GreaterThanOrEqualTo(2f * body),
                            id + "'s and " + other + "'s legs never meet.");
                    }
                }
                Assert.That(CompetitionCardPlaying(), Is.True, "The standings card follows the show.");
                Assert.That(NpcRead<HouseMeetingCoordinator>("npcMeetings").HasCompetitionStage, Is.False);
            }
            finally { director.SetCeremonyPace(CeremonyPace.Suspenseful); }
        }
    }
}
