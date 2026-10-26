using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The standalone season walk's veto meeting step (PortVerification.PerformSeasonDecision), run
    /// on legal histories through the walk's own station step and its own presses. The release
    /// candidate's walk (rc2, seed 2959573182) reached the final four with the player Head of
    /// Household and holder of the veto, and pressed the first replacement candidate's name - a
    /// control the meeting never draws for a veto locked at four - failing "Expected a reachable
    /// actual button: Riley Johnson (found 0)". The screen was right; the walk did not know the lock.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest, Timeout(180000)]
        public IEnumerator PortSeasonWalk_KeepsTheBlockWhenTheVetoIsLockedAtFour()
        {
            yield return InstallDiaryFixture(state => state.phase == EpisodePhase.VetoMeeting && !state.vetoResolved
                && state.hohId == state.playerId && state.vetoHolderId == state.playerId
                && EpisodeEngine.VetoIsLockedAtFinalFour(state) && EpisodeEngine.ReplacementCandidates(state).Any(),
                "the player Head of Household and holder of a veto locked at the final four");
            HoldTheHouseForTheFixture();
            player.Agent.speed = 25f; player.Agent.acceleration = 100f;
            var runner = BindPortConversationVerifier(out var owner);
            try
            {
                yield return PortConversationRoutine(runner, "OpenSeasonStation");
                var before = director.Snapshot;
                // What the screen offers a locked holder: the lock's line and the way to keep the
                // block, and nothing that uses the veto - no save, no pick, no replacement's name.
                var words = PanelWords(ActiveRect("Episode panel"));
                Assert.That(words, Has.Some.Contains(EpisodeDirector.VetoLockedLine));
                Assert.That(words, Has.Some.Contains("at the final four these nominations must stay."));
                Assert.That(words, Has.None.Contains("you name the replacement too"));
                Assert.That(MeetingButton("Do not use the veto"), Is.Not.Null);
                foreach (var candidate in EpisodeEngine.ReplacementCandidates(before))
                    Assert.That(MeetingButton(candidate.name), Is.Null, "A locked veto names no replacement: " + candidate.name);
                foreach (var nominee in before.nominees)
                    Assert.That(MeetingButton(EpisodeDirector.VetoSavePickCaption(before.Find(nominee).name)), Is.Null,
                        "A locked veto picks nobody to save.");

                yield return PortConversationRoutine(runner, "PerformSeasonDecision", before, false);
                var after = director.Snapshot;
                Assert.That(after.revision, Is.EqualTo(before.revision + 1), "The walk's step commits exactly one decision.");
                Assert.That(after.vetoResolved, Is.True);
                CollectionAssert.AreEqual(before.nominees, after.nominees, "A locked veto keeps the block.");
                Assert.That(after.events.Last(entry => entry.kind == "veto").text, Does.Contain("Nominations stand."));
                Assert.That(EpisodeValidation.TryValidate(after, out var reason), Is.True, reason);
            }
            finally { UnityEngine.Object.Destroy(owner); }
        }

        /// <summary>
        /// The final four's other meeting for a Head of Household: a nominee holds the veto and saves
        /// themself, so the player names the only replacement. The walk presses that name, and the
        /// screen draws it - the meeting at four is not short of the control the walk needs when the
        /// veto can be used.
        /// </summary>
        [UnityTest, Timeout(180000)]
        public IEnumerator PortSeasonWalk_NamesTheOnlyReplacementAfterANomineesSaveAtFour()
        {
            yield return InstallDiaryFixture(state => state.phase == EpisodePhase.VetoMeeting && !state.vetoResolved
                && state.hohId == state.playerId && state.vetoHolderId != state.playerId && state.Active.Count() == 4
                && EpisodeEngine.NpcVetoSave(state) != null,
                "the player Head of Household at the final four after a nominee's save");
            HoldTheHouseForTheFixture();
            player.Agent.speed = 25f; player.Agent.acceleration = 100f;
            var runner = BindPortConversationVerifier(out var owner);
            try
            {
                yield return PortConversationRoutine(runner, "OpenSeasonStation");
                var before = director.Snapshot;
                string saved = EpisodeEngine.NpcVetoSave(before);
                var replacement = EpisodeEngine.ReplacementCandidates(before).Single();
                Assert.That(MeetingButton(replacement.name), Is.Not.Null, "The only replacement is offered by name.");

                yield return PortConversationRoutine(runner, "PerformSeasonDecision", before, false);
                var after = director.Snapshot;
                Assert.That(after.revision, Is.EqualTo(before.revision + 1), "The walk's step commits exactly one decision.");
                Assert.That(after.vetoResolved, Is.True);
                Assert.That(after.nominees, Does.Contain(replacement.id), "The name pressed goes up.");
                Assert.That(after.nominees, Does.Not.Contain(saved), "The holder's save comes down.");
                Assert.That(EpisodeValidation.TryValidate(after, out var reason), Is.True, reason);
            }
            finally { UnityEngine.Object.Destroy(owner); }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortSeasonWalk_LockedVetoGuardRejectsAnIllegalChoiceBehindResults()
        {
            yield return InstallDiaryFixture(state => state.phase == EpisodePhase.VetoMeeting && !state.vetoResolved
                && state.hohId == state.playerId && state.vetoHolderId == state.playerId
                && EpisodeEngine.VetoIsLockedAtFinalFour(state) && EpisodeEngine.ReplacementCandidates(state).Any(),
                "a legal locked veto meeting for the overlay guard");
            HoldTheHouseForTheFixture();
            player.Agent.speed = 25f; player.Agent.acceleration = 100f;
            var runner = BindPortConversationVerifier(out var owner);
            GameObject injected = null;
            CompetitionResult resultCard = null;
            var stack = new Stack<IEnumerator>();
            try
            {
                yield return PortConversationRoutine(runner, "OpenSeasonStation");
                var before = director.Snapshot;
                var primary = System.IO.File.ReadAllBytes(director.SavePath);
                var backup = PortConversationBackupBytes();
                string illegalCaption = EpisodeEngine.ReplacementCandidates(before).First().name;
                // Fault only the displayed controls, never the authoritative history or command.
                var keep = MeetingButton("Do not use the veto");
                var extra = UnityEngine.Object.Instantiate(keep, keep.transform.parent);
                injected = extra.gameObject;
                extra.name = illegalCaption;
                extra.onClick = new Button.ButtonClickedEvent();
                foreach (var label in extra.GetComponentsInChildren<TMP_Text>()) label.text = illegalCaption;
                Assert.That(MeetingButton(illegalCaption), Is.Not.Null);
                resultCard = SceneComponents<CompetitionResult>().Single();
                Assert.That(resultCard.Play("Veto", "Guard fixture", before.week,
                    new[] { new CompetitionResult.Standing(before.Find(before.playerId).name, 1, true, true, null) }, true), Is.True);
                Assert.That(resultCard.IsPlaying, Is.True);
                // The card's time starts on the frame after the one that played it, and so does the
                // walk's wait to press its Continue, as it does after the walk's own presses.
                yield return null;

                stack.Push(PortConversationRoutine(runner, "PerformSeasonDecision", before, false));
                System.Exception failure = null;
                for (int step = 0; stack.Count > 0 && step < 5000 && failure == null; step++)
                {
                    var top = stack.Peek();
                    bool moved = false;
                    try { moved = top.MoveNext(); }
                    catch (System.Exception error) { failure = error; }
                    if (failure != null) break;
                    if (!moved) { (stack.Pop() as System.IDisposable)?.Dispose(); continue; }
                    if (top.Current is IEnumerator child) stack.Push(child);
                    else yield return top.Current;
                }
                Assert.That(failure, Is.TypeOf<System.InvalidOperationException>());
                Assert.That(failure.Message, Does.Contain("must offer no way to use it"));
                Assert.That(resultCard.IsPlaying, Is.False, "The guard must inspect the meeting after dismissing the result.");
                Assert.That(MeetingButton(illegalCaption), Is.Not.Null, "The offending control was actually exposed.");
                AssertPortConversationUnchanged(before, primary, backup);
            }
            finally
            {
                while (stack.Count > 0) (stack.Pop() as System.IDisposable)?.Dispose();
                if (resultCard != null) resultCard.Cancel();
                if (injected != null) UnityEngine.Object.Destroy(injected);
                UnityEngine.Object.Destroy(owner);
            }
        }

        /// <summary>A live, pressable control in the episode panel carrying these words, or null: the meeting's own, not the house's chrome.</summary>
        private Button MeetingButton(string caption)
        {
            var panel = ActiveRect("Episode panel");
            Assert.That(panel, Is.Not.Null, "The episode panel is open.");
            return panel.GetComponentsInChildren<Button>().FirstOrDefault(button => button.IsActive() && button.IsInteractable()
                && button.GetComponentsInChildren<TMP_Text>().Any(label => label.text == caption));
        }
    }
}
