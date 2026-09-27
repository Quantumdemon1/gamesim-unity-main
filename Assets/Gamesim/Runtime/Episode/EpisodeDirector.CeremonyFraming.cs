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
    /// game room for the veto, the living room for an eviction and the winner, the yard for a
    /// competition - on the clock, and comes back to where it was once the card and the reveal
    /// are done. Reduced motion cuts there and back instead of moving.
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>The room a ceremony is framed in, or null for a kind with no set.</summary>
        public static string CeremonyRoom(string kind)
        {
            switch (kind)
            {
                case CeremonySting.NominationKind: return "Nomination";
                case CeremonySting.VetoKind: return "Games";
                case CeremonyTakeover.VetoSelectionKind: return "Games";
                case CeremonySting.EvictionKind: return "Living";
                case CeremonySting.WinnerKind: return "Living";
                case CeremonySting.FinalEvictionKind: return "Living";
                case "competition": return "Yard";
                default: return null;
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
            ceremonyFrame = StartCoroutine(FrameCeremonyRoutine(marker.transform.position));
        }

        private IEnumerator FrameCeremonyRoutine(Vector3 at)
        {
            // Where the viewer was, to come back to - unless a framing was already in flight, in
            // which case that framing's origin is the one to keep.
            var focus = IsFramingCeremony ? ceremonyReturnFocus : cameraRig.DesiredFocus;
            var distance = IsFramingCeremony ? ceremonyReturnDistance : cameraRig.DesiredDistance;
            ceremonyReturnFocus = focus; ceremonyReturnDistance = distance;
            IsFramingCeremony = true;
            // Reduced motion cuts to the room rather than keeping the shot the viewer had: less
            // motion, not less of the episode. The ceremony card's own rule, applied to the camera.
            float move = reducedMotion ? CeremonyCutSeconds : CeremonyMoveSeconds;
            cameraRig.MoveTo(new Vector3(at.x, focus.y, at.z), CeremonyDistance, move);
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
