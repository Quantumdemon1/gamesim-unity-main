using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The sound a commit makes, by the name of the house's cue (<c>HouseAudio.Cue</c>, by name, as
    /// <see cref="SoundPictures"/> keeps it, since this file is Unity-free and the Unity-free suite
    /// compiles it). <c>EpisodeDirector.CommitCue</c> turns the name into the cue it plays.
    /// </summary>
    public static class CommitSound
    {
        /// <summary>
        /// The cue's name, chosen from every line the commit appended rather than its last one. The
        /// biggest beat wins: an eviction is followed by its votes read out and the next week's diary,
        /// and a veto meeting by a deal settling, and keying off the last line lost both sounds.
        /// </summary>
        public static string CueName(IEnumerable<string> appendedKinds)
        {
            var kinds = new HashSet<string>(appendedKinds ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            if (kinds.Contains("winner")) return "Finale";
            if (kinds.Contains("eviction")) return "Eviction";
            // The final Head of Household sending a finalist to the jury is the season's last
            // eviction and sounds like one, ranked with the weekly eviction. Its own kind matched
            // nothing here, and its card has no reveal to make a sound, so it clicked like a button.
            if (kinds.Contains("final-eviction")) return "Eviction";
            if (kinds.Contains("nomination")) return "Nomination";
            if (kinds.Contains("veto")) return "Veto";
            if (kinds.Contains("competition")) return "CompetitionWin";
            // The story's ceremonies (plan §5.1): a removal sounds like the house losing somebody, and
            // the rest like the block being set.
            if (kinds.Contains(StoryLog.Expulsion)) return "Eviction";
            if (kinds.Any(StoryLog.IsFallout)) return "Nomination";
            return "Button";
        }

        /// <summary>The sounds of a conversation that moved trust (AU-09): warmer, and cooler.</summary>
        public const string RelationshipUp = "RelationshipUp", RelationshipDown = "RelationshipDown";

        /// <summary>
        /// The cue's name for a commit made in a conversation (AU-09), where <paramref name="trustDelta"/>
        /// is the committed change in the player's trust toward the houseguest they are talking to. A
        /// bigger beat in the commit wins as it always did. Below every beat, trust gained sounds
        /// <see cref="RelationshipUp"/> and trust lost <see cref="RelationshipDown"/>, read as the trust
        /// chip reads it (<see cref="TrustChipSteps"/>), so a change the chip calls "No change in trust"
        /// keeps the button's click. Every conversation's commit used to click like a button.
        /// </summary>
        public static string CueName(IEnumerable<string> appendedKinds, double trustDelta)
        {
            string cue = CueName(appendedKinds);
            if (!string.Equals(cue, "Button", StringComparison.Ordinal)) return cue;
            int steps = TrustChipSteps(trustDelta);
            return steps > 0 ? RelationshipUp : steps < 0 ? RelationshipDown : cue;
        }

        /// <summary>
        /// A change in trust as the conversation's trust chip shows it: a whole number, rounded as the
        /// chip rounds it (Unity's Mathf.RoundToInt over a float: half to even), so the sound and the
        /// chip never disagree about whether anything moved. Not a number counts as no change.
        /// </summary>
        public static int TrustChipSteps(double trustDelta)
        {
            if (double.IsNaN(trustDelta) || double.IsInfinity(trustDelta)) return 0;
            return (int)Math.Round((double)(float)trustDelta);
        }

        /// <summary>
        /// Whether a commit's sound waits for the trust chip (AU-09): a conversation's warmer or cooler
        /// sound is asked in the frame the page draws the chip that says so - the commit's own frame
        /// when no spoken lines take the panel first, and the frame the lines give it back when they do.
        /// </summary>
        public static bool SoundsWithTheTrustChip(string cueName) =>
            string.Equals(cueName, RelationshipUp, StringComparison.Ordinal) || string.Equals(cueName, RelationshipDown, StringComparison.Ordinal);

        /// <summary>
        /// Whether the commit makes its sound <paramref name="cueName"/> itself (AU-07): when no card of
        /// its own reveals the beat, as it always did - and, when a staged veto meeting does, only for a
        /// sound that is not the veto's. A staged meeting only summons the house at its commit; the
        /// veto sounds with the page that shows the decision, a skipped page included.
        /// </summary>
        public static bool SoundsAtTheCommit(string cueName, bool revealed, bool vetoStaged) =>
            !revealed || (vetoStaged && !string.Equals(cueName, "Veto", StringComparison.Ordinal));
    }
}
