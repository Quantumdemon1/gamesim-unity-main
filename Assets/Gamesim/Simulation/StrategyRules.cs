using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The strategy windows: the week's two decisions, opened to the player before they are made.
    ///
    /// <para><b>A word before the decision.</b> Before nominations the player can go to the Head of
    /// Household, and before the veto meeting to the veto holder - and to the Head of Household again,
    /// about who goes up in a saved nominee's place. They used to be told the ceremony came first and
    /// sent away, so the two decisions that shape a week were the two nobody could talk to. A word is
    /// a social action from the week's budget: a conversation, a deal, or a plea.</para>
    ///
    /// <para><b>A plea</b> (<see cref="LobbyAsk"/>) is the reference's veto lobbying
    /// (<c>veto-lobbying-system.ts</c>), with its four approaches, its numbers and its four answers:
    /// a receptive houseguest is moved 40 to 60 points, an open one 15, a skeptical one not at all,
    /// and a hostile one 30 to 50 points the wrong way. The reference lobbies only the veto holder
    /// and only to use it; here the same conversation can ask the Head of Household to leave the
    /// player off the block or put somebody else on it, and the veto holder to keep the nominations
    /// as they are. A plea made with a deal writes one: a safety agreement that binds the player next
    /// week, which is what the reference's card says it does.</para>
    ///
    /// <para><b>Decisions that listen.</b> A houseguest deciding a nomination, a veto or a replacement
    /// now weighs the deals they are party to (the reference server's deal weights, read as points of
    /// warmth), their alliances and this week's pleas, as well as how they feel about each houseguest.
    /// They used to read feelings alone and have the deals judged afterwards, so a houseguest who had
    /// given their word nominated the person they gave it to, and was then blamed for it.</para>
    ///
    /// <para>Nothing here draws on the season's generator except a plea's own rolls, which are a
    /// committed command's. A season plays the windows from <see cref="EpisodeState.strategyRulesStartWeek"/>;
    /// 0 is a season that never does.</para>
    /// </summary>
    public static class StrategyRules
    {
        /// <summary>Whether this season plays the strategy windows in the week it is in.</summary>
        public static bool Apply(EpisodeState s) => s != null && s.strategyRulesStartWeek > 0 && s.week >= s.strategyRulesStartWeek;

        // ---------------------------------------------------------------- the numbers

        /// <summary>The most a plea can move a decision, either way. The reference's clamp.</summary>
        public const double MostInfluence = 100;

        /// <summary>How much less willing a houseguest is to nominate an ally.</summary>
        public const double AllyShield = 30;

        /// <summary>What a deal the Head of Household has broken with somebody does to their reluctance: the reference server's weight.</summary>
        public const double BrokenDealWeight = -35;

        /// <summary>How much more willing a Head of Household is to nominate the houseguest a target agreement of theirs names.</summary>
        public const double TargetPull = 40;

        /// <summary>How much more willing a veto holder is to save a nominee they gave a veto commitment to: the reference server's weight.</summary>
        public const double VetoCommitmentPull = 40;

        /// <summary>How much more willing a veto holder is to save an ally.</summary>
        public const double VetoAllyPull = 20;

        /// <summary>How warmly a veto holder must read a nominee to save them: the port's line, and the reference's.</summary>
        public const double VetoSaveLine = 30;

        /// <summary>
        /// Below this, a member of an alliance will not have the player in it: the reference's
        /// hostility check (<c>AllianceManager.tsx</c>).
        /// </summary>
        public const double HostilityLine = -10;

        /// <summary>
        /// What a deal between the Head of Household and a houseguest does to their reluctance to
        /// nominate them: the reference server's deal weights (<c>npc-nomination/index.ts</c>).
        /// </summary>
        public static double DealWeight(string type)
        {
            switch (type)
            {
                case DealKind.SafetyAgreement: return 35;
                case DealKind.TargetAgreement: return 15;
                case DealKind.VetoUse: return 40;
                case DealKind.FinalTwo: return 50;
                case DealKind.VoteTogether: return 25;
                case DealKind.Partnership: return 20;
                case DealKind.AllianceInvite: return 25;
                case DealKind.InformationSharing: return 10;
                // The port's own kind (C9): a safety pact's weight, since it binds the same act until the final three.
                case DealKind.FinalThree: return 35;
                default: return 0;
            }
        }

        // ---------------------------------------------------------------- the windows

        /// <summary>
        /// Who the player can have a word with outside free time, if anybody: the Head of Household
        /// before nominations; the veto holder before the meeting, while the veto is theirs to use;
        /// and the Head of Household then too, while a saved nominee would need replacing.
        /// </summary>
        public static List<string> Deciders(EpisodeState s)
        {
            var result = new List<string>();
            if (!Apply(s) || s.Find(s.playerId)?.status != ContestantStatus.Active) return result;
            if (s.phase == EpisodePhase.Nomination && s.nominees.Count == 0 && Npc(s, s.hohId)) result.Add(s.hohId);
            if (s.phase == EpisodePhase.VetoMeeting && !s.vetoResolved && VetoCanBeUsed(s))
            {
                if (Npc(s, s.vetoHolderId) && !s.nominees.Contains(s.vetoHolderId)) result.Add(s.vetoHolderId);
                if (Npc(s, s.hohId) && !result.Contains(s.hohId)) result.Add(s.hohId);
            }
            return result;
        }

        /// <summary>Whether a window is open: one of the week's decisions is waiting on a houseguest the player can reach.</summary>
        public static bool WindowOpen(EpisodeState s) => Deciders(s).Count > 0;

        /// <summary>Whether this houseguest is one the player can have a word with right now, outside free time.</summary>
        public static bool IsDecider(EpisodeState s, string id) => id != null && Deciders(s).Contains(id);

        /// <summary>
        /// The conversations a window allows: everything said <i>to</i> a houseguest. Listening in,
        /// rumours told to the house and scheming against somebody are not a word with the person
        /// deciding, and a vote promise waits for campaigning, as it always has.
        /// </summary>
        public static bool IsWindowConversation(EpisodeCommandKind kind)
        {
            switch (kind)
            {
                case EpisodeCommandKind.Eavesdrop:
                case EpisodeCommandKind.SpreadRumor:
                case EpisodeCommandKind.SchemeAgainst:
                case EpisodeCommandKind.PromiseVote:
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>Why the player cannot do this with this houseguest right now, in a window. Null when they can.</summary>
        public static string WindowRefusal(EpisodeState s, string targetId, EpisodeCommandKind kind)
        {
            if (!IsWindowConversation(kind)) return "That can wait for free time. Right now there is a decision to be made.";
            if (IsDecider(s, targetId)) return null;
            return s.phase == EpisodePhase.Nomination
                ? "Before nominations only the Head of Household has time for a word."
                : "Before the veto meeting only the veto holder and the Head of Household have time for a word.";
        }

        private static bool Npc(EpisodeState s, string id)
        {
            var who = s.Find(id);
            return who != null && !who.isPlayer && who.status == ContestantStatus.Active;
        }

        /// <summary>Whether the veto can be used at this meeting at all: not locked at four, and somebody left to name.</summary>
        public static bool VetoCanBeUsed(EpisodeState s) =>
            !string.IsNullOrEmpty(s.vetoHolderId) && !EpisodeEngine.VetoIsLockedAtFinalFour(s)
            && EpisodeEngine.ReplacementCandidates(s).Any();

        // ---------------------------------------------------------------- the pleas

        /// <summary>
        /// Whether this houseguest can be asked for their vote: the levers on, the campaign, the
        /// player on the block, and them voting. A plea from the block, one per voter, that the
        /// voter's ballot answers (<see cref="EpisodeEngine.Pleas"/>).
        /// </summary>
        public static bool CanBeAskedForTheirVote(EpisodeState s, string voterId) =>
            s != null && EpisodeEngine.LeverRulesOn(s) && Apply(s) && s.phase == EpisodePhase.Campaign && !s.evictionResolved
            && s.nominees.Contains(s.playerId) && s.Find(s.playerId)?.status == ContestantStatus.Active
            && Npc(s, voterId) && EpisodeEngine.Voters(s).Any(v => v.id == voterId);

        /// <summary>Whether the player has already had their plea with this houseguest in this phase: one each.</summary>
        public static bool AlreadyAsked(EpisodeState s, string deciderId) =>
            s.lobbies.Any(l => l.week == s.week && l.phase == s.phase && l.deciderId == deciderId);

        /// <summary>The pleas the player could make to this houseguest right now, each with who it is about.</summary>
        public static List<(string ask, string subjectId)> Pleas(EpisodeState s, string deciderId)
        {
            var result = new List<(string, string)>();
            if (s == null) return result;
            foreach (string ask in LobbyAsk.All)
                foreach (string subject in s.contestants.Select(c => c.id).Append(null))
                    if (CanLobby(s, deciderId, ask, subject, out _)) result.Add((ask, subject));
            return result;
        }

        /// <summary>
        /// Whether the player can put this plea to this houseguest right now.
        ///
        /// <para>Separate from whether it would land, for the same reason a deal is: a control that
        /// can only produce a refusal is a trap.</para>
        /// </summary>
        public static bool CanLobby(EpisodeState s, string deciderId, string ask, string subjectId, out string reason)
        {
            reason = null;
            if (!IsDecider(s, deciderId) && !CanBeAskedForTheirVote(s, deciderId))
                return Refuse(out reason, "Nobody is deciding anything you could change right now.");
            if (!LobbyAsk.IsKnown(ask)) return Refuse(out reason, "That is not something you can ask.");
            if (AlreadyAsked(s, deciderId)) return Refuse(out reason, s.Find(deciderId).name + " has already heard you out.");
            var subject = subjectId == null ? null : s.Find(subjectId);
            if (subjectId != null && (subject == null || subject.status != ContestantStatus.Active))
                return Refuse(out reason, "Name somebody still in the house.");
            bool naming = s.phase == EpisodePhase.Nomination;
            switch (ask)
            {
                case LobbyAsk.Spare:
                    if (deciderId != s.hohId) return Refuse(out reason, "Only the Head of Household decides who goes up.");
                    // Under the levers a third person can be kept off the block too: the reluctance
                    // always honoured any subject; only the ask was missing.
                    if (subjectId != s.playerId)
                    {
                        if (!EpisodeEngine.LeverRulesOn(s) || subjectId == null || subjectId == deciderId
                            || !(naming ? EpisodeEngine.NominationCandidates(s) : EpisodeEngine.ReplacementCandidates(s)).Any(c => c.id == subjectId))
                            return Refuse(out reason, "Only the Head of Household decides who goes up.");
                        return true;
                    }
                    if (!naming && !EpisodeEngine.ReplacementCandidates(s).Any(c => c.id == s.playerId))
                        return Refuse(out reason, "You could not be named in anybody's place.");
                    return true;
                case LobbyAsk.Target:
                    if (deciderId != s.hohId) return Refuse(out reason, "Only the Head of Household decides who goes up.");
                    if (subjectId == null || subjectId == s.playerId || subjectId == deciderId
                        || !(naming ? EpisodeEngine.NominationCandidates(s) : EpisodeEngine.ReplacementCandidates(s)).Any(c => c.id == subjectId))
                        return Refuse(out reason, "Name somebody who could go up.");
                    return true;
                case LobbyAsk.Save:
                    if (naming || deciderId != s.vetoHolderId) return Refuse(out reason, "Only the veto holder decides whether to use it.");
                    if (subjectId == null || !s.nominees.Contains(subjectId)) return Refuse(out reason, "Name somebody on the block.");
                    return true;
                case LobbyAsk.Keep:
                    if (naming || deciderId != s.vetoHolderId) return Refuse(out reason, "Only the veto holder decides whether to use it.");
                    if (subjectId != null) return Refuse(out reason, "Keeping the nominations is about nobody in particular.");
                    if (s.nominees.Contains(s.playerId)) return Refuse(out reason, "You are on the block: nobody would believe you wanted it left as it is.");
                    return true;
                case LobbyAsk.Vote:
                    if (!CanBeAskedForTheirVote(s, deciderId)) return Refuse(out reason, "Only a voter can be asked to keep you, and only while you are on the block.");
                    if (subjectId != s.playerId) return Refuse(out reason, "A plea for your vote is for yourself.");
                    return true;
            }
            return Refuse(out reason, "That is not something you can ask.");
        }

        private static bool Refuse(out string reason, string text) { reason = text; return false; }

        /// <summary>
        /// The chance, in percent, that a plea gets a hearing: the reference's lobbying formula.
        ///
        /// <para>The approach's base, fifteen for each of the houseguest's traits it suits and ten off
        /// for each it grates on, a fifth of the relationship, ten more for a player pleading from the
        /// block and ten for a deal already between them; clamped to 5-95. Two changes, both marked:
        /// the relationship is the houseguest's view of the player - the reference reads the player's
        /// own, which the person being persuaded cannot see - and what is asked is weighed as the deal
        /// table weighs the deal it resembles, so pushing a houseguest at a friend is a harder sell.</para>
        /// </summary>
        public static double Chance(EpisodeState s, string deciderId, string ask, string subjectId, string approach)
        {
            var decider = s.Find(deciderId);
            if (decider == null || !LobbyApproach.IsKnown(approach)) return 0;
            double chance = LobbyApproach.Base(approach) + LobbyApproach.TraitFit(decider.traits, approach)
                + JsRound(s.Score(deciderId, s.playerId) / 5);
            if (s.nominees.Contains(s.playerId)) chance += 10;
            if (NpcDeals.Between(s, deciderId, s.playerId).Count > 0) chance += 10;
            switch (ask)
            {
                case LobbyAsk.Vote:
                    // A voter in your alliance is easier; one in the other nominee's, or close to
                    // them, is harder: the vote is between the two of you. Under the commitment rules
                    // only an ally whose own commitment holds (Allegiance.Holds; C2, C3).
                    if (Allegiance.Holds(s, deciderId, s.playerId)) chance += 15;
                    string other = s.nominees.FirstOrDefault(id => id != s.playerId);
                    if (other != null && s.Allied(deciderId, other)) chance -= 20;
                    else if (other != null && s.Score(deciderId, other) > 30) chance -= 15;
                    break;
                case LobbyAsk.Target:
                    if (s.Allied(deciderId, subjectId)) chance -= 40;
                    double toTarget = s.Score(deciderId, subjectId);
                    if (toTarget < -20) chance += 20;
                    else if (toTarget > 30) chance -= 30;
                    break;
                case LobbyAsk.Save:
                    if (subjectId != s.playerId)
                    {
                        double toNominee = s.Score(deciderId, subjectId);
                        if (toNominee >= 50) chance += 20;
                        else if (toNominee < 0) chance -= 20;
                    }
                    break;
                case LobbyAsk.Keep:
                    if (s.nominees.Any(id => s.Allied(deciderId, id))) chance -= 30;
                    break;
            }
            return Math.Max(5, Math.Min(95, chance));
        }

        /// <summary>JavaScript's <c>Math.round</c>: halves go up, where .NET's default rounds them to even.</summary>
        private static double JsRound(double value) => Math.Floor(value + 0.5);

        /// <summary>
        /// How the houseguest takes it: the reference's answer roll. Under six tenths of the chance is
        /// a receptive hearing, 40 to 60 points; the rest of the chance an open one, 15; the next
        /// twenty a skeptical one, −5; anything past that a hostile one, −30 to −50. The second roll
        /// sizes a receptive or hostile answer, and is drawn only for those.
        /// </summary>
        public static (string response, double influence, double impact) Respond(double chance, double roll, Func<double> size)
        {
            double r = roll * 100;
            if (r < chance * 0.6) return (LobbyResponse.Receptive, 40 + 20 * size(), 5);
            if (r < chance) return (LobbyResponse.Open, 15, 3);
            if (r < chance + 20) return (LobbyResponse.Skeptical, -5, 0);
            return (LobbyResponse.Hostile, -(30 + 20 * size()), -8);
        }

        /// <summary>Whether an answer means the plea landed: a deal made with it is only made then.</summary>
        public static bool Landed(string response) => response == LobbyResponse.Receptive || response == LobbyResponse.Open;

        /// <summary>What the plea asked, as the log and the houseguest's memory put it: "to leave you off the block".</summary>
        public static string Describe(EpisodeState s, string ask, string subjectId)
        {
            string about = s.Find(subjectId)?.name ?? "somebody";
            switch (ask)
            {
                case LobbyAsk.Spare:
                    if (subjectId != null && subjectId != s.playerId)
                        return s.phase == EpisodePhase.Nomination ? "to keep " + about + " off the block" : "not to name " + about + " in a saved nominee's place";
                    return s.phase == EpisodePhase.Nomination ? "to leave you off the block" : "not to name you in a saved nominee's place";
                case LobbyAsk.Vote:
                    return "to vote to keep you";
                case LobbyAsk.Target:
                    return s.phase == EpisodePhase.Nomination ? "to nominate " + about : "to name " + about + " as the replacement";
                case LobbyAsk.Save:
                    return subjectId == s.playerId ? "to use the veto on you" : "to use the veto on " + about;
                default:
                    return "to keep the nominations the same";
            }
        }

        /// <summary>What the player says, by approach: the reference's lines where it has them.</summary>
        public static string Pitch(EpisodeState s, string ask, string subjectId, string approach)
        {
            string about = s.Find(subjectId)?.name ?? "them";
            bool self = subjectId == s.playerId;
            switch (ask)
            {
                case LobbyAsk.Vote:
                    return approach == LobbyApproach.Emotional ? "Please. I need your vote this week. I can't go out like this."
                        : approach == LobbyApproach.Strategic ? "Keep me and you keep a shield. Send me out and you're the next name up."
                        : approach == LobbyApproach.Deal ? "Vote to keep me and I owe you: my vote, my word, whatever you need."
                        : "Vote me out and everyone will know exactly whose vote it was.";
                case LobbyAsk.Spare:
                    if (!self)
                        return approach == LobbyApproach.Emotional ? "Please don't put " + about + " up. They're not who you think."
                            : approach == LobbyApproach.Strategic ? "Putting " + about + " up wastes your week. Look somewhere else."
                            : approach == LobbyApproach.Deal ? "Keep " + about + " off the block and you've got me next week."
                            : "Put " + about + " up and you'll answer for it.";
                    return approach == LobbyApproach.Emotional ? "Please don't put me up. I need this week."
                        : approach == LobbyApproach.Strategic ? "Put me up and you lose your best shield. Think about it."
                        : approach == LobbyApproach.Deal ? "Keep me off the block and I'm with you. Safety next week, whatever you need."
                        : "Put me up and the whole house will know exactly who you are.";
                case LobbyAsk.Target:
                    return approach == LobbyApproach.Emotional ? about + " is coming for both of us. Please, put them up."
                        : approach == LobbyApproach.Strategic ? "Nominating " + about + " is the smart game move."
                        : approach == LobbyApproach.Deal ? "Put " + about + " up and I'll protect you next week."
                        : "Everyone's saying it: " + about + " has to go up.";
                case LobbyAsk.Save:
                    if (self)
                        return approach == LobbyApproach.Emotional ? "Please, I'm begging you. Save me. I need this."
                            : approach == LobbyApproach.Strategic ? "If I go home, YOUR biggest shield is gone. Think about it."
                            : approach == LobbyApproach.Deal ? "Save me and I'll owe you everything. Final 2. Safety. Whatever you want."
                            : "If you don't save me, the house will turn on you next. Mark my words.";
                    return approach == LobbyApproach.Emotional ? "You're my last hope. Please save " + about + "."
                        : approach == LobbyApproach.Strategic ? "If you save " + about + ", here's how it helps YOUR game..."
                        : approach == LobbyApproach.Deal ? "I'll protect you next week if you use it now."
                        : "Everyone's watching. The house expects you to act.";
                default:
                    return approach == LobbyApproach.Emotional ? "Please leave it as it is. I'm scared of who goes up if you use it."
                        : approach == LobbyApproach.Strategic ? "Using it only puts a new target on your back. Keep your hands clean."
                        : approach == LobbyApproach.Deal ? "Keep the nominations the same and I've got you next week."
                        : "The house wants it left as it is. Use it and you're on your own.";
            }
        }

        /// <summary>What the houseguest says back, which is how the player learns how it went.</summary>
        public static string Answer(EpisodeState s, string ask, string subjectId, string approach, string response)
        {
            string about = s.Find(subjectId)?.name ?? "them";
            switch (response)
            {
                case LobbyResponse.Receptive:
                    switch (ask)
                    {
                        case LobbyAsk.Spare: return subjectId == s.playerId ? "You're not the one I'm looking at this week." : about + "? Fine. Not this week.";
                        case LobbyAsk.Vote: return "You've got my vote. Don't make me regret it.";
                        case LobbyAsk.Target: return about + "? You might be onto something.";
                        case LobbyAsk.Save: return subjectId == s.playerId ? "Okay. I hear you. I think I know what I'm doing with it."
                            : about + "... yeah. That might be the right call.";
                        default: return "Leaving it alone does sound like the smart play.";
                    }
                case LobbyResponse.Open:
                    return approach == LobbyApproach.Deal ? "Maybe. If you mean that, we might have something."
                        : "I'm listening. Convince me it's worth it.";
                case LobbyResponse.Skeptical:
                    return "I don't know. I have to think about my own game.";
                default:
                    return approach == LobbyApproach.Pressure ? "Are you threatening me? That's a great way to make sure I don't."
                        : "Don't push me. You're making this easy for me, and not the way you want.";
            }
        }

        // ---------------------------------------------------------------- the decisions

        /// <summary>
        /// How unwilling the Head of Household is to put this houseguest up: the lowest go up.
        ///
        /// <para>Warmth, as it always was, then the rest of what they know: an ally and the deals
        /// between them hold them back, a broken deal and a target agreement naming the houseguest push
        /// them on, and so do this week's pleas. A season without the strategy windows reads warmth
        /// alone.</para>
        ///
        /// <para>Under the commitment rules an ally's shield is theirs to give: it holds only while their
        /// own commitment to the player does, and never once they have turned on the pact
        /// (<see cref="Allegiance.Holds"/>; C2, C3). A broken deal pushes the Head of Household on only
        /// when the houseguest they size up broke it (<see cref="Breaches.CountsAgainst(EpisodeState, DealState, string)"/>,
        /// as the reference's own promise term reads a broken promise): their own breach is no grievance
        /// of theirs against the one they wronged. Before the rules either side's counted.</para>
        /// </summary>
        public static double NominationReluctance(EpisodeState s, string hohId, string id)
        {
            double reluctance = s.Score(hohId, id);
            if (!Apply(s)) return reluctance;
            if (Allegiance.Holds(s, hohId, id)) reluctance += AllyShield;
            foreach (var deal in s.deals.Where(d => (d.proposerId == hohId && d.recipientId == id) || (d.proposerId == id && d.recipientId == hohId)))
            {
                if (deal.status == DealStatus.Active)
                {
                    // New-rule safety has one canonical protection term below. Other families
                    // keep their existing weights; legacy seasons retain their original sum.
                    if (!UnifiedCommitments.RulesOn(s) || deal.type != DealKind.SafetyAgreement)
                        reluctance += DealWeight(deal.type);
                }
                else if (deal.status == DealStatus.Broken && Breaches.CountsAgainst(s, deal, id)) reluctance += BrokenDealWeight;
            }
            reluctance -= TargetPull * s.deals.Count(d => d.status == DealStatus.Active && d.type == DealKind.TargetAgreement
                && d.targetId == id && (d.proposerId == hohId || d.recipientId == hohId));
            foreach (var plea in s.lobbies.Where(l => l.week == s.week && l.deciderId == hohId && l.subjectId == id))
            {
                if (plea.ask == LobbyAsk.Spare) reluctance += plea.influence;
                else if (plea.ask == LobbyAsk.Target) reluctance -= plea.influence;
            }
            // Under the commitment rules (C7) a promise of safety the player called in holds its maker to
            // it: a safety deal's weight, times how hard it was held. Nothing in any other season.
            reluctance += UnifiedCommitments.RulesOn(s)
                ? UnifiedCommitments.StrongestProtection(s, hohId, id).Strength
                : Negotiation.SafetyHeld(s, hohId, id);
            if (UnifiedCommitments.RulesOn(s))
                reluctance += BrokenDealWeight * UnifiedCommitmentHistory.Breaches(s)
                    .Count(incident => incident.ActorId == id && incident.WrongedId == hohId);
            return reluctance;
        }

        /// <summary>
        /// How much the veto holder wants to save this nominee: warmth, then an ally, a veto
        /// commitment and this week's pleas for them. The reference's own rule, additively: it uses
        /// the veto when warmth and the lobbying's influence together pass thirty. Under the commitment
        /// rules an ally's pull holds only while their own commitment does (<see cref="Allegiance.Holds"/>).
        /// </summary>
        public static double VetoWillingness(EpisodeState s, string holderId, string nomineeId)
        {
            double willingness = s.Score(holderId, nomineeId);
            if (!Apply(s)) return willingness;
            if (Allegiance.Holds(s, holderId, nomineeId)) willingness += VetoAllyPull;
            if (NpcDeals.Between(s, holderId, nomineeId).Any(d => d.type == DealKind.VetoUse)) willingness += VetoCommitmentPull;
            willingness += s.lobbies.Where(l => l.week == s.week && l.deciderId == holderId && l.ask == LobbyAsk.Save
                && l.subjectId == nomineeId).Sum(l => l.influence);
            return willingness;
        }

        /// <summary>How warmly the veto holder must read a nominee before they use it: raised by a plea to keep things as they are.</summary>
        public static double VetoLine(EpisodeState s, string holderId) =>
            VetoSaveLine + (!Apply(s) ? 0 : s.lobbies.Where(l => l.week == s.week && l.deciderId == holderId
                && l.ask == LobbyAsk.Keep).Sum(l => l.influence));
    }

    /// <summary>What the player can ask of whoever is deciding.</summary>
    public static class LobbyAsk
    {
        /// <summary>To the Head of Household: leave me off the block (or out of a saved nominee's place).</summary>
        public const string Spare = "spare";
        /// <summary>To the Head of Household: put this houseguest up.</summary>
        public const string Target = "target";
        /// <summary>To the veto holder: use it on this nominee.</summary>
        public const string Save = "save";
        /// <summary>To the veto holder: keep the nominations the same.</summary>
        public const string Keep = "keep";
        /// <summary>To a voter, from the block: vote to keep me (STRATEGY-LOOP-PLAN.md §3).</summary>
        public const string Vote = "vote";

        public static readonly string[] All = { Spare, Target, Save, Keep, Vote };
        public static bool IsKnown(string ask) => ask != null && Array.IndexOf(All, ask) >= 0;

        /// <summary>A plea travels in a command's text as "ask/approach".</summary>
        public static string Encode(string ask, string approach) => ask + "/" + approach;

        public static bool TryDecode(string text, out string ask, out string approach)
        {
            ask = approach = null;
            var parts = (text ?? string.Empty).Trim().Split('/');
            if (parts.Length != 2 || !IsKnown(parts[0]) || !LobbyApproach.IsKnown(parts[1])) return false;
            ask = parts[0]; approach = parts[1];
            return true;
        }
    }

    /// <summary>How the player makes a plea: the reference's four lobbying cards.</summary>
    public static class LobbyApproach
    {
        public const string Emotional = "emotional", Strategic = "strategic", Deal = "deal", Pressure = "pressure";
        public static readonly string[] All = { Emotional, Strategic, Deal, Pressure };
        public static bool IsKnown(string approach) => approach != null && Array.IndexOf(All, approach) >= 0;

        /// <summary>Each card's base chance, in percent. The reference's numbers.</summary>
        public static double Base(string approach) =>
            approach == Emotional ? 55 : approach == Strategic ? 50 : approach == Deal ? 60 : approach == Pressure ? 35 : 0;

        private static readonly Dictionary<string, (string[] suits, string[] grates)> Fits = new Dictionary<string, (string[], string[])>
        {
            [Emotional] = (new[] { "Emotional", "Loyal", "Social", "Charming" }, new[] { "Strategic", "Analytical", "Manipulative", "Stubborn" }),
            [Strategic] = (new[] { "Strategic", "Analytical", "Flexible", "Intuitive" }, new[] { "Emotional", "Impulsive", "Stubborn" }),
            [Deal] = (new[] { "Strategic", "Flexible", "Charming", "Manipulative" }, new[] { "Loyal", "Stubborn", "Confrontational" }),
            [Pressure] = (new[] { "Impulsive", "Emotional", "Flexible" }, new[] { "Stubborn", "Confrontational", "Strategic", "Analytical" }),
        };

        /// <summary>Fifteen for each trait the card suits and ten off for each it grates on. The reference's lists.</summary>
        public static double TraitFit(IEnumerable<string> traits, string approach)
        {
            if (!Fits.TryGetValue(approach ?? string.Empty, out var fit)) return 0;
            double total = 0;
            foreach (string trait in traits ?? Enumerable.Empty<string>())
            {
                if (fit.suits.Any(t => string.Equals(t, trait, StringComparison.OrdinalIgnoreCase))) total += 15;
                if (fit.grates.Any(t => string.Equals(t, trait, StringComparison.OrdinalIgnoreCase))) total -= 10;
            }
            return total;
        }
    }

    /// <summary>How a houseguest takes a plea: the reference's four answers.</summary>
    public static class LobbyResponse
    {
        public const string Receptive = "receptive", Open = "open", Skeptical = "skeptical", Hostile = "hostile";
        public static readonly string[] All = { Receptive, Open, Skeptical, Hostile };
        public static bool IsKnown(string response) => response != null && Array.IndexOf(All, response) >= 0;
    }

    /// <summary>One plea: who heard it, what it asked and how, how it was taken, and what it moved.</summary>
    [Serializable]
    public sealed class LobbyState
    {
        public int week;
        /// <summary>The window it was made in: before nominations, or before the veto meeting.</summary>
        public EpisodePhase phase;
        public string deciderId, ask, subjectId, approach, response;
        /// <summary>How far it moved the decision: positive towards what was asked, negative away from it.</summary>
        public double influence;
        public LobbyState Clone() => (LobbyState)MemberwiseClone();
    }
}
