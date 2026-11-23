using System.Linq;
using Gamesim.Simulation;

namespace Gamesim.Presentation
{
    /// <summary>
    /// A choice the player has picked on a decision screen and not yet committed: the veto meeting,
    /// the eviction ballot on the episode screen, and the final Head of Household's choice
    /// (UI-UX-PLAN O1a and O1b, the owner's Q1). A press on a choice picks it, lit in place; one
    /// button under the screen names the result and commits it - the way the nomination screen has
    /// always worked with "Commit nominations". The command it sends is the command the one press
    /// used to send. Nothing here is saved: a pick is view state, and it holds only for the screen,
    /// the week and the season it was made on (<see cref="Holds"/>).
    /// </summary>
    public sealed class DecisionPick
    {
        /// <summary>The words every commit starts with, as the nominations' "Commit nominations" does.</summary>
        public const string CommitWord = "Commit · ";

        public enum Screen { VetoMeeting, Ballot, FinalChoice }

        public Screen On { get; private set; }
        public EpisodeCommandKind Kind { get; private set; }
        public string Target { get; private set; }
        public string Second { get; private set; }
        public bool UseVeto { get; private set; }
        private string sessionId;
        private int week;

        private DecisionPick() { }

        private static DecisionPick For(EpisodeState s, Screen on, EpisodeCommandKind kind, string target, string second, bool useVeto) =>
            new DecisionPick { On = on, Kind = kind, Target = target, Second = second, UseVeto = useVeto, sessionId = s?.sessionId, week = s != null ? s.week : -1 };

        /// <summary>The holder keeps the block: "Do not use the veto".</summary>
        public static DecisionPick KeepTheBlock(EpisodeState s) => For(s, Screen.VetoMeeting, EpisodeCommandKind.ResolveVeto, null, null, false);

        /// <summary>
        /// A save at the meeting: the holder saving <paramref name="saved"/> (the Head of Household then
        /// names the replacement), or with <paramref name="replacement"/> the player naming who goes up
        /// in their place - as Head of Household and holder, or as Head of Household after a houseguest's save.
        /// </summary>
        public static DecisionPick Save(EpisodeState s, string saved, string replacement = null) =>
            For(s, Screen.VetoMeeting, EpisodeCommandKind.ResolveVeto, saved, replacement, true);

        /// <summary>A ballot to evict <paramref name="target"/>.</summary>
        public static DecisionPick Ballot(EpisodeState s, string target) => For(s, Screen.Ballot, EpisodeCommandKind.CastVote, target, null, false);

        /// <summary>The final Head of Household's choice: <paramref name="evicted"/> joins the jury, the other finalist is taken.</summary>
        public static DecisionPick FinalChoice(EpisodeState s, string evicted) => For(s, Screen.FinalChoice, EpisodeCommandKind.FinalEvict, evicted, null, false);

        /// <summary>Whether this is the pick of <paramref name="target"/> (and <paramref name="second"/>, where a pick names two).</summary>
        public bool Is(EpisodeCommandKind kind, string target, string second = null, bool useVeto = false) =>
            Kind == kind && Target == target && Second == second && UseVeto == useVeto;

        /// <summary>
        /// Whether the pick can still be committed on <paramref name="s"/>: the same season and week,
        /// the screen's decision still open, and every name in it still one the screen offers. A pick
        /// that no longer holds is dropped, never committed.
        /// </summary>
        public bool Holds(EpisodeState s)
        {
            if (s == null || s.sessionId != sessionId || s.week != week) return false;
            switch (On)
            {
                case Screen.VetoMeeting: return VetoHolds(s);
                case Screen.Ballot: return BallotLive(s) && s.nominees.Contains(Target ?? "");
                case Screen.FinalChoice:
                    return s.phase == EpisodePhase.FinalEviction && s.hohId == s.playerId && Target != s.playerId
                        && s.Active.Any(actor => actor.id == Target);
                default: return false;
            }
        }

