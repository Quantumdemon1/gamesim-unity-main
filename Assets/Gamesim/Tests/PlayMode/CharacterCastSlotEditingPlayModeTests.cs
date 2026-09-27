using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Gamesim.Tests.PlayMode
{
    public sealed class CharacterCastSlotEditingPlayModeTests
    {
        private GameObject owner;

        [UnityTearDown]
        public IEnumerator TearDown() { if (owner != null) Object.Destroy(owner); yield return null; }

        [UnityTest]
        public IEnumerator ApplyingAndCancelingSlotEditsLeavePlayerAndOtherInstancesUntouched()
        {
            owner = new GameObject("Cast edit test");
            var cast = CastSelect.Attach(owner);
            var creator = CharacterCreator.Attach(owner);
            cast.ConfigureCreator(creator);
            SeasonBuilder.Choice committed = null;
            cast.Show(value => committed = value, () => { }, (_, __) => Assert.Fail("An NPC edit must not use the player creator callback."));
            var player = CharacterDraft.FromAppearance(CastTemplates.Find("emma-brown"));
            cast.SetDraft(player);
            var profile = CharacterProfile.FromDraft(Guid.NewGuid().ToString("N"), CharacterDraft.FromAppearance(CastTemplates.Find("alex-chen")));
            // Arrange two explicit instances without touching the user's profile directory.
            var slots = (List<CharacterProfile>)typeof(CastSelect).GetField("customHouseguests", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(cast);
            slots.Add(profile.Clone()); slots.Add(profile.Clone());
            Click(cast, "Cast slots");
            Click(cast, "Edit slot 1");
            Assert.That(creator.IsShowing, Is.True);
            Assert.That(cast.IsShowing, Is.False);
            Assert.That(creator.GetComponentsInChildren<Button>().Any(button => Caption(button) == CharacterCreator.StartCaption), Is.False);
            creator.Draft.Name = "Edited NPC";
            Click(creator, CharacterCreator.ApplySlotCaption);
            yield return null;
            Assert.That(cast.IsShowing, Is.True);
            Assert.That(committed, Is.Null, "Applying an NPC edit does not begin a season.");
            Assert.That(slots[0].name, Is.EqualTo("Edited NPC"));
            Assert.That(slots[1].name, Is.EqualTo(profile.name));
            Assert.That(profile.name, Is.Not.EqualTo("Edited NPC"));
            Click(cast, "Edit slot 1");
            creator.Draft.Name = "Discard this";
            Click(creator, CharacterCreator.CancelSlotCaption);
            yield return null;
            Assert.That(slots[0].name, Is.EqualTo("Edited NPC"));
            Click(cast, CastSelect.StartCaption);
            Assert.That(committed.Authored.Name, Is.EqualTo(player.Name));
            Assert.That(committed.Authored.Appearance.ContentKey(), Is.EqualTo(player.Appearance.ContentKey()));
            Assert.That(committed.CustomHouseguests[0].name, Is.EqualTo("Edited NPC"));
            Assert.That(committed.CustomHouseguests[1].name, Is.EqualTo(profile.name));
        }

        private static string Caption(Button button) => button.GetComponentInChildren<TMP_Text>()?.text;
        private static void Click(Component root, string text)
        {
            var buttons = root.GetComponentsInChildren<Button>().Where(button => button.gameObject.activeInHierarchy && Caption(button) == text).ToArray();
            Assert.That(buttons, Has.Length.EqualTo(1), text);
            buttons[0].onClick.Invoke();
        }
    }
}
