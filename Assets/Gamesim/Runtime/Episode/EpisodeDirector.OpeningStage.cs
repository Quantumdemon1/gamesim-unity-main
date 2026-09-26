using System.Collections.Generic;
using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;
using UnityEngine.AI;

namespace Gamesim.Episode
{
    /// <summary>
    /// The opening's front door, staged in the west of the yard: a facade the house queues behind,
    /// a door each houseguest walks through, a mark in front of the lens, and a gathering place out
    /// of shot behind the camera.
    ///
    /// <para>The yard's roped arch looks like an entrance and cannot be one: the competition gate
    /// stands inside it, its backdrop is eight tenths of a metre behind, and a podium sits on its
    /// centre line. The west yard is open lawn against the house's outer wall, and a runtime facade
    /// there has room to hide a queue of sixteen.</para>
    ///
    /// <para><b>Nobody is hidden and nobody is teleported mid-shot.</b> Switching a body off unbinds
    /// its navigation, and the house moves people only by walking them; the one placement the house
    /// allows is the one a load makes, before anyone is bound. So the stage does exactly that, once,
    /// under the title card: the load's own sequence, with the spots behind the facade in place of the
    /// season's anchors. Everyone then walks - to the door, through it, off, and home at the walk-in -
    /// and a skip puts them back with the same sequence under black.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        private OpeningStage openingStage;

        private OpeningStage CreateOpeningStage(bool verification) => OpeningStage.TryCreate(this, verification);

        private sealed class OpeningStage : OpeningSequence.IStage
        {
            // The marks, in world metres: on deck behind the light, just behind the door, the mark in
            // front of the lens, and a waypoint out of shot to the left that every exit takes.
            private static readonly Vector3 DeckMark = new Vector3(-5.3f, 0f, 13.8f);
            private static readonly Vector3 DoorMark = new Vector3(-3.85f, 0f, 13.8f);
            private static readonly Vector3 RevealMark = new Vector3(-1.6f, 0f, 14.1f);
            private static readonly Vector3 ExitMark = new Vector3(0.8f, 0f, 11.0f);
            private static readonly float[] QueueX = { -6.3f, -7.5f, -8.7f, -9.9f, -11.1f, -12.3f };
            private static readonly float[] QueueZ = { 11.2f, 12.4f, 13.6f, 14.8f };
            private static readonly float[] GatherX = { 3.6f, 4.8f, 6.0f, 7.2f, 8.4f };
            private static readonly float[] GatherZ = { 11.0f, 12.2f, 13.4f, 14.6f };
            // The competition course's stacks and crates carry no colliders, so floor checks cannot
            // see them; spots keep a metre from each.
            private static readonly Vector3[] CourseProps =
            {
                new Vector3(-6f, 0f, 15.5f), new Vector3(0f, 0f, 15.5f), new Vector3(6f, 0f, 15.5f),
                new Vector3(-4.3f, 0f, 15.9f), new Vector3(1.7f, 0f, 15.9f), new Vector3(7.7f, 0f, 15.9f),
            };
            private const string YardRoom = "Yard";
            private const float HomeTolerance = 0.6f;

            private readonly EpisodeDirector director;
            private readonly HouseRoomQuery rooms;
            private readonly NavMeshQueryFilter filter;
            private readonly float radius, height;
            private readonly List<Vector3> queue = new List<Vector3>(), gather = new List<Vector3>();
            // Why spots were turned down: logged only when the stage is refused for want of them.
            private readonly List<string> refusals = new List<string>();
            private readonly Dictionary<string, Vector3> gatherFor = new Dictionary<string, Vector3>();
            private readonly Dictionary<string, Queue<Vector3>> legs = new Dictionary<string, Queue<Vector3>>();
            private readonly Dictionary<string, Vector3> heading = new Dictionary<string, Vector3>();
            // Walks refused for the moment - a mark still occupied by whoever walked it last - and
            // when to stop trying them again.
            private readonly Dictionary<string, float> retrying = new Dictionary<string, float>();
            private const float RetrySeconds = 6f;
            private readonly object playerOwner = new object();
            private Vector3 deck, door, mark, exit;
            private OpeningDoorSet set;
            private bool begun, ended;
            // Who is waiting for the leaves to clear before walking through, and how open that is.
            private string throughWhenClear;
            private const float DoorClearOpenness = 0.35f;

            public bool Placed { get; private set; }

