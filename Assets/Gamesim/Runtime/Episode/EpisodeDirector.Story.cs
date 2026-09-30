using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;
using UnityEngine.AI;

namespace Gamesim.Episode
{
    /// <summary>
    /// Story beats on the episode screen and in the Diary Room.
    ///
    /// <para>A beat is drawn from its content id at display time - names, pronouns and the
    /// description are rendered by <see cref="StoryText"/> from the save's cast - and answered by
    /// its option id through <see cref="EpisodeCommandKind.ProgressStoryline"/>, never by its label.
    /// Two kinds of option take a second press: one that names somebody asks who first, and a rule
    /// break asks to be confirmed, because production's ladder is signposted and never an accident.
    /// </para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>A choice waiting on its second press: the beat, the option, and for a pick the person named.</summary>
        private string storyStepEvent, storyStepOption, storyStepPick;

        /// <summary>Every story beat waiting on the player, on the episode screen. Returns whether any were drawn.</summary>
        private bool PendingStoryBeats(EpisodeState state)
        {
            var beats = EpisodeEngine.OpenStoryBeats(state);
            if (storyStepEvent != null && !beats.Any(b => b.id == storyStepEvent)) ClearStoryStep();
            if (beats.Count == 0 || state.Find(state.playerId)?.status != ContestantStatus.Active) return false;
            foreach (var item in beats) RenderStoryBeat(state, item);
            return true;
        }

        /// <summary>
        /// A beat a conversation raised with this houseguest, drawn in the conversation's panel.
        /// Returns whether there was one. The episode screen still shows it, for a player who
        /// walked away from the conversation before answering.
        /// </summary>
        private bool ConversationBeat(EpisodeState state, string npcId)
        {
            if (state.Find(state.playerId)?.status != ContestantStatus.Active) return false;
            var item = EpisodeEngine.OpenStoryBeats(state).FirstOrDefault(e => e.surface == StorySurfaces.Conversation
                && (e.involvedIds.Contains(npcId) || e.cast.Any(role => role.contestantId == npcId)));
            if (item == null) return false;
            RenderStoryBeat(state, item);
            return true;
        }

        /// <summary>A Diary Room summons, in the room's pending tab.</summary>
        private bool PendingSummons(EpisodeState state)
        {
            var summons = EpisodeEngine.OpenSummons(state);
            if (summons == null || state.Find(state.playerId)?.status != ContestantStatus.Active) return false;
            RenderStoryBeat(state, summons);
            return true;
        }

        private void RenderStoryBeat(EpisodeState state, HouseEventState item)
        {
            hud.StoryBeatHeader(StoryText.Eyebrow(item), StoryText.Title(item), StoryText.Narrative(state, item));
            if (storyStepEvent == item.id)
            {
                var chosen = item.choices.FirstOrDefault(c => c.optionId == storyStepOption);
                if (chosen != null) { RenderStoryStep(state, item, chosen); return; }
                ClearStoryStep();
            }
            hud.StoryChoices(item.choices.Select(choice => StoryChoiceFor(state, item, choice)).ToList());
            FrameHouseEvent(item);
        }

        private EpisodeHud.StoryChoice StoryChoiceFor(EpisodeState state, HouseEventState item, HouseEventChoice choice)
        {
            bool costsTooMuch = choice.costsAction && !CanSpendAction(state);
            string note = choice.locked ? choice.lockReason
                : costsTooMuch ? (EpisodeEngine.WeekRulesOn(state) ? "No conversations left in this window" : "No actions left this week")
                : choice.conduct ? RuleBreakTag(state)
                : choice.costsAction ? "Costs an action"
                : choice.pickPerson ? "You choose who"
                : null;
            string eventId = item.id, optionId = choice.optionId;
            return new EpisodeHud.StoryChoice
            {
                Caption = EpisodeHud.EventChoiceCaption(choice.label),
                Description = StoryText.Description(state, item, choice),
                Risk = EpisodeHud.RiskTag(choice.risk),
                Odds = choice.checkBase >= 0 ? StoryOdds.InTen(StoryOdds.Chance(state, item, choice)) : null,
                Note = note,
                Locked = choice.locked || costsTooMuch,
                Choose = () => ChooseStoryOption(state, eventId, optionId),
            };
        }

        private static bool CanSpendAction(EpisodeState state) =>
            (EpisodeEngine.WeekRulesOn(state) ? EpisodeEngine.Window(state) != Windows.None : state.phase == EpisodePhase.Social || state.phase == EpisodePhase.Campaign)
            && EpisodeEngine.SocialActionsSpent(state) < EpisodeEngine.SocialActionBudget(state);

