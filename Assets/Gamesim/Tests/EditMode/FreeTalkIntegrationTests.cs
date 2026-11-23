using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Free talk slice 1 as it ships (talk rules 1, enrolled): the shipped tables behind the engine, a fresh season the game
    /// starts, the line the third beat shows, how a live season is observed at schema 30 since the enrolment, and a short walk
    /// in which the player takes every free talk on offer (<see cref="Soak"/>; the whole-season soak over many seeds is
    /// FreeTalkSeasonSoak, Unity-free and run by name). Simulation only, so the Unity-free subset runs it too.
    /// </summary>
    public sealed class FreeTalkIntegrationTests
    {
        // ------------------------------------------------------------------ the shipped tables in a fresh season

        [TestCase(CastTemplates.Roster.Regular)]
        [TestCase(CastTemplates.Roster.AllStars)]
        public void AFreshSeasonTheGameStartsOffersEveryHousemateATalkFromTheirOwnPool(CastTemplates.Roster roster)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8, Roster = roster }, 3301u);
            ShippedRules.ApplyFresh(s);
            Assert.That(s.story.talkRules, Is.EqualTo(TalkRules.Romance), "Level 3 is enrolled for fresh seasons (free talk slice 3).");
            Assert.That(FreeTalk.On(s), Is.True);
            Assert.That(FreeTalk.WindowKey(s), Is.EqualTo(1), "Move-in night.");
            foreach (var npc in s.Active.Where(c => !c.isPlayer))
            {
                var offer = FreeTalk.Offer(s, npc.id);
                Assert.That(offer.Available, Is.True, npc.id + ": " + offer.Reason);
                Assert.That(offer.Talk.Pool, Is.EqualTo(FreeTalk.PoolOf(npc)), npc.id + " is offered a talk from their own pool.");
                Assert.That(offer.Talk.Owner, Is.Null, "Slice 1 ships no person's own talks.");
                Assert.That(TalkBank.Find(offer.Talk.Id), Is.SameAs(offer.Talk), "The shipped tables are the bank.");
                if (roster == CastTemplates.Roster.AllStars)
                    Assert.That(offer.Talk.Pool, Is.EqualTo(TalkPools.Star), npc.id + ": an All-Star is offered only the star pool.");
            }
        }

        // ------------------------------------------------------------------ the third beat's "You:" line

        [Test]
        public void TheReplyIsTheLineAsSaidWithItsTokensFilled()
        {
            var rows = TalkPools.All.Select(p => FreeTalkFixture.Row(p, "named", TalkKind.Rhythm, 0, new TalkGate())).ToList();
            foreach (var r in rows) foreach (var reply in r.Replies) reply.Line = "(leaning in) \"Tell me, {name}.\"";
            using (TalkBank.Use(rows))
            {
                var s = FreeTalkFixture.Fresh();
                var npc = FreeTalkFixture.Npcs(s)[0];
                var offer = FreeTalk.Offer(s, npc.id);
                Assert.That(offer.Available, Is.True, offer.Reason);
                var result = FreeTalk.Result(s, npc.id, offer.Talk.Id, offer.Talk.Replies[0].Tone);
                Assert.That(result, Is.Not.Null);
                Assert.That(result.Reply, Is.EqualTo("(leaning in) \"Tell me, " + npc.name + ".\""), "Said with the housemate's name, as its button showed it.");
                Assert.That(TalkText.Spoken(result.Reply), Is.EqualTo(TalkScreenLines.Replies(s, npc.id, offer.Talk)[0].Words),
                    "The third beat's line is the words on the button pressed.");
            }
        }

        // ------------------------------------------------------------------ the schema-30 view since the enrolment

        [Test]
        public void AFreshSeasonAtTheEnrolledLevelWithNoTalkHadHasTheSameSchemaThirtyViewAsAtZero()
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 3302u);
            ShippedRules.ApplyFresh(s);
            Assert.That(s.story.talkRules, Is.EqualTo(TalkRules.Romance), "Level 3 since its enrolment: an unplayed season is the same JSON as at 0.");
            var atZero = s.Clone();
            atZero.story.talkRules = TalkRules.Off;
            var json = JObject.FromObject(s);
            string before = json.ToString(Formatting.None);
            var view = LegacyDigestSchema31Observer.View30(json);
            Assert.That(json.ToString(Formatting.None), Is.EqualTo(before), "The input is never changed.");
            Assert.That(view.ToString(Formatting.None), Is.EqualTo(LegacyDigestSchema31Observer.View30(JObject.FromObject(atZero)).ToString(Formatting.None)),
                "A season nobody talked in is the same schema-30 JSON at level 1 as at 0.");
            // The old-save recorder's projection (OldSaveTrace) takes it down the same way: the same three hashes.
            Assert.That(OldSaveTrace.Checkpoint(s, 30), Is.EqualTo(OldSaveTrace.Checkpoint(atZero, 30)),
                "The recorder's checkpoint at 30 of a season nobody talked in is the same at level 1 as at 0.");
        }

        [Test]
        public void ASeasonWithAFreeTalkHadHasNoSchemaThirtyView()
        {
            var engine = new EpisodeEngine(FreshShipped(3303u, CastTemplates.Roster.Regular));
            var s = engine.Snapshot;
            string npc = FreeTalkFixture.Npcs(s)[0].id;
            var offer = FreeTalk.Offer(s, npc);
            Assert.That(engine.Apply(FreeTalkFixture.Command(s, npc, offer.Talk.Id, offer.Talk.Replies[0].Tone)).accepted, Is.True);
            Assert.Throws<InvalidDataException>(() => LegacyDigestSchema31Observer.View30(JObject.FromObject(engine.Snapshot)),
                "A talk had writes the contact's window and count: refused, never viewed as a season without free talk.");
            Assert.Throws<InvalidDataException>(() => OldSaveTrace.Checkpoint(engine.Snapshot, 30),
                "The recorder's projection refuses it too: the contact's talk fields left their literals.");
        }

        // ------------------------------------------------------------------ a short walk taking every talk on offer

        [TestCase(CastTemplates.Roster.Regular, 3401u)]
        [TestCase(CastTemplates.Roster.AllStars, 3402u)]
        public void TwoWeeksTakingEveryFreeTalkHoldTheCapTheStreamAndTheRealPeopleRule(CastTemplates.Roster roster, uint seed)
        {
            var tally = Soak(seed, roster, 3);
            TestContext.WriteLine(tally.Line(roster + "/" + seed));
            Assert.That(tally.Talks, Is.GreaterThan(0), "The walk had free talks.");
            Assert.That(tally.Windows, Is.GreaterThan(1), "Over more than one window.");
        }

        // ------------------------------------------------------------------ the soak (shared with FreeTalkSeasonSoak)

        /// <summary>A fresh season the game starts, of eight unless said, at its shipped rules (talk rules 1).</summary>
        internal static EpisodeState FreshShipped(uint seed, CastTemplates.Roster roster, int size = 8)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size, Roster = roster }, seed);
            ShippedRules.ApplyFresh(s);
            return s;
        }

        /// <summary>What a soak saw.</summary>
        internal sealed class Tally
        {
            public int Talks, Windows, Repeats, StandingRepeats, Facts, StandingLines, StarTalks, Steps, MostRapport, Week, PlayerInWeek;
            public bool Finished;

            public string Line(string label) =>
                "SOAK " + label + ": talks " + Talks + ", windows " + Windows + ", usual repeats " + Repeats + ", standing repeats "
                + StandingRepeats + ", facts " + Facts + ", standing lines " + StandingLines + ", star-pool talks " + StarTalks
                + ", most rapport " + MostRapport + ", steps " + Steps + ", player in to week " + PlayerInWeek + ", last week " + Week
                + (Finished ? ", finished" : "");
        }

        private static readonly JsonSerializerSettings Reload = new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace };

        /// <summary>
        /// A fresh season at its shipped rules in which the player takes every free talk on offer in every window, until the
        /// season finishes or <paramref name="untilWeek"/> begins (0: to the end). Fails on the first breach of: the command
        /// accepted and applied exactly as FreeTalk.Result previewed it; the season's randomState byte-equal across every
        /// JustTalk; one counted talk per housemate per window (the second refused); no talk id said twice to a housemate except
        /// a repeat of "the usual" or of a standing talk (a weekly snapshot whose names are this week's); the bank never dry
        /// (ruling R9); an All-Star only ever given star-pool talks of layers 1, 2 and 4 (and house-life rhythm), never a fact
        /// past layer 2; every state after a talk or a house step valid, and the same after a save and reload.
        /// </summary>
        internal static Tally Soak(uint seed, CastTemplates.Roster roster, int untilWeek, int size = 8)
        {
            var tally = new Tally();
            var engine = new EpisodeEngine(FreshShipped(seed, roster, size));
            var seen = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var talked = new HashSet<string>(StringComparer.Ordinal);
            int lastKey = -1;
            for (int step = 0; step < 12000; step++)
            {
                var s = engine.Snapshot;
                tally.Week = s.week;
                if (s.phase == EpisodePhase.Finished) { tally.Finished = true; return tally; }
                if (untilWeek > 0 && s.week >= untilWeek) return tally;
                int key = FreeTalk.WindowKey(s);
                bool playerIn = s.Find(s.playerId).status == ContestantStatus.Active;
                if (playerIn) tally.PlayerInWeek = s.week;
                if (key != 0 && playerIn && key != lastKey) { tally.Windows++; lastKey = key; }
                if (key != 0 && playerIn)
                    foreach (var npc in s.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList())
                        TalkOnce(engine, npc, key, seen, talked, tally);
                s = engine.Snapshot;
                if (s.phase == EpisodePhase.Finished) { tally.Finished = true; return tally; }
                var result = engine.Apply(Next(s));
                Assert.That(result.accepted, Is.True, "The house's step: " + result.reason);
                tally.Steps++;
                ValidAndReloads(engine.Snapshot, "after a house step");
            }
            Assert.Fail("The season did not finish within its step budget.");
            return tally;
        }

        /// <summary>The engine tests' next lawful command, answering a finale question with an offered response (as AllWeekCadenceTests' walk does).</summary>
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

        private static void TalkOnce(EpisodeEngine engine, string npcId, int key, Dictionary<string, HashSet<string>> seen,
            HashSet<string> talked, Tally tally)
        {
            var s = engine.Snapshot;
            var npc = s.Find(npcId);
            string where = npc.id + " in window " + key + " (week " + s.week + ")";
            var offer = FreeTalk.Offer(s, npcId);
            if (talked.Contains(npcId + "|" + key))
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
                Assert.That(talk.Pool, Is.EqualTo(TalkPools.Star), where + ": an All-Star is given only the star pool.");
                Assert.That(talk.Layer == 0 || talk.Layer == 1 || talk.Layer == 2 || talk.Layer == 4, Is.True, where + ": layer " + talk.Layer + " of " + talk.Id);
            }
            else Assert.That(talk.Pool, Is.EqualTo(FreeTalk.ClassOf(npc)), where + ": a housemate is given their class's pool.");
            Assert.That(talk.Owner, Is.Null, where + ": slice 1 ships no person's own talks.");
            if (!seen.TryGetValue(npcId, out var ids)) seen[npcId] = ids = new HashSet<string>(StringComparer.Ordinal);
            if (offer.Repeat)
            {
                Assert.That(talk.Usual || talk.Kind == TalkKind.Standing, Is.True, where + ": only 'the usual' or a standing talk comes round again: " + talk.Id);
                Assert.That(ids.Contains(talk.Id), Is.True, where + ": a repeat of a talk never said: " + talk.Id);
                if (talk.Kind == TalkKind.Standing) tally.StandingRepeats++; else tally.Repeats++;
            }
            else Assert.That(ids.Add(talk.Id), Is.True, where + ": " + talk.Id + " was said to them before.");

            // The reply: the tone that lands best on them, every third talk the next in order, so every landing is walked.
            var tones = talk.Replies.Select(r => r.Tone).ToList();
            string tone = tally.Talks % 3 == 2 ? tones[tally.Talks % tones.Count]
                : tones.OrderByDescending(t => FreeTalk.Landing(s, npc, t)).First();
            var preview = FreeTalk.Result(s, npcId, talk.Id, tone);
            Assert.That(preview, Is.Not.Null, where + ": no result for the talk on offer.");

            var before = s.randomState;
            int revision = s.revision;
            var applied = engine.Apply(FreeTalkFixture.Command(s, npcId, talk.Id, tone));
            Assert.That(applied.accepted, Is.True, where + ": " + applied.reason);
            var after = engine.Snapshot;
            Assert.That(after.randomState, Is.EqualTo(before), where + ": a free talk drew from the season's stream.");
            Assert.That(after.revision, Is.EqualTo(revision + 1), where + ": one command.");
            var contact = FreeTalk.Contact(after, npcId);
            Assert.That(contact, Is.Not.Null);
            Assert.That(contact.talkWindow, Is.EqualTo(key), where);
            Assert.That(contact.rapport, Is.EqualTo(preview.RapportAfter), where + ": rapport as previewed.");
            Assert.That(contact.talkTenths, Is.EqualTo(preview.TenthsAfter), where + ": tenths as previewed.");
            if (preview.FactId != null)
            {
                Assert.That(Lore.Knows(after, preview.FactId), Is.True, where + ": the fact previewed is learned.");
                if (star) Assert.That(preview.FactLayer, Is.LessThanOrEqualTo(2), where + ": an All-Star never gives a layer-3 fact.");
                tally.Facts++;
            }
            if (!string.IsNullOrEmpty(preview.StandingLine)) tally.StandingLines++;
            tally.MostRapport = Math.Max(tally.MostRapport, contact.rapport);
            tally.Talks++;
            talked.Add(npcId + "|" + key);

            // The cap: the same housemate again in this window is refused and moves nothing.
            Assert.That(FreeTalk.Offer(after, npcId).Refusal, Is.EqualTo(TalkRefusal.TalkedThisWindow), where);
            string json = JsonConvert.SerializeObject(after);
            var again = engine.Apply(FreeTalkFixture.Command(after, npcId, talk.Id, tone));
            Assert.That(again.accepted, Is.False, where + ": a second counted talk was accepted.");
            Assert.That(JsonConvert.SerializeObject(engine.Snapshot), Is.EqualTo(json), where + ": a refused talk moved the season.");
            ValidAndReloads(after, where);
        }

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
