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
            return "Travel to " + RoomLabels.Name(room).ToLowerInvariant();
        }

        private sealed class Beacon
        {
            public string room;
            public RectTransform rect;
            public Button button;
            public Image mark;
            public TMP_Text caption;
            public GameObject tip;
            public CanvasGroup group;
            public bool hovered;
            public string glyph;
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

        /// <summary>Takes every icon off the screen, and out of the way of clicks.</summary>
        public void Hide()
        {
            IsShowing = false;
            foreach (var beacon in beacons) if (beacon.rect != null && beacon.rect.gameObject.activeSelf) beacon.rect.gameObject.SetActive(false);
        }

        private bool wanted, competition;
        private HouseCameraRig rig;
        private float scale = 1f;
        private string stationRoom, playerRoom;
        private System.Func<string, Vector3> where;
        private System.Func<Vector2, bool> covered;

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
                foreach (var beacon in beacons) { beacon.rect.localScale = Vector3.one * scale; beacon.glyph = null; }
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
                if (glyph != beacon.glyph)
                {
                    beacon.glyph = glyph;
                    beacon.mark.sprite = UiTheme.Icon(glyph);
                    beacon.mark.enabled = beacon.mark.sprite != null;
                    // Gold is power: the Head of Household's suite wears it here as it does on the map.
                    beacon.mark.color = beacon.room == "HoH" ? UiTheme.Gold : special ? UiTheme.Accent : UiTheme.Heading;
                    beacon.caption.text = Localisation.Text(Caption(beacon.room, stationRoom, competition));
                    var tipRect = (RectTransform)beacon.tip.transform;
                    tipRect.sizeDelta = new Vector2(Mathf.Ceil(beacon.caption.GetPreferredValues(beacon.caption.text).x) + 24f, 30f);
                }
                if (beacon.tip.activeSelf != beacon.hovered) beacon.tip.SetActive(beacon.hovered);
            }
            IsShowing = any;
        }
    }
}
