using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Free talk slice 3's pace (talk rules 3), on the shipped tables in seasons the game starts: the player courts one
    /// interested, open regular with a Flirt in every window the answer is on offer (and the talk's first reply in every other
    /// window), nothing paid, taking "Make it official" or their declaration as soon as either is there. Two outgoing flirts
    /// are a couple by the after-veto window of week 2; two steady ones by the end of week 4; a guarded housemate is not ready
    /// before week 4 by that play alone. A season whose player or housemate leaves before the deadline is passed over for the
    /// next seed, never counted as a pass.
    /// </summary>
    public sealed class FreeTalkRomancePaceTests
    {
        private const int Seeds = 40;

        /// <summary>What a courtship came to: the week and window the couple formed (0 when none), and whether a ready moment came before week 4.</summary>
        private sealed class Courtship
        {
            public uint Seed;
            public string NpcId;
            public int CoupleWeek, CoupleWindow, Flirts;
            public bool ReadyBeforeWeekFour, Finished;
            /// <summary>Where the romance stood after the last talk (said in every message, so a miss says why).</summary>
            public string Trace = "no talk";

            public override string ToString() =>
                "seed " + Seed + ", " + NpcId + ", couple in week " + CoupleWeek + " window " + CoupleWindow + " after " + Flirts + " Flirts; last: " + Trace;
        }

        private static string TraceOf(EpisodeState s, string npcId, TalkResult result)
        {
            var c = FreeTalk.Contact(s, npcId);
            return "week " + s.week + " window " + EpisodeEngine.Window(s) + ", " + (result?.Tone ?? result?.Choice.ToString()) + " landing " + result?.Landing
                + " scene " + result?.Scene + ", spark " + c?.spark + ", rapport " + c?.rapport + ", mutual " + StoryPeople.Mutual(s, npcId, s.playerId).ToString("0.0")
                + ", band " + TalkChemistry.Band(s, npcId) + ", interested " + TalkRomance.Interested(s, npcId) + ", ready " + TalkRomance.Ready(s, npcId)
                + ", romance '" + c?.romance + "', player class " + TalkLean.PlayerClass(s);
        }

        [Test]
        public void TwoOutgoingFlirtsAreACoupleInWeekOne()
        {
            var c = Court(TalkPools.Spark, TalkPools.Spark, until: 2, 4101u);
            TestContext.WriteLine("Spark+Spark: " + c);
            Assert.That(c.CoupleWeek, Is.GreaterThan(0), "They became a couple: " + c);
            Assert.That(c.CoupleWeek, Is.EqualTo(1), "In week 1 (at the after-veto window at the earliest, as the ready table allows): " + c);
        }

        [Test]
        public void TwoSteadyHousematesAreACoupleByTheEndOfWeekFour()
        {
            var c = Court(TalkPools.Steady, TalkPools.Steady, until: 4, 4201u);
            TestContext.WriteLine("Steady+Steady: " + c);
            Assert.That(c.CoupleWeek, Is.InRange(1, 4), "A couple by the end of week 4: " + c);
        }

        [Test]
        public void AGuardedHousemateIsNotReadyBeforeWeekFourByThatPlayAlone()
        {
            var c = Court(TalkPools.Spark, TalkPools.Guarded, until: 3, 4301u);
            TestContext.WriteLine("Spark+Guarded: " + c + "; ready before week 4 " + c.ReadyBeforeWeekFour);
            Assert.That(c.Finished, Is.True, "The control: the courtship ran to the end of week 3.");
            Assert.That(c.ReadyBeforeWeekFour, Is.False, "Not ready before week 4: " + c);
            Assert.That(c.CoupleWeek, Is.EqualTo(0), "No couple before week 4: " + c);
        }

        [Test]
        public void AtTalkRulesThreeARepeatedFlirtLandsAsTheFirstAndEveryOtherToneStillLandsLower()
        {
            for (uint seed = 3111u; seed < 3141u; seed++)
            {
                var s = FreeTalkS3Fixture.Fresh(seed);
                foreach (var npc in FreeTalkFixture.Npcs(s).Where(n => !FreeTalk.IsAllStar(n)).ToList())
                {
                    // Only where the step can show: both tones land at 0 or better before any repeat.
                    if (FreeTalk.Landing(s, npc, TalkRules.Flirt) < 0 || FreeTalk.Landing(s, npc, TalkRules.Warm) < 0) continue;
                    var c = FreeTalkFixture.ContactOf(s, npc.id);
                    c.lastTone = TalkRules.Flirt;
                    c.priorTone = TalkRules.Warm;
                    int flirt = FreeTalk.Landing(s, npc, TalkRules.Flirt), warm = FreeTalk.Landing(s, npc, TalkRules.Warm);
                    Assert.That(FreeTalk.LandingNow(s, npc.id, TalkRules.Flirt), Is.EqualTo(flirt), "Talk rules 3: a Flirt after a Flirt lands as the first.");
                    Assert.That(FreeTalk.LandingNow(s, npc.id, TalkRules.Warm), Is.EqualTo(warm - 1), "Talk rules 3: any other tone still lands one step lower on a repeat.");
                    s.story.talkRules = TalkRules.Chemistry;
                    Assert.That(FreeTalk.LandingNow(s, npc.id, TalkRules.Flirt), Is.EqualTo(flirt - 1), "Talk rules 2: a repeated Flirt-kind answer lands as it always did.");
                    return;
                }
            }
            Assert.Fail("No season had a housemate both tones land on at 0 or better.");
        }

        [Test]
        public void AFlirtAnsweredInKindAtAGoodLandingWarmsByTwo()
        {
            using (TalkBank.Use(FreeTalkS3Fixture.Table()))
            {
                for (uint seed = 3111u; seed < 3141u; seed++)
                {
                    var s = FreeTalkS3Fixture.Fresh(seed);
                    FreeTalkS2Fixture.ClassAs(s, s.playerId, TalkPools.Spark);
                    foreach (var npc in FreeTalkFixture.Npcs(s).Where(n => !FreeTalk.IsAllStar(n)).ToList())
                    {
                        // A housemate as the season made them, whom a Flirt lands well on.
                        if (!TalkRomance.Interested(s, npc.id) || FreeTalk.LandingNow(s, npc.id, TalkRules.Flirt) != 1) continue;
                        FreeTalkS2Fixture.Views(s, npc.id, 10, 10);
                        FreeTalkFixture.ContactOf(s, npc.id).rapport = 30;
                        var offer = FreeTalk.Offer(s, npc.id);
                        if (!offer.Available || !TalkRomance.FlirtOffered(s, npc.id, offer.Talk)) continue;
                        var engine = new EpisodeEngine(s);
                        var before = engine.Snapshot;
                        var result = FreeTalkS3Fixture.Say(engine, npc.id, TalkRomance.CommandText(offer.Talk.Id, TalkRules.Flirt));
                        var after = engine.Snapshot;
                        Assert.That(result.Scene, Is.EqualTo(RomanceScene.InKind), "The control: answered in kind.");
                        Assert.That(result.Landing, Is.EqualTo(1), "The control: a good landing.");
                        Assert.That(result.Warmth, Is.EqualTo(TalkRomance.InKindWarmth), "Two, not R5's one.");
                        Assert.That(after.Score(after.playerId, npc.id) - before.Score(before.playerId, npc.id), Is.EqualTo(2).Within(1e-9),
                            "The player's view of them moved by two.");
                        return;
                    }
                }
                Assert.Fail("No season had an interested housemate a Flirt lands well on.");
            }
        }

        [Test]
        public void AFlirtOnAStageTalkPlaysTheRomanceAndLeavesTheStageTalkDue()
        {
            var table = FreeTalkS3Fixture.Table().Concat(FreeTalkS2Fixture.TableStages.Select(st => FreeTalkS2Fixture.StageRow(TalkPools.Spark, st))).ToList();
            using (TalkBank.Use(table))
            {
                var s = FreeTalkS3Fixture.WithInterested(TalkPools.Spark, TalkPools.Spark, out string npc);
                FreeTalkS3Fixture.Flirting(s, npc, spark: 5, rapport: 30);
                var engine = new EpisodeEngine(s);
                var offer = FreeTalk.Offer(engine.Snapshot, npc);
                Assert.That(offer.Available && offer.Talk.Kind == TalkKind.Stage, Is.True, "The control: a stage talk is due.");
                string stageId = offer.Talk.Id;
                Assert.That(TalkRomance.FlirtOffered(engine.Snapshot, npc, offer.Talk), Is.True, "Flirt is on the stage talk.");
                long sequence = engine.Snapshot.nextSequence;
                var result = FreeTalkS3Fixture.Say(engine, npc, TalkRomance.CommandText(stageId, TalkRules.Flirt));
                var after = engine.Snapshot;
                Assert.That(result.Scene, Is.EqualTo(RomanceScene.InKind), "The romance played.");
                Assert.That(result.StageEvent, Is.Null, "No stage line.");
                Assert.That(FreeTalk.Contact(after, npc).spentTalks.Select(TalkRules.SpentId), Has.No.Member(stageId), "The stage talk is not spent.");
                Assert.That(after.events.Where(e => e.sequence >= sequence && e.kind == TalkFriendship.StageEventKind), Is.Empty, "No stage event row.");
                var next = FreeTalkS3Fixture.NextWindow(engine, npc);
                Assert.That(FreeTalk.Offer(next.Snapshot, npc).Talk?.Id, Is.EqualTo(stageId), "The stage talk is still due next window.");
            }
        }

        /// <summary>The first seed from <paramref name="first"/> whose courtship ran to the end of week <paramref name="until"/> (or to a couple) with both still in the house.</summary>
        private static Courtship Court(string playerPool, string pool, int until, uint first)
        {
            Assert.That(TalkBank.Rows, Is.SameAs(TalkTables.All), "The shipped tables.");
            for (uint seed = first; seed < first + Seeds; seed++)
                foreach (int size in new[] { 8, 12 })
                {
                    var c = CourtOnce(playerPool, pool, until, seed, size);
                    if (c != null && c.Finished) return c;
                }
            Assert.Fail("No seed ran a courtship to its deadline.");
            return null;
        }

        private static Courtship CourtOnce(string playerPool, string pool, int until, uint seed, int size)
        {
            var s = FreeTalkIntegrationTests.FreshShipped(seed, CastTemplates.Roster.Regular, size);
            Assert.That(s.story.talkRules, Is.EqualTo(TalkRules.Romance), "The control: a season the game starts plays talk rules 3.");
            FreeTalkS2Fixture.ClassAs(s, s.playerId, playerPool);
            // The housemate as the season made them (their own traits, never set by hand): of the class asked for, open,
            // interested, and for the outgoing pair one a Flirt lands well on (the owner's "flirtatious and outgoing").
            string npcId = FreeTalkFixture.Npcs(s).OrderBy(n => n.id, StringComparer.Ordinal)
                .Where(n => !FreeTalk.IsAllStar(n) && FreeTalk.ClassOf(n) == pool && TalkRomance.StanceOf(s, n.id) == RomanceStance.Open
                    && TalkRomance.Interested(s, n.id) && (pool != TalkPools.Spark || FreeTalk.Landing(s, n, TalkRules.Flirt) == 1))
                .Select(n => n.id).FirstOrDefault();
            // A guarded housemate made so by hand when the season made none: the case asks only that they are NOT ready.
            if (npcId == null && pool == TalkPools.Guarded)
                foreach (var npc in FreeTalkFixture.Npcs(s).OrderBy(n => n.id, StringComparer.Ordinal).Where(n => !FreeTalk.IsAllStar(n)))
                {
                    var traits = npc.traits;
                    FreeTalkS2Fixture.ClassAs(s, npc.id, pool);
                    if (TalkRomance.StanceOf(s, npc.id) == RomanceStance.Open && TalkRomance.Interested(s, npc.id)) { npcId = npc.id; break; }
                    npc.traits = traits;
                }
            if (npcId == null) return null;
            var engine = new EpisodeEngine(s);
            var court = new Courtship { Seed = seed, NpcId = npcId };
            int lastKey = -1;
            for (int step = 0; step < 4000; step++)
            {
                var now = engine.Snapshot;
                bool bothIn = now.Find(now.playerId).status == ContestantStatus.Active && now.Find(npcId).status == ContestantStatus.Active;
                if (now.week > until || now.phase == EpisodePhase.Finished) { court.Finished = bothIn || court.CoupleWeek > 0; return court; }
                if (!bothIn) return court.CoupleWeek > 0 ? Done(court) : null;
                int key = FreeTalk.WindowKey(now);
                if (key != 0 && key != lastKey)
                {
                    lastKey = key;
                    Talk(engine, npcId, court);
                    if (court.CoupleWeek > 0) return Done(court);
                    if (engine.Snapshot.week < 4 && TalkRomance.Ready(engine.Snapshot, npcId)) court.ReadyBeforeWeekFour = true;
                }
                var result = engine.Apply(HouseStep(engine.Snapshot, npcId));
                Assert.That(result.accepted, Is.True, "The house's step: " + result.reason);
            }
            Assert.Fail("Seed " + seed + ": the walk never reached its deadline.");
            return null;
        }

        private static Courtship Done(Courtship c) { c.Finished = true; return c; }

        /// <summary>The engine tests' next lawful command, except that a player who is Head of Household never nominates the one they court.</summary>
        private static EpisodeCommand HouseStep(EpisodeState s, string npcId)
        {
            var command = EpisodeEngineTests.NextCommand(s);
            if (command.kind == EpisodeCommandKind.Nominate && (command.targetId == npcId || command.secondTargetId == npcId))
            {
                var others = EpisodeEngine.NominationCandidates(s).Where(c => c.id != npcId).ToArray();
                if (others.Length >= 2) { command.targetId = others[0].id; command.secondTargetId = others[1].id; }
            }
            return command;
        }

        /// <summary>The window's one counted talk with them: their declaration accepted, else a Flirt where it is on offer, else the talk's first reply; then "Make it official" when the scene offers it.</summary>
        private static void Talk(EpisodeEngine engine, string npcId, Courtship court)
        {
            var s = engine.Snapshot;
            var offer = FreeTalk.Offer(s, npcId);
            if (!offer.Available) return;
            var talk = offer.Talk;
            string answer;
            var choices = TalkRomance.IsRomanceKind(talk.Kind) ? TalkRomance.ChoicesNow(s, npcId, talk) : new List<TalkChoice>();
            if (choices.Contains(TalkChoice.Accept)) answer = TalkChoices.Answer(TalkChoice.Accept);
            else if (choices.Contains(TalkChoice.MakeItOfficial)) answer = TalkChoices.Answer(TalkChoice.MakeItOfficial);
            else if (TalkRomance.FlirtOffered(s, npcId, talk) || talk.Replies.Any(r => r != null && r.Choice == TalkChoice.None && r.Tone == TalkRules.Flirt))
                answer = TalkRules.Flirt;
            // Keeping on good terms: the reply that lands best with them now.
            else answer = talk.Replies.Where(r => r != null && r.Choice == TalkChoice.None && r.Tone != null && r.Tone != TalkRules.Flirt)
                .OrderByDescending(r => FreeTalk.LandingNow(s, npcId, r.Tone)).First().Tone;
            if (answer == TalkRules.Flirt) court.Flirts++;
            var result = FreeTalkS3Fixture.Say(engine, npcId, TalkRomance.CommandText(talk.Id, answer));
            Record(engine.Snapshot, npcId, result, court);
            court.Trace = TraceOf(engine.Snapshot, npcId, result);
            if (court.CoupleWeek == 0 && result.Choices != null && result.Choices.Contains(TalkChoice.MakeItOfficial) && result.SceneTalkId != null)
            {
                var made = FreeTalkS3Fixture.Say(engine, npcId, TalkRomance.CommandText(result.SceneTalkId, TalkChoices.Answer(TalkChoice.MakeItOfficial)));
                Record(engine.Snapshot, npcId, made, court);
                court.Trace = TraceOf(engine.Snapshot, npcId, made);
            }
        }

        private static void Record(EpisodeState s, string npcId, TalkResult result, Courtship court)
        {
            if (result.Change != RomanceChange.Couple || court.CoupleWeek > 0) return;
            Assert.That(Bonds.Holds(s, s.playerId, npcId, BondKinds.Showmance), Is.True, "A couple is the showmance bond.");
            court.CoupleWeek = s.week;
            court.CoupleWindow = EpisodeEngine.Window(s);
        }
    }
}
