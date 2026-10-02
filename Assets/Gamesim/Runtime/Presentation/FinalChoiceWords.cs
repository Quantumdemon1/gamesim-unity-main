using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The final Head of Household's choice in words (UI-UX-PASS-PLAN Q0): the caption of the one
    /// control under each finalist's card, "Take Maya · evict Taylor"; the names the jurors' chips
    /// carry; the order the chips are grouped in by what the player can tell of each juror's lean,
    /// and the word over each group. Unity-free, so the subset pins the caption every walk and test
    /// presses beside the read it is drawn from.
    ///
    /// <para>The caption replaced "Evict {name}" deliberately (a contract change, made with every
    /// test and walk that presses it): the control sat under the card of the finalist it kept and
    /// named the other one, under a gold headline that read as the button but was a label (the show
    /// sweep's row 37). One control now says both halves of the choice.</para>
    /// </summary>
    public static class FinalChoiceWords
    {
        /// <summary>The caption's two halves: "Take " the finalist kept, " · evict " the finalist cut.</summary>
        public const string TakeWord = "Take ", EvictWord = " · evict ";

        /// <summary>The words over the chips' three groups: close to the finalist, a reason for a grudge, and no read either way.</summary>
        public const string CloseWord = "CLOSE", GrudgeWord = "GRUDGE", NoReadWord = "NO READ";

        /// <summary>
        /// What a chip's look says, under the cards: how sure the player can be of a lean, the
        /// certainty legend's three (Confirmed, Suspected, Unknown) said by the chip's fill and colour.
        /// </summary>
        public const string Legend = "Filled: you saw it or were part of it. Outlined: you only heard it. Grey: no read either way.";

        /// <summary>The line in place of the chips when nobody is on the jury yet.</summary>
        public const string NoJury = "Nobody is on the jury yet.";

        /// <summary>The leans in the order the chips are grouped: close, a grudge, no read.</summary>
        public static readonly IReadOnlyList<string> LeanOrder = new[] { FinalistRead.Support, FinalistRead.Bitter, FinalistRead.Uncertain };

        /// <summary>
        /// The names the page calls the two finalists by: their first names, as the house says them
        /// (<see cref="FinalistRead.FirstName"/>), or both whole where the two share a first name, so
        /// nothing on the page that names one can be read as naming the other.
        /// </summary>
        public static (string take, string cut) Names(string takeName, string cutName)
        {
            string take = FinalistRead.FirstName(takeName), cut = FinalistRead.FirstName(cutName);
            return string.Equals(take, cut, StringComparison.OrdinalIgnoreCase) ? (takeName ?? string.Empty, cutName ?? string.Empty) : (take, cut);
        }

        /// <summary>
        /// The control that takes <paramref name="takeName"/> to the Final 2 and so evicts
        /// <paramref name="cutName"/>: "Take Maya · evict Taylor", by the names the page calls them
        /// (<see cref="Names"/>), so the two controls on the page never read alike.
        /// </summary>
        public static string Caption(string takeName, string cutName)
        {
            var (take, cut) = Names(takeName, cutName);
            return TakeWord + take + EvictWord + cut;
        }

        /// <summary>The paired choice's caption, for a final eviction not between two finalists: a shape the engine never produces.</summary>
        public static string FallbackCaption(string name) => "Evict " + name;

        /// <summary>
        /// The line over both cards when the player's Final 2 agreements with somebody on the jury end
        /// broken whichever finalist is taken (<see cref="FinalistRead.BrokenEitherWay"/>'s names), said
        /// once rather than as a breach over either control; null when there are none.
        /// </summary>
        public static string EitherWay(IReadOnlyList<string> names) =>
            names == null || names.Count == 0 ? null
                : "Either way, your Final 2 " + (names.Count == 1 ? "agreement" : "agreements") + " with "
                  + string.Join(" and ", names) + " (on the jury) will end broken.";

        /// <summary>
        /// The caption of the control that evicts <paramref name="evictedId"/> at the final eviction:
        /// the other finalist taken. What a walk or a test presses for the command it means to commit.
        /// Null for anybody who is not one of the finalists the player is choosing between.
        /// </summary>
        public static string CaptionToEvict(EpisodeState state, string evictedId)
        {
            if (state == null || string.IsNullOrEmpty(evictedId)) return null;
            var others = FinalistRead.Others(state);
            var cut = others.FirstOrDefault(actor => actor.id == evictedId);
            if (cut == null) return null;
            if (others.Count != 2) return FallbackCaption(cut.name);
            return Caption(others.First(actor => actor.id != evictedId).name, cut.name);
        }

        /// <summary>
        /// The names the jurors' chips carry, one per name given and in its order: the first name, as
        /// the house says it, or the whole name where another name in the list shares that first
        /// name, so no two chips read alike.
        /// </summary>
        public static List<string> ChipNames(IReadOnlyList<string> names)
        {
            var result = new List<string>();
            if (names == null) return result;
            var firsts = names.Select(name => FinalistRead.FirstName(name)).ToList();
            for (int i = 0; i < names.Count; i++)
            {
                string first = firsts[i];
                bool shared = firsts.Where((other, j) => j != i && string.Equals(other, first, StringComparison.OrdinalIgnoreCase)).Any();
                result.Add(shared || string.IsNullOrEmpty(first) ? names[i] ?? string.Empty : first);
            }
            return result;
        }

        /// <summary>A lean's place in <see cref="LeanOrder"/>; anything unknown reads as no read.</summary>
        public static int LeanRank(string lean)
        {
            for (int i = 0; i < LeanOrder.Count; i++)
                if (LeanOrder[i] == lean) return i;
            return LeanOrder.Count - 1;
        }

        /// <summary>The word over a group of chips.</summary>
        public static string GroupWord(string lean) =>
            lean == FinalistRead.Support ? CloseWord : lean == FinalistRead.Bitter ? GrudgeWord : NoReadWord;

        /// <summary>
        /// The jurors' leans as the chips stand: close first, then a grudge, then no read, each group
        /// in the order given (the cast's, as <see cref="FinalistRead.Jurors"/> lists them).
        /// </summary>
        public static List<FinalistRead.JurorLean> Grouped(IEnumerable<FinalistRead.JurorLean> leans) =>
            leans == null ? new List<FinalistRead.JurorLean>()
                : leans.Where(lean => lean != null).Select((lean, index) => (lean, index))
                    .OrderBy(entry => LeanRank(entry.lean.lean)).ThenBy(entry => entry.index).Select(entry => entry.lean).ToList();

        /// <summary>
        /// Whether a juror's chip is filled: a lean the player saw or was part of (Confirmed). One
        /// they only heard (Suspected) is outlined, and so is no read at all.
        /// </summary>
        public static bool Filled(string lean, string certainty) =>
            lean != FinalistRead.Uncertain && certainty == FinalistRead.Confirmed;

        /// <summary>
        /// The chips' eyebrow, with the jury's size: "JURY READ (3 JURORS)"; with nobody on the jury,
        /// "JURY READ" alone over the line that says so, never a count of none.
        /// </summary>
        public static string JuryEyebrow(int jurors) =>
            jurors <= 0 ? "JURY READ" : "JURY READ (" + jurors + (jurors == 1 ? " JUROR)" : " JURORS)");

        /// <summary>
        /// The bullets' eyebrow over what taking <paramref name="takeName"/> means: "IF YOU TAKE MAYA",
        /// by the name the page calls them (<see cref="Names"/>) - whole where the two share a first name.
        /// </summary>
        public static string BulletsEyebrow(string takeName, string cutName) => "IF YOU TAKE " + Names(takeName, cutName).take.ToUpperInvariant();
    }
}
