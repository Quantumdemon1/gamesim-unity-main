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
    /// Overheard, in the rules (plan 32 §I, slice S2a; story rules 12): the house's witness command naming a conversation is a
    /// free Notice (<c>conv:{sequence}</c>) or Listen (<c>conv:{sequence}:listen</c>). A Notice writes the pair's
    /// <see cref="ClaimSource.Seen"/> standing in the topic's tone and one hunch line; a Listen is the conversation's one,
    /// caught on its keyed coin or hearing today's eavesdrop reveal without a draw; the week's caps are counted from the
    /// ledger, attempts included; the paid listen-in is refused at 12 and unchanged below; a sighted act at 12 is a Notice
    /// too; and <see cref="VoteRead.StandingKnown"/> ignores a hunch. Pure simulation, so the Unity-free subset runs it.
    /// </summary>
    public sealed class OverhearTests
    {
        // ------------------------------------------------------------ fixtures

        /// <summary>
        /// The vote answer's campaign (<see cref="VoteAnswerTests.Campaign"/>) with <paramref name="pairs"/> NPC conversations
        /// started through the engine's own door (the first two houseguests in the kitchen, the next two in the living room),
        /// each then given <paramref name="topic"/> (a topic with no duration bound of its own).
        /// </summary>
        internal static EpisodeState Talking(int rules, string topic, uint seed = 7, int pairs = 1)
        {
            var engine = new EpisodeEngine(VoteAnswerTests.Campaign(rules, seed));
            for (int pair = 0; pair < pairs; pair++)
            {
                var now = engine.Snapshot;
                var npcs = now.Active.Where(c => !c.isPlayer).ToArray();
                var request = new NpcOperationRequest
                {
                    kind = NpcOperationKind.Start, sessionId = now.sessionId, expectedRevision = now.revision, expectedPhase = now.phase,
                    expectedClockTick = now.npcSocial.clockTick, targetClockTick = now.npcSocial.clockTick, freeRoamReady = true,
                    sequence = now.npcSocial.nextConversationSequence, firstId = npcs[pair * 2].id, secondId = npcs[pair * 2 + 1].id,
                    rendezvousId = pair == 0 ? "kitchen-west-chat" : "living-east-chat",
                };
                request.worldReady.Add(new NpcWorldReadyEvidence { sequence = request.sequence, firstId = request.firstId, secondId = request.secondId,
                    rendezvousId = request.rendezvousId, observedClockTick = request.expectedClockTick });
                var result = engine.PrepareNpcOperation(request);
                Assert.That(result.accepted, Is.True, result.reason);
                engine = new EpisodeEngine(result.candidate);
            }
            var s = engine.Snapshot;
            foreach (var row in s.npcSocial.pending)
            {
                row.topic = topic;
                foreach (var memory in s.npcSocial.pairMemory.Where(m => (m.fromId == row.firstId && m.toId == row.secondId) || (m.fromId == row.secondId && m.toId == row.firstId)))
                    memory.lastTopic = topic;
            }
            Assert.That(s.npcSocial.pending.Count, Is.EqualTo(pairs));
            Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);
            return s;
        }

        internal static EpisodeCommand Overhear(EpisodeState s, NpcConversationState c, bool listen) => new EpisodeCommand
        {
            id = "overhear-" + s.revision + "-" + c.sequence + (listen ? "-listen" : ""), actorId = s.playerId, kind = EpisodeCommandKind.WitnessNpcAct,
            targetId = c.firstId, secondTargetId = c.secondId, text = EpisodeEngine.ConversationText(c.sequence, listen),
            expectedRevision = s.revision, expectedPhase = s.phase,
        };

        private static string Json(object value) => JsonConvert.SerializeObject(value);

        private static string Refused(EpisodeState s, EpisodeCommand command)
        {
            string before = Json(s);
            var result = new EpisodeEngine(s).Apply(command);
            Assert.That(result.accepted, Is.False, command.text);
            Assert.That(Json(result.state), Is.EqualTo(before), "Refused before anything moved: " + result.reason);
            return result.reason;
        }

        private static StandingRow Row(EpisodeState s, string from, string to, string source, double score) =>
            new StandingRow { week = s.week, fromId = from, toId = to, source = source, score = score };

        private static List<string> Npcs(EpisodeState s) => s.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();

        // ------------------------------------------------------------ the Notice

        [TestCase("casual", HunchTone.Warm, "talking warmly")]
        [TestCase("strategy", HunchTone.Scheming, "with their heads together")]
        [TestCase("tension", HunchTone.Tense, "snapping at each other")]
        public void ANoticeIsTheTopicsToneOnTheRecordAndOneLineAndIsFree(string topic, int tone, string words)
        {
            var s = Talking(StoryRules.Manners, topic);
            var c = s.npcSocial.pending.Single();
            var after = VoteAnswerTests.Accepted(s, Overhear(s, c, false));
            var row = after.ledger.standings.Single(r => r.source == ClaimSource.Seen);
            Assert.That((row.fromId, row.toId, row.score, row.week), Is.EqualTo((c.firstId, c.secondId, (double)tone, s.week)));
            var line = after.events.Last();
            Assert.That(line.kind, Is.EqualTo(EpisodeEngine.HunchEventKind));
            Assert.That(line.text, Is.EqualTo("You noticed " + s.Find(c.firstId).name + " and " + s.Find(c.secondId).name + " " + words + " in the kitchen."));
            Assert.That(line.audienceIds, Is.EqualTo(new[] { s.playerId }), "The player's alone.");
            Assert.That(after.events.Count(e => e.sequence >= s.nextSequence), Is.EqualTo(1), "One line.");
            Assert.That(after.randomState, Is.EqualTo(s.randomState), "No draw from the season's stream,");
            Assert.That(after.npcSocial.randomState, Is.EqualTo(s.npcSocial.randomState), "nor from the NPC world's,");
            Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(EpisodeEngine.SocialActionsSpent(s)), "and no action spent.");
            Assert.That(Json(after.relationships), Is.EqualTo(Json(s.relationships)), "Nobody's view moves.");
            Assert.That(Json(after.story.cooldowns), Is.EqualTo(Json(s.story.cooldowns)), "A Notice keeps no key.");
            Assert.That(EpisodeEngine.NoticedThisWeek(after), Is.EqualTo(1));
            Assert.That(Refused(after, Overhear(after, c, false)), Is.EqualTo("You already noticed them."), "Once a pair and tone a week.");
        }

        [Test]
        public void ANoticeReadsOnlyTheTopicNeverHowTheyStand()
        {
            string Noticed(double score)
            {
                var s = Talking(StoryRules.Manners, "tension");
                var c = s.npcSocial.pending.Single();
                VoteAnswerTests.SetScore(s, c.firstId, c.secondId, score);
                VoteAnswerTests.SetScore(s, c.secondId, c.firstId, score);
                var after = VoteAnswerTests.Accepted(s, Overhear(s, c, false));
                return Json(after.ledger.standings) + Json(after.events.Last().text);
            }
            Assert.That(Noticed(90), Is.EqualTo(Noticed(-90)), "Two close friends seen on a tense topic read tense: a hunch is honest but noisy.");
        }

        // ------------------------------------------------------------ refusals

        [Test]
        public void EachRefusalIsItsOwnAndChangesNothing()
        {
            var s = Talking(StoryRules.Manners, "casual");
            var c = s.npcSocial.pending.Single();
            var third = Npcs(s).First(id => id != c.firstId && id != c.secondId);
            var stale = Overhear(s, c, false); stale.text = EpisodeEngine.ConversationText(c.sequence + 7, false);
            Assert.That(Refused(s, stale), Is.EqualTo("That moment has passed."), "A stale sequence.");
            var garbled = Overhear(s, c, false); garbled.text = "conv:x";
            Assert.That(Refused(s, garbled), Is.EqualTo("That moment has passed."), "Not a conversation's number.");
            var wrongPair = Overhear(s, c, false); wrongPair.secondTargetId = third;
            Assert.That(Refused(s, wrongPair), Is.EqualTo("That moment has passed."), "Not who was there.");
            var player = Overhear(s, c, true); player.targetId = s.playerId;
            Assert.That(Refused(s, player), Is.EqualTo("You were part of that."), "The player's own pair.");
            var swapped = Overhear(s, c, false); (swapped.targetId, swapped.secondTargetId) = (c.secondId, c.firstId);
            Assert.That(new EpisodeEngine(s).Apply(swapped).accepted, Is.True, "Either way round is the same two.");

            var closed = s.Clone(); closed.phase = EpisodePhase.Eviction;
            Assert.That(EpisodeEngine.OverhearRefusal(closed, EpisodeEngine.ConversationText(c.sequence, false), c.firstId, c.secondId),
                Is.EqualTo("Nobody is talking where you can hear right now."), "A closed phase.");
            var gone = s.Clone(); gone.Find(gone.playerId).status = ContestantStatus.Evicted;
            Assert.That(EpisodeEngine.OverhearRefusal(gone, EpisodeEngine.ConversationText(c.sequence, true), c.firstId, c.secondId),
                Is.EqualTo("Evicted players can follow the season but cannot influence it."));
            var below = s.Clone(); below.story.rulesVersion = StoryRules.Roles;
            Assert.That(EpisodeEngine.OverhearRefusal(below, EpisodeEngine.ConversationText(c.sequence, true), c.firstId, c.secondId),
                Is.EqualTo(EpisodeEngine.WaveDKindRefusal), "Below 12 there is no overhearing.");
        }

        [Test]
        public void BelowTwelveTheTextIsRefusedAsItAlwaysWas()
        {
            var s = Talking(StoryRules.Roles, "casual");
            var c = s.npcSocial.pending.Single();
            foreach (bool listen in new[] { false, true })
            {
                string reason = Refused(s, Overhear(s, c, listen));
                Assert.That(reason, Is.EqualTo(EpisodeEngine.WitnessRefusal(s, EpisodeEngine.ConversationText(c.sequence, listen), c.firstId, c.secondId)),
                    "The witness's own refusal, as at 11 before this slice.");
            }
        }

        [Test]
        public void TheWeeksCapsAreCountedFromTheLedgerAttemptsIncluded()
        {
            var s = Talking(StoryRules.Manners, "strategy");
            var c = s.npcSocial.pending.Single();
            var npcs = Npcs(s);
            var others = npcs.Where(id => id != c.firstId && id != c.secondId).ToList();

            var full = s.Clone();
            for (int k = 0; k < EpisodeEngine.NoticesAWeek; k++)
                full.ledger.standings.Add(Row(full, others[k % others.Count], others[(k + 1) % others.Count], ClaimSource.Seen, 1 + k % 3));
            Assert.That(Refused(full, Overhear(full, c, false)), Is.EqualTo("You have noticed enough for this week."));
            var lastWeek = s.Clone();
            foreach (var row in full.ledger.standings.Where(r => r.source == ClaimSource.Seen)) { var old = row.Clone(); old.week = s.week - 1; lastWeek.ledger.standings.Add(old); }
            Assert.That(EpisodeEngine.NoticedThisWeek(lastWeek), Is.Zero, "Last week's do not count.");

            // A caught Listen's attempt counts as a Listen; a read's miss and an airing's standing, which point at the player, do not.
            var listened = s.Clone();
            listened.ledger.standings.Add(Row(listened, others[0], others[1], ClaimSource.Missed, 0));
            listened.ledger.standings.Add(Row(listened, others[1], listened.playerId, ClaimSource.Missed, 0));
            listened.ledger.standings.Add(Row(listened, others[2], listened.playerId, ClaimSource.Overheard, 10));
            Assert.That(EpisodeEngine.ListenedThisWeek(listened), Is.EqualTo(1));
            Assert.That(new EpisodeEngine(listened).Apply(Overhear(listened, c, true)).accepted, Is.True, "One Listen left.");
            listened.ledger.standings.Add(Row(listened, others[1], others[2], ClaimSource.Overheard, 10));
            Assert.That(Refused(listened, Overhear(listened, c, true)), Is.EqualTo("You have listened in enough this week."));
        }

        // ------------------------------------------------------------ the Listen

        [Test]
        public void AListenIsTheConversationsOneCaughtOnItsKeyedCoinOrHeardWithoutADraw()
        {
            int caught = 0, heard = 0;
            for (uint seed = 1; seed <= 120 && (caught < 3 || heard < 3); seed++)
            {
                var s = Talking(StoryRules.Manners, "strategy", seed);
                var c = s.npcSocial.pending.Single();
                ContestantState first = s.Find(c.firstId), second = s.Find(c.secondId);
                string key = EpisodeEngine.OverhearKey(c.sequence);
                bool expectCaught = StoryRandom.Chance(s, key, EpisodeEngine.CaughtChance(s, first, second));
                double between = s.Score(first.id, second.id);
                var after = VoteAnswerTests.Accepted(s, Overhear(s, c, true));
                Assert.That(after.randomState, Is.EqualTo(s.randomState), "seed " + seed + ": no draw from the season's stream.");
                Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(EpisodeEngine.SocialActionsSpent(s)), "Free.");
                Assert.That(after.story.cooldowns.Single(k => k.key == EpisodeEngine.HeardPrefix + c.sequence).untilWeek, Is.EqualTo(s.week + 1));
                Assert.That(EpisodeEngine.ListenedThisWeek(after), Is.EqualTo(1));
                var line = after.events.Last();
                Assert.That(line.kind, Is.EqualTo("eavesdrop"), "Logged as today's eavesdrop.");
                if (expectCaught)
                {
                    caught++;
                    var catcher = StoryRandom.Index(s, key + ":who", 2) == 0 ? first : second;
                    var row = after.ledger.standings.Single(r => r.week == s.week && (r.source == ClaimSource.Missed || r.source == ClaimSource.Overheard)
                        && r.toId != s.playerId);
                    Assert.That((row.fromId, row.toId, row.source, row.score), Is.EqualTo((first.id, second.id, ClaimSource.Missed, 0.0)), "The attempt, between the two.");
                    Assert.That(line.text, Is.EqualTo(catcher.name + " caught you listening in. That will cost you."));
                    Assert.That(line.audienceIds, Is.EqualTo(new[] { s.playerId, catcher.id }));
                    Assert.That(after.relationships.Count(r => r.events.Any(e => e.type == "eavesdrop")), Is.EqualTo(s.relationships.Count(r => r.events.Any(e => e.type == "eavesdrop")) + 2),
                        "One keyed change between the player and the catcher, both ways.");
                    Assert.That(after.memories.Last().ownerId, Is.EqualTo(catcher.id));
                    Assert.That(EpisodeEngine.ReadThisWeek(after, catcher.id), Is.False, "The catcher can still be read.");
                    Assert.That(after.ledger.claims.Count, Is.EqualTo(s.ledger.claims.Count), "Nothing heard.");
                }
                else
                {
                    heard++;
                    var row = after.ledger.standings.Single(r => r.week == s.week && r.source == ClaimSource.Overheard && r.toId != s.playerId);
                    Assert.That((row.fromId, row.toId, row.score), Is.EqualTo((first.id, second.id, between)), "The pair's true standing, as today's eavesdrop writes it.");
                    Assert.That(line.text, Does.StartWith("You overheard " + first.name + " and " + second.name + ". They "));
                    Assert.That(line.audienceIds, Is.EqualTo(new[] { s.playerId }));
                    Assert.That(after.ledger.standings.Any(r => r.source == ClaimSource.Missed), Is.False);
                }
                // The coin always lands the same way for one conversation: a second Listen on it is refused, caught or not.
                string again = Refused(after, Overhear(after, c, true));
                Assert.That(again, Is.EqualTo("You already heard them."));
            }
            Assert.That(caught, Is.GreaterThan(0), "Some Listens are caught,");
            Assert.That(heard, Is.GreaterThan(0), "and some heard.");
        }

        [Test]
        public void AThirdListenAfterOneCaughtAndOneHeardIsRefused()
        {
            for (uint seed = 1; seed <= 300; seed++)
            {
                var s = Talking(StoryRules.Manners, "strategy", seed, pairs: 2);
                var outcomes = s.npcSocial.pending.Select(c => StoryRandom.Chance(s, EpisodeEngine.OverhearKey(c.sequence),
                    EpisodeEngine.CaughtChance(s, s.Find(c.firstId), s.Find(c.secondId)))).ToList();
                if (outcomes[0] == outcomes[1]) continue;
                var after = s;
                foreach (var c in s.npcSocial.pending) after = VoteAnswerTests.Accepted(after, Overhear(after, c, true));
                Assert.That(after.ledger.standings.Count(r => r.source == ClaimSource.Missed && r.toId != after.playerId), Is.EqualTo(1));
                Assert.That(after.ledger.standings.Count(r => r.source == ClaimSource.Overheard && r.toId != after.playerId), Is.EqualTo(1));
                Assert.That(EpisodeEngine.ListenedThisWeek(after), Is.EqualTo(2));
                // A third conversation's Listen, as the check sees it: the same pair with its key gone.
                var third = after.Clone();
                third.story.cooldowns.RemoveAll(k => k.key.StartsWith(EpisodeEngine.HeardPrefix, StringComparison.Ordinal));
                var first = third.npcSocial.pending[0];
                Assert.That(EpisodeEngine.OverhearRefusal(third, EpisodeEngine.ConversationText(first.sequence, true), first.firstId, first.secondId),
                    Is.EqualTo("You have listened in enough this week."));
                Assert.That(EpisodeEngine.OverhearRefusal(third, EpisodeEngine.ConversationText(first.sequence, false), first.firstId, first.secondId),
                    Is.Null, "A Notice is still free.");
                return;
            }
            Assert.Fail("No seed gave one caught Listen and one heard.");
        }

        [Test]
        public void TheCatchIsOneOfThePairOnTheConversationsOwnKey()
        {
            var s = Talking(StoryRules.Manners, "strategy");
            var c = s.npcSocial.pending.Single();
            ContestantState first = s.Find(c.firstId), second = s.Find(c.secondId);
            foreach (var x in new[] { first, second }) x.traits = new List<string>();
            s.Find(s.playerId).traits = new List<string>();
            Assert.That(EpisodeEngine.CaughtChance(s, first, second), Is.EqualTo(0.15).Within(1e-9));
            first.traits = new List<string> { "Analytical" };
            Assert.That(EpisodeEngine.CaughtChance(s, first, second), Is.EqualTo(0.25).Within(1e-9));
            s.Find(s.playerId).traits = new List<string> { "Sneaky" };
            Assert.That(EpisodeEngine.CaughtChance(s, first, second), Is.EqualTo(0.20).Within(1e-9));
            first.traits = new List<string>();
            Assert.That(EpisodeEngine.CaughtChance(s, first, second), Is.EqualTo(0.10).Within(1e-9));
            int caught = 0, n = 0;
            for (uint seed = 1; seed <= 4000; seed++)
            {
                var x = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
                if (StoryRandom.Chance(x, EpisodeEngine.OverhearKey(seed), 0.20)) caught++;
                n++;
            }
            Assert.That(caught / (double)n, Is.EqualTo(0.20).Within(0.03), "The coin lands at its chance.");
        }

        // ------------------------------------------------------------ one pact sentence a week

        private static readonly MethodInfo OverhearOf = typeof(EpisodeEngine).GetMethod("Overhear", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("EpisodeEngine.Overhear is the Notice's and the Listen's commit.");

        [Test]
        public void ThePactSentenceIsHeardOnceAWeekAndAddsNoKnowerAfter()
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 4101);
            s.week = 6;
            s.phase = EpisodePhase.Social;
            EpisodeEngine.EnableStory(s, s.week);
            EpisodeEngine.EnableCommitments(s, s.week);
            EpisodeEngine.EnableAllianceLeaks(s, s.week);
            s.story.rulesVersion = StoryRules.Manners;
            Assert.That(AllianceLeaks.On(s), Is.True);
            var n = s.contestants.Where(c => !c.isPlayer).ToList();
            var pacts = new List<AllianceState>();
            for (int p = 0; p < 3; p++)
            {
                var pact = new AllianceState { id = "pair-" + p, name = "The Pair " + p + " Pact", active = true, members = new List<string> { n[2 * p].id, n[2 * p + 1].id } };
                s.alliances.Add(pact);
                Knowledge.AllianceFormed(s, pact);
                pacts.Add(pact);
            }
            long next = 1;
            NpcConversationState Uncaught(int p)
            {
                ContestantState a = n[2 * p], b = n[2 * p + 1];
                while (StoryRandom.Chance(s, EpisodeEngine.OverhearKey(next), EpisodeEngine.CaughtChance(s, a, b))) next++;
                var row = new NpcConversationState { sequence = next++, firstId = a.id, secondId = b.id, topic = "strategy", rendezvousId = "kitchen-west-chat", week = s.week, phase = s.phase };
                s.npcSocial.pending.Clear();
                s.npcSocial.pending.Add(row);
                s.npcSocial.nextConversationSequence = next;
                return row;
            }
            string Listen(int p)
            {
                var c = Uncaught(p);
                var command = new EpisodeCommand { id = "listen-" + c.sequence, actorId = s.playerId, kind = EpisodeCommandKind.WitnessNpcAct,
                    targetId = c.firstId, secondTargetId = c.secondId, text = EpisodeEngine.ConversationText(c.sequence, true) };
                Assert.That(EpisodeEngine.OverhearRefusal(s, command.text, c.firstId, c.secondId), Is.Null);
                OverhearOf.Invoke(null, new object[] { s, command });
                return s.events.Last(e => e.kind == "eavesdrop").text;
            }
            bool Knows(int p) => Knowledge.Knows(Knowledge.Of(s, FactKinds.Alliance, pacts[p].id), s.playerId);

            Assert.That(Listen(0), Does.EndWith(AllianceLeaks.ListenInSentence(s, pacts[0])), "The week's first pact sentence.");
            Assert.That(Knows(0), Is.True);
            Assert.That(s.story.cooldowns.Any(k => k.key == EpisodeEngine.PactHeardPrefix + s.week), Is.True);
            Assert.That(Listen(1), Does.Not.Contain(AllianceLeaks.ListenInSentence(s, pacts[1])), "A second pact the same week gives no sentence,");
            Assert.That(Knows(1), Is.False, "and no knower.");
            s.week = 7;
            Assert.That(Listen(2), Does.EndWith(AllianceLeaks.ListenInSentence(s, pacts[2])), "The next week gives one again.");
            Assert.That(Knows(2), Is.True);
        }

        // ------------------------------------------------------------ the paid listen-in, sightings, the readers

        [Test]
        public void ThePaidListenInIsRefusedAtTwelveAndUnchangedAtEleven()
        {
            EpisodeCommand Paid(EpisodeState s) => new EpisodeCommand { id = "paid-" + s.revision, actorId = s.playerId, kind = EpisodeCommandKind.Eavesdrop,
                expectedRevision = s.revision, expectedPhase = s.phase };
            var at12 = Talking(StoryRules.Manners, "strategy");
            Assert.That(Refused(at12, Paid(at12)), Is.EqualTo(EpisodeEngine.WalkOverToListenRefusal));
            var at11 = Talking(StoryRules.Roles, "strategy");
            var eleven = new EpisodeEngine(at11).Apply(Paid(at11));
            Assert.That(eleven.reason, Is.Not.EqualTo(EpisodeEngine.WalkOverToListenRefusal), "Below 12 the paid listen-in is what it always was.");
        }

        [TestCase(NpcActKinds.Talk, HunchTone.Warm)]
        [TestCase(NpcActKinds.Court, HunchTone.Scheming)]
        [TestCase(NpcActKinds.Confront, HunchTone.Tense)]
        public void ASightingAtTwelveIsANoticeOfTheTwoAndNothingBelow(string kind, int tone)
        {
            foreach (int rules in new[] { StoryRules.Blame, StoryRules.Manners })
            {
                var s = WitnessNpcActTests.Open(EpisodePhase.Campaign);
                s.story.rulesVersion = rules;
                var act = WitnessNpcActTests.Staged(s, kind);
                var after = VoteAnswerTests.Accepted(s, new EpisodeCommand
                {
                    id = "witness-" + s.revision + "-" + act.id, actorId = s.playerId, kind = EpisodeCommandKind.WitnessNpcAct,
                    targetId = act.actorId, secondTargetId = act.partnerId, text = act.id, expectedRevision = s.revision, expectedPhase = s.phase,
                });
                var seen = after.ledger.standings.Where(r => r.source == ClaimSource.Seen).ToList();
                if (rules < StoryRules.Manners) { Assert.That(seen, Is.Empty, "Below 12 a sighting is only a sighting."); continue; }
                Assert.That(seen.Select(r => (r.fromId, r.toId, r.score)), Is.EqualTo(new[] { (act.actorId, act.partnerId, (double)tone) }));
                Assert.That(after.events.Any(e => e.kind == EpisodeEngine.HunchEventKind), Is.False, "The sighting's own line says it.");
            }
        }

        [Test]
        public void ASightingWithTheWeeksNoticesSpentStillLandsAndWritesNoHunch()
        {
            var s = WitnessNpcActTests.Open(EpisodePhase.Campaign);
            s.story.rulesVersion = StoryRules.Manners;
            var act = WitnessNpcActTests.Staged(s, NpcActKinds.Talk);
            var others = Npcs(s).Where(id => id != act.actorId && id != act.partnerId).ToList();
            for (int k = 0; k < EpisodeEngine.NoticesAWeek; k++)
                s.ledger.standings.Add(Row(s, others[k % others.Count], others[(k + 1) % others.Count], ClaimSource.Seen, 1 + k % 3));
            var after = VoteAnswerTests.Accepted(s, new EpisodeCommand
            {
                id = "witness-full-" + act.id, actorId = s.playerId, kind = EpisodeCommandKind.WitnessNpcAct,
                targetId = act.actorId, secondTargetId = act.partnerId, text = act.id, expectedRevision = s.revision, expectedPhase = s.phase,
            });
            Assert.That(after.npcSocial.acts.Single(a => a.id == act.id).sighted, Is.True);
            Assert.That(EpisodeEngine.NoticedThisWeek(after), Is.EqualTo(EpisodeEngine.NoticesAWeek), "The cap is shared.");
        }

        [Test]
        public void StandingKnownIgnoresAHunch()
        {
            var s = VoteAnswerTests.Campaign(StoryRules.Manners);
            var npcs = Npcs(s);
            s.ledger.standings.Add(Row(s, npcs[0], npcs[1], ClaimSource.Seen, HunchTone.Warm));
            Assert.That(VoteRead.StandingKnown(s, npcs[0], npcs[1]), Is.False, "A hunch is not a standing the player holds.");
            s.ledger.standings.Add(Row(s, npcs[0], npcs[1], ClaimSource.Overheard, 30));
            Assert.That(VoteRead.StandingKnown(s, npcs[0], npcs[1]), Is.True);
        }

        [Test]
        public void HunchesAreNeverFilteredByThePacts()
        {
            var s = Talking(StoryRules.Manners, "strategy");
            var c = s.npcSocial.pending.Single();
            Assert.That(s.Allied(c.firstId, c.secondId), Is.False, "Precondition: no pact of the two.");
            var after = VoteAnswerTests.Accepted(s, Overhear(s, c, false));
            Assert.That(after.ledger.standings.Single(r => r.source == ClaimSource.Seen).score, Is.EqualTo((double)HunchTone.Scheming),
                "A scheming hunch about two with no pact: what the player saw, not what is.");
        }
    }
}
