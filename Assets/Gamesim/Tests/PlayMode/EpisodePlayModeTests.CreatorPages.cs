using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The creator's pages as the mockups lay them out: every control inside the frame and clear
    /// of the footer, at standard and larger text, and a colour swatch writing the colour it shows.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private static readonly string[] CreatorPages = { "Appearance", "Identity", "Personality", "My Houseguests", "Review" };
        private static readonly string[] CreatorCategories = { "Body", "Face", "Hair", "Clothing", "Accessories", "Colors" };

        [UnityTest]
        public IEnumerator Creator_EveryPageFitsTheFrameAndIsCapturedForReview()
        {
            yield return OpenCreator();
            var creator = Creator();
            // A saved houseguest, so My Houseguests has a card and its bar to lay out.
            var saved = creator.Draft.Copy();
            saved.Name = "Review Guest";
            Assert.That(creator.ProfileStore.Save(CharacterProfile.FromDraft(System.Guid.NewGuid().ToString("N"), saved), out var error), Is.True, error);
            foreach (float scale in new[] { 1f, 1.2f })
            {
                creator.FontScale = scale;
                yield return null; yield return null;
                foreach (string page in CreatorPages)
                {
                    CastButtons(page)[0].onClick.Invoke();
                    yield return null;
                    if (page == "Appearance")
                        foreach (string category in CreatorCategories)
                        {
                            CastButtons(category)[0].onClick.Invoke();
                            yield return null;
                            AssertCreatorFits(creator, page + " / " + category + " at " + scale);
                            if (scale == 1f && Application.isBatchMode) yield return CaptureCreatorFraming("creator-appearance-" + category.ToLowerInvariant());
                        }
                    else
                    {
                        AssertCreatorFits(creator, page + " at " + scale);
                        if (scale == 1f && Application.isBatchMode)
                            yield return CaptureCreatorFraming("creator-" + page.ToLowerInvariant().Replace(' ', '-'),
                                expectPreview: page == "Identity" || page == "Review");
                    }
                }
            }
            creator.FontScale = 1f;
        }

        private IEnumerator CaptureCreatorFraming(string name, bool expectPreview = true, int width = 1600, int height = 900,
            System.Action arrange = null)
        {
            yield return CaptureFraming(name, width: width, height: height, arrange: arrange,
                prepare: expectPreview ? () => PrepareCreatorFrame(name) : (System.Func<IEnumerator>)null,
                inspect: expectPreview ? frame => AssertCreatorFrameReady(name) : (System.Action<Texture2D>)null);
        }

        private IEnumerator PrepareCreatorFrame(string name)
        {
            float deadline = Time.realtimeSinceStartup + 25f;
            while (Time.realtimeSinceStartup < deadline)
            {
                var creator = Creator();
                var preview = creator.StudioPreview;
                if (creator.IsShowing && preview != null && preview.gameObject.activeInHierarchy && !preview.IsBuilding
                    && preview.CompletedKey == creator.Draft.Appearance.ContentKey()) break;
                yield return null;
            }
            // The preview's completion and the status text update occur in separate Update calls.
            yield return null;
            AssertCreatorFrameReady(name);
        }

        private void AssertCreatorFrameReady(string name)
        {
            var creator = Creator();
            var preview = creator.StudioPreview;
            Assert.That(creator.IsShowing && preview != null && preview.gameObject.activeInHierarchy && !preview.IsBuilding,
                Is.True, name + ": the displayed preview must complete before the frame is read.");
            Assert.That(preview.CompletedKey, Is.EqualTo(creator.Draft.Appearance.ContentKey()), name + ": capture the current draft.");
            if (CharacterBodySource.Provider != null)
                Assert.That(preview.CanRetry, Is.False, name + ": installed content must render its own avatar, rather than a fallback.");
            var image = creator.GetComponentsInChildren<RawImage>().Single(item => item.name == "Live character preview");
            var texture = preview.Texture as RenderTexture;
            Assert.That(image.isActiveAndEnabled && !image.canvasRenderer.cull && image.color.a >= .5f && image.texture == texture
                && texture != null && texture.IsCreated(), Is.True, name + ": the actual visible image must bind the ready studio.");
            var previous = RenderTexture.active;
            var resolved = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(texture, resolved); RenderTexture.active = resolved;
                pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); pixels.Apply();
                var colors = pixels.GetPixels32();
                var backdrop = colors[0];
                int foreground = 0, minX = texture.width, minY = texture.height, maxX = -1, maxY = -1;
                for (int pixel = 0; pixel < colors.Length; pixel += 16)
                {
                    var color = colors[pixel];
                    if (color.a < 128 || (System.Math.Abs(color.r - backdrop.r) < 8 && System.Math.Abs(color.g - backdrop.g) < 8
                        && System.Math.Abs(color.b - backdrop.b) < 8)) continue;
                    foreground++;
                    int x = pixel % texture.width, y = pixel / texture.width;
                    minX = System.Math.Min(minX, x); maxX = System.Math.Max(maxX, x);
                    minY = System.Math.Min(minY, y); maxY = System.Math.Max(maxY, y);
                }
                Assert.That(foreground, Is.GreaterThanOrEqualTo(128), name + ": a uniform clear is not an avatar.");
                Assert.That(maxX - minX + 1, Is.GreaterThanOrEqualTo(8), name + ": foreground has visible width.");
                Assert.That(maxY - minY + 1, Is.GreaterThanOrEqualTo(16), name + ": foreground has visible height.");
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(resolved); Object.Destroy(pixels); }
        }

        /// <summary>
        /// Every control is inside the creator's frame and none but the footer's own sits on the
        /// footer. A control in a scrolling list is held to the list's width instead: its height is
        /// the list's to scroll.
        /// </summary>
        private static void AssertCreatorFits(CharacterCreator creator, string where)
        {
            Canvas.ForceUpdateCanvases();
            var root = (RectTransform)creator.transform;
            var frame = root.rect;
            var footer = creator.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Fixed footer");
            var footerBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(root, footer);
            var controls = creator.GetComponentsInChildren<Selectable>().Where(control => control.IsActive()).ToArray();
            Assert.That(controls, Is.Not.Empty, where);
            var problems = new List<string>();
            foreach (var control in controls)
            {
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(root, control.transform);
                var scroll = control.GetComponentInParent<ScrollRect>();
                if (scroll != null && scroll.gameObject != control.gameObject && control.transform.IsChildOf(scroll.content))
                {
                    var viewport = RectTransformUtility.CalculateRelativeRectTransformBounds(root, scroll.viewport);
                    if (bounds.min.x < viewport.min.x - 1f || bounds.max.x > viewport.max.x + 1f)
                        problems.Add(control.name + " is wider than its list");
                    continue;
                }
                if (bounds.min.x < frame.xMin - 1f || bounds.max.x > frame.xMax + 1f || bounds.min.y < frame.yMin - 1f || bounds.max.y > frame.yMax + 1f)
                    problems.Add(control.name + " is outside the frame");
                if (!control.transform.IsChildOf(footer) && bounds.min.y < footerBounds.max.y - 1f)
                    problems.Add(control.name + " is on the footer");
            }
            Assert.That(problems, Is.Empty, where);
        }

