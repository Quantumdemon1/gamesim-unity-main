using Gamesim.Presentation;
using Gamesim.House;

namespace Gamesim.Episode
{
    /// <summary>
    /// One gate for everything the house draws over itself (UI-UX-PASS-PLAN G0 and H0): the
    /// yard's award sign and the station discs, the houseguests' name plates, the overview's room
    /// chips, the icons over the rooms and the talk prompt all ask it before drawing. They are
    /// world-space, or on a canvas under the HUD's, and the HUD's panels and boards are glass:
    /// drawn regardless, every one of them read through whatever stood over it - the sign through
    /// the competition board on all twelve games, the chips through the briefing, a plate half
    /// under the right column reading as another name.
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>
        /// Whether a HUD panel, a side card, a stage or a board stands over the house this frame:
        /// a panel by <see cref="IsPanelOpen"/>'s reckoning (which counts the opening, the recap,
        /// the report and the result card), the overview's briefing with its side card, a
        /// competition board - the director's or any other that is drawn - and a ceremony card.
        /// What the house draws under them does not draw, and E does nothing under them.
        /// </summary>
        public bool IsHouseUnderChrome => IsPanelOpen || IsBriefing || CompetitionGameScreen.AnyDrawn || CeremonyOverlays.OnScreen;

        /// <summary>
        /// Whether the chrome has every name plate down: a board over the house, or the briefing.
        /// Under anything else a plate is down only where the chrome covers it, measured late
        /// (<see cref="PlaceHouseOverlaysLate"/>).
        /// </summary>
        private bool chromePlatesDown;

        /// <summary>The late pass the plates are measured in, attached the first time the house ticks.</summary>
        private HouseOverlayPass overlayPass;
        private HousePublicDisplays publicDisplays;

        /// <summary>
        /// Each frame, what the house draws over itself as the chrome allows it (<see cref="Update"/>):
        /// the plates down together under a board or the briefing, the arena's sign and discs down
        /// under the board, the room chips down under the gate. Each plate is measured against the
        /// chrome later in the frame, once the camera has moved and the plate has been placed.
        /// </summary>
        private void TickHouseOverlays()
        {
            bool under = IsHouseUnderChrome;
            bool boardDrawn = CompetitionGameScreen.AnyDrawn;
            bool down = boardDrawn || IsBriefing;
            if (down != chromePlatesDown) { chromePlatesDown = down; ApplyPlates(); }
            // The arena stands under its own board, not under the episode screen the attempt was
            // opened from: the screen's flag holds for the whole attempt while the HUD and its panel
            // stand down, and asked as a panel, the sign and the discs never drew at all - not even
            // in the look sheet's capture, which switches the board off to photograph the yard.
            bool arenaUnder = challengeActive ? boardDrawn || CeremonyOverlays.OnScreen : under;
            SetCompetitionOverlaysVisible(!arenaUnder);
            if (publicDisplays == null) publicDisplays = HousePublicDisplays.Find(gameObject.scene);
            // Public yard lettering follows the same owner as the arena's sign: the board,
            // committed result and other panels. An attempt's canvas-off world view still draws it.
            if (publicDisplays != null && publicDisplays.isActiveAndEnabled) publicDisplays.SetCompetitionBoardDrawn(arenaUnder);
            if (roomLabels != null) roomLabels.Hidden = under;
            // Explicitly: a missing component is Unity's fake null in the editor, which ?? keeps.
            if (overlayPass == null)
            {
                overlayPass = gameObject.GetComponent<HouseOverlayPass>();
                if (overlayPass == null) overlayPass = gameObject.AddComponent<HouseOverlayPass>();
                overlayPass.Late = PlaceHouseOverlaysLate;
            }
        }

        /// <summary>
        /// Each plate against the chrome that covers it (<see cref="EpisodeHud.CoversPlate"/>), after
        /// the camera rig has moved for the frame and the houseguests have placed their plates:
        /// measured in Update, a pan left a plate under a card's edge for a frame. Through the
        /// camera the frame is drawn with - WorldToScreenPoint follows the overview's hand-set lens.
        ///
        /// <para>Never the one the player is talking to (mockup-12): their name stays over their
        /// head above the conversation, which is a scrim the pair are seen through, not a card.</para>
        /// </summary>
        private void PlaceHouseOverlaysLate()
        {
            if (housemates == null) return;
            var eye = cameraRig != null ? cameraRig.ViewCamera : null;
            bool chrome = hud != null && hud.IsVisible && eye != null;
            string talking = TalkingToId;
            foreach (var npc in housemates)
            {
                if (npc == null) continue;
                npc.PlateCovered = chrome && npc.Id != talking && npc.TryPlateScreenRect(eye, out var plate) && hud.CoversPlate(plate);
            }
        }
    }
}
