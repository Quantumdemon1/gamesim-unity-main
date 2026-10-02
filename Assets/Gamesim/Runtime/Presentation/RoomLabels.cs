using System.Collections.Generic;
using Gamesim.House;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The room names over the house while the overview holds (VISUAL-TARGET.md V5): a glass chip
    /// above each room marker, turned to the camera every frame the way the name tags are. Built
    /// when the overview opens and destroyed when it ends, so nothing of it exists in an ordinary
    /// frame and nothing of it can drift into a capture that did not ask for it.
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(1000)]
    public sealed class RoomLabels : MonoBehaviour
    {
        public const string RootName = "Room labels";
        /// <summary>Metres above the floor the chips float: over the walls, under the camera.</summary>
        public const float Height = 3.4f;
        // Read from the overview's height: at 0.017 a chip was eighteen pixels tall, a caption on a
        // blueprint rather than mockup-03's labels over its rooms.
        private const float ChipHeight = 56f, WorldScale = 0.03f, MarkSide = 30f;

        private readonly List<RectTransform> chips = new List<RectTransform>();
        private readonly List<RectTransform> hotspots = new List<RectTransform>();
        private Transform root;
        private RectTransform hotspotLayer;
        private Camera eyeCamera;

        /// <summary>The overlay that takes the map's clicks, one hotspot a chip.</summary>
        public const string HotspotLayerName = "Room map hotspots";
        public const string HotspotPrefix = "Map · ";

        public static RoomLabels Attach(GameObject host)
        {
            var existing = host.GetComponent<RoomLabels>();
            return existing != null ? existing : host.AddComponent<RoomLabels>();
        }

        public bool IsShowing => root != null;
        public int Count => chips.Count;

        /// <summary>
        /// Whether every chip is down: the briefing, a panel or a board over the house
        /// (UI-UX-PASS-PLAN G0). The chips are world-space and the chrome is glass, so a chip under
        /// it read through - "COMPETITION YARD" behind the briefing's subtitle.
        /// </summary>
        public bool Hidden { get; set; }

        /// <summary>
        /// Whether the HUD's chrome covers a screen rect, in pixels; null covers nothing. A chip any
        /// of which is under the chrome is down: half under the right column, a room's name reads
        /// as another room's.
        /// </summary>
        public System.Func<Rect, bool> Covered { get; set; }

        /// <summary>How many chips are drawn this frame. A read, for tests.</summary>
        public int ShownCount
        {
            get
            {
                int shown = 0;
                foreach (var chip in chips) if (chip != null && chip.gameObject.activeInHierarchy) shown++;
                return shown;
            }
        }

        /// <summary>A room marker's name as the set paints it on the floor.</summary>
        public static string Title(string roomName)
        {
            switch (roomName)
            {
                case "Living": return "LIVING ROOM";
                case "Kitchen": return "KITCHEN";
                case "Bedroom": return "BEDROOM";
                case "Private": return "PRIVATE ROOM";
                case "Yard": return "COMPETITION YARD";
                case "HoH": return "HOH SUITE";
                case "Nomination": return "NOMINATION ROOM";
                case "Games": return "GAME ROOM";
                default: return string.IsNullOrEmpty(roomName) ? "" : roomName.ToUpperInvariant();
            }
        }

        /// <summary>
        /// A room marker's name as a sentence says it - "Living room", "HoH suite" - for a card
        /// title. <see cref="Title"/> is the floor paint's capitals, which the live feed's caption
        /// and the overview's chips are held to. The words are the simulation's
        /// (<see cref="Gamesim.Simulation.RoomWords"/>), so the engine's own lines say a room the
        /// way the set does.
        /// </summary>
        public static string Name(string roomName) => Gamesim.Simulation.RoomWords.Name(roomName);

        /// <summary>
        /// The room's mark from Refinement Kit 6 where the kit draws one (a bed, a sofa, a kitchen,
        /// a gamepad, a crown, the diary), and the room's own glyph where it does not.
        /// </summary>
        public static Sprite Mark(string roomName)
        {
            string kit = null;
            switch (roomName)
            {
                case "Living": kit = PackArt.KitIconSofa; break;
                case "Kitchen": kit = PackArt.KitIconKitchen; break;
                case "Bedroom": kit = PackArt.KitIconBed; break;
                case "Private": kit = PackArt.KitIconDiary; break;
                case "HoH": kit = PackArt.KitIconCrown; break;
                case "Games": kit = PackArt.KitIconGamepad; break;
            }
            return (kit != null ? UiTheme.Pack(kit) : null) ?? UiTheme.Icon(Glyph(roomName));
        }

        /// <summary>The glyph a room's chip leads with, as mockup-03 marks each room.</summary>
        /// <summary>
        /// A room's name as it reads mid-sentence: "the living room", "the HoH suite". Only a first
        /// word that is an ordinary capitalised word is lowered; a name whose capitals mean
        /// something keeps them (<see cref="Gamesim.Simulation.RoomWords.InSentence"/>).
        /// </summary>
        public static string InSentence(string roomName) => Gamesim.Simulation.RoomWords.InSentence(roomName);

        public static string Glyph(string roomName)
        {
            switch (roomName)
            {
                case "Living": return "house";
                case "Kitchen": return "fork";
                case "Bedroom": return "bed";
                case "Private": return "chat";
                case "Yard": return "trophy";
                case "HoH": return "crown";
                case "Nomination": return "gavel";
                case "Games": return "target";
                default: return "house";
            }
        }

        /// <summary>
        /// Puts a chip over every room. With <paramref name="pick"/>, each chip is also a button
        /// that says which room was clicked - the overview is a map, and a map you cannot point at
        /// is a picture. The chip's name is its caption, as it is on the floor.
        /// </summary>
        public void Show(IEnumerable<HouseRoomMarker> markers, float scale, System.Action<string> pick = null, Camera eye = null)
        {
            Hide();
            root = new GameObject(RootName).transform;
            root.SetParent(transform, false);
            foreach (var marker in markers)
            {
                if (marker == null) continue;
                var canvas = new GameObject(marker.RoomName + " label", typeof(RectTransform), typeof(Canvas)).GetComponent<RectTransform>();
                canvas.SetParent(root, false);
                canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                canvas.localScale = Vector3.one * (WorldScale * scale);
                canvas.position = marker.transform.position + Vector3.up * Height;

                // Mockup-03's chip: dark glass with a hairline, the room's glyph, and its name in
                // white, as wide as the name - a fixed 330 left GYM floating in a bar of glass.
                var chip = HudPrimitives.Fill("Chip", canvas, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, .9f), 12);
                chip.anchorMin = Vector2.zero; chip.anchorMax = Vector2.one;
                chip.offsetMin = Vector2.zero; chip.offsetMax = Vector2.zero;
                UiTheme.AddBorder(chip, 12, new Color(UiTheme.Hairline.r, UiTheme.Hairline.g, UiTheme.Hairline.b, .6f));
                var word = HudPrimitives.Heading("Word", chip, 22f, UiTheme.Paper, TextAlignmentOptions.MidlineLeft);
                word.text = Title(marker.RoomName);
                word.characterSpacing = 2f;
                word.textWrappingMode = TextWrappingModes.NoWrap;
                float words = Mathf.Ceil(word.GetPreferredValues(word.text).x) + 4f;
                var sprite = UiTheme.Icon(Glyph(marker.RoomName));
                float lead = sprite != null ? 18f + MarkSide + 12f : 22f;
                canvas.sizeDelta = new Vector2(lead + words + 22f, ChipHeight);
                word.rectTransform.anchorMin = Vector2.zero; word.rectTransform.anchorMax = Vector2.one;
                word.rectTransform.offsetMin = new Vector2(lead, 0f); word.rectTransform.offsetMax = new Vector2(-18f, 0f);
                if (sprite != null)
                {
                    var mark = new GameObject("Mark", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    mark.rectTransform.SetParent(chip, false);
                    mark.rectTransform.anchorMin = mark.rectTransform.anchorMax = new Vector2(0f, .5f);
                    mark.rectTransform.pivot = new Vector2(0f, .5f);
                    mark.rectTransform.sizeDelta = new Vector2(MarkSide, MarkSide);
                    mark.rectTransform.anchoredPosition = new Vector2(18f, 0f);
                    mark.sprite = sprite; mark.preserveAspect = true; mark.raycastTarget = false;
                    // Gold is power: the Head of Household's suite is the one room that wears it.
                    mark.color = marker.RoomName == "HoH" ? UiTheme.Gold : UiTheme.Heading;
                }
                chips.Add(canvas);
                if (pick != null) hotspots.Add(Hotspot(marker.RoomName, pick));
            }
            eyeCamera = eye;
            Face();
            PlaceChips();
        }

        /// <summary>
        /// A screen-space button laid over one chip, the chip's own size wherever the camera puts
        /// it. The chips cannot take the click themselves: the overview's lens is a blended
        /// projection, and a world-space canvas is hit-tested by a camera ray that knows nothing of
        /// it - a click on the kitchen's name landed on the living room's. The overlay is placed
        /// from the same projection the frame is drawn with, so what is clicked is what is seen.
        /// </summary>
        private RectTransform Hotspot(string room, System.Action<string> pick)
        {
            if (hotspotLayer == null)
            {
                var layer = new GameObject(HotspotLayerName, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
                layer.transform.SetParent(root, false);
                var canvas = layer.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                // Under the HUD, whose chrome stays over the map.
                canvas.sortingOrder = 65;
                hotspotLayer = (RectTransform)layer.transform;
            }
            var spot = new GameObject(HotspotPrefix + room, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            spot.SetParent(hotspotLayer, false);
            spot.anchorMin = spot.anchorMax = Vector2.zero;
            spot.pivot = Vector2.zero;
            var face = spot.GetComponent<Image>();
            face.color = new Color(0f, 0f, 0f, 0f);
            face.raycastTarget = true;
            var button = spot.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            // The chip's words, as the control's caption.
            var caption = HudPrimitives.Label("Caption", spot, 12f, Color.clear, TextAlignmentOptions.Center);
            caption.text = Title(room);
            caption.raycastTarget = false;
            button.onClick.AddListener(() => pick(room));
            return spot;
        }

        public void Hide()
        {
            chips.Clear();
            hotspots.Clear();
            hotspotLayer = null;
            if (root == null) return;
            Destroy(root.gameObject);
            root = null;
        }

        private void LateUpdate() { Face(); PlaceChips(); }

        private void Face()
        {
            var camera = Camera.main;
            if (camera == null) return;
            foreach (var chip in chips)
                if (chip != null) chip.rotation = camera.transform.rotation;
        }

        private readonly Vector3[] corners = new Vector3[4];

        /// <summary>
        /// Each chip shown or down for the frame - down while every chip is (<see cref="Hidden"/>),
        /// behind the camera, or under the chrome (<see cref="Covered"/>) - and each hotspot over
        /// its chip's four corners, as the camera projects them this frame.
        /// </summary>
        private void PlaceChips()
        {
            var eye = eyeCamera != null ? eyeCamera : Camera.main;
            if (eye == null) return;
            float fit = hotspotLayer != null && hotspotLayer.lossyScale.x > 0f ? 1f / hotspotLayer.lossyScale.x : 1f;
            for (int i = 0; i < chips.Count; i++)
            {
                var chip = chips[i];
                if (chip == null) continue;
                bool shown = !Hidden;
                Vector2 low = new Vector2(float.MaxValue, float.MaxValue), high = new Vector2(float.MinValue, float.MinValue);
                if (shown)
                {
                    chip.GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        var screen = eye.WorldToScreenPoint(corner);
                        if (screen.z <= 0f) shown = false;
                        low = Vector2.Min(low, screen); high = Vector2.Max(high, screen);
                    }
                    if (shown && Covered != null && Covered(Rect.MinMaxRect(low.x, low.y, high.x, high.y))) shown = false;
                }
                if (chip.gameObject.activeSelf != shown) chip.gameObject.SetActive(shown);
                var spot = i < hotspots.Count ? hotspots[i] : null;
                if (spot == null) continue;
                if (spot.gameObject.activeSelf != shown) spot.gameObject.SetActive(shown);
                if (!shown) continue;
                spot.anchoredPosition = low * fit;
                spot.sizeDelta = (high - low) * fit;
            }
        }
    }
}
