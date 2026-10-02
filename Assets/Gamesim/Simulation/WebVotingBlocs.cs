using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Gamesim.Simulation
{
    // Transient Json.NET DTOs, never Unity-serialized fields or native save roots.
    public sealed class WebBlocActor
    {
        public string id, name, status;
        public bool isHoH;
        public List<string> traits = new List<string>();
    }
    public sealed class WebBlocAlliance
    {
        public string id, name, status, founderId;
        public double? stability;
        public List<string> members = new List<string>();
        /// <summary>
        /// A call made in this alliance (STRATEGY-LOOP-PLAN.md §3): the caller, the nominee they
        /// named, and who decided at the time to follow. Native only; the source never had it.
        /// </summary>
        public string calledTargetId, callerId;
        public List<string> followedIds = new List<string>();
    }
    public sealed class WebBlocScore { public string fromId, toId; public double score; }
    public sealed class WebBlocGrudge
    { public string holderId, targetId; public double severity; public bool forgiven; }
    public sealed class WebBlocSnapshot
    {
        public int week;
        public List<WebBlocActor> actors = new List<WebBlocActor>();
        public List<WebBlocAlliance> alliances = new List<WebBlocAlliance>();
        public List<string> nomineeIds = new List<string>();
        public List<WebBlocScore> relationships = new List<WebBlocScore>();
        public List<WebBlocGrudge> grudges = new List<WebBlocGrudge>();
    }
    public sealed class WebBlocDirective
    {
        public string voterId, voterName, directive, targetNomineeId, targetNomineeName, allianceName, allianceId;
        public WebBlocDirective Clone() => (WebBlocDirective)MemberwiseClone();
        public WebVoteBlocDirective ForVote() => new WebVoteBlocDirective
        { directive = directive, targetNomineeId = targetNomineeId, allianceId = allianceId };
    }
    public sealed class WebBlocResult
    {
        public string allianceId, allianceName, shotCallerId, shotCallerName, targetNomineeId, targetNomineeName, reasoning;
        public List<string> compliantVoters = new List<string>(), defectors = new List<string>();
        public List<WebBlocDirective> directives = new List<WebBlocDirective>();
    }
    public sealed class WebBlocRound
    {
        public uint seed;
        public int randomDraws;
        public List<WebBlocResult> results = new List<WebBlocResult>();
        public List<WebBlocDirective> directives = new List<WebBlocDirective>();
    }

    /// <summary>
    /// Source voting-bloc-system.ts with finite canonical-ID inputs and detached
    /// outputs. This is pressure, not guaranteed votes or permission to expose
    /// private evidence. Source founderId is deliberately not inferred from founder.
    /// </summary>
    public static class WebVotingBlocs
    {
        public static List<WebBlocResult> Resolve(WebBlocSnapshot snapshot,
            Func<string, string, double> getTrust, Func<double> nextRoll)
        {
            Validate(snapshot);
            if (snapshot.nomineeIds.Count != 2) return new List<WebBlocResult>();
            if (getTrust == null) throw new ArgumentNullException(nameof(getTrust));
            if (nextRoll == null) throw new ArgumentNullException(nameof(nextRoll));
            double Trust(string from, string to)
            {
                double value = getTrust(from, to); Finite(value, "trust"); return value;
            }
            var active = new HashSet<string>(snapshot.actors.Where(a => a.status == "Active").Select(a => a.id));
            var hoh = snapshot.actors.FirstOrDefault(a => a.isHoH)?.id;
            var nominees = snapshot.nomineeIds.Select(id => snapshot.actors.Single(a => a.id == id)).ToArray();
            var results = new List<WebBlocResult>();
            foreach (var alliance in snapshot.alliances)
            {
                // Source truthiness: empty/missing status is accepted, defined non-Active skipped.
                if (!string.IsNullOrEmpty(alliance.status) && alliance.status != "Active") continue;
                var eligible = alliance.members.Where(id => active.Contains(id) && !snapshot.nomineeIds.Contains(id) && id != hoh).ToList();
                // A call already made stands: the caller and the target are the call's, and who
                // follows was decided when it was made. Otherwise the source's own round.
                bool called = !string.IsNullOrEmpty(alliance.calledTargetId) && snapshot.nomineeIds.Contains(alliance.calledTargetId)
                    && !string.IsNullOrEmpty(alliance.callerId) && alliance.members.Contains(alliance.callerId);
                if (eligible.Count < (called ? 1 : 2)) continue;
                string caller = called ? alliance.callerId
                    : !string.IsNullOrEmpty(alliance.founderId) && eligible.Contains(alliance.founderId) ? alliance.founderId : null;
                if (caller == null)
                {
                    double best = double.NegativeInfinity;
                    foreach (var candidate in eligible)
                    {
                        double average = eligible.Where(id => id != candidate).Sum(id => Trust(candidate, id)) / Math.Max(1, eligible.Count - 1);
                        Finite(average, "average trust");
                        if (average > best) { best = average; caller = candidate; }
                    }
                }
                if (caller == null) continue;
                var target = called ? nominees.First(n => n.id == alliance.calledTargetId)
                    : Score(snapshot, caller, nominees[0].id) <= Score(snapshot, caller, nominees[1].id) ? nominees[0] : nominees[1];
                var result = new WebBlocResult
                {
                    allianceId = alliance.id, allianceName = alliance.name, shotCallerId = caller,
                    shotCallerName = snapshot.actors.FirstOrDefault(a => a.id == caller)?.name ?? "Unknown",
                    targetNomineeId = target.id, targetNomineeName = target.name
                };
                foreach (var voterId in eligible)
                {
                    var actor = snapshot.actors.Single(a => a.id == voterId);
                    bool complies = called ? voterId == caller || alliance.followedIds.Contains(voterId)
                        : Complies(Loyalty(snapshot, alliance, voterId, caller, target.id, Trust), actor.traits, nextRoll);
                    (complies ? result.compliantVoters : result.defectors).Add(voterId);
                    result.directives.Add(new WebBlocDirective
                    {
                        voterId = voterId, voterName = actor.name, directive = complies ? "bloc_vote" : "free_vote",
                        targetNomineeId = complies ? target.id : null, targetNomineeName = complies ? target.name : null,
                        allianceName = alliance.name, allianceId = alliance.id
                    });
                }
                result.reasoning = result.shotCallerName + " directed " + alliance.name + " to vote out " + target.name + ". "
                    + result.compliantVoters.Count + " complied, " + result.defectors.Count + " defected.";
                results.Add(result);
            }
            return results;
        }

        /// <summary>
        /// The source's loyalty of a member to the caller on this target: trust in the caller,
        /// the pact's stability (fifty where none is known), a grudge against the target, and
        /// liking the target. The call lever asks it at call time with the player as the caller.
        /// </summary>
        public static double Loyalty(WebBlocSnapshot snapshot, WebBlocAlliance alliance, string voterId, string callerId, string targetId,
            Func<string, string, double> getTrust)
        {
            var grudge = snapshot.grudges.FirstOrDefault(g => g.holderId == voterId && g.targetId == targetId && !g.forgiven);
            double personal = Score(snapshot, voterId, targetId);
            double loyalty = getTrust(voterId, callerId) * .4 + (alliance.stability ?? 50) * .3
                - (grudge?.severity ?? 0) * .2 - (personal > 0 ? personal * .1 : 0);
            Finite(loyalty, "loyalty");
            return loyalty;
        }

        /// <summary>Whether a member of that loyalty follows: above fifty always, thirty to fifty on a roll (harder for the Sneaky and the Strategic), below never.</summary>
        public static bool Complies(double loyalty, IList<string> traits, Func<double> nextRoll)
        {
            if (loyalty > 50) return true;
            if (loyalty < 30) return false;
            double roll = nextRoll(); Finite(roll, "roll");
            if (roll < 0 || roll >= 1) throw new ArgumentOutOfRangeException(nameof(nextRoll), "Roll must be in [0,1).");
            bool sneaky = traits.Contains("Sneaky") || traits.Contains("Strategic");
            return roll > (sneaky ? .65 : .5);
        }

        /// <summary>The round's trust proxy: fifty plus half the raw score, clamped to 0-100.</summary>
        public static Func<string, string, double> ProxyTrust(WebBlocSnapshot snapshot) =>
            (from, to) => Math.Max(0, Math.Min(100, 50 + Score(snapshot, from, to) * .5));

        public static List<WebBlocDirective> Flatten(IEnumerable<WebBlocResult> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            var seen = new HashSet<string>(StringComparer.Ordinal); var flattened = new List<WebBlocDirective>();
            foreach (var result in results)
            {
                if (result?.directives == null) throw new ArgumentException("Invalid bloc result.");
                foreach (var directive in result.directives)
                {
                    if (directive == null || string.IsNullOrEmpty(directive.voterId)) throw new ArgumentException("Invalid bloc directive.");
                    if (seen.Add(directive.voterId)) flattened.Add(directive.Clone());
                }
            }
            return flattened;
        }

        public static uint RoundSeed(WebBlocSnapshot snapshot)
        {
            Validate(snapshot);
            // JS default string sorting and FNV1a charCodeAt both operate on UTF-16.
            var key = new[] { snapshot.week.ToString(CultureInfo.InvariantCulture) }
                .Concat(snapshot.actors.Where(a => a.status == "Active").Select(a => a.id).OrderBy(id => id, StringComparer.Ordinal))
                .Concat(snapshot.nomineeIds.OrderBy(id => id, StringComparer.Ordinal));
            return SeededRandom.HashSeed(string.Join(":", key));
        }

        /// <summary>Source eviction-vote-round.ts: separate seeded stream, raw-score proxy trust.</summary>
        public static WebBlocRound ResolveRound(WebBlocSnapshot snapshot)
        {
            var result = new WebBlocRound { seed = RoundSeed(snapshot) };
            var random = new SeededRandom(result.seed);
            result.results = Resolve(snapshot, ProxyTrust(snapshot),
                () => { result.randomDraws++; return random.NextDouble(); });
            result.directives = Flatten(result.results);
            return result;
        }

        /// <summary>
        /// Supported native scenario adapter: copies actual pact membership, no invented founder.
        /// Before the levers, no stability and no grudges either, as it always was. Under them
        /// (STRATEGY-LOOP-PLAN.md §3) a pact's stability is its members' warmth for each other,
        /// the story's grudges are the round's, and a call the player made this week stands.
        /// Under the commitment rules (ACTIONS-DEALS-ALLIANCES-PLAN C3) a pact of the player's leaves
        /// out the members whose own commitment to the player has lapsed: they no longer answer to its
        /// bloc (<see cref="Allegiance.Following"/>). And (C5) the pact's founder is its first member,
        /// as the source's <c>founderId</c> is, invented by nothing: <see cref="Resolve"/> makes them the
        /// caller whenever they are among the members who vote, as the source does, and otherwise the
        /// round picks one as it always did. Without the rules no founder, as before.
        /// No ballots, phase state, sequence, revision or persisted RNG are changed.
        /// </summary>
        public static WebBlocSnapshot FromNative(EpisodeState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            bool founders = EpisodeEngine.CommitmentRulesOn(state);
            return new WebBlocSnapshot
            {
                week = state.week,
                actors = state.contestants.Select(a => new WebBlocActor
                { id = a.id, name = a.name, status = a.status == ContestantStatus.Active ? "Active" : a.status.ToString(),
                    isHoH = a.id == state.hohId, traits = new List<string>(a.traits) }).ToList(),
                alliances = state.alliances.Select(a =>
                {
                    var call = EpisodeEngine.CallThisWeek(state, a.id);
                    return new WebBlocAlliance
                    {
                        id = a.id, name = a.name, status = a.active ? "Active" : "Broken", members = Allegiance.Following(state, a),
                        founderId = founders ? EpisodeEngine.Founder(a) : null,
                        stability = EpisodeEngine.LeverRulesOn(state) ? Stability(state, a) : (double?)null,
                        calledTargetId = call?.targetId, callerId = call?.callerId,
                        followedIds = call != null ? new List<string>(call.followed) : new List<string>(),
                    };
                }).ToList(),
                nomineeIds = new List<string>(state.nominees),
                relationships = state.relationships.Select(r => new WebBlocScore { fromId = r.fromId, toId = r.toId, score = r.score }).ToList(),
                grudges = EpisodeEngine.LeverRulesOn(state) && state.story != null
                    ? state.story.grudges.Select(g => new WebBlocGrudge { holderId = g.holderId, targetId = g.targetId, severity = g.severity }).ToList()
                    : new List<WebBlocGrudge>()
            };
        }

        /// <summary>A pact's stability under the levers: fifty plus half its active members' average warmth for each other, 0-100.</summary>
        public static double Stability(EpisodeState state, AllianceState alliance)
        {
            var members = alliance.members.Where(id => state.Find(id)?.status == ContestantStatus.Active).ToList();
            if (members.Count < 2) return 50;
            double total = 0; int pairs = 0;
            foreach (var from in members) foreach (var to in members.Where(id => id != from)) { total += state.Score(from, to); pairs++; }
            return Math.Max(0, Math.Min(100, 50 + total / pairs * .5));
        }

        public static double Score(WebBlocSnapshot snapshot, string from, string to) =>
            snapshot.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to)?.score ?? 0;

        private static void Validate(WebBlocSnapshot s)
        {
            if (s == null || s.week < 0 || s.week > 10000 || s.actors == null || s.actors.Count > 64 ||
                s.alliances == null || s.alliances.Count > 1000 || s.nomineeIds == null || s.nomineeIds.Count > 64 ||
                s.relationships == null || s.relationships.Count > 4096 || s.grudges == null || s.grudges.Count > 4096)
                throw new ArgumentException("Unsupported bloc snapshot bounds.");
            foreach (var actor in s.actors)
            {
                if (actor == null || actor.traits == null || actor.traits.Count > 64) throw new ArgumentException("Invalid actor.");
                Text(actor.id); Text(actor.name); Text(actor.status);
                foreach (var trait in actor.traits) Text(trait);
            }
            if (s.actors.Select(a => a.id).Distinct(StringComparer.Ordinal).Count() != s.actors.Count) throw new ArgumentException("Duplicate actor.");
            foreach (var id in s.nomineeIds) if (!s.actors.Any(a => a.id == id)) throw new ArgumentException("Unknown nominee.");
            if (s.nomineeIds.Distinct(StringComparer.Ordinal).Count() != s.nomineeIds.Count) throw new ArgumentException("Duplicate nominee.");
            foreach (var alliance in s.alliances)
            {
                if (alliance == null || alliance.members == null || alliance.members.Count > 64) throw new ArgumentException("Invalid alliance.");
                Text(alliance.id); Text(alliance.name);
                foreach (var id in alliance.members) Text(id);
                if (alliance.members.Distinct(StringComparer.Ordinal).Count() != alliance.members.Count) throw new ArgumentException("Duplicate member.");
                if (alliance.status != null && alliance.status.Length > 128) throw new ArgumentException("Invalid status.");
                if (!string.IsNullOrEmpty(alliance.founderId)) Text(alliance.founderId);
                if (alliance.stability.HasValue) Finite(alliance.stability.Value, "stability");
            }
            foreach (var edge in s.relationships)
            { if (edge == null) throw new ArgumentException("Null score."); Text(edge.fromId); Text(edge.toId); Finite(edge.score, "score"); }
            if (s.relationships.GroupBy(r => new { r.fromId, r.toId }).Any(g => g.Count() > 1)) throw new ArgumentException("Duplicate directed score.");
            foreach (var grudge in s.grudges)
            { if (grudge == null) throw new ArgumentException("Null grudge."); Text(grudge.holderId); Text(grudge.targetId); Finite(grudge.severity, "severity"); }
        }
        private static void Text(string value)
        { if (string.IsNullOrEmpty(value) || value.Length > 256) throw new ArgumentException("Invalid bloc text or identifier."); }
        private static void Finite(double value, string field)
        { if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(field, "Expected finite value."); }
    }
}
