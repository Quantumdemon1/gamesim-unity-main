using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The story system's pacing (plan §5.2), measured over whole seasons the way a player lives
    /// them: the ceilings the airtime rules promise, held as checks, and the rate targets the owner
    /// tunes against, printed by an explicit report over the plan's 1,920 seasons.
    /// </summary>
    public sealed class StoryPacingTests
    {
        /// <summary>What one season looked like from the story system's side.</summary>
        internal sealed class Pace
        {
            /// <summary>
            /// Every beat put to the player; the budgeted ones exclude what the airtime lets through on
            /// top - production's must-fires and the urgent houseguest-initiated moments.
            /// </summary>
            public int weeks, weeksWithACard, asks, budgetedAsks, summons, showmances, npcRemovals, pileOns, arcsFinished, storiesFinished;
            public bool removalWindow, pariah;
            public readonly Dictionary<int, int> asksByWeek = new Dictionary<int, int>();
            public readonly Dictionary<int, int> budgetedByWeek = new Dictionary<int, int>();
            public readonly Dictionary<int, int> summonsByWeek = new Dictionary<int, int>();
            /// <summary>Plays offered, by week: their own budget (plan 30 §5), outside the arcs' asks.</summary>
            public int playOffers;
            public readonly Dictionary<int, int> playOffersByWeek = new Dictionary<int, int>();
            /// <summary>Each week's asks, as "arc/beat (lane, surface)", for a failure to say what asked.</summary>
            public readonly Dictionary<int, List<string>> askedByWeek = new Dictionary<int, List<string>>();

            // The watch's own notes: beats already seen, asks already counted, the pariah run.
            private readonly HashSet<string> seen = new HashSet<string>();
            private readonly HashSet<string> asked = new HashSet<string>();
            private string pariahTarget;
            private int pariahRun;

            /// <summary>
            /// Watches one accepted command: every beat it newly put to the player, counted as the airtime
            /// counts it (a story's beats closing at one anchor are one ask), every summons and play offer,
            /// and after an Advance whether some houseguest has three houseguests at forty against them.
            /// The balance lab watches its seasons with this too, so its pace reads as this report's.
            /// </summary>
            internal void Watch(EpisodeCommand command, EpisodeState after)
            {
                foreach (var beat in EpisodeEngine.OpenStoryBeats(after))
                {
                    if (!seen.Add(beat.id)) continue;
                    var arc = StoryCatalog.Find(after.storylines.FirstOrDefault(x => x.id == beat.cycleId)?.templateId);
                    // The first night's card rides on the opening and counts for no week.
                    if (arc?.id == "first-night") continue;
                    if (beat.surface == StorySurfaces.Summons)
                    {
                        summons++;
                        summonsByWeek[beat.week] = (summonsByWeek.TryGetValue(beat.week, out var n) ? n : 0) + 1;
                        continue;
                    }
                    if (arc?.play != null)
                    {
                        // Plays keep their own airtime (plan 30 §5): an offer counts against the plays'
                        // budget, and a step of a play taken on against nothing.
                        if (beat.contentId != null && beat.contentId.EndsWith(":offer", System.StringComparison.Ordinal))
                        {
                            playOffers++;
                            playOffersByWeek[beat.week] = (playOffersByWeek.TryGetValue(beat.week, out var p) ? p : 0) + 1;
                        }
                        continue;
                    }
                    if (!asked.Add(beat.week + "|" + beat.cycleId + "|" + beat.closesAnchor)) continue;
                    if (!askedByWeek.TryGetValue(beat.week, out var list)) askedByWeek[beat.week] = list = new List<string>();
                    list.Add((arc?.id ?? "?") + "/" + beat.contentId + " (" + arc?.lane + ", " + beat.surface + (arc?.urgent == true ? ", urgent" : "") + ")");
                    asks++;
                    asksByWeek[beat.week] = (asksByWeek.TryGetValue(beat.week, out var m) ? m : 0) + 1;
                    if (arc != null && arc.lane != StoryLanes.Production && !arc.urgent)
                    {
                        budgetedAsks++;
                        budgetedByWeek[beat.week] = (budgetedByWeek.TryGetValue(beat.week, out var b) ? b : 0) + 1;
                    }
                }
                if (command.kind == EpisodeCommandKind.Advance)
                {
                    // The reigning Head of Household is exempt, as the pile-on exempts them: every used
                    // veto leaves three nominees resenting the week's HoH by design (plan §5.2).
                    var target = after.Active.Where(c => !c.isPlayer && c.id != after.hohId)
                        .Select(c => (c.id, holders: Grudges.HoldersAgainst(after, c.id, 40).Count(h => after.Find(h)?.status == ContestantStatus.Active)))
                        .Where(x => x.holders >= 3).Select(x => x.id).OrderBy(id => id, StringComparer.Ordinal).FirstOrDefault();
                    if (target != null && target == pariahTarget) { if (++pariahRun >= 2) pariah = true; }
                    else { pariahTarget = target; pariahRun = target != null ? 1 : 0; }
                }
            }

            /// <summary>The season's totals, from its final state.</summary>
            internal void Close(EpisodeState final)
            {
                weeks = final.week;
                weeksWithACard = asksByWeek.Count(x => x.Value > 0);
                showmances = final.story.bonds.Count(b => b.kind == BondKinds.Showmance && b.aId != final.playerId && b.bId != final.playerId);
                npcRemovals = final.story.removals.Count(r => r.contestantId != final.playerId);
                pileOns = final.storylines.Count(x => x.templateId == "the-house-turns");
                arcsFinished = final.storylines.Count(x => x.status == StorylineStatus.Completed);
                // Stories, as against moments: the arcs with more than one beat, the plan's "about three".
                storiesFinished = final.storylines.Count(x => x.status == StorylineStatus.Completed
                    && (StoryCatalog.Find(x.templateId)?.beats.Length ?? 0) > 1);
            }
        }

        /// <summary>Plays a season with the story tests' driver and watches it (<see cref="Pace.Watch"/>).</summary>
        internal static Pace Measure(EpisodeState initial, int salt)
        {
            var engine = new EpisodeEngine(initial);
            var pace = new Pace { removalWindow = initial.contestants.Count >= 7 };
            for (int i = 0; i < 4000 && engine.Snapshot.phase != EpisodePhase.Finished; i++)
            {
                var s = engine.Snapshot;
                var command = StorySeasonTests.StoryNext(s, salt);
                var result = engine.Apply(command);
                Assert.That(result.accepted, Is.True, "salt " + salt + ": " + command.kind + ": " + result.reason);
                pace.Watch(command, result.state);
            }
            var final = engine.Snapshot;
            Assert.That(final.phase, Is.EqualTo(EpisodePhase.Finished), "salt " + salt + " did not finish.");
            pace.Close(final);
            return pace;
        }

        [Test]
        public void NoWeekAsksMoreThanTheAirtimeAllows()
        {
            for (uint seed = 1; seed <= 24; seed++)
            {
                var pace = Measure(StorySeasonTests.StorySeason(seed * 7 + 1, 8), (int)seed);
                foreach (var week in pace.budgetedByWeek)
                    Assert.That(week.Value, Is.LessThanOrEqualTo(week.Key == 1 ? 1 : 2), "Seed " + seed + ", week " + week.Key + ": asks: "
                        + string.Join("; ", pace.askedByWeek[week.Key]));
                foreach (var week in pace.summonsByWeek)
                    Assert.That(week.Value, Is.LessThanOrEqualTo(1), "Seed " + seed + ", week " + week.Key + ": summons.");
                foreach (var week in pace.playOffersByWeek)
                    Assert.That(week.Value, Is.LessThanOrEqualTo(EpisodeEngine.PlayOffersAWeek), "Seed " + seed + ", week " + week.Key + ": play offers.");
                Assert.That(pace.budgetedAsks, Is.LessThanOrEqualTo(9), "Seed " + seed + ": the default eight's ceiling is nine asks.");
            }
        }

        [Test]
        public void APureAllStarsSeasonHasNoShowmances()
        {
            for (uint seed = 1; seed <= 12; seed++)
                Assert.That(Measure(StorySeasonTests.StorySeason(seed * 11 + 3, 8, CastTemplates.Roster.AllStars), (int)seed).showmances,
                    Is.Zero, "Seed " + seed + ": real people do not play romance parts.");
        }

        [Test]
        public void ASixPersonHouseNeverRemovesAHouseguest()
        {
            for (uint seed = 1; seed <= 24; seed++)
                Assert.That(Measure(StorySeasonTests.StorySeason(seed * 5 + 2, 6), (int)seed).npcRemovals, Is.Zero, "Seed " + seed);
        }

#if !UNITY_5_3_OR_NEWER
        // A report for the Unity-free run (Tools/SimulationTests): Unity's batch runner executes an
        // [Explicit] test, and a 1,920-season sweep outlives its three-minute timeout.
        /// <summary>
        /// The plan's rate targets over its 1,920 seasons: eight house sizes' worth of the default
        /// eight, both rosters, and the six and twelve beside them. A report for tuning, not a check.
        /// </summary>
        [Test, Explicit("Diagnostic: prints the pacing measures against the plan's targets.")]
        public void PacingReport()
        {
            var groups = new List<(string name, int size, CastTemplates.Roster roster, int seasons)>
            {
                ("default eight, regular", 8, CastTemplates.Roster.Regular, 960),
                ("default eight, All-Stars", 8, CastTemplates.Roster.AllStars, 320),
                ("six, regular", 6, CastTemplates.Roster.Regular, 320),
                ("twelve, regular", 12, CastTemplates.Roster.Regular, 320),
            };
            foreach (var group in groups)
            {
                var paces = new List<Pace>();
                for (int i = 0; i < group.seasons; i++)
                    paces.Add(Measure(StorySeasonTests.StorySeason((uint)(i * 97 + group.size * 13 + 7), group.size, group.roster), i * 31 + group.size));
                double weeks = paces.Sum(p => p.weeks);
                TestContext.WriteLine("== " + group.name + " (" + paces.Count + " seasons)");
                TestContext.WriteLine("  weeks with a card:       " + (paces.Sum(p => p.weeksWithACard) / weeks).ToString("P0") + "   (target 30-60%)");
                TestContext.WriteLine("  asks per season:         " + paces.Average(p => p.budgetedAsks).ToString("0.0") + ", max " + paces.Max(p => p.budgetedAsks) + "   (target 4-6, ceiling 9 in the eight)");
                TestContext.WriteLine("  + must-fires per season: " + paces.Average(p => p.asks - p.budgetedAsks).ToString("0.0") + "   (production and urgent moments, on top)");
                TestContext.WriteLine("  summons per season:      " + paces.Average(p => p.summons).ToString("0.0") + ", max " + paces.Max(p => p.summons));
                TestContext.WriteLine("  NPC showmances:          " + paces.Average(p => p.showmances).ToString("0.00") + "   (target 0.5-1.5 with fictional NPCs, 0 in All-Stars)");
                var windows = paces.Where(p => p.removalWindow).ToList();
                if (windows.Count > 0)
                    TestContext.WriteLine("  seasons with a removal:  " + ((double)windows.Count(p => p.npcRemovals > 0) / windows.Count).ToString("P1") + "   (target at most 10%)");
                TestContext.WriteLine("  seasons with a pile-on:  " + ((double)paces.Count(p => p.pileOns > 0) / paces.Count).ToString("P0") + "   (target at most 25%)");
                TestContext.WriteLine("  pariah-free seasons:     " + ((double)paces.Count(p => !p.pariah) / paces.Count).ToString("P0") + "   (target at least 80%)");
                TestContext.WriteLine("  stories finished:        " + paces.Average(p => p.storiesFinished).ToString("0.0") + "   (target about 3; moments besides: "
                    + paces.Average(p => p.arcsFinished - p.storiesFinished).ToString("0.0") + ")");
            }
        }
#endif
    }
}
