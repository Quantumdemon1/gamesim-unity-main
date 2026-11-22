using System.Collections.Generic;
using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;
using UnityEngine.AI;

namespace Gamesim.Episode
{
    /// <summary>
    /// The staging families (competition staging, slice 3): the show slice 1 built for endurance,
    /// played for the other games in the family their equipment belongs to
    /// (<see cref="CompetitionShowFamilies"/>). The arena, the walk out, the skip, reduced motion,
    /// the bound and every way a show ends are slice 1's; this file plays the beats once the field is
    /// at its stations, from the committed standings (<see cref="CompetitionShowTimeline"/>):
    ///
    /// <para><b>Podium</b>: rounds of a quiz at the memory consoles - a question, everyone locks an
    /// answer in with a press, the reveal lights each console right or wrong; the wrong ones react
    /// and walk aside, the right ones stand easy; the winner cheers at their console.</para>
    /// <para><b>Table</b>: everyone works their dice tray or letter console with their hands; they
    /// finish in the standings' order, the winner first and cheering; time is called on the last.</para>
    /// <para><b>Course</b>: a slap of the reaction pad, a run out to a turn mark and back; the field
    /// turns for home in the standings' order and slaps the pad on getting back, the winner first.</para>
    /// <para><b>Tower</b>: the table's beats at the ring tower, with no instrument of anyone's own
    /// (EpisodeDirector.TowerShow.cs).</para>
    ///
    /// <para>Presentation only: nothing here is saved, draws from the season's generator or changes
    /// what was committed. Bodies move with what the cast has (poses, the cooking loop's hand work,
    /// the run, gestures and reactions); hands work the instruments through the instrument pose's
    /// show press.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>What one podium, table or course show did, kept after it ends, for tests to read.</summary>
        public sealed class CompetitionShowFamilyRecord
        {
            public CompetitionShowFamily Family { get; internal set; }
            /// <summary>The standings among those at their stations when the beats began, best first.</summary>
            public IReadOnlyList<string> Staged { get; internal set; }
            /// <summary>Who was resolved, in the order it happened: the podium's outs then its winner; the table's and course's finishes.</summary>
            public List<string> Order { get; } = new List<string>();
            /// <summary>The podium's outs, round by round, bottom first.</summary>
            public List<string> Outs { get; } = new List<string>();
            /// <summary>The table's and course's finishes, in order.</summary>
            public List<string> Finished { get; } = new List<string>();
            /// <summary>Course: runners not home by their own deadline, who never finish.</summary>
            public List<string> DidNotFinish { get; } = new List<string>();
            /// <summary>Table: the one time was called on, or null.</summary>
            public string TimeCalledOn { get; internal set; }
            /// <summary>Course: who ran a leg away from their post.</summary>
            public List<string> Ran { get; } = new List<string>();
            /// <summary>Who worked their instrument with a hand at least once (the show press).</summary>
            public HashSet<string> Pressed { get; } = new HashSet<string>();
            /// <summary>Who worked at a table in the cooking loop's hand work at least once.</summary>
            public HashSet<string> Worked { get; } = new HashSet<string>();
            /// <summary>The podium's rounds played.</summary>
            public int Rounds { get; internal set; }
            /// <summary>Whether the field turned to the winner before the card (table and course).</summary>
            public bool TurnedToWinner { get; internal set; }
            /// <summary>Course: each staged runner's post, where their leg starts and ends (competition show edges).</summary>
            public Dictionary<string, Vector3> LegFrom { get; } = new Dictionary<string, Vector3>();
            /// <summary>Course: each staged runner's turn mark, where their leg turns for home; none for a post with no leg.</summary>
            public Dictionary<string, Vector3> LegTo { get; } = new Dictionary<string, Vector3>();
            /// <summary>Tower: whose tower the stacks showed - the player's when they competed, else the winner's; null otherwise.</summary>
            public string TowerSubject { get; internal set; }
            /// <summary>Tower: the most moves the stacks showed done.</summary>
            public int TowerMovesShown { get; internal set; }
            /// <summary>Tower: whether the stacks showed the tower solved.</summary>
            public bool TowerSolved { get; internal set; }
        }

        /// <summary>The last podium, table or course show's record; null before the first.</summary>
        public CompetitionShowFamilyRecord LastCompetitionShowFamily { get; private set; }

        /// <summary>The family of the running show, or null when none runs.</summary>
        public CompetitionShowFamily? CompetitionShowFamilyRunning => competitionShow?.Plan.Family;

        /// <summary>The run speed of a course leg, metres a second: the house walks at 2.2.</summary>
        public const float CompetitionCourseRunSpeed = 3.8f;

