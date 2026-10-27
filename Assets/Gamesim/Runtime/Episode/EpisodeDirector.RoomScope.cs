using System;
using System.Collections.Generic;
using Gamesim.House;
using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>
    /// The room scope (ROOM-SCOPE-PLAN §3.2 a and h, RS1): which of the house's rooms the main view is
    /// about, read from the camera rig's rooms, with the holds on top.
    ///
    /// <para>RS1 computes and reports. Every source is off (<see cref="LiveRoomScopeSources"/>), so the
    /// scope says the whole house whatever the camera does, and nothing reads it to draw: no layer,
    /// mask or light changes. Each later slice turns its own source on in the slice that frames it
    /// (RS6a ceremonies and competitions, RS6b the other shots, RS5 free play and following), so no
    /// slice ships an interim rule. Tests and the probe switch every source on with
    /// <see cref="HoldRoomScope"/>; captures that must see everything take
    /// <see cref="HoldWholeHouse"/>, which wins.</para>
    ///
    /// <para>The scope also keeps every body's walker room, every frame: the player's and each
    /// houseguest's, by the floors (<see cref="HouseRoomScope.WalkerRoom"/>). Nothing reads them for
    /// gameplay yet; who-is-where moves to them in RSW.</para>
    ///
    /// <para>No statics: the holds and the scope belong to this director, so a new play session or a
    /// reloaded scene starts with none (statics outlive play sessions here).</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>What the main view is doing, as far as the room scope is concerned.</summary>
        public enum RoomScopeSource
        {
            /// <summary>Free roam: the player's room (RS5).</summary>
            FreePlay,
            /// <summary>Following a houseguest: theirs (RS5).</summary>
            Following,
            /// <summary>Jury or evicted, watching a room (RS6b).</summary>
            Watching,
            /// <summary>The conversation two-shot: the pair's rooms (RS6b).</summary>
            Conversation,
            /// <summary>A ceremony's framing and staging (RS6a).</summary>
            Ceremony,
            /// <summary>A competition: the Yard (RS6a).</summary>
            Competition,
            /// <summary>The diary room: Private (RS6b).</summary>
            Diary,
            /// <summary>A house event's shot: its people's rooms (RS6b).</summary>
            HouseEvent,
            /// <summary>The overview: the whole house, always (D1).</summary>
            Overview,
            /// <summary>The opening walk-in: the whole house until it hands over (D6).</summary>
            Opening,
            /// <summary>The staged walk-out: the whole house (D6).</summary>
            WalkOut,
        }

        /// <summary>The sources, as switches.</summary>
        [Flags]
        public enum RoomScopeSources
        {
            None = 0,
            FreePlay = 1 << (int)RoomScopeSource.FreePlay,
            Following = 1 << (int)RoomScopeSource.Following,
            Watching = 1 << (int)RoomScopeSource.Watching,
            Conversation = 1 << (int)RoomScopeSource.Conversation,
            Ceremony = 1 << (int)RoomScopeSource.Ceremony,
            Competition = 1 << (int)RoomScopeSource.Competition,
            Diary = 1 << (int)RoomScopeSource.Diary,
            HouseEvent = 1 << (int)RoomScopeSource.HouseEvent,
        }

        /// <summary>
        /// The sources whose rooms the scope uses. None in RS1: every source reports the whole house.
        /// A slice turns its own on here, with the camera calls that name its rooms. The overview,
        /// the opening and the walk-out have no switch: they are the whole house by decision (D1, D6).
        /// </summary>
        public const RoomScopeSources LiveRoomScopeSources = RoomScopeSources.None;

        private HouseRoomScope roomScope;
        private float roomScopeRetry;
        private readonly List<object> wholeHouseHolds = new List<object>(4);
        private readonly List<object> roomScopeHolds = new List<object>(4);

        /// <summary>The house's rooms: its floor boxes, the walker rule, the classifier. Null until the house is loaded.</summary>
        public HouseRoomScope RoomScope => EnsureRoomScope() ? roomScope : null;

        /// <summary>
        /// The rooms the main view draws: the camera rig's rooms when the view's source is live or a
        /// test holds the scope on, otherwise - and always under a whole-house hold - the whole house.
        /// </summary>
        public RoomSet ViewedRooms
        {
            get
            {
                if (wholeHouseHolds.Count > 0 || cameraRig == null || !EnsureRoomScope()) return RoomSet.WholeHouse;
                if (roomScopeHolds.Count == 0 && !IsRoomScopeSourceLive(CurrentRoomScopeSource)) return RoomSet.WholeHouse;
                return cameraRig.Rooms;
            }
        }

        /// <summary>Whether a source's rooms are used: only those turned on in <see cref="LiveRoomScopeSources"/>.</summary>
        public static bool IsRoomScopeSourceLive(RoomScopeSource source)
        {
            int bit = 1 << (int)source;
            return ((int)LiveRoomScopeSources & bit) != 0;
        }

        /// <summary>What the main view is doing now, for the scope's switches.</summary>
        public RoomScopeSource CurrentRoomScopeSource
        {
            get
            {
                if (overviewOpen) return RoomScopeSource.Overview;
                if (OpeningOwnsHouse) return RoomScopeSource.Opening;
                if (walkingOutId != null) return RoomScopeSource.WalkOut;
                if (IsFramingCeremony || IsCeremonyStaged) return RoomScopeSource.Ceremony;
                if (challengeActive) return RoomScopeSource.Competition;
                if (diaryOpen) return RoomScopeSource.Diary;
                if (cameraRig != null && cameraRig.IsConversationFocused) return RoomScopeSource.Conversation;
                if (IsFramingHouseEvent) return RoomScopeSource.HouseEvent;
                if (!playerIsActive) return RoomScopeSource.Watching;
                var subject = cameraRig != null ? cameraRig.FocusedSubject : null;
                if (subject != null && (player == null || subject != player.transform)) return RoomScopeSource.Following;
                return RoomScopeSource.FreePlay;
            }
        }

        /// <summary>
        /// Holds the whole house drawn for as long as <paramref name="owner"/> wants it: a capture or a
        /// test that must see everything. Counted by owner, so two owners hold it until both let go;
        /// the same owner twice is one hold. It wins over <see cref="HoldRoomScope"/>.
        /// </summary>
        public void HoldWholeHouse(object owner) => Hold(wholeHouseHolds, owner);

        /// <summary>
        /// Switches every source on for as long as <paramref name="owner"/> holds it, so a test or the
        /// probe sees a source's rooms before its slice makes it live.
        /// </summary>
        public void HoldRoomScope(object owner) => Hold(roomScopeHolds, owner);

        /// <summary>Lets go of whatever <paramref name="owner"/> holds: the whole house, the scope, or both.</summary>
        public void Release(object owner)
        {
            if (owner == null) return;
            wholeHouseHolds.Remove(owner);
            roomScopeHolds.Remove(owner);
        }

        /// <summary>Whether anything holds the whole house drawn.</summary>
        public bool IsWholeHouseHeld => wholeHouseHolds.Count > 0;

        private static void Hold(List<object> holds, object owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (!holds.Contains(owner)) holds.Add(owner);
        }

        /// <summary>
        /// The walker room of a body - the player or a houseguest - as the scope last tracked it, by
        /// the floors: kept through a doorway until the feet are inside the next room. Null for no body.
        /// </summary>
        public string WalkerRoomOf(Transform body)
        {
            if (body == null || !EnsureRoomScope()) return null;
            return HouseRoomScope.RoomId(roomScope.Track(body));
        }

        /// <summary>
        /// Every frame, before anything reads a room: the scope built once the house is there, handed
        /// to the rig, and every body's walker room moved on. Sixteen points against eight boxes; no
        /// allocation once each body has its row.
        /// </summary>
        private void TickRoomScope()
        {
            if (!EnsureRoomScope()) return;
            if (cameraRig != null && cameraRig.RoomScope != roomScope) cameraRig.UseRoomScope(roomScope);
            if (player != null && player.isActiveAndEnabled) roomScope.Track(player.transform);
            if (housemates == null) return;
            foreach (var housemate in housemates)
                if (housemate != null && housemate.isActiveAndEnabled) roomScope.Track(housemate.transform);
        }

        private bool EnsureRoomScope()
        {
            if (roomScope != null) return true;
            // A house the room query refuses is asked again once a second, not every frame.
            if (Time.unscaledTime < roomScopeRetry) return false;
            roomScopeRetry = Time.unscaledTime + 1f;
            if (!HouseRoomScope.TryCreate(gameObject.scene, out var built, out _)) return false;
            roomScope = built;
            if (cameraRig != null) cameraRig.UseRoomScope(roomScope);
            return true;
        }

        /// <summary>Lets a scope whose house no longer validates go; the next reader builds a new one.</summary>
        private void DropRoomScope()
        {
            roomScope = null;
            roomScopeRetry = 0f;
            if (cameraRig != null) cameraRig.UseRoomScope(null);
        }
    }
}
