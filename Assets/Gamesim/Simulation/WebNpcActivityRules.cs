using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    // Transient supplied-context DTOs, not a save schema or Unity placement contract.
    public sealed class WebNpcActivityPoint
    {
        public string id, label, room, activity;
        public int capacity;
        public List<WebNpcMotiveAmount> satisfaction = new List<WebNpcMotiveAmount>();
    }
    public sealed class WebNpcRoomPresence
    { public string id; public bool sameRoom, allied; public double relationship; }
    public sealed class WebNpcPointContext
    {
        public string npcId;
        public List<WebNpcRoomPresence> actors = new List<WebNpcRoomPresence>();
        // Null means the corresponding optional source Map was not supplied.
        public int? favoriteVisits, occupantCount;
    }
    public sealed class WebNpcScoredPoint
    { public WebNpcActivityPoint point; public double score; }
    public sealed class WebNpcSeekCandidate
    { public string id, state; public double distance, relationship; public bool allied; }
    public sealed class WebNpcSeekChoice
    { public string id, intent; public double score; }
    public sealed class WebNpcChainChoice
    { public string activity, carryItem; }
    public sealed class WebNpcActivityPhase
    {
        public string activity, startGesture, endGesture, pickUpItem;
        public int startDuration, endDuration;
        public bool putDownItem;
    }
    public sealed class WebNpcChainMetadata
    { public string id, carryItem; public List<string> steps = new List<string>(); }
    public sealed class WebNpcCarryChain
    { public string activity, item, targetActivity, targetRoom; }

    /// <summary>
    /// Original useNPCAutonomy.ts/activityChains.ts leaf arithmetic. Distance is XZ
    /// distance; sameRoom is supplied spatial evidence, NOT point.room string equality.
    /// No scheduler, Unity world coordinates, occupancy owner, save or scene mutation.
    /// </summary>
    public static class WebNpcActivityRules
    {
        private static readonly Dictionary<string, Dictionary<string, double>> RoomWeights =
            new Dictionary<string, Dictionary<string, double>>(StringComparer.Ordinal)
            {
                ["Social"] = Rooms("living",3,"kitchen",3,"nomination",2,"game",1),
                ["Charming"] = Rooms("living",3,"kitchen",2,"nomination",2,"backyard",2),
                ["Competitive"] = Rooms("game",4,"backyard",2,"living",1),
                ["Introverted"] = Rooms("bedroom",3,"bathroom",2,"hoh",2,"hallway",1),
                ["Strategic"] = Rooms("hoh",2,"nomination",3,"kitchen",2,"living",1),
                ["Emotional"] = Rooms("bedroom",2,"living",2,"backyard",2,"bathroom",1),
                ["Manipulative"] = Rooms("nomination",3,"living",2,"kitchen",2),
                ["Funny"] = Rooms("living",3,"kitchen",2,"game",2,"backyard",2),
                ["Analytical"] = Rooms("hoh",2,"game",2,"bedroom",2,"kitchen",1),
                ["Loyal"] = Rooms("living",3,"kitchen",2,"nomination",1)
            };

        public static double TraitRoomBonus(IReadOnlyList<string> traits, string room)
        {
            double bonus = 0;
            foreach (string trait in traits ?? Array.Empty<string>())
                if (trait != null && room != null && RoomWeights.TryGetValue(trait, out var weights)
                    && weights.TryGetValue(room, out double weight)) bonus += weight;
            return bonus;
        }

        public static double ScorePoint(WebNpcMotiveState motives, WebNpcActivityPoint point, double distance,
            IReadOnlyList<string> traits, WebNpcPointContext context = null)
        {
            ValidateMotives(motives); ValidatePoint(point); Distance(distance);
            double score = 0;
            foreach (string motive in WebNpcMotives.MotiveNames)
                score += WebNpcMotives.GetUrgency(motives.Get(motive))
                    * (point.satisfaction.FirstOrDefault(entry => entry.motive == motive)?.amount ?? 0);
            score /= 1 + distance * .1;
            score *= 1 + TraitRoomBonus(traits, point.room) * .2;
            if (context != null)
            {
                if (context.npcId == null) throw new ArgumentNullException(nameof(context.npcId));
                var actorIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var actor in context.actors ?? throw new ArgumentNullException(nameof(context.actors)))
                {
                    if (actor == null) throw new ArgumentException("Null room actor.");
                    if (actor.id == null || !actorIds.Add(actor.id))
                        throw new ArgumentException("Room actors must have distinct non-null string IDs.");
                    Finite(actor.relationship);
                    if (actor.id == context.npcId || !actor.sameRoom) continue;
                    if (actor.allied) score += 3;
                    if (actor.relationship < -30) score += -4;
                }
                if (context.favoriteVisits.HasValue) score += Math.Min(context.favoriteVisits.Value, 3);
                if (context.occupantCount.HasValue && point.capacity > 1)
                {
                    int count = context.occupantCount.Value;
                    if (motives.social < 30 && count > 0) score += 2d * count;
                    else if (motives.social > 50 && count > 0) score -= 2d * count;
                    if (count == 0) score += 2;
                }
            }
            if (point.activity == "stand") score -= 3;
            if (point.room == "backyard") score -= 5;
            return score;
        }

        /// <summary>Matches source Array.sort side effect: sorts the ENTIRE caller list stably in place before one draw.</summary>
        public static WebNpcActivityPoint PickFromTopScoredInPlace(IList<WebNpcScoredPoint> scored,
            Func<double> nextRoll, int topN = 3)
        {
            if (scored == null) throw new ArgumentNullException(nameof(scored));
            if (topN < 1) throw new ArgumentOutOfRangeException(nameof(topN));
            foreach (var entry in scored)
            { if (entry == null || entry.point == null) throw new ArgumentException("Null scored point."); Finite(entry.score); }
            if (scored.Count == 0) return null;
            var sorted = scored.OrderByDescending(entry => entry.score).ToArray();
            for (int index = 0; index < sorted.Length; index++) scored[index] = sorted[index];
            int take = Math.Min(topN, scored.Count);
            double total = 0;
            for (int index = 0; index < take; index++) total += Math.Max(scored[index].score, .01);
            double roll = Roll(nextRoll) * total;
            for (int index = 0; index < take; index++)
            {
                roll -= Math.Max(scored[index].score, .01);
                if (roll <= 0) return scored[index].point;
            }
            return scored[0].point;
        }

        public static double ScoreSeekTarget(WebNpcMotiveState motives, double distance, double relationship,
            bool allied, IReadOnlyList<string> traits = null, bool isPlayer = false, string playerPersonaLabel = null)
        {
            ValidateMotives(motives); Distance(distance); Finite(relationship);
            bool Has(string trait) => traits != null && traits.Contains(trait);
            double score = WebNpcMotives.GetUrgency(motives.social) * (10 + Math.Abs(relationship) * .3);
            if (allied) score *= Has("Loyal") ? 2 : 1.4;
            if (relationship < -20) score *= 1.2;
            if (Has("Social") || Has("Charming") || Has("Funny")) score *= 1.5;
            if (Has("Introverted")) score *= .6;
            if ((Has("Manipulative") || Has("Deceptive")) && relationship < 0) score *= 1.4;
            if (Has("Competitive") && relationship < -10) score *= 1.3;
            if (isPlayer && playerPersonaLabel != null)
            {
                if (playerPersonaLabel == "Social Butterfly") score += 3;
                else if (playerPersonaLabel == "Calculated") score += 1;
                else if (playerPersonaLabel == "Ruthless") score -= 2;
            }
            return score / (1 + distance * .15);
        }

        public static bool IsSeekCandidate(string npcId, string playerId, string candidateId, string state)
        {
            if (npcId == null) throw new ArgumentNullException(nameof(npcId));
            if (candidateId == null) throw new ArgumentNullException(nameof(candidateId));
            return candidateId != npcId && candidateId != playerId && (state == "idle" || state == "activity");
        }

        public static string SeekIntent(double relationship, bool allied, IReadOnlyList<string> traits)
        {
            Finite(relationship);
            if (allied) return "ally_check";
            if (relationship < -20) return "confront";
            if (relationship > 20 && traits != null
                && (traits.Contains("Sneaky") || traits.Contains("Gossipy") || traits.Contains("Manipulative"))) return "gossip_share";
            return "get_to_know";
        }

        /// <summary>Exact source seek-selection block AFTER furniture scoring; caller supplies that result and elapsed time.</summary>
        public static WebNpcSeekChoice SelectSeekTarget(WebNpcMotiveState motives, string npcId, string playerId,
            IReadOnlyList<WebNpcSeekCandidate> candidates, IReadOnlyList<string> traits,
            double bestFurnitureScore, double millisecondsSinceLastChange, bool hasSeekTarget,
            bool hasRelationshipMap, Func<double> nextRoll, string playerPersonaLabel = null)
        {
            ValidateMotives(motives); Finite(bestFurnitureScore); Finite(millisecondsSinceLastChange);
            if (npcId == null) throw new ArgumentNullException(nameof(npcId));
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            var candidateIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var candidate in candidates)
            {
                if (candidate == null) throw new ArgumentException("Null seek candidate.");
                if (candidate.id == null || !candidateIds.Add(candidate.id))
                    throw new ArgumentException("Seek candidates must have distinct non-null string IDs.");
                Distance(candidate.distance); Finite(candidate.relationship);
            }
            bool bored = !hasSeekTarget && millisecondsSinceLastChange > 30000;
            // Source evaluates the chance BEFORE checking whether relationshipMap exists.
            if (!(Roll(nextRoll) < (bored ? .7 : .5)) || !hasRelationshipMap) return null;
            WebNpcSeekChoice choice = null;
            foreach (var candidate in candidates)
            {
                if (!IsSeekCandidate(npcId, playerId, candidate.id, candidate.state)) continue;
                double score = ScoreSeekTarget(motives, candidate.distance, candidate.relationship,
                    candidate.allied, traits, false, playerPersonaLabel); // Player was excluded above.
                if (score > bestFurnitureScore * .8 && (choice == null || score > choice.score))
                    choice = new WebNpcSeekChoice { id = candidate.id, score = score,
                        intent = SeekIntent(candidate.relationship, candidate.allied, traits) };
            }
            return choice != null && choice.score > bestFurnitureScore * .6 ? choice : null;
        }

        public static WebNpcChainChoice NextChainActivity(string completedActivity, WebNpcMotiveState motives,
            Func<double> nextRoll, IReadOnlyList<string> traits = null)
        {
            ValidateMotives(motives);
            if (Roll(nextRoll) > .4) return null;
            // Source declaration order, including intentionally shadowed later templates.
            if (completedActivity == "sleep" && motives.rest > 80) return Chain("groom");
            if (completedActivity == "swim" && motives.fun > 60) return Chain("groom");
            if (completedActivity == "stand" && motives.social < 50) return Chain("sit");
            if (completedActivity == "game" && motives.fun < 60) return Chain("sit");
            if (completedActivity == "lounge" && motives.rest < 40) return Chain("groom");
            if (completedActivity == "cook" && motives.energy > 50) return Chain("sit", "plate");
            if (completedActivity == "cook" && motives.energy > 60) return Chain("sit", "plate");
            if (completedActivity == "swim" && motives.hygiene < 60) return Chain("groom", "towel");
            return null;
        }

        public static string NextInteractionPhase(string phase) => phase == "approaching" ? "starting"
            : phase == "starting" ? "active" : phase == "active" ? "ending" : null;
        public static int PhaseDuration(string activity, string phase)
        {
            if (phase != "starting" && phase != "ending") return 0;
            var entry = Phase(activity);
            return phase == "starting" ? entry.startDuration : entry.endDuration;
        }
        public static string PhaseGesture(string activity, string phase)
        {
            if (phase != "starting" && phase != "ending") return null;
            var entry = Phase(activity); return phase == "starting" ? entry.startGesture : entry.endGesture;
        }
        public static string CarryItemOnStart(string activity) => Phase(activity).pickUpItem;
        public static bool ShouldDropItemOnEnd(string activity) => Phase(activity).putDownItem;
        private static WebNpcActivityPhase Phase(string activity) => WebNpcActivityCatalog.Phases
            .SingleOrDefault(entry => entry.activity == activity) ?? throw new ArgumentException("Unknown source activity.", nameof(activity));

        private static WebNpcChainChoice Chain(string activity, string item = null) => new WebNpcChainChoice { activity = activity, carryItem = item };
        private static Dictionary<string, double> Rooms(params object[] values)
        {
            var result = new Dictionary<string, double>(StringComparer.Ordinal);
            for (int index = 0; index < values.Length; index += 2) result.Add((string)values[index], Convert.ToDouble(values[index + 1]));
            return result;
        }
        private static double Roll(Func<double> next)
        {
            if (next == null) throw new ArgumentNullException(nameof(next));
            double value = next(); Finite(value);
            if (value < 0 || value >= 1) throw new ArgumentOutOfRangeException(nameof(next));
            return value;
        }
        private static void Finite(double value)
        { if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value)); }
        private static void Distance(double value)
        { Finite(value); if (value < 0) throw new ArgumentOutOfRangeException(nameof(value)); }
        private static void ValidateMotives(WebNpcMotiveState state)
        { if (state == null) throw new ArgumentNullException(nameof(state)); foreach (string name in WebNpcMotives.MotiveNames) Finite(state.Get(name)); }
        private static void ValidatePoint(WebNpcActivityPoint point)
        {
            if (point == null || point.satisfaction == null) throw new ArgumentNullException(nameof(point));
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in point.satisfaction)
            {
                if (entry == null || !WebNpcMotives.MotiveNames.Contains(entry.motive) || !seen.Add(entry.motive))
                    throw new ArgumentException("Satisfaction must contain unique known object keys.");
                Finite(entry.amount);
            }
        }
    }
}
