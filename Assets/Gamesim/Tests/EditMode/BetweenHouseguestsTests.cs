using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The arcs review's finding 8: the player's commands that move two houseguests' standing with each other - a veto
    /// used and its replacement named, a lie, a scheme - under D2's all-week rules move the pair through the ledger and
    /// write none of the player's arcs (EpisodeEngine.ChangeBetween), as a completed conversation does
    /// (EpisodeNpcSocialTests). A change with the player as one of the two keeps the engine's path and its arc; with the
    /// rules off every change is the engine's path, both houseguests' arcs and all, as it always was.
    /// </summary>
    public sealed class BetweenHouseguestsTests
    {
        private static string Json(object value) => JsonConvert.SerializeObject(value);
        private static string Num(double value) => value.ToString(CultureInfo.InvariantCulture);

        /// <summary>A season as the director starts one (<see cref="ShippedRules.ApplyFresh"/>), with D2's all-week rules on or taken off.</summary>
        private static EpisodeState Shipped(uint seed, bool allWeek)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
            ShippedRules.ApplyFresh(s);
            if (!allWeek) s.allWeekRulesStartWeek = 0;
            Assert.That(EpisodeEngine.AllWeekOn(s), Is.EqualTo(allWeek));
            return s;
        }

        /// <summary>The arc history a step added, as "houseguest delta reason", arc by arc.</summary>
        private static string[] ArcLines(EpisodeState before, EpisodeState after) =>
            after.relationshipArcs.SelectMany(arc =>
            {
                int had = before.relationshipArcs.FirstOrDefault(a => a.npcId == arc.npcId)?.weeklyHistory.Count ?? 0;
                return arc.weeklyHistory.Skip(had).Select(h => arc.npcId + " " + Num(h.delta) + " " + h.reason);
            }).ToArray();

        /// <summary>The ledger entries a step added on one direction of a pair, as "type impact week N fades".</summary>
        private static string[] Added(EpisodeState before, EpisodeState after, string from, string to)
        {
            int had = before.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to)?.events.Count ?? 0;
            return after.relationships.Single(r => r.fromId == from && r.toId == to).events.Skip(had)
                .Select(e => Entry(e.type, e.impactScore, e.week) + (e.decayable ? "" : " (permanent)")).ToArray();
        }

        private static string Entry(string type, double impact, int week) => type + " " + Num(impact) + " week " + week + " fades";

        /// <summary>
        /// The engine walked by the engine tests' walker to the first state where <paramref name="make"/> makes a command
        /// it takes (and whose result <paramref name="taken"/> accepts, where given); null when the season's first six weeks
        /// pass first.
        /// </summary>
        private static EpisodeEngine WalkTo(EpisodeState fresh, Func<EpisodeState, EpisodeCommand> make, Func<EpisodeState, EpisodeState, bool> taken = null)
        {
            var engine = new EpisodeEngine(fresh);
            for (int i = 0; i < 800 && engine.Snapshot.phase != EpisodePhase.Finished && engine.Snapshot.week <= 6; i++)
            {
                var s = engine.Snapshot;
                var command = make(s);
                var trial = command == null ? null : new EpisodeEngine(s.Clone()).Apply(command);
                if (trial != null && trial.accepted && (taken == null || taken(s, trial.state))) return engine;
                var next = EpisodeEngineTests.NextCommand(s);
                // Under the finale rules a player finalist answers with one of the responses offered (SeasonAutopsyTests' walk).
                if (next.kind == EpisodeCommandKind.AnswerJury && EpisodeEngine.FinaleOn(s) && s.juryExchanges[s.juryQuestionIndex].finalistId == s.playerId)
                {
                    var exchange = s.juryExchanges[s.juryQuestionIndex];
                    next.secondTargetId = FinaleQuestions.Offered(exchange.category, exchange.receiptKind).First();
                }
                var step = engine.Apply(next);
                Assert.That(step.accepted, Is.True, "week " + s.week + " " + s.phase + ": " + step.reason);
            }
            return null;
        }

        private static EpisodeState Accept(EpisodeEngine engine, EpisodeCommand command)
        {
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True, command.kind + ": " + result.reason);
            return result.state;
        }

        private static EpisodeCommand Social(EpisodeState s, EpisodeCommandKind kind, string target, string second = null)
        {
            if (s.pendingDiary != null || s.Find(s.playerId).status != ContestantStatus.Active) return null;
            var c = EpisodeEngineTests.Command(s, kind);
            c.id = "between-" + s.revision + "-" + kind; c.targetId = target; c.secondTargetId = second;
            return c;
        }

        /// <summary>The first two houseguests in the house, in cast order: a lie's recipient and its subject.</summary>
        private static EpisodeCommand Lie(EpisodeState s)
        {
            var house = s.Active.Where(c => !c.isPlayer).ToList();
            return house.Count < 2 ? null : Social(s, EpisodeCommandKind.SpreadLie, house[0].id, house[1].id);
        }

        private static EpisodeCommand Scheme(EpisodeState s)
        {
            var house = s.Active.Where(c => !c.isPlayer).ToList();
            return house.Count < 2 ? null : Social(s, EpisodeCommandKind.SchemeAgainst, house[0].id);
        }

        /// <summary>
        /// The same state with D2's rules taken off, when that is a legal season: before any of the all-week rules' lines,
        /// records or acts exist. Null otherwise.
        /// </summary>
        private static EpisodeState RulesOffTwin(EpisodeState s)
        {
            var twin = s.Clone(); twin.allWeekRulesStartWeek = 0;
            return EpisodeValidation.TryValidate(twin, out _) && !EpisodeEngine.AllWeekOn(twin) ? twin : null;
        }

        /// <summary>The two seasons alike but for the rules' week, the relationship graph and the arcs.</summary>
        private static void AlikeOutsideTheGraphAndArcs(EpisodeState on, EpisodeState off)
        {
            var left = JObject.FromObject(on); var right = JObject.FromObject(off);
            foreach (string field in new[] { "allWeekRulesStartWeek", "relationships", "relationshipArcs" }) { left.Remove(field); right.Remove(field); }
            Assert.That(JToken.DeepEquals(left, right), Is.True, "The rules changed something besides the graph and the arcs.");
        }

        // ---------------------------------------------------------------- a lie

        /// <summary>
        /// A lie told one houseguest about another, under D2's rules: the two move through the ledger by the lie's damage,
        /// both ways, with a fading "lie" record each way (the type the engine's path always wrote), and neither one's arc
        /// moves. Where the lie comes back, the player's own change with its subject keeps the engine's path and its arc.
        /// The same state with the rules off takes the engine's path - both arcs move, the reverse by its reciprocal - and
        /// the two draw the same: the season's stream, the sequence ids and everything else end where the other's do.
        /// </summary>
        [Test]
        public void UnderTheAllWeekRulesALieMovesItsPairThroughTheLedgerAndWritesNoArc()
        {
            bool quiet = false, discovered = false, twinned = false;
            for (uint seed = 1; seed <= 40 && !(quiet && discovered && twinned); seed++)
            {
                var engine = WalkTo(Shipped(seed, true), Lie);
                if (engine == null) continue;
                var before = engine.Snapshot; var command = Lie(before);
                string recipient = command.targetId, about = command.secondTargetId;
                var twin = RulesOffTwin(before);
                var after = Accept(engine, command);

                var told = Added(before, after, recipient, about);
                Assert.That(told, Has.Length.EqualTo(1), "seed " + seed + ": one record of the lie, recipient of subject.");
                double damage = after.relationships.Single(r => r.fromId == recipient && r.toId == about).events.Last().impactScore;
                Assert.That(damage, Is.InRange(-12, -5).And.EqualTo(Math.Floor(damage)), "The source's five to twelve points.");
                foreach (var (from, to) in new[] { (recipient, about), (about, recipient) })
                {
                    Assert.That(Added(before, after, from, to), Is.EqualTo(new[] { Entry("lie", damage, before.week) }), from + " of " + to);
                    Assert.That(after.Score(from, to), Is.EqualTo(WebRules.ClampScore(before.Score(from, to) + damage)), from + " of " + to + ": the ledger's move is symmetric.");
                    Assert.That(after.relationships.Single(r => r.fromId == from && r.toId == to).lastInteractionWeek, Is.EqualTo(before.week));
                }
                var lines = ArcLines(before, after);
                Assert.That(lines.Where(l => l.StartsWith(recipient + " ", StringComparison.Ordinal)), Is.Empty, "seed " + seed + ": no arc for the recipient.");
                Assert.That(lines.Where(l => l.Contains("something about")), Is.Empty, "seed " + seed + ": the lie writes no arc.");
                bool cameBack = after.memories.Any(m => m.ownerId == about && m.text == "I found out what you were telling people about me.");
                if (cameBack)
                {
                    discovered = true;
                    Assert.That(lines.Any(l => l.StartsWith(about + " ", StringComparison.Ordinal) && l.EndsWith(" found out what you had been saying", StringComparison.Ordinal)), Is.True,
                        "seed " + seed + ": the lie came back, and the player's own change with its subject keeps its arc.");
                    Assert.That(Added(before, after, after.playerId, about), Does.Contain(Entry("lie", WebRules.RelationshipDelta(-15, before.Find(before.playerId).stats.social, true), before.week)),
                        "...through the engine's own path.");
                }
                else
                {
                    quiet = true;
                    Assert.That(lines, Is.Empty, "seed " + seed + ": no arc at all.");
                }

                if (twin == null) continue;
                twinned = true;
                var off = Accept(new EpisodeEngine(twin), command);
                Assert.That(off.randomState, Is.EqualTo(after.randomState), "seed " + seed + ": the season's stream ends where the rules-off season's does.");
                Assert.That(off.nextSequence, Is.EqualTo(after.nextSequence), "The lie's two records take the ids the engine's two took.");
                AlikeOutsideTheGraphAndArcs(after, off);
                var offLines = ArcLines(twin, off);
                Assert.That(offLines.Count(l => l.StartsWith(recipient + " ", StringComparison.Ordinal) && l.Contains("something about")), Is.EqualTo(1), "Without the rules the recipient's arc moves...");
                Assert.That(offLines.Count(l => l.StartsWith(about + " ", StringComparison.Ordinal) && l.Contains("something about")), Is.EqualTo(1), "...and the subject's.");
                double adjusted = WebRules.RelationshipDelta(damage, twin.Find(recipient).stats.social, false);
                Assert.That(off.Score(recipient, about), Is.EqualTo(WebRules.ClampScore(twin.Score(recipient, about) + adjusted)), "The engine's path, weighed by the recipient's social stat.");
                Assert.That(Added(twin, off, recipient, about), Is.EqualTo(new[] { Entry("lie", adjusted, twin.week) }));
            }
            Assert.That(quiet, Is.True, "No lie in forty seasons stayed quiet.");
            Assert.That(discovered, Is.True, "No lie in forty seasons came back.");
            Assert.That(twinned, Is.True, "No lie in forty seasons was told before the rules left anything a rules-off twin could not hold.");
        }

        // ---------------------------------------------------------------- a scheme

        /// <summary>
        /// A scheme against one houseguest, under D2's rules: each of the one or two houseguests it turns against them moves
        /// with them through the ledger by its damage, both ways, with a fading "scheme" record each way, and no arc moves -
        /// the player is in none of it. The rules-off twin takes the engine's path (the target's and each victim's arcs move)
        /// on the same draws and the same sequence ids.
        /// </summary>
        [Test]
        public void UnderTheAllWeekRulesASchemeMovesEachPairThroughTheLedgerAndWritesNoArc()
        {
            bool one = false, two = false, twinned = false;
            for (uint seed = 1; seed <= 40 && !(one && two && twinned); seed++)
            {
                var engine = WalkTo(Shipped(seed, true), Scheme);
                if (engine == null) continue;
                var before = engine.Snapshot; var command = Scheme(before);
                string target = command.targetId;
                var twin = RulesOffTwin(before);
                var after = Accept(engine, command);

                var victims = before.Active.Where(c => !c.isPlayer && c.id != target && Added(before, after, target, c.id).Length > 0).Select(c => c.id).ToList();
                Assert.That(victims.Count, Is.InRange(1, 2), "seed " + seed + ": one or two houseguests turned against the target.");
                if (victims.Count == 1) one = true; else two = true;
                foreach (string victim in victims)
                {
                    var record = Added(before, after, target, victim);
                    Assert.That(record, Has.Length.EqualTo(1), "seed " + seed + ": one record, target of victim.");
                    double damage = after.relationships.Single(r => r.fromId == target && r.toId == victim).events.Last().impactScore;
                    Assert.That(damage, Is.InRange(-8, -3).And.EqualTo(Math.Floor(damage)), "The source's three to eight points.");
                    foreach (var (from, to) in new[] { (target, victim), (victim, target) })
                    {
                        Assert.That(Added(before, after, from, to), Is.EqualTo(new[] { Entry("scheme", damage, before.week) }), from + " of " + to);
                        Assert.That(after.Score(from, to), Is.EqualTo(WebRules.ClampScore(before.Score(from, to) + damage)), from + " of " + to + ": the ledger's move is symmetric.");
                    }
                }
                Assert.That(ArcLines(before, after), Is.Empty, "seed " + seed + ": no arc - the player is in none of it.");
                Assert.That(after.Score(target, after.playerId), Is.EqualTo(before.Score(target, before.playerId)), "The target does not learn it was the player.");

                if (twin == null) continue;
                twinned = true;
                var off = Accept(new EpisodeEngine(twin), command);
                Assert.That(off.randomState, Is.EqualTo(after.randomState), "seed " + seed + ": the season's stream ends where the rules-off season's does.");
                Assert.That(off.nextSequence, Is.EqualTo(after.nextSequence), "The scheme's records take the ids the engine's took.");
                AlikeOutsideTheGraphAndArcs(after, off);
                var offLines = ArcLines(twin, off);
                Assert.That(offLines.Count(l => l.StartsWith(target + " ", StringComparison.Ordinal)), Is.EqualTo(victims.Count), "Without the rules the target's arc moves for each victim...");
                foreach (string victim in victims)
                    Assert.That(offLines.Count(l => l.StartsWith(victim + " ", StringComparison.Ordinal)), Is.EqualTo(1), "...and each victim's.");
            }
            Assert.That(one && two, Is.True, "Forty seasons did not show a scheme with one victim and one with two.");
            Assert.That(twinned, Is.True, "No scheme in forty seasons ran before the rules left anything a rules-off twin could not hold.");
        }

        // ---------------------------------------------------------------- the veto

        /// <summary>Whether the veto meeting is open and the house's holder will use the veto.</summary>
        private static bool VetoToBeUsed(EpisodeState s) =>
            s.phase == EpisodePhase.VetoMeeting && !s.vetoResolved && s.vetoHolderId != s.playerId && EpisodeEngine.NpcVetoSave(s) != null;

        /// <summary>
        /// The house's own veto meeting with all three of the veto's moves: a houseguest holds the veto and uses it on
        /// somebody else, a houseguest is Head of Household, the player is not on the block (and is not named in the saved
        /// nominee's place, which <see cref="NotThePlayersReplacement"/> checks on the step's result).
        /// </summary>
        private static EpisodeCommand HousesVeto(EpisodeState s) =>
            VetoToBeUsed(s) && s.hohId != s.playerId && s.hohId != s.vetoHolderId && EpisodeEngine.NpcVetoSave(s) != s.vetoHolderId
            && !s.nominees.Contains(s.playerId) ? EpisodeEngineTests.NextCommand(s) : null;

        private static bool NotThePlayersReplacement(EpisodeState before, EpisodeState after) => !after.nominees.Contains(after.playerId);

        /// <summary>The player's veto meeting as Head of Household: a houseguest's veto used, the player naming the replacement.</summary>
        private static EpisodeCommand PlayersReplacement(EpisodeState s) =>
            VetoToBeUsed(s) && s.hohId == s.playerId ? EpisodeEngineTests.NextCommand(s) : null;

        /// <summary>
        /// The state at the veto meeting with the window's last beats already played (EpisodeEngine.Close, which the step
        /// would play first and which is idempotent), so the step's own changes are the veto's alone.
        /// </summary>
        private static EpisodeEngine Closed(EpisodeEngine engine)
        {
            var s = engine.Snapshot;
            EpisodeEngine.Close(s);
            return new EpisodeEngine(s);
        }

        /// <summary>
        /// A veto between houseguests, under D2's rules: a houseguest saved by a houseguest, a houseguest named in their
        /// place by a houseguest Head of Household. Each of the veto's three moves goes through the ledger - the one saved
        /// and the holder by 25, the replacement and the Head of Household by -20, the replacement and the holder by -15 -
        /// both ways with a fading record of its own type, and no arc moves. (Without the rules such a meeting comes rarer
        /// still - none in sixty seasons' first six weeks -
        /// so the engine's path for the veto is held where the player names the replacement, below, and on the change itself.)
        /// </summary>
        [Test]
        public void UnderTheAllWeekRulesAVetoBetweenHouseguestsMovesEachPairThroughTheLedgerAndWritesNoArc()
        {
            // Such a meeting is rare (a holder off the block saving an ally, the player not named in their place): the
            // search starts at the first seed that holds one in this build (21, in week 3), and goes on from there.
            for (uint seed = 21; seed <= 80; seed++)
            {
                var found = WalkTo(Shipped(seed, true), HousesVeto, NotThePlayersReplacement);
                if (found == null) continue;
                var engine = Closed(found); var before = engine.Snapshot;
                string saved = EpisodeEngine.NpcVetoSave(before), holder = before.vetoHolderId, hoh = before.hohId;
                var after = Accept(engine, HousesVeto(before));
                Assert.That(after.vetoResolved, Is.True);
                string replacement = after.nominees.Single(id => !before.nominees.Contains(id));
                Assert.That(new[] { saved, holder, hoh, replacement }, Has.No.Member(before.playerId), "The player is out of it.");
                Assert.That(saved, Is.Not.EqualTo(holder), "The holder saved somebody else.");

                var moves = new List<(string from, string to, string type, double delta)> { (saved, holder, EpisodeEngine.VetoSaveEvent, 25), (replacement, hoh, EpisodeEngine.VetoReplacementEvent, -20) };
                if (hoh != holder) moves.Add((replacement, holder, EpisodeEngine.VetoForcedEvent, -15));
                foreach (var move in moves.Where(m => m.from != m.to))
                    foreach (var (from, to) in new[] { (move.from, move.to), (move.to, move.from) })
                    {
                        Assert.That(Added(before, after, from, to), Is.EqualTo(new[] { Entry(move.type, move.delta, before.week) }), "seed " + seed + ": " + from + " of " + to);
                        Assert.That(after.Score(from, to), Is.EqualTo(WebRules.ClampScore(before.Score(from, to) + move.delta)), from + " of " + to + ": the ledger's move is symmetric.");
                    }
                Assert.That(ArcLines(before, after), Is.Empty, "seed " + seed + ": no arc - the player is in none of it.");
                Assert.That(EpisodeEngine.HouseTalkRecords(after, before.nextSequence), Is.Zero, "A command's records are not the house's talk, which the autonomy QA allows.");
                Assert.That(EpisodeValidation.TryValidate(after, out string error), Is.True, error);
                return;
            }
            Assert.Fail("No season from 21 to 80 held the house's own veto meeting under the rules.");
        }

        /// <summary>
        /// The player's own path is unchanged under D2's rules: as Head of Household naming the replacement for a
        /// houseguest's veto, the replacement's change with the player keeps the engine's path - their arc moves by the
        /// change as the replacement's social stat weighs it (and by the nomination's -8), no record - while the save and
        /// the forced replacement, between houseguests, go through the ledger with no arc: only the replacement's arc moves.
        /// The same meeting with the rules off takes the engine's path for all of it: the holder's arc and the
        /// replacement's move for the forced replacement (and the one saved and the holder's for the save), no record.
        /// </summary>
        [Test]
        public void UnderTheAllWeekRulesTheReplacementsChangeWithThePlayerKeepsTheEnginesPathAndItsArc()
        {
            Assert.That(ThePlayersReplacement(true), Is.True, "No season in eighty had the player name a replacement for a houseguest's veto under the rules.");
            Assert.That(ThePlayersReplacement(false), Is.True, "No season in eighty had the player name a replacement for a houseguest's veto without the rules.");
        }

        private static bool ThePlayersReplacement(bool allWeek)
        {
            var types = new[] { EpisodeEngine.VetoSaveEvent, EpisodeEngine.VetoReplacementEvent, EpisodeEngine.VetoForcedEvent };
            for (uint seed = 1; seed <= 80; seed++)
            {
                var found = WalkTo(Shipped(seed, allWeek), PlayersReplacement);
                if (found == null) continue;
                var engine = Closed(found); var before = engine.Snapshot; var command = PlayersReplacement(before);
                string saved = command.targetId, replacement = command.secondTargetId, holder = before.vetoHolderId, player = before.playerId;
                var after = Accept(engine, command);
                var lines = ArcLines(before, after);
                string at = (allWeek ? "rules on" : "rules off") + ", seed " + seed + ": ";

                double adjusted = WebRules.RelationshipDelta(-20, before.Find(replacement).stats.social, false);
                Assert.That(lines.Count(l => l.StartsWith(replacement + " " + Num(adjusted) + " ", StringComparison.Ordinal) && l.EndsWith(" as replacement nominee", StringComparison.Ordinal)),
                    Is.EqualTo(1), at + "the engine's path: the replacement's arc, by the change as their social stat weighs it.");
                Assert.That(after.Score(replacement, player), Is.EqualTo(WebRules.ClampScore(before.Score(replacement, player) + adjusted)));
                Assert.That(Added(before, after, replacement, player), Is.Empty, at + "the engine's path records nothing for a veto.");
                var moves = new[] { (saved, holder, EpisodeEngine.VetoSaveEvent, 25d), (replacement, holder, EpisodeEngine.VetoForcedEvent, -15d) }.Where(m => m.Item1 != m.Item2).ToList();
                if (allWeek)
                {
                    Assert.That(lines.Select(l => l.Split(' ')[0]).Distinct(), Is.EqualTo(new[] { replacement }), at + "only the replacement's arc, the player's own.");
                    foreach (var (from, to, type, delta) in moves)
                    {
                        Assert.That(Added(before, after, from, to), Is.EqualTo(new[] { Entry(type, delta, before.week) }), at + from + " of " + to);
                        Assert.That(Added(before, after, to, from), Is.EqualTo(new[] { Entry(type, delta, before.week) }), at + to + " of " + from);
                    }
                }
                else
                {
                    foreach (string id in new[] { holder, replacement })
                        Assert.That(lines.Count(l => l.StartsWith(id + " ", StringComparison.Ordinal) && l.EndsWith(" on the block", StringComparison.Ordinal)), Is.EqualTo(1),
                            at + "without the rules the forced replacement moves both arcs: " + id);
                    if (saved != holder)
                        foreach (string id in new[] { saved, holder })
                            Assert.That(lines.Count(l => l.StartsWith(id + " ", StringComparison.Ordinal) && l.Contains(" used POV to save ")), Is.EqualTo(1), at + "...and the save both: " + id);
                    double forced = WebRules.RelationshipDelta(-15, before.Find(replacement).stats.social, false);
                    Assert.That(after.Score(replacement, holder), Is.EqualTo(WebRules.ClampScore(before.Score(replacement, holder) + forced)), at + "the engine's path, weighed.");
                    Assert.That(after.relationships.SelectMany(r => r.events).Select(e => e.type).Intersect(types), Is.Empty, at + "...and no record of the rules' types.");
                }
                Assert.That(EpisodeValidation.TryValidate(after, out string error), Is.True, error);
                return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- the change itself

        private static readonly MethodInfo Between = typeof(EpisodeEngine).GetMethod("ChangeBetween", BindingFlags.NonPublic | BindingFlags.Static, null,
            new[] { typeof(EpisodeState), typeof(string), typeof(string), typeof(double), typeof(string), typeof(string), typeof(string) }, null);

        private static void ChangeBetween(EpisodeState s, string from, string to, double delta, string note, string eventType, string recordType) =>
            Between.Invoke(null, new object[] { s, from, to, delta, note, eventType, recordType });

        /// <summary>
        /// The stream decision, on the change itself (EpisodeEngine.ChangeBetween; the editor's tests cannot see the
        /// simulation's internals). Between two houseguests under D2's rules it draws what the engine's path draws - one
        /// reciprocal untyped, two typed - and sets them aside, so the season's stream ends where the rules-off twin's does;
        /// a typed change takes the two sequence ids the engine's two records took, an untyped one (a veto's) two where the
        /// engine's took none. With the player as one of the two, the rules change nothing. A houseguest's change with
        /// themselves (a holder saving themselves) changes nothing and draws nothing, on either path.
        /// </summary>
        [Test]
        public void TheChangeDrawsWhatTheEnginesPathDrawsAndWritesNoArcBetweenHouseguests()
        {
            Assert.That(Between, Is.Not.Null, "EpisodeEngine.ChangeBetween(EpisodeState, string, string, double, string, string, string)");
            var on = Shipped(71, true); var off = RulesOffTwin(on);
            Assert.That(off, Is.Not.Null, "A fresh season's rules-off twin is a legal season.");
            var npcs = on.Active.Where(c => !c.isPlayer).Take(2).Select(c => c.id).ToArray();
            foreach (var (eventType, recordType, draws) in new[] { ((string)null, EpisodeEngine.VetoSaveEvent, 1), ("lie", (string)null, 2) })
            {
                var onBefore = on.Clone(); var offBefore = off.Clone();
                ChangeBetween(on, npcs[0], npcs[1], -9, "Between them", eventType, recordType);
                ChangeBetween(off, npcs[0], npcs[1], -9, "Between them", eventType, recordType);
                var rng = new SeededRandom(onBefore.randomState);
                for (int i = 0; i < draws; i++) rng.NextDouble();
                Assert.That(on.randomState, Is.EqualTo(rng.State), (eventType ?? recordType) + ": the engine's draws, set aside.");
                Assert.That(off.randomState, Is.EqualTo(on.randomState), "The rules-off twin's stream is where the season's is.");
                Assert.That(on.nextSequence, Is.EqualTo(onBefore.nextSequence + 2), "A record each way.");
                Assert.That(off.nextSequence, Is.EqualTo(offBefore.nextSequence + (eventType == null ? 0 : 2)), "The engine's path records only a typed change.");
                Assert.That(Json(on.relationshipArcs), Is.EqualTo(Json(onBefore.relationshipArcs)), "Between two houseguests, no arc.");
                Assert.That(ArcLines(offBefore, off).Select(l => l.Split(' ')[0]), Is.EquivalentTo(npcs), "Without the rules, both houseguests' arcs.");
                foreach (var (from, to) in new[] { (npcs[0], npcs[1]), (npcs[1], npcs[0]) })
                {
                    Assert.That(on.Score(from, to), Is.EqualTo(WebRules.ClampScore(onBefore.Score(from, to) - 9)));
                    Assert.That(Added(onBefore, on, from, to), Is.EqualTo(new[] { Entry(eventType ?? recordType, -9, on.week) }));
                }
                Assert.That(EpisodeEngine.HouseTalkRecords(on, onBefore.nextSequence), Is.Zero, "Not the house's talk.");
            }

            var player = Shipped(71, true); var playerOff = RulesOffTwin(player); var withPlayer = player.Clone();
            ChangeBetween(player, npcs[0], player.playerId, -9, "With the player", null, EpisodeEngine.VetoForcedEvent);
            ChangeBetween(playerOff, npcs[0], playerOff.playerId, -9, "With the player", null, EpisodeEngine.VetoForcedEvent);
            ChangeBetween(player, player.playerId, npcs[1], 6, "The player's", "lie", null);
            ChangeBetween(playerOff, playerOff.playerId, npcs[1], 6, "The player's", "lie", null);
            Assert.That(ArcLines(withPlayer, player).Select(l => l.Split(' ')[0]), Is.EquivalentTo(npcs), "The player's arcs move...");
            var left = JObject.FromObject(player); var right = JObject.FromObject(playerOff);
            left.Remove("allWeekRulesStartWeek"); right.Remove("allWeekRulesStartWeek");
            Assert.That(JToken.DeepEquals(left, right), Is.True, "...exactly as without the rules: the engine's path.");
            Assert.That(player.randomState, Is.Not.EqualTo(withPlayer.randomState), "...which rolls its reciprocals.");
            Assert.That(Added(withPlayer, player, npcs[0], player.playerId), Is.Empty, "An untyped change records nothing, and nothing of the rules' types.");

            var same = on.Clone();
            ChangeBetween(on, npcs[0], npcs[0], 25, "Saved themselves", null, EpisodeEngine.VetoSaveEvent);
            Assert.That(Json(on), Is.EqualTo(Json(same)), "A change with oneself is nothing, and draws nothing.");
        }

        // ---------------------------------------------------------------- validation

        /// <summary>
        /// The veto's three record types belong to D2's rules from their start week, as the house's talk records do
        /// (WaveDInertSchema28Tests): the engine's own path recorded nothing for a veto, so a season without the rules
        /// carries none, and none is dated before them.
        /// </summary>
        [TestCase(EpisodeEngine.VetoSaveEvent)] [TestCase(EpisodeEngine.VetoReplacementEvent)] [TestCase(EpisodeEngine.VetoForcedEvent)]
        public void TheVetosRecordsBelongToTheAllWeekRulesFromTheirStartWeek(string type)
        {
            Assert.That(new[] { "veto-save", "veto-replacement", "veto-forced" }, Has.Member(type), "The saved types.");
            Assert.That(RelationshipLedger.Decays(type), Is.True, "They fade.");
            var s = ContentCatalog.Create(2830);
            var edge = s.relationships.First(r => r.fromId != s.playerId && r.toId != s.playerId);
            edge.events.Add(new RelationshipEventState
            {
                sequence = s.nextSequence++, week = s.week, type = type, description = "A veto between two of the house", impactScore = 25,
                decayable = RelationshipLedger.Decays(type),
            });
            Refused(s, "none of their records");
            s.pactPlanRulesStartWeek = s.allianceLeakRulesStartWeek = 1; Refused(s, "none of their records");
            s.allWeekRulesStartWeek = 2; Refused(s, "none of their records");
            s.allWeekRulesStartWeek = 1; Accepted(s);
        }

        /// <summary>
        /// The consolidation check's ARCS2-4: the all-week rules' records - a veto's three moves and the house's talk - are only
        /// ever written between two houseguests. A change with the player as one of the two keeps the engine's own path
        /// (<c>ChangeBetween</c>, <c>CompletionChange</c>), which records none of them, as it has since each was introduced. So a
        /// season holding one on an edge of the player's, either way, is refused though the rules are on and it is dated from
        /// their start: it would feed the player's own ledger trust and the web's history with an entry no play can write. The
        /// same record between two houseguests stays legal (the control).
        /// </summary>
        [TestCase(EpisodeEngine.VetoSaveEvent)] [TestCase(EpisodeEngine.VetoReplacementEvent)] [TestCase(EpisodeEngine.VetoForcedEvent)]
        [TestCase(EpisodeEngine.NpcConversationEvent)] [TestCase(EpisodeEngine.NpcGossipEvent)]
        public void TheAllWeekRulesRecordsAreNeverOnAnEdgeOfThePlayers(string type)
        {
            Assert.That(EpisodeEngine.AllWeekRecord(type), Is.True, "One of the rules' types.");
            foreach (bool fromThePlayer in new[] { true, false })
            {
                var s = ContentCatalog.Create(2831);
                s.pactPlanRulesStartWeek = s.allianceLeakRulesStartWeek = s.allWeekRulesStartWeek = 1;
                RelationshipEventState Entry() => new RelationshipEventState
                {
                    sequence = s.nextSequence++, week = s.week, type = type, description = "A record of the all-week rules", impactScore = 25,
                    decayable = RelationshipLedger.Decays(type),
                };
                s.relationships.First(r => r.fromId != s.playerId && r.toId != s.playerId).events.Add(Entry());
                Accepted(s);
                s.relationships.First(r => fromThePlayer ? r.fromId == s.playerId && r.toId != s.playerId
                    : r.toId == s.playerId && r.fromId != s.playerId).events.Add(Entry());
                Refused(s, "only ever between two houseguests");
            }
        }

        private static void Accepted(EpisodeState s)
        { string before = Json(s); Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error); Assert.That(Json(s), Is.EqualTo(before)); }

        private static void Refused(EpisodeState s, string reason)
        {
            string before = Json(s);
            Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.False);
            Assert.That(error, Does.Contain(reason));
            Assert.Throws<ArgumentException>(() => new EpisodeEngine(s));
            Assert.That(Json(s), Is.EqualTo(before));
        }
    }
}