            /// <summary>Whether the stage has the house: placed and not yet ended.</summary>
            public bool Active => Placed && !ended;

            private OpeningStage(EpisodeDirector owner, HouseRoomQuery query)
            {
                director = owner;
                rooms = query;
                filter = new NavMeshQueryFilter { agentTypeID = owner.player.Agent.agentTypeID, areaMask = owner.player.Agent.areaMask };
                radius = 0.35f; height = 1.9f;
                foreach (var npc in owner.housemates)
                {
                    var capsule = npc != null ? npc.GetComponent<CapsuleCollider>() : null;
                    if (capsule == null) continue;
                    radius = Mathf.Max(radius, capsule.radius);
                    height = Mathf.Max(height, capsule.height);
                }
            }

            /// <summary>
            /// The stage, or null when this house cannot stage it: reduced motion, a headless run that
            /// did not ask, an intro already seen, a conversation the save is holding, a house whose
            /// movement has stopped, or a yard without room for everybody. The intro plays on cards
            /// then, as the reference build's does for a houseguest with no body.
            /// </summary>
            public static OpeningStage TryCreate(EpisodeDirector owner, bool verification)
            {
                var state = owner.projected;
                if (owner.reducedMotion || (Application.isBatchMode && !verification) || state == null) return null;
                if (state.openingBeatsSeen.Contains(OpeningBeat.Intro) || state.npcSocial.pending.Count > 0 || owner.npcWorldFailed) return null;
                // A house whose people cannot move at all - a season imported mid-week, a world
                // switched off for diagnostics - is not reset for a stage nobody can walk on.
                if (!NpcSocialState.IsEligible(state) || owner.npcDiagnosticsSuspended) return null;
                if (owner.player == null || owner.player.Agent == null || !owner.player.Agent.isOnNavMesh) return null;
                if (!HouseRoomQuery.TryCreate(owner.gameObject.scene, out var rooms, out var reason))
                { Debug.Log("Opening stage: " + reason); return null; }
                var stage = new OpeningStage(owner, rooms);
                return stage.Survey(owner.Housemates().Count) ? stage : null;
            }

            /// <summary>Every mark and spot checked the way binding and routing will check them; failures are skipped.</summary>
            private bool Survey(int houseguests)
            {
                var accepted = new List<Vector3>();
                if (!Accept(DeckMark, accepted, out deck, "the on-deck mark") || !Accept(DoorMark, accepted, out door, "the door mark")
                    || !Accept(RevealMark, accepted, out mark, "the reveal mark") || !Accept(ExitMark, accepted, out exit, "the exit mark"))
                { Debug.Log("Opening stage: " + refusals.Last() + "."); return false; }
                if (!Route(deck, door) || !Route(door, mark) || !Route(mark, exit))
                { Debug.Log("Opening stage: there is no walk through the front door."); return false; }

                foreach (var spot in Grid(QueueX, QueueZ).OrderBy(spot => (spot - DeckMark).sqrMagnitude))
                    if ((spot - DeckMark).magnitude >= 1.0f && Accept(spot, accepted, out var sampled, "a place to wait") && Route(sampled, deck)) queue.Add(sampled);
                foreach (var spot in Grid(GatherX, GatherZ).OrderBy(spot => (spot - ExitMark).sqrMagnitude))
                    if (Accept(spot, accepted, out var sampled, "a place to gather") && Route(exit, sampled)) gather.Add(sampled);
                if (queue.Count < houseguests || gather.Count < houseguests + 1)
                {
                    Debug.Log("Opening stage: the west yard has room for " + queue.Count + " to wait and " + gather.Count + " to gather, not "
                        + houseguests + ". " + string.Join("; ", refusals) + ".");
                    return false;
                }
                return true;
            }

            private static IEnumerable<Vector3> Grid(float[] xs, float[] zs)
            {
                foreach (float z in zs) foreach (float x in xs) yield return new Vector3(x, 0f, z);
            }

