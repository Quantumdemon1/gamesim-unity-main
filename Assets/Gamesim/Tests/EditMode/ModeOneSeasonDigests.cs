#if !UNITY_5_3_OR_NEWER
// Mode 1's recorded-season evidence, reproducible on demand: Tools/SimulationTests only. Unity's batch
// runner executes [Explicit] tests, so the file is compiled out of the editor's assemblies.
using System;
using System.Collections.Generic;
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
    /// Mode 1's recorded seasons (vote family V5a, the lead's decision D6): a season saved under the public
    /// unified Safety authority (<c>unifiedCommitmentRulesVersion</c> 1, hearings 1) plays mode 1 for as long
    /// as it is played, and vote family V5 moves every commitment reader beside it. So the reviewers' scratch
    /// check of the vote family's mode 1 is committed here: the director's and the strategy harness's rule sets
    /// of <see cref="CommitmentRulesSeasonDigests"/>, houses of 6, 8 and 12, seeds 1 to 6 - 36 seasons - under
    /// the commitment rules from week one and in mode 1, played to their ends by that harness's busy scripted
    /// player (reached by reflection: the same houses and the same moves).
    ///
    /// <para>Each season is digested at every phase change: the whole state, as it serializes, and every reader
    /// vote family V5 moves that a test can call - the views, the house's warmth, threat and trust, the jury's
    /// obligations, a Head of Household's reluctance, the deal and move odds as the player sees them and as the
    /// roll reads them, the story's odds, the houseguest notes, the commitments page, the player's week and word,
    /// Game Sense, the finalist and jury reads, the final case and the canonical Safety history. A reader that
    /// refuses the state is digested by its refusal. <see cref="Recorded"/> holds the digests the build before V5
    /// (a74a261a) made; every one is byte-identical after each slice, since V5 changes mode 2 only.</para>
    ///
    /// <para>Explicit, one test a rule set, each a few minutes: run with
    /// <c>dotnet test Tools/SimulationTests --filter "FullyQualifiedName~ModeOneSeasonDigests"</c>.</para>
    /// </summary>
    public sealed class ModeOneSeasonDigests
    {
        /// <summary>Each season as the build before V5 played it in mode 1: rule set, house size, seed, digest.</summary>
        private static readonly string[] Recorded =
        {
            "director n6 s1 3275c764b61fea17",
            "director n6 s2 c3a2c4be264c16b3",
            "director n6 s3 db4e26a910a8d4be",
            "director n6 s4 a4834cb1b58d8933",
            "director n6 s5 1e0a72ebeb263c39",
            "director n6 s6 d497468a52cb3cd4",
            "director n8 s1 1ea217f890f0955d",
            "director n8 s2 7ba029e4d9302319",
            "director n8 s3 786d3b25886daf5d",
            "director n8 s4 0dfcbc2b615fabf8",
            "director n8 s5 27e459bde8ec80cb",
            "director n8 s6 50bf9e181aabf0b1",
            "director n12 s1 f0548297f8a0e87b",
            "director n12 s2 46439dcb9cdc1133",
            "director n12 s3 d28033c430acb127",
            "director n12 s4 32e52523b0b1ae9a",
            "director n12 s5 262935668a69952f",
            "director n12 s6 b42bb06911a11ccd",
            "harness n6 s1 d3fbf60290224379",
            "harness n6 s2 5d54783c2ec28286",
            "harness n6 s3 254b8754682cfa08",
            "harness n6 s4 2ae68ac23ed6c21a",
            "harness n6 s5 464e1dd99f96a9a0",
            "harness n6 s6 dba99ee548caebc9",
            "harness n8 s1 95ebfffbf89b4a7d",
            "harness n8 s2 a77ea6600b582b96",
            "harness n8 s3 3166f19112ea0aff",
            "harness n8 s4 b2a049f4611daba7",
            "harness n8 s5 0c62cfaa8fa7506e",
            "harness n8 s6 d4ee73c53b09f804",
            "harness n12 s1 d5467301dca12bff",
            "harness n12 s2 f02abfb9567fa84a",
            "harness n12 s3 7034f3d56f8578b3",
            "harness n12 s4 0b84f3863a6e8921",
            "harness n12 s5 610755dc15fcfcec",
            "harness n12 s6 9c3c0c250e870d54",
        };

        [Test, Explicit("Plays the director's 18 seasons in mode 1 to their ends: every digest as it was before V5.")]
        public void TheDirectorsSeasonsInModeOnePlayAsTheyDidBeforeTheReadersMoved() => Check("director");

        [Test, Explicit("Plays the strategy harness's 18 seasons in mode 1 to their ends: every digest as it was before V5.")]
        public void TheHarnesssSeasonsInModeOnePlayAsTheyDidBeforeTheReadersMoved() => Check("harness");

        private static void Check(string config)
        {
            var played = Run(config, out int errors);
            TestContext.Out.WriteLine(string.Join("\n", played));
            Assert.That(errors, Is.Zero, string.Join("\n", played.Where(line => line.Contains(" ERROR "))));
            var recorded = Recorded.Where(line => line.StartsWith(config + " ", StringComparison.Ordinal)).ToList();
            Assert.That(recorded, Has.Count.EqualTo(played.Count), "Every season has its recorded digest.");
            var moved = played.Select(line => string.Join(" ", line.Split(' ').Take(4))).Where(key => !recorded.Contains(key)).ToList();
            Assert.That(moved, Is.Empty, "A mode-1 season moved:\n" + string.Join("\n", moved));
        }

        /// <summary>The 18 seasons of one rule set: a line each - rule set, size, seed, digest and the season's counts.</summary>
        public static List<string> Run(string config, out int errors)
        {
            var lines = new List<string>();
            errors = 0;
            foreach (int size in new[] { 6, 8, 12 })
            for (uint seed = 1; seed <= 6; seed++)
            {
                Play(config, size, seed, out string digest, out string stats, out string error);
                if (error != null) errors++;
                lines.Add(config + " n" + size + " s" + seed + " " + digest + " " + stats + (error != null ? " ERROR " + error : ""));
            }
            return lines;
        }

        private static readonly MethodInfo SeasonOf = Private("Season", typeof(string), typeof(int), typeof(uint), typeof(bool));
        private static readonly MethodInfo BusyOf = Private("Busy", typeof(EpisodeState), typeof(uint), typeof(int), typeof(bool));
        private static readonly MethodInfo NextOf = Private("NextCommand", typeof(EpisodeState));

        /// <summary>The digests' own seasons and scripted player, by reflection: the same houses and the same moves.</summary>
        private static MethodInfo Private(string name, params Type[] parameters) =>
            typeof(CommitmentRulesSeasonDigests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static, null, parameters, null)
            ?? throw new InvalidOperationException("CommitmentRulesSeasonDigests." + name + " is the harness's player.");

        /// <summary>The rule set's season under the commitment rules from week one, in mode 1 with its hearings.</summary>
        private static EpisodeState Season(string config, int size, uint seed)
        {
            var s = (EpisodeState)SeasonOf.Invoke(null, new object[] { config, size, seed, true });
            s.unifiedCommitmentRulesVersion = UnifiedCommitments.ProspectiveVersion;
            s.unifiedHearingRulesVersion = UnifiedCommitmentHearings.ProspectiveVersion;
            Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);
            return s;
        }

        private static void Play(string config, int size, uint seed, out string digest, out string stats, out string error)
        {
            error = null;
            var engine = new EpisodeEngine(Season(config, size, seed));
            var trace = new StringBuilder();
            var lastPhase = (EpisodePhase)(-1);
            int i = 0;
            for (; i < 3000 && engine.Snapshot.phase != EpisodePhase.Finished; i++)
            {
                var s = engine.Snapshot;
                if (s.phase != lastPhase) { Checkpoint(s, trace); lastPhase = s.phase; }
                bool done = false;
                for (int attempt = 0; attempt < 6 && !done; attempt++)
                {
                    var own = (EpisodeCommand)BusyOf.Invoke(null, new object[] { s, seed, attempt, true });
                    if (own == null) break;
                    done = engine.Apply(own).accepted;
                }
                if (done) continue;
                var next = (EpisodeCommand)NextOf.Invoke(null, new object[] { s });
                var applied = engine.Apply(next);
                if (!applied.accepted) { error = "week " + s.week + " " + s.phase + "/" + s.evictionStage + " " + next.kind + ": " + applied.reason; break; }
            }
            var final = engine.Snapshot;
            Checkpoint(final, trace);
            if (error == null && final.phase != EpisodePhase.Finished) error = "unfinished after " + i;
            stats = "cmds=" + i + " week=" + final.week + " deals=" + final.deals.Count + " canonical=" + final.unifiedCommitments.Count
                + " broken=" + final.deals.Count(d => d.status == DealStatus.Broken) + " winner=" + final.winnerId;
            digest = Hash(trace.ToString());
        }

        /// <summary>The whole state, and every reader vote family V5 moves that a test can call.</summary>
        private static void Checkpoint(EpisodeState s, StringBuilder trace)
        {
            trace.Append(JsonConvert.SerializeObject(s)).Append('\n');
            var people = s.contestants.ToList();
            foreach (var a in people)
            {
                trace.Append(a.id).Append(':').Append(Read(() => NpcDeals.BrokenDeals(s, a.id))).Append(';');
                foreach (var b in people)
                {
                    if (a.id == b.id) continue;
                    trace.Append(Read(() => F(NpcDeals.Adjusted(s, a.id, b.id)))).Append(',')
                        .Append(Read(() => F(ThreatAssessment.Total(s, a.id, b.id)))).Append(',')
                        .Append(Read(() => F(ThreatAssessment.TrustScore(s, a.id, b.id)))).Append(',')
                        .Append(Read(() => F(WebJuryVoting.Obligations(s, a.id, b.id)))).Append(',')
                        .Append(Read(() => F(WebJuryVoting.Score(s, a.id, b.id)))).Append(',')
                        .Append(Read(() => F(StrategyRules.NominationReluctance(s, a.id, b.id)))).Append(';');
                }
            }
            trace.Append('\n');
            foreach (var npc in s.Active.Where(c => !c.isPlayer))
            {
                string about = s.Active.Where(c => !c.isPlayer && c.id != npc.id).Select(c => c.id).FirstOrDefault();
                foreach (var kind in DealKind.All)
                {
                    string aboutId = kind == DealKind.VoteSave || kind == DealKind.VoteEvict ? s.nominees.FirstOrDefault(id => id != npc.id) ?? about : about;
                    trace.Append(Read(() => F(PlayerDeals.AcceptanceChance(s, npc.id, kind, aboutId)))).Append(',')
                        .Append(Read(() => PlayerDeals.Reasoning(s, npc.id, kind, false))).Append(',')
                        .Append(Read(() => { var known = KnownOdds.Deal(s, npc.id, kind, aboutId); return F(known.chance) + known.word; })).Append(';');
                }
                foreach (var move in new[] { Negotiation.Remind, Negotiation.Demand, Negotiation.Threaten, Negotiation.MendFences, Negotiation.VetoForAPrice })
                    trace.Append(Read(() => F(Negotiation.Chance(s, npc.id, move, true)) + "/" + F(Negotiation.Chance(s, npc.id, move, false)))).Append(';');
                trace.Append(Read(() => StoryOdds.PlayerBrokeTheirWord(s, npc.id))).Append('|')
                    .Append(Read(() => KnownOdds.History(s, npc.id))).Append('|')
                    .Append(Read(() => FinalistRead.Agreement(s, npc.id))).Append('|')
                    .Append(Read(() => FinalistRead.StandingWord(s, npc.id))).Append('|')
                    .Append(Read(() => JsonConvert.SerializeObject(HouseguestNotes.For(s, npc.id)))).Append('|');
            }
            trace.Append('\n');
            trace.Append(Read(() => JsonConvert.SerializeObject(NpcDeals.Pending(s)))).Append('\n')
                .Append(Read(() => JsonConvert.SerializeObject(CommitmentReferences.Promises(s)))).Append('\n')
                .Append(Read(() => JsonConvert.SerializeObject(CommitmentReferences.Deals(s)))).Append('\n')
                .Append(Read(() => JsonConvert.SerializeObject(UnifiedCommitmentHistory.Breaches(s)))).Append('\n')
                .Append(Read(() => JsonConvert.SerializeObject(UnifiedCommitmentHistory.Fulfillments(s)))).Append('\n')
                .Append(Read(() => JsonConvert.SerializeObject(CommitmentsRead.Of(s)))).Append('\n')
                .Append(Read(() => JsonConvert.SerializeObject(YourWeek.Build(s, s.week)))).Append('\n')
                .Append(Read(() => F(YourWord.Cost(s)) + YourWord.Word(s))).Append('\n')
                .Append(Read(() => string.Join("|", FinalistRead.BrokenEitherWay(s)))).Append('\n')
                .Append(Read(() => JsonConvert.SerializeObject(FinalCaseResume.Read(s)))).Append('\n')
                .Append(Read(() => JsonConvert.SerializeObject(AllianceRead.Read(s)))).Append('\n')
                .Append(Read(() => JsonConvert.SerializeObject(WaitingOnYou.Read(s)))).Append('\n');
            var sense = GameSense.Evaluate(s);
            trace.Append(sense.score).Append('/').Append(sense.competitions).Append('/').Append(sense.strategy).Append('/').Append(sense.social);
            foreach (var note in sense.notes) trace.Append('|').Append(note.text).Append(F(note.points));
            trace.Append('\n');
            var house = JuryHouseRead.Read(s);
            foreach (var juror in house.jurors) trace.Append(juror.id).Append(':').Append(juror.band).Append(':').Append(juror.reason).Append('|');
            trace.Append('\n');
        }

        /// <summary>A reader's answer, or the refusal it gives the state: either is the reader's own, and digested.</summary>
        private static string Read(Func<object> reader)
        {
            try { return Convert.ToString(reader(), System.Globalization.CultureInfo.InvariantCulture); }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException || e is NullReferenceException
                || e is IndexOutOfRangeException || e is KeyNotFoundException)
            { return "!" + e.GetType().Name + ":" + e.Message; }
        }

        private static string F(double value) => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

        private static string Hash(string text)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").Substring(0, 16).ToLowerInvariant();
        }
    }
}
#endif
