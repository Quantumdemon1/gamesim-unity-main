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
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(1000)]
    public sealed class EpisodeTravelBeacons : MonoBehaviour
    {
        public const string RootName = "Travel beacons";
        /// <summary>The count of houseguests on a room's icon, and the ring that marks the next stop.</summary>
        public const string BadgeName = "Houseguests here", NextStopName = "Next stop";
        public const string BeaconPrefix = "Beacon · ";
        public const string StationCaption = "Travel to episode screen";
        public const string CompetitionCaption = "Travel to competition";
        public const string DiaryCaption = "Travel to diary room";

        /// <summary>How far back the camera has to be before the icons show, and where they are fully in, in metres.</summary>
        public const float HiddenBelow = 14f, ShownFrom = 18f;

        /// <summary>Metres over a room's floor: above the walls, which are cut away to 1.1 m, and above heads.</summary>
        private const float Height = 2.9f;
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
            public bool ringed;
            public TMP_Text caption;
            public GameObject tip;
            public CanvasGroup group;
            public bool hovered;
            public string glyph, said;
        }

        /// <summary>Tells its beacon the pointer is over it, for the tip that names what a click does.</summary>
        private sealed class Hover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public Beacon beacon;
            public void OnPointerEnter(PointerEventData data) { if (beacon != null) beacon.hovered = true; }
            public void OnPointerExit(PointerEventData data) { if (beacon != null) beacon.hovered = false; }
        }

        private readonly List<Beacon> beacons = new List<Beacon>();
        private Canvas canvas;
        private RectTransform root;
        private float shownScale = -1f;

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
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out var local);
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
        }

        private bool wanted, competition;
        private HouseCameraRig rig;
        private float scale = 1f;
        private string stationRoom, playerRoom;
        private System.Func<string, Vector3> where;
        private System.Func<Vector2, bool> covered;
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

        /// <summary>
        /// Who is where, and where the player is being sent next: the counts on the icons and the
        /// ring round the one the objective names. Either may be null.
        /// </summary>
        public void Annotate(System.Func<string, int> countIn, string nextStopRoom)
        {
            occupants = countIn;
            nextStop = nextStopRoom;
        }

        /// <summary>
        /// What this frame's icons should show, from the director's Update. They are placed in
        /// LateUpdate, after the camera has moved: placed any earlier, every icon trails the view
        /// by a frame and swims whenever it pans. <paramref name="where"/> says where a room's icon
        /// floats over the floor; the room the player is standing in is left out, unless there is
        /// something in it to open.
        /// </summary>
        public void Request(bool visible, HouseCameraRig camera, float textScale, string screenRoom, bool isCompetition,
            string standingIn, System.Func<string, Vector3> anchor, System.Func<Vector2, bool> underChrome = null)
        {
            wanted = visible; rig = camera; scale = textScale; stationRoom = screenRoom; competition = isCompetition;
            playerRoom = standingIn; where = anchor; covered = underChrome;
            if (!visible) Hide();
        }

        private void LateUpdate()
        {
            if (!wanted || rig == null || where == null) { if (IsShowing) Hide(); return; }
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
            foreach (var beacon in beacons)
            {
                bool special = beacon.room == stationRoom || beacon.room == "Private";
                bool show = beacon.room != playerRoom || special;
                Vector3 screen = default;
                if (show)
                {
                    screen = eye.WorldToScreenPoint(where(beacon.room) + Vector3.up * Height);
                    // An icon under the HUD's chrome is not shown: it would be half hidden, and
                    // where the chrome takes no click, clickable without being seen.
                    show = screen.z > .5f && eye.pixelRect.Contains(screen) && (covered == null || !covered(screen));
                }
                if (!show)
                {
                    if (beacon.rect.gameObject.activeSelf) beacon.rect.gameObject.SetActive(false);
                    beacon.hovered = false;
                    continue;
                }
                if (!beacon.rect.gameObject.activeSelf) beacon.rect.gameObject.SetActive(true);
                any = true;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out var local);
                beacon.rect.anchoredPosition = local - frame.min;
                beacon.group.alpha = fade;
                beacon.group.blocksRaycasts = fade > .5f;
                beacon.group.interactable = fade > .5f;

                string glyph = beacon.room == stationRoom ? (competition ? "trophy" : "camera") : RoomLabels.Glyph(beacon.room);
                // Keyed on the words, not the glyph: the yard wears the trophy as a room and as the
                // competition's screen, and only its caption says which.
                string said = Caption(beacon.room, stationRoom, competition);
                if (glyph != beacon.glyph || said != beacon.said)
                {
                    beacon.glyph = glyph; beacon.said = said;
                    beacon.mark.sprite = UiTheme.Icon(glyph);
                    beacon.mark.enabled = beacon.mark.sprite != null;
                    // Gold is power: the Head of Household's suite wears it here as it does on the map.
                    beacon.mark.color = beacon.room == "HoH" ? UiTheme.Gold : special ? UiTheme.Accent : UiTheme.Heading;
                    beacon.caption.text = Localisation.Text(said);
                    var tipRect = (RectTransform)beacon.tip.transform;
                    tipRect.sizeDelta = new Vector2(Mathf.Ceil(beacon.caption.GetPreferredValues(beacon.caption.text).x) + 24f, 30f);
                }
                if (beacon.tip.activeSelf != beacon.hovered) beacon.tip.SetActive(beacon.hovered);

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
        }
    }
}