            /// <summary>
            /// Whether someone can stand here: in the yard's safe interior, on the floor routing uses,
            /// clear of every body and obstacle, and apart from the course's props and every spot
            /// already taken. Each refusal is logged with its reason; the spot is skipped.
            /// </summary>
            private bool Accept(Vector3 spot, List<Vector3> accepted, out Vector3 sampled, string what)
            {
                sampled = spot;
                string refusal = null;
                if (!rooms.TryLocate(spot, radius, out var room) || room != YardRoom)
                    refusal = "not in the yard (" + (room ?? rooms.LastFailure) + ")";
                else if (!rooms.TrySampleFloor(spot, radius, filter, .25f, out sampled, out var sampledRoom) || sampledRoom != YardRoom)
                    refusal = "off the walkable floor (" + (sampledRoom ?? rooms.LastFailure) + ")";
                // The player stands in for the body: the clearance query needs a house actor to leave out.
                else if (!rooms.HasCapsuleClearance(sampled, radius, height, director.player.transform))
                    refusal = rooms.LastFailure;
                else
                {
                    var flat = new Vector3(sampled.x, 0f, sampled.z);
                    if (CourseProps.Any(prop => (prop - flat).magnitude < 1.0f)) refusal = "beside the competition course's props";
                    // A metre apart: the grid is laid 1.2 m apart and a spot moves a little when it
                    // is sampled onto the floor, so asking for the grid's own spacing refused half of it.
                    else if (accepted.Any(other => (new Vector3(other.x, 0f, other.z) - flat).magnitude < 1.0f)) refusal = "too close to another place";
                }
                if (refusal != null) { refusals.Add(what + " at " + spot + " is " + refusal); return false; }
                accepted.Add(sampled);
                return true;
            }

            private bool Route(Vector3 from, Vector3 to)
            {
                var path = new NavMeshPath();
                return NavMesh.CalculatePath(from, to, filter, path) && path.status == NavMeshPathStatus.PathComplete;
            }

            // ------------------------------------------------------------ the stage's view

            public HouseCameraRig.Shot DoorShot => new HouseCameraRig.Shot
            {
                Focus = new Vector3(-2.0f, 1.85f, 13.8f), Distance = 4.1f, Pitch = 0f, Yaw = 270f,
                FieldOfView = 40f, Seconds = 0.6f, DepthOfFieldWeight = 0.3f,
            };

            /// <summary>The reference build's push-in as the door opens: a metre closer in half a second.</summary>
            public HouseCameraRig.Shot PushInShot => new HouseCameraRig.Shot
            {
                Focus = new Vector3(-2.0f, 1.85f, 13.8f), Distance = 3.1f, Pitch = 0f, Yaw = 270f,
                FieldOfView = 40f, Seconds = 0.5f, DepthOfFieldWeight = 0.3f,
            };

            public float BodyReadiness
            {
                get
                {
                    var bodies = director.Housemates().Select(npc => npc.GetComponent<CharacterPresentation>())
                        .Append(director.player != null ? director.player.GetComponentInChildren<CharacterPresentation>() : null)
                        .ToList();
                    int total = bodies.Count, ready = bodies.Count(body => body == null || !body.IsBodyAssembling);
                    return total == 0 ? 1f : ready / (float)total;
                }
            }

            public bool Ready
            {
                get
                {
                    if (!Placed || ended) return false;
                    if (begun) return true;
                    var meetings = director.npcMeetings;
                    if (meetings == null || !meetings.IsReady) return false;
                    var ids = director.Housemates().Select(npc => npc.Id).ToList();
                    if (!meetings.BeginOpeningStage(ids, out var reason)) { Debug.Log("Opening stage: " + reason); return false; }
                    begun = true;
                    return true;
                }
            }

            public bool Failed => director.npcWorldFailed;

            // ------------------------------------------------------------ placing and putting back

            /// <summary>
            /// Everybody behind the facade, facing the door, with the player on deck - by the load's
            /// own placement sequence, which is the only placement the house allows.
            /// </summary>
            public bool TryPlace()
            {
                if (Placed || ended) return Placed;
                var cast = director.Housemates();
                if (queue.Count < cast.Count) return false;
                director.ResetNpcSocialForLoad();
                for (int i = 0; i < cast.Count; i++)
                    cast[i].transform.SetPositionAndRotation(queue[i], Quaternion.Euler(0f, 90f, 0f));
                director.player.Agent.Warp(deck);
                director.player.Agent.ResetPath();
                Physics.SyncTransforms();
                director.Project();
                set = OpeningDoorSet.Build(director.gameObject.scene);
                Placed = true;
                return true;
            }

