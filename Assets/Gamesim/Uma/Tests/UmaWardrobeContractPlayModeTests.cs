#if GAMESIM_UMA
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gamesim.Uma.Tests
{
    /// <summary>
    /// What a UMA wardrobe part must meet to be worn in the house: the finish mark a part's own
    /// material may carry to keep its authored look.
    /// </summary>
    [TestFixture]
    public sealed class UmaWardrobeContractPlayModeTests
    {
        private static readonly int Metallic = Shader.PropertyToID("_Metallic"), Smoothness = Shader.PropertyToID("_Smoothness"),
            BumpScale = Shader.PropertyToID("_BumpScale");

        private readonly List<Object> owned = new List<Object>();

        [TearDown]
        public void DestroyOwned()
        {
            foreach (var value in owned) if (value != null) Object.Destroy(value);
            owned.Clear();
        }

        /// <summary>A shiny lit material, as a metal part is authored: metal, polished, with its normal map at full.</summary>
        private Material Shiny(string name, string tag = null, string value = null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null, "The house renders with URP's Lit shader.");
            var material = new Material(shader) { name = name, hideFlags = HideFlags.DontSave };
            owned.Add(material);
            material.SetFloat(Metallic, 1f);
            material.SetFloat(Smoothness, .9f);
            material.SetFloat(BumpScale, 1f);
            if (tag != null) material.SetOverrideTag(tag, value);
            return material;
        }

        [Test]
        public void AMaterialMarkedAuthoredKeepsItsFinishAndTheOthersAreFlattened()
        {
            var marked = Shiny("Marked", UmaStylizer.AuthoredPbrTag, UmaStylizer.AuthoredPbrVersion);
            var plain = Shiny("Plain");
            var wrongValue = Shiny("Wrong value", UmaStylizer.AuthoredPbrTag, "2");
            var wrongName = Shiny("Wrong name", "GamesimAuthoredPbr", UmaStylizer.AuthoredPbrVersion);
            var root = new GameObject("Authored finish part");
            owned.Add(root);
            root.AddComponent<MeshRenderer>().sharedMaterials = new[] { marked, plain, wrongValue, wrongName };

            Assert.That(UmaStylizer.AuthoredPbrTag, Is.EqualTo("GamesimAuthoredPBR"), "The name promised to the part authors.");
            Assert.That(UmaStylizer.AuthoredPbrVersion, Is.EqualTo("1"));
            Assert.That(UmaStylizer.KeepsAuthoredFinish(null), Is.False);
            Assert.That(UmaStylizer.KeepsAuthoredFinish(marked), Is.True);
            foreach (var other in new[] { plain, wrongValue, wrongName })
                Assert.That(UmaStylizer.KeepsAuthoredFinish(other), Is.False, other.name);

            // Twice, as a rebuild runs the pass again over everything on the body.
            UmaStylizer.Apply(root, new Color(.55f, .38f, .28f));
            UmaStylizer.Apply(root, new Color(.55f, .38f, .28f));

            Assert.That(marked.GetFloat(Metallic), Is.EqualTo(1f), "A marked part keeps its metal.");
            Assert.That(marked.GetFloat(Smoothness), Is.EqualTo(.9f).Within(.0001f), "And its polish.");
            Assert.That(marked.GetFloat(BumpScale), Is.EqualTo(1f), "And its normal map.");
            foreach (var other in new[] { plain, wrongValue, wrongName })
            {
                Assert.That(other.GetFloat(Metallic), Is.EqualTo(0f), other.name + " is flattened like any other material.");
                Assert.That(other.GetFloat(Smoothness), Is.LessThanOrEqualTo(.1f), other.name);
                Assert.That(other.GetFloat(BumpScale), Is.EqualTo(0f), other.name);
            }
        }

        /// <summary>
        /// UMA draws a part that is not UseExistingMaterial with a copy of its UMAMaterial's material:
        /// Instantiate, then the shader and CopyPropertiesFromMaterial (UMAGeneratorPro). The mark has
        /// to survive that copy for a NoAtlas part to keep its finish.
        /// </summary>
        [Test]
        public void AnOverrideTagSurvivesTheCopyUmaMakesOfAMaterial()
        {
            var source = Shiny("Authored source", UmaStylizer.AuthoredPbrTag, UmaStylizer.AuthoredPbrVersion);
            var copy = Object.Instantiate(source);
            owned.Add(copy);
            copy.shader = source.shader;
            copy.CopyPropertiesFromMaterial(source);
            Assert.That(copy.GetTag(UmaStylizer.AuthoredPbrTag, false, string.Empty), Is.EqualTo(UmaStylizer.AuthoredPbrVersion));
            Assert.That(UmaStylizer.KeepsAuthoredFinish(copy), Is.True);
        }
    }
}
#endif
