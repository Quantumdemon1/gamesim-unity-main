#if !UNITY_5_3_OR_NEWER
// The house talk's held seasons at story rules 12, reproducible on demand: Tools/SimulationTests only. Unity's batch runner
// executes [Explicit] tests, so the file is compiled out of the editor's assemblies.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Story rules 12's held seasons (plan 32 A-ON, section 3 item 5): the replay guard Stack B's slices keep byte-equal, as
    /// <see cref="RolesSeasonDigests"/> is Stack A's. The same 48 seasons - the director's own fresh seasons
    /// (<see cref="AllWeekSeasonDigests.Fresh"/>) in houses of 6, 8, 12 and 16, six seeds each, each played to its end by
    /// the digests' walker and by their busy player - with <c>story.rulesVersion</c> written as <see cref="StoryRules.Manners"/>
    /// BY NAME after ApplyFresh, so B-ON's enrolment, which moves <see cref="StoryRules.Current"/> to 13, leaves them where
    /// they are. The other levels a fresh season starts on are written by name too, at the values A-ON's head shipped: free
    /// talk rules 3 (<see cref="TalkRules.Romance"/>), the minds' rules 0 and competition rules 6 (<see cref="CompetitionRules.Trivia"/>),
    /// so a later enrolment of any of them moves none of these seasons either.
    ///
    /// <para>Each season is digested exactly as <see cref="RolesSeasonDigests"/> digests one (its checkpoints and marks, by
    /// reflection, so both files read the same state the same way): the whole state's JSON through the live schema's
    /// observer, then every reader <see cref="CommitmentRulesSeasonDigests"/> digests. <see cref="Recorded"/> holds the digests
    /// A-ON's head played, recorded twice byte for byte. A Stack B slice keeps every one: its rules read their own level (13),
    /// never these seasons'. A change to Stack A itself moves them and is re-recorded with its reason; nothing else may.</para>
    ///
    /// <para>Explicit: run with <c>dotnet test Tools/SimulationTests --filter "FullyQualifiedName=Gamesim.Tests.EditMode.MannersSeasonDigests.EverySeasonAtTheHouseTalksMannersPlaysAsRecorded"</c>.
    /// With the environment variable <c>GAMESIM_MANNERS_DIGESTS_OUT</c> set to a file path, the run also writes its lines
    /// there, one a season, for the re-record.</para>
    /// </summary>
    public sealed class MannersSeasonDigests
    {
        /// <summary>Each season as A-ON's head played it: player, house size, seed, digest.</summary>
        private static readonly string[] Recorded =
        {
            "walker n6 s1 6e477360ca4e7f42",
            "walker n6 s2 57c900ce249c8947",
            "walker n6 s3 c6ad9c87158dc566",
            "walker n6 s4 de2d11fc9ba7f911",
            "walker n6 s5 6a649e7cb228d780",
            "walker n6 s6 d2db849e1f67a95a",
            "busy n6 s1 0db529d39f5bf1f7",
            "busy n6 s2 3cc27ffef75ab02b",
            "busy n6 s3 074e47c4f4285642",
            "busy n6 s4 e79916d7b347d113",
            "busy n6 s5 f60018c668fb4b41",
            "busy n6 s6 cb1fce339b604b69",
            "walker n8 s1 ca1fc17a255283c7",
            "walker n8 s2 3a17f6bed56d41c5",
            "walker n8 s3 16c9f825b23c6100",
            "walker n8 s4 869344e0e3046d9f",
            "walker n8 s5 5710b43995a8020c",
            "walker n8 s6 ca4024d40cfb6688",
            "busy n8 s1 dfe6619fe0c88862",
            "busy n8 s2 f19db71cdabd00f8",
            "busy n8 s3 1ac9297cf249e5c4",
            "busy n8 s4 fc7eb71f60899b5d",
            "busy n8 s5 41f3a0fa606d9316",
            "busy n8 s6 1422a19aaa95192e",
            "walker n12 s1 7d3a06c281ef561f",
            "walker n12 s2 87eb3243db0d7d84",
            "walker n12 s3 af1745d5a8fe78a5",
            "walker n12 s4 35ff7fa148cd923a",
            "walker n12 s5 ebe9f95f29df5920",
            "walker n12 s6 5a7a97f0abcf9a43",
            "busy n12 s1 da7610f3b6ac8de6",
            "busy n12 s2 208cdf680a9160d1",
            "busy n12 s3 986996993c4613f9",
            "busy n12 s4 0895dbdf62170501",
            "busy n12 s5 364a017daab809f3",
            "busy n12 s6 8e0887e71625cd83",
            "walker n16 s1 f043c3151200ceda",
            "walker n16 s2 3a31fc3f7c795dcc",
            "walker n16 s3 9768879c9410a264",
            "walker n16 s4 c6652f1a0663ae27",
            "walker n16 s5 2db0f2be7c62c96a",
            "walker n16 s6 b8ae5da92b83daba",
            "busy n16 s1 ad9be618532eba30",
            "busy n16 s2 ca83886d9a7e0d37",
            "busy n16 s3 be15a7725bcf83f7",
            "busy n16 s4 e9ad90f84cd7300c",
            "busy n16 s5 78e52c353d290c0b",
            "busy n16 s6 10808fcdf52e8c22",
        };

        [Test, Explicit("Plays 48 fresh seasons at story rules 12 to their ends: the house talk's held digests, run on demand.")]
        public void EverySeasonAtTheHouseTalksMannersPlaysAsRecorded()
        {
            var played = Run(out int errors, out int mannersOn);
            string path = Environment.GetEnvironmentVariable("GAMESIM_MANNERS_DIGESTS_OUT");
            if (!string.IsNullOrEmpty(path)) File.WriteAllLines(path, played);
            foreach (var line in played) TestContext.Out.WriteLine(line);
            Assert.That(errors, Is.Zero, "Every command the walker gave was legal and every season finished:\n"
                + string.Join("\n", played.Where(line => line.Contains(" ERROR "))));
            Assert.That(mannersOn, Is.EqualTo(played.Count), "Every season starts on story rules 12 (EpisodeEngine.StoryAt Manners) and the role moments.");
            Assert.That(Recorded, Has.Length.EqualTo(played.Count), "Every season has its recorded digest.");
            var moved = played.Select(Key).Where(key => !Recorded.Contains(key)).ToList();
            Assert.That(moved, Is.Empty, "A season at story rules 12 moved:\n" + string.Join("\n", moved));
        }

        // ------------------------------------------------------------ the run

        private static readonly string[] Players = { "walker", "busy" };

        private static string Key(string line) => string.Join(" ", line.Split(' ').Take(4));

        /// <summary>
        /// Plays the 48 seasons and returns a line for each: player, size, seed, digest and the season's counts. Public, so a
        /// scratch program compiled against another build can print the same lines.
        /// </summary>
        public static List<string> Run(out int errors, out int mannersOn)
        {
            var lines = new List<string>();
            errors = 0;
            mannersOn = 0;
            foreach (int size in AllWeekSeasonDigests.Sizes)
            foreach (string player in Players)
            for (uint seed = 1; seed <= AllWeekSeasonDigests.Seeds; seed++)
            {
                var start = Season(size, seed);
                if (EpisodeEngine.StoryAt(start, StoryRules.Manners) && EpisodeEngine.RolesOn(start)) mannersOn++;
                Play(start, player, seed, out string digest, out string stats, out string error);
                if (error != null) errors++;
                lines.Add(player + " n" + size + " s" + seed + " " + digest + " " + stats + (error != null ? " ERROR " + error : ""));
            }
            return lines;
        }

        /// <summary>A fresh season as the director starts one, held at story rules 12 and A-ON's other shipped levels by name.</summary>
        private static EpisodeState Season(int size, uint seed)
        {
            var s = AllWeekSeasonDigests.Fresh(size, seed);
            Assert.That(s.story.rulesStartWeek, Is.Not.Zero, "A fresh season plays the story.");
            // Held at story rules 12, the house talk's manners, by name (plan 32 §Q1): ApplyFresh writes Current, which B-ON moves.
            s.story.rulesVersion = StoryRules.Manners;
            // Held at the other levels A-ON's head shipped, by name, so their own later enrolments move none of these seasons.
            s.competitionRulesVersion = CompetitionRules.Trivia;
            s.story.talkRules = TalkRules.Romance;
            s.story.mindRules = MindRules.Off;
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

        /// <summary>Each phase change's mark, as <see cref="RolesSeasonDigests"/> marks one.</summary>
        private static void Mark(EpisodeState s, IncrementalHash hash) =>
            hash.AppendData(Encoding.UTF8.GetBytes(s.week + "/" + s.phase + "/" + s.evictionStage + "/" + s.revision + "/" + s.randomState + "\n"));

        private static void Checkpoint(EpisodeState s, IncrementalHash hash)
        {
            var trace = new StringBuilder();
            Checkpoint(s, trace);
            hash.AppendData(Encoding.UTF8.GetBytes(trace.ToString()));
        }

        /// <summary>
        /// One checkpoint's text: <see cref="RolesSeasonDigests"/>' own, by reflection - the state's JSON through the live
        /// schema's observer (its schema-30 view, which refuses a season with any free talk had), then every reader - so a
        /// schema bump fails here by name until its observer is put in front (HeldReplayShapeTests), as it fails there.
        /// </summary>
        private static void Checkpoint(EpisodeState s, StringBuilder trace) => RolesCheckpoint.Invoke(null, new object[] { s, trace });

        private static readonly MethodInfo RolesCheckpoint =
            typeof(RolesSeasonDigests).GetMethod("Checkpoint", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(EpisodeState), typeof(StringBuilder) }, null)
            ?? throw new InvalidOperationException("RolesSeasonDigests.Checkpoint is the held seasons' checkpoint.");
    }
}
#endif
