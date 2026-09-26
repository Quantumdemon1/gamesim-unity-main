using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UMA;
using UMA.CharacterSystem;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Uma.Tests
{
    /// <summary>
    /// What a UMA body's own finishing pass lays on it, checked on what UMA actually drew: hair and
    /// glasses grown again after a change of clothes, the brows in a colour of their own, and the
    /// skin in the swatch picked.
    /// </summary>
    public sealed class UmaBodyTintPlayModeTests
    {
        private GameObject cast;
        private readonly List<GameObject> actors = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            // UMA logs a missing sway bone on some hair; that is UMA's, not the tint's.
            LogAssert.ignoreFailingMessages = true;
            cast = new GameObject("UMA tint provider", typeof(GamesimUmaCast));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var actor in actors) if (actor != null) Object.Destroy(actor);
            actors.Clear();
            Object.Destroy(cast);
            LogAssert.ignoreFailingMessages = false;
        }

        private static IModularCharacterBodyProvider Provider => (IModularCharacterBodyProvider)CharacterBodySource.Provider;

        private sealed class Built { public CharacterBody Body; public GameObject Actor; }

        private GameObject Actor(string name)
        {
            var actor = new GameObject(name + " " + actors.Count);
            actor.transform.position = new Vector3(actors.Count * 3f, 0f, 0f);
            actors.Add(actor);
            return actor;
        }

        /// <summary>A library look as the creator saves it, wearing <paramref name="wear"/> (catalog ids).</summary>
        private static CharacterAppearance Look(string preset, params string[] wear)
        {
            var catalog = Provider.Catalog;
            var appearance = catalog.Materialize(CharacterAppearance.Preset(preset));
            foreach (string id in wear)
            {
                var item = AppearanceEditing.Find(catalog, id);
                Assert.That(item, Is.Not.Null, id + " is in the catalog.");
                AppearanceEditing.Wear(appearance, item, catalog);
                Assert.That(AppearanceEditing.Outfit(appearance).wardrobe.Any(worn => item.Matches(worn.itemId)), Is.True, id + " is worn.");
            }
            return appearance;
        }

        /// <summary>A body built straight from the provider, as the creator's studio builds one, and settled.</summary>
        private IEnumerator Build(Built into, string id, CharacterAppearance appearance)
        {
            var actor = Actor("Tint actor");
            Assert.That(Provider.TryCreate(new CharacterBodyRequest(id, id, appearance, CharacterBuildPurpose.Studio, 1),
                actor.transform, Color.white, out var body), Is.True, id + " has a body.");
            var state = body.Root.GetComponent<CharacterBodyBuildState>();
            double deadline = Time.realtimeSinceStartupAsDouble + 30;
            while (!state.Ready && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(state.Ready, Is.True, "The body finished building.");
            yield return null;
            into.Body = body;
            into.Actor = actor;
        }

        // ------------------------------------------------------------------ a change of clothes

        /// <summary>
        /// A change of clothes in the house - into swimwear at the pool, and back - builds the new
        /// look out of sight, at no size, behind the body on show, and swaps it in once it is made.
        /// The hair and glasses grown on the old body are grown on the new one. Fitted while it had
        /// no size, they were fitted to a head collapsed to a single point, in which the scan finds
        /// no head at all, and the houseguest walked out of the change bald and without their glasses.
        /// </summary>
        [UnityTest]
        public IEnumerator AChangeOfClothesKeepsTheGrownHairAndGlasses()
        {
            yield return null;
            var player = ContentCatalog.Create(1).contestants.First(contestant => contestant.isPlayer);
            player.appearance = Look(ContentCatalog.PlayerId, "gs-hair-afro", "gs-acc-glasses");
            var swimming = CharacterOutfits.ForContext(player, CharacterOutfits.Swimwear);
            Assert.That(swimming.appearance.activeOutfit, Is.EqualTo(CharacterOutfits.Swimwear),
                "The look wears underwear, so it has a set to swim in: the hair kept, the clothes and glasses off.");

            var actor = Actor("Dressing actor");
            var presentation = CharacterPresentation.Attach(actor, player, Color.white);
            yield return Settle(presentation);
            AssertGrown(presentation, true, "Dressed for the day");

            CharacterPresentation.Dress(actor, swimming, Color.white);
            Assert.That(presentation.IsChangingOutfit, Is.True, "The swimming set is made behind the body on show.");
            yield return Changed(presentation, CharacterOutfits.Swimwear);
            AssertGrown(presentation, false, "Changed to swim");

            CharacterPresentation.Dress(actor, CharacterOutfits.ForContext(player, CharacterOutfits.Everyday), Color.white);
            Assert.That(presentation.IsChangingOutfit, Is.True, "The day's clothes are made behind the body on show.");
            yield return Changed(presentation, CharacterOutfits.Everyday);
            AssertGrown(presentation, true, "Changed back");
        }

        /// <summary>Waits for an attached houseguest's body: built, and no longer behind a stand-in.</summary>
        private static IEnumerator Settle(CharacterPresentation presentation)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 30;
            while (!Settled(presentation) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(Settled(presentation), Is.True, "The body finished building and took the stand-in's place.");
            yield return null;
        }

        private static bool Settled(CharacterPresentation presentation)
        {
            if (presentation.IsBodyAssembling || presentation.VisualRoot == null) return false;
            var state = presentation.VisualRoot.GetComponentInChildren<CharacterBodyBuildState>();
            return state != null && state.Ready;
        }

        /// <summary>
        /// Waits for a change of clothes to finish: the new body made and swapped in. The swap and
        /// the fitting happen in one LateUpdate pass, so the pieces are there the moment it reports done.
        /// </summary>
        private static IEnumerator Changed(CharacterPresentation presentation, string outfit)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 30;
            while (presentation.IsChangingOutfit && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(presentation.IsChangingOutfit, Is.False, "The " + outfit + " set was made and swapped in.");
            Assert.That(presentation.AppearanceSnapshot.activeOutfit, Is.EqualTo(outfit), "The body on show wears the " + outfit + " set.");
        }

        /// <summary>
        /// The body on show wears one head of hair, the afro, above its eyes and, when
        /// <paramref name="glasses"/>, the glasses at them: each hung from this body's own head and
        /// built of real points.
        /// </summary>
        private static void AssertGrown(CharacterPresentation presentation, bool glasses, string when)
        {
            var body = presentation.VisualRoot;
            // The live rig: UMA replaces the Animator a body was created with as it builds.
            var rig = body.GetComponentInChildren<Animator>();
            Assert.That(rig != null && rig.isHuman, Is.True, when + ": the body on show has its rig.");
            var head = rig.GetBoneTransform(HumanBodyBones.Head);
            var left = body.InverseTransformPoint(rig.GetBoneTransform(HumanBodyBones.LeftEye).position);
            var right = body.InverseTransformPoint(rig.GetBoneTransform(HumanBodyBones.RightEye).position);
            var eyes = (left + right) * .5f;
            float apart = Mathf.Abs(right.x - left.x);
            var pieces = body.GetComponentsInChildren<GrownPiece>();

            var hair = pieces.Where(piece => piece.ItemId.StartsWith(ProceduralHair.Prefix)).ToArray();
            Assert.That(hair.Select(piece => piece.ItemId), Is.EqualTo(new[] { "gs-hair-afro" }), when + ": one head of hair, the afro.");
            Assert.That(hair[0].transform.parent, Is.SameAs(head), when + ": the hair hangs from this body's head.");
            var afro = Local(body, hair[0], when);
            Assert.That(afro.max.y - eyes.y, Is.InRange(.08f, .4f), when + ": the afro stands above the eyes.");
            Assert.That(afro.min.y, Is.GreaterThan(eyes.y - .25f), when + ": and stays on the head.");
            Assert.That(afro.size.x, Is.GreaterThan(apart * 2f), when + ": as wide as a head, not a point.");

            if (!glasses) return;
            var frames = pieces.Where(piece => piece.ItemId == "gs-acc-glasses").ToArray();
            Assert.That(frames, Has.Length.EqualTo(1), when + ": the glasses are on.");
            Assert.That(frames[0].transform.parent, Is.SameAs(head), when + ": the glasses hang from this body's head.");
            var lenses = Local(body, frames[0], when);
            Assert.That(Mathf.Abs(lenses.center.y - eyes.y), Is.LessThan(.03f), when + ": the glasses sit at the height of the eyes.");
            Assert.That(lenses.max.z, Is.GreaterThan(eyes.z), when + ": in front of them.");
            Assert.That(lenses.size.x, Is.GreaterThan(apart * 1.6f), when + ": wider than the eyes.");
        }

        /// <summary>A piece's bounds in the body's own frame - x right, y up, z forward - failing on any point that is not a number.</summary>
        private static Bounds Local(Transform body, GrownPiece piece, string when)
        {
            var vertices = piece.GetComponent<MeshFilter>().sharedMesh.vertices;
            Assert.That(vertices, Is.Not.Empty, when + ": " + piece.ItemId + " has a mesh.");
            Bounds? bounds = null;
            int broken = 0;
            foreach (var vertex in vertices)
            {
                var point = body.InverseTransformPoint(piece.transform.TransformPoint(vertex));
                float sum = point.x + point.y + point.z;
                if (float.IsNaN(sum) || float.IsInfinity(sum)) { broken++; continue; }
                if (bounds.HasValue) { var grown = bounds.Value; grown.Encapsulate(point); bounds = grown; }
                else bounds = new Bounds(point, Vector3.zero);
            }
            Assert.That(broken, Is.Zero, when + ": every point of " + piece.ItemId + " is a number.");
            return bounds.Value;
        }

        // ------------------------------------------------------------------ a rebuild mid-look

        /// <summary>
        /// A rebuild that lands while a houseguest is looking round at someone - a colour tried, a
        /// proportion changed - refits their locs to the head as it rests, not turned and nodded to
        /// face whoever it is: the locs come out as a rebuild once the look is over makes them.
        /// Fitted to the looking head, the strands were built falling from where its roots were just
        /// then, and once the look ended their ends hung where no head was.
        /// </summary>
        [UnityTest]
        public IEnumerator ARebuildMidLookFitsLocsToTheHeadAsItRests()
        {
            yield return null;
            var player = ContentCatalog.Create(1).contestants.First(contestant => contestant.isPlayer);
            player.appearance = Look(ContentCatalog.PlayerId, "gs-hair-locs");
            var actor = Actor("Looking actor");
            var presentation = CharacterPresentation.Attach(actor, player, Color.white);
            presentation.SetReducedMotion(false);
            yield return Settle(presentation);
            // The idle held still, so the two rebuilds read the same body but for the look.
            var animator = presentation.VisualRoot.GetComponentInChildren<Animator>();
            animator.speed = 0f;
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            yield return null;
            var resting = head.rotation;
            GrownPiece Strands() => presentation.VisualRoot.GetComponentsInChildren<GrownPiece>()
                .SingleOrDefault(piece => piece.name == ProceduralHair.StrandsName);
            // Rebuilt as a colour change rebuilds it; UMA refits in its Update, with the head as the
            // last frame's look left it.
            IEnumerator Rebuild(string when)
            {
                var before = Strands();
                presentation.VisualRoot.GetComponentInChildren<DynamicCharacterAvatar>().UpdateColors(true);
                double deadline = Time.realtimeSinceStartupAsDouble + 20;
                while (Time.realtimeSinceStartupAsDouble < deadline && Strands() == before) yield return null;
                Assert.That(Strands(), Is.Not.Null.And.Not.SameAs(before), "The rebuild " + when + " refits the locs.");
            }
            IEnumerator Wait(double seconds)
            {
                double until = Time.realtimeSinceStartupAsDouble + seconds;
                while (Time.realtimeSinceStartupAsDouble < until) yield return null;
            }

            // Someone off to one side and well below: the look turns and nods the head as far as it goes.
            var target = Actor("Look target");
            target.transform.position = actor.transform.position + actor.transform.right * 1.2f + actor.transform.forward * .4f + Vector3.down * 1.2f;
            presentation.LookAt(target.transform, 60f);
            yield return Wait(1.5);
            Assert.That(Quaternion.Angle(head.rotation, resting), Is.GreaterThan(30f), "The head is turned to look when the rebuild lands.");
            yield return Rebuild("mid-look");
            var midLook = Strands().Mesh.vertices;

            // The look over, and the head back as it rests.
            presentation.LookAt(null, 0f);
            yield return Wait(1.5);
            Assert.That(Quaternion.Angle(head.rotation, resting), Is.LessThan(2f), "The head is back as it rests.");
            yield return Rebuild("after the look");
            var afterLook = Strands().Mesh.vertices;

            Assert.That(midLook, Has.Length.EqualTo(afterLook.Length), "The same strands are grown.");
            // All but a few points where the later rebuild puts them, and none far off: skin with a
            // trace of the neck in it shifts a little; a look's nod built in moves nearly every strand.
            var offsets = Enumerable.Range(0, afterLook.Length).Select(i => Vector3.Distance(afterLook[i], midLook[i])).OrderBy(d => d).ToArray();
            float typical = offsets[(int)(offsets.Length * .95f)], off = offsets[offsets.Length - 1];
            Assert.That(typical, Is.LessThan(.002f),
                "Rebuilt mid-look, the locs are built as a rebuild with the head at rest builds them: 5% are more than " + typical.ToString("0.0000") + " m off.");
            Assert.That(off, Is.LessThan(.03f), "and none is far off: " + off.ToString("0.000") + " m.");
        }

        /// <summary>
        /// A rebuild that lands while a houseguest lies in bed - a change into sleepwear finishing
        /// after they lay down, a colour tried - fits their hair to them standing, as it was fitted
        /// before they lay down. Fitted as they lay, the hair's base was drawn to a hairline tilted
        /// with the head and the strands were built falling past a head lying on its side; getting
        /// up, they wore it so until the next rebuild.
        /// </summary>
        [UnityTest]
        public IEnumerator ARebuildInBedFitsTheHairToTheHouseguestStanding()
        {
            yield return null;
            var player = ContentCatalog.Create(1).contestants.First(contestant => contestant.isPlayer);
            player.appearance = Look(ContentCatalog.PlayerId, "gs-hair-locs");
            var actor = Actor("Sleeping actor");
            var presentation = CharacterPresentation.Attach(actor, player, Color.white);
            yield return Settle(presentation);
            GrownPiece Piece(string name) => presentation.VisualRoot.GetComponentsInChildren<GrownPiece>().SingleOrDefault(piece => piece.name == name);
            var head = presentation.VisualRoot.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head);
            float standingHead = head.position.y - actor.transform.position.y;
            var standingBase = Piece(ProceduralHair.RootName).Mesh.vertices;
            var standingStrands = Piece(ProceduralHair.StrandsName).Mesh.vertices;

            presentation.SetActivity(CharacterPresentation.BodyActivity.Sleeping);
            double lying = Time.realtimeSinceStartupAsDouble + 10;
            while (Time.realtimeSinceStartupAsDouble < lying && head.position.y - actor.transform.position.y > standingHead * .6f) yield return null;
            Assert.That(head.position.y - actor.transform.position.y, Is.LessThan(standingHead * .6f), "The houseguest has lain down.");

            var before = Piece(ProceduralHair.StrandsName);
            presentation.VisualRoot.GetComponentInChildren<DynamicCharacterAvatar>().UpdateColors(true);
            double deadline = Time.realtimeSinceStartupAsDouble + 20;
            while (Time.realtimeSinceStartupAsDouble < deadline && Piece(ProceduralHair.StrandsName) == before) yield return null;
            Assert.That(Piece(ProceduralHair.StrandsName), Is.Not.SameAs(before), "The rebuild in bed refits the hair.");
            Assert.That(head.position.y - actor.transform.position.y, Is.LessThan(standingHead * .6f), "and they are still lying down after it.");

            var bedBase = Piece(ProceduralHair.RootName).Mesh.vertices;
            var bedStrands = Piece(ProceduralHair.StrandsName).Mesh.vertices;
            Assert.That(bedBase, Has.Length.EqualTo(standingBase.Length), "The same base is grown.");
            float baseOff = Enumerable.Range(0, bedBase.Length).Max(i => Vector3.Distance(bedBase[i], standingBase[i]));
            Assert.That(baseOff, Is.LessThan(.002f), "Refitted in bed, the base sits on the head as it did fitted standing: " + baseOff.ToString("0.000") + " m off.");
            Assert.That(bedStrands, Has.Length.EqualTo(standingStrands.Length), "The same strands are grown.");
            var offsets = Enumerable.Range(0, bedStrands.Length).Select(i => Vector3.Distance(bedStrands[i], standingStrands[i])).OrderBy(d => d).ToArray();
            float typical = offsets[(int)(offsets.Length * .95f)];
            Assert.That(typical, Is.LessThan(.002f), "Refitted in bed, the strands are built as they were standing: 5% are more than " + typical.ToString("0.0000") + " m off.");
        }

        // ------------------------------------------------------------------ brows

        /// <summary>
        /// The brows are drawn in the brow colour; the hair and a beard keep the hair colour. UMA's
        /// brow styles are drawn in the shared hair colour, so without a colour of their own the
        /// Brows row changed nothing - and handed a copy of the hair colour, its property block
        /// would draw them in the hair colour still.
        /// </summary>
        [UnityTest]
        public IEnumerator BrowsAreDrawnInTheBrowColourAndTheBeardInTheHairColour()
        {
            yield return null;
            var red = CharacterPalettes.Named(CharacterPalettes.Hair, "Red");
            var black = CharacterPalettes.Named(CharacterPalettes.Hair, "Jet black");
            var appearance = Look(ContentCatalog.PlayerId, "Beard_Trimmed_Recipe");
            AppearanceEditing.SetColor(appearance, "Hair", red);
            AppearanceEditing.SetColor(appearance, "Brows", black);
            var built = new Built(); yield return Build(built, ContentCatalog.PlayerId, appearance);
            var data = built.Body.Root.GetComponent<DynamicCharacterAvatar>().umaData;

            var brows = Slots(data, "Eyebrows_Average_Average");
            Assert.That(brows, Is.Not.Empty, "The brows are worn.");
            foreach (var slot in brows)
            {
                foreach (var overlay in slot.GetOverlayList().Where(candidate => candidate?.colorData != null))
                {
                    Assert.That(Apart(overlay.colorData.channelMask[0], black), Is.LessThan(.02f), overlay.overlayName + " is painted the brow colour.");
                    Assert.That(overlay.colorData.HasProperties, Is.False, overlay.overlayName + " carries no property block to draw over the tint.");
                }
                Assert.That(Apart(Drawn(built.Body, data, slot).GetColor("_BaseColor"), black), Is.LessThan(.02f),
                    "The brows are drawn in the brow colour, not the hair's.");
            }

            var hair = Slots(data, "Hair_LeftPart_Recipe");
            Assert.That(hair, Is.Not.Empty, "The hair is worn.");
            foreach (var slot in hair)
                Assert.That(Apart(Drawn(built.Body, data, slot).GetColor("_BaseColor"), red), Is.LessThan(.02f), "The hair is drawn in the hair colour.");

            // The beard is drawn with the same overlay as the brows, CardWhiskers, on a slot of its
            // own: painting the brows must leave it on the shared hair colour, not hand it a private
            // one. (UMA rebuilds the recipe's shared-colour list from the avatar's colours as it
            // builds, so the list's instance need not be the one an overlay holds; the share and its
            // name are what hold.)
            var beard = Slots(data, "Beard_Trimmed_Recipe");
            Assert.That(beard, Is.Not.Empty, "The beard is worn.");
            var whiskers = beard.SelectMany(slot => slot.GetOverlayList()).Where(overlay => overlay?.colorData != null).ToArray();
            Assert.That(whiskers, Is.Not.Empty);
            foreach (var overlay in whiskers)
            {
                Assert.That(overlay.colorData.IsASharedColor, Is.True, overlay.overlayName + " on the beard stays on a shared colour.");
                Assert.That(overlay.colorData.name, Is.EqualTo("Hair"), overlay.overlayName + " on the beard stays on the shared hair colour.");
                Assert.That(Apart(overlay.colorData.channelMask[0], red), Is.LessThan(.02f), "The beard is the hair colour.");
            }
        }

        /// <summary>
        /// UMA's HD brows carry a private colour of their own, whose additive channel the recipe
        /// sets. Painted the brow colour, it is repainted in place of being replaced, so that channel
        /// is still the recipe's.
        /// </summary>
        [UnityTest]
        public IEnumerator HdBrowsArePaintedTheBrowColourAndKeepTheirRecipesAdditive()
        {
            yield return null;
            var brown = CharacterPalettes.Named(CharacterPalettes.Hair, "Dark brown");
            var appearance = Look(ContentCatalog.PlayerId, "HDEyeBrows_Wardrobe");
            AppearanceEditing.SetColor(appearance, "Brows", brown);
            var built = new Built(); yield return Build(built, ContentCatalog.PlayerId, appearance);
            var data = built.Body.Root.GetComponent<DynamicCharacterAvatar>().umaData;

            var overlays = data.umaRecipe.slotDataList.Where(slot => slot != null).SelectMany(slot => slot.GetOverlayList())
                .Where(overlay => overlay?.colorData != null && overlay.overlayName == "HDEyeBrows_Overlay").ToArray();
            Assert.That(overlays, Is.Not.Empty, "The HD brows are worn.");
            foreach (var overlay in overlays)
            {
                var colour = overlay.colorData;
                Assert.That(colour.IsASharedColor, Is.False, "The HD brows keep a colour of their own.");
                Assert.That(Apart(colour.channelMask[0], brown), Is.LessThan(.02f), "Painted the brow colour.");
                // HDEyeBrows_Wardrobe's own colour: a white multiply, and an additive of (0, 0, 0, 255).
                Assert.That(colour.channelAdditiveMask, Is.Not.Empty);
                Assert.That(colour.channelAdditiveMask[0].a, Is.EqualTo(1f).Within(.01f), "The additive alpha the recipe gives the brows is kept.");
            }
        }

        /// <summary>The slots a wardrobe recipe put on a built body.</summary>
        private static SlotData[] Slots(UMAData data, string recipeName)
        {
            var recipe = UMAAssetIndexer.Instance.GetAsset<UMAWardrobeRecipe>(recipeName);
            Assert.That(recipe, Is.Not.Null, recipeName + " is in the Global Library.");
            var ids = recipe.PackedLoad().slotsV3.Where(part => part != null && !string.IsNullOrEmpty(part.id)).Select(part => part.id).ToArray();
            return data.umaRecipe.slotDataList.Where(slot => slot != null && ids.Contains(slot.slotName)).ToArray();
        }

        /// <summary>The material UMA generated for a slot, checked to be one the body draws and to have a base colour.</summary>
        private static Material Drawn(CharacterBody body, UMAData data, SlotData slot)
        {
            var generated = data.generatedMaterials.materials.FirstOrDefault(candidate => candidate?.materialFragments != null
                && candidate.materialFragments.Any(fragment => fragment?.slotData != null
                    && (fragment.slotData == slot || fragment.slotData.slotName == slot.slotName)));
            Assert.That(generated?.material, Is.Not.Null, slot.slotName + " has a generated material.");
            var drawn = body.Root.GetComponentsInChildren<SkinnedMeshRenderer>().SelectMany(renderer => renderer.sharedMaterials);
            Assert.That(drawn.Contains(generated.material), Is.True, slot.slotName + "'s material is one the body draws.");
            Assert.That(generated.material.HasProperty("_BaseColor"), Is.True, slot.slotName + "'s material has a base colour to read.");
            return generated.material;
        }

        private static float Apart(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);

        // ------------------------------------------------------------------ skin

        /// <summary>
        /// Skin is drawn in the swatch picked, measured on the texture UMA generated: the mean of the
        /// generated skin texture, in linear light, lands on the swatch at a light, a medium and a
        /// deep tone, on both bodies. The arithmetic test in <c>UmaGrownPiecesPlayModeTests</c> checks
        /// only the inversion, against the albedo constant it was worked out from; this measures what
        /// is drawn, so it fails if that constant is wrong, if UMA merges the colour in gamma, or if a
        /// body is drawn with a different skin texture.
        /// </summary>
        [UnityTest]
        public IEnumerator SkinIsDrawnInTheSwatchPicked_MeasuredOnTheGeneratedTexture()
        {
            yield return null;
            var races = new HashSet<string>();
            foreach (string tone in new[] { "Porcelain", "Tan", "Espresso" })
                foreach (string preset in new[] { ContentCatalog.PlayerId, "emma-brown" })
                {
                    var swatch = CharacterPalettes.Named(CharacterPalettes.Skin, tone);
                    var appearance = Look(preset);
                    AppearanceEditing.SetColor(appearance, "Skin", swatch);
                    string label = tone + " on " + appearance.bodyId;
                    races.Add(appearance.bodyId);
                    var built = new Built(); yield return Build(built, preset, appearance);

                    var skin = built.Body.Root.GetComponentsInChildren<Renderer>().SelectMany(renderer => renderer.sharedMaterials)
                        .FirstOrDefault(material => material != null && material.shader != null && material.shader.name.Contains("Skin"));
                    Assert.That(skin, Is.Not.Null, label + ": the body has a skin material.");
                    var mean = MeanOf(skin.GetTexture("_BaseMap"), out int counted);
                    Assert.That(counted, Is.GreaterThan(1000), label + ": the generated skin texture was read back.");
                    for (int channel = 0; channel < 3; channel++)
                    {
                        float target = Mathf.GammaToLinearSpace(swatch[channel]);
                        Assert.That(mean[channel], Is.EqualTo(target).Within(.03f),
                            label + ", channel " + channel + ": drawn " + mean[channel].ToString("0.000") + " in linear light, the swatch is " + target.ToString("0.000") + ".");
                    }
                    Object.Destroy(built.Actor);
                    yield return null;
                }
            Assert.That(races, Is.EquivalentTo(new[] { UmaCastLibrary.MaleRace, UmaCastLibrary.FemaleRace }), "Both bodies were measured.");
        }

        /// <summary>
        /// The mean of a texture's drawn pixels, in linear light, read back from the GPU. The black
        /// between an atlas's islands is left out of it.
        /// </summary>
        private static Color MeanOf(Texture texture, out int counted)
        {
            counted = 0;
            if (texture == null) return Color.clear;
            Texture2D readable;
            var previous = RenderTexture.active;
            if (texture is RenderTexture target)
            {
                RenderTexture.active = target;
                readable = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false, true);
                readable.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                readable.Apply();
                RenderTexture.active = previous;
            }
            else
            {
                var temporary = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                Graphics.Blit(texture, temporary);
                RenderTexture.active = temporary;
                readable = new Texture2D(temporary.width, temporary.height, TextureFormat.RGBA32, false, true);
                readable.ReadPixels(new Rect(0, 0, temporary.width, temporary.height), 0, 0);
                readable.Apply();
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
            }
            double r = 0, g = 0, b = 0;
            foreach (var pixel in readable.GetPixels())
            {
                if (pixel.r + pixel.g + pixel.b < .03f) continue;
                r += pixel.r; g += pixel.g; b += pixel.b; counted++;
            }
            Object.Destroy(readable);
            if (counted == 0) return Color.clear;
            return new Color((float)(r / counted), (float)(g / counted), (float)(b / counted), 1f);
        }
    }
}
