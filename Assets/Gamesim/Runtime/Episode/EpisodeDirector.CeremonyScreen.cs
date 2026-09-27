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
                {
                    int seats = EpisodeEngine.VetoPlayerCount(s.Active.Count());
                    var byRight = new List<EpisodeHud.CeremonyFace> { crown };
                    byRight.AddRange(block);
                    var pool = s.Active.Where(c => c.id != hoh.id && !s.nominees.Contains(c.id)).ToList();
                    int toDraw = System.Math.Max(0, System.Math.Min(pool.Count, seats - byRight.Count));
                    hud.CeremonyTitle(week, "Power of Veto Player Selection",
                        seats + " play for the Golden Power of Veto: the Head of Household and both nominees by right, and "
                        + toDraw + " drawn from the house.", UiTheme.Gold);
                    hud.CeremonyFaces("Playing by right", byRight, 128f);
                    hud.ChipBag(toDraw, toDraw == 1 ? "One chip to draw from the bag." : toDraw + " chips to draw from the bag.");
                    hud.CeremonyFaces("In the bag", pool.Select(c => Face(c.id, null, UiTheme.Muted)).ToList(), 110f);
                    break;
                }
                case EpisodePhase.VetoMeeting:
                {
                    var holder = s.Find(s.vetoHolderId);
                    if (holder == null) return;
                    var veto = Face(holder.id, "VETO", UiTheme.Gold);
                    if (!s.vetoResolved)
                    {
                        hud.CeremonyTitle(week, "Power of Veto Meeting",
                            holder.name + " holds the Golden Power of Veto and must decide whether to use it.", UiTheme.Gold);
                        var meeting = new List<EpisodeHud.CeremonyFace> { veto };
                        meeting.AddRange(block);
                        hud.CeremonyFaces("Holding the veto, and on the block", meeting, 150f);
                    }
                    else
                    {
                        var decision = s.events.LastOrDefault(e => e.kind == "veto" && e.week == s.week);
                        hud.CeremonyTitle(week, "The Veto Meeting Is Over", decision?.text, UiTheme.Gold);
                        var after = new List<EpisodeHud.CeremonyFace>(block) { veto };
                        hud.CeremonyFaces("The final nominees, and the veto", after, 150f);
                    }
                    break;
                }
            }
        }
    }
}
