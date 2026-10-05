using System.Linq;

namespace Gamesim.Simulation
{
    public sealed partial class EpisodeEngine
    {
        private static void AnswerHoHPitch(EpisodeState s, ReplyCardState card, string key)
        {
            Require(HoHPitches.ValidCard(s, card), "Hear pitches only before your nominations.");
            Require(ReplyCards.Pending(s)?.id == card.id, "Answer the houseguest currently speaking first.");
            var from = s.Find(card.fromId);
            if (key == HoHPitches.FeelOutKey)
            {
                Require(!HoHPitches.Assessed(s, card), "You already felt out this pitch.");
                // Asking why is a disclosure, not ReadPerson's chance-based perception attempt.
                // It teaches an assessment only: never a ballot, threat number, secret pact or odds.
                string subjectId = card.aboutId ?? s.playerId;
                double score = s.Score(from.id, subjectId);
                string opinion = score >= 25 ? "feel solid with" : score <= -25 ? "do not trust" : "are still weighing up";
                string subject = subjectId == s.playerId ? "you" : Name(s, subjectId);
                string line = "You felt out " + from.name + "'s pitch: they say they " + opinion + " " + subject
                    + ". Their recommendation is their own; asking makes no new promise.";
                AddStanding(s, from.id, subjectId, ClaimSource.Told, score);
                SeasonLedger.Append(s.ledger, s.ledger.replies, new ReplyRow { week = s.week, cardId = card.id,
                    kind = ReplyCards.Pitch, fromId = from.id, listenerId = card.aboutId, replyKey = key });
                Remember(s, s.playerId, from.id, line, true);
                Log(s, "information", line, s.playerId, from.id);
                return;
            }
            var reply = ReplyCards.Find(ReplyCards.Pitch, key);
            Require(reply != null, "Choose one of the answers you were offered.");
            if (key == "promise-safety")
            {
                Require(HoHPitches.CanPromiseSafety(s, from.id), "Your promise record is full.");
                if (!HoHPitches.HasSafetyPromise(s, from.id)) MakePromise(s, from.id, PromiseKind.Safety, null);
                else Log(s, "promise", "You reaffirmed your existing safety promise to " + from.name + ".", s.playerId, from.id);
            }
            s.replyCards.Remove(card);
            SeasonLedger.Append(s.ledger, s.ledger.replies, new ReplyRow { week = s.week, cardId = card.id,
                kind = ReplyCards.Pitch, fromId = from.id, listenerId = card.aboutId, replyKey = key, toThem = reply.ToThem });
            // ReplyRow.promised remains a vote-promise flag. Safety belongs to PromiseState.
            if (reply.ToThem != 0) Change(s, s.playerId, from.id, reply.ToThem, ReplyCards.Note(card.kind, reply.Label), "reply");
            Remember(s, from.id, s.playerId, ReplyCards.Memory(card.kind, key, s.week), true);
            Log(s, "reply", ReplyCards.Outcome(s, card, reply), s.playerId, from.id);
        }
    }
}
