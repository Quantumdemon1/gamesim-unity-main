using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>
    /// The other competitors let go around the player in the ranked endurance game (competition drop-outs).
    /// While the player holds on, the housemates at the grip rigs let go one by one - a shrug, hands off the
    /// grip, the rig reading out - lowest first, each only once the effort the player has banked puts them
    /// above that housemate whenever they let go; when the player's attempt ends, everybody the result ranks
    /// under them lets go with them. What the yard shows is read from a preview of the very command the
    /// attempt will commit, run by a separate engine on a copy of the season (<see cref="CompetitionHoldDropouts"/>),
    /// so it agrees with the standings the card then shows, and the season's own state and generator are
    /// never touched: the commit is exactly what it was.
    ///
    /// <para>Not in practice (it would tell the real result), not in a staged show (whose drops are the
    /// committed standings already), and nobody lets go mid-hold in the final Head of Household's first
    /// part, whose result moves with the player's effort: there only the let-go at the end is shown.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>The ranked endurance attempt's drop-outs; null when none are shown.</summary>
        private CompetitionHoldDropouts competitionDropouts;

        private readonly HashSet<string> competitionGameDropped = new HashSet<string>();

        /// <summary>Who has let go so far in the ranked endurance game being played, in order; empty when none is.</summary>
        public IReadOnlyList<string> CompetitionGameDrops => competitionDropouts != null ? competitionDropouts.Dropped.ToArray() : Array.Empty<string>();

        /// <summary>Who let go in the last ranked endurance game, in order, with the player's own let-go marked; null before the first.</summary>
        public CompetitionGameDropRecord LastCompetitionGameDrops { get; private set; }

        /// <summary>What one ranked endurance game showed of its field letting go, for tests to read.</summary>
        public sealed class CompetitionGameDropRecord
        {
            /// <summary>Who let go while the player still held on, in order.</summary>
            public List<string> BeforeYou { get; } = new List<string>();
            /// <summary>Who let go with the player, at the end of their attempt, in order.</summary>
            public List<string> WithYou { get; } = new List<string>();
            /// <summary>Whether housemates could let go while the player held on.</summary>
            public bool Live { get; internal set; }
        }

        /// <summary>Whether this housemate has let go around the player in their own ranked game.</summary>
        private bool CompetitionGameDropped(string id) => competitionShow == null && id != null && competitionGameDropped.Contains(id);

        /// <summary>
        /// At a ranked attempt's start: the preview, for an endurance game the player is in. Any failure
        /// leaves the field holding on to the end, as before.
        /// </summary>
        private void BeginCompetitionDropouts(EpisodeState origin, bool practice)
        {
            competitionDropouts = null;
            competitionGameDropped.Clear();
            if (practice || origin == null || challengeRun == null || challengeRun.Kind != CompetitionMiniGames.Kind.Endurance) return;
            try { competitionDropouts = CompetitionHoldDropouts.Preview(origin, challengeCommandId); }
            catch (Exception error) { competitionDropouts = null; Debug.Log("[Gamesim competition drop-outs] no preview: " + error.Message, this); }
            LastCompetitionGameDrops = competitionDropouts != null ? new CompetitionGameDropRecord { Live = competitionDropouts.Live } : null;
        }

        /// <summary>Each live frame of the ranked endurance game: who lets go now, from the effort banked so far.</summary>
        private void TickCompetitionDropouts()
        {
            if (competitionDropouts == null || challengeRun == null || challengePractice || competitionScreen == null || !competitionScreen.IsPlaying) return;
            double performance = CompetitionMiniGames.Performance(CompetitionMiniGames.EnduranceScore(challengeRun.Held, challengeRun.TimeLimit, challengeRun.RulesVersion));
            foreach (var id in competitionDropouts.Due(performance, challengeRun.Elapsed))
            {
                LastCompetitionGameDrops?.BeforeYou.Add(id);
                DropCompetitionGameEntrant(id);
            }
        }

        /// <summary>The player's attempt is over: everybody the result ranks under them lets go with them.</summary>
        private void EndCompetitionDropouts()
        {
            if (competitionDropouts == null || challengeRun == null || !challengeRun.Finished || challengePractice) return;
            foreach (var id in competitionDropouts.LetGo(challengeRun.Performance))
            {
                LastCompetitionGameDrops?.WithYou.Add(id);
                DropCompetitionGameEntrant(id);
            }
        }

        /// <summary>A line for the side column while anybody has let go: who last did, and how many still hold on.</summary>
        private string CompetitionDropoutsLine()
        {
            if (competitionDropouts == null || competitionDropouts.Dropped.Count == 0) return null;
            string last = competitionDropouts.Dropped[competitionDropouts.Dropped.Count - 1];
            string name = projected?.Find(last)?.name ?? challengeOrigin?.Find(last)?.name;
            int holding = competitionDropouts.StillHolding;
            return (name != null ? name + " let go. " : "")
                + (holding == 0 ? "Nobody else is holding on." : holding + (holding == 1 ? " other is" : " others are") + " still holding on.");
        }

        /// <summary>One housemate lets go: hands off the grip, the rig reads out, a shrug; then they stand easy by their rig.</summary>
        private void DropCompetitionGameEntrant(string id)
        {
            if (id == null || !competitionGameDropped.Add(id)) return;
            if (competitionInstruments.TryGetValue(id, out var instrument) && instrument != null) instrument.ShowHold(0f, false);
            CharacterPresentation visual = null;
            if (competitionBodies.TryGetValue(id, out var body))
            {
                if (body.contact != null) body.contact.Release();
                visual = body.visual;
            }
            if (visual == null) return;
            ReleaseCompetitionStance(visual);
            if (!visual.MakeGesture(CharacterPresentation.Gesture.Shrug)) visual.React(CharacterPresentation.Reaction.Shocked);
        }
    }
}
