using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// Whether a ceremony card is on the screen right now.
    ///
    /// <para>The nomination and eviction cards deliberately take no input: no
    /// <c>GraphicRaycaster</c>, every graphic non-raycasting, and a dismissal read straight off the
    /// mouse device. That rule is load-bearing - it is why nothing can be stranded behind a card,
    /// neither a player mid-walk nor an automated season driving fifty-six decisions in under a
    /// minute - and it is not going to change.</para>
    ///
    /// <para>The cost of it is that the click which dismisses a card is still an unclaimed click as
    /// far as the rest of the game is concerned, and the house is directly underneath. Clicking a
    /// near-opaque card you cannot see through dismissed it <em>and</em> walked the player to
    /// whatever floor happened to be behind it.</para>
    ///
    /// <para>So the card does not block the click; it says it was there. A frame stamp rather than
    /// a counter, because a counter has to be balanced on every exit path - including a card
    /// destroyed mid-play - and a stamp simply goes stale. The previous frame counts too, since
    /// script execution order between the card and the player is not defined and a card on screen
    /// this frame was on screen last frame as well.</para>
    /// </summary>
    public static class CeremonyOverlays
    {
        // Well before frame zero, but nowhere near int.MinValue: the comparison below is a
        // subtraction, and int.MinValue makes it overflow to a negative number on the very first
        // frame, which reads as "a card is on screen" and silently swallows every click in the game.
        private static int lastFrame = -1000;

        /// <summary>Called by a ceremony card on each frame it is playing.</summary>
        public static void Showing() => lastFrame = Time.frameCount;

        /// <summary>Whether the world should treat this frame's click as already spoken for.</summary>
        public static bool OnScreen => Time.frameCount - lastFrame <= 1;
    }
}
