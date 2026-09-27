using System.Collections;
using System.Linq;
using System.Reflection;
using Gamesim.Episode;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Competition rules 4 through the house: a new season's briefing offers the luck and word
    /// games on its own terms, each plays to a committed result through its real controls, the word
    /// game's letters stay out of the house's shortcuts, and a throw commits a throw the standings
    /// name - with the line that says how it went.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>The first seed whose season opens with a Head of Household competition of this kind.</summary>
        private static uint SeedOpeningWith(string kind)
        {
            for (uint seed = 1; seed < 500; seed++)
                if (CompetitionRules.Category(EpisodePhase.HoH, 1, seed) == kind) return seed;
            throw new System.InvalidOperationException("No season opens with " + kind);
        }

        /// <summary>A rules-4 season from the default cast, saved and loaded, at its first HoH's briefing.</summary>
        private IEnumerator InstallRules4AtFirstHoH(uint seed)
        {
            var initial = ContentCatalog.Create(seed);
            initial.competitionRulesVersion = CompetitionRules.Current;
            Assert.That(EpisodeValidation.TryValidate(initial, out var reason), Is.True, reason);
            new EpisodeSaveStore(director.SavePath).Save(initial);
            yield return ReloadEpisode();
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            ButtonWithCaption("Begin the next competition").onClick.Invoke();
            yield return null;
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            yield return null;
            Assert.That(director.Snapshot.phase, Is.EqualTo(EpisodePhase.HoH));
            Assert.That(director.Snapshot.competitionRulesVersion, Is.EqualTo(CompetitionRules.Current));
        }

        /// <summary>Enters the ranked game from the briefing and plays through its assembly and count.</summary>
        private IEnumerator EnterRanked(CompetitionMiniGames.Kind kind, CompetitionGameScreen[] found)
        {
            ButtonWithCaption(CompetitionMiniGames.EnterCaption(kind)).onClick.Invoke();
            yield return null;
            var screen = SceneComponents<CompetitionGameScreen>().Single();
            if (screen.IsAssembling)
                screen.GetComponentsInChildren<Button>().First(button => button.name == "Continue to competition").onClick.Invoke();
            for (int frame = 0; frame < 120 && !screen.IsPlaying; frame++) { screen.AdvanceReady(.2f); yield return null; }
            Assert.That(screen.IsPlaying, Is.True, "The ranked attempt is under way.");
            found[0] = screen;
        }

        private MiniGameRun ChallengeRun() => (MiniGameRun)typeof(EpisodeDirector)
            .GetField("challengeRun", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(director);

        /// <summary>The words on the card's label of this name, or null when the card draws no such label.</summary>
        private static string Labelled(Component root, string name) =>
            root.GetComponentsInChildren<TMP_Text>().SingleOrDefault(text => text.name == name)?.text;

        [UnityTest]
        public IEnumerator Rules4_TheLuckGameIsOfferedOnItsTermsAndPlaysToACommittedResult()
        {
            yield return InstallRules4AtFirstHoH(SeedOpeningWith("Luck"));
            var before = director.Snapshot;
            Assert.That(ButtonWithCaption(EpisodeDirector.AccessibleCompetitionCaption(4)), Is.Not.Null,
                "Half marks are worth one and a half points now, and the control says so.");
            Assert.That(EpisodeDirector.AccessibleCompetitionCaption(4), Is.EqualTo("Accessible alternative: steady 1.5-point bonus"));
            var facts = ActiveRect(EpisodeHud.BriefingFactsName);
            string factText = string.Join(" | ", facts.GetComponentsInChildren<TMP_Text>().Select(text => text.text));
            Assert.That(factText, Does.Contain("Three rolls").And.Contain("Adds 0–3 points"), factText);
            var throwCard = ButtonWithCaption(EpisodeHud.ThrowCompetitionCaption);
            Assert.That(string.Join(" ", throwCard.GetComponentsInChildren<TMP_Text>().Select(text => text.text)), Does.Contain("about nine times in ten"),
                "The throw says what it does now.");

            var found = new CompetitionGameScreen[1];
            yield return EnterRanked(CompetitionMiniGames.Kind.Dice, found);
            var screen = found[0];
            var run = ChallengeRun();
            Assert.That(run.Kind, Is.EqualTo(CompetitionMiniGames.Kind.Dice));
            screen.GetComponentsInChildren<Button>().Single(button => button.name == "Roll dice").onClick.Invoke();
            Assert.That(run.RollsUsed, Is.EqualTo(1), "The board's Roll rolls the director's attempt.");
            float landing = Time.realtimeSinceStartup + 5f;
            while (run.Rolling && Time.realtimeSinceStartup < landing) yield return null;
            Assert.That(run.HasRoll, Is.True, "The director's clock lands the dice.");
            int total = run.RollTotal;
            screen.GetComponentsInChildren<Button>().Single(button => button.name == "Keep roll").onClick.Invoke();
            Assert.That(run.Finished, Is.True);

            float deadline = Time.realtimeSinceStartup + 5f;
            while (director.Snapshot.revision == before.revision && Time.realtimeSinceStartup < deadline) yield return null;
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(before.revision + 1), "The kept roll commits once.");
            string explanation = after.events.Last(e => e.kind == "competition-performance").text;
            decimal points = System.Math.Round((decimal)(CompetitionMiniGames.DiceScore(total) / 10 * 3), 2, System.MidpointRounding.AwayFromZero);
            Assert.That(explanation, Does.Contain("performance " + (points == 0 ? "0" : "+" + points.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture))),
                "The kept total, out of ten, is worth up to three points: " + explanation);
            yield return null; yield return null;
            var card = SceneComponents<CompetitionResult>().Single();
            Assert.That(Labelled(card, "Player attempt"), Does.Contain("kept " + total + " on roll 1 of 3"), "The standings say the roll that was kept.");
            Assert.That(Labelled(card, "Week"), Does.EndWith("LUCK"), "The card names the kind the season dealt, from the season's own order.");
        }

        [UnityTest]
        public IEnumerator Rules4_ANewSeasonPlaysTheCurrentRules()
        {
            director.StartSeason(new SeasonBuilder.Choice());
            yield return null;
            Assert.That(director.Snapshot.competitionRulesVersion, Is.EqualTo(CompetitionRules.Current), "A season started now plays rules 4.");
            Assert.That(director.Snapshot.haveNotRulesStartWeek, Is.EqualTo(1), "and names Have-Nots from its first week.");
            Assert.That(director.Snapshot.strategyRulesStartWeek, Is.EqualTo(1), "and opens the strategy windows.");
            // Started without a roster choice - the default cast - it plays the same.
            director.StartSeason(null);
            yield return null;
            Assert.That(director.Snapshot.competitionRulesVersion, Is.EqualTo(CompetitionRules.Current));
            Assert.That(director.Snapshot.haveNotRulesStartWeek, Is.EqualTo(1), "The default cast's fixture seasons do not, but a season started from it does.");
            Assert.That(director.Snapshot.strategyRulesStartWeek, Is.EqualTo(1), "Nor do they open the windows; a season started from them does.");
        }

        [UnityTest]
        public IEnumerator Rules4_TheHouseguestScrambleSpellsTheHousesOwnNames()
        {
            uint seed = 1;
            while (CompetitionRules.Category(EpisodePhase.HoH, 1, seed) != "Social"
                || CompetitionDefinitions.Version4(seed, 1, EpisodePhase.HoH) != CompetitionDefinitions.HouseguestScramble) seed++;
            yield return InstallRules4AtFirstHoH(seed);
            var found = new CompetitionGameScreen[1];
            yield return EnterRanked(CompetitionMiniGames.Kind.Words, found);
            var run = ChallengeRun();
            Assert.That(run.Definition, Is.SameAs(CompetitionDefinitions.HouseguestScramble));
            var names = director.Snapshot.contestants.Select(contestant => CompetitionMiniGames.ScrambleForm(contestant.name))
                .Where(name => name.Length >= CompetitionMiniGames.ShortestScrambleWord).ToList();
            var dealt = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < 8; i++) { dealt.Add(run.Word); run.SkipWord(); }
            Assert.That(dealt.Where(word => !CompetitionMiniGames.BigBrotherWords.Contains(word)), Is.SubsetOf(names),
                "The words are this season's first names: " + string.Join(", ", dealt));
            Assert.That(dealt.Intersect(names).Count(), Is.GreaterThanOrEqualTo(System.Math.Min(4, names.Count)), "and the house's names are in it.");
        }

        [UnityTest]
        public IEnumerator Rules4_TheWordGameTypesAndTheHousesKeysWaitWhileItIsPlayed()
        {
            yield return InstallRules4AtFirstHoH(SeedOpeningWith("Social"));
            var found = new CompetitionGameScreen[1];
            yield return EnterRanked(CompetitionMiniGames.Kind.Words, found);
            var run = ChallengeRun();
            Assert.That(run.Kind, Is.EqualTo(CompetitionMiniGames.Kind.Words));
            if (testKeyboard == null) testKeyboard = InputSystem.AddDevice<Keyboard>();
            var journal = typeof(EpisodeDirector).GetField("journalOpen", BindingFlags.Instance | BindingFlags.NonPublic);

            // J is the notebook's key and E the house's Interact: here they are letters.
            foreach (var key in new[] { Key.J, Key.E })
            {
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(key));
                yield return null;
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
                yield return null;
                Assert.That((bool)journal.GetValue(director), Is.False, key + " did not open the notebook under the game.");
            }
            Assert.That(director.IsChallengeActive, Is.True, "The game is still being played.");

            run.ClearTiles();
            string word = run.Word;
            foreach (char letter in word)
            {
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState((Key)((int)Key.A + (letter - 'A'))));
                yield return null;
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
                yield return null;
            }
            Assert.That(run.WordsSolved, Is.EqualTo(1), word + ", typed on the keyboard, is spelled.");
        }

        /// <summary>
        /// A default-cast rules-4 season's opening throw, played out in the engine: the state at the
        /// HoH's briefing, and the state the throw commits.
        /// </summary>
        private static EpisodeState OpeningThrow(uint seed, out EpisodeState atHoH)
        {
            var probe = ContentCatalog.Create(seed); probe.competitionRulesVersion = CompetitionRules.Current;
            var engine = new EpisodeEngine(probe);
            var advanced = engine.Apply(new EpisodeCommand { id = "begin", actorId = probe.playerId, expectedPhase = probe.phase,
                expectedRevision = probe.revision, kind = EpisodeCommandKind.Advance });
            Assert.That(advanced.accepted, Is.True, advanced.reason);
            atHoH = advanced.state;
            var thrown = engine.Apply(new EpisodeCommand { id = "throw", actorId = probe.playerId, expectedPhase = atHoH.phase,
                expectedRevision = atHoH.revision, kind = EpisodeCommandKind.ThrowCompetition });
            Assert.That(thrown.accepted, Is.True, thrown.reason);
            return thrown.state;
        }

        private static uint SeedWhoseOpeningThrow(bool wins, out EpisodeState atHoH)
        {
            for (uint seed = 1; seed < 800; seed++)
            {
                var after = OpeningThrow(seed, out atHoH);
                if ((after.hohId == after.playerId) == wins) return seed;
            }
            throw new System.InvalidOperationException("No opening throw " + (wins ? "wins" : "loses") + ".");
        }

        /// <summary>Presses the briefing's throw, once nothing has drawn from the season's rolls since the engine's play-out.</summary>
        private IEnumerator PressThrow(EpisodeState predictedAtHoH)
        {
            Assert.That(director.Snapshot.randomState, Is.EqualTo(predictedAtHoH.randomState),
                "Nothing has drawn from the season's rolls since the engine played this throw out.");
            ButtonWithCaption(EpisodeHud.ThrowCompetitionCaption).onClick.Invoke();
            yield return null; yield return null;
        }

        [UnityTest]
        public IEnumerator Rules4_AThrowCommitsAThrowAndTheStandingsSaySo()
        {
            uint seed = SeedWhoseOpeningThrow(false, out var atHoH);
            yield return InstallRules4AtFirstHoH(seed);
            var before = director.Snapshot;
            yield return PressThrow(atHoH);
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(before.revision + 1), "The throw commits once.");
            Assert.That(after.hohId, Is.Not.EqualTo(after.playerId), "This throw lost, as nine in ten do.");
            Assert.That(after.events.Count(e => e.kind == EpisodeEngine.ThrowEventKind), Is.EqualTo(1), "and it is a throw, not an entry at the floor.");
            Assert.That(after.events.Last(e => e.kind == "competition-performance").text, Does.StartWith("You threw it. "));
            var card = SceneComponents<CompetitionResult>().Single();
            Assert.That(card.IsPlaying, Is.True);
            var ordered = after.competitionScores.OrderByDescending(entry => entry.score).ToList();
            int place = ordered.FindIndex(entry => entry.contestantId == after.playerId) + 1;
            Assert.That(Labelled(card, "Player attempt"), Is.EqualTo("You threw it  ·  " + place + " of " + ordered.Count),
                "The line where a played attempt says how it went.");
            var names = card.GetComponentsInChildren<TMP_Text>().Where(text => text.name == "Name").Select(text => text.text).ToList();
            Assert.That(names.Count(name => name.Contains(EpisodeDirector.ThrewNote)), Is.EqualTo(1), "One row is marked thrown: " + string.Join(", ", names));
            Assert.That(names.Single(name => name.Contains(EpisodeDirector.ThrewNote)), Does.Contain(after.Find(after.playerId).name), "and it is the player's.");
        }

        [UnityTest]
        public IEnumerator Rules4_AThrowThatWinsAnywaySaysThePlayerGotLucky()
        {
            uint seed = SeedWhoseOpeningThrow(true, out var atHoH);
            yield return InstallRules4AtFirstHoH(seed);
            yield return PressThrow(atHoH);
            var after = director.Snapshot;
            Assert.That(after.hohId, Is.EqualTo(after.playerId), "The throw won anyway.");
            var card = SceneComponents<CompetitionResult>().Single();
            Assert.That(Labelled(card, "Player attempt"), Is.EqualTo("You threw it and won anyway: you got lucky"));
            Assert.That(after.events.Last(e => e.kind == "competition-performance").text, Does.Contain("It lowers only your own score"),
                "Score details say how a throw can still win.");
        }

        [UnityTest]
        public IEnumerator Rules4_AnEarlierSeasonThrowsAsItAlwaysDid()
        {
            // The fixture's own season is a legacy one: its throw is an entry at the floor.
            Assert.That(director.Snapshot.competitionRulesVersion, Is.LessThan(CompetitionRules.Widened));
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            ButtonWithCaption("Begin the next competition").onClick.Invoke();
            yield return null;
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            yield return null;
            Assert.That(ButtonWithCaption("Accessible alternative: steady 1-point bonus"), Is.Not.Null, "Its half marks are still one point.");
            var throwCard = ButtonWithCaption(EpisodeHud.ThrowCompetitionCaption);
            Assert.That(string.Join(" ", throwCard.GetComponentsInChildren<TMP_Text>().Select(text => text.text)), Does.Contain("you may still win"));
            var before = director.Snapshot;
            throwCard.onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision + 1), "The old season's throw commits.");
            Assert.That(director.Snapshot.events.Any(e => e.kind == EpisodeEngine.ThrowEventKind), Is.False,
                "No throw of rules 4's kind: the old season's throw is a Compete at no performance.");
        }
    }
}
