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
                    Require(Voters(s).Any(x => x.id == s.playerId) || NeedsPlayerTieBreak(s), "You are not eligible to vote now.");
                    Vote(s, s.playerId, c.targetId, "Player's decision"); break;
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
                        s.nominees.Clear(); s.vetoPlayers.Clear(); s.votes.Clear();
                        s.evictionResolved = false; s.vetoResolved = false; s.competitionScores.Clear();
                        foreach (var promise in s.promises.Where(p => p.status == PromiseStatus.Active && p.expiresWeek > 0 && p.expiresWeek < s.week))
                            promise.status = PromiseStatus.Expired;
                    }
                    s.socialActions = 0; s.competitionResolved = false;
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
                    s.vetoPlayers = s.Active.Select(c => c.id).ToList(); // Six-person slice: all remaining houseguests play.
                    Log(s, "veto-selection", "Everyone remaining in the house is eligible for the veto competition.");
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
                    Phase(s, EpisodePhase.Campaign); break;
                case EpisodePhase.Campaign:
                    Log(s, "campaign-close", "Campaigning has closed. The house votes privately to evict."); Phase(s, EpisodePhase.Eviction); break;
                case EpisodePhase.Eviction:
                    if (s.evictionResolved) { Phase(s, EpisodePhase.Social); return; }
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
                    s.jurySentiment = WebJurySentiment.AddJuror(s.jurySentiment, evicted, Name(s, evicted), s.Score(s.playerId, evicted));
                    s.oathOpportunities.Remove(evicted);
                    Log(s, "eviction", Name(s, evicted) + Verb(s, evicted, " is evicted and joins ", " are evicted and join ")
                        + "this six-person season's jury.");
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

        private static void ResolveCompetition(EpisodeState s, double performance)
        {
            var players = CompetitionPlayers(s).ToArray(); Require(players.Length > 0, "No eligible competitors.");
            string category = s.phase == EpisodePhase.FinalHoHPart1 ? "Endurance" : s.phase == EpisodePhase.FinalHoHPart2 ? "Skill" :
                s.phase == EpisodePhase.FinalHoHPart3 ? "Mental" : s.week % 3 == 1 ? "Skill" : s.week % 3 == 2 ? "Mental" : "Endurance";
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
        public static IEnumerable<ContestantState> Voters(EpisodeState s) => s.Active.Where(c => c.id != s.hohId && !s.nominees.Contains(c.id));
        public static bool NeedsPlayerTieBreak(EpisodeState s) => s.phase == EpisodePhase.Eviction && !s.evictionResolved && s.hohId == s.playerId &&
            s.nominees.Count == 2 && Voters(s).All(c => s.votes.Any(v => v.voterId == c.id)) &&
            s.nominees.Select(id => s.votes.Count(v => v.voterId != s.hohId && v.targetId == id)).Distinct().Count() == 1;

        public static string NpcVetoSave(EpisodeState s)
        {
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
            Require(s.socialActions < 18, "This social window is complete. Continue the episode.");
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
                default: throw new RuleException("Unsupported social action.");
            }
            s.socialActions++;
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
            relation.events.Add(new RelationshipEventState { sequence = s.nextSequence++, week = s.week, type = type, description = note, impactScore = delta, decayable = true });
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
