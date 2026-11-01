using System;
using System.Collections.Generic;
using Gamesim.House;
using Gamesim.Presentation;
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
    /// <para>The second is the bodies (RS4, §3.2 c and e): a body is drawn while it is in a viewed room
    /// or its doorway band, is a camera subject or is named by a live prompt; a hidden body's renderer
    /// objects stand on its walker room's layer, which the main camera's mask leaves out, its plate is
    /// down and it cannot be picked, and it keeps animating, walking and sitting. The live feed's
    /// camera draws the shell and its own room. With the whole house drawn nothing is hidden and no
    /// mask bit moves, so, again, players see today's house.</para>
    ///
    /// <para>The third is the house itself (RS2a, §3.2 b): built, the scope puts every collider-free
    /// piece of furniture and dressing on its room's layer (HouseRoomScope.Dressing), and from then on
    /// every staging point places what it builds or moves in the same call, a piece not drawn yet is
    /// placed the frame it first is, and a piece that moves goes to the shell, all in this director's
    /// late pass. With nothing held the main camera draws every room layer, so players see today's
    /// house; the live feed, whose mask RS4 already writes, draws its own room's furniture and the shell.</para>
    ///
    /// <para>The scope also keeps every body's walker room, every frame: the player's and each
    /// houseguest's, by the floors (<see cref="HouseRoomScope.WalkerRoom"/>). Who-is-where reads them
    /// (RSW, EpisodeDirector.WhoIsWhere).</para>
    ///
    /// <para>The holds and the scope belong to this director, so a new play session or a reloaded scene
    /// starts with none. The statics are the lamps' subscription to the render pipeline's camera events
    /// and the list of scopes placing their houses' dressing (HouseRoomScope.PlacingFor), each reset at
    /// SubsystemRegistration (statics outlive play sessions here).</para>
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
        public void HoldWholeHouse(object owner)
        {
            Hold(wholeHouseHolds, owner);
            ApplyRoomScopeDrawing();
        }

        /// <summary>
        /// Switches every source on for as long as <paramref name="owner"/> holds it, so a test or the
        /// probe sees a source's rooms before its slice makes it live. The overview, the opening and
        /// the walk-out have no switch and stay the whole house (<see cref="IsWholeHouseSource"/>).
        /// </summary>
        public void HoldRoomScope(object owner)
        {
            Hold(roomScopeHolds, owner);
            ApplyRoomScopeDrawing();
        }

        /// <summary>
        /// Lets go of whatever <paramref name="owner"/> holds: the whole house, the scope, or both. Every
        /// hold and release draws what it changes at once (the bodies, the main camera's mask), so the
        /// code that follows it in the same frame reads the house as drawn.
        /// </summary>
        public void Release(object owner)
        {
            if (owner == null) return;
            wholeHouseHolds.Remove(owner);
            roomScopeHolds.Remove(owner);
            ApplyRoomScopeDrawing();
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
                ForgetMissingScopedBodies();
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
            // The lamps and their homes when the scope is built, and again if a dropped scope is
            // rebuilt. The house makes no shadowed point light at play, so they are the scene's
            // fourteen (RS1 pins them).
            roomScope.LampHomes(scopeLamps);
            // The house's dressing on its rooms' layers, at load (RS2a): the director's first Update,
            // before its Start goes on to build anything. From here its house's staging points place
            // what they build or move, through the scene (HouseRoomScope.PlaceDressing).
            roomScope.PlaceHouse();
            if (isActiveAndEnabled) HouseRoomScope.BeginPlacing(roomScope);
            if (cameraRig != null) cameraRig.UseRoomScope(roomScope);
            return true;
        }

        /// <summary>Lets a scope whose house no longer validates go; the next reader builds a new one.</summary>
        private void DropRoomScope()
        {
            UndoRoomScopeDrawing();
            RestoreScopedLamps();
            scopeLamps.Clear();
            if (roomScope != null)
            {
                HouseRoomScope.EndPlacing(roomScope);
                // Nothing left on a room layer that no scope watches; the next scope places the house again.
                roomScope.UndoDressing();
            }
            roomScope = null;
            roomScopeRetry = 0f;
            if (cameraRig != null) cameraRig.UseRoomScope(null);
        }

        /// <summary>
        /// Late every frame, in the house's overlay pass, before the bodies and the mask (RS2a): a placed
        /// piece that has moved goes to the shell, and a piece first drawn this frame takes its room's
        /// layer, both ahead of the main camera's render. Nothing before the scope is built.
        /// </summary>
        private void KeepRoomDressing()
        {
            if (roomScope != null && isActiveAndEnabled) roomScope.KeepDressingPlaced();
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

        /// <summary>On enable (EpisodeDirector.OnEnable): this director's main camera joins the hook, and its scope, if built, places its house's dressing again.</summary>
        private void HookLampScope()
        {
            if (roomScope != null) HouseRoomScope.BeginPlacing(roomScope);
            if (!lampScopeDirectors.Contains(this)) lampScopeDirectors.Add(this);
            if (lampScopeHooked) return;
            RenderPipelineManager.beginCameraRendering += BeginLampScope;
            RenderPipelineManager.endCameraRendering += EndLampScope;
            lampScopeHooked = true;
        }

        /// <summary>
        /// On disable: every lamp it has off goes back, so does every body it has hidden and every bit it
        /// has out of the main camera's mask, and the last director out takes the hook with it. The
        /// dressing stays on its rooms' layers, which the restored mask draws, and its house's staging
        /// points place nothing until it is enabled again; a piece moved meanwhile goes to the shell then,
        /// since the flag that says so waits.
        /// </summary>
        private void UnhookLampScope()
        {
            if (roomScope != null) HouseRoomScope.EndPlacing(roomScope);
            UndoRoomScopeDrawing();
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

        /// <summary>
        /// Every camera's begin of rendering, in every build. A director that throws is logged and its
        /// lamps put back, and the throw goes no further: past this handler it would skip the event's
        /// later subscribers and the other directors, and URP would draw no frame for the camera and
        /// never reach its end.
        /// </summary>
        private static void BeginLampScope(ScriptableRenderContext context, Camera camera)
        {
            for (int i = lampScopeDirectors.Count - 1; i >= 0; i--)
            {
                var director = lampScopeDirectors[i];
                if (director == null) { lampScopeDirectors.RemoveAt(i); continue; }
                // The bodies and the mask first (RS4), for a render that finds the view moved since.
                try { director.RedrawScopeForRender(camera); }
                catch (Exception e) { Debug.LogException(e, director); }
                try { director.DarkenLampsOutsideScope(camera); }
                catch (Exception e)
                {
                    Debug.LogException(e, director);
                    director.RestoreScopedLamps();
                }
            }
        }

        private static void EndLampScope(ScriptableRenderContext context, Camera camera)
        {
            for (int i = lampScopeDirectors.Count - 1; i >= 0; i--)
            {
                var director = lampScopeDirectors[i];
                if (director == null) continue;
                try { director.RestoreLampsAfter(camera); }
                catch (Exception e)
                {
                    Debug.LogException(e, director);
                    director.RestoreScopedLamps();
                }
            }
        }

        /// <summary>
        /// The main camera's begin of rendering: URP culls after this callback, so a lamp switched off
        /// here is not among the camera's visible lights, casts no shadow maps and lights nothing in
        /// its frame. Only lamps that are on are switched, and each is recorded, so the end puts back
        /// exactly what was changed. A lamp is a shadowed point light, so the fills, the screens'
        /// lights, the Moon and the follow spotlight are never lamps here. Every other camera - the
        /// live feed, the portraits, the HUD's overlay, the editor's - renders with the lamps as they are.
        /// </summary>
        private void DarkenLampsOutsideScope(Camera camera)
        {
            if (camera == null || cameraRig == null || camera != cameraRig.ViewCamera) return;
            // A render of the main camera that never reached its end: put back what it left off first.
            RestoreScopedLamps();
            // A render reads only a scope Update has built, and never builds one. With nothing held
            // and no source live the view is the whole house (ViewedRooms), so a player's render
            // stops at a field check.
            if (roomScope == null) return;
            if (roomScopeHolds.Count == 0 && LiveRoomScopeSources == RoomScopeSources.None) return;
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

        // ------------------------------------------------------------------------------------
        // The bodies (RS4, §3.2 e) and the main camera's mask (§3.2 c; RS4 takes the mask write, D16).
        // ------------------------------------------------------------------------------------

        /// <summary>One body the scope draws or hides: its renderer objects, and what it last did with them.</summary>
        private sealed class ScopedBody
        {
            public Transform Body;
            public HouseNpc Npc;
            public CharacterPresentation Presentation;
            /// <summary>The visual root and the finished-body count at the last look over its renderers: a change of either is a visual swap.</summary>
            public Transform Visual;
            public int Completed = -1;
            /// <summary>The body's hierarchy's transform count at the last look: a piece grown on it, or a renderer object made anew, changes it.</summary>
            public int Hierarchy = -1;
            public float LookAgainAt;
            /// <summary>
            /// The frame a look passed over a renderer object for its collider, or -1. Unity removes a
            /// Destroy()ed component only at the frame's end - the primitive rig's parts lose theirs that
            /// way - so such a body is looked over once more on a later frame, when the collider has gone.
            /// </summary>
            public int SkippedAt = -1;
            /// <summary>Every renderer's object under the body but the root's own, with no collider: what the scope layers.</summary>
            public readonly List<GameObject> Pieces = new List<GameObject>(8);
            public bool Hidden;
            /// <summary>The walker room it is hidden in, whose layer its pieces stand on; -1 while drawn.</summary>
            public int Room = -1;
        }

        /// <summary>
        /// How often a body's renderer objects are looked over again with no change seen: a renderer
        /// UMA keeps but reuses in a rebuild, or anything else the signs below miss. A swap itself - a
        /// change of clothes, a body finishing assembly, the primitive rig standing in - and a piece or
        /// renderer object made on the body (its hierarchy's count moves) are seen the frame they land.
        /// </summary>
        private const float BodyLookSeconds = .5f;

        private readonly List<ScopedBody> scopedBodies = new List<ScopedBody>(17);
        private readonly List<Renderer> scopeRendererBuffer = new List<Renderer>(64);
        private int hiddenBodyCount;
        /// <summary>
        /// The room bits the main camera had when the scope began writing its mask (<see cref="RoomLayers.ScopeMask"/>),
        /// or <see cref="RoomLayers.NothingKept"/> while it writes none; the bits it has out under the
        /// view; and the camera.
        /// </summary>
        private int mainMaskKept = RoomLayers.NothingKept;
        private int mainMaskTaken;
        private Camera mainMaskCamera;
        /// <summary>The view the drawing was last applied for, by its bits: a main-camera render that finds the view moved since draws it again first.</summary>
        private int drawnViewBits = -1;
        /// <summary>The houseguest the E prompt names (the Talk target or the invitation's speaker), set as the prompt is chosen.</summary>
        private HouseNpc promptNamed;
        /// <summary>The houseguests the free-time cards tag HERE, as the HUD last drew them (EpisodeDirector.FreeTimeBoard and FreeTimeScreen).</summary>
        private readonly HashSet<string> hereTagged = new HashSet<string>(StringComparer.Ordinal);
        /// <summary>The pair the conversation line was last shown about: it stays up to be read after the witness is over.</summary>
        private string captionFirstId, captionSecondId;

        /// <summary>Whether the room scope draws a body - the player or a houseguest - as of its last pass; yes for one it has never hidden.</summary>
        public bool IsBodyDrawn(Transform body)
        {
            if (body == null) return true;
            for (int i = 0; i < scopedBodies.Count; i++)
                if (ReferenceEquals(scopedBodies[i].Body, body)) return !scopedBodies[i].Hidden;
            return true;
        }

        /// <summary>The room layer a hidden body's renderer objects stand on (its walker room's), or -1 for a body drawn.</summary>
        public int HiddenBodyLayer(Transform body)
        {
            if (body == null) return -1;
            for (int i = 0; i < scopedBodies.Count; i++)
                if (ReferenceEquals(scopedBodies[i].Body, body))
                    return scopedBodies[i].Hidden && scopedBodies[i].Room >= 0 ? RoomLayers.First + scopedBodies[i].Room : -1;
            return -1;
        }

        /// <summary>How many bodies the room scope has hidden: none while the whole house is drawn.</summary>
        public int HiddenBodyCount => hiddenBodyCount;

        /// <summary>The room bits the scope has taken out of the main camera's culling mask: none while the whole house is drawn.</summary>
        public int MainCameraRoomBitsTaken => mainMaskTaken;

        /// <summary>
        /// What the view draws of the bodies, and the main camera's mask (§3.2 c and e). Run late every
        /// frame, in the house's overlay pass, after the camera has moved and before the main camera
        /// renders; at once when a hold changes; and at a main-camera render that finds the view moved
        /// since, or its mask put back over the scope's. The live feed renders by hand in its own
        /// LateUpdate, before this pass, so a body that changes state reaches the feed at its next render.
        ///
        /// <para>A body is drawn while its walker room is viewed, its feet are in a viewed room's doorway
        /// band, it is a camera subject - the player in free play, the followed houseguest, either of a
        /// conversation's two - or a live prompt names it: the walk-in pair, the Talk target, the
        /// invitation's speaker, anyone tagged HERE. A hidden body's renderer objects, never its root,
        /// stand on its walker room's layer, which the main camera's mask leaves out; its plate is down,
        /// and the invitation bubble and the conversation line wait for a drawn speaker and pair. It
        /// keeps its Animator, its agent, its seat fit and every parameter: only the drawing stops.</para>
        ///
        /// <para>With the whole house drawn and nothing hidden - every player's frame until a source goes
        /// live - it stops at a few comparisons and writes nothing.</para>
        /// </summary>
        private void ApplyRoomScopeDrawing()
        {
            if (!isActiveAndEnabled) return;
            // Nothing held and no source live: the view is the whole house (ViewedRooms), so with
            // nothing hidden and nothing out of the mask there is nothing to draw differently.
            if (roomScopeHolds.Count == 0 && LiveRoomScopeSources == RoomScopeSources.None && hiddenBodyCount == 0
                && mainMaskKept == RoomLayers.NothingKept)
            {
                drawnViewBits = RoomSet.WholeHouse.Bits;
                GatePromptsOnDrawnBodies();
                return;
            }
            var viewed = ViewedRooms;
            bool whole = roomScope == null || viewed.IsWholeHouse || viewed.IsEmpty;
            if (whole) viewed = RoomSet.WholeHouse;
            drawnViewBits = viewed.Bits;
            SyncMainCameraMask(viewed);
            if (whole && hiddenBodyCount == 0) { GatePromptsOnDrawnBodies(); return; }
            if (player != null) RowOf(player.transform, null);
            if (housemates != null)
                foreach (var npc in housemates)
                    if (npc != null) RowOf(npc.transform, npc);
            var source = CurrentRoomScopeSource;
            int hidden = 0;
            for (int i = scopedBodies.Count - 1; i >= 0; i--)
            {
                var row = scopedBodies[i];
                if (row.Body == null) { scopedBodies.RemoveAt(i); continue; }
                ScopeBody(row, whole, viewed, source);
                if (row.Hidden) hidden++;
            }
            hiddenBodyCount = hidden;
            GatePromptsOnDrawnBodies();
        }

        private ScopedBody RowOf(Transform body, HouseNpc npc)
        {
            for (int i = 0; i < scopedBodies.Count; i++)
                if (ReferenceEquals(scopedBodies[i].Body, body)) return scopedBodies[i];
            var row = new ScopedBody { Body = body, Npc = npc };
            scopedBodies.Add(row);
            return row;
        }

        /// <summary>
        /// One body's drawn state, applied: its pieces on its own layer or its walker room's, and its
        /// plate. A body that is not active keeps what it had until it is, or until the whole house is
        /// drawn again; then everything it had hidden comes back.
        /// </summary>
        private void ScopeBody(ScopedBody row, bool whole, RoomSet viewed, RoomScopeSource source)
        {
            var body = row.Body;
            bool hidden = row.Hidden;
            int room = row.Room;
            if (whole) { hidden = false; room = -1; }
            else if (body.gameObject.activeInHierarchy)
            {
                int walker = roomScope.Track(body);
                hidden = !roomScope.DrawsWalker(viewed, walker, body.position)
                    && !IsCameraSubject(body, source) && !(row.Npc != null && NamedByPrompt(row.Npc));
                room = hidden ? walker : -1;
            }
            // A visual swap is looked over the frame it lands, and every body now and then; a body
            // drawn again is looked over first, so nothing it had hidden is left behind. A look that
            // passed over a piece for its collider is made once more on a later frame: a collider
            // Destroy()ed in the frame of the look is gone by then, and its piece is the scope's.
            bool swapped = row.Body.hierarchyCount != row.Hierarchy || (row.Presentation != null
                && (row.Presentation.VisualRoot != row.Visual || row.Completed != CharacterPresentation.BodiesCompleted));
            bool recheck = row.SkippedAt >= 0 && Time.frameCount > row.SkippedAt;
            // A swap this frame may bring colliders of its own on their way out, so its look is never the second.
            if (swapped || recheck || Time.unscaledTime >= row.LookAgainAt || (row.Hidden && !hidden)) LookOver(row, recheck && !swapped);
            row.Hidden = hidden;
            row.Room = room;
            for (int i = row.Pieces.Count - 1; i >= 0; i--)
            {
                var piece = row.Pieces[i];
                if (piece == null) { row.Pieces.RemoveAt(i); row.LookAgainAt = 0f; continue; }
                int layer = HouseRoomScope.BodyLayer(piece.layer, !hidden, room);
                if (layer != piece.layer) piece.layer = layer;
            }
            if (row.Npc != null) row.Npc.BodyHidden = hidden;
        }

        /// <summary>
        /// A body's renderer objects, read again: every renderer's object under it, active or not, but
        /// the root's own (parking moves the root to layer 8) and any with a collider (a layer is physics
        /// there, and no collider may stand on a room layer). A collider counts even disabled and on its
        /// way out, so a piece passed over for one is read again on a later frame - once, unless that
        /// look was itself the second.
        /// </summary>
        private void LookOver(ScopedBody row, bool recheck)
        {
            if (row.Presentation == null) row.Presentation = row.Body.GetComponent<CharacterPresentation>();
            row.Visual = row.Presentation != null ? row.Presentation.VisualRoot : null;
            row.Completed = CharacterPresentation.BodiesCompleted;
            row.Hierarchy = row.Body.hierarchyCount;
            row.LookAgainAt = Time.unscaledTime + BodyLookSeconds;
            row.Pieces.Clear();
            scopeRendererBuffer.Clear();
            row.Body.GetComponentsInChildren(true, scopeRendererBuffer);
            var root = row.Body.gameObject;
            bool skipped = false;
            for (int i = 0; i < scopeRendererBuffer.Count; i++)
            {
                var renderer = scopeRendererBuffer[i];
                if (renderer == null) continue;
                var piece = renderer.gameObject;
                if (piece == root || row.Pieces.Contains(piece)) continue;
                if (piece.GetComponent<Collider>() != null) { skipped = true; continue; }
                row.Pieces.Add(piece);
            }
            scopeRendererBuffer.Clear();
            row.SkippedAt = skipped && !recheck ? Time.frameCount : -1;
        }

        /// <summary>The camera's subjects (§3.2 e): the one it follows, either of a conversation's two, and the player in free play.</summary>
        private bool IsCameraSubject(Transform body, RoomScopeSource source)
        {
            if (cameraRig == null || body == null) return false;
            var subject = cameraRig.FocusedSubject;
            if (subject != null && (subject == body || subject.IsChildOf(body))) return true;
            if (cameraRig.IsConversationSubject(body)) return true;
            return player != null && body == player.transform && source == RoomScopeSource.FreePlay;
        }

        /// <summary>
        /// Whether a live prompt names a houseguest (§3.2 e): the E prompt's Talk target or the
        /// invitation's speaker, the walk-in pair while its Pull is on screen, the invitation bubble's
        /// speaker while it is up, or a free-time card's HERE. With who-is-where by floor (RSW) they
        /// stand on the player's floor anyway; this is the guard that keeps a prompt from naming
        /// someone the view hides.
        /// </summary>
        private bool NamedByPrompt(HouseNpc npc)
        {
            if (ReferenceEquals(npc, promptNamed)) return true;
            string id = npc.Id;
            if (WalkInPullShown
                && (string.Equals(id, walkInFirst, StringComparison.Ordinal) || string.Equals(id, walkInSecond, StringComparison.Ordinal)))
                return true;
            if (npcInvitationNow != null && npcInvitationBubble != null && npcInvitationBubble.IsVisible
                && string.Equals(id, npcInvitationNow.NpcId, StringComparison.Ordinal))
                return true;
            return hereTagged.Count > 0 && hereTagged.Contains(id);
        }

        /// <summary>
        /// Whether the Pull on screen is the walk-in's (EpisodeDirector.Story, TickStoryPull): the
        /// proximity watch's offer is not a prompt by itself, since the Pull waits under a panel, a
        /// challenge, a ceremony card, an invitation that is up, or the player not in play, and never
        /// comes back for a pair the player turned down, while the watch offers that pair again.
        /// </summary>
        private bool WalkInPullShown => walkInFirst != null && projected != null && walkInWeek == projected.week
            && pullNow.HasValue && pullWalkIn != null && string.Equals(pullNow.Value.Key, pullWalkIn, StringComparison.Ordinal);

        /// <summary>The invitation bubble over a drawn speaker only, and the conversation line over a drawn pair only.</summary>
        private void GatePromptsOnDrawnBodies()
        {
            if (hiddenBodyCount == 0)
            {
                if (npcInvitationBubble != null) npcInvitationBubble.SpeakerDrawn = true;
                if (npcCaption != null) npcCaption.PairDrawn = true;
                return;
            }
            if (npcInvitationBubble != null)
                npcInvitationBubble.SpeakerDrawn = npcInvitationNow == null || IsBodyDrawn(HousemateBody(npcInvitationNow.NpcId));
            if (npcCaption != null)
                npcCaption.PairDrawn = IsBodyDrawn(HousemateBody(captionFirstId)) && IsBodyDrawn(HousemateBody(captionSecondId));
        }

        /// <summary>A houseguest's body by id, or null.</summary>
        private Transform HousemateBody(string id)
        {
            if (string.IsNullOrEmpty(id) || housemates == null) return null;
            foreach (var npc in housemates)
                if (npc != null && string.Equals(npc.Id, id, StringComparison.Ordinal)) return npc.transform;
            return null;
        }

        /// <summary>
        /// The main camera's culling mask from the view (§3.2 c, <see cref="RoomLayers.ScopeMask"/>): the
        /// plan's <c>(mask &amp; ~RoomBits) | ScopeBits</c>, among the room bits the camera had when the
        /// scope began, which the whole house writes back. Bits 20 to 27 are the view's at every pass,
        /// so a stale mask put back over the scope's - a capture's save and restore around a render
        /// after a move - lasts until the next pass or main-camera render at most; nothing outside them
        /// is touched, so the capture guard on bit 31 keeps its owner's value, and a room bit cleared
        /// before the scope began stays clear. Nothing is written while the whole house is drawn and
        /// nothing is kept.
        /// </summary>
        private void SyncMainCameraMask(RoomSet viewed)
        {
            var camera = cameraRig != null ? cameraRig.ViewCamera : null;
            if (!ReferenceEquals(camera, mainMaskCamera)) { PutBackMainCameraMask(); mainMaskCamera = camera; }
            if (camera == null || (mainMaskKept == RoomLayers.NothingKept && viewed.IsWholeHouse)) return;
            int before = camera.cullingMask;
            int after = RoomLayers.ScopeMask(before, viewed, ref mainMaskKept);
            mainMaskTaken = RoomLayers.TakenOut(mainMaskKept, viewed);
            if (after != before) camera.cullingMask = after;
        }

        private void PutBackMainCameraMask()
        {
            if (mainMaskKept != RoomLayers.NothingKept && mainMaskCamera != null)
                mainMaskCamera.cullingMask = RoomLayers.ScopeMask(mainMaskCamera.cullingMask, RoomSet.WholeHouse, ref mainMaskKept);
            mainMaskKept = RoomLayers.NothingKept;
            mainMaskTaken = 0;
        }

        /// <summary>
        /// A main camera's render that finds the view moved since the drawing was applied - a capture's
        /// render by hand straight after a move, before the frame's late pass - draws it first; one that
        /// finds the view the same but the mask's room bits not the scope's - put back by a capture
        /// around an earlier render - writes the mask again. Every other render costs a field check:
        /// with nothing held, no source live, nothing hidden and no mask kept, none. A render never
        /// builds the scope.
        /// </summary>
        private void RedrawScopeForRender(Camera camera)
        {
            if (camera == null || cameraRig == null || roomScope == null || camera != cameraRig.ViewCamera) return;
            if (roomScopeHolds.Count == 0 && LiveRoomScopeSources == RoomScopeSources.None && hiddenBodyCount == 0
                && mainMaskKept == RoomLayers.NothingKept) return;
            var viewed = ViewedRooms;
            if (viewed.IsWholeHouse || viewed.IsEmpty) viewed = RoomSet.WholeHouse;
            if (viewed.Bits != drawnViewBits) ApplyRoomScopeDrawing();
            else SyncMainCameraMask(viewed);
        }

        /// <summary>Everything the scope hid comes back and every bit it took out goes back: on disable, and when the scope is dropped.</summary>
        private void UndoRoomScopeDrawing()
        {
            PutBackMainCameraMask();
            mainMaskCamera = null;
            foreach (var row in scopedBodies)
            {
                if (row.Body == null) continue;
                foreach (var piece in row.Pieces)
                {
                    if (piece == null) continue;
                    int layer = HouseRoomScope.BodyLayer(piece.layer, true, -1);
                    if (layer != piece.layer) piece.layer = layer;
                }
                if (row.Npc != null) row.Npc.BodyHidden = false;
            }
            scopedBodies.Clear();
            hiddenBodyCount = 0;
            drawnViewBits = -1;
            if (npcInvitationBubble != null) npcInvitationBubble.SpeakerDrawn = true;
            if (npcCaption != null) npcCaption.PairDrawn = true;
        }

        /// <summary>Once a second, with the walker rows: the rows of bodies since destroyed go.</summary>
        private void ForgetMissingScopedBodies()
        {
            for (int i = scopedBodies.Count - 1; i >= 0; i--)
            {
                if (scopedBodies[i].Body != null) continue;
                if (scopedBodies[i].Hidden) hiddenBodyCount = Math.Max(0, hiddenBodyCount - 1);
                scopedBodies.RemoveAt(i);
            }
        }

        /// <summary>Who the HUD's free-time cards tag HERE: kept for the prompt rule, and let go at every render and panel close.</summary>
        private void TagHere(string id)
        {
            if (!string.IsNullOrEmpty(id)) hereTagged.Add(id);
        }
    }
}
