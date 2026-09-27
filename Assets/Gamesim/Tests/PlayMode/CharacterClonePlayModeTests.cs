using System.Collections;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed class CharacterClonePlayModeTests
    {
        [UnityTest] public IEnumerator AuthoredStyleCloneBuildsOnlyItsOwnIdentity() => CloneAndRebind(false,false);
        [UnityTest] public IEnumerator DeferredCloneDoesNotCopyAnotherProvidersBody() => CloneAndRebind(true,false);
        [UnityTest] public IEnumerator InactiveCloneWaitsForItsOwnIdentityAfterActivation() => CloneAndRebind(true,true);

        private IEnumerator CloneAndRebind(bool deferred,bool inactive)
        {
            var source = new GameObject("Bound template");
            GameObject clone = null;
            var provider = new CloneProvider(deferred);
            CharacterBodySource.Register(provider);
            try
            {
                var person = ContentCatalog.Create(710).Find(ContentCatalog.PlayerId);
                person.appearance = CharacterAppearance.Preset("player");
                var original = CharacterPresentation.Attach(source,person,Color.white);
                var originalVisual = original.VisualRoot;
                var originalPosition = originalVisual.localPosition;
                var appearance = original.AppearanceKey;
                Assert.That(provider.Builds,Is.EqualTo(1));
                if(inactive)source.SetActive(false);
                clone = CharacterPresentation.CloneUnbound(source,null);
                clone.SetActive(true);
                Assert.That(provider.Builds,Is.EqualTo(1),"Cloning collision/labels must not build the template's old identity.");
                Assert.That(clone.transform.Cast<Transform>().Any(t=>t.name=="Gamesim Character Visual"),Is.False);
                Assert.That(original.VisualRoot,Is.SameAs(originalVisual));
                Assert.That(originalVisual.parent,Is.EqualTo(source.transform));
                Assert.That(originalVisual.localPosition,Is.EqualTo(originalPosition));
                Assert.That(original.AppearanceKey,Is.EqualTo(appearance));
                Assert.That(source.activeSelf,Is.EqualTo(!inactive));

                var independent = person.Clone(); independent.id="custom-clone"; independent.isPlayer=false;
                var copied = CharacterPresentation.Attach(clone,independent,Color.cyan);
                yield return null;
                Assert.That(provider.Builds,Is.EqualTo(2));
                Assert.That(copied.CharacterId,Is.EqualTo(independent.id));
                Assert.That(clone.GetComponentsInChildren<CloneBodyMarker>(true),Has.Length.EqualTo(1));
                Assert.That(source.GetComponentsInChildren<CloneBodyMarker>(true),Has.Length.EqualTo(1));

                independent.appearance.activeOutfit="Formal";
                CharacterPresentation.Attach(clone,independent,Color.cyan);
                yield return null;
                Assert.That(provider.Builds,Is.EqualTo(3));
                Assert.That(clone.transform.Cast<Transform>().Count(t=>t.name=="Gamesim Character Visual"),Is.EqualTo(1));
                Assert.That(clone.GetComponentsInChildren<CloneBodyMarker>(true),Has.Length.EqualTo(1));
                Assert.That(original.AppearanceKey,Is.EqualTo(appearance),"Rebinding the copy must not change the source body.");
            }
            finally
            {
                CharacterBodySource.Unregister(provider);
                if(clone!=null)Object.Destroy(clone);
                Object.Destroy(source);
            }
            yield return null;
        }

        private sealed class CloneProvider : ICharacterBodyProvider
        {
            private readonly bool deferred;
            public int Builds;
            public CloneProvider(bool deferred) { this.deferred=deferred; }
            public bool TryCreate(string id,Transform parent,Color color,out CharacterBody body)
            {
                Builds++;
                var root=new GameObject("Owned provider body"); root.transform.SetParent(parent,false);
                root.AddComponent<CloneBodyMarker>();
                body=new CharacterBody(root,null,deferred); return true;
            }
            public void SetWardrobeColor(in CharacterBody body,Color color) { }
        }
    }

    public sealed class CloneBodyMarker : MonoBehaviour { }
}
