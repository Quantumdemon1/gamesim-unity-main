using System.Collections;
using System.IO;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        private IEnumerator InstallReplyPayoff(string kind, bool fresh = true, int size = 8)
        {
            yield return InstallTalkingHouse(size, kind == ReplyCards.Plea, s =>
            {
                EpisodeEngine.EnableWeek(s); EpisodeEngine.EnableLevers(s); EpisodeEngine.EnableCommitments(s);
                s.economyRulesVersion = fresh ? 1 : 0; s.replyCards.Clear();
                var npcs = s.Active.Where(c => !c.isPlayer).ToArray();
                s.replyCards.Add(new ReplyCardState { id = "reply-payoff-runtime", week = s.week, kind = kind,
                    fromId = kind == ReplyCards.Plea ? s.nominees[0] : npcs[0].id,
                    aboutId = kind == ReplyCards.Plea ? s.nominees[1] : kind == ReplyCards.Gossip ? npcs[1].id : null });
            });
            yield return OpenStation(); yield return null;
        }

        private string PayoffCaption(EpisodeState s, string key) => EpisodeHud.ReplyCaption(ReplyCards.Find(s.replyCards[0].kind, key).Label);
        private void AssertReplyCommit(EpisodeState before, string key)
        {
            AssertIntentCommand(before, EpisodeCommandKind.ReplyToHouseguest, before.replyCards[0].id, key);
            Assert.That(director.Snapshot.socialActions, Is.EqualTo(before.socialActions));
            Assert.That(director.Snapshot.outOfPhaseSocialActions, Is.EqualTo(before.outOfPhaseSocialActions));
        }

        [UnityTest]
        public IEnumerator ReplyPayoffs_BoardDeflectionEarnsItsAssessmentOnceAndDurably()
        {
            yield return InstallReplyPayoff(ReplyCards.Confrontation);
            var before = director.Snapshot; var click = ButtonWithCaption(PayoffCaption(before, "deflect")).onClick;
            click.Invoke(); var after = director.Snapshot;
            Assert.That(after.ledger.standings.Count, Is.EqualTo(before.ledger.standings.Count + 1));
            AssertReplyCommit(before, "deflect");
            click.Invoke(); AssertIntentDurable(after);
            director.LoadNow(); HoldTheHouseForTheFixture(); AssertIntentDurable(after);
        }

        [UnityTest]
        public IEnumerator ReplyPayoffs_GossipConfrontationLearnsOnlyTheNamedListenersAssessment()
        {
            yield return InstallReplyPayoff(ReplyCards.Gossip);
            var before = director.Snapshot; var card = before.replyCards[0];
            ButtonWithCaption(PayoffCaption(before, "confront")).onClick.Invoke();
            var row = director.Snapshot.ledger.standings.Last();
            Assert.That((row.fromId, row.toId, row.source), Is.EqualTo((card.fromId, card.aboutId, ClaimSource.Told)));
            AssertReplyCommit(before, "confront");
        }

        [UnityTest]
        public IEnumerator ReplyPayoffs_CampaignAnswersHaveDifferentConsequencesAndTheSameDurableAuthority()
        {
            foreach (string key in new[] { "promise", "noncommittal", "refuse" })
            {
                yield return InstallReplyPayoff(ReplyCards.Plea);
                var before = director.Snapshot;
                ButtonWithCaption(PayoffCaption(before, key)).onClick.Invoke();
                var after = director.Snapshot;
                Assert.That(after.ledger.standings.Count - before.ledger.standings.Count, Is.EqualTo(key == "refuse" ? 1 : 0));
                Assert.That(after.promises.Count - before.promises.Count, Is.EqualTo(key == "promise" ? 1 : 0));
                AssertReplyCommit(before, key);
            }
        }

        [UnityTest]
        public IEnumerator ReplyPayoffs_EscalationUsesTheSameNegotiatedOutcomeAsEngineReplayWithoutSpendingASeat()
        {
            yield return InstallReplyPayoff(ReplyCards.Confrontation);
            var before = director.Snapshot;
            ButtonWithCaption(PayoffCaption(before, "escalate")).onClick.Invoke();
            AssertReplyCommit(before, "escalate");
            Assert.That(director.Snapshot.events.Any(e => e.kind == "deal"), Is.True, "The proposal receives an answer, not an automatic agreement.");
        }

        [UnityTest]
        public IEnumerator ReplyPayoffs_ClosingAndReopeningAtTheSameRevisionRejectsOldAnswers()
        {
            yield return InstallReplyPayoff(ReplyCards.Confrontation);
            var before = director.Snapshot; var old = ButtonWithCaption(PayoffCaption(before, "escalate")).onClick;
            director.ClosePanels(); old.Invoke(); AssertIntentDurable(before);
            yield return OpenStation(); old.Invoke(); AssertIntentDurable(before);
            ButtonWithCaption(PayoffCaption(before, "deflect")).onClick.Invoke(); AssertReplyCommit(before, "deflect");
        }

        [UnityTest]
        public IEnumerator ReplyPayoffs_ReloadingTheSameCardRejectsCallbacksFromThePreviousWorld()
        {
            yield return InstallReplyPayoff(ReplyCards.Gossip);
            var before = director.Snapshot; var old = ButtonWithCaption(PayoffCaption(before, "confront")).onClick;
            director.LoadNow(); HoldTheHouseForTheFixture(); yield return OpenStation();
            old.Invoke(); AssertIntentDurable(before);
            ButtonWithCaption(PayoffCaption(before, "confront")).onClick.Invoke(); AssertReplyCommit(before, "confront");
        }

        [UnityTest]
        public IEnumerator ReplyPayoffs_FailedWritePreservesTheCardAndRequiresANewDeliberateAnswer()
        {
            yield return InstallReplyPayoff(ReplyCards.Confrontation);
            var before = director.Snapshot; byte[] bytes = File.ReadAllBytes(director.SavePath);
            var old = ButtonWithCaption(PayoffCaption(before, "escalate")).onClick;
            FailUiWriteBeforeReplacement(old.Invoke);
            AssertIntentDurable(before); Assert.That(File.ReadAllBytes(director.SavePath), Is.EqualTo(bytes));
            old.Invoke(); AssertIntentDurable(before);
            ButtonWithCaption(PayoffCaption(before, "escalate")).onClick.Invoke(); AssertReplyCommit(before, "escalate");
        }

        [UnityTest]
        public IEnumerator ReplyPayoffs_VisibleTradeoffsFitAndAreKeyboardReachableAtBothTextSizes()
        {
            foreach (string kind in ReplyCards.LegacyKinds)
            {
                yield return InstallReplyPayoff(kind, size: 16);
                foreach (bool large in new[] { false, true })
                {
                    yield return ApplyTextSize(large); yield return OpenStation();
                    Canvas.ForceUpdateCanvases(); yield return null;
                    var before = director.Snapshot; var card = before.replyCards[0];
                    foreach (var reply in ReplyCards.Replies(kind))
                    {
                        var button = ButtonWithCaption(EpisodeHud.ReplyCaption(reply.Label));
                        string text = ReplyCardPayoffs.Description(before, card, reply);
                        var label = button.GetComponentsInChildren<TMP_Text>().Single(t => t.text == text);
                        label.ForceMeshUpdate();
                        Assert.That(label.isTextOverflowing, Is.False, kind + "/" + reply.Key + " at " + large);
                        AssertInside(ScreenRect((RectTransform)button.transform), label.rectTransform, "Reply consequence copy");
                    }
                    yield return AssertKeyboardRing("Reply payoffs " + kind + " " + large, ModalRoot);
                    AssertIntentDurable(before);
                }
            }
        }

        [UnityTest]
        public IEnumerator ReplyPayoffs_LegacyCardsKeepTheirWordsAndNeverCreateTheNewTruce()
        {
            yield return InstallReplyPayoff(ReplyCards.Confrontation, fresh: false);
            var before = director.Snapshot;
            foreach (var reply in ReplyCards.Replies(ReplyCards.Confrontation))
                Assert.That(ButtonWithCaption(EpisodeHud.ReplyCaption(reply.Label)).GetComponentsInChildren<TMP_Text>().Any(t => t.text == reply.Description), Is.True);
            ButtonWithCaption(PayoffCaption(before, "escalate")).onClick.Invoke();
            Assert.That(director.Snapshot.deals.Count, Is.EqualTo(before.deals.Count));
            AssertReplyCommit(before, "escalate");
        }

        [UnityTest]
        public IEnumerator ReplyPayoffs_FinalThreeColumnUsesTheSameDescriptionsAndGuardedChoices()
        {
            yield return InstallReplyPayoff(ReplyCards.Confrontation, size: 3);
            Assert.That(director.IsFreeTimeBoard, Is.False);
            var before = director.Snapshot;
            Assert.That(NotebookText(), Does.Contain(ReplyCardPayoffs.Description(before, before.replyCards[0], ReplyCards.Find(ReplyCards.Confrontation, "deflect"))));
            ButtonWithCaption(PayoffCaption(before, "deflect")).onClick.Invoke();
            Assert.That(director.Snapshot.ledger.standings.Count, Is.EqualTo(before.ledger.standings.Count + 1));
            AssertReplyCommit(before, "deflect");
        }
    }
}
