using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;

namespace Gamesim.Episode
{
    /// <summary>
    /// The final Head of Household as an event (ENDGAME-PLAN F3): a card as each part opens,
    /// saying who won the part before and who plays now, and a card as the winner is crowned.
    ///
    /// <para>The cards key on the phase a commit arrives in, as the Final Three card does: the
    /// Advance behind "Continue to the next ceremony" takes Part 1 to Part 2, Part 2 to Part 3 and
    /// Part 3 to the final eviction, and none of those commits logs a competition, so no
    /// standings card is up to draw over them (the standings sit above every takeover). A part's
    /// own result is the standings card's, as it always was.</para>
    ///
    /// <para>The phase panel closes as a bracket card plays. The card takes no input of its own,
    /// and the click that moves it on would otherwise land on the next part's briefing beneath
    /// it, where a pressed "Accessible alternative" commits. The Final Three card arrives with the
    /// panel already closed, because its commit leaves the house for the yard; every walk, and
    /// every player, reopens the station for the next decision.</para>
    ///
    /// <para>Every line that can hold the player speaks to them ("You won Part 1 and wait in Part
    /// 3."): the default player is called "You", and a name with a third-person verb reads "You
    /// waits" (EpisodeEngine.Verb exists for the same reason).</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>"Part 1 of 3", or null outside the final Head of Household.</summary>
        public static string FinalHoHPartLabel(EpisodePhase phase) =>
            phase == EpisodePhase.FinalHoHPart1 ? "Part 1 of 3"
            : phase == EpisodePhase.FinalHoHPart2 ? "Part 2 of 3"
            : phase == EpisodePhase.FinalHoHPart3 ? "Part 3 of 3" : null;

        /// <summary>Whether a phase is one of the final Head of Household's three parts.</summary>
        public static bool IsFinalHoHPart(EpisodePhase phase) =>
            phase == EpisodePhase.FinalHoHPart1 || phase == EpisodePhase.FinalHoHPart2 || phase == EpisodePhase.FinalHoHPart3;

        /// <summary>
        /// What a final part is for, in the briefing's stakes line: Part 1 sends its winner straight
        /// to Part 3, Part 2 decides who meets them there, Part 3 crowns the final Head of
        /// Household. The line it replaced said the same for all three, which was wrong for two.
        /// </summary>
        public static string FinalPartStakes(EpisodeState state)
        {
            string winner1 = state.finalPart1WinnerId;
            switch (state.phase)
            {
                case EpisodePhase.FinalHoHPart1:
                    return "At stake: a seat in Part 3. The winner goes straight there; the other two play Part 2 for the other seat.";
                case EpisodePhase.FinalHoHPart2:
                    if (string.IsNullOrEmpty(winner1)) return "At stake: the other seat in Part 3.";
                    return winner1 == state.playerId ? "At stake: who joins you in Part 3."
                        : "At stake: the other seat in Part 3, against " + FinalistRead.FirstName(state.Find(winner1)?.name) + ".";
                case EpisodePhase.FinalHoHPart3:
                    return "At stake: the final Head of Household, and the choice of who sits beside them in the Final 2.";
                default:
                    return "At stake: progress toward the final Head of Household decision.";
            }
        }

