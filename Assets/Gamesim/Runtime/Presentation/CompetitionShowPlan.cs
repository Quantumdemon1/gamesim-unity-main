using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The plan of a competition's staged show in the yard (competition show, slice 1): who stands
    /// at a station, who lets go in which order, who is left holding on and cheers, and when each
    /// beat falls. Read from the standings a commit has already written, best first, in the order
    /// the engine and the standings card sort them; nothing is decided here a second time.
    ///
    /// <para>Pure presentation over committed state. It reads <see cref="EpisodeState.competitionScores"/>
    /// and never writes the state, draws from the season's generator or keeps anything that is
    /// saved: a season plays and replays byte for byte whether or not its competitions are shown.
    /// It names no Unity type, so the Unity-free suite runs it as the editor does.</para>
    ///
    /// <para>Slice 1 shows the endurance competitions only - a weekly one of that kind and the final
    /// Head of Household's first part, which is always one. The other kinds keep their card at once.</para>
    /// </summary>
    public sealed class CompetitionShowPlan
    {
        /// <summary>The one kind slice 1 stages.</summary>
        public const string EnduranceKind = "Endurance";

        /// <summary>Whether a competition of this kind is staged as a show.</summary>
        public static bool Shows(string category) => category == EnduranceKind;

        private CompetitionShowPlan(EpisodePhase phase, string category, string playerId, List<string> standings)
        {
            Phase = phase;
            Category = category;
            PlayerId = playerId;
            Standings = standings;
            var drops = new List<string>(standings);
            drops.RemoveAt(0);
            drops.Reverse();
            DropOrder = drops;
        }

        /// <summary>The phase the competition was played in (the committed state may have moved on: Part 1 to Part 2).</summary>
        public EpisodePhase Phase { get; }

        /// <summary>The competition's kind, as the standings card names it.</summary>
        public string Category { get; }

        /// <summary>The player's id, in the field or watching.</summary>
        public string PlayerId { get; }

        /// <summary>The committed field, best first: the standings card's order.</summary>
        public IReadOnlyList<string> Standings { get; }

        /// <summary>Who lets go, first out first: the standings from the bottom, never the winner.</summary>
        public IReadOnlyList<string> DropOrder { get; }

        /// <summary>The last one holding on: the standings' top line.</summary>
        public string WinnerId => Standings[0];

        /// <summary>Whether the player competed, and so takes a station like everybody else.</summary>
        public bool PlayerInField => PlayerId != null && Standings.Contains(PlayerId);

        /// <summary>
        /// The show for a competition just committed into <paramref name="committed"/>, played in
        /// <paramref name="phase"/> as <paramref name="category"/>; null when this kind is not staged
        /// or the standings have fewer than two people in them. The order is the engine's: highest
        /// score first, ties in the order the scores were written (a stable sort, as the card's).
        /// </summary>
        public static CompetitionShowPlan For(EpisodeState committed, EpisodePhase phase, string category)
        {
            if (committed?.competitionScores == null || !Shows(category)) return null;
            var standings = new List<string>();
            foreach (var entry in committed.competitionScores.OrderByDescending(entry => entry.score))
                if (entry != null && entry.contestantId != null && committed.Find(entry.contestantId) != null
                    && !standings.Contains(entry.contestantId)) standings.Add(entry.contestantId);
            if (standings.Count < 2) return null;
            return new CompetitionShowPlan(phase, category, committed.playerId, standings);
        }

        // ------------------------------------------------------------ the beats, in unscaled seconds

        /// <summary>
        /// How long the field is given to walk out to its stations. Once it is up the hold starts with
        /// whoever stands at a station, provided the winner (and the player, when they have a station)
        /// is there; anybody still walking is left out of the staging and goes to the side.
        /// </summary>
        public static float AssemblySeconds(bool quick) => quick ? 6f : 9f;

        /// <summary>
        /// How long the show waits, from its start, for the winner and a stationed player to reach their
        /// stations; past it the show gives way to the card, so nobody cheers or lets go away from their rig.
        /// </summary>
        public static float PrincipalSeconds(bool quick) => quick ? 7.5f : 11f;

        /// <summary>Everybody holding on before the first one lets go.</summary>
        public static float OpeningHoldSeconds(bool quick) => quick ? .75f : 1.5f;

        /// <summary>The seconds between one drop and the next: the drops share a window of 1.1 s each, 3 to 6 s in all, half that when quick.</summary>
        public static float DropSpacing(int drops, bool quick)
        {
            if (drops <= 0) return 0f;
            float window = drops * 1.1f;
            if (window < 3f) window = 3f;
            if (window > 6f) window = 6f;
            if (quick) window *= .5f;
            return window / drops;
        }

        /// <summary>
        /// The drop order among those holding on when the hold starts: the standings from the bottom,
        /// as <see cref="DropOrder"/>, less anybody who did not reach a station in time.
        /// </summary>
        public IReadOnlyList<string> DropOrderAmong(ICollection<string> holding)
            => holding == null ? DropOrder : DropOrder.Where(holding.Contains).ToList();

        /// <summary>The cheer at the winner's station, before the standings card.</summary>
        public static float CheerSeconds(bool quick) => quick ? 1.5f : 2.5f;

        /// <summary>How long a dropped houseguest stands at their station after letting go, before walking to the side.</summary>
        public static float LetGoSeconds(bool quick) => quick ? .6f : 1.1f;

        /// <summary>Under reduced motion there are no drops and no cheer: the field holds a still pose this long, and the card follows.</summary>
        public const float ReducedHoldSeconds = 1.5f;

        /// <summary>When, from the start of the hold, the <paramref name="index"/>th of <paramref name="drops"/> drops falls.</summary>
        public static float DropAt(int index, int drops, bool quick) => OpeningHoldSeconds(quick) + DropSpacing(drops, quick) * index;

        /// <summary>When, from the start of the hold, the winner is the last one on after <paramref name="drops"/> drops and cheers.</summary>
        public static float CheerAt(int drops, bool quick) => OpeningHoldSeconds(quick) + DropSpacing(drops, quick) * drops;

        /// <summary>When, from the start of the hold, the standings card plays after <paramref name="drops"/> drops.</summary>
        public static float CardAt(int drops, bool quick, bool reduced) => reduced ? ReducedHoldSeconds : CheerAt(drops, quick) + CheerSeconds(quick);

        /// <summary>When, from the start of the hold, the <paramref name="index"/>th drop falls, the whole field holding on.</summary>
        public float DropAt(int index, bool quick) => DropAt(index, DropOrder.Count, quick);

        /// <summary>When, from the start of the hold, the winner is the last one on and cheers, the whole field holding on.</summary>
        public float CheerAt(bool quick) => CheerAt(DropOrder.Count, quick);

        /// <summary>When, from the start of the hold, the standings card plays, the whole field holding on.</summary>
        public float CardAt(bool quick, bool reduced) => CardAt(DropOrder.Count, quick, reduced);

        /// <summary>
        /// The longest a show may run from its start, the walk out included, before it gives way to the
        /// card regardless: the wait for the winner, the whole field's hold, and a second to spare.
        /// </summary>
        public float HardSeconds(bool quick, bool reduced) => PrincipalSeconds(quick) + CardAt(quick, reduced) + 1f;

        /// <summary>
        /// How hard <paramref name="id"/> is working at <paramref name="t"/> seconds into the hold, 0
        /// to 1: each houseguest's effort climbs to its peak at the moment they let go, and the
        /// winner's to most of it at the end. Presentation only: the body's shake and the grip meter.
        /// </summary>
        public float Strain(string id, float t, bool quick) => Strain(DropOrder, id, t, quick);

        /// <summary>The same, when only <paramref name="dropOrder"/> holds on (<see cref="DropOrderAmong"/>).</summary>
        public float Strain(IReadOnlyList<string> dropOrder, string id, float t, bool quick)
        {
            if (t <= 0f) return 0f;
            if (dropOrder == null) dropOrder = DropOrder;
            int index = -1;
            for (int i = 0; i < dropOrder.Count; i++) if (dropOrder[i] == id) { index = i; break; }
            float peak = index >= 0 ? DropAt(index, dropOrder.Count, quick) : CheerAt(dropOrder.Count, quick);
            float top = index >= 0 ? 1f : .85f;
            if (peak <= 0f) return top;
            float strain = t / peak;
            return (strain > 1f ? 1f : strain) * top;
        }
    }
}
