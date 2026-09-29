using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;

namespace Gamesim.Episode
{
    /// <summary>
    /// The player on the jury (ENDGAME-PLAN F6, mockup 30): the vote as a screen of its own. The
    /// two finalists' cases as the player can read them (<see cref="FinalistRead.JurorCase"/>),
    /// then the two finalists as ballot cards, each card the control under the caption it has
    /// always had, <c>Vote for {name} to win</c>; that the vote is private; what matters to the
    /// player (their diary persona, read); and the final speeches to read again.
    ///
    /// <para>The ballot is for a juror only, as the engine counts one (Jury, or Evicted on an
    /// older save). A player production removed takes no seat on the jury: the engine takes no
    /// vote from them, and the ballot it used to draw for them could not be pressed and left no
    /// way on. They watch, and continue.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        public const string ReviewSpeechesCaption = "Review final speeches";
        public const string JuryPrivacyLine = "Your vote is private. It will be revealed at the finale.";
        public const string RemovedAtTheVoteLine = "Production removed you from the house: you have no vote. The jury decides without you.";

        /// <summary>Whether the final speeches are open under the ballot. View state.</summary>
        private bool reviewingSpeeches;

        /// <summary>Whether the player casts a jury vote now: a juror at the Jury phase who has not voted.</summary>
        public static bool JurorVotes(EpisodeState state) =>
            state != null && state.phase == EpisodePhase.Jury && EpisodeHud.IsJuror(state.Find(state.playerId))
            && !state.votes.Any(vote => vote.voterId == state.playerId);

        private void JurorBallot(EpisodeState state)
        {
            hud.ScreenHead("THE JURY VOTES", "WHO DESERVES TO WIN?", JuryPrivacyLine);
            hud.CertaintyLegend();
            var finalists = state.Active.ToList();
            hud.FinalistColumns(finalists.Select(actor => new EpisodeHud.FinalistColumn
            {
                Actor = actor, Read = FinalistRead.JurorCase(state, actor.id),
            }).ToList());
            hud.BallotCards(state, finalists.Select(actor => actor.id).ToList(), null,
                id => "Vote for " + state.Find(id).name + " to win",
                id => Commit(state, EpisodeCommandKind.CastVote, id),
                "YOUR VOTE", "Choose who deserves to win.", "trophy");
            hud.BeginSideCard(EpisodeHud.JurorMattersName, "WHAT MATTERS TO YOU");
            string persona = state.playerPersona?.current;
            hud.CardLine("Your diary persona: " + (string.IsNullOrEmpty(persona) ? "Neutral" : persona) + ".", 14, UiTheme.Paper);
            int reflections = state.playerPersona?.history?.Count ?? 0;
            hud.CardLine(reflections + (reflections == 1 ? " reflection recorded" : " reflections recorded") + " in the Diary Room.", 12, UiTheme.Muted);
            hud.EndSideCard();
            hud.Disclosure(ReviewSpeechesCaption, reviewingSpeeches, () => { reviewingSpeeches = !reviewingSpeeches; Render(); });
            if (!reviewingSpeeches) return;
            if (state.finalSpeeches == null || state.finalSpeeches.Count == 0)
            {
                hud.Footnote("No final speeches were recorded this season.");
                return;
            }
            foreach (var speech in state.finalSpeeches)
            {
                hud.CardLine(state.Find(speech.speakerId)?.name ?? "A finalist", 16, UiTheme.Paper, UiTheme.Weight.SemiBold);
                hud.CardLine(string.IsNullOrEmpty(speech.text) ? "No final speech was given." : speech.text, 14, UiTheme.Muted);
            }
        }
    }
}
