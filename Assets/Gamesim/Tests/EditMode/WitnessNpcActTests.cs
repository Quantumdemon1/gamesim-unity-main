using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// D2's witness and feed (WAVE-D-NPC-PACTS-PLAN §4.3, §4.6): kind 63 - the house tells the engine the player
    /// saw an act - refused for each of its reasons before anything happens, free when it lands, one line of who,
    /// where and how and nothing more, a loud act heard as well as seen, at most three a window; and the paid
    /// listen-in, which keeps its draws and hears an act the player saw once, its pact and meeting clauses giving
    /// way to D4's sentence. No D2 line names a pact.
    /// </summary>
    public sealed class WitnessNpcActTests
    {
        // ------------------------------------------------------------------ refusals

        [Test]
        public void WithoutTheAllWeekRulesTheWitnessIsRefusedBeforeAnything()
        {
            var s = Open(EpisodePhase.Campaign);
            var act = Staged(s, NpcActKinds.Talk);
            var off = s.Clone();
            off.allWeekRulesStartWeek = 0;
            off.npcSocial.acts.Clear(); off.npcSocial.beatPlan.Clear(); off.npcSocial.beatWindow = Windows.None;
            off.npcSocial.beatWeek = off.npcSocial.beatsFired = off.npcSocial.beatSeats = 0;
            string before = Json(off);
            var refused = Witness(off, act);
            Assert.That(refused.accepted, Is.False);
            Assert.That(refused.reason, Is.EqualTo(EpisodeEngine.WaveDKindRefusal));
            Assert.That(Json(refused.state), Is.EqualTo(before));
        }

        /// <summary>Each of the witness's reasons, by the pure check the house's watch asks, and the engine refusing on it with nothing changed.</summary>
        [Test]
        public void EachRefusalIsItsOwnAndChangesNothing()
        {
            var s = Open(EpisodePhase.Campaign);
            var npcs = Npcs(s);
            var act = Staged(s, NpcActKinds.Talk);
            Assert.That(EpisodeEngine.WitnessRefusal(s, act.id, act.actorId, act.partnerId), Is.Null, "Precondition: it can be seen.");
            var cases = new Dictionary<string, Func<EpisodeState, string>>
            {
                ["nothing like it"] = x => EpisodeEngine.WitnessRefusal(x, "0-2-0", act.actorId, act.partnerId),
                ["its two swapped"] = x => EpisodeEngine.WitnessRefusal(x, act.id, act.partnerId, act.actorId),
                ["somebody else"] = x => EpisodeEngine.WitnessRefusal(x, act.id, act.actorId, npcs.First(id => id != act.actorId && id != act.partnerId)),
                ["two ticks on"] = x => { x.windowActions[Windows.AfterVeto] = act.firedTick + 2; return EpisodeEngine.WitnessRefusal(x, act.id, act.actorId, act.partnerId); },
                ["one of them gone"] = x => { x.Find(act.partnerId).status = ContestantStatus.Evicted; return EpisodeEngine.WitnessRefusal(x, act.id, act.actorId, act.partnerId); },
                ["out of sight"] = x => { x.npcSocial.acts.Single(a => a.id == act.id).room = null; return EpisodeEngine.WitnessRefusal(x, act.id, act.actorId, act.partnerId); },
                ["seen already"] = x => { x.npcSocial.acts.Single(a => a.id == act.id).sighted = true; return EpisodeEngine.WitnessRefusal(x, act.id, act.actorId, act.partnerId); },
                ["about the player"] = x => { x.npcSocial.acts.Single(a => a.id == act.id).subjectId = x.playerId; return EpisodeEngine.WitnessRefusal(x, act.id, act.actorId, act.partnerId); },
                ["no window"] = x => { x.phase = EpisodePhase.Eviction; return EpisodeEngine.WitnessRefusal(x, act.id, act.actorId, act.partnerId); },
                ["the player gone"] = x => { x.Find(x.playerId).status = ContestantStatus.Evicted; return EpisodeEngine.WitnessRefusal(x, act.id, act.actorId, act.partnerId); },
                ["three seen this window"] = x =>
                {
                    for (int k = 0; k < 3; k++) { var seen = Staged(x, NpcActKinds.Talk); seen.sighted = true; }
                    return EpisodeEngine.WitnessRefusal(x, act.id, act.actorId, act.partnerId);
                },
            };
            var reasons = new List<string>();
            foreach (var c in cases)
            {
                string reason = c.Value(s.Clone());
                Assert.That(reason, Is.Not.Null.And.Not.Empty, c.Key);
                reasons.Add(reason);
            }
            Assert.That(reasons.Distinct().Count(), Is.GreaterThanOrEqualTo(8), "Each its own words: " + string.Join(" | ", reasons.Distinct()));
            // Through the engine: refused before anything is spent, drawn or logged.
            foreach (var c in new Dictionary<string, Func<EpisodeCommand, EpisodeCommand>>
            {
                ["unknown"] = x => { x.text = "0-2-0"; return x; },
                ["swapped"] = x => { string t = x.targetId; x.targetId = x.secondTargetId; x.secondTargetId = t; return x; },
            })
            {
                string before = Json(s);
                var refused = new EpisodeEngine(s).Apply(c.Value(Command(s, act)));
                Assert.That(refused.accepted, Is.False, c.Key);
                Assert.That(Json(refused.state), Is.EqualTo(before), c.Key);
            }
        }

        [Test]
        public void AtMostThreeActsAreSeenAWindow()
        {
            var s = Open(EpisodePhase.Social);
            var acts = Enumerable.Range(0, 4).Select(_ => Staged(s, NpcActKinds.Talk)).ToList();
            var engine = new EpisodeEngine(s);
            for (int k = 0; k < 3; k++)
            {
                var seen = engine.Apply(Command(engine.Snapshot, acts[k]));
                Assert.That(seen.accepted, Is.True, seen.reason);
            }
            var fourth = engine.Apply(Command(engine.Snapshot, acts[3]));
            Assert.That(fourth.accepted, Is.False, "The window's sightings are spent.");
            Assert.That(engine.Snapshot.npcSocial.acts.Count(a => a.sighted), Is.EqualTo(EpisodeEngine.SightingsAWindow));
        }

        // ------------------------------------------------------------------ the line

        /// <summary>
        /// One line, to the player alone, of who, where and how: an ordinary word "talking", the strategy kinds
        /// "with their heads together", a fight heard - never what was said, a pact, a rumour's subject or a number.
        /// </summary>
        [TestCase(NpcActKinds.Talk, "Kitchen", "sighting", "You saw {A} and {B} talking in the kitchen.")]
        [TestCase(NpcActKinds.Campaign, "Living", "sighting", "You saw {A} and {B} talking in the living room.")]
        [TestCase(NpcActKinds.Pact, "Bedroom", "sighting", "You saw {A} and {B} with their heads together in the bedroom.")]
        [TestCase(NpcActKinds.Rumour, "Yard", "sighting", "You saw {A} and {B} with their heads together in the competition yard.")]
        [TestCase(NpcActKinds.Court, "HoH", "sighting", "You saw {A} and {B} with their heads together in the HoH suite.")]
        [TestCase(NpcActKinds.Meet, "Games", "sighting", "You saw {A} and {B} with their heads together in the game room.")]
        [TestCase(NpcActKinds.Confront, "Living", "overheard", "You heard {A} have words with {B} in the living room.")]
        public void TheLineSaysWhoWhereAndHowAndNothingMore(string kind, string room, string lineKind, string words)
        {
            var s = Open(EpisodePhase.Campaign);
            var act = Staged(s, kind, room: room, subject: kind == NpcActKinds.Rumour || kind == NpcActKinds.Hunt);
            var result = Witness(s, act);
            Assert.That(result.accepted, Is.True, result.reason);
            var said = result.state.events.Where(e => e.sequence >= s.nextSequence && WaveDEventKinds.All.Contains(e.kind)).ToList();
            Assert.That(said, Has.Count.EqualTo(1), "One line.");
            Assert.That(said[0].kind, Is.EqualTo(lineKind));
            Assert.That(said[0].text, Is.EqualTo(words.Replace("{A}", s.Find(act.actorId).name).Replace("{B}", s.Find(act.partnerId).name)));
            Assert.That(said[0].audienceIds, Is.EqualTo(new[] { s.playerId }), "To the player alone.");
            var seen = result.state.npcSocial.acts.Single(a => a.id == act.id);
            Assert.That(seen.sighted, Is.True);
            Assert.That(seen.overheard, Is.EqualTo(kind == NpcActKinds.Confront), "A fight is heard as well as seen.");
            if (act.subjectId != null && s.Find(act.subjectId) != null)
                Assert.That(said[0].text, Does.Not.Contain(s.Find(act.subjectId).name), "Never a rumour's subject.");
            foreach (var pact in result.state.alliances) Assert.That(said[0].text, Does.Not.Contain(pact.name), "Never a pact.");
        }

        /// <summary>Free: no tick, no draw, nobody's view moved, no beat fired; the act's flags, one line and its id are all it writes.</summary>
        [Test]
        public void TheWitnessIsFree()
        {
            var s = Open(EpisodePhase.Social);
            var act = Staged(s, NpcActKinds.Talk);
            var result = Witness(s, act);
            Assert.That(result.accepted, Is.True, result.reason);
            var after = result.state;
            Assert.That(after.randomState, Is.EqualTo(s.randomState));
            Assert.That(after.windowActions, Is.EqualTo(s.windowActions));
            Assert.That(after.socialActions, Is.EqualTo(s.socialActions));
            Assert.That(after.npcSocial.beatsFired, Is.EqualTo(s.npcSocial.beatsFired), "No tick, so no beat.");
            Assert.That(Json(after.relationships), Is.EqualTo(Json(s.relationships)), "Nobody's view moved.");
            Assert.That(Json(after.memories), Is.EqualTo(Json(s.memories)), "Nothing remembered: a sighting teaches no fact.");
            Assert.That(after.nextSequence, Is.EqualTo(s.nextSequence + 1), "One line's id.");
        }

        // ------------------------------------------------------------------ the listen-in

        /// <summary>
        /// The paid listen-in, on the two of an open act the player saw: today's draws and today's line, the act's
        /// clause after it, once - a second listen-in on the same two hears none - and no knowledge granted.
        /// </summary>
        [Test]
        public void AListenInKeepsItsDrawsAndHearsASeenActOnce()
        {
            var s = Open(EpisodePhase.Social);
            var act = Staged(s, NpcActKinds.Promise, room: "Bedroom");
            act.sighted = true;
            var heard = Overhear(s, act, out var withoutBeats);
            string a = s.Find(act.actorId).name, b = s.Find(act.partnerId).name;
            string line = Line(heard, s);
            string plain = Line(withoutBeats, Without(s));
            Assert.That(line, Is.EqualTo(plain + " " + a + " gave " + b + " their word."), "Today's line, the act's clause after it.");
            Assert.That(heard.state.randomState, Is.EqualTo(withoutBeats.state.randomState), "Today's draws.");
            Assert.That(heard.state.npcSocial.acts.Single(x => x.id == act.id).overheard, Is.True);
            Assert.That(Json(heard.state.memories.Where(m => m.ownerId == s.playerId)), Is.EqualTo(Json(withoutBeats.state.memories.Where(m => m.ownerId == s.playerId))),
                "The player remembers what they always did: no knowledge granted.");
            var again = Overhear(heard.state, act, out _);
            Assert.That(Line(again, heard.state), Does.Not.Contain(" their word."), "Each act gives its clause once.");
        }

        [Test]
        public void AnActThePlayerDidNotSeeIsNeverHeard()
        {
            var s = Open(EpisodePhase.Social);
            var act = Staged(s, NpcActKinds.Hunt, room: "Yard", subject: true);
            var heard = Overhear(s, act, out var withoutBeats);
            Assert.That(Line(heard, s), Is.EqualTo(Line(withoutBeats, Without(s))), "Unseen, it adds nothing.");
            Assert.That(heard.state.npcSocial.acts.Single(x => x.id == act.id).overheard, Is.False);
        }

        /// <summary>Every clause a listen-in can hear, by kind, in the plan's words; a hunt and a rumour name their subject, as overheard.</summary>
        [TestCase(NpcActKinds.Hunt, " {A} told {B} that {Z} has to go.")]
        [TestCase(NpcActKinds.Rumour, " {A} told {B} that {Z} is the biggest threat in this house.")]
        [TestCase(NpcActKinds.Pact, " {A} and {B} agreed to work together.")]
        [TestCase(NpcActKinds.Meet, " {A} and {B} were going over a plan.")]
        [TestCase(NpcActKinds.Promise, " {A} gave {B} their word.")]
        [TestCase(NpcActKinds.Court, " {A} was making their case to {B}.")]
        [TestCase(NpcActKinds.Talk, null)]
        [TestCase(NpcActKinds.Confront, null)]
        public void EachKindsClauseIsThePlansWords(string kind, string words)
        {
            var s = Open(EpisodePhase.Social);
            var act = Staged(s, kind, room: "Bedroom", subject: kind == NpcActKinds.Hunt || kind == NpcActKinds.Rumour);
            string clause = EpisodeEngine.ActClause(s, act);
            if (words == null) { Assert.That(clause, Is.Null); return; }
            Assert.That(clause, Is.EqualTo(words.Replace("{A}", s.Find(act.actorId).name).Replace("{B}", s.Find(act.partnerId).name)
                .Replace("{Z}", act.subjectId == null || s.Find(act.subjectId) == null ? "" : s.Find(act.subjectId).name)));
            foreach (var pact in s.alliances) Assert.That(clause, Does.Not.Contain(pact.name), "Never a pact's name.");
        }

        /// <summary>
        /// Where D4's sentence says what the two are - the one pact of exactly them, out of the player's knowing -
        /// a pact formed or a pact's meeting gives no clause of its own (§4.3); where it does not, the same act does.
        /// </summary>
        [TestCase(NpcActKinds.Pact)] [TestCase(NpcActKinds.Meet)]
        public void DFoursSentenceSuppressesThePactAndMeetingClauses(string kind)
        {
            var s = Open(EpisodePhase.Social);
            var npcs = Npcs(s);
            var pact = new AllianceState { id = "alliance-npc-9100", name = "The Quiet Pact", members = new List<string> { npcs[0], npcs[1] }, active = true };
            s.alliances.Add(pact);
            s.ledger.alliances.Add(new AllianceRow { id = pact.id, startedWeek = s.week, why = "npc" });
            EpisodeEngine.AllianceFormedUnderRead(s, pact);
            var act = Staged(s, kind, npcs[0], npcs[1], room: "Bedroom");
            if (kind == NpcActKinds.Meet) act.subjectId = pact.id;
            act.sighted = true;
            Assert.That(AllianceLeaks.On(s), Is.True, "Precondition: the leak rules.");
            var heard = Overhear(s, act, out _);
            string line = Line(heard, s);
            Assert.That(line, Does.EndWith(AllianceLeaks.ListenInSentence(heard.state, pact)), "D4's sentence.");
            Assert.That(line, Does.Not.Contain(" agreed to work together.").And.Not.Contain(" were going over a plan."), "and no clause of D2's.");
            Assert.That(heard.state.npcSocial.acts.Single(x => x.id == act.id).overheard, Is.False, "The act is left unheard.");

            var noPact = s.Clone();
            noPact.alliances.RemoveAll(x => x.id == pact.id);
            noPact.ledger.alliances.RemoveAll(x => x.id == pact.id);
            noPact.story.facts.RemoveAll(f => f.kind == FactKinds.Alliance && f.refId == pact.id);
            if (kind == NpcActKinds.Meet)
            {
                var asPact = noPact.npcSocial.acts.Single(x => x.id == act.id);
                asPact.kind = NpcActKinds.Pact; asPact.subjectId = null;
            }
            var plain = Overhear(noPact, noPact.npcSocial.acts.Single(x => x.id == act.id), out _);
            Assert.That(Line(plain, noPact), Does.EndWith(" agreed to work together."), "Without D4's sentence the clause is said.");
        }

        // ------------------------------------------------------------------ what the house stages

        /// <summary>
        /// The director's selector (D2-S3): the oldest open acts staged in a room and not yet seen, nobody in two,
        /// two at most; nothing once the window's sightings are spent, the player has gone, or without the rules.
        /// </summary>
        [Test]
        public void TheHouseStagesTheOldestOpenUnseenActsNobodyInTwo()
        {
            var s = Open(EpisodePhase.Campaign);
            var npcs = Npcs(s);
            Assert.That(EpisodeEngine.StageableActs(s), Is.Empty, "Nothing staged before anything happened.");
            var first = Staged(s, NpcActKinds.Talk, npcs[0], npcs[1]);
            var unstaged = Staged(s, NpcActKinds.Eavesdrop, npcs[2], npcs[3], room: null);
            var seen = Staged(s, NpcActKinds.Pact, npcs[2], npcs[3]); seen.sighted = true;
            var sharing = Staged(s, NpcActKinds.Rumour, npcs[1], npcs[2]);
            var second = Staged(s, NpcActKinds.Promise, npcs[2], npcs[3]);
            var later = Staged(s, NpcActKinds.Talk, npcs[3], npcs[0]);
            Assert.That(EpisodeEngine.StageableActs(s).Select(a => a.id), Is.EqualTo(new[] { first.id, second.id }),
                "Oldest first, skipping the unstaged, the seen and one sharing a person; two at most.");
            Assert.That(EpisodeEngine.StageableActs(s, 4).Select(a => a.id), Is.EqualTo(new[] { first.id, second.id }), "Nobody in two.");
            // Later ticks come after earlier ones, and a closed act is never staged.
            first.firedTick = EpisodeEngine.WindowTick(s, first.window) + 1;
            Assert.That(EpisodeEngine.StageableActs(s, 1).Select(a => a.id), Is.EqualTo(new[] { sharing.id }));
            var moved = s.Clone(); moved.windowActions[Windows.AfterVeto] += 2;
            Assert.That(EpisodeEngine.StageableActs(moved).Select(a => a.id), Is.EqualTo(new[] { first.id }), "Only the one fired later is still open.");
            var spent = s.Clone();
            for (int k = 0; k < 2; k++) { var x = Staged(spent, NpcActKinds.Talk, npcs[0], npcs[3]); x.sighted = true; }
            Assert.That(EpisodeEngine.StageableActs(spent), Is.Empty, "Three seen this window: nothing more to see.");
            var gone = s.Clone(); gone.Find(gone.playerId).status = ContestantStatus.Evicted;
            Assert.That(EpisodeEngine.StageableActs(gone), Is.Empty, "Nobody to see it.");
            var off = s.Clone(); off.allWeekRulesStartWeek = 0;
            Assert.That(EpisodeEngine.StageableActs(off), Is.Empty, "Without the rules nothing is staged.");
            Assert.That(later.id, Is.Not.Null); Assert.That(unstaged.room, Is.Null);
        }

        // ------------------------------------------------------------------ privacy

        /// <summary>No D2 line or clause names a pact, whatever the act and wherever it is.</summary>
        [Test]
        public void NoD2LineNamesAPact()
        {
            var s = Open(EpisodePhase.Campaign);
            var npcs = Npcs(s);
            var pact = new AllianceState { id = "alliance-npc-9200", name = "The Named Pact", members = new List<string> { npcs[0], npcs[1] }, active = true };
            s.alliances.Add(pact);
            foreach (string kind in NpcActKinds.All)
                foreach (string room in RoomWords.Rooms)
                {
                    var act = new NpcActState { id = "x", kind = kind, actorId = npcs[0], partnerId = npcs[1], subjectId = kind == NpcActKinds.Meet ? pact.id : npcs[2], room = room };
                    Assert.That(EpisodeEngine.SightingLine(s, act), Does.Not.Contain(pact.name).And.Not.Contain(pact.id), kind + " " + room);
                    Assert.That(EpisodeEngine.ActClause(s, act) ?? "", Does.Not.Contain(pact.name).And.Not.Contain(pact.id), kind);
                }
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>A fresh season with the all-week beats walked to its second week's window of this phase, the player in the house, the window's own acts cleared so a test stages its own.</summary>
        internal static EpisodeState Open(EpisodePhase phase)
        {
            EpisodeState s = null;
            for (uint seed = 6201; seed < 6221 && (s == null || s.Find(s.playerId).status != ContestantStatus.Active); seed++)
            {
                var engine = new EpisodeEngine(AllWeekCadenceTests.Fresh(8, seed));
                for (int i = 0; i < 3000 && !(engine.Snapshot.week == 2 && engine.Snapshot.phase == phase); i++)
                {
                    var result = engine.Apply(Next(engine.Snapshot));
                    Assert.That(result.accepted, Is.True, result.reason);
                }
                s = engine.Snapshot;
            }
            Assert.That(s.Find(s.playerId).status, Is.EqualTo(ContestantStatus.Active), "Precondition: the player is in the house.");
            int window = EpisodeEngine.Window(s);
            s.npcSocial.acts.RemoveAll(a => a.window == window);
            return s;
        }

        private static List<string> Npcs(EpisodeState s) => s.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();

        private static int staged;

        /// <summary>An act of the open window, open and staged, at its tick, between two houseguests (the first two, unless named).</summary>
        internal static NpcActState Staged(EpisodeState s, string kind, string actor = null, string partner = null, string room = "Kitchen", bool subject = false)
        {
            var npcs = Npcs(s);
            int window = EpisodeEngine.Window(s);
            var act = new NpcActState
            {
                id = s.week + "-" + window + "-" + (70 + staged++ % 25), kind = kind, actorId = actor ?? npcs[0], partnerId = partner ?? npcs[1],
                subjectId = subject ? npcs[2] : null, room = room, week = s.week, window = window, firedTick = EpisodeEngine.WindowTick(s, window),
            };
            while (s.npcSocial.acts.Any(a => a.id == act.id)) act.id = s.week + "-" + window + "-" + (70 + staged++ % 25);
            // A pact's meeting names its pact (D2's decision 3): the two's own, made here where they have none.
            if (kind == NpcActKinds.Meet)
            {
                var pact = s.alliances.FirstOrDefault(a => a.active && a.members.Contains(act.actorId) && a.members.Contains(act.partnerId));
                if (pact == null)
                {
                    pact = new AllianceState { id = "alliance-npc-" + (9000 + staged), name = "The Staged Pact", members = new List<string> { act.actorId, act.partnerId }, active = true };
                    s.alliances.Add(pact);
                    s.ledger.alliances.Add(new AllianceRow { id = pact.id, startedWeek = s.week, why = "npc" });
                    EpisodeEngine.AllianceFormedUnderRead(s, pact);
                }
                act.subjectId = pact.id;
            }
            s.npcSocial.acts.Add(act);
            return act;
        }

        private static EpisodeCommand Command(EpisodeState s, NpcActState act) => new EpisodeCommand
        {
            id = "witness-" + s.revision + "-" + act.id, actorId = s.playerId, kind = EpisodeCommandKind.WitnessNpcAct,
            targetId = act.actorId, secondTargetId = act.partnerId, text = act.id, expectedRevision = s.revision, expectedPhase = s.phase,
        };

        private static CommandResult Witness(EpisodeState s, NpcActState act) => new EpisodeEngine(s).Apply(Command(s, act));

        /// <summary>
        /// A listen-in on the act's two that is not caught (the season's stream set to a value whose draw succeeds),
        /// and the same listen-in on the same season without the all-week rules.
        /// </summary>
        private static CommandResult Overhear(EpisodeState s, NpcActState act, out CommandResult withoutBeats)
        {
            for (uint attempt = 1; attempt < 200; attempt++)
            {
                var at = s.Clone();
                at.randomState = attempt * 2654435761u;
                var heard = new EpisodeEngine(at).Apply(Listen(at, act));
                Assert.That(heard.accepted, Is.True, heard.reason);
                if (heard.state.events.Any(e => e.sequence >= at.nextSequence && e.kind == "eavesdrop" && e.text.StartsWith("You overheard ", StringComparison.Ordinal)))
                {
                    var off = Without(at);
                    withoutBeats = new EpisodeEngine(off).Apply(Listen(off, act));
                    Assert.That(withoutBeats.accepted, Is.True, withoutBeats.reason);
                    return heard;
                }
            }
            throw new AssertionException("No listen-in went unnoticed.");
        }

        private static EpisodeCommand Listen(EpisodeState s, NpcActState act) => new EpisodeCommand
        {
            id = "listen-" + s.revision + "-" + s.randomState, actorId = s.playerId, kind = EpisodeCommandKind.Eavesdrop,
            targetId = act.actorId, secondTargetId = act.partnerId, expectedRevision = s.revision, expectedPhase = s.phase,
        };

        /// <summary>The same season with the all-week rules never on: no plan, no acts.</summary>
        private static EpisodeState Without(EpisodeState s)
        {
            var off = s.Clone();
            off.allWeekRulesStartWeek = 0;
            off.npcSocial.acts.Clear(); off.npcSocial.beatPlan.Clear(); off.npcSocial.beatWindow = Windows.None;
            off.npcSocial.beatWeek = off.npcSocial.beatsFired = off.npcSocial.beatSeats = 0;
            return off;
        }

        /// <summary>The listen-in's line in a step.</summary>
        private static string Line(CommandResult result, EpisodeState before) =>
            result.state.events.Single(e => e.sequence >= before.nextSequence && e.kind == "eavesdrop").text;

        private static EpisodeCommand Next(EpisodeState state)
        {
            var command = EpisodeEngineTests.NextCommand(state);
            if (command.kind == EpisodeCommandKind.AnswerJury && EpisodeEngine.FinaleOn(state))
            {
                var exchange = state.juryExchanges[state.juryQuestionIndex];
                if (exchange.finalistId == state.playerId)
                    command.secondTargetId = FinaleQuestions.Offered(exchange.category, exchange.receiptKind).First();
            }
            return command;
        }

        private static string Json(object value) => JsonConvert.SerializeObject(value);
    }
}
