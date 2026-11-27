using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The sound a commit makes (CommitSound, behind EpisodeDirector.CommitCue). The first case runs
    /// in the Unity-free suite too; the table over the ceremony strip's kinds reads the strip's own
    /// constants and the house's cues, so only Unity compiles it.
    /// </summary>
    public sealed class CommitSoundTests
    {
        /// <summary>
        /// The final Head of Household sending a finalist to the jury is the season's last eviction,
        /// and it sounds like one. Its kind matched nothing, and its card has no reveal to make a
        /// sound of its own, so it clicked like a button. It ranks with the weekly eviction: under
        /// the winner, over the block, the veto and a competition's result.
        /// </summary>
        [Test]
        public void TheFinalEvictionSoundsLikeAnEviction()
        {
            Assert.That(CommitSound.CueName(new[] { "final-eviction" }), Is.EqualTo("Eviction"),
                "A finalist sent to the jury is an eviction, not a button's click.");
            Assert.That(CommitSound.CueName(new[] { "promise-outcome", "deal-outcome", "final-eviction", "alliance" }), Is.EqualTo("Eviction"),
                "The promises, deals and alliances it settles do not take its sound.");
            Assert.That(CommitSound.CueName(new[] { "competition", "final-eviction" }), Is.EqualTo("Eviction"), "Over a competition's result,");
            Assert.That(CommitSound.CueName(new[] { "final-eviction", "veto", "nomination" }), Is.EqualTo("Eviction"), "over the veto and the block,");
            Assert.That(CommitSound.CueName(new[] { "final-eviction", "winner" }), Is.EqualTo("Finale"), "and under the winner.");
        }

        /// <summary>
        /// A staged veto meeting's commit only summons the house (AU-07): the veto sounds with the page
        /// that shows the decision, not while the house is still finding its seats. On the HUD the
        /// commit sounds the veto as it always did; a reveal makes its own sounds; and a bigger beat in
        /// a staged meeting's commit keeps its sound there.
        /// </summary>
        [Test]
        public void AStagedVetoMeetingLeavesTheVetosSoundToItsDecision()
        {
            Assert.That(CommitSound.SoundsAtTheCommit("Veto", revealed: true, vetoStaged: true), Is.False,
                "Staged, the veto waits for the decision's page.");
            Assert.That(CommitSound.SoundsAtTheCommit("Veto", revealed: false, vetoStaged: false), Is.True,
                "On the HUD's card, the commit sounds the veto, as it always did.");
            Assert.That(CommitSound.SoundsAtTheCommit("Nomination", revealed: true, vetoStaged: true), Is.True,
                "A bigger beat in the same commit keeps its sound.");
            Assert.That(CommitSound.SoundsAtTheCommit("Nomination", revealed: true, vetoStaged: false), Is.False,
                "A staged key ceremony makes its own sounds.");
            Assert.That(CommitSound.SoundsAtTheCommit("Button", revealed: false, vetoStaged: false), Is.True, "Every other commit sounds.");
        }

#if UNITY_5_3_OR_NEWER
        /// <summary>
        /// Every ceremony a commit can append makes a sound of its own, and none falls through to the
        /// button's click unless this table says it is quiet on purpose. The kinds are read from the
        /// strip's own constants, so a new kind the strip accepts has to come into this table; and
        /// every name the choice gives is one of the house's cues.
        /// </summary>
        [Test]
        public void EveryCeremonyACommitAppendsHasASound()
        {
            var sounds = new Dictionary<string, HouseAudio.Cue>
            {
                { CeremonySting.NominationKind, HouseAudio.Cue.Nomination },
                { CeremonySting.VetoKind, HouseAudio.Cue.Veto },
                { CeremonySting.EvictionKind, HouseAudio.Cue.Eviction },
                { CeremonySting.WinnerKind, HouseAudio.Cue.Finale },
                { CeremonySting.FinalEvictionKind, HouseAudio.Cue.Eviction },
            };
            // Quiet on purpose: the engine never logs a walk out, so no commit carries one. The
            // director plays its strip itself as the evicted leave (EpisodeDirector.WalkOut.cs).
            var quiet = new[] { CeremonySting.WalkOutKind };
            var accepted = typeof(CeremonySting)
                .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(field => field.IsLiteral && field.FieldType == typeof(string))
                .Select(field => (string)field.GetRawConstantValue())
                .Where(CeremonySting.IsCeremony)
                .ToList();
            Assert.That(accepted, Is.EquivalentTo(sounds.Keys.Concat(quiet)),
                "Every kind the strip accepts is in this table, with its sound or as quiet on purpose.");
            foreach (var pair in sounds)
            {
                Assert.That(pair.Value, Is.Not.EqualTo(HouseAudio.Cue.Button), pair.Key + " has a sound of its own.");
                Assert.That(CommitSound.CueName(new[] { pair.Key }), Is.EqualTo(pair.Value.ToString()), pair.Key + " makes its own sound.");
            }
            foreach (var kind in quiet)
                Assert.That(CommitSound.CueName(new[] { kind }), Is.EqualTo(HouseAudio.Cue.Button.ToString()), kind + " is quiet on purpose.");

            var names = new[] { "winner", "eviction", "final-eviction", "nomination", "veto", "competition", "expulsion", "blowup", "social" }
                .Select(kind => CommitSound.CueName(new[] { kind })).Concat(new[] { CommitSound.CueName(null) }).Distinct();
            foreach (var name in names)
                Assert.That(Enum.IsDefined(typeof(HouseAudio.Cue), name), Is.True, name + " is one of the house's cues.");
        }
#endif
    }
}
