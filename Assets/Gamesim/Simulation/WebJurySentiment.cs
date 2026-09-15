using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    [Serializable] public sealed class WebJurySentimentEvent
    {
        public int week;
        public double delta;
        public string reason;
        public WebJurySentimentEvent Clone() => (WebJurySentimentEvent)MemberwiseClone();
    }
    [Serializable] public sealed class WebJurorSentiment
    {
        public string jurorId, jurorName;
        public double sentiment;
        public List<WebJurySentimentEvent> events = new List<WebJurySentimentEvent>();
        public WebJurorSentiment Clone() => new WebJurorSentiment
        {
            jurorId = jurorId, jurorName = jurorName, sentiment = sentiment, events = events.Select(item => item.Clone()).ToList()
        };
    }
    [Serializable] public sealed class WebJurySentimentState
    {
        public List<WebJurorSentiment> jurors = new List<WebJurorSentiment>();
        public double overallSentiment;
        public WebJurySentimentState Clone() => new WebJurySentimentState
        {
            jurors = jurors.Select(item => item.Clone()).ToList(), overallSentiment = overallSentiment
        };
    }

    /// <summary>
    /// Source jury-sentiment-tracker.ts ledger, separate from directed relationships and jury ballots.
    /// Native list order represents source record insertion order for validated nonnumeric native IDs.
    /// </summary>
    public static class WebJurySentiment
    {
        public static WebJurySentimentState CreateInitial() => new WebJurySentimentState();

        public static WebJurySentimentState AddJuror(WebJurySentimentState state, string jurorId, string jurorName, double relationshipScore)
        {
            ValidateState(state); RequireId(jurorId); RequireFinite(relationshipScore);
            var result = state.Clone();
            var juror = new WebJurorSentiment
            {
                jurorId = jurorId, jurorName = jurorName, sentiment = JsRound(relationshipScore * 0.5),
                events = new List<WebJurySentimentEvent> { new WebJurySentimentEvent { week = 0, delta = 0, reason = "Entered jury" } }
            };
            int index = result.jurors.FindIndex(item => item.jurorId == jurorId);
            if (index < 0) result.jurors.Add(juror); else result.jurors[index] = juror;
            return Recalculate(result);
        }

        public static WebJurySentimentState UpdateJurorSentiment(WebJurySentimentState state, string jurorId, double delta, string reason, int week)
        {
            ValidateState(state); RequireId(jurorId); RequireFinite(delta);
            int index = state.jurors.FindIndex(item => item.jurorId == jurorId);
            if (index < 0) return state; // Exact source no-op: do not create a missing juror or recalculate.
            var result = state.Clone();
            Shift(result.jurors[index], delta, reason, week);
            return Recalculate(result);
        }

        public static WebJurySentimentState ShiftAllJurorSentiment(WebJurySentimentState state, double delta, string reason, int week)
        {
            ValidateState(state); RequireFinite(delta);
            var result = state.Clone();
            foreach (var juror in result.jurors) Shift(juror, delta, reason, week);
            return Recalculate(result);
        }

        private static void Shift(WebJurorSentiment juror, double delta, string reason, int week)
        {
            juror.sentiment = Math.Max(-100, Math.Min(100, juror.sentiment + delta));
            juror.events.Add(new WebJurySentimentEvent { week = week, delta = delta, reason = reason });
        }

        private static WebJurySentimentState Recalculate(WebJurySentimentState state)
        {
            double sum = 0;
            foreach (var juror in state.jurors) sum += juror.sentiment;
            state.overallSentiment = state.jurors.Count == 0 ? 0 : JsRound(sum / state.jurors.Count);
            return state;
        }

        private static double JsRound(double value)
        {
            double floor = Math.Floor(value);
            double result = value - floor < 0.5 ? floor : floor + 1;
            return result == 0 && value < 0 ? BitConverter.Int64BitsToDouble(long.MinValue) : result;
        }

        private static void RequireId(string id)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("A juror ID is required.", nameof(id));
        }

        private static void RequireFinite(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
        }

        private static void ValidateState(WebJurySentimentState state)
        {
            if (state == null || state.jurors == null) throw new ArgumentNullException(nameof(state));
            if (state.jurors.Any(item => item == null || string.IsNullOrEmpty(item.jurorId) || item.events == null
                || item.events.Any(entry => entry == null || double.IsNaN(entry.delta) || double.IsInfinity(entry.delta)))
                || state.jurors.Select(item => item.jurorId).Distinct(StringComparer.Ordinal).Count() != state.jurors.Count)
                throw new ArgumentException("The jury ledger is malformed.", nameof(state));
            foreach (var juror in state.jurors) RequireFinite(juror.sentiment);
        }
    }
}