        private bool VetoHolds(EpisodeState s)
        {
            if (s.phase != EpisodePhase.VetoMeeting || s.vetoResolved) return false;
            bool holds = s.vetoHolderId == s.playerId, heads = s.hohId == s.playerId;
            if (!UseVeto) return holds && Target == null && Second == null;
            if (EpisodeEngine.VetoIsLockedAtFinalFour(s) || !s.nominees.Contains(Target ?? "")) return false;
            bool named = Second != null && EpisodeEngine.ReplacementCandidates(s).Any(candidate => candidate.id == Second);
            if (holds) return heads ? named : Second == null && EpisodeEngine.ReplacementCandidates(s).Any();
            return heads && named && EpisodeEngine.NpcVetoSave(s) == Target;
        }

        /// <summary>Whether the player has an eviction ballot to cast on <paramref name="s"/>: the episode screen's rule for drawing the ballot.</summary>
        public static bool BallotLive(EpisodeState s) =>
            s != null && s.Find(s.playerId)?.status == ContestantStatus.Active
            && s.phase == EpisodePhase.Eviction && !s.evictionResolved
            && (s.evictionStage == EvictionStage.Voting || s.evictionStage == EvictionStage.Tiebreaker)
            && !s.votes.Any(vote => vote.voterId == s.playerId)
            && (EpisodeEngine.Voters(s).Any(voter => voter.isPlayer) || EpisodeEngine.NeedsPlayerTieBreak(s));

        /// <summary>The commit's caption for this pick on <paramref name="s"/>: the result it commits, by name.</summary>
        public string CommitCaption(EpisodeState s)
        {
            switch (On)
            {
                case Screen.VetoMeeting: return VetoCommitCaption(s, UseVeto, Target, Second);
                case Screen.Ballot: return BallotCommitCaption(s, Target);
                default: return FinalCommitCaption(s, Target);
            }
        }

        /// <summary>
        /// The veto meeting's commit: "Commit · do not use the veto"; a holder's save, "Commit · save
        /// Emma Brown" (or "save yourself"); a save with the replacement named, "Commit · save Emma
        /// Brown, nominate Sam Lee"; and a Head of Household naming the replacement after a
        /// houseguest's save, "Commit · nominate Sam Lee as the replacement".
        /// </summary>
        public static string VetoCommitCaption(EpisodeState s, bool useVeto, string saved, string replacement)
        {
            if (!useVeto) return CommitWord + "do not use the veto";
            string whom = saved == s.playerId ? "yourself" : NameOf(s, saved);
            if (replacement == null) return CommitWord + "save " + whom;
            if (s.vetoHolderId != s.playerId) return CommitWord + "nominate " + NameOf(s, replacement) + " as the replacement";
            return CommitWord + "save " + whom + ", nominate " + NameOf(s, replacement);
        }

        /// <summary>The ballot's commit: "Commit · vote to evict Sam Lee", and at a tie the Head of Household's "Commit · cast the deciding vote to evict Sam Lee".</summary>
        public static string BallotCommitCaption(EpisodeState s, string target) =>
            CommitWord + (EpisodeEngine.NeedsPlayerTieBreak(s) ? "cast the deciding vote to evict " : "vote to evict ") + NameOf(s, target);

        /// <summary>
        /// The final choice's commit, the two halves the column's control says, by the names the page
        /// calls them (<see cref="FinalChoiceWords.Names"/>): "Commit · take Maya, evict Taylor". With
        /// other than two finalists to choose between, "Commit · evict Taylor Kim".
        /// </summary>
        public static string FinalCommitCaption(EpisodeState s, string evicted)
        {
            var others = FinalistRead.Others(s);
            var cut = others.FirstOrDefault(actor => actor.id == evicted);
            var take = others.Count == 2 ? others.FirstOrDefault(actor => actor.id != evicted) : null;
            if (cut == null || take == null) return CommitWord + "evict " + NameOf(s, evicted);
            var (taken, gone) = FinalChoiceWords.Names(take.name, cut.name);
            return CommitWord + "take " + taken + ", evict " + gone;
        }

        private static string NameOf(EpisodeState s, string id) => s?.Find(id)?.name ?? id ?? string.Empty;
    }
}
