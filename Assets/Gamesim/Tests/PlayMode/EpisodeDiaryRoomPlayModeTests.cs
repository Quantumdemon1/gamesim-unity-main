using System;
using System.Collections;
using System.IO;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Persistence;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator DiaryRoom_AllFiveRoutesRemainReachableAndActualTravelButtonOpensWithE()
        {
            Assert.That(director.HasDiaryRoom, Is.True);
            var before = director.Snapshot;
            player.Agent.speed = 25; player.Agent.acceleration = 100;
            WarpPlayer(new Vector3(0,0,14));
            Assert.That(director.TryOpenDiary(), Is.False, "Entering requires physically reaching the private room.");
            foreach (var room in SceneComponents<HouseRoomMarker>().OrderBy(room => room.RoomName))
            {
                Assert.That(player.TryMoveTo(room.transform.position), Is.True, room.RoomName);
                yield return WaitForDiaryWalk();
                Assert.That(Vector3.Distance(player.transform.position, room.transform.position), Is.LessThan(1));
            }
            Assert.That(SceneComponents<HouseRoomMarker>(), Has.Length.EqualTo(5));
            ButtonWithCaption(EpisodeHud.DiaryTravelCaption).onClick.Invoke();
            Assert.That(director.IsDiaryOpen, Is.False, "The travel button moves the player; it does not open remotely.");
            yield return WaitForDiaryWalk();
            Assert.That(director.CanUseDiary, Is.True);
            yield return PressDiaryKey(Key.E);
            Assert.That(director.IsDiaryOpen, Is.True);
            Assert.That(player.InputEnabled, Is.False);
            Assert.That(cameraRig.ControlsEnabled, Is.False);
            Assert.That(cameraRig.IsConversationFocused, Is.False);
            Assert.That(ActiveDiaryText(), Does.Contain("PRIVATE DIARY ROOM"));
            var paused = director.Snapshot;
            AssertOnlyNpcTravelFieldsChanged(before, paused);
            // Walking leaves NPC autonomy active; the real diary modal must then
            // pause EVERY durable field, not just the player's decision counters.
            float pauseDeadline = Time.realtimeSinceStartup + 1.1f;
            while (Time.realtimeSinceStartup < pauseDeadline)
            {
                yield return null;
                Assert.That(director.IsDiaryOpen, Is.True);
                AssertEquivalent(paused, director.Snapshot);
            }
            yield return PressDiaryKey(Key.Escape);
            Assert.That(director.IsPanelOpen, Is.False);
            Assert.That(player.InputEnabled, Is.True);
            Assert.That(cameraRig.ControlsEnabled, Is.True);
            AssertOnlyNpcTravelFieldsChanged(before, director.Snapshot);
        }

        private static void AssertOnlyNpcTravelFieldsChanged(EpisodeState before, EpisodeState after)
        {
            Assert.That(EpisodeValidation.TryValidate(after, out var reason), Is.True, reason);
            Assert.That(after.revision, Is.GreaterThanOrEqualTo(before.revision));
            Assert.That(after.npcSocial.rulesStartWeek, Is.EqualTo(before.npcSocial.rulesStartWeek));
            Assert.That(after.npcSocial.clockTick, Is.GreaterThanOrEqualTo(before.npcSocial.clockTick));
            Assert.That(after.npcSocial.nextConversationSequence, Is.GreaterThanOrEqualTo(before.npcSocial.nextConversationSequence));
            Assert.That((long)after.revision - before.revision, Is.GreaterThanOrEqualTo(
                after.npcSocial.clockTick - before.npcSocial.clockTick
                + after.npcSocial.nextConversationSequence - before.npcSocial.nextConversationSequence),
                "Every whole tick and accepted start requires its own durable revision.");

            var unchanged = after.Clone();
            Assert.That(unchanged.relationships.Select(edge => edge.fromId + ":" + edge.toId),
                Is.EqualTo(before.relationships.Select(edge => edge.fromId + ":" + edge.toId)),
                "Autonomy cannot add/remove/reorder the authored directed relationship graph.");
            for (int index = 0; index < unchanged.relationships.Count; index++)
            {
                var edge = unchanged.relationships[index];
                var original = before.relationships[index];
                if (edge.fromId == before.playerId || edge.toId == before.playerId) continue;
                // Source NPC completion writes only score and lastInteractionWeek.
                // IDs, notes and event histories remain subject to the full comparison.
                Assert.That(edge.lastInteractionWeek,
                    Is.EqualTo(original.lastInteractionWeek).Or.EqualTo(after.week));
                edge.score = original.score;
                edge.lastInteractionWeek = original.lastInteractionWeek;
            }

            Assert.That(after.relationshipArcs.Select(arc => arc.npcId).Take(before.relationshipArcs.Count),
                Is.EqualTo(before.relationshipArcs.Select(arc => arc.npcId)),
                "Source aggregate arcs append; existing arcs and histories cannot be reordered.");
            foreach (var arc in after.relationshipArcs)
            {
                var original = before.relationshipArcs.SingleOrDefault(item => item.npcId == arc.npcId);
                var priorHistory = original?.weeklyHistory ?? new System.Collections.Generic.List<ArcHistory>();
                Assert.That(arc.weeklyHistory.Take(priorHistory.Count).Select(item => JsonUtility.ToJson(item)),
                    Is.EqualTo(priorHistory.Select(item => JsonUtility.ToJson(item))));
                Assert.That(arc.weeklyHistory.Count, Is.GreaterThanOrEqualTo(priorHistory.Count));
                var expected = original == null
                    ? new System.Collections.Generic.List<RelationshipArcState>()
                    : new[] { original.Clone() }.ToList();
                foreach (var entry in arc.weeklyHistory.Skip(priorHistory.Count))
                {
                    Assert.That(entry.week, Is.EqualTo(after.week));
                    Assert.That(entry.reason, Is.EqualTo("Relationship changed by "
                        + entry.delta.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                    expected = WebRelationshipArcs.Update(expected, arc.npcId, after.Find(arc.npcId).name,
                        entry.delta, entry.reason, entry.week).arcs;
                }
                Assert.That(expected, Has.Count.EqualTo(1),
                    "A new NPC aggregate arc must have an actual appended source history.");
                Assert.That(JsonUtility.ToJson(arc), Is.EqualTo(JsonUtility.ToJson(expected[0])),
                    "NPC aggregate arc metadata must be derived from its exact retained/appended history.");
            }

            unchanged.revision = before.revision;
            unchanged.npcSocial = before.npcSocial.Clone();
            unchanged.relationshipArcs = before.relationshipArcs.Select(arc => arc.Clone()).ToList();
            // This compares every remaining serialized field, including BOTH
            // player-directed relationship perspectives, all contestants/stats/mood,
            // main RNG, action cost, diary/persona state, logs, memories and receipts.
            AssertEquivalent(before, unchanged);
        }

        [UnityTest]
        public IEnumerator DiaryRoom_KeyboardTravelWalksAndWallsBlockInteraction()
        {
            WarpPlayer(new Vector3(0,0,14));
            player.Agent.speed = 25; player.Agent.acceleration = 100;
            yield return PressDiaryKey(Key.R);
            Assert.That(director.IsDiaryOpen, Is.False);
            yield return WaitForDiaryWalk();
            Assert.That(Vector3.Distance(player.transform.position,director.DiaryPosition), Is.LessThan(1));
            WarpPlayer(director.DiaryPosition + Vector3.right * 1.5f);
            Assert.That(director.CanUseDiary, Is.True);
            var blocker = new GameObject("Test-owned diary sight obstruction", typeof(BoxCollider));
            SceneManager.MoveGameObjectToScene(blocker,player.gameObject.scene);
            blocker.transform.position = (player.transform.position + director.DiaryPosition) * .5f + Vector3.up * 1.15f;
            blocker.transform.localScale = new Vector3(.3f,2.2f,1f);
            Physics.SyncTransforms();
            Assert.That(director.TryOpenDiary(), Is.False, "Proximity cannot reach through an opaque collider.");
            blocker.SetActive(false); Physics.SyncTransforms();
            Assert.That(director.TryOpenDiary(), Is.True);
        }

        [UnityTest]
        public IEnumerator DiaryRoom_PrivateMemoriesDoNotRevealOtherOwnersOrChangeTheEpisode()
        {
            yield return InstallDiaryFixture(state => state.Find(state.playerId).status == ContestantStatus.Active
                && state.memories.Any(memory => memory.ownerId == state.playerId), "recorded player experience");
            var before = director.Snapshot;
            yield return OpenDiaryFixturePanel();
            var labels = ActiveDiaryText();
            foreach (var memory in before.memories.Where(memory => memory.ownerId == before.playerId))
                Assert.That(labels, Does.Contain("Week " + memory.week + ": " + memory.text));
            foreach (var memory in before.memories.Where(memory => memory.ownerId != before.playerId
                && !before.memories.Any(owned => owned.ownerId == before.playerId && owned.week == memory.week && owned.text == memory.text)))
                Assert.That(labels, Does.Not.Contain("Week " + memory.week + ": " + memory.text));
            Assert.That(DiaryHasButton("Begin the next competition"), Is.False);
            Assert.That(DiaryHasButton("Continue episode"), Is.False);
            director.ContinueEpisode(); // Only the phase panel is permitted to expose progression.
            AssertEquivalent(before,director.Snapshot);
            director.ClosePanels();
            AssertEquivalent(before,director.Snapshot);
        }

        [UnityTest]
        public IEnumerator DiaryRoom_EPrioritizesRoomOverNearbyNpcAndLosingProximityClosesIt()
        {
            var before = director.Snapshot;
            var maya = SceneComponents<HouseNpc>().Single(npc => npc.Id == ContentCatalog.MayaId);
            WarpPlayer(director.DiaryPosition);
            maya.transform.position = director.DiaryPosition + Vector3.forward * 1.8f;
            Physics.SyncTransforms();
            Assert.That(director.TryOpenNpc(maya.Id), Is.True, "Both targets should be available for this priority test.");
            director.ClosePanels();
            yield return PressDiaryKey(Key.E);
            Assert.That(director.IsDiaryOpen, Is.True);
            Assert.That(cameraRig.IsConversationFocused, Is.False);
            WarpPlayer(new Vector3(0,0,14));
            yield return null; yield return null;
            Assert.That(director.IsPanelOpen, Is.False, "A moved player cannot retain remote diary access.");
            Assert.That(player.InputEnabled, Is.True);
            AssertEquivalent(before,director.Snapshot);
        }

        [UnityTest]
        public IEnumerator DiaryNomination_ReviewCancelAndEscapeAreAtomicThenActualConfirmCommitsOnce()
        {
            yield return InstallDiaryFixture(state => state.phase == EpisodePhase.Nomination
                && state.hohId == state.playerId && state.nominees.Count == 0, "player HoH nominations");
            var before = director.Snapshot;
            var bytes = File.ReadAllBytes(director.SavePath);
            yield return OpenDiaryFixturePanel();
            yield return ReviewDiaryNominations(before);
            Assert.That(director.HasDiaryDecisionDraft, Is.True);
            AssertEquivalent(before,director.Snapshot);
            Assert.That(File.ReadAllBytes(director.SavePath), Is.EqualTo(bytes));
            ButtonWithCaption(EpisodeHud.DiaryCancelCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.HasDiaryDecisionDraft, Is.False);
            AssertEquivalent(before,director.Snapshot);
            yield return ReviewDiaryNominations(before);
            yield return PressDiaryKey(Key.Escape);
            Assert.That(director.HasDiaryDecisionDraft, Is.False);
            Assert.That(director.IsPanelOpen, Is.False);
            AssertEquivalent(before,director.Snapshot);
            Assert.That(File.ReadAllBytes(director.SavePath), Is.EqualTo(bytes));
            yield return OpenDiaryFixturePanel();
            yield return ReviewDiaryNominations(before);
            var confirm = ButtonWithCaption(EpisodeHud.DiaryConfirmCaption);
            confirm.onClick.Invoke(); confirm.onClick.Invoke();
            yield return null; yield return null;
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(before.revision + 1), "Double activation must not create two commands.");
            Assert.That(after.nominees, Is.EqualTo(EpisodeEngine.NominationCandidates(before).Take(2).Select(actor => actor.id)));
            Assert.That(after.phase, Is.EqualTo(EpisodePhase.Nomination), "The private room never advances the ceremony.");
            Assert.That(director.IsDiaryOpen, Is.True);
            Assert.That(DiaryHasButton("Continue episode"), Is.False);
            Assert.That(new EpisodeSaveStore(director.SavePath).TryLoad(out var saved,out var message), Is.True,message);
            AssertEquivalent(after,saved);
        }

        [UnityTest]
        public IEnumerator DiaryNomination_NonHohHasNoControlsAndCannotCommitThroughAuthority()
        {
            yield return InstallDiaryFixture(state => state.phase == EpisodePhase.Nomination
                && state.Find(state.playerId).status == ContestantStatus.Active && state.hohId != state.playerId
                && state.nominees.Count == 0, "non-HoH nominations");
            var before = director.Snapshot;
            yield return OpenDiaryFixturePanel();
            Assert.That(DiaryHasButton(EpisodeHud.DiaryReviewNominationsCaption), Is.False);
            var attempt = NextCommand(before);
            attempt.kind = EpisodeCommandKind.Nominate;
            var candidates = EpisodeEngine.NominationCandidates(before).Take(2).ToArray();
            attempt.targetId = candidates[0].id; attempt.secondTargetId = candidates[1].id;
            Assert.That(director.Submit(attempt).accepted, Is.False);
            AssertEquivalent(before,director.Snapshot);
        }

        [UnityTest]
        public IEnumerator DiaryVote_ActualConfirmRecordsOnlyPlayersBallotWithoutRevealingOrAdvancing()
        {
            yield return InstallDiaryFixture(state => state.phase == EpisodePhase.Eviction && !state.evictionResolved
                && EpisodeEngine.Voters(state).Any(voter => voter.isPlayer)
                && !state.votes.Any(vote => vote.voterId == state.playerId), "eligible private eviction ballot");
            var before = director.Snapshot;
            yield return OpenDiaryFixturePanel();
            var caption = "Vote to evict " + before.Find(before.nominees[0]).name;
            ButtonWithCaption(caption).onClick.Invoke();
            yield return null; yield return null;
            AssertEquivalent(before,director.Snapshot);
            ButtonWithCaption(EpisodeHud.DiaryCancelCaption).onClick.Invoke();
            yield return null; yield return null;
            AssertEquivalent(before,director.Snapshot);
            ButtonWithCaption(caption).onClick.Invoke();
            yield return null; yield return null;
            ButtonWithCaption(EpisodeHud.DiaryConfirmCaption).onClick.Invoke();
            yield return null; yield return null;
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(before.revision + 1));
            Assert.That(after.phase, Is.EqualTo(EpisodePhase.Eviction));
            Assert.That(after.evictionResolved, Is.False);
            Assert.That(after.votes.Count, Is.EqualTo(before.votes.Count + 1));
            Assert.That(after.votes.Single(vote => vote.voterId == after.playerId).targetId, Is.EqualTo(before.nominees[0]));
            Assert.That(after.votes.Where(vote => vote.voterId != after.playerId).Select(vote => vote.voterId + ":" + vote.targetId),
                Is.EqualTo(before.votes.Where(vote => vote.voterId != before.playerId).Select(vote => vote.voterId + ":" + vote.targetId)));
            Assert.That(DiaryHasButton(caption), Is.False);
            Assert.That(DiaryHasButton("Continue episode"), Is.False);
            Assert.That(ActiveDiaryText(), Does.Contain("Your ballot has already been recorded."));
        }

        [UnityTest]
        public IEnumerator DiaryVote_NomineeHasNoBallotControlsAndEngineRejectsVoting()
        {
            yield return InstallDiaryFixture(state => state.phase == EpisodePhase.Eviction && !state.evictionResolved
                && state.nominees.Contains(state.playerId), "nominated player's ineligible ballot");
            var before = director.Snapshot;
            yield return OpenDiaryFixturePanel();
            foreach (string id in before.nominees)
                Assert.That(DiaryHasButton("Vote to evict " + before.Find(id).name), Is.False);
            var attempt = NextCommand(before); attempt.kind = EpisodeCommandKind.CastVote; attempt.targetId = before.nominees[0];
            Assert.That(director.Submit(attempt).accepted, Is.False);
            AssertEquivalent(before,director.Snapshot);
        }

        [UnityTest]
        public IEnumerator DiaryVeto_HolderCanReviewAndDeclineWithoutAdvancingTheMeeting()
        {
            yield return InstallDiaryFixture(state => state.phase == EpisodePhase.VetoMeeting
                && !state.vetoResolved && state.vetoHolderId == state.playerId, "player veto holder");
            var before = director.Snapshot;
            yield return OpenDiaryFixturePanel();
            ButtonWithCaption("Do not use the veto").onClick.Invoke();
            yield return null; yield return null;
            AssertEquivalent(before,director.Snapshot);
            ButtonWithCaption(EpisodeHud.DiaryConfirmCaption).onClick.Invoke();
            yield return null; yield return null;
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(before.revision + 1));
            Assert.That(after.vetoResolved, Is.True);
            Assert.That(after.nominees, Is.EqualTo(before.nominees));
            Assert.That(after.phase, Is.EqualTo(EpisodePhase.VetoMeeting));
        }

        [UnityTest]
        public IEnumerator DiaryVeto_ReplacementHohCannotOverrideNpcVetoChoice()
        {
            yield return InstallDiaryFixture(state => state.phase == EpisodePhase.VetoMeeting && !state.vetoResolved
                && state.hohId == state.playerId && state.vetoHolderId != state.playerId
                && EpisodeEngine.NpcVetoSave(state) != null, "HoH replacement for NPC veto");
            var before = director.Snapshot;
            string saved = EpisodeEngine.NpcVetoSave(before);
            var replacement = EpisodeEngine.ReplacementCandidates(before).First();
            yield return OpenDiaryFixturePanel();
            Assert.That(DiaryHasButton("Do not use the veto"), Is.False);
            ButtonWithCaption(replacement.name).onClick.Invoke();
            yield return null; yield return null;
            AssertEquivalent(before,director.Snapshot);
            ButtonWithCaption(EpisodeHud.DiaryConfirmCaption).onClick.Invoke();
            yield return null; yield return null;
            var after = director.Snapshot;
            Assert.That(after.vetoResolved, Is.True);
            Assert.That(after.nominees, Does.Not.Contain(saved));
            Assert.That(after.nominees, Does.Contain(replacement.id));
            Assert.That(after.phase, Is.EqualTo(EpisodePhase.VetoMeeting));
        }

        [UnityTest]
        public IEnumerator DiaryRoom_InactivePlayerCannotTravelOrInteract()
        {
            yield return InstallFinaleFixture(false);
            var before = director.Snapshot;
            var position = player.transform.position;
            Assert.That(ButtonWithCaption(EpisodeHud.DiaryTravelCaption).interactable, Is.False);
            director.GoToDiary();
            Assert.That(player.transform.position, Is.EqualTo(position));
            WarpPlayer(director.DiaryPosition);
            Assert.That(director.CanUseDiary, Is.False);
            Assert.That(director.TryOpenDiary(), Is.False);
            AssertEquivalent(before,director.Snapshot);
        }

        [UnityTest]
        public IEnumerator DiaryReflection_ReviewCancelThenActualConfirmPersistsOnlyPrivateIntendedEffects()
        {
            yield return InstallDiaryFixture(state => state.pendingDiary != null, "pending post-eviction reflection");
            var before = director.Snapshot;
            yield return ReloadEpisode();
            AssertEquivalent(before,director.Snapshot);
            var prompt = EpisodeEngine.CurrentDiary(before);
            var choice = prompt.choices.Single(item => item.id == "remorseful");
            var choiceCaption = choice.persona + " · " + choice.text;
            var diskBefore = File.ReadAllBytes(director.SavePath);
            yield return OpenDiaryFixturePanel();
            ButtonWithCaption(choiceCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(ActiveDiaryText(), Does.Contain("not a directed-trust change or a jury ballot"));
            AssertEquivalent(before,director.Snapshot);
            Assert.That(File.ReadAllBytes(director.SavePath), Is.EqualTo(diskBefore));
            ButtonWithCaption(EpisodeHud.DiaryCancelReflectionCaption).onClick.Invoke();
            yield return null; yield return null;
            AssertEquivalent(before,director.Snapshot);
            Assert.That(director.HasDiaryDecisionDraft, Is.False);
            ButtonWithCaption(choiceCaption).onClick.Invoke();
            yield return null; yield return null;
            var confirm = ButtonWithCaption(EpisodeHud.DiaryConfirmReflectionCaption);
            confirm.onClick.Invoke(); confirm.onClick.Invoke();
            yield return null; yield return null;
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(before.revision + 1));
            Assert.That(after.pendingDiary, Is.Null);
            Assert.That(after.resolvedDiaryIds, Does.Contain(before.pendingDiary.id));
            Assert.That(after.lastDiaryRoomWeek, Is.EqualTo(before.week));
            Assert.That(after.playerPersona.history.Count, Is.EqualTo(before.playerPersona.history.Count + 1));
            Assert.That(after.playerPersona.history.Last().persona, Is.EqualTo(choice.persona));
            var expectedPersona = WebDiaryRoom.ApplyPersonaChoice(before.playerPersona,choice,before.week);
            Assert.That(JsonUtility.ToJson(after.playerPersona), Is.EqualTo(JsonUtility.ToJson(expectedPersona)));
            var expectedLedger = WebJurySentiment.ShiftAllJurorSentiment(before.jurySentiment,choice.effects.juryDelta.Value,
                "Diary Room: " + choice.persona,before.week);
            Assert.That(JsonUtility.ToJson(after.jurySentiment), Is.EqualTo(JsonUtility.ToJson(expectedLedger)));
            Assert.That(after.relationships.Select(edge => edge.score), Is.EqualTo(before.relationships.Select(edge => edge.score)));
            Assert.That(after.votes.Select(vote => vote.voterId + ":" + vote.targetId), Is.EqualTo(before.votes.Select(vote => vote.voterId + ":" + vote.targetId)));
            Assert.That(after.phaseEventCompBonus, Is.EqualTo(before.phaseEventCompBonus));
            Assert.That(after.phaseEventSocialBonus, Is.EqualTo(before.phaseEventSocialBonus));
            var recordedAnswer = after.events.Last(entry => entry.kind == "diary-room");
            Assert.That(recordedAnswer.text, Does.Contain(choice.text));
            Assert.That(recordedAnswer.audienceIds, Is.EquivalentTo(new[] { after.playerId }), "A reflection is not an NPC confession or public reveal.");
            Assert.That(after.phase, Is.EqualTo(before.phase));
            yield return ReloadEpisode();
            AssertEquivalent(after,director.Snapshot);
            yield return OpenDiaryFixturePanel();
            Assert.That(DiaryHasButton(choiceCaption), Is.False);
            Assert.That(DiaryHasButton(EpisodeHud.DiarySkipReflectionCaption), Is.False);
            Assert.That(ActiveDiaryText(), Does.Contain("Current diary persona: " + after.playerPersona.current));
            director.ClosePanels();
            var npc = SceneComponents<HouseNpc>().First(actor => actor.gameObject.activeInHierarchy);
            yield return OpenNearbyNpc(npc);
            Assert.That(director.GetComponentsInChildren<TMPro.TMP_Text>().Single(label => label.name == "NPC spoken dialogue").text,
                Does.Not.Contain(choice.text), "Private player answers must not be presented as an NPC's own knowledge.");
        }

        [UnityTest]
        public IEnumerator DiaryReflection_ActualEpisodeScreenSkipConsumesPromptWithoutPersonaOrLedgerAwards()
        {
            yield return InstallDiaryFixture(state => state.pendingDiary != null, "skippable private reflection");
            var before = director.Snapshot;
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            yield return null; yield return null;
            Assert.That(DiaryHasButton(EpisodeHud.DiaryVisitReflectionCaption), Is.True);
            Assert.That(DiaryHasButton("Continue episode"), Is.False);
            Assert.That(DiaryHasButton("Begin the next competition"), Is.False);
            ButtonWithCaption(EpisodeHud.DiarySkipReflectionCaption).onClick.Invoke();
            yield return null; yield return null;
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(before.revision + 1));
            Assert.That(after.pendingDiary, Is.Null);
            Assert.That(after.resolvedDiaryIds, Does.Contain(before.pendingDiary.id));
            Assert.That(JsonUtility.ToJson(after.playerPersona), Is.EqualTo(JsonUtility.ToJson(before.playerPersona)));
            Assert.That(JsonUtility.ToJson(after.jurySentiment), Is.EqualTo(JsonUtility.ToJson(before.jurySentiment)));
            Assert.That(after.phaseEventCompBonus, Is.EqualTo(before.phaseEventCompBonus));
            Assert.That(after.phaseEventSocialBonus, Is.EqualTo(before.phaseEventSocialBonus));
            Assert.That(after.phase, Is.EqualTo(before.phase));
            yield return ReloadEpisode();
            AssertEquivalent(after,director.Snapshot);
            director.SkipDiary(); director.ReflectDiary("remorseful");
            AssertEquivalent(after,director.Snapshot);
        }

        [UnityTest]
        public IEnumerator NpcLoyalty_ActualMilestoneButtonRecordsPlayerDeclarationAndReloadsInNotebook()
        {
            yield return EarnVisibleOathOpportunity();
            var before = director.Snapshot;
            string id = ContentCatalog.MayaId;
            Assert.That(ActiveDiaryText(), Does.Contain("not " + before.Find(id).name + "'s consent or promise"));
            ButtonWithCaption(EpisodeHud.OathDeclareCaption).onClick.Invoke();
            yield return null; yield return null;
            var after = director.Snapshot;
            var oath = after.loyaltyOaths.Single(item => item.playerId == after.playerId && item.targetId == id);
            Assert.That(oath.week, Is.EqualTo(after.week));
            Assert.That(after.revision, Is.EqualTo(before.revision + 1));
            Assert.That(after.Score(after.playerId,id), Is.EqualTo(Math.Min(100,before.Score(before.playerId,id) + 5)));
            Assert.That(after.Score(id,after.playerId), Is.EqualTo(before.Score(id,before.playerId)), "Declaring loyalty does not produce NPC consent or reciprocal trust.");
            Assert.That(after.promises.Count, Is.EqualTo(before.promises.Count));
            Assert.That(after.oathOpportunities, Does.Not.Contain(id));
            Assert.That(DiaryHasButton(EpisodeHud.OathDeclareCaption), Is.False);
            yield return ReloadEpisode();
            AssertEquivalent(after,director.Snapshot);
            ButtonWithCaption("Notebook [J]").onClick.Invoke();
            yield return null; yield return null;
            Assert.That(ActiveDiaryText(), Does.Contain("You declared loyalty to " + after.Find(id).name));
            Assert.That(ActiveDiaryText(), Does.Contain("A declaration is not a mutual guarantee."));
        }

        [UnityTest]
        public IEnumerator NpcLoyalty_ActualPassButtonDoesNotCreateOathOrReciprocalEffects()
        {
            yield return EarnVisibleOathOpportunity();
            var before = director.Snapshot;
            ButtonWithCaption(EpisodeHud.OathDeclineCaption).onClick.Invoke();
            yield return null; yield return null;
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(before.revision + 1));
            Assert.That(after.loyaltyOaths.Count, Is.EqualTo(before.loyaltyOaths.Count));
            Assert.That(after.relationships.Select(edge => edge.score), Is.EqualTo(before.relationships.Select(edge => edge.score)));
            Assert.That(after.oathOpportunities, Does.Not.Contain(ContentCatalog.MayaId));
            Assert.That(after.shownOathMilestones, Does.Contain(ContentCatalog.MayaId));
            Assert.That(DiaryHasButton(EpisodeHud.OathDeclareCaption), Is.False);
            Assert.That(DiaryHasButton(EpisodeHud.OathDeclineCaption), Is.False);
            yield return ReloadEpisode();
            AssertEquivalent(after,director.Snapshot);
        }

        private IEnumerator EarnVisibleOathOpportunity()
        {
            var npc = SceneComponents<HouseNpc>().Single(actor => actor.Id == ContentCatalog.MayaId);
            yield return OpenNearbyNpc(npc);
            Assert.That(DiaryHasButton(EpisodeHud.OathDeclareCaption), Is.False);
            for (int index = 0; index < 18 && !director.Snapshot.oathOpportunities.Contains(npc.Id); index++)
            {
                var before = director.Snapshot;
                // Eighteen +4 talks only reach 72. Use the actual legal alliance action
                // once reciprocal trust permits it; its +8 crosses 75 within this window.
                string action = !before.Allied(before.playerId,npc.Id) && before.Score(npc.Id,before.playerId) >= 8
                    ? "Propose an alliance" : "Spend time together";
                ButtonWithCaption(action).onClick.Invoke();
                yield return null; yield return null;
                Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision + 1));
            }
            Assert.That(director.Snapshot.oathOpportunities, Does.Contain(npc.Id), "A legal conversation history must reach the source milestone before a declaration is offered.");
        }

        private IEnumerator ReviewDiaryNominations(EpisodeState state)
        {
            foreach (var candidate in EpisodeEngine.NominationCandidates(state).Take(2))
                ButtonWithCaption(candidate.name).onClick.Invoke();
            ButtonWithCaption(EpisodeHud.DiaryReviewNominationsCaption).onClick.Invoke();
            yield return null; yield return null;
        }

        private IEnumerator OpenDiaryFixturePanel()
        {
            // Fixture positioning is isolated from the real navigation-route acceptance test above.
            WarpPlayer(director.DiaryPosition);
            Assert.That(director.TryOpenDiary(), Is.True);
            yield return null; yield return null;
        }

        private IEnumerator WaitForDiaryWalk()
        {
            var deadline = Time.realtimeSinceStartup + 12;
            while (!player.HasArrived && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(player.HasArrived, Is.True, "The private-room route must complete on the actual baked navigation surface.");
            yield return null;
        }

        private IEnumerator PressDiaryKey(Key key)
        {
            if (testKeyboard == null) testKeyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(testKeyboard,new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(testKeyboard,new KeyboardState());
            yield return null; yield return null;
        }

        private bool DiaryHasButton(string caption) => director.GetComponentsInChildren<Button>()
            .Any(button => button.IsActive() && button.name == caption);

        private string ActiveDiaryText() => string.Join("\n",director.GetComponentsInChildren<TMPro.TMP_Text>()
            .Where(label => label.gameObject.activeInHierarchy).Select(label => label.text));

        private IEnumerator InstallDiaryFixture(Func<EpisodeState,bool> eligible, string description)
        {
            // Bounded, legal histories exercise real rule decisions, not hand-edited phase/role DTOs.
            EpisodeState fixture = null;
            for (uint seed = 1; seed <= 120 && fixture == null; seed++)
            {
                var engine = new EpisodeEngine(ContentCatalog.Create(seed));
                for (int guard = 0; guard < 150; guard++)
                {
                    var current = engine.Snapshot;
                    if (eligible(current)) { fixture = current; break; }
                    if (current.phase == EpisodePhase.Finished) break;
                    var result = engine.Apply(NextCommand(current));
                    Assert.That(result.accepted, Is.True,result.reason);
                }
            }
            Assert.That(fixture, Is.Not.Null,"No bounded legal fixture found: " + description);
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            yield return ReloadEpisode();
            AssertEquivalent(fixture,director.Snapshot);
        }
    }
}