        /// <summary>One competitor's state in a family show.</summary>
        private sealed class CompetitionShowActor
        {
            public int Rank;
            public CompetitionApparatus.ShowFace Face;
            public float FaceSince;
            public CharacterPresentation.Pose Pose = CharacterPresentation.Pose.AtEase;
            public bool Working;
            public float PressFrom = -1f, PressUntil = -1f;
            public float ReactUntil;
            public bool Away, Homeward, Running, Finished, DidNotFinish;
            /// <summary>Whether this is the player's houseguest, whose gait their controller owns.</summary>
            public bool IsPlayer;
        }

        private sealed partial class CompetitionShowRun
        {
            public CompetitionShowTimeline Timeline;
            public int NextBeat, NextHome;
            public bool FamilyBegun, TurnedToWinner;
            public float CardBy = float.PositiveInfinity;
            public CompetitionShowFamilyRecord FamilyRecord;
            public readonly Dictionary<string, CompetitionShowActor> Actors = new Dictionary<string, CompetitionShowActor>();
            public readonly Dictionary<string, HouseInteractionAnchor> CourseMarks = new Dictionary<string, HouseInteractionAnchor>();
        }

        /// <summary>Whether this competitor is out on a course leg now, away from their post.</summary>
        private bool CompetitionShowAway(string id)
            => competitionShow != null && id != null && competitionShow.Actors.TryGetValue(id, out var actor) && actor.Away;

        private CompetitionShowActor CompetitionShowActorFor(string id)
            => competitionShow != null && id != null && competitionShow.Actors.TryGetValue(id, out var actor) ? actor : null;

        // ------------------------------------------------------------ the beats

        /// <summary>Each frame of a podium, table or course show once its hold has begun (from TickCompetitionShow).</summary>
        private void TickCompetitionShowFamily(CompetitionShowRun run, float now, float t)
        {
            if (!run.FamilyBegun && !BeginCompetitionShowFamily(run)) return;
            var beats = run.Timeline.Beats;
            while (run.NextBeat < beats.Count && t >= beats[run.NextBeat].At)
            {
                var beat = beats[run.NextBeat++];
                PlayCompetitionShowBeat(run, beat, now);
                if (competitionShow != run) return;
            }
            foreach (var id in run.SideAt.Where(pair => now >= pair.Value).Select(pair => pair.Key).ToArray())
            {
                run.SideAt.Remove(id);
                SendCompetitionShowEntrantAside(run, id);
            }
            if (run.Plan.Family == CompetitionShowFamily.Course) TickCompetitionCourseHome(run, now, t);
            if (competitionShow == run && now >= run.CardBy) FinishCompetitionShow();
        }

        /// <summary>The beats begin with those at their stations: the timeline from their standings, and everyone's state.</summary>
        private bool BeginCompetitionShowFamily(CompetitionShowRun run)
        {
            run.FamilyBegun = true;
            var holding = new HashSet<string>(run.Stationed.Where(id => !run.LeftOut.Contains(id)));
            var staged = run.Plan.Standings.Where(holding.Contains).ToList();
            run.Timeline = CompetitionShowTimeline.For(run.Plan.Family, staged, run.Quick);
            run.FamilyRecord = new CompetitionShowFamilyRecord { Family = run.Plan.Family, Staged = staged.ToArray() };
            LastCompetitionShowFamily = run.FamilyRecord;
            if (run.Timeline == null)
            {
                Debug.Log("[Gamesim competition show] fewer than two at their stations; the card plays.", this);
                FinishCompetitionShow();
                return false;
            }
            for (int i = 0; i < staged.Count; i++)
                run.Actors[staged[i]] = new CompetitionShowActor { Rank = i, FaceSince = Time.unscaledTime, IsPlayer = staged[i] == run.Plan.PlayerId };
            foreach (var id in staged)
            {
                if (id == run.Plan.PlayerId) { if (competitionPlayerStation != null) run.FamilyRecord.LegFrom[id] = competitionPlayerStationPosition; }
                else if (competitionArenaActors.TryGetValue(id, out var post) && post != null) run.FamilyRecord.LegFrom[id] = post.Position;
                if (run.CourseMarks.TryGetValue(id, out var mark) && mark != null) run.FamilyRecord.LegTo[id] = mark.Position;
            }
            // A ring tower's rings follow the one at the stacks (EpisodeDirector.TowerShow.cs).
            BeginCompetitionTowerShow(run, staged);
            return true;
        }

