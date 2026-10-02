using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The final Head of Household's choice in words (UI-UX-PASS-PLAN Q0): the one control a column
    /// carries says both halves of the choice, "Take Maya · evict Taylor", and never reads like the
    /// other column's; the jurors' chips carry the names the house calls them and stand close, then a
    /// grudge, then no read; and a chip is filled only for a lean the player saw or was part of.
    /// Unity-free, so the dotnet subset runs it (Tools/SimulationTests).
    /// </summary>
    public sealed class FinalChoiceWordsTests
    {
        /// <summary>A Final 3 at the final eviction: the player as Head of Household with two finalists, three jurors.</summary>
        private static EpisodeState FinalThree(uint seed = 31)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 6 }, seed);
            s.week = 9;
            foreach (var juror in s.contestants.Where(c => !c.isPlayer).Take(3)) juror.status = ContestantStatus.Jury;
            s.phase = EpisodePhase.FinalEviction;
            s.hohId = s.playerId;
            return s;
        }

        private static FinalistRead.JurorLean Lean(string id, string lean, string certainty = FinalistRead.Confirmed) =>
            new FinalistRead.JurorLean { jurorId = id, name = id, lean = lean, certainty = certainty };

        [Test]
        public void TheCaptionSaysBothHalvesOfTheChoiceByFirstName()
        {
            Assert.That(FinalChoiceWords.Caption("Maya Hassan", "Taylor Kim"), Is.EqualTo("Take Maya · evict Taylor"));
            Assert.That(FinalChoiceWords.Caption("Dr. Will Kirby", "Taylor Kim"), Is.EqualTo("Take Will · evict Taylor"),
                "The first name as the house says it, past a title.");
            Assert.That(FinalChoiceWords.Caption("Taylor Kim", "Maya Hassan"), Is.Not.EqualTo(FinalChoiceWords.Caption("Maya Hassan", "Taylor Kim")),
                "The two columns' controls never read alike.");
        }

        [Test]
        public void FinalistsWhoShareAFirstNameAreNamedWhole()
        {
            string one = FinalChoiceWords.Caption("Maya Hassan", "maya Ortiz"), other = FinalChoiceWords.Caption("maya Ortiz", "Maya Hassan");
            Assert.That(one, Is.EqualTo("Take Maya Hassan · evict maya Ortiz"));
            Assert.That(other, Is.EqualTo("Take maya Ortiz · evict Maya Hassan"));
            Assert.That(one, Is.Not.EqualTo(other), "Whole names keep the two controls apart.");
        }

        [Test]
        public void TheCaptionToEvictIsTheOtherFinalistTaken()
        {
            var s = FinalThree();
            var others = FinalistRead.Others(s);
            Assert.That(others, Has.Count.EqualTo(2));
            var (a, b) = (others[0], others[1]);
            Assert.That(FinalChoiceWords.CaptionToEvict(s, b.id), Is.EqualTo(FinalChoiceWords.Caption(a.name, b.name)), "Evicting the second takes the first.");
            Assert.That(FinalChoiceWords.CaptionToEvict(s, a.id), Is.EqualTo(FinalChoiceWords.Caption(b.name, a.name)), "Evicting the first takes the second.");
            Assert.That(FinalChoiceWords.CaptionToEvict(s, a.id), Does.StartWith(FinalChoiceWords.TakeWord + FinalistRead.FirstName(b.name))
                .And.EndWith(FinalChoiceWords.EvictWord + FinalistRead.FirstName(a.name)));
            Assert.That(FinalChoiceWords.CaptionToEvict(s, s.playerId), Is.Null, "The player is not one of the two.");
            Assert.That(FinalChoiceWords.CaptionToEvict(s, FinalistRead.Jurors(s)[0].id), Is.Null, "Nor is a juror.");
            Assert.That(FinalChoiceWords.CaptionToEvict(null, a.id), Is.Null);
            Assert.That(FinalChoiceWords.CaptionToEvict(s, null), Is.Null);
        }

        [Test]
        public void ChipsCarryFirstNamesAndWholeNamesWhereTwoShareOne()
        {
            var names = new List<string> { "Maya Hassan", "Taylor Kim", "Maya Ortiz", "Dr. Will Kirby", "" };
            Assert.That(FinalChoiceWords.ChipNames(names), Is.EqualTo(new[] { "Maya Hassan", "Taylor", "Maya Ortiz", "Will", "" }));
            Assert.That(FinalChoiceWords.ChipNames(new List<string>()), Is.Empty);
            Assert.That(FinalChoiceWords.ChipNames(null), Is.Empty);
        }

        [Test]
        public void ChipsStandCloseThenAGrudgeThenNoReadInTheCastsOrder()
        {
            var leans = new[]
            {
                Lean("a", FinalistRead.Uncertain, FinalistRead.Unknown), Lean("b", FinalistRead.Bitter), Lean("c", FinalistRead.Support, FinalistRead.Suspected),
                Lean("d", FinalistRead.Bitter, FinalistRead.Suspected), Lean("e", FinalistRead.Support), Lean("f", FinalistRead.Uncertain, FinalistRead.Unknown),
            };
            Assert.That(FinalChoiceWords.Grouped(leans).Select(lean => lean.jurorId), Is.EqualTo(new[] { "c", "e", "b", "d", "a", "f" }));
            Assert.That(FinalChoiceWords.Grouped(null), Is.Empty);
            Assert.That(FinalChoiceWords.LeanOrder, Is.EqualTo(new[] { FinalistRead.Support, FinalistRead.Bitter, FinalistRead.Uncertain }));
            Assert.That(FinalChoiceWords.GroupWord(FinalistRead.Support), Is.EqualTo(FinalChoiceWords.CloseWord));
            Assert.That(FinalChoiceWords.GroupWord(FinalistRead.Bitter), Is.EqualTo(FinalChoiceWords.GrudgeWord));
            Assert.That(FinalChoiceWords.GroupWord(FinalistRead.Uncertain), Is.EqualTo(FinalChoiceWords.NoReadWord));
            Assert.That(FinalChoiceWords.LeanRank("anything else"), Is.EqualTo(FinalChoiceWords.LeanRank(FinalistRead.Uncertain)), "An unknown lean reads as no read.");
        }

        [Test]
        public void AChipIsFilledOnlyForALeanThePlayerSawOrWasPartOf()
        {
            Assert.That(FinalChoiceWords.Filled(FinalistRead.Support, FinalistRead.Confirmed), Is.True);
            Assert.That(FinalChoiceWords.Filled(FinalistRead.Bitter, FinalistRead.Confirmed), Is.True);
            Assert.That(FinalChoiceWords.Filled(FinalistRead.Support, FinalistRead.Suspected), Is.False, "Heard, not seen: outlined.");
            Assert.That(FinalChoiceWords.Filled(FinalistRead.Bitter, FinalistRead.Suspected), Is.False);
            Assert.That(FinalChoiceWords.Filled(FinalistRead.Uncertain, FinalistRead.Unknown), Is.False, "No read is never filled.");
            Assert.That(FinalChoiceWords.Filled(FinalistRead.Uncertain, FinalistRead.Confirmed), Is.False);
        }

        /// <summary>
        /// The chips read what the finalist read reads: a juror the finalist nominated on the public
        /// record is a grudge the player saw, a warm standing the player was told is closeness they
        /// heard, an alliance the player is in with both is closeness they were part of - and a juror
        /// with nothing on the record is no read, whatever the jury model holds.
        /// </summary>
        [Test]
        public void TheChipsAreThePlayersReadOfEachJuror()
        {
            var s = FinalThree();
            var finalist = FinalistRead.Others(s)[0];
            var jurors = FinalistRead.Jurors(s);
            s.ledger.power.Add(new PowerRow { week = 3, hohId = finalist.id, vetoHolderId = jurors[2].id, nominees = new List<string> { jurors[0].id, s.playerId } });
            s.ledger.standings.Add(new StandingRow { week = 4, fromId = jurors[1].id, toId = finalist.id, source = ClaimSource.Told, score = 40 });
            // The jury model's own warmth toward the finalist is not the player's to read.
            var model = s.relationships.FirstOrDefault(r => r.fromId == jurors[2].id && r.toId == finalist.id);
            if (model == null) s.relationships.Add(model = new RelationshipState { fromId = jurors[2].id, toId = finalist.id });
            model.score = 90;
            var grouped = FinalChoiceWords.Grouped(FinalistRead.Read(s, finalist.id).jurors);
            Assert.That(grouped.Select(lean => (lean.jurorId, lean.lean, lean.certainty)), Is.EqualTo(new[]
            {
                (jurors[1].id, FinalistRead.Support, FinalistRead.Suspected),
                (jurors[0].id, FinalistRead.Bitter, FinalistRead.Confirmed),
                (jurors[2].id, FinalistRead.Uncertain, FinalistRead.Unknown),
            }));
            Assert.That(grouped.Select(lean => FinalChoiceWords.Filled(lean.lean, lean.certainty)), Is.EqualTo(new[] { false, true, false }));

            s.alliances.Add(new AllianceState { id = "pact", name = "The Pact", active = true, members = new List<string> { s.playerId, finalist.id, jurors[2].id } });
            var withPact = FinalChoiceWords.Grouped(FinalistRead.Read(s, finalist.id).jurors);
            Assert.That(withPact.Select(lean => lean.jurorId), Is.EqualTo(new[] { jurors[1].id, jurors[2].id, jurors[0].id }), "Close first, in the cast's order.");
            Assert.That(withPact[1].certainty, Is.EqualTo(FinalistRead.Confirmed), "An alliance the player is in is closeness they were part of.");
            Assert.That(FinalChoiceWords.Filled(withPact[1].lean, withPact[1].certainty), Is.True);
        }

        [Test]
        public void TheEyebrowsSayTheJurysSizeAndWhoIsTaken()
        {
            Assert.That(FinalChoiceWords.JuryEyebrow(1), Is.EqualTo("JURY READ (1 JUROR)"));
            Assert.That(FinalChoiceWords.JuryEyebrow(13), Is.EqualTo("JURY READ (13 JURORS)"));
            Assert.That(FinalChoiceWords.BulletsEyebrow("Maya Hassan", "Taylor Kim"), Is.EqualTo("IF YOU TAKE MAYA"));
            Assert.That(FinalChoiceWords.BulletsEyebrow("Dr. Will Kirby", "Taylor Kim"), Is.EqualTo("IF YOU TAKE WILL"), "Past a title, as the caption.");
            Assert.That(FinalChoiceWords.FallbackCaption("Maya Hassan"), Is.EqualTo("Evict Maya Hassan"));
        }

        /// <summary>With nobody on the jury yet, the eyebrow over the line that says so counts nobody.</summary>
        [Test]
        public void AnEmptyJurysEyebrowCountsNobody()
        {
            Assert.That(FinalChoiceWords.JuryEyebrow(0), Is.EqualTo("JURY READ"));
            Assert.That(FinalChoiceWords.JuryEyebrow(-1), Is.EqualTo("JURY READ"));
            Assert.That(FinalChoiceWords.JuryEyebrow(0), Does.Not.Contain("0"));
        }

        /// <summary>
        /// Finalists who share a first name are named whole everywhere the page names them - the
        /// caption, the bullets' eyebrow - so nothing that names one reads as naming the other.
        /// </summary>
        [Test]
        public void TheBulletsEyebrowNamesWholeFinalistsWhoShareAFirstName()
        {
            Assert.That(FinalChoiceWords.BulletsEyebrow("Maya Hassan", "maya Ortiz"), Is.EqualTo("IF YOU TAKE MAYA HASSAN"));
            Assert.That(FinalChoiceWords.BulletsEyebrow("maya Ortiz", "Maya Hassan"), Is.EqualTo("IF YOU TAKE MAYA ORTIZ"));
            Assert.That(FinalChoiceWords.Names("Maya Hassan", "maya Ortiz"), Is.EqualTo(("Maya Hassan", "maya Ortiz")));
            Assert.That(FinalChoiceWords.Names("Maya Hassan", "Taylor Kim"), Is.EqualTo(("Maya", "Taylor")));
            Assert.That(FinalChoiceWords.BulletsEyebrow("Maya Hassan", null), Is.EqualTo("IF YOU TAKE MAYA"), "With nobody to share a name with, the first name.");
        }

        /// <summary>What breaks whichever finalist is taken, said once: nothing for nobody, then one agreement or several.</summary>
        [Test]
        public void TheEitherWayLineNamesTheJurorsWhoseAgreementsBreak()
        {
            Assert.That(FinalChoiceWords.EitherWay(new List<string>()), Is.Null);
            Assert.That(FinalChoiceWords.EitherWay(null), Is.Null);
            Assert.That(FinalChoiceWords.EitherWay(new List<string> { "Riley" }),
                Is.EqualTo("Either way, your Final 2 agreement with Riley (on the jury) will end broken."));
            Assert.That(FinalChoiceWords.EitherWay(new List<string> { "Riley", "Jamie" }),
                Is.EqualTo("Either way, your Final 2 agreements with Riley and Jamie (on the jury) will end broken."));
        }
    }
}
