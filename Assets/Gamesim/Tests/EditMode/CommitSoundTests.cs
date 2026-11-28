using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
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

        /// <summary>
        /// A conversation that warms or cools a relationship sounds like it (AU-09), where every
        /// conversation's commit clicked like a button: with no bigger beat in the commit, trust gained
        /// is RelationshipUp and trust lost RelationshipDown, counted as the trust chip counts it - so a
        /// change the chip calls "No change in trust" keeps the click - and every bigger beat still wins.
        /// Both wait for the chip; nothing else does.
        /// </summary>
        [Test]
        public void AConversationThatMovesTrustSoundsLikeIt_UnderEveryBiggerBeat()
        {
            Assert.That(CommitSound.CueName(new[] { "conversation", "memory" }, 4.4), Is.EqualTo(CommitSound.RelationshipUp),
                "Trust gained sounds warmer than a click.");
            Assert.That(CommitSound.CueName(new[] { "conversation" }, -3.0), Is.EqualTo(CommitSound.RelationshipDown), "Trust lost sounds cooler.");
            Assert.That(CommitSound.CueName(new[] { "conversation" }, 0.0), Is.EqualTo("Button"), "No change keeps the click.");
            Assert.That(CommitSound.CueName(new[] { "conversation" }, double.NaN), Is.EqualTo("Button"), "Nor does a change that is not a number.");

            // The chip rounds as Unity's Mathf.RoundToInt rounds a float, half to even: what it calls no
            // change sounds like none.
            Assert.That(CommitSound.CueName(new[] { "conversation" }, 0.5), Is.EqualTo("Button"), "Half a point reads 'No change in trust'.");
            Assert.That(CommitSound.CueName(new[] { "conversation" }, -0.5), Is.EqualTo("Button"));
            Assert.That(CommitSound.CueName(new[] { "conversation" }, 0.6), Is.EqualTo(CommitSound.RelationshipUp));
            Assert.That(CommitSound.CueName(new[] { "conversation" }, -1.5), Is.EqualTo(CommitSound.RelationshipDown));
            Assert.That(new[] { 2.5, 1.5, -2.5, 3.49 }.Select(CommitSound.TrustChipSteps), Is.EqualTo(new[] { 2, 2, -2, 3 }),
                "The chip's own count of the change.");

            var bigger = new Dictionary<string, string>
            {
                { "winner", "Finale" }, { "eviction", "Eviction" }, { "final-eviction", "Eviction" }, { "nomination", "Nomination" },
                { "veto", "Veto" }, { "competition", "CompetitionWin" }, { StoryLog.Expulsion, "Eviction" },
            };
            foreach (var pair in bigger)
                foreach (var delta in new[] { 6.0, -6.0 })
                    Assert.That(CommitSound.CueName(new[] { "conversation", pair.Key }, delta), Is.EqualTo(pair.Value),
                        pair.Key + " still wins over a change of " + delta + " in trust.");
            foreach (var kinds in new[] { null, new[] { "conversation" }, new[] { "veto", "deal-outcome" }, new[] { "eviction", "winner" } })
                Assert.That(CommitSound.CueName(kinds, 0.0), Is.EqualTo(CommitSound.CueName(kinds)), "With no change, a commit sounds as it always did.");

            Assert.That(CommitSound.SoundsWithTheTrustChip(CommitSound.RelationshipUp) && CommitSound.SoundsWithTheTrustChip(CommitSound.RelationshipDown),
                Is.True, "Both trust sounds wait for the chip that says so.");
            foreach (var cue in new[] { "Button", "Veto", "CompetitionWin", "SocialUp", null })
                Assert.That(CommitSound.SoundsWithTheTrustChip(cue), Is.False, (cue ?? "null") + " plays at the commit, as it did.");
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