#if GAMESIM_UMA
        // UMA only: these pages dress a modular body, and only UMA builds one.
        [UnityTest]
        public IEnumerator Creator_ColourSwatchesWriteTheColourTheyShow()
        {
            yield return OpenCreator();
            var creator = Creator();
            Assert.That(CharacterBodySource.Provider, Is.InstanceOf<IModularCharacterBodyProvider>(),
                "The garment colours are the modular bodies' - the harness's cast is.");

            CastButtons("Clothing")[0].onClick.Invoke();
            yield return null;
            var swatch = CastButtons("Fabric Chest 4").Single();
            var shown = swatch.GetComponent<Image>().color;
            swatch.onClick.Invoke();
            yield return null;
            Assert.That(AppearanceEditing.TryFabric(AppearanceEditing.Outfit(creator.Draft.Appearance), "Chest", out var worn), Is.True,
                "The top's colour is saved with the outfit.");
            AssertSameColour(worn, shown, "top");
            Assert.That(AppearanceEditing.TryFabric(AppearanceEditing.Outfit(creator.Draft.Appearance), "Legs", out _), Is.False,
                "Only the garment picked for changes.");
            Assert.That(CastButtons("Fabric Chest 4").Single().transform.Find("Chosen ring"), Is.Not.Null, "The worn colour is ringed.");
            CastButtons("Original top color").Single().onClick.Invoke();
            yield return null;
            Assert.That(AppearanceEditing.TryFabric(AppearanceEditing.Outfit(creator.Draft.Appearance), "Chest", out _), Is.False,
                "The garment's own colour comes back.");

            CastButtons("Colors")[0].onClick.Invoke();
            yield return null;
            var hair = CastButtons("Hair 9").Single();
            shown = hair.GetComponent<Image>().color;
            hair.onClick.Invoke();
            yield return null;
            AssertSameColour(AppearanceEditing.ColorValue(creator.Draft.Appearance, "Hair", Color.clear), shown, "hair");
            AssertSameColour(AppearanceEditing.ColorValue(creator.Draft.Appearance, "Brows", Color.clear), shown,
                "brows, which take the hair colour whichever brow style is worn");
            var brow = CastButtons("Brows 2").Single();
            shown = brow.GetComponent<Image>().color;
            brow.onClick.Invoke();
            yield return null;
            AssertSameColour(AppearanceEditing.ColorValue(creator.Draft.Appearance, "Brows", Color.clear), shown, "brows set apart");
        }
#endif

