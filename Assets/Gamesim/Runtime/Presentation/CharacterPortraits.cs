using System.Collections.Generic;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// Renders each houseguest's head to a texture the HUD can show, so a game about six people
    /// actually shows their faces.
    ///
    /// Portraits are drawn <b>unlit</b>, from each material's own base colour. That is a deliberate
    /// choice rather than a shortcut: the cast is flat-shaded low-poly, so flat portraits match the
    /// art, and — measured, not assumed — an offscreen camera here does not lit-shade the subject
    /// at all. With every light disabled and pure black ambient the skin still rendered white,
    /// which a lit surface cannot do. Unlit makes the output exact and independent of how the set
    /// happens to be lit, which is what a portrait wants anyway.
    ///
    /// The rig lives far below the house with a short far-clip, so it sees only its own subject and
    /// no layer needs reserving. Each portrait is rendered once and cached; nothing renders per frame.
    /// </summary>
    public static class CharacterPortraits
    {
        public const int Size = 192;
        private const string ModelResourceRoot = "GamesimCharacters/";
        private static readonly Vector3 RigOrigin = new Vector3(0f, -5000f, 0f);

        private static readonly Dictionary<string, RenderTexture> Cache = new Dictionary<string, RenderTexture>();
        private static readonly Dictionary<Material, Material> UnlitCache = new Dictionary<Material, Material>();
        private static GameObject rig;
        private static Camera rigCamera;

        /// <summary>The portrait for a persona, or null when that persona has no authored model.</summary>
        public static Texture Get(string appearanceId)
        {
            if (string.IsNullOrEmpty(appearanceId)) return null;
            if (Cache.TryGetValue(appearanceId, out var cached) && cached != null) return cached;

            var prefab = Resources.Load<GameObject>(ModelResourceRoot + appearanceId);
            if (prefab == null) return null;

            var texture = Render(prefab);
            Cache[appearanceId] = texture;
            return texture;
        }

        /// <summary>Drops every cached portrait and tears the rig down. Call on season teardown.</summary>
        public static void Release()
        {
            foreach (var texture in Cache.Values)
            {
                if (texture == null) continue;
                texture.Release();
                Destroy(texture);
            }
            Cache.Clear();

            foreach (var material in UnlitCache.Values) Destroy(material);
            UnlitCache.Clear();

            if (rig != null) Destroy(rig);
            rig = null;
            rigCamera = null;
        }

        private static void EnsureRig()
        {
            if (rig != null) return;

            rig = new GameObject("Gamesim Portrait Rig") { hideFlags = HideFlags.DontSave };
            rig.transform.position = RigOrigin;

            var cameraObject = new GameObject("Portrait Camera") { hideFlags = HideFlags.DontSave };
            cameraObject.transform.SetParent(rig.transform, false);
            rigCamera = cameraObject.AddComponent<Camera>();
            rigCamera.clearFlags = CameraClearFlags.SolidColor;
            rigCamera.backgroundColor = UiTheme.SurfaceRaised;
            rigCamera.fieldOfView = 24f;
            rigCamera.nearClipPlane = 0.05f;
            // A short far plane is what keeps the house out of frame without reserving a layer.
            rigCamera.farClipPlane = 8f;
            rigCamera.enabled = false;
        }

        private static RenderTexture Render(GameObject prefab)
        {
            EnsureRig();

            var subject = Object.Instantiate(prefab, rig.transform);
            subject.hideFlags = HideFlags.DontSave;
            subject.transform.localPosition = Vector3.zero;
            subject.transform.localRotation = Quaternion.identity;

            foreach (var collider in subject.GetComponentsInChildren<Collider>(true)) Destroy(collider);
            // A portrait is a still; an Animator would leave the pose mid-blend on the first frame.
            foreach (var animator in subject.GetComponentsInChildren<Animator>(true)) animator.enabled = false;

            Flatten(subject);
            Frame(subject);

            var texture = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32)
            {
                name = "Portrait " + prefab.name,
                antiAliasing = 2,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            texture.Create();

            rigCamera.targetTexture = texture;
            rigCamera.Render();
            rigCamera.targetTexture = null;

            Destroy(subject);
            return texture;
        }

        /// <summary>Swaps every slot for an unlit material carrying that slot's own base colour.</summary>
        private static void Flatten(GameObject subject)
        {
            var unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null) return;

            foreach (var renderer in subject.GetComponentsInChildren<Renderer>(true))
            {
                var source = renderer.sharedMaterials;
                var flattened = new Material[source.Length];
                for (int i = 0; i < source.Length; i++) flattened[i] = Unlit(source[i], unlitShader);
                renderer.sharedMaterials = flattened;
            }
        }

        private static Material Unlit(Material source, Shader unlitShader)
        {
            if (source == null) return null;
            if (UnlitCache.TryGetValue(source, out var existing) && existing != null) return existing;

            var colour = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor")
                       : source.HasProperty("_Color") ? source.GetColor("_Color")
                       : Color.white;

            var flat = new Material(unlitShader) { name = source.name + " (portrait)", hideFlags = HideFlags.DontSave };
            if (flat.HasProperty("_BaseColor")) flat.SetColor("_BaseColor", colour);
            UnlitCache[source] = flat;
            return flat;
        }

        /// <summary>
        /// Frames head and shoulders from the model's real geometry. The "Head" bone is the joint at
        /// the base of the skull, not its centre — on this stylised cast the head reaches well above
        /// it — so the crown comes from the render bounds and the bone only marks the jawline.
        /// </summary>
        private static void Frame(GameObject subject)
        {
            var renderers = subject.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);

            var head = FindBone(subject.transform, "Head");
            float jaw = head != null ? head.position.y - 0.12f : bounds.max.y - 0.60f;
            float crown = bounds.max.y + 0.05f;
            float centreY = (crown + jaw) * 0.5f;
            float extent = Mathf.Max((crown - jaw) * 0.5f, 0.05f);

            var focus = new Vector3(
                head != null ? head.position.x : bounds.center.x,
                centreY,
                head != null ? head.position.z : bounds.center.z);

            float distance = extent / Mathf.Tan(rigCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            rigCamera.transform.position = focus + subject.transform.forward * distance;
            rigCamera.transform.rotation = Quaternion.LookRotation(focus - rigCamera.transform.position, Vector3.up);
        }

        private static Transform FindBone(Transform root, string boneName)
        {
            foreach (var candidate in root.GetComponentsInChildren<Transform>(true))
                if (candidate.name == boneName) return candidate;
            return null;
        }

        private static void Destroy(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }
    }
}