        /// <summary>
        /// Where a rule break would leave the player on production's ladder, said before it is
        /// pressed: the strike it would be, or that it would be the last.
        /// </summary>
        private static string RuleBreakTag(EpisodeState state)
        {
            int next = Production.Strikes(state, state.playerId) + 1;
            if (next < 3) return "Rule break · Strike " + next + " of 3";
            bool removal = state.story.productionStrictness == StoryRules.Standard && Production.RemovalWindow(state);
            return removal ? "Rule break · Production will remove you" : "Rule break · Another penalty";
        }

        private void ChooseStoryOption(EpisodeState state, string eventId, string optionId)
        {
            var item = state.houseEvents.FirstOrDefault(e => e.id == eventId);
            var choice = item?.choices.FirstOrDefault(c => c.optionId == optionId);
            if (choice == null) return;
            if (choice.pickPerson || choice.conduct)
            {
                storyStepEvent = eventId; storyStepOption = optionId; storyStepPick = null;
                Render();
                return;
            }
            Commit(state, EpisodeCommandKind.ProgressStoryline, eventId, optionId);
        }

        /// <summary>The second press: who, for an option that names somebody; then the confirm, for a rule break.</summary>
        private void RenderStoryStep(EpisodeState state, HouseEventState item, HouseEventChoice choice)
        {
            string eventId = item.id, optionId = choice.optionId;
            if (choice.pickPerson && storyStepPick == null)
            {
                hud.Heading(choice.label.ToUpperInvariant() + ": WHO?");
                var people = choice.eligibleIds.Select(state.Find).Where(c => c != null && c.status == ContestantStatus.Active).ToList();
                if (people.Count == 0) hud.Paragraph("There is nobody left to name.");
                foreach (var person in people)
                {
                    string id = person.id;
                    hud.ActionFor(id, person.name, () =>
                    {
                        if (choice.conduct) { storyStepPick = id; Render(); }
                        else { ClearStoryStep(); Commit(state, EpisodeCommandKind.ProgressStoryline, eventId, optionId, text: id); }
                    });
                }
                hud.Action(EpisodeHud.StoryBackCaption, () => { ClearStoryStep(); Render(); });
                return;
            }
            // A rule break: said plainly, then pressed twice.
            hud.Heading(RuleBreakTag(state).ToUpperInvariant());
            hud.Paragraph("Production is watching. " + StoryText.Description(state, item, choice));
            hud.Action(EpisodeHud.StoryConfirmCaption, () =>
            {
                string picked = storyStepPick;
                ClearStoryStep();
                Commit(state, EpisodeCommandKind.ProgressStoryline, eventId, optionId, text: picked);
            });
            hud.Action(EpisodeHud.StoryBackCaption, () => { ClearStoryStep(); Render(); });
        }

        private void ClearStoryStep()
        {
            storyStepEvent = storyStepOption = storyStepPick = null;
        }

        /// <summary>
        /// The stories running this week, and what each story left behind: one line each, in the
        /// free-time panel beside the budget they draw on. A modifier says how long it has left,
        /// because a bonus with no end date is a number the player cannot plan around.
        /// </summary>
        private void StorylinesBlock(EpisodeState state)
        {
            PlaysBlock(state, finished: false);
            ThreadsBlock(state, finished: false);
            var running = state.storylines.Where(x => x.beatId != null && StorylineStatus.Running(x.status)
                                                      && StoryCatalog.Find(x.templateId)?.play == null && x.lane != StoryLanes.Thread)
                .OrderBy(x => x.week).ThenBy(x => x.id, StringComparer.Ordinal).ToList();
            var mine = state.activeModifiers.Where(m => m.weeksLeft > 0 && string.IsNullOrEmpty(m.ownerId)).ToList();
            if (running.Count == 0 && mine.Count == 0) return;
            hud.Heading("STORYLINES");
            foreach (var cycle in running)
            {
                var arc = StoryCatalog.Find(cycle.templateId);
                bool waiting = state.houseEvents.Any(e => !e.resolved && e.cycleId == cycle.id);
                hud.StoryLine(arc?.title ?? cycle.title, waiting ? "waiting on you" : "still playing out");
            }
            foreach (var modifier in mine)
            {
                string effect = string.Join(", ", new[]
                {
                    Math.Abs(modifier.competitionBonus) > 0.001 ? modifier.competitionBonus.ToString("+0;-0") + " in competitions" : null,
                    StoryModifiers.ActionsFrom(modifier.socialBonus) != 0 ? StoryModifiers.ActionsFrom(modifier.socialBonus).ToString("+0;-0") + " action" : null,
                }.Where(x => x != null));
                hud.StoryLine(modifier.name, (effect.Length == 0 ? "" : effect + ", ")
                    + modifier.weeksLeft + (modifier.weeksLeft == 1 ? " week left" : " weeks left"));
            }
        }

