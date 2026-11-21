using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The free talk's friendship stage talks (slice S2, talk rules 2; ruling R1: the talks are C# tables). What every stage row must
    /// be beyond <see cref="TalkTablesTests"/>, which reads every row of <see cref="TalkTables.All"/> (ids, 140-character spoken text,
    /// tokens, American spelling, the real-people deny-list) and so reads these too once they are registered: the four stage tables
    /// are in the registry, each pool holds at least three different talks for each of the four stages, a stage row takes only the
    /// stage's gates, the Close friend talk asks the favor with exactly its two replies, no other talk carries a favor, and the star
    /// pool's stage talks hold no romantic word. Simulation only, so the Unity-free subset runs it too.
    /// </summary>
    public sealed class TalkStageTablesTests
    {
        const int VariantsPerStage = 3;
        const string Keep = "I've got you", Refuse = "Can't promise that";

        static readonly FriendStage[] Stages = { FriendStage.Acquainted, FriendStage.Friendly, FriendStage.Friend, FriendStage.CloseFriend };

        static IEnumerable<TalkRow> StageRows => TalkTables.All.Where(r => r.Kind == TalkKind.Stage);

        static IEnumerable<KeyValuePair<string, string>> Lines(TalkRow row)
        {
            yield return new KeyValuePair<string, string>(row.Id + " open", row.Open);
            for (int i = 0; i < row.Replies.Length; i++)
            {
                var r = row.Replies[i];
                yield return new KeyValuePair<string, string>(row.Id + " reply " + i + " line", r.Line);
                yield return new KeyValuePair<string, string>(row.Id + " reply " + i + " lands", r.Lands);
                yield return new KeyValuePair<string, string>(row.Id + " reply " + i + " even", r.Even);
                yield return new KeyValuePair<string, string>(row.Id + " reply " + i + " grates", r.Grates);
            }
        }

        [Test]
        public void TheRegistryHoldsTheFourStageTablesAndEveryStageRowIsInIt()
        {
            var tables = new[] { TalkTableSparkStages.Rows, TalkTableSteadyStages.Rows, TalkTableGuardedStages.Rows, TalkTableStarStages.Rows };
            foreach (var table in tables)
                Assert.That(TalkTables.Tables.Contains(table), Is.True, "A stage table the registry does not join is never offered nor checked.");
            var registered = new HashSet<TalkRow>(TalkTables.All);
            Assert.That(tables.SelectMany(t => t).All(registered.Contains), Is.True);
            Assert.That(tables.SelectMany(t => t).All(r => r.Kind == TalkKind.Stage), Is.True, "The stage tables hold stage talks only.");
            Assert.That(StageRows.Count(), Is.EqualTo(tables.Sum(t => t.Length)), "Every stage talk lives in a stage table.");
        }

        [Test]
        public void EveryPoolHoldsAtLeastThreeDifferentTalksForEachStage()
        {
            var bad = new List<string>();
            foreach (string pool in TalkPools.All)
                foreach (var stage in Stages)
                {
                    var rows = StageRows.Where(r => r.Pool == pool && r.Stage == stage).ToList();
                    if (rows.Count < VariantsPerStage) bad.Add(pool + " " + stage + ": " + rows.Count + " talks");
                    if (rows.Select(r => TalkText.Spoken(r.Open)).Distinct().Count() != rows.Count) bad.Add(pool + " " + stage + ": two talks open with the same words");
                }
            foreach (var row in StageRows.Where(r => !Stages.Contains(r.Stage))) bad.Add(row.Id + ": stage " + row.Stage + " is never offered");
            Assert.That(bad, Is.Empty, "Three housemates of one class reaching the same stage in one season say different words.");
        }

        [Test]
        public void EveryStageRowTakesOnlyTheStagesGates()
        {
            var bad = new List<string>();
            foreach (var row in StageRows)
            {
                var g = row.When;
                if (g == null) { bad.Add(row.Id + ": no gate object"); continue; }
                if (!TalkPools.IsKnown(row.Pool) || row.Owner != null) bad.Add(row.Id + ": pool " + row.Pool + ", owner " + row.Owner);
                if (g.First || g.Layer != 0) bad.Add(row.Id + ": a first or layer gate (the stage itself is the gate)");
                if (g.Week < 0 || g.Week > 20) bad.Add(row.Id + ": week gate " + g.Week);
                if (g.Window != null && !TalkWindows.IsKnown(g.Window)) bad.Add(row.Id + ": unknown window " + g.Window);
                if (row.Layer != 0 || row.Reveals || row.Usual || row.Standing != null) bad.Add(row.Id + ": a stage talk is layer 0, reveals nothing, never repeats");
            }
            Assert.That(bad, Is.Empty);
        }

        [Test]
        public void EveryStageReplyIsFromTheClosedToneSetNeverFlirtAndNeverTwiceInOneTalk()
        {
            var bad = new List<string>();
            foreach (var row in StageRows)
            {
                int want = row.Stage == FriendStage.CloseFriend ? 2 : 3;
                if (row.Replies == null || row.Replies.Length != want) { bad.Add(row.Id + ": " + (row.Replies?.Length ?? 0) + " replies, want " + want); continue; }
                foreach (var r in row.Replies)
                {
                    if (!TalkRules.IsTone(r.Tone) || r.Tone == TalkRules.Flirt) bad.Add(row.Id + ": tone " + r.Tone);
                    if (r.HoldsBack) bad.Add(row.Id + ": a stage talk teaches no fact to hold back");
                }
                if (row.Replies.Select(r => r.Tone).Distinct().Count() != want) bad.Add(row.Id + ": a tone offered twice");
            }
            Assert.That(bad, Is.Empty);
        }

        [Test]
        public void TheCloseFriendTalkAsksTheFavorWithExactlyItsTwoReplies()
        {
            var bad = new List<string>();
            var close = StageRows.Where(r => r.Stage == FriendStage.CloseFriend).ToList();
            Assert.That(close.Count, Is.GreaterThanOrEqualTo(TalkPools.All.Length * VariantsPerStage));
            foreach (var row in close)
            {
                var keep = row.Replies.Where(r => r.Favour == TalkFavour.Keep).ToList();
                var refuse = row.Replies.Where(r => r.Favour == TalkFavour.Refuse).ToList();
                if (row.Replies.Length != 2 || keep.Count != 1 || refuse.Count != 1) { bad.Add(row.Id + ": want one Keep and one Refuse and nothing else"); continue; }
                if (!TalkText.Spoken(keep[0].Line).StartsWith(Keep)) bad.Add(row.Id + ": the Keep reply does not say \"" + Keep + "\"");
                if (!TalkText.Spoken(refuse[0].Line).StartsWith(Refuse)) bad.Add(row.Id + ": the Refuse reply does not say \"" + Refuse + "\"");
            }
            Assert.That(bad, Is.Empty);
        }

        [Test]
        public void NoTalkButTheCloseFriendTalkCarriesAFavor()
        {
            var bad = TalkTables.All.Where(r => !(r.Kind == TalkKind.Stage && r.Stage == FriendStage.CloseFriend))
                .Where(r => r.Replies.Any(x => x.Favour != TalkFavour.None) || r.Kind != TalkKind.Stage && r.Stage != FriendStage.Stranger)
                .Select(r => r.Id).ToList();
            Assert.That(bad, Is.Empty, "A favor is an act (+2 or -2 warmth): only the Close friend talk may hold one; a stage only on a stage talk.");
        }

        [Test]
        public void StageIdsAreUniqueAcrossEveryTableAndNameTheirPoolAndStage()
        {
            var bad = new List<string>();
            foreach (var dup in TalkTables.All.GroupBy(r => r.Id).Where(g => g.Count() > 1)) bad.Add("duplicate id " + dup.Key);
            foreach (var row in StageRows)
                if (!row.Id.StartsWith(row.Pool + ".stage.") || row.Id.Length > 64 || row.Id.Contains(":")) bad.Add(row.Id + ": want <pool>.stage.<stage>.<n>");
            Assert.That(bad, Is.Empty);
        }

        // ------------------------------------------------------------------ real people (rulings R7, R14, R15)

        /// <summary>
        /// Nothing romantic is built in slice 2, and nothing romantic is said by an All-Star: beyond the deny-list every row meets in
        /// <see cref="TalkTablesTests"/>, the words a flirtatious line would need, in any form.
        /// </summary>
        static readonly Regex Romantic = new Regex(
            @"\b(?:flirt\w*|crush\w*|dat(?:e|es|ed|ing)|kiss\w*|cute\w*|handsome|beautiful|pretty|hot|sweetie|love you|in love|lovely|adore\w*|heart\w*)\b",
            RegexOptions.IgnoreCase);

        [TestCase("You're cute when you plan.")] [TestCase("I think I love you.")] [TestCase("Are you flirting?")]
        public void TheRomanticListCatchesAFlirtatiousLine(string line) => Assert.That(Romantic.IsMatch(line), Is.True);

        [TestCase("Read the update before the vote.")] [TestCase("Shot through the house. Executed.")]
        public void TheRomanticListLetsGameWordsThrough(string line) => Assert.That(Romantic.IsMatch(line), Is.False);

        [Test]
        public void TheStarPoolsStageTalksHoldNoRomanticWord()
        {
            var star = StageRows.Where(r => r.Pool == TalkPools.Star).ToList();
            Assert.That(star.Count, Is.GreaterThanOrEqualTo(Stages.Length * VariantsPerStage), "The star pool's stage talks were read.");
            var bad = star.SelectMany(Lines)
                .SelectMany(l => Romantic.Matches(l.Value ?? "").Cast<Match>().Select(m => l.Key + ": " + m.Value)).ToList();
            Assert.That(bad, Is.Empty, "A friendship with an All-Star is a working friendship in the game.");
        }
    }
}
