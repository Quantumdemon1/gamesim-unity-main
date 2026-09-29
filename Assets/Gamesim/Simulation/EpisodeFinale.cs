using System;
using System.Linq;

namespace Gamesim.Simulation
{
    public sealed partial class EpisodeEngine
    {
        /// <summary>
        /// Switches the finale rules on from a week (ENDGAME-PLAN §3): history questions, the five
        /// responses, the final argument and its jury term. Seeds nothing. Seasons built directly
        /// stay off unless they enable it, so the finale's pinned fixtures keep the catalogue.
        /// </summary>
        public static void EnableFinale(EpisodeState s, int fromWeek = 1)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            s.finaleRulesStartWeek = Math.Max(1, Math.Min(fromWeek, s.week + 1));
        }

        /// <summary>Whether the finale runs under the schema 21 rules this week.</summary>
        public static bool FinaleOn(EpisodeState s) => s != null && s.finaleRulesStartWeek >= 1 && s.week >= s.finaleRulesStartWeek;

        /// <summary>
        /// How many questions the finale asks: every juror's question to a player finalist, or the
        /// spectating player's two - none at all for a player production removed, who is not a juror.
        /// </summary>
        private static int JuryExchangeCount(EpisodeState s) =>
            s.Find(s.playerId)?.status == ContestantStatus.Expelled ? 0
            : s.Active.Any(x => x.isPlayer)
                ? s.contestants.Count(x => x.status == ContestantStatus.Jury || x.status == ContestantStatus.Evicted) : 2;

        private static void PrepareJuryQuestion(EpisodeState s)
        {
            var entry = new JuryExchangeState();
            if (s.Active.Any(x => x.isPlayer))
            {
                // Stable native cast order replaces the web's separate juryMembers order.
                var juror = s.contestants.Where(x => x.status == ContestantStatus.Jury || x.status == ContestantStatus.Evicted).ElementAt(s.juryQuestionIndex);
                entry.questionerId = juror.id; entry.finalistId = s.playerId;
                // Under the finale rules, a question from the season's history: the same two draws.
                if (FinaleOn(s)) FinaleQuestions.Prepare(s, juror, s.juryQuestionIndex, entry, () => Roll(s));
                else
                {
                    var question = WebJuryQuestioning.CreateFinalistQuestion(juror.traits, s.juryQuestionIndex, () => Roll(s));
                    entry.tone = question.tone; entry.question = question.question;
                    entry.optionA = question.optionA; entry.optionB = question.optionB;
                    entry.correctChoice = question.correctIs; entry.opponentAnswer = question.opponentAnswer;
                }
            }
            else
            {
                entry.questionerId = s.playerId;
                entry.finalistId = s.Active.ElementAt(s.juryQuestionIndex).id;
            }
            s.juryExchanges.Add(entry);
        }

        private static void AnswerJury(EpisodeState s, EpisodeCommand c)
        {
            Require(s.phase == EpisodePhase.JuryQuestioning, "Jury questioning is not open.");
            var entry = s.juryExchanges[s.juryQuestionIndex];
            Require(!entry.completed, "This answer is already committed.");
            Require(c.targetId == (entry.finalistId == s.playerId ? entry.questionerId : entry.finalistId), "This is not the current jury exchange.");
            if (entry.finalistId == s.playerId && FinaleOn(s))
            {
                // One of the responses offered; the sign by fit, from what was saved; today's ±10
                // and its two draws, under a softer note.
                Require(FinaleQuestions.Offered(entry.category, entry.receiptKind).Contains(c.secondTargetId), "Choose one of the responses offered.");
                bool landed = FinaleQuestions.Lands(FinalArgument.ThemeOf(s.Find(entry.questionerId)), entry.category, c.secondTargetId);
                string note = FinaleQuestions.Note(Name(s, entry.questionerId), landed);
                entry.answer = FinaleQuestions.Line(entry.category, c.secondTargetId);
                Change(s, entry.questionerId, entry.finalistId, landed ? 10 : -10, note, "general");
                Log(s, "jury-answer", note);
            }
            else if (entry.finalistId == s.playerId)
            {
                Require(c.secondTargetId == "A" || c.secondTargetId == "B", "Choose answer A or B.");
                var impact = WebJuryQuestioning.EvaluateChoice(new WebJuryQuestion { correctIs = entry.correctChoice },
                    c.secondTargetId, entry.questionerId, Name(s, entry.questionerId), entry.finalistId);
                entry.answer = c.secondTargetId == "A" ? entry.optionA : entry.optionB;
                Change(s, impact.guestId1, impact.guestId2, impact.change, impact.note, impact.eventType);
                Log(s, "jury-answer", impact.note);
            }
            else
            {
                var option = WebJuryQuestioning.GetJurorQuestionOptions(s.juryQuestionIndex).FirstOrDefault(x => x.tone == c.secondTargetId);
                Require(option != null, "Choose a neutral, bitter or supportive question.");
                entry.tone = option.tone; entry.question = option.text;
                entry.answer = WebJuryQuestioning.CreateFallbackAnswer(() => Roll(s)).answer;
                // Source player-juror offline branch has no trust/stat consequence.
                Log(s, "jury-answer", Name(s, entry.finalistId) + " answered your jury question.");
            }
            entry.answerChoice = c.secondTargetId; entry.completed = true;
        }

        /// <summary>
        /// The lock (ENDGAME-PLAN F4b): once, for a player finalist under the finale rules, from the
        /// final eviction's commit until their speech is in. One of the five themes and three of the
        /// player's signature moments, or every one a quiet season has. It spends no draw and moves
        /// no score: the argument counts at the vote.
        /// </summary>
        private static void LockFinalArgument(EpisodeState s, EpisodeCommand c)
        {
            Require(FinaleOn(s), "The final argument is not part of this season's rules.");
            Require(s.phase == EpisodePhase.JuryQuestioning || s.phase == EpisodePhase.FinalSpeeches, "The final argument locks between the final eviction and your speech.");
            Require(s.Find(s.playerId)?.status == ContestantStatus.Active, "Only a finalist locks a final argument.");
            Require(s.finalArgument == null, "Your final argument is already locked.");
            Require(!s.finalSpeeches.Any(x => x.speakerId == s.playerId), "Your final speech is already committed.");
            Require(FinalArgument.Themes.Contains(c.secondTargetId), "Choose one of the five themes.");
            var references = FinalArgument.ParseReferences(c.text);
            var moments = FinalArgument.Moments(s).Select(m => m.reference).ToList();
            Require(references.Count == Math.Min(FinalArgument.MomentCount, moments.Count) && references.Distinct().Count() == references.Count
                && references.All(moments.Contains), "Choose three of your signature moments.");
            s.finalArgument = new FinalArgumentState { theme = c.secondTargetId, momentRefs = references };
            Log(s, "final-argument", "You locked your final argument: " + FinalArgument.Label(c.secondTargetId) + ".", s.playerId);
        }

        private static void BeginFinalSpeeches(EpisodeState s)
        {
            Phase(s, EpisodePhase.FinalSpeeches);
            foreach (var finalist in s.Active.Where(x => !x.isPlayer))
                s.finalSpeeches.Add(new FinalSpeechState { speakerId = finalist.id,
                    text = WebFinalSpeeches.GenerateNative(s, finalist.id, () => Roll(s)), isPlayerAuthored = false });
        }
    }
}
