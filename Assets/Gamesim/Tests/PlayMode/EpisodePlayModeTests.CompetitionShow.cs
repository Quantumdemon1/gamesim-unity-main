using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The competition as a show in the yard (competition show, slice 1): an endurance competition
    /// watched, simulated or thrown commits at once, then the yard is changed, the committed field
    /// walks out to grip rigs, lets go in the reverse order of the standings and leaves the winner
    /// cheering at their station; the standings card follows, and the yard is put back with nothing
    /// left behind. A press skips to the card; reduced motion holds a still pose instead; the ranked
    /// game and the other kinds are as they were.
    ///
    /// <para>Every wait is bounded in real seconds: the show runs on the unscaled clock. A batch run
    /// stages no show unless asked (<see cref="EpisodeDirector.CompetitionShowsInBatchRuns"/>), and a
    /// fixture past free time has no house world until it is built.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>The shows asked for in a batch run, and reduced motion set on the director, the rig and the bodies.</summary>
        private void AskForTheShow(bool reduced = false)
        {
            director.CompetitionShowsInBatchRuns = true;
            typeof(EpisodeDirector).GetField("reducedMotion", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(director, reduced);
            cameraRig.SetReducedMotion(reduced);
            foreach (var visual in SceneComponents<CharacterPresentation>()) visual.SetReducedMotion(reduced);
        }

        /// <summary>The house's world built and bound for a fixture installed past free time, the cast settled.</summary>
        private IEnumerator BuildTheWorldForTheShow()
        {
            director.BuildNpcWorldForDiagnostics();
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null);
            yield return WaitForNpcRuntimeBinding();
            yield return SettleCast();
        }

        /// <summary>A rules-4 season at its first Head of Household, of <paramref name="kind"/>, at the briefing, with the show asked for.</summary>
        private IEnumerator InstallShowAtFirstHoH(string kind, bool reduced = false)
        {
            yield return InstallRules4AtFirstHoH(SeedOpeningWith(kind));
            AskForTheShow(reduced);
            yield return BuildTheWorldForTheShow();
            Assert.That(director.Snapshot.phase, Is.EqualTo(EpisodePhase.HoH));
        }

        private static bool CompetitionCardPlaying() => SceneComponents<CompetitionResult>().Any(card => card.IsPlaying);

        private static List<string> CommittedStandings(EpisodeState state)
            => state.competitionScores.OrderByDescending(entry => entry.score).Select(entry => entry.contestantId).ToList();

        /// <summary>
        /// Presses the briefing's control of this caption and follows the show it stages to its card:
        /// committed at once, the card held, the yard changed, the field at its stations, the drops in
        /// the reverse order of the standings, the winner cheering at their station, then the card
        /// and the yard put back with nothing left behind. Skips with a real press when asked.
        /// </summary>
        private IEnumerator CommitAndFollowTheShow(string caption, bool playerInField, bool skip = false)
        {
            var before = director.Snapshot;
            var field = EpisodeEngine.CompetitionPlayers(before).Select(actor => actor.id).ToList();
            Assert.That(field.Contains(before.playerId), Is.EqualTo(playerInField), "The fixture puts the player where the case says.");
            ButtonWithCaption(caption).onClick.Invoke();
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(before.revision + 1), caption + ": the result commits at once, as it always has.");
            Assert.That(director.IsCompetitionShowRunning, Is.True, caption + ": the committed endurance result is staged in the yard.");
            Assert.That(CompetitionCardPlaying(), Is.False, "The standings card waits for the show.");
            // The course is deployed as the show's set, and its pieces in the station field struck (competition yard, slice 2).
            AssertDeployedYard("Endurance", caption + ": while the show is staged");
            var putAway = director.CompetitionYardPutAway.ToArray();
            Assert.That(putAway, Is.Not.Empty, "The set's pieces standing in the field are struck.");
            Assert.That(putAway.All(renderer => renderer != null && !renderer.enabled), Is.True);
            var standings = CommittedStandings(after);
            yield return null;
            Assert.That(director.CeremonySkipShowing, Is.True, "The skip chip says a press moves the show on.");

            int mostRigs = 0;
            float holdSeen = -1f;
            float deadline = Time.realtimeSinceStartup + 60f;
            while (director.IsCompetitionShowRunning && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(CompetitionCardPlaying(), Is.False, "No card over the show.");
                Assert.That(director.Snapshot.revision, Is.EqualTo(after.revision), "The show commits nothing.");
                if (director.CompetitionShowPhase == EpisodeDirector.CompetitionShowStep.Holding)
                {
                    mostRigs = Mathf.Max(mostRigs, SceneComponents<CompetitionApparatus>()
                        .Count(rig => rig.gameObject.activeInHierarchy && rig.Instrument == CompetitionApparatus.Family.GripRig));
                    if (holdSeen < 0f) holdSeen = Time.realtimeSinceStartup;
                    // Skipped once the rigs stand, before anybody has let go (the first drop is 1.5 s in).
                    if (skip && (mostRigs >= 2 || Time.realtimeSinceStartup > holdSeen + 1f)) { yield return PressKey(Key.Enter); break; }
                }
                yield return null;
            }
            Assert.That(director.IsCompetitionShowRunning, Is.False, "The show ends within its bound.");
            var record = director.LastCompetitionShow;
            Assert.That(record, Is.Not.Null);
            Assert.That(record.CardPlayed, Is.True, "The show ends on its card.");
            Assert.That(record.Skipped, Is.EqualTo(skip));
            Assert.That(record.Standings, Is.EqualTo(standings), "The show plays the committed standings.");
            Assert.That(record.Stationed, Is.EquivalentTo(field), "Every competitor of the committed field - and only they - took a station.");
            // A latecomer still walking when the walk out is up is left out of the staging and never stands at
            // their rig, so the count is at least the hold's pair; the winner is never left out.
            Assert.That(mostRigs, Is.GreaterThanOrEqualTo(2), "The field stood at grip rigs in the yard while they held on.");
            Assert.That(mostRigs, Is.LessThanOrEqualTo(field.Count), "No rig for anybody outside the field.");
            Assert.That(record.LeftOut, Is.Not.Null);
            Assert.That(record.LeftOut, Does.Not.Contain(standings[0]), "The winner holds on at their station.");
            Assert.That(record.LeftOut.Count, Is.LessThanOrEqualTo(field.Count - 2), "At least the hold's pair holds on.");
            if (record.LeftOut.Count > 0) Debug.Log("[Gamesim competition show test] left out, still walking: " + string.Join(", ", record.LeftOut));
            if (!skip)
            {
                Assert.That(record.Drops, Is.EqualTo(standings.Skip(1).Reverse().Where(id => !record.LeftOut.Contains(id)).ToList()),
                    "Those holding on let go from the bottom of the standings up; nobody left out lets go.");
                Assert.That(record.Cheered, Is.EqualTo(standings[0]), "The last one on, the winner, cheers.");
                Assert.That(record.CheerOffset, Is.LessThan(.6f), "at their own station.");
            }
            else Assert.That(record.Drops.Count, Is.LessThan(standings.Count - 1), "The press cut the show short.");
            Assert.That(CompetitionCardPlaying(), Is.True, "The standings card follows the show.");
            Assert.That(putAway.All(renderer => renderer == null || renderer.enabled), Is.True, "Every piece struck from the field is switched back on,");
            Assert.That(director.CompetitionYardPutAway, Is.Empty);
            AssertEverydayYard(caption + (skip ? ", skipped," : "") + " once the show is over");
            yield return null;
            Assert.That(SceneComponents<CompetitionApparatus>(), Is.Empty, "No rig is left in the yard.");
            Assert.That(SceneComponents<Transform>().Any(node => node.name == "Competition stage runtime"), Is.False, "No stage is left in the yard.");
            Assert.That(NpcRead<HashSet<Transform>>("competitionRouteOwners"), Is.Empty);
            Assert.That(NpcRead<bool>("competitionArenaStaging"), Is.False);
            Assert.That(NpcRead<HouseMeetingCoordinator>("npcMeetings").HasCompetitionStage, Is.False, "Every houseguest is let go.");
            Assert.That(player.HasActivityOwner, Is.False, "The player's body is let go.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(after.revision));
            Assert.That(director.Snapshot.randomState, Is.EqualTo(after.randomState), "The show draws nothing from the season.");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator CompetitionShow_ASimulatedEnduranceHoHIsStagedFromItsStandingsBeforeItsCard()
        {
            yield return InstallShowAtFirstHoH("Endurance");
            yield return CommitAndFollowTheShow(EpisodeHud.SimulateCompetitionCaption, playerInField: true);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator CompetitionShow_AThrownEnduranceHoHIsStagedWithThePlayerAtAStation()
        {
            yield return InstallShowAtFirstHoH("Endurance");
            yield return CommitAndFollowTheShow(EpisodeHud.ThrowCompetitionCaption, playerInField: true);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator CompetitionShow_AWatchedEnduranceVetoIsStagedWhileThePlayerWatches()
        {
            uint seed = 1;
            while (seed < 500 && CompetitionRules.Category(EpisodePhase.Veto, 1, seed) != "Endurance") seed++;
            Assert.That(seed, Is.LessThan(500));
            var initial = FullHouse(seed, 12);
            var npcs = initial.Active.Where(actor => !actor.isPlayer).Select(actor => actor.id).ToList();
            initial.phase = EpisodePhase.Veto; initial.hohId = npcs[0];
            initial.nominees = new List<string> { npcs[1], npcs[2] };
            initial.vetoPlayers = npcs.Take(EpisodeEngine.VetoPlayerCount(initial.Active.Count())).ToList();
            Assert.That(EpisodeValidation.TryValidate(initial, out var reason), Is.True, reason);
            new EpisodeSaveStore(director.SavePath).Save(initial);
            yield return ReloadEpisode();
            AskForTheShow();
            yield return BuildTheWorldForTheShow();
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            yield return null;
            yield return CommitAndFollowTheShow("Watch eligible housemates compete", playerInField: false);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator CompetitionShow_APressSkipsToTheCardAndPutsTheYardBack()
        {
            yield return InstallShowAtFirstHoH("Endurance");
            yield return CommitAndFollowTheShow(EpisodeHud.SimulateCompetitionCaption, playerInField: true, skip: true);
        }

        /// <summary>
        /// The winner's fanfare comes with the card that names the winner, not with the commit that
        /// starts the show: nothing is asked at the commit or while the field holds on, and the
        /// competition's win is asked once, as the standings card comes up saying who won.
        /// </summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator CompetitionShow_TheWinnersFanfarePlaysWithTheCardThatNamesTheWinner()
        {
            yield return InstallShowAtFirstHoH("Endurance");
            var audio = director.GetComponent<HouseAudio>();
            Assert.That(audio, Is.Not.Null);
            var asks = new List<string>();
            var faults = new List<string>();
            System.Action<HouseAudio.Cue> listen = cue =>
            {
                if (cue != HouseAudio.Cue.CompetitionWin) return;
                // Never throw through the director's own sound call.
                try
                {
                    var card = SceneComponents<CompetitionResult>().LastOrDefault(item => item.IsPlaying);
                    string line = card == null ? "no card" : card.GetComponentsInChildren<TMPro.TMP_Text>(true)
                        .Where(label => label.name == "Winner").Select(label => label.text).LastOrDefault() ?? "no winner line";
                    asks.Add((director.IsCompetitionShowRunning ? "show running" : "show over") + " | " + line);
                }
                catch (System.Exception error) { faults.Add(error.GetType().Name + ": " + error.Message); }
            };
            audio.CueAsked += listen;
            try
            {
                yield return CommitAndFollowTheShow(EpisodeHud.SimulateCompetitionCaption, playerInField: true);
                var after = director.Snapshot;
                var champion = after.Find(CommittedStandings(after)[0]);
                Assert.That(champion, Is.Not.Null);
                string named = champion.isPlayer ? "You win!" : champion.name + " wins!";
                yield return null;
                Assert.That(faults, Is.Empty);
                Assert.That(asks, Is.EqualTo(new[] { "show over | " + named }),
                    "One fanfare, asked as the card names the winner: none at the commit that started the show, none while it ran.");
            }
            finally { audio.CueAsked -= listen; }
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator CompetitionShow_ReducedMotionHoldsAStillFieldAndThenTheCard()
        {
            yield return InstallShowAtFirstHoH("Endurance", reduced: true);
            var before = director.Snapshot;
            var field = EpisodeEngine.CompetitionPlayers(before).Select(actor => actor.id).ToList();
            ButtonWithCaption(EpisodeHud.SimulateCompetitionCaption).onClick.Invoke();
            Assert.That(director.IsCompetitionShowRunning, Is.True, "Reduced motion still places the field.");
            AssertDeployedYard("Endurance", "for the still show");
            var putAway = director.CompetitionYardPutAway.ToArray();
            float held = -1f, deadline = Time.realtimeSinceStartup + 60f;
            while (director.IsCompetitionShowRunning && Time.realtimeSinceStartup < deadline)
            {
                if (held < 0f && director.CompetitionShowPhase == EpisodeDirector.CompetitionShowStep.Holding) held = Time.realtimeSinceStartup;
                Assert.That(director.CompetitionShowDrops, Is.Empty, "Nobody lets go under reduced motion.");
                Assert.That(CompetitionCardPlaying(), Is.False);
                yield return null;
            }
            Assert.That(director.IsCompetitionShowRunning, Is.False);
            Assert.That(held, Is.GreaterThan(0f), "The field held its still pose before the card.");
            Assert.That(Time.realtimeSinceStartup - held, Is.GreaterThanOrEqualTo(CompetitionShowPlan.ReducedHoldSeconds - .05f));
            var record = director.LastCompetitionShow;
            Assert.That(record.Reduced, Is.True);
            Assert.That(record.Drops, Is.Empty);
            Assert.That(record.Cheered, Is.Null, "No cheer under reduced motion.");
            Assert.That(record.CardPlayed, Is.True);
            Assert.That(record.Stationed, Is.EquivalentTo(field));
            Assert.That(CompetitionCardPlaying(), Is.True, "The card follows the still hold.");
            Assert.That(putAway.All(renderer => renderer == null || renderer.enabled), Is.True, "The yard is put back.");
            AssertEverydayYard("after the still show");
            yield return null;
            Assert.That(SceneComponents<CompetitionApparatus>(), Is.Empty);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator CompetitionShow_TheRankedGameIsItsOwnShowAndCommitsToItsCardAtOnce()
        {
            yield return EnterInstrumentAttempt("Endurance", ranked: true, beforeEntry: () => director.CompetitionShowsInBatchRuns = true);
            AssertDeployedYard("Endurance", "while the player plays the ranked game");
            var putAway = director.CompetitionYardPutAway.ToArray();
            Assert.That(putAway, Is.Not.Empty, "The yard changes for the player's own game as well.");
            Assert.That(putAway.All(renderer => !renderer.enabled), Is.True);
            var before = director.Snapshot;
            ChallengeRun().Finish();
            float deadline = Time.realtimeSinceStartup + 10f;
            while (director.Snapshot.revision == before.revision && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision + 1), "The ranked attempt commits once.");
            Assert.That(director.IsCompetitionShowRunning, Is.False, "The ranked game stages no second show.");
            yield return null; yield return null;
            Assert.That(CompetitionCardPlaying(), Is.True, "Its card plays at once, as it always has.");
            Assert.That(putAway.All(renderer => renderer == null || renderer.enabled), Is.True, "The yard is put back with the arena.");
            AssertEverydayYard("under the ranked game's card");
            Assert.That(SceneComponents<CompetitionApparatus>(), Is.Empty);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator CompetitionShow_AnotherKindKeepsItsCardAtOnce()
        {
            // Since the staging families (slice 3) every game of the catalogue is staged, by its game: the
            // one competition left unstaged is one the show does not know - here a season from before
            // rules 3, whose skill competition has no authored game at all.
            var initial = ContentCatalog.Create(1);
            initial.competitionRulesVersion = 2;
            Assert.That(EpisodeValidation.TryValidate(initial, out var reason), Is.True, reason);
            new EpisodeSaveStore(director.SavePath).Save(initial);
            yield return ReloadEpisode();
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            ButtonWithCaption("Begin the next competition").onClick.Invoke();
            yield return null;
            AskForTheShow();
            yield return BuildTheWorldForTheShow();
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            yield return null;
            var before = director.Snapshot;
            Assert.That(before.phase, Is.EqualTo(EpisodePhase.HoH));
            Assert.That(EpisodeEngine.CompetitionCategory(before), Is.EqualTo("Skill"));
            Assert.That(CompetitionDefinitions.For(before), Is.Null, "No authored game: a competition the show does not know.");
            ButtonWithCaption(EpisodeHud.SimulateCompetitionCaption).onClick.Invoke();
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision + 1));
            Assert.That(director.IsCompetitionShowRunning, Is.False, "A game the show does not know is not staged.");
            Assert.That(director.CompetitionYardPutAway, Is.Empty, "Nothing in the field is struck.");
            AssertEverydayYard("for a kind that is not staged: the course is never deployed");
            yield return null; yield return null;
            Assert.That(CompetitionCardPlaying(), Is.True, "The card plays at once, as it always has.");
            Assert.That(SceneComponents<CompetitionApparatus>(), Is.Empty);
        }
    }
}
