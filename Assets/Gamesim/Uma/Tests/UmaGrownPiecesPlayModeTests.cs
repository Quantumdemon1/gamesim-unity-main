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
    /// Hair and accessories built in code and fitted to a built UMA body: grown where they belong,
    /// in the colour chosen, with finishes of their own; and the skin and hair colours that UMA's
    /// own content draws.
    /// </summary>
    public sealed class UmaGrownPiecesPlayModeTests
    {
        private GameObject cast;
        private readonly List<GameObject> actors = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            // UMA logs a missing sway bone on some hair; that is UMA's, not these pieces'.
            LogAssert.ignoreFailingMessages = true;
            cast = new GameObject("UMA grown provider", typeof(GamesimUmaCast));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var actor in actors) Object.Destroy(actor);
            actors.Clear();
            Object.Destroy(cast);
            LogAssert.ignoreFailingMessages = false;
        }

        private sealed class Built { public CharacterBody Body; public CharacterAppearance Appearance; }

        /// <summary>The player's look wearing <paramref name="wear"/> (catalog ids), built and settled.</summary>
        private IEnumerator Build(Built into, string[] wear, Color? hair = null)
        {
            yield return null;
            var provider = (IModularCharacterBodyProvider)CharacterBodySource.Provider;
            var appearance = provider.Catalog.Materialize(CharacterAppearance.Preset("player"));
            foreach (string id in wear)
            {
                var item = AppearanceEditing.Find(provider.Catalog, id);
                Assert.That(item, Is.Not.Null, id + " is in the catalog.");
                AppearanceEditing.Wear(appearance, item, provider.Catalog);
            }
            if (hair.HasValue) AppearanceEditing.SetColor(appearance, "Hair", hair.Value);
            var actor = new GameObject("Grown actor " + actors.Count);
            actor.transform.position = new Vector3(actors.Count * 3f, 0f, 0f);
            actors.Add(actor);
            Assert.That(provider.TryCreate(new CharacterBodyRequest("player", "player", appearance, CharacterBuildPurpose.Studio, 1),
                actor.transform, Color.white, out var body), Is.True);
            var state = body.Root.GetComponent<CharacterBodyBuildState>();
            double deadline = Time.realtimeSinceStartupAsDouble + 30;
            while (!state.Ready && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(state.Ready, Is.True, "The body finished building.");
            yield return null;
            into.Body = body;
            into.Appearance = appearance;
        }

        private static GrownPiece Piece(CharacterBody body, string id)
        {
            var pieces = body.Root.GetComponentsInChildren<GrownPiece>().Where(piece => piece.ItemId == id).ToArray();
            Assert.That(pieces, Has.Length.EqualTo(1), "Exactly one " + id + " is worn.");
            return pieces[0];
        }

        /// <summary>A renderer's bounds in the body's own frame: x right, y up, z forward, from the body's root.</summary>
        private static Bounds Local(CharacterBody body, Renderer renderer)
        {
            var root = body.Root.transform;
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            var bounds = new Bounds(root.InverseTransformPoint(renderer.transform.TransformPoint(mesh.vertices[0])), Vector3.zero);
            foreach (var vertex in mesh.vertices) bounds.Encapsulate(root.InverseTransformPoint(renderer.transform.TransformPoint(vertex)));
            return bounds;
        }

        /// <summary>The body's live Animator: UMA replaces the one the body was created with as it builds.</summary>
        private static Animator Rig(CharacterBody body) => body.Root.GetComponentInChildren<Animator>();

        private static Vector3 Bone(CharacterBody body, HumanBodyBones bone)
            => body.Root.transform.InverseTransformPoint(Rig(body).GetBoneTransform(bone).position);

        [UnityTest]
        public IEnumerator GrownHair_SitsOnTheHeadInItsShapeAndTakesTheHairColour()
        {
            var colour = new Color(.63f, .3f, .14f);
            var afro = new Built(); yield return Build(afro, new[] { "gs-hair-afro" }, colour);
            var buzz = new Built(); yield return Build(buzz, new[] { "gs-hair-buzz" }, colour);

            var afroPiece = Piece(afro.Body, "gs-hair-afro");
            var buzzPiece = Piece(buzz.Body, "gs-hair-buzz");
            Assert.That(afroPiece.transform.parent, Is.SameAs(Rig(afro.Body).GetBoneTransform(HumanBodyBones.Head)), "Hair hangs from the head, so it follows it.");
            var afroBounds = Local(afro.Body, afroPiece.GetComponent<MeshRenderer>());
            var buzzBounds = Local(buzz.Body, buzzPiece.GetComponent<MeshRenderer>());
            var eyes = (Bone(buzz.Body, HumanBodyBones.LeftEye) + Bone(buzz.Body, HumanBodyBones.RightEye)) * .5f;

            // A buzz cut hugs the scalp; an afro stands well clear of it, up and out.
            Assert.That(buzzBounds.min.y, Is.GreaterThan(eyes.y - .08f), "A buzz cut stays on the scalp, above the jaw.");
            Assert.That(buzzBounds.max.y - eyes.y, Is.InRange(.06f, .2f), "A buzz cut's top is the top of the head.");
            Assert.That(afroBounds.max.y - buzzBounds.max.y, Is.GreaterThan(.04f), "An afro rises well above the scalp.");
            Assert.That(afroBounds.size.x - buzzBounds.size.x, Is.GreaterThan(.06f), "An afro stands out at the sides.");
            Assert.That(afroBounds.min.z, Is.LessThan(buzzBounds.min.z - .03f), "An afro is full at the back.");
            // Rounded out round a point behind the crown, as an afro sits - not a shell grown the
            // same depth all round the head, which reaches as far over the brow as behind it.
            float forward = afroBounds.max.z - buzzBounds.max.z, backward = buzzBounds.min.z - afroBounds.min.z;
            Assert.That(backward - forward, Is.GreaterThan(.035f),
                "An afro is fuller behind than over the brow: " + forward.ToString("0.000") + " m forward, " + backward.ToString("0.000") + " m back.");

            var material = afroPiece.GetComponent<MeshRenderer>().sharedMaterials[0];
            var worn = material.GetColor("_BaseColor");
            Assert.That(Mathf.Abs(worn.r - colour.r) + Mathf.Abs(worn.g - colour.g) + Mathf.Abs(worn.b - colour.b), Is.LessThan(.02f),
                "The grown hair is the colour picked for the hair.");
            Assert.That(afro.Body.Root.GetComponentsInChildren<GrownPiece>().Count(piece => piece.ItemId.StartsWith(ProceduralHair.Prefix)),
                Is.EqualTo(1), "One head of hair, however many times the body was refitted.");
        }

        [UnityTest]
        public IEnumerator Accessories_SitWhereTheyAreWorn()
        {
            var built = new Built();
            yield return Build(built, new[] { "gs-hair-fade", "gs-acc-glasses", "gs-acc-hoops", "gs-acc-chain" });
            var body = built.Body;
            var head = Rig(body).GetBoneTransform(HumanBodyBones.Head);
            var left = Bone(body, HumanBodyBones.LeftEye);
            var right = Bone(body, HumanBodyBones.RightEye);
            var eyes = (left + right) * .5f;

            var glasses = Piece(body, "gs-acc-glasses");
            Assert.That(glasses.transform.parent, Is.SameAs(head));
            var lenses = Local(body, glasses.GetComponent<MeshRenderer>());
            Assert.That(lenses.max.z - eyes.z, Is.InRange(.005f, .06f), "The frames sit in front of the eyes.");
            Assert.That(lenses.min.z, Is.LessThan(eyes.z - .03f), "The temples reach back toward the ears.");
            Assert.That(Mathf.Abs(lenses.center.y - eyes.y), Is.LessThan(.03f), "At the height of the eyes.");
            Assert.That(lenses.size.x, Is.GreaterThan(Mathf.Abs(right.x - left.x) * 1.6f), "Wider than the eyes, reaching the sides of the head.");

            var hoops = Local(body, Piece(body, "gs-acc-hoops").GetComponent<MeshRenderer>());
            Assert.That(hoops.max.y, Is.LessThan(eyes.y - .02f), "Earrings hang from the lobes, below the eyes.");
            Assert.That(hoops.min.y, Is.GreaterThan(eyes.y - .12f), "No lower than the jaw.");
            Assert.That(hoops.size.x, Is.GreaterThan(.1f), "One at each ear.");
            // Across the face itself - the line through the eyes - so a head turned in its idle does not count.
            var leftEye = Rig(body).GetBoneTransform(HumanBodyBones.LeftEye).position;
            var rightEye = Rig(body).GetBoneTransform(HumanBodyBones.RightEye).position;
            float offCentre = Vector3.Dot(Piece(body, "gs-acc-hoops").GetComponent<MeshRenderer>().bounds.center - (leftEye + rightEye) * .5f,
                (rightEye - leftEye).normalized);
            Assert.That(Mathf.Abs(offCentre), Is.LessThan(.01f), "A matched pair, either side of the face: " + offCentre.ToString("0.000") + " m off centre.");

            var chain = Piece(body, "gs-acc-chain");
            var chest = Rig(body).GetBoneTransform(HumanBodyBones.UpperChest) ?? Rig(body).GetBoneTransform(HumanBodyBones.Chest);
            Assert.That(chain.transform.parent, Is.SameAs(chest), "A necklace moves with the chest, not the head.");
            var necklace = Local(body, chain.GetComponent<MeshRenderer>());
            var neck = Bone(body, HumanBodyBones.Neck);
            Assert.That(necklace.max.y, Is.LessThan(neck.y + .05f), "Round the base of the neck.");
            Assert.That(necklace.max.z, Is.GreaterThan(neck.z + .04f), "Draped in front of the neck, not through it.");
            Assert.That(necklace.min.z, Is.LessThan(neck.z - .03f), "And round behind it.");
        }

        [UnityTest]
        public IEnumerator GrownPieces_KeepTheirOwnFinishes()
        {
            var built = new Built();
            yield return Build(built, new[] { "gs-acc-visor" });
            var first = Piece(built.Body, "gs-acc-visor");
            // Rebuilt, as a colour change or a slider rebuilds it: the flattening pass runs again
            // over everything on the body, the visor fitted last time included.
            built.Body.Root.GetComponent<DynamicCharacterAvatar>().UpdateColors(true);
            double deadline = Time.realtimeSinceStartupAsDouble + 20;
            while (Time.realtimeSinceStartupAsDouble < deadline
                   && built.Body.Root.GetComponentsInChildren<GrownPiece>().All(piece => piece == first)) yield return null;
            var refitted = Piece(built.Body, "gs-acc-visor");
            Assert.That(refitted, Is.Not.SameAs(first), "The rebuild refits the visor.");
            var visor = refitted.GetComponent<MeshRenderer>().sharedMaterials[0];
            Assert.That(visor.GetFloat("_Metallic"), Is.GreaterThan(.9f), "The flattening pass leaves a chrome visor chrome.");
            Assert.That(visor.GetFloat("_Smoothness"), Is.GreaterThan(.8f));
        }

        [UnityTest]
        public IEnumerator OnlyBodiesThatWearGrownPiecesKeepAReadableMesh()
        {
            var plain = new Built(); yield return Build(plain, new string[0]);
            var fitted = new Built(); yield return Build(fitted, new[] { "gs-acc-studs" });
            Assert.That(plain.Body.Root.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh.isReadable, Is.False,
                "A body with nothing fitted keeps UMA's memory saving.");
            Assert.That(fitted.Body.Root.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh.isReadable, Is.True,
                "A body with something fitted keeps the copy the fitting reads.");
            Piece(fitted.Body, "gs-acc-studs");
        }

        /// <summary>
        /// UMA's bun draws its hair card in a private white rather than the shared hair colour, so it
        /// stayed silver whatever was picked. It is handed the shared colour as it builds.
        /// </summary>
        [UnityTest]
        public IEnumerator TheBunTakesTheSharedHairColourLikeEveryOtherStyle()
        {
            var built = new Built();
            yield return Build(built, new[] { "Hair_Bun_Recipe" }, new Color(.4f, .2f, .1f));
            var recipe = built.Body.Root.GetComponent<DynamicCharacterAvatar>().umaData.umaRecipe;
            var bun = recipe.slotDataList.FirstOrDefault(slot => slot != null && slot.slotName != null && slot.slotName.Contains("Bun"));
            Assert.That(bun, Is.Not.Null, "The bun is worn.");
            var cards = bun.GetOverlayList().Where(overlay => overlay != null && overlay.overlayName.StartsWith("CardHair")).ToList();
            Assert.That(cards, Is.Not.Empty);
            var hair = recipe.sharedColors.First(colour => colour != null && colour.name == "Hair");
            foreach (var card in cards)
                Assert.That(card.colorData, Is.SameAs(hair), "The bun's card is drawn in the shared hair colour.");
        }

        /// <summary>
        /// A look straight from the library - a cast card's portrait, a houseguest with no saved
        /// outfit - wears its photo's colours too: Casey's shirt is her photo's pale blue.
        /// </summary>
        [UnityTest]
        public IEnumerator APresetLookIsDressedInItsPhotosColours()
        {
            yield return null;
            var provider = (IModularCharacterBodyProvider)CharacterBodySource.Provider;
            var actor = new GameObject("Preset actor");
            actors.Add(actor);
            Assert.That(provider.TryCreate(new CharacterBodyRequest("casey-wilson", "casey-wilson", CharacterAppearance.Preset("casey-wilson"),
                CharacterBuildPurpose.Studio, 1), actor.transform, Color.white, out var body), Is.True);
            var state = body.Root.GetComponent<CharacterBodyBuildState>();
            double deadline = Time.realtimeSinceStartupAsDouble + 30;
            while (!state.Ready && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(state.Ready, Is.True);
            UmaCastLibrary.TryGet("casey-wilson", out var look);
            var shirt = UMAAssetIndexer.Instance.GetAsset<UMAWardrobeRecipe>("tshirt_turquoise_Recipe").PackedLoad().slotsV3
                .Where(part => part != null && !string.IsNullOrEmpty(part.id)).Select(part => part.id).ToArray();
            var overlays = body.Root.GetComponent<DynamicCharacterAvatar>().umaData.umaRecipe.slotDataList
                .Where(slot => slot != null && shirt.Contains(slot.slotName)).SelectMany(slot => slot.GetOverlayList())
                .Where(overlay => overlay != null && !overlay.colorData.IsASharedColor).ToArray();
            Assert.That(overlays, Is.Not.Empty, "Her shirt is on.");
            var blue = look.Fabric["Chest"];
            foreach (var overlay in overlays)
            {
                var worn = overlay.colorData.channelMask[0];
                Assert.That(Mathf.Abs(worn.r - blue.r) + Mathf.Abs(worn.g - blue.g) + Mathf.Abs(worn.b - blue.b), Is.LessThan(.02f), overlay.overlayName);
            }
        }

        /// <summary>
        /// Black hair shines a soft grey, not red: the highlight is taken from the hair, and the
        /// material's own orange-red tint - which put a maroon sheen on every dark head - is gone.
        /// </summary>
        [Test]
        public void BlackHairShinesGreyNotRed()
        {
            var black = CharacterPalettes.Named(CharacterPalettes.Hair, "Jet black");
            var sheen = UmaBodyProvider.HairHighlight(black);
            Assert.That(Mathf.Max(sheen.r, sheen.g, sheen.b) - Mathf.Min(sheen.r, sheen.g, sheen.b), Is.LessThan(.03f), "A neutral sheen on black.");
            Assert.That(sheen.r, Is.InRange(.15f, .45f), "Visible, but soft.");
            var tint = UmaBodyProvider.HairColour(black).GetProperty<UMAColorProperty>("_SpecularTint").Value;
            Assert.That(tint, Is.EqualTo(sheen), "The hair's colour carries the highlight to the material.");
        }

        /// <summary>
        /// UMA draws skin as its albedo times the chosen colour, plus a lift, in linear light. The
        /// colour handed to it is worked out so the albedo's average lands on the swatch picked -
        /// the deepest tones included, which drew a muddy red when passed straight through.
        /// </summary>
        [Test]
        public void SkinColour_DrawsTheSwatchPickedAcrossEveryDepth()
        {
            foreach (var swatch in CharacterPalettes.All(CharacterPalettes.Skin))
            {
                var data = UmaBodyProvider.SkinColour(swatch.Colour);
                for (int channel = 0; channel < 3; channel++)
                {
                    float drawn = UmaBodyProvider.SkinAlbedoMean[channel] * Mathf.GammaToLinearSpace(data.channelMask[0][channel])
                                  + Mathf.GammaToLinearSpace(data.channelAdditiveMask[0][channel]);
                    float target = Mathf.GammaToLinearSpace(swatch.Colour[channel]);
                    Assert.That(drawn, Is.EqualTo(target).Within(.005f), swatch.Name + " channel " + channel);
                }
            }
        }
    }
}
