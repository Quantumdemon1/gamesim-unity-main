using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;

namespace Gamesim.Episode
{
    /// <summary>
    /// The cast strip's menu (playtest, 2026-09-27). A houseguest's chip only swung the camera onto
    /// them. It opens a small card over the chip now - walk over and talk, their profile, what is
    /// between you (alliances, promises, deals), or follow them - the web's selected-houseguest bar
    /// (SelectedNPCBar: Talk, Profile, View All Strategy) in the strip's own place.
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        private string castMenuFor;

        /// <summary>Whose chip's menu is open, or null.</summary>
        public string CastMenuFor => castMenuFor;

        /// <summary>
        /// A chip pressed. Its menu - the same chip again closes it; your own chip is your moves
        /// (<see cref="ToggleEmoteMenu"/>), and somebody who has left the house, or a chip pressed
        /// with a panel open, is the follow it always was.
        /// </summary>
        public void PressCastChip(string id)
        {
            var actor = projected?.Find(id);
            if (actor == null) return;
            if (id == projected.playerId && actor.status == ContestantStatus.Active && !IsPanelOpen)
            {
                ToggleEmoteMenu();
                return;
            }
            if (id == projected.playerId || actor.status != ContestantStatus.Active || IsPanelOpen)
            {
                castMenuFor = null;
                emoteMenuOpen = false;
                FollowHouseguest(id);
                return;
            }
            emoteMenuOpen = false;
            castMenuFor = castMenuFor == id ? null : id;
            Render();
        }

        public void CloseCastMenu()
        {
            if (castMenuFor == null) return;
            castMenuFor = null;
            Render();
        }

        /// <summary>Walk over and talk: the click on a body, by name from the strip.</summary>
        public void TalkFromCastMenu(string id)
        {
            castMenuFor = null;
            var npc = housemates?.FirstOrDefault(body => body != null && body.Id == id && body.gameObject.activeInHierarchy);
            if (npc != null) SelectHouseguest(npc);
            // SelectHouseguest renders when it goes; when it refuses, the menu still has to go.
            Render();
        }

        public void ProfileFromCastMenu(string id)
        {
            castMenuFor = null;
            ShowHouseguestProfile(id);
        }

        /// <summary>What is between you: the relationships page on them, which lists the alliances, promises and deals.</summary>
        public void DealsFromCastMenu(string id)
        {
            castMenuFor = null;
            RelationshipWeb.Select(projected, id);
            ShowNotebookSection(NotebookSection.Network);
        }

        public void FollowFromCastMenu(string id)
        {
            castMenuFor = null;
            FollowHouseguest(id);
            Render();
        }
    }
}
