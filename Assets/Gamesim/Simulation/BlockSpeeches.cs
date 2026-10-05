using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// E5's native speech adapter. An explicit appeal and public broadcast receipt, not NLP,
    /// a new promise, a relationship mutation, or a preview of anybody's private ballot.
    /// The existing event DTO holds a rigorously validated receipt so saved schema23 stays exact.
    /// </summary>
    public static class BlockSpeeches
    {
        public const string Quiet = "quiet", FactorCode = "speech", EventPrefix = "block-speech:";
        public const double MostInfluence = 4, PersuadableMargin = 20;
        public static readonly string[] Approaches = LobbyApproach.All;

        public static bool RulesOn(EpisodeState s) => EpisodeEngine.EconomyRulesOn(s)
            && EpisodeEngine.LeverRulesOn(s) && EpisodeEngine.AgencyOn(s) && StrategyRules.Apply(s);

        public static bool IsApproach(string key) => key == Quiet || LobbyApproach.IsKnown(key);
        public static bool IsReceiptKind(string kind) => kind != null && kind.StartsWith(EventPrefix, StringComparison.Ordinal);
        public static string EventApproach(EpisodeEvent receipt) => IsReceiptKind(receipt?.kind)
            ? receipt.kind.Substring(EventPrefix.Length) : null;
        public static string ReceiptText(EvictionSpeechState speech) => string.IsNullOrWhiteSpace(speech?.text)
            ? "No speech was given." : speech.text;

        public static string Label(string key) => key == LobbyApproach.Emotional ? "Personal appeal"
            : key == LobbyApproach.Strategic ? "Game case" : key == LobbyApproach.Deal ? "Offer to work together"
            : key == LobbyApproach.Pressure ? "Put pressure on the house" : key == Quiet ? "Let your game speak" : "Recorded speech";

        public static string Description(string key) => key == LobbyApproach.Emotional
            ? "Ask the house to keep the person, not just the player. Different listeners take an emotional appeal differently."
            : key == LobbyApproach.Strategic ? "Explain why keeping you helps their game. It can appeal to some listeners and grate on others."
            : key == LobbyApproach.Deal ? "Offer a future working relationship in your words. This is rhetoric: no deal or promise is automatically made."
            : key == LobbyApproach.Pressure ? "Challenge the house to act. Pressure can backfire; it does not guarantee a vote."
            : "Silence adds no speech influence. Their votes remain theirs.";

        /// <summary>
        /// The active house hears the ceremony; an evicted local player hears its public broadcast.
        /// That spectator is a witness, never an eligible voter. Speaker-first is this event contract.
        /// </summary>
        public static string[] Audience(EpisodeState s, string speakerId) => new[] { speakerId }
            .Concat(s.Active.Select(c => c.id).Where(id => id != speakerId))
            .Concat(s.Find(s.playerId)?.status == ContestantStatus.Active ? Array.Empty<string>() : new[] { s.playerId })
            .Distinct(StringComparer.Ordinal).ToArray();

        public static EpisodeEvent Receipt(EpisodeState s, EvictionSpeechState speech) => speech == null ? null
            : s.events.LastOrDefault(e => IsReceiptKind(e.kind) && e.week == speech.week
                && e.phase == EpisodePhase.Eviction && e.audienceIds.Count > 0 && e.audienceIds[0] == speech.speakerId);

        public static string Approach(EpisodeState s, EvictionSpeechState speech) => EventApproach(Receipt(s, speech));

        public static bool ProtectedReceipt(EpisodeState s, EpisodeEvent e) => IsReceiptKind(e.kind) && e.week == s.week
            && e.audienceIds.Count > 0 && s.evictionSpeeches.Any(x => x.week == e.week && x.speakerId == e.audienceIds[0]);

        public static string PublicLine(EpisodeState s, EpisodeEvent e) => IsReceiptKind(e?.kind) && e.audienceIds.Count > 0
            ? (s.Find(e.audienceIds[0])?.name ?? e.audienceIds[0]) + ": " + e.text : e?.text;

        /// <summary>NPC rhetoric has an authored closing sentence matching the recorded appeal.</summary>
        public static string NpcApproach(ContestantState npc) => npc.traits.Any(t => t == "Strategic" || t == "Analytical")
            ? LobbyApproach.Strategic : npc.traits.Contains("Confrontational") ? LobbyApproach.Pressure
            : npc.traits.Contains("Sneaky") ? LobbyApproach.Deal : LobbyApproach.Emotional;

        public static string NpcClosing(string approach) => approach == LobbyApproach.Strategic
            ? " Keeping me gives you another option in this game. Think about what helps your next move."
            : approach == LobbyApproach.Pressure ? " Don't take the easy vote just because it is easy. Show me you can choose for yourselves."
            : approach == LobbyApproach.Deal ? " If I stay, I want to work with people willing to work with me. We can talk terms afterwards."
            : " I'm asking you to remember the person you have lived with, and give me another week.";

        /// <summary>Ephemeral inputs only: saved authoritative text/audience are read, never rewritten.</summary>
        public static void Configure(EpisodeState s, WebVoteOptions options)
        {
            options.nativeSpeechRulesOn = RulesOn(s) && VoteRead.Available(s);
            if (!options.nativeSpeechRulesOn) return;
            options.speechHearer = options.voter.id != s.playerId
                && EpisodeEngine.Voters(s).Any(v => v.id == options.voter.id);
            foreach (var speech in s.evictionSpeeches.Where(x => x.week == s.week && s.nominees.Contains(x.speakerId)))
            {
                var receipt = Receipt(s, speech);
                if (receipt == null) continue; // Never backfill a receipt from old stored prose.
                string other = s.nominees.First(id => id != speech.speakerId);
                options.speechAppeals.Add(new WebVoteSpeechAppeal { nomineeId = speech.speakerId,
                    approach = EventApproach(receipt), heard = receipt.audienceIds.Contains(options.voter.id),
                    opponentAlly = s.Allied(options.voter.id, other), evidenceId = "speech:" + receipt.sequence });
            }
        }

        /// <summary>
        /// One immutable full post-bloc baseline, with ALL speech terms absent. Each nominee gets
        /// at most four points either way (pair margin at most eight). No tie draw or season RNG.
        /// Placeholders remain even at zero so public read counts cannot reveal susceptibility.
        /// </summary>
        public static void Apply(WebVoteOptions options, List<WebNomineeEvaluation> baseline)
        {
            if (!options.nativeSpeechRulesOn) return;
            bool persuadable = options.speechHearer && Math.Abs(baseline[0].score - baseline[1].score) < PersuadableMargin;
            foreach (var nominee in baseline)
            {
                var appeal = options.speechAppeals?.FirstOrDefault(a => a.nomineeId == nominee.nomineeId);
                double value = !persuadable || appeal == null || !appeal.heard || appeal.opponentAlly
                    || appeal.approach == Quiet || !LobbyApproach.IsKnown(appeal.approach) ? 0
                    : Math.Max(-MostInfluence, Math.Min(MostInfluence, 1 + LobbyApproach.TraitFit(options.voter.traits, appeal.approach) / 10));
                nominee.factors.Add(new WebVoteFactor { code = FactorCode, visibility = "private", value = value,
                    evidenceIds = appeal == null ? new List<string>() : new List<string> { appeal.evidenceId } });
                nominee.score += value;
            }
        }

        /// <summary>Reserved typed events have a closed shape, unlike ordinary prose log kinds.</summary>
        public static bool ValidateReceipts(EpisodeState s, out string error)
        {
            error = null;
            var receipts = s.events.Where(e => IsReceiptKind(e.kind)).ToArray();
            foreach (var e in receipts)
            {
                string approach = EventApproach(e);
                if (s.economyRulesVersion != 1 || !IsApproach(approach) || e.phase != EpisodePhase.Eviction
                    || s.weekRulesStartWeek < 1 || e.week < s.weekRulesStartWeek
                    || s.strategyRulesStartWeek < 1 || e.week < s.strategyRulesStartWeek
                    || s.agencyRulesStartWeek < 1 || e.week < s.agencyRulesStartWeek
                    || s.leverRulesStartWeek < 1 || e.week < s.leverRulesStartWeek
                    || e.audienceIds.Count < 3 || e.audienceIds.Count > EpisodeValidation.MaximumCast
                    || e.audienceIds.Distinct(StringComparer.Ordinal).Count() != e.audienceIds.Count
                    || !e.audienceIds.Contains(s.playerId) || (approach == Quiet && e.text != "No speech was given."))
                { error = "Invalid block-speech receipt."; return false; }
                if (e.week != s.week) continue; // Earlier weeks' words outlive cleared speech/nominee rows.
                string speaker = e.audienceIds[0];
                var speech = s.evictionSpeeches.FirstOrDefault(x => x.week == e.week && x.speakerId == speaker);
                if ((s.phase != EpisodePhase.Eviction && !(s.phase == EpisodePhase.Social && s.evictionResolved))
                    || (s.phase == EpisodePhase.Eviction && s.evictionStage == EvictionStage.Interaction)
                    || speech == null || !s.nominees.Contains(speaker) || e.text != ReceiptText(speech)
                    || (approach == Quiet) != string.IsNullOrWhiteSpace(speech.text)
                    || (!s.evictionResolved && s.phase == EpisodePhase.Eviction && !e.audienceIds.SequenceEqual(Audience(s, speaker))))
                { error = "Block-speech receipt does not match its recorded delivery."; return false; }
            }
            if (receipts.GroupBy(e => e.week + ":" + e.audienceIds[0]).Any(g => g.Count() > 1)
                || receipts.GroupBy(e => e.week).Any(g => g.Count() > 2))
            { error = "Duplicate block-speech receipt."; return false; }
            return true;
        }
    }
}
