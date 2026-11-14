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
        private IEnumerator InstallHouseTalkProfile(int rules, string tellOn)
        {
            yield return SettleCast();
            var state = director.Snapshot.Clone();
            Assert.That(EpisodeEngine.StoryOn(state), Is.True, "A shipped season plays the story.");
            state.story.rulesVersion = rules;
            if (tellOn != null)
                state.story.cooldowns.Add(new StoryCooldownState { key = EpisodeEngine.TellPrefix + tellOn + ":" + Manner.Hedged, untilWeek = state.week + EpisodeEngine.TellWeeks });
            Assert.That(EpisodeValidation.TryValidate(state, out var invalid), Is.True, invalid);
            new EpisodeSaveStore(director.SavePath).Save(state);
            yield return ReloadEpisode();
            yield return SettleCast();
            Assert.That(director.Snapshot.story.rulesVersion, Is.EqualTo(rules), "The fixture came back as it was written.");
        }

        [UnityTest]
        public IEnumerator HouseTalkProfile_WhatYouveLearnedDrawsAtTwelveAndNotBelow()
        {
            yield return SettleCast();
            var someone = director.Snapshot.contestants.First(c => !c.isPlayer && c.status == ContestantStatus.Active);
            string block = EpisodeHud.ProfileBlockPrefix + HouseguestNotes.WhatYouveLearnedHeading;

            yield return InstallHouseTalkProfile(StoryRules.Roles, null);
            director.ShowHouseguestProfile(someone.id);
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(director.ProfileId, Is.EqualTo(someone.id));
            Assert.That(ActiveRect(EpisodeHud.ProfileIdentityName), Is.Not.Null);
            Assert.That(GameObject.Find(block), Is.Null, "Below 12 the profile draws as it always did.");

            yield return InstallHouseTalkProfile(StoryRules.Manners, someone.id);
            int revision = director.Snapshot.revision;
            foreach (var size in TextSizes.All)
            {
                yield return ApplyTextSize(size);
                director.ShowHouseguestProfile(someone.id);
                yield return null;
                Canvas.ForceUpdateCanvases();
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
            }
            yield return ApplyTextSize(TextSize.Standard);
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "Reading a profile commits nothing.");
        }
    }
}
