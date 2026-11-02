using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The bodies, the plates and the cameras' masks (ROOM-SCOPE-PLAN §3.2 c, e and f, RS4): while the
    /// scope is a room, a body is drawn in a viewed room, within 0.6 m of one, as a camera subject or
    /// named by a live prompt; a hidden body's renderer objects stand on its walker room's layer, which
    /// the main camera's mask leaves out, its plate is down and it cannot be picked, and it goes on
    /// animating, walking and sitting. The live feed draws the shell and its own room. RS4 makes no
    /// source live (RS6a turns on only the ceremonies' and competitions'), so each test holds the scope on, and reads the drawing a frame
    /// after it changes: the director applies it late in the frame, before the render.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>A walk and a run, in metres a second, on the real clock.</summary>
        private const float Rs4Walk = 2.2f, Rs4Run = 4f;

        /// <summary>
        /// The camera's subject is drawn on every frame as it goes through every doorway of the house,
        /// walking and running, on the real clock: the player in free play, and a followed houseguest
        /// with the player elsewhere. The view changes rooms at each doorway, and the subject never
        /// leaves the drawing.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomScope_TheSubjectIsDrawnOnEveryFrameThroughEachDoorway()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            yield return Rs4BodiesBuilt();
            var scope = ScopeOf(director);
            var found = ScopeDoorways(scope);
            // Each doorway once: the halves of the two corner doorways and their middles.
            var crossings = found.Where(c => c.From < c.To).ToList();
            crossings.AddRange(ScopeCornerDoorways(scope, found).Where((c, i) => i % 2 == 0));
            Assert.That(crossings.Count, Is.GreaterThanOrEqualTo(12), "The house's ten doorways, the corner ones by their halves and their middles.");
            ScopeParkHouseguests(scope);
            var guest = SceneComponents<HouseNpc>().First(npc => npc.isActiveAndEnabled);
            // Both carried by their transforms, as RS1's walk does: the bake closes the kitchen's door to
            // the private room, and the narrow doorways' middles are off the mesh.
            var agent = player.Agent;
            bool agentPlaces = agent.updatePosition;
            agent.ResetPath();
            agent.updatePosition = false;
            var guestAgent = guest.GetComponent<UnityEngine.AI.NavMeshAgent>();
            bool guestPlaces = guestAgent != null && guestAgent.updatePosition;
            if (guestAgent != null) guestAgent.updatePosition = false;
            var owner = new object();
            var failures = new List<string>();
            int frames = 0;
            director.HoldRoomScope(owner);
            try
            {
                foreach (var subject in new[] { player.transform, guest.transform })
                {
                    bool following = subject != player.transform;
                    if (following)
                    {
                        director.FollowHouseguest(guest.Id);
                        Assert.That(cameraRig.FocusedSubject, Is.SameAs(guest.transform), "The camera follows " + guest.DisplayName + ".");
                    }
                    foreach (var crossing in crossings)
                        foreach (float speed in new[] { Rs4Walk, Rs4Run })
                        {
                            // The player out of the way of a followed walk: in a room neither side of the doorway.
                            if (following) ScopePlace(player.transform, ScopeOpenFloor(scope, Rs4FarRoom(scope, crossing.Start)), 2f);
                            ScopePlace(subject, crossing.Start);
                            if (!following) cameraRig.FocusSubject(player.transform, false);
                            yield return null;
                            string who = (following ? guest.DisplayName : "the player") + " through " + crossing + " at " + speed + " m/s";
                            if (director.ViewedRooms != RoomSet.Of(crossing.From)) { failures.Add(who + ": the view starts on " + director.ViewedRooms); continue; }
                            yield return Rs4Carry(subject, crossing, speed, who, failures, () => frames++);
                        }
                    if (following) director.FollowHouseguest(guest.Id);
                }
            }
            finally
            {
                director.Release(owner);
                agent.updatePosition = agentPlaces;
                if (guestAgent != null) guestAgent.updatePosition = guestPlaces;
                cameraRig.ClearSubject();
            }
            Assert.That(frames, Is.GreaterThan(crossings.Count * 4), "Every crossing took frames to walk.");
            Assert.That(failures, Is.Empty, string.Join("; ", failures.Take(20)));
            Assert.That(director.HiddenBodyCount, Is.Zero, "Released: nobody is hidden.");
        }

        /// <summary>
        /// The camera's subjects are drawn wherever they stand, whatever rooms the view names: either
        /// of a conversation's two, and the houseguest the camera follows. Held on the yard, the player
        /// in the kitchen and a houseguest in the games room, in a two-shot, are both drawn; a third
        /// houseguest in the bedroom is hidden until the camera follows them. With the two-shot over,
        /// the houseguest no longer in it is hidden, and the player, in free play again, is drawn.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomScope_TheTwoShotsTwoAndTheFollowedAreDrawnWhereverTheyStand()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            yield return Rs4BodiesBuilt();
            var scope = ScopeOf(director);
            ScopeParkHouseguests(scope);
            var guests = SceneComponents<HouseNpc>().Where(npc => npc.gameObject.activeInHierarchy).ToArray();
            Assert.That(guests.Length, Is.GreaterThanOrEqualTo(2), "Two houseguests to place.");
            var partner = guests[0];
            var third = guests[1];
            ScopePlace(player.transform, ScopeOpenFloor(scope, RoomLayers.IndexOf("Kitchen")), 2f);
            ScopePlace(partner.transform, ScopeOpenFloor(scope, RoomLayers.IndexOf("Games")), 2f);
            ScopePlace(third.transform, ScopeOpenFloor(scope, RoomLayers.IndexOf("Bedroom")), 2f);
            var owner = new object();
            // Under reduced motion the two-shot takes no shot of its own: the view keeps naming the yard.
            cameraRig.SetReducedMotion(true);
            try
            {
                cameraRig.MoveTo(Rs4Shot(scope, "Yard"));
                director.HoldRoomScope(owner);
                cameraRig.SetConversationFocus(player.transform, partner.transform);
                yield return null;
                Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.Conversation), "The two-shot is the view.");
                Assert.That(director.ViewedRooms, Is.EqualTo(RoomSet.Of("Yard")), "and it names the yard, where neither of the two stands.");
                Assert.That(Rs4DrawnNow(player.transform, out string why), Is.True, "The player, one of the two, is drawn in the kitchen: " + why);
                Assert.That(director.IsBodyDrawn(player.transform), Is.True);
                Assert.That(Rs4DrawnNow(partner.transform, out why), Is.True, partner.DisplayName + ", the other, is drawn in the games room: " + why);
                Assert.That(partner.BodyHidden, Is.False);
                Assert.That(Rs4HiddenNow(third.transform, out why), Is.True, third.DisplayName + ", in neither, is hidden in the bedroom: " + why);

                // The camera follows the third through the two-shot: its subject, drawn where it stands.
                cameraRig.FocusSubject(third.transform);
                yield return null;
                Assert.That(cameraRig.FocusedSubject, Is.SameAs(third.transform), "The camera follows " + third.DisplayName + ".");
                Assert.That(director.ViewedRooms, Is.EqualTo(RoomSet.Of("Yard")), "The two-shot keeps the view's rooms.");
                Assert.That(Rs4DrawnNow(third.transform, out why), Is.True, "The followed houseguest is drawn in the bedroom: " + why);
                Assert.That(director.IsBodyDrawn(third.transform), Is.True);
                cameraRig.ClearSubject();

                // The two-shot over: the view still the yard, the houseguest hidden, the player drawn in free play.
                cameraRig.EndConversation();
                yield return null;
                Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.FreePlay), "Free play again.");
                Assert.That(director.ViewedRooms, Is.EqualTo(RoomSet.Of("Yard")), "The view the two-shot came from: the yard.");
                Assert.That(Rs4HiddenNow(partner.transform, out why), Is.True, partner.DisplayName + ", out of the two-shot, is hidden: " + why);
                Assert.That(Rs4HiddenNow(third.transform, out why), Is.True, third.DisplayName + ", no longer followed, is hidden: " + why);
                Assert.That(Rs4DrawnNow(player.transform, out why), Is.True, "The player in free play is drawn: " + why);
            }
            finally
            {
                cameraRig.EndConversation();
                director.Release(owner);
                cameraRig.ReleaseShot(0f);
                cameraRig.SetReducedMotion(false);
            }
        }

        /// <summary>
        /// A houseguest is drawn in the viewed room and in its doorway band, and hidden past it: walked
        /// from three metres into the bedroom to the living room, they are drawn from the frame their
        /// feet come within 0.6 m of the living room's floor, and not a frame before. Hidden, their
        /// renderer objects stand on their room's layer, which the main camera's mask leaves out; the
        /// root is never moved and no collider stands on a room layer. Released, everyone is drawn.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomScope_ABodyIsDrawnInItsRoomAndInItsDoorway()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            yield return Rs4BodiesBuilt();
            var scope = ScopeOf(director);
            ScopeParkHouseguests(scope);
            var guests = SceneComponents<HouseNpc>().Where(npc => npc.gameObject.activeInHierarchy).ToArray();
            Assert.That(guests.Length, Is.GreaterThanOrEqualTo(2), "Two houseguests to place.");
            var walker = guests[0];
            var far = guests[1];
            int living = RoomLayers.IndexOf("Living"), bedroom = RoomLayers.IndexOf("Bedroom");
            var door = ScopeDoorways(scope).First(c => c.From == bedroom && c.To == living);
            ScopePlace(player.transform, ScopeOpenFloor(scope, RoomLayers.IndexOf("Kitchen")), 2f);
            ScopePlace(far.transform, ScopeOpenFloor(scope, RoomLayers.IndexOf("Games")), 2f);
            ScopePlace(walker.transform, ScopeOpenFloor(scope, living), 2f);
            var roots = guests.ToDictionary(npc => npc, npc => npc.gameObject.layer);
            // Carried by its transform, so each step stands exactly where it is put.
            var walkerAgent = walker.GetComponent<UnityEngine.AI.NavMeshAgent>();
            bool walkerPlaces = walkerAgent != null && walkerAgent.updatePosition;
            if (walkerAgent != null) walkerAgent.updatePosition = false;
            var owner = new object();
            cameraRig.MoveTo(Rs4Shot(scope, "Living"));
            director.HoldRoomScope(owner);
            var failures = new List<string>();
            try
            {
                Assert.That(director.ViewedRooms, Is.EqualTo(RoomSet.Of("Living")), "The held scope is the living room.");
                yield return null;
                Assert.That(Rs4DrawnNow(walker.transform, out string drawnWhy), Is.True, "In the living room, drawn: " + drawnWhy);
                Assert.That(walker.BodyHidden, Is.False);
                Assert.That(director.IsBodyDrawn(walker.transform), Is.True);
                Assert.That(Rs4HiddenNow(far.transform, out string hiddenWhy), Is.True, "In the games room, hidden: " + hiddenWhy);
                Assert.That(Rs4PiecesOn(far.transform), Is.EqualTo(new[] { RoomLayers.Games }), "Every renderer object of a hidden body stands on its room's layer.");
                Assert.That(director.HiddenBodyLayer(far.transform), Is.EqualTo(RoomLayers.Games));
                Assert.That(cameraRig.ViewCamera.cullingMask & (1 << RoomLayers.Games), Is.Zero, "and the main camera's mask leaves that layer out.");
                Assert.That(far.BodyHidden, Is.True, "Its plate is down.");
                Assert.That(director.HiddenBodyCount, Is.GreaterThanOrEqualTo(1));
                Rs4NoColliderOnARoomLayer(guests, roots, "hidden");

                // A piece with a collider is not the scope's, and one whose collider goes a frame after it
                // was looked over - the primitive rig's parts lose theirs by a deferred Destroy - is read
                // again on the next frame and joins its body's room, with no change to the hierarchy.
                var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
                part.name = "Room scope RS4 part";
                part.transform.SetParent(far.transform, false);
                part.transform.localPosition = Vector3.up;
                part.transform.localScale = Vector3.one * .1f;
                try
                {
                    yield return null;
                    Assert.That(part.layer, Is.EqualTo(0), "A renderer object with a collider stays off the room layers.");
                    Object.DestroyImmediate(part.GetComponent<Collider>());
                    yield return null;
                    Assert.That(part.layer, Is.EqualTo(RoomLayers.Games), "Its collider gone a frame after the look, it is read again and stands on the room's layer.");
                }
                finally { Object.Destroy(part); }
                yield return null;

                // From three metres into the bedroom to a metre into the living room, five centimetres a frame.
                var start = door.Wall - door.Across * 3f;
                var end = door.Wall + door.Across;
                ScopePlace(walker.transform, start, 1f);
                yield return null;
                float firstDrawnAt = float.NaN;
                const int Steps = 80;
                for (int step = 0; step <= Steps; step++)
                {
                    ScopePlace(walker.transform, Vector3.Lerp(start, end, step / (float)Steps), .3f);
                    yield return null;
                    var feet = walker.transform.position;
                    float outside = HouseWhoIsWhere.OutsideBy(scope.Box(living), feet);
                    bool inRoom = director.WalkerRoomOf(walker.transform) == "Living";
                    bool owed = inRoom || outside <= HouseWhoIsWhere.DoorwayBand;
                    bool drawn = Rs4DrawnNow(walker.transform, out string why);
                    bool hidden = Rs4HiddenNow(walker.transform, out _);
                    if (owed && !drawn) failures.Add(outside.ToString("0.00") + " m off the living room's floor: not drawn (" + why + ")");
                    if (!owed && !hidden) failures.Add(outside.ToString("0.00") + " m off the living room's floor: not hidden (" + why + ")");
                    if (drawn && float.IsNaN(firstDrawnAt)) firstDrawnAt = outside;
                }
                Assert.That(failures, Is.Empty, string.Join("; ", failures.Take(20)));
                Assert.That(firstDrawnAt, Is.InRange(HouseWhoIsWhere.DoorwayBand - .1f, HouseWhoIsWhere.DoorwayBand),
                    "Drawn from the doorway band's edge, 0.6 m before the living room's floor, within one step.");
            }
            finally
            {
                director.Release(owner);
                cameraRig.ReleaseShot(0f);
                if (walkerAgent != null) walkerAgent.updatePosition = walkerPlaces;
            }
            // Released at once: everybody drawn, every piece on its own layer, the mask whole.
            foreach (var npc in guests)
            {
                Assert.That(Rs4DrawnNow(npc.transform, out string why), Is.True, npc.Id + ", released: " + why);
                Assert.That(npc.BodyHidden, Is.False);
            }
            // The house's own dressing stands on its rooms' layers from load (RS2a); a body's never does once drawn.
            Assert.That(SceneComponents<Renderer>().Where(r => RoomLayers.IsRoomLayer(r.gameObject.layer) && HouseRoomScope.IsBody(r.transform))
                .Select(r => r.name).ToArray(), Is.Empty, "Released, no body's renderer stands on a room layer.");
            Assert.That(director.MainCameraRoomBitsTaken, Is.Zero);
            Assert.That(cameraRig.ViewCamera.cullingMask & RoomLayers.Mask, Is.EqualTo(RoomLayers.Mask), "The main camera draws every room layer again.");
            Rs4NoColliderOnARoomLayer(guests, roots, "released");
        }

        /// <summary>
        /// A live prompt names only people the view draws: the Talk target beside the player, anyone a
        /// free-time card tags HERE, the walk-in pair while its Pull is on screen and the invitation's
        /// speaker are drawn while the prompt names them, with the view held on another room, and hidden
        /// again once it no longer does; a walk-in the player turned down names nobody, though the
        /// watch offers it again. The player, in free play, is drawn wherever the view is.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomScope_APromptNamesOnlyDrawnPeople()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            yield return Rs4BodiesBuilt();
            var scope = ScopeOf(director);
            ScopeParkHouseguests(scope);
            var guests = SceneComponents<HouseNpc>().Where(npc => npc.gameObject.activeInHierarchy).ToArray();
            Assert.That(guests.Length, Is.GreaterThanOrEqualTo(3), "Three houseguests to place.");
            int kitchen = RoomLayers.IndexOf("Kitchen");
            var owner = new object();
            try
            {
                // The Talk target: beside the player in the kitchen, the view on the games room.
                var target = guests[0];
                ScopePlace(player.transform, ScopeOpenFloor(scope, kitchen), 2f);
                Rs4PlaceToTalk(target);
                cameraRig.MoveTo(Rs4Shot(scope, "Games"));
                director.HoldRoomScope(owner);
                yield return WaitFor(() => NpcRead<HouseNpc>("promptNamed") == target, 3f, "The E prompt names " + target.DisplayName + ".");
                Assert.That(director.ViewedRooms, Is.EqualTo(RoomSet.Of("Games")), "The view is the games room.");
                Assert.That(director.WalkerRoomOf(target.transform), Is.EqualTo("Kitchen"), target.DisplayName + " is in the kitchen, which the view leaves out.");
                yield return null;
                Assert.That(Rs4DrawnNow(target.transform, out string why), Is.True, "The Talk target is drawn while the prompt names them: " + why);
                Assert.That(target.BodyHidden, Is.False, "plate and all");
                // The player in free play is the view's subject: drawn in the kitchen, the view on the games room.
                Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.FreePlay), "The player is in free play.");
                Assert.That(Rs4DrawnNow(player.transform, out why), Is.True, "The player, in free play, is drawn outside the view's room: " + why);
                Assert.That(director.IsBodyDrawn(player.transform), Is.True);
                // Out of reach: no prompt names them, and the view hides them.
                ScopePlace(target.transform, Rs4FarFloor(scope, player.transform.position, kitchen), 1.5f);
                yield return WaitFor(() => NpcRead<HouseNpc>("promptNamed") != target, 3f, "The prompt lets " + target.DisplayName + " go.");
                yield return null;
                Assert.That(Rs4HiddenNow(target.transform, out why), Is.True, "No prompt names them: hidden in the kitchen: " + why);

                // A free-time card's HERE names them, in the pass that reads it; the next render, which
                // draws the cards again, lets it go (closing the panels does too, and would let the shot go).
                NpcInvoke("TagHere", target.Id);
                NpcInvoke("ApplyRoomScopeDrawing");
                Assert.That(Rs4DrawnNow(target.transform, out why), Is.True, "Tagged HERE, drawn: " + why);
                Assert.That(director.IsBodyDrawn(target.transform), Is.True);
                Assert.That(target.BodyHidden, Is.False);
                NpcInvoke("RenderChrome");
                yield return null;
                Assert.That(director.ViewedRooms, Is.EqualTo(RoomSet.Of("Games")), "The view is still the games room.");
                Assert.That(Rs4HiddenNow(target.transform, out why), Is.True, "Drawn again with no card, no HERE: hidden again: " + why);

                // The walk-in pair: two in the HoH suite, which the view leaves out, offered as a walk-in.
                var first = guests[1];
                var second = guests[2];
                int hoh = RoomLayers.IndexOf("HoH");
                var suite = ScopeOpenFloor(scope, hoh);
                ScopePlace(first.transform, suite, 2f);
                ScopePlace(second.transform, suite + Vector3.right * .9f, 2f);
                yield return null;
                Assert.That(Rs4HiddenNow(first.transform, out why) && Rs4HiddenNow(second.transform, out _), Is.True, "In the HoH suite, both hidden: " + why);
                // Offered as the walk-in watch offers one; the watch's own next look, which would withdraw
                // it, held off for the test. A walk-in names its pair only while its Pull is on screen.
                Assert.That(Gamesim.Simulation.EpisodeEngine.StoryOn(director.Snapshot), Is.True, "A walk-in is offered as a Pull only under the story system.");
                NpcWrite("proximityWatch", -1e6f);
                NpcWrite("walkInFirst", first.Id);
                NpcWrite("walkInSecond", second.Id);
                NpcWrite("walkInRoom", "HoH");
                NpcWrite("walkInWeek", director.Snapshot.week);
                string PullKey() => NpcRead<EpisodeHud.StoryPull?>("pullNow")?.Key;
                yield return WaitFor(() => PullKey() != null && PullKey() == NpcRead<string>("pullWalkIn"), 3f, "The walk-in's Pull is on screen.");
                Assert.That(director.PullOffered, Is.EqualTo(EpisodeHud.StepInCaption));
                yield return null;
                Assert.That(Rs4DrawnNow(first.transform, out why) && Rs4DrawnNow(second.transform, out _), Is.True,
                    "The walk-in pair is drawn while its Pull is on screen: " + why);
                // Turned down: the Pull goes, and the pair, still the watch's offer, is hidden again.
                string declined = NpcRead<string>("pullWalkIn");
                NpcRead<HashSet<string>>("declinedPulls").Add(declined);
                yield return WaitFor(() => PullKey() != declined, 3f, "Turned down, the walk-in's Pull goes.");
                yield return null;
                Assert.That(NpcRead<string>("walkInFirst"), Is.EqualTo(first.Id), "The offer the watch made is still there,");
                Assert.That(Rs4HiddenNow(first.transform, out why) && Rs4HiddenNow(second.transform, out _), Is.True,
                    "but a walk-in turned down names nobody: hidden: " + why);
                NpcWrite("walkInFirst", null);
                NpcWrite("walkInSecond", null);
                NpcWrite("walkInRoom", null);
                NpcWrite("walkInWeek", 0);
                NpcWrite("proximityWatch", 0f);
                yield return null;
                Assert.That(Rs4HiddenNow(first.transform, out why) && Rs4HiddenNow(second.transform, out _), Is.True, "Withdrawn, hidden: " + why);
            }
            finally
            {
                director.Release(owner);
                cameraRig.ReleaseShot(0f);
            }

            // The invitation's speaker: a motivated houseguest comes to the player with an offer, the
            // bubble goes up, and the view, still framing them, names a room away from them both.
            yield return InstallNpcInvitationFlowHouse();
            director.EndOverview();
            yield return null;
            var speaker = SceneComponents<HouseNpc>().Single(person => person.Id == director.CurrentNpcInvitationNpcId);
            var scoped = ScopeOf(director);
            int away = Rs4FarRoom(scoped, speaker.transform.position, player.transform.position);
            var invitationOwner = new object();
            cameraRig.SetReducedMotion(true);
            try
            {
                // A cut, under reduced motion: the speaker stays in the frame, so the bubble's anchor is on the screen.
                cameraRig.MoveTo(new HouseCameraRig.Shot
                {
                    Focus = speaker.transform.position + Vector3.up * 1.6f, Distance = 5f, Pitch = 25f, Yaw = 0f, Rooms = RoomSet.Of(away),
                });
                director.HoldRoomScope(invitationOwner);
                Assert.That(director.ViewedRooms, Is.EqualTo(RoomSet.Of(away)), "The view names the " + RoomLayers.RoomIds[away] + ", away from both.");
                int up = 0;
                for (int frame = 0; frame < 6; frame++)
                {
                    yield return null;
                    if (!director.NpcInvitationIsVisible) continue;
                    up++;
                    Assert.That(Rs4DrawnNow(speaker.transform, out string why), Is.True,
                        "While the invitation is up, its speaker " + speaker.DisplayName + " is drawn: " + why);
                    Assert.That(speaker.BodyHidden, Is.False);
                }
                Assert.That(up, Is.GreaterThan(0), "The invitation stayed up with the view naming another room.");
            }
            finally
            {
                director.Release(invitationOwner);
                cameraRig.ReleaseShot(0f);
                cameraRig.SetReducedMotion(false);
            }
        }

        /// <summary>
        /// A hidden houseguest has no plate - down, and neither turned to the camera nor faded while
        /// hidden, then placed the frame they are drawn again - and cannot be picked: a click on them
        /// goes through to what is behind them, a seated one is not picked, and a body the view hides
        /// stands in the way of nothing. The invitation bubble and the conversation line are drawn only
        /// over drawn people: withheld, each is down while what it says carries on.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomScope_AHiddenHouseguestShowsNoPlateOrBubbleAndCannotBePicked()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            yield return Rs4BodiesBuilt();
            var scope = ScopeOf(director);
            ScopeParkHouseguests(scope);
            var guests = SceneComponents<HouseNpc>().Where(npc => npc.gameObject.activeInHierarchy).ToArray();
            Assert.That(guests.Length, Is.GreaterThanOrEqualTo(2));
            var guest = guests[0];
            var label = guest.GetComponentInChildren<TextMesh>(true);
            Assert.That(label, Is.Not.Null, "The houseguest carries a name label.");
            ScopePlace(player.transform, ScopeOpenFloor(scope, RoomLayers.IndexOf("Yard")), 2f);
            ScopePlace(guest.transform, ScopeOpenFloor(scope, RoomLayers.IndexOf("Games")), 2f);
            var eye = cameraRig.ViewCamera;
            var owner = new object();
            var host = new GameObject("Room scope RS4 bubble and caption");
            cameraRig.SetReducedMotion(true);
            try
            {
                HouseCameraRig.Shot Close(float yaw, string rooms) => new HouseCameraRig.Shot
                {
                    Focus = guest.transform.position + Vector3.up, Distance = 5f, Pitch = 25f, Yaw = yaw, Rooms = RoomSet.Of(rooms),
                };
                // Drawn: a close shot of them, naming their room.
                cameraRig.MoveTo(Close(0f, "Games"));
                director.HoldRoomScope(owner);
                yield return null; yield return null;
                Assert.That(Rs4DrawnNow(guest.transform, out string why), Is.True, "In the viewed room: drawn: " + why);
                Assert.That(guest.PlateAlpha, Is.GreaterThan(.5f), "Five metres off, the plate is up.");
                // Hidden: the same pose, naming the living room instead.
                cameraRig.MoveTo(Close(0f, "Living"));
                yield return null; yield return null;
                Assert.That(Rs4HiddenNow(guest.transform, out why), Is.True, "The view leaves their room out: hidden: " + why);
                Assert.That(guest.BodyHidden, Is.True);
                Assert.That(guest.PlateAlpha, Is.Zero, "Their plate is down, however close the camera.");
                var held = label.transform.rotation;
                cameraRig.MoveTo(Close(90f, "Living"));
                yield return null; yield return null;
                Assert.That(Quaternion.Angle(eye.transform.rotation, held), Is.GreaterThan(30f), "The camera has turned.");
                Assert.That(Quaternion.Angle(label.transform.rotation, held), Is.LessThan(.01f), "Hidden, the plate is not turned to the camera.");

                // A click on them goes through, to the floor or whatever stands behind them: a ray that
                // meets their body first, from a step away and over their head, down past them.
                Physics.SyncTransforms();
                var ray = Rs4RayAt(guest);
                var screen = (Vector2)eye.WorldToScreenPoint(guest.transform.position + Vector3.up);
                Assert.That(player.InputEnabled, Is.True, "The player's clicks are on.");
                Assert.That(HouseScopePick.Raycast(eye, ray, 500f, HouseLayers.Pick, out var through), Is.True, "Something stands behind them.");
                Assert.That(through.collider.transform.IsChildOf(guest.transform), Is.False, "The pick passes over the hidden body.");
                Assert.That(through.collider.GetComponentInParent<HouseWalkable>() != null, Is.True, "and lands on the floor behind them (D13).");
                Assert.That(HouseScopePick.Draws(eye, through.collider), Is.True, "which the camera draws.");
                Assert.That(player.TryPointAt(ray, out var pointed), Is.True, "The pointer's pick, the hover's, finds something there too:");
                Assert.That(pointed.collider.transform.IsChildOf(guest.transform), Is.False, "not the hidden body,");
                Assert.That(pointed.collider, Is.SameAs(through.collider), "but the floor the click lands on: the hover says what the click does.");
                player.DispatchClick(ray, screen);
                Assert.That(cameraRig.FocusedSubject, Is.Not.SameAs(guest.transform), "Clicking them does not follow them.");
                Assert.That(NpcRead<string>("headingToNpcId"), Is.Not.EqualTo(guest.Id), "nor walk over to talk to them.");
                Assert.That(director.TalkingToId, Is.Null, "nor open a conversation.");
                // The click lands on the floor behind them, and walks the player there (D13).
                Assert.That(player.Agent.hasPath, Is.True, "The click on the floor behind them walks the player.");
                Assert.That(FlatDistance(player.Agent.pathEndPosition, through.point), Is.LessThan(.5f),
                    "to where it landed: " + FlatDistance(player.Agent.pathEndPosition, through.point).ToString("0.00") + " m off it.");
                // Stopped and put back in the yard, out of reach of the spot they were walking to, which the
                // houseguest stands on again below; and the camera lets them go.
                ScopePlace(player.transform, ScopeOpenFloor(scope, RoomLayers.IndexOf("Yard")), 2f);
                cameraRig.ClearSubject();

                // Drawn again: the plate is placed that frame, and the same ray picks them.
                cameraRig.MoveTo(Close(90f, "Games"));
                yield return null;
                Assert.That(Rs4DrawnNow(guest.transform, out why), Is.True, why);
                Assert.That(guest.PlateAlpha, Is.GreaterThan(.5f), "Drawn again, the plate is up.");
                Assert.That(Quaternion.Angle(label.transform.rotation, eye.transform.rotation), Is.LessThan(.5f), "and turned to the camera on the frame it is drawn.");
                Physics.SyncTransforms();
                Assert.That(HouseScopePick.Raycast(eye, ray, 500f, HouseLayers.Pick, out var picked) && picked.collider.transform.IsChildOf(guest.transform), Is.True,
                    "Drawn, the same click picks them.");
                Assert.That(player.TryPointAt(ray, out var pointedDrawn) && pointedDrawn.collider.transform.IsChildOf(guest.transform), Is.True,
                    "and so does the pointer's pick.");

                // Seated: hidden, not picked, though the pick without a camera still finds them.
                cameraRig.MoveTo(Close(90f, "Living"));
                var seat = Rs4SeatAnchor(scope, out int seatRoom);
                yield return Rs4Seat(guest, seat);
                var seatView = RoomLayers.RoomIds[Rs4FarRoom(scope, seat.Position)];
                cameraRig.MoveTo(Rs4Shot(scope, seatView));
                yield return null; yield return null;
                var sitting = guest.GetComponent<HouseSeatPresentation>();
                // From the seat's own camera point, the one it is framed from, at the sitter's chest.
                var at = sitting.VisualFocus - Vector3.up * .3f;
                var seatRay = new Ray(seat.CameraPosition, (at - seat.CameraPosition).normalized);
                Physics.SyncTransforms();
                Assert.That(Rs4HiddenNow(guest.transform, out why), Is.True, "Seated in the " + RoomLayers.RoomIds[seatRoom] + " with the view on the " + seatView + ": hidden: " + why);
                Assert.That(HouseSeatPresentation.TryPickNpc(guest.gameObject.scene, seatRay, out var bare) && bare == guest, Is.True,
                    "Without a camera the seated pick finds them: the ray is on their body.");
                Assert.That(HouseSeatPresentation.TryPickNpc(guest.gameObject.scene, seatRay, eye, out _), Is.False, "Hidden, they cannot be picked seated.");
                cameraRig.MoveTo(Rs4Shot(scope, RoomLayers.RoomIds[seatRoom]));
                yield return null;
                Assert.That(HouseSeatPresentation.TryPickNpc(guest.gameObject.scene, seatRay, eye, out var seatedPick) && seatedPick == guest, Is.True,
                    "Drawn, the seated pick finds them.");
                sitting.End();

                // The director's line over a pair: withheld while one of them is hidden.
                var caption = NpcRead<HouseConversationCaption>("npcCaption");
                cameraRig.MoveTo(Close(90f, "Living"));
                ScopePlace(guest.transform, ScopeOpenFloor(scope, RoomLayers.IndexOf("Games")), 2f);
                yield return null;
                Assert.That(caption, Is.Not.Null, "The house's conversation line, built with its world at load.");
                NpcWrite("captionFirstId", guest.Id);
                NpcWrite("captionSecondId", guests[1].Id);
                yield return null;
                Assert.That(NpcRead<HouseNpc>("promptNamed"), Is.Not.SameAs(guest), "No prompt names " + guest.DisplayName + ".");
                Assert.That(Rs4HiddenNow(guest.transform, out why), Is.True, guest.DisplayName + " is hidden: " + why);
                Assert.That(caption.PairDrawn, Is.False, "The house's conversation line waits while " + guest.DisplayName + " is hidden.");
                NpcWrite("captionFirstId", null);
                NpcWrite("captionSecondId", null);
                yield return null;
                Assert.That(caption.PairDrawn, Is.True, "With no pair to wait for, it is drawn.");

                // The bubble and the line themselves.
                var anchor = eye.transform.position + eye.transform.forward * 4f;
                var bubble = host.AddComponent<HouseNpcInvitationBubble>();
                bubble.Show("Maya", "A word, when you have a minute?", 1f, anchor, () => { }, () => { });
                Assert.That(bubble.IsVisible, Is.True, "A bubble over a drawn speaker is up.");
                bubble.SpeakerDrawn = false;
                Assert.That(bubble.IsVisible, Is.False, "Over a hidden speaker it is down, at once.");
                bubble.Show("Maya", "A word, when you have a minute?", 1f, anchor, () => { }, () => { });
                yield return null;
                Assert.That(bubble.IsVisible, Is.False, "and stays down through its refresh and a frame.");
                bubble.SpeakerDrawn = true;
                Assert.That(bubble.IsVisible, Is.True, "Drawn again, up at once.");
                // New words shown while the speaker is withheld are fitted the moment the bubble comes up,
                // not left at the last invitation's size.
                var bubblePanel = host.GetComponentsInChildren<RectTransform>(true).First(rect => rect.name == HouseNpcInvitationBubble.PanelName);
                var shortSize = bubblePanel.sizeDelta;
                const string Longer = "I have been thinking about the vote all week, and before the ceremony I would really like to talk through "
                    + "who we trust, who we do not, and what the two of us do next if it goes the wrong way.";
                bubble.SpeakerDrawn = false;
                bubble.Show("Maya", Longer, 1f, anchor, () => { }, () => { });
                Assert.That(bubble.IsVisible, Is.False, "Withheld, the new words wait.");
                bubble.SpeakerDrawn = true;
                Assert.That(bubble.IsVisible, Is.True);
                Assert.That(bubblePanel.sizeDelta.y, Is.GreaterThan(shortSize.y + 1f), "Up again, the bubble is fitted to its longer words: " + bubblePanel.sizeDelta + ".");
                // The size a bubble shown those words with its speaker drawn has, its canvas sized a frame first.
                var fresh = new GameObject("Room scope RS4 bubble, fitted fresh");
                try
                {
                    var fitted = fresh.AddComponent<HouseNpcInvitationBubble>();
                    fitted.Show("Maya", "A word, when you have a minute?", 1f, anchor, () => { }, () => { });
                    yield return null;
                    fitted.Show("Maya", Longer, 1f, anchor, () => { }, () => { });
                    var fittedPanel = fresh.GetComponentsInChildren<RectTransform>(true).First(rect => rect.name == HouseNpcInvitationBubble.PanelName);
                    Assert.That(Vector2.Distance(bubblePanel.sizeDelta, fittedPanel.sizeDelta), Is.LessThan(.5f),
                        "the size of its new words: " + bubblePanel.sizeDelta + " against " + fittedPanel.sizeDelta + " fitted with its speaker drawn.");
                }
                finally { Object.Destroy(fresh); }
                var line = host.AddComponent<HouseConversationCaption>();
                line.Show("Maya", "Riley", "strategy", 1f, anchor);
                var panel = host.GetComponentsInChildren<RectTransform>(true).First(rect => rect.name == HouseConversationCaption.PanelName);
                string words = HouseConversationCaption.Describe("Maya", "Riley", "strategy");
                Assert.That(panel.gameObject.activeSelf, Is.True);
                line.PairDrawn = false;
                Assert.That(panel.gameObject.activeSelf, Is.False, "Over a hidden pair the line is down.");
                Assert.That(line.CurrentText, Is.EqualTo(words), "What it says carries on: the Nearby card and the feed read it.");
                yield return null;
                Assert.That(panel.gameObject.activeSelf, Is.False);
                line.PairDrawn = true;
                Assert.That(panel.gameObject.activeSelf, Is.True, "Drawn again, up at once.");
            }
            finally
            {
                director.Release(owner);
                cameraRig.ReleaseShot(0f);
                cameraRig.SetReducedMotion(false);
                Object.Destroy(host);
            }
        }

        /// <summary>
        /// A houseguest seated while hidden is on the seat on the first frame their room is drawn: the
        /// seat fit runs on live bones whether or not the view draws them. Hidden, only their renderer
        /// objects move to the room's layer: the root keeps the furniture layer parking gave it.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomScope_TheSeatedArriveSeated()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            yield return Rs4BodiesBuilt();
            var scope = ScopeOf(director);
            ScopeParkHouseguests(scope);
            var guest = SceneComponents<HouseNpc>().First(npc => npc.gameObject.activeInHierarchy);
            var seat = Rs4SeatAnchor(scope, out int seatRoom);
            int away = Rs4FarRoom(scope, seat.Position);
            ScopePlace(player.transform, ScopeOpenFloor(scope, Rs4FarRoom(scope, seat.Position, ScopeCentre(scope, RoomLayers.RoomIds[away]))), 2f);
            var owner = new object();
            try
            {
                cameraRig.MoveTo(Rs4Shot(scope, RoomLayers.RoomIds[away]));
                director.HoldRoomScope(owner);
                yield return Rs4Seat(guest, seat);
                var sitting = guest.GetComponent<HouseSeatPresentation>();
                Assert.That(sitting.Settled, Is.True, guest.DisplayName + " sat down while hidden.");
                Assert.That(Rs4HiddenNow(guest.transform, out string why), Is.True, "Seated in the " + RoomLayers.RoomIds[seatRoom] + ", the view elsewhere: hidden: " + why);
                Assert.That(RoomLayers.IsRoomLayer(guest.gameObject.layer), Is.False, "The root is never on a room layer: parking's layer stays.");
                Assert.That(guest.GetComponentsInChildren<Collider>(true).Where(c => RoomLayers.IsRoomLayer(c.gameObject.layer)).Select(c => c.name).ToArray(), Is.Empty,
                    "No collider stands on a room layer.");

                // The first frame their room is drawn, they are on the seat.
                cameraRig.MoveTo(Rs4Shot(scope, RoomLayers.RoomIds[seatRoom]));
                yield return null;
                Assert.That(Rs4DrawnNow(guest.transform, out why), Is.True, "The seat's room in view: drawn on the first frame: " + why);
                Assert.That(sitting.Active && sitting.Settled, Is.True, "and still seated.");
                var hips = HipsOf(guest);
                var on = hips != null ? hips.position : sitting.VisualFeet;
                Assert.That(FlatDistance(on, seat.Position), Is.LessThan(.5f),
                    guest.DisplayName + "'s " + (hips != null ? "hips are" : "body is") + " on the seat: " + FlatDistance(on, seat.Position).ToString("0.00") + " m off it.");
                sitting.End();
            }
            finally
            {
                director.Release(owner);
                cameraRig.ReleaseShot(0f);
            }
        }

        /// <summary>
        /// The main camera's mask is the scope's (§3.2 c): bits 20 to 27 the view's, among the room bits
        /// the camera had when the scope began, from the hold's own call and at every pass after; every
        /// bit outside them as it was, a capture's guard bit cleared by hand included; a room bit cleared
        /// before the hold stays clear throughout and after; a viewed room's bit cleared under the hold,
        /// or a stale mask a capture saved and put back across a move, is the view's again at the next
        /// pass, and at the next render by hand before it; a whole-house hold puts every kept room back
        /// at once; and a render by hand straight after a move draws the new view. Released, the mask is
        /// as it was before the hold.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomScope_TheMainCameraLeavesOutOnlyTheRoomsOutsideTheScope()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            var scope = ScopeOf(director);
            var view = cameraRig.ViewCamera;
            int authored = view.cullingMask;
            Assert.That(authored & RoomLayers.Mask, Is.EqualTo(RoomLayers.Mask), "With nothing held the main camera draws every room layer.");
            Assert.That(director.MainCameraRoomBitsTaken, Is.Zero);
            const int Guard = 1 << 31;
            const int Bedroom = 1 << RoomLayers.Bedroom;
            int Rooms() => view.cullingMask & RoomLayers.Mask;
            // What the camera had when the scope began: every room bit but the bedroom's.
            const int Kept = RoomLayers.Mask & ~Bedroom;
            var owner = new object();
            var capture = new object();
            var inRender = new List<int>();
            void Look(ScriptableRenderContext context, Camera camera)
            {
                if (camera == view) inRender.Add(camera.cullingMask & RoomLayers.Mask);
            }
            // Before the hold: a capture lens's guard bit, cleared by its owner, and the bedroom's bit,
            // cleared by something else - a test's, the probe's.
            view.cullingMask = authored & ~Guard & ~Bedroom;
            try
            {
                cameraRig.MoveTo(Rs4Shot(scope, "Kitchen"));
                director.HoldRoomScope(owner);
                Assert.That(Rooms(), Is.EqualTo(1 << RoomLayers.Kitchen), "Held on the kitchen, at once: its room bit alone of the eight.");
                Assert.That(view.cullingMask & ~RoomLayers.Mask, Is.EqualTo(authored & ~Guard & ~RoomLayers.Mask), "Every other bit as it was, the guard bit's clear included.");
                Assert.That(director.MainCameraRoomBitsTaken, Is.EqualTo(Kept & ~(1 << RoomLayers.Kitchen)), "Out: the kept rooms but the kitchen.");
                yield return null;
                Assert.That(Rooms(), Is.EqualTo(1 << RoomLayers.Kitchen), "Between frames the mask stays the scope's.");

                cameraRig.MoveTo(new HouseCameraRig.Shot
                {
                    Focus = ScopeCentre(scope, "Kitchen"), Distance = 14f, Pitch = 45f, Yaw = 0f, Rooms = RoomSet.Of("Kitchen", "Yard"),
                });
                yield return null;
                Assert.That(Rooms(), Is.EqualTo(RoomSet.Of("Kitchen", "Yard").CullingBits), "Two rooms named: both bits.");

                // The bedroom's bit was out before the scope began: not the scope's to put back, even for its own view.
                cameraRig.MoveTo(Rs4Shot(scope, "Bedroom"));
                yield return null;
                Assert.That(Rooms(), Is.Zero, "The bedroom's view: its bit, cleared before the hold, stays clear, and every other room is out.");

                // A viewed room's bit cleared by hand under the hold is the view's again at the next pass.
                cameraRig.MoveTo(Rs4Shot(scope, "Living"));
                yield return null;
                Assert.That(Rooms(), Is.EqualTo(1 << RoomLayers.Living), "The living room's view.");
                view.cullingMask &= ~(1 << RoomLayers.Living);
                yield return null;
                Assert.That(Rooms(), Is.EqualTo(1 << RoomLayers.Living), "Cleared by hand under the hold, the living room's bit is the view's again a frame on.");

                // A whole-house hold wins, at once: every kept room back, the bedroom still out.
                director.HoldWholeHouse(capture);
                Assert.That(Rooms(), Is.EqualTo(Kept), "A whole-house hold: every room bit the camera had back.");
                Assert.That(director.MainCameraRoomBitsTaken, Is.Zero);
                director.Release(capture);
                Assert.That(Rooms(), Is.EqualTo(1 << RoomLayers.Living), "Let go, the scope again.");

                // A render by hand straight after a move, before the frame's own pass: the new view. A
                // capture saved the mask before the move and puts it back after its render: the stale
                // living room's mask goes over the scope's. The next render by hand, in the same frame,
                // and the next pass both draw the games room's view again.
                RenderPipelineManager.beginCameraRendering += Look;
                try
                {
                    int saved = view.cullingMask;
                    cameraRig.MoveTo(Rs4Shot(scope, "Games"));
                    LampScopeRenderByHand(view);
                    view.cullingMask = saved;
                    Assert.That(Rooms(), Is.EqualTo(1 << RoomLayers.Living), "The capture put its stale mask back.");
                    LampScopeRenderByHand(view);
                }
                finally { RenderPipelineManager.beginCameraRendering -= Look; }
                Assert.That(inRender.Count, Is.GreaterThanOrEqualTo(2), "Both renders by hand reached their begin.");
                Assert.That(inRender[0], Is.EqualTo(1 << RoomLayers.Games), "The first render saw the games room's view, drawn at its begin.");
                Assert.That(inRender[inRender.Count - 1], Is.EqualTo(1 << RoomLayers.Games), "and so did the one after the stale mask was put back.");
                view.cullingMask = (view.cullingMask & ~RoomLayers.Mask) | (1 << RoomLayers.Living);
                yield return null;
                Assert.That(Rooms(), Is.EqualTo(1 << RoomLayers.Games), "A stale mask put back between frames: the next pass draws the games room's view.");
                Assert.That(director.MainCameraRoomBitsTaken, Is.EqualTo(Kept & ~(1 << RoomLayers.Games)), "and the scope still has the others out.");
            }
            finally
            {
                director.Release(owner);
                director.Release(capture);
                cameraRig.ReleaseShot(0f);
            }
            Assert.That(Rooms(), Is.EqualTo(Kept), "Released: every room bit the camera had back, the bedroom's still clear.");
            Assert.That(director.MainCameraRoomBitsTaken, Is.Zero);
            Assert.That(view.cullingMask & Guard, Is.Zero, "The guard bit is still its owner's.");
            view.cullingMask = authored;
        }

        /// <summary>
        /// The live feed draws the shell and its own room (§3.2 c, D10): its camera's mask holds the
        /// room it looks into and no other room layer, and a houseguest the main view hides in that
        /// room - on that room's layer - is in the feed's drawing and out of the main camera's. With the
        /// house's dressing on its rooms' layers (RS2a), the feed's pixels: a piece of dressing in its
        /// room is drawn and one in the room next door, in its frame, is not, until the feed is aimed
        /// with both rooms.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomScope_TheLiveFeedDrawsItsOwnRoom()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            yield return Rs4BodiesBuilt();
            var scope = ScopeOf(director);
            ScopeParkHouseguests(scope);
            var feed = Object.FindAnyObjectByType<LiveFeed>();
            Assert.That(feed, Is.Not.Null, "The director attaches the live feed.");
            int bedroom = RoomLayers.IndexOf("Bedroom");
            var box = scope.Box(bedroom);
            ScopePlace(player.transform, new Vector3(box.center.x - 3f, 0f, box.center.y), 2.5f);
            var guest = SceneComponents<HouseNpc>().First(npc => npc.gameObject.activeInHierarchy);
            ScopePlace(guest.transform, new Vector3(box.center.x + 3f, 0f, box.center.y), 2.5f);
            Assert.That(Vector3.Distance(player.transform.position, guest.transform.position), Is.GreaterThan(3f), "Out of Talk's reach: no prompt names them.");
            var owner = new object();
            try
            {
                cameraRig.MoveTo(Rs4Shot(scope, "Games"));
                director.HoldRoomScope(owner);
                // The feed looks into the player's room (no scene, conversation or ceremony here).
                yield return WaitFor(() => feed.Rooms == RoomSet.Of(bedroom), 3f, "The feed is aimed into the bedroom, the player's room.");
                Assert.That(director.TryGetLiveFeedSubject(out _, out _, out var rooms), Is.True);
                Assert.That(rooms, Is.EqualTo(RoomSet.Of(bedroom)), "The feed's subject names its room.");
                int revision = feed.CaptureRevision;
                yield return WaitFor(() => feed.CaptureRevision != revision, 3f, "The feed renders.");
                int mask = feed.Camera.cullingMask;
                Assert.That(mask & RoomLayers.Mask, Is.EqualTo(1 << RoomLayers.Bedroom), "The feed's camera draws the bedroom's layer alone of the eight.");
                Assert.That(mask | RoomLayers.Mask, Is.EqualTo(-1), "and the shell: every other layer.");
                Assert.That(Rs4HiddenNow(guest.transform, out string why), Is.True, "The main view, on the games room, hides " + guest.DisplayName + ": " + why);
                Assert.That(Rs4PiecesOn(guest.transform), Is.EqualTo(new[] { RoomLayers.Bedroom }), "on the bedroom's layer,");
                Assert.That(Rs4DrawnNow(player.transform, out why), Is.True, "The player, in free play, is drawn in the bedroom whatever room the view names: " + why);
                Assert.That(director.IsBodyDrawn(player.transform), Is.True);
                Assert.That(mask & (1 << RoomLayers.Bedroom), Is.Not.Zero, "which the feed draws.");
                Assert.That(feed.Camera.cullingMask, Is.Not.EqualTo(cameraRig.ViewCamera.cullingMask), "The feed's mask is its own.");
                yield return Rs2aFeedDrawsItsOwnRoomsDressing(feed, scope);
            }
            finally
            {
                director.Release(owner);
                cameraRig.ReleaseShot(0f);
            }
            Assert.That(Rs4DrawnNow(guest.transform, out string drawnWhy), Is.True, "Released, drawn by the main camera again: " + drawnWhy);
        }

