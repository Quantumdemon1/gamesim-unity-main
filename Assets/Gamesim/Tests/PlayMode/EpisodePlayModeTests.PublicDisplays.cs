using System.Collections;
using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator PublicDisplays_RealCompetitionProjectsTheCurrentHoHAndClearsOldPortraits()
        {
            var displays=SceneComponents<HousePublicDisplays>().Single();
            var photos=SceneComponents<Renderer>().Where(r=>r.name==HousePublicDisplays.PhotoName).ToArray();
            Assert.That(photos,Has.Length.EqualTo(4));
            Assert.That(displays.HeadOfHouseholdId,Is.EqualTo(director.Snapshot.hohId));
            Assert.That(displays.YardText,Does.Contain("Week "+director.Snapshot.week));
            Assert.That(photos.All(p=>PublicPhotoTexture(p)==null),Is.True,"No current HoH means no placeholder family is presented as their portrait.");
            for(int guard=0;guard<100 && director.Snapshot.hohId==null;guard++)
            {
                var result=director.Submit(NextCommand(director.Snapshot));
                Assert.That(result.accepted,Is.True,result.reason);
                yield return null;
            }
            var state=director.Snapshot;var hoh=state.Find(state.hohId);
            Assert.That(hoh,Is.Not.Null,"A real competition must commit its HoH.");
            Assert.That(displays.HeadOfHouseholdId,Is.EqualTo(hoh.id));
            Assert.That(displays.HeadOfHouseholdName,Is.EqualTo(hoh.name));
            Assert.That(displays.YardText,Does.Contain(hoh.name));
            foreach(var surface in SceneComponents<Renderer>().Where(r=>r.name==HousePublicDisplays.YardName || r.name==HousePublicDisplays.PlaqueName))
            {
                var label=surface.GetComponentInChildren<TMP_Text>();
                Assert.That(label,Is.Not.Null,surface.name+" must carry a real world-space label.");
                Assert.That(label.text,Does.Contain(hoh.name));
                Assert.That(label.richText || label.raycastTarget,Is.False,"Public names are literal and the display cannot intercept house input.");
            }
            if(CharacterBodySource.Provider is IModularCharacterBodyProvider)
            {
                Texture expected=null;float deadline=Time.realtimeSinceStartup+60;
                while(expected==null && Time.realtimeSinceStartup<deadline){expected=CharacterPortraits.Get(hoh);yield return null;}
                Assert.That(expected,Is.Not.Null,"The current HoH portrait must use the shared appearance pipeline.");
                deadline=Time.realtimeSinceStartup+2;
                while(photos.Any(p=>PublicPhotoTexture(p)!=expected) && Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(photos.All(p=>PublicPhotoTexture(p)==expected),Is.True,"Every HoH frame follows the exact same current person, not a prior winner.");
            }
            else Assert.That(photos.All(p=>PublicPhotoTexture(p)==null),Is.True,"NoUMA draws no incorrect face.");
            var publicReset=state.Clone();publicReset.hohId=null;
            HousePublicDisplays.Project(player.gameObject.scene,publicReset);yield return null;
            Assert.That(displays.HeadOfHouseholdName,Is.Null);
            Assert.That(photos.All(p=>PublicPhotoTexture(p)==null),Is.True,"A week with no HoH must clear the previous portrait immediately.");
            int labels=SceneComponents<TMP_Text>().Count(t=>t.name=="Public text");
            HousePublicDisplays.Project(player.gameObject.scene,publicReset);yield return null;
            Assert.That(SceneComponents<TMP_Text>().Count(t=>t.name=="Public text"),Is.EqualTo(labels),"Repeated projection reuses the actual surfaces.");
            HousePublicDisplays.Project(player.gameObject.scene,state);
        }

        private static Texture PublicPhotoTexture(Renderer renderer)
        {
            var material=renderer.material;
            return material.HasProperty("_BaseMap")?material.GetTexture("_BaseMap")
                :material.HasProperty("_MainTex")?material.GetTexture("_MainTex"):null;
        }
    }
}
