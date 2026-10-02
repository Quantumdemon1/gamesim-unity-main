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
    /// <para><b>The decision</b> is the final eviction with the player as Head of Household: a page
    /// on the strategy stage (UI-UX-PASS-PLAN Q0, EpisodeHud.FinalChoice.cs), a card per finalist -
    /// the portrait card, the three public stat tiles, the jurors' chips coloured by the lean the
    /// player can tell, the bullets of what taking them means - and under each card "This decision
    /// cannot be undone", what the choice does, and the control that makes it, "Take Maya · evict
    /// Taylor" (<see cref="FinalChoiceWords.Caption"/>). It was "Evict {name}" under the other
    /// finalist's card and a gold headline that read as the button; every test and walk that
    /// presses the choice presses the new caption through <see cref="FinalChoiceWords.CaptionToEvict"/>.</para>
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
        /// The line under the band on the final Head of Household's page (MOCKUP-PASS M8, mockup 59):
        /// the band already names the screen, CHOOSE YOUR FINAL TWO, under the crown.
        /// </summary>
        public const string FinalTwoHeadLine = "What you know about each of them, and what taking each one means. Nothing here is a prediction.";

        /// <summary>
        /// Whether the panel is the final Head of Household's page: the player's choice between two
        /// finalists, with nothing of its own over it.
        /// </summary>
        private bool FinalChoiceBeat(EpisodeState s) => s != null && !challengeActive && s.pendingDiary == null
            && s.phase == EpisodePhase.FinalEviction && s.hohId == s.playerId && FinalistRead.Others(s).Count == 2;

        /// <summary>
        /// What taking <paramref name="take"/> does, as the engine settles it: the other finalist
        /// joins the jury, and the jury's questions come to the two left (EpisodeFinale's exchanges,
        /// each asked of the player with the other finalist's answer recorded beside it). By the
        /// names the page calls the two (<see cref="FinalChoiceWords.Names"/>): whole where they share
        /// a first name.
        /// </summary>
        public static string FinalChoiceConsequence(ContestantState take, ContestantState cut)
        {
            var (taken, gone) = FinalChoiceWords.Names(take.name, cut.name);
            return gone + " joins the jury; you and " + taken + " face the jury's questions.";
        }

        /// <summary>
        /// The final Head of Household's choice as a page (UI-UX-PASS-PLAN Q0): a card per finalist
        /// with the player's read of them - the record, where the player stands, the jurors' leans as
        /// chips, what taking them means - and under each card the warning, what the choice does and
        /// the control that makes it, "Take Maya · evict Taylor", which evicts the other. Every word is
        /// the read's (<see cref="FinalistRead"/>): nothing here is a prediction.
        /// </summary>
        private void FinalTwoChoice(EpisodeState state)
        {
            var others = FinalistRead.Others(state);
            if (others.Count != 2)
            {
                // Not a shape the engine produces; the plain paired choice it used to be.
                var pairs = hud.Pairs();
                foreach (var candidate in others)
                {
                    string id = candidate.id;
                    hud.PairedActionFor(pairs, id, FinalChoiceWords.FallbackCaption(candidate.name), () => Commit(state, EpisodeCommandKind.FinalEvict, id));
                }
                return;
            }
            // Whatever the choice: the Final 2 agreements with anyone but the two finalists end broken.
            var spec = new EpisodeHud.FinalChoiceSpec
            {
                Line = FinalTwoHeadLine,
                EitherWay = FinalChoiceWords.EitherWay(FinalistRead.BrokenEitherWay(state)),
                Columns = new List<EpisodeHud.FinalChoiceColumn>(),
            };
            // The jurors by the names their chips carry: first names, whole where two share one.
            var jurors = FinalistRead.Jurors(state);
            var chipNames = FinalChoiceWords.ChipNames(jurors.Select(juror => juror.name).ToList());
            var named = new Dictionary<string, string>();
            for (int i = 0; i < jurors.Count; i++) named[jurors[i].id] = chipNames[i];
            foreach (var take in others)
            {
                var cut = others.First(other => other.id != take.id);
                string cutId = cut.id;
                var read = FinalistRead.Read(state, take.id);
                spec.Columns.Add(new EpisodeHud.FinalChoiceColumn
                {
                    Actor = take, Read = read,
                    // Each juror's lean toward this finalist as the player can tell it, grouped close, a grudge, no read.
                    Jurors = FinalChoiceWords.Grouped(read.jurors).Select(lean => new EpisodeHud.FinalChoiceJuror
                    {
                        Id = lean.jurorId, Name = named.ContainsKey(lean.jurorId) ? named[lean.jurorId] : lean.name,
                        Lean = lean.lean, Certainty = lean.certainty,
                    }).ToList(),
                    BulletsHeading = FinalChoiceWords.BulletsEyebrow(take.name, cut.name),
                    Bullets = FinalistRead.IfYouTake(state, take.id, cut.id),
                    Caption = FinalChoiceWords.Caption(take.name, cut.name),
                    Warning = FinalChoiceWarning,
                    Consequence = FinalChoiceConsequence(take, cut),
                    // What this choice alone would break of the player's word (EpisodeDirector.YourWord).
                    Breach = FinalChoiceBreach(state, take, cut),
                    Choose = () => Commit(state, EpisodeCommandKind.FinalEvict, cutId),
                });
            }
            hud.FinalChoicePage(spec);
        }
    }
}
