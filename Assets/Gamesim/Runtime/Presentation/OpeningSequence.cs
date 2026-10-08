using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.House;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The five beats before the first competition, as the reference build plays them: the intro
    /// (a title card, then every houseguest through the front door one at a time, then the whole
    /// house together), the house entry, the walk-in, the tour, and the introductions.
    ///
    /// <para>It began as five title cards over a house nobody moved in - confetti, a room tour in
    /// alphabetical order, and a meet and greet that held a card for two seconds and made everyone
    /// like the player a little. The reference build's own opening is a premiere: a title, each
    /// houseguest walking through the doors with their name coming up under them, the house
    /// walking in, and then the player introducing themselves to every houseguest in turn and
    /// reading each one's traits to pick the right approach. That is what this now plays.</para>
    ///
    /// <para>The reference build is the GitHub web game (Quantumdemon1/gamesim, cloned at
    /// D:/gamesim-web), whose Big Brother opening is live code there: <c>IntroSequence.tsx</c> and
    /// <c>IntroBBEyeLogo.tsx</c> for the premiere, <c>tunnel/EntranceDoor.tsx</c> and
    /// <c>tunnel/CameraEffects.tsx</c> for the door, then <c>HouseEntrySequence.tsx</c>,
    /// <c>HouseWalkInSequence.tsx</c> and <c>MeetAndGreetPhase.tsx</c>, all under
    /// src/components/game-phases. D:/gamesim-main is a Survivor conversion of it and is not the
    /// reference for any of this.</para>
    ///
    /// <para><b>Everything is skippable and every beat is recorded.</b> A sequence that replays on
    /// every load is the single worst thing a cinematic can do, so each beat commits
    /// <see cref="EpisodeCommandKind.MarkOpeningBeat"/> as it finishes and a season that has seen a
    /// beat never plays it again. Skipping the show stops at the introductions when there are any
    /// left, because they are a choice rather than a show: a player who skipped the titles has
    /// decided about the titles, not about how to meet the house. The introductions have their own
    /// skip, which forfeits whoever was not met, as the reference build's does.</para>
    ///
    /// <para>Motion is the decoration, not the content. Under the reduced-motion preference nobody
    /// walks and nothing slides, scales or swings, while every card still holds long enough to read
    /// - the preference removes movement, not information.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class OpeningSequence : MonoBehaviour
    {
        /// <summary>Captions tests and the tour find these controls by.</summary>
        public const string SkipCaption = "Skip the opening";
        public const string ContinueCaption = "Continue";
        public const string NextCaption = "Next";
        public const string LetsPlayCaption = "Let's Play!";
        public const string SkipIntroductionsCaption = "Skip Introductions";
        public const string MeetHeading = "Meet the Houseguests";

        /// <summary>
        /// The ground under every hint the stage lays over the house - the Continue hint, the reveal's
        /// count, the introductions' header - as a sibling of the words it stands behind, which the
        /// tests find by this name. A hint laid straight on the camera's picture stood inside the lit
        /// doorway and vanished on the dark yard (UI-UX-PASS-PLAN S0).
        /// </summary>
        public const string GroundName = "Ground";

        /// <summary>The Skip pill's measure, which the Continue hint shares on the same row: height, width and the margin from the frame's corner.</summary>
        private const float PillHeight = 42f, PillWidth = 240f, PillMargin = 40f;

        /// <summary>What an introduction came to, as the game around the sequence committed it.</summary>
        public struct Introduction
        {
            public bool Accepted;
            /// <summary>Why it was refused, in words for the player.</summary>
            public string Reason;
            public WebIntroductions.Outcome Outcome;
        }

        /// <summary>
        /// The front door and the people walking through it: the world half of the intro, which the
        /// director owns because it owns the bodies. Every call is safe to make whatever state the
        /// stage is in, and every wait on it is bounded by the sequence in real seconds.
        /// </summary>
        public interface IStage
        {
            /// <summary>How many of the bodies have finished assembling, 0 to 1.</summary>
            float BodyReadiness { get; }
            /// <summary>Puts everybody behind the front door and builds it. False when it cannot.</summary>
            bool TryPlace();
            bool Placed { get; }
            /// <summary>Whether the house has bound everybody again after placing, so they can walk.</summary>
            bool Ready { get; }
            /// <summary>Whether the house's movement stopped: nobody can be walked through anything.</summary>
            bool Failed { get; }
            /// <summary>Puts everybody back where the season started them. Only ever under an opaque frame.</summary>
            void RestoreHome();
            HouseCameraRig.Shot DoorShot { get; }
            HouseCameraRig.Shot PushInShot { get; }
            /// <summary>Walks someone waiting up to just behind the door; false when they cannot be sent.</summary>
            bool OnDeck(string id);
            bool ToDoor(string id);
            bool AtDoor(string id);
            void OpenDoor();
            void CloseDoor();
            bool ThroughDoor(string id);
            bool OnMark(string id);
            /// <summary>Turns them to the camera and has them greet it.</summary>
            void Present(string id);
            /// <summary>Walks them off, out of shot, to where the house gathers.</summary>
            void SendOff(string id);
            /// <summary>Takes the front door down. Only ever under an opaque frame.</summary>
            void StrikeSet();
            void SendHome(string id);
            bool AllHome { get; }
            /// <summary>
            /// Ends every walk and holds everybody where they stand, for the introductions: a
            /// houseguest framed where they stand is still there when they answer.
            /// </summary>
            void HoldForIntroductions();
        }

        /// <summary>What the sequence needs from the game around it.</summary>
        public sealed class Settings
        {
            /// <summary>Commits a finished beat. With <see cref="Introduce"/>, the only things here that touch the season.</summary>
            public Action<string> MarkBeat;

            /// <summary>Optional. Without it nothing moves the camera.</summary>
            public HouseCameraRig Rig;

            /// <summary>The walk-in's crane: inside the house facing the yard, up, and over the top.</summary>
            public IReadOnlyList<HouseCameraRig.Shot> WalkInKeys = new HouseCameraRig.Shot[0];

            /// <summary>Optional. Without it every houseguest is introduced on a card instead of through the door.</summary>
            public IStage Stage;

            /// <summary>Runs the first-run tour and calls back when it is done or skipped.</summary>
            public Action<Action> RunTutorial;

            /// <summary>Closes the tour when the sequence is skipped or cancelled underneath it.</summary>
            public Action CancelTutorial;

            /// <summary>Called once, whether the sequence finished or was skipped.</summary>
            public Action Finished;

            /// <summary>Told as each beat begins, before it has drawn anything.</summary>
            public Action<string> BeatStarted;

            public bool ReducedMotion;

            /// <summary>
            /// Holds each card for its time even in batchmode, which otherwise skips every hold so
            /// an automated season is never kept waiting. Only a test that photographs a card sets it.
            /// </summary>
            public bool HoldHeadless;

            /// <summary>Every hold waits for <see cref="Advance"/> instead of a clock: a test's way to stop on a frame.</summary>
            public bool HoldUntilAdvanced;

            /// <summary>Plays the movement in batchmode too, for a verification run that photographs it.</summary>
            public bool MotionInBatchmode;

            /// <summary>The house, player included, in the season's order.</summary>
            public IReadOnlyList<ContestantState> Cast = new List<ContestantState>();

            /// <summary>The season's seed, which picks each houseguest's lines.</summary>
            public int Seed;

            /// <summary>The season's own opening line, which the house-entry card reads out.</summary>
            public string ArrivalLine = string.Empty;

            /// <summary>
            /// Commits an introduction: the houseguest's id and "warm", "calculated" or "bold".
            /// Without it the introductions are a card that hands over to the house.
            /// </summary>
            public Func<string, string, Introduction> Introduce;

            /// <summary>Whether the player has already met someone: a resumed season starts with the first stranger.</summary>
            public Func<string, bool> Introduced;

            /// <summary>Frames a houseguest for their introduction.</summary>
            public Action<string> FrameGuest;

            /// <summary>Plays how a houseguest took the player's introduction.</summary>
            public Action<string, WebIntroductions.Outcome> GuestReacts;

            /// <summary>Plays the house's click for a committed introduction, as the reference build clicks on a choice.</summary>
            public Action Click;

            /// <summary>
            /// How long the introductions' new controls stay disarmed, in real seconds. Null is the
            /// rule for play: 0.35 s for real input and none in batchmode. A test sets it to drive
            /// the arming on purpose, or to 0 to take it out of the way.
            /// </summary>
            public float? ArmSeconds;

            /// <summary>Which season of the show this is, for the title card's "Season N": 1 for a first season.</summary>
            public int SeasonNumber = 1;
        }

        /// <summary>
        /// Whether the theme should wait: the house is still assembling behind the loading screen,
        /// and the reference build starts its theme only once everything is ready.
        /// </summary>
        public bool MusicHeld { get; private set; }

        /// <summary>Whether the intro's closing fade has begun, which the theme fades out under.</summary>
        public bool MusicClosing { get; private set; }

        private CanvasGroup group;
        private CanvasScaler scaler;
        private RectTransform stage;
        private Settings settings;
        private Coroutine running;
        private readonly List<string> remaining = new List<string>();
        private bool advancePending;
        private int advanceFrame = -1;

        public bool IsPlaying => running != null;

        /// <summary>The beat on screen, or null between beats and when nothing is playing.</summary>
        public string CurrentBeat { get; private set; }

        /// <summary>The houseguest being introduced - through the door, on a card or in person - or null.</summary>
        public string CurrentGuestId { get; private set; }

        /// <summary>Whether the introductions are on screen and waiting on the player.</summary>
        public bool IsMeeting { get; private set; }

        /// <summary>Whether the last run was ended by a skip rather than played out.</summary>
        public bool WasSkipped { get; private set; }

        /// <summary>
        /// Whether skipping would forfeit a choice: the introductions are still to come, the game
        /// around the sequence can commit them, and somebody has not been met.
        /// </summary>
        public bool IntroductionsPending =>
            settings != null && settings.Introduce != null && remaining.Contains(OpeningBeat.MeetAndGreet)
            && Guests().Any(guest => !Met(guest.id));

        public float FontScale
        {
            set
            {
                if (scaler == null) return;
                float scale = Mathf.Clamp(value, 0.5f, 2f);
                scaler.referenceResolution = new Vector2(1920f / scale, 1080f / scale);
            }
        }

        /// <summary>
        /// A canvas above every other overlay, and it raycasts: its controls are the only ones on
        /// screen, and a click anywhere else moves the show on rather than walking the player.
        ///
        /// <para>Parented to the director, the way the cast screen and the season report are, rather
        /// than sitting on its own scene root like the ceremony cards. The ceremony cards stay
        /// outside that subtree on purpose because they are watched rather than used, and a card that
        /// happened to carry a houseguest's name would show up in every enumeration of the director's
        /// controls. This one is a screen with controls on it, so it belongs where the other screens
        /// are.</para>
        /// </summary>
        public static OpeningSequence Attach(GameObject owner)
        {
            var root = new GameObject("Gamesim Opening Sequence",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(CanvasGroup));
            root.transform.SetParent(owner.transform, false);

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the ceremony takeover's 100 and the cast screen's 125: nothing outranks the
            // opening, because nothing else should be happening during it.
            canvas.sortingOrder = 140;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var sequence = root.AddComponent<OpeningSequence>();
            sequence.scaler = scaler;
            sequence.group = root.GetComponent<CanvasGroup>();
            sequence.Hide();
            return sequence;
        }

        private void Hide()
        {
            // Alpha and raycasts only. A group made non-interactable bakes the disabled tint into
            // every control built under it, and the focus ring skips a hidden overlay by its alpha.
            group.alpha = 0f;
            group.blocksRaycasts = false;
            CurrentBeat = null;
            CurrentGuestId = null;
            IsMeeting = false;
        }

        private void Show()
        {
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
        }

        /// <summary>
        /// Plays whichever of the five beats this season has not seen.
        ///
        /// <para>Returns without doing anything when there are none left, which is the ordinary case
        /// for every load after the first. The caller does not have to know the difference.</para>
        /// </summary>
        public void Play(IEnumerable<string> alreadySeen, Settings plan)
        {
            if (running != null) return;
            settings = plan ?? new Settings();
            WasSkipped = false;
            MusicHeld = false;
            MusicClosing = false;

            var seen = new HashSet<string>(alreadySeen ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            remaining.Clear();
            remaining.AddRange(OpeningBeat.InOrder.Where(beat => !seen.Contains(beat)));
            if (remaining.Count == 0) { var done = settings.Finished; settings = null; done?.Invoke(); return; }

            if (settings.Rig != null) settings.Rig.ControlsEnabled = false;
            Show();
            running = StartCoroutine(Run());
        }

        /// <summary>
        /// Ends the sequence now and records every beat it had left, the introductions included.
        ///
        /// <para>Marking the unplayed beats rather than leaving them pending is the point: somebody
        /// who skipped the opening has decided about the opening, and offering it again on the next
        /// load would be arguing with them. The on-screen control is <see cref="PressSkip"/>, which
        /// stops at the introductions instead; this is for callers that mean all of it.</para>
        /// </summary>
        public void Skip()
        {
            if (running == null) return;
            StopAllCoroutines();
            running = null;
            if (CurrentBeat == OpeningBeat.Tutorial) settings.CancelTutorial?.Invoke();
            WasSkipped = true;
            foreach (var beat in remaining.ToList()) Mark(beat);
            Finish();
        }

        /// <summary>
        /// What the skip control and Escape do: skip the show, and stop at the introductions when
        /// there are still people to meet. Skipping a title card must never silently forfeit a
        /// choice the player has not seen yet.
        /// </summary>
        public void PressSkip()
        {
            if (running == null) return;
            if (CurrentBeat == OpeningBeat.MeetAndGreet || !IntroductionsPending) { Skip(); return; }
            StopAllCoroutines();
            // The loading gate and the closing fade were stopped with everything else, and neither
            // clears its own music cue once stopped.
            ReleaseMusicCues();
            if (CurrentBeat == OpeningBeat.Tutorial) settings.CancelTutorial?.Invoke();
            WasSkipped = true;
            foreach (var beat in remaining.ToList())
                if (beat != OpeningBeat.MeetAndGreet) Mark(beat);
            running = StartCoroutine(SkipToIntroductions());
        }

        /// <summary>Ends the sequence without recording anything: the season underneath it is being replaced.</summary>
        public void Cancel()
        {
            if (running == null) return;
            StopAllCoroutines();
            running = null;
            if (CurrentBeat == OpeningBeat.Tutorial) settings.CancelTutorial?.Invoke();
            Finish();
        }

        /// <summary>
        /// Moves the show on: ends the wait on screen - a card's hold, the pause before a houseguest
        /// walks, the moment they stand on their mark. It never cuts a walk short; a press during
        /// one ends the wait that follows it. Counted once a frame, because Space and a focused
        /// Continue can both report the same press.
        /// </summary>
        public void Advance()
        {
            if (running == null || IsMeeting || CurrentBeat == OpeningBeat.Tutorial) return;
            if (advanceFrame == Time.frameCount) return;
            advanceFrame = Time.frameCount;
            advancePending = true;
        }

        private bool TakeAdvance()
        {
            if (!advancePending) return false;
            advancePending = false;
            return true;
        }

        private void Update()
        {
            if (running == null || IsMeeting) return;
            // The Ceremony map's Advance: Space. A pad presses the focused Continue instead.
            if (Gamesim.House.HouseInput.Actions.Advance.WasPressedThisFrame()) Advance();
        }

        // ---------------------------------------------------------------- the sequence

        private IEnumerator Run()
        {
            foreach (var beat in remaining.ToList())
            {
                yield return PlayBeat(beat);
                Mark(beat);
            }
            running = null;
            Finish();
        }

        private IEnumerator PlayBeat(string beat)
        {
            CurrentBeat = beat;
            CurrentGuestId = null;
            advancePending = false;
            // A new beat is past the intro's loading and its closing fade, whichever one it follows.
            ReleaseMusicCues();
            settings.BeatStarted?.Invoke(beat);
            switch (beat)
            {
                case OpeningBeat.Intro: yield return Intro(); break;
                case OpeningBeat.HouseEntry: yield return HouseEntry(); break;
                case OpeningBeat.WalkIn: yield return WalkIn(); break;
                case OpeningBeat.Tutorial: yield return Tutorial(); break;
                case OpeningBeat.MeetAndGreet: yield return MeetAndGreet(); break;
            }
            // A beat that collapses headless still takes a frame, so whatever watches for it sees it.
            yield return null;
        }

        /// <summary>
        /// After a skip with people still to meet: the house is put back where the season started
        /// it - behind black, so nobody is seen jumping - and the introductions play.
        /// </summary>
        private IEnumerator SkipToIntroductions()
        {
            // A skip from the tour arrives with the overlay hidden for it.
            Show();
            var world = settings.Stage;
            if (world != null && world.Placed)
            {
                Clear();
                Scrim(Color.black);
                yield return null;
                world.StrikeSet();
                if (!world.AllHome) world.RestoreHome();
                yield return Pause(0.3f);
            }
            yield return PlayBeat(OpeningBeat.MeetAndGreet);
            Mark(OpeningBeat.MeetAndGreet);
            running = null;
            Finish();
        }

        private void Mark(string beat)
        {
            remaining.Remove(beat);
            settings.MarkBeat?.Invoke(beat);
        }

        /// <summary>
        /// Lets go of both music cues: whatever reads them next hears the house, not a loading
        /// screen or a fade that a skip, a cancel or the next beat has already ended.
        /// </summary>
        private void ReleaseMusicCues()
        {
            MusicHeld = false;
            MusicClosing = false;
        }

        private void Finish()
        {
            // Before the caller is told, so the music it picks on hearing it is the house's.
            ReleaseMusicCues();
            Clear();
            Hide();
            var rig = settings?.Rig;
            if (rig != null)
            {
                // Back to the view the house had before the opening took the camera.
                if (rig.HasShot) rig.ReleaseShot(1.2f);
                rig.ControlsEnabled = true;
            }
            var done = settings?.Finished;
            settings = null;
            done?.Invoke();
        }

        /// <summary>
        /// Hands over to the first-run tour and waits for it.
        ///
        /// <para>The overlay hides rather than closing: the tour points at HUD panels, and a
        /// full-screen scrim over the thing it is pointing at would make it useless.</para>
        /// </summary>
        private IEnumerator Tutorial()
        {
            Clear();
            if (settings.RunTutorial == null) yield break;

            group.alpha = 0f;
            group.blocksRaycasts = false;

            bool done = false;
            settings.RunTutorial(() => done = true);
            // A tour that never calls back - because it was suppressed in batchmode, or because the
            // player has seen it before - must not strand the rest of the opening behind it; ten
            // minutes is long enough to read anything on it.
            float waited = 0f;
            while (!done && waited < 600f) { waited += Time.unscaledDeltaTime; yield return null; }
            if (!done) settings.CancelTutorial?.Invoke();

            group.alpha = 1f;
            group.blocksRaycasts = true;
        }

        // ---------------------------------------------------------------- time

        /// <summary>
        /// Whether to skip the movement - the player asked for that, or nobody is watching.
        ///
        /// <para>Batchmode counts because the animations are timed against real seconds rather than
        /// frames: a title that rises over a second takes a second in a headless run too, and spends
        /// it drawing to a surface that is never presented. A verification run that photographs the
        /// movement asks for it.</para>
        /// </summary>
        private bool Motionless => settings == null || settings.ReducedMotion
                                   || (Application.isBatchMode && !settings.MotionInBatchmode);

        /// <summary>Whether nothing is watching the holds: a headless run that is not photographing them.</summary>
        private bool Unwatched => Application.isBatchMode && !settings.HoldHeadless && !settings.HoldUntilAdvanced;

        /// <summary>
        /// A readable pause, which Continue ends early.
        ///
        /// <para>Held even under reduced motion, and shortened to nothing in batchmode. The first is
        /// because the preference is about movement rather than about being given less time to read;
        /// the second is because an automated season must not spend ten seconds on a title card it
        /// cannot see.</para>
        /// </summary>
        private IEnumerator Hold(float seconds)
        {
            if (Unwatched) yield break;
            float elapsed = 0f;
            while (settings.HoldUntilAdvanced || elapsed < seconds)
            {
                if (TakeAdvance()) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        /// <summary>A pause Continue does not end: the half-beats between the steps of a reveal.</summary>
        private IEnumerator Pause(float seconds)
        {
            if (Unwatched || Motionless) yield break;
            float elapsed = 0f;
            while (elapsed < seconds) { elapsed += Time.unscaledDeltaTime; yield return null; }
        }

        /// <summary>
        /// A decoration over time: <paramref name="apply"/> is given 0 to 1, after a delay, for as
        /// long as <paramref name="owner"/> exists. Under reduced motion it jumps straight to 1.
        /// Decorations never hold anything up; the holds are the sequence's own.
        /// </summary>
        private void Animate(UnityEngine.Object owner, float delay, float seconds, Action<float> apply, Func<float, float> ease = null)
        {
            if (owner == null) return;
            if (Motionless) { apply(1f); return; }
            apply(0f);
            StartCoroutine(Tween(owner, delay, seconds, apply, ease ?? Smooth));
        }

        private static IEnumerator Tween(UnityEngine.Object owner, float delay, float seconds, Action<float> apply, Func<float, float> ease)
        {
            while (delay > 0f && owner != null) { delay -= Time.unscaledDeltaTime; yield return null; }
            float elapsed = 0f;
            while (owner != null && elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                apply(ease(Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, seconds))));
                yield return null;
            }
            if (owner != null) apply(1f);
        }

        /// <summary>A loop that runs while <paramref name="owner"/> exists, given the time since it started. Nothing under reduced motion.</summary>
        private void Loop(UnityEngine.Object owner, Action<float> apply)
        {
            if (owner == null || Motionless) return;
            StartCoroutine(Looping(owner, apply));
        }

        private static IEnumerator Looping(UnityEngine.Object owner, Action<float> apply)
        {
            float time = 0f;
            while (owner != null) { apply(time); time += Time.unscaledDeltaTime; yield return null; }
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        /// <summary>The reference build's title ease, a fast start that settles: its cubic-bezier(0.16, 1, 0.3, 1).</summary>
        private static float Settle(float t) => t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);

        /// <summary>
        /// The reference build's "easeOut", near enough: quick off the mark and slowing into place,
        /// which its lower third slides and scales on.
        /// </summary>
        private static float OutEase(float t) => 1f - (1f - t) * (1f - t);

        /// <summary>Straight through: for what counts rather than moves - letters typed, a flash's envelope.</summary>
        private static float Straight(float t) => t;

        // ---------------------------------------------------------------- pieces

        private void Clear()
        {
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            stage = null;
        }

        /// <summary>
        /// The ground a beat stands on. An opaque one is a title card: the pack's night ground under
        /// a vignette, the ground the menu and the cast screen stand on. A clear one lets the house be
        /// the content.
        /// </summary>
        private void Scrim(Color colour)
        {
            var scrim = HudPrimitives.Fill("Scrim", transform, colour, 1);
            Stretch(scrim);
            var image = scrim.GetComponent<Image>();
            image.raycastTarget = colour.a > 0f;
            if (colour.a <= 0f) image.enabled = false;
            if (colour.a >= 0.9f && colour != Color.black)
            {
                var night = UiTheme.Pack(PackArt.BackgroundNavy);
                if (night != null)
                {
                    image.sprite = night; image.type = Image.Type.Simple;
                    image.color = new Color(1f, 1f, 1f, colour.a);
                }
                HudPrimitives.Vignette(scrim);
            }

            stage = new GameObject("Stage", typeof(RectTransform)).GetComponent<RectTransform>();
            stage.SetParent(transform, false);
            Stretch(stage);
        }

        /// <summary>
        /// The whole frame as one button: a click anywhere, Enter or the pad's South moves the show
        /// on. Built before everything else on the beat so it is the control the keyboard lands on -
        /// with the skip control first, Enter would have skipped the opening.
        /// </summary>
        private void ContinueControl()
        {
            var whole = HudPrimitives.Fill(ContinueCaption, stage, new Color(0f, 0f, 0f, 0f), 1);
            Stretch(whole);
            var image = whole.GetComponent<Image>();
            image.raycastTarget = true;
            var button = whole.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(Advance);

            // The hint on the skip pill's row, bottom-left, and on its ground: centred at the foot of
            // the frame it stood inside the lit doorway of every door frame, muted type on the door's
            // light (UI-UX-PASS-PLAN S0, sweep-show 5). The ground is a sibling of the words.
            var hint = new GameObject("Hint", typeof(RectTransform)).GetComponent<RectTransform>();
            hint.SetParent(whole, false);
            hint.anchorMin = hint.anchorMax = new Vector2(0f, 0f);
            hint.pivot = new Vector2(0f, 0f);
            hint.sizeDelta = new Vector2(PillWidth, PillHeight);
            hint.anchoredPosition = new Vector2(PillMargin, PillMargin);
            Ground(hint, 18);
            var label = HudPrimitives.Label("Label", hint, 14f, UiTheme.Paper, TextAlignmentOptions.Left);
            label.text = Localisation.Text(ContinueCaption);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.anchorMin = new Vector2(0f, 0f);
            label.rectTransform.anchorMax = new Vector2(0.62f, 1f);
            label.rectTransform.offsetMin = new Vector2(18f, 0f);
            label.rectTransform.offsetMax = new Vector2(-8f, 0f);
            KeyChip(hint, "Space", new Vector2(0.62f, 0.5f), 64f);
        }

        /// <summary>
        /// The skip pill's ground under a hint laid over the house: the raised fill with the panel
        /// edge, behind every sibling, named <see cref="GroundName"/>. Opaque where the pill is .98:
        /// in linear light two per cent of the lit doorway lifts a fill this dark visibly.
        /// </summary>
        private static RectTransform Ground(RectTransform parent, int radius)
        {
            var fill = UiTheme.SurfaceRaised;
            var ground = HudPrimitives.Fill(GroundName, parent, new Color(fill.r, fill.g, fill.b, 1f), radius);
            Stretch(ground);
            ground.SetAsFirstSibling();
            UiTheme.AddBorder(ground, radius, UiTheme.Outline);
            return ground;
        }

        /// <summary>A key cap beside a control, naming its key. Never inside the control's caption.</summary>
        private static void KeyChip(RectTransform parent, string key, Vector2 anchor, float width)
        {
            var chip = HudPrimitives.Fill("Key", parent, new Color(1f, 1f, 1f, 0.08f), 6);
            chip.anchorMin = chip.anchorMax = anchor;
            chip.pivot = new Vector2(0f, 0.5f);
            chip.sizeDelta = new Vector2(width, 24f);
            UiTheme.AddBorder(chip, 6, new Color(1f, 1f, 1f, 0.25f));
            chip.GetComponent<Image>().raycastTarget = false;
            var word = HudPrimitives.Label("Word", chip, 12f, UiTheme.Paper, TextAlignmentOptions.Center);
            word.text = Localisation.Text(key);
            Stretch(word.rectTransform);
        }

        /// <summary>The skip control, on every beat of the show, in the same place every time.</summary>
        private void Skipper()
        {
            var pill = HudPrimitives.Fill(SkipCaption, stage, UiTheme.SurfaceRaised, 18);
            pill.anchorMin = new Vector2(1f, 0f);
            pill.anchorMax = new Vector2(1f, 0f);
            pill.pivot = new Vector2(1f, 0f);
            pill.sizeDelta = new Vector2(PillWidth, PillHeight);
            pill.anchoredPosition = new Vector2(-PillMargin, PillMargin);
            UiTheme.AddBorder(pill, 18, UiTheme.Outline);

            var image = pill.GetComponent<Image>();
            image.raycastTarget = true;

            var label = HudPrimitives.Label("Label", pill, 14f, UiTheme.Paper, TextAlignmentOptions.Center);
            label.text = SkipCaption;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(8f, 0f);
            label.rectTransform.offsetMax = new Vector2(-8f, 0f);

            var button = pill.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(PressSkip);
            Focusable(button, pill, UiTheme.Emphasis.Resting);

            // Escape does the same, and the key is named beside the pill rather than in its caption.
            var keys = new GameObject("Keys", typeof(RectTransform)).GetComponent<RectTransform>();
            keys.SetParent(stage, false);
            keys.anchorMin = keys.anchorMax = new Vector2(1f, 0f);
            keys.pivot = new Vector2(1f, 0f);
            keys.sizeDelta = new Vector2(52f, PillHeight);
            keys.anchoredPosition = new Vector2(-(PillMargin + PillWidth + 10f), PillMargin);
            KeyChip(keys, "Esc", new Vector2(0f, 0.5f), 44f);
        }

        /// <summary>A line of text, centred on <paramref name="position"/>, in a box a third taller than its type.</summary>
        private TMP_Text Text(Transform parent, string name, string value, float size, Color colour, float width,
            Vector2 position, UiTheme.Weight weight = UiTheme.Weight.Regular, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var label = HudPrimitives.Label(name, parent, size, colour, alignment);
            label.text = value;
            var font = UiTheme.Font(weight);
            if (font != null) label.font = font;
            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            // Inter draws nothing into a box under about 1.21 times its size, while .text still
            // reads right; a third taller keeps every line on screen.
            rect.sizeDelta = new Vector2(width, Mathf.Ceil(size * 1.34f));
            rect.anchoredPosition = position;
            return label;
        }

        /// <summary>The title blues, lit from above: the wordmark as the menu and the HUD draw it.</summary>
        private static void TitleGradient(TMP_Text label)
        {
            label.enableVertexGradient = true;
            label.colorGradient = new VertexGradient(UiTheme.Hex("6CC0FF"), UiTheme.Hex("6CC0FF"), UiTheme.Hex("3A86FF"), UiTheme.Hex("3A86FF"));
            label.color = Color.white;
        }

        private static RectTransform Box(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        /// <summary>
        /// A group to fade a piece by. A piece with controls on it keeps them live: a group made
        /// non-interactable greys out every control built under it and leaves them greyed.
        /// </summary>
        private static CanvasGroup Fader(RectTransform rect, bool interactive = false)
        {
            // Not ??: a missing component is Unity's fake null in the editor, which ?? takes for a real one.
            var fader = rect.GetComponent<CanvasGroup>();
            if (fader == null) fader = rect.gameObject.AddComponent<CanvasGroup>();
            fader.blocksRaycasts = interactive;
            fader.interactable = interactive;
            return fader;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>The house, player first and then everyone else in the season's order, as the reference build introduces them.</summary>
        private List<ContestantState> PlayerFirst() =>
            (settings.Cast ?? new List<ContestantState>()).Where(person => person != null)
                .OrderBy(person => person.isPlayer ? 0 : 1).ToList();

        private IEnumerable<ContestantState> Guests() =>
            (settings?.Cast ?? new List<ContestantState>()).Where(person => person != null && !person.isPlayer);

        private bool Met(string id) => settings?.Introduced != null && settings.Introduced(id);

        /// <summary>
        /// A control the keyboard can be seen on: brighter while hovered or focused, as the HUD's own
        /// controls are, and its edge a step up. Unity's default darkened a raised fill by four per
        /// cent, which nobody can see.
        /// </summary>
        private static void Focusable(Button button, RectTransform panel, UiTheme.Emphasis level)
        {
            var colours = button.colors;
            colours.highlightedColor = new Color(1.3f, 1.3f, 1.3f);
            colours.selectedColor = colours.highlightedColor;
            colours.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            button.colors = colours;
            if (panel != null) HudEmphasis.Promote(panel, level);
        }

        private static void Select(GameObject target)
        {
            if (EventSystem.current != null && target != null) EventSystem.current.SetSelectedGameObject(target);
        }
    }
}
