using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Free talk's week-event talks (slice S5's groundwork, built switched off at talk rules 5): the event gate read from the
    /// week's committed state (<see cref="TalkEvents"/>), a row's own talk level (<see cref="TalkRow.FromRules"/>), the 42 event
    /// talks of the three class pools, their priority of 100, and that every level below 5 is offered exactly main's talk.
    /// Simulation only, so the Unity-free subset runs it too.
    /// </summary>
    public sealed class FreeTalkEventTests
    {
        static readonly string[] ClassPools = { TalkPools.Spark, TalkPools.Steady, TalkPools.Guarded };

        static IEnumerable<TalkRow> EventRows => TalkTables.All.Where(r => r.Kind == TalkKind.Event);

        /// <summary>The events that are about the player, so they hold with every housemate (couple-noticed: but the partner).</summary>
        static bool HoldsForAnyone(string kind) =>
            kind == TalkEvents.PlayerHoH || kind == TalkEvents.PlayerNominated || kind == TalkEvents.CoupleNoticed;

        static IEnumerable<string> Lines(TalkRow row)
        {
            yield return row.Open;
            foreach (var r in row.Replies) { yield return r.Line; yield return r.Lands; yield return r.Even; yield return r.Grates; }
        }

        // ------------------------------------------------------------------ the house, set directly

        /// <summary>A fresh season of eight at talk rules 5, moved to week three: the housemate who talks, a bystander, and the rest.</summary>
        static EpisodeState House(out string npc, out string bystander, out List<string> rest)
        {
            var s = FreeTalkFixture.Fresh(TalkRules.ComesToTalk);
            s.week = 3;
            var npcs = FreeTalkFixture.Npcs(s);
            npc = npcs[0].id;
            bystander = npcs[1].id;
            rest = npcs.Skip(2).Select(c => c.id).ToList();
            return s;
        }

        /// <summary>Writes an event into the week's state the way the engine leaves it.</summary>
        static void Happen(EpisodeState s, string kind, string npc, List<string> rest)
        {
            switch (kind)
            {
                case TalkEvents.PlayerHoH: s.hohId = s.playerId; break;
                case TalkEvents.NpcHoH: s.hohId = npc; break;
                case TalkEvents.PlayerNominated: s.nominees = new List<string> { s.playerId, rest[0] }; break;
                case TalkEvents.NpcNominated: s.nominees = new List<string> { npc, rest[0] }; break;
                case TalkEvents.NpcVeto: s.vetoHolderId = npc; break;
                case TalkEvents.AllyEvicted:
                    // The vote is in: one nominee has gone to the jury, and the two's pact ended with the departure.
                    s.nominees = new List<string> { rest[0], rest[1] };
                    s.Find(rest[0]).status = ContestantStatus.Jury;
                    s.evictionResolved = true;
                    s.alliances.Add(new AllianceState { id = "test-pact", name = "Test pact", members = new List<string> { npc, rest[0] }, active = false });
                    break;
                case TalkEvents.CoupleNoticed:
                    // The player's couple, its showmance bond and its fact, gone public.
                    FreeTalkFixture.ContactOf(s, rest[0]).romance = TalkRules.Couple;
                    s.story.bonds.Add(new BondState { id = "test-bond", kind = BondKinds.Showmance, aId = s.playerId, bId = rest[0], status = BondStatus.Public, sinceWeek = s.week });
                    s.story.facts.Add(new HouseFactState
                    {
                        id = "test-fact", kind = FactKinds.Couple, actorId = s.playerId, subjectId = rest[0], refId = "test-bond",
                        visibility = FactVisibility.Public, week = s.week,
                    });
                    break;
                default:
                    Assert.Fail("Unknown event " + kind);
                    break;
            }
        }

        /// <summary>Opens a window of the week (any window when null).</summary>
        static void OpenWindow(EpisodeState s, string window)
        {
            int want;
            switch (window)
            {
                case TalkWindows.AfterNoms: s.phase = EpisodePhase.VetoSelection; want = Windows.AfterNominations; break;
                case TalkWindows.AfterVeto: s.phase = EpisodePhase.Campaign; want = Windows.AfterVeto; break;
                case TalkWindows.AfterEviction: s.phase = EpisodePhase.Social; want = Windows.AfterEviction; break;
                default: s.phase = EpisodePhase.Nomination; want = Windows.AfterHoH; break;
            }
            Assert.That(EpisodeEngine.Window(s), Is.EqualTo(want), "The control: the window is open.");
            Assert.That(EpisodeEngine.IsFirstNight(s), Is.False, "The control: not move-in night.");
        }

        // ------------------------------------------------------------------ the tables

        [Test]
        public void TheEventTablesHoldTwoTalksForEachEventInEachClassPoolAndNoneForAnAllStar()
        {
            foreach (var table in new[] { TalkTableEventsSpark.Rows, TalkTableEventsSteady.Rows, TalkTableEventsGuarded.Rows })
                Assert.That(TalkTables.Tables.Contains(table), Is.True, "An event table the registry does not join is never offered nor checked.");
            var allStars = new HashSet<string>(CastTemplates.In(CastTemplates.Roster.AllStars).Select(t => t.Id));
            var bad = new List<string>();
            foreach (string pool in ClassPools)
                foreach (string kind in TalkEvents.All)
                {
                    var rows = EventRows.Where(r => r.Pool == pool && r.Owner == null && r.When.Event == kind).ToList();
                    if (rows.Count != 2) bad.Add(pool + " " + kind + ": " + rows.Count + " talks");
                    foreach (var row in rows.Where(r => !r.Id.StartsWith(pool + ".event." + kind + ".")))
                        bad.Add(row.Id + ": want <pool>.event.<event>.<n>");
                }
            foreach (var row in EventRows.Where(r => r.Pool == TalkPools.Star || r.Owner != null && allStars.Contains(r.Owner)))
                bad.Add(row.Id + ": an All-Star's event talk (no All-Star line is written in this slice)");
            Assert.That(bad, Is.Empty);
            Assert.That(EventRows.Count(r => r.Owner == null), Is.EqualTo(TalkEvents.All.Length * 3 * 2), "Spec 5.4: 7 events x 3 classes x 2 talks.");
        }

        [Test]
        public void NoEventLineHoldsAPrivateOrRomanticWordAGenderedWordOrAName()
        {
            var gendered = new Regex(@"\b(he|she|him|her|his|hers|himself|herself)\b", RegexOptions.IgnoreCase);
            var bad = new List<string>();
            int read = 0;
            foreach (var row in EventRows)
                foreach (string line in Lines(row))
                {
                    read++;
                    foreach (Match m in TalkTablesTests.DenyWords.Matches(line ?? "")) bad.Add(row.Id + ": " + m.Value);
                    foreach (Match m in gendered.Matches(line ?? "")) bad.Add(row.Id + ": " + m.Value);
                    // No token: an event line never names a housemate, who in a house of both rosters could be an All-Star.
                    if ((line ?? "").IndexOf('{') >= 0) bad.Add(row.Id + ": a token in " + line);
                }
            Assert.That(read, Is.EqualTo((42 + 72) * 13), "Every line of every event talk was read, the pools' 42 and the regulars' own 72: the opening and three replies of four lines.");
            Assert.That(bad, Is.Empty);
        }

        // ------------------------------------------------------------------ the gate

        [TestCase(TalkEvents.PlayerHoH)]
        [TestCase(TalkEvents.PlayerNominated)]
        [TestCase(TalkEvents.NpcNominated)]
        [TestCase(TalkEvents.NpcHoH)]
        [TestCase(TalkEvents.NpcVeto)]
        [TestCase(TalkEvents.AllyEvicted)]
        [TestCase(TalkEvents.CoupleNoticed)]
        public void EachEventHoldsFromTheWeeksCommittedStateAndOnlyAtTalkLevelFive(string kind)
        {
            var s = House(out string npc, out string bystander, out var rest);
            Assert.That(TalkEvents.Of(s, npc), Is.Empty, "The control: nothing has happened this week.");
            Happen(s, kind, npc, rest);
            Assert.That(TalkEvents.Of(s, npc), Is.EqualTo(new[] { kind }), "The event holds with them, and no other.");
            Assert.That(TalkEvents.Holds(s, bystander, kind), Is.EqualTo(HoldsForAnyone(kind)), "With a bystander: only the player's own events.");
            Assert.That(TalkEvents.Holds(s, s.playerId, kind), Is.False, "Never with the player.");
            foreach (int level in new[] { TalkRules.Off, TalkRules.Talk, TalkRules.Chemistry, TalkRules.Romance, TalkRules.Couples })
            {
                s.story.talkRules = level;
                Assert.That(TalkEvents.Of(s, npc), Is.Empty, "Below talk rules 5 no event holds (level " + level + ").");
            }
        }

        [TestCase(TalkEvents.PlayerHoH)]
        [TestCase(TalkEvents.PlayerNominated)]
        [TestCase(TalkEvents.NpcNominated)]
        [TestCase(TalkEvents.NpcHoH)]
        [TestCase(TalkEvents.NpcVeto)]
        [TestCase(TalkEvents.AllyEvicted)]
        [TestCase(TalkEvents.CoupleNoticed)]
        public void AtTalkLevelFiveEachEventPutsItsTalkOnOfferAheadOfEveryOtherAndBelowItNever(string kind)
        {
            int rowsChecked = 0;
            foreach (string pool in ClassPools)
                foreach (var row in EventRows.Where(r => r.Pool == pool && r.When.Event == kind).ToList())
                {
                    var s = House(out string npc, out _, out var rest);
                    FreeTalkS2Fixture.ClassAs(s, npc, pool);
                    FreeTalkFixture.ContactOf(s, npc).talks = 1;
                    OpenWindow(s, row.When.Window);
                    var before = FreeTalk.Offer(s, npc);
                    Assert.That(before.Available && before.Talk.Kind != TalkKind.Event, Is.True, row.Id + ": the control, an ordinary talk before the event.");
                    Happen(s, kind, npc, rest);
                    var offer = FreeTalk.Offer(s, npc);
                    Assert.That(offer.Available, Is.True, row.Id + ": " + offer.Reason);
                    Assert.That(offer.Talk.Kind, Is.EqualTo(TalkKind.Event), row.Id + ": the event's talk is on offer, ahead of " + before.Talk.Id);
                    Assert.That(offer.Talk.When.Event, Is.EqualTo(kind), row.Id);
                    // From their pool, or (slice 5's person tables) from their own table, which goes ahead of the pool's.
                    if (offer.Talk.Owner != null) Assert.That(offer.Talk.Owner, Is.EqualTo(StoryPeople.TemplateOf(s.Find(npc)).Id), row.Id);
                    else Assert.That(offer.Talk.Pool, Is.EqualTo(pool), row.Id);
                    Assert.That(FreeTalk.PriorityOf(offer.Talk), Is.EqualTo(100), "Spec 3.13: an event talk's priority.");
                    // The same house below talk rules 5: the talk on offer is the one before the event, as main offers it.
                    foreach (int level in new[] { TalkRules.Romance, TalkRules.Couples })
                    {
                        s.story.talkRules = level;
                        var below = FreeTalk.Offer(s, npc);
                        Assert.That(below.Talk == null || below.Talk.Kind != TalkKind.Event, Is.True, row.Id + ": no event talk at level " + level);
                    }
                    rowsChecked++;
                }
            Assert.That(rowsChecked, Is.EqualTo(6), "The event's two talks in each of the three class pools.");
        }

        [Test]
        public void AtTalkLevelFiveAnEventTalkIsAnsweredAsAnOrdinaryTalkAndSpentOnce()
        {
            var engine = new EpisodeEngine(FreeTalkFixture.Fresh(TalkRules.ComesToTalk));
            // Move-in night: a first talk with everyone, so layer 1 is open with each of them.
            foreach (var npc in FreeTalkFixture.Npcs(engine.Snapshot))
            {
                var result = Answer(engine, npc.id, out _, out _);
                Assert.That(result.accepted, Is.True, "Move-in night's talk with " + npc.id + ": " + result.reason);
            }
            FreeTalkFixture.WalkUntil(engine, x => x.phase == EpisodePhase.Nomination && !string.IsNullOrEmpty(x.hohId));
            var s = engine.Snapshot;
            bool playerHoH = s.hohId == s.playerId;
            string npcId = playerHoH ? FreeTalkFixture.Npcs(s)[0].id : s.hohId;
            var offer = FreeTalk.Offer(s, npcId);
            Assert.That(offer.Talk, Is.Not.Null, offer.Reason);
            Assert.That(offer.Talk.Kind, Is.EqualTo(TalkKind.Event), "The Head of Household's win is this window's talk.");
            Assert.That(offer.Talk.When.Event, Is.EqualTo(playerHoH ? TalkEvents.PlayerHoH : TalkEvents.NpcHoH));
            int talksBefore = FreeTalk.Contact(s, npcId).talks;

            var answered = Answer(engine, npcId, out string talkId, out string tone);
            Assert.That(answered.accepted, Is.True, answered.reason);
            Assert.That(talkId, Is.EqualTo(offer.Talk.Id));
            var after = engine.Snapshot;
            var contact = FreeTalk.Contact(after, npcId);
            Assert.That(contact.talks, Is.EqualTo(talksBefore + 1), "A counted talk.");
            Assert.That(contact.spentTalks, Does.Contain(TalkRules.Spent(talkId, tone)), "Spent with the tone it was answered in.");
            Assert.That(FreeTalk.Offer(after, npcId).Refusal, Is.EqualTo(TalkRefusal.TalkedThisWindow), "One talk a window.");

            int key = FreeTalk.WindowKey(after);
            FreeTalkFixture.WalkUntil(engine, x => FreeTalk.WindowKey(x) != 0 && FreeTalk.WindowKey(x) != key);
            var next = FreeTalk.Offer(engine.Snapshot, npcId);
            Assert.That(next.Available, Is.True, next.Reason);
            Assert.That(next.Talk.Id, Is.Not.EqualTo(talkId), "An event talk is said once.");
        }

        /// <summary>
        /// Spec 3.2: once flirting is open, a talk carries the Flirt answer as its fourth reply, and an event talk is otherwise an
        /// ordinary talk, so it carries it as a layer, rhythm, standing or stage talk does: the button is drawn and the engine
        /// takes it (the review of slice 5's groundwork).
        /// </summary>
        [Test]
        public void AtTalkLevelFiveAnEventTalkCarriesTheFlirtAnswerWhileFlirtingIsOpen()
        {
            var s = FreeTalkS3Fixture.WithInterested(TalkPools.Spark, TalkPools.Spark, out string npc);
            s.story.talkRules = TalkRules.ComesToTalk;
            s.week = 3;
            FreeTalkS3Fixture.Flirting(s, npc, spark: 5, rapport: 30);
            FreeTalkFixture.ContactOf(s, npc).talks = 1;
            Happen(s, TalkEvents.PlayerHoH, npc, new List<string>());
            OpenWindow(s, TalkWindows.AfterHoH);
            Assert.That(TalkRomance.FlirtOpen(s, npc), Is.True, "The control: flirting is open with them.");
            var offer = FreeTalk.Offer(s, npc);
            Assert.That(offer.Available, Is.True, offer.Reason);
            Assert.That(offer.Talk.Kind, Is.EqualTo(TalkKind.Event), "The control: the player's win is the talk on offer.");
            Assert.That(TalkRomance.FlirtOffered(s, npc, offer.Talk), Is.True, "An event talk carries the Flirt answer.");
            Assert.That(TalkScreenLines.FlirtShown(s, npc, offer.Talk, TalkScreenLines.Replies(s, npc, offer.Talk)), Is.True,
                "The Flirt button is drawn on it.");
            var flirted = TalkRomance.Result(s, npc, TalkRomance.CommandText(offer.Talk.Id, TalkRules.Flirt));
            Assert.That(flirted, Is.Not.Null, "The engine takes a Flirt on the event talk.");
            Assert.That(flirted.TalkId, Is.EqualTo(offer.Talk.Id));
        }

        /// <summary>Answers the talk on offer with a housemate in its first tone; the talk's id and the tone, or nulls.</summary>
        static CommandResult Answer(EpisodeEngine engine, string npcId, out string talkId, out string tone)
        {
            var s = engine.Snapshot;
            var offer = FreeTalk.Offer(s, npcId);
            var reply = offer.Talk?.Replies?.FirstOrDefault(r => r != null && r.Choice == TalkChoice.None && TalkRules.IsTone(r.Tone) && r.Tone != TalkRules.Flirt);
            talkId = offer.Talk?.Id;
            tone = reply?.Tone;
            return engine.Apply(FreeTalkFixture.Command(s, npcId, talkId ?? "none", tone ?? TalkRules.Warm));
        }

        // ------------------------------------------------------------------ every earlier level is main's

        /// <summary>
        /// The acceptance: over a fixed season walked at talk rules 3 and 4, talking with every housemate in every window, the talk
        /// on offer (and what answering it did) is exactly what main's tables offer, which are the shipped tables without the rows
        /// written for a later level. The walk passes windows where a level-5 season would have offered an event talk, so the
        /// event rows were there to be offered.
        /// </summary>
        [TestCase(TalkRules.Romance)]
        [TestCase(TalkRules.Couples)]
        public void BelowTalkLevelFiveTheTalkOnOfferOverAWalkedSeasonIsMainsTalk(int level)
        {
            Assert.That(TalkBank.Rows, Is.SameAs(TalkTables.All), "The walk reads the shipped tables.");
            var shipped = Walk(level, out int eventsAtFive);
            List<string> main;
            using (TalkBank.Use(TalkTables.All.Where(r => r.FromRules == 0))) main = Walk(level, out _);
            Assert.That(eventsAtFive, Is.GreaterThan(0), "The walk passed windows where talk rules 5 would have offered an event talk.");
            Assert.That(shipped.Count, Is.GreaterThanOrEqualTo(30), "The walk talked with the house in five windows or more.");
            Assert.That(shipped, Is.EqualTo(main));
        }

        const int WalkWeeks = 4;

        static List<string> Walk(int level, out int eventsAtFive)
        {
            var lines = new List<string>();
            eventsAtFive = 0;
            var engine = new EpisodeEngine(FreeTalkFixture.Fresh(level, seed: 3211u));
            int lastKey = -1;
            for (int step = 0; step < 4000; step++)
            {
                var s = engine.Snapshot;
                if (s.phase == EpisodePhase.Finished || s.week > WalkWeeks || s.Find(s.playerId).status != ContestantStatus.Active) break;
                int key = FreeTalk.WindowKey(s);
                if (key != 0 && key != lastKey)
                {
                    lastKey = key;
                    var five = s.Clone();
                    five.story.talkRules = TalkRules.ComesToTalk;
                    foreach (var npc in FreeTalkFixture.Npcs(five))
                        if (FreeTalk.Offer(five, npc.id).Talk?.Kind == TalkKind.Event) eventsAtFive++;
                    foreach (var npc in FreeTalkFixture.Npcs(s))
                    {
                        var at = engine.Snapshot;
                        var offer = FreeTalk.Offer(at, npc.id);
                        string line = at.week + "/" + key + "/" + npc.id + ": " + offer.Refusal + " " + (offer.Talk?.Id ?? "-") + (offer.Repeat ? " again" : "");
                        var reply = offer.Talk?.Replies?.FirstOrDefault(r => r != null && r.Choice == TalkChoice.None && TalkRules.IsTone(r.Tone) && r.Tone != TalkRules.Flirt);
                        if (reply != null)
                        {
                            var result = engine.Apply(FreeTalkFixture.Command(at, npc.id, offer.Talk.Id, reply.Tone));
                            var contact = FreeTalk.Contact(engine.Snapshot, npc.id);
                            line += " | " + reply.Tone + " " + result.accepted + " rapport " + contact?.rapport + "." + contact?.talkTenths;
                        }
                        lines.Add(line);
                    }
                }
                var next = engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot));
                Assert.That(next.accepted, Is.True, "The walk: " + next.reason);
            }
            return lines;
        }
    }
}