#if GAMESIM_UMA
        // UMA only: these pages dress a modular body, and only UMA builds one.
        /// <summary>
        /// The Colors page names what is worn: the tone in its heading, the brows by the whole hair
        /// palette - a dyed colour the hair handed on is "matches hair", not "custom" - and "custom"
        /// only for a colour on no palette. Match hair color puts the hair's colour back on the
        /// brows, and the keyboard or pad on a swatch names it as the pointer does.
        /// </summary>
        [UnityTest]
        public IEnumerator Creator_TheColorsPageNamesTheWornAndTheFocusedSwatch()
        {
            yield return OpenCreator();
            var creator = Creator();
            Assert.That(CharacterBodySource.Provider, Is.InstanceOf<IModularCharacterBodyProvider>());
            CastButtons("Colors")[0].onClick.Invoke();
            yield return null;
            string Heading(string prefix) => creator.GetComponentsInChildren<TMPro.TMP_Text>().Single(text => text.name == prefix + " heading").text;
            Color Worn(string id) => AppearanceEditing.ColorValue(creator.Draft.Appearance, id, Color.clear);

            CastButtons("Skin 1").Single().onClick.Invoke();
            yield return null;
            Assert.That(Heading("Skin"), Does.Contain("PORCELAIN"), "The worn tone is named.");

            // Hair 24 is the dyed Blue: the brows take it and say so.
            CastButtons("Hair 24").Single().onClick.Invoke();
            yield return null;
            Assert.That(Heading("Hair"), Does.Contain("BLUE"));
            Assert.That(Heading("Brows"), Does.Contain("MATCHES HAIR").And.Not.Contain("CUSTOM"), "A dyed colour from the hair is not a custom one.");

            CastButtons("Brows 2").Single().onClick.Invoke();
            yield return null;
            Assert.That(Heading("Brows"), Does.Contain("BLACK").And.Not.Contain("MATCHES HAIR"), "Brows of their own are named.");

            // A dyed brow colour apart from the hair - a saved look's - is named from the whole palette.
            AppearanceEditing.SetColor(creator.Draft.Appearance, "Brows", CharacterPalettes.Named(CharacterPalettes.Hair, "Teal"));
            CastButtons("Colors")[0].onClick.Invoke();
            yield return null;
            Assert.That(Heading("Brows"), Does.Contain("TEAL").And.Not.Contain("CUSTOM"));
            AppearanceEditing.SetColor(creator.Draft.Appearance, "Brows", new Color(.5f, .1f, .9f));
            CastButtons("Colors")[0].onClick.Invoke();
            yield return null;
            Assert.That(Heading("Brows"), Does.Contain("CUSTOM"), "Only a colour on no palette is custom.");

            CastButtons(CharacterCreator.MatchHairCaption).Single().onClick.Invoke();
            yield return null;
            AssertSameColour(Worn("Brows"), Worn("Hair"), "brows matched to the hair");
            Assert.That(Heading("Brows"), Does.Contain("MATCHES HAIR"));

            // Skin 5 is Sand. Focus on it names it; focus leaving names the worn tone again.
            var events = UnityEngine.EventSystems.EventSystem.current;
            Assert.That(events, Is.Not.Null, "The creator is driven by the scene's event system.");
            events.SetSelectedGameObject(CastButtons("Skin 5").Single().gameObject);
            Assert.That(Heading("Skin"), Does.Contain("SAND"), "The focused swatch is named in the heading.");
            events.SetSelectedGameObject(null);
            Assert.That(Heading("Skin"), Does.Contain("PORCELAIN").And.Not.Contain("SAND"), "Focus moving off names the worn tone again.");
        }
#endif

#if GAMESIM_UMA
        // UMA only: these pages dress a modular body, and only UMA builds one.
        /// <summary>
        /// A starting look's garment colour - the one in its photo, seldom a swatch - is on its row,
        /// ringed at the end, and stays there to be picked again after trying another; the row's way
        /// back to the garment's own colour still clears the tint.
        /// </summary>
        [UnityTest]
        public IEnumerator Creator_AStartingLooksGarmentColourIsRingedOnItsRow()
        {
            yield return OpenCreator();
            var creator = Creator();
            Assert.That(CharacterBodySource.Provider, Is.InstanceOf<IModularCharacterBodyProvider>());
            // Chelsie's top is the pink of her photo, which no fabric swatch is.
            creator.Show(new SeasonBuilder.Choice(), CharacterDraft.FromAppearance(CastTemplates.Find("chelsie-baham")), _ => { }, () => { });
            yield return null;
            CastButtons("Clothing")[0].onClick.Invoke();
            yield return null;
            bool Tinted(out Color tint) => AppearanceEditing.TryFabric(AppearanceEditing.Outfit(creator.Draft.Appearance), "Chest", out tint);
            Assert.That(Tinted(out var photo), Is.True, "Her top wears the colour in her photo.");
            List<Button> Row() => Enumerable.Range(1, 16).SelectMany(number => CastButtons("Fabric Chest " + number)).ToList();
            var row = Row();
            var ringed = row.Where(swatch => swatch.transform.Find("Chosen ring") != null).ToList();
            Assert.That(ringed, Has.Count.EqualTo(1), "The worn colour is ringed on the row.");
            Assert.That(ringed[0], Is.SameAs(row.Last()), "A colour off the palette joins the end of the row.");
            Assert.That(row, Has.Count.EqualTo(13), "The twelve fabric swatches and the photo's colour.");
            AssertSameColour(ringed[0].GetComponent<Image>().color, photo, "the ringed swatch");

            CastButtons("Fabric Chest 1").Single().onClick.Invoke();
            yield return null;
            row = Row();
            Assert.That(row, Has.Count.EqualTo(13), "Trying a swatch leaves the photo's colour on the row.");
            ringed = row.Where(swatch => swatch.transform.Find("Chosen ring") != null).ToList();
            Assert.That(ringed, Has.Count.EqualTo(1));
            Assert.That(ringed[0], Is.SameAs(row[0]), "The swatch tried is the one ringed.");
            CastButtons("Fabric Chest 13").Single().onClick.Invoke();
            yield return null;
            Assert.That(Tinted(out var again), Is.True);
            AssertSameColour(again, photo, "the photo's colour picked again from its swatch");

            CastButtons("Original top color").Single().onClick.Invoke();
            yield return null;
            Assert.That(Tinted(out _), Is.False, "The garment's own colour comes back.");
            Assert.That(Row(), Has.Count.EqualTo(13), "And the photo's colour is still there to go back to.");
        }
