using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;

namespace Gamesim.Episode
{
    /// <summary>
    /// The week's ceremonies as screens (playtest, 2026-09-27). When the player had nothing to
    /// decide, the nomination, the veto's draw and the veto meeting were a quiet card 300 high:
    /// the phase's name and one line of "HoH: X · Nominees: A and B". The web gives each the whole
    /// screen - the Head of Household large, the block in red, the veto's chips in a bag
    /// (NominationContent, ChipDraw, POVMeeting) - and so does this: the ceremony's name, what is
    /// happening, and the house as faces carrying the week's roles. The way on is the panel's own
    /// "Continue episode", pinned under it as before.
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>The beats that are a ceremony's screen rather than a quiet card when the player has nothing to decide.</summary>
        private static bool CeremonyScreenBeat(EpisodeState s) => s != null && s.pendingDiary == null
            && (s.phase == EpisodePhase.Nomination || s.phase == EpisodePhase.VetoSelection || s.phase == EpisodePhase.VetoMeeting);

        /// <summary>
        /// The week's four strategy screens, which take the strategy stage (PACK8-PASS-PLAN A3): the
        /// ceremonies' three and the campaign. The Head of Household's own picker is one of the
        /// nomination's steps there (B1, EpisodeDirector.NominationScreen.cs); only the diary's
        /// nomination keeps a column of its own. Never under a challenge, whose sheet is a layout of
        /// its own.
        /// </summary>
        private bool StrategyScreenBeat(EpisodeState s) => s != null && !challengeActive
            && (CeremonyScreenBeat(s) || (s.phase == EpisodePhase.Campaign && s.pendingDiary == null));

        /// <summary>The Pack 8 shell a strategy screen is framed in: the veto meeting has none of its own and wears the draw's.</summary>
        private static string StrategyShell(EpisodeState s) =>
            s.phase == EpisodePhase.Nomination ? PackArt.Pack8NominationShell
            : s.phase == EpisodePhase.Campaign ? PackArt.Pack8CampaignShell
            : PackArt.Pack8VetoShell;

        private void CeremonyScreen(EpisodeState s)
        {
            if (!CeremonyScreenBeat(s) || s.Find(s.hohId) == null) return;
            var hoh = s.Find(s.hohId);
            string week = "WEEK " + s.week;
            EpisodeHud.CeremonyFace Face(string id, string role, UnityEngine.Color colour) => new EpisodeHud.CeremonyFace(id, role, colour);
            var crown = Face(hoh.id, "HOH", UiTheme.Gold);
            var block = s.nominees.Where(id => s.Find(id) != null).Select(id => Face(id, "NOM", UiTheme.Danger)).ToList();
            switch (s.phase)
            {
                case EpisodePhase.Nomination when s.nominees.Count == 0:
                    hud.CeremonyTitle(week, "Nomination Ceremony",
                        hoh.name + " must nominate two houseguests for eviction. Whoever is named can still save themselves in the Power of Veto.",
                        UiTheme.Danger);
                    // The whole house in one row, the crown on its Head: who decides, and who could be named.
                    var house = new List<EpisodeHud.CeremonyFace> { crown };
                    house.AddRange(s.Active.Where(c => c.id != hoh.id).Select(c => Face(c.id, null, UiTheme.Muted)));
                    hud.CeremonyFaces("The house", house, 124f);
                    break;
                case EpisodePhase.Nomination:
                    hud.CeremonyTitle(week, "Nominated for Eviction",
                        hoh.name + " has made their decision. " + string.Join(" and ", s.nominees.Select(id => s.Find(id)?.name))
                        + " are on the block.", UiTheme.Danger);
                    var named = new List<EpisodeHud.CeremonyFace>(block) { crown };
                    hud.CeremonyFaces("On the block, and who put them there", named, 150f);
                    break;
                case EpisodePhase.VetoSelection:
                    // One row across the frame, and no draw at six or fewer (EpisodeDirector.VetoDraw.cs).
                    VetoDrawScreen(s);
                    break;
                case EpisodePhase.VetoMeeting:
                    // The meeting's own screens, before and after it (EpisodeDirector.VetoMeeting).
                    VetoMeetingScreen(s);
                    break;
            }
        }
    }
}
