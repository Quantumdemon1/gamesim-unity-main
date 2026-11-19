// Frozen30 (talk S0, schema 31's step 1): what the live validators read at 64d52f59 that the schema-29 image never
// did - schema 30's pooled rules (the alumni rules and their edition, the tuning rules), the alumni histories'
// receipts (EpisodeValidation.Alumni.cs) and the house talk's records (EpisodeValidation.HouseTalk.cs, story rules 12).
// Frozen copies of exactly the members those checks read, over Frozen30's data-only snapshot. BCL only.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Gamesim.Persistence.Frozen30Data;

namespace Gamesim.Persistence.Frozen30ReadOnly
{
    internal static partial class EpisodeValidation
    {
        // SOURCE 64d52f59 Gamesim.Simulation.EpisodeValidation.TryValidateAlumni (EpisodeValidation.Alumni.cs)
        private static bool TryValidateAlumni(EpisodeState s, out string error)
        {
            error = null;
            var edition = AlumniHistories.EditionOf(s);
            if (edition == null) return true;
            var templateOf = AlumniHistories.Seated(s, edition).ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);
            foreach (var edge in s.relationships)
            {
                List<RelationshipEventState> receipts = null;
                foreach (var entry in edge.events)
                    if (AlumniHistories.IsReceipt(entry)) (receipts ??= new List<RelationshipEventState>()).Add(entry);
                if (receipts == null) continue;
                var holder = s.Find(edge.fromId);
                AlumniPriorPart part = null;
                if (receipts.Count == 1 && holder != null && !holder.isPlayer
                    && templateOf.TryGetValue(edge.fromId, out string holderTemplate) && templateOf.TryGetValue(edge.toId, out string aboutTemplate))
                    part = edition.PartFor(holderTemplate, aboutTemplate);
                var receipt = receipts[0];
                if (part == null || receipt.week != 1 || receipt.decayable || receipt.impactScore != part.Impact
                    || !string.Equals(receipt.description, part.Description, StringComparison.Ordinal))
                    return Fail(out error, "Invalid alumni history receipt.");
            }
            return true;
        }

        // SOURCE 64d52f59 Gamesim.Simulation.EpisodeValidation.TryValidateHouseTalk (EpisodeValidation.HouseTalk.cs)
        private static bool TryValidateHouseTalk(EpisodeState s, out string error)
        {
            error = null;
            return TryValidateHouseTalkManners(s, out error);
        }

