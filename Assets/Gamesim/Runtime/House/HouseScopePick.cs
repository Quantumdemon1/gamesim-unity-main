using System.Collections.Generic;
using UnityEngine;

namespace Gamesim.House
{
    /// <summary>
    /// The room scope's pick filter (ROOM-SCOPE-PLAN §3.2 f, D13): a hit on a collider whose owner the
    /// camera does not draw is passed over, and the ray goes on to the next hit. The click, the hover
    /// and the seated pick ask it, so the player can never select what the view leaves out; floors and
    /// walls are always hittable, so a neighbour's visible floor still walks the player there.
    ///
    /// <para>Keyed on whether the camera draws the hit's owner, and nothing else: an owner is drawn
    /// unless every renderer it has drawing stands on a room layer the camera's culling mask leaves out.
    /// The room scope only ever hides things by moving them to a room layer, so with the whole house
    /// drawn this answers yes for everything and <see cref="Raycast"/> is exactly
    /// <see cref="Physics.Raycast(Ray, out RaycastHit, float, int, QueryTriggerInteraction)"/>.
    /// RS4 hides bodies this way; RS2b's furniture joins through <see cref="OwnerOf"/> (a furniture
    /// proxy's owner is its prop) with no change to the filter itself.</para>
    /// </summary>
    public static class HouseScopePick
    {
        private static readonly RaycastHit[] hits = new RaycastHit[64];
        private static readonly List<Renderer> renderers = new List<Renderer>(32);

        /// <summary>
        /// What a hit belongs to, for drawing: a body's root - a houseguest's or the player's - for any
        /// collider on it; anything else, the collider's own object. A floor carries its own renderer,
        /// and a wall or a proxy has none drawing, so either is always drawn.
        /// </summary>
        public static Transform OwnerOf(Collider hit)
        {
            if (hit == null) return null;
            var npc = hit.GetComponentInParent<HouseNpc>();
            if (npc != null) return npc.transform;
            var player = hit.GetComponentInParent<HousePlayerController>();
            if (player != null) return player.transform;
            return hit.transform;
        }

        /// <summary>
        /// Whether a culling mask draws an owner: yes unless every renderer it has drawing (enabled, on
        /// an active object) stands on a room layer the mask leaves out. An owner with nothing drawing -
        /// a wall whose renderer is off, a collision proxy, a body still being built - counts as drawn.
        /// No allocation.
        /// </summary>
        public static bool Draws(int mask, Transform owner)
        {
            if (owner == null) return true;
            renderers.Clear();
            owner.GetComponentsInChildren(false, renderers);
            bool drawing = false;
            for (int i = 0; i < renderers.Count; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || !renderer.enabled) continue;
                drawing = true;
                int layer = renderer.gameObject.layer;
                if (!RoomLayers.IsRoomLayer(layer) || (mask & (1 << layer)) != 0) { renderers.Clear(); return true; }
            }
            renderers.Clear();
            return !drawing;
        }

        /// <summary>Whether <paramref name="camera"/> draws what <paramref name="hit"/> belongs to. Yes with no camera.</summary>
        public static bool Draws(Camera camera, Collider hit) => camera == null || hit == null || Draws(camera.cullingMask, OwnerOf(hit));

        /// <summary>
        /// The nearest hit along the ray whose owner <paramref name="camera"/> draws, triggers ignored.
        /// Physics.Raycast's own hit whenever its owner is drawn, so with the whole house drawn nothing
        /// changes; otherwise every hit along the ray, nearest first, past the hidden ones. Fails closed
        /// (no hit) if the ray holds more hits than the buffer and its nearest is hidden.
        /// </summary>
        public static bool Raycast(Camera camera, Ray ray, float distance, int layers, out RaycastHit hit)
        {
            if (!Physics.Raycast(ray, out hit, distance, layers, QueryTriggerInteraction.Ignore)) return false;
            if (Draws(camera, hit.collider)) return true;
            int count = Physics.RaycastNonAlloc(ray, hits, distance, layers, QueryTriggerInteraction.Ignore);
            if (count >= hits.Length) { hit = default; return false; }
            // Nearest first: insertion sort, a few hits at most.
            for (int i = 1; i < count; i++)
            {
                var item = hits[i];
                int j = i - 1;
                while (j >= 0 && hits[j].distance > item.distance) { hits[j + 1] = hits[j]; j--; }
                hits[j + 1] = item;
            }
            for (int i = 0; i < count; i++)
            {
                if (!Draws(camera, hits[i].collider)) continue;
                hit = hits[i];
                return true;
            }
            hit = default;
            return false;
        }
    }
}
