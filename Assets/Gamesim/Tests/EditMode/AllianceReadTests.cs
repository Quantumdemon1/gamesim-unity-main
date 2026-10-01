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
    /// through the player's own reading and the calls on the record. Unity-free, so the dotnet subset
    /// runs it (Tools/SimulationTests).
    /// </summary>
    public sealed class AllianceReadTests
    {
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

        [Test]
        public void EvidenceAboutOnePactNeverRevealsAnother()
        {
            var s = House();
            var n = Others(s);
            var pair = Pact(s, "pair", true, n[0].id, n[1].id);
            var pairFact = PrivateFact(s, pair);
            var bigger = Pact(s, "bigger", true, n[0].id, n[1].id, n[2].id);
            var biggerFact = PrivateFact(s, bigger);
            // The house's rumour reaches the player about the pair: a knower now, and the whisper names its first two.
            Knowledge.AddKnower(s, pairFact, s.playerId);
            Knowledge.MakeKnown(s, pairFact, FactVisibility.Whispered);
            Line(s, 4, StoryLog.Whisper, AllianceRead.WhisperLine(s, pairFact));
            Assert.That(AllianceRead.WhisperLine(s, biggerFact), Is.EqualTo(AllianceRead.WhisperLine(s, pairFact)),
                "The same two names open both pacts' whispers.");

            var card = AllianceRead.Suspected(s).Single();
            Assert.That(card.memberIds, Is.EqualTo(new[] { n[0].id, n[1].id }),
                "The pact the player heard of, and not the bigger one the same two names open.");
            Assert.That(Dated(card), Is.EqualTo(new[] { "4: " + AllianceRead.WhisperLine(s, pairFact) }));
            Assert.That(AllianceRead.SuspectedPairs(s), Is.EqualTo(new[] { (n[0].id, n[1].id) }));

            // Two pacts of the same people are one card, and it says nothing of there being two.
            string before = JsonConvert.SerializeObject(AllianceRead.Suspected(s));
            var again = Pact(s, "again", false, n[1].id, n[0].id);
            Knowledge.AddKnower(s, PrivateFact(s, again), s.playerId);
            Assert.That(JsonConvert.SerializeObject(AllianceRead.Suspected(s)), Is.EqualTo(before));
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
            var engineWhisper = typeof(EpisodeEngine).GetMethod("Whisper", BindingFlags.NonPublic | BindingFlags.Static, null,
                new[] { typeof(EpisodeState), typeof(HouseFactState) }, null);
            Assert.That(engineWhisper, Is.Not.Null, "The engine's whisper, which the page matches.");
            string whisper = (string)engineWhisper.Invoke(null, new object[] { s, heardFact });
            Assert.That(whisper, Is.EqualTo(AllianceRead.WhisperLine(s, heardFact)), "The page matches the engine's whisper, word for word.");
            Line(s, 5, StoryLog.Whisper, whisper);
            var second = AllianceRead.Suspected(s).Single(c => c.memberIds.Contains(n[2].id));
            Assert.That(Dated(second), Is.EqualTo(new[] { "5: " + whisper }));
            Assert.That(second.certainty, Is.EqualTo(FinalistRead.Suspected));

            // Out in the open: confirmed, and said.
            Knowledge.MakeKnown(s, heardFact, FactVisibility.Public);
            second = AllianceRead.Suspected(s).Single(c => c.memberIds.Contains(n[2].id));
            Assert.That(second.certainty, Is.EqualTo(FinalistRead.Confirmed));
            Assert.That(Dated(second), Is.EqualTo(new[] { "5: " + whisper, "0: " + AllianceRead.OutInTheOpen }));

            // A pact the player knows of whose line the log has let go still shows, undated.
            var older = Pact(s, "older", true, n[4].id, n[5].id);
            Knowledge.AddKnower(s, PrivateFact(s, older), s.playerId);
            var third = AllianceRead.Suspected(s).Single(c => c.memberIds.Contains(n[4].id));
            Assert.That(Dated(third), Is.EqualTo(new[] { "0: " + AllianceRead.HeardOfIt }));
            Assert.That(AllianceRead.Suspected(s).Select(c => c.memberIds[0]), Is.EqualTo(new[] { n[0].id, n[2].id, n[4].id }),
                "The earliest news first, the undated last.");
        }

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
        public void AnEndedPactShowsWhenAndWhy()
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
            Assert.That(read.ended, Is.EqualTo(AllianceRead.FellApart),
                "and why, in the words the player was told: 'turned' is read from their partner's private view.");
            row.why = "player/soured";
            Assert.That(AllianceRead.Yours(s).Single().ended, Is.EqualTo(AllianceRead.FellApart));
            row.why = "player/ended";
            Assert.That(AllianceRead.Yours(s).Single().ended, Is.EqualTo(AllianceRead.JustEnded));
            n[0].status = ContestantStatus.Jury;
            row.why = "player/left-house";
            Assert.That(AllianceRead.Yours(s).Single().ended, Is.EqualTo(First(n[0]) + " left the house."), "A departure is public.");

            // The line the engine told the player wins, word for word (TellThePlayerWhichAlliancesEnded).
            var tell = typeof(EpisodeEngine).GetMethod("TellThePlayerWhichAlliancesEnded", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(tell, Is.Not.Null, "The engine's own word to the player when a pact of theirs ends.");
            n[0].status = ContestantStatus.Active;
            row.why = "player/ended";
            s.week = 4;
            tell.Invoke(null, new object[] { s, new List<AllianceState> { pact } });
            s.week = 6;
            Assert.That(s.events.Last().text, Is.EqualTo("Your alliance with " + n[0].name + " has fallen apart."));
            Assert.That(AllianceRead.Yours(s).Single().ended, Is.EqualTo(AllianceRead.FellApart), "The line the player read that week.");

            var left = Pact(s, "left", false, s.playerId, n[1].id);
            s.ledger.alliances.Add(new AllianceRow { id = left.id, why = "player/left-house", startedWeek = 3, endedWeek = 5 });
            n[1].status = ContestantStatus.Evicted;
            s.week = 5;
            tell.Invoke(null, new object[] { s, new List<AllianceState> { left } });
            s.week = 6;
            Assert.That(AllianceRead.Yours(s).Single(p => p.id == left.id).ended, Is.EqualTo(First(n[1]) + " left the house."));
            Assert.That(AllianceRead.Yours(s).Single(p => p.id == left.id).endedWeek, Is.EqualTo(5));
        }

        [Test]
        public void LeavingAPactThroughTheEngineIsOnThePageWhenAndWhy()
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(31));
            for (int i = 0; i < 900 && !(engine.Snapshot.phase == EpisodePhase.Campaign && engine.Snapshot.nominees.Count == 2); i++)
                Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            var s = engine.Snapshot;
            Assert.That(s.phase, Is.EqualTo(EpisodePhase.Campaign), "The season reached a campaign.");
            s.socialActions = 0; s.outOfPhaseSocialActions = 0;
            var friend = s.Active.First(c => !c.isPlayer && !s.Allied(s.playerId, c.id));
            SetScore(s, friend.id, s.playerId, 30);
            engine = new EpisodeEngine(s);
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
            var theirs = Pact(s, "npc-made", true, n[0].id, n[1].id);
            s.ledger.alliances.Add(new AllianceRow { id = theirs.id, why = "npc", startedWeek = 2 });
            theirs.members.Add(s.playerId);
            Line(s, 4, "alliance", n[0].name + " brought you into " + theirs.name + ".");

            var read = AllianceRead.Yours(s).Single();
            Assert.That(read.formedWeek, Is.EqualTo(4), "The week the player came in, not the week its members made it.");
            Assert.That(read.formed, Is.EqualTo(First(n[0]) + " brought you in."));
            Assert.That(read.members.Select(m => m.id), Is.EqualTo(new[] { n[0].id, n[1].id }));

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

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind, string targetId) =>
            new EpisodeCommand
            {
                id = "alliances-" + kind + "-" + s.revision + "-" + Guid.NewGuid().ToString("N"), actorId = s.playerId, kind = kind,
                targetId = targetId, expectedRevision = s.revision, expectedPhase = s.phase,
            };
    }
}
