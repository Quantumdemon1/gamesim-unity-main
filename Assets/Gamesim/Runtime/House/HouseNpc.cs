using Gamesim.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.House
{
    /// <summary>Authored identity for a house character. Simulation state is added in a later slice.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(210)]
    public sealed class HouseNpc : MonoBehaviour
    {
        [SerializeField] private string id = "maya";
        [SerializeField] private string displayName = "Maya";

        private TextMesh nameLabel;
        private Camera labelCamera;
        private Vector3 labelRestPosition;
        private bool labelPositionKnown;

        public string Id => id;
        public string DisplayName => displayName;

        public void Configure(string stableId, string characterName)
        {
            id = string.IsNullOrWhiteSpace(stableId) ? "maya" : stableId.Trim();
            displayName = string.IsNullOrWhiteSpace(characterName) ? "Maya" : characterName.Trim();
            RefreshNameLabel();
        }

        private void OnEnable()
        {
            RefreshNameLabel();
            labelCamera = Camera.main;
        }

        private void OnTransformChildrenChanged()
        {
            RefreshNameLabel();
        }

        private void RefreshNameLabel()
        {
            if (nameLabel == null)
            {
                nameLabel = GetComponentInChildren<TextMesh>(true);
            }

            if (nameLabel != null)
            {
                if(!labelPositionKnown){labelRestPosition=nameLabel.transform.localPosition;labelPositionKnown=true;}
                nameLabel.text = displayName;
                Plate();
            }
        }

        /// <summary>The plate's name, so a test can find it.</summary>
        public const string PlateName = "Name plate";
        /// <summary>Canvas units to metres: a 48-unit plate stands about a quarter of a metre tall.</summary>
        private const float PlateScale = 0.0055f;
        private RectTransform plate;
        private CanvasGroup plateGroup;
        private TMP_Text plateWord;
        private bool spotlit;

        /// <summary>
        /// Whether this houseguest is the one the player is with - followed or talked to. Their
        /// plate stays up at any distance, under the follow spotlight: it is how the
        /// player finds who they picked from across the house.
        /// </summary>
        public bool Spotlit
        {
            get => spotlit;
            set => spotlit = value;
        }

        /// <summary>
        /// Whether the plate is kept down whatever the distance: while the opening plays, the
        /// houseguests are introduced by their cards, and a plate over each head would name them
        /// before the show does; and while a ceremony's card is up, whose frame is the show's.
        /// </summary>
        public bool PlateSuppressed { get; set; }

        /// <summary>
        /// The name as the mockups draw it over a houseguest (01, 12): the given name in the HUD's
        /// face on the pack's name plate - dark glass with a blue edge. The TextMesh stays as the
        /// name's holder and its fade, and stops drawing: in the default font it was a large grey
        /// word in a director's close shot, not a plate.
        /// </summary>
        private void Plate()
        {
            var renderer = nameLabel.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.enabled = false;
            if (plate == null)
            {
                var holder = new GameObject(PlateName, typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
                plate = (RectTransform)holder.transform;
                plate.SetParent(nameLabel.transform, false);
                plate.localPosition = Vector3.zero;
                plate.localRotation = Quaternion.identity;
                plate.localScale = Vector3.one * PlateScale;
                holder.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                plateGroup = holder.GetComponent<CanvasGroup>();
                plateGroup.interactable = false; plateGroup.blocksRaycasts = false;

                var ground = new GameObject("Plate", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                ground.rectTransform.SetParent(plate, false);
                ground.rectTransform.anchorMin = Vector2.zero; ground.rectTransform.anchorMax = Vector2.one;
                ground.rectTransform.offsetMin = Vector2.zero; ground.rectTransform.offsetMax = Vector2.zero;
                ground.raycastTarget = false;
                if (!UiTheme.PackSliced(ground, PackArt.Nameplate, 14f))
                {
                    UiTheme.Style(ground, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, .9f), 10);
                    UiTheme.AddBorder(ground.rectTransform, 10, UiTheme.Hairline);
                }

                plateWord = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                plateWord.rectTransform.SetParent(plate, false);
                plateWord.rectTransform.anchorMin = Vector2.zero; plateWord.rectTransform.anchorMax = Vector2.one;
                plateWord.rectTransform.offsetMin = new Vector2(14f, 2f); plateWord.rectTransform.offsetMax = new Vector2(-14f, -2f);
                var face = UiTheme.Font(UiTheme.Weight.SemiBold);
                if (face != null) plateWord.font = face;
                plateWord.fontSize = 28f; plateWord.color = UiTheme.Paper;
                plateWord.alignment = TextAlignmentOptions.Center;
                plateWord.textWrappingMode = TextWrappingModes.NoWrap;
                plateWord.overflowMode = TextOverflowModes.Overflow;
                plateWord.raycastTarget = false; plateWord.richText = false;
            }
            string given = displayName ?? string.Empty;
            int space = given.IndexOf(' ');
            if (space > 0) given = given.Substring(0, space);
            plateWord.text = given;
            float width = Mathf.Max(80f, Mathf.Ceil(plateWord.GetPreferredValues(given).x) + 40f);
            plate.sizeDelta = new Vector2(width, 48f);
        }

        private void LateUpdate()
        {
            if (nameLabel == null)
            {
                return;
            }

            var seat=GetComponent<HouseSeatPresentation>();
            if(seat!=null && seat.Active)nameLabel.transform.position=seat.VisualFocus+Vector3.up*.35f;
            else if(labelPositionKnown)nameLabel.transform.localPosition=labelRestPosition;

            if (labelCamera == null || !labelCamera.isActiveAndEnabled)
            {
                labelCamera = Camera.main;
            }

            if (labelCamera != null)
            {
                // TextMesh faces its local -Z axis, matching a camera-facing billboard.
                nameLabel.transform.rotation = labelCamera.transform.rotation;
                // Close-range only (camera Phase 4): a name over every head from across the house is
                // a screen of labels; at a conversation's distance it is who you are talking to.
                float away = Vector3.Distance(labelCamera.transform.position, nameLabel.transform.position);
                NameTagAlpha = Mathf.Clamp01((NameTagFar - away) / Mathf.Max(0.01f, NameTagFar - NameTagNear));
                // A tag is sized for the dollhouse's distances; a director's shot comes within a few
                // metres, where the same tag would fill the top of the frame. Inside the near
                // distance it shrinks with the range instead, down to a floor that still reads.
                if (!nameTagScaleKnown) { nameTagScale = nameLabel.transform.localScale; nameTagScaleKnown = true; }
                float near = Mathf.Clamp(away / NameTagNear, NameTagNearestScale, 1f);
                nameLabel.transform.localScale = nameTagScale * near;
                // And it comes down toward the head as it shrinks. At the authored height it floats
                // clear of the heads around it from the dollhouse's distance; in a two-shot the same
                // height put it in the top bar's band, and mockup-12 keeps the name over the head.
                // All the way down by a conversation's distance, and no further closer in, where it
                // would sink into the head it names.
                if ((seat == null || !seat.Active) && labelPositionKnown)
                    nameLabel.transform.localPosition = labelRestPosition - Vector3.up * Mathf.InverseLerp(1f, .5f, near) * NameTagCloseDrop;
                var colour = nameLabel.color;
                if (!Mathf.Approximately(colour.a, NameTagAlpha)) { colour.a = NameTagAlpha; nameLabel.color = colour; }
                if (plateGroup != null) plateGroup.alpha = PlateSuppressed ? 0f : spotlit ? 1f : NameTagAlpha;
            }
        }

        /// <summary>Inside this distance the name tag is fully shown; beyond <see cref="NameTagFar"/> it is gone.</summary>
        public const float NameTagNear = 9f, NameTagFar = 14f;
        /// <summary>How small a tag gets, as a share of its authored size, when a shot is right on top of it.</summary>
        public const float NameTagNearestScale = 0.3f;
        /// <summary>How far a tag comes down toward the head, in metres, by half the near distance.</summary>
        public const float NameTagCloseDrop = 0.45f;
        private Vector3 nameTagScale;
        private bool nameTagScaleKnown;
        /// <summary>How much of the name tag the camera's distance leaves visible, 0 to 1.</summary>
        public float NameTagAlpha { get; private set; } = 1f;
    }
}
