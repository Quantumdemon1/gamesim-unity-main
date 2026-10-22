using System.Linq;
using UnityEngine;

namespace Gamesim.Editor
{
    /// <summary>
    /// The owner's arrangement of the bedroom, the Head of Household's ensuite and the game room's
    /// study, as the owner left it by hand in the editor on 2026-09-23 and asked for again on
    /// 2026-10-15 ("Yes, re-create it"). It survived only as a crash-recovery scene saved against
    /// acec3bda (the consolidation ledger's decision D8 records it, outside the repository);
    /// it is re-created here, in the placement pass, and never by copying that scene, which
    /// predates three weeks of scene work. The pieces themselves are rows of
    /// <see cref="HouseSetPieces"/>' plan; this is what the pass does around them, before the
    /// collision is fitted and the NavMesh is baked, so the bake sees the rooms as they are played.
    ///
    /// <para><b>How the hand-placed values were read.</b> A yaw within three degrees of a right
    /// angle is taken as that right angle: the rotate gizmo is imprecise and the plan places by
    /// right angles. Any other yaw is the measured one, unless the owner has since squared it (the
    /// suite's sofa). A position is the measured one unless it would stand in a wall, another piece
    /// or a doorway, in which case it is the nearest clear spot. The plan places a piece by the
    /// centre of its bounds; every piece here has its origin under its centre, so the scene's
    /// positions are those centres. The owner's later calls (2026-10-16) are noted where they
    /// apply: the double beds' size and the sofa's angle.</para>
    ///
    /// <para><b>The bedroom.</b> The two double beds - the prototype's bed frames, which stood
    /// side by side in the middle of the room at (-11, 6.5) and (-5, 6.5) - go into the south-west
    /// corner, heads to the west wall (yaw 90.14 and 91.31, taken as 90), with the bedside drawer
    /// that stood at (-12.88, 2.20) moved between them. The owner left them at (-13.10, 0.89) and
    /// (-13.12, 2.62), with the drawer at (-13.70, 1.84), at the 0.94 x 1.18 m the bed frames had
    /// fitted them to. On 2026-10-16 the owner asked for full-size doubles, about 1.4 x 2.0 m, if
    /// they fit. They are 1.20 x 1.50 m, three quarters of the authored 1.6 x 2.0 m bed
    /// (bb_set_doublebed.py) and the largest the corner takes (below): heads on the west wall's
    /// skirting and the south bed on the south wall's, at (-13.105, 0.745) and (-13.105, 2.387), with the drawer a centimetre clear of
    /// each at (-13.70, 1.566). The wardrobe that stood in that corner is gone. The bookcase on the
    /// east partition moves from (-0.98, 8.40) to (-0.48, 7.27) and turns from 270 to 90 (89.48):
    /// its open front faced the partition, and now faces the room.</para>
    ///
    /// <para><b>What sizes the double beds.</b> The corner runs 4.18 m from the south wall to the
    /// lower bunk, and in front of the bunk is the aisle its sleeper lies down from: the approach
    /// of bed-3, at (-12.88, 3.67). The house walks there from the living room's door, past the
    /// north double bed's foot. Measured with the bake on a D: copy on 2026-10-16, the beds packed
    /// into the corner at each size: a bed under the bake's 0.75 m step is a step it walks onto, so
    /// a route does not go round a bed in its way but over it. The walk passed the old beds' foot
    /// 0.30 m off and passes these 0.29 m off, which is where they stop: any larger and it passes
    /// closer than it ever did, it would touch a 1.31 x 1.64 m bed, and at 1.40 x 1.75 m every route
    /// from the living room, the kitchen and the south wing runs 0.15 m over the north bed's corner.
    /// From 1.44 x 1.80 m up the bed stands in the aisle and the bunk's approach leaves the mesh, and
    /// from 1.52 x 1.90 m the bed is too tall to climb at all and the bake's 0.5 m clearance closes
    /// the aisle; the resolver, which backs off whatever box costs an approach, would switch the
    /// bed's collision off to get it back. Full size wants the lower bunk, or the side its sleeper
    /// walks in on, moved: the owner's call.</para>
    ///
    /// <para><b>The ensuite.</b> The toilet and the basin leave the east divider, where both faced
    /// the wall with their backs to the room, for the south wall: the toilet to (-5.96, -18.98), the
    /// basin to (-6.71, -19.15), both turned to 180 (179.79 and -178.31), facing into the suite.
    /// The shower moves from (-5.555, -13.60) to (-5.38, -13.04): the open leaf of the Head of
    /// Household's door swings through where it stood, and now passes it. The sofa on the west
    /// wall turns from 90 to face into the room rather than the wall. The owner left it at -86.80,
    /// 3.2 degrees from a right angle, outside the three the rule allows, so it kept that angle
    /// until the owner asked on 2026-10-16 for it squared: it stands at 270.</para>
    ///
    /// <para><b>The speaker.</b> The owner moved the nomination room's west speaker, which stood at
    /// (-3.87, -14.00) in front of the suite's doorway, into the ensuite, and left it 1.48 m under
    /// the floor at (-5.23, -12.89): that part of the session looked unfinished. It stands on the
    /// floor here. At its measured x and z it would stand inside the moved shower, so it takes the
    /// nearest clear spot, north of the shower's tiled back at (-5.23, -12.43), still facing into
    /// the suite. The nomination room keeps its east speaker.</para>
    ///
    /// <para><b>The study.</b> The game room's desk corner turns to face itself: the desk chair from
    /// 90 to 270 (-92.49, its small tilt dropped), so it faces the desk rather than the room; the
    /// laptop from 270 to 82.29, so its screen faces the chair (eight degrees off square, which is
    /// how it was left); the book stack from 280 to 90 (90.71). The lounge chair by the west
    /// divider turns from 70 to 270 (-87.79), out of the wall's face and into the room. Turned, its
    /// corner still reached 0.105 m into the stride in front of the divider's doorway (it reached
    /// further at 70), so it stands 0.115 m east of its measured (5.705, -13.40), at (5.82, -13.40):
    /// the nearest clear spot, a centimetre clear. The stride is the margin this pass keeps clear
    /// in front of a doorway's gap, not the gap: a bake with the chair on the owner's spot reached
    /// the same room pairs, approaches and bindings and backed off nothing more, so the 0.115 m is
    /// the owner's to take back: its row's -0.40, the pin's 5.705, and a doorway margin in
    /// HouseRoomArrangementTests that stops short of the chair.</para>
    ///
    /// <para><b>The corkboard.</b> The room finish (2026-09-28, after the owner's session) hung its
    /// corkboard and polaroids in the middle of the east partition's north segment, where the owner
    /// stood the bookcase; it now hangs on the segment nothing tall stands in front of, the south
    /// one.</para>
    ///
    /// <para><b>The plants.</b> The three small plants the planting pass grew on every raised bed
    /// stood on the top of the sphere the placeholder had, 1.9 m up and 1.3 m over the bed's box;
    /// the owner deleted all twelve of them. <see cref="HouseSetPieces"/>' planting no longer grows
    /// them, and the raised beds stand as their boxes.</para>
    ///
    /// <para><b>Not re-created.</b> The session also dragged the seven practical lights round the
    /// prototype's television console; the living room's gallery (MOCKUP-PASS-PLAN M21) has since
    /// switched those lights off with the console, so there is nothing left to arrange. The desk's
    /// rotation field was touched but its rotation never changed.</para>
    ///
    /// <para>Nothing here is a piece the game uses: no sleep anchor stands on the double beds (the
    /// beds a houseguest lies on are the singles, the bunks, the Head of Household's bed and the
    /// cots), no routine visits the wardrobe, the plants or the bathroom fixtures, and the seats the
    /// cast sits on are the gallery's, the long table's, the loungers', the hot tub's, the bench's
    /// and the diary chair.</para>
    /// </summary>
    internal static class HouseRoomArrangement
    {
        public const string BedroomFloor = "Bedroom floor";