        /// <summary>The heading the plays sit under, in the free-time panel and on the plays page.</summary>
        public const string PlaysHeading = "PLAYS";

        /// <summary>
        /// The plays, one line each (plan 30 §4): the ones on offer and in play, and on the plays page
        /// how the finished ones went too.
        /// </summary>
        private void PlaysBlock(EpisodeState state, bool finished)
        {
            var plays = EpisodeEngine.Plays(state).Where(p => finished || p.ending == null).ToList();
            if (plays.Count == 0) return;
            hud.Heading(PlaysHeading);
            foreach (var play in plays) hud.StoryLine(play.title, PlayLine(play));
        }

        /// <summary>The heading the season's threads sit under, in the free-time panel and on the plays page (plan 31).</summary>
        public const string ThreadsHeading = "THREADS";

        /// <summary>
        /// The season's threads, one line each (plan 31 §3): the ones still going and, on the plays page,
        /// how the finished ones ended too.
        /// </summary>
        private void ThreadsBlock(EpisodeState state, bool finished)
        {
            var threads = EpisodeEngine.Threads(state).Where(t => finished || t.ending == null).ToList();
            if (threads.Count == 0) return;
            hud.Heading(ThreadsHeading);
            foreach (var thread in threads) hud.StoryLine(thread.label, ThreadLine(thread));
        }

        /// <summary>
        /// A thread in a line: how it ended; or the chapter it is on and how that went; or, before its
        /// first chapter, what it is about.
        /// </summary>
        public static string ThreadLine(EpisodeEngine.ThreadView thread)
        {
            if (thread.ending != null) return thread.outcome ?? thread.ending;
            if (thread.chapters.Count == 0) return thread.premise;
            var last = thread.chapters[thread.chapters.Count - 1];
            string how = last.result == ThreadChapterResults.Open ? "under way"
                : last.result == ThreadChapterResults.Landed ? "went your way"
                : last.result == ThreadChapterResults.Missed ? "did not go your way" : "never came to anything";
            return "Chapter " + thread.chapters.Count + ", " + last.title + " · " + how;
        }

        /// <summary>A play in a line: what it pays, then the goal and how far along it is, or how it went.</summary>
        public static string PlayLine(EpisodeEngine.PlayView play)
        {
            string currency = PlayCurrencies.Label(play.currency);
            if (play.ending != null)
                return currency + " · " + (play.ending == PlayEndings.Won ? "won" : play.ending == PlayEndings.Part ? "part of the way" : "lost")
                       + " · " + play.outcome;
            string until = "until " + StoryText.ClosesAt(play.deadline);
            if (!play.takenOn) return currency + " · on offer, " + until + " · " + play.goal;
            string progress = play.progress.need > 1 ? play.progress.have + " of " + play.progress.need
                : play.progress.Met ? "done" : "not yet";
            return currency + " · " + play.goal + " · " + progress + ", " + until;
        }

        /// <summary>
        /// The line above the way on, never in its caption (plan §5.2): which storylines moving on
        /// lets pass, and what that means for each - its lapse, in the option's own words.
        /// </summary>
        private void AdvanceWarning(EpisodeState state)
        {
            if (state.Find(state.playerId)?.status != ContestantStatus.Active) return;
            var passing = EpisodeEngine.LapsingOnAdvance(state);
            if (passing.Count == 0) return;
            string Each(HouseEventState item)
            {
                var lapse = item.choices.FirstOrDefault(c => c.optionId == item.lapseOptionId);
                return StoryText.Title(item) + (lapse != null ? " (" + lapse.label + ")" : "");
            }
            // In the footer's strip on the strategy stage, beside the way on it is about; a paragraph
            // above it everywhere else.
            hud.PinnedNote("Moving on lets " + (passing.Count == 1 ? "1 storyline" : passing.Count + " storylines")
                + " pass: " + string.Join("; ", passing.Select(Each)) + ".", AdvanceWarningName, true);
        }

        /// <summary>The warning's name, so a test can find it without reading its words.</summary>
        public const string AdvanceWarningName = "Storylines passing";