            /// <summary>Everybody back where the season started them, by the same sequence. Only ever under an opaque frame.</summary>
            public void RestoreHome()
            {
                ReleaseEveryone();
                director.ResetNpcSocialForLoad();
                if (director.initialNpcPositions != null)
                    for (int i = 0; i < director.housemates.Length && i < director.initialNpcPositions.Length; i++)
                        if (director.housemates[i] != null)
                            director.housemates[i].transform.SetPositionAndRotation(director.initialNpcPositions[i], director.initialNpcRotations[i]);
                if (director.player.Agent.isOnNavMesh) { director.player.Agent.Warp(director.initialPlayerPosition); director.player.Agent.ResetPath(); }
                Physics.SyncTransforms();
                director.Project();
            }

            public void StrikeSet()
            {
                if (set != null) Object.Destroy(set.gameObject);
                set = null;
            }

            /// <summary>Lets the house have its people back, putting them home first when asked and anybody is not.</summary>
            public void End(bool restoreHome)
            {
                if (ended) return;
                if (restoreHome && Placed && !AllHome) RestoreHome();
                else ReleaseEveryone();
                StrikeSet();
                ended = true;
            }

            private void ReleaseEveryone()
            {
                if (director.npcMeetings != null) director.npcMeetings.EndOpeningStage();
                if (director.player != null) director.player.ReleaseActivityMove(playerOwner);
                legs.Clear();
                throughWhenClear = null;
                heading.Clear();
                retrying.Clear();
                begun = false;
                foreach (var npc in director.Housemates())
                {
                    var visual = npc.GetComponent<CharacterPresentation>();
                    if (visual != null) { visual.SetFacing(float.NaN); visual.LookAt(null, 0f); }
                    StopDancing(visual);
                }
                // The player too: a skip during their own moment on the mark left them facing the lens.
                var own = Visual(director.projected.playerId);
                if (own != null) { own.SetFacing(float.NaN); own.LookAt(null, 0f); }
                StopDancing(own);
            }

            // ------------------------------------------------------------ walking

            public bool OnDeck(string id) => Send(id, deck);
            public bool ToDoor(string id) => Send(id, door);
            public bool AtDoor(string id) => Arrived(id, door);
            /// <summary>
            /// Walks them through once the leaves are out of their way. The leaves shut flush and rattle
            /// shut for 0.3 s before they swing, and a walk begun with the door was through a closed
            /// leaf before it moved - the reference's leaves stand ajar with a slot down the middle, so
            /// its walker could go at once. Held here, the walk starts a third of the way into the
            /// swing, and the leaves are three quarters open by the time anybody reaches them.
            /// </summary>
            public bool ThroughDoor(string id)
            {
                if (!Placed || ended) return false;
                if (set != null && set.Openness < DoorClearOpenness) { throughWhenClear = id; return true; }
                throughWhenClear = null;
                return Send(id, mark);
            }
            public bool OnMark(string id) => Arrived(id, mark);

            public void OpenDoor() { if (set != null) set.Open(director.reducedMotion); }
            public void CloseDoor() { if (set != null) set.Close(director.reducedMotion); }

            /// <summary>Turned to the lens, greeting it: the reference build only ever walked its people, which a real body can do better than.</summary>
            public void Present(string id)
            {
                var visual = Visual(id);
                if (visual == null) return;
                visual.SetFacing(90f);
                if (director.cameraRig != null && director.cameraRig.ViewCamera != null) visual.LookAt(director.cameraRig.ViewCamera.transform, 2f);
                // A little dance on the mark while the name comes up; a body with no dance cheers.
                if (visual.CanAct(CharacterPresentation.BodyActivity.Dancing)) visual.SetActivity(CharacterPresentation.BodyActivity.Dancing);
                else if (id != director.projected.playerId) director.React(id, CharacterPresentation.Reaction.Cheered);
                else visual.React(CharacterPresentation.Reaction.Cheered);
            }

            /// <summary>Stops a dance on the mark, and only a dance: whatever else the body is doing is not the stage's.</summary>
            private static void StopDancing(CharacterPresentation visual)
            {
                if (visual != null && visual.Activity == CharacterPresentation.BodyActivity.Dancing)
                    visual.SetActivity(CharacterPresentation.BodyActivity.None);
            }

            /// <summary>Off to the left, out of shot, and on to a place behind the camera where the house gathers.</summary>
            public void SendOff(string id)
            {
                var visual = Visual(id);
                if (visual != null) visual.SetFacing(float.NaN);
                StopDancing(visual);
                if (throughWhenClear == id) throughWhenClear = null;
                if (!gatherFor.TryGetValue(id, out var place))
                {
                    place = gather[Mathf.Min(gatherFor.Count, gather.Count - 1)];
                    gatherFor[id] = place;
                }
                Send(id, exit, place);
            }

