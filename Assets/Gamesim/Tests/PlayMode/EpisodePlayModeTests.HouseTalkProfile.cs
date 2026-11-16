using System;
using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The house talk's blocks on a houseguest's profile (plan 32 §C and §N "Labels draw", S1): in a season at story rules 12
    /// the profile draws "What you've learned" once the reveal has caught the houseguest lying with a tell, as a card of its
    /// own between "What you remember" and "Other records", every label drawing at Standard and at Largest; the same season at
    /// 11 draws no block. Installed through a saved fixture, since no season the game starts reaches 12 before A-ON.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// The fixture's own house playing the story at <paramref name="rules"/>, with whatever <paramref name="also"/> writes
        /// into it, validated, saved, reloaded and held still: the house talk's install, for every slice that needs a season
        /// at 12 or above. The fixture starts the default scene's season, which never plays the story (the director's
        /// StartSeason, through ShippedRules.ApplyFresh, is what switches it on), so the story is switched on here from this
        /// week, as SaveWithTheStoryOn does, and the level is then set by name. The house's own clock is held after the load
        /// (KeepTheBlock's install), so nothing commits under a test that reads the season.
        /// </summary>
        private IEnumerator InstallHouseTalkSeason(int rules, Action<EpisodeState> also = null)
        {
            yield return SettleCast();
            var state = director.Snapshot.Clone();
            if (!EpisodeEngine.StoryOn(state)) EpisodeEngine.EnableStory(state, state.week);
            state.story.rulesVersion = rules;
            also?.Invoke(state);
            Assert.That(EpisodeEngine.StoryAt(state, rules), Is.True, "The fixture plays the story at " + rules + ".");
            Assert.That(EpisodeValidation.TryValidate(state, out var invalid), Is.True, invalid);
            new EpisodeSaveStore(director.SavePath).Save(state);
            yield return ReloadEpisode();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return SettleCast();
            Assert.That(EpisodeEngine.StoryOn(director.Snapshot), Is.True, "The fixture came back playing the story.");
            Assert.That(director.Snapshot.story.rulesVersion, Is.EqualTo(rules), "The fixture came back as it was written.");
        }

        /// <summary>A tell learned on <paramref name="voterId"/>: the reveal's record of a lie told in <paramref name="manner"/>.</summary>
        private static Action<EpisodeState> TellLearned(string voterId, string manner) => state =>
            state.story.cooldowns.Add(new StoryCooldownState { key = EpisodeEngine.TellPrefix + voterId + ":" + manner, untilWeek = state.week + EpisodeEngine.TellWeeks });

        [UnityTest]
        public IEnumerator HouseTalkProfile_WhatYouveLearnedDrawsAtTwelveAndNotBelow()
        {
            yield return SettleCast();
            var someone = director.Snapshot.contestants.First(c => !c.isPlayer && c.status == ContestantStatus.Active);
            string block = EpisodeHud.ProfileBlockPrefix + HouseguestNotes.WhatYouveLearnedHeading;

            yield return InstallHouseTalkSeason(StoryRules.Roles);
            director.ShowHouseguestProfile(someone.id);
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(director.ProfileId, Is.EqualTo(someone.id));
            Assert.That(ActiveRect(EpisodeHud.ProfileIdentityName), Is.Not.Null);
            Assert.That(ActiveRect(block), Is.Null, "Below 12 the profile draws as it always did.");

            yield return InstallHouseTalkSeason(StoryRules.Manners, TellLearned(someone.id, Manner.Hedged));
            Assert.That(EpisodeEngine.TellsOf(director.Snapshot, someone.id), Is.EqualTo(new[] { Manner.Hedged }), "The tell came back as it was written.");
            foreach (var size in TextSizes.All)
            {
                yield return ApplyTextSize(size);
                // Read here, with the house's clock held: what the open itself commits is what this test is about.
                int revision = director.Snapshot.revision;
                director.ShowHouseguestProfile(someone.id);
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(director.ProfileId, Is.EqualTo(someone.id), size + ": the profile is open.");
                var card = ActiveRect(block);
                Assert.That(card, Is.Not.Null, size + ": the block is a card of its own.");
                var words = card.GetComponentsInChildren<TMP_Text>().Select(t => t.text).ToArray();
                Assert.That(words, Has.Some.Contains(HouseAnswers.MannerWords(Manner.Hedged)), size + ": the tell, in words.");
                Assert.That(words.Any(w => System.Text.RegularExpressions.Regex.IsMatch(w, @"\d")), Is.False, size + ": never a number.");
                AssertEveryLabelDraws("the profile's house-talk block at " + size, card);
                foreach (var label in card.GetComponentsInChildren<TMP_Text>())
                {
                    label.ForceMeshUpdate();
                    Assert.That(label.isTextOverflowing, Is.False, size + ": '" + label.text + "' fits its box.");
                }
                Assert.That(director.Snapshot.revision, Is.EqualTo(revision), size + ": reading a profile commits nothing.");
            }
            yield return ApplyTextSize(TextSize.Standard);
        }
    }
}
