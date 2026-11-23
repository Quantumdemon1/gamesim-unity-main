using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The whole-house moves' lock (<see cref="WholeHouseMoves.Lock"/>) against the engine's own refusal,
    /// at every step of a whole season: a move is never drawn locked when the engine would take it, and
    /// never left open when it would refuse it. The lock's "no actions left" is the engine's own
    /// "window complete" refusal, word for word; every other refusal is "not during this part of the week".
    /// Each probe runs on a copy of the season, so the walk itself is never changed by it.
    /// </summary>
    public sealed class WholeHouseMoveLockTests
    {
        /// <summary>The engine's refusal when the window's actions are spent (HouseMeeting and Social both say it).</summary>
        private const string WindowComplete = "This social window is complete. Continue the episode.";

        private static readonly EpisodePhase[] WeekPhases =
        {
            EpisodePhase.Social, EpisodePhase.HoH, EpisodePhase.Nomination, EpisodePhase.Veto,
            EpisodePhase.VetoMeeting, EpisodePhase.Campaign, EpisodePhase.Eviction,
        };

        [Test]
        public void TheLockAgreesWithTheEngineAtEveryStepOfANewSeason()
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 4127u);
            ShippedRules.ApplyFresh(s);
            Walk(s, "a new season");
        }

        [Test]
        public void TheLockAgreesWithTheEngineAtEveryStepOfASeasonWithoutTheWeeksWindows()
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 911u);
            Assert.That(EpisodeEngine.WeekRulesOn(s), Is.False, "The legacy week: one budget, no windows.");
            Walk(s, "a season without windows");
        }

        [Test]
        public void OnlyTheWholeHouseMovesAreRead()
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 7u);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => WholeHouseMoves.Lock(s, EpisodeCommandKind.Talk));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => WholeHouseMoves.Lock(s, EpisodeCommandKind.BuyActionPoint));
        }

        [Test]
        public void ReadingTheLockChangesNothing()
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 4127u);
            ShippedRules.ApplyFresh(s);
            s.phase = EpisodePhase.Social;
            var before = JsonConvert.SerializeObject(s);
            WholeHouseMoves.Lock(s, EpisodeCommandKind.HouseMeeting);
            WholeHouseMoves.Lock(s, EpisodeCommandKind.Eavesdrop);
            Assert.That(JsonConvert.SerializeObject(s), Is.EqualTo(before), "No draw, no write.");
        }

        // ------------------------------------------------------------ the walk

        private static void Walk(EpisodeState start, string what)
        {
            var engine = new EpisodeEngine(start);
            var seen = new HashSet<EpisodePhase>();
            var outcomes = new HashSet<HouseMoveLock>();
            var spentIn = new HashSet<EpisodePhase>();
            string lastSpent = null;
            for (int step = 0; step < 4000 && engine.Snapshot.phase != EpisodePhase.Finished; step++)
            {
                var state = engine.Snapshot;
                seen.Add(state.phase);
                foreach (var probe in Probes)
                    outcomes.Add(Agree(state, probe, what + ", step " + step));
                // Once in each free time and each campaign: spend the window to its end, checking at every action.
                string key = state.week + ":" + state.phase;
                if ((state.phase == EpisodePhase.Social || state.phase == EpisodePhase.Campaign) && key != lastSpent
                    && WholeHouseMoves.Lock(state, EpisodeCommandKind.HouseMeeting) == HouseMoveLock.Open)
                {
                    lastSpent = key;
                    if (SpendTheWindow(state, what + ", week " + state.week + " " + state.phase, outcomes)) spentIn.Add(state.phase);
                }
                var result = engine.Apply(Next(state));
                Assert.That(result.accepted, Is.True, what + ": the walk's own step " + step + " at " + state.phase + ": " + result.reason);
            }
            Assert.That(engine.Snapshot.phase, Is.EqualTo(EpisodePhase.Finished), what + " reaches its end.");
            Assert.That(seen, Is.SupersetOf(WeekPhases), what + " passes every part of a week.");
            Assert.That(outcomes, Is.EquivalentTo(new[] { HouseMoveLock.Open, HouseMoveLock.NoActionsLeft, HouseMoveLock.NotNow }),
                what + " meets all three answers.");
            Assert.That(spentIn, Is.EquivalentTo(new[] { EpisodePhase.Social, EpisodePhase.Campaign }),
                what + " runs out of actions in free time and in the campaign.");
        }

        /// <summary>Spends a copy of the window with rallies until the lock closes; true when it closed on no actions left.</summary>
        private static bool SpendTheWindow(EpisodeState state, string what, HashSet<HouseMoveLock> outcomes)
        {
            var copy = new EpisodeEngine(state);
            for (int i = 0; i < 60; i++)
            {
                var now = copy.Snapshot;
                HouseMoveLock last = HouseMoveLock.Open;
                foreach (var probe in Probes) { last = Agree(now, probe, what + ", action " + i); outcomes.Add(last); }
                if (WholeHouseMoves.Lock(now, EpisodeCommandKind.HouseMeeting) != HouseMoveLock.Open)
                    return WholeHouseMoves.Lock(now, EpisodeCommandKind.HouseMeeting) == HouseMoveLock.NoActionsLeft;
                var rally = Command(now, EpisodeCommandKind.HouseMeeting, EpisodeEngine.RallyTroops, "spend");
                var result = copy.Apply(rally);
                Assert.That(result.accepted, Is.True, what + ": a rally the lock left open: " + result.reason);
            }
            Assert.Fail(what + ": the window never closed.");
            return false;
        }

        private struct Probe { public EpisodeCommandKind Kind; public string Text; }

        private static readonly Probe[] Probes =
        {
            new Probe { Kind = EpisodeCommandKind.HouseMeeting, Text = EpisodeEngine.RallyTroops },
            new Probe { Kind = EpisodeCommandKind.HouseMeeting, Text = EpisodeEngine.AirDirtyLaundry },
            new Probe { Kind = EpisodeCommandKind.Eavesdrop, Text = null },
        };

        /// <summary>The lock for a move, checked against what the engine does with it on a copy of the season.</summary>
        private static HouseMoveLock Agree(EpisodeState state, Probe probe, string where)
        {
            var locked = WholeHouseMoves.Lock(state, probe.Kind);
            var result = new EpisodeEngine(state).Apply(Command(state, probe.Kind, probe.Text, "probe"));
            string label = where + " (" + state.phase + ", " + probe.Kind + (probe.Text != null ? " " + probe.Text : "") + ")";
            if (locked == HouseMoveLock.Open)
                Assert.That(result.accepted, Is.True, label + ": the lock is open, so the engine takes it; it said: " + result.reason);
            else
            {
                Assert.That(result.accepted, Is.False, label + ": the lock is " + locked + ", so the engine refuses it.");
                if (locked == HouseMoveLock.NoActionsLeft)
                    Assert.That(result.reason, Is.EqualTo(WindowComplete), label + ": no actions left is the engine's own reason.");
                else
                    Assert.That(result.reason, Is.Not.EqualTo(WindowComplete), label + ": a refusal for the window's actions is never \"not now\".");
            }
            return locked;
        }

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind, string text, string tag) => new EpisodeCommand
        {
            id = "lock-" + tag + "-" + s.revision + "-" + kind + "-" + (text ?? ""), actorId = s.playerId, kind = kind, text = text,
            expectedRevision = s.revision, expectedPhase = s.phase,
        };

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
    }
}
