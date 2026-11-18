using System;
using System.IO;
using System.Linq;
using System.Text;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Menu slice 3, part a: the notebook's tab strip "Notes | Deals &amp; promises | Alliances", the
    /// left rail's one Notebook group with Save and Settings at its foot, and one head and one foot on
    /// the notebook's pages - in words and order (<see cref="NotebookStrip"/>) and held in the source.
    ///
    /// <para>Plain C#: the cases run in the Unity-free suite as well as in EditMode (the source root
    /// from <c>Application.dataPath</c> in the editor, by walking up from the binary in the subset).
    /// The cases that hold the strip's names to the director's need the Unity assemblies and run in
    /// EditMode only.</para>
    /// </summary>
    public sealed class MenuNotebookStripATests
    {
        [Test]
        public void TheStrip_IsNotesDealsAndAlliances_InThatOrder()
        {
            Assert.That(NotebookStrip.Tabs.Select(tab => tab.Caption), Is.EqualTo(new[] { "Notes", "Deals & promises", "Alliances" }));
            Assert.That(NotebookStrip.Tabs.Select(tab => tab.Section),
                Is.EqualTo(new[] { NotebookStrip.NotesSection, NotebookStrip.DealsSection, NotebookStrip.AlliancesSection }));
        }

        [Test]
        public void EachPageOfTheGroup_LightsExactlyItsOwnTab_AndNoOtherPageHasTheStrip()
        {
            for (int i = 0; i < NotebookStrip.Tabs.Count; i++)
            {
                Assert.That(NotebookStrip.Lit(NotebookStrip.Tabs[i].Section), Is.EqualTo(i), NotebookStrip.Tabs[i].Caption + " lights its own tab.");
                Assert.That(NotebookStrip.Carries(NotebookStrip.Tabs[i].Section), Is.True);
            }
            foreach (var other in new[] { NotebookStrip.PeopleSection, NotebookStrip.NetworkSection, NotebookStrip.RoomsSection,
                NotebookStrip.VotesSection, NotebookStrip.StorySection, null, "" })
            {
                Assert.That(NotebookStrip.Lit(other), Is.EqualTo(-1), (other ?? "null") + " is no page of the strip's group.");
                Assert.That(NotebookStrip.Carries(other), Is.False);
            }
        }

        [Test]
        public void LeftAndRight_StepAlongTheStrip_AndStopAtItsEnds()
        {
            Assert.That(NotebookStrip.Step(0, 1, 3), Is.EqualTo(1));
            Assert.That(NotebookStrip.Step(1, 1, 3), Is.EqualTo(2));
            Assert.That(NotebookStrip.Step(2, 1, 3), Is.EqualTo(2), "Right on the last tab stays there: the strip does not wrap.");
            Assert.That(NotebookStrip.Step(2, -1, 3), Is.EqualTo(1));
            Assert.That(NotebookStrip.Step(0, -1, 3), Is.EqualTo(0), "Left on the first tab stays there.");
            Assert.That(NotebookStrip.Step(0, 1, 0), Is.EqualTo(-1), "No tabs, nowhere to go.");
        }

        [Test]
        public void TheRail_HasOneNotebookGroup_WithDealsAndPromises_AndSaveAndSettingsAtItsFoot()
        {
            Assert.That(NotebookStrip.RailHeading, Is.EqualTo("Notebook"));
            Assert.That(NotebookStrip.RailRows.Select(row => row.Caption), Is.EqualTo(new[]
                { "Notebook [J]", "Deals & promises", "Houseguests", "Relationships", "Who is where", "The vote", "The story so far" }),
                "The notebook's rows in reading order: the notes and Deals & promises, then the pages the rail always had, their captions unchanged.");
            Assert.That(NotebookStrip.RailRows.Select(row => row.Section), Is.EqualTo(new[]
                { NotebookStrip.NotesSection, NotebookStrip.DealsSection, NotebookStrip.PeopleSection, NotebookStrip.NetworkSection,
                  NotebookStrip.RoomsSection, NotebookStrip.VotesSection, NotebookStrip.StorySection }), "Each row opens the page it always opened.");
            Assert.That(NotebookStrip.RailRows.Select(row => row.Caption), Has.None.EqualTo("Save [F5]").And.None.EqualTo("Settings").And.None.EqualTo("Overview"),
                "Save, Settings and the overview are not pages of the notebook.");
            Assert.That(NotebookStrip.FootRows, Is.EqualTo(new[] { "Save [F5]", "Settings" }));
            Assert.That(NotebookStrip.DealsTab, Is.EqualTo(NotebookStrip.RailRows[1].Caption), "The rail's row and the strip's tab are one name.");
        }

        [Test]
        public void TheGroupsPages_HaveNoWayBackBetweenThem_AndTheNotesNoDoorInTheirHead()
        {
            Assert.That(Code(Read("Episode/EpisodeDirector.Notes.cs")), Does.Not.Contain("PageDoor("), "The strip's tab took the door's place in the notes' head.");
            string word = Code(Read("Episode/EpisodeDirector.YourWord.cs"));
            Assert.That(word, Does.Not.Contain("WayBack("), "Deals & promises is the notes' sibling: no way back to them.");
            Assert.That(word, Does.Not.Contain("BackToYourNotesCaption"));
            Assert.That(Code(Read("Episode/EpisodeDirector.Alliances.cs")), Does.Not.Contain("WayBack("), "Nor from Alliances.");
            string tabs = Code(Read("Episode/EpisodeDirector.NotebookTabs.cs"));
            Assert.That(tabs, Does.Contain("hud.NotebookTabStrip(tabs)"), "The strip is drawn by the HUD's one strip.");
            Assert.That(tabs, Does.Contain("ShowNotebookSection(target)"), "A tab shows its page as the rail does.");
        }

        [Test]
        public void TheNotebooksPages_HaveOneHeadAndOneFoot()
        {
            string director = Code(Read("Episode/EpisodeDirector.cs"));
            int start = director.IndexOf("if (journalOpen)", StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThan(0), "The notebook's branch of the render.");
            int end = director.IndexOf("hud.ApplyPendingScroll();", start, StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start));
            string branch = director.Substring(start, end - start);
            Assert.That(branch, Does.Contain("hud.ReadingHeader("), "Every page under the reading frame's head.");
            Assert.That(branch, Does.Not.Contain("ScreenHeader("), "No second head.");
            Assert.That(branch, Does.Not.Contain("PanelTitle("), "The web and the season lost the old title under the head.");
            Assert.That(branch, Does.Not.Contain("hud.Action("), "and the command at their top: the house activities are in the foot.");
            Assert.That(branch.Split(new[] { "hud.NotebookFooter(" }, StringSplitOptions.None).Length - 1, Is.EqualTo(2),
                "The web and the season end in the notebook's one foot, as the other pages do.");
            Assert.That(branch, Does.Contain("NotebookTabs(journalSection)"), "The strip, on the pages of its group.");
        }

        [Test]
        public void TheRailIsDrawnFromTheGroups_WithItsOldLabelGone()
        {
            string chrome = Code(Read("Episode/EpisodeHud.Chrome.cs"));
            Assert.That(chrome, Does.Not.Contain("\"NOTEBOOK & SETTINGS\""), "Save and Settings sit apart from the notebook, under no notebook label.");
            Assert.That(chrome, Does.Contain("NotebookStrip.RailRows"));
            Assert.That(chrome, Does.Contain("NotebookStrip.RailHeading"));
            Assert.That(chrome, Does.Contain("NotebookStrip.FootRows"));
            Assert.That(chrome, Does.Not.Contain("RailButton(navigation, \"Notebook [J]\""), "The notebook's row is in the notebook's group now.");
        }

        [Test]
        public void TheConversationsWayBack_SaysTheTopics()
        {
            Assert.That(Code(Read("Episode/EpisodeDirector.ConversationNavigation.cs")),
                Does.Contain("ConversationTopicsBackCaption = WayOut.BackPrefix + \"the topics\""), "As every other way back says the place: \"Back to the topics\".");
        }

        [Test]
        public void TheToursRailStep_SaysTheRailAsItIsNow()
        {
            string tour = Read("Presentation/HouseTutorial.cs");
            int start = tour.IndexOf("new Step(\"sidemenu\"", StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThan(0), "The tour's step on the side rail.");
            int end = tour.IndexOf("new Step(", start + 1, StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start));
            string step = tour.Substring(start, end - start);
            foreach (var row in NotebookStrip.RailRows.Where(row => row.Caption != NotebookStrip.NotebookRow))
                Assert.That(step, Does.Contain(row.Caption), "The step names every row of the Notebook group: " + row.Caption + ".");
            Assert.That(step, Does.Contain(NotebookStrip.RailHeading + ":"), "and names the group by its heading.");
            Assert.That(step, Does.Contain("Save and Settings are at its foot"), "Only Save and Settings are at the rail's foot.");
            Assert.That(step, Does.Not.Contain("Notebook, Save"), "The notebook's row heads its group now, not the foot.");
        }

