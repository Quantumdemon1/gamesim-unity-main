using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// The vote page before anyone has been evicted: each tab says why it is empty, in the
        /// words true of that state, and the page keeps its mark for the rail in both. "No eviction
        /// results yet" is said only when the season has had no eviction - the page used to say
        /// "Nobody has voted yet" over a season that had evicted someone.
        /// </summary>
        [UnityTest]
        public IEnumerator NotebookVotes_AnEmptyPageSaysWhyInBothTabs()
        {
            yield return SettleCast();
            var book = VoteRecords.Read(director.Snapshot);
            Assume.That(book.NoEvictionYet && !book.VoteInProgress, "The fixture season opens before any eviction.");
            int revision = director.Snapshot.revision;

            director.ShowNotebookSection(EpisodeDirector.NotebookSection.Votes);
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(ActiveRect(EpisodeHud.NotebookHeaderName).GetComponentsInChildren<TMP_Text>().Select(t => t.text),
                Does.Contain("The vote"));
            Assert.That(ActiveRect(EpisodeDirector.NotebookSection.Votes), Is.Not.Null, "The rail's mark is on the page.");
            string Words(string name) => string.Join("\n", ActiveRect(name).GetComponentsInChildren<TMP_Text>().Select(t => t.text));
            Assert.That(Words("No vote records"), Does.Contain("No eviction results yet."));
            Assert.That(ActiveRect(EpisodeHud.VotesPrivacyName), Is.Null, "The results tab makes no claim about ballots.");
            if (Application.isBatchMode) yield return CaptureFraming("notebook-votes");

            ButtonWithCaption("Known ballots").onClick.Invoke();
            yield return null;
            Assert.That(ActiveRect(EpisodeDirector.NotebookSection.Votes), Is.Not.Null, "and on the other tab.");
            Assert.That(ActiveRect("No vote records"), Is.Null);
            Assert.That(Words("No known ballots"), Does.Contain("No ballots are known yet."));
            Assert.That(Words(EpisodeHud.VotesPrivacyName), Does.Contain("remain private"));
            foreach (var label in ActiveRect(EpisodeHud.NotebookHeaderName).parent.GetComponentsInChildren<TMP_Text>())
            {
                label.ForceMeshUpdate();
                Assert.That(label.isTextOverflowing, Is.False, "'" + label.text + "' fits its box.");
            }
            Assert.That(ButtonWithCaption("House activities"), Is.Not.Null);
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "Reading the record commits nothing.");
            director.ClosePanels();
            yield return null;
        }
    }
}
