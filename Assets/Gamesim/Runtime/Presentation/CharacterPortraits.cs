using System.Collections.Generic;
using Gamesim.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// Shared appearance portraits. Modular looks are built one at a time in an isolated studio
    /// and captured after their final DNA pass. Authored fallback models use the existing unlit
    /// headshot path. A bounded content-keyed cache retains faces after their actors leave the house.
    /// </summary>
    public static class CharacterPortraits
    {
        /// <summary>Portrait pixels a side (VISUAL-TARGET.md V3): the mockups' headshots, not a thumbnail.</summary>
        public const int Size = 384;
        private const string ModelResourceRoot = "GamesimCharacters/";
        private static readonly Vector3 RigOrigin = new Vector3(0f, -5000f, 0f);

        private static readonly Dictionary<string, RenderTexture> Cache = new Dictionary<string, RenderTexture>();
        private const int MaximumCachedPortraits = 96;
        private static readonly LinkedList<string> CacheOrder = new LinkedList<string>();
        private static readonly Dictionary<string, LinkedListNode<string>> OrderNodes = new Dictionary<string, LinkedListNode<string>>();
        private sealed class TemporaryPortrait { public int Attempts; public double RetryAt; }
        private static readonly Dictionary<string, TemporaryPortrait> Temporary = new Dictionary<string, TemporaryPortrait>();
        private static readonly Dictionary<Material, Material> UnlitCache = new Dictionary<Material, Material>();
        private static GameObject rig;
        private static Camera rigCamera;
        private static CharacterPortraitQueue queue;

        /// <summary>An owned, immutable appearance request. Bindings resolve and hash it once, then poll the cache.</summary>
        public sealed class PortraitRequest
        {
            internal readonly CharacterAppearance Appearance;
            internal readonly string FallbackId;
            public string Key { get; }

            internal PortraitRequest(CharacterAppearance ownedAppearance)
            {
                Appearance = ownedAppearance;
                Key = "appearance:" + ownedAppearance.ContentKey();
                var template = CastTemplates.Find(ownedAppearance.presetId);
                FallbackId = template == null ? ownedAppearance.fallbackId
                    : CharacterPresentation.AppearanceId(CastTemplates.ToContestant(template, false), template.Id);
            }
        }

        /// <summary>A portrait is keyed by its entire saved look, independent of player/NPC identity.</summary>
        public static Texture Get(ContestantState contestant) => Get(Prepare(contestant));

        public static PortraitRequest Prepare(ContestantState contestant)
        {
            if (contestant == null) return null;
            // Cast headshots keep the Everyday look while bodies dress for the current activity.
            // The explicit GetAppearance path still previews the creator's selected outfit.
            var appearance = CharacterOutfits.Resolve(contestant.appearance, CharacterOutfits.Everyday) ?? CharacterAppearance.Preset(
                string.IsNullOrEmpty(contestant.sourceTemplateId) ? contestant.id : contestant.sourceTemplateId);
            appearance.fallbackId = CharacterPresentation.AppearanceId(contestant, contestant.id);
            return new PortraitRequest(appearance);
        }

        /// <summary>The explicit editor path preserves the selected outfit rather than choosing Everyday.</summary>
        public static PortraitRequest PrepareAppearance(CharacterAppearance appearance) =>
            appearance == null ? null : new PortraitRequest(appearance.Clone());

        public static Texture GetAppearance(CharacterAppearance appearance) => Get(PrepareAppearance(appearance));

        public static Texture Get(PortraitRequest request)
        {
            if (request == null) return null;
            string key = request.Key;
            if (TryCached(key, out var cached))
            {
                if (Temporary.TryGetValue(key, out var failed) && Time.realtimeSinceStartupAsDouble >= failed.RetryAt
                    && Application.isPlaying && CharacterBodySource.Provider is IModularCharacterBodyProvider)
                    Enqueue(key, request.Appearance);
                return cached;
            }
            if (!Application.isPlaying || !(CharacterBodySource.Provider is IModularCharacterBodyProvider))
            {
                return Get(request.FallbackId);
            }
            Enqueue(key, request.Appearance);
            return null;
        }

        private static void Enqueue(string key, CharacterAppearance appearance)
        {
            if (queue == null)
            {
                var root = new GameObject("Gamesim Portrait Queue") { hideFlags = HideFlags.DontSave };
                queue = root.AddComponent<CharacterPortraitQueue>();
            }
            queue.Enqueue(key, appearance);
        }

        public static void Bind(RawImage image, ContestantState contestant)
        {
            if (image == null || contestant == null) return;
            var binding = image.GetComponent<CharacterPortraitBinding>() ?? image.gameObject.AddComponent<CharacterPortraitBinding>();
            binding.Set(contestant);
        }

        public static void Bind(Renderer renderer, ContestantState contestant)
        {
            if (renderer == null || contestant == null) return;
            var binding = renderer.GetComponent<CharacterPortraitMaterialBinding>()
                ?? renderer.gameObject.AddComponent<CharacterPortraitMaterialBinding>();
            binding.Set(contestant);
        }

        internal static void StoreAppearance(string key, Texture source, bool temporary = false)
        {
            if (source == null) return;
            bool hadTemporary = Temporary.TryGetValue(key, out var failed);
            if (temporary)
            {
                if (failed == null) { failed = new TemporaryPortrait(); Temporary[key] = failed; }
                failed.Attempts++;
                // Demand-driven retries retain visible placeholders and back off to at most one attempt/minute.
                failed.RetryAt = Time.realtimeSinceStartupAsDouble + Mathf.Min(60f, Mathf.Pow(2f, Mathf.Min(6, failed.Attempts - 1)));
                if (Cache.ContainsKey(key)) return;
            }
            else
            {
                if (Cache.ContainsKey(key) && !hadTemporary) return;
                Temporary.Remove(key);
                if (Cache.TryGetValue(key, out var previous) && previous != null) { previous.Release(); Destroy(previous); }
            }
            var target = new RenderTexture(Size, Size, 0, RenderTextureFormat.ARGB32)
            { name = key, hideFlags = HideFlags.DontSave, filterMode = FilterMode.Bilinear };
            target.Create();
            // The studio image is 4:5. Crop to a square rather than stretching the face.
            Graphics.Blit(source, target, new Vector2(1f, .8f), new Vector2(0f, .1f));
            CachePortrait(key, target);
        }

        private static bool TryCached(string key, out RenderTexture texture)
        {
            if (!Cache.TryGetValue(key, out texture) || texture == null) return false;
            Touch(key);
            return true;
        }

        private static void Touch(string key)
        {
            if (OrderNodes.TryGetValue(key, out var node))
            { CacheOrder.Remove(node); CacheOrder.AddLast(node); }
            else OrderNodes[key] = CacheOrder.AddLast(key);
        }

        private static void CachePortrait(string key, RenderTexture texture)
        {
            Cache[key] = texture; Touch(key);
            while (Cache.Count > MaximumCachedPortraits && CacheOrder.First != null)
            {
                string oldest = CacheOrder.First.Value; CacheOrder.RemoveFirst();
                OrderNodes.Remove(oldest);
                if (!Cache.TryGetValue(oldest, out var retired)) continue;
                Cache.Remove(oldest);
                Temporary.Remove(oldest);
                if (retired != null) { retired.Release(); Destroy(retired); }
            }
        }

        /// <summary>The portrait for a persona, or null when that persona has no authored model.</summary>
        public static Texture Get(string appearanceId)
        {
            if (string.IsNullOrEmpty(appearanceId)) return null;
            if (TryCached(appearanceId, out var cached)) return cached;

            var prefab = Resources.Load<GameObject>(ModelResourceRoot + appearanceId);
            if (prefab == null) return null;

            var texture = Render(prefab);
            CachePortrait(appearanceId, texture);
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
            var presentation = body.GetComponentInParent<CharacterPresentation>();
            if (presentation != null && presentation.AppearanceSnapshot != null)
                return GetAppearance(presentation.AppearanceSnapshot);

            // Namespaced, because the two paths share one cache and the ids collide: a houseguest's
            // contestant id and their appearance id are the same string for this cast, so an early
            // render that fell back to the prefab would poison the live key and the HUD would show
            // authored faces for the rest of the season no matter how many bodies finished.
            string cacheKey = "live:" + contestantId + ":" + (presentation?.AppearanceKey ?? body.GetEntityId().ToString());
            if (TryCached(cacheKey, out var cached)) return cached;

            var skins = body.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skins.Length == 0) return null;

            var extent = skins[0].bounds;
            foreach (var skin in skins) extent.Encapsulate(skin.bounds);
            if (extent.size.y < 0.5f) return null; // still assembling

            var texture = RenderLive(body, extent, contestantId);
            if (texture != null) CachePortrait(cacheKey, texture);
            return texture;
        }

        /// <summary>Drops every cached portrait and tears the rig down. Call on season teardown.</summary>
        public static void Release()
        {
            if (queue != null) Destroy(queue.gameObject);
            queue = null;
            foreach (var texture in Cache.Values)
            {
                if (texture == null) continue;
                texture.Release();
                Destroy(texture);
            }
            Cache.Clear();
            CacheOrder.Clear();
            OrderNodes.Clear();
            Temporary.Clear();

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
