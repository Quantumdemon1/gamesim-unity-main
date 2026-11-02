using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
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

            foreach (var textSize in TextSizes.All)
            {
                yield return ApplyTextSize(textSize);
                yield return PutAwayTheCards();
                float scale = TextSizes.Scale(textSize);
                string where = " " + TextScales.At(textSize);

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
                if (cards && band - rail.rect.width >= 306f * scale)
                    Assert.That(card, Is.Not.Null, "Removing the decorative quote keeps the jury card when the finalists leave room" + where + ".");
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
        /// is. Two of them side by side still wear a name each, one lifted clear of the other, and
        /// the frame is captured with the rooms and the names in it. None of it takes a click, and
        /// it goes with the icons when the camera closes in. In the room view a finalist the view
        /// hides is named over their room's icon, the next stop's too where it waits at the frame's top.
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

            // Two of them side by side, as two finalists talking or sharing the couch are: the
            // player stood beside another finalist, and the view centred on the pair, away from the
            // chrome. Both are named, and neither name covers the other.
            var partner = SceneComponents<HouseNpc>().First(body => body.gameObject.activeInHierarchy
                && state.Active.Any(actor => !actor.isPlayer && actor.id == body.Id));
            Assert.That(NavMesh.SamplePosition(partner.transform.position + partner.transform.right * .9f, out var beside, 1.5f, NavMesh.AllAreas), Is.True,
                "There is floor beside " + partner.DisplayName + ".");
            WarpPlayer(beside.position);
            cameraRig.MoveTo((partner.transform.position + player.transform.position) * .5f, 24f);
            yield return WaitFor(() => director.TravelBeacons.IsNaming(state.playerId) && director.TravelBeacons.IsNaming(partner.Id), 6f,
                "Side by side in the middle of the view, the player and " + partner.DisplayName + " are both named.");
            float apart = Vector3.Distance(player.transform.position, partner.transform.position);
            for (int frame = 0; frame < 3; frame++)
            {
                var shown = NameChips().Where(chip => chip.gameObject.activeInHierarchy).ToArray();
                for (int a = 0; a < shown.Length; a++)
                    for (int b = a + 1; b < shown.Length; b++)
                        Assert.That(ScreenRect(shown[a]).Overlaps(ScreenRect(shown[b])), Is.False,
                            shown[a].name + " covers " + shown[b].name + ", with the player " + apart.ToString("0.0") + " m from " + partner.DisplayName + ".");
                yield return null;
            }
            // Beside a finalist the E key is usually a talk, and at three a talk's prompt carries
            // its line under the words. The capture below has it in the frame when it is up.
            var promptBox = LastActive("Interaction prompt");
            if (promptBox != null && promptBox.GetComponentsInChildren<TMP_Text>().Any(label => label.text.Contains("Talk to ")))
                Assert.That(promptBox.GetComponentsInChildren<TMP_Text>().Any(label => label.name == EpisodeHud.PromptHintName && label.text == EpisodeHud.TalkHint),
                    Is.True, "At three, the Talk prompt carries its line.");
            // The rooms' names, the chips and the prompt are drawn things: the frame is the evidence.
            if (Application.isBatchMode) yield return CaptureFraming("endgame-final-three-map", settle: false);

            // Close in on somebody: the icons go, and the names with them.
            cameraRig.FocusSubject(SceneComponents<HouseNpc>().First(actor => actor.gameObject.activeInHierarchy).transform);
            deadline = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < deadline && cameraRig.Distance > EpisodeTravelBeacons.HiddenBelow - .5f) yield return null;
            yield return null;
            Assert.That(Naming(), Is.False, "Close up, the name plates name them.");

            // The room view (D17, RS5): a finalist the view hides wears their name over their room's icon, not
            // over a body that is not drawn.
            cameraRig.ClearSubject();
            yield return RoomViewLetGoOfTheDollhouse();
            var scope = ScopeOf(director);
            int playerRoom = RoomLayers.IndexOf(director.WalkerRoomOf(player.transform));
            int elsewhere = Rs4FarRoom(scope, player.transform.position);
            ScopePlace(partner.transform, ScopeOpenFloor(scope, elsewhere), 2f);
            cameraRig.RecenterOnPlayer();
            yield return null;
            yield return RoomViewWaitOutTheLastChange();
            Assert.That(cameraRig.ViewRoom, Is.EqualTo(playerRoom), "The room view frames the player's room,");
            Assert.That(director.IsBodyDrawn(partner.transform), Is.False, partner.DisplayName + ", in the " + RoomLayers.RoomIds[elsewhere] + ", is hidden.");
            string theirs = RoomLayers.RoomIds[elsewhere];
            yield return WaitFor(() => director.TravelBeacons.IsNaming(partner.Id) && director.TravelBeacons.TryPlacedAt(theirs, out _), 3f,
                partner.DisplayName + " is named, and the " + theirs + "'s icon is up.");
            Assert.That(director.TravelBeacons.TryPlacedAt(theirs, out var iconAt), Is.True);
            var hung = ScreenRect(NameChips().Last(rect => rect.name == EpisodeTravelBeacons.NameChipPrefix + partner.Id));
            Assert.That(Mathf.Abs(hung.center.x - iconAt.x), Is.LessThan(hung.width * .5f + 4f), "The name hangs over the " + theirs + "'s icon (" + hung + " against " + iconAt + "),");
            // Hung as every name chip is (EpisodeTravelBeacons.PlaceNames, since the T0 review): over the icon, clear of
            // it, or - where the space over it is the frame's edge, the chrome or another name - on its other side,
            // under the room's name and clear of it. Either way in the icon's column (above), never on the icon or its words.
            var theirIcon = ScreenRect(BeaconFor(theirs));
            var theirTag = ScreenRect((RectTransform)BeaconFor(theirs).Find(EpisodeTravelBeacons.RoomNameName));
            Assert.That(hung.yMin >= theirIcon.yMax || hung.yMax <= theirTag.yMin, Is.True,
                "over it, or under its room's name where the space over it is taken, clear of both (the name " + hung + ", the icon "
                + theirIcon + ", the room's name " + theirTag + ").");

            // A houseguest the room view draws wears the plate it shows over every drawn body (RS5), and no chip over the
            // plate as well, the name twice (the fixes' review): the third finalist, stood beside the player in the middle of
            // the player's room, is drawn with the plate up and no chip, and the player, who has no plate, keeps theirs.
            director.SuspendNpcAutonomyForDiagnostics();
            var third = SceneComponents<HouseNpc>().First(body => body.gameObject.activeInHierarchy && body.Id != partner.Id
                && state.Active.Any(actor => !actor.isPlayer && actor.id == body.Id));
            var middle = ScopeOpenFloor(scope, playerRoom);
            ScopePlace(player.transform, middle, 2f);
            Assert.That(NavMesh.SamplePosition(middle + Vector3.right * 1.2f, out var besideThePlayer, 1f, NavMesh.AllAreas), Is.True, "There is floor beside the player.");
            ScopePlace(third.transform, besideThePlayer.position, 2f);
            cameraRig.RecenterOnPlayer();
            yield return null;
            yield return RoomViewWaitOutTheLastChange();
            Assert.That(cameraRig.ViewRoom, Is.EqualTo(playerRoom), "The view still frames the player's room.");
            yield return WaitFor(() => director.IsBodyDrawn(third.transform) && director.TravelBeacons.IsNaming(state.playerId), 3f,
                third.DisplayName + ", beside the player, is drawn, and the player is named mid-frame.");
            Assert.That(third.RoomViewPlates, Is.True, "The room view shows " + third.DisplayName + "'s plate,");
            Assert.That(director.TravelBeacons.IsNaming(third.Id), Is.False, "so no name chip hangs over it as well.");

            // And a hidden finalist's name reads as their room's alone (D17(3), the fixes' review): over their room's icon,
            // moved past it and the names before it only, never lifted past a neighbour's icon to hang over it. The partner
            // stands in the nomination room - from the living room's view its icon and the games room's wait at the frame's
            // edge the same way (the lane's rs4 captures), and from the Head of Household's its icon stands at the doorway
            // with the yard's and the games room's beside it - and in each of the nine framings the name is shown in, it is
            // nearest the nomination room's icon, touches no other icon or room's name, and hangs in the icon's column.
            ScopePlace(partner.transform, ScopeOpenFloor(scope, RoomLayers.IndexOf("Nomination")), 2f);
            var misread = new List<string>();
            foreach (var viewedRoom in new[] { "Living", "HoH" })
            {
                int room = RoomLayers.IndexOf(viewedRoom);
                cameraRig.SetReducedMotion(false);
                yield return RoomViewPlayerIn(scope, room);
                cameraRig.SetReducedMotion(true);
                int namedIn = 0;
                float nearest = HouseRoomView.NearestDistance, farthest = HouseRoomView.FarthestDistance(room);
                foreach (float distance in new[] { nearest, (nearest + farthest) * .5f, farthest })
                    foreach (float yaw in new[] { 0f, 45f, 90f })
                    {
                        RoomViewSetYaw(yaw);
                        SetCameraDistance(distance);
                        yield return null;
                        yield return null;
                        yield return null;
                        string where = viewedRoom + " at " + distance.ToString("0.##") + " m, yaw " + yaw;
                        if (director.IsBodyDrawn(partner.transform)) { misread.Add(where + ": " + partner.DisplayName + " is drawn"); continue; }
                        if (!director.TravelBeacons.IsNaming(partner.Id)) continue;
                        namedIn++;
                        var name = ScreenRect(NameChips().Last(rect => rect.name == EpisodeTravelBeacons.NameChipPrefix + partner.Id));
                        string closest = null;
                        float best = float.PositiveInfinity;
                        foreach (var id in RoomLayers.RoomIds)
                        {
                            var icon = BeaconFor(id);
                            if (icon == null || !icon.gameObject.activeInHierarchy) continue;
                            float away = (ScreenCentre(icon) - name.center).sqrMagnitude;
                            if (away < best) { best = away; closest = id; }
                            if (id == "Nomination") continue;
                            var tag = icon.Find(EpisodeTravelBeacons.RoomNameName) as RectTransform;
                            if (ScreenRect(icon).Overlaps(name) || (tag != null && tag.gameObject.activeSelf && ScreenRect(tag).Overlaps(name)))
                                misread.Add(where + ": the name (" + name + ") is on the " + id + "'s icon or its room's name");
                        }
                        if (closest != "Nomination") misread.Add(where + ": the name (" + name + ") is nearest the " + closest + "'s icon");
                        var own = BeaconFor("Nomination");
                        var ownIcon = ScreenRect(own);
                        var ownTag = ScreenRect((RectTransform)own.Find(EpisodeTravelBeacons.RoomNameName));
                        if (Mathf.Abs(name.center.x - ownIcon.center.x) >= name.width * .5f + 4f || (name.yMin < ownIcon.yMax && name.yMax > ownTag.yMin))
                            misread.Add(where + ": the name (" + name + ") is out of the nomination room's icon's column (the icon " + ownIcon + ", its name " + ownTag + ")");
                    }
                if (namedIn == 0) misread.Add(viewedRoom + ": " + partner.DisplayName + " is named in none of the nine framings");
            }

            // And the next stop's own (the claims' fourth review): the finalists hidden in the next stop's room - the yard the
            // competition is in, or the diary room when one is owed - wear their names over its icon where it waits at the
            // frame's top under the shade, the icon moving down for the air over it no further than half a footprint for
            // each name. From the living room, with no doorway to either, the icon waits at the frame's edge its way, at the
            // top in some of the nine framings: each framing is read with the partner and the third finalist in the
            // nomination room, then with both in the next stop's room, and where the icon stood at the top both names hang
            // over it in its column, and it stands within a footprint of where it stood.
            string next = director.TravelBeacons.NextStop;
            Assert.That(next, Is.EqualTo("Yard").Or.EqualTo("Private"), "At the Final 3 the next stop is the competition's yard or the diary room.");
            int living = RoomLayers.IndexOf("Living");
            var livingFloor = ScopeOpenFloor(scope, living);
            float livingNearest = HouseRoomView.NearestDistance, livingFarthest = HouseRoomView.FarthestDistance(living);
            var framings = new List<(float distance, float yaw)>();
            foreach (float distance in new[] { livingNearest, (livingNearest + livingFarthest) * .5f, livingFarthest })
                foreach (float yaw in new[] { 0f, 45f, 90f })
                    framings.Add((distance, yaw));
            var atTheTop = new Vector2?[framings.Count];
            var overTheNextStop = new List<string>();
            foreach (string inRoom in new[] { "Nomination", next })
            {
                var floorThere = ScopeOpenFloor(scope, RoomLayers.IndexOf(inRoom));
                Assert.That(NavMesh.SamplePosition(floorThere + Vector3.right * 1.2f, out var besideThePartner, 1f, NavMesh.AllAreas), Is.True,
                    "There is floor beside the " + inRoom + "'s middle.");
                ScopePlace(partner.transform, floorThere, 2f);
                ScopePlace(third.transform, besideThePartner.position, 2f);
                cameraRig.SetReducedMotion(false);
                yield return RoomViewPlayerIn(scope, living);
                yield return WaitFor(() => director.WalkerRoomOf(partner.transform) == inRoom && director.WalkerRoomOf(third.transform) == inRoom
                    && !director.IsBodyDrawn(partner.transform) && !director.IsBodyDrawn(third.transform), 3f,
                    partner.DisplayName + " and " + third.DisplayName + " stand in the " + inRoom + ", hidden from the living room's view.");
                cameraRig.SetReducedMotion(true);
                for (int framing = 0; framing < framings.Count; framing++)
                {
                    RoomViewSetYaw(framings[framing].yaw);
                    SetCameraDistance(framings[framing].distance);
                    RoomViewSetDesired(new Vector3(livingFloor.x, cameraRig.transform.position.y, livingFloor.z), framings[framing].distance);
                    yield return null;
                    yield return null;
                    yield return null;
                    string where = "Living at " + framings[framing].distance.ToString("0.##") + " m, yaw " + framings[framing].yaw + ", the two in the " + inRoom;
                    var icons = director.TravelBeacons;
                    if (!icons.TryPlacedAt(next, out var at)) { overTheNextStop.Add(where + ": the next stop's icon is not shown"); continue; }
                    if (inRoom != next)
                    {
                        var frame = cameraRig.ViewCamera.pixelRect;
                        if (icons.IsPinned(next) && at.y >= frame.yMin + .75f * frame.height) atTheTop[framing] = at;
                        continue;
                    }
                    if (atTheTop[framing] == null) continue;
                    var nextIcon = ScreenRect(BeaconFor(next));
                    // The code's footprint: half the icon's side and the badges' reach past it (Side * .5 + 8 of Side 46).
                    float footprint = nextIcon.width * (31f / 46f);
                    if ((at - atTheTop[framing].Value).magnitude > 2f * footprint + 1f)
                        overTheNextStop.Add(where + ": the next stop's icon moved " + (at - atTheTop[framing].Value).magnitude.ToString("0") + " px from "
                            + atTheTop[framing].Value + ", past half a footprint (" + footprint.ToString("0") + " px) for each of the two names");
                    foreach (var finalist in new[] { partner, third })
                    {
                        if (director.IsBodyDrawn(finalist.transform)) { overTheNextStop.Add(where + ": " + finalist.DisplayName + " is drawn"); continue; }
                        if (!icons.IsNaming(finalist.Id)) { overTheNextStop.Add(where + ": " + finalist.DisplayName + " is not named, the icon at the top (" + at + ")"); continue; }
                        var name = ScreenRect(NameChips().Last(rect => rect.name == EpisodeTravelBeacons.NameChipPrefix + finalist.Id));
                        if (name.yMin < nextIcon.yMax - .5f || Mathf.Abs(name.center.x - nextIcon.center.x) >= name.width * .5f + 4f)
                            overTheNextStop.Add(where + ": " + finalist.DisplayName + "'s name (" + name + ") does not hang over the " + next + "'s icon (" + nextIcon + ")");
                    }
                }
            }
            cameraRig.SetReducedMotion(false);
            Assert.That(misread, Is.Empty, misread.Count + " found: " + string.Join("; ", misread.Take(12)));
            Assert.That(atTheTop.Any(spot => spot != null), Is.True, "From the living room the " + next + "'s icon waits at the frame's top in one of the nine framings.");
            Assert.That(overTheNextStop, Is.Empty, overTheNextStop.Count + " found: " + string.Join("; ", overTheNextStop.Take(12)));
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
