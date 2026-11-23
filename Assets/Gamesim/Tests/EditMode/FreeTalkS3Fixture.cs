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
    /// Free talk slice 3's fixtures (talk rules 3): romance rows of the tests' own for each class pool and for every All-Stars
    /// template (all twelve alike), states set directly, the command's text, and the anchor's romance pass. Never the shipped
    /// tables. Simulation only, so the Unity-free subset runs it.
    /// </summary>
    public static class FreeTalkS3Fixture
    {
        public static readonly string[] ClassPools = { TalkPools.Spark, TalkPools.Steady, TalkPools.Guarded };

        private static TalkReply Choice(TalkChoice choice) =>
            new TalkReply { Choice = choice, Line = "\"" + TalkChoices.Caption(choice) + ".\"", Lands = "\"All right.\"" };

        private static TalkReply Tone(string tone) =>
            new TalkReply { Tone = tone, Line = "\"Say it " + tone + ".\"", Lands = "\"Good.\"", Even = "\"Fine.\"", Grates = "\"No.\"" };

        private static TalkReply[] Tones(params TalkReply[] extra) =>
            new[] { Tone(TalkRules.Warm), Tone(TalkRules.Straight), Tone(TalkRules.Tease) }.Concat(extra).ToArray();

        /// <summary>Every romance kind, once (a turn-aside in each attempt, both reactions), for a class pool or a template's own table.</summary>
        public static List<TalkRow> RomanceRows(string pool, string owner)
        {
            string p = (owner ?? pool) + ".rt.";
            TalkRow Row(string name, TalkKind kind, string open, params TalkReply[] replies) => new TalkRow
            {
                Id = p + name, Pool = owner == null ? pool : null, Owner = owner, Kind = kind, Open = open,
                Replies = replies ?? Array.Empty<TalkReply>(),
            };
            var rows = new List<TalkRow>
            {
                Row("flirt", TalkKind.FlirtLine, null,
                    new TalkReply { Tone = TalkRules.Flirt, Line = "\"Save me a seat.\"", Lands = "\"Already did.\"", Even = "\"Maybe.\"", Grates = "\"Easy.\"" }),
                Row("open", TalkKind.Opening, "\"You again.\"", Tones(Tone(TalkRules.Flirt), Choice(TalkChoice.JustFriends))),
                Row("softno", TalkKind.SoftNo, "\"I like you. Not like that.\""),
                Row("hold", TalkKind.HoldBack, "\"Let's see where this game takes us.\""),
                Row("line", TalkKind.LineDrawn, "\"Friends. That's the line.\""),
                Row("accepts", TalkKind.Accepts, "\"Yes.\""),
                Row("refuses", TalkKind.Refuses, "\"Not like that.\""),
                Row("declare", TalkKind.Declaration, "\"Are we doing this?\"",
                    Choice(TalkChoice.Accept), Choice(TalkChoice.NotYet), Choice(TalkChoice.NotLikeThat), Choice(TalkChoice.JustFriends)),
                Row("couple", TalkKind.Couple, "\"Us.\"", Tones(Choice(TalkChoice.EndItKindly))),
                Row("ex", TalkKind.Ex, "\"Awkward.\"", Tones()),
                Row("jealous", TalkKind.Jealousy, "\"I heard.\"", Tones(Choice(TalkChoice.EndItKindly))),
            };
            var turn1 = Row("turn1", TalkKind.TurnAside, "\"Was that a line?\"",
                Choice(TalkChoice.LaughItOff), Choice(TalkChoice.PushAgain), Choice(TalkChoice.Apologize));
            turn1.Attempt = 1; turn1.Second = "\"I'm here to play.\"";
            var turn2 = Row("turn2", TalkKind.TurnAside, "\"Still here to play.\""); turn2.Attempt = 2;
            var turn3 = Row("turn3", TalkKind.TurnAside, "\"Game only.\""); turn3.Attempt = 3;
            var react1 = Row("react1", TalkKind.Reaction, "\"You two aren't subtle.\"", Tones()); react1.Reaction = 1;
            var react2 = Row("react2", TalkKind.Reaction, "\"Careful. A pair is a target.\"", Tones()); react2.Reaction = 2;
            rows.AddRange(new[] { turn1, turn2, turn3, react1, react2 });
            return rows;
        }

        /// <summary>The romance rows of the three class pools and of every All-Stars template.</summary>
        public static List<TalkRow> Romance() =>
            ClassPools.SelectMany(p => RomanceRows(p, null))
                .Concat(CastTemplates.In(CastTemplates.Roster.AllStars).SelectMany(t => RomanceRows(null, t.Id))).ToList();

        /// <summary>Slice 1's small table with every romance row (no stage talks: a stage talk carries no Flirt answer).</summary>
        public static List<TalkRow> Table() => FreeTalkFixture.Table().Concat(Romance()).ToList();

        /// <summary>A fresh season of eight on move-in night at talk rules 3.</summary>
        public static EpisodeState Fresh(uint seed = 3111u) => FreeTalkFixture.Fresh(TalkRules.Romance, seed: seed);

        /// <summary>The player's class and a housemate's, by the traits slice 2's fixture names.</summary>
        public static void Classes(EpisodeState s, string npcId, string playerPool, string npcPool)
        {
            FreeTalkS2Fixture.ClassAs(s, s.playerId, playerPool);
            FreeTalkS2Fixture.ClassAs(s, npcId, npcPool);
            Assert.That(TalkLean.PlayerClass(s), Is.EqualTo(playerPool), "The control: the player's class.");
        }

        /// <summary>The first housemate of <paramref name="pool"/>'s class (made so) whose stance is open or strategic and who is interested, or null.</summary>
        public static string Interested(EpisodeState s, string playerPool, string pool, Func<ContestantState, bool> filter = null)
        {
            foreach (var npc in FreeTalkFixture.Npcs(s))
            {
                if (filter != null && !filter(npc) || FreeTalk.IsAllStar(npc)) continue;
                var stance = TalkRomance.StanceOf(s, npc.id);
                if (stance != RomanceStance.Open && stance != RomanceStance.Strategic) continue;
                var traits = npc.traits;
                Classes(s, npc.id, playerPool, pool);
                FreeTalkS2Fixture.Views(s, npc.id, 50, 50);
                if (TalkRomance.Interested(s, npc.id)) return npc.id;
                npc.traits = traits;
            }
            return null;
        }

        private static readonly Dictionary<string, (EpisodeState state, string npc)> Walked = new Dictionary<string, (EpisodeState state, string npc)>();

        /// <summary>
        /// A season (walked to the first window of <paramref name="week"/> when it is past one) with a housemate of the class asked
        /// for who is interested in the player: the first of a fixed run of seeds that has one. A walked season is kept and handed
        /// out as a copy.
        /// </summary>
        public static EpisodeState WithInterested(string playerPool, string pool, out string npcId, int week = 1)
        {
            string key = playerPool + "|" + pool + "|" + week;
            if (Walked.TryGetValue(key, out var kept))
            {
                var copy = kept.state.Clone();
                npcId = kept.npc;
                return copy;
            }
            for (uint seed = week <= 1 ? 3111u : 3201u; seed < (week <= 1 ? 3141u : 3261u); seed++)
            {
                EpisodeState s;
                if (week <= 1) s = Fresh(seed);
                else
                {
                    var engine = new EpisodeEngine(Fresh(seed));
                    bool PlayerOut(EpisodeState x) => x.Find(x.playerId).status != ContestantStatus.Active;
                    FreeTalkFixture.WalkUntil(engine, x => PlayerOut(x) || x.week >= week && FreeTalk.WindowKey(x) != 0 || x.phase == EpisodePhase.Finished);
                    s = engine.Snapshot;
                    if (PlayerOut(s) || s.week < week || FreeTalk.WindowKey(s) == 0) continue;
                }
                npcId = Interested(s, playerPool, pool);
                if (npcId == null) continue;
                if (week > 1) Walked[key] = (s.Clone(), npcId);
                return s;
            }
            Assert.Fail("No season had an interested " + pool + " housemate for a " + playerPool + " player.");
            npcId = null;
            return null;
        }

        /// <summary>A romance with a housemate set directly: flirting, the spark, rapport, both views, the player's Flirts.</summary>
        public static ContactState Flirting(EpisodeState s, string npcId, int spark, int rapport, int flirts = 1, double views = 50)
        {
            var c = FreeTalkFixture.ContactOf(s, npcId);
            c.romance = TalkRules.Flirting;
            c.spark = spark;
            c.rapport = rapport;
            c.flirts = flirts;
            FreeTalkS2Fixture.Views(s, npcId, views, views);
            return c;
        }

        /// <summary>The first All-Stars template the stance table reads as <paramref name="stance"/>.</summary>
        public static string TemplateWith(RomanceStance stance)
        {
            var id = CastTemplates.In(CastTemplates.Roster.AllStars).Select(t => t.Id).FirstOrDefault(t => TalkRomance.StanceOfTemplate(t) == stance);
            Assert.That(id, Is.Not.Null, "The control: a template with the stance " + stance + ".");
            return id;
        }

        /// <summary>A housemate's card made from an All-Stars template (a renamed card: the name is not the template's).</summary>
        public static void AsTemplate(EpisodeState s, string npcId, string templateId)
        {
            s.Find(npcId).sourceTemplateId = templateId;
            Assert.That(FreeTalk.IsAllStar(s.Find(npcId)), Is.True, "The control: an All-Stars template.");
        }

        public static EpisodeCommand Command(EpisodeState s, string npcId, string text) => new EpisodeCommand
        {
            id = "s3-" + s.revision + "-" + npcId + "-" + text.Length, actorId = s.playerId, expectedRevision = s.revision, expectedPhase = s.phase,
            kind = EpisodeCommandKind.JustTalk, targetId = npcId, text = text,
        };

        /// <summary>
        /// Says <paramref name="text"/> to a housemate: the engine's preview, then the command, which must be accepted, must leave the
        /// season's stream byte-equal and must give exactly the previewed romance fields. Returns the preview.
        /// </summary>
        public static TalkResult Say(EpisodeEngine engine, string npcId, string text)
        {
            var s = engine.Snapshot;
            var preview = TalkRomance.Result(s, npcId, text);
            Assert.That(preview, Is.Not.Null, "The preview refused " + text);
            var result = engine.Apply(Command(s, npcId, text));
            Assert.That(result.accepted, Is.True, text + ": " + result.reason);
            Assert.That(result.state.randomState, Is.EqualTo(s.randomState), text + ": the season's stream moved.");
            var c = FreeTalk.Contact(result.state, npcId);
            Assert.That(c.spark, Is.EqualTo(preview.SparkAfter), text + ": spark as previewed.");
            Assert.That(c.romance, Is.EqualTo(preview.RomanceAfter), text + ": romance as previewed.");
            ValidAndReloads(result.state, text);
            return preview;
        }

        /// <summary>A command the engine refuses, leaving the season as it was.</summary>
        public static void Refused(EpisodeEngine engine, string npcId, string text)
        {
            var s = engine.Snapshot;
            string json = JsonConvert.SerializeObject(s);
            Assert.That(TalkRomance.Result(s, npcId, text), Is.Null, text + ": the preview should refuse.");
            Assert.That(engine.Apply(Command(s, npcId, text)).accepted, Is.False, text + ": accepted.");
            Assert.That(JsonConvert.SerializeObject(engine.Snapshot), Is.EqualTo(json), text + ": a refusal moved the season.");
        }

        /// <summary>A new window with a housemate: the state with their window cap cleared, in a new engine.</summary>
        public static EpisodeEngine NextWindow(EpisodeEngine engine, string npcId)
        {
            var s = engine.Snapshot;
            var c = FreeTalk.Contact(s, npcId);
            c.talkWindow = 0;
            c.talkPending = "";
            return new EpisodeEngine(s);
        }

        /// <summary>The anchor's romance pass (EpisodeEngine.RomanceAtAnchor), on the state itself.</summary>
        public static void Anchor(EpisodeState s, string anchor) =>
            typeof(EpisodeEngine).GetMethod("RomanceAtAnchor", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { s, anchor });

        /// <summary>The player's partner named on the block (EpisodeEngine.PartnerNamed), on the state itself.</summary>
        public static void PartnerNamed(EpisodeState s, params string[] ids) =>
            typeof(EpisodeEngine).GetMethod("PartnerNamed", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { s, ids });

        private static readonly JsonSerializerSettings Reload = new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace };

        /// <summary>The state validates, and a save and reload of it (its JSON read back into a new engine) is the same state.</summary>
        public static void ValidAndReloads(EpisodeState s, string where)
        {
            Assert.That(EpisodeValidation.TryValidate(s, out string reason), Is.True, where + ": " + reason);
            string json = JsonConvert.SerializeObject(s);
            var loaded = JsonConvert.DeserializeObject<EpisodeState>(json, Reload);
            Assert.That(EpisodeValidation.TryValidate(loaded, out reason), Is.True, where + " (reloaded): " + reason);
            Assert.That(JsonConvert.SerializeObject(new EpisodeEngine(loaded).Snapshot), Is.EqualTo(json), where + ": the reload is not the same state.");
        }
    }
}
