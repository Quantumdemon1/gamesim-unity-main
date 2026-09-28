using System.Collections;
using System.Linq;
using Gamesim.House;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>
    /// The read in the season walk (STRATEGY-LOOP-PLAN.md §2): once a season, in a campaign, the
    /// walk asks a voter where their head is at and reads them, one commit each, through the
    /// conversation panel the player uses. Tolerant of a season that never offers the chance: a
    /// campaign the walk cannot reach a voter in is noted, not failed.
    /// </summary>
    public sealed partial class PortVerification
    {
        private bool seasonVoteAsked, seasonPersonRead;

        /// <summary>Whether this step is the read's: a campaign with a vote to read and a question still to ask.</summary>
        private bool SeasonReadDue(EpisodeState state) =>
            state.phase == EpisodePhase.Campaign && VoteRead.Available(state) && (!seasonVoteAsked || !seasonPersonRead)
            && state.Find(state.playerId)?.status == ContestantStatus.Active
            && EpisodeEngine.Voters(state).Any(v => !v.isPlayer);

        /// <summary>
        /// Asks, then reads, a voter with a body the walk can reach. Returns true when a command was
        /// committed; false when nothing could be done this step, so the caller falls through to the
        /// campaign's own decision. Either way each chance is taken once a season.
        /// </summary>
        private IEnumerator ExerciseSeasonRead(EpisodeState state, bool graphical)
        {
            bool asking = !seasonVoteAsked;
            if (asking) seasonVoteAsked = true; else seasonPersonRead = true;
            string caption = asking ? EpisodeHud.AskVoteCaption : EpisodeHud.ReadPersonCaption;
            var voterIds = EpisodeEngine.Voters(state).Where(v => !v.isPlayer).Select(v => v.id).ToList();
            var bodies = seasonDirector.gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HouseNpc>(true))
                .Where(actor => actor.gameObject.activeInHierarchy && voterIds.Contains(actor.Id))
                .ToList();
            if (bodies.Count == 0) { seasonReport.readNote = "No voter had a body to approach for the read."; seasonReadCommitted = false; yield break; }

            yield return CloseSeasonPanel();
            HouseNpc chosen = null; var approach = Vector3.zero;
            foreach (var candidate in bodies)
                if (FindSeasonNpcApproach(candidate, out approach)) { chosen = candidate; break; }
            if (chosen == null) { seasonReport.readNote = "No safe approach to a voter; the read was skipped."; seasonReadCommitted = false; yield break; }
            if (!seasonPlayer.TryMoveTo(approach)) { seasonReport.readNote = "The read's approach was not accepted."; seasonReadCommitted = false; yield break; }
            yield return WaitSeasonWalk("read conversation", approach, false);
            if (!seasonPlayer.HasArrived || Vector3.Distance(seasonPlayer.transform.position, approach) >= 1.1f)
            { seasonReport.readNote = "The read's approach did not complete within its route deadline."; seasonReadCommitted = false; yield break; }
            if (!seasonDirector.TryOpenNpc(chosen.Id)) { seasonReport.readNote = chosen.Id + " was not interactable for the read."; seasonReadCommitted = false; yield break; }
            yield return null; yield return null;
            if (!HasSeasonButtonText(caption))
            {
                seasonReport.readNote = "The conversation offered no \"" + caption + "\" to " + chosen.Id + ".";
                yield return CloseSeasonPanel();
                seasonReadCommitted = false; yield break;
            }
            var before = seasonDirector.Snapshot;
            yield return CaptureSeason(asking ? "vote-asked" : "person-read", graphical);
            yield return ClickSeasonButton(caption);
            var after = seasonDirector.Snapshot;
            RequireSeason(after.revision == before.revision + 1, "The read's control must commit exactly one command: " + caption);
            RequireSeason(EpisodeEngine.SocialActionsSpent(after) == EpisodeEngine.SocialActionsSpent(before), "Asking and reading are free.");
            if (asking)
            {
                RequireSeason(after.events.Skip(before.events.Count).Any(e => e.kind == "vote-read"), "Asking about the vote must say what came of it.");
                seasonReport.votesAsked++;
            }
            else seasonReport.peopleRead++;
            seasonReadCommitted = true;
            yield return CloseSeasonPanel();
        }

        private bool seasonReadCommitted;
    }
}
