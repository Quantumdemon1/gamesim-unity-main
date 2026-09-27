using System.Collections.Generic;
using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>
    /// The player's own moves (brainstorm, 2026-09-27): your chip on the cast strip, or G, opens a
    /// small card of them - a cheer, a shrug, a celebration, your own pose and three dances - and
    /// your houseguest stops and makes it. The web's PlayerEmoteMenu offers the same kind of thing
    /// on selecting your own avatar.
    ///
    /// <para>Presentation only, as the web's is: nothing is committed, nothing is saved, and nobody's
    /// opinion of you moves. A pose or a dance holds until you move; the rest play once. With motion
    /// reduced, every move is a moment instead - your pose, eased in and out - and the status line
    /// says what you did.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>A move the player can make. Appended to, never reordered: the card lists them in this order.</summary>
        public enum Emote { Cheer, Shrug, Celebrate, Pose, Samba, HipHop, Wave }

        private bool emoteMenuOpen;
        /// <summary>Whether the card of your moves is up over your chip.</summary>
        public bool EmoteMenuOpen => emoteMenuOpen;

        private Emote? emote;
        private bool emotePending;
        private float emoteUntil, emoteWaitUntil;
        private CharacterPresentation.BodyActivity emoteActivity;

        /// <summary>The move the player is making or about to make, or null.</summary>
        public Emote? PlayerEmote => emote;

        /// <summary>How long a move waits for a walking body to come to a stop before it is made anyway.</summary>
        private const float EmoteStopWait = 1f;
        /// <summary>A move made as a moment - with motion reduced - holds the pose this long between its ease in and its ease out.</summary>
        public const float MomentaryHold = 1.4f;

        private CharacterPresentation PlayerVisual => player != null ? player.GetComponent<CharacterPresentation>() : null;

        public void ToggleEmoteMenu()
        {
            castMenuFor = null;
            emoteMenuOpen = !emoteMenuOpen && !IsPanelOpen;
            Render();
        }

        public void CloseEmoteMenu()
        {
            if (!emoteMenuOpen) return;
            emoteMenuOpen = false;
            Render();
        }

        /// <summary>
        /// Whether this body can make the move: every one on a body cast from the mocap, and on a
        /// body that cannot pose or dance only the cheer, which every cast can act out.
        /// </summary>
        public bool CanEmote(Emote kind)
        {
            var visual = PlayerVisual;
            if (visual == null) return false;
            switch (kind)
            {
                case Emote.Cheer: return true;
                case Emote.Shrug: return visual.Supports(CharacterPresentation.Gesture.Shrug);
                case Emote.Celebrate: return visual.Supports(CharacterPresentation.Gesture.Celebrate);
                case Emote.Pose: return visual.CanAct(CharacterPresentation.BodyActivity.Posing);
                default: return visual.CanDance(StyleOf(kind));
            }
        }

        private static CharacterPresentation.DanceStyle StyleOf(Emote kind) =>
            kind == Emote.Samba ? CharacterPresentation.DanceStyle.Samba
            : kind == Emote.HipHop ? CharacterPresentation.DanceStyle.HipHop
            : kind == Emote.Wave ? CharacterPresentation.DanceStyle.Wave
            : CharacterPresentation.DanceStyle.House;

        /// <summary>What the status line says you did.</summary>
        public static string EmoteLine(Emote kind)
        {
            switch (kind)
            {
                case Emote.Cheer: return "You cheer.";
                case Emote.Shrug: return "You shrug.";
                case Emote.Celebrate: return "You celebrate.";
                case Emote.Pose: return "You strike a pose.";
                case Emote.Samba: return "You break into a samba.";
                case Emote.HipHop: return "You break into some hip-hop.";
                default: return "You break into a wave dance.";
            }
        }

        /// <summary>Makes a move: stops you where you stand, and makes it once you have stopped.</summary>
        public void MakeEmote(Emote kind)
        {
            emoteMenuOpen = false;
            EndEmote(false);
            if (player == null || PlayerVisual == null || !player.InputEnabled || IsPanelOpen || !CanEmote(kind)) { Render(); return; }
            player.StopHere();
            emote = kind;
            emotePending = true;
            emoteWaitUntil = Time.unscaledTime + EmoteStopWait;
            message = EmoteLine(kind);
            Render();
        }

        /// <summary>Lets go of a held pose or dance.</summary>
        public void Relax()
        {
            EndEmote(false);
            Render();
        }

        /// <summary>
        /// Holds the move while nothing else wants the player, and ends it the moment something
        /// does: a walk, a panel, an activity, a challenge, a ceremony card, the show.
        /// </summary>
        private void TickEmote()
        {
            if (emote == null) return;
            var visual = PlayerVisual;
            if (visual == null || player == null) { EndEmote(false); return; }
            // Something else took the player - an activity, the diary chair - and owns the body now.
            if (player.HasActivityOwner) { EndEmote(true); return; }
            if (!player.InputEnabled || IsPanelOpen || challengeActive || CeremonyOverlays.OnScreen || OpeningOwnsHouse || Walking())
            { EndEmote(false); return; }
            if (emotePending)
            {
                if (!visual.IsStill && Time.unscaledTime < emoteWaitUntil) return;
                Make(visual, emote.Value);
                return;
            }
            if (Time.unscaledTime >= emoteUntil) EndEmote(false);
        }

        /// <summary>A walk chosen since the move began: StopHere cleared the path, so any path is a new one.</summary>
        private bool Walking()
        {
            var agent = player.Agent;
            return agent != null && agent.enabled && agent.isOnNavMesh && (agent.hasPath || agent.pathPending);
        }

        private void Make(CharacterPresentation visual, Emote kind)
        {
            emotePending = false;
            // The house answers first, from where it stands: who is near enough is decided now.
            string answered = QueueAnswers(kind);
            if (answered.Length > 0) { message = EmoteLine(kind) + " " + answered; Render(); }
            if (visual.ReducedMotion)
            {
                // A moment, not a motion: your pose, eased in by the controller and out after a beat.
                if (visual.CanAct(CharacterPresentation.BodyActivity.Posing))
                {
                    visual.SetPose(CastMoves.PoseFor(visual.Frame, projected?.playerId));
                    visual.SetActivity(CharacterPresentation.BodyActivity.Posing);
                    emoteActivity = CharacterPresentation.BodyActivity.Posing;
                    emoteUntil = Time.unscaledTime + MomentaryHold;
                }
                else emote = null;
                return;
            }
            switch (kind)
            {
                case Emote.Cheer:
                    // Every cast can cheer: the mocap body's own gesture, or the ceremony beat.
                    if (!visual.MakeGesture(CharacterPresentation.Gesture.Cheer)) visual.React(CharacterPresentation.Reaction.Cheered);
                    emote = null;
                    return;
                case Emote.Shrug: visual.MakeGesture(CharacterPresentation.Gesture.Shrug); emote = null; return;
                case Emote.Celebrate: visual.MakeGesture(CharacterPresentation.Gesture.Celebrate); emote = null; return;
                case Emote.Pose:
                    visual.SetPose(CastMoves.PoseFor(visual.Frame, projected?.playerId));
                    visual.SetActivity(CharacterPresentation.BodyActivity.Posing);
                    emoteActivity = CharacterPresentation.BodyActivity.Posing;
                    break;
                default:
                    visual.SetDanceStyle(StyleOf(kind));
                    visual.SetActivity(CharacterPresentation.BodyActivity.Dancing);
                    emoteActivity = CharacterPresentation.BodyActivity.Dancing;
                    break;
            }
            emoteUntil = float.PositiveInfinity;
        }

        // ------------------------------------------------------------------ the house answers

        /// <summary>How the house answers a move: a cheer back, a shrug, or a look.</summary>
        public enum Answer { None, Cheer, Shrug, Look }

        /// <summary>How near a houseguest has to be to answer a move, in metres.</summary>
        public const float AnswerReach = 5f;

        /// <summary>How many answer at most: a whole room cheering at once would be a flash mob.</summary>
        public const int MostAnswers = 3;

        private readonly List<(HouseNpc npc, float at, Answer answer)> answers = new List<(HouseNpc, float, Answer)>();

        /// <summary>Who answered the last move, and how, nearest first: for the tests.</summary>
        public IReadOnlyList<(string id, Answer answer)> LastAnswers => lastAnswers;
        private readonly List<(string id, Answer answer)> lastAnswers = new List<(string, Answer)>();

        /// <summary>
        /// How a houseguest answers a move, by what your own record says they are to you - the
        /// reading the cast strip and the notebook show, never what they privately think of you. A
        /// friend or an ally cheers a cheer, a celebration or a dance back; somebody you distrust or
        /// count a rival shrugs it off; anyone else turns to look. A pose is looked at, and a shrug
        /// is nobody's business.
        /// </summary>
        public static Answer AnswerTo(Emote move, RelationshipWeb.Kind kind)
        {
            if (move == Emote.Shrug) return Answer.None;
            if (move == Emote.Pose) return Answer.Look;
            switch (kind)
            {
                case RelationshipWeb.Kind.Friendship:
                case RelationshipWeb.Kind.Alliance: return Answer.Cheer;
                case RelationshipWeb.Kind.Rivalry:
                case RelationshipWeb.Kind.Distrust: return Answer.Shrug;
                default: return Answer.Look;
            }
        }

        /// <summary>
        /// Queues the answers of whoever is near enough and standing still - somebody walking past
        /// does not stop to cheer - a beat apart, nearest first. Returns what the status line adds.
        /// </summary>
        private string QueueAnswers(Emote move)
        {
            answers.Clear();
            lastAnswers.Clear();
            var state = projected;
            if (state == null || housemates == null || player == null) return "";
            var here = player.transform.position;
            var near = housemates.Where(npc => npc != null && npc.gameObject.activeInHierarchy
                    && state.Find(npc.Id)?.status == ContestantStatus.Active
                    && Vector3.Distance(npc.transform.position, here) <= AnswerReach && StandingStill(npc))
                .OrderBy(npc => Vector3.Distance(npc.transform.position, here)).Take(MostAnswers).ToArray();
            var cheered = new List<string>();
            var shrugged = new List<string>();
            float at = Time.unscaledTime + .5f;
            foreach (var npc in near)
            {
                var answer = AnswerTo(move, RelationshipWeb.KindOf(state, npc.Id));
                if (answer == Answer.None) continue;
                answers.Add((npc, at, answer));
                lastAnswers.Add((npc.Id, answer));
                at += .35f;
                string first = (state.Find(npc.Id)?.name ?? npc.DisplayName).Split(' ')[0];
                if (answer == Answer.Cheer) cheered.Add(first);
                else if (answer == Answer.Shrug) shrugged.Add(first);
            }
            var line = new List<string>();
            if (cheered.Count > 0) line.Add(Names(cheered) + (cheered.Count == 1 ? " cheers" : " cheer") + " you on.");
            if (shrugged.Count > 0) line.Add(Names(shrugged) + (shrugged.Count == 1 ? " shrugs." : " shrug."));
            return string.Join(" ", line);
        }

        private static bool StandingStill(HouseNpc npc)
        {
            // An explicit comparison: the editor hands back a stand-in for a missing component that
            // a pattern match or ?? takes for a real one.
            var visual = npc.GetComponent<CharacterPresentation>();
            return visual != null && visual.IsStill;
        }

        private static string Names(List<string> names) =>
            names.Count == 1 ? names[0] : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[names.Count - 1];

        /// <summary>Plays each answer when its beat comes; a panel opening drops the rest.</summary>
        private void TickAnswers()
        {
            if (answers.Count == 0) return;
            if (IsPanelOpen || OpeningOwnsHouse || player == null) { answers.Clear(); return; }
            for (int i = answers.Count - 1; i >= 0; i--)
            {
                var (npc, at, answer) = answers[i];
                if (Time.unscaledTime < at) continue;
                answers.RemoveAt(i);
                var visual = npc != null ? npc.GetComponent<CharacterPresentation>() : null;
                if (visual == null) continue;
                visual.LookAt(player.transform, 2.5f);
                if (answer == Answer.Cheer)
                {
                    // A sitter claps from the chair; anybody else cheers, and a body with no gesture
                    // of its own cheers the ceremony's cheer.
                    if (visual.IsSeated || !visual.MakeGesture(CharacterPresentation.Gesture.Cheer)) visual.React(CharacterPresentation.Reaction.Cheered);
                }
                else if (answer == Answer.Shrug) visual.MakeGesture(CharacterPresentation.Gesture.Shrug);
            }
        }

        /// <summary>
        /// Ends the move. The pose or the dance is let go only if it is still the move's and nothing
        /// else has the player: an activity that took the body sets its own cue, and clearing it
        /// here would drop its first frame.
        /// </summary>
        private void EndEmote(bool taken)
        {
            var visual = PlayerVisual;
            if (!taken && visual != null && emoteActivity != CharacterPresentation.BodyActivity.None && visual.Activity == emoteActivity)
                visual.SetActivity(CharacterPresentation.BodyActivity.None);
            emote = null;
            emotePending = false;
            emoteActivity = CharacterPresentation.BodyActivity.None;
        }
    }
}
