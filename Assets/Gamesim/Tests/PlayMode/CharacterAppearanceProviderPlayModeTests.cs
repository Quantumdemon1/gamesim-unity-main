using System.Collections;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed class CharacterAppearanceProviderPlayModeTests
    {
        private GameObject actor;
        private ModularProvider provider;

        [SetUp]
        public void SetUp()
        {
            actor = new GameObject("Appearance test actor");
            provider = new ModularProvider();
            CharacterBodySource.Register(provider);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            CharacterBodySource.Unregister(provider);
            Object.Destroy(actor);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SelectedTemplateAppearanceIsIndependentOfPlayerIdentityAndPronouns()
        {
            var character = CastTemplates.ToContestant(CastTemplates.Find("emma-brown"), true);
            character.id = ContentCatalog.PlayerId;
            character.pronouns = "they/them";
            character.appearance = CharacterAppearance.Preset("emma-brown");
            CharacterPresentation.Attach(actor, character, Color.magenta);
            yield return null;
            Assert.That(provider.Last.ContestantId, Is.EqualTo(ContentCatalog.PlayerId));
            Assert.That(provider.Last.Appearance.presetId, Is.EqualTo("emma-brown"));
            Assert.That(provider.Last.Purpose, Is.EqualTo(CharacterBuildPurpose.Gameplay));
        }

        [UnityTest]
        public IEnumerator EditingAppearanceOnSameContestantRebuildsAndUnchangedAttachDoesNot()
        {
            var character = ContentCatalog.Create(1).Find(ContentCatalog.PlayerId);
            character.appearance = CharacterAppearance.Preset("player");
            CharacterPresentation.Attach(actor, character, Color.white);
            yield return null;
            CharacterPresentation.Attach(actor, character, Color.white);
            Assert.That(provider.Requests, Is.EqualTo(1));
            character.appearance.colors.Add(new AppearanceColor { id = "Hair", r = .8f, g = .2f, b = .1f });
            CharacterPresentation.Attach(actor, character, Color.white);
            yield return null;
            Assert.That(provider.Requests, Is.EqualTo(2));
            Assert.That(provider.Last.Appearance.colors[0].r, Is.EqualTo(.8f));
            character.appearance.colors[0].r = .1f;
            Assert.That(provider.Last.Appearance.colors[0].r, Is.EqualTo(.8f), "An asynchronous request owns an immutable copy.");
        }

        [Test]
        public void ClosingOneProviderRestoresThePreviousOwner()
        {
            var second = new ModularProvider();
            CharacterBodySource.Register(second);
            Assert.That(CharacterBodySource.Provider, Is.SameAs(second));
            CharacterBodySource.Unregister(second);
            Assert.That(CharacterBodySource.Provider, Is.SameAs(provider));
        }

        private sealed class ModularProvider : IModularCharacterBodyProvider
        {
            public int Requests;
            public CharacterBodyRequest Last;
            public ICharacterAppearanceCatalog Catalog => null;
            public bool TryCreate(in CharacterBodyRequest request, Transform parent, Color color, out CharacterBody body)
            {
                Last = request; Requests++;
                var root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                root.transform.SetParent(parent, false);
                body = new CharacterBody(root, null, false); return true;
            }
            public bool TryCreate(string appearanceId, Transform parent, Color color, out CharacterBody body)
            { Assert.Fail("Modular requests must not infer identity from a parent hierarchy."); body = default; return false; }
            public void SetWardrobeColor(in CharacterBody body, Color color) { }
        }
    }
}
