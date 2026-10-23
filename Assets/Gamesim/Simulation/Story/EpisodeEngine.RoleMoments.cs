using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The veto board's economics (ROLE-MOMENTS-PLAN R3, §3.0 and §3.2), under the role moments only
    /// (<see cref="RolesOn"/>): what the cards of the offer board say, the engine makes true.
    ///
    /// <para><b>One word on the veto.</b> The veto saves one of them, so the player gives one word on it a week
    /// (<see cref="Negotiation.VetoWordGiven"/>). Naming a price for it has always refused a second; under the role
    /// moments a second yes to a nominee's ask is refused too (<see cref="SecondVetoWord"/>), and so is a story's
    /// promise of the veto (the showmance's, which <see cref="StoryCatalog"/> hides once a word is given, or while the
    /// partner's own ask waits on the board; drawn before the word, it is drawn locked and refused,
    /// <see cref="VetoWordRefusal"/>). Before them only the meeting's row locked a second yes, and a season that
    /// recorded two still replays.</para>
    ///
    /// <para><b>A no, and silence.</b> An honest no costs what the card's tone stakes (<see cref="RoleTone.Stakes"/>),
    /// from 0 to -3 in place of the flat -3, and an ask left hanging costs more, -1 to -5, when the decision lapses
    /// it. A lapse is a change in how they feel and nothing else: the ask expires as it always did - never Broken,
    /// no witnesses, nothing against the player's word or their standing with the house.</para>
    ///
    /// <para><b>The riders.</b> A Bitter nominee whose word was kept eases their grudge against the player by thirty;
    /// a Desperate one owes the player a favour (a hook, of which the story holds at most 32); a Threatening one
    /// turned down or left hanging, and left on the block, holds a grudge of thirty against the player.</para>
    ///
    /// <para><b>The tone the player saw.</b> A no is charged in its own command, from the state its card was drawn
    /// from. Silence and the riders settle with the decision, which first fires the house's last beats
    /// (<see cref="Close"/>) and then moves the block, the scores and the deals: a beat that seeks the player out
    /// lifts the player's read of that houseguest, and so does the save of the one saved, and either can turn a card
    /// Friendly. So the board is read as the player last saw it, before the decision's command touches anything
    /// (<see cref="VetoBoardAsSeen"/>), and settled from that reading once the decision is made
    /// (<see cref="SettleTheVetoBoard"/>).</para>
    ///
    /// <para><b>Only the board's asks.</b> A card is a nominee's ask (<see cref="RoleOffers.IsVetoAsk"/>), filed only
    /// while the player holds the veto, so only the player's own decision settles a board. A houseguest's decision on a
    /// veto they hold settles nothing here: a word they gave the player - a favour called in - was never a card, and
    /// it pays no rider (R3's review: it gave back the favour the player had just spent).</para>
    ///
    /// <para>Every change goes through the writers the engine already has: <see cref="Change"/> for the player's
    /// standing, which draws the season's reciprocal roll - only a season at Roles reaches it - and the story effect
    /// router (<see cref="ApplyStoryEffect"/>) for a grudge, its easing and a favour. Filing the asks stays
    /// roll-free (<see cref="NpcDeals.AskForTheVeto"/>), and nothing new is stored: the tones are read again.</para>
    /// </summary>
    public sealed partial class EpisodeEngine
    {
        /// <summary>The ledger's type for an offer the player left unanswered until it lapsed: a change in how they feel, never a breach, and one that fades.</summary>
        public const string LapsedOfferEvent = "deal_lapsed";

        /// <summary>One card of the veto board as the player last saw it: the ask, who made it, the tone the card wore, and how the ask stood.</summary>
        private sealed class BoardCard
        {
            public string askId, askerId, tone, status;
        }

        /// <summary>
        /// Whether a yes to this offer would be a second word on the veto this week, which the role moments refuse in
        /// the words a second price is refused in (<see cref="Negotiation.VetoSavesOneRefusal"/>).
        /// </summary>
        private static bool SecondVetoWord(EpisodeState s, DealState offer) =>
            RolesOn(s) && offer != null && Negotiation.IsVetoWord(offer.type) && Negotiation.VetoWordGiven(s);

        /// <summary>
        /// What an honest no to this offer costs with its maker: under the role moments, for a veto ask, its card's
        /// tone's no (<see cref="RoleOffers.ToneOf"/>), read before the answer changes anything; otherwise, and for every
        /// other offer, the no it always cost (<see cref="PlayerDeals.DeclineImpact"/>).
        /// </summary>
        private static double DeclineCost(EpisodeState s, DealState offer) =>
            RolesOn(s) && RoleOffers.IsVetoAsk(s, offer) && RoleTone.Stakes(RoleOffers.ToneOf(s, offer)) is RoleTone.Stake stake
                ? stake.decline : PlayerDeals.DeclineImpact;

        /// <summary>
        /// Why a story option may not be taken now, under the role moments, or null: one that would give the player's
        /// own word on the veto once they have given one this week (<see cref="GivesAVetoWord"/>), refused in the words a
        /// second price is (<see cref="Negotiation.VetoSavesOneRefusal"/>). The engine refuses it as it is answered
        /// (<see cref="ProgressStoryline"/>) and the director draws it locked, so an option drawn before the word was
        /// given - a beat can wait for airtime - is never pressable after it. Null in every season without the role
        /// moments, where a story's promise stood beside a yes as it always did.
        /// </summary>
        public static string VetoWordRefusal(EpisodeState s, HouseEventChoice choice) =>
            RolesOn(s) && GivesAVetoWord(s, choice) && Negotiation.VetoWordGiven(s) ? Negotiation.VetoSavesOneRefusal : null;

        /// <summary>
        /// Whether a story option, answered, would give the player's own word on the veto: a deal of that kind among its
        /// effects that the player makes. Under the role moments such an option is refused once a word is given (R3), as
        /// a second yes is. A holder's word to the player - a favour called in - is theirs, not the player's, and stands.
        /// </summary>
        private static bool GivesAVetoWord(EpisodeState s, HouseEventChoice choice) =>
            choice != null && (choice.effects ?? new List<StoryEffectState>()).Concat(choice.backfire ?? new List<StoryEffectState>())
                .Any(effect => effect != null && effect.kind == StoryEffects.Deal && Negotiation.IsVetoWord(effect.type) && effect.fromId == s.playerId);

        /// <summary>
        /// The veto board as the player last saw it: every one of this week's veto asks, answered or not
        /// (<see cref="RoleOffers.IsVetoAsk"/>), in the board's public order, each with the tone its card wore and how
        /// it stood. Read before the decision's command moves anything - its last beats and the decision itself - so
        /// silence and the riders settle on the tone the card showed. Null, reading nothing, without the role moments.
        /// </summary>
        private static List<BoardCard> VetoBoardAsSeen(EpisodeState s) =>
            !RolesOn(s) ? null
                : RoleOffers.InBoardOrder(s, s.deals.Where(d => RoleOffers.IsVetoAsk(s, d)))
                    .Select(d => new BoardCard { askId = d.id, askerId = d.proposerId, tone = RoleOffers.ToneOf(s, d), status = d.status }).ToList();

        /// <summary>
        /// What the veto decision left of the board (<see cref="VetoBoardAsSeen"/>), settled right after it in the same
        /// command: each card in turn, on the tone it wore as the player last saw it. An ask the decision lapsed - still
        /// waiting when it was made - costs the tone's lapse with its maker, through <see cref="Change"/>, and the player
        /// is told so; then the tone's rider, where it has one - kept, a Bitter maker's grudge eases and a Desperate one
        /// owes the player a favour; turned down or left hanging, and left on the block, a Threatening one holds a
        /// grudge. Nothing for a maker no longer in the house, and nothing at all without the role moments.
        /// </summary>
        private static void SettleTheVetoBoard(EpisodeState s, List<BoardCard> board)
        {
            if (board == null) return;
            foreach (var card in board)
            {
                var ask = s.deals.FirstOrDefault(d => d.id == card.askId);
                var asker = s.Find(card.askerId);
                var stake = RoleTone.Stakes(card.tone);
                if (ask == null || stake == null || asker == null || asker.status != ContestantStatus.Active) continue;
                string title = DealKind.Title(ask.type).ToLowerInvariant(), key = "w" + s.week + ":veto-board:" + ask.id;
                bool hanging = card.status == DealStatus.Proposed && ask.status == DealStatus.Expired;
                if (hanging)
                {
                    Change(s, s.playerId, asker.id, stake.lapse, "Left my " + title + " unanswered.", LapsedOfferEvent);
                    Log(s, "deal", "You left a " + title + " from " + asker.name + " unanswered.", s.playerId, asker.id);
                }
                bool kept = ask.status == DealStatus.Fulfilled;
                bool leftUp = (hanging || ask.status == DealStatus.Declined) && s.nominees.Contains(asker.id);
                switch (stake.rider)
                {
                    case RoleTone.Riders.EasesTheirGrudge when kept:
                        Rider(s, StoryEffects.Ease, asker.id, s.playerId, stake.riderAmount, null, key);
                        break;
                    case RoleTone.Riders.OwesAFavour when kept:
                        int owed = s.story.hooks.Count(h => !h.spent && h.holderId == s.playerId && h.overId == asker.id);
                        Rider(s, StoryEffects.Hook, s.playerId, asker.id, 0, null, key);
                        // Only where the story had room for it: it holds 32 at most (Hooks.Create).
                        if (s.story.hooks.Count(h => !h.spent && h.holderId == s.playerId && h.overId == asker.id) > owed)
                            Log(s, "deal", asker.name + " owes you a favour for your word on the veto.", s.playerId, asker.id);
                        break;
                    case RoleTone.Riders.HoldsAGrudge when leftUp:
                        Rider(s, StoryEffects.Grudge, asker.id, s.playerId, stake.riderAmount, GrudgeCauses.Story, key);
                        break;
                }
            }
        }

        /// <summary>A rider, through the story effect router as a story option's consequence goes: no cycle, nobody's reception, no backfire.</summary>
        private static void Rider(EpisodeState s, string kind, string fromId, string toId, double amount, string type, string key) =>
            ApplyStoryEffect(s, new StoryEffectState { kind = kind, fromId = fromId, toId = toId, amount = amount, type = type }, null, null, key, 0, false);
    }
}
