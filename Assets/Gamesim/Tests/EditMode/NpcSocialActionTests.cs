using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Houseguests spending their own social turns.
    ///
    /// <para>Two things here are worth more than the rest. The first is that this pass, unlike every
    /// other NPC pass, <b>spends the season's randomness</b> — and it has to, because the source
    /// chooses conversation partners by weighted sampling and a deterministic version of that would
    /// have everyone talk to their best friend forever. The second is that the house may now act on
    /// the player, so everything it does to them has to be something they can see.</para>
    /// </summary>
    public sealed class NpcSocialActionTests
    {
        // ---------------------------------------------------------------- personality

        /// <summary>The source's <c>getTraitPreferences</c>, case for case.</summary>
        [TestCase("Strategic", NpcActionKind.AllianceProposal, NpcActionKind.SpreadInfo, NpcActionKind.Talk)]
        [TestCase("Loyal", NpcActionKind.AllianceMeeting, NpcActionKind.Promise, NpcActionKind.Talk)]
        [TestCase("Social", NpcActionKind.Talk, NpcActionKind.AllianceMeeting, NpcActionKind.Promise)]
        [TestCase("Paranoid", NpcActionKind.Eavesdrop, NpcActionKind.Talk, NpcActionKind.SpreadInfo)]
        [TestCase("Floater", NpcActionKind.Talk, NpcActionKind.AllianceMeeting)]
        [TestCase("Confrontational", NpcActionKind.Confront, NpcActionKind.Talk, NpcActionKind.SpreadInfo)]
        public void TheRepertoireIsTheSourcesOwnTable(string trait, params NpcActionKind[] expected)
        {
            CollectionAssert.AreEqual(expected, NpcSocialActions.Repertoire(trait));
        }

        /// <summary>
        /// The gap between the two trait tables, pinned so it stays visible.
        ///
        /// <para>Nine of this project's seventeen traits are not among the source's ten and fall to
        /// the default repertoire. This is a record of a real shortfall, not an assertion that the
        /// shortfall is correct — if somebody maps these traits deliberately, this test is the one
        /// they should be changing.</para>
        /// </summary>
        [TestCase("Charming")] [TestCase("Funny")] [TestCase("Manipulative")]
        [TestCase("Impulsive")] [TestCase("Deceptive")] [TestCase("Introverted")]
        [TestCase("Stubborn")] [TestCase("Flexible")] [TestCase("Intuitive")]
        public void TraitsThisProjectHasAndTheSourceDoesNotFallToTheDefault(string trait)
        {
            CollectionAssert.AreEqual(NpcSocialActions.Repertoire(null), NpcSocialActions.Repertoire(trait),
                trait + " has no repertoire in the source, so it plays the default one.");
        }

        [Test]
        public void TheLeadTraitIsTheOnlyOneRead()
        {
            var npc = new ContestantState { traits = new List<string> { "Sneaky", "Loyal" } };
            Assert.That(NpcSocialActions.LeadTrait(npc), Is.EqualTo("Sneaky"));
            Assert.That(NpcSocialActions.LeadTrait(new ContestantState()), Is.Null);
        }

        // ---------------------------------------------------------------- the verbs

        /// <summary>
        /// The one thing that makes this pass spend randomness at all: friends are likelier, not
        /// certain. If this ever passes with a single distinct partner, the sampling has collapsed
        /// into argmax and the house has gone back to talking to one person.
        /// </summary>
        [Test]
        public void TalkingPrefersFriendsWithoutAlwaysChoosingThem()
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            string favourite = null;
            for (uint seed = 1; seed <= 240; seed++)
            {
                var state = House(seed, out string actor);
                var others = state.Active.Where(c => c.id != actor).Select(c => c.id).ToList();
                favourite = others[0];
                foreach (string id in others) Set(state, actor, id, id == favourite ? 90 : -50);

                var before = Scores(state, actor);
                NpcSocialActions.Perform(state, actor, NpcActionKind.Talk);
                string spoken = Scores(state, actor).Single(pair => pair.Value > before[pair.Key]).Key;
                counts[spoken] = counts.TryGetValue(spoken, out int held) ? held + 1 : 1;
            }

            Assert.That(counts[favourite], Is.GreaterThan(120), "The house should gravitate to friends.");
            Assert.That(counts.Count, Is.GreaterThan(1), "…and still surprise. This is weighted sampling, not argmax.");
        }

        [Test]
        public void NobodyPicksAFightWithSomebodyTheyGetOnWith()
        {
            var state = House(5u, out string actor);
            foreach (var other in state.Active.Where(c => c.id != actor)) Set(state, actor, other.id, 40);

            Assert.That(NpcSocialActions.Perform(state, actor, NpcActionKind.Confront), Is.False);
            Assert.That(state.randomState, Is.EqualTo(House(5u, out _).randomState),
                "A verb with nobody to aim at costs no turn and no draw.");
        }

        [Test]
        public void AConfrontationLandsOnWhoeverTheyLikeLeast()
        {
            var state = House(5u, out string actor);
            var others = state.Active.Where(c => c.id != actor).Select(c => c.id).ToList();
            foreach (string id in others) Set(state, actor, id, -10);
            Set(state, actor, others[2], -80);

            double theirSide = state.Score(others[2], actor);
            Assert.That(NpcSocialActions.Perform(state, actor, NpcActionKind.Confront), Is.True);
            Assert.That(state.Score(actor, others[2]),
                Is.EqualTo(-80 + NpcSocialActions.ConfrontationImpact).Within(0.001));
            Assert.That(state.Score(others[2], actor),
                Is.EqualTo(theirSide + NpcSocialActions.ConfrontationImpact).Within(0.001),
                "Both sides feel a confrontation, and equally — there is no reciprocal roll here.");
        }

        /// <summary>
        /// A rumour damages the listener's view of its subject and leaves no trace of who told them.
        /// That is the difference between a rumour and an accusation.
        /// </summary>
        [Test]
        public void ARumourDamagesTheSubjectAndNeverNamesItsSource()
        {
            var state = House(9u, out string actor);
            foreach (var edge in state.relationships) edge.score = 10;

            double before = state.relationships.Sum(r => r.score);
            Assert.That(NpcSocialActions.Perform(state, actor, NpcActionKind.SpreadInfo), Is.True);
            Assert.That(state.relationships.Sum(r => r.score), Is.LessThan(before));

            var rumours = state.relationships.SelectMany(r => r.events.Select(e => new { r.fromId, r.toId, e }))
                .Where(row => row.e.type == "rumor").ToList();
            Assert.That(rumours, Is.Not.Empty);
            Assert.That(rumours.Any(row => row.fromId == actor || row.toId == actor), Is.False,
                "Nobody in the room knows where it came from.");
        }

        /// <summary>
        /// Who a rumour is about is not drawn at all — it is whoever the storyteller reads as the
        /// biggest threat. Give one houseguest four Head of Household wins and the house should talk
        /// about almost nobody else; the exceptions are the runs where that houseguest happened to be
        /// the person being told, and they are the only exceptions.
        /// </summary>
        [Test]
        public void ARumourIsAboutWhoeverTheyReadAsTheBiggestThreat()
        {
            int aboutTheBeast = 0;
            for (uint seed = 1; seed <= 60; seed++)
            {
                var state = House(seed, out string actor);
                foreach (var edge in state.relationships) edge.score = 10;
                var beast = state.Active.First(c => c.id != actor && !c.isPlayer);
                beast.hohWins = 4;

                Assert.That(ThreatAssessment.RankedTargets(state, actor)[0], Is.EqualTo(beast.id));
                Assert.That(NpcSocialActions.Perform(state, actor, NpcActionKind.SpreadInfo), Is.True);

                bool touched = state.relationships
                    .Any(r => (r.fromId == beast.id || r.toId == beast.id)
                              && r.events.Any(e => e.type == "rumor"));
                if (touched) aboutTheBeast++;
            }

            Assert.That(aboutTheBeast, Is.GreaterThan(45),
                "People talk about the person they are worried about.");
        }

        /// <summary>
        /// Eavesdropping produces knowledge, not goodwill: nothing moves, and what was heard belongs
        /// to whoever heard it. The player is never in the room in either role.
        /// </summary>
        [Test]
        public void AnOverheardConversationBelongsOnlyToWhoeverHeardIt()
        {
            int heard = 0, caught = 0;
            for (uint seed = 1; seed <= 60; seed++)
            {
                var state = House(seed, out string actor);
                int eventsBefore = state.events.Count;
                Assert.That(NpcSocialActions.Perform(state, actor, NpcActionKind.Eavesdrop), Is.True);

                Assert.That(state.events.Count, Is.EqualTo(eventsBefore),
                    "The season's own log never carries a conversation the player was not in.");
                Assert.That(state.memories.Any(m => m.ownerId == state.playerId && m.week == state.week), Is.False);

                var learned = state.memories.Where(m => m.ownerId == actor).ToList();
                if (learned.Count > 0) { heard++; Assert.That(learned.All(m => m.isPrivate), Is.True); }
                else caught++;
            }

            Assert.That(heard, Is.GreaterThan(0));
            Assert.That(caught, Is.GreaterThan(0), "Roughly three times in ten, somebody notices.");
        }

        [Test]
        public void AnAllianceMeetingStrengthensItsMembers()
        {
            var state = House(3u, out string actor);
            string partner = state.Active.First(c => c.id != actor && !c.isPlayer).id;
            state.alliances.Add(new AllianceState
            {
                id = "pact", name = "A Pact",
                members = new List<string> { actor, partner }, active = true,
            });

            double before = state.Score(actor, partner);
            Assert.That(NpcSocialActions.Perform(state, actor, NpcActionKind.AllianceMeeting), Is.True);
            Assert.That(state.Score(actor, partner),
                Is.EqualTo(before + NpcSocialActions.AllianceMeetingImpact).Within(0.001));
        }

        [Test]
        public void NobodyConvenesAnAllianceThePlayerBelongsTo()
        {
            var state = House(3u, out string actor);
            state.alliances.Add(new AllianceState
            {
                id = "pact", name = "A Pact",
                members = new List<string> { actor, state.playerId }, active = true,
            });

            Assert.That(NpcSocialActions.Perform(state, actor, NpcActionKind.AllianceMeeting), Is.False,
                "A meeting the player never hears about must not move their standing.");
        }

        // ---------------------------------------------------------------- what the player may be told

        /// <summary>
        /// The house may now act on the player. Acceptance A9 says the player only ever learns what
        /// their own character would know, so anything the pass writes to the season's log is
        /// addressed to them and nothing else is written at all.
        /// </summary>
        [Test]
        public void EverythingThisPassLogsIsAddressedToThePlayer()
        {
            var state = Warm(Season(17u, 10), 30);
            var known = new HashSet<int>(state.events.Select(e => e.sequence));

            for (int week = 1; week <= 6; week++) { state.week = week; NpcSocialActions.Settle(state); }

            var written = state.events.Where(e => !known.Contains(e.sequence)).ToList();
            Assert.That(written.All(e => e.audienceIds.Contains(state.playerId)), Is.True,
                "A houseguest's private business is not the season's news.");
        }

        [Test]
        public void ThePlayerIsNeverEnrolledInAnAllianceTheyDidNotJoin()
        {
            var state = Warm(Season(21u, 10), 60);
            for (int week = 1; week <= 8; week++) { state.week = week; NpcSocialActions.Settle(state); }

            Assert.That(state.alliances.Any(a => a.members.Contains(state.playerId)), Is.False,
                "Acceptance is decided by mutual desire here, and the player's side of that is not asked of them.");
            Assert.That(state.promises.Any(p => p.toId == state.playerId), Is.False,
                "And a promise they cannot answer is a line in a file they never see.");
        }

        // ---------------------------------------------------------------- the pass

        /// <summary>
        /// The honest opposite of the alliance and promise passes, which spend nothing. This one
        /// spends the season's generator, and saying so is the point of the test.
        /// </summary>
        [Test]
        public void ThePassSpendsTheSeasonsGenerator()
        {
            var state = Warm(Season(4u, 8), 20);
            uint before = state.randomState;
            NpcSocialActions.Settle(state);
            Assert.That(state.randomState, Is.Not.EqualTo(before));
        }

        [Test]
        public void TheSameHousePlaysTheSameWeekEveryTime()
        {
            var first = Warm(Season(31u, 10), 25);
            var second = Warm(Season(31u, 10), 25);
            NpcSocialActions.Settle(first);
            NpcSocialActions.Settle(second);

            Assert.That(first.randomState, Is.EqualTo(second.randomState));
            Assert.That(first.nextSequence, Is.EqualTo(second.nextSequence));
            CollectionAssert.AreEqual(Digest(first), Digest(second));
        }

        [Test]
        public void AHouseBeforeItsRulesBeginDoesNothingAtAll()
        {
            var state = Warm(Season(6u, 8), 40);
            state.npcSocial.rulesStartWeek = state.week + 1;

            uint before = state.randomState;
            long sequence = state.nextSequence;
            NpcSocialActions.Settle(state);
            NpcSocialActions.Campaign(state);

            Assert.That(state.randomState, Is.EqualTo(before));
            Assert.That(state.nextSequence, Is.EqualTo(sequence),
                "Not one draw and not one record — this is what lets a recorded season stay the season it recorded.");
        }

        [Test]
        public void ThirtyWeeksOfThisLeaveAValidSeason()
        {
            var state = Warm(Season(12u, 12), 45);
            for (int week = 1; week <= 30; week++) { state.week = week; NpcSocialActions.Settle(state); }

            Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.True, error);
        }

        /// <summary>
        /// That the season itself runs the pass, rather than the pass merely working when called.
        ///
        /// <para>Worth its own test because the whole suite stayed green when this landed, and a
        /// green suite is exactly what a pass that is never invoked also looks like. The assertions
        /// are about the house having a private life the player is not party to: words given,
        /// standing moved between two people who are not them, and things overheard that stayed with
        /// whoever overheard them.</para>
        ///
        /// <para>Notably <b>not</b> asserted: that alliances form. See
        /// <see cref="AColdHouseNeverReachesTheSourcesAllianceFloor"/> for why that is a property of
        /// the season rather than of the pass.</para>
        /// </summary>
        [Test]
        public void ASeasonPlayedThroughTheEngineLetsTheHouseActOnItsOwn()
        {
            var state = Play(7u);

            Assert.That(state.promises.Any(p => p.id.StartsWith("promise-npc-", StringComparison.Ordinal)),
                Is.True, "Houseguests should have given each other their word.");
            Assert.That(state.memories.Any(m => m.ownerId != state.playerId && m.week > 1), Is.True,
                "…and learned things the player has no access to.");
            Assert.That(state.relationships.Any(r => r.fromId != state.playerId && r.toId != state.playerId
                                                     && r.events.Any(e => e.week > 1)), Is.True,
                "…and moved each other's standing without the player in the room.");
        }

        /// <summary>
        /// The house does not pair off in the shipped six-person scenario, and this records why.
        ///
        /// <para>The source's floor is a relationship of 25 before anybody will propose. Houseguests
        /// here start at zero with each other and the only thing that warms them in a headless season
        /// is this pass's own conversations, at four points each. Four weeks of that, spread across a
        /// house by weighted sampling, does not reach twenty-five — so no alliance forms, and the
        /// bloc system that reads alliances still only ever sees the player's.</para>
        ///
        /// <para><b>This is a parity gap, not a passing test dressed up as one.</b> The reference
        /// build warms its house through a conversation system that runs continuously; this port has
        /// one too, but it only turns while somebody is actually walking around, so a season driven
        /// by commands alone never gets it. Closing the gap means either warming the house faster or
        /// lowering the floor, and both are changes to the source's numbers — which is why this
        /// states the shortfall rather than quietly fixing it.</para>
        /// </summary>
        [Test]
        public void AColdHouseNeverReachesTheSourcesAllianceFloor()
        {
            var state = Play(7u);
            double warmest = state.relationships
                .Where(r => r.fromId != state.playerId && r.toId != state.playerId)
                .Select(r => r.score)
                .DefaultIfEmpty(0)
                .Max();

            Assert.That(warmest, Is.LessThan(NpcAlliances.MinimumRelationship),
                "If this ever fails the house has started warming up, and the alliance assertion "
                + "belongs back in the test above.");
            Assert.That(state.alliances.Any(a => a.id.StartsWith("alliance-npc-", StringComparison.Ordinal)),
                Is.False);
        }

        // ---------------------------------------------------------------- campaigning

        [Test]
        public void ANomineeCampaignsToTheVetoHolderFirst()
        {
            var state = Nominated(out string nominee, out string vetoHolder);
            double before = state.Score(nominee, vetoHolder);

            NpcSocialActions.Campaign(state);
            Assert.That(state.Score(nominee, vetoHolder),
                Is.EqualTo(before + NpcSocialActions.CampaignImpact).Within(0.001),
                "Desperation reads as urgency: the largest situational boost in the source's table.");
        }

        [Test]
        public void NobodyCampaignsAfterTheVotesAreCounted()
        {
            var state = Nominated(out _, out _);
            state.evictionResolved = true;

            uint before = state.randomState;
            long sequence = state.nextSequence;
            NpcSocialActions.Campaign(state);
            Assert.That(state.randomState, Is.EqualTo(before));
            Assert.That(state.nextSequence, Is.EqualTo(sequence));
        }

        // ---------------------------------------------------------------- fixtures

        /// <summary>A whole season, played end to end through the engine the way a player would.</summary>
        private static EpisodeState Play(uint seed)
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(seed));
            for (int guard = 0; guard < 260 && engine.Snapshot.phase != EpisodePhase.Finished; guard++)
            {
                var result = engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot));
                Assert.That(result.accepted, Is.True, result.reason);
            }
            return engine.Snapshot;
        }

        private static EpisodeState Season(uint seed, int size) =>
            SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size }, seed);

        /// <summary>A house of eight and one houseguest in it who is not the player.</summary>
        private static EpisodeState House(uint seed, out string actor)
        {
            var state = Season(seed, 8);
            actor = state.Active.First(c => !c.isPlayer).id;
            return state;
        }

        private static EpisodeState Nominated(out string nominee, out string vetoHolder)
        {
            var state = Season(7u, 8);
            var cast = state.Active.Where(c => !c.isPlayer).ToList();
            state.hohId = cast[0].id;
            state.nominees = new List<string> { cast[1].id, cast[2].id };
            state.vetoHolderId = cast[3].id;
            nominee = cast[1].id;
            vetoHolder = cast[3].id;
            return state;
        }

        private static EpisodeState Warm(EpisodeState state, double score)
        {
            foreach (var edge in state.relationships) edge.score = score;
            return state;
        }

        private static void Set(EpisodeState state, string from, string to, double score)
        {
            foreach (var edge in state.relationships.Where(r => r.fromId == from && r.toId == to))
                edge.score = score;
        }

        private static Dictionary<string, double> Scores(EpisodeState state, string from) =>
            state.relationships.Where(r => r.fromId == from).ToDictionary(r => r.toId, r => r.score);

        private static List<string> Digest(EpisodeState state) =>
            state.relationships.Select(r => r.fromId + ">" + r.toId + "=" + r.score)
                .Concat(state.alliances.Select(a => "alliance:" + a.id + ":" + string.Join("+", a.members)))
                .Concat(state.promises.Select(p => "promise:" + p.id + ":" + p.fromId + ">" + p.toId))
                .Concat(state.memories.Select(m => "memory:" + m.ownerId + ":" + m.text))
                .OrderBy(entry => entry, StringComparer.Ordinal)
                .ToList();
    }
}
