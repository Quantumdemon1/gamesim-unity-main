using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The Have-Nots and the veto's prizes and punishments: who they are, what they cost, what they
    /// win, that none of it spends the season's generator, and that a season without them has none.
    /// </summary>
    public sealed class HaveNotTests
    {
        private static EpisodeState Season(uint seed, int house = 12)
        {
            var roster = CastTemplates.In(CastTemplates.Roster.Regular).Select(template => template.Id).ToList();
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = house, PlayerTemplateId = roster[(int)(seed % roster.Count)] }, seed);
            Assert.That(state.haveNotRulesStartWeek, Is.EqualTo(1), "A new season plays Have-Nots from its first week.");
            return state;
        }

        private static EpisodeState AtHoH(EpisodeState state)
        {
            state.phase = EpisodePhase.HoH;
            return state;
        }

        private static EpisodeState AtVeto(EpisodeState state, params string[] haveNots)
        {
            state.phase = EpisodePhase.Veto;
            state.hohId = state.Active.First(actor => !actor.isPlayer).id;
            state.nominees = state.Active.Where(actor => actor.id != state.hohId && !actor.isPlayer).Take(2).Select(actor => actor.id).ToList();
            state.vetoPlayers = state.Active.Take(6).Select(actor => actor.id).ToList();
            if (!state.vetoPlayers.Contains(state.playerId)) state.vetoPlayers[5] = state.playerId;
            state.haveNots = haveNots.ToList();
            return state;
        }

        [TestCase(12, 3)]
        [TestCase(8, 2)]
        [TestCase(5, 1)]
        [TestCase(4, 0)]
        public void TheLastOutOfTheHeadOfHouseholdAreTheWeeksHaveNots(int active, int expected)
        {
            var state = AtHoH(Season(21));
            foreach (var actor in state.contestants.Where(c => !c.isPlayer).Skip(active - 1)) actor.status = ContestantStatus.Evicted;
            var result = Apply(state, EpisodeCommandKind.Compete, .5);
            var lowest = result.competitionScores.OrderBy(score => score.score).Select(score => score.contestantId)
                .Where(id => id != result.hohId).Take(expected).ToList();
            Assert.That(result.haveNots, Is.EqualTo(lowest), "The " + expected + " lowest scores, the new Head of Household aside.");
            Assert.That(result.haveNots, Does.Not.Contain(result.hohId));
            if (expected > 0)
                Assert.That(result.events.Last(e => e.kind == HaveNots.EventKind).text, Does.StartWith("This week's Have-Nots: "));
            else Assert.That(result.events.Any(e => e.kind == HaveNots.EventKind), Is.False, "None from the final four.");
        }

        [Test]
        public void NamingTheHaveNotsSpendsNoneOfTheSeasonsRolls()
        {
            var with = AtHoH(Season(33));
            var without = with.Clone(); without.haveNotRulesStartWeek = 0;
            var a = Apply(with, EpisodeCommandKind.Compete, .5);
            var b = Apply(without, EpisodeCommandKind.Compete, .5);
            Assert.That(a.randomState, Is.EqualTo(b.randomState), "Have-Nots come from the standings, not from a roll.");
            Assert.That(a.competitionScores.Select(x => x.score), Is.EqualTo(b.competitionScores.Select(x => x.score)),
                "Nobody is a Have-Not in the competition that names them.");
            Assert.That(b.haveNots, Is.Empty, "A season without them names none.");
        }

        [Test]
        public void AHaveNotLosesAPointInTheVetoAndTheExplanationSaysSo()
        {
            var state = AtVeto(Season(44));
            string npc = state.vetoPlayers.First(id => id != state.playerId);
            var tired = AtVeto(state.Clone(), npc, state.playerId);
            var fresh = AtVeto(state.Clone());
            var a = Apply(tired, EpisodeCommandKind.Compete, .6);
            var b = Apply(fresh, EpisodeCommandKind.Compete, .6);
            Assert.That(a.randomState, Is.EqualTo(b.randomState));
            foreach (var entry in b.competitionScores)
            {
                double expected = entry.contestantId == npc || entry.contestantId == state.playerId ? HaveNots.VetoPenalty : 0;
                Assert.That(entry.score - Score(a, entry.contestantId), Is.EqualTo(expected).Within(1e-9), entry.contestantId);
            }
            string text = a.events.Last(e => e.kind == "competition-performance").text;
            Assert.That(text, Does.Contain("; have-not -1"), text);
            string terms = text.Substring(text.IndexOf("Your weighted stats:", StringComparison.Ordinal));
            var values = Regex.Matches(terms, @"(?:physical|mental|endurance|social|luck|Chance|second roll|nominee|have-not|preparation|event|storyline|performance|throw|rounding) ([+-]?\d+(?:\.\d+)?)")
                .Cast<Match>().Select(m => decimal.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).ToArray();
            decimal sum = decimal.Parse(Regex.Match(text, @"Sum = (\d+\.\d+)").Groups[1].Value, CultureInfo.InvariantCulture);
            Assert.That(values.Sum(), Is.EqualTo(sum), "The have-not point is one of the terms that add up: " + text);
            Assert.That(b.events.Last(e => e.kind == "competition-performance").text, Does.Not.Contain("have-not"), "Only a Have-Not is told.");
        }

        [Test]
        public void AHaveNotIsTiredInTheVetoOnlyAndAPlayerAmongThemHasOneFewerConversation()
        {
            var state = Season(55);
            int budget = EpisodeEngine.SocialActionBudget(state);
            state.haveNots.Add(state.playerId);
            Assert.That(EpisodeEngine.SocialActionBudget(state), Is.EqualTo(budget - 1), "One fewer conversation.");
            state.phase = EpisodePhase.HoH;
            Assert.That(HaveNots.Penalty(state, state.playerId), Is.Zero, "The veto's point, and no other competition's.");
            state.phase = EpisodePhase.Veto;
            Assert.That(HaveNots.Penalty(state, state.playerId), Is.EqualTo(HaveNots.VetoPenalty));
            state.Find(state.playerId).status = ContestantStatus.Evicted;
            Assert.That(HaveNots.Is(state, state.playerId), Is.False, "An evicted Have-Not is nobody's Have-Not.");
        }

        [Test]
        public void TheVetosRunnerUpWinsAPrizeAndItsLastFinisherTakesAPunishment()
        {
            var state = AtVeto(Season(66));
            var result = Apply(state, EpisodeCommandKind.Compete, .5);
            var ordered = result.competitionScores.OrderByDescending(score => score.score).Select(score => score.contestantId).ToList();
            Assert.That(result.vetoPrizes.Select(p => p.contestantId), Is.EqualTo(new[] { ordered[1], ordered[ordered.Count - 1] }));
            Assert.That(result.vetoPrizes[0].prizeId, Is.EqualTo(HaveNots.PrizeFor(state.seed, state.week).Id));
            Assert.That(result.vetoPrizes[1].prizeId, Is.EqualTo(HaveNots.PunishmentFor(state.seed, state.week).Id));
            Assert.That(result.vetoPrizes.All(p => p.week == state.week), Is.True);
            var lines = result.events.Where(e => e.kind == HaveNots.PrizeEventKind).Select(e => e.text).ToList();
            Assert.That(lines, Has.Count.EqualTo(2));
            Assert.That(lines[0], Does.Contain("came second in the veto and won"));
            Assert.That(lines[1], Does.Contain("finished last in the veto"));
        }

        [Test]
        public void EachWeekHasItsOwnPrizeAndPunishmentFromTheSeed()
        {
            var prizes = Enumerable.Range(1, 12).Select(week => HaveNots.PrizeFor(9, week).Id).ToList();
            var punishments = Enumerable.Range(1, 12).Select(week => HaveNots.PunishmentFor(9, week).Id).ToList();
            Assert.That(prizes.Distinct().Count(), Is.EqualTo(HaveNots.Prizes.Length), "Every prize comes up across a season.");
            Assert.That(punishments.Distinct().Count(), Is.EqualTo(HaveNots.Punishments.Length));
            Assert.That(HaveNots.PrizeFor(9, 4), Is.SameAs(HaveNots.PrizeFor(9, 4)), "A reload deals the same week.");
        }

        [Test]
        public void APassKeepsItsHolderOffTheNextHaveNotsAndAPunishmentPutsItsTakerOn()
        {
            var state = AtHoH(Season(77));
            var probe = Apply(state.Clone(), EpisodeCommandKind.Compete, 0);
            string lowest = probe.haveNots[0];
            string safe = probe.competitionScores.OrderByDescending(x => x.score).Select(x => x.contestantId)
                .First(id => id != probe.hohId && !probe.haveNots.Contains(id));
            state.haveNotPasses.Add(lowest);
            state.punishedHaveNots.Add(safe);
            var result = Apply(state, EpisodeCommandKind.Compete, 0);
            Assert.That(result.haveNots, Does.Not.Contain(lowest), "The pass keeps the lowest score off the list.");
            Assert.That(result.haveNots, Does.Contain(safe), "The punishment puts a safe score on it.");
            Assert.That(result.haveNots.Count, Is.EqualTo(HaveNots.Count(state.Active.Count()) + 1), "The pass hands its place on; the punishment is on top.");
            Assert.That(result.haveNotPasses, Is.Empty, "Used, they are gone.");
            Assert.That(result.punishedHaveNots, Is.Empty);
        }

        [Test]
        public void APunishedHouseguestWhoWinsHeadOfHouseholdEatsAtTheTable()
        {
            var state = AtHoH(Season(78));
            string winner = Apply(state.Clone(), EpisodeCommandKind.Compete, .5).hohId;
            state.punishedHaveNots.Add(winner);
            var result = Apply(state, EpisodeCommandKind.Compete, .5);
            Assert.That(result.hohId, Is.EqualTo(winner), "The punishment changes nobody's score.");
            Assert.That(result.haveNots, Does.Not.Contain(winner), "The Head of Household is never a Have-Not.");
        }

        [Test]
        public void ThePassAndTheHaveNotPunishmentAreHandedOnToNextWeek()
        {
            // A season whose first veto hands out both, so each can be seen to do what it says.
            for (uint seed = 1; seed < 500; seed++)
            {
                if (HaveNots.PrizeFor(seed, 1).Id != HaveNots.PassId || HaveNots.PunishmentFor(seed, 1).Id != HaveNots.PunishedId) continue;
                var result = Apply(AtVeto(Season(seed)), EpisodeCommandKind.Compete, .5);
                Assert.That(result.haveNotPasses, Is.EqualTo(new[] { result.vetoPrizes[0].contestantId }), "The runner-up's pass waits for next week.");
                Assert.That(result.punishedHaveNots, Is.EqualTo(new[] { result.vetoPrizes[1].contestantId }), "and the last finisher's place among the Have-Nots.");
                return;
            }
            Assert.Fail("No season's first veto hands out both.");
        }

        [Test]
        public void TheLuxuryAndTheAlarmChangeAPlayersConversationsUntilTheEndOfNextWeek()
        {
            foreach (var id in new[] { HaveNots.LuxuryId, HaveNots.AlarmId })
            {
                uint seed = 1;
                bool luxury = id == HaveNots.LuxuryId;
                // A season and week whose veto hands this out, with the player in the place that gets it.
                EpisodeState result = null;
                for (; seed < 400 && result == null; seed++)
                {
                    var prize = luxury ? HaveNots.PrizeFor(seed, 1) : HaveNots.PunishmentFor(seed, 1);
                    if (prize.Id != id) continue;
                    var state = AtVeto(Season(seed));
                    var after = Apply(state, EpisodeCommandKind.Compete, luxury ? .5 : 0);
                    if (after.vetoPrizes.Any(p => p.prizeId == id && p.contestantId == state.playerId)) result = after;
                }
                Assert.That(result, Is.Not.Null, "Some season hands the player " + id + ".");
                var modifier = result.activeModifiers.Single(m => m.id.StartsWith(id, StringComparison.Ordinal));
                Assert.That(modifier.weeksLeft, Is.EqualTo(2), "The rest of this week and all of next.");
                Assert.That(StoryModifiers.ActionsFrom(modifier.socialBonus), Is.EqualTo(luxury ? 1 : -1));
                Assert.That(result.events.Where(e => e.kind == HaveNots.PrizeEventKind).Select(e => e.text),
                    Has.Some.StartWith(luxury ? "You came second in the veto and won " : "You finished last in the veto: "),
                    "The house's story speaks to the player about their own prize.");
            }
        }

        [Test]
        public void TheFinaleHasNoHaveNots()
        {
            var state = Season(88);
            foreach (var actor in state.contestants.Where(c => !c.isPlayer).Skip(2)) actor.status = ContestantStatus.Evicted;
            string other = state.Active.First(actor => !actor.isPlayer).id;
            state.haveNots.Add(other); state.haveNotPasses.Add(state.playerId);
            state.phase = EpisodePhase.Social; state.evictionResolved = true; state.week = 9;
            var result = Apply(state, EpisodeCommandKind.Advance, 0);
            Assert.That(result.phase, Is.EqualTo(EpisodePhase.FinalHoHPart1));
            Assert.That(result.haveNots, Is.Empty, "The last three play the finale fed.");
            Assert.That(result.haveNotPasses, Is.Empty);
        }

        [Test]
        public void ASeasonWithoutThemHasNoneAndTheSaveHoldsOnlyWhatIsTrue()
        {
            var legacy = ContentCatalog.Create(12);
            Assert.That(legacy.haveNotRulesStartWeek, Is.Zero, "The default cast's fixture seasons play without them.");
            Assert.That(EpisodeValidation.TryValidate(legacy, out var reason), Is.True, reason);
            legacy.haveNots.Add(legacy.contestants[1].id);
            Assert.That(EpisodeValidation.TryValidate(legacy, out _), Is.False, "A season without Have-Nots has none.");

            var state = Season(99);
            state.haveNots.Add("nobody");
            Assert.That(EpisodeValidation.TryValidate(state, out _), Is.False, "Only houseguests.");
            state.haveNots.Clear(); state.haveNots.Add(state.playerId); state.haveNots.Add(state.playerId);
            Assert.That(EpisodeValidation.TryValidate(state, out _), Is.False, "Once each.");
            state.haveNots.Clear();
            state.vetoPrizes.Add(new VetoPrizeState { week = 1, contestantId = state.playerId, prizeId = "a-pony" });
            Assert.That(EpisodeValidation.TryValidate(state, out _), Is.False, "Only prizes the lists hold.");
            state.vetoPrizes[0].prizeId = HaveNots.CashId;
            Assert.That(EpisodeValidation.TryValidate(state, out reason), Is.True, reason);
        }

        private static double Score(EpisodeState state, string id) => state.competitionScores.Single(entry => entry.contestantId == id).score;

        private static EpisodeState Apply(EpisodeState state, EpisodeCommandKind kind, double performance)
        {
            var result = new EpisodeEngine(state).Apply(new EpisodeCommand
            {
                id = Guid.NewGuid().ToString("N"), actorId = state.playerId, expectedPhase = state.phase,
                expectedRevision = state.revision, kind = kind, performance = performance,
            });
            Assert.That(result.accepted, Is.True, result.reason);
            return result.state;
        }
    }
}
