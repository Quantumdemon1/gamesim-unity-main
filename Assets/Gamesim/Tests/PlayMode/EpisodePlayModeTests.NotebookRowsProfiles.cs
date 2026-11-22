using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Persistence;
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
    /// Backlog item 21 on the built screens: a houseguest's name on the room map (Who is where) and a
    /// ballot of someone else's on the vote page open that houseguest's profile as a page reached from
    /// the page, as the notes and the web do; its way back names the page, and Escape or the way back
    /// returns there as it was left. The player's own name is never a link.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator NotebookRowsProfiles_ANameOnTheRoomMapOpensTheProfile_AndEscapeGoesBackToTheMap()
        {
            yield return SettleCast();
            director.ShowNotebookSection(EpisodeDirector.NotebookSection.Rooms);
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            var state = director.Snapshot;
            var you = state.Find(state.playerId);

            var map = LastActive(HouseMap.RootName);
            Assert.That(map, Is.Not.Null, "The room map is drawn.");
            var links = map.GetComponentsInChildren<Button>().Where(b => b.name.StartsWith(EpisodeHud.ProfileLinkPrefix, System.StringComparison.Ordinal)).ToList();
            Assert.That(links.Select(b => b.name), Has.None.EqualTo(EpisodeHud.ProfileLinkPrefix + you.name), "Your own name is not a link.");
            Assert.That(links, Is.Not.Empty, "The houseguests' names on the map are links.");
            var link = links[0];
            var actor = state.contestants.Single(c => EpisodeHud.ProfileLinkPrefix + c.name == link.name);
            // A room card shows the first name; the card of those the house cannot place shows the whole name.
            Assert.That(link.GetComponent<TMP_Text>().text, Is.EqualTo(CharacterNames.FirstName(actor.name)).Or.EqualTo(actor.name),
                "Its caption is the name the map shows.");

            link.onClick.Invoke();
            yield return Frames(2);
            Assert.That(director.ProfileId, Is.EqualTo(actor.id), "The name opens their profile.");
            Assert.That(director.ProfileReturnsTo, Is.EqualTo(EpisodeDirector.NotebookSection.Rooms));
            var back = AssertPageWayBack(EpisodeDirector.ProfileBackCaptionFor(EpisodeDirector.NotebookSection.Rooms), "A profile opened from the room map");
            Assert.That(WayOut.CaptionOf(back), Is.EqualTo("Back to Who is where"));

            yield return PressKey(Key.Escape);
            yield return null;
            Assert.That(director.ProfileId, Is.Null, "Escape goes back one page,");
            Assert.That(director.IsPanelOpen, Is.True, "leaves the notebook open,");
            Assert.That(director.ActiveSection, Is.EqualTo(EpisodeDirector.NotebookSection.Rooms), "and on the room map it came from.");
            Assert.That(LastActive(HouseMap.RootName), Is.Not.Null);
            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator NotebookRowsProfiles_ABallotOnTheVotePageOpensTheVotersProfile_AndItsWayBackReturnsToTheBallots()
        {
            var state = PartlyKnownReveal();
            director.SuspendNpcAutonomyForDiagnostics();
            new EpisodeSaveStore(director.SavePath).Save(state);
            yield return ReloadEpisode();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return SkipReveals();
            director.ClosePanels();
            yield return null;
            var shown = director.Snapshot;
            var known = VoteRecords.Read(shown).Records
                .SelectMany(r => r.Ballots.Where(b => b.Known && !b.ByPlayer && b.VoterId != null && b.VoterId != shown.playerId).Select(b => (r.Week, b)))
                .ToList();
            Assert.That(known, Is.Not.Empty, "The vote page shows a ballot of someone else's.");
            var (week, ballot) = known[0];
            var voter = shown.Find(ballot.VoterId);

            director.ShowNotebookSection(EpisodeDirector.NotebookSection.Votes);
            yield return Frames(2);
            ButtonWithCaption("Known ballots").onClick.Invoke();
            yield return Frames(2);
            Canvas.ForceUpdateCanvases();
            var card = LastActive(EpisodeHud.BallotsPrefix + week);
            Assert.That(card, Is.Not.Null, "Week " + week + "'s known ballots are drawn.");
            var link = card.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name == EpisodeHud.ProfileLinkPrefix + voter.name);
            Assert.That(link, Is.Not.Null, voter.name + "'s ballot is a link.");
            Assert.That(link.GetComponent<TMP_Text>().text, Does.StartWith(voter.name), "Its row is their ballot, in the page's words.");
            Assert.That(card.GetComponentsInChildren<Button>().Select(b => b.name), Has.None.EqualTo(EpisodeHud.ProfileLinkPrefix + shown.Find(shown.playerId).name),
                "Your own ballot is not a link.");

            link.onClick.Invoke();
            yield return Frames(2);
            Assert.That(director.ProfileId, Is.EqualTo(voter.id), "The ballot opens the voter's profile.");
            Assert.That(director.ProfileReturnsTo, Is.EqualTo(EpisodeDirector.NotebookSection.Votes));
            var back = AssertPageWayBack(EpisodeDirector.ProfileBackCaptionFor(EpisodeDirector.NotebookSection.Votes), "A profile opened from the vote");
            Assert.That(WayOut.CaptionOf(back), Is.EqualTo("Back to the vote"));
            back.onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.ProfileId, Is.Null, "The way back leaves the profile");
            Assert.That(director.ActiveSection, Is.EqualTo(EpisodeDirector.NotebookSection.Votes), "for the vote page it came from,");
            Assert.That(LastActive(EpisodeHud.BallotsPrefix + week), Is.Not.Null, "on the Known ballots tab, as it was left.");
            director.ClosePanels();
            yield return null;
        }
    }
}
