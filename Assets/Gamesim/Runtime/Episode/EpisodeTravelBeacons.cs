using System.Collections.Generic;
using Gamesim.House;
using Gamesim.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// Clickable icons over the house: one above each room, and a click takes the player there.
    ///
    /// <para>Two of them are more than rooms. The room the phase's screen stands in carries the
    /// screen - the episode screen, or the competition in a competition week - and the private
    /// room carries the diary: out of reach they go there, and within reach they open it, so the
    /// icon over the thing is the way to use the thing.</para>
    ///
    /// <para>Shown only while the camera is pulled back over the house. Close in, following
    /// somebody, the icons would sit on top of the people the shot is about, and a click meant
    /// for a houseguest would land on a room instead; the house is a map from far away and a set
    /// from close up. They never take the keyboard: every place they go has its own control, and
    /// the notebook's panels keep their Tab ring to themselves.</para>
    ///
    /// <para>Each icon names its room under it, and at the endgame the overlay names the few left
    /// in the house over their heads as well (MOCKUP-PASS M14): labels, never controls, shown and
    /// faded with the icons, at the distance the houseguests' own name plates have faded out.</para>
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(1000)]
    public sealed class EpisodeTravelBeacons : MonoBehaviour
    {
        public const string RootName = "Travel beacons";
        /// <summary>The count of houseguests on a room's icon, and the ring that marks the next stop.</summary>
        public const string BadgeName = "Houseguests here", NextStopName = "Next stop", PointerName = "Toward the next stop";
        public const string BeaconPrefix = "Beacon · ";
        public const string StationCaption = "Travel to episode screen";
        public const string CompetitionCaption = "Travel to competition";
        public const string DiaryCaption = "Travel to diary room";

        /// <summary>
        /// The room's name under its icon, and the name chips over the endgame's few (MOCKUP-PASS
        /// M14, mockup 60), so a test can find them. Labels, never controls.
        /// </summary>
        public const string RoomNameName = "Room name", NameChipPrefix = "Name chip · ";

        /// <summary>How far back the camera has to be before the icons show, and where they are fully in, in metres.</summary>
        public const float HiddenBelow = 14f, ShownFrom = 18f;

        /// <summary>Metres over a room's floor: above the walls, which are cut away to 1.1 m, and above heads.</summary>
        public const float Height = 2.9f;
        private const float Side = 46f, MarkSide = 26f;

        /// <summary>The caption a room's icon carries: its name, or what it is for.</summary>
        public static string Caption(string room, string stationRoom, bool competition)
        {
            if (room == stationRoom) return competition ? CompetitionCaption : StationCaption;
            if (room == "Private") return DiaryCaption;
            return "Travel to the " + RoomLabels.InSentence(room);
        }

        private sealed class Beacon
        {
            public string room;
            public RectTransform rect;
            public Button button;
            public Image mark, ring;
            public RectTransform badge;
            public TMP_Text count;
            public int shownCount = -1;
            public bool ringed, pinned;
            public RectTransform pointer;
            public TMP_Text caption;
            public GameObject tip, roomName;
            public CanvasGroup group;
            public bool hovered;
            public string glyph, said;
        }

        /// <summary>Tells its beacon the pointer is over it, for the tip that names what a click does.</summary>
        private sealed class Hover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            [System.NonSerialized] public Beacon beacon;
            public void OnPointerEnter(PointerEventData data) { if (beacon != null) beacon.hovered = true; }
            public void OnPointerExit(PointerEventData data) { if (beacon != null) beacon.hovered = false; }
        }

        private readonly List<Beacon> beacons = new List<Beacon>();
        private Canvas canvas;
        private RectTransform root;
        private float shownScale = -1f;

        /// <summary>
        /// The camera a screen point is measured against: none for the overlay the icons are, the
        /// canvas's own when something - a test capture - has turned it into a camera canvas.
        /// </summary>
        private Camera UiCamera => canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

        /// <summary>Whether any icon is on screen and can be clicked.</summary>
        public bool IsShowing { get; private set; }
        public int Count => beacons.Count;

        public static EpisodeTravelBeacons Attach(GameObject host)
        {
            var existing = host.GetComponentInChildren<EpisodeTravelBeacons>(true);
            if (existing != null) return existing;
            var holder = new GameObject(RootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            holder.transform.SetParent(host.transform, false);
            return holder.AddComponent<EpisodeTravelBeacons>();
        }

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Under the HUD, whose panels cover the house and everything on it; over the name
            // plates and a warp's dip.
            canvas.sortingOrder = 65;
            var scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = .5f;
            root = (RectTransform)transform;
        }

        /// <summary>One icon a room, built once for the house's rooms and kept.</summary>
        public void Build(IEnumerable<HouseRoomMarker> markers, System.Action<string> go)
        {
            foreach (var beacon in beacons) if (beacon.rect != null) Destroy(beacon.rect.gameObject);
            beacons.Clear();
            foreach (var marker in markers)
            {
                if (marker == null || string.IsNullOrEmpty(marker.RoomName)) continue;
                string room = marker.RoomName;
                var beacon = new Beacon { room = room };
                var rect = new GameObject(BeaconPrefix + room, typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
                rect.SetParent(root, false);
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                rect.pivot = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(Side, Side);
                beacon.rect = rect;
                beacon.group = rect.GetComponent<CanvasGroup>();

                // A glass disc with a hairline, as the room chips on the overview wear.
                var disc = HudPrimitives.Disc("Disc", rect, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, .9f));
                disc.anchorMin = Vector2.zero; disc.anchorMax = Vector2.one;
                disc.offsetMin = Vector2.zero; disc.offsetMax = Vector2.zero;
                var face = disc.GetComponent<Image>();
                face.raycastTarget = true;
                var ring = HudPrimitives.Disc("Ring", rect, new Color(UiTheme.Hairline.r, UiTheme.Hairline.g, UiTheme.Hairline.b, .7f));
                ring.anchorMin = Vector2.zero; ring.anchorMax = Vector2.one;
                ring.offsetMin = new Vector2(-2f, -2f); ring.offsetMax = new Vector2(2f, 2f);
                ring.SetAsFirstSibling();
                beacon.ring = ring.GetComponent<Image>();

                var mark = new GameObject("Mark", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                mark.rectTransform.SetParent(rect, false);
                mark.rectTransform.sizeDelta = new Vector2(MarkSide, MarkSide);
                mark.preserveAspect = true; mark.raycastTarget = false;
                beacon.mark = mark;

                // The words a click stands for, under the icon while the pointer is on it. The
                // text is always there, visible or not: it is the control's caption.
                var tip = HudPrimitives.Fill("Tip", rect, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, .94f), 10);
                tip.anchorMin = tip.anchorMax = new Vector2(.5f, 0f);
                tip.pivot = new Vector2(.5f, 1f);
                tip.anchoredPosition = new Vector2(0f, -6f);
                tip.GetComponent<Image>().raycastTarget = false;
                var caption = HudPrimitives.Label("Caption", tip, 15f, UiTheme.Paper, TextAlignmentOptions.Center);
                caption.textWrappingMode = TextWrappingModes.NoWrap;
                caption.raycastTarget = false;
                caption.rectTransform.anchorMin = Vector2.zero; caption.rectTransform.anchorMax = Vector2.one;
                caption.rectTransform.offsetMin = new Vector2(10f, 0f); caption.rectTransform.offsetMax = new Vector2(-10f, 0f);
                beacon.caption = caption;
                beacon.tip = tip.gameObject;

                // How many houseguests are in the room, on the icon's shoulder: where the house is
                // is where the talking is. A label, not a control - the icon takes the click - and
                // made after the caption, so the caption stays the icon's first words.
                var badge = HudPrimitives.Disc(BadgeName, rect, UiTheme.Accent);
                badge.anchorMin = badge.anchorMax = new Vector2(1f, 1f);
                badge.pivot = new Vector2(.5f, .5f);
                badge.sizeDelta = new Vector2(20f, 20f);
                badge.anchoredPosition = new Vector2(-4f, -4f);
                badge.GetComponent<Image>().raycastTarget = false;
                var count = HudPrimitives.Label("Count", badge, 12f, UiTheme.Ink, TextAlignmentOptions.Center);
                count.textWrappingMode = TextWrappingModes.NoWrap;
                count.raycastTarget = false;
                count.rectTransform.anchorMin = Vector2.zero; count.rectTransform.anchorMax = Vector2.one;
                count.rectTransform.offsetMin = Vector2.zero; count.rectTransform.offsetMax = Vector2.zero;
                badge.gameObject.SetActive(false);
                beacon.badge = badge; beacon.count = count;

                // The pip on the rim of a next stop that has had to wait somewhere clear, on the
                // side its place is on.
                var pointer = HudPrimitives.Disc(PointerName, rect, UiTheme.Accent);
                pointer.anchorMin = pointer.anchorMax = new Vector2(.5f, .5f);
                pointer.pivot = new Vector2(.5f, .5f);
                pointer.sizeDelta = new Vector2(10f, 10f);
                pointer.GetComponent<Image>().raycastTarget = false;
                pointer.gameObject.SetActive(false);
                beacon.pointer = pointer;

                // The room's own name under its icon, in the floor paint's capitals the overview's
                // chips use (MOCKUP-PASS M14, mockup 60): a map should say which room is which
                // without a hover. A label, not a control, faded with the icon it hangs from, and
                // made after the caption so the caption stays the icon's first words. The hover's
                // tip takes its place while it is up - the tip says what a click on it does.
                var nameTag = HudPrimitives.Fill(RoomNameName, rect, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, .82f), 8);
                nameTag.anchorMin = nameTag.anchorMax = new Vector2(.5f, 0f);
                nameTag.pivot = new Vector2(.5f, 1f);
                nameTag.anchoredPosition = new Vector2(0f, -4f);
                nameTag.GetComponent<Image>().raycastTarget = false;
                var title = HudPrimitives.Heading("Words", nameTag, 11f, UiTheme.Paper, TextAlignmentOptions.Center);
                title.characterSpacing = 3f;
                title.textWrappingMode = TextWrappingModes.NoWrap;
                title.raycastTarget = false;
                title.text = Localisation.Text(RoomLabels.Title(room));
                title.rectTransform.anchorMin = Vector2.zero; title.rectTransform.anchorMax = Vector2.one;
                title.rectTransform.offsetMin = new Vector2(8f, 0f); title.rectTransform.offsetMax = new Vector2(-8f, 0f);
                // 20 tall for an 11: well over the 1.3 times its words a label needs to draw.
                nameTag.sizeDelta = new Vector2(Mathf.Ceil(title.GetPreferredValues(title.text).x) + 20f, 20f);
                beacon.roomName = nameTag.gameObject;

                var button = rect.gameObject.AddComponent<Button>();
                button.targetGraphic = face;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                var colours = button.colors;
                colours.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
                colours.pressedColor = new Color(.8f, .8f, .8f, 1f);
                button.colors = colours;
                button.onClick.AddListener(() =>
                {
                    // A click selects a Button, and the HUD reads a selection as keyboard focus.
                    if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == rect.gameObject)
                        EventSystem.current.SetSelectedGameObject(null);
                    beacon.hovered = false;
                    go?.Invoke(room);
                });
                beacon.button = button;
                rect.gameObject.AddComponent<Hover>().beacon = beacon;
                beacons.Add(beacon);
            }
            shownScale = -1f;
            Hide();
        }

        public const string FurnitureTipName = "Furniture tip";
        private RectTransform furnitureTip;
        private TMP_Text furnitureTipText;

        /// <summary>
        /// Names what a click on the furniture under the pointer would do, just below the pointer;
        /// null takes it away. It takes no click: the click is the furniture's.
        /// </summary>
        public void ShowFurnitureTip(string text, Vector2 screen, float textScale)
        {
            if (string.IsNullOrEmpty(text))
            {
                if (furnitureTip != null && furnitureTip.gameObject.activeSelf) furnitureTip.gameObject.SetActive(false);
                return;
            }
            if (furnitureTip == null)
            {
                furnitureTip = HudPrimitives.Fill(FurnitureTipName, root, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, .94f), 10);
                furnitureTip.anchorMin = furnitureTip.anchorMax = Vector2.zero;
                furnitureTip.pivot = new Vector2(.5f, 1f);
                furnitureTip.GetComponent<Image>().raycastTarget = false;
                furnitureTipText = HudPrimitives.Label("Caption", furnitureTip, 15f, UiTheme.Paper, TextAlignmentOptions.Center);
                furnitureTipText.textWrappingMode = TextWrappingModes.NoWrap;
                furnitureTipText.raycastTarget = false;
                furnitureTipText.rectTransform.anchorMin = Vector2.zero; furnitureTipText.rectTransform.anchorMax = Vector2.one;
                furnitureTipText.rectTransform.offsetMin = new Vector2(10f, 0f); furnitureTipText.rectTransform.offsetMax = new Vector2(-10f, 0f);
            }
            if (!furnitureTip.gameObject.activeSelf) furnitureTip.gameObject.SetActive(true);
            furnitureTipText.text = Localisation.Text(text);
            furnitureTip.localScale = Vector3.one * textScale;
            furnitureTip.sizeDelta = new Vector2(Mathf.Ceil(furnitureTipText.GetPreferredValues(furnitureTipText.text).x) + 24f, 30f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, UiCamera, out var local);
            furnitureTip.anchoredPosition = local - root.rect.min + new Vector2(0f, -22f);
        }

        /// <summary>Takes every icon off the screen, and out of the way of clicks.</summary>
        public void Hide()
        {
            IsShowing = false;
            foreach (var beacon in beacons)
            {
                // An icon switched off never hears the pointer leave, so it forgets the hover here.
                beacon.hovered = false;
                if (beacon.tip != null && beacon.tip.activeSelf) beacon.tip.SetActive(false);
                if (beacon.rect != null && beacon.rect.gameObject.activeSelf) beacon.rect.gameObject.SetActive(false);
            }
            namesShowing = false;
            foreach (var chip in names)
                if (chip.rect != null && chip.rect.gameObject.activeSelf) chip.rect.gameObject.SetActive(false);
        }

        /// <summary>One houseguest to name over the house: who, the words, and what to hang them over.</summary>
        public readonly struct Named
        {
            public readonly string Id, Words;
            /// <summary>The head bone, when the body has resolved one; null falls back to the root.</summary>
            public readonly Transform Head;
            public readonly Transform Body;
            public Named(string id, string words, Transform head, Transform body) { Id = id; Words = words; Head = head; Body = body; }
        }

        private sealed class NameChip
        {
            public string id;
            public RectTransform rect;
            public TMP_Text word;
            public CanvasGroup group;
            public Transform head, body;
            /// <summary>Whether the ground has been fitted to the words since they last changed. Measuring the words does not need the chip to be showing.</summary>
            public bool fitted;
        }

        private readonly List<NameChip> names = new List<NameChip>();
        private bool namesShowing;

        /// <summary>The screen rects, in pixels, of the chips placed so far this frame. Kept, so placing them makes no garbage.</summary>
        private readonly List<Rect> placedNames = new List<Rect>();

        /// <summary>
        /// Metres over the head bone a name chip floats, clear of the hair; and over the root, for a
        /// body with no head bone to read yet, about a standing head's height and the same air.
        /// </summary>
        private const float OverHead = .32f, OverRoot = 2.05f;

        /// <summary>Whether a houseguest's name chip is on screen. A read for tests.</summary>
        public bool IsNaming(string id)
        {
            foreach (var chip in names)
                if (chip.id == id) return chip.rect != null && chip.rect.gameObject.activeInHierarchy;
            return false;
        }

        /// <summary>
        /// Who wears a name chip while the icons show (MOCKUP-PASS M14, mockup 60): at the endgame
        /// the few left in the house, whom the player has to find from across it after the name
        /// plates have faded out with the distance. Null or empty names nobody. Kept between calls:
        /// a chip is made once for a person and moved every frame.
        ///
        /// <para>The order matters. Chips are placed in the order they are given, and where two
        /// would cover each other the one given first stays over its head while the later one is
        /// lifted clear of it, so the caller puts first the chip that should never move.</para>
        /// </summary>
        public void Name(IList<Named> people)
        {
            for (int i = names.Count - 1; i >= 0; i--)
            {
                bool kept = false;
                if (people != null)
                    foreach (var person in people)
                        if (person.Id == names[i].id) { kept = true; break; }
                if (kept) continue;
                if (names[i].rect != null) Destroy(names[i].rect.gameObject);
                names.RemoveAt(i);
            }
            if (people == null) return;
            // The chips before this index are in the order given. Every chip after it belongs to
            // somebody still to come in the list, since everybody else was taken away above.
            int ordered = 0;
            foreach (var person in people)
            {
                int at = -1;
                for (int i = 0; i < names.Count; i++)
                    if (names[i].id == person.Id) { at = i; break; }
                // Somebody named twice keeps the chip and the place of the first time.
                if (at >= 0 && at < ordered) continue;
                NameChip chip;
                if (at < 0) { chip = NewNameChip(person.Id); names.Insert(ordered, chip); }
                else
                {
                    chip = names[at];
                    if (at != ordered) { names.RemoveAt(at); names.Insert(ordered, chip); }
                }
                ordered++;
                chip.head = person.Head;
                chip.body = person.Body;
                if (chip.word.text == person.Words) continue;
                chip.word.text = person.Words;
                chip.fitted = false;
            }
        }

        /// <summary>
        /// A name on the icons' glass: no control, nothing that takes a click, and under every
        /// icon, so a chip that drifts across a room's icon never hides the thing to press.
        /// </summary>
        private NameChip NewNameChip(string id)
        {
            var rect = new GameObject(NameChipPrefix + id, typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            rect.SetParent(root, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(.5f, 0f);
            var group = rect.GetComponent<CanvasGroup>();
            group.interactable = false; group.blocksRaycasts = false;
            var ground = HudPrimitives.Fill("Ground", rect, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, .88f), 10);
            ground.anchorMin = Vector2.zero; ground.anchorMax = Vector2.one;
            ground.offsetMin = Vector2.zero; ground.offsetMax = Vector2.zero;
            UiTheme.AddBorder(ground, 10, UiTheme.Edge(UiTheme.Emphasis.Resting));
            var word = HudPrimitives.Label("Name", rect, 13f, UiTheme.Paper, TextAlignmentOptions.Center);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) word.font = semibold;
            word.textWrappingMode = TextWrappingModes.NoWrap;
            word.raycastTarget = false;
            // 24 tall for a 13: over the 1.3 times its words a label needs to draw.
            word.rectTransform.anchorMin = Vector2.zero; word.rectTransform.anchorMax = Vector2.one;
            word.rectTransform.offsetMin = new Vector2(10f, 0f); word.rectTransform.offsetMax = new Vector2(-10f, 0f);
            rect.sizeDelta = new Vector2(60f, 24f);
            rect.gameObject.SetActive(false);
            return new NameChip { id = id, rect = rect, word = word, group = group };
        }

        /// <summary>
        /// Hangs each name chip over its houseguest's head, faded with the icons. The head bone
        /// rather than the root: a body can stand metres from its transform, and a seated one is
        /// lower than its root says.
        ///
        /// <para>Two people close together, two finalists talking or sharing the couch, would wear
        /// one chip over the other, and the one drawn on top would hide the other's name. So the
        /// chips are placed in the order they were given, and a chip that would touch one already
        /// placed is lifted clear of it. The first one stays over its head.</para>
        ///
        /// <para>A chip is shown only when all of it is on the screen and clear of the HUD's
        /// chrome, not just the point it hangs from. The chrome draws over this canvas, and a name
        /// half under the right column or the strip reads as a different name.</para>
        /// </summary>
        private void PlaceNames(Camera eye, Rect frame, float fade)
        {
            bool any = false;
            // Canvas units to screen pixels, for the chips' footprints and the air between them.
            float pixels = scale * canvas.scaleFactor;
            var bounds = eye.pixelRect;
            placedNames.Clear();
            foreach (var chip in names)
            {
                if (chip.rect == null) continue;
                bool show = false;
                Rect box = default;
                bool headed = chip.head != null;
                if (headed || chip.body != null)
                {
                    var at = headed ? chip.head.position + Vector3.up * OverHead : chip.body.position + Vector3.up * OverRoot;
                    var screen = eye.WorldToScreenPoint(at);
                    if (screen.z > .5f)
                    {
                        // Fitted before it is tested, so the test is of the whole chip as it will be drawn.
                        if (!chip.fitted)
                        {
                            chip.rect.sizeDelta = new Vector2(Mathf.Ceil(chip.word.GetPreferredValues(chip.word.text).x) + 24f, 24f);
                            chip.fitted = true;
                        }
                        var size = chip.rect.sizeDelta * pixels;
                        // The chip hangs from the middle of its bottom edge.
                        var over = new Rect(screen.x - size.x * .5f, screen.y, size.x, size.y);
                        box = over;
                        LiftClear(ref box, 2f * pixels);
                        show = bounds.Contains(box.min) && bounds.Contains(box.max) && !ChipCovered(box);
                        // Lifted off the frame or into the chrome - a finalist under the top bar -
                        // the name tries the other side, under what it stood clear of, before it
                        // is put away.
                        if (!show && box.y != over.y)
                        {
                            var under = over;
                            LowerClear(ref under, 2f * pixels);
                            if (bounds.Contains(under.min) && bounds.Contains(under.max) && !ChipCovered(under)) { box = under; show = true; }
                        }
                    }
                }
                if (!show)
                {
                    if (chip.rect.gameObject.activeSelf) chip.rect.gameObject.SetActive(false);
                    continue;
                }
                if (!chip.rect.gameObject.activeSelf) chip.rect.gameObject.SetActive(true);
                any = true;
                placedNames.Add(box);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root, new Vector2(box.center.x, box.yMin), UiCamera, out var local);
                chip.rect.anchoredPosition = local - frame.min;
                chip.rect.localScale = Vector3.one * scale;
                chip.group.alpha = fade;
            }
            namesShowing = any;
        }

        /// <summary>The icons placed this frame, in screen pixels, with their badges' reach: a name chip stands clear of them.</summary>
        private readonly List<Rect> placedIcons = new List<Rect>();

        /// <summary>
        /// Moves a chip's screen rect up until it is at least <paramref name="gap"/> clear of every
        /// chip placed before it this frame and of every icon on screen. Each move takes it above
        /// one placed rect's top, and it only ever goes up, so it passes each at most once and the
        /// loop ends.
        ///
        /// <para>The icons too: a chip is drawn under them, so a houseguest standing at a room's
        /// icon wore their name half under it, and what showed read as the icon's own - the yard's
        /// count of one over the tail of "Taylor" read "1 of" on its badge (endgame-final-three;
        /// UI-UX-PASS-PLAN T0).</para>
        /// </summary>
        private void LiftClear(ref Rect box, float gap)
        {
            for (int pass = 0; pass <= placedNames.Count + placedIcons.Count; pass++)
            {
                bool moved = Past(placedNames, ref box, gap, true);
                moved |= Past(placedIcons, ref box, gap, true);
                if (!moved) return;
            }
        }

        /// <summary>
        /// The same, the other way: moves a chip's screen rect down until it is clear of every chip
        /// placed and every icon, for a name that lifting took off the frame or under the chrome.
        /// </summary>
        private void LowerClear(ref Rect box, float gap)
        {
            for (int pass = 0; pass <= placedNames.Count + placedIcons.Count; pass++)
            {
                bool moved = Past(placedNames, ref box, gap, false);
                moved |= Past(placedIcons, ref box, gap, false);
                if (!moved) return;
            }
        }

        /// <summary>
        /// Moves <paramref name="box"/> past every rect of <paramref name="placed"/> it touches,
        /// above it when <paramref name="up"/> and below it otherwise. Indexed, not enumerated
        /// through an array of the lists: this runs for every chip every frame.
        /// </summary>
        private static bool Past(List<Rect> placed, ref Rect box, float gap, bool up)
        {
            bool moved = false;
            for (int i = 0; i < placed.Count; i++)
            {
                var other = placed[i];
                bool touches = box.xMin < other.xMax + gap && box.xMax > other.xMin - gap
                    && box.yMin < other.yMax + gap && box.yMax > other.yMin - gap;
                if (!touches) continue;
                box.y = up ? other.yMax + gap : other.yMin - gap - box.height;
                moved = true;
            }
            return moved;
        }

        /// <summary>
        /// Whether the chrome covers any of a chip: its bottom, middle and top, at both ends and at
        /// points between them no further apart than the chip is tall, so a narrow piece of chrome
        /// cannot slip between the points tested.
        /// </summary>
        private bool ChipCovered(Rect box)
        {
            // Given the chrome as rects, the chip is one test.
            if (coveredBox != null) return coveredBox(box);
            if (covered == null) return false;
            int spans = Mathf.Max(2, Mathf.CeilToInt(box.width / Mathf.Max(1f, box.height)));
            for (int x = 0; x <= spans; x++)
                for (int y = 0; y <= 2; y++)
                    if (covered(new Vector2(box.xMin + box.width * x / spans, box.yMin + box.height * y * .5f))) return true;
            return false;
        }

        private bool wanted, competition;
        private HouseCameraRig rig;
        private float scale = 1f;
        private string stationRoom, playerRoom;
        private System.Func<string, Vector3> where;
        private System.Func<Vector2, bool> covered;
        /// <summary>The chrome asked about a whole screen rect at once, when the caller has it so; preferred over the points.</summary>
        private System.Func<Rect, bool> coveredBox;
        private System.Func<string, int> occupants;
        private string nextStop;

        /// <summary>How many houseguests the icon over a room says are in it, as last shown; -1 while it is hidden.</summary>
        public int ShownCount(string room)
        {
            foreach (var beacon in beacons)
                if (beacon.room == room) return beacon.rect != null && beacon.rect.gameObject.activeInHierarchy ? beacon.shownCount : -1;
            return -1;
        }

        /// <summary>The room whose icon is marked as the next stop, while one is.</summary>
        public string NextStop => nextStop;

        /// <summary>Whether a room's icon is on screen away from its place, waiting somewhere clear because its place is not.</summary>
        public bool IsPinned(string room)
        {
            foreach (var beacon in beacons)
                if (beacon.room == room) return beacon.pinned && beacon.rect != null && beacon.rect.gameObject.activeInHierarchy;
            return false;
        }

        /// <summary>
        /// Who is where, and where the player is being sent next: the counts on the icons and the
        /// ring round the one the objective names. Either may be null.
        /// </summary>
        public void Annotate(System.Func<string, int> countIn, string nextStopRoom, string stagedRoom = null)
        {
            occupants = countIn;
            nextStop = nextStopRoom;
            dramaRoom = stagedRoom;
        }

        /// <summary>
        /// The room a story moment is being acted out in, whose icon wears the drama mark (plan
        /// §5.1) - the mark only: its caption is how the icon is found, and the ring still belongs
        /// to the next stop, because an optional scene teases rather than nags.
        /// </summary>
        public string DramaRoom => dramaRoom;
        private string dramaRoom;
        private const string DramaGlyph = "drama";

        /// <summary>
        /// What this frame's icons should show, from the director's Update. They are placed in
        /// LateUpdate, after the camera has moved: placed any earlier, every icon trails the view
        /// by a frame and swims whenever it pans. <paramref name="where"/> says where a room's icon
        /// floats over the floor; the room the player is standing in is left out, unless there is
        /// something in it to open. The chrome is asked about points (<paramref name="underChrome"/>),
        /// or - when the caller can say it so - about whole screen rects (<paramref name="underChromeBox"/>),
        /// which makes an icon's square and the room's name under it one question each.
        /// </summary>
        public void Request(bool visible, HouseCameraRig camera, float textScale, string screenRoom, bool isCompetition,
            string standingIn, System.Func<string, Vector3> anchor, System.Func<Vector2, bool> underChrome = null,
            System.Func<Rect, bool> underChromeBox = null)
        {
            wanted = visible; rig = camera; scale = textScale; stationRoom = screenRoom; competition = isCompetition;
            playerRoom = standingIn; where = anchor; covered = underChrome; coveredBox = underChromeBox;
            if (!visible) Hide();
        }

        private void LateUpdate()
        {
            if (!wanted || rig == null || where == null) { if (IsShowing || namesShowing) Hide(); return; }
            Place(rig.ViewCamera, rig.Distance);
        }

        private void Place(Camera eye, float cameraDistance)
        {
            if (eye == null || beacons.Count == 0) { Hide(); return; }
            float fade = Mathf.InverseLerp(HiddenBelow, ShownFrom, cameraDistance);
            if (fade <= 0f) { Hide(); return; }
            if (!Mathf.Approximately(shownScale, scale))
            {
                shownScale = scale;
                foreach (var beacon in beacons) { beacon.rect.localScale = Vector3.one * scale; beacon.glyph = null; beacon.said = null; }
            }
            var frame = root.rect;
            bool any = false;
            // Canvas units to screen pixels, for the icons' footprints.
            float pixels = scale * canvas.scaleFactor;
            // Middle to edge of an icon, in screen pixels, with a little air.
            float margin = (Side * .5f + 6f) * pixels;
            // The icons' footprints this frame, with their badges' reach, for the name chips to clear.
            float footprint = (Side * .5f + 8f) * pixels;
            placedIcons.Clear();
            foreach (var beacon in beacons)
            {
                bool special = beacon.room == stationRoom || beacon.room == "Private";
                bool show = beacon.room != playerRoom || special;
                Vector3 screen = default;
                bool pinned = false;
                Vector2 toward = default;
                if (show)
                {
                    screen = eye.WorldToScreenPoint(where(beacon.room) + Vector3.up * Height);
                    // An icon under the HUD's chrome is not shown: it would be half hidden, and
                    // where the chrome takes no click, clickable without being seen. Any of it, not
                    // just its middle: an icon half under the status line was still being shown,
                    // and the room's name under the icon - wider than it, hung below the square -
                    // showed through the cards while the icon itself stood clear.
                    show = screen.z > .5f && eye.pixelRect.Contains(screen) && !IconCovered(beacon, screen, margin, pixels);
                    // Except the next stop, which is never lost that way: its icon waits at the
                    // first clear spot on the way from its place to the middle of the screen,
                    // whole and clickable, with a pip on the side its place is on.
                    if (!show && beacon.room == nextStop && screen.z > .5f
                        && TryPin(eye.pixelRect, screen, margin, beacon, pixels, out var clear))
                    {
                        toward = ((Vector2)screen - clear).normalized;
                        screen = new Vector3(clear.x, clear.y, screen.z);
                        show = pinned = true;
                    }
                }
                if (!show)
                {
                    if (beacon.rect.gameObject.activeSelf) beacon.rect.gameObject.SetActive(false);
                    beacon.hovered = false;
                    continue;
                }
                if (!beacon.rect.gameObject.activeSelf) beacon.rect.gameObject.SetActive(true);
                any = true;
                placedIcons.Add(new Rect(screen.x - footprint, screen.y - footprint, 2f * footprint, 2f * footprint));
                // The room's name hung under the icon is part of what a name chip must clear, as the
                // chrome treats it (IconCovered): a chip over "KITCHEN" read as the room's own words.
                if (beacon.roomName != null && !beacon.hovered) placedIcons.Add(TagRect(beacon, screen, pixels));
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, UiCamera, out var local);
                beacon.rect.anchoredPosition = local - frame.min;
                beacon.group.alpha = fade;
                beacon.group.blocksRaycasts = fade > .5f;
                beacon.group.interactable = fade > .5f;

                bool staged = beacon.room == dramaRoom && beacon.room != stationRoom && beacon.room != "Private";
                string glyph = staged ? DramaGlyph : beacon.room == stationRoom ? (competition ? "trophy" : "camera") : RoomLabels.Glyph(beacon.room);
                // Keyed on the words, not the glyph: the yard wears the trophy as a room and as the
                // competition's screen, and only its caption says which.
                string said = Caption(beacon.room, stationRoom, competition);
                if (glyph != beacon.glyph || said != beacon.said)
                {
                    beacon.glyph = glyph; beacon.said = said;
                    beacon.mark.sprite = staged ? UiTheme.Pack(PackArt.IconDrama) ?? UiTheme.Icon(RoomLabels.Glyph(beacon.room)) : UiTheme.Icon(glyph);
                    beacon.mark.enabled = beacon.mark.sprite != null;
                    // Gold is power: the Head of Household's suite wears it here as it does on the map.
                    // The drama mark is pack art in its own colours.
                    beacon.mark.color = staged ? Color.white : beacon.room == "HoH" ? UiTheme.Gold : special ? UiTheme.Accent : UiTheme.Heading;
                    beacon.caption.text = Localisation.Text(said);
                    var tipRect = (RectTransform)beacon.tip.transform;
                    tipRect.sizeDelta = new Vector2(Mathf.Ceil(beacon.caption.GetPreferredValues(beacon.caption.text).x) + 24f, 30f);
                }
                if (beacon.tip.activeSelf != beacon.hovered) beacon.tip.SetActive(beacon.hovered);
                if (beacon.roomName != null && beacon.roomName.activeSelf == beacon.hovered) beacon.roomName.SetActive(!beacon.hovered);
                if (beacon.pinned != pinned) { beacon.pinned = pinned; beacon.pointer.gameObject.SetActive(pinned); }
                if (pinned) beacon.pointer.anchoredPosition = toward * (Side * .5f + 6f);

                int here = occupants != null ? Mathf.Max(0, occupants(beacon.room)) : 0;
                if (here != beacon.shownCount)
                {
                    beacon.shownCount = here;
                    beacon.badge.gameObject.SetActive(here > 0);
                    beacon.count.text = here > 9 ? "9+" : here.ToString();
                }
                // The next stop's ring is lit, and breathes unless motion is reduced.
                bool next = beacon.room == nextStop;
                float breathe = next && !rig.ReducedMotion ? .5f + .5f * Mathf.Sin(Time.unscaledTime * 3f) : 1f;
                beacon.ring.color = next ? new Color(UiTheme.Accent.r, UiTheme.Accent.g, UiTheme.Accent.b, .55f + .45f * breathe)
                    : new Color(UiTheme.Hairline.r, UiTheme.Hairline.g, UiTheme.Hairline.b, .7f);
                float reach = next ? 4f + 2f * breathe : 2f;
                beacon.ring.rectTransform.offsetMin = new Vector2(-reach, -reach);
                beacon.ring.rectTransform.offsetMax = new Vector2(reach, reach);
                // Renamed only when it changes: reading a name back makes a string every frame.
                if (beacon.ringed != next) { beacon.ringed = next; beacon.ring.name = next ? NextStopName : "Ring"; }
            }
            IsShowing = any;
            PlaceNames(eye, frame, fade);
        }

        /// <summary>
        /// The first spot on the way from <paramref name="target"/> to the middle of the screen
        /// where the whole icon - <paramref name="margin"/> from its middle to its edge, and the
        /// room's name under it - is on the screen and clear of the HUD's chrome.
        /// </summary>
        private bool TryPin(Rect pixels, Vector2 target, float margin, Beacon beacon, float scalePixels, out Vector2 at)
        {
            at = default;
            float tag = TagDepth(beacon, scalePixels);
            var inner = new Rect(pixels.xMin + margin, pixels.yMin + tag, pixels.width - 2f * margin, pixels.height - margin - tag);
            if (inner.width <= 0f || inner.height <= 0f) return false;
            const int Steps = 24;
            for (int step = 0; step <= Steps; step++)
            {
                var point = Vector2.Lerp(target, pixels.center, step / (float)Steps);
                if (!inner.Contains(point) || IconCovered(beacon, point, margin, scalePixels)) continue;
                at = point;
                return true;
            }
            return false;
        }

        /// <summary>Whether the chrome covers an icon here: its middle, or anywhere on the square round it.</summary>
        private bool Covered(Vector2 point, float margin)
        {
            if (covered == null) return false;
            if (covered(point)) return true;
            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                    if ((x != 0 || y != 0) && covered(point + new Vector2(x * margin, y * margin))) return true;
            return false;
        }

        /// <summary>How far under an icon's middle the room's name reaches, in screen pixels; the margin where there is no name.</summary>
        private static float TagDepth(Beacon beacon, float scalePixels)
        {
            if (beacon.roomName == null) return (Side * .5f + 6f) * scalePixels;
            var tag = (RectTransform)beacon.roomName.transform;
            return (Side * .5f - tag.anchoredPosition.y + tag.sizeDelta.y) * scalePixels;
        }

        /// <summary>
        /// Whether the chrome covers any of an icon drawn with its middle at <paramref name="point"/>:
        /// the square round it, and the room's name under it. A name half under a card read as
        /// another room's (the play sweep's row 10). Asked as two rects when the chrome can be;
        /// otherwise as points along the name's top and bottom edges no further apart than it is
        /// tall, so a narrow piece of chrome cannot slip between them.
        /// </summary>
        private bool IconCovered(Beacon beacon, Vector2 point, float margin, float scalePixels)
        {
            if (coveredBox != null)
                return coveredBox(new Rect(point.x - margin, point.y - margin, 2f * margin, 2f * margin))
                    || (beacon.roomName != null && coveredBox(TagRect(beacon, point, scalePixels)));
            if (Covered(point, margin)) return true;
            if (covered == null || beacon.roomName == null) return false;
            var tag = TagRect(beacon, point, scalePixels);
            int spans = Mathf.Max(2, Mathf.CeilToInt(tag.width / Mathf.Max(1f, tag.height)));
            for (int x = 0; x <= spans; x++)
            {
                float at = tag.xMin + tag.width * x / spans;
                if (covered(new Vector2(at, tag.yMax)) || covered(new Vector2(at, tag.yMin))) return true;
            }
            return false;
        }

        /// <summary>The room's name under an icon drawn with its middle at <paramref name="point"/>, in screen pixels.</summary>
        private static Rect TagRect(Beacon beacon, Vector2 point, float scalePixels)
        {
            var tag = (RectTransform)beacon.roomName.transform;
            float width = tag.sizeDelta.x * scalePixels, height = tag.sizeDelta.y * scalePixels;
            float top = point.y - (Side * .5f - tag.anchoredPosition.y) * scalePixels;
            return new Rect(point.x - width * .5f, top - height, width, height);
        }
    }
}
