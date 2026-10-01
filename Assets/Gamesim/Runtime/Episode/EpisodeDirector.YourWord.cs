using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>
    /// Your word (ACTIONS-DEALS-ALLIANCES-PLAN V1): the notebook page of every commitment the player
    /// is a party to (<see cref="CommitmentsRead"/>), and the warnings the decision screens give
    /// before a choice breaks one - the Head of Household's picks, the veto, the ballot and the
    /// final choice.
    ///
    /// <para>Every warning is a dry run (<see cref="CommitmentsRead.WouldBreak"/>): the engine on a
    /// copy of the season, so nothing is committed, the season's revision does not move and its
    /// random stream is not drawn from. Nothing is run at all when the player has nothing standing
    /// that the decision settles, which is most of the time. A sweep of every replacement a screen
    /// offers is judged by the rules on one copy (<see cref="CommitmentsRead.ByTheRules(EpisodeState, IEnumerable{CommitmentsRead.Decision})"/>),
    /// which the EditMode tests hold to the engine, rather than one engine run a name; the engine
    /// runs only for the few choices a screen commits as they stand. A render repeats its dry runs,
    /// so they are kept for the state they were run on and thrown away when a commit replaces it.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>The Your word page's door on the notes page, and its way back: captions of their own.</summary>
        public const string YourWordCaption = "Your word", BackToYourNotesCaption = "Back to your notes";

        /// <summary>How many commitments a houseguest's card shows before it counts the rest.</summary>
        private const int WordShown = 8;

        // ------------------------------------------------------------ the dry runs

        /// <summary>The state the dry runs below were run on, and what each decision would break on it.</summary>
        private EpisodeState breachState;
        private readonly Dictionary<string, List<CommitmentsRead.Breach>> breachRuns = new Dictionary<string, List<CommitmentsRead.Breach>>(StringComparer.Ordinal);

        /// <summary>
        /// What a decision would break, from the dry run on this very state: run once, and kept while
        /// the HUD keeps drawing from it. A commit makes a new state and empties the store.
        /// </summary>
        private List<CommitmentsRead.Breach> DryRun(EpisodeState state, CommitmentsRead.Decision decision) =>
            decision == null ? new List<CommitmentsRead.Breach>() : Kept(state, DecisionKey(decision), () => CommitmentsRead.WouldBreak(state, decision));

        /// <summary>What any of several decisions would break, by the rules on one copy of this very state: a sweep, run once and kept like a dry run.</summary>
        private List<CommitmentsRead.Breach> RulesRun(EpisodeState state, IEnumerable<CommitmentsRead.Decision> decisions)
        {
            var list = (decisions ?? Enumerable.Empty<CommitmentsRead.Decision>()).Where(d => d != null).ToList();
            if (list.Count == 0) return new List<CommitmentsRead.Breach>();
            return Kept(state, "rules|" + string.Join(";", list.Select(DecisionKey)), () => CommitmentsRead.ByTheRules(state, list));
        }

        private List<CommitmentsRead.Breach> Kept(EpisodeState state, string key, Func<List<CommitmentsRead.Breach>> run)
        {
            if (state == null) return new List<CommitmentsRead.Breach>();
            if (!ReferenceEquals(breachState, state)) { breachRuns.Clear(); breachState = state; }
            if (!breachRuns.TryGetValue(key, out var breaches)) breachRuns[key] = breaches = run();
            return breaches;
        }

        private static string DecisionKey(CommitmentsRead.Decision decision) =>
            decision.kind + "|" + decision.firstId + "|" + decision.secondId + "|" + decision.useVeto;

        /// <summary>The Head of Household's picks, as a review warns of them: null until two different names are picked, and when they break nothing.</summary>
        public string NominationWarning(EpisodeState state, string first, string second) =>
            NominationBreach(state, first, second, out _);

        /// <summary>
        /// The picker's footer warning for two picks - the dry run's words - and the short form the
        /// strip falls back to when those and what committing lets pass cannot share it: how many of
        /// the player's commitments the picks break, and how many storylines committing lets pass.
        /// Nulls until two different names are picked, and when they break nothing.
        /// </summary>
        private string NominationBreach(EpisodeState state, string first, string second, out string brief)
        {
            brief = null;
            if (state == null || string.IsNullOrEmpty(first) || string.IsNullOrEmpty(second) || first == second) return null;
            var breaches = DryRun(state, CommitmentsRead.Decision.Nominate(first, second));
            string words = CommitmentsRead.Warning(state, breaches);
            if (words == null) return null;
            int lapsing = NominationSteps.LapsingOnNominate(state).Count;
            brief = "Committing these breaks " + CommitmentsRead.CountOf(breaches) + " of your commitments (see " + YourWordCaption + ")"
                + (lapsing > 0 ? " and lets " + (lapsing == 1 ? "1 storyline" : lapsing + " storylines") + " pass" : "") + ".";
            return words;
        }

        /// <summary>The picker's picks, warned of in the footer's strip in place: its words, or its short form where they cannot share the strip.</summary>
        private void WarnOfThePicks(EpisodeState state, string first, string second)
        {
            string words = NominationBreach(state, first, second, out string brief);
            hud.FooterBreachWarning(words, brief);
        }

        /// <summary>
        /// The veto meeting's warning, over every choice the screen offers: the holder's saves and
        /// keeping the block; a Head of Household who holds it, each replacement under the nominee
        /// picked to save; a Head of Household naming the replacement for a houseguest's save, each
        /// name. Null when no choice breaks anything - at once, with no run at all, when the player
        /// has nothing standing that a veto settles.
        /// </summary>
        public string VetoWarning(EpisodeState state, string savePick)
        {
            if (state == null || state.phase != EpisodePhase.VetoMeeting || state.vetoResolved) return null;
            if (!CommitmentsRead.AtStake(state, CommitmentsRead.DecisionKinds.Veto)) return null;
            var breaches = new List<CommitmentsRead.Breach>();
            bool holds = state.vetoHolderId == state.playerId, heads = state.hohId == state.playerId;
            var candidates = EpisodeEngine.ReplacementCandidates(state).Select(c => c.id).ToList();
            bool usable = !EpisodeEngine.VetoIsLockedAtFinalFour(state) && candidates.Count > 0;
            if (holds)
            {
                // Every replacement under the nominee picked to save, by the rules on one copy.
                if (usable && heads)
                {
                    if (state.nominees.Contains(savePick ?? ""))
                        breaches.AddRange(RulesRun(state, candidates.Select(replacement => CommitmentsRead.Decision.Veto(true, savePick, replacement))));
                }
                else if (usable)
                    foreach (string nominee in state.nominees)
                        breaches.AddRange(DryRun(state, CommitmentsRead.Decision.Veto(true, nominee)));
                breaches.AddRange(DryRun(state, CommitmentsRead.Decision.Veto(false)));
            }
            else if (heads)
            {
                // Every name the Head of Household can put up for a houseguest's save, likewise.
                string saved = EpisodeEngine.NpcVetoSave(state);
                if (saved != null)
                    breaches.AddRange(RulesRun(state, candidates.Select(replacement => CommitmentsRead.Decision.Veto(true, saved, replacement))));
            }
            return CommitmentsRead.Warning(state, breaches);
        }

        /// <summary>The ballot's warning: what voting out each of <paramref name="targets"/> would break, judged on the player's own ballot. Null when nothing.</summary>
        public string BallotWarning(EpisodeState state, IEnumerable<string> targets)
        {
            if (state == null || targets == null) return null;
            var breaches = new List<CommitmentsRead.Breach>();
            foreach (string target in targets) breaches.AddRange(DryRun(state, CommitmentsRead.Decision.Vote(target)));
            return CommitmentsRead.Warning(state, breaches);
        }

        /// <summary>
        /// A diary draft's warning: the one decision under review - a nomination, a veto or a ballot
        /// - as the screen it came from would have warned of it. Null for any other draft.
        /// </summary>
        private string DraftWarning(DiaryDecisionDraft draft)
        {
            var state = draft?.origin;
            if (state == null) return null;
            switch (draft.kind)
            {
                case EpisodeCommandKind.Nominate: return NominationWarning(state, draft.target, draft.second);
                case EpisodeCommandKind.ResolveVeto:
                    return CommitmentsRead.Warning(state, DryRun(state, CommitmentsRead.Decision.Veto(draft.useVeto, draft.target, draft.second)));
                case EpisodeCommandKind.CastVote: return BallotWarning(state, new[] { draft.target });
                default: return null;
            }
        }

        /// <summary>
        /// The final choice's warnings, a column each: what taking <paramref name="take"/> breaks
        /// and taking the other does not. What either choice breaks - a Final 2 agreement with a
        /// juror - is the screen's footnote already, said once rather than over both controls.
        /// </summary>
        private string FinalChoiceBreach(EpisodeState state, ContestantState take, ContestantState cut)
        {
            if (state == null || take == null || cut == null) return null;
            var taking = DryRun(state, CommitmentsRead.Decision.FinalEviction(cut.id));
            var other = DryRun(state, CommitmentsRead.Decision.FinalEviction(take.id));
            var eitherWay = new HashSet<string>(other.Select(b => b.kind + ":" + b.id), StringComparer.Ordinal);
            return CommitmentsRead.Warning(state, taking.Where(b => !eitherWay.Contains(b.kind + ":" + b.id)));
        }

        // ------------------------------------------------------------ the page

        /// <summary>
        /// The Your word page: every commitment the player is a party to, the open ones first and
        /// then the settled ones with how each ended, a card a houseguest in each. A door back to the
        /// notes in the page's head, and the notebook's own foot.
        /// </summary>
        private void RenderNotebookWord(EpisodeState state)
        {
            hud.PageDoor(BackToYourNotesCaption, () => ShowNotebookSection(NotebookSection.Notes));
            // The mark exists in every state: it is what the notebook scrolls to.
            hud.Mark(NotebookSection.Word);
            var all = CommitmentsRead.Of(state);
            if (all.Count == 0)
                hud.EmptyState("No commitments", PackArt.KitEmptyPrivate, "Nobody has your word yet, and you have nobody's.",
                    "Promises, deals, loyalty oaths and the calls you make in an alliance land here as you make them.");
            else
            {
                WordSection(state, "STILL OPEN", EpisodeHud.WordOpenCardPrefix, all.Where(c => c.IsOpen).ToList());
                WordSection(state, "SETTLED", EpisodeHud.WordSettledCardPrefix, all.Where(c => !c.IsOpen).ToList());
            }
            hud.NotebookFooter("Only what you are a party to, from the season's own record. Who broke a deal is said only where the record shows it.",
                "House activities", OpenHouseActivities);
        }

        /// <summary>One section of the page: its eyebrow and a card for each houseguest with something in it, in the house's order.</summary>
        private void WordSection(EpisodeState state, string heading, string prefix, List<CommitmentsRead.Commitment> items)
        {
            if (items.Count == 0) return;
            hud.Eyebrow(heading + " · " + items.Count, UiTheme.Muted);
            foreach (var actor in state.contestants.Where(c => !c.isPlayer))
            {
                var theirs = items.Where(c => c.withId == actor.id).ToList();
                if (theirs.Count == 0) continue;
                var lines = theirs.Take(WordShown)
                    .Select(c => new EpisodeHud.WordLine(CommitmentsRead.Line(c), WordInk(c.outcome))).ToList();
                hud.WordCard(prefix, actor, StatusWord(actor.status), lines, Math.Max(0, theirs.Count - WordShown));
            }
        }

        /// <summary>How a commitment ended, in a colour as well as its words: open in the paper, kept in the allied green, broken in the conflict red, lapsed muted.</summary>
        public static Color WordInk(string outcome)
        {
            switch (outcome)
            {
                case CommitmentsRead.Outcomes.Kept: return UiTheme.Allied;
                case CommitmentsRead.Outcomes.Broken: return UiTheme.Conflict;
                case CommitmentsRead.Outcomes.Lapsed: return UiTheme.Muted;
                default: return UiTheme.Paper;
            }
        }
    }
}
