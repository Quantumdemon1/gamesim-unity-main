using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.House;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The show's beats: the intro (loading, the title, each houseguest's reveal, the house
    /// together), the house entry and the walk-in. Timings, copy and flourishes are the reference
    /// build's wherever it has them - the GitHub web game's IntroSequence.tsx, HouseEntrySequence.tsx
    /// and HouseWalkInSequence.tsx under D:/gamesim-web/src/components/game-phases - and each
    /// departure says why beside it.
    /// </summary>
    public sealed partial class OpeningSequence
    {
        private static readonly Color Night = new Color(0.01f, 0.02f, 0.03f, 1f);
        private static readonly Color Transparent = new Color(0f, 0f, 0f, 0f);

        /// <summary>
        /// How long the loading gate waits for the bodies before the show starts without them: the
        /// reference build's desktop cap on its preload (IntroSequence.tsx:297-303).
        /// </summary>
        public const float LoadingCap = 20f;

        /// <summary>When the title's "Season N" starts typing, and the time each letter takes (IntroSequence.tsx:443-463).</summary>
        public const float SeasonAt = 1.8f;
        public const float SeasonLetterSeconds = 0.06f;

        /// <summary>When "{N} Houseguests. 1 Winner." rises in under it (IntroSequence.tsx:464-471).</summary>
        public const float SubtitleAt = 2.4f;

        /// <summary>
        /// The lower third's stagger (IntroSequence.tsx:561-623): the whole fades in, then the name,
        /// the face, the details and the count arrive one after another.
        /// </summary>
        public const float LowerThirdAt = 1.5f;
        public const float NameAt = 1.6f;
        public const float PortraitAt = 1.8f;
        public const float DetailsAt = 1.9f;
        public const float CounterAt = 2.2f;

        /// <summary>The white flash as a door gives, and how white it gets (IntroSequence.tsx:546-558).</summary>
        public const float FlashSeconds = 0.15f;
        public const float FlashPeak = 0.8f;

        /// <summary>How many pieces of confetti the house together gets (IntroSequence.tsx:366-376).</summary>
        public const int ConfettiCount = 120;

        /// <summary>The walk-in's keys by what they frame: pulled back from the doorway, the doorway, over the bedrooms, over the whole house.</summary>
        public const int WalkInPushFrom = 0, WalkInDoorway = 1, WalkInRise = 2, WalkInOver = 3;

        private const float TitleHold = 3.5f;           // its title card, desktop (IntroSequence.tsx:335)
        private const float CardReveal = 3.0f;          // its reveal without a body (IntroSequence.tsx:343-353)
        private const float GroupHold = 3.5f;           // its group shot, desktop (IntroSequence.tsx:368)
        private const float TransitionSeconds = 1.2f;   // a 0.8 s fade and the rest held black (IntroSequence.tsx:381-386)
        private const float HouseEntryHold = 6.0f;      // its house entry, desktop (HouseEntrySequence.tsx:17)
        private const float HandOffHold = 2.4f;
        private const float DoorWaitCap = 6.0f;
        private const float MarkWaitCap = 4.0f;
        private const float NameX = 160f;
        private const float WalkInCaptionFloor = 128f;  // its caption's pb-32 (HouseWalkInSequence.tsx:760)

        // The confetti's physics, canvas-confetti's defaults turned from frames at 60 a second into
        // seconds: its speed is lost at 0.9 a frame, and its gravity is a steady 3 px a frame down.
        private const float ConfettiSpread = 80f;
        private const float ConfettiOriginFromBottom = 0.4f;
        private const float ConfettiDecay = 6.3216f;    // -ln(0.9) * 60
        private const float ConfettiFall = 180f;
        private const float ConfettiLife = 200f / 60f;  // its 200 ticks, each piece fading linearly over them

        /// <summary>
        /// The reference build's golds, and the house's two blues twice among them: gold is the
        /// colour of power in this house, so it may join a celebration but not own it.
        /// </summary>
        private static readonly Color[] ConfettiColours =
        {
            UiTheme.Hex("FBBF24"), UiTheme.Hex("F59E0B"), UiTheme.Hex("D97706"), Color.white, UiTheme.Hex("FEF3C7"),
            UiTheme.Heading, UiTheme.Accent, UiTheme.Heading, UiTheme.Accent,
        };

        private struct ConfettiPiece
        {
            public RectTransform rect;
            public Image image;
            public Color colour;
            public float angle, speed, spin, tilt, wobble, wobbleRate, flipRate;
        }

        /// <summary>
        /// How long a reveal waits before the door opens, and how long its houseguest stands on the
        /// mark, for a house of <paramref name="count"/> reveals. The reference build's 2.8 s walk up
        /// its tunnel (IntroTunnelScene.tsx:145-152) and a second on the mark up to eight people,
        /// shortened towards 2.0 s and 0.4 s at sixteen so a big house does not spend a minute and a
        /// half at the door.
        /// </summary>
        public static void RevealTiming(int count, out float doorAt, out float markHold)
        {
            float k = Mathf.Clamp01((count - 8) / 8f);
            doorAt = 2.8f - 0.8f * k;
            markHold = 1.0f - 0.6f * k;
        }

        /// <summary>
        /// How many letters of the title's "Season N" are up <paramref name="elapsed"/> seconds into
        /// the title card: none before 1.8 s, then letter i at 1.8 + 0.06 i - the reference build's
        /// typewriter, one letter per stagger step.
        /// </summary>
        public static int SeasonLettersShown(float elapsed, int length)
        {
            if (length <= 0 || elapsed < SeasonAt) return 0;
            return Mathf.Min(length, Mathf.FloorToInt((elapsed - SeasonAt) / SeasonLetterSeconds + 1e-4f) + 1);
        }

        /// <summary>
        /// The door flash's opacity <paramref name="t"/> of the way through its 0.15 s: up to 0.8 by
        /// 30% of the way and back to nothing at the end - the reference's keyframes [0, 0.8, 0] at
        /// times [0, 0.3, 1].
        /// </summary>
        public static float FlashAlpha(float t)
        {
            t = Mathf.Clamp01(t);
            return FlashPeak * (t < 0.3f ? t / 0.3f : (1f - t) / 0.7f);
        }

        /// <summary>
        /// When the walk-in's camera takes each key after the doorway, in seconds from the start: the
        /// push-in to the doorway runs from the start for as long as its key says, the rise follows
        /// it, the crane follows the rise, and the camera has settled once the crane's own time is up.
        /// With the director's keys that is the reference build's schedule
        /// (HouseWalkInSequence.tsx:81-135): pushed in by 1 s, risen by 4 s, over the top by 11 s.
        /// All zero for a set of keys too short to crane.
        /// </summary>
        public static void WalkInSchedule(IReadOnlyList<HouseCameraRig.Shot> keys, out float riseAt, out float craneAt, out float settledAt)
        {
            riseAt = craneAt = settledAt = 0f;
            if (keys == null || keys.Count <= WalkInOver) return;
            riseAt = Mathf.Max(0.5f, keys[WalkInDoorway].Seconds);
            craneAt = riseAt + Mathf.Max(0.5f, keys[WalkInRise].Seconds);
            settledAt = craneAt + Mathf.Max(0.5f, keys[WalkInOver].Seconds);
        }

        /// <summary>A beat's frame: its ground, the whole-frame Continue first, then the skip control.</summary>
        private void Section(Color ground)
        {
            Clear();
            Scrim(ground);
            ContinueControl();
            Skipper();
        }

        // ---------------------------------------------------------------- the intro

        /// <summary>
        /// The reference build's intro: a title card, then one reveal per houseguest - the player
        /// first - then the whole house on one card, then a fade to black. With a stage, a reveal is
        /// a houseguest walking through the front door; without one - reduced motion, a headless
        /// run, a house that could not be staged - it is their portrait on a card, which is the
        /// reference build's own reveal for a houseguest with no body.
        /// </summary>
        private IEnumerator Intro()
        {
            Section(Night);
            var world = Motionless ? null : settings.Stage;
            if (world != null) yield return LoadingGate(world);
            advancePending = false;

            yield return TitleCard();

            var order = PlayerFirst();
            bool staged = world != null && world.Placed;
            if (staged)
            {
                // Under the title still: everybody has to be bound to the house again before they
                // can be walked anywhere, and a house that never binds is shown on cards instead.
                float waited = 0f;
                while (!world.Ready && !world.Failed && waited < 3f) { waited += Time.unscaledDeltaTime; yield return null; }
                if (!world.Ready)
                {
                    world.StrikeSet();
                    world.RestoreHome();
                    staged = false;
                }
            }

            if (staged && order.Count > 0) yield return DoorReveals(world, order);
            else
                for (int i = 0; i < order.Count; i++) yield return CardReveal2D(order[i], i, order.Count);
            CurrentGuestId = null;

            if (order.Count > 0) yield return GroupBeat(world, order);
            yield return FadeToBlack();
        }

        /// <summary>
        /// Waits for the bodies, with the theme held until they are built, as the reference build
        /// starts its theme only once its assets are ready (IntroSequence.tsx:323-326). Its caption
        /// is the reference's "Preparing the show..." in capitals, up from the first frame; its bar
        /// counts bodies that have finished assembling, where the reference faked a curve that
        /// stopped at 85%. It gives up after the reference's 20 s.
        /// </summary>
        private IEnumerator LoadingGate(IStage world)
        {
            float waited = 0f;
            RectTransform gate = null;
            RectTransform fill = null;
            TMP_Text percent = null;
            MusicHeld = world.BodyReadiness < 1f;
            if (MusicHeld)
            {
                gate = Box("Loading", stage, new Vector2(600f, 120f), Vector2.zero);
                var gateFader = Fader(gate);
                Animate(gate, 0f, 0.5f, t =>
                {
                    gateFader.alpha = t;
                    gate.anchoredPosition = new Vector2(0f, -10f * (1f - t));
                });
                var words = Text(gate, "Caption", Localisation.Text("Preparing the show..."), 18f, UiTheme.Muted, 600f, new Vector2(0f, 30f));
                words.fontStyle = FontStyles.UpperCase;
                words.characterSpacing = 10f;
                var track = HudPrimitives.Fill("Track", gate, new Color(1f, 1f, 1f, 0.1f), 2);
                track.anchorMin = track.anchorMax = new Vector2(0.5f, 0.5f);
                track.sizeDelta = new Vector2(256f, 6f);
                track.anchoredPosition = Vector2.zero;
                fill = HudPrimitives.Fill("Bar", track, UiTheme.Heading, 2);
                fill.anchorMin = new Vector2(0f, 0f); fill.anchorMax = new Vector2(0f, 1f);
                fill.pivot = new Vector2(0f, 0.5f);
                fill.sizeDelta = new Vector2(0f, 0f);
                percent = Text(gate, "Percent", "0%", 14f, UiTheme.Muted, 200f, new Vector2(0f, -28f));
            }
            while (world.BodyReadiness < 1f && waited < LoadingCap)
            {
                if (fill != null)
                {
                    float share = Mathf.Clamp01(world.BodyReadiness);
                    fill.sizeDelta = new Vector2(256f * share, 0f);
                    percent.text = Localisation.Format("{0}%", Mathf.RoundToInt(share * 100f));
                }
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            MusicHeld = false;
            if (gate != null) Destroy(gate.gameObject);
            // Placed only once every body is built: placing a body mid-assembly lost it its binding.
            if (world.BodyReadiness >= 1f) world.TryPlace();
        }

        /// <summary>
        /// The title card, the reference build's (IntroSequence.tsx:422-471): the eye
        /// (IntroBBEyeLogo.tsx), the wordmark, the season typed out letter by letter, and how many
        /// are playing for one win rising in under it. The wordmark is the show's name as a lockup,
        /// "GAMESIM" over a letterspaced "THE HOUSE" - Gamesim: The House - landing as one piece. All
        /// of it in the title blues rather than the reference's amber, because gold means power in
        /// this house and nobody has any yet. Under reduced motion every line is simply there.
        /// </summary>
        private IEnumerator TitleCard()
        {
            var card = Box("Title card", stage, new Vector2(1600f, 800f), Vector2.zero);
            var fader = Fader(card);
            Animate(card, 0.3f, 0.8f, t => fader.alpha = t);

            EyeEmblem(card, new Vector2(0f, 190f));

            var wordmark = Box("Wordmark", card, new Vector2(1500f, 220f), new Vector2(0f, 4f));
            var wordmarkFader = Fader(wordmark);
            Animate(wordmark, 0.3f, 1.2f, t =>
            {
                wordmark.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, t);
                wordmarkFader.alpha = t;
            }, Settle);

            var title = Text(wordmark, "Title", "GAMESIM", 96f, Color.white, 1500f, new Vector2(0f, 36f), UiTheme.Weight.Bold);
            TitleGradient(title);
            title.characterSpacing = 12f;

            var house = Text(wordmark, "Brand line", Localisation.Text("THE HOUSE"), 34f, UiTheme.Accent, 1500f,
                new Vector2(0f, -36f), UiTheme.Weight.SemiBold);
            house.characterSpacing = 80f;

            // "Season N" from the career record: the reference counted it off the week and read
            // "Season 2" on a new game's first night.
            var season = Text(card, "Season", Localisation.Format("Season {0}", Mathf.Max(1, settings.SeasonNumber)), 20f,
                new Color(UiTheme.Heading.r, UiTheme.Heading.g, UiTheme.Heading.b, 0.8f), 1500f, new Vector2(0f, -92f));
            season.fontStyle = FontStyles.UpperCase;
            season.characterSpacing = 40f;
            int letters = season.text.Length;
            float typing = SeasonAt + SeasonLetterSeconds * letters;
            Animate(season, 0f, typing, t => season.maxVisibleCharacters = SeasonLettersShown(t * typing, letters), Straight);

            int count = settings.Cast?.Count ?? 0;
            string line = count > 0
                ? Localisation.Format("{0} Houseguests. 1 Winner.", count)
                : Localisation.Text("A NEW SEASON BEGINS");
            var subtitle = Text(card, "Subtitle", line, 24f, UiTheme.Paper, 1500f, new Vector2(0f, -142f));
            subtitle.fontStyle = FontStyles.UpperCase;
            subtitle.characterSpacing = 30f;
            Animate(subtitle, SubtitleAt, 0.8f, t =>
            {
                subtitle.alpha = 0.85f * t;
                subtitle.rectTransform.anchoredPosition = new Vector2(0f, -142f - 20f * (1f - t));
            }, OutEase);

            yield return Hold(TitleHold);
        }

        /// <summary>
        /// The eye: a glow that breathes, two rings, the eye itself and a line scanning down it - the
        /// reference build's IntroBBEyeLogo, on its own loop timings, drawn from the house's icon set
        /// in the title blues.
        /// </summary>
        private void EyeEmblem(RectTransform parent, Vector2 position)
        {
            var root = Box("Eye emblem", parent, new Vector2(160f, 160f), position);
            Animate(root, 0.2f, 1.0f, t => root.localScale = Vector3.one * t, Settle);

            var glowSprite = UiTheme.Pack(PackArt.GlowCyan);
            if (glowSprite != null)
            {
                var glow = Picture("Glow", root, glowSprite, new Color(1f, 1f, 1f, 0.3f), new Vector2(260f, 260f));
                Loop(glow, time => glow.color = new Color(1f, 1f, 1f, 0.3f + 0.1f * Mathf.Sin(time * Mathf.PI * 2f / 2.5f)));
            }

            var outer = Picture("Outer ring", root, ThinRing(), UiTheme.Hex("6CC0FF99"), new Vector2(140f, 140f));
            Loop(outer, time => outer.rectTransform.sizeDelta = Vector2.one * (142f + 2f * Mathf.Sin(time * Mathf.PI * 2f / 3f)));
            var middle = Picture("Mid ring", root, ThinRing(), UiTheme.Hex("3A86FF66"), new Vector2(102f, 102f));
            Loop(middle, time => middle.rectTransform.sizeDelta = Vector2.one * (101f - 1f * Mathf.Sin(time * Mathf.PI * 2f / 2.5f)));

            var eyeSprite = UiTheme.Icon("eye");
            if (eyeSprite != null)
            {
                var eye = Picture("Eye", root, eyeSprite, UiTheme.Hex("9AD4FF"), new Vector2(96f, 96f));
                eye.preserveAspect = true;
                Loop(eye, time => eye.rectTransform.localScale = Vector3.one * (1.05f + 0.05f * Mathf.Sin(time * Mathf.PI * 2f / 2f)));
            }
            else
            {
                var iris = HudPrimitives.Disc("Iris", root, UiTheme.Hex("4DA8FF"));
                iris.sizeDelta = new Vector2(58f, 58f);
                var pupil = HudPrimitives.Disc("Pupil", iris, UiTheme.Ink);
                pupil.sizeDelta = new Vector2(26f, 26f);
            }

            var window = Box("Scan", root, new Vector2(140f, 140f), Vector2.zero);
            window.gameObject.AddComponent<RectMask2D>();
            var scan = HudPrimitives.Fill("Line", window, new Color(1f, 1f, 1f, 0.5f), 1);
            scan.anchorMin = scan.anchorMax = new Vector2(0.5f, 0.5f);
            scan.sizeDelta = new Vector2(140f, 2f);
            scan.GetComponent<Image>().raycastTarget = false;
            if (Motionless) scan.gameObject.SetActive(false);
            Loop(scan, time => scan.anchoredPosition = new Vector2(0f, Mathf.PingPong(time * (140f / 1.5f), 140f) - 70f));
        }

        /// <summary>
        /// Who someone is, under them: their face, their name, their age, what they do and where
        /// they are from, and which of the house they are. The reference build's lower third on its
        /// own stagger (IntroSequence.tsx:561-623): the whole fades in at 1.5 s without moving, the
        /// name slides in from the left at 1.6 s, the face grows from seven tenths at 1.8 s, the
        /// details slide in from the right at 1.9 s and the count fades up at 2.2 s, in capitals.
        /// The name is just the name, as the reference shows it: the "(You)" the HUD's lists add is
        /// for finding yourself in a list, and a premiere introduces one person at a time.
        /// </summary>
        private RectTransform LowerThird(ContestantState person, int index, int count)
        {
            var root = new GameObject("Lower third", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(stage, false);
            root.anchorMin = root.anchorMax = new Vector2(0f, 0f);
            root.pivot = new Vector2(0f, 0f);
            root.sizeDelta = new Vector2(1100f, 200f);
            root.anchoredPosition = new Vector2(80f, 110f);
            var fader = Fader(root);
            Animate(root, LowerThirdAt, 0.3f, t => fader.alpha = t);

            var face = HudPrimitives.Portrait(root, CharacterPortraits.Get(person), UiTheme.Heading, 128f, 3f, false, person);
            face.name = "Portrait";
            face.anchorMin = face.anchorMax = new Vector2(0f, 0.5f);
            face.pivot = new Vector2(0.5f, 0.5f);
            face.anchoredPosition = new Vector2(67f, 0f);
            var faceFader = Fader(face);
            Animate(face, PortraitAt, 0.5f, t =>
            {
                face.localScale = Vector3.one * Mathf.Lerp(0.7f, 1f, t);
                faceFader.alpha = t;
            }, OutEase);

            var lines = new List<string>();
            if (person.age > 0) lines.Add(Localisation.Format("Age {0}", person.age));
            if (!string.IsNullOrWhiteSpace(person.occupation)) lines.Add(person.occupation);
            if (!string.IsNullOrWhiteSpace(person.hometown)) lines.Add(person.hometown);

            float top = 88f + lines.Count * 15f;
            var name = LeftText(root, "Name", person.name, 48f, UiTheme.Paper, new Vector2(NameX, top), UiTheme.Weight.Bold);
            name.textWrappingMode = TextWrappingModes.NoWrap;
            Animate(name, NameAt, 0.5f, t =>
            {
                name.alpha = t;
                name.rectTransform.anchoredPosition = new Vector2(NameX - 40f * (1f - t), top);
            }, OutEase);

            // The details slide as one block, as the reference's do.
            var details = new GameObject("Details", typeof(RectTransform)).GetComponent<RectTransform>();
            details.SetParent(root, false);
            Stretch(details);
            var detailsFader = Fader(details);
            float y = top - 50f;
            foreach (var line in lines)
            {
                LeftText(details, "Line", line, 22f, UiTheme.Heading, new Vector2(NameX, y), UiTheme.Weight.Medium);
                y -= 30f;
            }
            Animate(details, DetailsAt, 0.5f, t =>
            {
                detailsFader.alpha = t;
                details.anchoredPosition = new Vector2(30f * (1f - t), 0f);
            }, OutEase);

            // The count stands at the other side, above the skip control, the way the reference
            // build puts it in the bottom corner - on a chip, in paper: muted type laid on the dark
            // yard was all but invisible over the shut door (UI-UX-PASS-PLAN S0, sweep-show 21).
            var chip = new GameObject("Counter chip", typeof(RectTransform)).GetComponent<RectTransform>();
            chip.SetParent(stage, false);
            chip.anchorMin = chip.anchorMax = new Vector2(1f, 0f);
            chip.pivot = new Vector2(1f, 0f);
            chip.anchoredPosition = new Vector2(-PillMargin, 100f);
            Ground(chip, 15);
            var counter = HudPrimitives.Label("Counter", chip, 18f, UiTheme.Paper, TextAlignmentOptions.Center);
            counter.text = Localisation.Format("{0} of {1}", index + 1, count);
            counter.fontStyle = FontStyles.UpperCase;
            counter.characterSpacing = 10f;
            counter.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(counter.rectTransform);
            counter.rectTransform.offsetMin = new Vector2(16f, 0f);
            counter.rectTransform.offsetMax = new Vector2(-16f, 0f);
            // As wide as its words with room to spare - the label has no wrap, and a count that
            // overran it by a unit would be cut - and never narrower than "16 OF 16"; a box well over
            // 1.3 times its type, as every box here is.
            float words = Mathf.Ceil(counter.GetPreferredValues(counter.text).x);
            chip.sizeDelta = new Vector2(Mathf.Max(132f, words + 56f), 30f);
            chip.SetParent(root, true);
            var chipFader = Fader(chip);
            Animate(chip, CounterAt, 0.4f, t => chipFader.alpha = t);
            return root;
        }

        private TMP_Text LeftText(RectTransform parent, string name, string value, float size, Color colour, Vector2 position, UiTheme.Weight weight)
        {
            var label = HudPrimitives.Label(name, parent, size, colour, TextAlignmentOptions.Left);
            label.text = value;
            var font = UiTheme.Font(weight);
            if (font != null) label.font = font;
            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(900f, Mathf.Ceil(size * 1.34f));
            rect.anchoredPosition = position;
            return label;
        }

        /// <summary>
        /// A reveal on a card: the portrait coming up out of a glow with a ring pulsing off it, and
        /// the lower third. The reference build's reveal for a houseguest with no body, held its
        /// three seconds.
        /// </summary>
        private IEnumerator CardReveal2D(ContestantState person, int index, int count)
        {
            Section(Night);
            CurrentGuestId = person.id;
            advancePending = false;

            var glowSprite = UiTheme.Pack(PackArt.GlowCyan);
            if (glowSprite != null)
            {
                var glow = Picture("Glow", stage, glowSprite, new Color(1f, 1f, 1f, 0f), new Vector2(560f, 560f));
                glow.rectTransform.anchoredPosition = new Vector2(0f, 60f);
                Animate(glow, 0f, 2f, t => glow.color = new Color(1f, 1f, 1f, t < 0.5f ? t * 2f : 1f - 0.4f * (t - 0.5f) * 2f));
            }

            var pulse = Picture("Pulse", stage, ThinRing(), UiTheme.Hex("4DA8FF99"), new Vector2(252f, 252f));
            pulse.rectTransform.anchoredPosition = new Vector2(0f, 60f);
            if (Motionless) pulse.enabled = false;
            Loop(pulse, time =>
            {
                float t = time % 1.5f / 1.5f;
                pulse.rectTransform.localScale = Vector3.one * (1f + 0.15f * t);
                pulse.color = new Color(0.3f, 0.66f, 1f, 0.6f * (1f - t));
            });

            var face = HudPrimitives.Portrait(stage, CharacterPortraits.Get(person), UiTheme.Heading, 240f, 3f, false, person);
            face.name = "Portrait";
            face.anchorMin = face.anchorMax = new Vector2(0.5f, 0.5f);
            face.anchoredPosition = new Vector2(0f, 60f);
            Animate(face, 0f, 0.8f, t => face.localScale = Vector3.one * Mathf.Lerp(0.5f, 1f, t), Settle);

            LowerThird(person, index, count);
            yield return Hold(CardReveal);
        }

        /// <summary>
        /// Every houseguest through the front door, one at a time: the door holds shut while they
        /// walk up behind it, shudders and swings open in a white flash and a burst of light as the
        /// camera pushes in, and they walk through to a mark in front of the lens and greet it with
        /// their name coming up under them, then walk off out of shot to where the house gathers.
        /// The reference build's tunnel reveal (IntroTunnelScene.tsx, tunnel/EntranceDoor.tsx),
        /// walked for real in the yard: its walker covered a ten-metre tunnel in 2.8 s and went on
        /// out past the lens.
        /// </summary>
        private IEnumerator DoorReveals(IStage world, List<ContestantState> order)
        {
            Section(Transparent);
            var rig = settings.Rig;
            var door = world.DoorShot;
            door.Seconds = 0.01f;
            rig?.MoveTo(door);
            RevealTiming(order.Count, out float doorAt, out float markHold);
            Coroutine deckRetry = null;

            for (int i = 0; i < order.Count; i++)
            {
                var person = order[i];
                CurrentGuestId = person.id;
                advancePending = false;
                world.CloseDoor();
                if (i > 0 && rig != null) { var back = world.DoorShot; back.Seconds = 0.6f; rig.MoveTo(back); }

                float clock = 0f;
                bool advanced = false;
                var third = LowerThird(person, i, order.Count);
                // The next houseguest steps up behind the door while this one is revealed. A retry
                // still running for this one ends: their own walk to the door replaces it.
                if (deckRetry != null) StopCoroutine(deckRetry);
                deckRetry = i + 1 < order.Count ? StartCoroutine(OnDeck(world, order[i + 1].id)) : null;
                yield return Wait(0.3f, () => clock += Time.unscaledDeltaTime);
                bool walking = world.ToDoor(person.id);
                if (walking)
                {
                    while (clock < DoorWaitCap && !(world.AtDoor(person.id) && (clock >= doorAt || advanced)))
                    {
                        if (TakeAdvance()) advanced = true;
                        clock += Time.unscaledDeltaTime;
                        yield return null;
                    }
                    walking = world.AtDoor(person.id);
                }

                if (walking)
                {
                    world.OpenDoor();
                    DoorFlash();
                    if (rig != null) rig.MoveTo(world.PushInShot);
                    yield return Pause(0.2f);
                    walking = world.ThroughDoor(person.id);
                    float waited = 0f;
                    while (walking && !world.OnMark(person.id) && waited < MarkWaitCap) { waited += Time.unscaledDeltaTime; yield return null; }
                    if (walking && world.OnMark(person.id)) world.Present(person.id);
                    yield return Hold(markHold);
                }
                else
                {
                    // A houseguest who could not be walked is still introduced, over the closed door,
                    // and stays behind it: the set has no colliders, so sent off from there they would
                    // walk through it in shot. The walk-in takes them home once it is down.
                    yield return Hold(CardReveal);
                }

                FadeOutAndDrop(third, 0.3f);
                if (walking) world.SendOff(person.id);
            }
            CurrentGuestId = null;
        }

        /// <summary>
        /// The white flash as the door gives - the reference build's camera flash on each arrival
        /// (IntroSequence.tsx:546-558): the whole frame to 0.8 white and back in 0.15 s, over the
        /// house and the name coming up. Nothing under reduced motion: a frame going white is the
        /// kind of thing the preference asks to be spared, and the open door says the same.
        /// </summary>
        private void DoorFlash()
        {
            if (Motionless || stage == null) return;
            var flash = HudPrimitives.Fill("Flash", stage, new Color(1f, 1f, 1f, 0f), 1);
            Stretch(flash);
            flash.SetAsLastSibling();
            var image = flash.GetComponent<Image>();
            image.raycastTarget = false;
            StartCoroutine(Flashing(flash, image));
        }

        /// <summary>The flash on its own clock, and then gone: unscaled, so a paused season cannot strand a white frame.</summary>
        private static IEnumerator Flashing(RectTransform flash, Image image)
        {
            float elapsed = 0f;
            while (flash != null && elapsed < FlashSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                image.color = new Color(1f, 1f, 1f, FlashAlpha(elapsed / FlashSeconds));
                yield return null;
            }
            if (flash != null) Destroy(flash.gameObject);
        }

        /// <summary>Sends someone waiting up behind the door, trying again until they can go or it is too late to matter.</summary>
        private static IEnumerator OnDeck(IStage world, string id)
        {
            float waited = 0f;
            while (waited < DoorWaitCap && !world.OnDeck(id))
            {
                float step = 0.25f;
                while (step > 0f) { step -= Time.unscaledDeltaTime; waited += Time.unscaledDeltaTime; yield return null; }
            }
        }

        /// <summary>A short wait Continue ends, reporting each frame it spends.</summary>
        private IEnumerator Wait(float seconds, System.Action tick)
        {
            if (Unwatched) yield break;
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                if (TakeAdvance()) yield break;
                elapsed += Time.unscaledDeltaTime;
                tick?.Invoke();
                yield return null;
            }
        }

        private void FadeOutAndDrop(RectTransform rect, float seconds)
        {
            if (rect == null) return;
            if (Motionless) { Destroy(rect.gameObject); return; }
            var fader = Fader(rect);
            float from = fader.alpha;
            Animate(rect, 0f, seconds, t => fader.alpha = from * (1f - t));
            Destroy(rect.gameObject, seconds + 0.05f);
        }

        /// <summary>
        /// The whole house on one card, their faces popping in one after another, and the reference
        /// build's welcome under them in its light capitals, rising in - "Welcome to Gamesim: The
        /// House" where the reference welcomed its viewers to the Big Brother house - with its
        /// confetti thrown over it all. The front door comes down behind it, out of sight.
        /// </summary>
        private IEnumerator GroupBeat(IStage world, List<ContestantState> order)
        {
            Section(Night);
            if (world != null && world.Placed) world.StrikeSet();

            const int columns = 6;
            const float side = 80f, gap = 16f;
            int rows = Mathf.CeilToInt(order.Count / (float)columns);
            float height = rows * side + (rows - 1) * gap;
            var grid = Box("Group", stage, new Vector2(columns * side + (columns - 1) * gap, height), new Vector2(0f, 40f));
            Animate(grid, 0f, 0.8f, t => grid.localScale = Vector3.one * Mathf.Lerp(1.1f, 1f, t), Settle);

            for (int i = 0; i < order.Count; i++)
            {
                int row = i / columns, column = i % columns;
                int inRow = Mathf.Min(columns, order.Count - row * columns);
                float x = (column - (inRow - 1) / 2f) * (side + gap);
                float y = height / 2f - side / 2f - row * (side + gap);
                var face = HudPrimitives.Portrait(grid, CharacterPortraits.Get(order[i]), UiTheme.Heading, side, 2f, false, order[i]);
                face.name = "Portrait";
                face.anchorMin = face.anchorMax = new Vector2(0.5f, 0.5f);
                face.anchoredPosition = new Vector2(x, y);
                Animate(face, 0.08f * i, 0.4f, t => face.localScale = Vector3.one * t, Settle);
            }

            float welcomeY = 40f - height / 2f - 60f;
            var welcome = Text(stage, "Welcome", Localisation.Text("Welcome to Gamesim: The House"), 30f, UiTheme.Accent, 1500f,
                new Vector2(0f, welcomeY), UiTheme.Weight.Regular);
            welcome.fontStyle = FontStyles.UpperCase;
            welcome.characterSpacing = 10f;
            Animate(welcome, 0.6f, 0.6f, t =>
            {
                welcome.alpha = 0.9f * t;
                welcome.rectTransform.anchoredPosition = new Vector2(0f, welcomeY - 20f * (1f - t));
            }, OutEase);

            GroupConfetti();

            yield return Hold(GroupHold);
        }

        /// <summary>
        /// The reference build's burst over the house together (IntroSequence.tsx:366-376, on
        /// canvas-confetti's defaults): 120 pieces thrown up across an 80° fan from 60% of the way
        /// down the screen, each losing its speed as it climbs and then drifting down, spinning and
        /// turning over, fading out over about three and a third seconds - canvas-confetti's 200 ticks
        /// at 60 a second, most of the card's 3.5 s hold - and then gone. Its own stream seeded
        /// from the season, never UnityEngine.Random and never the season's generator, so two plays
        /// of one season throw the same confetti. None under reduced motion, as the reference's
        /// disableForReducedMotion; a skip or the next beat clears whatever is still in the air.
        /// </summary>
        private void GroupConfetti()
        {
            if (Motionless || stage == null) return;
            var burst = new GameObject("Confetti", typeof(RectTransform)).GetComponent<RectTransform>();
            burst.SetParent(stage, false);
            Stretch(burst);

            var noise = new System.Random(unchecked(settings.Seed * 486187739 + 0x0C0FE77));
            float spread = ConfettiSpread * Mathf.Deg2Rad;
            var pieces = new ConfettiPiece[ConfettiCount];
            for (int i = 0; i < pieces.Length; i++)
            {
                var rect = new GameObject("Piece", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                rect.SetParent(burst, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, ConfettiOriginFromBottom);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(Between(noise, 9f, 13f), Between(noise, 6f, 9f));
                rect.anchoredPosition = Vector2.zero;
                var image = rect.GetComponent<Image>();
                image.color = ConfettiColours[noise.Next(ConfettiColours.Length)];
                image.raycastTarget = false;
                pieces[i] = new ConfettiPiece
                {
                    rect = rect,
                    image = image,
                    colour = image.color,
                    angle = Between(noise, -spread / 2f, spread / 2f),
                    // canvas-confetti's launch: half its start speed of 45 px a frame, plus up to all of it.
                    speed = 60f * Between(noise, 22.5f, 67.5f),
                    spin = Between(noise, 180f, 540f) * (noise.Next(2) == 0 ? -1f : 1f),
                    tilt = Between(noise, 0f, 360f),
                    wobble = Between(noise, 0f, Mathf.PI * 2f),
                    wobbleRate = Between(noise, 3f, 6.6f),
                    flipRate = Between(noise, 4f, 8f),
                };
            }
            StartCoroutine(Throw(burst, pieces));
        }

        private static IEnumerator Throw(RectTransform burst, ConfettiPiece[] pieces)
        {
            float time = 0f;
            while (burst != null && time < ConfettiLife)
            {
                time += Time.unscaledDeltaTime;
                // How far a piece has gone for each px/s it was thrown at, its speed shrinking as it goes.
                float reach = (1f - Mathf.Exp(-ConfettiDecay * time)) / ConfettiDecay;
                float fade = 1f - Mathf.Clamp01(time / ConfettiLife);
                for (int i = 0; i < pieces.Length; i++)
                {
                    var piece = pieces[i];
                    if (piece.rect == null) continue;
                    float travel = piece.speed * reach;
                    float sway = 10f * (Mathf.Cos(piece.wobble + piece.wobbleRate * time) - Mathf.Cos(piece.wobble));
                    piece.rect.anchoredPosition = new Vector2(Mathf.Sin(piece.angle) * travel + sway,
                        Mathf.Cos(piece.angle) * travel - ConfettiFall * time);
                    piece.rect.localRotation = Quaternion.Euler(0f, 0f, piece.tilt + piece.spin * time);
                    piece.rect.localScale = new Vector3(Mathf.Cos(piece.wobble + piece.flipRate * time), 1f, 1f);
                    piece.image.color = new Color(piece.colour.r, piece.colour.g, piece.colour.b, piece.colour.a * fade);
                }
                yield return null;
            }
            if (burst != null) Destroy(burst.gameObject);
        }

        private static float Between(System.Random noise, float min, float max) => min + (float)noise.NextDouble() * (max - min);

        /// <summary>
        /// The reference build's close: a fade to black, held a moment. The theme fades out under it
        /// as the reference's does (IntroSequence.tsx:323-326); whatever plays the music reads that
        /// from <see cref="MusicClosing"/>, which the next beat, a skip or the end clears.
        /// </summary>
        private IEnumerator FadeToBlack()
        {
            MusicClosing = true;
            var black = HudPrimitives.Fill("Fade", stage, new Color(0f, 0f, 0f, 0f), 1);
            Stretch(black);
            var image = black.GetComponent<Image>();
            image.raycastTarget = false;
            Animate(black, 0f, 0.8f, t => image.color = new Color(0f, 0f, 0f, t));
            yield return Hold(TransitionSeconds);
        }

        // ---------------------------------------------------------------- house entry and walk-in

        /// <summary>
        /// The reference build's house-entry card (HouseEntrySequence.tsx): "Welcome to the House"
        /// with how many have entered under it in capitals, the card growing in from nine tenths at
        /// 0.3 s. At 2 s the season's own arrival line fades in under the count, where the reference
        /// fades in "Head of Household competition starting soon..." low on the screen: the tour and
        /// the introductions come next here, and promising a competition would be untrue. The line
        /// is in the card rather than low on the screen, where it ran edge to edge over the Continue
        /// hint (UI-UX-PASS-PLAN S0, sweep-show 22), and the card holds for the beat so it can be read.
        /// </summary>
        private IEnumerator HouseEntry()
        {
            Section(Night);
            var card = HudPrimitives.Glass("Card", stage);
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(880f, 272f);
            card.anchoredPosition = Vector2.zero;
            var fader = Fader(card);
            Animate(card, 0.3f, 0.6f, t =>
            {
                fader.alpha = t;
                card.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, t);
            });

            var title = Text(card, "Heading", Localisation.Text("Welcome to the House"), 60f, Color.white, 840f,
                new Vector2(0f, 58f), UiTheme.Weight.Bold);
            TitleGradient(title);
            title.textWrappingMode = TextWrappingModes.NoWrap;

            int count = settings.Cast?.Count ?? 0;
            string entered = count > 0
                ? Localisation.Format("{0} Houseguests have entered", count)
                : Localisation.Text("THE HOUSEGUESTS ARRIVE");
            var under = Text(card, "Entered", entered, 20f, UiTheme.Accent, 840f, new Vector2(0f, -10f));
            under.fontStyle = FontStyles.UpperCase;
            under.characterSpacing = 10f;

            var line = Text(card, "Line", string.IsNullOrEmpty(settings.ArrivalLine)
                    ? Localisation.Text("Doors open, bags come down, and nobody knows anybody yet.")
                    : settings.ArrivalLine,
                14f, new Color(UiTheme.Heading.r, UiTheme.Heading.g, UiTheme.Heading.b, 0.8f), 840f, new Vector2(0f, -70f));
            line.fontStyle = FontStyles.UpperCase;
            line.characterSpacing = 6f;
            // Three lines of room: an All-Stars season's line runs long.
            line.rectTransform.sizeDelta = new Vector2(840f, 56f);
            Animate(line, 2.0f, 0.5f, t => line.alpha = 0.8f * t);

            yield return Hold(HouseEntryHold - 0.1f);
            // The walk-in opens inside the house looking out at the yard; the cut happens under this
            // card, so the house is never seen jumping to it.
            var keys = settings.WalkInKeys;
            if (settings.Rig != null && keys != null && keys.Count > WalkInOver)
            {
                var first = keys[WalkInPushFrom];
                first.Seconds = 0.01f;
                settings.Rig.MoveTo(Motionless ? keys[WalkInOver] : first);
            }
            yield return Pause(0.1f);
        }

        /// <summary>
        /// The walk-in (HouseWalkInSequence.tsx): the house is the content. The camera starts pulled
        /// back inside the house looking out at the yard, pushes in toward the doorway for a second,
        /// rises over the bedrooms until 4 s and cranes over the top of the whole house by 11 s - the
        /// reference's schedule - while everybody walks in to where the season starts them. The
        /// reference's walkers came from spawn points by its doors, and only those with a model at
        /// all; here the whole house walks home. Its caption rises in at 0.5 s and is gone at 3 s,
        /// faded over its last quarter second where the reference's simply vanished.
        /// </summary>
        private IEnumerator WalkIn()
        {
            Section(Transparent);
            var caption = Text(stage, "Caption", Localisation.Text("The houseguests enter the house..."), 20f,
                new Color(UiTheme.Paper.r, UiTheme.Paper.g, UiTheme.Paper.b, 0.85f), 1500f, Vector2.zero);
            caption.fontStyle = FontStyles.UpperCase;
            caption.characterSpacing = 10f;
            var place = caption.rectTransform;
            place.anchorMin = place.anchorMax = new Vector2(0.5f, 0f);
            place.pivot = new Vector2(0.5f, 0f);
            place.anchoredPosition = new Vector2(0f, WalkInCaptionFloor);
            if (!Motionless)
            {
                Animate(caption, 0.5f, 0.6f, t =>
                {
                    caption.alpha = 0.85f * t;
                    place.anchoredPosition = new Vector2(0f, WalkInCaptionFloor - 10f * (1f - t));
                }, OutEase);
                StartCoroutine(DropCaptionAt(caption, 3.0f, 0.25f));
            }

            var rig = settings.Rig;
            var keys = settings.WalkInKeys;
            var world = settings.Stage != null && settings.Stage.Placed ? settings.Stage : null;
            var order = PlayerFirst();

            if (rig == null || keys == null || keys.Count <= WalkInOver || Motionless)
            {
                if (rig != null && keys != null && keys.Count > WalkInOver) rig.MoveTo(keys[WalkInOver]);
                if (world != null) foreach (var person in order) world.SendHome(person.id);
                yield return Hold(3f);
                yield break;
            }

            var first = keys[WalkInPushFrom];
            first.Seconds = 0.01f;
            rig.MoveTo(first);
            WalkInSchedule(keys, out float riseAt, out float craneAt, out float settledAt);
            int sent = 0;
            float clock = 0f, allHomeAt = -1f;
            bool pushing = false, rising = false, over = false;
            while (true)
            {
                if (TakeAdvance())
                {
                    var last = keys[WalkInOver];
                    last.Seconds = 0.01f;
                    rig.MoveTo(last);
                    break;
                }
                // A timed move starts from wherever the camera is, so the push-in waits for the cut
                // to its first key to land - a frame at most, bounded in case it never reports.
                if (!pushing && clock > 0f && (!rig.IsTravelling || clock >= 0.05f)) { rig.MoveTo(keys[WalkInDoorway]); pushing = true; }
                if (world != null)
                    while (sent < order.Count && clock >= 0.3f + 0.35f * sent) world.SendHome(order[sent++].id);
                if (!rising && clock >= riseAt) { rig.MoveTo(keys[WalkInRise]); rising = true; }
                if (!over && clock >= craneAt) { rig.MoveTo(keys[WalkInOver]); over = true; }
                if (world != null && allHomeAt < 0f && sent == order.Count && world.AllHome) allHomeAt = clock;
                // Done once the crane has landed and everybody has been home a second, and never
                // later than 18 s; with nobody to walk, at 10 s.
                bool done = world == null
                    ? clock >= 10f
                    : clock >= 18f || (clock >= settledAt && allHomeAt >= 0f && clock >= allHomeAt + 1f);
                if (done) break;
                clock += Time.unscaledDeltaTime;
                yield return null;
            }
            // Anybody not yet sent goes now; stragglers keep walking under the next beat.
            if (world != null) while (sent < order.Count) world.SendHome(order[sent++].id);
        }

        /// <summary>Takes a caption away at <paramref name="at"/> seconds, fading it over the last <paramref name="fade"/> of them.</summary>
        private static IEnumerator DropCaptionAt(TMP_Text text, float at, float fade)
        {
            float clock = 0f, from = -1f;
            while (text != null && clock < at)
            {
                clock += Time.unscaledDeltaTime;
                if (clock >= at - fade)
                {
                    if (from < 0f) from = text.alpha;
                    text.alpha = from * Mathf.Clamp01((at - clock) / fade);
                }
                yield return null;
            }
            if (text != null) text.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- drawing

        private static Image Picture(string name, Transform parent, Sprite sprite, Color colour, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            var image = rect.GetComponent<Image>();
            image.sprite = sprite;
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        private static Sprite thinRing;

        /// <summary>
        /// A ring with a hairline stroke, a fiftieth of its diameter. The theme's own ring is drawn
        /// for timers, a ninth of its diameter thick, which at the emblem's size is a band rather
        /// than the line the reference build's rings are. Generated once.
        /// </summary>
        private static Sprite ThinRing()
        {
            if (thinRing != null) return thinRing;
            const int size = 256;
            const float radius = 124f, half = 3f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Opening thin ring", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - size / 2f, dy = y + 0.5f - size / 2f;
                    float distance = Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - radius);
                    byte alpha = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(half + 0.5f - distance));
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            thinRing = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            thinRing.name = "Opening thin ring";
            thinRing.hideFlags = HideFlags.DontSave;
            return thinRing;
        }
    }
}
