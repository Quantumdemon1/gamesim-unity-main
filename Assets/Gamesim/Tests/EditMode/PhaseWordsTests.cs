using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The one table of a phase's words (UI-UX plan D15, the menu review's change 7): the week chip's
    /// word, unchanged, and the news line the status line says in place of "Week 4 · Nominations",
    /// which only repeated the chip. Unity-free, so the dotnet subset runs it (Tools/SimulationTests).
    /// </summary>
    public sealed class PhaseWordsTests
    {
        private static IEnumerable<EpisodePhase> AllPhases() => Enum.GetValues(typeof(EpisodePhase)).Cast<EpisodePhase>();

        /// <summary>The phases that carried a week on the status line before: each now leads with the chip's word.</summary>
        private static readonly EpisodePhase[] WeekPhases =
        {
            EpisodePhase.Social, EpisodePhase.HoH, EpisodePhase.Nomination, EpisodePhase.VetoSelection,
            EpisodePhase.Veto, EpisodePhase.VetoMeeting, EpisodePhase.Campaign, EpisodePhase.Eviction,
            EpisodePhase.FinalHoHPart1, EpisodePhase.FinalHoHPart2, EpisodePhase.FinalHoHPart3, EpisodePhase.FinalEviction,
        };

        [Test]
        public void EveryPhaseHasAChipWordAndANewsLineWithoutTheWeek()
        {
            foreach (var phase in AllPhases())
            {
                string chip = PhaseWords.Short(phase), news = PhaseWords.News(phase);
                Assert.That(chip, Is.Not.Null.And.Not.Empty, phase + " has a chip word.");
                Assert.That(chip.Length, Is.LessThanOrEqualTo(PhaseWords.MaxShortLength), phase + ": '" + chip + "'.");
                Assert.That(news, Is.Not.Null.And.Not.Empty, phase + " has a news line.");
                Assert.That(news.Length, Is.LessThanOrEqualTo(PhaseWords.MaxNewsLength), phase + ": '" + news + "'.");
                Assert.That(news, Does.Not.StartWith("Week "), phase + ": the week is the chip's.");
                Assert.That(news, Does.Not.Contain("Week"), phase + ": the week is the chip's.");
                // A name run together from two words or more is the enum's, never the house's.
                if (phase != EpisodePhase.HoH && phase.ToString().Skip(1).Any(char.IsUpper))
                    Assert.That(news, Does.Not.Contain(phase.ToString()), phase + " is said in words.");
            }
            Assert.That(PhaseWords.MaxNewsLength, Is.LessThanOrEqualTo(70), "The status line's room.");
        }

        [Test]
        public void AWeeksPhaseLeadsWithTheChipsWordAndTheFinaleKeepsItsWords()
        {
            foreach (var phase in WeekPhases)
            {
                string chip = PhaseWords.Short(phase), news = PhaseWords.News(phase);
                Assert.That(news.StartsWith(chip + ":", StringComparison.Ordinal) || news.StartsWith(chip + ",", StringComparison.Ordinal),
                    Is.True, phase + ": the chip and the status line call it the same thing. It says '" + news + "'.");
                Assert.That(news.Length, Is.GreaterThan(chip.Length + 10), phase + " says what the phase is, not only its name.");
            }
            Assert.That(PhaseWords.News(EpisodePhase.Nomination), Is.EqualTo("Nominations: the Head of Household names two houseguests"));
            Assert.That(PhaseWords.News(EpisodePhase.VetoMeeting), Does.StartWith("Veto meeting: "), "One meeting, one name.");
            // The finale's beats never carried a week: their lines are as they were.
            Assert.That(PhaseWords.News(EpisodePhase.JuryQuestioning), Is.EqualTo("Jury questioning"));
            Assert.That(PhaseWords.News(EpisodePhase.FinalSpeeches), Is.EqualTo("Final speeches"));
            Assert.That(PhaseWords.News(EpisodePhase.Jury), Is.EqualTo("The jury votes"));
            Assert.That(PhaseWords.News(EpisodePhase.Finished), Is.EqualTo("The season is over"));
        }

        [Test]
        public void TheChipsWordsAreAsTheyWere()
        {
            var expected = new Dictionary<EpisodePhase, string>
            {
                [EpisodePhase.Social] = "Free time", [EpisodePhase.HoH] = "HoH comp", [EpisodePhase.Nomination] = "Nominations",
                [EpisodePhase.VetoSelection] = "Veto draw", [EpisodePhase.Veto] = "Veto comp", [EpisodePhase.VetoMeeting] = "Veto meeting",
                [EpisodePhase.Campaign] = "Campaign", [EpisodePhase.Eviction] = "Eviction",
                [EpisodePhase.FinalHoHPart1] = "Final HoH", [EpisodePhase.FinalHoHPart2] = "Final HoH", [EpisodePhase.FinalHoHPart3] = "Final HoH",
                [EpisodePhase.FinalEviction] = "Final eviction", [EpisodePhase.Jury] = "Jury", [EpisodePhase.JuryQuestioning] = "Jury",
                [EpisodePhase.FinalSpeeches] = "Jury", [EpisodePhase.Finished] = "Finale",
            };
            Assert.That(AllPhases().Count(), Is.EqualTo(expected.Count), "Every phase is in the table.");
            foreach (var phase in AllPhases())
                Assert.That(PhaseWords.Short(phase), Is.EqualTo(expected[phase]), phase + ": the week chip's text is unchanged.");
        }

        [Test]
        public void TheFinalHeadOfHouseholdsPartsAreDistinct()
        {
            var parts = new[] { EpisodePhase.FinalHoHPart1, EpisodePhase.FinalHoHPart2, EpisodePhase.FinalHoHPart3 };
            for (int i = 0; i < parts.Length; i++)
            {
                Assert.That(PhaseWords.PartLabel(parts[i]), Is.EqualTo("Part " + (i + 1) + " of 3"));
                Assert.That(PhaseWords.News(parts[i]), Does.StartWith("Final HoH, Part " + (i + 1) + " of 3: "));
            }
            Assert.That(parts.Select(PhaseWords.News).Distinct().Count(), Is.EqualTo(3), "Each part says who plays in it.");
            foreach (var phase in AllPhases().Except(parts))
                Assert.That(PhaseWords.PartLabel(phase), Is.Null, phase + " is no part of the final Head of Household.");
        }
    }
}
