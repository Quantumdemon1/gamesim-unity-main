using System.Collections;
using System.IO;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator ConversationResponse_AChosenNominationApproachPlaysItsActualPublishedAnswerOnce()
        {
            yield return InstallTalkingHouse(8, false, state =>
            {
                state.phase = EpisodePhase.Nomination;
                state.hohId = state.Active.First(person => !person.isPlayer).id;
            });
            var before = director.Snapshot;
            string listener = before.hohId;
            yield return TalkTo(listener);
            yield return EnterConversationCategory(EpisodeDirector.GameCategoryCaption);
            string askCaption = EpisodeHud.LobbyAskCaption(before, before.Find(listener).name, LobbyAsk.Spare, before.playerId);
            ButtonWithCaption(askCaption).onClick.Invoke();
            yield return null;
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.StrategicDiscussionCaption), Is.Null,
                "Choosing how to make a plea replaces the unrelated topic choices.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision));
            ButtonWithCaption(EpisodeHud.LobbyApproachCaption(LobbyApproach.Emotional, true)).onClick.Invoke();
            yield return null;
            var accepted = director.Snapshot;
            var record = accepted.lobbies.Single();
            byte[] saved = File.ReadAllBytes(director.SavePath);
            AssertExchangeFlowStep(before.playerId, StrategyRules.Pitch(before, record.ask, record.subjectId, record.approach));
            ButtonWithCaption(EpisodeHud.ConversationContinueCaption).onClick.Invoke();
            yield return null;
            AssertExchangeFlowStep(listener, StrategyRules.Answer(before, record.ask, record.subjectId, record.approach, record.response));
            if (Application.isBatchMode) yield return CaptureFraming("dialogue-nomination-answer");
            ButtonWithCaption(EpisodeHud.ConversationContinueCaption).onClick.Invoke();
            yield return null;
            Assert.That(director.IsConversationExchangePlaying, Is.False);
            Assert.That(JsonUtility.ToJson(director.Snapshot), Is.EqualTo(JsonUtility.ToJson(accepted)));
            CollectionAssert.AreEqual(saved, File.ReadAllBytes(director.SavePath));
            Assert.That(EpisodeEngine.SocialActionsSpent(accepted), Is.EqualTo(EpisodeEngine.SocialActionsSpent(before) + 1));
        }

        [UnityTest]
        public IEnumerator ConversationResponse_TheVoteQuestionSpeaksTheClaimItPublishedWithoutAnotherAction()
        {
            yield return InstallTalkingHouse(8, true, state => WarmToPlayer(state, 70));
            var before = director.Snapshot;
            string voter = EpisodeEngine.Voters(before).First(person => !person.isPlayer).id;
            yield return TalkTo(voter);
            yield return EnterConversationCategory(EpisodeDirector.LearnCategoryCaption);
            ButtonWithCaption(EpisodeHud.AskVoteCaption).onClick.Invoke();
            yield return null;
            var accepted = director.Snapshot;
            byte[] saved = File.ReadAllBytes(director.SavePath);
            var events = accepted.events.Where(item => item.sequence >= before.nextSequence && item.kind == "vote-read").ToList();
            Assert.That(events, Has.Count.EqualTo(1));
            AssertExchangeFlowStep(before.playerId, "Where's your head at on this week's vote?");
            ButtonWithCaption(EpisodeHud.ConversationContinueCaption).onClick.Invoke();
            yield return null;
            string named = before.nominees.Select(before.Find).Where(person => events[0].text.EndsWith("\"" + person.name + ".\""))
                .Select(person => person.id == before.playerId ? "you" : person.name).SingleOrDefault();
            AssertExchangeFlowStep(voter, named == null ? "Ask me after." : "I'm leaning toward evicting " + named + ".");
            Assert.That(EpisodeEngine.SocialActionsSpent(accepted), Is.EqualTo(EpisodeEngine.SocialActionsSpent(before)),
                "The existing limited vote question remains free.");
            ButtonWithCaption(EpisodeHud.ConversationSkipCaption).onClick.Invoke();
            yield return null;
            Assert.That(JsonUtility.ToJson(director.Snapshot), Is.EqualTo(JsonUtility.ToJson(accepted)));
            CollectionAssert.AreEqual(saved, File.ReadAllBytes(director.SavePath));
        }
    }
}
