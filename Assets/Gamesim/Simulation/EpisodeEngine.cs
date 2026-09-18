using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Single-writer native episode authority. Commands commit a detached candidate or no change.
    /// Web-compatible arithmetic lives in WebRules; native slice policies are recorded separately.
    /// </summary>
    public sealed partial class EpisodeEngine
    {
        private EpisodeState current;
        public EpisodeState Snapshot => current.Clone();

        public EpisodeEngine(EpisodeState initial)
        {
            if (!EpisodeValidation.TryValidate(initial, out var error)) throw new ArgumentException(error, nameof(initial));
            current = initial.Clone();
        }

        public CommandResult Apply(EpisodeCommand command)
        {
            if (command == null || string.IsNullOrWhiteSpace(command.id) || command.id.Length > 160)
                return Rejected("A bounded command ID is required.");
            if (current.acceptedCommandIds.Contains(command.id)) return new CommandResult
                { duplicate = true, reason = "This command was already committed.", state = Snapshot };
            if (command.expectedRevision != current.revision || command.expectedPhase != current.phase)
                return Rejected("The episode changed. Refresh the decision before submitting it.");
            if (command.actorId != current.playerId) return Rejected("Only the local player's command channel is accepted.");
            if (!Enum.IsDefined(typeof(EpisodeCommandKind), command.kind)) return Rejected("Unknown command.");
            if (command.text != null && (command.text.Length > 2000 || command.text.Any(ch => char.IsControl(ch) && ch != '\n' && ch != '\r' && ch != '\t')))
                return Rejected("Text must be at most 2,000 characters without unsupported control characters.");
            if (double.IsNaN(command.performance) || double.IsInfinity(command.performance) || command.performance < 0 || command.performance > 1)
                return Rejected("Competition input must be between zero and one.");
            var next = current.Clone();
            try
            {
                Execute(next, command);
                CancelInvalidNpcConversations(next);
                next.revision = checked(current.revision + 1);
                next.acceptedCommandIds.Add(command.id);
                if (next.acceptedCommandIds.Count > 256) next.acceptedCommandIds.RemoveAt(0);
                if (!EpisodeValidation.TryValidate(next, out var error)) throw new RuleException("Candidate rejected: " + error);
                current = next;
                return new CommandResult { accepted = true, reason = "Committed", state = Snapshot };
            }
            catch (RuleException error) { return Rejected(error.Message); }
        }

        private CommandResult Rejected(string reason) => new CommandResult { reason = reason, state = Snapshot };

        private static void Execute(EpisodeState s, EpisodeCommand c)
        {
            Require(s.phase != EpisodePhase.Finished, "This season is complete. Start a new season to play again.");
            switch (c.kind)
            {
                case EpisodeCommandKind.Advance: Advance(s); break;
                case EpisodeCommandKind.Compete:
                    Require(IsCompetition(s.phase) && !s.competitionResolved, "No unresolved competition is available.");
                    ResolveCompetition(s, c.performance); break;
                case EpisodeCommandKind.Nominate:
                    Require(s.phase == EpisodePhase.Nomination && s.hohId == s.playerId, "Only the reigning HoH chooses nominees.");
                    Nominate(s, c.targetId, c.secondTargetId); break;
                case EpisodeCommandKind.ResolveVeto:
                    Require(s.phase == EpisodePhase.VetoMeeting && !s.vetoResolved, "No veto decision is pending.");
                    Require(s.vetoHolderId == s.playerId || s.hohId == s.playerId, "The veto holder or replacement-selecting HoH must act.");
                    if (s.vetoHolderId != s.playerId)
                    {
                        var saved = NpcVetoSave(s);
                        Require(saved != null && c.useVeto && c.targetId == saved, "The NPC veto decision must be preserved.");
                    }
                    ResolveVeto(s, c.useVeto, c.targetId, c.secondTargetId); break;
                case EpisodeCommandKind.CastVote:
                    if (s.phase == EpisodePhase.Jury)
                    {
                        Require(s.Find(s.playerId).status == ContestantStatus.Jury || s.Find(s.playerId).status == ContestantStatus.Evicted, "Only jurors vote for a winner.");
                        Require(s.Active.Any(x => x.id == c.targetId), "Choose a finalist.");
                        Require(!s.votes.Any(v => v.voterId == s.playerId), "Your jury vote is already committed.");
                        s.votes.Add(new VoteState { voterId = s.playerId, targetId = c.targetId, reason = "Player's jury vote" });
                        break;
                    }
                    Require(s.phase == EpisodePhase.Eviction, "Voting is not open.");
                    Require(!s.evictionResolved, "The eviction is already complete.");
                    // The house votes at the voting stage, not before it. Without this a ballot could
                    // be cast while the nominees were still speaking — which is both out of order and
                    // the one thing that can make a save's stage disagree with its own votes.
                    Require(s.evictionStage == EvictionStage.Voting || s.evictionStage == EvictionStage.Tiebreaker,
                        "The nominees are still speaking. The house votes after that.");
                    Require(Voters(s).Any(x => x.id == s.playerId) || NeedsPlayerTieBreak(s), "You are not eligible to vote now.");
                    Vote(s, s.playerId, c.targetId, "Player's decision"); break;
                case EpisodeCommandKind.SubmitEvictionSpeech:
                    Require(s.phase == EpisodePhase.Eviction && s.evictionStage == EvictionStage.Speeches,
                        "The nominees are not speaking right now.");
                    Require(s.nominees.Contains(s.playerId), "Only a nominee speaks from the block.");
                    Require(!s.evictionSpeeches.Any(x => x.speakerId == s.playerId), "Your speech is already committed.");
                    s.evictionSpeeches.Add(new EvictionSpeechState
                    {
                        speakerId = s.playerId, week = s.week, isPlayerAuthored = true,
                        text = (c.text ?? string.Empty).Trim(),
                    });
                    Log(s, "eviction-speech", string.IsNullOrWhiteSpace(c.text)
                        ? "You let your game speak for itself."
                        : "You addressed the house from the block.");
                    break;
                case EpisodeCommandKind.SetBackdoorPlan: SetBackdoorPlan(s, s.Find(c.targetId)); break;
                case EpisodeCommandKind.FinalEvict:
                    Require(s.phase == EpisodePhase.FinalEviction && s.hohId == s.playerId, "Only the final HoH makes this choice.");
                    FinalEvict(s, c.targetId); break;
                case EpisodeCommandKind.AnswerJury: AnswerJury(s, c); break;
                case EpisodeCommandKind.SkipQuestioning:
                    Require(s.phase == EpisodePhase.JuryQuestioning, "Jury questioning is not open.");
                    if (!s.juryExchanges[s.juryQuestionIndex].completed) s.juryExchanges.RemoveAt(s.juryQuestionIndex);
                    s.juryQuestionIndex = s.juryExchanges.Count;
                    BeginFinalSpeeches(s); break;
                case EpisodeCommandKind.SubmitSpeech:
                    Require(s.phase == EpisodePhase.FinalSpeeches && s.Active.Any(x => x.isPlayer), "Only a finalist can submit a final speech.");
                    Require(!s.finalSpeeches.Any(x => x.speakerId == s.playerId), "Your final speech is already committed.");
                    s.finalSpeeches.Add(new FinalSpeechState { speakerId = s.playerId, text = (c.text ?? "").Trim(), isPlayerAuthored = true });
                    Log(s, "final-speech", string.IsNullOrWhiteSpace(c.text) ? "You chose to let your game speak for itself." : "You delivered your final speech.");
                    break;
                case EpisodeCommandKind.StudyHouse: StudyHouse(s, c); break;
                case EpisodeCommandKind.SimulateCompetition: SimulateWeeklyCompetition(s, c); break;
                case EpisodeCommandKind.ReflectDiary: ReflectDiary(s, c); break;
                case EpisodeCommandKind.SkipDiary: ResolveDiary(s, c, false); break;
                case EpisodeCommandKind.SwearLoyalty: ResolveOathOpportunity(s, c, true); break;
                case EpisodeCommandKind.DeclineLoyalty: ResolveOathOpportunity(s, c, false); break;
                default: Social(s, c); break;
            }
        }

        public static bool IsCompetition(EpisodePhase phase) => phase == EpisodePhase.HoH || phase == EpisodePhase.Veto ||
            phase == EpisodePhase.FinalHoHPart1 || phase == EpisodePhase.FinalHoHPart2 || phase == EpisodePhase.FinalHoHPart3;

        public static IEnumerable<ContestantState> CompetitionPlayers(EpisodeState s)
        {
            if (s.phase == EpisodePhase.Veto) return s.Active.Where(c => s.vetoPlayers.Contains(c.id));
            if (s.phase == EpisodePhase.FinalHoHPart2) return s.Active.Where(c => c.id != s.finalPart1WinnerId);
            if (s.phase == EpisodePhase.FinalHoHPart3) return s.Active.Where(c => c.id == s.finalPart1WinnerId || c.id == s.finalPart2WinnerId);
            return s.Active.Where(c => s.Active.Count() <= 3 || c.id != s.previousHohId);
        }

        private static void Advance(EpisodeState s)
        {
            switch (s.phase)
            {
                case EpisodePhase.Social:
                    Require(s.pendingDiary == null, "Visit the Diary Room or skip the pending reflection before beginning the next competition.");
                    if (s.evictionResolved)
                    {
                        s.previousHohId = s.hohId; s.week++; s.hohId = null; s.vetoHolderId = null;
                        s.nominees.Clear(); s.vetoPlayers.Clear(); s.votes.Clear(); s.evictionSpeeches.Clear();
                        s.backdoorTargetId = null;   // A plan for a week that has ended is not a plan.
                        s.evictionResolved = false; s.vetoResolved = false; s.competitionScores.Clear();
                        foreach (var promise in s.promises.Where(p => p.status == PromiseStatus.Active && p.expiresWeek > 0 && p.expiresWeek < s.week))
                            promise.status = PromiseStatus.Expired;
                    }
                    s.socialActions = 0; s.outOfPhaseSocialActions = 0; s.competitionResolved = false;
                    Phase(s, s.Active.Count() == 3 ? EpisodePhase.FinalHoHPart1 : EpisodePhase.HoH); break;
                case EpisodePhase.HoH:
                case EpisodePhase.Veto:
                case EpisodePhase.FinalHoHPart1:
                case EpisodePhase.FinalHoHPart2:
                case EpisodePhase.FinalHoHPart3:
                    if (!s.competitionResolved)
                    {
                        Require(!CompetitionPlayers(s).Any(p => p.id == s.playerId), "Enter the competition before continuing.");
                        ResolveCompetition(s, 0); return;
                    }
                    var next = s.phase == EpisodePhase.HoH ? EpisodePhase.Nomination : s.phase == EpisodePhase.Veto ? EpisodePhase.VetoMeeting :
                        s.phase == EpisodePhase.FinalHoHPart1 ? EpisodePhase.FinalHoHPart2 : s.phase == EpisodePhase.FinalHoHPart2 ? EpisodePhase.FinalHoHPart3 : EpisodePhase.FinalEviction;
                    s.competitionResolved = false; Phase(s, next); break;
                case EpisodePhase.Nomination:
                    if (s.nominees.Count == 0)
                    {
                        Require(s.hohId != s.playerId, "Choose two nominees first.");
                        var weakest = NominationCandidates(s).OrderBy(c => s.Score(s.hohId, c.id)).Take(2).ToArray();
                        Nominate(s, weakest[0].id, weakest[1].id); return;
                    }
                    Phase(s, EpisodePhase.VetoSelection); break;
                case EpisodePhase.VetoSelection:
                    s.vetoPlayers = DrawVetoPlayers(s);
                    Log(s, "veto-selection", s.vetoPlayers.Count >= s.Active.Count()
                        ? "Everyone remaining in the house is eligible for the veto competition."
                        : "Veto players drawn: " + string.Join(", ", s.vetoPlayers.Select(id => Name(s, id))) + ".");
                    Phase(s, EpisodePhase.Veto); break;
                case EpisodePhase.VetoMeeting:
                    if (!s.vetoResolved)
                    {
                        Require(s.vetoHolderId != s.playerId, "Choose whether to use the veto first.");
                        var saved = NpcVetoSave(s);
                        Require(saved == null || s.hohId != s.playerId, "The veto will be used. As HoH, choose the replacement nominee.");
                        var replacement = saved == null ? null : ReplacementCandidates(s).OrderBy(c => s.Score(s.hohId, c.id)).First().id;
                        ResolveVeto(s, saved != null, saved, replacement); return;
                    }
                    // A nominee begging for votes and somebody courting the Head of Household are
                    // both positional, so they only make sense once the block is settled. This is
                    // that moment.
                    Phase(s, EpisodePhase.Campaign); NpcPromises.Settle(s); break;
                case EpisodePhase.Campaign:
                    // Campaigning IS the reference build's interaction stage — last conversations
                    // and vote-wrangling — so eviction night opens on the speeches rather than
                    // repeating a stage the season has just spent a whole phase on.
                    s.evictionStage = EvictionStage.Speeches;
                    // This sentence is recorded verbatim in the frozen voting-bloc witness. The
                    // speeches announce themselves in their own entries; rewording a committed line
                    // to describe a new stage would break a replay comparison for no gain.
                    Log(s, "campaign-close", "Campaigning has closed. The house votes privately to evict.");
                    Phase(s, EpisodePhase.Eviction); break;
                case EpisodePhase.Eviction:
                    if (s.evictionResolved)
                    {
                        s.evictionStage = EvictionStage.Interaction;
                        Phase(s, EpisodePhase.Social);
                        // The house takes stock as the social week opens: pacts that have soured
                        // fall apart, people who want to work together start doing so, and words are
                        // given. Neither pass draws randomness, so neither can shift the generator.
                        NpcAlliances.Settle(s);
                        NpcPromises.Settle(s);
                        return;
                    }
                    // Eviction night runs as stages inside this phase rather than as phases of its
                    // own, because EpisodePhase ordinals are frozen into every historical save.
                    // Interaction is reachable only on a save that pre-dates the staging, whose
                    // migration reads the stage from its ballots. Treat it as the speeches, which is
                    // where a night that has not voted yet actually stands.
                    if (s.evictionStage == EvictionStage.Interaction) s.evictionStage = EvictionStage.Speeches;
                    if (s.evictionStage == EvictionStage.Speeches)
                    {
                        SpeakFromTheBlock(s);
                        Require(s.evictionSpeeches.Count >= s.nominees.Count,
                            "Deliver your speech from the block before the house votes.");
                        s.evictionStage = EvictionStage.Voting;
                        Log(s, "eviction-stage", "The house votes privately to evict.");
                        return;
                    }
                    int votesBefore = s.votes.Count;
                    var missingNpcVoters = Voters(s).Where(c => c.id != s.playerId && !s.votes.Any(v => v.voterId == c.id)).ToArray();
                    // Plan the entire missing batch before recording any ballot or settling consequences.
                    var coordinatedVotes = PrepareBlocBallots(s, missingNpcVoters.Select(voter => voter.id).ToArray());
                    foreach (var voter in missingNpcVoters)
                    {
                        var evaluation = coordinatedVotes == null ? WebEvictionVoting.EvaluateNative(s, voter.id) : coordinatedVotes[voter.id];
                        Vote(s, voter.id, evaluation.selectedNomineeId, WebEvictionVoting.ExplainNative(s, evaluation));
                    }
                    if (!Voters(s).All(c => s.votes.Any(v => v.voterId == c.id)))
                    {
                        if (s.votes.Count > votesBefore) return;
                        throw new RuleException("Cast your eviction vote first.");
                    }
                    var tally = s.nominees.Select(id => new { id, count = s.votes.Count(v => v.targetId == id && v.voterId != s.hohId) }).ToArray();
                    string evicted;
                    if (tally[0].count == tally[1].count)
                    {
                        if (!s.votes.Any(v => v.voterId == s.hohId))
                        {
                            if (s.hohId == s.playerId)
                            {
                                s.evictionStage = EvictionStage.Tiebreaker;
                                if (s.votes.Count > votesBefore) return;
                                throw new RuleException("The vote is tied. Cast the HoH tie-break vote.");
                            }
                            var evaluation = EvaluateRegularHohTieBreak(s);
                            Vote(s, s.hohId, evaluation.selectedNomineeId, "HoH tie-break: " + WebEvictionVoting.ExplainNative(s, evaluation));
                        }
                        evicted = s.votes.Single(v => v.voterId == s.hohId).targetId;
                    }
                    else evicted = tally.OrderByDescending(x => x.count).First().id;
                    // Resolve oath vote consequences only at the public reveal. Pending private ballots
                    // must not disclose themselves via a breach log or influence this round's remaining voters.
                    foreach (var vote in s.votes)
                    {
                        foreach (var promise in s.promises.Where(p => p.status == PromiseStatus.Active && p.kind == PromiseKind.Vote && p.fromId == vote.voterId).ToArray())
                            SettlePromise(s, promise, promise.targetId == vote.targetId ? PromiseStatus.Fulfilled : PromiseStatus.Broken);
                        ApplyOathPlan(s, WebLoyaltyOaths.EvictionVote(OathSnapshot(s), vote.voterId, vote.targetId));
                    }
                    s.Find(evicted).status = ContestantStatus.Jury; s.evictionResolved = true;
                    s.evictionStage = EvictionStage.Results;
                    s.jurySentiment = WebJurySentiment.AddJuror(s.jurySentiment, evicted, Name(s, evicted), s.Score(s.playerId, evicted));
                    s.oathOpportunities.Remove(evicted);
                    Log(s, "eviction", Name(s, evicted) + Verb(s, evicted, " is evicted and joins ", " are evicted and join ")
                        + "the jury.");
                    foreach (var vote in s.votes) Log(s, "vote-reveal", Name(s, vote.voterId) + " voted to evict "
                        + Target(s, vote.targetId, vote.voterId) + ". " + vote.reason);
                    PreparePostEvictionDiary(s, evicted);
                    break;
                case EpisodePhase.FinalEviction:
                    Require(s.hohId != s.playerId, "Choose the final eviction first.");
                    var finalContext = s.Clone();
                    finalContext.nominees = s.Active.Where(c => c.id != s.hohId).Select(c => c.id).ToList();
                    var finalOptions = WebEvictionVoting.FromNative(finalContext, s.hohId);
                    // Match the source fast-forward final-selection caller, which supplies no memory/persona context.
                    finalOptions.memories.Clear(); finalOptions.playerPersonaLabel = null;
                    FinalEvict(s, WebEvictionVoting.Evaluate(finalOptions).selectedNomineeId); break;
                case EpisodePhase.JuryQuestioning:
                    Require(s.juryExchanges[s.juryQuestionIndex].completed, "Answer the current question or skip questioning.");
                    s.juryQuestionIndex++;
                    if (s.juryQuestionIndex >= JuryExchangeCount(s)) BeginFinalSpeeches(s); else PrepareJuryQuestion(s);
                    break;
                case EpisodePhase.FinalSpeeches:
                    Require(!s.Active.Any(x => x.isPlayer) || s.finalSpeeches.Any(x => x.speakerId == s.playerId), "Deliver or skip your final speech first.");
                    Phase(s, EpisodePhase.Jury); break;
                case EpisodePhase.Jury: ResolveJury(s); break;
                default: throw new RuleException("No transition is available.");
            }
        }

        /// <summary>
        /// Which discipline a competition tests, from the phase and the week.
        ///
        /// <para>Public because the result card names it, and a second copy of this expression in the
        /// presentation layer is how the card would eventually announce "Endurance" over a set of
        /// scores the engine rolled as "Mental". It is a pure function of committed state, so the
        /// card recomputes rather than reading the category back out of the event sentence.</para>
        /// </summary>
        public static string CompetitionCategory(EpisodePhase phase, int week)
        {
            if (phase == EpisodePhase.FinalHoHPart1) return "Endurance";
            if (phase == EpisodePhase.FinalHoHPart2) return "Skill";
            if (phase == EpisodePhase.FinalHoHPart3) return "Mental";
            return week % 3 == 1 ? "Skill" : week % 3 == 2 ? "Mental" : "Endurance";
        }

        private static void ResolveCompetition(EpisodeState s, double performance)
        {
            var players = CompetitionPlayers(s).ToArray(); Require(players.Length > 0, "No eligible competitors.");
            string category = CompetitionCategory(s.phase, s.week);
            s.competitionScores.Clear();
            if (s.phase == EpisodePhase.FinalHoHPart1)
            {
                // Native input adapter: precision earns up to two effective endurance points for
                // this challenge only. This bonus policy is native; stored stats never change.
                var effectivePlayers = players.Select(contestant => contestant.Clone()).ToArray();
                foreach (var contestant in effectivePlayers.Where(contestant => contestant.isPlayer))
                    contestant.stats.endurance = Math.Min(10, contestant.stats.endurance + performance * 2);
                s.competitionScores = WebEnduranceCompetition.Run(effectivePlayers, () => Roll(s)).scores;
            }
            else
            {
                foreach (var contestant in players)
                {
                    // Native precision challenge supplies a bounded player bonus to the web runner's existing bonus input.
                    double bonus = contestant.isPlayer ? performance * 2 : 0;
                    double score = WebRules.WeightedCompetitionScore(contestant.stats, category, s.nominees.Contains(contestant.id), bonus, Roll(s), 0);
                    s.competitionScores.Add(new CompetitionScore { contestantId = contestant.id, score = score });
                }
            }
            var winner = s.competitionScores.OrderByDescending(x => x.score).First().contestantId;
            if (s.phase == EpisodePhase.HoH) { s.hohId = winner; s.Find(winner).hohWins++; }
            else if (s.phase == EpisodePhase.Veto) { s.vetoHolderId = winner; s.Find(winner).vetoWins++; }
            else if (s.phase == EpisodePhase.FinalHoHPart1) s.finalPart1WinnerId = winner;
            else if (s.phase == EpisodePhase.FinalHoHPart2) s.finalPart2WinnerId = winner;
            else { s.hohId = winner; s.Find(winner).hohWins++; }
            s.competitionResolved = true;
            Log(s, "competition", "Competition winner: " + Name(s, winner) + " · " + category + ".");
        }

        public static IEnumerable<ContestantState> NominationCandidates(EpisodeState s) => s.Active.Where(c => c.id != s.hohId);
        public static IEnumerable<ContestantState> ReplacementCandidates(EpisodeState s) => s.Active.Where(c => c.id != s.hohId && c.id != s.vetoHolderId && !s.nominees.Contains(c.id));
        /// <summary>
        /// How many social actions a week allows: half the active house, rounded up.
        ///
        /// <para>Ported from the reference build's <c>Math.ceil(activeCount / 2)</c>. This read as a
        /// flat eighteen, which is the same family of mistake as the seven cast-derived constants
        /// already corrected — except this one was never even right for six. Eighteen is three times
        /// what a six-person house should get, so the social week has always been far looser here
        /// than in the build being copied.</para>
        ///
        /// <para>The budget tightens as the house empties, which is the point: the endgame is meant
        /// to leave less room to work than the opening weeks.</para>
        /// </summary>
        public static int SocialActionBudget(EpisodeState s) =>
            s.week < s.socialBudgetRulesStartWeek
                ? LegacySocialActionBudget
                : (int)Math.Ceiling(Math.Max(0, s.Active.Count()) / 2.0);

        /// <summary>
        /// The flat allowance this project used before the rule was ported.
        ///
        /// <para>Kept so a season saved under it can finish the week it is in. It was never the
        /// reference build's number for any house size — six houseguests should get three — which is
        /// why it is a compatibility boundary rather than a supported alternative.</para>
        /// </summary>
        public const int LegacySocialActionBudget = 18;

        /// <summary>What the player has spent this week, in the social window and outside it.</summary>
        public static int SocialActionsSpent(EpisodeState s) => s.socialActions + s.outOfPhaseSocialActions;

        /// <summary>
        /// At Final 4 a veto holder who is not on the block may not use the veto.
        ///
        /// <para>The reference build states this as a rule of its own. Without it the veto holder at
        /// four could pull a nominee down and force the Head of Household to name the only remaining
        /// person — themselves or the other safe player — which turns the last full week into a
        /// formality.</para>
        /// </summary>
        public static bool VetoIsLockedAtFinalFour(EpisodeState s) =>
            s.Active.Count() == 4 && !string.IsNullOrEmpty(s.vetoHolderId) && !s.nominees.Contains(s.vetoHolderId);

        public static IEnumerable<ContestantState> Voters(EpisodeState s) => s.Active.Where(c => c.id != s.hohId && !s.nominees.Contains(c.id));
        public static bool NeedsPlayerTieBreak(EpisodeState s) => s.phase == EpisodePhase.Eviction && !s.evictionResolved && s.hohId == s.playerId &&
            s.nominees.Count == 2 && Voters(s).All(c => s.votes.Any(v => v.voterId == c.id)) &&
            s.nominees.Select(id => s.votes.Count(v => v.voterId != s.hohId && v.targetId == id)).Distinct().Count() == 1;

        public static string NpcVetoSave(EpisodeState s)
        {
            // Before anything else, because Advance commits whatever this returns and the Final 4
            // lock would then reject it — leaving the veto meeting with no legal command at all.
            // A locked veto is one the holder may not use, so the answer is "saves nobody".
            if (VetoIsLockedAtFinalFour(s)) return null;
            if (!ReplacementCandidates(s).Any()) return null;
            if (s.nominees.Contains(s.vetoHolderId)) return s.vetoHolderId;
            return s.nominees.OrderByDescending(id => s.Score(s.vetoHolderId, id)).FirstOrDefault(id => s.Score(s.vetoHolderId, id) > 30);
        }

        private static void Nominate(EpisodeState s, string first, string second)
        {
            Require(s.nominees.Count == 0, "Nominations are already committed.");
            var eligible = new HashSet<string>(NominationCandidates(s).Select(c => c.id));
            Require(first != second && eligible.Contains(first ?? "") && eligible.Contains(second ?? ""), "Choose two distinct eligible houseguests.");
            s.nominees = new List<string> { first, second };
            foreach (var nominee in s.nominees) NominationEffects(s, nominee);
            Log(s, "nomination", Name(s, s.hohId) + Verb(s, s.hohId, " nominates ", " nominate ")
                + Target(s, first, s.hohId) + " and " + Target(s, second, s.hohId) + ".");
        }

        private static void NominationEffects(EpisodeState s, string id, bool initial = true)
        {
            var guest = s.Find(id); guest.timesNominated++; guest.nominationWeeks.Add(s.week);
            var oathSnapshot = OathSnapshot(s);
            var oathRolls = WebLoyaltyOaths.NominationNeutralWitnesses(oathSnapshot, s.hohId, id).Select(_ => Roll(s)).ToArray();
            ApplyOathPlan(s, WebLoyaltyOaths.Nomination(oathSnapshot, s.hohId, id, oathRolls));
            // Current source accepted-consequences applies -8 to the player arc, not normal trust.
            if (id == s.playerId || s.hohId == s.playerId)
                Arc(s, id == s.playerId ? s.hohId : id, -8, "Nominated " + (s.hohId == s.playerId ? guest.name : "you") + " in week " + s.week);
            if (initial)
            {
                var moods = new[] { "Angry", "Upset", "Neutral", "Content", "Happy" };
                var stress = new[] { "Relaxed", "Normal", "Tense", "Stressed", "Overwhelmed" };
                guest.mood = moods[Math.Max(0, Array.IndexOf(moods, guest.mood) - 2)];
                guest.stressLevel = stress[Math.Min(4, Array.IndexOf(stress, guest.stressLevel) + 2)];
            }
            Remember(s, id, s.hohId, "Nominated me in week " + s.week + ".", false);
            foreach (var promise in s.promises.Where(p => p.status == PromiseStatus.Active && p.fromId == s.hohId &&
                ((p.kind == PromiseKind.Safety && p.toId == id) || (p.kind == PromiseKind.AllianceLoyalty && s.Allied(p.fromId, id)))).ToArray())
                SettlePromise(s, promise, PromiseStatus.Broken);
        }

        private static void ResolveVeto(EpisodeState s, bool use, string saved, string replacement)
        {
            if (use)
            {
                Require(!VetoIsLockedAtFinalFour(s),
                    "At the final four a veto holder who is not on the block cannot use the veto.");
                Require(s.nominees.Contains(saved ?? ""), "The veto can only save a current nominee.");
                Require(ReplacementCandidates(s).Any(), "The veto cannot be used because no legal replacement exists.");
                if (s.hohId != s.playerId) replacement = ReplacementCandidates(s).OrderBy(c => s.Score(s.hohId, c.id)).First().id;
                Require(ReplacementCandidates(s).Any(c => c.id == replacement), "Choose an eligible replacement; the HoH and veto holder are immune.");
                s.nominees.Remove(saved); s.nominees.Add(replacement); NominationEffects(s, replacement, false);
                Change(s, saved, s.vetoHolderId, 25, Name(s, s.vetoHolderId) + " used POV to save " + Target(s, saved, s.vetoHolderId));
                Change(s, replacement, s.hohId, -20, Name(s, s.hohId) + " named " + Target(s, replacement, s.hohId) + " as replacement nominee");
                if (s.hohId != s.vetoHolderId) Change(s, replacement, s.vetoHolderId, -15, Name(s, s.vetoHolderId) + " used POV forcing " + Target(s, replacement, s.vetoHolderId) + " on the block");
                Log(s, "veto", Name(s, s.vetoHolderId) + Verb(s, s.vetoHolderId, " saves ", " save ")
                    + Target(s, saved, s.vetoHolderId) + "; " + TargetStart(s, replacement, s.vetoHolderId)
                    + Verb(s, replacement, " is", " are") + " the replacement nominee.");
            }
            else Log(s, "veto", Name(s, s.vetoHolderId) + Verb(s, s.vetoHolderId, " declines ", " decline ")
                + "to use the veto. Nominations stand.");
            s.vetoResolved = true;
        }

        /// <summary>
        /// Puts every nominee's speech on the record, generating the ones the player does not write.
        ///
        /// <para>The player's own is theirs to author and is committed separately, so this fills in
        /// around it rather than speaking for them: a nominee who has already spoken is skipped.
        /// A player who is not on the block has nothing to say here, and the stage passes straight
        /// through.</para>
        /// </summary>
        private static void SpeakFromTheBlock(EpisodeState s)
        {
            foreach (var nomineeId in s.nominees)
            {
                if (nomineeId == s.playerId) continue;
                if (s.evictionSpeeches.Any(x => x.speakerId == nomineeId)) continue;
                var nominee = s.Find(nomineeId);
                if (nominee == null) continue;
                s.evictionSpeeches.Add(new EvictionSpeechState
                {
                    speakerId = nomineeId, week = s.week, isPlayerAuthored = false,
                    text = HouseDialogue.EvictionPlea(s, nomineeId),
                });
                Log(s, "eviction-speech", Name(s, nomineeId) + ": "
                    + s.evictionSpeeches.Last(x => x.speakerId == nomineeId).text);
            }
        }

        private static void Vote(EpisodeState s, string voter, string target, string reason)
        {
            Require(s.nominees.Contains(target ?? ""), "Vote for a current nominee.");
            Require(!s.votes.Any(v => v.voterId == voter), "This vote is already committed.");
            s.votes.Add(new VoteState { voterId = voter, targetId = target, reason = reason });
            Log(s, "private-vote", "Your eviction vote was recorded.", voter);
        }

        private static void FinalEvict(EpisodeState s, string target)
        {
            Require(s.Active.Count() == 3 && s.Active.Any(c => c.id == target && c.id != s.hohId), "Choose one of the other finalists to evict.");
            var selected = s.Active.Single(c => c.id != target && c.id != s.hohId).id;
            foreach (var promise in s.promises.Where(p => p.status == PromiseStatus.Active && p.kind == PromiseKind.FinalTwo && p.fromId == s.hohId).ToArray())
                SettlePromise(s, promise, promise.toId == selected ? PromiseStatus.Fulfilled : PromiseStatus.Broken);
            s.Find(target).status = ContestantStatus.Jury;
            s.jurySentiment = WebJurySentiment.AddJuror(s.jurySentiment, target, Name(s, target), s.Score(s.playerId, target));
            s.oathOpportunities.Clear(); // No social oath decisions remain after final eviction.
            s.votes.Clear();
            Log(s, "final-eviction", Name(s, s.hohId) + Verb(s, s.hohId, " takes ", " take ")
                + Target(s, selected, s.hohId) + " to the final two. " + TargetStart(s, target, s.hohId)
                + Verb(s, target, " joins", " join") + " the jury.");
            Phase(s, EpisodePhase.JuryQuestioning);
            s.juryExchanges.Clear(); s.juryQuestionIndex = 0;
            PrepareJuryQuestion(s);
        }

        private static void ResolveJury(EpisodeState s)
        {
            var finalists = s.Active.ToArray(); Require(finalists.Length == 2, "The jury requires exactly two finalists.");
            int votesBefore = s.votes.Count;
            foreach (var juror in s.contestants.Where(c => c.status == ContestantStatus.Jury || c.status == ContestantStatus.Evicted))
            {
                if (juror.isPlayer || s.votes.Any(v => v.voterId == juror.id)) continue;
                // Source JuryVotingWrapper: directed trust plus two independent +/-10 rolls.
                // Persist only gameplay randomness; browser animation-delay draws have no native equivalent.
                double firstScore = s.Score(juror.id, finalists[0].id) + Roll(s) * 20 - 10;
                double secondScore = s.Score(juror.id, finalists[1].id) + Roll(s) * 20 - 10;
                var preferred = firstScore > secondScore ? finalists[0] : finalists[1];
                s.votes.Add(new VoteState { voterId = juror.id, targetId = preferred.id, reason = "Personal trust and this juror's final impression." });
                Log(s, "jury-vote", Name(s, juror.id) + Verb(s, juror.id, " votes for ", " vote for ")
                    + Target(s, preferred.id, juror.id) + " to win.");
            }
            if (!s.Active.Any(c => c.id == s.playerId) && !s.votes.Any(v => v.voterId == s.playerId))
            {
                if (s.votes.Count > votesBefore) return;
                throw new RuleException("Cast your jury vote for a finalist first.");
            }
            int firstVotes = s.votes.Count(v => v.targetId == finalists[0].id), secondVotes = s.votes.Count(v => v.targetId == finalists[1].id);
            var ordered = firstVotes > secondVotes ? finalists : new[] { finalists[1], finalists[0] };
            // The source reveal's strict > comparison awards a tie to the second cast-order finalist.
            if (firstVotes == secondVotes)
                Log(s, "jury-tie", "Jury tie: the source game's tie rule awards the win to the second finalist in cast order.");
            s.winnerId = ordered[0].id; s.runnerUpId = ordered[1].id;
            ordered[0].status = ContestantStatus.Winner; ordered[1].status = ContestantStatus.RunnerUp;
            Phase(s, EpisodePhase.Finished); Log(s, "winner", "Gamesim winner: " + ordered[0].name + "!");
        }

        private static void Social(EpisodeState s, EpisodeCommand c)
        {
            Require(s.phase == EpisodePhase.Social || s.phase == EpisodePhase.Campaign, "Social actions are available during free time and campaigning.");
            Require(s.Find(s.playerId).status == ContestantStatus.Active, "Evicted players can follow the season but cannot influence it.");
            var target = s.Find(c.targetId);
            Require(target != null && target.status == ContestantStatus.Active && !target.isPlayer, "Approach an active housemate.");
            Require(SocialActionsSpent(s) < SocialActionBudget(s),
                "This social window is complete. Continue the episode.");
            switch (c.kind)
            {
                case EpisodeCommandKind.Talk:
                    Change(s, s.playerId, target.id, 4);
                    Remember(s, target.id, s.playerId, "We spent time talking in week " + s.week + ".", true);
                    Log(s, "conversation", target.name + ": " + target.motive, s.playerId, target.id); break;
                case EpisodeCommandKind.FormAlliance:
                    Require(!s.Allied(s.playerId, target.id), "You already share an active alliance.");
                    Require(s.Score(target.id, s.playerId) >= 8, "Build some trust before proposing an alliance.");
                    s.alliances.Add(new AllianceState { id = "alliance-" + s.nextSequence, name = "The " + target.name.Split(' ')[0] + " Pact", members = new List<string> { s.playerId, target.id } });
                    Change(s, s.playerId, target.id, 8); Log(s, "alliance", "You and " + target.name + " formed a private alliance.", s.playerId, target.id); break;
                case EpisodeCommandKind.LeaveAlliance:
                    var alliance = s.alliances.FirstOrDefault(a => a.active && a.members.Contains(s.playerId) && a.members.Contains(target.id));
                    Require(alliance != null, "No shared alliance is active."); alliance.active = false; Change(s, target.id, s.playerId, -15);
                    Remember(s, target.id, s.playerId, "Left our alliance.", true); Log(s, "alliance", "You left the alliance with " + target.name + ".", s.playerId, target.id); break;
                case EpisodeCommandKind.PromiseSafety: MakePromise(s, target.id, PromiseKind.Safety, null); break;
                case EpisodeCommandKind.PromiseFinalTwo: MakePromise(s, target.id, PromiseKind.FinalTwo, null); break;
                case EpisodeCommandKind.PromiseVote:
                    Require(s.phase == EpisodePhase.Campaign && Voters(s).Any(x => x.id == s.playerId) && s.nominees.Contains(c.secondTargetId ?? ""), "Choose a valid eviction target for your voting promise.");
                    MakePromise(s, target.id, PromiseKind.Vote, c.secondTargetId); break;
                case EpisodeCommandKind.ShareInformation:
                    var known = s.memories.LastOrDefault(m => m.ownerId == s.playerId && m.subjectId != target.id);
                    Require(known != null, "You have no personal information to share yet.");
                    Remember(s, target.id, known.subjectId, "Heard from you: " + known.text, true);
                    Change(s, s.playerId, target.id, 3); Log(s, "information", "You shared something you personally knew with " + target.name + ".", s.playerId, target.id); break;
                case EpisodeCommandKind.AskForIntel: AskForIntel(s, target); break;
                case EpisodeCommandKind.Eavesdrop: Eavesdrop(s); break;
                case EpisodeCommandKind.SpreadLie: SpreadLie(s, target, c.secondTargetId); break;
                case EpisodeCommandKind.VentAbout: VentAbout(s, target, c.secondTargetId); break;
                case EpisodeCommandKind.SchemeAgainst: SchemeAgainst(s, target); break;
                default: throw new RuleException("Unsupported social action.");
            }
            // Campaigning is not the social week, and the reference build counts those separately
            // even though they draw on the same budget — the in-phase counter resets with the phase
            // and this one has to survive it.
            if (s.phase == EpisodePhase.Social) s.socialActions++;
            else s.outOfPhaseSocialActions++;
        }

        // ---------------------------------------------------------------- the rest of the vocabulary
        //
        // Ported from the reference build's reducer. Every number below is its number, and every
        // roll goes through Roll(s) rather than a fresh generator: these are committed commands, so
        // a replay has to reproduce them exactly. The source calls Math.random() directly, which is
        // the one thing about it that cannot be copied literally.

        /// <summary>Roughly seven times in ten, per the source's spy approach.</summary>
        public const double EavesdropSuccessChance = 0.7;

        /// <summary>How often a lie comes back to the person it was about.</summary>
        public const double LieDiscoveryChance = 0.3;

        /// <summary>
        /// Asks a housemate what they know. Source: two to four points, and they remember being asked.
        ///
        /// <para>What they say is not manufactured here. The player learns the conversation
        /// happened; anything the housemate knows and chooses to share reaches the player through
        /// the same private-memory channel every other disclosure in this game uses.</para>
        /// </summary>
        private static void AskForIntel(EpisodeState s, ContestantState target)
        {
            double improvement = 2 + Math.Floor(Roll(s) * 3);
            Change(s, s.playerId, target.id, improvement);
            Remember(s, target.id, s.playerId, "You asked me what I knew in week " + s.week
                + ". It seems my read on this house is worth something to you.", true);
            Log(s, "information", "You asked " + target.name + " what they had been hearing.",
                s.playerId, target.id);
        }

        /// <summary>
        /// Listens in on two housemates. Source: caught roughly three times in ten, and being caught
        /// costs eight with whoever catches you.
        ///
        /// <para><b>What success reveals is deliberately narrow.</b> The source hands over the two
        /// houseguests' relationship score and this does the same — as a private memory the player
        /// owns, which is the only channel that keeps acceptance A9 true. An overheard conversation
        /// must not become narration of things the player's character was not there for, and it must
        /// not become a fact the rest of the house suddenly shares.</para>
        /// </summary>
        private static void Eavesdrop(EpisodeState s)
        {
            var others = s.Active.Where(c => !c.isPlayer).ToList();
            Require(others.Count >= 2, "There is nobody to overhear right now.");

            // Drawn before the outcome roll, so the pair is the same whether or not you are caught:
            // the conversation was happening either way.
            var first = Draw(s, others);
            var second = Draw(s, others.Where(c => c.id != first.id).ToList());

            if (Roll(s) >= EavesdropSuccessChance)
            {
                var catcher = Draw(s, others);
                Change(s, s.playerId, catcher.id, -8, catcher.name + " caught you listening in", "eavesdrop");
                Remember(s, catcher.id, s.playerId, "I caught you listening to a conversation you were not part of.", true);
                Log(s, "eavesdrop", catcher.name + " caught you listening in. That will cost you.",
                    s.playerId, catcher.id);
                return;
            }

            double between = s.Score(first.id, second.id);
            string reading = between >= 25 ? "sounded close"
                : between <= -25 ? "sounded like they cannot stand each other"
                : "sounded careful with each other";
            Remember(s, s.playerId, first.id, "I overheard " + first.name + " and " + second.name
                + " in week " + s.week + ". They " + reading + ".", true);
            Log(s, "eavesdrop", "You overheard " + first.name + " and " + second.name + ". They " + reading + ".",
                s.playerId);
        }

        /// <summary>
        /// Tells one housemate something untrue about another. Source: five to twelve points of
        /// damage between them, and fifteen against you if it comes back.
        ///
        /// <para>Discovery is rolled here rather than passed in, because the source's caller decides
        /// it and this engine has no caller outside itself. A lie that is never discovered is still
        /// recorded as a lie in the recipient's memory: they were told something, and whether it was
        /// true is not theirs to know.</para>
        /// </summary>
        private static void SpreadLie(EpisodeState s, ContestantState recipient, string aboutId)
        {
            var about = s.Find(aboutId);
            Require(about != null && about.status == ContestantStatus.Active && !about.isPlayer
                && about.id != recipient.id, "Choose someone else in the house to lie about.");

            double damage = -(5 + Math.Floor(Roll(s) * 8));
            Change(s, recipient.id, about.id, damage,
                "You told " + recipient.name + " something about " + about.name, "lie");
            Remember(s, recipient.id, about.id, "You told me something about " + about.name
                + " in week " + s.week + ". I have not checked it.", true);

            bool discovered = Roll(s) < LieDiscoveryChance;
            if (discovered)
            {
                Change(s, s.playerId, about.id, -15, about.name + " found out what you had been saying", "lie");
                Remember(s, about.id, s.playerId, "I found out what you were telling people about me.", true);
            }
            Log(s, "lie", discovered
                    ? "You told " + recipient.name + " something about " + about.name + " — and " + about.name + " found out."
                    : "You told " + recipient.name + " something about " + about.name + ".",
                discovered ? new[] { s.playerId, recipient.id, about.id } : new[] { s.playerId, recipient.id });
        }

        /// <summary>
        /// Complains about a third housemate. Source: eight to fifteen if it lands, minus ten if it
        /// does not — venting is a real risk, not a free bonding action.
        /// </summary>
        private static void VentAbout(EpisodeState s, ContestantState listener, string aboutId)
        {
            var about = s.Find(aboutId);
            Require(about != null && about.id != listener.id && !about.isPlayer,
                "Choose someone else in the house to vent about.");

            // It lands when they already dislike the subject; the house's own opinions decide this
            // rather than a bare coin toss.
            bool receptive = s.Score(listener.id, about.id) < 0 || Roll(s) < 0.5;
            double delta = receptive ? 8 + Math.Floor(Roll(s) * 8) : -10;
            Change(s, s.playerId, listener.id, delta,
                receptive ? listener.name + " agreed with you about " + about.name
                    : listener.name + " did not enjoy hearing it", "vent");
            Remember(s, listener.id, s.playerId, receptive
                ? "You vented to me about " + about.name + ". I was glad it was not just me."
                : "You vented to me about " + about.name + ". I did not enjoy it.", true);
            Log(s, "vent", receptive
                    ? "You vented about " + about.name + " to " + listener.name + ", and it landed."
                    : "You vented about " + about.name + " to " + listener.name + ". It did not land.",
                s.playerId, listener.id);
        }

        /// <summary>
        /// Works against someone quietly. Source: one or two of their relationships take three to
        /// eight points of damage.
        ///
        /// <para>The damage lands on the target's bonds with other houseguests rather than on the
        /// player's standing, which is the whole point of doing it quietly. The log entry is the
        /// player's own, because nobody else knows it happened.</para>
        /// </summary>
        private static void SchemeAgainst(EpisodeState s, ContestantState target)
        {
            var others = s.Active.Where(c => !c.isPlayer && c.id != target.id).ToList();
            Require(others.Count >= 1, "There is nobody left to turn against them.");

            int victims = Math.Min(others.Count, 1 + (int)Math.Floor(Roll(s) * 2));
            for (int i = 0; i < victims && others.Count > 0; i++)
            {
                var victim = Draw(s, others);
                others.Remove(victim);
                double damage = -(3 + Math.Floor(Roll(s) * 6));
                Change(s, target.id, victim.id, damage, "Something you said reached " + victim.name, "scheme");
            }
            Remember(s, s.playerId, target.id, "I spent week " + s.week + " quietly working against "
                + target.name + ".", true);
            Log(s, "scheme", "You worked against " + target.name + " without saying so to their face.", s.playerId);
        }

        /// <summary>
        /// Marks someone the Head of Household means to backdoor.
        ///
        /// <para>A plan, not a nomination: it costs an action and changes nobody's standing. It is
        /// stored so the intent survives a reload, and it clears when the week turns, because a plan
        /// for a week that has ended is not a plan.</para>
        /// </summary>
        private static void SetBackdoorPlan(EpisodeState s, ContestantState target)
        {
            // Not a social action, and deliberately not on the same budget. A backdoor plan is the
            // shape of a nomination rather than a conversation: you are deciding who the week is
            // actually aimed at before naming the two people who will stand in for it. It is also
            // the only moment it can be set — during the social week the title still belongs to
            // last week's winner, and by campaigning the veto has already been used.
            Require(s.phase == EpisodePhase.Nomination, "A backdoor is planned when nominations are made.");
            Require(s.hohId == s.playerId, "Only the Head of Household can plan a backdoor.");
            Require(s.nominees.Count == 0, "Nominations are already committed.");
            Require(target != null && target.status == ContestantStatus.Active && !target.isPlayer,
                "Choose an active housemate to aim the week at.");
            s.backdoorTargetId = target.id;
            Remember(s, s.playerId, target.id, "I mean to get " + target.name
                + " on the block after the veto, not before it.", true);
            Log(s, "backdoor", "You settled on a plan to backdoor " + target.name + ".", s.playerId);
        }

        /// <summary>
        /// One of a list, drawn from the season's own generator.
        ///
        /// <para>The source shuffles with <c>Math.random()</c>. That cannot be copied literally here:
        /// these are committed commands and a replay has to reproduce them, so every draw comes off
        /// the saved stream instead.</para>
        /// </summary>
        private static ContestantState Draw(EpisodeState s, List<ContestantState> from)
        {
            int index = (int)Math.Floor(Roll(s) * from.Count);
            return from[Math.Min(Math.Max(0, index), from.Count - 1)];
        }

        private static void MakePromise(EpisodeState s, string to, PromiseKind kind, string target)
        {
            Require(!s.promises.Any(p => p.status == PromiseStatus.Active && p.fromId == s.playerId && p.toId == to && p.kind == kind), "This promise is already active.");
            s.promises.Add(new PromiseState { id = "promise-" + s.nextSequence, fromId = s.playerId, toId = to, targetId = target,
                kind = kind, status = PromiseStatus.Active, week = s.week, expiresWeek = kind == PromiseKind.FinalTwo ? 0 : kind == PromiseKind.Safety ? s.week + 1 : s.week });
            Remember(s, to, s.playerId, "Made me a " + kind + " promise.", true);
            Remember(s, s.playerId, to, "I promised " + kind + " to " + Name(s, to) + ".", true);
            Log(s, "promise", "You promised " + kind + " to " + Name(s, to) + ".", s.playerId, to);
        }

        private static void SettlePromise(EpisodeState s, PromiseState promise, PromiseStatus status)
        {
            promise.status = status;
            var kind = promise.kind == PromiseKind.FinalTwo ? "final_2" : promise.kind == PromiseKind.AllianceLoyalty ? "alliance_loyalty" : promise.kind.ToString().ToLowerInvariant();
            // The original PromiseSystem calls its optional-loyalty API with the default 5
            // and records a one-way event, unlike generic reciprocal social changes.
            var delta = WebRules.PromiseImpact(kind, status == PromiseStatus.Broken ? "broken" : "fulfilled");
            WriteScore(s, promise.toId, promise.fromId, delta);
            string text = Name(s, promise.fromId) + (status == PromiseStatus.Broken ? " broke" : " fulfilled") + " a " + promise.kind + " promise.";
            Remember(s, promise.toId, promise.fromId, text, true); Remember(s, promise.fromId, promise.toId, text, true);
            Log(s, "promise-outcome", text, promise.fromId, promise.toId);
            if (status == PromiseStatus.Broken)
            {
                foreach (var witness in s.Active.Where(c => c.id != promise.fromId && c.id != promise.toId))
                {
                    double chance = s.Allied(witness.id, promise.toId) ? 0.8 : s.Score(witness.id, promise.toId) > 50 ? 0.6 : s.Score(witness.id, promise.fromId) > 50 ? 0.3 : 0.2;
                    if (Roll(s) >= chance) continue;
                    Remember(s, witness.id, promise.fromId, text, true); WriteScore(s, witness.id, promise.fromId, WebRules.JsRound(delta * 0.4));
                }
            }
        }

        private static void Change(EpisodeState s, string from, string to, double delta, string note = null, string eventType = null)
            => ChangeWithRoll(s, from, to, delta, () => Roll(s), note, eventType);

        // Shared source reducer. The ordinary player path keeps its original stream;
        // trusted NPC completion injects a separate saved stream, without duplicating rules.
        private static void ChangeWithRoll(EpisodeState s, string from, string to, double delta, Func<double> nextRoll,
            string note = null, string eventType = null)
        {
            if (from == to) return;
            double beforeScore = s.Score(from, to);
            double adjusted = WebRules.RelationshipDelta(delta, s.Find(from).stats.social, s.Find(from).isPlayer);
            string reason = note ?? "Relationship changed by " + adjusted.ToString(System.Globalization.CultureInfo.InvariantCulture);
            // Source event and reciprocal-score draws are distinct. Native timestamps are logical sequence IDs.
            if (eventType != null)
            {
                AddRelationshipEvent(s, from, to, adjusted, reason, eventType);
                AddRelationshipEvent(s, to, from, WebRules.ReciprocalDelta(delta, nextRoll()), reason, eventType);
            }
            WriteScore(s, from, to, adjusted);
            WriteScore(s, to, from, WebRules.ReciprocalDelta(delta, nextRoll()));
            if (from == s.playerId && adjusted > 0 && beforeScore < 75 && s.Score(from, to) >= 75 && !s.shownOathMilestones.Contains(to))
            {
                s.shownOathMilestones.Add(to);
                if (s.Find(to).status == ContestantStatus.Active && s.Active.Count() > 2)
                    s.oathOpportunities.Add(to);
                Log(s, "relationship-milestone", "Your bond with " + Name(s, to) + " crossed 75. A personal loyalty declaration is available during social time.", s.playerId);
            }
            foreach (var relation in s.relationships.Where(r => (r.fromId == from && r.toId == to) || (r.fromId == to && r.toId == from)))
            {
                relation.lastInteractionWeek = s.week;
                if (note != null) { relation.notes.Add(note); if (relation.notes.Count > 256) relation.notes.RemoveAt(0); }
            }
            if (from == s.playerId || to == s.playerId) Arc(s, from == s.playerId ? to : from, adjusted, reason);
            else { Arc(s, to, adjusted, reason); Arc(s, from, adjusted, reason); }
        }

        private static void AddRelationshipEvent(EpisodeState s, string from, string to, double delta, string note, string type)
        {
            WriteScore(s, from, to, 0);
            var relation = s.relationships.Single(r => r.fromId == from && r.toId == to);
            relation.events.Add(new RelationshipEventState { sequence = s.nextSequence++, week = s.week,
                type = type, description = note, impactScore = delta,
                // Set from the act rather than hardcoded. An untyped change is ordinary social
                // traffic and fades; the ledger names the ones that do not.
                //
                // No currently-written type is permanent, so this is a no-op today and correct the
                // moment one is. Naming the permanent acts — nominations, veto saves, alliances —
                // means passing an eventType where none is passed now, and the branch above
                // consumes an extra generator roll and two sequence numbers when it fires. That
                // re-rolls every season and breaks every replay fixture, so it lands once, with the
                // NPC writes in Phase C, rather than twice.
                decayable = RelationshipLedger.Decays(type) });
            if (relation.events.Count > 512) relation.events.RemoveAt(0);
        }

        private static void Arc(EpisodeState s, string npcId, double delta, string reason)
        {
            s.relationshipArcs = WebRelationshipArcs.Update(s.relationshipArcs, npcId, Name(s, npcId), delta, reason, s.week).arcs;
        }

        private static void WriteScore(EpisodeState s, string from, string to, double delta)
        {
            var relation = s.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (relation == null) { relation = new RelationshipState { fromId = from, toId = to }; s.relationships.Add(relation); }
            relation.score = WebRules.ClampScore(relation.score + delta);
        }

        /// <summary>
        /// How many houseguests sit in a veto competition: six, or everyone still in the house when
        /// fewer than six remain.
        ///
        /// <para>Six is the format's number — the Head of Household, both nominees, and three drawn
        /// — and it is what the web game seats. This project previously put <b>every</b> active
        /// houseguest in the veto competition, which is indistinguishable from the real rule in a
        /// six-person house and wrong in any larger one. Existing saves are unaffected for exactly
        /// that reason: at six active or fewer the two rules produce the same lineup.</para>
        /// </summary>
        public const int VetoLineupSize = 6;

        public static int VetoPlayerCount(int activeCount)
            => Math.Min(VetoLineupSize, Math.Max(0, activeCount));

        /// <summary>
        /// The veto lineup: the HoH and both nominees by right, then a seeded draw for the rest.
        ///
        /// <para>The draw runs off <see cref="EpisodeState.randomState"/> and advances it, so the
        /// same save always draws the same names and a reload cannot re-roll a lineup the player
        /// has already seen.</para>
        /// </summary>
        private static List<string> DrawVetoPlayers(EpisodeState s)
        {
            var active = s.Active.Select(c => c.id).ToList();
            int seats = VetoPlayerCount(active.Count);

            // When every remaining houseguest plays there is nothing to draw, so the random stream
            // must not be touched. Drawing "all of them" one name at a time still consumes rolls,
            // which silently shifted every later result in a six-person season and broke replay
            // determinism — a committed season would not reproduce itself.
            if (seats >= active.Count) return active;

            var lineup = new List<string>();
            if (active.Contains(s.hohId)) lineup.Add(s.hohId);
            foreach (var nominee in s.nominees)
                if (active.Contains(nominee) && !lineup.Contains(nominee)) lineup.Add(nominee);

            var pool = active.Where(id => !lineup.Contains(id)).ToList();
            var rng = new SeededRandom(s.randomState);
            while (lineup.Count < seats && pool.Count > 0)
            {
                int index = rng.NextInt(pool.Count);
                lineup.Add(pool[index]);
                pool.RemoveAt(index);
            }
            s.randomState = rng.State;

            // Held in house order rather than draw order, so the lineup reads the same way the cast
            // does everywhere else and a save diff does not churn on a reshuffle.
            return active.Where(id => lineup.Contains(id)).ToList();
        }

        private static double Roll(EpisodeState s)
        {
            var rng = new SeededRandom(s.randomState); var roll = rng.NextDouble(); s.randomState = rng.State; return roll;
        }

        private static void Remember(EpisodeState s, string owner, string subject, string text, bool privacy)
        {
            s.memories.Add(new MemoryState { ownerId = owner, subjectId = subject, text = text, week = s.week, isPrivate = privacy });
            while (s.memories.Count(m => m.ownerId == owner) > 30) s.memories.Remove(s.memories.First(m => m.ownerId == owner));
        }

        private static string Name(EpisodeState s, string id) => s.Find(id)?.name ?? "Unknown housemate";

        /// <summary>
        /// Second-person verb agreement.
        ///
        /// <para>The player's contestant is literally named "You", so every sentence built as
        /// <c>Name(...) + " verbs "</c> read "You is evicted", "You nominates", "You saves You". It
        /// was in the committed event text, so it reached the status line, the notebook, the
        /// ceremony card and the eviction — the one line the whole run is supposed to land on.</para>
        /// </summary>
        private static string Verb(EpisodeState s, string id, string third, string second)
            => id == s.playerId ? second : third;

        /// <summary>
        /// The object form of a houseguest: their name, "you", or "yourself" when the actor is
        /// acting on themselves.
        /// </summary>
        private static string Target(EpisodeState s, string id, string actorId)
            => id != s.playerId ? Name(s, id) : (id == actorId ? "yourself" : "you");

        /// <summary>The same, for a pronoun that opens a sentence.</summary>
        private static string TargetStart(EpisodeState s, string id, string actorId)
        {
            var value = Target(s, id, actorId);
            return value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value.Substring(1);
        }

        private static void Phase(EpisodeState s, EpisodePhase phase) { s.phase = phase; Log(s, "phase", "Week " + s.week + " · " + phase); }
        private static void Log(EpisodeState s, string kind, string text, params string[] audience)
        {
            s.events.Add(new EpisodeEvent { sequence = s.nextSequence++, week = s.week, phase = s.phase, kind = kind, text = text, audienceIds = audience.ToList() });
            if (s.events.Count > 256) s.events.RemoveAt(0);
        }
        private static void Require(bool condition, string message) { if (!condition) throw new RuleException(message); }
        private sealed class RuleException : Exception { public RuleException(string message) : base(message) { } }
    }
}