#if UNITY_5_3_OR_NEWER
        [Test]
        public void TheStripsNames_AreTheDirectorsNames()
        {
            Assert.That(NotebookStrip.NotesSection, Is.EqualTo(Gamesim.Episode.EpisodeDirector.NotebookSection.Notes));
            Assert.That(NotebookStrip.DealsSection, Is.EqualTo(Gamesim.Episode.EpisodeDirector.NotebookSection.Word));
            Assert.That(NotebookStrip.AlliancesSection, Is.EqualTo(Gamesim.Episode.EpisodeDirector.NotebookSection.Alliances));
            Assert.That(NotebookStrip.PeopleSection, Is.EqualTo(Gamesim.Episode.EpisodeDirector.NotebookSection.People));
            Assert.That(NotebookStrip.NetworkSection, Is.EqualTo(Gamesim.Episode.EpisodeDirector.NotebookSection.Network));
            Assert.That(NotebookStrip.RoomsSection, Is.EqualTo(Gamesim.Episode.EpisodeDirector.NotebookSection.Rooms));
            Assert.That(NotebookStrip.VotesSection, Is.EqualTo(Gamesim.Episode.EpisodeDirector.NotebookSection.Votes));
            Assert.That(NotebookStrip.StorySection, Is.EqualTo(Gamesim.Episode.EpisodeDirector.NotebookSection.Story));
            Assert.That(NotebookStrip.DealsTab, Is.EqualTo(Gamesim.Episode.EpisodeDirector.DealsAndPromisesName), "One name for deals.");
            Assert.That(NotebookStrip.DealsTab, Is.EqualTo(Gamesim.Episode.EpisodeDirector.YourWordCaption));
            Assert.That(Gamesim.Episode.EpisodeHud.TabFocusName, Is.EqualTo(TabStep.FocusName));
        }

        [Test]
        public void TheTopicsWayBack_IsAWayBackToThePlace()
        {
            Assert.That(Gamesim.Episode.EpisodeDirector.ConversationTopicsBackCaption, Is.EqualTo("Back to the topics"));
            Assert.That(WayOut.IsWayBack(Gamesim.Episode.EpisodeDirector.ConversationTopicsBackCaption), Is.True);
        }

        [Test]
        public void TheRailsHeading_IsTheSectionHeadingsSize_InABoxThatDrawsIt()
        {
            Assert.That(IconRail.HeadingSize, Is.EqualTo(Gamesim.Episode.EpisodeHud.SectionHeadingSize), "The section heading's size, the smallest a label may be.");
            Assert.That(IconRail.HeadingBand, Is.GreaterThanOrEqualTo(Math.Ceiling(IconRail.HeadingSize * 1.3f + 4f) + 3f),
                "The band holds the heading's box, 1.3 times its type and more, and the divider under it.");
            Assert.That(IconRail.Height(8, 1f, true) - IconRail.Height(8, 1f), Is.EqualTo(IconRail.HeadingBand).Within(.001f));
            Assert.That(IconRail.Height(8, 1.4f, false), Is.EqualTo(IconRail.Height(8, 1.4f)).Within(.001f));
        }

        /// <summary>
        /// A pad's chip stands above the top edge of the control that wears it, and "Notebook [J]",
        /// the first row under the rail's Notebook heading, wears one. The heading keeps that rise
        /// free under its divider, at every text size and every scale the rail can take there (it
        /// never grows past the text's, and never shrinks under 1): the chip's top stands below the
        /// divider, which stands below the heading's words. Without the room the chip lay across the
        /// divider at every size and over the heading's K at the largest text on the 4:3 frame.
        /// </summary>
        [Test]
        public void TheRailsHeading_KeepsThePadChipOfTheRowUnderItClearOfItsDividerAndWords()
        {
            string row = NotebookStrip.RailRows[0].Caption;
            Assert.That(row, Is.EqualTo(NotebookStrip.NotebookRow), "The notebook's row is the first under the heading,");
            Assert.That(InputGlossary.CaptionAction(row), Is.Not.Null, "and it names a key, so it wears a chip on a pad.");
            Assert.That(Gamesim.Episode.EpisodeHud.ChipRoomOver("Overview", 1.4f), Is.EqualTo(0f), "A row that names no key gets no room.");
            foreach (var size in TextSizes.All)
            {
                float text = TextSizes.Scale(size);
                float room = Gamesim.Episode.EpisodeHud.ChipRoomOver(row, text);
                Assert.That(room, Is.EqualTo(Gamesim.Episode.EpisodeHud.GlyphChipRise * text).Within(.001f), "The room is the chip's rise at " + size + " text.");
                foreach (float rail in new[] { 1f, (1f + text) * .5f, text })
                {
                    string at = size + " text, rail scale " + rail.ToString("0.###");
                    float band = IconRail.HeadingTop(1, rail);
                    float words = band + 2f * rail + IconRail.HeadingBox(rail);
                    float rule = band + IconRail.HeadingRuleOffset(rail);
                    float chipTop = IconRail.RowTop(1, rail, 1, room) - Gamesim.Episode.EpisodeHud.GlyphChipRise * text;
                    Assert.That(words, Is.LessThanOrEqualTo(rule), at + ": the heading's words stand over its divider.");
                    Assert.That(chipTop, Is.GreaterThanOrEqualTo(rule + IconRail.HeadingRuleThickness + 1f), at + ": the chip's top stands under the divider.");
                    Assert.That(IconRail.RowTop(1, rail, 1, room) - IconRail.RowTop(1, rail, 1), Is.EqualTo(room).Within(.001f), at + ": the room is all the rows move.");
                    Assert.That(IconRail.RowTop(0, rail, 1, room), Is.EqualTo(IconRail.Pad * rail).Within(.001f), at + ": the overview, over the heading, stays.");
                }
            }
        }