            public void SendHome(string id)
            {
                var visual = Visual(id);
                if (visual != null) visual.SetFacing(float.NaN);
                Send(id, Home(id));
            }

            public bool AllHome => director.Housemates().All(npc => Near(npc.transform.position, Home(npc.Id)))
                                   && Near(director.player.transform.position, director.initialPlayerPosition);

            private Vector3 Home(string id)
            {
                if (id == director.projected.playerId) return director.initialPlayerPosition;
                for (int i = 0; i < director.housemates.Length; i++)
                    if (director.housemates[i] != null && director.housemates[i].Id == id && director.initialNpcPositions != null && i < director.initialNpcPositions.Length)
                        return director.initialNpcPositions[i];
                return director.initialPlayerPosition;
            }

            private static bool Near(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude <= HomeTolerance;

            /// <summary>Starts someone on a walk through these places in turn, replacing whatever they were doing.</summary>
            private bool Send(string id, params Vector3[] places)
            {
                if (!Placed || ended || places.Length == 0) return false;
                legs[id] = new Queue<Vector3>(places.Skip(1));
                return Walk(id, places[0]);
            }

            private bool Walk(string id, Vector3 place)
            {
                heading[id] = place;
                bool going;
                if (id == director.projected.playerId)
                {
                    var player = director.player;
                    player.ReleaseActivityMove(playerOwner);
                    going = player.TryBeginActivityMove(playerOwner, place, out _);
                }
                else going = begun && director.npcMeetings != null && director.npcMeetings.RouteOpeningActor(id, place, out _);
                // A walk to a place somebody is still standing on is refused by its clearance check;
                // it is tried again each tick for a while rather than leaving them parked in shot.
                if (going) retrying.Remove(id);
                else if (!retrying.ContainsKey(id)) retrying[id] = Time.unscaledTime + RetrySeconds;
                return going;
            }

            private bool Arrived(string id, Vector3 place)
            {
                if (!heading.TryGetValue(id, out var going) || (going - place).sqrMagnitude > .0001f) return false;
                if (legs.TryGetValue(id, out var next) && next.Count > 0) return false;
                return id == director.projected.playerId
                    ? director.player.ActivityHasArrived(playerOwner)
                    : director.npcMeetings != null && director.npcMeetings.OpeningActorArrived(id);
            }

            /// <summary>Each frame the stage has the house: its people kept walking, and on to their next place as they arrive.</summary>
            public void Tick()
            {
                if (!Active) return;
                if (begun && director.npcMeetings != null) director.npcMeetings.ResumeOpeningActors();
                if (throughWhenClear != null && (set == null || set.Openness >= DoorClearOpenness))
                {
                    var id = throughWhenClear;
                    throughWhenClear = null;
                    Send(id, mark);
                }
                foreach (var id in retrying.Keys.ToList())
                {
                    if (Time.unscaledTime > retrying[id]) { retrying.Remove(id); continue; }
                    if (heading.TryGetValue(id, out var place)) Walk(id, place);
                }
                foreach (var id in legs.Keys.ToList())
                {
                    var next = legs[id];
                    if (next.Count == 0) continue;
                    bool there = id == director.projected.playerId
                        ? director.player.ActivityHasArrived(playerOwner)
                        : director.npcMeetings != null && director.npcMeetings.OpeningActorArrived(id);
                    if (there) Walk(id, next.Dequeue());
                }
            }

            private CharacterPresentation Visual(string id)
            {
                if (id == director.projected.playerId) return director.player != null ? director.player.GetComponentInChildren<CharacterPresentation>() : null;
                var npc = director.Housemates().FirstOrDefault(each => each.Id == id);
                return npc != null ? npc.GetComponent<CharacterPresentation>() : null;
            }
        }

        /// <summary>The houseguests with a body in the house now, in cast order.</summary>
        private List<HouseNpc> Housemates() =>
            housemates == null ? new List<HouseNpc>()
                : housemates.Where(npc => npc != null && npc.gameObject.activeInHierarchy && projected != null
                    && projected.Find(npc.Id)?.status == ContestantStatus.Active).ToList();
    }
}
