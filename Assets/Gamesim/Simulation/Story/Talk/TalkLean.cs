using System;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The player's own read (slice S2, talk rules 2, the lead's ruling T4): the class of their creator traits, moved by the
    /// running lean of the tones they answer in (<c>story.talkLean</c>, -6..6, the one field slice 2 writes). Each counted free
    /// talk at talk rules 2 or later moves the lean by its tone's <see cref="Step"/>; nothing else writes it. Below talk rules 2
    /// the lean is never read and stays 0. Pure, nothing drawn.
    /// </summary>
    public static class TalkLean
    {
        /// <summary>The lean from which the player's class moves one class: toward Spark at 4 or more, toward Guarded at -4 or less.</summary>
        public const int ShiftAt = 4;

        /// <summary>The profile words (spec 3.6). "A flirt" is the game's word for a bold talker; nothing romantic follows from it in slice 2.</summary>
        public const string SparkWord = "a flirt", SteadyWord = "easy to talk to", GuardedWord = "hard to read";

        /// <summary>The player's own line: "The house reads you as " + the word + ".".</summary>
        public const string ReadsYouAs = "The house reads you as ";

        /// <summary>
        /// The step a counted talk answered in a tone moves the lean (T4): the bold and playful tones Tease and Push +1; the careful
        /// and yielding tones Careful and Let it go -1; Warm, Straight and Call it out 0. Flirt is slice 3's: 0 here.
        /// </summary>
        public static int Step(string tone)
        {
            switch (tone)
            {
                case TalkRules.Tease:
                case TalkRules.Push:
                    return 1;
                case TalkRules.Careful:
                case TalkRules.LetGo:
                    return -1;
                default:
                    return 0;
            }
        }

        /// <summary>The lean after a step, clamped to -6..6.</summary>
        public static int After(int lean, string tone) =>
            Math.Max(-TalkRules.MostLean, Math.Min(TalkRules.MostLean, lean + Step(tone)));

        /// <summary>The class the player's creator traits give them (spec 3.5's rule, <see cref="FreeTalk.ClassOf"/>).</summary>
        public static string StartClass(EpisodeState s) => FreeTalk.ClassOf(s?.Find(s.playerId));

        /// <summary>A class moved by a lean: one class toward Spark at <see cref="ShiftAt"/> or more, toward Guarded at -4 or less, never more than one.</summary>
        public static string ClassAt(string startClass, int lean)
        {
            if (lean >= ShiftAt) return startClass == TalkPools.Guarded ? TalkPools.Steady : TalkPools.Spark;
            if (lean <= -ShiftAt) return startClass == TalkPools.Spark ? TalkPools.Steady : TalkPools.Guarded;
            return startClass;
        }

        /// <summary>The player's class now: their start class moved by the saved lean at talk rules 2; null when slice 2 is off.</summary>
        public static string PlayerClass(EpisodeState s) =>
            TalkChemistry.On(s) ? ClassAt(StartClass(s), s.story.talkLean) : null;

        /// <summary>A class's profile word.</summary>
        public static string Word(string playerClass) =>
            playerClass == TalkPools.Spark ? SparkWord : playerClass == TalkPools.Guarded ? GuardedWord : SteadyWord;

        /// <summary>"The house reads you as a flirt." for a class.</summary>
        public static string LineFor(string playerClass) => ReadsYouAs + Word(playerClass) + ".";
    }
}