#endif

        /// <summary>
        /// The six category tiles stay on their panel however short a wide screen leaves it: at a
        /// fixed pitch the sixth hung past the panel's foot at 21:9 with larger text and at 32:9.
        /// </summary>
        [UnityTest]
        public IEnumerator Creator_TheCategoryTilesFitTheirPanelOnWideScreens()
        {
            yield return OpenCreator();
            var creator = Creator();
            var canvas = creator.GetComponent<Canvas>();
            var camera = cameraRig.ViewCamera;
            var previous = camera.targetTexture;
            var textures = new List<RenderTexture>();
            try
            {
                foreach (var (width, height, scale) in new[] { (1280, 540, 1.2f), (1280, 360, 1f) })
                {
                    var texture = new RenderTexture(width, height, 24);
                    textures.Add(texture);
                    camera.targetTexture = texture;
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = Mathf.Max(camera.nearClipPlane + .1f, 1f);
                    creator.FontScale = scale;
                    Canvas.ForceUpdateCanvases();
                    yield return null; yield return null;
                    CastButtons("Body")[0].onClick.Invoke();
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    string shape = width + "x" + height + " at " + scale;
                    var panel = creator.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Appearance categories");
                    var bounds = panel.rect;
                    var corners = new Vector3[4];
                    foreach (string category in CreatorCategories)
                    {
                        var tile = CastButtons(category).Single(button => button.transform.parent == panel).GetComponent<RectTransform>();
                        tile.GetWorldCorners(corners);
                        foreach (var corner in corners)
                        {
                            var local = panel.InverseTransformPoint(corner);
                            Assert.That(local.y, Is.GreaterThanOrEqualTo(bounds.yMin - 1f), category + " hangs below its panel at " + shape);
                            Assert.That(local.y, Is.LessThanOrEqualTo(bounds.yMax + 1f), category + " rises above its panel at " + shape);
                            Assert.That(local.x, Is.InRange(bounds.xMin - 1f, bounds.xMax + 1f), category + " is wider than its panel at " + shape);
                        }
                    }
                }
            }
            finally
            {
                creator.FontScale = 1f;
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                camera.targetTexture = previous;
                foreach (var texture in textures) { texture.Release(); Object.Destroy(texture); }
            }
        }

#if GAMESIM_UMA
        // UMA only: these pages dress a modular body, and only UMA builds one.
        /// <summary>
        /// Face details - freckles, makeup, an older face - are the person's, not the outfit's: worn
        /// from the Face page on every outfit and taken off every outfit, left alone by the Hair
        /// page's reset, and put back as the look began by the Face page's.
        /// </summary>
        [UnityTest]
        public IEnumerator Creator_FaceDetailsAreWornOnEveryOutfitAndResetWithTheFace()
        {
            yield return OpenCreator();
            var creator = Creator();
            Assert.That(CharacterBodySource.Provider, Is.InstanceOf<IModularCharacterBodyProvider>());
            var catalog = ((IModularCharacterBodyProvider)CharacterBodySource.Provider).Catalog;
            // A look that begins with a face detail, so the reset has one to put back.
            var start = CharacterDraft.Blank();
            start.Appearance = catalog.Materialize(start.Appearance);
            var details = catalog.Items.Where(item => item.Slot == "Face" && item.Fits(start.Appearance.bodyId)).ToList();
            Assert.That(details, Has.Count.GreaterThanOrEqualTo(2), "The catalog offers face details for this body.");
            AppearanceEditing.Wear(start.Appearance, details[1], catalog);
            creator.Show(new SeasonBuilder.Choice(), start, _ => { }, () => { });
            yield return null;
            // A second outfit, so "every outfit" is more than one.
            CastButtons("Clothing")[0].onClick.Invoke();
            yield return null;
            CastButtons("Competition").Single().onClick.Invoke();
            yield return null;
            CastButtons("Everyday").Single().onClick.Invoke();
            yield return null;
            string[] Worn() => creator.Draft.Appearance.outfits.Select(outfit => outfit.wardrobe.FirstOrDefault(item => item.slot == "Face")?.itemId).ToArray();
            Assert.That(creator.Draft.Appearance.outfits, Has.Count.GreaterThanOrEqualTo(2));
            Assert.That(Worn(), Is.All.EqualTo(details[1].Id), "The starting face detail is on both outfits.");

            CastButtons("Face")[0].onClick.Invoke();
            yield return null;
            var texts = creator.GetComponentsInChildren<TMPro.TMP_Text>().Where(text => text.isActiveAndEnabled).Select(text => text.text).ToList();
            Assert.That(texts, Does.Contain("FACE DETAILS"), "The Face page has a strip of face details.");
            CastButtons("Previous face details").Single().onClick.Invoke();
            yield return null;
            Assert.That(Worn(), Is.All.EqualTo(details[0].Id), "A face detail is worn on every outfit.");

            CastButtons("Hair")[0].onClick.Invoke();
            yield return null;
            CastButtons("Reset hair").Single().onClick.Invoke();
            yield return null;
            Assert.That(Worn(), Is.All.EqualTo(details[0].Id), "The Hair page's reset leaves the face alone.");

            CastButtons("Face")[0].onClick.Invoke();
            yield return null;
            CastButtons("Remove face details").Single().onClick.Invoke();
            yield return null;
            Assert.That(Worn(), Is.All.Null, "Removed from every outfit, not only the one being edited.");
            CastButtons("Reset face").Single().onClick.Invoke();
            yield return null;
            Assert.That(Worn(), Is.All.EqualTo(details[1].Id), "The Face page's reset puts the look's own face detail back on every outfit.");
        }
