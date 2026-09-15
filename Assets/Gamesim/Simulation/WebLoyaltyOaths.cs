using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Gamesim.Simulation
{
    [Serializable] public sealed class WebOathRecord
    {
        public string playerId, targetId;
        public int week;
        public long timestamp;
    }
    [Serializable] public sealed class WebOathActor
    {
        public string id, name, status;
        public bool isPlayer;
    }
    [Serializable] public sealed class WebOathEdge
    {
        public string fromId, toId;
        public double score;
    }
    [Serializable] public sealed class WebOathSnapshot
    {
        public int week;
        public List<WebOathActor> actors = new List<WebOathActor>();
        public List<WebOathRecord> oaths = new List<WebOathRecord>();
        public List<WebOathEdge> relationships = new List<WebOathEdge>();
    }
    [Serializable] public sealed class WebOathArcChange
    {
        public string npcId, npcName, reason;
        public int week;
        public double delta;
    }
    [Serializable] public sealed class WebOathRipple
    {
        public string fromId, toId, victimId, note;
        public double delta, score;
        public int lastInteractionWeek;
        public bool createVictimEdge, createActorEdge;
    }
    [Serializable] public sealed class WebOathBreakPlan
    {
        public bool broken;
        public int matchedIndex = -1;
        public string actorId, victimId, factType, logDescription;
        public List<int> remainingOathIndices = new List<int>();
        public List<string> neutralWitnessIds = new List<string>();
        public List<WebOathArcChange> arcChanges = new List<WebOathArcChange>();
        public List<WebOathRipple> ripples = new List<WebOathRipple>();
    }
    [Serializable] public sealed class WebOathCampaignProposal
    {
        public bool available, allianceOpportunity;
        public double relationshipDelta;
        public string memoryEntry;
        // The source cutscene never creates state.loyaltyOaths.
        public bool createsStructuredOath;
    }

    /// <summary>
    /// Pure, bounded OATH-ONLY consequence plans. These do not authorize an
    /// action, create consent, change state, settle promises/deals, publish to a
    /// bus, or apply downstream grudge/jury facts. Normal nomination -8 arcs,
    /// initial mood/stress changes and accounting remain with their owner.
    /// </summary>
    public static class WebLoyaltyOaths
    {
        /// <summary>Independent required samples, in stored active cast order.</summary>
        public static string[] NominationNeutralWitnesses(WebOathSnapshot snapshot, string hohId, string nomineeId)
        {
            var prepared = Inspect(snapshot, hohId, nomineeId);
            return prepared.matchedIndex < 0 ? Array.Empty<string>() : prepared.witnesses
                .Where(w => Score(snapshot, w.id, nomineeId) > -20 && Score(snapshot, w.id, nomineeId) < 20)
                .Select(w => w.id).ToArray();
        }

        /// <summary>
        /// accepted-consequences/nomination-direct.ts oath sub-operation. Supply
        /// exactly one [0,1) sample for each neutral witness from the descriptor.
        /// Both initial and replacement newcomer oath breaches use this policy.
        /// </summary>
        public static WebOathBreakPlan Nomination(WebOathSnapshot snapshot, string hohId, string nomineeId,
            IReadOnlyList<double> neutralRolls)
        {
            var prepared = Inspect(snapshot, hohId, nomineeId);
            var required = NominationNeutralWitnesses(snapshot, hohId, nomineeId);
            if (neutralRolls == null || neutralRolls.Count != required.Length)
                throw new ArgumentException("Supply exactly one roll per neutral nomination witness.", nameof(neutralRolls));
            foreach (var roll in neutralRolls) RequireRoll(roll);
            int index = 0;
            return Resolve(snapshot, prepared, false, _ => neutralRolls[index++]);
        }

        /// <summary>
        /// eviction-reducer.ts SET_EVICTION_VOTE oath sub-operation. Neutral
        /// witnesses use their own source-hashed RNG, never the season stream.
        /// </summary>
        public static WebOathBreakPlan EvictionVote(WebOathSnapshot snapshot, string voterId, string nomineeId)
        {
            var prepared = Inspect(snapshot, voterId, nomineeId);
            return Resolve(snapshot, prepared, true, witnessId => EvictionWitnessRoll(snapshot.week, voterId, nomineeId, witnessId));
        }

        public static double EvictionWitnessRoll(int week, string voterId, string nomineeId, string witnessId)
        {
            RequireWeek(week); RequireText(voterId, "voterId"); RequireText(nomineeId, "nomineeId"); RequireText(witnessId, "witnessId");
            var key = week.ToString(CultureInfo.InvariantCulture) + ":" + voterId + ":" + nomineeId + ":" + witnessId + ":loyalty-oath";
            return new SeededRandom(SeededRandom.HashSeed(key)).NextDouble();
        }

        /// <summary>
        /// campaign-cutscene-generator.ts personal_chat choice. A favorable
        /// conversation is not a persisted mutual oath or guaranteed NPC pact.
        /// </summary>
        public static WebOathCampaignProposal CampaignProposal(IReadOnlyList<string> playerTraits,
            IReadOnlyList<string> npcTraits, double relationshipScore, string playerName)
        {
            if (playerTraits == null || npcTraits == null) throw new ArgumentNullException("traits");
            RequireFiniteScore(relationshipScore); RequireText(playerName, nameof(playerName));
            bool available = (playerTraits.Contains("Emotional") || playerTraits.Contains("Loyal")) && relationshipScore > 30;
            if (!available) return new WebOathCampaignProposal();
            bool guarded = npcTraits.Contains("Introverted") || npcTraits.Contains("Stubborn") || npcTraits.Contains("Deceptive");
            return new WebOathCampaignProposal
            {
                available = true, relationshipDelta = guarded ? -3 : 12, allianceOpportunity = !guarded,
                memoryEntry = guarded ? playerName + " proposed a loyalty oath. Too much, too soon."
                    : playerName + " and I made a loyalty oath. This is real.",
                createsStructuredOath = false
            };
        }

        private sealed class Prepared
        {
            public WebOathActor actor, victim;
            public int matchedIndex;
            public WebOathActor[] witnesses;
        }

        private static Prepared Inspect(WebOathSnapshot snapshot, string actorId, string victimId)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            RequireWeek(snapshot.week); RequireText(actorId, nameof(actorId)); RequireText(victimId, nameof(victimId));
            if (actorId == victimId) throw new ArgumentException("An oath breach requires distinct actors.");
            if (snapshot.actors == null || snapshot.actors.Count < 2 || snapshot.actors.Count > 64 ||
                snapshot.actors.Any(actor => actor == null) || snapshot.actors.Count(actor => actor.isPlayer) > 1 ||
                snapshot.actors.Select(actor => actor.id).Distinct(StringComparer.Ordinal).Count() != snapshot.actors.Count)
                throw new ArgumentException("Unsupported cast representation.");
            foreach (var actor in snapshot.actors)
            {
                RequireText(actor.id, "actor.id"); RequireText(actor.name, "actor.name");
                if (!new[] { "Active", "Evicted", "Jury", "Winner", "Runner-Up" }.Contains(actor.status))
                    throw new ArgumentException("Unsupported actor status.");
            }
            var first = snapshot.actors.SingleOrDefault(actor => actor.id == actorId);
            var second = snapshot.actors.SingleOrDefault(actor => actor.id == victimId);
            if (first?.status != "Active" || second?.status != "Active")
                throw new ArgumentException("Supply canonical active action actors after action authorization.");
            if (snapshot.oaths == null || snapshot.oaths.Count > 512 || snapshot.oaths.Any(oath => oath == null))
                throw new ArgumentException("Unsupported oath representation.");
            foreach (var oath in snapshot.oaths)
            {
                RequireText(oath.playerId, "oath.playerId"); RequireText(oath.targetId, "oath.targetId");
                if (oath.week < 0 || oath.week > snapshot.week || oath.timestamp < 0 || oath.timestamp > 9007199254740991L)
                    throw new ArgumentException("Unsupported oath metadata.");
            }
            if (snapshot.relationships == null || snapshot.relationships.Count > 4096 || snapshot.relationships.Any(edge => edge == null) ||
                snapshot.relationships.GroupBy(edge => new { edge.fromId, edge.toId }).Any(group => group.Count() > 1))
                throw new ArgumentException("Unsupported relationship representation.");
            foreach (var edge in snapshot.relationships)
            {
                RequireText(edge.fromId, "edge.fromId"); RequireText(edge.toId, "edge.toId"); RequireFiniteScore(edge.score);
            }
            int matched = snapshot.oaths.FindIndex(oath => (oath.playerId == actorId && oath.targetId == victimId) ||
                (oath.playerId == victimId && oath.targetId == actorId));
            return new Prepared
            {
                actor = first, victim = second, matchedIndex = matched,
                witnesses = snapshot.actors.Where(actor => actor.status == "Active" && actor.id != actorId && actor.id != victimId).ToArray()
            };
        }

        private static WebOathBreakPlan Resolve(WebOathSnapshot snapshot, Prepared p, bool eviction, Func<string, double> nextNeutral)
        {
            var plan = new WebOathBreakPlan { broken = p.matchedIndex >= 0, matchedIndex = p.matchedIndex,
                actorId = p.actor.id, victimId = p.victim.id };
            var selected = plan.broken ? snapshot.oaths[p.matchedIndex] : null;
            for (int index = 0; index < snapshot.oaths.Count; index++)
                if (!ReferenceEquals(snapshot.oaths[index], selected)) plan.remainingOathIndices.Add(index);
            if (!plan.broken) return plan;
            plan.factType = "loyalty_oath_broken";
            plan.logDescription = p.actor.name + " broke their loyalty oath to " + p.victim.name +
                (eviction ? " by voting to evict them!" : " by nominating them!");
            if (p.actor.isPlayer || p.victim.isPlayer)
            {
                var npc = p.actor.isPlayer ? p.victim : p.actor;
                AddArc(plan, npc, eviction ? -20 : -25, "Broke loyalty oath by " + (eviction ? "voting to evict " : "nominating ") +
                    (p.actor.isPlayer ? p.victim.name : "you") + " in week " + snapshot.week, snapshot.week);
            }
            foreach (var witness in p.witnesses)
            {
                var feeling = Score(snapshot, witness.id, p.victim.id);
                double delta;
                string note;
                if (feeling >= 20)
                {
                    delta = -15;
                    note = "Furious that " + p.actor.name + " betrayed their ally " + p.victim.name + " (week " + snapshot.week + ")";
                }
                else if (feeling <= -20)
                {
                    delta = 3;
                    note = "Respected " + p.actor.name + (eviction ? "'s ruthless vote against " : "'s ruthless betrayal of ") + p.victim.name +
                        " (week " + snapshot.week + ")";
                }
                else
                {
                    plan.neutralWitnessIds.Add(witness.id);
                    var roll = nextNeutral(witness.id); RequireRoll(roll);
                    delta = eviction ? -(Math.Floor(roll * 4) + 3) : -(Math.Floor(roll * 6) + 5);
                    note = (eviction ? "Learned " : "Witnessed ") + p.actor.name +
                        (eviction ? " broke a loyalty oath (week " : " break a loyalty oath (week ") + snapshot.week + ")";
                }
                plan.ripples.Add(new WebOathRipple
                {
                    fromId = witness.id, toId = p.actor.id, victimId = p.victim.id, delta = delta,
                    score = Math.Max(-100, Math.Min(100, Score(snapshot, witness.id, p.actor.id) + delta)),
                    note = note, lastInteractionWeek = snapshot.week,
                    createVictimEdge = !HasEdge(snapshot, witness.id, p.victim.id),
                    createActorEdge = !HasEdge(snapshot, witness.id, p.actor.id)
                });
                if (witness.isPlayer) AddArc(plan, p.actor, delta, note, snapshot.week);
            }
            return plan;
        }

        private static void AddArc(WebOathBreakPlan plan, WebOathActor actor, double delta, string reason, int week)
            => plan.arcChanges.Add(new WebOathArcChange { npcId = actor.id, npcName = actor.name, delta = delta, reason = reason, week = week });
        private static bool HasEdge(WebOathSnapshot snapshot, string from, string to)
            => snapshot.relationships.Any(edge => edge.fromId == from && edge.toId == to);
        private static double Score(WebOathSnapshot snapshot, string from, string to)
            => snapshot.relationships.FirstOrDefault(edge => edge.fromId == from && edge.toId == to)?.score ?? 0;
        private static void RequireWeek(int week) { if (week < 0 || week > 10000) throw new ArgumentOutOfRangeException(nameof(week)); }
        private static void RequireText(string value, string name)
        { if (string.IsNullOrEmpty(value) || value.Length > 4000) throw new ArgumentException("Missing or oversized " + name); }
        private static void RequireFiniteScore(double value)
        { if (double.IsNaN(value) || double.IsInfinity(value) || Math.Abs(value) > 100) throw new ArgumentOutOfRangeException(nameof(value)); }
        private static void RequireRoll(double value)
        { if (double.IsNaN(value) || value < 0 || value >= 1) throw new ArgumentOutOfRangeException(nameof(value), "A random sample must be in [0,1)."); }
    }
}