        private void PlayCompetitionShowBeat(CompetitionShowRun run, CompetitionShowBeat beat, float now)
        {
            var record = run.FamilyRecord;
            switch (beat.Kind)
            {
                case CompetitionShowBeatKind.Question:
                    record.Rounds = beat.Round + 1;
                    foreach (var id in beat.Ids) { SetCompetitionShowFace(run, id, CompetitionApparatus.ShowFace.Question, now); SetCompetitionShowPose(run, id, CharacterPresentation.Pose.AtEase); }
                    break;
                case CompetitionShowBeatKind.LockIn:
                    foreach (var id in beat.Ids)
                    {
                        SetCompetitionShowFace(run, id, CompetitionApparatus.ShowFace.LockedIn, now);
                        // Not all in the same instant: each locks in a moment after the one above them.
                        PressCompetitionShowInstrument(run, id, now + CompetitionShowActorFor(id).Rank % 4 * .08f, .45f);
                    }
                    break;
                case CompetitionShowBeatKind.Reveal:
                    var outs = new HashSet<string>(beat.Ids);
                    foreach (var pair in run.Actors.Where(pair => !pair.Value.Finished && !run.Out.Contains(pair.Key)))
                    {
                        if (outs.Contains(pair.Key)) continue;
                        SetCompetitionShowFace(run, pair.Key, CompetitionApparatus.ShowFace.Right, now);
                        SetCompetitionShowPose(run, pair.Key, CharacterPresentation.Pose.HandOnHip);
                    }
                    foreach (var id in beat.Ids)
                    {
                        SetCompetitionShowFace(run, id, CompetitionApparatus.ShowFace.Wrong, now);
                        // Out as slice 1 lets go: hands off, a shrug - the runner-up's shock - and the walk aside.
                        DropCompetitionShowEntrant(run, id, id == run.Timeline.Standings[1]);
                        record.Outs.Add(id); record.Order.Add(id);
                    }
                    break;
                case CompetitionShowBeatKind.Work:
                    foreach (var id in beat.Ids)
                    {
                        var actor = CompetitionShowActorFor(id);
                        actor.Working = true;
                        SetCompetitionShowFace(run, id, CompetitionApparatus.ShowFace.Working, now);
                    }
                    break;
                case CompetitionShowBeatKind.Finish:
                    foreach (var id in beat.Ids) FinishCompetitionShowEntrant(run, id, now);
                    break;
                case CompetitionShowBeatKind.TimeUp:
                    foreach (var id in beat.Ids)
                    {
                        var actor = CompetitionShowActorFor(id);
                        actor.Working = false;
                        SetCompetitionShowFace(run, id, CompetitionApparatus.ShowFace.TimeUp, now);
                        record.TimeCalledOn = id; record.Order.Add(id);
                        ReactCompetitionShowEntrant(run, id, CharacterPresentation.Reaction.Shocked, now);
                    }
                    break;
                case CompetitionShowBeatKind.Go:
                    foreach (var id in beat.Ids)
                    {
                        SetCompetitionShowFace(run, id, CompetitionApparatus.ShowFace.Go, now);
                        PressCompetitionShowInstrument(run, id, now, .35f);
                    }
                    break;
                case CompetitionShowBeatKind.Depart:
                    foreach (var id in beat.Ids) SendCompetitionShowRunner(run, id, true);
                    break;
                case CompetitionShowBeatKind.Turn:
                    foreach (var id in beat.Ids) SendCompetitionShowRunner(run, id, false);
                    break;
                case CompetitionShowBeatKind.Cheer:
                    if (!run.Cheered)
                    {
                        run.CheeredAt = now;
                        CheerCompetitionShowWinner(run);
                        if (run.Plan.Family == CompetitionShowFamily.Podium) record.Order.Add(run.Plan.WinnerId);
                    }
                    break;
                // A course still waiting on a late runner lets them finish their leg first; the course's
                // homecomings turn the field to the winner once everyone is settled (competition show edges).
                case CompetitionShowBeatKind.TurnToWinner:
                    if (!run.Timeline.CardWaits(run.NextHome)) TurnCompetitionShowToWinner(run, now);
                    break;
                case CompetitionShowBeatKind.Card:
                    if (!run.Timeline.CardWaits(run.NextHome)) FinishCompetitionShow();
                    break;
            }
        }