#endif

#if GAMESIM_UMA
        // UMA only: these pages dress a modular body, and only UMA builds one.
        /// <summary>
        /// Changing body takes off a face detail the other body has nothing like, and says so:
        /// makeup, drawn for one body alone, came back from the change as whichever face detail the
        /// other body sorted first - an older face nobody chose. An older face stays an older face.
        /// </summary>
        [UnityTest]
        public IEnumerator Creator_ChangingBodyTakesOffMakeupTheOtherBodyCannotWear()
        {
            yield return OpenCreator();
            var creator = Creator();
            Assert.That(CharacterBodySource.Provider, Is.InstanceOf<IModularCharacterBodyProvider>());
            var catalog = ((IModularCharacterBodyProvider)CharacterBodySource.Provider).Catalog;
            var makeup = catalog.Items.First(item => item.Slot == "Face" && item.StyleGroup == "face.makeup");
            var drawnFor = catalog.Bodies.Single(body => makeup.Fits(body.Id));
            var other = catalog.Bodies.Single(body => body.Id != drawnFor.Id);
            var start = CharacterDraft.Blank();
            start.Appearance = catalog.ChangeBody(catalog.Materialize(start.Appearance), drawnFor.Id);
            AppearanceEditing.Wear(start.Appearance, makeup, catalog);
            creator.Show(new SeasonBuilder.Choice(), start, _ => { }, () => { });
            yield return null;
            CastButtons("Body")[0].onClick.Invoke();
            yield return null;
            string Face() => AppearanceEditing.Outfit(creator.Draft.Appearance).wardrobe.FirstOrDefault(item => item.slot == "Face")?.itemId;
            Assert.That(Face(), Is.EqualTo(makeup.Id), "The look begins in makeup.");

            CastButtons(other.Label).Single().onClick.Invoke();
            yield return null;
            Assert.That(creator.Draft.Appearance.bodyId, Is.EqualTo(other.Id));
            Assert.That(Face(), Is.Null, "Makeup the other body cannot wear comes off rather than becoming an older face.");
            var texts = creator.GetComponentsInChildren<TMPro.TMP_Text>().Select(text => text.text).ToList();
            Assert.That(texts.Any(text => text.Contains(makeup.Label + " → removed")), Is.True, "The change says the makeup came off.");

            CastButtons("Undo").Single().onClick.Invoke();
            yield return null;
            Assert.That(Face(), Is.EqualTo(makeup.Id), "Undo puts the makeup back.");
            // An older face drawn for this body alone: the other body's older face takes its place.
            var older = catalog.Items.First(item => item.Slot == "Face" && item.StyleGroup == "face.age" && item.Fits(drawnFor.Id) && !item.Fits(other.Id));
            AppearanceEditing.Wear(creator.Draft.Appearance, older, catalog);
            CastButtons(other.Label).Single().onClick.Invoke();
            yield return null;
            var kept = AppearanceEditing.Find(catalog, Face());
            Assert.That(kept, Is.Not.Null, "An older face is kept through the change.");
            Assert.That(kept.StyleGroup, Is.EqualTo("face.age"), "It is still an older face.");
            Assert.That(kept.Fits(other.Id), Is.True);
        }
#endif