        /// <summary>
        /// The card's line for the phase a part has just opened (Part 2, Part 3) or the crowning
        /// (the final eviction), from committed state, speaking to the player wherever they are in
        /// it. Null for any other phase.
        /// </summary>
        public static string FinalHoHLine(EpisodeState state)
        {
            string player = state.playerId;
            string Name(string id) => FinalistRead.FirstName(state.Find(id)?.name);
            string winner1 = state.finalPart1WinnerId, winner2 = state.finalPart2WinnerId;
            switch (state.phase)
            {
                case EpisodePhase.FinalHoHPart2:
                {
                    var field = EpisodeEngine.CompetitionPlayers(state).Select(c => c.id).ToList();
                    if (field.Count != 2 || string.IsNullOrEmpty(winner1))
                        return Localisation.Text("The Part 1 winner waits in Part 3. The other two play for the other seat.");
                    if (winner1 == player)
                        return Localisation.Format("You won Part 1 and wait in Part 3. {0} and {1} play for the other seat.", Name(field[0]), Name(field[1]));
                    if (field.Contains(player))
                        return Localisation.Format("{0} won Part 1 and waits in Part 3. You and {1} play for the other seat.",
                            Name(winner1), Name(field.First(id => id != player)));
                    return Localisation.Format("{0} won Part 1 and waits in Part 3. {1} and {2} play for the other seat.",
                        Name(winner1), Name(field[0]), Name(field[1]));
                }
                case EpisodePhase.FinalHoHPart3:
                {
                    if (string.IsNullOrEmpty(winner1) || string.IsNullOrEmpty(winner2)) return null;
                    var watching = state.Active.FirstOrDefault(c => c.id != winner1 && c.id != winner2);
                    string players = winner1 == player ? Localisation.Format("You and {0}", Name(winner2))
                        : winner2 == player ? Localisation.Format("You and {0}", Name(winner1))
                        : Localisation.Format("{0} and {1}", Name(winner1), Name(winner2));
                    string rest = watching == null ? ""
                        : watching.id == player ? " " + Localisation.Text("You watch.")
                        : " " + Localisation.Format("{0} watches.", Name(watching.id));
                    return Localisation.Format("{0} play for the final Head of Household.", players) + rest;
                }
                case EpisodePhase.FinalEviction:
                    if (string.IsNullOrEmpty(state.hohId)) return null;
                    return state.hohId == player
                        ? Localisation.Text("You won it. One decision remains: who sits beside you in the Final 2.")
                        : Localisation.Format("{0} won it, and now chooses who sits beside them in the Final 2.", Name(state.hohId));
                default:
                    return null;
            }
        }

        /// <summary>
        /// Plays the bracket or the crowning card when a commit arrives in Part 2, Part 3 or the
        /// final eviction from the part before it. Returns whether it played.
        /// </summary>
        private bool PlayFinalHoHCard(EpisodeState state, EpisodePhase wasPhase, HashSet<string> wasActive)
        {
            if (takeover == null || state == null || wasPhase == state.phase) return false;
            var subjects = new List<CeremonyTakeover.Subject>();
            var shown = new HashSet<string>();
            // Each face once, with the first badge it is given: the winners before the rest.
            void Add(string id, string badge)
            {
                var actor = state.Find(id);
                if (actor == null || !shown.Add(actor.id)) return;
                subjects.Add(new CeremonyTakeover.Subject(actor.name, badge, CharacterPortraits.Get(actor), actor));
            }

            string kind, title;
            if (wasPhase == EpisodePhase.FinalHoHPart1 && state.phase == EpisodePhase.FinalHoHPart2)
            {
                kind = CeremonyTakeover.FinalHoHPartKind;
                title = Localisation.Text("Final HoH · Part 2");
                Add(state.finalPart1WinnerId, "WON PART 1");
                foreach (var actor in EpisodeEngine.CompetitionPlayers(state)) Add(actor.id, "PART 2");
            }
            else if (wasPhase == EpisodePhase.FinalHoHPart2 && state.phase == EpisodePhase.FinalHoHPart3)
            {
                kind = CeremonyTakeover.FinalHoHPartKind;
                title = Localisation.Text("Final HoH · Part 3");
                Add(state.finalPart1WinnerId, "WON PART 1");
                Add(state.finalPart2WinnerId, "WON PART 2");
                foreach (var actor in state.Active) Add(actor.id, "WATCHING");
            }
            else if (wasPhase == EpisodePhase.FinalHoHPart3 && state.phase == EpisodePhase.FinalEviction && !string.IsNullOrEmpty(state.hohId))
            {
                kind = CeremonyTakeover.FinalHoHCrownedKind;
                title = Localisation.Text("Final Head of Household");
                Add(state.hohId, "FINAL HOH");
                foreach (var actor in state.Active) Add(actor.id, "FINAL 3");
            }
            else return false;

            EndCeremonyCards();
            takeover.Play(kind, state.week, subjects, reducedMotion, title, FinalHoHLine(state));
            if (kind == CeremonyTakeover.FinalHoHCrownedKind)
            {
                // The winner acts it out and the room turns to them: all three are still in the house.
                React(state.hohId, CharacterPresentation.Reaction.Won);
                TurnHeads(state, state.hohId, null);
            }
            // The next part's briefing is not pressed through the card (see the class summary).
            else if (phaseOpen) ClosePanelsInternal(false);
            return true;
        }
    }
}
