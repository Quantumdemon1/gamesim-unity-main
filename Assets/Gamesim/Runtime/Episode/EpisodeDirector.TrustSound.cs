using Gamesim.Presentation;

namespace Gamesim.Episode
{
    /// <summary>
    /// A conversation that warms or cools a relationship sounds like it (AU-09). Every accepted
    /// conversation commit clicked like a button, although the page goes on to say what it bought on
    /// the trust chip. Now, with no bigger beat in the commit, trust gained sounds
    /// <see cref="HouseAudio.Cue.RelationshipUp"/> and trust lost <see cref="HouseAudio.Cue.RelationshipDown"/>
    /// (<see cref="CommitCue(System.Collections.Generic.IEnumerable{string}, double)"/>), and it is
    /// asked in the frame the chip is drawn - its picture. A spoken exchange has the panel first, so
    /// there the press still clicks at the commit and the trust sound waits for the page the lines give
    /// back; with no lines, the chip is drawn at the commit and its sound is the commit's only one.
    /// Presentation only: the change is read after the commit, nothing is drawn from the season's
    /// stream, and nothing is saved.
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>The trust sound the last conversation's commit owes its chip, or null when none is owed.</summary>
        private HouseAudio.Cue? trustCue;

        /// <summary>The commit's own sound: a trust sound waits for its chip; every other cue plays now, as it always did.</summary>
        private void PlayCommitCue(HouseAudio.Cue cue)
        {
            if (CommitSound.SoundsWithTheTrustChip(cue.ToString())) { trustCue = cue; return; }
            if (audioBed != null) audioBed.PlayCue(cue);
        }

        /// <summary>The conversation's page draws the trust chip: a trust sound it is owed is asked now, once.</summary>
        private void SoundTheTrustChip()
        {
            if (!trustCue.HasValue) return;
            var cue = trustCue.Value;
            trustCue = null;
            if (audioBed != null) audioBed.PlayCue(cue);
        }

        /// <summary>
        /// After an accepted commit's own render: a trust sound its chip did not take there - the spoken
        /// lines have the panel, or a free talk's page does - leaves the press the click every
        /// conversation's commit made, and waits for the chip. With no chip coming for this action,
        /// nothing is owed.
        /// </summary>
        private void SettleTrustCue(bool accepted)
        {
            if (!accepted || !trustCue.HasValue) return;
            if (audioBed != null) audioBed.PlayCue(HouseAudio.Cue.Button);
            if (!lastSocialAction.HasValue) trustCue = null;
        }
    }
}
