using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// What a passer-by is told about a conversation they have walked in on.
    ///
    /// <para>Receives names and an allowlisted topic only; never receives a simulation
    /// snapshot/effect/target.</para>
    ///
    /// <para>V2 (VISUAL-TARGET.md §5, mockups 01, 06 and 09) gives it somewhere to point. It used
    /// to be a banner at the top of the screen, which said who was talking but not where they were:
    /// in a house of twelve, "Maya and Riley are discussing the game" sent the player looking. The
    /// mockups put the line in a bubble over the pair's heads with a tail pointing down at them, so
    /// the sentence and the people arrive together. Given a world anchor it does that; given none
    /// it stays the banner it was, which is what the ambient-caption test drives.</para>
    ///
    /// <para>The bubble is clamped rather than free. A caption that follows a pair across the room
    /// will otherwise walk under the house pill at the top of the screen, and a line of prose over
    /// the week counter is worse than a line of prose in the wrong place.</para>
    /// </summary>
    public sealed class HouseConversationCaption : MonoBehaviour
    {
        /// <summary>The panel's name, which the ambient-caption test finds it by.</summary>
        public const string PanelName = "Witnessed generic topic";
        /// <summary>The tail's name, so a test can tell a bubble from a banner.</summary>
        public const string TailName = "Caption tail";

        private const float BannerWidth = 650f, BannerHeight = 70f;
        private const float BubbleWidth = 430f, BubbleHeight = 62f;
        /// <summary>How far over the anchor the bubble floats, in canvas units.</summary>
        private const float BubbleRise = 26f;
        private const float SideMargin = 24f;

        private GameObject root;
        private RectTransform canvasRect, panel, tail;
        private TMP_Text label;
        private Vector3 anchorWorld;
        private bool anchored;
        private EpisodeHud episodeHud;

        public string CurrentText => root != null && root.activeSelf ? label.text : "";

        /// <summary>The banner: centred at the top, where it has always been.</summary>
        public void Show(string firstName, string secondName, string topic, float fontScale)
        {
            Fill(firstName, secondName, topic, fontScale);
            anchored = false;
            panel.anchorMin = panel.anchorMax = Vector2.zero;
            panel.pivot = new Vector2(.5f, 1f);
            PlaceBanner();
            tail.gameObject.SetActive(false);
        }

        /// <summary>The bubble: over the pair, with a tail pointing down at them.</summary>
        public void Show(string firstName, string secondName, string topic, float fontScale, Vector3 worldAnchor)
        {
            Fill(firstName, secondName, topic, fontScale);
            anchored = true;
            anchorWorld = worldAnchor;
            panel.anchorMin = panel.anchorMax = Vector2.zero;
            panel.pivot = new Vector2(.5f, 0f);
            SizeForText(BubbleWidth,BubbleHeight);
            tail.gameObject.SetActive(true);
            Place();
        }

        public void Hide() { if (root != null) root.SetActive(false); }

        private void LateUpdate()
        {
            if(root==null || !root.activeSelf)return;
            if(anchored)Place();else PlaceBanner();
        }

        private Rect SafeBounds()
        {
            if(episodeHud==null)episodeHud=GetComponent<EpisodeHud>();
            var size=canvasRect.rect.size;
            return episodeHud!=null ? episodeHud.WorldCaptionSafeBounds
                : Rect.MinMaxRect(SideMargin,100f,size.x-SideMargin,size.y-104f);
        }

        private void PlaceBanner()
        {
            if(canvasRect==null)return;
            var safe=SafeBounds();
            SizeForText(Mathf.Min(BannerWidth,safe.width),BannerHeight);
            panel.anchoredPosition=new Vector2(safe.center.x,safe.yMax);
        }

        /// <summary>
        /// Puts the bubble over its anchor, in the canvas's own units, and keeps it on screen.
        ///
        /// <para>Behind the camera the projection flips, so a pair the player has turned away from
        /// would otherwise have its caption reappear mirrored on the far side. That case hides the
        /// bubble instead: the witness rule already says the player can see them.</para>
        /// </summary>
        private void Place()
        {
            var camera = Camera.main;
            if (camera == null || canvasRect == null) return;

            var screen = camera.WorldToScreenPoint(anchorWorld);
            if (screen.z <= 0f) { panel.gameObject.SetActive(false); return; }
            panel.gameObject.SetActive(true);

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, screen, null, out var local)) return;

            var size = canvasRect.rect.size;
            var safe=SafeBounds();
            float width=Mathf.Min(label.text.Length>85 ? BannerWidth : BubbleWidth,safe.width);
            SizeForText(width,BubbleHeight);
            float halfWidth = panel.sizeDelta.x * .5f;
            float x = Mathf.Clamp(local.x + size.x * .5f, safe.xMin+halfWidth,safe.xMax-halfWidth);
            float y = Mathf.Clamp(local.y + size.y * .5f + BubbleRise,
                safe.yMin,Mathf.Max(safe.yMin,safe.yMax-panel.sizeDelta.y));
            panel.anchoredPosition = new Vector2(x, y);
            tail.anchoredPosition=new Vector2(Mathf.Clamp(local.x+size.x*.5f-x,-halfWidth+20f,halfWidth-20f),2f);
        }

        private void SizeForText(float width,float minimumHeight)
        {
            float preferred=label.GetPreferredValues(label.text,Mathf.Max(1,width-32f),float.PositiveInfinity).y+18f;
            panel.sizeDelta=new Vector2(width,Mathf.Max(minimumHeight,preferred));
        }

        public static string Describe(string firstName, string secondName, string topic)
        {
            string action = topic == "strategy" ? "are discussing the game."
                : topic == "gossip" ? "are discussing house gossip."
                : topic == "tension" || topic == "rivalry" ? "are having a tense conversation."
                : topic == "nominations" ? "are discussing nominations."
                : topic == "alliance_talk" ? "are discussing working together."
                : "are chatting.";
            return firstName + " and " + secondName + " " + action;
        }

        private void Fill(string firstName, string secondName, string topic, float fontScale)
        {
            if (root == null) Create();
            label.text = Describe(firstName, secondName, topic);
            label.fontSize = Mathf.RoundToInt(19 * Mathf.Clamp(fontScale, 1, 1.2f));
            root.SetActive(true);
        }

        private void Create()
        {
            root = new GameObject("Observed house conversation", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 69;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            canvasRect = (RectTransform)root.transform;

            var panelObject = new GameObject(PanelName, typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(root.transform, false);
            panel = panelObject.GetComponent<RectTransform>();
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, 1);
            panel.pivot = new Vector2(.5f, 1); panel.anchoredPosition = new Vector2(0, -108);
            panel.sizeDelta = new Vector2(BannerWidth, BannerHeight);
            var background = panelObject.GetComponent<Image>();
            Gamesim.Presentation.UiTheme.Style(background, Gamesim.Presentation.UiTheme.Ink, Gamesim.Presentation.UiTheme.PanelRadius);
            background.raycastTarget = false;

            // The tail, as a square stood on its corner under the bubble's middle. A triangle would
            // need a sprite; this needs nothing and reads the same at the size it is drawn.
            var tailObject = new GameObject(TailName, typeof(RectTransform), typeof(Image));
            tailObject.transform.SetParent(panelObject.transform, false);
            tail = tailObject.GetComponent<RectTransform>();
            tail.anchorMin = tail.anchorMax = new Vector2(.5f, 0f);
            tail.pivot = new Vector2(.5f, .5f);
            tail.anchoredPosition = new Vector2(0f, 2f);
            tail.sizeDelta = new Vector2(18f, 18f);
            tail.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var tailImage = tailObject.GetComponent<Image>();
            tailImage.color = Gamesim.Presentation.UiTheme.Ink;
            tailImage.raycastTarget = false;
            tailObject.SetActive(false);

            var text = new GameObject("Caption", typeof(RectTransform), typeof(TextMeshProUGUI)); text.transform.SetParent(panelObject.transform, false);
            var textRect = text.GetComponent<RectTransform>(); textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16, 6); textRect.offsetMax = new Vector2(-16, -6);
            label = text.GetComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset != null
                ? TMP_Settings.defaultFontAsset
                : Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            label.color = new Color(.95f, .96f, .98f); label.alignment = TextAlignmentOptions.Center;
            label.richText = false; label.raycastTarget = false;
            label.enableAutoSizing = false;
            label.textWrappingMode = TextWrappingModes.Normal;
        }

        private void OnDisable() => Hide();
        private void OnDestroy() { if (root != null) Destroy(root); }
    }
}
