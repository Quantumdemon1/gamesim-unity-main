using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Fresh E2 replies trade warmth, information, freedom and a negotiated obligation. This
    /// reader explains public terms only: it never selects a hidden opinion or previews a roll.
    /// Existing keys and base effects belong to ReplyCards, including the legacy descriptions.
    /// </summary>
    public static class ReplyCardPayoffs
    {
        public static bool On(EpisodeState s) => EpisodeEngine.EconomyRulesOn(s);

        public static string Description(EpisodeState s, ReplyCardState card, ReplyCards.Reply reply)
        {
            if (reply == null) return null;
            if (!On(s) || card == null) return reply.Description;
            if (card.kind == ReplyCards.Confrontation)
            {
                if (reply.Key == "apologize") return "Repair trust. No new intel or commitment.";
                if (reply.Key == "deflect") return "Lose trust; learn one opinion about someone else. No promise.";
                if (reply.Key == "escalate") return PlayerDeals.CanPropose(s, card.fromId, DealKind.SafetyAgreement, null, out _)
                    ? "Offer mutual safety this week after a fight. They may refuse; a yes binds both."
                    : "Push back hard. No new safety proposal is available.";
            }
            if (card.kind == ReplyCards.Gossip)
            {
                if (reply.Key == "confront") return AssessmentAvailable(s, card)
                    ? "Lose trust; learn their view of the listener. No ballot revealed."
                    : "Lose trust. The listener is unavailable, so there is no new assessment.";
                if (reply.Key == "slide") return "Take the smallest trust loss. No new intel or retaliation.";
                if (reply.Key == "gossip-back") return "Damage their bond with the listener, at a trust cost.";
            }
            if (card.kind == ReplyCards.Plea)
            {
                if (reply.Key == "promise") return "Gain trust; promise to keep them. Your vote can break your word.";
                if (reply.Key == "noncommittal") return "No new loss, intel or commitment. Your vote stays free.";
                if (reply.Key == "refuse") return AssessmentAvailable(s, card)
                    ? "Lose trust; hear their view of the other nominee. No promise."
                    : "Refuse and lose trust. No other active nominee to assess; no promise.";
            }
            return reply.Description;
        }

        // Identity and public status only. Ranking and scores are never read in an option preview.
        public static bool AssessmentAvailable(EpisodeState s, ReplyCardState card)
        {
            if (s == null || card == null || card.aboutId == s.playerId || card.aboutId == card.fromId) return false;
            var subject = s.Find(card.aboutId);
            return subject != null && subject.status == ContestantStatus.Active
                && (card.kind != ReplyCards.Plea || (s.phase == EpisodePhase.Campaign && !s.evictionResolved
                    && s.nominees.Contains(card.fromId) && s.nominees.Contains(card.aboutId)));
        }
    }
}