#if GAMESIM_UMA
        // UMA bodies: an Animator, humanoid bones and a change of clothes, which the primitive rig has none of.

        /// <summary>
        /// A hidden body keeps its pose live (§3.2 e, D9): walking in a room the view leaves out, every
        /// house body's Animator always animates, and the hidden one's legs swing as it goes.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomScope_AHiddenBodyKeepsItsPoseLive()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            yield return Rs4BodiesBuilt();
            var scope = ScopeOf(director);
            ScopeParkHouseguests(scope);
            var bodies = SceneComponents<HouseNpc>().Where(npc => npc.gameObject.activeInHierarchy).ToArray();
            var animators = bodies.Select(npc => npc.GetComponentsInChildren<Animator>(true).FirstOrDefault(rig => rig.isHuman)).ToArray();
            Assert.That(bodies.Length, Is.GreaterThan(0), "Houseguests to read.");
            Assert.That(animators.Count(rig => rig != null), Is.EqualTo(bodies.Length), "A humanoid Animator a body: the primitive rig has none, and would pass this on nothing.");
            foreach (var rig in animators)
                Assert.That(rig.cullingMode, Is.EqualTo(AnimatorCullingMode.AlwaysAnimate), rig.transform.root.name + "'s Animator culls when unseen.");
            var walker = bodies[0];
            var thigh = animators[0].GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            Assert.That(thigh, Is.Not.Null, "The body has a left thigh.");
            int games = RoomLayers.IndexOf("Games");
            var box = scope.Box(games);
            var from = new Vector3(box.center.x - 2f, 0f, box.center.y);
            var to = new Vector3(box.center.x + 2f, 0f, box.center.y);
            // Carried by its transform, so the agent never pulls it back; the walk cycle reads the
            // transform's speed over Time.deltaTime, so the clock is pinned: a batchmode frame is half a
            // millisecond, which reads as standing still.
            var walkerAgent = walker.GetComponent<UnityEngine.AI.NavMeshAgent>();
            bool walkerPlaces = walkerAgent != null && walkerAgent.updatePosition;
            if (walkerAgent != null) { walkerAgent.ResetPath(); walkerAgent.updatePosition = false; }
            ScopePlace(walker.transform, from, 2f);
            var owner = new object();
            try
            {
                cameraRig.MoveTo(Rs4Shot(scope, "Living"));
                director.HoldRoomScope(owner);
                yield return null;
                Assert.That(Rs4HiddenNow(walker.transform, out string why), Is.True, walker.DisplayName + " in the games room is hidden: " + why);
                var rest = thigh.localRotation;
                float swing = 0f;
                int hiddenFrames = 0, frames = 0;
                // Back and forth across the room at a walk, on game time: 75 frames of a thirtieth of a second.
                Time.captureDeltaTime = 1f / 30f;
                float along = 0f, heading = 1f;
                float span = Vector3.Distance(from, to);
                for (int frame = 0; frame < 75; frame++)
                {
                    along += heading * 1.4f * Time.deltaTime;
                    if (along > span) { along = span; heading = -1f; }
                    if (along < 0f) { along = 0f; heading = 1f; }
                    walker.transform.position = Vector3.Lerp(from, to, along / span);
                    yield return null;
                    frames++;
                    if (Rs4HiddenNow(walker.transform, out _)) hiddenFrames++;
                    swing = Mathf.Max(swing, Quaternion.Angle(rest, thigh.localRotation));
                }
                Assert.That(hiddenFrames, Is.EqualTo(frames), "Hidden on every frame of the walk.");
                Assert.That(animators[0].isActiveAndEnabled, Is.True, "Its Animator runs.");
                Assert.That(animators[0].cullingMode, Is.EqualTo(AnimatorCullingMode.AlwaysAnimate));
                Assert.That(swing, Is.GreaterThan(8f), "The hidden body's thigh swings as it walks: " + swing.ToString("0.0") + " degrees at most.");
            }
            finally
            {
                Time.captureDeltaTime = 0f;
                director.Release(owner);
                cameraRig.ReleaseShot(0f);
                if (walkerAgent != null) walkerAgent.updatePosition = walkerPlaces;
            }
        }

        /// <summary>
        /// A change of clothes keeps a hidden body in its room (§3.2 e): through the build every
        /// renderer drawn at a size stays off the main camera's drawing, and from the frame the new look
        /// takes over every renderer object of it stands on the room's layer.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomScope_AnOutfitChangeKeepsTheBodyInItsRoom()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            yield return Rs4BodiesBuilt();
            var scope = ScopeOf(director);
            ScopeParkHouseguests(scope);
            var guest = SceneComponents<HouseNpc>().First(npc => npc.gameObject.activeInHierarchy);
            var presentation = guest.GetComponent<CharacterPresentation>();
            ScopePlace(guest.transform, ScopeOpenFloor(scope, RoomLayers.IndexOf("Games")), 2f);
            var modular = CharacterBodySource.Provider as IModularCharacterBodyProvider;
            Assert.That(modular?.Catalog, Is.Not.Null, "The UMA provider's wardrobe.");
            var draft = director.Snapshot.Find(presentation.CharacterId).Clone();
            draft.appearance = modular.Catalog.Materialize(draft.appearance);
            var item = modular.Catalog.Items.FirstOrDefault(value => value.Fits(draft.appearance.bodyId)
                && !AppearanceEditing.IsCharacterSlot(value.Slot)
                && !AppearanceEditing.Outfit(draft.appearance).wardrobe.Any(worn => value.Matches(worn.itemId)));
            Assert.That(item, Is.Not.Null, "A garment they are not wearing.");
            AppearanceEditing.Wear(draft.appearance, item, modular.Catalog);
            Assert.That(draft.appearance.TryValidate(out var invalid), Is.True, invalid);
            var owner = new object();
            var failures = new List<string>();
            try
            {
                cameraRig.MoveTo(Rs4Shot(scope, "Living"));
                director.HoldRoomScope(owner);
                yield return null;
                Assert.That(Rs4HiddenNow(guest.transform, out string why), Is.True, guest.DisplayName + " in the games room is hidden: " + why);
                var before = presentation.VisualRoot;
                CharacterPresentation.Dress(guest.gameObject, draft, Color.white); // a copied recipe, never the saved contestant
                Assert.That(presentation.IsChangingOutfit, Is.True, "A new look is being built.");
                float deadline = Time.realtimeSinceStartup + CharacterPresentation.AssemblyTimeoutSeconds + 2f;
                while (presentation.VisualRoot == before && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                    if (!Rs4HiddenAtSize(guest.transform, out why)) failures.Add("building: " + why);
                }
                Assert.That(presentation.VisualRoot, Is.Not.SameAs(before), "The new look took over.");
                float watch = Time.realtimeSinceStartup + .75f;
                bool first = true;
                do
                {
                    var on = Rs4PiecesOn(guest.transform);
                    if (!on.SequenceEqual(new[] { RoomLayers.Games })) failures.Add((first ? "the first frame of the new look" : "after it") + ": layers " + string.Join(", ", on));
                    first = false;
                    yield return null;
                } while (Time.realtimeSinceStartup < watch);
                Assert.That(failures, Is.Empty, string.Join("; ", failures.Take(10)));
            }
            finally
            {
                director.Release(owner);
                cameraRig.ReleaseShot(0f);
            }
            Assert.That(Rs4DrawnNow(guest.transform, out string drawn), Is.True, "Released, the new look is drawn: " + drawn);
        }

        /// <summary>Hidden, counting only what is drawn at a size: a look being built stands at no size behind the body on show.</summary>
        private bool Rs4HiddenAtSize(Transform body, out string why)
        {
            int mask = cameraRig.ViewCamera.cullingMask;
            var shown = body.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.transform.lossyScale.sqrMagnitude > 1e-6f).ToArray();
            var drawn = shown.Where(r => (mask & (1 << r.gameObject.layer)) != 0).Select(r => r.name + " on " + r.gameObject.layer).ToArray();
            why = drawn.Length == 0 ? "" : string.Join(", ", drawn) + " drawn";
            return shown.Length > 0 && drawn.Length == 0;
        }
