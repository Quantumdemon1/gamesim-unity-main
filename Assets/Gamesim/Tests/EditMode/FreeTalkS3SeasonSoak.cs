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
    /// Free talk slice 3's season soak: fresh seasons the game starts (talk rules 3) in which the player takes every free talk on
    /// offer in every window, to the end of the season, in three arms: one answers Flirt whenever it is offered and accepts
    /// everything; one flirts with everyone and then says Not yet, Just friends and ends things in rotation; one never flirts.
    /// Twenty seeds an arm, half of them All-Star casts. Every breach fails the case: an exception; a second counted talk in a
    /// window; a scene the rules play once played twice (a hold-back, a line drawn, a turn-aside attempt), or a turn-aside out of
    /// order; Flirt offered after it was taken away; a romance field out of its bounds; a bond with a here-to-play housemate; an
    /// undecided housemate faster than a regular of their class with the same answer, opening, or declaring before six Flirts;
    /// a housemate opening or declaring before the player's first Flirt; an All-Star given a talk from outside the star pool and
    /// their own table; the season's randomState moved by a talk or a choice; a state that does not validate or is not the same
    /// after a save and reload. Each arm prints the counts the owner's feedback needs.
    /// </summary>
    public sealed class FreeTalkS3SeasonSoak
    {
        private const string Why = "Free talk slice 3's whole-season soak: twenty seasons an arm, each to its end. Run by name.";

        private const int Seeds = 20;

        internal enum Arm { Eager, Rotation, Never, Focused }

        /// <summary>
        /// A fourth arm for the owner's pace questions: the player courts ONE housemate (the first regular, by id, who is open or
        /// strategic and interested at move-in, a Spark one first) as the eager arm does, and never flirts with anyone else.
        /// </summary>
        [Test, Explicit(Why)] public void CourtsOneInterestedHousemateTwentySeasons() => Seasons(Arm.Focused, 5401u);

        [Test, Explicit(Why)] public void FlirtsAndAcceptsEverythingTwentySeasons() => Seasons(Arm.Eager, 5101u);

        [Test, Explicit(Why)] public void FlirtsThenNotYetJustFriendsAndEndingsInRotationTwentySeasons() => Seasons(Arm.Rotation, 5201u);

        [Test, Explicit(Why)] public void NeverFlirtsTwentySeasons() => Seasons(Arm.Never, 5301u);

        // ------------------------------------------------------------------ the counts

        internal sealed class Tally
        {
            public int Seasons, Finished, Talks, Flirts, Continuations, PlayerHereToPlay;
            public int Couples, SeasonsWithACouple, SoftNos, HoldBacks, LinesDrawn, TurnAsides, TurnAsidesToThree, Declarations, Openings;
            public int NotYets, JustFriends, Endings, TheyEnded, PartnerHeard, WentPublic, Reactions, SecondDeclarations, Refused;
            public readonly List<int> FirstCoupleWeek = new List<int>();
            /// <summary>Why no couple: in-kind answers, spark reached 40, Ready then, and what Ready lacked (warmth, rapport, week).</summary>
            public int InKinds, AtForty, ReadyAtForty, LackWarmth, LackRapport, LackWeek;
            public readonly List<int> MostSpark = new List<int>();
            public readonly List<int> PlayerLastWeek = new List<int>();

            private static string Spread(List<int> values) => values.Count == 0 ? "none"
                : "n " + values.Count + ", 0: " + values.Count(v => v == 0) + ", 1-19: " + values.Count(v => v > 0 && v < 20)
                  + ", 20-39: " + values.Count(v => v >= 20 && v < 40) + ", 40+: " + values.Count(v => v >= 40);

            public string Why(string label) =>
                "S3SOAK " + label + " why: in-kind answers " + InKinds + ", spark reached 40 on " + AtForty + " (ready then " + ReadyAtForty
                + "; lacking warmth " + LackWarmth + ", rapport " + LackRapport + ", week " + LackWeek + ")"
                + " | most spark per flirted housemate: " + Spread(MostSpark)
                + " | the player's last week in the house: " + Weeks(PlayerLastWeek);
            public readonly Dictionary<string, List<int>> CoupleWeekByPair = new Dictionary<string, List<int>>(StringComparer.Ordinal);

            private static string Weeks(List<int> weeks) => weeks.Count == 0 ? "none"
                : "n " + weeks.Count + ", min " + weeks.Min() + ", median " + weeks.OrderBy(w => w).ElementAt(weeks.Count / 2) + ", max " + weeks.Max();

            public string Line(string label) =>
                "S3SOAK " + label + ": seasons " + Seasons + " (finished " + Finished + ", player card here to play " + PlayerHereToPlay + ")"
                + ", counted talks " + Talks + ", flirts " + Flirts + ", continuations " + Continuations
                + " | couples formed " + Couples + " in " + SeasonsWithACouple + " seasons; first couple's week: " + Weeks(FirstCoupleWeek)
                + " | couple week by pair (player+housemate): "
                + string.Join("; ", CoupleWeekByPair.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + " " + Weeks(p.Value)))
                + " | soft nos " + SoftNos + ", hold-backs " + HoldBacks + ", lines drawn " + LinesDrawn + ", turn-asides " + TurnAsides
                + " (reached attempt 3: " + TurnAsidesToThree + "), openings " + Openings + ", declarations " + Declarations
                + " | not yets " + NotYets + ", just friends " + JustFriends + ", ended kindly " + Endings + ", they ended it " + TheyEnded
                + ", second declarations " + SecondDeclarations + " | a partner heard of a flirt " + PartnerHeard
                + ", couples gone public " + WentPublic + ", house reactions said " + Reactions + ", refused commands " + Refused;
        }

        private static void Seasons(Arm arm, uint first)
        {
            Assert.That(TalkBank.Rows, Is.SameAs(TalkTables.All), "The soak walks the shipped tables.");
            var tally = new Tally();
            for (uint seed = first; seed < first + Seeds; seed++)
            {
                var roster = seed % 2 == 0 && arm != Arm.Focused ? CastTemplates.Roster.AllStars : CastTemplates.Roster.Regular;
                Season(seed, roster, arm, tally);
                TestContext.WriteLine("season " + arm + "/" + roster + "/" + seed + " done; talks so far " + tally.Talks + ", couples " + tally.Couples);
            }
            TestContext.WriteLine(tally.Line(arm.ToString()));
            TestContext.WriteLine(tally.Why(arm.ToString()));
            Assert.That(tally.Finished, Is.EqualTo(Seeds), "Every season reached its end.");
            if (arm == Arm.Never)
                Assert.That(tally.Flirts + tally.Couples + tally.SoftNos + tally.TurnAsides + tally.Declarations + tally.Openings, Is.EqualTo(0),
                    "A player who never flirts sees nothing romantic.");
            else Assert.That(tally.Flirts, Is.GreaterThan(0), "Flirt was offered and taken.");
        }

        // ------------------------------------------------------------------ the walk

        private static readonly string[] Rotation =
            { TalkRules.Warm, TalkRules.Tease, TalkRules.Straight, TalkRules.Careful, TalkRules.Push, TalkRules.LetGo, TalkRules.CallOut };

        private sealed class Walk
        {
            public readonly Dictionary<string, int> HoldBacks = new Dictionary<string, int>(StringComparer.Ordinal);
            public readonly Dictionary<string, int> LinesDrawn = new Dictionary<string, int>(StringComparer.Ordinal);
            public readonly Dictionary<string, int> LastAttempt = new Dictionary<string, int>(StringComparer.Ordinal);
            public readonly HashSet<string> Talked = new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> Public = new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> Jealous = new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> Couples = new HashSet<string>(StringComparer.Ordinal);
            public int FirstCoupleWeek, Choices, LastWeekIn;
            public string Target;
            public readonly Dictionary<string, int> MostSpark = new Dictionary<string, int>(StringComparer.Ordinal);
        }

        private static void Season(uint seed, CastTemplates.Roster roster, Arm arm, Tally tally)
        {
            var engine = new EpisodeEngine(FreeTalkIntegrationTests.FreshShipped(seed, roster, 12));
            var start = engine.Snapshot;
            Assert.That(start.story.talkRules, Is.EqualTo(TalkRules.Romance), "A season the game starts is at talk rules 3.");
            Assert.That(start.story.romanceStorylines, Is.True, "A fresh season plays romance.");
            if (TalkRomance.PlayerHereToPlay(start)) tally.PlayerHereToPlay++;
            var walk = new Walk();
            if (arm == Arm.Focused)
                walk.Target = start.Active.Where(c => !c.isPlayer && !FreeTalk.IsAllStar(c) && TalkRomance.Interested(start, c.id)
                        && (TalkRomance.StanceOf(start, c.id) == RomanceStance.Open || TalkRomance.StanceOf(start, c.id) == RomanceStance.Strategic))
                    .OrderBy(c => FreeTalk.ClassOf(c) == TalkPools.Spark ? 0 : 1).ThenBy(c => c.id, StringComparer.Ordinal).Select(c => c.id).FirstOrDefault();
            tally.Seasons++;
            string label = arm + "/" + roster + "/" + seed;
            for (int step = 0; step < 12000; step++)
            {
                var s = engine.Snapshot;
                if (s.phase == EpisodePhase.Finished) { End(s, arm, walk, tally); return; }
                int key = FreeTalk.WindowKey(s);
                bool playerIn = s.Find(s.playerId).status == ContestantStatus.Active;
                if (playerIn) walk.LastWeekIn = s.week;
                if (key != 0 && playerIn)
                    foreach (var npc in s.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList())
                        TalkOnce(engine, npc, key, arm, walk, tally, label);
                s = engine.Snapshot;
                if (s.phase == EpisodePhase.Finished) { End(s, arm, walk, tally); return; }
                var result = engine.Apply(Next(s));
                Assert.That(result.accepted, Is.True, label + ": the house's step: " + result.reason);
                AfterStep(engine.Snapshot, walk, tally, label);
            }
            Assert.Fail(label + ": the season did not finish within its step budget.");
        }

        private static void End(EpisodeState s, Arm arm, Walk walk, Tally tally)
        {
            tally.Finished++;
            tally.PlayerLastWeek.Add(walk.LastWeekIn);
            tally.MostSpark.AddRange(walk.MostSpark.Values);
            if (walk.FirstCoupleWeek > 0) { tally.SeasonsWithACouple++; tally.FirstCoupleWeek.Add(walk.FirstCoupleWeek); }
            foreach (var c in s.story.contacts.Where(c => c != null))
            {
                Bounds(c, "end");
                if (arm == Arm.Never)
                    Assert.That(c.spark == 0 && c.romance == "" && c.flirts == 0 && c.turnAsides == 0, Is.True,
                        "A player who never flirts: no spark, romance, flirt or turn-aside with " + c.npcId + ".");
            }
        }

        /// <summary>After a house step: the state validates and reloads, no here-to-play bond, and the counts the house's anchors move.</summary>
        private static void AfterStep(EpisodeState s, Walk walk, Tally tally, string label)
        {
            FreeTalkS3Fixture.ValidAndReloads(s, label + " after a house step (week " + s.week + ")");
            NoHereToPlayBond(s, label);
            foreach (var c in s.story.contacts.Where(c => c != null))
            {
                Bounds(c, label);
                if (c.talkFlags.Contains(TalkRules.Jealous)) { if (walk.Jealous.Add(c.npcId + "|" + c.romanceWeek + "|" + s.week)) tally.PartnerHeard++; }
                if (c.romance == TalkRules.Couple && IsPublic(s, c.npcId) && walk.Public.Add(c.npcId + "|" + c.romanceWeek)) tally.WentPublic++;
                if (walk.Couples.Contains(c.npcId) && c.romance != TalkRules.Couple) { walk.Couples.Remove(c.npcId); tally.TheyEnded++; }
            }
        }

        private static bool IsPublic(EpisodeState s, string npcId) =>
            Knowledge.Of(s, FactKinds.Couple, StoryConsumers.CoupleRef(s, s.playerId, npcId))?.visibility == FactVisibility.Public;

        private static void NoHereToPlayBond(EpisodeState s, string label)
        {
            foreach (var npc in s.contestants.Where(c => !c.isPlayer && TalkRomance.StanceOf(s, c.id) == RomanceStance.HereToPlay))
            {
                Assert.That(Bonds.Holds(s, s.playerId, npc.id, BondKinds.Showmance), Is.False, label + ": a bond with a here-to-play housemate " + npc.id + ".");
                var c = FreeTalk.Contact(s, npc.id);
                if (c != null)
                    Assert.That(c.spark == 0 && (c.romance == "" || c.romance == TalkRules.Friends), Is.True,
                        label + ": romance with a here-to-play housemate " + npc.id + ": " + c.romance + " spark " + c.spark);
            }
        }

        private static readonly string[] Flags =
        {
            TalkRules.RunningJoke, TalkRules.Pushy, TalkRules.TookTheNoWell, TalkRules.FlirtRemoved, TalkRules.Ex, TalkRules.Jealous,
            TalkRules.MayHear, TalkRules.CoupleTalk,
        };

        private static void Bounds(ContactState c, string where)
        {
            Assert.That(c.spark, Is.InRange(0, TalkRomance.MostSpark), where + ": spark");
            Assert.That(c.cool, Is.InRange(0, 2), where + ": cool");
            Assert.That(c.notYets, Is.InRange(0, 3), where + ": notYets");
            Assert.That(c.turnAsides, Is.InRange(0, 3), where + ": turnAsides");
            Assert.That(c.flirts, Is.InRange(0, TalkRules.MostCount), where + ": flirts");
            Assert.That(c.romance == "" || TalkRules.IsRomance(c.romance), Is.True, where + ": romance " + c.romance);
            Assert.That(c.talkFlags.Count, Is.LessThanOrEqualTo(TalkRules.MostFlags), where + ": flags");
            Assert.That(c.talkFlags.All(TalkRules.IsFlag), Is.True, where + ": a flag outside the set");
        }

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

        private static string Tone(IList<string> tones, int talks)
        {
            for (int i = 0; i < Rotation.Length; i++)
            {
                string t = Rotation[(talks + i) % Rotation.Length];
                if (tones.Contains(t)) return t;
            }
            return tones[0];
        }

        /// <summary>What the arm answers to a romance talk on offer (a choice, or a tone of its own).</summary>
        private static string RomanceAnswer(Arm arm, List<TalkChoice> choices, List<string> tones, Walk walk, int talks, bool hasPartner)
        {
            if (arm == Arm.Never) return Tone(tones.Where(t => t != TalkRules.Flirt).ToList(), talks);
            if (arm == Arm.Eager)
            {
                // Accepts everything, but never declares to a second housemate while a couple stands.
                if (hasPartner) choices = choices.Where(c => c != TalkChoice.MakeItOfficial).ToList();
                foreach (var c in new[] { TalkChoice.MakeItOfficial, TalkChoice.Accept })
                    if (choices.Contains(c)) return TalkChoices.Answer(c);
                if (tones.Contains(TalkRules.Flirt)) return TalkRules.Flirt;
                return Tone(tones, talks);
            }
            int turn = walk.Choices++ % 3;
            var wanted = turn == 0 ? new[] { TalkChoice.NotYet }
                : turn == 1 ? new[] { TalkChoice.JustFriends }
                : new[] { TalkChoice.EndItKindly, TalkChoice.MakeItOfficial, TalkChoice.Accept };
            foreach (var c in wanted)
                if (choices.Contains(c)) return TalkChoices.Answer(c);
            if (tones.Contains(TalkRules.Flirt)) return TalkRules.Flirt;
            return tones.Count == 0 ? TalkChoices.Answer(choices[0]) : Tone(tones, talks);
        }

        /// <summary>What the arm answers to a scene left open (a continuation), or null to let it lapse.</summary>
        private static TalkChoice? Continuation(Arm arm, List<TalkChoice> choices, Walk walk, bool hasPartner)
        {
            if (choices.Count == 0 || arm == Arm.Never) return null;
            if (arm == Arm.Eager)
            {
                if (!hasPartner && choices.Contains(TalkChoice.MakeItOfficial)) return TalkChoice.MakeItOfficial;
                if (choices.Contains(TalkChoice.PushAgain)) return TalkChoice.PushAgain;
                return null;
            }
            int turn = walk.Choices++ % 3;
            var wanted = turn == 0 ? new[] { TalkChoice.LaughItOff, TalkChoice.MakeItOfficial }
                : turn == 1 ? new[] { TalkChoice.JustFriends, TalkChoice.PushAgain }
                : new[] { TalkChoice.Apologize, TalkChoice.MakeItOfficial };
            foreach (var c in wanted)
                if (choices.Contains(c)) return c;
            return null;
        }

        private static void TalkOnce(EpisodeEngine engine, string npcId, int key, Arm arm, Walk walk, Tally tally, string label)
        {
            // The focused arm courts its one housemate as the eager arm does and talks to everyone else as the arm that never flirts.
            if (arm == Arm.Focused) arm = npcId == walk.Target ? Arm.Eager : Arm.Never;
            var s = engine.Snapshot;
            var npc = s.Find(npcId);
            string where = label + ": " + npc.id + " in window " + key + " (week " + s.week + ")";
            var offer = FreeTalk.Offer(s, npcId);
            if (walk.Talked.Contains(npcId + "|" + key))
            {
                Assert.That(offer.Refusal, Is.EqualTo(TalkRefusal.TalkedThisWindow), where + ": a second counted talk in one window.");
                return;
            }
            Assert.That(offer.Refusal, Is.EqualTo(TalkRefusal.None), where + ": the talk was refused: " + offer.Reason);
            var talk = offer.Talk;
            var contactBefore = FreeTalk.Contact(s, npcId);
            int flirtsBefore = contactBefore?.flirts ?? 0;
            bool star = FreeTalk.IsAllStar(npc);
            var stance = TalkRomance.StanceOf(s, npcId);
            bool romanceRow = TalkRomance.IsRomanceKind(talk.Kind);
            if (star)
            {
                if (romanceRow) Assert.That(talk.Owner, Is.EqualTo(StoryPeople.TemplateOf(npc)?.Id), where + ": an All-Star's romance talk from their own table: " + talk.Id);
                else Assert.That(talk.Pool == TalkPools.Star && talk.Owner == null, Is.True, where + ": an All-Star is given only the star pool: " + talk.Id);
            }
            else
            {
                Assert.That(talk.Owner, Is.Null, where + ": a regular is given no person's table: " + talk.Id);
                Assert.That(talk.Pool, Is.EqualTo(FreeTalk.ClassOf(npc)), where + ": a housemate is given their class's pool: " + talk.Id);
            }
            if (talk.Kind == TalkKind.Opening || talk.Kind == TalkKind.Declaration)
            {
                Assert.That(flirtsBefore, Is.GreaterThanOrEqualTo(1), where + ": they moved first before the player's first Flirt (R8).");
                Assert.That(stance, Is.Not.EqualTo(RomanceStance.HereToPlay).And.Not.EqualTo(RomanceStance.Closed), where);
                if (stance == RomanceStance.Undecided)
                {
                    Assert.That(talk.Kind, Is.EqualTo(TalkKind.Declaration), where + ": an undecided housemate never opens.");
                    Assert.That(flirtsBefore, Is.GreaterThanOrEqualTo(TalkRomance.UndecidedDeclaresAfter), where + ": undecided declared before six Flirts.");
                }
                if (talk.Kind == TalkKind.Opening) tally.Openings++; else tally.Declarations++;
            }
            if (talk.Kind == TalkKind.Reaction) tally.Reactions++;

            bool flirtOffered = TalkRomance.FlirtOffered(s, npcId, talk);
            if (flirtOffered)
            {
                Assert.That(contactBefore == null || !contactBefore.talkFlags.Contains(TalkRules.FlirtRemoved), Is.True, where + ": Flirt after flirt-removed.");
                Assert.That(TalkRomance.PlayerHereToPlay(s), Is.False, where + ": Flirt offered to a here-to-play player card.");
                if (stance == RomanceStance.Undecided)
                {
                    var (pc, nc) = (TalkLean.PlayerClass(s), FreeTalk.ClassOf(npc));
                    Assert.That(contactBefore?.rapport ?? 0, Is.GreaterThanOrEqualTo(TalkChemistry.RapportGate(s, npcId, TalkRomance.FlirtRapport(pc, nc)) + TalkRomance.UndecidedRapport),
                        where + ": undecided's Flirt gate is 3 higher.");
                }
            }

            var tones = talk.Replies.Where(r => r != null && r.Choice == TalkChoice.None && r.Tone != null).Select(r => r.Tone).Distinct().ToList();
            var choices = romanceRow ? TalkRomance.ChoicesNow(s, npcId, talk) : new List<TalkChoice>();
            string answer;
            if (romanceRow) answer = RomanceAnswer(arm, choices, tones, walk, tally.Talks, TalkRomance.PartnerOf(s) != null);
            else if (arm != Arm.Never && flirtOffered) answer = TalkRules.Flirt;
            else answer = Tone(tones.Where(t => t != TalkRules.Flirt).ToList(), tally.Talks);
            string witness = arm == Arm.Eager && tally.Talks % 4 == 3
                ? s.Active.Where(c => !c.isPlayer && c.id != npcId).Select(c => c.id).OrderBy(id => id, StringComparer.Ordinal).FirstOrDefault()
                : null;

            var result = Say(engine, npcId, TalkRomance.CommandText(talk.Id, answer, witness), where, tally);
            if (result == null) return;
            var after = engine.Snapshot;
            var contact = FreeTalk.Contact(after, npcId);
            Assert.That(contact.talkWindow, Is.EqualTo(key), where);
            tally.Talks++;
            walk.Talked.Add(npcId + "|" + key);
            bool flirted = answer == TalkRules.Flirt;
            if (flirted)
            {
                tally.Flirts++;
                Assert.That(contact.flirts, Is.EqualTo(Math.Min(TalkRules.MostCount, flirtsBefore + 1)), where + ": a Flirt counts once.");
            }
            Scene(s, after, npcId, stance, result, walk, tally, where);
            Record(after, npcId, result, walk, tally);

            // The cap: the same housemate again in this window is refused and moves nothing (a scene left open is the only way on).
            Assert.That(FreeTalk.Offer(after, npcId).Refusal, Is.EqualTo(TalkRefusal.TalkedThisWindow), where);
            string json = JsonConvert.SerializeObject(after);
            var again = engine.Apply(FreeTalkS3Fixture.Command(after, npcId, TalkRomance.CommandText(talk.Id, tones.FirstOrDefault() ?? TalkRules.Warm)));
            Assert.That(again.accepted, Is.False, where + ": a second counted talk was accepted.");
            Assert.That(JsonConvert.SerializeObject(engine.Snapshot), Is.EqualTo(json), where + ": a refused talk moved the season.");

            // A scene left open: the arm answers it (not counted) or lets it lapse.
            var open = result.Choices ?? new List<TalkChoice>();
            var pick = Continuation(arm, open, walk, TalkRomance.PartnerOf(after) != null);
            if (pick.HasValue && !string.IsNullOrEmpty(result.SceneTalkId))
            {
                var cont = Say(engine, npcId, TalkRomance.CommandText(result.SceneTalkId, TalkChoices.Answer(pick.Value)), where + " (continuation " + pick.Value + ")", tally);
                if (cont != null)
                {
                    Assert.That(cont.Continuation, Is.True, where + ": a choice on an open scene is a continuation.");
                    Assert.That(FreeTalk.Contact(engine.Snapshot, npcId).talks, Is.EqualTo(contact.talks), where + ": a continuation is not counted.");
                    tally.Continuations++;
                    Record(engine.Snapshot, npcId, cont, walk, tally);
                }
            }
            NoHereToPlayBond(engine.Snapshot, where);
        }

        /// <summary>The once-only scenes and the undecided pace, checked on the Flirt's result.</summary>
        private static void Scene(EpisodeState before, EpisodeState after, string npcId, RomanceStance stance, TalkResult result, Walk walk, Tally tally, string where)
        {
            switch (result.Scene)
            {
                case RomanceScene.HoldBack:
                    Assert.That(stance, Is.EqualTo(RomanceStance.Undecided), where);
                    Assert.That(Inc(walk.HoldBacks, npcId), Is.EqualTo(1), where + ": the hold-back played twice.");
                    Assert.That(result.SparkAfter, Is.EqualTo(result.SparkBefore), where + ": no spark in the hold-back's window.");
                    tally.HoldBacks++;
                    break;
                case RomanceScene.LineDrawn:
                    Assert.That(Inc(walk.LinesDrawn, npcId), Is.EqualTo(1), where + ": the line drawn twice.");
                    tally.LinesDrawn++;
                    break;
                case RomanceScene.TurnAside:
                    Assert.That(stance, Is.EqualTo(RomanceStance.HereToPlay), where);
                    int last = walk.LastAttempt.TryGetValue(npcId, out int seen) ? seen : 0;
                    Assert.That(result.Attempt, Is.EqualTo(last + 1), where + ": turn-aside attempts in order, each once.");
                    Assert.That(result.Attempt, Is.InRange(1, 3), where);
                    walk.LastAttempt[npcId] = result.Attempt;
                    tally.TurnAsides++;
                    if (result.Attempt == 3)
                    {
                        tally.TurnAsidesToThree++;
                        Assert.That(FreeTalk.Contact(after, npcId).talkFlags, Does.Contain(TalkRules.FlirtRemoved), where + ": attempt 3 takes Flirt away.");
                    }
                    break;
                case RomanceScene.SoftNo:
                    tally.SoftNos++;
                    break;
                case RomanceScene.InKind:
                    tally.InKinds++;
                    walk.MostSpark[npcId] = Math.Max(walk.MostSpark.TryGetValue(npcId, out int most) ? most : 0, result.SparkAfter);
                    if (result.SparkAfter >= TalkRomance.ReadySpark)
                    {
                        tally.AtForty++;
                        if (TalkRomance.Ready(after, npcId)) tally.ReadyAtForty++;
                        else
                        {
                            var row = TalkRomance.ReadyRow(TalkLean.PlayerClass(after), FreeTalk.ClassOf(after.Find(npcId)));
                            int warmth = row.warmth - (stance == RomanceStance.Strategic ? 10 : 0);
                            if (StoryPeople.Mutual(after, npcId, after.playerId) < warmth) tally.LackWarmth++;
                            if ((FreeTalk.Contact(after, npcId)?.rapport ?? 0) < TalkRomance.RapportGate(after, npcId, row.rapport)) tally.LackRapport++;
                            if (after.week < TalkRomance.WeekGate(after, npcId, row.week)) tally.LackWeek++;
                        }
                    }
                    if (stance == RomanceStance.Undecided)
                    {
                        // Never ahead of a regular of their class with the same answer: the step at the pace without x0.6. The
                        // pace is read after the command, as the engine reads it: the Flirt's own lean step (U2) can move the
                        // player's class inside the command.
                        double l = result.Landing > 0 ? 1.5 : result.Landing < 0 ? 0.5 : 1.0;
                        double regularPace = TalkRomance.Pace(after, npcId) / TalkRomance.UndecidedPace;
                        Assert.That(result.SparkAfter, Is.EqualTo(Math.Min(TalkRomance.MostSpark, result.SparkBefore + TalkRomance.SparkStep(after, npcId, result.Landing))),
                            where + ": the step is the engine's own at this pace.");
                        int regular = Math.Min(TalkRomance.MostSparkStep, (int)Math.Round(5 * regularPace * l + 1e-9, MidpointRounding.AwayFromZero));
                        Assert.That(result.SparkAfter - result.SparkBefore, Is.LessThanOrEqualTo(regular), where + ": undecided ahead of a regular.");
                    }
                    break;
            }
        }

        private static int Inc(Dictionary<string, int> map, string key) => map[key] = (map.TryGetValue(key, out int n) ? n : 0) + 1;

        private static void Record(EpisodeState s, string npcId, TalkResult result, Walk walk, Tally tally)
        {
            switch (result.Change)
            {
                case RomanceChange.Couple:
                    tally.Couples++;
                    if (walk.FirstCoupleWeek == 0) walk.FirstCoupleWeek = s.week;
                    string pair = TalkLean.PlayerClass(s) + "+" + FreeTalk.ClassOf(s.Find(npcId));
                    if (!tally.CoupleWeekByPair.TryGetValue(pair, out var weeks)) tally.CoupleWeekByPair[pair] = weeks = new List<int>();
                    weeks.Add(s.week);
                    Assert.That(Bonds.Holds(s, s.playerId, npcId, BondKinds.Showmance), Is.True, "A couple is the showmance bond.");
                    break;
                case RomanceChange.NotYet: tally.NotYets++; break;
                case RomanceChange.JustFriends: tally.JustFriends++; break;
                case RomanceChange.Ended: tally.Endings++; break;
                case RomanceChange.SecondDeclaration: tally.SecondDeclarations++; break;
            }
            // Couples the player's own choices ended leave the set here; the rest (the house's, the partner's) are counted in AfterStep.
            foreach (var c in s.story.contacts.Where(c => c != null))
            {
                if (c.romance == TalkRules.Couple) walk.Couples.Add(c.npcId);
                else walk.Couples.Remove(c.npcId);
            }
            if (result.WentPublic) tally.WentPublic++;
        }

        /// <summary>Says a command: the engine's preview, then the command, accepted with the stream byte-equal and the romance fields as previewed; the state validates and reloads.</summary>
        private static TalkResult Say(EpisodeEngine engine, string npcId, string text, string where, Tally tally)
        {
            var s = engine.Snapshot;
            var preview = TalkRomance.Result(s, npcId, text);
            Assert.That(preview, Is.Not.Null, where + ": the preview refused " + text);
            uint stream = s.randomState;
            int revision = s.revision;
            var applied = engine.Apply(FreeTalkS3Fixture.Command(s, npcId, text));
            Assert.That(applied.accepted, Is.True, where + ": " + text + ": " + applied.reason);
            var after = engine.Snapshot;
            Assert.That(after.randomState, Is.EqualTo(stream), where + ": " + text + " drew from the season's stream.");
            Assert.That(after.revision, Is.EqualTo(revision + 1), where + ": one command.");
            var c = FreeTalk.Contact(after, npcId);
            Assert.That(c.spark, Is.EqualTo(preview.SparkAfter), where + ": spark as previewed.");
            Assert.That(c.romance, Is.EqualTo(preview.RomanceAfter), where + ": romance as previewed.");
            Bounds(c, where);
            FreeTalkS3Fixture.ValidAndReloads(after, where + " " + text);
            return preview;
        }
    }
}
#endif
