#if !UNITY_5_3_OR_NEWER
// Unity-free subset only: a whole sixteen-seat season walked command by command, beside the digests that share its
// walker (AllWeekSeasonDigests.Busy and Walk). Kept out of the editor's assemblies, where the batch runner would play it
// on every EditMode run.
using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The story's cooldown table holds the house talk's keys (plan 32 §L and §N, "cooldown capacity"). Cooldown keys are
    /// free text in one table of 256 rows, and <c>EpisodeEngine.Cool</c> drops a new key silently when 256 unexpired rows
    /// remain (EpisodeEngine.Story.cs, the cooldowns block). The house talk writes its records there - <c>said:</c>,
    /// <c>tell:</c> and <c>h2h:</c> at 12 (S1), the overhearing and talker keys later - so before any of them is built on,
    /// this pins that the biggest house the game can seat (the sixteen-seat verification stress house; no playable roster
    /// seats more than twelve) never comes near the bound.
    ///
    /// <para>The season is played at <see cref="StoryRules.Latest"/>, the highest level this build defines, so each stack's
    /// first slice (S1, S3, S5a) plays its own keys here without an edit; the keys a later stack will add and this build
    /// does not yet write are held as a reserve at plan 32 §L's worst case. The busy player of the commitment digests asks
    /// every voter and reads every houseguest every week, the most <c>said:</c> keys a week can hold.</para>
    /// </summary>
    public sealed class HouseTalkCapacityTests
    {
        /// <summary>The table's bound (EpisodeEngine.Cool; EpisodeStoryValidation refuses more rows).</summary>
        private const int Rows = 256;

        /// <summary>
        /// Plan 32 §L's worst case, at sixteen seats, for the keys later slices write that this build does not: <c>liar:</c>
        /// 30, <c>found:</c> 5, <c>heard:</c> 2, <c>pact-heard:</c> 1, <c>talk:</c> 2, <c>confidant:</c> 15, <c>invent:</c> 1,
        /// <c>invented:</c> 3. Each slice that starts writing one takes it out of here and lets the walk measure it.
        /// </summary>
        private static readonly Dictionary<string, int> LaterKeys = new Dictionary<string, int>
        {
            { "liar:", 30 }, { "found:", 5 }, { "heard:", 2 }, { "pact-heard:", 1 },
            { "talk:", 2 }, { "confidant:", 15 }, { "invent:", 1 }, { "invented:", 3 },
        };

        /// <summary>Plan 32 §L's worst case for <c>tell:</c> keys at sixteen seats: the walk's own lies may tell less often.</summary>
        private const int TellsAtWorst = 45;

        [TestCase(16001u)]
        [TestCase(16002u)]
        public void ASixteenSeatSeasonNeverFillsTheCooldownTable(uint seed)
        {
            var s = AllWeekSeasonDigests.Fresh(16, seed);
            Assert.That(s.contestants.Count, Is.EqualTo(16), "The verification stress house seats sixteen.");
            Assert.That(s.story.rulesStartWeek, Is.Not.Zero, "A fresh season plays the story.");
            // The highest level this build defines (see the summary): a test may write a level above Current.
            s.story.rulesVersion = StoryRules.Latest;
            Assert.That(EpisodeValidation.TryValidate(s, out var invalid), Is.True, invalid);

            var engine = new EpisodeEngine(s);
            int peakLive = 0, peakRows = 0, peakTells = 0, asks = 0, i = 0;
            string peakAt = "";
            for (; i < 9000 && engine.Snapshot.phase != EpisodePhase.Finished; i++)
            {
                var at = engine.Snapshot;
                EpisodeCommand applied = null;
                for (int attempt = 0; attempt < 6 && applied == null; attempt++)
                {
                    var busy = AllWeekSeasonDigests.Busy(at, seed, attempt);
                    if (busy == null) break;
                    if (engine.Apply(busy).accepted) applied = busy;
                }
                if (applied == null)
                {
                    applied = AllWeekSeasonDigests.Walk(at);
                    var result = engine.Apply(applied);
                    Assert.That(result.accepted, Is.True, "week " + at.week + " " + at.phase + " " + applied.kind + ": " + result.reason);
                }
                var now = engine.Snapshot;
                int live = now.story.cooldowns.Count(c => c.untilWeek > now.week);
                int tells = now.story.cooldowns.Count(c => c.untilWeek > now.week && c.key.StartsWith("tell:", StringComparison.Ordinal));
                peakTells = Math.Max(peakTells, tells);
                peakRows = Math.Max(peakRows, now.story.cooldowns.Count);
                if (live > peakLive) { peakLive = live; peakAt = "week " + now.week + " " + now.phase; }
                Assert.That(now.story.cooldowns.Count, Is.LessThanOrEqualTo(Rows), "The table never holds more than its bound.");
                if (applied.kind == EpisodeCommandKind.AskVote)
                {
                    asks++;
                    // From 12 every answer is recorded (plan 32 §G1 step 7): the key for this voter this week is there.
                    if (EpisodeEngine.StoryAt(now, StoryRules.Manners))
                        Assert.That(now.story.cooldowns.Any(c => c.key.StartsWith("said:" + at.week + ":" + applied.targetId + ":", StringComparison.Ordinal)),
                            Is.True, "week " + at.week + ": the answer of " + applied.targetId + " was dropped from the table.");
                }
            }
            Assert.That(engine.Snapshot.phase, Is.EqualTo(EpisodePhase.Finished), "unfinished after " + i);
            Assert.That(asks, Is.GreaterThan(16), "The busy player asked the vote week after week.");

            int reserve = LaterKeys.Values.Sum() + Math.Max(0, TellsAtWorst - peakTells);
            TestContext.WriteLine("seed " + seed + " rules " + engine.Snapshot.story.rulesVersion + ": " + i + " commands, " + asks + " vote asks; peak live rows "
                + peakLive + " (" + peakAt + "), peak rows " + peakRows + ", peak tell: keys " + peakTells + "; reserve " + reserve
                + "; headroom " + (Rows - peakLive - reserve));
            Assert.That(peakLive + reserve, Is.LessThan(Rows),
                "The live keys at their peak, with the later stacks' worst case on top, stay under the table's bound: none is ever dropped.");
        }
    }
}
#endif
