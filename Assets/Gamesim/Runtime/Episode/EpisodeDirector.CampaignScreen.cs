using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>
    /// The campaign as a screen (playtest, 2026-09-27). It was fourteen blocks down one column - the
    /// location, the meter, the storylines, the whole house's meetings with a paragraph each, the
    /// price of more interactions, listening in and its odds - a wall of text before the one way on.
    /// Now, as the web's campaign stage draws it: who is on the block, as faces; the interactions
    /// left; the votes as a grid of cards, each one press from walking over to talk. The rest waits
    /// behind "More ways to campaign" for the player who wants it.
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        public const string CampaignMoreCaption = "More ways to campaign", CampaignLessCaption = "Fewer ways to campaign";

        /// <summary>Whether the campaign's other ways - the whole house, listening in, where you are - are open. View state.</summary>
        private bool campaignMore;

        private void CampaignScreen(EpisodeState state)
        {
            // Something that has happened to the player comes first, as it always has.
            if (!PendingReplyCard(state)) PendingHouseEvent(state);
            var block = state.nominees.Where(id => state.Find(id) != null).ToList();
            hud.CeremonyTitle("WEEK " + state.week, "Campaign",
                block.Count == 2
                    ? state.Find(block[0]).name + " and " + state.Find(block[1]).name + " are on the block. The house votes when you close campaigning."
                    : "The house votes when you close campaigning.",
                UiTheme.Accent);
            int budget = EpisodeEngine.SocialActionBudget(state);
            hud.Meter("Interactions available",
                Mathf.Max(0, budget - EpisodeEngine.SocialActionsSpent(state)), budget, UiTheme.Accent);
            if (HaveNots.Is(state, state.playerId)) hud.Paragraph(HaveNotLine);
            hud.CeremonyFaces("On the block", block.Select(id => new EpisodeHud.CeremonyFace(id, "NOM", UiTheme.Danger)).ToList(), 130f);
            hud.CampaignVoters(EpisodeEngine.Voters(state).Where(actor => !actor.isPlayer).Select(actor => actor.id).ToList(), TalkFromCampaign);
            StorylinesBlock(state);
            if (state.playerStudyBonus > 0)
                hud.Paragraph("Preparation banked for competitions: " + state.playerStudyBonus + "/5.");
            hud.Action(campaignMore ? CampaignLessCaption : CampaignMoreCaption, () => { campaignMore = !campaignMore; Render(); });
            if (!campaignMore) return;
            CurrentLocation(state);
            // The whole house's moves and listening in, as the tiles free time draws them.
            HouseMoves(state);
        }

        /// <summary>Walk over and talk, from a voter's card: the panel closes, then the walk.</summary>
        private void TalkFromCampaign(string id)
        {
            ClosePanels();
            TalkFromCastMenu(id);
        }
    }
}
