using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Gamesim.Tests.PlayMode
{
    public sealed class CharacterStudioRecoveryPlayModeTests
    {
        private CharacterStudioPreview preview;
        private DeferredProvider provider;

        // Nested coroutine failures can bypass a test body's finally; teardown owns these exact roots.
        private readonly List<GameObject> creatorThumbnailOwners = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            provider = new DeferredProvider();
            CharacterBodySource.Register(provider);
            preview = CharacterStudioPreview.Create("Recovery test studio");
            preview.BuildTimeoutSeconds = .1f;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var owner in creatorThumbnailOwners)
            {
                if (owner == null) continue;
                owner.SetActive(false);
                Object.Destroy(owner);
            }
            creatorThumbnailOwners.Clear();
            CharacterBodySource.Unregister(provider);
            CharacterPortraits.Release();
            if (preview != null) Object.Destroy(preview.gameObject);
            yield return null;
        }

        /// <summary>
        /// A look the studio fails to build is no portrait at all - never somebody else's face, which
        /// is what the failed look used to be given - waits out a back-off before it is tried again,
        /// does not hold up the rest of the cast, and is built after all once the studio can.
        /// </summary>
        [UnityTest]
        public IEnumerator AFailedPortraitIsNoFaceAndRecoversWithoutStarvingTheCast()
        {
            CharacterPortraits.Release();
            provider.ThrowAfterAllocation = true;
            var broken = CharacterPortraits.PrepareAppearance(CharacterAppearance.Preset("player"));
            Assert.That(CharacterPortraits.Get(broken), Is.Null, "Nothing to show until the studio has built it.");
            yield return Until(() => provider.Roots.Count == 1 && provider.Roots[0] == null);
            for (int i = 0; i < 30; i++)
            {
                Assert.That(CharacterPortraits.Get(broken), Is.Null, "A failed build is no face, and nothing stands in for it.");
                yield return null;
            }
            Assert.That(provider.Roots.Count, Is.EqualTo(1), "The failed look waits out its back-off, however often it is asked for.");
            provider.ThrowAfterAllocation = false;
            provider.CompleteImmediately = true;
            var another = CharacterPortraits.PrepareAppearance(CharacterAppearance.Preset("emma-brown"));
            Texture healthy = null;
            yield return Until(() => (healthy = CharacterPortraits.Get(another)) != null);
            Assert.That(healthy, Is.Not.Null, "An earlier failed portrait cannot block the remaining cast.");
            Texture recovered = null;
            yield return Until(() => (recovered = CharacterPortraits.Get(broken)) != null);
            Assert.That(recovered, Is.Not.Null, "and after its back-off the failed look is built after all.");
        }

        [UnityTest]
        public IEnumerator PreparedPortraitsOwnTheirSnapshotAndSurviveCacheInvalidation()
        {
            CharacterPortraits.Release();
            provider.CompleteImmediately = true;
            var contestant = ContentCatalog.Create(17).Find(ContentCatalog.PlayerId);
            contestant.appearance = CharacterAppearance.Preset("player");
            contestant.appearance.outfits.Add(new CharacterOutfit { id = "Everyday" });
            contestant.appearance.outfits.Add(new CharacterOutfit { id = "Formal" });
            contestant.appearance.activeOutfit = "Formal";
            AppearanceEditing.SetColor(contestant.appearance, "Hair", new Color(.2f, .3f, .4f));
            var prepared = CharacterPortraits.Prepare(contestant);
            var explicitOutfit = CharacterPortraits.PrepareAppearance(contestant.appearance);
            string key = prepared.Key;
            Assert.That(explicitOutfit.Key, Is.Not.EqualTo(prepared.Key), "Gameplay headshots use Everyday; explicit previews retain Formal.");
            contestant.appearance.colors[0].r = .9f;
            contestant.appearance.activeOutfit = "Everyday";
            Texture first = null;
            yield return Until(() => (first = CharacterPortraits.Get(prepared)) != null);
            Assert.That(provider.Last.Appearance.activeOutfit, Is.EqualTo("Everyday"));
            Assert.That(provider.Last.Appearance.colors.Single(value => value.id == "Hair").r, Is.EqualTo(.2f));
            Assert.That(prepared.Key, Is.SameAs(key), "Polling reuses the prepared key.");
            Assert.That(CharacterPortraits.Prepare(contestant).Key, Is.Not.EqualTo(key), "A new binding sees subsequent edits.");
            for (int i = 0; i < 100; i++) Assert.That(CharacterPortraits.Get(prepared), Is.SameAs(first));
            int builtBeforeRelease = provider.Roots.Count;
            CharacterPortraits.Release();
            yield return null;
            Texture rebuilt = null;
            yield return Until(() => (rebuilt = CharacterPortraits.Get(prepared)) != null);
            Assert.That(provider.Roots.Count, Is.GreaterThan(builtBeforeRelease));
            Assert.That(first == null, Is.True);
            Assert.That(rebuilt, Is.Not.Null);
            yield return Until(() => CharacterPortraits.Get(explicitOutfit) != null);
            Assert.That(provider.Last.Appearance.activeOutfit, Is.EqualTo("Formal"));
            Assert.That(provider.Last.Appearance.colors.Single(value => value.id == "Hair").r, Is.EqualTo(.2f));
        }

        [UnityTest]
        public IEnumerator VisibleBindingReacquiresItsPortraitAfterLruEvictionWithoutChangingTint()
        {
            CharacterPortraits.Release();
            provider.CompleteImmediately = true;
            var view = new GameObject("Evicted portrait binding", typeof(RectTransform), typeof(RawImage));
            try
            {
                var image = view.GetComponent<RawImage>();
                var tint = new Color(.3f, .3f, .3f, .8f);
                image.color = tint;
                CharacterPortraits.Bind(image, ContentCatalog.Create(3).Find(ContentCatalog.PlayerId));
                yield return Until(() => image.texture != null);
                var original = image.texture;
                // Seed completed captures through the cache's insertion path; building 97 unrelated avatars
                // would add delay without improving the eviction/binding behavior this test exercises.
                var store = typeof(CharacterPortraits).GetMethod("StoreAppearance", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(store, Is.Not.Null);
                for (int i = 0; i < 97; i++)
                    store.Invoke(null, new object[] { "eviction-fixture-" + i, Texture2D.whiteTexture });
                yield return null;
                Assert.That(original == null, Is.True, "The least recently used texture must be released.");
                yield return Until(() => image.texture != null && image.texture != original);
                Assert.That(image.color, Is.EqualTo(tint));
                Assert.That(image.enabled, Is.True);
            }
            finally { Object.Destroy(view); }
        }

        /// <summary>
        /// Storing a portrait leaves the active render target as it found it. Graphics.Blit leaves
        /// its destination active, and a ScreenCapture later in the same frame then read the 384-pixel
        /// portrait instead of the screen. The standalone check's season walk stopped on exactly that.
        /// </summary>
        [Test]
        public void StoringAPortraitLeavesTheActiveRenderTargetAlone()
        {
            var store = typeof(CharacterPortraits).GetMethod("StoreAppearance", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(store, Is.Not.Null);
            var elsewhere = new RenderTexture(64, 64, 0);
            try
            {
                RenderTexture.active = null;
                store.Invoke(null, new object[] { "active-target-fixture-screen", Texture2D.whiteTexture });
                Assert.That(RenderTexture.active == null, Is.True, "The screen is still the target after a portrait is stored,");
                RenderTexture.active = elsewhere;
                store.Invoke(null, new object[] { "active-target-fixture-other", Texture2D.whiteTexture });
                Assert.That(RenderTexture.active == elsewhere, Is.True, "and so is any other target that was active.");
            }
            finally
            {
                RenderTexture.active = null;
                Object.Destroy(elsewhere);
            }
        }

        [UnityTest]
        public IEnumerator TimedOutBuildUsesFallbackAndRetryRecoversTheSameSavedAppearance()
        {
            var appearance = CharacterAppearance.Preset("emma-brown");
            string key = appearance.ContentKey();
            preview.Show(appearance);
            yield return Until(() => preview.CanRetry && !preview.IsBuilding);
            Assert.That(preview.Status, Does.Contain("too long"));
            Assert.That(preview.CompletedKey, Is.EqualTo(key));
            Assert.That(provider.Roots[0] == null, Is.True, "Timed-out revision must release its entire body.");
            string failure = preview.LastFailureDiagnostic;
            Assert.That(failure, Does.Contain("; body=" + provider.LastRootEntityId + ";"),
                "The diagnostic retains the original body ID after its deferred destruction.");
            Assert.That(failure, Does.Contain("; revision=" + provider.Last.Revision + ";"));
            Assert.That(failure, Does.Contain("; stateRevision=" + provider.Last.Revision + ";"));
            Assert.That(failure, Does.Contain("; bodyActive=True;"));
            Assert.That(failure, Does.Contain("; stateReady=False;"),
                "The fallback has a renderer but no build state; these fields describe the original pending body.");
            Assert.That(failure, Does.Contain("; renderers=1;"));
            Assert.That(failure, Does.StartWith("frame="));
            Assert.That(failure, Does.Contain("; buildElapsedMs="));
            preview.Show(appearance);
            Assert.That(preview.LastFailureDiagnostic, Is.SameAs(failure),
                "Polling an unchanged request must not erase its failure evidence.");
            provider.CompleteImmediately = true;
            preview.Retry();
            Assert.That(preview.LastFailureDiagnostic, Is.Null, "Retry begins a fresh request with no stale failure.");
            yield return Until(() => !preview.IsBuilding);
            Assert.That(preview.LastFailureDiagnostic, Is.Null, "A successful retry cannot inherit the previous failure.");
            Assert.That(provider.Roots.Count, Is.EqualTo(2));
            Assert.That(preview.CanRetry, Is.False);
            Assert.That(preview.Status, Is.EqualTo("Preview ready"));
            Assert.That(preview.CompletedKey, Is.EqualTo(key));
            Assert.That(appearance.ContentKey(), Is.EqualTo(key), "Recovery cannot overwrite saved customizations.");
        }

        [UnityTest]
        public IEnumerator ProviderExceptionReleasesPartiallyAllocatedChildrenAndOffersRetry()
        {
            provider.ThrowAfterAllocation = true;
            preview.Show(CharacterAppearance.Preset("player"));
            yield return Until(() => preview.CanRetry && !preview.IsBuilding);
            Assert.That(preview.Status, Does.Contain("could not be built"));
            Assert.That(provider.Roots[0] == null, Is.True);
            Assert.That(preview.GetComponentsInChildren<Renderer>(), Is.Not.Empty, "Recovery shows a plain placeholder.");
            Assert.That(preview.LastFailureDiagnostic, Does.Contain("; body=0;"),
                "An exception before the provider returns a subject cannot claim the fallback was the original body.");
            Assert.That(preview.LastFailureDiagnostic, Does.Contain("; stateReady=missing;"));
        }

        [UnityTest]
        public IEnumerator AReadyFlagWithoutRenderersAlsoTimesOut()
        {
            provider.NoRenderer = true;
            provider.CompleteImmediately = true;
            preview.Show(CharacterAppearance.Preset("player"));
            yield return Until(() => preview.CanRetry && !preview.IsBuilding);
            Assert.That(preview.Status, Does.Contain("too long"));
            Assert.That(provider.Roots[0] == null, Is.True);
            Assert.That(preview.LastFailureDiagnostic, Does.Contain("; body=" + provider.LastRootEntityId + ";"));
            Assert.That(preview.LastFailureDiagnostic, Does.Contain("; stateReady=True;"),
                "A ready flag without geometry was already true on the original, not invented by fallback.");
            Assert.That(preview.LastFailureDiagnostic, Does.Contain("; renderers=0;"));
            preview.Show(CharacterAppearance.Preset("emma-brown"));
            Assert.That(preview.LastFailureDiagnostic, Is.Null, "A genuinely different appearance clears the old failure.");
        }

        [UnityTest]
        public IEnumerator NewestRevisionWinsAndDisablingCancelsPendingWork()
        {
            preview.BuildTimeoutSeconds = 3f;
            var oldAppearance = CharacterAppearance.Preset("player");
            preview.Show(oldAppearance);
            yield return Until(() => provider.Roots.Count == 1);
            var oldBody = provider.Roots[0];
            var changed = oldAppearance.Clone();
            AppearanceEditing.SetValue(changed, "height", .29f);
            provider.CompleteImmediately = true;
            preview.Show(changed);
            // A late completion from the previous request cannot publish its pixels as the new revision.
            oldBody.GetComponent<CharacterBodyBuildState>().Ready = true;
            yield return Until(() => !preview.IsBuilding);
            Assert.That(oldBody == null, Is.True);
            Assert.That(preview.CompletedKey, Is.EqualTo(changed.ContentKey()));
            provider.CompleteImmediately = false;
            AppearanceEditing.SetValue(changed, "height", .31f);
            preview.Show(changed);
            yield return Until(() => provider.Roots.Count == 3);
            var pendingBody = provider.Roots[2];
            preview.gameObject.SetActive(false);
            yield return null;
            Assert.That(preview.IsBuilding, Is.False);
            Assert.That(pendingBody == null, Is.True);
            provider.CompleteImmediately = true;
            preview.gameObject.SetActive(true);
            yield return Until(() => !preview.IsBuilding);
            Assert.That(preview.CompletedKey, Is.EqualTo(changed.ContentKey()));
            var texture = preview.Texture as RenderTexture;
            Object.Destroy(preview.gameObject);
            yield return null;
            Assert.That(texture == null || !texture.IsCreated(), Is.True, "Closing releases the render target.");
        }

        [UnityTest]
        public IEnumerator CreatorViewsRetainPublishedPixelsUntilTheLatestPoseIsRendered()
        {
            UseSettledCreator();
            var appearance = CharacterAppearance.Preset("emma-brown");
            preview.Show(appearance);
            yield return Until(() => !preview.IsBuilding);
            var target = preview.Texture;
            var before = ReadStudioPixels();
            var camera = preview.GetComponentInChildren<Camera>();
            var position = camera.transform.position;
            int publications = 0;
            preview.StatusChanged += () => { if (!preview.IsBuilding && !preview.CanRetry) publications++; };

            preview.View(90f); preview.Zoom(.1f); preview.FocusFace(true); preview.FocusFace(false); preview.View(0f); preview.View(90f);

            Assert.That(preview.IsBuilding, Is.True, "A requested view cannot advertise the old image as ready.");
            Assert.That(preview.CompletedKey, Is.EqualTo(appearance.ContentKey()), "The retained image keeps its published key.");
            Assert.That(preview.Texture, Is.SameAs(target));
            Assert.That(camera.transform.position, Is.EqualTo(position));
            CollectionAssert.AreEqual(before, ReadStudioPixels(), "Pending requests retain the actual published RT pixels.");
            yield return null;
            Assert.That(preview.IsBuilding, Is.True, "An ordinary Update applies the pose before later publication.");
            provider.Roots.Last().transform.localScale = new Vector3(1f, 1.25f, 1f);
            preview.Zoom(-.1f); // A newer size request coalesces with the pending view and its normal pose updates.
            yield return Until(() => !preview.IsBuilding);

            Assert.That(preview.CanRetry, Is.False);
            Assert.That(preview.Turn, Is.EqualTo(90f));
            Assert.That(publications, Is.EqualTo(1), "Only the final requested view is published.");
            AssertRendererInFrame(camera, provider.Roots.Last().GetComponent<Renderer>());
            var published = ReadStudioPixels(); position = camera.transform.position;
            provider.Roots.Last().transform.localScale = new Vector3(1f, 1.5f, 1f);
            yield return null; yield return null; yield return null;
            Assert.That(camera.transform.position, Is.EqualTo(position), "Settled previews do not add an idle fitting/render loop.");
            CollectionAssert.AreEqual(published, ReadStudioPixels());
            Assert.That(publications, Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator CreatorQueuedViewsCannotPublishASupersededAppearance()
        {
            UseSettledCreator();
            var appearance = CharacterAppearance.Preset("emma-brown");
            preview.Show(appearance);
            yield return Until(() => !preview.IsBuilding);
            var oldBody = provider.Roots.Last();
            var published = ReadStudioPixels();
            int publications = 0;
            preview.StatusChanged += () => { if (!preview.IsBuilding && !preview.CanRetry) publications++; };
            preview.View(90f);
            Assert.That(preview.IsBuilding, Is.True);
            var changed = appearance.Clone();
            AppearanceEditing.SetValue(changed, "height", .29f);
            preview.Show(changed); preview.View(0f);
            oldBody.GetComponent<CharacterBodyBuildState>().Ready = true;
            yield return Until(() => provider.Roots.Count == 2);
            Assert.That(preview.IsBuilding, Is.True, "The replacement body still needs its own final image.");
            Assert.That(preview.CompletedKey, Is.EqualTo(appearance.ContentKey()));
            CollectionAssert.AreEqual(published, ReadStudioPixels());
            yield return Until(() => !preview.IsBuilding);
            Assert.That(oldBody == null, Is.True);
            Assert.That(preview.CompletedKey, Is.EqualTo(changed.ContentKey()));
            Assert.That(preview.Turn, Is.EqualTo(0f));
            Assert.That(publications, Is.EqualTo(1), "The superseded queued view cannot publish between revisions.");
            Assert.That(provider.Last.Appearance.ContentKey(), Is.EqualTo(changed.ContentKey()));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator CreatorPendingPublicationCancelsOnDisableAndDisposal()
        {
            UseSettledCreator();
            var appearance = CharacterAppearance.Preset("emma-brown");
            preview.Show(appearance);
            yield return Until(() => !preview.IsBuilding);
            var target = (RenderTexture)preview.Texture;
            var oldBody = provider.Roots.Last();
            var published = ReadStudioPixels();
            int publications = 0;
            preview.StatusChanged += () => { if (!preview.IsBuilding && !preview.CanRetry) publications++; };
            preview.View(90f);
            Assert.That(preview.IsBuilding, Is.True);
            preview.gameObject.SetActive(false);
            yield return null; yield return null; yield return null;
            Assert.That(oldBody == null, Is.True);
            Assert.That(preview.IsBuilding, Is.False);
            Assert.That(preview.Texture, Is.SameAs(target));
            Assert.That(target.IsCreated(), Is.True);
            Assert.That(preview.CompletedKey, Is.EqualTo(appearance.ContentKey()));
            CollectionAssert.AreEqual(published, ReadStudioPixels());
            Assert.That(publications, Is.Zero, "Disabled work cannot publish a queued view.");
            preview.gameObject.SetActive(true);
            yield return Until(() => !preview.IsBuilding);
            Assert.That(preview.CompletedKey, Is.EqualTo(appearance.ContentKey()));
            Assert.That(preview.Turn, Is.EqualTo(90f));
            Assert.That(publications, Is.EqualTo(1));
            var currentBody = provider.Roots.Last();
            preview.View(0f);
            Assert.That(preview.IsBuilding, Is.True);
            Object.Destroy(preview.gameObject);
            yield return null;
            Assert.That(currentBody == null && (target == null || !target.IsCreated()), Is.True);
            Assert.That(publications, Is.EqualTo(1), "Disposal cancels the last pending view and releases owned resources.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator CreatorThumbnailShowsItsPlaceholderUntilTheMatchingPortraitIsReady()
        {
            CharacterPortraits.Release();
            var owner = new GameObject("Creator portrait placeholder fixture");
            creatorThumbnailOwners.Add(owner);
            try
            {
                var creator = CharacterCreator.Attach(owner);
                // Keep the actual thumbnail/binding active without running the unrelated full-page controller.
                creator.enabled = false;
                creator.GetComponent<CanvasGroup>().alpha = 1f;
                var contestant = ContentCatalog.Create(17).Find(ContentCatalog.PlayerId);
                contestant.appearance = CharacterAppearance.Preset("player");
                var expected = CharacterPortraits.Prepare(contestant);
                int clicks = 0;
                var button = CreatorPortraitThumbnail(creator, contestant, () => clicks++);
                var image = button.transform.Find("Picture/Face").GetComponent<UnityEngine.UI.RawImage>();
                var placeholder = button.transform.Find("Picture/Portrait placeholder").GetComponent<UnityEngine.UI.Image>();
                var tint = new Color(.3f, .3f, .3f, .8f);
                image.color = tint;

                Assert.That(placeholder.sprite, Is.Not.Null, "The actual houseguest sprite must be available.");
                Assert.That(placeholder.sprite, Is.SameAs(UiTheme.Icon("houseguest")));
                Assert.That(placeholder.enabled && placeholder.gameObject.activeInHierarchy, Is.True);
                Assert.That(placeholder.color.a, Is.GreaterThan(0f));
                Assert.That(placeholder.rectTransform.rect.width, Is.GreaterThan(0f));
                Assert.That(placeholder.rectTransform.rect.height, Is.GreaterThan(0f));
                Assert.That(placeholder.raycastTarget, Is.False);
                Assert.That(image.enabled, Is.False);
                Assert.That(image.texture, Is.Null);
                button.onClick.Invoke();
                Assert.That(clicks, Is.EqualTo(1), "The placeholder cannot replace the thumbnail's action.");
                yield return Until(() => provider.Roots.Count == 1);
                Assert.That(provider.Roots[0].GetComponent<CharacterBodyBuildState>().Ready, Is.False);
                Assert.That(placeholder.enabled, Is.True, "The icon remains while the real portrait build is pending.");
                Assert.That(image.enabled, Is.False);
                provider.Roots[0].GetComponent<CharacterBodyBuildState>().Ready = true;
                yield return Until(() => image.enabled);

                Assert.That(image.texture, Is.SameAs(CharacterPortraits.Get(expected)));
                Assert.That(image.texture.name, Is.EqualTo(expected.Key));
                Assert.That(((RenderTexture)image.texture).IsCreated(), Is.True);
                Assert.That(placeholder.enabled, Is.False, "No silhouette may show through a ready portrait's transparent pixels.");
                Assert.That(image.color, Is.EqualTo(tint));
                LogAssert.NoUnexpectedReceived();
            }
            finally { if (owner != null) owner.SetActive(false); Object.Destroy(owner); }
        }

        [UnityTest]
        public IEnumerator CreatorThumbnailRestoresItsPlaceholderAfterInvalidationAndRebinding()
        {
            CharacterPortraits.Release();
            provider.CompleteImmediately = true;
            var owner = new GameObject("Creator portrait recovery fixture");
            creatorThumbnailOwners.Add(owner);
            try
            {
                var creator = CharacterCreator.Attach(owner);
                // Keep the actual thumbnail/binding active without running the unrelated full-page controller.
                creator.enabled = false;
                creator.GetComponent<CanvasGroup>().alpha = 1f;
                var contestant = ContentCatalog.Create(17).Find(ContentCatalog.PlayerId);
                contestant.appearance = CharacterAppearance.Preset("player");
                var originalRequest = CharacterPortraits.Prepare(contestant);
                var button = CreatorPortraitThumbnail(creator, contestant, null);
                var image = button.transform.Find("Picture/Face").GetComponent<UnityEngine.UI.RawImage>();
                var placeholder = button.transform.Find("Picture/Portrait placeholder").GetComponent<UnityEngine.UI.Image>();
                var tint = new Color(.3f, .3f, .3f, .8f);
                image.color = tint;
                yield return Until(() => image.enabled);
                var original = image.texture;
                Assert.That(placeholder.enabled, Is.False);

                provider.CompleteImmediately = false;
                CharacterPortraits.Release();
                yield return Until(() => placeholder.enabled);
                Assert.That(original == null, Is.True, "Invalidation releases the old cached texture.");
                Assert.That(image.enabled, Is.False);
                yield return Until(() => provider.Roots.Count == 2);
                Assert.That(provider.Roots.Last().GetComponent<CharacterBodyBuildState>().Ready, Is.False);
                Assert.That(placeholder.enabled, Is.True);
                provider.Roots.Last().GetComponent<CharacterBodyBuildState>().Ready = true;
                yield return Until(() => image.enabled);
                var restored = image.texture;
                Assert.That(restored, Is.SameAs(CharacterPortraits.Get(originalRequest)));
                Assert.That(placeholder.enabled, Is.False);
                Assert.That(image.color, Is.EqualTo(tint));

                contestant.appearance = CharacterAppearance.Preset("emma-brown");
                var changedRequest = CharacterPortraits.Prepare(contestant);
                Assert.That(changedRequest.Key, Is.Not.EqualTo(originalRequest.Key));
                CharacterPortraits.Bind(image, contestant);
                Assert.That(image.texture, Is.Null, "The previous look cannot stand in for the newly bound key.");
                Assert.That(image.enabled, Is.False);
                Assert.That(placeholder.enabled, Is.True, "A new pending look restores the icon synchronously.");
                Assert.That(CharacterPortraits.Get(originalRequest), Is.SameAs(restored));
                yield return Until(() => provider.Roots.Count == 3);
                Assert.That(provider.Roots.Last().GetComponent<CharacterBodyBuildState>().Ready, Is.False);
                Assert.That(placeholder.enabled, Is.True);
                provider.Roots.Last().GetComponent<CharacterBodyBuildState>().Ready = true;
                yield return Until(() => image.enabled);
                Assert.That(image.texture, Is.SameAs(CharacterPortraits.Get(changedRequest)));
                Assert.That(image.texture, Is.Not.SameAs(restored));
                Assert.That(image.texture.name, Is.EqualTo(changedRequest.Key));
                Assert.That(placeholder.enabled, Is.False);
                Assert.That(image.color, Is.EqualTo(tint));
                LogAssert.NoUnexpectedReceived();
            }
            finally { if (owner != null) owner.SetActive(false); Object.Destroy(owner); }
        }

        private static UnityEngine.UI.Button CreatorPortraitThumbnail(CharacterCreator creator, ContestantState contestant, Action click)
        {
            var thumbnail = typeof(CharacterCreator).GetMethod("Thumbnail", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(thumbnail, Is.Not.Null);
            return (UnityEngine.UI.Button)thumbnail.Invoke(creator, new object[]
            {
                creator.transform, "Starting look You", "You", new Rect(0f, 0f, 86f, 104f), false,
                null, null, "houseguest", click, contestant
            });
        }

        private void UseSettledCreator()
        {
            Object.Destroy(preview.gameObject);
            preview = CharacterStudioPreview.Create("Creator publication fixture", fitDeformedBody: true, settleRenders: true);
            preview.BuildTimeoutSeconds = 3f;
            provider.CompleteImmediately = true;
        }

        private Color32[] ReadStudioPixels()
        {
            var target = (RenderTexture)preview.Texture;
            var prior = RenderTexture.active;
            var resolved = RenderTexture.GetTemporary(target.width, target.height, 0, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(target, resolved); RenderTexture.active = resolved;
                pixels.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0); pixels.Apply();
                return pixels.GetPixels32();
            }
            finally { RenderTexture.active = prior; RenderTexture.ReleaseTemporary(resolved); Object.DestroyImmediate(pixels); }
        }

        private static void AssertRendererInFrame(Camera camera, Renderer renderer)
        {
            // The provider capsule's ordinary world bounds are an independent projection oracle.
            var bounds = renderer.bounds;
            for (int i = 0; i < 8; i++)
            {
                var point = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                var projected = camera.WorldToViewportPoint(point);
                Assert.That(projected.z, Is.InRange(camera.nearClipPlane, camera.farClipPlane));
                Assert.That(projected.x, Is.InRange(.02f, .98f));
                Assert.That(projected.y, Is.InRange(.02f, .98f));
            }
        }

        private static IEnumerator Until(Func<bool> condition)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 4;
            while (!condition() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(condition(), Is.True, "Bounded preview operation did not complete.");
            yield return null; // Deferred Destroy has now completed too.
        }

        private sealed class DeferredProvider : IModularCharacterBodyProvider
        {
            public bool CompleteImmediately, ThrowAfterAllocation, NoRenderer;
            public CharacterBodyRequest Last;
            public string LastRootEntityId;
            public readonly List<GameObject> Roots = new List<GameObject>();
            public ICharacterAppearanceCatalog Catalog => null;
            public bool TryCreate(in CharacterBodyRequest request, Transform parent, Color color, out CharacterBody body)
            {
                Last = request;
                var root = NoRenderer ? new GameObject("Empty pending body") : GameObject.CreatePrimitive(PrimitiveType.Capsule);
                root.transform.SetParent(parent, false);
                Roots.Add(root);
                LastRootEntityId = root.GetEntityId().ToString();
                if (ThrowAfterAllocation) throw new InvalidOperationException("Test provider failed after allocation");
                var state = root.AddComponent<CharacterBodyBuildState>();
                state.Revision = request.Revision; state.Ready = CompleteImmediately;
                body = new CharacterBody(root, null, true);
                return true;
            }
            public bool TryCreate(string id, Transform parent, Color color, out CharacterBody body)
            { body = default; throw new InvalidOperationException("Explicit appearance request required."); }
            public void SetWardrobeColor(in CharacterBody body, Color color) { }
        }
    }
}
