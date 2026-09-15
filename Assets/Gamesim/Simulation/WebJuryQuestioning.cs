using System;
using System.Collections.Generic;

namespace Gamesim.Simulation
{
    [Serializable]
    public sealed class WebJuryResponsePair
    {
        public string optionA, optionB, correctIs, trait;
    }

    [Serializable]
    public sealed class WebJuryQuestion
    {
        public string tone, question, optionA, optionB, correctIs, trait, opponentAnswer;
    }

    [Serializable]
    public sealed class WebJuryQuestionOption
    {
        public string tone, text;
    }

    [Serializable]
    public sealed class WebJuryAnswer
    {
        public string answer, tone;
    }

    /// <summary>The original UPDATE_RELATIONSHIPS payload, NOT a finalized score or one-way write.</summary>
    [Serializable]
    public sealed class WebJuryChoiceImpact
    {
        public string guestId1, guestId2, note, eventType;
        public int change;
    }

    /// <summary>
    /// Pure rules/data port of JuryQuestioningPhase.tsx. Presentation stages, command authorization,
    /// relationship resolution and persistence belong to the caller. The source uses Math.random;
    /// supplying the saved SeededRandom stream is an explicit native determinism adapter.
    /// </summary>
    public static class WebJuryQuestioning
    {
        public static string GetPrimaryTrait(IReadOnlyList<string> traits)
        {
            // JavaScript hg.traits?.[0] || 'Strategic': do not trim or inspect later traits.
            return traits == null || traits.Count == 0 || string.IsNullOrEmpty(traits[0]) ? "Strategic" : traits[0];
        }

        public static WebJuryResponsePair GetResponsePair(IReadOnlyList<string> traits, int index, double flipRoll)
        {
            RequireIndex(index); RequireRoll(flipRoll);
            var trait = GetPrimaryTrait(traits);
            var pairs = WebJuryQuestioningCatalog.Pairs.TryGetValue(trait, out var found)
                ? found : WebJuryQuestioningCatalog.Pairs["Strategic"];
            var pair = pairs[index % pairs.Length];
            bool flip = flipRoll > 0.5;
            return new WebJuryResponsePair
            {
                optionA = flip ? pair[0] : pair[1], optionB = flip ? pair[1] : pair[0],
                correctIs = flip ? "A" : "B", trait = trait
            };
        }

        public static string GetQuestionType(double roll)
        {
            RequireRoll(roll);
            return roll < 0.2 ? "bitter" : roll < 0.4 ? "supportive" : "neutral";
        }

        /// <summary>Exactly two draws: question tone, then response A/B order. Persist the returned question.</summary>
        public static WebJuryQuestion CreateFinalistQuestion(IReadOnlyList<string> jurorTraits, int jurorIndex, Func<double> nextRoll)
        {
            RequireIndex(jurorIndex);
            if (nextRoll == null) throw new ArgumentNullException(nameof(nextRoll));
            string tone = GetQuestionType(nextRoll());
            var pair = GetResponsePair(jurorTraits, jurorIndex, nextRoll());
            var questions = WebJuryQuestioningCatalog.Questions[tone];
            return new WebJuryQuestion
            {
                tone = tone, question = questions[jurorIndex % questions.Length],
                optionA = pair.optionA, optionB = pair.optionB, correctIs = pair.correctIs,
                trait = pair.trait, opponentAnswer = GetOpponentAnswer(jurorIndex)
            };
        }

        public static string GetOpponentAnswer(int jurorIndex)
        {
            RequireIndex(jurorIndex);
            return WebJuryQuestioningCatalog.NpcResponses[jurorIndex % WebJuryQuestioningCatalog.NpcResponses.Length];
        }

        /// <summary>
        /// No draws or mutations. Apply this payload exactly once through relationship rules.
        /// The ordinary finalist path's actor is the NPC juror, so its direct delta is exactly +/-10.
        /// </summary>
        public static WebJuryChoiceImpact EvaluateChoice(WebJuryQuestion question, string choice,
            string jurorId, string jurorName, string finalistId)
        {
            if (question == null) throw new ArgumentNullException(nameof(question));
            RequireChoice(question.correctIs); RequireChoice(choice);
            if (string.IsNullOrEmpty(jurorId) || string.IsNullOrEmpty(finalistId) || jurorId == finalistId)
                throw new ArgumentException("Jury questioning needs distinct juror and finalist IDs.");
            if (jurorName == null) throw new ArgumentNullException(nameof(jurorName));
            bool correct = choice == question.correctIs;
            return new WebJuryChoiceImpact
            {
                guestId1 = jurorId, guestId2 = finalistId, change = correct ? 10 : -10,
                note = jurorName + (correct ? " was impressed by your response during jury questioning."
                    : " was unconvinced by your response during jury questioning."), eventType = "general"
            };
        }

        /// <summary>Player-as-juror options, in source neutral/bitter/supportive order. No randomness.</summary>
        public static WebJuryQuestionOption[] GetJurorQuestionOptions(int finalistIndex)
        {
            RequireIndex(finalistIndex);
            var tones = new[] { "neutral", "bitter", "supportive" };
            var result = new WebJuryQuestionOption[tones.Length];
            for (int offset = 0; offset < tones.Length; offset++)
            {
                var questions = WebJuryQuestioningCatalog.Questions[tones[offset]];
                // Long addition preserves the source number arithmetic at Int32.MaxValue.
                result[offset] = new WebJuryQuestionOption { tone = tones[offset], text = questions[(int)(((long)finalistIndex + offset) % questions.Length)] };
            }
            return result;
        }

        /// <summary>Source offline/error response for a player juror. One draw, confident tone, NO score impact.</summary>
        public static WebJuryAnswer CreateFallbackAnswer(Func<double> nextRoll)
        {
            if (nextRoll == null) throw new ArgumentNullException(nameof(nextRoll));
            double roll = nextRoll(); RequireRoll(roll);
            var answers = WebJuryQuestioningCatalog.NpcResponses;
            return new WebJuryAnswer { answer = answers[(int)Math.Floor(roll * answers.Length)], tone = "confident" };
        }

        /// <summary>Checks saved question content without consuming RNG. Ownership/phase checks remain caller-owned.</summary>
        public static bool MatchesSource(WebJuryQuestion question, IReadOnlyList<string> jurorTraits, int jurorIndex)
        {
            if (question == null || jurorIndex < 0 || (question.correctIs != "A" && question.correctIs != "B")
                || question.tone == null || !WebJuryQuestioningCatalog.Questions.TryGetValue(question.tone, out var questions)) return false;
            var pair = GetResponsePair(jurorTraits, jurorIndex, question.correctIs == "A" ? 0.75 : 0.25);
            return question.question == questions[jurorIndex % questions.Length] && question.optionA == pair.optionA
                && question.optionB == pair.optionB && question.trait == pair.trait && question.opponentAnswer == GetOpponentAnswer(jurorIndex);
        }

        private static void RequireIndex(int index)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
        }

        private static void RequireRoll(double roll)
        {
            if (double.IsNaN(roll) || double.IsInfinity(roll) || roll < 0 || roll >= 1)
                throw new ArgumentOutOfRangeException(nameof(roll), "Random samples must be in [0, 1).");
        }

        private static void RequireChoice(string choice)
        {
            if (choice != "A" && choice != "B") throw new ArgumentException("Choice must be A or B.", nameof(choice));
        }
    }
}
