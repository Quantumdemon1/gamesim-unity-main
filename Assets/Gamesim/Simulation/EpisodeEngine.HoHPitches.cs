using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    public sealed partial class EpisodeEngine
    {
        private static void AnswerHoHPitch(EpisodeState s, ReplyCardState card, string key)
        {
            Require(HoHPitches.ValidCard(s, card), "Hear pitches only before your nominations.");
            // Under the role moments the pitches are a board, answered in any order (ROLE-MOMENTS-PLAN R5, §3.1).
            Require(HoHPitches.OnTheBoard(s) || ReplyCards.Pending(s)?.id == card.id, "Answer the houseguest currently speaking first.");
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
            // What the answer moves as it is given: its own number; on the board (R5) nothing for a promise, which pays at the
            // nominations if it is kept, and the card's tone's honest no for a no, read before the answer moves anything.
            double toThem = HoHPitches.AnswerImpact(s, card, reply);
            if (key == "promise-safety")
            {
                Require(HoHPitches.CanPromiseSafety(s, from.id), "Your promise record is full.");
                if (!HoHPitches.HasSafetyPromise(s, from.id)) MakePromise(s, from.id, PromiseKind.Safety, null, UnifiedCommitments.HoHPitch);
                else Log(s, "promise", "You reaffirmed your existing safety promise to " + from.name + ".", s.playerId, from.id);
            }
            s.replyCards.Remove(card);
            SeasonLedger.Append(s.ledger, s.ledger.replies, new ReplyRow { week = s.week, cardId = card.id,
                kind = ReplyCards.Pitch, fromId = from.id, listenerId = card.aboutId, replyKey = key, toThem = toThem });
            // ReplyRow.promised remains a vote-promise flag. Safety belongs to PromiseState.
            if (toThem != 0) Change(s, s.playerId, from.id, toThem, ReplyCards.Note(card.kind, reply.Label), "reply");
            Remember(s, from.id, s.playerId, ReplyCards.Memory(card.kind, key, s.week), true);
            Log(s, "reply", ReplyCards.Outcome(s, card, reply), s.playerId, from.id);
        }

        /// <summary>
        /// The pitch board at the nominations (ROLE-MOMENTS-PLAN R5, §3.1), under the role moments only, read off the week's
        /// reply rows: each one the player promised safety and left off the block warms to them by
        /// <see cref="HoHPitches.KeptOffTheBlock"/> - the promise's warmth, paid when it is kept at the ceremony rather than
        /// when it was made - through <see cref="Change"/>, and the player is told. A promise to somebody nominated broke with
        /// the nomination, under the promise rules, and pays nothing; naming one later as the replacement breaks it the same
        /// way. Only a promise pays: a pitcher heard or turned down and left off the block is owed nothing. A pitch left
        /// unanswered was cleared with the others, at no cost. (The honest no's ease is the ceremony's own, beside its grudge:
        /// <see cref="EaseHonestNos"/>.) Nothing in a season without the role moments, whose promise paid as it was made. A
        /// week's pitch rows exist only when the player is its Head of Household - nobody pitches to anybody else - so another
        /// Head of Household's nominations find none and settle nothing.
        /// </summary>
        private static void SettleThePitchBoard(EpisodeState s)
        {
            if (!HoHPitches.BoardRules(s)) return;
            var promised = s.ledger.replies.Where(r => r != null && r.week == s.week && r.kind == ReplyCards.Pitch
                && r.replyKey == HoHPitches.PromiseKey).Select(r => r.fromId).Distinct().ToList();
            // Nobody leaves the house between the pitches and the names - production removes at the social close, the vote at
            // the eviction - so each promised pitcher is still here.
            foreach (string id in promised)
            {
                var pitcher = s.Find(id);
                if (pitcher == null || s.nominees.Contains(id)) continue;
                Change(s, s.playerId, id, HoHPitches.KeptOffTheBlock, "Kept me off the block, as promised.", "reply");
                Log(s, "promise", pitcher.name + " stayed off your block, as you promised.", s.playerId, id);
            }
        }

        /// <summary>
        /// Which of the houseguests about to go on the block - at the ceremony, or as the replacement - the player turned down
        /// at their pitch this week, under the role moments, with what each holds against the player before the nomination
        /// writes its grudge (R5, read just before <c>StoryNominated</c> or <c>StoryReplacement</c>). Not a Threatening one,
        /// who takes no discount: a tone read from the week's fixed facts (<see cref="RoleTone.RiderTones"/>), so it is the
        /// one their card wore. Nobody without the board's rules, and nobody the player did not turn down this week - which is
        /// nobody in a week somebody else is Head of Household, since only the player's own pitches write the rows.
        /// </summary>
        private static List<(string id, double held)> HonestNos(EpisodeState s, IEnumerable<string> named) =>
            !HoHPitches.BoardRules(s) ? new List<(string, double)>()
                : named.Where(id => HoHPitches.TurnedDownThisWeek(s, id) && RoleTone.Of(s, id, RoleTone.Role.Courting) != RoleTone.Threatening)
                    .Select(id => (id, Grudges.Severity(s, id, s.playerId))).ToList();

        /// <summary>
        /// The honest no (R5), once the nomination has written its grudge: each of <see cref="HonestNos"/>' grudge against
        /// the player eases by <see cref="HoHPitches.HonestNoEase"/>, through the story effect router, as a rider eases a
        /// grudge (R3) - honesty softens the blow. Never by more than this nomination added: a grudge they already held
        /// stacks the nomination's at half its weight, up to the ceiling (<see cref="Grudges.Add"/>), and an honest no softens
        /// this nomination; it never forgives an earlier wrong. A fresh grudge of 70 eases to 40; one of 50 stacks to 85 and
        /// eases to 55; one of 90 stacks to the ceiling and eases back to 90.
        /// </summary>
        private static void EaseHonestNos(EpisodeState s, List<(string id, double held)> honest)
        {
            foreach (var (id, held) in honest)
            {
                double ease = Math.Min(HoHPitches.HonestNoEase, Grudges.Severity(s, id, s.playerId) - held);
                if (ease > 0) Rider(s, StoryEffects.Ease, id, s.playerId, ease, null, "w" + s.week + ":pitch-board:" + id);
            }
        }
    }
}
