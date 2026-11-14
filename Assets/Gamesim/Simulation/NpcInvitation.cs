using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// A transient invitation to discuss an offer already saved for the player. No agenda,
    /// relationship score, ballot, random draw or writable commitment is exposed to presentation.
    /// Opening this invitation is not an answer to its offer.
    /// </summary>
    public sealed class NpcInvitation
    {
        public string RecordId { get; }
        public string NpcId { get; }
        public string SessionId { get; }
        public string PlayerId { get; }
        public string Kind { get; }
        public string OpeningLine { get; }
        public int Week { get; }
        public EpisodePhase Phase { get; }
        private readonly string aboutId;
        private readonly int offeredWeek, expiresWeek;

        private NpcInvitation(EpisodeState state, DealState offer)
        {
            RecordId = offer.id; NpcId = offer.proposerId; SessionId = state.sessionId;
            PlayerId = state.playerId; Kind = offer.type; Week = state.week; Phase = state.phase;
            aboutId = offer.targetId; offeredWeek = offer.week; expiresWeek = offer.expiresWeek;
            OpeningLine = Opening(state, offer);
        }

        public static bool PhaseAllows(EpisodePhase phase) => phase == EpisodePhase.Social
            || phase == EpisodePhase.Campaign || phase == EpisodePhase.Nomination
            || phase == EpisodePhase.VetoSelection || phase == EpisodePhase.VetoMeeting;

        /// <summary>
        /// The canonical pending-offer reader owns both visibility and urgency ordering. Called
        /// when the installed snapshot changes, rather than reconstructing the list each frame.
        /// An old proposed row remains available for exactly as long as the engine leaves it proposed.
        /// </summary>
        public static IReadOnlyList<NpcInvitation> Available(EpisodeState state)
        {
            if (state == null || !PhaseAllows(state.phase) || string.IsNullOrEmpty(state.sessionId)
                || state.Find(state.playerId)?.status != ContestantStatus.Active)
                return Array.Empty<NpcInvitation>();
            return NpcDeals.Pending(state).Where(offer => Eligible(state, offer))
                .Select(offer => new NpcInvitation(state, offer)).ToList().AsReadOnly();
        }

        /// <summary>
        /// Revalidates the exact offer. Autonomous clock revisions alone do not invalidate its
        /// question; a new session, phase, week, answer or altered terms does. The owner additionally
        /// guards callbacks by load generation and its current invitation view.
        /// </summary>
        public bool IsCurrent(EpisodeState state)
        {
            if (state == null || state.sessionId != SessionId || state.playerId != PlayerId
                || state.week != Week || state.phase != Phase || !PhaseAllows(state.phase)
                || state.Find(PlayerId)?.status != ContestantStatus.Active) return false;
            return NpcDeals.Pending(state).Any(offer => Eligible(state, offer) && offer.id == RecordId
                && offer.proposerId == NpcId && offer.type == Kind && offer.targetId == aboutId
                && offer.week == offeredWeek && offer.expiresWeek == expiresWeek);
        }

        // Under the role moments the veto board's own asks - the nominees' and the Head of Household's ask to keep the block -
        // stay on the board, where their tone and what a no and silence cost are said (ROLE-MOMENTS-PLAN R7, the lead's Q8).
        // They wait only at the meeting, a phase an invitation plays in with the conversation open, so without this an
        // invitation - the director's way whenever the asks are off - would raise them there. A reader: no rule reads it.
        private static bool Eligible(EpisodeState state, DealState offer) => offer != null
            && !string.IsNullOrEmpty(offer.id) && DealKind.IsKnown(offer.type)
            && offer.status == DealStatus.Proposed && offer.recipientId == state.playerId
            && offer.proposerId != state.playerId && state.Find(offer.proposerId)?.status == ContestantStatus.Active
            && (!DealKind.NamesATarget(offer.type)
                || state.Find(offer.targetId)?.status == ContestantStatus.Active)
            && !(EpisodeEngine.RolesOn(state) && RoleOffers.IsBoardAsk(state, offer));

        private static string Opening(EpisodeState state, DealState offer)
        {
            string about = offer.targetId == offer.proposerId ? "me"
                : offer.targetId == state.playerId ? "you" : state.Find(offer.targetId)?.name;
            switch (offer.type)
            {
                case DealKind.VetoUse: return "Can we talk? I need your help with the veto.";
                // A keep-the-block deal (ROLE-MOMENTS-PLAN R4), only under the role moments; the Head of Household's own ask
                // stays on the board (Eligible), so this is any other one's.
                case DealKind.VetoHold: return "Can we talk about the veto? I'd like my nominations to stand.";
                case DealKind.VoteSave: return "Can we talk about your vote to keep " + (about ?? "me") + "?";
                case DealKind.VoteEvict: return "Can we talk about voting against " + (about ?? "the other nominee") + "?";
                case DealKind.VoteTogether: return "Can we compare notes on this vote? I'd like us to vote together.";
                case DealKind.TargetAgreement: return "Can we talk about working together against " + (about ?? "a common target") + "?";
                case DealKind.SafetyAgreement: return "Can we talk about keeping each other off the block?";
                case DealKind.InformationSharing: return "Can we talk? I'd like us to share what we hear.";
                case DealKind.FinalTwo: return "Can we talk privately about going to the final two together?";
                case DealKind.FinalThree: return "Can we talk about getting each other to the final three?";
                case DealKind.AllianceInvite: return "Can we talk about making an alliance?";
                default: return "Can we talk about working together?";
            }
        }
    }
}
