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

        /// <summary>
        /// The player's look - or <paramref name="preset"/>'s - wearing <paramref name="wear"/> (catalog
        /// ids) and without <paramref name="takeOff"/>, built and settled.
        /// </summary>
        private IEnumerator Build(Built into, string[] wear, Color? hair = null, string preset = "player", string[] takeOff = null)
        {
            yield return null;
            var provider = (IModularCharacterBodyProvider)CharacterBodySource.Provider;
            var appearance = provider.Catalog.Materialize(CharacterAppearance.Preset(preset));
            if (takeOff != null)
                foreach (var outfit in appearance.outfits)
                    outfit.wardrobe.RemoveAll(worn => takeOff.Contains(worn.itemId));
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
            // Measured in the pose both were fitted in - standing, as the controller starts - so the
            // two bodies' moments in the idle, each turning its head a little, do not tilt one
            // against the other.
            foreach (var body in new[] { afro.Body, buzz.Body }) { var rig = Rig(body); rig.Rebind(); rig.Update(0f); }

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

        /// <summary>A piece's vertices where they are in the world now.</summary>
        private static Vector3[] World(GrownPiece piece)
        {
            var vertices = piece.GetComponent<MeshFilter>().sharedMesh.vertices;
            var toWorld = piece.transform.localToWorldMatrix;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = toWorld.MultiplyPoint3x4(vertices[i]);
            return vertices;
        }

        /// <summary>
        /// A cap put on over grown hair goes over it. The hair is a mesh of its own, not part of the
        /// body the head was read from, and once the cap sat inside an afro with only its brim
        /// showing. Now the cap is fitted to the head and the afro is held under it.
        /// </summary>
        [UnityTest]
        public IEnumerator Headwear_GoesOverGrownHairNotInsideIt()
        {
            var built = new Built();
            yield return Build(built, new[] { "gs-hair-afro", "gs-acc-cap" });
            var root = built.Body.Root.transform;
            var hair = Local(built.Body, Piece(built.Body, "gs-hair-afro").GetComponent<MeshRenderer>());
            var capPiece = Piece(built.Body, "gs-acc-cap");
            var cap = Local(built.Body, capPiece.GetComponent<MeshRenderer>());
            var crown = World(capPiece).Select(vertex => root.InverseTransformPoint(vertex)).OrderByDescending(vertex => vertex.y).First();

            Assert.That(cap.max.y - hair.max.y, Is.GreaterThan(.002f),
                "The cap's top is above the afro's, not under it: " + (cap.max.y - hair.max.y).ToString("0.000") + " m.");
            Assert.That(hair.Contains(crown), Is.False, "The cap's crown is outside the hair, not buried in it.");
        }

        /// <summary>
        /// A grown afro is drawn from both sides. It is one surface round a hollow over the head,
        /// closed at the hairline by a wall down to the skin: drawn from its outside only, the
        /// inside of that shell - seen from below the brow line, round the temples, past the ears -
        /// was never drawn, and the bare scalp showed through it in notches all along the hairline.
        /// </summary>
        [UnityTest]
        public IEnumerator GrownAfro_IsDrawnFromBothSidesSoNoOpeningShowsTheScalp()
        {
            var built = new Built(); yield return Build(built, new[] { "gs-hair-afro" });
            var afro = Piece(built.Body, "gs-hair-afro");
            Assert.That(afro.GetComponent<MeshRenderer>().sharedMaterials.All(material => material.GetFloat("_Cull") == 0f), Is.True,
                "The afro is drawn from both sides.");
            var crop = new Built(); yield return Build(crop, new[] { "gs-hair-fade" });
            Assert.That(Piece(crop.Body, "gs-hair-fade").GetComponent<MeshRenderer>().sharedMaterials.Any(material => material.GetFloat("_Cull") == 0f), Is.False,
                "A close crop, which has no hollow to see into, is drawn from its outside as before.");
        }

        /// <summary>
        /// A hat is the size of a hat whatever hair is under it. Fitted over everything the hair
        /// carried, a cap stood off big curls like a mushroom, and long hair still came through its
        /// back. It is fitted to the head, with room for hair pressed flat, so over Tyler's curls it
        /// is the cap it is over a buzz cut. (Even stubble is given that room: a hat holds what it
        /// covers a centimetre inside itself, and with no room for hair that is under the skin.)
        /// </summary>
        [UnityTest]
        public IEnumerator Headwear_IsTheSizeOfTheHeadNotOfTheHairUnderIt()
        {
            var curls = new Built(); yield return Build(curls, new[] { "Hair_Poofy", "gs-acc-cap" });
            var crop = new Built(); yield return Build(crop, new[] { "gs-hair-buzz", "gs-acc-cap" });
            var overCurls = Local(curls.Body, Piece(curls.Body, "gs-acc-cap").GetComponent<MeshRenderer>());
            var overCrop = Local(crop.Body, Piece(crop.Body, "gs-acc-cap").GetComponent<MeshRenderer>());
            Assert.That(Mathf.Abs(overCurls.size.x - overCrop.size.x), Is.LessThan(.004f),
                "As wide over curls as over a buzz cut: " + overCurls.size.x.ToString("0.000") + " m against " + overCrop.size.x.ToString("0.000") + " m.");
            Assert.That(Mathf.Abs(overCurls.max.y - overCrop.max.y), Is.LessThan(.004f),
                "As tall over curls as over a buzz cut: " + (overCurls.max.y - overCrop.max.y).ToString("0.000") + " m higher.");
        }

        /// <summary>
        /// Long hair goes under a cap and comes out beneath it, not through it. Every point of every
        /// hair card the cap covers - a card crossing the band included, which held only by its
        /// corners crossed the cloth just above the band - is held well inside it: nearer than half a
        /// centimetre, the cap and the hair fought over which was in front and the hair showed
        /// through in patches. The hair still falls below the cap, and right under the rim it lies close to
        /// the head rather than flaring out over the cap's back.
        /// </summary>
        [UnityTest]
        public IEnumerator UmaHair_GoesUnderAHatAndComesOutBeneathIt()
        {
            var built = new Built(); yield return Build(built, new[] { "gs-acc-cap" }, preset: "vanessa-rousso");
            var shape = built.Body.Root.GetComponentInChildren<HatShape>();
            Assert.That(shape, Is.Not.Null, "The cap knows its own shape.");
            var skin = built.Body.Root.GetComponentsInChildren<SkinnedMeshRenderer>().First(renderer => renderer.GetComponent<GrownPiece>() == null);
            var points = Posed(skin);
            var mesh = skin.sharedMesh;
            var materials = skin.sharedMaterials;
            var hair = Enumerable.Range(0, Mathf.Min(mesh.subMeshCount, materials.Length))
                .Where(s => materials[s] != null && materials[s].name.Contains("Hair_1stPass"))
                .SelectMany(s => mesh.GetTriangles(s)).ToArray();
            Assert.That(hair, Is.Not.Empty, "Her hair is drawn.");
            AssertUnder(shape, points, hair, "Vanessa's hair");
            AssertCloseUnderTheRim(shape, points, hair, "Vanessa's hair");
            int falling = hair.Distinct().Count(i => shape.UnderRim(points[i], out float below, out _) && below > .05f);
            Assert.That(falling, Is.GreaterThan(100), "Her hair still falls well below the cap.");
        }

        /// <summary>
        /// Grown hair goes under a hat too: an afro under a cap, locs under a beanie - every point of
        /// it the hat covers held well inside, and the rest showing below the rim.
        /// </summary>
        [UnityTest]
        public IEnumerator GrownHair_GoesUnderAHatAndComesOutBeneathIt()
        {
            foreach (var (style, hat) in new[] { ("gs-hair-afro", "gs-acc-cap"), ("gs-hair-locs", "gs-acc-beanie") })
            {
                var built = new Built(); yield return Build(built, new[] { style, hat });
                var shape = built.Body.Root.GetComponentInChildren<HatShape>();
                Assert.That(shape, Is.Not.Null, hat + " knows its own shape.");
                int showing = 0;
                foreach (var piece in built.Body.Root.GetComponentsInChildren<GrownPiece>().Where(piece => piece.ItemId == style))
                {
                    var skin = piece.GetComponent<SkinnedMeshRenderer>();
                    var points = skin != null ? Posed(skin) : World(piece);
                    var triangles = piece.Mesh.triangles;
                    AssertUnder(shape, points, triangles, style + " (" + piece.name + ")");
                    AssertCloseUnderTheRim(shape, points, triangles, style + " (" + piece.name + ")");
                    showing += points.Count(point => shape.UnderRim(point, out _, out _));
                }
                Assert.That(showing, Is.GreaterThan(0), style + " shows below the " + hat + ".");
            }
        }

        /// <summary>Every point across every triangle the hat covers is well inside it: corners, edges and middles.</summary>
        private static void AssertUnder(HatShape shape, Vector3[] points, int[] triangles, string what)
        {
            int covered = 0;
            float worst = float.PositiveInfinity;
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                Vector3 a = points[triangles[t]], b = points[triangles[t + 1]], c = points[triangles[t + 2]];
                for (int u = 0; u <= 4; u++)
                    for (int w = 0; w <= 4 - u; w++)
                    {
                        float clearance = shape.Clearance(a + (b - a) * (u / 4f) + (c - a) * (w / 4f));
                        if (float.IsPositiveInfinity(clearance)) continue;
                        covered++;
                        worst = Mathf.Min(worst, clearance);
                    }
            }
            Assert.That(covered, Is.GreaterThan(0), what + ": some of it is under the hat.");
            // Well inside: nearer than about half a centimetre, the hat and the hair fought over which
            // was in front, and the hair showed through in patches.
            Assert.That(worst, Is.GreaterThan(.005f),
                what + ": everything under the hat is well inside it, not through it: the nearest is " + worst.ToString("0.0000") + " m inside.");
        }

        /// <summary>
        /// Below the band, hair stands out across the head past the rim no faster than it falls - 0.7
        /// of a metre per metre, fanning out from close under the rim - rather than flaring out over
        /// the hat.
        /// </summary>
        private static void AssertCloseUnderTheRim(HatShape shape, Vector3[] points, int[] triangles, string what)
        {
            float worst = float.NegativeInfinity, worstBelow = 0f, worstPast = 0f;
            foreach (int i in triangles.Distinct())
            {
                if (!shape.UnderRim(points[i], out float below, out float past) || below > .1f) continue;
                float over = past - (.7f * below + .002f);
                if (over > worst) { worst = over; worstBelow = below; worstPast = past; }
            }
            Assert.That(worst, Is.LessThan(0f),
                what + ": below the rim it fans out no faster than it falls: " + worstPast.ToString("0.000") + " m past the rim, "
                + worstBelow.ToString("0.000") + " m below it.");
        }

        /// <summary>
        /// Long hair hangs as long under a hat as without one. Held within a set distance of the
        /// head's middle, hair hanging straight down past the shoulders was out of reach however it
        /// hung and was drawn up into the neck: Vanessa's under her cap by a hand's width, box braids
        /// by twenty centimetres. Below the band hair is only ever drawn in across the head, so
        /// nothing that hangs below the rim is lifted. The cap is put on in the frame the hair is
        /// read in, so the hair is read in the same pose either way.
        /// </summary>
        [UnityTest]
        public IEnumerator LongHair_HangsAsLongUnderAHatAsWithout()
        {
            // Vanessa keeps her studs, and with them a mesh the cap can hold her hair in.
            foreach (var (preset, wear, what) in new[]
            {
                ("vanessa-rousso", new[] { "gs-acc-studs" }, "Vanessa's hair"),
                (ContentCatalog.PlayerId, new[] { "gs-hair-braids" }, "Box braids"),
            })
            {
                var built = new Built(); yield return Build(built, wear, preset: preset, takeOff: new[] { "gs-acc-cap" });
                var body = built.Body;
                Assert.That(body.Root.GetComponentInChildren<HatShape>(), Is.Null, what + ": no cap yet.");
                var up = body.Root.transform.up;
                var before = HairPoints(body);
                ProceduralAccessories.Dress(body.Root, wear.Where(ProceduralAccessories.IsProcedural).Append("gs-acc-cap"));
                var shape = body.Root.GetComponentInChildren<HatShape>();
                Assert.That(shape, Is.Not.Null, what + ": the cap is on.");
                var after = HairPoints(body);
                Assert.That(after, Has.Length.EqualTo(before.Length), what + ": the same hair either way.");
                Assert.That(Enumerable.Range(0, before.Length).Count(i => after[i] != before[i]), Is.GreaterThan(0), what + ": the cap holds some of it.");
                int hanging = 0;
                float lifted = 0f;
                for (int i = 0; i < before.Length; i++)
                {
                    if (!shape.UnderRim(before[i], out float below, out _) || below < .02f) continue;
                    hanging++;
                    lifted = Mathf.Max(lifted, Vector3.Dot(after[i] - before[i], up));
                }
                Assert.That(hanging, Is.GreaterThan(100), what + " hangs well below the cap.");
                Assert.That(lifted, Is.LessThan(.002f), what + ": nothing that hangs below the cap is lifted: " + lifted.ToString("0.000") + " m at most.");
            }
        }

        /// <summary>
        /// Hair held under a hat stays in one piece. A grown shell gives each of its triangles
        /// corners of its own, so every point of it is there once for each triangle it is a corner
        /// of; a copy held one way for one triangle and another way for the next tore the shell open
        /// all round just under the band. Every copy of a point is held alike.
        /// </summary>
        [UnityTest]
        public IEnumerator HairHeldUnderAHat_StaysInOnePiece()
        {
            foreach (string style in new[] { "gs-hair-afro", "gs-hair-coils" })
            {
                var built = new Built(); yield return Build(built, new[] { style });
                var piece = Piece(built.Body, style);
                var before = World(piece);
                var copies = Enumerable.Range(0, before.Length).GroupBy(i => Vector3Int.RoundToInt(before[i] * 20000f))
                    .Select(group => group.ToArray()).Where(group => group.Length > 1).ToArray();
                Assert.That(copies, Is.Not.Empty, style + ": its points are shared by triangles, each with a copy of its own.");
                ProceduralAccessories.Dress(built.Body.Root, new[] { "gs-acc-cap" });
                var after = World(piece);
                Assert.That(Enumerable.Range(0, before.Length).Count(i => after[i] != before[i]), Is.GreaterThan(0), style + ": the cap holds some of it.");
                float torn = copies.Max(group => group.Max(i => Vector3.Distance(after[i], after[group[0]])));
                Assert.That(torn, Is.LessThan(.0002f), style + ": every copy of a point is held alike: " + torn.ToString("0.0000") + " m apart at most.");
            }
        }

        /// <summary>Where every point of the hair a body wears is now: UMA's hair cards, and hair grown on the head.</summary>
        private static Vector3[] HairPoints(CharacterBody body)
        {
            var points = new List<Vector3>();
            foreach (var skin in body.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var piece = skin.GetComponent<GrownPiece>();
                if (piece != null && piece.name != ProceduralHair.StrandsName) continue;
                var posed = Posed(skin);
                if (piece != null) { points.AddRange(posed); continue; }
                var mesh = skin.sharedMesh;
                var materials = skin.sharedMaterials;
                points.AddRange(Enumerable.Range(0, Mathf.Min(mesh.subMeshCount, materials.Length))
                    .Where(s => materials[s] != null && materials[s].name.Contains("Hair_1stPass"))
                    .SelectMany(s => mesh.GetTriangles(s)).Distinct().OrderBy(i => i).Select(i => posed[i]));
            }
            foreach (var piece in body.Root.GetComponentsInChildren<GrownPiece>())
                if (piece.name == ProceduralHair.RootName && piece.GetComponent<MeshFilter>() != null) points.AddRange(World(piece));
            return points.ToArray();
        }

        /// <summary>
        /// A cap over locs goes over their roots too. The strands are a piece of their own, shared
        /// with the neck, and only the part that turns wholly with the head is what a cap has to
        /// clear; left out, the cap was fitted to the base under them and the roots came through it.
        /// </summary>
        [UnityTest]
        public IEnumerator Headwear_GoesOverTheRootsOfLongStrands()
        {
            var built = new Built();
            yield return Build(built, new[] { "gs-hair-locs", "gs-acc-cap" });
            var root = built.Body.Root.transform;
            var strands = built.Body.Root.GetComponentsInChildren<GrownPiece>().Single(piece => piece.name == ProceduralHair.StrandsName);
            float roots = Posed(strands.GetComponent<SkinnedMeshRenderer>()).Max(vertex => root.InverseTransformPoint(vertex).y);
            var cap = Local(built.Body, Piece(built.Body, "gs-acc-cap").GetComponent<MeshRenderer>());
            Assert.That(cap.max.y - roots, Is.GreaterThan(.002f),
                "The cap's top is above the locs' roots, not under them: " + (cap.max.y - roots).ToString("0.000") + " m.");
        }

        /// <summary>
        /// A rebuild grows the same locs in the same places. Every rebuild refits them - a colour
        /// tried in the creator, a slider moved - and the head it reads again welds a few of its
        /// points differently each time: strands rooted by index into those points were dealt
        /// somewhere new each time, and the style reshuffled under the player's eyes. Read in the
        /// head's own space, where the body's pose cannot move it, what is wholly the head's of the
        /// strands comes back where it was - each point matched to the nearest again, so a strand
        /// rooted at the very edge of the scalp that grows one time and not the next moves no other.
        /// </summary>
        [UnityTest]
        public IEnumerator ARebuildGrowsTheSameLocsInTheSamePlaces()
        {
            var built = new Built();
            yield return Build(built, new[] { "gs-hair-locs" });
            GrownPiece Strands() => built.Body.Root.GetComponentsInChildren<GrownPiece>().Single(piece => piece.name == ProceduralHair.StrandsName);
            // The part of every strand that is wholly the head's, where the head holds it.
            Vector3[] OnTheHead(GrownPiece piece)
            {
                var mesh = piece.Mesh;
                var weights = mesh.boneWeights;
                var vertices = mesh.vertices;
                var toHead = mesh.bindposes[0];
                return Enumerable.Range(0, weights.Length)
                    .Where(i => weights[i].boneIndex0 == 0 && weights[i].weight0 > .999f)
                    .Select(i => toHead.MultiplyPoint3x4(vertices[i])).ToArray();
            }
            var first = Strands();
            var before = OnTheHead(first);
            Assert.That(before, Is.Not.Empty, "The strands have roots on the head.");

            built.Body.Root.GetComponent<DynamicCharacterAvatar>().UpdateColors(true);
            double deadline = Time.realtimeSinceStartupAsDouble + 20;
            while (Time.realtimeSinceStartupAsDouble < deadline && Strands() == first) yield return null;
            var refitted = Strands();
            Assert.That(refitted, Is.Not.SameAs(first), "The rebuild refits the locs.");
            var after = OnTheHead(refitted);

            Assert.That(after, Is.Not.Empty, "The strands grow again.");
            var offsets = before.Select(point => after.Min(other => Vector3.Distance(point, other))).OrderBy(d => d).ToArray();
            float typical = offsets[(int)(offsets.Length * .95f)];
            Assert.That(typical, Is.LessThan(.002f), "Each strand roots where it rooted before: 5% of the points are more than " + typical.ToString("0.0000") + " m from any grown again.");
        }

        /// <summary>
        /// Locs and a cap refitted with the head turned come out as they would have fitted straight
        /// on. A body is refitted after every rebuild - a colour tried, a change of clothes - and its
        /// head may be turned when it happens: the strands were built swung round with it, and their
        /// ends stayed swung once it came back; the cap was fitted to where the strands' roots would
        /// be had the head not turned. (A houseguest's own refits are made with the body standing,
        /// which takes a look's nod off with the rest of the pose:
        /// UmaBodyTintPlayModeTests.ARebuildMidLookFitsLocsToTheHeadAsItRests.)
        /// </summary>
        [UnityTest]
        public IEnumerator LongStrandsAndACapRefittedWithTheHeadTurnedFitAsTheyWouldStraightOn()
        {
            var built = new Built();
            yield return Build(built, new[] { "gs-hair-locs", "gs-acc-cap" });
            var body = built.Body;
            var root = body.Root.transform;
            var head = Rig(body).GetBoneTransform(HumanBodyBones.Head);
            var rest = head.localRotation;
            var colour = CharacterPalettes.Named(CharacterPalettes.Hair, "Soft black");
            void Refit()
            {
                // As UmaBodyTint refits after a rebuild: the hair, then what goes over it.
                ProceduralHair.Grow(body.Root, "gs-hair-locs", colour);
                ProceduralAccessories.Dress(body.Root, new[] { "gs-acc-cap" });
            }
            Vector3[] Strands() => Posed(body.Root.GetComponentsInChildren<GrownPiece>()
                .Single(piece => piece.name == ProceduralHair.StrandsName).GetComponent<SkinnedMeshRenderer>());
            Vector3[] Cap()
            {
                var cap = Piece(body, "gs-acc-cap");
                return cap.Mesh.vertices.Select(vertex => head.InverseTransformPoint(cap.transform.TransformPoint(vertex))).ToArray();
            }
            float Farthest(Vector3[] a, Vector3[] b) => Enumerable.Range(0, a.Length).Max(i => Vector3.Distance(a[i], b[i]));

            // Both fits in one frame, so the two read the same body but for the turn.
            Refit();
            var straightStrands = Strands();
            var straightCap = Cap();
            head.rotation = Quaternion.AngleAxis(CharacterPresentation.LookYawLimit, root.up) * head.rotation;
            Refit();
            head.localRotation = rest;
            var lookStrands = Strands();
            var lookCap = Cap();

            Assert.That(lookStrands, Has.Length.EqualTo(straightStrands.Length), "The same strands are grown either way.");
            Assert.That(lookCap, Has.Length.EqualTo(straightCap.Length), "The same cap is made either way.");
            // The style comes back as it was: all but a few points where they were, and none far off.
            // Skin with a trace of the neck in it shifts a little under a turned head, and a strand
            // rooted there may drift; a style swung round, nodded or reshuffled moves nearly all of them.
            var strandOffsets = Enumerable.Range(0, straightStrands.Length).Select(i => Vector3.Distance(straightStrands[i], lookStrands[i])).OrderBy(d => d).ToArray();
            float typical = strandOffsets[(int)(strandOffsets.Length * .95f)], strandsOff = strandOffsets[strandOffsets.Length - 1];
            Assert.That(typical, Is.LessThan(.002f),
                "Refitted with the head turned and the head brought back, the strands hang as they would fitted straight on: 5% are more than " + typical.ToString("0.0000") + " m off.");
            Assert.That(strandsOff, Is.LessThan(.03f), "and none is far off: " + strandsOff.ToString("0.000") + " m.");
            float capOff = Farthest(straightCap, lookCap);
            Assert.That(capOff, Is.LessThan(.003f),
                "Refitted with the head turned, the cap sits on the head as it would fitted straight on: " + capOff.ToString("0.000") + " m off.");
        }

        /// <summary>
        /// Locs and box braids are whole strands shared between the head and the neck. All on the
        /// head, a head turned to face someone swung them round through the shoulders; hung from the
        /// neck below the chin, the same turn parted them there, the part on the head ending at the
        /// jaw like a curtain cut straight across. Turned as far as a look turns the head, their
        /// roots turn with it, what rests on the shoulders stays there, and in between they bend
        /// rather than part. Built whole - every vertex a number - and lying over the shoulders and back at
        /// rest, not through them.
        /// </summary>
        [UnityTest]
        public IEnumerator LongStrands_BendFromTheHeadToTheNeckSoATurnedHeadNeitherSwingsNorPartsThem()
        {
            foreach (string id in new[] { "gs-hair-locs", "gs-hair-braids" })
            {
                var built = new Built();
                yield return Build(built, new[] { id });
                var body = built.Body;
                var root = body.Root.transform;
                var rig = Rig(body);
                var head = rig.GetBoneTransform(HumanBodyBones.Head);
                var chestBone = rig.GetBoneTransform(HumanBodyBones.UpperChest) ?? rig.GetBoneTransform(HumanBodyBones.Chest);
                var neckBone = rig.GetBoneTransform(HumanBodyBones.Neck) ?? chestBone;
                var pieces = body.Root.GetComponentsInChildren<GrownPiece>().Where(piece => piece.ItemId == id).ToArray();
                var onHead = pieces.Where(piece => piece.name == ProceduralHair.RootName).ToArray();
                var strands = pieces.Where(piece => piece.name == ProceduralHair.StrandsName).ToArray();
                Assert.That(onHead, Has.Length.EqualTo(1), id + ": one piece on the head.");
                Assert.That(strands, Has.Length.EqualTo(1), id + ": one piece of strands.");
                Assert.That(onHead[0].transform.parent, Is.SameAs(head), id + ": the base rides on the head.");
                var skin = strands[0].GetComponent<SkinnedMeshRenderer>();
                Assert.That(skin, Is.Not.Null, id + ": the strands are skinned, not carried by one bone.");
                Assert.That(skin.bones, Has.Length.EqualTo(2), id + ": between two bones.");
                Assert.That(skin.bones[0], Is.SameAs(head), id + ": the head,");
                Assert.That(skin.bones[1], Is.SameAs(neckBone), id + ": and the neck.");
                foreach (var piece in pieces)
                    Assert.That(piece.Mesh.vertices.All(Finite), Is.True, id + ": every vertex of " + piece.name + " is a number.");

                // The shoulders and upper back, drawn tighter than the body itself: a strand inside
                // this is in the body.
                var chest = root.InverseTransformPoint(chestBone.position);
                float shoulders = Mathf.Abs(Bone(body, HumanBodyBones.RightUpperArm).x - Bone(body, HumanBodyBones.LeftUpperArm).x) * .5f;
                var torso = new Vector3(shoulders, .1f, .1f);
                var rest = Posed(skin);
                int inside = rest.Count(vertex =>
                {
                    var d = root.InverseTransformPoint(vertex) - chest;
                    return (d.x / torso.x) * (d.x / torso.x) + (d.y / torso.y) * (d.y / torso.y) + (d.z / torso.z) * (d.z / torso.z) < 1f;
                });
                Assert.That(inside, Is.EqualTo(0), id + ": at rest the strands lie over the shoulders and back, not through them.");

                var weights = strands[0].Mesh.boneWeights;
                Assert.That(weights, Has.Length.EqualTo(rest.Length), id + ": every vertex is weighted.");
                float HeadShare(BoneWeight w) => w.boneIndex0 == 0 ? w.weight0 : w.boneIndex1 == 0 ? w.weight1 : 0f;
                var roots = Enumerable.Range(0, rest.Length).Where(i => HeadShare(weights[i]) > .999f).ToArray();
                var necks = Enumerable.Range(0, rest.Length).Where(i => HeadShare(weights[i]) < .001f).ToArray();
                // What lies down the back and over the shoulders, a hand below the shoulder line.
                // Hair turns with the head it grows from - short locs, ending about the shoulders,
                // follow it in part - and only what rests on the body should hang on; long braids
                // always reach that far.
                float shoulderY = (Bone(body, HumanBodyBones.LeftUpperArm).y + Bone(body, HumanBodyBones.RightUpperArm).y) * .5f - .05f;
                var resting = Enumerable.Range(0, rest.Length).Where(i => root.InverseTransformPoint(rest[i]).y < shoulderY).ToArray();
                Assert.That(roots, Is.Not.Empty, id + ": the strands' roots are the head's.");
                if (id == "gs-hair-braids") Assert.That(resting, Is.Not.Empty, id + ": long braids reach down the back.");

                // Turned to face someone, as far as a look turns the head.
                head.rotation = Quaternion.AngleAxis(CharacterPresentation.LookYawLimit, root.up) * head.rotation;
                var turned = Posed(skin);
                float rootsMoved = roots.Max(i => Vector3.Distance(rest[i], turned[i]));
                Assert.That(rootsMoved, Is.GreaterThan(.03f), id + ": the head turned, and the roots with it.");
                if (resting.Length > 0)
                {
                    float restingMoved = resting.Max(i => Vector3.Distance(rest[i], turned[i]));
                    Assert.That(restingMoved, Is.LessThan(rootsMoved / 4f), id + ": what rests on the shoulders and back stays there rather than swinging round with the head: "
                        + restingMoved.ToString("0.000") + " m against the roots' " + rootsMoved.ToString("0.000") + " m.");
                }
                if (necks.Length > 0)
                {
                    float necksMoved = necks.Max(i => Vector3.Distance(rest[i], turned[i]));
                    Assert.That(necksMoved, Is.LessThan(.001f), id + ": what is wholly the neck's stays where it hangs: " + necksMoved.ToString("0.000") + " m moved.");
                }

                // Bent, not parted: no edge of any strand drawn out to three times its length at rest.
                var triangles = strands[0].Mesh.triangles;
                float stretch = 0f;
                for (int t = 0; t + 2 < triangles.Length; t += 3)
                    for (int e = 0; e < 3; e++)
                    {
                        int a = triangles[t + e], b = triangles[t + (e + 1) % 3];
                        float before = Vector3.Distance(rest[a], rest[b]);
                        if (before > 1e-5f) stretch = Mathf.Max(stretch, Vector3.Distance(turned[a], turned[b]) / before);
                    }
                Assert.That(stretch, Is.LessThan(3f), id + ": the strands bend between head and neck rather than part: an edge drawn out " + stretch.ToString("0.0") + " times.");
            }
        }

        /// <summary>A skinned piece's vertices where the bones hold them now, in the world.</summary>
        private static Vector3[] Posed(SkinnedMeshRenderer skin)
        {
            var baked = new Mesh();
            skin.BakeMesh(baked, true);
            var vertices = baked.vertices;
            Object.Destroy(baked);
            var toWorld = skin.transform.localToWorldMatrix;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = toWorld.MultiplyPoint3x4(vertices[i]);
            return vertices;
        }

        private static bool Finite(Vector3 v)
            => !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z)
               && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);

        /// <summary>
        /// A bow tie sits in the middle of the chest whatever the head was doing when it was fitted.
        /// Built in the head's axes, a refit with the head turned - a colour change mid-conversation -
        /// left it turned on the collar and off to one side for good.
        /// </summary>
        [UnityTest]
        public IEnumerator ABowTie_SitsOnTheChestsMidlineWhenFittedWithTheHeadTurned()
        {
            var built = new Built();
            yield return Build(built, new[] { "gs-acc-bowtie" });
            var body = built.Body;
            var root = body.Root.transform;
            var head = Rig(body).GetBoneTransform(HumanBodyBones.Head);
            head.rotation = Quaternion.AngleAxis(20f, root.up) * head.rotation;
            Assert.That(ProceduralAccessories.Dress(body.Root, new[] { "gs-acc-bowtie" }), Is.EqualTo(1), "The bow tie is refitted.");

            var piece = Piece(body, "gs-acc-bowtie");
            var chest = Rig(body).GetBoneTransform(HumanBodyBones.UpperChest) ?? Rig(body).GetBoneTransform(HumanBodyBones.Chest);
            Assert.That(piece.transform.parent, Is.SameAs(chest), "A bow tie moves with the chest, not the head.");
            var bow = Local(body, piece.GetComponent<MeshRenderer>());
            var midline = (Bone(body, HumanBodyBones.LeftUpperArm) + Bone(body, HumanBodyBones.RightUpperArm)) * .5f;
            float offCentre = bow.center.x - midline.x;
            Assert.That(Mathf.Abs(offCentre), Is.LessThan(.01f),
                "The bow sits on the middle of the chest, between the shoulders: " + offCentre.ToString("0.000") + " m off centre.");
        }

        /// <summary>
        /// Hair painted for one colour over one skin goes once no head wears it: trying colours in
        /// the creator painted new textures with every pick, and none were ever let go. Only the
        /// last few let go are kept, for a look changed back; colours tried a dozen picks ago are
        /// gone. What is still worn stays - through a refit in the same colour, too.
        /// </summary>
        [UnityTest]
        public IEnumerator PaintedHair_IsLetGoWhenNoHeadWearsItAnyMore()
        {
            var built = new Built();
            yield return Build(built, new[] { "gs-hair-buzz" });
            var body = built.Body.Root;
            var skin = new Color(.55f, .38f, .27f);
            const int picks = 12;
            var tried = new List<Material>();
            var painted = new List<Texture>();
            GameObject worn = null;
            Color colour = Color.black;
            for (int i = 0; i < picks; i++)
            {
                var previous = worn;
                colour = Color.HSVToRGB(i / (float)picks, .55f, .45f);
                worn = ProceduralHair.Grow(body, "gs-hair-buzz", colour, skin);
                Assert.That(worn, Is.Not.Null, "The buzz cut grows in colour " + i + ".");
                // A buzz cut is one band, drawn with a texture painted for its colour. Its texture is
                // read now: by the end a colour tried early on is gone, and a material that is gone
                // cannot be asked what it drew with - nor told apart from another that is gone.
                var band = worn.GetComponent<MeshRenderer>().sharedMaterials[0];
                var paint = band.GetTexture("_BaseMap");
                Assert.That(paint != null && !painted.Any(earlier => ReferenceEquals(earlier, paint)), Is.True,
                    "Colour " + i + " painted a texture of its own.");
                tried.Add(band);
                painted.Add(paint);
                // One pick at a time, as a player makes them: the hair replaced goes before the next.
                double settle = Time.realtimeSinceStartupAsDouble + 5;
                while (previous != null && Time.realtimeSinceStartupAsDouble < settle) yield return null;
                Assert.That(previous == null, Is.True, "The hair replaced by colour " + i + " has gone.");
            }
            var current = worn.GetComponent<MeshRenderer>().sharedMaterials;
            // The first half of the picks: long enough ago that nothing should keep them.
            var staleMaterials = tried.Take(picks / 2).ToList();
            var staleTextures = painted.Take(picks / 2).ToList();
            Assert.That(staleMaterials.Any(material => current.Contains(material)), Is.False);
            // The pick just before is still kept, textures and all, for a look changed back.
            Assert.That(tried[picks - 2] != null && painted[picks - 2] != null, Is.True,
                "The colour picked last before this one is kept a little while.");

            double deadline = Time.realtimeSinceStartupAsDouble + 5;
            while ((staleMaterials.Any(material => material != null) || staleTextures.Any(texture => texture != null))
                   && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(staleTextures.Count(texture => texture != null), Is.EqualTo(0), "Textures painted for colours tried long ago are let go.");
            Assert.That(staleMaterials.Count(material => material != null), Is.EqualTo(0), "And their materials.");
            Assert.That(current.All(material => material != null && material.GetTexture("_BaseMap") != null), Is.True,
                "The hair still worn keeps its materials and textures.");

            // Refitted in the colour it wears, as every rebuild does: the old piece goes as the new
            // one takes the same materials up, and they stay.
            var replaced = worn;
            worn = ProceduralHair.Grow(body, "gs-hair-buzz", colour, skin);
            deadline = Time.realtimeSinceStartupAsDouble + 5;
            while (replaced != null && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(replaced == null, Is.True, "The old head of hair has gone.");
            var refitted = worn.GetComponent<MeshRenderer>().sharedMaterials;
            Assert.That(refitted.All(material => current.Contains(material)), Is.True, "The refit wears the same materials.");
            Assert.That(refitted.All(material => material != null && material.GetTexture("_BaseMap") != null), Is.True,
                "A refit in the same colour keeps them, textures and all.");
        }
    }
}
