using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gamesim.House
{
    /// <summary>
    /// The seats a ceremony gathers the house to (CEREMONY-CUTSCENES-PLAN §2-3): a chair for
    /// everybody who draws a key round the nomination table, the two hot seats and the sofa's seats
    /// in the living room, the marks behind them, and the living room's own screen.
    ///
    /// <para>Authored at runtime from the set the scene carries, the way the arena's stations and
    /// the opening's facade are: the scene is never re-saved for them, a roster of any size gets
    /// the seats it needs, and a house dressed differently simply has fewer. Each seat is a
    /// <see cref="HouseInteractionAnchor"/> on its chair, so the meeting coordinator walks a body
    /// to its approach and <see cref="HouseSeatPresentation"/> sits it, exactly as the dining chairs
    /// seat a conversation today.</para>
    /// </summary>
    public static class CeremonySeating
    {
        /// <summary>A chair at the nomination table, slot i in cast order; seated.</summary>
        public const string NominationSeat = "nomination-seat";
        /// <summary>Where the Head of Household (slot 0) and the veto holder (slot 1) stand at the head of the table, in front of the screen.</summary>
        public const string NominationHead = "nomination-head";
        /// <summary>The nominees' two chairs on eviction night, facing the living room's screen; seated.</summary>
        public const string HotSeat = "hot-seat";
        /// <summary>The living room sofa's seats, where the room has no gallery; seated.</summary>
        public const string SofaSeat = "sofa-seat";
        /// <summary>
        /// The living room gallery's couch seats (MOCKUP-PASS-PLAN M22): the U's base from its middle
        /// outward, then its arms from the base's end; seated. Everyone at an eviction but the two
        /// nominees sits here, the Head of Household included.
        /// </summary>
        public const string GallerySeat = "gallery-seat";
        /// <summary>Standing marks in the living room: slot 0 beside the screen for the Head of Household, the rest behind the sofa.</summary>
        public const string LivingMark = "living-mark";

        /// <summary>The anchors of a venue, by slot.</summary>
        public static List<HouseInteractionAnchor> Anchors(Scene scene, string venue) =>
            HouseInteractionAnchors.InScene(scene).Where(a => a.VenueId == venue && a.isActiveAndEnabled).OrderBy(a => a.Slot).ToList();

        /// <summary>
        /// Dresses the sets for a house of <paramref name="houseguests"/>: enough chairs round the
        /// table, the hot seats and the sofa's seats with their anchors, and the living room's
        /// screen. Idempotent: a house already dressed for this size is left alone.
        /// </summary>
        public static void Ensure(Scene scene, int houseguests)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            CeremonySets.Ensure(scene, houseguests);
        }
    }
}
