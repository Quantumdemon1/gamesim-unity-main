using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Free talk slice 3 (talk rules 3): the Flirt answer's gates, interest, spark, the ready table, who moves first and the
    /// declaration's outcomes (spec 3.9 with the lead's rulings U2, U3 and R8). The tests' own tables; Unity-free.
    /// </summary>
    public sealed class FreeTalkRomanceTests
    {
        private static IDisposable Tables() => TalkBank.Use(FreeTalkS3Fixture.Table());

        // ------------------------------------------------------------------ the Flirt answer's gates (U2)

        [Test]
        public void TheFlirtGateIsThePairsRapportAtItsEdgeForEveryPairOfClasses()
        {
            using (Tables())
                foreach (var pc in FreeTalkS3Fixture.ClassPools)
                    foreach (var nc in FreeTalkS3Fixture.ClassPools)
                    {
                        var s = FreeTalkS3Fixture.Fresh();
                        string npc = FreeTalkFixture.Npcs(s).First(x => !FreeTalk.IsAllStar(x)).id;
                        FreeTalkS3Fixture.Classes(s, npc, pc, nc);
                        int gate = TalkRomance.FlirtRapport(pc, nc);
                        var c = FreeTalkFixture.ContactOf(s, npc);
                        c.rapport = gate - 1;
                        Assert.That(TalkRomance.FlirtOpen(s, npc), Is.False, pc + "+" + nc + " one below " + gate);
                        c.rapport = gate;
                        Assert.That(TalkRomance.FlirtOpen(s, npc), Is.True, pc + "+" + nc + " at " + gate);
                    }
            Assert.That(new[]
            {
                TalkRomance.FlirtRapport(TalkPools.Spark, TalkPools.Spark), TalkRomance.FlirtRapport(TalkPools.Spark, TalkPools.Steady),
                TalkRomance.FlirtRapport(TalkPools.Steady, TalkPools.Steady), TalkRomance.FlirtRapport(TalkPools.Steady, TalkPools.Guarded),
                TalkRomance.FlirtRapport(TalkPools.Guarded, TalkPools.Spark), TalkRomance.FlirtRapport(TalkPools.Guarded, TalkPools.Guarded),
            }, Is.EqualTo(new[] { 1, 3, 4, 7, 7, 9 }), "Spec 3.9's table, either way round.");
        }

        [Test]
        public void KindredLowersTheFlirtGateByTwoAndGratingClosesIt()
        {
            using (Tables())
            {
                var s = FreeTalkS3Fixture.Fresh();
                string npc = FreeTalkFixture.Npcs(s).First(x => !FreeTalk.IsAllStar(x)).id;
                var c = FreeTalkS2Fixture.Kindred(s, npc);
                Assert.That(TalkChemistry.Band(s, npc), Is.EqualTo(ChemistryBand.Kindred), "The control: kindred.");
                int gate = Math.Max(0, TalkRomance.FlirtRapport(TalkLean.PlayerClass(s), FreeTalk.ClassOf(s.Find(npc))) - 2);
                c.rapport = gate;
                Assert.That(TalkRomance.FlirtOpen(s, npc), Is.True, "Kindred: open at the gate less two.");
                if (gate > 0) { c.rapport = gate - 1; Assert.That(TalkRomance.FlirtOpen(s, npc), Is.False, "One below it."); }

                FreeTalkS2Fixture.Grating(s, npc).rapport = 60;
                Assert.That(TalkChemistry.Band(s, npc), Is.EqualTo(ChemistryBand.Grating), "The control: grating.");
                Assert.That(TalkRomance.FlirtOpen(s, npc), Is.False, "Never while grating, whatever the rapport.");
            }
        }

        [Test]
        public void FlirtIsClosedWhileRomanceIsClosedAfterFlirtRemovedForAHereToPlayPlayerAndBelowLevelThree()
        {
            using (Tables())
            {
                var s = FreeTalkS3Fixture.Fresh();
                string npc = FreeTalkFixture.Npcs(s).First(x => !FreeTalk.IsAllStar(x)).id;
                FreeTalkS3Fixture.Classes(s, npc, TalkPools.Spark, TalkPools.Spark);
                var c = FreeTalkFixture.ContactOf(s, npc);
                c.rapport = 20;
                Assert.That(TalkRomance.FlirtOpen(s, npc), Is.True, "The control: open.");

                c.romanceClosedUntil = s.week + 1;
                Assert.That(TalkRomance.FlirtOpen(s, npc), Is.False, "Closed until a later week.");
                c.romanceClosedUntil = s.week;
                Assert.That(TalkRomance.FlirtOpen(s, npc), Is.True, "Open again in the week it was closed until.");

                c.talkFlags.Add(TalkRules.FlirtRemoved);
                Assert.That(TalkRomance.FlirtOpen(s, npc), Is.False, "Never after flirt-removed.");
                c.talkFlags.Clear();

                var player = s.Find(s.playerId);
                player.sourceTemplateId = FreeTalkS3Fixture.TemplateWith(RomanceStance.HereToPlay);
                Assert.That(TalkRomance.PlayerHereToPlay(s), Is.True, "The control: the player's card is a here-to-play template.");
                foreach (var other in FreeTalkFixture.Npcs(s))
                {
                    FreeTalkFixture.ContactOf(s, other.id).rapport = 50;
                    Assert.That(TalkRomance.FlirtOpen(s, other.id), Is.False, other.id + ": never offered to a here-to-play player.");
                }
                player.sourceTemplateId = null;

                s.story.romanceStorylines = false;
                Assert.That(TalkRomance.FlirtOpen(s, npc), Is.False, "Never with the romance switch off.");
                s.story.romanceStorylines = true;
                s.story.talkRules = TalkRules.Chemistry;
                Assert.That(TalkRomance.FlirtOpen(s, npc), Is.False, "Never below talk rules 3.");
                Assert.That(TalkRomance.DueTalk(s, npc), Is.Null);
            }
        }

        // ------------------------------------------------------------------ interest (fair, fixed per season)

        [Test]
        public void InterestReadsNoNameGenderWordOrLooksAndItsCoinIsFixedPerSeason()
        {
            using (Tables())
            {
                var s = FreeTalkS3Fixture.Fresh();
                foreach (var npc in FreeTalkFixture.Npcs(s))
                {
                    double a = TalkRomance.InterestOf(s, npc.id), u = TalkRomance.InterestCoin(s, npc.id);
                    var copy = s.Clone();
                    foreach (var who in new[] { copy.Find(npc.id), copy.Find(copy.playerId) })
                    {
                        who.name = "Renamed " + who.id;
                        who.pronouns = who.pronouns == "she/her" ? "he/him" : "she/her";
                        who.appearance = null;
                    }
                    Assert.That(TalkRomance.InterestOf(copy, npc.id), Is.EqualTo(a), npc.id + ": interest moved with a name, a pronoun or the looks.");
                    Assert.That(TalkRomance.InterestCoin(copy, npc.id), Is.EqualTo(u), npc.id + ": the coin moved with a name, a pronoun or the looks.");
                    copy.week += 3;
                    FreeTalkFixture.ContactOf(copy, npc.id).talkWindow = 7;
                    Assert.That(TalkRomance.InterestCoin(copy, npc.id), Is.EqualTo(u), npc.id + ": the coin is fixed for the season.");
                }
                var coins = FreeTalkFixture.Npcs(s).Select(n => TalkRomance.InterestCoin(s, n.id)).Distinct().Count();
                Assert.That(coins, Is.EqualTo(FreeTalkFixture.Npcs(s).Count), "Each housemate has a coin of their own.");
            }
        }

        [Test]
        public void AgreementCanAddAtMostATenthToInterest()
        {
            var s = FreeTalkS3Fixture.Fresh();
            string npc = FreeTalkFixture.Npcs(s).First(x => !FreeTalk.IsAllStar(x)).id;
            var c = FreeTalkFixture.ContactOf(s, npc);
            c.agreement = 0;
            double none = TalkRomance.InterestOf(s, npc);
            c.agreement = 5;
            Assert.That(TalkRomance.InterestOf(s, npc) - none, Is.EqualTo(0.1).Within(1e-9));
            c.agreement = 10;
            Assert.That(TalkRomance.InterestOf(s, npc) - none, Is.EqualTo(0.1).Within(1e-9), "Capped at a tenth.");
            c.agreement = -10;
            Assert.That(TalkRomance.InterestOf(s, npc) - none, Is.EqualTo(-0.2).Within(1e-9), "A bad record counts in full.");
        }

        // ------------------------------------------------------------------ spark

        [Test]
        public void SparkStepsByPaceAndLandingAreSpec39sWithTheCapAtFifteen()
        {
            var expected = new Dictionary<string, int[]>
            {
                // landing -1, 0, +1
                { "spark+spark", new[] { 6, 13, 15 } }, { "spark+steady", new[] { 4, 7, 11 } }, { "steady+steady", new[] { 3, 5, 8 } },
                { "spark+guarded", new[] { 2, 4, 6 } }, { "steady+guarded", new[] { 2, 3, 5 } }, { "guarded+guarded", new[] { 1, 2, 3 } },
            };
            var s = FreeTalkS3Fixture.Fresh();
            string npc = FreeTalkFixture.Npcs(s).First(x => !FreeTalk.IsAllStar(x)).id;
            foreach (var pair in expected)
            {
                var parts = pair.Key.Split('+');
                FreeTalkS3Fixture.Classes(s, npc, parts[0], parts[1]);
                var steps = new[] { -1, 0, 1 }.Select(l => TalkRomance.SparkStep(s, npc, l)).ToArray();
                Assert.That(steps, Is.EqualTo(pair.Value), pair.Key);
                FreeTalkS3Fixture.Classes(s, npc, parts[1], parts[0]);
                Assert.That(new[] { -1, 0, 1 }.Select(l => TalkRomance.SparkStep(s, npc, l)).ToArray(), Is.EqualTo(pair.Value), pair.Key + " either way round");
            }
        }

        [Test]
        public void OneCountedFlirtAWindowBuildsTheSparkAndItIsClampedAtAHundred()
        {
            using (Tables())
            {
                var s = FreeTalkS3Fixture.WithInterested(TalkPools.Spark, TalkPools.Spark, out string npc);
                FreeTalkFixture.ContactOf(s, npc).rapport = 10;
                var engine = new EpisodeEngine(s);
                var offer = FreeTalk.Offer(engine.Snapshot, npc);
                Assert.That(TalkRomance.FlirtOffered(engine.Snapshot, npc, offer.Talk), Is.True, "The control: Flirt is on offer.");
                var said = FreeTalkS3Fixture.Say(engine, npc, TalkRomance.CommandText(offer.Talk.Id, TalkRules.Flirt));
                Assert.That(said.Scene, Is.EqualTo(RomanceScene.InKind));
                int step = TalkRomance.SparkStep(s, npc, said.Landing);
                var c = FreeTalk.Contact(engine.Snapshot, npc);
                Assert.That(c.spark, Is.EqualTo(step), "One Flirt's step.");
                Assert.That(c.flirts, Is.EqualTo(1));
                Assert.That(c.romance, Is.EqualTo(TalkRules.Flirting));

                // The cap: a second Flirt this window is refused; a continuation adds no spark.
                FreeTalkS3Fixture.Refused(engine, npc, TalkRomance.CommandText(offer.Talk.Id, TalkRules.Flirt));
                Assert.That(said.Choices, Has.No.Member(TalkChoice.MakeItOfficial), "Not ready on move-in night.");

                // The clamp.
                engine = FreeTalkS3Fixture.NextWindow(engine, npc);
                var at = engine.Snapshot;
                FreeTalk.Contact(at, npc).spark = 98;
                engine = new EpisodeEngine(at);
                offer = FreeTalk.Offer(engine.Snapshot, npc);
                FreeTalkS3Fixture.Say(engine, npc, TalkRomance.CommandText(offer.Talk.Id, TalkRules.Flirt));
                Assert.That(FreeTalk.Contact(engine.Snapshot, npc).spark, Is.EqualTo(100), "Spark stops at 100.");
            }
        }

        [Test]
        public void JustFriendsClosesRomanceAtNoCostAndThePlayersOwnFlirtLiftsIt()
        {
            using (Tables())
            {
                var s = FreeTalkS3Fixture.WithInterested(TalkPools.Spark, TalkPools.Spark, out string npc);
                FreeTalkFixture.ContactOf(s, npc).rapport = 10;
                var engine = new EpisodeEngine(s);
                var offer = FreeTalk.Offer(engine.Snapshot, npc);
                var said = FreeTalkS3Fixture.Say(engine, npc, TalkRomance.CommandText(offer.Talk.Id, TalkRules.Flirt));
                Assert.That(said.Choices, Has.Member(TalkChoice.JustFriends), "Just friends is in every romance talk.");
                var before = engine.Snapshot;
                var friends = FreeTalkS3Fixture.Say(engine, npc, TalkRomance.CommandText(said.SceneTalkId, TalkChoices.Answer(TalkChoice.JustFriends)));
                var after = engine.Snapshot;
                Assert.That(friends.Continuation, Is.True);
                Assert.That(FreeTalk.Contact(after, npc).romance, Is.EqualTo(TalkRules.Friends));
                Assert.That(after.Score(npc, after.playerId), Is.EqualTo(before.Score(npc, before.playerId)), "No cost to their view.");
                Assert.That(after.Score(after.playerId, npc), Is.EqualTo(before.Score(before.playerId, npc)), "No cost to the player's.");
                Assert.That(FreeTalk.Contact(after, npc).talkPending, Is.EqualTo(""));
                FreeTalkS3Fixture.Refused(engine, npc, TalkRomance.CommandText(said.SceneTalkId, TalkChoices.Answer(TalkChoice.JustFriends)));
                Assert.That(TalkRomance.DueTalk(after, npc), Is.Null, "Friends: they open nothing and declare nothing.");

                engine = FreeTalkS3Fixture.NextWindow(engine, npc);
                offer = FreeTalk.Offer(engine.Snapshot, npc);
                var lifted = FreeTalkS3Fixture.Say(engine, npc, TalkRomance.CommandText(offer.Talk.Id, TalkRules.Flirt));
                Assert.That(lifted.Change, Is.EqualTo(RomanceChange.FriendsLifted), "The player's own Flirt lifts it.");
                Assert.That(FreeTalk.Contact(engine.Snapshot, npc).romance, Is.EqualTo(TalkRules.Flirting));
            }
        }

        // ------------------------------------------------------------------ ready, who moves first, the declaration

        [Test]
        public void TheReadyTableIsSpec39s()
        {
            Assert.That(TalkRomance.ReadyRow(TalkPools.Spark, TalkPools.Spark), Is.EqualTo((15, 3, 1)));
            Assert.That(TalkRomance.ReadyRow(TalkPools.Steady, TalkPools.Spark), Is.EqualTo((25, 5, 2)));
            Assert.That(TalkRomance.ReadyRow(TalkPools.Steady, TalkPools.Steady), Is.EqualTo((30, 6, 3)));
            Assert.That(TalkRomance.ReadyRow(TalkPools.Guarded, TalkPools.Spark), Is.EqualTo((40, 9, 4)));
            Assert.That(TalkRomance.ReadyRow(TalkPools.Steady, TalkPools.Guarded), Is.EqualTo((40, 9, 4)));
            Assert.That(TalkRomance.ReadyRow(TalkPools.Guarded, TalkPools.Guarded), Is.EqualTo((50, 12, 5)));
        }

        [Test]
        public void ReadyNeedsTheSparkTheWarmthTheRapportAndTheWeek()
        {
            using (Tables())
            {
                var s = FreeTalkS3Fixture.WithInterested(TalkPools.Spark, TalkPools.Steady, out string npc);
                s.week = 3;
                var c = FreeTalkS3Fixture.Flirting(s, npc, spark: 40, rapport: 5, views: 25);
                Assert.That(TalkRomance.Ready(s, npc), Is.True, "Spark+Steady: spark 40, warmth 25, rapport 5, week 2 or later.");
                c.spark = 39; Assert.That(TalkRomance.Ready(s, npc), Is.False, "spark 39"); c.spark = 40;
                c.rapport = 4; Assert.That(TalkRomance.Ready(s, npc), Is.False, "rapport 4"); c.rapport = 5;
                // The ready warmth counts the spark (a third of it): at spark 40 the row's 25 is met from a mutual warmth of 12.
                FreeTalkS2Fixture.Views(s, npc, 11.9, 50); Assert.That(TalkRomance.Ready(s, npc), Is.False, "their warmth 11.9 at spark 40");
                FreeTalkS2Fixture.Views(s, npc, 50, 11.9); Assert.That(TalkRomance.Ready(s, npc), Is.False, "the player's warmth 11.9 at spark 40");
                c.spark = 43; Assert.That(TalkRomance.Ready(s, npc), Is.True, "the player's warmth 11.9 at spark 43: the spark counts"); c.spark = 40;
                FreeTalkS2Fixture.Views(s, npc, 25, 25);
                s.week = 1; Assert.That(TalkRomance.Ready(s, npc), Is.False, "week 1"); s.week = 3;
                c.romance = TalkRules.Friends; Assert.That(TalkRomance.Ready(s, npc), Is.False, "just friends");
            }
        }

        [Test]
        public void TheyOpenAndDeclareOnlyAfterThePlayersFirstFlirtAndByTheirClass()
        {
            using (Tables())
            {
                foreach (var pool in FreeTalkS3Fixture.ClassPools)
                {
                    var s = FreeTalkS3Fixture.WithInterested(TalkPools.Spark, pool, out string npc);
                    s.week = 6;
                    var c = FreeTalkS3Fixture.Flirting(s, npc, spark: 80, rapport: 30, flirts: 0, views: 70);
                    Assert.That(TalkRomance.Ready(s, npc), Is.True, pool + ": the control: ready.");
                    Assert.That(TalkRomance.DueTalk(s, npc), Is.Null, pool + ": nothing before the player's first Flirt (R8).");
                    c.flirts = 1;
                    var due = TalkRomance.DueTalk(s, npc);
                    if (pool == TalkPools.Spark) Assert.That(due?.Kind, Is.EqualTo(TalkKind.Declaration), "A Spark declares when ready.");
                    else Assert.That(due, Is.Null, pool + " does not declare after one Flirt.");
                    c.flirts = TalkRomance.SteadyDeclaresAfter;
                    due = TalkRomance.DueTalk(s, npc);
                    if (pool == TalkPools.Guarded) Assert.That(due, Is.Null, "A Guarded housemate never declares.");
                    else Assert.That(due?.Kind, Is.EqualTo(TalkKind.Declaration), pool + " declares after four Flirts.");

                    // A Spark opens before ready (R8: after the first Flirt).
                    c.flirts = 1; c.spark = 10;
                    due = TalkRomance.DueTalk(s, npc);
                    if (pool == TalkPools.Spark) Assert.That(due?.Kind, Is.EqualTo(TalkKind.Opening), "A Spark flirts first.");
                    else Assert.That(due, Is.Null, pool + " never opens.");
                    c.flirts = 0;
                    Assert.That(TalkRomance.DueTalk(s, npc), Is.Null, pool + ": no opening before the player's first Flirt.");
                }
            }
        }

        [Test]
        public void NotYetIsAskedAgainAWeekLaterAtMostTwice()
        {
            using (Tables())
            {
                var s = FreeTalkS3Fixture.WithInterested(TalkPools.Spark, TalkPools.Spark, out string npc);
                s.week = 4;
                var c = FreeTalkS3Fixture.Flirting(s, npc, spark: 80, rapport: 30, views: 70);
                Assert.That(TalkRomance.DueTalk(s, npc)?.Kind, Is.EqualTo(TalkKind.Declaration), "The control: they declare.");
                for (int asked = 1; asked <= 3; asked++)
                {
                    c.notYets = asked; c.romanceWeek = s.week;
                    Assert.That(TalkRomance.DueTalk(s, npc)?.Kind, Is.Not.EqualTo(TalkKind.Declaration), "Not yet " + asked + ": not again this week.");
                    s.week++;
                    if (asked < 3) Assert.That(TalkRomance.DueTalk(s, npc)?.Kind, Is.EqualTo(TalkKind.Declaration), "Asked again after not yet " + asked);
                    else Assert.That(TalkRomance.DueTalk(s, npc)?.Kind, Is.Not.EqualTo(TalkKind.Declaration), "After the third not yet it lapses.");
                }
            }
        }

        [Test]
        public void TheirDeclarationsOutcomesAreSpec39sNumbers()
        {
            using (TalkBank.Use(FreeTalkS3Fixture.Romance()))
            {
                foreach (var pool in FreeTalkS3Fixture.ClassPools.Where(p => p != TalkPools.Guarded))
                    foreach (var choice in new[] { TalkChoice.Accept, TalkChoice.NotYet, TalkChoice.NotLikeThat, TalkChoice.JustFriends })
                    {
                        var s = FreeTalkS3Fixture.WithInterested(TalkPools.Spark, pool, out string npc, week: 2);
                        FreeTalkS3Fixture.Flirting(s, npc, spark: 80, rapport: 30, flirts: 6, views: 70);
                        var engine = new EpisodeEngine(s);
                        var offer = FreeTalk.Offer(engine.Snapshot, npc);
                        Assert.That(offer.Talk?.Kind, Is.EqualTo(TalkKind.Declaration), pool + ": the control: their declaration is on offer.");
                        var before = engine.Snapshot;
                        FreeTalkS3Fixture.Say(engine, npc, TalkRomance.CommandText(offer.Talk.Id, TalkChoices.Answer(choice)));
                        var after = engine.Snapshot;
                        var c = FreeTalk.Contact(after, npc);
                        double moved = after.Score(after.playerId, npc) - before.Score(before.playerId, npc);
                        string where = pool + " " + choice;
                        switch (choice)
                        {
                            case TalkChoice.Accept:
                                Assert.That(c.romance, Is.EqualTo(TalkRules.Couple), where);
                                Assert.That(Bonds.Holds(after, after.playerId, npc, BondKinds.Showmance), Is.True, where + ": the existing bond.");
                                var fact = Knowledge.Of(after, FactKinds.Couple, StoryConsumers.CoupleRef(after, after.playerId, npc));
                                Assert.That(fact, Is.Not.Null, where + ": the existing Couple fact.");
                                Assert.That(fact.visibility, Is.EqualTo(FactVisibility.Whispered), where);
                                Assert.That(moved, Is.EqualTo(0), where + ": the couple is the bond; no view moves.");
                                break;
                            case TalkChoice.NotYet:
                                Assert.That(c.notYets, Is.EqualTo(1), where);
                                Assert.That(c.romance, Is.EqualTo(TalkRules.Flirting), where);
                                Assert.That(moved, Is.EqualTo(0), where + ": nothing lost.");
                                break;
                            case TalkChoice.NotLikeThat:
                                Assert.That(c.romance, Is.EqualTo(TalkRules.Closed), where);
                                Assert.That(c.romanceClosedUntil, Is.EqualTo(after.week + TalkRomance.NotLikeThatWeeks), where);
                                if (pool == TalkPools.Spark) Assert.That(moved, Is.EqualTo(0), where + ": 0 with a Spark.");
                                else Assert.That(moved, Is.EqualTo(-2).Within(0.5), where + ": -2 with a Steady.");
                                break;
                            case TalkChoice.JustFriends:
                                Assert.That(c.romance, Is.EqualTo(TalkRules.Friends), where);
                                Assert.That(moved, Is.EqualTo(0), where + ": no cost.");
                                break;
                        }
                    }
            }
        }

        /// <summary>The scene a Flirt in kind leaves open this window, with its Make it official (set directly).</summary>
        private static TalkRow OpenScene(EpisodeState s, string npc)
        {
            var c = FreeTalk.Contact(s, npc);
            var flirtRow = TalkRomance.FlirtLine(s, npc);
            c.talkWindow = FreeTalk.WindowKey(s);
            c.talkPending = flirtRow.Id;
            Assert.That(TalkRomance.ChoicesNow(s, npc, flirtRow), Has.Member(TalkChoice.MakeItOfficial), "The control: ready.");
            return flirtRow;
        }

        [Test]
        public void MakingItOfficialWithSomeoneInterestedIsACouple()
        {
            using (Tables())
            {
                var s = FreeTalkS3Fixture.WithInterested(TalkPools.Spark, TalkPools.Spark, out string npc, week: 2);
                FreeTalkS3Fixture.Flirting(s, npc, spark: 80, rapport: 30, flirts: 1, views: 70);
                var row = OpenScene(s, npc);
                var engine = new EpisodeEngine(s);
                var said = FreeTalkS3Fixture.Say(engine, npc, TalkRomance.CommandText(row.Id, TalkChoices.Answer(TalkChoice.MakeItOfficial)));
                Assert.That(said.Scene, Is.EqualTo(RomanceScene.Accepts));
                Assert.That(said.Continuation, Is.True, "A continuation: not counted.");
                Assert.That(FreeTalk.Contact(engine.Snapshot, npc).romance, Is.EqualTo(TalkRules.Couple));
                Assert.That(Bonds.Holds(engine.Snapshot, engine.Snapshot.playerId, npc, BondKinds.Showmance), Is.True, "The existing bond.");
            }
        }

        [Test]
        public void MakingItOfficialWithSomeoneNoLongerInterestedIsTheirNotLikeThat()
        {
            using (Tables())
            {
                var s = FreeTalkS2Fixture.AtWeek(4, TalkRules.Romance).Snapshot;
                FreeTalkS2Fixture.ClassAs(s, s.playerId, TalkPools.Spark);
                // The regular with the highest coin, made Guarded and given the worst record: A is at most 0.24 below a coin above it.
                string npc = FreeTalkFixture.Npcs(s).Where(x => !FreeTalk.IsAllStar(x) && !TalkRomance.InAnotherShowmance(s, x.id))
                    .Where(x => TalkRomance.StanceOf(s, x.id) == RomanceStance.Open || TalkRomance.StanceOf(s, x.id) == RomanceStance.Strategic)
                    .OrderByDescending(x => TalkRomance.InterestCoin(s, x.id)).First().id;
                FreeTalkS2Fixture.ClassAs(s, npc, TalkPools.Guarded);
                var c = FreeTalkS3Fixture.Flirting(s, npc, spark: 80, rapport: 30, flirts: 1, views: 70);
                c.agreement = -10;
                Assert.That(TalkRomance.Interested(s, npc), Is.False, "The control: no longer interested.");
                var row = OpenScene(s, npc);
                var engine = new EpisodeEngine(s);
                var said = FreeTalkS3Fixture.Say(engine, npc, TalkRomance.CommandText(row.Id, TalkChoices.Answer(TalkChoice.MakeItOfficial)));
                var after = FreeTalk.Contact(engine.Snapshot, npc);
                Assert.That(said.Scene, Is.EqualTo(RomanceScene.Refuses));
                Assert.That(after.romance, Is.EqualTo(TalkRules.Closed));
                Assert.That(after.romanceClosedUntil, Is.EqualTo(s.week + TalkRomance.NotLikeThatWeeks));
                Assert.That(Bonds.ShowmancePartner(engine.Snapshot, engine.Snapshot.playerId), Is.Null, "No bond.");
                Assert.That(TalkRomance.DueTalk(engine.Snapshot, npc), Is.Null, "And they declare nothing while not interested.");
            }
        }
    }
}
