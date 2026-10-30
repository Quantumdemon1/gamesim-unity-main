using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Alumni rules 3, <see cref="AlumniRules.Reputation"/> (ALL-STAR-HISTORIES-PLAN §14.C, §14.D, slice S2): the owner's
    /// direction of 2026-10-29, "players also have reputations and their connections are known to other players". An
    /// alumnus's career (winner 4, finalist 3, played 2) and the known pairs of past games - the seated shared alliances,
    /// the Hitmen (8) and the Committee (4), while both are in and neither has put the other up - count in the threat a
    /// Head of Household and their pact-mates read at the nominations, and in the vote's threat. Only seated shared
    /// alliances are pairs, counted as pairs; a departure or a public put-up ends one, and nothing private does. Every
    /// evaluator outside a pair reads its risk, inside the alliance cap, and a pact they can see that holds both is counted
    /// once, as the pact. The vote weighs what the nominations weigh, with no reputation clamp of its own. The house's
    /// talk - <see cref="ThreatAssessment.Total"/>, the rankings, the pitch target, a broken word's grudge - is the same
    /// at every value, and below 3 nothing reads the edition.
    ///
    /// <para>Fixtures in the NPC-agency style: the shipped All-Stars season held at this slice's value, with blocks, power
    /// rows, pacts and ledger entries written in, read through the public readers. Only public members, so the file
    /// compiles in Unity's EditMode assembly and in the dotnet subset. §14.D.3's table is asserted as
    /// <see cref="ThreatAssessment.TotalWithPastGames"/> less <see cref="ThreatAssessment.Total"/>.</para>
    /// </summary>
    public sealed class KnownBlocsTests
    {
        private const string Dan = "dan-gheesling", Will = "dr-will-kirby", Derrick = "derrick-levasseur", Janelle = "janelle-pierzina",
            Cody = "cody-calafiore", Danielle = "danielle-reyes", Vanessa = "vanessa-rousso", Tyler = "tyler-crispen", Chelsie = "chelsie-baham",
            Rachel = "rachel-reilly", Xavier = "xavier-prather";

        private const string Hitmen = "bb16-hitmen", Committee = "bb22-committee";
        private const double Exact = 1e-9;

        /// <summary>
        /// The shipped All-Stars season held at <paramref name="value"/>: by default this slice's own value,
        /// <see cref="AlumniRules.Reputation"/>, so a later value's terms never reach these fixtures (§14.I: every behaviour
        /// test names its value).
        /// </summary>
        private static EpisodeState AllStars(int size, uint seed, string player = null, int value = AlumniRules.Reputation) =>
            AlumniHistoriesTests.AllStars(size, seed, player, value);

        /// <summary>The shipped All-Stars season built after a change to its cast (a renamed or repeated card), enrolled, and held at this slice's value.</summary>
        private static EpisodeState Built(int size, uint seed, Action<EpisodeState> cast, string player = null)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size, Roster = CastTemplates.Roster.AllStars, PlayerTemplateId = player }, seed);
            cast(s);
            ShippedRules.ApplyFresh(s);
            Assert.That(AlumniRules.On(s) && s.alumniRulesVersion == AlumniRules.Latest, Is.True, "The season enrols: other alumni are seated.");
            s.alumniRulesVersion = AlumniRules.Reputation;
            return s;
        }

        private static string On(EpisodeState s, string templateId) => AlumniHistoriesTests.On(s, templateId);

        /// <summary>The same season held at another alumni rules value: the field alone (§14.G).</summary>
        private static EpisodeState Held(EpisodeState s, int value)
        {
            var held = s.Clone();
            held.alumniRulesVersion = value;
            return held;
        }

        /// <summary>What the past games add to one evaluator's nomination threat: <see cref="ThreatAssessment.TotalWithPastGames"/> less <see cref="ThreatAssessment.Total"/>.</summary>
        private static double PastGames(EpisodeState s, string evaluatorId, string targetId) =>
            ThreatAssessment.TotalWithPastGames(s, evaluatorId, targetId) - ThreatAssessment.Total(s, evaluatorId, targetId);

        /// <summary>The live pairs as fact id and the two houseguests.</summary>
        private static List<string> Pairs(EpisodeState s) => KnownBlocs.LivePairs(s).Select(Name).ToList();

        /// <summary>§14.D.2's definition, from the seated pairs: shared alliances whose two houseguests are both in and have not put each other up.</summary>
        private static List<string> SeatedAndLive(EpisodeState s) =>
            AlumniHistories.SeatedPairs(s, AlumniHistories.EditionOf(s))
                .Where(p => p.Fact.Standing > 0 && s.Find(p.FirstId).status == ContestantStatus.Active && s.Find(p.SecondId).status == ContestantStatus.Active
                            && AlumniLoyalty.PublicSplitWeek(s, p.FirstId, p.SecondId, includeThisWeek: true) == 0)
                .Select(Name).ToList();

        private static string Name(AlumniHistories.SeatedPair pair) => pair.Fact.Id + ":" + pair.FirstId + "+" + pair.SecondId;

        /// <summary>This week's live block: the Head of Household and their nominations, before the veto and the reveal.</summary>
        private static void Block(EpisodeState s, string hohId, params string[] nominees)
        {
            s.hohId = hohId;
            s.nominees = nominees.ToList();
            s.evictionResolved = false;
        }

        /// <summary>A pact written into the house. With no story fact it is known to everybody; <see cref="Private"/> makes it its members' alone.</summary>
        private static AllianceState Pact(EpisodeState s, string name, params string[] members)
        {
            var pact = new AllianceState { id = "alliance-test-" + name, name = name, members = members.ToList(), active = true };
            s.alliances.Add(pact);
            return pact;
        }

        /// <summary>A pact's private fact at birth, as the story rules write it: its members are its knowers.</summary>
        private static HouseFactState Private(EpisodeState s, AllianceState pact)
        {
            Knowledge.AllianceFormed(s, pact);
            return Knowledge.Of(s, FactKinds.Alliance, pact.id);
        }

        /// <summary>The voter's "threat" factor on one nominee: minus the vote's threat at the voter's weight.</summary>
        private static double ThreatFactor(WebVoteOptions options, string nomineeId) =>
            WebEvictionVoting.Evaluate(options).nomineeEvaluations.Single(e => e.nomineeId == nomineeId).factors.Single(f => f.code == "threat").value;

        /// <summary>
        /// How many points of the vote's threat the voter's alumni terms add on a nominee: the "threat" factor without them
        /// (the season held at 2) less with them, over what one more point of career costs it. Every other factor is the
        /// same at 2 and 3, and the threat is weighed linearly away from its bounds.
        /// </summary>
        private static double VoteThreatPoints(EpisodeState three, string voterId, string nomineeId)
        {
            var with = WebEvictionVoting.FromNative(three, voterId);
            var without = WebEvictionVoting.FromNative(Held(three, AlumniRules.Loyalty), voterId);
            Assert.That(without.alumniTerms, Is.Empty, "Held at 2, the vote reads no past game.");
            var onePointMore = WebEvictionVoting.FromNative(three, voterId);
            onePointMore.alumniTerms.Single(t => t.nomineeId == nomineeId).career += 1;
            double perPoint = ThreatFactor(with, nomineeId) - ThreatFactor(onePointMore, nomineeId);
            Assert.That(perPoint, Is.GreaterThan(0), "A point of threat costs the nominee.");
            return (ThreatFactor(without, nomineeId) - ThreatFactor(with, nomineeId)) / perPoint;
        }

        private static (string nomineeId, double pairRisk, double career)[] Terms(IEnumerable<WebVoteAlumniTerm> terms) =>
            terms.Select(t => (t.nomineeId, t.pairRisk, t.career)).ToArray();

        // ------------------------------------------------------------------ who is a known pair (§14.D.1-D.2)

        [Test]
        public void OnlySeatedSharedAlliancesAreKnownPairs()
        {
            // The twelve-house seats all five of edition 1's facts; two are shared alliances.
            var twelve = AllStars(12, 2201u);
            string derrick = On(twelve, Derrick), cody = On(twelve, Cody), tyler = On(twelve, Tyler), dan = On(twelve, Dan);
            Assert.That(AlumniHistories.SeatedPairs(twelve, AlumniEditions.Histories1).Count, Is.EqualTo(5));
            Assert.That(Pairs(twelve), Is.EqualTo(new[] { Hitmen + ":" + derrick + "+" + cody, Committee + ":" + cody + "+" + tyler }));
            foreach (var fact in AlumniEditions.Histories1.Pairs)
                Assert.That(KnownBlocs.Risk(fact), Is.EqualTo(fact.Id == Hitmen ? 8 : fact.Id == Committee ? 4 : 0),
                    fact.Id + ": a final-two partnership 8, another shared alliance 4, a nomination nothing.");
            // The nominations shape only their holders' receipts: no pair risk for anyone they involve.
            foreach (string template in new[] { Janelle, Danielle, Will })
                foreach (var evaluator in twelve.contestants.Where(c => c.id != On(twelve, template)))
                    Assert.That(KnownBlocs.RiskFor(twelve, evaluator.id, On(twelve, template)), Is.Zero, template + " is in no known pair.");
            Assert.That(KnownBlocs.RiskFor(twelve, On(twelve, Janelle), tyler), Is.EqualTo(4), "Tyler's nomination of Janelle is no bloc; the Committee is.");
            // Pairs, never groups: the chain through Cody is two facts, and Derrick and Tyler were never a pair.
            Assert.That(AlumniEditions.Histories1.PairOf(Derrick, Tyler), Is.Null);
            Assert.That(KnownBlocs.RiskFor(twelve, dan, derrick), Is.EqualTo(8), "Derrick sits in the Hitmen alone.");
            Assert.That(KnownBlocs.RiskFor(twelve, dan, tyler), Is.EqualTo(4), "Tyler in the Committee alone.");
            Assert.That(KnownBlocs.RiskFor(twelve, dan, cody), Is.EqualTo(12), "Cody in both, counted pair by pair.");

            // The eight-house seats the Hitmen and neither BB22 row.
            var eight = AllStars(8, 2202u);
            Assert.That(eight.contestants.Any(c => (c.sourceTemplateId ?? c.id) == Tyler), Is.False);
            Assert.That(Pairs(eight), Is.EqualTo(new[] { Hitmen + ":" + On(eight, Derrick) + "+" + On(eight, Cody) }));
            Assert.That(KnownBlocs.RiskFor(eight, On(eight, Dan), On(eight, Cody)), Is.EqualTo(8));
        }

        [Test]
        public void LivePairsAgreeWithSeatedPairs()
        {
            var houses = new List<(string name, EpisodeState s, string[] expected)>();
            var twelve = AllStars(12, 2301u);
            houses.Add(("twelve", twelve, new[] { Hitmen + ":" + Derrick + "+" + Cody, Committee + ":" + Cody + "+" + Tyler }));
            houses.Add(("eight", AllStars(8, 2302u), new[] { Hitmen + ":" + Derrick + "+" + Cody }));
            // The player on Cody's card is Cody: both pairs hold them, and Tyler takes the eighth seat.
            var card = AllStars(8, 2303u, Cody);
            string player = card.playerId;
            houses.Add(("player on Cody's card", card, new[] { Hitmen + ":" + Derrick + "+" + player, Committee + ":" + player + "+" + Tyler }));
            // A renamed card is fiction: Cody renamed is in no pair.
            var renamed = Built(12, 2304u, house => house.contestants.Single(c => c.id == Cody).name = "Cody");
            houses.Add(("renamed Cody", renamed, new string[0]));
            // Two houseguests on Tyler's card: neither is him, so the Committee is not seated.
            var repeated = Built(12, 2305u, house =>
            {
                var vanessa = house.contestants.Single(c => c.id == Vanessa);
                vanessa.sourceTemplateId = Tyler;
                vanessa.name = "Tyler Crispen";
            });
            houses.Add(("two on Tyler's card", repeated, new[] { Hitmen + ":" + Derrick + "+" + Cody }));
            // Departures and splits, filtered the same way.
            var gone = twelve.Clone();
            gone.Find(Derrick).status = ContestantStatus.Evicted;
            houses.Add(("Derrick evicted", gone, new[] { Committee + ":" + Cody + "+" + Tyler }));
            var split = twelve.Clone();
            Block(split, Tyler, Cody, Chelsie);
            houses.Add(("Tyler nominates Cody", split, new[] { Hitmen + ":" + Derrick + "+" + Cody }));
            foreach (var (name, s, expected) in houses)
            {
                Assert.That(Pairs(s), Is.EqualTo(SeatedAndLive(s)), name + ": the seated pairs, filtered.");
                Assert.That(Pairs(s), Is.EqualTo(expected), name);
            }
            Assert.That(KnownBlocs.CareerFor(card, player), Is.EqualTo(4), "The player on Cody's card has his career.");
            Assert.That(KnownBlocs.CareerFor(renamed, Cody), Is.Zero, "A renamed card has none.");
            Assert.That(KnownBlocs.CareerFor(repeated, Tyler), Is.Zero);
            Assert.That(KnownBlocs.CareerFor(repeated, Vanessa), Is.Zero, "Nor either of two on one card.");
        }

        // ------------------------------------------------------------------ what ends a known pair (§14.D.2)

        [Test]
        public void AKnownPairEndsWhenAMemberLeavesOrOneNominatesTheOther()
        {
            var s = AllStars(12, 2401u);
            string derrick = On(s, Derrick), cody = On(s, Cody), tyler = On(s, Tyler), dan = On(s, Dan), chelsie = On(s, Chelsie);
            string both = Hitmen + ":" + derrick + "+" + cody, committee = Committee + ":" + cody + "+" + tyler;
            Assert.That(Pairs(s), Is.EqualTo(new[] { both, committee }));

            // A departure, whatever kind.
            var evicted = s.Clone();
            evicted.Find(derrick).status = ContestantStatus.Evicted;
            Assert.That(Pairs(evicted), Is.EqualTo(new[] { committee }));
            Assert.That(KnownBlocs.RiskFor(evicted, dan, cody), Is.EqualTo(4));
            Assert.That(KnownBlocs.RiskFor(evicted, dan, derrick), Is.Zero);
            var jury = s.Clone();
            jury.Find(tyler).status = ContestantStatus.Jury;
            Assert.That(Pairs(jury), Is.EqualTo(new[] { both }));
            Assert.That(KnownBlocs.RiskFor(jury, dan, cody), Is.EqualTo(8));

            // This week's block, from the moment it is said, either way round.
            var named = s.Clone();
            Block(named, cody, tyler, chelsie);
            Assert.That(Pairs(named), Is.EqualTo(new[] { both }), "Cody puts Tyler up: the Committee splits in public.");
            Assert.That(KnownBlocs.RiskFor(named, dan, tyler), Is.Zero);
            Assert.That(KnownBlocs.RiskFor(named, dan, cody), Is.EqualTo(8));
            var reverse = s.Clone();
            Block(reverse, tyler, cody, chelsie);
            Assert.That(Pairs(reverse), Is.EqualTo(new[] { both }), "And Tyler putting Cody up.");
            var elsewhere = s.Clone();
            Block(elsewhere, cody, chelsie, dan);
            Assert.That(Pairs(elsewhere), Is.EqualTo(new[] { both, committee }), "Putting up somebody else splits nothing.");

            // An earlier week's ceremony: Tyler named Cody as the replacement in week 2, and it is week 4.
            var replaced = s.Clone();
            replaced.week = 4;
            replaced.ledger.power.Add(new PowerRow
            {
                week = 2, hohId = tyler, vetoHolderId = chelsie, vetoUsed = true, savedId = chelsie, replacementId = cody,
                nominees = new List<string> { cody, dan }, tally = new List<int> { 6, 3 }, evicteeId = dan,
            });
            Assert.That(Pairs(replaced), Is.EqualTo(new[] { both }), "The replacement is a nomination too.");
            // A nominee saved by the veto was put up all the same.
            var saved = s.Clone();
            saved.week = 3;
            saved.ledger.power.Add(new PowerRow
            {
                week = 1, hohId = derrick, vetoHolderId = cody, vetoUsed = true, savedId = cody, replacementId = chelsie,
                nominees = new List<string> { chelsie, dan }, tally = new List<int> { 6, 3 }, evicteeId = dan,
            });
            Assert.That(Pairs(saved), Is.EqualTo(new[] { committee }), "Derrick put Cody up in week 1, and the veto saved him.");

            // The final eviction is the last Head of Household's choice, not a nomination: its row splits nothing.
            var final = s.Clone();
            final.week = 4;
            final.ledger.power.Add(new PowerRow
            {
                week = 3, hohId = derrick, nominees = new List<string> { cody, dan }, tally = new List<int>(), evicteeId = dan,
            });
            Assert.That(Pairs(final), Is.EqualTo(new[] { both, committee }));
        }

        [Test]
        public void APrivateSplitIsNotPublic()
        {
            var s = AllStars(12, 2501u);
            string derrick = On(s, Derrick), cody = On(s, Cody), tyler = On(s, Tyler), dan = On(s, Dan);
            var before = Pairs(s);
            // The Hitmen's final two broken, a betrayal on Cody's own edge, both scores at the floor, two ballots against
            // Cody, and a pact of the two that has ended: all of it private, so the house still sees the pair.
            s.deals.Single(d => d.type == DealKind.FinalTwo).status = DealStatus.Broken;
            RelationshipLedger.RecordOneWay(s, cody, derrick, "deal_broken", -45, "Broke the final two.", permanent: true);
            s.relationships.Single(r => r.fromId == cody && r.toId == derrick).score = -100;
            s.relationships.Single(r => r.fromId == derrick && r.toId == cody).score = -100;
            s.votes.Add(new VoteState { voterId = derrick, targetId = cody });
            s.votes.Add(new VoteState { voterId = tyler, targetId = cody });
            Pact(s, "hitmen-ended", derrick, cody).active = false;
            Assert.That(AlumniLoyalty.GoodwillShare(s, cody, derrick), Is.Zero, "Cody's own loyalty has ended (§14.B), privately.");
            Assert.That(Pairs(s), Is.EqualTo(before));
            Assert.That(KnownBlocs.RiskFor(s, dan, cody), Is.EqualTo(12));
            Assert.That(KnownBlocs.RiskFor(s, dan, derrick), Is.EqualTo(8));
        }

        // ------------------------------------------------------------------ the nomination threat (§14.C, §14.D.3)

        [Test]
        public void PairRiskCountsToOutsidersInsideTheCapAndStepsAsideForAVisiblePact()
        {
            var s = AllStars(12, 2601u);
            string derrick = On(s, Derrick), cody = On(s, Cody), tyler = On(s, Tyler), dan = On(s, Dan), chelsie = On(s, Chelsie);
            var targets = new[] { derrick, cody, tyler };
            // §14.D.3's table: rows are evaluators, columns Derrick, Cody and Tyler (-1 for themselves).
            var table = new List<(string evaluator, int[] risk)>
            {
                (derrick, new[] { -1, 4, 4 }), (cody, new[] { 0, -1, 0 }), (tyler, new[] { 8, 8, -1 }),
            };
            foreach (var outsider in s.contestants.Where(c => !targets.Contains(c.id)))
                table.Add((outsider.id, new[] { 8, 12, 4 }));
            var two = Held(s, AlumniRules.Loyalty);
            foreach (var (evaluator, risk) in table)
                for (int i = 0; i < targets.Length; i++)
                {
                    if (risk[i] < 0) continue;
                    string at = evaluator + " reading " + targets[i];
                    Assert.That(KnownBlocs.RiskFor(s, evaluator, targets[i]), Is.EqualTo(risk[i]), at);
                    Assert.That(PastGames(s, evaluator, targets[i]) - KnownBlocs.CareerFor(s, targets[i]), Is.EqualTo(risk[i]).Within(Exact), at + ": in the threat.");
                    Assert.That(PastGames(two, evaluator, targets[i]), Is.Zero, at + ": nothing at 2.");
                }

            // Inside the cap of twenty: a pact of four everybody knows of (16) leaves room for four of Cody's twelve,
            // and a pact of five (20) for none. The career is in reputation, outside this cap.
            var four = s.Clone();
            Pact(four, "four", cody, On(four, Will), On(four, Janelle), On(four, Vanessa));
            Assert.That(PastGames(four, chelsie, cody), Is.EqualTo(4 + 4).Within(Exact));
            var five = s.Clone();
            Pact(five, "five", cody, On(five, Will), On(five, Janelle), On(five, Vanessa), On(five, Rachel));
            Assert.That(PastGames(five, chelsie, cody), Is.EqualTo(0 + 4).Within(Exact));

            // The Hitmen re-form, privately: Dan cannot see the pact, so it adds nothing for him and the pair still counts.
            var reformed = s.Clone();
            var pact = Pact(reformed, "hitmen", derrick, cody);
            var fact = Private(reformed, pact);
            Assert.That(KnownBlocs.RiskFor(reformed, dan, derrick), Is.EqualTo(8));
            Assert.That(ThreatAssessment.Total(reformed, dan, derrick), Is.EqualTo(ThreatAssessment.Total(s, dan, derrick)).Within(Exact));
            // Once Dan knows of it, the pact is the bloc in his reading and the pair steps aside: never counted twice.
            fact.knowers.Add(dan);
            Assert.That(KnownBlocs.RiskFor(reformed, dan, derrick), Is.Zero);
            Assert.That(KnownBlocs.RiskFor(reformed, dan, cody), Is.EqualTo(4), "The Committee still counts.");
            Assert.That(ThreatAssessment.TotalWithPastGames(reformed, dan, derrick), Is.EqualTo(ThreatAssessment.TotalWithPastGames(s, dan, derrick)).Within(Exact),
                "The pact's two heads replace the pair's 8.");
            Assert.That(KnownBlocs.RiskFor(reformed, tyler, derrick), Is.EqualTo(8), "Tyler does not know of it.");
            // An ended pact is no bloc: the pair counts again.
            pact.active = false;
            Assert.That(KnownBlocs.RiskFor(reformed, dan, derrick), Is.EqualTo(8));
        }

        [Test]
        public void CareerReputationIsPublicAndANewcomerHasNone()
        {
            var s = AllStars(12, 2701u);
            var careers = new Dictionary<string, int>
            {
                { Dan, 4 }, { Will, 4 }, { Derrick, 4 }, { Janelle, 2 }, { Cody, 4 }, { Danielle, 3 },
                { Vanessa, 2 }, { Tyler, 3 }, { Chelsie, 4 }, { Rachel, 4 }, { Xavier, 4 },
            };
            foreach (var career in careers)
                Assert.That(KnownBlocs.CareerFor(s, On(s, career.Key)), Is.EqualTo(career.Value), career.Key);
            Assert.That(KnownBlocs.CareerFor(s, s.playerId), Is.Zero, "The newcomer's reputation is what they do in the house.");
            // The record is public: every evaluator, the player included, reads the same career.
            foreach (var target in s.contestants)
                foreach (var evaluator in s.contestants.Where(c => c.id != target.id))
                    Assert.That(PastGames(s, evaluator.id, target.id) - KnownBlocs.RiskFor(s, evaluator.id, target.id),
                        Is.EqualTo(KnownBlocs.CareerFor(s, target.id)).Within(Exact), evaluator.id + " reading " + target.id);

            // The player on a card is that alumnus; renamed off it, or with a card held twice, nobody is.
            var card = AllStars(8, 2702u, Cody);
            Assert.That(KnownBlocs.CareerFor(card, card.playerId), Is.EqualTo(4));
            var offCard = Built(8, 2703u, c => c.Find(c.playerId).name = "Not Cody", Cody);
            Assert.That(KnownBlocs.CareerFor(offCard, offCard.playerId), Is.Zero);
            var renamed = Built(8, 2704u, c => c.contestants.Single(x => x.id == Danielle).name = "Danielle");
            Assert.That(KnownBlocs.CareerFor(renamed, Danielle), Is.Zero);
            var twice = Built(8, 2705u, c =>
            {
                var vanessa = c.contestants.Single(x => x.id == Vanessa);
                vanessa.sourceTemplateId = Danielle;
                vanessa.name = "Danielle Reyes";
            });
            Assert.That(KnownBlocs.CareerFor(twice, Danielle), Is.Zero);
            Assert.That(KnownBlocs.CareerFor(twice, Vanessa), Is.Zero);

            // The clamp at fifteen: three words Cody broke (8) and a wrong on Dan's own edge (trust under 35, 5) put Cody's
            // reputation with Dan at 13 before the career, so the career adds 2 of its 4.
            var clamped = s.Clone();
            string cody = On(clamped, Cody), dan = On(clamped, Dan), derrick = On(clamped, Derrick);
            for (int i = 0; i < 3; i++)
                clamped.promises.Add(new PromiseState
                {
                    id = "promise-test-" + i, fromId = cody, toId = dan, kind = PromiseKind.Information, status = PromiseStatus.Broken,
                    brokenById = cody, week = 1, settledWeek = 1,
                });
            RelationshipLedger.RecordOneWay(clamped, dan, cody, "deal_broken", -50, "A wrong.", permanent: true);
            Assert.That(ThreatAssessment.TrustScore(clamped, cody, dan), Is.LessThan(35));
            Assert.That(PastGames(clamped, dan, cody), Is.EqualTo(12 + 2).Within(Exact));
            // And at nought: Cody trusts Derrick above seventy (a pact on top of the past game and the handshake), so his
            // reputation with Cody is -3 before the career, and the career adds 1.
            var trusted = s.Clone();
            RelationshipLedger.Record(trusted, cody, derrick, "alliance-formed", 30, "They formed a pact.");
            Assert.That(ThreatAssessment.TrustScore(trusted, derrick, cody), Is.GreaterThan(70));
            Assert.That(PastGames(trusted, cody, derrick), Is.EqualTo(1).Within(Exact));
        }

        [Test]
        public void TheHouseTalkReadsNoPastGame()
        {
            var three = AllStars(12, 2801u);
            string cody = On(three, Cody), dan = On(three, Dan);
            three.Find(cody).hohWins = 1;
            three.Find(On(three, Rachel)).vetoWins = 1;
            // The player holds the power at the nominations, so the house courts them with pitches.
            three.phase = EpisodePhase.Nomination;
            three.hohId = three.playerId;
            three.nominees.Clear();
            var held = new[] { Held(three, AlumniRules.Histories), Held(three, AlumniRules.Loyalty) };
            Assert.That(ThreatAssessment.TotalWithPastGames(three, dan, cody), Is.GreaterThan(ThreatAssessment.TotalWithPastGames(held[1], dan, cody) + 15),
                "The nominations' reading moves at 3, so what follows is no accident.");
            foreach (var other in held)
            {
                string at = "at " + other.alumniRulesVersion + ": ";
                foreach (var evaluator in three.contestants)
                {
                    Assert.That(ThreatAssessment.RankedTargets(three, evaluator.id), Is.EqualTo(ThreatAssessment.RankedTargets(other, evaluator.id)),
                        at + evaluator.id + "'s ranking, which the rumour subject and the pitch target read.");
                    foreach (var target in three.contestants.Where(c => c.id != evaluator.id))
                    {
                        Assert.That(ThreatAssessment.Total(three, evaluator.id, target.id), Is.EqualTo(ThreatAssessment.Total(other, evaluator.id, target.id)),
                            at + evaluator.id + " reading " + target.id);
                        Assert.That(Grudges.ThreatScaled(three, 100, evaluator.id, target.id), Is.EqualTo(Grudges.ThreatScaled(other, 100, evaluator.id, target.id)),
                            at + "a broken word's grudge, " + evaluator.id + " holding it against " + target.id);
                    }
                }
            }
            // The pitches themselves: each courting houseguest names the same target at 1, 2 and 3.
            Assert.That(HoHPitches.Available(three), Is.True);
            var courting = three.contestants.Where(c => NpcAgendas.Of(three, c.id)?.kind == Agendas.Court).Select(c => c.id).ToList();
            Assert.That(courting, Is.Not.Empty);
            foreach (var state in held.Concat(new[] { three }))
                foreach (string npc in courting) HoHPitches.Offer(state, npc);
            List<string> Pitched(EpisodeState state) =>
                state.replyCards.Where(c => c.kind == ReplyCards.Pitch).Select(c => c.fromId + "->" + c.aboutId).ToList();
            Assert.That(Pitched(three), Has.Count.EqualTo(courting.Count));
            foreach (var other in held) Assert.That(Pitched(three), Is.EqualTo(Pitched(other)));
        }

        [Test]
        public void TheNominationThreatTermCarriesThePastGames()
        {
            var three = AllStars(12, 2901u);
            string dan = On(three, Dan), chelsie = On(three, Chelsie);
            // An outsider Head of Household with an outsider pact-mate, everybody knowing of their pact.
            Pact(three, "outsiders", dan, chelsie);
            var two = Held(three, AlumniRules.Loyalty);
            var uplift = new Dictionary<string, double>
            {
                { On(three, Cody), 0.4 * 16 + 0.2 * 16 }, { On(three, Derrick), 0.6 * 12 }, { On(three, Tyler), 0.6 * 7 },
                { On(three, Janelle), 0.6 * 2 }, { On(three, Will), 0.6 * 4 }, { three.playerId, 0 },
                { chelsie, 0.4 * 4 },
            };
            Assert.That(uplift[On(three, Cody)], Is.EqualTo(9.6).Within(Exact), "The bound: Cody's 12 pair risk and 4 career, at 0.4 and 0.2.");
            foreach (var expected in uplift)
            {
                Assert.That(EpisodeEngine.ThreatTerm(three, dan, expected.Key) - EpisodeEngine.ThreatTerm(two, dan, expected.Key),
                    Is.EqualTo(expected.Value).Within(Exact), expected.Key + ": Dan's reading and Chelsie's.");
                Assert.That(EpisodeEngine.NominationWeight(two, dan, expected.Key) - EpisodeEngine.NominationWeight(three, dan, expected.Key),
                    Is.EqualTo(expected.Value).Within(Exact), expected.Key + ": Dan is that much less reluctant to put them up, the replacement included.");
            }
            // No Head of Household, with a pact or without, reads more than the bound for anybody.
            foreach (var hoh in three.contestants.Where(c => !c.isPlayer))
                foreach (var target in three.contestants.Where(c => c.id != hoh.id))
                {
                    double more = EpisodeEngine.ThreatTerm(three, hoh.id, target.id) - EpisodeEngine.ThreatTerm(two, hoh.id, target.id);
                    Assert.That(more, Is.InRange(0.0, 9.6 + Exact), hoh.id + " on " + target.id);
                }
        }

        // ------------------------------------------------------------------ the vote (§14.D.4)

        [Test]
        public void TheVoteWeighsTheSameAlumniThreatAsTheNominations()
        {
            var s = AllStars(12, 3001u);
            string cody = On(s, Cody), derrick = On(s, Derrick), tyler = On(s, Tyler), dan = On(s, Dan), player = s.playerId;
            s.nominees = new List<string> { cody, player };
            var options = WebEvictionVoting.FromNative(s, dan);
            Assert.That(Terms(options.alumniTerms), Is.EqualTo(new[] { (cody, 12.0, 4.0) }), "Cody's pairs and career; the newcomer has no term.");
            Assert.That(Terms(KnownBlocs.VoteTerms(s, dan)), Is.EqualTo(Terms(options.alumniTerms)));
            Assert.That(Terms(KnownBlocs.VoteTerms(s, derrick)), Is.EqualTo(new[] { (cody, 4.0, 4.0) }), "Derrick reads the Committee, not his own pair.");
            Assert.That(Terms(KnownBlocs.VoteTerms(s, tyler)), Is.EqualTo(new[] { (cody, 8.0, 4.0) }));
            Assert.That(VoteThreatPoints(s, dan, cody), Is.EqualTo(16).Within(1e-6));
            Assert.That(PastGames(s, dan, cody), Is.EqualTo(16).Within(Exact), "The same 16 in the nominations.");
            var two = Held(s, AlumniRules.Loyalty);
            Assert.That(ThreatFactor(options, player), Is.EqualTo(ThreatFactor(WebEvictionVoting.FromNative(two, dan), player)), "The newcomer's threat is the web's.");
            Assert.That(WebEvictionVoting.Evaluate(options).nomineeEvaluations.SelectMany(e => e.factors.Select(f => e.nomineeId + ":" + f.code)),
                Is.EqualTo(WebEvictionVoting.Evaluate(WebEvictionVoting.FromNative(two, dan)).nomineeEvaluations.SelectMany(e => e.factors.Select(f => e.nomineeId + ":" + f.code))),
                "No factor of its own: it is part of the threat.");

            // Inside the cap of twenty, as in the nominations: a pact of four the voter knows of leaves room for 4 of 12.
            var four = s.Clone();
            Pact(four, "four", cody, On(four, Will), On(four, Janelle), On(four, Vanessa));
            Assert.That(VoteThreatPoints(four, dan, cody), Is.EqualTo(4 + 4).Within(1e-6));
            Assert.That(PastGames(four, dan, cody), Is.EqualTo(4 + 4).Within(Exact));

            // The vote has no reputation clamp of fifteen: where the nominations' clamp cuts Cody's career to 2, the vote
            // weighs all 4.
            var clamped = s.Clone();
            for (int i = 0; i < 3; i++)
                clamped.promises.Add(new PromiseState
                {
                    id = "promise-test-" + i, fromId = cody, toId = dan, kind = PromiseKind.Information, status = PromiseStatus.Broken,
                    brokenById = cody, week = 1, settledWeek = 1,
                });
            RelationshipLedger.RecordOneWay(clamped, dan, cody, "deal_broken", -50, "A wrong.", permanent: true);
            Assert.That(PastGames(clamped, dan, cody), Is.EqualTo(12 + 2).Within(Exact), "The nominations: the clamp cuts the career.");
            Assert.That(VoteThreatPoints(clamped, dan, cody), Is.EqualTo(12 + 4).Within(1e-6), "The vote: all of it.");

            // An imported web round carries no alumni term, and none is ever read from one.
            Assert.That(new WebVoteOptions().alumniTerms, Is.Not.Null.And.Empty);
            Assert.That(JsonConvert.DeserializeObject<WebVoteOptions>("{\"memories\":[]}").alumniTerms, Is.Not.Null.And.Empty);
        }

        // ------------------------------------------------------------------ below the value (§14.G)

        [Test]
        public void BelowReputationNoThreatReadsTheEdition()
        {
            var three = AllStars(12, 3101u);
            string cody = On(three, Cody), dan = On(three, Dan), chelsie = On(three, Chelsie), player = three.playerId;
            Pact(three, "outsiders", dan, chelsie);
            three.nominees = new List<string> { cody, player };
            Assert.That(Pairs(three), Has.Count.EqualTo(2), "At 3 the pairs are known.");
            Assert.That(KnownBlocs.CareerFor(three, cody), Is.EqualTo(4));
            var unknown = three.Clone();
            unknown.alumniCatalogEdition = "allstar-histories-0";
            var regular = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 12 }, 3102u);
            ShippedRules.ApplyFresh(regular);
            Assert.That(regular.alumniRulesVersion, Is.EqualTo(AlumniRules.Off));
            var npcs = regular.contestants.Where(c => !c.isPlayer).ToList();
            regular.nominees = new List<string> { npcs[0].id, npcs[1].id };
            Pact(regular, "pair", npcs[2].id, npcs[3].id);
            foreach (var (name, s) in new[] { ("at 2", Held(three, AlumniRules.Loyalty)), ("at 1", Held(three, AlumniRules.Histories)),
                         ("an edition this build does not know", unknown), ("a Regular season at 0", regular) })
            {
                Assert.That(KnownBlocs.LivePairs(s), Is.Empty, name);
                string voter = s.contestants.First(c => !c.isPlayer && !s.nominees.Contains(c.id)).id;
                Assert.That(KnownBlocs.VoteTerms(s, voter), Is.Empty, name);
                Assert.That(WebEvictionVoting.FromNative(s, voter).alumniTerms, Is.Empty, name);
                foreach (var target in s.contestants)
                {
                    Assert.That(KnownBlocs.CareerFor(s, target.id), Is.Zero, name + ": " + target.id);
                    foreach (var evaluator in s.contestants.Where(c => c.id != target.id))
                    {
                        string at = name + ": " + evaluator.id + " reading " + target.id;
                        Assert.That(KnownBlocs.RiskFor(s, evaluator.id, target.id), Is.Zero, at);
                        Assert.That(ThreatAssessment.TotalWithPastGames(s, evaluator.id, target.id), Is.EqualTo(ThreatAssessment.Total(s, evaluator.id, target.id)), at);
                        if (evaluator.isPlayer) continue;
                        // The term as NPC agency built it, bit for bit.
                        double term = EpisodeEngine.ThreatWeight * ThreatAssessment.Total(s, evaluator.id, target.id);
                        var allies = NpcAgendas.PactMates(s, evaluator.id).Where(m => m != target.id).ToList();
                        if (allies.Count > 0) term += EpisodeEngine.AlliesThreatWeight * allies.Average(m => ThreatAssessment.Total(s, m, target.id));
                        Assert.That(EpisodeEngine.ThreatTerm(s, evaluator.id, target.id), Is.EqualTo(term), at + ": the nomination term.");
                    }
                }
            }
        }
    }
}
