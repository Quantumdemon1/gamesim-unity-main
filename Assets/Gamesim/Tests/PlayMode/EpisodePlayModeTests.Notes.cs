using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The notebook's own page (playtest, 2026-09-28): "Notebook [J]" used to open the relationships,
    /// which the rail already had a row for. It opens on your notes now: a card a houseguest with
    /// what your character has on them, filters by kind, and the relationships still their own row.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator Notes_TheNotebookOpensOnYourNotesNotTheRelationships()
        {
            yield return SettleCast();
            ButtonWithCaption("Notebook [J]").onClick.Invoke();
            yield return null; yield return null;
            var state = director.Snapshot;
            Assert.That(director.ActiveSection, Is.EqualTo(EpisodeDirector.NotebookSection.Notes), "The notebook's own page.");
            Assert.That(NotebookText(), Does.Contain("Your notes"), "and it names itself.");
            foreach (var actor in state.contestants.Where(c => !c.isPlayer))
                Assert.That(ActiveRect(EpisodeHud.NotesCardPrefix + actor.name), Is.Not.Null, actor.name + " has a card.");
            Assert.That(ActiveRect(EpisodeDirector.NotebookSection.Network), Is.Null, "The relationship web is not this page.");
            if (UnityEngine.Application.isBatchMode) yield return CaptureFraming("notebook-notes");

            // A kind's filter lists only those with something of that kind: a fresh season has none.
            ButtonWithCaption("Their vote").onClick.Invoke();
            yield return null; yield return null;
            Assert.That(ActiveRect("No notes"), Is.Not.Null, "Nothing of that kind yet, said as such.");
            ButtonWithCaption("Everyone").onClick.Invoke();
            yield return null; yield return null;
            Assert.That(ActiveRect(EpisodeHud.NotesCardPrefix + state.contestants.First(c => !c.isPlayer).name), Is.Not.Null);

            // The rail's row is still the relationships.
            director.ShowNotebookSection(EpisodeDirector.NotebookSection.Network);
            yield return null; yield return null;
            Assert.That(director.ActiveSection, Is.EqualTo(EpisodeDirector.NotebookSection.Network));
            director.ClosePanels();
            yield return null;
        }

        /// <summary>Every word on screen, the notebook's included.</summary>
        private string NotebookText() => string.Join("\n", director.GetComponentsInChildren<TMPro.TMP_Text>()
            .Where(text => text.gameObject.activeInHierarchy).Select(text => text.text));
    }
}
