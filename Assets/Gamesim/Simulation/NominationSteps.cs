using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The nomination's steps this week, as the screen's tracker shows them (PACK8-PASS-PLAN B1,
    /// mockup 72). Read from the committed state on every render and never saved: the week's story
    /// beats, the Head of Household's decision, the ceremony and what came of it.
    ///
    /// <para>The steps are built from <see cref="EpisodeState.houseEvents"/>, not from fixed words.
    /// The pack's README draws "Lobby, Stay Off the Block, Nomination Ceremony, Outcome" every week,
    /// but The Lobby is a coin toss that needs a houseguest at the head of the house, and Stay Off
    /// the Block is cast only in some weeks; next week is a different set of stories. So a story
    /// cycle is a step for as long as one of its beats waits on the player or it closed at this
    /// nomination, and the tracker is three or four steps (decision 3).</para>
    ///
    /// <para>Exactly one step is current, by the plan's rule: a waiting beat the player opened from
    /// the tracker; else, for a player Head of Household who has not nominated, the picker, so the
    /// walks can always press the names and "Commit nominations" at once; else the first open beat;
    /// else the ceremony; else the outcome. Only a waiting story that is not current can be pressed.
    /// Nothing here reads a hidden score, a feeling or anything the player was not shown.</para>
    /// </summary>
    public static class NominationSteps
    {
        /// <summary>What a step is.</summary>
        public enum Kind { HeadOfHousehold, Story, Picker, Ceremony, Outcome }

        /// <summary>Where a step stands. A story that lapsed is over, but it was let pass, not answered.</summary>
        public enum Standing { Done, LetPass, Waiting, Current, Next }

        /// <summary>The most steps the tracker holds (decision 3: three or four segments).</summary>
        public const int MostSteps = 4;

        public const string HeadOfHouseholdTitle = "Head of Household";
        public const string PickerTitle = "Name your nominees";
        public const string CeremonyTitle = "Nomination Ceremony";
        public const string OutcomeTitle = "Outcome";
        /// <summary>The step the week's later stories fold into when there are more than the tracker has room for.</summary>
        public const string FoldedTitle = "More storylines";

        /// <summary>One step of the week.</summary>
        public sealed class Step
        {
            public Kind kind;
            /// <summary>The step's name, without its number.</summary>
            public string title;
            /// <summary>Its number and name, "2. The Lobby": what the tracker prints, and a waiting story's caption.</summary>
            public string label;
            public Standing standing;
            /// <summary>
            /// For a story: the open beat it stands for - the one on screen when it is current, the
            /// one pressing it opens when it waits. Null for a story that is over, and for every
            /// other kind.
            /// </summary>
            public string eventId;
            /// <summary>How many of the week's story cycles the step stands for: more than one once folded.</summary>
            public int stories;

            /// <summary>Whether the tracker draws the step as a control: a waiting story that is not the current step.</summary>
            public bool Opens => kind == Kind.Story && standing == Standing.Waiting && eventId != null;
        }

        /// <summary>The word under a step's name, so where it stands is never said by colour alone.</summary>
        public static string StandingWord(Standing standing)
        {
            switch (standing)
            {
                case Standing.Done: return "Complete";
                case Standing.LetPass: return "Let pass";
                case Standing.Waiting: return "Waiting on you";
                case Standing.Current: return "Current";
                default: return "Next";
            }
        }

        /// <summary>Whether the player is the Head of Household with their two names still to say.</summary>
        public static bool Picking(EpisodeState s) =>
            s != null && s.phase == EpisodePhase.Nomination && s.nominees.Count == 0 && s.hohId != null
            && s.hohId == s.playerId && s.Find(s.playerId)?.status == ContestantStatus.Active;

        /// <summary>
        /// The beats a player Head of Household's "Commit nominations" lets pass: the open ones that
        /// close at the nominations, and any left over from before last week, as the engine's
        /// <c>Nominate</c> lapses them. Empty for anybody else, whose way on is "Continue episode" and
        /// whose warning is <see cref="EpisodeEngine.LapsingOnAdvance"/>.
        /// </summary>
        public static List<HouseEventState> LapsingOnNominate(EpisodeState s)
        {
            if (!Picking(s) || !EpisodeEngine.StoryOn(s)) return new List<HouseEventState>();
            return EpisodeEngine.OpenStoryBeats(s)
                .Where(e => e.closesAnchor == StoryAnchors.NomsSet || e.week < s.week - 1).ToList();
        }

        /// <summary>The step that is current: exactly one of any list <see cref="Build"/> returns.</summary>
        public static Step Current(IReadOnlyList<Step> steps) => steps?.FirstOrDefault(step => step.standing == Standing.Current);

        /// <summary>
        /// The week's steps, oldest first. <paramref name="opened"/> is the beat the player opened
        /// from the tracker, if any: view state, which takes the step only while that beat is open.
        /// Empty outside the nomination.
        /// </summary>
        public static List<Step> Build(EpisodeState s, string opened = null)
        {
            var steps = new List<Step>();
            if (s == null || s.phase != EpisodePhase.Nomination) return steps;
            bool active = s.Find(s.playerId)?.status == ContestantStatus.Active;
            bool named = s.nominees.Count > 0;
            bool playerHoh = s.hohId != null && s.hohId == s.playerId;
            bool picking = Picking(s);

            // The week's story cycles, in the order their first beat reached the house. A beat still
            // open is on the screen whatever week it came in, as the episode screen always drew it; a
            // settled one belongs to this nomination only if it closed here this week. A houseguest
            // no longer in the house is shown no story.
            var cycles = new List<List<HouseEventState>>();
            var keys = new List<string>();
            if (active)
                foreach (var item in s.houseEvents)
                {
                    if (item == null || !item.IsStory) continue;
                    if (item.resolved && !(item.week == s.week && item.closesAnchor == StoryAnchors.NomsSet)) continue;
                    string key = item.cycleId ?? item.id;
                    int at = keys.IndexOf(key);
                    if (at < 0) { keys.Add(key); cycles.Add(new List<HouseEventState> { item }); }
                    else cycles[at].Add(item);
                }

            // The fixed steps: the ceremony and the outcome, and the picker before them for a player
            // Head of Household, before and after the names are said. The stories take what is left.
            int slots = MostSteps - (playerHoh ? 3 : 2);
            if (cycles.Count < slots) steps.Add(new Step { kind = Kind.HeadOfHousehold, title = HeadOfHouseholdTitle, standing = Standing.Done });
            if (cycles.Count <= slots)
                foreach (var cycle in cycles) steps.Add(StoryStep(cycle, opened));
            else
            {
                for (int i = 0; i < slots - 1; i++) steps.Add(StoryStep(cycles[i], opened));
                steps.Add(Folded(cycles.Skip(slots - 1).ToList(), opened));
            }
            if (playerHoh)
                steps.Add(new Step { kind = Kind.Picker, title = PickerTitle, standing = named ? Standing.Done : Standing.Next });
            steps.Add(new Step { kind = Kind.Ceremony, title = CeremonyTitle, standing = named ? Standing.Done : Standing.Next });
            steps.Add(new Step { kind = Kind.Outcome, title = OutcomeTitle, standing = Standing.Next });

            // The current step, by the plan's rule, in order.
            var current = opened == null ? null
                : steps.FirstOrDefault(step => step.kind == Kind.Story && step.standing == Standing.Waiting && step.eventId == opened);
            if (current == null && picking) current = steps.First(step => step.kind == Kind.Picker);
            if (current == null) current = steps.FirstOrDefault(step => step.kind == Kind.Story && step.standing == Standing.Waiting);
            if (current == null && !named) current = steps.First(step => step.kind == Kind.Ceremony);
            if (current == null) current = steps.First(step => step.kind == Kind.Outcome);
            current.standing = Standing.Current;

            for (int i = 0; i < steps.Count; i++) steps[i].label = (i + 1) + ". " + steps[i].title;
            return steps;
        }

        /// <summary>
        /// One story cycle's step: waiting while a beat of it is open, and then let pass or done by
        /// what its last beat took - the lapse option is what a beat takes when the week moves on
        /// without an answer. Its name is the arc's, which does not change from beat to beat.
        /// </summary>
        private static Step StoryStep(List<HouseEventState> beats, string opened)
        {
            var open = beats.Where(beat => !beat.resolved).ToList();
            var first = beats[0];
            string title = StoryText.ArcOf(first)?.title ?? StoryText.Title(first);
            if (string.IsNullOrEmpty(title)) title = "Storyline";
            var step = new Step { kind = Kind.Story, title = title, stories = 1 };
            if (open.Count > 0)
            {
                step.standing = Standing.Waiting;
                step.eventId = open.Any(beat => beat.id == opened) ? opened : open[0].id;
            }
            else step.standing = LetPass(beats.Last()) ? Standing.LetPass : Standing.Done;
            return step;
        }

        /// <summary>The cycles past the tracker's room, as one step: waiting while any of them waits, with the beat pressing it opens.</summary>
        private static Step Folded(List<List<HouseEventState>> cycles, string opened)
        {
            var open = cycles.SelectMany(beats => beats).Where(beat => !beat.resolved).ToList();
            var step = new Step { kind = Kind.Story, title = FoldedTitle, stories = cycles.Count };
            if (open.Count > 0)
            {
                step.standing = Standing.Waiting;
                step.eventId = open.Any(beat => beat.id == opened) ? opened : open[0].id;
            }
            else step.standing = cycles.All(beats => LapsedOut(beats)) ? Standing.LetPass : Standing.Done;
            return step;
        }

        private static bool LapsedOut(List<HouseEventState> beats) => LetPass(beats.Last());

        /// <summary>Whether a settled beat was let pass: it took its lapse option.</summary>
        private static bool LetPass(HouseEventState beat)
        {
            if (beat == null || !beat.resolved || string.IsNullOrEmpty(beat.lapseOptionId)) return false;
            return beat.chosenIndex >= 0 && beat.chosenIndex < beat.choices.Count
                && beat.choices[beat.chosenIndex].optionId == beat.lapseOptionId;
        }
    }
}