        // ---------------------------------------------------------------- the Pull (plan §5.1)
        //
        // Scene, Pull, Moment, Fallout. The house acts a moment out; a non-modal card offers to step
        // in, naming nobody; stepping in opens the beat's card where the player stands; and what
        // comes of it is the status line, the log and the ceremony cards. The episode screen still
        // shows every open beat, so a Pull turned down is never a story lost.

        /// <summary>The caption that takes the player back out of a scene card.</summary>
        public const string SceneCardDoneCaption = "Back to the house";

        /// <summary>A walk-in the player has found: the pair, the room and the week. Nothing is written until they step in.</summary>
        private string walkInFirst, walkInSecond, walkInRoom;
        private int walkInWeek;

        /// <summary>What the player has turned down: a beat by its id, a walk-in by its week, room and pair. Presentation only, never saved.</summary>
        private readonly HashSet<string> declinedPulls = new HashSet<string>();

        /// <summary>The scene card: a story beat opened where the player stands, followed through its cycle.</summary>
        private bool sceneCardOpen;
        private string sceneCardCycle;

        /// <summary>A Step in on its way: the beat waiting for the player in the room they are walking to.</summary>
        private string sceneBeatPending;

        /// <summary>Whether a story beat's card is open out in the house, for the tests.</summary>
        public bool IsSceneCardOpen => sceneCardOpen;

        /// <summary>The Pull on screen right now, for the tests: its primary caption, or null.</summary>
        public string PullOffered { get; private set; }

        /// <summary>
        /// The house's room for a story venue, or null for a venue it builds no room for (the
        /// storage room, the hallway, the bathroom): a beat there is staged nowhere and waits at
        /// the episode screen like any other.
        /// </summary>
        public static string VenueRoom(string venue)
        {
            switch (venue)
            {
                case StoryVenues.Kitchen: return "Kitchen";
                case StoryVenues.Living: return "Living";
                case StoryVenues.Yard: return "Yard";
                case StoryVenues.Bedroom: return "Bedroom";
                case StoryVenues.HohRoom: return "HoH";
                case StoryVenues.DiaryRoom: return "Private";
                case StoryVenues.Table: return "Nomination";
                default: return null;
            }
        }

        private static string WalkInKey(int week, string room, string first, string second) =>
            "walk-in|" + week + "|" + room + "|" + first + "|" + second;

        private void ClearWalkIn() { walkInFirst = walkInSecond = walkInRoom = null; walkInWeek = 0; }

        /// <summary>
        /// The Pull the house is offering right now, or null. A walk-in the player is standing in
        /// comes first; then the first open beat that is staged in the house - a scene or a meeting
        /// with somebody in it, or a houseguest coming to find the player. A beat with nobody in it
        /// but the player, a conversation's beat and a summons have their own places.
        /// </summary>
        private EpisodeHud.StoryPull? CurrentPull(EpisodeState state)
        {
            if (state == null || !EpisodeEngine.StoryOn(state) || IsPanelOpen || challengeActive || CeremonyOverlays.OnScreen) return null;
            if (!playerIsActive || EpisodeEngine.IsCompetition(state.phase)) return null;
            if (walkInFirst != null && walkInWeek == state.week
                && (state.phase == EpisodePhase.Social || state.phase == EpisodePhase.Campaign))
            {
                string key = WalkInKey(walkInWeek, walkInRoom, walkInFirst, walkInSecond);
                if (!declinedPulls.Contains(key))
                    return new EpisodeHud.StoryPull
                    {
                        Key = key, Primary = EpisodeHud.StepInCaption, Secondary = EpisodeHud.StayOutCaption,
                        Eyebrow = RoomLabels.Title(walkInRoom) + " \u00b7 SOMETHING'S GOING ON",
                        Title = "Something's going on in here",
                        Stakes = "Two houseguests, and it isn't small talk. Step in and it's yours to handle.",
                        Accept = StepIntoWalkIn, Decline = () => { declinedPulls.Add(key); ClearWalkIn(); },
                    };
            }
            foreach (var item in EpisodeEngine.OpenStoryBeats(state))
            {
                if (declinedPulls.Contains(item.id)) continue;
                var pull = PullFor(state, item);
                if (pull != null) return pull;
            }
            return null;
        }

