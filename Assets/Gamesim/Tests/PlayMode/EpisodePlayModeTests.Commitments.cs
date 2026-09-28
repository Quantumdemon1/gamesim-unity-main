using System.Collections;
using System.Linq;
using System.Reflection;
using Gamesim.Episode;
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
    /// What the player has committed to, and what their last action bought, are on the screen.
    ///
    /// <para>Two gaps of the same kind: the simulation knew and the player was not told. A deal
    /// struck with a houseguest appeared only on the panel where it was made - the relationship
    /// column's "Between you" block listed alliances and promises and never deals - so it was
    /// forgotten until breaking it cost up to forty-five points. And an action whose trust change
    /// rounded to zero drew no outcome at all, so one of six actions for the week vanished without
    /// a word.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator Commitments_BetweenYouListsAnActiveDealWithItsStakes()
        {
            var engine = (EpisodeEngine)typeof(EpisodeDirector)
                .GetField("engine", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(director);
            var live = (EpisodeState)typeof(EpisodeEngine)
                .GetField("current", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(engine);
            var maya = live.Find(ContentCatalog.MayaId);
            Assert.That(maya, Is.Not.Null, "The fixture should hold Maya.");

            // An agreed deal, in the shape PlayerDeals.Draft writes one, between the player and Maya.
            live.deals.Add(new DealState
            {
                id = "deal-commitments-playmode", type = DealKind.SafetyAgreement,
                proposerId = live.playerId, recipientId = maya.id,
                status = DealStatus.Active, week = live.week,
                trustImpact = DealKind.DefaultTrust(DealKind.SafetyAgreement),
            });
            // The column draws from the projection, which only rebuilds when a command commits.
            typeof(EpisodeDirector).GetMethod("Project", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(director, null);

            RelationshipWeb.ClearSelection();
            // The notebook opens on your notes; the web is the rail's Relationships page.
            director.ShowNotebookSection(EpisodeDirector.NotebookSection.Network);
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();

            var section = Section();
            var graph = Under(section, RelationshipWeb.GraphName);
            var node = graph.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == RelationshipWeb.NodeName(maya.name));
            node.onClick.Invoke();
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();

            var column = Under(Section(), RelationshipWeb.ColumnName);
            string copy = Copy(column);
            Assert.That(copy, Does.Contain(RelationshipWeb.BetweenHeading));
            Assert.That(copy, Does.Contain(DealKind.Title(DealKind.SafetyAgreement)),
                "An agreed deal with Maya must appear where the player reads what is between them. "
                + "This block listed alliances and promises and never deals, so a commitment was "
                + "forgotten until breaking it cost up to forty-five points.");
            Assert.That(copy, Does.Contain("high stakes"),
                "and it carries what breaking it would cost, in the words the propose button used.");

            director.ClosePanels();
            yield return null;
            live.deals.RemoveAll(deal => deal.id == "deal-commitments-playmode");
            typeof(EpisodeDirector).GetMethod("Project", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(director, null);
        }

        [UnityTest]
        public IEnumerator Commitments_AnActionThatMovesNothingStillSaysSo()
        {
            director.ClosePanels();
            yield return null;
            var npc = SceneComponents<Gamesim.House.HouseNpc>().First(actor => actor.gameObject.activeInHierarchy);
            WarpPlayer(npc.transform.position
                + (player.transform.position - npc.transform.position).normalized * 1.4f);
            yield return null;
            Assert.That(director.TryOpenNpc(npc.Id), Is.True);
            yield return null;

            var hud = director.GetComponent<EpisodeHud>();
            Assert.That(hud, Is.Not.Null);
            hud.OutcomeChips(0.2d);
            Canvas.ForceUpdateCanvases();

            var said = director.GetComponentsInChildren<TMP_Text>(true)
                .Where(label => label.gameObject.activeInHierarchy)
                .Select(label => label.text)
                .ToArray();
            Assert.That(said, Has.Some.EqualTo("No change in trust"),
                "An action whose trust change rounds to zero drew nothing at all, so one of six "
                + "actions for the week vanished without a word. No change is information; silence "
                + "is not.");

            director.ClosePanels();
            yield return null;
        }
    }
}
