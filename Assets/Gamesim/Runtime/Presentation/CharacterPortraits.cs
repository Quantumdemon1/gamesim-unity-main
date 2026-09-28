using System.Collections.Generic;
using Gamesim.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// Shared appearance portraits. Modular looks are built one at a time in an isolated studio
    /// and captured after their final DNA pass; a look with no portrait yet - or no provider to build
    /// one, which is a clone without UMA - has none, and the HUD draws its own placeholder. There is
    /// no other cast to photograph instead. A bounded content-keyed cache retains faces after their
    /// actors leave the house.
    /// </summary>
    public static class CharacterPortraits
    {
        /// <summary>Portrait pixels a side (VISUAL-TARGET.md V3): the mockups' headshots, not a thumbnail.</summary>
        public const int Size = 384;

        private static readonly Dictionary<string, RenderTexture> Cache = new Dictionary<string, RenderTexture>();
        private const int MaximumCachedPortraits = 96;
        private static readonly LinkedList<string> CacheOrder = new LinkedList<string>();
        private static readonly Dictionary<string, LinkedListNode<string>> OrderNodes = new Dictionary<string, LinkedListNode<string>>();
        private sealed class TemporaryPortrait { public int Attempts; public double RetryAt; }
        private static readonly Dictionary<string, TemporaryPortrait> Temporary = new Dictionary<string, TemporaryPortrait>();
        private static CharacterPortraitQueue queue;

        /// <summary>An owned, immutable appearance request. Bindings resolve and hash it once, then poll the cache.</summary>
        public sealed class PortraitRequest
        {
            internal readonly CharacterAppearance Appearance;
            public string Key { get; }

            internal PortraitRequest(CharacterAppearance ownedAppearance)
            {
                Appearance = ownedAppearance;
                Key = "appearance:" + ownedAppearance.ContentKey();
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
            if (TryCached(key, out var cached)) return cached;
            if (!Application.isPlaying || !(CharacterBodySource.Provider is IModularCharacterBodyProvider)) return null;
            // A look whose build failed waits out its back-off before the studio tries it again.
            if (Temporary.TryGetValue(key, out var failed) && Time.realtimeSinceStartupAsDouble < failed.RetryAt) return null;
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

        /// <summary>
        /// A look the studio could not build: nothing is cached - the HUD keeps its placeholder - and
        /// demand-driven retries back off to at most one attempt a minute.
        /// </summary>
        internal static void MarkFailed(string key)
        {
            if (!Temporary.TryGetValue(key, out var failed)) { failed = new TemporaryPortrait(); Temporary[key] = failed; }
            failed.Attempts++;
            failed.RetryAt = Time.realtimeSinceStartupAsDouble + Mathf.Min(60f, Mathf.Pow(2f, Mathf.Min(6, failed.Attempts - 1)));
        }

        internal static void StoreAppearance(string key, Texture source)
        {
            if (source == null || Cache.ContainsKey(key)) return;
            Temporary.Remove(key);
            var target = new RenderTexture(Size, Size, 0, RenderTextureFormat.ARGB32)
            { name = key, hideFlags = HideFlags.DontSave, filterMode = FilterMode.Bilinear };
            target.Create();
            // The studio image is 4:5. Crop to a square rather than stretching the face.
            // Blit leaves its destination as the active render target, so whatever was active is put
            // back. Otherwise the next ScreenCapture in the frame reads this 384-pixel portrait
            // instead of the screen. The standalone check's first screenshot did, and the season
            // walk stopped on the error (2026-09-27).
            var active = RenderTexture.active;
            Graphics.Blit(source, target, new Vector2(1f, .8f), new Vector2(0f, .1f));
            RenderTexture.active = active;
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

        /// <summary>Drops every cached portrait and the studio queue. Call on season teardown.</summary>
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
        }

        private static void Destroy(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }
    }
}