        private EpisodeHud.StoryPull? PullFor(EpisodeState state, HouseEventState item)
        {
            string eventId = item.id, room = VenueRoom(item.venue);
            string stakes = "It won't wait past " + StoryText.ClosesAt(item.closesAnchor) + ".";
            var play = PlayPull(state, item);
            if (play != null) return play;
            switch (item.surface)
            {
                case StorySurfaces.Approach:
                    return new EpisodeHud.StoryPull
                    {
                        Key = eventId, Primary = EpisodeHud.HearThemOutCaption, Secondary = EpisodeHud.NotNowCaption,
                        Eyebrow = "SOMEONE'S LOOKING FOR YOU",
                        Title = "A houseguest wants a word",
                        Stakes = stakes,
                        Accept = () => OpenSceneCard(eventId), Decline = () => declinedPulls.Add(eventId),
                    };
                case StorySurfaces.Scene:
                case StorySurfaces.Meeting:
                    if (!item.cast.Any(role => !string.IsNullOrEmpty(role.contestantId) && role.contestantId != state.playerId)) return null;
                    bool meeting = item.surface == StorySurfaces.Meeting;
                    string where = room != null ? RoomLabels.InSentence(room) : null;
                    return new EpisodeHud.StoryPull
                    {
                        Key = eventId,
                        Primary = meeting ? EpisodeHud.JoinMeetingCaption : EpisodeHud.StepInCaption,
                        Secondary = meeting ? EpisodeHud.NotNowCaption : EpisodeHud.StayOutCaption,
                        Eyebrow = (room != null ? RoomLabels.Title(room) : "THE HOUSE")
                            + (meeting ? " \u00b7 THE HOUSE IS GATHERING" : " \u00b7 SOMETHING'S GOING ON"),
                        Title = meeting ? (where != null ? "The house is gathering in the " + where : "The house is gathering")
                            : (where != null ? "Something's going on in the " + where : "Something's going on"),
                        Stakes = stakes,
                        Accept = () => StepInto(eventId, room), Decline = () => declinedPulls.Add(eventId),
                    };
                default:
                    return null;
            }
        }

        /// <summary>
        /// A play's Pull (plan 30 §4): its offer, with what it pays and until when, taken on in one
        /// press; or a later step of a play already taken on, which opens where the player stands.
        /// Null for a beat that is not a play's.
        /// </summary>
        private EpisodeHud.StoryPull? PlayPull(EpisodeState state, HouseEventState item)
        {
            var cycle = state.storylines.FirstOrDefault(x => x.id == item.cycleId);
            var arc = StoryCatalog.Find(cycle?.templateId);
            var play = arc?.play;
            if (play == null) return null;
            string eventId = item.id;
            string until = StoryText.ClosesAt(play.deadline);
            string goal = StoryText.Fill(state, play.goal, cycle.cast);
            bool offer = !EpisodeEngine.TakenOn(cycle);
            return new EpisodeHud.StoryPull
            {
                Key = eventId,
                Primary = offer ? EpisodeHud.TakeItOnCaption : EpisodeHud.StepInCaption,
                Secondary = EpisodeHud.NotNowCaption,
                Eyebrow = PlayCurrencies.Label(play.currency).ToUpperInvariant() + (offer ? " · A PLAY" : " · YOUR PLAY")
                    + " · UNTIL " + until.ToUpperInvariant(),
                Title = arc.title,
                Stakes = goal,
                Accept = offer ? () => TakePlay(eventId) : () => OpenSceneCard(eventId),
                Decline = () => declinedPulls.Add(eventId),
            };
        }

        /// <summary>
        /// Takes a play on from its Pull, then opens its first step where the player stands: the
        /// offer's own option through <see cref="EpisodeCommandKind.ProgressStoryline"/>, never a label.
        /// </summary>
        private void TakePlay(string eventId)
        {
            var state = projected;
            var offer = state?.houseEvents.FirstOrDefault(e => e.id == eventId && !e.resolved);
            if (offer == null) return;
            string cycleId = offer.cycleId;
            ChooseStoryOption(state, eventId, PlayOptions.TakeItOn);
            var next = projected?.houseEvents.FirstOrDefault(e => e.cycleId == cycleId && !e.resolved);
            if (next != null) OpenSceneCard(next.id);
        }

        /// <summary>
        /// Puts the Pull up or takes it down, every frame and without a render. Worked out again
        /// only when something it reads has changed: the state, the walk-in, what has been turned
        /// down, and whether anything else has the screen.
        /// </summary>
        private void TickStoryPull()
        {
            if (hud == null) return;
            bool free = IsReady && !IsPanelOpen && !challengeActive && !CeremonyOverlays.OnScreen && playerIsActive;
            string walkIn = walkInFirst == null ? null : WalkInKey(walkInWeek, walkInRoom, walkInFirst, walkInSecond);
            if (!ReferenceEquals(pullFor, projected) || pullFree != free || pullWalkIn != walkIn || pullDeclined != declinedPulls.Count)
            {
                pullFor = projected; pullFree = free; pullWalkIn = walkIn; pullDeclined = declinedPulls.Count;
                pullNow = free ? CurrentPull(projected) : null;
            }
            PullOffered = pullNow?.Primary;
            hud.SetPull(pullNow);
        }

