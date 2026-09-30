using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
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
    /// MOCKUP-PASS M14, the Final 3 house view (mockup 60): the strip's finalist cards with their
    /// traits and the jury in its quote card's slot; the jury strip's door to the jury house, a
    /// panel of its own over the house; the rooms and the few left named at map distance; the
    /// Talk prompt's line; and the live feed's caption naming the few in a room. The frame's
    /// earlier pins hold beside them: one rail child per actor with its parts, a face per juror on
    /// the objectives card's strip, and the icons' captions.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private IEnumerator InstallTheFinalThree()
        {
            HoldTheHouseForTheFixture();
            yield return InstallDiaryFixture(
                state => state.phase == EpisodePhase.FinalHoHPart1 && state.Active.Any(actor => actor.isPlayer),
                "the player at the Final 3");
            yield return PutAwayTheCards();
        }

        /// <summary>Every label under <paramref name="root"/> with copy draws at least one character of it.</summary>
        private static void AssertCopyDrawsUnder(RectTransform root, string where)
        {
            Canvas.ForceUpdateCanvases();
            foreach (var label in root.GetComponentsInChildren<TMP_Text>())
            {
                if (string.IsNullOrWhiteSpace(label.text)) continue;
                label.ForceMeshUpdate(true);
                var info = label.textInfo;
                Assert.That(info.characterInfo.Take(info.characterCount).Any(glyph => glyph.isVisible), Is.True,
                    where + ": '" + label.text + "' (" + label.name + ", box " + label.rectTransform.rect.size.ToString("0") + ") draws nothing.");
            }
        }

        private RectTransform[] NameChips() => director.GetComponentsInChildren<RectTransform>(true)
            .Where(rect => rect.name.StartsWith(EpisodeTravelBeacons.NameChipPrefix, System.StringComparison.Ordinal)).ToArray();

        /// <summary>
        /// At three the strip is the finalists' cards where the band has room for them: the chip's
        /// parts by the chip's names, a wider photo, and the traits in a column of their own. The
        /// quote card's slot holds the jury - 'The Jury (n)', what they are there to do, a face per
        /// juror bound through the portrait studio, first names up to seven - and a canvas too narrow
        /// for it leaves the jury to the objectives card's strip. The pill calls the jury's number
        /// Jury members. At both text sizes.
        /// </summary>
        [UnityTest]
        public IEnumerator Endgame_TheFinalThreeStripIsTheFinalistsCardsAndTheJury()
        {
            yield return InstallTheFinalThree();
            var state = director.Snapshot;
            Assume.That(EpisodeHud.IsFinalThree(state), Is.True, "Three active in the first part of the final Head of Household.");
            var jurors = state.contestants.Where(EpisodeHud.IsJuror).ToList();
            Assume.That(jurors, Is.Not.Empty, "A jury is seated by the Final 3.");

            foreach (bool larger in new[] { false, true })
            {
                yield return ApplyTextSize(larger);
                yield return PutAwayTheCards();
                float scale = larger ? 1.2f : 1f;
                string where = larger ? " at the larger text" : " at the resting text";

                // The pins the frame at three already had: one rail child per actor, each with its parts.
                var rail = TheRail();
                Assert.That(rail.Cast<Transform>().Count(), Is.EqualTo(3), "The strip is the three who are left" + where + ".");
                float band = ((RectTransform)HudCanvas().transform).rect.width - 2f * CastRail.SideMargin;
                bool cards = (3f * 306f - 6f) * scale <= band;
                foreach (var actor in state.Active)
                {
                    var entry = (RectTransform)rail.Cast<Transform>().Single(child => child.name == actor.name);
                    Assert.That(entry.Find(CastRail.ChipName), Is.Not.Null, actor.name + " keeps the chip's glass" + where + ",");
                    Assert.That(entry.GetComponent<Button>(), Is.Not.Null, "the control that follows them,");
                    Assert.That(entry.GetComponentsInChildren<TMP_Text>(true).Count(label => label.name == CastRail.MoodWordName), Is.EqualTo(1), "the mood in a word,");
                    Assert.That(entry.GetComponentsInChildren<RectTransform>(true).Count(rect => rect.name == CastRail.MoodGlyphName), Is.EqualTo(1), "and the mood face.");
                    Assert.That(entry.Cast<Transform>().Count(child => child.name == CastRail.BadgeName), Is.LessThanOrEqualTo(1), "One role at most.");
                    if (!cards) continue;
                    Assert.That(entry.rect.width, Is.EqualTo(300f * scale).Within(1f), actor.name + "'s is the finalist's card" + where + ".");
                    var traits = entry.GetComponentsInChildren<TMP_Text>(true).Where(label => label.name == CastRail.TraitName).Select(label => label.text).ToList();
                    Assert.That(traits, Is.EqualTo(actor.traits.Take(4).ToList()), actor.name + "'s traits, in the cast card's order.");
                    Assert.That(entry.GetComponentsInChildren<Image>(true).Count(image => image.name == CastRail.TraitDotName), Is.EqualTo(traits.Count), "A dot before each.");
                    AssertCopyDrawsUnder(entry, actor.name + "'s card" + where);
                }
                if (cards)
                    for (int a = 0; a < rail.childCount; a++)
                        for (int b = a + 1; b < rail.childCount; b++)
                            Assert.That(ScreenRect((RectTransform)rail.GetChild(a)).Overlaps(ScreenRect((RectTransform)rail.GetChild(b))), Is.False,
                                rail.GetChild(a).name + "'s card overlaps " + rail.GetChild(b).name + "'s" + where + ".");

                // The jury: in the quote card's slot where the band has room, and on the objectives
                // card's strip always.
                var strip = LastActive(EpisodeHud.JuryStripName);
                Assert.That(strip, Is.Not.Null, "The objectives card keeps its strip" + where + ".");
                Assert.That(strip.GetComponentsInChildren<Image>().Count(image => image.name == "Juror"), Is.EqualTo(jurors.Count), "One face per juror on the strip.");
                var card = LastActive(CastRail.QuoteName);
                if (card != null)
                {
                    var labels = card.GetComponentsInChildren<TMP_Text>().ToList();
                    Assert.That(labels.Single(label => label.name == CastRail.JuryHeadingName).text, Is.EqualTo("The Jury (" + jurors.Count + ")"),
                        "The quote card's slot is the jury's at the endgame" + where + ".");
                    Assert.That(labels.Single(label => label.name == CastRail.JuryLineName).text, Is.EqualTo(CastRail.JuryDecidesLine), "The vote is still to come.");
                    Assert.That(card.GetComponentsInChildren<RectTransform>().Count(rect => rect.name == CastRail.JuryFaceName), Is.EqualTo(jurors.Count), "A face per juror,");
                    Assert.That(card.GetComponentsInChildren<CharacterPortraitBinding>(true), Has.Length.EqualTo(jurors.Count), "each bound to them, so a late face lands.");
                    var names = labels.Where(label => label.name == CastRail.JuryNameName).Select(label => label.text).ToList();
                    Assert.That(names, Is.EqualTo(jurors.Count <= CastRail.JuryNamesUpTo
                        ? jurors.Select(juror => FinalistRead.FirstName(juror.name)).ToList() : new System.Collections.Generic.List<string>()),
                        "First names under the faces while seven or fewer share the row; faces alone past that.");
                    Assert.That(card.GetComponentsInChildren<Graphic>(true).Any(graphic => graphic.raycastTarget), Is.False, "The card takes no click.");
                    Assert.That(ScreenRect(card).Overlaps(ScreenRect(rail)), Is.False, "The card stands clear of the finalists" + where + ".");
                    AssertCopyDrawsUnder(card, "The jury card" + where);
                }
                // With no room for the card (4:3 at the larger text), the strip above is the jury in the frame.

                var pill = ActiveRect("House pill");
                Assert.That(pill, Is.Not.Null);
                Assert.That(Words(pill), Does.Contain(EpisodeHud.JuryCellLabel).And.Contain(jurors.Count.ToString()),
                    "The pill's third cell is the jury members' number.");
            }
            yield return ApplyTextSize(false);
            yield return PutAwayTheCards();
            if (Application.isBatchMode) yield return CaptureFraming("endgame-final-three-view", settle: false);
        }

        /// <summary>
        /// The owner's decision 42: the objectives card's jury strip is a door to the jury house,
        /// with a caption of its own, through the Final 3's parts as well as the window. It opens a
        /// panel of its own over the house - no station panel under it - which pauses the house as
        /// every panel does, commits nothing, and goes back to the house on leaving or on Escape.
        /// Under a panel the strip is no door, so it never stands live beside the tile or the row.
        /// </summary>
        [UnityTest]
        public IEnumerator Endgame_TheJuryStripOpensTheJuryHouseOverTheHouse()
        {
            yield return InstallTheFinalThree();
            var state = director.Snapshot;
            Assume.That(EpisodeHud.IsFinalThree(state), Is.True, "Three active in the first part of the final Head of Household.");
            Assume.That(FinalistRead.Jurors(state), Is.Not.Empty, "A jury is seated by the Final 3.");
            Assert.That(EpisodeDirector.JuryStripDoorAvailable(state), Is.True, "At three, with a jury, the strip is a door,");
            Assert.That(EpisodeDirector.JuryHouseAvailable(state), Is.False, "through the final Head of Household's parts, where the tile and the row are not.");
            Assert.That(EpisodeDirector.JuryStripCaption, Is.Not.EqualTo(EpisodeDirector.JuryHouseCaption), "A caption of its own.");
            // A render with nothing open over the house: the frame the strip is a door in.
            director.ClosePanels();
            yield return Frames(2);

            var strip = LastActive(EpisodeHud.JuryStripName);
            Assert.That(strip, Is.Not.Null);
            var door = ButtonWithCaption(EpisodeDirector.JuryStripCaption);
            Assert.That(door.transform.IsChildOf(strip), Is.True, "The door is the strip's own,");
            Assert.That(strip.GetComponentsInChildren<Image>().Count(image => image.name == "Juror"), Is.EqualTo(state.contestants.Count(EpisodeHud.IsJuror)),
                "and the faces are still one a juror.");

            door.onClick.Invoke();
            yield return Frames(2);
            Canvas.ForceUpdateCanvases();
            Assert.That(director.InJuryHouse, Is.True);
            Assert.That(director.IsPanelOpen, Is.True, "Over the house, the jury house is a panel of its own,");
            Assert.That(director.IsPhasePanelOpen, Is.False, "not the station's.");
            // Opening any panel banks the house's elapsed free time, so the state is read once it is open.
            var open = director.Snapshot;
            AssertTheJuryHouse(open);
            Assert.That(ButtonWithCaptionOrNull(EpisodeDirector.JuryStripCaption), Is.Null, "Under a panel the strip is no door,");
            Assert.That(ButtonWithCaptionOrNull(EpisodeDirector.JuryHouseCaption), Is.Null, "and no second door stands in the view.");
            yield return Frames(2);
            AssertEquivalent(open, director.Snapshot);

            ButtonWithCaption(EpisodeDirector.LeaveJuryHouseCaption).onClick.Invoke();
            yield return Frames(2);
            Assert.That(director.InJuryHouse, Is.False);
            Assert.That(director.IsPanelOpen, Is.False, "Leaving goes back to the house,");
            Assert.That(ButtonWithCaptionOrNull(EpisodeDirector.JuryStripCaption), Is.Not.Null, "where the strip is a door again.");

            // Escape's close, as every panel's.
            ButtonWithCaption(EpisodeDirector.JuryStripCaption).onClick.Invoke();
            yield return Frames(2);
            Assert.That(director.InJuryHouse, Is.True);
            director.ClosePanels();
            yield return Frames(2);
            Assert.That(director.InJuryHouse, Is.False);
            Assert.That(director.IsPanelOpen, Is.False);
        }

        /// <summary>
        /// Pulled back over the house, every room's icon names its room under it - a label, while
        /// the caption a click is found by stays the icon's. At three, the few left in the house are
        /// named over their heads, the player as the lists name them; in an ordinary week nobody
        /// is. None of it takes a click, and it goes with the icons when the camera closes in.
        /// </summary>
        [UnityTest]
        public IEnumerator Endgame_AtMapDistanceTheRoomsAndTheFewAreNamed()
        {
            director.ClosePanels();
            yield return null;
            yield return PullBackOverTheHouse();
            var beacons = director.TravelBeacons;
            Assert.That(beacons, Is.Not.Null, "The icons are built the first time the house is the view.");
            Assert.That(beacons.IsShowing, Is.True, "Pulled back over the house, the rooms carry their icons.");
            int named = 0;
            foreach (var marker in SceneComponents<HouseRoomMarker>().Where(marker => marker.isActiveAndEnabled))
            {
                var icon = BeaconFor(marker.RoomName);
                if (icon == null || !icon.gameObject.activeInHierarchy) continue;
                var tag = icon.Find(EpisodeTravelBeacons.RoomNameName);
                Assert.That(tag != null && tag.gameObject.activeInHierarchy, Is.True, marker.RoomName + "'s icon names its room.");
                Assert.That(tag.GetComponentInChildren<TMP_Text>().text, Is.EqualTo(RoomLabels.Title(marker.RoomName)), "In the floor paint's words.");
                Assert.That(tag.GetComponentsInChildren<Graphic>(true).Any(graphic => graphic.raycastTarget), Is.False, "A label, not a control.");
                Assert.That(icon.Find("Tip").GetComponentInChildren<TMP_Text>(true).text, Is.Not.EqualTo(tag.GetComponentInChildren<TMP_Text>().text),
                    "The icon's caption is still what a click does, not the room's name.");
                AssertCopyDrawsUnder((RectTransform)tag, marker.RoomName + "'s name");
                named++;
            }
            Assert.That(named, Is.GreaterThan(0), "Some room's icon is on screen.");
            Assert.That(NameChips().Any(chip => chip.gameObject.activeInHierarchy), Is.False, "Nobody is named over the house in an ordinary week.");

            yield return InstallTheFinalThree();
            var state = director.Snapshot;
            Assume.That(EpisodeHud.IsFinalThree(state), Is.True, "Three active in the first part of the final Head of Household.");
            yield return PullBackOverTheHouse();
            // The names are read with the rooms' counts, twice a second, on the reloaded house's icons.
            bool Naming() => director.TravelBeacons != null && state.Active.Any(actor => director.TravelBeacons.IsNaming(actor.id));
            float deadline = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < deadline && !Naming()) yield return null;
            Assert.That(director.TravelBeacons, Is.Not.Null);
            Assert.That(director.TravelBeacons.IsShowing, Is.True, "The icons are up at the Final 3.");
            foreach (var actor in state.Active)
            {
                var chip = NameChips().LastOrDefault(rect => rect.name == EpisodeTravelBeacons.NameChipPrefix + actor.id);
                Assert.That(chip, Is.Not.Null, actor.name + " wears a name chip at the Final 3.");
                Assert.That(chip.GetComponentInChildren<TMP_Text>(true).text,
                    Is.EqualTo(HudPrimitives.WithYou(FinalistRead.FirstName(actor.name), actor.id == state.playerId)), "Named as the lists name them.");
                Assert.That(chip.GetComponentsInChildren<Graphic>(true).Any(graphic => graphic.raycastTarget), Is.False, "A label, never a control.");
                Assert.That(chip.GetComponentInChildren<Selectable>(true), Is.Null);
            }
            Assert.That(Naming(), Is.True, "From over the middle of the house, at least one of the three is named on screen.");
            foreach (var chip in NameChips().Where(chip => chip.gameObject.activeInHierarchy))
                AssertCopyDrawsUnder(chip, chip.name);

            // Close in on somebody: the icons go, and the names with them.
            cameraRig.FocusSubject(SceneComponents<HouseNpc>().First(actor => actor.gameObject.activeInHierarchy).transform);
            deadline = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < deadline && cameraRig.Distance > EpisodeTravelBeacons.HiddenBelow - .5f) yield return null;
            yield return null;
            Assert.That(Naming(), Is.False, "Close up, the name plates name them.");
        }

        /// <summary>
        /// A hint is a muted line of its own under the prompt's words: the words, the control and
        /// its caption are what they always were, the box grows to hold the line, and a prompt with
        /// no hint is the prompt it was.
        /// </summary>
        [UnityTest]
        public IEnumerator Prompt_AHintIsALineOfItsOwnUnderTheWords()
        {
            director.ClosePanels();
            yield return null;
            var hud = director.GetComponentInChildren<EpisodeHud>(true);
            const string words = "E  ·  Talk to Somebody";
            // Set and read inside one frame: the director sets the prompt again every Update.
            hud.SetPrompt(words, EpisodeHud.TalkHint);
            Canvas.ForceUpdateCanvases();
            var root = LastActive("Interaction prompt");
            Assert.That(root, Is.Not.Null, "The prompt is up.");
            var labels = root.GetComponentsInChildren<TMP_Text>(true);
            var hint = labels.Single(label => label.name == EpisodeHud.PromptHintName);
            Assert.That(hint.gameObject.activeInHierarchy, Is.True, "The hint is drawn,");
            Assert.That(hint.text, Is.EqualTo(EpisodeHud.TalkHint));
            var said = labels.Single(label => label != hint && label.text == words);
            Assert.That(root.GetComponent<Button>(), Is.Not.Null, "The prompt is still the control,");
            Assert.That(labels.Any(label => label.text == EpisodeHud.InteractCaption), Is.True, "with its caption unchanged.");
            Assert.That(ScreenRect(hint.rectTransform).yMax, Is.LessThanOrEqualTo(ScreenRect(said.rectTransform).yMin + .5f), "The line sits under the words,");
            Assert.That(ScreenRect(hint.rectTransform).yMin, Is.GreaterThanOrEqualTo(ScreenRect(root).yMin - .5f), "inside the prompt.");
            AssertCopyDrawsUnder(root, "The prompt with its hint");

            hud.SetPrompt(words);
            Assert.That(hint.gameObject.activeSelf, Is.False, "No hint, no line,");
            Assert.That(root.rect.height, Is.EqualTo(52f).Within(.5f), "and the prompt is the height it was.");
            yield return null;
        }

        /// <summary>
        /// The live feed's caption names the room and counts who is in it; at the endgame, where a
        /// room holds three at most, it names them, the player first as YOU. An empty room and a
        /// room of more than three are counted, and the caption never says anybody is talking.
        /// </summary>
        [Test]
        public void LiveFeed_AtTheEndgameTheCaptionNamesTheFewInTheRoom()
        {
            var you = new HouseMap.Occupant("p", "Emma Stone", null, true);
            var alex = new HouseMap.Occupant("a", "Alex Park", null, false);
            var jordan = new HouseMap.Occupant("j", "Jordan Lee", null, false);
            var sam = new HouseMap.Occupant("s", "Sam Diaz", null, false);
            Assert.That(EpisodeDirector.RoomCaption("Living", new[] { alex, you, jordan }, true), Is.EqualTo("LIVING ROOM · YOU, ALEX, JORDAN"));
            Assert.That(EpisodeDirector.RoomCaption("Kitchen", new[] { alex }, true), Is.EqualTo("KITCHEN · ALEX"));
            Assert.That(EpisodeDirector.RoomCaption("Living", new[] { alex, you, jordan }, false), Is.EqualTo("LIVING ROOM · 3 HOUSEGUESTS"),
                "An ordinary week counts them, as it always did.");
            Assert.That(EpisodeDirector.RoomCaption("Kitchen", new HouseMap.Occupant[0], true), Is.EqualTo("KITCHEN · 0 HOUSEGUESTS"), "An empty room is counted.");
            Assert.That(EpisodeDirector.RoomCaption("Yard", new[] { alex, you, jordan, sam }, true), Is.EqualTo("COMPETITION YARD · 4 HOUSEGUESTS"),
                "More than three are counted.");
            Assert.That(EpisodeDirector.RoomCaption("Kitchen", null, true), Is.EqualTo("KITCHEN · 0 HOUSEGUESTS"), "Nobody read is nobody named.");
        }
    }
}
