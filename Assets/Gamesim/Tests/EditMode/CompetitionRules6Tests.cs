using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Competition rules 6 switched on (the trivia's switch-on): every season the game starts plays rules 6, so from week 4,
    /// after three evictions, a memory board can be dealt as the trivia; a built season stays on rules 4; a season held at
    /// rules 5 by name deals exactly rules 5's games; and the two rules commit the same results, the same state to the trivia's
    /// first week and the same state but for the line that names the game after it. Unity-free, so the subset runs it on
    /// every push.
    /// </summary>
    public sealed class CompetitionRules6Tests
    {
        [Test]
        public void ANewSeasonStartsOnRulesSixAndABuiltSeasonStaysOnRulesFour()
        {
            Assert.That(CompetitionRules.Trivia, Is.EqualTo(6));
            Assert.That(CompetitionRules.Current, Is.EqualTo(CompetitionRules.Trivia), "The rules every season the game starts plays.");
            Assert.That(CompetitionRules.Newest, Is.EqualTo(CompetitionRules.Current), "Nothing is defined beyond them.");
            foreach (int size in new[] { 8, 12 })
                foreach (uint seed in new[] { 1u, 6101u })
                {
                    var built = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size }, seed);
                    Assert.That(built.competitionRulesVersion, Is.EqualTo(CompetitionRules.Widened), size + "/" + seed + ": a built season stays on rules 4.");
                    var fresh = built.Clone();
                    ShippedRules.ApplyFresh(fresh);
                    Assert.That(fresh.competitionRulesVersion, Is.EqualTo(CompetitionRules.Trivia), size + "/" + seed + ": a season the game starts plays rules 6.");
                    Assert.That(EpisodeValidation.TryValidate(fresh, out string reason), Is.True, reason);
                    var chosen = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size }, seed);
                    ShippedRules.ApplyFresh(chosen, new SeasonBuilder.Choice { HouseSize = size });
                    Assert.That(chosen.competitionRulesVersion, Is.EqualTo(CompetitionRules.Trivia), size + "/" + seed + ": the cast screen's form too.");
                }
        }

        /// <summary>
        /// A season the game starts deals rules 6's selector at every competition, with no rule moved by hand: the late memory
        /// board rules 5 would deal is the trivia, in week 4 or later after three evictions, and every other game is rules 5's.
        /// </summary>
        [Test]
        public void AFreshSeasonDealsTheRulesSixSelectorAndTheTriviaOnlyLate()
        {
            uint seed = LateBoardSeed(out int boardWeek, out EpisodePhase boardPhase);
            var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 10 }, seed);
            ShippedRules.ApplyFresh(fresh);
            var engine = new EpisodeEngine(fresh);
            var seen = new HashSet<string>();
            string firstTrivia = null;
            int competitions = 0;
            for (int step = 0; step < 4000 && engine.Snapshot.week <= boardWeek && engine.Snapshot.phase != EpisodePhase.Finished; step++)
            {
                var s = engine.Snapshot;
                if (EpisodeEngine.IsCompetition(s.phase) && !s.competitionResolved && seen.Add(s.week + " " + s.phase))
                {
                    competitions++;
                    var dealt = CompetitionDefinitions.For(s);
                    var five = CompetitionDefinitions.Version5(s.seed, s.week, s.phase);
                    Assert.That(dealt, Is.SameAs(CompetitionDefinitions.Version6(s, s.week, s.phase)), s.week + " " + s.phase + ": rules 6's selector.");
                    if (dealt == CompetitionDefinitions.Trivia)
                    {
                        if (firstTrivia == null) firstTrivia = s.week + " " + s.phase;
                        Assert.That(s.week, Is.GreaterThanOrEqualTo(CompetitionTrivia.EarliestWeek), "Never before week 4.");
                        Assert.That(s.ledger.power.Count(r => r.week < s.week && r.evicteeId != null), Is.GreaterThanOrEqualTo(CompetitionTrivia.EvictionsFirst),
                            "Never before three evictions.");
                        Assert.That(five, Is.SameAs(CompetitionDefinitions.HouseMemory).Or.SameAs(CompetitionDefinitions.FirstImpressions),
                            "The trivia takes a memory board's place.");
                        Assert.That(CompetitionTrivia.Quiz(s).Count, Is.EqualTo(CompetitionTrivia.Questions));
                    }
                    else Assert.That(dealt, Is.SameAs(five), s.week + " " + s.phase + ": every other game is rules 5's.");
                }
                var result = engine.Apply(EpisodeEngineTests.NextCommand(s));
                Assert.That(result.accepted, Is.True, "step " + step + ": " + result.reason);
            }
            Assert.That(competitions, Is.GreaterThan(2 * (boardWeek - 1)), "The walk met every week's competitions.");
            Assert.That(firstTrivia, Is.EqualTo(boardWeek + " " + boardPhase), "seed " + seed + ": the season's first late memory board is the trivia.");
            Assert.That(engine.Snapshot.events.Any(e => e.kind == "competition-definition" && e.text.StartsWith(CompetitionDefinitions.Trivia.Id + " ", StringComparison.Ordinal)),
                Is.True, "The engine logged the game it dealt.");
        }

        /// <summary>
        /// Rules 5's selection is unchanged and the rules change only the game: the same season, played by the same commands,
        /// started on rules 6 and held at rules 5 by name, deals rules 5's own games at rules 5 (never the trivia), ends every
        /// step before the trivia's first week on the same state but for the rules' own number, and every step after it on the
        /// same state but for that number and the line that names the game dealt.
        /// </summary>
        [Test]
        public void ASeasonHeldAtRulesFiveDealsItsOwnGamesAndCommitsWhatRulesSixCommits()
        {
            uint seed = LateBoardSeed(out int boardWeek, out _);
            var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 10 }, seed);
            ShippedRules.ApplyFresh(fresh);
            Assert.That(fresh.competitionRulesVersion, Is.EqualTo(CompetitionRules.Trivia));
            var held = fresh.Clone();
            held.competitionRulesVersion = CompetitionRules.RingTower;
            Assert.That(EpisodeValidation.TryValidate(held, out string reason), Is.True, reason);
            var six = new EpisodeEngine(fresh);
            var five = new EpisodeEngine(held);
            int before = 0, after = 0;
            for (int step = 0; step < 4000 && six.Snapshot.week <= boardWeek + 1 && six.Snapshot.phase != EpisodePhase.Finished; step++)
            {
                var s = five.Snapshot;
                if (EpisodeEngine.IsCompetition(s.phase))
                {
                    Assert.That(CompetitionDefinitions.For(s), Is.SameAs(CompetitionDefinitions.Version5(s.seed, s.week, s.phase)), s.week + " " + s.phase + ": rules 5's own game.");
                    Assert.That(CompetitionDefinitions.For(s), Is.Not.SameAs(CompetitionDefinitions.Trivia));
                }
                var command = EpisodeEngineTests.NextCommand(six.Snapshot);
                var a = six.Apply(command);
                var b = five.Apply(command);
                Assert.That(a.accepted, Is.True, a.reason);
                Assert.That(b.accepted, Is.True, b.reason);
                Assert.That(b.state.competitionRulesVersion, Is.EqualTo(CompetitionRules.RingTower), "A season keeps the rules it holds.");
                if (six.Snapshot.week < boardWeek)
                {
                    Assert.That(Json(six.Snapshot, blankDealt: false), Is.EqualTo(Json(five.Snapshot, blankDealt: false)), "step " + step + ": " + command.kind);
                    before++;
                }
                else
                {
                    Assert.That(Json(six.Snapshot, blankDealt: true), Is.EqualTo(Json(five.Snapshot, blankDealt: true)), "step " + step + ": " + command.kind);
                    after++;
                }
            }
            Assert.That(before, Is.GreaterThan(0));
            Assert.That(after, Is.GreaterThan(0), "The walk went past the trivia's first week.");
            Assert.That(Dealt(six.Snapshot, CompetitionDefinitions.Trivia), Is.True, "Rules 6 dealt the trivia.");
            Assert.That(Dealt(five.Snapshot, CompetitionDefinitions.Trivia), Is.False, "Rules 5 never deals it.");
        }

        /// <summary>A seed whose first memory board under rules 5 falls in week 4 or 5, so a short walk reaches it.</summary>
        private static uint LateBoardSeed(out int week, out EpisodePhase phase)
        {
            for (uint seed = 1; seed < 2000; seed++)
                for (int w = 1; w <= 12; w++)
                    foreach (var p in new[] { EpisodePhase.HoH, EpisodePhase.Veto })
                    {
                        var game = CompetitionDefinitions.Version5(seed, w, p);
                        if (game != CompetitionDefinitions.HouseMemory && game != CompetitionDefinitions.FirstImpressions) continue;
                        if (w >= 4 && w <= 5) { week = w; phase = p; return seed; }
                        w = 13; break;
                    }
            throw new InvalidOperationException("No seed with a late memory board.");
        }

        private static bool Dealt(EpisodeState state, CompetitionDefinition game) =>
            state.events.Any(e => e.kind == "competition-definition" && e.text.StartsWith(game.Id + " ", StringComparison.Ordinal));

        /// <summary>The state as JSON with the rules' number taken out and, when asked, the dealt game's line too.</summary>
        private static string Json(EpisodeState state, bool blankDealt)
        {
            var o = JObject.FromObject(state);
            o["competitionRulesVersion"] = 0;
            if (blankDealt)
                foreach (var entry in (JArray)o["events"])
                    if ((string)entry["kind"] == "competition-definition") entry["text"] = "";
            return o.ToString(Formatting.None);
        }
    }
}
