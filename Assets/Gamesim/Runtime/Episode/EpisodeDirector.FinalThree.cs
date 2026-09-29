using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;

namespace Gamesim.Episode
{
    /// <summary>
    /// The Final 3's two finalist screens (ENDGAME-PLAN F2).
    ///
    /// <para><b>Compare the finalists</b> opens from a free tile on Endgame Preparation, the window
    /// at three: the other two side by side, each with what the player knows and how sure they
    /// can be of it (<see cref="FinalistRead"/>), and what taking them would mean if the player
    /// wins the final Head of Household. It commits nothing and costs nothing.</para>
    ///
    /// <para><b>The decision</b> is the final eviction with the player as Head of Household: the
    /// same two cards, what taking each one means, and under each card the headline "Take {name}
    /// to the Final 2", "This decision cannot be undone" and the control that does it - the other
    /// finalist's pinned <c>Evict {name}</c>. The warning sits on the control: the screen opens
    /// scrolled to its first control, and a warning at the head of a tall screen was out of sight.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        public const string CompareFinalistsCaption = "Compare the finalists";
        public const string BackToPreparationCaption = "Back to endgame preparation";
        public const string FinalChoiceWarning = "This decision cannot be undone.";

        /// <summary>Whether the comparison is open over Endgame Preparation. View state, like a houseguest's screen.</summary>
        private bool comparingFinalists;

        /// <summary>Whether the comparison is open. A read for tests.</summary>
        public bool ComparingFinalists => comparingFinalists;

        /// <summary>The window at three, for a player who is one of the three: Endgame Preparation.</summary>
        public static bool Preparing(EpisodeState state) =>
            state != null && state.phase == EpisodePhase.Social && EpisodeHud.IsFinalThree(state)
            && state.Active.Any(actor => actor.id == state.playerId);

        /// <summary>Opens the comparison over Endgame Preparation. Public for tests.</summary>
        public void OpenFinalistComparison()
        {
            if (!Preparing(projected)) return;
            moveScreenId = null; juryHouseOpen = false; finalCaseOpen = false;
            comparingFinalists = true;
            Render();
        }

        /// <summary>Back to Endgame Preparation.</summary>
        public void CloseFinalistComparison()
        {
            comparingFinalists = false;
            Render();
        }

        /// <summary>The free tile that opens the comparison, first among the moves at three.</summary>
        private static EpisodeHud.MoveTile CompareTile(System.Action open) => new EpisodeHud.MoveTile
        {
            Caption = CompareFinalistsCaption,
            Description = "What you know about the other two, how sure you can be of it, and what taking each would mean.",
            Corner = "Free", CornerTint = UiTheme.Allied, Glyph = "people", Foot = "Costs no action", Choose = open,
        };

        private void FinalistComparison(EpisodeState state)
        {
            hud.ScreenHead("COMPARE THE FINALISTS", "WHAT YOU KNOW",
                "The other two as the house has shown them to you. Nothing here is a prediction.");
            hud.Action(BackToPreparationCaption, CloseFinalistComparison);
            hud.CertaintyLegend();
            var others = FinalistRead.Others(state);
            var columns = new List<EpisodeHud.FinalistColumn>();
            foreach (var take in others)
            {
                var cut = others.FirstOrDefault(other => other.id != take.id);
                columns.Add(new EpisodeHud.FinalistColumn
                {
                    Actor = take, Read = FinalistRead.Read(state, take.id),
                    BulletsHeading = "IF YOU WIN AND TAKE " + FinalistRead.FirstName(take.name).ToUpperInvariant(),
                    Bullets = cut != null ? FinalistRead.IfYouTake(state, take.id, cut.id) : null,
                });
            }
            hud.FinalistColumns(columns);
        }

        /// <summary>
        /// The final Head of Household's choice: the two finalists as cards, what taking each means,
        /// the warning, and under each card "Take {name} to the Final 2" over the control that
        /// does it, which evicts the other and keeps the caption the tests and the season walks
        /// press, <c>Evict {name}</c>.
        /// </summary>
        private void FinalTwoChoice(EpisodeState state)
        {
            var others = FinalistRead.Others(state);
            if (others.Count != 2)
            {
                // Not a shape the engine produces; the plain paired choice it used to be.
                var pairs = hud.Pairs();
                foreach (var candidate in others) { string id = candidate.id; hud.PairedActionFor(pairs, id, "Evict " + candidate.name, () => Commit(state, EpisodeCommandKind.FinalEvict, id)); }
                return;
            }
            hud.CertaintyLegend();
            // Whatever the choice: the Final 2 agreements with anyone but the two finalists end broken.
            var broken = FinalistRead.BrokenEitherWay(state);
            if (broken.Count > 0)
                hud.Footnote("Either way, your Final 2 " + (broken.Count == 1 ? "agreement" : "agreements") + " with "
                    + string.Join(" and ", broken) + " (on the jury) will end broken.", UiTheme.Warning);
            var columns = new List<EpisodeHud.FinalistColumn>();
            foreach (var take in others)
            {
                var cut = others.First(other => other.id != take.id);
                string cutId = cut.id;
                columns.Add(new EpisodeHud.FinalistColumn
                {
                    Actor = take, Read = FinalistRead.Read(state, take.id),
                    Bullets = FinalistRead.IfYouTake(state, take.id, cut.id),
                    Headline = "Take " + take.name + " to the Final 2",
                    Caption = "Evict " + cut.name,
                    Warning = FinalChoiceWarning,
                    Choose = () => Commit(state, EpisodeCommandKind.FinalEvict, cutId),
                });
            }
            hud.FinalistColumns(columns);
        }
    }
}
