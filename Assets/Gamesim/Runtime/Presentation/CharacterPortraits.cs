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
        /// <summary>Portrait pixels a side (VISUAL-TARGET.md V3): the mockups' headshots, not a thumbnail.</summary>
        public const int Size = 384;
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

        /// <summary>
        /// The portrait for a houseguest whose body is generated at runtime, rendered from the body
        /// standing in the house rather than from a prefab.
        ///
        /// <para>UMA characters have no prefab to instantiate — they are assembled at runtime — so
        /// the prefab path returns nothing for them, and the HUD ends up showing authored faces
        /// while the world shows generated ones. Nobody chose that; it is just what happens when the
        /// two systems answer the same question differently.</para>
        ///
        /// <para>This points the portrait camera at the live body instead of moving the body to the
        /// camera. Two consequences worth stating. The short far plane that keeps the house out of
        /// frame is doing more work here, because the subject is standing in the house rather than
        /// alone under it — a houseguest close behind another can appear in the shot. And the live
        /// materials are never touched, so unlike the prefab path this cannot flatten anything the
        /// player is looking at; the subject is lit for the capture instead.</para>
        ///
        /// <para>Nothing is cached until the body actually has geometry. A UMA character is empty
        /// for the first frames of its assembly, and a blank portrait cached once would stay blank
        /// for the rest of the season.</para>
        /// </summary>
        public static Texture GetLive(string contestantId, Transform body)
        {
            if (string.IsNullOrEmpty(contestantId) || body == null) return null;

            // Namespaced, because the two paths share one cache and the ids collide: a houseguest's
            // contestant id and their appearance id are the same string for this cast, so an early
            // render that fell back to the prefab would poison the live key and the HUD would show
            // authored faces for the rest of the season no matter how many bodies finished.
            string cacheKey = "live:" + contestantId;
            if (Cache.TryGetValue(cacheKey, out var cached) && cached != null) return cached;

            var skins = body.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skins.Length == 0) return null;

            var extent = skins[0].bounds;
            foreach (var skin in skins) extent.Encapsulate(skin.bounds);
            if (extent.size.y < 0.5f) return null; // still assembling

            var texture = RenderLive(body, extent, contestantId);
            if (texture != null) Cache[cacheKey] = texture;
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
            // The mockups' headshots sit on the night ground, not on a panel colour.
            rigCamera.backgroundColor = UiTheme.Background;
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

        /// <summary>
        /// Renders a body where it stands. The camera is moved to the subject and a light is created
        /// for the shot, so nothing about the live character changes.
        /// </summary>
        private static RenderTexture RenderLive(Transform body, Bounds extent, string name)
        {
            EnsureRig();

            float previousFar = rigCamera.farClipPlane;
            var previousParent = rigCamera.transform.parent;
            rigCamera.transform.SetParent(null, true);

            // The same framing the prefab path uses: head bone for the focus, jaw to crown for the
            // extent, distance solved from the field of view. Three attempts at hand-placing this
            // camera produced a blown-out white disc, a forehead, and a neck — because a fraction of
            // body height is the wrong ruler when hair changes where the crown is and the cast is
            // deliberately not all one height.
            Frame(body.gameObject);

            // Far enough to clear the subject, short enough to leave the house behind it out of
            // frame. Measured from where Frame put the camera rather than assumed.
            float distance = Vector3.Distance(rigCamera.transform.position, body.position);
            rigCamera.farClipPlane = distance + 0.75f;

            // Directional rather than a point light. A point light close enough to reach a face this
            // small arrives at an intensity that blows the whole portrait to white, and the distance
            // that fixes that is past the far plane. A directional light does not fall off.
            // Three points (VISUAL-TARGET.md V3): a soft warm key from high on one side, a low fill
            // from the other so the shadow side keeps its shape, and a cool rim from behind that
            // separates hair and shoulders from the dark ground. All directional, for the reason
            // above; none shadowing, because the far plane is inches behind the head.
            var lamp = new GameObject("Portrait Light") { hideFlags = HideFlags.DontSave };
            lamp.transform.SetParent(rigCamera.transform, false);
            lamp.transform.localRotation = Quaternion.Euler(24f, -32f, 0f);
            var light = lamp.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.05f;
            light.shadows = LightShadows.None;
            light.color = new Color(1f, 0.95f, 0.88f);
            var fillLamp = new GameObject("Portrait Fill") { hideFlags = HideFlags.DontSave };
            fillLamp.transform.SetParent(rigCamera.transform, false);
            fillLamp.transform.localRotation = Quaternion.Euler(8f, 40f, 0f);
            var fill = fillLamp.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.35f;
            fill.shadows = LightShadows.None;
            fill.color = new Color(0.85f, 0.9f, 1f);
            var rimLamp = new GameObject("Portrait Rim") { hideFlags = HideFlags.DontSave };
            rimLamp.transform.SetParent(rigCamera.transform, false);
            rimLamp.transform.localRotation = Quaternion.Euler(-20f, 150f, 0f);
            var rim = rimLamp.AddComponent<Light>();
            rim.type = LightType.Directional;
            rim.intensity = 0.9f;
            rim.shadows = LightShadows.None;
            rim.color = UiTheme.Glow;

            var texture = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32)
            {
                name = "Portrait " + name,
                antiAliasing = 2,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            texture.Create();

            rigCamera.targetTexture = texture;
            rigCamera.Render();
            rigCamera.targetTexture = null;

            Destroy(lamp);

            Destroy(fillLamp);

            Destroy(rimLamp);
            rigCamera.farClipPlane = previousFar;
            rigCamera.transform.SetParent(previousParent, false);
            rigCamera.transform.localPosition = Vector3.zero;
            rigCamera.transform.localRotation = Quaternion.identity;
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
