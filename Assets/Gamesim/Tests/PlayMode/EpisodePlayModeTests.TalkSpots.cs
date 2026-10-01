using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Persistence;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Talk on real seats (PACK8-PASS-PLAN C3). Asked to talk, the player and the houseguest go to
    /// a talk spot in the houseguest's room or the player's; once both are there they sit on the
    /// seats or face each other and the conversation opens, and closing it gets both up at their
    /// seats and lets the place go. Reduced motion, a batch run that does not ask, and a second press
    /// open the conversation as it always opened. A saved living-room chat whose own pair is taken
    /// sits on the couches, and the save never names the place.
    ///
    /// <para>Every wait is bounded in real seconds: the walks, the seats and the arrivals run on the
    /// unscaled clock. The bodies are measured where the seat puts them, because a seat moves only
    /// the visual body and leaves the navigation root at its approach.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// The talk spots asked for in a batch run, and reduced motion - which never walks to one -
        /// set on the director, the rig and the bodies the way the stage tests set it.
        /// </summary>
        private void AskForTheTalkSpots(bool reduced = false)
        {
            director.TalkSpotsInBatchRuns = true;
            typeof(EpisodeDirector).GetField("reducedMotion", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(director, reduced);
            cameraRig.SetReducedMotion(reduced);
            foreach (var visual in SceneComponents<CharacterPresentation>()) visual.SetReducedMotion(reduced);
        }

        /// <summary>The houseguest nearest the player with nothing else to do: no conversation, no activity, no walk.</summary>
        private HouseNpc FreeHouseguest(HouseMeetingCoordinator coordinator)
        {
            var npc = SceneComponents<HouseNpc>()
                .Where(body => body.gameObject.activeInHierarchy && coordinator.TryGetMotion(body.Id, out var motion) && motion.LeaseId == null)
                .OrderBy(body => (body.transform.position - player.transform.position).sqrMagnitude).FirstOrDefault();
            Assert.That(npc, Is.Not.Null, "A houseguest with nothing else to do.");
            return npc;
        }

        [UnityTest]
        public IEnumerator TalkSpots_TalkTakesBothToAPlaceSitsOrFacesThemThereAndClosingLetsItGo()
        {
            yield return WaitForNpcRuntimeBinding();
            AskForTheTalkSpots();
            var coordinator = NpcRead<HouseMeetingCoordinator>("npcMeetings");
            var npc = FreeHouseguest(coordinator);
            Assert.That(HouseRoomQuery.TryCreate(npc.gameObject.scene, out var rooms, out var why), Is.True, why);
            rooms.TryLocate(npc.transform.position, .35f, out var theirRoom);
            rooms.TryLocate(player.transform.position, player.Agent.radius, out var yourRoom);

            director.TalkFromCastMenu(npc.Id);
            yield return null;
            var spot = director.CurrentTalkSpot;
            Assert.That(spot, Is.Not.Null, "Talk sends the two to a place to talk: " + coordinator.LastFailure);
            Assert.That(director.TalkSpotId, Is.EqualTo(spot.SpotId));
            Assert.That(spot.RoomId, Is.EqualTo(theirRoom).Or.EqualTo(yourRoom), "in the houseguest's room, or the player's.");
            Assert.That(director.WalkingToId, Is.EqualTo(npc.Id), "The walk is the walk to them, so everything that calls a walk off calls this off.");
            Assert.That(director.TalkingToId, Is.Null, "Nothing opens before both are there.");
            Assert.That(coordinator.TryGetMotion(npc.Id, out var motion), Is.True);
            Assert.That(motion.LeaseId, Is.EqualTo(spot.Token), "The houseguest is on their way to their place.");

            var npcVisual = npc.GetComponent<CharacterPresentation>();
            var mine = player.GetComponent<CharacterPresentation>();
            var air = new List<string>();
            void Check(string moment)
            {
                foreach (var (body, visual) in new[] { (npc.transform, npcVisual), (player.transform, mine) })
                {
                    var seat = body.GetComponent<HouseSeatPresentation>();
                    if (visual != null && visual.IsSeated && (seat == null || !seat.Active) && air.Count < 8)
                        air.Add(body.name + " " + moment + ": seated with no seat holding the body");
                }
            }

            float deadline = Time.realtimeSinceStartup + 45f;
            while (director.TalkingToId == null && director.CurrentTalkSpot == spot && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                Check("on the way");
            }
            Assert.That(director.TalkingToId, Is.EqualTo(npc.Id), "The conversation opens once both are there. The place is "
                + (director.CurrentTalkSpot == spot ? "still held" : "let go") + ", the walk is to " + (director.WalkingToId ?? "nobody")
                + ", the houseguest " + (motion.ArrivalFailure ?? "has arrived") + ", the player " + FlatDistance(player.transform.position, spot.PlayerStands).ToString("0.00")
                + " m from their place. " + director.StatusMessage);
            Assert.That(director.CurrentTalkSpot, Is.SameAs(spot), "It opened at the place, and the conversation keeps it.");
            Assert.That(cameraRig.IsConversationFocused, Is.True);
            var npcSeat = npc.GetComponent<HouseSeatPresentation>();
            var mySeat = player.GetComponent<HouseSeatPresentation>();
            if (spot.Seated)
            {
                Assert.That(npcSeat != null && npcSeat.Active && mySeat != null && mySeat.Active, Is.True,
                    "At two seats both sit down before the panel opens.");
                // The two-shot looks into a pair sitting the same way from the side they face, not at
                // their backs - unless a houseguest stands where that camera would be, when the rig
                // takes the other side. Read on the frame it opened, before anybody has moved far.
                var faces = Quaternion.Euler(0f, spot.NpcPlace.Facing, 0f) * Vector3.forward
                    + Quaternion.Euler(0f, spot.PlayerPlace.Facing, 0f) * Vector3.forward;
                if (faces.sqrMagnitude >= 1f)
                {
                    var across = npc.transform.position - player.transform.position;
                    across.y = 0f;
                    var front = Vector3.Cross(Vector3.up, across).normalized;
                    if (Vector3.Dot(front, faces) < 0f) front = -front;
                    var eye = (spot.NpcPlace.Position + spot.PlayerPlace.Position) * .5f
                        + front * HouseCameraRig.TwoShotDistance * Mathf.Cos(HouseCameraRig.TwoShotPitch * Mathf.Deg2Rad);
                    bool somebodyThere = SceneComponents<HouseNpc>().Any(other => other != npc && other.gameObject.activeInHierarchy
                        && FlatDistance(other.transform.position, eye) < HouseCameraRig.TwoShotClearance + 1.5f);
                    var looking = Quaternion.Euler(0f, cameraRig.Yaw, 0f) * Vector3.forward;
                    Assert.That(Vector3.Dot(looking, front) < 0f || somebodyThere, Is.True,
                        "The two-shot frames a seated pair from the side they face. Seats face " + spot.NpcPlace.Facing.ToString("0")
                        + " and " + spot.PlayerPlace.Facing.ToString("0") + ", the camera looks along " + cameraRig.Yaw.ToString("0") + ".");
                }
            }

            float settle = Time.realtimeSinceStartup + 3f;
            while (spot.Seated && Time.realtimeSinceStartup < settle)
            {
                yield return null;
                Check("in the conversation");
                npcSeat = npc.GetComponent<HouseSeatPresentation>();
                mySeat = player.GetComponent<HouseSeatPresentation>();
                if (npcSeat != null && npcSeat.Settled && mySeat != null && mySeat.Settled) break;
            }
            if (spot.Seated)
            {
                Assert.That(HouseConversationSpots.OnFurniture(spot.NpcPlace) && HouseConversationSpots.OnFurniture(spot.PlayerPlace), Is.True,
                    "Both seats are on furniture.");
                Assert.That(npcSeat != null && npcSeat.Active && npcSeat.Settled && npcVisual.IsSeated, Is.True, "The houseguest sits on their seat,");
                Assert.That(mySeat != null && mySeat.Active && mySeat.Settled && mine.IsSeated, Is.True, "and the player on theirs,");
                // At the hips where the rig has them, as the ceremony tests measure a sitter: the sit
                // fits the hips to the cushion, which leaves the visual root off the seat by the pose's
                // own offset. Half a metre is still well inside the 0.9 m between a pair's seats.
                var npcHips = HipsOf(npc);
                var myHips = HipsOf(player);
                Assert.That(FlatDistance(npcHips != null ? npcHips.position : npcSeat.VisualFeet, spot.NpcPlace.Position), Is.LessThan(.5f), "each body on its seat,");
                Assert.That(FlatDistance(myHips != null ? myHips.position : mySeat.VisualFeet, spot.PlayerPlace.Position), Is.LessThan(.5f), "not beside it,");
                Assert.That(FlatDistance(npc.transform.position, spot.NpcPlace.Approach), Is.LessThan(.3f), "while the roots wait on the approaches,");
                Assert.That(FlatDistance(player.transform.position, spot.PlayerStands), Is.LessThan(.45f), "the player's included.");
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(npcVisual.FacingYaw, spot.NpcPlace.Facing)), Is.LessThan(.01f), "Each faces the way its seat does.");
            }
            else
            {
                Assert.That(npcVisual.IsSeated || mine.IsSeated, Is.False, "At a standing place nobody sits.");
                Assert.That(FlatDistance(npc.transform.position, spot.NpcPlace.Approach), Is.LessThan(.3f), "Each stands on their mark,");
                Assert.That(FlatDistance(player.transform.position, spot.PlayerStands), Is.LessThan(.45f), "the player too,");
                var toPlayer = player.transform.position - npc.transform.position;
                float facing = Mathf.Atan2(toPlayer.x, toPlayer.z) * Mathf.Rad2Deg;
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(npcVisual.FacingYaw, facing)), Is.LessThan(1f), "and they face each other.");
            }

            director.ClosePanels();
            yield return null;
            yield return null;
            Assert.That(director.CurrentTalkSpot, Is.Null, "Closing the conversation lets the place go,");
            Assert.That(coordinator.Talk, Is.Null);
            Assert.That(spot.Released, Is.True);
            Assert.That(motion.LeaseId == null || !motion.LeaseId.StartsWith("talk:"), Is.True, "and the houseguest is the house's again.");
            if (spot.Seated)
            {
                Assert.That(npcSeat.IsExiting || !npcSeat.Active, Is.True, "Both get up at their seats:");
                Assert.That(mySeat.IsExiting || !mySeat.Active, Is.True, "the stand-up outlives the place.");
                float up = Time.realtimeSinceStartup + 2f;
                while ((npcSeat.Active || mySeat.Active) && Time.realtimeSinceStartup < up) { yield return null; Check("getting up"); }
                Assert.That(npcSeat.Active || mySeat.Active, Is.False, "and are up.");
            }
            Assert.That(air, Is.Empty, "A body sat with nothing under it:\n" + string.Join("\n", air));
        }

        /// <summary>
        /// Talk opens as it always did under reduced motion, in a batch run that does not ask, on a
        /// second press, and when the walk is called off: no place is held, and the walk is the
        /// walk over to them or the conversation at once.
        /// </summary>
        [UnityTest]
        public IEnumerator TalkSpots_TheOldWayOpensUnderReducedMotionUnaskedAndOnASecondPress()
        {
            yield return WaitForNpcRuntimeBinding();
            var coordinator = NpcRead<HouseMeetingCoordinator>("npcMeetings");
            var npc = FreeHouseguest(coordinator);
            void Reset() { director.ClosePanels(); NpcInvoke("CancelTravel"); player.StopHere(); }

            if (Application.isBatchMode)
            {
                AskForTheTalkSpots();
                director.TalkSpotsInBatchRuns = false;
                director.TalkFromCastMenu(npc.Id);
                yield return null;
                Assert.That(director.CurrentTalkSpot, Is.Null, "A batch run walks to no place unless asked: the audited walk and every test that talks see what they always saw.");
                Assert.That(coordinator.Talk, Is.Null);
                Assert.That(director.WalkingToId == npc.Id || director.TalkingToId == npc.Id, Is.True, "Talk walks over to them, or talks at once.");
                Reset();
                yield return null;
            }

            AskForTheTalkSpots(reduced: true);
            director.TalkFromCastMenu(npc.Id);
            yield return null;
            Assert.That(director.CurrentTalkSpot, Is.Null, "Reduced motion never walks to a place, whatever the run asked,");
            Assert.That(coordinator.Talk, Is.Null, "and the house holds nobody for one.");
            Assert.That(director.WalkingToId == npc.Id || director.TalkingToId == npc.Id, Is.True, "Talk walks over to them, or talks at once.");
            Reset();
            yield return null;

            AskForTheTalkSpots();
            director.TalkFromCastMenu(npc.Id);
            yield return null;
            var spot = director.CurrentTalkSpot;
            Assert.That(spot, Is.Not.Null, "Asked for, Talk sends them to a place: " + coordinator.LastFailure);
            director.TalkFromCastMenu(npc.Id);
            yield return null;
            Assert.That(director.CurrentTalkSpot, Is.Null, "Pressed again, nobody walks to the place any more:");
            Assert.That(spot.Released && coordinator.Talk == null, Is.True, "it is let go,");
            Assert.That(coordinator.TryGetMotion(npc.Id, out var motion) && motion.LeaseId != spot.Token, Is.True, "and the houseguest with it;");
            Assert.That(director.WalkingToId == npc.Id || director.TalkingToId == npc.Id, Is.True, "the conversation opens where they are, or after the walk over to them.");
            Reset();
            yield return null;

            director.TalkFromCastMenu(npc.Id);
            yield return null;
            spot = director.CurrentTalkSpot;
            Assert.That(spot, Is.Not.Null);
            director.GoToStation();
            yield return null;
            Assert.That(director.CurrentTalkSpot, Is.Null, "Asking for the episode screen calls the walk to a place off, as it calls off the walk to a houseguest,");
            Assert.That(spot.Released && coordinator.Talk == null, Is.True, "and lets the place go.");
            Assert.That(director.WalkingToId, Is.Null);
            Reset();
            yield return null;
        }

        /// <summary>
        /// A houseguest with nothing else to do, and the player put beside them where E names them:
        /// on the baked floor, in sight of them, with nobody nearer and nothing else E would do first.
        /// </summary>
        private HouseNpc BesideAFreeHouseguest(HouseMeetingCoordinator coordinator)
        {
            var free = SceneComponents<HouseNpc>()
                .Where(body => body.gameObject.activeInHierarchy && coordinator.TryGetMotion(body.Id, out var motion) && motion.LeaseId == null)
                .OrderBy(body => (body.transform.position - player.transform.position).sqrMagnitude).ToList();
            foreach (var npc in free)
                for (int direction = 0; direction < 8; direction++)
                {
                    float angle = direction * Mathf.PI / 4f;
                    var candidate = npc.transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.7f;
                    if (!NavMesh.SamplePosition(candidate, out var hit, .5f, player.Agent.areaMask) || !player.Agent.Warp(hit.position)) continue;
                    player.Agent.ResetPath();
                    Physics.SyncTransforms();
                    // What E would do from here, and to whom: the director's own choice, read through its seam.
                    var chosen = new object[] { null };
                    if (NpcInvoke("ChooseInteraction", chosen).ToString() == "Talk" && ReferenceEquals(chosen[0], npc)) return npc;
                }
            Assert.Fail("No place beside a houseguest with nothing else to do where E would talk to them.");
            return null;
        }

        /// <summary>
        /// E beside a houseguest says what a click on them says, and goes the same way: to a talk
        /// spot. Pressed again on the one already on the way, the place is let go and the
        /// conversation opens where they are, the walk to the place ended with it.
        /// </summary>
        [UnityTest]
        public IEnumerator TalkSpots_EBesideAHouseguestGoesToAPlaceAndPressedAgainTalksWhereTheyAre()
        {
            yield return WaitForNpcRuntimeBinding();
            AskForTheTalkSpots();
            var coordinator = NpcRead<HouseMeetingCoordinator>("npcMeetings");
            var npc = BesideAFreeHouseguest(coordinator);

            director.Interact();
            yield return null;
            var spot = director.CurrentTalkSpot;
            Assert.That(spot, Is.Not.Null, "E sends the two to a place to talk, as a click on them does: " + coordinator.LastFailure);
            Assert.That(spot.NpcId, Is.EqualTo(npc.Id));
            Assert.That(director.WalkingToId, Is.EqualTo(npc.Id), "The walk is the walk to them.");
            Assert.That(director.TalkingToId, Is.Null, "Nothing opens before both are there.");

            Assert.That((bool)NpcInvoke("CanTalk", npc), Is.True, "A frame on, they are still in reach of E.");
            director.Interact();
            yield return null;
            Assert.That(director.TalkingToId, Is.EqualTo(npc.Id), "Pressed again, E talks where they are,");
            Assert.That(director.CurrentTalkSpot, Is.Null, "nobody walks to the place any more,");
            Assert.That(spot.Released && coordinator.Talk == null, Is.True, "the place is let go,");
            Assert.That(director.WalkingToId, Is.Null, "and the walk with it:");
            Assert.That(player.Agent.hasPath, Is.False, "the player does not walk on to the place once the conversation closes.");
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// A saved living-room chat whose own pair somebody is standing on goes to another of the
        /// living room's places - the couches - and sits there; the save is written as it was read,
        /// and saved again it still names the living room's venue and never the place.
        /// </summary>
        [UnityTest]
        public IEnumerator TalkSpots_ASavedLivingRoomChatWhoseOwnPairIsTakenSitsOnTheCouchesAndSavesAsItWas()
        {
            var fixture = NpcPendingRuntimeFixture();
            Assert.That(fixture.npcSocial.pending.Single().rendezvousId, Is.EqualTo(HouseConversationSpots.LivingFamily));
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            yield return ReloadEpisode();
            var written = File.ReadAllBytes(director.SavePath);

            // Somebody stands on the living room's own pair before the house can send anybody there.
            var scene = player.gameObject.scene;
            Assert.That(HouseInteractionAnchors.TryFind(scene, HouseConversationSpots.LivingFamily, 0, out var own), Is.True);
            var occupant = new GameObject("Standing on the living room's own pair", typeof(CapsuleCollider));
            SceneManager.MoveGameObjectToScene(occupant, scene);
            var capsule = occupant.GetComponent<CapsuleCollider>();
            capsule.radius = .35f; capsule.height = 1.8f; capsule.center = Vector3.up * .9f;
            occupant.transform.position = own.Approach;
            Physics.SyncTransforms();
            try
            {
                yield return WaitForNpcRuntimeBinding();
                NpcInvoke("SetNpcWorldPaused", false);
                NpcInvoke("RestorePendingNpcMeetings");
                var coordinator = NpcRead<HouseMeetingCoordinator>("npcMeetings");
                var leases = NpcRead<Dictionary<long, HouseMeetingLease>>("npcPendingWorld");
                Assert.That(leases.TryGetValue(1, out var lease), Is.True,
                    "The saved chat reunites at another of its venue's places: " + coordinator.LastFailure);
                Assert.That(lease.VenueId, Is.EqualTo(HouseConversationSpots.LivingFamily), "It is still the living room's, as the save names it,");
                Assert.That(HouseConversationSpots.IsSpot(lease.SpotId), Is.True, "at one of the living room's talk spots,");
                Assert.That(lease.Seated, Is.True, "on the couches.");
                Assert.That(File.ReadAllBytes(director.SavePath), Is.EqualTo(written), "Nothing about the place is written: the save is the one loaded, byte for byte.");
                Assert.That(JsonUtility.ToJson(director.Snapshot.npcSocial), Is.EqualTo(JsonUtility.ToJson(fixture.npcSocial)));

                // The director ticks its own world every frame; ticking it again here would run the
                // twenty-second reunion clock at double speed and cancel the meeting mid-walk.
                float deadline = Time.realtimeSinceStartup + 40;
                while (!director.IsNpcConversationPhysicallyReady(1) && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(director.IsNpcConversationPhysicallyReady(1), Is.True, "The pair must reach the couches. " + director.NpcAutonomyDiagnostic);
                Assert.That(leases.TryGetValue(1, out var arrived) && ReferenceEquals(arrived, lease), Is.True, "The couches are still the pair's.");
                var bodies = SceneComponents<HouseNpc>();
                var pair = new[] { lease.FirstId, lease.SecondId }.Select(id => bodies.Single(body => body.Id == id)).ToArray();
                float settled = Time.realtimeSinceStartup + 4f;
                bool Sitting(HouseNpc body) => body.GetComponent<HouseSeatPresentation>() != null && body.GetComponent<HouseSeatPresentation>().Active
                    && body.GetComponent<CharacterPresentation>().IsSeated;
                while (!pair.All(Sitting) && Time.realtimeSinceStartup < settled) yield return null;
                foreach (var body in pair)
                {
                    Assert.That(Sitting(body), Is.True, body.DisplayName + " sits down.");
                    Assert.That(coordinator.TryGetSeat(body.Id, out var seat), Is.True);
                    Assert.That(seat.VenueId, Is.EqualTo(lease.SpotId), body.DisplayName + " sits on the spot's seat,");
                    Assert.That(HouseConversationSpots.OnFurniture(seat), Is.True, "which is on furniture:");
                    Assert.That(seat.transform.parent.name, Is.EqualTo(CeremonySets.LoungeFourName).Or.EqualTo(CeremonySets.LoungeThreeName), "a couch.");
                }

                director.SaveNow();
                Assert.That(director.Snapshot.npcSocial.pending.Single(row => row.sequence == 1).rendezvousId, Is.EqualTo(HouseConversationSpots.LivingFamily),
                    "Saved again, the conversation is still the living room's,");
                Assert.That(File.ReadAllText(director.SavePath), Does.Not.Contain(lease.SpotId), "and the save never names the place.");
                Assert.That(new EpisodeSaveStore(director.SavePath).TryLoad(out var reloaded, out var reason), Is.True, reason);
                AssertEquivalent(director.Snapshot, reloaded);
            }
            finally { Object.Destroy(occupant); }
        }
    }
}
