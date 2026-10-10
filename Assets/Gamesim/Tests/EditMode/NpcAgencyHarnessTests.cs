using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// NPC agency over whole seasons (NPC-AGENCY-PLAN.md §7): the pact cap never breached, pacts
    /// among houseguests actually forming, cold pairs standing, and a report of how warm the house
    /// runs, how the agendas spread and whether a Head of Household nominates the threat.
    /// </summary>
    public sealed class NpcAgencyHarnessTests
    {
        /// <summary>A season as the director ships one, with or without agency.</summary>
        internal static EpisodeState Season(uint seed, int size, CastTemplates.Roster roster, bool agency, IList<string> playerTraits = null)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size, Roster = roster }, seed);
            s.strategyRulesStartWeek = 1; s.blocRulesStartWeek = 1;
            if (playerTraits != null) s.Find(s.playerId).traits = playerTraits.ToList();
            EpisodeEngine.EnableStory(s); EpisodeEngine.EnableRead(s); EpisodeEngine.EnableLevers(s); EpisodeEngine.EnableWeek(s);
            if (agency) EpisodeEngine.EnableAgency(s);
            return s;
        }

        internal sealed class Measure
        {
            public int weeks, pactsFormed, invitations, capBreaches, doubleBooked, coldPairsWeek3, storyColdPairsWeek3, warmPairsWeek3, npcNominations, topThreatNominated;
            public double warmestMutual = double.MinValue, coldestMutual = double.MaxValue, houseOnPlayerWeek3, houseOnPlayerEnd;
            public readonly Dictionary<string, int> agendas = new Dictionary<string, int>();
        }

        /// <summary>Plays a season with the story tests' driver and watches the house.</summary>
        internal static Measure Play(EpisodeState initial, int salt)
        {
            var engine = new EpisodeEngine(initial);
            var m = new Measure();
            bool week3Taken = false;
            int lastPacts = 0;
            for (int i = 0; i < 4000 && engine.Snapshot.phase != EpisodePhase.Finished; i++)
            {
                var s = engine.Snapshot;
                var command = StorySeasonTests.StoryNext(s, salt);
                var result = engine.Apply(command);
                Assert.That(result.accepted, Is.True, "salt " + salt + ": " + command.kind + ": " + result.reason);
                var after = result.state;
                if (command.kind != EpisodeCommandKind.Advance) continue;

                var pacts = after.alliances.Where(a => a.active && a.id.StartsWith("alliance-npc-", StringComparison.Ordinal)
                    && !a.members.Contains(after.playerId) && a.members.Count(id => after.Find(id)?.status == ContestantStatus.Active) >= 2).ToList();
                // A breach is a pact formed while the house was at its share; pacts outliving a
                // shrinking house are not one, since the cap holds proposals, not standing pacts.
                if (EpisodeEngine.AgencyOn(after) && pacts.Count > NpcAlliances.PactCap(after) && pacts.Count > lastPacts) m.capBreaches++;
                lastPacts = pacts.Count;
                if (after.Active.Any(c => !c.isPlayer && pacts.Count(p => p.members.Contains(c.id)) > 1)) m.doubleBooked++;

                if (after.phase == EpisodePhase.Social && s.phase != EpisodePhase.Social)
                    foreach (var npc in after.Active.Where(c => !c.isPlayer))
                    {
                        string kind = NpcAgendas.Of(after, npc.id)?.kind ?? "none";
                        m.agendas[kind] = (m.agendas.TryGetValue(kind, out var n) ? n : 0) + 1;
                    }
                if (after.phase == EpisodePhase.VetoSelection && s.phase == EpisodePhase.Nomination && after.hohId != after.playerId && after.nominees.Count == 2)
                {
                    m.npcNominations++;
                    // The most dangerous candidate the Head of Household is not in a pact with: an ally is shielded whatever their threat.
                    string top = ThreatAssessment.RankedTargets(after, after.hohId).FirstOrDefault(id => !after.Allied(after.hohId, id) && id != after.hohId);
                    if (top != null && after.nominees.Contains(top)) m.topThreatNominated++;
                }
                if (!week3Taken && after.week == 3)
                {
                    week3Taken = true;
                    var npcs = after.Active.Where(c => !c.isPlayer).ToList();
                    foreach (var a in npcs) foreach (var b in npcs.Where(x => string.CompareOrdinal(a.id, x.id) < 0))
                    {
                        double low = Math.Min(after.Score(a.id, b.id), after.Score(b.id, a.id)), high = Math.Max(after.Score(a.id, b.id), after.Score(b.id, a.id));
                        if (high <= -10) m.coldPairsWeek3++;
                        if (low <= -10) m.storyColdPairsWeek3++;
                        if (low >= 10) m.warmPairsWeek3++;
                    }
                    m.houseOnPlayerWeek3 = npcs.Average(c => after.Score(c.id, after.playerId));
                }
            }
            var final = engine.Snapshot;
            m.weeks = final.week;
            m.pactsFormed = final.alliances.Count(a => a.id.StartsWith("alliance-npc-", StringComparison.Ordinal));
            m.invitations = final.deals.Count(d => d.type == DealKind.AllianceInvite && d.recipientId == final.playerId && d.id.StartsWith("deal-ask-", StringComparison.Ordinal));
            var everyone = final.contestants.Where(c => !c.isPlayer).ToList();
            foreach (var a in everyone) foreach (var b in everyone.Where(x => string.CompareOrdinal(a.id, x.id) < 0))
            {
                double mutual = final.Score(a.id, b.id) + final.Score(b.id, a.id);
                m.warmestMutual = Math.Max(m.warmestMutual, mutual); m.coldestMutual = Math.Min(m.coldestMutual, mutual);
            }
            m.houseOnPlayerEnd = everyone.Average(c => final.Score(c.id, final.playerId));
            return m;
        }

        [Test]
        public void PactsFormWithinTheCapAndColdPairsStay()
        {
            var runs = new List<Measure>();
            for (uint seed = 1; seed <= 12; seed++) runs.Add(Play(Season(seed * 13 + 5, 8, CastTemplates.Roster.Regular, true), (int)seed));
            Assert.That(runs.Sum(r => r.capBreaches), Is.Zero, "The house never holds more pacts than its share.");
            Assert.That(runs.Sum(r => r.doubleBooked), Is.Zero, "Nobody is in two.");
            Assert.That(runs.Sum(r => r.pactsFormed), Is.GreaterThan(0), "Houseguests pair up on their own account at last.");
            Assert.That(runs.Count(r => r.coldPairsWeek3 > 0), Is.GreaterThanOrEqualTo(runs.Count / 2), "Cold pairs stand in most seasons: the story's blow-ups need them.");
        }

        /// <summary>
        /// The same house with its turns all week (WAVE-D-NPC-PACTS-PLAN D2): the pact rung is tried at a
        /// houseguest's first beat of the week (<see cref="EpisodeEngine.PactWindow"/>), as the weekly pass tried
        /// it once, so the cap and one pact a houseguest still hold the count - never breached, nobody in two - and
        /// pacts among houseguests form inside D2's decision 8 band, 1.0 to 1.5 a season, around the 1.2 agency gave
        /// them. Outside it, decision 8 says where the rung is tried.
        /// </summary>
        [Test]
        public void UnderTheAllWeekRulesPactsStillFormWithinTheCap()
        {
            var runs = new List<Measure>();
            for (uint seed = 1; seed <= 12; seed++)
            {
                var s = Season(seed * 13 + 5, 8, CastTemplates.Roster.Regular, true);
                EpisodeEngine.EnableAllWeek(s);
                runs.Add(Play(s, (int)seed));
            }
            Assert.That(runs.Sum(r => r.capBreaches), Is.Zero, "The house never holds more pacts than its share.");
            Assert.That(runs.Sum(r => r.doubleBooked), Is.Zero, "Nobody is in two.");
            double perSeason = runs.Average(r => r.pactsFormed);
            TestContext.WriteLine("NPC-only pacts a season under the all-week rules: " + perSeason.ToString("0.00"));
            Assert.That(perSeason, Is.InRange(1.0, 1.5), "Decision 8's band: NPC-only pacts a season at 8.");
        }

