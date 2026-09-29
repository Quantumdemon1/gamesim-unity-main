using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;

namespace Gamesim.Episode
{
    /// <summary>
    /// What the overview's dashboard says (EpisodeHud.Overview.cs): the week in a line, the house
    /// at a glance, the plays and the threads, the phase's rule, and the smart moves for where
    /// things stand - each a control the player can press from the briefing. Read from committed
    /// state; nothing here decides anything.
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>The dashboard's way to the episode screen: a caption of its own, beside the rail's.</summary>
        public const string OverviewStationCaption = "Head to the episode screen";

        /// <summary>The dashboard's copy of the free-time moves, and its own way on.</summary>
        public const string OverviewListenCaption = "Listen in on a conversation";

        public EpisodeHud.DashboardView Dashboard(EpisodeState state)
        {
            var view = new EpisodeHud.DashboardView();
            if (state == null) return view;
            int remaining = state.Active.Count();
            view.Title = "OVERVIEW";
            view.Headline = "WHERE THINGS STAND";
            view.Hint = "Week " + state.week + "  ·  " + remaining + (remaining == 1 ? " houseguest remains" : " houseguests remain")
                + "  ·  " + EpisodeHud.PhaseShort(state.phase);
            view.GlanceIds = state.Active.Select(c => c.id).ToList();
            view.PickGlance = OpenGlanceProfile;
            foreach (var play in EpisodeEngine.Plays(state).Where(p => p.ending == null))
                view.Plays.Add((play.title + (PlaySubject(state, play) is ContestantState who ? " — " + who.name : ""), PlayLine(play)));
            foreach (var thread in EpisodeEngine.Threads(state).Where(t => t.ending == null))
                view.Threads.Add((thread.label, ThreadLine(thread)));
            view.PhaseRule = PhaseRule(state);
            view.RecommendedHint = "Smart moves for where things stand right now.";
            view.Recommended = RecommendedMoves(state);
            return view;
        }

        /// <summary>A glance card pressed: their profile, which ends the overview as any page does.</summary>
        private void OpenGlanceProfile(string id)
        {
            if (id == null) return;
            ShowHouseguestProfile(id);
        }

        /// <summary>The phase's rule in a line, the way the dashboard states it.</summary>
        public static string PhaseRule(EpisodeState state)
        {
            switch (state.phase)
            {
                case EpisodePhase.Social:
                    return "This is Free Time. You can talk, listen, and take actions around the house. Actions do not carry over when Free Time ends.";
                case EpisodePhase.Campaign:
                    return "This is the campaign. The house votes when you close campaigning; every conversation counts.";
                case EpisodePhase.Nomination:
                    return "The Head of Household names two houseguests for eviction. Whoever is named can still save themselves with the veto.";
                case EpisodePhase.VetoSelection:
                    return "The veto's players are drawn: the Head of Household and both nominees by right, and the rest from the house.";
                case EpisodePhase.VetoMeeting:
                    return "The veto holder decides whether the block stands. A save puts the Head of Household's replacement up.";
                case EpisodePhase.Eviction:
                    return "The house votes tonight. The Head of Household votes only to break a tie.";
                case EpisodePhase.HoH:
                case EpisodePhase.Veto:
                case EpisodePhase.FinalHoHPart1:
                case EpisodePhase.FinalHoHPart2:
                case EpisodePhase.FinalHoHPart3:
                    return "A competition is next. Whoever wins it holds the week's power; preparation banked beforehand counts.";
                default:
                    return PhaseTitle(state.phase);
            }
        }

