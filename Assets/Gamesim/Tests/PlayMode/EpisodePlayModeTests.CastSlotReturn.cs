using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        private IEnumerator PrepareCastSlotReturn()
        {
            yield return OpenCastScreen();
            CastScreen().FontScale = 1.2f;
            yield return null; yield return null;
            var template = CastTemplates.Find("emma-brown");
            yield return CreatorEntrySubmit(template.Category);
            yield return CreatorEntrySubmit(template.Name);
            for (int size = SeasonBuilder.DefaultHouseSize; size < SeasonBuilder.LargestHouse(CastTemplates.Roster.Regular); size++)
                yield return CreatorEntrySubmit("More houseguests");
            foreach (string name in new[] { "Keep Return Guest", "Other Return Guest" })
            {
                var draft = CharacterDraft.FromAppearance(template); draft.Name = name;
                Assert.That(CastScreen().ProfileStore.Save(CharacterProfile.FromDraft(Guid.NewGuid().ToString("N"), draft), out var error), Is.True, error);
            }
            yield return CreatorEntrySubmit("Cast slots");
            for (int slot = 0; slot < 11; slot++) yield return CreatorEntrySubmit("Add Keep Return Guest to the cast");
            CastScreen().GetComponentsInChildren<TMP_InputField>().Single(field => field.name == "Profile name search").text = "Keep";
            yield return CreatorEntrySubmit("Search profiles");
            Assert.That(CastButtons("Add Other Return Guest to the cast"), Is.Empty, "The non-default profile filter is applied.");
            Assert.That(CastGridScroll().content.rect.height, Is.GreaterThan(CastGridScroll().viewport.rect.height), "This route has a real scroll position to preserve.");
        }

        private IEnumerator OpenReturnSlot(Action<float> rememberScroll)
        {
            CastButtons("Edit slot 7").Single().Select();
            yield return null; yield return null;
            rememberScroll(CastGridScroll().content.anchoredPosition.y);
            yield return PressKey(Key.Enter);
            yield return null; yield return null;
            Assert.That(Creator().IsShowing, Is.True);
        }

        private void AssertReturnSlot(float scroll, string name)
        {
            Assert.That(Creator().IsShowing, Is.False);
            Assert.That(CastScreen().IsShowing, Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(CastButtons("Edit slot 7").Single().gameObject),
                "Apply and Back return native keyboard focus to the same slot.");
            Assert.That(CastGridScroll().content.anchoredPosition.y, Is.EqualTo(scroll).Within(1f), "Returning keeps the place in the list.");
            Assert.That(CastScreen().GetComponentsInChildren<TMP_InputField>().Single(field => field.name == "Profile name search").text,
                Is.EqualTo("Keep"), "The profile search survives the trip.");
            Assert.That(CastButtons("Add Other Return Guest to the cast"), Is.Empty);
            var type = typeof(CastSelect);
            Assert.That(type.GetField("category", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(CastScreen()),
                Is.EqualTo(CastTemplates.Find("emma-brown").Category));
            Assert.That(type.GetField("selectedId", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(CastScreen()), Is.EqualTo("emma-brown"));
            Assert.That(CastScreen().GetComponentsInChildren<TMP_Text>().Any(label => label.text == "Slot 7: " + name), Is.True);
            var slot = CastButtons("Edit slot 7").Single().GetComponent<RectTransform>();
            Assert.That(Encloses(ScreenRect(CastGridScroll().viewport), ScreenRect(slot)), Is.True, "The returned focus is visible.");
        }

        [UnityTest]
        public IEnumerator CastSlotReturn_KeyboardApplyReturnsToTheSameVisibleSlotAndKeepsItsFilter()
        {
            yield return PrepareCastSlotReturn();
            float scroll = 0f;
            string session = director.Snapshot.sessionId;
            yield return OpenReturnSlot(value => scroll = value);
            Assert.That(scroll, Is.GreaterThan(0f), "The edited slot was below the initial fold.");
            yield return CreatorEntrySubmit("Identity");
            CreatorEntryName("Applied Return Guest");
            yield return CreatorEntrySubmit(CharacterCreator.ApplySlotCaption);
            AssertReturnSlot(scroll, "Applied Return Guest");
            Assert.That(director.Snapshot.sessionId, Is.EqualTo(session), "Applying an NPC edit never starts a season.");
            // No programmatic selection: Enter must reopen the slot to which Apply returned us.
            yield return PressKey(Key.Enter); yield return null; yield return null;
            Assert.That(Creator().Draft.Name, Is.EqualTo("Applied Return Guest"));
            yield return PressKey(Key.Escape); yield return null; yield return null;
            AssertReturnSlot(scroll, "Applied Return Guest");
        }

        [UnityTest]
        public IEnumerator CastSlotReturn_EscapeAndCancelReturnFocusWithoutApplyingTheDraft()
        {
            yield return PrepareCastSlotReturn();
            float scroll = 0f;
            yield return OpenReturnSlot(value => scroll = value);
            yield return CreatorEntrySubmit("Identity");
            CreatorEntryName("Discarded Escape Guest");
            yield return PressKey(Key.Escape); yield return null; yield return null;
            AssertReturnSlot(scroll, "Keep Return Guest");
            yield return PressKey(Key.Enter); yield return null; yield return null;
            Assert.That(Creator().Draft.Name, Is.EqualTo("Keep Return Guest"));
            yield return CreatorEntrySubmit(CharacterCreator.QuickCaption);
            CreatorEntryName("Discarded Cancel Guest");
            yield return CreatorEntrySubmit(CharacterCreator.CancelSlotCaption);
            AssertReturnSlot(scroll, "Keep Return Guest");
        }
    }
}
