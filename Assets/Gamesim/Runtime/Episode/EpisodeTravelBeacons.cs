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
            /// <summary>Where the icon was placed this frame, in screen pixels, and whether it was: a name chip of someone the view hides hangs over it (D17).</summary>
            public Vector2 placedAt;
            public bool placed, atDoorway;
            /// <summary>This frame's doorway from the viewed room that leads to the room (D17), its x NaN where none does or outside the room view.</summary>
            public Vector3 door;
            /// <summary>
            /// The air over the icon this frame's name chips of the few the room view hides in the room hang in (D17(3)), in
            /// screen pixels: the widest chip, and the chips' height with the air between them; 0 where nobody hidden is named.
            /// </summary>
            public float airWidth, airReach;
            /// <summary>How many name chips hang in that air this frame.</summary>
            public int airNames;
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
            var holder = new GameObject(RootName, typeof(RectTransform), typeof(Canvas), typeof(FrameScaler), typeof(GraphicRaycaster));
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
            /// <summary>The room the room view hides them in this frame (D17), null for somebody it draws and outside the room view.</summary>
            public string hiddenIn;
            /// <summary>The body's houseguest, whose plate the room view shows over a drawn body (RS5); none for the player.</summary>
            public HouseNpc plate;
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
                if (chip.body != person.Body)
                {
                    chip.body = person.Body;
                    chip.plate = person.Body != null ? person.Body.GetComponent<HouseNpc>() : null;
                }
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
        ///
        /// <para>In the room view (D17, RS5) somebody it hides wears the name over their room's icon,
        /// in that icon's column, and a houseguest it draws wears the plate it shows over every drawn
        /// body instead: the player alone, who has no plate, keeps a chip over the head.</para>
        /// </summary>
        private void PlaceNames(Camera eye, Rect frame, float fade)
        {
            bool any = false;
            // Canvas units to screen pixels, for the chips' footprints and the air between them.
            float pixels = scale * canvas.scaleFactor;
            float gap = 2f * pixels;
            var bounds = eye.pixelRect;
            placedNames.Clear();
            foreach (var chip in names)
            {
                if (chip.rect == null) continue;
                bool show = false;
                Rect box = default;
                bool headed = chip.head != null;
                // Someone the room view hides wears the name over their room's icon (D17), not over a body
                // that is not drawn; with that icon not shown, no name. Read as the icons were placed (Place).
                string hiddenIn = chip.hiddenIn;
                var own = hiddenIn != null ? PlacedBeacon(hiddenIn) : null;
                // And a houseguest the room view draws wears their plate, fully shown there (RS5): a chip over it as well
                // named them twice, one name over the other (the fixes' review).
                bool plated = roomViewRoom != null && hiddenIn == null && chip.plate != null && chip.plate.RoomViewPlates;
                if (own != null || (hiddenIn == null && !plated && (headed || chip.body != null)))
                {
                    var screen = own != null ? new Vector3(own.placedAt.x, own.placedAt.y + (Side * .5f + 8f) * pixels, 1f)
                        : eye.WorldToScreenPoint(headed ? chip.head.position + Vector3.up * OverHead : chip.body.position + Vector3.up * OverRoot);
                    if (screen.z > .5f)
                    {
                        // Fitted before it is tested, so the test is of the whole chip as it will be drawn.
                        Fit(chip);
                        var size = chip.rect.sizeDelta * pixels;
                        // The chip hangs from the middle of its bottom edge.
                        var over = new Rect(screen.x - size.x * .5f, screen.y, size.x, size.y);
                        // A hidden finalist's name slides along its row into the frame, still over the icon, as the air kept
                        // for it does (AirRect): over an icon at the frame's side it hung half off it (the claims' second review).
                        if (own != null) over.x = Slide(over.x, over.width, bounds);
                        box = over;
                        if (own != null)
                        {
                            // Over the icon, moved past it and the names before it only, and shown only where it reads as
                            // the room's: on no other room's icon or name, and nearer its own icon than any other. Lifted
                            // past a neighbour's icon as well, the name hung over the neighbour's (the fixes' review). Where
                            // the space over it is not clear, under the room's name, the same way.
                            ClearOwn(own, ref box, gap, true, pixels);
                            show = OnFrameAndClear(box, bounds) && !ReadsAsAnother(own, box, gap);
                            if (!show)
                            {
                                var under = over;
                                ClearOwn(own, ref under, gap, false, pixels);
                                if (OnFrameAndClear(under, bounds) && !ReadsAsAnother(own, under, gap)) { box = under; show = true; }
                            }
                        }
                        else
                        {
                            LiftClear(ref box, gap);
                            show = OnFrameAndClear(box, bounds);
                            // Lifted off the frame or into the chrome - a finalist under the top bar -
                            // the name tries the other side, under what it stood clear of, before it
                            // is put away.
                            if (!show && box.y != over.y)
                            {
                                var under = over;
                                LowerClear(ref under, gap);
                                if (OnFrameAndClear(under, bounds)) { box = under; show = true; }
                            }
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

        /// <summary>Fits a chip's ground to its words, once after they change.</summary>
        private static void Fit(NameChip chip)
        {
            if (chip.fitted) return;
            chip.rect.sizeDelta = new Vector2(Mathf.Ceil(chip.word.GetPreferredValues(chip.word.text).x) + 24f, 24f);
            chip.fitted = true;
        }

        /// <summary>Whether a chip's screen rect is all on the frame and clear of the chrome.</summary>
        private bool OnFrameAndClear(Rect box, Rect bounds) => bounds.Contains(box.min) && bounds.Contains(box.max) && !ChipCovered(box);

        /// <summary>A room's icon as placed this frame, or null while it is not shown.</summary>
        private Beacon PlacedBeacon(string room)
        {
            foreach (var beacon in beacons)
                if (beacon.room == room && beacon.placed) return beacon;
            return null;
        }

        /// <summary>
        /// Moves the name chip of somebody the room view hides past their room's icon - its square with the badges' reach,
        /// and the room's name under it - and past the chips placed before it, up or down, and past nothing else (D17(3)):
        /// it stays in that icon's column. Each move takes it past one rect it touches, one way, so the loop ends.
        /// </summary>
        private void ClearOwn(Beacon own, ref Rect box, float gap, bool up, float scalePixels)
        {
            float footprint = (Side * .5f + 8f) * scalePixels;
            var square = new Rect(own.placedAt.x - footprint, own.placedAt.y - footprint, 2f * footprint, 2f * footprint);
            bool named = own.roomName != null && !own.hovered;
            var tag = named ? TagRect(own, own.placedAt, scalePixels) : default;
            for (int pass = 0; pass <= placedNames.Count + 2; pass++)
            {
                bool moved = Past(placedNames, ref box, gap, up);
                moved |= PastOne(square, ref box, gap, up);
                if (named) moved |= PastOne(tag, ref box, gap, up);
                if (!moved) return;
            }
        }

        /// <summary>
        /// Whether a hidden finalist's name chip here would read as another room's (D17(3)): on another room's icon or the
        /// room's name under it, or nearer another room's icon than its own.
        /// </summary>
        private bool ReadsAsAnother(Beacon own, Rect box, float gap)
        {
            for (int i = 0; i < placedIcons.Count; i++)
                if (placedOwners[i] != own && Touches(box, placedIcons[i], gap)) return true;
            float mine = (box.center - own.placedAt).sqrMagnitude;
            foreach (var beacon in beacons)
                if (beacon != own && beacon.placed && (box.center - beacon.placedAt).sqrMagnitude < mine) return true;
            return false;
        }

        /// <summary>The icons placed this frame, in screen pixels, with their badges' reach: a name chip stands clear of them.</summary>
        private readonly List<Rect> placedIcons = new List<Rect>();
        /// <summary>Whose icon each of <see cref="placedIcons"/> is.</summary>
        private readonly List<Beacon> placedOwners = new List<Beacon>();

        /// <summary>
        /// The room view's icons placed so far this frame (D17), each icon's square with its badges' reach and
        /// the room's name under it, hovered or not: an icon placed later stands clear of them. Read only while
        /// <see cref="keepApart"/>; outside the room view the icons stand over their rooms, apart as the rooms are.
        /// </summary>
        private readonly List<Rect> claimedSpots = new List<Rect>();
        /// <summary>
        /// The air the room view's icons placed so far this frame keep over them for their hidden finalists' names (D17(3)),
        /// apart from <see cref="claimedSpots"/>: an icon placed later keeps out of it as far as its footprint takes it, and
        /// otherwise stands in it, where an icon never stands on another.
        /// </summary>
        private readonly List<Rect> claimedAir = new List<Rect>();
        private bool keepApart;

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
                moved |= PastOne(placed[i], ref box, gap, up);
            return moved;
        }

        /// <summary>Moves <paramref name="box"/> past <paramref name="other"/> where it touches it: above it when <paramref name="up"/>, below it otherwise.</summary>
        private static bool PastOne(Rect other, ref Rect box, float gap, bool up)
        {
            if (!Touches(box, other, gap)) return false;
            box.y = up ? other.yMax + gap : other.yMin - gap - box.height;
            return true;
        }

        /// <summary>Whether two screen rects come within <paramref name="gap"/> of each other.</summary>
        private static bool Touches(Rect box, Rect other, float gap)
            => box.xMin < other.xMax + gap && box.xMax > other.xMin - gap && box.yMin < other.yMax + gap && box.yMax > other.yMin - gap;

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
        /// <summary>The room view's room, its doorways and who it hides (D17): null outside the room view.</summary>
        private string roomViewRoom;
        private System.Func<string, Vector3> doorway;
        private System.Func<Transform, string> hiddenRoom;

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
        /// The room view's way-finding (ROOM-SCOPE-PLAN §3.2 i, D17, RS5), from the director's Update with
        /// <see cref="Request"/>: <paramref name="viewedRoom"/> is the room the view frames, or null outside
        /// the room view. In the room view the icons are not gated on the camera's distance: the next stop
        /// stays pinned and clickable at every distance, and every other room has its icon with its count,
        /// at the viewed room's doorway that leads there (<paramref name="doorwayTo"/>: the doorway's middle,
        /// or a point whose x is NaN for a room with no doorway from here), or at the frame's edge in its
        /// direction. The few named at the endgame (MOCKUP-PASS M14) whom the view hides
        /// (<paramref name="hiddenIn"/>: the room a hidden body stands in, null for one drawn) wear their
        /// names over their room's icon instead of over their heads.
        /// </summary>
        public void RoomView(string viewedRoom, System.Func<string, Vector3> doorwayTo, System.Func<Transform, string> hiddenIn)
        {
            roomViewRoom = viewedRoom; doorway = doorwayTo; hiddenRoom = hiddenIn;
        }

        /// <summary>The room the icons were last placed for in the room view, or null outside it.</summary>
        public string RoomViewRoom => roomViewRoom;

        /// <summary>Whether a room's icon stands at the viewed room's doorway that leads there this frame (D17); false outside the room view.</summary>
        public bool IsAtDoorway(string room)
        {
            foreach (var beacon in beacons)
                if (beacon.room == room) return beacon.atDoorway && beacon.rect != null && beacon.rect.gameObject.activeInHierarchy;
            return false;
        }

        /// <summary>Where a room's icon was placed this frame, in screen pixels; false while it is not shown.</summary>
        public bool TryPlacedAt(string room, out Vector2 screen)
        {
            screen = default;
            foreach (var beacon in beacons)
                if (beacon.room == room && beacon.placed && beacon.rect != null && beacon.rect.gameObject.activeInHierarchy) { screen = beacon.placedAt; return true; }
            return false;
        }

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
            // The viewed room as the rig framed it this frame - its late pass runs before this one - rather than
            // as the director read it in its Update: on a cut's own frame the icons stand at the new room's doorways.
            if (doorway != null && rig != null) roomViewRoom = rig.ViewRoom >= 0 ? RoomLayers.RoomIds[rig.ViewRoom] : null;
            // The room view's icons are not gated on the distance (D17): every room-view distance is 4 to 18.25 m.
            bool roomView = roomViewRoom != null;
            float fade = roomView ? 1f : Mathf.InverseLerp(HiddenBelow, ShownFrom, cameraDistance);
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
            placedOwners.Clear();
            screenFrame = eye.pixelRect;
            // In the room view the icons are about the rooms beyond the one on screen.
            string viewing = roomView ? roomViewRoom : playerRoom;
            // The room view's icons never stand on one another (D17: every room keeps an icon, and one drawn
            // over another is lost - the lane's rs4 captures had the nomination room's over the games room's,
            // both waiting at the frame's edge the same way). They claim their spots in turn: the next stop
            // first, where it always stood or, for its hidden finalists' names, within half a footprint of it per
            // name (below); then the viewed room's own, the screen or the diary it opens; then
            // the rooms a doorway leads to; then the rooms with none, at the frame's edge. An icon whose spot one
            // before it holds takes the next clear one as an icon under the chrome does (TrySpot) - and an icon at
            // a doorway on screen the first clear spot from the doorway towards the middle, beside it - and none
            // is lost for want of one. Outside the room view the order is the icons' own and nothing is claimed:
            // they stand over their rooms, apart as the rooms are.
            claimedSpots.Clear();
            claimedAir.Clear();
            keepApart = roomView;
            foreach (var beacon in beacons)
            {
                beacon.door = roomView && beacon.room != viewing && doorway != null ? doorway(beacon.room) : new Vector3(float.NaN, 0f, 0f);
                beacon.airWidth = beacon.airReach = 0f;
                beacon.airNames = 0;
            }
            // The few named at the endgame whom the room view hides wear their names over their rooms' icons (D17(3)), so
            // such an icon keeps the air over it for their chips, and the icons after it keep out of that air - a chip
            // lifted past a neighbour's icon hung over the neighbour's (the fixes' review) - where that moves no icon
            // further than a footprint (below).
            float gap = 2f * pixels;
            foreach (var chip in names)
            {
                chip.hiddenIn = roomView && hiddenRoom != null && chip.body != null && chip.rect != null ? hiddenRoom(chip.body) : null;
                if (chip.hiddenIn == null) continue;
                foreach (var beacon in beacons)
                {
                    if (beacon.room != chip.hiddenIn) continue;
                    Fit(chip);
                    var size = chip.rect.sizeDelta * pixels;
                    beacon.airWidth = Mathf.Max(beacon.airWidth, size.x);
                    beacon.airReach += size.y + gap;
                    beacon.airNames++;
                    break;
                }
            }
            for (int turn = 0; turn < 4; turn++)
            foreach (var beacon in beacons)
            {
                if (ClaimTurn(beacon, roomView, viewing) != turn) continue;
                beacon.placed = false;
                bool special = beacon.room == stationRoom || beacon.room == "Private";
                bool show = beacon.room != viewing || special;
                Vector3 screen = default;
                bool pinned = false, atDoorway = false;
                Vector2 toward = default;
                // The doorway or the place on screen that an icon waiting beside it points its pip at; NaN for one
                // pinned at the frame's edge, whose pip points the room's way from the middle.
                var aim = new Vector2(float.NaN, 0f);
                if (show && roomView && beacon.room != viewing)
                {
                    // D17: at the viewed room's doorway that leads there, on screen or pinned towards it, or,
                    // with no doorway from here, at the frame's edge in its direction.
                    var door = beacon.door;
                    atDoorway = !float.IsNaN(door.x);
                    screen = eye.WorldToScreenPoint((atDoorway ? door : where(beacon.room)) + Vector3.up * Height);
                    bool doorOnScreen = atDoorway && screen.z > .5f && eye.pixelRect.Contains(screen);
                    show = doorOnScreen && !IconCovered(beacon, screen, margin, pixels);
                    if (doorOnScreen) aim = screen;
                    // A doorway clear of the chrome whose spot an icon before it holds: beside it, the pip its way.
                    if (!show && doorOnScreen && !UnderChrome(beacon, screen, margin, pixels)
                        && TryPin(eye.pixelRect, screen, margin, beacon, pixels, out var beside))
                    {
                        toward = ((Vector2)screen - beside).normalized;
                        screen = new Vector3(beside.x, beside.y, screen.z);
                        show = pinned = true;
                    }
                    if (!show && TryEdge(eye.pixelRect, screen, margin, beacon, pixels, out var edge, out var away))
                    {
                        toward = away;
                        aim.x = float.NaN;
                        screen = new Vector3(edge.x, edge.y, Mathf.Max(1f, Mathf.Abs(screen.z)));
                        show = pinned = true;
                    }
                }
                else if (show)
                {
                    screen = eye.WorldToScreenPoint(where(beacon.room) + Vector3.up * Height);
                    aim = screen;
                    // An icon under the HUD's chrome is not shown: it would be half hidden, and
                    // where the chrome takes no click, clickable without being seen. Any of it, not
                    // just its middle: an icon half under the status line was still being shown,
                    // and the room's name under the icon - wider than it, hung below the square -
                    // showed through the cards while the icon itself stood clear.
                    show = screen.z > .5f && eye.pixelRect.Contains(screen) && !IconCovered(beacon, screen, margin, pixels);
                    // Except the next stop, which is never lost that way: its icon waits at the
                    // first clear spot on the way from its place to the middle of the screen,
                    // whole and clickable, with a pip on the side its place is on. In the room view
                    // (D17) a place behind the camera is pinned too, at the frame's edge its way.
                    if (!show && beacon.room == nextStop && screen.z > .5f
                        && TrySpot(eye.pixelRect, screen, margin, beacon, pixels, out var clear))
                    {
                        toward = ((Vector2)screen - clear).normalized;
                        screen = new Vector3(clear.x, clear.y, screen.z);
                        show = pinned = true;
                    }
                    else if (!show && roomView && beacon.room == nextStop && screen.z <= .5f
                        && TryEdge(eye.pixelRect, screen, margin, beacon, pixels, out var behind, out var away))
                    {
                        toward = away;
                        aim.x = float.NaN;
                        screen = new Vector3(behind.x, behind.y, 1f);
                        show = pinned = true;
                    }
                    // Nor, in the room view, the viewed room's own icon - the screen or the diary, which its click opens - with
                    // its place on screen and clear of the chrome but held by the next stop's: beside it, the pip its way, or,
                    // with no clear spot anywhere, at its place all the same (D17; the fixes' review).
                    else if (!show && roomView && beacon.room != nextStop && screen.z > .5f && eye.pixelRect.Contains(screen)
                        && !UnderChrome(beacon, screen, margin, pixels)
                        && TrySpot(eye.pixelRect, screen, margin, beacon, pixels, out var beside))
                    {
                        show = true;
                        pinned = (beside - (Vector2)screen).sqrMagnitude > .25f;
                        if (pinned)
                        {
                            toward = ((Vector2)screen - beside).normalized;
                            screen = new Vector3(beside.x, beside.y, screen.z);
                        }
                    }
                }
                // Every icon took its spot above without the air the hidden finalists' names hang in (D17(3)). One whose
                // own air is not clear there, or which stands in the air an icon before it keeps, moves to the nearest
                // spot within its footprint's width where neither is so - beside its doorway or its place, the pip that
                // way - or else stays, and keeps no air; the names then take their fallback (PlaceNames). The next stop
                // moves no further than half its footprint's width for each name, two at most: the air walked it halfway
                // to the middle of the frame, and a finalist's room's icon from the frame's edge to its middle (the
                // claims' second review), but kept where it stood, pinned at the frame's top under the shade with its air
                // in the shade and the icon pinned under it on the spot under it, it left a finalist in its room unnamed
                // in about a fifth of the modelled names (the claims' third review), and moved half a footprint for both
                // finalists' names, whose air is a name taller, it left them unnamed in about a fifth too (the claims'
                // fourth review). It claims its spot first, so only the chrome or the frame's edge covers its air.
                bool aired = false;
                if (show && keepApart)
                {
                    if (IconCovered(beacon, screen, margin, pixels, Clear.WithAir)
                        && TryAround(eye.pixelRect, screen, margin, beacon, pixels, Clear.WithAir, footprint * .5f,
                            beacon.room == nextStop ? footprint * Mathf.Clamp(beacon.airNames, 1, 2) : 2f * footprint, out var aside))
                    {
                        if (!float.IsNaN(aim.x)) toward = (aim - aside).normalized;
                        screen = new Vector3(aside.x, aside.y, screen.z);
                        pinned = true;
                    }
                    aired = AirFree(beacon, screen, pixels);
                }
                beacon.atDoorway = show && atDoorway;
                if (!show)
                {
                    if (beacon.rect.gameObject.activeSelf) beacon.rect.gameObject.SetActive(false);
                    beacon.hovered = false;
                    continue;
                }
                if (!beacon.rect.gameObject.activeSelf) beacon.rect.gameObject.SetActive(true);
                any = true;
                beacon.placed = true;
                beacon.placedAt = screen;
                placedIcons.Add(new Rect(screen.x - footprint, screen.y - footprint, 2f * footprint, 2f * footprint));
                placedOwners.Add(beacon);
                // The room's name hung under the icon is part of what a name chip must clear, as the
                // chrome treats it (IconCovered): a chip over "KITCHEN" read as the room's own words.
                if (beacon.roomName != null && !beacon.hovered)
                {
                    placedIcons.Add(TagRect(beacon, screen, pixels));
                    placedOwners.Add(beacon);
                }
                // And in the room view what the icons after it stand clear of: the name too while the hover's
                // tip is up, so that a hover never moves another room's icon, and, where it keeps it, the air its
                // hidden finalists' names hang in, which they keep out of as far as a footprint takes them (above).
                if (keepApart)
                {
                    claimedSpots.Add(new Rect(screen.x - footprint, screen.y - footprint, 2f * footprint, 2f * footprint));
                    if (beacon.roomName != null) claimedSpots.Add(TagRect(beacon, screen, pixels));
                    if (aired) claimedAir.Add(AirRect(beacon, screen, pixels));
                }
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
        /// room's name under it - is on the screen and clear of the HUD's chrome (and in the room
        /// view as <paramref name="clear"/> says, <see cref="IconCovered"/>).
        /// </summary>
        private bool TryPin(Rect pixels, Vector2 target, float margin, Beacon beacon, float scalePixels, out Vector2 at, Clear clear = Clear.Icon)
        {
            at = default;
            var inner = InnerRect(pixels, margin, beacon, scalePixels);
            if (inner.width <= 0f || inner.height <= 0f) return false;
            const int Steps = 24;
            for (int step = 0; step <= Steps; step++)
            {
                var point = Vector2.Lerp(target, pixels.center, step / (float)Steps);
                if (!inner.Contains(point) || IconCovered(beacon, point, margin, scalePixels, clear)) continue;
                at = point;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Where an icon waits whose own spot is not clear: the first clear spot on the way from <paramref name="target"/>
        /// to the middle of the screen (<see cref="TryPin"/>). In the room view no icon is lost for want of one there (D17:
        /// in the yard's crowded views the icons before it held every spot on the way, and the fixes' review found rooms
        /// with no icon at all): then the nearest clear spot round the target (<see cref="TryAround"/>), rings a footprint
        /// apart out to the frame's far corner; and at the last the first spot on the way clear of the chrome alone,
        /// standing on an icon before it, as they all stood before they claimed their spots.
        /// </summary>
        private bool TrySpot(Rect pixels, Vector2 target, float margin, Beacon beacon, float scalePixels, out Vector2 at)
        {
            if (TryPin(pixels, target, margin, beacon, scalePixels, out at)) return true;
            if (!keepApart) return false;
            return TryAround(pixels, target, margin, beacon, scalePixels, Clear.Icon, (Side * .5f + 8f) * scalePixels, float.PositiveInfinity, out at)
                || TryPin(pixels, target, margin, beacon, scalePixels, out at, Clear.ChromeOnly);
        }

        /// <summary>
        /// The room view's wider search (D17): rings round <paramref name="target"/> <paramref name="step"/> apart, each
        /// walked both ways in turn from its side away from the middle of the frame, so that a spot along the frame's edge
        /// comes before one further in, and the first spot where the whole icon is on the screen and clear as
        /// <paramref name="clear"/> says. Points about a step apart round each ring, out to <paramref name="reach"/> or the
        /// frame's far corner, whichever is nearer.
        /// </summary>
        private bool TryAround(Rect pixels, Vector2 target, float margin, Beacon beacon, float scalePixels, Clear clear, float step, float reach, out Vector2 at)
        {
            at = default;
            var inner = InnerRect(pixels, margin, beacon, scalePixels);
            if (inner.width <= 0f || inner.height <= 0f || step < 1f) return false;
            var outward = target - pixels.center;
            float start = outward.sqrMagnitude < 1f ? -.5f * Mathf.PI : Mathf.Atan2(outward.y, outward.x);
            reach = Mathf.Min(reach, Mathf.Max(Mathf.Max((target - pixels.min).magnitude, (target - pixels.max).magnitude),
                Mathf.Max((target - new Vector2(pixels.xMin, pixels.yMax)).magnitude, (target - new Vector2(pixels.xMax, pixels.yMin)).magnitude)));
            for (int ring = 1; ring * step <= reach; ring++)
            {
                float radius = ring * step;
                int points = Mathf.Max(6, Mathf.CeilToInt(2f * Mathf.PI * radius / step));
                for (int i = 0; i < points; i++)
                {
                    // 0, +1, -1, +2, -2, ...: both ways round from the outward side.
                    int k = (i + 1) / 2 * (i % 2 == 1 ? 1 : -1);
                    float angle = start + k * 2f * Mathf.PI / points;
                    var point = target + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    if (!inner.Contains(point) || IconCovered(beacon, point, margin, scalePixels, clear)) continue;
                    at = point;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Where an icon's middle may stand with the whole icon and the room's name under it on the screen.</summary>
        private static Rect InnerRect(Rect pixels, float margin, Beacon beacon, float scalePixels)
        {
            float tag = TagDepth(beacon, scalePixels);
            return new Rect(pixels.xMin + margin, pixels.yMin + tag, pixels.width - 2f * margin, pixels.height - margin - tag);
        }

        /// <summary>
        /// The room view's pin for a room with no doorway on screen (D17): the point on the frame's inner
        /// edge in the direction of <paramref name="screen"/> from the middle of the frame (a point behind
        /// the camera mirrored, as its projection is), then the first clear spot from there towards the
        /// middle, or round it (<see cref="TrySpot"/>). <paramref name="away"/> is the direction the pip points.
        /// </summary>
        private bool TryEdge(Rect pixels, Vector3 screen, float margin, Beacon beacon, float scalePixels, out Vector2 at, out Vector2 away)
        {
            at = default; away = default;
            var centre = pixels.center;
            var toward = (Vector2)screen - centre;
            if (screen.z < 0f) toward = -toward;
            if (toward.sqrMagnitude < 1f) toward = Vector2.down;
            var inner = InnerRect(pixels, margin, beacon, scalePixels);
            if (inner.width <= 0f || inner.height <= 0f) return false;
            float kx = toward.x > 0f ? (inner.xMax - .5f - centre.x) / toward.x : toward.x < 0f ? (inner.xMin - centre.x) / toward.x : float.PositiveInfinity;
            float ky = toward.y > 0f ? (inner.yMax - .5f - centre.y) / toward.y : toward.y < 0f ? (inner.yMin - centre.y) / toward.y : float.PositiveInfinity;
            float k = Mathf.Max(0f, Mathf.Min(kx, ky));
            away = toward.normalized;
            return TrySpot(pixels, centre + toward * k, margin, beacon, scalePixels, out at);
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
        /// tall, so a narrow piece of chrome cannot slip between them. In the room view an icon placed
        /// before it this frame covers it as well (<see cref="Claimed"/>, D17), and, asked with the air,
        /// so do the air the icons before it keep for their hidden finalists' names and the chrome and the
        /// frame's edge over it where its own hidden finalists' names hang (<see cref="AirCovered"/>), as
        /// <paramref name="clear"/> says.
        /// </summary>
        private bool IconCovered(Beacon beacon, Vector2 point, float margin, float scalePixels, Clear clear = Clear.Icon)
            => (keepApart && clear != Clear.ChromeOnly && Claimed(beacon, point, margin, scalePixels, clear == Clear.WithAir))
                || UnderChrome(beacon, point, margin, scalePixels)
                || (keepApart && clear == Clear.WithAir && AirCovered(beacon, point, scalePixels));

        /// <summary>
        /// How much of a spot the room view asks to be clear (D17): the icon and the room's name under it clear of the icons
        /// placed before it and the chrome, where every icon takes its spot; the same clear of the air the icons before it
        /// keep too, with, for a room a hidden finalist is named over, the air over it for the names clear of all of that and
        /// of the frame's edge, where an icon moves within its footprint for the names (D17(3)); or the icon clear of the
        /// chrome alone, the last resort.
        /// </summary>
        private enum Clear { WithAir, Icon, ChromeOnly }

        /// <summary>
        /// Whether an icon drawn with its middle at <paramref name="point"/> - its square, and the room's name
        /// under it - would stand on an icon the room view placed before it this frame (D17), and, with
        /// <paramref name="withAir"/>, on the air an icon before it keeps for its hidden finalists' names, or
        /// whether the air over it that its own hidden finalists' names hang in would stand on either.
        /// </summary>
        private bool Claimed(Beacon beacon, Vector2 point, float margin, float scalePixels, bool withAir = false)
        {
            if (claimedSpots.Count == 0 && (!withAir || claimedAir.Count == 0)) return false;
            var square = new Rect(point.x - margin, point.y - margin, 2f * margin, 2f * margin);
            bool named = beacon.roomName != null;
            var tag = named ? TagRect(beacon, point, scalePixels) : default;
            bool aired = withAir && beacon.airReach > 0f;
            var air = aired ? AirRect(beacon, point, scalePixels) : default;
            return Holds(claimedSpots, square, named, tag, aired, air) || (withAir && Holds(claimedAir, square, named, tag, aired, air));
        }

        /// <summary>Whether any of <paramref name="spots"/> overlaps the square, the name where there is one or the air where it is asked about.</summary>
        private static bool Holds(List<Rect> spots, Rect square, bool named, Rect tag, bool aired, Rect air)
        {
            for (int i = 0; i < spots.Count; i++)
                if (spots[i].Overlaps(square) || (named && spots[i].Overlaps(tag)) || (aired && spots[i].Overlaps(air))) return true;
            return false;
        }

        /// <summary>This frame's screen, in pixels, as the icons are placed on it.</summary>
        private Rect screenFrame;

        /// <summary>Whether the air over an icon drawn at <paramref name="point"/>, where its hidden finalists' names hang (D17(3)), is off the frame or under the chrome.</summary>
        private bool AirCovered(Beacon beacon, Vector2 point, float scalePixels)
        {
            if (beacon.airReach <= 0f) return false;
            var air = AirRect(beacon, point, scalePixels);
            return air.xMin < screenFrame.xMin || air.yMin < screenFrame.yMin || air.xMax >= screenFrame.xMax || air.yMax >= screenFrame.yMax
                || ChipCovered(air);
        }

        /// <summary>
        /// Whether an icon drawn at <paramref name="point"/> keeps the air over it for its hidden finalists' names (D17(3)):
        /// it has some, and the air is on the frame, clear of the chrome and of every icon and air placed before it.
        /// </summary>
        private bool AirFree(Beacon beacon, Vector2 point, float scalePixels)
        {
            if (beacon.airReach <= 0f || AirCovered(beacon, point, scalePixels)) return false;
            var air = AirRect(beacon, point, scalePixels);
            return !Holds(claimedSpots, air, false, default, false, default) && !Holds(claimedAir, air, false, default, false, default);
        }

        /// <summary>
        /// The air over an icon drawn with its middle at <paramref name="point"/> that its hidden finalists' names hang in
        /// (D17(3)), in screen pixels: from the top of its footprint up past the last name, padded round by a name's gap and
        /// the badges' reach past an icon's tested square, so that an icon clear of it is clear of the names by their gap;
        /// slid along its row into this frame's screen as the names are (<see cref="Slide"/>), so that it holds them there.
        /// </summary>
        private Rect AirRect(Beacon beacon, Vector2 point, float scalePixels)
        {
            float pad = 4f * scalePixels;
            float width = beacon.airWidth + 2f * pad;
            return new Rect(Slide(point.x - width * .5f, width, screenFrame), point.y + (Side * .5f + 8f) * scalePixels, width, beacon.airReach + pad);
        }

        /// <summary>
        /// The left edge <paramref name="x"/> of a row <paramref name="width"/> wide slid along the row into
        /// <paramref name="frame"/>, a pixel short of its right edge, where the row fits: a hidden finalist's name and the air
        /// kept for it (D17(3)). Slid, it still spans its icon's middle, which is on the frame: it moves only as far as
        /// brings its near end to the frame's side.
        /// </summary>
        private static float Slide(float x, float width, Rect frame)
            => width < frame.width - 1f ? Mathf.Clamp(x, frame.xMin, frame.xMax - 1f - width) : x;

        /// <summary>
        /// The turn a room's icon claims its spot in (D17): in the room view the next stop first, then the viewed room's own
        /// (shown only when it is the screen's or the diary's, which a click on it opens), then the rooms a doorway from the
        /// viewed room leads to, then the rooms with no doorway from here; outside the room view one turn, in the icons' own order.
        /// </summary>
        private int ClaimTurn(Beacon beacon, bool roomView, string viewing)
            => !roomView || beacon.room == nextStop ? 0 : beacon.room == viewing ? 1 : !float.IsNaN(beacon.door.x) ? 2 : 3;

        /// <summary>The chrome's half of <see cref="IconCovered"/>: whether the HUD covers the icon's square or the room's name.</summary>
        private bool UnderChrome(Beacon beacon, Vector2 point, float margin, float scalePixels)
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
