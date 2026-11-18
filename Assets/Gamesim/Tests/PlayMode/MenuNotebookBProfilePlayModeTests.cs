using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Menu slice 3, part b, on the built screens: a name on a reading page opens that houseguest's
    /// profile as a page reached from the page (ruling N6 ii) - "Back to your notes", "Back to
    /// Relationships" - and Escape or the way back returns there; trust is the one chip (N6 iii) on
    /// the houseguests' table, the notes and the web; and the table's first row "You" opens the
    /// player's own card (N6 iv). Each case opens the notebook itself from the house, reads nothing
    /// that depends on where the house's people stand, and closes what it opened.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator MenuNotebookB_ANameOnTheNotesOpensTheProfile_AndEscapeGoesBackToTheNotes()
        {
            yield return SettleCast();
            director.ShowNotebookSection(EpisodeDirector.NotebookSection.Notes);
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            var state = director.Snapshot;
            var actor = state.contestants.First(c => !c.isPlayer);
            var card = LastActive(EpisodeHud.NotesCardPrefix + actor.name);
            Assert.That(card, Is.Not.Null, actor.name + " has a card on the notes.");

            // Where you stand is the trust chip, not the web's five-band word beside the status.
            var chips = card.GetComponentsInChildren<RectTransform>().Where(rect => rect.name == TrustChip.ChipName).ToArray();
            Assert.That(chips, Has.Length.EqualTo(1), "One trust chip on the card.");
            var words = chips[0].GetComponentsInChildren<TMP_Text>();
            Assert.That(words.Single(t => t.name == TrustChip.FigureName).text, Is.EqualTo(TrustChip.Figure(state.Score(state.playerId, actor.id))));
            Assert.That(words.Single(t => t.name == TrustChip.LabelName).text, Is.EqualTo(TrustChip.Label), "Said in words, not by colour alone.");
            string old = RelationshipWeb.StandingWord(RelationshipWeb.KindOf(state, actor.id)) + "  ·  " + EpisodeDirector.StatusWord(actor.status);
            Assert.That(card.GetComponentsInChildren<TMP_Text>().Select(t => t.text), Has.None.EqualTo(old), "The old standing line is gone.");

            var link = card.GetComponentsInChildren<Button>().SingleOrDefault(button => button.name == EpisodeHud.ProfileLinkPrefix + actor.name);
            Assert.That(link, Is.Not.Null, "Their name is a link.");
            Assert.That(link.GetComponent<TMP_Text>().text, Is.EqualTo(actor.name), "Its caption is their name.");
            link.onClick.Invoke();
            yield return Frames(2);
            Assert.That(director.ProfileId, Is.EqualTo(actor.id), "The name opens their profile.");
            Assert.That(director.ProfileReturnsTo, Is.EqualTo(EpisodeDirector.NotebookSection.Notes));
            AssertPageWayBack(EpisodeDirector.ProfileBackCaptionFor(EpisodeDirector.NotebookSection.Notes), "A profile opened from the notes");
            Assert.That(EpisodeDirector.ProfileBackCaptionFor(EpisodeDirector.NotebookSection.Notes), Is.EqualTo("Back to your notes"));

            yield return PressKey(Key.Escape);
            yield return null;
            Assert.That(director.ProfileId, Is.Null, "Escape goes back one page,");
            Assert.That(director.IsPanelOpen, Is.True, "leaves the notebook open,");
            Assert.That(director.ActiveSection, Is.EqualTo(EpisodeDirector.NotebookSection.Notes), "and on the notes it came from.");
            Assert.That(LastActive(EpisodeHud.NotesCardPrefix + actor.name), Is.Not.Null);
            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator MenuNotebookB_TheWebsChosenHouseguestLinksToTheProfile_AndItsWayBackReturnsToTheWeb()
        {
            yield return SettleCast();
            RelationshipWeb.ClearSelection();
            var state = director.Snapshot;
            var actor = state.contestants.First(c => !c.isPlayer && c.status == ContestantStatus.Active);
            RelationshipWeb.Select(state, actor.id);
            director.ShowNotebookSection(EpisodeDirector.NotebookSection.Network);
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            state = director.Snapshot;

            var column = LastActive(RelationshipWeb.ColumnName);
            Assert.That(column, Is.Not.Null, "The web's column reads the chosen houseguest.");
            var chips = column.GetComponentsInChildren<RectTransform>().Where(rect => rect.name == TrustChip.ChipName).ToArray();
            Assert.That(chips, Has.Length.EqualTo(1), "Your trust in them is one chip.");
            Assert.That(chips[0].GetComponentsInChildren<TMP_Text>().Single(t => t.name == TrustChip.FigureName).text,
                Is.EqualTo(TrustChip.Figure(state.Score(state.playerId, actor.id))));
            Assert.That(column.GetComponentsInChildren<TMP_Text>().Select(t => t.text), Has.None.EqualTo("Standing"),
                "The Standing tile, which said the same value in a word of its own, is gone.");

            var row = LastActive(RelationshipWeb.ProfileRowPrefix + actor.id);
            Assert.That(row, Is.Not.Null, "A link to their profile under their head.");
            Assert.That(row.GetComponentsInChildren<TMP_Text>().Select(t => t.text),
                Does.Contain(RelationshipWeb.ProfileLinkCaption(CharacterNames.FirstName(actor.name))));
            var press = row.GetComponent<Button>();
            Assert.That(press, Is.Not.Null, "The link is a control.");
            press.onClick.Invoke();
            yield return Frames(2);
            Assert.That(director.ProfileId, Is.EqualTo(actor.id));
            var back = AssertPageWayBack(EpisodeDirector.ProfileBackCaptionFor(EpisodeDirector.NotebookSection.Network), "A profile opened from the web");
            Assert.That(WayOut.CaptionOf(back), Is.EqualTo("Back to Relationships"));
            back.onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.ProfileId, Is.Null, "The way back leaves the profile");
            Assert.That(director.ActiveSection, Is.EqualTo(EpisodeDirector.NotebookSection.Network), "for the web it came from.");
            director.ClosePanels();
            yield return null;
            RelationshipWeb.ClearSelection();
        }

        [UnityTest]
        public IEnumerator MenuNotebookB_TheTablesTrustIsTheChip_AndItsFirstRowYouOpensYourCard()
        {
            yield return SettleCast();
            director.ShowNotebookSection(EpisodeDirector.NotebookSection.People);
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            var state = director.Snapshot;
            var you = state.Find(state.playerId);

            // Every row's YOUR TRUST is the chip: the figure, and a shape - a level bar at zero, a mark otherwise.
            var rows = director.GetComponentsInChildren<RectTransform>()
                .Where(rect => rect.name.StartsWith(EpisodeHud.RosterRowPrefix, System.StringComparison.Ordinal)).ToArray();
            Assert.That(rows, Is.Not.Empty);
            foreach (var houseguest in rows)
            {
                var actor = state.contestants.Single(c => EpisodeHud.RosterRowPrefix + c.name == houseguest.name);
                double score = state.Score(state.playerId, actor.id);
                var chip = houseguest.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == TrustChip.ChipName);
                Assert.That(chip.GetComponentsInChildren<TMP_Text>().Single(t => t.name == TrustChip.FigureName).text, Is.EqualTo(TrustChip.Figure(score)));
                Assert.That(chip.Find(TrustChip.ShapeName).childCount, Is.EqualTo(TrustChip.BandOf(score) == TrustChip.Band.Even ? 1 : 2),
                    actor.name + ": the shape says the band as well as the colour.");
            }

            // The first row is yours, under the column heads and above everyone else's.
            var yours = LastActive(EpisodeHud.YourRowName);
            Assert.That(yours, Is.Not.Null, "The table has your own row.");
            Assert.That(yours.GetComponentsInChildren<TMP_Text>().First().text, Is.EqualTo(EpisodeHud.YourRowCaption), "Its caption is \"You\".");
            Assert.That(yours.GetComponentsInChildren<RectTransform>().Any(rect => rect.name == TrustChip.ChipName), Is.False, "No trust in yourself.");
            Assert.That(rows.All(row => row.parent == yours.parent && row.GetSiblingIndex() > yours.GetSiblingIndex()), Is.True, "It is the first row.");
            var heads = LastActive("Houseguest columns");
            Assert.That(heads != null && heads.GetSiblingIndex() < yours.GetSiblingIndex(), Is.True, "under the column heads.");

            yours.GetComponent<Button>().onClick.Invoke();
            yield return Frames(2);
            Canvas.ForceUpdateCanvases();
            Assert.That(director.ProfileId, Is.EqualTo(you.id), "The row opens your card.");
            Assert.That(director.ActiveSection, Is.EqualTo(EpisodeDirector.NotebookSection.People));
            Assert.That(LastActive(EpisodeHud.NotebookHeaderName).GetComponentsInChildren<TMP_Text>().Select(t => t.text),
                Does.Contain(EpisodeDirector.YourCardTitle));
            Assert.That(LastActive(EpisodeHud.ProfileIdentityName).GetComponentsInChildren<TMP_Text>().Select(t => t.text), Does.Contain(you.name));
            Assert.That(LastActive(EpisodeHud.ProfileTrustName), Is.Null, "Your card has no trust card.");
            var week = LastActive(EpisodeHud.ProfileBlockPrefix + EpisodeDirector.YourWeekHeading);
            Assert.That(week, Is.Not.Null, "Your week, as the other screens say it.");
            Assert.That(week.GetComponentsInChildren<TMP_Text>().Select(t => t.text), Has.Some.Contains("Week " + state.week + "  ·  "),
                "The overview's week, in its words.");
            foreach (var label in LastActive(EpisodeDirector.NotebookSection.People).GetComponentsInChildren<TMP_Text>())
            {
                label.ForceMeshUpdate();
                Assert.That(label.isTextOverflowing, Is.False, "'" + label.text + "' fits its box.");
            }
            AssertPageWayBack(EpisodeHud.ProfileBackCaption, "Your card");

            yield return PressKey(Key.Escape);
            yield return null;
            Assert.That(director.ProfileId, Is.Null, "Escape goes back to the table.");
            Assert.That(LastActive(EpisodeHud.YourRowName), Is.Not.Null);
            director.ClosePanels();
            yield return null;
        }
    }
}
