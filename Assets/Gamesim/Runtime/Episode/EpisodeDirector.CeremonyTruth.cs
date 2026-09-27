using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// Ceremonies that keep their secret until they tell it: the keys dealt in an order that gives
    /// nothing away, the house's chrome stepping aside while a reveal plays, the evicted houseguest
    /// still in the room for their own eviction, the sound of the beat that actually happened, and
    /// the pace the player chose.
    ///
    /// <para>Each of these was a spoiler. Safe keys came out in cast order and the player is first in
    /// the cast, so the first key told a safe player their fate. The status line, the house panel and
    /// the cast strip were redrawn from the committed result while the reveal was still counting to
    /// it. The evicted houseguest's body was switched off at the commit, so the room reacted to an
    /// empty spot. And the eviction's sound never played, because the commit's last line is always a
    /// vote read out after it.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        private CeremonyPace ceremonyPace = CeremonyPace.Suspenseful;

        /// <summary>The houseguest being evicted, kept in the room while a card narrates the eviction.</summary>
        private string departingId;

        /// <summary>Whether the HUD stepped aside for a key ceremony or a live eviction now playing.</summary>
        private bool revealHeld;

        /// <summary>The settings' pace switch, in the words of what pressing it does.</summary>
        public const string QuickCeremoniesCaption = "Make ceremonies quick";
        public const string SuspensefulCeremoniesCaption = "Make ceremonies suspenseful";

        /// <summary>How the ceremony reveals are paced: suspenseful unless the player asked for quick.</summary>
        public CeremonyPace CeremonyPaceSetting => ceremonyPace;

        /// <summary>The evicted houseguest still standing in the room for their eviction's reveal, or null.</summary>
        public string DepartingId => departingId;

        /// <summary>Sets the reveal pace and keeps it with the other preferences.</summary>
        public void SetCeremonyPace(CeremonyPace pace)
        {
            ceremonyPace = pace;
            ApplyPreferences();
        }

        /// <summary>
        /// The sound a commit makes, chosen from every line it appended rather than its last one. The
        /// biggest beat wins: an eviction is followed by its votes read out and the next week's diary,
        /// and a veto meeting by a deal settling, and keying off the last line lost both sounds.
        /// </summary>
        public static HouseAudio.Cue CommitCue(IEnumerable<string> appendedKinds)
        {
            var kinds = new HashSet<string>(appendedKinds ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            if (kinds.Contains("winner")) return HouseAudio.Cue.Finale;
            if (kinds.Contains("eviction")) return HouseAudio.Cue.Eviction;
            if (kinds.Contains("nomination")) return HouseAudio.Cue.Nomination;
            if (kinds.Contains("veto")) return HouseAudio.Cue.Veto;
            if (kinds.Contains("competition")) return HouseAudio.Cue.CompetitionWin;
            // The story's ceremonies (plan §5.1): a removal sounds like the house losing somebody, and
            // the rest like the block being set.
            if (kinds.Contains(StoryLog.Expulsion)) return HouseAudio.Cue.Eviction;
            if (kinds.Any(StoryFallout.IsFallout)) return HouseAudio.Cue.Nomination;
            return HouseAudio.Cue.Button;
        }

        /// <summary>
        /// The order the safe keys are dealt in: shuffled, the same way every time for the same week of
        /// the same season, so a reload shows the same ceremony. Presentation only - it reads the seed
        /// and never draws from the season's generator. The reference build shuffles its keys; cast
        /// order dealt the player the first key every week they were safe.
        /// </summary>
        public static List<string> KeyOrder(IEnumerable<string> ids, uint seed, int week) =>
            (ids ?? Enumerable.Empty<string>())
                .Select((id, index) => (id, index, rank: KeyRank(seed, week, id)))
                .OrderBy(entry => entry.rank).ThenBy(entry => entry.index)
                .Select(entry => entry.id).ToList();

        private static uint KeyRank(uint seed, int week, string id)
        {
            unchecked
            {
                uint hash = 2166136261u;
                void Mix(uint value)
                {
                    for (int shift = 0; shift < 32; shift += 8) { hash ^= (value >> shift) & 0xFF; hash *= 16777619u; }
                }
                Mix(seed); Mix((uint)week); Mix(0x6B657973u); // "keys"
                foreach (char c in id ?? string.Empty) Mix(c);
                return hash;
            }
        }

        /// <summary>
        /// The house's chrome steps aside while a key ceremony or a live eviction plays: it is drawn
        /// from the committed result, so it named the nominees and the evicted before the reveal
        /// reached them. It comes back, and the house redraws, the moment the card is over or skipped.
        /// </summary>
        private void HoldHudForReveal()
        {
            if (hud == null) return;
            revealHeld = true;
            hud.HoldForReveal(true);
        }

        /// <summary>
        /// Each frame: the chrome returns when the reveal it stepped aside for ends; the evicted
        /// houseguest leaves when the last card narrating their eviction is gone; and while any
        /// ceremony card is on screen the UI takes no Submit, so the key that moves a card on does
        /// not also press whatever HUD control has the keyboard.
        /// </summary>
        private void TickCeremonies()
        {
            bool revealing = (keyCeremony != null && keyCeremony.IsPlaying) || (voteReveal != null && voteReveal.IsPlaying);
            if (revealHeld && !revealing)
            {
                revealHeld = false;
                if (hud != null) hud.HoldForReveal(false);
                if (IsReady) Render();
            }
            bool narrating = revealing || (takeover != null && takeover.IsPlaying);
            if (departingId != null && !narrating)
            {
                departingId = null;
                if (IsReady) Project();
            }
            GuardUiSubmit(CeremonyOverlays.OnScreen);
        }

        private bool submitHeld;

        /// <summary>
        /// While a ceremony card is on screen the UI's Submit and Cancel wait. The cards read Enter,
        /// Escape and the pad's South and East themselves - they take no input through the event
        /// system, by design - so the same press also reached whichever HUD control had the
        /// keyboard, and moving a card on could commit the next decision underneath it.
        /// </summary>
        private void GuardUiSubmit(bool cardUp)
        {
            if (cardUp == submitHeld) return;
            var module = EventSystem.current != null ? EventSystem.current.currentInputModule as InputSystemUIInputModule : null;
            var submit = module != null && module.submit != null ? module.submit.action : null;
            var cancel = module != null && module.cancel != null ? module.cancel.action : null;
            if (submit == null && cancel == null) { submitHeld = false; return; }
            if (cardUp) { submit?.Disable(); cancel?.Disable(); }
            else { submit?.Enable(); cancel?.Enable(); }
            submitHeld = cardUp;
        }

        /// <summary>Whether the UI's Submit is waiting for a ceremony card. A read, for tests.</summary>
        public bool IsSubmitHeldForCeremony => submitHeld;

        /// <summary>Forgets a reveal's hold and a departure when a season is replaced under them.</summary>
        private void ResetCeremonyTruth()
        {
            departingId = null;
            if (revealHeld && hud != null) hud.HoldForReveal(false);
            revealHeld = false;
            GuardUiSubmit(false);
        }

        /// <summary>
        /// The jury's line on the finale panel: how many jurors there really are, and - when an even
        /// jury could split - who a tie goes to, by name. It said "Four jurors" whatever the house, and
        /// the default house seats six.
        /// </summary>
        public static string JuryLine(EpisodeState state)
        {
            int jurors = state?.contestants?.Count(actor => actor.status == ContestantStatus.Jury) ?? 0;
            string line = jurors == 1 ? "One juror chooses the winner." : jurors + " jurors choose the winner.";
            var finalists = state?.contestants?.Where(actor => actor.status == ContestantStatus.Active).ToList();
            if (jurors > 0 && jurors % 2 == 0 && finalists != null && finalists.Count == 2)
                line += " If they split evenly, the win goes to " + (finalists[1].id == state.playerId ? "you" : finalists[1].name)
                    + ", as the reference game's tie rule has it.";
            return line;
        }
    }
}
