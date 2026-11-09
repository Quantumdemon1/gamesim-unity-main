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
            const string Tuned = "Study preparation: saved between weeks; " + TunedUse + ". A confirmed study raises it, up to 1.";
            Assert.That(StudyWords.RulesLine(small), Is.EqualTo(Tuned));
            // The final week of a house of eight: three remain, and a study still fills it to one. Tuning 1 dropped the small
            // house's boost (the final week's two, and a watch adding one) before it shipped, so no line names either.
            foreach (var gone in small.contestants.Where(c => !c.isPlayer).Skip(2)) gone.status = ContestantStatus.Evicted;
            Assert.That(small.Active.Count(), Is.EqualTo(3));
            Assert.That(StudyWords.RulesLine(small), Is.EqualTo(Tuned));

            var big = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 12 }, 1709u);
            big.tuningRulesVersion = TuningRules.Bands;
            Assert.That(StudyWords.RulesLine(big), Is.EqualTo(Tuned), "A house of nine to twelve reads the same.");
            foreach (var gone in big.contestants.Where(c => !c.isPlayer).Skip(2)) gone.status = ContestantStatus.Evicted;
            Assert.That(StudyWords.RulesLine(big), Is.EqualTo(Tuned), "Its final week too.");
            foreach (var line in new[] { StudyWords.Use(small), StudyWords.RulesLine(small), StudyWords.RulesLine(big) })
            {
                Assert.That(line, Does.Not.Contain("5"), "No tuned line names tuning 0's five.");
                Assert.That(line, Does.Not.Contain("watch").And.Not.Contain("up to 2"), "No tuned line names the dropped boost.");
            }
        }

        /// <summary>
        /// Under tuning 1 the study section's words and the record row's say what is left to use preparation in: the next
        /// weekly Head of Household or veto, unless it is thrown; in the final week, when three remain and the final Head of
        /// Household follows the free time directly (EpisodeEngine's Advance out of Social), its three parts; and once those
        /// are over, or for a player out of the house, nothing. Tuning 0's record row reads as it always did in all of them.
        /// </summary>
        [Test]
        public void UnderTuningOneTheStudyWordsSayWhatIsLeftToUsePreparationIn()
        {
            const string Weekly = "Used up by your next weekly competition.", Final = "Counts in the final HoH's three parts.",
                None = "No competition is left to use it in.";
            foreach (int size in new[] { 8, 12 })
            {
                var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size }, 1709u);
                s.tuningRulesVersion = TuningRules.Bands;
                s.phase = EpisodePhase.Social;
                Assert.That(StudyWords.UseShort(s), Is.EqualTo(Weekly), "At " + size);
                Assert.That(StudyWords.FullLine(s), Is.EqualTo("Your preparation is full. The next weekly HoH or veto competition you enter uses it up, unless you throw it."));
                Assert.That(StudyWords.StudyAside(s), Is.EqualTo("Preparation is used up by the next weekly HoH or veto you enter, unless you throw it; a study changes it only when you confirm."));
                Assert.That(TuningRules.FullRefusal(s), Is.EqualTo("Your preparation is full. It is used in the next Head of Household or veto competition you enter."));

                var juror = s.Clone();
                juror.Find(juror.playerId).status = ContestantStatus.Jury;
                Assert.That(StudyWords.UseShort(juror), Is.EqualTo(None), "A juror has no competition left, at " + size);

                // The final week: three remain, and the final Head of Household is all that is left.
                foreach (var gone in s.contestants.Where(c => !c.isPlayer).Skip(2)) gone.status = ContestantStatus.Evicted;
                Assert.That(s.Active.Count(), Is.EqualTo(3));
                Assert.That(StudyWords.UseShort(s), Is.EqualTo(Final), "At " + size);
                Assert.That(StudyWords.FullLine(s), Is.EqualTo("Your preparation is full. All three parts of the final Head of Household count it."));
                Assert.That(StudyWords.StudyAside(s), Is.EqualTo("Preparation counts in all three parts of the final HoH; a study changes it only when you confirm."));
                Assert.That(TuningRules.FullRefusal(s), Is.EqualTo("Your preparation is full. All three parts of the final Head of Household count it."));
                foreach (var phase in new[] { EpisodePhase.FinalHoHPart1, EpisodePhase.FinalHoHPart2, EpisodePhase.FinalHoHPart3 })
                {
                    s.phase = phase;
                    Assert.That(StudyWords.UseShort(s), Is.EqualTo(Final), phase + " at " + size);
                }
                foreach (var phase in new[] { EpisodePhase.FinalEviction, EpisodePhase.JuryQuestioning, EpisodePhase.FinalSpeeches, EpisodePhase.Jury, EpisodePhase.Finished })
                {
                    s.phase = phase;
                    Assert.That(StudyWords.UseShort(s), Is.EqualTo(None), phase + " at " + size);
                }

                foreach (var held in new[] { s, juror })
                {
                    held.tuningRulesVersion = TuningRules.Off;
                    Assert.That(StudyWords.UseShort(held), Is.EqualTo("Used in every competition you enter."), "Tuning 0's row, at " + size);
                }
            }
        }
    }
}
