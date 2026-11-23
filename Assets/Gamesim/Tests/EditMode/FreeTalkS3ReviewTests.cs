using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Free talk slice 3's review fixes (talk rules 3), Unity-free: a housemate already in a showmance with someone else is never
    /// offered a Flirt, never ready and never made a couple with the player; an undecided housemate of the Guarded class never
    /// declares, as a Guarded regular never does; the panel resumes an open scene only in its own window and draws no empty
    /// opener; and every All-Star's refusal says no plainly, and their house reactions speak of the game only.
    /// </summary>
    public sealed class FreeTalkS3ReviewTests
    {
        private static IDisposable RomanceOnly() => TalkBank.Use(FreeTalkS3Fixture.Romance());

        [Test]
        public void AHousemateInAnotherShowmanceIsNeverOfferedAFlirtNeverReadyAndNeverACouple()
        {
            using (RomanceOnly())
            {
                var s = FreeTalkS3Fixture.WithInterested(TalkPools.Spark, TalkPools.Spark, out string npc, week: 2);
                var contact = FreeTalkS3Fixture.Flirting(s, npc, spark: 80, rapport: 30, flirts: 2, views: 70);
                var declaration = TalkRomance.DueTalk(s, npc);
                var flirtRow = TalkRomance.FlirtLine(s, npc);
                Assert.That(declaration?.Kind, Is.EqualTo(TalkKind.Declaration), "The control: their declaration is due.");
                Assert.That(TalkRomance.Ready(s, npc), Is.True, "The control: ready.");
                Assert.That(TalkRomance.FlirtOpen(s, npc), Is.True, "The control: Flirt is open.");
                Assert.That(flirtRow, Is.Not.Null, "The control: a Flirt line.");

                string other = FreeTalkFixture.Npcs(s).First(c => c.id != npc).id;
                Assert.That(Bonds.Form(s, npc, other, BondKinds.Showmance, BondStatus.Private, null), Is.Not.Null, "The control: the house's own pair.");
                Assert.That(TalkRomance.InAnotherShowmance(s, npc), Is.True);
                Assert.That(TalkRomance.InAnotherShowmance(s, s.playerId), Is.False, "The player holds no showmance.");
                Assert.That(TalkRomance.FlirtOpen(s, npc), Is.False, "No Flirt with someone in another showmance.");
                Assert.That(TalkRomance.Ready(s, npc), Is.False, "Never ready while their showmance holds.");
                Assert.That(TalkRomance.DueTalk(s, npc)?.Kind, Is.Not.EqualTo(TalkKind.Declaration), "They do not declare.");
                Assert.That(TalkRomance.ChoicesNow(s, npc, flirtRow), Has.No.Member(TalkChoice.MakeItOfficial));
                Assert.That(TalkRomance.ChoicesNow(s, npc, declaration), Has.No.Member(TalkChoice.Accept), "No Accept.");
                Assert.That(TalkRomance.ChoicesNow(s, npc, declaration), Has.Member(TalkChoice.JustFriends), "Just friends stays.");

                // A declaration left open in this window before their pair formed cannot be accepted.
                contact.talkPending = declaration.Id;
                contact.talkWindow = FreeTalk.WindowKey(s);
                FreeTalkS3Fixture.Refused(new EpisodeEngine(s), npc, TalkRomance.CommandText(declaration.Id, TalkChoices.Answer(TalkChoice.Accept)));

                // The engine's last guard: forming the couple itself is refused and makes no bond.
                var form = typeof(EpisodeEngine).GetMethod("FormCouple", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.That(form, Is.Not.Null, "The control: the engine's couple step.");
                Assert.That(() => form.Invoke(null, new object[] { s, s.Find(npc), contact, new TalkResult() }),
                    Throws.InstanceOf<TargetInvocationException>());
                Assert.That(Bonds.Between(s, s.playerId, npc, BondKinds.Showmance), Is.Null, "No second showmance.");

                Bonds.End(s, npc, other, BondKinds.Showmance, false);
                contact.talkPending = "";
                Assert.That(TalkRomance.Ready(s, npc), Is.True, "Their showmance ended: ready again.");
                Assert.That(TalkRomance.FlirtOpen(s, npc), Is.True, "Their showmance ended: Flirt is open again.");
            }
        }

        [Test]
        public void AnUndecidedHousemateOfTheGuardedClassNeverDeclares()
        {
            using (RomanceOnly())
            {
                string template = FreeTalkS3Fixture.TemplateWith(RomanceStance.Undecided);
                for (uint seed = 3111u; seed < 3171u; seed++)
                {
                    int count = FreeTalkFixture.Npcs(FreeTalkS3Fixture.Fresh(seed)).Count;
                    for (int i = 0; i < count; i++)
                    {
                        var s = FreeTalkS3Fixture.Fresh(seed);
                        string npc = FreeTalkFixture.Npcs(s)[i].id;
                        FreeTalkS3Fixture.AsTemplate(s, npc, template);
                        var contact = FreeTalkS3Fixture.Flirting(s, npc, spark: TalkRomance.MostSpark, rapport: 60, flirts: 10, views: 80);
                        contact.agreement = 5;
                        s.week = 9;
                        bool Declarable() => TalkChemistry.Band(s, npc) != ChemistryBand.Grating && TalkRomance.Interested(s, npc) && TalkRomance.Ready(s, npc);

                        FreeTalkS3Fixture.Classes(s, npc, TalkPools.Spark, TalkPools.Steady);
                        if (!Declarable() || TalkRomance.DueTalk(s, npc)?.Kind != TalkKind.Declaration) continue;
                        FreeTalkS3Fixture.Classes(s, npc, TalkPools.Spark, TalkPools.Guarded);
                        if (!Declarable()) continue;

                        Assert.That(TalkRomance.StanceOf(s, npc), Is.EqualTo(RomanceStance.Undecided), "The control: undecided.");
                        Assert.That(contact.flirts, Is.GreaterThan(TalkRomance.UndecidedDeclaresAfter), "The control: well past the undecided count.");
                        Assert.That(TalkRomance.DueTalk(s, npc)?.Kind, Is.Not.EqualTo(TalkKind.Declaration),
                            "An undecided Guarded housemate never declares, as a Guarded regular never does.");
                        return;
                    }
                }
                Assert.Fail("No season had an undecided housemate who would declare as Steady and is ready as Guarded.");
            }
        }

        [Test]
        public void AnOpenSceneIsDrawnOnlyInItsOwnWindowAndAnEmptyOpenerDrawsNoLine()
        {
            using (RomanceOnly())
            {
                var s = FreeTalkS3Fixture.Fresh();
                string npc = FreeTalkFixture.Npcs(s).First().id;
                int key = FreeTalk.WindowKey(s);
                Assert.That(key, Is.Not.EqualTo(0), "The control: a talk window.");
                var contact = FreeTalkFixture.ContactOf(s, npc);
                string declare = TalkPools.Spark + ".rt.declare";
                Assert.That(TalkBank.Find(declare), Is.Not.Null, "The control: the fixture's declaration row.");
                contact.talkPending = declare;
                contact.talkWindow = key;
                uint random = s.randomState;
                Assert.That(TalkScreenLines.PendingScene(s, npc), Is.Not.Null, "The control: drawn in its own window.");
                contact.talkWindow = key + 1;
                Assert.That(TalkScreenLines.PendingScene(s, npc), Is.Null, "A scene from another window is not drawn: the engine would refuse it.");

                var flirt = TalkBank.Find(TalkPools.Spark + ".rt.flirt");
                Assert.That(flirt, Is.Not.Null);
                Assert.That(TalkScreenLines.SceneOpener(s, npc, flirt, "Dana"), Is.Null, "A Flirt answered in kind opens with nothing: no 'Dana: ' line.");
                var row = TalkBank.Find(declare);
                string opener = TalkScreenLines.Opener(s, npc, row);
                Assert.That(opener, Is.Not.Empty);
                Assert.That(TalkScreenLines.SceneOpener(s, npc, row, "Dana"), Is.EqualTo("Dana: " + opener));
                Assert.That(s.randomState, Is.EqualTo(random), "Reading the screen draws nothing.");
            }
        }

        [Test]
        public void EveryRefusalSaysNoPlainlyAndEveryReactionSpeaksOfTheGame()
        {
            var reason = new Regex(@"\b(?:can't|cannot|got to|have to|need it|in here|in this house)\b", RegexOptions.IgnoreCase);
            var firstHand = new Regex(@"(?:\bI'd know\b|\btrust me\b|\bI've been\b|\bI've seen\b)", RegexOptions.IgnoreCase);
            var rows = TalkTables.StarTables.Values.SelectMany(t => t).ToList();
            string Words(TalkRow r) => string.Join(" ", new[] { r.Open, r.Second }.Concat((r.Replies ?? new TalkReply[0])
                .SelectMany(p => new[] { p.Line, p.Lands, p.Even, p.Grates })).Where(x => !string.IsNullOrEmpty(x)));

            var refusals = rows.Where(r => r.Kind == TalkKind.SoftNo || r.Kind == TalkKind.Refuses).ToList();
            var reactions = rows.Where(r => r.Kind == TalkKind.Reaction).ToList();
            Assert.That(refusals.Count, Is.GreaterThanOrEqualTo(24), "The control: every All-Star's soft no and refusal.");
            Assert.That(reactions.Count, Is.GreaterThanOrEqualTo(24), "The control: every All-Star's two house reactions.");
            Assert.That(reason.IsMatch("I can't. Not in this game."), Is.True, "The control: the pattern catches a hinted reason.");
            Assert.That(firstHand.IsMatch("Trust me, I've been the target."), Is.True, "The control: the pattern catches a claim.");
            Assert.That(refusals.Where(r => reason.IsMatch(Words(r))).Select(r => r.Id), Is.Empty, "A refusal says no plainly, with no reason.");
            Assert.That(reactions.Where(r => firstHand.IsMatch(Words(r))).Select(r => r.Id), Is.Empty, "A pair is a target, said of the game only.");
        }
    }
}
