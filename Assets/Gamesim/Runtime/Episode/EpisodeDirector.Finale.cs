using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;

namespace Gamesim.Episode
{
    /// <summary>
    /// The finale on screen: the jury's vote read one juror at a time (<see cref="JuryReveal"/>),
    /// and, once the season is over, two ways back into that night from the finale's page: the
    /// vote read again, and what each juror asked and gave as their reason (MOCKUP-PASS-PLAN M4;
    /// the page itself is EpisodeDirector.FinalePage.cs).
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>The finale panel's control that reads the jury's vote again.</summary>
        public const string WatchFinaleReplayCaption = "Watch finale replay";

        /// <summary>The finale panel's disclosure of the jury's questions, the answers and the reasons.</summary>
        public const string JuryQuestionsCaption = "The jury's questions";

        /// <summary>The names the disclosure's lines carry, so a test can tell them from the panel's own.</summary>
        public const string JuryQuestionsJurorName = "Jury's questions juror", JuryQuestionsQuestionName = "Jury's questions question",
            JuryQuestionsAnswerName = "Jury's questions answer", JuryQuestionsReasonName = "Jury's questions reason";

        private JuryReveal juryReveal;

        /// <summary>Whether the jury's questions are open under the finale's panel. View state.</summary>
        private bool juryQuestionsOpen;

        /// <summary>Whether the jury's vote is being read.</summary>
        private bool JuryRevealPlaying => juryReveal != null && juryReveal.IsPlaying;

        /// <summary>
        /// Whether the season's jury vote can be read again: two finalists, a winner who is one of
        /// them, and at least one ballot for one of them from somebody else - the shape
        /// <see cref="JuryReveal.Play"/> accepts. A season without it offers no replay rather than
        /// a control that does nothing.
        /// </summary>
        private static bool CanReplayJuryReveal(EpisodeState state)
        {
            if (state?.contestants == null || state.votes == null) return false;
            var finalists = new HashSet<string>(state.contestants
                .Where(c => c.status == ContestantStatus.Winner || c.status == ContestantStatus.RunnerUp).Select(c => c.id));
            return finalists.Count == 2 && finalists.Contains(state.winnerId)
                && state.votes.Any(v => finalists.Contains(v.targetId) && !finalists.Contains(v.voterId) && state.Find(v.voterId) != null);
        }

        /// <summary>
        /// Reads the jury's vote again, on the finale's panel once the season is over: the same
        /// card, the same deal and the same count as on the night, from the committed ballots. The
        /// chrome steps aside as it did then, and <see cref="TickCeremonies"/> brings it back, panel
        /// and all, when the card ends or is skipped. It reads the season and writes nothing.
        /// </summary>
        public void ReplayJuryReveal()
        {
            var state = Snapshot;
            if (state == null || state.phase != EpisodePhase.Finished || juryReveal == null || JuryRevealPlaying) return;
            EndCeremonyCards();
            if (juryReveal.Play(JuryFinalists(state), JuryVotes(state), state.winnerId,
                    reducedMotion, ceremonyPace, unchecked((int)state.seed)))
                HoldHudForReveal();
        }

