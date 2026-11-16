using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The Ring Tower in the house (competition ring tower, slice 4; competition rules 5). A tower competition's
    /// ranked game and practice deploy the yard's three stacks as the playable tower, with separate graduated rings
    /// in the stacks' own neon and the stacks' own joined meshes switched off; the tower plays to the end by the
    /// mouse, the keyboard and the pad; a larger ring on a smaller one is refused with a cue; a ranked tower commits
    /// a result, a practice nothing; and the yard is put away after. Watched, simulated, thrown or taken by the
    /// accessible route, a tower competition commits straight to its standings card, as every game but endurance does.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>A rules-5 season from the default cast whose first HoH is mental - so its game is the tower - saved and loaded, at the briefing.</summary>
        private IEnumerator InstallRingTowerAtFirstHoH()
        {
            uint seed = SeedOpeningWith(CompetitionRules.Mental);
            Assert.That(CompetitionDefinitions.Version5(seed, 1, EpisodePhase.HoH), Is.SameAs(CompetitionDefinitions.RingTower),
                "A season's first mental competition is the tower under rules 5.");
            var initial = ContentCatalog.Create(seed);
            initial.competitionRulesVersion = CompetitionRules.RingTower;
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
            Assert.That(director.Snapshot.competitionRulesVersion, Is.EqualTo(CompetitionRules.RingTower));
            Assert.That(director.CompetitionGameFor(director.Snapshot), Is.EqualTo(CompetitionMiniGames.Kind.Tower));
        }

        /// <summary>Starts the attempt from the briefing's control of this caption and plays through its assembly and count.</summary>
        private IEnumerator StartTowerAttempt(string caption, CompetitionGameScreen[] found)
        {
            ButtonWithCaption(caption).onClick.Invoke();
            yield return null;
            var screen = SceneComponents<CompetitionGameScreen>().Single();
            if (screen.IsAssembling)
                screen.GetComponentsInChildren<Button>().First(button => button.name == "Continue to competition").onClick.Invoke();
            for (int frame = 0; frame < 120 && !screen.IsPlaying; frame++) { screen.AdvanceReady(.2f); yield return null; }
            Assert.That(screen.IsPlaying, Is.True, "The tower is under way.");
            Assert.That(ChallengeRun().Kind, Is.EqualTo(CompetitionMiniGames.Kind.Tower));
            found[0] = screen;
        }

        /// <summary>A stall's pause is taken off, as a player resumes: the attempt's clock is the director's, the frames are the test's.</summary>
        private static void ResumeTower(CompetitionGameScreen screen)
        {
            if (screen.Paused) screen.TogglePause();
        }

        private static Button TowerStack(CompetitionGameScreen screen, int stack) =>
            screen.GetComponentsInChildren<Button>().Single(button => button.name == CompetitionGameScreen.StackCaption(stack));

        /// <summary>The deployed tower: the yard's three stacks standing, their own meshes off, four separate graduated rings in their neon.</summary>
        private void AssertTowerDeployed(string when)
        {
            AssertDeployedYard(CompetitionRules.Mental, when);
            var tower = director.CompetitionRingTowerShown;
            Assert.That(tower, Is.Not.Null, "The playable tower stands " + when + ".");
            Assert.That(tower.Stacks, Has.Count.EqualTo(3), "On the yard's three stacks.");
            var putAway = new HashSet<Renderer>(director.CompetitionYardPutAway);
            foreach (var stack in tower.Stacks)
            {
                Assert.That(stack.gameObject.activeInHierarchy, Is.True, stack.name + " stands " + when + ".");
                var own = stack.GetComponentsInChildren<MeshRenderer>(true);
                Assert.That(own, Is.Not.Empty);
                Assert.That(own.Where(renderer => renderer.enabled), Is.Empty, stack.name + "'s own joined rings never show under the playable ones.");
                Assert.That(own.All(putAway.Contains), Is.True, "The yard's one owner switched them off and will switch them back on.");
                var colliders = stack.GetComponentsInChildren<Collider>(true);
                Assert.That(colliders.Any(collider => collider.gameObject.layer == 8 && collider.enabled), Is.True,
                    stack.name + " keeps its fitted layer-8 Collision proxy: nobody is routed into it.");
            }
            Assert.That(tower.Rings, Has.Count.EqualTo(RingTowerPuzzle.StandardRings), "Four separate rings.");
            for (int size = 1; size <= tower.Rings.Count; size++)
            {
                var ring = tower.Rings[size - 1];
                Assert.That(ring.gameObject.activeInHierarchy && ring.GetComponent<MeshRenderer>().enabled, Is.True, "Ring " + size + " is drawn.");
                Assert.That(ring.GetComponent<MeshRenderer>().sharedMaterial.name.ToLowerInvariant(), Does.Contain("neon"), "In the stacks' own neon.");
                Assert.That(ring.GetComponentsInChildren<Collider>(true), Is.Empty, "Collider-free.");
                if (size > 1)
                    Assert.That(ring.GetComponent<MeshFilter>().sharedMesh.bounds.size.x,
                        Is.GreaterThan(tower.Rings[size - 2].GetComponent<MeshFilter>().sharedMesh.bounds.size.x + .02f), "Graduated.");
            }
            Assert.That(SceneComponents<CompetitionApparatus>(), Is.Empty, "The stacks are the equipment: nobody has a console to pretend at.");
        }

        /// <summary>The yard after a tower: everyday, the stacks' own meshes switched back on under their put-away pieces, no tower left.</summary>
        private void AssertTowerPutAway(string when)
        {
            Assert.That(director.CompetitionRingTowerShown, Is.Null, "No tower stands " + when + ".");
            AssertEverydayYard(when);
            foreach (var stack in YardSetPieces(out _).Where(node => CompetitionYardPieces.Stem(node.name) == CompetitionYardPieces.Stack))
                Assert.That(stack.GetComponentsInChildren<MeshRenderer>(true).All(renderer => renderer.enabled), Is.True,
                    stack.name + "'s own mesh is back on, put away with its stack " + when + ".");
        }

        /// <summary>Plays the fewest moves by pressing the board's stack controls, as a mouse does.</summary>
        private IEnumerator ClickTheTowerHome(CompetitionGameScreen screen, MiniGameRun run)
        {
            foreach (var (from, to) in RingTowerPuzzle.Solution(run.Tower.Rings))
            {
                ResumeTower(screen);
                TowerStack(screen, from).onClick.Invoke();
                TowerStack(screen, to).onClick.Invoke();
                yield return null;
            }
        }

        /// <summary>Walks the keyboard's or the pad's selection to a stack with Right, then presses Enter or A on it.</summary>
        private IEnumerator PickTowerStackByInput(CompetitionGameScreen screen, int stack, Gamepad pad)
        {
            string wanted = CompetitionGameScreen.StackCaption(stack);
            for (int step = 0; step < 6; step++)
            {
                ResumeTower(screen);
                var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                if (selected != null && selected.name == wanted) break;
                if (pad != null) yield return PressPad(pad, GamepadButton.DpadRight); else yield return PressKey(Key.RightArrow);
            }
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo(wanted), "The selection walks to " + wanted + ".");
            ResumeTower(screen);
            if (pad != null) yield return PressPad(pad, GamepadButton.South); else yield return PressKey(Key.Enter);
        }

        private IEnumerator PlayTheTowerByInput(CompetitionGameScreen screen, MiniGameRun run, Gamepad pad)
        {
            foreach (var (from, to) in RingTowerPuzzle.Solution(run.Tower.Rings))
            {
                int moves = run.Tower.Moves;
                yield return PickTowerStackByInput(screen, from, pad);
                Assert.That(run.Tower.Lifted, Is.EqualTo(from), "The press lifted stack " + (from + 1) + "'s top ring.");
                yield return PickTowerStackByInput(screen, to, pad);
                Assert.That(run.Tower.Moves, Is.EqualTo(moves + 1), "The press put it on stack " + (to + 1) + ".");
            }
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator RingTower_TheRankedTowerDeploysTheStacksPlaysByClicksAndCommitsOnce()
        {
            yield return InstallRingTowerAtFirstHoH();
            AssertEverydayYard("at the tower's briefing");
            Assert.That(ButtonWithCaption(CompetitionMiniGames.EnterCaption(CompetitionMiniGames.Kind.Tower)), Is.Not.Null, "Enter tower challenge");
            string facts = string.Join(" | ", ActiveRect(EpisodeHud.BriefingFactsName).GetComponentsInChildren<TMP_Text>().Select(text => text.text));
            Assert.That(facts, Does.Contain("4 rings, 3 stacks").And.Contain("90 seconds"), facts);
            var before = director.Snapshot;
            var found = new CompetitionGameScreen[1];
            yield return StartTowerAttempt(CompetitionMiniGames.EnterCaption(CompetitionMiniGames.Kind.Tower), found);
            var screen = found[0];
            AssertTowerDeployed("from the moment the ranked tower starts");
            var tower = director.CompetitionRingTowerShown;
            // The player takes their place in front of the middle stack.
            var middle = tower.PegFoot(1);
            float until = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < until
                && new Vector2(player.transform.position.x - middle.x, player.transform.position.z - middle.z).magnitude > 2.2f) yield return null;
            Assert.That(new Vector2(player.transform.position.x - middle.x, player.transform.position.z - middle.z).magnitude, Is.LessThan(2.2f),
                "The player stands at the stacks: " + player.transform.position + " against the middle peg at " + middle + ".");
            Assert.That(player.transform.position.z, Is.LessThan(middle.z), "On the house's side, facing them.");

            var run = ChallengeRun();
            yield return ClickTheTowerHome(screen, run);
            Assert.That(run.Finished, Is.True, "Moving the whole tower ends the game.");
            Assert.That((run.Tower.Moves, run.Tower.Refused), Is.EqualTo((15, 0)));
            Assert.That(run.Performance, Is.GreaterThanOrEqualTo(.8), "The fewest moves: at least six, two and the time left, out of ten.");
            for (int size = 1; size <= tower.Rings.Count; size++)
                Assert.That(tower.Home(size).x, Is.EqualTo(tower.PegFoot(2).x).Within(.001f), "Ring " + size + " belongs on the last stack.");

            float deadline = Time.realtimeSinceStartup + 6f;
            while (director.Snapshot.revision == before.revision && Time.realtimeSinceStartup < deadline) yield return null;
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(before.revision + 1), "The tower commits once.");
            Assert.That(after.competitionResolved, Is.True);
            Assert.That(after.competitionScores.Count, Is.EqualTo(EpisodeEngine.CompetitionPlayers(before).Count()), "Everyone in the field has a committed score.");
            Assert.That(after.events.Last(e => e.kind == "competition-definition").text, Does.StartWith("ring-tower-v5"));
            yield return null; yield return null;
            var card = SceneComponents<CompetitionResult>().Single();
            Assert.That(Labelled(card, "Player attempt"), Does.Contain("tower moved in 15 moves"), "The standings say how the tower went.");
            AssertTowerPutAway("after the ranked tower commits");
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator RingTower_ALargerRingOnASmallerOneIsRefusedWithACue()
        {
            yield return InstallRingTowerAtFirstHoH();
            var before = director.Snapshot;
            var found = new CompetitionGameScreen[1];
            yield return StartTowerAttempt("Practice this competition", found);
            var screen = found[0];
            var cues = new List<HouseAudio.Cue>();
            screen.CueRequested += cue => cues.Add(cue);
            var run = ChallengeRun();
            ResumeTower(screen);
            TowerStack(screen, 0).onClick.Invoke();
            TowerStack(screen, 1).onClick.Invoke();          // the smallest ring to the middle
            TowerStack(screen, 0).onClick.Invoke();          // lift the next
            yield return null;
            ResumeTower(screen);
            cues.Clear();
            TowerStack(screen, 1).onClick.Invoke();          // ...onto the smaller: refused
            yield return null;
            Assert.That(run.Tower.LastRefusal, Is.EqualTo(RingTowerPuzzle.Refusal.LargerOnSmaller));
            Assert.That((run.Tower.Moves, run.Tower.Refused), Is.EqualTo((1, 1)), "Refused, and nothing moved.");
            Assert.That(run.Tower.Stack(1), Is.EqualTo(new[] { 1 }));
            Assert.That(run.Tower.Stack(0), Is.EqualTo(new[] { 4, 3, 2 }));
            Assert.That(cues, Does.Contain(HouseAudio.Cue.SocialDown), "A refusal sounds.");
            Assert.That(Labelled(screen, "Attempt feedback"), Does.Contain("larger ring cannot go on a smaller one"), "and says why.");
            Assert.That(Labelled(screen, "Tower moves"), Is.EqualTo("Moves 1  ·  fewest 15"), "The move count shows.");
            TowerStack(screen, 0).onClick.Invoke();          // put it back
            ResumeTower(screen);
            yield return PressKey(Key.Escape);
            yield return null; yield return null;
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision), "A practice commits nothing.");
            AssertTowerPutAway("after the practice is left");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator RingTower_ThePracticeTowerPlaysToTheEndByTheKeyboardAndCommitsNothing()
        {
            yield return InstallRingTowerAtFirstHoH();
            var before = director.Snapshot;
            var found = new CompetitionGameScreen[1];
            yield return StartTowerAttempt("Practice this competition", found);
            AssertTowerDeployed("while the tower is practised");
            var run = ChallengeRun();
            yield return PlayTheTowerByInput(found[0], run, null);
            Assert.That(run.Finished && run.Tower.Solved, Is.True, "The keyboard moved the whole tower.");
            yield return null; yield return null;
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision), "A practice commits nothing.");
            Assert.That(director.Snapshot.randomState, Is.EqualTo(before.randomState), "and draws nothing.");
            ButtonWithCaption("Return to briefing").onClick.Invoke();
            yield return null; yield return null;
            AssertTowerPutAway("after the practice returns to the briefing");
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision));
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator RingTower_ThePracticeTowerPlaysToTheEndByThePad()
        {
            yield return InstallRingTowerAtFirstHoH();
            var before = director.Snapshot;
            var found = new CompetitionGameScreen[1];
            yield return StartTowerAttempt("Practice this competition", found);
            var run = ChallengeRun();
            yield return PlayTheTowerByInput(found[0], run, TestGamepad());
            Assert.That(run.Finished && run.Tower.Solved, Is.True, "The pad moved the whole tower.");
            yield return null; yield return null;
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision));
            ButtonWithCaption("Return to briefing").onClick.Invoke();
            yield return null; yield return null;
            AssertTowerPutAway("after the pad's practice");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator RingTower_SimulatedThrownAndAccessibleTowersCommitStraightToTheCard()
        {
            foreach (string caption in new[] { EpisodeHud.SimulateCompetitionCaption, EpisodeHud.ThrowCompetitionCaption,
                EpisodeDirector.AccessibleCompetitionCaption(CompetitionRules.RingTower) })
            {
                yield return InstallRingTowerAtFirstHoH();
                var before = director.Snapshot;
                ButtonWithCaption(caption).onClick.Invoke();
                Assert.That(director.CompetitionRingTowerShown, Is.Null, caption + ": no tower is staged.");
                Assert.That(director.CompetitionYardDeployed, Is.False, caption + ": nothing is staged in the yard.");
                yield return null; yield return null;
                var after = director.Snapshot;
                Assert.That(after.revision, Is.EqualTo(before.revision + 1), caption + " commits once, at once.");
                Assert.That(after.competitionResolved, Is.True);
                Assert.That(CompetitionCardPlaying(), Is.True, caption + ": the standings card plays.");
                AssertTowerPutAway("after " + caption);
            }
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator RingTower_AWatchedTowerVetoCommitsStraightToTheCard()
        {
            uint seed = 1;
            while (seed < 500 && CompetitionRules.Category(EpisodePhase.Veto, 1, seed) != CompetitionRules.Mental) seed++;
            Assert.That(CompetitionDefinitions.Version5(seed, 1, EpisodePhase.Veto), Is.SameAs(CompetitionDefinitions.RingTower));
            var initial = FullHouse(seed, 12);
            initial.competitionRulesVersion = CompetitionRules.RingTower;
            var npcs = initial.Active.Where(actor => !actor.isPlayer).Select(actor => actor.id).ToList();
            initial.phase = EpisodePhase.Veto; initial.hohId = npcs[0];
            initial.nominees = new List<string> { npcs[1], npcs[2] };
            initial.vetoPlayers = npcs.Take(EpisodeEngine.VetoPlayerCount(initial.Active.Count())).ToList();
            Assert.That(EpisodeValidation.TryValidate(initial, out var reason), Is.True, reason);
            new EpisodeSaveStore(director.SavePath).Save(initial);
            yield return ReloadEpisode();
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            yield return null;
            var before = director.Snapshot;
            ButtonWithCaption("Watch eligible housemates compete").onClick.Invoke();
            Assert.That(director.CompetitionRingTowerShown, Is.Null, "A watched tower stages nothing.");
            yield return null; yield return null;
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision + 1), "Watching commits once.");
            Assert.That(director.Snapshot.competitionResolved, Is.True);
            Assert.That(CompetitionCardPlaying(), Is.True, "The standings card plays.");
            AssertTowerPutAway("after a watched tower");
        }
    }
}
