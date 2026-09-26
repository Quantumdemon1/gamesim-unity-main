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
    /// together), the house entry and the walk-in. Timings are the reference build's wherever it
    /// has one - its intro, its tunnel and door, its house entry and walk-in - and each departure
    /// says why beside it.
    /// </summary>
    public sealed partial class OpeningSequence
    {
        private static readonly Color Night = new Color(0.01f, 0.02f, 0.03f, 1f);
        private static readonly Color Transparent = new Color(0f, 0f, 0f, 0f);

        private const float LoadingCap = 15f;           // the reference build's hard cap on its preload
        private const float TitleHold = 3.5f;           // its title card, desktop
        private const float CardReveal = 3.0f;          // its reveal without a body
        private const float LowerThirdAt = 1.5f;        // its name card, and the portrait after it
        private const float PortraitAt = 1.8f;
        private const float GroupHold = 3.5f;
        private const float TransitionSeconds = 1.2f;   // a 0.8 s fade and the rest held black
        private const float HouseEntryHold = 6.0f;
        private const float HandOffHold = 2.4f;
        private const float DoorWaitCap = 6.0f;
        private const float MarkWaitCap = 4.0f;

        /// <summary>
        /// How long a reveal waits before the door opens, and how long its houseguest stands on the
        /// mark, for a house of <paramref name="count"/> reveals. The reference build's 2.8 s and a
        /// second up to eight people, shortened towards 2.0 s and 0.4 s at sixteen so a big house
        /// does not spend a minute and a half at the door.
        /// </summary>
        public static void RevealTiming(int count, out float doorAt, out float markHold)
        {
            float k = Mathf.Clamp01((count - 8) / 8f);
            doorAt = 2.8f - 0.8f * k;
            markHold = 1.0f - 0.6f * k;
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
        /// reference build's own fallback for a houseguest with no body.
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
        /// Waits for the bodies. The reference build faked its progress bar while it preloaded;
        /// this one counts bodies that have finished assembling, and only shows at all when that
        /// takes longer than half a second.
        /// </summary>
        private IEnumerator LoadingGate(IStage world)
        {
            float waited = 0f;
            RectTransform gate = null;
            RectTransform fill = null;
            TMP_Text percent = null;
            while (world.BodyReadiness < 1f && waited < LoadingCap)
            {
                if (gate == null && waited >= 0.5f)
                {
                    gate = Box("Loading", stage, new Vector2(600f, 120f), new Vector2(0f, -220f));
                    var words = Text(gate, "Caption", Localisation.Text("PREPARING THE HOUSE..."), 16f, UiTheme.Muted, 600f, new Vector2(0f, 20f));
                    words.characterSpacing = 6f;
                    var track = HudPrimitives.Fill("Track", gate, new Color(1f, 1f, 1f, 0.12f), 2);
                    track.anchorMin = track.anchorMax = new Vector2(0.5f, 0.5f);
                    track.sizeDelta = new Vector2(256f, 4f);
                    track.anchoredPosition = new Vector2(0f, -8f);
                    fill = HudPrimitives.Fill("Bar", track, UiTheme.Heading, 2);
                    fill.anchorMin = new Vector2(0f, 0f); fill.anchorMax = new Vector2(0f, 1f);
                    fill.pivot = new Vector2(0f, 0.5f);
                    fill.sizeDelta = new Vector2(0f, 0f);
                    percent = Text(gate, "Percent", "0%", 14f, UiTheme.Muted, 200f, new Vector2(0f, -32f));
                }
                if (fill != null)
                {
                    float share = Mathf.Clamp01(world.BodyReadiness);
                    fill.sizeDelta = new Vector2(256f * share, 0f);
                    percent.text = Localisation.Format("{0}%", Mathf.RoundToInt(share * 100f));
                }
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            if (gate != null) Destroy(gate.gameObject);
            // Placed only once every body is built: placing a body mid-assembly lost it its binding.
            if (world.BodyReadiness >= 1f) world.TryPlace();
        }

        /// <summary>
        /// The title card: the eye, the wordmark and how many are playing for one win. The eye is the
        /// reference build's Big Brother-era emblem, orphaned by its Survivor rename, redrawn in the
        /// title blues because gold means power in this house and nobody has any yet.
        /// </summary>
        private IEnumerator TitleCard()
        {
            var card = Box("Title card", stage, new Vector2(1600f, 800f), Vector2.zero);
            var fader = Fader(card);
            Animate(card, 0.3f, 0.8f, t => fader.alpha = t);

            EyeEmblem(card, new Vector2(0f, 230f));

            var title = Text(card, "Title", "GAMESIM", 96f, Color.white, 1500f, new Vector2(0f, 40f), UiTheme.Weight.Bold);
            TitleGradient(title);
            title.characterSpacing = 12f;
            Animate(title, 0.3f, 1.2f, t => title.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, t), Settle);

            int count = settings.Cast?.Count ?? 0;
            string line = count > 0
                ? Localisation.Format("{0} Houseguests. 1 Winner.", count)
                : Localisation.Text("A NEW SEASON BEGINS");
            var subtitle = Text(card, "Subtitle", line, 28f, UiTheme.Paper, 1500f, new Vector2(0f, -70f));
            Animate(subtitle, 1.6f, 0.8f, t =>
            {
                subtitle.alpha = t;
                subtitle.rectTransform.anchoredPosition = new Vector2(0f, -70f - 20f * (1f - t));
            });

            yield return Hold(TitleHold);
        }

        /// <summary>
        /// The eye: a glow that breathes, two rings, the eye itself and a line scanning down it -
        /// the reference build's emblem, drawn from the house's own icon set.
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
        /// they are from, and which of the house they are. The reference build's lower third,
        /// sliding up at 1.5 s and bringing the face in at 1.8 s.
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
            Animate(root, LowerThirdAt, 0.6f, t =>
            {
                fader.alpha = t;
                root.anchoredPosition = new Vector2(80f, 110f - 30f * (1f - t));
            });

            var face = HudPrimitives.Portrait(root, CharacterPortraits.Get(person), UiTheme.Heading, 128f, 3f, false, person);
            face.name = "Portrait";
            face.anchorMin = face.anchorMax = new Vector2(0f, 0.5f);
            face.pivot = new Vector2(0.5f, 0.5f);
            face.anchoredPosition = new Vector2(67f, 0f);
            Animate(face, PortraitAt, 0.5f, t => face.localScale = Vector3.one * t, Settle);

            var lines = new List<string>();
            if (person.age > 0) lines.Add(Localisation.Format("Age {0}", person.age));
            if (!string.IsNullOrWhiteSpace(person.occupation)) lines.Add(person.occupation);
            if (!string.IsNullOrWhiteSpace(person.hometown)) lines.Add(person.hometown);

            float top = 88f + lines.Count * 15f;
            var name = LeftText(root, "Name", HudPrimitives.WithYou(person.name, person.isPlayer), 48f, UiTheme.Paper,
                new Vector2(160f, top), UiTheme.Weight.Bold);
            name.textWrappingMode = TextWrappingModes.NoWrap;
            float y = top - 50f;
            foreach (var line in lines)
            {
                LeftText(root, "Line", line, 22f, UiTheme.Heading, new Vector2(160f, y), UiTheme.Weight.Medium);
                y -= 30f;
            }

            // The count stands at the other side, above the skip control, the way the reference
            // build puts it in the bottom corner.
            var counter = HudPrimitives.Label("Counter", root, 18f, UiTheme.Muted, TextAlignmentOptions.Right);
            counter.text = Localisation.Format("{0} of {1}", index + 1, count);
            var place = counter.rectTransform;
            place.SetParent(stage, false);
            place.anchorMin = place.anchorMax = new Vector2(1f, 0f);
            place.pivot = new Vector2(1f, 0f);
            place.sizeDelta = new Vector2(240f, 26f);
            place.anchoredPosition = new Vector2(-80f, 100f);
            place.SetParent(root, true);
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
        /// walk up behind it, shudders and swings open in a burst of light as the camera pushes in,
        /// and they walk through to a mark in front of the lens and greet it with their name coming
        /// up under them, then walk off out of shot to where the house gathers. The reference
        /// build's tunnel reveal, walked for real - its walker glided at ten metres a second and
        /// left through the lens.
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
        /// build's "Welcome" line under them. The front door comes down behind it, out of sight.
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

            var welcome = Text(stage, "Welcome", Localisation.Text("Welcome to the House"), 40f, Color.white, 1500f,
                new Vector2(0f, 40f - height / 2f - 60f), UiTheme.Weight.Bold);
            TitleGradient(welcome);
            Animate(welcome, 0.6f, 0.5f, t => welcome.alpha = t);

            yield return Hold(GroupHold);
        }

        /// <summary>The reference build's close: a fade to black, held a moment.</summary>
        private IEnumerator FadeToBlack()
        {
            var black = HudPrimitives.Fill("Fade", stage, new Color(0f, 0f, 0f, 0f), 1);
            Stretch(black);
            var image = black.GetComponent<Image>();
            image.raycastTarget = false;
            Animate(black, 0f, 0.8f, t => image.color = new Color(0f, 0f, 0f, t));
            yield return Hold(TransitionSeconds);
        }

        // ---------------------------------------------------------------- house entry and walk-in

        /// <summary>
        /// The reference build's house-entry card: how many have come in, and the season's own line
        /// under it. Its "...competition starting soon" line is left out - it dates from when this
        /// card was the last thing before the competition, and three beats follow it now - and its
        /// "Welcome to the House" is not repeated, because the group card said it seven seconds ago.
        /// </summary>
        private IEnumerator HouseEntry()
        {
            Section(Night);
            var card = HudPrimitives.Glass("Card", stage);
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(760f, 220f);
            card.anchoredPosition = Vector2.zero;
            var fader = Fader(card);
            Animate(card, 0.3f, 0.6f, t => fader.alpha = t);

            int count = settings.Cast?.Count ?? 0;
            string heading = count > 0
                ? Localisation.Format("{0} Houseguests have entered", count)
                : Localisation.Text("THE HOUSEGUESTS ARRIVE");
            var title = Text(card, "Heading", heading, 44f, Color.white, 720f, new Vector2(0f, 42f), UiTheme.Weight.Bold);
            TitleGradient(title);

            var line = Text(card, "Line", string.IsNullOrEmpty(settings.ArrivalLine)
                    ? Localisation.Text("Doors open, bags come down, and nobody knows anybody yet.")
                    : settings.ArrivalLine,
                18f, UiTheme.Muted, 700f, new Vector2(0f, -38f));
            line.rectTransform.sizeDelta = new Vector2(700f, 50f);
            Animate(line, 2.0f, 0.5f, t => line.alpha = t);

            if (!Motionless) StartCoroutine(FadeCardAt(card, fader, 4.0f));

            yield return Hold(HouseEntryHold - 0.1f);
            // The walk-in opens inside the house looking out at the yard; the cut happens under this
            // card, so the house is never seen jumping to it.
            var keys = settings.WalkInKeys;
            if (settings.Rig != null && keys != null && keys.Count > 0)
            {
                var first = keys[0];
                first.Seconds = 0.01f;
                settings.Rig.MoveTo(Motionless && keys.Count > 2 ? keys[2] : first);
            }
            yield return Pause(0.1f);
        }

        private static IEnumerator FadeCardAt(RectTransform card, CanvasGroup fader, float at)
        {
            while (at > 0f && card != null) { at -= Time.unscaledDeltaTime; yield return null; }
            float t = 0f;
            while (card != null && t < 0.4f) { t += Time.unscaledDeltaTime; fader.alpha = 1f - t / 0.4f; yield return null; }
        }

        /// <summary>
        /// The walk-in: the house is the content. The camera starts inside looking out at the yard,
        /// cranes up and ends over the top of the house while everybody walks in to where the season
        /// starts them - the reference build's walk-in, whose people stood in view at their spawn
        /// points before the doors opened and whose caption vanished without its fade.
        /// </summary>
        private IEnumerator WalkIn()
        {
            Section(Transparent);
            var caption = Text(stage, "Caption", Localisation.Text("The Houseguests enter the house..."), 40f, UiTheme.Paper, 1500f,
                new Vector2(0f, -360f));
            if (!Motionless)
            {
                caption.alpha = 0f;
                Animate(caption, 0.5f, 0.5f, t => caption.alpha = t);
                StartCoroutine(FadeTextAt(caption, 3.0f));
            }

            var rig = settings.Rig;
            var keys = settings.WalkInKeys;
            var world = settings.Stage != null && settings.Stage.Placed ? settings.Stage : null;
            var order = PlayerFirst();

            if (rig == null || keys == null || keys.Count < 3 || Motionless)
            {
                if (rig != null && keys != null && keys.Count >= 3) rig.MoveTo(keys[2]);
                if (world != null) foreach (var person in order) world.SendHome(person.id);
                yield return Hold(3f);
                yield break;
            }

            var first = keys[0];
            first.Seconds = 0.01f;
            rig.MoveTo(first);
            int sent = 0;
            float clock = 0f, allHomeAt = -1f;
            bool rising = false, over = false;
            while (true)
            {
                if (TakeAdvance())
                {
                    var last = keys[2];
                    last.Seconds = 0.01f;
                    rig.MoveTo(last);
                    break;
                }
                if (world != null)
                    while (sent < order.Count && clock >= 0.3f + 0.35f * sent) world.SendHome(order[sent++].id);
                if (!rising && clock >= 3.5f) { rig.MoveTo(keys[1]); rising = true; }
                if (!over && clock >= 3.5f + Mathf.Max(0.5f, keys[1].Seconds)) { rig.MoveTo(keys[2]); over = true; }
                if (world != null && allHomeAt < 0f && sent == order.Count && world.AllHome) allHomeAt = clock;
                bool done = world == null
                    ? clock >= 10f
                    : clock >= 18f || (clock >= 13.5f && allHomeAt >= 0f && clock >= allHomeAt + 1f);
                if (done) break;
                clock += Time.unscaledDeltaTime;
                yield return null;
            }
            // Anybody not yet sent goes now; stragglers keep walking under the next beat.
            if (world != null) while (sent < order.Count) world.SendHome(order[sent++].id);
        }

        private static IEnumerator FadeTextAt(TMP_Text text, float at)
        {
            while (at > 0f && text != null) { at -= Time.unscaledDeltaTime; yield return null; }
            float t = 0f;
            while (text != null && t < 0.5f) { t += Time.unscaledDeltaTime; text.alpha = 1f - t / 0.5f; yield return null; }
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
