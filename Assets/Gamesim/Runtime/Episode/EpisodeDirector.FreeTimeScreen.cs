using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>
    /// Free time as a screen (playtest, 2026-09-28). It was a column of paragraphs: the location,
    /// the meter, a heading and a sentence on meetings over two rows, a sentence on buying time over
    /// two rows, a sentence on exploring, a sentence on listening in over a row, then the way on -
    /// and the one decision the screen exists for, who to talk to, was nowhere on it. Now, as the
    /// campaign draws its votes: the house as cards, those in the room with you first, each one
    /// press from walking over to talk, with the latest thing you have on them; and the moves that
    /// name nobody as tiles that say in a line what each does. Every rule the paragraphs stated is
    /// still said, on the tile it belongs to.
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        private void FreeTimeScreen(EpisodeState state)
        {
            // Before anything the player chose to do: something has happened to them, and a
            // situation buried under the ordinary controls is a situation they will not see.
            if (!PendingReplyCard(state)) PendingHouseEvent(state);
            CurrentLocation(state);
            // The web build draws this as a bar you can watch drain rather than a sentence you
            // have to read and subtract. The caption still carries the numbers.
            int budget = EpisodeEngine.SocialActionBudget(state);
            hud.Meter("Interactions available",
                Mathf.Max(0, budget - EpisodeEngine.SocialActionsSpent(state)), budget, UiTheme.Accent);
            string rule = BudgetRule(state);
            if (rule != null) hud.Paragraph(rule);
            // The stories running and what they left behind, beside the budget they draw on.
            StorylinesBlock(state);
            if (state.playerStudyBonus > 0)
                hud.Paragraph("Preparation banked for competitions: " + state.playerStudyBonus + "/5.");
            // Beside the meter it takes a conversation from.
            if (HaveNots.Is(state, state.playerId)) hud.Paragraph(HaveNotLine);
            HouseAsCards(state);
            HouseMoves(state);
        }

        /// <summary>
        /// What the week gives, in a line under the meter: under the windows, which window this is
        /// and that its seats do not carry; under the old pool, the pool's rule. Null when no window
        /// is open.
        /// </summary>
        public static string BudgetRule(EpisodeState state)
        {
            if (!EpisodeEngine.WeekRulesOn(state))
                return "The house gives you half its number in actions each week, so the budget tightens as people leave.";
            int window = EpisodeEngine.Window(state);
            if (window == Windows.None) return null;
            return Windows.Names[window] + ". What you do not spend here does not carry to the next window.";
        }

        /// <summary>
        /// The house as cards: those in the room with you first, the week's role on each photo (the
        /// Head of Household, the block, the veto, or simply here), where you stand by your own
        /// reading, and the latest thing you have on them from your notes. Pressing one walks over.
        /// </summary>
        private void HouseAsCards(EpisodeState state)
        {
            var here = HouseOccupancy(state)
                .FirstOrDefault(room => room.Occupants != null && room.Occupants.Any(person => person.IsPlayer));
            var hereIds = new HashSet<string>((here.Occupants ?? new List<HouseMap.Occupant>())
                .Where(person => !person.IsPlayer).Select(person => person.Id));
            // Those in the room first; a stable sort keeps cast order within each half.
            var others = state.Active.Where(c => !c.isPlayer).OrderByDescending(c => hereIds.Contains(c.id) ? 1 : 0).ToList();
            if (others.Count == 0) return;
            hud.Eyebrow("WHO TO TALK TO", UiTheme.Muted);
            var cards = others.Select(c =>
            {
                string role = null; Color colour = UiTheme.Muted;
                if (c.id == state.hohId) { role = "HOH"; colour = UiTheme.Gold; }
                else if (state.nominees.Contains(c.id)) { role = "NOM"; colour = UiTheme.Danger; }
                else if (c.id == state.vetoHolderId) { role = "VETO"; colour = UiTheme.Accent; }
                else if (hereIds.Contains(c.id)) { role = "HERE"; colour = UiTheme.Allied; }
                return new EpisodeHud.HouseCard(c.id, role, colour, HouseguestNotes.Brief(state, c.id));
            }).ToList();
            hud.HouseCards(cards, TalkFromCampaign);
        }

        /// <summary>
        /// The moves that name nobody, as tiles: the two house meetings, listening in, and the two
        /// ways of buying time, each saying what it does and what it costs before it is pressed,
        /// with its category in the corner. On the campaign they wait behind "More ways to campaign".
        /// </summary>
        private void HouseMoves(EpisodeState state)
        {
            if (!state.Active.Any(c => !c.isPlayer)) return;
            hud.Eyebrow("THE WHOLE HOUSE", UiTheme.Muted);
            var tiles = new List<EpisodeHud.MoveTile>
            {
                Tile(EpisodeHud.RallyHouseCaption, "Moves everybody at once: mostly warmer, with one sceptic.",
                    EpisodeCommandKind.HouseMeeting, "people", () => Commit(state, EpisodeCommandKind.HouseMeeting, text: EpisodeEngine.RallyTroops)),
                Tile(EpisodeHud.AirLaundryCaption, "No middle ground: each housemate comes down with you or against you.",
                    EpisodeCommandKind.HouseMeeting, "target", () => Commit(state, EpisodeCommandKind.HouseMeeting, text: EpisodeEngine.AirDirtyLaundry)),
            };
            // Listening in needs no one to talk to, so it sits here rather than in a conversation.
            if (state.Active.Count(c => !c.isPlayer) >= 2)
                tiles.Add(Tile("Listen in on a conversation", "Works seven times in ten; the rest of the time somebody notices.",
                    EpisodeCommandKind.Eavesdrop, "eye", () => Commit(state, EpisodeCommandKind.Eavesdrop)));
            int left = WebSocialVocabulary.PurchaseCeiling - state.boughtActionPoints;
            if (left > 0)
            {
                string purchases = left + (left == 1 ? " purchase" : " purchases") + " left.";
                tiles.Add(Tile(EpisodeHud.BuyBurnOneCaption, "One more interaction for " + Mathf.Abs((int)WebSocialVocabulary.BurnOneCost)
                        + " goodwill with one housemate. " + purchases,
                    EpisodeCommandKind.BuyActionPoint, "exit", () => Commit(state, EpisodeCommandKind.BuyActionPoint, text: WebSocialVocabulary.BurnOne)));
                tiles.Add(Tile(EpisodeHud.BuySpreadCaption, "One more interaction for " + Mathf.Abs((int)WebSocialVocabulary.SpreadAllCost)
                        + " goodwill with every housemate. " + purchases,
                    EpisodeCommandKind.BuyActionPoint, "chat", () => Commit(state, EpisodeCommandKind.BuyActionPoint, text: WebSocialVocabulary.SpreadAll)));
            }
            hud.MoveTiles(tiles);
            if (left <= 0)
                hud.Paragraph("You have bought as much time as the house will give you this " + (EpisodeEngine.LeverRulesOn(state) ? "week" : "season") + ".");
        }

        private EpisodeHud.MoveTile Tile(string caption, string description, EpisodeCommandKind kind, string glyph, System.Action choose)
        {
            string category = Category(kind);
            return new EpisodeHud.MoveTile { Caption = caption, Description = description, Corner = category, CornerTint = CategoryTint(category), Glyph = glyph, Choose = choose };
        }

        /// <summary>A category's colour on a tile's corner: risky in the warning amber, strategic in the action blue, social in the allied green.</summary>
        private static Color CategoryTint(string category) =>
            category == "risky" ? UiTheme.Joke : category == "strategic" ? UiTheme.Accent : category == "social" ? UiTheme.Allied : UiTheme.Muted;
    }
}