        private EpisodeState pullFor;
        private bool pullFree;
        private string pullWalkIn;
        private int pullDeclined;
        private EpisodeHud.StoryPull? pullNow;

        /// <summary>
        /// The room a story moment is being acted out in, or null: the first open scene or meeting
        /// with somebody in it, in a room the house builds. Read by the feed and the room icons, and
        /// worked out once per state.
        /// </summary>
        private string StagedRoom(EpisodeState state, out bool meeting)
        {
            if (!ReferenceEquals(stagedFor, state))
            {
                stagedFor = state; stagedRoom = null; stagedMeeting = false; stagedBeat = null;
                if (state != null && EpisodeEngine.StoryOn(state) && state.Find(state.playerId)?.status == ContestantStatus.Active)
                    foreach (var item in EpisodeEngine.OpenStoryBeats(state))
                    {
                        if (item.surface != StorySurfaces.Scene && item.surface != StorySurfaces.Meeting) continue;
                        if (!item.cast.Any(role => !string.IsNullOrEmpty(role.contestantId) && role.contestantId != state.playerId)) continue;
                        var room = VenueRoom(item.venue);
                        if (room == null) continue;
                        stagedRoom = room; stagedMeeting = item.surface == StorySurfaces.Meeting; stagedBeat = item;
                        break;
                    }
            }
            meeting = stagedMeeting;
            return stagedRoom;
        }

        private EpisodeState stagedFor;
        private string stagedRoom;
        private bool stagedMeeting;
        private HouseEventState stagedBeat;

        // ---------------------------------------------------------------- staging (plan §5.1, Scene)

        /// <summary>How long a scene holds its people before the house lets them go about their day.</summary>
        public const float SceneHoldSeconds = 60f;
        /// <summary>The most people a scene gathers: the beat's own, never a crowd.</summary>
        public const int SceneCastLimit = 4;
        /// <summary>
        /// The spots a scene offers its people: every point of the ring, so each can take the first
        /// their own body clears - a room with furniture on the ring still gathers its scene.
        /// </summary>
        private const int SceneCandidates = 24;

        private string sceneStagedBeat;
        private float sceneStagedAt;

        /// <summary>
        /// Gathers a staged moment's people in its room: once per beat, while it is open and the
        /// house is free, and let go the moment it is answered or lapses, the player turns it down,
        /// the free time ends, or it has held them long enough. Presentation only - nothing saved.
        /// </summary>
        private void TickSceneStage()
        {
            if (npcMeetings == null || !IsReady) return;
            // A ceremony staged in the house has it: a scene never stands its people in the aisles
            // during a nomination or an eviction, or takes the card's camera. BeginCeremonyStage ends a
            // scene already up; this covers the ceremony's gathering, before its every lease exists.
            if (IsCeremonyStaged || npcMeetings.HasCeremonyStage)
            {
                if (npcMeetings.HasSceneStage) npcMeetings.EndSceneStage();
                return;
            }
            var state = projected;
            StagedRoom(state, out _);
            var beat = stagedBeat;
            bool freeTime = state != null && (state.phase == EpisodePhase.Social || state.phase == EpisodePhase.Campaign);
            bool wanted = beat != null && freeTime && !declinedPulls.Contains(beat.id);
            if (npcMeetings.HasSceneStage)
            {
                bool stale = !wanted || npcMeetings.SceneStageKey != beat.id
                    || (!IsPanelOpen && Time.unscaledTime - sceneStagedAt > SceneHoldSeconds);
                if (stale) npcMeetings.EndSceneStage();
                return;
            }
            if (!wanted || sceneStagedBeat == beat.id || IsPanelOpen || challengeActive || OpeningOwnsHouse) return;
            sceneStagedBeat = beat.id;
            string room = VenueRoom(beat.venue);
            var ids = beat.involvedIds.Concat(beat.cast.Select(role => role.contestantId))
                .Where(id => !string.IsNullOrEmpty(id) && id != state.playerId && state.Find(id)?.status == ContestantStatus.Active)
                .Distinct().Take(SceneCastLimit).ToList();
            var places = ScenePlaces(room, SceneCandidates);
            if (places.Count == 0) return;
            if (npcMeetings.BeginSceneStage(beat.id, ids, places) > 0) sceneStagedAt = Time.unscaledTime;
        }

