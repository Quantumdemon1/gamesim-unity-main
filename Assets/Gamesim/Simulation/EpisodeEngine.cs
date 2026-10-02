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
                ReconcileAllianceRows(next);
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
                    // A beat that closes at the nominations lapses first, so anything it moved has
                    // moved before the names are said.
                    if (StoryOn(s)) StoryLapse(s, StoryAnchors.NomsSet);
                    Nominate(s, c.targetId, c.secondTargetId);
                    // The player's own backdoor plan, made before the names were said, becomes the
                    // week's backdoor story - unless they put the target straight up after all.
                    if (s.backdoorTargetId != null && !s.nominees.Contains(s.backdoorTargetId))
                        StartBackdoor(s, s.backdoorTargetId, s.nominees.ToList());
                    StoryAnchor(s, StoryAnchors.NomsSet); break;
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
                    RecordPlayerBallot(s, c.targetId);
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
                case EpisodeCommandKind.MarkOpeningBeat: MarkOpeningBeat(s, c.targetId); break;
                // Its own case, not the default: Social() would charge an interaction for it, and
                // the first night's introductions are free, as they are in the reference build.
                case EpisodeCommandKind.Introduce: Introduce(s, c); break;
                // Under the commitment rules (C7) a counter that stands is answered as an offer is, free,
                // by naming the houseguest who made it: there is no deal waiting for it to name.
                case EpisodeCommandKind.RespondToDeal when Negotiation.OpenCounter(s, c.targetId) != null: AnswerCounter(s, c); break;
                case EpisodeCommandKind.RespondToDeal: RespondToDeal(s, c); break;
                case EpisodeCommandKind.BuyActionPoint: BuyActionPoint(s, c); break;
                // Not a social action: the situation came to the player, and charging them
                // an interaction for being walked in on would be charging for the weather.
                case EpisodeCommandKind.ResolveHouseEvent: ResolveHouseEvent(s, c); break;
                // A story beat, answered by its option id. Its ordinal was reserved for this and has
                // always thrown until now, so no recording holds a successful one. Not a social
                // action unless the option itself says it costs one.
                case EpisodeCommandKind.ProgressStoryline: ProgressStoryline(s, c); break;
                // The house telling the engine where the player is. Only the director knows, which
                // is why this arrives as a command rather than being computed in the week.
                case EpisodeCommandKind.WitnessProximity: WitnessProximity(s, c); break;
                // Addressed to the room rather than to a person, so it cannot go through
                // Social(), which requires a housemate to approach. It still costs an
                // action, which it spends for itself.
                case EpisodeCommandKind.HouseMeeting: HouseMeeting(s, c); break;
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
                case EpisodeCommandKind.ThrowCompetition: ThrowWeeklyCompetition(s, c); break;
                case EpisodeCommandKind.ReflectDiary: ReflectDiary(s, c); break;
                case EpisodeCommandKind.SkipDiary: ResolveDiary(s, c, false); break;
                case EpisodeCommandKind.SwearLoyalty: ResolveOathOpportunity(s, c, true); break;
                case EpisodeCommandKind.DeclineLoyalty: ResolveOathOpportunity(s, c, false); break;
                case EpisodeCommandKind.Lobby: Lobby(s, c); break;
                // Not a social action: the houseguest came to the player, as an offer does.
                case EpisodeCommandKind.ReplyToHouseguest: ReplyToHouseguest(s, c); break;
                // Free, like a reply: a question is not an action, and the read is the play.
                case EpisodeCommandKind.AskVote: AskVote(s, c); break;
                case EpisodeCommandKind.ReadPerson: ReadPerson(s, c); break;
                case EpisodeCommandKind.LockFinalArgument: LockFinalArgument(s, c); break;
                // Under the commitment rules (C2) cutting ties with an ally who turned on the pact this
                // week, once the player can know it, is free: no action, no warmth, no grudge. Any other
                // leave is the social action below, as it always was.
                case EpisodeCommandKind.LeaveAlliance when Allegiance.FreeExit(s, c.targetId): CutTies(s, c, s.Find(c.targetId)); break;
                // Growing and renaming a pact (C5) exist only under the commitment rules: without them
                // they are refused before anything is spent, drawn or logged.
                case EpisodeCommandKind.BringIntoAlliance:
                    Require(CommitmentRulesOn(s), CommitmentKindRefusal);
                    Social(s, c); break;
                case EpisodeCommandKind.RenameAlliance:
                    Require(CommitmentRulesOn(s), CommitmentKindRefusal);
                    RenameAlliance(s, c); break;
                default: Social(s, c); break;
            }
        }

        public static bool IsCompetition(EpisodePhase phase) => phase == EpisodePhase.HoH || phase == EpisodePhase.Veto ||
            phase == EpisodePhase.FinalHoHPart1 || phase == EpisodePhase.FinalHoHPart2 || phase == EpisodePhase.FinalHoHPart3;

        /// <summary>
        /// The engine's own lines, never shown as a line of the story: the phase markers, and the
        /// competition's committed standings and performance arithmetic, which are the record's
        /// and not something anybody in the house sees (UI-UX-PASS-PLAN decision 12). The readers
        /// that list events for the player leave them out; a reader that counts events by kind
        /// reads <see cref="EpisodeState.events"/> as it always did. A kind, not an audience: an
        /// audience can only name houseguests, and a recorded season's audiences do not move.
        /// </summary>
        public static bool IsScaffolding(string kind) =>
            kind == "phase" || kind == "competition-standings" || kind == "competition-performance";

        public static IEnumerable<ContestantState> CompetitionPlayers(EpisodeState s)
        {
            if (s.phase == EpisodePhase.Veto) return s.Active.Where(c => s.vetoPlayers.Contains(c.id));
            if (s.phase == EpisodePhase.FinalHoHPart2) return s.Active.Where(c => c.id != s.finalPart1WinnerId);
            if (s.phase == EpisodePhase.FinalHoHPart3) return s.Active.Where(c => c.id == s.finalPart1WinnerId || c.id == s.finalPart2WinnerId);
            // Production's penalty: somebody on their second strike sits out this week's HoH
            // competition. Derived from the saved conduct record, so validation's recount agrees.
            return s.Active.Where(c => (s.Active.Count() <= 3 || c.id != s.previousHohId)
                                       && !(StoryAt(s, StoryRules.Production) && Production.SitsOut(s, c.id)));
        }

        private static void Advance(EpisodeState s)
        {
            switch (s.phase)
            {
                case EpisodePhase.Social:
                    Require(s.pendingDiary == null, "Visit the Diary Room or skip the pending reflection before beginning the next competition.");
                    // The social window closes: open beats lapse, and a removal production has decided
                    // on happens here - after the diary check, before the week turns, so no eviction
                    // can intervene and no juror row needs dropping.
                    if (StoryOn(s)) StorySocialClose(s);
                    if (s.evictionResolved)
                    {
                        s.previousHohId = s.hohId; s.week++; s.hohId = null; s.vetoHolderId = null;
                        s.nominees.Clear(); s.vetoPlayers.Clear(); s.votes.Clear(); s.evictionSpeeches.Clear();
                        s.backdoorTargetId = null;   // A plan for a week that has ended is not a plan.
                        s.evictionResolved = false; s.vetoResolved = false; s.competitionScores.Clear();
                        RecordPleas(s);
                        s.lobbies.Clear();
                        // Under the levers, bought time is the week's: the counter that never reset.
                        if (LeverRulesOn(s)) s.boughtActionPoints = 0;
                        foreach (var promise in s.promises.Where(p => p.status == PromiseStatus.Active && p.expiresWeek > 0 && p.expiresWeek < s.week))
                            promise.status = PromiseStatus.Expired;
                        if (StoryAt(s, StoryRules.Bonds)) MoodsSettle(s);
                        StoryWeekTurn(s);
                    }
                    s.socialActions = 0; s.outOfPhaseSocialActions = 0; s.competitionResolved = false;
                    ResetWindows(s);
                    s.replyCards.Clear();
                    // The finale has no Have-Nots: the last week's end with its three, and so do any
                    // passes and punishments the final four's veto left behind.
                    if (s.Active.Count() == 3) { s.haveNots.Clear(); s.haveNotPasses.Clear(); s.punishedHaveNots.Clear(); }
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
                    // The show's anchors: a Head of Household is crowned, or the veto is won.
                    string crossed = next == EpisodePhase.Nomination ? StoryAnchors.HohCrowned
                        : next == EpisodePhase.VetoMeeting ? StoryAnchors.VetoWon : null;
                    if (crossed != null && StoryOn(s)) StoryLapse(s, crossed);
                    s.competitionResolved = false; Phase(s, next);
                    // A nominee asks the player for the veto while it is still theirs to use.
                    if (next == EpisodePhase.VetoMeeting) NpcDeals.AskForTheVeto(s);
                    // Under agency, whoever has no claim on the new Head of Household courts them
                    // before the nominations (NPC-AGENCY-PLAN.md §4).
                    if (next == EpisodePhase.Nomination) CourtTheHoH(s);
                    if (crossed != null) StoryAnchor(s, crossed);
                    break;
                case EpisodePhase.Nomination:
                    if (s.nominees.Count == 0)
                    {
                        Require(s.hohId != s.playerId, "Choose two nominees first.");
                        // A beat that closes at the nominations lapses first: a pitch the HoH heard
                        // has already moved their view by the time they rank the house.
                        if (StoryOn(s)) StoryLapse(s, StoryAnchors.NomsSet);
                        // Least reluctant first: the strategy windows' reluctance (deals, alliances,
                        // pleas) and what the story system wrote (grudges, their word). With neither,
                        // it is the score, so this ranks exactly as it always did.
                        var ranked = NominationCandidates(s)
                            .OrderBy(c => NominationWeight(s, s.hohId, c.id)).ToList();
                        string backdoor = NpcBackdoorTarget(s, ranked);
                        var weakest = ranked.Where(c => c.id != backdoor).Take(2).ToArray();
                        Nominate(s, weakest[0].id, weakest[1].id);
                        if (backdoor != null) StartBackdoor(s, backdoor, weakest.Select(w => w.id).ToList());
                        StoryAnchor(s, StoryAnchors.NomsSet);
                        return;
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
                        var replacement = saved == null ? null : NpcReplacement(s);
                        ResolveVeto(s, saved != null, saved, replacement); return;
                    }
                    if (StoryOn(s)) StoryLapse(s, StoryAnchors.BlockSet);
                    // A nominee begging for votes and somebody courting the Head of Household are
                    // both positional, so they only make sense once the block is settled. This is
                    // that moment.
                    Phase(s, EpisodePhase.Campaign);
                    NpcPromises.Settle(s);
                    // Deals are struck here as well as at the social week's open, because half the
                    // ladder is positional: a nominee begging the veto holder and somebody courting
                    // the Head of Household only mean anything once the block is settled.
                    NpcDeals.Settle(s);
                    NpcDeals.Propose(s);
                    // The block's stories first, then the nominees' pleas, which fill any gap.
                    StoryAnchor(s, StoryAnchors.BlockSet);
                    NpcSocialActions.Campaign(s);
                    break;
                case EpisodePhase.Campaign:
                    if (StoryOn(s)) StoryLapse(s, StoryAnchors.EvictionEve);
                    // Campaigning IS the reference build's interaction stage — last conversations
                    // and vote-wrangling — so eviction night opens on the speeches rather than
                    // repeating a stage the season has just spent a whole phase on.
                    s.evictionStage = EvictionStage.Speeches;
                    s.replyCards.Clear();
                    // This sentence is recorded verbatim in the frozen voting-bloc witness. The
                    // speeches announce themselves in their own entries; rewording a committed line
                    // to describe a new stage would break a replay comparison for no gain.
                    Log(s, "campaign-close", "Campaigning has closed. The house votes privately to evict.");
                    Phase(s, EpisodePhase.Eviction);
                    StoryAnchor(s, StoryAnchors.EvictionEve);
                    // Under the commitment rules (C1) an information deal passes its reading: last in
                    // the step, as the house is about to vote and nothing more is campaigned.
                    if (CommitmentRulesOn(s)) PassTheReadings(s);
                    break;
                case EpisodePhase.Eviction:
                    if (s.evictionResolved)
                    {
                        // Under the commitment rules (C1, X4) whatever still bound the evictee ends as
                        // the house turns to the social week: after the reveal's reconcile saw a deal the
                        // player took as taken, and after the walk out read it for their goodbye, and
                        // before the house's own turns and settle (NpcSocialActions, NpcDeals) see it.
                        if (CommitmentRulesOn(s)) EndWithTheEvictee(s, s.ledger.power.LastOrDefault(p => p.week == s.week)?.evicteeId);
                        if (StoryOn(s)) StoryLapse(s, StoryAnchors.EvictionNight);
                        s.evictionStage = EvictionStage.Interaction;
                        Phase(s, EpisodePhase.Social);
                        // The house takes stock as the social week opens and then plays it: pacts
                        // that have soured fall apart, people who want to work together start doing
                        // so, words are given, and everybody spends three turns on whatever their
                        // personality makes natural. This one DOES draw from the season's generator
                        // — target choice is weighted sampling — which is why it sits behind the
                        // same rules boundary and why a recording declares itself past it.
                        // The player's alliances going in, so the ones the settle ends can be told.
                        var theirs = s.alliances.Where(a => a.active && a.members.Contains(s.playerId)).ToList();
                        // Past the story boundary the story system is the one producer: the legacy
                        // catalogue, crises, emergent situations and one-chapter storylines are
                        // ported into its pool as one-beat arcs, sharing its airtime instead of a
                        // one-situation-a-week slot. The night's stories are drawn before the house
                        // takes its turns, so a houseguest who confronts you fills a gap in the
                        // week rather than taking the only card the night had.
                        if (StoryOn(s)) StoryAnchor(s, StoryAnchors.EvictionNight);
                        NpcSocialActions.Settle(s);
                        NpcDeals.Settle(s);
                        NpcDeals.Propose(s);
                        // The week has turned, so what the last one left behind gets a week older
                        // and anything the player walked away from is written off.
                        Storylines.AgeModifiers(s);
                        Storylines.AbandonStale(s);
                        if (!StoryOn(s))
                        {
                            BeginStoryline(s);
                            OfferHouseEvent(s);
                        }
                        NarrateHouse(s);
                        TellThePlayerWhichAlliancesEnded(s, theirs);
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
                        Vote(s, voter.id, evaluation.selectedNomineeId, ExplainKnown(s, evaluation));
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
                    SettleDeals(s, DealResolution.Verdicts(s, DealResolution.Votes, null, voteDeals: LeverRulesOn(s)));
                    StoryVotesRevealed(s, evicted);
                    s.Find(evicted).status = ContestantStatus.Jury; s.evictionResolved = true;
                    // Off the block by the house's vote is saved too.
                    foreach (var survivor in s.nominees.Where(id => id != evicted)) MoodLift(s, survivor);
                    if (StoryOn(s)) Bonds.Apart(s, evicted);
                    s.evictionStage = EvictionStage.Results;
                    s.jurySentiment = WebJurySentiment.AddJuror(s.jurySentiment, evicted, Name(s, evicted), s.Score(s.playerId, evicted));
                    s.oathOpportunities.Remove(evicted);
                    // The reveal reads the count, never the ballots (UI-UX-PASS-PLAN B0): the
                    // eviction line carries the house's count on its tail - the Head of Household's
                    // deciding vote named, since the format reads it live - and each ballot's line is
                    // the voter's own, logged to them alone in the words it always had. The reveal
                    // logs no line of its own: the story mints its cycle and house-event ids from the
                    // sequence, so one more event here would re-roll a recorded season from its first
                    // eviction. What the player knows of the others is KnownBallots' to say: their
                    // own, the tie-break, what the count proves, what they were told.
                    Log(s, "eviction", Name(s, evicted) + Verb(s, evicted, " is evicted and joins ", " are evicted and join ")
                        + "the jury. " + CountTail(s, evicted, tally.Select(x => (x.id, x.count)).ToList()));
                    foreach (var vote in s.votes) Log(s, "vote-reveal", Name(s, vote.voterId) + " voted to evict "
                        + Target(s, vote.targetId, vote.voterId) + ". " + vote.reason, vote.voterId);
                    SettleVoteRead(s, evicted);
                    RecordReveal(s, evicted, tally.Select(x => x.count).ToList());
                    RecordJurorStanding(s, evicted);
                    // Under the commitment rules (C2) an ally whose ballot went against the player has
                    // turned on their pact - told to the betrayer alone, as the ballot is.
                    BallotBetrayals(s, evicted);
                    ReconcileOpportunities(s);
                    PreparePostEvictionDiary(s, evicted);
                    break;
                case EpisodePhase.FinalEviction:
                    Require(s.hohId != s.playerId, "Choose the final eviction first.");
                    var finalContext = s.Clone();
                    finalContext.nominees = s.Active.Where(c => c.id != s.hohId).Select(c => c.id).ToList();
                    var finalOptions = WebEvictionVoting.FromNative(finalContext, s.hohId);
                    // Match the source fast-forward final-selection caller, which supplies no memory/persona context.
                    finalOptions.memories.Clear(); finalOptions.playerPersonaLabel = null;
                    // Under the commitment rules (C1) a final two deal is a real obligation in the
                    // choice, not the web's deal term alone (about 4.5 points after its weight).
                    if (CommitmentRulesOn(s)) finalOptions.obligations.AddRange(FinalTwoTerms(s, s.hohId, finalContext.nominees));
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
        public static string CompetitionCategory(EpisodeState state) =>
            CompetitionCategory(state.phase, state.week, state.competitionRulesVersion, state.seed);

        /// <param name="seed">The season's seed: from rules 4 the season deals its five kinds in an order of its own.</param>
        public static string CompetitionCategory(EpisodePhase phase, int week, int rulesVersion = 1, uint seed = 0)
        {
            if (phase == EpisodePhase.FinalHoHPart1) return "Endurance";
            if (phase == EpisodePhase.FinalHoHPart2) return "Skill";
            if (phase == EpisodePhase.FinalHoHPart3) return "Mental";
            if (rulesVersion >= CompetitionRules.Widened) return CompetitionRules.Category(phase, week, seed);
            int rotation = week + (rulesVersion >= 2 && phase == EpisodePhase.Veto ? 1 : 0);
            return rotation % 3 == 1 ? "Skill" : rotation % 3 == 2 ? "Mental" : "Endurance";
        }

        /// <summary>Rules 3 applies the same earned preparation to every player entry route.</summary>
        public static double CommonCompetitionBonus(EpisodeState state) =>
            state.playerStudyBonus + state.phaseEventCompBonus + Storylines.CompetitionBonus(state);

        private static string CompetitionNumber(double value) => value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        private static string CompetitionSigned(double value) => value.ToString("+0.##;-0.##;0", System.Globalization.CultureInfo.InvariantCulture);

        private static void LogCompetitionInput(EpisodeState state, double performance, bool simulated, string numericExplanation = null)
        {
            if (state.competitionRulesVersion < 3 || !CompetitionPlayers(state).Any(actor => actor.isPlayer)) return;
            double manual = simulated ? 0 : performance * CompetitionRules.PerformanceWeight(state.competitionRulesVersion);
            double bonus = CommonCompetitionBonus(state) + manual;
            string detail = (simulated ? "Simulated: no performance bonus" : "Performance " + CompetitionNumber(performance * 100)
                + "%: " + CompetitionSigned(manual))
                + " · preparation " + CompetitionSigned(state.playerStudyBonus)
                + " · event " + CompetitionSigned(state.phaseEventCompBonus)
                + " · storyline " + CompetitionSigned(Storylines.CompetitionBonus(state)) + ". ";
            if (state.phase == EpisodePhase.FinalHoHPart1)
            {
                double original = state.Find(state.playerId).stats.endurance;
                double effective = Math.Max(0, Math.Min(10, original + bonus));
                detail += "Effective endurance " + CompetitionNumber(original) + " → " + CompetitionNumber(effective)
                    + " (" + CompetitionSigned(effective - original) + " after the 0–10 cap). Seeded survival rolls decide placement; stored stats stay unchanged.";
            }
            else detail += "Total player bonus " + CompetitionSigned(bonus)
                + ". Weighted stats, the nominee bonus and seeded rolls decide the remaining score. A zero performance bonus can still win.";
            Log(state, "competition-performance", numericExplanation ?? detail);
        }

        private static void LogCompetitionDefinition(EpisodeState state)
        {
            var definition = CompetitionDefinitions.For(state);
            if (definition != null) Log(state, "competition-definition", definition.Id + " · " + definition.Title
                + " · " + definition.Category + ". " + definition.Summary);
        }

        /// <param name="thrown">The player threw it (rules 4): no bonuses, and only part of their score counts.</param>
        private static void ResolveCompetition(EpisodeState s, double performance, bool thrown = false)
        {
            var players = CompetitionPlayers(s).ToArray(); Require(players.Length > 0, "No eligible competitors.");
            string category = CompetitionCategory(s);
            s.competitionScores.Clear();
            string numericExplanation = null;
            // Two points for full marks through rules 3, three from rules 4. The same multiplication
            // either way, so a frozen season's arithmetic is unchanged to the last bit.
            double weight = CompetitionRules.PerformanceWeight(s.competitionRulesVersion);
            bool widened = s.competitionRulesVersion >= CompetitionRules.Widened;
            if (s.phase == EpisodePhase.FinalHoHPart1)
            {
                // Native input adapter: precision earns up to two effective endurance points (three
                // from rules 4) for this challenge only. This bonus policy is native; stored stats never change.
                var effectivePlayers = players.Select(contestant => contestant.Clone()).ToArray();
                foreach (var contestant in effectivePlayers.Where(contestant => contestant.isPlayer))
                    contestant.stats.endurance = s.competitionRulesVersion >= 3
                        ? Math.Max(0, Math.Min(10, contestant.stats.endurance + performance * weight + CommonCompetitionBonus(s)))
                        : Math.Min(10, contestant.stats.endurance + performance * weight + Storylines.CompetitionBonus(s));
                var rounds = s.competitionRulesVersion >= 3 ? new List<string>() : null;
                var endurance = WebEnduranceCompetition.Run(effectivePlayers, () => Roll(s),
                    rounds == null ? null : (Action<double, string, double, double, bool>)((time, id, roll, chance, eliminated) =>
                    {
                        if (id == s.playerId) rounds.Add(ScoreNumber(time) + "s: survival " + ScoreNumber(chance)
                            + " from roll " + ScoreNumber(roll) + (eliminated ? " (eliminated)" : " (survived)"));
                    }));
                s.competitionScores = endurance.scores;
                if (rounds != null && players.Any(c => c.isPlayer))
                {
                    var original = players.First(c => c.isPlayer).stats;
                    var effective = effectivePlayers.First(c => c.isPlayer).stats;
                    var score = s.competitionScores.First(c => c.contestantId == s.playerId).score;
                    double capAdjustment = effective.endurance - original.endurance - CommonCompetitionBonus(s) - performance * weight;
                    decimal enduranceRounding = DisplayedScore(effective.endurance) - (DisplayedScore(original.endurance)
                        + DisplayedScore(s.playerStudyBonus) + DisplayedScore(s.phaseEventCompBonus)
                        + DisplayedScore(Storylines.CompetitionBonus(s)) + DisplayedScore(performance * weight) + DisplayedScore(capAdjustment));
                    numericExplanation = "Final endurance · stored " + ScoreNumber(original.endurance)
                        + "; preparation " + CompetitionSigned(s.playerStudyBonus) + "; event " + CompetitionSigned(s.phaseEventCompBonus)
                        + "; storyline " + CompetitionSigned(Storylines.CompetitionBonus(s)) + "; performance " + CompetitionSigned(performance * weight)
                        + "; cap adjustment " + ScoreNumber(capAdjustment) + "; rounding " + SignedScore(enduranceRounding)
                        + ". Effective endurance " + ScoreNumber(original.endurance) + " → " + ScoreNumber(effective.endurance)
                        + " (" + CompetitionSigned(effective.endurance - original.endurance) + " after the 0–10 cap). Physical contribution " + ScoreNumber(original.physical * .3)
                        + ". Each survival value = (effective endurance + physical contribution) × (0.5 + roll × 0.5). "
                        + string.Join("; ", rounds) + ". Committed score " + ScoreNumber(score) + "s"
                        + (endurance.winnerId == s.playerId ? " (last elimination time + 10s winner margin)." : " (elimination time).")
                        + " Values shown rounded; stored statistics stay unchanged.";
                }
            }
            else
            {
                double playerRoll = 0, playerLuckRoll = 0, playerRaw = 0;
                foreach (var contestant in players)
                {
                    // Native precision challenge supplies a bounded player bonus to the web runner's existing bonus input.
                    // A storyline modifier rides on the same input, which is the one place a
                    // competition bonus is already read — a second path would be a second answer.
                    // A throw gives every bonus up.
                    bool throwing = thrown && contestant.isPlayer;
                    double bonus = contestant.isPlayer && !throwing
                        ? performance * weight + (s.competitionRulesVersion >= 3 ? CommonCompetitionBonus(s) : Storylines.CompetitionBonus(s)) : 0;
                    // A Have-Not is tired in the veto, thrown or not. Zero everywhere else and in
                    // every season without them, so their arithmetic is unchanged.
                    bonus -= HaveNots.Penalty(s, contestant.id);
                    double roll = Roll(s);
                    // Rules 4's luck draws its second roll straight after the first, competitor by competitor.
                    double luckRoll = widened && CompetitionRules.RollsTwice(category) ? Roll(s) : 0;
                    double raw = widened
                        ? CompetitionRules.Score(contestant.stats, category, s.nominees.Contains(contestant.id), bonus, roll, luckRoll)
                        : WebRules.WeightedCompetitionScore(contestant.stats, category, s.nominees.Contains(contestant.id), bonus, roll, 0);
                    double score = throwing ? raw * CompetitionRules.ThrowShare(players.Length) : raw;
                    s.competitionScores.Add(new CompetitionScore { contestantId = contestant.id, score = score });
                    if (contestant.isPlayer) { playerRoll = roll; playerLuckRoll = luckRoll; playerRaw = raw; }
                    if (s.competitionRulesVersion >= 3 && !widened && contestant.isPlayer)
                        numericExplanation = WeightedCompetitionExplanation(s, contestant, category, performance, false, roll, score);
                }
                // Rules 4 explains once every score is in: a throw's story is where it finished.
                var you = players.FirstOrDefault(contestant => contestant.isPlayer);
                if (widened && you != null)
                    numericExplanation = WidenedCompetitionExplanation(s, you, category, performance, false, thrown,
                        playerRoll, playerLuckRoll, playerRaw);
            }
            var winner = s.competitionScores.OrderByDescending(x => x.score).First().contestantId;
            if (s.phase == EpisodePhase.HoH) { s.hohId = winner; s.Find(winner).hohWins++; }
            else if (s.phase == EpisodePhase.Veto) { s.vetoHolderId = winner; s.Find(winner).vetoWins++; }
            else if (s.phase == EpisodePhase.FinalHoHPart1) s.finalPart1WinnerId = winner;
            else if (s.phase == EpisodePhase.FinalHoHPart2) s.finalPart2WinnerId = winner;
            else { s.hohId = winner; s.Find(winner).hohWins++; }
            MoodLift(s, winner);
            s.competitionResolved = true;
            LogCompetitionDefinition(s);
            LogCompetitionStandings(s);
            // The last out of a Head of Household are the week's Have-Nots; the veto's runner-up
            // and last finisher take its prize and punishment. Both read the standings just committed.
            HaveNots.Assign(s);
            HaveNots.AwardVetoPrizes(s);
            // The one line that says the player threw it, for them alone: the results card tags their
            // row from it, and the house is not told.
            if (thrown) Log(s, ThrowEventKind, "You threw the " + AwardName(s.phase) + " competition.", s.playerId);
            if (s.competitionRulesVersion >= 3) LogCompetitionInput(s, performance, false, numericExplanation);
            else if (s.competitionRulesVersion >= 2 && players.Any(c => c.isPlayer))
                Log(s, "competition-performance", "Player performance input: " + Math.Round(performance * 100).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "% · "
                    + Math.Round(performance * 2, 2).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + (s.phase == EpisodePhase.FinalHoHPart1
                        ? " effective endurance points (capped at 10)." : " performance bonus points.")
                    + " Character statistics and seeded competition rolls also determine placement; full performance does not guarantee a win.");
            RecordCompetition(s, players, category, thrown ? CompetitionEntry.Thrown : CompetitionEntry.Played, performance);
            Log(s, "competition", "Competition winner: " + Name(s, winner) + " · " + category + ".");
        }

        private static void LogCompetitionStandings(EpisodeState state)
        {
            if (state.competitionRulesVersion < 2) return;
            int place = 0;
            Log(state, "competition-standings", "Committed competition standings: " + string.Join("; ",
                state.competitionScores.OrderByDescending(score => score.score).Select(score =>
                    (++place) + ". " + Name(state, score.contestantId) + " "
                    + score.score.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))) + ".");
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
            // Bought turns are added on top rather than folded into the rule, because the allowance
            // and what was paid for beyond it are different facts. A season under the legacy flat
            // allowance still gets what it bought: refusing it would be charging for nothing.
            // What the week gives, what was bought, and what a storyline left behind — which can be
            // negative — less a conversation for a Have-Not, so the whole thing is floored at one. A
            // week with no interactions at all would be a week the player cannot play.
            WeekRulesOn(s) ? WindowBudget(s)
            : Math.Max(1, EarnedSocialActionBudget(s) + Math.Max(0, s.boughtActionPoints)
                        + Storylines.SocialActions(s) - HaveNots.ActionCost(s));

        /// <summary>The allowance before anything is bought: what the week gives you for free.</summary>
        public static int EarnedSocialActionBudget(EpisodeState s) =>
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
        public static int SocialActionsSpent(EpisodeState s) => WeekRulesOn(s) ? WindowSpent(s) : s.socialActions + s.outOfPhaseSocialActions;

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
            // Warmth against the port's line of 30. From the strategy windows, deals, alliances and
            // this week's pleas are weighed too (StrategyRules); the story system adds what it wrote -
            // a bond, a grudge - and may refuse outright (a nemesis, a cold partner). With neither,
            // this saves exactly who it always did.
            double line = StrategyRules.VetoLine(s, s.vetoHolderId);
            return s.nominees.OrderByDescending(id => SaveWeight(s, s.vetoHolderId, id))
                .FirstOrDefault(id => SaveWeight(s, s.vetoHolderId, id) > line
                                      && !StoryConsumers.WillNotSave(s, s.vetoHolderId, id)
                                      && !ColdPartnerLeavesThemUp(s, s.vetoHolderId, id));
        }

        /// <summary>
        /// How reluctant a Head of Household is to put somebody up: the strategy windows' reluctance,
        /// plus what the story system wrote between them. Each reduces to the score without its rules,
        /// so the sum is the score in a season that plays neither.
        /// </summary>
        public static double NominationWeight(EpisodeState s, string hohId, string id) =>
            StrategyRules.NominationReluctance(s, hohId, id) + StoryConsumers.NominationPreference(s, hohId, id) - s.Score(hohId, id)
            // Under agency, how dangerous they are, as the Head of Household and their pact read it (NPC-AGENCY-PLAN.md §5.1).
            - ThreatTerm(s, hohId, id);

        /// <summary>How much a veto holder wants to save a nominee: the strategy windows' willingness plus the story's terms.</summary>
        public static double SaveWeight(EpisodeState s, string holderId, string id) =>
            StrategyRules.VetoWillingness(s, holderId, id) + StoryConsumers.SavePreference(s, holderId, id) - s.Score(holderId, id);

        /// <summary>
        /// The showmance veto dilemma from the other side: a partner who is cold and steady enough
        /// (Honest −2 or lower, Steady 2 or higher) may leave you up. A keyed coin, so the Execute-
        /// time check and the Advance agree on it.
        /// </summary>
        private static bool ColdPartnerLeavesThemUp(EpisodeState s, string holderId, string nomineeId)
        {
            if (!StoryAt(s, StoryRules.Bonds) || !Bonds.Holds(s, holderId, nomineeId, BondKinds.Showmance)) return false;
            var axes = Personality.Of(s.Find(holderId));
            return axes.Honest <= -2 && axes.Steady >= 2
                   && StoryRandom.Chance(s, "w" + s.week + ":veto-partner:" + holderId + ":" + nomineeId, 0.5);
        }

        /// <summary>
        /// Who an NPC Head of Household names as the replacement: the real target of a backdoor plan
        /// they made at the nominations if that person is eligible, otherwise the one they are least
        /// reluctant to name (<see cref="NominationWeight"/>) - with neither system's rules, exactly
        /// their lowest score, as it always was.
        /// </summary>
        private static string NpcReplacement(EpisodeState s)
        {
            var candidates = ReplacementCandidates(s).ToList();
            string planned = BackdoorPlanned(s);
            if (planned != null && candidates.Any(c => c.id == planned)) return planned;
            return candidates.OrderBy(c => NominationWeight(s, s.hohId, c.id)).First().id;
        }

        private static void Nominate(EpisodeState s, string first, string second)
        {
            Require(s.nominees.Count == 0, "Nominations are already committed.");
            var eligible = new HashSet<string>(NominationCandidates(s).Select(c => c.id));
            Require(first != second && eligible.Contains(first ?? "") && eligible.Contains(second ?? ""), "Choose two distinct eligible houseguests.");
            s.nominees = new List<string> { first, second };
            foreach (var nominee in s.nominees) NominationEffects(s, nominee);
            StoryNominated(s, s.nominees);
            SettleDeals(s, DealResolution.Verdicts(s, DealResolution.Nominates, s.hohId, s.nominees.ToList()));
            Log(s, "nomination", Name(s, s.hohId) + Verb(s, s.hohId, " nominates ", " nominate ")
                + Target(s, first, s.hohId) + " and " + Target(s, second, s.hohId) + ".");
            // Under the commitment rules (C2) an ally who puts the player up has turned on their pact.
            if (s.hohId != s.playerId && s.nominees.Contains(s.playerId)) Betrayal(s, s.hohId, Allegiance.Nominated, false);
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
                var stress = new[] { "Relaxed", "Normal", "Tense", "Stressed", "Overwhelmed" };
                MoodStep(guest, -2);
                guest.stressLevel = stress[Math.Min(4, Array.IndexOf(stress, guest.stressLevel) + 2)];
            }
            // The text stays nameless: WebEvictionVoting.Memory matches a memory to a nominee by
            // name (+2 toward whoever it names, parity-pinned to the web's ten factors), so naming
            // the Head of Household here would move every recorded season's ballots. The subject
            // is on the memory; the diary names them from it (MemoryWords.Said).
            Remember(s, id, s.hohId, "Nominated me in week " + s.week + ".", false);
            foreach (var promise in s.promises.Where(p => p.status == PromiseStatus.Active && p.fromId == s.hohId &&
                ((p.kind == PromiseKind.Safety && p.toId == id) || (p.kind == PromiseKind.AllianceLoyalty && s.Allied(p.fromId, id)))).ToArray())
                SettlePromise(s, promise, PromiseStatus.Broken);
        }

        /// <summary>The mood ladder, worst to best: what a houseguest's face and chip say.</summary>
        public static readonly string[] Moods = { "Angry", "Upset", "Neutral", "Content", "Happy" };

        /// <summary>
        /// Moves a mood along <see cref="Moods"/>: the web's mental-state steps (mental-state.ts) -
        /// nominated -2, saved +2, a competition won +2. The web defines all three and calls only
        /// the first, so a nominee there is Angry for the rest of the season, as one was here
        /// (playtest, 2026-09-27). Nothing in the rules reads a mood; it is what the house shows.
        /// </summary>
        private static void MoodStep(ContestantState guest, int steps)
        {
            if (guest == null || steps == 0) return;
            int at = Array.IndexOf(Moods, guest.mood);
            if (at < 0) at = Array.IndexOf(Moods, "Neutral");
            guest.mood = Moods[Math.Max(0, Math.Min(Moods.Length - 1, at + steps))];
        }

        /// <summary>
        /// Saved, or a competition won: two steps up. Under the rule the story's stress relief plays
        /// under (M5, bonds), for the same reason - a season that predates it keeps its moods as they
        /// were, so a replay of one is exact - and a season this build starts has it from week one.
        /// </summary>
        private static void MoodLift(EpisodeState s, string id)
        {
            if (StoryAt(s, StoryRules.Bonds)) MoodStep(s.Find(id), 2);
        }

        /// <summary>
        /// Who an Angry or Upset houseguest is sore at, when the whole house saw why: the Head of
        /// Household who nominated them, or who - with the veto holder - named them the replacement.
        /// The ceremony's grudge when the story keeps one, else this week's or last week's Head of
        /// Household. Never a private grudge (a lie found out, a deal broken): what somebody thinks
        /// in private is what the game does not tell you. Null when nobody is to blame in public.
        /// </summary>
        public static string MoodTarget(EpisodeState s, string id, out string why)
        {
            why = null;
            var guest = s?.Find(id);
            if (guest == null || (guest.mood != "Angry" && guest.mood != "Upset")) return null;
            var ceremony = s.story?.grudges?
                .Where(g => g.holderId == id && g.targetId != id && s.Find(g.targetId) != null
                    && (g.cause == GrudgeCauses.Nominated || g.cause == GrudgeCauses.Replacement || g.cause == GrudgeCauses.ReplacementVeto))
                .OrderByDescending(g => g.originWeek).ThenByDescending(g => g.severity).FirstOrDefault();
            if (ceremony != null)
            {
                why = (ceremony.cause == GrudgeCauses.Nominated ? "nominated them" : "put them on the block as the replacement")
                    + " in week " + ceremony.originWeek;
                return ceremony.targetId;
            }
            if (guest.nominationWeeks.Contains(s.week) && !string.IsNullOrEmpty(s.hohId) && s.hohId != id)
            { why = "nominated them in week " + s.week; return s.hohId; }
            if (guest.nominationWeeks.Contains(s.week - 1) && !string.IsNullOrEmpty(s.previousHohId) && s.previousHohId != id)
            { why = "nominated them in week " + (s.week - 1); return s.previousHohId; }
            return null;
        }

        /// <summary>
        /// A new week takes every mood one step back toward Neutral, the way the story's week turn
        /// takes one step off stress, and under the same rule (see <see cref="MoodLift"/>): a
        /// nomination is felt, and then it is last week's. Native; the web has no recovery at all.
        /// </summary>
        private static void MoodsSettle(EpisodeState s)
        {
            int neutral = Array.IndexOf(Moods, "Neutral");
            foreach (var guest in s.Active)
            {
                int at = Array.IndexOf(Moods, guest.mood);
                if (at >= 0 && at != neutral) MoodStep(guest, at < neutral ? 1 : -1);
            }
        }

        private static void ResolveVeto(EpisodeState s, bool use, string saved, string replacement)
        {
            // The block as it stood before this decision. A veto commitment is kept by lifting the
            // partner off it, and reading the block afterwards would find them already gone.
            var blockBefore = s.nominees.ToList();
            if (use)
            {
                Require(!VetoIsLockedAtFinalFour(s),
                    "At the final four a veto holder who is not on the block cannot use the veto.");
                Require(s.nominees.Contains(saved ?? ""), "The veto can only save a current nominee.");
                Require(ReplacementCandidates(s).Any(), "The veto cannot be used because no legal replacement exists.");
                if (s.hohId != s.playerId) replacement = NpcReplacement(s);
                Require(ReplacementCandidates(s).Any(c => c.id == replacement), "Choose an eligible replacement; the HoH and veto holder are immune.");
                s.nominees.Remove(saved); s.nominees.Add(replacement); NominationEffects(s, replacement, false);
                MoodLift(s, saved);
                StoryReplacement(s, replacement);
                Change(s, saved, s.vetoHolderId, 25, Name(s, s.vetoHolderId) + " used POV to save " + Target(s, saved, s.vetoHolderId));
                Change(s, replacement, s.hohId, -20, Name(s, s.hohId) + " named " + Target(s, replacement, s.hohId) + " as replacement nominee");
                if (s.hohId != s.vetoHolderId) Change(s, replacement, s.vetoHolderId, -15, Name(s, s.vetoHolderId) + " used POV forcing " + Target(s, replacement, s.vetoHolderId) + " on the block");
                Log(s, "veto", Name(s, s.vetoHolderId) + Verb(s, s.vetoHolderId, " saves ", " save ")
                    + Target(s, saved, s.vetoHolderId) + "; " + Target(s, replacement, s.vetoHolderId)
                    + Verb(s, replacement, " is", " are") + " the replacement nominee.");
            }
            else Log(s, "veto", Name(s, s.vetoHolderId) + Verb(s, s.vetoHolderId, " declines ", " decline ")
                + "to use the veto. Nominations stand.");
            var vetoVerdicts = DealResolution.Verdicts(s, DealResolution.Vetoes, s.vetoHolderId,
                nominees: blockBefore, savedId: use ? saved : null, used: use);
            SettleDeals(s, vetoVerdicts);
            // Under the commitment rules (C2) an ally who broke their veto commitment to the player has
            // turned on their pact: the decision is the house's to see.
            foreach (var verdict in vetoVerdicts.Where(v => v.status == DealStatus.Broken && v.actorId != null
                         && v.actorId != s.playerId && DealResolution.Partner(v.deal, v.actorId) == s.playerId))
                Betrayal(s, verdict.actorId, Allegiance.BrokeDeal(verdict.deal.type), false);
            // Naming a replacement is a nomination, and a safety pact with the person named is
            // broken by it exactly as it would be at the ceremony itself.
            if (use) SettleDeals(s, DealResolution.Verdicts(s, DealResolution.Nominates, s.hohId,
                new List<string> { replacement }));
            // So is putting an ally up in a saved nominee's place (C2).
            if (use && replacement == s.playerId && s.hohId != s.playerId) Betrayal(s, s.hohId, Allegiance.NamedReplacement, false);
            // Under the commitment rules (C1) the Head of Household's nominating is done for the
            // week, so a safety pact of theirs with somebody they spared is kept - and the one spared
            // thinks the better of them for it, at the pact's weight. Before them it could only break.
            if (CommitmentRulesOn(s)) SettleDeals(s, DealResolution.Verdicts(s, DealResolution.Spares, s.hohId, s.nominees.ToList()));
            s.vetoResolved = true;
            RecordVeto(s, use, saved, replacement);
            // A question about a decision already taken is no longer on the table.
            if (StrategyRules.Apply(s))
                foreach (var offer in s.deals.Where(d => d.status == DealStatus.Proposed && d.type == DealKind.VetoUse))
                    offer.status = DealStatus.Expired;
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

        /// <summary>
        /// The count on the tail of the eviction line: "By a vote of 3 to 1." - the house's count,
        /// the evictee's first - and the Head of Household's deciding vote named when the house tied
        /// ("By a vote of 2 to 2; Maya Hassan broke the tie.", "you broke the tie" for a player Head
        /// of Household, who is spoken to), since the format reads that live. A single voter is "By a
        /// single vote.", naming nobody. The line still opens with the evictee's name, which the
        /// readers parse the subject from.
        /// </summary>
        public static string CountTail(EpisodeState s, string evicted, IReadOnlyList<(string id, int count)> tally)
        {
            int against = tally.FirstOrDefault(x => x.id == evicted).count;
            int others = tally.Where(x => x.id != evicted).Sum(x => x.count);
            if (against + others == 1) return "By a single vote.";
            string tail = "By a vote of " + against + " to " + others;
            if (against == others && !string.IsNullOrEmpty(s.hohId))
                tail += "; " + (s.hohId == s.playerId ? "you" : Name(s, s.hohId)) + " broke the tie";
            return tail + ".";
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
            foreach (var promise in s.promises.Where(p => FinalChoiceSettles(s, p, s.hohId)).ToArray())
                SettlePromise(s, promise, promise.toId == selected ? PromiseStatus.Fulfilled : PromiseStatus.Broken);
            SettleDeals(s, DealResolution.Verdicts(s, DealResolution.Selects, s.hohId, selectedId: selected));
            RecordFinalEviction(s, target, s.Active.Where(c => c.id != s.hohId).Select(c => c.id).ToList());
            RecordJurorStanding(s, target);
            s.Find(target).status = ContestantStatus.Jury;
            if (StoryOn(s)) Bonds.Apart(s, target);
            s.jurySentiment = WebJurySentiment.AddJuror(s.jurySentiment, target, Name(s, target), s.Score(s.playerId, target));
            // No social week follows this eviction, so the settle that ends an evictee's alliances
            // never comes; end them here. Behind the same boundary as that settle, so a save that
            // predates autonomy keeps exactly the alliances it had.
            var ended = NpcSocialState.AutonomyHasBegun(s) ? NpcAlliances.EndBroken(s) : new List<AllianceState>();
            s.oathOpportunities.Clear(); // No social oath decisions remain after final eviction.
            s.votes.Clear();
            Log(s, "final-eviction", Name(s, s.hohId) + Verb(s, s.hohId, " takes ", " take ")
                + Target(s, selected, s.hohId) + " to the final two. " + TargetStart(s, target, s.hohId)
                + Verb(s, target, " joins", " join") + " the jury.");
            Phase(s, EpisodePhase.JuryQuestioning);
            s.juryExchanges.Clear(); s.juryQuestionIndex = 0;
            // A player production removed has no questions to ask: straight to the speeches.
            if (JuryExchangeCount(s) > 0) PrepareJuryQuestion(s); else BeginFinalSpeeches(s);
            // Last in the step, as at the weekly settle: nothing minted above moves, and it is the
            // line the status bar shows.
            TellThePlayerWhichAlliancesEnded(s, ended.Where(a => a.members.Contains(s.playerId)).ToList());
            ReconcileOpportunities(s);
            // Under the commitment rules (C1, X4) whatever still bound the one evicted ends with them,
            // as at the weekly eviction: after the reconcile, so a deal the player took stays on the
            // record as taken, and drawing nothing.
            if (CommitmentRulesOn(s)) EndWithTheEvictee(s, target);
        }

        private static void ResolveJury(EpisodeState s)
        {
            var finalists = s.Active.ToArray(); Require(finalists.Length == 2, "The jury requires exactly two finalists.");
            // A juror's ballot stays private until the player's own is in: nothing is cast, and
            // nothing read out, before then. This used to cast and publish every other juror's vote
            // first and only then refuse to count them, so a juror who continued before voting read
            // the whole jury's ballots and still had a vote to cast. A player production removed is
            // not a juror: there is no vote to wait for.
            Require(s.Active.Any(c => c.id == s.playerId) || s.votes.Any(v => v.voterId == s.playerId)
                    || s.Find(s.playerId).status == ContestantStatus.Expelled,
                "Cast your jury vote for a finalist first.");
            foreach (var juror in s.contestants.Where(c => c.status == ContestantStatus.Jury || c.status == ContestantStatus.Evicted))
            {
                if (juror.isPlayer || s.votes.Any(v => v.voterId == juror.id)) continue;
                // Two independent final impressions, the same size as the two rolls this used to be
                // the whole of. What changed is what they are applied to: a juror now weighs how a
                // finalist played as well as how they were treated, so the game a finalist ran is
                // worth something at the end rather than nothing.
                double firstScore = WebJuryVoting.Score(s, juror.id, finalists[0].id)
                                    + Roll(s) * WebJuryVoting.FinalImpression * 2 - WebJuryVoting.FinalImpression;
                double secondScore = WebJuryVoting.Score(s, juror.id, finalists[1].id)
                                     + Roll(s) * WebJuryVoting.FinalImpression * 2 - WebJuryVoting.FinalImpression;
                var preferred = firstScore > secondScore ? finalists[0] : finalists[1];
                var passedOver = preferred.id == finalists[0].id ? finalists[1] : finalists[0];
                s.votes.Add(new VoteState
                {
                    voterId = juror.id, targetId = preferred.id,
                    reason = WebJuryVoting.Reason(s, juror.id, preferred, passedOver),
                });
                Log(s, "jury-vote", Name(s, juror.id) + Verb(s, juror.id, " votes for ", " vote for ")
                    + Target(s, preferred.id, juror.id) + " to win.");
            }
            int firstVotes = s.votes.Count(v => v.targetId == finalists[0].id), secondVotes = s.votes.Count(v => v.targetId == finalists[1].id);
            var ordered = firstVotes > secondVotes ? finalists : new[] { finalists[1], finalists[0] };
            // The source reveal's strict > comparison awards a tie to the second cast-order finalist.
            if (firstVotes == secondVotes)
                Log(s, "jury-tie", "Jury tie: the source game's tie rule awards the win to the second finalist in cast order.");
            s.winnerId = ordered[0].id; s.runnerUpId = ordered[1].id;
            ordered[0].status = ContestantStatus.Winner; ordered[1].status = ContestantStatus.RunnerUp;
            // The story's threads end here, reading the jury they were heading for (plan 31).
            StoryFinale(s);
            Phase(s, EpisodePhase.Finished); Log(s, "winner", "Gamesim winner: " + ordered[0].name + "!");
            ReconcileOpportunities(s);
        }

        private static void Social(EpisodeState s, EpisodeCommand c)
        {
            RequireConversationWindow(s, c);
            Require(s.Find(s.playerId).status == ContestantStatus.Active, "Evicted players can follow the season but cannot influence it.");
            var target = s.Find(c.targetId);
            // Listening in names nobody: the engine draws the pair it overhears. Every other action
            // is aimed at somebody, and at somebody still in the house.
            if (c.kind != EpisodeCommandKind.Eavesdrop)
                Require(target != null && target.status == ContestantStatus.Active && !target.isPlayer, "Approach an active housemate.");
            Require(SocialActionsSpent(s) < SocialActionBudget(s),
                "This social window is complete. Continue the episode.");
            switch (c.kind)
            {
                case EpisodeCommandKind.Talk:
                    Change(s, s.playerId, target.id, 4);
                    Remember(s, target.id, s.playerId, "We spent time talking in week " + s.week + ".", true);
                    // The line says what happened. It used to be the houseguest's cast-template
                    // motive, verbatim - their goal, a lore facet the player is meant to learn
                    // (Lore.Facets.Goal, through game talk at rapport 3 or a story's reveal) - so
                    // one plain conversation printed it to Recent events, the toast and the recap
                    // (UI-UX-PASS-PLAN decision 19). The goal appears only once the facet is learned.
                    Log(s, "conversation", "You and " + target.name + " talked about the game.", s.playerId, target.id); break;
                case EpisodeCommandKind.FormAlliance:
                    // Under the commitment rules the proposal asks the invitation's question, one roll
                    // on its odds, and a no spends the action like a yes (ACTIONS-DEALS-ALLIANCES-PLAN C4).
                    if (CommitmentRulesOn(s)) { ProposeAlliance(s, target); break; }
                    // The body moved to FormAllianceWith so a story can form one with a keyed roll;
                    // this path passes the season's own stream, exactly as it always drew.
                    FormAllianceWith(s, target, () => Roll(s));
                    if (StoryAt(s, StoryRules.Bonds)) Knowledge.AllianceFormed(s, s.alliances.Last());
                    break;
                // "Bring {name} into {pact}" (C5): the houseguest asked is the one in front of the player.
                case EpisodeCommandKind.BringIntoAlliance: BringIntoAlliance(s, target, c); break;
                case EpisodeCommandKind.LeaveAlliance:
                    // Under the commitment rules (C5) the leave names its pact, and in a pact of three or
                    // more it takes only the player out; a pact of two ends as below.
                    if (CommitmentRulesOn(s)) { LeavePact(s, target, c); break; }
                    var alliance = s.alliances.FirstOrDefault(a => a.active && a.members.Contains(s.playerId) && a.members.Contains(target.id));
                    Require(alliance != null, "No shared alliance is active."); alliance.active = false; Change(s, target.id, s.playerId, -15);
                    Remember(s, target.id, s.playerId, "Left our alliance.", true); Log(s, "alliance", "You left the alliance with " + target.name + ".", s.playerId, target.id);
                    foreach (var member in alliance.members.Where(id => id != s.playerId)) StoryAllianceLeft(s, member, s.playerId);
                    break;
                case EpisodeCommandKind.PromiseSafety: MakePromise(s, target.id, PromiseKind.Safety, null); break;
                case EpisodeCommandKind.PromiseFinalTwo: MakePromise(s, target.id, PromiseKind.FinalTwo, null); break;
                case EpisodeCommandKind.PromiseVote:
                    Require(s.phase == EpisodePhase.Campaign && Voters(s).Any(x => x.id == s.playerId) && s.nominees.Contains(c.secondTargetId ?? ""), "Choose a valid eviction target for your voting promise.");
                    // Under the commitment rules (R0) a promise to vote somebody out is never made to
                    // them: kept, it earned their thanks on the way to the jury (X2's engine half).
                    Require(!CommitmentRulesOn(s) || c.secondTargetId != target.id, "You cannot promise somebody that you will vote them out.");
                    MakePromise(s, target.id, PromiseKind.Vote, c.secondTargetId); break;
                case EpisodeCommandKind.ShareInformation:
                    var known = s.memories.LastOrDefault(m => m.ownerId == s.playerId && m.subjectId != target.id);
                    Require(known != null, "You have no personal information to share yet.");
                    Remember(s, target.id, known.subjectId, "Heard from you: " + known.text, true);
                    Change(s, s.playerId, target.id, 3); Log(s, "information", "You shared something you personally knew with " + target.name + ".", s.playerId, target.id); break;
                case EpisodeCommandKind.AskForIntel: AskForIntel(s, target); break;
                case EpisodeCommandKind.Eavesdrop: Eavesdrop(s, c); break;
                case EpisodeCommandKind.SpreadLie: SpreadLie(s, target, c.secondTargetId); break;
                case EpisodeCommandKind.VentAbout: VentAbout(s, target, c.secondTargetId); break;
                case EpisodeCommandKind.SchemeAgainst: SchemeAgainst(s, target); break;
                case EpisodeCommandKind.ProposeDeal: ProposeDeal(s, target, c); break;
                case EpisodeCommandKind.CallTheVote: CallTheVote(s, target, c); break;
                // The web's situation moves (C7), under the commitment rules only.
                case EpisodeCommandKind.Negotiate: Negotiate(s, target, c); break;
                case EpisodeCommandKind.SmallTalk:
                    Converse(s, target, WebSocialVocabulary.SmallTalk(Roll(s)),
                        "You passed the time with " + target.name + "."); break;
                case EpisodeCommandKind.PersonalChat:
                    Converse(s, target, WebSocialVocabulary.PersonalChat(Roll(s)),
                        "You told " + target.name + " something about yourself."); break;
                case EpisodeCommandKind.RelationshipBuilding:
                    Converse(s, target, WebSocialVocabulary.RelationshipBuilding(Roll(s)),
                        "You spent real time with " + target.name + "."); break;
                case EpisodeCommandKind.StrategicDiscussion:
                    Converse(s, target, WebSocialVocabulary.StrategicDiscussion(Roll(s)),
                        "You talked tactics with " + target.name + "."); break;
                case EpisodeCommandKind.DiscussGame: DiscussGame(s, target); break;
                case EpisodeCommandKind.ShareSecret: ShareSecret(s, target); break;
                case EpisodeCommandKind.SpreadRumor: SpreadRumor(s, target, c); break;
                case EpisodeCommandKind.PillowTalk:
                case EpisodeCommandKind.Cook:
                case EpisodeCommandKind.InviteUp:
                case EpisodeCommandKind.PublicDefense:
                case EpisodeCommandKind.AllianceMeet:
                case EpisodeCommandKind.CompPractice:
                case EpisodeCommandKind.PlayAGame: RoomAct(s, target, c); break;
                default: throw new RuleException("Unsupported social action.");
            }
            SpendSocialAction(s);
            // A conversation is the web's other beat trigger: it advances a story the houseguest is
            // in, raises a broken word waiting between you, and sometimes starts something new.
            if (target != null && TopicChance(c.kind) > 0) StoryConversation(s, target.id, c.kind, c.secondTargetId);
        }

        /// <summary>
        /// Where a word with somebody can be said: free time and the campaign, and outside them only a
        /// window the week opens. The social actions' gate, and the free exit's (C2), which is said where
        /// a leave is said and costs nothing.
        /// </summary>
        private static void RequireConversationWindow(EpisodeState s, EpisodeCommand c)
        {
            // The rule itself is ConversationWindowRefusal's (EpisodeEngine.Negotiation.cs), so a screen can
            // ask it before it offers a word; refused here in the same words.
            string refusal = ConversationWindowRefusal(s, c.targetId, c.kind);
            Require(refusal == null, refusal);
        }

        /// <summary>
        /// Books one social action against the right counter.
        ///
        /// <para>Campaigning is not the social week, and the reference build counts those separately
        /// even though they draw on the same budget — the in-phase counter resets with the phase and
        /// this one has to survive it.</para>
        /// </summary>
        private static void SpendSocialAction(EpisodeState s)
        {
            if (s.phase == EpisodePhase.Social) s.socialActions++;
            else s.outOfPhaseSocialActions++;
            SpendInWindow(s);
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
        /// <para>They answer. The doc comment here used to say that anything the housemate knew
        /// "reaches the player through the same private-memory channel every other disclosure in
        /// this game uses" - and nothing ever put anything into that channel. The memory this wrote
        /// was owned by the TARGET, so the houseguest remembered being asked and the player, who
        /// had spent one of six actions for the week, learned nothing at all. Eavesdrop two blocks
        /// below writes its memory to <c>s.playerId</c>; this now does the same.</para>
        ///
        /// <para>What they hand over is their own read on somebody else, which is a fact they
        /// genuinely hold rather than one invented here, and it is the same shape of disclosure
        /// Eavesdrop makes. The subject is chosen from the roll ALREADY DRAWN for the trust
        /// improvement: a second <see cref="Roll"/> would advance the stream and re-roll every
        /// season from this point, which is never a local change in a seeded simulation.</para>
        /// </summary>
        private static void AskForIntel(EpisodeState s, ContestantState target)
        {
            double roll = Roll(s);
            double improvement = 2 + Math.Floor(roll * 3);
            Change(s, s.playerId, target.id, improvement);
            Remember(s, target.id, s.playerId, "You asked me what I knew in week " + s.week
                + ". It seems my read on this house is worth something to you.", true);

            var about = s.Active
                .Where(actor => actor.id != s.playerId && actor.id != target.id)
                .OrderBy(actor => actor.id, StringComparer.Ordinal)
                .ToList();
            if (about.Count == 0)
            {
                Log(s, "information", "You asked " + target.name + " what they had been hearing.",
                    s.playerId, target.id);
                return;
            }

            var subject = about[Math.Min(about.Count - 1, (int)(roll * about.Count))];
            double between = s.Score(target.id, subject.id);
            AddStanding(s, target.id, subject.id, ClaimSource.Told, between);
            string reading = between >= 25 ? "is solid with"
                : between <= -25 ? "does not trust"
                : "is still working out";
            Remember(s, s.playerId, target.id, target.name + " told me in week " + s.week + " that they "
                + reading + " " + subject.name + ".", true);
            Log(s, "information", "You asked " + target.name + " what they had been hearing. They "
                + reading + " " + subject.name + ".", s.playerId, target.id);
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
        private static void Eavesdrop(EpisodeState s, EpisodeCommand c)
        {
            var others = s.Active.Where(x => !x.isPlayer).ToList();
            Require(others.Count >= 2, "There is nobody to overhear right now.");

            // The pair on screen when the house names one (STRATEGY-LOOP-PLAN.md section 2);
            // otherwise drawn, before the outcome roll, so the pair is the same whether or not you
            // are caught: the conversation was happening either way. A recorded season names
            // nobody and draws exactly as it always did.
            var named = s.Find(c.targetId);
            var namedSecond = s.Find(c.secondTargetId);
            ContestantState first, second;
            if (named != null && namedSecond != null && named.id != namedSecond.id && !named.isPlayer && !namedSecond.isPlayer
                && named.status == ContestantStatus.Active && namedSecond.status == ContestantStatus.Active)
            { first = named; second = namedSecond; }
            else
            {
                first = Draw(s, others);
                second = Draw(s, others.Where(x => x.id != first.id).ToList());
            }

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
            AddStanding(s, first.id, second.id, ClaimSource.Overheard, between);
            string vote = OverheardVote(s, first, second);
            Remember(s, s.playerId, first.id, "I overheard " + first.name + " and " + second.name
                + " in week " + s.week + ". They " + reading + "." + vote, true);
            Log(s, "eavesdrop", "You overheard " + first.name + " and " + second.name + ". They " + reading + "." + vote,
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
            // Under the levers the memory sounds like what it is, so the ballot's memory term reads
            // it as a lie about that person rather than as a neutral mention of their name.
            Remember(s, recipient.id, about.id, "You told me something " + (LeverRulesOn(s) ? "suspicious " : "") + "about " + about.name
                + " in week " + s.week + ". I have not checked it.", true);

            bool discovered = Roll(s) < LieDiscoveryChance;
            if (discovered)
            {
                Change(s, s.playerId, about.id, -15, about.name + " found out what you had been saying", "lie");
                Remember(s, about.id, s.playerId, "I found out what you were telling people about me.", true);
                StoryLieDiscovered(s, about.id, s.playerId);
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
        /// Records that an opening beat has been played.
        ///
        /// <para>Bookkeeping, not an event: no roll, no log entry and no standing changes. It exists
        /// so the intro does not replay every time a season is loaded, which is the one thing an
        /// unskippable-feeling cinematic does that nothing else in this game does.</para>
        ///
        /// <para>That includes the meet-and-greet. Marking it used to warm the whole house to the
        /// player by a flat amount; now the introductions move people, one houseguest and one chosen
        /// approach at a time (<see cref="Introduce"/>), and anyone the player skips gets nothing, as
        /// in the reference build. Paying out here as well would count every introduction twice. What
        /// the mark does do is close the introductions: once it is recorded they are over.</para>
        ///
        /// <para>Deliberately not confined to a phase. The beats run before the first competition, so
        /// the season is in its opening social week the whole time — but a player who quits during
        /// the walk-in and comes back is still owed the rest of the sequence, and refusing the mark
        /// because the phase had moved on would replay the intro forever.</para>
        /// </summary>
        private static void MarkOpeningBeat(EpisodeState s, string beat)
        {
            Require(OpeningBeat.IsKnown(beat), "That is not one of the opening beats.");
            if (s.openingBeatsSeen.Contains(beat)) return;
            s.openingBeatsSeen.Add(beat);
            // The meet-and-greet over, move-in night has time for one real conversation.
            if (beat == OpeningBeat.MeetAndGreet && IsFirstNight(s)) StartAimed(s, "first-night", null, StoryAnchors.EvictionNight);
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

        // ---------------------------------------------------------------- the event layer
        //
        // Ported from house-event-system.ts. The reference asks a language model for a bespoke
        // situation and falls back to a template catalog; only the catalog is ported, and that is
        // deliberate — a save whose content came from a network call could not be replayed, and
        // every season here has to reproduce exactly from its seed.

        /// <summary>
        /// Puts this week's situation in front of the player, if there is one.
        ///
        /// <para>Drawn at the same transition the house strikes its bargains at, because that is
        /// when the week has settled enough for a situation to be about something. One a week, and
        /// only where the player is still in the house to have it happen to them.</para>
        /// </summary>
        private static void OfferHouseEvent(EpisodeState s)
        {
            if (!HouseEvents.Ready(s)) return;

            // The season's own state first. An emergent situation is about something the player has
            // spent weeks building, and a catalogue entry drawn over the top of it would be the
            // house talking about nothing while a rivalry it can see goes unremarked.
            var drawn = HouseEventSources.Emergent(s, s.nextSequence);

            // Then the week going wrong, which only starts once the house has been in it a while.
            if (drawn == null && Roll(s) < CrisisChance) drawn = HouseEventSources.Crisis(s, Roll(s), s.nextSequence);

            // Then the catalogue.
            if (drawn == null) drawn = HouseEvents.Draw(s, Roll(s), s.nextSequence);
            if (drawn == null) return;

            s.houseEvents.Add(drawn);
            Log(s, "house-event", drawn.title + ". " + drawn.narrative, s.playerId);
        }

        /// <summary>
        /// How often a week goes wrong, once the house has been in it long enough.
        ///
        /// <para>Not the reference's — its crises are scheduled by a system with its own cadence and
        /// phase filters. One in four weeks is frequent enough to be a risk the player plans around
        /// and rare enough that it is still bad luck when it happens.</para>
        /// </summary>
        public const double CrisisChance = 0.25;

        /// <summary>
        /// Starts a story, if the season is in a position to tell one.
        ///
        /// <para>Before the week's ordinary situation, and it takes the slot when it lands — a
        /// storyline chapter <i>is</i> a house event, so offering one of each would be two
        /// situations in a week that allows one.</para>
        /// </summary>
        private static void BeginStoryline(EpisodeState s)
        {
            if (!HouseEvents.Ready(s)) return;
            if (!Storylines.Ready(s)) return;
            if (!Storylines.Begin(s, Roll(s), s.nextSequence, out var story, out var chapter)) return;

            // Both or neither. A record whose chapter went missing is a story nobody can answer.
            s.houseEvents.Add(chapter);
            s.storylines.Add(story);
            Log(s, "storyline", story.title + ". " + chapter.narrative, s.playerId);
        }

        /// <summary>
        /// The house being a house: narration, no question, and no place in the week's one situation.
        ///
        /// <para>Kept separate from <see cref="OfferHouseEvent"/> because an ambient line is not a
        /// situation — it arrives already settled and must never occupy the slot that asks the
        /// player something.</para>
        /// </summary>
        private static void NarrateHouse(EpisodeState s)
        {
            if (s.week < s.eventRulesStartWeek) return;
            if (s.houseEvents.Count >= HouseEvents.Ceiling) return;
            if (s.Active.Count(c => !c.isPlayer) == 0) return;
            if (s.houseEvents.Any(e => e.kind == HouseEventKind.Ambient && e.week == s.week)) return;

            // Every draw happens AFTER the guards. Passing the room in as an argument spent a roll
            // on choosing one even in the weeks that narrate nothing — and a draw spent is a season
            // re-rolled, whether or not anything was done with it.
            // Past the story boundary the draws are keyed, so a story beat taking a week's airtime
            // can never change how many main-stream rolls the house narration spends.
            bool keyed = StoryOn(s);
            var line = keyed
                ? HouseEventSources.Ambient(s, StoryRandom.Unit(s, "w" + s.week + ":ambient:pick"),
                    StoryRandom.Unit(s, "w" + s.week + ":ambient:which"),
                    HouseRooms.Any(s, StoryRandom.Unit(s, "w" + s.week + ":ambient:room")), s.nextSequence)
                : HouseEventSources.Ambient(s, Roll(s), Roll(s), HouseRooms.Any(s, Roll(s)), s.nextSequence);
            if (line == null) return;
            s.houseEvents.Add(line);
            Log(s, "house-ambient", line.narrative, s.playerId);
        }

        /// <summary>
        /// Finishes the story a chapter belonged to, and banks what the choice left behind.
        ///
        /// <para>Only a chapter has a story; an ordinary situation answers to nobody and this does
        /// nothing for it. The modifier is looked up from the template rather than stored on the
        /// choice, because a modifier is the template's business and storing it on every choice
        /// would put the same three objects in every save that ever saw one.</para>
        /// </summary>
        private static void CloseStoryline(EpisodeState s, HouseEventState chapter, int index)
        {
            var story = Storylines.For(s, chapter.id);
            if (story == null) return;

            story.status = StorylineStatus.Completed;
            story.endedWeek = s.week;

            var template = Storylines.Find(story.templateId);
            var chosen = template != null && index >= 0 && index < template.options.Length
                ? template.options[index].modifier : null;
            if (chosen == null) return;

            // One of each at a time. Taking the same choice twice in a season should not stack a
            // bonus on itself; it should reset the clock on the one already running.
            s.activeModifiers.RemoveAll(m => m.id == chosen.id);
            s.activeModifiers.Add(new StoryModifierState
            {
                id = chosen.id, name = chosen.name, description = chosen.description,
                weeksLeft = chosen.weeks,
                competitionBonus = chosen.competition, socialBonus = chosen.social,
            });
            Log(s, "storyline-outcome", story.title + " — " + chosen.name + ": " + chosen.description,
                s.playerId);
        }

        /// <summary>
        /// Walking in on two houseguests.
        ///
        /// <para>The only event source the engine cannot originate, because where everybody is
        /// standing lives in the house rather than in the save. The director offers it; the engine
        /// still decides whether it is allowed, so a malformed or out-of-turn offer changes nothing.
        /// </para>
        /// </summary>
        private static void WitnessProximity(EpisodeState s, EpisodeCommand c)
        {
            if (StoryOn(s)) { StoryProximity(s, c); return; }
            Require(HouseEvents.Ready(s), "Nothing more is going to happen this week.");
            Require(HouseEvents.Pending(s) == null, "Deal with what is already in front of you first.");
            var drawn = HouseEventSources.Proximity(s, c.targetId, c.secondTargetId, c.text, s.nextSequence);
            Require(drawn != null, "There is nobody there to walk in on.");
            s.houseEvents.Add(drawn);
            Log(s, "house-event", drawn.title + ". " + drawn.narrative, s.playerId);
        }

        /// <summary>
        /// The player answering a situation.
        ///
        /// <para>Every consequence was resolved to a person when the event was drawn, so answering
        /// three weeks later moves the people it was always about rather than re-aiming at whoever
        /// the player has since fallen out with.</para>
        ///
        /// <para><c>trustChange</c> is written to the ledger rather than to a counter of its own.
        /// Trust in this port is <see cref="ThreatAssessment.TrustScore"/>, which reads weighted
        /// ledger impact — so a choice that costs the player trust has to leave a mark the ledger
        /// can see, or it costs nothing at all.</para>
        /// </summary>
        private static void ResolveHouseEvent(EpisodeState s, EpisodeCommand c)
        {
            Require(s.Find(s.playerId).status == ContestantStatus.Active,
                "Evicted players can follow the season but cannot influence it.");
            var item = s.houseEvents.FirstOrDefault(e => e.id == c.targetId && !e.resolved);
            Require(item != null, "That situation has already passed.");
            // A story beat is answered by its option id through ProgressStoryline, never by a label.
            Require(!item.IsStory, "Answer that moment with one of its own options.");

            // The choice travels as its own label rather than as an index, so a screen and an engine
            // that disagree about the order cannot silently commit the wrong answer — which is the
            // failure a stored list of options exists to make impossible.
            int index = item.choices.FindIndex(x => string.Equals(x.label, (c.text ?? string.Empty).Trim(),
                StringComparison.OrdinalIgnoreCase));
            Require(index >= 0, "Choose one of this situation's recorded options.");

            var choice = item.choices[index];
            foreach (var impact in choice.impacts)
            {
                var target = s.Find(impact.targetId);
                if (target == null || target.status != ContestantStatus.Active) continue;
                Change(s, s.playerId, target.id, impact.amount, choice.label, "house_event");
            }

            if (Math.Abs(choice.trustChange) > 0.001)
                foreach (string id in item.involvedIds)
                {
                    var witness = s.Find(id);
                    if (witness == null || witness.status != ContestantStatus.Active) continue;
                    RelationshipLedger.Record(s, witness.id, s.playerId,
                        choice.trustChange > 0 ? "house_event_trust" : "house_event_distrust",
                        choice.trustChange, choice.label);
                }

            item.resolved = true;
            item.chosenIndex = index;
            item.outcome = HouseEvents.Outcome(s, item, index);
            CloseStoryline(s, item, index);
            Remember(s, s.playerId, item.involvedIds.FirstOrDefault() ?? s.playerId,
                item.title + ": " + choice.label, true);
            Log(s, "house-event-outcome", item.outcome, s.playerId);
        }

        // ---------------------------------------------------------------- the social vocabulary
        //
        // Ported from player-action-reducer.ts. Every number is WebSocialVocabulary's, which is
        // where they can be read and tested; every draw goes through Roll(s), because these are
        // committed commands whose rolls are recorded and have to replay identically.

        /// <summary>The two ways of spreading a rumour, and the two ways of calling a meeting.</summary>
        public const string PublicCallout = "public-callout", WhisperCampaign = "whisper-campaign";
        public const string RallyTroops = "rally-troops", AirDirtyLaundry = "air-dirty-laundry";

        /// <summary>One conversation: a warmth change, a memory, and a line in the log.</summary>
        private static void Converse(EpisodeState s, ContestantState target, double delta, string line)
        {
            Change(s, s.playerId, target.id, delta);
            Remember(s, target.id, s.playerId, line, true);
            Log(s, "conversation", line, s.playerId, target.id);
        }

        /// <summary>
        /// Talking game, which is the first conversation that can go wrong.
        ///
        /// <para>Two draws, in the source's order: whether it lands, then how well. Drawing once and
        /// reusing the number would tie "did it work" to "how well it worked", which is a different
        /// game from the one being ported.</para>
        /// </summary>
        private static void DiscussGame(EpisodeState s, ContestantState target)
        {
            double gate = Roll(s);
            double delta = WebSocialVocabulary.DiscussGame(gate, Roll(s));
            bool landed = delta > 0;
            string line = landed
                ? "You talked game with " + target.name + " and they were with you."
                : target.name + " decided you were scheming.";
            Change(s, s.playerId, target.id, delta);
            Remember(s, target.id, s.playerId, line, true);
            Log(s, landed ? "conversation" : "conversation-backfire", line, s.playerId, target.id);
        }

        /// <summary>
        /// The most a single conversation can move, either way. Ten to eighteen when they keep it,
        /// minus fifteen when they decide to use it.
        /// </summary>
        private static void ShareSecret(EpisodeState s, ContestantState target)
        {
            double gate = Roll(s);
            double delta = WebSocialVocabulary.ShareSecret(gate, Roll(s));
            bool kept = delta > 0;
            string line = kept
                ? "You told " + target.name + " something you should not have, and they kept it."
                : target.name + " intends to use what you told them.";
            Change(s, s.playerId, target.id, delta);
            Remember(s, target.id, s.playerId, line, true);
            // The player's own record of having said it, which is the half they can act on later.
            Remember(s, s.playerId, target.id, kept
                ? "I trusted " + target.name + " with something."
                : "I told " + target.name + " too much.", true);
            Log(s, kept ? "conversation" : "conversation-backfire", line, s.playerId, target.id);
        }

        /// <summary>
        /// A rumour about somebody, told either quietly or in front of everyone.
        ///
        /// <para>The reference is handed a success flag by its contextual-action generator; there is
        /// no such generator here, so the engine rolls for it. The odds and every consequence are
        /// the source's.</para>
        ///
        /// <para>A whisper reaches one person and poisons what they think of the subject. A call-out
        /// reaches two or three and costs more with each. Both cost the player badly with the
        /// subject when it gets back to them, and a call-out costs twice what a whisper does — which
        /// is the trade: reach in exchange for exposure.</para>
        /// </summary>
        private static void SpreadRumor(EpisodeState s, ContestantState target, EpisodeCommand c)
        {
            bool loudly = string.Equals((c.text ?? string.Empty).Trim(), PublicCallout,
                StringComparison.OrdinalIgnoreCase);
            var audience = s.Active
                .Where(x => x.id != s.playerId && x.id != target.id)
                .OrderBy(x => x.id, StringComparer.Ordinal).ToList();
            Require(audience.Count > 0, "There is nobody left to tell.");
            // Under the commitment rules (R0, X7) a whisper reaches the person the player is talking
            // to, whom the command names; before them the house drew a listener, after the floor's roll.
            var listener = !loudly && CommitmentRulesOn(s) ? audience.FirstOrDefault(x => x.id == c.secondTargetId) : null;
            Require(loudly || listener != null || !CommitmentRulesOn(s), "Choose who to whisper it to.");

            if (Roll(s) <= WebSocialVocabulary.RumourFloor)
            {
                double cost = loudly ? WebSocialVocabulary.CalloutBackfire : WebSocialVocabulary.WhisperBackfire;
                Change(s, s.playerId, target.id, cost);
                Remember(s, target.id, s.playerId, "Spread a rumour about me.", true);
                Log(s, "rumour-backfire", "Your rumour about " + target.name + " got back to them.",
                    s.playerId, target.id);
                return;
            }

            if (!loudly)
            {
                var heard = listener ?? audience[(int)(Roll(s) * audience.Count) % audience.Count];
                double damage = WebSocialVocabulary.WhisperDamage(Roll(s));
                // Between THEM, not between the player and either: a whisper poisons a relationship
                // the player is not part of, which is the whole point of whispering it.
                RelationshipLedger.Move(s, heard.id, target.id, damage);
                RelationshipLedger.Record(s, heard.id, target.id, "heard_a_rumour", damage,
                    "Heard something about " + target.name + ".");
                Log(s, "rumour", "You whispered about " + target.name + " to " + heard.name + ".",
                    s.playerId, heard.id);
                return;
            }

            // Under the levers a call-out reaches whoever happens to be there, not the first two
            // or three houseguests by id every time.
            if (LeverRulesOn(s)) Shuffle(audience, () => Roll(s));
            int reach = Math.Min(audience.Count, WebSocialVocabulary.CalloutAudience(Roll(s)));
            for (int i = 0; i < reach; i++)
            {
                double damage = WebSocialVocabulary.CalloutDamage(Roll(s));
                RelationshipLedger.Move(s, audience[i].id, target.id, damage);
                RelationshipLedger.Record(s, audience[i].id, target.id, "heard_a_rumour", damage,
                    "Watched " + Name(s, s.playerId) + " call " + target.name + " out.");
            }
            Log(s, "rumour", "You called " + target.name + " out in front of " + reach
                + (reach == 1 ? " housemate." : " housemates."), s.playerId, target.id);
        }

        /// <summary>
        /// Addressing the whole house at once.
        ///
        /// <para>The one action that touches every relationship in a single command. Rallying is
        /// mostly positive with one sceptic; airing everything has no middle — each houseguest
        /// either agrees with you or does not, which is what makes it the approach that can end a
        /// game in an afternoon. A meeting that fails costs a little with everybody.</para>
        /// </summary>
        private static void HouseMeeting(EpisodeState s, EpisodeCommand c)
        {
            Require(s.phase == EpisodePhase.Social || s.phase == EpisodePhase.Campaign,
                "A house meeting belongs to free time or campaigning.");
            Require(s.Find(s.playerId).status == ContestantStatus.Active,
                "Evicted players can follow the season but cannot influence it.");
            Require(SocialActionsSpent(s) < SocialActionBudget(s),
                "This social window is complete. Continue the episode.");

            var house = s.Active.Where(x => x.id != s.playerId)
                .OrderBy(x => x.id, StringComparer.Ordinal).ToList();
            Require(house.Count > 0, "There is nobody to call together.");

            bool airing = string.Equals((c.text ?? string.Empty).Trim(), AirDirtyLaundry,
                StringComparison.OrdinalIgnoreCase);
            bool worked = Roll(s) > WebSocialVocabulary.MeetingFloor;
            int sceptic = worked && !airing ? (int)(Roll(s) * house.Count) % house.Count : -1;

            for (int i = 0; i < house.Count; i++)
            {
                double delta = !worked ? WebSocialVocabulary.MeetingFailure(Roll(s))
                    : airing ? WebSocialVocabulary.MeetingAiring(Roll(s))
                    : i == sceptic ? WebSocialVocabulary.MeetingSceptic
                    : WebSocialVocabulary.MeetingRally(Roll(s));
                Change(s, s.playerId, house[i].id, delta);
            }

            Log(s, "house-meeting", !worked
                ? "You called a house meeting and it did not land."
                : airing
                    ? "You called a house meeting and put everything on the table."
                    : "You called a house meeting and rallied the room.");
            SpendSocialAction(s);
        }

        /// <summary>
        /// Buying another turn, paid for in goodwill.
        ///
        /// <para>The budget has been a hard ceiling with no way past it, which makes a week where
        /// the house moves faster than the allowance unplayable rather than expensive. Two ways to
        /// pay, both the source's: burn one bridge badly, or make the whole house slightly colder.
        /// </para>
        ///
        /// <para>Buying is not itself a social action — charging one to earn one would be a control
        /// that does nothing — so it sits outside <see cref="Social"/> and takes no budget check
        /// beyond having somewhere to put the cost.</para>
        /// </summary>
        private static void BuyActionPoint(EpisodeState s, EpisodeCommand c)
        {
            Require(s.phase == EpisodePhase.Social || s.phase == EpisodePhase.Campaign,
                "Actions are only worth buying during free time or campaigning.");
            Require(s.Find(s.playerId).status == ContestantStatus.Active,
                "Evicted players can follow the season but cannot influence it.");
            Require(s.boughtActionPoints < WebSocialVocabulary.PurchaseCeiling,
                "You have already bought as much time as the house will give you.");

            string cost = (c.text ?? string.Empty).Trim();
            Require(WebSocialVocabulary.IsKnownCost(cost), "Choose how to pay for the extra action.");

            var house = s.Active.Where(x => x.id != s.playerId)
                .OrderBy(x => x.id, StringComparer.Ordinal).ToList();
            Require(house.Count > 0, "There is nobody left to spend goodwill with.");

            if (cost == WebSocialVocabulary.BurnOne)
            {
                // Whoever the player named, or somebody the house picks. The reference draws at
                // random and lets a caller pre-pick; both routes exist here for the same reason.
                var burned = house.FirstOrDefault(x => x.id == c.targetId)
                             ?? house[(int)(Roll(s) * house.Count) % house.Count];
                Change(s, s.playerId, burned.id, WebSocialVocabulary.BurnOneCost);
                Log(s, "bought-action", "You bought yourself more time, and it cost you with "
                    + burned.name + ".", s.playerId, burned.id);
            }
            else
            {
                foreach (var guest in house)
                    Change(s, s.playerId, guest.id, WebSocialVocabulary.SpreadAllCost);
                Log(s, "bought-action", "You bought yourself more time, and the whole house felt it.");
            }

            s.boughtActionPoints = checked(s.boughtActionPoints + 1);
        }

        // ---------------------------------------------------------------- the deal table
        //
        // NpcDeals is the house bargaining among itself and leaves the player out on purpose; these
        // two are the player's side of the same table. Both draw from the season's generator, which
        // is legitimate here and nowhere else in the deal code: they run only inside a committed
        // command, so the draw is recorded and replays identically.

        /// <summary>
        /// The player puts something to a houseguest, and hears the answer straight away.
        ///
        /// <para>The reference queues a proposal and resolves it later; resolving it here instead is
        /// deliberate. A pending proposal from the player would be a second store the save format
        /// has no room for, and the reference's own resolution is a single roll against a number
        /// computed entirely from present state — so asking and answering in one command produces
        /// the same distribution with one fewer thing to persist.</para>
        /// </summary>
        private static void ProposeDeal(EpisodeState s, ContestantState target, EpisodeCommand c)
        {
            string type = (c.text ?? string.Empty).Trim();
            string about = c.secondTargetId;
            // The attempt's own sequence, before anything is minted: a refusal's counter is drawn on a coin
            // keyed to it (C7), so every attempt has one of its own.
            int attempt = s.nextSequence;
            Require(PlayerDeals.CanPropose(s, target.id, type, about, out string refusal), refusal);

            double chance = PlayerDeals.AcceptanceChance(s, target.id, type, about);
            // An alliance invitation is the one way to a pact (ACTIONS-DEALS-ALLIANCES-PLAN C4): under
            // the commitment rules a grudge of forty or more says no to it whatever the roll, which is
            // drawn all the same, as 'Propose an alliance' draws it.
            bool accepted = Roll(s) * 100 < chance && !(type == DealKind.AllianceInvite && GrudgeRefusesAlliance(s, target.id));
            string title = DealKind.Title(type).ToLowerInvariant();
            string said = PlayerDeals.Reasoning(s, target.id, type, accepted);

            if (accepted)
            {
                var read = LeverRead(s, target.id);
                var struck = PlayerDeals.Draft(s, target.id, type, about, "deal-player-" + s.nextSequence);
                s.deals.Add(struck);
                // Under the rules a deal struck is on the record as a chance taken now (C1), as an
                // accepted offer is: one that lapses before any reveal reconciles it was still made.
                if (CommitmentRulesOn(s)) Opportunity(s, struck.id, OpportunityKinds.Deal, struck.week).response = OpportunityResponse.Taken;
                Change(s, s.playerId, target.id, PlayerDeals.AcceptedImpact,
                    "Agreed a " + title + " with you.", "deal_accepted");
                Remember(s, target.id, s.playerId, "Agreed a " + title + " with me.", true);
                Log(s, "deal", target.name + " agreed a " + title + ". “" + said + "”",
                    s.playerId, target.id);
                if (type == DealKind.AllianceInvite) AllyThroughInvitation(s, target.id);
                if (type == DealKind.VoteEvict || type == DealKind.VoteSave) LeverLine(s, target.id, read, WantsOut(s, type, about), "your deal");
                return;
            }

            Change(s, s.playerId, target.id, PlayerDeals.RefusedImpact,
                "Turned down a " + title + " from you.", "deal_refused");
            Log(s, "deal", target.name + " turned down a " + title + ". “" + said + "”",
                s.playerId, target.id);
            // Under the commitment rules (C7) a houseguest who was close to yes may come back with the
            // same deal and a price on it, on a keyed coin: the season's stream is as a plain refusal left it.
            if (CommitmentRulesOn(s)) OfferCounter(s, target, type, about, chance, attempt);
        }

        /// <summary>
        /// The player answers something a houseguest put to them.
        ///
        /// <para>Not a social action, and deliberately: the offer was somebody else's move, and
        /// charging the player an action to decline it would make being popular expensive. The
        /// reference treats a response the same way.</para>
        /// </summary>
        /// <summary>The word a command carries to mean yes. Anything else is a refusal.</summary>
        public const string AcceptDeal = "accept";

        private static void RespondToDeal(EpisodeState s, EpisodeCommand c)
        {
            Require(s.Find(s.playerId).status == ContestantStatus.Active,
                "Evicted players can follow the season but cannot influence it.");
            var deal = s.deals.FirstOrDefault(d => d.id == c.targetId
                                                   && d.status == DealStatus.Proposed
                                                   && d.recipientId == s.playerId);
            Require(deal != null, "That offer is no longer on the table.");
            var from = s.Find(deal.proposerId);
            Require(from != null && from.status == ContestantStatus.Active,
                "The houseguest who offered that is no longer in the house.");

            string title = DealKind.Title(deal.type).ToLowerInvariant();
            if (string.Equals((c.text ?? string.Empty).Trim(), AcceptDeal, StringComparison.OrdinalIgnoreCase))
            {
                // The player's three (ACTIONS-DEALS-ALLIANCES-PLAN C4, decision 10): under the
                // commitment rules a yes that would bring them into a fourth pact is not theirs to give.
                Require(!(deal.type == DealKind.AllianceInvite && InvitationPastPactCap(s, deal.proposerId)), PactCapRefusal);
                deal.status = DealStatus.Active;
                // The offer lapsed at the end of this week; the arrangement it becomes runs for as
                // long as its own kind runs for, which for a final two or a partnership is no limit.
                deal.expiresWeek = PlayerDeals.Draft(s, deal.proposerId, deal.type, deal.targetId, deal.id).expiresWeek;
                // Under the commitment rules (C1, decision 15) a yes is a commitment, not free
                // warmth: +4 rather than +12, and the offer weighs one step heavier if it breaks
                // (DealResolution.BreachWeight). The same two draws either way.
                Change(s, s.playerId, deal.proposerId, CommitmentRulesOn(s) ? PlayerDeals.CommittedAcceptedImpact : PlayerDeals.AcceptedImpact,
                    "Took me up on a " + title + ".", "deal_accepted");
                // Under the rules the yes is on the record as a chance taken now (C1): a deal that then
                // lapses or ends before any reveal reconciles it is not an offer left on the table.
                if (CommitmentRulesOn(s)) Opportunity(s, deal.id, OpportunityKinds.Deal, deal.week).response = OpportunityResponse.Taken;
                Remember(s, s.playerId, deal.proposerId, "I accepted a " + title + " from " + from.name + ".", true);
                Log(s, "deal", "You accepted a " + title + " from " + from.name + ".", s.playerId, deal.proposerId);
                if (deal.type == DealKind.AllianceInvite) AllyThroughInvitation(s, deal.proposerId);
                // Under the commitment rules (C7) a nominee's veto ask carries a price, struck with the yes.
                if (CommitmentRulesOn(s)) StrikeTheAskPrice(s, deal);
                return;
            }

            deal.status = DealStatus.Declined;
            Change(s, s.playerId, deal.proposerId, PlayerDeals.DeclineImpact,
                "Turned down my " + title + ".", "deal_declined");
            Log(s, "deal", "You declined a " + title + " from " + from.name + ".", s.playerId, deal.proposerId);
        }

        /// <summary>
        /// Writes the verdicts an action reached on the deals it touched.
        ///
        /// <para>Direction matters and is not the reference's. <c>applyDealOutcome</c> moves the
        /// <i>proposer's</i> view of the recipient whichever of them acted, which reads as the person
        /// who broke their word thinking less of the person they wronged. This port writes it the way
        /// <see cref="SettlePromise"/> already does: the wronged party's view of whoever acted. A
        /// voting block has no single actor, so that one moves both ways.</para>
        ///
        /// <para>The betrayal spread draws from the season's generator, which is legitimate because
        /// this only ever runs inside a committed command — the same line the promise witnesses sit
        /// on, and the same reason.</para>
        /// </summary>
        private static void SettleDeals(EpisodeState s, List<DealResolution.Verdict> verdicts)
        {
            // Under the commitment rules (C0) a deal says who broke it and when, and its breach is held
            // by the one wronged and never fades (X11). Before them the record is both ways and fades,
            // as it always was, so a season played without them settles exactly as it did.
            bool rules = CommitmentRulesOn(s);
            foreach (var verdict in verdicts)
            {
                // A partnership kept at a vote stands (C1): nothing to settle, a small record of the keep.
                if (verdict.stands) { KeptAndStanding(s, verdict); continue; }
                var deal = verdict.deal;
                deal.status = verdict.status;
                // Under the commitment rules (C1) an offer the player accepted weighs one step
                // heavier when it breaks; every other deal, and every deal before the rules, weighs
                // its own trust (DealResolution.BreachWeight).
                double delta = DealResolution.Impact(s, deal, verdict.status);
                bool kept = verdict.status == DealStatus.Fulfilled;
                string title = DealKind.Title(deal.type).ToLowerInvariant();
                if (rules)
                {
                    deal.settledWeek = s.week;
                    deal.brokenById = kept ? null : verdict.actorId;
                }

                // A vote deal's outcome is a ballot (UI-UX-PASS-PLAN decision 4): the settlement is
                // the same, but the line goes only to whoever can know it - the party whose own
                // ballot decided it, and never the player as the other party, who is told once they
                // know the ballot (KnownBallots.DealOutcomeKnown). So is a partnership's under the
                // commitment rules (C1), which only the vote settles. Every other deal's line goes to
                // the pair, as it always did.
                bool ballot = KnownBallots.SettledByABallot(deal);
                // And under the rules a settlement a ballot decided never moves the player's own view
                // of the other party: their trust and standing word are what they can read, and a jump
                // the size of a deal's weight would tell them the ballot every line keeps to itself.
                // The record and the memory still say it, and their readers wait for the ballot.
                bool keepPlayersView = rules && ballot;
                if (verdict.actorId == null)
                {
                    string text = Name(s, deal.proposerId) + " and " + Name(s, deal.recipientId)
                        + (kept ? " held to their " : " fell out over their ") + title + ".";
                    if (!(keepPlayersView && deal.proposerId == s.playerId)) WriteScore(s, deal.proposerId, deal.recipientId, delta);
                    if (!(keepPlayersView && deal.recipientId == s.playerId)) WriteScore(s, deal.recipientId, deal.proposerId, delta);
                    // Both walked away from it, so both hold it: permanently, under the rules.
                    RelationshipLedger.Record(s, deal.proposerId, deal.recipientId,
                        kept ? "deal_fulfilled" : "deal_broken", delta, text, permanent: rules && !kept);
                    var pair = new[] { deal.proposerId, deal.recipientId };
                    Log(s, "deal-outcome", text, ballot ? pair.Where(id => id != s.playerId).ToArray() : pair);
                }
                else
                {
                    string wronged = DealResolution.Partner(deal, verdict.actorId);
                    string text = Name(s, verdict.actorId) + (kept ? " honoured a " : " broke a ")
                        + title + " with " + Name(s, wronged) + ".";
                    if (!(keepPlayersView && wronged == s.playerId)) WriteScore(s, wronged, verdict.actorId, delta);
                    if (rules && !kept) RecordBreach(s, wronged, verdict.actorId, delta, text);
                    else RelationshipLedger.Record(s, wronged, verdict.actorId,
                        kept ? "deal_fulfilled" : "deal_broken", delta, text);
                    Remember(s, wronged, verdict.actorId, text, true);
                    Log(s, "deal-outcome", text, ballot ? new[] { verdict.actorId } : new[] { verdict.actorId, wronged });
                    // A breach the player suffered by somebody else's ballot raises no reckoning for them
                    // under the commitment rules (C1): the story's "You Broke Your Word" would tell them a
                    // ballot they may not know (decision 4). The memory and the line wait for the ballot.
                    bool theirBallotAgainstYou = keepPlayersView && wronged == s.playerId;
                    if (!kept && !theirBallotAgainstYou) StoryWordBroken(s, wronged, verdict.actorId, GrudgeCauses.DealBroken, 60);
                }

                if (!kept) SpreadBetrayal(s, deal, verdict.actorId);
                // Under the commitment rules (C7) a price is void once the one it was owed to breaks what it
                // bought: last, so nothing above draws or mints any differently for it.
                if (rules && !kept) VoidThePrice(s, deal, verdict.actorId);
            }
        }

        /// <summary>
        /// Word getting around that somebody broke their word.
        ///
        /// <para>The reference's <c>spreadBetrayalInfo</c>: each other houseguest has a two-in-five
        /// chance of hearing, and hearing costs the betrayer between five and fifteen points of
        /// their standing with that person. Where nobody in particular acted — a voting block both
        /// sides walked away from — there is no betrayer to gossip about, so nothing spreads.</para>
        /// </summary>
        private static void SpreadBetrayal(EpisodeState s, DealState deal, string betrayerId)
        {
            if (betrayerId == null) return;
            string wronged = DealResolution.Partner(deal, betrayerId);
            foreach (var witness in s.Active.Where(c => c.id != betrayerId && c.id != wronged).ToList())
            {
                if (Roll(s) >= DealResolution.BetrayalChance) continue;
                double penalty = Math.Floor(DealResolution.BetrayalFloor + Roll(s) * DealResolution.BetrayalSpread);
                WriteScore(s, witness.id, betrayerId, penalty);
                string heard = "Heard that " + Name(s, betrayerId) + " broke a deal with " + Name(s, wronged) + ".";
                // Under the commitment rules (C0) the one who heard holds it against the betrayer, and the
                // betrayer holds nothing against the one who heard it; before them it was written both ways.
                if (CommitmentRulesOn(s)) RelationshipLedger.RecordOneWay(s, witness.id, betrayerId, "heard_about_betrayal", penalty, heard);
                else RelationshipLedger.Record(s, witness.id, betrayerId, "heard_about_betrayal", penalty, heard);
            }
        }

        private static void SettlePromise(EpisodeState s, PromiseState promise, PromiseStatus status)
        {
            promise.status = status;
            // Under the commitment rules (C0) a promise says who broke it - its maker, whose act settles
            // it - and when it was kept or broken.
            bool rules = CommitmentRulesOn(s);
            if (rules)
            {
                promise.settledWeek = s.week;
                promise.brokenById = status == PromiseStatus.Broken ? promise.fromId : null;
            }
            var kind = promise.kind == PromiseKind.FinalTwo ? "final_2" : promise.kind == PromiseKind.AllianceLoyalty ? "alliance_loyalty" : promise.kind.ToString().ToLowerInvariant();
            // The original PromiseSystem calls its optional-loyalty API with the default 5
            // and records a one-way event, unlike generic reciprocal social changes.
            var delta = WebRules.PromiseImpact(kind, status == PromiseStatus.Broken ? "broken" : "fulfilled");
            WriteScore(s, promise.toId, promise.fromId, delta);
            string text = Name(s, promise.fromId) + (status == PromiseStatus.Broken ? " broke" : " fulfilled") + " a " + promise.kind + " promise.";
            // And the one promised holds the outcome on their record, kept or broken, permanently: the
            // ledger's own rule for a word given (X11). Before the rules nothing recorded it at all.
            if (rules)
                RelationshipLedger.RecordOneWay(s, promise.toId, promise.fromId,
                    status == PromiseStatus.Broken ? "promise-broken" : "promise-kept", delta, text, permanent: true);
            Remember(s, promise.toId, promise.fromId, text, true); Remember(s, promise.fromId, promise.toId, text, true);
            // A vote promise's outcome is the promiser's ballot (UI-UX-PASS-PLAN decision 4): the
            // line goes to them alone, and the promisee is told once they know the ballot
            // (KnownBallots.PromiseOutcomeKnown). Every other promise's line goes to the pair.
            if (promise.kind == PromiseKind.Vote) Log(s, "promise-outcome", text, promise.fromId);
            else Log(s, "promise-outcome", text, promise.fromId, promise.toId);
            if (status == PromiseStatus.Broken) StoryWordBroken(s, promise.toId, promise.fromId, GrudgeCauses.PromiseBroken, 60);
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

        internal static void Change(EpisodeState s, string from, string to, double delta, string note = null, string eventType = null)
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

        internal static double Roll(EpisodeState s)
        {
            var rng = new SeededRandom(s.randomState); var roll = rng.NextDouble(); s.randomState = rng.State; return roll;
        }

        internal static void Remember(EpisodeState s, string owner, string subject, string text, bool privacy)
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
        ///
        /// <para>A houseguest acting on themselves takes their own reflexive - herself, himself or
        /// themselves, from their pronouns - as the player takes "yourself". Only the player's was
        /// known, so a houseguest who held the veto from the block logged "Emma Brown saves Emma
        /// Brown", and the veto meeting's screen, the status line and the week's recap all said it
        /// (PACK8-PASS-PLAN B3). The veto's self-save is the one sentence that reaches here with an
        /// actor acting on themselves; its relationship note is never written, because a houseguest
        /// has no relationship with themselves.</para>
        /// </summary>
        private static string Target(EpisodeState s, string id, string actorId)
            => id == s.playerId ? (id == actorId ? "yourself" : "you")
                : id == actorId ? StoryPeople.Pronouns(s.Find(id)).themselves : Name(s, id);

        /// <summary>The same, for a pronoun that opens a sentence.</summary>
        private static string TargetStart(EpisodeState s, string id, string actorId)
        {
            var value = Target(s, id, actorId);
            return value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value.Substring(1);
        }

        /// <summary>
        /// Tells the player which of their alliances just ended, and nothing more.
        ///
        /// <para><see cref="NpcAlliances.Dissolve"/> ends the player's alliance when the player has
        /// soured on their partner, or when the partner has left the house, and it used to do so
        /// without a word: "Allied" simply went from the web, the conversation header and the cast
        /// strip. The reference build toasts the same moment. The final eviction ends alliances too
        /// (<see cref="NpcAlliances.EndBroken"/>) and tells them here the same way.</para>
        ///
        /// <para>Logged LAST in the step - the one that opens the social week, after the house has
        /// narrated itself, or the final eviction, after the first jury question - for two reasons. Every id minted in the step - the pacts, promises, deals and
        /// house events the settle writes - is what it was before this existed. And the director's
        /// status line is the last event the player may see, so this is what they read. It draws
        /// no roll and saves no new field.</para>
        ///
        /// <para>It carries no number and no direction. A partner leaving the house is public; an
        /// alliance that sours has fallen apart, in the same words however it soured. Nobody is
        /// told anything once the player is out of the house themselves.</para>
        /// </summary>
        private static void TellThePlayerWhichAlliancesEnded(EpisodeState s, List<AllianceState> theirs)
        {
            if (s.Find(s.playerId)?.status != ContestantStatus.Active) return;
            foreach (var alliance in theirs.Where(a => !a.active))
            {
                var partners = alliance.members.Where(id => id != s.playerId)
                    .Select(id => s.Find(id)).Where(actor => actor != null).ToList();
                if (partners.Count == 0) continue;
                string names = string.Join(" and ", partners.Select(actor => actor.name));
                bool gone = partners.All(actor => actor.status != ContestantStatus.Active);
                string text = gone
                    ? names + (partners.Count == 1 ? " has" : " have") + " left the house, and your alliance has ended."
                    : "Your alliance with " + names + " has fallen apart.";
                Log(s, "alliance", text, new[] { s.playerId }.Concat(partners.Select(actor => actor.id)).ToArray());
            }
        }

        private static void Phase(EpisodeState s, EpisodePhase phase) { s.phase = phase; Log(s, "phase", "Week " + s.week + " · " + phase); }
        internal static void Log(EpisodeState s, string kind, string text, params string[] audience)
        {
            s.events.Add(new EpisodeEvent { sequence = s.nextSequence++, week = s.week, phase = s.phase, kind = kind, text = text, audienceIds = audience.ToList() });
            if (s.events.Count > 256) s.events.RemoveAt(0);
        }
        private static void Require(bool condition, string message) { if (!condition) throw new RuleException(message); }
        private sealed class RuleException : Exception { public RuleException(string message) : base(message) { } }
    }
}