        /// <summary>
        /// The prototype's pieces the arrangement takes out of the bedroom: the two bed frames with
        /// the models fitted to them, their duvet and pillow boxes, and the wardrobe with its model.
        /// The boxes carry the colliders the bake reads; the models are what is seen.
        /// </summary>
        private static readonly string[] Struck =
        {
            "Bed frame", "Bed frame (model)", "Duvet", "Pillow", "Wardrobe", "Wardrobe (model)",
        };

        /// <summary>
        /// Strikes the prototype's bed frames and wardrobe from the bedroom, with the invisible boxes
        /// they were fitted to: switched off rather than deleted, as the living room's gallery keeps
        /// the pieces it replaces, so the bake sees no collider where they stood. The double beds
        /// themselves are plan rows now. Pieces under the set-piece root or the room finish are
        /// theirs to rebuild and are left alone. Idempotent. Returns how many pieces it struck.
        /// </summary>
        internal static int StrikeTheBedroomPrototype(Transform world, Transform setPieces)
        {
            var floor = world.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == BedroomFloor && t.GetComponent<Renderer>() != null);
            if (floor == null) return 0;
            var bounds = floor.GetComponent<Renderer>().bounds;
            bool InRoom(Vector3 p) => p.x >= bounds.min.x && p.x <= bounds.max.x && p.z >= bounds.min.z && p.z <= bounds.max.z;

            int struck = 0;
            foreach (var node in world.GetComponentsInChildren<Transform>(true))
            {
                if (node == null || !node.gameObject.activeSelf || !Struck.Contains(node.name) || !InRoom(node.position)) continue;
                if (setPieces != null && node.IsChildOf(setPieces)) continue;
                if (node.GetComponentsInParent<Transform>(true).Any(p => p.name == HouseRoomFinish.RootName)) continue;
                node.gameObject.SetActive(false);
                struck++;
            }
            return struck;
        }
    }
}
