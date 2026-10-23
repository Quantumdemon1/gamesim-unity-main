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
    /// conversation panel the player uses. Ineligible/known-owner/unreachable chances are explicit
    /// zero-coverage dispositions; a missing ordinary eligible leaf is a failure, never a skip.
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
        /// season only after a real commit or a legitimate finite disposition. A missing eligible
        /// leaf/no-op fails before consuming it; physical misses retain their three-approach bound.
        /// </summary>
        private IEnumerator ExerciseSeasonRead(EpisodeState state, bool graphical)
        {
            if (seasonVoteAsked && seasonPersonRead) yield break;
            bool asking = !seasonVoteAsked;
            string feature = asking ? "AskVote" : "ReadPerson";
            void Consume() { if (asking) seasonVoteAsked = true; else seasonPersonRead = true; }
            void Missed()
            {
                if (++seasonReadMisses < 3) return;
                Consume(); seasonReadMisses = 0;
            }
            string caption = asking ? EpisodeHud.AskVoteCaption : EpisodeHud.ReadPersonCaption;
            state = seasonDirector.Snapshot;
            string owner = SeasonConversationBlocker(state, seasonDirector.TalkingToId);
            if (owner != null)
            {
                seasonReport.readNote = "BlockedByOtherView: " + owner;
                RecordSeasonConversationAttempt(feature, "BlockedByOtherView", state, seasonDirector.TalkingToId, caption, owner);
                Consume(); yield break; // Finite scheduling, NOT a successful read.
            }
            var voterIds = EpisodeEngine.Voters(state).Where(v => !v.isPlayer
                && SeasonReadIneligible(state, v.id, asking) == null).Select(v => v.id).ToList();
            if (voterIds.Count == 0)
            {
                seasonReport.readNote = "Ineligible: no active voter eligible for this week's " + feature + ".";
                RecordSeasonConversationAttempt(feature, "Ineligible", state, null, caption, seasonReport.readNote);
                Consume(); yield break;
            }
            var bodies = seasonDirector.gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HouseNpc>(true))
                .Where(actor => actor.gameObject.activeInHierarchy && voterIds.Contains(actor.Id)).ToList();
            if (bodies.Count == 0)
            {
                seasonReport.readNote = "Unreachable: no eligible voter body.";
                RecordSeasonConversationAttempt(feature, "Unreachable", state, null, caption, seasonReport.readNote);
                Missed(); yield break;
            }
            yield return CloseSeasonPanel();
            HouseNpc chosen = null; var approach = Vector3.zero;
            foreach (var candidate in bodies)
                if (FindSeasonNpcApproach(candidate, out approach)) { chosen = candidate; break; }
            string missed = null;
            if (chosen == null) missed = "No safe eligible voter approach.";
            else if (!seasonPlayer.TryMoveTo(approach)) missed = "The approach was not accepted.";
            else
            {
                yield return WaitSeasonWalk("read conversation", approach, false);
                if (!seasonPlayer.HasArrived || Vector3.Distance(seasonPlayer.transform.position, approach) >= 1.1f)
                    missed = "The route deadline expired.";
                else if (!seasonDirector.TryOpenNpc(chosen.Id)) missed = "The reached voter was not interactable.";
            }
            if (missed != null)
            {
                seasonReport.readNote = "Unreachable: " + missed;
                RecordSeasonConversationAttempt(feature, "Unreachable", seasonDirector.Snapshot, chosen != null ? chosen.Id : null, caption, missed);
                Missed(); yield break;
            }
            yield return null; yield return null;
            yield return DrainSeasonConversationExchange("before-read", false);
            var before = seasonDirector.Snapshot;
            string ineligible = SeasonReadIneligible(before, chosen.Id, asking);
            owner = SeasonConversationBlocker(before, chosen.Id);
            if (ineligible != null || owner != null)
            {
                seasonReport.readNote = owner != null ? "BlockedByOtherView: " + owner : "Ineligible: " + ineligible;
                RecordSeasonConversationAttempt(feature, owner != null ? "BlockedByOtherView" : "Ineligible",
                    before, chosen.Id, caption, owner ?? ineligible);
                Consume(); yield return CloseSeasonPanel(); yield break;
            }
            yield return EnterSeasonConversationCategory(EpisodeDirector.LearnCategoryCaption);
            RequireSeasonConversationLeaf(feature, chosen.Id, EpisodeDirector.LearnCategoryCaption, caption);
            before = seasonDirector.Snapshot;
            var baseline = ConversationBaseline();
            yield return CaptureSeason(asking ? "vote-asked" : "person-read", graphical);
            RequireConversationUnchanged(baseline, "Capturing the available read");
            yield return ClickSeasonButton(caption);
            var after = seasonDirector.Snapshot;
            RequireSeasonConversationCommit(before, after, baseline, false, feature);
            RequireSeason(EpisodeDirector.CommittedEvents(after, before.nextSequence).Any(e => e.kind == "vote-read")
                && (asking ? EpisodeEngine.AskedThisWeek(after, chosen.Id) : EpisodeEngine.ReadThisWeek(after, chosen.Id)),
                "The read must record this exact person's actual weekly attempt: " + caption);
            // Missing/no-op eligible leaves throw BEFORE consuming their attempt or claiming coverage.
            Consume(); seasonReadMisses = 0;
            seasonReport.commands++;
            if (asking) seasonReport.votesAsked++; else seasonReport.peopleRead++;
            seasonReport.readNote = "Committed: one free " + feature + " to " + chosen.Id + ".";
            RecordSeasonConversationAttempt(feature, "Committed", before, chosen.Id, caption, seasonReport.readNote, after);
            yield return DrainSeasonConversationExchange(asking ? "vote-answer" : "person-reading", graphical);
            yield return CloseSeasonPanel();
        }

        private static string SeasonReadIneligible(EpisodeState state, string npcId, bool asking)
        {
            if (state.Find(state.playerId)?.status != ContestantStatus.Active) return "The player is not active.";
            if (state.Find(npcId)?.status != ContestantStatus.Active || npcId == state.playerId) return "No active NPC.";
            if (asking)
            {
                if (!VoteRead.Available(state) || !EpisodeEngine.Voters(state).Any(v => v.id == npcId))
                    return "No current vote for this voter.";
                return EpisodeEngine.AskedThisWeek(state, npcId) ? "Already asked this voter this week." : null;
            }
            if (state.phase != EpisodePhase.Social && state.phase != EpisodePhase.Campaign) return "Not a reading window.";
            return EpisodeEngine.ReadThisWeek(state, npcId) ? "Already read this housemate this week." : null;
        }

        private bool seasonPlayTaken;

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
