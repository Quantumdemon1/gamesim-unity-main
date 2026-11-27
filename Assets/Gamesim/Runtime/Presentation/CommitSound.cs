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
    }
}
