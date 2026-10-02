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
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The house's one gate for what it draws over itself (UI-UX-PASS-PLAN G0 and H0): nothing
    /// world-space or on a canvas under the HUD's - the icons over the rooms and the names under
    /// them, the houseguests' plates, the overview's room chips, the yard's sign and station discs,
    /// the talk prompt - projects into a HUD card's rect, and all of it is down under a board, the
    /// briefing or a panel. The chrome itself: the status line stays down under the Nearby bar
    /// through a render, a corner link's words stay legible under a column that cannot be pressed,
    /// a Recent Events line ends in a whole word or an ellipsis, and the briefing's recommended row
    /// stands inside the briefing.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// The chrome's screen rects, in pixels: every active child of the HUD's canvas but the
        /// containers the size of the frame, read as the house reads them (<see cref="EpisodeHud.Covers"/>).
        /// </summary>
        private List<Rect> ChromeRects()
        {
            var hud = HudCanvas();
            var frame = ((RectTransform)hud.transform).rect;
            var rects = new List<Rect>();
            foreach (Transform child in hud.transform)
            {
                if (!child.gameObject.activeInHierarchy || !(child is RectTransform rect)) continue;
                var size = rect.rect.size;
                if (size.x <= 0f || size.y <= 0f || size.x * size.y > frame.width * frame.height * .6f) continue;
                rects.Add(ScreenRect(rect));
            }
            return rects;
        }

        /// <summary>A piece the house draws, inset by a pixel so a touching edge is not a lie on the chrome, lies on no card.</summary>
        private static void AssertClearOfChrome(Rect piece, List<Rect> chrome, string what)
        {
            var inner = Rect.MinMaxRect(piece.xMin + 1f, piece.yMin + 1f, piece.xMax - 1f, piece.yMax - 1f);
            foreach (var card in chrome)
                Assert.That(inner.Overlaps(card), Is.False, what + " at " + piece + " lies under the chrome at " + card + ".");
        }

        /// <summary>
        /// Nothing the house draws over itself projects into a HUD card's rect: the icons over the
        /// rooms, the room names under them and the name chips at the endgame, every name plate that
        /// is drawn, and the overview's room chips while the map is up - each through the camera the
        /// frame is drawn with, against the chrome's rects.
        /// </summary>
        private void AssertNothingOfTheHouseUnderTheChrome(string where)
        {
            Canvas.ForceUpdateCanvases();
            var chrome = ChromeRects();
            Assert.That(chrome, Is.Not.Empty, where + ": the HUD has chrome on screen.");
            foreach (var rect in director.GetComponentsInChildren<RectTransform>()
                         .Where(item => item.gameObject.activeInHierarchy
                             && (item.name.StartsWith(EpisodeTravelBeacons.BeaconPrefix, System.StringComparison.Ordinal)
                                 || item.name.StartsWith(EpisodeTravelBeacons.NameChipPrefix, System.StringComparison.Ordinal)
                                 || item.name == EpisodeTravelBeacons.RoomNameName)))
                AssertClearOfChrome(ScreenRect(rect), chrome, where + ": " + rect.name);
            var eye = cameraRig.ViewCamera;
            foreach (var npc in SceneComponents<HouseNpc>().Where(actor => actor.gameObject.activeInHierarchy && actor.PlateAlpha > .01f))
                if (npc.TryPlateScreenRect(eye, out var plate))
                    AssertClearOfChrome(plate, chrome, where + ": " + npc.DisplayName + "'s plate");
            var labels = SceneComponents<RoomLabels>().FirstOrDefault();
            if (labels != null && labels.IsShowing)
            {
                var corners = new Vector3[4];
                foreach (var chip in labels.GetComponentsInChildren<Canvas>().Where(canvas => canvas.renderMode == RenderMode.WorldSpace && canvas.gameObject.activeInHierarchy))
                {
                    ((RectTransform)chip.transform).GetWorldCorners(corners);
                    var points = corners.Select(corner => eye.WorldToScreenPoint(corner)).ToArray();
                    if (points.Any(point => point.z <= 0f)) continue;
                    AssertClearOfChrome(Rect.MinMaxRect(points.Min(p => p.x), points.Min(p => p.y), points.Max(p => p.x), points.Max(p => p.y)),
                        chrome, where + ": the room chip " + chip.name);
                }
            }
        }

        /// <summary>
        /// Over the house with an offer waiting (the 'offer-badges' frame), at both text sizes, from
        /// the house's own distance and pulled back over it: no icon, room name, name chip or drawn
        /// plate projects into a HUD card. With the offer's own card open - a panel - the icons are
        /// down with it and nothing the panel covers is up.
        /// </summary>
        [UnityTest]
        public IEnumerator ChromeGate_NothingTheHouseDrawsProjectsIntoAHudCard()
        {
            HoldTheHouseForTheFixture();
            var maya = SceneComponents<HouseNpc>().Single(npc => npc.Id == ContentCatalog.MayaId);
            PutBeforeThePlayer(live => live.deals.Add(WaitingOffer(live, maya.Id, DealKind.SafetyAgreement, "deal-ask-gate")));
            foreach (bool larger in new[] { false, true })
            {
                yield return ApplyTextSize(larger);
                string where = larger ? " at the larger text" : " at the resting text";
                director.ClosePanels();
                yield return Frames(3);
                Assert.That(director.IsHouseUnderChrome, Is.False, "Nothing stands over the house" + where + ".");
                AssertNothingOfTheHouseUnderTheChrome("The house with an offer waiting" + where);
                yield return PullBackOverTheHouse();
                yield return Frames(3);
                Assert.That(director.TravelBeacons.IsShowing, Is.True, "Pulled back, the rooms carry their icons" + where + ".");
                AssertNothingOfTheHouseUnderTheChrome("The house from above with an offer waiting" + where);
            }
            yield return ApplyTextSize(false);

            yield return OpenNearbyNpc(maya);
            yield return Frames(3);
            Assert.That(director.IsPanelOpen, Is.True, "The offer's card is a panel.");
            Assert.That(director.IsHouseUnderChrome, Is.True, "A panel over the house is the gate.");
            Assert.That(director.TravelBeacons.IsShowing, Is.False, "The icons are down under the panel.");
            AssertNothingOfTheHouseUnderTheChrome("The offer's card");
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// The briefing (the 'overview-dashboard' frame): every room chip is down under it and the
        /// talk prompt is not drawn over it - it drew over the recommended row - and E does nothing
        /// the prompt does not say; the recommended row stands inside the briefing, on the 16:9 frame
        /// and the 4:3 at the resting size, and once scrolled to at the larger text, where the
        /// briefing scrolls. The map put back brings the chips up except under the chrome, and the
        /// prompt with them.
        /// </summary>
        [UnityTest]
        public IEnumerator ChromeGate_TheBriefingHidesTheRoomChipsAndThePromptAndKeepsItsRecommendedRow()
        {
            HoldTheHouseForTheFixture();
            yield return SettleCast();
            var maya = SceneComponents<HouseNpc>().Single(npc => npc.Id == ContentCatalog.MayaId);
            foreach (bool larger in new[] { false, true })
            {
                yield return ApplyTextSize(larger);
                string where = larger ? " at the larger text" : " at the resting text";
                // Beside somebody, so the prompt has something to say.
                WarpPlayer(maya.transform.position + Vector3.right * 1.2f);
                director.ClosePanels();
                yield return Frames(3);
                Assert.That(ActiveRect("Interaction prompt"), Is.Not.Null, "Beside a houseguest the prompt is up" + where + ".");

                Assert.That(director.ToggleOverview(), Is.True);
                yield return Frames(3);
                Canvas.ForceUpdateCanvases();
                Assert.That(director.IsBriefing, Is.True, "The rail's row is the briefing over the map" + where + ".");
                Assert.That(director.IsHouseUnderChrome, Is.True, "The briefing is the gate" + where + ".");
                Assert.That(ActiveRect("Interaction prompt"), Is.Null, "The prompt is not drawn over the briefing" + where + ".");
                var labels = SceneComponents<RoomLabels>().Single();
                Assert.That(labels.IsShowing, Is.True, "The map's chips exist under the briefing" + where + ",");
                Assert.That(labels.ShownCount, Is.EqualTo(0), "and every one of them is down" + where + ".");
                string talking = director.TalkingToId;
                director.Interact();
                yield return null;
                Assert.That(director.TalkingToId, Is.EqualTo(talking), "E under the briefing does what the prompt says: nothing" + where + ".");
                Assert.That(director.IsBriefing, Is.True, "and the briefing stays" + where + ".");

                if (!larger)
                    yield return AtBothFrames(frame => AssertWithin(OnTheHud(LastActive(EpisodeHud.DashboardName)),
                        OnTheHud(LastActive(EpisodeHud.RecommendedTilesName)), "The recommended row on the " + frame + " frame", "the briefing"));
                else
                {
                    // The briefing scrolls at the larger text; the row is whole once it is scrolled to.
                    var panel = LastActive(EpisodeHud.DashboardName);
                    var row = LastActive(EpisodeHud.RecommendedTilesName);
                    var scroll = panel.GetComponentInChildren<ScrollRect>();
                    Assert.That(scroll, Is.Not.Null, "The briefing has its scroll.");
                    ScrollTo(scroll, row);
                    Canvas.ForceUpdateCanvases();
                    AssertWithin(ScreenRect(panel), ScreenRect(row), "The recommended row scrolled to" + where, "the briefing");
                }

                ButtonWithCaption(EpisodeDirector.ShowMapCaption).onClick.Invoke();
                yield return Frames(3);
                Canvas.ForceUpdateCanvases();
                Assert.That(director.IsOverview && !director.IsBriefing, Is.True, "The map is up without the briefing" + where + ".");
                Assert.That(labels.ShownCount, Is.GreaterThan(0), "The map's chips are back" + where + ",");
                AssertNothingOfTheHouseUnderTheChrome("The map" + where);
                Assert.That(ActiveRect("Interaction prompt"), Is.Not.Null, "and the prompt is back over the map" + where + ".");
                director.EndOverview();
                yield return Frames(2);
            }
            yield return ApplyTextSize(false);
        }

        /// <summary>Scrolls <paramref name="row"/> into the scroll's viewport, as the HUD reveals a selection.</summary>
        private static void ScrollTo(ScrollRect scroll, RectTransform row)
        {
            Canvas.ForceUpdateCanvases();
            var viewport = scroll.viewport;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, row);
            float offset = bounds.min.y < viewport.rect.yMin ? viewport.rect.yMin - bounds.min.y
                : bounds.max.y > viewport.rect.yMax ? viewport.rect.yMax - bounds.max.y : 0f;
            var position = scroll.content.anchoredPosition;
            position.y = Mathf.Clamp(position.y + offset, 0f, Mathf.Max(0f, scroll.content.rect.height - viewport.rect.height));
            scroll.StopMovement();
            scroll.content.anchoredPosition = position;
        }

        /// <summary>
        /// A competition board over the yard (the 'minigame-count' frame) takes the yard's sign, the
        /// station discs and every name plate down; the board switched off for a frame - a capture
        /// of the yard does that - brings them back, and switched on again takes them down again.
        /// The sign keeps its place and its words throughout.
        /// </summary>
        [UnityTest]
        public IEnumerator ChromeGate_ACompetitionBoardHidesTheYardSignTheStationDiscsAndThePlates()
        {
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            ButtonWithCaption("Begin the next competition").onClick.Invoke();
            yield return null;
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            ButtonWithCaption("Practice this competition").onClick.Invoke();
            yield return Frames(3);
            var screen = SceneComponents<CompetitionGameScreen>().Single();
            Assert.That(screen.IsDrawn, Is.True, "The practice puts its board up over the yard.");
            Assert.That(CompetitionGameScreen.AnyDrawn, Is.True, "and the house knows a board is drawn.");
            Assert.That(director.IsHouseUnderChrome, Is.True, "A board is the gate.");

            var sign = SceneComponents<TextMeshPro>().Single(text => text.name == EpisodeDirector.CompetitionSignName && text.gameObject.activeInHierarchy);
            var discs = SceneComponents<MeshRenderer>().Where(renderer => renderer.GetComponent<MeshFilter>() != null
                && renderer.GetComponent<MeshFilter>().sharedMesh != null && renderer.GetComponent<MeshFilter>().sharedMesh.name == "Competition station disc").ToList();
            Assert.That(discs, Is.Not.Empty, "The arena marks its stations.");
            var npcs = SceneComponents<HouseNpc>().Where(npc => npc.gameObject.activeInHierarchy).ToList();
            Assert.That(npcs, Is.Not.Empty, "The house has houseguests to name.");
            AssertArenaOverlays(sign, discs, npcs, false, "under the board");
            Assert.That(sign.text, Is.Not.Empty, "The sign keeps its words under the board.");

            var surface = screen.GetComponent<Canvas>();
            surface.enabled = false;
            yield return Frames(3);
            Assert.That(screen.IsDrawn, Is.False, "A board whose canvas is off is not drawn.");
            Assert.That(director.IsHouseUnderChrome, Is.False, "and nothing stands over the house.");
            AssertArenaOverlays(sign, discs, npcs, true, "with the board off");

            surface.enabled = true;
            yield return Frames(3);
            AssertArenaOverlays(sign, discs, npcs, false, "with the board back");
        }

        private static void AssertArenaOverlays(TextMeshPro sign, List<MeshRenderer> discs, List<HouseNpc> npcs, bool drawn, string when)
        {
            Assert.That(sign.GetComponent<MeshRenderer>().enabled, Is.EqualTo(drawn), "The yard's sign is " + (drawn ? "" : "not ") + "drawn " + when + ".");
            foreach (var disc in discs)
                Assert.That(disc.enabled, Is.EqualTo(drawn), disc.name + " is " + (drawn ? "" : "not ") + "drawn " + when + ".");
            foreach (var npc in npcs)
                Assert.That(npc.PlateSuppressed, Is.EqualTo(!drawn), npc.DisplayName + "'s plate is " + (drawn ? "up" : "down") + " " + when + ".");
            if (!drawn)
                foreach (var npc in npcs)
                    Assert.That(npc.PlateAlpha, Is.LessThan(.01f), npc.DisplayName + "'s plate draws nothing " + when + ".");
        }

        /// <summary>
        /// The Final 3 frame (the 'endgame-final-three' frame), at both text sizes, pulled back over
        /// the house: no icon, room name, name chip or drawn plate projects into the objectives
        /// card or any other.
        /// </summary>
        [UnityTest]
        public IEnumerator ChromeGate_NothingTheHouseDrawsProjectsIntoTheFinalThreeObjectives()
        {
            yield return InstallTheFinalThree();
            foreach (bool larger in new[] { false, true })
            {
                yield return ApplyTextSize(larger);
                yield return PutAwayTheCards();
                string where = larger ? " at the larger text" : " at the resting text";
                director.ClosePanels();
                yield return PullBackOverTheHouse();
                yield return Frames(3);
                Assert.That(ActiveRect(EpisodeHud.ObjectivesCardName), Is.Not.Null, "The objectives card is up" + where + ".");
                AssertNothingOfTheHouseUnderTheChrome("The Final 3 frame" + where);
            }
            yield return ApplyTextSize(false);
        }

        /// <summary>
        /// The status line stands down under the Nearby bar and stays down through a render in the
        /// same frame (the 'nearby' frame): the rebuilt status line used to come back over the bar's
        /// words, found by name in the copy on its way out.
        /// </summary>
        [UnityTest]
        public IEnumerator ChromeGate_TheStatusStaysDownUnderTheNearbyBarThroughARender()
        {
            HoldTheHouseForTheFixture();
            yield return null;
            Assert.That(director.CanListenIn, Is.True, "Free time with actions left should offer listening in.");
            var caption = SceneComponents<HouseConversationCaption>().First();
            caption.Show("Maya Hassan", "Riley Johnson", "strategy", 1f);
            yield return Frames(2);
            Assert.That(ActiveRect(EpisodeHud.SpeechBarName), Is.Not.Null, "The bar comes up with the card.");
            Assert.That(ActiveRect("Status"), Is.Null, "The status line stands down for it.");

            caption.Show("Maya Hassan", "Riley Johnson", "strategy", 1f);
            RenderHudForTheCurrentCanvas();
            Assert.That(LastActive(EpisodeHud.SpeechBarName), Is.Not.Null, "A render in the same frame keeps the bar up,");
            Assert.That(LastActive("Status"), Is.Null, "and the status line down.");
            yield return Frames(2);
            caption.Show("Maya Hassan", "Riley Johnson", "strategy", 1f);
            yield return null;
            Assert.That(ActiveRect("Status"), Is.Null, "A frame on, the status line is still down,");
            var bar = ActiveRect(EpisodeHud.SpeechBarName);
            Assert.That(bar, Is.Not.Null, "and the bar is up.");
            Canvas.ForceUpdateCanvases();
            // No other piece of chrome stands over the bar's words.
            var own = ScreenRect(bar);
            foreach (var words in bar.GetComponentsInChildren<TMP_Text>().Where(label => !string.IsNullOrWhiteSpace(label.text)))
            {
                var box = ScreenRect(words.rectTransform);
                foreach (var piece in ChromeRects())
                    if (piece != own)
                        Assert.That(piece.Overlaps(box), Is.False, "'" + words.text + "' on the bar lies under the chrome at " + piece + ".");
            }
            caption.Hide();
            yield return null;
        }

        /// <summary>
        /// A corner link under a column that cannot be pressed keeps its words legible: its white
        /// ground is painted nothing, not Unity's grey at half alpha, and the words draw at full
        /// colour. 'View all' on the Recent Events card, 'See all' on the Relationships card.
        /// </summary>
        [UnityTest]
        public IEnumerator ChromeGate_TheCornerLinksKeepTheirWordsLegibleUnderANonInteractableColumn()
        {
            HoldTheHouseForTheFixture();
            director.ClosePanels();
            yield return Frames(2);
            var events = ActiveRect(EpisodeHud.RecentEventsCardName);
            Assert.That(events, Is.Not.Null, "The Recent Events card is up.");
            events.gameObject.AddComponent<CanvasGroup>().interactable = false;
            yield return RealSeconds(.3f);
            AssertLinkLegible(events, EpisodeHud.ViewAllEventsCaption);

            var npc = SceneComponents<HouseNpc>().First(actor => actor.gameObject.activeInHierarchy);
            director.FollowHouseguest(npc.Id);
            director.ClosePanels();
            yield return Frames(2);
            var relationships = ActiveRect(EpisodeHud.RelationshipsCardName);
            Assert.That(relationships, Is.Not.Null, "Following somebody, the Relationships card is up.");
            relationships.gameObject.AddComponent<CanvasGroup>().interactable = false;
            yield return RealSeconds(.3f);
            AssertLinkLegible(relationships, EpisodeHud.SeeAllRelationshipsCaption);
            director.FollowHouseguest(npc.Id);
            yield return null;
        }

        private static void AssertLinkLegible(RectTransform card, string caption)
        {
            var link = card.GetComponentsInChildren<RectTransform>(true).First(rect => rect.name == caption && rect.gameObject.activeInHierarchy);
            var button = link.GetComponent<Button>();
            Assert.That(button, Is.Not.Null, "'" + caption + "' is a control.");
            Assert.That(button.IsInteractable(), Is.False, "'" + caption + "' cannot be pressed under the column.");
            var ground = link.GetComponent<Image>();
            Assert.That(ground.canvasRenderer.GetColor().a, Is.LessThan(.05f), "'" + caption + "'s ground is painted nothing under its words, not the disabled grey.");
            var words = link.GetComponentsInChildren<TMP_Text>().First(label => label.text == caption);
            Assert.That(words.canvasRenderer.GetColor().a, Is.GreaterThan(.99f), "'" + caption + "'s words draw at full colour.");
            Assert.That(words.color.a, Is.GreaterThan(.99f), "'" + caption + "'s words are not faded.");
            Assert.That(words.color, Is.EqualTo(UiTheme.Accent), "'" + caption + "'s words keep the accent.");
        }

        /// <summary>
        /// A Recent Events line that runs past its two lines ends in a whole word and an ellipsis,
        /// at both text sizes, and no line clips: a fifty-letter budget ended mid-word behind the
        /// box's edge, the ellipsis on a third line nobody saw.
        /// </summary>
        [UnityTest]
        public IEnumerator ChromeGate_TheRecentEventsExcerptEndsInAWholeWordOrAnEllipsis()
        {
            HoldTheHouseForTheFixture();
            const string said = "Maya Hassan: Build a dependable voting partnership without making promises she cannot keep, and keep the house guessing about where the numbers really stand this week.";
            PutBeforeThePlayer(live => live.events.Add(new EpisodeEvent
                { sequence = live.nextSequence++, week = live.week, phase = live.phase, kind = "conversation", text = said }));
            foreach (bool larger in new[] { false, true })
            {
                yield return ApplyTextSize(larger);
                string where = larger ? " at the larger text" : " at the resting text";
                director.ClosePanels();
                yield return Frames(2);
                Canvas.ForceUpdateCanvases();
                var card = ActiveRect(EpisodeHud.RecentEventsCardName);
                Assert.That(card, Is.Not.Null, "The Recent Events card is up" + where + ".");
                var lines = card.GetComponentsInChildren<TMP_Text>().Where(label => label.name == EpisodeHud.RecentEventLineName).ToList();
                Assert.That(lines, Is.Not.Empty, "The card carries its lines" + where + ".");
                var cut = lines.FirstOrDefault(label => label.text.EndsWith("…", System.StringComparison.Ordinal));
                Assert.That(cut, Is.Not.Null, "The long line is cut with an ellipsis" + where + ".");
                string stem = cut.text.Substring(0, cut.text.Length - 1);
                Assert.That(said.StartsWith(stem, System.StringComparison.Ordinal), Is.True, "The cut keeps the line's own words" + where + ": '" + cut.text + "'.");
                Assert.That(stem.Length < said.Length && !char.IsLetterOrDigit(said[stem.Length]) || stem.Length == said.Length, Is.True,
                    "The cut falls after a whole word" + where + ": '" + cut.text + "'.");
                foreach (var line in lines)
                {
                    line.ForceMeshUpdate(true);
                    Assert.That(line.isTextOverflowing, Is.False, "'" + line.text + "' fits its two lines" + where + ".");
                    Assert.That(line.textInfo.lineCount, Is.LessThanOrEqualTo(2), "'" + line.text + "' takes two lines at most" + where + ".");
                    Assert.That(line.textInfo.characterInfo.Take(line.textInfo.characterCount).Count(glyph => glyph.isVisible),
                        Is.EqualTo(line.text.Count(ch => !char.IsWhiteSpace(ch))), "'" + line.text + "' draws every character" + where + ".");
                }
            }
            yield return ApplyTextSize(false);
        }
    }
}
