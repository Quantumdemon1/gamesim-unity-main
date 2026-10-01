using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;

namespace Gamesim.Episode
{
    /// <summary>
    /// The nomination as a screen of steps (PACK8-PASS-PLAN B1, mockup 72). On the strategy stage it
    /// is four status cards - the Head of Household, the phase, the conversations left and the
    /// player's objective - over a tracker of this week's steps, one step's body, and a footer with
    /// the way on and what comes next. It replaced a column that stacked every open story beat, the
    /// ceremony, a row of faces and the way on in one scroll (the owner's screenshots 66 to 69).
    ///
    /// <para>The steps come from <see cref="NominationSteps"/>, read from the committed state on
    /// every render. Which beat the player opened from the tracker, whether the Head of Household
    /// is on the backdoor view, and the two names they have picked are view state, like the free
    /// time's houseguest screen: nothing here is saved or committed until a control commits it,
    /// and every control commits through the command it always did.</para>
    ///
    /// <para>Every word on the cards is public or the player's own: who holds the house, the phase,
    /// the player's own budget, and an objective keyed to the player's role, never to a houseguest's
    /// private ranking.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>The waiting beat the player opened from the tracker, while it is open. View state.</summary>
        private string nominationOpenedBeat;
        /// <summary>Whether the Head of Household is on the backdoor view rather than the picker. View state.</summary>
        private bool nominationBackdoorView;
        /// <summary>The two names the Head of Household has picked, kept across a render until they commit. View state.</summary>
        private string nominationFirst, nominationSecond;
        /// <summary>The week the view state belongs to: a new week starts on the picker with nothing picked.</summary>
        private int nominationViewWeek = -1;

        /// <summary>What closing the panel, or a new week, throws away.</summary>
        private void ForgetNominationView()
        {
            nominationOpenedBeat = null;
            nominationBackdoorView = false;
            nominationFirst = nominationSecond = null;
            nominationViewWeek = -1;
        }

        /// <summary>
        /// Draws the nomination's screen when the phase panel is the nomination on the strategy
        /// stage, and says whether it did. Anything else - the diary's nomination, a layout that is
        /// not the stage - is left to the panel's own path.
        /// </summary>
        private bool NominationScreen(EpisodeState state)
        {
            if (state == null || hud == null || state.phase != EpisodePhase.Nomination
                || hud.CurrentActivityLayout != EpisodeHud.ActivityLayout.Strategy) return false;
            var hoh = state.Find(state.hohId);
            if (hoh == null) return false;
            if (nominationViewWeek != state.week) { ForgetNominationView(); nominationViewWeek = state.week; }
            bool active = state.Find(state.playerId)?.status == ContestantStatus.Active;
            bool picking = NominationSteps.Picking(state);
            if (!picking || !string.IsNullOrEmpty(state.backdoorTargetId)) nominationBackdoorView = false;
            if (!picking) nominationFirst = nominationSecond = null;
            var open = active ? EpisodeEngine.OpenStoryBeats(state) : new List<HouseEventState>();
            if (nominationOpenedBeat != null && !open.Any(beat => beat.id == nominationOpenedBeat)) nominationOpenedBeat = null;
            if (storyStepEvent != null && !open.Any(beat => beat.id == storyStepEvent)) ClearStoryStep();

            var steps = NominationSteps.Build(state, nominationOpenedBeat);
            var current = NominationSteps.Current(steps);
            if (current == null) return false;
            NominationStatus(state, hoh);
            NominationTracker(steps);
            var beat = current.kind == NominationSteps.Kind.Story ? open.FirstOrDefault(item => item.id == current.eventId) : null;
            if (beat != null) NominationStory(state, beat);
            else if (current.kind == NominationSteps.Kind.Picker)
            {
                if (nominationBackdoorView) NominationBackdoor(state);
                else NominationPicker(state);
            }
            else if (state.nominees.Count == 0) NominationCeremony(state, hoh);
            else NominationOutcome(state, hoh);
            NominationFooter(state, hoh, current, picking);
            return true;
        }

        // ------------------------------------------------------------ the header