#if GAMESIM_UMA
        // UMA only: these pages dress a modular body, and only UMA builds one.
        /// <summary>
        /// Accessories are picked from their own page, worn with the outfit being edited, undone and
        /// redone like any edit, and put back as they were by that page's reset - taken off a look
        /// that began without them, and put back on one that began with them.
        /// </summary>
        [UnityTest]
        public IEnumerator Creator_AccessoriesAreWornFromTheirPageAndResetWithIt()
        {
            yield return OpenCreator();
            var creator = Creator();
            CastButtons("Accessories")[0].onClick.Invoke();
            yield return null;
            // A second outfit first: the blank look has only the one, and "this outfit only" held of
            // an empty set whatever Wear did.
            CastButtons("Competition").Single().onClick.Invoke();
            yield return null;
            CastButtons("Everyday").Single().onClick.Invoke();
            yield return null;
            string Worn(string slot) => AppearanceEditing.Outfit(creator.Draft.Appearance).wardrobe.FirstOrDefault(item => item.slot == slot)?.itemId;
            CharacterOutfit Other() => creator.Draft.Appearance.outfits.Single(outfit => outfit.id == "Competition");
            Assert.That(creator.Draft.Appearance.activeOutfit, Is.EqualTo("Everyday"));
            Assert.That(Worn("Eyewear"), Is.Null, "The starting look wears no glasses.");
            CastButtons("Black frames").Single().onClick.Invoke();
            yield return null;
            CastButtons("Gold hoops").Single().onClick.Invoke();
            yield return null;
            Assert.That(Worn("Eyewear"), Is.EqualTo(ProceduralAccessories.Prefix + "glasses"));
            Assert.That(Worn("Earrings"), Is.EqualTo(ProceduralAccessories.Prefix + "hoops"));
            Assert.That(Other().wardrobe, Is.Not.Empty, "The other outfit is dressed.");
            Assert.That(Other().wardrobe.Any(item => ProceduralAccessories.Slots.Contains(item.slot)), Is.False, "Worn with this outfit only.");

            CastButtons("Undo").Single().onClick.Invoke();
            yield return null;
            Assert.That(Worn("Earrings"), Is.Null, "Undo takes the hoops off.");
            Assert.That(Worn("Eyewear"), Is.EqualTo(ProceduralAccessories.Prefix + "glasses"), "Only the hoops.");
            CastButtons("Redo").Single().onClick.Invoke();
            yield return null;
            Assert.That(Worn("Earrings"), Is.EqualTo(ProceduralAccessories.Prefix + "hoops"), "Redo puts them back.");

            CastButtons("Reset accessories").Single().onClick.Invoke();
            yield return null;
            Assert.That(Worn("Eyewear"), Is.Null, "Reset takes them off again.");
            Assert.That(Worn("Earrings"), Is.Null);
            Assert.That(Worn("Chest"), Is.Not.Null, "And leaves the clothes alone.");

            // A starting look that wears glasses - Riley's black frames - gets them back from the reset.
            creator.Show(new SeasonBuilder.Choice(), CharacterDraft.FromAppearance(CastTemplates.Find("riley-johnson")), _ => { }, () => { });
            yield return null;
            CastButtons("Accessories")[0].onClick.Invoke();
            yield return null;
            Assert.That(Worn("Eyewear"), Is.EqualTo(ProceduralAccessories.Prefix + "glasses"), "Riley's look wears his glasses.");
            CastButtons("Remove eyewear").Single().onClick.Invoke();
            yield return null;
            Assert.That(Worn("Eyewear"), Is.Null);
            CastButtons("Reset accessories").Single().onClick.Invoke();
            yield return null;
            Assert.That(Worn("Eyewear"), Is.EqualTo(ProceduralAccessories.Prefix + "glasses"), "Reset puts the starting look's glasses back on.");
        }
#endif

