#if !UNITY_5_3_OR_NEWER
// D2's season evidence, reproducible on demand: Tools/SimulationTests only. Unity's batch runner
// executes [Explicit] tests, so the file is compiled out of the editor's assemblies.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// D2, the all-week NPC strategy (WAVE-D-NPC-PACTS-PLAN §4), over whole seasons.
    ///
    /// <para><b>The measure (D2-S0).</b> What the house does with its turns while they are all spent at
    /// once, as the social week opens (<see cref="NpcSocialActions.Settle"/>): the director's own fresh
    /// seasons (<see cref="ShippedRules.ApplyFresh"/>, the all-week rules held off) in houses of 6, 8 and 12,
    /// and the sixteen-person stress house on the combined roster, six seeds each, played by the digests'
    /// walker and by their busy player. Each of the house's acts is read from what a step wrote - the ledger's
    /// new rows, the pacts and words it made, the memories, the lines to the player and the reply cards - so the
    /// same reading measures any cadence the same way.</para>
    ///
    /// <para><b>Off.</b> The all-week rules play in the week's windows, which only the digests' director set
    /// has, and need nothing else of the commitment rules; <see cref="RecordedUnderCommitments"/> holds the 54
    /// seasons <see cref="CommitmentRulesSeasonDigests"/> plays under the commitment rules as the build before
    /// Wave D played them (2ea986df), and with the all-week rules off every one is byte-identical. (The seasons
    /// without the commitment rules are <see cref="CommitmentRulesSeasonDigests"/>' first half.)</para>
    ///
    /// <para><b>On.</b> The same 54 seasons with the all-week rules from week one wherever the week runs in
    /// windows (the director set; the legacy and harness sets have no windows and never play them), and the
    /// combined Wave D digest (§5 item 2): the fresh seasons with all three designs on - story, hearing,
    /// commitment, lever and week rules with them - at 6, 8, 12 and 16, by the walker, the busy player and a
    /// watcher who sees every act the house stages, up to the window's three; each against the same seasons
    /// with only the all-week rules off. Every command legal, and every step checked: a close and a full tick of
    /// every state draw nothing from the season's stream, a close never reaches the player, an act between two
    /// houseguests moves no arc of the player's, the week's acts stay within their bound and each houseguest
    /// within the week's beats, and every window's plan is spent before the window ends. Counted: beats a window
    /// and a week by house size, acts by kind, pacts formed all week against the weekly pass, words by kind,
    /// sightings and overheard clauses, player-involving acts and reply cards, mean |score| at week 4, D4's
    /// leaks, D3's war rooms, and the 256-line log's reach in weeks.</para>
    ///
    /// <para>Explicit: run with <c>dotnet test Tools/SimulationTests --filter "FullyQualifiedName~AllWeekSeasonDigests"</c>,
    /// one test at a time (the first takes most of ten minutes).</para>
    /// </summary>
    public sealed class AllWeekSeasonDigests
    {
        /// <summary>CommitmentRulesSeasonDigests.Run(true) at 2ea986df, before Wave D: the commitment rules on, Wave D's rules off.</summary>
        private static readonly string[] RecordedUnderCommitments =
        {
            "director n6 s1 898b5b200d4a3df9",
            "director n6 s2 096f334c6d8216da",
            "director n6 s3 0857ac1d72ad3cc5",
            "director n6 s4 94f5d2c67e988a29",
            "director n6 s5 7650f12a2770d630",
            "director n6 s6 b0530733d9656a36",
            "director n8 s1 1d254c0c125d5f13",
            "director n8 s2 b3131af609fb8f26",
            "director n8 s3 2f20d6bef43bc83e",
            "director n8 s4 3caf55434baeae87",
            "director n8 s5 9f07c3effe440228",
            "director n8 s6 2ba9f1b4de63f2d1",
            "director n12 s1 aec86623fc888dc4",
            "director n12 s2 7718ab85ebbe7df8",
            "director n12 s3 9a191dd79d89484c",
            "director n12 s4 41c7613bc9ace68a",
            "director n12 s5 c6c9a228179d4f6b",
            "director n12 s6 52ca890a7ac4ba1d",
            "legacy n6 s1 64f24a5b468b2885",
            "legacy n6 s2 55dc0f40ed5cdc42",
            "legacy n6 s3 b4d7f6c013c95765",
            "legacy n6 s4 e847b367f9ed9fd9",
            "legacy n6 s5 64476676a8bb7bc6",
            "legacy n6 s6 35bcc235b7acf771",
            "legacy n8 s1 ceba594228916acb",
            "legacy n8 s2 6a485be47dc2e69b",
            "legacy n8 s3 d98bb38d85a93857",
            "legacy n8 s4 b41beb533029b754",
            "legacy n8 s5 ced9a9d22e63ebb9",
            "legacy n8 s6 3da53f0a62835388",
            "legacy n12 s1 a150ee7cc958dba4",
            "legacy n12 s2 be056d29ce2dc5f1",
            "legacy n12 s3 e7ce0292916f7d0a",
            "legacy n12 s4 b487ab069ae80e98",
            "legacy n12 s5 95cee828ef343a18",
            "legacy n12 s6 6909159c43f4e119",
            "harness n6 s1 33094b4cdeddc6c4",
            "harness n6 s2 4e6cda838bd6ad61",
            "harness n6 s3 4cbe39765966c74f",
            "harness n6 s4 5736a948423a7d02",
            "harness n6 s5 6d5a0caa7f3ba54d",
            "harness n6 s6 9ef3876fcb40dd08",
            "harness n8 s1 264e3f0098e3b8f0",
            "harness n8 s2 7adddfeee8a92259",
            "harness n8 s3 b27eac80a7796ec0",
            "harness n8 s4 20b580d7316c2d03",
            "harness n8 s5 0f4cd0db117e3a92",
            "harness n8 s6 a376a9f6278a6b55",
            "harness n12 s1 b8759acae2a69ddc",
            "harness n12 s2 a6b119966d28256e",
            "harness n12 s3 a925b7fc4689adc6",
            "harness n12 s4 c3b832136bca38d7",
            "harness n12 s5 55da6cd996e653e7",
            "harness n12 s6 5f6750cd56f31fb7",
        };

        [Test, Explicit("Plays 54 seasons under the commitment rules to their ends: D2 off, every digest as it was.")]
        public void WithoutTheAllWeekRulesEverySeasonUnderTheCommitmentRulesPlaysAsItDidBeforeThem()
        {
            var played = CommitmentRulesSeasonDigests.Run(true, out int errors, out _);
            Assert.That(errors, Is.Zero, string.Join("\n", played.Where(line => line.Contains(" ERROR "))));
            Assert.That(played, Has.Count.EqualTo(RecordedUnderCommitments.Length), "Every season has its recorded digest.");
            var moved = played.Select(line => string.Join(" ", line.Split(' ').Take(4))).Where(key => !RecordedUnderCommitments.Contains(key)).ToList();
            Assert.That(moved, Is.Empty, "A season under the commitment rules without the all-week rules moved:\n" + string.Join("\n", moved));
        }

        [Test, Explicit("Plays the 54 recorded seasons with the all-week rules wherever they play: every command legal, every beat drawing nothing.")]
        public void UnderTheAllWeekRulesTheRecordedSeasonsStayLegalAndDrawNothing()
        {
            var counts = new Dictionary<string, int>();
            var errors = new List<string>();
            int played = 0;
            foreach (var config in new[] { "director", "legacy", "harness" })
            foreach (int size in new[] { 6, 8, 12 })
            for (uint seed = 1; seed <= 6; seed++)
            {
                var start = (EpisodeState)SeasonOf.Invoke(null, new object[] { config, size, seed, true });
                if (start.weekRulesStartWeek < 1) continue; // No windows: the rules have nowhere to play.
                EpisodeEngine.EnableAllWeek(start);
                played++;
                string error = Play(start, "busy", seed, counts, null, size, probe: true);
                if (error != null) errors.Add(config + " n" + size + " s" + seed + ": " + error);
            }
            TestContext.Out.WriteLine(played + " of 54 recorded seasons play the all-week rules: " + string.Join(", ", counts.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => k.Key + "=" + k.Value)));
            Assert.That(errors, Is.Empty, "Every command a season under the all-week rules was given is legal:\n" + string.Join("\n", errors));
            Assert.That(played, Is.EqualTo(18), "The director set: the only one with the week's windows.");
            AssertProbes(counts, "the recorded seasons");
        }

        [Test, Explicit("Plays 168 fresh seasons, all of Wave D on and the all-week rules off beside them: the combined digest and D2's numbers.")]
        public void TheCombinedWaveDDigest()
        {
            var settle = Measure(allWeek: false, players: new[] { "walker", "busy" });
            var allWeek = Measure(allWeek: true, players: new[] { "walker", "busy" });
            var watched = Measure(allWeek: true, players: new[] { "watcher" });
            var trioOff = Measure(allWeek: false, players: new[] { "trio" });
            var trioOn = Measure(allWeek: true, players: new[] { "trio" });
            Report("the weekly pass (all-week rules off), walker and busy player", settle);
            Report("all week (all three designs on), walker and busy player", allWeek);
            Report("all week, a watcher who sees every act staged", watched);
            Compare(settle, allWeek);
            CompareWarRooms(trioOff, trioOn);
            foreach (var (name, run) in new[] { ("off", settle), ("on", allWeek), ("watched", watched), ("trio off", trioOff), ("trio on", trioOn) })
                Assert.That(run.errors, Is.Empty, "Every command was legal (" + name + "):\n" + string.Join("\n", run.errors));
            var all = Total(allWeek).Concat(Total(watched)).Concat(Total(trioOn)).GroupBy(k => k.Key)
                .ToDictionary(g => g.Key, g => g.Key == "log-reach-min" ? g.Min(k => k.Value) : g.Sum(k => k.Value));
            AssertProbes(all, "the fresh seasons");
            Assert.That(Get(all, "beat"), Is.GreaterThan(0), "The house played its beats.");
            Assert.That(Get(all, "line-sighting") + Get(all, "line-overheard"), Is.GreaterThan(0), "The watcher saw acts.");
            Assert.That(Get(all, "sightings-past-cap"), Is.Zero, "At most three seen a window.");
            Assert.That(Get(all, "unseen-line"), Is.Zero, "No D2 line for an act the player did not see (acceptance e).");
            Assert.That(Get(all, "pact-named"), Is.Zero, "No D2 line names a pact.");
            Assert.That(Get(all, "season-stream-moved-by-a-witness"), Is.Zero, "A witness draws nothing.");
            Assert.That(Get(all, "leak-roll"), Is.GreaterThan(0), "D4's leaks rolled alongside.");
            Assert.That(Get(Total(trioOn), "war-room"), Is.GreaterThan(0), "D3's war rooms met alongside.");
        }

        [Test, Explicit("Plays 48 fresh seasons with the house's turns at the social week's opening: a report.")]
        public void SettleByHouseSize()
        {
            var run = Measure(allWeek: false, players: new[] { "walker", "busy" });
            Report("settle", run);
            Assert.That(run.errors, Is.Empty, "Every command was legal:\n" + string.Join("\n", run.errors));
            Assert.That(run.bySize.Values.Sum(c => Get(c, "pact")), Is.GreaterThan(0), "Houseguests formed pacts.");
        }

        // ------------------------------------------------------------ the run

        internal static readonly int[] Sizes = { 6, 8, 12, 16 };
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

        private static Dictionary<string, int> Total(RunCounts run) =>
            run.bySize.Values.SelectMany(c => c).GroupBy(k => k.Key).ToDictionary(g => g.Key, g => g.Sum(k => k.Value));

        private static readonly MethodInfo SeasonOf = Private("Season", typeof(string), typeof(int), typeof(uint), typeof(bool));
        private static readonly MethodInfo BusyOf = Private("Busy", typeof(EpisodeState), typeof(uint), typeof(int), typeof(bool));
        private static readonly MethodInfo NextOf = Private("NextCommand", typeof(EpisodeState));
        private static readonly MethodInfo TrioOf = Private(typeof(PactPlanSeasonDigests), "ATrioOfThePlayers", typeof(EpisodeState));
        private static readonly MethodInfo WarRoomOf = Private(typeof(PactPlanSeasonDigests), "WarRoom", typeof(EpisodeState));
        private static readonly MethodInfo AnswerOf = Private(typeof(PactPlanSeasonDigests), "Answer", typeof(EpisodeState), typeof(uint));

        /// <summary>The digests' scripted players, by reflection: the same moves.</summary>
        private static MethodInfo Private(string name, params Type[] parameters) => Private(typeof(CommitmentRulesSeasonDigests), name, parameters);

        private static MethodInfo Private(Type owner, string name, params Type[] parameters) =>
            owner.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static, null, parameters, null)
            ?? throw new InvalidOperationException(owner.Name + "." + name + " is the harness's player.");

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

        /// <summary>The watcher's sighting: the first act the house would stage now, as the director's watch would see it.</summary>
        private static EpisodeCommand Watch(EpisodeState s)
        {
            var act = EpisodeEngine.StageableActs(s, 1).FirstOrDefault();
            return act == null ? null : new EpisodeCommand
            {
                id = "watch-" + s.revision + "-" + act.id, actorId = s.playerId, kind = EpisodeCommandKind.WitnessNpcAct,
                targetId = act.actorId, secondTargetId = act.partnerId, text = act.id, expectedRevision = s.revision, expectedPhase = s.phase,
            };
        }

        private static RunCounts Measure(bool allWeek, string[] players)
        {
            var run = new RunCounts();
            foreach (int size in Sizes)
            foreach (string player in players)
            for (uint seed = 1; seed <= Seeds; seed++)
            {
                if (!run.bySize.TryGetValue(size, out var counts)) run.bySize[size] = counts = new Dictionary<string, int>();
                var start = Fresh(size, seed);
                start.allWeekRulesStartWeek = allWeek ? 1 : 0;
                // D3's trio player: a pact of three of the player's from week one (PactPlanSeasonDigests).
                if (player == "trio") TrioOf.Invoke(null, new object[] { start });
                string error = Play(start, player, seed, counts, run, size, probe: allWeek);
                if (error != null) run.errors.Add(player + " n" + size + " s" + seed + ": " + error);
                Count(counts, "seasons");
            }
            return run;
        }

        private static string Play(EpisodeState start, string player, uint seed, Dictionary<string, int> counts, RunCounts run, int size, bool probe)
        {
            var engine = new EpisodeEngine(start);
            int scored = 0;
            for (int i = 0; i < 8000 && engine.Snapshot.phase != EpisodePhase.Finished; i++)
            {
                var s = engine.Snapshot;
                // Mean |score| as a week's Head of Household competition opens: week 4's, and week 3's for
                // the six-house, whose fourth week is the finale's.
                if (run != null && s.phase == EpisodePhase.HoH && (s.week == 3 || s.week == 4) && scored != s.week)
                {
                    scored = s.week;
                    var npcs = s.Active.Where(c => !c.isPlayer).ToList();
                    int key = size * 10 + s.week;
                    if (!run.npcScoreWeek4.ContainsKey(key)) { run.npcScoreWeek4[key] = new List<double>(); run.playerScoreWeek4[key] = new List<double>(); }
                    foreach (var a in npcs) foreach (var b in npcs.Where(x => x.id != a.id)) run.npcScoreWeek4[key].Add(Math.Abs(s.Score(a.id, b.id)));
                    foreach (var a in npcs) run.playerScoreWeek4[key].Add(Math.Abs(s.Score(a.id, s.playerId)));
                }
                EpisodeCommand applied = null;
                CommandResult result = null;
                if (player == "watcher")
                {
                    var watch = Watch(s);
                    if (watch != null)
                    {
                        var seen = engine.Apply(watch);
                        if (!seen.accepted) return "week " + s.week + " " + s.phase + " the watcher's sighting refused: " + seen.reason;
                        applied = watch; result = seen;
                    }
                }
                if (player == "trio")
                    foreach (var own in new[] { (EpisodeCommand)AnswerOf.Invoke(null, new object[] { s, seed }), (EpisodeCommand)WarRoomOf.Invoke(null, new object[] { s }) })
                    {
                        if (own == null || result != null) continue;
                        var tried = engine.Apply(own);
                        if (tried.accepted) { applied = own; result = tried; }
                        else if (own.kind == EpisodeCommandKind.AnswerPactPlan) return "week " + s.week + " " + s.phase + " AnswerPactPlan refused: " + tried.reason;
                    }
                for (int attempt = 0; player != "walker" && attempt < 6 && result == null; attempt++)
                {
                    var own = Busy(s, seed, attempt);
                    if (own == null) break;
                    var tried = engine.Apply(own);
                    if (tried.accepted) { applied = own; result = tried; }
                }
                if (result == null)
                {
                    applied = Walk(s);
                    result = engine.Apply(applied);
                    if (!result.accepted) return "week " + s.week + " " + s.phase + " " + applied.kind + ": " + result.reason;
                }
                Step(s, result.state, counts);
                if (probe) Probe(s, applied, result.state, counts);
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
                // D4's weekly leak, as its own harness reads it: each pact that rolls, and each whose coin lands.
                if (AllianceLeaks.On(before))
                    foreach (var pact in before.alliances.Where(p => AllianceLeaks.Rolls(before, p) != null))
                    {
                        Count(counts, "leak-roll");
                        if (AllianceLeaks.Leaks(before, pact)) Count(counts, "leak");
                    }
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
            // D3's war rooms, as its own harness reads them: each plan a meeting opened.
            Count(counts, "war-room", after.ledger.plans.Count(p => !before.ledger.plans.Any(b => b.week == p.week && b.allianceId == p.allianceId)));
            foreach (var line in after.events.Where(e => e.sequence >= before.nextSequence))
            {
                if (line.kind == WaveDEventKinds.Sighting) Count(counts, "line-sighting");
                else if (line.kind == WaveDEventKinds.Overheard) Count(counts, "line-overheard");
                else if (line.kind == "eavesdrop" && line.text != null && Clauses.Any(c => line.text.Contains(c))) Count(counts, "line-overheard-clause");
            }
            // The log's reach, in weeks back, once it is full: the story page reads three (§5 item 4).
            if (after.events.Count >= 256)
            {
                Count(counts, "log-full-steps");
                int reach = after.week - after.events[0].week;
                if (!counts.TryGetValue("log-reach-min", out int least) || reach < least) counts["log-reach-min"] = reach;
            }
        }

        /// <summary>The words only D2's act clauses say, by which a listen-in's line is known to carry one.</summary>
        private static readonly string[] Clauses =
            { " agreed to work together.", " were going over a plan.", " their word.", " was making their case to ", " is the biggest threat in this house.", " has to go." };

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

        // ------------------------------------------------------------ what the all-week rules must keep

        /// <summary>
        /// The all-week rules' checks on one committed step (§4.6, §5 item 2): the acts it recorded, by kind and
        /// window; a close and a full tick of the state after it, which must draw nothing from the season's stream,
        /// a close never reaching the player and an act between two houseguests moving no arc of the player's;
        /// the week's bound and beats; the window it ended, its plan spent; and what the player was told.
        /// </summary>
        private static void Probe(EpisodeState before, EpisodeCommand command, EpisodeState after, Dictionary<string, int> counts)
        {
            string me = after.playerId;
            bool Involves(NpcActState a) => a.actorId == me || a.partnerId == me || a.subjectId == me;
            // What the step recorded. Acts the week's turn cleared in the step are counted by Step's reading.
            if (after.week == before.week)
                foreach (var act in New(before, after))
                {
                    Count(counts, "act-" + act.kind);
                    if (EpisodeEngine.IsBeat(act)) { Count(counts, "beat"); Count(counts, "beat-w" + act.window); }
                    if (Involves(act)) Count(counts, "act-with-the-player");
                    if (act.room != null) Count(counts, "act-staged");
                }
            // Every beat left in the open window, at its close and at a full tick.
            int window = EpisodeEngine.Window(after);
            if (EpisodeEngine.AllWeekOn(after) && window != Windows.None && !EpisodeEngine.IsFirstNight(after))
            {
                Count(counts, "probes");
                var closed = after.Clone();
                EpisodeEngine.Close(closed);
                if (closed.randomState != after.randomState) Count(counts, "beat-drew");
                if (New(after, closed).Any(Involves) || closed.replyCards.Count != after.replyCards.Count) Count(counts, "close-reached-the-player");
                if (JsonConvert.SerializeObject(closed.relationshipArcs) != JsonConvert.SerializeObject(after.relationshipArcs)) Count(counts, "npc-act-moved-an-arc");
                var ticked = after.Clone();
                while (ticked.windowActions.Count < Windows.Count) ticked.windowActions.Add(0);
                ticked.windowActions[window] = 100;
                EpisodeEngine.CatchUp(ticked);
                if (ticked.randomState != after.randomState) Count(counts, "beat-drew");
                if (!New(after, ticked).Any(Involves) && JsonConvert.SerializeObject(ticked.relationshipArcs) != JsonConvert.SerializeObject(after.relationshipArcs))
                    Count(counts, "npc-act-moved-an-arc");
            }
            // The week's bound and each houseguest's beats.
            var week = after.week == before.week ? after : before;
            if (week.npcSocial.acts.Count > EpisodeEngine.MostActs(week)) Count(counts, "acts-past-the-bound");
            foreach (var npc in week.contestants.Where(c => !c.isPlayer))
                if (week.npcSocial.acts.Count(a => a.actorId == npc.id && EpisodeEngine.IsBeat(a)) > EpisodeEngine.BeatsAWeek) Count(counts, "beats-past-the-quota");
            // A window ended: its plan spent, wherever the step left it (acceptance b, restated per decision 1).
            int left = EpisodeEngine.Window(before);
            if (EpisodeEngine.AllWeekOn(before) && left != Windows.None && (EpisodeEngine.Window(after) != left || after.week != before.week)
                && before.npcSocial.beatWeek == before.week && before.npcSocial.beatWindow == left)
            {
                Count(counts, "window-ends");
                if (before.npcSocial.beatPlan.Count == 0) Count(counts, "window-empty-plan");
                bool spent = before.npcSocial.beatsFired == before.npcSocial.beatPlan.Count
                    || (after.npcSocial.beatWeek == before.week && after.npcSocial.beatWindow == left && after.npcSocial.beatsFired == after.npcSocial.beatPlan.Count);
                if (!spent) Count(counts, "window-left-unspent");
            }
            // What the player was told: a sighting only of an act they saw, never a pact, at most three a window.
            foreach (var line in after.events.Where(e => e.sequence >= before.nextSequence && (e.kind == WaveDEventKinds.Sighting || e.kind == WaveDEventKinds.Overheard)))
            {
                if (command.kind != EpisodeCommandKind.WitnessNpcAct) Count(counts, "unseen-line");
                if (after.alliances.Any(p => !string.IsNullOrEmpty(p.name) && line.text.Contains(p.name))) Count(counts, "pact-named");
            }
            if (command.kind == EpisodeCommandKind.WitnessNpcAct && after.randomState != before.randomState) Count(counts, "season-stream-moved-by-a-witness");
            if (after.npcSocial.acts.Where(a => a.sighted).GroupBy(a => a.window).Any(g => g.Count() > EpisodeEngine.SightingsAWindow)) Count(counts, "sightings-past-cap");
            // The week's lines of D2's own, for the log's volume (§5 item 4).
            Count(counts, "d2-lines", after.events.Count(e => e.sequence >= before.nextSequence && (e.kind == WaveDEventKinds.Sighting || e.kind == WaveDEventKinds.Overheard)));
        }

        private static List<NpcActState> New(EpisodeState before, EpisodeState after) =>
            after.npcSocial.acts.Where(a => !before.npcSocial.acts.Any(b => b.id == a.id && b.week == a.week)).ToList();

        private static void AssertProbes(Dictionary<string, int> counts, string what)
        {
            Assert.That(Get(counts, "probes"), Is.GreaterThan(0), what + ": the probes ran.");
            Assert.That(Get(counts, "beat-drew"), Is.Zero, what + ": no beat draws from the season's stream.");
            Assert.That(Get(counts, "close-reached-the-player"), Is.Zero, what + ": a close never reaches the player.");
            Assert.That(Get(counts, "npc-act-moved-an-arc"), Is.Zero, what + ": an act between two houseguests moves no arc of the player's.");
            Assert.That(Get(counts, "acts-past-the-bound"), Is.Zero, what + ": the week's acts within their bound.");
            Assert.That(Get(counts, "beats-past-the-quota"), Is.Zero, what + ": three beats a houseguest a week at most.");
            Assert.That(Get(counts, "window-left-unspent"), Is.Zero, what + ": every window's plan spent before it ends.");
            Assert.That(Get(counts, "window-ends"), Is.GreaterThan(0), what + ": windows ended.");
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
                TestContext.Out.WriteLine("    D4 leaks: " + Get(c, "leak") + " of " + Get(c, "leak-roll") + " rolls; D3 war rooms: " + Get(c, "war-room")
                    + "; full-log steps: " + Get(c, "log-full-steps") + ", the log's least reach once full: "
                    + (c.ContainsKey("log-reach-min") ? c["log-reach-min"].ToString(inv) + " weeks back" : "never full"));
                if (Get(c, "probes") > 0)
                {
                    TestContext.Out.WriteLine("    beats per week: " + (Get(c, "beat") / weeks).ToString("0.00", inv) + " (by window: "
                        + string.Join(", ", Enumerable.Range(0, Windows.Count).Select(w => Windows.Names[w] + " " + (Get(c, "beat-w" + w) / weeks).ToString("0.00", inv))) + ")");
                    TestContext.Out.WriteLine("    acts by kind: " + string.Join(", ", NpcActKinds.All.Select(k => k + " " + Get(c, "act-" + k)))
                        + "; staged " + Get(c, "act-staged") + "; with the player " + Get(c, "act-with-the-player"));
                    TestContext.Out.WriteLine("    windows ended " + Get(c, "window-ends") + ", with an empty plan " + Get(c, "window-empty-plan")
                        + "; D2's lines a week " + (Get(c, "d2-lines") / weeks).ToString("0.00", inv));
                }
                if (Get(c, "line-sighting") + Get(c, "line-overheard") + Get(c, "line-overheard-clause") > 0)
                    TestContext.Out.WriteLine("    sightings " + Get(c, "line-sighting") + ", overheard " + Get(c, "line-overheard")
                        + ", listen-ins that heard an act " + Get(c, "line-overheard-clause"));
            }
        }

        /// <summary>The all-week rules against the weekly pass on the same seasons: drift and the player's share (§4.6 b, decision 2).</summary>
        private static void Compare(RunCounts settle, RunCounts allWeek)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            TestContext.Out.WriteLine("all week against the weekly pass:");
            foreach (int size in Sizes)
            {
                if (!settle.bySize.TryGetValue(size, out var off) || !allWeek.bySize.TryGetValue(size, out var on)) continue;
                double offWeeks = Math.Max(1, Get(off, "weeks")), onWeeks = Math.Max(1, Get(on, "weeks"));
                double offInvolving = ActKinds.Sum(k => Get(off, "player-" + k)) / offWeeks, onInvolving = ActKinds.Sum(k => Get(on, "player-" + k)) / onWeeks;
                double offSeasons = Math.Max(1, Get(off, "seasons")), onSeasons = Math.Max(1, Get(on, "seasons"));
                string line = "  n" + size + ": player-involving acts a week " + offInvolving.ToString("0.00", inv) + " -> " + onInvolving.ToString("0.00", inv)
                    + " (" + (offInvolving > 0 ? (onInvolving / offInvolving).ToString("P0", inv) : "n/a") + ")"
                    + "; NPC-only pacts a season " + (Get(off, "season-pacts") / offSeasons).ToString("0.00", inv) + " -> " + (Get(on, "season-pacts") / onSeasons).ToString("0.00", inv)
                    + "; D4 leaks a roll " + Rate(off, "leak", "leak-roll") + " -> " + Rate(on, "leak", "leak-roll")
                    + "; D3 war rooms a season " + (Get(off, "war-room") / offSeasons).ToString("0.00", inv) + " -> " + (Get(on, "war-room") / onSeasons).ToString("0.00", inv);
                foreach (int week in new[] { 3, 4 })
                    if (settle.npcScoreWeek4.TryGetValue(size * 10 + week, out var a) && allWeek.npcScoreWeek4.TryGetValue(size * 10 + week, out var b) && a.Count > 0 && b.Count > 0)
                        line += "; mean |score| week " + week + " NPC<->NPC " + a.Average().ToString("0.00", inv) + " -> " + b.Average().ToString("0.00", inv)
                            + " (" + ((b.Average() / a.Average()) - 1).ToString("+0%;-0%", inv) + "), NPC->player "
                            + settle.playerScoreWeek4[size * 10 + week].Average().ToString("0.00", inv) + " -> " + allWeek.playerScoreWeek4[size * 10 + week].Average().ToString("0.00", inv);
                TestContext.Out.WriteLine(line);
            }

            string Rate(Dictionary<string, int> c, string hits, string of) =>
                Get(c, of) == 0 ? "n/a" : ((double)Get(c, hits) / Get(c, of)).ToString("0.000", inv);
        }

        /// <summary>D3's war rooms with the house's beats and without, by the trio player (§5 item 3).</summary>
        private static void CompareWarRooms(RunCounts off, RunCounts on)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            TestContext.Out.WriteLine("D3's trio player, the all-week rules off and on:");
            foreach (int size in Sizes)
            {
                if (!off.bySize.TryGetValue(size, out var a) || !on.bySize.TryGetValue(size, out var b)) continue;
                double seasons = Math.Max(1, Get(a, "seasons"));
                TestContext.Out.WriteLine("  n" + size + ": war rooms a season " + (Get(a, "war-room") / seasons).ToString("0.00", inv)
                    + " -> " + (Get(b, "war-room") / Math.Max(1, Get(b, "seasons"))).ToString("0.00", inv)
                    + "; D4 leaks " + Get(a, "leak") + "/" + Get(a, "leak-roll") + " -> " + Get(b, "leak") + "/" + Get(b, "leak-roll")
                    + "; NPC-only pacts a season " + (Get(a, "season-pacts") / seasons).ToString("0.00", inv) + " -> " + (Get(b, "season-pacts") / Math.Max(1, Get(b, "seasons"))).ToString("0.00", inv));
            }
        }
    }
}
#endif
