using System;
using System.Globalization;

namespace Gamesim.Simulation
{
    /// <summary>Repairs recognised historical prose for display; saved text remains authoritative.</summary>
    public static class NarrativeDisplay
    {
        private static readonly string[] IntelReadings = { "is solid with", "does not trust", "is still working out" };

        /// <summary>Returns an exact intel template with chosen pronouns and agreement, or the original line.</summary>
        public static string Render(EpisodeState state, string line)
        {
            if (string.IsNullOrEmpty(line) || state?.contestants == null) return line;
            bool question = line.StartsWith("You asked ", StringComparison.Ordinal);
            if (!question && line.IndexOf(" told me in week ", StringComparison.Ordinal) < 0) return line;

            string rendered = null;
            foreach (var teller in state.contestants)
            {
                if (teller == null || string.IsNullOrEmpty(teller.name)) continue;
                string questionStart = "You asked " + teller.name;
                string memoryStart = teller.name + " told me in week ";
                int week = 0;
                if (question)
                {
                    if (!line.StartsWith(questionStart + " what ", StringComparison.Ordinal)) continue;
                    if (line == questionStart + " what they had been hearing.")
                    {
                        if (rendered != null) return line; // Names must identify one speaker, including old custom casts.
                        rendered = questionStart + " what " + EnglishReference.For(teller).Subject + " had been hearing.";
                        continue;
                    }
                }
                else
                {
                    if (!line.StartsWith(memoryStart, StringComparison.Ordinal)) continue;
                    int end = line.IndexOf(" that they ", memoryStart.Length, StringComparison.Ordinal);
                    if (end < 0 || !int.TryParse(line.Substring(memoryStart.Length, end - memoryStart.Length),
                        NumberStyles.None, CultureInfo.InvariantCulture, out week) || week < 1) continue;
                }

                foreach (var subject in state.contestants)
                {
                    if (subject == null || ReferenceEquals(teller, subject) || string.IsNullOrEmpty(subject.name)) continue;
                    foreach (string reading in IntelReadings)
                    {
                        string oldAnswer = reading + " " + subject.name + ".";
                        bool aimed = question && line == questionStart + " what they think of " + subject.name + ". They " + oldAnswer;
                        bool generic = question && line == questionStart + " what they had been hearing. They " + oldAnswer;
                        bool memory = !question && line == memoryStart + week.ToString(CultureInfo.InvariantCulture) + " that they " + oldAnswer;
                        if (!aimed && !generic && !memory) continue;
                        if (rendered != null) return line;
                        var reference = EnglishReference.For(teller);
                        string answer = Answer(reference, reading, subject.name);
                        rendered = memory
                            ? memoryStart + week.ToString(CultureInfo.InvariantCulture) + " that " + reference.Subject + " " + answer
                            : questionStart + (aimed
                                ? " what " + reference.Subject + " " + reference.Present("thinks", "think") + " of " + subject.name + ". "
                                : " what " + reference.Subject + " had been hearing. ") + reference.SentenceSubject + " " + answer;
                    }
                }
            }
            return rendered ?? line;
        }

        private static string Answer(EnglishReference reference, string reading, string subject)
        {
            if (reading == "is solid with") return reference.Present("is", "are") + " solid with " + subject + ".";
            if (reading == "does not trust") return reference.Present("does", "do") + " not trust " + subject + ".";
            return reference.Present("is", "are") + " still figuring " + subject + " out.";
        }

        /// <summary>Only the known catalogue sentences change; Neutral never calls this display correction.</summary>
        internal static string StoryTemplate(string text, Func<string, ContestantState> resolve)
        {
            string role;
            switch (text)
            {
                case "Let {CONFIDANT} think what {CONFIDANT.they} likes.": role = "CONFIDANT"; break;
                case "Promise {HOH} you will keep {HOH.them} safe next week if {HOH.they} keep you safe now.": role = "HOH"; break;
                case "{HOH} would not commit, and now {HOH.they} know how worried you are.": role = "HOH"; break;
                case "Ask {SUBJECT} what {SUBJECT.they} want out of this game.": role = "SUBJECT"; break;
                case "Help {NOMINEE} through the vote so that {NOMINEE.they} owe you one.": role = "NOMINEE"; break;
                default: return null;
            }
            var who = resolve(role);
            if (who == null) return null;
            var reference = EnglishReference.For(who);
            string name = who.name ?? "this houseguest";
            switch (role)
            {
                case "CONFIDANT": return "Let " + name + " think what " + reference.Subject + " " + reference.Present("likes", "like") + ".";
                case "SUBJECT": return "Ask " + name + " what " + reference.Subject + " " + reference.Present("wants", "want") + " out of this game.";
                case "NOMINEE": return "Help " + name + " through the vote so that " + reference.Subject + " " + reference.Present("owes", "owe") + " you one.";
                default: return text.StartsWith("Promise ", StringComparison.Ordinal)
                    ? "Promise " + name + " you will keep " + reference.Object + " safe next week if " + reference.Subject + " " + reference.Present("keeps", "keep") + " you safe now."
                    : name + " would not commit, and now " + reference.Subject + " " + reference.Present("knows", "know") + " how worried you are.";
            }
        }

        private readonly struct EnglishReference
        {
            public readonly string Subject, Object, SentenceSubject;
            private readonly bool plural;
            private EnglishReference(string subject, string obj, string sentenceSubject, bool plural)
            { Subject = subject; Object = obj; SentenceSubject = sentenceSubject; this.plural = plural; }

            public static EnglishReference For(ContestantState who)
            {
                switch (who.pronouns?.Trim().ToLowerInvariant())
                {
                    case "he/him": return new EnglishReference("he", "him", "He", false);
                    case "she/her": return new EnglishReference("she", "her", "She", false);
                    case "they/them": return new EnglishReference("they", "them", "They", true);
                    default:
                        string name = string.IsNullOrEmpty(who.name) ? "this houseguest" : who.name;
                        return new EnglishReference(name, name, name, false);
                }
            }
            public string Present(string singular, string pluralForm) => plural ? pluralForm : singular;
        }
    }
}
