using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        private static string First(EpisodeState state, string id) => FinalistRead.FirstName(state.Find(id)?.name);

        /// <summary>The card's line is the one the bracket speaks, and never a name with the wrong person's verb.</summary>
        private static void AssertTheLine(string[] words, EpisodeState state)
        {
            string line = EpisodeDirector.FinalHoHLine(state);
            Assert.That(line, Is.Not.Null.And.Not.Empty);
            Assert.That(words, Does.Contain(line));
            foreach (var wrong in new[] { "You waits", "You watches", "You wins", "You won Part 1 and waits" })
                Assert.That(line, Does.Not.Contain(wrong));
        }

        /// <summary>
        /// Whether a label shows every character it holds. TMP does not report a line cut short in
        /// its box (no wrap, truncate) as overflowing, so count what it drew against what it holds.
        /// </summary>
        private static bool ShowsAllOf(TMP_Text text)
        {
            text.ForceMeshUpdate();
            var info = text.textInfo;
            int drawn = 0, held = 0;
            for (int i = 0; i < info.characterCount; i++)
            {
                if (char.IsWhiteSpace(info.characterInfo[i].character)) continue;
                held++;
                if (info.characterInfo[i].isVisible) drawn++;
            }
            return drawn == held && !text.isTextOverflowing;
        }

        /// <summary>The takeover's words, and that none of them loses its end.</summary>
        private static string[] TakeoverWords(CeremonyTakeover takeover)
        {
            var texts = takeover.GetComponentsInChildren<TMP_Text>(true).Where(text => text.gameObject.activeInHierarchy).ToArray();
            foreach (var text in texts)
                Assert.That(ShowsAllOf(text), Is.True, "The card loses the end of \"" + text.text + "\".");
            return texts.Select(text => text.text).ToArray();
        }

        /// <summary>
        /// Resolves the part the house is in through its own control - the accessible alternative
        /// for a player in the field, or watching - and returns the standings card it played.
        /// </summary>
        private IEnumerator ResolveFinalPart(System.Action<CompetitionResult> result)
        {
            yield return OpenFinalePanel();
            var state = director.Snapshot;
            Assert.That(EpisodeDirector.IsFinalHoHPart(state.phase), Is.True);
            Assert.That(state.competitionResolved, Is.False, state.phase + " is not resolved yet.");
            string caption = EpisodeEngine.CompetitionPlayers(state).Any(actor => actor.isPlayer)
                ? EpisodeDirector.AccessibleCompetitionCaption(state.competitionRulesVersion) : "Watch eligible housemates compete";
            ButtonWithCaption(caption).onClick.Invoke();
            yield return Frames(2);
            Assert.That(director.Snapshot.competitionResolved, Is.True, state.phase + " resolves through " + caption + ".");
            var card = SceneComponents<CompetitionResult>().Single();
            Assert.That(card.IsPlaying, Is.True, "The part's standings, as ever.");
            result(card);
            card.Cancel();
            foreach (var takeover in SceneComponents<CeremonyTakeover>()) takeover.Cancel();
            yield return Frames(2);
        }

        /// <summary>Presses the way on from a resolved part, and returns the card the commit played.</summary>
        private IEnumerator ContinueFromFinalPart(EpisodePhase expected)
        {
            yield return OpenFinalePanel();
            ButtonWithCaption("Continue to the next ceremony").onClick.Invoke();
            yield return Frames(2);
            Assert.That(director.Snapshot.phase, Is.EqualTo(expected));
        }

        /// <summary>
        /// ENDGAME-PLAN F3: the final Head of Household as an event. Each part opens with a card of
        /// the bracket - who won the part before, who plays now - over a closed panel, so the click
        /// that moves it on cannot press the next briefing's controls. Each part's standings card
        /// heads itself with the game and carries the part in its week line. The final eviction
        /// opens with the crowning: the final Head of Household badged, the line theirs or the
        /// player's, the winner acting it out. Every card's words fit.
        /// </summary>
        [UnityTest]
        public IEnumerator Endgame_TheFinalHoHOpensEachPartWithItsBracketAndCrownsItsWinner()
        {
            HoldTheHouseForTheFixture();
            yield return InstallDiaryFixture(
                state => state.phase == EpisodePhase.FinalHoHPart1 && state.competitionResolved && state.pendingDiary == null,
                "a resolved first part of the final Head of Household");
            yield return PutAwayTheCards();
            director.BuildNpcWorldForDiagnostics();
            var takeover = SceneComponents<CeremonyTakeover>().Single();

            // Into Part 2.
            yield return ContinueFromFinalPart(EpisodePhase.FinalHoHPart2);
            var state = director.Snapshot;
            Assert.That(takeover.PlayingKind, Is.EqualTo(CeremonyTakeover.FinalHoHPartKind), "Part 2 opens with the bracket.");
            var words = TakeoverWords(takeover);
            Assert.That(words, Does.Contain("Final HoH · Part 2"));
            Assert.That(words.Count(text => text == "WON PART 1"), Is.EqualTo(1), "The Part 1 winner, badged.");
            Assert.That(words.Count(text => text == "PART 2"), Is.EqualTo(2), "The two who play Part 2.");
            AssertTheLine(words, state);
            Assert.That(string.Join("\n", words), Does.Contain(state.finalPart1WinnerId == state.playerId
                ? "You won Part 1 and wait in Part 3." : First(state, state.finalPart1WinnerId) + " won Part 1 and waits in Part 3."));
            Assert.That(director.IsPanelOpen, Is.False, "The briefing is not pressed through the card.");
            if (Application.isBatchMode) yield return CaptureFraming("endgame-final-hoh-bracket", settle: false);
            takeover.Cancel();
            yield return Frames(1);

            // Part 2's standings: the game's name heads them, the part is in the week line.
            yield return ResolveFinalPart(card =>
            {
                var texts = card.GetComponentsInChildren<TMP_Text>(true);
                string week = texts.Single(text => text.name == "Week").text;
                Assert.That(week, Does.Contain("FINAL HOH, PART 2 OF 3"), "The part stays with the award.");
                Assert.That(week, Does.EndWith(EpisodeEngine.CompetitionCategory(director.Snapshot).ToUpperInvariant()), "The category still ends the line.");
                string game = EpisodeDirector.CompetitionTitleFor(director.Snapshot).Split(new[] { " · " }, System.StringSplitOptions.None).Last();
                Assert.That(texts.Single(text => text.name == "Award").text, Is.EqualTo(game.ToUpperInvariant()), "The game heads the card again.");
                foreach (var text in texts.Where(text => text.name == "Week" || text.name == "Award"))
                    Assert.That(ShowsAllOf(text), Is.True, text.text);
            });

            // Into Part 3.
            yield return ContinueFromFinalPart(EpisodePhase.FinalHoHPart3);
            state = director.Snapshot;
            Assert.That(takeover.PlayingKind, Is.EqualTo(CeremonyTakeover.FinalHoHPartKind), "Part 3 opens with the bracket.");
            words = TakeoverWords(takeover);
            Assert.That(words, Does.Contain("Final HoH · Part 3"));
            Assert.That(words.Count(text => text == "WON PART 1"), Is.EqualTo(1));
            Assert.That(words.Count(text => text == "WON PART 2"), Is.EqualTo(1));
            Assert.That(words.Count(text => text == "WATCHING"), Is.EqualTo(1), "The third watches.");
            AssertTheLine(words, state);
            Assert.That(string.Join("\n", words), Does.Contain("play for the final Head of Household."));
            Assert.That(director.IsPanelOpen, Is.False);
            takeover.Cancel();
            yield return Frames(1);

            yield return ResolveFinalPart(card => { });

            // Into the final eviction: the crowning.
            yield return ContinueFromFinalPart(EpisodePhase.FinalEviction);
            state = director.Snapshot;
            Assert.That(takeover.PlayingKind, Is.EqualTo(CeremonyTakeover.FinalHoHCrownedKind), "The final eviction opens with the crowning.");
            words = TakeoverWords(takeover);
            Assert.That(words, Does.Contain("Final Head of Household"));
            Assert.That(words.Count(text => text == "FINAL HOH"), Is.EqualTo(1));
            Assert.That(words.Count(text => text == "FINAL 3"), Is.EqualTo(2));
            AssertTheLine(words, state);
            if (state.hohId != state.playerId)
            {
                var body = SceneComponents<HouseNpc>().Single(npc => npc.Id == state.hohId).GetComponent<CharacterPresentation>();
                Assert.That(body.LastReaction, Is.EqualTo(CharacterPresentation.Reaction.Won), "The winner acts it out.");
            }
            // The chip at the final eviction: its longest line either fits or gives up the week.
            foreach (var text in LastActive("Week chip").GetComponentsInChildren<TMP_Text>())
                Assert.That(ShowsAllOf(text), Is.True, "The week chip loses the end of \"" + text.text + "\".");
            if (Application.isBatchMode) yield return CaptureFraming("endgame-final-hoh-crowned", settle: false);
            takeover.Cancel();
            yield return Frames(1);
        }

        /// <summary>
        /// The words the final parts are read by, from cloned states: the award carries its part
        /// with no separator of its own, the game follows it under every rules version, and the
        /// briefing's stakes say what each part is for.
        /// </summary>
        [Test]
        public void Endgame_TheFinalPartsNameTheirPartTheirGameAndTheirStakes()
        {
            var state = director.Snapshot.Clone();
            foreach (var actor in state.Active.Where(actor => !actor.isPlayer).Skip(2).ToList()) actor.status = ContestantStatus.Jury;
            state.finalPart1WinnerId = state.Active.First(actor => !actor.isPlayer).id;
            string first = FinalistRead.FirstName(state.Find(state.finalPart1WinnerId).name);

            state.competitionRulesVersion = 4;
            foreach (var (phase, n) in new[] { (EpisodePhase.FinalHoHPart1, 1), (EpisodePhase.FinalHoHPart2, 2), (EpisodePhase.FinalHoHPart3, 3) })
            {
                state.phase = phase;
                string title = EpisodeDirector.CompetitionTitleFor(state);
                Assert.That(title, Does.StartWith("Final HoH, Part " + n + " of 3 · "), title);
                Assert.That(title.Split(new[] { " · " }, System.StringSplitOptions.None), Has.Length.EqualTo(2), "One separator: award, then game.");
                Assert.That(title, Does.EndWith(CompetitionDefinitions.For(state).Title));
                Assert.That(EpisodeDirector.FinalHoHPartLabel(phase), Is.EqualTo("Part " + n + " of 3"));
            }
            state.competitionRulesVersion = 2;
            state.phase = EpisodePhase.FinalHoHPart2;
            Assert.That(CompetitionDefinitions.For(state), Is.Null, "No authored games before rules 3.");
            Assert.That(EpisodeDirector.CompetitionTitleFor(state), Is.EqualTo("Final HoH, Part 2 of 3 · "
                + CompetitionMiniGames.DisplayName(CompetitionMiniGames.For(EpisodeEngine.CompetitionCategory(state)))), "The game is named all the same.");

            state.phase = EpisodePhase.FinalHoHPart1;
            Assert.That(EpisodeDirector.FinalPartStakes(state), Does.Contain("straight there"));
            state.phase = EpisodePhase.FinalHoHPart2;
            Assert.That(EpisodeDirector.FinalPartStakes(state), Is.EqualTo("At stake: the other seat in Part 3, against " + first + "."));
            state.phase = EpisodePhase.FinalHoHPart3;
            Assert.That(EpisodeDirector.FinalPartStakes(state), Does.Contain("the final Head of Household"));
            // Wherever the player is in the bracket, the lines speak to them.
            var others = state.Active.Where(actor => !actor.isPlayer).ToList();
            others[0].name = "Dr. Will Kirby";
            state.finalPart1WinnerId = state.playerId;
            state.phase = EpisodePhase.FinalHoHPart2;
            Assert.That(EpisodeDirector.FinalPartStakes(state), Is.EqualTo("At stake: who joins you in Part 3."));
            Assert.That(EpisodeDirector.FinalHoHLine(state), Does.StartWith("You won Part 1 and wait in Part 3. "));
            Assert.That(EpisodeDirector.FinalHoHLine(state), Does.Contain("Will").And.Not.Contain("Dr."), "A title is not a first name.");
            state.finalPart1WinnerId = others[0].id;
            Assert.That(EpisodeDirector.FinalHoHLine(state), Is.EqualTo("Will won Part 1 and waits in Part 3. You and "
                + FinalistRead.FirstName(others[1].name) + " play for the other seat."));
            state.phase = EpisodePhase.FinalHoHPart3;
            state.finalPart2WinnerId = state.playerId;
            Assert.That(EpisodeDirector.FinalHoHLine(state), Is.EqualTo("You and Will play for the final Head of Household. "
                + FinalistRead.FirstName(others[1].name) + " watches."));
            state.finalPart2WinnerId = others[1].id;
            Assert.That(EpisodeDirector.FinalHoHLine(state), Does.EndWith(" play for the final Head of Household. You watch."));
            state.phase = EpisodePhase.FinalEviction;
            state.hohId = state.playerId;
            Assert.That(EpisodeDirector.FinalHoHLine(state), Does.StartWith("You won it."));
            state.hohId = others[0].id;
            Assert.That(EpisodeDirector.FinalHoHLine(state), Does.StartWith("Will won it,"));

            state.phase = EpisodePhase.HoH;
            Assert.That(EpisodeDirector.IsFinalHoHPart(state.phase), Is.False);
            Assert.That(EpisodeDirector.FinalHoHLine(state), Is.Null);
            Assert.That(EpisodeDirector.CompetitionTitleFor(state), Does.StartWith("Head of Household"), "An ordinary week's title is unchanged.");
        }
    }
}
