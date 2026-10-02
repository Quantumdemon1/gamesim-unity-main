using Gamesim.Presentation;

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
        /// What the house draws under them does not draw.
        /// </summary>
        public bool IsHouseUnderChrome => IsPanelOpen || IsBriefing || CompetitionGameScreen.AnyDrawn || CeremonyOverlays.OnScreen;

        /// <summary>
        /// Whether the chrome has every name plate down: a board over the house, or the briefing.
        /// Not a docked panel: mockup-12 keeps the name of the one you are talking to over their
        /// head above the conversation, and a plate under a panel's glass is down by its own rect.
        /// </summary>
        private bool chromePlatesDown;

        /// <summary>
        /// Each frame, what the house draws over itself as the chrome allows it (<see cref="Update"/>):
        /// the plates down together under a board or the briefing and one by one under the chrome
        /// that covers them, the arena's sign and discs with the gate, the room chips with it too.
        /// </summary>
        private void TickHouseOverlays()
        {
            bool under = IsHouseUnderChrome;
            bool down = CompetitionGameScreen.AnyDrawn || IsBriefing;
            if (down != chromePlatesDown) { chromePlatesDown = down; ApplyPlates(); }
            var eye = cameraRig != null ? cameraRig.ViewCamera : null;
            bool chrome = hud != null && hud.IsVisible && eye != null;
            if (housemates != null)
                foreach (var npc in housemates)
                {
                    if (npc == null) continue;
                    // Through the camera the frame is drawn with - WorldToScreenPoint follows the
                    // overview's hand-set lens - against the chrome's rects, containers included.
                    npc.PlateCovered = chrome && npc.TryPlateScreenRect(eye, out var plate) && hud.CoversAny(plate, true);
                }
            SetCompetitionOverlaysVisible(!under);
            if (roomLabels != null) roomLabels.Hidden = under;
        }
    }
}
