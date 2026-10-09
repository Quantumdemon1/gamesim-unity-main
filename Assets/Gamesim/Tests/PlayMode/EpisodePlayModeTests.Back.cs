using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The pad's B (PLAN A, A2): one Escape/Back matrix. Every overlay the house puts up - the front
    /// door's screens, the panels, a ceremony card, a competition's board and results, the season
    /// report, the tour and the opening - closes or skips the same way by Escape as by B, nothing
    /// underneath acts, and nothing is committed that Escape would not commit. With nothing open,
    /// B does nothing at all, where Escape and Start are the pause menu.
    ///
    /// <para>Each overlay runs twice from a fresh house - the save root emptied and the scene
    /// reloaded - once pressed with Escape and once with B, and what the house looks like after each
    /// press is compared line for line.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private sealed class BackCase
        {
            public string Name;
            /// <summary>Puts the overlay up, from a fresh house.</summary>
            public Func<IEnumerator> Open;
            /// <summary>How many presses: a ceremony card's first skips the reveal, its second closes the card.</summary>
            public int Presses = 1;
            /// <summary>What to wait for after a press, at most five seconds, when the outcome takes a moment to land.</summary>
            public Func<bool> Settled;
            /// <summary>What the last press must have done, so a dead B and a dead Escape cannot agree on nothing.</summary>
            public Func<bool> Done;
            /// <summary>Whether a press may commit: the opening records the beats it skips, and Escape commits them too.</summary>
            public bool MayCommit;
            /// <summary>Whether the house's own clock is held for the fixture; the opening's people walk on it.</summary>
            public bool Hold = true;
            /// <summary>
            /// Whether the hold is taken again once the overlay is up. Not for the diary: holding the
            /// house disposes its world, the player's diary seat with it, and the diary closes itself
            /// when its seat goes - two frames before the press, which then found nothing open.
            /// </summary>
            public bool HoldAgain = true;
        }

        [UnityTest, Timeout(1500000)]
        public IEnumerator Back_EveryOverlayClosesOrSkipsTheSameWayByKeyAndPad()
        {
            KeyCeremony keys = null;
            CompetitionGameScreen board = null;
            CompetitionResult results = null;
            var cases = new List<BackCase>
            {
                new BackCase { Name = "main menu, with a season behind it", MayCommit = true,
                    Open = () => Step(() => director.OpenMainMenu()),
                    Settled = () => !Menu().IsShowing, Done = () => !Menu().IsShowing },
                // With none - the slot's save unreadable, the house in recovery - the menu has
                // nowhere to close to: Escape and B both leave it up, and nothing under it acts.
                new BackCase { Name = "main menu, with no season to go back to", Open = OpenMainMenuWithNoSeasonForBack,
                    Done = () => Menu().IsShowing && !director.SeasonInProgress },
                new BackCase { Name = "cast select", Open = OpenCastScreen,
                    Done = () => !CastScreen().IsShowing && director.IsPanelOpen },
                new BackCase { Name = "character creator", Open = () => OpenCreator(),
                    Done = () => !Creator().IsShowing && CastScreen().IsShowing },
                new BackCase { Name = "settings", Open = () => Step(() => director.OpenSettings()), Done = () => !director.IsPanelOpen },
                new BackCase { Name = "notebook", Open = () => Step(() => director.OpenJournal()), Done = () => !director.IsPanelOpen },
                new BackCase { Name = "conversation", Open = OpenMayaForBack, Done = () => !director.IsConversationOpen },
                new BackCase { Name = "phase panel", Open = OpenStation, Done = () => !director.IsPhasePanelOpen },
                new BackCase { Name = "diary", HoldAgain = false, Open = OpenDiaryForBack, Done = () => !director.IsDiaryOpen },
                new BackCase { Name = "free-time board", Open = OpenFreeTimeBoardForBack, Done = () => !director.IsPanelOpen },
                new BackCase { Name = "campaign board", Open = OpenCampaignBoardForBack, Done = () => !director.IsPanelOpen },
                // Every ceremony card reads the Ceremony map's Skip, bound to Escape and to B alike;
                // the key ceremony stands for them all: the first press skips, the second closes.
                new BackCase { Name = "key ceremony", Presses = 2, MayCommit = true,
                    Open = () => OpenKeyCeremonyForBack(found => keys = found), Done = () => keys != null && !keys.IsPlaying },
                new BackCase { Name = "competition board", Open = () => OpenPracticeBoardForBack(found => board = found),
                    Done = () => board != null && !board.IsShowing && director.IsPanelOpen },
                // Paused, the board's focus is on Resume and its clock is stopped: the press still
                // leaves a practice, by either device.
                new BackCase { Name = "competition board, paused", Open = () => OpenPracticeBoardForBack(found => board = found, paused: true),
                    Done = () => board != null && !board.IsShowing && director.IsPanelOpen },
                new BackCase { Name = "competition results", MayCommit = true, Open = () => OpenResultsForBack(found => results = found),
                    Done = () => results != null && !results.IsPlaying },
                new BackCase { Name = "season report", Open = OpenSeasonReportForBack,
                    Done = () => !director.IsSeasonReportOpen && director.IsPhasePanelOpen },
                new BackCase { Name = "tour", Open = OpenTourForBack, Done = () => !DirectorTour().IsShowing },
                new BackCase { Name = "opening", Hold = false, MayCommit = true,
                    Open = OpenOpeningForBack, Settled = () => director.Opening.IsMeeting, Done = () => director.Opening.IsMeeting },
            };

            var failures = new List<string>();
            foreach (var item in cases)
            {
                var byKey = new List<string>();
                yield return RunBackCase(item, false, byKey);
                var byPad = new List<string>();
                yield return RunBackCase(item, true, byPad);
                for (int press = 0; press < Math.Max(byKey.Count, byPad.Count); press++)
                {
                    string key = press < byKey.Count ? byKey[press] : "(no press)";
                    string pad = press < byPad.Count ? byPad[press] : "(no press)";
                    TestContext.WriteLine("back matrix: " + item.Name + " press " + (press + 1) + "\n  Escape: " + key + "\n  B:      " + pad);
                    if (key != pad) failures.Add(item.Name + ", press " + (press + 1) + ":\n  Escape: " + key + "\n  B:      " + pad);
                }
            }
            Assert.That(failures, Is.Empty, "Escape and B must do the same thing on every overlay:\n" + string.Join("\n", failures));
        }

        [UnityTest]
        public IEnumerator Back_WithNothingOpenDoesNothing_WhereEscapeIsThePauseMenu()
        {
            HoldTheHouseForTheFixture();
            director.ClosePanels();
            yield return Frames(2);
            int revision = director.Snapshot.revision;
            yield return PressBack();
            yield return Frames(2);
            Assert.That(director.IsPanelOpen, Is.False, "B with nothing open opens nothing: the pause menu is Start's and Escape's.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "and commits nothing.");

            yield return PressBack();
            Assert.That(director.IsPanelOpen, Is.False, "Pressed again, still nothing.");
            yield return PressKey(Key.Escape);
            Assert.That(director.IsPanelOpen, Is.True, "Escape with nothing open is the pause menu.");
            yield return PressBack();
            Assert.That(director.IsPanelOpen, Is.False, "and B closes it, as it closes any panel.");
        }

        /// <summary>
        /// An evicted houseguest walking out reads Escape and B as its skip, as a ceremony card
        /// does, without being one. With nothing else open, the Escape that skips the walk stops
        /// there - it used to go on to open the pause menu over the goodbye, where B, which is
        /// nothing with nothing open, only skipped - and Start while they walk opens nothing either.
        /// </summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator Back_TheEscapeThatSkipsAWalkOutStopsThere()
        {
            director.WalkOutsInBatchRuns = true;
            yield return PlayUntilAHouseguestLeaves();
            string leaving = director.DepartingId;
            SceneComponents<VoteReveal>().Single().Cancel();
            yield return Frames(3);
            Assert.That(director.WalkingOutId, Is.EqualTo(leaving), "The evicted walks out,");
            Assert.That(director.WalkOutIsStaged, Is.False, "on the HUD frame, unstaged,");
            Assert.That(director.IsPanelOpen || director.IsOverview || director.EmoteMenuOpen, Is.False, "with nothing else open.");

            yield return PressOnPad(GamepadButton.Start);
            yield return Frames(2);
            Assert.That(director.WalkingOutId, Is.EqualTo(leaving), "Start is not the walk's skip: they walk on,");
            Assert.That(ButtonWithCaptionOrNull("Save now  [F5]"), Is.Null, "and it opens no pause menu over them.");

            // Past the walk's guard on a press, a third of a second on the real clock.
            float guard = Time.realtimeSinceStartup + .5f;
            while (Time.realtimeSinceStartup < guard) yield return null;
            Assert.That(director.WalkingOutId, Is.EqualTo(leaving), "Still walking.");
            yield return PressKey(Key.Escape);
            yield return Frames(2);
            Assert.That(director.WalkingOutId, Is.Null, "Escape skips the walk out,");
            Assert.That(ButtonWithCaptionOrNull("Save now  [F5]"), Is.Null, "and stops there: the pause menu is not opened under the goodbye.");
        }

        private IEnumerator RunBackCase(BackCase item, bool pad, List<string> outcomes)
        {
            yield return FreshEpisode();
            if (item.Hold) HoldTheHouseForTheFixture();
            yield return item.Open();
            if (item.Hold && item.HoldAgain) HoldTheHouseForTheFixture();
            yield return Frames(2);
            string how = item.Name + (pad ? " by B" : " by Escape");
            for (int press = 0; press < item.Presses; press++)
            {
                int revision = director.Snapshot.revision;
                if (pad) yield return PressBack();
                else yield return PressKey(Key.Escape);
                float until = Time.realtimeSinceStartup + 5f;
                if (item.Settled != null) while (!item.Settled() && Time.realtimeSinceStartup < until) yield return null;
                yield return Frames(2);
                int committed = director.Snapshot.revision - revision;
                outcomes.Add(BackOutcome() + " | committed " + committed);
                if (!item.MayCommit) Assert.That(committed, Is.Zero, how + ": nothing underneath acts.");
            }
            Assert.That(item.Done(), Is.True, how + " did not close or skip it: " + outcomes.LastOrDefault());
        }

        /// <summary>The house as Escape or B left it: every overlay's state, in words.</summary>
        private string BackOutcome()
        {
            string Each<T>(Func<T, string> say) where T : Component => string.Join(",", SceneComponents<T>().Select(say));
            var opening = director.Opening;
            return string.Join(" | ", new[]
            {
                "panel " + director.IsPanelOpen, "phase " + director.IsPhasePanelOpen, "talk " + director.IsConversationOpen,
                "diary " + director.IsDiaryOpen, "free time " + director.IsFreeTimeBoard, "report " + director.IsSeasonReportOpen,
                "challenge " + director.IsChallengeActive, "moves " + director.EmoteMenuOpen, "overview " + director.IsOverview,
                "menu " + (Menu() != null && Menu().IsShowing), "cast " + (CastScreen() != null && CastScreen().IsShowing),
                "creator " + (Creator() != null && Creator().IsShowing), "tour " + DirectorTour().IsShowing,
                "opening " + (opening != null && opening.IsPlaying) + "/" + (opening != null && opening.IsMeeting),
                "board " + Each<CompetitionGameScreen>(screen => screen.IsShowing + ":" + screen.Paused),
                "results " + Each<CompetitionResult>(card => card.IsPlaying.ToString()),
                "keys " + Each<KeyCeremony>(card => card.IsPlaying + ":" + card.ShowingBlock),
                "votes " + Each<VoteReveal>(card => card.IsPlaying.ToString()),
            });
        }

        /// <summary>The pad's B, pressed and let go, on a pad of the test's own.</summary>
        private IEnumerator PressBack()
        {
            if (testGamepad == null) testGamepad = InputSystem.AddDevice<Gamepad>();
            yield return PressPad(testGamepad, GamepadButton.East);
        }

        /// <summary>A house with nothing behind it: the save root emptied, then the scene loaded again.</summary>
        private IEnumerator FreshEpisode()
        {
            if (director != null) director.FreezeForReloadForDiagnostics();
            foreach (var file in Directory.GetFiles(temporaryDirectory, "*", SearchOption.AllDirectories)) File.Delete(file);
            yield return ReloadEpisode();
        }

        private static IEnumerator Step(Action act)
        {
            act();
            yield return null;
        }

        /// <summary>
        /// The front door with no season behind it. The director always holds a season in memory -
        /// it builds the authored one before it looks for a save - so "no season" is what the menu
        /// itself goes by (<see cref="EpisodeDirector.SeasonInProgress"/>): a slot whose save cannot
        /// be read, which leaves the house in recovery until the player recovers it or starts again.
        /// Written to this test's slot and loaded as a launch loads it.
        /// </summary>
        private IEnumerator OpenMainMenuWithNoSeasonForBack()
        {
            director.FreezeForReloadForDiagnostics();
            File.WriteAllText(director.SavePath, "not a season");
            yield return ReloadEpisode();
            Assert.That(director.SeasonInProgress, Is.False, "An unreadable save leaves no season to go back to.");
            director.OpenMainMenu();
            yield return null;
            Assert.That(Menu().IsShowing, Is.True, "The front door is up over the recovery.");
        }

        private IEnumerator OpenMayaForBack()
        {
            var maya = SceneComponents<HouseNpc>().Single(npc => npc.Id == ContentCatalog.MayaId);
            yield return OpenNearbyNpc(maya);
            Assert.That(director.IsConversationOpen, Is.True);
        }

        private IEnumerator OpenDiaryForBack()
        {
            director.GoToDiary();
            float deadline = Time.realtimeSinceStartup + 20f;
            while (!director.CanUseDiary && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.TryOpenDiary(), Is.True, "The player reached the diary room.");
            deadline = Time.realtimeSinceStartup + 20f;
            while (!director.IsDiarySettled && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.IsDiarySettled, Is.True, "Seated in the diary room.");
        }

        private IEnumerator OpenFreeTimeBoardForBack()
        {
            yield return InstallFreeTime(8, FreeTimeWaiting.Nothing);
            yield return OpenStation();
            Assert.That(director.IsFreeTimeBoard, Is.True, "Free time's board is up.");
        }

        private IEnumerator OpenCampaignBoardForBack()
        {
            yield return InstallCampaignWithABeat();
            yield return OpenStation();
            Assert.That(director.Snapshot.phase, Is.EqualTo(EpisodePhase.Campaign));
        }

        private IEnumerator OpenKeyCeremonyForBack(Action<KeyCeremony> found)
        {
            var keys = SceneComponents<KeyCeremony>().Single();
            found(keys);
            yield return PlayUntilTheKeyCeremony(keys);
            // Past the card's read-first delay, still inside the key-by-key stage.
            float readable = Time.realtimeSinceStartup + 1.2f;
            while (Time.realtimeSinceStartup < readable) yield return null;
            Assert.That(keys.IsPlaying && !keys.ShowingBlock, Is.True, "The keys are still coming out.");
        }

        private IEnumerator OpenPracticeBoardForBack(Action<CompetitionGameScreen> found, bool paused = false)
        {
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            ButtonWithCaption("Begin the next competition").onClick.Invoke();
            yield return null;
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            ButtonWithCaption("Practice this competition").onClick.Invoke();
            yield return null;
            var screen = SceneComponents<CompetitionGameScreen>().Single();
            found(screen);
            Assert.That(screen.IsShowing, Is.True);
            if (!paused) yield break;
            // Paused as its own control pauses it, so both runs reach the press by the same way.
            screen.TogglePause();
            yield return null;
            Assert.That(screen.IsShowing && screen.Paused, Is.True, "The board is paused.");
        }

        private IEnumerator OpenResultsForBack(Action<CompetitionResult> found)
        {
            Assert.That(director.Submit(NextCommand(director.Snapshot)).accepted, Is.True);
            Assert.That(director.Submit(NextCommand(director.Snapshot)).accepted, Is.True);
            var card = SceneComponents<CompetitionResult>().Single();
            found(card);
            yield return null;
            Assert.That(card.IsPlaying, Is.True, "The competition's results are up.");
            // The card lets the press that brought it up go by, for a quarter second of the unscaled
            // clock: on a warm run two frames are a few milliseconds, so the matrix's press would land
            // inside that guard and be let go as the card means it to be. Press once it takes one.
            float until = Time.realtimeSinceStartup + 5f;
            while (!card.TakesAPress && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(card.TakesAPress, Is.True, "The results take a press once the one that brought them up has gone by.");
        }

        private IEnumerator OpenSeasonReportForBack()
        {
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            yield return null;
            director.ShowSeasonReport();
            yield return null;
            Assert.That(director.IsSeasonReportOpen, Is.True);
        }

        private IEnumerator OpenTourForBack()
        {
            var tour = DirectorTour();
            tour.Show(TourChrome);
            yield return Frames(2);
            Assert.That(tour.IsShowing, Is.True);
        }

        private IEnumerator OpenOpeningForBack()
        {
            director.PlayOpeningForVerification(holdUntilAdvanced: true);
            yield return Frames(2);
            Assert.That(director.Opening.IsPlaying, Is.True);
        }
    }
}