        /// <summary>
        /// The four status cards (mockup 72): who holds the house, the phase and whose call it is,
        /// the player's own conversations left and what they count against, and the objective their
        /// role gives them this week.
        /// </summary>
        private void NominationStatus(EpisodeState state, ContestantState hoh)
        {
            var row = hud.StrategyHeaderRow(EpisodeHud.NominationStatusRowName, hud.StatusCardRowHeight(true));
            if (row == null) return;
            var player = state.Find(state.playerId);
            bool active = player?.status == ContestantStatus.Active, named = state.nominees.Count > 0, playerHoh = hoh.id == state.playerId;
            hud.StatusCard(row, 0, 4, "HOH", HudPrimitives.WithYou(hoh.name, hoh.isPlayer), "Head of Household",
                PackArt.Pack8StatusHoh, PackArt.Pack8IconHoh, "crown", UiTheme.Gold);
            hud.StatusCard(row, 1, 4, "PHASE", EpisodeHud.PhaseShort(state.phase),
                named ? "The block is set" : playerHoh ? "Your call" : FirstName(hoh.name) + "'s call",
                PackArt.Pack8StatusPhase, PackArt.Pack8IconPeople, "people", UiTheme.Danger);
            // The player's own budget, the number the top bar's Actions left shows, and what it is
            // counted against: the week's windows keep their seats apart, the old pool does not.
            hud.StatusCard(row, 2, 4, "CONVERSATIONS LEFT", active ? ActionsLeftCount(state).ToString() : "None",
                !active ? "You are watching" : EpisodeEngine.WeekRulesOn(state) ? "This window" : "This week",
                PackArt.Pack8StatusActions, PackArt.Pack8IconChat, "chat", UiTheme.Accent);
            NominationObjective(state, hoh, out string objective, out string why);
            hud.StatusCard(row, 3, 4, "OBJECTIVE", objective, why, PackArt.Pack8StatusPhase, PackArt.Pack8IconTarget, "target", UiTheme.Danger);
        }

        /// <summary>
        /// The player's objective this week, from their role alone and true by the rules: the Head
        /// of Household names two, a nominee plays for the veto by right, and anybody else could be
        /// named. Never whose list the player is on: a houseguest's ranking is theirs.
        /// </summary>
        private static void NominationObjective(EpisodeState state, ContestantState hoh, out string objective, out string why)
        {
            var player = state.Find(state.playerId);
            bool named = state.nominees.Count > 0;
            if (player == null || player.status != ContestantStatus.Active) { objective = "Watching"; why = "You are out of the house"; return; }
            if (hoh.id == state.playerId)
            {
                objective = named ? "Your nominees are named" : "Name two nominees";
                why = named ? "The veto can still change the block" : "Either can still win the veto";
                return;
            }
            if (state.nominees.Contains(state.playerId)) { objective = "Get off the block"; why = "Nominees play for the veto by right"; return; }
            objective = "Stay off the block";
            why = named ? "A used veto means a replacement" : FirstName(hoh.name) + " names two nominees";
        }

        /// <summary>The tracker: the week's steps, each with the word for where it stands; only a waiting story opens.</summary>
        private void NominationTracker(List<NominationSteps.Step> steps)
        {
            var row = hud.StrategyHeaderRow(EpisodeHud.NominationTrackerRowName, hud.StepTrackerHeight);
            if (row == null) return;
            var cells = new List<EpisodeHud.TrackerStep>();
            foreach (var step in steps)
            {
                var where = step.standing == NominationSteps.Standing.Current ? EpisodeHud.TrackerState.Current
                    : step.standing == NominationSteps.Standing.Done || step.standing == NominationSteps.Standing.LetPass ? EpisodeHud.TrackerState.Done
                    : EpisodeHud.TrackerState.Next;
                string eventId = step.eventId;
                cells.Add(new EpisodeHud.TrackerStep(step.label, NominationSteps.StandingWord(step.standing), where,
                    step.Opens ? () => OpenNominationBeat(eventId) : (Action)null));
            }
            hud.StepTracker(row, cells, UiTheme.Danger);
        }

        /// <summary>A waiting story pressed on the tracker: it becomes the step. Nothing is committed.</summary>
        private void OpenNominationBeat(string eventId)
        {
            nominationOpenedBeat = eventId;
            nominationBackdoorView = false;
            ClearStoryStep();
            Render();
        }

        // ------------------------------------------------------------ the steps

        /// <summary>A story beat as the step: its words beside its options, one beat at a time.</summary>
        private void NominationStory(EpisodeState state, HouseEventState item)
        {
            if (storyStepEvent == item.id)
            {
                var chosen = item.choices.FirstOrDefault(c => c.optionId == storyStepOption);
                if (chosen != null) { NominationStorySecondPress(state, item, chosen); return; }
                ClearStoryStep();
            }
            var tiles = item.choices.Select(choice => (StoryChoiceFor(state, item, choice), StoryTileSkin(item, choice))).ToList();
            hud.NominationStoryStep(StoryText.Eyebrow(item), StoryText.Title(item), StoryText.Narrative(state, item), tiles);
        }

