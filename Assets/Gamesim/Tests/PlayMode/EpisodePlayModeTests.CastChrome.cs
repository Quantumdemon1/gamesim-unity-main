using System.Collections;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// V2's cast chrome: the rail as portrait chips (mockup-01, -10) and the cast screen as the
    /// mockup-02 card grid.
    ///
    /// <para>The mood is drawn twice on a chip on purpose — a face glyph in the mood's colour and
    /// the mood as a word — because only the word is a state a screen reader can announce. These
    /// tests hold both halves, and hold the parts the rail already had: the standing badge, and the
    /// portrait that is a button.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator CastRail_ChipsCarryAMoodFaceAndAMoodWordInTheMoodsColour()
        {
            director.ClosePanels();
            yield return null;
            Canvas.ForceUpdateCanvases();

            var state = director.Snapshot;
            var rail = director.GetComponentsInChildren<RectTransform>(true)
                .First(rect => rect.name == CastRail.RootName);
            var entries = rail.Cast<Transform>().ToArray();
            Assert.That(entries.Length, Is.EqualTo(state.contestants.Count),
                "One chip per houseguest, evicted included.");

            foreach (var actor in state.contestants)
            {
                var entry = entries.FirstOrDefault(child => child.name == actor.name);
                Assert.That(entry, Is.Not.Null, actor.name + " has no chip on the rail.");

                Assert.That(entry.Find(CastRail.ChipName), Is.Not.Null,
                    actor.name + "'s chip has no glass ground.");
                Assert.That(entry.GetComponent<Button>(), Is.Not.Null,
                    actor.name + "'s chip must still be the control that follows them.");

                var word = entry.GetComponentsInChildren<TMP_Text>(true)
                    .FirstOrDefault(label => label.name == CastRail.MoodWordName);
                Assert.That(word, Is.Not.Null, actor.name + "'s chip has no mood word.");

                // The rail's own reckoning, not a second copy of it: a winner and a runner-up are
                // inactive and are not out.
                bool gone = CastRail.IsOut(actor);
                string expected = gone ? "Evicted"
                    : string.IsNullOrEmpty(actor.mood) ? "Neutral" : actor.mood;
                Assert.That(word.text, Is.EqualTo(expected),
                    actor.name + "'s chip must say the mood the simulation holds, in one word.");

                var moodColour = gone ? UiTheme.Muted : RelationshipWeb.MoodColour(actor.mood);
                Assert.That(SameColour(word.color, moodColour), Is.True,
                    "The word is drawn in the mood's own colour: " + word.color + " is not " + moodColour + ".");

                // The glyph sits on the portrait's shoulder; the face inside it carries the colour.
                var badge = entry.GetComponentsInChildren<RectTransform>(true)
                    .FirstOrDefault(rect => rect.name == CastRail.MoodGlyphName);
                Assert.That(badge, Is.Not.Null, actor.name + "'s chip has no mood face.");
                var face = badge.GetComponentsInChildren<Image>(true)
                    .FirstOrDefault(image => image.name == "Face");
                Assert.That(face, Is.Not.Null, "The mood face is drawn, as a glyph or as a pip.");
                Assert.That(SameColour(face.color, moodColour), Is.True,
                    "The mood face is drawn in the mood's own colour: " + face.color + " is not " + moodColour + ".");
            }

            // The standing badge the rail has always carried is still there, and still says YOU for
            // the player when they hold nothing else this week.
            var you = entries.First(child => child.name == state.Find(state.playerId).name);
            var badges = you.GetComponentsInChildren<RectTransform>(true)
                .Where(rect => rect.name == CastRail.BadgeName)
                .ToArray();
            Assert.That(badges, Has.Length.EqualTo(1), "The player's chip keeps exactly one standing badge.");
            string standing = badges[0].GetComponentInChildren<TMP_Text>(true).text;
            Assert.That(new[] { "YOU", "HOH", "VETO", "NOM", "WINNER", "FINAL 2", "OUT" }, Does.Contain(standing),
                "The badge word is one the rail already used, and this one is '" + standing + "'.");
        }

        /// <summary>
        /// A chip is three lines deep, so the rail can no longer assume a full house fits at full
        /// size. It scales the whole chip to the column it has rather than letting the last
        /// houseguest slide under the lower third.
        /// </summary>
        [UnityTest]
        public IEnumerator CastRail_FitsAboveTheLowerThirdAtTheHouseItIsGiven()
        {
            director.ClosePanels();
            Canvas.ForceUpdateCanvases();
            yield return null;
            Canvas.ForceUpdateCanvases();

            var rail = director.GetComponentsInChildren<RectTransform>(true)
                .First(rect => rect.name == CastRail.RootName);
            var status = director.GetComponentsInChildren<RectTransform>(true)
                .First(rect => rect.name == "Status" && rect.gameObject.activeInHierarchy);

            var railRect = ScreenRect(rail);
            var statusRect = ScreenRect(status);
            Assert.That(railRect.Overlaps(statusRect), Is.False,
                "The rail " + railRect + " runs into the lower third " + statusRect + ".");
            Assert.That(railRect.height, Is.GreaterThan(0f), "The rail is not collapsed.");
        }

        /// <summary>
        /// The cast screen's cards are the mockups' glass: a hairline on every card, and the glow
        /// on the one that is picked. The word "PLAYING AS" goes on saying it too, because the glow
        /// is decoration and the word is the state.
        /// </summary>
        [UnityTest]
        public IEnumerator CastSelect_CardsAreGlassAndOnlyTheChosenOneGlows()
        {
            yield return OpenCastScreen();
            Canvas.ForceUpdateCanvases();

            var chosen = CastTemplates.In(CastTemplates.Roster.Regular).First();
            var card = CastScreen().GetComponentsInChildren<RectTransform>(true)
                .Single(rect => rect.name == chosen.Name);

            Assert.That(SameColour(card.GetComponent<Image>().color, UiTheme.GlassFill), Is.True,
                "A resting card is the mockups' glass ground.");
            Assert.That(Child(card, "Border"), Is.Not.Null, "Every card carries the hairline.");
            Assert.That(Child(card, CastSelect.CardGlowName), Is.Null,
                "A card nobody picked does not glow.");
            Assert.That(card.GetComponentsInChildren<RectTransform>(true)
                    .Count(rect => rect.name == CastSelect.TraitChipName),
                Is.EqualTo(chosen.Traits.Length),
                "The traits are the mockup's pills, one per trait.");
            foreach (var trait in chosen.Traits)
                Assert.That(Copy(card), Does.Contain(trait), "and they carry the build's own words.");

            CastButtons(chosen.Name)[0].onClick.Invoke();
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();

            card = CastScreen().GetComponentsInChildren<RectTransform>(true)
                .Single(rect => rect.name == chosen.Name);
            Assert.That(Child(card, CastSelect.CardGlowName), Is.Not.Null,
                "The picked card gains the mockups' glow.");
            Assert.That(Copy(card), Does.Contain("PLAYING AS"),
                "and still states the pick in words, which is what a screen reader reads.");
        }

        private static Transform Child(Transform parent, string name)
        {
            foreach (Transform child in parent) if (child.name == name) return child;
            return null;
        }

        /// <summary>Colour equality at eight-bit precision, which is what the theme round-trips to.</summary>
        private static bool SameColour(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.005f && Mathf.Abs(a.g - b.g) < 0.005f
            && Mathf.Abs(a.b - b.b) < 0.005f && Mathf.Abs(a.a - b.a) < 0.005f;
    }
}
