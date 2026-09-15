using System;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Pure authored fallback, adapted from canonical character biographies and local dialogue
    /// topics in the web game. New prose, not a generated service response or a rules mutation.
    /// Reads only public phase, the speaker's own relationship, direct promises and shared alliance.
    /// </summary>
    public static class HouseDialogue
    {
        public static string Greeting(EpisodeState state, string npcId)
        {
            var npc = Speaker(state, npcId);
            if (npc == null) return string.Empty;
            string id = ContentCatalog.CanonicalId(npc.id);
            if (state.phase != EpisodePhase.Social && state.phase != EpisodePhase.Campaign)
                return Pick(id,
                    "The ceremony comes first. Let's talk when we have time to be clear.",
                    "The next round is waiting. Catch me when we're done.",
                    "Let's get through the ceremony. We can find a quiet minute afterward.",
                    "This house has terrible timing. Let's catch up after the ceremony.",
                    "Let's finish the current decision before adding another conversation.",
                    "Let's finish the ceremony, then talk during free time.");
            if (state.phase == EpisodePhase.Campaign && Contains(state.nominees, npc.id))
                return Pick(id,
                    "I'm on the block. I can make my case, but I won't pretend I know your vote.",
                    "I'm nominated. Give me a fair chance to tell you why I should stay.",
                    "Being nominated is hard. Could we talk honestly about where things stand?",
                    "My name is on the block, which makes small talk a little ambitious. Hear me out?",
                    "I'm nominated. I'd like to explain my case without guessing what you've decided.",
                    "I'm nominated. Can I tell you why I want to stay?");
            string opening = Pick(id,
                "Before we make a deal, let's be clear about what we're promising.",
                "Give me a fair shot in the yard. After that, we can talk numbers.",
                "Want a quiet minute? This place can make every conversation feel urgent.",
                "Funny how everyone has a plan until someone asks who it's for.",
                "I can work with uncertainty. I just need to know which parts are facts.",
                "Let's take a minute and talk about our own game.");
            return opening + " " + RelationshipLine(state, npc);
        }

        /// <summary>A contextual reply with no claim that a new action was accepted.</summary>
        public static string Response(EpisodeState state, string npcId)
        {
            var npc = Speaker(state, npcId);
            return npc == null ? string.Empty : RelationshipLine(state, npc);
        }

        /// <summary>
        /// Call only after the named action was accepted, using its committed state. Rejected or
        /// duplicate commands should retain their own result text and must not replay this response.
        /// No response itself creates, fulfils or guarantees a promise, alliance, disclosure or vote.
        /// </summary>
        public static string Response(EpisodeState state, string npcId, EpisodeCommandKind acceptedAction)
        {
            var npc = Speaker(state, npcId);
            if (npc == null) return string.Empty;
            string id = ContentCatalog.CanonicalId(npc.id);
            if (acceptedAction == EpisodeCommandKind.FormAlliance && SharedAlliance(state, npc.id, true))
                return Pick(id,
                    "We have an alliance. Let's keep its commitments precise and talk before changing course.",
                    "We're working together. I still intend to compete for my own game.",
                    "I'm glad we're working together. Let's keep making room for an honest conversation.",
                    "Our alliance has a name now. I'd like it to have substance, too.",
                    "Our alliance is in place. A plan is useful when both people understand it.",
                    "We have an alliance now. Let's be clear with each other.");
            if (acceptedAction == EpisodeCommandKind.LeaveAlliance && !SharedAlliance(state, npc.id, true) && SharedAlliance(state, npc.id, false))
                return Pick(id,
                    "You've left our alliance. I'll treat that as a change in our agreement, not an unspoken favor.",
                    "You left our alliance. All right. I'm playing my own game from here.",
                    "You left our alliance. That hurts, but I'd rather know where we stand.",
                    "So our alliance is over. Awkward, but at least nobody has to guess.",
                    "The alliance is over. I'll stop making plans that depend on it.",
                    "Our alliance has ended. We'll need to rebuild trust before making another plan.");
            if (acceptedAction == EpisodeCommandKind.PromiseSafety && DirectActivePromise(state, npc.id, PromiseKind.Safety))
                return Pick(id,
                    "I heard your safety promise. I'll judge it by the decisions you actually control.",
                    "You promised me safety. Don't say it just because it sounds good right now.",
                    "I heard your promise to keep me safe. Please be honest if your situation changes.",
                    "A safety promise. That's one sentence I'll remember when the room gets quiet.",
                    "Your safety promise is clear. It isn't the same thing as control over everyone else's vote.",
                    "I heard your safety promise. I'll remember it when decisions are made.");
            if (acceptedAction == EpisodeCommandKind.PromiseFinalTwo && DirectActivePromise(state, npc.id, PromiseKind.FinalTwo))
                return Pick(id,
                    "You made a final-two promise to me. It's your commitment; don't mistake my hearing it for a second promise.",
                    "You said final two. That's a big commitment. I won't forget you made it.",
                    "I heard your final-two promise. I want us to be honest about what that asks of you.",
                    "Final two is a very small room. I'll remember that you put my name in it.",
                    "Your final-two promise is recorded between us. It doesn't make the outcome certain.",
                    "I heard your final-two promise. That is a commitment, not a guaranteed result.");
            if (acceptedAction == EpisodeCommandKind.PromiseVote && DirectActivePromise(state, npc.id, PromiseKind.Vote))
                return Pick(id,
                    "I heard your voting promise. I'm holding you to your choice, not to guesses about the house.",
                    "You told me how you'd vote. Follow through if you're going to say it.",
                    "Thank you for being clear about your voting promise. I know the final count isn't yours alone.",
                    "A voting promise is useful. A prediction dressed up as one isn't.",
                    "I understand your voting promise. We still don't know everyone else's ballot.",
                    "I heard your voting promise. We can't promise the whole house's decision.");
            if (acceptedAction == EpisodeCommandKind.ShareInformation && HasOwnMemory(state, npc.id, null, "Heard from you: "))
                return Pick(id,
                    "I'll keep what you told me separate from what I've witnessed myself.",
                    "I heard you. I'm not calling something a fact just because it helps my game.",
                    "Thank you for telling me. I'll be careful with it, and with what I assume it means.",
                    "Useful to hear. I'll leave room for the parts neither of us actually saw.",
                    "That's information from you, not an independent observation. The distinction matters.",
                    "I heard what you shared. I'll distinguish it from what I've seen myself.");
            if (acceptedAction == EpisodeCommandKind.Talk && HasOwnMemory(state, npc.id, state.playerId, "We spent time talking in week "))
                return Pick(id,
                    "I'm glad we took the time to talk.",
                    "Good. A straight conversation beats circling each other all afternoon.",
                    "Thank you for making time for me.",
                    "Look at us, finishing a conversation without a dramatic announcement.",
                    "Talking helped. I don't need every uncertainty settled at once.",
                    "Thanks for taking the time to talk.") + " " + RelationshipLine(state, npc);
            return RelationshipLine(state, npc);
        }

        private static string RelationshipLine(EpisodeState state, ContestantState npc)
        {
            string id = ContentCatalog.CanonicalId(npc.id);
            // Promise outcomes are only those made by the player directly to this speaker.
            // Use the last resolved direct promise in saved list order; this DTO has no separate
            // settlement timestamp. Active promises do not erase an earlier recorded outcome.
            var outcome = LatestDirectOutcome(state, npc.id);
            if (outcome != null && outcome.status == PromiseStatus.Broken)
                return Pick(id,
                    "You broke a promise to me. I can listen, but rebuilding trust will take more than another promise.",
                    "You broke your promise to me. Show me something different before asking me to rely on you.",
                    "I remember the promise you broke to me. I'm willing to talk about it, not pretend it didn't hurt.",
                    "A promise you made to me didn't survive the decision. You'll understand if I'm careful now.",
                    "You broke a promise to me. I'm adjusting what I rely on, not guessing at your intentions.",
                    "You broke a promise to me. I need time and actions before I can rely on you again.");
            if (outcome != null && outcome.status == PromiseStatus.Fulfilled)
                return Pick(id,
                    "You kept your promise to me. That gives this conversation something solid to build on.",
                    "You followed through on your promise to me. I respect that.",
                    "You kept your promise to me. I haven't forgotten how that felt.",
                    "You kept your word to me. Refreshing, honestly.",
                    "You fulfilled your promise to me. That's an action I can actually account for.",
                    "You kept your promise to me. I appreciate the follow-through.");
            if (SharedAlliance(state, npc.id, true))
                return Pick(id,
                    "We share an alliance. Let's make sure we're discussing the same plan.",
                    "We're allies. I want a plan that gives both of us a real shot.",
                    "We're in an alliance together. I'd like us to keep checking in, not just counting votes.",
                    "We're allies, so let's make this more useful than exchanging reassuring looks.",
                    "We share an alliance. I'd rather clarify an assumption than build a plan on it.",
                    "We're allies. Let's be clear about what we're asking of each other.");
            double trust = OwnTrust(state, npc.id);
            if (trust <= -15)
                return Pick(id,
                    "I'm cautious about relying on you. Let's keep this conversation specific.",
                    "I'm not ready to rely on you. Say what you mean and we'll start there.",
                    "I'm feeling guarded with you. A little honesty would help us start again.",
                    "I'm keeping a little distance. We can still have an honest conversation.",
                    "I'm cautious about trusting you. I'd like to separate what happened from what we assumed.",
                    "I'm cautious about trusting you, but I'm willing to listen.");
            if (trust >= 25)
                return Pick(id,
                    "I'm comfortable talking with you. That doesn't mean we should skip the details.",
                    "I feel good about talking with you. Let's keep it straightforward.",
                    "I'm glad it's you. I feel more at ease when we talk.",
                    "You're someone I can have a real conversation with. Let's not waste it.",
                    "I trust you enough to speak plainly. We can still disagree about a plan.",
                    "I feel comfortable talking with you. Let's be honest about our plans.");
            return Pick(id,
                "I'm still getting to know you. What matters most to you in this game?",
                "I'm still figuring you out. I'd rather hear your plan directly.",
                "We don't have to solve the whole game today. How are you settling in?",
                "We're still getting to know each other. Tell me something that isn't a campaign slogan.",
                "I'm still getting to know you. A clear conversation is a useful place to start.",
                "I'm still getting to know you. What's on your mind?");
        }

        private static ContestantState Speaker(EpisodeState state, string npcId)
        {
            if (state?.contestants == null || string.IsNullOrEmpty(state.playerId) || string.IsNullOrEmpty(npcId)) return null;
            ContestantState speaker = null;
            bool playerExists = false;
            foreach (var contestant in state.contestants)
            {
                if (contestant == null) continue;
                if (contestant.id == state.playerId && contestant.isPlayer) playerExists = true;
                if (contestant.id == npcId) speaker = contestant;
            }
            if (speaker == null && npcId == "maya")
                foreach (var contestant in state.contestants)
                    if (contestant != null && contestant.id == ContentCatalog.MayaId) { speaker = contestant; break; }
            return playerExists && speaker != null && !speaker.isPlayer && speaker.status == ContestantStatus.Active ? speaker : null;
        }

        private static double OwnTrust(EpisodeState state, string npcId)
        {
            if (state.relationships != null)
                foreach (var edge in state.relationships)
                    if (edge != null && edge.fromId == npcId && edge.toId == state.playerId)
                        return double.IsNaN(edge.score) || double.IsInfinity(edge.score) ? 0 : edge.score;
            return 0;
        }

        private static PromiseState LatestDirectOutcome(EpisodeState state, string npcId)
        {
            if (state.promises == null) return null;
            for (int index = state.promises.Count - 1; index >= 0; index--)
            {
                var promise = state.promises[index];
                if (promise != null && promise.fromId == state.playerId && promise.toId == npcId && promise.week <= state.week &&
                    (promise.status == PromiseStatus.Broken || promise.status == PromiseStatus.Fulfilled)) return promise;
            }
            return null;
        }

        private static bool DirectActivePromise(EpisodeState state, string npcId, PromiseKind kind)
        {
            if (state.promises == null) return false;
            foreach (var promise in state.promises)
                if (promise != null && promise.fromId == state.playerId && promise.toId == npcId && promise.kind == kind &&
                    promise.status == PromiseStatus.Active && promise.week <= state.week &&
                    (promise.expiresWeek == 0 || promise.expiresWeek >= state.week)) return true;
            return false;
        }

        private static bool SharedAlliance(EpisodeState state, string npcId, bool active)
        {
            if (state.alliances == null) return false;
            foreach (var alliance in state.alliances)
                if (alliance != null && alliance.active == active && Contains(alliance.members, npcId) && Contains(alliance.members, state.playerId)) return true;
            return false;
        }

        private static bool HasOwnMemory(EpisodeState state, string npcId, string subjectId, string prefix)
        {
            if (state.memories == null) return false;
            foreach (var memory in state.memories)
                if (memory != null && memory.ownerId == npcId && memory.week <= state.week &&
                    (subjectId == null || memory.subjectId == subjectId) && memory.text != null && memory.text.StartsWith(prefix, StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool Contains(System.Collections.Generic.List<string> ids, string id) => ids != null && ids.Contains(id);

        private static string Pick(string id, string maya, string taylor, string jamie, string casey, string riley, string fallback)
        {
            switch (id)
            {
                case "maya-hassan": return maya;
                case "taylor-kim": return taylor;
                case "jamie-roberts": return jamie;
                case "casey-wilson": return casey;
                case "riley-johnson": return riley;
                default: return fallback; // Imported IDs are not inferred from displayed names.
            }
        }
    }
}