        /// <summary>A table's or course's finisher: hands off the work, the instrument says so, and the winner cheers.</summary>
        private void FinishCompetitionShowEntrant(CompetitionShowRun run, string id, float now)
        {
            var actor = CompetitionShowActorFor(id);
            if (actor == null || actor.Finished) return;
            actor.Finished = true; actor.Working = false;
            bool course = run.Plan.Family == CompetitionShowFamily.Course;
            SetCompetitionShowFace(run, id, course ? CompetitionApparatus.ShowFace.Finished : CompetitionApparatus.ShowFace.Done, now);
            SetCompetitionShowPose(run, id, CharacterPresentation.Pose.HandOnHip);
            // The last slap of the pad, or the last tile or die set down.
            PressCompetitionShowInstrument(run, id, now, .4f);
            run.FamilyRecord.Finished.Add(id); run.FamilyRecord.Order.Add(id);
            if (id == run.Plan.WinnerId && !run.Cheered)
            {
                run.CheeredAt = now;
                CheerCompetitionShowWinner(run);
            }
        }

        /// <summary>The field turns to the winner, and the card follows a moment later.</summary>
        private void TurnCompetitionShowToWinner(CompetitionShowRun run, float now)
        {
            if (run.TurnedToWinner) return;
            run.TurnedToWinner = true; run.FamilyRecord.TurnedToWinner = true;
            if (projected != null) TurnHeads(projected, run.Plan.WinnerId, null);
            run.CardBy = Mathf.Min(run.CardBy, now + CompetitionShowTimeline.WinnerHoldSeconds(run.Quick));
        }

        /// <summary>
        /// The course's homecomings, each frame: the next in the standings finishes on reaching their
        /// post; one not home by their own deadline does not finish, and the next is looked at. Nobody
        /// finishes ahead of anyone above them, so the order is always the committed one.
        /// </summary>
        private void TickCompetitionCourseHome(CompetitionShowRun run, float now, float t)
        {
            var staged = run.Timeline.Standings;
            while (run.NextHome < staged.Count)
            {
                string id = staged[run.NextHome];
                var actor = CompetitionShowActorFor(id);
                bool home = !actor.Away || actor.Homeward && CompetitionShowRunnerHome(run, id);
                // A late runner is waited for through their grace (competition show edges): they finish
                // their leg and slap the pad in their place, and those below them wait at their posts.
                var call = run.Timeline.CallFor(id, t, home, actor.Homeward);
                if (call == CompetitionShowTimeline.CourseCall.Finish)
                {
                    actor.Away = false;
                    StopCompetitionShowRunner(id, actor);
                    FinishCompetitionShowEntrant(run, id, now);
                    if (competitionShow != run) return;
                    run.NextHome++;
                    continue;
                }
                if (call == CompetitionShowTimeline.CourseCall.DidNotFinish)
                {
                    actor.DidNotFinish = true;
                    run.FamilyRecord.DidNotFinish.Add(id);
                    run.NextHome++;
                    continue;
                }
                break;
            }
            // A runner past their deadline still comes home; at their post they stand, unfinished.
            foreach (var pair in run.Actors)
                if (pair.Value.DidNotFinish && pair.Value.Away && pair.Value.Homeward && CompetitionShowRunnerHome(run, pair.Key))
                { pair.Value.Away = false; StopCompetitionShowRunner(pair.Key, pair.Value); SetCompetitionShowPose(run, pair.Key, CharacterPresentation.Pose.AtEase); }
            if (run.NextHome >= staged.Count) TurnCompetitionShowToWinner(run, now);
        }

        /// <summary>Whether a homeward runner stands at their own post again.</summary>
        private bool CompetitionShowRunnerHome(CompetitionShowRun run, string id)
        {
            if (id == run.Plan.PlayerId)
                return player != null && competitionPlayerStation != null && player.ActivityHasArrived(competitionPlayerOwner)
                    && Flat(player.transform.position - competitionPlayerStationPosition) < .6f;
            if (npcMeetings == null || !npcMeetings.CompetitionActorArrived(id)) return false;
            var root = BodyFor(id);
            return root != null && competitionArenaActors.TryGetValue(id, out var anchor) && anchor != null && Flat(root.position - anchor.Position) < .6f;
        }

        private static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