        /// <summary>
        /// Places for a scene's people in its room: a loose ring round the room's middle, each on the
        /// walkable floor, in the room, clear of furniture, and a stride from the next - the gather the
        /// plan's staging asks for. Fewer than asked when the room is tight.
        ///
        /// <para>The NavMesh has holes only under the set pieces it was baked with, and a body's
        /// clearance is checked against sight, which furniture is not part of; so a seat cloned at run
        /// time - the ceremonies' hot seats and gallery rows - is caught only by the furniture test here.</para>
        /// </summary>
        private List<Vector3> ScenePlaces(string room, int count)
        {
            var places = new List<Vector3>();
            var marker = room == null ? null : RoomMarker(room);
            if (marker == null || count <= 0 || !HouseRoomQuery.TryCreate(gameObject.scene, out var rooms, out _)) return places;
            Physics.SyncTransforms();
            var centre = marker.transform.position;
            for (int ring = 0; ring < 3 && places.Count < count; ring++)
            {
                float radius = 1.2f + ring * 0.6f;
                for (int step = 0; step < 8 && places.Count < count; step++)
                {
                    float angle = (step * 45f + ring * 22.5f) * Mathf.Deg2Rad;
                    var wanted = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                    if (!NavMesh.SamplePosition(wanted, out var hit, 0.8f, NavMesh.AllAreas)) continue;
                    if (!rooms.TryLocate(hit.position, 0.3f, out var at) || at != room) continue;
                    if (InsideFurniture(hit.position)) continue;
                    if (places.Any(p => (p - hit.position).sqrMagnitude < 1.1f * 1.1f)) continue;
                    places.Add(hit.position);
                }
            }
            return places;
        }

        /// <summary>For the tests: the places a scene in this room would offer its people now.</summary>
        public IReadOnlyList<Vector3> ScenePlacesForDiagnostics(string room, int count) => ScenePlaces(room, count);

        private static readonly Collider[] sceneFurniture = new Collider[8];

        /// <summary>
        /// Whether a body standing here would be inside furniture: the ceremony marks' own capsule
        /// (CeremonySets) against the furniture layer. The caller syncs the transforms.
        /// </summary>
        private bool InsideFurniture(Vector3 feet) =>
            gameObject.scene.GetPhysicsScene().OverlapCapsule(feet + Vector3.up * 0.35f, feet + Vector3.up * 1.5f, 0.3f,
                sceneFurniture, 1 << HouseLayers.Furniture, QueryTriggerInteraction.Ignore) > 0;

        /// <summary>
        /// Step in on a walk-in: the engine's own walk-in, committed now with the pair the player
        /// found and the room they found them in, then its card, opened where they stand.
        /// </summary>
        private void StepIntoWalkIn()
        {
            var state = projected;
            if (state == null || walkInFirst == null) return;
            string first = walkInFirst, second = walkInSecond, room = walkInRoom;
            ClearWalkIn();
            if (!EpisodeEngine.ProximityOpen(state, first, second, room)) return;
            var before = new HashSet<string>(EpisodeEngine.OpenStoryBeats(state).Select(e => e.id));
            var result = Submit(new EpisodeCommand
            {
                id = Guid.NewGuid().ToString("N"), actorId = state.playerId,
                kind = EpisodeCommandKind.WitnessProximity, expectedPhase = state.phase,
                expectedRevision = state.revision, targetId = first, secondTargetId = second, text = room,
            });
            if (result == null || !result.accepted) return;
            var opened = EpisodeEngine.OpenStoryBeats(result.state).FirstOrDefault(e => !before.Contains(e.id));
            if (opened != null) OpenSceneCard(opened.id);
        }

        /// <summary>
        /// Step in, or join the meeting: the card where the player stands if the moment is in the
        /// room they are in, or has no room; otherwise the walk there, and the card on arrival. A
        /// room the house cannot reach opens the card where they are rather than not at all.
        /// </summary>
        private void StepInto(string eventId, string room)
        {
            // The screen's room and the diary's each open their own panel on arrival, so a moment
            // staged in either opens where the player stands instead.
            if (room == null || !playerIsActive || room == beaconPlayerRoom || room == StationRoomId() || room == "Private")
            { OpenSceneCard(eventId); return; }
            GoToRoom(room);
            if (arrivingIn == room) { sceneBeatPending = eventId; return; }
            // A warp lands at once, and a room out of reach is no reason to lose the moment.
            OpenSceneCard(eventId);
        }

