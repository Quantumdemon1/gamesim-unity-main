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
        public IEnumerator CastRail_TwelveHouseguestsFitAtLargerTextWithoutOverlapping()
        {
            // Put the HUD itself at the larger size first. The probe strip below is built at 1.2
            // and is measured against the REAL lower third, and the band the lower third stands on
            // is reserved off the strip's scaled height - so a 1.2 strip judged against a HUD still
            // laid out at 1.0 is being compared to a floor nineteen units too low. That is a
            // property of the test rather than of the strip, and while the strip was a column that
            // grew downward from the top margin it never showed.
            yield return ApplyTextSize(true);
            director.ClosePanels();
            yield return null;
            var canvas = director.GetComponentsInChildren<Canvas>()
                .Single(item => item.name == "Gamesim Episode HUD");
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 12 }, 2042);
            var rail = CastRail.Build(canvas.transform, state, 1.2f, TMP_Settings.defaultFontAsset, _ => null, _ => { });
            try
            {
                Canvas.ForceUpdateCanvases();
                yield return null;
                var entries = rail.Cast<Transform>().Select(item => (RectTransform)item).ToArray();
                Assert.That(entries.Length, Is.EqualTo(12));
                var status = ActiveRect("Status");
                // The strip is the bottom-most band now, so it is BELOW the lower third rather than
                // above it. What has to hold either way is that the two do not sit on each other.
                Assert.That(ScreenRect(rail).Overlaps(ScreenRect(status)), Is.False,
                    "The strip " + ScreenRect(rail) + " runs into the lower third " + ScreenRect(status) + ".");
                float width = ((RectTransform)canvas.transform).rect.width * canvas.scaleFactor;
                for (int a = 0; a < entries.Length; a++)
                {
                    var rect = ScreenRect(entries[a]);
                    Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(0));
                    // A row runs out of screen sideways where a column ran out of it downward, and
                    // the twelfth chip is the one that goes. Nothing checked this while the strip
                    // was a column, because a column could not.
                    Assert.That(rect.xMax, Is.LessThanOrEqualTo(width),
                        entries[a].name + " at " + rect + " is off the right-hand edge of a "
                        + width.ToString("0") + "px screen.");
                    Assert.That(rect.Overlaps(ScreenRect(status)), Is.False);
                    var mood = entries[a].GetComponentsInChildren<TMP_Text>().Single(label => label.name == CastRail.MoodWordName);
                    Assert.That(mood.fontSize, Is.EqualTo(Mathf.RoundToInt(11 * 1.2f)), "Full cast must not cancel larger text.");
                    for (int b = a + 1; b < entries.Length; b++)
                        Assert.That(rect.Overlaps(ScreenRect(entries[b])), Is.False, entries[a].name + " overlaps " + entries[b].name);
                }
            }
            finally { Object.Destroy(rail.gameObject); }
            // Hand the HUD back the way it was found; the fixture is shared.
            yield return ApplyTextSize(false);
        }

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
        /// A chip is three lines deep, so the strip can no longer assume a full house fits at full
        /// size. It scales the whole chip to the band it has rather than letting the last houseguest
        /// walk off the edge of the frame.
        /// </summary>
        [UnityTest]
        public IEnumerator CastRail_ClearsTheLowerThirdAtTheHouseItIsGiven()
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
                "The strip " + railRect + " runs into the lower third " + statusRect + ".");
            Assert.That(railRect.height, Is.GreaterThan(0f), "The strip is not collapsed.");
            Assert.That(railRect.width, Is.GreaterThan(0f), "and it has a row to draw into.");
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

            Assert.That(SameColour(card.GetComponent<Image>().color, UiTheme.CardFill), Is.True,
                "A resting card sits on the card ground, which is a LIFT above the screen behind it. "
                + "It used to be GlassFill on a scrim of Background at 0.97 - the same hex, under one "
                + "percent apart - so twelve cards were twelve hairlines around nothing.");
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

            // Copy() walks INACTIVE labels too, so the assertion above is satisfied by a label that
            // is switched off, collapsed to nothing or truncated away - none of which a player or a
            // screen reader would get anything from. The word has to actually be on the screen.
            var spoken = card.GetComponentsInChildren<TMP_Text>(true)
                .FirstOrDefault(label => label.text == "PLAYING AS");
            Assert.That(spoken, Is.Not.Null);
            Assert.That(spoken.gameObject.activeInHierarchy, Is.True,
                "The word that carries the pick is switched off, so only the glow says it.");
            spoken.ForceMeshUpdate();
            Assert.That(spoken.isTextOverflowing, Is.False,
                "The word that carries the pick is clipped out of its box.");

            // And the chosen card keeps its ground. UiTheme.Glass repaints whatever it is given to
            // GlassFill, and it runs on this card and no other - so the one card the player picked
            // is the one card that can silently lose the fill every other card has.
            Assert.That(SameColour(card.GetComponent<Image>().color, UiTheme.CardFill), Is.True,
                "The picked card has a different ground from the eleven it sits beside.");
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
