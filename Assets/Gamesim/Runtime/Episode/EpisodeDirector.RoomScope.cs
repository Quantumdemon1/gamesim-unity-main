using System;
using System.Collections.Generic;
using Gamesim.House;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gamesim.Episode
{
    /// <summary>
    /// The room scope (ROOM-SCOPE-PLAN §3.2 a and h, RS1): which of the house's rooms the main view is
    /// about, read from the camera rig's rooms, with the holds on top.
    ///
    /// <para>RS1 computes and reports. Every source is off (<see cref="LiveRoomScopeSources"/>), so the
    /// scope says the whole house whatever the camera does. Each later slice turns its own source on in
    /// the slice that frames it (RS6a ceremonies and competitions, RS6b the other shots, RS5 free play
    /// and following), so no slice ships an interim rule. Tests and the probe switch every source on
    /// with <see cref="HoldRoomScope"/>; captures that must see everything take
    /// <see cref="HoldWholeHouse"/>, which wins.</para>
    ///
    /// <para>The first reader that draws is the lamps' hook (RS3, §3.2 d, D8 i): while the scope is a
    /// room, the main camera renders with the lamps homed outside it off, and puts them back when it
    /// is done. Until a source goes live, only a hold makes the scope a room, so players see today's
    /// lamps.</para>
    ///
    /// <para>The scope also keeps every body's walker room, every frame: the player's and each
    /// houseguest's, by the floors (<see cref="HouseRoomScope.WalkerRoom"/>). Who-is-where reads them
    /// (RSW, EpisodeDirector.WhoIsWhere).</para>
    ///
    /// <para>The holds and the scope belong to this director, so a new play session or a reloaded scene
    /// starts with none. The one static is the lamps' subscription to the render pipeline's camera
    /// events, which is reset at SubsystemRegistration (statics outlive play sessions here).</para>
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
        private float roomScopeRetry, roomScopeForgetAt;
        private readonly List<object> wholeHouseHolds = new List<object>(4);
        private readonly List<object> roomScopeHolds = new List<object>(4);

        /// <summary>The house's rooms: its floor boxes, the walker rule, the classifier. Null until the house is loaded.</summary>
        public HouseRoomScope RoomScope => EnsureRoomScope() ? roomScope : null;

        /// <summary>
        /// The rooms the main view draws: the camera rig's rooms when the view's source is live or a
        /// test holds the scope on, otherwise - and always under a whole-house hold, the overview, the
        /// opening and the walk-out - the whole house.
        /// </summary>
        public RoomSet ViewedRooms
        {
            get
            {
                if (wholeHouseHolds.Count > 0 || cameraRig == null || !EnsureRoomScope()) return RoomSet.WholeHouse;
                var source = CurrentRoomScopeSource;
                if (IsWholeHouseSource(source)) return RoomSet.WholeHouse;
                if (roomScopeHolds.Count == 0 && !IsRoomScopeSourceLive(source)) return RoomSet.WholeHouse;
                return cameraRig.Rooms;
            }
        }

        /// <summary>Whether a source's rooms are used: only those turned on in <see cref="LiveRoomScopeSources"/>.</summary>
        public static bool IsRoomScopeSourceLive(RoomScopeSource source)
        {
            if (IsWholeHouseSource(source)) return false;
            int bit = 1 << (int)source;
            return ((int)LiveRoomScopeSources & bit) != 0;
        }

        /// <summary>
        /// The overview, the opening and the walk-out: the whole house by decision (D1, D6), with no
        /// switch, so neither a slice nor <see cref="HoldRoomScope"/> makes them a room, whatever the
        /// rig follows over them (the walk-out follows the one leaving).
        /// </summary>
        public static bool IsWholeHouseSource(RoomScopeSource source)
            => source == RoomScopeSource.Overview || source == RoomScopeSource.Opening || source == RoomScopeSource.WalkOut;

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
        /// probe sees a source's rooms before its slice makes it live. The overview, the opening and
        /// the walk-out have no switch and stay the whole house (<see cref="IsWholeHouseSource"/>).
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
        /// allocation once each body has its row. Once a second, the rows of bodies since destroyed
        /// (a rebuilt cast, a subject the rig followed) go.
        /// </summary>
        private void TickRoomScope()
        {
            // Between frames every lamp reads as authored, even after a main-camera render that never
            // reached its end (another begin handler threw): what it left off goes back here.
            RestoreScopedLamps();
            if (!EnsureRoomScope()) return;
            if (cameraRig != null && cameraRig.RoomScope != roomScope) cameraRig.UseRoomScope(roomScope);
            if (Time.unscaledTime >= roomScopeForgetAt)
            {
                roomScopeForgetAt = Time.unscaledTime + 1f;
                roomScope.ForgetMissing();
            }
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
            // The lamps and their homes once, with the house: the scene's fourteen (RS1 pins them).
            // A light made later is never a lamp the hook switches, so it is drawn as it is.
            roomScope.LampHomes(scopeLamps);
            if (cameraRig != null) cameraRig.UseRoomScope(roomScope);
            return true;
        }

        /// <summary>Lets a scope whose house no longer validates go; the next reader builds a new one.</summary>
        private void DropRoomScope()
        {
            RestoreScopedLamps();
            scopeLamps.Clear();
            roomScope = null;
            roomScopeRetry = 0f;
            if (cameraRig != null) cameraRig.UseRoomScope(null);
        }

        // ------------------------------------------------------------------------------------
        // The lamps (RS3, §3.2 d, D8 i): for the main camera only, every lamp homed outside the
        // scope is off from the camera's begin of rendering to its end, and put back after.
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// The directors the lamps' hook serves, and whether the hook is on the pipeline's camera
        /// events. One static pair of handlers, on while any director is enabled: the editor starts
        /// play without a domain reload, so a handler subscribed in one session would otherwise
        /// still be there in the next. <see cref="ForgetLampScope"/> lets both go before a session
        /// begins, so a session that never let go leaves neither a second subscription nor its
        /// destroyed director behind.
        /// </summary>
        private static readonly List<EpisodeDirector> lampScopeDirectors = new List<EpisodeDirector>(2);
        private static bool lampScopeHooked;

        /// <summary>Every lamp of the house and the room it stands in, read with the scope.</summary>
        private readonly List<HouseRoomScope.LampHome> scopeLamps = new List<HouseRoomScope.LampHome>(16);
        /// <summary>The lamps this render of the main camera switched off, and the camera: put back at its end.</summary>
        private readonly List<Light> lampsOffForView = new List<Light>(16);
        private Camera lampsOffFor;

        /// <summary>
        /// How many lamps the main camera has off: inside its render, after the hook, the lamps homed
        /// outside the scope that were on; between frames, always none.
        /// </summary>
        public int LampsOffForTheMainCamera => lampsOffForView.Count;

        /// <summary>On enable (EpisodeDirector.OnEnable): this director's main camera joins the hook.</summary>
        private void HookLampScope()
        {
            if (!lampScopeDirectors.Contains(this)) lampScopeDirectors.Add(this);
            if (lampScopeHooked) return;
            RenderPipelineManager.beginCameraRendering += BeginLampScope;
            RenderPipelineManager.endCameraRendering += EndLampScope;
            lampScopeHooked = true;
        }

        /// <summary>On disable: every lamp it has off goes back, and the last director out takes the hook with it.</summary>
        private void UnhookLampScope()
        {
            RestoreScopedLamps();
            lampScopeDirectors.Remove(this);
            for (int i = lampScopeDirectors.Count - 1; i >= 0; i--)
                if (lampScopeDirectors[i] == null) lampScopeDirectors.RemoveAt(i);
            if (lampScopeDirectors.Count > 0 || !lampScopeHooked) return;
            RenderPipelineManager.beginCameraRendering -= BeginLampScope;
            RenderPipelineManager.endCameraRendering -= EndLampScope;
            lampScopeHooked = false;
        }

        /// <summary>A play session starts with no hook and no director in it; the first director to enable subscribes.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ForgetLampScope()
        {
            RenderPipelineManager.beginCameraRendering -= BeginLampScope;
            RenderPipelineManager.endCameraRendering -= EndLampScope;
            lampScopeHooked = false;
            lampScopeDirectors.Clear();
        }

        private static void BeginLampScope(ScriptableRenderContext context, Camera camera)
        {
            for (int i = lampScopeDirectors.Count - 1; i >= 0; i--)
            {
                var director = lampScopeDirectors[i];
                if (director == null) { lampScopeDirectors.RemoveAt(i); continue; }
                director.DarkenLampsOutsideScope(camera);
            }
        }

        private static void EndLampScope(ScriptableRenderContext context, Camera camera)
        {
            for (int i = lampScopeDirectors.Count - 1; i >= 0; i--)
            {
                var director = lampScopeDirectors[i];
                if (director != null) director.RestoreLampsAfter(camera);
            }
        }

        /// <summary>
        /// The main camera's begin of rendering: URP culls after this callback, so a lamp switched off
        /// here is not among the camera's visible lights, casts no shadow maps and lights nothing in
        /// its frame. Only lamps that are on are switched, and each is recorded, so the end puts back
        /// exactly what was changed. The fills, the screens' lights, the Moon and any light made at
        /// play (the follow spotlight) are never lamps here. Every other camera - the live feed, the
        /// portraits, the HUD's overlay, the editor's - renders with the lamps as they are.
        /// </summary>
        private void DarkenLampsOutsideScope(Camera camera)
        {
            if (camera == null || cameraRig == null || camera != cameraRig.ViewCamera) return;
            // A render of the main camera that never reached its end: put back what it left off first.
            RestoreScopedLamps();
            var viewed = ViewedRooms;
            if (viewed.IsWholeHouse || viewed.IsEmpty) return;
            for (int i = 0; i < scopeLamps.Count; i++)
            {
                var light = scopeLamps[i].Light;
                if (light == null || !light.enabled || !HouseRoomScope.LampOutsideScope(scopeLamps[i].Room, viewed)) continue;
                light.enabled = false;
                lampsOffForView.Add(light);
            }
            if (lampsOffForView.Count > 0) lampsOffFor = camera;
        }

        /// <summary>The end of a camera's rendering: the main camera's puts its lamps back.</summary>
        private void RestoreLampsAfter(Camera camera)
        {
            if (lampsOffForView.Count == 0 || camera != lampsOffFor) return;
            RestoreScopedLamps();
        }

        private void RestoreScopedLamps()
        {
            if (lampsOffForView.Count == 0) return;
            for (int i = 0; i < lampsOffForView.Count; i++)
                if (lampsOffForView[i] != null) lampsOffForView[i].enabled = true;
            lampsOffForView.Clear();
            lampsOffFor = null;
        }
    }
}
