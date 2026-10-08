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
    /// ACTIONS-DEALS-ALLIANCES-PLAN V3's gate: the alliances page shows the player's own pacts in
    /// full, and another houseguest's only on evidence the player holds - a fact they know, a play's
    /// receipt, the whisper that told them - never on the pact merely existing. Strength shows only
    /// through the player's own reading and the calls on the record, and an ending reads the same
    /// whatever a partner privately thinks. The engine's own lines the page reads are pinned here,
    /// word for word, by calling the engine. Unity-free, so the dotnet subset runs it
    /// (Tools/SimulationTests).
    /// </summary>
    public sealed class AllianceReadTests
    {
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Static;

        /// <summary>A house of eight in week six: the player and seven others, nobody allied.</summary>
        private static EpisodeState House(uint seed = 41)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
            s.week = 6;
            return s;
        }

        private static List<ContestantState> Others(EpisodeState s) => s.contestants.Where(c => !c.isPlayer).ToList();

        private static string First(ContestantState actor) => FinalistRead.FirstName(actor.name);

        private static AllianceState Pact(EpisodeState s, string id, bool active, params string[] members)
        {
            var pact = new AllianceState { id = id, name = "The " + id + " Pact", active = active, members = members.ToList() };
            s.alliances.Add(pact);
            return pact;
        }

        /// <summary>The pact's private fact, as the read rules make it at birth: its members know.</summary>
        private static HouseFactState PrivateFact(EpisodeState s, AllianceState pact)
        {
            Knowledge.AllianceFormed(s, pact);
            return Knowledge.Of(s, FactKinds.Alliance, pact.id);
        }

        /// <summary>A line in the season's log, written to <paramref name="audience"/>; the player's own when none is named.</summary>
        private static void Line(EpisodeState s, int week, string kind, string text, params string[] audience) =>
            s.events.Add(new EpisodeEvent
            {
                sequence = s.nextSequence++, week = week, kind = kind, text = text,
                audienceIds = audience.Length > 0 ? audience.ToList() : new List<string> { s.playerId },
            });

        private static void SetScore(EpisodeState s, string from, string to, double score)
        {
            var row = s.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (row == null) s.relationships.Add(row = new RelationshipState { fromId = from, toId = to });
            row.score = score;
        }

        private static IEnumerable<string> Dated(AllianceRead.SuspectedPact card) => card.evidence.Select(e => e.week + ": " + e.text);

        // ------------------------------------------------------------ the engine, called as it calls itself

        private static MethodInfo EngineMethod(string name, params Type[] parameters)
        {
            var method = typeof(EpisodeEngine).GetMethod(name, Private, null, parameters, null);
            Assert.That(method, Is.Not.Null, "The engine's " + name + "(" + string.Join(", ", parameters.Select(t => t.Name)) + "), which the page reads the words of.");
            return method;
        }

        /// <summary>One story effect, applied the way a beat applies it (EpisodeEngine.ApplyStoryEffect).</summary>
        private static void ApplyStoryEffect(EpisodeState s, StoryEffectState effect) =>
            EngineMethod("ApplyStoryEffect", typeof(EpisodeState), typeof(StoryEffectState), typeof(StorylineState),
                    typeof(string), typeof(string), typeof(int), typeof(bool))
                .Invoke(null, new object[] { s, effect, null, null, "alliances-test", 0, false });

        /// <summary>
        /// One story effect as a beat or a play applies it under the leak rules: the pact an alliance
        /// spread is about kept first (EpisodeEngine.RememberSpreadPact), then applied. Returns what was kept.
        /// </summary>
        private static Dictionary<StoryEffectState, AllianceState> ApplyRemembering(EpisodeState s, StoryEffectState effect)
        {
            var spread = new Dictionary<StoryEffectState, AllianceState>();
            EngineMethod("RememberSpreadPact", typeof(EpisodeState), typeof(StoryEffectState), typeof(Dictionary<StoryEffectState, AllianceState>))
                .Invoke(null, new object[] { s, effect, spread });
            ApplyStoryEffect(s, effect);
            return spread;
        }

        /// <summary>What the engine tells the player when pacts of theirs have ended (EpisodeEngine.TellThePlayerWhichAlliancesEnded).</summary>
        private static void TellThePlayer(EpisodeState s, params AllianceState[] ended) =>
            EngineMethod("TellThePlayerWhichAlliancesEnded", typeof(EpisodeState), typeof(List<AllianceState>))
                .Invoke(null, new object[] { s, ended.ToList() });

        /// <summary>A season driven to its first campaign with two on the block, the player's actions unspent.</summary>
        private static EpisodeState Campaign()
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(31));
            for (int i = 0; i < 900 && !(engine.Snapshot.phase == EpisodePhase.Campaign && engine.Snapshot.nominees.Count == 2); i++)
                Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            var s = engine.Snapshot;
            Assert.That(s.phase, Is.EqualTo(EpisodePhase.Campaign), "The season reached a campaign.");
            s.socialActions = 0; s.outOfPhaseSocialActions = 0;
            return s;
        }

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind, string targetId) =>
            new EpisodeCommand
            {
                id = "alliances-" + kind + "-" + s.revision + "-" + Guid.NewGuid().ToString("N"), actorId = s.playerId, kind = kind,
                targetId = targetId, expectedRevision = s.revision, expectedPhase = s.phase,
            };

        // ------------------------------------------------------------ pacts between others

        [Test]
        public void APactWithNoPlayerEvidenceNeverAppears()
        {
            var s = House();
            var n = Others(s);
            var secret = Pact(s, "secret", true, n[0].id, n[1].id);
            var fact = PrivateFact(s, secret);
            // Formed before the knowledge rules: no fact, so every voter counts it. Nothing ever showed the player.
            var legacy = Pact(s, "legacy", true, n[2].id, n[3].id);
            // What its members saw, and lines that reached somebody else.
            Line(s, 4, "alliance", secret.name + " is finished.", n[0].id, n[1].id);
            Line(s, 4, StoryLog.Whisper, AllianceRead.WhisperLine(s, fact), n[4].id);
            Line(s, 5, StoryLog.Receipt, AllianceRead.LearnedLine(s, legacy), n[5].id);
            // A listen-in on the pair, a read and an overheard vote: a standing, a read and a claim. None names a pact.
            s.ledger.standings.Add(new StandingRow { week = 5, fromId = n[0].id, toId = n[1].id, source = ClaimSource.Overheard, score = 70 });
            s.ledger.standings.Add(new StandingRow { week = 5, fromId = n[2].id, toId = s.playerId, source = ClaimSource.Read, score = 40 });
            s.ledger.claims.Add(new ClaimRow { week = 5, voterId = n[2].id, targetId = n[4].id, source = ClaimSource.Overheard });
            s.memories.Add(new MemoryState { ownerId = s.playerId, subjectId = n[0].id, week = 5, isPrivate = true,
                text = "I overheard " + n[0].name + " and " + n[1].name + " in week 5. They sounded close." });

            Assert.That(FinalistRead.AllianceCertainty(s, secret), Is.Null, "The finalist cards' own rule agrees: unknown.");
            Assert.That(FinalistRead.AllianceCertainty(s, legacy), Is.Null);
            var page = AllianceRead.Read(s);
            Assert.That(page.suspected, Is.Empty, "No evidence, no card - not even an unknown one.");
            Assert.That(page.yours, Is.Empty);
            Assert.That(AllianceRead.SuspectedPairs(s), Is.Empty, "and nothing for the web to draw.");
            Assert.That(JsonConvert.SerializeObject(page), Does.Not.Contain(secret.name).And.Not.Contain(legacy.name));
        }

        /// <summary>
        /// The house's rumour mill, run as the engine runs it at an anchor (StorySystemsAt, then
        /// Knowledge.Spread): it passes one whispered fact to one new knower, and its whisper names
        /// that pact's first two members. A bigger pact the same two open is a different fact the
        /// mill has not passed on, so the page never shows it.
        /// </summary>
        [Test]
        public void TheRumourMillsWhisperAboutOnePactNeverRevealsAnother()
        {
            var s = House();
            EpisodeEngine.EnableStory(s, s.week);
            var n = Others(s);
            var pair = Pact(s, "pair", true, n[0].id, n[1].id);
            var pairFact = PrivateFact(s, pair);
            Knowledge.MakeKnown(s, pairFact, FactVisibility.Whispered);
            var bigger = Pact(s, "bigger", true, n[0].id, n[1].id, n[2].id);
            var biggerFact = PrivateFact(s, bigger);
            // The player is the one either of the pair would tell, and close to whom it is about.
            SetScore(s, n[0].id, s.playerId, 90);
            SetScore(s, n[1].id, s.playerId, 90);
            SetScore(s, s.playerId, n[1].id, 60);
            var mill = EngineMethod("StorySystemsAt", typeof(EpisodeState), typeof(string));
            for (int week = s.week; week < s.week + 60 && !Knowledge.Knows(pairFact, s.playerId); week++)
            {
                s.week = week;
                mill.Invoke(null, new object[] { s, StoryAnchors.HohCrowned });
            }
            Assert.That(Knowledge.Knows(pairFact, s.playerId), Is.True, "The rumour reached the player within sixty weeks of anchors.");
            Assert.That(Knowledge.Knows(biggerFact, s.playerId), Is.False, "The mill passes the fact it spreads, and no other.");
            var whisper = s.events.Single(e => e.kind == StoryLog.Whisper);
            Assert.That(whisper.text, Is.EqualTo(AllianceRead.WhisperLine(s, biggerFact)), "The same two names open both pacts' whispers.");

            var card = AllianceRead.Suspected(s).Single();
            Assert.That(card.memberIds, Is.EqualTo(new[] { n[0].id, n[1].id }),
                "The pact the player heard of, and not the bigger one the same two names open.");
            Assert.That(Dated(card), Is.EqualTo(new[] { whisper.week + ": " + whisper.text }), "The engine's whisper, word for word, and its week.");
            Assert.That(AllianceRead.SuspectedPairs(s), Is.EqualTo(new[] { (n[0].id, n[1].id) }));

            // Two pacts of the same people are one card, and it says nothing of there being two.
            string before = JsonConvert.SerializeObject(AllianceRead.Suspected(s));
            var again = Pact(s, "again", false, n[1].id, n[0].id);
            Knowledge.AddKnower(s, PrivateFact(s, again), s.playerId);
            Assert.That(JsonConvert.SerializeObject(AllianceRead.Suspected(s)), Is.EqualTo(before));
        }

        /// <summary>
        /// A story's leak (LeakAlliance), applied as a beat applies it, in a season without the leak
        /// rules: the engine makes the player a knower of every pact holding the two people it names,
        /// while the receipt names one; the page follows the engine's knowledge, so the bigger pact
        /// shows too, undated. Pinned as such a season plays it, to its end; under the leak rules
        /// (WAVE-D-NPC-PACTS-PLAN D4) the spread grants one pact, the receipt's
        /// (<see cref="UnderTheLeakRulesAStoryLeakLetsThePlayerKnowOnlyThePactItsReceiptNames"/>).
        /// </summary>
        [Test]
        public void AStoryLeakTodayLetsThePlayerKnowEveryPactOfThePairItNames()
        {
            var s = House();
            EpisodeEngine.EnableStory(s, s.week);
            var n = Others(s);
            var pair = Pact(s, "pair", true, n[0].id, n[1].id);
            var pairFact = PrivateFact(s, pair);
            var bigger = Pact(s, "bigger", true, n[0].id, n[1].id, n[2].id);
            var biggerFact = PrivateFact(s, bigger);
            var leak = new StoryEffectState { kind = StoryEffects.Spread, fromId = n[0].id, toId = n[1].id, thirdId = s.playerId,
                type = FactKinds.Alliance, text = FactVisibility.Whispered };
            ApplyStoryEffect(s, leak);
            string receipt = PlayReceipts.For(s, new[] { leak }).Single();
            Line(s, s.week, StoryLog.Receipt, receipt);
            Assert.That(receipt, Is.EqualTo(AllianceRead.LearnedLine(s, pair)), "The receipt names the first pact holding the two.");
            Assert.That(Knowledge.Knows(pairFact, s.playerId) && Knowledge.Knows(biggerFact, s.playerId), Is.True,
                "Today the spread reaches every pact holding both; this flips when the engine narrows it.");

            var cards = AllianceRead.Suspected(s);
            Assert.That(cards.Select(c => c.memberIds.Count), Is.EqualTo(new[] { 2, 3 }), "The leaked pact, dated, then the bigger one.");
            Assert.That(Dated(cards[0]), Is.EqualTo(new[] { s.week + ": " + receipt }));
            Assert.That(Dated(cards[1]), Is.EqualTo(new[] { "0: " + AllianceRead.HeardOfIt }), "Known to the engine, with no line that told the player.");
        }

        /// <summary>
        /// The same leak under the leak rules (WAVE-D-NPC-PACTS-PLAN §2.3, the C8 defect fixed): one pair
        /// is one pact, so the spread grants the pact the player does not yet know of - here the first -
        /// and the receipt names that pact, as the engine resolved it before the grant. The bigger pact
        /// the same two open stays dark, and the page shows one card, dated by the receipt. A second leak
        /// of the same two, the first now known, grants the bigger one, and its receipt names it; asked
        /// after the grant without the engine's pact, the receipt would name the known first one, so
        /// under the rules that form says nothing.
        /// </summary>
        [Test]
        public void UnderTheLeakRulesAStoryLeakLetsThePlayerKnowOnlyThePactItsReceiptNames()
        {
            var s = House();
            EpisodeEngine.EnableStory(s, s.week);
            EpisodeEngine.EnableCommitments(s, s.week);
            EpisodeEngine.EnableAllianceLeaks(s, s.week);
            Assert.That(AllianceLeaks.On(s), Is.True);
            var n = Others(s);
            var pair = Pact(s, "pair", true, n[0].id, n[1].id);
            var pairFact = PrivateFact(s, pair);
            var bigger = Pact(s, "bigger", true, n[0].id, n[1].id, n[2].id);
            var biggerFact = PrivateFact(s, bigger);
            var leak = new StoryEffectState { kind = StoryEffects.Spread, fromId = n[0].id, toId = n[1].id, thirdId = s.playerId,
                type = FactKinds.Alliance, text = FactVisibility.Whispered };
            uint random = s.randomState; int ids = s.nextSequence;
            var granted = ApplyRemembering(s, leak);
            Assert.That(s.randomState, Is.EqualTo(random), "The spread draws nothing,");
            Assert.That(s.nextSequence, Is.EqualTo(ids), "and writes no id.");
            Assert.That(granted[leak], Is.SameAs(pair), "The engine resolved the pact the player does not know of, before the grant.");
            string receipt = PlayReceipts.For(s, new[] { leak }, granted).Single();
            Line(s, s.week, StoryLog.Receipt, receipt);
            Assert.That(receipt, Is.EqualTo(AllianceRead.LearnedLine(s, pair)), "The receipt names the pact the spread granted.");
            Assert.That(Knowledge.Knows(pairFact, s.playerId), Is.True);
            Assert.That(pairFact.visibility, Is.EqualTo(FactVisibility.Whispered));
            Assert.That(Knowledge.Knows(biggerFact, s.playerId), Is.False, "One pair, one pact: the bigger one stays dark.");
            Assert.That(biggerFact.visibility, Is.EqualTo(FactVisibility.Private), "and is not widened.");

            var cards = AllianceRead.Suspected(s);
            Assert.That(cards.Select(c => c.memberIds.Count), Is.EqualTo(new[] { 2 }), "One card: the pact the player was given.");
            Assert.That(Dated(cards[0]), Is.EqualTo(new[] { s.week + ": " + receipt }));

            // Again, the pair now known: the resolver turns to the one the player does not know of.
            var again = new StoryEffectState { kind = StoryEffects.Spread, fromId = n[1].id, toId = n[0].id, thirdId = s.playerId,
                type = FactKinds.Alliance, text = FactVisibility.Whispered };
            var grantedAgain = ApplyRemembering(s, again);
            Assert.That(Knowledge.Knows(biggerFact, s.playerId), Is.True);
            Assert.That(grantedAgain[again], Is.SameAs(bigger), "Resolved before the grant: the one the player did not know of yet.");
            Assert.That(PlayReceipts.For(s, new[] { again }, grantedAgain).Single(),
                Is.EqualTo(AllianceRead.LearnedLine(s, bigger)), "The engine's receipt names the pact it resolved before the grant.");
            Assert.That(PlayReceipts.For(s, new[] { again }), Is.Empty,
                "Without the engine's pact the receipt would name the known pair, listed first: under the rules it names none.");
        }

        [Test]
        public void ALeakedFactMakesItAppearWithItsEvidence()
        {
            var s = House();
            var n = Others(s);
            var pact = Pact(s, "leaked", true, n[0].id, n[1].id);
            var fact = PrivateFact(s, pact);
            Assert.That(AllianceRead.Suspected(s), Is.Empty, "Before the leak, nothing.");

            // The leak as the engine writes it (LeakAlliance): one more knower, out as a whisper, and
            // the play's receipt to the player in the receipts' own words.
            Knowledge.AddKnower(s, fact, s.playerId);
            Knowledge.MakeKnown(s, fact, FactVisibility.Whispered);
            string receipt = PlayReceipts.For(s, new[]
            {
                new StoryEffectState { kind = StoryEffects.Spread, fromId = n[0].id, toId = n[1].id, thirdId = s.playerId,
                    type = FactKinds.Alliance, text = FactVisibility.Whispered },
            }).Single();
            Assert.That(receipt, Is.EqualTo(AllianceRead.LearnedLine(s, pact)), "The page reads the receipt the engine writes, word for word.");
            Line(s, 3, StoryLog.Receipt, receipt);
            string state = JsonConvert.SerializeObject(s);
            uint random = s.randomState;

            var card = AllianceRead.Suspected(s).Single();
            Assert.That(card.memberIds, Is.EqualTo(new[] { n[0].id, n[1].id }));
            Assert.That(card.certainty, Is.EqualTo(FinalistRead.Suspected), "Heard of, not seen.");
            Assert.That(Dated(card), Is.EqualTo(new[] { "3: " + receipt }), "What the player was shown, and the week.");
            Assert.That(AllianceRead.SuspectedPairs(s), Is.EqualTo(new[] { (n[0].id, n[1].id) }));
            Assert.That(JsonConvert.SerializeObject(s), Is.EqualTo(state), "Reading the page must not change the state,");
            Assert.That(s.randomState, Is.EqualTo(random), "or draw from its generator.");

            // Through the house's rumour mill: the engine's own whisper dates a pact the player now knows of.
            var heard = Pact(s, "heard", true, n[2].id, n[3].id);
            var heardFact = PrivateFact(s, heard);
            Knowledge.AddKnower(s, heardFact, s.playerId);
            string whisper = (string)EngineMethod("Whisper", typeof(EpisodeState), typeof(HouseFactState)).Invoke(null, new object[] { s, heardFact });
            Assert.That(whisper, Is.EqualTo(AllianceRead.WhisperLine(s, heardFact)), "The page matches the engine's whisper, word for word.");
            Line(s, 5, StoryLog.Whisper, whisper);
            var second = AllianceRead.Suspected(s).Single(c => c.memberIds.Contains(n[2].id));
            Assert.That(Dated(second), Is.EqualTo(new[] { "5: " + whisper }));
            Assert.That(second.certainty, Is.EqualTo(FinalistRead.Suspected));

            // Out in the open: confirmed, and said without a tense - the house knowing is not the pact standing.
            Knowledge.MakeKnown(s, heardFact, FactVisibility.Public);
            second = AllianceRead.Suspected(s).Single(c => c.memberIds.Contains(n[2].id));
            Assert.That(second.certainty, Is.EqualTo(FinalistRead.Confirmed));
            Assert.That(Dated(second), Is.EqualTo(new[] { "5: " + whisper, "0: " + AllianceRead.OutInTheOpen }));
            Assert.That(AllianceRead.OutInTheOpen, Does.Not.Contain("are working"), "Tenseless.");

            // A pact the player knows of whose line the log has let go still shows, undated.
            var older = Pact(s, "older", true, n[4].id, n[5].id);
            Knowledge.AddKnower(s, PrivateFact(s, older), s.playerId);
            var third = AllianceRead.Suspected(s).Single(c => c.memberIds.Contains(n[4].id));
            Assert.That(Dated(third), Is.EqualTo(new[] { "0: " + AllianceRead.HeardOfIt }));
            Assert.That(AllianceRead.Suspected(s).Select(c => c.memberIds[0]), Is.EqualTo(new[] { n[0].id, n[2].id, n[4].id }),
                "The earliest news first, the undated last.");
        }

        // ------------------------------------------------------------ the player's own pacts

        [Test]
        public void YourPactListsItsCallsAndWhoFollowedFromASeededLedger()
        {
            var s = House();
            var n = Others(s);
            var core = Pact(s, "core", true, s.playerId, n[0].id, n[1].id);
            s.ledger.alliances.Add(new AllianceRow { id = core.id, why = "player", startedWeek = 2 });
            s.ledger.calls.Add(new BlocCallRow { week = 5, allianceId = core.id, callerId = s.playerId, targetId = n[3].id,
                followed = new List<string> { n[0].id, n[1].id } });
            s.ledger.calls.Add(new BlocCallRow { week = 3, allianceId = core.id, callerId = s.playerId, targetId = n[2].id,
                followed = new List<string> { n[0].id }, defected = new List<string> { n[1].id } });
            var other = Pact(s, "other", true, s.playerId, n[4].id);
            s.ledger.alliances.Add(new AllianceRow { id = other.id, why = "player", startedWeek = 4 });
            s.ledger.calls.Add(new BlocCallRow { week = 4, allianceId = other.id, callerId = s.playerId, targetId = n[2].id,
                defected = new List<string> { n[4].id } });
            SetScore(s, s.playerId, n[0].id, 30);
            SetScore(s, s.playerId, n[1].id, -20);
            SetScore(s, n[1].id, s.playerId, -80);
            s.deals.Add(new DealState { id = "f2", type = DealKind.FinalTwo, proposerId = s.playerId, recipientId = n[0].id, status = DealStatus.Active, week = 3 });
            s.deals.Add(new DealState { id = "theirs", type = DealKind.FinalTwo, proposerId = n[0].id, recipientId = n[2].id, status = DealStatus.Active, week = 3 });
            s.deals.Add(new DealState { id = "no", type = DealKind.VetoUse, proposerId = s.playerId, recipientId = n[1].id, status = DealStatus.Declined, week = 4 });
            s.deals.Add(new DealState { id = "kept", type = DealKind.VoteEvict, proposerId = n[1].id, recipientId = s.playerId, targetId = n[3].id,
                status = DealStatus.Fulfilled, week = 5 });

            var pacts = AllianceRead.Yours(s);
            Assert.That(pacts.Select(p => p.id), Is.EqualTo(new[] { core.id, other.id }));
            var pact = pacts[0];
            Assert.That(pact.name, Is.EqualTo(core.name));
            Assert.That(pact.active, Is.True);
            Assert.That(pact.ended, Is.Null);
            Assert.That(pact.formedWeek, Is.EqualTo(2), "The week the ledger says it began: the player founded it.");
            Assert.That(pact.formed, Is.EqualTo("You formed it with " + First(n[0]) + " and " + First(n[1]) + "."));
            Assert.That(pact.calls.Select(c => c.week), Is.EqualTo(new[] { 3, 5 }), "Its calls, oldest first, and no other pact's.");
            Assert.That(pact.calls[0].targetId, Is.EqualTo(n[2].id));
            Assert.That(pact.calls[0].followed, Is.EqualTo(new[] { n[0].id }));
            Assert.That(pact.calls[0].defected, Is.EqualTo(new[] { n[1].id }));
            Assert.That(pact.calls[1].followed, Is.EqualTo(new[] { n[0].id, n[1].id }));
            Assert.That(pact.calls[1].defected, Is.Empty);

            var riley = pact.members.Single(m => m.id == n[0].id);
            var jo = pact.members.Single(m => m.id == n[1].id);
            Assert.That(pact.members.Select(m => m.id), Is.EqualTo(new[] { n[0].id, n[1].id }), "Everyone but the player.");
            Assert.That((riley.followed, riley.ignored), Is.EqualTo((2, 0)));
            Assert.That((jo.followed, jo.ignored), Is.EqualTo((1, 1)));
            Assert.That(riley.reading, Is.EqualTo(AllianceRead.Friendly));
            Assert.That(jo.reading, Is.EqualTo(AllianceRead.Wary), "The player's own reading - never \"Allied\", which every member is.");
            Assert.That(pact.deals.Select(d => d.text), Is.EqualTo(new[]
            {
                "Final Two Deal with " + First(n[0]) + " · agreed",
                "Vote to Evict with " + First(n[1]) + ", on " + First(n[3]) + " · honoured",
            }), "The deals the player agreed with its members: not an offer declined, not a deal between two others.");

            // Strength shows only through the player's reading and the calls: a member's private view moves nothing.
            string before = JsonConvert.SerializeObject(AllianceRead.Read(s));
            SetScore(s, n[1].id, s.playerId, 80);
            SetScore(s, n[0].id, s.playerId, -90);
            SetScore(s, n[0].id, n[1].id, -90);
            Assert.That(JsonConvert.SerializeObject(AllianceRead.Read(s)), Is.EqualTo(before));
        }

        [Test]
        public void AnEndedPactShowsWhenAndWhyAndNeverAPartnersPrivateView()
        {
            var s = House();
            var n = Others(s);
            var pact = Pact(s, "gone", false, s.playerId, n[0].id);
            var row = new AllianceRow { id = pact.id, why = "player/turned", startedWeek = 2, endedWeek = 4 };
            s.ledger.alliances.Add(row);

            var read = AllianceRead.Yours(s).Single();
            Assert.That(read.active, Is.False);
            Assert.That(read.formedWeek, Is.EqualTo(2));
            Assert.That(read.endedWeek, Is.EqualTo(4), "When it ended,");
            // The ledger's turned, soured and ended are all read from views the player never sees.
            foreach (var why in new[] { "player/turned", "player/soured", "player/ended", "player/" })
            {
                row.why = why;
                Assert.That(AllianceRead.Yours(s).Single().ended, Is.EqualTo(AllianceRead.JustEnded), why + " reads like the others.");
            }

            // A departure is public, but only who was gone by the week it ended is said.
            n[0].status = ContestantStatus.Jury;
            row.why = "player/left-house";
            Assert.That(AllianceRead.Yours(s).Single().ended, Is.EqualTo(AllianceRead.JustEnded), "No record of when they left: nothing is said.");
            s.ledger.power.Add(new PowerRow { week = 3, evicteeId = n[0].id, hohId = n[3].id });
            Assert.That(AllianceRead.Yours(s).Single().ended, Is.EqualTo(First(n[0]) + " left the house."));
            s.ledger.power.Last().week = 5;
            Assert.That(AllianceRead.Yours(s).Single().ended, Is.EqualTo(AllianceRead.JustEnded), "Gone only after it ended: not why it ended.");

            // A partner gone weeks before does not make a later ending a departure: the ledger's
            // left-house outranks every other reason, so with one partner still in the house it says nothing.
            var three = Pact(s, "three", false, s.playerId, n[1].id, n[2].id);
            s.ledger.alliances.Add(new AllianceRow { id = three.id, why = "player/left-house", startedWeek = 1, endedWeek = 5 });
            n[1].status = ContestantStatus.Jury;
            s.ledger.power.Add(new PowerRow { week = 2, evicteeId = n[1].id, hohId = n[3].id });
            Assert.That(AllianceRead.Yours(s).Single(p => p.id == three.id).ended, Is.EqualTo(AllianceRead.JustEnded));
            n[2].status = ContestantStatus.Evicted;
            s.ledger.power.Add(new PowerRow { week = 4, evicteeId = n[2].id, hohId = n[3].id });
            Assert.That(AllianceRead.Yours(s).Single(p => p.id == three.id).ended, Is.EqualTo(First(n[1]) + " and " + First(n[2]) + " left the house."));
        }

        /// <summary>
        /// The leak the review found: with the engine's own ending line gone from the log, the
        /// ledger's reason once told "It fell apart." from "It ended." - which is a partner's private
        /// view. Formed and left through the engine twice, with the partner privately warm and then
        /// cold: the ledger tells the two apart, and the page must not.
        /// </summary>
        [Test]
        public void APartnersPrivateViewNeverChangesHowALeftPactReads()
        {
            var s = Campaign();
            var friend = s.Active.First(c => !c.isPlayer && !s.Allied(s.playerId, c.id));
            SetScore(s, friend.id, s.playerId, 30);
            SetScore(s, s.playerId, friend.id, 20);
            var engine = new EpisodeEngine(s);
            Assert.That(engine.Apply(Command(s, EpisodeCommandKind.FormAlliance, friend.id)).accepted, Is.True);
            var formed = engine.Snapshot;
            string pactId = formed.alliances.Last(a => a.active && a.members.Contains(friend.id)).id;

            var reasons = new List<string>();
            var withLine = new List<string>();
            string Leave(double partnersView)
            {
                var start = formed.Clone();
                SetScore(start, friend.id, start.playerId, partnersView);
                var leaving = new EpisodeEngine(start);
                var left = leaving.Apply(Command(start, EpisodeCommandKind.LeaveAlliance, friend.id));
                Assert.That(left.accepted, Is.True, left.reason);
                var after = leaving.Snapshot;
                reasons.Add(after.ledger.alliances.Single(r => r.id == pactId).why);
                withLine.Add(AllianceRead.Yours(after).Single(p => p.id == pactId).ended);
                after.events.RemoveAll(e => e.kind == "alliance");
                return JsonConvert.SerializeObject(AllianceRead.Yours(after));
            }
            string warm = Leave(5), cold = Leave(-25);
            Assert.That(reasons, Is.EqualTo(new[] { "player/ended", "player/turned" }), "The ledger reads the partner's private view.");
            Assert.That(withLine, Is.EqualTo(new[] { AllianceRead.YouLeft, AllianceRead.YouLeft }), "While the line is on the log it says why.");
            Assert.That(cold, Is.EqualTo(warm), "With the line gone the page reads the same, whatever the partner thinks.");
            Assert.That(warm, Does.Contain(AllianceRead.JustEnded));
        }

        [Test]
        public void AnEndingLineThatCouldMeanAnotherPactIsNotRead()
        {
            var s = House();
            var n = Others(s);
            var pair = Pact(s, "pair", false, s.playerId, n[0].id);
            var trio = Pact(s, "trio", false, s.playerId, n[0].id, n[1].id);
            s.ledger.alliances.Add(new AllianceRow { id = pair.id, why = "player/ended", startedWeek = 2, endedWeek = 4 });
            var trioRow = new AllianceRow { id = trio.id, why = "player/ended", startedWeek = 2, endedWeek = 4 };
            s.ledger.alliances.Add(trioRow);
            Line(s, 4, "alliance", "You left the alliance with " + n[0].name + ".", s.playerId, n[0].id);
            Assert.That(AllianceRead.Yours(s).Select(p => p.ended), Is.EqualTo(new[] { AllianceRead.JustEnded, AllianceRead.JustEnded }),
                "Both pacts held them and both ended that week: the walk-out names neither.");
            trioRow.endedWeek = 3;
            Assert.That(AllianceRead.Yours(s).Single(p => p.id == pair.id).ended, Is.EqualTo(AllianceRead.YouLeft), "Alone that week, it is this pact's.");
            Assert.That(AllianceRead.Yours(s).Single(p => p.id == trio.id).ended, Is.EqualTo(AllianceRead.JustEnded), "and not a line from another week.");

            // A line naming the pact outranks one naming a member.
            Line(s, 4, "alliance", pair.name + " is finished.", s.playerId, n[0].id);
            Assert.That(AllianceRead.Yours(s).Single(p => p.id == pair.id).ended, Is.EqualTo(AllianceRead.CalledOff));

            // Two pacts of the player's by one name, ended the same week: a line naming that name names neither.
            foreach (var (id, members) in new[]
                     {
                         ("same-1", new List<string> { s.playerId, n[3].id }),
                         ("same-2", new List<string> { s.playerId, n[3].id, n[4].id }),
                     })
            {
                s.alliances.Add(new AllianceState { id = id, name = "The Same Pact", active = false, members = members });
                s.ledger.alliances.Add(new AllianceRow { id = id, why = "player/ended", startedWeek = 2, endedWeek = 5 });
            }
            Line(s, 5, "alliance", "The Same Pact is finished.", s.playerId, n[3].id);
            Assert.That(AllianceRead.Yours(s).Where(p => p.name == "The Same Pact").Select(p => p.ended),
                Is.EqualTo(new[] { AllianceRead.JustEnded, AllianceRead.JustEnded }), "Either could be meant, so neither is read.");

            // With no ledger row the week is unknown: only a line naming the pact itself is read.
            var old = Pact(s, "old", false, s.playerId, n[2].id);
            Line(s, 2, "alliance", "You left the alliance with " + n[2].name + ".", s.playerId, n[2].id);
            Line(s, 3, "alliance", "Your alliance with " + n[2].name + " has fallen apart.", s.playerId, n[2].id);
            var undated = AllianceRead.Yours(s).Single(p => p.id == old.id);
            Assert.That(undated.endedWeek, Is.Zero);
            Assert.That(undated.ended, Is.EqualTo(AllianceRead.JustEnded), "Lines from any week, naming only a member, prove nothing.");
            Line(s, 3, "alliance", old.name + " is finished.", s.playerId, n[2].id);
            Assert.That(AllianceRead.Yours(s).Single(p => p.id == old.id).ended, Is.EqualTo(AllianceRead.CalledOff));
        }

        /// <summary>Every engine line the page reads for an ending, written by the engine itself.</summary>
        [Test]
        public void TheEnginesOwnEndingLinesReadAsThePageSaysThem()
        {
            var s = House();
            EpisodeEngine.EnableStory(s, s.week);
            var n = Others(s);

            // Fallen apart (EpisodeEngine.TellThePlayerWhichAlliancesEnded), and only while the line is there.
            var soured = Pact(s, "alliance-11", true, s.playerId, n[0].id);
            EpisodeEngine.ReconcileAllianceRows(s);
            soured.active = false;
            EpisodeEngine.ReconcileAllianceRows(s);
            TellThePlayer(s, soured);
            Assert.That(s.events.Last().text, Is.EqualTo("Your alliance with " + n[0].name + " has fallen apart."));
            Assert.That(AllianceRead.Yours(s).Single(p => p.id == soured.id).ended, Is.EqualTo(AllianceRead.FellApart));
            s.events.RemoveAt(s.events.Count - 1);
            Assert.That(AllianceRead.Yours(s).Single(p => p.id == soured.id).ended, Is.EqualTo(AllianceRead.JustEnded));

            // Everybody else in it has left the house, said in the plural for two.
            var trio = Pact(s, "alliance-12", true, s.playerId, n[1].id, n[2].id);
            EpisodeEngine.ReconcileAllianceRows(s);
            n[1].status = ContestantStatus.Jury; n[2].status = ContestantStatus.Jury;
            trio.active = false;
            EpisodeEngine.ReconcileAllianceRows(s);
            TellThePlayer(s, trio);
            Assert.That(s.events.Last().text, Is.EqualTo(n[1].name + " and " + n[2].name + " have left the house, and your alliance has ended."));
            Assert.That(AllianceRead.Yours(s).Single(p => p.id == trio.id).ended, Is.EqualTo(First(n[1]) + " and " + First(n[2]) + " left the house."));

            // A story ends it (StoryEffects.AllianceEnd): "{name} is finished."
            var cut = Pact(s, "alliance-13", true, s.playerId, n[3].id);
            EpisodeEngine.ReconcileAllianceRows(s);
            ApplyStoryEffect(s, new StoryEffectState { kind = StoryEffects.AllianceEnd, fromId = s.playerId, toId = n[3].id });
            EpisodeEngine.ReconcileAllianceRows(s);
            Assert.That(s.events.Last().text, Is.EqualTo(cut.name + " is finished."));
            Assert.That(AllianceRead.Yours(s).Single(p => p.id == cut.id).ended, Is.EqualTo(AllianceRead.CalledOff));

            // A story takes a member out of a pair (StoryEffects.AllianceLeave): "{name} is out of {pact}."
            var pair = Pact(s, "alliance-14", true, s.playerId, n[4].id);
            EpisodeEngine.ReconcileAllianceRows(s);
            ApplyStoryEffect(s, new StoryEffectState { kind = StoryEffects.AllianceLeave, fromId = n[4].id, toId = s.playerId });
            EpisodeEngine.ReconcileAllianceRows(s);
            Assert.That(s.events.Last().text, Is.EqualTo(n[4].name + " is out of " + pair.name + "."));
            Assert.That(AllianceRead.Yours(s).Single(p => p.id == pair.id).ended, Is.EqualTo(First(n[4]) + " is out of it."));
            Assert.That(AllianceRead.Yours(s).Where(p => !p.active).Select(p => p.endedWeek).Distinct(), Is.EqualTo(new[] { s.week }));
        }

        [Test]
        public void LeavingAPactThroughTheEngineIsOnThePageWhenAndWhy()
        {
            var s = Campaign();
            var friend = s.Active.First(c => !c.isPlayer && !s.Allied(s.playerId, c.id));
            SetScore(s, friend.id, s.playerId, 30);
            var engine = new EpisodeEngine(s);
            var formed = engine.Apply(Command(s, EpisodeCommandKind.FormAlliance, friend.id));
            Assert.That(formed.accepted, Is.True, formed.reason);
            var after = engine.Snapshot;
            var pact = AllianceRead.Yours(after).Single(p => p.active && p.members.Any(m => m.id == friend.id));
            Assert.That(pact.formedWeek, Is.EqualTo(after.week));
            Assert.That(pact.formed, Is.EqualTo("You formed it with " + First(friend) + "."));

            var left = engine.Apply(Command(after, EpisodeCommandKind.LeaveAlliance, friend.id));
            Assert.That(left.accepted, Is.True, left.reason);
            var ended = AllianceRead.Yours(engine.Snapshot).Single(p => p.id == pact.id);
            Assert.That(ended.active, Is.False);
            Assert.That(ended.endedWeek, Is.EqualTo(after.week));
            Assert.That(ended.ended, Is.EqualTo(AllianceRead.YouLeft), "The engine's own line to the player says why.");
        }

        [Test]
        public void APactThePlayerWasBroughtIntoIsDatedByTheInvitationAlone()
        {
            var s = House();
            var n = Others(s);
            // The engine's invitation (EpisodeEngine.AllyThroughInvitation) brings the player into an
            // NPC pact made in week 2, and says so in its own words in week 6.
            var theirs = new AllianceState { id = "alliance-npc-77", name = "The " + First(n[0]) + " and " + First(n[1]) + " Pact",
                members = new List<string> { n[0].id, n[1].id }, active = true };
            s.alliances.Add(theirs);
            s.ledger.alliances.Add(new AllianceRow { id = theirs.id, why = "npc", startedWeek = 2 });
            // Nobody in it below the hostility line with the player, nor the player sour on them: it will have them.
            SetScore(s, n[1].id, s.playerId, 20);
            SetScore(s, s.playerId, n[1].id, 20);
            EngineMethod("AllyThroughInvitation", typeof(EpisodeState), typeof(string)).Invoke(null, new object[] { s, n[0].id });
            Assert.That(theirs.members.Last(), Is.EqualTo(s.playerId), "Brought in at the end.");
            Assert.That(s.events.Last().text, Is.EqualTo(n[0].name + " brought you into " + theirs.name + "."));

            var read = AllianceRead.Yours(s).Single();
            Assert.That(read.formedWeek, Is.EqualTo(6), "The week the player came in, not the week its members made it.");
            Assert.That(read.formed, Is.EqualTo(First(n[0]) + " brought you in."));
            Assert.That(read.members.Select(m => m.id), Is.EqualTo(new[] { n[0].id, n[1].id }));
            Assert.That(FinalistRead.RelationshipLine(s, n[1].id), Is.EqualTo("Allied since week 6"), "The finalist cards date it the same way.");

            s.events.Clear();
            read = AllianceRead.Yours(s).Single();
            Assert.That(read.formedWeek, Is.Zero, "With the line gone the record cannot say - and never says week 2, which nobody told the player.");
            Assert.That(read.formed, Is.EqualTo(AllianceRead.BroughtIn));

            // A pair an invitation made: the invitation is how it began, and is not listed again as a deal.
            var invited = Pact(s, "invited", true, s.playerId, n[2].id);
            s.ledger.alliances.Add(new AllianceRow { id = invited.id, why = "player", startedWeek = 5 });
            s.deals.Add(new DealState { id = "ask", type = DealKind.AllianceInvite, proposerId = n[2].id, recipientId = s.playerId,
                status = DealStatus.Active, week = 5 });
            var pair = AllianceRead.Yours(s).Single(p => p.id == invited.id);
            Assert.That(pair.formedWeek, Is.EqualTo(5));
            Assert.That(pair.formed, Is.EqualTo(First(n[2]) + " invited you."));
            Assert.That(pair.deals, Is.Empty);
            s.deals.Single().proposerId = s.playerId;
            s.deals.Single().recipientId = n[2].id;
            Assert.That(AllianceRead.Yours(s).Single(p => p.id == invited.id).formed, Is.EqualTo("You invited " + First(n[2]) + "."));
        }

        /// <summary>
        /// A story's pact is said the way the story said it: "Riley and Jo let you in" is not a pact
        /// the player formed. It is dated from its beginning - the player was in it from the start -
        /// and the finalist cards date it the same way; a story's pact the player was let into later
        /// (the player last, as an invitation adds them) is not theirs since then.
        /// </summary>
        [Test]
        public void AStorysPactReadsAsTheStoryToldItAndIsDatedAsTheCardsDateIt()
        {
            var s = House();
            var n = Others(s);
            var pact = Pact(s, "alliance-story-9", true, s.playerId, n[0].id, n[1].id);
            s.ledger.alliances.Add(new AllianceRow { id = pact.id, why = "story", startedWeek = 3 });
            var cast = new List<StoryRoleState>
            {
                new StoryRoleState { role = "A", contestantId = n[0].id }, new StoryRoleState { role = "B", contestantId = n[1].id },
            };
            Assert.That(AllianceRead.Yours(s).Single().formed, Is.EqualTo("It came together in a story, with " + First(n[0]) + " and " + First(n[1]) + "."),
                "Without the beat on the record, nobody is said to have formed it.");
            s.storylines.Add(new StorylineState
            {
                id = "cycle-9", templateId = "behind-closed-doors", week = 3, cast = cast,
                path = new List<StoryStepState> { new StoryStepState { beatId = "you-know", optionId = "join", result = StoryResults.Success, week = 3 } },
            });
            var option = StoryCatalog.Find("behind-closed-doors").Beat("you-know").Option("join");
            string told = StoryText.Fill(s, option.outcome, cast);
            Assert.That(told, Does.Contain("let you in"), "The catalog's own words for the beat.");
            var read = AllianceRead.Yours(s).Single();
            Assert.That(read.formed, Is.EqualTo(told));
            Assert.That(read.formedWeek, Is.EqualTo(3));
            Assert.That(FinalistRead.RelationshipLine(s, n[0].id), Is.EqualTo("Allied since week 3"), "The finalist cards date it the same way.");

            var later = Pact(s, "alliance-story-10", true, n[2].id, n[3].id, s.playerId);
            s.ledger.alliances.Add(new AllianceRow { id = later.id, why = "story", startedWeek = 2 });
            Assert.That(AllianceRead.Yours(s).Single(p => p.id == later.id).formedWeek, Is.Zero);
            Assert.That(FinalistRead.RelationshipLine(s, n[2].id), Is.Null, "Not theirs since week 2, on the page or the card.");
        }

        [Test]
        public void TheReadingIsThePlayersOwnScoreInTheWebsBands()
        {
            var s = House();
            var other = Others(s)[0];
            foreach (var (score, word) in new[]
                     {
                         (60.0, AllianceRead.Friendly), (15.0, AllianceRead.Friendly), (14.9, AllianceRead.Neutral), (-14.9, AllianceRead.Neutral),
                         (-15.0, AllianceRead.Wary), (-39.9, AllianceRead.Wary), (-40.0, AllianceRead.Hostile),
                     })
            {
                SetScore(s, s.playerId, other.id, score);
                Assert.That(AllianceRead.ReadingWord(s, other.id), Is.EqualTo(word), score.ToString());
                Assert.That(AllianceRead.ReadingWord(s, other.id), Is.EqualTo(FinalistRead.StandingWord(s, other.id)),
                    "The finalist cards' words, which are the web's.");
            }
            Pact(s, "ours", true, s.playerId, other.id);
            Assert.That(FinalistRead.StandingWord(s, other.id), Is.EqualTo("Allied"));
            Assert.That(AllianceRead.ReadingWord(s, other.id), Is.EqualTo(AllianceRead.Hostile), "Inside a pact the reading still reads.");
        }
    }
}
