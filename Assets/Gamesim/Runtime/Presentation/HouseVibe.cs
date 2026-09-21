using System.Collections.Generic;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>Counts this week's events the player knows. These are activity categories, not measured moods or private relationships.</summary>
    public static class HouseVibe
    {
        public readonly struct Reading
        {
            public readonly int Activity, Commitments, GameStakes;
            public Reading(int activity, int commitments, int gameStakes)
            { Activity = activity; Commitments = commitments; GameStakes = gameStakes; }
            public int Total => Activity + Commitments + GameStakes;
            public int Peak => Mathf.Max(1, Total);
            public float Fraction(int count) => Mathf.Clamp01(count / (float)Peak);
            public IEnumerable<(string Word, int Count, string Icon, Color Tint)> Rows()
            {
                yield return ("Activity", Activity, "people", UiTheme.Paper);
                // Commitments are alliances and promises kept, which is the positive-relationship
                // colour; game stakes are nominations, evictions and backdoors, which is danger.
                // Neither is navigation, and neither is an achievement.
                yield return ("Commitments", Commitments, "handshake", UiTheme.Allied);
                yield return ("Game stakes", GameStakes, "task", UiTheme.Danger);
                yield return ("Known total", Total, "calendar", UiTheme.Muted);
            }
        }

        // A house event can be a conflict. Counting it as activity makes no claim about its valence.
        private static readonly HashSet<string> CommitmentKinds = new HashSet<string>
        { "alliance", "deal", "deal-outcome", "promise", "promise-outcome", "relationship-milestone" };
        private static readonly HashSet<string> StakesKinds = new HashSet<string>
        { "nomination", "eviction", "final-eviction", "backdoor", "lie", "rumour", "rumour-backfire",
          "scheme", "vent", "campaign-close", "veto" };

        public static Reading Of(EpisodeState state)
        {
            if (state?.events == null) return new Reading(0, 0, 0);
            int activity = 0, commitments = 0, stakes = 0;
            foreach (var entry in state.events)
            {
                if (entry == null || entry.week != state.week) continue;
                if (entry.audienceIds != null && entry.audienceIds.Count > 0
                    && !entry.audienceIds.Contains(state.playerId)) continue;
                if (CommitmentKinds.Contains(entry.kind)) commitments++;
                else if (StakesKinds.Contains(entry.kind)) stakes++;
                else activity++;
            }
            return new Reading(activity, commitments, stakes);
        }

        // Retained call name for the existing chrome hook; the copy no longer invents a mood.
        public static string Tension(Reading reading) => "Known events this week: " + reading.Total;
    }
}
