using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Persistence;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The veto meeting's screens (PACK8-PASS-PLAN B3, the owner's mockups 81 and 82): every one of
    /// them on the strategy stage without a scroll at both text sizes in the default house, and in
    /// the largest the decision in view over a list that scrolls; who holds what in a strip across
    /// the header, the holder once however many things they are, and the captions the walks press
    /// where they always were. Screenshots 74 and 75 were the holder twice, a scroll, and "Emma
    /// Brown saves Emma Brown".
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>The step fits the stage: the column is no taller than the scroll that holds it.</summary>
        private void AssertTheMeetingFits(string where)
        {
            Canvas.ForceUpdateCanvases();
            var content = ActiveRect("Episode content");
            var viewport = (RectTransform)content.parent;
            Assert.That(content.rect.height, Is.LessThanOrEqualTo(viewport.rect.height + .5f),
                where + " fits without a scroll: " + content.rect.height.ToString("0") + " in " + viewport.rect.height.ToString("0") + ".");
        }

        /// <summary>Every label on the panel says all of its words in its box.</summary>
        private static void AssertNoMeetingLabelOverflows(RectTransform panel, string where)
        {
            foreach (var label in panel.GetComponentsInChildren<TMP_Text>())
            {
                label.ForceMeshUpdate();
                Assert.That(label.isTextOverflowing, Is.False, where + ": '" + label.text + "' fits its box.");
            }
        }

        /// <summary>The word on a card's pill of that name, or null without one.</summary>
        private static string MeetingPillOn(RectTransform card, string pill)
        {
            var rect = card.GetComponentsInChildren<RectTransform>().FirstOrDefault(part => part.name == pill);
            return rect == null ? null : rect.GetComponentInChildren<TMP_Text>().text;
        }

        /// <summary>The one face card of this houseguest on the panel.</summary>
        private static RectTransform MeetingCardOf(RectTransform panel, EpisodeState state, string id)
        {
            var cards = panel.GetComponentsInChildren<RectTransform>().Where(rect => rect.name == "Face · " + state.Find(id).name).ToArray();
            Assert.That(cards, Has.Length.EqualTo(1), state.Find(id).name + " is one card on the meeting's row.");
            return cards[0];
        }

        /// <summary>The headline the pinned way on wears over its caption, or null.</summary>
        private string MeetingHeadlineOn(string caption)
        {
            // An explicit null test, not ?.: a label is a UnityEngine.Object.
            var headline = FindButton(caption).GetComponentsInChildren<TMP_Text>().FirstOrDefault(label => label.name == EpisodeHud.MeetingHeadlineName);
            return headline == null ? null : headline.text;
        }

        /// <summary>
        /// A houseguest holds the veto from the block, and the player has nothing to decide: the
        /// holder is one card with both pills (screenshot 74 drew them twice), who holds what is a
        /// strip across the header in the house's own words, nothing on the screen offers a use or
        /// a keep or says what the holder will do, and "Continue episode" wears "HOLD THE MEETING".
        /// </summary>
        [UnityTest]
        public IEnumerator VetoMeeting_AHouseguestHoldingItFromTheBlockIsOneCardAndNothingSaysWhatTheyWillDo()
        {
            string holder = null, other = null;
            yield return InstallStrategySeason(48, state =>
            {
                AtVetoMeeting(state, false);
                holder = state.vetoHolderId = state.nominees[0];
                other = state.nominees[1];
            });
            var before = director.Snapshot;
            yield return AtBothTextSizes(larger =>
            {
                string where = "The meeting with a houseguest holding the veto" + (larger ? " at the larger text" : "");
                AssertOnTheStrategyStage("Continue episode", where);
                AssertTheMeetingFits(where);
                var panel = ActiveRect("Episode panel");
                Assert.That(ActiveRect(EpisodeHud.CeremonyTitleName).GetComponent<TMP_Text>().text, Is.EqualTo("Power of Veto Meeting"));

                var strip = ActiveRect(EpisodeHud.HouseStatusStripName);
                Assert.That(strip, Is.Not.Null, where + " says who holds what in a strip,");
                Assert.That(strip.IsChildOf(ActiveRect("Episode content")), Is.False, "across the header, outside the scroll,");
                Assert.That(strip.GetComponentsInChildren<TMP_Text>().Select(label => label.text), Does.Contain(EpisodeDirector.HouseStatus(before)),
                    "in the house's own words.");

                var card = MeetingCardOf(panel, before, holder);
                Assert.That(MeetingPillOn(card, "Role"), Is.EqualTo("VETO"), "The holder's card carries the veto");
                Assert.That(MeetingPillOn(card, EpisodeHud.BlockPillName), Is.EqualTo(EpisodeDirector.OnTheBlockPill), "and the block.");
                Assert.That(MeetingPillOn(MeetingCardOf(panel, before, other), "Role"), Is.EqualTo("NOM"));

                Assert.That(ButtonWithCaptionOrNull("Do not use the veto"), Is.Null, "The houseguest decides at the press:");
                foreach (var id in before.nominees)
                    Assert.That(ButtonWithCaptionOrNull("Save " + before.Find(id).name + " (HoH chooses replacement)"), Is.Null, "nothing to use,");
                Assert.That(PanelWords(panel).Any(words => words.Contains(" saves ") || words.Contains(" using the veto on ")), Is.False,
                    "and nothing says what they will do.");
                Assert.That(MeetingHeadlineOn("Continue episode"), Is.EqualTo(EpisodeDirector.HoldTheMeetingHeadline), "The way on wears the mockup's words.");
                AssertEveryLabelDraws(panel, where);
                AssertNoMeetingLabelOverflows(panel, where);
            });
            Assert.That(director.Snapshot.vetoResolved, Is.False, "Opening the meeting decides nothing.");
            if (Application.isBatchMode)
            {
                yield return OpenStation();
                yield return CaptureFraming("veto-meeting-npc-holder");
                director.ClosePanels();
                yield return null;
            }
        }

        /// <summary>
        /// The player holds the veto (mockup 81): the saves under a gold "USE THE VETO", peers in one
        /// row as PhasePanel_TheVetoHoldersSavesArePeers pins them, "Do not use the veto" its own
        /// row under them with "Keep nominations the same" beside its caption, the rule in a strip,
        /// the holder's own card - and no scroll at either text size. Keeping the block is still
        /// one press.
        /// </summary>
        [UnityTest]
        public IEnumerator VetoMeeting_TheHolderUsesOrKeepsTheBlockOnOneScreen()
        {
            yield return InstallStrategySeason(49, state => AtVetoMeeting(state, true));
            var before = director.Snapshot;
            yield return AtBothTextSizes(larger =>
            {
                string where = "The player's veto decision" + (larger ? " at the larger text" : "");
                Canvas.ForceUpdateCanvases();
                Assert.That(director.GetComponentInChildren<EpisodeHud>().CurrentActivityLayout, Is.EqualTo(EpisodeHud.ActivityLayout.Strategy),
                    where + " takes the strategy stage.");
                var panel = ActiveRect("Episode panel");
                AssertOnTheStage(panel);
                AssertTheMeetingFits(where);
                Assert.That(ActiveRect(EpisodeHud.CeremonyTitleName).GetComponent<TMP_Text>().text, Is.EqualTo("Power of Veto Meeting"),
                    "The decision names its ceremony.");
                var words = PanelWords(panel);
                Assert.That(words, Does.Contain(EpisodeDirector.HouseStatus(before)));
                Assert.That(words, Does.Contain(EpisodeDirector.UseTheVetoEyebrow));
                Assert.That(words, Does.Contain(EpisodeDirector.VetoInfoLine));

                var saves = before.nominees.Select(id => FindButton("Save " + before.Find(id).name + " (HoH chooses replacement)")).ToArray();
                if (!larger)
                {
                    Assert.That(saves[0].transform.parent.name, Is.EqualTo(EpisodeHud.ChoiceRowName));
                    Assert.That(saves[1].transform.parent, Is.SameAs(saves[0].transform.parent), "The nominees share one row.");
                }
                else foreach (var save in saves) Assert.That(save.transform.parent.name, Is.EqualTo("Episode content"), "one a row at the larger text.");
                var keep = FindButton("Do not use the veto");
                Assert.That(keep.transform.parent.name, Is.Not.EqualTo(EpisodeHud.ChoiceRowName), "Keeping the block is its own row,");
                Assert.That(keep.GetComponentsInChildren<TMP_Text>().Single(label => label.name == EpisodeHud.KeepSubtitleName).text,
                    Is.EqualTo(EpisodeDirector.KeepNominationsLine), "under the mockup's words for it,");
                Assert.That(ScreenRect((RectTransform)keep.transform).yMax, Is.LessThanOrEqualTo(ScreenRect((RectTransform)saves.Last().transform).yMin + .5f),
                    "and under the saves, as the mockup reads.");
                Assert.That(MeetingPillOn(MeetingCardOf(panel, before, before.playerId), "Role"), Is.EqualTo("VETO"), "The holder is on the row.");

                var pinned = panel.Cast<Transform>().Select(child => child.GetComponent<Button>())
                    .Where(button => button != null && button.IsActive() && button.name != "Close  [Esc]").ToArray();
                Assert.That(pinned, Is.Empty, "The decision is the way on; nothing is pinned beside it.");
                AssertEveryLabelDraws(panel, where);
                AssertNoMeetingLabelOverflows(panel, where);
            });
            Assert.That(director.Snapshot.vetoResolved, Is.False, "Opening the decision decides nothing.");

            yield return OpenStation();
            yield return null;
            ButtonWithCaption("Do not use the veto").onClick.Invoke();
            yield return null; yield return null;
            var after = director.Snapshot;
            Assert.That(after.vetoResolved, Is.True, "One press keeps the block.");
            Assert.That(after.nominees, Is.EquivalentTo(before.nominees));
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// The player is Head of Household and holder: whom to save is a pick on the screen, the
        /// first nominee until another is pressed, and then one list of who goes up in their place -
        /// every candidate's name once, where it was twice, one list under each nominee. The pick
        /// commits nothing; a name uses the veto on the nominee picked.
        /// </summary>
        [UnityTest]
        public IEnumerator VetoMeeting_TheHeadOfHouseholdHoldingItPicksWhomToSaveAndReadsEachNameOnce()
        {
            yield return InstallStrategySeason(50, state =>
            {
                AtVetoMeeting(state, true);
                state.hohId = state.playerId;
            });
            var before = director.Snapshot;
            string first = before.nominees[0], second = before.nominees[1];
            var candidates = EpisodeEngine.ReplacementCandidates(before).ToList();
            Assert.That(candidates, Has.Count.GreaterThanOrEqualTo(2), "The fixture has a choice of replacement.");
            yield return AtBothTextSizes(larger =>
            {
                string where = "The Head of Household's veto decision" + (larger ? " at the larger text" : "");
                AssertTheMeetingFits(where);
                foreach (var candidate in candidates)
                    Assert.That(director.GetComponentsInChildren<Button>(true).Count(button => button.IsActive()
                        && button.GetComponentsInChildren<TMP_Text>(true).Any(label => label.text == candidate.name)), Is.EqualTo(1),
                        where + " offers " + candidate.name + " once.");
                foreach (var nominee in before.nominees)
                    Assert.That(FindButton(EpisodeDirector.VetoSavePickCaption(before.Find(nominee).name)), Is.Not.Null, "Each nominee can be picked to save.");
                Assert.That(FindButton("Do not use the veto"), Is.Not.Null, "and the block can be kept.");
                var panel = ActiveRect("Episode panel");
                AssertEveryLabelDraws(panel, where);
                AssertNoMeetingLabelOverflows(panel, where);
            });

            yield return OpenStation();
            yield return null;
            ButtonWithCaption(EpisodeDirector.VetoSavePickCaption(before.Find(second).name)).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.Snapshot.vetoResolved, Is.False, "Picking whom to save decides nothing.");
            ButtonWithCaption(candidates[0].name).onClick.Invoke();
            yield return null; yield return null;
            var after = director.Snapshot;
            Assert.That(after.vetoResolved, Is.True, "A name uses the veto,");
            Assert.That(after.nominees, Is.EquivalentTo(new[] { first, candidates[0].id }), "on the nominee picked, and puts the name up.");
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// The largest house, with the player Head of Household and holder: thirteen houseguests can
        /// go up, more rows of faces and readings than the stage has left under the decision at
        /// either text size, so this step is taller than the stage. What runs past it is the end of
        /// that list, never a way to decide: with the column at its top, the title, both picks, "Do
        /// not use the veto" and the rule stand in the scroll's window, every name is listed under
        /// the rule that says a replacement follows, and each is offered once and draws.
        /// </summary>
        [UnityTest]
        public IEnumerator VetoMeeting_InTheLargestHouseTheHeadOfHouseholdHoldingItKeepsTheDecisionInView()
        {
            HoldTheHouseForTheFixture();
            var state = FullHouse(53, EpisodeValidation.MaximumCast);
            AtVetoMeeting(state, true);
            state.hohId = state.playerId;
            Assert.That(EpisodeValidation.TryValidate(state, out var invalid), Is.True, invalid);
            new EpisodeSaveStore(director.SavePath).Save(state);
            yield return ReloadEpisode();
            var before = director.Snapshot;
            var candidates = EpisodeEngine.ReplacementCandidates(before).ToList();
            Assert.That(candidates, Has.Count.EqualTo(EpisodeValidation.MaximumCast - 3), "Everybody but the player and the block can go up.");
            yield return AtBothTextSizes(larger =>
            {
                string where = "The Head of Household's veto decision in a house of " + EpisodeValidation.MaximumCast + (larger ? " at the larger text" : "");
                Canvas.ForceUpdateCanvases();
                Assert.That(director.GetComponentInChildren<EpisodeHud>().CurrentActivityLayout, Is.EqualTo(EpisodeHud.ActivityLayout.Strategy),
                    where + " takes the strategy stage.");
                // Read from the column's top, where the stage opens it: a selection may have moved it.
                var content = ActiveRect("Episode content");
                content.GetComponentInParent<ScrollRect>().verticalNormalizedPosition = 1f;
                Canvas.ForceUpdateCanvases();
                var window = ScreenRect((RectTransform)content.parent);
                AssertInside(window, ActiveRect(EpisodeHud.CeremonyTitleName), where + "'s title");
                foreach (var nominee in before.nominees)
                    AssertInside(window, (RectTransform)FindButton(EpisodeDirector.VetoSavePickCaption(before.Find(nominee).name)).transform,
                        where + "'s pick of " + before.Find(nominee).name);
                AssertInside(window, (RectTransform)FindButton("Do not use the veto").transform, where + "'s 'Do not use the veto'");
                var rule = ActiveRect(EpisodeHud.MeetingInfoStripName);
                Assert.That(rule, Is.Not.Null, where + " says what using the veto does.");
                AssertInside(window, rule, where + "'s rule");
                foreach (var candidate in candidates)
                {
                    var offered = director.GetComponentsInChildren<Button>(true).Where(button => button.IsActive()
                        && button.GetComponentsInChildren<TMP_Text>(true).Any(label => label.text == candidate.name)).ToArray();
                    Assert.That(offered, Has.Length.EqualTo(1), where + " offers " + candidate.name + " once.");
                    Assert.That(ScreenRect((RectTransform)offered[0].transform).yMax, Is.LessThanOrEqualTo(ScreenRect(rule).yMin + .5f),
                        where + " lists " + candidate.name + " under the rule.");
                }
                var panel = ActiveRect("Episode panel");
                AssertEveryLabelDraws(panel, where);
                AssertNoMeetingLabelOverflows(panel, where);
            });
            Assert.That(director.Snapshot.vetoResolved, Is.False, "Opening the decision decides nothing.");
        }

        /// <summary>
        /// The player is Head of Household and a houseguest holding the veto from the block saves
        /// herself: the screen says whose save it is in her own words - "Save X and nominate:" read
        /// as if the player were saving - the holder's card carries VETO and SAVED, and the
        /// candidates are peers in a row, as PhasePanel_TheVetoHoldersSavesArePeers pins them.
        /// </summary>
        [UnityTest]
        public IEnumerator VetoMeeting_TheHeadOfHouseholdHearsWhoseSaveItIsBeforeNamingTheReplacement()
        {
            string holder = null;
            yield return InstallStrategySeason(51, state =>
            {
                AtVetoMeeting(state, false);
                state.hohId = state.playerId;
                holder = state.vetoHolderId = state.nominees[0];
                state.Find(holder).pronouns = "she/her";
            });
            var before = director.Snapshot;
            Assert.That(EpisodeEngine.NpcVetoSave(before), Is.EqualTo(holder), "A holder on the block saves herself.");
            string line = before.Find(holder).name + " is using the veto on herself. Name the replacement nominee.";
            yield return AtBothTextSizes(larger =>
            {
                string where = "Naming the replacement" + (larger ? " at the larger text" : "");
                AssertTheMeetingFits(where);
                var panel = ActiveRect("Episode panel");
                var words = PanelWords(panel);
                Assert.That(words, Does.Contain(line), "The Head of Household hears whose save it is.");
                Assert.That(words.Any(said => said.StartsWith("Save ") && said.EndsWith(" and nominate:")), Is.False, "Nothing reads as the player's own save.");
                var card = MeetingCardOf(panel, before, holder);
                Assert.That((MeetingPillOn(card, "Role"), MeetingPillOn(card, EpisodeHud.BlockPillName)), Is.EqualTo(("VETO", EpisodeDirector.SavedPill)));
                var candidates = EpisodeEngine.ReplacementCandidates(before).Select(candidate => FindButton(candidate.name)).ToArray();
                if (!larger)
                {
                    Assert.That(candidates[0].transform.parent.name, Is.EqualTo(EpisodeHud.ChoiceRowName));
                    Assert.That(candidates[1].transform.parent, Is.SameAs(candidates[0].transform.parent), "The first two candidates share a row.");
                }
                Assert.That(ButtonWithCaptionOrNull("Do not use the veto"), Is.Null, "The block is not the Head of Household's to keep.");
                AssertEveryLabelDraws(panel, where);
                AssertNoMeetingLabelOverflows(panel, where);
            });
            Assert.That(director.Snapshot.vetoResolved, Is.False);
        }

        /// <summary>
        /// After the meeting (mockup 82): one row, built from the week's power row - the block as it
        /// stands, the replacement beside the holder with the replacement's marker between them, and
        /// the holder with VETO USED - under the meeting's own line in her own reflexive, over the
        /// mockup's VETO USED line, and "Continue episode" wears "CONTINUE TO EVICTION CAMPAIGN".
        /// </summary>
        [UnityTest]
        public IEnumerator VetoMeeting_AfterTheMeetingOneRowSaysWhoCameDownAndWhoWentUp()
        {
            var state = StrategySeason(52, shaped =>
            {
                AtVetoMeeting(shaped, false);
                shaped.vetoHolderId = shaped.nominees[0];
                foreach (var id in shaped.nominees) shaped.Find(id).nominationWeeks.Add(shaped.week);
            });
            string holder = state.vetoHolderId, kept = state.nominees[1];
            var engine = new EpisodeEngine(state);
            var result = engine.Apply(NextCommand(state));
            Assert.That(result.accepted, Is.True, result.reason);
            var resolved = engine.Snapshot;
            Assert.That(resolved.vetoResolved && resolved.phase == EpisodePhase.VetoMeeting, Is.True, "The meeting is over and its screen is next.");
            new EpisodeSaveStore(director.SavePath).Save(resolved);
            yield return ReloadEpisode();
            var before = director.Snapshot;
            string replacement = before.nominees.Single(id => id != kept);
            string reflexive = StoryPeople.Pronouns(before.Find(holder)).themselves;
            yield return AtBothTextSizes(larger =>
            {
                string where = "After the meeting" + (larger ? " at the larger text" : "");
                AssertOnTheStrategyStage("Continue episode", where);
                AssertTheMeetingFits(where);
                var panel = ActiveRect("Episode panel");
                Assert.That(ActiveRect(EpisodeHud.CeremonyTitleName).GetComponent<TMP_Text>().text, Is.EqualTo("The Veto Meeting Is Over"));
                var words = PanelWords(panel);
                Assert.That(words.Any(said => said.StartsWith(before.Find(holder).name + " saves " + reflexive + "; ")), Is.True,
                    "The meeting's line says the holder saved " + reflexive + ".");
                Assert.That(words, Does.Contain(EpisodeDirector.VetoUsedLine));

                var holderCard = MeetingCardOf(panel, before, holder);
                var replacementCard = MeetingCardOf(panel, before, replacement);
                Assert.That(MeetingPillOn(holderCard, "Role"), Is.EqualTo(EpisodeDirector.VetoUsedPill));
                Assert.That(MeetingPillOn(holderCard, EpisodeHud.BlockPillName), Is.Null, "The holder is off the block.");
                Assert.That(MeetingPillOn(replacementCard, "Role"), Is.EqualTo("NOM"));
                Assert.That(MeetingPillOn(MeetingCardOf(panel, before, kept), "Role"), Is.EqualTo("NOM"));
                var marker = ActiveRect(EpisodeHud.ReplacementMarkName);
                Assert.That(marker, Is.Not.Null, "The replacement is marked.");
                Assert.That(marker.GetComponentsInChildren<TMP_Text>().Select(label => label.text), Does.Contain(EpisodeDirector.ReplacementMarkWords));
                Assert.That(ScreenRect(replacementCard).xMax, Is.LessThanOrEqualTo(ScreenRect(marker).xMin + .5f), "The marker stands after the replacement");
                Assert.That(ScreenRect(marker).xMax, Is.LessThanOrEqualTo(ScreenRect(holderCard).xMin + .5f), "and before the holder.");
                Assert.That(MeetingHeadlineOn("Continue episode"), Is.EqualTo(EpisodeDirector.ToTheCampaignHeadline));
                AssertEveryLabelDraws(panel, where);
                AssertNoMeetingLabelOverflows(panel, where);
            });
            if (Application.isBatchMode)
            {
                yield return OpenStation();
                yield return CaptureFraming("veto-meeting-over");
                director.ClosePanels();
                yield return null;
            }
        }
    }
}
