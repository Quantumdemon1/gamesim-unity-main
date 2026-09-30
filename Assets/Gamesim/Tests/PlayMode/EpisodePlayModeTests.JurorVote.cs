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
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// ENDGAME-PLAN F6's player on the jury. With no panel open the frame says they are
        /// watching (the objectives card), and no spectator banner is left without a parent. At the
        /// vote: the two finalists' cases, then the two as ballot cards under the caption the vote
        /// has always had, the privacy line, what matters to the player, and the final speeches to
        /// read again - at both text sizes, the copy fitting. The vote commits from its card.
        /// </summary>
        [UnityTest]
        public IEnumerator Endgame_ThePlayerOnTheJuryVotesOnAScreenOfTheirOwn()
        {
            HoldTheHouseForTheFixture();
            yield return InstallFinaleFixture(false);
            yield return PutAwayTheCards();
            var state = director.Snapshot;
            Assert.That(EpisodeHud.IsJuror(state.Find(state.playerId)), Is.True, "The player is on the jury.");

            // The frame, with no panel open.
            director.ClosePanels();
            yield return Frames(2);
            var line = LastActive(EpisodeHud.SpectatorLineName);
            Assert.That(line, Is.Not.Null, "The objectives card says the player is watching.");
            Assert.That(line.GetComponent<TMP_Text>().text, Is.EqualTo(EpisodeHud.SpectatorCaption));
            Assert.That(Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Count(text => text.name == "Spectator banner" && text.GetComponentInParent<Canvas>(true) == null), Is.Zero,
                "No banner made with no panel to hold it.");

            // Through the questions and the speeches to the vote.
            yield return OpenFinalePanel();
            ButtonWithCaption(EpisodeHud.JurySkipCaption).onClick.Invoke();
            yield return Frames(2);
            Assert.That(director.Snapshot.phase, Is.EqualTo(EpisodePhase.FinalSpeeches));
            ButtonWithCaption(EpisodeHud.SpeechContinueCaption).onClick.Invoke();
            yield return Frames(2);
            state = director.Snapshot;
            Assert.That(state.phase, Is.EqualTo(EpisodePhase.Jury));
            Assert.That(EpisodeDirector.JurorVotes(state), Is.True);
            Assert.That(state.finalSpeeches, Is.Not.Empty, "Both finalists spoke, so there are speeches to read again.");
            // What each speech reads as on screen: its words through the table, or the line for none.
            var bodies = state.finalSpeeches.Select(speech =>
                Localisation.Text(string.IsNullOrEmpty(speech.text) ? "No final speech was given." : speech.text)).ToList();

            foreach (bool larger in new[] { false, true })
            {
                yield return ApplyTextSize(larger);
                yield return OpenFinalePanel();
                yield return Frames(2);
                Canvas.ForceUpdateCanvases();
                var panel = LastActive("Episode panel");
                string words = Words(panel);
                Assert.That(words, Does.Contain("THE JURY VOTES").And.Contain(EpisodeDirector.JuryPrivacyLine).And.Contain("YOUR VOTE"));
                var quotes = new List<string>();
                foreach (var finalist in state.Active)
                {
                    var card = LastActive(EpisodeHud.FinalistCardPrefix + finalist.name);
                    Assert.That(card, Is.Not.Null, finalist.name + "'s case.");
                    Assert.That(Words(card), Does.Contain("WHAT THEY DID TO YOU").And.Contain("COMPETITION RECORD")
                        .And.Not.Contain("KNOWN JURY SUPPORT"), "The juror's case: not the other jurors' leans.");
                    Assert.That(card.GetComponentsInChildren<Button>(), Is.Empty, "A case is read, not pressed.");
                    AssertDecisionCopyFits(card);
                    var vote = ButtonWithCaption("Vote for " + finalist.name + " to win");
                    Assert.That(vote.transform.IsChildOf(LastActive(EpisodeHud.BallotRowName)), Is.True, "The vote is a ballot card.");

                    // MOCKUP-PASS M8 (mockup 50): the finale's card, its name in capitals, one photo,
                    // the three public numbers, the player's standing and agreement on one line, the
                    // crown on the final Head of Household alone, and a quote from the final speech.
                    string caseWords = Words(card);
                    Assert.That(card.Find("Finale frame"), Is.Not.Null, "The finale's card behind the case.");
                    Assert.That(caseWords, Does.Contain(finalist.name.ToUpperInvariant()));
                    Assert.That(card.GetComponentsInChildren<CharacterPortraitBinding>(true), Has.Length.EqualTo(1), "A photo bound to them.");
                    var read = FinalistRead.JurorCase(state, finalist.id);
                    Assert.That(card.GetComponentsInChildren<TMP_Text>().Where(text => text.name == "Stat value").Select(text => text.text),
                        Is.EqualTo(new[] { read.wins.ToString(), read.votesSurvived.ToString(), finalist.timesNominated.ToString() }),
                        "Comp wins, votes survived and times on the block, from the record.");
                    Assert.That(caseWords, Does.Contain("YOU AND " + FinalistRead.FirstName(finalist.name).ToUpperInvariant()));
                    Assert.That(caseWords.Contains("FINAL HEAD OF HOUSEHOLD"), Is.EqualTo(finalist.id == FinalistRead.FinalHeadOfHousehold(state)),
                        "The crown is on the final Head of Household's case, and on nobody else's.");
                    var quote = card.GetComponentsInChildren<TMP_Text>().SingleOrDefault(text => text.name == "Quote");
                    Assert.That(quote, Is.Not.Null, finalist.name + " spoke, so their case quotes them.");
                    string speech = state.finalSpeeches.Single(item => item.speakerId == finalist.id).text;
                    // Its words, less the marks around them and any ellipsis where a long one was cut.
                    string sentence = quote.text.Trim('“', '”').TrimEnd('…');
                    bool fromSpeech = card.GetComponentsInChildren<TMP_Text>().Any(text => text.name == "Quote source" && text.text == EpisodeDirector.FinalSpeechSource);
                    // The first case always quotes the speech; a later one gives way to the line they
                    // introduced themselves with only when its speech would repeat the case beside it.
                    if (quotes.Count == 0) Assert.That(fromSpeech, Is.True, "The first case quotes the final speech.");
                    if (fromSpeech) Assert.That(speech, Does.Contain(sentence), "The quote is their own words from the final speech.");
                    else Assert.That(sentence, Is.EqualTo(read.line), "Otherwise their own introduction, never invented words.");
                    quotes.Add(sentence);
                }
                Assert.That(quotes.Distinct().Count(), Is.EqualTo(quotes.Count), "No case repeats the quote beside it.");
                if (!larger)
                {
                    var versus = LastActive(EpisodeHud.VersusName);
                    Assert.That(versus, Is.Not.Null, "The two cases are set against each other.");
                    Assert.That(Words(versus), Does.Not.Contain("JOURNEYS"), "No tagline the owner has not approved (decision 10).");
                }
                AssertEveryLabelDraws(LastActive(EpisodeHud.FinalistColumnsName), "The juror's cases (" + (larger ? "larger" : "resting") + " text)");
                Assert.That(LastActive(EpisodeHud.JurorMattersName), Is.Not.Null, "What matters to you.");
                Assert.That(words, Does.Not.Contain("Trust "), "No trust number on the vote.");

                foreach (string body in bodies)
                    Assert.That(words, Does.Not.Contain(body), "The speeches wait behind their disclosure.");
                var review = ButtonWithCaption(EpisodeDirector.ReviewSpeechesCaption);
                review.onClick.Invoke();
                yield return Frames(2);
                string opened = Words(LastActive("Episode panel"));
                foreach (var speech in state.finalSpeeches)
                    Assert.That(opened, Does.Contain(state.Find(speech.speakerId).name), "Who spoke.");
                foreach (string body in bodies)
                    Assert.That(opened, Does.Contain(body), "The speeches, read again.");
                ButtonWithCaption(EpisodeDirector.ReviewSpeechesCaption).onClick.Invoke();
                yield return Frames(2);
                string closed = Words(LastActive("Episode panel"));
                foreach (string body in bodies)
                    Assert.That(closed, Does.Not.Contain(body), "and put away again.");
                ButtonWithCaption(EpisodeDirector.ReviewSpeechesCaption).onClick.Invoke();
                yield return Frames(1);
                if (!larger && Application.isBatchMode) yield return CaptureFraming("endgame-juror-vote", settle: false);
                director.ClosePanels();
                yield return null;
            }
            yield return ApplyTextSize(false);

            yield return OpenFinalePanel();
            var first = state.Active.First();
            ButtonWithCaption("Vote for " + first.name + " to win").onClick.Invoke();
            yield return Frames(2);
            Assert.That(director.Snapshot.votes.Any(vote => vote.voterId == state.playerId && vote.targetId == first.id), Is.True,
                "The vote commits from its card.");
        }
    }
}
