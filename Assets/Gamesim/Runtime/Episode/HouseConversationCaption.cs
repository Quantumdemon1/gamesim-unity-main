using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>Receives names and an allowlisted topic only; never receives a simulation snapshot/effect/target.</summary>
    public sealed class HouseConversationCaption : MonoBehaviour
    {
        private GameObject root;
        private Text label;
        public string CurrentText => root != null && root.activeSelf ? label.text : "";

        public void Show(string firstName, string secondName, string topic, float fontScale)
        {
            if (root == null) Create();
            label.text = Describe(firstName, secondName, topic);
            label.fontSize = Mathf.RoundToInt(19 * Mathf.Clamp(fontScale, 1, 1.2f));
            root.SetActive(true);
        }
        public void Hide() { if (root != null) root.SetActive(false); }
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
        private void Create()
        {
            root = new GameObject("Observed house conversation", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 69;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            var panel = new GameObject("Witnessed generic topic", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            var rect = panel.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
            rect.pivot = new Vector2(.5f, 1); rect.anchoredPosition = new Vector2(0, -108); rect.sizeDelta = new Vector2(650, 70);
            var background = panel.GetComponent<Image>(); background.color = new Color(.035f, .055f, .085f, .95f); background.raycastTarget = false;
            var text = new GameObject("Caption", typeof(RectTransform), typeof(Text)); text.transform.SetParent(panel.transform, false);
            var textRect = text.GetComponent<RectTransform>(); textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16, 6); textRect.offsetMax = new Vector2(-16, -6);
            label = text.GetComponent<Text>(); label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.color = new Color(.95f, .96f, .98f); label.alignment = TextAnchor.MiddleCenter;
            label.supportRichText = false; label.raycastTarget = false;
        }
        private void OnDisable() => Hide();
        private void OnDestroy() { if (root != null) Destroy(root); }
    }
}
