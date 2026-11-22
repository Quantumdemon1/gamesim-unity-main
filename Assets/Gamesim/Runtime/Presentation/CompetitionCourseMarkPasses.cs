using System;
using System.Collections.Generic;
using System.Text;

namespace Gamesim.Presentation
{
    /// <summary>
    /// How hard the arena looks for a course runner's turn mark (competition show edges, after the
    /// built house's second run, where the player's own houseguest still ran no leg). The arena's
    /// checks of a mark come in two kinds: the yard's plan - on the yard's walking map, a leg clear of
    /// every station and every other leg, spaced from every place, a reservation on the deck clear of
    /// every other reservation and of the scenery - and the bodies standing in the yard as the marks
    /// are chosen. The second kind is read before anybody has walked out: the field and the audience
    /// still stand wherever the house left them, and by the time a runner sets off each of them stands
    /// at the place the plan already keeps clear.
    ///
    /// <para>So the marks are looked for in passes. <see cref="Pass.Strict"/> first, with every check
    /// as it always was, so every mark found before is found again, in the same place. A runner with
    /// none is looked for again with the bodies on their way to a stage place set aside
    /// (<see cref="Pass.MoversAside"/>). The player's own houseguest, who always runs a leg, is looked
    /// for once more on the walking map alone (<see cref="Pass.OnTheWalkingMap"/>): a point the walking
    /// map gives at the runner's width already stands clear of every collision proxy the map was baked
    /// from. The yard's plan is never relaxed. Pure presentation: no Unity type, no season state, no
    /// random draw.</para>
    /// </summary>
    public static class CompetitionCourseMarkPasses
    {
        /// <summary>One look for a mark, and what it checks.</summary>
        public enum Pass
        {
            /// <summary>Every check, every body standing where it stands now: the course's marks as they always were.</summary>
            Strict = 0,
            /// <summary>Every check, but a body on its way to a stage place does not count where it stands now.</summary>
            MoversAside = 1,
            /// <summary>The yard's plan and the walking map only: no body and no collider is read (the player's own houseguest only).</summary>
            OnTheWalkingMap = 2,
        }

        /// <summary>Why a mark was turned down, or <see cref="None"/> when it was taken.</summary>
        public enum Refusal { None = 0, Floor = 1, Legs = 2, Spacing = 3, Body = 4, Footprint = 5 }

        private static readonly Pass[] RunnerPasses = { Pass.Strict, Pass.MoversAside };
        private static readonly Pass[] MustRunPasses = { Pass.Strict, Pass.MoversAside, Pass.OnTheWalkingMap };

        /// <summary>The passes for a runner, in order: the player's own houseguest, who must run, gets one more.</summary>
        public static IReadOnlyList<Pass> For(bool mustRun) => mustRun ? MustRunPasses : RunnerPasses;

        /// <summary>Whether a body on its way to a stage place counts where it stands now.</summary>
        public static bool CountsMovers(Pass pass) => pass == Pass.Strict;

        /// <summary>Whether the bodies and the colliders standing in the yard are read at all.</summary>
        public static bool ReadsBodies(Pass pass) => pass != Pass.OnTheWalkingMap;

        /// <summary>The arena's check of one offered mark in one pass: why it is turned down, or the floor's own point it would take.</summary>
        public delegate Refusal Check(CompetitionCourseLegs.Spot offered, Pass pass, out CompetitionCourseLegs.Spot taken);

        /// <summary>How many marks each pass tried and why each was turned down, for the log.</summary>
        public sealed class Tally
        {
            private readonly int[,] counts = new int[3, 6];
            private readonly bool[] looked = new bool[3];

            /// <summary>How many marks <paramref name="pass"/> was offered.</summary>
            public int Tried(Pass pass) => counts[(int)pass, 0];

            /// <summary>How many marks <paramref name="pass"/> turned down for <paramref name="why"/>.</summary>
            public int Refused(Pass pass, Refusal why) => why == Refusal.None ? 0 : counts[(int)pass, (int)why];

            /// <summary>Whether <paramref name="pass"/> was looked at all.</summary>
            public bool Looked(Pass pass) => looked[(int)pass];

            internal void Begin(Pass pass) => looked[(int)pass] = true;

            internal void Count(Pass pass, Refusal why)
            {
                counts[(int)pass, 0]++;
                if (why != Refusal.None) counts[(int)pass, (int)why]++;
            }

            public override string ToString()
            {
                var text = new StringBuilder();
                foreach (Pass pass in MustRunPasses)
                {
                    if (!Looked(pass)) continue;
                    if (text.Length > 0) text.Append("; ");
                    text.Append(pass).Append(": tried ").Append(Tried(pass))
                        .Append(", floor ").Append(Refused(pass, Refusal.Floor))
                        .Append(", legs ").Append(Refused(pass, Refusal.Legs))
                        .Append(", spacing ").Append(Refused(pass, Refusal.Spacing))
                        .Append(", body ").Append(Refused(pass, Refusal.Body))
                        .Append(", footprint ").Append(Refused(pass, Refusal.Footprint));
                }
                return text.ToString();
            }
        }

        /// <summary>
        /// Looks for <paramref name="id"/>'s turn mark pass by pass (<see cref="For"/>): each pass offers
        /// <paramref name="candidates"/> afresh and <paramref name="check"/> reads each one; the first
        /// taken is accepted on <paramref name="legs"/> and ends the look. Returns whether a mark was
        /// taken, the floor's point in <paramref name="taken"/> and its pass in <paramref name="takenIn"/>.
        /// </summary>
        public static bool TryChoose(CompetitionCourseLegs legs, string id, bool mustRun,
            Func<IEnumerable<CompetitionCourseLegs.Spot>> candidates, Check check, Tally tally,
            out CompetitionCourseLegs.Spot taken, out Pass takenIn)
        {
            taken = default; takenIn = Pass.Strict;
            if (legs == null || id == null || candidates == null || check == null) return false;
            if (tally == null) tally = new Tally();
            foreach (var pass in For(mustRun))
            {
                tally.Begin(pass);
                foreach (var offered in candidates())
                {
                    var why = check(offered, pass, out var point);
                    tally.Count(pass, why);
                    if (why != Refusal.None) continue;
                    legs.Accept(id, point);
                    taken = point; takenIn = pass;
                    return true;
                }
            }
            return false;
        }
    }
}
