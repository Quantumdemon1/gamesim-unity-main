using System;
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

        public static bool HasSafetyPromise(EpisodeState s, string fromId) => s.promises.Any(p =>
            p.status == PromiseStatus.Active && p.fromId == s.playerId && p.toId == fromId && p.kind == PromiseKind.Safety);

        public static bool CanPromiseSafety(EpisodeState s, string fromId) => HasSafetyPromise(s, fromId) || s.promises.Count < 200;

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
            : s.promises.Count >= 200 ? "Your promise record is full. Hear them out or turn them down instead."
            : "Gain trust; promise them safety through next week. Nominating them now or as a replacement can break your word.";
    }
}
