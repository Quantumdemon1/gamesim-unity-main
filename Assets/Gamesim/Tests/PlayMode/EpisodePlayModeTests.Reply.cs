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
    /// A houseguest's reply to time spent together says each thing once.
    ///
    /// <para>The reply to "Spend time together" was the shipped acknowledgement with the
    /// houseguest's standing sentence glued to the end - and that sentence was the one the greeting
    /// had just ended on, nearly every time, because a single talk rarely moves anyone across a
    /// band. So the reply read as the greeting said twice, and the one time the sentence WAS news
    /// it was buried where the player had learned to skim. Now the acknowledgement is the reply,
    /// and the standing is a line of its own only when the talk moved it. Nothing is hidden: when
    /// nothing moved, it is the sentence the player just read.</para>
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

            var spoken = Said(SpokenDialogueName);
            Assert.That(spoken, Is.EqualTo(new[] { Quoted(HouseDialogue.TalkAcknowledgement(after, mayaId)) }),
                "The reply is the acknowledgement.");
            Assert.That(spoken[0], Does.Not.Contain(before),
                "It used to end on the sentence the greeting had just ended on.");
            Assert.That(Said(EpisodeHud.FollowUpDialogueName), Is.Empty,
                "Nothing moved, so there is nothing more to say.");
        }

        [UnityTest]
        public IEnumerator Conversation_ATalkThatMovesThemSaysWhereTheyStandNow()
        {
            yield return OpenMayaWithHerTrustAt(22);
            string mayaId = ContentCatalog.MayaId;
            string before = HouseDialogue.Response(director.Snapshot, mayaId);
            yield return ClickBlocCommand("Spend time together");
            var after = director.Snapshot;
            Assert.That(after.Score(mayaId, after.playerId), Is.GreaterThanOrEqualTo(25d),
                "Precondition: a talk is worth at least +3.2 to her, so from 22 it crosses into the warm band.");

            Assert.That(Said(SpokenDialogueName), Is.EqualTo(new[] { Quoted(HouseDialogue.TalkAcknowledgement(after, mayaId)) }));
            var follow = Said(EpisodeHud.FollowUpDialogueName);
            Assert.That(follow, Is.EqualTo(new[] { Quoted(HouseDialogue.Response(after, mayaId)) }),
                "The talk moved her, and she says where she stands now - as a line of its own.");
            Assert.That(follow[0], Is.Not.EqualTo(Quoted(before)), "and it is not what she said before the talk.");

            Canvas.ForceUpdateCanvases();
            var reply = Line(SpokenDialogueName);
            var shift = Line(EpisodeHud.FollowUpDialogueName);
            Assert.That(ScreenRect(reply.rectTransform).yMin, Is.GreaterThanOrEqualTo(ScreenRect(shift.rectTransform).yMax - 0.5f),
                "The reply comes first and what changed comes under it, without the two running into each other.");
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