#endif

        private static string Read(string relative) => File.ReadAllText(Path.Combine(RuntimeRoot(), relative));

        /// <summary>A source with its comments blanked and its line breaks kept, never inside a string.</summary>
        private static string Code(string source)
        {
            var code = new StringBuilder(source.Length);
            bool block = false, text = false;
            for (var index = 0; index < source.Length; index++)
            {
                char c = source[index];
                char next = index + 1 < source.Length ? source[index + 1] : '\0';
                if (block)
                {
                    if (c == '*' && next == '/') { block = false; index++; }
                    else if (c == '\n') code.Append('\n');
                    continue;
                }
                if (text)
                {
                    code.Append(c);
                    if (c == '\\' && next != '\0') { code.Append(next); index++; }
                    else if (c == '"' || c == '\n') text = false;
                    continue;
                }
                if (c == '"') { text = true; code.Append(c); continue; }
                if (c == '\'' && index + 2 < source.Length)
                {
                    int end = source.IndexOf('\'', index + (next == '\\' ? 3 : 2));
                    if (end > index && end - index <= 4) { code.Append(source, index, end - index + 1); index = end; continue; }
                }
                if (c == '/' && next == '*') { block = true; index++; continue; }
                if (c == '/' && next == '/')
                {
                    while (index < source.Length && source[index] != '\n') index++;
                    code.Append('\n');
                    continue;
                }
                code.Append(c);
            }
            return code.ToString();
        }

        private static string RuntimeRoot()
        {
#if UNITY_EDITOR
            return Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "Gamesim", "Runtime"));
#else
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, "Assets", "Gamesim", "Runtime");
                if (Directory.Exists(candidate)) return Path.GetFullPath(candidate);
            }
            Assert.Fail("Assets/Gamesim/Runtime was not found above " + AppContext.BaseDirectory);
            return null;
#endif
        }
    }
}
