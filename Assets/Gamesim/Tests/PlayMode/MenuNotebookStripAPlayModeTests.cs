using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Menu slice 3, part a: the notebook's tab strip "Notes | Deals &amp; promises | Alliances" on
    /// the three pages of one group (the review's change 6), the left rail's one Notebook group with
    /// Save and Settings apart at its foot, and the relationship web's link to the alliances.
    ///
    /// <para>Every case opens what it measures itself, from closed panels, so none depends on a case
    /// before it. Nothing here reads where the house's people stand, a revision or the wall clock;
    /// the keys are sent as the move and submit events the input module gives a control, inside one
    /// frame, so no render can re-select a control between a press and its check. The strip and the
    /// panel are found under the HUD's own Close (the live panel, never a copy a rebuild left).</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private static readonly string[] OtherNotebookPages =
        {
            EpisodeDirector.NotebookSection.People, EpisodeDirector.NotebookSection.Network, EpisodeDirector.NotebookSection.Rooms,
            EpisodeDirector.NotebookSection.Votes, EpisodeDirector.NotebookSection.Story,
        };

        [UnityTest]
        public IEnumerator MenuNotebookStripA_TheThreePagesShareOneStrip_OneTabLitByMoreThanColour_AtEveryTextSize()
        {
            var originalSize = director.TextSize;
            try
            {
                foreach (var size in TextSizes.All)
                {
                    director.SetTextSize(size);
                    director.ClosePanels();
                    yield return null;
                    string at = " at " + TextSizes.Name(size) + " text";
                    foreach (var tab in NotebookStrip.Tabs)
                    {
                        director.ShowNotebookSection(tab.Section);
                        yield return Frames(2);
                        Assert.That(director.ActiveSection, Is.EqualTo(tab.Section), tab.Caption + " is open" + at + ".");
                        AssertNotebookStrip(tab.Caption, tab.Caption + at);
                    }
                    foreach (var page in OtherNotebookPages)
                    {
                        director.ShowNotebookSection(page);
                        yield return Frames(2);
                        Assert.That(director.ActiveSection, Is.EqualTo(page));
                        Assert.That(LiveNotebookStrip(), Is.Null, page + " is no page of the strip's group" + at + ".");
                    }

                    // A click on a tab turns the page, and the page it turns to lights its own tab.
                    director.ShowNotebookSection(EpisodeDirector.NotebookSection.Alliances);
                    yield return Frames(2);
                    NotebookStripTab(NotebookStrip.NotesTab).onClick.Invoke();
                    yield return Frames(2);
                    Assert.That(director.ActiveSection, Is.EqualTo(EpisodeDirector.NotebookSection.Notes), "The Notes tab opens the notes" + at + ".");
                    Assert.That(director.IsPanelOpen, Is.True, "in the same notebook.");
                    AssertNotebookStrip(NotebookStrip.NotesTab, "The notes after a click" + at);
                }
            }
            finally
            {
                director.SetTextSize(originalSize);
                director.ClosePanels();
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator MenuNotebookStripA_LeftAndRightMoveAlongTheStrip_TheFocusIsNoSecondLitTab_AndSubmitTurnsThePage()
        {
            director.ClosePanels();
            yield return null;
            director.ShowNotebookSection(EpisodeDirector.NotebookSection.Notes);
            yield return Frames(2);
            AssertNotebookStrip(NotebookStrip.NotesTab, "The notes");

            // One frame from here to the submit: no render comes between a key and its check.
            var notes = NotebookStripTab(NotebookStrip.NotesTab);
            var deals = NotebookStripTab(NotebookStrip.DealsTab);
            var alliances = NotebookStripTab(NotebookStrip.AlliancesTab);
            foreach (var tab in new[] { notes, deals, alliances })
            {
                Assert.That(tab.navigation.mode, Is.Not.EqualTo(Navigation.Mode.None), "'" + tab.name + "' is in the keyboard ring.");
                Assert.That(tab.navigation.selectOnDown, Is.Not.Null.And.Not.SameAs(tab), "Down leaves '" + tab.name + "': no trap.");
            }
            // From nothing, so the select reaches the tab even if a render had put the focus on it.
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(notes.gameObject);
            Assert.That(TabFocusShown(notes), Is.True, "The focused tab shows the focus line.");

            Move(MoveDirection.Right);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(deals.gameObject), "Right moves to Deals & promises.");
            Assert.That(TabFocusShown(deals) && !TabFocusShown(notes), Is.True, "The line moves with the focus.");
            Assert.That(LitTabs(), Is.EqualTo(new[] { NotebookStrip.NotesTab }), "Moving the focus turns no page: the notes stay the lit tab.");
            Assert.That(deals.transform.Find(NotebookStrip.LitMarkName), Is.Null, "The focused tab never wears the lit tab's bar.");
            Assert.That(deals.colors.selectedColor, Is.EqualTo(deals.colors.normalColor), "nor a brighter ground.");

            Move(MoveDirection.Right);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(alliances.gameObject), "Right again, to Alliances.");
            Move(MoveDirection.Right);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(alliances.gameObject), "The strip does not wrap.");
            Move(MoveDirection.Left);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(deals.gameObject), "Left comes back.");
            Assert.That(director.ActiveSection, Is.EqualTo(EpisodeDirector.NotebookSection.Notes), "Still the notes.");

            ExecuteEvents.Execute(deals.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            yield return Frames(2);
            Assert.That(director.ActiveSection, Is.EqualTo(EpisodeDirector.NotebookSection.Word), "Enter or the pad's A turns to Deals & promises.");
            AssertNotebookStrip(NotebookStrip.DealsTab, "Deals & promises by the keyboard");
            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator MenuNotebookStripA_TheWebsLinkOpensAlliancesWithItsStrip_AndEscapeClosesFromThere()
        {
            RelationshipWeb.ClearSelection();
            director.ClosePanels();
            yield return null;
            try
            {
                director.ShowNotebookSection(EpisodeDirector.NotebookSection.Network);
                yield return Frames(2);
                Assert.That(LiveNotebookStrip(), Is.Null, "Relationships keeps its own page, with no strip.");
                var link = FindButton(RelationshipWeb.AlliancesDoorCaption);
                var words = link.GetComponentsInChildren<TMP_Text>().Single(text => text.text == RelationshipWeb.AlliancesDoorCaption);
                Assert.That((words.fontStyle & FontStyles.Underline) != 0, Is.True, "A plain link, underlined, not a door.");
                Assert.That(words.fontSize, Is.GreaterThanOrEqualTo(14f), "No label under 14.");
                link.onClick.Invoke();
                yield return Frames(2);
                Assert.That(director.ActiveSection, Is.EqualTo(EpisodeDirector.NotebookSection.Alliances), "The link opens the alliances page");
                AssertNotebookStrip(NotebookStrip.AlliancesTab, "Alliances by the web's link");

                yield return PressKey(Key.Escape);
                yield return null;
                Assert.That(director.IsPanelOpen, Is.False, "With no way back between siblings, Escape is Close's.");
            }
            finally
            {
                RelationshipWeb.ClearSelection();
                director.ClosePanels();
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator MenuNotebookStripA_TheRailHasOneNotebookGroup_SaveAndSettingsApart_AtEveryTextSizeAndInTheCompactHud()
        {
            var originalSize = director.TextSize;
            bool originalCompact = director.CompactHud;
            try
            {
                foreach (bool compact in new[] { false, true })
                foreach (var size in TextSizes.All)
                {
                    director.SetTextSize(size);
                    director.SetCompactHud(compact);
                    director.ClosePanels();
                    yield return Frames(2);
                    Canvas.ForceUpdateCanvases();
                    string at = (compact ? "compact HUD, " : "full HUD, ") + TextSizes.Name(size) + " text";

                    var rail = LastActiveRect(IconRail.RootName);
                    Assert.That(rail, Is.Not.Null, at + ": the rail.");
                    var rows = rail.GetComponentsInChildren<Button>().ToArray();
                    Assert.That(rows.Select(row => row.name), Is.EqualTo(new[] { "Overview", NotebookStrip.NotebookRow, "House", "Strategy", "The story so far" }),
                        at + ": the overview, then the notebook's rows in reading order.");
                    for (int i = 1; i < rows.Length; i++)
                        Assert.That(ScreenRect((RectTransform)rows[i].transform).yMax, Is.LessThanOrEqualTo(ScreenRect((RectTransform)rows[i - 1].transform).yMin + 1f),
                            at + ": '" + rows[i].name + "' stands under '" + rows[i - 1].name + "'.");

                    var heading = rail.GetComponentsInChildren<TMP_Text>().SingleOrDefault(text => text.name == IconRail.HeadingName);
                    Assert.That(heading, Is.Not.Null, at + ": one heading in the rail.");
                    Assert.That(heading.text, Is.EqualTo(NotebookStrip.RailHeading), at);
                    Assert.That((heading.fontStyle & FontStyles.UpperCase) != 0, Is.True, at + ": in the section heading's small capitals.");
                    Assert.That(heading.fontSize, Is.GreaterThanOrEqualTo(14f), at + ": no label under 14.");
                    Assert.That(heading.rectTransform.rect.height, Is.GreaterThanOrEqualTo(heading.fontSize * 1.3f - .5f), at + ": a box that draws it.");
                    Assert.That(rail.GetComponentsInChildren<RectTransform>().Any(rect => rect.name == IconRail.HeadingRuleName), Is.True, at + ": with its divider.");
                    var headingRect = ScreenRect(heading.rectTransform);
                    Assert.That(headingRect.yMin, Is.GreaterThanOrEqualTo(ScreenRect((RectTransform)rows[1].transform).yMax - 1f), at + ": over the notebook's first row");
                    Assert.That(headingRect.yMax, Is.LessThanOrEqualTo(ScreenRect((RectTransform)rows[0].transform).yMin + 1f), at + ": and under the overview.");
                    AssertEveryLabelDraws(rail, at + ", the rail");

                    var foot = LastActiveRect("Navigation");
                    Assert.That(foot, Is.Not.Null, at + ": the rail's foot.");
                    Assert.That(foot.GetComponentsInChildren<Button>().Select(button => button.name), Is.EqualTo(NotebookStrip.FootRows), at + ": Save and Settings, and nothing else.");
                    Assert.That(ScreenRect(foot).yMax, Is.LessThanOrEqualTo(ScreenRect(rail).yMin + 1f), at + ": under the notebook's group.");
                    var rule = LastActiveRect(EpisodeHud.RailFootRuleName);
                    Assert.That(rule, Is.Not.Null, at + ": set apart by a rule.");
                    Assert.That(ScreenRect(rule).yMin, Is.GreaterThanOrEqualTo(ScreenRect(foot).yMax - 1f), at + ": the rule over the foot.");
                    var next = LastActiveRect("Next move");
                    var play = compact ? LastActiveRect("Objective") : next;
                    Assert.That(play.GetComponentsInChildren<Button>().Select(button => button.name),
                        Is.EqualTo(new[] { EpisodeHud.DiaryTravelCaption, EpisodeHud.CompetitionTravelCaption, EpisodeHud.EpisodeTravelCaption }), at);
                    AssertEpisodeTravelLabel(compact, at);
                    AssertRailDisclosure("House", false);
                    AssertRailDisclosure("Strategy", false);
                    if (!compact && size == TextSize.Largest && Application.isBatchMode)
                        yield return CaptureFraming("play-episode-caption-largest-21x9", settle: false,
                            inspect: _ => AssertEpisodeTravelLabel(false, "largest, 2560x1080"), width: 2560, height: 1080);
                    if (!compact)
                    {
                        next = LastActiveRect("Next move");
                        foot = LastActiveRect("Navigation");
                        rail = LastActiveRect(IconRail.RootName);
                        Assert.That(next, Is.Not.Null, at + ": the places to go next.");
                        Assert.That(ScreenRect(foot).yMax, Is.LessThanOrEqualTo(ScreenRect(next).yMin + 1f), at + ": the foot is the rail's last group.");
                    }

                    // It fits: clear of the cast strip along the floor, and on the screen.
                    var cast = LastActiveRect(CastRail.RootName);
                    if (!compact) Assert.That(cast, Is.Not.Null, at + ": the cast strip.");
                    if (cast != null)
                        foreach (var part in new[] { rail, foot })
                            Assert.That(ScreenRect(part).Overlaps(ScreenRect(cast)), Is.False, at + ": '" + part.name + "' " + ScreenRect(part) + " runs onto the cast strip " + ScreenRect(cast) + ".");
                    Assert.That(ScreenRect(foot).yMin, Is.GreaterThanOrEqualTo(-.5f), at + ": on the screen.");
                }
            }
            finally
            {
                director.SetTextSize(originalSize);
                director.SetCompactHud(originalCompact);
                director.ClosePanels();
            }
            yield return null;
        }

        private IEnumerator AssertRailFolderInput()
        {
            var originalSize = director.TextSize;
            bool originalCompact = director.CompactHud;
            try
            {
                // The rows open what they name: Deals & promises its page, lit on the rail; the notebook's row the notes.
                director.SetTextSize(TextSize.Standard);
                director.SetCompactHud(false);
                director.ClosePanels();
                yield return Frames(2);
                var house = RailRow("House");
                EventSystem.current.SetSelectedGameObject(house.gameObject);
                ExecuteEvents.Execute(house.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                yield return Frames(2);
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(RailRow("House").gameObject), "Expand keeps keyboard focus on its group.");
                AssertRailDisclosure("House", true);
                AssertRailDisclosure("Strategy", false);
                foreach (var caption in new[] { "Houseguests", "Relationships", "Who is where" })
                    Assert.That(RailRow(caption), Is.Not.Null);
                Assert.That(RailRow("House").GetComponentsInChildren<TMP_Text>().Any(label => label.text.Contains(director.Snapshot.Active.Count().ToString())), Is.True);
                RailRow("Strategy").onClick.Invoke();
                yield return Frames(2);
                Assert.That(LastActiveRect(IconRail.RootName).GetComponentsInChildren<Button>().Any(button => button.name == "Houseguests"), Is.False);
                AssertRailDisclosure("House", false);
                AssertRailDisclosure("Strategy", true);
                Assert.That(RailRow("The vote"), Is.Not.Null);
                RailRow("Strategy").onClick.Invoke();
                yield return Frames(2);
                AssertRailDisclosure("Strategy", false);
                Assert.That(LastActiveRect(IconRail.RootName).GetComponentsInChildren<Button>().Any(button => button.name == "The vote"), Is.False);
                RailRow("Strategy").onClick.Invoke();
                yield return Frames(2);
                var mouse = TestMouse();
                var target = RailRow(NotebookStrip.DealsTab);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = ScreenCentre((RectTransform)target.transform) });
                yield return Frames(2);
                Canvas.ForceUpdateCanvases();
                target = RailRow(NotebookStrip.DealsTab);
                var pointer = ScreenCentre((RectTransform)target.transform);
                var hits = new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = pointer }, hits);
                Assert.That(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == target, Is.True,
                    "Pointer at " + pointer + " must reach Deals & promises. Hits: " + string.Join(", ", hits.Select(hit => hit.gameObject.name)));
                bool clicked = false;
                target.onClick.AddListener(() => clicked = true);
                yield return ClickAt(mouse, pointer);
                Assert.That(clicked, Is.True, "The UI module must deliver the real mouse click to the row it hit. Target still active: "
                    + (target != null && target.IsActive()) + ", point: " + pointer + ", mouse: " + mouse.position.ReadValue()
                    + ", module: " + EventSystem.current.currentInputModule + ", selected: " + EventSystem.current.currentSelectedGameObject);
                yield return Frames(2);
                Assert.That(director.ActiveSection, Is.EqualTo(EpisodeDirector.NotebookSection.Word), "The rail's Deals & promises opens its page");
                AssertNotebookStrip(NotebookStrip.DealsTab, "Deals & promises from the rail");
                Assert.That(RailRow(NotebookStrip.DealsTab).transform.Find(IconRail.ActiveMarkName), Is.Not.Null, "and its row is lit.");
                RailRow(NotebookStrip.NotebookRow).onClick.Invoke();
                yield return Frames(2);
                Assert.That(director.ActiveSection, Is.EqualTo(EpisodeDirector.NotebookSection.Notes), "\"Notebook [J]\" opens the notes, as J does.");
                Assert.That(RailRow(NotebookStrip.NotebookRow).transform.Find(IconRail.ActiveMarkName), Is.Not.Null, "and its row is lit.");
            }
            finally
            {
                director.SetTextSize(originalSize);
                director.SetCompactHud(originalCompact);
                director.ClosePanels();
            }
            yield return null;
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>The live notebook's strip: a child of the live panel itself, out of the scroll, or null.</summary>
        private RectTransform LiveNotebookStrip()
        {
            var panel = LiveReadingPanel(director.GetComponent<EpisodeHud>());
            if (panel == null) return null;
            RectTransform found = null;
            foreach (Transform child in panel)
                if (child.name == NotebookStrip.StripName && child.gameObject.activeInHierarchy) found = (RectTransform)child;
            return found;
        }

        /// <summary>The live strip's tab of this caption.</summary>
        private Button NotebookStripTab(string caption)
        {
            var strip = LiveNotebookStrip();
            Assert.That(strip, Is.Not.Null, "The notebook's strip is up.");
            var tab = strip.GetComponentsInChildren<Button>().LastOrDefault(button => button.IsActive() && button.name == caption);
            Assert.That(tab, Is.Not.Null, "The strip has a tab '" + caption + "'.");
            return tab;
        }

        /// <summary>The captions of the live strip's tabs wearing the lit tab's bar.</summary>
        private string[] LitTabs() => LiveNotebookStrip().GetComponentsInChildren<Button>()
            .Where(button => button.transform.Find(NotebookStrip.LitMarkName) != null).Select(button => button.name).ToArray();

        private static bool TabFocusShown(Button tab)
        {
            var line = tab.transform.Find(EpisodeHud.TabFocusName);
            return line != null && line.gameObject.activeSelf;
        }

        /// <summary>A move along the focused control, as the arrow keys and the pad's stick or d-pad send it.</summary>
        private static void Move(MoveDirection direction)
        {
            var focused = EventSystem.current.currentSelectedGameObject;
            Assert.That(focused, Is.Not.Null, "Something has the focus to move from.");
            var move = new AxisEventData(EventSystem.current) { moveDir = direction, moveVector = direction == MoveDirection.Left ? Vector2.left : Vector2.right };
            ExecuteEvents.Execute(focused, move, ExecuteEvents.moveHandler);
        }

        private void AssertRailDisclosure(string group, bool expanded)
        {
            var row = RailRow(group).transform;
            var label = (RectTransform)row.Find("Row label");
            var chevron = row.Find("Disclosure").GetComponent<Image>();
            Assert.That(chevron.sprite, Is.Not.Null, group);
            Assert.That(chevron.sprite, Is.SameAs(UiTheme.Pack(expanded ? PackArt.KitIconChevronDown : PackArt.KitIconChevronRight)), group);
            Assert.That(chevron.raycastTarget, Is.False, group);
            var count = row.Find("Group status") as RectTransform;
            if (group == "House")
            {
                Assert.That(count, Is.Not.Null);
                var countLabel = count.GetComponent<TMP_Text>();
                Assert.That(countLabel.text, Is.EqualTo(director.Snapshot.Active.Count().ToString()));
                Assert.That(countLabel.raycastTarget, Is.False);
                Assert.That(ScreenRect(label).xMax, Is.LessThanOrEqualTo(ScreenRect(count).xMin), group);
                Assert.That(ScreenRect(count).xMax, Is.LessThan(ScreenRect(chevron.rectTransform).xMin), group);
            }
            else
            {
                Assert.That(count, Is.Null, group);
                Assert.That(ScreenRect(label).xMax, Is.LessThan(ScreenRect(chevron.rectTransform).xMin), group);
            }
        }

        private void AssertEpisodeTravelLabel(bool compact, string at)
        {
            var container = LastActiveRect(compact ? "Objective" : "Next move");
            var button = container.GetComponentsInChildren<Button>().Single(row => row.name == EpisodeHud.EpisodeTravelCaption);
            var label = button.GetComponentsInChildren<TMP_Text>().Single(text => text.text == EpisodeHud.EpisodeTravelCaption);
            string title = compact ? EpisodeHud.EpisodeTravelCaption : "Episode";
            AssertDrawnLine(label, title, at);
            if (compact) return;

            var destination = button.transform.Find("Episode destination");
            Assert.That(destination, Is.Not.Null, at + ": room subtitle under the action.");
            var room = destination.GetComponent<TMP_Text>();
            AssertDrawnLine(room, "Nominations Room", at);
            var bounds = ScreenRect((RectTransform)button.transform);
            AssertInside(bounds, label.rectTransform, at + ": action title");
            AssertInside(bounds, room.rectTransform, at + ": room subtitle");
            Assert.That(ScreenRect(room.rectTransform).yMax, Is.LessThanOrEqualTo(ScreenRect(label.rectTransform).yMin + .5f), at);
            var mark = (RectTransform)button.transform.Find("Row mark");
            Assert.That(ScreenRect(mark).xMax, Is.LessThan(ScreenRect(label.rectTransform).xMin), at);
        }

        private void AssertDrawnLine(TMP_Text label, string expected, string at)
        {
            label.ForceMeshUpdate();
            var info = label.textInfo;
            var glyphs = info.characterInfo.Take(info.characterCount).Where(character => character.isVisible).ToArray();
            string shown = new string(glyphs.Select(character => character.character).ToArray());
            string metrics = string.Join(", ", info.lineInfo.Take(info.lineCount)
                .Select(line => $"advance={line.maxAdvance:F1} height={line.lineHeight:F1}"));
            string details = $"{at}, {label.name}: raw '{label.text}', parsed '{label.GetParsedText()}', visible '{shown}', "
                + $"rect {label.rectTransform.rect}, screen {ScreenRect(label.rectTransform)}, preferred {label.preferredWidth:F1}x{label.preferredHeight:F1}, "
                + $"font {label.font.name} at {label.fontSize:F1}, smallest glyph {(glyphs.Length == 0 ? 0f : glyphs.Min(glyph => glyph.pointSize)):F1}, "
                + $"lines [{metrics}], overflow {label.isTextOverflowing}, truncated {label.isTextTruncated}, focus {EventSystem.current.currentSelectedGameObject}";
            Assert.That(label.isTextOverflowing || label.isTextTruncated, Is.False, details);
            Assert.That(shown, Is.EqualTo(expected.Replace(" ", "")), details);
            Assert.That(info.lineCount, Is.EqualTo(1), details);
            if (director.TextSize == TextSize.Largest)
                Assert.That(glyphs.Min(glyph => glyph.pointSize), Is.GreaterThanOrEqualTo(16f), details);
        }

        /// <summary>The rail's row of this caption, on the live rail.</summary>
        private Button RailRow(string caption)
        {
            var rail = LastActiveRect(IconRail.RootName);
            Assert.That(rail, Is.Not.Null, "The rail is up.");
            var row = rail.GetComponentsInChildren<Button>().LastOrDefault(button => button.name == caption);
            Assert.That(row, Is.Not.Null, "The rail has a row '" + caption + "'.");
            return row;
        }

        /// <summary>The newest active rect of this name: a rebuild in the same frame leaves the old copy first.</summary>
        private RectTransform LastActiveRect(string name) => director.GetComponentsInChildren<RectTransform>()
            .LastOrDefault(rect => rect.name == name && rect.gameObject.activeInHierarchy);

        /// <summary>
        /// The strip on a page of its group: under the frame's head, out of the scroll (the page scrolls
        /// under it), the three tabs in order and inside the panel, each saying all of its caption and
        /// clear of Close, exactly one lit - by a bar under it as well as its ground and weight - and no
        /// way back on the page, because the tabs are siblings.
        /// </summary>
        private void AssertNotebookStrip(string lit, string where)
        {
            Canvas.ForceUpdateCanvases();
            var hud = director.GetComponent<EpisodeHud>();
            var panel = LiveReadingPanel(hud);
            Assert.That(panel, Is.Not.Null, where + ": the notebook is up.");
            Assert.That(hud.IsReadingFrame, Is.True, where + ": in the reading frame.");
            var strip = LiveNotebookStrip();
            Assert.That(strip, Is.Not.Null, where + " carries the notebook's strip.");
            Assert.That(strip.GetComponentInParent<ScrollRect>(), Is.Null, where + ": the strip does not scroll.");
            var scroll = panel.GetComponentsInChildren<ScrollRect>().FirstOrDefault(item => item.transform.parent == panel);
            Assert.That(scroll, Is.Not.Null, where + ": the page's scroll.");
            Assert.That(ScreenRect(strip).yMin, Is.GreaterThanOrEqualTo(ScreenRect(scroll.viewport).yMax - 1f), where + ": the page scrolls under the strip.");
            var head = panel.GetComponentsInChildren<RectTransform>().LastOrDefault(rect => rect.name == EpisodeHud.ReadingHeaderName);
            Assert.That(head, Is.Not.Null, where + ": the frame's one head.");
            Assert.That(ScreenRect(strip).yMax, Is.LessThanOrEqualTo(ScreenRect(head).yMin + 1f), where + ": the strip under the head.");

            var tabs = strip.GetComponentsInChildren<Button>().Where(button => button.IsActive()).ToArray();
            Assert.That(tabs.Select(tab => tab.name), Is.EqualTo(NotebookStrip.Tabs.Select(tab => tab.Caption)), where + ": Notes | Deals & promises | Alliances.");
            var close = hud.PanelClose;
            foreach (var tab in tabs)
            {
                var rect = (RectTransform)tab.transform;
                AssertInside(ScreenRect(panel), rect, where + ": '" + tab.name + "'");
                if (close != null) Assert.That(ScreenRect(rect).Overlaps(ScreenRect((RectTransform)close.transform)), Is.False, where + ": '" + tab.name + "' clears Close.");
                Assert.That(tab.IsInteractable(), Is.True, where + ": '" + tab.name + "' can be pressed.");
                var label = tab.GetComponentsInChildren<TMP_Text>().Single(text => text.text == tab.name);
                label.ForceMeshUpdate();
                Assert.That(label.isTextOverflowing || label.isTextTruncated, Is.False, where + ": '" + tab.name + "' says all its words.");
                Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(14f), where + ": no label under 14.");
                Assert.That(label.rectTransform.rect.height, Is.GreaterThanOrEqualTo(label.fontSize * 1.3f - .5f), where + ": '" + tab.name + "' in a box that draws it.");
            }
            Assert.That(LitTabs(), Is.EqualTo(new[] { lit }), where + ": exactly one tab is lit, its own.");
            var bar = (RectTransform)tabs.Single(tab => tab.name == lit).transform.Find(NotebookStrip.LitMarkName);
            Assert.That(bar.gameObject.activeInHierarchy && bar.rect.height >= 3f, Is.True, where + ": the lit tab wears a bar under it, a shape and not a colour alone.");
            var litFont = tabs.Single(tab => tab.name == lit).GetComponentInChildren<TMP_Text>().font;
            foreach (var other in tabs.Where(tab => tab.name != lit))
                if (UiTheme.Font(UiTheme.Weight.SemiBold) != null && UiTheme.Font(UiTheme.Weight.Medium) != null)
                    Assert.That(other.GetComponentInChildren<TMP_Text>().font, Is.Not.SameAs(litFont), where + ": the lit tab stands in the heavier weight.");
            AssertEveryLabelDraws(strip, where + "'s strip");
            Assert.That(panel.GetComponentsInChildren<Button>().Any(button => button.IsActive() && button.name == WayOut.BackObjectName), Is.False,
                where + ": no way back between the strip's pages.");
        }
    }
}
