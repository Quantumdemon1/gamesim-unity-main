using System.Collections;
using System.Linq;
using System.Reflection;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The deal table, driven through the actual conversation panel.
    ///
    /// <para>The simulation half is covered by <c>NpcDealTests</c> and <c>PlayerDealTests</c>. What
    /// those cannot show is that the buttons exist, say what they do, and commit the thing they say
    /// — which is where the reference build's own deal screens most often went wrong, promising one
    /// arrangement and writing another.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator DealPanel_ProposingThroughTheRealButtonCommitsThatExactDeal()
        {
            var maya = SceneComponents<HouseNpc>().Single(npc => npc.Id == ContentCatalog.MayaId);
            yield return OpenNearbyNpc(maya);

            var before = director.Snapshot;
            Assert.That(before.dealRulesStartWeek, Is.EqualTo(1), "A fresh season deals from week one.");
            var offered = PlayerDeals.Available(before, maya.Id);
            Assert.That(offered, Is.Not.Empty, "There should be something the player can put to Maya.");

            string type = offered.First(kind => kind != DealKind.TargetAgreement);
            var button = ButtonWithCaption(EpisodeHud.DealProposeCaption(DealKind.Title(type).ToLowerInvariant()));
            Assert.That(button.IsInteractable(), Is.True);
            button.onClick.Invoke();
            yield return null;

            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(before.revision + 1), "A proposal is a committed command.");
            Assert.That(after.events.Skip(before.events.Count).Any(e => e.kind == "deal"), Is.True,
                "The house should have said something either way.");

            var struck = after.deals.Where(d => d.proposerId == after.playerId).ToList();
            if (struck.Count == 0)
            {
                Assert.That(after.Score(maya.Id, after.playerId),
                    Is.LessThan(before.Score(maya.Id, after.playerId)), "A refusal costs a little warmth.");
                yield break;
            }
            Assert.That(struck, Has.Count.EqualTo(1));
            Assert.That(struck[0].type, Is.EqualTo(type), "The button must commit the deal it names.");
            Assert.That(struck[0].recipientId, Is.EqualTo(maya.Id));
            Assert.That(struck[0].status, Is.EqualTo(DealStatus.Active));
        }

        [UnityTest]
        public IEnumerator DealPanel_AnOfferOnTheTableCanBeAcceptedOrDeclinedFromTheConversation()
        {
            var maya = SceneComponents<HouseNpc>().Single(npc => npc.Id == ContentCatalog.MayaId);
            yield return OpenNearbyNpc(maya);

            // Put an offer on the table the way the house would, then reopen so the panel draws it.
            var offer = Seed(maya.Id, DealKind.SafetyAgreement);
            director.ClosePanels();
            yield return OpenNearbyNpc(maya);

            var before = director.Snapshot;
            Assert.That(NpcDeals.Pending(before).Any(d => d.id == offer.id), Is.True);
            var decline = ButtonWithCaption(EpisodeHud.DealDeclineCaption);
            Assert.That(decline.IsInteractable(), Is.True);
            Assert.That(ButtonWithCaption(EpisodeHud.DealAcceptCaption).IsInteractable(), Is.True);

            int spent = EpisodeEngine.SocialActionsSpent(before);
            decline.onClick.Invoke();
            yield return null;

            var after = director.Snapshot;
            Assert.That(after.deals.Single(d => d.id == offer.id).status, Is.EqualTo(DealStatus.Declined));
            Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(spent),
                "Answering somebody else's offer is not one of the player's own actions.");
            Assert.That(after.Score(maya.Id, after.playerId),
                Is.LessThan(before.Score(maya.Id, before.playerId)));
            Assert.That(NpcDeals.Pending(after), Is.Empty);
        }

        /// <summary>
        /// Puts a proposal into the live season without going through a phase transition.
        ///
        /// <para>The engine files offers at the campaign and social boundaries, and driving a whole
        /// week to reach one would make this test about the week rather than about the panel. There
        /// is no command for "a houseguest offers you something" — that is the house's move, not the
        /// player's — so the fixture writes it in the same shape <see cref="NpcDeals.Propose"/> does
        /// and the panel is left to find it.</para>
        /// </summary>
        private DealState Seed(string fromId, string type)
        {
            var engine = (EpisodeEngine)typeof(EpisodeDirector)
                .GetField("engine", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(director);
            var live = (EpisodeState)typeof(EpisodeEngine)
                .GetField("current", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(engine);
            var offer = new DealState
            {
                id = "deal-ask-playmode", type = type, proposerId = fromId, recipientId = live.playerId,
                status = DealStatus.Proposed, week = live.week, trustImpact = DealKind.DefaultTrust(type),
            };
            live.deals.Add(offer);
            // The panel draws from the projection, not from the engine, and a projection is only
            // rebuilt when a command commits. Nothing committed here — the house made this move —
            // so the projection is refreshed by hand.
            typeof(EpisodeDirector).GetMethod("Project", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(director, null);
            return offer;
        }
    }
}
