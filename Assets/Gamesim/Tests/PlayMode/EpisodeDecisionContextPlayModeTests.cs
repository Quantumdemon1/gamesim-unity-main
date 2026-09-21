using System.Collections;
using System.Collections.Generic;
using System.IO;
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
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator HouseEventContextShowsItsParticipantsAndKeepsTheActualChoiceCommit()
        {
            var fixture=ContentCatalog.Create(7);
            var ids=fixture.contestants.Where(c=>c.id!=fixture.playerId).Take(2).Select(c=>c.id).ToArray();
            fixture.houseEvents.Clear();
            fixture.houseEvents.Add(new HouseEventState
            {
                id="context-event",kind=HouseEventKind.House,week=fixture.week,title="A disagreement at dinner",
                narrative="Two houseguests disagree about the shared kitchen.",involvedIds=ids.ToList(),
                choices=new List<HouseEventChoice>{new HouseEventChoice{label="Listen without taking sides",risk=HouseEventRisk.Low,
                    description="Hear both houseguests out."}}
            });
            Assert.That(EpisodeValidation.TryValidate(fixture,out var reason),Is.True,reason);
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            yield return ReloadEpisode();
            yield return ApplyTextSize(true);
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(),Is.True);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var before=director.Snapshot;
            var context=DecisionUiRoot(EpisodeHud.HouseEventContextName);
            Assert.That(context,Is.Not.Null);
            Assert.That(context.GetComponentsInChildren<CharacterPortraitBinding>(),Has.Length.EqualTo(2));
            string copy=string.Join("\n",context.GetComponentsInChildren<TMP_Text>().Select(label=>label.text));
            foreach(string id in ids)Assert.That(copy,Does.Contain(before.Find(id).name));
            Assert.That(copy,Does.Contain("KNOWN HOUSE EVENTS").And.Contain("not anyone's private feelings"));
            Assert.That(copy,Does.Not.Contain("High tension").And.Not.Contain("Harmony"));
            AssertDecisionCopyFits(context);
            AssertEquivalent(before,director.Snapshot);
            ButtonWithCaption(EpisodeHud.EventChoiceCaption("Listen without taking sides")).onClick.Invoke();
            yield return null;
            var after=director.Snapshot;
            Assert.That(after.revision,Is.EqualTo(before.revision+1));
            Assert.That(after.houseEvents.Single(item=>item.id=="context-event").resolved,Is.True);
            Assert.That(after.houseEvents.Single(item=>item.id=="context-event").chosenIndex,Is.Zero);
        }

        [UnityTest]
        public IEnumerator NominationComparisonPreservesSelectionAndReviewAtBothTextSizes()
        {
            yield return InstallDiaryFixture(state=>state.phase==EpisodePhase.Nomination && state.hohId==state.playerId
                && state.nominees.Count==0,"player nominations with candidate context");
            foreach(bool larger in new[]{false,true})
            {
                yield return ApplyTextSize(larger);
                yield return OpenDiaryFixturePanel();
                var before=director.Snapshot;
                var bytes=File.ReadAllBytes(director.SavePath);
                var candidates=EpisodeEngine.NominationCandidates(before).Take(3).ToArray();
                var firstButton=ButtonWithCaption(candidates[0].name);
                var secondButton=ButtonWithCaption(candidates[1].name);
                firstButton.onClick.Invoke();secondButton.onClick.Invoke();
                ButtonWithCaption(EpisodeHud.ShowCandidateContextCaption).onClick.Invoke();
                yield return null;
                Canvas.ForceUpdateCanvases();
                var comparison=DecisionUiRoot(EpisodeHud.CandidateContextName);
                Assert.That(comparison,Is.Not.Null);
                Assert.That(comparison.GetComponentsInChildren<CharacterPortraitBinding>(),Has.Length.EqualTo(2));
                string copy=string.Join("\n",comparison.GetComponentsInChildren<TMP_Text>().Select(label=>label.text));
                Assert.That(copy,Does.Contain(candidates[0].name).And.Contain(candidates[1].name));
                Assert.That(copy,Does.Contain("HoH ").And.Contain("Veto ").And.Contain("Your trust:"));
                AssertDecisionCopyFits(comparison);
                Assert.That(ButtonWithCaption(candidates[0].name),Is.SameAs(firstButton),"Comparison must not rebuild the nomination input.");
                Assert.That(ButtonWithCaption(candidates[1].name),Is.SameAs(secondButton));
                var review=ButtonWithCaption(EpisodeHud.DiaryReviewNominationsCaption);
                var reviewBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(comparison,review.transform);
                Assert.That(reviewBounds.min.y,Is.GreaterThanOrEqualTo(comparison.rect.yMax),
                    "Expanding context must leave the existing review/commit control above it.");
                ButtonWithCaption(EpisodeHud.HideCandidateContextCaption).onClick.Invoke();
                firstButton.onClick.Invoke();
                ButtonWithCaption(candidates[2].name).onClick.Invoke();
                ButtonWithCaption(EpisodeHud.ShowCandidateContextCaption).onClick.Invoke();
                Canvas.ForceUpdateCanvases();
                comparison=DecisionUiRoot(EpisodeHud.CandidateContextName);
                Assert.That(comparison.GetComponentsInChildren<RectTransform>().Any(rect=>rect.name=="Candidate context "+candidates[0].id),Is.False);
                Assert.That(comparison.GetComponentsInChildren<RectTransform>().Any(rect=>rect.name=="Candidate context "+candidates[2].id),Is.True);
                AssertEquivalent(before,director.Snapshot);
                Assert.That(File.ReadAllBytes(director.SavePath),Is.EqualTo(bytes));
                review.onClick.Invoke();
                Assert.That(director.HasDiaryDecisionDraft,Is.True,"The same current nomination input must still reach review.");
                AssertEquivalent(before,director.Snapshot);
                ButtonWithCaption(EpisodeHud.DiaryCancelCaption).onClick.Invoke();
                Assert.That(director.HasDiaryDecisionDraft,Is.False);
                director.ClosePanels();
                yield return null;
            }
        }

        private RectTransform DecisionUiRoot(string name) => director.GetComponentsInChildren<RectTransform>()
            .SingleOrDefault(rect=>rect.name==name && rect.gameObject.activeInHierarchy);

        private static void AssertDecisionCopyFits(RectTransform root)
        {
            foreach(var label in root.GetComponentsInChildren<TMP_Text>())
            {
                label.ForceMeshUpdate();
                Assert.That(label.isTextOverflowing,Is.False,label.text+" must fit the selected text size.");
                var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(root,label.transform);
                Assert.That(bounds.min.x,Is.GreaterThanOrEqualTo(root.rect.xMin-.1f),label.text);
                Assert.That(bounds.max.x,Is.LessThanOrEqualTo(root.rect.xMax+.1f),label.text);
            }
        }
    }
}
