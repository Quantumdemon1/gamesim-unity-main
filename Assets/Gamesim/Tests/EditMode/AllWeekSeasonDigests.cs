#if !UNITY_5_3_OR_NEWER
// D2's season evidence, reproducible on demand: Tools/SimulationTests only. Unity's batch runner
// executes [Explicit] tests, so the file is compiled out of the editor's assemblies.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// D2, the all-week NPC strategy (WAVE-D-NPC-PACTS-PLAN §4), over whole seasons.
    ///
    /// <para><b>The measure (D2-S0).</b> What the house does with its turns while they are all spent at
    /// once, as the social week opens (<see cref="NpcSocialActions.Settle"/>): the director's own fresh
    /// seasons (<see cref="ShippedRules.ApplyFresh"/>) in houses of 6, 8 and 12, and the sixteen-person
    /// stress house on the combined roster, six seeds each, played by the digests' walker and by their busy
    /// player. Each of the house's acts is read from what a step wrote - the ledger's new rows, the pacts
    /// and words it made, the memories, the lines to the player and the reply cards - so the same reading
    /// measures any cadence the same way.</para>
    ///
    /// <para>Explicit: run with <c>dotnet test Tools/SimulationTests --filter "FullyQualifiedName~AllWeekSeasonDigests"</c>.</para>
    /// </summary>
    public sealed class AllWeekSeasonDigests
    {
        [Test, Explicit("Plays 48 fresh seasons with the house's turns at the social week's opening: a report.")]
        public void SettleByHouseSize()
        {
            var run = Measure(allWeek: false);
            Report("settle", run);
            Assert.That(run.errors, Is.Empty, "Every command was legal:\n" + string.Join("\n", run.errors));
            Assert.That(run.bySize.Values.Sum(c => Get(c, "pact")), Is.GreaterThan(0), "Houseguests formed pacts.");
        }

        // ------------------------------------------------------------ the run

        internal static readonly int[] Sizes = { 6, 8, 12, 16 };
        internal static readonly string[] Players = { "walker", "busy" };
        internal const int Seeds = 6;

        /// <summary>The house's acts, as a step's writes read them.</summary>
        internal static readonly string[] ActKinds = { "pact", "promise", "talk", "hunt", "meet", "rumour", "confront", "eavesdrop" };

        internal sealed class RunCounts
        {
            public readonly Dictionary<int, Dictionary<string, int>> bySize = new Dictionary<int, Dictionary<string, int>>();
            public readonly Dictionary<int, List<double>> npcScoreWeek4 = new Dictionary<int, List<double>>(), playerScoreWeek4 = new Dictionary<int, List<double>>();
            public readonly List<string> errors = new List<string>();
        }

        internal static int Get<TKey>(IDictionary<TKey, int> counts, TKey key) => counts.TryGetValue(key, out int n) ? n : 0;

        internal static void Count<TKey>(Dictionary<TKey, int> counts, TKey key, int by = 1)
        {
            counts.TryGetValue(key, out int n); counts[key] = n + by;
        }

        private static readonly MethodInfo BusyOf = Private("Busy", typeof(EpisodeState), typeof(uint), typeof(int), typeof(bool));
        private static readonly MethodInfo NextOf = Private("NextCommand", typeof(EpisodeState));

        /// <summary>The digests' scripted players, by reflection: the same moves.</summary>
        private static MethodInfo Private(string name, params Type[] parameters) =>
            typeof(CommitmentRulesSeasonDigests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static, null, parameters, null)
            ?? throw new InvalidOperationException("CommitmentRulesSeasonDigests." + name + " is the harness's player.");

        internal static EpisodeCommand Busy(EpisodeState s, uint seed, int attempt) => (EpisodeCommand)BusyOf.Invoke(null, new object[] { s, seed, attempt, true });
        internal static EpisodeCommand Walk(EpisodeState s) => (EpisodeCommand)NextOf.Invoke(null, new object[] { s });

        /// <summary>A fresh season as the director starts one, at a size: a roster's house, or the stress house past twelve.</summary>
        internal static EpisodeState Fresh(int size, uint seed)
        {
            var s = size > SeasonBuilder.LargestRosterHouse
                ? SeasonBuilder.CreateVerificationStressHouse(size, seed)
                : SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size }, seed);
            ShippedRules.ApplyFresh(s);
            return s;
        }

        private static RunCounts Measure(bool allWeek)
        {
            var run = new RunCounts();
            foreach (int size in Sizes)
            foreach (string player in Players)
            for (uint seed = 1; seed <= Seeds; seed++)
            {
                if (!run.bySize.TryGetValue(size, out var counts)) run.bySize[size] = counts = new Dictionary<string, int>();
                var start = Fresh(size, seed);
                start.allWeekRulesStartWeek = allWeek ? 1 : 0;
                string error = Play(start, player == "busy", seed, counts, run, size);
                if (error != null) run.errors.Add(player + " n" + size + " s" + seed + ": " + error);
                Count(counts, "seasons");
            }
            return run;
        }

        private static string Play(EpisodeState start, bool busy, uint seed, Dictionary<string, int> counts, RunCounts run, int size)
        {
            var engine = new EpisodeEngine(start);
            int scored = 0;
            for (int i = 0; i < 6000 && engine.Snapshot.phase != EpisodePhase.Finished; i++)
            {
                var s = engine.Snapshot;
                // Mean |score| as a week's Head of Household competition opens: week 4's, and week 3's for
                // the six-house, whose fourth week is the finale's.
                if (s.phase == EpisodePhase.HoH && (s.week == 3 || s.week == 4) && scored != s.week)
                {
                    scored = s.week;
                    var npcs = s.Active.Where(c => !c.isPlayer).ToList();
                    int key = size * 10 + s.week;
                    if (!run.npcScoreWeek4.ContainsKey(key)) { run.npcScoreWeek4[key] = new List<double>(); run.playerScoreWeek4[key] = new List<double>(); }
                    foreach (var a in npcs) foreach (var b in npcs.Where(x => x.id != a.id)) run.npcScoreWeek4[key].Add(Math.Abs(s.Score(a.id, b.id)));
                    foreach (var a in npcs) run.playerScoreWeek4[key].Add(Math.Abs(s.Score(a.id, s.playerId)));
                }
                CommandResult result = null;
                for (int attempt = 0; busy && attempt < 6 && result == null; attempt++)
                {
                    var own = Busy(s, seed, attempt);
                    if (own == null) break;
                    var tried = engine.Apply(own);
                    if (tried.accepted) result = tried;
                }
                if (result == null)
                {
                    var next = Walk(s);
                    result = engine.Apply(next);
                    if (!result.accepted) return "week " + s.week + " " + s.phase + " " + next.kind + ": " + result.reason;
                }
                Step(s, result.state, counts);
            }
            var final = engine.Snapshot;
            Count(counts, "season-pacts", final.alliances.Count(a => a.id.StartsWith("alliance-npc-", StringComparison.Ordinal)));
            foreach (var promise in CommitmentReferences.Promises(final).Where(p => p.id != null && p.id.StartsWith("promise-npc-", StringComparison.Ordinal)))
                Count(counts, "season-promise-" + promise.kind);
            return final.phase == EpisodePhase.Finished ? null : "unfinished";
        }

        // ------------------------------------------------------------ what a step did

        /// <summary>
        /// One step's acts of the house, read from what it wrote. Each act is one key of the ledger's new rows
        /// (a pair, a type, a note), a new NPC-only pact, a new word an NPC gave, or a new overheard memory; the
        /// player's own acts (their commands' lines and rows) are left out by their words.
        /// </summary>
        internal static void Step(EpisodeState before, EpisodeState after, Dictionary<string, int> counts)
        {
            // Once a week, as the social week opens: the house's people that week.
            if (before.phase == EpisodePhase.Eviction && before.evictionResolved && after.phase == EpisodePhase.Social)
            {
                Count(counts, "weeks");
                Count(counts, "npc-weeks", after.Active.Count(c => !c.isPlayer));
            }
            // The words given as the campaign opens (NpcPromises.Settle) are standing business, not turns.
            bool campaignOpens = before.phase == EpisodePhase.VetoMeeting && after.phase == EpisodePhase.Campaign;
            int turnPromises = campaignOpens ? TurnPromises(before, after) : int.MaxValue;
            foreach (var act in Acts(before, after))
            {
                if (act.kind == "promise" && campaignOpens && turnPromises-- <= 0) { Count(counts, "promise-campaign-open"); continue; }
                Count(counts, act.kind);
                if (act.player) Count(counts, "player-" + act.kind);
            }
            foreach (var card in after.replyCards.Where(c => !before.replyCards.Any(b => b.id == c.id)))
                Count(counts, "card-" + card.kind);
            foreach (var line in after.events.Where(e => e.sequence >= before.nextSequence))
            {
                if (line.kind == WaveDEventKinds.Sighting) Count(counts, "line-sighting");
                else if (line.kind == WaveDEventKinds.Overheard) Count(counts, "line-overheard");
            }
        }

        /// <summary>The words given in a step's beats (D2's recorded promise acts), as against the campaign's opening pass.</summary>
        private static int TurnPromises(EpisodeState before, EpisodeState after) =>
            after.npcSocial.acts.Count(a => a.kind == "promise" && !before.npcSocial.acts.Any(b => b.id == a.id));

        internal struct HouseAct { public string kind; public bool player; }

        internal static List<HouseAct> Acts(EpisodeState before, EpisodeState after)
        {
            var acts = new List<HouseAct>();
            string me = after.playerId;
            // The ledger's new rows, one key per act: the pair, unordered, its type and its note.
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var edge in after.relationships)
                foreach (var row in edge.events.Where(e => e.sequence >= before.nextSequence && e.description != null))
                {
                    string first = string.CompareOrdinal(edge.fromId, edge.toId) < 0 ? edge.fromId : edge.toId;
                    string second = first == edge.fromId ? edge.toId : edge.fromId;
                    if (!keys.Add(first + "|" + second + "|" + row.type + "|" + row.description)) continue;
                    bool player = first == me || second == me;
                    string kind = Classify(row.type, row.description);
                    if (kind != null) acts.Add(new HouseAct { kind = kind, player = player });
                }
            // Alliance meetings write one row a pair: one act a meeting.
            var meetings = acts.Count(a => a.kind == "meet-pair");
            acts.RemoveAll(a => a.kind == "meet-pair");
            var notes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var edge in after.relationships)
                foreach (var row in edge.events.Where(e => e.sequence >= before.nextSequence && e.type == "alliance-meeting" && e.description != null))
                    if (notes.Add(row.description)) acts.Add(new HouseAct { kind = "meet" });
            Assert.That(meetings, Is.GreaterThanOrEqualTo(notes.Count));
            // Pacts among houseguests, words houseguests gave, and what an NPC overheard.
            foreach (var pact in after.alliances.Where(a => a.id.StartsWith("alliance-npc-", StringComparison.Ordinal) && !before.alliances.Any(b => b.id == a.id)))
                acts.Add(new HouseAct { kind = "pact" });
            var promisesBefore = new HashSet<string>(CommitmentReferences.Promises(before).Select(p => p.id), StringComparer.Ordinal);
            foreach (var promise in CommitmentReferences.Promises(after).Where(p => p.id != null && p.id.StartsWith("promise-npc-", StringComparison.Ordinal) && !promisesBefore.Contains(p.id)))
                acts.Add(new HouseAct { kind = "promise" });
            var heard = new HashSet<string>(before.memories.Where(m => m.text != null && m.text.StartsWith("I overheard ", StringComparison.Ordinal))
                .Select(m => m.ownerId + "|" + m.text), StringComparer.Ordinal);
            foreach (var memory in after.memories.Where(m => m.ownerId != me && m.text != null && m.text.StartsWith("I overheard ", StringComparison.Ordinal)))
                if (heard.Add(memory.ownerId + "|" + memory.text)) acts.Add(new HouseAct { kind = "eavesdrop" });
            return acts;
        }

        /// <summary>A ledger row's act, by its type and the words the house's verbs write; null for anything else.</summary>
        private static string Classify(string type, string note)
        {
            switch (type)
            {
                case "talk":
                    if (note.Contains(" courted ")) return "court";
                    if (note.Contains(" talked about ")) return "hunt";
                    return note.Contains(" spent time with ") ? "talk" : null;
                case "alliance-meeting": return "meet-pair";
                case "rumor":
                    // A hunt's word against its threat is the hunt's; a rumour is the one that names a listener.
                    if (note.StartsWith("What ", StringComparison.Ordinal)) return null;
                    return note.StartsWith("Something ", StringComparison.Ordinal) || note.EndsWith(" is the biggest threat in this house.", StringComparison.Ordinal) ? "rumour" : null;
                case "confrontation": return note.Contains(" had words with ") ? "confront" : null;
                case "eavesdrop": return note.Contains(" caught you ") ? null : "eavesdrop";
                case "campaign": return "campaign";
                default: return null;
            }
        }

        // ------------------------------------------------------------ the report

        internal static void Report(string name, RunCounts run)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            TestContext.Out.WriteLine(name + ":");
            foreach (int size in Sizes)
            {
                if (!run.bySize.TryGetValue(size, out var c)) continue;
                double npcWeeks = Math.Max(1, Get(c, "npc-weeks")), weeks = Math.Max(1, Get(c, "weeks")), seasons = Math.Max(1, Get(c, "seasons"));
                TestContext.Out.WriteLine("  n" + size + ": " + Get(c, "seasons") + " seasons, " + Get(c, "weeks") + " weeks, " + Get(c, "npc-weeks") + " NPC-weeks");
                int beats = ActKinds.Sum(k => Get(c, k));
                TestContext.Out.WriteLine("    successes per NPC per week: " + (beats / npcWeeks).ToString("0.00", inv) + " ("
                    + string.Join(", ", ActKinds.Select(k => k + " " + (Get(c, k) / npcWeeks).ToString("0.000", inv))) + ")");
                TestContext.Out.WriteLine("    court " + (Get(c, "court") / weeks).ToString("0.00", inv) + "/week, campaign " + (Get(c, "campaign") / weeks).ToString("0.00", inv) + "/week");
                int involving = ActKinds.Sum(k => Get(c, "player-" + k));
                TestContext.Out.WriteLine("    player-involving acts per week: " + (involving / weeks).ToString("0.00", inv) + " ("
                    + string.Join(", ", ActKinds.Where(k => Get(c, "player-" + k) > 0).Select(k => k + " " + (Get(c, "player-" + k) / weeks).ToString("0.00", inv))) + ")");
                TestContext.Out.WriteLine("    reply cards per week: " + string.Join(", ", ReplyCards.All.Select(k => k + " " + (Get(c, "card-" + k) / weeks).ToString("0.00", inv))));
                TestContext.Out.WriteLine("    NPC-only pacts per season: " + (Get(c, "season-pacts") / seasons).ToString("0.00", inv)
                    + "; NPC promises per season: " + string.Join(", ", new[] { PromiseKind.Vote, PromiseKind.Safety, PromiseKind.AllianceLoyalty, PromiseKind.FinalTwo }
                        .Select(k => k + " " + (Get(c, "season-promise-" + k) / seasons).ToString("0.00", inv))));
                foreach (int week in new[] { 3, 4 })
                    if (run.npcScoreWeek4.TryGetValue(size * 10 + week, out var npcScores) && npcScores.Count > 0)
                        TestContext.Out.WriteLine("    mean |score| at week " + week + "'s HoH: NPC<->NPC " + npcScores.Average().ToString("0.00", inv)
                            + ", NPC->player " + run.playerScoreWeek4[size * 10 + week].Average().ToString("0.00", inv));
                TestContext.Out.WriteLine("    promises at the campaign's opening (standing business): " + (Get(c, "promise-campaign-open") / weeks).ToString("0.00", inv) + "/week");
                if (Get(c, "line-sighting") + Get(c, "line-overheard") > 0)
                    TestContext.Out.WriteLine("    sightings " + Get(c, "line-sighting") + ", overheard " + Get(c, "line-overheard"));
            }
        }
    }
}
#endif
