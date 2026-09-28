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
        /// <summary>Approaches that found nobody to talk to: the chance is kept for the next step, three times, then let go.</summary>
        private int seasonReadMisses;

        /// <summary>Whether this step is the read's: a campaign with a vote to read and a question still to ask.</summary>
        private bool SeasonReadDue(EpisodeState state) =>
            state.phase == EpisodePhase.Campaign && VoteRead.Available(state) && (!seasonVoteAsked || !seasonPersonRead)
            && state.Find(state.playerId)?.status == ContestantStatus.Active
            && EpisodeEngine.Voters(state).Any(v => !v.isPlayer);

        /// <summary>
        /// Asks, then reads, a voter with a body the walk can reach: a step of the walk's own, outside
        /// the one-decision accounting, because the approach is free roam. Each chance is taken once a
        /// season whether or not it could be taken; a campaign with no reachable voter is noted.
        /// </summary>
        private IEnumerator ExerciseSeasonRead(EpisodeState state, bool graphical)
        {
            bool asking = !seasonVoteAsked;
            // The chance is taken once the control is pressed, or given up after three approaches that
            // found nobody: a voter who is not interactable this step may be the next.
            void Consume() { if (asking) seasonVoteAsked = true; else seasonPersonRead = true; }
            void Missed() { if (++seasonReadMisses >= 3) { Consume(); seasonReadMisses = 0; } }
            string caption = asking ? EpisodeHud.AskVoteCaption : EpisodeHud.ReadPersonCaption;
            var voterIds = EpisodeEngine.Voters(state).Where(v => !v.isPlayer).Select(v => v.id).ToList();
            var bodies = seasonDirector.gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HouseNpc>(true))
                .Where(actor => actor.gameObject.activeInHierarchy && voterIds.Contains(actor.Id))
                .ToList();
            if (bodies.Count == 0) { seasonReport.readNote = "No voter had a body to approach for the read."; Missed(); seasonReadCommitted = false; yield break; }

            yield return CloseSeasonPanel();
            HouseNpc chosen = null; var approach = Vector3.zero;
            foreach (var candidate in bodies)
                if (FindSeasonNpcApproach(candidate, out approach)) { chosen = candidate; break; }
            if (chosen == null) { seasonReport.readNote = "No safe approach to a voter; the read was skipped."; Missed(); seasonReadCommitted = false; yield break; }
            if (!seasonPlayer.TryMoveTo(approach)) { seasonReport.readNote = "The read's approach was not accepted."; Missed(); seasonReadCommitted = false; yield break; }
            yield return WaitSeasonWalk("read conversation", approach, false);
            if (!seasonPlayer.HasArrived || Vector3.Distance(seasonPlayer.transform.position, approach) >= 1.1f)
            { seasonReport.readNote = "The read's approach did not complete within its route deadline."; Missed(); seasonReadCommitted = false; yield break; }
            if (!seasonDirector.TryOpenNpc(chosen.Id)) { seasonReport.readNote = chosen.Id + " was not interactable for the read."; Missed(); seasonReadCommitted = false; yield break; }
            yield return null; yield return null;
            if (!HasSeasonButtonText(caption))
            {
                seasonReport.readNote = "The conversation offered no \"" + caption + "\" to " + chosen.Id + ".";
                Consume();
                yield return CloseSeasonPanel();
                seasonReadCommitted = false; yield break;
            }
            Consume();
            var before = seasonDirector.Snapshot;
            yield return CaptureSeason(asking ? "vote-asked" : "person-read", graphical);
            yield return ClickSeasonButton(caption);
            var after = seasonDirector.Snapshot;
            RequireSeason(after.revision == before.revision + 1, "The read's control must commit exactly one command: " + caption);
            RequireSeason(EpisodeValidation.TryValidate(after, out var reason), "The projected season must remain valid after the read: " + reason);
            seasonReport.commands++;
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

        private bool seasonReadCommitted, seasonPlayTaken;

        /// <summary>
        /// Takes the play the Pull offers, once a season, and checks the notebook's Story section
        /// lists it under PLAYS. The play's first step opens on the scene card, where the walk's
        /// next decision answers it off the lapse like any story step.
        /// </summary>
        private IEnumerator TakeSeasonPlay(bool graphical)
        {
            seasonPlayTaken = true;
            var before = seasonDirector.Snapshot;
            yield return CaptureSeason("play-offered", graphical);
            yield return ClickSeasonButton(EpisodeHud.TakeItOnCaption);
            var after = seasonDirector.Snapshot;
            RequireSeason(after.revision > before.revision, "Taking a play on must commit.");
            RequireSeason(after.storylines.Any(x => EpisodeEngine.TakenOn(x)), "A play taken on is a play taken on.");
            seasonReport.playsTaken++;
            seasonReport.commands++;
            yield return CloseSeasonPanel();
            seasonDirector.ShowNotebookSection(EpisodeDirector.NotebookSection.Story);
            yield return null;
            RequireSeason(SeasonTextOnScreen(EpisodeDirector.PlaysHeading),
                "The notebook's Story section must list the play under " + EpisodeDirector.PlaysHeading + ".");
            yield return CaptureSeason("play-in-notebook", graphical);
            yield return CloseSeasonPanel();
        }

        /// <summary>Whether a label with exactly these words is on screen.</summary>
        private bool SeasonTextOnScreen(string words) => seasonDirector.GetComponentsInChildren<TMPro.TMP_Text>(true)
            .Any(text => text.isActiveAndEnabled && text.text == words);
    }
}
