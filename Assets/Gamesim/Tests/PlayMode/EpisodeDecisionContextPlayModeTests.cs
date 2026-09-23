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
            // The two it is about, standing together in the middle of the living room.
            var living=SceneComponents<Gamesim.House.HouseRoomMarker>().Single(marker=>marker.RoomName=="Living").transform.position;
            for(int i=0;i<ids.Length;i++)
            {
                var body=SceneComponents<Gamesim.House.HouseNpc>().Single(npc=>npc.Id==ids[i]);
                var agent=body.GetComponent<UnityEngine.AI.NavMeshAgent>();
                Assume.That(agent,Is.Not.Null);
                Assume.That(UnityEngine.AI.NavMesh.SamplePosition(living+new Vector3(i==0?-.7f:.7f,0f,0f),out var hit,1.5f,agent.areaMask),Is.True);
                Assume.That(agent.Warp(hit.position),Is.True);
            }
            Assert.That(director.TryOpenPhasePanel(),Is.True);
            yield return null;
            // Mockup-04: the card is low in the frame and the people it is about are above it.
            Assert.That(director.IsFramingHouseEvent,Is.True,"The camera takes the event's people when they stand together.");
            yield return Settle(()=>!cameraRig.IsTravelling&&cameraRig.HasArrived(0.05f),3f);
            Canvas.ForceUpdateCanvases();
            var before=director.Snapshot;
            var context=DecisionUiRoot(EpisodeHud.HouseEventContextName);
            Assert.That(context,Is.Not.Null);
            Assert.That(context.GetComponentsInChildren<CharacterPortraitBinding>(),Has.Length.EqualTo(2));
            string copy=string.Join("\n",context.GetComponentsInChildren<TMP_Text>().Select(label=>label.text));
            foreach(string id in ids)Assert.That(copy,Does.Contain(before.Find(id).name));
            Assert.That(copy,Does.Contain("WHAT YOU HAVE SEEN THIS WEEK").And.Contain("Not anyone's private feelings"));
            Assert.That(copy,Does.Not.Contain("High tension").And.Not.Contain("Harmony"));
            AssertDecisionCopyFits(context);
            AssertEquivalent(before,director.Snapshot);
            var hud=director.GetComponentInChildren<EpisodeHud>();
            Assert.That(hud.CurrentActivityLayout,Is.EqualTo(EpisodeHud.ActivityLayout.HouseEvent),
                "A pending event takes the band, where its choices are above the fold.");
            // The event keeps the priority: the way on is not pinned over its choices, it follows them.
            string onward=before.phase==EpisodePhase.Campaign?"Close campaigning and open voting":"Begin the next competition";
            Assert.That(FindButton(onward).transform.parent.name,Is.EqualTo("Episode content"),
                "Under a house event the way on stays in the column, after the choices.");
            var choices=DecisionUiRoot(EpisodeHud.EventChoicesName);
            Assert.That(choices,Is.Not.Null,"The choices are tiles.");
            Assert.That(ButtonWithCaption(EpisodeHud.EventChoiceCaption("Listen without taking sides")).transform.IsChildOf(choices),Is.True);
            if(Application.isBatchMode)yield return CaptureFraming("house-event");
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

        /// <summary>
        /// The Head of Household's decision out of the diary (mockup-09): the candidates as a grid
        /// of cards in a band between the rail and the right column, with the house, the strip and
        /// the column left up around it, and a pick lit on its card without rebuilding the input.
        /// </summary>
        [UnityTest]
        public IEnumerator Nominations_TheDecisionIsABandOfCandidateCards()
        {
            yield return InstallDiaryFixture(state=>state.phase==EpisodePhase.Nomination && state.hohId==state.playerId
                && state.nominees.Count==0,"player nominations in the house");
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(),Is.True);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var hud=director.GetComponentInChildren<EpisodeHud>();
            Assert.That(hud.CurrentActivityLayout,Is.EqualTo(EpisodeHud.ActivityLayout.Nominations),
                "Out of the diary the nominations take the band, not the docked panel.");

            var grid=ActiveRect(EpisodeHud.NomineeGridName);
            Assert.That(grid,Is.Not.Null,"The candidates are a grid of cards.");
            var candidates=EpisodeEngine.NominationCandidates(director.Snapshot).ToArray();
            Assert.That(grid.GetComponentsInChildren<UnityEngine.UI.Button>().Select(card=>card.name),
                Is.EquivalentTo(candidates.Select(candidate=>candidate.name)),"One card per candidate, named by the candidate.");

            // The band stands clear of the chrome the mockup keeps up around it.
            var panel=ActiveRect("Episode panel");
            foreach(var name in new[]{CastRail.RootName,EpisodeHud.HouseVibeCardName,IconRail.RootName,"Status"})
            {
                var chrome=ActiveRect(name);
                Assert.That(chrome,Is.Not.Null,name+" stays up while the Head of Household decides.");
                Assert.That(ScreenRect(panel).Overlaps(ScreenRect(chrome)),Is.False,"The band covers '"+name+"'.");
            }

            // A pick lights its card in place; pressing it again puts it back.
            var first=ButtonWithCaption(candidates[0].name);
            first.onClick.Invoke();
            var picked=first.transform.Find("Picked");
            Assert.That(picked!=null && picked.gameObject.activeSelf,Is.True,"A picked card is marked.");
            Assert.That(ButtonWithCaption(candidates[0].name),Is.SameAs(first),"Picking must not rebuild the input.");
            first.onClick.Invoke();
            picked=first.transform.Find("Picked");
            Assert.That(picked==null || !picked.gameObject.activeSelf,Is.True,"A card put back is not marked.");

            yield return CaptureFraming("nominations-band");
            director.ClosePanels();
            yield return null;
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
