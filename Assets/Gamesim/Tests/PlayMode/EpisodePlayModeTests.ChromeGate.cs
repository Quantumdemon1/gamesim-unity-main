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
    /// the talk prompt - projects into a HUD card's rect, through the screen's lens or a capture's,
    /// and all of it is down under a board, the briefing or a panel. The one you are talking to
    /// keeps their name. The chrome itself: the status line stays down under the Nearby bar through
    /// a render, a corner link's words stay legible under a column that cannot be pressed, a Recent
    /// Events line ends in a whole word or an ellipsis, and the briefing's recommended row stands
    /// inside the briefing. E does what the prompt says, and nothing under the gate.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>The cards a competition board stands over the yard with, by the names the screen gives them.</summary>
        private static readonly string[] BoardCardNames =
            { "Competition challenge", "Competition timer", "Game surface", "Competition field card", "Competition controls" };

        private Canvas HudCanvasOrNull() => director.GetComponentsInChildren<Canvas>(true).FirstOrDefault(item => item.name == "Gamesim Episode HUD");

        /// <summary>
        /// The lens a part is drawn through: none for an overlay canvas, whose world corners are its
        /// pixels; the canvas's camera for a camera canvas, as a capture makes every overlay; the
        /// view for a world-space canvas.
        /// </summary>
        private Camera LensOf(Component part)
        {
            var canvas = part.GetComponentInParent<Canvas>();
            if (canvas == null) return cameraRig.ViewCamera;
            canvas = canvas.rootCanvas;
            switch (canvas.renderMode)
            {
                case RenderMode.ScreenSpaceOverlay: return null;
                case RenderMode.ScreenSpaceCamera: return canvas.worldCamera;
                default: return cameraRig.ViewCamera;
            }
        }

        /// <summary>A part's screen rect, in pixels, through the lens its canvas is drawn with: the screen's, or a capture's target.</summary>
        private Rect ScreenBox(RectTransform rect)
        {
            var lens = LensOf(rect);
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var points = corners.Select(corner => RectTransformUtility.WorldToScreenPoint(lens, corner)).ToArray();
            return Rect.MinMaxRect(points.Min(p => p.x), points.Min(p => p.y), points.Max(p => p.x), points.Max(p => p.y));
        }

        /// <summary>A world-space rect's screen rect through <paramref name="eye"/>; false with a corner behind the lens.</summary>
        private static bool TryWorldRect(RectTransform rect, Camera eye, out Rect box)
        {
            box = default;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var points = corners.Select(corner => eye.WorldToScreenPoint(corner)).ToArray();
            if (points.Any(point => point.z <= 0f)) return false;
            box = Rect.MinMaxRect(points.Min(p => p.x), points.Min(p => p.y), points.Max(p => p.x), points.Max(p => p.y));
            return true;
        }

        /// <summary>
        /// The chrome's screen rects, in pixels, read as the house reads them: every active child of
        /// the HUD's canvas through the canvas's lens but the containers the size of the frame and a
        /// piece faded out by its own group (<see cref="EpisodeHud.Covers"/>). For a name plate
        /// (<see cref="EpisodeHud.CoversPlate"/>): the containers too, but not the shade under the top
        /// bar, and in a conversation not its stage - a scrim the pair are seen through - whose
        /// column and dial stand in for it. None while the HUD is off or aside.
        /// </summary>
        private List<Rect> ChromeRects(bool forPlates = false)
        {
            var rects = new List<Rect>();
            var canvas = HudCanvasOrNull();
            var hud = director.GetComponentInChildren<EpisodeHud>(true);
            if (canvas == null || !canvas.gameObject.activeInHierarchy || hud == null || hud.IsHeldForReveal) return rects;
            var group = canvas.GetComponent<CanvasGroup>();
            if (group != null && group.alpha <= .01f) return rects;
            var root = (RectTransform)canvas.transform;
            var frame = root.rect;
            bool conversation = hud.CurrentActivityLayout == EpisodeHud.ActivityLayout.Conversation;
            foreach (Transform child in root)
            {
                if (!child.gameObject.activeInHierarchy || !(child is RectTransform rect)) continue;
                var own = child.GetComponent<CanvasGroup>();
                if (own != null && own.alpha <= .01f && child.GetComponent<HudReveal>() == null) continue;
                var size = rect.rect.size;
                if (size.x <= 0f || size.y <= 0f) continue;
                bool container = size.x * size.y > frame.width * frame.height * .6f;
                if (!forPlates)
                {
                    if (!container) rects.Add(ScreenBox(rect));
                    continue;
                }
                if (child.name == EpisodeHud.TopShadeName) continue;
                if (conversation && child.name == "Episode panel")
                {
                    foreach (var part in rect.GetComponentsInChildren<RectTransform>()
                                 .Where(item => item.name == EpisodeHud.ConversationColumnName || item.name == EpisodeHud.DialName))
                        rects.Add(ScreenBox(part));
                    continue;
                }
                rects.Add(ScreenBox(rect));
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
        /// is drawn but the one you are talking to, and the overview's room chips while the map is
        /// up - each through the lens the frame is drawn with, against the chrome's rects.
        /// </summary>
        private void AssertNothingOfTheHouseUnderTheChrome(string where)
        {
            Canvas.ForceUpdateCanvases();
            var chrome = ChromeRects();
            var plateChrome = ChromeRects(true);
            Assert.That(chrome, Is.Not.Empty, where + ": the HUD has chrome on screen.");
            var beacons = director.TravelBeacons;
            if (beacons != null)
                foreach (var rect in beacons.GetComponentsInChildren<RectTransform>()
                             .Where(item => item.name.StartsWith(EpisodeTravelBeacons.BeaconPrefix, System.StringComparison.Ordinal)
                                 || item.name.StartsWith(EpisodeTravelBeacons.NameChipPrefix, System.StringComparison.Ordinal)
                                 || item.name == EpisodeTravelBeacons.RoomNameName))
                    AssertClearOfChrome(ScreenBox(rect), chrome, where + ": " + rect.name);
            var eye = cameraRig.ViewCamera;
            string talking = director.TalkingToId;
            foreach (var npc in SceneComponents<HouseNpc>().Where(actor => actor.gameObject.activeInHierarchy && actor.PlateAlpha > .01f && actor.Id != talking))
                if (npc.TryPlateScreenRect(eye, out var plate))
                    AssertClearOfChrome(plate, plateChrome, where + ": " + npc.DisplayName + "'s plate");
            var labels = SceneComponents<RoomLabels>().FirstOrDefault();
            if (labels != null && labels.IsShowing)
                foreach (var chip in labels.GetComponentsInChildren<Canvas>().Where(canvas => canvas.renderMode == RenderMode.WorldSpace))
                    if (TryWorldRect((RectTransform)chip.transform, eye, out var box))
                        AssertClearOfChrome(box, chrome, where + ": the room chip " + chip.name);
        }

        /// <summary>
        /// The chrome covers what it stands over, as the house asks: the middle of every piece, and
        /// the whole of it. Through a capture's lens this is the H0 root cause (the play sweep's row
        /// 10): a camera canvas's corners read as an overlay's are metres in front of the lens and
        /// cover no pixel, so every icon was shown under every card in every frame photographed.
        /// </summary>
        private void AssertTheChromeCoversWhatItStandsOver(string where)
        {
            Canvas.ForceUpdateCanvases();
            var hud = director.GetComponentInChildren<EpisodeHud>(true);
            var pieces = ChromeRects();
            Assert.That(pieces, Is.Not.Empty, where + ": the HUD has chrome on screen.");
            foreach (var piece in pieces)
            {
                Assert.That(hud.Covers(piece.center), Is.True, where + ": the chrome covers the middle of its own piece at " + piece + ".");
                Assert.That(hud.CoversAny(piece), Is.True, where + ": and the piece at " + piece + ".");
            }
        }

        /// <summary>
        /// Over the house with an offer waiting (the 'offer-badges' frame), at both text sizes, from
        /// the house's own distance and pulled back over it: no icon, room name, name chip or drawn
        /// plate projects into a HUD card. With the offer's own card open - a conversation - the
        /// icons are down, Maya keeps her name over the conversation, and nobody else's plate is up
        /// under its column or its dial.
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
            yield return Settle(() => !cameraRig.IsTravelling && cameraRig.HasArrived(0.05f), 4f);
            yield return Frames(2);
            Assert.That(director.IsPanelOpen, Is.True, "The offer's card is a panel.");
            Assert.That(director.IsHouseUnderChrome, Is.True, "A panel over the house is the gate.");
            Assert.That(director.TravelBeacons.IsShowing, Is.False, "The icons are down under the panel.");
            Assert.That(maya.PlateAlpha, Is.GreaterThan(.01f), "The one you are talking to keeps her name over the conversation (mockup-12).");
            AssertNothingOfTheHouseUnderTheChrome("The offer's card");
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// Through a capture's lens - every overlay drawn through the view camera into a target, as
        /// the look sheet's frames are - the chrome covers what it stands over and nothing of the
        /// house projects into it, on the 16:9 frame and the 4:3: over the house with an offer
        /// waiting (the 'offer-badges' frame), on the overview's map, and under its briefing (the
        /// 'overview-dashboard' frame), where every room chip is down. Read as an overlay, a camera
        /// canvas covered nothing (the play sweep's row 10).
        /// </summary>
        [UnityTest]
        public IEnumerator ChromeGate_ThroughACaptureLensTheChromeCoversWhatItStandsOver()
        {
            HoldTheHouseForTheFixture();
            yield return SettleCast();
            var maya = SceneComponents<HouseNpc>().Single(npc => npc.Id == ContentCatalog.MayaId);
            PutBeforeThePlayer(live => live.deals.Add(WaitingOffer(live, maya.Id, DealKind.SafetyAgreement, "deal-ask-lens")));
            director.ClosePanels();
            yield return Frames(3);
            yield return PullBackOverTheHouse();
            yield return Frames(3);
            yield return AtBothFrames(frame =>
            {
                string where = "The house from above through the capture's lens on the " + frame + " frame";
                Assert.That(director.TravelBeacons.IsShowing, Is.True, where + ": the rooms carry their icons.");
                AssertTheChromeCoversWhatItStandsOver(where);
                AssertNothingOfTheHouseUnderTheChrome(where);
            });

            Assert.That(director.ToggleOverview(), Is.True);
            yield return WaitForTheOverviewLens();
            Assert.That(director.IsBriefing, Is.True, "The rail's row is the briefing over the map.");
            yield return AtBothFrames(frame =>
            {
                string where = "The briefing through the capture's lens on the " + frame + " frame";
                Assert.That(SceneComponents<RoomLabels>().Single().ShownCount, Is.EqualTo(0), where + ": every room chip is down under it.");
                AssertTheChromeCoversWhatItStandsOver(where);
                AssertNothingOfTheHouseUnderTheChrome(where);
            });

            ButtonWithCaption(EpisodeDirector.ShowMapCaption).onClick.Invoke();
            yield return WaitForTheOverviewLens();
            Assert.That(director.IsOverview && !director.IsBriefing, Is.True, "The map is up without the briefing.");
            yield return AtBothFrames(frame =>
            {
                string where = "The map through the capture's lens on the " + frame + " frame";
                Assert.That(SceneComponents<RoomLabels>().Single().ShownCount, Is.GreaterThan(0), where + ": the map's chips are up.");
                AssertTheChromeCoversWhatItStandsOver(where);
                AssertNothingOfTheHouseUnderTheChrome(where);
            });
            director.EndOverview();
            yield return Frames(2);
        }

        /// <summary>
        /// Waits for the overview's shot and its lens to settle: the lens eases into its orthographic
        /// look after the pivot has arrived, and the chips move on screen until it has.
        /// </summary>
        private IEnumerator WaitForTheOverviewLens()
        {
            float deadline = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < deadline && !(cameraRig.HasShot && cameraRig.HasArrived(.1f) && !cameraRig.IsTravelling
                       && cameraRig.LensOrthographic > .995f))
                yield return null;
            yield return Frames(2);
            Canvas.ForceUpdateCanvases();
            Assert.That(cameraRig.LensOrthographic, Is.GreaterThan(.995f), "The overview's lens has settled.");
        }

        /// <summary>
        /// The briefing (the 'overview-dashboard' frame): every room chip is down under it and the
        /// talk prompt is not drawn over it - it drew over the recommended row - and E does nothing
        /// the prompt does not say; the recommended row stands inside the briefing, on the 16:9 frame
        /// and the 4:3 at the resting size, and once scrolled to at the larger text, where the
        /// briefing scrolls. The map put back brings the chips up except under the chrome, and the
        /// prompt with them; the player's disc stays on the map throughout.
        /// </summary>
        [UnityTest]
        public IEnumerator ChromeGate_TheBriefingHidesTheRoomChipsAndThePromptAndKeepsItsRecommendedRow()
        {
            HoldTheHouseForTheFixture();
            yield return SettleCast();
            var maya = SceneComponents<HouseNpc>().Single(npc => npc.Id == ContentCatalog.MayaId);
            var disc = player.GetComponentsInChildren<Transform>(true)
                .Where(part => part.name == EpisodeDirector.PlayerMarkerName).Select(part => part.GetComponent<Renderer>()).FirstOrDefault(renderer => renderer != null);
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
                yield return WaitForTheOverviewLens();
                Assert.That(director.IsBriefing, Is.True, "The rail's row is the briefing over the map" + where + ".");
                Assert.That(director.IsHouseUnderChrome, Is.True, "The briefing is the gate" + where + ".");
                Assert.That(ActiveRect("Interaction prompt"), Is.Null, "The prompt is not drawn over the briefing" + where + ".");
                var labels = SceneComponents<RoomLabels>().Single();
                Assert.That(labels.IsShowing, Is.True, "The map's chips exist under the briefing" + where + ",");
                Assert.That(labels.ShownCount, Is.EqualTo(0), "and every one of them is down" + where + ".");
                if (disc != null) Assert.That(disc.enabled, Is.True, "The player's disc stays: under the briefing it is the map's 'you are here'" + where + ".");
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
                yield return WaitForTheOverviewLens();
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
        /// A competition board over the yard (the 'minigame-count' and 'competition-practice' frames)
        /// takes the yard's sign, the station discs and every name plate down - through a capture's
        /// lens, the sign stands behind the board's cards and nothing the house draws reads through
        /// them; the board switched off for a frame - a capture of the yard does that - brings them
        /// back, the episode screen the attempt was opened from notwithstanding, and switched on
        /// again takes them down again. The sign keeps its place and its words throughout.
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
            Assert.That(director.IsPhasePanelOpen, Is.True, "The attempt keeps the episode screen it was opened from.");

            var sign = SceneComponents<TextMeshPro>().Single(text => text.name == EpisodeDirector.CompetitionSignName && text.gameObject.activeInHierarchy);
            var discs = SceneComponents<MeshRenderer>().Where(IsStationDisc).ToList();
            Assert.That(discs, Is.Not.Empty, "The arena marks its stations.");
            var npcs = SceneComponents<HouseNpc>().Where(npc => npc.gameObject.activeInHierarchy).ToList();
            Assert.That(npcs, Is.Not.Empty, "The house has houseguests to name.");
            AssertArenaOverlays(sign, discs, npcs, false, "under the board");
            Assert.That(sign.text, Is.Not.Empty, "The sign keeps its words under the board.");

            // The board in play, the yard framed, and the frame through a capture's lens.
            if (screen.IsAssembling)
                screen.GetComponentsInChildren<Button>().First(button => button.name == "Continue to competition").onClick.Invoke();
            yield return Settle(() => cameraRig.HasShot && !cameraRig.IsTravelling && cameraRig.HasArrived(.1f), 4f);
            yield return ThroughTheLens(1600, 900, () => AssertNoWorldLabelOnTheBoard(screen, "The practice board through the capture's lens", true));

            var surface = screen.GetComponent<Canvas>();
            surface.enabled = false;
            yield return Frames(3);
            Assert.That(screen.IsDrawn, Is.False, "A board whose canvas is off is not drawn.");
            Assert.That(director.IsHouseUnderChrome, Is.True, "The episode screen the attempt was opened from is still open,");
            AssertArenaOverlays(sign, discs, npcs, true, "with the board off");

            surface.enabled = true;
            yield return Frames(3);
            AssertArenaOverlays(sign, discs, npcs, false, "with the board back");
        }

        private static bool IsStationDisc(MeshRenderer renderer) =>
            renderer.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null && filter.sharedMesh.name == "Competition station disc";

        private void AssertArenaOverlays(TextMeshPro sign, List<MeshRenderer> discs, List<HouseNpc> npcs, bool drawn, string when)
        {
            Assert.That(director.CompetitionOverlaysShown, Is.EqualTo(drawn), "The arena's overlays are " + (drawn ? "" : "not ") + "drawn " + when + ".");
            Assert.That(sign.GetComponent<MeshRenderer>().enabled, Is.EqualTo(drawn), "The yard's sign is " + (drawn ? "" : "not ") + "drawn " + when + ".");
            foreach (var disc in discs)
                Assert.That(disc.enabled, Is.EqualTo(drawn), disc.name + " is " + (drawn ? "" : "not ") + "drawn " + when + ".");
            foreach (var npc in npcs)
                Assert.That(npc.PlateSuppressed, Is.EqualTo(!drawn), npc.DisplayName + "'s plate is " + (drawn ? "up" : "down") + " " + when + ".");
            if (!drawn)
                foreach (var npc in npcs)
                    Assert.That(npc.PlateAlpha, Is.LessThan(.01f), npc.DisplayName + "'s plate draws nothing " + when + ".");
        }

        /// <summary>The visible glyphs of a world-space text through <paramref name="eye"/>, as one screen rect; false with none, or one behind the lens.</summary>
        private static bool TryGlyphRect(TMP_Text text, Camera eye, out Rect box)
        {
            box = default;
            text.ForceMeshUpdate();
            var info = text.textInfo;
            float left = float.MaxValue, right = float.MinValue, bottom = float.MaxValue, top = float.MinValue;
            bool any = false;
            for (int i = 0; i < info.characterCount; i++)
            {
                var glyph = info.characterInfo[i];
                if (!glyph.isVisible) continue;
                foreach (var corner in new[] { glyph.bottomLeft, glyph.topLeft, glyph.topRight, glyph.bottomRight })
                {
                    var screen = eye.WorldToScreenPoint(text.transform.TransformPoint(corner));
                    if (screen.z <= 0f) return false;
                    left = Mathf.Min(left, screen.x); right = Mathf.Max(right, screen.x);
                    bottom = Mathf.Min(bottom, screen.y); top = Mathf.Max(top, screen.y);
                    any = true;
                }
            }
            if (any) box = Rect.MinMaxRect(left, bottom, right, top);
            return any;
        }

        /// <summary>A world bounds' screen rect through <paramref name="eye"/>, from its eight corners; false with one behind the lens.</summary>
        private static bool TryBoundsRect(Bounds bounds, Camera eye, out Rect box)
        {
            box = default;
            float left = float.MaxValue, right = float.MinValue, bottom = float.MaxValue, top = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? bounds.min.x : bounds.max.x, (i & 2) == 0 ? bounds.min.y : bounds.max.y, (i & 4) == 0 ? bounds.min.z : bounds.max.z);
                var screen = eye.WorldToScreenPoint(corner);
                if (screen.z <= 0f) return false;
                left = Mathf.Min(left, screen.x); right = Mathf.Max(right, screen.x);
                bottom = Mathf.Min(bottom, screen.y); top = Mathf.Max(top, screen.y);
            }
            box = Rect.MinMaxRect(left, bottom, right, top);
            return true;
        }

        /// <summary>
        /// No world label reads through a competition board's cards (G0's minigame frames): no
        /// world-space text that is drawn, no name plate that is drawn and no station disc that is
        /// drawn projects into one, through the lens the frame is drawn with. With
        /// <paramref name="signBehind"/>, the yard's sign is asked to stand behind one of the cards
        /// first, so the frame is one the check means something in.
        /// </summary>
        private void AssertNoWorldLabelOnTheBoard(CompetitionGameScreen board, string where, bool signBehind)
        {
            Canvas.ForceUpdateCanvases();
            var cards = board.GetComponentsInChildren<RectTransform>()
                .Where(rect => BoardCardNames.Contains(rect.name)).Select(ScreenBox).ToList();
            Assert.That(cards, Is.Not.Empty, where + ": the board stands over the yard.");
            var eye = cameraRig.ViewCamera;
            if (signBehind)
            {
                var sign = SceneComponents<TextMeshPro>().SingleOrDefault(text => text.name == EpisodeDirector.CompetitionSignName && text.gameObject.activeInHierarchy);
                Assert.That(sign, Is.Not.Null, where + ": the arena has its sign.");
                Assert.That(TryGlyphRect(sign, eye, out var behind) && cards.Any(card => card.Overlaps(behind)), Is.True,
                    where + ": the sign stands behind the board, so the frame is one the check means something in: sign "
                    + behind + ", cards " + string.Join(", ", cards) + ".");
            }
            foreach (var text in SceneComponents<TextMeshPro>().Where(item => item.gameObject.activeInHierarchy && item.enabled))
            {
                var renderer = text.GetComponent<MeshRenderer>();
                if (renderer == null || !renderer.enabled || !TryGlyphRect(text, eye, out var box)) continue;
                foreach (var card in cards)
                    Assert.That(box.Overlaps(card), Is.False, where + ": '" + text.text + "' (" + text.name + ") at " + box + " reads through the board's card at " + card + ".");
            }
            foreach (var npc in SceneComponents<HouseNpc>().Where(actor => actor.gameObject.activeInHierarchy && actor.PlateAlpha > .01f))
                if (npc.TryPlateScreenRect(eye, out var plate))
                    foreach (var card in cards)
                        Assert.That(plate.Overlaps(card), Is.False, where + ": " + npc.DisplayName + "'s plate at " + plate + " reads through the board's card at " + card + ".");
            foreach (var disc in SceneComponents<MeshRenderer>().Where(item => item.enabled && item.gameObject.activeInHierarchy && IsStationDisc(item)))
                if (TryBoundsRect(disc.bounds, eye, out var mark))
                    foreach (var card in cards)
                        Assert.That(mark.Overlaps(card), Is.False, where + ": " + disc.name + " at " + mark + " shows through the board's card at " + card + ".");
        }

        /// <summary>
        /// Runs <paramref name="check"/> with every overlay canvas drawn through the view camera into
        /// a target of this size, as a capture draws the frame, the HUD rendered for it. Unlike
        /// <see cref="AtFrame"/> it asks nothing of the HUD's shape, so it holds while the HUD stands
        /// down for a competition. Everything is put back afterwards.
        /// </summary>
        private IEnumerator ThroughTheLens(int width, int height, System.Action check)
        {
            var camera = cameraRig.ViewCamera;
            var overlays = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(canvas => canvas.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            var texture = new RenderTexture(width, height, 24);
            var previousTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = texture;
                foreach (var canvas in overlays)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = Mathf.Max(camera.nearClipPlane + 0.1f, 1f);
                }
                Canvas.ForceUpdateCanvases();
                yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                RenderHudForTheCurrentCanvas();
                yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                check();
            }
            finally
            {
                camera.targetTexture = previousTarget;
                foreach (var canvas in overlays)
                    if (canvas != null) canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                texture.Release();
                Object.Destroy(texture);
            }
            Canvas.ForceUpdateCanvases();
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            RenderHudForTheCurrentCanvas();
            yield return null;
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
            yield return UnderANonInteractableColumn(EpisodeHud.RecentEventsCardName, card => AssertLinkLegible(card, EpisodeHud.ViewAllEventsCaption));

            var npc = SceneComponents<HouseNpc>().First(actor => actor.gameObject.activeInHierarchy);
            director.FollowHouseguest(npc.Id);
            director.ClosePanels();
            yield return Frames(2);
            yield return UnderANonInteractableColumn(EpisodeHud.RelationshipsCardName, card => AssertLinkLegible(card, EpisodeHud.SeeAllRelationshipsCaption));
            director.FollowHouseguest(npc.Id);
            yield return null;
        }

        /// <summary>
        /// The jury strip's door at three keeps its words legible under a column that cannot be
        /// pressed, as the other corner links do: the 'endgame-final-three' frame drew it as a grey
        /// chip with its words lost in it.
        /// </summary>
        [UnityTest]
        public IEnumerator ChromeGate_TheJuryDoorKeepsItsWordsLegibleUnderANonInteractableColumn()
        {
            yield return InstallTheFinalThree();
            director.ClosePanels();
            yield return Frames(2);
            Assert.That(director.JuryStripIsADoor(director.Snapshot), Is.True, "At three the jury strip is a door.");
            yield return UnderANonInteractableColumn(EpisodeHud.ObjectivesCardName, card => AssertLinkLegible(card, EpisodeDirector.JuryStripCaption));
        }

        /// <summary>
        /// The card by this name under a group that cannot be pressed, held there until Unity's own
        /// tint has had its time to cross over. A render in the wait rebuilds the card without the
        /// group, so the last live copy is taken again after it, and the wait starts over on a new
        /// copy until one is the same card on both sides of it.
        /// </summary>
        private IEnumerator UnderANonInteractableColumn(string cardName, System.Action<RectTransform> check)
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                var card = LastActive(cardName);
                Assert.That(card, Is.Not.Null, "'" + cardName + "' is up.");
                var group = card.GetComponent<CanvasGroup>();
                if (group == null) group = card.gameObject.AddComponent<CanvasGroup>();
                group.interactable = false;
                yield return RealSeconds(.3f);
                if (LastActive(cardName) != card) continue;
                check(card);
                yield break;
            }
            Assert.Fail("'" + cardName + "' was rebuilt under every wait.");
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

        /// <summary>
        /// E and the mouse still open the episode screen at the station with the gate in Interact.
        /// The rail's "Go to episode screen" warps the player there from the yard, and then the
        /// prompt's own button, E, and the rail's button pressed again each open it. A board stood
        /// over the house is the gate: the prompt says nothing and E does nothing under it. Gone, it
        /// holds nothing - a gate that outlived what it stood for once swallowed every input in the
        /// house (a ceremony card's stamp from a session before).
        /// </summary>
        [UnityTest]
        public IEnumerator ChromeGate_EAndTheMouseStillOpenTheEpisodeScreenAndAGoneBoardHoldsNothing()
        {
            HoldTheHouseForTheFixture();
            yield return ToTheScreenFromTheYard();
            ButtonWithCaption(EpisodeHud.InteractCaption).onClick.Invoke();
            yield return null;
            Assert.That(director.IsPhasePanelOpen, Is.True, "A click on the prompt at the screen opens it.");

            yield return ToTheScreenFromTheYard();
            director.Interact();
            yield return null;
            Assert.That(director.IsPhasePanelOpen, Is.True, "E at the screen opens it.");

            yield return ToTheScreenFromTheYard();
            ButtonWithCaption("Go to episode screen").onClick.Invoke();
            yield return null;
            Assert.That(director.IsPhasePanelOpen, Is.True, "The rail's button pressed again at the screen opens it.");

            yield return ToTheScreenFromTheYard();
            var owner = new GameObject("Gate probe owner");
            var probe = CompetitionGameScreen.Attach(owner);
            try
            {
                var definition = CompetitionDefinitions.All[0];
                var run = new MiniGameRun(CompetitionMiniGames.For(definition.Category), 7, CompetitionMiniGames.CurrentRules, definition);
                probe.Show(run, "Gate probe", "You", true, i => run.Flip(i), () => run.Tap(), d => run.Tap(d), () => run.SetHolding(!run.Holding), () => { });
                yield return Frames(2);
                Assert.That(director.IsHouseUnderChrome, Is.True, "A board drawn over the house is the gate.");
                Assert.That(ButtonWithCaptionOrNull(EpisodeHud.InteractCaption), Is.Null, "The prompt says nothing under it,");
                director.Interact();
                yield return null;
                Assert.That(director.IsPhasePanelOpen, Is.False, "and E does nothing.");
            }
            finally
            {
                Object.Destroy(probe.gameObject);
                Object.Destroy(owner);
            }
            yield return Frames(2);
            Assert.That(CompetitionGameScreen.AnyDrawn, Is.False, "A board destroyed is drawn nowhere,");
            Assert.That(director.IsHouseUnderChrome, Is.False, "and holds nothing over the house.");
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.InteractCaption), Is.Not.Null, "The prompt is back,");
            director.Interact();
            yield return null;
            Assert.That(director.IsPhasePanelOpen, Is.True, "and E opens the screen again.");
            director.ClosePanels();
            yield return null;
        }

        /// <summary>Closes everything and sends the player from the yard to the episode screen by the rail's button, which warps them there.</summary>
        private IEnumerator ToTheScreenFromTheYard()
        {
            director.ClosePanels();
            WarpPlayer(new Vector3(0f, 0f, 14f));
            yield return null;
            ButtonWithCaption("Go to episode screen").onClick.Invoke();
            Assert.That(director.LastTravel, Is.EqualTo(EpisodeDirector.TravelKind.Warp), "From the yard the rail's button warps the player to the screen.");
            yield return Frames(2);
            Assert.That(director.IsPhasePanelOpen, Is.False, "Arriving by the button opens nothing by itself.");
        }
    }
}
