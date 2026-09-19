using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The live feed (VISUAL-TARGET.md phase V5): a second, small camera on the house, aimed
    /// wherever the director says the action is, rendered into a card-sized texture every third
    /// frame from above the walls. Never the main camera: the name tags and the follow ring read
    /// <see cref="Camera.main"/>, and a feed that took the tag would turn every label toward it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LiveFeed : MonoBehaviour
    {
        public const string CameraName = "Live feed camera";
        public const int Width = 320, Height = 180;
        /// <summary>One render in this many frames: a card, not a second viewport.</summary>
        public const int EveryNthFrame = 3;
        /// <summary>The eye, relative to the subject and the feed's yaw: over the wall line, looking down.</summary>
        private static readonly Vector3 Offset = new Vector3(0f, 3.4f, -3.2f);

        private Camera feedCamera;
        private RenderTexture texture;
        private Vector3 subject;
        private float yaw;

        public static LiveFeed Attach(GameObject host)
        {
            if (host == null) return null;
            var existing = host.GetComponent<LiveFeed>();
            if (existing != null) return existing;
            var feed = host.AddComponent<LiveFeed>();
            feed.Build();
            return feed;
        }

        public Texture Texture => texture;
        public Camera Camera => feedCamera;
        public bool HasSubject { get; private set; }
        public Vector3 Subject => subject;
        /// <summary>While a panel is up the house is paused and the card is under it; nothing to render.</summary>
        public bool Paused { get; set; }

        /// <summary>Aims the feed at a point in the house, from a yaw, above the walls.</summary>
        public void Aim(Vector3 at, float fromYaw)
        {
            subject = at;
            yaw = fromYaw;
            HasSubject = true;
        }

        public void Clear() => HasSubject = false;

        private void Build()
        {
            var holder = new GameObject(CameraName);
            holder.transform.SetParent(transform, false);
            feedCamera = holder.AddComponent<Camera>();
            // Rendered by hand, so a disabled camera: Unity never schedules it, and Camera.main
            // (which wants an enabled camera tagged MainCamera) cannot pick it even by accident.
            feedCamera.enabled = false;
            feedCamera.fieldOfView = 45f;
            feedCamera.nearClipPlane = 0.15f;
            feedCamera.farClipPlane = 40f;
            feedCamera.clearFlags = CameraClearFlags.SolidColor;
            feedCamera.backgroundColor = UiTheme.Background;
            feedCamera.allowMSAA = false;
            var data = feedCamera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.antialiasing = AntialiasingMode.None;
            texture = new RenderTexture(Width, Height, 16, RenderTextureFormat.ARGB32)
            {
                name = "Live feed", filterMode = FilterMode.Bilinear,
            };
            feedCamera.targetTexture = texture;
        }

        private void LateUpdate()
        {
            if (feedCamera == null || texture == null || !HasSubject || Paused) return;
            if (Time.frameCount % EveryNthFrame != 0) return;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            var eye = subject + Quaternion.Euler(0f, yaw, 0f) * Offset;
            feedCamera.transform.position = eye;
            feedCamera.transform.rotation = Quaternion.LookRotation(subject + Vector3.up * 0.9f - eye, Vector3.up);
            feedCamera.Render();
        }

        private void OnDestroy()
        {
            if (texture == null) return;
            texture.Release();
            Destroy(texture);
            texture = null;
        }
    }
}
