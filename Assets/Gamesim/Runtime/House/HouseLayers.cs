using UnityEngine;

namespace Gamesim.House
{
    /// <summary>
    /// Which physics layers each kind of query is allowed to see.
    ///
    /// <para>Until now every query in the game passed <see cref="Physics.DefaultRaycastLayers"/>, so
    /// there was exactly one bit of control in the whole project: a collider was either on layer 2
    /// and invisible to everything, or on layer 0 and visible to everything. That is enough for the
    /// prototype's nav-only stand-ins, which should be seen by nothing but the NavMesh bake. It is
    /// not enough for the furniture a player can see, which has to answer two questions with
    /// different answers: <em>can I click this?</em> yes, and <em>does this block my view?</em> no.
    /// A sofa you cannot click is the bug; a sofa that pulls the camera in and hides a conversation
    /// is also the bug.</para>
    ///
    /// <para>So furniture gets its own layer, <see cref="Furniture"/>, and the queries that are
    /// about LINE OF SIGHT exclude it while the query that is about POINTING AT SOMETHING does not.
    /// The NavMesh bake reads every layer and is unaffected by either.</para>
    /// </summary>
    public static class HouseLayers
    {
        /// <summary>Visible furniture a player can click but cannot hide behind.</summary>
        public const int Furniture = 8;

        private const int FurnitureBit = 1 << Furniture;

        /// <summary>
        /// Pointing: the click that walks the player, picks a houseguest or opens a prop. Sees
        /// furniture, because the whole point is to hit the thing under the cursor.
        /// </summary>
        public static int Pick => Physics.DefaultRaycastLayers;

        /// <summary>
        /// Looking: conversation witnessing, the diary sightline, the camera's occlusion pull-in,
        /// the seated-body pick's occlusion check and the room queries. All of these ask "is
        /// anything solid between these two points", and a coffee table is not an answer to that.
        /// </summary>
        public static int Sight => Physics.DefaultRaycastLayers & ~FurnitureBit;
    }
}
