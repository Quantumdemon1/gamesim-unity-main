using System;
using Gamesim.Presentation;

namespace Gamesim.Episode
{
    /// <summary>
    /// The houseguests' murmurs (AU-16; the owner's ruling of 26 November 2026, "Yes"): a soft,
    /// wordless voice under each line a conversation shows, in the speaker's own pitch, and a quieter
    /// one under a witnessed caption, in the voice of whichever of the pair has the floor. Each plays
    /// once, as its line comes up; a redraw of the line is silent, and a line that goes - skipped, or
    /// its conversation over - takes its murmur with it.
    ///
    /// <para>Presentation only: the murmur is made from the speaker's id, their pronouns and the line
    /// (<see cref="Murmur"/>), reads the state and never changes it, and draws nothing from the
    /// season's random stream. It plays on the Voice volume (<see cref="SoundChannel.Voice"/>).</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        // The exchange line last murmured: its session, revision and step, as the line's reading clock keys it.
        private string murmuredSession;
        private int murmuredRevision = -1, murmuredStep = -1;

        /// <summary>A conversation's spoken line is drawn: a line new to the voice murmurs in its speaker's voice; a redraw of it does not.</summary>
        private void MurmurExchangeLine(string session, int revision, int step, string speakerId, string line)
        {
            if (string.Equals(session, murmuredSession, StringComparison.Ordinal) && revision == murmuredRevision && step == murmuredStep) return;
            murmuredSession = session;
            murmuredRevision = revision;
            murmuredStep = step;
            if (audioBed != null) audioBed.PlayMurmur(speakerId, PronounsOf(speakerId), line, witnessed: false);
        }

        /// <summary>The conversation's line went - skipped, moved past or the talk ended: its murmur stops, and the next line murmurs afresh.</summary>
        private void HushExchangeMurmur()
        {
            murmuredSession = null;
            murmuredRevision = murmuredStep = -1;
            if (audioBed != null) audioBed.StopMurmur();
        }

        /// <summary>A witnessed caption's new line over a pair the player sees: the one with the floor murmurs it, quieter.</summary>
        private void MurmurWitnessedLine(string speakerId, string words)
        {
            if (audioBed == null || string.IsNullOrEmpty(words)) return;
            audioBed.PlayMurmur(speakerId, PronounsOf(speakerId), words, witnessed: true);
        }

        /// <summary>The pronouns a houseguest's voice takes its register from, or null for one the state does not hold.</summary>
        private string PronounsOf(string id) => id == null || projected == null ? null : projected.Find(id)?.pronouns;
    }
}
