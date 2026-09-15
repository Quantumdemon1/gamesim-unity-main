using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    [Serializable] public sealed class ArcHistory
    {
        public int week;
        public double delta;
        public string reason;
        public ArcHistory Clone() => (ArcHistory)MemberwiseClone();
    }

    [Serializable] public sealed class RelationshipArcState
    {
        public string npcId, npcName, arcType = "neutral";
        public double intensity;
        public int escalationLevel;
        public List<ArcHistory> weeklyHistory = new List<ArcHistory>();
        public RelationshipArcState Clone()
        {
            var clone = (RelationshipArcState)MemberwiseClone();
            clone.weeklyHistory = weeklyHistory.Select(item => item.Clone()).ToList();
            return clone;
        }
    }

    [Serializable] public sealed class ArcEscalation
    { public string type, npcName; public int level; }
    public sealed class ArcUpdate
    { public List<RelationshipArcState> arcs; public ArcEscalation escalationEvent; }

    /// <summary>Source relationship-arc-tracker.ts arithmetic and authored narrative, with detached native DTOs.</summary>
    public static class WebRelationshipArcs
    {
        private static readonly double[] Thresholds = { 0, 25, 50, 75, 90 };
        private static readonly string[] Labels = { "Neutral", "Mild", "Moderate", "Intense", "Legendary" };
        public static int GetEscalationLevel(double intensity)
        {
            for (int i = Thresholds.Length - 1; i >= 0; i--) if (intensity >= Thresholds[i]) return i;
            return 0;
        }
        public static string GetEscalationLabel(int level)
        {
            if (level < 0) throw new ArgumentOutOfRangeException(nameof(level));
            return Labels[Math.Min(level, Labels.Length - 1)];
        }

        public static ArcUpdate Update(IEnumerable<RelationshipArcState> arcs, string npcId, string npcName, double delta, string reason, int week)
        {
            if (double.IsNaN(delta) || double.IsInfinity(delta)) throw new ArgumentOutOfRangeException(nameof(delta));
            var updated = arcs?.Select(arc => arc.Clone()).ToList() ?? new List<RelationshipArcState>();
            var target = updated.FirstOrDefault(arc => arc.npcId == npcId);
            if (target == null)
            {
                target = new RelationshipArcState { npcId = npcId, npcName = npcName };
                updated.Add(target);
            }
            target.weeklyHistory.Add(new ArcHistory { week = week, delta = delta, reason = reason });
            target.intensity = Math.Min(100, target.intensity + Math.Min(Math.Abs(delta) * 1.5, 15));
            double sentiment = target.weeklyHistory.Sum(item => item.delta);
            target.arcType = sentiment <= -15 ? "rivalry" : sentiment >= 15 ? "friendship" : "neutral";
            int oldLevel = target.escalationLevel;
            target.escalationLevel = GetEscalationLevel(target.intensity);
            var escalation = target.escalationLevel > oldLevel && target.arcType != "neutral"
                ? new ArcEscalation { type = target.arcType == "rivalry" ? "RIVALRY_ESCALATION" : "FRIENDSHIP_DEEPENED", npcName = target.npcName, level = target.escalationLevel }
                : null;
            return new ArcUpdate { arcs = updated, escalationEvent = escalation };
        }

        public static string Narrative(RelationshipArcState arc)
        {
            string label = GetEscalationLabel(arc.escalationLevel);
            if (arc.arcType == "rivalry")
            {
                switch (label)
                {
                    case "Mild": return "Tensions are building between you and " + arc.npcName + ".";
                    case "Moderate": return "Your rivalry with " + arc.npcName + " is becoming the talk of the house.";
                    case "Intense": return "Your bitter feud with " + arc.npcName + " has reached a boiling point.";
                    case "Legendary": return "Your legendary rivalry with " + arc.npcName + " defines this season.";
                }
            }
            else if (arc.arcType == "friendship")
            {
                switch (label)
                {
                    case "Mild": return "A bond is forming between you and " + arc.npcName + ".";
                    case "Moderate": return "Your friendship with " + arc.npcName + " is becoming a powerful force in the house.";
                    case "Intense": return "You and " + arc.npcName + " have forged an unbreakable bond.";
                    case "Legendary": return "Your legendary alliance with " + arc.npcName + " will go down in Big Brother history.";
                }
            }
            return "";
        }
    }
}
