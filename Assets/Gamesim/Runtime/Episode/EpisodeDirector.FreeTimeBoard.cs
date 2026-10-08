using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>
    /// Free time as one board on the strategy stage (ACTIONS-DEALS-ALLIANCES-PLAN F1, the owner's
    /// mockup 87; EpisodeHud.FreeTimeBoard.cs): whoever came to the player, or the first story beat
    /// waiting, or the play in motion, beside the budget; the story in a line; the house as cards; the
    /// other ways to spend the time; and a footer with where the player is, a way to stay in the house
    /// and the way on.
    ///
    /// <para>A story beat waiting is a banner whose "Answer" opens it as the step, as the nomination
    /// opens one from its tracker, with "Back to free time" in the footer while it is open and the
    /// keyboard on it as it opens. Answer and the strip's chips stand over the step's words column,
    /// so neither a second click nor a second Enter on what opened a beat answers it. The other way
    /// round, a step that goes - answered, or left by "Back to free time" - or whose options give
    /// way to its next press leaves whatever is drawn now under the pointer: the panel takes no
    /// pointer press for a moment of real time (EpisodeHud.HoldPointerOffPanel), and the keyboard
    /// lands where a second Enter commits nothing. Which beat is open and which page of cards is
    /// shown are view state, like the houseguest's screen: nothing here is saved, and every control
    /// commits through the command it always did.</para>
    ///
    /// <para>Not over everything free time can be: a diary reflection waiting keeps its card, a
    /// legacy house event keeps its band and its camera, the Final 3's window keeps Endgame
    /// Preparation (decision 17), a houseguest's screen keeps its layout, and a player watching from
    /// outside the house keeps the screen they had.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>Free time's way on, the caption the season's walks press.</summary>
        public const string BeginNextCompetitionCaption = "Begin the next competition";

        /// <summary>The board's own captions, new with it.</summary>
        public const string StayInTheHouseCaption = "Stay in the house", ExploreTheHouseCaption = "Explore the house",
            DoAnActivityCaption = "Do an activity", AnswerBeatCaption = "Answer";

        /// <summary>The mockup's words for the footer's two buttons, as headlines over their captions.</summary>
        public const string EndFreeTimeHeadline = "END FREE TIME", ContinueFreeTimeHeadline = "CONTINUE FREE TIME";

        /// <summary>What costs an action in free time and what does not, as the engine counts it (the plan's truthful copy).</summary>
        public const string FreeTimeCostCopy = "Talking, listening in and house meetings each spend one action. Walking the house, "
            + "house activities and answering anyone who comes to you are free.";

        /// <summary>
        /// The same under the commitment rules (ACTIONS-DEALS-ALLIANCES-PLAN R0, X1), where studying
        /// the house in the diary room is one of the window's actions too.
        /// </summary>
        public const string FreeTimeCostCopyWithStudy = "Talking, listening in, house meetings and studying the house each spend one action. "
            + "Walking the house, house activities and answering anyone who comes to you are free.";

        /// <summary>What costs an action in this season's free time: a study too, under the commitment rules.</summary>
        public static string FreeTimeCostCopyFor(EpisodeState state) =>
            EpisodeEngine.CommitmentRulesOn(state) ? FreeTimeCostCopyWithStudy : FreeTimeCostCopy;

        /// <summary>
        /// The budget card's copy: what costs and what is free, then what beginning the next
        /// competition loses, as the engine counts it. Everything left goes when the week turns; on a
        /// night that does not turn it - move-in night - what was bought carries into the week's
        /// windows; and without the levers bought time is never reset, so it comes back every week.
        /// </summary>
        public static string FreeTimeCostLine(EpisodeState state)
        {
            const string lost = "Unspent actions are lost when you begin the next competition";
            string costs = FreeTimeCostCopyFor(state);
            if (EpisodeEngine.EconomyRulesOn(state) && EpisodeEngine.IsFirstNight(state))
                return costs + " Unspent base actions are lost when you begin the next competition. Only unspent extras carry into the week.";
            if (state != null && !state.evictionResolved) return costs + " " + lost + ", but actions you buy carry into the week.";
            return costs + " " + lost + (EpisodeEngine.LeverRulesOn(state) ? "." : "; actions you buy come back every week.");
        }

        /// <summary>The words at a tile's foot: what it costs, that it is free, or why it is locked.</summary>
        public const string TileCostsAction = "Cost: 1 action", TileFree = "Free", TileNoActionsLeft = "No actions left";

        /// <summary>The beat opened from the board's banner or its strip, while it is open. View state.</summary>
        private string freeTimeOpenedBeat;
        /// <summary>
        /// Whether the next render of an opened beat puts the keyboard on "Back to free time": once,
        /// as it opens, so a second Enter on whatever opened it goes back rather than answering.
        /// </summary>
        private bool freeTimeBeatFocusBack;
        /// <summary>The page of the house's cards shown, and the free time it belongs to. View state.</summary>
        private int freeTimePage, freeTimeViewKey = -1;
        /// <summary>
        /// What the board's panel showed at its last render - the board, a houseguest's screen, or a
        /// beat's step at its first or second press - so a render that replaces one under the pointer
        /// is known (<see cref="Replaces"/>). View state.
        /// </summary>
        private string freeTimeView;
        private const string FreeTimeBoardView = "board", FreeTimeStepView = "step:", FreeTimeHouseguestView = "houseguest:";

        /// <summary>
        /// Whether a render's view replaces the last one under the pointer: a step that goes -
        /// answered, left by "Back to free time", or redrawn by an answer for its next press - or a
        /// houseguest's screen and the board taking each other's place, either way (UI-UX-PASS-PLAN
        /// U0's review): "Back to free time" in the footer's secondary slot stands where the board
        /// draws "Stay in the house", and a card's name where the screen draws its strip and its
        /// moves. Not the board giving way to a step, whose words column stands under what opened it,
        /// nor one houseguest's screen to another's, whose strip stands where it stood.
        /// </summary>
        private static bool Replaces(string before, string now) =>
            before != null && now != null && before != now
            && (before.StartsWith(FreeTimeStepView, StringComparison.Ordinal)
                || before.StartsWith(FreeTimeHouseguestView, StringComparison.Ordinal) != now.StartsWith(FreeTimeHouseguestView, StringComparison.Ordinal));

        /// <summary>Whether free time is on the board now, rather than its old column. A read for tests.</summary>
        public bool IsFreeTimeBoard => hud != null && phaseOpen && FreeTimeBoardBeat(projected)
            && hud.CurrentActivityLayout == EpisodeHud.ActivityLayout.Strategy;

        /// <summary>The story beat open as the board's step, or null. A read for tests.</summary>
        public string FreeTimeBeatOpen => freeTimeOpenedBeat;

        /// <summary>What closing the panel throws away: the board opens again on its root and its first page.</summary>
        private void ForgetFreeTimeView()
        {
            freeTimeOpenedBeat = null;
            freeTimeBeatFocusBack = false;
            freeTimePage = 0;
            freeTimeView = null;
            // A panel opened again opens to a pointer and a keyboard that have moved on: nothing to
            // hold off, and no focus left to land.
            if (hud == null) return;
            hud.EndPointerHold();
            hud.DropFocusAsked();
        }

        /// <summary>
        /// Whether a state's free time is the strategy stage's - the board, or a houseguest's screen
        /// opened from its cards - by the state alone: the social window with no reflection waiting,
        /// no legacy house event, not the Final 3's window, and a player still in the house. Every
        /// other free time draws the old column, whose scroll stands clear of its pinned row. Pure,
        /// for the reachability pin; the director adds that no challenge has the panel.
        /// </summary>
        public static bool FreeTimeOnTheStage(EpisodeState state)
        {
            if (state == null || state.phase != EpisodePhase.Social || state.pendingDiary != null) return false;
            // A legacy situation keeps the house event's band, the camera on its people and the
            // Social pin; legacy events happen only with the story off.
            if (state.houseEvents.Any(e => !e.resolved && !e.IsStory)) return false;
            // At three this window is Endgame Preparation, which moves onto the board later (decision 17).
            if (EpisodeHud.IsFinalThree(state)) return false;
            // A player out of the game has nothing here to spend; they keep the screen they watch from.
            var you = state.Find(state.playerId);
            return you != null && you.status == ContestantStatus.Active;
        }

        /// <summary>Whether free time is on the strategy stage now: its state says so, and no challenge has the panel.</summary>
        private bool FreeTimeStageBeat(EpisodeState state) => !challengeActive && FreeTimeOnTheStage(state);

        /// <summary>The houseguest whose screen is open over free time, when one is: active, and not the player. Null for the root.</summary>
        private ContestantState HouseguestScreenChosen(EpisodeState state)
        {
            var chosen = state != null && moveScreenId != null ? state.Find(moveScreenId) : null;
            return chosen != null && !chosen.isPlayer && chosen.status == ContestantStatus.Active ? chosen : null;
        }

        /// <summary>Whether free time is drawn as the board: on the stage, with no houseguest's screen over it.</summary>
        private bool FreeTimeBoardBeat(EpisodeState state) => FreeTimeStageBeat(state) && HouseguestScreenChosen(state) == null;

        /// <summary>
        /// Whether free time is a houseguest's screen on the stage (UI-UX-PASS-PLAN U0): opened from
        /// the board's cards, laid for the board's frame with the board's footer - the way on, "Back
        /// to free time" in the secondary slot, and the strip - in place of the Stage's column, whose
        /// scroll cut its rows at the pinned bar. At three and for a watcher the column is still
        /// free time's (decision 17), and the screen keeps its old layout there.
        /// </summary>
        private bool HouseguestScreenBeat(EpisodeState state) => FreeTimeStageBeat(state) && HouseguestScreenChosen(state) != null;

        /// <summary>
        /// Draws free time's board when free time is on the strategy stage, and says whether it did:
        /// the beat opened as the step, or the board, and the footer under either; or a houseguest's
        /// screen opened from the board's cards, with the footer it shares.
        /// </summary>
        private bool FreeTimeBoard(EpisodeState state)
        {
            if (hud == null || hud.CurrentActivityLayout != EpisodeHud.ActivityLayout.Strategy || !FreeTimeStageBeat(state))
            {
                freeTimeView = null;
                return false;
            }
            // A new free time opens on the first page; week one has two, move-in night and the one after its eviction.
            int key = state.week * 2 + (EpisodeEngine.IsFirstNight(state) ? 0 : 1);
            if (freeTimeViewKey != key) { ForgetFreeTimeView(); freeTimeViewKey = key; }
            var chosen = HouseguestScreenChosen(state);
            if (chosen != null)
            {
                // A houseguest's screen stands where the board stood: the press on a card's name that
                // opened it may be the first of two, and the second would land on whatever is drawn
                // there now - a strip face, a way to spend the action, or a move that commits. The
                // pointer is held off the panel a moment as it replaces the board, as the board does
                // when it replaces the screen (Replaces).
                string here = FreeTimeHouseguestView + chosen.id;
                bool crossed = Replaces(freeTimeView, here);
                freeTimeView = here;
                hud.StrategyWholeWidth();
                HouseguestScreen(state, chosen);
                HouseguestFooter(state);
                if (crossed) hud.HoldPointerOffPanel(EpisodeHud.PointerHoldSeconds);
                else hud.KeepPointerHold();
                return true;
            }
            var open = EpisodeEngine.OpenStoryBeats(state);
            if (freeTimeOpenedBeat != null && !open.Any(beat => beat.id == freeTimeOpenedBeat)) freeTimeOpenedBeat = null;
            if (storyStepEvent != null && !open.Any(beat => beat.id == storyStepEvent)) ClearStoryStep();
            var opened = freeTimeOpenedBeat != null ? open.FirstOrDefault(beat => beat.id == freeTimeOpenedBeat) : null;

            // What the panel shows now - the board, or a beat's step at its first press or its second -
            // and whether it replaces a step (answered, left by "Back to free time", or redrawn by an
            // answer for its next press) or a houseguest's screen (left by "Back to free time").
            // Whatever is drawn now stands under the pointer that pressed, and the second press of a
            // double click would land on it: a way to buy time, a move, the way on, "Stay in the
            // house", or the person a pick commits to. Pointer presses are held off the panel a moment.
            bool second = opened != null && storyStepEvent == opened.id && opened.choices.Any(choice => choice.optionId == storyStepOption);
            string view = opened == null ? FreeTimeBoardView
                : FreeTimeStepView + opened.id + (second ? "/" + storyStepOption + "/" + storyStepPick : "");
            bool replaced = Replaces(freeTimeView, view);
            freeTimeView = view;
            // A focus the last render asked for, which a render in between would otherwise drop.
            hud.KeepFocusAsked();

            if (opened != null)
            {
                // The beat as the step, as the nomination draws one: its words beside its options,
                // and the second press - who, or the confirm - in its place. Laid at the board's whole
                // width, so its words column is under what opened it (EpisodeHud.BoardStepWords) and
                // the footer does not move.
                hud.StrategyWholeWidth();
                NominationStory(state, opened);
                FreeTimeFooter(state, true);
                // The keyboard goes where a second Enter commits nothing: opened from the board, to the
                // way back; at its second press, to the step's own Back rather than the first person or
                // the confirm; and back at its options, to the way back again.
                if (freeTimeBeatFocusBack) hud.FocusWhenWired(BackToFreeTimeCaption);
                else if (replaced) hud.FocusWhenWired(second ? EpisodeHud.StoryBackCaption : BackToFreeTimeCaption);
                freeTimeBeatFocusBack = false;
            }
            else
            {
                freeTimeBeatFocusBack = false;
                hud.StrategyWholeWidth();
                var spec = FreeTimeSpec(state, open);
                spec.FocusSafely = replaced;
                hud.FreeTimeBoard(spec);
                FreeTimeFooter(state, false);
            }
            if (replaced) hud.HoldPointerOffPanel(EpisodeHud.PointerHoldSeconds);
            else hud.KeepPointerHold();
            return true;
        }

        /// <summary>A waiting beat pressed on the board: it becomes the step, with the keyboard on the way back. Nothing is committed.</summary>
        private void OpenFreeTimeBeat(string eventId)
        {
            freeTimeOpenedBeat = eventId;
            freeTimeBeatFocusBack = true;
            ClearStoryStep();
            Render();
        }

        /// <summary>Back from a beat to the board, its answer still waiting.</summary>
        private void CloseFreeTimeBeat()
        {
            freeTimeOpenedBeat = null;
            freeTimeBeatFocusBack = false;
            ClearStoryStep();
            Render();
        }

        // ------------------------------------------------------------ what the board says

        private EpisodeHud.FreeTimeBoardSpec FreeTimeSpec(EpisodeState state, List<HouseEventState> open)
        {
            var spec = new EpisodeHud.FreeTimeBoardSpec();
            // Somebody who came to the player first, as they always were: then a story waiting on
            // them; then what they are already playing for.
            var card = ReplyCards.Pending(state);
            HouseEventState heroBeat = null;
            if (card != null)
            {
                string cardId = card.id;
                var next = state.replyCards.Skip(1).FirstOrDefault();
                spec.Hero = EpisodeHud.FreeTimeHero.Reply;
                spec.ReplyTitle = ReplyCards.Title(state, card);
                spec.ReplyMessage = ReplyCards.Message(state, card);
                spec.RepliesWaiting = state.replyCards.Count;
                spec.ReplyNext = next != null ? state.Find(next.fromId)?.name : null;
                object replyView = BeginReplyChoices();
                spec.Replies = ReplyCards.Replies(card.kind).Select(reply =>
                {
                    string key = reply.Key;
                    return new EpisodeHud.CampaignReply
                    {
                        Caption = EpisodeHud.ReplyCaption(reply.Label), Key = key, Description = ReplyCardPayoffs.Description(state, card, reply),
                        Risk = EpisodeHud.RiskTag(reply.Risk),
                        Choose = ReplyChoice(state, cardId, key, replyView),
                    };
                }).ToList();
            }
            else if (open.Count > 0)
            {
                heroBeat = open[0];
                string eventId = heroBeat.id;
                // A beat with no answer that spends an action is free to answer, and says so: move-in
                // night's one real conversation is one.
                bool free = !heroBeat.choices.Any(choice => choice.costsAction);
                spec.Hero = EpisodeHud.FreeTimeHero.Beat;
                spec.BeatEyebrow = (StoryText.Eyebrow(heroBeat) ?? "Story").ToUpperInvariant() + " · WAITING ON YOU";
                spec.BeatTitle = StoryText.Title(heroBeat);
                spec.BeatNarrative = StoryText.Narrative(state, heroBeat);
                spec.BeatFree = free;
                spec.BeatNote = (free ? "Answering spends no action." : "Some answers spend an action.")
                    + "  ·  It won't wait past " + StoryText.ClosesAt(heroBeat.closesAnchor) + ".";
                spec.Answer = () => OpenFreeTimeBeat(eventId);
            }
            else spec.Hero = EpisodeHud.FreeTimeHero.Play;

            // The play in motion: the hero when nothing is waiting, else a segment of the strip.
            var plays = EpisodeEngine.Plays(state).Where(play => play.ending == null).ToList();
            var running = plays.FirstOrDefault(play => play.takenOn) ?? plays.FirstOrDefault();
            if (spec.Hero == EpisodeHud.FreeTimeHero.Play)
            {
                var subject = running != null ? PlaySubject(state, running) : null;
                spec.PlayEyebrow = "CURRENT PLAY" + (running != null ? " · " + PlayCurrencies.Label(running.currency).ToUpperInvariant() : "");
                spec.PlayTitle = running == null ? "No play in motion." : running.title + (subject != null ? " — " + subject.name : "");
                spec.PlayBar = running != null && running.takenOn;
                if (spec.PlayBar)
                {
                    spec.PlayBarLabel = PlayCurrencies.Label(running.currency);
                    spec.PlayHave = running.progress.have;
                    spec.PlayNeed = running.progress.need;
                }
                else if (running != null) spec.PlayStatus = "On offer until " + StoryText.ClosesAt(running.deadline) + ".";
                spec.PlayGoal = running != null ? running.goal : "Plays are offered as the season turns; a Pull brings one to you.";
            }
            else spec.StripPlay = running != null ? PlayShort(running) : "None in motion";

            // The budget: the window's own count and rule, what costs and what is free, what moving on
            // loses, and the two ways to buy another action while the house still sells them.
            int left = ActionsLeftCount(state);
            spec.ActionsLeft = left;
            spec.Rule = BudgetRule(state);
            spec.Copy = FreeTimeCostLine(state);
            spec.Unused = UnusedActionsNote(state);
            spec.Buys = new List<EpisodeHud.BuyButton>();
            if (WebSocialVocabulary.PurchaseCeiling - state.boughtActionPoints > 0)
            {
                spec.Buys.Add(new EpisodeHud.BuyButton
                {
                    Caption = EpisodeHud.BuyBurnOneCaption, Price = Mathf.Abs((int)WebSocialVocabulary.BurnOneCost) + " goodwill · choose a housemate",
                    Choose = () => OpenActionPurchase(state),
                });
                spec.Buys.Add(new EpisodeHud.BuyButton
                {
                    Caption = EpisodeHud.BuySpreadCaption, Price = Mathf.Abs((int)WebSocialVocabulary.SpreadAllCost) + " goodwill with every housemate",
                    Choose = () => Commit(state, EpisodeCommandKind.BuyActionPoint, text: WebSocialVocabulary.SpreadAll),
                });
            }
            else spec.BoughtOut = "You have bought as much time as the house will give you this " + (EpisodeEngine.LeverRulesOn(state) ? "week" : "season") + ".";

            // The story in a line: the threads and the storylines playing out, the other beats waiting
            // as chips, the preparation banked, and what being a Have-Not costs, word for word.
            spec.Threads = StoryStripLine(state);
            spec.Waiting = open.Where(beat => beat != heroBeat).Select(beat =>
            {
                string eventId = beat.id;
                return (StoryText.Title(beat), (Action)(() => OpenFreeTimeBeat(eventId)));
            }).ToList();
            spec.Preparation = state.playerStudyBonus > 0 ? "Preparation " + state.playerStudyBonus + "/5" : null;
            spec.HaveNot = HaveNots.Is(state, state.playerId) ? HaveNotLine : null;

            // The house as cards, those in the room with the player first, with the week's role on each photo.
            var here = HouseOccupancy(state).FirstOrDefault(room => room.Occupants != null && room.Occupants.Any(person => person.IsPlayer));
            var hereIds = new HashSet<string>((here.Occupants ?? new List<HouseMap.Occupant>()).Where(person => !person.IsPlayer).Select(person => person.Id));
            spec.Cards = OthersHereFirst(state).Select(c =>
            {
                string role = EpisodeHud.RoleFor(state, c.id, out var colour);
                if (role == null && hereIds.Contains(c.id)) { role = "HERE"; colour = UiTheme.Allied; }
                return new EpisodeHud.HouseCard(c.id, role, colour, null);
            }).ToList();
            spec.Page = freeTimePage;
            spec.ChoosePage = page => { freeTimePage = page; Render(); };
            spec.Talk = TalkFromCampaign;
            spec.Pick = OpenHouseguestScreen;
            spec.Moves = FreeTimeMoves(state, left);
            return spec;
        }

        /// <summary>
        /// The threads, the storylines playing out and what the stories left the player, in the
        /// strip's few words; the old card's line when nothing is in play.
        /// </summary>
        private static string StoryStripLine(EpisodeState state)
        {
            var parts = EpisodeEngine.Threads(state).Where(thread => thread.ending == null).Select(thread => thread.label).ToList();
            int playing = state.storylines.Count(x => x.beatId != null && StorylineStatus.Running(x.status)
                && StoryCatalog.Find(x.templateId)?.play == null && x.lane != StoryLanes.Thread
                && !state.houseEvents.Any(e => !e.resolved && e.cycleId == x.id));
            if (playing > 0) parts.Add(playing + (playing == 1 ? " storyline playing out" : " storylines playing out"));
            foreach (var modifier in state.activeModifiers.Where(m => m.weeksLeft > 0 && string.IsNullOrEmpty(m.ownerId)))
                parts.Add(modifier.name + " (" + modifier.weeksLeft + (modifier.weeksLeft == 1 ? " week left)" : " weeks left)"));
            return parts.Count > 0 ? string.Join("  ·  ", parts) : "Nothing in play yet. Threads start from what you do.";
        }

        /// <summary>
        /// The other ways to spend the time (mockup 87): walking the house and the house activities,
        /// which are free, listening in and the two house meetings, which cost an action each and are
        /// locked with the reason when none is left. The campaign's own whole-house rows are
        /// <see cref="HouseMoves"/>, which this leaves as they are.
        /// </summary>
        private List<EpisodeHud.BoardTile> FreeTimeMoves(EpisodeState state, int left)
        {
            bool spent = left <= 0;
            EpisodeHud.BoardTile CostedTile(string caption, string glyph, string corner, Color tint, Action choose) => new EpisodeHud.BoardTile
            {
                Caption = caption, Glyph = glyph, Corner = corner, CornerTint = tint, Locked = spent,
                Cost = spent ? TileNoActionsLeft : TileCostsAction, Choose = choose,
            };
            EpisodeHud.BoardTile FreeTile(string caption, string glyph, Action choose) => new EpisodeHud.BoardTile
            {
                Caption = caption, Glyph = glyph, Cost = TileFree, Free = true, Choose = choose,
            };
            var tiles = new List<EpisodeHud.BoardTile>
            {
                // The overview's map, the panel closed to walk: a pair found in a room offers a walk-in.
                FreeTile(ExploreTheHouseCaption, "house", () => ShowOverview()),
            };
            // Listening in needs two to overhear.
            if (state.Active.Count(c => !c.isPlayer) >= 2)
                tiles.Add(CostedTile(OverviewListenCaption, "eye", "Works 7 in 10", UiTheme.Joke, () => Commit(state, EpisodeCommandKind.Eavesdrop)));
            tiles.Add(CostedTile(EpisodeHud.RallyHouseCaption, "people", "Risky", UiTheme.Joke,
                () => Commit(state, EpisodeCommandKind.HouseMeeting, text: EpisodeEngine.RallyTroops)));
            tiles.Add(CostedTile(EpisodeHud.AirLaundryCaption, "target", AiringRiskLabel(state), UiTheme.Danger,
                () => Commit(state, EpisodeCommandKind.HouseMeeting, text: EpisodeEngine.AirDirtyLaundry)));
            tiles.Add(FreeTile(DoAnActivityCaption, "dumbbell", OpenHouseActivities));
            return tiles;
        }

        // ------------------------------------------------------------ the footer

        /// <summary>
        /// The footer: the way on pinned under its headline, the secondary slot - a way to stay in the
        /// house on the board, the way back from an opened beat - and a strip with what moving on lets
        /// pass, else where the player is, else a line of the phase's own rule copy.
        /// </summary>
        private void FreeTimeFooter(EpisodeState state, bool beatOpen)
        {
            var wayOn = hud.PinnedAction(BeginNextCompetitionCaption, () => Commit(state, EpisodeCommandKind.Advance));
            hud.DressWayOn(wayOn, EndFreeTimeHeadline, PackArt.Pack8ContinueButton);
            if (beatOpen) hud.PinnedSecondary(BackToFreeTimeCaption, CloseFreeTimeBeat);
            else hud.DressSecondary(hud.PinnedSecondary(StayInTheHouseCaption, ClosePanels), ContinueFreeTimeHeadline);
            // A storyline moving on lets pass outranks the rest of the strip.
            AdvanceWarning(state);
            string where = FreeTimeLocationLine(state);
            if (where != null) hud.PinnedNote(where, EpisodeHud.LocationCardName, false);
            else hud.PinnedNote(PhaseRule(state), EpisodeHud.FreeTimeTipName, false);
        }

        /// <summary>
        /// The footer under a houseguest's screen on the stage (UI-UX-PASS-PLAN U0): the way on under
        /// its headline, "Back to free time" in the secondary slot, and the strip with where the
        /// player is over what moving on loses - the unused actions, which the Stage's column pinned
        /// as a note under the way on - unless a storyline moving on lets pass outranks them.
        /// </summary>
        private void HouseguestFooter(EpisodeState state)
        {
            var wayOn = hud.PinnedAction(BeginNextCompetitionCaption, () => Commit(state, EpisodeCommandKind.Advance));
            hud.DressWayOn(wayOn, EndFreeTimeHeadline, PackArt.Pack8ContinueButton);
            hud.PinnedSecondary(BackToFreeTimeCaption, CloseHouseguestScreen);
            AdvanceWarning(state);
            string where = FreeTimeLocationLine(state);
            if (where != null) hud.PinnedNote(where, EpisodeHud.LocationCardName, false);
            else hud.PinnedNote(PhaseRule(state), EpisodeHud.FreeTimeTipName, false);
            string unused = UnusedActionsNote(state);
            if (unused != null) hud.PinnedNote(unused, EpisodeHud.UnusedActionsNoteName, EpisodeHud.FooterRank.Notice, true);
        }

        /// <summary>"Nomination room · you have it to yourself": the player's room and who else is in it, by first name.</summary>
        private string FreeTimeLocationLine(EpisodeState state)
        {
            var here = HouseOccupancy(state).FirstOrDefault(room => room.Occupants != null && room.Occupants.Any(person => person.IsPlayer));
            if (string.IsNullOrEmpty(here.Name)) return null;
            var others = (here.Occupants ?? new List<HouseMap.Occupant>()).Where(person => !person.IsPlayer)
                .Select(person => (person.Name ?? "").Split(' ')[0]).ToArray();
            string company = others.Length == 0 ? "you have it to yourself"
                : others.Length + (others.Length == 1 ? " houseguest here: " : " houseguests here: ") + string.Join(", ", others);
            return RoomLabels.Name(here.Name) + "  ·  " + company;
        }
    }
}
