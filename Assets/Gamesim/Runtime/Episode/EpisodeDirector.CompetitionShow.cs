using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>
    /// The competition as a show in the yard (competition show, slice 1). When an endurance
    /// competition is decided without the ranked game being played - watched from outside the
    /// field, simulated, thrown or taken the accessible way - the result commits at once, as it
    /// always has, and its standings card waits behind a short show played from those standings:
    /// the arena is built as for the player's own game (the yard's everyday pieces put away, a grip
    /// rig at a station for every competitor, the player's among them when they competed), the
    /// field walks out and holds on, each lets go in the reverse order of the standings and walks to
    /// the side, and the last one holding on - the winner - cheers at their station. Then the card
    /// plays, and the yard is put back.
    ///
    /// <para>Presentation only, as the ceremony stage is: nothing here is saved, draws from the
    /// season's generator or changes what was committed; the show only defers the card. A press -
    /// the skip chip's - plays the card at once; the ceremonies' pace makes it shorter; reduced motion
    /// keeps the walk to the stations and holds the field in a still pose for a moment before the
    /// card, with no drops and no cheer. A load, a new commit or the director switched off ends it
    /// with everyone let go and the yard put back, and its card, never played, stays unplayed.</para>
    ///
    /// <para>Off in a batch run unless a test asks (<see cref="CompetitionShowsInBatchRuns"/>), so the
    /// audited suites' timing is unchanged; and never for the ranked game, whose own arena is the show.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>For tests: stage the competition shows even in a batch run, where they are otherwise skipped.</summary>
        public bool CompetitionShowsInBatchRuns { get; set; }

        /// <summary>Whether endurance competitions decided without the ranked game are staged as a show at all. On for play.</summary>
        public bool CompetitionShows { get; set; } = true;

        /// <summary>Where a running show is.</summary>
        public enum CompetitionShowStep { Assembling, Holding, Cheering }

        /// <summary>What one show did, kept after it ends, for tests to read.</summary>
        public sealed class CompetitionShowRecord
        {
            /// <summary>The committed standings the show played, best first.</summary>
            public IReadOnlyList<string> Standings { get; internal set; }
            /// <summary>Who stood at a station, the player included when they competed and their route allowed.</summary>
            public IReadOnlyList<string> Stationed { get; internal set; }
            /// <summary>Who let go, in the order they did.</summary>
            public IReadOnlyList<string> Drops { get; internal set; }
            /// <summary>Who had a station but had not reached it when the hold started, and so was left out of the staging.</summary>
            public IReadOnlyList<string> LeftOut { get; internal set; }
            /// <summary>Who cheered, or null when the show ended before the cheer.</summary>
            public string Cheered { get; internal set; }
            /// <summary>How far the cheering body's root stood from its station's place when it cheered, in metres; NaN without a cheer.</summary>
            public float CheerOffset { get; internal set; } = float.NaN;
            /// <summary>Whether the show reached its card - played to its end or skipped - rather than being cut off.</summary>
            public bool CardPlayed { get; internal set; }
            /// <summary>Whether a press skipped it.</summary>
            public bool Skipped { get; internal set; }
            /// <summary>Whether it played under reduced motion.</summary>
            public bool Reduced { get; internal set; }
            /// <summary>How many of the yard's renderers it put away while it ran.</summary>
            public int PutAway { get; internal set; }
            /// <summary>Who the walk out hurried past the house's walk to reach their station in time, in the order they were first hurried.</summary>
            public IReadOnlyList<string> Hurried { get; internal set; }
            /// <summary>Whom the walk out put in the run gait at some point, in the order they first took it.</summary>
            public IReadOnlyList<string> RanOut { get; internal set; }
            /// <summary>Seconds from the show's start to its hold - the field assembled, or the wait for it up; NaN when no hold began.</summary>
            public float AssembledSeconds { get; internal set; } = float.NaN;
            /// <summary>
            /// Seconds from the show's start to when the walk out meant the field to stand at its stations:
            /// <see cref="CompetitionShowPlan.WalkOutSeconds"/>, or later when a route was longer than that
            /// (<see cref="CompetitionShowPlan.ExtendWalkOut"/>).
            /// </summary>
            public float WalkOutSeconds { get; internal set; } = float.NaN;
            /// <summary>Seconds from the show's start to when the wait for the field was up (<see cref="CompetitionShowPlan.AssemblyBy"/>).</summary>
            public float AssemblyWaitSeconds { get; internal set; } = float.NaN;
        }

        private sealed partial class CompetitionShowRun
        {
            public CompetitionShowPlan Plan;
            public CompetitionDefinition Definition;
            public Action PlayCard;
            public readonly List<Action> AfterCard = new List<Action>();
            public bool Quick, Reduced, Skipped, CardPlayed;
            public float StartedAt, HoldFrom = -1f, HardBy, PressGuardUntil, CheeredAt;
            public int NextDrop;
            public readonly List<string> Dropped = new List<string>();
            public readonly HashSet<string> Out = new HashSet<string>();
            public readonly Dictionary<string, float> SideAt = new Dictionary<string, float>();
            public readonly Dictionary<string, HouseInteractionAnchor> SideMarks = new Dictionary<string, HouseInteractionAnchor>();
            public readonly List<string> Stationed = new List<string>();
            /// <summary>Who lets go, in order, among those at their stations when the hold started; set then.</summary>
            public IReadOnlyList<string> DropOrder;
            /// <summary>Who had a station but had not reached it when the hold started: left out of the staging.</summary>
            public readonly List<string> LeftOut = new List<string>();
            public bool Cheered;
            public float CheerOffset = float.NaN;
            public int PutAway;
            public bool HoldBegun => HoldFrom >= 0f;
            /// <summary>The walk out: who was ever hurried and who ever ran, in order.</summary>
            public readonly List<string> Hurried = new List<string>(), RanOut = new List<string>();
            /// <summary>Whose body the walk out has in the run gait now.</summary>
            public readonly Dictionary<string, CharacterPresentation> Running = new Dictionary<string, CharacterPresentation>();
            /// <summary>When the walk out next reads the walkers' routes; whether it is over.</summary>
            public float NextPaceAt;
            public bool WalkOutOver;
            /// <summary>When the walk out means the field to stand at its stations: later than its wait when a route is long.</summary>
            public float WalkOutBy = float.NaN;
        }

        private CompetitionShowRun competitionShow;

        /// <summary>Whether a commit is the ranked game's (or the timing bar's) own, played live: it never gets a show.</summary>
        private bool competitionLiveCommit;

        /// <summary>A press within this long of the show's start belongs to the click that committed it.</summary>
        private const float CompetitionShowPressGuard = .35f;

        /// <summary>Whether a competition show is running now.</summary>
        public bool IsCompetitionShowRunning => competitionShow != null;

        /// <summary>Where the running show is, or null.</summary>
        public CompetitionShowStep? CompetitionShowPhase => competitionShow == null ? (CompetitionShowStep?)null
            : !competitionShow.HoldBegun ? CompetitionShowStep.Assembling
            : competitionShow.Cheered ? CompetitionShowStep.Cheering : CompetitionShowStep.Holding;

        /// <summary>Who has let go so far in the running show, in order. Empty when none runs.</summary>
        public IReadOnlyList<string> CompetitionShowDrops => competitionShow != null ? competitionShow.Dropped.ToArray() : Array.Empty<string>();

        /// <summary>The last show's record, kept after it ends; null before the first.</summary>
        public CompetitionShowRecord LastCompetitionShow { get; private set; }

        /// <summary>The yard's renderers the arena has put away now (its own game's or a show's). Empty when none.</summary>
        public IReadOnlyList<Renderer> CompetitionYardPutAway => competitionYard.Hidden;

        /// <summary>Whether the yard's course is deployed as a competition's set now (competition yard, slice 2); false in everyday life.</summary>
        public bool CompetitionYardDeployed => competitionYard.State == CompetitionYardDressing.YardState.Competition;

        /// <summary>The kind of competition the course is deployed for; null in everyday life.</summary>
        public string CompetitionYardKind => competitionYard.Kind;

        /// <summary>The competition equipment put away for everyday life now (competition yard, slice 2). Empty while a set is deployed.</summary>
        public IReadOnlyList<GameObject> CompetitionYardStowed => competitionYard.Stowed;

        /// <summary>Skips the running show: the card plays now. What the skip chip's press does; public for tests.</summary>
        public void SkipCompetitionShow()
        {
            if (competitionShow == null) return;
            competitionShow.Skipped = true;
            FinishCompetitionShow();
        }

        /// <summary>Whether the show is telling its story: the HUD stands aside for it as for a reveal.</summary>
        private bool CompetitionShowNarrating => competitionShow != null;

        /// <summary>
        /// Stages the competition this commit just decided, when it is one the show plays and this
        /// house can: the card is played by <paramref name="playCard"/> when the show is done, instead
        /// of now. Returns false - and the caller plays the card at once, as it always has - for any
        /// other kind, the ranked game's own commit, a batch run not asking, or a house that cannot
        /// stage it.
        /// </summary>
        private bool TryBeginCompetitionShow(EpisodeState before, EpisodeState committed, EpisodePhase phase, string category, Action playCard)
        {
            if (!CompetitionShows || competitionLiveCommit || (Application.isBatchMode && !CompetitionShowsInBatchRuns)
                || before == null || committed == null || playCard == null) return false;
            // By game, in the game's family (competition staging, slice 3): a game the show does not know commits to its card at once.
            var plan = CompetitionShowPlan.For(committed, phase, category, CompetitionDefinitions.For(before)?.Id);
            if (plan == null) return false;
            string declined = CompetitionShowDeclined();
            if (declined != null) { Debug.Log("Competition show declined: " + declined); return false; }
            // The competition takes the yard: anybody still walking out goes now, as for the game.
            FinishWalkOut(immediate: true);
            float now = Time.unscaledTime;
            var run = new CompetitionShowRun
            {
                Plan = plan, Definition = CompetitionDefinitions.For(before), PlayCard = playCard,
                Quick = ceremonyPace == CeremonyPace.Quick, Reduced = reducedMotion,
                StartedAt = now, PressGuardUntil = now + CompetitionShowPressGuard,
            };
            run.HardBy = now + plan.HardSeconds(run.Quick, run.Reduced);
            run.WalkOutBy = now + CompetitionShowPlan.WalkOutSeconds(run.Quick);
            if (!BeginCompetitionArena(committed, run) || competitionShow != run)
            {
                // The arena refused on its first tick and the show already played its card: not twice.
                if (run.CardPlayed) return true;
                if (competitionShow == run) EndCompetitionArena();
                Debug.Log("Competition show declined: " + (competitionArenaStatus ?? "the arena could not be built"));
                return false;
            }
            if (competitionPlayerStation != null) run.Stationed.Add(plan.PlayerId);
            foreach (var id in plan.Standings) if (competitionArenaActors.ContainsKey(id)) run.Stationed.Add(id);
            if (run.Stationed.Count < 2)
            {
                EndCompetitionArena();
                Debug.Log("Competition show declined: fewer than two of the field could take a station.");
                return false;
            }
            if (!run.Stationed.Contains(plan.WinnerId))
            {
                // The winner cheers at their station or not at all: without one there is no show to stage.
                EndCompetitionArena();
                Debug.Log("Competition show declined: the winner could not take a station.");
                return false;
            }
            run.PutAway = competitionYard.Hidden.Count;
            // The chrome stands aside as for a reveal, the house's input is held, and the camera takes the yard.
            HoldHudForReveal(redraw: true);
            if (player != null) player.SetInputEnabled(false);
            FrameCompetitionShow(run);
            // The walk out keeps pace with the wait from its first step.
            PaceCompetitionShowWalkOut(run, now);
            return true;
        }

        /// <summary>Why this house cannot stage a show now, in words for the log, or null when it can.</summary>
        private string CompetitionShowDeclined()
        {
            if (npcMeetings == null) return "the house's world is not built";
            if (npcWorldFailed) return "the house's world stopped: " + npcWorldFailure;
            if (npcDiagnosticsSuspended) return "the house's world is suspended for diagnostics";
            if (!npcMeetings.IsReady) return "the house's world is still binding its people";
            if (challengeActive || competitionArenaStaging) return "the competition arena is in use";
            if (IsCeremonyStaged) return "a ceremony has the house";
            if (openingStage != null && openingStage.Active) return "the opening has the house";
            if (player == null || player.Agent == null || !player.Agent.isOnNavMesh) return "the player has no body on the NavMesh";
            if (cameraRig == null) return "there is no camera rig";
            return null;
        }

        /// <summary>The yard wide, as the game's own competition shot takes it; under reduced motion a cut.</summary>
        private void FrameCompetitionShow(CompetitionShowRun run)
        {
            if (cameraRig == null || competitionStageFloor == null) return;
            var deck = competitionStageFloor.bounds;
            cameraRig.MoveTo(new HouseCameraRig.Shot
            {
                Focus = new Vector3(deck.center.x, deck.max.y + 1f, deck.center.z), Distance = CompetitionShotDistance,
                Pitch = CompetitionShotPitch, Yaw = 0f, FieldOfView = CompetitionShotFieldOfView,
                Seconds = run.Reduced || cameraRig.ReducedMotion ? CeremonyCutSeconds : CompetitionShotSeconds,
                Rooms = CompetitionRooms, TravelFromRooms = ViewedRooms,
            });
        }

        /// <summary>Each frame: the show's beats, from the walk out to the card.</summary>
        private void TickCompetitionShow()
        {
            var run = competitionShow;
            if (run == null) return;
            if (!competitionArenaStaging) { EndCompetitionShowState(); return; }
            float now = Time.unscaledTime;
            // The show holds the house's input as a card does: the press is the skip's.
            CeremonyOverlays.Showing();
            if (cameraRig != null) cameraRig.ControlsEnabled = false;
            if (player != null) player.SetInputEnabled(false);
            if (now >= run.PressGuardUntil && CeremonyTakeover.SkipPressed()) { run.Skipped = true; FinishCompetitionShow(); return; }
            if (now > run.HardBy)
            {
                Debug.Log("[Gamesim competition show] the show ran past its bound; the card plays. " + CompetitionStageDiagnostic(), this);
                FinishCompetitionShow(); return;
            }
            var plan = run.Plan;
            if (!run.HoldBegun)
            {
                // The hold waits for the field at its stations. Once the walk out is up it starts with those
                // there, provided the winner and a stationed player are; past the wait for those two the card
                // plays without a show, so nobody lets go or cheers away from their rig.
                // Both waits run from when the walk out means the field to be there: later than its
                // own wait when a route is long - a quick ceremony, the far end of the house, a big cast.
                PaceCompetitionShowWalkOut(run, now);
                if (!CompetitionShowFieldArrived(run))
                {
                    if (now < CompetitionShowPlan.AssemblyBy(run.WalkOutBy, run.Quick)) return;
                    if (!CompetitionShowPrincipalsArrived(run))
                    {
                        if (now < CompetitionShowPlan.PrincipalBy(run.WalkOutBy, run.Quick)) return;
                        Debug.Log("[Gamesim competition show] the winner did not reach their station in time; the card plays. " + CompetitionStageDiagnostic(), this);
                        FinishCompetitionShow(); return;
                    }
                }
                BeginCompetitionShowHold(run, now);
            }
            float t = now - run.HoldFrom;
            int drops = run.DropOrder.Count;
            if (run.Reduced)
            {
                if (t >= CompetitionShowPlan.CardAt(drops, run.Quick, true)) FinishCompetitionShow();
                return;
            }
            // A podium, table or course show plays its own beats (EpisodeDirector.CompetitionShowFamilies.cs).
            if (plan.Family != CompetitionShowFamily.Hold) { TickCompetitionShowFamily(run, now, t); return; }
            while (run.NextDrop < drops && t >= CompetitionShowPlan.DropAt(run.NextDrop, drops, run.Quick))
            {
                DropCompetitionShowEntrant(run, run.DropOrder[run.NextDrop], run.NextDrop == drops - 1 && run.DropOrder[run.NextDrop] == plan.Standings[1]);
                run.NextDrop++;
            }
            foreach (var id in run.SideAt.Where(pair => now >= pair.Value).Select(pair => pair.Key).ToArray())
            {
                run.SideAt.Remove(id);
                SendCompetitionShowEntrantAside(run, id);
            }
            // The winner cheers at their station only: pushed off it, the cheer waits (the show's bound still holds).
            if (!run.Cheered && t >= CompetitionShowPlan.CheerAt(drops, run.Quick) && CompetitionShowArrived(run, plan.WinnerId))
            {
                run.CheeredAt = now;
                CheerCompetitionShowWinner(run);
            }
            if (run.Cheered && now - run.CheeredAt >= CompetitionShowPlan.CheerSeconds(run.Quick)) FinishCompetitionShow();
        }

        /// <summary>
        /// The hold starts: those at their stations hold on and let go in the reverse order of the
        /// standings; anybody stationed but still walking is left out of the staging - no drop, no
        /// gesture - and goes to their place at the side.
        /// </summary>
        private void BeginCompetitionShowHold(CompetitionShowRun run, float now)
        {
            run.HoldFrom = now;
            // The walk out is over: everybody walks at the house's pace again, a latecomer to the side included.
            EndCompetitionShowWalkOut(run);
            var holding = new HashSet<string>();
            foreach (var id in run.Stationed)
            {
                if (CompetitionShowArrived(run, id)) holding.Add(id);
                else { run.LeftOut.Add(id); run.Out.Add(id); run.SideAt[id] = now; }
            }
            run.DropOrder = run.Plan.DropOrderAmong(holding);
            if (run.LeftOut.Count > 0)
                Debug.Log("[Gamesim competition show] left out, still walking when the hold began: " + string.Join(", ", run.LeftOut), this);
        }

        /// <summary>Whether this stationed competitor stands at their station.</summary>
        private bool CompetitionShowArrived(CompetitionShowRun run, string id)
        {
            if (id == run.Plan.PlayerId) return player != null && player.ActivityHasArrived(competitionPlayerOwner);
            return npcMeetings != null && npcMeetings.CompetitionActorArrived(id);
        }

        /// <summary>Whether every stationed competitor stands at their station.</summary>
        private bool CompetitionShowFieldArrived(CompetitionShowRun run)
        {
            foreach (var id in run.Stationed) if (!CompetitionShowArrived(run, id)) return false;
            return true;
        }

        /// <summary>Whether the winner, and the player when they have a station, stand at their stations.</summary>
        private bool CompetitionShowPrincipalsArrived(CompetitionShowRun run)
        {
            if (!CompetitionShowArrived(run, run.Plan.WinnerId)) return false;
            return !run.Stationed.Contains(run.Plan.PlayerId) || CompetitionShowArrived(run, run.Plan.PlayerId);
        }

        // ------------------------------------------------------------ the walk out keeps pace with the wait

        /// <summary>
        /// The walk out, a few times a second until the hold: every stationed houseguest not yet at
        /// their station is paced to reach it by the walk out's time, a little inside the wait for the
        /// field (<see cref="CompetitionShowPlan.WalkOutSeconds"/>, <see cref="CompetitionShowPlan.WalkOutPace"/>),
        /// from the route they still have to go - so a late start, a queue at the yard's door or a
        /// detour is caught up - and takes the run gait when that pace is a run. Never under the
        /// house's walk and never over the top pace; whoever cannot make it even so is left out at the
        /// hold, as before. The houseguests of a fresh season stand all over the house when a
        /// competition is decided, and at the house's walk three of five needed 12.5 to 13 s to reach
        /// the yard's stations, past both the 9 s wait for the field and the 11 s wait for the winner.
        ///
        /// <para>The player's own walk to their station keeps the gait its route deserves - a run past
        /// 8 m - which their controller owns (<see cref="HousePlayerController.TryBeginActivityMove"/>);
        /// a pace written over it would be taken for a test's and conceded. Reduced motion paces the
        /// same way: this is movement, and the body's run gait is not an effect reduced motion holds back.</para>
        /// </summary>
        private void PaceCompetitionShowWalkOut(CompetitionShowRun run, float now)
        {
            if (run == null || run.WalkOutOver || run.HoldBegun || npcMeetings == null) return;
            // A runner who has arrived stops running the frame they arrive, not at the next reading.
            foreach (var id in run.Running.Keys.ToArray())
                if (npcMeetings.CompetitionActorArrived(id)) { npcMeetings.PaceCompetitionActor(id, 0f); PutBackCompetitionShowWalker(run, id); }
            if (now < run.NextPaceAt) return;
            run.NextPaceAt = now + CompetitionShowPlan.WalkOutRereadSeconds;
            ExtendCompetitionShowWalkOut(run, now);
            float secondsLeft = run.WalkOutBy - now;
            foreach (var id in run.Stationed)
            {
                if (id == run.Plan.PlayerId) continue;
                float pace = CompetitionShowPlan.WalkPace;
                if (!npcMeetings.CompetitionActorArrived(id))
                {
                    float remaining = npcMeetings.CompetitionActorRemaining(id);
                    if (remaining < 0f) { PutBackCompetitionShowWalker(run, id); continue; }
                    pace = CompetitionShowPlan.WalkOutPace(remaining, secondsLeft);
                }
                bool hurried = pace > CompetitionShowPlan.WalkPace + .01f;
                // The house's own pace again on arrival, and for anybody the pace no longer hurries.
                if (!npcMeetings.PaceCompetitionActor(id, hurried ? pace : 0f)) { PutBackCompetitionShowWalker(run, id); continue; }
                if (hurried && !run.Hurried.Contains(id)) run.Hurried.Add(id);
                bool running = run.Running.ContainsKey(id);
                if (CompetitionShowPlan.RunsAt(pace, running) == running) continue;
                if (running) { PutBackCompetitionShowWalker(run, id); continue; }
                var visual = CompetitionShowVisualOf(id);
                if (visual == null) continue;
                visual.SetRunning(true);
                run.Running[id] = visual;
                if (!run.RanOut.Contains(id)) run.RanOut.Add(id);
            }
        }

        /// <summary>
        /// The walk out's own time, read again from the routes still to go (competition show edges): a
        /// houseguest who cannot reach their station by it at the planned pace, or the player at their
        /// own gait, moves it later - up to <see cref="CompetitionShowPlan.MostWalkOutSeconds"/> - and the
        /// waits for the field and the winner, and the show's bound, move with it. So at a quick ceremony
        /// and in a cast of sixteen the far end of the house still reaches the field before the show starts.
        /// </summary>
        private void ExtendCompetitionShowWalkOut(CompetitionShowRun run, float now)
        {
            float was = run.WalkOutBy;
            foreach (var id in run.Stationed)
            {
                if (id == run.Plan.PlayerId)
                {
                    if (player == null || player.Agent == null || !player.Agent.enabled || !player.Agent.isOnNavMesh
                        || player.ActivityHasArrived(competitionPlayerOwner)) continue;
                    float left = player.Agent.pathPending || !player.Agent.hasPath || float.IsInfinity(player.Agent.remainingDistance)
                        ? Flat(player.transform.position - competitionPlayerStationPosition) : player.Agent.remainingDistance;
                    run.WalkOutBy = CompetitionShowPlan.ExtendWalkOut(run.WalkOutBy, now, run.StartedAt, run.Quick, left, player.Agent.speed * .9f);
                    continue;
                }
                if (npcMeetings.CompetitionActorArrived(id)) continue;
                run.WalkOutBy = CompetitionShowPlan.ExtendWalkOut(run.WalkOutBy, now, run.StartedAt, run.Quick, npcMeetings.CompetitionActorRemaining(id));
            }
            if (run.WalkOutBy > was) run.HardBy += run.WalkOutBy - was;
        }

        /// <summary>One walker out of the walk out's run gait (their pace is the caller's, or went with their route).</summary>
        private static void PutBackCompetitionShowWalker(CompetitionShowRun run, string id)
        {
            if (!run.Running.TryGetValue(id, out var visual)) return;
            run.Running.Remove(id);
            if (visual != null) visual.SetRunning(false);
        }

        /// <summary>
        /// The walk out is over - the hold began, or the show ended by any path: everybody it hurried
        /// walks at the house's pace again and nobody keeps its run gait. The course's own legs, later,
        /// set their own run.
        /// </summary>
        private void EndCompetitionShowWalkOut(CompetitionShowRun run)
        {
            if (run == null || run.WalkOutOver) return;
            run.WalkOutOver = true;
            // A stage already ended released its routes, and with them the pace (HouseNpcMotion.Release).
            if (npcMeetings != null)
                foreach (var id in run.Hurried) npcMeetings.PaceCompetitionActor(id, 0f);
            foreach (var id in run.Running.Keys.ToArray()) PutBackCompetitionShowWalker(run, id);
        }

        /// <summary>A stationed houseguest's body, or null.</summary>
        private CharacterPresentation CompetitionShowVisualOf(string id)
        {
            if (competitionBodies.TryGetValue(id, out var body) && body.visual != null) return body.visual;
            var root = BodyFor(id);
            return root != null ? root.GetComponent<CharacterPresentation>() : null;
        }

        /// <summary>Whether the show has let this competitor go: dropped, or the winner once cheering.</summary>
        private bool CompetitionShowReleased(string id) => competitionShow != null && id != null
            && (competitionShow.Out.Contains(id) || competitionShow.Cheered && id == competitionShow.Plan.WinnerId || CompetitionShowAway(id));

        /// <summary>Whether this competitor holds on in the running show now.</summary>
        private bool CompetitionShowGripping(string id) => competitionShow != null && competitionShow.HoldBegun && !CompetitionShowReleased(id);

        /// <summary>How hard this competitor is working now, 0 to 1; never under reduced motion.</summary>
        private float CompetitionShowStrain(string id)
        {
            var run = competitionShow;
            if (run == null || !run.HoldBegun || run.Reduced || CompetitionShowReleased(id)) return 0f;
            return run.Plan.Strain(run.DropOrder, id, Time.unscaledTime - run.HoldFrom, run.Quick);
        }

        /// <summary>One competitor lets go: hands off the grip, the rig reads out, a shrug - or the runner-up's shock - then the walk aside.</summary>
        private void DropCompetitionShowEntrant(CompetitionShowRun run, string id, bool runnerUp)
        {
            run.Out.Add(id); run.Dropped.Add(id);
            run.SideAt[id] = Time.unscaledTime + CompetitionShowPlan.LetGoSeconds(run.Quick);
            if (competitionInstruments.TryGetValue(id, out var instrument) && instrument != null) instrument.ShowHold(0f, false);
            CharacterPresentation visual = null;
            if (competitionBodies.TryGetValue(id, out var body))
            {
                if (body.contact != null) body.contact.Release();
                visual = body.visual;
            }
            if (visual == null)
            {
                var root = BodyFor(id);
                if (root != null) visual = root.GetComponent<CharacterPresentation>();
            }
            if (visual == null) return;
            ReleaseCompetitionStance(visual);
            if (runnerUp || !visual.MakeGesture(CharacterPresentation.Gesture.Shrug)) visual.React(CharacterPresentation.Reaction.Shocked);
        }

        /// <summary>A dropped competitor walks to their place at the side; with none, or no route, they stand by their rig.</summary>
        private void SendCompetitionShowEntrantAside(CompetitionShowRun run, string id)
        {
            if (!run.SideMarks.TryGetValue(id, out var mark) || mark == null) return;
            if (id == run.Plan.PlayerId)
            {
                if (player == null || competitionPlayerStation == null) return;
                player.ReleaseActivityMove(competitionPlayerOwner);
                if (!player.TryBeginActivityMove(competitionPlayerOwner, mark.Position, out var reason))
                {
                    Debug.Log("[Gamesim competition show] the player stays by their rig: " + reason, this);
                    player.TryBeginActivityMove(competitionPlayerOwner, competitionPlayerStationPosition, out _);
                }
                var playerVisual = player.GetComponent<CharacterPresentation>();
                if (playerVisual != null) playerVisual.SetFacing(float.NaN);
                return;
            }
            if (npcMeetings != null && !npcMeetings.MoveCompetitionActor(id, mark))
                Debug.Log("[Gamesim competition show] " + id + " stays by their rig: no route to the side.", this);
        }

        /// <summary>The last one holding on cheers where they stand - their station - and the yard turns to them.</summary>
        private void CheerCompetitionShowWinner(CompetitionShowRun run)
        {
            run.Cheered = true;
            string id = run.Plan.WinnerId;
            Transform root = null;
            Vector3 station = default;
            bool stationed = false;
            if (id == run.Plan.PlayerId && competitionPlayerStation != null && player != null)
            { root = player.transform; station = competitionPlayerStationPosition; stationed = true; }
            else if (competitionArenaActors.TryGetValue(id, out var anchor) && anchor != null)
            { root = BodyFor(id); station = anchor.Position; stationed = true; }
            else root = BodyFor(id);
            CharacterPresentation visual = null;
            if (competitionBodies.TryGetValue(id, out var body))
            {
                if (body.contact != null) body.contact.Release();
                visual = body.visual;
            }
            if (visual == null && root != null) visual = root.GetComponent<CharacterPresentation>();
            if (visual != null) { ReleaseCompetitionStance(visual); visual.React(CharacterPresentation.Reaction.Won); }
            if (root != null && stationed)
            {
                var flat = root.position - station; flat.y = 0f;
                run.CheerOffset = flat.magnitude;
            }
            if (projected != null) TurnHeads(projected, id, null);
        }

        /// <summary>The heading from <paramref name="from"/> to the winner's body, or NaN when there is none.</summary>
        private float CompetitionShowFacingWinner(Vector3 from)
        {
            if (competitionShow == null) return float.NaN;
            var winner = BodyFor(competitionShow.Plan.WinnerId);
            if (winner == null) return float.NaN;
            var to = winner.position - from;
            if (to.x * to.x + to.z * to.z < .01f) return float.NaN;
            return Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// A released competitor, each frame from the arena's tick: a dropped one faces the field from
        /// the side once there (the winner once they are cheering); the winner keeps their station's heading.
        /// </summary>
        private void TickCompetitionShowReleased(string id, CompetitionBody body)
        {
            var visual = body != null ? body.visual : null;
            if (visual == null || competitionShow == null) return;
            visual.SetTalking(false); visual.SetArguing(false); visual.SetSeated(false);
            if (CompetitionShowAway(id)) { visual.SetFacing(float.NaN); return; }
            if (id == competitionShow.Plan.WinnerId)
            {
                if (competitionArenaActors.TryGetValue(id, out var anchor) && anchor != null) visual.SetFacing(anchor.Facing);
                return;
            }
            bool there = npcMeetings != null && npcMeetings.CompetitionActorArrived(id);
            visual.SetFacing(there && competitionShow.Cheered && body.root != null ? CompetitionShowFacingWinner(body.root.transform.position) : float.NaN);
        }

        /// <summary>The player, released in the show, each frame: facing the winner from the side once there.</summary>
        private void TickCompetitionShowPlayerReleased()
        {
            if (player == null || competitionShow == null) return;
            var visual = player.GetComponent<CharacterPresentation>();
            if (visual == null) return;
            if (CompetitionShowAway(competitionShow.Plan.PlayerId)) { visual.SetFacing(float.NaN); return; }
            if (competitionShow.Plan.WinnerId == competitionShow.Plan.PlayerId) { visual.SetFacing(competitionPlayerStationFacing); return; }
            bool there = player.ActivityHasArrived(competitionPlayerOwner);
            visual.SetFacing(there && competitionShow.Cheered ? CompetitionShowFacingWinner(player.transform.position) : float.NaN);
        }

        /// <summary>
        /// The show's end with its card: the arena goes - everyone let go, the yard put back - and the
        /// standings card plays, with whatever was waiting behind it (a final part's bracket).
        /// </summary>
        private void FinishCompetitionShow()
        {
            var run = competitionShow;
            if (run == null) return;
            run.CardPlayed = true;
            EndCompetitionArena();
            if (cameraRig != null) cameraRig.ReleaseShot(CompetitionShotSeconds);
            run.PlayCard?.Invoke();
            foreach (var after in run.AfterCard) after?.Invoke();
            if (isActiveAndEnabled) SyncCompetitionResultInput();
        }

        /// <summary>
        /// Forgets the running show, from the arena's end whatever ended it; its card plays only if
        /// <see cref="FinishCompetitionShow"/> is the caller. Its record is kept for tests.
        /// </summary>
        private void EndCompetitionShowState()
        {
            var run = competitionShow;
            if (run == null) return;
            competitionShow = null;
            EndCompetitionShowWalkOut(run);
            EndCompetitionShowFamily(run);
            LastCompetitionShow = new CompetitionShowRecord
            {
                Standings = run.Plan.Standings.ToArray(), Stationed = run.Stationed.ToArray(), Drops = run.Dropped.ToArray(), LeftOut = run.LeftOut.ToArray(),
                Cheered = run.Cheered ? run.Plan.WinnerId : null, CheerOffset = run.CheerOffset, CardPlayed = run.CardPlayed,
                Skipped = run.Skipped, Reduced = run.Reduced, PutAway = run.PutAway,
                Hurried = run.Hurried.ToArray(), RanOut = run.RanOut.ToArray(),
                AssembledSeconds = run.HoldBegun ? run.HoldFrom - run.StartedAt : float.NaN,
                WalkOutSeconds = run.WalkOutBy - run.StartedAt,
                AssemblyWaitSeconds = CompetitionShowPlan.AssemblyBy(run.WalkOutBy, run.Quick) - run.StartedAt,
            };
            if (skipChip != null) skipChip.Show(false);
            if (!run.CardPlayed)
            {
                if (cameraRig != null) cameraRig.ReleaseShot(CompetitionShotSeconds);
                if (isActiveAndEnabled && IsReady && projected != null) SyncCompetitionResultInput();
            }
        }

        /// <summary>Whether a show is waiting to play its card: a card the same commit would play beside it waits too.</summary>
        private bool CompetitionShowHoldsTheCard => competitionShow != null && !competitionShow.CardPlayed;

        /// <summary>Plays <paramref name="card"/> after the running show's standings, or now when no show holds them.</summary>
        private void AfterCompetitionShow(Action card)
        {
            if (CompetitionShowHoldsTheCard) competitionShow.AfterCard.Add(card);
            else card?.Invoke();
        }
    }
}
