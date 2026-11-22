using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>
    /// The ring tower as a show (competition drop-outs and tower show). A ring tower competition that is
    /// watched, simulated, thrown or taken by the accessible route commits at once, as it always has, and
    /// its standings card waits behind a short show played from the committed standings: the yard's three
    /// stacks stand with their playable rings, as for the player's own game; the player, when they
    /// competed, takes their place at the stacks, and otherwise the winner does; the rest of the field
    /// stand at the side, facing the tower, and work the puzzle with their hands. They finish in the
    /// standings' order, the winner first and cheering, and time is called on the last. The tower's rings
    /// work through the fewest moves from the first stack to the last and finish on the finish of the one
    /// at the stacks - two thirds done when time is called on them (<see cref="CompetitionTowerShowRings"/>).
    ///
    /// <para>Presentation only, as every show is: nothing is saved, nothing draws from the season's
    /// generator, nothing changes what was committed. The walk out, the skip, the bound, reduced motion
    /// (a still hold, the rings at the first stack) and every way a show ends are slice 1's.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        private sealed partial class CompetitionShowRun
        {
            /// <summary>A tower show's rings, from the beats; null before the beats begin and for any other show.</summary>
            public CompetitionTowerShowRings TowerRings;
        }

        /// <summary>Whether the running show is a ring tower's.</summary>
        private bool CompetitionTowerShowRunning => competitionShow != null && competitionShow.Plan.Family == CompetitionShowFamily.Tower;

        /// <summary>
        /// Whether this competitor takes the place at the stacks in a tower show: the winner, when the player
        /// has no place there (they watched, or their route was refused). The player's own place is theirs.
        /// </summary>
        private bool CompetitionTowerShowAtTheStacks(CompetitionShowRun show, string id) =>
            show != null && show.Plan.Family == CompetitionShowFamily.Tower && competitionRingTower != null
            && competitionPlayerStation == null && id != null && id == show.Plan.WinnerId;

        /// <summary>The tower's rings as the running tower show has them now; null (the first stack) before its beats.</summary>
        private RingTowerPuzzle CompetitionTowerShowPuzzle()
        {
            var run = competitionShow;
            if (run == null || run.TowerRings == null || !run.HoldBegun) return null;
            var puzzle = run.TowerRings.PuzzleAt(Time.unscaledTime - run.HoldFrom);
            if (run.FamilyRecord != null)
            {
                run.FamilyRecord.TowerMovesShown = Mathf.Max(run.FamilyRecord.TowerMovesShown, puzzle.Moves);
                run.FamilyRecord.TowerSolved |= puzzle.Solved;
            }
            return puzzle;
        }

        /// <summary>The beats begin (from BeginCompetitionShowFamily): whose tower the stacks show.</summary>
        private void BeginCompetitionTowerShow(CompetitionShowRun run, IReadOnlyList<string> staged)
        {
            if (run.Plan.Family != CompetitionShowFamily.Tower || run.Timeline == null) return;
            string subject = staged.Contains(run.Plan.PlayerId) && competitionPlayerStation != null ? run.Plan.PlayerId
                : staged.Contains(run.Plan.WinnerId) ? run.Plan.WinnerId : staged.FirstOrDefault();
            run.TowerRings = CompetitionTowerShowRings.For(run.Timeline, subject);
            if (run.FamilyRecord != null) run.FamilyRecord.TowerSubject = run.TowerRings?.SubjectId;
            if (run.TowerRings == null) Debug.Log("[Gamesim competition show] the tower's rings stay on the first stack: no beat for " + subject + ".", this);
        }

        /// <summary>
        /// A tower show's competitor at their place, each frame, with no instrument of their own: the pose the
        /// beat asks for, and at work the cooking loop's hand work - the puzzle worked with the hands. A
        /// reaction plays out before the stance comes back.
        /// </summary>
        private void CompetitionTowerShowStance(CharacterPresentation visual, bool arrived, string id)
        {
            var run = competitionShow;
            if (run == null || run.Plan.Family != CompetitionShowFamily.Tower || visual == null || !arrived
                || !visual.IsStill || !visual.CanAct(CharacterPresentation.BodyActivity.Posing)) return;
            var actor = CompetitionShowActorFor(id);
            if (actor == null || Time.unscaledTime < actor.ReactUntil) return;
            var activity = CharacterPresentation.BodyActivity.Posing;
            if (actor.Working && !run.Reduced && visual.CanAct(CharacterPresentation.BodyActivity.Cooking))
                activity = CharacterPresentation.BodyActivity.Cooking;
            if (!competitionPoses.TryGetValue(visual, out var own))
            {
                if (visual.Activity != CharacterPresentation.BodyActivity.None) return;
                own = new CompetitionPose { previous = visual.HeldPose };
                competitionPoses.Add(visual, own);
            }
            else if (visual.Activity != own.writtenActivity) return;
            own.written = actor.Pose; own.writtenActivity = activity;
            visual.SetPose(actor.Pose); visual.SetActivity(activity);
            if (activity == CharacterPresentation.BodyActivity.Cooking && run.FamilyRecord != null) run.FamilyRecord.Worked.Add(id);
        }

        /// <summary>
        /// The heading a tower show's competitor at the side keeps: towards the tower, so the field faces the
        /// puzzle it works; <paramref name="fallback"/> at the stacks, in any other show, or with no tower.
        /// </summary>
        private float CompetitionTowerShowFacing(string id, Vector3 from, float fallback)
        {
            if (!CompetitionTowerShowRunning || competitionRingTower == null || CompetitionTowerShowAtTheStacks(competitionShow, id)) return fallback;
            var to = competitionRingTower.Centre - from;
            if (to.x * to.x + to.z * to.z < .01f) return fallback;
            return Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
        }
    }
}