        /// <summary>A Step in's walk has arrived: the card it was walking to, if the moment is still open.</summary>
        private bool OpenPendingScene()
        {
            if (sceneBeatPending == null) return false;
            string eventId = sceneBeatPending;
            sceneBeatPending = null;
            return OpenSceneCard(eventId);
        }

        /// <summary>
        /// The Moment (plan §5.1): the beat's card, opened out in the house rather than at the
        /// episode screen. It is the one new modal state. It follows its story through any beat
        /// that chains on, and once the story leaves nothing to answer it says what came of it and
        /// hands the player back to the house.
        /// </summary>
        public bool OpenSceneCard(string eventId)
        {
            var state = projected;
            var item = state?.houseEvents.FirstOrDefault(e => e.id == eventId && !e.resolved);
            if (!IsReady || blockedRecovery || item == null || !playerIsActive) return false;
            PauseNpcSocialForPanel();
            ClosePanels();
            sceneCardOpen = true;
            sceneCardCycle = item.cycleId ?? item.id;
            // What the card will say changed is what is logged from here on.
            sceneCardSince = state.events.Count == 0 ? 0 : state.events.Max(e => e.sequence);
            player.SetInputEnabled(false);
            if (cameraRig != null) cameraRig.ControlsEnabled = false;
            Render();
            return true;
        }

        /// <summary>The scene card's content: the story's open beat, or what came of it and the way back.</summary>
        private void SceneCard(EpisodeState state)
        {
            var item = EpisodeEngine.OpenStoryBeats(state).FirstOrDefault(e => (e.cycleId ?? e.id) == sceneCardCycle);
            if (item != null && state.Find(state.playerId)?.status == ContestantStatus.Active)
            {
                RenderStoryBeat(state, item);
                if (storyStepEvent == null) hud.Action(EpisodeHud.NotNowCaption, ClosePanels);
                return;
            }
            hud.SetActivityLayout(EpisodeHud.ActivityLayout.Stage);
            if (PlayCameOfIt(state)) return;
            hud.Heading("WHAT CAME OF IT");
            var outcome = state.events.LastOrDefault(e => e.kind == StoryLog.Outcome
                && (e.audienceIds.Count == 0 || e.audienceIds.Contains(state.playerId)));
            hud.Paragraph(outcome != null ? StoryText.Log(state, outcome) : "The moment has passed.");
            hud.Action(SceneCardDoneCaption, ClosePanels);
        }

        /// <summary>The event sequence when the scene card opened: its receipts are the ones logged after it.</summary>
        private int sceneCardSince;

        /// <summary>
        /// A play's end of the card (plan 30 §4): won, part-won, lost or still in play; what the last
        /// step did; and the receipts, one line for each thing that changed. False for a card whose
        /// story is not a play.
        /// </summary>
        private bool PlayCameOfIt(EpisodeState state)
        {
            var cycle = state.storylines.FirstOrDefault(x => x.id == sceneCardCycle);
            var play = StoryCatalog.Find(cycle?.templateId)?.play;
            if (play == null) return false;
            string ending = StorylineStatus.Running(cycle.status) ? null : cycle.endingId;
            hud.Heading(ending == PlayEndings.Won ? "PLAY WON" : ending == PlayEndings.Part ? "PART OF THE WAY"
                : ending == PlayEndings.Lost ? "PLAY LOST" : "STILL IN PLAY");
            bool Mine(EpisodeEvent e) => e.sequence > sceneCardSince && (e.audienceIds.Count == 0 || e.audienceIds.Contains(state.playerId));
            var step = state.events.LastOrDefault(e => e.kind == StoryLog.Outcome && Mine(e));
            if (step != null) hud.Paragraph(StoryText.Log(state, step));
            var decided = ending == null ? null : state.events.LastOrDefault(e => e.kind == StoryLog.Play && Mine(e));
            if (decided != null) hud.Paragraph(decided.text);
            else if (ending == null)
                hud.Paragraph("Goal: " + StoryText.Fill(state, play.goal, cycle.cast) + " It is decided by " + StoryText.ClosesAt(play.deadline) + ".");
            foreach (var receipt in state.events.Where(e => e.kind == StoryLog.Receipt && Mine(e)))
                hud.Paragraph(receipt.text);
            hud.Action(SceneCardDoneCaption, ClosePanels);
            return true;
        }
    }
}
