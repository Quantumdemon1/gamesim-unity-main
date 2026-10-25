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
    /// <para><b>Only the board's asks.</b> A card is a nominee's ask (<see cref="RoleOffers.IsVetoAsk"/>), or the Head of
    /// Household's ask to keep the block (<see cref="RoleOffers.IsKeepTheBlockAsk"/>, R4), each filed only while the
    /// player holds the veto, so only the player's own decision settles a board. A houseguest's decision on a veto they
    /// hold settles nothing here: a word they gave the player - a favour called in - was never a card, and it pays no
    /// rider (R3's review: it gave back the favour the player had just spent).</para>
    ///
    /// <para><b>The Head of Household's card</b> (ROLE-MOMENTS-PLAN R4, §3.3). A no and silence cost what its tone
    /// stakes, as a nominee's do. Taken, the veto decision judges it (<see cref="DealResolution.Hold"/>): not used, kept,
    /// and a Bitter Head of Household's grudge eases by thirty; used, the player broke it, with every consequence a
    /// breach has. Turned down or left hanging, and the veto used anyway, they asked and were defied: -5 with them
    /// (<see cref="RoleTone.DefiedCost"/>) and a grudge of fifteen, a Threatening one's thirty, their rider's.</para>
    ///
    /// <para>Every change goes through the writers the engine already has: <see cref="Change"/> for the player's
    /// standing, which draws the season's reciprocal roll - only a season at Roles reaches it - and the story effect
    /// router (<see cref="ApplyStoryEffect"/>) for a grudge, its easing and a favour. Filing the asks stays
    /// roll-free (<see cref="NpcDeals.AskForTheVeto"/>), and nothing new is stored: the tones are read again.</para>
    ///
    /// <para><b>The pawn conversation</b> (ROLE-MOMENTS-PLAN R6, §3.4a). A houseguest Head of Household who names the
    /// player as one of the week's first two nominees may call them up and say they are only a pawn, in any week, on a
    /// keyed coin, once per Head of Household a season, and only where the card can be put to them there and then
    /// (<see cref="PawnTalkComes"/>). In a backdoor week the backdoor's own "Just a Pawn" plays it, its plan going quiet
    /// when the coin says no; in any other week the pawn conversation does (<see cref="StartThePawnTalk"/>), with the
    /// same card, so the card is a claim and never a tell. Neither holds the Game lane (<see cref="LeavesTheLaneOpen"/>).
    /// It is stored as a storyline like any other, and the coin is drawn from the story's keyed stream, never the season's.</para>
    /// </summary>
    public sealed partial class EpisodeEngine
    {
        /// <summary>The ledger's type for an offer the player left unanswered until it lapsed: a change in how they feel, never a breach, and one that fades.</summary>
        public const string LapsedOfferEvent = "deal_lapsed";

        /// <summary>
        /// The ledger's type for the veto used after the Head of Household asked the player to leave the block alone, and was
        /// turned down or left hanging (R4): a change in how they feel, never a breach - the player gave no word - and one that fades.
        /// </summary>
        public const string DefiedAskEvent = "veto_defied";

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
        /// What an honest no to this offer costs with its maker: under the role moments, for a card of the veto board - a
        /// nominee's veto ask, or the Head of Household's ask to keep the block (R4) - its card's tone's no
        /// (<see cref="RoleOffers.ToneOf"/>), read before the answer changes anything; otherwise, and for every other
        /// offer, the no it always cost (<see cref="PlayerDeals.DeclineImpact"/>).
        /// </summary>
        private static double DeclineCost(EpisodeState s, DealState offer) =>
            RolesOn(s) && RoleOffers.IsBoardAsk(s, offer) && RoleTone.Stakes(RoleOffers.ToneOf(s, offer)) is RoleTone.Stake stake
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
        /// The veto board as the player last saw it: every one of this week's veto asks and the Head of Household's ask to
        /// keep the block, answered or not (<see cref="RoleOffers.IsBoardAsk"/>), in the board's public order, each with the
        /// tone its card wore and how it stood. Read before the decision's command moves anything - its last beats and the
        /// decision itself - so silence and the riders settle on the tone the card showed. Null, reading nothing, without
        /// the role moments.
        /// </summary>
        private static List<BoardCard> VetoBoardAsSeen(EpisodeState s) =>
            !RolesOn(s) ? null
                : RoleOffers.InBoardOrder(s, s.deals.Where(d => RoleOffers.IsBoardAsk(s, d)))
                    .Select(d => new BoardCard { askId = d.id, askerId = d.proposerId, tone = RoleOffers.ToneOf(s, d), status = d.status }).ToList();

        /// <summary>
        /// What the veto decision left of the board (<see cref="VetoBoardAsSeen"/>), settled right after it in the same
        /// command: each card in turn, on the tone it wore as the player last saw it. An ask the decision lapsed - still
        /// waiting when it was made - costs the tone's lapse with its maker, through <see cref="Change"/>, and the player
        /// is told so; then the tone's rider, where it has one - kept, a Bitter maker's grudge eases and a Desperate one
        /// owes the player a favour; turned down or left hanging, and left on the block, a Threatening one holds a
        /// grudge. The Head of Household's card (R4) settles its own way (<see cref="SettleTheHold"/>). Nothing for a
        /// maker no longer in the house, and nothing at all without the role moments.
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
                if (NpcDeals.IsKeepTheBlockId(ask.id))
                {
                    SettleTheHold(s, ask, asker, stake, hanging, key);
                    continue;
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

        /// <summary>
        /// The Head of Household's card, settled after the decision (R4, §3.3), its silence already charged: taken and kept -
        /// the veto left unused - a Bitter one's grudge against the player eases by thirty; taken and broken, the breach was
        /// settled with the decision (<see cref="DealResolution.Hold"/>) and nothing is added to it here. Turned down or left
        /// hanging, and the veto used anyway, they asked and were defied: <see cref="RoleTone.DefiedCost"/> with them through
        /// <see cref="Change"/>, never a breach - the player gave no word - and a grudge of <see cref="RoleTone.DefiedGrudge"/>,
        /// or a Threatening one's rider, thirty; and the player is told. Left unused, a no or silence costs nothing more.
        /// </summary>
        private static void SettleTheHold(EpisodeState s, DealState ask, ContestantState hoh, RoleTone.Stake stake, bool hanging, string key)
        {
            if (stake.rider == RoleTone.Riders.EasesTheirGrudge && ask.status == DealStatus.Fulfilled)
                Rider(s, StoryEffects.Ease, hoh.id, s.playerId, stake.riderAmount, null, key);
            if (!(hanging || ask.status == DealStatus.Declined) || !VetoUsedThisWeek(s)) return;
            Change(s, s.playerId, hoh.id, RoleTone.DefiedCost, "Used the veto after I asked them not to.", DefiedAskEvent);
            Rider(s, StoryEffects.Grudge, hoh.id, s.playerId,
                stake.rider == RoleTone.Riders.HoldsAGrudge ? stake.riderAmount : RoleTone.DefiedGrudge, GrudgeCauses.Story, key);
            Log(s, "deal", "You used the veto after " + hoh.name + " asked you not to.", s.playerId, hoh.id);
        }

        /// <summary>Whether this week's veto was used, as the decision recorded it on the week's power row.</summary>
        private static bool VetoUsedThisWeek(EpisodeState s) => s.ledger?.power?.LastOrDefault(p => p != null && p.week == s.week)?.vetoUsed == true;

        /// <summary>A rider, through the story effect router as a story option's consequence goes: no cycle, nobody's reception, no backfire.</summary>
        private static void Rider(EpisodeState s, string kind, string fromId, string toId, double amount, string type, string key) =>
            ApplyStoryEffect(s, new StoryEffectState { kind = kind, fromId = fromId, toId = toId, amount = amount, type = type }, null, null, key, 0, false);

        // ---------------------------------------------------------------- the pawn conversation (R6, §3.4a)

        /// <summary>The pawn conversation's arc: "Just a Pawn" in a week the Head of Household plans no backdoor (R6).</summary>
        internal const string PawnTalkArc = "the-pawn-talk";

        /// <summary>
        /// The opener both variants share, and the route it takes to the card: the backdoor's plan and the pawn conversation's
        /// record the same step, so <see cref="PawnTalkHad"/> reads either.
        /// </summary>
        internal const string PawnPlanBeat = "plan", PawnRoute = "pawn";

        /// <summary>The coin's chance (§3.4a; tunable - §5.5 names 0.33 as the first saving if the season runs long).</summary>
        public const double PawnTalkChance = 0.5;

        /// <summary>
        /// The fewest houseguests the pawn conversation comes with: the backdoor's own floor of six (<see cref="NpcBackdoorTarget"/>),
        /// so the card comes only where a plan could stand behind it. Below that it dilutes no tell, and buys little: at the final
        /// four there is one voter and no tie-break for a safety pact to weigh in. Its own constant (the owner's call to tune), so
        /// moving it never moves an old season's backdoor.
        /// </summary>
        public const int PawnTalkMinActive = 6;

        /// <summary>
        /// Whether the pawn conversation comes this week (R6, §3.4a): under the role moments, once the names are said, when a
        /// houseguest Head of Household has named the player as one of the week's first two nominees, first or second, with six or
        /// more in the house (<see cref="PawnTalkMinActive"/>) and room to put the card to the player there and then
        /// (<see cref="PawnTalkAirtime"/>), on the week's keyed coin (<c>w{week}:pawn-talk:{hohId}</c>, the story's stream, never the
        /// season's), and at most once per Head of Household a season, whichever variant came before. The backdoor's plan reads it,
        /// and goes quiet on a no; in any other week <see cref="StartThePawnTalk"/> does. Never whether a plan stands behind it.
        /// </summary>
        public static bool PawnTalkComes(EpisodeState s)
        {
            if (!RolesOn(s) || !s.nominees.Contains(s.playerId) || s.Active.Count() < PawnTalkMinActive || !PawnTalkAirtime(s)) return false;
            var hoh = StoryPeople.NpcHoh(s);
            return hoh != null && !PawnTalkHad(s, hoh.id) && StoryRandom.Chance(s, "w" + s.week + ":pawn-talk:" + hoh.id, PawnTalkChance);
        }

        /// <summary>
        /// Whether Just a Pawn can be put to the player as the names are said (R6's review): past the story's first ask
        /// (<see cref="BeforeTheFirstAsk"/>), with no other ask open and one of the week's two left - the airtime its card is drawn
        /// on a moment later, so a conversation that comes is always on the table at once. Its words are "before the veto players
        /// are drawn" and it closes at the veto competition, but a card put off never asks its pulse again: one put off in week one
        /// surfaced after the eviction, and one put off with the week's asks spent at the next week's nominations, naming last
        /// week's Head of Household, writing its promises a week late. So it does not wait. Without the room nobody is called up, in
        /// either variant alike, nothing is recorded, and that Head of Household may call the player up in another week.
        /// </summary>
        private static bool PawnTalkAirtime(EpisodeState s) => !BeforeTheFirstAsk(s) && AirtimeFor(s, StorySurfaces.Conversation);

        /// <summary>
        /// Whether a running cycle leaves its lane to the house's other arcs (R6's review): under the role moments the backdoor and
        /// the pawn conversation, which the rules start whatever the lane holds, run beside the Game lane rather than in it. Each
        /// runs to eviction night, so in the very weeks the player is on the block it kept out Call in the Favour - the favour R3's
        /// Desperate rider and quiet tears' "Sit with them a while" leave owed - and Final Plea; and both alike, so what else may come
        /// never says which one is running. Every other season's backdoor holds the lane as it always did.
        /// </summary>
        private static bool LeavesTheLaneOpen(EpisodeState s, StorylineState cycle) =>
            (cycle.templateId == BackdoorArc || cycle.templateId == PawnTalkArc) && RolesOn(s);

        /// <summary>
        /// Whether this Head of Household has had the pawn conversation with the player this season: a backdoor's plan or a pawn
        /// conversation, with them in the chair, that took the route to the card - started, which under the role moments puts the
        /// card to the player at once (an older week's backdoor card may have waited, or never found airtime). Read from the
        /// season's storylines, which nothing prunes, so nothing new is stored.
        /// </summary>
        public static bool PawnTalkHad(EpisodeState s, string hohId) =>
            hohId != null && s.storylines.Any(x => (x.templateId == BackdoorArc || x.templateId == PawnTalkArc)
                && x.cast.Any(r => r.role == "HOH" && r.contestantId == hohId)
                && x.path.Any(p => p.beatId == PawnPlanBeat && p.optionId == PawnRoute));

        /// <summary>
        /// The pawn conversation in a week the Head of Household plans no backdoor (R6, §3.4a): started where the backdoor starts,
        /// as the names are said, so its card is put to the player at once, as the backdoor's Just a Pawn is under the role moments
        /// (<see cref="PawnTalkAirtime"/>) - and rule-driven, like the backdoor, neither drawn from the pool nor held behind the Game
        /// lane, nor holding it (<see cref="LeavesTheLaneOpen"/>). In a backdoor week the plan has already taken the same coin, and
        /// on a yes recorded the conversation (<see cref="PawnTalkHad"/>), so this never starts a second. Nothing without the role
        /// moments.
        /// </summary>
        private static void StartThePawnTalk(EpisodeState s)
        {
            if (!PawnTalkComes(s)) return;
            var arc = StoryCatalog.Find(PawnTalkArc);
            if (arc == null) return;
            StartCycle(s, arc, StoryCatalog.Bind().With("HOH", s.hohId), StoryAnchors.NomsSet);
        }
    }
}
