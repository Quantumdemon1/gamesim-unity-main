using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;

namespace Gamesim.Episode
{
    /// <summary>
    /// The campaign as a screen (playtest, 2026-09-27). It was fourteen blocks down one column - the
    /// location, the meter, the storylines, the whole house's meetings with a paragraph each, the
    /// price of more interactions, listening in and its odds - a wall of text before the one way on.
    /// Then, as the web's campaign stage draws it: who is on the block, as faces; the interactions
    /// left; the votes as a grid of cards, each one press from walking over to talk.
    ///
    /// <para>Now one board on the strategy stage (PACK8-PASS-PLAN B4, mockup 83;
    /// EpisodeHud.CampaignBoard.cs): the plea when a nominee came pleading, the block as heroes,
    /// the week's situation, the tabs, and the goals, the intel and a tip. The rest still waits
    /// behind "More ways to campaign" for the player who wants it. The director hands the board the
    /// words the rules and its own view state decide; the tab and the page are view state, reset
    /// with each week's campaign, and nothing on the board commits anything but its controls.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        public const string CampaignMoreCaption = "More ways to campaign", CampaignLessCaption = "Fewer ways to campaign";

        /// <summary>The campaign's headline over its title (mockup 83): the player's own case from the block, or the vote to swing.</summary>
        public const string CampaignOnTheBlockHeadline = "Build your case", CampaignSwingHeadline = "Swing the vote";

        /// <summary>The footer's strip on the campaign: what closing it opens.</summary>
        public const string CampaignUpNext = "Up next: eviction night, when the house votes.";

        /// <summary>The campaign's tip: the notebook's own words for what a read is.</summary>
        public const string CampaignTip = "A read is what you have learned, not a ballot. Ask them straight, read them, listen in, or hear it from an ally.";

        /// <summary>Whether the campaign's other ways - the whole house, listening in, where you are - are open. View state.</summary>
        private bool campaignMore;

        /// <summary>The board's tab and page, and the week they were chosen in. View state: never saved, and a new week's campaign opens on its houseguests.</summary>
        private EpisodeHud.CampaignTab campaignTab;
        private int campaignPage, campaignWeek = -1;

