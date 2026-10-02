using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    /// The pickers, decorated (UI-UX-PASS-PLAN P1, the play sweep's row 15): a person's card in one of
    /// the conversation's pickers keeps the caption its row had as its own label, whole and on screen,
    /// and is drawn around it - the person's name large over the caption, which stands in the muted
    /// small type of the verb every card repeats; a target agreement's stakes on a chip with the
    /// player's read of the chance beside it, "no read" exactly where the table says it has little to
    /// go on; and the player's own trust of the person as a bar and in words. The geometry and the
    /// content each fit test asserts are in <see cref="AssertPickerFits"/>
    /// (EpisodePlayModeTests.ConversationGroups.cs); these tests drive it through every picker, both
    /// frames and both text sizes in a house of sixteen.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// A house whose readings run the bar's length: the player's own trust of seven of them, from
        /// the last houseguest back, spread from -100 to 100 and across the three colours, each the
        /// other way about from theirs of the player - a bar that read their view of the player would
        /// read every one of them wrong. The last of them shares a pact with the player, and in the
        /// campaign the second nominee does too, so ALLY shows in every picker; the nominees' readings
        /// differ, so the promise's two cards do.
        /// </summary>
        private static void ReadingsAcrossTheBar(EpisodeState state)
        {
            var npcs = state.Active.Where(c => !c.isPlayer).ToList();
            double[] readings = { 42, -60, 100, -100, 6, -5, 73 };
            for (int i = 0; i < readings.Length && i < npcs.Count; i++)
            {
                string id = npcs[npcs.Count - 1 - i].id;
                Reading(state, state.playerId, id, readings[i]);
                Reading(state, id, state.playerId, -readings[i]);
            }
            var members = new List<string> { state.playerId, npcs[npcs.Count - 1].id };
            if (state.nominees.Count > 1)
            {
                Reading(state, state.playerId, state.nominees[0], -35);
                Reading(state, state.nominees[0], state.playerId, 35);
                Reading(state, state.playerId, state.nominees[1], 55);
                Reading(state, state.nominees[1], state.playerId, -55);
                members.Add(state.nominees[1]);
            }
            state.alliances.Add(new AllianceState { id = FitAllianceId, name = "The Fit", active = true, members = members });
        }

        /// <summary>
        /// A person's card keeps the caption its row had as its own label: the button's first label, a
        /// direct child, every letter of it drawn, in the muted type, and the one live control carrying
        /// those words - which is how a test, the audited walk and a screen reader find it. What is
        /// drawn around it - the name, the chip and its chance, the reading and ALLY - is no live
        /// control's caption, so none of it can make a caption ambiguous.
        /// </summary>
        private void AssertCardKeepsItsCaption(RectTransform cell, string where)
        {
            string caption = cell.name;
            string about = where + ": '" + caption + "'";
            var button = cell.GetComponent<Button>();
            Assert.That(button != null, Is.True, about + " is a control.");
            var words = cell.GetComponentInChildren<TMP_Text>(true);
            Assert.That(words != null && words.text == caption, Is.True, about + ": the button's first label is its caption, word for word.");
            Assert.That(words.transform.parent, Is.SameAs(cell.transform), about + ": the caption is the card's own label.");
            Assert.That(words.gameObject.activeInHierarchy && words.enabled && words.color.a > .99f, Is.True, about + ": the caption is on screen.");
            Assert.That(words.color, Is.EqualTo(UiTheme.Muted), about + ": the caption is in the muted type.");
            words.ForceMeshUpdate(true);
            Assert.That(words.isTextOverflowing, Is.False, about + ": the caption is cut off.");
            int letters = caption.Count(letter => !char.IsWhiteSpace(letter));
            Assert.That(words.textInfo.characterInfo.Take(words.textInfo.characterCount).Count(glyph => glyph.isVisible), Is.EqualTo(letters),
                about + ": every letter of the caption is drawn.");

            var carrying = ActiveButtons(caption);
            Assert.That(carrying.Count, Is.EqualTo(1), about + ": one live control carries these words, not " + carrying.Count + ".");
            Assert.That(carrying[0], Is.SameAs(button), about + ": and it is this card.");
            Assert.That(FindButton(caption), Is.SameAs(button), about + ": found by its caption as every test finds a control.");

            var captions = director.GetComponentsInChildren<Button>().Where(control => control.IsActive()).Select(control => control.name).ToList();
            foreach (var label in cell.GetComponentsInChildren<TMP_Text>(true).Where(label => label != words && !string.IsNullOrWhiteSpace(label.text)))
                Assert.That(captions, Does.Not.Contain(label.text),
                    about + ": '" + label.text + "' (" + label.name + ") is drawn around the caption, and no control is called that.");
        }

        /// <summary>
        /// Every person in every picker a conversation offers, in free time and the campaign of a house
        /// of sixteen, keeps the caption their row had, whole and on screen, as the one control those
        /// words find; their name stands large over it; and the bar and the words at the foot read the
        /// player's own trust of them - spread from -100 to 100 here, and the other way about from
        /// theirs of the player - in the colour the ring and ALLY share.
        /// </summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator PickerCards_EveryPersonKeepsTheirCaptionWholeUnderTheirNameAndTheBarIsYourOwnTrust()
        {
            foreach (bool campaign in new[] { false, true })
            {
                yield return InstallTalkingHouse(16, campaign, ReadingsAcrossTheBar);
                yield return SettleCast();
                var listener = Listener(director.Snapshot);
                yield return TalkTo(listener.id);
                Assert.That(director.IsConversationOpen, Is.True, "The conversation opens.");
                var state = director.Snapshot;
                string where = (campaign ? "The campaign" : "Free time") + " in a house of 16";
                var pickers = ExpectedPickers(state, listener.id);
                Assert.That(pickers.Count, Is.GreaterThanOrEqualTo(campaign ? 5 : 4), where + ": the conversation's pickers.");
                foreach (var (verb, people) in pickers)
                {
                    yield return PressRow(verb);
                    string open = where + " with '" + verb + "' open";
                    var cells = PickerCells(verb, open);
                    Assert.That(cells.Select(cell => cell.name), Is.EquivalentTo(people), open + ": every person under the caption their row had.");
                    foreach (var cell in cells) AssertCardKeepsItsCaption(cell, open);
                    AssertPickerFits(verb, open);
                    // The bars say something here: the readings run from one end of the scale to the other.
                    var scores = cells.Select(cell => state.Score(state.playerId, PersonOnCell(state, cell.name).id)).Distinct().ToList();
                    Assert.That(scores.Count, Is.GreaterThan(1), open + ": the cards' readings differ, so their bars do: " + string.Join(", ", scores));
                }
                director.ClosePanels();
                yield return null;
            }
        }

        /// <summary>
        /// A target agreement's card says "no read" exactly where the deal table says it has little to
        /// go on and the card's person is one the player knows nothing of. With no read of the
        /// houseguest, no claim and no history the table draws its "Many unknowns" chip, and every card
        /// says "no read" in the muted type - but the one whose person the house was overheard being
        /// cold on, whose card carries the read's word. With a current read and a history there is no
        /// chip, and every card says the read's word.
        /// </summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator PickerCards_NoReadStandsExactlyWhereTheTableSaysItHasLittleToGoOn()
        {
            string known = null;
            yield return InstallTalkingHouse(8, false, season =>
            {
                season.memories.RemoveAll(memory => memory.ownerId == season.playerId);
                var npcs = season.Active.Where(c => !c.isPlayer).ToList();
                known = npcs.Last().id;
                foreach (var npc in npcs.Where(c => c.id != known))
                    season.ledger.standings.Add(new StandingRow { week = season.week, fromId = npc.id, toId = known, source = ClaimSource.Overheard, score = -60 });
            });
            yield return SettleCast();
            var listener = Listener(director.Snapshot);
            Assert.That(listener.id, Is.Not.EqualTo(known), "The player talks to somebody other than the one the house was overheard on.");
            yield return TalkTo(listener.id);
            var state = director.Snapshot;
            string where = "A conversation with nothing to go on";
            Assert.That(KnownOdds.Unknowns(state, listener.id), Is.EqualTo(KnownOdds.Many), where + ": no read, no claim, no history.");
            yield return PressRow(EpisodeDirector.TargetDealPickerCaption);
            Assert.That(ActiveRect(EpisodeHud.UnknownsChipName) != null, Is.True, where + ": the table says it has little to go on.");
            var cells = PickerCells(EpisodeDirector.TargetDealPickerCaption, where);
            Assert.That(cells.Select(cell => PersonOnCell(state, cell.name).id), Does.Contain(known),
                where + ": the one the house was overheard on is among the cards.");
            foreach (var cell in cells)
            {
                var person = PersonOnCell(state, cell.name);
                string about = where + ": '" + cell.name + "'";
                var chance = cell.Find(EpisodeHud.PickerChanceName);
                Assert.That(chance != null, Is.True, about + " says the player's read of the chance.");
                var words = chance.GetComponent<TMP_Text>();
                if (person.id == known)
                {
                    Assert.That(words.text, Is.EqualTo(KnownOdds.Deal(state, listener.id, DealKind.TargetAgreement, person.id).word).And.Not.EqualTo(KnownOdds.NoRead),
                        about + ": the house was overheard being cold on " + person.name + ", and the card says the read that gives.");
                    Assert.That(words.color, Is.EqualTo(UiTheme.Paper), about + ": a read is drawn in the paper type.");
                }
                else
                {
                    Assert.That(words.text, Is.EqualTo(KnownOdds.NoRead), about + ": nothing behind it, and it says so.");
                    Assert.That(words.color, Is.EqualTo(UiTheme.Muted), about + ": in the muted type.");
                }
            }
            AssertPickerFits(EpisodeDirector.TargetDealPickerCaption, where);
            director.ClosePanels();
            yield return null;

            yield return InstallTalkingHouse(8, false, season =>
            {
                foreach (var npc in season.Active.Where(c => !c.isPlayer))
                {
                    season.ledger.standings.Add(new StandingRow { week = season.week, fromId = npc.id, toId = season.playerId, source = ClaimSource.Read, score = 30 });
                    for (int i = 1; i <= KnownOdds.HistoryEnough; i++)
                        season.memories.Add(new MemoryState
                        {
                            ownerId = season.playerId, subjectId = npc.id, week = season.week,
                            text = "A long talk with " + npc.name + " on move-in night, the " + (i == 1 ? "first" : "second") + " of a few.",
                        });
                }
            });
            yield return SettleCast();
            listener = Listener(director.Snapshot);
            yield return TalkTo(listener.id);
            state = director.Snapshot;
            where = "A conversation with a read and a history";
            Assert.That(KnownOdds.Unknowns(state, listener.id), Is.EqualTo(KnownOdds.Few), where + ": a current read and a history to go on.");
            yield return PressRow(EpisodeDirector.TargetDealPickerCaption);
            Assert.That(ActiveRect(EpisodeHud.UnknownsChipName) == null, Is.True, where + ": the table says nothing of unknowns.");
            foreach (var cell in PickerCells(EpisodeDirector.TargetDealPickerCaption, where))
            {
                var person = PersonOnCell(state, cell.name);
                Assert.That(ChanceOn(cell.GetComponent<Button>()),
                    Is.EqualTo(KnownOdds.Deal(state, listener.id, DealKind.TargetAgreement, person.id).word).And.Not.EqualTo(KnownOdds.NoRead),
                    where + ": '" + cell.name + "' says the read's word.");
            }
            AssertPickerFits(EpisodeDirector.TargetDealPickerCaption, where);
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// The decorated pickers fit the HUD as a capture lays it out on the 16:9 frame and on the 4:3,
        /// at both text sizes, in a house of sixteen: in free time venting, a lie, a call-out (whose
        /// captions carry the name mid-sentence) and the target agreements; in the campaign the promise
        /// about the vote and the target agreements. On each frame every row keeps room for its words
        /// (the cards are no rows, and leave the rows as they were), nothing on the panel is cut off or
        /// draws nothing, and every card fits as <see cref="AssertPickerFits"/> says.
        /// </summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator PickerCards_FitOnBothFramesAtBothTextSizesInAHouseOfSixteen()
        {
            foreach (bool campaign in new[] { false, true })
            {
                yield return InstallTalkingHouse(16, campaign,
                    AlliedWith(season => campaign ? season.nominees[1] : season.Active.Last(c => !c.isPlayer).id));
                yield return SettleCast();
                var verbs = campaign
                    ? new[] { EpisodeDirector.PromiseToEvictPickerCaption, EpisodeDirector.TargetDealPickerCaption }
                    : new[] { EpisodeDirector.VentPickerCaption, EpisodeDirector.LiePickerCaption, EpisodeDirector.CalloutPickerCaption, EpisodeDirector.TargetDealPickerCaption };
                foreach (bool larger in new[] { false, true })
                {
                    yield return ApplyTextSize(larger);
                    var listener = Listener(director.Snapshot);
                    yield return TalkTo(listener.id);
                    string where = (campaign ? "The campaign" : "Free time") + " in a house of 16" + (larger ? " at the larger text" : "");
                    foreach (string verb in verbs)
                    {
                        yield return PressRow(verb);
                        string open = where + " with '" + verb + "' open";
                        AssertPickerFits(verb, open);
                        yield return AtBothFrames(frame =>
                        {
                            string at = open + " on the " + frame + " frame";
                            AssertRowsHaveRoom(larger, at);
                            AssertCopyHolds(at);
                            AssertPickerFits(verb, at);
                        });
                    }
                    director.ClosePanels();
                    yield return null;
                }
                yield return ApplyTextSize(false);
            }
        }
    }
}
