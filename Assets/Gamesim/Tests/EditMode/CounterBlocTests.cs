using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Alumni rules 4, <see cref="AlumniRules.CounterBlocs"/> (ALL-STAR-HISTORIES-PLAN §14.E, slice S3): the owner's
    /// direction of 2026-10-29, "if there are 3 players in an All star season known to be a preexisting alliance then the
    /// other players in the house might ally against them for numbers". While a known pair of past games stands, two
    /// houseguests outside it want each other more as partners, 7.5 a pair and two pairs at most, and members and their
    /// partners read nothing; a pact of houseguests with no common threat hunts a pair's members, the hub first, through a
    /// pact-mate still warm to them, and stops while that pact-mate still rates them at one hunt's cost, so the route alone
    /// never takes a score below nought. Never with the player as the listener; every ordinary common threat first; the
    /// score floor, the threshold, the pact cap and pacts of two untouched; and every term ends with the pair. The house's
    /// nominations follow through alumni rules 3's readings, with no term of their own.
    ///
    /// <para>Fixtures in the NPC-agency style, with no season walk: the shipped All-Stars season held at this slice's value,
    /// with scores, traits, pacts and power rows written in, read through the public readers and, for a pursuit, one beat
    /// of the all-week ladder a week. Only public members, so the file compiles in Unity's EditMode assembly and in the
    /// dotnet subset.</para>
    /// </summary>
    public sealed class CounterBlocTests
    {
        private const string Dan = "dan-gheesling", Will = "dr-will-kirby", Derrick = "derrick-levasseur", Cody = "cody-calafiore",
            Vanessa = "vanessa-rousso", Tyler = "tyler-crispen", Chelsie = "chelsie-baham", Rachel = "rachel-reilly", Xavier = "xavier-prather";

        private const double Exact = 1e-9;

        /// <summary>
        /// The shipped All-Stars season held at <paramref name="value"/>: by default this slice's own value,
        /// <see cref="AlumniRules.CounterBlocs"/>, so a later value's terms never reach these fixtures (§14.I: every behaviour
        /// test names its value).
        /// </summary>
        private static EpisodeState AllStars(int size, uint seed, string player = null, int value = AlumniRules.CounterBlocs) =>
            AlumniHistoriesTests.AllStars(size, seed, player, value);

        private static string On(EpisodeState s, string templateId) => AlumniHistoriesTests.On(s, templateId);

        /// <summary>The same season held at another alumni rules value: the field alone (§14.G).</summary>
        private static EpisodeState Held(EpisodeState s, int value)
        {
            var held = s.Clone();
            held.alumniRulesVersion = value;
            return held;
        }

        private static void Set(EpisodeState s, string from, string to, double score) =>
            s.relationships.Single(r => r.fromId == from && r.toId == to).score = score;

        /// <summary>One houseguest's view of everybody else, at one score.</summary>
        private static void Views(EpisodeState s, string fromId, double score)
        {
            foreach (var other in s.contestants.Where(c => c.id != fromId)) Set(s, fromId, other.id, score);
        }

        /// <summary>A pact written into the house. With no story fact it is known to everybody.</summary>
        private static AllianceState Pact(EpisodeState s, string name, params string[] members)
        {
            var pact = new AllianceState { id = "alliance-test-" + name, name = name, members = members.ToList(), active = true };
            s.alliances.Add(pact);
            return pact;
        }

        /// <summary>A houseguest with these traits and social 6, the stat their strategic value reads.</summary>
        private static void Temperament(EpisodeState s, string id, params string[] traits)
        {
            var who = s.Find(id);
            who.traits = traits.ToList();
            who.stats.social = 6;
        }

        /// <summary>Sets one view so that the desire it carries at alumni rules 3 is <paramref name="desire"/>: the source weighs the relationship at 0.4.</summary>
        private static void DesireAtThree(EpisodeState s, string npcId, string targetId, double desire)
        {
            double rest = NpcAlliances.Desire(Held(s, AlumniRules.Reputation), npcId, targetId) - 0.4 * s.Score(npcId, targetId);
            Set(s, npcId, targetId, (desire - rest) / 0.4);
            Assert.That(NpcAlliances.Desire(Held(s, AlumniRules.Reputation), npcId, targetId), Is.EqualTo(desire).Within(Exact));
        }

        private static (string kind, string partnerId, string targetId) Agenda(EpisodeState s, string npcId)
        {
            var agenda = NpcAgendas.Of(s, npcId);
            return (agenda?.kind, agenda?.partnerId, agenda?.targetId);
        }

        /// <summary>
        /// One week's turn of a houseguest's agenda on the all-week ladder (<see cref="NpcSocialActions.Beat"/>): the week
        /// moves on and its word is already given, so the beat goes to the pursuit, which the ladder runs once a week. The act
        /// is recorded as the engine records it.
        /// </summary>
        private static NpcActState Pursuit(EpisodeState s, string npcId)
        {
            s.week++;
            s.npcSocial.acts.Add(new NpcActState
            {
                id = s.week + "-0-word-" + npcId, kind = NpcActKinds.Promise, actorId = npcId, week = s.week, window = Windows.AfterHoH,
            });
            var act = NpcSocialActions.Beat(s, npcId, StoryRandom.Stream(s, "test:bloc-hunt:" + s.week), false, false);
            if (act != null)
            {
                act.id = s.week + "-1-pursuit-" + npcId;
                act.week = s.week;
                act.window = Windows.AfterHoH;
                s.npcSocial.acts.Add(act);
            }
            return act;
        }

        // ------------------------------------------------------------------ desire (§14.E.1)

        [Test]
        public void OutsidersWantEachOtherMoreForEveryKnownPairTheyFace()
        {
            Assert.That(KnownBlocs.CounterBlocDesire, Is.EqualTo(7.5), "Half the source's fifteen for a shared threat.");
            Assert.That(KnownBlocs.CounterBlocPairsCap, Is.EqualTo(2));
            Assert.That(AlumniRules.At(AlumniHistoriesTests.AllStars(8, 4100u), AlumniRules.CounterBlocs), Is.True,
                "Fresh All-Star seasons start at 4 or later (§14.A).");

            var eight = AllStars(8, 4101u);
            var twelve = AllStars(12, 4102u);
            var card = AllStars(8, 4103u, Cody);
            var houses = new List<(string name, EpisodeState s, string[] members, double outsiders)>
            {
                ("the eight-house, the Hitmen", eight, new[] { On(eight, Derrick), On(eight, Cody) }, 7.5),
                ("the twelve-house, the Hitmen and the Committee", twelve, new[] { On(twelve, Derrick), On(twelve, Cody), On(twelve, Tyler) }, 15),
                ("the player on Cody's card, both pairs in an eight-house", card, new[] { On(card, Derrick), card.playerId, On(card, Tyler) }, 15),
            };
            foreach (var (name, s, members, outsiders) in houses)
            {
                Assert.That(KnownBlocs.LivePairs(s), Has.Count.EqualTo(members.Length - 1), name);
                var three = Held(s, AlumniRules.Reputation);
                var below = new[] { three, Held(s, AlumniRules.Loyalty), Held(s, AlumniRules.Histories) };
                foreach (var a in s.contestants)
                    foreach (var b in s.contestants.Where(c => c.id != a.id))
                    {
                        bool outside = !members.Contains(a.id) && !members.Contains(b.id);
                        string at = name + ": " + a.id + " toward " + b.id;
                        Assert.That(KnownBlocs.CounterDesire(s, a.id, b.id), Is.EqualTo(outside ? outsiders : 0), at);
                        if (outside)
                            Assert.That(NpcAlliances.Desire(s, a.id, b.id) - NpcAlliances.Desire(three, a.id, b.id), Is.EqualTo(outsiders).Within(Exact),
                                at + ": in the desire.");
                        else
                            Assert.That(NpcAlliances.Desire(s, a.id, b.id), Is.EqualTo(NpcAlliances.Desire(three, a.id, b.id)),
                                at + ": nothing added, bit for bit.");
                        foreach (var held in below)
                            Assert.That(KnownBlocs.CounterDesire(held, a.id, b.id), Is.Zero, at + ", held at " + held.alumniRulesVersion);
                    }
            }

            // The partners: Derrick is in the Hitmen alone, but beside Cody, who is in the Committee too, so neither pair counts
            // for him, toward anybody or from anybody; Tyler likewise.
            string dan = On(twelve, Dan), vanessa = On(twelve, Vanessa);
            Assert.That(KnownBlocs.CounterDesire(twelve, On(twelve, Derrick), dan), Is.Zero);
            Assert.That(KnownBlocs.CounterDesire(twelve, vanessa, On(twelve, Tyler)), Is.Zero);
            // An outsider player is wanted more, as any outsider is.
            Assert.That(KnownBlocs.CounterDesire(eight, On(eight, Dan), eight.playerId), Is.EqualTo(7.5));
            Assert.That(KnownBlocs.CounterDesire(twelve, dan, twelve.playerId), Is.EqualTo(15));
        }

        [Test]
        public void ACounterPactFormsAtFourWhereNoneFormedAtThree()
        {
            var four = AllStars(8, 4201u);
            string dan = On(four, Dan), vanessa = On(four, Vanessa);
            // Nobody is warm enough for a pact but Dan and Vanessa, with no temperament and social 6, each view set so the
            // desire it carries at 3 sits halfway between the threshold less the Hitmen's 7.5 and the threshold.
            foreach (var who in four.contestants) Views(four, who.id, 0);
            Temperament(four, dan);
            Temperament(four, vanessa);
            double atThree = NpcAlliances.ProposeThreshold - KnownBlocs.CounterBlocDesire / 2;
            DesireAtThree(four, dan, vanessa, atThree);
            DesireAtThree(four, vanessa, dan, atThree);
            Assert.That(System.Math.Min(four.Score(dan, vanessa), four.Score(vanessa, dan)), Is.GreaterThanOrEqualTo(NpcAlliances.MinimumRelationship),
                "Over the floor, so the desire alone decides.");
            var three = Held(four, AlumniRules.Reputation);
            var gone = Held(four, AlumniRules.CounterBlocs);
            gone.Find(On(gone, Derrick)).status = ContestantStatus.Evicted;
            Assert.That(NpcAlliances.Desire(four, dan, vanessa), Is.EqualTo(atThree + KnownBlocs.CounterBlocDesire).Within(Exact));
            Assert.That(NpcAlliances.WouldPropose(three, dan, vanessa) || NpcAlliances.WouldPropose(three, vanessa, dan), Is.False,
                "At 3 neither would put it to the other.");
            Assert.That(NpcAlliances.WouldPropose(four, dan, vanessa) && NpcAlliances.WouldPropose(four, vanessa, dan), Is.True,
                "At 4 both would: they face the Hitmen.");

            NpcAlliances.Settle(three);
            Assert.That(NpcAlliances.NpcOnlyPacts(three), Is.Empty, "No pact forms at 3,");
            NpcAlliances.Settle(four);
            var pacts = NpcAlliances.NpcOnlyPacts(four);
            Assert.That(pacts, Has.Count.EqualTo(1), "one forms at 4,");
            Assert.That(pacts[0].members, Is.EquivalentTo(new[] { dan, vanessa }), "between the two outsiders.");

            // With the Hitmen gone there is nothing to answer, and the same two stay apart.
            NpcAlliances.Settle(gone);
            Assert.That(NpcAlliances.NpcOnlyPacts(gone), Is.Empty);
        }

        [Test]
        public void TheScoreFloorAndThePactCapStillHold()
        {
            // The floor: two kindred outsiders who want each other far past the threshold, the Hitmen's 7.5 and all, a hair under 25.
            var floor = AllStars(8, 4301u);
            string dan = On(floor, Dan), vanessa = On(floor, Vanessa);
            foreach (var who in floor.contestants) Views(floor, who.id, 0);
            Temperament(floor, dan, "Loyal", "Funny");
            Temperament(floor, vanessa, "Loyal", "Funny");
            Set(floor, dan, vanessa, NpcAlliances.MinimumRelationship - 0.01);
            Set(floor, vanessa, dan, NpcAlliances.MinimumRelationship - 0.01);
            Assert.That(KnownBlocs.CounterDesire(floor, dan, vanessa), Is.EqualTo(KnownBlocs.CounterBlocDesire));
            Assert.That(NpcAlliances.Desire(floor, dan, vanessa), Is.GreaterThan(NpcAlliances.ProposeThreshold + KnownBlocs.CounterBlocDesire));
            Assert.That(NpcAlliances.WouldPropose(floor, dan, vanessa) || NpcAlliances.WouldPropose(floor, vanessa, dan), Is.False,
                "Under the floor no desire makes a pact.");
            Set(floor, dan, vanessa, NpcAlliances.MinimumRelationship);
            Set(floor, vanessa, dan, NpcAlliances.MinimumRelationship);
            Assert.That(NpcAlliances.WouldPropose(floor, dan, vanessa) && NpcAlliances.WouldPropose(floor, vanessa, dan), Is.True, "On it, it does.");

            // The cap: a house that loves everybody pairs off at 4 as at 3, one pact among houseguests per three of them, one
            // each, and every pact a pair.
            foreach (int size in new[] { 8, 12 })
            {
                var four = AllStars(size, 4300u + (uint)size);
                var npcs = four.contestants.Where(c => !c.isPlayer).Select(c => c.id).ToList();
                foreach (var a in npcs)
                    foreach (var b in npcs.Where(x => x != a)) Set(four, a, b, 80);
                foreach (var s in new[] { Held(four, AlumniRules.Reputation), four })
                {
                    string at = "the " + size + "-house at " + s.alumniRulesVersion;
                    NpcAlliances.Settle(s);
                    NpcAlliances.Settle(s);
                    var pacts = NpcAlliances.NpcOnlyPacts(s);
                    Assert.That(NpcAlliances.PactCap(s), Is.EqualTo(npcs.Count / 3), at);
                    Assert.That(pacts, Has.Count.EqualTo(npcs.Count / 3), at + ": one pact per three houseguests, and no more the week after.");
                    Assert.That(pacts.All(p => p.members.Count == 2), Is.True, at + ": pacts of two.");
                    Assert.That(npcs.All(id => pacts.Count(p => p.members.Contains(id)) <= 1), Is.True, at + ": nobody in two.");
                    var unpaired = npcs.Where(id => !pacts.Any(p => p.members.Contains(id))).ToList();
                    Assert.That(unpaired.Any(a => unpaired.Any(b => b != a && NpcAlliances.WouldPropose(s, a, b))), Is.False, at + ": the house is at its share.");
                }
            }
        }

        [Test]
        public void AtTheFloorAnEightHouseCounterPactStillNeedsTemperament()
        {
            // §14.E.1's arithmetic: at the floor of 25, with neutral trust, social 6, no wins and no pact carried, desire is
            // 10 + 2.7 + 2 x compatibility, and a pact needs more than 25: compatibility 7 with no known pair to answer, 3
            // against the Hitmen, -1 against both pairs. The same two traits on both sides make the compatibility the same
            // either way round.
            var cases = new List<(int size, int value, string[] traits, int compatibility, int pairs, bool forms)>
            {
                (8, AlumniRules.Reputation, new[] { "Loyal", "Social" }, 6, 0, false),
                (8, AlumniRules.Reputation, new[] { "Loyal", "Funny" }, 7, 0, true),
                (8, AlumniRules.CounterBlocs, new[] { "Social" }, 2, 1, false),
                (8, AlumniRules.CounterBlocs, new[] { "Funny" }, 3, 1, true),
                (12, AlumniRules.CounterBlocs, new[] { "Manipulative", "Introverted" }, -2, 2, false),
                (12, AlumniRules.CounterBlocs, new[] { "Sneaky" }, -1, 2, true),
            };
            uint seed = 4400u;
            foreach (var c in cases)
            {
                var s = AllStars(c.size, ++seed, value: c.value);
                string dan = On(s, Dan), vanessa = On(s, Vanessa);
                string at = "the " + c.size + "-house at " + c.value + ", compatibility " + c.compatibility;
                Views(s, dan, 0);
                Views(s, vanessa, 0);
                Temperament(s, dan, c.traits);
                Temperament(s, vanessa, c.traits);
                Set(s, dan, vanessa, NpcAlliances.MinimumRelationship);
                Set(s, vanessa, dan, NpcAlliances.MinimumRelationship);
                Assert.That(TraitAffinity.Compatibility(s.Find(dan), s.Find(vanessa)), Is.EqualTo(c.compatibility), at);
                Assert.That(TraitAffinity.Compatibility(s.Find(vanessa), s.Find(dan)), Is.EqualTo(c.compatibility), at);
                Assert.That(ThreatAssessment.TrustScore(s, vanessa, dan), Is.EqualTo(ThreatAssessment.NeutralTrust), at + ": no past game between them.");
                Assert.That(ThreatAssessment.TrustScore(s, dan, vanessa), Is.EqualTo(ThreatAssessment.NeutralTrust), at);
                double expected = NpcAlliances.MinimumRelationship * 0.4 + 6 * 3 * 0.15 + EpisodeEngine.DesirePerPoint * c.compatibility
                                  + KnownBlocs.CounterBlocDesire * c.pairs;
                Assert.That(NpcAlliances.Desire(s, dan, vanessa), Is.EqualTo(expected).Within(Exact), at);
                Assert.That(NpcAlliances.Desire(s, vanessa, dan), Is.EqualTo(expected).Within(Exact), at);
                Assert.That(NpcAlliances.WouldPropose(s, dan, vanessa) && NpcAlliances.WouldPropose(s, vanessa, dan), Is.EqualTo(c.forms), at);
            }
        }

        // ------------------------------------------------------------------ the bloc hunt (§14.E.2)

        [Test]
        public void AnOutsidePactHuntsTheHubFirst()
        {
            var s = AllStars(12, 4501u);
            string dan = On(s, Dan), chelsie = On(s, Chelsie), derrick = On(s, Derrick), cody = On(s, Cody), tyler = On(s, Tyler);
            Pact(s, "outsiders", dan, chelsie);
            Views(s, dan, 10);
            Views(s, chelsie, 10);
            Assert.That(NpcDeals.CommonThreat(s, dan, chelsie), Is.Null, "Nobody disliked and no competition beast: no common threat.");
            Assert.That((KnownBlocs.RiskFor(s, dan, cody), KnownBlocs.RiskFor(s, dan, derrick), KnownBlocs.RiskFor(s, dan, tyler)), Is.EqualTo((12, 8, 4)));
            Assert.That(Agenda(s, dan), Is.EqualTo((Agendas.Hunt, chelsie, cody)), "The hub first: Cody sits in both pairs.");
            Assert.That(NpcAgendas.Describe(s, dan, NpcAgendas.Of(s, dan)),
                Is.EqualTo(s.Find(dan).name + " wants " + s.Find(cody).name + " out, with " + s.Find(chelsie).name), "The read's own words: no new line.");
            Assert.That(Agenda(Held(s, AlumniRules.Reputation), dan), Is.EqualTo((Agendas.Hold, chelsie, (string)null)), "At 3 the pact holds tight.");

            // The stop is the listener's view, at one hunt's cost and over.
            Assert.That(KnownBlocs.HuntStopLine, Is.EqualTo(-EpisodeEngine.HuntImpact).And.EqualTo(6));
            Set(s, chelsie, cody, KnownBlocs.HuntStopLine);
            Assert.That(Agenda(s, dan).targetId, Is.EqualTo(cody), "At one hunt's cost Chelsie can still be told.");
            Set(s, chelsie, cody, KnownBlocs.HuntStopLine - 0.01);
            Assert.That(Agenda(s, dan), Is.EqualTo((Agendas.Hunt, chelsie, derrick)), "Cooled on Cody, she hears about Derrick (8),");
            Set(s, chelsie, derrick, KnownBlocs.HuntStopLine - 0.01);
            Assert.That(Agenda(s, dan), Is.EqualTo((Agendas.Hunt, chelsie, tyler)), "then Tyler (4),");
            Set(s, chelsie, tyler, KnownBlocs.HuntStopLine - 0.01);
            Assert.That(Agenda(s, dan), Is.EqualTo((Agendas.Hold, chelsie, (string)null)), "and then the pact holds tight.");
            Assert.That(KnownBlocs.CounterTarget(s, dan, chelsie), Is.Null);

            // The listener's warmth, not the hunter's.
            var listener = s.Clone();
            Views(listener, chelsie, 10);
            Set(listener, dan, cody, 0);
            Assert.That(Agenda(listener, dan).targetId, Is.EqualTo(cody), "Dan cold on Cody and Chelsie warm: Cody.");
            Set(listener, dan, cody, 15);
            Set(listener, chelsie, cody, 0);
            Assert.That(Agenda(listener, dan).targetId, Is.EqualTo(derrick), "Dan warm on Cody and Chelsie cold: not Cody.");

            // The order is the hunter's reading, not the listener's: the Hitmen re-form privately and only Chelsie hears of
            // it, so in her reading their pact is the bloc and the pair steps aside, while in Dan's it still counts.
            var reading = s.Clone();
            Views(reading, chelsie, 10);
            Set(reading, chelsie, cody, 0);
            var hitmen = Pact(reading, "hitmen", derrick, cody);
            Knowledge.AllianceFormed(reading, hitmen);
            Knowledge.Of(reading, FactKinds.Alliance, hitmen.id).knowers.Add(chelsie);
            Assert.That((KnownBlocs.RiskFor(reading, dan, derrick), KnownBlocs.RiskFor(reading, dan, tyler)), Is.EqualTo((8, 4)));
            Assert.That((KnownBlocs.RiskFor(reading, chelsie, derrick), KnownBlocs.RiskFor(reading, chelsie, tyler)), Is.EqualTo((0, 4)));
            Assert.That(Agenda(reading, dan), Is.EqualTo((Agendas.Hunt, chelsie, derrick)),
                "Chelsie cold on Cody: Dan's reading puts Derrick (8) before Tyler (4), though hers would not.");

            // The eight-house's Hitmen read 8 each, and the tie goes by id: Cody first.
            var eight = AllStars(8, 4502u);
            string eDan = On(eight, Dan), eVanessa = On(eight, Vanessa);
            Pact(eight, "outsiders", eDan, eVanessa);
            Views(eight, eDan, 10);
            Views(eight, eVanessa, 10);
            Assert.That(KnownBlocs.RiskFor(eight, eDan, On(eight, Cody)), Is.EqualTo(KnownBlocs.RiskFor(eight, eDan, On(eight, Derrick))));
            Assert.That(Agenda(eight, eDan), Is.EqualTo((Agendas.Hunt, eVanessa, On(eight, Cody))));

            // A pact with one of a pair never hunts it, nor the other pair of his partner: in the twelve-house Dan's pact with
            // Derrick holds on both sides, though the Committee holds neither of them.
            var inside = AllStars(12, 4503u);
            string iDan = On(inside, Dan), iDerrick = On(inside, Derrick);
            Pact(inside, "with derrick", iDan, iDerrick);
            Views(inside, iDan, 10);
            Views(inside, iDerrick, 10);
            Assert.That(KnownBlocs.LivePairs(inside), Has.Count.EqualTo(2));
            Assert.That(Agenda(inside, iDan), Is.EqualTo((Agendas.Hold, iDerrick, (string)null)),
                "Derrick is in the Hitmen and beside Cody, who is in the Committee: no pair counts for a pact he is in.");
            Assert.That(Agenda(inside, iDerrick), Is.EqualTo((Agendas.Hold, iDan, (string)null)));
        }

        [Test]
        public void AnOutsidePactNeverHuntsAPactMate()
        {
            // The player on Cody's card, taken into two outsiders' pact as an accepted invitation takes them: the hub is
            // their own pact-mate, so each hunts Derrick through the other.
            var card = AllStars(8, 4511u, Cody);
            string cDan = On(card, Dan), cVanessa = On(card, Vanessa), cDerrick = On(card, Derrick), player = card.playerId;
            Pact(card, "invited", cDan, cVanessa, player);
            Views(card, cDan, 10);
            Views(card, cVanessa, 10);
            Assert.That(KnownBlocs.RiskFor(card, cDan, player), Is.EqualTo(12), "The player is still the hub in Dan's reading,");
            Assert.That(Agenda(card, cDan), Is.EqualTo((Agendas.Hunt, cVanessa, cDerrick)), "but a pact-mate: Derrick (8) is next.");
            Assert.That(Agenda(card, cVanessa), Is.EqualTo((Agendas.Hunt, cDan, cDerrick)));

            // A story's trio that holds Cody passes him over and hunts on past him.
            var twelve = AllStars(12, 4512u);
            string dan = On(twelve, Dan), chelsie = On(twelve, Chelsie), cody = On(twelve, Cody), derrick = On(twelve, Derrick), tyler = On(twelve, Tyler);
            Views(twelve, dan, 10);
            Views(twelve, chelsie, 10);
            var trio = twelve.Clone();
            Pact(trio, "story three", dan, chelsie, cody);
            Assert.That(NpcDeals.CommonThreat(trio, dan, chelsie), Is.Null);
            Assert.That(Agenda(trio, dan), Is.EqualTo((Agendas.Hunt, chelsie, derrick)), "Cody is in the pact: Derrick (8) is next.");

            // The player's pact with Dan and Chelsie, and no other: the hub.
            var house = twelve.Clone();
            Pact(house, "with you", house.playerId, dan, chelsie);
            Assert.That(Agenda(house, dan), Is.EqualTo((Agendas.Hunt, chelsie, cody)));

            // The hunter's own pact with Cody beside it: he never hunts his partner, until that pact has ended.
            var his = house.Clone();
            var npcOnly = Pact(his, "dan and cody", dan, cody);
            Assert.That(Agenda(his, dan), Is.EqualTo((Agendas.Hunt, chelsie, derrick)), "Dan's own partner: Derrick is next.");
            npcOnly.active = false;
            Assert.That(Agenda(his, dan), Is.EqualTo((Agendas.Hunt, chelsie, cody)), "An ended pact is no bar: the hub again.");

            // The listener's pact with Cody, which Dan can see: he never asks her to turn on her own partner. A pact of hers he
            // has not heard of stays out of his reading, until he hears of it.
            var hers = house.Clone();
            var theirs = Pact(hers, "chelsie and cody", chelsie, cody);
            Assert.That(Agenda(hers, dan), Is.EqualTo((Agendas.Hunt, chelsie, derrick)), "Her partner, in the open: Derrick is next.");
            Knowledge.AllianceFormed(hers, theirs);
            Assert.That(Agenda(hers, dan), Is.EqualTo((Agendas.Hunt, chelsie, cody)), "Her partner, privately: Dan cannot know.");
            Knowledge.Of(hers, FactKinds.Alliance, theirs.id).knowers.Add(dan);
            Assert.That(Agenda(hers, dan), Is.EqualTo((Agendas.Hunt, chelsie, derrick)), "Once he knows: Derrick.");

            // A pact of others' that Dan can see is no bar: the Hitmen re-formed in the open are a bloc of their own, so their
            // pair steps aside in his reading, and Cody, still in the Committee, ties Tyler at 4 and comes first by id.
            var others = house.Clone();
            Pact(others, "hitmen", derrick, cody);
            Assert.That((KnownBlocs.RiskFor(others, dan, derrick), KnownBlocs.RiskFor(others, dan, cody), KnownBlocs.RiskFor(others, dan, tyler)),
                Is.EqualTo((0, 4, 4)));
            Assert.That(string.CompareOrdinal(cody, tyler), Is.Negative);
            Assert.That(Agenda(others, dan), Is.EqualTo((Agendas.Hunt, chelsie, cody)));

            // Below 4 every one of them holds tight.
            foreach (var (name, s, npcId) in new[] { ("card", card, cDan), ("trio", trio, dan), ("his", his, dan), ("hers", hers, dan) })
                Assert.That(Agenda(Held(s, AlumniRules.Reputation), npcId).kind, Is.EqualTo(Agendas.Hold), name);
        }

        [Test]
        public void TheBlocHuntNeverTakesAListenerBelowZero()
        {
            var s = AllStars(8, 4601u);
            string dan = On(s, Dan), vanessa = On(s, Vanessa), cody = On(s, Cody), derrick = On(s, Derrick);
            Pact(s, "outsiders", dan, vanessa);
            Views(s, dan, 10);
            Views(s, vanessa, 10);
            Set(s, vanessa, cody, 20);
            Set(s, vanessa, derrick, KnownBlocs.HuntStopLine);
            double codyBefore = s.Score(cody, vanessa), derrickBefore = s.Score(derrick, vanessa);

            var hunted = new List<string>();
            for (int i = 0; i < 8; i++)
            {
                var act = Pursuit(s, dan);
                string at = "Week " + s.week;
                Assert.That(act, Is.Not.Null, at + ": the agenda's pursuit.");
                if (act.kind == NpcActKinds.Hunt)
                {
                    Assert.That(act.partnerId, Is.EqualTo(vanessa), at);
                    hunted.Add(act.subjectId);
                }
                else Assert.That((act.kind, act.partnerId), Is.EqualTo((NpcActKinds.Hold, vanessa)), at + ": nobody left to hunt, the pact holds tight.");
                Assert.That(s.Score(vanessa, cody), Is.GreaterThanOrEqualTo(0), at);
                Assert.That(s.Score(vanessa, derrick), Is.GreaterThanOrEqualTo(0), at);
            }
            Assert.That(hunted, Is.EqualTo(new[] { cody, cody, cody, derrick }), "Cody at 20, 14 and 8, then Derrick at 6, and no more.");
            Assert.That(s.Score(vanessa, cody), Is.EqualTo(2).Within(Exact));
            Assert.That(s.Score(vanessa, derrick), Is.EqualTo(0).Within(Exact), "The stop is one hunt's cost: never below nought.");
            Assert.That(codyBefore - s.Score(cody, vanessa), Is.EqualTo(18).Within(Exact), "The target's view falls with it, by less than the listener's 20,");
            Assert.That(derrickBefore - s.Score(derrick, vanessa), Is.EqualTo(6).Within(Exact), "and by no more than the listener's 6.");
            Assert.That(Agenda(s, dan), Is.EqualTo((Agendas.Hold, vanessa, (string)null)), "Hold resumes.");
        }

        [Test]
        public void TheBlocRouteNeverHuntsWithThePlayerAsListener()
        {
            var s = AllStars(8, 4701u);
            string dan = On(s, Dan), vanessa = On(s, Vanessa), will = On(s, Will), cody = On(s, Cody), derrick = On(s, Derrick), player = s.playerId;
            Views(s, dan, 10);
            Views(s, vanessa, 10);
            Views(s, player, 30);

            // A pact with the player alone: the player is warm to the whole house, Cody included, and is never told.
            var withYou = s.Clone();
            Pact(withYou, "you and dan", dan, player);
            Assert.That(KnownBlocs.CounterTarget(withYou, dan, player), Is.EqualTo(cody), "The reading itself would name Cody,");
            Assert.That(Agenda(withYou, dan), Is.EqualTo((Agendas.Hold, player, (string)null)), "but the agenda never asks it with the player listening.");

            // A trio with the player closest to Dan: the bloc route passes the player over and tells Vanessa.
            var trio = s.Clone();
            Pact(trio, "trio", dan, player, vanessa);
            Set(trio, dan, player, 40);
            Assert.That(Agenda(trio, dan), Is.EqualTo((Agendas.Hunt, vanessa, cody)));
            Set(trio, vanessa, cody, 0);
            Set(trio, vanessa, derrick, 0);
            Assert.That(Agenda(trio, dan), Is.EqualTo((Agendas.Hold, player, (string)null)), "Vanessa cold on both: it holds, and never hunts through the player.");

            // The ordinary common threat is told to the player, as it always was.
            var common = withYou.Clone();
            Set(common, dan, will, -15);
            Set(common, player, will, -10);
            Assert.That(Agenda(common, dan), Is.EqualTo((Agendas.Hunt, player, will)));

            // The player on a pair's card can be the target: on Cody's card the player is the hub.
            var card = AllStars(8, 4702u, Cody);
            string cDan = On(card, Dan), cVanessa = On(card, Vanessa);
            Pact(card, "outsiders", cDan, cVanessa);
            Views(card, cDan, 10);
            Views(card, cVanessa, 10);
            Assert.That(KnownBlocs.RiskFor(card, cDan, card.playerId), Is.EqualTo(12));
            Assert.That(Agenda(card, cDan), Is.EqualTo((Agendas.Hunt, cVanessa, card.playerId)));
        }

        [Test]
        public void TheOrdinaryCommonThreatStillComesFirst()
        {
            var s = AllStars(12, 4801u);
            string dan = On(s, Dan), chelsie = On(s, Chelsie), xavier = On(s, Xavier), will = On(s, Will), rachel = On(s, Rachel), cody = On(s, Cody);
            Pact(s, "three", dan, chelsie, xavier);
            Views(s, dan, 10);
            Views(s, chelsie, 10);
            Views(s, xavier, 10);
            Set(s, dan, xavier, 40);
            Set(s, dan, chelsie, 30);
            Assert.That(Agenda(s, dan), Is.EqualTo((Agendas.Hunt, xavier, cody)), "No common threat: the bloc, through the closest pact-mate.");
            Assert.That(Agenda(Held(s, AlumniRules.Reputation), dan), Is.EqualTo((Agendas.Hold, xavier, (string)null)), "At 3 the pact holds tight.");

            // A competition beast neither is warm to: the common threat, before the bloc, with the same pact-mate.
            var beast = s.Clone();
            beast.Find(rachel).hohWins = 2;
            Assert.That(Agenda(beast, dan), Is.EqualTo((Agendas.Hunt, xavier, rachel)));

            // A common threat through the farther pact-mate still comes before the bloc through the closer one.
            var farther = s.Clone();
            Set(farther, dan, will, -15);
            Set(farther, chelsie, will, -10);
            Assert.That(NpcDeals.CommonThreat(farther, dan, xavier), Is.Null);
            Assert.That(Agenda(farther, dan), Is.EqualTo((Agendas.Hunt, chelsie, will)), "Every common threat comes before any bloc.");
        }

        // ------------------------------------------------------------------ bounds and what follows (§14.E.2-E.3)

        [Test]
        public void TheCounterBlocEndsWithTheBloc()
        {
            var s = AllStars(12, 4901u);
            string dan = On(s, Dan), vanessa = On(s, Vanessa), chelsie = On(s, Chelsie), derrick = On(s, Derrick), cody = On(s, Cody), tyler = On(s, Tyler);
            Pact(s, "outsiders", dan, chelsie);
            Views(s, dan, 10);
            Views(s, chelsie, 10);
            Assert.That(KnownBlocs.CounterDesire(s, dan, vanessa), Is.EqualTo(15));
            Assert.That(Agenda(s, dan), Is.EqualTo((Agendas.Hunt, chelsie, cody)));

            // One departure: Derrick leaves and the Hitmen end with him. The Committee stands: 7.5, and Cody before Tyler by id.
            var gone = s.Clone();
            gone.Find(derrick).status = ContestantStatus.Evicted;
            Assert.That(KnownBlocs.CounterDesire(gone, dan, vanessa), Is.EqualTo(7.5));
            Assert.That(Agenda(gone, dan), Is.EqualTo((Agendas.Hunt, chelsie, cody)));

            // And a public split: Tyler, Head of Household in week 2, put Cody up, and Derrick went home. No pair stands.
            var over = s.Clone();
            over.week = 3;
            over.ledger.power.Add(new PowerRow
            {
                week = 2, hohId = tyler, nominees = new List<string> { cody, derrick }, tally = new List<int> { 2, 7 }, evicteeId = derrick,
            });
            over.Find(derrick).status = ContestantStatus.Evicted;
            Assert.That(KnownBlocs.LivePairs(over), Is.Empty);
            var three = Held(over, AlumniRules.Reputation);
            foreach (var a in over.Active)
                foreach (var b in over.Active.Where(c => c.id != a.id))
                {
                    string at = a.id + " and " + b.id;
                    Assert.That(KnownBlocs.CounterDesire(over, a.id, b.id), Is.Zero, at);
                    Assert.That(NpcAlliances.Desire(over, a.id, b.id), Is.EqualTo(NpcAlliances.Desire(three, a.id, b.id)), at + ": the desire of 3, bit for bit.");
                    Assert.That(KnownBlocs.CounterTarget(over, a.id, b.id), Is.Null, at);
                }
            Assert.That(Agenda(over, dan), Is.EqualTo((Agendas.Hold, chelsie, (string)null)), "The hunt is over: the pact holds tight.");

            // The eight-house's one pair, ended by this week's block: Derrick puts Cody up, and from that moment nothing counts.
            var eight = AllStars(8, 4902u);
            string eDan = On(eight, Dan), eVanessa = On(eight, Vanessa);
            Pact(eight, "outsiders", eDan, eVanessa);
            Views(eight, eDan, 10);
            Views(eight, eVanessa, 10);
            Assert.That(KnownBlocs.CounterDesire(eight, eDan, eVanessa), Is.EqualTo(7.5));
            Assert.That(Agenda(eight, eDan).kind, Is.EqualTo(Agendas.Hunt));
            var named = eight.Clone();
            named.phase = EpisodePhase.Campaign;
            named.hohId = On(named, Derrick);
            named.nominees = new List<string> { On(named, Cody), On(named, Will) };
            named.evictionResolved = false;
            Assert.That(KnownBlocs.CounterDesire(named, eDan, eVanessa), Is.Zero, "The Hitmen split in public at the nominations.");
            Assert.That(NpcAlliances.Desire(named, eDan, eVanessa), Is.EqualTo(NpcAlliances.Desire(Held(named, AlumniRules.Reputation), eDan, eVanessa)));
            Assert.That(Agenda(named, eDan), Is.EqualTo((Agendas.Hold, eVanessa, (string)null)));
        }

        [Test]
        public void AnOutsideHeadOfHouseholdPutsABlocMemberUpOverAnEqualOutsider()
        {
            var s = AllStars(12, 5001u);
            string dan = On(s, Dan), chelsie = On(s, Chelsie), cody = On(s, Cody), rachel = On(s, Rachel);
            Pact(s, "outsiders", dan, chelsie);
            Views(s, dan, 10);
            Views(s, chelsie, 10);
            // Rachel made Cody's equal in everything this season shows: his stats, no wins, the same standing with everybody.
            s.Find(rachel).stats = s.Find(cody).stats.Clone();
            foreach (var other in s.contestants.Where(c => c.id != cody && c.id != rachel)) Set(s, other.id, rachel, s.Score(other.id, cody));
            Set(s, cody, rachel, s.Score(rachel, cody));
            foreach (string evaluator in new[] { chelsie, dan })
                Assert.That(ThreatAssessment.Total(s, evaluator, rachel), Is.EqualTo(ThreatAssessment.Total(s, evaluator, cody)).Within(Exact),
                    "Equal in this season's threat, as " + evaluator + " reads it.");
            Assert.That(KnownBlocs.CareerFor(s, rachel), Is.EqualTo(KnownBlocs.CareerFor(s, cody)), "Both won their seasons.");

            EpisodeState Crowned(EpisodeState from)
            {
                var hoh = from.Clone();
                hoh.phase = EpisodePhase.Nomination;
                hoh.hohId = chelsie;
                hoh.nominees.Clear();
                hoh.evictionResolved = false;
                return hoh;
            }
            double Gap(EpisodeState hoh) => EpisodeEngine.NominationWeight(hoh, chelsie, rachel) - EpisodeEngine.NominationWeight(hoh, chelsie, cody);

            Assert.That(Gap(Crowned(Held(s, AlumniRules.Loyalty))), Is.EqualTo(0).Within(Exact), "At 2 Chelsie as Head of Household cannot tell them apart.");
            var four = Crowned(s);
            double gap = Gap(four);
            Assert.That(gap, Is.EqualTo((EpisodeEngine.ThreatWeight + EpisodeEngine.AlliesThreatWeight) * 12).Within(Exact),
                "At 4 she and her pact-mate read Cody's two pairs: he goes up first.");
            var threeCrowned = Crowned(Held(s, AlumniRules.Reputation));
            foreach (var candidate in four.Active.Where(c => c.id != chelsie))
                Assert.That(EpisodeEngine.NominationWeight(four, chelsie, candidate.id), Is.EqualTo(EpisodeEngine.NominationWeight(threeCrowned, chelsie, candidate.id)),
                    candidate.id + ": no nomination term of its own at 4.");

            // The hunt: Dan tells Chelsie that Cody has to go, and her reluctance to name him falls with her view of him.
            double before = s.Score(chelsie, cody);
            var act = Pursuit(s, dan);
            Assert.That((act.kind, act.partnerId, act.subjectId), Is.EqualTo((NpcActKinds.Hunt, chelsie, cody)));
            Assert.That(s.Score(chelsie, cody), Is.EqualTo(before + EpisodeEngine.HuntImpact).Within(Exact));
            var hunted = Crowned(s);
            Assert.That(Gap(hunted), Is.GreaterThan(gap).And.EqualTo(gap - EpisodeEngine.HuntImpact).Within(0.1),
                "Six further apart, less what the hunt costs Cody's standing with the house.");
            var ranked = hunted.Active.Where(c => c.id != chelsie).OrderBy(c => EpisodeEngine.NominationWeight(hunted, chelsie, c.id)).Select(c => c.id).ToList();
            Assert.That(ranked.IndexOf(cody), Is.LessThan(ranked.IndexOf(rachel)), "Cody goes up before his equal.");
        }
    }
}
