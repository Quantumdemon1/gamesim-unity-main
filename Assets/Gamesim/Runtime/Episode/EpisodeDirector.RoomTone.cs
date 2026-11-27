using Gamesim.House;
using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>Room tone per room (MASTER-PLAN §3.C): the audio follows the room the camera looks into.</summary>
    public sealed partial class EpisodeDirector
    {
        private HouseRoomQuery roomToneQuery;
        private bool roomToneQueried;
        private float roomToneNext;
        // When the focus last stopped being in any room, NaN while it is in one (RoomToneBlend.HeldRoom).
        private float roomToneMissedSince = float.NaN;
        /// <summary>The room whose tone plays, as the director last decided it.</summary>
        public string RoomUnderCamera { get; private set; }

        /// <summary>
        /// A few times a second, not every frame: the room under the camera's focus, and the audio
        /// told when it changes. The focus rather than the player, because the viewer is where the
        /// camera is - following a houseguest into the kitchen should sound like the kitchen.
        /// A focus in no room - a doorway, a gap between floors - keeps the last room's tone for
        /// <see cref="Gamesim.Presentation.RoomToneBlend.FallbackHoldSeconds"/> before the generic
        /// air, so walking through a doorway is one crossfade rather than two.
        /// </summary>
        private void TickRoomTone()
        {
            if (audioBed == null || cameraRig == null || Time.unscaledTime < roomToneNext) return;
            roomToneNext = Time.unscaledTime + 0.25f;
            if (!roomToneQueried)
            {
                roomToneQueried = true;
                HouseRoomQuery.TryCreate(gameObject.scene, out roomToneQuery, out _);
            }
            if (roomToneQuery == null) return;
            var focus = cameraRig.DesiredFocus;
            var feet = new Vector3(focus.x, 0f, focus.z);
            string found = roomToneQuery.TryLocate(feet, 0.35f, out var located) ? located : null;
            string room = Gamesim.Presentation.RoomToneBlend.HeldRoom(found, RoomUnderCamera, Time.unscaledTime, ref roomToneMissedSince);
            if (room == RoomUnderCamera) return;
            RoomUnderCamera = room;
            audioBed.SetRoom(room);
        }
    }
}
