using System;

namespace Gamesim.Presentation
{
    /// <summary>The ground under the player's feet, as their steps sound on it.</summary>
    public enum StepSurface { Floor, Tile, Grass, Deck }

    /// <summary>
    /// The player's footsteps, as numbers: how far apart the feet land at a speed, the ground each
    /// room is, how loud a step is from where the camera looks, and the small change of pitch
    /// between one step and the next. <c>HouseFootsteps</c> plays them; the steps live in
    /// <c>Resources/Audio/Steps/&lt;Surface&gt;_&lt;1..4&gt;</c>, rendered by <c>ArtSource/audio/bb_steps.py</c>.
    ///
    /// <para>The stride is the body's own, read off the takes it walks and runs with rather than
    /// from animation events: the walk plays two footfalls a cycle at the pace
    /// <c>CharacterPresentation</c> gives the animator, so a foot lands every
    /// <see cref="StepSeconds"/>, and at the house's walk of 2.2 m/s that is every 0.87 m.</para>
    ///
    /// <para>Presentation only: nothing here reads or writes the season, and the pitch's variation
    /// comes from a generator of its own (<see cref="StepVariations"/>), never the season's
    /// random stream.</para>
    /// </summary>
    public static class HouseSteps
    {
        /// <summary>Where the steps live: <c>Resources/Audio/Steps/&lt;Surface&gt;_&lt;n&gt;</c>.</summary>
        public const string ResourceFolder = "Audio/Steps/";

        /// <summary>How many takes of a step each ground has, played in turn.</summary>
        public const int Variations = 4;

        /// <summary>The ground each take covers in a second, as <c>CharacterPresentation.WalkTakeSpeed</c> and <c>RunTakeSpeed</c> have it.</summary>
        public const float WalkTakeSpeed = 1.68f, RunTakeSpeed = 4.83f;

        /// <summary>How far the animator speeds a take up or slows it down, as <c>CharacterPresentation.SlowestPace</c> and <c>FastestPace</c> have it.</summary>
        public const float SlowestPace = .75f, FastestPace = 1.65f;

        /// <summary>One cycle of the walk (Walk_loop: 31 frames at 30 frames a second); a foot lands twice in it.</summary>
        public const float WalkCycleSeconds = 31f / 30f;

        /// <summary>One cycle of the run (Jog_Fwd_Loop: frames 0 to 28 at 30 frames a second); a foot lands twice in it.</summary>
        public const float RunCycleSeconds = 28f / 30f;

        /// <summary>Slower than this the body is standing, as <c>CharacterPresentation.Pace</c> reads it: no foot lands.</summary>
        public const float StandingBelow = .3f;

        /// <summary>Faster than this the body was moved, not walked (a warp, a load), as <c>CharacterPresentation</c> reads it: no foot lands.</summary>
        public const float WarpAbove = 8f;

        /// <summary>Seconds a body stands before its next walk starts afresh: a frame's stall mid-walk keeps the stride's place.</summary>
        public const float StillSeconds = .2f;

        /// <summary>The share of a stride already walked as a body sets off, so the first foot lands a quarter of a stride in.</summary>
        public const float FirstStep = .75f;

        /// <summary>Within this many metres of the camera's focus a step is at full level.</summary>
        public const float NearMetres = 4f;

        /// <summary>From this many metres of the camera's focus a step is not heard.</summary>
        public const float FarMetres = 16f;

        /// <summary>The most a step's pitch is moved either way, as a share: about two thirds of a semitone.</summary>
        public const float PitchJitter = .04f;

        /// <summary>Feet this far over the yard's lawn stand on a deck: the pool's, which rises 0.35 m.</summary>
        public const float DeckAbove = .25f;

        /// <summary>The steps' place in the mix, in decibels against their recordings: 0 plays them as rendered.</summary>
        public const float GainDb = 0f;

        /// <summary>The factor the steps are played at for their place in the mix.</summary>
        public static float Gain => (float)Math.Pow(10.0, GainDb / 20.0);

        /// <summary>How fast the take plays at <paramref name="speed"/> metres a second: the animator's pace.</summary>
        public static float Pace(float speed, bool running)
        {
            if (!(speed >= StandingBelow)) return 1f;
            float pace = speed / (running ? RunTakeSpeed : WalkTakeSpeed);
            return Math.Min(FastestPace, Math.Max(SlowestPace, pace));
        }

        /// <summary>Seconds from one foot landing to the next at <paramref name="speed"/>: half the take's cycle at its pace.</summary>
        public static float StepSeconds(float speed, bool running)
            => (running ? RunCycleSeconds : WalkCycleSeconds) / 2f / Pace(speed, running);

