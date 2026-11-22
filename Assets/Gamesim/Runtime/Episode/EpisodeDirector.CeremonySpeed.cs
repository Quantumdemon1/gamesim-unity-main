using Gamesim.Presentation;

namespace Gamesim.Episode
{
    /// <summary>
    /// The ceremonies remember Speed up (UI-UX plan D2, the lead's ruling L9): once the player speeds
    /// a reveal up, the later reveals of the same session start fast - the key ceremony, the live
    /// eviction, the jury's vote, the veto draw and the veto meeting on the living room's screen -
    /// until the player presses Speed up again on one, which puts them back at their own pace.
    ///
    /// <para>The speed lives in this director's own field and is handed to each Play. It is never a
    /// static, which would outlive a play session in the editor (statics are kept when domain reload
    /// is off), and it is never saved: a new session starts every reveal at its own pace. It changes
    /// how long an order takes to read, never the order or the result, which the commit decided and
    /// saved before any card plays.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>The speed the player left the last reveal at this session: 1, or CeremonyPacing.SpeedUp.</summary>
        private float ceremonySpeed = 1f;

        /// <summary>The speed the next reveal starts at: 1, or <see cref="CeremonyPacing.SpeedUp"/> once the player sped one up.</summary>
        public float CeremonySpeed => ceremonySpeed;

        /// <summary>Keeps the speed a reveal was just set to, for the reveals after it.</summary>
        private void RememberCeremonySpeed(float speed) => ceremonySpeed = CeremonyPacing.SessionSpeed(speed);

        /// <summary>
        /// Listens to every reveal the director attaches at its start for the player's Speed up, and
        /// hands each a way to ask which device the house last heard.
        /// </summary>
        private void ListenForCeremonySpeed()
        {
            if (keyCeremony != null) keyCeremony.SpeedChanged += RememberCeremonySpeed;
            if (voteReveal != null) voteReveal.SpeedChanged += RememberCeremonySpeed;
            if (juryReveal != null) juryReveal.SpeedChanged += RememberCeremonySpeed;
            if (takeover != null) takeover.SpeedChanged += RememberCeremonySpeed;
            // Each card opens in the words of the device the house last heard (UI-UX plan U5).
            if (keyCeremony != null) keyCeremony.PadHints = CardsOnPad;
            if (voteReveal != null) voteReveal.PadHints = CardsOnPad;
            if (juryReveal != null) juryReveal.PadHints = CardsOnPad;
            if (takeover != null) takeover.PadHints = CardsOnPad;
        }

        /// <summary>The device the house's hints are in, which a ceremony card opens in.</summary>
        private bool CardsOnPad() => padHints;
    }
}