        /// <summary>
        /// A course runner sets off: out to their turn mark, or home to their post. Hands off the pad and
        /// the stance let go for the run; a runner with no mark or no route stays at their post and
        /// finishes there in their turn.
        /// </summary>
        private void SendCompetitionShowRunner(CompetitionShowRun run, string id, bool outward)
        {
            var actor = CompetitionShowActorFor(id);
            if (actor == null || actor.Finished) return;
            if (outward && !run.CourseMarks.ContainsKey(id)) return;
            if (!outward && !actor.Away) return;
            HouseInteractionAnchor target = null;
            if (outward) target = run.CourseMarks[id];
            else if (id != run.Plan.PlayerId) competitionArenaActors.TryGetValue(id, out target);
            bool moving;
            if (id == run.Plan.PlayerId)
            {
                if (player == null || competitionPlayerStation == null) return;
                player.ReleaseActivityMove(competitionPlayerOwner);
                Vector3 to = outward ? target.Position : competitionPlayerStationPosition;
                moving = player.TryBeginActivityMove(competitionPlayerOwner, to, out var reason);
                if (!moving)
                {
                    Debug.Log("[Gamesim competition show] the player's leg has no route: " + reason, this);
                    // Back to the post whatever happens: the station's own route is the arena's.
                    player.TryBeginActivityMove(competitionPlayerOwner, competitionPlayerStationPosition, out _);
                }
                // The player's houseguest runs the leg as the others do (competition show edges): a leg is
                // a few metres, which the controller's own choice of gait would walk.
                else if (player.SetActivityGait(competitionPlayerOwner, true)) actor.Running = true;
            }
            else
            {
                moving = target != null && npcMeetings != null && npcMeetings.MoveCompetitionActor(id, target);
                var root = BodyFor(id);
                var motion = root != null ? root.GetComponent<HouseNpcMotion>() : null;
                if (moving && motion != null) motion.SetSpeed(CompetitionCourseRunSpeed);
                var visual = root != null ? root.GetComponent<CharacterPresentation>() : null;
                if (moving && visual != null) { visual.SetRunning(true); actor.Running = true; }
                if (!moving) Debug.Log("[Gamesim competition show] " + id + " has no route for the leg; they stay where they are.", this);
            }
            if (outward)
            {
                if (!moving) return;
                actor.Away = true;
                run.FamilyRecord.Ran.Add(id);
                if (competitionBodies.TryGetValue(id, out var body))
                {
                    if (body.contact != null) body.contact.Release();
                    ReleaseCompetitionStance(body.visual);
                }
                SetCompetitionShowFace(run, id, CompetitionApparatus.ShowFace.Running, Time.unscaledTime);
            }
            else actor.Homeward = true;
        }

        private void StopCompetitionShowRunner(string id, CompetitionShowActor actor)
        {
            if (!actor.Running) return;
            actor.Running = false;
            if (actor.IsPlayer) { if (player != null) player.SetActivityGait(competitionPlayerOwner, false); return; }
            var root = BodyFor(id);
            var visual = root != null ? root.GetComponent<CharacterPresentation>() : null;
            if (visual != null) visual.SetRunning(false);
        }

        /// <summary>A competitor's reaction: their stance let go while it plays, and taken up again after.</summary>
        private void ReactCompetitionShowEntrant(CompetitionShowRun run, string id, CharacterPresentation.Reaction kind, float now)
        {
            var actor = CompetitionShowActorFor(id);
            CharacterPresentation visual = null;
            if (competitionBodies.TryGetValue(id, out var body)) visual = body.visual;
            if (visual == null) { var root = BodyFor(id); if (root != null) visual = root.GetComponent<CharacterPresentation>(); }
            if (visual == null) return;
            if (body != null && body.contact != null) body.contact.SetShowPress(false);
            ReleaseCompetitionStance(visual);
            visual.React(kind);
            if (actor != null) actor.ReactUntil = now + 2f;
        }

        private void SetCompetitionShowPose(CompetitionShowRun run, string id, CharacterPresentation.Pose pose)
        {
            var actor = CompetitionShowActorFor(id);
            if (actor != null) actor.Pose = pose;
        }

        private void PressCompetitionShowInstrument(CompetitionShowRun run, string id, float from, float seconds)
        {
            var actor = CompetitionShowActorFor(id);
            if (actor == null) return;
            actor.PressFrom = from; actor.PressUntil = from + seconds;
        }

        /// <summary>Whether this competitor's hand is on their instrument now: a timed press, or the rhythm of work at a table.</summary>
        private static bool CompetitionShowPressing(CompetitionShowRun run, CompetitionShowActor actor, float now)
        {
            if (actor == null || run.Reduced) return false;
            if (now >= actor.PressFrom && now < actor.PressUntil) return true;
            // At work: a tap every .6 s, each competitor a little off the next one's step.
            return actor.Working && Mathf.Repeat(now + actor.Rank * .17f, .6f) < .32f;
        }

        /// <summary>A competitor's face on their instrument, drawn now and each frame after while they are at it.</summary>
        private void SetCompetitionShowFace(CompetitionShowRun run, string id, CompetitionApparatus.ShowFace face, float now)
        {
            var actor = CompetitionShowActorFor(id);
            if (actor == null) return;
            actor.Face = face; actor.FaceSince = now;
            if (competitionInstruments.TryGetValue(id, out var instrument) && instrument != null) instrument.ShowBeat(face, 0f, run.Reduced);
        }

