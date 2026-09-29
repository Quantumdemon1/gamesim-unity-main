using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The words the end screens and the status line are read by (MOCKUP-PASS-PLAN M1): a phase
    /// said in words rather than by its enum's name, the veto's two cards saying who plays and
    /// which way the meeting went, a quote that never repeats the one beside it, and standings
    /// that print one place each wherever the record can order them.
    /// </summary>
    public sealed class EndScreenCopyTests
    {
        [Test]
        public void PhaseLine_SaysEveryPhaseInWords()
        {
            Assert.That(EpisodeDirector.PhaseLine(EpisodePhase.FinalHoHPart2, 4), Is.EqualTo("Week 4 · Final HoH, Part 2 of 3"));
            Assert.That(EpisodeDirector.PhaseLine(EpisodePhase.FinalEviction, 4), Is.EqualTo("Week 4 · The final decision"));
            Assert.That(EpisodeDirector.PhaseLine(EpisodePhase.JuryQuestioning, 4), Is.EqualTo("Jury questioning"));
            Assert.That(EpisodeDirector.PhaseLine(EpisodePhase.VetoMeeting, 2), Is.EqualTo("Week 2 · Veto meeting"),
                "An ordinary week's phase is the week chip's word under its week.");
            foreach (EpisodePhase phase in System.Enum.GetValues(typeof(EpisodePhase)))
            {
                string line = EpisodeDirector.PhaseLine(phase, 4);
                Assert.That(line, Is.Not.Null.And.Not.Empty, phase + " has a line.");
                // A name run together from two words or more is the enum's, never the house's.
                if (phase != EpisodePhase.HoH && phase.ToString().Skip(1).Any(char.IsUpper))
                    Assert.That(line, Does.Not.Contain(phase.ToString()), phase + " is said in words.");
            }
        }

        [Test]
        public void StatusLine_WordsAPhaseAndLeavesEveryOtherEventAsLogged()
        {
            var state = ContentCatalog.Create(20260929u);
            var phase = new EpisodeEvent { week = 4, phase = EpisodePhase.FinalHoHPart2, kind = "phase", text = "Week 4 · FinalHoHPart2" };
            Assert.That(EpisodeDirector.StatusLine(state, phase), Is.EqualTo("Week 4 · Final HoH, Part 2 of 3"));
            Assert.That(phase.text, Is.EqualTo("Week 4 · FinalHoHPart2"), "The logged text is left as it was.");
            var veto = new EpisodeEvent { week = 4, phase = EpisodePhase.VetoMeeting, kind = "veto",
                text = "Maya declines to use the veto. Nominations stand." };
            Assert.That(EpisodeDirector.StatusLine(state, veto), Is.EqualTo(veto.text));
            Assert.That(EpisodeDirector.StatusLine(state, null), Is.Null, "No event, no line: the caller says the decision committed.");
        }

        [Test]
        public void VetoCards_SayWhoPlaysAndWhichWayTheMeetingWent()
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 11u);
            var cast = state.contestants.Select(c => c.id).ToList();
            state.hohId = cast[1];
            state.nominees = new List<string> { cast[2], cast[3] };
            state.vetoPlayers = new List<string> { cast[1], cast[2], cast[3], cast[4], cast[5], cast[6] };
            Assert.That(EpisodeDirector.VetoFieldLine(state), Is.EqualTo(
                "Six play for the Golden Power of Veto: the Head of Household, both nominees and three drawn from the house."));

            // Six left in the house: everyone plays, and the card keeps its own line.
            foreach (var id in cast.Skip(6)) state.Find(id).status = ContestantStatus.Jury;
            state.vetoPlayers = cast.Take(6).ToList();
            Assert.That(EpisodeDirector.VetoFieldLine(state), Is.Null);

            var block = new[] { cast[2], cast[3] };
            Assert.That(EpisodeDirector.VetoMeetingLine(block, new[] { cast[3], cast[2] }), Is.EqualTo(EpisodeDirector.VetoNotUsedLine),
                "The same two on the block, in any order: the veto was not used.");
            Assert.That(EpisodeDirector.VetoMeetingLine(block, new[] { cast[3], cast[5] }), Is.EqualTo(EpisodeDirector.VetoUsedLine),
                "One down and a replacement up: it was.");
            Assert.That(EpisodeDirector.VetoNotUsedLine, Is.EqualTo("Veto not used: the nominations stay the same."));
            Assert.That(EpisodeDirector.VetoUsedLine, Is.EqualTo("Veto used: a nominee is removed and a replacement is named."));
        }

        [Test]
        public void ExcerptBeside_NeverRepeatsTheQuoteBesideIt()
        {
            const string winner = "I played every week like it was my last. I won when I had to.";
            Assert.That(EndScreenKit.Excerpt(winner), Is.EqualTo("I played every week like it was my last."));
            Assert.That(EndScreenKit.ExcerptBeside("I played every week like it was my last. I kept my word.", 80, winner),
                Is.EqualTo("I kept my word."), "The same opening: the next sentence.");
            Assert.That(EndScreenKit.ExcerptBeside("I came here to win! And I nearly did.", 80, winner),
                Is.EqualTo("I came here to win!"), "A speech of its own keeps its first line.");
            Assert.That(EndScreenKit.ExcerptBeside("I played every week like it was my last.", 80, winner),
                Is.Null, "The same opening and nothing after it: no quote.");
            Assert.That(EndScreenKit.ExcerptBeside("I played every week like it was my last.\nI played every week like it was my last.", 80, winner),
                Is.Null, "A next sentence that repeats the shown one: no quote either.");
            Assert.That(EndScreenKit.ExcerptBeside(null, 80, winner), Is.Null);
            Assert.That(EndScreenKit.ExcerptBeside("I kept my word.", 80), Is.EqualTo("I kept my word."), "Nothing shown beside it: its first line.");

            // Cut as the plain excerpt cuts, at a word under the limit.
            const string long_ = "Every single week of this game I sat across from people who wanted me gone and talked them out of it one by one.";
            Assert.That(EndScreenKit.ExcerptBeside(long_, 80, winner), Is.EqualTo(EndScreenKit.Excerpt(long_, 80)));
            Assert.That(EndScreenKit.Excerpt(long_, 80), Does.EndWith("…").And.Length.LessThanOrEqualTo(80));
        }

        /// <summary>
        /// A finished house of eight, built rather than played: the winner, the runner-up, five
        /// jurors and one out before the jury, each evictee's week in the ledger's power rows, and no
        /// jury ledger, so every placement is the fallback's. <paramref name="leftInOrder"/> is who
        /// left, first to last.
        /// </summary>
        private static EpisodeState EightWithoutAJuryLedger(out List<ContestantState> leftInOrder)
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 5u);
            var cast = state.contestants.ToList();
            cast[1].status = ContestantStatus.Winner; state.winnerId = cast[1].id;
            cast[2].status = ContestantStatus.RunnerUp; state.runnerUpId = cast[2].id;
            leftInOrder = new List<ContestantState> { cast[7], cast[3], cast[0], cast[6], cast[4], cast[5] };
            for (int week = 1; week <= leftInOrder.Count; week++)
            {
                var gone = leftInOrder[week - 1];
                gone.status = week == 1 ? ContestantStatus.Evicted : ContestantStatus.Jury;
                state.ledger.power.Add(new PowerRow { week = week, hohId = cast[1].id, evicteeId = gone.id, tally = new List<int> { 2, 1 } });
            }
            state.week = leftInOrder.Count;
            state.phase = EpisodePhase.Finished;
            return state;
        }

        [Test]
        public void StandingsOrder_BreaksTheFallbacksTieByTheWeekEachLeft()
        {
            var state = EightWithoutAJuryLedger(out var left);
            Assert.That(left.Skip(1).Select(c => CareerLedger.Placement(state, c)), Is.All.EqualTo(7),
                "The fallback alone seats all five jurors seventh.");

            var order = SeasonReport.StandingsOrder(state);
            Assert.That(order.Select(e => e.place), Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
            Assert.That(order.Select(e => e.who.id), Is.EqualTo(new[] { state.winnerId, state.runnerUpId }
                .Concat(Enumerable.Reverse(left).Select(c => c.id))), "The latest to leave finished highest.");

            // Two who left in the same week share the lowest place of theirs; the rest keep their own.
            state.ledger.power.Single(p => p.evicteeId == left[2].id).week = left.IndexOf(left[3]) + 1;
            Assert.That(SeasonReport.StandingsOrder(state).Select(e => e.place), Is.EqualTo(new[] { 1, 2, 3, 4, 6, 6, 7, 8 }));

            // With no row to order them by, the coarse count stands as it always has.
            state.ledger.power.Clear();
            Assert.That(SeasonReport.StandingsOrder(state).Select(e => e.place), Is.EqualTo(new[] { 1, 2, 7, 7, 7, 7, 7, 8 }));
        }

        [Test]
        public void StandingsOrder_KeepsTheSharedNumberWhereARunWouldLeaveTheHouse()
        {
            // A save that gained the jury ledger part way through the season (a version 1 or 2 save
            // migrates with an empty one): only the last three to leave are on it, so the ledger's
            // places mix with the fallback's, and seventh and eighth are each shared.
            var state = EightWithoutAJuryLedger(out var left);
            foreach (var gone in left.Skip(3)) state.jurySentiment = WebJurySentiment.AddJuror(state.jurySentiment, gone.id, gone.name, 0);
            var coarse = state.contestants.ToDictionary(c => c.id, c => CareerLedger.Placement(state, c));
            Assert.That(coarse.Values.Count(place => place == 7), Is.EqualTo(3), "Two off the ledger and one on it share seventh.");
            Assert.That(coarse.Values.Count(place => place == 8), Is.EqualTo(2), "and one of each share eighth.");

            // Seventh's run would reach eighth and eighth's would pass the house: both keep the number.
            var places = SeasonReport.StandingsOrder(state).Select(e => (e.who.id, e.place)).ToList();
            Assert.That(places.Select(e => e.place), Is.All.InRange(1, state.contestants.Count), "No place past the foot of the house.");
            Assert.That(places.Select(e => e.place), Is.Ordered, "No place out of order.");
            Assert.That(places.Select(e => e.place), Is.EqualTo(places.Select(e => coarse[e.id])),
                "A group whose run does not fit prints the number it shares: " + string.Join(", ", places.Select(e => e.place)));
        }

        [Test]
        public void StandingsOrder_ReadsTheJuryLedgerWhenThereIsOne()
        {
            var state = EightWithoutAJuryLedger(out var left);
            foreach (var gone in left) state.jurySentiment = WebJurySentiment.AddJuror(state.jurySentiment, gone.id, gone.name, 0);
            // The jury ledger alone orders them: no power rows to lean on.
            state.ledger.power.Clear();
            var order = SeasonReport.StandingsOrder(state);
            Assert.That(order.Select(e => e.place), Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
            Assert.That(order.Select(e => e.who.id), Is.EqualTo(new[] { state.winnerId, state.runnerUpId }
                .Concat(Enumerable.Reverse(left).Select(c => c.id))));
        }
    }
}
