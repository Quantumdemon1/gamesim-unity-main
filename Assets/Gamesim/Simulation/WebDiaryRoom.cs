using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Gamesim.Simulation
{
    [Serializable] public sealed class WebPersonaScore
    {
        public string persona;
        public int score;
        public WebPersonaScore Clone() => (WebPersonaScore)MemberwiseClone();
    }
    [Serializable] public sealed class WebPersonaHistory
    {
        public string persona;
        public int week;
        public WebPersonaHistory Clone() => (WebPersonaHistory)MemberwiseClone();
    }
    [Serializable] public sealed class WebPersonaState
    {
        public string current = "Neutral";
        // Preserve JavaScript object insertion order: order breaks equal-score ties.
        public List<WebPersonaScore> scores = new List<WebPersonaScore>();
        public List<WebPersonaHistory> history = new List<WebPersonaHistory>();
        public WebPersonaState Clone() => new WebPersonaState
        {
            current = current, scores = scores.Select(item => item.Clone()).ToList(), history = history.Select(item => item.Clone()).ToList()
        };
    }
    // These catalog/choice-plan DTOs are transient, not Unity-serialized save state.
    // Json.NET handles their public nullable fields; EpisodeState stores applied values.
    public sealed class WebDiaryEffects
    {
        public int? juryDelta, reputationDelta, socialBonus, competitionBonus, stealthModifier;
    }
    public sealed class WebDiaryChoice
    {
        public string id, text, persona, description;
        public WebDiaryEffects effects = new WebDiaryEffects();
    }
    public sealed class WebDiaryEvent
    {
        public string id, trigger, narrative;
        public int week;
        public List<WebDiaryChoice> choices = new List<WebDiaryChoice>();
    }
    public sealed class WebDiaryChoicePlan
    {
        public WebPersonaState persona;
        public int lastDiaryRoomWeek;
        public int? socialBonusDelta, competitionBonusDelta, juryDelta;
        public string juryReason, logType, logDescription, phase;
        public string[] dispatchTypes;
    }

    /// <summary>
    /// Pure diary-room-system.ts rules plus an explicit plan for the GameHUD choice callback.
    /// This helper does not authorize a command, schedule a UI event, mutate jury state, or turn
    /// declared reputation/stealth effects into gameplay effects that the source never dispatched.
    /// </summary>
    public static class WebDiaryRoom
    {
        private static readonly string[] PersonaOrder = { "Neutral", "Remorseful", "Ruthless", "Calculated", "Social Butterfly" };

        public static WebPersonaState CreateInitialPersonaState() => new WebPersonaState
        {
            scores = PersonaOrder.Select(persona => new WebPersonaScore { persona = persona, score = 0 }).ToList()
        };

        public static WebPersonaState ApplyPersonaChoice(WebPersonaState state, WebDiaryChoice choice, int week)
        {
            if (state == null || state.scores == null || state.history == null) throw new ArgumentException("Persona state is required.", nameof(state));
            if (choice == null || !PersonaOrder.Contains(choice.persona)) throw new ArgumentException("Unknown choice persona.", nameof(choice));
            if (state.scores.Any(item => item == null || !PersonaOrder.Contains(item.persona) || item.score < 0)
                || state.scores.Select(item => item.persona).Distinct(StringComparer.Ordinal).Count() != state.scores.Count
                || state.history.Any(item => item == null || !PersonaOrder.Contains(item.persona)))
                throw new ArgumentException("Persona scores/history are malformed.", nameof(state));
            var result = state.Clone();
            var score = result.scores.FirstOrDefault(item => item.persona == choice.persona);
            if (score == null) { score = new WebPersonaScore { persona = choice.persona }; result.scores.Add(score); }
            score.score = checked(score.score + 1);
            var dominant = result.scores[0];
            foreach (var item in result.scores.Skip(1)) if (item.score > dominant.score) dominant = item;
            result.current = dominant.score >= 2 ? dominant.persona : "Neutral";
            result.history.Add(new WebPersonaHistory { persona = choice.persona, week = week });
            return result;
        }

        /// <summary>Quota and post-eviction/unknown branches consume zero draws; nomination/mid-week consume one.</summary>
        public static bool ShouldTrigger(string trigger, int week, int? lastDiaryRoomWeek, Func<double> nextRoll)
        {
            if (lastDiaryRoomWeek.HasValue && lastDiaryRoomWeek.Value >= week) return false;
            switch (trigger)
            {
                case "post_eviction": return true;
                case "post_nomination": return Roll(nextRoll) < 0.40;
                case "mid_week": return Roll(nextRoll) < 0.25;
                default: return false;
            }
        }

        /// <summary>No randomness. playerWasInvolved is retained but unused by the original source.</summary>
        public static WebDiaryEvent GenerateEvent(string trigger, int week, string evictedName = null,
            bool playerWasInvolved = false, bool isNominee = false)
        {
            if (trigger == null) throw new ArgumentNullException(nameof(trigger));
            WebDiaryEvent result;
            if (trigger == "post_eviction" && !string.IsNullOrEmpty(evictedName))
            {
                result = WebDiaryRoomCatalog.PostEviction();
                result.narrative = result.narrative.Replace(WebDiaryRoomCatalog.EvicteeToken, evictedName);
                foreach (var choice in result.choices) choice.text = choice.text.Replace(WebDiaryRoomCatalog.EvicteeToken, evictedName);
            }
            else if (trigger == "post_nomination") result = isNominee ? WebDiaryRoomCatalog.Nominated() : WebDiaryRoomCatalog.NotNominated();
            else result = WebDiaryRoomCatalog.Generic();
            result.trigger = trigger; result.week = week; result.id = "diary-" + trigger + "-" + week.ToString(CultureInfo.InvariantCulture);
            return result;
        }

        /// <summary>
        /// Plan actual GameHUD dispatches after a catalog choice. The current game week, not
        /// the event's displayed week, is used by the source callback. Jury delta is an intent
        /// for the separate sentiment ledger, never a directed relationship-score change.
        /// </summary>
        public static WebDiaryChoicePlan PlanChoice(WebPersonaState persona, WebDiaryEvent diaryEvent,
            string choiceId, int currentWeek, string currentPhase)
        {
            if (diaryEvent == null || diaryEvent.choices == null) throw new ArgumentNullException(nameof(diaryEvent));
            var choices = diaryEvent.choices.Where(item => item != null && item.id == choiceId).ToArray();
            if (choices.Length != 1 || choices[0].effects == null) throw new ArgumentException("The current diary choice is not valid.", nameof(choiceId));
            var choice = choices[0];
            var result = new WebDiaryChoicePlan
            {
                persona = ApplyPersonaChoice(persona ?? CreateInitialPersonaState(), choice, currentWeek),
                lastDiaryRoomWeek = currentWeek, socialBonusDelta = Truthy(choice.effects.socialBonus),
                competitionBonusDelta = Truthy(choice.effects.competitionBonus), juryDelta = Truthy(choice.effects.juryDelta),
                juryReason = Truthy(choice.effects.juryDelta).HasValue ? "Diary Room: " + choice.persona : null,
                logType = "diary-room", logDescription = "Diary Room confessional: \"" + choice.text + "\" (" + choice.persona + ")", phase = currentPhase
            };
            var dispatches = new List<string> { "SET_PLAYER_PERSONA", "SET_LAST_DIARY_WEEK" };
            if (result.socialBonusDelta.HasValue) dispatches.Add("SET_SOCIAL_BONUS");
            if (result.competitionBonusDelta.HasValue) dispatches.Add("SET_COMPETITION_BONUS");
            if (result.juryDelta.HasValue) dispatches.Add("SET_JURY_SENTIMENT");
            dispatches.Add("LOG_EVENT"); result.dispatchTypes = dispatches.ToArray();
            return result;
        }

        private static int? Truthy(int? value) => value.GetValueOrDefault() == 0 ? (int?)null : value;
        private static double Roll(Func<double> nextRoll)
        {
            if (nextRoll == null) throw new ArgumentNullException(nameof(nextRoll));
            double sample = nextRoll();
            if (double.IsNaN(sample) || double.IsInfinity(sample) || sample < 0 || sample >= 1)
                throw new ArgumentOutOfRangeException(nameof(nextRoll), "Random samples must be in [0, 1).");
            return sample;
        }
    }
}
