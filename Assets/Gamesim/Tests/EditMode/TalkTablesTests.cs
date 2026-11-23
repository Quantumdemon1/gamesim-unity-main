using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The free talk's tables (slice 1, ruling R1: the talks are C# tables, no text format and no validator tool): what every row in
    /// <see cref="TalkTables.All"/> must be. Unique ids, spoken text of at most 140 characters (the words inside the quotes), gates,
    /// tokens and tones from the contract's closed sets, the coverage of ruling R9, the star pool's layers (R7), and the real-people
    /// deny-list over every line an All-Star can say and every line that can be said about one. Simulation only, so the Unity-free
    /// subset runs it too.
    /// </summary>
    public sealed class TalkTablesTests
    {
        const int MaxSpoken = 140;

        // Slice 3: the romance kinds (after Stage) have their own row form; TalkRomanceTablesTests reads them.
        static IEnumerable<TalkRow> Rows => TalkTables.All.Where(r => r.Kind <= TalkKind.Stage);

        /// <summary>Every piece of text a row holds, with a name for the failure message.</summary>
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
            foreach (var s in StandingLines(row)) yield return s;
        }

        static IEnumerable<KeyValuePair<string, string>> StandingLines(TalkRow row)
        {
            if (row.Standing == null) yield break;
            yield return new KeyValuePair<string, string>(row.Id + " standing honest", row.Standing.Honest);
            yield return new KeyValuePair<string, string>(row.Id + " standing neutral", row.Standing.Neutral);
            yield return new KeyValuePair<string, string>(row.Id + " standing dishonest", row.Standing.Dishonest);
        }

        static IEnumerable<string> Quoted(string line) =>
            Regex.Matches(line ?? "", "\"([^\"]*)\"").Cast<Match>().Select(m => m.Groups[1].Value);

        static IEnumerable<string> TokensIn(string line) =>
            Regex.Matches(line ?? "", @"\{([^}]*)\}").Cast<Match>().Select(m => m.Groups[1].Value);

        // ------------------------------------------------------------------ the registry

        [Test]
        public void TheRegistryJoinsEveryTableAndEveryPoolHasEachKind()
        {
            Assert.That(TalkTables.Tables.Count, Is.EqualTo(16 + 15), "Four pools, three kinds each, each pool's stage talks (slice 2), and the three pools' and twelve All-Stars' romance talks (slice 3).");
            Assert.That(TalkTables.All.Count, Is.EqualTo(TalkTables.Tables.Sum(t => t.Length)));
            Assert.That(Rows.Count(), Is.EqualTo(276 + 48), "The converter read 276 talks from the source and wrote 276 rows; slice 2 adds 48 stage talks.");
            Assert.That(TalkTables.All.Count, Is.EqualTo(276 + 48 + 3 * 48 + 12 * 19), "Slice 3 adds 48 romance talks per class pool and 19 per All-Star.");
            foreach (string pool in TalkPools.All)
                foreach (TalkKind kind in new[] { TalkKind.Layer, TalkKind.Rhythm, TalkKind.Standing })
                    Assert.That(Rows.Count(r => r.Pool == pool && r.Kind == kind), Is.GreaterThan(0), pool + " " + kind);
        }

        [Test]
        public void EveryIdIsUniqueShortColonFreeAndNamesItsPool()
        {
            var bad = new List<string>();
            foreach (var row in Rows)
            {
                if (string.IsNullOrEmpty(row.Id) || row.Id.Length > 64 || row.Id.Contains(":")) bad.Add("bad id: " + row.Id);
                else if (!row.Id.StartsWith(row.Pool + ".")) bad.Add(row.Id + ": does not start with its pool " + row.Pool);
            }
            foreach (var dup in Rows.GroupBy(r => r.Id).Where(g => g.Count() > 1)) bad.Add("duplicate id " + dup.Key);
            Assert.That(bad, Is.Empty);
        }

        // ------------------------------------------------------------------ text

        [Test]
        public void EverySpokenTextIsAtMost140CharactersAndEveryLineIsWellFormed()
        {
            var bad = new List<string>();
            foreach (var row in Rows)
                foreach (var line in Lines(row))
                {
                    if (string.IsNullOrWhiteSpace(line.Value)) { bad.Add(line.Key + ": empty"); continue; }
                    if (line.Value.Count(c => c == '"') % 2 != 0) bad.Add(line.Key + ": odd number of quotes");
                    if (line.Value.Count(c => c == '(') != line.Value.Count(c => c == ')')) bad.Add(line.Key + ": unbalanced parentheses");
                    if (!Quoted(line.Value).Any() && !Regex.IsMatch(line.Value, @"^\(.*\)$")) bad.Add(line.Key + ": no speech and no stage direction");
                    foreach (string spoken in Quoted(line.Value))
                        if (spoken.Length > MaxSpoken) bad.Add(line.Key + ": spoken text of " + spoken.Length + " characters");
                    if (line.Value.Any(c => c > '~' || (c < ' ' && c != '\t'))) bad.Add(line.Key + ": a character outside printable ASCII");
                }
            Assert.That(bad, Is.Empty);
        }

        [Test]
        public void EveryTokenIsFromTheContractsVocabularyAndStandingTokensOnlyInStandingLines()
        {
            var bad = new List<string>();
            foreach (var row in Rows)
            {
                var standing = new HashSet<string>(StandingLines(row).Select(l => l.Key));
                foreach (var line in Lines(row))
                    foreach (string token in TokensIn(line.Value))
                    {
                        bool plain = TalkText.Tokens.Contains(token);
                        bool stand = TalkText.StandingTokens.Contains(token);
                        if (!plain && !stand) bad.Add(line.Key + ": unknown token {" + token + "}");
                        else if (stand && !standing.Contains(line.Key)) bad.Add(line.Key + ": {" + token + "} outside a standing line");
                    }
            }
            Assert.That(bad, Is.Empty);
        }

        [Test]
        public void EveryStandingFormCarriesTheTokensItsTruthNeeds()
        {
            var bad = new List<string>();
            foreach (var row in Rows.Where(r => r.Kind == TalkKind.Standing))
            {
                var honest = new HashSet<string>(TokensIn(row.Standing.Honest));
                var neutral = new HashSet<string>(TokensIn(row.Standing.Neutral));
                var dishonest = new HashSet<string>(TokensIn(row.Standing.Dishonest));
                if (!honest.IsSupersetOf(new[] { "close", "worry", "safe" })) bad.Add(row.Id + ": the honest form names close, worry and safe");
                if (!neutral.Contains("safe") || neutral.Contains("close") || neutral.Contains("worry")) bad.Add(row.Id + ": the neutral form keeps {safe} and names nobody");
                if (!dishonest.IsSupersetOf(new[] { "close", "worry" })) bad.Add(row.Id + ": the dishonest form carries the decoys");
                if (Regex.IsMatch(row.Standing.Dishonest, @"\(.*\)")) bad.Add(row.Id + ": the dishonest form carries a stage direction that gives it away");
            }
            Assert.That(bad, Is.Empty);
        }

        [Test]
        public void EveryLineIsInAmericanSpelling()
        {
            var british = new Regex(@"\b(colou?rs?\w*|humour|candour|neighbour\w*|favourite\w*|favour\w*|behaviour|honour\w*|rumour\w*|practis\w+|metres?|centres?|theatres?|\w+is(e|ed|ing)|labelled|labelling|travelled|cancelled|grey|maths|learnt|whilst|amongst|mum|mums|defence|licence|offence|tea towel|washing up)\b", RegexOptions.IgnoreCase);
            // The -ise words English spells that way on both sides of the Atlantic.
            var same = new HashSet<string> { "rise", "risen", "rising", "raise", "raised", "raising", "wise", "otherwise", "noise", "promise", "promised",
                "promising", "surprise", "surprised", "surprising", "supervise", "exercise", "exercising", "advise", "advised", "comprise", "compromise",
                "despise", "devise", "disguise", "enterprise", "expertise", "franchise", "improvise", "improvised", "merchandise", "precise", "premise",
                "revise", "televise", "concise", "demise", "excise", "incise", "arise", "arising", "praise", "praised", "poise", "poised", "cruise",
                "bruise", "bruised", "chaise", "anise", "valise", "paradise", "likewise", "clockwise", "lengthwise", "streetwise", "stepwise", "guise",
                "colors", "color", "colored", "coloring", "colorful", "favor", "favors" };
            var bad = new List<string>();
            foreach (var row in Rows)
                foreach (var line in Lines(row))
                    foreach (Match m in british.Matches(line.Value))
                        if (!same.Contains(m.Value.ToLowerInvariant())) bad.Add(line.Key + ": " + m.Value);
            Assert.That(bad, Is.Empty);
        }

        // ------------------------------------------------------------------ gates, kinds and tones

        [Test]
        public void EveryGateIsFromTheSliceOneVocabularyAndFitsItsKind()
        {
            var bad = new List<string>();
            foreach (var row in Rows)
            {
                var g = row.When;
                if (g == null) { bad.Add(row.Id + ": no gate object"); continue; }
                if (!TalkPools.IsKnown(row.Pool) || row.Owner != null) bad.Add(row.Id + ": a pool talk with pool " + row.Pool + " and owner " + row.Owner);
                if (g.Window != null && !TalkWindows.IsKnown(g.Window)) bad.Add(row.Id + ": unknown window " + g.Window);
                if (g.Layer < 0 || g.Layer > 4) bad.Add(row.Id + ": layer gate " + g.Layer);
                if (g.Week < 0 || g.Week > 20) bad.Add(row.Id + ": week gate " + g.Week);
                if (g.First && (g.Layer != 0 || g.Week != 0 || g.Window != null)) bad.Add(row.Id + ": a first talk with other gates");
                if (!g.First && g.Layer == 0 && row.Kind != TalkKind.Stage) bad.Add(row.Id + ": neither first nor a layer gate");
                if (row.Priority < 0 || row.Priority > 100) bad.Add(row.Id + ": priority " + row.Priority);
                switch (row.Kind)
                {
                    case TalkKind.Layer:
                        int want = g.First ? 1 : g.Layer;
                        if (row.Layer < 1 || row.Layer > 3 || row.Layer != want) bad.Add(row.Id + ": layer frame on layer " + row.Layer + " gated " + want);
                        if (!row.Reveals || row.Usual || row.Standing != null) bad.Add(row.Id + ": a layer frame reveals, never repeats and has no standing lines");
                        break;
                    case TalkKind.Standing:
                        if (row.Layer != 4 || g.Layer != 4 || g.First) bad.Add(row.Id + ": a standing talk is on layer 4");
                        if (row.Reveals || row.Usual || row.Standing == null) bad.Add(row.Id + ": a standing talk has its three forms and reveals nothing");
                        break;
                    case TalkKind.Rhythm:
                        if (row.Layer != 0 || g.First || row.Reveals || row.Standing != null) bad.Add(row.Id + ": a rhythm talk is layer 0 and reveals nothing");
                        if (g.Layer > 1) bad.Add(row.Id + ": a rhythm talk gated past layer 1");
                        break;
                    case TalkKind.Stage:
                        // Slice 2: the stage itself is the gate (TalkStageTablesTests checks the rest).
                        if (row.Layer != 0 || g.First || g.Layer != 0 || row.Reveals || row.Usual || row.Standing != null) bad.Add(row.Id + ": a stage talk is layer 0 and gated by its stage");
                        break;
                    default:
                        bad.Add(row.Id + ": kind " + row.Kind + " is not in slice 1");
                        break;
                }
            }
            Assert.That(bad, Is.Empty);
        }

        [Test]
        public void EveryTalkOffersThreeDistinctTonesFromTheClosedSetAndNeverFlirt()
        {
            var bad = new List<string>();
            foreach (var row in Rows)
            {
                // Slice 2: the Close friend stage talk offers its favor's two replies.
                int want = row.Kind == TalkKind.Stage && row.Stage == FriendStage.CloseFriend ? 2 : 3;
                if (row.Replies == null || row.Replies.Length != want) { bad.Add(row.Id + ": " + (row.Replies?.Length ?? 0) + " replies"); continue; }
                foreach (var r in row.Replies)
                {
                    if (!TalkRules.IsTone(r.Tone) || r.Tone == TalkRules.Flirt) bad.Add(row.Id + ": tone " + r.Tone);
                    if (TalkTone.Approach(r.Tone) == null || TalkTone.Caption(r.Tone) == "") bad.Add(row.Id + ": tone " + r.Tone + " has no approach or caption");
                    if (r.HoldsBack) bad.Add(row.Id + ": no source talk holds a fact back by its reply");
                }
                if (row.Replies.Select(r => r.Tone).Distinct().Count() != want) bad.Add(row.Id + ": a tone offered twice");
            }
            Assert.That(bad, Is.Empty);
        }

        // ------------------------------------------------------------------ coverage (ruling R9)

        [Test]
        public void EveryPoolCoversTheSeasonAndNeverRunsDry()
        {
            foreach (string pool in new[] { TalkPools.Spark, TalkPools.Steady, TalkPools.Guarded })
            {
                var rows = Rows.Where(r => r.Pool == pool).ToList();
                Assert.That(rows.Count(r => !r.Usual), Is.GreaterThanOrEqualTo(45),
                    pool + ": a regular housemate's talks fill 45 counted windows before any repeats (R9).");
                for (int layer = 1; layer <= 3; layer++)
                    Assert.That(rows.Count(r => r.Kind == TalkKind.Layer && r.Layer == layer), Is.GreaterThanOrEqualTo(6), pool + " layer " + layer);
                Assert.That(rows.Count(r => r.Kind == TalkKind.Layer && r.When.First), Is.GreaterThanOrEqualTo(1), pool + ": a first talk");
            }
            var star = Rows.Where(r => r.Pool == TalkPools.Star).ToList();
            Assert.That(star.Count, Is.GreaterThanOrEqualTo(60), "The star pool holds at least 60 talks (R9).");
            Assert.That(star.Count(r => r.Kind == TalkKind.Layer && r.When.First), Is.GreaterThanOrEqualTo(1), "star: a first talk");
            foreach (string pool in TalkPools.All)
            {
                var usual = Rows.Where(r => r.Pool == pool && r.Usual).ToList();
                Assert.That(usual.Count, Is.GreaterThanOrEqualTo(3), pool + ": the usual talks that may repeat (R9)");
                Assert.That(usual.All(r => r.Kind == TalkKind.Rhythm && r.When.Window == null && r.When.Week == 0 && r.When.Layer <= 1), Is.True,
                    pool + ": a usual talk can be said in any window once layer 1 is open, so the bank never runs dry");
                foreach (string window in new[] { TalkWindows.AfterHoH, TalkWindows.AfterNoms, TalkWindows.AfterVeto, TalkWindows.AfterEviction })
                    Assert.That(Rows.Count(r => r.Pool == pool && r.When.Window == window), Is.GreaterThanOrEqualTo(3), pool + " " + window);
            }
        }

        // ------------------------------------------------------------------ real people (rulings R7, R14, R15)

        [Test]
        public void TheStarPoolHoldsLayersOneAndTwoAndStandingOnly()
        {
            var bad = Rows.Where(r => r.Pool == TalkPools.Star)
                .Where(r => !(r.Kind == TalkKind.Rhythm && r.Layer == 0
                              || r.Kind == TalkKind.Layer && (r.Layer == 1 || r.Layer == 2) && r.When.Layer <= 2
                              || r.Kind == TalkKind.Standing && r.Layer == 4
                              || r.Kind == TalkKind.Stage && r.Layer == 0))
                .Select(r => r.Id + " " + r.Kind + " " + r.Layer).ToList();
            Assert.That(bad, Is.Empty, "An All-Star's talks cover layers 1, 2 and 4 only (R7): no layer 3, no private background.");
        }

        /// <summary>
        /// The words a line would need to state or imply a fact about a real person's private life (partners, family, religion,
        /// politics, illness), and the romantic words slice 1 builds nothing with. Whole words, any case; "ex-" as a prefix.
        /// </summary>
        static readonly string[] Deny =
        {
            // partners and family
            "wife", "wives", "husband", "husbands", "married", "marry", "marriage", "wedding", "engaged", "engagement",
            "girlfriend", "boyfriend", "fiance", "fiancee", "spouse", "partner", "kid", "kids", "children", "child", "son", "sons",
            "daughter", "daughters", "baby", "babies", "pregnant", "pregnancy", "divorce", "divorced", "ex", "exes", "family",
            "mom", "dad", "mother", "father", "parents", "dating",
            // religion
            "religion", "religious", "church", "god", "pray", "prayer", "faith", "bible", "jesus", "mosque", "synagogue", "temple",
            // politics
            "politics", "political", "democrat", "democrats", "republican", "republicans", "election", "president", "liberal",
            "conservative", "senator", "congress",
            // illness
            "cancer", "illness", "ill", "sick", "sickness", "disease", "hospital", "diagnosis", "diagnosed", "therapy", "therapist",
            "surgery", "chemo", "rehab", "sober", "addiction", "addict", "depression", "medication",
            // romance (nothing romantic is built in slice 1)
            "kiss", "kissed", "kissing", "date", "dates", "romance", "romantic", "crush", "flirt", "flirting", "flirty", "love", "loved",
            "lover", "showmance", "couple", "sexy", "sex", "darling", "sweetheart", "babe", "honey", "gorgeous", "handsome", "beautiful",
            "single",
        };

        static readonly Regex DenyWords = new Regex(@"\b(?:" + string.Join("|", Deny.Select(Regex.Escape)) + @")\b|\bex-", RegexOptions.IgnoreCase);

        [TestCase("I miss my wife.")] [TestCase("My Ex-partner")] [TestCase("Church on Sunday")] [TestCase("a little sick")] [TestCase("Let's date.")]
        public void TheDenyListCatchesAPrivateFact(string line) => Assert.That(DenyWords.IsMatch(line), Is.True);

        [TestCase("I'll be still.")] [TestCase("Exactly. Sunday.")] [TestCase("the update")]
        public void TheDenyListLetsGameWordsThrough(string line) => Assert.That(DenyWords.IsMatch(line), Is.False);

        [Test]
        public void NoLineAnAllStarSaysOrThatCanNameOneHoldsAPrivateOrRomanticWord()
        {
            var person = new Regex(@"\{(close|worry|hoh|nominees|name)\}");
            var read = new List<KeyValuePair<string, string>>();
            foreach (var row in Rows)
            {
                if (row.Pool == TalkPools.Star) read.AddRange(Lines(row));
                else read.AddRange(Lines(row).Where(l => person.IsMatch(l.Value)));
            }
            Assert.That(read.Count, Is.GreaterThan(66 * 13), "Every star-pool line and every class-pool line that can name a housemate was read.");
            var bad = read.SelectMany(l => DenyWords.Matches(l.Value).Cast<Match>().Select(m => l.Key + ": " + m.Value)).ToList();
            Assert.That(bad, Is.Empty);
        }
    }
}
