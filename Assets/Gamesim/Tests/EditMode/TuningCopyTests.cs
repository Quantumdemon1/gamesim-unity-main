using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// B9's copy in the diary room (B9-BALANCE-PLAN §4.7): every line that names the study's limit or what uses preparation
    /// reads a season before B9's tuning exactly as it always did, and a season on tuning 1 by its own limit. The record's
    /// row and the study section count out of <see cref="TuningRules.PreparationOutOf"/>, which TuningRulesTests pins; these
    /// are the sentences around them (<see cref="StudyWords"/>, which the director's StudyUse and StudyUseShort read).
    /// </summary>
    public sealed class TuningCopyTests
    {
        private const string RulesThreeUse = "used in every competition you enter, however you play it, the final HoH's three parts included";
        private const string TunedUse = "used up by the next weekly HoH or veto competition you enter, unless you throw it; the final HoH's three parts all count it";

        [Test]
        public void BeforeB9TheDiarysStudyLinesReadAsTheyAlwaysDid()
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 1709u);
            Assert.That(s.tuningRulesVersion, Is.Zero, "A built season is tuning 0's.");
            Assert.That(s.competitionRulesVersion, Is.GreaterThanOrEqualTo(3));
            Assert.That(StudyWords.Use(s), Is.EqualTo(RulesThreeUse));
            Assert.That(StudyWords.UseShort(s), Is.EqualTo("Used in every competition you enter."));
            Assert.That(StudyWords.RulesLine(s), Is.EqualTo("Study preparation: saved between weeks; " + RulesThreeUse
                + ". It stays between 0 and 5 and changes only when you confirm a study approach."));
            s.competitionRulesVersion = 2;
            Assert.That(StudyWords.RulesLine(s), Is.EqualTo("Study preparation: saved between weeks; used only when you choose to simulate "
                + "a weekly HoH or veto competition, not when you play one yourself or in the final HoH. It stays between 0 and 5 and changes only "
                + "when you confirm a study approach."));
            Assert.That(StudyWords.UseShort(s), Is.EqualTo("Used only when you simulate a weekly competition."));
        }

        [Test]
        public void UnderTuningOneTheDiarysStudyLinesNameTheSeasonsOwnLimitAndWhatUsesItUp()
        {
            var small = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 1709u);
            small.tuningRulesVersion = TuningRules.Bands;
            Assert.That(StudyWords.Use(small), Is.EqualTo(TunedUse));
            Assert.That(StudyWords.UseShort(small), Is.EqualTo("Used up by your next weekly competition."));
            Assert.That(StudyWords.RulesLine(small), Is.EqualTo("Study preparation: saved between weeks; " + TunedUse
                + ". A confirmed study raises it, up to 1; watching a Head of Household you sit out, with some banked, adds one, up to 2."));
            // The final week of a house of eight or fewer: three remain, and a study fills it to two.
            foreach (var gone in small.contestants.Where(c => !c.isPlayer).Skip(2)) gone.status = ContestantStatus.Evicted;
            Assert.That(small.Active.Count(), Is.EqualTo(3));
            Assert.That(StudyWords.RulesLine(small), Is.EqualTo("Study preparation: saved between weeks; " + TunedUse
                + ". A confirmed study raises it, up to 2; watching a Head of Household you sit out, with some banked, adds one, up to 2."));

            var big = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 12 }, 1709u);
            big.tuningRulesVersion = TuningRules.Bands;
            Assert.That(StudyWords.RulesLine(big), Is.EqualTo("Study preparation: saved between weeks; " + TunedUse
                + ". A confirmed study raises it, up to 1."), "A house of nine to twelve has no watch, and its final week fills to one.");
            foreach (var gone in big.contestants.Where(c => !c.isPlayer).Skip(2)) gone.status = ContestantStatus.Evicted;
            Assert.That(StudyWords.RulesLine(big), Does.EndWith("up to 1."));
            foreach (var line in new[] { StudyWords.Use(small), StudyWords.RulesLine(small), StudyWords.RulesLine(big) })
                Assert.That(line, Does.Not.Contain("5"), "No tuned line names tuning 0's five.");
        }
    }
}
