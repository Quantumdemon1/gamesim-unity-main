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
                return Vary(state,
                    Pick(id,
                        "The ceremony comes first. Let's talk when we have time to be clear.",
                        "The next round is waiting. Catch me when we're done.",
                        "Let's get through the ceremony. We can find a quiet minute afterward.",
                        "This house has terrible timing. Let's catch up after the ceremony.",
                        "Let's finish the current decision before adding another conversation.",
                        "Let's finish the ceremony, then talk during free time."),
                    Pick(id,
                        "Not now — a conversation held during a ceremony is a conversation half heard.",
                        "Whistle's about to go. Find me after and I'm all yours.",
                        "Let's give the ceremony its moment. I'll be right here afterward.",
                        "Everyone wants to talk the minute nobody can. After the ceremony.",
                        "There's a decision in progress. I'd rather not add variables to it.",
                        "After the ceremony. There'll be time."),
                    Pick(id,
                        "Let's do this properly, and properly means after the ceremony.",
                        "Heads in the game first. We'll talk when the result's in.",
                        "I'd rather listen to you than half-listen. Let's wait until the ceremony's done.",
                        "I'd love to plot, but the house is busy pretending to be surprised. Later.",
                        "One process at a time. Once the ceremony closes, I'm available.",
                        "The ceremony first. Then we talk."));
            if (state.phase == EpisodePhase.Campaign && Contains(state.nominees, npc.id))
                return Vary(state,
                    Pick(id,
                        "I'm on the block. I can make my case, but I won't pretend I know your vote.",
                        "I'm nominated. Give me a fair chance to tell you why I should stay.",
                        "Being nominated is hard. Could we talk honestly about where things stand?",
                        "My name is on the block, which makes small talk a little ambitious. Hear me out?",
                        "I'm nominated. I'd like to explain my case without guessing what you've decided.",
                        "I'm nominated. Can I tell you why I want to stay?"),
                    Pick(id,
                        "My name is up. I'll tell you plainly why it shouldn't be, and you can weigh it.",
                        "I'm on the block, and I'm not going to sulk about it. Ask me anything.",
                        "It's my week on the block. I'd be grateful for a few honest minutes.",
                        "I'm nominated, so apparently I'm interesting now. Want to hear my side?",
                        "I've been nominated. I'd like to lay out the facts before the house settles on a story.",
                        "I'm on the block this week. Can we talk about it?"),
                    Pick(id,
                        "Nominated. I'd rather make my case to your face than have it made for me.",
                        "The block's a competition like any other. Give me a fair hearing.",
                        "I won't pretend I'm not worried. Could we just talk, the two of us?",
                        "Being on the block does wonders for your popularity. Suddenly everyone has a minute.",
                        "I'm nominated, and I know some of what's being said isn't accurate. Let me correct it.",
                        "I'm nominated. I'd like a chance to explain."));
            string opening = Vary(state,
                Pick(id,
                    "Before we make a deal, let's be clear about what we're promising.",
                    "Give me a fair shot in the yard. After that, we can talk numbers.",
                    "Want a quiet minute? This place can make every conversation feel urgent.",
                    "Funny how everyone has a plan until someone asks who it's for.",
                    "I can work with uncertainty. I just need to know which parts are facts.",
                    "Let's take a minute and talk about our own game."),
                Pick(id,
                    "I keep a short list of what I've promised in here. Let's not add to it lightly.",
                    "Between competitions is when the real work gets done. What's on your mind?",
                    "Come sit. Nobody has to decide anything in the next five minutes.",
                    "Let me guess — you want to talk about the vote. Everyone's very original this week.",
                    "Tell me what you've actually seen, not what you've heard. I'll do the same.",
                    "Good to catch you. What's going on with you this week?"),
                Pick(id,
                    "Say what you mean and I'll do the same. It saves a second conversation later.",
                    "I don't do side-eye. If we've got something to sort out, sort it here.",
                    "How are you holding up? Really, not the version for the house.",
                    "Every wall in this place has ears, so let's give them something worth hearing.",
                    "I've been keeping notes. Some of them are about you, and they're mostly fair.",
                    "Let's talk. Where do you stand this week?"));
            return opening + " " + RelationshipLine(state, npc);
        }

        /// <summary>
        /// A nominee's speech from the block on eviction night.
        ///
        /// <para>Authored in the same voices as the rest of this file, and written from the
        /// speaker's own standing only — a plea that named who was voting which way would be the
        /// nominee telling the house something they have no way of knowing.</para>
        /// </summary>
        public static string EvictionPlea(EpisodeState state, string npcId)
        {
            var npc = Speaker(state, npcId);
            if (npc == null) return "I'd like to stay. That's the whole speech.";
            string id = ContentCatalog.CanonicalId(npc.id);
            return Vary(state,
                Pick(id,
                    "I've kept every promise I made in here, including the ones that cost me. Keep me and that doesn't change.",
                    "I'm not going to beg. I've played hard and I've played straight, and I'd like the chance to keep doing both.",
                    "I've looked after people in this house when it wasn't strategic. I'd like to think that counts for something tonight.",
                    "I know I'm loud. I also know I've never lied to any of you about what I wanted. Keep me and you keep that.",
                    "I've watched this house carefully, and I've told you what I saw. I'd like to keep being useful to you.",
                    "I'd like to stay, and I'd rather ask you honestly than work you for it. That's my speech."),
                Pick(id,
                    "I've told you the truth even when a lie would have been safer. Keep me and you keep that standard.",
                    "I've won when it mattered and I've lost without excuses. Give me the chance to do it again.",
                    "I've been there for people when the cameras didn't care. I'm asking you to be there for me tonight.",
                    "I know I'm not everyone's cup of tea. But I've never poured you a cold one.",
                    "I've paid attention, and I've shared what I saw with the people who asked. I'd like to keep doing that for you.",
                    "I'd like to stay. I've played fair and I'll keep playing fair."),
                Pick(id,
                    "Every deal I made, I kept. If that's worth something in this house, show me tonight.",
                    "I'm a target because I compete. Keep me and I'll compete for you too.",
                    "I've tried to make this house kinder. I'd like a few more weeks to keep trying.",
                    "If you evict me, the house gets quieter and duller. Just saying.",
                    "I've never told you a fact I couldn't stand behind. That's the whole case.",
                    "I want to stay. I'll earn it if you let me."));
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
                return Vary(state,
                    Pick(id,
                        "We have an alliance. Let's keep its commitments precise and talk before changing course.",
                        "We're working together. I still intend to compete for my own game.",
                        "I'm glad we're working together. Let's keep making room for an honest conversation.",
                        "Our alliance has a name now. I'd like it to have substance, too.",
                        "Our alliance is in place. A plan is useful when both people understand it.",
                        "We have an alliance now. Let's be clear with each other."),
                    Pick(id,
                        "An alliance, then. Let's write the terms in our heads so neither of us rewrites them later.",
                        "Allies. Good. I'll pull my weight; I expect you to pull yours.",
                        "I'm really glad. It's easier to be brave in here with someone beside you.",
                        "An alliance. I'll try not to look too smug about it in the kitchen.",
                        "Alliance noted. I'll treat it as data about what you'll do, not a guarantee.",
                        "We're allied now. Let's keep talking."),
                    Pick(id,
                        "We're in this together. I take that seriously, and I'll expect you to.",
                        "Deal. Now let's win something and make it count.",
                        "Thank you. I'll be honest with you, even when it's awkward.",
                        "Partners in crime. Mostly the polite kind.",
                        "Fine. An alliance is a hypothesis about trust; let's test it gently.",
                        "We're working together. Good."));
            if (acceptedAction == EpisodeCommandKind.LeaveAlliance && !SharedAlliance(state, npc.id, true) && SharedAlliance(state, npc.id, false))
                return Vary(state,
                    Pick(id,
                        "You've left our alliance. I'll treat that as a change in our agreement, not an unspoken favor.",
                        "You left our alliance. All right. I'm playing my own game from here.",
                        "You left our alliance. That hurts, but I'd rather know where we stand.",
                        "So our alliance is over. Awkward, but at least nobody has to guess.",
                        "The alliance is over. I'll stop making plans that depend on it.",
                        "Our alliance has ended. We'll need to rebuild trust before making another plan."),
                    Pick(id,
                        "Noted. I'd rather an honest exit than a quiet one, and this was honest.",
                        "You're out. Fine — but don't expect me to guard your back on the block.",
                        "I understand. I'll miss having you in my corner.",
                        "Breaking up with me in the living room. Bold. Noted.",
                        "Alliance dissolved. I'm updating what I expect from you accordingly.",
                        "You've left. I'll plan without you."),
                    Pick(id,
                        "That's your right. I'll hold you to nothing now, and expect the same.",
                        "Okay. We're competitors again. I can respect that.",
                        "I won't hold it against you, but I'd have liked a warning.",
                        "And just like that, I'm single again. The house will talk.",
                        "Understood. Our agreement is over; the history isn't.",
                        "The alliance is done. Good luck."));
            if (acceptedAction == EpisodeCommandKind.PromiseSafety && DirectActivePromise(state, npc.id, PromiseKind.Safety))
                return Vary(state,
                    Pick(id,
                        "I heard your safety promise. I'll judge it by the decisions you actually control.",
                        "You promised me safety. Don't say it just because it sounds good right now.",
                        "I heard your promise to keep me safe. Please be honest if your situation changes.",
                        "A safety promise. That's one sentence I'll remember when the room gets quiet.",
                        "Your safety promise is clear. It isn't the same thing as control over everyone else's vote.",
                        "I heard your safety promise. I'll remember it when decisions are made."),
                    Pick(id,
                        "I heard your safety promise. Keep it where you can, and tell me where you can't.",
                        "Safety, you say. Prove it the week it's hard, not the week it's free.",
                        "Thank you for promising. I'll try not to lean on it more than is fair.",
                        "A safety promise. I'll frame it, next to the other ones.",
                        "Safety promised. I'll record it against your future votes, not your intentions.",
                        "You've promised to keep me safe. I'll remember."),
                    Pick(id,
                        "I heard your safety promise. A promise is a decision you've made early; make sure you meant it.",
                        "You've got my safety in your hands. Don't fumble it.",
                        "That means a lot. I hope the week never tests it.",
                        "Promised safety. Let's see if it survives contact with the nominations.",
                        "Noted. A safety promise binds you, not the six other ballots.",
                        "Safety promised. Thank you."));
            if (acceptedAction == EpisodeCommandKind.PromiseFinalTwo && DirectActivePromise(state, npc.id, PromiseKind.FinalTwo))
                return Vary(state,
                    Pick(id,
                        "You made a final-two promise to me. It's your commitment; don't mistake my hearing it for a second promise.",
                        "You said final two. That's a big commitment. I won't forget you made it.",
                        "I heard your final-two promise. I want us to be honest about what that asks of you.",
                        "Final two is a very small room. I'll remember that you put my name in it.",
                        "Your final-two promise is recorded between us. It doesn't make the outcome certain.",
                        "I heard your final-two promise. That is a commitment, not a guaranteed result."),
                    Pick(id,
                        "Final two is the promise people mean least and remember longest. I'll remember it.",
                        "Final two. That's a lot of weeks to keep. Let's see if we're both still here.",
                        "I'd be honoured. Let's get there honestly.",
                        "The finale, with me. Either flattering or a very long con.",
                        "A final-two promise: high value, low verifiability. Logged.",
                        "You've promised me final two. I'll hold onto that."),
                    Pick(id,
                        "I heard final two. That's your word; I'll watch how you carry it.",
                        "Final two. Then let's make sure neither of us is on the block first.",
                        "Thank you. I'll try to be someone worth taking that far.",
                        "Final two. If you're lying, at least you picked a good target.",
                        "Final two promised. The odds improve if we both survive the week.",
                        "Final two. I'll remember."));
            if (acceptedAction == EpisodeCommandKind.PromiseVote && DirectActivePromise(state, npc.id, PromiseKind.Vote))
                return Vary(state,
                    Pick(id,
                        "I heard your voting promise. I'm holding you to your choice, not to guesses about the house.",
                        "You told me how you'd vote. Follow through if you're going to say it.",
                        "Thank you for being clear about your voting promise. I know the final count isn't yours alone.",
                        "A voting promise is useful. A prediction dressed up as one isn't.",
                        "I understand your voting promise. We still don't know everyone else's ballot.",
                        "I heard your voting promise. We can't promise the whole house's decision."),
                    Pick(id,
                        "I heard your vote. One ballot is what you control, so that's what I'll hold you to.",
                        "You said how you'll vote. Good. Now don't change it in the diary room.",
                        "Thank you for telling me. Whatever happens, I know where you stood.",
                        "A vote pledge. The house's favourite currency, and the most frequently counterfeited.",
                        "Voting promise received. One vote; I'm not extrapolating.",
                        "You've promised your vote. Noted."),
                    Pick(id,
                        "Your vote is yours to promise; the others aren't. I'll judge only the one.",
                        "Vote's promised. Stick to it and we're good.",
                        "That's kind of you. I'll try to deserve it on the night.",
                        "Your vote, promised in advance. Let's hope the room doesn't get persuasive.",
                        "A promised vote is a single data point. Still, it's a real one.",
                        "Your vote. Thank you."));
            if (acceptedAction == EpisodeCommandKind.ShareInformation && HasOwnMemory(state, npc.id, null, "Heard from you: "))
                return Vary(state,
                    Pick(id,
                        "I'll keep what you told me separate from what I've witnessed myself.",
                        "I heard you. I'm not calling something a fact just because it helps my game.",
                        "Thank you for telling me. I'll be careful with it, and with what I assume it means.",
                        "Useful to hear. I'll leave room for the parts neither of us actually saw.",
                        "That's information from you, not an independent observation. The distinction matters.",
                        "I heard what you shared. I'll distinguish it from what I've seen myself."),
                    Pick(id,
                        "Thank you. I'll hold it as your account, not as the record.",
                        "Good to know. I'll check it against what I've seen before I act on it.",
                        "I appreciate you trusting me with that.",
                        "Interesting. Filed under 'things people tell me on purpose'.",
                        "Received. Source: you. Weighting accordingly.",
                        "Noted. I'll keep that in mind."),
                    Pick(id,
                        "Understood. I'll weigh who told me as much as what was said.",
                        "Right. That changes a few things, if it holds up.",
                        "Thank you. I'll be careful how I use it.",
                        "Gossip with strategic value. My favourite kind.",
                        "That's a claim, not a confirmation. Useful all the same.",
                        "Thanks. Good to know."));
            if (acceptedAction == EpisodeCommandKind.Talk && HasOwnMemory(state, npc.id, state.playerId, "We spent time talking in week "))
                return Vary(state,
                    Pick(id,
                        "I'm glad we took the time to talk.",
                        "Good. A straight conversation beats circling each other all afternoon.",
                        "Thank you for making time for me.",
                        "Look at us, finishing a conversation without a dramatic announcement.",
                        "Talking helped. I don't need every uncertainty settled at once.",
                        "Thanks for taking the time to talk."),
                    Pick(id,
                        "That was useful. Clear conversations are rarer in here than they should be.",
                        "Good talk. Same time tomorrow, minus the cameras.",
                        "That was nice. I needed a normal conversation.",
                        "A whole conversation and nobody cried. Progress.",
                        "Helpful. I have a better model of you now.",
                        "Good talk."),
                    Pick(id,
                        "I'm glad we did that. Say the same thing next week and I'll believe it more.",
                        "Appreciated. It's easier to trust someone who shows up.",
                        "Thank you. It's easier to breathe after talking to you.",
                        "Well, that was almost pleasant. Don't tell anyone.",
                        "Time well spent. Fewer unknowns than before.",
                        "Thanks for talking.")) + " " + RelationshipLine(state, npc);
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
                return Vary(state,
                    Pick(id,
                        "You broke a promise to me. I can listen, but rebuilding trust will take more than another promise.",
                        "You broke your promise to me. Show me something different before asking me to rely on you.",
                        "I remember the promise you broke to me. I'm willing to talk about it, not pretend it didn't hurt.",
                        "A promise you made to me didn't survive the decision. You'll understand if I'm careful now.",
                        "You broke a promise to me. I'm adjusting what I rely on, not guessing at your intentions.",
                        "You broke a promise to me. I need time and actions before I can rely on you again."),
                    Pick(id,
                        "You broke your word to me. I'm not angry; I'm recalculating.",
                        "You broke a promise. In the yard we'd call that a foul.",
                        "I trusted you, and it didn't hold. I'm still willing to try, slowly.",
                        "Ah, the promise that wasn't. Consider me educated.",
                        "A broken promise is a data point I'd rather not have. It's recorded.",
                        "You broke a promise. That's hard to forget."),
                    Pick(id,
                        "Your promise to me didn't survive. I'll weigh the next one against that.",
                        "You said one thing and did another. Show me you're better than that.",
                        "I don't want to be bitter about the broken promise. Help me not be.",
                        "Broken promises are the house special. Still stings when it's yours.",
                        "The promise failed. I'm not judging you, just updating.",
                        "You didn't keep your word. I'll be careful."));
            if (outcome != null && outcome.status == PromiseStatus.Fulfilled)
                return Vary(state,
                    Pick(id,
                        "You kept your promise to me. That gives this conversation something solid to build on.",
                        "You followed through on your promise to me. I respect that.",
                        "You kept your promise to me. I haven't forgotten how that felt.",
                        "You kept your word to me. Refreshing, honestly.",
                        "You fulfilled your promise to me. That's an action I can actually account for.",
                        "You kept your promise to me. I appreciate the follow-through."),
                    Pick(id,
                        "You did what you said. That's the whole basis of anything we build next.",
                        "You came through. That's what I needed to see.",
                        "You kept your word. Thank you — I felt that.",
                        "You kept a promise. In this house that's practically a party trick.",
                        "Promise fulfilled. Your reliability goes up in my book.",
                        "You kept your promise. Thank you."),
                    Pick(id,
                        "Your word held. I noticed, and I'll act on it.",
                        "Kept your promise. Good. I return favours.",
                        "You did what you promised. That makes this easier.",
                        "A kept promise. I'm suspicious, but pleasantly.",
                        "Fulfilled as stated. I'll weight your future promises higher.",
                        "You followed through. I appreciate it."));
            if (SharedAlliance(state, npc.id, true))
                return Vary(state,
                    Pick(id,
                        "We share an alliance. Let's make sure we're discussing the same plan.",
                        "We're allies. I want a plan that gives both of us a real shot.",
                        "We're in an alliance together. I'd like us to keep checking in, not just counting votes.",
                        "We're allies, so let's make this more useful than exchanging reassuring looks.",
                        "We share an alliance. I'd rather clarify an assumption than build a plan on it.",
                        "We're allies. Let's be clear about what we're asking of each other."),
                    Pick(id,
                        "Allies, so no surprises: tell me before you move, not after.",
                        "We're a team. Let's play like one this week.",
                        "It's good having you on my side. Let's keep talking.",
                        "Our alliance, meeting in secret in the most public room in the house.",
                        "We're aligned. Let's make sure we agree on the facts, not just the goal.",
                        "We're allies. Let's coordinate."),
                    Pick(id,
                        "As allies, I'd rather know your doubts than your reassurances.",
                        "Same side. What do you need from me this week?",
                        "I'm glad we're together in this. How are you feeling about the week?",
                        "Ally to ally: who are we pretending to like today?",
                        "Alliance check-in. Any new information I should have?",
                        "Allies. What's the plan?"));
            double trust = OwnTrust(state, npc.id);
            if (trust <= -15)
                return Vary(state,
                    Pick(id,
                        "I'm cautious about relying on you. Let's keep this conversation specific.",
                        "I'm not ready to rely on you. Say what you mean and we'll start there.",
                        "I'm feeling guarded with you. A little honesty would help us start again.",
                        "I'm keeping a little distance. We can still have an honest conversation.",
                        "I'm cautious about trusting you. I'd like to separate what happened from what we assumed.",
                        "I'm cautious about trusting you, but I'm willing to listen."),
                    Pick(id,
                        "I'm careful with you. Give me reasons, not reassurances.",
                        "I've got my guard up with you. Earn it down.",
                        "I'm a little wary. We can still be kind to each other.",
                        "I'm keeping my distance, mostly for my own good.",
                        "My trust in you is low. Evidence can change that.",
                        "I'm wary of you. But I'll listen."),
                    Pick(id,
                        "I don't rely on you right now. That can change; you'd have to change it.",
                        "Not sure about you yet. Show me something.",
                        "I've been hurt before in here. Be patient with me.",
                        "Trust is at a discount today. Sorry, house rules.",
                        "Low confidence on you. Talk specifics and it may rise.",
                        "I'm not sure I can trust you."));
            if (trust >= 25)
                return Vary(state,
                    Pick(id,
                        "I'm comfortable talking with you. That doesn't mean we should skip the details.",
                        "I feel good about talking with you. Let's keep it straightforward.",
                        "I'm glad it's you. I feel more at ease when we talk.",
                        "You're someone I can have a real conversation with. Let's not waste it.",
                        "I trust you enough to speak plainly. We can still disagree about a plan.",
                        "I feel comfortable talking with you. Let's be honest about our plans."),
                    Pick(id,
                        "I trust you. Let's not waste that on vague talk.",
                        "You're one of the good ones. What's up?",
                        "It's nice seeing you. You make this place feel easier.",
                        "My favourite conspirator. What's the scheme?",
                        "I trust your information. Tell me what you know.",
                        "Good to see you. Let's talk."),
                    Pick(id,
                        "We understand each other. Let's use that carefully.",
                        "You've got my respect. Let's talk straight.",
                        "I always feel better after we talk. What's on your mind?",
                        "Ah, someone I actually like. Sit down.",
                        "You're reliable. That's rare and I value it.",
                        "I'm glad it's you. What's going on?"));
            return Vary(state,
                Pick(id,
                    "I'm still getting to know you. What matters most to you in this game?",
                    "I'm still figuring you out. I'd rather hear your plan directly.",
                    "We don't have to solve the whole game today. How are you settling in?",
                    "We're still getting to know each other. Tell me something that isn't a campaign slogan.",
                    "I'm still getting to know you. A clear conversation is a useful place to start.",
                    "I'm still getting to know you. What's on your mind?"),
                Pick(id,
                    "We haven't really talked. What do you actually want from this game?",
                    "Don't know you well yet. What are you here to win?",
                    "We should get to know each other. What's home like for you?",
                    "So, who are you when the cameras aren't rolling?",
                    "I have little data on you. Tell me something true.",
                    "Let's get acquainted. What brings you here?"),
                Pick(id,
                    "I'm reserving judgement on you, which is the fair thing to do.",
                    "Still sizing you up. Nothing personal.",
                    "I'd like to know you better. Tell me something you like.",
                    "You're a mystery. Mysteries make me nervous.",
                    "Open question on you. What's your game plan?",
                    "We haven't talked much. Let's change that."));
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

        /// <summary>
        /// One of three authored variants, by week. The first is the original line, so week one
        /// reads as it always has and every pinned test keeps its text; the later weeks stop
        /// repeating it. Keyed on the week and never on a roll: a line is presentation, and
        /// presentation must not spend the season's randomness.
        /// </summary>
        private static string Vary(EpisodeState state, string first, string second, string third)
        {
            int week = state != null && state.week > 0 ? state.week : 1;
            switch ((week - 1) % 3)
            {
                case 1: return second;
                case 2: return third;
                default: return first;
            }
        }

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
