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

        // ------------------------------------------------------------ D4-2: the listen-in

        /// <summary>A season stream whose next draw is a listen-in caught (0.7 or more) or not.</summary>
        private static uint StreamWhereTheListenIn(bool caught)
        {
            for (uint x = 1; ; x++)
                if ((new SeededRandom(x).NextDouble() >= EpisodeEngine.EavesdropSuccessChance) == caught) return x;
        }

        /// <summary>Listens in on two named houseguests the way the engine does (EpisodeEngine.Eavesdrop), caught or not.</summary>
        private static void ListenIn(EpisodeState s, string first, string second, bool caught = false)
        {
            s.randomState = StreamWhereTheListenIn(caught);
            var command = new EpisodeCommand { id = "listen-" + s.nextSequence, actorId = s.playerId, kind = EpisodeCommandKind.Eavesdrop,
                targetId = first, secondTargetId = second };
            EngineMethod("Eavesdrop", typeof(EpisodeState), typeof(EpisodeCommand)).Invoke(null, new object[] { s, command });
        }

        private static EpisodeEvent LastLine(EpisodeState s, string kind) => s.events.Last(e => e.kind == kind);

        /// <summary>
        /// The same listen-in on a pact of the two overheard, with the leak rules and without: the same
        /// draws, the same ids, the same memory and the same audience. Under the rules the player is a
        /// knower of the pact - still private, so the house's gossip has not started - and the line says so
        /// in one sentence naming everyone the alliances page's card shows, which the page then dates.
        /// </summary>
        [Test]
        public void AListenInOnAPactOfExactlyTheTwoMakesThePlayerASuspectedKnowerInTheLineAndNowhereElse()
        {
            EpisodeState Fixture(bool rules)
            {
                var s = House();
                EpisodeEngine.EnableStory(s, s.week);
                EpisodeEngine.EnableCommitments(s, s.week);
                if (rules) EpisodeEngine.EnableAllianceLeaks(s, s.week);
                var n = Others(s);
                PrivateFact(s, Pact(s, "pair", true, n[1].id, n[0].id));
                return s;
            }
            var off = Fixture(false);
            var on = Fixture(true);
            var o = Others(off);
            ListenIn(off, o[0].id, o[1].id);
            ListenIn(on, o[0].id, o[1].id);
            Assert.That(on.randomState, Is.EqualTo(off.randomState), "The same draws,");
            Assert.That(on.nextSequence, Is.EqualTo(off.nextSequence), "the same ids,");
            Assert.That(on.memories.Last().text, Is.EqualTo(off.memories.Last().text), "the same memory, which names no pact,");
            var offLine = LastLine(off, "eavesdrop");
            var onLine = LastLine(on, "eavesdrop");
            Assert.That(onLine.audienceIds, Is.EqualTo(new[] { on.playerId }), "and the player alone hears it.");
            Assert.That(offLine.text, Does.StartWith("You overheard " + o[0].name + " and " + o[1].name + ". They "));
            Assert.That(Knowledge.Knows(Knowledge.Of(off, FactKinds.Alliance, "pair"), off.playerId), Is.False, "Without the rules nothing is learned.");

            var pact = on.alliances.Single(a => a.id == "pair");
            var fact = Knowledge.Of(on, FactKinds.Alliance, pact.id);
            string sentence = " From the way they talked, " + on.contestants.Where(c => pact.members.Contains(c.id)).Select(c => c.name)
                .Aggregate((a, b) => a + " and " + b) + " are working together.";
            Assert.That(AllianceLeaks.ListenInSentence(on, pact), Is.EqualTo(sentence), "Named in the card's order.");
            Assert.That(onLine.text, Is.EqualTo(offLine.text + sentence), "The line, then the sentence.");
            Assert.That(Knowledge.Knows(fact, on.playerId), Is.True);
            Assert.That(fact.visibility, Is.EqualTo(FactVisibility.Private), "Overhearing starts no gossip.");
            Assert.That(FinalistRead.AllianceCertainty(on, pact), Is.EqualTo(FinalistRead.Suspected));
            var card = AllianceRead.Suspected(on).Single();
            Assert.That(card.memberIds, Is.EqualTo(on.contestants.Where(c => pact.members.Contains(c.id)).Select(c => c.id)));
            Assert.That(card.evidence.Select(e => e.week + ": " + e.text), Is.EqualTo(new[] { on.week + ": " + onLine.text }),
                "The page dates it by the line that told the player.");
        }

        [Test]
        public void AListenInGrantsNothingForTwoOfABiggerPactThePlayersOwnPactOrWhenCaught()
        {
            // Two of a pact of three, all in the house: the line is the line, and nothing is learned.
            var s = Rules();
            var n = Others(s);
            var trio = Pact(s, "trio", true, n[0].id, n[1].id, n[2].id);
            var trioFact = PrivateFact(s, trio);
            ListenIn(s, n[0].id, n[1].id);
            Assert.That(LastLine(s, "eavesdrop").text, Does.Not.Contain("From the way they talked"));
            Assert.That(Knowledge.Knows(trioFact, s.playerId), Is.False, "Two members of a bigger pact give nothing away.");

            // Its third member gone: the people of it in the house are exactly the two, and the sentence
            // names all three, as the card does.
            n[2].status = ContestantStatus.Evicted;
            ListenIn(s, n[1].id, n[0].id);
            Assert.That(Knowledge.Knows(trioFact, s.playerId), Is.True);
            Assert.That(LastLine(s, "eavesdrop").text, Does.EndWith(AllianceLeaks.ListenInSentence(s, trio)));
            Assert.That(AllianceLeaks.ListenInSentence(s, trio), Does.Contain(n[2].name));
            // Heard again: known already, so nothing more is said.
            ListenIn(s, n[0].id, n[1].id);
            Assert.That(LastLine(s, "eavesdrop").text, Does.Not.Contain("From the way they talked"));

            // The player's own pact.
            var t = Rules();
            var m = Others(t);
            var ours = Pact(t, "ours", true, t.playerId, m[0].id, m[1].id);
            PrivateFact(t, ours);
            ListenIn(t, m[0].id, m[1].id);
            Assert.That(LastLine(t, "eavesdrop").text, Does.Not.Contain("From the way they talked"), "Nothing to learn of one's own pact.");

            // Caught: nothing is overheard at all.
            var u = Rules();
            var k = Others(u);
            var pair = Pact(u, "pair", true, k[0].id, k[1].id);
            var pairFact = PrivateFact(u, pair);
            ListenIn(u, k[0].id, k[1].id, caught: true);
            Assert.That(Knowledge.Knows(pairFact, u.playerId), Is.False, "A caught listen-in grants nothing.");
            Assert.That(u.events.Any(e => e.kind == "eavesdrop" && e.text.Contains("From the way they talked")), Is.False);
        }

        /// <summary>
        /// The shared builder (X5): with every clause empty it is the line the season always said; with
        /// them, in the plan's order - what was overheard, the vote, D2's act, D4's sentence.
        /// </summary>
        [Test]
        public void TheEavesdropLineIsTodaysLineAndThenItsClausesInOrder()
        {
            Assert.That(EpisodeEngine.EavesdropLine("Riley Chen", "Jo Park", "sounded close", "", null, null),
                Is.EqualTo("You overheard Riley Chen and Jo Park. They sounded close."));
            Assert.That(EpisodeEngine.EavesdropLine("Riley Chen", "Jo Park", "sounded close", " [vote]", " [act]", " [pact]"),
                Is.EqualTo("You overheard Riley Chen and Jo Park. They sounded close. [vote] [act] [pact]"));
        }

        // ------------------------------------------------------------ D4-3: the weekly leak

        private static void LeakPass(EpisodeState s) => EngineMethod("LeakPass", typeof(EpisodeState)).Invoke(null, new object[] { s });

        private static void StorySystemsAt(EpisodeState s, string anchor) =>
            EngineMethod("StorySystemsAt", typeof(EpisodeState), typeof(string)).Invoke(null, new object[] { s, anchor });

        /// <summary>Whether the plan's coin lands for a pact this week, worked out here from its key and its odds.</summary>
        private static bool CoinLands(EpisodeState s, AllianceState pact) =>
            AllianceLeaks.Rolls(s, pact) != null && StoryRandom.Unit(s, "w" + s.week + ":leak:" + pact.id) < AllianceLeaks.Odds(s, pact);

        /// <summary>
        /// The leak pass, over forty houses: each pact that rolls goes out as a whisper exactly when its
        /// keyed coin lands under its odds, and nothing else moves - no knower, no line, no id, no draw.
        /// A pact formed this week never rolls, and the order the pacts are taken in changes nothing.
        /// </summary>
        [Test]
        public void TheLeakPassWhispersExactlyThePactsWhoseCoinLandsAndMovesNothingElse()
        {
            int leaked = 0, kept = 0;
            for (uint seed = 4400; seed < 4440; seed++)
            {
                var s = Rules(seed);
                var n = Others(s);
                PrivateFact(s, Pact(s, "ours", true, s.playerId, n[0].id, n[1].id), s.week - 1);
                Pact(s, "second", true, s.playerId, n[2].id);
                PrivateFact(s, Pact(s, "theirs", true, n[2].id, n[3].id, n[4].id, n[5].id), s.week - 1);
                PrivateFact(s, Pact(s, "fresh", true, n[4].id, n[5].id));
                var expected = s.alliances.ToDictionary(a => a.id, a => CoinLands(s, a));
                Assert.That(expected["fresh"], Is.False, "A pact formed this week waits a week.");
                var reversed = s.Clone();
                reversed.alliances.Reverse();
                uint random = s.randomState; int ids = s.nextSequence; int lines = s.events.Count;
                string knowers = string.Join(";", s.story.facts.Select(f => f.id + ":" + string.Join(",", f.knowers)));
                LeakPass(s);
                LeakPass(reversed);
                foreach (var pact in s.alliances)
                {
                    var fact = Knowledge.Of(s, FactKinds.Alliance, pact.id);
                    if (fact == null) continue;
                    Assert.That(fact.visibility, Is.EqualTo(expected[pact.id] ? FactVisibility.Whispered : FactVisibility.Private), "Seed " + seed + ", " + pact.id);
                    Assert.That(Knowledge.Of(reversed, FactKinds.Alliance, pact.id).visibility, Is.EqualTo(fact.visibility), "Order changes nothing.");
                    if (expected[pact.id]) leaked++; else kept++;
                }
                Assert.That(s.randomState, Is.EqualTo(random), "No draw from the season,");
                Assert.That(s.nextSequence, Is.EqualTo(ids), "no id,");
                Assert.That(s.events.Count, Is.EqualTo(lines), "no line,");
                Assert.That(string.Join(";", s.story.facts.Select(f => f.id + ":" + string.Join(",", f.knowers))), Is.EqualTo(knowers), "and no knower.");
            }
            Assert.That(leaked, Is.GreaterThan(0), "Some pacts got out,");
            Assert.That(kept, Is.GreaterThan(leaked), "most did not.");
        }

        /// <summary>
        /// Where the leak runs: at the eviction night anchor only, after the showmances and before the
        /// gossip, which can carry what got out the same night - and not at all without the rules.
        /// </summary>
        [Test]
        public void TheLeakRunsAtEvictionNightOnlyAndItsGossipCanCarryItTheSameNight()
        {
            int carried = 0, leaks = 0;
            for (uint seed = 4500; seed < 4580; seed++)
            {
                var s = Rules(seed);
                var n = Others(s);
                var pact = Pact(s, "leaky", true, n[0].id, n[1].id, n[2].id, n[3].id);
                PrivateFact(s, pact, s.week - 1);
                if (!CoinLands(s, pact)) continue;
                leaks++;
                var otherAnchor = s.Clone();
                StorySystemsAt(otherAnchor, StoryAnchors.HohCrowned);
                Assert.That(Knowledge.Of(otherAnchor, FactKinds.Alliance, pact.id).visibility, Is.EqualTo(FactVisibility.Private), "Only eviction night leaks.");
                var off = s.Clone();
                off.allianceLeakRulesStartWeek = 0;
                StorySystemsAt(off, StoryAnchors.EvictionNight);
                Assert.That(Knowledge.Of(off, FactKinds.Alliance, pact.id).visibility, Is.EqualTo(FactVisibility.Private), "Without the rules nothing leaks.");
                uint random = s.randomState;
                StorySystemsAt(s, StoryAnchors.EvictionNight);
                var fact = Knowledge.Of(s, FactKinds.Alliance, pact.id);
                Assert.That(fact.visibility, Is.EqualTo(FactVisibility.Whispered));
                Assert.That(s.randomState, Is.EqualTo(random));
                if (fact.knowers.Any(id => !pact.members.Contains(id))) carried++;
            }
            Assert.That(leaks, Is.GreaterThan(0), "Eighty houses: some coins land.");
            Assert.That(carried, Is.GreaterThan(0), "and the same night's gossip carried one.");
        }

        // ------------------------------------------------------------ D4-4: double-dealing

        /// <summary>
        /// The player juggling two pacts: "first" with the ally, and "other" with two more, whose fact the
        /// ally has not heard of and which is out as a whisper. Every view the ally holds of the other two
        /// is neutral, so nobody in it is their rival.
        /// </summary>
        private static (EpisodeState s, ContestantState ally, AllianceState other) Juggling(uint seed = 4101, bool rules = true)
        {
            var s = House(seed);
            EpisodeEngine.EnableStory(s, s.week);
            EpisodeEngine.EnableCommitments(s, s.week);
            if (rules) EpisodeEngine.EnableAllianceLeaks(s, s.week);
            var n = Others(s);
            PrivateFact(s, Pact(s, "first", true, s.playerId, n[0].id), s.week - 1);
            var other = Pact(s, "other", true, s.playerId, n[1].id, n[2].id);
            Knowledge.MakeKnown(s, PrivateFact(s, other, s.week - 1), FactVisibility.Whispered);
            SetScore(s, n[0].id, n[1].id, 0);
            SetScore(s, n[0].id, n[2].id, 0);
            return (s, n[0], other);
        }

        /// <summary>A story telling somebody of the pact between two people (LeakAlliance), as a beat applies it.</summary>
        private static void Tell(EpisodeState s, string a, string b, string listener) =>
            ApplyStoryEffect(s, new StoryEffectState { kind = StoryEffects.Spread, fromId = a, toId = b, thirdId = listener,
                type = FactKinds.Alliance, text = FactVisibility.Whispered });

        private static List<EpisodeEvent> DoubleDealingLines(EpisodeState s) => s.events.Where(e => e.kind == WaveDEventKinds.DoubleDealing).ToList();

        private static List<RelationshipEventState> Receipts(EpisodeState s, string from) =>
            s.relationships.Where(r => r.fromId == from && r.toId == s.playerId).SelectMany(r => r.events)
                .Where(e => e.type == StoryReceipts.DoubleDealt).ToList();

        /// <summary>Every view and record between two houseguests, neither of them the player.</summary>
        private static string BetweenOthers(EpisodeState s) => JsonConvert.SerializeObject(
            s.relationships.Where(r => r.fromId != s.playerId && r.toId != s.playerId).OrderBy(r => r.fromId + ">" + r.toId, StringComparer.Ordinal));

        [Test]
        public void AnAllyAStoryTellsOfThePlayersOtherPactKeepsTheReceiptAndThePlayerHearsWhoInOneLine()
        {
            var (s, ally, other) = Juggling();
            var n = Others(s);
            string others = BetweenOthers(s), grudges = JsonConvert.SerializeObject(s.story.grudges);
            uint random = s.randomState; int ids = s.nextSequence;
            double view = s.Score(ally.id, s.playerId);
            Tell(s, s.playerId, n[1].id, ally.id);

            var line = DoubleDealingLines(s).Single();
            Assert.That(line.text, Is.EqualTo(ally.name + " found out about The other Pact, your alliance with " + n[1].name + " and " + n[2].name
                + ". They won't forget you kept it from them."), "One line, word for word.");
            Assert.That(line.text, Is.EqualTo(AllianceLeaks.Line(s, ally.id, other)));
            Assert.That(line.audienceIds, Is.EqualTo(new[] { s.playerId, ally.id }), "To the player and the one who found out.");
            var receipt = Receipts(s, ally.id).Single();
            Assert.That(receipt.impactScore, Is.EqualTo(-10), "The receipt on the ledger, at its own impact.");
            Assert.That(receipt.decayable, Is.False, "and permanent.");
            Assert.That(receipt.week, Is.EqualTo(s.week));
            Assert.That(receipt.description, Is.EqualTo("Found out you are also working with " + n[1].name + " and " + n[2].name + "."));
            Assert.That(s.Score(ally.id, s.playerId), Is.EqualTo(view - 10).Within(1e-9), "Their view of the player moves with it.");
            Assert.That(BetweenOthers(s), Is.EqualTo(others), "Nothing moves between two houseguests,");
            Assert.That(JsonConvert.SerializeObject(s.story.grudges), Is.EqualTo(grudges), "and nobody holds a grudge.");
            Assert.That(s.randomState, Is.EqualTo(random), "No draw:");
            Assert.That(s.nextSequence, Is.EqualTo(ids + 2), "the receipt's id and the line's, and nothing else.");
            Assert.That(s.alliances.Single(a => a.id == "first").active && other.active, Is.True, "Both pacts stand.");

            // Told again: they know already, so nothing more.
            Tell(s, n[2].id, s.playerId, ally.id);
            Assert.That(DoubleDealingLines(s), Has.Count.EqualTo(1), "Once per houseguest and pact.");
            Assert.That(Receipts(s, ally.id), Has.Count.EqualTo(1));
        }

        [Test]
        public void AnAllyWithARivalInThePactHoldsAGrudgeInsteadAndHearsTheSameLine()
        {
            var (plain, plainAlly, _) = Juggling();
            var (grudge, grudgeAlly, _) = Juggling();
            var (hostile, hostileAlly, _) = Juggling();
            var n = Others(grudge);
            Grudges.Add(grudge, grudgeAlly.id, n[1].id, AllianceLeaks.RivalGrudge, GrudgeCauses.Story);
            SetScore(hostile, hostileAlly.id, n[2].id, AllianceLeaks.RivalScore);
            Assert.That(AllianceLeaks.HasRival(plain, plainAlly.id, plain.alliances.Single(a => a.id == "other")), Is.False);
            foreach (var s in new[] { plain, grudge, hostile }) Tell(s, s.playerId, n[1].id, Others(s)[0].id);

            string said = DoubleDealingLines(plain).Single().text;
            Assert.That(DoubleDealingLines(grudge).Single().text, Is.EqualTo(said), "Both reactions say the same line.");
            Assert.That(DoubleDealingLines(hostile).Single().text, Is.EqualTo(said));
            Assert.That(Grudges.Severity(plain, plainAlly.id, plain.playerId), Is.Zero);
            foreach (var (s, ally) in new[] { (grudge, grudgeAlly), (hostile, hostileAlly) })
            {
                Assert.That(Receipts(s, ally.id), Is.Empty, "A rival in the pact: a grudge, not the receipt.");
                var held = s.story.grudges.Single(g => g.holderId == ally.id && g.targetId == s.playerId);
                Assert.That(held.severity, Is.EqualTo(48).Within(1e-9), "Forty, by the allied ×1.2: past the refusal line.");
                Assert.That(held.cause, Is.EqualTo(GrudgeCauses.AllianceBetrayed));
                Assert.That(EpisodeEngine.GrudgeRefusesAlliance(s, ally.id), Is.True, "Forty or more refuses a pact.");
            }

            // A grudge already held stacks at half.
            var (stack, stackAlly, _) = Juggling();
            var m = Others(stack);
            Grudges.Add(stack, stackAlly.id, m[1].id, 30, GrudgeCauses.Story);
            Grudges.Add(stack, stackAlly.id, stack.playerId, 10, GrudgeCauses.Story);
            Tell(stack, stack.playerId, m[1].id, stackAlly.id);
            Assert.That(Grudges.Severity(stack, stackAlly.id, stack.playerId), Is.EqualTo(10 + 48 * 0.5).Within(1e-9));
        }

        [Test]
        public void ThePlayersCardSaysWhoFoundOutAndWhen()
        {
            var (s, ally, other) = Juggling();
            var n = Others(s);
            Tell(s, s.playerId, n[1].id, ally.id);
            var cards = AllianceRead.Yours(s);
            Assert.That(cards.Single(p => p.id == other.id).exposures.Select(e => e.week + ": " + e.text),
                Is.EqualTo(new[] { s.week + ": " + FinalistRead.FirstName(ally.name) + " found out about it." }));
            Assert.That(cards.Single(p => p.id == "first").exposures, Is.Empty, "The pact they share with the player is left alone.");
            // Renamed since: the line names the old name, the gap a renamed pact's other lines have too.
            other.name = "The Renamed Pact";
            Assert.That(AllianceRead.Yours(s).Single(p => p.id == other.id).exposures, Is.Empty);
        }

        [Test]
        public void TheHousesGossipCatchesThePlayerOutTooButOnlyOnce()
        {
            var (s, ally, other) = Juggling(4102);
            var n = Others(s);
            // The pact's two others would tell the ally first, and the ally is close to whom it is about.
            foreach (var teller in new[] { n[1], n[2] })
                foreach (var c in s.Active.Where(c => c.id != teller.id))
                    SetScore(s, teller.id, c.id, c.id == ally.id ? 95 : 0);
            SetScore(s, ally.id, n[2].id, 0); SetScore(s, ally.id, n[1].id, 0);
            var fact = Knowledge.Of(s, FactKinds.Alliance, other.id);
            string others = BetweenOthers(s);
            int start = s.week;
            for (int week = start; week < start + 60 && !fact.knowers.Contains(ally.id); week++)
            {
                s.week = week;
                StorySystemsAt(s, StoryAnchors.HohCrowned);
            }
            Assert.That(fact.knowers, Has.Member(ally.id), "The gossip reached the ally within sixty weeks of anchors.");
            Assert.That(DoubleDealingLines(s), Has.Count.EqualTo(1), "and they reacted, in one line.");
            Assert.That(Receipts(s, ally.id), Has.Count.EqualTo(1));
            Assert.That(BetweenOthers(s), Is.EqualTo(others), "Gossip and the reaction move nothing between two others.");
            int known = s.week;
            for (int week = known + 1; week < known + 6; week++) { s.week = week; StorySystemsAt(s, StoryAnchors.BlockSet); }
            Assert.That(DoubleDealingLines(s), Has.Count.EqualTo(1), "Knowers only grow: never twice.");
        }

        [Test]
        public void NobodyIsCaughtOutByAMemberAnEndedPactAnEvictedPlayerAnOutsiderOrOneWhoKnewBeforeAllying()
        {
            void Expect(string why, Action<EpisodeState, ContestantState, AllianceState> arrange, bool rules = true)
            {
                var (s, ally, other) = Juggling(4103, rules);
                arrange(s, ally, other);
                var n = Others(s);
                Tell(s, s.playerId, n[1].id, ally.id);
                Assert.That(DoubleDealingLines(s), Is.Empty, why + ": no line,");
                Assert.That(Receipts(s, ally.id), Is.Empty, why + ": no receipt,");
                Assert.That(Grudges.Severity(s, ally.id, s.playerId), Is.Zero, why + ": no grudge.");
            }
            Expect("Without the leak rules", (s, a, p) => { }, rules: false);
            Expect("A member of the pact", (s, a, p) => p.members.Add(a.id));
            Expect("An ended pact", (s, a, p) => p.active = false);
            Expect("An evicted player", (s, a, p) => s.Find(s.playerId).status = ContestantStatus.Evicted);
            Expect("Somebody the player shares no pact with", (s, a, p) => s.alliances.Single(x => x.id == "first").active = false);
            Expect("Somebody who knew before they allied", (s, a, p) => Knowledge.AddKnower(s, Knowledge.Of(s, FactKinds.Alliance, p.id), a.id));
            Expect("An evicted ally", (s, a, p) => a.status = ContestantStatus.Evicted);

            // A pact between others: an NPC finding out about an NPC pact is never the player's business.
            var t = Rules(4104);
            var m = Others(t);
            PrivateFact(t, Pact(t, "mine", true, t.playerId, m[0].id), t.week - 1);
            PrivateFact(t, Pact(t, "theirs", true, m[1].id, m[2].id), t.week - 1);
            Tell(t, m[1].id, m[2].id, m[0].id);
            Assert.That(Knowledge.Knows(Knowledge.Of(t, FactKinds.Alliance, "theirs"), m[0].id), Is.True);
            Assert.That(DoubleDealingLines(t), Is.Empty, "No reaction to a pact the player is not in.");
        }

        /// <summary>
        /// The same story made public: everyone in the house hears of the pact at once, and of them only the
        /// player's allies outside it react - one line each.
        /// </summary>
        [Test]
        public void AStoryMakingThePactPublicCatchesThePlayerOutWithEveryAllyOutsideIt()
        {
            var (s, ally, other) = Juggling(4105);
            var n = Others(s);
            var second = Pact(s, "second", true, s.playerId, n[3].id);
            PrivateFact(s, second, s.week - 1);
            SetScore(s, n[3].id, n[1].id, 0); SetScore(s, n[3].id, n[2].id, 0);
            ApplyStoryEffect(s, new StoryEffectState { kind = StoryEffects.Spread, fromId = n[1].id, toId = n[2].id,
                type = FactKinds.Alliance, text = FactVisibility.Public });
            Assert.That(DoubleDealingLines(s).Select(e => e.audienceIds[1]), Is.EquivalentTo(new[] { ally.id, n[3].id }),
                "The two allies outside it, and nobody else in the house.");
            Assert.That(Receipts(s, ally.id), Has.Count.EqualTo(1));
            Assert.That(Receipts(s, n[3].id), Has.Count.EqualTo(1));
        }

        /// <summary>
        /// A season under the hearing rules (D4-L2), as fresh seasons are: the gossip runs on its staged
        /// clone and installs only the facts, and the leak, the gossip and the reaction all still land - the
        /// coin is the plan's, the ally reacts once, and the hearing storage stays valid.
        /// </summary>
        [Test]
        public void UnderTheHearingRulesTheLeakAndTheGossipStillCatchThePlayerOut()
        {
            var (s, ally, other) = Juggling(4106);
            s.unifiedCommitmentRulesVersion = UnifiedCommitments.ProspectiveVersion;
            s.unifiedHearingRulesVersion = UnifiedCommitmentHearings.ProspectiveVersion;
            Assert.That(UnifiedCommitmentHearings.RulesOn(s), Is.True, "Precondition: the hearing rules are on.");
            var n = Others(s);
            var fact = Knowledge.Of(s, FactKinds.Alliance, other.id);
            fact.visibility = FactVisibility.Private;
            foreach (var teller in new[] { n[1], n[2] })
                foreach (var c in s.Active.Where(c => c.id != teller.id))
                    SetScore(s, teller.id, c.id, c.id == ally.id ? 95 : 0);
            int leakWeek = Enumerable.Range(s.week, 80).First(week => { var w = s.Clone(); w.week = week; return CoinLands(w, w.alliances.Single(a => a.id == other.id)); });
            s.week = leakWeek;
            StorySystemsAt(s, StoryAnchors.EvictionNight);
            fact = Knowledge.Of(s, FactKinds.Alliance, other.id);
            Assert.That(fact.visibility, Is.EqualTo(FactVisibility.Whispered), "The coin landed and the pact got out, through the staged gossip.");
            for (int week = leakWeek + 1; week < leakWeek + 60 && !fact.knowers.Contains(ally.id); week++)
            {
                s.week = week;
                StorySystemsAt(s, StoryAnchors.HohCrowned);
                fact = Knowledge.Of(s, FactKinds.Alliance, other.id);
            }
            Assert.That(fact.knowers, Has.Member(ally.id), "The staged gossip reached the ally.");
            Assert.That(DoubleDealingLines(s), Has.Count.EqualTo(1));
            Assert.That(Receipts(s, ally.id), Has.Count.EqualTo(1));
            Assert.That(UnifiedCommitmentHearings.ValidateStorage(s, out string error), Is.True, error);
        }

        /// <summary>The listen-in as the player presses it: one command, accepted, and the season legal after it.</summary>
        [Test]
        public void TheListenInIsOneLegalCommand()
        {
            var s = ContentCatalog.Create(4301);
            EpisodeEngine.EnableStory(s); EpisodeEngine.EnableCommitments(s); EpisodeEngine.EnableAllianceLeaks(s);
            var n = Others(s);
            var pact = new AllianceState { id = "alliance-npc-" + s.nextSequence++, name = "The Overheard Pact", active = true,
                members = new List<string> { n[0].id, n[1].id } };
            s.alliances.Add(pact);
            Knowledge.AllianceFormed(s, pact);
            s.randomState = StreamWhereTheListenIn(false);
            Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);
            var command = EpisodeEngineTests.Command(s, EpisodeCommandKind.Eavesdrop);
            command.targetId = n[0].id; command.secondTargetId = n[1].id;
            var result = new EpisodeEngine(s).Apply(command);
            Assert.That(result.accepted, Is.True, result.reason);
            var after = result.state;
            Assert.That(after.events.Last(e => e.kind == "eavesdrop").text, Does.EndWith(AllianceLeaks.ListenInSentence(after, pact)));
            Assert.That(Knowledge.Knows(Knowledge.Of(after, FactKinds.Alliance, pact.id), after.playerId), Is.True);
        }
    }
}
