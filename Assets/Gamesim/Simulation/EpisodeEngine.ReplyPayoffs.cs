using System;
using System.Linq;

namespace Gamesim.Simulation
{
    public sealed partial class EpisodeEngine
    {
        private static void ReplyPayoff(EpisodeState s, ReplyCardState card, ReplyCards.Reply reply)
        {
            if (!ReplyCardPayoffs.On(s)) return;
            if (card.kind == ReplyCards.Confrontation && reply.Key == "escalate")
            {
                // A reply is free; the existing proposal reducer owns consent, expiry, the cap,
                // relationship consequences and any counter. It does not itself spend an action.
                // No free automatic pact, substituted acceptance coin or new save record type.
                if (PlayerDeals.CanPropose(s, card.fromId, DealKind.SafetyAgreement, null, out string refusal))
                    ProposeDeal(s, s.Find(card.fromId), new EpisodeCommand { text = DealKind.SafetyAgreement });
                else Log(s, "reply-payoff", "No new safety proposal was made. " + refusal, s.playerId, card.fromId);
                return;
            }
            string subjectId = null;
            if (card.kind == ReplyCards.Confrontation && reply.Key == "deflect")
                subjectId = s.Active.Where(c => c.id != s.playerId && c.id != card.fromId)
                    .OrderBy(c => s.Score(card.fromId, c.id)).ThenBy(c => c.id, StringComparer.Ordinal)
                    .Select(c => c.id).FirstOrDefault();
            else if ((card.kind == ReplyCards.Gossip && reply.Key == "confront") || (card.kind == ReplyCards.Plea && reply.Key == "refuse"))
            {
                if (ReplyCardPayoffs.AssessmentAvailable(s, card)) subjectId = card.aboutId;
            }
            if (subjectId == null) return;
            var from = s.Find(card.fromId); var subject = s.Find(subjectId);
            double score = s.Score(from.id, subject.id);
            string opinion = score >= 25 ? "feel solid with" : score <= -25 ? "do not trust" : "are still weighing up";
            string line = from.name + " answered you: they " + opinion + " " + subject.name + ". This is an assessment, not a vote promise.";
            AddStanding(s, from.id, subject.id, ClaimSource.Told, score);
            Remember(s, s.playerId, from.id, line, true);
            Log(s, "information", line, s.playerId, from.id);
        }
    }
}
