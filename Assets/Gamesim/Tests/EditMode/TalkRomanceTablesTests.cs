using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The free talk's romance tables (slice S3, talk rules 3; ruling R1: the talks are C# tables): the three class pools' romance
    /// talks and each All-Stars template's own table, and the running-joke callbacks held beside them. What every romance row must
    /// be: unique ids across every table, spoken text of at most 140 characters, the row form of its kind, gates, tones and
    /// choices from the contract's vocabulary, three variants of each kind per pool, the twelve All-Star tables holding the same
    /// kinds as one another, and the real-people deny-list over every line an All-Star can say and every line that can be said
    /// about one. Simulation only, so the Unity-free subset runs it too. <see cref="TalkTablesTests"/> reads the slice 1 and 2
    /// kinds; this fixture reads the rest.
    /// </summary>
    public sealed class TalkRomanceTablesTests
    {
        const int MaxSpoken = 140;
        const int Variants = 3;

        static IEnumerable<TalkRow> Romance => TalkTables.All.Where(r => r.Kind > TalkKind.Stage);

        static readonly TalkRow[][] PoolTables = { TalkTableSparkRomance.Rows, TalkTableSteadyRomance.Rows, TalkTableGuardedRomance.Rows };
        static readonly string[] Pools = { TalkPools.Spark, TalkPools.Steady, TalkPools.Guarded };

        static IEnumerable<string> AllStarIds => CastTemplates.In(CastTemplates.Roster.AllStars).Select(t => t.Id);

        /// <summary>Every piece of text a romance row holds, named for the failure message.</summary>
        static IEnumerable<KeyValuePair<string, string>> Lines(TalkRow row)
        {
            if (row.Open != null) yield return new KeyValuePair<string, string>(row.Id + " open", row.Open);
            if (row.Second != null) yield return new KeyValuePair<string, string>(row.Id + " second", row.Second);
            for (int i = 0; i < row.Replies.Length; i++)
            {
                var r = row.Replies[i];
                yield return new KeyValuePair<string, string>(row.Id + " reply " + i + " line", r.Line);
                yield return new KeyValuePair<string, string>(row.Id + " reply " + i + " lands", r.Lands);
                if (r.Choice != TalkChoice.None) continue;
                yield return new KeyValuePair<string, string>(row.Id + " reply " + i + " even", r.Even);
                yield return new KeyValuePair<string, string>(row.Id + " reply " + i + " grates", r.Grates);
            }
        }

        static IEnumerable<string> Quoted(string line) =>
            Regex.Matches(line ?? "", "\"([^\"]*)\"").Cast<Match>().Select(m => m.Groups[1].Value);

        static bool Tone(TalkReply r) => r.Choice == TalkChoice.None;

        // ------------------------------------------------------------------ the registry and ids

        [Test]
        public void TheRegistryJoinsTheRomanceTablesAndEveryAllStarTemplateHasItsOwn()
        {
            foreach (var table in PoolTables)
                Assert.That(TalkTables.Tables.Contains(table), Is.True, "A romance table the registry does not join is never offered nor checked.");
            var stars = AllStarIds.ToList();
            Assert.That(stars.Count, Is.EqualTo(12));
            Assert.That(TalkTables.StarTables.Keys.OrderBy(k => k), Is.EqualTo(stars.OrderBy(k => k)), "Every All-Stars template has its own table, and only they do.");
            foreach (var pair in TalkTables.StarTables)
            {
                Assert.That(TalkTables.Tables.Contains(pair.Value), Is.True, pair.Key);
                Assert.That(pair.Value.All(r => r.Owner == pair.Key && r.Pool == null && r.Kind > TalkKind.Stage), Is.True, pair.Key + ": its own romance rows only");
            }
            for (int i = 0; i < Pools.Length; i++)
                Assert.That(PoolTables[i].All(r => r.Pool == Pools[i] && r.Owner == null && r.Kind > TalkKind.Stage), Is.True, Pools[i] + ": its own romance rows only");
            int listed = PoolTables.Sum(t => t.Length) + TalkTables.StarTables.Values.Sum(t => t.Length);
            Assert.That(Romance.Count(), Is.EqualTo(listed), "Every romance row lives in a romance table.");
        }

        [Test]
        public void EveryIdIsUniqueAcrossEveryTableShortColonFreeAndNamesItsPoolOrPerson()
        {
            var bad = new List<string>();
            var every = TalkTables.All.Concat(TalkStarCallbacks.Rows).ToList();
            foreach (var dup in every.GroupBy(r => r.Id).Where(g => g.Count() > 1)) bad.Add("duplicate id " + dup.Key);
            var prefixes = new Dictionary<string, string>();
            foreach (var row in Romance.Concat(TalkStarCallbacks.Rows))
            {
                if (string.IsNullOrEmpty(row.Id) || row.Id.Length > 64 || row.Id.Contains(":")) { bad.Add("bad id: " + row.Id); continue; }
                string head = row.Id.Substring(0, row.Id.IndexOf('.') < 0 ? row.Id.Length : row.Id.IndexOf('.'));
                if (!row.Id.StartsWith(head + ".rom.")) bad.Add(row.Id + ": want <pool or person>.rom.<kind>.<n>");
                if (row.Pool != null && head != row.Pool) bad.Add(row.Id + ": does not start with its pool " + row.Pool);
                if (row.Owner != null)
                {
                    if (prefixes.TryGetValue(head, out var owner) && owner != row.Owner) bad.Add(row.Id + ": prefix shared by " + owner + " and " + row.Owner);
                    prefixes[head] = row.Owner;
                    if (TalkPools.IsKnown(head)) bad.Add(row.Id + ": a person's talk under a pool's prefix");
                }
            }
            Assert.That(bad, Is.Empty);
        }

        // ------------------------------------------------------------------ text

        [Test]
        public void EveryLineIsWellFormedAtMost140SpokenCharactersAsciiAndNoToken()
        {
            var bad = new List<string>();
            foreach (var row in Romance.Concat(TalkStarCallbacks.Rows))
                foreach (var line in Lines(row))
                {
                    if (string.IsNullOrWhiteSpace(line.Value)) { bad.Add(line.Key + ": empty"); continue; }
                    if (line.Value.Count(c => c == '"') % 2 != 0) bad.Add(line.Key + ": odd number of quotes");
                    if (line.Value.Count(c => c == '(') != line.Value.Count(c => c == ')')) bad.Add(line.Key + ": unbalanced parentheses");
                    if (!Quoted(line.Value).Any()) bad.Add(line.Key + ": no spoken words");
                    foreach (string spoken in Quoted(line.Value))
                        if (spoken.Length > MaxSpoken) bad.Add(line.Key + ": spoken text of " + spoken.Length + " characters");
                    if (line.Value.Any(c => c > '~' || c < ' ')) bad.Add(line.Key + ": a character outside printable ASCII");
                    if (line.Value.Contains("{") || line.Value.Contains("}")) bad.Add(line.Key + ": a token (a romance line names nobody)");
                    if (Regex.IsMatch(line.Value, @"\d")) bad.Add(line.Key + ": a number read out");
                }
            Assert.That(bad, Is.Empty);
        }

        [Test]
        public void EveryLineIsInAmericanSpelling()
        {
            var british = new Regex(@"\b(colou?rs?\w*|humour|candour|neighbour\w*|favourite\w*|favour\w*|behaviour|honour\w*|rumour\w*|practis\w+|apologis\w*|realis\w*|metres?|centres?|theatres?|labelled|travelled|cancelled|grey|maths|learnt|whilst|amongst|mum|mums|defence|offence)\b", RegexOptions.IgnoreCase);
            var bad = Romance.Concat(TalkStarCallbacks.Rows).SelectMany(Lines)
                .SelectMany(l => british.Matches(l.Value ?? "").Cast<Match>().Select(m => l.Key + ": " + m.Value)).ToList();
            Assert.That(bad, Is.Empty);
        }

        static readonly Regex Gendered = new Regex(@"\b(he|she|him|her|his|hers|man|dude|girl|boy|guy|lady|bro)\b", RegexOptions.IgnoreCase);

        [Test]
        public void NoSpokenWordAssumesThePlayersGender()
        {
            var bad = Romance.Concat(TalkStarCallbacks.Rows).SelectMany(Lines)
                .SelectMany(l => Quoted(l.Value).SelectMany(q => Gendered.Matches(q).Cast<Match>()).Select(m => l.Key + ": " + m.Value)).ToList();
            Assert.That(bad, Is.Empty, "The player character may be anyone: no spoken romance line reads a gender.");
        }

        // ------------------------------------------------------------------ gates, kinds, tones and choices

        [Test]
        public void EveryGateIsSparkWeekOrWindowOnly()
        {
            var bad = new List<string>();
            foreach (var row in Romance)
            {
                var g = row.When;
                if (g == null) { bad.Add(row.Id + ": no gate object"); continue; }
                if (g.First || g.Layer != 0) bad.Add(row.Id + ": a first or layer gate is never read on a romance row");
                if (g.Spark < 0 || g.Spark >= TalkRomance.ReadySpark) bad.Add(row.Id + ": spark gate " + g.Spark);
                if (g.Week < 0 || g.Week > 20) bad.Add(row.Id + ": week gate " + g.Week);
                if (g.Window != null && !TalkWindows.IsKnown(g.Window)) bad.Add(row.Id + ": unknown window " + g.Window);
                if (row.Layer != 0 || row.Reveals || row.Usual || row.Standing != null || row.Stage != FriendStage.Stranger) bad.Add(row.Id + ": slice 1 and 2 fields on a romance row");
                if (row.Priority != 0) bad.Add(row.Id + ": a romance row takes its kind's priority");
                if (g.Spark != 0 && row.Kind != TalkKind.FlirtLine && row.Kind != TalkKind.Opening) bad.Add(row.Id + ": a spark gate on a scene");
            }
            Assert.That(bad, Is.Empty);
        }

        static readonly Dictionary<TalkKind, TalkChoice[]> ChoicesOf = new Dictionary<TalkKind, TalkChoice[]>
        {
            { TalkKind.FlirtLine, new TalkChoice[0] },
            { TalkKind.Opening, new[] { TalkChoice.JustFriends } },
            { TalkKind.SoftNo, new TalkChoice[0] }, { TalkKind.HoldBack, new TalkChoice[0] }, { TalkKind.LineDrawn, new TalkChoice[0] },
            { TalkKind.TurnAside, new[] { TalkChoice.LaughItOff, TalkChoice.PushAgain, TalkChoice.Apologize } },
            { TalkKind.Declaration, new[] { TalkChoice.Accept, TalkChoice.NotYet, TalkChoice.NotLikeThat, TalkChoice.JustFriends } },
            { TalkKind.Accepts, new TalkChoice[0] }, { TalkKind.Refuses, new TalkChoice[0] },
            { TalkKind.Couple, new[] { TalkChoice.EndItKindly } },
            { TalkKind.Ex, new TalkChoice[0] },
            { TalkKind.Jealousy, new[] { TalkChoice.EndItKindly } },
            { TalkKind.Reaction, new TalkChoice[0] },
        };

        [Test]
        public void EveryRowHasTheFormOfItsKind()
        {
            var bad = new List<string>();
            foreach (var row in Romance)
            {
                if (!ChoicesOf.ContainsKey(row.Kind)) { bad.Add(row.Id + ": kind " + row.Kind + " is not in the row form"); continue; }
                var tones = row.Replies.Where(Tone).ToList();
                var choices = row.Replies.Where(r => !Tone(r)).Select(r => r.Choice).ToList();
                foreach (var r in tones)
                    if (!TalkRules.IsTone(r.Tone) || TalkTone.Approach(r.Tone) == null) bad.Add(row.Id + ": tone " + r.Tone);
                foreach (var r in row.Replies.Where(r => !Tone(r)))
                    if (r.Tone != null || r.Even != null || r.Grates != null) bad.Add(row.Id + ": a choice reply carries a tone or a landing");
                if (row.Replies.Any(r => r.HoldsBack || r.Favour != TalkFavour.None)) bad.Add(row.Id + ": a slice 1 or 2 reply field");
                if (tones.Select(r => r.Tone).Distinct().Count() != tones.Count) bad.Add(row.Id + ": a tone offered twice");
                if (choices.Distinct().Count() != choices.Count) bad.Add(row.Id + ": a choice offered twice");
                var allowed = ChoicesOf[row.Kind];
                if (choices.Any(c => !allowed.Contains(c))) bad.Add(row.Id + ": a choice its kind does not hold");
                bool scene = allowed.Length == 0 && row.Kind != TalkKind.FlirtLine && row.Kind != TalkKind.Ex && row.Kind != TalkKind.Reaction;
                if (row.Kind != TalkKind.TurnAside && row.Second != null) bad.Add(row.Id + ": only a turn-aside has a second beat");
                if (row.Kind != TalkKind.TurnAside && row.Attempt != 0) bad.Add(row.Id + ": only a turn-aside has an attempt");
                if (row.Kind != TalkKind.Reaction && row.Reaction != 0) bad.Add(row.Id + ": only a reaction has a reaction number");
                switch (row.Kind)
                {
                    case TalkKind.FlirtLine:
                        if (row.Open != null || row.Replies.Length != 1 || tones.Count != 1 || tones[0].Tone != TalkRules.Flirt) bad.Add(row.Id + ": a Flirt line is one Flirt reply and no opener");
                        break;
                    case TalkKind.Opening:
                        if (row.Open == null || tones.Count < 2 || tones.Count > 3 || tones.Count(r => r.Tone == TalkRules.Flirt) != 1 || !choices.SequenceEqual(allowed))
                            bad.Add(row.Id + ": an opening has 2 or 3 tones, one of them Flirt, and Just friends");
                        break;
                    case TalkKind.TurnAside:
                        if (row.Open == null || row.Attempt < 1 || row.Attempt > 3) bad.Add(row.Id + ": a turn-aside has an opener and an attempt 1 to 3");
                        if (row.Attempt == 1 && (row.Second == null || tones.Count != 0 || !choices.SequenceEqual(allowed))) bad.Add(row.Id + ": attempt 1 is the reaction, the second beat and the three choices");
                        if (row.Attempt > 1 && (row.Second != null || row.Replies.Length != 0)) bad.Add(row.Id + ": attempts 2 and 3 are one line");
                        break;
                    case TalkKind.Declaration:
                        if (row.Open == null || tones.Count != 0 || !choices.SequenceEqual(allowed)) bad.Add(row.Id + ": a declaration is Accept, Not yet, Not like that and Just friends");
                        break;
                    case TalkKind.Couple:
                        if (row.Open == null || tones.Count < 2 || tones.Count > 3 || tones.Any(r => r.Tone == TalkRules.Flirt) || !choices.SequenceEqual(allowed)) bad.Add(row.Id + ": a couple talk is 2 or 3 tones and End it kindly");
                        break;
                    case TalkKind.Jealousy:
                        if (row.Open == null || tones.Count != 3 || tones.Any(r => r.Tone == TalkRules.Flirt)) bad.Add(row.Id + ": a jealousy talk is 3 tones");
                        break;
                    case TalkKind.Ex:
                    case TalkKind.Reaction:
                        if (row.Open == null || tones.Count != 3 || choices.Count != 0 || tones.Any(r => r.Tone == TalkRules.Flirt)) bad.Add(row.Id + ": 3 tones, no Flirt, no choice");
                        if (row.Kind == TalkKind.Reaction && row.Reaction != 1 && row.Reaction != 2) bad.Add(row.Id + ": reaction 1 or 2");
                        break;
                    default:
                        if (!scene || row.Open == null || row.Replies.Length != 0) bad.Add(row.Id + ": a scene is one line");
                        break;
                }
            }
            Assert.That(bad, Is.Empty);
        }

        // ------------------------------------------------------------------ coverage

        /// <summary>Each kind a class pool must hold at least three different talks of (slice 3's brief, A).</summary>
        static readonly TalkKind[] PoolKinds =
        {
            TalkKind.FlirtLine, TalkKind.Opening, TalkKind.SoftNo, TalkKind.LineDrawn, TalkKind.Declaration, TalkKind.Accepts,
            TalkKind.Refuses, TalkKind.Couple, TalkKind.Ex, TalkKind.Jealousy,
        };

        [Test]
        public void EveryPoolHoldsThreeDifferentTalksOfEachKindAndTheFlirtsBuildWithTheSpark()
        {
            var bad = new List<string>();
            for (int i = 0; i < Pools.Length; i++)
            {
                var rows = PoolTables[i];
                foreach (var kind in PoolKinds)
                {
                    var of = rows.Where(r => r.Kind == kind).ToList();
                    if (of.Count < Variants) bad.Add(Pools[i] + " " + kind + ": " + of.Count);
                    var words = of.Select(r => r.Open ?? r.Replies[0].Lands).ToList();
                    if (words.Distinct().Count() != words.Count) bad.Add(Pools[i] + " " + kind + ": two talks say the same words");
                }
                for (int n = 1; n <= 2; n++)
                    if (rows.Count(r => r.Kind == TalkKind.Reaction && r.Reaction == n) < Variants) bad.Add(Pools[i] + " reaction " + n);
                foreach (var kind in new[] { TalkKind.FlirtLine, TalkKind.Opening })
                {
                    var bands = rows.Where(r => r.Kind == kind).GroupBy(r => r.When.Spark).ToList();
                    if (bands.Count < 3 || bands.Any(b => b.Count() < Variants) || bands.All(b => b.Key != 0))
                        bad.Add(Pools[i] + " " + kind + ": want three talks at each of three spark gates, from no gate up (early, building, close to ready)");
                }
                if (rows.Any(r => r.Kind == TalkKind.TurnAside || r.Kind == TalkKind.HoldBack)) bad.Add(Pools[i] + ": turn-asides and hold-backs are the All-Stars' own");
            }
            Assert.That(bad, Is.Empty);
        }

        /// <summary>What a row holds, without its words: the twelve tables must not be told apart by it (U5).</summary>
        static string Shape(TalkRow r) =>
            r.Kind + "/" + r.Attempt + "/" + r.Reaction + "/" + r.When.Spark + "/" + (r.Second != null) + "/" +
            string.Join(",", r.Replies.Select(x => x.Choice != TalkChoice.None ? x.Choice.ToString() : x.Tone == TalkRules.Flirt ? "flirt" : "tone"));

        [Test]
        public void TheTwelveAllStarTablesHoldTheSameKindsAsOneAnother()
        {
            var shapes = TalkTables.StarTables.ToDictionary(p => p.Key, p => string.Join(" ", p.Value.Select(Shape).OrderBy(s => s)));
            var first = shapes.Values.First();
            var bad = shapes.Where(p => p.Value != first).Select(p => p.Key).ToList();
            Assert.That(bad, Is.Empty, "A stance is a setting: no table may show it by what it holds.");
        }

        [Test]
        public void EveryAllStarTableHoldsEveryKindItsPlayerCanMeet()
        {
            var bad = new List<string>();
            foreach (var pair in TalkTables.StarTables)
            {
                var rows = pair.Value;
                foreach (var kind in new[] { TalkKind.Opening, TalkKind.SoftNo, TalkKind.HoldBack, TalkKind.Declaration, TalkKind.Accepts,
                             TalkKind.Refuses, TalkKind.Couple, TalkKind.Ex, TalkKind.Jealousy })
                    if (!rows.Any(r => r.Kind == kind)) bad.Add(pair.Key + ": no " + kind);
                if (rows.Count(r => r.Kind == TalkKind.FlirtLine) < 3) bad.Add(pair.Key + ": three Flirt lines");
                for (int n = 1; n <= 3; n++)
                    if (rows.Count(r => r.Kind == TalkKind.TurnAside && r.Attempt == n) != 1) bad.Add(pair.Key + ": turn-aside attempt " + n);
                for (int n = 1; n <= 2; n++)
                    if (!rows.Any(r => r.Kind == TalkKind.Reaction && r.Reaction == n)) bad.Add(pair.Key + ": reaction " + n);
                if (rows.Any(r => r.Kind == TalkKind.LineDrawn)) bad.Add(pair.Key + ": the line drawn is a closed regular's");
                if (TalkStarCallbacks.Rows.Count(r => r.Owner == pair.Key) != 1) bad.Add(pair.Key + ": one running-joke callback");
            }
            Assert.That(TalkStarCallbacks.Rows.All(r => TalkTables.StarTables.ContainsKey(r.Owner) && r.Pool == null && r.Replies.Length == 3 && r.Replies.All(Tone)), Is.True,
                "A callback is an All-Star's own talk with three tone replies.");
            Assert.That(bad, Is.Empty);
        }

        // ------------------------------------------------------------------ real people (rulings R7, R14, R15; slice 3's U5 to U8)

        /// <summary>
        /// The words a line would need to state or imply a fact about a real person's private life, or to be more than tame about
        /// one: partners and family, a home, faith, politics, health, looks, and the romantic words. Whole words, any case; "ex-" as
        /// a prefix.
        /// </summary>
        static readonly string[] Deny =
        {
            "wife", "wives", "husband", "husbands", "married", "marry", "marriage", "wedding", "engaged", "engagement", "girlfriend",
            "boyfriend", "fiance", "fiancee", "spouse", "partner", "partners", "kid", "kids", "children", "child", "son", "sons", "daughter",
            "daughters", "baby", "babies", "pregnant", "pregnancy", "divorce", "divorced", "ex", "exes", "family", "families", "mom", "dad",
            "mother", "father", "parents", "dating", "single", "taken", "home", "at home", "back home", "off the market", "spoken for",
            "religion", "religious", "church", "god", "pray", "prayer", "faith", "bible", "politics", "political", "election", "president",
            "cancer", "illness", "ill", "sick", "disease", "hospital", "therapy", "therapist", "surgery", "rehab", "sober", "addiction",
            "depression", "medication", "body", "looks",
            "kiss", "kissed", "kissing", "kisses", "date", "dates", "date night", "romance", "romantic", "crush", "flirt", "flirting", "flirty",
            "love", "loved", "love you", "lover", "in love", "showmance", "couple", "sexy", "sex", "darling", "sweetheart", "babe", "honey",
            "sweetie", "gorgeous", "handsome", "beautiful", "pretty", "lovely", "cute", "hot", "bed", "adore", "heart", "hearts",
        };

        static readonly Regex DenyWords = new Regex(@"\b(?:" + string.Join("|", Deny.Select(Regex.Escape)) + @")\b|\bex-", RegexOptions.IgnoreCase);

        [TestCase("Back home, it's different.")] [TestCase("You're cute when you plan.")] [TestCase("Date night in the yard?")]
        [TestCase("I love you.")] [TestCase("The bed by the window.")]
        public void TheDenyListCatchesAPrivateOrUntameWord(string line) => Assert.That(DenyWords.IsMatch(line), Is.True);

        [TestCase("Final two, together. A target on both backs.")] [TestCase("I'm here to play.")] [TestCase("Exactly the vote we needed.")]
        public void TheDenyListLetsGameWordsThrough(string line) => Assert.That(DenyWords.IsMatch(line), Is.False);

        [Test]
        public void NoLineAnAllStarSaysOrThatCanBeSaidAboutOneHoldsADeniedWord()
        {
            // An All-Star says only their own table's lines and their callback; a third housemate's reaction and a jealous
            // housemate's talk can be about an All-Star (the player's undecided or open partner, or the one flirted with).
            var read = TalkTables.StarTables.Values.SelectMany(t => t).Concat(TalkStarCallbacks.Rows)
                .Concat(PoolTables.SelectMany(t => t).Where(r => r.Kind == TalkKind.Reaction || r.Kind == TalkKind.Jealousy))
                .SelectMany(Lines).ToList();
            Assert.That(read.Count, Is.GreaterThan(12 * 60), "Every All-Star line and every pool line that can be about one was read.");
            var bad = read.SelectMany(l => DenyWords.Matches(l.Value).Cast<Match>().Select(m => l.Key + ": " + m.Value)).ToList();
            Assert.That(bad, Is.Empty);
        }

        [Test]
        public void NoTurnAsideOrHoldBackFormsABondOrGivesAReason()
        {
            var bad = new List<string>();
            var reason = new Regex(@"\b(because|reason|reasons|outside|out there|real life|after the show|when this is over)\b", RegexOptions.IgnoreCase);
            foreach (var row in Romance.Where(r => r.Kind == TalkKind.TurnAside || r.Kind == TalkKind.HoldBack))
            {
                if (row.Owner == null || row.Pool != null) bad.Add(row.Id + ": an All-Star's own row");
                // Their effects: a turn-aside's only choices are the scene's three, none of them a bond (Accept, Make it official).
                if (row.Replies.Any(r => r.Choice != TalkChoice.LaughItOff && r.Choice != TalkChoice.PushAgain && r.Choice != TalkChoice.Apologize))
                    bad.Add(row.Id + ": a reply that is not Laugh it off, Push again or Apologize");
                if (row.Kind == TalkKind.HoldBack && row.Replies.Length != 0) bad.Add(row.Id + ": a hold-back is one line");
                foreach (var line in Lines(row))
                    foreach (Match m in reason.Matches(line.Value)) bad.Add(line.Key + ": " + m.Value);
            }
            Assert.That(Romance.Count(r => r.Kind == TalkKind.TurnAside), Is.EqualTo(36), "Twelve All-Stars, three attempts each.");
            Assert.That(bad, Is.Empty, "The scene never forms a bond and never gives a reason.");
        }
    }
}
