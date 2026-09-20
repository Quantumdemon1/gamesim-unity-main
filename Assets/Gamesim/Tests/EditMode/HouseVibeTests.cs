using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The four-bar mood readout mockup-04 draws (VISUAL-TARGET.md V2).
    ///
    /// <para>It is a readout, so the risk it carries is not misbehaviour but misinformation: a bar
    /// that counts the wrong week, or counts scheming the player was never told about, tells them
    /// something their character does not know. Those two are the tests that matter here; the rest
    /// pin the arithmetic so the bars can be read against each other.</para>
    /// </summary>
    public sealed class HouseVibeTests
    {
        private static EpisodeState Fresh()
        {
            var state = ContentCatalog.Create(7);
            state.events.Clear();
            return state;
        }

        private static void Log(EpisodeState state, string kind, int week, params string[] audience)
        {
            var entry = new EpisodeEvent
            {
                sequence = state.nextSequence++, week = week, phase = state.phase, kind = kind,
                text = kind + " in week " + week,
            };
            entry.audienceIds.AddRange(audience);
            state.events.Add(entry);
        }

        [Test]
        public void AnEmptyWeekReadsAsSettledAndDrawsNothing()
        {
            var reading = HouseVibe.Of(Fresh());
            Assert.That(reading.Fun, Is.Zero);
            Assert.That(reading.Trust, Is.Zero);
            Assert.That(reading.Drama, Is.Zero);
            Assert.That(reading.Harmony, Is.Zero);
            Assert.That(reading.Peak, Is.EqualTo(1), "The peak never divides by zero.");
            Assert.That(reading.Fraction(0), Is.EqualTo(0f));
            Assert.That(HouseVibe.Tension(reading), Is.EqualTo("Settled"));
        }

        [Test]
        public void EachKindLandsInOneBarAndHarmonyIsWhatIsLeft()
        {
            var state = Fresh();
            Log(state, "conversation", state.week);
            Log(state, "house-event", state.week);
            Log(state, "competition", state.week);
            Log(state, "alliance", state.week);
            Log(state, "promise", state.week);
            Log(state, "nomination", state.week);

            var reading = HouseVibe.Of(state);
            Assert.That(reading.Fun, Is.EqualTo(3));
            Assert.That(reading.Trust, Is.EqualTo(2));
            Assert.That(reading.Drama, Is.EqualTo(1));
            Assert.That(reading.Harmony, Is.EqualTo(4), "Harmony is the week's goodwill less its drama.");
            Assert.That(reading.Peak, Is.EqualTo(4), "Bars are drawn against the largest of the four.");
            Assert.That(reading.Fraction(4), Is.EqualTo(1f));
            Assert.That(reading.Fraction(2), Is.EqualTo(.5f));
        }

        [Test]
        public void AWeekOfNothingButDramaHasNoHarmonyLeftAndSaysSo()
        {
            var state = Fresh();
            Log(state, "nomination", state.week);
            Log(state, "rumour", state.week);
            Log(state, "backdoor", state.week);
            Log(state, "conversation", state.week);

            var reading = HouseVibe.Of(state);
            Assert.That(reading.Drama, Is.EqualTo(3));
            Assert.That(reading.Harmony, Is.Zero, "Harmony floors at nothing rather than going negative.");
            Assert.That(HouseVibe.Tension(reading), Is.EqualTo("High tension"));
        }

        [Test]
        public void LastWeekIsNotThisWeek()
        {
            var state = Fresh();
            state.week = 3;
            Log(state, "conversation", 1);
            Log(state, "nomination", 2);
            Log(state, "alliance", 3);

            var reading = HouseVibe.Of(state);
            Assert.That(reading.Fun, Is.Zero, "A conversation two weeks ago is not this week's mood.");
            Assert.That(reading.Drama, Is.Zero);
            Assert.That(reading.Trust, Is.EqualTo(1));
        }

        [Test]
        public void WhatThePlayerWasNeverToldIsNotCounted()
        {
            var state = Fresh();
            var other = state.contestants.Find(actor => actor.id != state.playerId).id;
            Log(state, "scheme", state.week, other);                       // private to someone else
            Log(state, "rumour", state.week, other, state.playerId);       // the player was there
            Log(state, "conversation", state.week);                        // public

            var reading = HouseVibe.Of(state);
            Assert.That(reading.Drama, Is.EqualTo(1),
                "A bar that summed scheming the player never saw would hand them what their "
                + "character does not know, which is the one way a readout can cheat.");
            Assert.That(reading.Fun, Is.EqualTo(1));
        }

        [Test]
        public void TheFourRowsAreTheMockupsFourInItsOrder()
        {
            var rows = HouseVibe.Of(Fresh()).Rows();
            Assert.That(System.Linq.Enumerable.Select(rows, row => row.Word),
                Is.EqualTo(new[] { "Fun", "Trust", "Drama", "Harmony" }));
        }
    }
}