        /// <summary>
        /// A tile's Pack 8 face, by the kind of answer and never by the arc that asks it: next week's
        /// beat is a different arc with the same kinds of answer. Taking a play on is the warm face,
        /// letting a moment pass the quiet one, and every other answer wears its risk.
        /// </summary>
        public static string StoryTileSkin(HouseEventState item, HouseEventChoice choice)
        {
            if (choice == null) return null;
            if (choice.optionId == PlayOptions.TakeItOn) return PackArt.Pack8ChoiceWarm;
            if (choice.lapse || (item != null && choice.optionId == item.lapseOptionId)) return PackArt.Pack8ChoiceQuiet;
            return choice.risk == HouseEventRisk.High ? PackArt.Pack8NomRiskHigh
                : choice.risk == HouseEventRisk.Medium ? PackArt.Pack8NomRiskSome : PackArt.Pack8NomRiskLow;
        }

        /// <summary>
        /// A story option's second press on the stage: who, as a grid of faces, for an option that
        /// names somebody; then the confirm, for a rule break. The same commands as the episode
        /// screen's own second press (EpisodeDirector.Story), under the same captions.
        /// </summary>
        private void NominationStorySecondPress(EpisodeState state, HouseEventState item, HouseEventChoice choice)
        {
            string eventId = item.id, optionId = choice.optionId;
            hud.NominationStepHead(StoryText.Eyebrow(item), StoryText.Title(item));
            if (choice.pickPerson && storyStepPick == null)
            {
                hud.Heading(choice.label.ToUpperInvariant() + ": WHO?");
                var people = choice.eligibleIds.Select(state.Find).Where(c => c != null && c.status == ContestantStatus.Active).ToList();
                if (people.Count == 0) hud.Paragraph("There is nobody left to name.");
                hud.NominationPeople(people.Select(person =>
                {
                    string id = person.id;
                    return (id, person.name, (Action)(() =>
                    {
                        if (choice.conduct) { storyStepPick = id; Render(); }
                        else { ClearStoryStep(); Commit(state, EpisodeCommandKind.ProgressStoryline, eventId, optionId, text: id); }
                    }));
                }).ToList(), 57f * hud.FontScale + 12f);
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

        /// <summary>
        /// The player Head of Household's step: the candidates as cards fitted to the step, the
        /// commit pinned, the comparison in the footer's secondary slot, and the backdoor plan
        /// behind a door of its own. The diary's nomination keeps its column (RenderPlayerDecision).
        /// </summary>
        private void NominationPicker(EpisodeState state)
        {
            var candidates = EpisodeEngine.NominationCandidates(state).Select(c => new EpisodeHud.Option(c.id, c.name)).ToArray();
            var aimed = string.IsNullOrEmpty(state.backdoorTargetId) ? null : state.Find(state.backdoorTargetId);
            string aim = aimed == null ? null : "This week is aimed at " + aimed.name + ". Nominate two others and use the veto to put them up.";
            hud.NominationPicker(candidates, nominationFirst, nominationSecond,
                (first, second) => { nominationFirst = first; nominationSecond = second; },
                (first, second) => OfferPlayerDecision(state, false, EpisodeCommandKind.Nominate,
                    "Nominate " + state.Find(first)?.name + " and " + state.Find(second)?.name
                    + ". Confirmed nominations become part of the episode record.", first, second),
                "Commit nominations", aim,
                string.IsNullOrEmpty(state.backdoorTargetId) ? () => { nominationBackdoorView = true; Render(); } : (Action)null);
        }

        /// <summary>
        /// The backdoor plan, behind the picker's door: who this week is really aimed at. It costs
        /// nothing and moves nobody - a plan the house cannot hear - and answering it goes back to
        /// the picker with the names already picked.
        /// </summary>
        private void NominationBackdoor(EpisodeState state)
        {
            var aims = EpisodeEngine.NominationCandidates(state).Select(aim =>
            {
                string id = aim.id;
                return (id, "Aim this week at " + aim.name, (Action)(() =>
                {
                    nominationBackdoorView = false;
                    Commit(state, EpisodeCommandKind.SetBackdoorPlan, id);
                }), Category(EpisodeCommandKind.SetBackdoorPlan));
            }).ToList();
            hud.NominationBackdoor("You can also settle on who this week is really aimed at. A backdoor plan "
                + "costs nothing, tells nobody, and is yours to change until you nominate.", aims);
        }

        /// <summary>
        /// The ceremony before the names (mockup 72): its name, what the Head of Household must do,
        /// the house's pointer to a word with them while there is time, and the house as faces with
        /// the crown on its Head, fitted to what is left of the step.
        /// </summary>
        private void NominationCeremony(EpisodeState state, ContestantState hoh)
        {
            hud.NominationTitle(NominationSteps.CeremonyTitle,
                hoh.name + " must nominate two houseguests for eviction. Whoever is named can still save themselves in the Power of Veto.",
                UiTheme.Danger, true);
            string pointer = WindowLine(state);
            if (pointer != null) hud.NominationNote(pointer, UiTheme.Accent);
            var house = new List<EpisodeHud.CeremonyFace> { new EpisodeHud.CeremonyFace(hoh.id, "HOH", UiTheme.Gold) };
            house.AddRange(state.Active.Where(c => c.id != hoh.id).Select(c => new EpisodeHud.CeremonyFace(c.id, null, UiTheme.Muted)));
            hud.NominationFaces("HOUSEGUESTS (" + state.Active.Count() + ")", house, 124f, id => NominationFaceSkin(state, id), state.playerId);
        }

        /// <summary>
        /// What came of it: the two on the block and who put them there. The player reads their own
        /// decision in the second person, and their own name marked as theirs.
        /// </summary>
        private void NominationOutcome(EpisodeState state, ContestantState hoh)
        {
            var block = state.nominees.Select(state.Find).Where(c => c != null).ToList();
            string names = string.Join(" and ", block.Select(c => HudPrimitives.WithYou(c.name, c.isPlayer)));
            string decided = hoh.id == state.playerId ? "You have made your decision."
                : hoh.name + " has made " + StoryPeople.Pronouns(hoh).their + " decision.";
            hud.NominationTitle("Nominated for Eviction", decided + " " + names + (block.Count == 1 ? " is" : " are") + " on the block.",
                UiTheme.Danger, true);
            var faces = block.Select(c => new EpisodeHud.CeremonyFace(c.id, "NOM", UiTheme.Danger)).ToList();
            faces.Add(new EpisodeHud.CeremonyFace(hoh.id, "HOH", UiTheme.Gold));
            hud.NominationFaces("On the block, and who put them there", faces, 150f, id => NominationFaceSkin(state, id), state.playerId);
        }

        /// <summary>A face's Pack 8 card, by the week's role: the crown's, the block's, or the house's.</summary>
        private static string NominationFaceSkin(EpisodeState state, string id) =>
            id == state.hohId ? PackArt.Pack8HouseguestHoh
            : state.nominees.Contains(id) ? PackArt.Pack8HouseguestNominee
            : PackArt.Pack8HouseguestNeutral;

        // ------------------------------------------------------------ the footer

        /// <summary>
        /// The footer: the way on pinned once, and a strip with what comes next or, outranking it,
        /// what moving on lets pass. The Head of Household's picker pins its own commit; a story or
        /// the backdoor view opened over it has the way back to the picker instead, and the strip
        /// says which storylines committing lets pass - once a silent lapse.
        /// </summary>
        private void NominationFooter(EpisodeState state, ContestantState hoh, NominationSteps.Step current, bool picking)
        {
            if (picking)
            {
                if (current.kind != NominationSteps.Kind.Picker || nominationBackdoorView)
                    hud.PinnedSecondary(EpisodeHud.BackToNomineesCaption, () =>
                    {
                        nominationOpenedBeat = null;
                        nominationBackdoorView = false;
                        ClearStoryStep();
                        Render();
                    });
                var lapsing = NominationSteps.LapsingOnNominate(state);
                if (lapsing.Count > 0) hud.PinnedNote("Committing lets " + StorylinesPassing(lapsing) + ".", AdvanceWarningName, true);
                else hud.PinnedNote(NominationUpNext(state, hoh), null, false);
                return;
            }
            hud.WearNominationPrimary(hud.PinnedAction("Continue episode", () => Commit(state, EpisodeCommandKind.Advance)));
            AdvanceWarning(state);
            MovingOnCosts(state);
            hud.PinnedNote(NominationUpNext(state, hoh), null, false);
        }

        /// <summary>"1 storyline pass: The Invite List (Not now)": each beat's title and its lapse, in the option's own words.</summary>
        private static string StorylinesPassing(List<HouseEventState> passing)
        {
            string Each(HouseEventState item)
            {
                var lapse = item.choices.FirstOrDefault(c => c.optionId == item.lapseOptionId);
                return StoryText.Title(item) + (lapse != null ? " (" + lapse.label + ")" : "");
            }
            return (passing.Count == 1 ? "1 storyline" : passing.Count + " storylines") + " pass: " + string.Join("; ", passing.Select(Each));
        }

        /// <summary>
        /// What comes next, in the week's own order and nothing more: who nominates, then the veto's
        /// draw and who plays in it by right. It never promises a staged scene, which batch runs and
        /// reduced motion do not play.
        /// </summary>
        public static string NominationUpNext(EpisodeState state, ContestantState hoh)
        {
            if (state.nominees.Count == 0)
                return (hoh.id == state.playerId ? "Up next: your nominations are revealed at the nomination ceremony."
                        : "Up next: find out who " + hoh.name + " nominates.")
                    + " After nominations, the Power of Veto players are drawn.";
            string byRight = state.nominees.Contains(state.playerId) ? "Nominees play by right."
                : hoh.id == state.playerId ? "As Head of Household you play by right."
                : "The Head of Household and both nominees play by right.";
            return "Up next: the Power of Veto player selection. " + byRight;
        }
    }
}
