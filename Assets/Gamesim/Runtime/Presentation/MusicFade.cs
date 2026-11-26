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

        /// <summary>
        /// Seconds from silence to full for a competition's own loop, and from full to silence. It
        /// comes up in a second, so it is there by the end of the count, while the season's bed falls
        /// over its 1.5 s; it goes over 1.5 s as the card comes, while the bed rises over its 2 s.
        /// </summary>
        public const float CompetitionFadeIn = 1.0f;
        public const float CompetitionFadeOut = 1.5f;

        /// <summary>The competition loop's gain at full level, as a share of the master volume: the season bed's, which it takes over from.</summary>
        public const float CompetitionPeak = 0.45f;

        /// <summary>A competition loop's fade: 1 s up, 1.5 s down.</summary>
        public static float Competition(float level, float target, float seconds) =>
            Step(level, target, target > level ? CompetitionFadeIn : CompetitionFadeOut, seconds);

        /// <summary>Seconds a duck takes to fall from full to its level.</summary>
        public const float DuckIn = 0.3f;

        /// <summary>Seconds a duck takes to come back up from its level to full.</summary>
        public const float DuckOut = 0.6f;

        /// <summary>The music's level, as a share of where it was, while the game menu card is open: about a third.</summary>
        public const float PausedMusicDuck = 0.35f;

        /// <summary>The rooms' beds' level while the game menu card is open: half.</summary>
        public const float PausedBedDuck = 0.5f;

        /// <summary>
        /// The music's level under one of the game's big moments: the diary room, a vote, key or jury
        /// reveal, and a ceremony staged in the house. Half, so the moment's own cue stands clear of
        /// the bed: at full, the vote's cue is only about 4 dB louder than the bed.
        /// </summary>
        public const float MomentMusicDuck = 0.5f;

        /// <summary>
        /// How far the music is ducked: to <see cref="PausedMusicDuck"/> with the game menu card open,
        /// to <see cref="MomentMusicDuck"/> under a big moment, and not at all otherwise. The Settings
        /// frame is not the card, so its sliders are heard at the level they set.
        /// </summary>
        public static float MusicDuckFor(bool gameMenuOpen, bool bigMoment) =>
            gameMenuOpen ? PausedMusicDuck : bigMoment ? MomentMusicDuck : 1f;

        /// <summary>How far the rooms' beds are ducked: to <see cref="PausedBedDuck"/> with the game menu card open, and not at all otherwise.</summary>
        public static float BedDuckFor(bool gameMenuOpen) => gameMenuOpen ? PausedBedDuck : 1f;

        /// <summary>
        /// The music's level while the player's activity plays music of its own - the dance's beat
        /// (<see cref="ActivitySounds.DucksMusic"/>): a quarter, so the beat is the music in the room
        /// and the season's bed is only under it.
        /// </summary>
        public const float ActivityMusicDuck = 0.25f;

        /// <summary>
        /// How far the music is ducked, with the player's activity in it: as
        /// <see cref="MusicDuckFor(bool, bool)"/>, and to <see cref="ActivityMusicDuck"/> while an
        /// activity's own music is heard. The deepest wins, so the game menu card opened over a dance
        /// leaves the music where the dance put it.
        /// </summary>
        public static float MusicDuckFor(bool gameMenuOpen, bool bigMoment, bool activityMusic)
        {
            float duck = MusicDuckFor(gameMenuOpen, bigMoment);
            return activityMusic && ActivityMusicDuck < duck ? ActivityMusicDuck : duck;
        }

        /// <summary>
        /// A duck's level moved toward <paramref name="target"/> for <paramref name="seconds"/>, in a
        /// straight line: a fall from full to <paramref name="deepest"/> takes <see cref="DuckIn"/>, and
        /// the way back from it to full takes <see cref="DuckOut"/>, so a duck turned round halfway
        /// takes half the time. <paramref name="deepest"/> is the level of the duck being entered or
        /// left (see <see cref="MusicDuck"/>). Time that did not pass moves nothing.
        /// </summary>
        public static float Duck(float level, float target, float deepest, float seconds)
        {
            level = float.IsNaN(level) ? 1f : Clamp01(level);
            target = float.IsNaN(target) ? 1f : Clamp01(target);
            if (float.IsNaN(seconds) || seconds <= 0f) return level;
            float floor = float.IsNaN(deepest) ? target : Clamp01(deepest);
            floor = Math.Min(floor, Math.Min(level, target));
            float span = 1f - floor;
            if (span <= 0f || level == target) return target;
            return Step(level, target, (target < level ? DuckIn : DuckOut) / span, seconds);
        }

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }

    /// <summary>
    /// One duck: a share of a voice's level, 1 when nothing holds it down, eased toward what the
    /// game asks for by <see cref="MusicFade.Duck"/>. The house keeps one for its music and one for
    /// the rooms' beds; the cues have none and are never ducked.
    ///
    /// <para>It remembers how deep it is going, or has rested, so a fall and a return each move at
    /// one rate for that duck: from a third back to full in 0.6 s, and from half back to full in
    /// 0.6 s too. Asking for the level it is already heading to changes nothing, so the director
    /// asks every frame.</para>
    /// </summary>
    public sealed class MusicDuck
    {
        private float deepest = 1f;

        /// <summary>Where the duck is now: 1 is not ducked.</summary>
        public float Level { get; private set; } = 1f;

        /// <summary>Where the duck is heading.</summary>
        public float Target { get; private set; } = 1f;

        /// <summary>Asks the duck to head for <paramref name="target"/>, 0 to 1 (not a number is 1).</summary>
        public void Ask(float target)
        {
            target = float.IsNaN(target) ? 1f : target < 0f ? 0f : target > 1f ? 1f : target;
            Target = target;
            if (target < deepest) deepest = target;
        }

        /// <summary>Moves the duck on by <paramref name="seconds"/>.</summary>
        public void Tick(float seconds)
        {
            Level = MusicFade.Duck(Level, Target, deepest, seconds);
            // Arrived, it rests here, and the way back is measured from here.
            if (Level == Target) deepest = Target;
        }
    }
}
