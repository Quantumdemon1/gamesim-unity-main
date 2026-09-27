using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Your own moves (2026-09-27): your chip on the cast strip, or G, opens a card of them; a pose
    /// or a dance holds until you go, the house answers, and nothing is committed. Every wait is
    /// bounded in real seconds.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// Your chip is your moves: a card of them over it, with nothing committed and no panel
        /// opened. A pose holds until you walk off, a dance until a panel opens, and G opens and
        /// closes the card from the keyboard.
        /// </summary>
        [UnityTest]
        public IEnumerator Emotes_YourChipIsYourMovesAndTheyHoldUntilYouGo()
        {
            var visual = player.GetComponent<CharacterPresentation>();
            yield return WaitFor(() => visual.CanAct(CharacterPresentation.BodyActivity.Posing), 30f,
                "The player's body is built on the controller that poses.");
            visual.SetReducedMotion(false);
            string you = director.Snapshot.playerId;

            int revision = director.Snapshot.revision;
            director.PressCastChip(you);
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "Opening your moves commits nothing.");
            yield return null;
            Assert.That(director.EmoteMenuOpen, Is.True, "Your own chip opens your moves.");
            Assert.That(director.IsPanelOpen, Is.False, "The card is not a panel: the house goes on around it.");
            foreach (EpisodeDirector.Emote kind in System.Enum.GetValues(typeof(EpisodeDirector.Emote)))
                ButtonWithCaption(EpisodeHud.EmoteCaption(kind));

            yield return MakeTheMove(EpisodeHud.EmoteCaption(EpisodeDirector.Emote.Pose),
                () => visual.Activity == CharacterPresentation.BodyActivity.Posing, "You strike a pose.");
            Assert.That(visual.HeldPose, Is.EqualTo(CastMoves.PoseFor(visual.Frame, you)), "Your own pose, the one a body of your build strikes.");
            Assert.That(director.EmoteMenuOpen, Is.False, "The card goes as the move is made.");
            Assert.That(director.StatusMessage, Does.StartWith(EpisodeDirector.EmoteLine(EpisodeDirector.Emote.Pose)));
            yield return Frames(30);
            Assert.That(visual.Activity, Is.EqualTo(CharacterPresentation.BodyActivity.Posing), "The pose holds while you stand there.");

            Assert.That(NavMesh.SamplePosition(player.transform.position + player.transform.right * 2.5f, out var hit, 2f, NavMesh.AllAreas), Is.True);
            Assert.That(player.TryMoveTo(hit.position), Is.True, "You walk off.");
            yield return WaitFor(() => visual.Activity == CharacterPresentation.BodyActivity.None, 2f, "and the pose goes with the first step.");
            Assert.That(director.PlayerEmote, Is.Null);
            yield return WaitFor(() => player.HasArrived, 10f, "You get where you were going.");

            director.PressCastChip(you);
            yield return null;
            yield return MakeTheMove(EpisodeHud.EmoteCaption(EpisodeDirector.Emote.Samba),
                () => visual.Activity == CharacterPresentation.BodyActivity.Dancing, "You break into a samba.");
            Assert.That(visual.Dance, Is.EqualTo(CharacterPresentation.DanceStyle.Samba));
            director.OpenSettings();
            yield return Frames(2);
            Assert.That(visual.Activity, Is.EqualTo(CharacterPresentation.BodyActivity.None), "A panel stops the dance.");
            director.ClosePanels();
            yield return Frames(2);

            if (testKeyboard == null) testKeyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.G));
            yield return null;
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            yield return Frames(2);
            Assert.That(director.EmoteMenuOpen, Is.True, "G opens your moves.");
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.G));
            yield return null;
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            yield return Frames(2);
            Assert.That(director.EmoteMenuOpen, Is.False, "and closes them.");
        }

        /// <summary>
        /// Presses a move and waits for it to be made, asserting that neither the press nor the
        /// making commits anything. Read around each moment rather than across the whole wait: the
        /// house's walk-in watch commits on its own every four seconds of free time.
        /// </summary>
        private IEnumerator MakeTheMove(string caption, System.Func<bool> made, string what)
        {
            int before = director.Snapshot.revision;
            ButtonWithCaption(caption).onClick.Invoke();
            Assert.That(director.Snapshot.revision, Is.EqualTo(before), caption + ": choosing a move commits nothing.");
            float until = Time.realtimeSinceStartup + 3f;
            int last = director.Snapshot.revision;
            while (!made() && Time.realtimeSinceStartup < until)
            {
                last = director.Snapshot.revision;
                yield return null;
            }
            Assert.That(made(), Is.True, what);
            Assert.That(director.Snapshot.revision, Is.EqualTo(last), caption + ": making the move commits nothing.");
        }

        /// <summary>
        /// With motion reduced, a move is a moment: your pose, eased in and out, whatever you asked
        /// for - and the status line says what you did.
        /// </summary>
        [UnityTest]
        public IEnumerator Emotes_WithMotionReducedAMoveIsAMomentOfYourPose()
        {
            var visual = player.GetComponent<CharacterPresentation>();
            yield return WaitFor(() => visual.CanAct(CharacterPresentation.BodyActivity.Posing), 30f,
                "The player's body is built on the controller that poses.");
            visual.SetReducedMotion(true);
            director.PressCastChip(director.Snapshot.playerId);
            yield return null;
            ButtonWithCaption(EpisodeHud.EmoteCaption(EpisodeDirector.Emote.Samba)).onClick.Invoke();
            yield return WaitFor(() => visual.Activity == CharacterPresentation.BodyActivity.Posing, 3f,
                "The samba is a moment of your pose.");
            Assert.That(visual.HeldPose, Is.EqualTo(CastMoves.PoseFor(visual.Frame, director.Snapshot.playerId)));
            Assert.That(director.StatusMessage, Does.StartWith(EpisodeDirector.EmoteLine(EpisodeDirector.Emote.Samba)),
                "The status line says what you did.");
            float began = Time.realtimeSinceStartup;
            yield return WaitFor(() => visual.Activity == CharacterPresentation.BodyActivity.None, EpisodeDirector.MomentaryHold + 2f,
                "and it lets go of itself.");
            Assert.That(Time.realtimeSinceStartup - began, Is.GreaterThan(EpisodeDirector.MomentaryHold * .5f),
                "after a beat, not at once.");
            Assert.That(director.PlayerEmote, Is.Null);
        }

        /// <summary>
        /// The house answers a cheer: whoever is near enough and standing still turns to you and
        /// answers by what your own record says they are to you - a cheer back, a shrug or a look -
        /// and somebody across the house does not answer at all.
        /// </summary>
        [UnityTest]
        public IEnumerator Emotes_TheHouseNearbyAnswersACheer()
        {
            director.SuspendNpcAutonomyForDiagnostics();
            var visual = player.GetComponent<CharacterPresentation>();
            yield return WaitFor(() => visual.CanAct(CharacterPresentation.BodyActivity.Posing), 30f,
                "The player's body is built on the controller that poses.");
            var state = director.Snapshot;
            var npcs = SceneComponents<HouseNpc>().Where(npc => npc.gameObject.activeInHierarchy).ToArray();
            var maya = npcs.Single(npc => npc.Id == ContentCatalog.MayaId);
            var mayaVisual = maya.GetComponent<CharacterPresentation>();
            yield return WaitFor(() => mayaVisual.IsStill, 8f, "Maya is standing still.");
            Assert.That(NavMesh.SamplePosition(maya.transform.position + maya.transform.forward * 1.4f, out var beside, 1.5f, NavMesh.AllAreas), Is.True);
            Assert.That(player.TryWarpTo(beside.position), Is.True, "You stand beside Maya.");
            yield return Frames(3);
            visual.SetReducedMotion(false);
            foreach (var npc in npcs) npc.GetComponent<CharacterPresentation>().SetReducedMotion(false);
            var far = npcs.Where(npc => Vector3.Distance(npc.transform.position, player.transform.position) > EpisodeDirector.AnswerReach + 1f)
                .Select(npc => npc.Id).ToArray();

            director.MakeEmote(EpisodeDirector.Emote.Cheer);
            yield return WaitFor(() => director.LastAnswers.Any(answer => answer.id == maya.Id), 3f, "Maya is near enough to answer.");
            var expected = EpisodeDirector.AnswerTo(EpisodeDirector.Emote.Cheer, RelationshipWeb.KindOf(state, maya.Id));
            Assert.That(director.LastAnswers.First(answer => answer.id == maya.Id).answer, Is.EqualTo(expected),
                "Maya answers by what your own record says she is to you (" + RelationshipWeb.KindOf(state, maya.Id) + ").");
            yield return WaitFor(() => mayaVisual.LookTarget == player.transform, 3f, "Maya turns to look at you.");
            if (expected == EpisodeDirector.Answer.Cheer)
                Assert.That(mayaVisual.LastGesture == CharacterPresentation.Gesture.Cheer
                    || mayaVisual.LastReaction == CharacterPresentation.Reaction.Cheered, Is.True, "and cheers you on.");
            if (expected == EpisodeDirector.Answer.Shrug)
                Assert.That(mayaVisual.LastGesture, Is.EqualTo(CharacterPresentation.Gesture.Shrug), "and shrugs.");
            if (expected == EpisodeDirector.Answer.Cheer || expected == EpisodeDirector.Answer.Shrug)
                Assert.That(director.StatusMessage, Does.Contain("Maya"), "The status line says who answered.");
            foreach (var id in far)
                Assert.That(director.LastAnswers.Any(answer => answer.id == id), Is.False, id + " is across the house and does not answer.");
        }
    }
}
