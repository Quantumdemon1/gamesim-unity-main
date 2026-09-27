using System.Collections;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed class CharacterOutfitPlayModeTests
    {
        [UnityTest]
        public IEnumerator PhaseDressingRebuildsOnlyWhenTheResolvedLookChangesAndDoesNotMutateSavedAppearance()
        {
            var actor = new GameObject("Activity outfit actor");
            var provider = new OutfitProvider();
            CharacterBodySource.Register(provider);
            try
            {
                var person = ContentCatalog.Create(700).Find(ContentCatalog.PlayerId);
                person.appearance = CharacterAppearance.Preset("player");
                foreach (string context in new[] { CharacterOutfits.Everyday, CharacterOutfits.Competition, CharacterOutfits.Formal })
                {
                    var outfit = new CharacterOutfit { id = context };
                    outfit.wardrobe.Add(new AppearanceWardrobe { slot = "Chest", itemId = context + "Shirt" });
                    person.appearance.outfits.Add(outfit);
                }
                string saved = person.appearance.ContentKey();
                var presentation = CharacterPresentation.Attach(actor, CharacterOutfits.ForPhase(person, EpisodePhase.Social), Color.white);
                yield return null;
                CharacterPresentation.Attach(actor, CharacterOutfits.ForPhase(person, EpisodePhase.HoH), Color.white);
                yield return null;
                Assert.That(provider.Outfit, Is.EqualTo(CharacterOutfits.Competition));
                Assert.That(provider.Builds, Is.EqualTo(2));
                CharacterPresentation.Attach(actor, CharacterOutfits.ForPhase(person, EpisodePhase.Veto), Color.white);
                Assert.That(provider.Builds, Is.EqualTo(2), "An unchanged competition look must not rebuild.");
                CharacterPresentation.Attach(actor, CharacterOutfits.ForPhase(person, EpisodePhase.Eviction), Color.white);
                yield return null;
                Assert.That(provider.Outfit, Is.EqualTo(CharacterOutfits.Formal));
                Assert.That(presentation.CharacterId, Is.EqualTo(person.id));
                CharacterPresentation.Attach(actor, CharacterOutfits.ForPhase(person, EpisodePhase.Campaign), Color.white);
                yield return null;
                Assert.That(provider.Outfit, Is.EqualTo(CharacterOutfits.Everyday));
                Assert.That(person.appearance.ContentKey(), Is.EqualTo(saved));
                Assert.That(actor.GetComponentsInChildren<CharacterPresentation>().Length, Is.EqualTo(1));
            }
            finally
            {
                CharacterBodySource.Unregister(provider);
                Object.Destroy(actor);
            }
            yield return null;
        }

        private sealed class OutfitProvider : IModularCharacterBodyProvider
        {
            public int Builds;
            public string Outfit;
            public ICharacterAppearanceCatalog Catalog => null;
            public bool TryCreate(in CharacterBodyRequest request, Transform parent, Color badge, out CharacterBody body)
            {
                Builds++; Outfit = request.Appearance.activeOutfit;
                var mesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                mesh.transform.SetParent(parent, false);
                body = new CharacterBody(mesh, null, false); return true;
            }
            public bool TryCreate(string id, Transform parent, Color color, out CharacterBody body)
            { body = default; return false; }
            public void SetWardrobeColor(in CharacterBody body, Color color) { }
        }
    }
}
