using System.Collections.Generic;
using Gamesim.Simulation;

namespace Gamesim.Episode
{
    /// <summary>
    /// The house talk's blocks on a houseguest's profile (plan 32 §C, "Where it shows"; S1): "Their manner" and "What you've
    /// learned" from story rules 12, each only where its evidence exists, filled from the pure reader
    /// <see cref="HouseguestNotes.HouseTalkBlocks"/>. "How they seem to see you" joins from 13 (S3), here. Below 12 the
    /// reader gives nothing and the profile draws as it always did. Called by one line in <c>RenderHouseguestProfile</c>.
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        private static void HouseTalkBlocks(EpisodeState state, ContestantState actor, EpisodeHud.ProfileView view)
        {
            if (state == null || actor == null || view == null) return;
            foreach (var block in HouseguestNotes.HouseTalkBlocks(state, actor.id))
                view.Blocks.Add((block.Heading, (IList<string>)block.Lines));
        }
    }
}