        /// <summary>
        /// Metres from one foot landing to the next at <paramref name="speed"/>: the take's own
        /// stride while its pace is inside the animator's range (0.87 m at a walk, 2.25 m at a
        /// run), shorter below it and longer above.
        /// </summary>
        public static float Stride(float speed, bool running) => speed * StepSeconds(speed, running);

        /// <summary>
        /// The ground in <paramref name="room"/>, with the feet <paramref name="raised"/> metres over
        /// its floor: the kitchen's and the games room's tile, the yard's lawn or, raised, the pool's
        /// deck, and wood everywhere else, a room not known included.
        /// </summary>
        public static StepSurface SurfaceOf(string room, float raised)
        {
            switch (room)
            {
                case "Kitchen":
                case "Games":
                    return StepSurface.Tile;
                case "Yard":
                    return raised > DeckAbove ? StepSurface.Deck : StepSurface.Grass;
                default:
                    return StepSurface.Floor;
            }
        }

        /// <summary>The take of a step on <paramref name="surface"/>, by its file's name: <c>Floor_1</c> to <c>Floor_4</c>.</summary>
        public static string ClipName(StepSurface surface, int variation)
            => surface + "_" + (((variation % Variations) + Variations) % Variations + 1);

        /// <summary>
        /// A step's level <paramref name="metres"/> from the camera's focus, 0 to 1: full within
        /// <see cref="NearMetres"/>, falling away on a curve to nothing at <see cref="FarMetres"/>.
        /// The house's voices are not placed in space, so this is what tells a step near from far.
        /// </summary>
        public static float GainAt(float metres)
        {
            if (float.IsNaN(metres) || metres >= FarMetres) return 0f;
            if (metres <= NearMetres) return 1f;
            float left = 1f - (metres - NearMetres) / (FarMetres - NearMetres);
            return left * left;
        }
    }

    /// <summary>
    /// When a foot lands, from how far the body travelled each frame: every
    /// <see cref="HouseSteps.Stride"/> at the frame's speed, one landing at most a frame. A body
    /// standing, or moved faster than any walk, lands none. Arithmetic only, so
    /// Tools/SimulationTests pins it.
    /// </summary>
    public sealed class StepClock
    {
        private float still;

        /// <summary>How much of the next stride has been walked, 0 to 1.</summary>
        public float Phase { get; private set; }

        /// <summary>Whether the body is under way: it has moved since it last stood for <see cref="HouseSteps.StillSeconds"/>.</summary>
        public bool Walking { get; private set; }

        /// <summary>
        /// A frame of <paramref name="seconds"/> in which the body covered <paramref name="metres"/>
        /// over the ground; true when a foot lands in it. The first landing of a walk comes a quarter
        /// of a stride in (<see cref="HouseSteps.FirstStep"/>). A long frame lands one foot, not a
        /// burst of them, and keeps its place in the next stride.
        /// </summary>
        public bool Advance(float metres, float seconds, bool running)
        {
            if (!(seconds > 0f) || !(metres >= 0f) || float.IsInfinity(metres)) return false;
            float speed = metres / seconds;
            // A warp or a load: the body did not walk there, and the walk it was on keeps its place.
            if (speed > HouseSteps.WarpAbove) return false;
            if (speed < HouseSteps.StandingBelow)
            {
                still += seconds;
                if (still >= HouseSteps.StillSeconds) Halt();
                return false;
            }
            still = 0f;
            if (!Walking) { Walking = true; Phase = HouseSteps.FirstStep; }
            Phase += metres / HouseSteps.Stride(speed, running);
            if (Phase < 1f) return false;
            Phase -= (float)Math.Floor(Phase);
            return true;
        }

        /// <summary>The body has stopped, sat down or gone: its next walk starts afresh.</summary>
        public void Halt()
        {
            Walking = false;
            Phase = 0f;
            still = 0f;
        }
    }

    /// <summary>
    /// The four takes of a step in turn, each at a pitch moved a little: from a generator of its
    /// own, seeded here, so the house's steps never draw on the season's random stream and a
    /// session's steps vary the same way every time.
    /// </summary>
    public sealed class StepVariations
    {
        /// <summary>The seed the house's steps vary from.</summary>
        public const int Seed = 1212;

        private readonly Random random;

        /// <summary>The take the next step plays, 0 to <see cref="HouseSteps.Variations"/> less one.</summary>
        public int Next { get; private set; }

        public StepVariations(int seed = Seed) { random = new Random(seed); }

        /// <summary>The take this step plays; the next step plays the one after it, round.</summary>
        public int Take()
        {
            int take = Next;
            Next = (Next + 1) % HouseSteps.Variations;
            return take;
        }

        /// <summary>This step's pitch: 1, moved by up to <see cref="HouseSteps.PitchJitter"/> either way.</summary>
        public float Pitch() => 1f + ((float)random.NextDouble() * 2f - 1f) * HouseSteps.PitchJitter;
    }
}
