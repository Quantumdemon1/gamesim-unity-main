using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The free "Just talk" screen at talk rules 3 (free talk slice 3), the two things only a built screen shows: a talk with a
    /// housemate the engine opens Flirt with carries the Flirt reply, last, and a declaration left open with another housemate
    /// shows its choices as the panel's buttons by their captions; at talk rules 2, with the same rapport behind them, neither is
    /// drawn. The fixture installs the level itself (fresh seasons are enrolled only at integration) and holds the house still.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// A house at a talk level with the romance switch on: every other housemate (by cast order) well clear of every flirt
        /// gate (rapport 20 against the highest gate of 15, agreement 6, three talks), the rest with this window's talk had and,
        /// at talk rules 3 only, their declaration left open (the row of their pool in contact.talkPending, a spark of 60, two Flirts).
        /// </summary>
        private IEnumerator InstallRomanceTalkHouse(int talkRules)
        {
            yield return InstallTalkingHouse(8, false, s =>
            {
                EpisodeEngine.EnableWeek(s); EpisodeEngine.EnableStory(s);
                s.story.talkRules = talkRules;
                s.story.romanceStorylines = true;
                int index = 0;
                foreach (var who in s.contestants.Where(c => !c.isPlayer && c.status == ContestantStatus.Active).ToList())
                {
                    var contact = s.story.contacts.FirstOrDefault(c => c.npcId == who.id);
                    if (contact == null) s.story.contacts.Add(contact = new ContactState { npcId = who.id });
                    contact.rapport = 20;
                    contact.talks = 3;
                    contact.agreement = 6;
                    if (index++ % 2 == 1)
                    {
                        contact.talkWindow = FreeTalk.WindowKey(s);
                        // The romance's saved fields only where the level writes them (talk rules 3).
                        if (talkRules < TalkRules.Romance) continue;
                        string pool = FreeTalk.PoolOf(who);
                        var row = TalkBank.Rows.FirstOrDefault(r => r.Kind == TalkKind.Declaration && r.Pool == pool);
                        Assert.That(row, Is.Not.Null, "The tables hold a declaration for the pool " + pool + ".");
                        contact.talkPending = row.Id;
                        contact.romance = TalkRules.Declared;
                        contact.spark = 60;
                        contact.flirts = 2;
                    }
                }
            });
            Assert.That(director.Snapshot.story.talkRules, Is.EqualTo(talkRules), "The fixture came back at its talk rules.");
        }

        private List<string> RomanceTalkBodies() =>
            SceneComponents<HouseNpc>().Where(npc => npc.gameObject.activeInHierarchy).Select(npc => npc.Id).ToList();

        private IEnumerator OpenJustTalkPanel(string id)
        {
            yield return TalkTo(id);
            yield return EnterConversationCategory(EpisodeDirector.ConnectCategoryCaption);
            ButtonWithCaption(EpisodeHud.JustTalkCaption).onClick.Invoke();
            yield return null;
            Assert.That(director.IsJustTalkOpen, Is.True, "The row opens the talk's panel.");
        }

        [UnityTest]
        public IEnumerator JustTalkRomance_AtTalkRulesThreeTheFlirtReplyAndADeclarationsChoicesAreButtons()
        {
            yield return InstallRomanceTalkHouse(TalkRules.Romance);
            var state = director.Snapshot;
            Assert.That(TalkRomance.On(state), Is.True, "The fixture plays romance.");
            var bodies = RomanceTalkBodies();
            var flirter = state.Active.FirstOrDefault(c => !c.isPlayer && bodies.Contains(c.id) && FreeTalk.Offer(state, c.id).Available
                && TalkRomance.FlirtOffered(state, c.id, FreeTalk.Offer(state, c.id).Talk));
            Assert.That(flirter, Is.Not.Null, "Somebody with a body has an ordinary talk on offer and Flirt open.");
            string id = flirter.id;
            int revision = state.revision;

            yield return OpenJustTalkPanel(id);
            var flirt = ButtonWithCaptionOrNull(TalkScreenLines.FlirtCaption);
            Assert.That(flirt, Is.Not.Null, "The Flirt reply is on the panel.");
            Assert.That(flirt.IsInteractable(), Is.True);
            var talk = FreeTalk.Offer(director.Snapshot, id).Talk;
            foreach (var reply in TalkScreenLines.Replies(director.Snapshot, id, talk))
                Assert.That(ButtonWithCaptionOrNull(reply.Words), Is.Not.Null, "The talk's own replies stay: " + reply.Words);
            var replyButtons = TalkScreenLines.Replies(director.Snapshot, id, talk).Select(r => ButtonWithCaptionOrNull(r.Words)).ToList();
            Assert.That(replyButtons.All(b => b.transform.GetSiblingIndex() < flirt.transform.GetSiblingIndex()), Is.True,
                "Flirt is placed after the talk's replies.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "Opening the panel commits nothing.");

            director.ClosePanels();
            yield return null;
            var declared = director.Snapshot.Active.FirstOrDefault(c => !c.isPlayer && bodies.Contains(c.id)
                && TalkScreenLines.PendingScene(director.Snapshot, c.id) != null);
            Assert.That(declared, Is.Not.Null, "Somebody with a body has a declaration left open this window.");
            var open = TalkScreenLines.PendingScene(director.Snapshot, declared.id);
            Assert.That(open.Row.Kind, Is.EqualTo(TalkKind.Declaration));
            Assert.That(open.Choices.Select(c => c.Choice), Is.SupersetOf(new[] { TalkChoice.Accept, TalkChoice.NotYet, TalkChoice.JustFriends }),
                "A declaration offers Accept, Not yet and Just friends.");

            yield return OpenJustTalkPanel(declared.id);
            foreach (var choice in open.Choices)
            {
                var button = ButtonWithCaptionOrNull(choice.Caption);
                Assert.That(button, Is.Not.Null, "The choice is a button: " + choice.Caption);
                Assert.That(button.IsInteractable(), Is.True, choice.Caption);
            }
            Assert.That(ButtonWithCaptionOrNull(TalkScreenLines.FlirtCaption), Is.Null, "A declaration's panel offers no Flirt reply.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "Nothing was committed.");
        }

        [UnityTest]
        public IEnumerator JustTalkRomance_AtTalkRulesTwoNeitherIsDrawn()
        {
            yield return InstallRomanceTalkHouse(TalkRules.Chemistry);
            var state = director.Snapshot;
            Assert.That(TalkScreenLines.RomanceShown(state), Is.False);
            var bodies = RomanceTalkBodies();
            var talker = state.Active.FirstOrDefault(c => !c.isPlayer && bodies.Contains(c.id) && FreeTalk.Offer(state, c.id).Available);
            Assert.That(talker, Is.Not.Null, "Somebody with a body has a talk on offer.");
            int revision = state.revision;

            yield return OpenJustTalkPanel(talker.id);
            Assert.That(ButtonWithCaptionOrNull(TalkScreenLines.FlirtCaption), Is.Null, "No Flirt reply at talk rules 2.");
            foreach (TalkChoice choice in System.Enum.GetValues(typeof(TalkChoice)))
                if (choice != TalkChoice.None)
                    Assert.That(ButtonWithCaptionOrNull(TalkScreenLines.ChoiceCaption(choice)), Is.Null, "No choice button: " + choice);

            // Every other housemate has had this window's talk: the row is locked, as at slice 2.
            director.ClosePanels();
            yield return null;
            var had = director.Snapshot.Active.FirstOrDefault(c => !c.isPlayer && bodies.Contains(c.id)
                && FreeTalk.Offer(director.Snapshot, c.id).Refusal == TalkRefusal.TalkedThisWindow);
            Assert.That(had, Is.Not.Null, "Somebody with a body has had this window's talk.");
            yield return TalkTo(had.id);
            yield return EnterConversationCategory(EpisodeDirector.ConnectCategoryCaption);
            var row = ButtonWithCaptionOrNull(EpisodeHud.JustTalkCaption);
            Assert.That(row, Is.Not.Null, "The row is on the Connect page,");
            Assert.That(row.IsInteractable(), Is.False, "locked: this window's talk is had.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "Nothing was committed.");
        }
    }
}
