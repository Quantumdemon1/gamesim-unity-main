using System;

namespace Gamesim.Presentation
{
    /// <summary>
    /// How the music fades, as the reference build's two audio hooks fade it: a straight line in
    /// gain at a constant rate, so a fade that starts halfway takes half the time, and a fade turned
    /// round in the middle carries on from wherever it had got to rather than jumping.
    ///
    /// <para>The theme rises and falls over 1.5 s (<c>useIntroAudio.ts</c>'s fadeDuration). The
    /// season's bed rises over 2 s and falls over 1.5 s (<c>useBackgroundMusic.ts</c>'s FADE_IN_MS
    /// and FADE_OUT_MS). A fade out calls it silence once it is within a thousandth of it, as both
    /// hooks end theirs.</para>
    ///
    /// <para>Arithmetic only, with nothing of Unity's in it, so the numbers are pinned in EditMode
    /// without an audio device or a clock, and outside the editor by Tools/SimulationTests.
    /// <see cref="HouseAudio"/> steps it once a frame on unscaled time, crediting no frame more than
    /// <see cref="LongestStep"/>.</para>
    /// </summary>
    public static class MusicFade
    {
        /// <summary>Seconds from silence to full for the theme, and back.</summary>
        public const float ThemeFadeIn = 1.5f;
        public const float ThemeFadeOut = 1.5f;

        /// <summary>Seconds from silence to full for the season's bed, and from full to silence.</summary>
        public const float SeasonFadeIn = 2.0f;
        public const float SeasonFadeOut = 1.5f;

        /// <summary>The level a fade out gives up at and calls silence: the reference build's 0.001.</summary>
        public const float SilenceFloor = 0.001f;

        /// <summary>
        /// Each voice's gain at full level, as a share of the master volume.
        ///
        /// <para>Both are the port's loudness from before there were fades - its one music gain of
        /// 0.45 - so the fades change how the music moves and not how loud it is. The reference build
        /// plays its theme at 0.35 of master and its bed at 0.15, the theme about 2.3 times the bed;
        /// whoever balances the two by ear starts from there. That ratio cannot be had with the bed
        /// kept where it is: the theme would need 0.45 x 2.33 = 1.05 at full volume, over an
        /// AudioSource's ceiling of 1, so taking it means making the bed quieter.</para>
        /// </summary>
        public const float ThemePeak = 0.45f;
        public const float SeasonPeak = 0.45f;

        /// <summary>
        /// The most of one frame a fade is credited: a twentieth of a second.
        ///
        /// <para>Unscaled time, which the fades run on so that a paused game still fades its music,
        /// is not capped by maximumDeltaTime, and a frame stalled by a clip decoding or the house
        /// being placed would otherwise move a fade by all of its stall - a voice entering after a
        /// 300 ms frame would start a fifth of the way up its rise instead of from silence. The
        /// reference build takes its first step from the moment its fade begins, so it never inherits
        /// a hitch from before. At a twentieth every frame at 20 fps or better counts in full, and
        /// it is a step nobody hears; a fade held up by a longer frame loses the difference instead of jumping.</para>
        /// </summary>
        public const float LongestStep = 1f / 20f;

        /// <summary>
        /// A frame of <paramref name="seconds"/>, as a fade is credited it: all of it up to
        /// <see cref="LongestStep"/>, and <see cref="LongestStep"/> of anything longer. Time that did
        /// not pass is passed on as it is, for <see cref="Step"/> to ignore.
        /// </summary>
        public static float FrameStep(float seconds) => seconds > LongestStep ? LongestStep : seconds;

        // Within this of its target a fade has arrived. Frame-sized float steps that add up to the
        // whole fade can land a hair short of it, and a voice held at 0.99999 is never "at full".
        private const double Arrival = 1e-4;

        /// <summary>
        /// <paramref name="level"/> moved toward <paramref name="target"/> for <paramref name="seconds"/>,
        /// at the rate that covers the whole range, 0 to 1, in <paramref name="fullSeconds"/>.
        ///
        /// <para>Time that did not pass - zero, negative or NaN - moves nothing. A fade out that
        /// reaches <see cref="SilenceFloor"/> is silence; a fade in is never snapped, because a
        /// headless frame is under half a millisecond and its first steps are smaller than the floor.
        /// A <paramref name="fullSeconds"/> of zero or less is a cut straight to the target.</para>
        /// </summary>
        public static float Step(float level, float target, float fullSeconds, float seconds)
        {
            if (float.IsNaN(level)) level = 0f;
            if (float.IsNaN(seconds) || seconds <= 0f) return level;
            target = float.IsNaN(target) ? 0f : Clamp01(target);
            level = Clamp01(level);
            if (level == target) return target;
            if (float.IsNaN(fullSeconds) || fullSeconds <= 0f) return target;

            double rate = seconds / (double)fullSeconds;
            double next = level < target ? Math.Min(target, level + rate) : Math.Max(target, level - rate);
            if (Math.Abs(target - next) <= Arrival) next = target;
            if (target < level && next <= SilenceFloor) next = 0.0;
            return (float)next;
        }

        /// <summary>The theme's fade: 1.5 s whichever way it goes.</summary>
        public static float Theme(float level, float target, float seconds) =>
            Step(level, target, target > level ? ThemeFadeIn : ThemeFadeOut, seconds);

        /// <summary>The season bed's fade: 2 s up, 1.5 s down.</summary>
        public static float Season(float level, float target, float seconds) =>
            Step(level, target, target > level ? SeasonFadeIn : SeasonFadeOut, seconds);

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
