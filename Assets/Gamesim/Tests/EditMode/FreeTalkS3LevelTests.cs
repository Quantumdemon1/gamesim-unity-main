using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Free talk slice 3's bounds (talk rules 3, ruling U1 and the lead's T9): below level 3 nothing romantic is read, written or
    /// shown and the talk on offer is the one it was; validation refuses slice 3's fields below it; and at level 3 the Friends
    /// named are read before a command's closing beats, on both paths, with the same draws. Unity-free.
    /// </summary>
    public sealed class FreeTalkS3LevelTests
    {
        private static bool RomanceAtDefaults(ContactState c) =>
            c.talkPending == "" && c.cool == 0 && c.spark == 0 && c.romance == "" && c.romanceWeek == 0 && c.romanceClosedUntil == 0
            && c.flirts == 0 && c.notYets == 0 && c.turnAsides == 0 && c.coupleAnchors == 0
            && !c.talkFlags.Any(f => f != TalkRules.FavourKept && f != TalkRules.FavourRefused);

        [TestCase(TalkRules.Off)]
        [TestCase(TalkRules.Talk)]
        [TestCase(TalkRules.Chemistry)]
        public void BelowLevelThreeNothingRomanticIsReadWrittenOrShown(int level)
        {
            var s = FreeTalkFixture.Fresh(level);
            foreach (var npc in FreeTalkFixture.Npcs(s))
            {
                FreeTalkFixture.ContactOf(s, npc.id).rapport = 30;
                FreeTalkS2Fixture.Views(s, npc.id, 70, 70);
            }
            var plain = new Dictionary<string, string>();
            using (TalkBank.Use(FreeTalkFixture.Table()))
                foreach (var npc in FreeTalkFixture.Npcs(s)) plain[npc.id] = FreeTalk.Offer(s, npc.id).Talk?.Id;
            using (TalkBank.Use(FreeTalkS3Fixture.Table()))
            {
                foreach (var npc in FreeTalkFixture.Npcs(s))
                {
                    Assert.That(FreeTalk.Offer(s, npc.id).Talk?.Id, Is.EqualTo(plain[npc.id]), npc.id + ": the romance rows change no offer.");
                    Assert.That(TalkRomance.FlirtOpen(s, npc.id), Is.False, npc.id);
                    Assert.That(TalkRomance.DueTalk(s, npc.id), Is.Null, npc.id);
                    Assert.That(TalkRomance.Words(s, npc.id), Is.Null, npc.id);
                    Assert.That(TalkRomanceNotes.PageLine(s, npc.id), Is.Null, npc.id);
                    Assert.That(TalkRomanceNotes.ProfileNotes(s, npc.id), Is.Empty, npc.id);
                    Assert.That(TalkRomance.Result(s, npc.id, TalkRomance.CommandText("x", TalkRules.Flirt)), Is.Null, npc.id);
                }
                string json = JsonConvert.SerializeObject(s);
                FreeTalkS3Fixture.Anchor(s, StoryAnchors.EvictionNight);
                Assert.That(JsonConvert.SerializeObject(s), Is.EqualTo(json), "The anchor's romance pass writes nothing.");
                if (level == TalkRules.Off) return;

                var engine = new EpisodeEngine(s);
                var npcId = FreeTalkFixture.Npcs(s)[0].id;
                var offer = FreeTalk.Offer(engine.Snapshot, npcId);
                FreeTalkS3Fixture.Refused(engine, npcId, TalkRomance.CommandText(offer.Talk.Id, TalkRules.Flirt));
                FreeTalkS3Fixture.Refused(engine, npcId, TalkRomance.CommandText(offer.Talk.Id, TalkRules.Warm, FreeTalkFixture.Npcs(s)[1].id));
                FreeTalkS3Fixture.Refused(engine, npcId, TalkRomance.CommandText(offer.Talk.Id, TalkChoices.Answer(TalkChoice.JustFriends)));
                foreach (var npc in FreeTalkFixture.Npcs(s))
                {
                    var o = FreeTalk.Offer(engine.Snapshot, npc.id);
                    if (!o.Available) continue;
                    var result = engine.Apply(FreeTalkFixture.Command(engine.Snapshot, npc.id, o.Talk.Id, o.Talk.Replies[0].Tone));
                    Assert.That(result.accepted, Is.True, npc.id + ": " + result.reason);
                }
                Assert.That(engine.Snapshot.story.contacts.All(RomanceAtDefaults), Is.True, "No slice 3 field is written below level 3.");
            }
        }

        [Test]
        public void ValidationRefusesSliceThreesFieldsBelowLevelThreeOnly()
        {
            var s = FreeTalkFixture.Fresh(TalkRules.Chemistry);
            string npc = FreeTalkFixture.Npcs(s)[0].id;
            FreeTalkS2Fixture.AssertValid(s);
            var c = FreeTalkFixture.ContactOf(s, npc);
            foreach (Action<ContactState> write in new Action<ContactState>[]
                     {
                         x => x.spark = 5, x => x.romance = TalkRules.Flirting, x => x.flirts = 1, x => x.turnAsides = 1, x => x.cool = 1,
                         x => x.romanceClosedUntil = 2, x => x.talkPending = "x", x => x.talkFlags.Add(TalkRules.Pushy),
                     })
            {
                var at = s.Clone();
                write(FreeTalk.Contact(at, npc) ?? FreeTalkFixture.ContactOf(at, npc));
                Assert.That(EpisodeValidation.TryValidate(at, out _), Is.False, "Refused at talk rules 2.");
                at.story.talkRules = TalkRules.Romance;
                Assert.That(EpisodeValidation.TryValidate(at, out string why), Is.True, "Valid at talk rules 3: " + why);
            }
        }

        // ------------------------------------------------------------------ T9: the Friends named, read before the closing beats

        /// <summary>Kindred, rapport 8, the player's view 40: a Friend while their view of the player is 35 or more.</summary>
        private static void AlmostFriend(EpisodeState s, string npcId, double theirView)
        {
            FreeTalkS2Fixture.Kindred(s, npcId).rapport = 8;
            FreeTalkS2Fixture.Views(s, npcId, theirView, 40);
        }

        private static bool Friend(EpisodeState s, string npcId) => TalkFriendship.AtLeast(s, npcId, FriendStage.Friend);

        /// <summary>
        /// A state where the house's closing beats (EpisodeEngine.Close) move one candidate's view of the player across the Friend
        /// stage's 35: down (<paramref name="cancels"/>) or up. The candidate is set just across from where the beats take them.
        /// </summary>
        private static bool Across(EpisodeState at, IEnumerable<string> candidates, bool cancels, out string friend)
        {
            foreach (var id in candidates)
            {
                var probe = at.Clone();
                AlmostFriend(probe, id, 35);
                double before = probe.Score(id, probe.playerId);
                EpisodeEngine.Close(probe);
                double moved = probe.Score(id, probe.playerId) - before;
                if (cancels ? moved >= -0.5 : moved <= 0.5) continue;
                var set = at.Clone();
                AlmostFriend(set, id, 35 - moved / 2);
                var closed = set.Clone();
                EpisodeEngine.Close(closed);
                if (Friend(set, id) == cancels && Friend(closed, id) != cancels)
                {
                    AlmostFriend(at, id, 35 - moved / 2);
                    friend = id;
                    return true;
                }
            }
            friend = null;
            return false;
        }

        private static EpisodeState AtTalkRules(EpisodeState s, int level)
        {
            var copy = s.Clone();
            copy.story.talkRules = level;
            return copy;
        }

        [TestCase(true)]
        [TestCase(false)]
        public void AtLevelThreeABeatInsideTheNominationNeitherCancelsNorAddsAFriendsCost(bool cancels)
        {
            for (uint seed = 3201u; seed < 3321u; seed++)
            {
                var engine = new EpisodeEngine(FreeTalkFixture.Fresh(TalkRules.Romance, seed: seed));
                FreeTalkFixture.WalkUntil(engine, x => x.phase == EpisodePhase.Nomination && x.nominees.Count == 0 && x.hohId == x.playerId
                    || x.week > 4 || x.Find(x.playerId).status != ContestantStatus.Active || x.phase == EpisodePhase.Finished);
                var s = engine.Snapshot;
                if (!(s.phase == EpisodePhase.Nomination && s.nominees.Count == 0 && s.hohId == s.playerId)) continue;
                var pool = EpisodeEngine.NominationCandidates(s).Select(c => c.id).ToList();
                if (!Across(s, pool, cancels, out string friend)) continue;
                string other = pool.First(id => id != friend);
                FreeTalkS2Fixture.Views(s, other, 20, 20);
                var states = new[] { TalkRules.Chemistry, TalkRules.Romance }.Select(level =>
                {
                    var at = AtTalkRules(s, level);
                    var command = EpisodeEngineTests.Command(at, EpisodeCommandKind.Nominate);
                    command.targetId = friend; command.secondTargetId = other;
                    var result = new EpisodeEngine(at).Apply(command);
                    Assert.That(result.accepted, Is.True, "Talk rules " + level + ": " + result.reason);
                    return result.state;
                }).ToList();
                Assert.That(states[1].randomState, Is.EqualTo(states[0].randomState), "The nomination draws what it drew.");
                double extra = states[0].Score(friend, s.playerId) - states[1].Score(friend, s.playerId);
                if (cancels) Assert.That(extra, Is.InRange(6.0, 10.0), "A Friend as the player saw them pays the 8 at level 3, though a beat took them below.");
                else Assert.That(extra, Is.InRange(-10.0, -6.0), "Not a Friend as the player saw them: level 3 adds no cost though a beat made them one.");
                return;
            }
            Assert.Fail("No seed's closing beats moved a candidate across the Friend stage " + (cancels ? "down" : "up") + ".");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void AtLevelThreeABeatInsideTheVetoMeetingNeitherCancelsNorAddsTheReplacementsCost(bool cancels)
        {
            for (uint seed = 3201u; seed < 3401u; seed++)
            {
                var engine = new EpisodeEngine(FreeTalkFixture.Fresh(TalkRules.Romance, seed: seed));
                FreeTalkFixture.WalkUntil(engine, x => x.phase == EpisodePhase.VetoMeeting && !x.vetoResolved
                    && x.hohId == x.playerId && x.vetoHolderId != x.playerId && EpisodeEngine.NpcVetoSave(x) != null
                    || x.week > 4 || x.Find(x.playerId).status != ContestantStatus.Active || x.phase == EpisodePhase.Finished);
                var s = engine.Snapshot;
                if (!(s.phase == EpisodePhase.VetoMeeting && !s.vetoResolved && s.hohId == s.playerId && s.vetoHolderId != s.playerId)) continue;
                string saved = EpisodeEngine.NpcVetoSave(s);
                if (saved == null) continue;
                var pool = EpisodeEngine.ReplacementCandidates(s).Select(c => c.id).ToList();
                if (!Across(s, pool, cancels, out string friend)) continue;
                if (EpisodeEngine.NpcVetoSave(s) != saved) continue;
                var states = new[] { TalkRules.Chemistry, TalkRules.Romance }.Select(level =>
                {
                    var at = AtTalkRules(s, level);
                    var command = EpisodeEngineTests.Command(at, EpisodeCommandKind.ResolveVeto);
                    command.useVeto = true; command.targetId = saved; command.secondTargetId = friend;
                    var result = new EpisodeEngine(at).Apply(command);
                    Assert.That(result.accepted, Is.True, "Talk rules " + level + ": " + result.reason);
                    return result.state;
                }).ToList();
                Assert.That(states[1].randomState, Is.EqualTo(states[0].randomState), "The veto meeting draws what it drew.");
                double extra = states[0].Score(friend, s.playerId) - states[1].Score(friend, s.playerId);
                if (cancels) Assert.That(extra, Is.InRange(6.0, 10.0), "The Friend the player named pays the 8 at level 3, though a beat took them below.");
                else Assert.That(extra, Is.InRange(-10.0, -6.0), "Not a Friend as the player saw them: level 3 adds no cost.");
                return;
            }
            Assert.Fail("No seed's closing beats moved a replacement candidate across the Friend stage " + (cancels ? "down" : "up") + ".");
        }
    }
}
