using System.Linq;

namespace Gamesim.Simulation
{
    public sealed partial class EpisodeEngine
    {
        private static int JuryExchangeCount(EpisodeState s) => s.Active.Any(x => x.isPlayer)
            ? s.contestants.Count(x => x.status == ContestantStatus.Jury || x.status == ContestantStatus.Evicted) : 2;

        private static void PrepareJuryQuestion(EpisodeState s)
        {
            var entry = new JuryExchangeState();
            if (s.Active.Any(x => x.isPlayer))
            {
                // Stable native cast order replaces the web's separate juryMembers order.
                var juror = s.contestants.Where(x => x.status == ContestantStatus.Jury || x.status == ContestantStatus.Evicted).ElementAt(s.juryQuestionIndex);
                var question = WebJuryQuestioning.CreateFinalistQuestion(juror.traits, s.juryQuestionIndex, () => Roll(s));
                entry.questionerId = juror.id; entry.finalistId = s.playerId;
                entry.tone = question.tone; entry.question = question.question;
                entry.optionA = question.optionA; entry.optionB = question.optionB;
                entry.correctChoice = question.correctIs; entry.opponentAnswer = question.opponentAnswer;
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
            if (entry.finalistId == s.playerId)
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

        private static void BeginFinalSpeeches(EpisodeState s)
        {
            Phase(s, EpisodePhase.FinalSpeeches);
            foreach (var finalist in s.Active.Where(x => !x.isPlayer))
                s.finalSpeeches.Add(new FinalSpeechState { speakerId = finalist.id,
                    text = WebFinalSpeeches.GenerateNative(s, finalist.id, () => Roll(s)), isPlayerAuthored = false });
        }
    }
}