        /// <summary>
        /// The smart moves for where things stand: the houseguest worth talking to, listening in
        /// and the house meeting while free time is on and there is an action to spend, and the way
        /// on. Each ends the overview and does what the free-time screen's own control does.
        /// </summary>
        private List<EpisodeHud.MoveTile> RecommendedMoves(EpisodeState state)
        {
            var tiles = new List<EpisodeHud.MoveTile>();
            bool freeTime = state.phase == EpisodePhase.Social && state.Find(state.playerId)?.status == ContestantStatus.Active;
            int left = ActionsLeftCount(state);
            var target = RecommendedTarget(state, out string why);
            if (target != null && freeTime && left > 0)
            {
                string id = target.id, first = (target.name ?? "").Split(' ')[0];
                tiles.Add(new EpisodeHud.MoveTile
                {
                    Caption = EpisodeHud.CastTalkCaption(first), Description = why, Glyph = "chat",
                    Value = "High value", ValueTint = UiTheme.Allied, Corner = "Low risk", CornerTint = UiTheme.Allied, Foot = "Cost: 1 action",
                    Choose = () => { EndOverview(); TalkFromCastMenu(id); },
                });
            }
            if (freeTime && left > 0 && state.Active.Count(c => !c.isPlayer) >= 2)
                tiles.Add(new EpisodeHud.MoveTile
                {
                    Caption = OverviewListenCaption, Description = "Find out what people are really saying. Works seven times in ten.", Glyph = "eye",
                    Value = "Good intel", ValueTint = UiTheme.Accent, Corner = "Some risk", CornerTint = UiTheme.Joke, Foot = "Cost: 1 action",
                    Choose = () => { EndOverview(); Commit(state, EpisodeCommandKind.Eavesdrop); },
                });
            if (freeTime && left > 0)
                tiles.Add(new EpisodeHud.MoveTile
                {
                    Caption = EpisodeHud.RallyHouseCaption, Description = "Get everyone in one room and shape the narrative.", Glyph = "people",
                    Value = "High impact", ValueTint = UiTheme.Strategic, Corner = "Higher risk", CornerTint = UiTheme.Joke, Foot = "Cost: 1 action",
                    Choose = () => { EndOverview(); Commit(state, EpisodeCommandKind.HouseMeeting, text: EpisodeEngine.RallyTroops); },
                });
            tiles.Add(new EpisodeHud.MoveTile
            {
                Caption = OverviewStationCaption, Glyph = "camera",
                Description = freeTime ? "You've done what you can. End free time and begin the competition." : "Continue the week where it is decided.",
                Value = "Safe choice", ValueTint = UiTheme.Allied, Corner = freeTime ? "Ends free time" : "Next step", CornerTint = UiTheme.Muted,
                Foot = freeTime && left > 0 ? left + (left == 1 ? " unused action" : " unused actions") + " will be lost." : null,
                Choose = () => { EndOverview(); GoToStation(); },
            });
            return tiles;
        }

        /// <summary>
        /// Who is worth talking to now: the person a running play is about, else whoever has put
        /// something to you and waits on an answer, else the houseguest you are warmest with who is
        /// not yet an ally. Null with nobody to talk to.
        /// </summary>
        private static ContestantState RecommendedTarget(EpisodeState state, out string why)
        {
            why = null;
            foreach (var play in EpisodeEngine.Plays(state).Where(p => p.ending == null && p.takenOn))
            {
                var subject = PlaySubject(state, play);
                if (subject == null) continue;
                why = play.goal;
                return subject;
            }
            var offer = NpcDeals.Pending(state).FirstOrDefault(d => state.Find(d.proposerId)?.status == ContestantStatus.Active);
            if (offer != null)
            {
                why = DealSentence(state, offer);
                return state.Find(offer.proposerId);
            }
            var warmest = state.Active.Where(c => !c.isPlayer && !state.Allied(state.playerId, c.id))
                .OrderByDescending(c => state.Score(state.playerId, c.id)).FirstOrDefault()
                ?? state.Active.FirstOrDefault(c => !c.isPlayer);
            if (warmest != null) why = "Build trust and learn where " + warmest.name.Split(' ')[0] + " stands.";
            return warmest;
        }
    }
}
