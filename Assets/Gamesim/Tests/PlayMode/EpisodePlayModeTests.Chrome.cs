using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The V2 chrome: the top bar, the right column's cards and the conversation dial.
    ///
    /// <para>What these hold to is not how the screen looks — that is judged from the look sheet
    /// against the mockups — but the two things a rebuild of the chrome can silently break: a
    /// caption that stopped being reachable, and a control that fell out of the keyboard's ring
    /// because it moved from a list into a ring of petals.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// Chrome is structure, not emphasis. With no panel open and nothing hovered or focused,
        /// the accent edge must appear nowhere: it is the colour that says "act here", and a frame
        /// where eighteen panels say it is a frame where none of them does.
        ///
        /// <para>This is the invariant the build had quietly lost. Chrome() took a Color it never
        /// read, so all seventeen call sites painted the same cyan hairline and the house could not
        /// win its own frame. The mockups are the other way round: mockup-01 gives no persistent
        /// panel a lit edge at all.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator Chrome_WearsNoAccentEdgeWhileNothingIsAskingToBeActedOn()
        {
            director.ClosePanels();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            yield return null; yield return null;

            var accent = UiTheme.Edge(UiTheme.Emphasis.Active);
            var lit = director.GetComponentsInChildren<Image>(true)
                .Where(image => image.name == "Border" && image.gameObject.activeInHierarchy)
                .Where(image => Same(image.color, accent))
                .Select(image => image.transform.parent != null ? image.transform.parent.name : "(orphan)")
                .ToArray();

            Assert.That(lit, Is.Empty,
                "Persistent chrome is wearing the act-here colour on: " + string.Join(", ", lit));

            // And the resting level is actually on them, rather than nothing being drawn at all.
            var resting = UiTheme.Edge(UiTheme.Emphasis.Resting);
            var borders = director.GetComponentsInChildren<Image>(true)
                .Where(image => image.name == "Border" && image.gameObject.activeInHierarchy).ToArray();
            Assert.That(borders, Is.Not.Empty, "The HUD should still draw panel edges.");
            Assert.That(borders.Any(image => Same(image.color, resting)), Is.True,
                "No panel is carrying the resting edge, so the level is not reaching the panels.");
        }

        private static bool Same(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < .004f && Mathf.Abs(a.g - b.g) < .004f
            && Mathf.Abs(a.b - b.b) < .004f && Mathf.Abs(a.a - b.a) < .004f;

        /// <summary>The seven seats of the dial, in the order the director fills them.</summary>
        private static readonly string[] PetalCaptions =
        {
            EpisodeHud.SmallTalkCaption,
            EpisodeHud.StrategicDiscussionCaption,
            EpisodeHud.PersonalChatCaption,
            EpisodeHud.MorePetalCaption,
            EpisodeHud.RelationshipBuildingCaption,
            EpisodeHud.ShareSecretCaption,
            "Spend time together",
        };

        /// <summary>Actions the dial does not seat. They stay ordinary rows, one level in.</summary>
        private static readonly string[] RowCaptions =
        {
            EpisodeHud.DiscussGameCaption,
            "Promise safety",
            "Share something I know",
            "Ask what they have heard",
        };

        [UnityTest]
        public IEnumerator Chrome_ConversationDialSeatsSevenPetalsAndKeepsEveryCaption()
        {
            var maya = SceneComponents<HouseNpc>().Single(npc => npc.Id == ContentCatalog.MayaId);
            yield return OpenNearbyNpc(maya);

            var dial = ActiveRect(EpisodeHud.DialName);
            Assert.That(dial, Is.Not.Null, "A conversation must be drawn around the speaker as a dial.");

            var petals = dial.GetComponentsInChildren<Button>()
                .Where(button => button.IsActive()).ToArray();
            Assert.That(petals.Length, Is.EqualTo(PetalCaptions.Length),
                "The dial seats the mockups' seven petals; it seated " + petals.Length + ".");

            // Every caption still finds exactly one control - Single() is what the suite's own
            // lookup does, so a duplicated or reworded caption fails here rather than six tests later.
            foreach (var caption in PetalCaptions)
            {
                var button = ButtonWithCaption(caption);
                Assert.That(button.transform.IsChildOf(dial), Is.True,
                    "'" + caption + "' should be a petal on the dial.");
            }
            foreach (var caption in RowCaptions)
            {
                var button = ButtonWithCaption(caption);
                Assert.That(button.transform.IsChildOf(dial), Is.False,
                    "'" + caption + "' is one of the actions the dial does not seat, so it stays a row.");
            }

            // A ring of petals is only a ring if they do not sit on one another.
            Canvas.ForceUpdateCanvases();
            yield return null;
            for (int a = 0; a < petals.Length; a++)
            for (int b = a + 1; b < petals.Length; b++)
            {
                var first = ScreenRect((RectTransform)petals[a].transform);
                var second = ScreenRect((RectTransform)petals[b].transform);
                Assert.That(first.Overlaps(second), Is.False,
                    "Petal '" + petals[a].name + "' " + first + " overlaps '" + petals[b].name + "' " + second + ".");
            }

            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Chrome_MorePetalMovesTheKeyboardToTheRowsBeneathTheDial()
        {
            var maya = SceneComponents<HouseNpc>().Single(npc => npc.Id == ContentCatalog.MayaId);
            yield return OpenNearbyNpc(maya);

            var dial = ActiveRect(EpisodeHud.DialName);
            Assert.That(dial, Is.Not.Null);

            var before = director.Snapshot;
            ButtonWithCaption(EpisodeHud.MorePetalCaption).onClick.Invoke();
            yield return null;

            var selected = EventSystem.current.currentSelectedGameObject;
            Assert.That(selected, Is.Not.Null, "The More petal must hand the keyboard somewhere.");
            Assert.That(selected.transform.IsChildOf(dial), Is.False,
                "The More petal hands the keyboard to the first row beneath the dial, not back to a petal.");

            var panel = ActiveRect("Episode panel");
            Assert.That(selected.transform.IsChildOf(panel), Is.True,
                "It must stay inside the panel; focus outside it is focus the ring cannot walk.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision),
                "Pressing More is navigation. It commits nothing and spends no action.");
            Assert.That(director.Snapshot.socialActions, Is.EqualTo(before.socialActions));

            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Chrome_TopBarSharesOneBandAndTheRightColumnStacksItsCards()
        {
            director.ClosePanels();
            // Twice, with a frame between. The brand sits inside a layout group with a size fitter
            // and the chip is anchored to the canvas: measured in the frame the HUD was rebuilt, the
            // first has not been laid out yet and the second has, and they read eight pixels apart
            // for one frame while agreeing perfectly on every frame after it.
            Canvas.ForceUpdateCanvases();
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;

            var brand = ActiveRect("Brand");
            var pill = ActiveRect("House pill");
            var week = ActiveRect("Week chip");
            var objective = ActiveRect("Objective");
            var navigation = ActiveRect("Navigation");
            var rail = ActiveRect(IconRail.RootName);
            Assert.That(brand, Is.Not.Null); Assert.That(pill, Is.Not.Null); Assert.That(week, Is.Not.Null);
            Assert.That(objective, Is.Not.Null); Assert.That(navigation, Is.Not.Null); Assert.That(rail, Is.Not.Null);

            // One band: the mockups' top bar is a row of chips, not panels at different heights -
            // and a row in the mockups' order, brand, week, objective, the house's numbers.
            float top = ScreenRect(brand).yMax;
            var chips = new[] { brand, week, objective, pill };
            foreach (var chip in chips)
                Assert.That(ScreenRect(chip).yMax, Is.EqualTo(top).Within(1f),
                    "'" + chip.name + "' sits on the top bar's band with the brand.");
            for (int i = 1; i < chips.Length; i++)
                Assert.That(ScreenRect(chips[i]).xMin, Is.GreaterThan(ScreenRect(chips[i - 1]).xMax),
                    "'" + chips[i].name + "' follows '" + chips[i - 1].name + "' along the band.");

            // And navigation belongs to the left rail, under the notebook's pages, where every
            // mockup puts it - not buttons at the end of the top bar.
            Assert.That(ScreenRect(navigation).yMax, Is.LessThanOrEqualTo(ScreenRect(rail).yMin + 1f),
                "The navigation stacks under the rail.");
            Assert.That(ScreenRect(navigation).xMin, Is.EqualTo(ScreenRect(rail).xMin).Within(1f),
                "The navigation shares the rail's gutter.");

            var events = ActiveRect(EpisodeHud.RecentEventsCardName);
            Assert.That(events, Is.Not.Null, "The right column carries a recent-events card.");
            Assert.That(ScreenRect(events).yMax, Is.LessThan(ScreenRect(brand).yMin),
                "The right column starts below the top bar.");

            // Three cards now, not two: house vibe moved out of the left column so that gutter
            // can become a navigation rail. Overlap alone would not catch the column running off
            // the bottom of the screen, and it fits by only 24 units - so measure the foot.
            var vibe = ActiveRect(EpisodeHud.HouseVibeCardName);
            Assert.That(vibe, Is.Not.Null, "The right column carries the house-vibe card.");
            Assert.That(ScreenRect(vibe).yMax, Is.LessThanOrEqualTo(ScreenRect(events).yMin + 1f),
                "House vibe stacks below recent events rather than beside or over it.");
            var band = ActiveRect("Status");
            Assert.That(band, Is.Not.Null);
            Assert.That(ScreenRect(vibe).yMin, Is.GreaterThan(ScreenRect(band).yMax),
                "The right column must clear the status band: vibe " + ScreenRect(vibe) +
                " against status " + ScreenRect(band) + ".");

            // The column is a stack. Whatever else is in the gutter, nothing may sit on the card.
            // Both cards, not just the top one: this loop checked recent events and stopped, and the
            // card that actually got sat on was the one BELOW it. The controls box grew a fifth line,
            // could not grow wider, and took the bottom out of house vibe instead - visible in every
            // screenshot, asserted by nothing, because the only card being guarded was the one the
            // box was nowhere near.
            foreach (var card in new[] { events, vibe })
            foreach (var name in new[] { "Brand", "Navigation", "Objective", "Exploration controls", "Status",
                "House pill", EpisodeDirector.LiveFeedCardName })
            {
                var panel = ActiveRect(name);
                if (panel == null || panel == card) continue;
                Assert.That(ScreenRect(card).Overlaps(ScreenRect(panel)), Is.False,
                    "'" + card.name + "' " + ScreenRect(card) +
                    " overlaps '" + name + "' " + ScreenRect(panel) + ".");
            }
        }

        [UnityTest]
        public IEnumerator Chrome_RecentEventsShowsOnlyWhatThePlayerIsAllowedToHaveSeen()
        {
            director.ClosePanels();
            yield return null;

            var state = director.Snapshot;
            var card = ActiveRect(EpisodeHud.RecentEventsCardName);
            Assert.That(card, Is.Not.Null);

            var secrets = state.events
                .Where(entry => entry.audienceIds.Count > 0 && !entry.audienceIds.Contains(state.playerId))
                .Select(entry => entry.text)
                .Where(text => !string.IsNullOrEmpty(text) && text.Length >= 24)
                .ToArray();

            var shown = card.GetComponentsInChildren<TMP_Text>(true)
                .Where(label => !string.IsNullOrEmpty(label.text))
                .Select(label => label.text)
                .ToArray();

            foreach (var secret in secrets)
                Assert.That(shown.Any(line => line.StartsWith(secret.Substring(0, 24))), Is.False,
                    "The column reported an event the player's character was not party to: " + secret);
        }

        /// <summary>
        /// The playtest's week-2 conversation with the Head of Household (2026-09-27). Its plea rows
        /// carry a portrait, a trust reading and a tag, and in the fixed 340-unit column their
        /// captions were left 20 units or less: the words stood one letter a line in rows grown 860
        /// tall. The conversation takes the stage now, and at both text sizes every row keeps room
        /// for its words, a tag that will not fit beside them going under them instead.
        /// </summary>
        [UnityTest]
        public IEnumerator Conversation_EveryRowKeepsRoomForItsWords()
        {
            foreach (bool larger in new[] { false, true })
            {
                string hoh = null;
                yield return InstallStrategySeason(41, state =>
                {
                    state.phase = EpisodePhase.Nomination;
                    hoh = state.hohId = state.Active.First(actor => !actor.isPlayer).id;
                });
                yield return ApplyTextSize(larger);
                yield return TalkTo(hoh);
                AssertRowsHaveRoom(larger, "the conversation with the Head of Household");
                var state = director.Snapshot;
                ButtonWithCaption(EpisodeHud.LobbyAskCaption(state, state.Find(hoh).name, LobbyAsk.Spare, state.playerId)).onClick.Invoke();
                yield return null;
                AssertRowsHaveRoom(larger, "the plea's approaches");
                director.ClosePanels();
                yield return null;
            }
        }

        private void AssertRowsHaveRoom(bool larger, string where)
        {
            Canvas.ForceUpdateCanvases();
            float scale = larger ? 1.2f : 1f;
            var column = ActiveRect(EpisodeHud.ConversationColumnName);
            Assert.That(column, Is.Not.Null, "A conversation is open: " + where + ".");
            var dial = ActiveRect(EpisodeHud.DialName);
            int rows = 0;
            foreach (var button in column.GetComponentsInChildren<Button>().Where(button => button.IsActive()))
            {
                // The rows: in the column's scrolling list, not the dial and not its Close.
                if (dial != null && button.transform.IsChildOf(dial) || button.GetComponentInParent<ScrollRect>() == null) continue;
                var caption = button.GetComponentsInChildren<TMP_Text>(true)
                    .FirstOrDefault(text => text.transform.parent == button.transform && text.text == button.name);
                if (caption == null || button.GetComponent<LayoutElement>() == null) continue;
                rows++;
                var row = (RectTransform)button.transform;
                string about = "'" + button.name + "' in " + where + " at " + (larger ? "larger" : "standard") + " text";
                Assert.That(caption.rectTransform.rect.width, Is.GreaterThanOrEqualTo(EpisodeHud.MinCaptionWidth * scale - .5f),
                    about + " was left " + caption.rectTransform.rect.width.ToString("0") + " units for its words in a row "
                    + row.rect.width.ToString("0") + " wide.");
                Assert.That(row.rect.height, Is.LessThan(200f * scale),
                    about + " grew " + row.rect.height.ToString("0") + " tall: its words are standing a letter a line.");
                // Everything the row draws side by side - the face, the words, the tag, the trust
                // reading, the ALLY mark - keeps to its own place.
                var pieces = row.Cast<Transform>().Select(child => (RectTransform)child)
                    .Where(rect => rect.gameObject.activeInHierarchy
                        && (rect.name == "Tag" || rect.name == "Portrait" || rect.GetComponent<TMP_Text>() != null))
                    .ToArray();
                for (int a = 0; a < pieces.Length; a++)
                for (int b = a + 1; b < pieces.Length; b++)
                    Assert.That(ScreenRect(pieces[a]).Overlaps(ScreenRect(pieces[b])), Is.False,
                        about + ": " + Piece(pieces[a]) + " and " + Piece(pieces[b]) + " are drawn over each other.");
            }
            Assert.That(rows, Is.GreaterThan(3), "The rows were found: " + where + ".");
            var panel = ActiveRect("Episode panel");
            var canvas = panel.GetComponentInParent<Canvas>().rootCanvas.GetComponent<RectTransform>();
            Assert.That(panel.rect.width, Is.GreaterThan(canvas.rect.width * .55f),
                "The conversation takes the stage, not a strip between the rail and the right column: " + where + ".");
        }

        private static string Piece(RectTransform rect)
        {
            var text = rect.GetComponentInChildren<TMP_Text>();
            return rect.name + (text != null ? " '" + text.text + "'" : "");
        }

        private RectTransform ActiveRect(string name) => director.GetComponentsInChildren<RectTransform>(true)
            .FirstOrDefault(rect => rect.name == name && rect.gameObject.activeInHierarchy);
    }
}
