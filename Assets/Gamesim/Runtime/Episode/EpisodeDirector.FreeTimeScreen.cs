using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>
    /// Free time as three screens (the owner's review of the mockups, 2026-09-28): information and
    /// decisions no longer compete for one screen.
    ///
    /// <para><b>Free Time</b> is the root, the screen played every week: how many actions are left,
    /// large; the house as cards, each with the week's role on the photo, where you stand, the line
    /// the season has on them and a way to walk over; the moves that name nobody as cards with their
    /// risk and their cost; where you are, the play in motion and the threads in a column beside
    /// them; and the way on, with what it costs in unused actions under it.</para>
    ///
    /// <para><b>A houseguest's screen</b> opens from a card's name: the house in a strip to change
    /// your mind, the three ways to spend an action on them - talk privately, ask for information,
    /// pitch a deal - each with its cost and its risk, the whole house's moves under them, and your
    /// own context and what you have on them beside. Talking walks over and opens the conversation;
    /// asking and pitching do the same and put what you came for first in it.</para>
    ///
    /// <para><b>The overview</b> is the strategic dashboard (EpisodeHud.Overview.cs): the roles, the
    /// house at a glance, the plays, the threads, the phase's rules and the smart moves for where
    /// things stand, on the left-hand rail's Overview.</para>
    ///
    /// <para>Nothing here reads anything the player is not entitled to, commits anything on its
    /// own, or changes a caption: every control keeps the name a test finds it by.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>The captions of a houseguest's screen: what they are found by.</summary>
        public const string TalkPrivatelyCaption = "Talk privately", AskForInformationCaption = "Ask for information",
            PitchADealCaption = "Pitch a deal", BackToFreeTimeCaption = "Back to free time";

        /// <summary>The free-time screen's head at the Final 3 (ENDGAME-PLAN F1).</summary>
        public const string EndgamePreparationTitle = "ENDGAME PREPARATION";

        /// <summary>What a houseguest's screen sent the player into a conversation for, so the conversation puts it first.</summary>
        public const string IntentAsk = "ask", IntentDeal = "deal";

        /// <summary>The houseguest whose screen is open over free time, or null for the root. View state.</summary>
        private string moveScreenId;

        /// <summary>What the player came into the next conversation to do, or null: consumed when it closes.</summary>
        private string conversationIntent;

        /// <summary>Whose screen free time is on, or null on the root. A read for tests.</summary>
        public string HouseguestScreenFor => moveScreenId;

        /// <summary>The conversation's intent, or null. A read for tests.</summary>
        public string ConversationIntent => conversationIntent;

        /// <summary>Opens a houseguest's screen over free time. Public for tests.</summary>
        public void OpenHouseguestScreen(string id)
        {
            var actor = projected?.Find(id);
            if (actor == null || actor.isPlayer || actor.status != ContestantStatus.Active) return;
            moveScreenId = id;
            Render();
        }

        /// <summary>Back to the Free Time root.</summary>
        public void CloseHouseguestScreen()
        {
            moveScreenId = null;
            Render();
        }

        /// <summary>
        /// Walk over and talk, with what you came for: the panel closes, the walk begins, and the
        /// conversation, when it opens, puts the ask or the deal first. Public for tests.
        /// </summary>
        public void TalkWithIntent(string id, string intent)
        {
            ClosePanels();
            conversationIntent = intent;
            TalkFromCastMenu(id);
        }

        private void FreeTimeScreen(EpisodeState state)
        {
            var chosen = moveScreenId != null ? state.Find(moveScreenId) : null;
            if (chosen != null && !chosen.isPlayer && chosen.status == ContestantStatus.Active) { HouseguestScreen(state, chosen); return; }
            moveScreenId = null;
            // The comparison over Endgame Preparation (EpisodeDirector.FinalThree.cs), while the window lasts.
            if (comparingFinalists && Preparing(state)) { FinalistComparison(state); return; }
            comparingFinalists = false;
            // The final case read early, and the jury house, over Endgame Preparation
            // (EpisodeDirector.FinalCase.cs, EpisodeDirector.JuryHouse.cs), while the window lasts.
            if (FinalCaseIfOpen(state)) return;
            if (JuryHouseIfOpen(state)) return;
            // Before anything the player chose to do: something has happened to them, and a
            // situation buried under the ordinary controls is a situation they will not see.
            if (!PendingReplyCard(state)) PendingHouseEvent(state);
            int left = ActionsLeftCount(state);
            // At three this window is the endgame's preparation (ENDGAME-PLAN F1): the final-four
            // eviction opens it and leaving it starts the final Head of Household's first part.
            // The seat and the moves are the week's own; the head says what they are for now.
            // Only for a player who is one of the three: a juror watching the window is not preparing.
            if (Preparing(state))
                hud.ScreenHead(EndgamePreparationTitle, ActionsLeftHeadline(left),
                    "Three remain. Study in the Diary Room, settle who you trust, and get ready: the final Head of Household is next.");
            else
                hud.ScreenHead("FREE TIME", ActionsLeftHeadline(left), "Talk to a houseguest or make a move around the house.");
            // The week's rule under the head: which window this is and that its seats do not carry.
            string rule = BudgetRule(state);
            if (rule != null) hud.Footnote(rule);
            if (HaveNots.Is(state, state.playerId)) hud.Footnote(HaveNotLine, UiTheme.Warning);
            hud.BeginColumns(300f);
            HouseAsCards(state);
            HouseMoves(state, true);
            if (state.playerStudyBonus > 0) hud.Footnote("Preparation banked for competitions: " + state.playerStudyBonus + "/5.");
            hud.SideColumn();
            LocationCard(state);
            CurrentPlayCard(state);
            StoryCard(state);
            hud.EndColumns();
        }

        /// <summary>"1 ACTION LEFT", "3 ACTIONS LEFT", or "NO ACTIONS LEFT".</summary>
        public static string ActionsLeftHeadline(int left) =>
            left <= 0 ? "NO ACTIONS LEFT" : left + (left == 1 ? " ACTION LEFT" : " ACTIONS LEFT");

        /// <summary>What moving on costs, under the way on: the unused actions. Null when none are.</summary>
        public static string UnusedActionsNote(EpisodeState state)
        {
            if (state == null || state.phase != EpisodePhase.Social) return null;
            int left = ActionsLeftCount(state);
            return left <= 0 ? null : left + (left == 1 ? " unused action" : " unused actions") + " will be lost.";
        }

        private static int ActionsLeftCount(EpisodeState state) =>
            Mathf.Max(0, EpisodeEngine.SocialActionBudget(state) - EpisodeEngine.SocialActionsSpent(state));

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

        // ------------------------------------------------------------ the house as cards

        /// <summary>The houseguests other than the player, those in the room with you first; cast order within each half.</summary>
        private List<ContestantState> OthersHereFirst(EpisodeState state)
        {
            var here = HouseOccupancy(state)
                .FirstOrDefault(room => room.Occupants != null && room.Occupants.Any(person => person.IsPlayer));
            var hereIds = new HashSet<string>((here.Occupants ?? new List<HouseMap.Occupant>())
                .Where(person => !person.IsPlayer).Select(person => person.Id));
            return state.Active.Where(c => !c.isPlayer).OrderByDescending(c => hereIds.Contains(c.id) ? 1 : 0).ToList();
        }

        /// <summary>
        /// The house as cards: those in the room with you first, the week's role on each photo (the
        /// Head of Household, the block, the veto, or simply here), where you stand by your own
        /// reading, the play about them or the latest thing you have on them, a name that opens
        /// their screen, and a way to walk over.
        /// </summary>
        private void HouseAsCards(EpisodeState state)
        {
            var others = OthersHereFirst(state);
            if (others.Count == 0) return;
            var here = HouseOccupancy(state)
                .FirstOrDefault(room => room.Occupants != null && room.Occupants.Any(person => person.IsPlayer));
            var hereIds = new HashSet<string>((here.Occupants ?? new List<HouseMap.Occupant>())
                .Where(person => !person.IsPlayer).Select(person => person.Id));
            hud.SectionHead("people", "WHO DO YOU WANT TO TALK TO?", "Build relationships, gather information, and shape your game.");
            var cards = others.Select(c =>
            {
                string role = EpisodeHud.RoleFor(state, c.id, out var colour);
                if (role == null && hereIds.Contains(c.id)) { role = "HERE"; colour = UiTheme.Allied; }
                return new EpisodeHud.HouseCard(c.id, role, colour, CardLine(state, c.id));
            }).ToList();
            hud.HouseCards(cards, TalkFromCampaign, OpenHouseguestScreen);
        }

        /// <summary>The line under a name: the play about them and how far it is, else the latest thing you have on them.</summary>
        private static string CardLine(EpisodeState state, string id)
        {
            var play = PlayAbout(state, id);
            if (play != null) return PlayShort(play);
            return HouseguestNotes.Brief(state, id);
        }

        /// <summary>A play in two words and its count: "Know Them 0/3".</summary>
        private static string PlayShort(EpisodeEngine.PlayView play) =>
            play.title + (play.progress.need > 1 ? " " + play.progress.have + "/" + play.progress.need : play.progress.Met ? " done" : "");

        /// <summary>The running play the player has taken on that is about this houseguest, or null.</summary>
        private static EpisodeEngine.PlayView PlayAbout(EpisodeState state, string id)
        {
            if (state == null || id == null) return null;
            foreach (var play in EpisodeEngine.Plays(state))
            {
                if (play.ending != null || !play.takenOn) continue;
                var cycle = state.storylines.FirstOrDefault(x => x.id == play.cycleId);
                if (cycle?.cast != null && cycle.cast.Any(role => role.contestantId == id)) return play;
            }
            return null;
        }

        /// <summary>Who a play is about: its first cast member other than the player, or null.</summary>
        private static ContestantState PlaySubject(EpisodeState state, EpisodeEngine.PlayView play)
        {
            var cycle = state.storylines.FirstOrDefault(x => x.id == play.cycleId);
            if (cycle?.cast == null) return null;
            foreach (var role in cycle.cast)
            {
                var who = state.Find(role.contestantId);
                if (who != null && !who.isPlayer) return who;
            }
            return null;
        }

        /// <summary>The season's running threads this houseguest is in.</summary>
        private static List<EpisodeEngine.ThreadView> ThreadsWith(EpisodeState state, string id)
        {
            var threads = new List<EpisodeEngine.ThreadView>();
            if (state == null || id == null) return threads;
            foreach (var thread in EpisodeEngine.Threads(state))
            {
                if (thread.ending != null) continue;
                var cycle = state.storylines.FirstOrDefault(x => x.id == thread.cycleId);
                if (cycle?.cast != null && cycle.cast.Any(role => role.contestantId == id)) threads.Add(thread);
            }
            return threads;
        }

        // ------------------------------------------------------------ the moves that name nobody

        /// <summary>
        /// The moves that name nobody, as cards: the two house meetings, listening in, and the two
        /// ways of buying time, each saying what it does, its risk and what it costs before it is
        /// pressed. On the campaign they wait behind "More ways to campaign", as rows.
        /// </summary>
        private void HouseMoves(EpisodeState state, bool asCards = false)
        {
            if (!state.Active.Any(c => !c.isPlayer)) return;
            if (asCards) hud.SectionHead("home", "OTHER WAYS TO SPEND YOUR TIME", "Make a move, gather information, or shake things up around the house.");
            else hud.Eyebrow("THE WHOLE HOUSE", UiTheme.Muted);
            var tiles = new List<EpisodeHud.MoveTile>
            {
                Tile(EpisodeHud.RallyHouseCaption, "Rally the house for a meeting. Moves everybody at once: mostly warmer, with one sceptic.",
                    EpisodeCommandKind.HouseMeeting, "people", () => Commit(state, EpisodeCommandKind.HouseMeeting, text: EpisodeEngine.RallyTroops), "Risky"),
                Tile(EpisodeHud.AirLaundryCaption, "No middle ground: each housemate comes down with you or against you.",
                    EpisodeCommandKind.HouseMeeting, "target", () => Commit(state, EpisodeCommandKind.HouseMeeting, text: EpisodeEngine.AirDirtyLaundry), "High risk"),
            };
            // At three, first of all: the comparison, which costs nothing (ENDGAME-PLAN F2).
            if (asCards && Preparing(state)) tiles.Insert(0, CompareTile(OpenFinalistComparison));
            if (asCards && Preparing(state) && JuryHouseAvailable(state)) tiles.Insert(1, JuryHouseTile(OpenJuryHouse));
            // The final case to read early, beside the jury house, under the finale rules (MOCKUP-PASS M15).
            if (asCards && FinalCaseEarlyAvailable(state)) tiles.Insert(JuryHouseAvailable(state) ? 2 : 1, FinalCaseTile(OpenFinalCase));
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
                    EpisodeCommandKind.BuyActionPoint, "exit", () => Commit(state, EpisodeCommandKind.BuyActionPoint, text: WebSocialVocabulary.BurnOne), "Risky", "Gains 1 action"));
                tiles.Add(Tile(EpisodeHud.BuySpreadCaption, "One more interaction for " + Mathf.Abs((int)WebSocialVocabulary.SpreadAllCost)
                        + " goodwill with every housemate. " + purchases,
                    EpisodeCommandKind.BuyActionPoint, "chat", () => Commit(state, EpisodeCommandKind.BuyActionPoint, text: WebSocialVocabulary.SpreadAll), null, "Gains 1 action"));
            }
            hud.MoveTiles(tiles, asCards ? EpisodeHud.TileStyle.Cards : EpisodeHud.TileStyle.Rows);
            if (left <= 0)
                hud.Footnote("You have bought as much time as the house will give you this " + (EpisodeEngine.LeverRulesOn(state) ? "week" : "season") + ".");
        }

        private EpisodeHud.MoveTile Tile(string caption, string description, EpisodeCommandKind kind, string glyph, System.Action choose,
            string risk = null, string foot = "Cost: 1 action")
        {
            string category = Category(kind);
            string corner = risk ?? (category == "risky" ? "Risky" : category == "strategic" ? "Strategic" : category == "social" ? "Social" : category);
            return new EpisodeHud.MoveTile
            {
                Caption = caption, Description = description, Corner = corner, CornerTint = RiskTint(corner), Glyph = glyph, Foot = foot, Choose = choose,
            };
        }

        /// <summary>A risk or category word's colour on a tile: risk in the warning amber, high risk in red, strategic in the action blue, social in the allied green.</summary>
        private static Color RiskTint(string word)
        {
            switch (word)
            {
                case "Risky": case "risky": case "Medium risk": return UiTheme.Joke;
                case "High risk": return UiTheme.Danger;
                case "Strategic": case "strategic": return UiTheme.Accent;
                case "Social": case "social": case "Low risk": return UiTheme.Allied;
                default: return UiTheme.Muted;
            }
        }

        /// <summary>A category's colour on a tile's corner: risky in the warning amber, strategic in the action blue, social in the allied green.</summary>
        private static Color CategoryTint(string category) =>
            category == "risky" ? UiTheme.Joke : category == "strategic" ? UiTheme.Accent : category == "social" ? UiTheme.Allied : UiTheme.Muted;

        // ------------------------------------------------------------ the side column

        /// <summary>Where you are: the room, whether you have it to yourself, and who is in it with you.</summary>
        private void LocationCard(EpisodeState state)
        {
            var here = HouseOccupancy(state)
                .FirstOrDefault(room => room.Occupants != null && room.Occupants.Any(person => person.IsPlayer));
            if (string.IsNullOrEmpty(here.Name)) return;
            bool alone = !(here.Occupants ?? new List<HouseMap.Occupant>()).Any(person => !person.IsPlayer);
            hud.BeginSideCard(EpisodeHud.LocationCardName, "CURRENT LOCATION", alone ? "Private" : "Shared", alone ? UiTheme.Accent : UiTheme.Muted);
            hud.CardLine(RoomLabels.Title(here.Name), 20, UiTheme.Paper, UiTheme.Weight.SemiBold);
            hud.CardLine(CompanyLine(here, false), 13, UiTheme.Muted);
            hud.EndSideCard();
        }

        /// <summary>The play in motion: its name and who it is about, how far it is as a bar, and the goal in a line.</summary>
        private void CurrentPlayCard(EpisodeState state)
        {
            var plays = EpisodeEngine.Plays(state).Where(p => p.ending == null).ToList();
            var running = plays.FirstOrDefault(p => p.takenOn) ?? plays.FirstOrDefault();
            hud.BeginSideCard(EpisodeHud.CurrentPlayCardName, "CURRENT PLAY");
            if (running == null)
            {
                hud.CardLine("No play in motion.", 14, UiTheme.Muted);
                hud.CardLine("Plays are offered as the season turns; a Pull brings one to you.", 12, UiTheme.Muted);
                hud.EndSideCard();
                return;
            }
            var subject = PlaySubject(state, running);
            hud.CardLine(running.title + (subject != null ? " — " + subject.name : ""), 16, UiTheme.Paper, UiTheme.Weight.SemiBold);
            if (running.takenOn) hud.ProgressBar(PlayCurrencies.Label(running.currency), running.progress.have, running.progress.need, UiTheme.Accent);
            else hud.CardLine("On offer until " + StoryText.ClosesAt(running.deadline) + ".", 12, UiTheme.Gold);
            hud.CardLine(running.goal, 13, UiTheme.Muted);
            hud.EndSideCard();
        }

        /// <summary>
        /// The week's story, in the story's own blocks (EpisodeDirector.Story.cs, StorylinesBlock):
        /// the plays on offer and in play, the season's threads, the storylines running and what
        /// they left behind. One call, on every screen that shows the week's options, so a play on
        /// offer and the threads stay in view while actions are spent - the story session's ask.
        /// </summary>
        private void StoryCard(EpisodeState state)
        {
            int threads = EpisodeEngine.Threads(state).Count(t => t.ending == null);
            hud.BeginSideCard(EpisodeHud.ThreadsCardName, "THREADS & STORYLINES", threads > 0 ? threads.ToString() : null, UiTheme.Accent);
            if (!StoryInPlay(state)) hud.CardLine("Nothing in play yet. Threads start from what you do.", 13, UiTheme.Muted);
            StorylinesBlock(state);
            hud.EndSideCard();
        }

        /// <summary>Whether the story's blocks have anything to say: a play, a thread, a storyline running or a modifier of the player's.</summary>
        private static bool StoryInPlay(EpisodeState state) =>
            EpisodeEngine.Plays(state).Any(p => p.ending == null) || EpisodeEngine.Threads(state).Any(t => t.ending == null)
            || state.storylines.Any(x => x.beatId != null && StorylineStatus.Running(x.status) && StoryCatalog.Find(x.templateId)?.play == null && x.lane != StoryLanes.Thread)
            || state.activeModifiers.Any(m => m.weeksLeft > 0 && string.IsNullOrEmpty(m.ownerId));

        // ------------------------------------------------------------ a houseguest's screen

        /// <summary>
        /// A houseguest's screen: the house in a strip with them pressed, the three ways to spend an
        /// action on them, the whole house's moves, and beside them your own context and what you
        /// have on them.
        /// </summary>
        private void HouseguestScreen(EpisodeState state, ContestantState who)
        {
            hud.Action(BackToFreeTimeCaption, CloseHouseguestScreen);
            int left = ActionsLeftCount(state);
            hud.ScreenHead("FREE TIME", left <= 0 ? "NO ACTIONS LEFT" : left == 1 ? "SPEND YOUR LAST ACTION" : "SPEND AN ACTION",
                "Build relationships, gather information, or make a move. Choose who to interact with and how.");
            hud.HouseguestStrip(OthersHereFirst(state).Select(c => c.id).ToList(), who.id, OpenHouseguestScreen);
            string first = (who.name ?? "").Split(' ')[0];
            string id = who.id;
            hud.BeginColumns(300f);
            hud.SectionHead("chat", "TALK TO " + who.name.ToUpperInvariant(), "Choose how you want to spend your action with " + first + ".");
            hud.Tiles("Interaction tiles", new List<EpisodeHud.MoveTile>
            {
                new EpisodeHud.MoveTile
                {
                    Caption = TalkPrivatelyCaption, Description = "Have a one-on-one conversation with " + first + ".",
                    Corner = "Low risk", CornerTint = UiTheme.Allied, Glyph = "chat", Foot = "Cost: 1 action",
                    Choose = () => TalkWithIntent(id, null),
                },
                new EpisodeHud.MoveTile
                {
                    Caption = AskForInformationCaption, Description = "Try to learn what " + first + " knows or is planning.",
                    Corner = "Medium risk", CornerTint = UiTheme.Joke, Glyph = "eye", Foot = "Cost: 1 action",
                    Choose = () => TalkWithIntent(id, IntentAsk),
                },
                new EpisodeHud.MoveTile
                {
                    Caption = PitchADealCaption, Description = "Propose an alliance, trade, or mutual plan.",
                    Corner = "Medium risk", CornerTint = UiTheme.Joke, Glyph = "handshake", Foot = "Cost: 1 action",
                    Choose = () => TalkWithIntent(id, IntentDeal),
                },
            });
            hud.SectionHead("people", "WHOLE HOUSE MOVES", "Spend your action on a move that affects everyone.");
            HouseMoves(state, true);
            hud.SideColumn();
            YourContextCard(state);
            AboutCard(state, who);
            StoryCard(state);
            hud.EndColumns();
        }

        /// <summary>Your own week in three lines: your role, what you have left to spend, and where you are being sent next.</summary>
        private void YourContextCard(EpisodeState state)
        {
            hud.BeginSideCard(EpisodeHud.YourContextCardName, "YOUR CONTEXT");
            string role, why;
            // At three the final-four week's roles are still in state until the window closes; what
            // matters now is the final Head of Household (ENDGAME-PLAN F1).
            if (Preparing(state)) { role = "You are in the Final 3"; why = "The final Head of Household is next."; }
            else if (state.playerId == state.hohId) { role = "You are HOH"; why = "You can set the tone this week."; }
            else if (state.nominees != null && state.nominees.Contains(state.playerId)) { role = "You are on the block"; why = "Campaign for the votes you need."; }
            else if (state.playerId == state.vetoHolderId) { role = "You hold the veto"; why = "The meeting is yours to call."; }
            else { role = "You are safe this week"; why = "Spend the week on what comes next."; }
            hud.CardLine(role, 15, UiTheme.Gold, UiTheme.Weight.SemiBold);
            hud.CardLine(why, 12, UiTheme.Muted);
            int left = ActionsLeftCount(state);
            hud.CardLine(left + (left == 1 ? " action left" : " actions left"), 15, UiTheme.Paper, UiTheme.Weight.SemiBold);
            hud.CardLine(left == 1 ? "Choose carefully." : left == 0 ? "The week's actions are spent." : "Spend them well.", 12, UiTheme.Muted);
            hud.CardLine(EpisodeHud.ObjectiveTitle(state), 15, UiTheme.Paper, UiTheme.Weight.SemiBold);
            hud.CardLine(hud.NextStop(state), 12, UiTheme.Muted);
            hud.EndSideCard();
        }

        // ------------------------------------------------------------ what a conversation puts first

        /// <summary>
        /// The conversation's asking rows: what they have heard, the vote while there is one to ask
        /// about, and the read (STRATEGY-LOOP-PLAN.md section 2: free, once a week each). Drawn
        /// first when the player came to ask, in their usual place otherwise, never both.
        /// </summary>
        private void AskRows(EpisodeState state, ContestantState npc, bool window)
        {
            hud.Tag(hud.Action("Ask what they have heard", () => Commit(state, EpisodeCommandKind.AskForIntel, npc.id)),
                Category(EpisodeCommandKind.AskForIntel));
            // The question is for a voter while there is a vote to ask about; the look is for anyone,
            // in free time or the campaign.
            if (VoteRead.Available(state) && EpisodeEngine.Voters(state).Any(v => v.id == npc.id)
                && !EpisodeEngine.AskedThisWeek(state, npc.id))
                hud.Tag(hud.Action(EpisodeHud.AskVoteCaption, () => Commit(state, EpisodeCommandKind.AskVote, npc.id)),
                    Category(EpisodeCommandKind.AskVote));
            if (!window && !EpisodeEngine.ReadThisWeek(state, npc.id))
                hud.Tag(hud.Action(EpisodeHud.ReadPersonCaption, () => Commit(state, EpisodeCommandKind.ReadPerson, npc.id)),
                    Category(EpisodeCommandKind.ReadPerson));
        }

        /// <summary>
        /// The conversation's promise and alliance rows. Drawn first, with the deal table, when the
        /// player came to pitch a deal; in their usual place otherwise, never both.
        /// </summary>
        private void DealRows(EpisodeState state, ContestantState npc, bool allied)
        {
            hud.Tag(hud.Action("Promise safety", () => Commit(state, EpisodeCommandKind.PromiseSafety, npc.id)),
                Category(EpisodeCommandKind.PromiseSafety));
            hud.Tag(hud.Action("Propose a final-two promise", () => Commit(state, EpisodeCommandKind.PromiseFinalTwo, npc.id)),
                Category(EpisodeCommandKind.PromiseFinalTwo));
            hud.Tag(hud.Action(allied ? "Leave our alliance" : "Propose an alliance",
                    () => Commit(state, allied ? EpisodeCommandKind.LeaveAlliance : EpisodeCommandKind.FormAlliance, npc.id)),
                Category(allied ? EpisodeCommandKind.LeaveAlliance : EpisodeCommandKind.FormAlliance));
        }

        /// <summary>What you have on them: the play about them as a bar, where you stand, the threads they are in, and the latest note.</summary>
        private void AboutCard(EpisodeState state, ContestantState who)
        {
            hud.BeginSideCard(EpisodeHud.AboutCardName, "ABOUT " + who.name.ToUpperInvariant());
            var play = PlayAbout(state, who.id);
            if (play != null) hud.ProgressBar(play.title, play.progress.have, play.progress.need, UiTheme.Accent);
            var kind = RelationshipWeb.KindOf(state, who.id);
            hud.CardLine("Your relationship", 13, UiTheme.Muted);
            hud.CardLine(RelationshipWeb.StandingWord(kind), 15, kind == RelationshipWeb.Kind.Neutral ? UiTheme.Paper : RelationshipWeb.StandingColour(kind), UiTheme.Weight.SemiBold);
            foreach (var thread in ThreadsWith(state, who.id))
            {
                hud.CardLine(thread.label, 14, UiTheme.Paper, UiTheme.Weight.SemiBold);
                hud.CardLine(ThreadLine(thread), 12, UiTheme.Muted);
            }
            string note = HouseguestNotes.Brief(state, who.id);
            if (!string.IsNullOrEmpty(note)) hud.CardLine(note, 12, UiTheme.Muted);
            hud.EndSideCard();
        }
    }
}
