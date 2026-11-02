using System.Collections;
using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>
    /// Ceremony framing presets (camera Phase 4, MASTER-PLAN §3.E): when a ceremony's card plays,
    /// the camera goes to the room where that ceremony happens - the nomination room's table, the
    /// living room for the veto meeting, an eviction and the winner, the game room for the veto
    /// draw, the yard for a competition - on the clock, and comes back to where it was once the card
    /// and the reveal are done. Reduced motion cuts there and back instead of moving.
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>
        /// The room a ceremony is framed in, or null for a kind with no set. The veto meeting is the
        /// living room's (PACK8-PASS-PLAN decision 5), where it is staged, so the card on the HUD
        /// frame and the live feed name the room the staged meeting plays in.
        /// </summary>
        public static string CeremonyRoom(string kind)
        {
            switch (kind)
            {
                case CeremonySting.NominationKind: return "Nomination";
                case CeremonySting.VetoKind: return "Living";
                case CeremonyTakeover.VetoSelectionKind: return "Games";
                case CeremonySting.EvictionKind: return "Living";
                case CeremonySting.WinnerKind: return "Living";
                case CeremonySting.FinalEvictionKind: return "Living";
                case "competition": return "Yard";
                default: return StoryFallout.RoomFor(kind);
            }
        }

        /// <summary>A room-wide shot: the whole set and everyone on it, faces still readable.</summary>
        public const float CeremonyDistance = 11f;
        /// <summary>The move in, and the move back, each take this long.</summary>
        public const float CeremonyMoveSeconds = 1.2f;
        /// <summary>Under reduced motion the camera cuts: a move this short reads as a cut, not a pan.</summary>
        public const float CeremonyCutSeconds = 0.01f;
        /// <summary>A card's strip lasts a few seconds; the framing holds at least this long.</summary>
        public const float CeremonyHoldSeconds = 2.5f;

        private Coroutine ceremonyFrame;
        public bool IsFramingCeremony { get; private set; }

        /// <summary>The rig's <see cref="HouseCameraRig.ViewTakeovers"/> just after the framing's move: what nothing has changed while the framing holds the camera.</summary>
        private int ceremonyFrameTakeovers;

        /// <summary>
        /// Whether the ceremony's framing holds the camera (ROOM-SCOPE-PLAN RS6a): it is framing, and
        /// nothing has taken the view since its move - no input moved the camera, nothing followed,
        /// cut, moved or took a shot. The framing is a free move, with the controls left on, so F, a
        /// pan or a follow during its hold makes the view theirs; the room scope's ceremony source is
        /// the framing's only while this holds.
        /// </summary>
        public bool CeremonyFramingHoldsTheCamera
            => IsFramingCeremony && cameraRig != null && cameraRig.ViewTakeovers == ceremonyFrameTakeovers;

        /// <summary>Frames the ceremony's room and returns when its card is done. Public for the tests.</summary>
        public void FrameCeremony(string kind)
        {
            if (cameraRig == null || !gameObject.activeInHierarchy) return;
            var room = CeremonyRoom(kind);
            if (room == null) return;
            var marker = gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HouseRoomMarker>(true))
                .FirstOrDefault(m => m.RoomName == room);
            if (marker == null) return;
            if (ceremonyFrame != null) StopCoroutine(ceremonyFrame);
            ceremonyFrame = StartCoroutine(FrameCeremonyRoutine(marker.transform.position, RoomSet.Of(room)));
        }

        /// <summary>
        /// The move to the ceremony's room names that room (ROOM-SCOPE-PLAN D6, RS6a): while it travels
        /// the rooms the view drew as it left are drawn with it - free play's whole house until RS5 -
        /// and from its landing the ceremony's room alone, until the framing lets go or something else
        /// takes the view (<see cref="CeremonyFramingHoldsTheCamera"/>). The move back is the view's
        /// own again - free play's, whole house until RS5 - since the framing is over as it starts.
        /// </summary>
        private IEnumerator FrameCeremonyRoutine(Vector3 at, RoomSet rooms)
        {
            // Where the viewer was, to come back to - unless a framing was already in flight, in
            // which case that framing's origin is the one to keep.
            var focus = IsFramingCeremony ? ceremonyReturnFocus : cameraRig.DesiredFocus;
            var distance = IsFramingCeremony ? ceremonyReturnDistance : cameraRig.DesiredDistance;
            ceremonyReturnFocus = focus; ceremonyReturnDistance = distance;
            // What the view drew as the framing takes it, read before the framing is the view's source:
            // the move draws it with the ceremony's room until it lands, so nothing the viewer saw goes
            // before the camera has moved.
            var left = ViewedRooms;
            IsFramingCeremony = true;
            // Reduced motion cuts to the room rather than keeping the shot the viewer had: less
            // motion, not less of the episode. The ceremony card's own rule, applied to the camera.
            float move = reducedMotion ? CeremonyCutSeconds : CeremonyMoveSeconds;
            cameraRig.MoveTo(new Vector3(at.x, focus.y, at.z), CeremonyDistance, move, rooms, left);
            ceremonyFrameTakeovers = cameraRig.ViewTakeovers;
            float until = Time.unscaledTime + CeremonyHoldSeconds;
            yield return null;
            // Held for as long as any card narrating the ceremony is up; the key reveal was left out
            // of this, so the camera could pull back to the house halfway through the keys.
            while (Time.unscaledTime < until || (takeover != null && takeover.IsPlaying)
                || (voteReveal != null && voteReveal.IsPlaying) || (keyCeremony != null && keyCeremony.IsPlaying) || JuryRevealPlaying)
                yield return null;
            if (cameraRig != null) cameraRig.MoveTo(focus, distance, reducedMotion ? CeremonyCutSeconds : CeremonyMoveSeconds);
            IsFramingCeremony = false;
            ceremonyFrame = null;
        }

        private Vector3 ceremonyReturnFocus;
        private float ceremonyReturnDistance;
    }
}