        /// <summary>The instrument's beat, each frame after its resting faces (from SyncCompetitionInstrument).</summary>
        private void CompetitionShowFamilyFace(string id, CompetitionApparatus instrument)
        {
            var run = competitionShow;
            if (run == null || run.Plan.Family == CompetitionShowFamily.Hold || instrument == null) return;
            var actor = CompetitionShowActorFor(id);
            if (actor == null || actor.Face == CompetitionApparatus.ShowFace.Ready) return;
            instrument.ShowBeat(actor.Face, Time.unscaledTime - actor.FaceSince, run.Reduced);
        }

        /// <summary>
        /// A podium, table or course competitor at their station, each frame (from CompetitionStance):
        /// a pose the beat asks for - easy, confident, or the cooking loop's hand work at a table - and the
        /// hand on the instrument while the beat presses. A reaction plays out before the stance comes back.
        /// </summary>
        private void CompetitionShowFamilyStance(CharacterPresentation visual, bool arrived, CompetitionApparatus instrument)
        {
            if (visual == null || !arrived || instrument == null || !visual.IsStill || !visual.CanAct(CharacterPresentation.BodyActivity.Posing)) return;
            var run = competitionShow;
            string id = instrument.ActorId;
            var actor = CompetitionShowActorFor(id);
            float now = Time.unscaledTime;
            if (actor != null && now < actor.ReactUntil) return;
            var activity = CharacterPresentation.BodyActivity.Posing;
            if (actor != null && actor.Working && !run.Reduced && visual.CanAct(CharacterPresentation.BodyActivity.Cooking))
                activity = CharacterPresentation.BodyActivity.Cooking;
            var pose = actor != null ? actor.Pose : CharacterPresentation.Pose.AtEase;
            if (!competitionPoses.TryGetValue(visual, out var own))
            {
                if (visual.Activity != CharacterPresentation.BodyActivity.None) return;
                own = new CompetitionPose { previous = visual.HeldPose };
                competitionPoses.Add(visual, own);
            }
            else if (visual.Activity != own.writtenActivity) return;
            own.written = pose; own.writtenActivity = activity;
            visual.SetPose(pose); visual.SetActivity(activity);
            if (activity == CharacterPresentation.BodyActivity.Cooking && run.FamilyRecord != null) run.FamilyRecord.Worked.Add(id);
            var contact = own.contact;
            if (contact == null)
            {
                contact = visual.GetComponent<CompetitionInstrumentPose>();
                if (contact == null) contact = visual.gameObject.AddComponent<CompetitionInstrumentPose>();
                own.contact = contact;
            }
            if (id != null && competitionBodies.TryGetValue(id, out var body)) body.contact = contact;
            contact.SetFitGate(competitionFitGate);
            contact.Bind(instrument, null, CompetitionArenaPlaying(id), CompetitionArenaPaused, arrived);
            bool pressing = CompetitionShowPressing(run, actor, now);
            contact.SetShowPress(pressing);
            if (pressing && run.FamilyRecord != null) run.FamilyRecord.Pressed.Add(id);
        }

        /// <summary>Whatever ended the show: no runner keeps the run gait (the routes and their pace go with the stage).</summary>
        private void EndCompetitionShowFamily(CompetitionShowRun run)
        {
            foreach (var pair in run.Actors) StopCompetitionShowRunner(pair.Key, pair.Value);
        }

        // ------------------------------------------------------------ the arena's places

        /// <summary>
        /// A show's places beside the stations: slice 1's places at the side for a hold or a podium,
        /// whose competitors go out and walk aside; a turn mark in front of every post for a course;
        /// nothing for a table, where everyone stays at their tray.
        /// </summary>
        private void ReserveCompetitionShowMarks(CompetitionShowRun show, EpisodeState state, IReadOnlyCollection<string> staged,
            HouseRoomQuery rooms, NavMeshQueryFilter filter, Collider floor, IEnumerable<Vector3> places, List<Vector3> used)
        {
            switch (show.Plan.Family)
            {
                case CompetitionShowFamily.Table: return;
                // The ring tower's field stays at its places, as a table's does (EpisodeDirector.TowerShow.cs).
                case CompetitionShowFamily.Tower: return;
                case CompetitionShowFamily.Course: ReserveCompetitionCourseMarks(show, state, staged, rooms, filter, floor, used, places); return;
                default: ReserveCompetitionShowSideMarks(show, state, staged, rooms, filter, floor, places, used); return;
            }
        }

