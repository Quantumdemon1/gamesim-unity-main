using System.Collections.Generic;
using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>
    /// The Ring Tower in the yard (competition ring tower, slice 4; competition rules 5).
    ///
    /// <para>When the player starts the ranked game or practice of a ring tower competition, the arena's set
    /// keeps the yard's three neon stacks standing rather than striking them from the field, and they become
    /// the playable tower (<see cref="CompetitionRingTower"/>): the stacks' own joined meshes are switched off
    /// through the yard's one owner and drawn again with separate, graduated rings that follow the player's
    /// picks. The player takes a place in front of the middle stack; the rest of the field stand at the side,
    /// as the arena places them, with no instrument - nobody pretends to solve. The camera frames the three
    /// stacks (reduced motion keeps the viewer's shot, as every automatic framing does). Everything goes
    /// with the arena, whatever ends it: the yard's restore switches the stacks' meshes back on and puts the
    /// equipment away.</para>
    ///
    /// <para>A ring tower competition that is not played - watched, simulated, thrown, or taken by the
    /// accessible route - commits straight to the standings card, as every game but endurance does today.
    /// Nothing here runs for a staged show.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>The attempt's tower, while a ring tower competition's arena stands; null otherwise.</summary>
        private CompetitionRingTower competitionRingTower;

        /// <summary>The playable tower standing in the yard now, for tests; null when none is.</summary>
        public CompetitionRingTower CompetitionRingTowerShown => competitionRingTower != null ? competitionRingTower : null;

        /// <summary>The ring tower's shot: back far enough for all three stacks, low enough to see the rings on the pegs.</summary>
        public const float RingTowerShotDistance = 12f, RingTowerShotPitch = 14f, RingTowerShotFieldOfView = 50f;

        /// <summary>
        /// How far east of the stacks' middle the tower's shot looks, in metres: the stacks stand left of the
        /// frame's middle, in the part the competition's side column leaves.
        /// </summary>
        public const float RingTowerShotShift = 1.6f;

        /// <summary>Whether this arena plays the player's ring tower: the tower's definition, and no staged show.</summary>
        private bool RingTowerArena(CompetitionDefinition definition) =>
            competitionShow == null && definition != null && definition.Pattern == CompetitionPattern.RingTower;

        /// <summary>
        /// The arena's set for a ring tower (called as the arena is built, after the yard is deployed and its
        /// scenery read): the stacks stand again, the scenery is read with them in it, and the playable tower
        /// is built on them. For any other competition it only forgets a tower an arena of the same frame left.
        /// </summary>
        private void DeployRingTower(Collider floor, CompetitionDefinition definition)
        {
            competitionRingTower = null;
            if (!RingTowerArena(definition) || floor == null || competitionArenaRoot == null) return;
            competitionYard.KeepStanding(CompetitionYardPieces.Stack);
            var stacks = RingTowerStacks(floor);
            // The stacks are drawn again before the stations are placed, so every reservation clears them.
            competitionScenery = CompetitionStageScenery.Collect(floor, competitionArenaRoot.transform);
            competitionRingTower = CompetitionRingTower.Build(competitionArenaRoot.transform, stacks, RingTowerPuzzle.StandardRings,
                () => challengeRun, () => reducedMotion, competitionYard.Conceal);
            if (competitionRingTower == null)
                Debug.LogWarning("[Gamesim ring tower] the yard has " + stacks.Count + " stacks to play on; the board plays the tower alone.", this);
        }

        /// <summary>The yard's stacks standing on the deck: the outermost node of each, drawn and active.</summary>
        private static List<Transform> RingTowerStacks(Collider floor)
        {
            var deck = floor.bounds;
            var found = new List<Transform>();
            foreach (var node in floor.transform.root.GetComponentsInChildren<Transform>())
            {
                if (CompetitionYardPieces.Stem(node.name) != CompetitionYardPieces.Stack) continue;
                bool nested = false;
                for (var at = node.parent; at != null; at = at.parent)
                    if (CompetitionYardPieces.Stem(at.name) == CompetitionYardPieces.Stack) { nested = true; break; }
                if (nested) continue;
                var at0 = node.position;
                if (at0.x < deck.min.x || at0.x > deck.max.x || at0.z < deck.min.z || at0.z > deck.max.z) continue;
                found.Add(node);
            }
            return found.OrderBy(node => node.position.x).ToList();
        }

        /// <summary>
        /// The player's places for a ring tower: in front of the middle stack, facing it, then a little to either
        /// side, then every place the arena would give them anyway. Any other competition: the arena's own.
        /// </summary>
        private IEnumerable<Vector3> RingTowerPlayerCandidates(IEnumerable<Vector3> arena)
        {
            if (competitionRingTower == null) return arena;
            var middle = competitionRingTower.PegFoot(1);
            var preferred = new List<Vector3>();
            foreach (float back in new[] { 1.25f, 1.45f, 1.7f })
                foreach (float side in new[] { 0f, -.5f, .5f })
                    preferred.Add(new Vector3(middle.x + side, middle.y, middle.z - back));
            return preferred.Concat(arena);
        }

        /// <summary>The rest of the field's places for a ring tower: nearest the yard's west side first, out of the tower's way.</summary>
        private IEnumerable<Vector3> RingTowerFieldCandidates(IEnumerable<Vector3> arena, Bounds deck)
        {
            if (competitionRingTower == null) return arena;
            var side = new Vector3(deck.min.x + 2f, deck.max.y, deck.center.z);
            return arena.OrderBy(place => (place - side).sqrMagnitude).ToArray();
        }

        /// <summary>
        /// The ring tower's shot, in place of the competition's wide: the three stacks, from the house's side.
        /// False when there is no tower to frame or the viewer keeps their own shot.
        /// </summary>
        private bool FrameRingTower()
        {
            if (competitionRingTower == null || cameraRig == null || cameraRig.ReducedMotion) return false;
            cameraRig.MoveTo(new HouseCameraRig.Shot
            {
                Focus = competitionRingTower.Centre + Vector3.right * RingTowerShotShift, Distance = RingTowerShotDistance,
                Pitch = RingTowerShotPitch, Yaw = 0f, FieldOfView = RingTowerShotFieldOfView, Seconds = CompetitionShotSeconds,
                Rooms = CompetitionRooms, TravelFromRooms = ViewedRooms,
            });
            competitionFramed = true;
            return true;
        }

        /// <summary>
        /// The game a competition plays, from its definition and its kind: the ring tower is a mental game
        /// with its own board; every other definition plays its kind's game.
        /// </summary>
        private static CompetitionMiniGames.Kind CompetitionGame(EpisodeState state) =>
            CompetitionMiniGames.For(CompetitionDefinitions.For(state), EpisodeEngine.CompetitionCategory(state));

        /// <summary>The game the open competition plays, for a test or the season walk. Precision when no competition is open.</summary>
        public CompetitionMiniGames.Kind CompetitionGameFor(EpisodeState state) =>
            state != null && EpisodeEngine.IsCompetition(state.phase) ? CompetitionGame(state) : CompetitionMiniGames.Kind.Precision;
    }
}
