using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// E4's native adapter: a courting NPC recommends a target and asks the player HoH for safety.
    /// Uses existing card/standing/promise records, never a preview of hidden nomination weights.
    /// </summary>
    public static class HoHPitches
    {
        public const string FeelOutKey = "feel-out";
        public static bool Available(EpisodeState s) => EpisodeEngine.EconomyRulesOn(s)
            && StrategyRules.Apply(s) && EpisodeEngine.AgencyOn(s)
            && s.phase == EpisodePhase.Nomination && s.nominees.Count == 0
            && s.hohId == s.playerId && s.Find(s.playerId)?.status == ContestantStatus.Active;

        public static bool ValidCard(EpisodeState s, ReplyCardState card) => Available(s)
            && card != null && card.kind == ReplyCards.Pitch && card.week == s.week
            && card.fromId != s.playerId && s.Find(card.fromId)?.status == ContestantStatus.Active
            && (card.aboutId == null || (card.aboutId != card.fromId && card.aboutId != s.playerId
                && s.Find(card.aboutId)?.status == ContestantStatus.Active))
            && !(s.ledger?.replies?.Any(r => r != null && r.week == card.week && r.kind == ReplyCards.Pitch
                && r.fromId == card.fromId && r.replyKey != FeelOutKey) ?? false);

        /// <summary>Only the existing courting agenda owns an offer, once per speaker in this nomination.</summary>
        public static void Offer(EpisodeState s, string fromId)
        {
            if (!Available(s) || NpcAgendas.Of(s, fromId)?.kind != Agendas.Court
                || s.replyCards.Count >= 24 || s.replyCards.Any(c => c.kind == ReplyCards.Pitch && c.fromId == fromId)
                || s.ledger.replies.Any(r => r.week == s.week && r.kind == ReplyCards.Pitch && r.fromId == fromId)) return;
            string id = "reply-pitch-" + s.week + "-" + fromId;
            if (s.replyCards.Any(c => c.id == id)) return;
            string target = ThreatAssessment.RankedTargets(s, fromId)
                .FirstOrDefault(other => other != s.playerId && !s.Allied(fromId, other));
            s.replyCards.Add(new ReplyCardState { id = id, week = s.week, kind = ReplyCards.Pitch,
                fromId = fromId, aboutId = target });
        }

        /// <summary>
        /// One assessment per exact card, independent of the weekly ReadPerson attempt. During a
        /// nomination only pitches write reply rows (at most 24 inspections + 24 final answers).
        /// That bounded lifetime keeps a new receipt in the 512-row ledger even when it started full.
        /// Nominate clears pending cards, and an answered speaker is not offered another this week.
        /// </summary>
        public static bool Assessed(EpisodeState s, ReplyCardState card) => s?.ledger?.replies != null && card != null
            && s.ledger.replies.Any(r => r.week == card.week && r.cardId == card.id && r.kind == ReplyCards.Pitch
                && r.fromId == card.fromId && r.replyKey == FeelOutKey);

        public static bool HasSafetyPromise(EpisodeState s, string fromId) => CommitmentReferences.Promises(s).Any(p =>
            p.status == PromiseStatus.Active && p.fromId == s.playerId && p.toId == fromId && p.kind == PromiseKind.Safety);

        public static bool CanPromiseSafety(EpisodeState s, string fromId) => HasSafetyPromise(s, fromId) || CommitmentReferences.PromiseCount(s) < 200;

        /// <summary>Display the standing learned by the assessment, never the live hidden score.</summary>
        public static string Assessment(EpisodeState s, ReplyCardState card)
        {
            if (!Assessed(s, card)) return null;
            string subject = card.aboutId ?? s.playerId;
            var read = s.ledger.standings.LastOrDefault(r => r.week == card.week && r.fromId == card.fromId
                && r.toId == subject && r.source == ClaimSource.Told);
            if (read == null) return "You already felt out this pitch. Check your notebook for the conversation.";
            string band = read.score >= 25 ? "feel solid with" : read.score <= -25 ? "do not trust" : "are still weighing up";
            return "From your assessment: they say they " + band + " " + (subject == s.playerId ? "you" : s.Find(subject)?.name)
                + ". Asking makes no new promise.";
        }

        public static string SafetyDescription(EpisodeState s, string fromId) => HasSafetyPromise(s, fromId)
            ? "Reaffirm your existing safety promise; no second promise is made. Nominating them can break your word."
            : CommitmentReferences.PromiseCount(s) >= 200 ? PromiseRecordFull
            : "Gain trust; promise them safety through next week. Nominating them now or as a replacement can break your word.";

        /// <summary>Why a new promise of safety cannot be made: the record holds as many as it can.</summary>
        public const string PromiseRecordFull = "Your promise record is full. Hear them out or turn them down instead.";

        // ---------------------------------------------------------------- the pitch board (ROLE-MOMENTS-PLAN R5, §3.1)
        //
        // Under the role moments a nomination's pitches are a board: at most three, answered in any order, and a promise of
        // safety pays when it is kept rather than when it is made. Nothing new is stored: the cards are the reply cards they
        // always were, and the payments read the week's reply rows. Every rule here is off in a season without the role
        // moments, which keeps every pitch its courting houseguests bring, one at a time, at the answers' old numbers.

        /// <summary>The most pitches one nomination brings under the role moments: the two the player reads warmest, and the one they read coolest.</summary>
        public const int BoardSize = 3, WarmestOnTheBoard = 2;

        /// <summary>
        /// What a promise of safety is worth with a pitcher under the role moments, paid at the nominations to one the player
        /// left off the block: the answer's old +8, paid when the promise is kept at the ceremony rather than when it is made.
        /// </summary>
        public const double KeptOffTheBlock = 8;

        /// <summary>
        /// How far a pitcher's grudge for the nomination eases when the player turned them down before naming them - at the
        /// ceremony or as the replacement - under the role moments: an honest no softens the blow. Not for a Threatening one,
        /// and never by more than that nomination added to a grudge they already held (<c>EpisodeEngine.EaseHonestNos</c>).
        /// </summary>
        public const double HonestNoEase = 30;

        /// <summary>The answers' keys, as <see cref="ReplyCards"/> holds them.</summary>
        public const string PromiseKey = "promise-safety", HearKey = "hear", TurnDownKey = "turn-down";

        /// <summary>
        /// Whether the pitch board's rules play this week, whatever the phase: the role moments (<see cref="EpisodeEngine.RolesOn"/>)
        /// and what <see cref="Available"/> asks besides the phase - the economy, the strategy windows and agency.
        /// </summary>
        public static bool BoardRules(EpisodeState s) => EpisodeEngine.RolesOn(s) && EpisodeEngine.EconomyRulesOn(s)
            && StrategyRules.Apply(s) && EpisodeEngine.AgencyOn(s);

        /// <summary>Whether this nomination's pitches are the role moments' board: pitches are heard (<see cref="Available"/>) under the role moments.</summary>
        public static bool OnTheBoard(EpisodeState s) => Available(s) && EpisodeEngine.RolesOn(s);

        /// <summary>
        /// Whether the card with this id may be answered now: under the role moments any pitch waiting on the board, in any
        /// order; otherwise, as always, only the card waiting first (<see cref="ReplyCards.Pending"/>).
        /// </summary>
        public static bool Answerable(EpisodeState s, string cardId)
        {
            var card = s?.replyCards?.FirstOrDefault(c => c != null && c.id == cardId);
            if (card == null) return false;
            return (card.kind == ReplyCards.Pitch && OnTheBoard(s)) || ReplyCards.Pending(s)?.id == cardId;
        }

        /// <summary>
        /// Which of these courting houseguests the board keeps, in its order: the two the player reads warmest, then the one
        /// they read coolest - by the player's own reading only (<see cref="ReplyCards.ReadsAsClose"/>, then their own score
        /// of each), ties by id. Never by how any of them reads the player. Three or fewer are all kept, warmest first.
        /// </summary>
        public static List<string> BoardPicks(EpisodeState s, IEnumerable<string> pitcherIds)
        {
            var ids = (pitcherIds ?? Enumerable.Empty<string>()).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToList();
            var warm = ids.OrderByDescending(id => ReplyCards.ReadsAsClose(s, id)).ThenByDescending(id => s.Score(s.playerId, id))
                .ThenBy(id => id, StringComparer.Ordinal).Take(WarmestOnTheBoard).ToList();
            var cool = ids.Where(id => !warm.Contains(id)).OrderBy(id => ReplyCards.ReadsAsClose(s, id)).ThenBy(id => s.Score(s.playerId, id))
                .ThenBy(id => id, StringComparer.Ordinal).Take(BoardSize - warm.Count);
            return warm.Concat(cool).ToList();
        }

        /// <summary>
        /// Under the role moments, the nomination's pitches as its board, once every courting houseguest has come to the player
        /// (<c>EpisodeEngine.CourtTheHoH</c>): only <see cref="BoardPicks"/>' cards stay, in its order, where the pitches stood.
        /// A houseguest whose card does not stay still courted the Head of Household, as every courting houseguest does; they
        /// are simply not on the board. It draws nothing and reads nothing hidden. Nothing without the board.
        /// </summary>
        public static void HoldTheBoard(EpisodeState s)
        {
            if (!OnTheBoard(s)) return;
            var pitches = s.replyCards.Where(c => c.kind == ReplyCards.Pitch).ToList();
            if (pitches.Count == 0) return;
            int at = s.replyCards.IndexOf(pitches[0]);
            var kept = BoardPicks(s, pitches.Select(c => c.fromId)).Select(id => pitches.First(c => c.fromId == id)).ToList();
            s.replyCards.RemoveAll(c => c.kind == ReplyCards.Pitch);
            s.replyCards.InsertRange(Math.Min(at, s.replyCards.Count), kept);
        }

        /// <summary>
        /// What turning a pitcher down costs with them: on the board, their card's tone's honest no (<see cref="RoleTone.Stakes"/>,
        /// 0 to -3), read from the state the card is answered in; without it, the answer's own -5.
        /// </summary>
        public static double TurnDownCost(EpisodeState s, string fromId) =>
            OnTheBoard(s) && RoleTone.Stakes(RoleTone.Of(s, fromId, RoleTone.Role.Courting)) is RoleTone.Stake stake
                ? stake.decline : ReplyCards.Find(ReplyCards.Pitch, TurnDownKey).ToThem;

        /// <summary>
        /// What an answer moves between the player and the pitcher as it is given. Without the board, the answer's own number.
        /// On the board a promise moves nothing at the answer - it pays at the nominations, if kept (<see cref="KeptOffTheBlock"/>)
        /// - hearing them is nothing, as ever, and a no costs its tone's (<see cref="TurnDownCost"/>).
        /// </summary>
        public static double AnswerImpact(EpisodeState s, ReplyCardState card, ReplyCards.Reply reply) =>
            !OnTheBoard(s) ? reply.ToThem : reply.Key == TurnDownKey ? TurnDownCost(s, card.fromId) : 0;

        /// <summary>
        /// Whether a pitch answer's recorded change is one its week's rules could have written: the answer's own number in a
        /// week without the role moments, exactly as before; in a week with them, nothing for a promise or for hearing them,
        /// and for a no one of the honest no's a courting houseguest's tone can cost (<see cref="RoleTone.Stakes"/>).
        /// </summary>
        public static bool RecordedImpact(EpisodeState s, ReplyRow row)
        {
            var reply = row == null ? null : ReplyCards.Find(ReplyCards.Pitch, row.replyKey);
            if (reply == null) return false;
            if (!EpisodeEngine.RolesOnIn(s, row.week)) return row.toThem == reply.ToThem;
            return reply.Key == TurnDownKey
                ? RoleTone.TonesFor(RoleTone.Role.Courting).Any(tone => RoleTone.Stakes(tone).decline == row.toThem)
                : row.toThem == 0;
        }

        /// <summary>Whether the player turned this houseguest's pitch down this week: the week's reply row says so.</summary>
        public static bool TurnedDownThisWeek(EpisodeState s, string fromId) => AnsweredThisWeek(s, fromId, TurnDownKey);

        private static bool AnsweredThisWeek(EpisodeState s, string fromId, string key) =>
            s?.ledger?.replies != null && !string.IsNullOrEmpty(fromId)
            && s.ledger.replies.Any(r => r != null && r.week == s.week && r.kind == ReplyCards.Pitch && r.fromId == fromId && r.replyKey == key);
    }
}