        /// <summary>
        /// What each juror asked at the finale, both finalists' answers, and the reason each gave
        /// with their vote, from the record: the questions and answers as <c>juryExchanges</c> saved
        /// them, and the reasons as the ballots carry them, in full. The truthful stand-in for
        /// mockup 53's jury roundtable - what the jurors said among themselves was never recorded,
        /// so it is not here. Nor is anything a question was scored by: never the catalogue's
        /// answer key, the answer the player passed over, or whether an answer landed. Opened as the
        /// finale page's step by "The jury's questions" (EpisodeDirector.FinalePage.cs).
        /// </summary>
        private void JuryQuestionsRecord(EpisodeState state)
        {
            var exchanges = (state.juryExchanges ?? new List<JuryExchangeState>())
                .Where(x => x != null && x.completed && !string.IsNullOrEmpty(x.question)).ToList();
            var ballots = SeasonReport.JuryBallots(state);
            var finalists = state.contestants
                .Where(c => c.status == ContestantStatus.Winner || c.status == ContestantStatus.RunnerUp).ToList();
            hud.Footnote(exchanges.Count == 0
                ? "No questions were put to the finalists this season. Each juror's reason is as they gave it with their vote."
                : "Each question as it was put, the finalists' answers as they were given, and each juror's reason as they gave it with their vote.");
            string Who(ContestantState c) => c == null ? "A finalist" : c.isPlayer ? "You" : c.name;
            string Quoted(string words) => "\u201C" + words + "\u201D";
            // The jury in cast order, the order the questions were asked in.
            foreach (var juror in state.contestants)
            {
                var asked = exchanges.Where(x => x.questionerId == juror.id).ToList();
                var ballot = ballots.Where(b => b.JurorId == juror.id).ToList();
                if (asked.Count == 0 && ballot.Count == 0) continue;
                hud.CardLine(Who(juror), 16, UiTheme.Paper, UiTheme.Weight.SemiBold).name = JuryQuestionsJurorName;
                foreach (var exchange in asked)
                {
                    var to = state.Find(exchange.finalistId);
                    string addressed = to == null ? "the finalist" : to.isPlayer ? "you" : to.name;
                    hud.CardLine((juror.isPlayer ? "You asked " : "Asked ") + addressed + ": " + Quoted(exchange.question),
                        14, UiTheme.Paper).name = JuryQuestionsQuestionName;
                    if (!string.IsNullOrEmpty(exchange.answer))
                        hud.CardLine(Who(to) + ": " + Quoted(exchange.answer), 14, UiTheme.Muted).name = JuryQuestionsAnswerName;
                    // The other finalist's answer to the same question, as the questioning showed it.
                    var other = finalists.FirstOrDefault(f => f.id != exchange.finalistId);
                    if (!string.IsNullOrEmpty(exchange.opponentAnswer) && other != null)
                        hud.CardLine(Who(other) + ": " + Quoted(exchange.opponentAnswer), 14, UiTheme.Muted).name = JuryQuestionsAnswerName;
                }
                foreach (var vote in ballot)
                {
                    string chosen = vote.FinalistId == state.playerId ? "you" : vote.Finalist;
                    hud.CardLine("Voted for " + chosen + (string.IsNullOrEmpty(vote.Reason) ? "." : ": " + Quoted(vote.Reason)),
                        14, UiTheme.Paper).name = JuryQuestionsReasonName;
                }
            }
        }

        /// <summary>The two finalists, in cast order, with their faces: the order the engine counts them in.</summary>
        private List<JuryReveal.Finalist> JuryFinalists(EpisodeState state) =>
            state?.contestants == null ? new List<JuryReveal.Finalist>()
                : state.contestants.Where(c => c.status == ContestantStatus.Winner || c.status == ContestantStatus.RunnerUp)
                    .Select(c => new JuryReveal.Finalist(c.id, c.name, CharacterPortraits.Get(c), c, c.isPlayer)).ToList();

        /// <summary>
        /// The jury's committed ballots, in the order they are read: dealt from the seed, the same way
        /// every time for the same season, as the keys are. A juror who is the player reads as "You".
        /// </summary>
        private List<JuryReveal.Juror> JuryVotes(EpisodeState state)
        {
            var ballots = new List<JuryReveal.Juror>();
            if (state?.votes == null) return ballots;
            var finalists = new HashSet<string>(state.contestants
                .Where(c => c.status == ContestantStatus.Winner || c.status == ContestantStatus.RunnerUp).Select(c => c.id));
            var cast = state.votes.Where(v => finalists.Contains(v.targetId) && !finalists.Contains(v.voterId))
                .GroupBy(v => v.voterId).Select(g => g.First()).ToDictionary(v => v.voterId);
            foreach (var id in JuryOrder(cast.Keys, state.seed))
            {
                var juror = state.Find(id);
                if (juror == null) continue;
                ballots.Add(new JuryReveal.Juror(juror.id, juror.isPlayer ? "You" : juror.name, cast[id].targetId,
                    CharacterPortraits.Get(juror), juror));
            }
            return ballots;
        }

        /// <summary>
        /// The order the jury is read in: shuffled from the seed and nothing else, so a reload reads it
        /// the same way. The engine records the ballots in cast order, and reading them that way would
        /// read the player's own first whenever they sat on the jury.
        /// </summary>
        public static List<string> JuryOrder(IEnumerable<string> ids, uint seed) =>
            (ids ?? Enumerable.Empty<string>())
                .Select((id, index) => (id, index, rank: JuryRank(seed, id)))
                .OrderBy(entry => entry.rank).ThenBy(entry => entry.index)
                .Select(entry => entry.id).ToList();

        private static uint JuryRank(uint seed, string id)
        {
            unchecked
            {
                uint hash = 2166136261u;
                void Mix(uint value)
                {
                    for (int shift = 0; shift < 32; shift += 8) { hash ^= (value >> shift) & 0xFF; hash *= 16777619u; }
                }
                Mix(seed); Mix(0x6A757279u); // "jury"
                foreach (char c in id ?? string.Empty) Mix(c);
                return hash;
            }
        }
    }
}