        /// <summary>
        /// Each post's turn mark (competition show edges, <see cref="CompetitionCourseLegs"/>): straight
        /// back toward the house as far as is clear (4.5 m down to 2 m), or leaning away from the yard's
        /// middle where a station stands in front of the post, so no leg runs through another station's
        /// equipment or body or crosses another runner's leg - in one row or two. Each mark is a whole
        /// standing reservation clear of every station, place and set piece, as the side marks are. A
        /// post with no clear mark runs no leg.
        /// </summary>
        private void ReserveCompetitionCourseMarks(CompetitionShowRun show, EpisodeState state, IReadOnlyCollection<string> staged,
            HouseRoomQuery rooms, NavMeshQueryFilter filter, Collider floor, List<Vector3> used, IEnumerable<Vector3> places = null)
        {
            var playerCapsule = player.GetComponent<CapsuleCollider>();
            // The player's own houseguest always runs a leg (competition show edges, after the built
            // house's run, where their post at the middle of the yard found none of the course's own
            // marks): theirs is tried every way and at the yard's own clear places too.
            var placeSpots = places != null ? places.Select(p => new CompetitionCourseLegs.Spot(p.x, p.z)).ToList() : null;
            var runners = new Dictionary<string, (Transform self, float radius, float height, Vector3 post)>();
            foreach (string id in show.Plan.Standings)
            {
                if (id == state.playerId)
                {
                    if (competitionPlayerStation == null) continue;
                    runners[id] = (player.transform, Mathf.Max(player.Agent.radius, playerCapsule != null ? playerCapsule.radius : 0), player.Agent.height,
                        competitionPlayerStationPosition);
                }
                else
                {
                    if (!staged.Contains(id) || !competitionBodies.TryGetValue(id, out var body) || body.root == null || body.capsule == null
                        || !competitionArenaActors.TryGetValue(id, out var anchor) || anchor == null) continue;
                    runners[id] = (body.root.transform, body.capsule.radius, body.capsule.height, anchor.Position);
                }
            }
            if (runners.Count == 0) return;
            // The yard's floor plan: every runner's station, and every other body standing on the deck.
            float runnerRadius = runners.Values.Max(runner => runner.radius);
            float reach = CompetitionStageFootprint.Reach(CompetitionApparatus.For(show.Definition, show.Plan.Category));
            var deck = floor.bounds;
            var legs = new CompetitionCourseLegs(deck.min.x, deck.max.x, deck.min.z, deck.max.z, runnerRadius + .33f, runnerRadius, deck.center.x);
            foreach (var pair in runners)
                legs.AddPost(pair.Key, pair.Value.post.x, pair.Value.post.z,
                    CompetitionCourseLegs.Box.Station(pair.Value.post.x, pair.Value.post.z, pair.Value.radius, reach, CompetitionStageFootprint.HalfWidth));
            foreach (var pair in competitionArenaActors)
            {
                if (runners.ContainsKey(pair.Key) || pair.Value == null) continue;
                bool contestant = show.Plan.Standings.Contains(pair.Key);
                var at = pair.Value.Position;
                legs.AddObstacle(contestant ? CompetitionCourseLegs.Box.Station(at.x, at.z, runnerRadius, reach, CompetitionStageFootprint.HalfWidth)
                    : CompetitionCourseLegs.Box.Body(at.x, at.z, runnerRadius));
                if (pair.Value.Seated) legs.AddObstacle(CompetitionCourseLegs.Box.Body(pair.Value.Approach.x, pair.Value.Approach.z, runnerRadius));
            }
            // Everybody on their way to a stage place (the field and the audience, the player too) stands
            // wherever the house left them while the marks are chosen; by the time a runner sets off each
            // stands at the place the plan above keeps clear (CompetitionCourseMarkPasses.MoversAside).
            var movers = new HashSet<Transform>(competitionRouteOwners);
            movers.Add(player.transform);
            foreach (string id in staged)
                if (competitionBodies.TryGetValue(id, out var body) && body.root != null) movers.Add(body.root.transform);
            foreach (string id in legs.Order)
            {
                var runner = runners[id];
                bool isPlayer = id == state.playerId;
                var tally = new CompetitionCourseMarkPasses.Tally();
                Vector3 position = default;
                CompetitionStageFootprint footprint = default;
                bool found = CompetitionCourseMarkPasses.TryChoose(legs, id, isPlayer,
                    () => isPlayer ? legs.EveryWayCandidates(id, placeSpots) : legs.Candidates(id),
                    (CompetitionCourseLegs.Spot spot, CompetitionCourseMarkPasses.Pass pass, out CompetitionCourseLegs.Spot taken) =>
                    {
                        taken = spot;
                        var wanted = new Vector3(spot.X, runner.post.y, spot.Z);
                        if (!rooms.TrySampleFloor(wanted, runner.radius, filter, .25f, out position, out var room) || room != "Yard")
                            return CompetitionCourseMarkPasses.Refusal.Floor;
                        // The floor's own point, not the one asked for: the leg is checked again from there.
                        taken = new CompetitionCourseLegs.Spot(position.x, position.z);
                        if (!legs.Clears(id, taken)) return CompetitionCourseMarkPasses.Refusal.Legs;
                        var at = position;
                        if (used.Any(p => (p - at).sqrMagnitude < 1.6f)) return CompetitionCourseMarkPasses.Refusal.Spacing;
                        footprint = CompetitionStageFootprint.Actor(position, runner.radius, runner.height);
                        if (CompetitionCourseMarkPasses.ReadsBodies(pass))
                        {
                            bool bodyClear = CompetitionCourseMarkPasses.CountsMovers(pass)
                                ? rooms.HasCapsuleClearance(position, runner.radius, runner.height, runner.self)
                                : CompetitionCourseMarkClearOfStill(position, runner.radius, runner.height, runner.self, floor, movers);
                            if (!bodyClear) return CompetitionCourseMarkPasses.Refusal.Body;
                            bool fits = CompetitionCourseMarkPasses.CountsMovers(pass)
                                ? FitsCompetitionFootprint(footprint, floor, runner.self)
                                : FitsCompetitionCourseMark(footprint, floor) && footprint.HasStaticClearance(gameObject.scene.GetPhysicsScene(), floor,
                                    competitionPlacementHits, movers, runner.self);
                            if (!fits) return CompetitionCourseMarkPasses.Refusal.Footprint;
                        }
                        else if (!FitsCompetitionCourseMark(footprint, floor)) return CompetitionCourseMarkPasses.Refusal.Footprint;
                        return CompetitionCourseMarkPasses.Refusal.None;
                    }, tally, out _, out var takenIn);
                if (found)
                {
                    var mark = HouseInteractionAnchor.Create(competitionArenaRoot.transform, "competition-course", "Yard", 200 + show.CourseMarks.Count,
                        position, CompetitionContestantFacing, false);
                    show.CourseMarks[id] = mark; used.Add(mark.Approach); competitionFootprints[id + ":course"] = footprint;
                    if (takenIn != CompetitionCourseMarkPasses.Pass.Strict)
                        Debug.Log("[Gamesim competition show] turn mark for " + (isPlayer ? "your houseguest " : "") + id + " at " + position.ToString("F2")
                            + " found in the " + takenIn + " pass (" + tally + ").", this);
                }
                else
                    Debug.Log("[Gamesim competition show] no clear turn mark for " + (isPlayer ? "your houseguest " : "") + id + " at " + runner.post.ToString("F2")
                        + ": " + tally + "; they run no leg.", this);
            }
        }