        // SOURCE 64d52f59 Gamesim.Simulation.EpisodeValidation.TryValidateHouseTalkManners (EpisodeValidation.HouseTalk.cs)
        private static bool TryValidateHouseTalkManners(EpisodeState s, out string error)
        {
            error = null;
            var keys = s.story?.cooldowns;
            if (keys == null) return true;
            bool At(string key, string prefix) => key != null && key.StartsWith(prefix, StringComparison.Ordinal);
            var records = keys.Where(c => At(c?.key, EpisodeEngine.SaidPrefix) || At(c?.key, EpisodeEngine.TellPrefix) || At(c?.key, Regard.HeadToHeadPrefix)).ToList();
            if (records.Count == 0) return true;
            if (!EpisodeEngine.StoryAt(s, StoryRules.Manners))
                return Fail(out error, "A season below the house talk's manners has none of its records.");
            bool Seated(string id) => s.contestants.Any(c => c.id == id);
            foreach (var row in records)
            {
                var parts = row.key.Split(':');
                bool ok;
                if (At(row.key, EpisodeEngine.SaidPrefix))
                    ok = parts.Length == 5 && int.TryParse(parts[1], out int said) && said >= 1 && said <= s.week && Seated(parts[2])
                         && Candour.IsStance(parts[3]) && Manner.IsKnown(parts[4])
                         && (parts[3] == Candour.Neutral) == (parts[4] == Manner.NonAnswer || parts[4] == Manner.Deflect);
                else if (At(row.key, EpisodeEngine.TellPrefix))
                    ok = parts.Length == 3 && Seated(parts[1]) && Manner.IsTell(parts[2]);
                else
                    ok = parts.Length == 4 && int.TryParse(parts[1], out int won) && won >= 1 && won <= s.week && (parts[2] == "hoh" || parts[2] == "veto")
                         && Seated(parts[3]) && parts[3] != s.playerId;
                if (!ok) return Fail(out error, "Invalid house talk records.");
            }
            return true;
        }
    }

    // SOURCE 64d52f59 Gamesim.Simulation.Candour.Honest,Neutral,Dishonest,Stances,IsStance (Candour.cs)
    internal static class Candour
    {
        public const string Honest = "honest", Neutral = "neutral", Dishonest = "dishonest";
        public static readonly string[] Stances = { Honest, Neutral, Dishonest };
        public static bool IsStance(string stance) => stance != null && Array.IndexOf(Stances, stance) >= 0;
    }

    // SOURCE 64d52f59 Gamesim.Simulation.Manner (Candour.cs): the closed manners and tells.
    internal static class Manner
    {
        public const string Definite = "definite", Hedged = "hedged", TurnedBack = "turned-back", LaidOnThick = "laid-on-thick";
        public const string NonAnswer = "non-answer", Deflect = "deflect";
        public static readonly string[] All = { Definite, Hedged, TurnedBack, LaidOnThick, NonAnswer, Deflect };
        public static readonly string[] Tells = { Hedged, TurnedBack, LaidOnThick };
        public static bool IsKnown(string manner) => manner != null && Array.IndexOf(All, manner) >= 0;
        public static bool IsTell(string manner) => manner != null && Array.IndexOf(Tells, manner) >= 0;
    }

    // SOURCE 64d52f59 Gamesim.Simulation.Regard.HeadToHeadPrefix (Regard.cs)
    internal static class Regard
    {
        public const string HeadToHeadPrefix = "h2h:";
    }

    // SOURCE 64d52f59 Gamesim.Simulation.AlumniRules (AlumniHistories.cs): the ladder's values, its bound and On.
    internal static class AlumniRules
    {
        public const int Off = 0, Histories = 1, Loyalty = 2, Reputation = 3, CounterBlocs = 4, GrowingPacts = 5, TitledNames = 6;
        public const int Latest = TitledNames;
        public static bool IsDefined(int version) => version >= Off && version <= Latest;
        public static bool On(EpisodeState s) =>
            s != null && s.alumniRulesVersion >= Histories && AlumniEditions.Find(s.alumniCatalogEdition) != null;
    }

    // SOURCE 64d52f59 Gamesim.Simulation.TuningRules (TuningRules.cs): what validation reads.
    internal static class TuningRules
    {
        public const int Off = 0, Bands = 1;
        public const int Latest = Bands;
        public const int UnspentLimit = 5, StudyCap = 1;
        public static bool IsDefined(int version) => version >= Off && version <= Latest;
        public static bool At(EpisodeState s, int version) => s != null && s.tuningRulesVersion >= version;
        public static int StudyLimit(EpisodeState s) => !At(s, Bands) ? UnspentLimit : StudyCap;
        public static int PreparationBound(EpisodeState s) => StudyLimit(s);
        public const string JuryTiebreakKey = "gamesim/tuning-1/jury-tiebreak";
        public static bool KeyedJuryTiebreak(EpisodeState s) => At(s, Bands);
        public const string JuryTieRefusal = "A tied jury's winner must be the finalist the season's tiebreak draw gives the win to.";

        public static string JuryTieWinner(uint seed, string firstId, string secondId)
        {
            if (firstId == null) throw new ArgumentNullException(nameof(firstId));
            if (secondId == null) throw new ArgumentNullException(nameof(secondId));
            bool inOrder = string.CompareOrdinal(firstId, secondId) <= 0;
            string low = inOrder ? firstId : secondId, high = inOrder ? secondId : firstId;
            return (JuryTiebreakHash(seed, firstId, secondId) & 1UL) == 0 ? low : high;
        }

        public static ulong JuryTiebreakHash(uint seed, string firstId, string secondId)
        {
            if (firstId == null) throw new ArgumentNullException(nameof(firstId));
            if (secondId == null) throw new ArgumentNullException(nameof(secondId));
            bool inOrder = string.CompareOrdinal(firstId, secondId) <= 0;
            ulong hash = FnvOffset;
            hash = MixText(hash, JuryTiebreakKey);
            hash = MixWord(hash, seed);
            hash = MixText(hash, inOrder ? firstId : secondId);
            hash = MixText(hash, inOrder ? secondId : firstId);
            unchecked
            {
                hash ^= hash >> 33;
                hash *= 0xff51afd7ed558ccdUL;
                hash ^= hash >> 33;
                hash *= 0xc4ceb9fe1a85ec53UL;
                hash ^= hash >> 33;
            }
            return hash;
        }

        private const ulong FnvOffset = 14695981039346656037UL, FnvPrime = 1099511628211UL;

        private static ulong MixByte(ulong hash, int value)
        {
            unchecked { return (hash ^ (ulong)(value & 0xFF)) * FnvPrime; }
        }

        private static ulong MixWord(ulong hash, uint value)
        {
            for (int shift = 0; shift < 32; shift += 8) hash = MixByte(hash, (int)((value >> shift) & 0xFFu));
            return hash;
        }

        private static ulong MixText(ulong hash, string text)
        {
            hash = MixWord(hash, (uint)text.Length);
            foreach (char unit in text)
            {
                hash = MixByte(hash, unit);
                hash = MixByte(hash, unit >> 8);
            }
            return hash;
        }
    }

    // SOURCE 64d52f59 Gamesim.Simulation.AlumniIdentity, AlumniPriorPart, AlumniPairFact and AlumniEdition
    // (AlumniHistories.cs): only the fields and lookups validation reads.
    internal sealed class AlumniIdentity
    {
        internal AlumniIdentity(string templateId, string name) { TemplateId = templateId; Name = name; }
        public string TemplateId { get; }
        public string Name { get; }
    }

    internal sealed class AlumniPriorPart
    {
        internal AlumniPriorPart(string holderId, string aboutId, double impact, string description)
        {
            HolderId = holderId; AboutId = aboutId; Impact = impact; Description = description;
        }
        public string HolderId { get; }
        public string AboutId { get; }
        public double Impact { get; }
        public string Description { get; }
    }

    internal sealed class AlumniPairFact
    {
        internal AlumniPairFact(string id, string firstId, string secondId, AlumniPriorPart[] parts)
        {
            Id = id; FirstId = firstId; SecondId = secondId; Parts = Array.AsReadOnly((AlumniPriorPart[])parts.Clone());
        }
        public string Id { get; }
        public string FirstId { get; }
        public string SecondId { get; }
        public ReadOnlyCollection<AlumniPriorPart> Parts { get; }
        public bool Involves(string templateId) => templateId == FirstId || templateId == SecondId;
    }

    internal sealed class AlumniEdition
    {
        internal AlumniEdition(string id, AlumniIdentity[] identities, AlumniPairFact[] pairs)
        {
            Id = id;
            Identities = Array.AsReadOnly((AlumniIdentity[])identities.Clone());
            Pairs = Array.AsReadOnly(pairs.OrderBy(p => p.Id, StringComparer.Ordinal).ToArray());
        }
        public string Id { get; }
        public ReadOnlyCollection<AlumniIdentity> Identities { get; }
        public ReadOnlyCollection<AlumniPairFact> Pairs { get; }
        public AlumniIdentity Identity(string templateId) => Identities.FirstOrDefault(i => i.TemplateId == templateId);
        public AlumniPairFact PairOf(string firstId, string secondId) =>
            Pairs.FirstOrDefault(p => p.Involves(firstId) && p.Involves(secondId) && firstId != secondId);
        public AlumniPriorPart PartFor(string holderId, string aboutId) =>
            PairOf(holderId, aboutId)?.Parts.FirstOrDefault(part => part.HolderId == holderId && part.AboutId == aboutId);
    }

    // SOURCE 64d52f59 Gamesim.Simulation.AlumniEditions (AlumniHistories.cs): edition 1's identities and the parts its
    // receipts are checked against, with the same weights.
    internal static class AlumniEditions
    {
        public const string Histories1Id = "allstar-histories-1";
        public const double PastActWeight = 0.5;
        public const double PastNomination = -25 * PastActWeight;
        public const double PastAlliance = 30 * PastActWeight;

        private const string Dan = "dan-gheesling", Will = "dr-will-kirby", Derrick = "derrick-levasseur", Janelle = "janelle-pierzina",
            Cody = "cody-calafiore", Danielle = "danielle-reyes", Vanessa = "vanessa-rousso", Tyler = "tyler-crispen",
            Chelsie = "chelsie-baham", Rachel = "rachel-reilly", Xavier = "xavier-prather", Jun = "jun-song";

        public static readonly AlumniEdition Histories1 = new AlumniEdition(Histories1Id,
            new[]
            {
                new AlumniIdentity(Dan, "Dan Gheesling"),
                new AlumniIdentity(Will, "Dr. Will Kirby"),
                new AlumniIdentity(Derrick, "Derrick Levasseur"),
                new AlumniIdentity(Janelle, "Janelle Pierzina"),
                new AlumniIdentity(Cody, "Cody Calafiore"),
                new AlumniIdentity(Danielle, "Danielle Reyes"),
                new AlumniIdentity(Vanessa, "Vanessa Rousso"),
                new AlumniIdentity(Tyler, "Tyler Crispen"),
                new AlumniIdentity(Chelsie, "Chelsie Baham"),
                new AlumniIdentity(Rachel, "Rachel Reilly"),
                new AlumniIdentity(Xavier, "Xavier Prather"),
                new AlumniIdentity(Jun, "Jun Song"),
            },
            new[]
            {
                new AlumniPairFact("bb16-hitmen", Derrick, Cody, new[]
                {
                    new AlumniPriorPart(Derrick, Cody, PastAlliance, "In Big Brother 16, the Hitmen alliance with Cody Calafiore reached the final two."),
                    new AlumniPriorPart(Cody, Derrick, PastAlliance, "In Big Brother 16, the Hitmen alliance with Derrick Levasseur reached the final two."),
                }),
                new AlumniPairFact("bb22-committee", Cody, Tyler, new[]
                {
                    new AlumniPriorPart(Cody, Tyler, PastAlliance, "In Big Brother 22, Tyler Crispen worked with Cody Calafiore in the Committee alliance."),
                    new AlumniPriorPart(Tyler, Cody, PastAlliance, "In Big Brother 22, Cody Calafiore worked with Tyler Crispen in the Committee alliance."),
                }),
                new AlumniPairFact("bb22-tyler-nominated-janelle", Tyler, Janelle, new[]
                {
                    new AlumniPriorPart(Janelle, Tyler, PastNomination, "In Big Brother 22, Tyler Crispen nominated Janelle Pierzina for eviction."),
                }),
                new AlumniPairFact("bb7-chilltown-renomination", Will, Danielle, new[]
                {
                    new AlumniPriorPart(Danielle, Will, PastNomination,
                        "In Big Brother 7, Dr. Will Kirby's Chilltown helped persuade the Head of Household to nominate Danielle Reyes in Janelle Pierzina's place, before Danielle's eviction."),
                }),
                new AlumniPairFact("bb7-danielle-nominated-janelle", Danielle, Janelle, new[]
                {
                    new AlumniPriorPart(Janelle, Danielle, PastNomination, "In Big Brother 7, Danielle Reyes nominated Janelle Pierzina for eviction."),
                }),
            });

        private static readonly ReadOnlyCollection<AlumniEdition> editions = Array.AsReadOnly(new[] { Histories1 });

        public static AlumniEdition Find(string id)
        {
            if (id == null) return null;
            for (int i = 0; i < editions.Count; i++)
                if (string.Equals(editions[i].Id, id, StringComparison.Ordinal)) return editions[i];
            return null;
        }
    }

    // SOURCE 64d52f59 Gamesim.Simulation.AlumniHistories (AlumniHistories.cs): the receipt type, the edition and the seating.
    internal static class AlumniHistories
    {
        public const string ReceiptType = "alumni-history";

        public static bool IsReceipt(RelationshipEventState entry) => entry != null && entry.type == ReceiptType;

        public static AlumniEdition EditionOf(EpisodeState s) => AlumniRules.On(s) ? AlumniEditions.Find(s.alumniCatalogEdition) : null;

        public static Dictionary<string, string> Seated(EpisodeState s, AlumniEdition edition)
        {
            var seated = new Dictionary<string, string>(StringComparer.Ordinal);
            if (s?.contestants == null || edition == null) return seated;
            var ambiguous = new HashSet<string>(StringComparer.Ordinal);
            foreach (var who in s.contestants)
            {
                if (who == null) continue;
                string templateId = who.sourceTemplateId ?? who.id;
                var identity = templateId == null ? null : edition.Identity(templateId);
                if (identity == null || !string.Equals(identity.Name, who.name, StringComparison.Ordinal)) continue;
                if (seated.ContainsKey(templateId)) ambiguous.Add(templateId);
                else seated.Add(templateId, who.id);
            }
            foreach (string templateId in ambiguous) seated.Remove(templateId);
            return seated;
        }
    }
}
