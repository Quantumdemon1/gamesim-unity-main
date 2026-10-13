#if GAMESIM_ALLSTAR_CANDIDATE
namespace Gamesim.Simulation
{
    /// <summary>Original authored speech for already accepted ordinary conversations; no hidden facts or new outcomes.</summary>
    public static class AlumniVoice
    {
        public static string Response(EpisodeState state, string npcId, EpisodeCommandKind kind, string fallback)
        {
            if (!AlumniHistory.TryProfile(state, npcId, out var profile)) return fallback;
            var voice = Words(profile.TemplateId);
            switch (kind)
            {
                case EpisodeCommandKind.SmallTalk:
                case EpisodeCommandKind.RelationshipBuilding: return voice.social;
                case EpisodeCommandKind.StrategicDiscussion:
                case EpisodeCommandKind.Talk: return voice.game;
                case EpisodeCommandKind.ShareInformation: return voice.information;
                default: return fallback;
            }
        }

        private static (string social, string game, string information) Words(string id)
        {
            switch (id)
            {
                case "dan-gheesling": return ("You learn a lot about somebody when there isn't a competition going on.",
                    "Walk me through what you need. Then we can see where our interests meet.", "I appreciate you bringing it to me. Let me think about what it changes.");
                case "dr-will-kirby": return ("Finally, somebody who appreciates my excellent company.",
                    "Everybody wants to be in charge. I'm happy to let them explain why keeping me helps.", "Interesting. I promise to look suitably surprised when I hear it again.");
                case "derrick-levasseur": return ("It's easier to work together when we actually know each other.",
                    "Let's look at the people who benefit from each option. We don't need to make this messier than it is.", "Thanks. We should check that against what we already know.");
                case "cody-calafiore": return ("I like being able to have a normal conversation in here.",
                    "I'd rather know where we stand than guess. What are you thinking for this week?", "Okay. That gives me something to think through before making a move.");
                case "janelle-pierzina": return ("Good. We can talk about something besides who is winning the next competition.",
                    "Tell me what you're offering and what you need back. I don't want vague promises.", "Thanks for telling me directly. I'll keep my eyes open.");
                case "danielle-reyes": return ("You have to laugh sometimes. Otherwise this house will drive you up the wall.",
                    "I listen carefully. The people who say the least can still be doing the most.", "I'll remember that. Knowing something and showing everybody you know it are different things.");
                case "vanessa-rousso": return ("A little time to breathe helps. I've been thinking through a lot of possibilities.",
                    "What happens after that move? Who connects the groups, and who would have the numbers?", "That changes one part of the calculation. I still want to check the rest.");
                case "tyler-crispen": return ("It's nice to hang out without making every minute a game conversation.",
                    "Let's talk it through quietly. I want to know what we can actually count on.", "Thanks for trusting me with it. I'll think about where it fits.");
                case "chelsie-baham": return ("We can have a good time and still keep our eyes open.",
                    "Everybody sees who wins. I also watch who gets the winner to make a decision.", "That's useful. Timing matters as much as knowing it.");
                case "rachel-reilly": return ("We can have fun in here. I'm still going to fight hard when it matters.",
                    "If you want me fighting for you, tell me how we protect each other.", "I'd rather you say it to me than have me hear it through somebody else.");
                case "xavier-prather": return ("A calm conversation does everybody some good.",
                    "We can disagree about a move. First, let's understand what each of us is trying to protect.", "All right. Let's keep the facts separate from the assumptions.");
                default: return ("Everybody needs a meal and a conversation eventually.",
                    "I want to know where the information goes, not just who is talking the loudest.", "That's useful. Some people bring better information than ingredients.");
            }
        }
    }
}
#endif
