using System.Collections.Generic;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// Pick, then commit (UI-UX-PLAN O1a, O1b; the owner's Q1): at the veto meeting, the eviction
    /// ballot on the episode screen and the final Head of Household's choice a press on a choice
    /// picks it, lit in place, and one button pinned under the screen names the result and commits
    /// it, the way the nomination screen's "Commit nominations" always has. The first press commits
    /// nothing; Escape or the pad's B clears a pick before it closes the screen; what a choice would
    /// break of the player's word is said for the pick before the commit. The command committed is
    /// the one the single press used to send. The diary room keeps its own review and Confirm.
    ///
    /// <para>And a look at the notebook from a decision (UI-UX-PLAN D5; the lead's L10): a notebook
    /// page opened over the nominations, the veto meeting, the eviction vote, the final decision or
    /// the final case has a way back to that screen at its top left, which Escape and B take, and the
    /// screen comes back with the player's picks as they were left. Opening the notebook still
    /// flushes the house's due time first, as every panel does, and so does coming back.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>The choice picked and not yet committed, or null. View state: never saved.</summary>
        private DecisionPick decisionPick;

        /// <summary>The decision screen the notebook was opened over, with its picks, while the notebook is up. View state.</summary>
        private DecisionPeek decisionPeek;

        /// <summary>What a decision screen held when the notebook was opened over it.</summary>
        private sealed class DecisionPeek
        {
            public string Place;
            public DecisionPick Pick;
            public string NominationFirst, NominationSecond, NominationOpenedBeat;
            public bool NominationBackdoorView, NominationPitchesDismissed;
            public int NominationViewWeek;
            public object PitchNavigationView;
            public bool FinalCaseOpen, FinalCaseEarly;
            public string ChosenTheme;
            public List<string> ChosenMoments;
        }

        /// <summary>The places the notebook's way back names, "Back to the veto meeting".</summary>
        public const string NominationsPlace = "the nominations", VetoMeetingPlace = "the veto meeting",
            EvictionVotePlace = "the eviction vote", FinalDecisionPlace = "the final decision", FinalCasePlace = "the final case";

        /// <summary>The caption of the commit waiting under the screen, or null with nothing picked. A read for tests and walks.</summary>
        public string DecisionPickCommitCaption => PickOn(projected)?.CommitCaption(projected);

        /// <summary>The way back the notebook shows while it is open over a decision, or null. A read for tests.</summary>
        public string DecisionPeekBackCaption => journalOpen && decisionPeek != null ? WayOut.BackTo(decisionPeek.Place) : null;

        /// <summary>The pick, while it still holds on <paramref name="state"/>; a pick that no longer does is dropped.</summary>
        private DecisionPick PickOn(EpisodeState state)
        {
            if (decisionPick != null && !decisionPick.Holds(state)) decisionPick = null;
            return decisionPick;
        }

        /// <summary>Whether a pick on <paramref name="screen"/> is waiting on <paramref name="state"/>: its own warning stands then, in place of every choice's.</summary>
        private bool Picked(EpisodeState state, DecisionPick.Screen screen) => PickOn(state)?.On == screen;

        /// <summary>Picks a choice: lit when the screen draws again, committed by nothing.</summary>
        private void PickDecision(DecisionPick pick)
        {
            decisionPick = pick;
            Render();
        }

        /// <summary>
        /// The commit under the screen for the pick waiting on <paramref name="state"/>, captioned by
        /// the result (<see cref="DecisionPick.CommitCaption"/>), pinned under the scroll where the way
        /// on stands on every other screen; nothing with no pick.
        /// </summary>
        private Button PinDecisionCommit(EpisodeState state)
        {
            var pick = PickOn(state);
            if (pick == null) return null;
            return hud.PinnedAction(pick.CommitCaption(state), () => CommitDecisionPick(state, pick));
        }

        /// <summary>Sends the pick's command, once: a second press of the same control finds the pick gone.</summary>
        private void CommitDecisionPick(EpisodeState state, DecisionPick pick)
        {
            if (pick == null || decisionPick != pick || !pick.Holds(state)) return;
            decisionPick = null;
            Commit(state, pick.Kind, pick.Target, pick.Second, pick.UseVeto);
        }

        /// <summary>What a choice picked would break of the player's word: the veto's or the ballot's own, or null with no pick on that screen.</summary>
        private string PickWarning(EpisodeState state, DecisionPick.Screen screen)
        {
            var pick = PickOn(state);
            if (pick == null || pick.On != screen) return null;
            switch (screen)
            {
                case DecisionPick.Screen.VetoMeeting:
                    return CommitmentsRead.Warning(state, DryRun(state, CommitmentsRead.Decision.Veto(pick.UseVeto, pick.Target, pick.Second)));
                case DecisionPick.Screen.Ballot: return BallotWarning(state, new[] { pick.Target });
                default: return null;
            }
        }

        /// <summary>
        /// Escape and B on a decision screen with a pick waiting: the pick goes, and the screen stays.
        /// False with no pick, a confirm card up, or no episode screen open, and the press goes on
        /// down the chain.
        /// </summary>
        private bool ClearDecisionPick()
        {
            if (!phaseOpen || hud == null || hud.ConfirmCancelAction != null || PickOn(projected) == null) return false;
            decisionPick = null;
            Render();
            return true;
        }

        /// <summary>What closing the panels forgets of the pick and of a look at the notebook.</summary>
        private void ForgetDecisionPick()
        {
            decisionPick = null;
            decisionPeek = null;
        }

        /// <summary>The decision screen up now, by the place the notebook's way back names; null when the panel is not one.</summary>
        private string DecisionPlace(EpisodeState s)
        {
            if (!phaseOpen || s == null) return null;
            if (finalCaseOpen) return FinalCasePlace;
            if (NominationSteps.Picking(s)) return NominationsPlace;
            if (s.phase == EpisodePhase.VetoMeeting && !s.vetoResolved
                && (s.vetoHolderId == s.playerId || s.hohId == s.playerId && EpisodeEngine.NpcVetoSave(s) != null)) return VetoMeetingPlace;
            if (DecisionPick.BallotLive(s)) return EvictionVotePlace;
            if (FinalChoiceBeat(s)) return FinalDecisionPlace;
            return null;
        }

        /// <summary>
        /// What the notebook keeps as it opens: the decision screen under it and its picks, when it is
        /// opened over one; the look already kept, when the notebook is open and turns to another
        /// page; nothing otherwise.
        /// </summary>
        private DecisionPeek KeepDecisionForNotebook()
        {
            if (!phaseOpen) return journalOpen ? decisionPeek : null;
            string place = DecisionPlace(projected);
            if (place == null) return null;
            return new DecisionPeek
            {
                Place = place, Pick = decisionPick,
                NominationFirst = nominationFirst, NominationSecond = nominationSecond, NominationOpenedBeat = nominationOpenedBeat,
                NominationBackdoorView = nominationBackdoorView, NominationPitchesDismissed = nominationPitchesDismissed,
                NominationViewWeek = nominationViewWeek, PitchNavigationView = pitchNavigationView,
                FinalCaseOpen = finalCaseOpen, FinalCaseEarly = finalCaseEarly, ChosenTheme = chosenTheme,
                ChosenMoments = new List<string>(chosenMoments),
            };
        }

        /// <summary>The notebook's way back to the decision it was opened over, at its top left; the page's own way back, drawn first, keeps the place.</summary>
        private void DecisionPeekWayBack()
        {
            if (!journalOpen || decisionPeek == null || hud == null) return;
            hud.WayBack(WayOut.BackTo(decisionPeek.Place), ReturnToDecision);
        }

        /// <summary>
        /// Back from the notebook to the decision it was opened over, as it was left: the episode
        /// screen open again with the picks, the nomination's view, the final case's choices. The
        /// house's due time is flushed first, as it is for every panel that opens; a pick it leaves
        /// no longer holding is dropped when the screen draws. Public for tests.
        /// </summary>
        public void ReturnToDecision()
        {
            var peek = decisionPeek;
            if (peek == null || !journalOpen) return;
            PauseNpcSocialForPanel();
            if (blockedRecovery) return;
            ClosePanels();
            phaseOpen = true;
            decisionPick = peek.Pick;
            nominationFirst = peek.NominationFirst; nominationSecond = peek.NominationSecond; nominationOpenedBeat = peek.NominationOpenedBeat;
            nominationBackdoorView = peek.NominationBackdoorView; nominationPitchesDismissed = peek.NominationPitchesDismissed;
            nominationViewWeek = peek.NominationViewWeek; pitchNavigationView = peek.PitchNavigationView;
            finalCaseOpen = peek.FinalCaseOpen; finalCaseEarly = peek.FinalCaseEarly; chosenTheme = peek.ChosenTheme;
            chosenMoments.Clear();
            if (peek.ChosenMoments != null) chosenMoments.AddRange(peek.ChosenMoments);
            if (player != null) player.SetInputEnabled(false);
            if (cameraRig != null) cameraRig.ControlsEnabled = false;
            Render();
        }
    }
}
