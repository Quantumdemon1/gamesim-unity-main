using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    [Serializable] public sealed class WebVoteContestant
    {
        public string id, name;
        public bool isPlayer;
        public List<string> traits = new List<string>();
        public ContestantStats stats = new ContestantStats();
        public int hohWins, vetoWins;
    }

    [Serializable] public sealed class WebVoteAlliance
    {
        public string id, name, status;
        public List<string> members = new List<string>();
    }

    [Serializable] public sealed class WebVotePromise
    {
        public string fromId, toId, type, status;
    }

    // Explicit context types: absent native systems are not inferred from unrelated fields.
    [Serializable] public sealed class WebVoteDeal
    {
        public string id, proposerId, recipientId, type, status, targetHouseguestId;
    }

    [Serializable] public sealed class WebVoteRelationshipArc
    {
        public string npcId, npcName, arcType;
        public double intensity, escalationLevel;
    }

    [Serializable] public sealed class WebVoteBlocDirective
    {
        public string directive, targetNomineeId, allianceId;
    }

    [Serializable] public sealed class WebVoteState
    {
        public List<WebVoteContestant> allActive = new List<WebVoteContestant>();
        public List<RelationshipState> relationships = new List<RelationshipState>();
        public List<WebVoteAlliance> alliances = new List<WebVoteAlliance>();
        public List<WebVoteDeal> deals = new List<WebVoteDeal>();
        public List<WebVotePromise> promises = new List<WebVotePromise>();
        public List<WebVoteRelationshipArc> relationshipArcs = new List<WebVoteRelationshipArc>();
        public int activeCount;
    }

    [Serializable] public sealed class WebVoteOptions
    {
        public WebVoteState state = new WebVoteState();
        public WebVoteContestant voter;
        public List<WebVoteContestant> nominees = new List<WebVoteContestant>();
        public WebVoteBlocDirective blocDirective;
        public List<string> memories = new List<string>();
        public string playerPersonaLabel;
    }

    [Serializable] public sealed class WebVoteFactor
    {
        public string code, visibility;
        public double value;
        public List<string> evidenceIds = new List<string>();
    }

    [Serializable] public sealed class WebNomineeEvaluation
    {
        public string nomineeId;
        public double score;
        public List<WebVoteFactor> factors = new List<WebVoteFactor>();
    }

    [Serializable] public sealed class WebVoteEvaluation
    {
        public string voterId, selectedNomineeId, savedNomineeId, confidence;
        public double margin;
        public List<WebNomineeEvaluation> nomineeEvaluations = new List<WebNomineeEvaluation>();
        public List<string> publicReasonCodes = new List<string>();
        public List<string> privateReasonCodes = new List<string>();
    }

    /// <summary>
    /// Exact ten-factor evaluateEvictionVote/explainEvictionVote port at original default balance.
    /// This is the per-voter evaluator, not the separate voting-bloc/grudge round generator.
    /// </summary>
    public static class WebEvictionVoting
    {
        private sealed class Weights
        {
            // src/config.ts defaults, including the evaluator's additional history weight.
            public double threat = 0.25, alliance = 0.25, relationship = 0.3;
            public double deal = 0.1, strategicValue = 0.1, history = 0.1;
        }

        private sealed class EvidenceValue
        {
            public double value;
            public List<string> evidenceIds = new List<string>();
        }

        /// <summary>
        /// The native scenario stores no deals, player persona, or bloc directives. Their
        /// explicit DTO collections remain empty; this does not reconstruct an imported web round.
        /// Native ordered memories use newest ten owned entries; the web sorts by timestamps.
        /// </summary>
        public static WebVoteOptions FromNative(EpisodeState state, string voterId)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.nominees.Count != 2 || state.Find(voterId) == null)
                throw new ArgumentException("Voting requires a known voter and two nominees.");
            var active = state.Active.Select(NativeContestant).ToList();
            var options = new WebVoteOptions
            {
                voter = NativeContestant(state.Find(voterId)),
                nominees = state.nominees.Select(id => NativeContestant(state.Find(id))).ToList(),
                state = new WebVoteState
                {
                    allActive = active,
                    activeCount = active.Count,
                    relationships = state.relationships.Select(r => r.Clone()).ToList(),
                    relationshipArcs = state.relationshipArcs.Select(a => new WebVoteRelationshipArc
                    { npcId = a.npcId, npcName = a.npcName, arcType = a.arcType, intensity = a.intensity, escalationLevel = a.escalationLevel }).ToList(),
                    alliances = state.alliances.Select(a => new WebVoteAlliance
                    {
                        id = a.id, name = a.name, status = a.active ? "Active" : "Dissolved",
                        members = new List<string>(a.members)
                    }).ToList(),
                    promises = state.promises.Select(p => new WebVotePromise
                    {
                        fromId = p.fromId, toId = p.toId, type = PromiseType(p.kind),
                        status = p.status.ToString().ToLowerInvariant()
                    }).ToList()
                },
                memories = state.memories.Where(m => m.ownerId == voterId).Reverse().Take(10).Select(m => m.text).ToList(),
                playerPersonaLabel = state.playerPersona.current
            };
            return options;
        }

        public static WebVoteEvaluation EvaluateNative(EpisodeState state, string voterId) => Evaluate(FromNative(state, voterId));

        public static string ExplainNative(EpisodeState state, WebVoteEvaluation evaluation, bool includePrivate = false) =>
            Explain(evaluation, state.contestants.Select(NativeContestant), includePrivate);

        public static WebVoteEvaluation Evaluate(WebVoteOptions options, Func<double> random = null)
        {
            if (options == null || options.state == null || options.voter == null || options.nominees == null || options.nominees.Count != 2)
                throw new ArgumentException("An evaluator input requires a state, voter, and ordered nominee pair.");
            if (options.nominees.Any(n => n == null) || options.nominees[0].id == options.nominees[1].id)
                throw new ArgumentException("Nominees must be two distinct contestants.");
            var weights = TraitWeights(options.voter.traits, options.state.activeCount);
            var evaluations = options.nominees.Select(n => EvaluateNominee(options, n, weights)).ToList();
            int selectedIndex;
            if (evaluations[0].score == evaluations[1].score)
            {
                var key = options.voter.id + ":" + string.Join(":", options.nominees.Select(n => n.id).OrderBy(id => id, StringComparer.Ordinal));
                var rng = new SeededRandom(SeededRandom.HashSeed(key));
                double roll = random == null ? rng.NextDouble() : random();
                if (double.IsNaN(roll) || roll < 0 || roll >= 1) throw new ArgumentOutOfRangeException(nameof(random));
                selectedIndex = (int)Math.Floor(roll * evaluations.Count);
            }
            else selectedIndex = evaluations[0].score < evaluations[1].score ? 0 : 1;
            var selected = evaluations[selectedIndex];
            var saved = evaluations[selectedIndex == 0 ? 1 : 0];
            double margin = Math.Abs(saved.score - selected.score);
            return new WebVoteEvaluation
            {
                voterId = options.voter.id, nomineeEvaluations = evaluations,
                selectedNomineeId = selected.nomineeId, savedNomineeId = saved.nomineeId,
                margin = margin, confidence = margin >= 25 ? "decisive" : margin >= 10 ? "leaning" : "tossUp",
                publicReasonCodes = RankReasons(selected, saved, true).Take(3).ToList(),
                privateReasonCodes = RankReasons(selected, saved, false).Take(3).ToList()
            };
        }

        public static string Explain(WebVoteEvaluation evaluation, IEnumerable<WebVoteContestant> nominees, bool includePrivate = false)
        {
            var pair = nominees.ToArray();
            var target = pair.FirstOrDefault(n => n.id == evaluation.selectedNomineeId);
            var saved = pair.FirstOrDefault(n => n.id == evaluation.savedNomineeId);
            if (target == null || saved == null) return "This is the best move for my game.";
            var codes = includePrivate ? evaluation.privateReasonCodes : evaluation.publicReasonCodes;
            switch (codes.FirstOrDefault())
            {
                case "relationship": return "I'm closer with " + saved.name + ", so I have to vote out " + target.name + ".";
                case "threat": return target.name + " is the bigger threat and needs to go.";
                case "alliance": return saved.name + " is in my alliance. I have to protect my own.";
                case "deal": return "I made a commitment that means keeping " + saved.name + " safe.";
                case "history": return "My history with " + target.name + " makes this the right vote for me.";
                case "blocPressure": return "My alliance needs me to vote out " + target.name + ".";
                case "memory": return "What I've learned about " + target.name + " makes them too risky to keep.";
                case "persona": return target.name + "'s reputation makes them dangerous to keep.";
                default: return "Keeping " + saved.name + " is better for my game right now.";
            }
        }

        private static Weights TraitWeights(IEnumerable<string> traits, int activeCount)
        {
            var w = new Weights();
            foreach (string trait in traits)
            {
                switch (trait)
                {
                    case "Strategic": w.threat += 0.15; w.relationship -= 0.1; w.strategicValue += 0.1; break;
                    case "Loyal": w.alliance += 0.2; w.deal += 0.15; w.threat -= 0.1; break;
                    case "Competitive": w.threat += 0.1; w.strategicValue += 0.05; break;
                    case "Emotional": w.relationship += 0.2; w.threat -= 0.15; break;
                    case "Sneaky": w.alliance -= 0.15; w.strategicValue += 0.15; w.deal -= 0.1; break;
                    case "Confrontational": w.relationship += 0.1; w.alliance -= 0.1; break;
                    case "Analytical": w.threat += 0.1; w.strategicValue += 0.1; w.relationship -= 0.1; break;
                }
            }
            double total = w.threat + w.alliance + w.relationship + w.deal + w.strategicValue + w.history;
            w.threat /= total; w.alliance /= total; w.relationship /= total;
            w.deal /= total; w.strategicValue /= total; w.history /= total;
            if (activeCount <= 5) { w.threat *= 1.5; w.strategicValue *= 1.4; w.relationship *= 0.7; }
            return w;
        }

        private static double Threat(WebVoteContestant evaluator, WebVoteContestant target, WebVoteState state)
        {
            double competition = Math.Min(40, target.hohWins * 8 + target.vetoWins * 6);
            var others = state.allActive.Where(c => c.id != target.id && c.id != evaluator.id).ToArray();
            double average = others.Length == 0 ? 0 : others.Sum(c => Score(state, c.id, target.id)) / others.Length;
            double social = others.Length == 0 ? 15 : Clamp((average + 100) * 0.15, 0, 30);
            double alliance = Math.Min(20, state.alliances.Where(a => Active(a) && a.members.Contains(target.id)).Sum(a => a.members.Count * 4));
            double potential = (target.stats.competition / 10) * 3 + (target.stats.strategic / 10) * 2;
            if (target.stats.social >= 7 && target.stats.strategic >= 7) potential += 2;
            double broken = Math.Min(8, state.deals.Count(d => d.status == "broken" && (d.proposerId == target.id || d.recipientId == target.id)) * 3);
            var arc = target.isPlayer ? state.relationshipArcs.FirstOrDefault(a => a.npcId == evaluator.id) : null;
            double arcThreat = arc?.arcType == "rivalry" ? Math.Min(7, Math.Floor(arc.intensity / 15)) :
                arc?.arcType == "friendship" ? -Math.Min(5, Math.Floor(arc.intensity / 20)) : 0;
            return Clamp(competition + social + alliance + Math.Min(10, potential) + broken + arcThreat, 0, 100);
        }

        private static EvidenceValue AllianceLoyalty(string evaluatorId, string targetId, WebVoteState state)
        {
            var shared = state.alliances.Where(a => Active(a) && a.members.Contains(evaluatorId) && a.members.Contains(targetId)).ToArray();
            return new EvidenceValue
            {
                value = Math.Min(100, shared.Sum(a => Math.Min(a.members.Count * 10, 50) + (a.members.Count <= 3 ? 20 : 0))),
                evidenceIds = shared.Select(a => a.id).ToList()
            };
        }

        private static EvidenceValue DealObligation(string evaluatorId, string targetId, WebVoteState state)
        {
            var result = new EvidenceValue();
            foreach (var promise in state.promises)
            {
                bool pair = (promise.fromId == evaluatorId && promise.toId == targetId) || (promise.fromId == targetId && promise.toId == evaluatorId);
                if (!pair) continue;
                string evidence = "promise:" + promise.fromId + ":" + promise.toId + ":" + promise.type;
                if (promise.status == "pending" || promise.status == "active")
                {
                    result.value += promise.fromId == evaluatorId ? promise.type == "safety" ? 30 : promise.type == "vote" ? 20 : promise.type == "alliance_loyalty" ? 15 : 10 : 10;
                    result.evidenceIds.Add(evidence);
                }
                else if (promise.status == "broken" && promise.fromId == targetId) { result.value -= 25; result.evidenceIds.Add(evidence); }
            }
            foreach (var deal in state.deals)
            {
                if (deal.proposerId != evaluatorId && deal.recipientId != evaluatorId) continue;
                bool pair = deal.proposerId == targetId || deal.recipientId == targetId;
                bool targets = deal.targetHouseguestId == targetId;
                if (!pair && !targets) continue;
                if (deal.status == "active")
                {
                    if (deal.type == "vote_evict" && targets) result.value -= 35;
                    else if (deal.type == "target_agreement" && targets) result.value -= 20;
                    else if (deal.type == "vote_save" && targets) result.value += 35;
                    else if (pair) result.value += PairDealValue(deal.type);
                    result.evidenceIds.Add(deal.id);
                }
                else if (deal.status == "broken" && pair) { result.value -= 35; result.evidenceIds.Add(deal.id); }
                else if (deal.status == "fulfilled" && pair) { result.value += 5; result.evidenceIds.Add(deal.id); }
            }
            result.value = Clamp(result.value, -50, 50);
            return result;
        }

        private static double PairDealValue(string type)
        {
            switch (type)
            {
                case "safety_agreement": return 35;
                case "vote_together": return 25;
                case "final_two": return 50;
                case "partnership": return 20;
                case "veto_use": return 40;
                case "information_sharing": return 10;
                case "alliance_invite": return 25;
                default: return 10;
            }
        }

        private static double StrategicValue(WebVoteContestant evaluator, WebVoteContestant target, WebVoteState state)
        {
            double value = 50;
            int evaluatorWins = evaluator.hohWins + evaluator.vetoWins, targetWins = target.hohWins + target.vetoWins;
            if (targetWins > evaluatorWins) value += 15;
            if (state.activeCount <= 5)
            {
                if (target.stats.social >= 7) value -= 20;
                if (targetWins == 0 && target.stats.physical < 5) value += 15;
                if (targetWins >= 3) value -= 10;
            }
            else
            {
                if (target.stats.competition >= 7) value += 10;
                if (target.stats.social >= 8) value -= 10;
            }
            var others = state.allActive.Where(c => c.id != evaluator.id && c.id != target.id).ToArray();
            double perception = others.Length == 0 ? 0 : others.Sum(c => Score(state, c.id, target.id)) / others.Length;
            if (perception > 20) value -= 10;
            else if (perception < -20) value += 10;
            return Clamp(value, 0, 100);
        }

        private static double Personality(WebVoteContestant evaluator, WebVoteContestant target)
        {
            double value = evaluator.traits.Count(t => target.traits.Contains(t)) * 5;
            var pairs = new[] { new[] { "Loyal", "Sneaky" }, new[] { "Confrontational", "Floater" }, new[] { "Strategic", "Emotional" }, new[] { "Competitive", "Social" } };
            foreach (var pair in pairs)
                if ((evaluator.traits.Contains(pair[0]) && target.traits.Contains(pair[1])) || (evaluator.traits.Contains(pair[1]) && target.traits.Contains(pair[0]))) value -= 5;
            return Clamp(value, -20, 20);
        }

        private static double Memory(IEnumerable<string> memories, WebVoteContestant target)
        {
            double value = 0;
            string targetName = target.name.ToLowerInvariant();
            foreach (string memory in memories)
            {
                string text = memory.ToLowerInvariant();
                if (!text.Contains(targetName)) continue;
                if (text.Contains("lie") || text.Contains("deceiv") || text.Contains("betray") || text.Contains("suspicious")) value -= 8;
                else if (text.Contains("trust") || text.Contains("loyal") || text.Contains("helped") || text.Contains("saved")) value += 5;
                else if (text.Contains("threat") || text.Contains("danger") || text.Contains("target")) value -= 5;
                else value += 2;
            }
            return Clamp(value, -25, 25);
        }

        private static double Persona(string label)
        {
            switch (label)
            {
                case "Ruthless": return -5;
                case "Strategic Mastermind": return -4;
                case "Villain": return -6;
                case "Manipulator": return -5;
                case "Aggressive": return -3;
                case "Social Butterfly": return 3;
                case "Loyal": return 4;
                case "Floater": return 2;
                case "Underdog": return 3;
                case "Peacemaker": return 2;
                case "Charmer": return 3;
                default: return 0;
            }
        }

        private static WebNomineeEvaluation EvaluateNominee(WebVoteOptions o, WebVoteContestant nominee, Weights weights)
        {
            var alliance = AllianceLoyalty(o.voter.id, nominee.id, o.state);
            var deal = DealObligation(o.voter.id, nominee.id, o.state);
            var arc = nominee.isPlayer ? o.state.relationshipArcs.FirstOrDefault(a => a.npcId == o.voter.id) : null;
            double history = arc == null ? 0 : (arc.arcType == "rivalry" ? -1 : arc.arcType == "friendship" ? 1 : 0) * arc.intensity;
            double persona = nominee.isPlayer && !string.IsNullOrEmpty(o.playerPersonaLabel) ? Persona(o.playerPersonaLabel) : 0;
            bool follows = o.blocDirective?.directive == "bloc_vote" && o.blocDirective.targetNomineeId == nominee.id;
            var memories = o.memories ?? new List<string>();
            var factors = new List<WebVoteFactor>
            {
                Factor("relationship", Score(o.state, o.voter.id, nominee.id) * weights.relationship, "private", "relationship:" + o.voter.id + ":" + nominee.id),
                Factor("threat", -Threat(o.voter, nominee, o.state) * weights.threat, "public", "resume:" + nominee.id),
                Factor("alliance", alliance.value * weights.alliance, "private", alliance.evidenceIds.ToArray()),
                Factor("deal", deal.value * weights.deal, "private", deal.evidenceIds.ToArray()),
                Factor("strategicValue", StrategicValue(o.voter, nominee, o.state) * weights.strategicValue * 0.5, "private"),
                Factor("history", history * weights.history, "playerKnown", arc == null ? Array.Empty<string>() : new[] { "relationship-arc:" + arc.npcId }),
                Factor("personality", Personality(o.voter, nominee), "private"),
                Factor("memory", Memory(memories, nominee), "private", memories.Count > 0 ? new[] { "memories:" + o.voter.id } : Array.Empty<string>()),
                Factor("persona", persona, "private", persona == 0 ? Array.Empty<string>() : new[] { "persona:" + o.playerPersonaLabel }),
                Factor("blocPressure", follows ? -40 : 0, "private", follows ? new[] { o.blocDirective.allianceId } : Array.Empty<string>())
            };
            return new WebNomineeEvaluation { nomineeId = nominee.id, factors = factors, score = factors.Sum(f => f.value) };
        }

        private static IEnumerable<string> RankReasons(WebNomineeEvaluation selected, WebNomineeEvaluation saved, bool publicOnly) =>
            saved.factors.Where(f => !publicOnly || f.visibility == "playerKnown" || f.visibility == "public")
                .Select(f => new { f.code, contribution = f.value - (selected.factors.FirstOrDefault(s => s.code == f.code)?.value ?? 0) })
                .Where(f => f.contribution > 0.01).OrderByDescending(f => f.contribution).Select(f => f.code);

        private static WebVoteFactor Factor(string code, double value, string visibility, params string[] evidence) =>
            new WebVoteFactor { code = code, value = value, visibility = visibility, evidenceIds = evidence.ToList() };

        private static double Score(WebVoteState state, string from, string to) => state.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to)?.score ?? 0;
        private static bool Active(WebVoteAlliance alliance) => string.IsNullOrEmpty(alliance.status) || alliance.status == "Active";
        private static double Clamp(double value, double minimum, double maximum) => Math.Max(minimum, Math.Min(maximum, value));
        private static string PromiseType(PromiseKind kind) => kind == PromiseKind.FinalTwo ? "final_2" : kind == PromiseKind.AllianceLoyalty ? "alliance_loyalty" : kind.ToString().ToLowerInvariant();
        private static WebVoteContestant NativeContestant(ContestantState contestant) => new WebVoteContestant
        {
            id = contestant.id, name = contestant.name, isPlayer = contestant.isPlayer, traits = new List<string>(contestant.traits),
            stats = contestant.stats.Clone(), hohWins = contestant.hohWins, vetoWins = contestant.vetoWins
        };
    }
}
