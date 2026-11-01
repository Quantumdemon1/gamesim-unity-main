using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gamesim.House
{
    /// <summary>
    /// The dressing (ROOM-SCOPE-PLAN §3.2 b, RS2a): the classification applied. Every collider-free
    /// piece of the house the classifier puts in a room stands on that room's layer
    /// (<see cref="RoomLayers"/>), so a camera leaves a room's furniture out of its drawing with one bit
    /// of its culling mask; everything else - the shell, anything on another layer, the bodies (RS4's),
    /// anything not yet drawn - stays where it is and is drawn always.
    ///
    /// <para><b>When.</b> Once at load, over the whole house (<see cref="PlaceHouse"/>: the director, as
    /// it builds its scope), and again at every point that builds or moves dressing, on what it built or
    /// moved, in the same call (<see cref="PlaceDressing"/>): the ceremony sets, the competition's stage,
    /// the diary's backdrop, the public displays, the memory wall's layout and the house's activity
    /// effects. A copy of another room's prop - the living room's screen and hot seats are copies of the
    /// nomination room's - is placed by where it stands, never by the layer it was copied with.</para>
    ///
    /// <para><b>Not drawn yet.</b> A piece on an inactive object, or with no bounds yet (Unity may give a
    /// renderer on an inactive object empty bounds), waits, on the shell's layer, and is placed the first
    /// time it can be read: the director's late pass looks every frame (<see cref="KeepDressingPlaced"/>),
    /// after Update and before the main camera renders, so it is placed in the frame it is first drawn.</para>
    ///
    /// <para><b>A piece that moves goes to the shell</b>, and stays there: a placed piece whose transform
    /// changes after it took its room - an animated part, anything moved by code that did not place it
    /// again - is back on layer 0 in that frame's late pass, so nothing is ever hidden in a room it has
    /// moved into. A staging point that moves a piece places it again itself, in the same call. Read
    /// through <see cref="Transform.hasChanged"/>, which nothing else in the game uses: the dressing
    /// clears it as it places a piece.</para>
    ///
    /// <para>Every layer write comes in Update or LateUpdate, ahead of the frame's main-camera render and
    /// the Resident Drawer's once-a-frame read (§2.8). No collider's object is ever put on a room layer:
    /// a piece with a collider is the shell's, and one placed before it had one goes back to layer 0 the
    /// next time it is placed. The cameras' masks are not written here: RS4's are the only mask writes.</para>
    /// </summary>
    public sealed partial class HouseRoomScope
    {
        /// <summary>The pieces the dressing has on a room layer, and their transforms beside them: watched every frame for a move.</summary>
        private readonly List<Renderer> placed = new List<Renderer>(640);
        private readonly List<Transform> placedTransforms = new List<Transform>(640);
        private readonly HashSet<Renderer> placedSet = new HashSet<Renderer>();
        /// <summary>The pieces not drawn yet: placed the first frame they can be read.</summary>
        private readonly List<Renderer> waiting = new List<Renderer>(128);
        private readonly HashSet<Renderer> waitingSet = new HashSet<Renderer>();
        /// <summary>The placed pieces that moved and went to the shell, for tests and diagnostics; destroyed ones are let go.</summary>
        private readonly HashSet<Renderer> movedOut = new HashSet<Renderer>();
        private readonly List<Renderer> placeBuffer = new List<Renderer>(64);
        private static readonly List<Transform> carryBuffer = new List<Transform>(16);

        /// <summary>How many pieces of the house the dressing has on a room layer.</summary>
        public int PlacedCount => placed.Count;

        /// <summary>How many pieces wait to be placed the first frame they are drawn.</summary>
        public int WaitingCount => waiting.Count;

        /// <summary>Whether the dressing has <paramref name="renderer"/> on its room's layer.</summary>
        public bool IsPlaced(Renderer renderer) => renderer != null && placedSet.Contains(renderer);

        /// <summary>Whether <paramref name="renderer"/> waits to be placed the first frame it is drawn.</summary>
        public bool IsWaiting(Renderer renderer) => renderer != null && waitingSet.Contains(renderer);

        /// <summary>Whether a placed piece moved and went to the shell, and no staging point has placed it since.</summary>
        public bool WentToTheShell(Renderer renderer) => renderer != null && movedOut.Contains(renderer);

        // ------------------------------------------------------------------------------------
        // The rule.
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// The layer a piece of the house goes to (§3.2 b), from its layer and its classification: its
        /// room's layer for a piece in a room; layer 0, the shell's, for a piece on a room layer that is no
        /// longer in one - it crosses a wall, stands outside, is on the shell list, has a collider; and the
        /// layer it has for anything else: a piece already on the shell, a piece on a layer of its own
        /// (parking's 8, the tests' 30 and 31), a renderer of another kind, and a body's, which RS4 layers
        /// by where it walks (<see cref="BodyLayer"/>).
        /// </summary>
        public static int DressingLayer(int current, Placement placement, int room)
        {
            if (placement == Placement.Body || placement == Placement.Kind || placement == Placement.Layer) return current;
            if (placement == Placement.Room && room >= 0 && room < RoomLayers.Count) return RoomLayers.First + room;
            return RoomLayers.IsRoomLayer(current) ? 0 : current;
        }

        /// <summary>
        /// Whether a renderer can be placed now: on an object active in the hierarchy and, but for a
        /// particle renderer (read by its emitter), with bounds of some size.
        /// </summary>
        public static bool CanPlace(Renderer renderer)
        {
            if (renderer == null || !renderer.gameObject.activeInHierarchy) return false;
            if (renderer is ParticleSystemRenderer) return true;
            var size = renderer.bounds.size;
            return size.x > 0f || size.y > 0f || size.z > 0f;
        }

        // ------------------------------------------------------------------------------------
        // Placing.
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// The load's pass: every renderer in the scope's house, active or not, placed by where it stands
        /// - its room's layer for a piece the classifier puts in a room, the shell's otherwise - and the
        /// pieces not drawn yet left waiting. Returns how many stand on a room layer. A scope built from
        /// rectangles has no house to place.
        /// </summary>
        public int PlaceHouse()
        {
            if (query == null) return 0;
            rootBuffer.Clear();
            query.Scene.GetRootGameObjects(rootBuffer);
            foreach (var root in rootBuffer)
            {
                rendererBuffer.Clear();
                root.GetComponentsInChildren(true, rendererBuffer);
                for (int i = 0; i < rendererBuffer.Count; i++) PlaceOne(rendererBuffer[i]);
            }
            rendererBuffer.Clear();
            rootBuffer.Clear();
            return placed.Count;
        }

        /// <summary>
        /// A staging point's call: every renderer under <paramref name="piece"/>, its own included, active
        /// or not, placed by where it stands now. Returns how many of them stand on a room layer after it.
        /// </summary>
        public int Place(Transform piece)
        {
            if (piece == null) return 0;
            placeBuffer.Clear();
            piece.GetComponentsInChildren(true, placeBuffer);
            int inRooms = 0;
            for (int i = 0; i < placeBuffer.Count; i++)
            {
                var renderer = placeBuffer[i];
                PlaceOne(renderer);
                if (renderer != null && RoomLayers.IsRoomLayer(renderer.gameObject.layer) && placedSet.Contains(renderer)) inRooms++;
            }
            placeBuffer.Clear();
            return inRooms;
        }

        /// <summary>One renderer placed by where it stands now: on its room's layer, back on layer 0, waiting, or left alone.</summary>
        private void PlaceOne(Renderer renderer)
        {
            if (renderer == null) return;
            if (!(renderer is MeshRenderer || renderer is SkinnedMeshRenderer || renderer is ParticleSystemRenderer)) return;
            var piece = renderer.gameObject;
            int layer = piece.layer;
            // A piece on a layer of its own, or a body's (RS4 draws bodies by where they walk): never the dressing's.
            if ((layer != 0 && !RoomLayers.IsRoomLayer(layer)) || IsBody(renderer.transform)) { Forget(renderer); return; }
            if (!CanPlace(renderer))
            {
                // Not drawn yet: the shell's until it can be read.
                ForgetPlaced(renderer);
                if (RoomLayers.IsRoomLayer(layer)) piece.layer = 0;
                if (waitingSet.Add(renderer)) waiting.Add(renderer);
                return;
            }
            if (waitingSet.Remove(renderer)) waiting.Remove(renderer);
            var placement = Classify(renderer, out int room);
            int next = DressingLayer(layer, placement, room);
            if (next != layer) piece.layer = next;
            if (!RoomLayers.IsRoomLayer(next)) { ForgetPlaced(renderer); return; }
            var t = renderer.transform;
            if (placedSet.Add(renderer)) { placed.Add(renderer); placedTransforms.Add(t); }
            movedOut.Remove(renderer);
            t.hasChanged = false;
        }

        /// <summary>
        /// Every frame, in the director's late pass, after Update and before the main camera renders: a
        /// placed piece that has moved goes to the shell, and a waiting piece that can be read now is
        /// placed. One flag read a placed piece, its transform kept beside it; one activity read a
        /// waiting one; no allocation.
        /// </summary>
        public void KeepDressingPlaced()
        {
            for (int i = placed.Count - 1; i >= 0; i--)
            {
                var t = placedTransforms[i];
                if (t == null) { RemovePlacedAt(i); continue; }
                if (!t.hasChanged) continue;
                t.hasChanged = false;
                var renderer = placed[i];
                RemovePlacedAt(i);
                if (renderer == null) continue;
                // Carried onto a body: the body's layers are RS4's from now on.
                if (IsBody(t)) continue;
                var piece = renderer.gameObject;
                if (RoomLayers.IsRoomLayer(piece.layer)) piece.layer = 0;
                if (movedOut.Count >= 256) movedOut.RemoveWhere(gone => gone == null);
                movedOut.Add(renderer);
            }
            for (int i = waiting.Count - 1; i >= 0; i--)
            {
                var renderer = waiting[i];
                if (renderer == null) { RemoveWaitingAt(i); continue; }
                if (!CanPlace(renderer)) continue;
                RemoveWaitingAt(i);
                PlaceOne(renderer);
            }
        }

        /// <summary>Every piece the dressing placed back on layer 0, and nothing waiting: when the director lets its scope go.</summary>
        public void UndoDressing()
        {
            for (int i = 0; i < placed.Count; i++)
            {
                var renderer = placed[i];
                if (renderer == null) continue;
                var piece = renderer.gameObject;
                if (RoomLayers.IsRoomLayer(piece.layer) && !IsBody(renderer.transform)) piece.layer = 0;
            }
            placed.Clear();
            placedTransforms.Clear();
            placedSet.Clear();
            waiting.Clear();
            waitingSet.Clear();
            movedOut.Clear();
        }

        private void Forget(Renderer renderer)
        {
            ForgetPlaced(renderer);
            if (waitingSet.Remove(renderer)) waiting.Remove(renderer);
        }

        private void ForgetPlaced(Renderer renderer)
        {
            if (!placedSet.Remove(renderer)) return;
            int index = placed.IndexOf(renderer);
            if (index >= 0) RemovePlacedAt(index);
        }

        private void RemovePlacedAt(int index)
        {
            var renderer = placed[index];
            int last = placed.Count - 1;
            placed[index] = placed[last];
            placedTransforms[index] = placedTransforms[last];
            placed.RemoveAt(last);
            placedTransforms.RemoveAt(last);
            placedSet.Remove(renderer);
        }

        private void RemoveWaitingAt(int index)
        {
            var renderer = waiting[index];
            int last = waiting.Count - 1;
            waiting[index] = waiting[last];
            waiting.RemoveAt(last);
            waitingSet.Remove(renderer);
        }

        /// <summary>
        /// Gives what hangs on a piece - a world-space canvas mounted on a display's face - the piece's
        /// layer, so it is drawn exactly where the piece is: every object under
        /// <paramref name="attachment"/> on layer 0 or a room layer takes <paramref name="host"/>'s, when
        /// that is layer 0 or a room layer. Anything on a layer of its own is left alone.
        /// </summary>
        public static void CarryLayer(GameObject host, Transform attachment)
        {
            if (host == null || attachment == null) return;
            int layer = host.layer;
            if (layer != 0 && !RoomLayers.IsRoomLayer(layer)) return;
            carryBuffer.Clear();
            attachment.GetComponentsInChildren(true, carryBuffer);
            for (int i = 0; i < carryBuffer.Count; i++)
            {
                var item = carryBuffer[i].gameObject;
                int own = item.layer;
                if (own != layer && (own == 0 || RoomLayers.IsRoomLayer(own))) item.layer = layer;
            }
            carryBuffer.Clear();
        }

        // ------------------------------------------------------------------------------------
        // Which scope places a scene's dressing.
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// The scopes placing their houses' dressing: each director's, while it is enabled. Static, so a
        /// staging point anywhere - a static set dresser, a component on a body - reaches its house's
        /// scope through the scene; let go at <see cref="RuntimeInitializeLoadType.SubsystemRegistration"/>,
        /// since the editor starts play without a domain reload and a scope from the last session would
        /// otherwise stay listed.
        /// </summary>
        private static readonly List<HouseRoomScope> placing = new List<HouseRoomScope>(2);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ForgetPlacing() => placing.Clear();

        /// <summary>The director's, when it is enabled with a scope or builds one: this scope places what its house's staging points build or move.</summary>
        public static void BeginPlacing(HouseRoomScope scope)
        {
            if (scope == null || scope.query == null || placing.Contains(scope)) return;
            placing.Add(scope);
        }

        /// <summary>The director's, when it is disabled or lets its scope go: its house's staging points place nothing until it begins again.</summary>
        public static void EndPlacing(HouseRoomScope scope) => placing.Remove(scope);

        /// <summary>The scope that places a scene's dressing, or null: no director there, or none enabled (a preview scene, the prototype).</summary>
        public static HouseRoomScope PlacingFor(Scene scene)
        {
            for (int i = placing.Count - 1; i >= 0; i--)
            {
                var own = placing[i].query.Scene;
                if (!own.IsValid() || !own.isLoaded) { placing.RemoveAt(i); continue; }
                if (own == scene) return placing[i];
            }
            return null;
        }

        /// <summary>
        /// A staging point's one call, made in the frame it builds or moves dressing: every renderer under
        /// <paramref name="piece"/> placed by where it stands, by the scope of the piece's scene. Nothing
        /// where no director places the scene's dressing. Returns how many stand on a room layer after it.
        /// </summary>
        public static int PlaceDressing(Transform piece)
        {
            if (piece == null) return 0;
            var scope = PlacingFor(piece.gameObject.scene);
            return scope != null ? scope.Place(piece) : 0;
        }
    }
}
