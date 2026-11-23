using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Free talk slice 3 (talk rules 3): the three stances read by template roster (rulings R7, R14, R15, U5), the here-to-play
    /// turn-aside's three attempts (U6), the undecided stance's hold-back and its slower pace (U7), the closed regular's line, and
    /// the jury terms. The tests' own tables, all twelve templates' alike; Unity-free.
    /// </summary>
    public sealed class FreeTalkStanceTests
    {
        private static IDisposable Tables() => TalkBank.Use(FreeTalkS3Fixture.Table());

        [Test]
        public void TheStanceTableHoldsEveryAllStarTemplateExactlyOnceAndNothingElse()
        {
            var templates = CastTemplates.In(CastTemplates.Roster.AllStars).Select(t => t.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();
            Assert.That(templates.Count, Is.EqualTo(12), "The control: twelve All-Stars templates.");
            Assert.That(TalkStanceTable.ByTemplate.Keys.OrderBy(x => x, StringComparer.Ordinal).ToList(), Is.EqualTo(templates));
            Assert.That(TalkStanceTable.ByTemplate.Values.All(v => v == RomanceStance.HereToPlay || v == RomanceStance.Undecided || v == RomanceStance.Open),
                Is.True, "An All-Star is here to play, undecided or open.");
            Assert.That(TalkRomance.StanceOfTemplate("no-such-template"), Is.EqualTo(RomanceStance.HereToPlay), "A template the table lacks fails closed.");
        }

        [Test]
        public void AStanceIsReadByTemplateWhateverTheCardIsNamed()
        {
            var s = FreeTalkS3Fixture.Fresh();
            string npc = FreeTalkFixture.Npcs(s).First().id;
            foreach (var template in CastTemplates.In(CastTemplates.Roster.AllStars))
            {
                FreeTalkS3Fixture.AsTemplate(s, npc, template.Id);
                s.Find(npc).name = template.Name;
                var asIs = TalkRomance.StanceOf(s, npc);
                s.Find(npc).name = "Somebody Else";
                Assert.That(TalkRomance.StanceOf(s, npc), Is.EqualTo(asIs), template.Id + ": renaming the card keeps the stance.");
                Assert.That(asIs, Is.EqualTo(TalkRomance.StanceOfTemplate(template.Id)), template.Id + ": the table's.");
            }
        }

        [Test]
        public void EveryAllStarIsServedRomanceRowsOnlyFromTheirOwnTable()
        {
            using (Tables())
            {
                var s = FreeTalkS3Fixture.Fresh();
                string npc = FreeTalkFixture.Npcs(s).First().id;
                foreach (var template in CastTemplates.In(CastTemplates.Roster.AllStars))
                {
                    FreeTalkS3Fixture.AsTemplate(s, npc, template.Id);
                    Assert.That(TalkRomance.FlirtLine(s, npc).Owner, Is.EqualTo(template.Id), template.Id);
                    foreach (var kind in new[] { TalkKind.SoftNo, TalkKind.HoldBack, TalkKind.Accepts, TalkKind.Refuses })
                        Assert.That(TalkRomance.SceneRow(s, npc, kind).Owner, Is.EqualTo(template.Id), template.Id + " " + kind);
                    for (int attempt = 1; attempt <= 3; attempt++)
                        Assert.That(TalkRomance.SceneRow(s, npc, TalkKind.TurnAside, attempt).Owner, Is.EqualTo(template.Id), template.Id + " turn " + attempt);
                }
            }
        }

        // ------------------------------------------------------------------ here to play (U6)

        [Test]
        public void HereToPlayTurnsAsideThreeTimesWithItsFlagsCoolAndJuryTermsAndNeverABondOrASpark()
        {
            using (Tables())
            {
                var s = FreeTalkS3Fixture.Fresh();
                string npc = FreeTalkFixture.Npcs(s).First().id;
                FreeTalkS3Fixture.AsTemplate(s, npc, FreeTalkS3Fixture.TemplateWith(RomanceStance.HereToPlay));
                FreeTalkS3Fixture.Classes(s, npc, TalkPools.Spark, TalkPools.Spark);
                FreeTalkFixture.ContactOf(s, npc).rapport = 20;
                FreeTalkS2Fixture.Views(s, npc, 40, 40);
                int bonds = s.story.bonds.Count;
                var engine = new EpisodeEngine(s);

                // Attempt 1: the full scene, three choices; push again.
                var offer = FreeTalk.Offer(engine.Snapshot, npc);
                var first = FreeTalkS3Fixture.Say(engine, npc, TalkRomance.CommandText(offer.Talk.Id, TalkRules.Flirt));
                Assert.That(first.Scene, Is.EqualTo(RomanceScene.TurnAside));
                Assert.That(first.Attempt, Is.EqualTo(1));
                Assert.That(first.SceneLine, Is.Not.Empty, "Their reaction.");
                Assert.That(first.SceneSecond, Is.Not.Empty, "Their \"I'm here to play\".");
                Assert.That(first.Choices, Is.EqualTo(new[] { TalkChoice.LaughItOff, TalkChoice.PushAgain, TalkChoice.Apologize }));
                var before = engine.Snapshot;
                var push = FreeTalkS3Fixture.Say(engine, npc, TalkRomance.CommandText(first.SceneTalkId, TalkChoices.Answer(TalkChoice.PushAgain)));
                var after = engine.Snapshot;
                var c = FreeTalk.Contact(after, npc);
                Assert.That(push.ActWarmth, Is.EqualTo(-3));
                Assert.That(after.Score(after.playerId, npc) - before.Score(before.playerId, npc), Is.EqualTo(-3).Within(1e-9), "Push again: -3.");
                Assert.That(c.cool, Is.EqualTo(2));
                Assert.That(c.talkFlags, Does.Contain(TalkRules.Pushy));

                // The jury term inside the band: pushy -3 at talk rules 3 only.
                double pushy = StoryConsumers.JuryStory(after, npc, after.playerId);
                var level2 = after.Clone(); level2.story.talkRules = TalkRules.Chemistry;
                var noFlag = after.Clone(); FreeTalk.Contact(noFlag, npc).talkFlags.Remove(TalkRules.Pushy);
                Assert.That(pushy - StoryConsumers.JuryStory(noFlag, npc, after.playerId), Is.EqualTo(TalkRomance.JuryPushy));
                Assert.That(StoryConsumers.JuryStory(level2, npc, after.playerId), Is.EqualTo(StoryConsumers.JuryStory(noFlag, npc, after.playerId)), "Not below talk rules 3.");
                Assert.That(pushy, Is.InRange(-15.0, 12.0), "Inside the band.");

                // Attempt 2: the short line, -1 for pushy; the cool lowers this talk and counts down.
                engine = FreeTalkS3Fixture.NextWindow(engine, npc);
                before = engine.Snapshot;
                offer = FreeTalk.Offer(before, npc);
                var second = FreeTalkS3Fixture.Say(engine, npc, TalkRomance.CommandText(offer.Talk.Id, TalkRules.Flirt));
                Assert.That(second.Attempt, Is.EqualTo(2));
                Assert.That(second.Choices, Is.Empty, "No choices after the first attempt.");
                Assert.That(second.ActWarmth, Is.EqualTo(-1), "-1 if pushy.");
                var noCool = before.Clone();
                FreeTalk.Contact(noCool, npc).cool = 0;
                Assert.That(second.Landing, Is.EqualTo(Math.Max(-1, FreeTalk.LandingNow(noCool, npc, TalkRules.Flirt) - 1)), "Cool lowers the landing a step.");
                Assert.That(FreeTalk.Contact(engine.Snapshot, npc).cool, Is.EqualTo(1));

                // Attempt 3: the final line and flirt-removed.
                engine = FreeTalkS3Fixture.NextWindow(engine, npc);
                offer = FreeTalk.Offer(engine.Snapshot, npc);
                var third = FreeTalkS3Fixture.Say(engine, npc, TalkRomance.CommandText(offer.Talk.Id, TalkRules.Flirt));
                Assert.That(third.Attempt, Is.EqualTo(3));
                var end = engine.Snapshot;
                Assert.That(FreeTalk.Contact(end, npc).talkFlags, Does.Contain(TalkRules.FlirtRemoved));
                Assert.That(FreeTalk.Contact(end, npc).turnAsides, Is.EqualTo(3));
                Assert.That(TalkRomance.FlirtOpen(end, npc), Is.False, "Flirt leaves their talks.");
                Assert.That(FreeTalk.Contact(end, npc).spark, Is.EqualTo(0), "Never a spark.");
                Assert.That(end.story.bonds.Count, Is.EqualTo(bonds), "Never a bond.");
                Assert.That(TalkRomance.DueTalk(end, npc), Is.Null, "They never open or declare.");
                engine = FreeTalkS3Fixture.NextWindow(engine, npc);
                offer = FreeTalk.Offer(engine.Snapshot, npc);
                FreeTalkS3Fixture.Refused(engine, npc, TalkRomance.CommandText(offer.Talk.Id, TalkRules.Flirt));
            }
        }

        [TestCase(TalkChoice.LaughItOff, 2, 1, "running-joke")]
        [TestCase(TalkChoice.Apologize, 0, 1, "took-the-no-well")]
        public void TheOtherTwoTurnAsideAnswersAreSpec310s(TalkChoice choice, int warmth, int rapport, string flag)
        {
            using (Tables())
            {
                var s = FreeTalkS3Fixture.Fresh();
                string npc = FreeTalkFixture.Npcs(s).First().id;
                FreeTalkS3Fixture.AsTemplate(s, npc, FreeTalkS3Fixture.TemplateWith(RomanceStance.HereToPlay));
                FreeTalkS3Fixture.Classes(s, npc, TalkPools.Spark, TalkPools.Spark);
                FreeTalkFixture.ContactOf(s, npc).rapport = 20;
                FreeTalkS2Fixture.Views(s, npc, 40, 40);
                var engine = new EpisodeEngine(s);
                var offer = FreeTalk.Offer(engine.Snapshot, npc);
                var first = FreeTalkS3Fixture.Say(engine, npc, TalkRomance.CommandText(offer.Talk.Id, TalkRules.Flirt));
                var before = engine.Snapshot;
                FreeTalkS3Fixture.Say(engine, npc, TalkRomance.CommandText(first.SceneTalkId, TalkChoices.Answer(choice)));
                var after = engine.Snapshot;
                Assert.That(after.Score(after.playerId, npc) - before.Score(before.playerId, npc), Is.EqualTo(warmth).Within(0.11), "warmth");
                Assert.That(FreeTalk.Contact(after, npc).rapport - FreeTalk.Contact(before, npc).rapport, Is.EqualTo(rapport), "rapport");
                Assert.That(FreeTalk.Contact(after, npc).talkFlags, Does.Contain(flag));
                Assert.That(after.story.bonds.Count, Is.EqualTo(before.story.bonds.Count), "Never a bond.");
                if (choice == TalkChoice.Apologize)
                {
                    Assert.That(TalkRomanceNotes.ProfileNotes(after, npc), Does.Contain(TalkRomanceNotes.TookTheNoWell));
                    var noFlag = after.Clone(); FreeTalk.Contact(noFlag, npc).talkFlags.Remove(TalkRules.TookTheNoWell);
                    Assert.That(StoryConsumers.JuryStory(after, npc, after.playerId) - StoryConsumers.JuryStory(noFlag, npc, after.playerId),
                        Is.EqualTo(TalkRomance.JuryTookTheNoWell), "Took the no well: +2 at the jury.");
                }
            }
        }

        // ------------------------------------------------------------------ undecided (U7)

        [Test]
        public void AnUndecidedTemplateHoldsBackFirstThenIsSlowerThanARegularOfTheSameClassAtEveryStep()
        {
            using (Tables())
            {
                var s = FreeTalkS3Fixture.Fresh();
                var npcs = FreeTalkFixture.Npcs(s);
                string star = npcs[0].id, regular = npcs.Skip(1).First(x => !FreeTalk.IsAllStar(x)).id;
                FreeTalkS3Fixture.AsTemplate(s, star, FreeTalkS3Fixture.TemplateWith(RomanceStance.Undecided));
                foreach (var pc in FreeTalkS3Fixture.ClassPools)
                    foreach (var nc in FreeTalkS3Fixture.ClassPools)
                    {
                        FreeTalkS3Fixture.Classes(s, star, pc, nc);
                        FreeTalkS2Fixture.ClassAs(s, regular, nc);
                        string pair = pc + "+" + nc;
                        Assert.That(TalkRomance.Pace(s, star), Is.EqualTo(TalkRomance.Pace(s, regular) * TalkRomance.UndecidedPace).Within(1e-9), pair);
                        foreach (int landing in new[] { -1, 0, 1 })
                        {
                            int a = TalkRomance.SparkStep(s, star, landing), b = TalkRomance.SparkStep(s, regular, landing);
                            Assert.That(a, Is.LessThanOrEqualTo(b), pair + " landing " + landing);
                            if (b >= 3) Assert.That(a, Is.LessThan(b), pair + " landing " + landing + ": slower.");
                        }
                        Assert.That(TalkRomance.RapportGate(s, star, 5), Is.EqualTo(TalkRomance.RapportGate(s, regular, 5) + 3), pair + ": rapport gates +3.");
                        Assert.That(TalkRomance.WeekGate(s, star, 2), Is.EqualTo(TalkRomance.WeekGate(s, regular, 2) + 1), pair + ": week gates +1.");
                        int gate = TalkRomance.FlirtRapport(pc, nc);
                        FreeTalkFixture.ContactOf(s, star).rapport = gate + 2;
                        Assert.That(TalkRomance.FlirtOpen(s, star), Is.False, pair + ": the Flirt gate is 3 higher.");
                        FreeTalkFixture.ContactOf(s, star).rapport = gate + 3;
                        Assert.That(TalkRomance.FlirtOpen(s, star), Is.True, pair + ": open at the gate plus 3.");
                    }

                // The first Flirt holds back, with no spark.
                FreeTalkS3Fixture.Classes(s, star, TalkPools.Spark, TalkPools.Spark);
                FreeTalkFixture.ContactOf(s, star).rapport = 20;
                FreeTalkS2Fixture.Views(s, star, 40, 40);
                var engine = new EpisodeEngine(s);
                var offer = FreeTalk.Offer(engine.Snapshot, star);
                var held = FreeTalkS3Fixture.Say(engine, star, TalkRomance.CommandText(offer.Talk.Id, TalkRules.Flirt));
                Assert.That(held.Scene, Is.EqualTo(RomanceScene.HoldBack));
                Assert.That(held.SceneLine, Is.Not.Empty);
                Assert.That(FreeTalk.Contact(engine.Snapshot, star).spark, Is.EqualTo(0), "No spark that window.");
                Assert.That(TalkRomance.SceneFor(engine.Snapshot, star), Is.Not.EqualTo(RomanceScene.HoldBack), "Only the first.");
                Assert.That(TalkRomance.DueTalk(engine.Snapshot, star), Is.Null, "They never open.");
            }
        }

        [Test]
        public void AnUndecidedTemplateDeclaresOnlyAfterSixFlirtsAndCanStillSayNo()
        {
            using (Tables())
            {
                // Their declaration: interested, ready, only from the player's sixth Flirt.
                var found = false;
                for (uint seed = 3111u; seed < 3131u && !found; seed++)
                {
                    var s = FreeTalkS3Fixture.Fresh(seed);
                    s.week = 8;
                    foreach (var npc in FreeTalkFixture.Npcs(s))
                    {
                        FreeTalkS3Fixture.AsTemplate(s, npc.id, FreeTalkS3Fixture.TemplateWith(RomanceStance.Undecided));
                        FreeTalkS3Fixture.Classes(s, npc.id, TalkPools.Spark, TalkPools.Spark);
                        var c = FreeTalkS3Fixture.Flirting(s, npc.id, spark: 80, rapport: 40, flirts: 5, views: 70);
                        c.agreement = 10;
                        if (!TalkRomance.Interested(s, npc.id)) continue;
                        Assert.That(TalkRomance.Ready(s, npc.id), Is.True, "The control: ready.");
                        Assert.That(TalkRomance.DueTalk(s, npc.id), Is.Null, "Five Flirts: no declaration yet.");
                        c.flirts = 6;
                        Assert.That(TalkRomance.DueTalk(s, npc.id)?.Kind, Is.EqualTo(TalkKind.Declaration), "Six: they declare.");
                        found = true;
                        break;
                    }
                }
                Assert.That(found, Is.True, "An interested undecided housemate in the first seeds.");

                // A no is still possible: an undecided housemate whose coin is above their interest plays the soft no after the hold-back.
                var t = FreeTalkS3Fixture.Fresh();
                string star = FreeTalkFixture.Npcs(t).OrderByDescending(x => TalkRomance.InterestCoin(t, x.id)).First().id;
                FreeTalkS3Fixture.AsTemplate(t, star, FreeTalkS3Fixture.TemplateWith(RomanceStance.Undecided));
                FreeTalkS3Fixture.Classes(t, star, TalkPools.Spark, TalkPools.Guarded);
                var k = FreeTalkFixture.ContactOf(t, star);
                k.flirts = 1;
                k.agreement = -10;
                Assert.That(TalkRomance.InterestOf(t, star), Is.LessThan(TalkRomance.InterestCoin(t, star)), "The control: not interested.");
                Assert.That(TalkRomance.SceneFor(t, star), Is.EqualTo(RomanceScene.SoftNo), "An undecided housemate can still say no.");
            }
        }

        // ------------------------------------------------------------------ a closed regular

        [Test]
        public void AClosedRegularDrawsTheLineOnceAndFlirtLeavesTheirTalks()
        {
            using (Tables())
            {
                EpisodeState s = null; ContestantState npc = null;
                for (uint seed = 3111u; seed < 3141u && npc == null; seed++)
                {
                    s = FreeTalkS3Fixture.Fresh(seed);
                    npc = FreeTalkFixture.Npcs(s).FirstOrDefault(x => !FreeTalk.IsAllStar(x) && TalkRomance.StanceOf(s, x.id) == RomanceStance.Closed);
                }
                Assert.That(npc, Is.Not.Null, "A regular whose sheet is closed in the first seeds.");
                FreeTalkS3Fixture.Classes(s, npc.id, TalkPools.Spark, TalkPools.Guarded);
                Assert.That(TalkRomance.StanceOf(s, npc.id), Is.EqualTo(RomanceStance.Closed), "The control: closed by the sheet.");
                FreeTalkFixture.ContactOf(s, npc.id).rapport = 20;
                FreeTalkS2Fixture.Views(s, npc.id, 40, 40);
                var engine = new EpisodeEngine(s);
                var offer = FreeTalk.Offer(engine.Snapshot, npc.id);
                var before = engine.Snapshot;
                var said = FreeTalkS3Fixture.Say(engine, npc.id, TalkRomance.CommandText(offer.Talk.Id, TalkRules.Flirt));
                Assert.That(said.Scene, Is.EqualTo(RomanceScene.LineDrawn));
                var c = FreeTalk.Contact(engine.Snapshot, npc.id);
                Assert.That(c.talkFlags, Does.Contain(TalkRules.FlirtRemoved));
                Assert.That(c.rapport, Is.GreaterThanOrEqualTo(FreeTalk.Contact(before, npc.id).rapport), "Rapport is kept.");
                Assert.That(c.spark, Is.EqualTo(0));
                Assert.That(TalkRomance.FlirtOpen(engine.Snapshot, npc.id), Is.False);
            }
        }
    }

}
