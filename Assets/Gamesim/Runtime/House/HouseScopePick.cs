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
    /// unless every renderer it has drawing stands on a room layer the camera's culling mask leaves out
    /// (<see cref="RoomLayers.Draws"/>). The room scope only ever hides things by moving them to a room
    /// layer, so with the whole house drawn this answers yes for everything and <see cref="Raycast"/> is
    /// exactly <see cref="Physics.Raycast(Ray, out RaycastHit, float, int, QueryTriggerInteraction)"/>.
    /// RS4 hides bodies this way; RS2b's furniture joins through <see cref="OwnerOf"/> (a furniture
    /// proxy's owner is its prop) with no change to the filter itself.</para>
    /// </summary>
    public static class HouseScopePick
    {
        private static readonly RaycastHit[] hits = new RaycastHit[64];
        private static readonly List<Renderer> renderers = new List<Renderer>(32);

        /// <summary>
        /// What a hit belongs to, for drawing: a body's root - a houseguest's or the player's - for any
        /// collider on it; a piece of furniture's collision proxy, the prop it hangs under (RS2b,
        /// <see cref="IsFurnitureProxy"/>); anything else, the collider's own object. The proxy draws
        /// nothing itself - its prop's renderers stand on the prop's room layer (RS2a) - so a proxy read
        /// as its own owner would always count as drawn, and hidden furniture would take the click. A
        /// floor carries its own renderer, and a wall has none drawing (its renderer is off), so either is
        /// always drawn: a neighbour's visible floor still walks the player there.
        /// </summary>
        public static Transform OwnerOf(Collider hit)
        {
            if (hit == null) return null;
            var npc = hit.GetComponentInParent<HouseNpc>();
            if (npc != null) return npc.transform;
            var player = hit.GetComponentInParent<HousePlayerController>();
            if (player != null) return player.transform;
            var prop = hit.transform.parent;
            if (prop != null && IsFurnitureProxy(hit.gameObject.layer)) return prop;
            return hit.transform;
        }

        /// <summary>
        /// Whether a collider on this layer, on no body, is a piece of furniture's collision proxy: one on
        /// <see cref="HouseLayers.Furniture"/>. The house keeps nothing else there - the fitted
        /// <c>Collision</c> boxes, children of the props they fit (HouseFurnitureCollision), and an
        /// authored model's <c>_col</c> shapes, children of the model's root (AuthoredAssetImporter) - but
        /// a parked body's root, which <see cref="OwnerOf"/> reads as the body first. Floors, walls, fences
        /// and the prototype's planters carry their colliders on layer 0, so they keep their own.
        /// </summary>
        public static bool IsFurnitureProxy(int layer) => layer == HouseLayers.Furniture;

        /// <summary>
        /// Whether a culling mask draws an owner: yes unless every renderer it has drawing (enabled, on
        /// an active object) stands on a room layer the mask leaves out. An owner with nothing drawing -
        /// a wall whose renderer is off, a body still being built, a prop whose renderers are all off -
        /// counts as drawn. No allocation.
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
                if (RoomLayers.Draws(mask, renderer.gameObject.layer)) { renderers.Clear(); return true; }
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