#if GAMESIM_UMA
        // UMA only: these pages dress a modular body, and only UMA builds one.
        /// <summary>
        /// Randomize rolls the accessories too - most often to none - unless their page's lock is
        /// on, and colours a random houseguest from the skin out: hair and eyes weighted by the
        /// skin's depth, so deep skin seldom comes with light eyes or blonde, red or white hair.
        /// </summary>
        [UnityTest]
        public IEnumerator Creator_RandomizeRollsAccessoriesUnlessLockedAndColoursFromTheSkin()
        {
            yield return OpenCreator();
            var creator = Creator();
            Assert.That(CharacterBodySource.Provider, Is.InstanceOf<IModularCharacterBodyProvider>());
            creator.Show(new SeasonBuilder.Choice(), CharacterDraft.FromAppearance(CastTemplates.Find("vanessa-rousso")), _ => { }, () => { });
            creator.RandomSeed = 20260925;
            yield return null;
            CastButtons("Accessories")[0].onClick.Invoke();
            yield return null;
            string Accessories() => string.Join(",", AppearanceEditing.Outfit(creator.Draft.Appearance).wardrobe
                .Where(item => ProceduralAccessories.Slots.Contains(item.slot)).OrderBy(item => item.slot).Select(item => item.slot + "=" + item.itemId));
            string start = Accessories();
            Assert.That(start, Does.Contain(ProceduralAccessories.Prefix + "cap"), "Her starting look wears her trucker cap.");

            var lightEyes = new[] { "Green", "Grey-green", "Blue", "Light blue", "Grey-blue", "Grey" };
            var unlikelyHair = new[] { "Auburn", "Copper", "Strawberry", "Dark blonde", "Honey blonde", "Golden blonde", "Platinum", "Silver", "White", "Salt and pepper" };
            const int rolls = 30;
            int changed = 0, bare = 0, worn = 0, deep = 0, unlikely = 0;
            for (int roll = 0; roll < rolls; roll++)
            {
                CastButtons("Randomize").Single().onClick.Invoke();
                yield return null;
                string now = Accessories();
                int count = now.Length == 0 ? 0 : now.Split(',').Length;
                if (now != start) changed++;
                if (count == 0) bare++;
                worn += count;
                var appearance = creator.Draft.Appearance;
                if (CharacterPalettes.DepthOf(AppearanceEditing.ColorValue(appearance, "Skin", Color.clear)) < 2) continue;
                deep++;
                string eyes = CharacterPalettes.NameOf(CharacterPalettes.Eyes, AppearanceEditing.ColorValue(appearance, "Eyes", Color.clear));
                string hair = CharacterPalettes.NameOf(CharacterPalettes.Hair, AppearanceEditing.ColorValue(appearance, "Hair", Color.clear));
                Assert.That(eyes, Is.Not.Null, "Roll " + roll + " gave palette eyes.");
                Assert.That(hair, Is.Not.Null, "Roll " + roll + " gave a palette hair colour.");
                if (lightEyes.Contains(eyes) || unlikelyHair.Contains(hair)) unlikely++;
            }
            Assert.That(changed, Is.GreaterThan(0), "Randomize rolls the accessories when they are not locked.");
            Assert.That(bare, Is.GreaterThan(0), "Often a random houseguest wears none at all.");
            Assert.That(worn, Is.GreaterThan(0), "But sometimes one.");
            Assert.That(worn, Is.LessThan(rolls * ProceduralAccessories.Slots.Length / 2), "An accessory is the exception, not the rule.");
            Assert.That(deep, Is.GreaterThan(0), "Tan and deep skin come up as often as fair.");
            Assert.That(unlikely, Is.LessThanOrEqualTo(Mathf.Max(2, deep / 2)),
                unlikely + " of " + deep + " tan or deep-skinned rolls had light eyes or blonde, red or grey hair.");

            CastButtons("Lock Accessories").Single().onClick.Invoke();
            yield return null;
            string locked = Accessories();
            for (int roll = 0; roll < 4; roll++)
            {
                CastButtons("Randomize").Single().onClick.Invoke();
                yield return null;
                Assert.That(Accessories(), Is.EqualTo(locked), "Roll " + roll + " kept the locked accessories.");
            }
            Assert.That(CastButtons("Unlock Accessories"), Is.Not.Empty, "The lock says it is on.");
        }
#endif

#if GAMESIM_UMA
        // UMA only: these pages dress a modular body, and only UMA builds one.
        /// <summary>The face page offers its features under headings, the jaw among them.</summary>
        [UnityTest]
        public IEnumerator Creator_TheFaceGroupsItsFeaturesUnderHeadings()
        {
            yield return OpenCreator();
            var creator = Creator();
            CastButtons("Face")[0].onClick.Invoke();
            yield return null;
            var texts = creator.GetComponentsInChildren<TMPro.TMP_Text>().Where(text => text.isActiveAndEnabled).Select(text => text.text).ToList();
            foreach (string heading in new[] { "FACE SHAPE", "CHEEKS", "EYES & BROWS", "NOSE", "MOUTH & EARS" })
                Assert.That(texts, Does.Contain(heading), heading);
            foreach (string feature in new[] { "Jaw width", "Cheekbones", "Eye tilt", "Nose bridge" })
                Assert.That(creator.GetComponentsInChildren<Slider>().Any(slider => slider.name == feature + " slider"), Is.True, feature);
        }
#endif

        /// <summary>
        /// Handed to a camera of another shape - what a review capture does, and what a window resize
        /// does to the screen - the creator lays itself out again for that frame on the next frame.
        /// </summary>
        [UnityTest]
        public IEnumerator Creator_LaysItselfOutAgainForTheFrameItIsDrawnTo()
        {
            yield return OpenCreator();
            var creator = Creator();
            var canvas = creator.GetComponent<Canvas>();
            var camera = cameraRig.ViewCamera;
            var texture = new RenderTexture(1600, 900, 24);
            var previous = camera.targetTexture;
            try
            {
                camera.targetTexture = texture;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = Mathf.Max(camera.nearClipPlane + .1f, 1f);
                Canvas.ForceUpdateCanvases();
                yield return null; yield return null;
                var root = (RectTransform)creator.transform;
                Assert.That(root.rect.width / root.rect.height, Is.EqualTo(16f / 9f).Within(.01f), "The canvas took the camera's shape.");
                AssertCreatorFits(creator, "Appearance through a 16:9 camera");
                var footer = creator.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Fixed footer");
                var start = CastButtons(CharacterCreator.StartCaption).Single().GetComponent<RectTransform>();
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(root, start);
                Assert.That(bounds.max.x, Is.GreaterThan(root.rect.xMax - root.rect.width * .4f),
                    "The way on stands at the right of this frame, not where the 4:3 frame put it.");
                Assert.That(start.IsChildOf(footer), Is.True);
            }
            finally
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                camera.targetTexture = previous;
                texture.Release();
                Object.Destroy(texture);
            }
        }

        [UnityTest]
        public IEnumerator Creator_ANewHouseguestAsksBeforeReplacingTheDraft()
        {
            yield return OpenCreator();
            var creator = Creator();
            creator.Draft.Name = "Keep Me";
            CastButtons("My Houseguests")[0].onClick.Invoke();
            yield return null;
            CastButtons("Create new houseguest").Single().onClick.Invoke();
            yield return null;
            Assert.That(creator.Draft.Name, Is.EqualTo("Keep Me"), "One press asks; it does not throw the draft away.");
            CastButtons("Confirm new houseguest").Single().onClick.Invoke();
            yield return null;
            Assert.That(creator.Draft.Name, Is.Empty, "The second press starts a blank houseguest.");
            Assert.That(CastButtons("Next starting look"), Is.Not.Empty, "It opens on the appearance step.");
        }

