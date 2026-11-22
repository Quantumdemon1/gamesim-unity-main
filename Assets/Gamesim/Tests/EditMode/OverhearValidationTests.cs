using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// What a season may hold of overhearing's records (plan 32 §I and §L, S2a; EpisodeValidation.HouseTalk.cs): below story
    /// rules 12 a <c>Seen</c> standing, a caught Listen's <c>Missed</c> standing between two houseguests, a hunch line and a
    /// <c>heard:</c> or <c>pact-heard:</c> key are refused outright; at 12 a <c>Seen</c> row is a standing between two
    /// houseguests in a tone, never a vote claim, the hunch line is the player's alone, and each key names a conversation the
    /// season has numbered or a week it has reached. Pure simulation, so the Unity-free subset runs it.
    /// </summary>
    public sealed class OverhearValidationTests
    {
        private const string BelowOverheard = "A season below the house talk's overhearing has none of its records.";
        private const string Invalid = "Invalid overheard records.";

        /// <summary>A season at <paramref name="rules"/> with one NPC conversation numbered (so <c>heard:1</c> names one), and one record added.</summary>
        private static EpisodeState With(int rules, string record)
        {
            var s = OverhearTests.Talking(rules, "casual");
            var npcs = s.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();
            void Standing(string from, string to, string source, double score) =>
                s.ledger.standings.Add(new StandingRow { week = s.week, fromId = from, toId = to, source = source, score = score });
            void Line(params string[] audience) => s.events.Add(new EpisodeEvent { sequence = s.nextSequence++, week = s.week, phase = s.phase,
                kind = EpisodeEngine.HunchEventKind, text = "You noticed them.", audienceIds = audience.ToList() });
            void Key(string key) => s.story.cooldowns.Add(new StoryCooldownState { key = key, untilWeek = s.week + 1 });
            switch (record)
            {
                case "seen": Standing(npcs[0], npcs[1], ClaimSource.Seen, HunchTone.Scheming); break;
                case "caught": Standing(npcs[0], npcs[1], ClaimSource.Missed, 0); break;
                case "hunch line": Line(s.playerId); break;
                case "heard": Key(EpisodeEngine.HeardPrefix + "1"); break;
                case "pact-heard": Key(EpisodeEngine.PactHeardPrefix + s.week); break;
                case "seen claim": s.ledger.claims.Add(new ClaimRow { week = s.week, voterId = npcs[0], targetId = npcs[1], source = ClaimSource.Seen }); break;
                case "seen of the player": Standing(npcs[0], s.playerId, ClaimSource.Seen, HunchTone.Warm); break;
                case "seen by the player": Standing(s.playerId, npcs[0], ClaimSource.Seen, HunchTone.Warm); break;
                case "seen tone 0": Standing(npcs[0], npcs[1], ClaimSource.Seen, 0); break;
                case "seen tone 4": Standing(npcs[0], npcs[1], ClaimSource.Seen, 4); break;
                case "seen tone 1.5": Standing(npcs[0], npcs[1], ClaimSource.Seen, 1.5); break;
                case "hunch line overheard by another": Line(s.playerId, npcs[0]); break;
                case "heard:0": Key(EpisodeEngine.HeardPrefix + "0"); break;
                case "heard:x": Key(EpisodeEngine.HeardPrefix + "x"); break;
                case "heard unnumbered": Key(EpisodeEngine.HeardPrefix + s.npcSocial.nextConversationSequence); break;
                case "pact-heard:0": Key(EpisodeEngine.PactHeardPrefix + "0"); break;
                case "pact-heard next week": Key(EpisodeEngine.PactHeardPrefix + (s.week + 1)); break;
                case "pact-heard:x": Key(EpisodeEngine.PactHeardPrefix + "x"); break;
                default: Assert.Fail("No such record: " + record); break;
            }
            return s;
        }

        [TestCase("seen")]
        [TestCase("caught")]
        [TestCase("hunch line")]
        [TestCase("heard")]
        [TestCase("pact-heard")]
        public void BelowTwelveEveryRecordIsRefused(string record)
        {
            var s = With(StoryRules.Roles, record);
            Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.False, record);
            Assert.That(error, Is.EqualTo(BelowOverheard));
        }

        [TestCase("seen claim")]
        [TestCase("seen of the player")]
        [TestCase("seen by the player")]
        [TestCase("seen tone 0")]
        [TestCase("seen tone 4")]
        [TestCase("seen tone 1.5")]
        [TestCase("hunch line overheard by another")]
        [TestCase("heard:0")]
        [TestCase("heard:x")]
        [TestCase("heard unnumbered")]
        [TestCase("pact-heard:0")]
        [TestCase("pact-heard next week")]
        [TestCase("pact-heard:x")]
        public void AtTwelveAMalformedRecordIsRefused(string record)
        {
            var s = With(StoryRules.Manners, record);
            Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.False, record);
            Assert.That(error, Is.EqualTo(Invalid));
        }

        [TestCase("seen")]
        [TestCase("caught")]
        [TestCase("hunch line")]
        [TestCase("heard")]
        [TestCase("pact-heard")]
        public void AtTwelveAWellFormedRecordIsAccepted(string record)
        {
            var s = With(StoryRules.Manners, record);
            Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);
        }

        /// <summary>A read's miss points at the player, as it always has: no level holds it back.</summary>
        [Test]
        public void AReadsMissIsNoOverheardRecordBelowTwelve()
        {
            var s = OverhearTests.Talking(StoryRules.Roles, "casual");
            string npc = s.Active.First(c => !c.isPlayer).id;
            s.ledger.standings.Add(new StandingRow { week = s.week, fromId = npc, toId = s.playerId, source = ClaimSource.Missed, score = 0 });
            Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);
        }
    }
}
