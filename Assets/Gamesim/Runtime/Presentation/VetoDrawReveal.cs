using System.Collections.Generic;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The veto's draw, drawn (PACK8-PASS-PLAN B2, the owner's mockup 76): after the selection's
    /// "Continue episode" commits the draw, the chips come out of the bag one at a time and each
    /// turns into the face it drew, beside the three who play by right, until the whole field is up.
    /// It used to be the generic field card, the six at once, and the drawn names were read in the
    /// status line before the card had said anything.
    ///
    /// <para>Presentation only. The draw is made and saved by the commit before this plays; the card
    /// is handed the committed lineup and writes nothing. The engine keeps the lineup in house order,
    /// so the order the chips turn is a seeded presentation order the caller chooses
    /// (<see cref="VetoDraw.RevealOrder"/>), never the order the engine drew in. Everything on it is
    /// public by then: the lineup rule and who plays.</para>
    ///
    /// <para>The ceremony cards' rules: nothing raycasts, it ends on a timer rather than on
    /// acknowledgement, and the presses that move it on are read straight off the devices. The first
    /// press turns every chip still in the bag and shows the field - the order given up, never the
    /// result - and a press on the field ends the card. Batch runs and reduced motion never play it:
    /// they keep the field card it stands in for.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VetoDrawReveal : MonoBehaviour
    {
        private const float FadeIn = 0.30f;
        /// <summary>The beat with every chip in the bag, before the first comes out.</summary>
        private const float Intro = 0.70f;
        /// <summary>How long each chip holds the stage: out of the bag, the slot lit, the face turned over.</summary>
        private const float PerChip = 0.90f;
        /// <summary>How long a chip takes to leave the bag, and then its face to turn over.</summary>
        private const float Turn = 0.30f;
        /// <summary>How long the whole field holds once every chip is out.</summary>
        private const float FieldHold = 2.6f;
        private const float FadeOut = 0.45f;
        /// <summary>How long past its fade before a press moves it on: a press in flight as it appears is not one.</summary>
        private const float ReadDelay = 0.35f;

        /// <summary>One houseguest on the card: who, the pill they wear, and the face it shows.</summary>
        public readonly struct Player
        {
            public readonly string Id;
            public readonly string Name;
            public readonly string Badge;
            public readonly Texture Portrait;
            public readonly ContestantState Character;

            public Player(string id, string name, string badge, Texture portrait, ContestantState character = null)
            {
                Id = id; Name = name; Badge = badge; Portrait = portrait;
                Character = character?.Clone();
            }
        }

        /// <summary>The card's parts, by the names a test finds them by.</summary>
        public const string ProgressName = "Draw progress", SlotName = "Draw slot", ChipName = "Chip", DrawnPrefix = "Drawn · ",
            PlayingPrefix = "Playing · ";

        /// <summary>What the card says once every chip is out.</summary>
        public const string FieldCaption = "The field is set";

        private CanvasGroup group;
        private RectTransform column, scrim, glass, row, rule;
        private TMP_Text eyebrow, title, line, progress, dismiss;
        private readonly List<RectTransform> chips = new List<RectTransform>();
        private readonly List<RectTransform> faces = new List<RectTransform>();
        private readonly List<GameObject> emptySlots = new List<GameObject>();
        private readonly List<GameObject> litSlots = new List<GameObject>();
        private readonly List<Player> drawn = new List<Player>();
        private readonly List<string> order = new List<string>();
        private float elapsed, upFor, chipLift;
        private bool playing, reduced, fieldShown;
        private int turned = -1;

        /// <summary>Matches the HUD's text size, so a large-text player gets a large card.</summary>
        public float FontScale { get; set; } = 1f;

        public bool IsPlaying => playing;

        /// <summary>True once every chip is out and the whole field is up.</summary>
        public bool ShowingField => fieldShown;

        /// <summary>How many chips have come out of the bag so far.</summary>
        public int ChipsOut => Mathf.Max(0, turned);

        /// <summary>Who the chips turn into, in the order they come out.</summary>
        public IReadOnlyList<string> Order => order;

        /// <summary>How long the card runs start to finish when nobody moves it on, in real seconds.</summary>
        public float Duration => FieldAt + FieldHold + FadeOut;

        private float DrawStart => FadeIn + Intro;
        private float FieldAt => DrawStart + drawn.Count * PerChip;

        /// <summary>Creates the card as a root object in <paramref name="owner"/>'s scene, as every ceremony card is.</summary>
        public static VetoDrawReveal Attach(GameObject owner)
        {
            var root = new GameObject("Gamesim Veto Draw",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            if (owner != null && owner.scene.IsValid()) SceneManager.MoveGameObjectToScene(root, owner.scene);

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // The field card's order: this plays instead of it, never over it.
            canvas.sortingOrder = 100;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;

            return root.AddComponent<VetoDrawReveal>();
        }

        /// <summary>
        /// Plays the draw: <paramref name="byRight"/> - the Head of Household and the block - on the
        /// left, the bag with a chip for each of <paramref name="drawnPlayers"/>, and a slot for each
        /// that the chip turns into their face, in the order given. <paramref name="fieldLine"/> is the
        /// line under the title. Declines a draw with nobody drawn - everyone played - and the caller
        /// plays the field card instead, so the beat is never silent.
        /// </summary>
        public bool Play(int week, IList<Player> byRight, IList<Player> drawnPlayers, string fieldLine, bool reducedMotion)
        {
            if (drawnPlayers == null || drawnPlayers.Count == 0) return false;
            drawn.Clear();
            drawn.AddRange(drawnPlayers);
            order.Clear();
            foreach (var player in drawn) order.Add(player.Id);

            Build();
            reduced = reducedMotion;
            elapsed = 0f;
            upFor = 0f;
            turned = -1;
            fieldShown = false;
            playing = true;

            eyebrow.text = Localisation.Text("WEEK " + Mathf.Max(1, week));
            title.text = Localisation.Text("Power of Veto");
            line.text = Localisation.Text(fieldLine ?? string.Empty);
            dismiss.text = Localisation.Text(CeremonyTakeover.DismissCaption);
            Lay(byRight ?? new List<Player>());

            column.gameObject.SetActive(true);
            scrim.gameObject.SetActive(true);
            group.alpha = reduced ? 1f : 0f;
            Show(0);
            // A player who asked for less motion gets the field at once, for the same time on screen.
            if (reduced) SkipToField();
            return true;
        }

        /// <summary>
        /// The first press's step: every chip still in the bag out at once and the whole field shown.
        /// The order is given up, never the field. Nothing once the field is up.
        /// </summary>
        public void SkipToField()
        {
            if (!playing || fieldShown) return;
            elapsed = Mathf.Max(elapsed, FieldAt);
            Show(drawn.Count);
            Field();
        }

        /// <summary>Takes the card down at once: a new beat, a scene change, or a press on the field.</summary>
        public void Cancel()
        {
            playing = false;
            if (group != null) group.alpha = 0f;
            if (column != null) column.gameObject.SetActive(false);
            if (scrim != null) scrim.gameObject.SetActive(false);
        }

        private bool Dismissable => upFor >= FadeIn + ReadDelay;

        private void Update()
        {
            if (!playing) return;
            CeremonyOverlays.Showing();
            float step = Time.unscaledDeltaTime;
            upFor += step;

            if (Dismissable && CeremonyTakeover.SkipPressed())
            {
                if (fieldShown) { Cancel(); return; }
                SkipToField();
                return;
            }

            elapsed += step;
            float end = FieldAt + FieldHold;
            if (reduced) group.alpha = 1f;
            else if (elapsed < FadeIn) group.alpha = Eased(elapsed / FadeIn);
            else if (elapsed < end) group.alpha = 1f;
            else
            {
                float exit = (elapsed - end) / FadeOut;
                if (exit >= 1f) { Cancel(); return; }
                group.alpha = 1f - Eased(exit);
            }
            if (reduced && elapsed >= end + FadeOut) { Cancel(); return; }

            if (!fieldShown)
            {
                int due = Due(elapsed);
                if (due != turned) Show(due);
                if (elapsed >= FieldAt) Field();
            }
            Animate();
        }

        private static float Eased(float t) => 1f - (1f - t) * (1f - t);

        /// <summary>How many chips are out at <paramref name="t"/> on the card's clock.</summary>
        private int Due(float t) => t < DrawStart ? 0 : Mathf.Min(drawn.Count, Mathf.FloorToInt((t - DrawStart) / PerChip) + 1);

        /// <summary>Puts the first <paramref name="count"/> chips out, and says how far the draw has got.</summary>
        private void Show(int count)
        {
            turned = count;
            if (count == 0)
                progress.text = Localisation.Text(drawn.Count == 1 ? "One chip in the bag" : Spelled(drawn.Count) + " chips in the bag");
            else
                progress.text = Localisation.Text("Chip " + count + " of " + drawn.Count + ": " + drawn[count - 1].Name);
            Animate();
        }

        /// <summary>The close: every drawn face up beside the three who play by right.</summary>
        private void Field()
        {
            fieldShown = true;
            turned = drawn.Count;
            progress.text = Localisation.Text(FieldCaption);
            Animate();
        }

        /// <summary>
        /// Each chip out of the bag lifts and fades; its slot lights; then its face turns over it,
        /// a card flipping on its upright axis. Everything a chip does is read from the card's clock,
        /// so a skip lands every chip where the clock says it is.
        /// </summary>
        private void Animate()
        {
            for (int i = 0; i < drawn.Count; i++)
            {
                float local = elapsed - (DrawStart + i * PerChip);
                bool isOut = i < turned;
                float lift = isOut ? Mathf.Clamp01(local / Turn) : 0f;
                float flip = isOut ? Mathf.Clamp01((local - Turn) / Turn) : 0f;
                if (fieldShown) { lift = 1f; flip = 1f; }
                if (i < chips.Count)
                {
                    var chip = chips[i];
                    var image = chip.GetComponent<Graphic>();
                    if (image != null) { var c = image.color; c.a = 1f - lift; image.color = c; }
                    chip.anchoredPosition = new Vector2(chip.anchoredPosition.x, -chipLift + lift * 18f * Scale);
                    chip.gameObject.SetActive(lift < 1f);
                }
                if (i < emptySlots.Count && emptySlots[i] != null) emptySlots[i].SetActive(!isOut);
                if (i < litSlots.Count && litSlots[i] != null) litSlots[i].SetActive(isOut && flip < 1f);
                if (i < faces.Count)
                {
                    faces[i].gameObject.SetActive(isOut && flip > 0f);
                    faces[i].localScale = new Vector3(reduced ? 1f : Eased(flip), 1f, 1f);
                }
            }
        }

        private float Scale => Mathf.Max(0.5f, FontScale);

        private static string Spelled(int count)
        {
            string[] words = { "No", "One", "Two", "Three", "Four", "Five", "Six" };
            return count >= 0 && count < words.Length ? words[count] : count.ToString();
        }

        // ------------------------------------------------------------------ the card

        private void Build()
        {
            float s = Scale;
            if (column == null)
            {
                group = GetComponent<CanvasGroup>();
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
                var root = (RectTransform)transform;

                // The room behind, dimmed and vignetted, as the other ceremony cards leave it.
                scrim = HudPrimitives.Fill("Scrim", root, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, 0.55f), 1);
                scrim.anchorMin = Vector2.zero; scrim.anchorMax = Vector2.one;
                scrim.offsetMin = Vector2.zero; scrim.offsetMax = Vector2.zero;
                HudPrimitives.Vignette(scrim);

                column = new GameObject("Draw card", typeof(RectTransform)).GetComponent<RectTransform>();
                column.SetParent(root, false);
                column.anchorMin = column.anchorMax = new Vector2(.5f, .5f);
                column.pivot = new Vector2(.5f, .5f);
                glass = HudPrimitives.Fill("Card glass", column, UiTheme.GlassFill, UiTheme.GlassRadius);
                glass.anchorMin = Vector2.zero; glass.anchorMax = Vector2.one;
                UiTheme.Glass(glass, UiTheme.GlassRadius);

                eyebrow = HudPrimitives.Label("Draw week", column, 15f, UiTheme.Muted, TextAlignmentOptions.Center);
                eyebrow.characterSpacing = 14f;
                title = HudPrimitives.Label("Draw title", column, 44f, UiTheme.Paper, TextAlignmentOptions.Center);
                var bold = UiTheme.Font(UiTheme.Weight.Bold);
                if (bold != null) title.font = bold;
                line = HudPrimitives.Label("Field line", column, 18f, UiTheme.Muted, TextAlignmentOptions.Center);
                line.fontStyle = FontStyles.Italic;
                progress = HudPrimitives.Label(ProgressName, column, 17f, UiTheme.Gold, TextAlignmentOptions.Center);
                rule = HudPrimitives.Fill("Draw rule", column, new Color(UiTheme.Muted.r, UiTheme.Muted.g, UiTheme.Muted.b, 0.35f), 1);
                dismiss = HudPrimitives.Label("Draw dismiss", column, 15f, UiTheme.Muted, TextAlignmentOptions.Center);
            }
            eyebrow.fontSize = 15f * s;
            title.fontSize = 44f * s;
            line.fontSize = 18f * s;
            progress.fontSize = 17f * s;
            dismiss.fontSize = 15f * s;
        }

        /// <summary>
        /// Lays the card out for this draw, top to bottom, each label in a box 1.3 times its type -
        /// Inter draws nothing in a box under 1.21 - and the row of faces, the bag and the slots in
        /// the middle, rebuilt for every play.
        /// </summary>
        private void Lay(IList<Player> byRight)
        {
            float s = Scale, width = 1040f * s, gap = 12f * s, sep = 40f * s, bagColumn = 150f * s;
            int cards = byRight.Count + drawn.Count;
            float card = Mathf.Min(118f * s, (width - 2f * sep - bagColumn - Mathf.Max(0, cards - 2) * gap) / Mathf.Max(1, cards));
            float photo = card * 1.05f, cardTall = photo + 34f * s, headingBox = 18f * s, rowTall = headingBox + 6f * s + cardTall;

            if (row != null) { row.gameObject.SetActive(false); Destroy(row.gameObject); }
            chips.Clear(); faces.Clear(); emptySlots.Clear(); litSlots.Clear();
            row = new GameObject("Draw row", typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(column, false);

            float y = 0f;
            y = Place(eyebrow.rectTransform, width, 22f * s, y) + 8f * s;
            y = Place(title.rectTransform, width, 58f * s, y);
            y = Place(line.rectTransform, width, 50f * s, y) + 12f * s;
            float rowTop = y;
            y = Place(row, width, rowTall, y) + 12f * s;
            y = Place(progress.rectTransform, width, 26f * s, y) + 14f * s;
            rule.anchorMin = rule.anchorMax = new Vector2(.5f, 1f);
            rule.pivot = new Vector2(.5f, 1f);
            rule.sizeDelta = new Vector2(132f * s, 1f);
            rule.anchoredPosition = new Vector2(0f, -y);
            y += 1f + 10f * s;
            y = Place(dismiss.rectTransform, width, 24f * s, y);
            column.sizeDelta = new Vector2(width, y);
            glass.offsetMin = new Vector2(-36f * s, -28f * s);
            glass.offsetMax = new Vector2(36f * s, 28f * s);

            // The row, centred: the three by right, a chevron, the bag, a chevron, the slots.
            float span = cards * card + Mathf.Max(0, byRight.Count - 1) * gap + Mathf.Max(0, drawn.Count - 1) * gap + 2f * sep + bagColumn;
            float x = Mathf.Max(0f, (width - span) * .5f);
            float cardsTop = headingBox + 6f * s;
            float leftWidth = byRight.Count * card + Mathf.Max(0, byRight.Count - 1) * gap;
            Heading(row, "AUTOMATICALLY PLAYING", x, leftWidth, headingBox);
            for (int i = 0; i < byRight.Count; i++)
                Card(row, PlayingPrefix, byRight[i], x + i * (card + gap), cardsTop, card, photo,
                    byRight[i].Badge == "HOH" ? UiTheme.Gold : UiTheme.Danger,
                    byRight[i].Badge == "HOH" ? PackArt.Pack8VetoAutoHoh : PackArt.Pack8VetoAutoNominee);
            x += leftWidth;
            float middle = cardsTop + cardTall * .5f;
            Chevron(row, x + sep * .5f, middle);
            x += sep;

            // The bag, and a chip for each seat the draw fills, in a row on its belly.
            float bag = Mathf.Min(bagColumn - 30f * s, cardTall - 40f * s), chip = 26f * s;
            var bagArt = UiTheme.Pack(PackArt.Pack8VetoBag);
            var sack = bagArt != null ? Picture("Bag", row, bagArt, Color.white) : HudPrimitives.Fill("Bag", row, new Color(.55f, .33f, .12f, 1f), 26);
            EndScreenKit.Place(sack, x + (bagColumn - bag) * .5f, cardsTop, bag, bag);
            chipLift = cardsTop + bag + 6f * s;
            var chipArt = UiTheme.Pack(PackArt.Pack8DrawChip);
            float chipStep = chip + 6f * s, chipStart = x + bagColumn * .5f - (drawn.Count - 1) * chipStep * .5f - chip * .5f;
            for (int i = 0; i < drawn.Count; i++)
            {
                var piece = chipArt != null ? Picture(ChipName, row, chipArt, Color.white) : HudPrimitives.Fill(ChipName, row, UiTheme.Accent, 9);
                EndScreenKit.Place(piece, chipStart + i * chipStep, chipLift, chip, chip);
                chips.Add(piece);
            }
            x += bagColumn;
            Chevron(row, x + sep * .5f, middle);
            x += sep;

            // A slot for each chip: empty, lit as its chip comes out, then the face it drew.
            float rightWidth = drawn.Count * card + Mathf.Max(0, drawn.Count - 1) * gap;
            Heading(row, "DRAWN FROM THE BAG", x, rightWidth, headingBox);
            for (int i = 0; i < drawn.Count; i++)
            {
                float left = x + i * (card + gap);
                var slot = EndScreenKit.Box(SlotName, row, left, cardsTop, card, cardTall);
                float disc = Mathf.Min(card, photo) * .6f;
                var empty = EndScreenKit.Whole("Empty", slot, PackArt.Pack8DrawSlotEmpty, new Vector2(card * .5f, -photo * .5f), disc);
                emptySlots.Add(empty != null ? empty.gameObject : Disc("Empty", slot, UiTheme.SurfaceRaised, card * .5f, photo * .5f, disc));
                var lit = EndScreenKit.Whole("Lit", slot, PackArt.Pack8DrawSlotFilled, new Vector2(card * .5f, -photo * .5f), disc);
                litSlots.Add(lit != null ? lit.gameObject : Disc("Lit", slot, UiTheme.Glow, card * .5f, photo * .5f, disc));
                var face = Card(slot, DrawnPrefix, drawn[i], 0f, 0f, card, photo, UiTheme.Accent, PackArt.Pack8DrawnPlayer);
                // Turned over from its middle, as a card is.
                face.pivot = new Vector2(.5f, 1f);
                face.anchoredPosition = new Vector2(card * .5f, 0f);
                faces.Add(face);
            }
        }

        /// <summary>Stands a row of the card under the last, centred; returns where the next starts.</summary>
        private static float Place(RectTransform rect, float width, float height, float y)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
            rect.pivot = new Vector2(.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -y);
            rect.sizeDelta = new Vector2(width, height);
            return y + height;
        }

        private void Heading(RectTransform row, string words, float x, float width, float height)
        {
            var label = HudPrimitives.Label("Row heading", row, 13f * Scale, UiTheme.Accent, TextAlignmentOptions.Center);
            label.text = Localisation.Text(words);
            label.characterSpacing = 4f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.enableAutoSizing = true;
            label.fontSizeMax = label.fontSize;
            label.fontSizeMin = Mathf.Min(10f, label.fontSize);
            EndScreenKit.Place(label.rectTransform, x, 0f, width, height);
        }

        /// <summary>One face on the card, on Pack 8's frame for it: the photo, its pill, the name under it.</summary>
        private RectTransform Card(RectTransform parent, string prefix, Player player, float x, float y, float width, float photo,
            Color tint, string frame)
        {
            float s = Scale;
            var card = EndScreenKit.Box(prefix + (player.Name ?? player.Id), parent, x, y, width, photo + 34f * s);
            EndScreenKit.Frame(card, frame, 10f * s, new Color(UiTheme.SurfaceRaised.r, UiTheme.SurfaceRaised.g, UiTheme.SurfaceRaised.b, .95f),
                new Color(tint.r, tint.g, tint.b, .6f));
            var picture = HudPrimitives.RectPortrait(card, "Photo", player.Portrait, player.Character, new Vector2(width - 10f * s, photo - 5f * s), 7);
            picture.anchorMin = picture.anchorMax = new Vector2(.5f, 1f);
            picture.pivot = new Vector2(.5f, 1f);
            picture.anchoredPosition = new Vector2(0f, -5f * s);
            if (!string.IsNullOrEmpty(player.Badge))
            {
                var pill = HudPrimitives.Fill("Badge", picture, tint, 4);
                pill.anchorMin = pill.anchorMax = new Vector2(.5f, 0f);
                pill.pivot = new Vector2(.5f, 0f);
                pill.anchoredPosition = new Vector2(0f, 4f * s);
                pill.sizeDelta = new Vector2(Mathf.Min(width - 16f * s, (player.Badge.Length * 7.5f + 16f) * s), 16f * s);
                var badge = HudPrimitives.Label("Badge text", pill, 11f * s, UiTheme.Ink, TextAlignmentOptions.Center);
                badge.text = Localisation.Text(player.Badge);
                badge.rectTransform.anchorMin = Vector2.zero; badge.rectTransform.anchorMax = Vector2.one;
                badge.rectTransform.offsetMin = Vector2.zero; badge.rectTransform.offsetMax = Vector2.zero;
            }
            var name = HudPrimitives.Label("Name", card, 14f * s, UiTheme.Paper, TextAlignmentOptions.Center);
            name.text = Localisation.Text(player.Name ?? string.Empty);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) name.font = semibold;
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.overflowMode = TextOverflowModes.Ellipsis;
            name.enableAutoSizing = true;
            name.fontSizeMax = name.fontSize;
            name.fontSizeMin = Mathf.Min(10f, name.fontSize);
            EndScreenKit.Place(name.rectTransform, 4f * s, photo + 6f * s, width - 8f * s, 22f * s);
            return card;
        }

        private void Chevron(RectTransform row, float centreX, float centreY)
        {
            float side = 26f * Scale;
            var arrow = UiTheme.Pack(PackArt.Pack8ArrowRight);
            if (arrow != null)
            {
                var image = Picture("Chevron", row, arrow, Color.white);
                EndScreenKit.Place(image, centreX - side * .5f, centreY - side * .5f, side, side);
                return;
            }
            var holder = EndScreenKit.Box("Chevron", row, centreX - side * .5f, centreY - side * .5f, side, side);
            HudPrimitives.Chevron(holder, UiTheme.Accent, side);
        }

        private static RectTransform Picture(string name, RectTransform parent, Sprite sprite, Color colour)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.rectTransform.SetParent(parent, false);
            image.sprite = sprite;
            image.color = colour;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image.rectTransform;
        }

        /// <summary>A slot's disc without the pack: the theme's circle, centred where the pack's would be.</summary>
        private static GameObject Disc(string name, RectTransform parent, Color colour, float centreX, float centreY, float side)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            var rect = image.rectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(centreX, -centreY);
            rect.sizeDelta = new Vector2(side, side);
            image.sprite = UiTheme.Circle();
            image.color = colour;
            image.raycastTarget = false;
            return image.gameObject;
        }
    }
}
