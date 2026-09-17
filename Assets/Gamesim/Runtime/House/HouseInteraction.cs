using Gamesim.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Gamesim.House
{
    /// <summary>Nearby conversation and a lightweight screen-space house HUD.</summary>
    [DisallowMultipleComponent]
    public sealed class HouseInteraction : MonoBehaviour
    {
        private const float TalkDistance = 2.8f;
        private const float EyeHeight = 1.15f;
        // Shared with the episode HUD through UiTheme; Mint is kept as a name so the existing
        // call sites read unchanged, but it now resolves to the set's pale neon accent.
        private static readonly Color Ink = UiTheme.Ink;
        private static readonly Color Surface = UiTheme.Surface;
        private static readonly Color Mint = UiTheme.Accent;
        private static readonly Color White = UiTheme.Paper;

        [SerializeField] private HousePlayerController player;
        [SerializeField] private HouseNpc npc;
        [SerializeField] private HouseCameraRig rig;

        private readonly RaycastHit[] sightHits = new RaycastHit[32];
        private Canvas hud;
        private TMP_FontAsset font;
        private GameObject prompt;
        private TMP_Text promptText;
        private GameObject conversation;
        private RectTransform conversationRect;
        private TMP_Text conversationName;
        private TMP_Text conversationText;
        private Button[] choices;
        private Button exitButton;
        private bool restorePlayerInput;

        public bool IsDialogueOpen { get; private set; }

        /// <summary>Talking requires proximity and a clear view; walls cannot be talked through.</summary>
        public bool CanInteract => Application.isPlaying && isActiveAndEnabled && !IsDialogueOpen &&
            player != null && player.isActiveAndEnabled && npc != null && npc.isActiveAndEnabled &&
            rig != null && rig.isActiveAndEnabled && rig.ViewCamera != null &&
            (player.transform.position - npc.transform.position).sqrMagnitude <= TalkDistance * TalkDistance &&
            HasLineOfSight();

        public void Configure(HousePlayerController housePlayer, HouseNpc houseNpc, HouseCameraRig cameraRig)
        {
            EndDialogue();
            player = housePlayer;
            npc = houseNpc;
            rig = cameraRig;
            RefreshIdentity();
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                EnsureHud();
            }
        }

        private System.Collections.IEnumerator Start()
        {
            // Player.Start enables navigation after scene NavMesh registration.
            yield return null;
            if (player != null && player.Agent != null && player.Agent.isOnNavMesh &&
                npc != null && rig != null && rig.ViewCamera != null && hud != null)
            {
                Debug.Log("Gamesim U02 house ready: player on NavMesh; camera, Maya and HUD connected.");
            }
            else
            {
                Debug.LogError("Gamesim U02 house could not initialize its player, navigation, camera, NPC or HUD.");
            }
        }

        private void Update()
        {
            if (hud == null)
            {
                EnsureHud();
            }

            if (IsDialogueOpen && (player == null || !player.isActiveAndEnabled ||
                npc == null || !npc.isActiveAndEnabled || rig == null || !rig.isActiveAndEnabled))
            {
                EndDialogue();
            }

            bool available = CanInteract;
            if (prompt != null)
            {
                prompt.SetActive(available);
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (IsDialogueOpen && keyboard.escapeKey.wasPressedThisFrame)
                {
                    EndDialogue();
                }
                else if (IsDialogueOpen)
                {
                    if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
                    {
                        SelectResponse(0);
                    }
                    else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
                    {
                        SelectResponse(1);
                    }
                    else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
                    {
                        SelectResponse(2);
                    }
                }
                else if (available && keyboard.eKey.wasPressedThisFrame)
                {
                    TryBeginDialogue();
                }
            }

            if (conversationRect != null && hud != null)
            {
                float canvasWidth = ((RectTransform)hud.transform).rect.width;
                conversationRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                    Mathf.Max(280f, Mathf.Min(720f, canvasWidth - 48f)));
            }
        }

        public bool TryBeginDialogue()
        {
            if (!CanInteract)
            {
                return false;
            }

            EnsureHud();
            restorePlayerInput = player.InputEnabled;
            player.SetInputEnabled(false);
            rig.SetConversationFocus(player.transform, npc.transform);
            IsDialogueOpen = true;
            RefreshIdentity();
            conversationText.text = "Hey, I'm " + npc.DisplayName + ". Welcome in! The living room is a good place to get your bearings. " +
                "Take a look around, then come say hello.";
            conversation.SetActive(true);
            prompt.SetActive(false);
            return true;
        }

        public void EndDialogue()
        {
            if (IsDialogueOpen)
            {
                IsDialogueOpen = false;
                if (player != null)
                {
                    player.SetInputEnabled(restorePlayerInput);
                }

                if (rig != null)
                {
                    rig.EndConversation();
                }
            }

            if (conversation != null)
            {
                conversation.SetActive(false);
            }
        }

        private bool HasLineOfSight()
        {
            Vector3 origin = player.transform.position + Vector3.up * EyeHeight;
            Vector3 offset = npc.transform.position + Vector3.up * EyeHeight - origin;
            float distance = offset.magnitude;
            if (distance < 0.001f)
            {
                return true;
            }

            int count = Physics.RaycastNonAlloc(origin, offset / distance, sightHits, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            // A saturated buffer must not accidentally hide an occluding wall.
            RaycastHit[] hits = count == sightHits.Length
                ? Physics.RaycastAll(origin, offset / distance, distance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                : sightHits;
            if (hits != sightHits)
            {
                count = hits.Length;
            }

            for (int index = 0; index < count; index++)
            {
                Transform hit = hits[index].transform;
                if (hit == null || hit == transform || hit.IsChildOf(player.transform) ||
                    hit.IsChildOf(npc.transform))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private void EnsureHud()
        {
            if (hud != null || !Application.isPlaying)
            {
                return;
            }

            font = TMP_Settings.defaultFontAsset != null
                ? TMP_Settings.defaultFontAsset
                : Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            GameObject canvasObject = new GameObject("Gamesim House HUD", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            hud = canvasObject.GetComponent<Canvas>();
            hud.renderMode = RenderMode.ScreenSpaceOverlay;
            hud.sortingOrder = 50;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform brand = Panel("Identity", hud.transform, Ink);
            Anchor(brand, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f),
                new Vector2(306f, 104f));
            Label("Brand", brand, "GAMESIM", 29, White, new Vector2(20f, -15f), new Vector2(266f, 37f));
            Label("Slice", brand, "HOUSE PROTOTYPE", 15, Mint, new Vector2(21f, -59f), new Vector2(266f, 24f));

            RectTransform controls = Panel("Controls", hud.transform, Ink);
            // Five lines for the same reason the episode HUD's panel has five: click-to-follow is a
            // new control and lengthening an existing line clips it. The panel grew instead.
            Anchor(controls, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f),
                new Vector2(352f, 175f));
            Label("Control heading", controls, "MAKE YOURSELF AT HOME", 15, Mint,
                new Vector2(18f, -13f), new Vector2(320f, 24f));
            Label("Control detail", controls,
                "Click a houseguest: follow\nClick floor: walk   ·   F: recenter\nWASD / arrows: pan   ·   Scroll: zoom\nRight-drag: orbit\nE: talk nearby   ·   Esc: leave",
                17, White, new Vector2(18f, -44f), new Vector2(320f, 122f));

            RectTransform promptRect = Panel("Talk prompt", hud.transform, Ink);
            Anchor(promptRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f),
                new Vector2(330f, 54f));
            prompt = promptRect.gameObject;
            promptText = Label("Prompt text", promptRect, "", 21, Mint,
                new Vector2(16f, -8f), new Vector2(298f, 38f));
            promptText.alignment = TextAlignmentOptions.Center;
            prompt.SetActive(false);

            conversationRect = Panel("Conversation", hud.transform, Ink);
            Anchor(conversationRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f),
                new Vector2(720f, 396f));
            conversation = conversationRect.gameObject;
            conversationName = Label("Character name", conversationRect, "", 26, Mint,
                new Vector2(24f, -18f), new Vector2(650f, 36f));
            StretchWidth(conversationName.rectTransform, 24f, 24f);
            conversationText = Label("Conversation text", conversationRect, "", 20, White,
                new Vector2(24f, -61f), new Vector2(672f, 104f));
            conversationText.lineSpacing = 1.1f;
            StretchWidth(conversationText.rectTransform, 24f, 24f);

            string[] labels = { "[1]   Where should I start?", "[2]   What's your favorite spot?", "[3]   How are you settling in?" };
            choices = new Button[labels.Length];
            for (int index = 0; index < choices.Length; index++)
            {
                int choice = index;
                choices[index] = MakeButton("Response " + (index + 1), conversationRect, labels[index],
                    -172f - index * 51f, Surface, White);
                choices[index].onClick.AddListener(() => SelectResponse(choice));
            }

            exitButton = MakeButton("Leave conversation", conversationRect, "Back to exploring    [Esc]", -330f,
                new Color(0.12f, 0.25f, 0.25f, 1f), Mint);
            exitButton.onClick.AddListener(EndDialogue);
            conversation.SetActive(false);
            RefreshIdentity();
        }

        private void SelectResponse(int choice)
        {
            if (!IsDialogueOpen || conversationText == null)
            {
                return;
            }

            switch (choice)
            {
                case 0:
                    conversationText.text = "Start with the living room, then have a wander through the kitchen. " +
                        "The bedrooms are a little quieter. You'll know your way around soon.";
                    break;
                case 1:
                    conversationText.text = "The sofa, honestly. I like having a comfortable place to sit and talk. " +
                        "Though I can usually be tempted into the kitchen.";
                    break;
                default:
                    conversationText.text = "Still finding my feet! A new house always feels a little strange at first. " +
                        "It's nice to have someone to say hello to.";
                    break;
            }
        }

        private void RefreshIdentity()
        {
            string characterName = npc != null ? npc.DisplayName : "Maya";
            if (conversationName != null)
            {
                conversationName.text = characterName;
            }

            if (promptText != null)
            {
                promptText.text = "[ E ]   Talk to " + characterName;
            }
        }

        private Button MakeButton(string name, Transform parent, string caption, float top,
            Color background, Color foreground)
        {
            RectTransform rect = Panel(name, parent, background);
            Anchor(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, top),
                new Vector2(672f, 43f));
            StretchWidth(rect, 24f, 24f);
            rect.GetComponent<Image>().raycastTarget = true;
            Button button = rect.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.3f, 1.45f, 1.45f, 1f);
            colors.pressedColor = new Color(0.8f, 1.1f, 1.0f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.colorMultiplier = 1f;
            button.colors = colors;
            TMP_Text text = Label("Caption", rect, caption, 18, foreground,
                new Vector2(14f, -2f), new Vector2(644f, 39f));
            StretchWidth(text.rectTransform, 14f, 14f);
            text.alignment = TextAlignmentOptions.Left;
            return button;
        }

        private static RectTransform Panel(string name, Transform parent, Color background)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            Image image = panel.GetComponent<Image>();
            UiTheme.Style(image, background, UiTheme.ControlRadius);
            // Visible HUD surfaces consume pointer input so clicks/drags cannot leak into the house.
            image.raycastTarget = true;
            return panel.GetComponent<RectTransform>();
        }

        private TMP_Text Label(string name, Transform parent, string value, int size, Color color,
            Vector2 position, Vector2 dimensions)
        {
            GameObject label = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(parent, false);
            TMP_Text text = label.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.text = value;
            text.richText = false;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Truncate;
            Anchor(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), position, dimensions);
            return text;
        }

        private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 pivot,
            Vector2 position, Vector2 dimensions)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.sizeDelta = dimensions;
            rect.anchoredPosition = position;
        }

        private static void StretchWidth(RectTransform rect, float left, float right)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(left, rect.offsetMin.y);
            rect.offsetMax = new Vector2(-right, rect.offsetMax.y);
        }

        private void OnDisable()
        {
            EndDialogue();
            DestroyHud();
        }

        private void OnDestroy()
        {
            EndDialogue();
            DestroyHud();
        }

        private void DestroyHud()
        {
            if (choices != null)
            {
                foreach (Button button in choices)
                {
                    if (button != null)
                    {
                        button.onClick.RemoveAllListeners();
                    }
                }
            }

            if (exitButton != null)
            {
                exitButton.onClick.RemoveAllListeners();
            }

            if (hud != null)
            {
                hud.gameObject.SetActive(false);
                Destroy(hud.gameObject);
            }

            hud = null;
            conversation = null;
            conversationRect = null;
            conversationName = null;
            conversationText = null;
            prompt = null;
            promptText = null;
            choices = null;
            exitButton = null;
        }
    }
}
