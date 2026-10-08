using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>An observation of an actual audible leaf, never an inferred agreement fact.</summary>
    [Serializable]
    public sealed class UnifiedHearingEvidenceState
    {
        public string incidentKey;
        public HouseFactState fact;
        public UnifiedHearingEvidenceState Clone() => new UnifiedHearingEvidenceState {
            incidentKey = incidentKey, fact = fact?.Clone() };
    }

    /// <summary>Initial knowledge has no hearing impact; a spread owns one incident/listener effect.</summary>
    [Serializable]
    public sealed class UnifiedHearingReceiptState
    {
        public string incidentKey, listenerId, factId, kind;
        public int heardWeek;
        public UnifiedHearingReceiptState Clone() => (UnifiedHearingReceiptState)MemberwiseClone();
    }

    /// <summary>
    /// Separately versioned hearing authority. It observes real facts, not the strongest agreement's
    /// private evidence. Fresh playable seasons select it explicitly. No helper rolls, publishes
    /// relationship effects, creates facts, or silently reconstructs old hearings.
    /// </summary>
    public static class UnifiedCommitmentHearings
    {
        public const int ProspectiveVersion = 1;
        // Both permanent family inventories count their historical and legacy rows. One leaf/ref,
        // one incident/listener pair, and the stored cast maximum prove these non-evicting bounds.
        public const int EvidenceCapacity = UnifiedCommitments.FamilyCapacity * 2;
        public const int ReceiptCapacity = EvidenceCapacity * (EpisodeValidation.MaximumCast - 1);
        public const string Initial = "initial", Spread = "spread";

        public static bool RulesOn(EpisodeState s) => s != null
            && s.unifiedHearingRulesVersion == ProspectiveVersion && UnifiedCommitments.RulesOn(s);

        public static bool ValidateStorage(EpisodeState s, out string error)
        {
            error = null;
            if (s == null || s.unifiedHearingEvidence == null || s.unifiedHearingReceipts == null)
                return Refuse(out error, "Missing durable hearing storage.");
            if (s.unifiedHearingRulesVersion == 0)
                return (s.unifiedHearingEvidence.Count == 0 && s.unifiedHearingReceipts.Count == 0)
                    || Refuse(out error, "Disabled hearing authority must remain empty.");
            if (!RulesOn(s) || !YourWord.On(s) || s.story?.facts == null || s.story.facts.Any(f => f == null)
                || s.unifiedHearingEvidence.Count > EvidenceCapacity || s.unifiedHearingReceipts.Count > ReceiptCapacity)
                return Refuse(out error, "Invalid prospective hearing storage.");
            if (!UnifiedCommitments.ValidateRecords(s, out error)) return false;
            IReadOnlyList<UnifiedCommitmentIncident> incidents;
            try { incidents = UnifiedCommitmentHistory.Breaches(s); }
            catch (ArgumentException e) { return Refuse(out error, e.Message); }
            var facts = new Dictionary<string, UnifiedHearingEvidenceState>(StringComparer.Ordinal);
            var refs = new HashSet<string>(StringComparer.Ordinal);
            foreach (var evidence in s.unifiedHearingEvidence)
            {
                if (evidence == null || !ValidFact(s, evidence.fact)
                    || facts.ContainsKey(evidence.fact.id) || !refs.Add(evidence.fact.refId))
                    return Refuse(out error, "Invalid or duplicate audible fact provenance.");
                facts.Add(evidence.fact.id, evidence);
                var incident = incidents.FirstOrDefault(i => i.EffectKey == evidence.incidentKey);
                if (!Matches(s, incident, evidence.fact))
                    return Refuse(out error, "Audible evidence does not belong to its actual safety incident.");
                var live = s.story.facts.Where(f => f.id == evidence.fact.id).ToArray();
                if (live.Length > 1 || live.Length == 1 && (!SameIdentity(live[0], evidence.fact)
                    || !ValidFact(s, live[0]) || Rank(live[0].visibility) < Rank(evidence.fact.visibility)
                    || evidence.fact.knowers.Any(id => !live[0].knowers.Contains(id))))
                    return Refuse(out error, "The live fact contradicts its archived audible provenance.");
                if (s.story.facts.Any(f => f.refId == evidence.fact.refId && f.kind == FactKinds.BrokenWord
                    && f.id != evidence.fact.id))
                    return Refuse(out error, "A canonical leaf cannot acquire another fact identity.");
            }
            var pairs = new HashSet<(string incident, string listener)>();
            foreach (var receipt in s.unifiedHearingReceipts)
            {
                if (receipt == null || !Token(receipt.listenerId) || !Token(receipt.factId)
                    || !facts.TryGetValue(receipt.factId, out var evidence) || receipt.incidentKey != evidence.incidentKey
                    || !pairs.Add((receipt.incidentKey, receipt.listenerId)))
                    return Refuse(out error, "Invalid or duplicate incident/listener hearing receipt.");
                var listener = s.Find(receipt.listenerId);
                var incident = incidents.First(i => i.EffectKey == receipt.incidentKey);
                int settled = s.unifiedCommitments.First(r => r.id == incident.EffectOwnerId).settledWeek;
                if (listener == null || listener.isPlayer || listener.id == s.playerId
                    || !evidence.fact.knowers.Contains(listener.id) || receipt.heardWeek < settled
                    || receipt.heardWeek < evidence.fact.week || receipt.heardWeek > s.week)
                    return Refuse(out error, "A hearing needs a real observed listener and a valid actual date.");
                if (receipt.kind == Initial)
                {
                    var owner = s.unifiedCommitments.First(r => r.id == incident.EffectOwnerId);
                    if (listener.id != incident.WrongedId || owner.sourcePolicy != UnifiedCommitments.DealPolicy
                        || evidence.fact.refId != owner.id || evidence.fact.week != settled || receipt.heardWeek != settled
                        || !evidence.fact.knowers.Contains(s.playerId))
                        return Refuse(out error, "Initial knowledge must come from the selected deal's actual emission.");
                }
                else if (receipt.kind != Spread) return Refuse(out error, "Unknown hearing receipt kind.");
            }
            return true;
        }

        internal static void RequireValid(EpisodeState s)
        {
            if (!ValidateStorage(s, out string error)) throw new ArgumentException(error, nameof(s));
        }

        internal static bool CanonicalLeaf(EpisodeState s, HouseFactState fact) =>
            fact != null && s.unifiedCommitments.Any(row => row.id == fact.refId);

        /// <summary>Called only after the actual selected deal source emitted its real fact.</summary>
        internal static void RecordInitial(EpisodeState s, HouseFactState fact)
        {
            RequireValid(s);
            var staged = s.Clone();
            var incident = Incident(staged, fact);
            var owner = staged.unifiedCommitments.First(r => r.id == incident.EffectOwnerId);
            if (owner.sourcePolicy != UnifiedCommitments.DealPolicy || owner.id != fact.refId
                || fact.week != owner.settledWeek || fact.week != s.week
                || !fact.knowers.Contains(s.playerId) || !fact.knowers.Contains(incident.WrongedId))
                throw new ArgumentException("Only the selected deal's emitted first-party fact seeds initial knowledge.");
            Observe(staged, fact, incident);
            if (!staged.unifiedHearingReceipts.Any(r => r.incidentKey == incident.EffectKey && r.listenerId == incident.WrongedId))
                staged.unifiedHearingReceipts.Add(new UnifiedHearingReceiptState { incidentKey = incident.EffectKey,
                    listenerId = incident.WrongedId, factId = fact.id, heardWeek = s.week, kind = Initial });
            RequireValid(staged);
            Install(s, staged);
        }

        /// <summary>Preflights the entire observed collection before installing any archive.</summary>
        internal static void ObserveSpread(EpisodeState s, IReadOnlyList<(HouseFactState fact, string listener)> told)
        {
            RequireValid(s);
            var staged = s.Clone();
            var observed = new HashSet<string>(told.Where(t => YourWord.IsYours(s, t.fact) && CanonicalLeaf(s, t.fact))
                .Select(t => t.fact.id), StringComparer.Ordinal);
            // Fact source order, rather than effect-owner or teller order, preserves audible leaf provenance.
            foreach (var fact in staged.story.facts.Where(f => observed.Contains(f.id)))
                Observe(staged, fact, Incident(staged, fact));
            foreach (var (fact, listener) in told.Where(t => observed.Contains(t.fact.id)))
            {
                var person = staged.Find(listener);
                if (person == null || person.status != ContestantStatus.Active || !fact.knowers.Contains(listener))
                    throw new ArgumentException("A spread observation requires its actual active knower.");
            }
            RequireValid(staged);
            Install(s, staged);
        }

        /// <summary>Detached hearing plan; the command owner publishes the existing effect only if added.</summary>
        internal static EpisodeState PrepareHearing(EpisodeState s, HouseFactState fact, string listenerId, out bool added)
        {
            RequireValid(s);
            var staged = s.Clone();
            var incident = Incident(staged, fact);
            var listener = staged.Find(listenerId);
            if (listener == null || listener.isPlayer || listener.id == s.playerId
                || listener.status != ContestantStatus.Active || !fact.knowers.Contains(listenerId))
                throw new ArgumentException("Only an actual active listener of the audible fact can receive a hearing.");
            Observe(staged, fact, incident);
            added = !staged.unifiedHearingReceipts.Any(r => r.incidentKey == incident.EffectKey && r.listenerId == listenerId);
            if (added) staged.unifiedHearingReceipts.Add(new UnifiedHearingReceiptState { incidentKey = incident.EffectKey,
                listenerId = listenerId, factId = fact.id, heardWeek = s.week, kind = Spread });
            RequireValid(staged);
            return staged;
        }

        /// <summary>Refresh only already observed leaves before pruning or after genuine widening.</summary>
        internal static void RefreshObserved(EpisodeState s)
        {
            if (!RulesOn(s)) return;
            RequireValid(s);
            var staged = s.Clone();
            foreach (var evidence in staged.unifiedHearingEvidence)
            {
                var live = staged.story.facts.FirstOrDefault(f => f.id == evidence.fact.id);
                if (live != null) evidence.fact = live.Clone();
            }
            RequireValid(staged);
            Install(s, staged);
        }

        /// <summary>Detached actual observations survive fact pruning; unobserved private aliases do not appear.</summary>
        internal static List<HouseFactState> AudibleFacts(EpisodeState s)
        {
            RequireValid(s);
            var result = s.unifiedHearingEvidence.Select(e =>
                (s.story.facts.FirstOrDefault(f => f.id == e.fact.id) ?? e.fact).Clone()).ToList();
            var ids = new HashSet<string>(result.Select(f => f.id), StringComparer.Ordinal);
            result.AddRange(s.story.facts.Where(f => YourWord.IsYours(s, f) && !ids.Contains(f.id)).Select(f => f.Clone()));
            return result;
        }

        internal static void Install(EpisodeState target, EpisodeState staged)
        {
            target.unifiedHearingEvidence = staged.unifiedHearingEvidence.Select(e => e.Clone()).ToList();
            target.unifiedHearingReceipts = staged.unifiedHearingReceipts.Select(r => r.Clone()).ToList();
        }

        private static UnifiedCommitmentIncident Incident(EpisodeState s, HouseFactState fact)
        {
            if (!ValidFact(s, fact) || !s.story.facts.Any(f => SameIdentity(f, fact)
                && f.visibility == fact.visibility && f.knowers.SequenceEqual(fact.knowers)))
                throw new ArgumentException("Hearing evidence must be an actual audible fact in this state.");
            var incident = UnifiedCommitmentHistory.Breaches(s).FirstOrDefault(i => Matches(s, i, fact));
            return incident ?? throw new ArgumentException("The audible leaf must name a real canonical safety breach.");
        }

        private static void Observe(EpisodeState staged, HouseFactState fact, UnifiedCommitmentIncident incident)
        {
            var existing = staged.unifiedHearingEvidence.FirstOrDefault(e => e.fact.refId == fact.refId || e.fact.id == fact.id);
            if (existing != null)
            {
                if (existing.incidentKey != incident.EffectKey || !SameIdentity(existing.fact, fact))
                    throw new ArgumentException("An observed leaf's immutable provenance cannot be replaced.");
                existing.fact = fact.Clone();
            }
            else staged.unifiedHearingEvidence.Add(new UnifiedHearingEvidenceState { incidentKey = incident.EffectKey, fact = fact.Clone() });
        }

        private static bool Matches(EpisodeState s, UnifiedCommitmentIncident i, HouseFactState f) => i != null
            && i.ActorId == s.playerId && i.ActorId == f.actorId && i.WrongedId == f.subjectId && i.EvidenceIds.Contains(f.refId)
            // Current sources emit no PromisePolicy-ref BrokenWord. A genuine heard deal alias may
            // differ from a stronger promise owner, but observing it never proves an initial emission.
            && s.unifiedCommitments.Any(row => row.id == f.refId && row.sourcePolicy == UnifiedCommitments.DealPolicy
                && row.settledWeek == f.week);
        private static bool ValidFact(EpisodeState s, HouseFactState f) => f != null && FactIdentity(s, f.id) && Token(f.refId)
            && f.kind == FactKinds.BrokenWord && f.actorId == s.playerId && s.Find(f.subjectId) != null
            && f.actorId != f.subjectId && Rank(f.visibility) >= Rank(FactVisibility.Whispered)
            && f.week >= 1 && f.week <= s.week && f.knowers != null && f.knowers.Count <= s.contestants.Count
            // Both real source creation paths seed these parties, and knowledge only widens.
            // A retained listener cannot replace that original audience after the live leaf is pruned.
            && f.knowers.Contains(f.actorId) && f.knowers.Contains(f.subjectId)
            && f.knowers.All(id => Token(id) && s.Find(id) != null)
            && f.knowers.Distinct(StringComparer.Ordinal).Count() == f.knowers.Count;
        private static bool SameIdentity(HouseFactState a, HouseFactState b) => a != null && b != null
            && a.id == b.id && a.refId == b.refId && a.kind == b.kind && a.actorId == b.actorId
            && a.subjectId == b.subjectId && a.week == b.week;
        private static int Rank(string visibility) => Array.IndexOf(FactVisibility.All, visibility);
        private static bool FactIdentity(EpisodeState s, string id) => id != null && id.StartsWith("fact-", StringComparison.Ordinal)
            && int.TryParse(id.Substring(5), NumberStyles.None, CultureInfo.InvariantCulture, out int sequence)
            && sequence >= 1 && sequence < s.nextSequence && s.nextSequence <= 1000000
            && id == "fact-" + sequence.ToString(CultureInfo.InvariantCulture);
        private static bool Token(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 160 && !value.Any(char.IsControl);
        private static bool Refuse(out string error, string message) { error = message; return false; }
    }
}
