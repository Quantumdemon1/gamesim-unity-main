using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Persistence;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The strategy windows in the house: the station points the player at whoever is deciding, that
    /// houseguest hears one plea in two steps, everybody else is still busy, a nominee's question
    /// about the veto is answered on the veto decision, and a houseguest who came to the player waits
    /// on the free-time panel for an answer.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>A default-cast season that plays the strategy windows, saved and loaded, shaped by <paramref name="shape"/>.</summary>
        private IEnumerator InstallStrategySeason(uint seed, System.Action<EpisodeState> shape)
        {
            var state = StrategySeason(seed, shape);
            Assert.That(EpisodeValidation.TryValidate(state, out var reason), Is.True, reason);
            new EpisodeSaveStore(director.SavePath).Save(state);
            yield return ReloadEpisode();
        }

        /// <summary>The season <see cref="InstallStrategySeason"/> installs, built without installing it, so a search can play it on the engine alone first.</summary>
        private static EpisodeState StrategySeason(uint seed, System.Action<EpisodeState> shape)
        {
            var state = ContentCatalog.Create(seed);
            state.competitionRulesVersion = CompetitionRules.Current;
            state.haveNotRulesStartWeek = 1;
            state.strategyRulesStartWeek = 1;
            shape?.Invoke(state);
            return state;
        }

        /// <summary>The veto meeting: the first houseguest is Head of Household, the next two nominated, the fourth - or the player - holding the veto.</summary>
        private static void AtVetoMeeting(EpisodeState state, bool playerHolds)
        {
            var npcs = state.Active.Where(actor => !actor.isPlayer).Select(actor => actor.id).ToList();
            state.phase = EpisodePhase.VetoMeeting;
            state.hohId = npcs[0];
            state.nominees = new System.Collections.Generic.List<string> { npcs[1], npcs[2] };
            state.vetoHolderId = playerHolds ? state.playerId : npcs[3];
            state.vetoPlayers = state.Active.Select(actor => actor.id).Take(EpisodeEngine.VetoPlayerCount(state.Active.Count())).ToList();
            if (!state.vetoPlayers.Contains(state.vetoHolderId)) state.vetoPlayers[state.vetoPlayers.Count - 1] = state.vetoHolderId;
        }

        private IEnumerator TalkTo(string id)
        {
            director.ClosePanels();
            yield return null;
            var npc = SceneComponents<HouseNpc>().First(actor => actor.Id == id && actor.gameObject.activeInHierarchy);
            yield return OpenNearbyNpc(npc);
            yield return null;
        }

        private string ShownText() => string.Join(" ", director.GetComponentsInChildren<TMP_Text>(true)
            .Where(label => label.gameObject.activeInHierarchy).Select(label => label.text));

        [UnityTest]
        public IEnumerator Lobbying_TheHeadOfHouseholdHearsOnePleaBeforeNominations()
        {
            string hoh = null, other = null;
            yield return InstallStrategySeason(41, state =>
            {
                state.phase = EpisodePhase.Nomination;
                hoh = state.hohId = state.Active.First(actor => !actor.isPlayer).id;
                other = state.Active.First(actor => !actor.isPlayer && actor.id != hoh).id;
            });
            yield return OpenStation();
            Assert.That(ShownText(), Does.Contain(EpisodeDirector.WindowLine(director.Snapshot)), "The station points the player at them.");

            yield return TalkTo(other);
            Assert.That(ShownText(), Does.Contain("Before nominations only the Head of Household has time for a word."));
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.SmallTalkCaption), Is.Null, "Everybody else is still busy.");

            yield return TalkTo(hoh);
            var state = director.Snapshot;
            string name = state.Find(hoh).name;
            Assert.That(ShownText(), Does.Contain(EpisodeDirector.LobbyHeading(state)));
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.SmallTalkCaption), Is.Not.Null, "A word before the ceremony can be any conversation.");
            Assert.That(ButtonWithCaptionOrNull("Work against them quietly"), Is.Null, "Scheming waits for free time.");
            ButtonWithCaption(EpisodeHud.LobbyAskCaption(state, name, LobbyAsk.Spare, state.playerId)).onClick.Invoke();
            yield return null;
            foreach (var approach in LobbyApproach.All)
                Assert.That(ButtonWithCaptionOrNull(EpisodeHud.LobbyApproachCaption(approach, true)), Is.Not.Null, approach);
            Assert.That(director.Snapshot.lobbies, Is.Empty, "Choosing what to ask commits nothing.");
            ButtonWithCaption(EpisodeHud.LobbyApproachCaption(LobbyApproach.Emotional, true)).onClick.Invoke();
            yield return null; yield return null;

            var plea = director.Snapshot.lobbies.Single();
            Assert.That((plea.deciderId, plea.ask, plea.subjectId, plea.approach),
                Is.EqualTo((hoh, LobbyAsk.Spare, state.playerId, LobbyApproach.Emotional)));
            Assert.That(ShownText(), Does.Contain("You have had your plea with " + name + ". They were " + plea.response + "."));
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.LobbyAskCaption(state, name, LobbyAsk.Spare, state.playerId)), Is.Null,
                "One plea each.");
        }

        [UnityTest]
        public IEnumerator Lobbying_TheVetoHolderIsAskedAboutTheNominationsAndTheHeadOfHouseholdAboutAReplacement()
        {
            yield return InstallStrategySeason(42, state => AtVetoMeeting(state, false));
            var state = director.Snapshot;
            string holder = state.vetoHolderId, name = state.Find(holder).name, nominee = state.nominees[0];
            yield return TalkTo(holder);
            ButtonWithCaption(EpisodeHud.LobbyAskCaption(state, name, LobbyAsk.Save, nominee)).onClick.Invoke();
            yield return null;
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.LobbyApproachCaption(LobbyApproach.Emotional, false)), Is.Not.Null,
                "A plea for somebody else is put in the reference's other words.");
            ButtonWithCaption(EpisodeHud.LobbyBackCaption).onClick.Invoke();
            yield return null;
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.LobbyAskCaption(state, name, LobbyAsk.Keep, null)), Is.Not.Null,
                "Back returns to what to ask.");
            ButtonWithCaption(EpisodeHud.LobbyAskCaption(state, name, LobbyAsk.Keep, null)).onClick.Invoke();
            yield return null;
            ButtonWithCaption(EpisodeHud.LobbyApproachCaption(LobbyApproach.Strategic, false)).onClick.Invoke();
            yield return null; yield return null;
            var plea = director.Snapshot.lobbies.Single();
            Assert.That((plea.deciderId, plea.ask, plea.subjectId, plea.approach), Is.EqualTo((holder, LobbyAsk.Keep, (string)null, LobbyApproach.Strategic)));

            yield return TalkTo(state.hohId);
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.LobbyAskCaption(state, state.Find(state.hohId).name, LobbyAsk.Spare, state.playerId)),
                Is.Not.Null, "The Head of Household still has a replacement to name, and the player could be it.");
        }

        [UnityTest]
        public IEnumerator VetoOffers_ANomineesQuestionIsAnsweredOnTheVetoDecision()
        {
            yield return InstallStrategySeason(43, state =>
            {
                AtVetoMeeting(state, true);
                state.deals.Add(new DealState
                {
                    id = "deal-veto-9", type = DealKind.VetoUse, proposerId = state.nominees[0], recipientId = state.playerId,
                    status = DealStatus.Proposed, week = state.week, expiresWeek = state.week, trustImpact = DealKind.DefaultTrust(DealKind.VetoUse),
                });
            });
            yield return OpenStation();
            var state = director.Snapshot;
            string asking = state.Find(state.nominees[0]).name;
            Assert.That(ShownText(), Does.Contain("AN OFFER FROM " + asking.ToUpperInvariant()));
            Assert.That(ShownText(), Does.Contain(asking + " is on the block and wants your word that you will use the veto on them."));
            ButtonWithCaption(EpisodeHud.DealAcceptCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.Snapshot.deals.Single(deal => deal.id == "deal-veto-9").status, Is.EqualTo(DealStatus.Active));
            Assert.That(ButtonWithCaptionOrNull("Do not use the veto"), Is.Not.Null, "The decision itself is still to make.");
        }

        [UnityTest]
        public IEnumerator ReplyCards_AConfrontationWaitsOnTheFreeTimePanelAndIsAnswered()
        {
            string from = null;
            yield return InstallStrategySeason(44, state =>
            {
                state.phase = EpisodePhase.Social;
                from = state.Active.First(actor => !actor.isPlayer).id;
                state.replyCards.Add(new ReplyCardState { id = "reply-7", week = state.week, kind = ReplyCards.Confrontation, fromId = from });
            });
            yield return OpenStation();
            var state = director.Snapshot;
            Assert.That(ShownText(), Does.Contain(EpisodeHud.ReplyCardEyebrow));
            Assert.That(ShownText(), Does.Contain(ReplyCards.Title(state, state.replyCards[0])));
            Assert.That(ShownText(), Does.Contain(ReplyCards.Message(state, state.replyCards[0])));
            foreach (var reply in ReplyCards.Replies(ReplyCards.Confrontation))
                Assert.That(ButtonWithCaptionOrNull(EpisodeHud.ReplyCaption(reply.Label)), Is.Not.Null, reply.Label);
            ButtonWithCaption(EpisodeHud.ReplyCaption("Apologize")).onClick.Invoke();
            yield return null; yield return null;
            var after = director.Snapshot;
            Assert.That(after.replyCards, Is.Empty);
            Assert.That(after.events.Last(e => e.kind == "reply").text, Is.EqualTo("You apologized to " + after.Find(from).name + "."));
            Assert.That(ShownText(), Does.Not.Contain(EpisodeHud.ReplyCardEyebrow), "Answered, the card is gone.");
        }
    }
}