#if !UNITY_5_3_OR_NEWER
        /// <summary>
        /// How the house runs with agency and without, for tuning: a report, not a check. The
        /// Unity-free run only; the batch runner executes an explicit test and this outlives its timeout.
        /// </summary>
        [Test, Explicit("A report: run it by name.")]
        public void AgencyReport()
        {
            var groups = new List<(string name, int size, CastTemplates.Roster roster, int seasons, IList<string> traits)>
            {
                ("eight, regular, trait-less player", 8, CastTemplates.Roster.Regular, 48, null),
                ("eight, regular, a funny charming player", 8, CastTemplates.Roster.Regular, 24, new[] { "Funny", "Charming" }),
                ("eight, regular, a manipulative deceptive player", 8, CastTemplates.Roster.Regular, 24, new[] { "Manipulative", "Deceptive" }),
                ("eight, All-Stars", 8, CastTemplates.Roster.AllStars, 24, null),
                ("twelve, regular", 12, CastTemplates.Roster.Regular, 24, null),
            };
            foreach (var group in groups)
                foreach (bool agency in new[] { false, true })
                {
                    var runs = new List<Measure>();
                    for (int i = 0; i < group.seasons; i++)
                        runs.Add(Play(Season((uint)(i * 89 + group.size * 7 + 3), group.size, group.roster, agency, group.traits), i * 17 + group.size));
                    TestContext.WriteLine("== " + group.name + (agency ? ", agency" : ", no agency") + " (" + runs.Count + " seasons)");
                    TestContext.WriteLine("  NPC-only pacts a season:   " + runs.Average(r => r.pactsFormed).ToString("0.00") + "  (seasons with one: " + ((double)runs.Count(r => r.pactsFormed > 0) / runs.Count).ToString("P0") + ", cap breaches " + runs.Sum(r => r.capBreaches) + ", double-booked " + runs.Sum(r => r.doubleBooked) + ")");
                    TestContext.WriteLine("  invitations to the player: " + runs.Average(r => r.invitations).ToString("0.00"));
                    TestContext.WriteLine("  week 3 pairs, cold/warm:   " + runs.Average(r => r.coldPairsWeek3).ToString("0.0") + " / " + runs.Average(r => r.warmPairsWeek3).ToString("0.0")
                        + "   (cold as the story reads it, one side at -10: " + runs.Average(r => r.storyColdPairsWeek3).ToString("0.0") + ")");
                    TestContext.WriteLine("  mutual at the end:         warmest " + runs.Average(r => r.warmestMutual).ToString("0") + ", coldest " + runs.Average(r => r.coldestMutual).ToString("0"));
                    TestContext.WriteLine("  the house on the player:   week 3 " + runs.Average(r => r.houseOnPlayerWeek3).ToString("0.0") + ", end " + runs.Average(r => r.houseOnPlayerEnd).ToString("0.0"));
                    TestContext.WriteLine("  NPC HoH named top threat:  " + (runs.Sum(r => r.npcNominations) == 0 ? "n/a" : ((double)runs.Sum(r => r.topThreatNominated) / runs.Sum(r => r.npcNominations)).ToString("P0")));
                    var agendas = runs.SelectMany(r => r.agendas).GroupBy(p => p.Key).ToDictionary(g => g.Key, g => g.Sum(p => p.Value));
                    double total = Math.Max(1, agendas.Values.Sum());
                    TestContext.WriteLine("  agendas at the social week: " + string.Join(", ", agendas.OrderByDescending(p => p.Value).Select(p => p.Key + " " + (p.Value / total).ToString("P0"))));
                }
        }
#endif
    }
}