#endif

        // ------------------------------------------------------------------------------------
        // Helpers.
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// The feed's first pixel test (RS2a): aimed into the bedroom from over the living room, its eye
        /// a few metres south of the bedroom's wall, two unlit markers placed as dressing by where they
        /// stand - one in the bedroom on the feed's line of sight, over the furniture and past the wall's
        /// top, one in the living room just in front of the eye and to its side. Aimed with the bedroom
        /// alone, the feed draws the bedroom's marker and not the living room's; aimed with both, both.
        /// </summary>
        private IEnumerator Rs2aFeedDrawsItsOwnRoomsDressing(LiveFeed feed, HouseRoomScope scope)
        {
            int bedroom = RoomLayers.IndexOf("Bedroom"), living = RoomLayers.IndexOf("Living");
            var box = scope.Box(bedroom);
            var aim = new Vector3(box.center.x, 0f, box.yMin + 1.5f);
            var frame = new Texture2D(LiveFeed.Width, LiveFeed.Height, TextureFormat.RGBA32, false);
            var made = new List<Object> { frame };
            try
            {
                // Where the feed's eye stands for that aim, from a render of it.
                yield return Rs2aRenderFeed(feed, aim, RoomSet.Of(bedroom), frame);
                var eye = feed.Camera.transform;
                var own = ScopeProbeCube(made, "Room scope RS2a feed's own", eye.position + eye.forward * 3f, .3f, Color.magenta);
                var next = ScopeProbeCube(made, "Room scope RS2a feed's neighbour", eye.position + eye.forward * 1.2f + eye.right * .6f, .2f, Color.cyan);
                Assert.That(scope.RoomUnder(own.transform.position), Is.EqualTo(bedroom), "The first marker stands in the bedroom.");
                Assert.That(scope.RoomUnder(next.transform.position), Is.EqualTo(living), "The second stands in the living room, the eye's side of the wall.");
                HouseRoomScope.PlaceDressing(own.transform);
                HouseRoomScope.PlaceDressing(next.transform);
                Assert.That(own.layer, Is.EqualTo(RoomLayers.Bedroom), "Placed as dressing: the bedroom's layer.");
                Assert.That(next.layer, Is.EqualTo(RoomLayers.Living), "and the living room's.");

                yield return Rs2aRenderFeed(feed, aim, RoomSet.Of(bedroom), frame);
                var ownAt = Rs2aFeedPixel(feed, frame, own.transform.position, "the bedroom's marker");
                var nextAt = Rs2aFeedPixel(feed, frame, next.transform.position, "the living room's marker");
                Assert.That(ScopeProbeDifference(ownAt, Color.magenta), Is.LessThan(.35f), "The feed draws its own room's dressing: " + ownAt + ".");
                Assert.That(ScopeProbeDifference(nextAt, Color.cyan), Is.GreaterThan(.5f), "and not the room next door's: " + nextAt + ".");

                yield return Rs2aRenderFeed(feed, aim, RoomSet.Of(bedroom).With(RoomSet.Of(living)), frame);
                var bothAt = Rs2aFeedPixel(feed, frame, next.transform.position, "the living room's marker, both rooms named");
                Assert.That(ScopeProbeDifference(bothAt, Color.cyan), Is.LessThan(.35f), "Aimed with both rooms, it draws the living room's marker too: " + bothAt + ".");
            }
            finally
            {
                foreach (var item in made) if (item != null) Object.Destroy(item);
            }
        }

        /// <summary>
        /// Aims the feed, with its rooms, on a frame it renders - after the director's own aim in Update,
        /// before the feed's LateUpdate - and reads the picture it rendered for that aim into <paramref name="into"/>.
        /// </summary>
        private static IEnumerator Rs2aRenderFeed(LiveFeed feed, Vector3 aim, RoomSet rooms, Texture2D into)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (true)
            {
                yield return null;
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "The feed renders the aim it is given.");
                if (feed.Paused || Time.frameCount % LiveFeed.EveryNthFrame != 0) continue;
                int revision = feed.CaptureRevision;
                feed.Aim(aim, 0f, "", rooms);
                yield return null;
                if (feed.CaptureRevision == revision || feed.CapturedSubject != aim) continue;
                var active = RenderTexture.active;
                try
                {
                    RenderTexture.active = (RenderTexture)feed.Texture;
                    into.ReadPixels(new Rect(0, 0, LiveFeed.Width, LiveFeed.Height), 0, 0, false);
                    into.Apply(false);
                }
                finally { RenderTexture.active = active; }
                yield break;
            }
        }

        /// <summary>The feed's pixel where a point stands in its picture, the point asserted inside the frame.</summary>
        private static Color Rs2aFeedPixel(LiveFeed feed, Texture2D frame, Vector3 at, string what)
        {
            var seen = feed.Camera.WorldToViewportPoint(at);
            Assert.That(seen.z > 0f && seen.x > .05f && seen.x < .95f && seen.y > .05f && seen.y < .95f, Is.True, what + " is in the feed's frame: " + seen + ".");
            return frame.GetPixel(Mathf.RoundToInt(seen.x * (LiveFeed.Width - 1)), Mathf.RoundToInt(seen.y * (LiveFeed.Height - 1)));
        }

        /// <summary>Waits, on the real clock, for every active body to finish assembling: a body being built draws nothing to test.</summary>
        private IEnumerator Rs4BodiesBuilt()
        {
            yield return WaitFor(() => SceneComponents<CharacterPresentation>()
                    .Where(body => body.gameObject.activeInHierarchy).All(body => !body.IsBodyAssembling && !body.IsChangingOutfit),
                90f, "Every body in the house finished assembling.");
        }

        /// <summary>A room's view at D4's farthest zoom, naming the room.</summary>
        private static HouseCameraRig.Shot Rs4Shot(HouseRoomScope scope, string room) => new HouseCameraRig.Shot
        {
            Focus = ScopeCentre(scope, room), Distance = 11.25f, Pitch = 45f, Yaw = 0f, Rooms = RoomSet.Of(room),
        };

        /// <summary>The renderers a body has drawing, and the layers of those the main camera's mask leaves out.</summary>
        private void Rs4Read(Transform body, out int drawing, out int outOfMask, out string layers)
        {
            int mask = cameraRig.ViewCamera.cullingMask;
            drawing = 0; outOfMask = 0;
            var seen = new SortedSet<int>();
            foreach (var renderer in body.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled) continue;
                drawing++;
                seen.Add(renderer.gameObject.layer);
                if ((mask & (1 << renderer.gameObject.layer)) == 0) outOfMask++;
            }
            layers = drawing + " renderers on layers " + string.Join("/", seen) + ", " + outOfMask + " left out by the mask";
        }

        /// <summary>Whether the main camera draws all of a body: every renderer it has drawing is on a layer the mask holds.</summary>
        private bool Rs4DrawnNow(Transform body, out string why)
        {
            Rs4Read(body, out int drawing, out int outOfMask, out why);
            return drawing > 0 && outOfMask == 0;
        }

        /// <summary>Whether the main camera draws none of a body: every renderer it has drawing is on a layer the mask leaves out.</summary>
        private bool Rs4HiddenNow(Transform body, out string why)
        {
            Rs4Read(body, out int drawing, out int outOfMask, out why);
            return drawing > 0 && outOfMask == drawing;
        }

        /// <summary>The distinct layers of a body's renderer objects, its root's own aside.</summary>
        private static int[] Rs4PiecesOn(Transform body) => body.GetComponentsInChildren<Renderer>(true)
            .Where(r => r.gameObject != body.gameObject).Select(r => r.gameObject.layer).Distinct().OrderBy(layer => layer).ToArray();

        /// <summary>No collider on any body stands on a room layer, and no root moved off the layer it had.</summary>
        private static void Rs4NoColliderOnARoomLayer(HouseNpc[] bodies, Dictionary<HouseNpc, int> roots, string when)
        {
            foreach (var npc in bodies)
            {
                Assert.That(npc.gameObject.layer, Is.EqualTo(roots[npc]), npc.Id + "'s root keeps its layer, " + when + ".");
                Assert.That(npc.GetComponentsInChildren<Collider>(true).Where(c => RoomLayers.IsRoomLayer(c.gameObject.layer)).Select(c => c.name).ToArray(),
                    Is.Empty, npc.Id + ": no collider on a room layer, " + when + ".");
            }
        }

        /// <summary>Carries a body through a doorway at a speed on the real clock, and checks every frame that the main camera draws it.</summary>
        private IEnumerator Rs4Carry(Transform body, ScopeCrossing crossing, float speed, string who, List<string> failures, Action frame)
        {
            float length = Vector3.Distance(crossing.Start, crossing.End), done = 0f;
            var before = director.ViewedRooms;
            bool changed = false;
            while (done < length)
            {
                done = Mathf.Min(length, done + speed * Mathf.Clamp(Time.unscaledDeltaTime, .0005f, .05f));
                ScopePlace(body, Vector3.Lerp(crossing.Start, crossing.End, done / length), .3f);
                yield return null;
                frame();
                if (director.ViewedRooms != before) changed = true;
                if (!Rs4DrawnNow(body, out string why) || !director.IsBodyDrawn(body))
                {
                    failures.Add(who + ", " + done.ToString("0.00") + " m along: not drawn (" + why + ")");
                    yield break;
                }
            }
            if (!changed) failures.Add(who + ": the view never left the " + before);
            if (director.ViewedRooms != RoomSet.Of(crossing.To)) failures.Add(who + ": the view ended on " + director.ViewedRooms);
        }

        /// <summary>The room whose floor is farthest from every point given: well away from all of them.</summary>
        private static int Rs4FarRoom(HouseRoomScope scope, params Vector3[] from)
        {
            int best = -1;
            float bestDistance = -1f;
            for (int room = 0; room < RoomLayers.Count; room++)
            {
                if (!scope.Has(room)) continue;
                float nearest = from.Min(point => HouseWhoIsWhere.OutsideBy(scope.Box(room), point));
                if (nearest <= bestDistance) continue;
                bestDistance = nearest;
                best = room;
            }
            Assert.That(bestDistance, Is.GreaterThan(1f), "A room well away from " + string.Join(", ", from.Select(p => p.ToString("F1"))) + ".");
            return best;
        }

        /// <summary>A walkable point in a room more than four metres from a point: out of Talk's reach.</summary>
        private Vector3 Rs4FarFloor(HouseRoomScope scope, Vector3 from, int room)
        {
            var box = scope.Box(room);
            foreach (var corner in new[] { new Vector2(box.xMin + 1.5f, box.yMin + 1.5f), new Vector2(box.xMax - 1.5f, box.yMax - 1.5f),
                         new Vector2(box.xMin + 1.5f, box.yMax - 1.5f), new Vector2(box.xMax - 1.5f, box.yMin + 1.5f) })
            {
                var at = new Vector3(corner.x, 0f, corner.y);
                if (Vector3.Distance(at, from) > 4f && UnityEngine.AI.NavMesh.SamplePosition(at, out var hit, 1.5f, player.Agent.areaMask)
                    && Vector3.Distance(hit.position, from) > 4f) return hit.position;
            }
            Assert.Fail("No floor in the " + RoomLayers.RoomIds[room] + " four metres from " + from.ToString("F1") + ".");
            return from;
        }

        /// <summary>Stands a houseguest beside the player where Talk opens: one floor, in reach, in sight.</summary>
        private void Rs4PlaceToTalk(HouseNpc npc)
        {
            for (int direction = 0; direction < 8; direction++)
            {
                float angle = direction * Mathf.PI / 4f;
                var candidate = player.transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.5f;
                if (!UnityEngine.AI.NavMesh.SamplePosition(candidate, out var hit, .4f, player.Agent.areaMask)) continue;
                ScopePlace(npc.transform, hit.position, .4f);
                if ((bool)NpcInvoke("CanTalk", npc)) return;
            }
            Assert.Fail("Nowhere beside the player where Talk opens with " + npc.DisplayName + ".");
        }

        /// <summary>
        /// A click's ray that meets a standing body first and the bare floor next: from a step away
        /// and over the head, down through the chest, so a click that passes them walks rather than
        /// opening a piece of furniture.
        /// </summary>
        private static Ray Rs4RayAt(HouseNpc npc)
        {
            var target = npc.transform.position + Vector3.up;
            for (int direction = 0; direction < 8; direction++)
            {
                float angle = direction * Mathf.PI / 4f;
                var origin = npc.transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.6f + Vector3.up * 1.7f;
                var ray = new Ray(origin, (target - origin).normalized);
                var hits = Physics.RaycastAll(ray, 10f, HouseLayers.Pick, QueryTriggerInteraction.Ignore).OrderBy(hit => hit.distance).ToArray();
                if (hits.Length < 2 || !hits[0].collider.transform.IsChildOf(npc.transform)) continue;
                var behind = hits.FirstOrDefault(hit => !hit.collider.transform.IsChildOf(npc.transform));
                if (behind.collider != null && behind.collider.GetComponentInParent<HouseWalkable>() != null) return ray;
            }
            Assert.Fail("No ray from beside " + npc.DisplayName + " meets their body first and the floor behind them.");
            return default;
        }

        /// <summary>A seat in the house: an authored seated anchor in a room, and that room.</summary>
        private HouseInteractionAnchor Rs4SeatAnchor(HouseRoomScope scope, out int room)
        {
            var seat = HouseInteractionAnchors.InScene(director.gameObject.scene)
                .Where(anchor => anchor != null && anchor.isActiveAndEnabled && anchor.Seated && RoomLayers.IndexOf(anchor.RoomId) >= 0)
                .OrderBy(anchor => anchor.RoomId == "Living" ? 0 : 1).FirstOrDefault();
            Assert.That(seat, Is.Not.Null, "The house has a seat.");
            room = RoomLayers.IndexOf(seat.RoomId);
            return seat;
        }

        /// <summary>Sits a houseguest on a seat from its approach, and waits, on the real clock, for them to settle.</summary>
        private IEnumerator Rs4Seat(HouseNpc npc, HouseInteractionAnchor seat)
        {
            ScopePlace(npc.transform, seat.Approach, 1f);
            var sitting = npc.GetComponent<HouseSeatPresentation>();
            if (sitting == null) sitting = npc.gameObject.AddComponent<HouseSeatPresentation>();
            sitting.Begin(seat, () => true);
            Assert.That(sitting.Active, Is.True, npc.DisplayName + " takes the seat.");
            yield return WaitFor(() => sitting.Settled, 4f, npc.DisplayName + " settles on the seat.");
        }
    }
}
