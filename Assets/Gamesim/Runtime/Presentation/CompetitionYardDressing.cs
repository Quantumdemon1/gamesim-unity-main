using System.Collections.Generic;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The one owner of what stands in the yard when (competition show, slice 1; competition yard,
    /// slice 2). The yard is in one of two states:
    ///
    /// <para><b>Everyday</b> - free time, the ceremonies, every moment no competition is played or
    /// staged: the competition's equipment is put away (<see cref="CompetitionYardPieces"/>: the stacks
    /// and their crates, the lanes and their markings, the gates, the lettering, the podiums), and the
    /// television set's dressing stays. Put away means the piece's object is switched off, its renderers
    /// and colliders with it, so nothing invisible stands in anyone's way.</para>
    ///
    /// <para><b>A competition</b> of a given kind - the player's own game or practice, or a staged show:
    /// the course is deployed as its set, and then the pieces that would stand inside the station field
    /// are put away again (slice 1's rule: the course's stacks and crates, plants, the pool table, a
    /// flight case in the field), so the stations stand where they always have. <see cref="Restore"/>
    /// returns to everyday, whatever ended the competition.</para>
    ///
    /// <para>Nothing is moved, destroyed or saved, and only what this switched off is switched back on.
    /// The pieces are found at runtime by the names their authoring gives them, under the deck's own
    /// house, so neither the scene nor an editor pass changes. A piece put away keeps the hole its
    /// collider left in the baked NavMesh: nobody is routed onto that floor, which stays dead until a
    /// later bake gives it back.</para>
    /// </summary>
    public sealed class CompetitionYardDressing
    {
        /// <summary>The yard's two states.</summary>
        public enum YardState
        {
            /// <summary>The competition equipment put away; the television set standing.</summary>
            Everyday,
            /// <summary>The course deployed as a competition's set, its pieces in the station field put away.</summary>
            Competition,
        }

        /// <summary>The everyday pieces that are put away when they stand in the field: the course's stacks and crates, plants, the pool table.</summary>
        public static readonly string[] Struck =
        {
            "bb_set_comp_stack", "bb_set_comp_crate", "bb_set_ph_plant", "bb_set_ph_plantsmall",
            "bb_set_plant", "bb_set_plantsmall", "bb_set_pooltable", "bb_set_flight_case",
        };

        /// <summary>The deck's edge band, in metres, where nothing is put away.</summary>
        public const float EdgeBand = 1.2f;

        /// <summary>How far past the deck's edge an everyday piece may stand and still be the yard's (the backdrop and its signs stand on the back edge).</summary>
        public const float YardMargin = 1f;

        /// <summary>The house's root, the one the authoring passes place every yard piece under.</summary>
        public const string HouseRoot = "House Architecture";

        private readonly List<Renderer> renderers = new List<Renderer>();
        private readonly List<Collider> colliders = new List<Collider>();
        private readonly List<GameObject> stowed = new List<GameObject>();

        /// <summary>The state the yard is in.</summary>
        public YardState State { get; private set; } = YardState.Everyday;

        /// <summary>The kind of competition the course is deployed for; null in everyday life.</summary>
        public string Kind { get; private set; }

        /// <summary>The renderers in the station field this switched off for a competition, and will switch back on.</summary>
        public IReadOnlyList<Renderer> Hidden => renderers;

        /// <summary>The colliders in the station field this switched off for a competition, and will switch back on.</summary>
        public IReadOnlyList<Collider> Disabled => colliders;

        /// <summary>The competition equipment this put away for everyday life, and will deploy for a competition.</summary>
        public IReadOnlyList<GameObject> Stowed => stowed;

        /// <summary>Whether the field's pieces are put away for a competition now.</summary>
        public bool PutAway => renderers.Count > 0 || colliders.Count > 0;

        /// <summary>
        /// Everyday life: the competition equipment standing in <paramref name="deck"/>'s yard is put
        /// away. Anything already put away stays so; a competition's set is taken down first.
        /// </summary>
        public void Everyday(Collider deck)
        {
            if (State == YardState.Competition) Restore();
            if (deck == null) return;
            // Settled already on this deck: a projection after every commit asks again, and the walk is the house's whole tree.
            if (everydayDeck == deck) return;
            foreach (var piece in Pieces(deck))
                if (piece.activeSelf) { piece.SetActive(false); stowed.Add(piece); }
            State = YardState.Everyday; Kind = null; everydayDeck = deck;
        }

        private Collider everydayDeck;

        /// <summary>
        /// A competition of <paramref name="kind"/>: the equipment put away is deployed as its set,
        /// then the pieces in the station field are put away (slice 1's rule), except anything under
        /// <paramref name="own"/>, the arena's own stage.
        /// </summary>
        public void Deploy(Collider deck, Transform own, string kind)
        {
            if (State == YardState.Competition) StrikeBack();
            foreach (var piece in stowed) if (piece != null) piece.SetActive(true);
            stowed.Clear(); everydayDeck = null;
            State = YardState.Competition; Kind = kind;
            if (deck == null) return;
            Strike(deck, own);
        }

        /// <summary>Back to everyday life, whatever ended the competition: the field's pieces back on, the equipment put away.</summary>
        public void Restore()
        {
            var deck = lastDeck;
            StrikeBack();
            State = YardState.Everyday; Kind = null;
            if (deck != null) Everyday(deck);
        }

        private Collider lastDeck;

        /// <summary>Puts away every listed piece standing in <paramref name="deck"/>'s field, except anything under <paramref name="own"/>.</summary>
        private void Strike(Collider deck, Transform own)
        {
            lastDeck = deck;
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
                    if (renderer.enabled) { renderer.enabled = false; renderers.Add(renderer); }
                foreach (var collider in piece.GetComponentsInChildren<Collider>())
                    if (collider.enabled && collider != deck) { collider.enabled = false; colliders.Add(collider); }
            }
            if (colliders.Count > 0) Physics.SyncTransforms();
        }

        /// <summary>The field's pieces back on: only what <see cref="Strike"/> switched off.</summary>
        private void StrikeBack()
        {
            foreach (var renderer in renderers) if (renderer != null) renderer.enabled = true;
            foreach (var collider in colliders) if (collider != null) collider.enabled = true;
            bool synced = colliders.Count > 0;
            renderers.Clear(); colliders.Clear();
            if (synced) Physics.SyncTransforms();
        }

        /// <summary>
        /// The yard's pieces of <see cref="CompetitionYardPieces"/> put away in everyday life, by their
        /// outermost node, under the deck's house and standing in its yard (the deck and a metre round it).
        /// Inactive nodes are read as well, so a piece this put away is found again.
        /// </summary>
        private IEnumerable<GameObject> Pieces(Collider deck)
        {
            lastDeck = deck;
            var root = deck.transform.root;
            if (root.name != HouseRoot) yield break;
            var yard = deck.bounds;
            foreach (var node in root.GetComponentsInChildren<Transform>(true))
            {
                if (node == deck.transform || !CompetitionYardPieces.PutAwayInEveryday(node.name)) continue;
                bool nested = false;
                for (var at = node.parent; at != null; at = at.parent)
                    if (CompetitionYardPieces.Find(at.name) != null) { nested = true; break; }
                if (nested) continue;
                var at0 = node.position;
                if (at0.x < yard.min.x - YardMargin || at0.x > yard.max.x + YardMargin
                    || at0.z < yard.min.z - YardMargin || at0.z > yard.max.z + YardMargin) continue;
                yield return node.gameObject;
            }
        }

        /// <summary>
        /// The ring tower's set (competition ring tower, slice 4): every piece of <paramref name="model"/> struck from
        /// the field stands again - drawn, its collider back - because the competition is played on it. Returns how
        /// many renderers and colliders came back. Only what <see cref="Strike"/> switched off is touched.
        /// </summary>
        public int KeepStanding(string model)
        {
            int back = 0;
            for (int i = renderers.Count - 1; i >= 0; i--)
                if (renderers[i] != null && IsPiece(renderers[i].transform, model)) { renderers[i].enabled = true; renderers.RemoveAt(i); back++; }
            bool synced = false;
            for (int i = colliders.Count - 1; i >= 0; i--)
                if (colliders[i] != null && IsPiece(colliders[i].transform, model)) { colliders[i].enabled = true; colliders.RemoveAt(i); back++; synced = true; }
            if (synced) Physics.SyncTransforms();
            return back;
        }

        /// <summary>
        /// Switches one renderer of the deployed set off for this competition and remembers it, so the yard's
        /// restore switches it back on whatever ends the competition (the ring tower draws its stacks again
        /// from their own profile). Nothing while the yard is in everyday life.
        /// </summary>
        public void Conceal(Renderer renderer)
        {
            if (renderer == null || !renderer.enabled || State != YardState.Competition) return;
            renderer.enabled = false; renderers.Add(renderer);
        }

        private static bool IsPiece(Transform at, string model)
        {
            for (; at != null; at = at.parent) if (CompetitionYardPieces.Stem(at.name) == model) return true;
            return false;
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
    }
}