        private void CampaignScreen(EpisodeState state)
        {
            if (campaignWeek != state.week)
            {
                campaignWeek = state.week;
                campaignTab = EpisodeHud.CampaignTab.Talk;
                campaignPage = 0;
            }
            hud.CampaignColumn();
            var card = ReplyCards.Pending(state);
            // Something that has happened to the player comes first, as it always has: a plea on the
            // board itself, a house event still waiting over it.
            if (card == null) PendingHouseEvent(state);
            bool active = state.Find(state.playerId)?.status == ContestantStatus.Active;
            bool waiting = card != null || state.houseEvents.Any(e => !e.resolved && !e.IsStory)
                || (active && EpisodeEngine.OpenStoryBeats(state).Count > 0);
            var spec = new EpisodeHud.CampaignBoardSpec
            {
                Headline = state.nominees.Contains(state.playerId) ? CampaignOnTheBlockHeadline : CampaignSwingHeadline,
                Line = "The house votes when you close campaigning.",
                Tip = CampaignTip,
                Tab = campaignTab,
                Page = campaignPage,
                More = campaignMore,
                HaveNot = HaveNots.Is(state, state.playerId),
                Folded = waiting,
                TalkingPoints = CampaignTalkingPoints(state),
                Stories = CampaignStoryRows(state),
                Goals = CampaignBrief.Goals(state),
                Intel = CampaignBrief.RecentIntel(state),
                Talk = TalkFromCampaign,
                ChooseTab = tab => { campaignTab = tab; campaignPage = 0; Render(); },
                ChoosePage = page => { campaignPage = page; Render(); },
                ToggleMore = () => { campaignMore = !campaignMore; Render(); },
            };
            if (card != null)
            {
                string cardId = card.id;
                var next = state.replyCards.Skip(1).FirstOrDefault();
                spec.PleaTitle = ReplyCards.Title(state, card);
                spec.PleaMessage = ReplyCards.Message(state, card);
                spec.PleasWaiting = state.replyCards.Count;
                spec.PleaNext = next != null ? state.Find(next.fromId)?.name : null;
                spec.Replies = ReplyCards.Replies(card.kind).Select(reply =>
                {
                    string key = reply.Key;
                    return new EpisodeHud.CampaignReply
                    {
                        Caption = EpisodeHud.ReplyCaption(reply.Label), Key = key, Description = reply.Description,
                        Risk = EpisodeHud.RiskTag(reply.Risk),
                        Choose = () => Commit(state, EpisodeCommandKind.ReplyToHouseguest, cardId, text: key),
                    };
                }).ToList();
            }
            hud.CampaignBoard(spec);
            // What closing the campaign opens, in the footer's strip beside the way on; a storyline
            // that moving on lets pass outranks it there.
            hud.PinnedNote(CampaignUpNext, null, false);
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

        /// <summary>
        /// What this week's rules let the player do in a conversation on the campaign, in plain words
        /// with where each stands: the question and the look, both free and once a week each; a vote
        /// promise; from the block, a plea to each voter; and a call in each of the player's pacts.
        /// Never a conversation's caption: those are the conversation's to show.
        /// </summary>
        private static List<string> CampaignTalkingPoints(EpisodeState state)
        {
            var points = new List<string>();
            var voters = EpisodeEngine.Voters(state).Where(voter => !voter.isPlayer && voter.status == ContestantStatus.Active).ToList();
            if (VoteRead.Available(state) && voters.Count > 0)
            {
                int asked = voters.Count(voter => EpisodeEngine.AskedThisWeek(state, voter.id));
                points.Add("Ask a voter straight where their vote is: free, once a week each. " + asked + " of " + voters.Count + " asked.");
            }
            int others = state.Active.Count(actor => !actor.isPlayer);
            int read = state.Active.Count(actor => !actor.isPlayer && EpisodeEngine.ReadThisWeek(state, actor.id));
            if (others > 0) points.Add("Get a read on how somebody sees you: free, once a week each. " + read + " of " + others + " read.");
            if (voters.Count > 0 && state.nominees.Any(id => id != state.playerId))
                points.Add("Promise a voter to evict one of the nominees. A promise is kept or broken at the vote.");
            if (voters.Any(voter => StrategyRules.CanBeAskedForTheirVote(state, voter.id)))
                points.Add("From the block, ask each voter to keep you: one plea each, and each costs one of your conversations.");
            if (EpisodeEngine.LeverRulesOn(state) && VoteRead.Available(state))
                foreach (var pact in state.alliances.Where(a => a.active && a.members.Contains(state.playerId)))
                    points.Add(state.ledger.calls.Any(k => k.week == state.week && k.allianceId == pact.id)
                        ? "You have called the vote in " + pact.name + " this week."
                        : "Through one of " + pact.name + "'s members, name who the bloc evicts: once a week.");
            return points;
        }

        /// <summary>
        /// The Storylines tab's rows: the plays and the threads still going, the storylines running
        /// and what the stories left the player - the free-time panel's blocks, one row each - and
        /// the preparation banked for the competitions.
        /// </summary>
        private static List<(string Title, string Line)> CampaignStoryRows(EpisodeState state)
        {
            var rows = new List<(string Title, string Line)>();
            foreach (var play in EpisodeEngine.Plays(state).Where(p => p.ending == null)) rows.Add((play.title, PlayLine(play)));
            foreach (var thread in EpisodeEngine.Threads(state).Where(t => t.ending == null)) rows.Add((thread.label, ThreadLine(thread)));
            // The same storylines and modifiers StorylinesBlock lists, in its order and its words.
            foreach (var cycle in state.storylines.Where(x => x.beatId != null && StorylineStatus.Running(x.status)
                             && StoryCatalog.Find(x.templateId)?.play == null && x.lane != StoryLanes.Thread)
                         .OrderBy(x => x.week).ThenBy(x => x.id, StringComparer.Ordinal))
            {
                var arc = StoryCatalog.Find(cycle.templateId);
                bool waiting = state.houseEvents.Any(e => !e.resolved && e.cycleId == cycle.id);
                rows.Add((arc?.title ?? cycle.title, waiting ? "waiting on you" : "still playing out"));
            }
            foreach (var modifier in state.activeModifiers.Where(m => m.weeksLeft > 0 && string.IsNullOrEmpty(m.ownerId)))
            {
                string effect = string.Join(", ", new[]
                {
                    Math.Abs(modifier.competitionBonus) > 0.001 ? modifier.competitionBonus.ToString("+0;-0") + " in competitions" : null,
                    StoryModifiers.ActionsFrom(modifier.socialBonus) != 0 ? StoryModifiers.ActionsFrom(modifier.socialBonus).ToString("+0;-0") + " action" : null,
                }.Where(x => x != null));
                rows.Add((modifier.name, (effect.Length == 0 ? "" : effect + ", ") + modifier.weeksLeft + (modifier.weeksLeft == 1 ? " week left" : " weeks left")));
            }
            if (state.playerStudyBonus > 0) rows.Add(("Preparation banked for competitions", state.playerStudyBonus + "/5"));
            return rows;
        }
    }
}
