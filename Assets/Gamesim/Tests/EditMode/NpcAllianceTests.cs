using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Houseguests pairing up without the player.
    ///
    /// <para>Before this, <c>alliances.Add</c> appeared once in the whole simulation, on the
    /// player's path — so the voting-bloc system had only ever coordinated blocs the player built.
    /// The tests that matter most here are the two properties that make the pass safe to run every
    /// week: it spends no randomness, and the same house always pairs up the same way.</para>
    /// </summary>
    public sealed class NpcAllianceTests
    {
        // ---------------------------------------------------------------- safety

        /// <summary>
        /// The pass reads and writes state but never draws. A roll spent here would shift every
        /// competition and vote after it, which is what makes this the property to defend.
        /// </summary>
        [Test]
        public void SettlingSpendsNoRandomness()
        {
            var state = Warm(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 7u), 40);
            uint before = state.randomState;

            NpcAlliances.Settle(state);

            Assert.That(state.randomState, Is.EqualTo(before),
                "Alliance formation must not touch the season's generator.");
        }

        [Test]
        public void TheSameHousePairsUpTheSameWayEveryTime()
        {
            var first = Warm(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 10 }, 11u), 40);
            var second = Warm(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 10 }, 11u), 40);

            NpcAlliances.Settle(first);
            NpcAlliances.Settle(second);

            CollectionAssert.AreEqual(Pacts(first), Pacts(second));
        }

        [Test]
        public void SettlingLeavesAValidSeason()
        {
            var state = Warm(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 12 }, 3u), 60);
            NpcAlliances.Settle(state);
            Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.True, error);
        }

        // ---------------------------------------------------------------- the formula

        /// <summary>
        /// The source's floor: below 25 nothing happens however attractive the other terms are.
        /// </summary>
        [Test]
        public void NobodyAlliesBelowTheRelationshipFloor()
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 7u);
            var pair = state.Active.Where(c => !c.isPlayer).Take(2).ToList();
            Set(state, pair[0].id, pair[1].id, NpcAlliances.MinimumRelationship - 1);

            Assert.That(NpcAlliances.WouldPropose(state, pair[0].id, pair[1].id), Is.False);

            Set(state, pair[0].id, pair[1].id, NpcAlliances.MinimumRelationship + 40);
            Assert.That(NpcAlliances.WouldPropose(state, pair[0].id, pair[1].id), Is.True,
                "Well above the floor, with warmth on both sides, they should want to work together.");
        }

        /// <summary>
        /// Enemy of my enemy, worth fifteen a head. Two houseguests who are lukewarm about each
        /// other but share a problem should want to work together more than two who share nothing.
        /// </summary>
        [Test]
        public void SharedThreatsDrawPeopleTogether()
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 7u);
            var cast = state.Active.Where(c => !c.isPlayer).ToList();
            string a = cast[0].id, b = cast[1].id, enemy = cast[2].id, bystander = cast[3].id;

            Set(state, a, b, 30);
            Set(state, a, bystander, 30);
            double without = NpcAlliances.Desire(state, a, b);

            // Now give A and B somebody they both dislike.
            Set(state, a, enemy, NpcAlliances.DislikeLine - 5);
            Set(state, b, enemy, NpcAlliances.DislikeLine - 5);
            double with = NpcAlliances.Desire(state, a, b);

            Assert.That(with - without, Is.EqualTo(15).Within(0.001),
                "One shared threat is worth fifteen points of desire.");
        }

        /// <summary>Diminishing returns: every pact already carried costs ten points of appetite.</summary>
        [Test]
        public void EveryAllianceAlreadyCarriedMakesTheNextOneLessAttractive()
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 10 }, 7u);
            var cast = state.Active.Where(c => !c.isPlayer).ToList();
            string a = cast[0].id, b = cast[1].id;
            Set(state, a, b, 40);

            double alone = NpcAlliances.Desire(state, a, b);
            state.alliances.Add(new AllianceState
            {
                id = "existing", name = "An Earlier Pact",
                members = new List<string> { a, cast[2].id }, active = true,
            });

            Assert.That(NpcAlliances.Desire(state, a, b), Is.EqualTo(alone - 10).Within(0.001));
        }

        [Test]
        public void NobodyCarriesMoreThanThreeAlliances()
        {
            var state = Warm(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 12 }, 5u), 80);
            for (int week = 0; week < 8; week++) NpcAlliances.Settle(state);

            foreach (var houseguest in state.Active)
                Assert.That(NpcAlliances.ActiveAlliancesFor(state, houseguest.id).Count,
                    Is.LessThanOrEqualTo(NpcAlliances.MaximumEach),
                    houseguest.name + " is in too many alliances.");
        }

        // ---------------------------------------------------------------- forming and falling apart

        [Test]
        public void AWarmHouseActuallyFormsAlliances()
        {
            var state = Warm(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 10 }, 9u), 60);
            Assert.That(state.alliances, Is.Empty, "A fresh season has none.");

            NpcAlliances.Settle(state);

            Assert.That(state.alliances.Where(a => a.active), Is.Not.Empty,
                "Houseguests who all like each other should find partners.");
            Assert.That(state.alliances.All(a => a.members.Count == 2), Is.True);
        }

        /// <summary>One new pact each per week, so a warm house does not pair off in one evening.</summary>
        [Test]
        public void NobodyJoinsMoreThanOneNewAllianceInAWeek()
        {
            var state = Warm(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 12 }, 9u), 80);
            NpcAlliances.Settle(state);

            var joined = state.alliances.Where(a => a.active).SelectMany(a => a.members).ToList();
            CollectionAssert.AreEqual(joined.Distinct().OrderBy(id => id).ToList(),
                joined.OrderBy(id => id).ToList(),
                "Nobody should appear in two alliances formed on the same week.");
        }

        [Test]
        public void APactDissolvesWhenItsMembersTurnOnEachOther()
        {
            var state = Warm(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 9u), 60);
            NpcAlliances.Settle(state);
            var pact = state.alliances.First(a => a.active);
            string one = pact.members[0], other = pact.members[1];

            Set(state, one, other, NpcAlliances.SourLine - 10);
            NpcAlliances.Settle(state);

            Assert.That(pact.active, Is.False, "An alliance between two people who now dislike each other is over.");
        }

        /// <summary>
        /// Souring is not betrayal. Two people drifting apart must not put a permanent grudge on the
        /// books that neither of them earned.
        /// </summary>
        [Test]
        public void DriftingApartLeavesNoGrudge()
        {
            var state = Warm(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 9u), 60);
            NpcAlliances.Settle(state);
            var pact = state.alliances.First(a => a.active);
            string one = pact.members[0], other = pact.members[1];

            Set(state, one, other, NpcAlliances.SourLine - 10);
            NpcAlliances.Settle(state);

            Assert.That(RelationshipLedger.HoldsAGrudge(state, one, other), Is.False,
                "Nobody did anything to anybody here.");
        }

        [Test]
        public void AnAllianceLosesItsClaimWhenItsMembersLeaveTheHouse()
        {
            var state = Warm(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 9u), 60);
            NpcAlliances.Settle(state);
            var pact = state.alliances.First(a => a.active);

            state.Find(pact.members[0]).status = ContestantStatus.Jury;
            NpcAlliances.Settle(state);

            Assert.That(pact.active, Is.False, "A pact of one is not a pact.");
        }

        // ---------------------------------------------------------------- the player's alliances

        /// <summary>
        /// The player's alliance does not end on a number the player cannot know.
        ///
        /// <para>It used to. <see cref="NpcAlliances.Dissolve"/> read both directions of every pair,
        /// so the partner's private feeling toward the player ended the player's alliance in the
        /// weekly settle while the player's own reading was a warm +40 - and nothing the player
        /// could see or remember said so. The reference build's weekly check reads the player's own
        /// score toward the partner and nothing else. This began as the test that confirmed the
        /// silent ending; it now holds the rule that replaced it.</para>
        /// </summary>
        [Test]
        public void ThePartnersPrivateFeelingDoesNotEndThePlayersAlliance()
        {
            // Cool enough that no houseguest pairs up this week and muddies the picture.
            var state = Warm(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 9u), 10);
            string you = state.playerId;
            var partner = state.contestants.First(c => !c.isPlayer && c.status == ContestantStatus.Active);
            var pact = Ally(state, you, partner.id);
            OneWay(state, you, partner.id, 40);                       // the player's own reading: warm
            OneWay(state, partner.id, you, NpcAlliances.SourLine - 5); // the partner's, which is private

            int events = state.events.Count;
            int memories = state.memories.Count(m => m.ownerId == you);
            NpcSocialActions.Settle(state);   // what EpisodeEngine runs as the social week opens

            Assert.That(pact.active, Is.True,
                "The partner's private feeling is theirs. It does not end the player's alliance.");
            Assert.That(state.events.Skip(events).Where(e => e.kind == "alliance"), Is.Empty);
            Assert.That(state.memories.Count(m => m.ownerId == you), Is.EqualTo(memories));
        }

        [Test]
        public void ThePlayersOwnReadingEndsTheirAlliance()
        {
            var state = Warm(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 9u), 10);
            string you = state.playerId;
            var partner = state.contestants.First(c => !c.isPlayer && c.status == ContestantStatus.Active);
            var pact = Ally(state, you, partner.id);
            OneWay(state, you, partner.id, NpcAlliances.SourLine - 5);
            OneWay(state, partner.id, you, 40);

            NpcSocialActions.Settle(state);

            Assert.That(pact.active, Is.False,
                "A player who has come to dislike their partner is not in an alliance with them any more.");
        }

        /// <summary>
        /// Houseguests' own pacts keep the rule they had: either of them souring ends it. Only the
        /// player's alliance changed, because only there was one side's feeling a secret.
        /// </summary>
        [Test]
        public void AHouseguestsPactStillSoursOnEitherSide()
        {
            var state = Warm(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 9u), 10);
            var npcs = state.contestants.Where(c => !c.isPlayer && c.status == ContestantStatus.Active).ToArray();
            var pact = Ally(state, npcs[0].id, npcs[1].id);
            OneWay(state, npcs[0].id, npcs[1].id, 40);
            OneWay(state, npcs[1].id, npcs[0].id, NpcAlliances.SourLine - 5);   // the second member's side

            NpcSocialActions.Settle(state);

            Assert.That(pact.active, Is.False, "Either houseguest souring ends their pact, as it always has.");
        }

        // ---------------------------------------------------------------- telling the player

        /// <summary>
        /// When the player sours on their partner, the week that ends the alliance says so - through
        /// the real Continue that opens the social week, as the last line the player sees in it.
        /// </summary>
        [Test]
        public void WhenThePlayerSoursOnTheirPartnerTheWeekSaysTheAllianceFellApart()
        {
            var before = EvictionResolved(PlayerSurvives);
            string you = before.playerId;
            var partner = before.Active.First(c => !c.isPlayer);
            var pact = Ally(before, you, partner.id);
            OneWay(before, you, partner.id, NpcAlliances.SourLine - 5);
            OneWay(before, partner.id, you, 40);

            var added = OpenTheSocialWeek(before, out var after);

            Assert.That(after.alliances.Single(a => a.id == pact.id).active, Is.False);
            var notices = added.Where(e => e.kind == "alliance").ToList();
            Assert.That(notices.Select(e => e.text), Is.EqualTo(new[] { "Your alliance with " + partner.name + " has fallen apart." }),
                "One line, in words that say it ended and nothing about why or by how much.");
            Assert.That(notices[0].text.Any(char.IsDigit), Is.False, "No number reaches the player.");
            Assert.That(notices[0].audienceIds, Is.EquivalentTo(new[] { you, partner.id }),
                "It is the two of them's business, as forming and leaving an alliance are.");

            var seen = added.Where(e => Sees(after, e)).ToList();
            Assert.That(seen.Count, Is.GreaterThan(1),
                "Precondition: the step logs other lines the player sees, so being last means something.");
            Assert.That(seen.Last().sequence, Is.EqualTo(notices[0].sequence),
                "It is the last thing the player can see in the step, which is what the status line shows. "
                + "Logged where the alliance ends, it was buried under the house's own lines.");
        }

        /// <summary>
        /// A partner leaving the house ends the alliance too, and that is public - so it is said in
        /// those words, whether or not the player had also soured on them.
        /// </summary>
        [TestCase(false, TestName = "WhenThePartnerLeavesTheHouseTheWeekSaysSo(warm)")]
        [TestCase(true, TestName = "WhenThePartnerLeavesTheHouseTheWeekSaysSo(and soured)")]
        public void WhenThePartnerLeavesTheHouseTheWeekSaysSo(bool alsoSoured)
        {
            var before = EvictionResolved(PlayerSurvives);
            string you = before.playerId;
            var gone = before.contestants.Single(c => !c.isPlayer && c.status != ContestantStatus.Active);
            var pact = Ally(before, you, gone.id);
            OneWay(before, you, gone.id, alsoSoured ? NpcAlliances.SourLine - 5 : 40);
            OneWay(before, gone.id, you, 40);

            var added = OpenTheSocialWeek(before, out var after);

            Assert.That(after.alliances.Single(a => a.id == pact.id).active, Is.False);
            Assert.That(added.Where(e => e.kind == "alliance").Select(e => e.text),
                Is.EqualTo(new[] { gone.name + " has left the house, and your alliance has ended." }),
                "The public reason, and only the public reason.");
        }

        [Test]
        public void ThePartnersPrivateFeelingEndsNothingAndSaysNothingWhenTheWeekOpens()
        {
            var before = EvictionResolved(PlayerSurvives);
            string you = before.playerId;
            var partner = before.Active.First(c => !c.isPlayer);
            var pact = Ally(before, you, partner.id);
            OneWay(before, you, partner.id, 40);
            OneWay(before, partner.id, you, NpcAlliances.SourLine - 5);

            var added = OpenTheSocialWeek(before, out var after);

            Assert.That(after.alliances.Single(a => a.id == pact.id).active, Is.True);
            Assert.That(added.Where(e => e.kind == "alliance"), Is.Empty);
        }

        /// <summary>
        /// Two houseguests' pact ending is not the player's news - the recap lists every "alliance"
        /// event with no audience filter, so a line here would announce a pact the player never saw.
        /// </summary>
        [Test]
        public void AHouseguestsPactEndingIsNotThePlayersNews()
        {
            var before = EvictionResolved(PlayerSurvives);
            var npcs = before.Active.Where(c => !c.isPlayer).ToArray();
            var pact = Ally(before, npcs[0].id, npcs[1].id);
            OneWay(before, npcs[0].id, npcs[1].id, NpcAlliances.SourLine - 5);

            var added = OpenTheSocialWeek(before, out var after);

            Assert.That(after.alliances.Single(a => a.id == pact.id).active, Is.False, "Precondition: their pact ended.");
            Assert.That(added.Where(e => e.kind == "alliance"), Is.Empty);
        }

        /// <summary>
        /// Only an alliance that ends THIS week is news. One the player left, or that fell apart an
        /// earlier week, stays in the save with the player among its members - and a notice that
        /// looked for ended alliances rather than alliances that just ended would repeat it every
        /// week for the rest of the season.
        /// </summary>
        [Test]
        public void AnAllianceThatEndedEarlierIsNotToldAgain()
        {
            var before = EvictionResolved(PlayerSurvives);
            string you = before.playerId;
            var partner = before.Active.First(c => !c.isPlayer);
            var old = Ally(before, you, partner.id);
            old.active = false;                          // left, or dissolved, some earlier week
            OneWay(before, you, partner.id, NpcAlliances.SourLine - 5);

            var added = OpenTheSocialWeek(before, out _);

            Assert.That(added.Where(e => e.kind == "alliance"), Is.Empty,
                "An alliance that was already over going into the week is not news this week.");
        }

        /// <summary>Two alliances ending in one week are two lines, each in its own words.</summary>
        [Test]
        public void EveryAllianceThatEndsIsTold()
        {
            var before = EvictionResolved(PlayerSurvives);
            string you = before.playerId;
            var gone = before.contestants.Single(c => !c.isPlayer && c.status != ContestantStatus.Active);
            var soured = before.Active.First(c => !c.isPlayer);
            Ally(before, you, gone.id);
            Ally(before, you, soured.id);
            OneWay(before, you, soured.id, NpcAlliances.SourLine - 5);

            var added = OpenTheSocialWeek(before, out _);

            Assert.That(added.Where(e => e.kind == "alliance").Select(e => e.text), Is.EquivalentTo(new[]
            {
                gone.name + " has left the house, and your alliance has ended.",
                "Your alliance with " + soured.name + " has fallen apart.",
            }));
        }

        /// <summary>
        /// The notice spends nothing: no roll - one would re-roll every competition and vote after it -
        /// and no memory, which the player's gossip is drawn from. Opening the same week with and
        /// without an alliance to end leaves the generator and the player's memories identical.
        /// </summary>
        [Test]
        public void TellingThePlayerSpendsNoRollAndNoMemory()
        {
            var quiet = EvictionResolved(PlayerSurvives);
            var told = quiet.Clone();
            string you = told.playerId;
            var partner = told.Active.First(c => !c.isPlayer);
            Ally(told, you, partner.id);
            OneWay(told, you, partner.id, NpcAlliances.SourLine - 5);
            OneWay(quiet, you, partner.id, NpcAlliances.SourLine - 5);

            var saidNothing = OpenTheSocialWeek(quiet, out var afterQuiet);
            var saidIt = OpenTheSocialWeek(told, out var afterTold);

            Assert.That(saidIt.Count(e => e.kind == "alliance"), Is.EqualTo(1), "Precondition: one of them was told.");
            Assert.That(saidNothing.Count(e => e.kind == "alliance"), Is.EqualTo(0));
            Assert.That(afterTold.randomState, Is.EqualTo(afterQuiet.randomState),
                "Telling the player drew from the season's generator.");
            Assert.That(afterTold.memories.Count(m => m.ownerId == you), Is.EqualTo(afterQuiet.memories.Count(m => m.ownerId == you)),
                "Telling the player wrote them a memory.");
        }

        /// <summary>The player's own reading exactly on the sour line keeps the alliance, as the source's strict comparison does.</summary>
        [Test]
        public void ThePlayerExactlyOnTheSourLineStaysAllied()
        {
            var state = Warm(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 9u), 10);
            string you = state.playerId;
            var partner = state.contestants.First(c => !c.isPlayer && c.status == ContestantStatus.Active);
            var pact = Ally(state, you, partner.id);
            OneWay(state, you, partner.id, NpcAlliances.SourLine);

            NpcSocialActions.Settle(state);

            Assert.That(pact.active, Is.True, "On the line is not past it.");
        }

        [Test]
        public void NobodyIsToldOnceThePlayerIsOutOfTheHouse()
        {
            var before = EvictionResolved(PlayerEvicted);
            string you = before.playerId;
            Assert.That(before.Find(you).status, Is.Not.EqualTo(ContestantStatus.Active), "Precondition: the player was this week's evictee.");
            var partner = before.Active.First(c => !c.isPlayer);
            var pact = Ally(before, you, partner.id);
            OneWay(before, you, partner.id, NpcAlliances.SourLine - 5);

            var added = OpenTheSocialWeek(before, out var after);

            Assert.That(after.alliances.Single(a => a.id == pact.id).active, Is.False, "Precondition: it ended.");
            Assert.That(added.Where(e => e.kind == "alliance"), Is.Empty,
                "A juror is not playing the social week; there is nobody in the house to tell.");
        }

        // ---------------------------------------------------------------- what it feeds

        /// <summary>
        /// The whole reason this phase came first: an alliance the player never touched must be
        /// visible to the threat model, because that is what the bloc system reads.
        /// </summary>
        [Test]
        public void AnAllianceThePlayerNeverTouchedRaisesItsMembersThreat()
        {
            var state = Warm(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 10 }, 9u), 60);
            NpcAlliances.Settle(state);
            var pact = state.alliances.First(a => a.active);
            string member = pact.members[0];

            Assert.That(pact.members, Does.Not.Contain(state.playerId),
                "This fixture is about a pact formed between houseguests.");
            Assert.That(ThreatAssessment.Assess(state, state.playerId, member).Alliance, Is.EqualTo(8),
                "Two members, four points a head — the player can now see a bloc they had no part in.");
        }

        /// <summary>Forming a pact writes a permanent ledger entry, which is what trust reads.</summary>
        [Test]
        public void FormingAPactIsRememberedPermanently()
        {
            var state = Warm(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 9u), 60);
            NpcAlliances.Settle(state);
            var pact = state.alliances.First(a => a.active);
            string one = pact.members[0], other = pact.members[1];

            var entry = state.relationships
                .Single(r => r.fromId == one && r.toId == other)
                .events.Single(e => e.type == "alliance-formed");
            Assert.That(entry.decayable, Is.False, "Forming an alliance is not something that fades.");

            state.week += 20;
            Assert.That(ThreatAssessment.TrustScore(state, other, one),
                Is.GreaterThan(ThreatAssessment.NeutralTrust),
                "Twenty weeks on, they should still trust the person they allied with.");
        }

        // ---------------------------------------------------------------- fixtures

        /// <summary>A house where everybody has warmed to everybody by the given amount.</summary>
        private static EpisodeState Warm(EpisodeState state, double score)
        {
            foreach (var edge in state.relationships) edge.score = score;
            return state;
        }

        private static void Set(EpisodeState state, string from, string to, double score)
        {
            foreach (var edge in state.relationships.Where(r =>
                         (r.fromId == from && r.toId == to) || (r.fromId == to && r.toId == from)))
                edge.score = score;
        }

        // A real season each: in the first the player is still in the house after week one's
        // eviction, in the second the player was its evictee. Both are past the autonomy boundary.
        private const uint PlayerSurvives = 3;
        private const uint PlayerEvicted = 15;

        /// <summary>A real season walked through the engine to the moment an eviction has resolved.</summary>
        private static EpisodeState EvictionResolved(uint seed)
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(seed));
            for (int guard = 0; guard < 260; guard++)
            {
                var snapshot = engine.Snapshot;
                if (snapshot.phase == EpisodePhase.Eviction && snapshot.evictionResolved)
                {
                    Assert.That(NpcSocialState.AutonomyHasBegun(snapshot), Is.True, "Precondition: the settle runs.");
                    return snapshot;
                }
                var result = engine.Apply(EpisodeEngineTests.NextCommand(snapshot));
                Assert.That(result.accepted, Is.True, result.reason);
            }
            Assert.Fail("Seed " + seed + " never reached a resolved eviction.");
            return null;
        }

        /// <summary>The Continue that opens the social week, through the engine; returns what it logged.</summary>
        private static List<EpisodeEvent> OpenTheSocialWeek(EpisodeState before, out EpisodeState after)
        {
            var engine = new EpisodeEngine(before);
            var result = engine.Apply(EpisodeEngineTests.Command(before, EpisodeCommandKind.Advance));
            Assert.That(result.accepted, Is.True, result.reason);
            after = engine.Snapshot;
            Assert.That(after.phase, Is.EqualTo(EpisodePhase.Social));
            long from = before.nextSequence;
            return after.events.Where(e => e.sequence >= from).ToList();
        }

        private static bool Sees(EpisodeState state, EpisodeEvent e) =>
            e.audienceIds.Count == 0 || e.audienceIds.Contains(state.playerId);

        private static AllianceState Ally(EpisodeState state, string first, string second)
        {
            var pact = new AllianceState
            {
                id = "alliance-fixture-" + state.alliances.Count, name = "The Fixture Pact",
                members = new List<string> { first, second }, active = true,
            };
            state.alliances.Add(pact);
            return pact;
        }

        /// <summary>One direction only: what <paramref name="from"/> thinks of <paramref name="to"/>.</summary>
        private static void OneWay(EpisodeState state, string from, string to, double score)
        {
            foreach (var edge in state.relationships.Where(r => r.fromId == from && r.toId == to)) edge.score = score;
        }

        private static List<string> Pacts(EpisodeState state) => state.alliances
            .Where(a => a.active)
            .Select(a => string.Join("+", a.members.OrderBy(id => id)))
            .OrderBy(name => name)
            .ToList();
    }
}
