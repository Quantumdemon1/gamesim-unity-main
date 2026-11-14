#if !UNITY_5_3_OR_NEWER
// The role moments' held seasons, reproducible on demand: Tools/SimulationTests only. Unity's batch runner
// executes [Explicit] tests, so the file is compiled out of the editor's assemblies.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Story rules 11's held seasons (plan 32 §N and §Q2, HT0): the replay guard every house-talk slice keeps byte-equal.
    /// The director's own fresh seasons (<see cref="AllWeekSeasonDigests.Fresh"/>: <see cref="ShippedRules.ApplyFresh"/>
    /// on a roster's house, or the stress house past twelve) in houses of 6, 8, 12 and 16, six seeds each, with
    /// <c>story.rulesVersion</c> written as <see cref="StoryRules.Roles"/> BY NAME after ApplyFresh - as
    /// CommitmentRulesSeasonDigests holds its seasons at <see cref="StoryRules.Threads"/> - so a later enrolment that moves
    /// <see cref="StoryRules.Current"/> leaves them where they are. Each is played to its end by the digests' walker and by
    /// their busy player, every command the walker gives legal and every season finishing, and digested as each week opens
    /// and at the end - the whole state's JSON, then every reader CommitmentRulesSeasonDigests digests (warmth, threat,
    /// trust, the jury's obligations and score, the deal odds and their words, the story's odds, the houseguest notes, Game
    /// Sense and the jury read), by reflection, so both files read the same readers the same way - with a mark at every
    /// phase change between (the phase, the revision and the season's random stream). A checkpoint at every phase change,
    /// as the commitment digests take, made one run of the 48 seasons last 30 minutes in the lane's queue; the weekly
    /// checkpoints keep a run, and the re-record's two runs, inside one queue job.
    ///
    /// <para><see cref="Recorded"/> holds the digests the R7 stack played (R7's switch-on at 2892a4e8, the role moments
    /// switched on, before any plan-32 code: HT0 adds only the three level constants to the simulation). A house-talk slice
    /// keeps every one: its rules read their own level (12, 13 or 14), never these seasons'. A change to the role moments
    /// themselves (a balance cut) moves them and is re-recorded with its reason; nothing else may move one.</para>
    ///
    /// <para>Explicit: run with <c>dotnet test Tools/SimulationTests --filter "FullyQualifiedName~RolesSeasonDigests"</c>.
    /// With the environment variable <c>GAMESIM_ROLES_DIGESTS_OUT</c> set to a file path, the run also writes its lines
    /// there, one a season, for the re-record.</para>
    /// </summary>
    public sealed class RolesSeasonDigests
    {
        /// <summary>Each season as the R7 stack's head played it: player, house size, seed, digest.</summary>
        private static readonly string[] Recorded =
        {
            "walker n6 s1 c7c89f1da2e994da",
            "walker n6 s2 4d537f9ca4033e30",
            "walker n6 s3 49a285a28e458873",
            "walker n6 s4 d7a65db713aa1ccc",
            "walker n6 s5 82c58640ad6139fc",
            "walker n6 s6 5a32a5c035358fdf",
            "busy n6 s1 82a82dfeb432627d",
            "busy n6 s2 d2fa7e50cc2f02d6",
            "busy n6 s3 51a76e45c0fc5b80",
            "busy n6 s4 9596ceed54baad4e",
            "busy n6 s5 0518bd6956cd98b8",
            "busy n6 s6 120ad691bd68ef98",
            "walker n8 s1 637c34d31c42e67f",
            "walker n8 s2 8d4623a31b3590e5",
            "walker n8 s3 a24d0bed04a4230b",
            "walker n8 s4 923e6fd85350a281",
            "walker n8 s5 974263b6f006894c",
            "walker n8 s6 6eb195fc77f4b566",
            "busy n8 s1 d5255341f9c36e24",
            "busy n8 s2 dde9646c382abbd4",
            "busy n8 s3 9b6edbc5b7d95709",
            "busy n8 s4 6bb5cf326eb75d8f",
            "busy n8 s5 ccd8739afdb1a26a",
            "busy n8 s6 21e9af0a254a2194",
            "walker n12 s1 49daaa15d1fcc971",
            "walker n12 s2 9d1d52c357d978cc",
            "walker n12 s3 ea06f2d5f6d3275d",
            "walker n12 s4 0b1bdab155d3c63c",
            "walker n12 s5 d36a717fefd0eb48",
            "walker n12 s6 0b049867e951fc21",
            "busy n12 s1 899652c20032d6c4",
            "busy n12 s2 ee8e8e20cf469397",
            "busy n12 s3 8fd4eeaaf4a91950",
            "busy n12 s4 2a405b834cecb3cc",
            "busy n12 s5 19a752d5cf7973df",
            "busy n12 s6 8301c17e6fb76420",
            "walker n16 s1 aec4100819875ae1",
            "walker n16 s2 d6b79792d6a047c3",
            "walker n16 s3 defbcee01c8a5dbb",
            "walker n16 s4 4ee7678ddffdf242",
            "walker n16 s5 b8e2555769bb79ba",
            "walker n16 s6 ca3c85bb19094ed5",
            "busy n16 s1 dc8956f9f5d9c353",
            "busy n16 s2 076c3ccbc1461a01",
            "busy n16 s3 7a109acbd9400fac",
            "busy n16 s4 fda49dcec47d8ed0",
            "busy n16 s5 e10e125d6a482f6d",
            "busy n16 s6 4343a4b3a9c76d4f",
        };

        [Test, Explicit("Plays 48 fresh seasons at story rules 11 to their ends: the role moments' held digests, run on demand.")]
        public void EverySeasonAtTheRoleMomentsPlaysAsRecorded()
        {
            var played = Run(out int errors, out int rolesOn);
            string path = Environment.GetEnvironmentVariable("GAMESIM_ROLES_DIGESTS_OUT");
            if (!string.IsNullOrEmpty(path)) File.WriteAllLines(path, played);
            foreach (var line in played) TestContext.Out.WriteLine(line);
            Assert.That(errors, Is.Zero, "Every command the walker gave was legal and every season finished:\n"
                + string.Join("\n", played.Where(line => line.Contains(" ERROR "))));
            Assert.That(rolesOn, Is.EqualTo(played.Count), "Every season starts on the role moments' rules (EpisodeEngine.RolesOn).");
            Assert.That(Recorded, Has.Length.EqualTo(played.Count), "Every season has its recorded digest.");
            var moved = played.Select(Key).Where(key => !Recorded.Contains(key)).ToList();
            Assert.That(moved, Is.Empty, "A season at story rules 11 moved:\n" + string.Join("\n", moved));
        }

        // ------------------------------------------------------------ the run

        private static readonly string[] Players = { "walker", "busy" };

        private static string Key(string line) => string.Join(" ", line.Split(' ').Take(4));

        /// <summary>
        /// Plays the 48 seasons and returns a line for each: player, size, seed, digest and the season's counts. Public, so a
        /// scratch program compiled against another build can print the same lines.
        /// </summary>
        public static List<string> Run(out int errors, out int rolesOn)
        {
            var lines = new List<string>();
            errors = 0;
            rolesOn = 0;
            foreach (int size in AllWeekSeasonDigests.Sizes)
            foreach (string player in Players)
            for (uint seed = 1; seed <= AllWeekSeasonDigests.Seeds; seed++)
            {
                var start = Season(size, seed);
                if (EpisodeEngine.RolesOn(start)) rolesOn++;
                Play(start, player, seed, out string digest, out string stats, out string error);
                if (error != null) errors++;
                lines.Add(player + " n" + size + " s" + seed + " " + digest + " " + stats + (error != null ? " ERROR " + error : ""));
            }
            return lines;
        }

        /// <summary>A fresh season as the director starts one, held at story rules 11 by name.</summary>
        private static EpisodeState Season(int size, uint seed)
        {
            var s = AllWeekSeasonDigests.Fresh(size, seed);
            Assert.That(s.story.rulesStartWeek, Is.Not.Zero, "A fresh season plays the story.");
            // Held at story rules 11, the role moments, by name (plan 32 §Q1): ApplyFresh writes Current, which each
            // house-talk enrolment moves; these seasons stay the role moments' own.
            s.story.rulesVersion = StoryRules.Roles;
            Assert.That(EpisodeValidation.TryValidate(s, out var invalid), Is.True, invalid);
            return s;
        }

        private static void Play(EpisodeState start, string player, uint seed, out string digest, out string stats, out string error)
        {
            error = null;
            var engine = new EpisodeEngine(start);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var lastPhase = (EpisodePhase)(-1);
                int lastWeek = -1, i = 0, own = 0;
                for (; i < 8000 && engine.Snapshot.phase != EpisodePhase.Finished; i++)
                {
                    var s = engine.Snapshot;
                    if (s.week != lastWeek) { Checkpoint(s, hash); lastWeek = s.week; }
                    if (s.phase != lastPhase) { Mark(s, hash); lastPhase = s.phase; }
                    bool done = false;
                    for (int attempt = 0; player == "busy" && attempt < 6 && !done; attempt++)
                    {
                        var busy = AllWeekSeasonDigests.Busy(s, seed, attempt);
                        if (busy == null) break;
                        if (engine.Apply(busy).accepted) { done = true; own++; }
                    }
                    if (done) continue;
                    var next = AllWeekSeasonDigests.Walk(s);
                    var applied = engine.Apply(next);
                    if (!applied.accepted) { error = "week " + s.week + " " + s.phase + "/" + s.evictionStage + " " + next.kind + ": " + applied.reason; break; }
                }
                var final = engine.Snapshot;
                Checkpoint(final, hash);
                if (error == null && final.phase != EpisodePhase.Finished) error = "unfinished after " + i;
                stats = "cmds=" + i + " own=" + own + " week=" + final.week + " rules=" + final.story.rulesVersion + " winner=" + final.winnerId
                    + " ms=" + clock.ElapsedMilliseconds;
                digest = BitConverter.ToString(hash.GetHashAndReset()).Replace("-", "").Substring(0, 16).ToLowerInvariant();
            }
        }

        /// <summary>
        /// Each phase change's mark: where the season is, its revision and the season's random stream, so a draw taken or
        /// left between two weekly checkpoints moves the digest at the phase it happened in.
        /// </summary>
        private static void Mark(EpisodeState s, IncrementalHash hash) =>
            hash.AppendData(Encoding.UTF8.GetBytes(s.week + "/" + s.phase + "/" + s.evictionStage + "/" + s.revision + "/" + s.randomState + "\n"));

        /// <summary>The whole state's JSON, then the commitment digests' readers of it: as each week opens, and at the end.</summary>
        private static void Checkpoint(EpisodeState s, IncrementalHash hash)
        {
            var trace = new StringBuilder(JObject.FromObject(s).ToString(Formatting.None)).Append('\n');
            ReadersOf.Invoke(null, new object[] { s, trace });
            hash.AppendData(Encoding.UTF8.GetBytes(trace.ToString()));
        }

        private static readonly MethodInfo ReadersOf =
            typeof(CommitmentRulesSeasonDigests).GetMethod("Readers", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(EpisodeState), typeof(StringBuilder) }, null)
            ?? throw new InvalidOperationException("CommitmentRulesSeasonDigests.Readers is the digests' reader set.");
    }
}
#endif
