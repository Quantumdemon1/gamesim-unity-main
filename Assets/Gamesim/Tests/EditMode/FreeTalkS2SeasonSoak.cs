#if !UNITY_5_3_OR_NEWER
// Unity-free only (Tools/SimulationTests): Unity's test runner executes [Explicit] tests in a batch run, so this whole-season
// soak is compiled out of the editor's assemblies and run by name with dotnet test.
using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Free talk slice 2's season soak: fresh seasons the game starts (talk rules 2) in which the player takes every free talk on
    /// offer in every window, to the end of the season, in three arms: a fixed rotation of tones, always the boldest tone, and
    /// always the most careful one; twenty seeds an arm, half of them All-Star casts. Every breach fails the case: a second
    /// counted talk in a window; a talk said twice to a housemate (but "the usual" or a weekly standing talk); a stage talk
    /// played twice, out of order, past a stage not yet played, or before its stage holds; a stage talk without its one event
    /// row, or an event row without one; the lean outside -6..6 or away from its tone's step; the player's class more than one
    /// from where it started; the bank dry; an All-Star given a talk outside the star pool; the season's randomState moved by
    /// a talk; a state that does not validate or is not the same after a save and reload. Each arm prints the counts the
    /// owner's feedback needs: housemates per stage reached, per chemistry word shown, and the player's class at the end.
    /// </summary>
    public sealed class FreeTalkS2SeasonSoak
    {
        private const string Why = "Free talk slice 2's whole-season soak: twenty seasons an arm, each to its end. Run by name.";

        private const int Seeds = 20;

        /// <summary>The arms: how the player answers.</summary>
        internal enum Arm { Rotation, Boldest, MostCareful }

        [Test, Explicit(Why)] public void RotationOfTonesTwentySeasons() => Seasons(Arm.Rotation, 4101u);

        [Test, Explicit(Why)] public void BoldestToneTwentySeasons() => Seasons(Arm.Boldest, 4201u);

        [Test, Explicit(Why)] public void MostCarefulToneTwentySeasons() => Seasons(Arm.MostCareful, 4301u);

        // ------------------------------------------------------------------ the counts

        /// <summary>What an arm saw, summed over its seasons.</summary>
        internal sealed class Tally
        {
            public int Seasons, Finished, Talks, Windows, StageTalks, Favours, FavoursKept, FavoursRefused, ChemistryFirstShown, StarTalks;
            public int MostLean, LeastLean;
            /// <summary>Housemates (all who were in the house) by the highest stage talk the player played with them, Stranger to Close friend.</summary>
            public readonly int[] StagePlayed = new int[5];
            /// <summary>Housemates by their derived stage at the season's end (Ride or die counted apart).</summary>
            public readonly int[] StageAtEnd = new int[6];
            public readonly Dictionary<string, int> Chemistry = new Dictionary<string, int>(StringComparer.Ordinal);
            public readonly Dictionary<string, int> ClassAtEnd = new Dictionary<string, int>(StringComparer.Ordinal);
            public readonly Dictionary<string, int> ClassAtStart = new Dictionary<string, int>(StringComparer.Ordinal);
            public int Housemates;

            public static void Add(Dictionary<string, int> map, string key) => map[key ?? "none"] = (map.TryGetValue(key ?? "none", out int n) ? n : 0) + 1;

            private static string Map(Dictionary<string, int> map) => string.Join(", ", map.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + " " + p.Value));

            public string Line(string label) =>
                "S2SOAK " + label + ": seasons " + Seasons + " (finished " + Finished + "), talks " + Talks + ", windows " + Windows
                + ", stage talks " + StageTalks + ", favours " + Favours + " (kept " + FavoursKept + ", refused " + FavoursRefused + ")"
                + ", star-pool talks " + StarTalks + ", lean range " + LeastLean + ".." + MostLean
                + " | housemates " + Housemates
                + " | stage talk played to: stranger " + StagePlayed[0] + ", acquainted " + StagePlayed[1] + ", friendly " + StagePlayed[2]
                + ", friend " + StagePlayed[3] + ", close friend " + StagePlayed[4]
                + " | stage at end: stranger " + StageAtEnd[0] + ", acquainted " + StageAtEnd[1] + ", friendly " + StageAtEnd[2]
                + ", friend " + StageAtEnd[3] + ", close friend " + StageAtEnd[4] + ", ride or die " + StageAtEnd[5]
                + " | chemistry at end: " + Map(Chemistry) + " (first shown " + ChemistryFirstShown + ")"
                + " | player class at start: " + Map(ClassAtStart) + "; at end: " + Map(ClassAtEnd);
        }

        private static void Seasons(Arm arm, uint first)
        {
            Assert.That(TalkBank.Rows, Is.SameAs(TalkTables.All), "The soak walks the shipped tables.");
            var tally = new Tally { MostLean = 0, LeastLean = 0 };
            for (uint seed = first; seed < first + Seeds; seed++)
            {
                var roster = seed % 2 == 0 ? CastTemplates.Roster.AllStars : CastTemplates.Roster.Regular;
                Season(seed, roster, arm, tally);
                TestContext.WriteLine("season " + arm + "/" + roster + "/" + seed + " done; talks so far " + tally.Talks);
            }
            TestContext.WriteLine(tally.Line(arm.ToString()));
            Assert.That(tally.Finished, Is.EqualTo(Seeds), "Every season reached its end.");
            Assert.That(tally.StageTalks, Is.GreaterThan(0), "Stage talks were played.");
        }

        // ------------------------------------------------------------------ the walk

        private static readonly string[] Rotation =
            { TalkRules.Warm, TalkRules.Tease, TalkRules.Straight, TalkRules.Careful, TalkRules.Push, TalkRules.LetGo, TalkRules.CallOut };

        /// <summary>The tone the arm answers in, among the talk's own tones.</summary>
        private static string Pick(Arm arm, IList<string> tones, int talks)
        {
            switch (arm)
            {
                case Arm.Boldest:
                    return tones.OrderByDescending(TalkLean.Step).ThenBy(t => Array.IndexOf(Rotation, t)).First();
                case Arm.MostCareful:
                    return tones.OrderBy(TalkLean.Step).ThenBy(t => Array.IndexOf(Rotation, t)).First();
                default:
                    for (int i = 0; i < Rotation.Length; i++)
                    {
                        string t = Rotation[(talks + i) % Rotation.Length];
                        if (tones.Contains(t)) return t;
                    }
                    return tones[0];
            }
        }

        private static readonly string[] ClassOrder = { TalkPools.Spark, TalkPools.Steady, TalkPools.Guarded };

        private sealed class Walk
        {
            public readonly Dictionary<string, HashSet<string>> Seen = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            public readonly Dictionary<string, FriendStage> LastStage = new Dictionary<string, FriendStage>(StringComparer.Ordinal);
            public readonly HashSet<string> Talked = new HashSet<string>(StringComparer.Ordinal);
            public string StartClass;
        }

        private static void Season(uint seed, CastTemplates.Roster roster, Arm arm, Tally tally)
        {
            var engine = new EpisodeEngine(FreeTalkIntegrationTests.FreshShipped(seed, roster, 12));
            Assert.That(engine.Snapshot.story.talkRules, Is.EqualTo(TalkRules.Chemistry), "A season the game starts is at talk rules 2.");
            var walk = new Walk { StartClass = TalkLean.StartClass(engine.Snapshot) };
            Assert.That(ClassOrder, Does.Contain(walk.StartClass));
            Tally.Add(tally.ClassAtStart, walk.StartClass);
            tally.Seasons++;
            int lastKey = -1;
            string label = arm + "/" + roster + "/" + seed;
            for (int step = 0; step < 12000; step++)
            {
                var s = engine.Snapshot;
                if (s.phase == EpisodePhase.Finished) { End(s, walk, tally); return; }
                int key = FreeTalk.WindowKey(s);
                bool playerIn = s.Find(s.playerId).status == ContestantStatus.Active;
                if (key != 0 && playerIn && key != lastKey) { tally.Windows++; lastKey = key; }
                if (key != 0 && playerIn)
                    foreach (var npc in s.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList())
                        TalkOnce(engine, npc, key, arm, walk, tally, label);
                s = engine.Snapshot;
                if (s.phase == EpisodePhase.Finished) { End(s, walk, tally); return; }
                var result = engine.Apply(Next(s));
                Assert.That(result.accepted, Is.True, label + ": the house's step: " + result.reason);
                ValidAndReloads(engine.Snapshot, label + " after a house step");
            }
            Assert.Fail(label + ": the season did not finish within its step budget.");
        }

        private static void End(EpisodeState s, Walk walk, Tally tally)
        {
            tally.Finished++;
            foreach (var npc in s.contestants.Where(c => !c.isPlayer))
            {
                tally.Housemates++;
                var played = walk.LastStage.TryGetValue(npc.id, out var p) ? p : FriendStage.Stranger;
                tally.StagePlayed[(int)played]++;
                tally.StageAtEnd[(int)TalkFriendship.Stage(s, npc.id)]++;
                Tally.Add(tally.Chemistry, TalkChemistry.WordFor(s, npc.id) ?? "not shown");
            }
            string cls = TalkLean.PlayerClass(s);
            Tally.Add(tally.ClassAtEnd, cls);
            Assert.That(Math.Abs(Array.IndexOf(ClassOrder, cls) - Array.IndexOf(ClassOrder, walk.StartClass)), Is.LessThanOrEqualTo(1),
                "The player's class at the end is at most one from where it started.");
        }

        /// <summary>The engine tests' next lawful command, answering a finale question with an offered response (as FreeTalkIntegrationTests' walk does).</summary>
        private static EpisodeCommand Next(EpisodeState state)
        {
            var command = EpisodeEngineTests.NextCommand(state);
            if (command.kind == EpisodeCommandKind.AnswerJury && EpisodeEngine.FinaleOn(state))
            {
                var exchange = state.juryExchanges[state.juryQuestionIndex];
                if (exchange.finalistId == state.playerId)
                    command.secondTargetId = FinaleQuestions.Offered(exchange.category, exchange.receiptKind).First();
            }
            return command;
        }

        private static void TalkOnce(EpisodeEngine engine, string npcId, int key, Arm arm, Walk walk, Tally tally, string label)
        {
            var s = engine.Snapshot;
            var npc = s.Find(npcId);
            string where = label + ": " + npc.id + " in window " + key + " (week " + s.week + ")";
            var offer = FreeTalk.Offer(s, npcId);
            if (walk.Talked.Contains(npcId + "|" + key))
            {
                Assert.That(offer.Refusal, Is.EqualTo(TalkRefusal.TalkedThisWindow), where + ": a second counted talk in one window.");
                return;
            }
            Assert.That(offer.Refusal, Is.EqualTo(TalkRefusal.None), where + ": the bank ran dry or the talk was refused: " + offer.Reason);
            var talk = offer.Talk;
            bool star = FreeTalk.IsAllStar(npc);
            if (star)
            {
                tally.StarTalks++;
                Assert.That(talk.Pool, Is.EqualTo(TalkPools.Star), where + ": an All-Star is given only the star pool: " + talk.Id);
                Assert.That(talk.Owner, Is.Null, where + ": no person's own talks ship.");
            }
            else Assert.That(talk.Pool, Is.EqualTo(FreeTalk.ClassOf(npc)), where + ": a housemate is given their class's pool: " + talk.Id);
            if (!walk.Seen.TryGetValue(npcId, out var ids)) walk.Seen[npcId] = ids = new HashSet<string>(StringComparer.Ordinal);
            if (offer.Repeat)
            {
                Assert.That(talk.Usual || talk.Kind == TalkKind.Standing, Is.True, where + ": only 'the usual' or a standing talk comes round again: " + talk.Id);
                Assert.That(ids.Contains(talk.Id), Is.True, where + ": a repeat of a talk never said: " + talk.Id);
            }
            else Assert.That(ids.Add(talk.Id), Is.True, where + ": " + talk.Id + " was said to them before.");

            // A stage talk: never twice, in order, none skipped, and only once its stage holds.
            bool stageTalk = talk.Kind == TalkKind.Stage;
            var lastStage = walk.LastStage.TryGetValue(npcId, out var ls) ? ls : FriendStage.Stranger;
            if (stageTalk)
            {
                Assert.That(offer.Repeat, Is.False, where + ": a stage talk is never a repeat.");
                Assert.That(talk.Stage, Is.EqualTo(lastStage + 1), where + ": stage talks come in order, none skipped or played twice (" + talk.Id + " after " + lastStage + ").");
                Assert.That(TalkFriendship.TableStage(s, npcId), Is.GreaterThanOrEqualTo(talk.Stage), where + ": a stage talk before its stage holds.");
            }

            var tones = talk.Replies.Select(r => r.Tone).ToList();
            Assert.That(tones, Has.None.EqualTo(TalkRules.Flirt), where + ": no flirt in slice 2.");
            string tone = Pick(arm, tones, tally.Talks);
            var preview = FreeTalk.Result(s, npcId, talk.Id, tone);
            Assert.That(preview, Is.Not.Null, where + ": no result for the talk on offer.");

            uint before = s.randomState;
            int revision = s.revision;
            int leanBefore = s.story.talkLean;
            long sequence = s.nextSequence;
            var applied = engine.Apply(FreeTalkFixture.Command(s, npcId, talk.Id, tone));
            Assert.That(applied.accepted, Is.True, where + ": " + applied.reason);
            var after = engine.Snapshot;
            Assert.That(after.randomState, Is.EqualTo(before), where + ": a free talk drew from the season's stream.");
            Assert.That(after.revision, Is.EqualTo(revision + 1), where + ": one command.");
            var contact = FreeTalk.Contact(after, npcId);
            Assert.That(contact.talkWindow, Is.EqualTo(key), where);
            Assert.That(contact.rapport, Is.EqualTo(preview.RapportAfter), where + ": rapport as previewed.");

            // The lean (T4): the tone's step, clamped; the player's class at most one from its start.
            Assert.That(after.story.talkLean, Is.EqualTo(TalkLean.After(leanBefore, tone)), where + ": the lean moves by the tone's step.");
            Assert.That(after.story.talkLean, Is.InRange(-TalkRules.MostLean, TalkRules.MostLean), where);
            tally.MostLean = Math.Max(tally.MostLean, after.story.talkLean);
            tally.LeastLean = Math.Min(tally.LeastLean, after.story.talkLean);
            string cls = TalkLean.PlayerClass(after);
            Assert.That(Math.Abs(Array.IndexOf(ClassOrder, cls) - Array.IndexOf(ClassOrder, walk.StartClass)), Is.LessThanOrEqualTo(1),
                where + ": the player's class " + cls + " is more than one from " + walk.StartClass + ".");

            // The event row: one for a stage talk, none otherwise.
            var stageRows = after.events.Where(e => e.sequence >= sequence && e.kind == TalkFriendship.StageEventKind).ToList();
            if (stageTalk)
            {
                tally.StageTalks++;
                walk.LastStage[npcId] = talk.Stage;
                Assert.That(stageRows.Count, Is.EqualTo(1), where + ": a stage talk writes one event row.");
                Assert.That(stageRows[0].text, Is.EqualTo(preview.StageEvent), where);
                Assert.That(preview.StageReached, Is.EqualTo(talk.Stage), where);
            }
            else Assert.That(stageRows, Is.Empty, where + ": an event row without a stage talk.");

            // The favour (T6): only on a Close friend's stage talk, +2 or -2 as one act.
            if (preview.Favour != TalkFavour.None)
            {
                Assert.That(stageTalk && talk.Stage == FriendStage.CloseFriend, Is.True, where + ": a favour off the Close friend talk.");
                Assert.That(preview.Warmth, Is.EqualTo(preview.Favour == TalkFavour.Keep ? TalkFriendship.FavourWarmth : -TalkFriendship.FavourWarmth), where);
                tally.Favours++;
                if (preview.Favour == TalkFavour.Keep) tally.FavoursKept++; else tally.FavoursRefused++;
            }
            if (preview.ChemistryShown != null)
            {
                Assert.That(contact.talks, Is.EqualTo(TalkChemistry.ShownAfterTalks), where + ": the chemistry first shows on the third counted talk.");
                tally.ChemistryFirstShown++;
            }
            tally.Talks++;
            walk.Talked.Add(npcId + "|" + key);

            // The cap: the same housemate again in this window is refused and moves nothing.
            Assert.That(FreeTalk.Offer(after, npcId).Refusal, Is.EqualTo(TalkRefusal.TalkedThisWindow), where);
            string json = JsonConvert.SerializeObject(after);
            var again = engine.Apply(FreeTalkFixture.Command(after, npcId, talk.Id, tone));
            Assert.That(again.accepted, Is.False, where + ": a second counted talk was accepted.");
            Assert.That(JsonConvert.SerializeObject(engine.Snapshot), Is.EqualTo(json), where + ": a refused talk moved the season.");
            ValidAndReloads(after, where);
        }

        private static readonly JsonSerializerSettings Reload = new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace };

        /// <summary>The state validates, and a save and reload of it (its JSON read back into a new engine) is the same state.</summary>
        private static void ValidAndReloads(EpisodeState s, string where)
        {
            Assert.That(EpisodeValidation.TryValidate(s, out string reason), Is.True, where + ": " + reason);
            string json = JsonConvert.SerializeObject(s);
            var loaded = JsonConvert.DeserializeObject<EpisodeState>(json, Reload);
            Assert.That(EpisodeValidation.TryValidate(loaded, out reason), Is.True, where + " (reloaded): " + reason);
            Assert.That(JsonConvert.SerializeObject(new EpisodeEngine(loaded).Snapshot), Is.EqualTo(json), where + ": the reload is not the same state.");
        }
    }
}
#endif
