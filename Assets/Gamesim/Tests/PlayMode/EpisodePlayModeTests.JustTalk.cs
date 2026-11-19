using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The free "Just talk" screen (free talk slice 1), the two things only a built screen shows: in a season at talk rules 1
    /// the row on the Connect page opens the talk's panel, a reply plays the talk to its end - their reaction and what changed,
    /// no action spent, the season's stream untouched - the row then says the window's talk is had, and the housemate's
    /// notebook page shows the fact learned; and a season at talk rules 0 shows no row and no block. The fixture installs
    /// the level itself (fresh seasons are enrolled only at integration) and holds the house still.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private IEnumerator InstallJustTalkHouse(int talkRules)
        {
            yield return InstallTalkingHouse(8, false, s =>
            {
                EpisodeEngine.EnableWeek(s); EpisodeEngine.EnableStory(s);
                s.story.talkRules = talkRules;
            });
            Assert.That(director.Snapshot.story.talkRules, Is.EqualTo(talkRules), "The fixture came back at its talk rules.");
        }

        [UnityTest]
        public IEnumerator JustTalk_TheRowPlaysATalkToItsEndAndTheNotebookShowsTheFact()
        {
            yield return InstallJustTalkHouse(TalkRules.Talk);
            Assert.That(FreeTalk.On(director.Snapshot), Is.True, "The fixture plays free talk.");
            // Somebody with a body whose talk on offer teaches a fact on one of its replies: the first talk is a layer-1 frame.
            var bodies = SceneComponents<HouseNpc>().Where(npc => npc.gameObject.activeInHierarchy).Select(npc => npc.Id).ToList();
            string id = null, tone = null;
            foreach (var who in director.Snapshot.Active.Where(c => !c.isPlayer && bodies.Contains(c.id)))
            {
                var offer = FreeTalk.Offer(director.Snapshot, who.id);
                if (!offer.Available) continue;
                var teaching = TalkScreenLines.Replies(director.Snapshot, who.id, offer.Talk)
                    .FirstOrDefault(r => !string.IsNullOrEmpty(FreeTalk.Result(director.Snapshot, who.id, offer.Talk.Id, r.Tone)?.Fact));
                if (teaching == null) continue;
                id = who.id; tone = teaching.Tone;
                break;
            }
            Assert.That(id, Is.Not.Null, "Somebody's talk on offer teaches a fact on one of its replies.");

            yield return TalkTo(id);
            yield return EnterConversationCategory(EpisodeDirector.ConnectCategoryCaption);
            Assert.That(ButtonWithCaption(EpisodeHud.JustTalkCaption).IsInteractable(), Is.True, "The free row is open.");
            ButtonWithCaption(EpisodeHud.JustTalkCaption).onClick.Invoke();
            yield return null;
            Assert.That(director.IsJustTalkOpen, Is.True, "The row opens the talk's panel.");
            Assert.That(director.JustTalkShown, Is.Null, "The panel opens on their line, before any reply.");

            // Read here, with the panel open and the house held: the state the replies were drawn from.
            var before = director.Snapshot;
            var talk = FreeTalk.Offer(before, id).Talk;
            Assert.That(talk, Is.Not.Null, "The talk is still on offer.");
            var expected = FreeTalk.Result(before, id, talk.Id, tone);
            Assert.That(expected, Is.Not.Null);
            Assert.That(expected.Fact, Is.Not.Null.And.Not.Empty, "The reply chosen teaches a fact.");
            string words = TalkScreenLines.Replies(before, id, talk).Single(r => r.Tone == tone).Words;
            Assert.That(ShownText(), Does.Contain(TalkScreenLines.ReplyHeading));

            ButtonWithCaption(words).onClick.Invoke();
            yield return null;
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(before.revision + 1), "One command: the talk.");
            Assert.That(after.acceptedCommandIds.Last(), Is.Not.Null);
            Assert.That(after.randomState, Is.EqualTo(before.randomState), "A free talk draws nothing from the season's stream.");
            Assert.That(after.windowActions, Is.EqualTo(before.windowActions), "A free talk spends no action.");
            Assert.That(director.IsJustTalkOpen, Is.True, "The panel stays for the third beat.");
            Assert.That(director.JustTalkShown, Is.Not.Null, "The third beat is on show.");
            Assert.That(director.JustTalkShown.Fact, Is.EqualTo(expected.Fact), "It shows what the engine applied.");
            string shown = ShownText();
            Assert.That(shown, Does.Contain(TalkScreenLines.ChangedHeading));
            Assert.That(shown, Does.Contain(expected.Outcome));
            Assert.That(shown, Does.Contain(TalkScreenLines.ChangeLine(expected)), "What changed, in one plain line.");

            ButtonWithCaption(EpisodeDirector.JustTalkBackCaption).onClick.Invoke();
            yield return null;
            Assert.That(director.IsJustTalkOpen, Is.False, "The way back leaves the panel.");
            var row = ButtonWithCaptionOrNull(EpisodeHud.JustTalkCaption);
            Assert.That(row, Is.Not.Null, "The row stays on the Connect page,");
            Assert.That(row.IsInteractable(), Is.False, "locked: this window's talk is had.");
            Assert.That(ShownText(), Does.Contain(FreeTalk.TalkedReason), "and it says why.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(after.revision), "Going back commits nothing.");

            director.ClosePanels();
            yield return null;
            director.ShowHouseguestProfile(id);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var card = ActiveRect(EpisodeHud.ProfileBlockPrefix + TalkNotes.WhatYouKnowHeading);
            Assert.That(card, Is.Not.Null, "The notebook page has the block.");
            var labels = card.GetComponentsInChildren<TMP_Text>().Select(t => t.text).ToArray();
            Assert.That(labels, Has.Some.EqualTo("· " + expected.Fact), "The fact learned is on their page.");
            Assert.That(labels.Any(l => l == TalkNotes.WhoTheyAre || l == TalkNotes.Background || l == TalkNotes.Drives || l == TalkNotes.Secrets),
                Is.True, "Under a layer's plain heading.");
        }

        [UnityTest]
        public IEnumerator JustTalk_ASeasonAtTalkRulesZeroShowsNoRowAndNoBlock()
        {
            yield return InstallJustTalkHouse(0);
            string id = Listener(director.Snapshot).id;
            yield return TalkTo(id);
            yield return EnterConversationCategory(EpisodeDirector.ConnectCategoryCaption);
            int revision = director.Snapshot.revision;
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.JustTalkCaption), Is.Null, "No free row at talk rules 0.");
            Assert.That(ButtonWithCaption(EpisodeHud.SmallTalkCaption), Is.Not.Null, "The Connect page is drawn as it always was.");
            director.ClosePanels();
            yield return null;
            director.ShowHouseguestProfile(id);
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(ActiveRect(EpisodeHud.ProfileIdentityName), Is.Not.Null, "The profile is open.");
            Assert.That(ActiveRect(EpisodeHud.ProfileBlockPrefix + TalkNotes.WhatYouKnowHeading), Is.Null, "No block at talk rules 0.");
            Assert.That(ActiveRect(EpisodeHud.ProfileBlockPrefix + TalkNotes.FromThemHeading), Is.Null);
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "Nothing was committed.");
        }
    }
}
