using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The Head of Household's pitch board (ROLE-MOMENTS-PLAN R5, §3.1) on the offer board R2 built: under the role moments the
    /// nomination's pitches are portrait cards - each saying the tone, the situation, the read, the line, the recommendation,
    /// the stakes and what a no and silence cost in labels of their own - over the pitch's four answers under their own captions,
    /// unchanged, each the card's own answer to a screen reader and to the keyboard's return after a rebuild; any card answers
    /// first; at the largest text the cards page; nothing on the board moves. A season without the role moments keeps the pitch
    /// it always drew, one at a time.
    ///
    /// <para>The fixture is the pitches' own (<see cref="InstallHoHPitches"/>'s house): the player Head of Household before the
    /// nominations with three pitches waiting, here under the story at Roles - with the strategy windows, the commitment rules,
    /// the levers, the economy and agency the board needs - and three houseguests shaped to wear three tones.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>The pitch's four answers, by their captions, unchanged: the three answers and the free question.</summary>
        private static string[] PitchBoardCaptions() =>
            ReplyCards.Replies(ReplyCards.Pitch).Select(reply => EpisodeHud.ReplyCaption(reply.Label)).Concat(new[] { EpisodeHud.FeelOutPitchCaption }).ToArray();

        /// <summary>
        /// The player Head of Household before the nominations, three pitches waiting - from a Threatening houseguest, an Earnest
        /// one and a Pragmatic one, each recommending somebody - under the role moments, or, without <paramref name="roles"/>, the
        /// shipped story rules; saved and loaded, with the house's own clock held so nothing commits under the test.
        /// </summary>
        private IEnumerator InstallPitchBoard(bool roles, bool fullRecord = false)
        {
            yield return InstallTalkingHouse(8, false, state =>
            {
                EpisodeEngine.EnableWeek(state);
                EpisodeEngine.EnableLevers(state);
                EpisodeEngine.EnableCommitments(state);
                EpisodeEngine.EnableAgency(state);
                EpisodeEngine.EnableStory(state);
                if (roles) state.story.rulesVersion = StoryRules.Roles;
                state.economyRulesVersion = 1;
                state.phase = EpisodePhase.Nomination;
                state.hohId = state.playerId;
                state.nominees.Clear();
                state.houseEvents.Clear();
                state.replyCards.Clear();
                state.alliances.RemoveAll(pact => pact.members.Contains(state.playerId));
                var npcs = state.Active.Where(person => !person.isPlayer).ToArray();
                var traits = new[] { new List<string> { "Competitive", "Stubborn" }, new List<string> { "Emotional" }, new List<string>() };
                for (int i = 0; i < traits.Length; i++)
                {
                    npcs[i].traits = traits[i];
                    var edge = state.relationships.FirstOrDefault(r => r.fromId == state.playerId && r.toId == npcs[i].id);
                    if (edge == null) state.relationships.Add(edge = new RelationshipState { fromId = state.playerId, toId = npcs[i].id });
                    edge.score = 0;
                    state.replyCards.Add(new ReplyCardState { id = "reply-pitch-" + state.week + "-" + npcs[i].id, week = state.week,
                        kind = ReplyCards.Pitch, fromId = npcs[i].id, aboutId = npcs[i + 3].id });
                }
                // A full promise record (R5b), settled promises to a houseguest who is not pitching: every card's "Promise safety"
                // locked, and its lock line said.
                if (fullRecord)
                    for (int p = 0; p < NpcPromises.PromiseCeiling; p++)
                        state.promises.Add(new PromiseState { id = "full-record-promise-" + p, fromId = state.playerId, toId = npcs[6].id,
                            kind = PromiseKind.Safety, status = PromiseStatus.Expired, week = state.week, expiresWeek = state.week + 1 });
            });
            director.SuspendNpcAutonomyForDiagnostics();
            var installed = director.Snapshot;
            Assert.That(EpisodeEngine.RolesOn(installed) && HoHPitches.OnTheBoard(installed), Is.EqualTo(roles), "The board plays only under the role moments.");
            Assert.That(installed.replyCards, Has.Count.EqualTo(3));
        }

        /// <summary>The captions sweep for the pitch board: every control says one caption, is named by it, and is a pitch's answer or the pager's; each card has one of each answer.</summary>
        private static void AssertPitchBoardCaptions(RectTransform board, string where)
        {
            var answers = PitchBoardCaptions();
            var allowed = answers.Concat(new[] { EpisodeHud.PreviousOfferCaption, EpisodeHud.NextOfferCaption }).ToArray();
            foreach (var button in board.GetComponentsInChildren<Button>())
            {
                var words = button.GetComponentsInChildren<TMP_Text>(true).Select(label => label.text).ToArray();
                Assert.That(words, Has.Length.EqualTo(1), where + ": '" + button.name + "' says its caption and nothing else: " + string.Join(" | ", words));
                Assert.That(allowed, Does.Contain(words[0]), where + ": '" + words[0] + "' is a caption the board may carry.");
                Assert.That(button.name, Is.EqualTo(words[0]), where + ": a control is named by its caption.");
            }
            foreach (var card in OfferCards(board))
                foreach (var caption in answers)
                    Assert.That(card.GetComponentsInChildren<Button>().Count(button => button.name == caption), Is.EqualTo(1), where + ": " + card.name + " has one '" + caption + "'.");
        }

        /// <summary>The player's answer to a pitch, as the board and the old card both commit it.</summary>
        private static EpisodeCommand PitchAnswer(EpisodeState s, string cardId, string key) => new EpisodeCommand
        {
            id = "pitch-board-expected-" + s.revision, actorId = s.playerId, kind = EpisodeCommandKind.ReplyToHouseguest,
            targetId = cardId, text = key, expectedRevision = s.revision, expectedPhase = s.phase,
        };

        /// <summary>The season the press committed is the one the same answer commits on the engine; only the command's own id differs.</summary>
        private static void AssertThePitchAnswer(EpisodeState before, EpisodeState after, string cardId, string key, string what)
        {
            var expected = new EpisodeEngine(before).Apply(PitchAnswer(before, cardId, key));
            Assert.That(expected.accepted, Is.True, expected.reason);
            Assert.That(after.revision, Is.EqualTo(before.revision + 1), what + " is one commit.");
            var mine = expected.state.Clone();
            mine.acceptedCommandIds = new List<string>(after.acceptedCommandIds);
            Assert.That(JsonUtility.ToJson(after), Is.EqualTo(JsonUtility.ToJson(mine)), what + " commits what the engine's answer commits.");
        }

        /// <summary>
        /// Under the role moments the pitches are the board's cards, in its order, every word the card's own label and every
        /// answer the card's own under its unchanged caption; the last card answers first, committing exactly the engine's
        /// answer, and leaves; a pitcher felt out keeps their card, the question locked and their answer on it.
        /// </summary>
        [UnityTest]
        public IEnumerator PitchBoard_UnderRolesThePitchesAreCardsAnsweredInAnyOrder()
        {
            yield return InstallPitchBoard(true);
            yield return OpenStation();
            LandThePanel();
            yield return null;
            var state = director.Snapshot;
            var pitches = RoleOffers.PitchBoardCards(state);
            var board = LastActive(EpisodeHud.OfferBoardName);
            Assert.That(board, Is.Not.Null, "Under the role moments the pitches are a board,");
            Assert.That(ActiveRect(EpisodeHud.StoryStepName), Is.Null, "not the one pitch at a time.");
            Assert.That(OfferLabel(board, EpisodeHud.OfferEyebrowName), Is.EqualTo(EpisodeHud.PitchBoardEyebrow));
            Assert.That(OfferLabel(board, EpisodeHud.OfferLineName), Is.EqualTo(EpisodeDirector.PitchBoardLine));
            Assert.That(OfferLabel(board, EpisodeHud.OfferFreeName), Is.EqualTo(EpisodeHud.OfferBoardFreeLine));
            Assert.That(OfferLabel(board, EpisodeHud.OfferSilenceName), Is.Null, "An unanswered pitch costs nothing, and nothing says it does.");

            var cards = OfferCards(board);
            Assert.That(cards.Select(card => card.name), Is.EqualTo(pitches.Select(pitch => EpisodeHud.OfferCardPrefix + state.Find(pitch.fromId).name)),
                "A card a pitch, in the board's order.");
            var hud = Hud;
            var tones = new List<string>();
            for (int i = 0; i < cards.Length; i++)
            {
                var words = RoleOffers.PitchCard(state, pitches[i]);
                string who = words.name;
                tones.Add(words.tone);
                Assert.That(OfferLabel(cards[i], EpisodeHud.OfferNameName), Is.EqualTo(who));
                Assert.That(OfferLabel(cards[i], EpisodeHud.OfferChipName), Is.EqualTo("NOMINATION PITCH"), who);
                Assert.That(OfferLabel(cards[i], EpisodeHud.OfferChipLineName), Is.EqualTo(RoleOffers.PitchLine), who);
                Assert.That(OfferLabel(cards[i], EpisodeHud.OfferToneWordName), Is.EqualTo(words.tone), who);
                Assert.That(cards[i].GetComponentsInChildren<TMP_Text>().Single(label => label.transform.parent.name == EpisodeHud.OfferToneMarkName).text,
                    Is.EqualTo(RoleOffers.ToneMark(words.tone)), who + "'s tone has its own mark beside its word.");
                Assert.That(OfferLabel(cards[i], EpisodeHud.OfferSituationName), Is.EqualTo(words.situation), who);
                Assert.That(OfferLabel(cards[i], EpisodeHud.OfferReadName), Is.EqualTo(words.read), who);
                Assert.That(OfferLabel(cards[i], EpisodeHud.OfferQuoteName), Is.EqualTo("“" + words.quote + "”"), who);
                Assert.That(OfferLabel(cards[i], EpisodeHud.OfferRecommendsName), Is.Not.Null.And.EqualTo("Recommends: " + state.Find(pitches[i].aboutId).name), who);
                Assert.That(OfferLabel(cards[i], EpisodeHud.OfferTermsName), Is.Null, who + ": a pitch has no terms.");
                Assert.That(OfferLabel(cards[i], EpisodeHud.OfferStakesName), Is.EqualTo(words.stakes), who);
                Assert.That(OfferLabel(cards[i], EpisodeHud.OfferAnswersName), Is.EqualTo(words.answers), who + ": what a no and silence cost.");
                Assert.That(OfferLabel(cards[i], EpisodeHud.OfferHistoryName), Is.EqualTo(words.history), who);
                Assert.That(OfferLabel(cards[i], EpisodeHud.OfferAssessmentName), Is.Null, who + " is not felt out yet.");
                Assert.That(OfferLabel(cards[i], EpisodeHud.OfferLockName), Is.Null, who + "'s promise is open.");
                var photo = cards[i].GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Photo");
                Assert.That(photo.GetComponentsInChildren<CharacterPortraitBinding>(true), Has.Length.EqualTo(1), who + "'s face is bound to the portrait studio.");
                Assert.That(photo.GetComponentsInChildren<TMP_Text>(), Is.Empty, who + "'s face carries no initials, and no role's pill: they hold no role.");
                foreach (var caption in PitchBoardCaptions())
                {
                    var button = OfferButton(cards[i], caption);
                    Assert.That(button.GetComponentsInChildren<TMP_Text>(true).Select(label => label.text), Is.EqualTo(new[] { caption }),
                        who + ": '" + caption + "' says its caption, unchanged, and nothing more.");
                    Assert.That(hud.TryDescribeBoardAnswer(button, out string cardId, out string key), Is.True, who + ": '" + caption + "' is the card's own answer.");
                    Assert.That(cardId, Is.EqualTo(pitches[i].id));
                    Assert.That(EpisodeHud.ReplyCaption(ReplyCards.Find(ReplyCards.Pitch, key)?.Label ?? "") == caption
                        || (key == HoHPitches.FeelOutKey && caption == EpisodeHud.FeelOutPitchCaption), Is.True, who + ": '" + caption + "' answers " + key + ".");
                    Assert.That(hud.TryDescribeOfferControl(button, out _), Is.False, who + ": a pitch's answer is no offer's control.");
                    Assert.That(hud.TryReadOfferControl(button, out var reading), Is.True, who + ": '" + caption + "' is read.");
                    Assert.That(reading, Is.EqualTo(RoleOffers.Spoken(caption, words)));
                    Assert.That(button.IsInteractable(), Is.True, who + ": '" + caption + "' can be pressed.");
                }
            }
            Assert.That(tones, Is.EquivalentTo(new[] { RoleTone.Threatening, RoleTone.Earnest, RoleTone.Pragmatic }), "The three shaped tones are on the board.");

            // The last card first: any order. A press commits as it returns, so the season is read then.
            var before = director.Snapshot;
            var hear = OfferButton(cards[2], EpisodeHud.ReplyCaption(ReplyCards.Find(ReplyCards.Pitch, HoHPitches.HearKey).Label));
            ScrollIntoView(hear);
            AssertPressable(hear, hear.name);
            hear.onClick.Invoke();
            var heard = director.Snapshot;
            yield return null; yield return null;
            AssertThePitchAnswer(before, heard, pitches[2].id, HoHPitches.HearKey, "Hearing the last pitch first");
            board = LastActive(EpisodeHud.OfferBoardName);
            Assert.That(OfferCards(board).Select(card => card.name), Is.EqualTo(pitches.Take(2).Select(pitch => EpisodeHud.OfferCardPrefix + heard.Find(pitch.fromId).name)),
                "Answered, the card leaves the board; the others stay, in their order.");

            var feel = OfferButton(OfferCards(board)[1], EpisodeHud.FeelOutPitchCaption);
            ScrollIntoView(feel);
            AssertPressable(feel, EpisodeHud.FeelOutPitchCaption);
            feel.onClick.Invoke();
            var felt = director.Snapshot;
            yield return null; yield return null;
            AssertThePitchAnswer(heard, felt, pitches[1].id, HoHPitches.FeelOutKey, "Feeling the second pitcher out");
            board = LastActive(EpisodeHud.OfferBoardName);
            var second = OfferCards(board)[1];
            Assert.That(OfferLabel(second, EpisodeHud.OfferAssessmentName), Is.EqualTo(HoHPitches.Assessment(felt, felt.replyCards.Single(c => c.id == pitches[1].id))),
                "Felt out, the card says what they said,");
            Assert.That(OfferButton(second, EpisodeHud.FeelOutPitchCaption).IsInteractable(), Is.False, "and the question is locked, its caption kept.");
            AssertPitchBoardCaptions(board, "After two answers");
        }

        /// <summary>
        /// At every text size every control on the pitch board says its caption and nothing else, every label draws its copy in
        /// its box and none is set under twelve pixels at 1080p. At the standard and the larger text every card is on the one
        /// page; at the largest they page, a card a page, with "Next offer", "Previous offer" and "1 of 3", and a turn commits
        /// nothing and keeps the keyboard on the pager. The board is photographed at each size for review; the frames are not compared.
        /// </summary>
        [UnityTest]
        public IEnumerator PitchBoard_EveryCaptionAndLabelHoldsAtEveryTextSizeAndTheLargestPages()
        {
            yield return InstallPitchBoard(true);
            var pitches = RoleOffers.PitchBoardCards(director.Snapshot);
            foreach (var size in TextSizes.All)
            {
                if (size != TextSize.Standard)
                {
                    director.ClosePanels();
                    yield return ApplyTextSize(size);
                }
                yield return OpenStation();
                yield return null;
                string where = "The pitch board " + TextScales.At(size);
                var board = LastActive(EpisodeHud.OfferBoardName);
                Assert.That(board, Is.Not.Null, where + " is up.");
                var cards = OfferCards(board);
                bool largest = size == TextSize.Largest;
                if (!largest)
                {
                    Assert.That(cards, Has.Length.EqualTo(pitches.Count), where + " has every card on the one page,");
                    Assert.That(ActiveRect(EpisodeHud.OfferCountName), Is.Null, where + " has no pager.");
                }
                else
                {
                    Assert.That(cards, Has.Length.EqualTo(1), where + " pages, a card a page.");
                    Assert.That(OfferLabel(board, EpisodeHud.OfferCountName), Is.EqualTo("1 of " + pitches.Count), where + " says where the player is.");
                    Assert.That(ButtonWithCaptionOrNull(EpisodeHud.PreviousOfferCaption), Is.Null, "No way back from the first card.");
                }
                AssertPitchBoardCaptions(board, where);
                AssertEveryLabelDraws(board, where);
                AssertNoMeetingLabelOverflows(board, where);
                AssertNoBoardTypeUnderTwelvePixels(board, where);
                if (Application.isBatchMode) yield return CaptureFraming("pitch-board" + TextScales.Suffix(size));
                if (!largest) continue;

                int revision = director.Snapshot.revision;
                ButtonWithCaption(EpisodeHud.NextOfferCaption).onClick.Invoke();
                int turned = director.Snapshot.revision;
                yield return null; yield return null;
                board = LastActive(EpisodeHud.OfferBoardName);
                string next = where + ", turned on";
                Assert.That(OfferCards(board).Select(card => card.name), Is.EqualTo(new[] { EpisodeHud.OfferCardPrefix + director.Snapshot.Find(pitches[1].fromId).name }),
                    next + ", shows the second card.");
                Assert.That(OfferLabel(board, EpisodeHud.OfferCountName), Is.EqualTo("2 of " + pitches.Count), next);
                Assert.That(ButtonWithCaptionOrNull(EpisodeHud.PreviousOfferCaption), Is.Not.Null, next + ": a way back,");
                Assert.That(ButtonWithCaptionOrNull(EpisodeHud.NextOfferCaption), Is.Not.Null, next + ": and on, to the third.");
                Assert.That(EventSystem.current.currentSelectedGameObject != null
                    && EventSystem.current.currentSelectedGameObject.name == EpisodeHud.NextOfferCaption, Is.True, next + ": the keyboard stays on the pager.");
                Assert.That(turned, Is.EqualTo(revision), "Turning a page commits nothing.");
                AssertPitchBoardCaptions(board, next);
                AssertEveryLabelDraws(board, next);
                AssertNoMeetingLabelOverflows(board, next);
                if (Application.isBatchMode) yield return CaptureFraming("pitch-board-largest-page-2");
            }
            director.ClosePanels();
            yield return ApplyTextSize(false);
        }

        /// <summary>
        /// The pitch board fits the stage (R5b), on the frame the game is played on, 16:9, photographed ("pitch-board-fit*.png"):
        /// at the standard and the larger text the three cards, their every word and every answer are on the one page with no
        /// scroll - the column no taller than the scroll that holds it - and at the largest each page holds its one card whole,
        /// and its pager, with no scroll. The batch canvas's 4:3 frame holds it too at the standard and the largest text, so a run
        /// without the photographs still pins the fit. At every size, on both frames, each card's four answers stand two to a row
        /// in their order - "Promise safety" beside "Hear them without committing" over "Turn them down" beside "Feel them out
        /// (free)" - inside the card, every caption on two lines at most, and every card's captions at the one size; at the
        /// standard text the stakes and what a no and silence cost each take one line. Then a pitcher is felt out, and their card
        /// carries their answer and the question locked ("pitch-board-felt*.png"): the same shape and the same stage at every
        /// size. Every problem is gathered and said at once, with the stage's numbers.
        /// </summary>
        [UnityTest]
        public IEnumerator PitchBoard_FitsTheStageWithItsAnswersTwoByTwo()
        {
            yield return InstallPitchBoard(true);
            var problems = new List<string>();
            yield return PitchBoardFrames("The pitch board", "pitch-board-fit", null, problems);

            yield return OpenStation();
            LandThePanel();
            yield return null;
            var pitches = RoleOffers.PitchBoardCards(director.Snapshot);
            var feel = OfferButton(OfferCards(LastActive(EpisodeHud.OfferBoardName))[1], EpisodeHud.FeelOutPitchCaption);
            ScrollIntoView(feel);
            AssertPressable(feel, EpisodeHud.FeelOutPitchCaption);
            feel.onClick.Invoke();
            yield return null; yield return null;
            string felt = EpisodeHud.OfferCardPrefix + director.Snapshot.Find(pitches[1].fromId).name;
            Assert.That(OfferLabel(OfferCards(LastActive(EpisodeHud.OfferBoardName)).Single(card => card.name == felt), EpisodeHud.OfferAssessmentName),
                Is.Not.Null, "Felt out, the card says what they said.");
            yield return PitchBoardFrames("With a pitcher felt out, the pitch board", "pitch-board-felt", felt, problems);
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        /// <summary>
        /// With the player's promise record full, every card says so in its lock line over a locked "Promise safety" (R5b): the same
        /// shape and the same stage at every size ("pitch-board-full-record*.png"), the locked caption at its neighbours' size.
        /// </summary>
        [UnityTest]
        public IEnumerator PitchBoard_AFullPromiseRecordStillFitsTheStage()
        {
            yield return InstallPitchBoard(true, fullRecord: true);
            var installed = director.Snapshot;
            Assert.That(installed.replyCards.Where(card => HoHPitches.CanPromiseSafety(installed, card.fromId)), Is.Empty, "The record is full for every pitcher.");
            var problems = new List<string>();
            yield return PitchBoardFrames("With a full promise record, the pitch board", "pitch-board-full-record", null, problems);
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        /// <summary>
        /// Whether the screen is a frame the board is laid out for, 4:3 to 16:9 (R5b): the batch canvas is 4:3, and an editor's
        /// game view can be any shape, where the fit says nothing.
        /// </summary>
        private static bool PitchBoardStageFrame()
        {
            float aspect = Screen.width / Mathf.Max(1f, Screen.height);
            return aspect >= 4f / 3f - .01f && aspect <= 16f / 9f + .01f;
        }

        /// <summary>
        /// The board at every size, on the 4:3 frame and photographed on the 16:9 one as <paramref name="frame"/>, its problems added
        /// to <paramref name="problems"/>: every page at the largest text, or only the page of the card named <paramref name="only"/>.
        /// The 16:9 frame holds it without a scroll at every size; the 4:3 at the standard and the largest text, its larger text
        /// standing two cards to a row and scrolling, as it always has. Back at the standard text after.
        /// </summary>
        private IEnumerator PitchBoardFrames(string what, string frame, string only, List<string> problems)
        {
            foreach (var size in TextSizes.All)
            {
                director.ClosePanels();
                yield return ApplyTextSize(size);
                yield return OpenStation();
                LandThePanel();
                yield return null;
                bool largest = size == TextSize.Largest;
                int pages = largest ? RoleOffers.PitchBoardCards(director.Snapshot).Count : 1;
                // From the first page, whichever the board kept.
                for (int back = 0; largest && back < pages && ButtonWithCaptionOrNull(EpisodeHud.PreviousOfferCaption) != null; back++)
                {
                    ButtonWithCaption(EpisodeHud.PreviousOfferCaption).onClick.Invoke();
                    yield return null; yield return null;
                }
                for (int page = 0; page < pages; page++)
                {
                    if (page > 0)
                    {
                        ButtonWithCaption(EpisodeHud.NextOfferCaption).onClick.Invoke();
                        yield return null; yield return null;
                    }
                    if (largest && only != null && !OfferCards(LastActive(EpisodeHud.OfferBoardName)).Any(card => card.name == only)) continue;
                    string where = what + " " + TextScales.At(size) + (largest ? ", page " + (page + 1) : "");
                    PitchBoardShapeProblems(where + " on the 4:3 frame", size, problems);
                    if (size != TextSize.Larger && PitchBoardStageFrame()) PitchBoardFitProblems(where + " on the 4:3 frame", problems);
                    if (!Application.isBatchMode) continue;
                    string framed = where + " on the 16:9 frame";
                    yield return CaptureFraming(frame + TextScales.Suffix(size) + (largest ? "-page-" + (page + 1) : ""), inspect: _ =>
                    {
                        PitchBoardShapeProblems(framed, size, problems);
                        PitchBoardFitProblems(framed, problems);
                    });
                }
            }
            director.ClosePanels();
            yield return ApplyTextSize(TextSize.Standard);
        }

        /// <summary>
        /// What is wrong with the board's shape on this frame, added to <paramref name="problems"/>: every card's answers two by two
        /// (<see cref="TwoByTwoProblems"/>), every card's captions at one size, and at the standard text its stakes and what a no
        /// and silence cost on one line each. The board's numbers go to the run's record first.
        /// </summary>
        private void PitchBoardShapeProblems(string where, TextSize size, List<string> problems)
        {
            var board = LastActive(EpisodeHud.OfferBoardName);
            if (board == null) { problems.Add(where + ": no board is up."); return; }
            TestContext.WriteLine("[R5b] " + where + ":\n" + PitchBoardNumbers(board));
            foreach (var card in OfferCards(board))
            {
                TwoByTwoProblems(card, where, problems);
                if (size != TextSize.Standard) continue;
                foreach (string note in new[] { EpisodeHud.OfferStakesName, EpisodeHud.OfferAnswersName })
                {
                    var label = card.GetComponentsInChildren<TMP_Text>().SingleOrDefault(text => text.name == note);
                    if (label == null) { problems.Add(where + ": " + card.name + " has no '" + note + "'."); continue; }
                    label.ForceMeshUpdate(true);
                    if (label.textInfo.lineCount != 1) problems.Add(where + ": " + card.name + "'s '" + label.text + "' takes " + label.textInfo.lineCount + " lines, not one.");
                }
            }
            // Every card's captions at the one size where no lock is drawn: a locked answer keeps no room for a lock the icon set
            // does not have, so a felt-out card, or a full record's every card, stands at its neighbours' size.
            if (board.GetComponentsInChildren<RectTransform>(true).Any(rect => rect.name == EpisodeHud.LockMarkName)) return;
            var captions = PitchBoardCaptions();
            var sizes = OfferCards(board).SelectMany(card => card.GetComponentsInChildren<Button>(true))
                .Where(button => captions.Contains(button.name)).Select(button => button.GetComponentInChildren<TMP_Text>())
                .Where(label => label != null).Select(label => label.fontSizeMax).Distinct().ToList();
            if (sizes.Count > 1) problems.Add(where + ": the answers' captions stand at " + string.Join(", ", sizes) + ", not one size.");
        }

        /// <summary>
        /// What keeps a card's four answers from standing two to a row in their order, added to <paramref name="problems"/>: side
        /// by side and level, the second row under the first in the same columns, each inside the card and on two lines at most.
        /// </summary>
        private static void TwoByTwoProblems(RectTransform card, string where, List<string> problems)
        {
            var captions = PitchBoardCaptions();
            var buttons = captions.Select(caption => card.GetComponentsInChildren<Button>(true).SingleOrDefault(button => button.name == caption)).ToArray();
            string who = where + ": " + card.name;
            if (buttons.Any(button => button == null)) { problems.Add(who + " lacks one of " + string.Join(", ", captions) + "."); return; }
            // In the card's own units (LocalRect, EpisodePlayModeTests.ConversationGroups.cs): a frame's numbers whatever its camera.
            var rects = buttons.Select(button => LocalRect(card, (RectTransform)button.transform)).ToArray();
            for (int row = 0; row < 2; row++)
            {
                Rect first = rects[2 * row], second = rects[2 * row + 1];
                if (first.xMax > second.xMin + .5f) problems.Add(who + ": '" + captions[2 * row] + "' does not stand left of '" + captions[2 * row + 1] + "': " + first + " / " + second);
                if (Mathf.Abs(first.yMax - second.yMax) > .5f || Mathf.Abs(first.yMin - second.yMin) > .5f)
                    problems.Add(who + ": '" + captions[2 * row] + "' and '" + captions[2 * row + 1] + "' are not level: " + first + " / " + second);
            }
            if (rects[2].yMax > rects[0].yMin + .5f) problems.Add(who + ": the second row does not stand under the first.");
            if (Mathf.Abs(rects[2].xMin - rects[0].xMin) > .5f || Mathf.Abs(rects[3].xMin - rects[1].xMin) > .5f) problems.Add(who + ": the rows do not share their columns.");
            var inside = card.rect;
            for (int i = 0; i < captions.Length; i++)
            {
                if (rects[i].xMin < inside.xMin - .5f || rects[i].xMax > inside.xMax + .5f || rects[i].yMin < inside.yMin - .5f || rects[i].yMax > inside.yMax + .5f)
                    problems.Add(who + ": '" + captions[i] + "' stands outside the card: " + rects[i] + " in " + inside);
                var label = buttons[i].GetComponentInChildren<TMP_Text>();
                label.ForceMeshUpdate(true);
                if (label.textInfo.lineCount < 1 || label.textInfo.lineCount > 2) problems.Add(who + ": '" + captions[i] + "' takes " + label.textInfo.lineCount + " lines.");
            }
        }

        /// <summary>
        /// What keeps the stage from holding the board without a scroll, added to <paramref name="problems"/>: the column taller
        /// than its scroll, or a card or the pager not wholly inside the scroll's window.
        /// </summary>
        private void PitchBoardFitProblems(string where, List<string> problems)
        {
            Canvas.ForceUpdateCanvases();
            var content = ActiveRect("Episode content");
            var viewport = (RectTransform)content.parent;
            if (content.rect.height > viewport.rect.height + .5f)
                problems.Add(where + " scrolls: the column is " + content.rect.height.ToString("0.#") + " in " + viewport.rect.height.ToString("0.#") + ".");
            var board = LastActive(EpisodeHud.OfferBoardName);
            if (board == null) return;
            var window = viewport.rect;
            foreach (var part in OfferCards(board).Concat(board.GetComponentsInChildren<RectTransform>().Where(rect => rect.name == EpisodeHud.OfferPagerName)))
            {
                var at = LocalRect(viewport, part);
                if (at.yMin < window.yMin - .5f || at.yMax > window.yMax + .5f)
                    problems.Add(where + ": " + part.name + " is not wholly in the window: " + at + " in " + window + ".");
            }
        }

        /// <summary>The stage's numbers, for the record of a run: the column's height in the scroll's, the board's, and each card's with its parts', from its top.</summary>
        private string PitchBoardNumbers(RectTransform board)
        {
            Canvas.ForceUpdateCanvases();
            var content = ActiveRect("Episode content");
            var viewport = (RectTransform)content.parent;
            var parts = new List<string> { "column " + content.rect.height.ToString("0.#") + " in " + viewport.rect.height.ToString("0.#") + ", board " + board.rect.height.ToString("0.#") };
            float boardTop = board.rect.yMax;
            foreach (Transform child in board)
            {
                if (!(child is RectTransform part) || !part.gameObject.activeInHierarchy) continue;
                var at = LocalRect(board, part);
                parts.Add(part.name + " @" + (boardTop - at.yMax).ToString("0.#") + " h" + at.height.ToString("0.#") + " w" + at.width.ToString("0.#"));
                if (!part.name.StartsWith(EpisodeHud.OfferCardPrefix, System.StringComparison.Ordinal)) continue;
                foreach (Transform inner in part)
                {
                    if (!(inner is RectTransform piece) || !piece.gameObject.activeInHierarchy) continue;
                    var place = LocalRect(part, piece);
                    // An explicit null test, not ??: a label is a UnityEngine.Object.
                    var label = piece.GetComponent<TMP_Text>();
                    if (label == null) label = piece.GetComponentInChildren<TMP_Text>();
                    if (label != null) label.ForceMeshUpdate(true);
                    parts.Add("  " + piece.name + " @" + (part.rect.yMax - place.yMax).ToString("0.#") + " h" + place.height.ToString("0.#") + " x" + (place.xMin - part.rect.xMin).ToString("0.#")
                        + " w" + place.width.ToString("0.#") + (label != null ? " f" + label.fontSize.ToString("0.#") + " lines " + label.textInfo.lineCount : ""));
                }
            }
            return string.Join("\n", parts);
        }

        /// <summary>Nothing on the pitch board travels or fades: under "Reduce character motion" the panel cuts in and the board stands still; with motion on, the cards still cut in.</summary>
        [UnityTest]
        public IEnumerator PitchBoard_TheCardsCutInAndNothingOnTheBoardMoves()
        {
            yield return InstallPitchBoard(true);
            director.OpenSettings();
            yield return null;
            ButtonWithCaption("Reduce character motion").onClick.Invoke();
            yield return null;
            director.ClosePanels();
            yield return null;
            Assert.That(Hud.ReducedMotion, Is.True, "The setting reaches the HUD.");
            yield return OpenStation();
            var panel = LastActive("Episode panel");
            Assert.That(panel.GetComponent<HudReveal>(), Is.Null, "Under reduced motion the panel cuts in,");
            var board = LastActive(EpisodeHud.OfferBoardName);
            AssertTheBoardIsStill(board, "Under reduced motion");
            var placed = OfferCards(board).ToDictionary(card => card.name, card => (card.anchoredPosition, card.sizeDelta, card.localScale));
            float until = Time.realtimeSinceStartup + .3f;
            while (Time.realtimeSinceStartup < until) yield return null;
            Canvas.ForceUpdateCanvases();
            foreach (var card in OfferCards(LastActive(EpisodeHud.OfferBoardName)))
                Assert.That((card.anchoredPosition, card.sizeDelta, card.localScale), Is.EqualTo(placed[card.name]),
                    card.name + " stands where it was drawn, a third of a second on.");

            director.OpenSettings();
            yield return null;
            ButtonWithCaption("Enable character motion").onClick.Invoke();
            yield return null;
            director.ClosePanels();
            yield return null;
            Assert.That(Hud.ReducedMotion, Is.False);
            yield return OpenStation();
            AssertTheBoardIsStill(LastActive(EpisodeHud.OfferBoardName), "With motion on");
        }

        /// <summary>
        /// Every card carries the same four captions, so a rebuild the house orders on its own account puts the keyboard back on
        /// the answer it was on - the same card's, the same answer - not the first card's of that caption.
        /// </summary>
        [UnityTest]
        public IEnumerator PitchBoard_ARebuildKeepsTheKeyboardOnTheCardItWasOn()
        {
            yield return InstallPitchBoard(true);
            yield return OpenStation();
            LandThePanel();
            yield return null;
            var pitches = RoleOffers.PitchBoardCards(director.Snapshot);
            foreach (var key in new[] { HoHPitches.TurnDownKey, HoHPitches.FeelOutKey })
            {
                string caption = key == HoHPitches.FeelOutKey ? EpisodeHud.FeelOutPitchCaption : EpisodeHud.ReplyCaption(ReplyCards.Find(ReplyCards.Pitch, key).Label);
                var held = OfferButton(OfferCards(LastActive(EpisodeHud.OfferBoardName))[1], caption);
                EventSystem.current.SetSelectedGameObject(held.gameObject);
                Assert.That(Hud.TryDescribeBoardAnswer(held, out string was, out string wasKey), Is.True);
                Assert.That((was, wasKey), Is.EqualTo((pitches[1].id, key)), "The keyboard is on the second card's '" + caption + "'.");
                RenderHudForTheCurrentCanvas();
                Assert.That(held.gameObject.activeInHierarchy, Is.False, "The rebuild threw that control away,");
                yield return null;
                var now = EventSystem.current.currentSelectedGameObject;
                Assert.That(now != null && now.activeInHierarchy && now.name == caption, Is.True,
                    "and the keyboard is on a live '" + caption + "' again: " + (now != null ? now.name : "nothing") + ",");
                Assert.That(Hud.TryDescribeBoardAnswer(now.GetComponent<Button>(), out string card, out string answer), Is.True, "the card's own answer,");
                Assert.That((card, answer), Is.EqualTo((pitches[1].id, key)), "the second card's, as it was, not the first card's of that caption.");
            }
        }

        /// <summary>The Diary Room's nomination has the same board under the role moments; an answer saves as it is pressed, and the chair keeps the player.</summary>
        [UnityTest]
        public IEnumerator PitchBoard_TheDiaryRoomHasTheSameBoard()
        {
            yield return InstallPitchBoard(true);
            yield return OpenDiaryFixturePanel();
            LandThePanel();
            yield return null;
            var state = director.Snapshot;
            var pitches = RoleOffers.PitchBoardCards(state);
            Assert.That(director.StatusMessage, Is.EqualTo(EpisodeDirector.DiaryPitchInsideMessage), "The room says how its answers save.");
            var board = LastActive(EpisodeHud.OfferBoardName);
            Assert.That(board, Is.Not.Null, "The diary's nomination has the board.");
            Assert.That(OfferCards(board).Select(card => card.name), Is.EqualTo(pitches.Select(pitch => EpisodeHud.OfferCardPrefix + state.Find(pitch.fromId).name)));
            AssertPitchBoardCaptions(board, "The diary's board");
            AssertEveryLabelDraws(board, "The diary's board");
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.BackToNomineesCaption), Is.Not.Null, "The way back to the nominees stays.");

            var before = director.Snapshot;
            var turn = OfferButton(OfferCards(board)[1], EpisodeHud.ReplyCaption(ReplyCards.Find(ReplyCards.Pitch, HoHPitches.TurnDownKey).Label));
            ScrollIntoView(turn);
            AssertPressable(turn, turn.name);
            turn.onClick.Invoke();
            var turned = director.Snapshot;
            yield return null; yield return null;
            Assert.That(director.IsDiaryOpen, Is.True, "Still in the chair.");
            AssertThePitchAnswer(before, turned, pitches[1].id, HoHPitches.TurnDownKey, "Turning the second pitcher down in the diary");
        }

        /// <summary>A season without the role moments draws the pitch it always drew: one at a time, its answers as tiles, and no board.</summary>
        [UnityTest]
        public IEnumerator PitchBoard_WithoutRolesThePitchesComeOneAtATime()
        {
            yield return InstallPitchBoard(false);
            yield return OpenStation();
            LandThePanel();
            yield return null;
            var state = director.Snapshot;
            Assert.That(EpisodeEngine.RolesOn(state), Is.False);
            Assert.That(ActiveRect(EpisodeHud.OfferBoardName), Is.Null, "No board without the role moments,");
            Assert.That(ActiveRect(EpisodeHud.StoryStepName), Is.Not.Null, "but the pitch as it always was:");
            Assert.That(ShownText(), Does.Contain(ReplyCards.Title(state, ReplyCards.Pending(state))), "the first pitch, and only it.");
            foreach (var caption in PitchBoardCaptions())
                Assert.That(director.GetComponentsInChildren<Button>().Count(button => button.IsActive()
                        && button.GetComponentsInChildren<TMP_Text>(true).Any(label => label.text == caption)), Is.EqualTo(1),
                    "'" + caption + "' once, for the one pitch.");
            Assert.That(ShownText(), Does.Not.Contain(EpisodeHud.PitchBoardEyebrow));
        }
    }
}
