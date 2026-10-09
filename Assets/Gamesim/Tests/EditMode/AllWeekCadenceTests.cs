using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// D2's cadence (WAVE-D-NPC-PACTS-PLAN §1, §4.3, §4.6): the gate and its enable, the rest rotation, the
    /// window's plan and its keyed order, the schedule, each window's close before its decision and its
    /// idempotence, player-free closes, the ladder's weekly limits and the quota, the week's acts and their
    /// bound, no draw from the season's stream, Dissolve alone as the social week opens, the recorded courts
    /// and campaign visits, the status marker, a counter left standing, positional words after the veto,
    /// and whole seasons - the sixteen-person house, a fresh season under the hearing rules, and a replay
    /// through a save at every command. Pure, in the Unity-free subset and the editor's suite.
    /// </summary>
    public sealed class AllWeekCadenceTests
    {
        // ------------------------------------------------------------------ the gate

        [Test]
        public void EnableAllWeekClampsToTheSeasonAndNeedsTheWeekRulesNoLater()
        {
            var s = ContentCatalog.Create(6101);
            Assert.Throws<ArgumentNullException>(() => EpisodeEngine.EnableAllWeek(null));
            Assert.Throws<ArgumentException>(() => EpisodeEngine.EnableAllWeek(s), "No week rules: no windows to play in.");
            Assert.That(s.allWeekRulesStartWeek, Is.Zero);
            EpisodeEngine.EnableWeek(s, 2);
            Assert.Throws<ArgumentException>(() => EpisodeEngine.EnableAllWeek(s, 1), "The week rules start after it.");
            EpisodeEngine.EnableAllWeek(s, 9);
            Assert.That(s.allWeekRulesStartWeek, Is.EqualTo(2), "Clamped to the week after the season's own.");
            EpisodeEngine.EnableWeek(s);
            EpisodeEngine.EnableAllWeek(s, 0);
            Assert.That(s.allWeekRulesStartWeek, Is.EqualTo(1), "Never before week one.");
            Valid(s);
        }

        /// <summary>The importer switches the week rules on for next week, so they are not on yet when the all-week beats are enabled with them (D2-M2).</summary>
        [Test]
        public void TheImportersEnableAtNextWeekSucceedsWhileTheWeekRulesAreNotOnYet()
        {
            var s = ContentCatalog.Create(6102);
            EpisodeEngine.EnableWeek(s, s.week + 1);
            Assert.That(EpisodeEngine.WeekRulesOn(s), Is.False);
            EpisodeEngine.EnableAllWeek(s, s.week + 1);
            Assert.That(s.allWeekRulesStartWeek, Is.EqualTo(s.week + 1));
            Assert.That(EpisodeEngine.AllWeekOn(s), Is.False, "Not this week.");
            Valid(s);
        }

        [Test]
        public void TheGateNeedsItsWeekTheWindowsAndTheHouseActingOnItsOwnAccount()
        {
            var s = Fresh(8, 6103);
            Assert.That(EpisodeEngine.AllWeekOn(s), Is.True);
            var later = s.Clone(); later.allWeekRulesStartWeek = 2;
            Assert.That(EpisodeEngine.AllWeekOn(later), Is.False, "Before its start week.");
            var never = s.Clone(); never.allWeekRulesStartWeek = 0;
            Assert.That(EpisodeEngine.AllWeekOn(never), Is.False, "Never.");
            var windowless = s.Clone(); windowless.weekRulesStartWeek = 0;
            Assert.That(EpisodeEngine.AllWeekOn(windowless), Is.False, "No windows.");
            var waiting = s.Clone(); waiting.npcSocial.rulesStartWeek = 2;
            Assert.That(EpisodeEngine.AllWeekOn(waiting), Is.False, "The house not yet on its own account.");
            Assert.That(EpisodeEngine.AllWeekOn(null), Is.False);
        }

        /// <summary>With the rule on but the house not yet acting on its own account, nothing plans or acts.</summary>
        [Test]
        public void NoBeatsWhileTheHouseHasNotBegunToActOnItsOwn()
        {
            var s = Fresh(8, 6104);
            s.npcSocial.rulesStartWeek = 2;
            var engine = new EpisodeEngine(s);
            To(engine, x => x.week == 1 && x.phase == EpisodePhase.Campaign);
            var at = engine.Snapshot;
            Assert.That(at.npcSocial.beatWindow, Is.EqualTo(Windows.None));
            Assert.That(at.npcSocial.acts, Is.Empty);
        }

        [Test]
        public void NoBeatsOnMoveInNight()
        {
            var s = Fresh(8, 6105);
            Assert.That(EpisodeEngine.IsFirstNight(s), Is.True);
            var engine = new EpisodeEngine(s);
            var npc = s.Active.First(c => !c.isPlayer);
            var talked = Apply(engine, EpisodeCommandKind.Talk, npc.id);
            Assert.That(talked.accepted, Is.True, talked.reason);
            Assert.That(talked.state.npcSocial.beatWindow, Is.EqualTo(Windows.None), "No plan on move-in night.");
            Assert.That(talked.state.npcSocial.acts, Is.Empty);
            To(engine, x => x.phase == EpisodePhase.HoH);
            Assert.That(engine.Snapshot.npcSocial.acts, Is.Empty, "Nor at its close.");
        }

        // ------------------------------------------------------------------ the schedule

        [Test]
        public void DueIsInStepWithTheSeatsAndAllOfThemOnceTheyAreSpent()
        {
            // AfterNominations' one seat, eight in the house: three at the opening, the other two after a word or at the close.
            Assert.That(Enumerable.Range(0, 3).Select(t => EpisodeEngine.Due(5, 1, t)), Is.EqualTo(new[] { 3, 5, 5 }));
            // AfterEviction's two: 2, 2 more, 1 more.
            Assert.That(Enumerable.Range(0, 4).Select(t => EpisodeEngine.Due(5, 2, t)), Is.EqualTo(new[] { 2, 4, 5, 5 }));
            // A sixteen-house's five seats, eleven beats.
            Assert.That(Enumerable.Range(0, 7).Select(t => EpisodeEngine.Due(11, 5, t)), Is.EqualTo(new[] { 2, 4, 6, 8, 10, 11, 11 }));
            Assert.That(EpisodeEngine.Due(0, 2, 0), Is.Zero, "An empty plan has nothing due.");
            Assert.That(EpisodeEngine.Due(4, 2, 50), Is.EqualTo(4), "Bought time fires nothing more.");
        }

        [Test]
        public void EachHousemateRestsOneWindowAWeekAndAHaveNotTwo()
        {
            var s = Fresh(8, 6106);
            for (int week = 1; week <= 4; week++)
            {
                s.week = week;
                for (int i = 0; i < s.contestants.Count; i++)
                {
                    var rests = Enumerable.Range(0, Windows.Count).Where(w => EpisodeEngine.Rests(s, i, w)).ToList();
                    Assert.That(rests, Is.EqualTo(new[] { (i + week) % Windows.Count }), "index " + i + " week " + week);
                }
            }
            s.week = 2;
            var npc = s.contestants.First(c => !c.isPlayer);
            int index = s.contestants.IndexOf(npc);
            s.haveNots.Add(npc.id);
            Assert.That(Enumerable.Range(0, Windows.Count).Where(w => EpisodeEngine.Rests(s, index, w)),
                Is.EqualTo(new[] { (index + 2) % 4, (index + 4) % 4 }.OrderBy(w => w)), "A Have-Not rests in the opposite window too.");
            Assert.That(EpisodeEngine.BeatQuota(s, npc.id), Is.EqualTo(2));
            Assert.That(EpisodeEngine.BeatQuota(s, s.contestants.Last(c => !c.isPlayer).id), Is.EqualTo(3));
        }

        /// <summary>
        /// As the nominations open, the window's plan: every houseguest in the house who is not resting, once,
        /// in the order the window's own keyed stream shuffles them; the window's seats as they stand; and the
        /// beats due at its opening fired, each kept as an act of the window at tick 0.
        /// </summary>
        [Test]
        public void AWindowsPlanIsItsWakingHouseguestsInTheWindowsKeyedOrder()
        {
            var engine = new EpisodeEngine(Fresh(8, 6107));
            To(engine, x => x.phase == EpisodePhase.HoH);
            var before = engine.Snapshot;
            To(engine, x => x.phase == EpisodePhase.Nomination);
            var s = engine.Snapshot;
            var social = s.npcSocial;
            var waking = new List<string>();
            for (int i = 0; i < s.contestants.Count; i++)
                if (!s.contestants[i].isPlayer && s.contestants[i].status == ContestantStatus.Active && !EpisodeEngine.Rests(s, i, Windows.AfterHoH))
                    waking.Add(s.contestants[i].id);
            var draw = StoryRandom.Stream(s, "allweek:order:" + s.week + ":" + Windows.AfterHoH);
            for (int i = waking.Count - 1; i > 0; i--)
            {
                int j = Math.Min(i, (int)(draw() * (i + 1)));
                string swap = waking[i]; waking[i] = waking[j]; waking[j] = swap;
            }
            Assert.That(social.beatWeek, Is.EqualTo(s.week));
            Assert.That(social.beatWindow, Is.EqualTo(Windows.AfterHoH));
            Assert.That(social.beatPlan, Is.EqualTo(waking), "The waking houseguests, in the window's keyed order.");
            Assert.That(social.beatSeats, Is.EqualTo(EpisodeEngine.WindowSeats(s, Windows.AfterHoH)));
            Assert.That(social.beatsFired, Is.EqualTo(EpisodeEngine.Due(waking.Count, social.beatSeats, 0)));
            foreach (var act in social.acts.Where(EpisodeEngine.IsBeat))
            {
                Assert.That(act.window, Is.EqualTo(Windows.AfterHoH)); Assert.That(act.firedTick, Is.Zero);
                Assert.That(act.week, Is.EqualTo(s.week));
                int k = int.Parse(act.id.Split('-')[2]);
                Assert.That(act.actorId, Is.EqualTo(social.beatPlan[k]), "Beat k is the plan's k-th houseguest.");
                Assert.That(NpcActKinds.IsKnown(act.kind), Is.True);
                Assert.That(act.room == null || RoomWords.IsRoom(act.room), Is.True);
            }
            Assert.That(s.npcSocial.acts.Count(a => !before.npcSocial.acts.Any(b => b.id == a.id) && EpisodeEngine.IsBeat(a)),
                Is.LessThanOrEqualTo(social.beatsFired));
        }

        /// <summary>A house whose houseguests all rest in one window plans it empty and fires nothing there (D2's decision 1); every other window plays.</summary>
        [Test]
        public void ASmallHouseCanRestEveryHousemateInAWindow()
        {
            var s = Fresh(6, 6108);
            // Two houseguests four places apart in the cast rest in the same window; take the rest out of the house.
            var npcs = s.contestants.Where(c => !c.isPlayer).ToList();
            var keep = new[] { npcs[0], npcs.First(c => (s.contestants.IndexOf(c) - s.contestants.IndexOf(npcs[0])) % 4 == 0 && c != npcs[0]) };
            foreach (var c in npcs.Where(c => !keep.Contains(c))) c.status = ContestantStatus.Evicted;
            s.week = 2;
            int resting = (s.contestants.IndexOf(keep[0]) + s.week) % 4;
            foreach (int window in Enumerable.Range(0, Windows.Count))
            {
                var at = InWindow(s, window);
                at.npcSocial.acts.Clear();
                EpisodeEngine.Close(at);
                Assert.That(at.npcSocial.beatWindow, Is.EqualTo(window), "A plan, empty or not.");
                if (window == resting)
                {
                    Assert.That(at.npcSocial.beatPlan, Is.Empty, "Both rest: nobody plays this window.");
                    Assert.That(at.npcSocial.acts, Is.Empty);
                }
                else Assert.That(at.npcSocial.beatPlan, Is.EquivalentTo(keep.Select(c => c.id)));
            }
        }

        [Test]
        public void CompeteInTheVetoFiresNoBeat()
        {
            var engine = new EpisodeEngine(Fresh(8, 6109));
            To(engine, x => x.phase == EpisodePhase.Veto && !x.competitionResolved && EpisodeEngine.CompetitionPlayers(x).Any(p => p.isPlayer));
            var s = engine.Snapshot;
            Assert.That(EpisodeEngine.Window(s), Is.EqualTo(Windows.AfterNominations), "The veto sits in the window after the nominations (D2-M1).");
            var result = Apply(engine, EpisodeCommandKind.Compete, performance: 0.5);
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(result.state.npcSocial.beatsFired, Is.EqualTo(s.npcSocial.beatsFired), "Free: no tick, no beat.");
            Assert.That(Json(result.state.npcSocial.acts), Is.EqualTo(Json(s.npcSocial.acts)));
        }

        // ------------------------------------------------------------------ each close

        /// <summary>
        /// Over whole weeks: every step that ends a window - the nominations (the player's or the house's), the
        /// veto's use (the player's or a houseguest's), the campaign's close and the free time's - opens with the
        /// window's close. What a close of the state before the step fires is exactly what the step fired first:
        /// the same acts, and the same ledger rows under the same sequence numbers, so nothing of the step's own
        /// came before them; the window's plan is spent; and the doubled steps fire nothing more (D2-L1).
        /// </summary>
        [TestCase(6110u)] [TestCase(6111u)] [TestCase(6112u)]
        public void EachCloseFiresTheWindowsRestBeforeItsDecision(uint seed)
        {
            var engine = new EpisodeEngine(Fresh(8, seed));
            var closed = new Dictionary<string, int>();
            for (int i = 0; i < 4000 && engine.Snapshot.phase != EpisodePhase.Finished && engine.Snapshot.week <= 4; i++)
            {
                var s = engine.Snapshot;
                var command = Next(s);
                var result = engine.Apply(command);
                Assert.That(result.accepted, Is.True, result.reason);
                var after = result.state;
                string ends = Ends(s, after);
                if (ends == null || !EpisodeEngine.AllWeekOn(s) || EpisodeEngine.IsFirstNight(s)) continue;
                int window = EpisodeEngine.Window(s);
                var probe = s.Clone();
                EpisodeEngine.Close(probe);
                var fired = New(s, probe);
                closed[ends] = (closed.TryGetValue(ends, out int n) ? n : 0) + fired.Count;
                Assert.That(probe.npcSocial.beatsFired, Is.EqualTo(probe.npcSocial.beatPlan.Count));
                // The close's ledger rows are the step's first, under the same numbers.
                foreach (var row in Rows(s, probe))
                    Assert.That(Rows(s, after), Has.Member(row), ends + ": the close came first.");
                // Its acts are the step's, unless the week turned with it and cleared them.
                if (after.week == s.week)
                {
                    Assert.That(Json(New(s, after).Where(a => a.window == window).ToList()), Is.EqualTo(Json(fired)), ends + ": the close's acts.");
                    if (after.npcSocial.beatWeek == s.week && after.npcSocial.beatWindow == window)
                        Assert.That(after.npcSocial.beatsFired, Is.EqualTo(after.npcSocial.beatPlan.Count), ends + ": every beat fired.");
                }
                else Assert.That(after.npcSocial.acts, Is.Empty, "The week turned.");
                // The doubled steps: a second close of the window fires nothing.
                if (EpisodeEngine.Window(after) == window)
                {
                    var again = after.Clone();
                    EpisodeEngine.Close(again);
                    Assert.That(Json(again), Is.EqualTo(Json(after)), ends + ": a second close fires nothing.");
                }
            }
            foreach (string step in new[] { "nominations", "veto", "campaign", "free time" })
                Assert.That(closed.ContainsKey(step), Is.True, step);
            Assert.That(closed.Values.Sum(), Is.GreaterThan(0), "The closes fired beats.");
        }

        /// <summary>Which window a committed step ended, by what it decided; null for one that ended none.</summary>
        private static string Ends(EpisodeState before, EpisodeState after)
        {
            if (before.phase == EpisodePhase.Nomination && before.nominees.Count == 0 && after.nominees.Count == 2) return "nominations";
            if (before.phase == EpisodePhase.VetoMeeting && !before.vetoResolved && after.vetoResolved) return "veto";
            if (before.phase == EpisodePhase.Campaign && after.phase == EpisodePhase.Eviction) return "campaign";
            if (before.phase == EpisodePhase.Social && after.phase != EpisodePhase.Social && !EpisodeEngine.IsFirstNight(before)) return "free time";
            return null;
        }

        /// <summary>The ledger rows written since a state, by holder, other, sequence, type and words.</summary>
        private static List<string> Rows(EpisodeState before, EpisodeState after) =>
            after.relationships.SelectMany(r => r.events.Where(e => e.sequence >= before.nextSequence)
                .Select(e => r.fromId + "|" + r.toId + "|" + e.sequence + "|" + e.type + "|" + e.description)).ToList();

        [Test]
        public void CloseIsIdempotent()
        {
            var engine = new EpisodeEngine(Fresh(8, 6113));
            To(engine, x => x.phase == EpisodePhase.Campaign);
            var s = engine.Snapshot;
            var once = s.Clone(); EpisodeEngine.Close(once);
            Assert.That(once.npcSocial.beatsFired, Is.EqualTo(once.npcSocial.beatPlan.Count));
            var twice = once.Clone(); EpisodeEngine.Close(twice);
            Assert.That(Json(twice), Is.EqualTo(Json(once)), "An exhausted plan fires nothing.");
            var caught = once.Clone(); EpisodeEngine.CatchUp(caught);
            Assert.That(Json(caught), Is.EqualTo(Json(once)), "and neither does the catch-up.");
        }

        // ------------------------------------------------------------------ the player

        /// <summary>
        /// A close never reaches the player (D2-H3), however the house leans toward them - every houseguest spoiling
        /// for a fight with them, adoring them, or reading them as the biggest threat: the close's beats pick their
        /// fights, their talks and their rumours' subjects in the house, give no card, say no line to the player and
        /// move none of the player's arcs. The same beats at a tick reach the player.
        /// </summary>
        [TestCase(Fight)] [TestCase(Adore)] [TestCase(Gossip)]
        public void ACloseNeverReachesThePlayer(string temperament)
        {
            var s = Spoiling(EpisodePhase.Social, temperament);
            var closed = s.Clone(); EpisodeEngine.Close(closed);
            var fired = New(s, closed);
            Assert.That(fired, Is.Not.Empty, "The close fired.");
            Assert.That(fired.Any(a => a.partnerId == s.playerId || a.subjectId == s.playerId || a.actorId == s.playerId), Is.False, "Nobody was aimed at the player.");
            Assert.That(closed.replyCards.Count, Is.EqualTo(s.replyCards.Count), "No card.");
            Assert.That(closed.events.Any(e => e.sequence >= s.nextSequence && (e.audienceIds.Count == 0 || e.audienceIds.Contains(s.playerId))), Is.False, "No line the player hears.");
            Assert.That(Json(closed.relationshipArcs), Is.EqualTo(Json(s.relationshipArcs)), "No arc of the player's moved.");
            Assert.That(closed.randomState, Is.EqualTo(s.randomState));

            var ticked = s.Clone();
            ticked.windowActions[Windows.AfterEviction] = ticked.npcSocial.beatSeats;
            EpisodeEngine.CatchUp(ticked);
            var aimed = New(s, ticked).Where(a => a.partnerId == s.playerId || a.subjectId == s.playerId).ToList();
            Assert.That(aimed, Is.Not.Empty, "At a tick the same house reaches the player.");
            Assert.That(aimed.All(a => a.room == null), Is.True, "An act with the player in it is never staged (decision 9).");
            if (temperament == Fight) Assert.That(ticked.replyCards.Count, Is.GreaterThan(s.replyCards.Count), "and a fight puts its card to them, in free time.");
            Assert.That(ticked.randomState, Is.EqualTo(s.randomState), "Still nothing from the season's stream.");
        }

        /// <summary>
        /// The pursuit rung in a player-free beat: with the player at the head of the house, a houseguest's agenda is
        /// to court them; a close skips it and moves down the ladder, where a tick's beat courts the player.
        /// </summary>
        [Test]
        public void APlayerFreeBeatSkipsAPursuitOfThePlayer()
        {
            var s = InWindow(Fresh(8, 6145), Windows.AfterHoH);
            s.npcSocial.acts.Clear();
            s.hohId = s.playerId;
            var npc = s.Active.First(c => !c.isPlayer);
            foreach (var other in s.Active.Where(c => c.id != npc.id)) { Score(s, npc.id, other.id, 0); Score(s, other.id, npc.id, 0); }
            var agenda = NpcAgendas.Of(s, npc.id);
            Assert.That((agenda?.kind, agenda?.partnerId), Is.EqualTo((Agendas.Court, s.playerId)), "Precondition: courting the player.");
            // The week's repertoire already spent, and no pact or word to be had: only the pursuit is left.
            int k = 60;
            foreach (string kind in new[] { NpcActKinds.Talk, NpcActKinds.Meet, NpcActKinds.Rumour, NpcActKinds.Confront, NpcActKinds.Eavesdrop })
                s.npcSocial.acts.Add(new NpcActState { id = s.week + "-1-" + k++, kind = kind, actorId = npc.id, partnerId = s.Active.Last(c => !c.isPlayer).id, week = s.week, window = Windows.AfterNominations });
            var closing = NpcSocialActions.Beat(s.Clone(), npc.id, StoryRandom.Stream(s, "test:pursuit"), true, false);
            Assert.That(closing, Is.Null, "A close skips courting the player, and there is nothing below it.");
            var tick = NpcSocialActions.Beat(s.Clone(), npc.id, StoryRandom.Stream(s, "test:pursuit"), false, false);
            Assert.That((tick?.kind, tick?.partnerId), Is.EqualTo((NpcActKinds.Court, s.playerId)), "A tick courts them.");
        }

        /// <summary>A tick's card lives the whole window: the player's next word leaves it, and the week's turn clears it.</summary>
        [Test]
        public void ATickBeatsCardLivesToTheWindowsEnd()
        {
            var s = Spoiling(EpisodePhase.Social);
            var engine = new EpisodeEngine(s);
            var friend = s.Active.First(c => !c.isPlayer);
            var talked = Apply(engine, EpisodeCommandKind.Talk, friend.id);
            Assert.That(talked.accepted, Is.True, talked.reason);
            var cards = talked.state.replyCards.Where(c => c.kind == ReplyCards.Confrontation).Select(c => c.id).ToList();
            Assert.That(cards, Is.Not.Empty, "A beat at the tick confronted the player and put its card to them.");
            var again = Apply(engine, EpisodeCommandKind.Talk, friend.id);
            if (again.accepted)
                Assert.That(again.state.replyCards.Select(c => c.id), Is.SupersetOf(cards), "The next word leaves it standing.");
            To(engine, x => x.phase == EpisodePhase.HoH);
            Assert.That(engine.Snapshot.replyCards, Is.Empty, "The week's turn clears it.");
        }

        /// <summary>A card lives only in free time and the campaign: a beat after the HoH that confronts the player says its line, and puts no card.</summary>
        [Test]
        public void ABeatInAWindowNoCardLivesInSaysItsLineWithoutOne()
        {
            var s = Spoiling(EpisodePhase.Nomination);
            var ticked = s.Clone();
            ticked.windowActions[Windows.AfterHoH] = ticked.npcSocial.beatSeats;
            EpisodeEngine.CatchUp(ticked);
            Assert.That(New(s, ticked).Any(a => a.partnerId == s.playerId && a.kind == NpcActKinds.Confront), Is.True, "Somebody confronted the player.");
            Assert.That(ticked.events.Any(e => e.sequence >= s.nextSequence && e.kind == "confrontation" && e.audienceIds.Contains(s.playerId)), Is.True, "They heard it.");
            Assert.That(ticked.replyCards.Count, Is.EqualTo(s.replyCards.Count), "No card outside free time and the campaign.");
        }

        // ------------------------------------------------------------------ the season's stream

        /// <summary>
        /// At every step of a season, every beat left in the open window - fired as its close would, and fired
        /// at a tick as if the window's seats were spent - leaves the season's stream where it was; and a close's
        /// player-free beats move none of the player's arcs.
        /// </summary>
        [TestCase(6, 6114u)] [TestCase(12, 6115u)]
        public void NoBeatDrawsFromTheSeasonsStream(int size, uint seed)
        {
            var engine = new EpisodeEngine(Fresh(size, seed));
            int probed = 0;
            for (int i = 0; i < 4000 && engine.Snapshot.phase != EpisodePhase.Finished; i++)
            {
                var s = engine.Snapshot;
                int window = EpisodeEngine.Window(s);
                if (window != Windows.None && !EpisodeEngine.IsFirstNight(s))
                {
                    var closed = s.Clone(); EpisodeEngine.Close(closed);
                    Assert.That(closed.randomState, Is.EqualTo(s.randomState), "A close draws nothing.");
                    Assert.That(Json(closed.relationshipArcs), Is.EqualTo(Json(s.relationshipArcs)), "and moves no arc.");
                    var ticked = s.Clone();
                    while (ticked.windowActions.Count < Windows.Count) ticked.windowActions.Add(0);
                    ticked.windowActions[window] = 100;
                    EpisodeEngine.CatchUp(ticked);
                    Assert.That(ticked.randomState, Is.EqualTo(s.randomState), "A tick's beats draw nothing either.");
                    if (New(s, closed).Count + New(s, ticked).Count > 0) probed++;
                }
                var result = engine.Apply(Next(s));
                Assert.That(result.accepted, Is.True, result.reason);
            }
            Assert.That(probed, Is.GreaterThan(10));
        }

        // ------------------------------------------------------------------ the ladder

        /// <summary>
        /// Every week of whole seasons at eight and twelve: no houseguest past three beats (two on slop), one new
        /// pact for anybody, one word given, one court and one building, holding or hunting (a court as the
        /// nominations open leaves the week's building its own beat, as the weekly pass did), each repertoire kind
        /// once, each pact met once; the week's acts within its bound; and NPC-only pacts within the house's share.
        /// </summary>
        [TestCase(8, 6116u)] [TestCase(12, 6117u)]
        public void TheLaddersWeeklyLimitsHoldAllWeek(int size, uint seed)
        {
            var engine = new EpisodeEngine(Fresh(size, seed));
            int weeks = 0, beats = 0, pacts = 0, courtedAndPursued = 0;
            for (int i = 0; i < 4000 && engine.Snapshot.phase != EpisodePhase.Finished; i++)
            {
                var s = engine.Snapshot;
                var result = engine.Apply(Next(s));
                Assert.That(result.accepted, Is.True, result.reason);
                var after = result.state;
                var added = New(s, after);
                beats += added.Count(EpisodeEngine.IsBeat);
                // A pact among houseguests forms only with room in the house's share, and nobody is in two.
                if (added.Any(a => a.kind == NpcActKinds.Pact))
                {
                    pacts++;
                    var theirs = NpcAlliances.NpcOnlyPacts(after).Where(p => p.id.StartsWith("alliance-npc-", StringComparison.Ordinal)).ToList();
                    Assert.That(theirs.Count, Is.LessThanOrEqualTo(NpcAlliances.PactCap(after)), "within the share");
                    Assert.That(after.Active.Any(c => !c.isPlayer && theirs.Count(p => p.members.Contains(c.id)) > 1), Is.False, "nobody in two");
                }
                // The week as it closes: every limit held over all of it.
                var week = after.week == s.week ? after : s;
                if (after.week != s.week) weeks++;
                var acts = week.npcSocial.acts;
                Assert.That(acts.Count, Is.LessThanOrEqualTo(EpisodeEngine.MostActs(week)));
                foreach (var npc in week.contestants.Where(c => !c.isPlayer))
                {
                    var mine = acts.Where(a => a.actorId == npc.id).ToList();
                    Assert.That(mine.Count(EpisodeEngine.IsBeat), Is.LessThanOrEqualTo(EpisodeEngine.BeatsAWeek), npc.id);
                    Assert.That(acts.Count(a => a.kind == NpcActKinds.Pact && (a.actorId == npc.id || a.partnerId == npc.id)), Is.LessThanOrEqualTo(1), "one new pact");
                    Assert.That(mine.Count(a => a.kind == NpcActKinds.Promise), Is.LessThanOrEqualTo(1), "one word");
                    Assert.That(mine.Count(a => a.kind == NpcActKinds.Court), Is.LessThanOrEqualTo(1), "one court");
                    Assert.That(mine.Count(a => NpcActKinds.IsPursuit(a.kind) && a.kind != NpcActKinds.Court), Is.LessThanOrEqualTo(1), "one building, holding or hunting");
                    if (after.week != s.week && mine.Any(a => a.kind == NpcActKinds.Court) && mine.Any(a => NpcActKinds.IsPursuit(a.kind) && a.kind != NpcActKinds.Court))
                        courtedAndPursued++;
                    foreach (string kind in new[] { NpcActKinds.Talk, NpcActKinds.Meet, NpcActKinds.Rumour, NpcActKinds.Confront, NpcActKinds.Eavesdrop })
                        Assert.That(mine.Count(a => a.kind == kind), Is.LessThanOrEqualTo(1), kind);
                }
                Assert.That(acts.Where(a => a.kind == NpcActKinds.Meet).GroupBy(a => a.subjectId).All(g => g.Count() == 1), Is.True, "each pact met once");
            }
            Assert.That(weeks, Is.GreaterThan(2));
            Assert.That(beats, Is.GreaterThan(weeks * 3), "The house played its beats.");
            Assert.That(courtedAndPursued, Is.GreaterThan(0), "Somebody courted a Head of Household and still built, held or hunted that week.");
        }

        /// <summary>
        /// Courting the Head of Household is a pursuit of its own (acceptance (b)'s drift): the weekly pass courted
        /// as the nominations opened and still built, held or hunted on its own turn. A court leaves the week's
        /// building, holding or hunting to a beat, which spends it; and a houseguest still courting does not court twice.
        /// </summary>
        [Test]
        public void ACourtLeavesTheWeeksBuildingToItsOwnBeat()
        {
            // After the veto the power is spent and the agendas are the house's own again.
            var s = InWindow(Fresh(8, 6146), Windows.AfterVeto);
            s.npcSocial.acts.Clear();
            var npc = s.Active.FirstOrDefault(c => !c.isPlayer && c.id != s.hohId && !s.nominees.Contains(c.id) && Pursues(s, c.id));
            Assert.That(npc, Is.Not.Null, "Precondition: a houseguest building, holding or hunting.");
            var agenda = NpcAgendas.Of(s, npc.id);
            string pursuit = agenda.kind == Agendas.Build ? NpcActKinds.Build : agenda.kind == Agendas.Hold ? NpcActKinds.Hold : NpcActKinds.Hunt;
            // This week's new pact and word already had, so the pursuit is the ladder's next rung; and the court.
            string other = s.Active.First(c => !c.isPlayer && c.id != npc.id).id;
            Spent(s, npc.id, other);
            s.npcSocial.acts.Add(new NpcActState { id = s.week + "-0-court-" + npc.id, kind = NpcActKinds.Court, actorId = npc.id, partnerId = s.hohId, week = s.week, window = Windows.AfterHoH });
            Assert.That(NpcSocialActions.PursuitSpent(s, npc.id), Is.False, "A court is not the week's building.");
            var beat = NpcSocialActions.Beat(s.Clone(), npc.id, StoryRandom.Stream(s, "test:court"), true, false);
            Assert.That((beat?.kind, beat?.partnerId), Is.EqualTo((pursuit, agenda.partnerId)), "The beat pursues the agenda.");
            s.npcSocial.acts.Add(new NpcActState { id = s.week + "-2-62", kind = pursuit, actorId = npc.id, partnerId = agenda.partnerId, week = s.week, window = Windows.AfterVeto });
            Assert.That(NpcSocialActions.PursuitSpent(s, npc.id), Is.True, "Once a week.");
            var next = NpcSocialActions.Beat(s.Clone(), npc.id, StoryRandom.Stream(s, "test:court"), true, false);
            Assert.That(next == null || !NpcActKinds.IsPursuit(next.kind), Is.True, "No second pursuit: " + next?.kind);

            // While the power is live, a houseguest who courted as the nominations opened does not court again.
            var live = InWindow(Fresh(8, 6146), Windows.AfterHoH);
            live.npcSocial.acts.Clear();
            var suitor = live.Active.FirstOrDefault(c => !c.isPlayer && c.id != live.hohId && NpcAgendas.Of(live, c.id)?.kind == Agendas.Court);
            Assert.That(suitor, Is.Not.Null, "Precondition: somebody courting the Head of Household.");
            Assert.That(NpcSocialActions.PursuitSpent(live, suitor.id), Is.False, "Not yet.");
            Spent(live, suitor.id, live.Active.First(c => !c.isPlayer && c.id != suitor.id && c.id != live.hohId).id);
            live.npcSocial.acts.Add(new NpcActState { id = live.week + "-0-court-" + suitor.id, kind = NpcActKinds.Court, actorId = suitor.id, partnerId = live.hohId, week = live.week, window = Windows.AfterHoH });
            Assert.That(NpcSocialActions.PursuitSpent(live, suitor.id), Is.True, "The court was the week's.");
            var again = NpcSocialActions.Beat(live.Clone(), suitor.id, StoryRandom.Stream(live, "test:court"), true, false);
            Assert.That(again == null || !NpcActKinds.IsPursuit(again.kind), Is.True, "No second court: " + again?.kind);
        }

        /// <summary>A houseguest building, holding or hunting toward somebody in the house other than the player, with work left in it.</summary>
        private static bool Pursues(EpisodeState s, string npcId)
        {
            var agenda = NpcAgendas.Of(s, npcId);
            return agenda != null && (agenda.kind == Agendas.Build || agenda.kind == Agendas.Hold || agenda.kind == Agendas.Hunt)
                && agenda.partnerId != null && agenda.partnerId != s.playerId && NpcAgendas.StillWorking(s, npcId, agenda)
                && (agenda.kind != Agendas.Hunt || (agenda.targetId != s.playerId && s.Find(agenda.targetId)?.status == ContestantStatus.Active));
        }

        /// <summary>This week's new pact and word already had by a houseguest: the ladder's first two rungs spent.</summary>
        private static void Spent(EpisodeState s, string npcId, string otherId)
        {
            s.npcSocial.acts.Add(new NpcActState { id = s.week + "-0-60", kind = NpcActKinds.Pact, actorId = npcId, partnerId = otherId, week = s.week, window = Windows.AfterHoH });
            s.npcSocial.acts.Add(new NpcActState { id = s.week + "-1-61", kind = NpcActKinds.Promise, actorId = npcId, partnerId = otherId, week = s.week, window = Windows.AfterNominations });
        }

        /// <summary>A houseguest named a Have-Not mid-week, with two beats already, has no third (the quota).</summary>
        [Test]
        public void AHaveNotNamedMidWeekStopsAtTwoBeats()
        {
            foreach (uint seed in new[] { 6118u, 6141u, 6142u, 6143u })
            {
                var engine = new EpisodeEngine(Fresh(8, seed));
                for (int i = 0; i < 2000 && engine.Snapshot.phase != EpisodePhase.Finished; i++)
                {
                    var s = engine.Snapshot;
                    int window = EpisodeEngine.Window(s);
                    if (window != Windows.None && s.npcSocial.beatWindow == window && !EpisodeEngine.IsFirstNight(s))
                    {
                        var plain = s.Clone(); EpisodeEngine.Close(plain);
                        // Somebody off slop on their second beat whose close would have given them a third.
                        string npc = s.npcSocial.beatPlan.Skip(s.npcSocial.beatsFired)
                            .FirstOrDefault(id => !s.haveNots.Contains(id) && EpisodeEngine.BeatsThisWeek(s, id) == 2 && EpisodeEngine.BeatsThisWeek(plain, id) == 3);
                        if (npc != null)
                        {
                            var slop = s.Clone();
                            slop.haveNots.Add(npc);
                            Assert.That(EpisodeEngine.BeatQuota(slop, npc), Is.EqualTo(2));
                            EpisodeEngine.Close(slop);
                            Assert.That(EpisodeEngine.BeatsThisWeek(slop, npc), Is.EqualTo(2), "No third beat on slop.");
                            Assert.That(slop.npcSocial.beatsFired, Is.EqualTo(slop.npcSocial.beatPlan.Count), "The slot still counts.");
                            return;
                        }
                    }
                    Assert.That(engine.Apply(Next(s)).accepted, Is.True);
                }
            }
            Assert.Fail("No houseguest on a second beat with a third to come.");
        }

        /// <summary>The quota is a hard check on the week's beats, whatever the plan says: a fourth is never fired; courts and campaign visits do not count.</summary>
        [Test]
        public void TheQuotaStopsAFourthBeatAndRecordsDoNotCount()
        {
            var s = InWindow(Fresh(8, 6119), Windows.AfterVeto);
            s.npcSocial.acts.Clear();
            string npc = s.contestants.First(c => !c.isPlayer && c.status == ContestantStatus.Active).id;
            string other = s.contestants.Last(c => !c.isPlayer && c.status == ContestantStatus.Active).id;
            for (int k = 0; k < 3; k++)
                s.npcSocial.acts.Add(new NpcActState { id = s.week + "-0-" + (40 + k), kind = NpcActKinds.Talk, actorId = npc, partnerId = other, week = s.week, window = Windows.AfterHoH });
            s.npcSocial.acts.Add(new NpcActState { id = s.week + "-0-court-" + npc, kind = NpcActKinds.Court, actorId = npc, partnerId = other, week = s.week, window = Windows.AfterHoH });
            Assert.That(EpisodeEngine.BeatsThisWeek(s, npc), Is.EqualTo(3), "The court is not a beat.");
            Assert.That(EpisodeEngine.IsBeat(s.npcSocial.acts.Last()), Is.False);
            // Force the houseguest into the window's plan.
            EpisodeEngine.Close(s);
            var planned = s.Clone();
            planned.npcSocial.beatsFired = 0; planned.npcSocial.beatPlan = new List<string> { npc };
            planned.npcSocial.acts.RemoveAll(a => a.window == Windows.AfterVeto);
            EpisodeEngine.Close(planned);
            Assert.That(EpisodeEngine.BeatsThisWeek(planned, npc), Is.EqualTo(3), "No fourth.");
            Assert.That(planned.npcSocial.beatsFired, Is.EqualTo(1));
        }

        /// <summary>
        /// The ladder's pact rung is tried at every beat until it lands, and the word once a week: a houseguest
        /// with nobody to pair with gives their word, then - a partner warm at last - forms a pact at their next
        /// beat, and gives no second word that week.
        /// </summary>
        [Test]
        public void ThePactRungIsTriedAtEveryBeatAndTheWordOnceAWeek()
        {
            var s = InWindow(Fresh(8, 6120), Windows.AfterHoH);
            s.npcSocial.acts.Clear();
            s.agencyRulesStartWeek = 0;
            var npcs = s.Active.Where(c => !c.isPlayer).ToList();
            var a = npcs[0]; var b = npcs[1]; var c = npcs[2];
            foreach (var x in npcs) foreach (var y in npcs.Where(y => y != x)) Score(s, x.id, y.id, 0);
            foreach (var x in s.alliances) x.active = false;
            // Warm enough for a word, not a pact: the word.
            Score(s, a.id, c.id, 35); Score(s, c.id, a.id, 0);
            var first = NpcSocialActions.Beat(s, a.id, StoryRandom.Stream(s, "test:1"), false, false);
            Assert.That(first?.kind, Is.EqualTo(NpcActKinds.Promise), "No pact to be had: a word.");
            first.id = s.week + "-0-50"; first.week = s.week; first.window = Windows.AfterHoH; s.npcSocial.acts.Add(first);
            // Now warm both ways with b: the pact rung, tried again, lands.
            Score(s, a.id, b.id, 90); Score(s, b.id, a.id, 90);
            var second = NpcSocialActions.Beat(s, a.id, StoryRandom.Stream(s, "test:2"), false, false);
            Assert.That(second?.kind, Is.EqualTo(NpcActKinds.Pact));
            Assert.That(second.partnerId, Is.EqualTo(b.id));
            second.id = s.week + "-0-51"; second.week = s.week; second.window = Windows.AfterHoH; s.npcSocial.acts.Add(second);
            // Still warm enough for another word, but the week's word is given; and the pact is this week's.
            Score(s, a.id, npcs[3].id, 60);
            var third = NpcSocialActions.Beat(s, a.id, StoryRandom.Stream(s, "test:3"), false, false);
            Assert.That(third == null || (third.kind != NpcActKinds.Promise && third.kind != NpcActKinds.Pact), Is.True, third?.kind);
        }

        // ------------------------------------------------------------------ the week

        [Test]
        public void TheWeeksActsAreClearedAsItTurns()
        {
            var engine = new EpisodeEngine(Fresh(8, 6121));
            To(engine, x => x.week == 2 && x.phase == EpisodePhase.Social);
            var s = engine.Snapshot;
            Assert.That(s.npcSocial.acts, Is.Not.Empty, "The week acted.");
            To(engine, x => x.phase == EpisodePhase.HoH);
            Assert.That(engine.Snapshot.week, Is.EqualTo(3));
            Assert.That(engine.Snapshot.npcSocial.acts, Is.Empty, "The week turned.");
        }

        /// <summary>
        /// As the social week opens under the rules the weekly pass is gone: what has soured falls apart, and
        /// every pact formed and word given in the step is one the beats after it recorded (Settle would give
        /// words in a warm house with nothing recorded).
        /// </summary>
        [Test]
        public void UnderTheRulesTheSocialWeekOpensWithDissolveAlone()
        {
            var engine = new EpisodeEngine(Fresh(8, 6122));
            To(engine, x => x.phase == EpisodePhase.Eviction && x.evictionResolved);
            var s = engine.Snapshot;
            foreach (var x in s.Active.Where(c => !c.isPlayer)) foreach (var y in s.Active.Where(c => !c.isPlayer && c.id != x.id)) Score(s, x.id, y.id, 45);
            var warm = new EpisodeEngine(s);
            var result = warm.Apply(Next(s));
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(result.state.phase, Is.EqualTo(EpisodePhase.Social));
            var after = result.state;
            var acts = New(s, after);
            int words = CommitmentReferences.Promises(after).Count(p => p.id.StartsWith("promise-npc-", StringComparison.Ordinal))
                - CommitmentReferences.Promises(s).Count(p => p.id.StartsWith("promise-npc-", StringComparison.Ordinal));
            Assert.That(words, Is.EqualTo(acts.Count(a => a.kind == NpcActKinds.Promise)), "Every word given is a beat's.");
            int pacts = after.alliances.Count(a => a.id.StartsWith("alliance-npc-", StringComparison.Ordinal) && !s.alliances.Any(b => b.id == a.id));
            Assert.That(pacts, Is.EqualTo(acts.Count(a => a.kind == NpcActKinds.Pact)), "Every pact formed is a beat's.");
            Assert.That(acts.All(a => a.window == Windows.AfterEviction && a.firedTick == 0), Is.True);
            Assert.That(after.npcSocial.beatsFired, Is.EqualTo(EpisodeEngine.Due(after.npcSocial.beatPlan.Count, after.npcSocial.beatSeats, 0)));

            var without = s.Clone(); without.allWeekRulesStartWeek = 0;
            without.npcSocial.acts.Clear(); without.npcSocial.beatPlan.Clear(); without.npcSocial.beatWindow = Windows.None;
            without.npcSocial.beatWeek = without.npcSocial.beatsFired = without.npcSocial.beatSeats = 0;
            var settled = new EpisodeEngine(without).Apply(Next(without));
            Assert.That(settled.accepted, Is.True, settled.reason);
            int settleWords = CommitmentReferences.Promises(settled.state).Count(p => p.id.StartsWith("promise-npc-", StringComparison.Ordinal))
                - CommitmentReferences.Promises(without).Count(p => p.id.StartsWith("promise-npc-", StringComparison.Ordinal));
            Assert.That(settleWords, Is.GreaterThan(words), "Control: the weekly pass gives more words at once in the same warm house.");
        }

        /// <summary>A court as the nominations open and a nominee's campaign visits are the week's acts, recorded with no draw, never counted as beats.</summary>
        [Test]
        public void CourtsAndCampaignVisitsAreRecordedNotCounted()
        {
            int courts = 0, visits = 0;
            foreach (uint seed in new[] { 6123u, 6124u, 6125u })
            {
                var engine = new EpisodeEngine(Fresh(8, seed));
                for (int i = 0; i < 3000 && engine.Snapshot.phase != EpisodePhase.Finished && engine.Snapshot.week <= 3; i++)
                {
                    var s = engine.Snapshot;
                    var result = engine.Apply(Next(s));
                    Assert.That(result.accepted, Is.True, result.reason);
                    foreach (var act in New(s, result.state).Where(a => !EpisodeEngine.IsBeat(a)))
                    {
                        if (act.kind == NpcActKinds.Court)
                        {
                            courts++;
                            Assert.That(act.id, Is.EqualTo(s.week + "-0-court-" + act.actorId));
                            Assert.That(act.partnerId, Is.EqualTo(result.state.hohId));
                            Assert.That(act.room, Is.EqualTo(act.partnerId == s.playerId ? null : "HoH"));
                            Assert.That(act.window, Is.EqualTo(Windows.AfterHoH));
                        }
                        else
                        {
                            Assert.That(act.kind, Is.EqualTo(NpcActKinds.Campaign));
                            visits++;
                            Assert.That(act.id, Is.EqualTo(s.week + "-2-campaign-" + act.actorId + "-" + act.partnerId));
                            Assert.That(result.state.nominees, Has.Member(act.actorId));
                            Assert.That(act.window, Is.EqualTo(Windows.AfterVeto));
                        }
                    }
                }
            }
            Assert.That(courts, Is.GreaterThan(0), "Somebody courted a Head of Household.");
            Assert.That(visits, Is.GreaterThan(0), "Nominees campaigned.");
            var probe = InWindow(Fresh(8, 6126), Windows.AfterVeto);
            uint random = probe.randomState;
            EpisodeEngine.RecordAct(probe, NpcActKinds.Campaign, probe.week + "-2-campaign-x-y", probe.contestants[1].id, probe.contestants[2].id, null);
            Assert.That(probe.randomState, Is.EqualTo(random), "A record draws nothing.");
            int count = probe.npcSocial.acts.Count;
            EpisodeEngine.RecordAct(probe, NpcActKinds.Campaign, probe.week + "-2-campaign-x-y", probe.contestants[1].id, probe.contestants[2].id, null);
            Assert.That(probe.npcSocial.acts.Count, Is.EqualTo(count), "Kept once.");
        }

        [Test]
        public void TheFinalThreesFreeTimePlaysItsBeatsAndTheFinaleNone()
        {
            var engine = new EpisodeEngine(Fresh(6, 6127));
            To(engine, x => x.phase == EpisodePhase.Social && x.Active.Count() == 3 && x.evictionResolved, 4000);
            var s = engine.Snapshot;
            Assert.That(s.npcSocial.beatWindow, Is.EqualTo(Windows.AfterEviction));
            Assert.That(s.npcSocial.beatWeek, Is.EqualTo(s.week));
            To(engine, x => x.phase == EpisodePhase.FinalHoHPart1);
            var finale = engine.Snapshot;
            Assert.That(finale.npcSocial.acts, Is.Empty);
            for (int i = 0; i < 200 && engine.Snapshot.phase != EpisodePhase.Finished; i++)
            {
                var at = engine.Snapshot;
                var result = engine.Apply(Next(at));
                Assert.That(result.accepted, Is.True, result.reason);
                Assert.That(result.state.npcSocial.acts, Is.Empty, "The finale has no windows: no beats.");
            }
        }

        // ------------------------------------------------------------------ the commit

        /// <summary>The status marker: where the house's catch-up began in the commit, so the status line names the step (decision 10).</summary>
        [Test]
        public void TheStatusMarkerIsWhereTheHousesCatchUpBegan()
        {
            var engine = new EpisodeEngine(Fresh(8, 6128));
            To(engine, x => x.phase == EpisodePhase.HoH && x.competitionResolved);
            var s = engine.Snapshot;
            var result = engine.Apply(Next(s));
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(result.state.phase, Is.EqualTo(EpisodePhase.Nomination), "The Head of Household is crowned and the window opens.");
            Assert.That(result.beatsFromSequence, Is.GreaterThanOrEqualTo(s.nextSequence));
            Assert.That(result.beatsFromSequence, Is.LessThanOrEqualTo(result.state.nextSequence));
            Assert.That(result.state.events.Any(e => e.sequence < result.beatsFromSequence && e.sequence >= s.nextSequence && e.kind == "phase"), Is.True,
                "The step's own lines come before the marker.");
            var off = s.Clone(); off.allWeekRulesStartWeek = 0;
            off.npcSocial.acts.Clear(); off.npcSocial.beatPlan.Clear(); off.npcSocial.beatWindow = Windows.None;
            off.npcSocial.beatWeek = off.npcSocial.beatsFired = off.npcSocial.beatSeats = 0;
            Assert.That(new EpisodeEngine(off).Apply(Next(off)).beatsFromSequence, Is.Zero, "No catch-up, no marker.");
        }

        /// <summary>
        /// A proposal refused with a counter on the table (C7) catches the house up without a word to the player,
        /// which would lapse it (D2's decision 6): a house spoiling for a fight with the player leaves it standing.
        /// </summary>
        [Test]
        public void ACounterOnTheTableIsLeftStanding()
        {
            var basis = Spoiling(EpisodePhase.Campaign);
            var npc = basis.Active.First(c => !c.isPlayer && !basis.nominees.Contains(c.id));
            Score(basis, npc.id, basis.playerId, 30); Score(basis, basis.playerId, npc.id, 30);
            for (int attempt = 0; attempt < 600; attempt++)
            {
                var s = basis.Clone();
                s.nextSequence += attempt;
                s.randomState = (uint)(attempt * 2654435761u + 7u);
                var result = Apply(new EpisodeEngine(s), EpisodeCommandKind.ProposeDeal, npc.id, null, DealKind.Partnership);
                if (!result.accepted || !result.state.events.Any(e => e.sequence >= s.nextSequence && e.kind == Negotiation.CounterEventKind)) continue;
                Assert.That(result.beatsFromSequence, Is.GreaterThan(0), "The house caught up.");
                Assert.That(Negotiation.OpenCounter(result.state, npc.id), Is.Not.Null, "The counter stands.");
                Assert.That(New(s, result.state).Any(a => a.partnerId == s.playerId || a.subjectId == s.playerId), Is.False, "Player-free.");
                return;
            }
            Assert.Fail("No refusal came back as a counter.");
        }

        /// <summary>
        /// A word on the vote or for safety belongs to the window the block is set in (D2's decision 7): a nominee's
        /// beat before the veto offers only loyalty or a final two; after it, the vote.
        /// </summary>
        [Test]
        public void PositionalWordsWaitForTheVeto()
        {
            var engine = new EpisodeEngine(Fresh(8, 6129));
            To(engine, x => x.phase == EpisodePhase.VetoSelection);
            var s = engine.Snapshot;
            string nominee = s.nominees.First(id => id != s.playerId);
            string voter = s.Active.First(c => !c.isPlayer && !s.nominees.Contains(c.id)).id;
            Assert.That(NpcPromises.Offer(s, nominee, voter), Is.EqualTo(PromiseKind.Vote), "Precondition: on the block, the word is the vote.");
            Assert.That(NpcPromises.Offer(s, nominee, voter, false), Is.Not.EqualTo(PromiseKind.Vote));
            Assert.That(NpcPromises.Offer(s, voter, s.hohId, false), Is.Not.EqualTo(PromiseKind.Safety));
            var before = s.Clone();
            Assert.That(NpcPromises.TryGive(before, nominee, false, out string toId) && CommitmentReferences.Promises(before).Any(p => p.fromId == nominee && p.toId == toId
                && (p.kind == PromiseKind.Vote || p.kind == PromiseKind.Safety)), Is.False);
            int weekWords = 0;
            for (int i = 0; i < 400 && engine.Snapshot.phase != EpisodePhase.Eviction; i++)
            {
                var at = engine.Snapshot;
                var result = engine.Apply(Next(at));
                Assert.That(result.accepted, Is.True, result.reason);
                foreach (var act in New(at, result.state).Where(a => a.kind == NpcActKinds.Promise))
                {
                    var word = CommitmentReferences.Promises(result.state).LastOrDefault(p => p.fromId == act.actorId && p.toId == act.partnerId && p.week == at.week);
                    Assert.That(word, Is.Not.Null, "A word recorded is a word given.");
                    if (act.window != Windows.AfterVeto)
                        Assert.That(word.kind == PromiseKind.Vote || word.kind == PromiseKind.Safety, Is.False, "window " + act.window + ": " + word.kind);
                    weekWords++;
                }
            }
            Assert.That(engine.Snapshot.phase, Is.EqualTo(EpisodePhase.Eviction));
        }

        // ------------------------------------------------------------------ whole seasons

        /// <summary>The sixteen-person house on the combined roster with the shipped rules and the beats: every candidate validated, every week's acts within the bound.</summary>
        [Test]
        public void ASixteenPersonHouseStaysInsideTheWeeksBound()
        {
            var s = SeasonBuilder.CreateVerificationStressHouse(16, 6130);
            ShippedRules.ApplyFresh(s);
            EpisodeEngine.EnableAllWeek(s);
            var engine = new EpisodeEngine(s);
            int most = 0;
            for (int i = 0; i < 8000 && engine.Snapshot.phase != EpisodePhase.Finished; i++)
            {
                var at = engine.Snapshot;
                var result = engine.Apply(Next(at));
                Assert.That(result.accepted, Is.True, "week " + at.week + " " + at.phase + ": " + result.reason);
                most = Math.Max(most, result.state.npcSocial.acts.Count);
                Assert.That(result.state.npcSocial.acts.Count, Is.LessThanOrEqualTo(EpisodeEngine.MostActs(result.state)));
            }
            Assert.That(engine.Snapshot.phase, Is.EqualTo(EpisodePhase.Finished));
            Assert.That(most, Is.GreaterThan(16), "A big house's week holds many acts.");
            Assert.That(EpisodeEngine.MostActs(s), Is.EqualTo(64));
        }

        /// <summary>A fresh season as the director starts one - the hearing rules on - with the beats from week one plays to its end, every command legal.</summary>
        [TestCase(8, 6131u)]
#if !UNITY_5_3_OR_NEWER
        [TestCase(6, 6132u)] [TestCase(12, 6133u)]
#endif
        public void AFreshSeasonUnderTheHearingRulesPlaysTheBeatsToItsEnd(int size, uint seed)
        {
            var s = Fresh(size, seed);
            Assert.That(UnifiedCommitmentHearings.RulesOn(s), Is.True, "The hearing rules are on.");
            var engine = new EpisodeEngine(s);
            int acts = 0;
            for (int i = 0; i < 6000 && engine.Snapshot.phase != EpisodePhase.Finished; i++)
            {
                var at = engine.Snapshot;
                var result = engine.Apply(Next(at));
                Assert.That(result.accepted, Is.True, "week " + at.week + " " + at.phase + ": " + result.reason);
                acts += New(at, result.state).Count;
            }
            Assert.That(engine.Snapshot.phase, Is.EqualTo(EpisodePhase.Finished));
            Assert.That(acts, Is.GreaterThan(size * 2));
        }

        /// <summary>
        /// Acceptance (c) (D2-M5): with no NPC world driving the clock, the same commands replay to the same
        /// state, and a season saved and loaded at every command plays on to exactly the same state.
        /// </summary>
        [Test]
        public void TheSameCommandsReplayToTheSameStateThroughASaveAtEveryCommand()
        {
            var start = Fresh(8, 6134);
            var engine = new EpisodeEngine(start);
            var commands = new List<EpisodeCommand>();
            var states = new List<string>();
            for (int i = 0; i < 1500 && engine.Snapshot.phase != EpisodePhase.Finished && engine.Snapshot.week <= 3; i++)
            {
                var command = Next(engine.Snapshot);
                var result = engine.Apply(command);
                Assert.That(result.accepted, Is.True, result.reason);
                commands.Add(command); states.Add(Json(result.state));
            }
            var replay = new EpisodeEngine(start);
            for (int i = 0; i < commands.Count; i++)
            {
                Assert.That(Json(replay.Apply(commands[i]).state), Is.EqualTo(states[i]), "replay at " + i);
                var loaded = new EpisodeEngine(Load(i == 0 ? Json(start) : states[i - 1]));
                Assert.That(Json(loaded.Apply(commands[i]).state), Is.EqualTo(states[i]), "through a save at " + i);
            }
            Assert.That(commands.Count, Is.GreaterThan(30));
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>A fresh season as the director starts one, at a size, with the all-week beats from week one.</summary>
        internal static EpisodeState Fresh(int size, uint seed)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size }, seed);
            ShippedRules.ApplyFresh(s);
            EpisodeEngine.EnableAllWeek(s);
            Valid(s);
            return s;
        }

        /// <summary>How the house leans toward the player in <see cref="Spoiling"/>.</summary>
        private const string Fight = "fight", Adore = "adore", Gossip = "gossip";

        /// <summary>
        /// A fresh season walked to its second week's window of this phase, the house leaning one way toward the
        /// player: spoiling for a fight with them and indifferent to each other, leading with a fight
        /// (<see cref="Fight"/>); adoring them and cold to each other, leading with a talk, their agendas free to
        /// point at the player (<see cref="Adore"/>); or indifferent all round, leading with a rumour, the player
        /// the house's biggest threat (<see cref="Gossip"/>, no longer a lawful season, for direct calls only).
        /// No pacts or words to be had; the window's plan made, none of it fired yet.
        /// </summary>
        private static EpisodeState Spoiling(EpisodePhase phase, string temperament = Fight)
        {
            EpisodeState s = null;
            // The first of these seasons whose player is still in the house by then.
            for (uint seed = 6144; seed < 6164 && (s == null || s.Find(s.playerId).status != ContestantStatus.Active); seed++)
            {
                var engine = new EpisodeEngine(Fresh(8, seed));
                To(engine, x => x.week == 2 && x.phase == phase && (phase != EpisodePhase.Nomination || x.nominees.Count == 0));
                s = engine.Snapshot;
            }
            Assert.That(s.Find(s.playerId).status, Is.EqualTo(ContestantStatus.Active), "Precondition: the player is in the house.");
            int window = EpisodeEngine.Window(s);
            // A fresh ladder: none of the week's beats spent yet (its courts and visits stand).
            s.npcSocial.acts.RemoveAll(a => a.window == window || EpisodeEngine.IsBeat(a));
            if (temperament == Gossip) { s.Find(s.playerId).hohWins = 5; s.Find(s.playerId).vetoWins = 5; }
            // Indifferent to each other: no grudge between houseguests, which a fight would pick before the player.
            if (temperament == Fight) s.story?.grudges?.RemoveAll(g => g.targetId != s.playerId);
            foreach (var c in s.contestants.Where(c => !c.isPlayer))
            {
                c.traits = new List<string> { temperament == Fight ? "Confrontational" : temperament == Adore ? "Social" : "Sneaky" };
                Score(s, c.id, s.playerId, temperament == Fight ? -60 : temperament == Adore ? 100 : 0);
                foreach (var o in s.contestants.Where(o => !o.isPlayer && o.id != c.id)) Score(s, c.id, o.id, temperament == Adore ? -60 : 5);
                // The week's pursuits already spent - a court as the nominations opened, and the week's building -
                // so no agenda takes a beat, except where the house adores the player and its agendas may point at them.
                if (temperament != Adore && c.status == ContestantStatus.Active)
                {
                    string partner = s.contestants.First(o => !o.isPlayer && o.id != c.id).id;
                    if (!s.npcSocial.acts.Any(a => a.actorId == c.id && a.kind == NpcActKinds.Court))
                        s.npcSocial.acts.Add(new NpcActState { id = s.week + "-0-court-" + c.id, kind = NpcActKinds.Court, actorId = c.id,
                            partnerId = partner, week = s.week, window = Windows.AfterHoH });
                    if (!s.npcSocial.acts.Any(a => a.actorId == c.id && NpcActKinds.IsPursuit(a.kind) && a.kind != NpcActKinds.Court))
                        s.npcSocial.acts.Add(new NpcActState { id = s.week + "-0-build-" + c.id, kind = NpcActKinds.Build, actorId = c.id,
                            partnerId = partner, week = s.week, window = Windows.AfterHoH });
                }
            }
            s.replyCards.Clear();
            s.npcSocial.beatsFired = 0;
            Assert.That(s.npcSocial.beatWindow, Is.EqualTo(window));
            Assert.That(s.npcSocial.beatPlan, Is.Not.Empty);
            return s;
        }

        /// <summary>A copy of a season in a window's phase - a plan to be made - for a direct call; no command, no validation.</summary>
        private static EpisodeState InWindow(EpisodeState basis, int window)
        {
            var s = basis.Clone();
            var npcs = s.Active.Where(c => !c.isPlayer).ToList();
            s.hohId = npcs[0].id;
            switch (window)
            {
                case Windows.AfterHoH: s.phase = EpisodePhase.Nomination; s.nominees.Clear(); break;
                case Windows.AfterNominations: s.phase = EpisodePhase.VetoMeeting; s.nominees = new List<string> { npcs[1].id, npcs.Last().id }; break;
                case Windows.AfterVeto: s.phase = EpisodePhase.Campaign; s.nominees = new List<string> { npcs[1].id, npcs.Last().id }; s.vetoResolved = true; break;
                default: s.phase = EpisodePhase.Social; s.evictionResolved = true; break;
            }
            s.npcSocial.beatWindow = Windows.None; s.npcSocial.beatWeek = 0; s.npcSocial.beatsFired = 0; s.npcSocial.beatSeats = 0; s.npcSocial.beatPlan.Clear();
            Assert.That(EpisodeEngine.Window(s), Is.EqualTo(window));
            return s;
        }

        /// <summary>The acts a step added.</summary>
        private static List<NpcActState> New(EpisodeState before, EpisodeState after) =>
            after.npcSocial.acts.Where(a => !before.npcSocial.acts.Any(b => b.id == a.id && b.week == a.week)).ToList();

        private static void To(EpisodeEngine engine, Func<EpisodeState, bool> until, int most = 3000, bool allowFinish = false)
        {
            for (int i = 0; i < most && !until(engine.Snapshot); i++)
            {
                if (engine.Snapshot.phase == EpisodePhase.Finished)
                {
                    if (allowFinish) return;
                    Assert.Fail("The season ended first.");
                }
                var result = engine.Apply(Next(engine.Snapshot));
                Assert.That(result.accepted, Is.True, result.reason);
            }
            if (!allowFinish) Assert.That(until(engine.Snapshot), Is.True, "Reached.");
        }

        /// <summary>The engine tests' next lawful command, answering a finale question with an offered response.</summary>
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

        private static CommandResult Apply(EpisodeEngine engine, EpisodeCommandKind kind, string target = null, string second = null, string text = null, double performance = 0)
        {
            var s = engine.Snapshot;
            return engine.Apply(new EpisodeCommand
            {
                id = "all-week-" + s.revision + "-" + kind, actorId = s.playerId, kind = kind, targetId = target, secondTargetId = second,
                text = text, performance = performance, expectedRevision = s.revision, expectedPhase = s.phase,
            });
        }

        private static void Score(EpisodeState s, string from, string to, double score)
        {
            var edge = s.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (edge == null) s.relationships.Add(edge = new RelationshipState { fromId = from, toId = to });
            edge.score = score;
        }

        private static void Valid(EpisodeState s) => Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);

        private static string Json(object value) => JsonConvert.SerializeObject(value);

        private static EpisodeState Load(string json) => JsonConvert.DeserializeObject<EpisodeState>(json,
            new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace });
    }
}