#if GAMESIM_UMA
        // UMA only: these pages dress a modular body, and only UMA builds one.
        [UnityTest]
        public IEnumerator Creator_ALockedCategoryKeepsItsLookThroughRandomize()
        {
            yield return OpenCreator();
            var creator = Creator();
            Assert.That(CharacterBodySource.Provider, Is.InstanceOf<IModularCharacterBodyProvider>());
            CastButtons("Hair")[0].onClick.Invoke();
            yield return null;
            CastButtons("Lock Hair").Single().onClick.Invoke();
            yield return null;
            string Hair() => string.Join(",", AppearanceEditing.Outfit(creator.Draft.Appearance).wardrobe
                .Where(item => AppearanceEditing.IsCharacterSlot(item.slot)).OrderBy(item => item.slot).Select(item => item.slot + "=" + item.itemId));
            string before = Hair(), look = creator.Draft.Appearance.ContentKey();
            var colour = AppearanceEditing.ColorValue(creator.Draft.Appearance, "Hair", Color.clear);
            for (int roll = 0; roll < 4; roll++)
            {
                CastButtons("Randomize").Single().onClick.Invoke();
                yield return null;
                Assert.That(Hair(), Is.EqualTo(before), "Roll " + roll + " kept the locked hair.");
                AssertSameColour(AppearanceEditing.ColorValue(creator.Draft.Appearance, "Hair", Color.clear), colour,
                    "roll " + roll + " kept the locked hair's colour, which is on the Hair page");
            }
            Assert.That(creator.Draft.Appearance.ContentKey(), Is.Not.EqualTo(look), "Randomize changed what was not locked.");
            Assert.That(CastButtons("Unlock Hair"), Is.Not.Empty, "The lock says it is on.");
        }
#endif

        /// <summary>
        /// Looking at a saved houseguest does not load it: the bar's Delete reaches any card while the
        /// draft on screen stays as it is.
        /// </summary>
        [UnityTest]
        public IEnumerator Creator_LookingAtASavedHouseguestLeavesTheDraftAlone()
        {
            yield return OpenCreator();
            var creator = Creator();
            foreach (string name in new[] { "Saved Ada", "Saved Bo" })
            {
                var saved = creator.Draft.Copy();
                saved.Name = name;
                Assert.That(creator.ProfileStore.Save(CharacterProfile.FromDraft(System.Guid.NewGuid().ToString("N"), saved), out var error), Is.True, error);
            }
            creator.Draft.Name = "Unsaved Cy";
            CastButtons("My Houseguests")[0].onClick.Invoke();
            yield return null;
            CastButtons("Details for Saved Bo").Single().onClick.Invoke();
            yield return null;
            Assert.That(creator.Draft.Name, Is.EqualTo("Unsaved Cy"), "Looking is not loading.");
            CastButtons("Delete Saved Bo").Single().onClick.Invoke();
            yield return null;
            CastButtons("Confirm delete Saved Bo").Single().onClick.Invoke();
            yield return null;
            Assert.That(creator.ProfileStore.List().Select(profile => profile.name), Is.EquivalentTo(new[] { "Saved Ada" }));
            Assert.That(creator.Draft.Name, Is.EqualTo("Unsaved Cy"), "Deleting a saved houseguest leaves the draft alone.");
        }

        /// <summary>The comparison with the original look belongs to the Appearance page; leaving it ends it.</summary>
        [UnityTest]
        public IEnumerator Creator_TheComparisonEndsWithTheAppearancePage()
        {
            yield return OpenCreator();
            CastButtons("Next starting look")[0].onClick.Invoke();
            yield return null;
            CastButtons("Compare original").Single().onClick.Invoke();
            yield return null;
            Assert.That(CastButtons("Return to edited look"), Is.Not.Empty);
            CastButtons("Identity")[0].onClick.Invoke();
            yield return null;
            CastButtons("Appearance")[0].onClick.Invoke();
            yield return null;
            Assert.That(CastButtons("Compare original"), Is.Not.Empty, "Back on Appearance, the edited look is the one shown.");
            Assert.That(CastButtons("Return to edited look"), Is.Empty);
        }

        private static void AssertSameColour(Color actual, Color expected, string what)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(.01f), what + " red");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(.01f), what + " green");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(.01f), what + " blue");
        }
    }
}
