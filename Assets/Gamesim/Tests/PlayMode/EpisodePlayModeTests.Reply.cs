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
    /// A houseguest's acknowledgement of time spent together is spoken once after the player.
    /// The ordered exchange does not repeat the old greeting or duplicate its reply in the menu.
    /// The committed relationship can stay in its band or cross it; reading the spoken reply never
    /// applies either outcome again.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private const string SpokenDialogueName = "NPC spoken dialogue";

        [UnityTest]
        public IEnumerator Conversation_ATalkThatMovesNobodySaysItsPieceOnce()
        {
            yield return OpenMayaWithHerTrustAt(5);
            string mayaId = ContentCatalog.MayaId;
            string before = HouseDialogue.Response(director.Snapshot, mayaId);
            yield return ClickBlocCommand("Spend time together");
            var after = director.Snapshot;
            Assert.That(HouseDialogue.Response(after, mayaId), Is.EqualTo(before),
                "Precondition: one talk from 5 stays in the same band, so where Maya stands is unchanged.");

            Assert.That(director.ConversationExchangeSpeakerId, Is.EqualTo(after.playerId), "The player speaks first.");
            ButtonWithCaption(EpisodeHud.ConversationContinueCaption).onClick.Invoke();
            yield return null;
            Assert.That(director.ConversationExchangeSpeakerId, Is.EqualTo(mayaId));
            var spoken = Said(EpisodeHud.ConversationExchangeLineName);
            Assert.That(spoken, Is.EqualTo(new[] { Quoted(HouseDialogue.TalkAcknowledgement(after, mayaId)) }),
                "The reply is the acknowledgement.");
            Assert.That(spoken[0], Does.Not.Contain(before),
                "It used to end on the sentence the greeting had just ended on.");
            Assert.That(Said(EpisodeHud.FollowUpDialogueName), Is.Empty,
                "Nothing moved, so there is nothing more to say.");
            ButtonWithCaption(EpisodeHud.ConversationContinueCaption).onClick.Invoke();
            yield return null;
            Assert.That(Said(SpokenDialogueName), Is.Empty, "Returning to topics does not replay the acknowledged conversation.");
        }

        [UnityTest]
        public IEnumerator Conversation_ATalkThatMovesThemKeepsTheChangedStandingAndSpeaksItsAcknowledgementOnce()
        {
            yield return OpenMayaWithHerTrustAt(22);
            string mayaId = ContentCatalog.MayaId;
            string before = HouseDialogue.Response(director.Snapshot, mayaId);
            yield return ClickBlocCommand("Spend time together");
            var after = director.Snapshot;
            Assert.That(after.Score(mayaId, after.playerId), Is.GreaterThanOrEqualTo(25d),
                "Precondition: a talk is worth at least +3.2 to her, so from 22 it crosses into the warm band.");

            Assert.That(HouseDialogue.Response(after, mayaId), Is.Not.EqualTo(before), "The changed relationship remains on the committed state.");
            Assert.That(director.ConversationExchangeSpeakerId, Is.EqualTo(after.playerId));
            ButtonWithCaption(EpisodeHud.ConversationContinueCaption).onClick.Invoke();
            yield return null;
            Assert.That(director.ConversationExchangeSpeakerId, Is.EqualTo(mayaId));
            var spoken = Said(EpisodeHud.ConversationExchangeLineName);
            Assert.That(spoken, Is.EqualTo(new[] { Quoted(HouseDialogue.TalkAcknowledgement(after, mayaId)) }));
            Assert.That(spoken[0], Does.Not.Contain(before), "The reply does not repeat the greeting from before the talk.");
            Assert.That(Said(SpokenDialogueName), Is.Empty, "The old reply surface does not duplicate the current speaker.");
            Assert.That(Said(EpisodeHud.FollowUpDialogueName), Is.Empty, "The ordered exchange displays one spoken line at a time.");
            ButtonWithCaption(EpisodeHud.ConversationContinueCaption).onClick.Invoke();
            yield return null;
            Assert.That(Said(SpokenDialogueName), Is.Empty, "Reading the reply again does not replay it in the menu.");
            Assert.That(HouseDialogue.Response(director.Snapshot, mayaId), Is.EqualTo(HouseDialogue.Response(after, mayaId)));
        }

        /// <summary>
        /// A legal save whose only change is Maya's own feeling toward the player, installed and
        /// opened through the house the way a player reaches her.
        /// </summary>
        private IEnumerator OpenMayaWithHerTrustAt(double score)
        {
            var seeded = ContentCatalog.Create(11);
            var edge = seeded.relationships.Single(item => item.fromId == ContentCatalog.MayaId && item.toId == seeded.playerId);
            edge.score = score;
            Assert.That(EpisodeValidation.TryValidate(seeded, out var reason), Is.True, reason);
            new EpisodeSaveStore(director.SavePath).Save(seeded);
            yield return ReloadEpisode();
            var maya = SceneComponents<HouseNpc>().Single(npc => npc.Id == ContentCatalog.MayaId);
            yield return OpenNearbyNpc(maya);
        }

        private static string Quoted(string line) => "\"" + line + "\"";

        private string[] Said(string name) => director.GetComponentsInChildren<TMP_Text>(true)
            .Where(label => label.gameObject.activeInHierarchy && label.name == name)
            .Select(label => label.text).ToArray();

        private TMP_Text Line(string name) => director.GetComponentsInChildren<TMP_Text>(true)
            .Single(label => label.gameObject.activeInHierarchy && label.name == name);
    }
}
