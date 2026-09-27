using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.House;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Gamesim.Episode
{
    /// <summary>The follow camera: who the rig rides and how the cast rail, the keys and the pad choose them.</summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>The id of the houseguest the camera is following, or null.</summary>
        public string FollowedId
        {
            get
            {
                var subject = cameraRig != null ? cameraRig.FocusedSubject : null;
                if (subject == null || projected == null) return null;
                var npc = subject.GetComponentInParent<HouseNpc>();
                if (npc != null) return npc.Id;
                return subject.GetComponentInParent<HousePlayerController>() != null ? projected.playerId : null;
            }
        }

        /// <summary>The name of the houseguest the camera is following, or null.</summary>
        public string FollowedName
        {
            get
            {
                var subject = cameraRig != null ? cameraRig.FocusedSubject : null;
                if (subject == null || projected == null) return null;
                var npc = subject.GetComponentInParent<HouseNpc>();
                if (npc != null) return projected.Find(npc.Id)?.name;
                return subject.GetComponentInParent<HousePlayerController>() != null ? projected.Find(projected.playerId)?.name : null;
            }
        }

        /// <summary>
        /// Follows a houseguest by id, from the cast rail: the camera rides them until something
        /// else is asked of it. The same id again lets go. Honoured under reduced motion for the
        /// reason click-to-follow is: the viewer asked for this shot.
        /// </summary>
        public void FollowHouseguest(string id)
        {
            if (cameraRig == null || string.IsNullOrEmpty(id)) return;
            var body = BodyFor(id);
            if (body == null) return;
            if (cameraRig.FocusedSubject == body) cameraRig.ClearSubject();
            else cameraRig.FocusSubject(body);
            hud?.ShowFollowing(FollowedName);
            lastFollowed = FollowedName;
        }

        /// <summary>Tab follows the next active houseguest in cast order, Shift+Tab the previous; the player is in the ring.</summary>
        public void FollowNext(bool backwards)
        {
            if (cameraRig == null || projected == null) return;
            var ring = new List<Transform>();
            foreach (var actor in projected.contestants)
            {
                if (actor.status != ContestantStatus.Active) continue;
                var body = BodyFor(actor.id);
                if (body != null) ring.Add(body);
            }
            if (ring.Count == 0) return;
            int at = ring.IndexOf(cameraRig.FocusedSubject);
            int next = at < 0 ? (backwards ? ring.Count - 1 : 0) : (at + (backwards ? ring.Count - 1 : 1)) % ring.Count;
            cameraRig.FocusSubject(ring[next]);
            hud?.ShowFollowing(FollowedName);
            lastFollowed = FollowedName;
        }

        /// <summary>The house event the camera is on, so a re-render does not take the shot again.</summary>
        private string framedEventId;
        public const float EventShotDistance = 6.5f;
        public const float EventShotPitch = 28f;
        public const float EventShotSeconds = 0.8f;
        /// <summary>How far apart the people in an event can stand and still be one scene, in metres.</summary>
        public const float EventShotReach = 6f;

        /// <summary>
        /// Mockup-04: the event's card is low in the frame and the people it is about are above it,
        /// in the room. Taken when the phase panel shows an event whose people stand together, and
        /// let go with the panel, as every panel's shot is. People in different rooms are not a
        /// scene, and then the camera stays where it is.
        /// </summary>
        private void FrameHouseEvent(HouseEventState item)
        {
            // The episode screen's card, or a story's card opened out in the house (plan §5.1).
            if (cameraRig == null || item == null || !(phaseOpen || sceneCardOpen)) return;
            if (framedEventId == item.id && cameraRig.HasShot) return;
            var bodies = item.involvedIds.Select(BodyFor).Where(body => body != null).ToArray();
            if (bodies.Length == 0) return;
            var centre = bodies.Aggregate(Vector3.zero, (sum, body) => sum + body.position) / bodies.Length;
            if (bodies.Any(body => Vector3.Distance(body.position, centre) > EventShotReach * .5f)) return;
            framedEventId = item.id;
            // The pivot under the floor puts the faces in the upper third, over the card, and the
            // card over their legs - mockup-04's frame.
            cameraRig.MoveTo(new HouseCameraRig.Shot
            {
                Focus = centre - Vector3.up * .2f, Distance = EventShotDistance, Pitch = EventShotPitch, KeepYaw = true,
                FieldOfView = HouseCameraRig.TwoShotFieldOfView, Seconds = EventShotSeconds, DepthOfFieldWeight = 1f,
            });
        }

        /// <summary>Whether the camera is on a house event's people, for a test.</summary>
        public bool IsFramingHouseEvent => framedEventId != null && cameraRig != null && cameraRig.HasShot && (phaseOpen || sceneCardOpen);

        private Transform BodyFor(string id)
        {
            if (housemates != null)
                foreach (var npc in housemates)
                    if (npc != null && npc.Id == id && npc.gameObject.activeInHierarchy) return npc.transform;
            return player != null && projected != null && id == projected.playerId ? player.transform : null;
        }
    }
}
