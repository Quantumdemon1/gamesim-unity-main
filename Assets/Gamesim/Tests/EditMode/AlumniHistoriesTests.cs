using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The All-Star player histories (ALL-STAR-HISTORIES-PLAN, H1a's edition and H1b's opening state). Edition 1 is pinned
    /// as the plan records it (§3): the frozen identity table, five pairs from shared Big Brother seasons, the career
    /// record, the numbers' one rule, nothing held out and no source the game-register rule refuses. A fresh season that
    /// seats alumni starts with exactly their seated pairs' histories, the same on every run and every seed, with nothing
    /// drawn; a season that seats none is the season it was; validation holds every receipt to its part; the readers keyed
    /// to the receipt read it only under the alumni rules; and a season saved and loaded keeps it and plays on the same.
    /// The store's own round trip is <c>AlumniHistoriesSaveTests</c> (EditMode only).
    /// </summary>
    public sealed class AlumniHistoriesTests
    {
        private const string Dan = "dan-gheesling", Will = "dr-will-kirby", Derrick = "derrick-levasseur", Janelle = "janelle-pierzina",
            Cody = "cody-calafiore", Danielle = "danielle-reyes", Vanessa = "vanessa-rousso", Tyler = "tyler-crispen",
            Chelsie = "chelsie-baham", Rachel = "rachel-reilly", Xavier = "xavier-prather", Jun = "jun-song";

        private static AlumniEdition One => AlumniEditions.Histories1;

        // ------------------------------------------------------------------ H1a: edition 1, inert

        [Test]
        public void EditionOneKnowsTheTwelveAlumniByItsOwnFrozenTable()
        {
            Assert.That(AlumniEditions.Histories1Id, Is.EqualTo("allstar-histories-1"));
            Assert.That(One.Id, Is.EqualTo("allstar-histories-1"));
            Assert.That(AlumniEditions.All, Is.EqualTo(new[] { One }), "Every edition ever shipped.");
            Assert.That(AlumniEditions.Latest, Is.SameAs(One), "New seasons start from edition 1.");
            Assert.That(AlumniEditions.Find("allstar-histories-1"), Is.SameAs(One));
            Assert.That(AlumniEditions.Find("allstar-histories-2"), Is.Null);
            Assert.That(AlumniEditions.Find("ALLSTAR-HISTORIES-1"), Is.Null, "Ordinal ids.");
            Assert.That(AlumniEditions.Find(null), Is.Null);
            Assert.That(One.Identities.Select(i => i.TemplateId + "=" + i.Name), Is.EqualTo(new[]
            {
                "dan-gheesling=Dan Gheesling", "dr-will-kirby=Dr. Will Kirby", "derrick-levasseur=Derrick Levasseur",
                "janelle-pierzina=Janelle Pierzina", "cody-calafiore=Cody Calafiore", "danielle-reyes=Danielle Reyes",
                "vanessa-rousso=Vanessa Rousso", "tyler-crispen=Tyler Crispen", "chelsie-baham=Chelsie Baham",
                "rachel-reilly=Rachel Reilly", "xavier-prather=Xavier Prather", "jun-song=Jun Song",
            }));
            // Today the cards agree with the edition's table. A card's id or name changed later must not move a saved
            // season's histories: the edition keeps its own table, and such an edit needs a new edition or a decision.
            Assert.That(CastTemplates.In(CastTemplates.Roster.AllStars).Select(t => t.Id + "=" + t.Name),
                Is.EqualTo(One.Identities.Select(i => i.TemplateId + "=" + i.Name)));
        }

        [Test]
        public void EditionOneHoldsFivePairsFromSharedBigBrotherSeasons()
        {
            Assert.That(One.Pairs.Select(p => p.Id + " " + p.FirstId + "/" + p.SecondId + " BB" + p.Season + " standing " + p.Standing
                    + " [" + string.Join(", ", p.Parts.Select(x => x.HolderId + "->" + x.AboutId + " " + x.Impact)) + "] " + (p.OpeningDeal ?? "-")
                    + " row " + p.ResearchRow + " sources " + string.Join(",", p.Sources)),
                Is.EqualTo(new[]
                {
                    "bb16-hitmen derrick-levasseur/cody-calafiore BB16 standing 20 [derrick-levasseur->cody-calafiore 15, cody-calafiore->derrick-levasseur 15] final_two row hitmen-bb16 sources derrick-bb16-2014",
                    "bb22-committee cody-calafiore/tyler-crispen BB22 standing 10 [cody-calafiore->tyler-crispen 15, tyler-crispen->cody-calafiore 15] - row cody-tyler-bb22 sources cody-bb22-2020,tyler-bb22-2020",
                    "bb22-tyler-nominated-janelle tyler-crispen/janelle-pierzina BB22 standing 0 [janelle-pierzina->tyler-crispen -12.5] - row janelle-tyler-bb22 sources janelle-bb22-2020",
                    "bb7-chilltown-renomination dr-will-kirby/danielle-reyes BB7 standing 0 [danielle-reyes->dr-will-kirby -12.5] - row will-danielle-bb7 sources bb7-danielle-exit-2006,bb7-cast-2006",
                    "bb7-danielle-nominated-janelle danielle-reyes/janelle-pierzina BB7 standing 0 [janelle-pierzina->danielle-reyes -12.5] - row danielle-janelle-bb7 sources bb7-danielle-nominations-2006",
                }));
            // Edition 1's words, verbatim. Validation holds every saved receipt to its part's Description character for
            // character (§5.3), so these words are part of the save format: an edit in place - a wording audit, a typo
            // fix - would refuse every saved season that holds the receipt, and its backup with it. The acts and the
            // career record are pinned with them, so the edition changes only as a whole.
            const string Immutable = "Edition 1 is immutable: saved seasons hold its words, and validation compares a receipt's character for character. "
                + "A wording change needs a new edition id, selected for fresh seasons only.";
            Assert.That(One.Pairs.SelectMany(p => p.Parts).Select(x => x.HolderId + "->" + x.AboutId + ": " + x.Description), Is.EqualTo(new[]
            {
                "derrick-levasseur->cody-calafiore: In Big Brother 16, the Hitmen alliance with Cody Calafiore reached the final two.",
                "cody-calafiore->derrick-levasseur: In Big Brother 16, the Hitmen alliance with Derrick Levasseur reached the final two.",
                "cody-calafiore->tyler-crispen: In Big Brother 22, Tyler Crispen worked with Cody Calafiore in the Committee alliance.",
                "tyler-crispen->cody-calafiore: In Big Brother 22, Cody Calafiore worked with Tyler Crispen in the Committee alliance.",
                "janelle-pierzina->tyler-crispen: In Big Brother 22, Tyler Crispen nominated Janelle Pierzina for eviction.",
                "danielle-reyes->dr-will-kirby: In Big Brother 7, Dr. Will Kirby's Chilltown helped persuade the Head of Household to nominate Danielle Reyes in Janelle Pierzina's place, before Danielle's eviction.",
                "janelle-pierzina->danielle-reyes: In Big Brother 7, Danielle Reyes nominated Janelle Pierzina for eviction.",
            }), Immutable);
            Assert.That(One.Pairs.Select(p => p.Id + ": " + p.Act), Is.EqualTo(new[]
            {
                "bb16-hitmen: Derrick and Cody's Hitmen alliance reached the Big Brother 16 final two.",
                "bb22-committee: Cody and Tyler worked together in the Committee alliance in Big Brother 22.",
                "bb22-tyler-nominated-janelle: Tyler nominated Janelle in Big Brother 22.",
                "bb7-chilltown-renomination: In Big Brother 7, Will's Chilltown helped persuade the Head of Household to nominate Danielle in Janelle's place before Danielle's eviction.",
                "bb7-danielle-nominated-janelle: Danielle nominated Janelle in Big Brother 7.",
            }), Immutable);
            Assert.That(One.Careers.Select(c => c.TemplateId + ": " + c.Record), Is.EqualTo(new[]
            {
                "dan-gheesling: Won Big Brother 10; a finalist in Big Brother 14.",
                "dr-will-kirby: Won Big Brother 2.",
                "derrick-levasseur: Won Big Brother 16.",
                "janelle-pierzina: Played Big Brother 6, 7, 14 and 22.",
                "cody-calafiore: Runner-up in Big Brother 16; won Big Brother 22.",
                "danielle-reyes: Runner-up in Big Brother 3; played Big Brother 7.",
                "vanessa-rousso: Third in Big Brother 17.",
                "tyler-crispen: Runner-up in Big Brother 20; played Big Brother 22.",
                "chelsie-baham: Won Big Brother 26.",
                "rachel-reilly: Played Big Brother 12; won Big Brother 13.",
                "xavier-prather: Won Big Brother 23.",
                "jun-song: Won Big Brother 4.",
            }), Immutable);
            Assert.That(One.Pairs.Select(p => p.Id), Is.Ordered.Using((IComparer<string>)StringComparer.Ordinal), "Fact-id order is the writer's order.");
            Assert.That(One.Pairs.All(p => p.Show == "Big Brother"), Is.True, "Edition 1 is shared Big Brother seasons only (D-H1).");
            var known = new HashSet<string>(One.Identities.Select(i => i.TemplateId));
            foreach (var pair in One.Pairs)
            {
                Assert.That(known.Contains(pair.FirstId) && known.Contains(pair.SecondId) && pair.FirstId != pair.SecondId, Is.True, pair.Id);
                Assert.That(pair.Parts.All(part => pair.Involves(part.HolderId) && pair.Involves(part.AboutId) && part.HolderId != part.AboutId), Is.True, pair.Id);
                Assert.That(pair.Parts.Select(part => part.HolderId).Distinct().Count(), Is.EqualTo(pair.Parts.Count), pair.Id + ": one part a direction.");
                Assert.That(pair.Sources, Is.Not.Empty, pair.Id + ": every fact keeps its sources.");
            }
            Assert.That(One.Pairs.Select(p => string.Join("/", new[] { p.FirstId, p.SecondId }.OrderBy(x => x, StringComparer.Ordinal))).Distinct().Count(),
                Is.EqualTo(One.Pairs.Count), "One fact a pair.");
            Assert.That(One.PairOf(Cody, Derrick), Is.SameAs(One.PairOf(Derrick, Cody)), "Either way round.");
            Assert.That(One.PartFor(Tyler, Janelle), Is.Null, "Tyler's view of Janelle has no part: the nomination moves the nominee's trust only.");
        }

        /// <summary>§3.6: an alliance is the ledger's alliance-formed +30 at half weight both ways, a nomination its nominated -25 at half weight on the nominee's view only; standing for shared alliances only, 20 to a final two and 10 for another.</summary>
        [Test]
        public void EveryNumberFollowsTheOneRule()
        {
            Assert.That(AlumniEditions.PastActWeight, Is.EqualTo(0.5));
            Assert.That(AlumniEditions.PastAlliance, Is.EqualTo(15));
            Assert.That(AlumniEditions.PastNomination, Is.EqualTo(-12.5));
            foreach (var pair in One.Pairs)
            {
                if (pair.Standing > 0)
                {
                    Assert.That(pair.Standing, Is.EqualTo(pair.OpeningDeal == DealKind.FinalTwo ? AlumniEditions.FinalTwoAllianceStanding : AlumniEditions.AllianceStanding), pair.Id);
                    Assert.That(pair.Parts.Select(p => p.Impact), Is.EqualTo(new[] { AlumniEditions.PastAlliance, AlumniEditions.PastAlliance }), pair.Id + ": both ways.");
                }
                else
                {
                    Assert.That(pair.Standing, Is.Zero, pair.Id + ": a nomination is not shared, so it has no standing.");
                    Assert.That(pair.Parts.Select(p => p.Impact), Is.EqualTo(new[] { AlumniEditions.PastNomination }), pair.Id + ": the nominee's view only.");
                    Assert.That(pair.OpeningDeal, Is.Null);
                }
            }
            Assert.That(One.Pairs.Count(p => p.OpeningDeal != null), Is.EqualTo(1), "One opening commitment: the Hitmen's final two.");
        }

        [Test]
        public void EveryAlumnusHasACareerRecordAndTheNominationThreatReadsIt()
        {
            Assert.That(One.Careers.Select(c => c.TemplateId), Is.EqualTo(One.Identities.Select(i => i.TemplateId)));
            var reputation = new Dictionary<string, int>
            {
                { Dan, 4 }, { Will, 4 }, { Derrick, 4 }, { Janelle, 2 }, { Cody, 4 }, { Danielle, 3 },
                { Vanessa, 2 }, { Tyler, 3 }, { Chelsie, 4 }, { Rachel, 4 }, { Xavier, 4 }, { Jun, 4 },
            };
            foreach (var record in One.Careers)
            {
                Assert.That(One.CareerReputation(record.TemplateId), Is.EqualTo(reputation[record.TemplateId]), record.TemplateId);
                Assert.That(record.Sources, Is.Not.Empty, record.TemplateId);
                Assert.That(record.Record, Does.Contain("Big Brother"), record.TemplateId + ": the seasons, in the research's words.");
            }
            Assert.That(One.Career(Janelle).Record, Is.EqualTo("Played Big Brother 6, 7, 14 and 22."));
            Assert.That(One.Career(Janelle).Sources, Is.EqualTo(new[] { "janelle-bb22-2020" }), "Janelle's record rests on her BB22 exit interview alone (§3.4).");
            Assert.That(One.CareerReputation("player"), Is.Zero, "A newcomer has no record.");
            // Career reputation and pair risk are read from alumni rules 3, by the nomination threat and the vote only
            // (§14.C, §14.D; KnownBlocsTests): the owner's direction of 2026-10-29 took them ahead of B9.
            Assert.That(AlumniEditions.WinnerReputation + AlumniEditions.FinalistReputation + AlumniEditions.PlayedReputation, Is.EqualTo(9));
            Assert.That(AlumniEditions.FinalTwoPairRisk, Is.EqualTo(8));
            Assert.That(AlumniEditions.AlliancePairRisk, Is.EqualTo(4));
            var s = AllStars(12, 53u, value: AlumniRules.Reputation);
            foreach (var templateId in new[] { Dan, Janelle, Danielle, Tyler })
            {
                string id = On(s, templateId);
                Assert.That(KnownBlocs.CareerFor(s, id), Is.EqualTo(One.CareerReputation(templateId)), templateId + ": the edition's own value.");
                // The nomination threat reads it, as the newcomer player sees them: outside every pair, and with neither
                // the alliance cap nor reputation's clamp binding, the past games add the pair risk and the career exactly.
                Assert.That(ThreatAssessment.TotalWithPastGames(s, s.playerId, id) - ThreatAssessment.Total(s, s.playerId, id) - KnownBlocs.RiskFor(s, s.playerId, id),
                    Is.EqualTo(One.CareerReputation(templateId)).Within(1e-9), templateId + ": the career is in the nomination threat.");
            }
            Assert.That(KnownBlocs.CareerFor(s, s.playerId), Is.Zero, "The newcomer has none.");
        }

        /// <summary>§3.3: every row the plan holds out is absent, with every source the game-register rule refuses.</summary>
        [Test]
        public void NothingHeldOutIsInTheEdition()
        {
            foreach (var (a, b) in new[] { (Will, Janelle), (Janelle, Cody), (Cody, Danielle), (Danielle, Xavier), (Cody, Xavier),
                         (Janelle, Rachel), (Rachel, Cody), (Janelle, Dan), (Dan, Will) })
                Assert.That(One.PairOf(a, b), Is.Null, a + " and " + b + " are held out of edition 1.");
            var cited = One.Pairs.SelectMany(p => p.Sources).Concat(One.Careers.SelectMany(c => c.Sources)).ToList();
            foreach (string refused in new[] { "will-janelle-2006", "snake-cast-interview-2022", "rachel-traitors-2023", "janelle-traitors-2024",
                         "reindeer-danielle-cody-2023", "dan-will-2013", "will-strategy-2017" })
                Assert.That(cited, Does.Not.Contain(refused));
            var words = One.Pairs.Select(p => p.Act).Concat(One.Pairs.SelectMany(p => p.Parts.Select(x => x.Description)))
                .Concat(One.Careers.Select(c => c.Record)).ToList();
            foreach (string line in words)
                foreach (string personal in new[] { "romance", "showmance", "kiss", "dating", "friend", "respect", "suspect", "believed", "feel" })
                    Assert.That(line.IndexOf(personal, StringComparison.OrdinalIgnoreCase), Is.LessThan(0), "Game acts only: " + line);
        }

        [Test]
        public void TheEditionIsReadOnlyAndHoldsNoStateOrRandomness()
        {
            Assert.Throws<NotSupportedException>(() => ((IList<AlumniPairFact>)One.Pairs).Add(One.Pairs[0]));
            Assert.Throws<NotSupportedException>(() => ((IList<AlumniIdentity>)One.Identities).RemoveAt(0));
            Assert.Throws<NotSupportedException>(() => ((IList<AlumniPriorPart>)One.Pairs[0].Parts).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<string>)One.Pairs[0].Sources).Add("x"));
            Assert.Throws<NotSupportedException>(() => ((IList<AlumniCareerRecord>)One.Careers).Add(One.Careers[0]));
            foreach (var type in new[] { typeof(AlumniEdition), typeof(AlumniPairFact), typeof(AlumniPriorPart), typeof(AlumniCareerRecord), typeof(AlumniIdentity), typeof(AlumniEditions) })
            {
                Assert.That(type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).Where(p => p.CanWrite), Is.Empty, type.Name + ": no setter.");
                Assert.That(type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).Where(f => !f.IsLiteral && !f.IsInitOnly), Is.Empty, type.Name + ": no writable field.");
                Assert.That(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance), Is.Empty, type.Name + ": nothing outside the simulation makes one.");
                Assert.That(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .SelectMany(m => m.GetParameters()).Where(p => p.ParameterType == typeof(EpisodeState) || p.ParameterType == typeof(SeededRandom)), Is.Empty,
                    type.Name + ": an edition reads no season and no generator.");
            }
        }

        // ------------------------------------------------------------------ H1b: the opening state

        /// <summary>The houseguest on a card.</summary>
        internal static string On(EpisodeState s, string templateId) => s.contestants.Single(c => (c.sourceTemplateId ?? c.id) == templateId).id;

        /// <summary>
        /// The shipped All-Stars season, enrolled at the alumni rules new seasons start from, then held at
        /// <paramref name="value"/> (ALL-STAR-HISTORIES-PLAN 14.G): a hold is the field alone, valid while every value
        /// from it to <see cref="AlumniRules.Latest"/> writes the same opening (AlumniLoyaltyTests pins that).
        /// </summary>
        internal static EpisodeState AllStars(int size, uint seed, string player = null, int value = AlumniRules.Latest)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size, Roster = CastTemplates.Roster.AllStars, PlayerTemplateId = player }, seed);
            ShippedRules.ApplyFresh(s);
            Assert.That(AlumniRules.On(s) && s.alumniRulesVersion == AlumniRules.Latest, Is.True, "The shipped All-Stars season enrols at the latest alumni rules.");
            s.alumniRulesVersion = value;
            return s;
        }

        /// <summary>Every receipt in a season: holder's template, about's template (the player's "player" when on no card) and impact.</summary>
        private static List<string> Receipts(EpisodeState s) =>
            s.relationships.SelectMany(r => r.events.Where(AlumniHistories.IsReceipt).Select(e => Template(s, r.fromId) + "->" + Template(s, r.toId) + " " + e.impactScore))
                .OrderBy(x => x, StringComparer.Ordinal).ToList();

        private static string Template(EpisodeState s, string id) { var c = s.Find(id); return c.isPlayer ? "player:" + (c.sourceTemplateId ?? "newcomer") : c.sourceTemplateId ?? c.id; }

        private static double FirstImpression(EpisodeState s, string from, string to) =>
            s.Find(from).isPlayer ? 0 : TraitAffinity.Compatibility(s.Find(from), s.Find(to)) * EpisodeEngine.FirstImpressionPerPoint;

        [TestCase(8, 1u)] [TestCase(8, 77u)] [TestCase(8, 4242u)] [TestCase(12, 1u)] [TestCase(12, 4242u)]
        public void AFreshAllStarsSeasonStartsWithItsSeatedPairsHistories(int size, uint seed)
        {
            var s = AllStars(size, seed);
            Assert.That(s.alumniRulesVersion, Is.EqualTo(AlumniRules.Latest));
            Assert.That(s.alumniCatalogEdition, Is.EqualTo("allstar-histories-1"));
            Assert.That(AlumniRules.On(s), Is.True);
            var expected = new List<string>
            {
                "cody-calafiore->derrick-levasseur 15", "danielle-reyes->dr-will-kirby -12.5",
                "derrick-levasseur->cody-calafiore 15", "janelle-pierzina->danielle-reyes -12.5",
            };
            // From nine Tyler is seated (table order), and with him the Committee and his nomination of Janelle.
            if (size >= 9) expected.AddRange(new[] { "cody-calafiore->tyler-crispen 15", "janelle-pierzina->tyler-crispen -12.5", "tyler-crispen->cody-calafiore 15" });
            Assert.That(Receipts(s), Is.EqualTo(expected.OrderBy(x => x, StringComparer.Ordinal)));
            foreach (var edge in s.relationships)
                foreach (var receipt in edge.events.Where(AlumniHistories.IsReceipt))
                {
                    Assert.That(receipt.week, Is.EqualTo(1));
                    Assert.That(receipt.decayable, Is.False, "A past game never fades.");
                    Assert.That(receipt.description, Is.EqualTo(One.PartFor(Template(s, edge.fromId), Template(s, edge.toId)).Description));
                }
            // Standing: the first impression plus the shared alliance, both ways; everybody else's is the first impression alone.
            foreach (var edge in s.relationships)
            {
                var fact = One.PairOf(Template(s, edge.fromId), Template(s, edge.toId));
                double standing = fact == null ? 0 : fact.Standing;
                Assert.That(edge.score, Is.EqualTo(FirstImpression(s, edge.fromId, edge.toId) + standing), edge.fromId + "->" + edge.toId);
                Assert.That(edge.lastInteractionWeek, Is.EqualTo(standing != 0 ? 1 : 0), "Move marks the pair in touch; nothing else is.");
            }
            Assert.That(ThreatAssessment.TrustScore(s, On(s, Derrick), On(s, Cody)), Is.EqualTo(50 + (15 + 18) / 3.0).Within(1e-9),
                "Cody's trust in Derrick: the past alliance and the opening final two's handshake.");
            Assert.That(ThreatAssessment.TrustScore(s, On(s, Danielle), On(s, Janelle)), Is.EqualTo(50 - 12.5 / 3).Within(1e-9));
            Assert.That(ThreatAssessment.TrustScore(s, On(s, Janelle), On(s, Danielle)), Is.EqualTo(50), "The nominator's own view is untouched.");
            Assert.That(s.relationshipArcs, Is.Empty, "No arc: the histories are not this season.");
            Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);
        }

        [Test]
        public void TheHitmenWalkInWithAnOrdinaryFinalTwo()
        {
            var s = AllStars(8, 1601u);
            var deal = s.deals.Single();
            Assert.That(deal.type, Is.EqualTo(DealKind.FinalTwo));
            Assert.That(deal.proposerId, Is.EqualTo(On(s, Derrick)));
            Assert.That(deal.recipientId, Is.EqualTo(On(s, Cody)));
            Assert.That(deal.status, Is.EqualTo(DealStatus.Active));
            Assert.That(deal.week, Is.EqualTo(1));
            Assert.That(deal.expiresWeek, Is.Zero, "Open-ended, like every final two.");
            Assert.That(deal.targetId, Is.Null);
            Assert.That(deal.trustImpact, Is.EqualTo(DealKind.DefaultTrust(DealKind.FinalTwo)));
            Assert.That(deal.id, Does.StartWith("deal-npc-"), "The deal pass's own row, not tagged as history.");
            Assert.That(s.unifiedCommitments.Any(row => row.id == deal.id), Is.False, "A final two is no vote-family kind, so it is an ordinary row in mode 2.");
            foreach (var (from, to) in new[] { (Derrick, Cody), (Cody, Derrick) })
            {
                var accepted = s.relationships.Single(r => r.fromId == On(s, from) && r.toId == On(s, to)).events.Single(e => e.type == "deal_accepted");
                Assert.That(accepted.impactScore, Is.EqualTo(18));
                Assert.That(accepted.decayable, Is.True, "The handshake fades as usual; the past game does not.");
            }
            Assert.That(NpcDeals.Between(s, On(s, Derrick), On(s, Cody)).Select(d => d.id), Is.EqualTo(new[] { deal.id }));
        }

        [Test]
        public void TheSameChoiceAndSeedBuildTheSameHouseAndEverySeedTheSameHistories()
        {
            foreach (int size in new[] { 8, 12 })
            {
                foreach (uint seed in new[] { 1u, 77u, 4242u })
                    Assert.That(Json(AllStars(size, seed)), Is.EqualTo(Json(AllStars(size, seed))), size + " seed " + seed);
                string Histories(EpisodeState s) => string.Join("|", s.relationships.SelectMany(r => r.events.Where(AlumniHistories.IsReceipt)
                    .Select(e => r.fromId + ">" + r.toId + " " + e.sequence + " " + e.impactScore + " " + e.description))) + " deals "
                    + string.Join(",", s.deals.Select(d => d.id + " " + d.type + " " + d.proposerId + ">" + d.recipientId)) + " next " + s.nextSequence;
                Assert.That(Histories(AllStars(size, 77u)), Is.EqualTo(Histories(AllStars(size, 1u))), "A past game is the same whatever the seed.");
                Assert.That(Histories(AllStars(size, 4242u)), Is.EqualTo(Histories(AllStars(size, 1u))));
            }
        }

        [TestCase(8, 515u)] [TestCase(12, 516u)]
        public void TheHistoriesDrawNothingAndMintOnlyTheirOwnSequences(int size, uint seed)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size, Roster = CastTemplates.Roster.AllStars }, seed);
            uint random = s.randomState;
            int sequence = s.nextSequence, events = s.events.Count;
            ShippedRules.ApplyFresh(s);
            Assert.That(s.randomState, Is.EqualTo(random), "No roll, the histories' step included.");
            Assert.That(s.events.Count, Is.EqualTo(events), "Nothing logged.");
            Assert.That(s.revision, Is.Zero);
            Assert.That(s.acceptedCommandIds, Is.Empty);
            int receipts = Receipts(s).Count;
            Assert.That(s.nextSequence, Is.EqualTo(sequence + receipts + 2), "One sequence a receipt, and two for the final two's handshake (its id takes the first).");
            Assert.That(s.relationships.SelectMany(r => r.events).Select(e => e.sequence).OrderBy(x => x),
                Is.EqualTo(Enumerable.Range(sequence, receipts + 2)), "Every ledger entry is the histories', numbered in order.");
            var deal = s.deals.Single();
            Assert.That(deal.id, Is.EqualTo("deal-npc-" + (sequence + receipts)));
        }

        private static IEnumerable<TestCaseData> OrdinaryBuilders()
        {
            yield return new TestCaseData("quick start", 0);
            yield return new TestCaseData("regular", 6);
            yield return new TestCaseData("regular", 8);
            yield return new TestCaseData("regular", 12);
        }

        private static EpisodeState Ordinary(string builder, int size, uint seed) =>
            builder == "quick start" ? ContentCatalog.Create(seed) : SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size }, seed);

        [TestCaseSource(nameof(OrdinaryBuilders))]
        public void ASeasonThatSeatsNoAlumnusStartsAsItDid(string builder, int size)
        {
            foreach (uint seed in new[] { 1u, 77u, 4242u })
            {
                var s = Ordinary(builder, size, seed);
                ShippedRules.ApplyFresh(s);
                Assert.That(s.alumniRulesVersion, Is.Zero, builder + " " + size);
                Assert.That(s.alumniCatalogEdition, Is.Null);
                Assert.That(Receipts(s), Is.Empty);
                Assert.That(s.deals, Is.Empty);
                Assert.That(s.relationships.SelectMany(r => r.events), Is.Empty, "Nothing on the ledger.");
                string before = Json(s);
                EpisodeEngine.EnableAlumniHistories(s);
                Assert.That(Json(s), Is.EqualTo(before), "With nobody to remember, the histories' step writes nothing.");
            }
        }

        [Test]
        public void NothingButApplyFreshWritesAHistory()
        {
            var created = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 12, Roster = CastTemplates.Roster.AllStars }, 9u);
            Assert.That(created.alumniRulesVersion, Is.Zero, "SeasonBuilder.Create builds the cast before any rule is on.");
            Assert.That(Receipts(created), Is.Empty);
            var scene = ContentCatalog.Create(9u);
            Assert.That(scene.alumniRulesVersion, Is.Zero, "The default scene never runs ApplyFresh.");

            var enrolled = AllStars(8, 9u);
            string once = Json(enrolled);
            EpisodeEngine.EnableAlumniHistories(enrolled);
            Assert.That(Json(enrolled), Is.EqualTo(once), "Never written twice.");

            var engine = new EpisodeEngine(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8, Roster = CastTemplates.Roster.AllStars }, 9u));
            Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            var played = engine.Snapshot;
            string before = Json(played);
            EpisodeEngine.EnableAlumniHistories(played);
            Assert.That(Json(played), Is.EqualTo(before), "A season under way is never enrolled.");
            Assert.Throws<ArgumentNullException>(() => EpisodeEngine.EnableAlumniHistories(null));
        }

        /// <summary>§7.2: the player on an alumni card. The houseguest's view of the player only; no Move, no arc, no seeded deal.</summary>
        [Test]
        public void ThePlayerOnAnAlumniCardMeetsTheHousesViewOfThemAndKeepsTheirOwn()
        {
            var s = AllStars(8, 31u, Derrick);
            Assert.That(s.alumniRulesVersion, Is.EqualTo(AlumniRules.Latest));
            // With Derrick's card taken by the player, Tyler is the eighth seat (table order), so the BB22 rows are seated too.
            Assert.That(Receipts(s), Is.EqualTo(new[]
            {
                "cody-calafiore->player:derrick-levasseur 15", "cody-calafiore->tyler-crispen 15", "danielle-reyes->dr-will-kirby -12.5",
                "janelle-pierzina->danielle-reyes -12.5", "janelle-pierzina->tyler-crispen -12.5", "tyler-crispen->cody-calafiore 15",
            }));
            string player = s.playerId, cody = On(s, Cody);
            Assert.That(s.Score(player, cody), Is.Zero, "The player's own view is theirs.");
            Assert.That(s.Score(cody, player), Is.EqualTo(FirstImpression(s, cody, player)), "No standing written with the player.");
            Assert.That(s.relationships.Where(r => r.fromId == player || r.toId == player).All(r => r.lastInteractionWeek == 0), Is.True);
            Assert.That(s.deals, Is.Empty, "No final two on the player's behalf: the weekly offer pass decides.");
            Assert.That(ThreatAssessment.TrustScore(s, player, cody), Is.EqualTo(55).Within(1e-9), "Cody's trust in the player.");
            Assert.That(s.relationshipArcs, Is.Empty);
            Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);

            // Janelle's views are the player's own: her two past nominations write nothing.
            var janelle = AllStars(9, 32u, Janelle);
            Assert.That(Receipts(janelle), Is.EqualTo(new[]
            {
                "cody-calafiore->derrick-levasseur 15", "cody-calafiore->tyler-crispen 15", "danielle-reyes->dr-will-kirby -12.5",
                "derrick-levasseur->cody-calafiore 15", "tyler-crispen->cody-calafiore 15",
            }));
            Assert.That(EpisodeValidation.TryValidate(janelle, out error), Is.True, error);
        }

        [Test]
        public void ARenamedOrRepeatedCardHasNoHistory()
        {
            var renamed = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8, Roster = CastTemplates.Roster.AllStars }, 41u);
            renamed.contestants.Single(c => c.id == Cody).name = "Cody";
            ShippedRules.ApplyFresh(renamed);
            Assert.That(Receipts(renamed), Is.EqualTo(new[] { "danielle-reyes->dr-will-kirby -12.5", "janelle-pierzina->danielle-reyes -12.5" }),
                "A renamed card is fiction: no Hitmen.");
            Assert.That(renamed.deals, Is.Empty);
            Assert.That(EpisodeValidation.TryValidate(renamed, out string error), Is.True, error);

            var repeated = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8, Roster = CastTemplates.Roster.AllStars }, 42u);
            var vanessa = repeated.contestants.Single(c => c.id == Vanessa);
            vanessa.sourceTemplateId = Danielle;
            vanessa.name = "Danielle Reyes";
            Assert.That(AlumniHistories.Seated(repeated, One).ContainsKey(Danielle), Is.False, "Two houseguests on one card: neither is her.");
            ShippedRules.ApplyFresh(repeated);
            Assert.That(Receipts(repeated), Is.EqualTo(new[] { "cody-calafiore->derrick-levasseur 15", "derrick-levasseur->cody-calafiore 15" }));

            var renamedPlayer = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8, Roster = CastTemplates.Roster.AllStars, PlayerTemplateId = Cody }, 43u);
            renamedPlayer.Find(renamedPlayer.playerId).name = "Not Cody";
            ShippedRules.ApplyFresh(renamedPlayer);
            Assert.That(Receipts(renamedPlayer), Is.EqualTo(new[] { "danielle-reyes->dr-will-kirby -12.5", "janelle-pierzina->danielle-reyes -12.5", "janelle-pierzina->tyler-crispen -12.5" }),
                "The player renamed off Cody's card is nobody's past partner; Tyler, seated in Cody's place, keeps his nomination of Janelle.");
        }

        // ------------------------------------------------------------------ validation (§5.3)

        [Test]
        public void ValidationHoldsEveryReceiptToItsPart()
        {
            var s = AllStars(12, 51u);
            Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);
            void Refused(string why, Action<EpisodeState> change)
            {
                var bad = s.Clone();
                change(bad);
                Assert.That(EpisodeValidation.TryValidate(bad, out string reason), Is.False, why);
                Assert.That(reason, Is.EqualTo("Invalid alumni history receipt."), why);
            }
            RelationshipEventState Receipt(EpisodeState state, string from, string to) =>
                state.relationships.Single(r => r.fromId == On(state, from) && r.toId == On(state, to)).events.Single(AlumniHistories.IsReceipt);
            Refused("another weight", bad => Receipt(bad, Janelle, Tyler).impactScore = -12);
            Refused("other words", bad => Receipt(bad, Cody, Tyler).description += " Again.");
            Refused("fading", bad => Receipt(bad, Derrick, Cody).decayable = true);
            // Week one only: in week two the ledger allows an entry dated week two, and the receipt's own check refuses it.
            var later = new EpisodeEngine(s.Clone());
            for (int i = 0; i < 400 && later.Snapshot.week < 2; i++)
                Assert.That(later.Apply(EpisodeEngineTests.NextCommand(later.Snapshot)).accepted, Is.True);
            var weekTwo = later.Snapshot;
            Assert.That(weekTwo.week, Is.EqualTo(2));
            Assert.That(EpisodeValidation.TryValidate(weekTwo, out error), Is.True, error);
            var dated = weekTwo.Clone();
            Receipt(dated, Janelle, Tyler).week = 2;
            Assert.That(EpisodeValidation.TryValidate(dated, out string datedReason), Is.False, "another week");
            Assert.That(datedReason, Is.EqualTo("Invalid alumni history receipt."), "another week");
            Refused("twice on an edge", bad =>
            {
                var edge = bad.relationships.Single(r => r.fromId == On(bad, Danielle) && r.toId == On(bad, Will));
                var copy = edge.events.Single(AlumniHistories.IsReceipt).Clone();
                copy.sequence = bad.nextSequence++;
                edge.events.Add(copy);
            });
            Refused("a direction without a part", bad => RelationshipLedger.RecordOneWay(bad, On(bad, Tyler), On(bad, Janelle), AlumniHistories.ReceiptType,
                AlumniEditions.PastNomination, One.PartFor(Janelle, Tyler).Description, permanent: true));
            Refused("no pair at all", bad => RelationshipLedger.RecordOneWay(bad, On(bad, Dan), On(bad, Chelsie), AlumniHistories.ReceiptType, 15, "x", permanent: true));
            Refused("its holder renamed", bad => bad.contestants.Single(c => c.id == On(bad, Danielle)).name = "Danielle");

            var player = AllStars(8, 52u, Derrick);
            Assert.That(EpisodeValidation.TryValidate(player, out error), Is.True, error);
            var held = player.Clone();
            RelationshipLedger.RecordOneWay(held, held.playerId, On(held, Cody), AlumniHistories.ReceiptType, AlumniEditions.PastAlliance,
                One.PartFor(Derrick, Cody).Description, permanent: true);
            Assert.That(EpisodeValidation.TryValidate(held, out string heldReason), Is.False, "The player's own view is never a receipt.");
            Assert.That(heldReason, Is.EqualTo("Invalid alumni history receipt."));
        }

        [Test]
        public void TheAlumniRulesAndTheirEditionGoTogether()
        {
            var s = AllStars(8, 61u);
            void Refused(string expected, Action<EpisodeState> change)
            {
                var bad = s.Clone();
                change(bad);
                Assert.That(EpisodeValidation.TryValidate(bad, out string reason), Is.False, expected);
                Assert.That(reason, Is.EqualTo(expected));
            }
            const string NotEnabled = "Schema 30's pooled rules are not enabled in this build.", NoEdition = "The alumni rules need the edition their season was built from.";
            Refused(NoEdition, bad => bad.alumniCatalogEdition = null);
            Refused(NoEdition, bad => bad.alumniCatalogEdition = "allstar-histories-2");
            Refused(NoEdition, bad => bad.alumniCatalogEdition = "");
            Refused(NotEnabled, bad => bad.alumniRulesVersion = AlumniRules.Latest + 1);
            Refused(NotEnabled, bad => bad.alumniRulesVersion = -1);
            Refused(NotEnabled, bad => bad.tuningRulesVersion = TuningRules.Latest + 1);
            var regular = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 62u);
            ShippedRules.ApplyFresh(regular);
            regular.alumniCatalogEdition = "allstar-histories-1";
            Assert.That(EpisodeValidation.TryValidate(regular, out string offReason), Is.False);
            Assert.That(offReason, Is.EqualTo(NotEnabled), "No edition at alumni rules 0.");
        }

        /// <summary>§5.3: at alumni rules 0 the receipt type is free text, as every ledger type was at schema 29, and no reader treats it specially.</summary>
        [Test]
        public void AtAlumniRulesZeroTheReceiptTypeMeansNothing()
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 71u);
            ShippedRules.ApplyFresh(s);
            var npcs = s.contestants.Where(c => !c.isPlayer).Select(c => c.id).ToList();
            RelationshipLedger.RecordOneWay(s, npcs[0], npcs[1], AlumniHistories.ReceiptType, 30, "Anything at all.", permanent: true);
            RelationshipLedger.RecordOneWay(s, npcs[0], npcs[1], AlumniHistories.ReceiptType, 30, "Twice.", permanent: false);
            Assert.That(AlumniRules.On(s), Is.False);
            Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);
            Assert.That(RelationshipLedger.RecentImpact(s, npcs[0], npcs[1]), Is.EqualTo(60), "An ordinary entry: it counts as recent.");
            RelationshipLedger.RecordOneWay(s, npcs[0], npcs[1], "deal_broken", -20, "Broke it.", permanent: true);
            Assert.That(RelationshipLedger.WeightedImpact(s, npcs[0], npcs[1]), Is.EqualTo(40), "And no breach rule reads it.");
            // Nor the graded share of alumni rules 2 (§14.B): with npcs[1] putting npcs[0] up this week, every new reader is silent.
            s.hohId = npcs[1];
            s.nominees = new List<string> { npcs[0], npcs[2] };
            Assert.That(AlumniLoyalty.GoodwillShare(s, npcs[0], npcs[1]), Is.EqualTo(1));
            Assert.That(AlumniLoyalty.GrievanceShare(s, npcs[0], npcs[1]), Is.EqualTo(1));
            Assert.That(AlumniLoyalty.PutUpWeeks(s, npcs[1], npcs[0], includeThisWeek: true), Is.Empty);
            Assert.That(AlumniLoyalty.PublicSplitWeek(s, npcs[0], npcs[1], includeThisWeek: true), Is.Zero);
            Assert.That(RelationshipLedger.WeightedImpact(s, npcs[0], npcs[1]), Is.EqualTo(40));
            // Nor the reputations and known pairs of alumni rules 3 (§14.C, §14.D): no career, no pair, and every threat as it was.
            Assert.That(KnownBlocs.LivePairs(s), Is.Empty);
            Assert.That(KnownBlocs.VoteTerms(s, npcs[3]), Is.Empty);
            Assert.That(WebEvictionVoting.FromNative(s, npcs[3]).alumniTerms, Is.Empty);
            foreach (string id in npcs)
            {
                Assert.That(KnownBlocs.CareerFor(s, id), Is.Zero);
                Assert.That(KnownBlocs.RiskFor(s, npcs[3], id), Is.Zero);
                Assert.That(ThreatAssessment.TotalWithPastGames(s, npcs[3], id), Is.EqualTo(ThreatAssessment.Total(s, npcs[3], id)));
            }
            // Nor the counter-blocs of alumni rules 4 (§14.E): no desire for numbers, and no bloc to hunt.
            foreach (string a in npcs)
                foreach (string b in npcs.Where(other => other != a))
                {
                    Assert.That(KnownBlocs.CounterDesire(s, a, b), Is.Zero);
                    Assert.That(KnownBlocs.CounterTarget(s, a, b), Is.Null);
                }
        }

        // ------------------------------------------------------------------ the readers (§7.3, §7.4)

        [Test]
        public void APastGameIsNotRecentButItIsTrusted()
        {
            var s = AllStars(8, 81u);
            string derrick = On(s, Derrick), cody = On(s, Cody);
            Assert.That(RelationshipLedger.RecentImpact(s, cody, derrick), Is.EqualTo(18), "The handshake is recent; the past game is not.");
            Assert.That(RelationshipLedger.WeightedImpact(s, cody, derrick), Is.EqualTo(33), "Trust reads both.");
            var off = s.Clone();
            off.alumniRulesVersion = AlumniRules.Off;
            off.alumniCatalogEdition = null;
            Assert.That(RelationshipLedger.RecentImpact(off, cody, derrick), Is.EqualTo(33), "Off the rules, the same entry is an ordinary one.");
        }

        /// <summary>
        /// Alumni rules 1's breach rule (§7.4), held at 1 (a season saved there replays by it for ever); from alumni rules
        /// 2 the graded share replaces it (§14.B, AlumniLoyaltyTests). Earlier name, before §14:
        /// ABreachThisSeasonEndsInheritedGoodwillButNeverHealsAnOldGrievance.
        /// </summary>
        [Test]
        public void AtAlumniRulesOneABreachThisSeasonEndsInheritedGoodwillButNeverHealsAnOldGrievance()
        {
            var s = AllStars(8, 82u, value: AlumniRules.Histories);
            string derrick = On(s, Derrick), cody = On(s, Cody), janelle = On(s, Janelle), danielle = On(s, Danielle);
            Assert.That(ThreatAssessment.TrustScore(s, derrick, cody), Is.EqualTo(50 + 33 / 3.0).Within(1e-9));
            var broken = s.Clone();
            RelationshipLedger.RecordOneWay(broken, cody, derrick, "deal_broken", -20, "Derrick broke their final two.", permanent: true);
            Assert.That(ThreatAssessment.TrustScore(broken, derrick, cody), Is.EqualTo(50 + (18 - 20) / 3.0).Within(1e-9),
                "Broken this season, the old partnership's +15 no longer counts: an old partner's betrayal hurts as a stranger's does.");
            Assert.That(ThreatAssessment.TrustScore(broken, cody, derrick), Is.EqualTo(50 + 33 / 3.0).Within(1e-9), "Only the edge that holds the breach.");
            var faded = s.Clone();
            RelationshipLedger.RecordOneWay(faded, cody, derrick, "talk", -20, "A cold word.");
            Assert.That(ThreatAssessment.TrustScore(faded, derrick, cody), Is.EqualTo(50 + 13 / 3.0).Within(1e-9), "Ordinary friction is no breach.");
            var grievance = s.Clone();
            RelationshipLedger.RecordOneWay(grievance, janelle, danielle, "deal_broken", -20, "Danielle broke her word.", permanent: true);
            Assert.That(ThreatAssessment.TrustScore(grievance, danielle, janelle), Is.EqualTo(50 + (-12.5 - 20) / 3.0).Within(1e-9),
                "A past nomination still counts after a new wrong.");
            var off = broken.Clone();
            off.alumniRulesVersion = AlumniRules.Off;
            off.alumniCatalogEdition = null;
            Assert.That(ThreatAssessment.TrustScore(off, derrick, cody), Is.EqualTo(50 + 13 / 3.0).Within(1e-9), "Off the rules, every entry counts as it always did.");

            // The rule as built, type by type (§7.4, section 13). Once an alumni-1 season is saved it replays by this
            // list, so narrowing or widening it is a new alumni rules value, never an edit; the lead or the owner settles
            // it before the branch ships. Every permanent negative entry of this season that is not a receipt ends the
            // goodwill, whatever its type: deal_broken (the settlement's two-way record, RecordBreach's one-way record and
            // the unified vote's atoms all write it), a broken promise (the legacy and the unified writer), a betrayal,
            // mending fences' rebuff, D4's double-dealing, every negative permanent story receipt, and the ledger's other
            // permanent negatives.
            foreach (string type in new[]
                     {
                         "deal_broken", "promise-broken", Allegiance.BetrayedType, Negotiation.RebuffType, StoryReceipts.DoubleDealt,
                         StoryReceipts.SoldOut, StoryReceipts.SecretExposed, StoryReceipts.ShowmanceBetrayed, StoryReceipts.PublicBlowup,
                         "nominated", "voted-against",
                     })
            {
                var breach = s.Clone();
                RelationshipLedger.RecordOneWay(breach, cody, derrick, type, -20, "A breach: " + type, permanent: true);
                Assert.That(ThreatAssessment.TrustScore(breach, derrick, cody), Is.EqualTo(50 + (18 - 20) / 3.0).Within(1e-9), type + " ends the inherited goodwill.");
            }
            // And what keeps it: nothing that fades, nothing at zero, nothing positive.
            foreach (var (type, impact, permanent, why) in new[]
                     {
                         ("deal_broken", 0.0, true, "The breaker's own zero-weight record of the deal they broke (the commitment rules)"),
                         ("deal_broken", -20.0, false, "A broken deal recorded off the commitment rules, which fades"),
                         (StoryReceipts.Snubbed, StoryReceipts.Impact(StoryReceipts.Snubbed), false, "A fading story receipt"),
                         (StoryReceipts.Argued, StoryReceipts.Impact(StoryReceipts.Argued), false, "Another fading story receipt"),
                         ("heard_about_betrayal", -10.0, false, "Hearsay"),
                         ("promise-kept", 25.0, true, "A kept promise"),
                     })
            {
                var kept = s.Clone();
                RelationshipLedger.RecordOneWay(kept, cody, derrick, type, impact, "Not a breach: " + type, permanent);
                Assert.That(ThreatAssessment.TrustScore(kept, derrick, cody), Is.EqualTo(50 + (33 + impact) / 3.0).Within(1e-9), why + " keeps the inherited goodwill.");
            }
        }

        [Test]
        public void ABusyEdgeKeepsItsPastGame()
        {
            var s = AllStars(8, 83u);
            string derrick = On(s, Derrick), cody = On(s, Cody);
            var off = s.Clone();
            off.alumniRulesVersion = AlumniRules.Off;
            off.alumniCatalogEdition = null;
            for (int i = 0; i < 600; i++)
            {
                RelationshipLedger.RecordOneWay(s, cody, derrick, "talk", 1, "Talk " + i);
                RelationshipLedger.RecordOneWay(off, cody, derrick, "talk", 1, "Talk " + i);
            }
            var edge = s.relationships.Single(r => r.fromId == cody && r.toId == derrick);
            Assert.That(edge.events, Has.Count.EqualTo(512));
            Assert.That(edge.events.Count(AlumniHistories.IsReceipt), Is.EqualTo(1), "Under the alumni rules the oldest ordinary entry goes.");
            Assert.That(edge.events[0].type, Is.EqualTo(AlumniHistories.ReceiptType));
            Assert.That(edge.events.Last().description, Is.EqualTo("Talk 599"));
            var offEdge = off.relationships.Single(r => r.fromId == cody && r.toId == derrick);
            Assert.That(offEdge.events, Has.Count.EqualTo(512));
            Assert.That(offEdge.events.Any(AlumniHistories.IsReceipt), Is.False, "Off the rules the oldest goes, as it always did.");
            Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);
        }

        // ------------------------------------------------------------------ saved, loaded and played

        [Test]
        public void ASeasonSavedAndLoadedKeepsItsHistoriesAndPlaysOnTheSame()
        {
            var start = AllStars(8, 91u);
            var loaded = Load(Saved(start));
            Assert.That(Json(loaded), Is.EqualTo(Json(start)), "The round trip keeps every field.");
            Assert.That(loaded.alumniRulesVersion, Is.EqualTo(AlumniRules.Latest));
            Assert.That(loaded.alumniCatalogEdition, Is.EqualTo("allstar-histories-1"));
            Assert.That(Receipts(loaded), Is.EqualTo(Receipts(start)));
            Assert.That(EpisodeValidation.TryValidate(loaded, out string error), Is.True, error);
            var engine = new EpisodeEngine(start);
            var again = new EpisodeEngine(loaded);
            for (int i = 0; i < 400 && engine.Snapshot.week <= 2; i++)
            {
                var command = EpisodeEngineTests.NextCommand(engine.Snapshot);
                var result = engine.Apply(command);
                Assert.That(result.accepted, Is.True, result.reason);
                var replayed = again.Apply(command);
                Assert.That(Json(replayed.state), Is.EqualTo(Json(result.state)), "command " + i);
                if (i % 25 == 0) again = new EpisodeEngine(Load(Saved(again.Snapshot)));
            }
            Assert.That(engine.Snapshot.week, Is.GreaterThan(2), "Two weeks played.");
            Assert.That(Receipts(engine.Snapshot), Is.EqualTo(Receipts(start)), "The past game is still there in week three.");
        }

        [TestCase(8, 101u)] [TestCase(12, 102u)]
        public void AnAllStarsSeasonWithItsHistoriesPlaysToAWinner(int size, uint seed)
        {
            var engine = new EpisodeEngine(AllStars(size, seed));
            int commands = 0;
            while (engine.Snapshot.phase != EpisodePhase.Finished && commands < 6000)
            {
                var s = engine.Snapshot;
                var result = engine.Apply(EpisodeEngineTests.NextCommand(s));
                Assert.That(result.accepted, Is.True, "week " + s.week + " " + s.phase + ": " + result.reason);
                commands++;
                if (result.state.week != s.week) Assert.That(EpisodeValidation.TryValidate(result.state, out string weekError), Is.True, weekError);
            }
            var final = engine.Snapshot;
            Assert.That(final.phase, Is.EqualTo(EpisodePhase.Finished));
            Assert.That(final.Find(final.winnerId), Is.Not.Null);
            Assert.That(EpisodeValidation.TryValidate(final, out string error), Is.True, error);
            Assert.That(Receipts(final), Is.EqualTo(Receipts(AllStars(size, seed))), "Nothing rewrites or drops a past game.");
        }

        // ------------------------------------------------------------------ the season without its histories

        /// <summary>
        /// The season as it was before the histories' step: each seated houseguest pair's standing taken back and its
        /// interaction week cleared, every receipt and the opening final two with its handshake removed, the sequences
        /// they took returned, and the alumni rules off. What the step wrote is exactly what this takes away, so the
        /// result equals the season built without the step (the subset's control) and, at the byte level, the fresh
        /// season recorded at the live 29 (<c>FreshSeasonStateDigestTests</c>).
        /// </summary>
        internal static EpisodeState WithoutHistories(EpisodeState enrolled)
        {
            var s = enrolled.Clone();
            var edition = AlumniHistories.EditionOf(s);
            if (edition == null) return s;
            bool Between(RelationshipState r, string a, string b) => (r.fromId == a && r.toId == b) || (r.fromId == b && r.toId == a);
            int minted = 0;
            foreach (var pair in AlumniHistories.SeatedPairs(s, edition))
            {
                if (s.Find(pair.FirstId).isPlayer || s.Find(pair.SecondId).isPlayer) continue;
                foreach (var edge in s.relationships.Where(r => Between(r, pair.FirstId, pair.SecondId)))
                {
                    if (pair.Fact.Standing != 0) { edge.score -= pair.Fact.Standing; edge.lastInteractionWeek = 0; }
                    if (pair.Fact.OpeningDeal != null) minted += edge.events.RemoveAll(e => e.type == "deal_accepted" && e.week == 1);
                }
                if (pair.Fact.OpeningDeal != null)
                    s.deals.RemoveAll(d => d.type == pair.Fact.OpeningDeal && d.proposerId == pair.FirstId && d.recipientId == pair.SecondId && d.week == 1);
            }
            foreach (var edge in s.relationships) minted += edge.events.RemoveAll(AlumniHistories.IsReceipt);
            s.nextSequence -= minted;
            s.alumniRulesVersion = AlumniRules.Off;
            s.alumniCatalogEdition = null;
            return s;
        }

