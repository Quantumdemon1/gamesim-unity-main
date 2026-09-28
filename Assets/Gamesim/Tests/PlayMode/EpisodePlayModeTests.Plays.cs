using System;
using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Persistence;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Plays in the house (plan 30 §4): offered on the Pull, taken on in one press with the first
    /// step opening where the player stands, won on the card with a receipt that says what changed,
    /// and kept on the notebook's plays page.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// A season at the nominations with a secret alliance the player cannot see and another they
        /// know about: a legal history played with the story off, switched on there, and The Secret
        /// Alliance offered through the engine's own seam.
        /// </summary>
        private IEnumerator InstallSecretAllianceFixture()
        {
            EpisodeState fixture = null;
            for (uint seed = 1; seed <= 30 && fixture == null; seed++)
            {
                var engine = new EpisodeEngine(SeasonBuilder.Create(new SeasonBuilder.Choice(), seed));
                for (int guard = 0; guard < 400; guard++)
                {
                    var current = engine.Snapshot;
                    if (current.week >= 2 && current.phase == EpisodePhase.Nomination && current.nominees.Count == 0
                        && current.hohId != current.playerId && current.Find(current.playerId).status == ContestantStatus.Active)
                    {
                        EpisodeEngine.EnableStory(current, current.week);
                        var npcs = current.Active.Where(c => !c.isPlayer).OrderBy(c => c.id, StringComparer.Ordinal).ToList();
                        foreach (var other in current.alliances) other.active = false;
                        var secret = NpcAlliances.FormFromStory(current, new[] { npcs[0].id, npcs[1].id }.ToList());
                        Knowledge.AllianceFormed(current, secret);
                        var known = NpcAlliances.FormFromStory(current, new[] { npcs[2].id, npcs[3].id }.ToList());
                        Knowledge.AllianceFormed(current, known);
                        Knowledge.MakeKnown(current, Knowledge.Of(current, FactKinds.Alliance, known.id), FactVisibility.Public);
                        if (EpisodeEngine.StartStory(current, "the-secret-alliance", StoryAnchors.HohCrowned)) fixture = current;
                        break;
                    }
                    if (current.phase == EpisodePhase.Finished) break;
                    Assert.That(engine.Apply(NextCommand(current)).accepted, Is.True);
                }
            }
            Assert.That(fixture, Is.Not.Null, "No bounded legal fixture with The Secret Alliance on offer found.");
            director.SuspendNpcAutonomyForDiagnostics();
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            yield return ReloadEpisode();
            director.SuspendNpcAutonomyForDiagnostics();
            AssertEquivalent(fixture, director.Snapshot);
            yield return null; yield return null;
        }

        [UnityTest]
        public IEnumerator Plays_TakenOnFromThePullWonOnTheCardAndKeptOnThePlaysPage()
        {
            yield return InstallSecretAllianceFixture();
            Assert.That(director.PullOffered, Is.EqualTo(EpisodeHud.TakeItOnCaption), "The play is offered on the Pull.");
            Assert.That(ShownText(), Does.Contain("INTEL"), "The Pull says what the play pays.");

            ButtonWithCaption(EpisodeHud.TakeItOnCaption).onClick.Invoke();
            yield return null;
            Assert.That(director.IsSceneCardOpen, Is.True, "Taking it on opens its first step where the player stands.");
            var cycleId = director.Snapshot.storylines.Last(x => x.templateId == "the-secret-alliance").id;
            Assert.That(EpisodeEngine.TakenOn(director.Snapshot.storylines.Single(x => x.id == cycleId)), Is.True);

            ButtonWithCaption(EpisodeHud.EventChoiceCaption("Trade something you know")).onClick.Invoke();
            yield return null;
            Assert.That(director.Snapshot.storylines.Single(x => x.id == cycleId).endingId, Is.EqualTo(PlayEndings.Won),
                "Knowing one alliance bought the other: won on the spot.");
            string card = ShownText();
            Assert.That(card, Does.Contain("PLAY WON"), "The card says it was won,");
            Assert.That(card, Does.Contain("You learned:"), "and the receipt says what changed.");

            ButtonWithCaption(EpisodeDirector.SceneCardDoneCaption).onClick.Invoke();
            yield return null;
            director.ShowNotebookSection(EpisodeDirector.NotebookSection.Story);
            yield return null;
            string page = ShownText();
            Assert.That(page, Does.Contain(EpisodeDirector.PlaysHeading), "The notebook's Story section opens with the plays,");
            Assert.That(page, Does.Contain("The Secret Alliance"), "and keeps the one just won,");
            Assert.That(page, Does.Contain("Intel · won"), "with what it paid and how it went.");
        }
    }
}
