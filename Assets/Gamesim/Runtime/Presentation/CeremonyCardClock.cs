namespace Gamesim.Presentation
{
    /// <summary>
    /// The one clock every ceremony card runs on (UI-UX plan D3a): the takeover and its veto meeting,
    /// the sting, the key ceremony, the live eviction, the jury's vote and the veto draw each own one.
    /// It keeps what each card used to keep for itself - the frame it was played in, the real time it
    /// has been up, its own time at its speed, the speed, and the device the player last pressed - and
    /// the rules they all shared:
    ///
    /// <para>Neither the frame a card was played in nor the next is its own time: the first update's
    /// delta can be the frame before it was played, the second's the frame it was played in (the
    /// commit, its save, the house's render), before anybody had seen it (<see cref="Counts"/>).</para>
    ///
    /// <para>A press moves a card on only once it has been up past its fade and a short read
    /// (<see cref="ReadDelay"/>): a press already in flight as it appears is not one.</para>
    ///
    /// <para>Speed up toggles between the card's own pace and <see cref="CeremonyPacing.SpeedUp"/>;
    /// it changes how long the order takes, never the order.</para>
    ///
    /// <para>The card reads the presses itself, through the Ceremony map's Skip and Speed, and hands
    /// them here; the clock reads no device. Unity-free, so the subset holds the rules itself.</para>
    /// </summary>
    public struct CeremonyCardClock
    {
        /// <summary>How long past its fade a card must have been up before a press moves it on, in real seconds.</summary>
        public const float ReadDelay = 0.35f;

        private int playedFrame;
        private float speed;

        /// <summary>The card's own time, run at its speed: what its beats are scheduled on.</summary>
        public float Elapsed;

        /// <summary>The real time the card has been up, from its first counted frame: what the read delay is measured on.</summary>
        public float UpFor;

        /// <summary>Whether the controls the card names are the pad's: the device the player last pressed.</summary>
        public bool OnPad;

        /// <summary>How fast the card's own time runs: 1, or <see cref="CeremonyPacing.SpeedUp"/> while the player has sped it up.</summary>
        public float Speed => speed > 0f ? speed : 1f;

        /// <summary>Whether the card is running sped up.</summary>
        public bool SpedUp => Speed > 1f;

        /// <summary>
        /// Starts the clock in <paramref name="frame"/>: nothing on it yet, at <paramref name="startSpeed"/>,
        /// naming the pad's controls when <paramref name="onPad"/>.
        /// </summary>
        public void Start(int frame, float startSpeed, bool onPad)
        {
            playedFrame = frame;
            Elapsed = 0f;
            UpFor = 0f;
            speed = startSpeed > 0f ? startSpeed : 1f;
            OnPad = onPad;
        }

        /// <summary>Runs the card's own time at <paramref name="value"/> from now on.</summary>
        public void SetSpeed(float value) => speed = value > 0f ? value : 1f;

        /// <summary>Whether <paramref name="frame"/> is the card's own time: not the frame it was played in, nor the next.</summary>
        public bool Counts(int frame) => frame - playedFrame > 1;

        /// <summary>Whether the card has been up long enough, past a fade of <paramref name="fadeIn"/>, for a press to move it on.</summary>
        public bool Readable(float fadeIn) => UpFor >= fadeIn + ReadDelay;

        /// <summary>
        /// One counted frame of real time, <paramref name="delta"/> seconds, with what the player pressed
        /// in it: <paramref name="pad"/> is the device the frame's press came from (null for none),
        /// <paramref name="speedPressed"/> and <paramref name="skipPressed"/> the Ceremony map's Speed and
        /// Skip. Follows the device, and toggles the speed on a readable Speed; says what changed and
        /// whether a readable Skip came. The card's own time is run separately (<see cref="Run"/>), after
        /// the card has answered a skip.
        /// </summary>
        public CardPresses Step(float delta, bool? pad, bool speedPressed, bool skipPressed, float fadeIn = CeremonyPacing.FadeIn)
        {
            UpFor += delta;
            bool device = pad.HasValue && pad.Value != OnPad;
            if (device) OnPad = pad.Value;
            bool readable = Readable(fadeIn);
            bool faster = readable && speedPressed;
            if (faster) speed = SpedUp ? 1f : CeremonyPacing.SpeedUp;
            return new CardPresses(device, faster, readable && skipPressed);
        }

        /// <summary>Runs the card's own time on by <paramref name="delta"/> real seconds at its speed; returns where it is.</summary>
        public float Run(float delta) => Elapsed += delta * Speed;

        /// <summary>The cards' ease out: quick to arrive, settling into place.</summary>
        public static float Eased(float t) => 1f - (1f - t) * (1f - t);

        /// <summary>
        /// A card's fade at <paramref name="t"/> on its own time, for a card that holds until
        /// <paramref name="end"/>: in over <paramref name="fadeIn"/>, out over <paramref name="fadeOut"/>,
        /// and under reduced motion no fade at all for the same time on screen. False once the card is over.
        /// </summary>
        public static bool Fade(float t, float end, bool reduced, float fadeIn, float fadeOut, out float alpha)
        {
            if (reduced)
            {
                // Reading time, not movement: the same time on screen with no fade.
                alpha = 1f;
                return t < end + fadeOut;
            }
            if (t < fadeIn) { alpha = Eased(t / fadeIn); return true; }
            if (t < end) { alpha = 1f; return true; }
            float exit = (t - end) / fadeOut;
            if (exit >= 1f) { alpha = 0f; return false; }
            alpha = 1f - Eased(exit);
            return true;
        }

        /// <summary>The reveals' fade, on <see cref="CeremonyPacing"/>'s fades.</summary>
        public static bool Fade(float t, float end, bool reduced, out float alpha) =>
            Fade(t, end, reduced, CeremonyPacing.FadeIn, CeremonyPacing.FadeOut, out alpha);
    }

    /// <summary>What one counted frame brought a card (<see cref="CeremonyCardClock.Step"/>).</summary>
    public readonly struct CardPresses
    {
        /// <summary>The player pressed on the other device: the card's controls change their words.</summary>
        public readonly bool DeviceChanged;

        /// <summary>A readable Speed: the card's speed toggled.</summary>
        public readonly bool SpeedChanged;

        /// <summary>A readable Skip: the card takes its next step, to its result or off the screen.</summary>
        public readonly bool Skip;

        public CardPresses(bool deviceChanged, bool speedChanged, bool skip)
        {
            DeviceChanged = deviceChanged; SpeedChanged = speedChanged; Skip = skip;
        }
    }
}
