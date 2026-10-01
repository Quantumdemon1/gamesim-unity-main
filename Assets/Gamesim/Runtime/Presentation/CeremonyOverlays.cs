using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// Whether a ceremony card is on the screen right now.
    ///
    /// <para>The nomination and eviction cards deliberately take no input through the event system:
    /// no <c>GraphicRaycaster</c>, every graphic non-raycasting, and their skip, speed-up and
    /// dismissal read straight off the mouse, keyboard and pad devices. That rule is load-bearing -
    /// it is why nothing can be stranded behind a card,
    /// neither a player mid-walk nor an automated season driving fifty-six decisions in under a
    /// minute - and it is not going to change.</para>
    ///
    /// <para>The cost of it is that the press which moves a card on is still an unclaimed press as
    /// far as the rest of the game is concerned, and the house is directly underneath: the house's
    /// click guard, its shortcuts and the UI's Submit and Cancel all ask this before acting. Clicking a
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
        /// <summary>A stamp from before any frame: nothing has been shown.</summary>
        private const int Never = -1000;

        // Well before frame zero, but nowhere near int.MinValue: the comparison below is a
        // subtraction, and int.MinValue makes it overflow to a negative number on the very first
        // frame, which reads as "a card is on screen" and silently swallows every click in the game.
        private static int lastFrame = Never;

        /// <summary>Called by a ceremony card on each frame it is playing.</summary>
        public static void Showing() => lastFrame = Time.frameCount;

        /// <summary>Whether the world should treat this frame's click as already spoken for.</summary>
        public static bool OnScreen => Holds(Time.frameCount, lastFrame);

        /// <summary>
        /// Whether a card stamped on <paramref name="stamped"/> still holds the click on frame
        /// <paramref name="now"/>: that frame or the one before it, and never a frame that has not
        /// happened yet. The editor can start play without reloading the domain, and then this
        /// static outlives the session that wrote it: a card shown an hour into one season left a
        /// stamp of two hundred thousand frames, the next season started again from frame one, and
        /// "now minus stamp" was a large negative number - under the limit, so a card was on screen,
        /// for the whole first hour of a season that had no ceremony in it. E, Escape, the prompt and
        /// every click on the house went nowhere, with nothing on screen to say why.
        /// </summary>
        public static bool Holds(int now, int stamped)
        {
            // In 64 bits, so that no seed can overflow the subtraction into a negative age.
            long age = (long)now - stamped;
            return age >= 0 && age <= 1;
        }

        /// <summary>
        /// Nothing is on screen: what a fresh domain would say. Run as play begins, because a play
        /// session started without a domain reload keeps every static from the one before it.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Forget() => lastFrame = Never;
    }
}