#if !UNITY_5_3_OR_NEWER
        // The control is the internal ApplyFresh(fresh, false), which only the Unity-free subset can reach (no InternalsVisibleTo).
        private static IEnumerable<TestCaseData> EveryBuilder()
        {
            yield return new TestCaseData("quick start", 0, CastTemplates.Roster.Regular);
            yield return new TestCaseData("regular", 6, CastTemplates.Roster.Regular);
            yield return new TestCaseData("regular", 8, CastTemplates.Roster.Regular);
            yield return new TestCaseData("regular", 12, CastTemplates.Roster.Regular);
            yield return new TestCaseData("regular", 8, CastTemplates.Roster.AllStars);
            yield return new TestCaseData("regular", 12, CastTemplates.Roster.AllStars);
            yield return new TestCaseData("stress", 16, CastTemplates.Roster.Regular);
        }

        private static EpisodeState Build(string builder, int size, CastTemplates.Roster roster, uint seed)
        {
            switch (builder)
            {
                case "quick start": return ContentCatalog.Create(seed);
                case "stress": return SeasonBuilder.CreateVerificationStressHouse(size, seed);
                default: return SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size, Roster = roster }, seed);
            }
        }

        /// <summary>The histories are the only difference from the same season built without their step: field for field, seed for seed.</summary>
        [TestCaseSource(nameof(EveryBuilder))]
        public void TheHistoriesAreTheOnlyDifferenceFromTheSeasonWithoutThem(string builder, int size, CastTemplates.Roster roster)
        {
            foreach (uint seed in new[] { 1u, 77u, 4242u })
            {
                var shipped = Build(builder, size, roster, seed);
                ShippedRules.ApplyFresh(shipped);
                var control = Build(builder, size, roster, seed);
                ShippedRules.ApplyFresh(control, alumniHistories: false);
                Assert.That(control.alumniRulesVersion, Is.Zero);
                bool alumni = roster == CastTemplates.Roster.AllStars || builder == "stress";
                Assert.That(shipped.alumniRulesVersion, Is.EqualTo(alumni ? AlumniRules.Latest : AlumniRules.Off), builder + " " + size + " " + roster);
                if (!alumni) Assert.That(Json(shipped), Is.EqualTo(Json(control)), "A season that seats no alumnus is exactly the control.");
                Assert.That(shipped.randomState, Is.EqualTo(control.randomState), "Nothing drawn.");
                Assert.That(Json(WithoutHistories(shipped)), Is.EqualTo(Json(control)), builder + " " + size + " " + roster + " seed " + seed);
            }
        }

        [Test]
        public void TheStressHouseSeatsTheEightHouseAlumniAndTheirThreePairs()
        {
            var s = SeasonBuilder.CreateVerificationStressHouse(16, 1u);
            ShippedRules.ApplyFresh(s);
            Assert.That(AlumniHistories.Seated(s, One).Keys.OrderBy(x => x, StringComparer.Ordinal),
                Is.EqualTo(new[] { Dan, Derrick, Will, Cody, Danielle, Janelle, Vanessa }.OrderBy(x => x, StringComparer.Ordinal)));
            Assert.That(AlumniHistories.SeatedPairs(s, One).Select(p => p.Fact.Id), Is.EqualTo(new[] { "bb16-hitmen", "bb7-chilltown-renomination", "bb7-danielle-nominated-janelle" }));
            Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);
        }
#endif

        // ------------------------------------------------------------------ helpers

        internal static string Json(EpisodeState state)
            => JsonConvert.SerializeObject(state, new JsonSerializerSettings { ContractResolver = new PublicFields() });

        /// <summary>A plain serializer round trip, as AllWeekCadenceTests saves and loads; the store's own is AlumniHistoriesSaveTests'.</summary>
        internal static string Saved(EpisodeState state) => JsonConvert.SerializeObject(state);

        internal static EpisodeState Load(string json) => JsonConvert.DeserializeObject<EpisodeState>(json,
            new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace });

        private sealed class PublicFields : DefaultContractResolver
        {
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
                => type.GetFields(BindingFlags.Instance | BindingFlags.Public)
                    .Select(field => base.CreateProperty(field, MemberSerialization.Fields)).ToList();
        }
    }
}
