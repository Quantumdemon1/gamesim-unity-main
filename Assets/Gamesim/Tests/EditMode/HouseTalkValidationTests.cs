using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// What a season may hold of the house talk's records (plan 32 §L; EpisodeValidation.HouseTalk.cs): below story rules 12 a
    /// <c>said:</c>, <c>tell:</c>, <c>h2h:</c> or <c>line:</c> cooldown key is refused outright; at 12 each must name a seated
    /// houseguest, a week of the season, a known stance with a manner that fits it (a tell's manner is a tell), a competition's
    /// kind and one of a slot's lines, and an <c>h2h:</c> never names the player. Later slices append their records to the
    /// same validator and their cases here. Pure simulation, so the Unity-free subset runs it.
    /// </summary>
    public sealed class HouseTalkValidationTests
    {
        private const string BelowManners = "A season below the house talk's manners has none of its records.";
        private const string Invalid = "Invalid house talk records.";

        // The key's placeholders: {w} this week, {w+1} next week, {npc} a seated voter, {p} the player.
        private static EpisodeState With(int rules, string template)
        {
            var s = VoteAnswerTests.Campaign(rules);
            string key = template.Replace("{w+1}", (s.week + 1).ToString()).Replace("{w}", s.week.ToString())
                .Replace("{npc}", VoteAnswerTests.Voter(s, 0)).Replace("{p}", s.playerId);
            s.story.cooldowns.Add(new StoryCooldownState { key = key, untilWeek = s.week + 1 });
            return s;
        }

        // ------------------------------------------------------------ S1: manners, candour, tells (story rules 12)

        [TestCase("said:{w}:{npc}:honest:definite")]
        [TestCase("tell:{npc}:hedged")]
        [TestCase("h2h:{w}:hoh:{npc}")]
        [TestCase("line:{w}:{npc}:definite:0")]
        public void BelowTwelveEveryRecordIsRefused(string template)
        {
            var s = With(StoryRules.Roles, template);
            Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.False, template);
            Assert.That(error, Is.EqualTo(BelowManners));
        }

        [TestCase("said:{w}:{npc}:sincere:definite")]
        [TestCase("said:{w}:{npc}:honest:mumbled")]
        [TestCase("said:{w}:{npc}:neutral:definite")]
        [TestCase("said:{w}:{npc}:honest:non-answer")]
        [TestCase("said:0:{npc}:honest:definite")]
        [TestCase("said:{w+1}:{npc}:honest:definite")]
        [TestCase("said:{w}:nobody-seated:honest:definite")]
        [TestCase("said:{w}:{npc}:honest")]
        [TestCase("tell:{npc}:definite")]
        [TestCase("tell:nobody-seated:hedged")]
        [TestCase("h2h:{w}:comp:{npc}")]
        [TestCase("h2h:{w}:hoh:{p}")]
        [TestCase("h2h:{w}:hoh:nobody-seated")]
        [TestCase("h2h:{w+1}:veto:{npc}")]
        [TestCase("line:{w}:{npc}:definite:3")]
        [TestCase("line:{w}:{npc}:mumbled:0")]
        [TestCase("line:{w+1}:{npc}:definite:0")]
        [TestCase("line:{w}:nobody-seated:definite:0")]
        public void AtTwelveAMalformedRecordIsRefused(string template)
        {
            var s = With(StoryRules.Manners, template);
            Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.False, template);
            Assert.That(error, Is.EqualTo(Invalid));
        }

        [TestCase("said:{w}:{npc}:honest:definite")]
        [TestCase("tell:{npc}:hedged")]
        [TestCase("h2h:{w}:hoh:{npc}")]
        public void AtTwelveAWellFormedRecordIsAccepted(string template)
        {
            var s = With(StoryRules.Manners, template);
            Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);
        }
    }
}