        /// <summary>A course mark's reservation on the deck, clear of every other reservation and of the scenery: the yard's plan, no collider read.</summary>
        private bool FitsCompetitionCourseMark(CompetitionStageFootprint footprint, Collider floor)
            => footprint.FitsOn(floor.bounds) && ClearsCompetitionNeighbours(footprint) && (competitionScenery == null || competitionScenery.Clears(footprint));

        /// <summary>
        /// Whether a body standing at <paramref name="feet"/> meets nothing but the yard's floor, itself
        /// and those on their way to a stage place (<paramref name="movers"/>): the capsule the house
        /// reads for a body (HouseRoomQuery.HasCapsuleClearance), with the movers set aside.
        /// </summary>
        private bool CompetitionCourseMarkClearOfStill(Vector3 feet, float radius, float height, Transform self, Collider floor, ISet<Transform> movers)
        {
            Physics.SyncTransforms();
            if (height < radius * 2f) return false;
            int count = gameObject.scene.GetPhysicsScene().OverlapCapsule(feet + Vector3.up * radius, feet + Vector3.up * (height - radius), radius,
                competitionPlacementHits, HouseLayers.Sight, QueryTriggerInteraction.Ignore);
            if (count == competitionPlacementHits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var hit = competitionPlacementHits[i];
                if (hit == null) return false;
                if (hit == floor) continue;
                bool moving = false;
                for (var at = hit.transform; at != null && !moving; at = at.parent) moving = at == self || movers.Contains(at);
                if (!moving) return false;
            }
            return true;
        }
    }
}
