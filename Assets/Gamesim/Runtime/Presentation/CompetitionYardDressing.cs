using System.Collections.Generic;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The yard changed for a competition (competition show, slice 1): while the arena stands - the
    /// player's own game or a show - the everyday pieces standing in the field are put away, and
    /// the competition's equipment stands there instead. Afterwards every one is back exactly as it
    /// was, whatever ended the arena: the show's end, a skip, a load, a new commit, the director
    /// switched off.
    ///
    /// <para>Put away means drawn no longer: each piece's renderers are switched off, and any
    /// collider it carries with them so nothing invisible stands in the field. Nothing is moved,
    /// destroyed or saved, and only what this switched off is switched back on. The pieces are
    /// found by their model's name under the deck's own house, and only where their middle stands
    /// inside the field (the deck less its edge band, where the pool, the hot tub, the plants and the
    /// entrance stand). The pool and the hot tub are never put away, nor the loungers the audience
    /// sits on, nor the podiums, gates, backdrop and signs, which are the competition's own.</para>
    ///
    /// <para>This runs before the arena reads its scenery (<see cref="CompetitionStageScenery.Collect"/>),
    /// which counts only enabled renderers, so the stations may stand where the pieces stood. A piece
    /// put away keeps its baked NavMesh hole; the bodies route around a patch of empty floor.</para>
    /// </summary>
    public sealed class CompetitionYardDressing
    {
        /// <summary>The everyday pieces that are put away when they stand in the field: the course's stacks and crates, plants, the pool table.</summary>
        public static readonly string[] Struck =
        {
            "bb_set_comp_stack", "bb_set_comp_crate", "bb_set_ph_plant", "bb_set_ph_plantsmall",
            "bb_set_plant", "bb_set_plantsmall", "bb_set_pooltable", "bb_set_flight_case",
        };

        /// <summary>The deck's edge band, in metres, where nothing is put away.</summary>
        public const float EdgeBand = 1.2f;

        private readonly List<Renderer> renderers = new List<Renderer>();
        private readonly List<Collider> colliders = new List<Collider>();

        /// <summary>The renderers this switched off and will switch back on.</summary>
        public IReadOnlyList<Renderer> Hidden => renderers;

        /// <summary>The colliders this switched off and will switch back on.</summary>
        public IReadOnlyList<Collider> Disabled => colliders;

        /// <summary>Whether the pieces are put away now.</summary>
        public bool PutAway => renderers.Count > 0 || colliders.Count > 0;

        /// <summary>Puts away every listed piece standing in <paramref name="deck"/>'s field, except anything under <paramref name="own"/>.</summary>
        public static CompetitionYardDressing Strike(Collider deck, Transform own)
        {
            var dressing = new CompetitionYardDressing();
            if (deck == null) return dressing;
            var field = deck.bounds;
            var pieces = new HashSet<Transform>();
            foreach (var node in deck.transform.root.GetComponentsInChildren<Transform>())
            {
                if (own != null && node.IsChildOf(own)) continue;
                if (!IsStruck(node.name)) continue;
                // The outermost of a model's own nodes only: an imported piece repeats its name on its mesh child.
                bool nested = false;
                for (var at = node.parent; at != null; at = at.parent) if (IsStruck(at.name)) { nested = true; break; }
                if (nested) continue;
                var middle = Middle(node);
                if (middle.x < field.min.x + EdgeBand || middle.x > field.max.x - EdgeBand
                    || middle.z < field.min.z + EdgeBand || middle.z > field.max.z - EdgeBand) continue;
                if (middle.y < field.min.y - 1f || middle.y > field.max.y + 3f) continue;
                pieces.Add(node);
            }
            foreach (var piece in pieces)
            {
                foreach (var renderer in piece.GetComponentsInChildren<Renderer>())
                    if (renderer.enabled) { renderer.enabled = false; dressing.renderers.Add(renderer); }
                foreach (var collider in piece.GetComponentsInChildren<Collider>())
                    if (collider.enabled && collider != deck) { collider.enabled = false; dressing.colliders.Add(collider); }
            }
            if (dressing.colliders.Count > 0) Physics.SyncTransforms();
            return dressing;
        }

        /// <summary>Whether a node's name is one of the struck models', allowing the editor's " (2)" suffix.</summary>
        public static bool IsStruck(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            int cut = name.IndexOf(" (", System.StringComparison.Ordinal);
            string stem = cut > 0 ? name.Substring(0, cut) : name;
            foreach (var model in Struck) if (stem == model) return true;
            return false;
        }

        private static Vector3 Middle(Transform piece)
        {
            bool any = false; var box = new Bounds(piece.position, Vector3.zero);
            foreach (var renderer in piece.GetComponentsInChildren<Renderer>())
            {
                if (!any) { box = renderer.bounds; any = true; } else box.Encapsulate(renderer.bounds);
            }
            return any ? box.center : piece.position;
        }

        /// <summary>Puts every piece back exactly as it was: only what this switched off is switched on.</summary>
        public void Restore()
        {
            foreach (var renderer in renderers) if (renderer != null) renderer.enabled = true;
            foreach (var collider in colliders) if (collider != null) collider.enabled = true;
            bool synced = colliders.Count > 0;
            renderers.Clear(); colliders.Clear();
            if (synced) Physics.SyncTransforms();
        }
    }
}
