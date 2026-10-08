using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Leaks and double-dealing (WAVE-D-NPC-PACTS-PLAN §2, D4): the leak rules' gate, odds and coin;
    /// one pair, one pact; the whisper that names everyone; the listen-in's grant and its sentence; the
    /// weekly leak; and an ally who finds out about the player's other pact. Every rule is pinned off as
    /// well as on, since a season without the rules plays as it did. Unity-free, so the dotnet subset
    /// runs it (Tools/SimulationTests).
    /// </summary>
    public sealed class AllianceLeakTests
    {
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Static;

        /// <summary>A house of eight in week six: the player and seven others, nobody allied, nothing on.</summary>
        private static EpisodeState House(uint seed = 4101)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
            s.week = 6;
            return s;
        }

        /// <summary>The same house with the story's knowledge, the commitment rules and the leak rules on from this week.</summary>
        private static EpisodeState Rules(uint seed = 4101)
        {
            var s = House(seed);
            EpisodeEngine.EnableStory(s, s.week);
            EpisodeEngine.EnableCommitments(s, s.week);
            EpisodeEngine.EnableAllianceLeaks(s, s.week);
            Assert.That(AllianceLeaks.On(s), Is.True, "Precondition: the leak rules are on.");
            return s;
        }

        private static List<ContestantState> Others(EpisodeState s) => s.contestants.Where(c => !c.isPlayer).ToList();

        private static AllianceState Pact(EpisodeState s, string id, bool active, params string[] members)
        {
            var pact = new AllianceState { id = id, name = "The " + id + " Pact", active = active, members = members.ToList() };
            s.alliances.Add(pact);
            return pact;
        }

        /// <summary>The pact's private fact, as the rules make it at birth, dated <paramref name="week"/>.</summary>
        private static HouseFactState PrivateFact(EpisodeState s, AllianceState pact, int week = 0)
        {
            Knowledge.AllianceFormed(s, pact);
            var fact = Knowledge.Of(s, FactKinds.Alliance, pact.id);
            if (week > 0) fact.week = week;
            return fact;
        }

        private static void SetScore(EpisodeState s, string from, string to, double score)
        {
            var row = s.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (row == null) s.relationships.Add(row = new RelationshipState { fromId = from, toId = to });
            row.score = score;
        }

        private static string Json(EpisodeState s) => JsonConvert.SerializeObject(s);

        private static MethodInfo EngineMethod(string name, params Type[] parameters)
        {
            var method = typeof(EpisodeEngine).GetMethod(name, Private, null, parameters, null);
            Assert.That(method, Is.Not.Null, "The engine's " + name + "(" + string.Join(", ", parameters.Select(t => t.Name)) + ").");
            return method;
        }

        // ------------------------------------------------------------ D4-0: the gate, the odds, the coin, the resolver

        [Test]
        public void TheGateNeedsItsStartWeekTheStorysKnowledgeAndTheCommitmentRules()
        {
            var s = House();
            Assert.That(AllianceLeaks.On(s), Is.False, "A season built directly plays none of it.");
            EpisodeEngine.EnableStory(s, s.week);
            EpisodeEngine.EnableAllianceLeaks(s, s.week);
            Assert.That(s.allianceLeakRulesStartWeek, Is.EqualTo(s.week));
            Assert.That(AllianceLeaks.On(s), Is.False, "With the commitment rules off but the start week set, it is off.");
            EpisodeEngine.EnableCommitments(s, s.week);
            Assert.That(AllianceLeaks.On(s), Is.True);
            s.story.rulesVersion = StoryRules.Bonds - 1;
            Assert.That(AllianceLeaks.On(s), Is.False, "Without the story's facts, it is off.");
            s.story.rulesVersion = StoryRules.Current;
            EpisodeEngine.EnableAllianceLeaks(s, s.week + 1);
            Assert.That(AllianceLeaks.On(s), Is.False, "Before its start week, it is off.");
            s.week++;
            Assert.That(AllianceLeaks.On(s), Is.True, "and on from it.");

            // Enabling is clamped as the commitment rules' is: never before week one, never past next week.
            var t = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 4102);
            Assert.That(t.week, Is.EqualTo(1));
            EpisodeEngine.EnableAllianceLeaks(t, 40);
            Assert.That(t.allianceLeakRulesStartWeek, Is.EqualTo(t.week + 1));
            EpisodeEngine.EnableAllianceLeaks(t, -3);
            Assert.That(t.allianceLeakRulesStartWeek, Is.EqualTo(1));
            Assert.That(EpisodeValidation.TryValidate(t, out string error), Is.True, error);
        }

        [TestCase(2, 1, 0.05)] [TestCase(2, 2, 0.10)] [TestCase(2, 3, 0.15)]
        [TestCase(3, 1, 0.08)] [TestCase(3, 2, 0.13)] [TestCase(3, 3, 0.18)]
        [TestCase(4, 1, 0.11)] [TestCase(4, 2, 0.16)] [TestCase(4, 3, 0.21)]
        public void TheOddsAreThePlansTable(int members, int held, double odds)
        {
            Assert.That(AllianceLeaks.Odds(members, held, true), Is.EqualTo(odds).Within(1e-9), "The player's pact.");
            Assert.That(AllianceLeaks.Odds(members, held, false), Is.EqualTo(AllianceLeaks.Odds(members, 1, true)).Within(1e-9),
                "A pact between others takes the base odds whatever the player holds.");
        }

        [Test]
        public void TheOddsNeverPassAHalfAndNeverGoBelowTheBase()
        {
            Assert.That(AllianceLeaks.Odds(12, 3, true), Is.EqualTo(0.45).Within(1e-9));
            Assert.That(AllianceLeaks.Odds(16, 3, true), Is.EqualTo(AllianceLeaks.OddsCap), "The cap is a guard.");
            Assert.That(AllianceLeaks.Odds(40, 0, false), Is.EqualTo(AllianceLeaks.OddsCap));
            Assert.That(AllianceLeaks.Odds(0, 0, true), Is.EqualTo(AllianceLeaks.BaseOdds).Within(1e-9), "Fewer than two members adds nothing,");
            Assert.That(AllianceLeaks.Odds(2, 0, true), Is.EqualTo(AllianceLeaks.BaseOdds).Within(1e-9), "and nor does holding no other pact.");
        }

        [Test]
        public void ThePlayersJugglingCountsOnlyOnTheirOwnPactsAndOnlyWhileTheyAreInTheHouse()
        {
            var s = Rules();
            var n = Others(s);
            var ours = Pact(s, "ours", true, s.playerId, n[0].id, n[1].id);
            Pact(s, "second", true, s.playerId, n[2].id);
            Pact(s, "third", true, s.playerId, n[3].id);
            var theirs = Pact(s, "theirs", true, n[4].id, n[5].id, n[6].id);
            Assert.That(EpisodeEngine.PlayerPactsHeld(s), Is.EqualTo(3));
            Assert.That(AllianceLeaks.Odds(s, ours), Is.EqualTo(0.18).Within(1e-9), "Three of the player's, three in it.");
            Assert.That(AllianceLeaks.Odds(s, theirs), Is.EqualTo(0.08).Within(1e-9), "Three between others: the base odds.");

            // An evicted member does not count, and an evicted player juggles nothing.
            n[1].status = ContestantStatus.Evicted;
            Assert.That(AllianceLeaks.Odds(s, ours), Is.EqualTo(0.15).Within(1e-9));
            s.Find(s.playerId).status = ContestantStatus.Evicted;
            Assert.That(AllianceLeaks.Odds(s, ours), Is.EqualTo(0.05).Within(1e-9));
        }

        [Test]
        public void OnlyAStandingPactsOwnPrivateFactFromAnEarlierWeekRolls()
        {
            var s = Rules();
            var n = Others(s);
            var pact = Pact(s, "pact", true, n[0].id, n[1].id);
            Assert.That(AllianceLeaks.Rolls(s, pact), Is.Null, "No fact, no coin: a legacy pact is known to everyone already.");
            var fact = PrivateFact(s, pact);
            Assert.That(fact.week, Is.EqualTo(s.week));
            Assert.That(AllianceLeaks.Rolls(s, pact), Is.Null, "A pact formed this week waits a week.");
            fact.week = s.week - 1;
            Assert.That(AllianceLeaks.Rolls(s, pact), Is.SameAs(fact));
            fact.visibility = FactVisibility.Whispered;
            Assert.That(AllianceLeaks.Rolls(s, pact), Is.Null, "A whispered fact never rolls again.");
            fact.visibility = FactVisibility.Private;
            pact.active = false;
            Assert.That(AllianceLeaks.Rolls(s, pact), Is.Null, "An ended pact stops rolling.");
            pact.active = true;
            n[1].status = ContestantStatus.Evicted;
            Assert.That(AllianceLeaks.Rolls(s, pact), Is.Null, "Two of its members in the house.");
            n[1].status = ContestantStatus.Active;

            // Somebody in the house outside it, and four in the house.
            var everyone = Pact(s, "everyone", true, s.Active.Select(c => c.id).ToArray());
            PrivateFact(s, everyone, s.week - 1);
            Assert.That(AllianceLeaks.Rolls(s, everyone), Is.Null, "Nobody left to tell.");
            foreach (var gone in n.Skip(2)) gone.status = ContestantStatus.Evicted;
            Assert.That(s.Active.Count(), Is.EqualTo(3));
            Assert.That(AllianceLeaks.Rolls(s, pact), Is.Null, "Three in the house roll nothing.");
            n[2].status = ContestantStatus.Active;
            Assert.That(AllianceLeaks.Rolls(s, pact), Is.SameAs(fact), "Four do.");
        }

        [Test]
        public void ALeakIsItsKeyedCoinUnderItsOddsAndDrawsNothingFromTheSeason()
        {
            int leaked = 0, rolled = 0;
            for (uint seed = 4200; seed < 4260; seed++)
            {
                var s = Rules(seed);
                var n = Others(s);
                var pact = Pact(s, "coin", true, n[0].id, n[1].id, n[2].id, n[3].id);
                PrivateFact(s, pact, s.week - 1);
                string before = Json(s);
                bool leaks = AllianceLeaks.Leaks(s, pact);
                Assert.That(leaks, Is.EqualTo(StoryRandom.Unit(s, "w" + s.week + ":leak:" + pact.id) < 0.11), "Seed " + seed + ": the coin is the plan's key under the plan's odds.");
                Assert.That(AllianceLeaks.Key(s, pact), Is.EqualTo("w6:leak:coin"));
                Assert.That(Json(s), Is.EqualTo(before), "Reading the coin changes nothing: no draw, no id.");
                rolled++;
                if (leaks) leaked++;
                s.allianceLeakRulesStartWeek = 0;
                Assert.That(AllianceLeaks.Leaks(s, pact), Is.False, "Off, nothing leaks.");
            }
            Assert.That(leaked, Is.GreaterThan(0).And.LessThan(rolled), "Sixty houses: some leak, most do not.");
        }

        [Test]
        public void OnePairOnePactPrefersAStandingPactThenOneTheListenerDoesNotKnowThenTheHousesOrder()
        {
            var s = Rules();
            var n = Others(s);
            Assert.That(Knowledge.PactOfPair(s, n[0].id, n[1].id, s.playerId), Is.Null, "No pact holds them.");
            var ended = Pact(s, "ended", false, n[0].id, n[1].id);
            PrivateFact(s, ended);
            var known = Pact(s, "known", true, n[1].id, n[0].id, n[2].id);
            Knowledge.AddKnower(s, PrivateFact(s, known), s.playerId);
            var hidden = Pact(s, "hidden", true, n[0].id, n[1].id, n[3].id);
            PrivateFact(s, hidden);
            var later = Pact(s, "later", true, n[0].id, n[1].id);
            PrivateFact(s, later);
            Pact(s, "other", true, n[0].id, n[2].id);

            Assert.That(Knowledge.PactOfPair(s, n[0].id, n[1].id, null), Is.SameAs(known), "No listener: the first standing pact.");
            Assert.That(Knowledge.PactOfPair(s, n[1].id, n[0].id, s.playerId), Is.SameAs(hidden),
                "A listener: the first standing pact they do not know of, whichever way round the two are named.");
            Assert.That(Knowledge.PactOfPair(s, n[0].id, n[1].id, n[3].id), Is.SameAs(known), "A member knows their own pact.");
            hidden.active = false; later.active = false; known.active = false;
            Assert.That(Knowledge.PactOfPair(s, n[0].id, n[1].id, s.playerId), Is.SameAs(ended), "All ended: the oldest unknown.");
            Assert.That(Knowledge.PactOfPair(s, n[0].id, n[1].id, null), Is.SameAs(ended), "and with no listener, the oldest.");
        }

        // ------------------------------------------------------------ D4-1: one pair, one pact; the whisper names everyone

        /// <summary>One story effect, applied the way a beat applies it (EpisodeEngine.ApplyStoryEffect).</summary>
        private static void ApplyStoryEffect(EpisodeState s, StoryEffectState effect) =>
            EngineMethod("ApplyStoryEffect", typeof(EpisodeState), typeof(StoryEffectState), typeof(StorylineState),
                    typeof(string), typeof(string), typeof(int), typeof(bool))
                .Invoke(null, new object[] { s, effect, null, null, "leaks-test", 0, false });

        private static string Whisper(EpisodeState s, HouseFactState fact) =>
            (string)EngineMethod("Whisper", typeof(EpisodeState), typeof(HouseFactState)).Invoke(null, new object[] { s, fact });

        [Test]
        public void AStoryMadePublicGoesPublicForOnePactOfThePairAndTheOthersStayDark()
        {
            foreach (bool rules in new[] { false, true })
            {
                var s = rules ? Rules() : House();
                if (!rules) EpisodeEngine.EnableStory(s, s.week);
                var n = Others(s);
                var first = Pact(s, "first", true, n[0].id, n[1].id, n[2].id);
                var firstFact = PrivateFact(s, first);
                var second = Pact(s, "second", true, n[1].id, n[0].id);
                var secondFact = PrivateFact(s, second);
                // SpreadAlliance: the pair's pact out in the open, nobody named.
                ApplyStoryEffect(s, new StoryEffectState { kind = StoryEffects.Spread, fromId = n[0].id, toId = n[1].id,
                    type = FactKinds.Alliance, text = FactVisibility.Public });
                Assert.That(firstFact.visibility, Is.EqualTo(FactVisibility.Public), "The resolver's pact goes public.");
                Assert.That(secondFact.visibility, Is.EqualTo(rules ? FactVisibility.Private : FactVisibility.Public),
                    rules ? "Under the rules the other stays as it was." : "Without them every pact of the pair goes.");
                Assert.That(Knowledge.Knows(secondFact, n[5].id), Is.EqualTo(!rules));
            }
        }

        [Test]
        public void UnderTheRulesTheWhisperNamesEveryoneAndThePageDatesBothForms()
        {
            var off = House();
            EpisodeEngine.EnableStory(off, off.week);
            var o = Others(off);
            var trioOff = Pact(off, "trio", true, o[0].id, o[1].id, o[2].id);
            var offFact = PrivateFact(off, trioOff);
            Assert.That(Whisper(off, offFact), Is.EqualTo("Word in the house: " + o[0].name + " and " + o[1].name + " are working together."),
                "Without the rules: the two names it always said.");
            Assert.That(Whisper(off, offFact), Is.EqualTo(AllianceRead.WhisperLine(off, offFact)));

            var s = Rules();
            var n = Others(s);
            var trio = Pact(s, "trio", true, n[0].id, n[1].id, n[2].id);
            var fact = PrivateFact(s, trio);
            string everyone = Whisper(s, fact);
            Assert.That(everyone, Is.EqualTo("Word in the house: " + n[0].name + ", " + n[1].name + " and " + n[2].name + " are working together."),
                "Under the rules it names everyone, in the pact's own order.");
            Assert.That(everyone, Is.EqualTo(AllianceLeaks.WhisperLine(s, trio)));
            var pair = Pact(s, "pair", true, n[3].id, n[4].id);
            var pairFact = PrivateFact(s, pair);
            Assert.That(Whisper(s, pairFact), Is.EqualTo(AllianceRead.WhisperLine(s, pairFact)), "For a pair it is the line it always was.");

            // The page dates a card it knows of by either form: a line said before the rules, and one since.
            Knowledge.AddKnower(s, fact, s.playerId);
            string twoNames = AllianceRead.WhisperLine(s, fact);
            s.events.Add(new EpisodeEvent { sequence = s.nextSequence++, week = 3, kind = StoryLog.Whisper, text = twoNames, audienceIds = new List<string> { s.playerId } });
            s.events.Add(new EpisodeEvent { sequence = s.nextSequence++, week = 5, kind = StoryLog.Whisper, text = everyone, audienceIds = new List<string> { s.playerId } });
            var card = AllianceRead.Suspected(s).Single();
            Assert.That(card.evidence.Select(e => e.week + ": " + e.text), Is.EqualTo(new[] { "3: " + twoNames, "5: " + everyone }));
            Assert.That(AllianceRead.IsWhisperLine(s, fact, trio, twoNames) && AllianceRead.IsWhisperLine(s, fact, trio, everyone), Is.True);
            Assert.That(AllianceRead.IsWhisperLine(s, pairFact, pair, everyone), Is.False);
        }

        // ------------------------------------------------------------ the secret alliance under the rules

        private static EpisodeState Apply(EpisodeState s, EpisodeCommand c)
        {
            var result = new EpisodeEngine(s).Apply(c);
            Assert.That(result.accepted, Is.True, c.kind + ": " + result.reason);
            return result.state;
        }

        private static StorylineState Cycle(EpisodeState s, string arcId) => s.storylines.Last(x => x.templateId == arcId);

        private static EpisodeState Answer(EpisodeState s, string arcId, string optionId)
        {
            var command = EpisodeEngineTests.Command(s, EpisodeCommandKind.ProgressStoryline);
            command.targetId = s.houseEvents.Single(e => e.cycleId == Cycle(s, arcId).id && !e.resolved).id;
            command.secondTargetId = optionId;
            return Apply(s, command);
        }

        /// <summary>
        /// The Secret Alliance (plan 30, Intel) when the secret pair also sits in a pact of three the whole
        /// house knows, made first: under the rules the play is about the secret pact - one the player does
        /// not know of before one they do - so taking it on wins nothing, and the leak it pays with grants
        /// the secret pair and names it in its receipt. The pact of three is never named to the player.
        /// </summary>
        [Test]
        public void UnderTheRulesTheSecretAllianceIsAboutTheSecretPactAndItsReceiptNamesIt()
        {
            var engine = new EpisodeEngine(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 31));
            for (int i = 0; i < 800 && !(engine.Snapshot.week >= 2 && engine.Snapshot.phase == EpisodePhase.Nomination
                     && engine.Snapshot.nominees.Count == 0 && engine.Snapshot.hohId != engine.Snapshot.playerId); i++)
                Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            var s = engine.Snapshot;
            Assert.That(s.phase, Is.EqualTo(EpisodePhase.Nomination));
            Assert.That(s.Find(s.playerId).status, Is.EqualTo(ContestantStatus.Active));
            EpisodeEngine.EnableStory(s, s.week);
            EpisodeEngine.EnableCommitments(s, s.week);
            EpisodeEngine.EnableAllianceLeaks(s, s.week);
            Assert.That(AllianceLeaks.On(s), Is.True);
            foreach (var other in s.alliances) other.active = false;
            var npcs = s.Active.Where(c => !c.isPlayer).OrderBy(c => c.id, StringComparer.Ordinal).ToList();
            // The pact of three the house knows of, first; then the secret pair inside it.
            var known = NpcAlliances.FormFromStory(s, new List<string> { npcs[0].id, npcs[1].id, npcs[2].id });
            Knowledge.AllianceFormed(s, known);
            Knowledge.MakeKnown(s, Knowledge.Of(s, FactKinds.Alliance, known.id), FactVisibility.Public);
            var secret = new AllianceState { id = "alliance-story-" + s.nextSequence++, name = "The Secret Pair", active = true,
                members = new List<string> { npcs[0].id, npcs[1].id } };
            s.alliances.Add(secret);
            Knowledge.AllianceFormed(s, secret);
            // One more the player knows of, between two others, to trade.
            var traded = NpcAlliances.FormFromStory(s, new List<string> { npcs[3].id, npcs[4].id });
            Knowledge.AllianceFormed(s, traded);
            Knowledge.MakeKnown(s, Knowledge.Of(s, FactKinds.Alliance, traded.id), FactVisibility.Public);
            Assert.That(Knowledge.AllianceVisibleTo(s, secret, s.playerId), Is.False, "The fixture's pair is secret.");
            Assert.That(Knowledge.PactOfPair(s, npcs[0].id, npcs[1].id, s.playerId), Is.SameAs(secret));

            Assert.That(EpisodeEngine.StartStory(s, "the-secret-alliance", StoryAnchors.HohCrowned), Is.True, "The Secret Alliance casts.");
            s = Answer(s, "the-secret-alliance", PlayOptions.TakeItOn);
            Assert.That(Cycle(s, "the-secret-alliance").endingId, Is.Null.Or.Empty,
                "Taken on and not yet won: the pact of three the player knows of is not the one it is about.");
            s = Answer(s, "the-secret-alliance", "trade-what-you-know");
            Assert.That(Cycle(s, "the-secret-alliance").endingId, Is.EqualTo(PlayEndings.Won), "Found out: won on the spot.");
            var secretAfter = s.alliances.Single(a => a.id == secret.id);
            Assert.That(Knowledge.AllianceVisibleTo(s, secretAfter, s.playerId), Is.True);
            var receipts = s.events.Where(e => e.kind == StoryLog.Receipt).Select(e => e.text).ToList();
            Assert.That(receipts, Has.Member(AllianceRead.LearnedLine(s, secretAfter)), "The receipt names the secret pair,");
            Assert.That(receipts, Has.No.Member(AllianceRead.LearnedLine(s, s.alliances.Single(a => a.id == known.id))), "never the pact of three.");
        }
    }
}
