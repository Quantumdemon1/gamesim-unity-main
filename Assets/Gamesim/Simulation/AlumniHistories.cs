using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The alumni rules (ALL-STAR-HISTORIES-PLAN §5.4): schema 30's <see cref="EpisodeState.alumniRulesVersion"/>, the
    /// fresh-season gate for every alumni behaviour, read as a bound, never as a gate on one number.
    ///
    /// <para>Each behaviour names the version that introduced it, and asks <see cref="At"/> with it. <see cref="Histories"/>
    /// is H1b's: the alumni's past games, written once into a fresh season's opening relationships
    /// (<see cref="EpisodeEngine.EnableAlumniHistories"/>). <see cref="Loyalty"/> is the owner's direction of 2026-10-29
    /// (§14.B): the inherited loyalty changes and decays with this season's acts (<see cref="AlumniLoyalty"/>).
    /// <see cref="Reputation"/> is the same direction's reputations and known connections (§14.C, §14.D): an alumnus's
    /// career and the known pairs of past games count in the nomination threat and the vote (<see cref="KnownBlocs"/>).
    /// Value 4 is claimed by §14 for counter-blocs, and 5 by its 14.L for outsider pacts that grow; they are not defined
    /// yet. The campaigns (AS7) take the next free value after §14, and H1d's other parts (after B9) later ones, in
    /// landing order. Only range checks and "new seasons start here" may read <see cref="Latest"/>.
    /// </para>
    ///
    /// <para>A season is never moved onto these rules after it starts: load, migration, recovery and the importer
    /// carry the value they find, and every season from before schema 30 holds 0 for ever.</para>
    /// </summary>
    public static class AlumniRules
    {
        /// <summary>No alumni behaviour: every season before schema 30, and every fresh season that seats no alumnus.</summary>
        public const int Off = 0;

        /// <summary>H1b: the histories' opening state, from the edition the season names.</summary>
        public const int Histories = 1;

        /// <summary>
        /// §14.B: the opening state of <see cref="Histories"/>, and the graded receipt share - this season's wrongs and
        /// public nominations erode inherited goodwill, pacts and kept words repair it up to a cap, a betrayal ends it, an
        /// old grievance heals through play - with the shared standing written off once at a public split.
        /// </summary>
        public const int Loyalty = 2;

        /// <summary>
        /// §14.C-D: everything of <see cref="Loyalty"/>, and the public record read as reputation. An alumnus's career
        /// (winner, finalist, played) and the risk of the known pairs of past games (a seated shared alliance whose two
        /// houseguests are both in and have not put each other up) count in the threat a Head of Household and their
        /// pact-mates read at the nominations, and in the vote's threat (<see cref="KnownBlocs"/>). The house's talk - the
        /// rumour subject, the pitch target, a broken word's grudge - reads the threat without them.
        /// </summary>
        public const int Reputation = 3;

        /// <summary>
        /// The highest value this build defines: for range checks and for new seasons only. Fresh seasons that seat an
        /// alumnus start here (§14.A); a value a test or the lab holds is a field write after that, valid while every value
        /// up to this one writes the same opening.
        /// </summary>
        public const int Latest = Reputation;

        /// <summary>Whether a saved value is one this build defines (validation's range).</summary>
        public static bool IsDefined(int version) => version >= Off && version <= Latest;

        /// <summary>
        /// Whether a season plays with its alumni's histories: from <see cref="Histories"/>, with an edition this build
        /// knows. Every reader keyed to the histories' receipt type asks this first, so an entry of that type in a
        /// season at 0 is an ordinary entry (§5.3).
        /// </summary>
        public static bool On(EpisodeState s) =>
            s != null && s.alumniRulesVersion >= Histories && AlumniEditions.Find(s.alumniCatalogEdition) != null;

        /// <summary>
        /// Whether a season plays a behaviour introduced at <paramref name="version"/>: on the alumni rules (<see cref="On"/>)
        /// at that value or later. A bound, never a gate on one number, so a later value carries every earlier one's
        /// behaviour; every term §14 adds asks this with its own value.
        /// </summary>
        public static bool At(EpisodeState s, int version) => On(s) && s.alumniRulesVersion >= version;
    }

    /// <summary>How an alumnus's record reads in the edition's career table (§3.4).</summary>
    public enum AlumniCareer
    {
        /// <summary>Played, without reaching a final two.</summary>
        Played,
        /// <summary>Reached a final two without winning.</summary>
        Finalist,
        /// <summary>Won a season.</summary>
        Winner,
    }

    /// <summary>One row of an edition's frozen identity table: a card's template id and its name as the card gave it.</summary>
    public sealed class AlumniIdentity
    {
        internal AlumniIdentity(string templateId, string name) { TemplateId = templateId; Name = name; }

        public string TemplateId { get; }
        public string Name { get; }
    }

    /// <summary>
    /// One directed part of a past game: the holder's view of the other, written once as a permanent one-way ledger
    /// receipt (<see cref="AlumniHistories.ReceiptType"/>) on the holder's edge, at the act's own weight (§3.6).
    /// </summary>
    public sealed class AlumniPriorPart
    {
        internal AlumniPriorPart(string holderId, string aboutId, double impact, string description)
        {
            HolderId = holderId; AboutId = aboutId; Impact = impact; Description = description;
        }

        /// <summary>The template id of the one whose view it is.</summary>
        public string HolderId { get; }
        /// <summary>The template id of the one it is about.</summary>
        public string AboutId { get; }
        /// <summary>The receipt's impact: trust reads a third of it (<see cref="ThreatAssessment.TrustPerImpactPoint"/>).</summary>
        public double Impact { get; }
        /// <summary>The receipt's words, in the game register: the act, never a reading of it.</summary>
        public string Description { get; }
    }

    /// <summary>
    /// One on-show game act between two alumni, from the project's research (§3.1-§3.2): who, the act, its research row
    /// and sources, the shared standing it writes (shared alliances only) and its directed parts.
    /// </summary>
    public sealed class AlumniPairFact
    {
        internal AlumniPairFact(string id, int season, string firstId, string secondId, string act, string researchRow,
            string[] sources, double standing, AlumniPriorPart[] parts, string openingDeal)
        {
            Id = id; Show = "Big Brother"; Season = season; FirstId = firstId; SecondId = secondId; Act = act;
            ResearchRow = researchRow; Sources = AlumniEditions.Freeze(sources); Standing = standing;
            Parts = AlumniEditions.Freeze(parts); OpeningDeal = openingDeal;
        }

        /// <summary>The fact's id, unique and ordinal-ordered in its edition; the order the writer takes.</summary>
        public string Id { get; }
        /// <summary>The show the act happened on. Edition 1 holds Big Brother seasons only (decision D-H1).</summary>
        public string Show { get; }
        public int Season { get; }
        /// <summary>The two people, by template id, in the research row's order.</summary>
        public string FirstId { get; }
        public string SecondId { get; }
        /// <summary>The act, in the game register.</summary>
        public string Act { get; }
        /// <summary>The research row it comes from (Codex's ALLSTAR-RESEARCH.md and AlumniCatalog.cs, 24baba83).</summary>
        public string ResearchRow { get; }
        /// <summary>The row's source ids, less any that fails the game-register rule (§3.1 rule 4).</summary>
        public ReadOnlyCollection<string> Sources { get; }
        /// <summary>The pair's shared standing, written both ways through <see cref="RelationshipLedger.Move"/>; 0 for none.</summary>
        public double Standing { get; }
        /// <summary>The directed parts, each a receipt on its holder's edge.</summary>
        public ReadOnlyCollection<AlumniPriorPart> Parts { get; }
        /// <summary>The ordinary deal the pair walks in with when both are houseguests (the Hitmen's final two); null for none.</summary>
        public string OpeningDeal { get; }

        public bool Involves(string templateId) => templateId == FirstId || templateId == SecondId;
    }

    /// <summary>One alumnus's career record (§3.4): the record's words and how it reads. Read as career reputation from alumni rules 3 (§14.C).</summary>
    public sealed class AlumniCareerRecord
    {
        internal AlumniCareerRecord(string templateId, string record, AlumniCareer career, string[] sources)
        {
            TemplateId = templateId; Record = record; Career = career; Sources = AlumniEditions.Freeze(sources);
        }

        public string TemplateId { get; }
        public string Record { get; }
        public AlumniCareer Career { get; }
        public ReadOnlyCollection<string> Sources { get; }
    }

    /// <summary>
    /// One immutable edition of the alumni's histories (§3): its id, the frozen identity table, the pairs and the career
    /// record. A later change is a new edition with a new id, selected for fresh seasons only; a saved season reads its
    /// own edition by id for ever. It reads no live card, lore sheet or state, draws nothing and changes nothing.
    /// </summary>
    public sealed class AlumniEdition
    {
        internal AlumniEdition(string id, AlumniIdentity[] identities, AlumniPairFact[] pairs, AlumniCareerRecord[] careers)
        {
            Id = id;
            Identities = AlumniEditions.Freeze(identities);
            Pairs = AlumniEditions.Freeze(pairs.OrderBy(p => p.Id, StringComparer.Ordinal).ToArray());
            Careers = AlumniEditions.Freeze(careers);
        }

        public string Id { get; }
        /// <summary>The template ids and card names this edition knows, frozen with it (§3.5).</summary>
        public ReadOnlyCollection<AlumniIdentity> Identities { get; }
        /// <summary>The pairs, in fact-id ordinal order.</summary>
        public ReadOnlyCollection<AlumniPairFact> Pairs { get; }
        public ReadOnlyCollection<AlumniCareerRecord> Careers { get; }

        public AlumniIdentity Identity(string templateId) => Identities.FirstOrDefault(i => i.TemplateId == templateId);

        public AlumniCareerRecord Career(string templateId) => Careers.FirstOrDefault(c => c.TemplateId == templateId);

        /// <summary>The pair fact between two people, either way round; null for none.</summary>
        public AlumniPairFact PairOf(string firstId, string secondId) =>
            Pairs.FirstOrDefault(p => p.Involves(firstId) && p.Involves(secondId) && firstId != secondId);

        /// <summary>The directed part one holds about another; null when the edition gives that direction none.</summary>
        public AlumniPriorPart PartFor(string holderId, string aboutId) =>
            PairOf(holderId, aboutId)?.Parts.FirstOrDefault(part => part.HolderId == holderId && part.AboutId == aboutId);

        /// <summary>
        /// What the record is worth to career reputation (§3.6, §7.5): winner, finalist, otherwise played; 0 for nobody
        /// the edition knows. Read from alumni rules 3 (<see cref="AlumniRules.Reputation"/>, §14.C) through
        /// <see cref="KnownBlocs.CareerFor"/>, by the nomination threat and the vote only: the owner's direction of
        /// 2026-10-29 decided it ahead of B9 (§14.A).
        /// </summary>
        public int CareerReputation(string templateId)
        {
            var record = Career(templateId);
            if (record == null) return 0;
            switch (record.Career)
            {
                case AlumniCareer.Winner: return AlumniEditions.WinnerReputation;
                case AlumniCareer.Finalist: return AlumniEditions.FinalistReputation;
                default: return AlumniEditions.PlayedReputation;
            }
        }
    }

    /// <summary>
    /// Every alumni histories edition ever shipped, compiled for ever (§5.4), and edition 1's numbers (§3.6).
    ///
    /// <para><b>The facts</b> are on-show, public game acts between two alumni, already in the project's research
    /// (Codex's candidate branch, origin/codex/allstar-characterization-20261012 at 24baba83: ALLSTAR-RESEARCH.md and
    /// AlumniCatalog.cs, whose research row and source ids each fact keeps), under §3.1's rule: the act, not a reading of
    /// it; the game register only, with no source whose subject is romance, personal life or conduct; direction decides
    /// whose trust moves; and no act the research does not name. Nothing here is off-show or personal.</para>
    /// </summary>
    public static class AlumniEditions
    {
        /// <summary>Edition 1's id (§3.2).</summary>
        public const string Histories1Id = "allstar-histories-1";

        /// <summary>A past act counts at half the in-game act's own ledger weight (§3.6).</summary>
        public const double PastActWeight = 0.5;

        /// <summary>A past nomination: half the ledger's permanent <c>nominated</c> -25. Trust reads about -4.</summary>
        public const double PastNomination = -25 * PastActWeight;

        /// <summary>A past alliance: half the ledger's permanent <c>alliance-formed</c> +30. Trust reads +5.</summary>
        public const double PastAlliance = 30 * PastActWeight;

        /// <summary>Shared standing for an on-show alliance that reached a final two, and for another (§3.6).</summary>
        public const double FinalTwoAllianceStanding = 20, AllianceStanding = 10;

        /// <summary>Career reputation (§3.6): winner, finalist, otherwise played. Read from alumni rules 3 by the nomination threat and the vote (<see cref="KnownBlocs.CareerFor"/>, §14.C).</summary>
        public const int WinnerReputation = 4, FinalistReputation = 3, PlayedReputation = 2;

        /// <summary>Pair risk (§3.6, §7.5): a final-two partnership's bloc and another shared alliance's. Read from alumni rules 3 by the nomination threat and the vote (<see cref="KnownBlocs.Risk"/>, §14.D).</summary>
        public const int FinalTwoPairRisk = 8, AlliancePairRisk = 4;

        private const string Dan = "dan-gheesling", Will = "dr-will-kirby", Derrick = "derrick-levasseur", Janelle = "janelle-pierzina",
            Cody = "cody-calafiore", Danielle = "danielle-reyes", Vanessa = "vanessa-rousso", Tyler = "tyler-crispen",
            Chelsie = "chelsie-baham", Rachel = "rachel-reilly", Xavier = "xavier-prather", Jun = "jun-song";

        internal static ReadOnlyCollection<T> Freeze<T>(T[] values) => Array.AsReadOnly((T[])(values ?? new T[0]).Clone());

        /// <summary>Edition 1: five pairs from shared Big Brother seasons and a career record for all twelve.</summary>
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
                new AlumniPairFact("bb16-hitmen", 16, Derrick, Cody,
                    "Derrick and Cody's Hitmen alliance reached the Big Brother 16 final two.",
                    "hitmen-bb16", new[] { "derrick-bb16-2014" }, FinalTwoAllianceStanding,
                    new[]
                    {
                        new AlumniPriorPart(Derrick, Cody, PastAlliance, "In Big Brother 16, the Hitmen alliance with Cody Calafiore reached the final two."),
                        new AlumniPriorPart(Cody, Derrick, PastAlliance, "In Big Brother 16, the Hitmen alliance with Derrick Levasseur reached the final two."),
                    }, DealKind.FinalTwo),
                new AlumniPairFact("bb22-committee", 22, Cody, Tyler,
                    "Cody and Tyler worked together in the Committee alliance in Big Brother 22.",
                    "cody-tyler-bb22", new[] { "cody-bb22-2020", "tyler-bb22-2020" }, AllianceStanding,
                    new[]
                    {
                        new AlumniPriorPart(Cody, Tyler, PastAlliance, "In Big Brother 22, Tyler Crispen worked with Cody Calafiore in the Committee alliance."),
                        new AlumniPriorPart(Tyler, Cody, PastAlliance, "In Big Brother 22, Cody Calafiore worked with Tyler Crispen in the Committee alliance."),
                    }, null),
                new AlumniPairFact("bb22-tyler-nominated-janelle", 22, Tyler, Janelle,
                    "Tyler nominated Janelle in Big Brother 22.",
                    "janelle-tyler-bb22", new[] { "janelle-bb22-2020" }, 0,
                    new[]
                    {
                        new AlumniPriorPart(Janelle, Tyler, PastNomination, "In Big Brother 22, Tyler Crispen nominated Janelle Pierzina for eviction."),
                    }, null),
                new AlumniPairFact("bb7-chilltown-renomination", 7, Will, Danielle,
                    "In Big Brother 7, Will's Chilltown helped persuade the Head of Household to nominate Danielle in Janelle's place before Danielle's eviction.",
                    "will-danielle-bb7", new[] { "bb7-danielle-exit-2006", "bb7-cast-2006" }, 0,
                    new[]
                    {
                        new AlumniPriorPart(Danielle, Will, PastNomination,
                            "In Big Brother 7, Dr. Will Kirby's Chilltown helped persuade the Head of Household to nominate Danielle Reyes in Janelle Pierzina's place, before Danielle's eviction."),
                    }, null),
                new AlumniPairFact("bb7-danielle-nominated-janelle", 7, Danielle, Janelle,
                    "Danielle nominated Janelle in Big Brother 7.",
                    "danielle-janelle-bb7", new[] { "bb7-danielle-nominations-2006" }, 0,
                    new[]
                    {
                        new AlumniPriorPart(Janelle, Danielle, PastNomination, "In Big Brother 7, Danielle Reyes nominated Janelle Pierzina for eviction."),
                    }, null),
            },
            new[]
            {
                new AlumniCareerRecord(Dan, "Won Big Brother 10; a finalist in Big Brother 14.", AlumniCareer.Winner, new[] { "dan-bb14-2012" }),
                new AlumniCareerRecord(Will, "Won Big Brother 2.", AlumniCareer.Winner, new[] { "bb-legacy-2022", "bb7-cast-2006" }),
                new AlumniCareerRecord(Derrick, "Won Big Brother 16.", AlumniCareer.Winner, new[] { "derrick-bb16-2014" }),
                new AlumniCareerRecord(Janelle, "Played Big Brother 6, 7, 14 and 22.", AlumniCareer.Played, new[] { "janelle-bb22-2020" }),
                new AlumniCareerRecord(Cody, "Runner-up in Big Brother 16; won Big Brother 22.", AlumniCareer.Winner, new[] { "cody-bb22-2020", "reindeer-cast-2023" }),
                new AlumniCareerRecord(Danielle, "Runner-up in Big Brother 3; played Big Brother 7.", AlumniCareer.Finalist,
                    new[] { "danielle-bb7-2006", "bb-cookout-history-2021", "bb7-cast-2006" }),
                new AlumniCareerRecord(Vanessa, "Third in Big Brother 17.", AlumniCareer.Played, new[] { "vanessa-poker-2015", "vanessa-bb17-2015" }),
                new AlumniCareerRecord(Tyler, "Runner-up in Big Brother 20; played Big Brother 22.", AlumniCareer.Finalist, new[] { "tyler-bb20-2018", "tyler-bb22-2020" }),
                new AlumniCareerRecord(Chelsie, "Won Big Brother 26.", AlumniCareer.Winner, new[] { "chelsie-bb26-2024" }),
                new AlumniCareerRecord(Rachel, "Played Big Brother 12; won Big Brother 13.", AlumniCareer.Winner, new[] { "rachel-bb13-2011" }),
                new AlumniCareerRecord(Xavier, "Won Big Brother 23.", AlumniCareer.Winner, new[] { "xavier-bb23-2021" }),
                new AlumniCareerRecord(Jun, "Won Big Brother 4.", AlumniCareer.Winner, new[] { "jun-bb4-2003" }),
            });

        private static readonly ReadOnlyCollection<AlumniEdition> editions = Freeze(new[] { Histories1 });

        /// <summary>Every edition ever shipped.</summary>
        public static IReadOnlyList<AlumniEdition> All => editions;

        /// <summary>The edition a fresh season starts from. New seasons only: a saved season reads its own by id.</summary>
        public static AlumniEdition Latest => Histories1;

        /// <summary>The edition with this id, or null: an unknown id is no edition at all. A plain loop: trust reads it on every call in a season on the alumni rules.</summary>
        public static AlumniEdition Find(string id)
        {
            if (id == null) return null;
            for (int i = 0; i < editions.Count; i++)
                if (string.Equals(editions[i].Id, id, StringComparison.Ordinal)) return editions[i];
            return null;
        }
    }

    /// <summary>
    /// The pure readers of a season's alumni histories (§3.5, §6): who in the house is which alumnus, and which of an
    /// edition's pairs the house seats. Functions of the edition and the saved cast; nothing is stored, drawn or changed.
    /// </summary>
    public static class AlumniHistories
    {
        /// <summary>The ledger type of a directed part's receipt (§5.2). Means nothing in a season at alumni rules 0.</summary>
        public const string ReceiptType = "alumni-history";

        /// <summary>Whether a ledger entry is a histories receipt by its type. Readers ask <see cref="AlumniRules.On"/> first.</summary>
        public static bool IsReceipt(RelationshipEventState entry) => entry != null && entry.type == ReceiptType;

        /// <summary>The edition a season names, or null at alumni rules 0 or for an id this build does not know.</summary>
        public static AlumniEdition EditionOf(EpisodeState s) => AlumniRules.On(s) ? AlumniEditions.Find(s.alumniCatalogEdition) : null;

        /// <summary>
        /// The alumni an edition finds in the house, template id to houseguest id (§3.5): a houseguest whose
        /// <c>sourceTemplateId</c> (else their id) is a table id and whose saved name is the table's name, the player
        /// included, read against the edition's own frozen table. When two houseguests resolve to one template, neither
        /// does: two custom slots from one card are both ambiguous. A renamed card is fiction and resolves to nobody.
        /// </summary>
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

        /// <summary>One edition pair the house seats: the fact and the two houseguests' ids.</summary>
        public sealed class SeatedPair
        {
            internal SeatedPair(AlumniPairFact fact, string firstId, string secondId) { Fact = fact; FirstId = firstId; SecondId = secondId; }

            public AlumniPairFact Fact { get; }
            /// <summary>The houseguest on the fact's first card.</summary>
            public string FirstId { get; }
            /// <summary>The houseguest on the fact's second card.</summary>
            public string SecondId { get; }

            /// <summary>The houseguest on one of the fact's cards, by template id.</summary>
            public string On(string templateId) => templateId == Fact.FirstId ? FirstId : templateId == Fact.SecondId ? SecondId : null;
        }

        /// <summary>The edition's pairs whose two people the house seats, intact and unique, in fact-id order.</summary>
        public static List<SeatedPair> SeatedPairs(EpisodeState s, AlumniEdition edition)
        {
            var seated = Seated(s, edition);
            var pairs = new List<SeatedPair>();
            if (edition == null) return pairs;
            foreach (var fact in edition.Pairs)
                if (seated.TryGetValue(fact.FirstId, out string first) && seated.TryGetValue(fact.SecondId, out string second))
                    pairs.Add(new SeatedPair(fact, first, second));
            return pairs;
        }
    }
}
