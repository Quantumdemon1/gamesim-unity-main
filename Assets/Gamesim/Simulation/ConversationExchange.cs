using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Authored words for one accepted conversation command. This is transient presentation,
    /// never a saved record or a second command. Narration stays narration in OutcomeLine.
    /// </summary>
    public sealed class ConversationExchange
    {
        public string PlayerId { get; }
        public string NpcId { get; }
        public string PlayerLine { get; }
        public string NpcLine { get; }
        public string OutcomeLine { get; }
        public bool IsDramatic { get; }
        public bool CanFollowExistingStory { get; }

        private ConversationExchange(EpisodeState after, string npcId, string playerLine,
            string npcLine, string outcome, bool dramatic)
        {
            PlayerId = after.playerId; NpcId = npcId; PlayerLine = playerLine;
            NpcLine = npcLine; OutcomeLine = outcome; IsDramatic = dramatic;
            CanFollowExistingStory = EpisodeEngine.OpenStoryBeats(after).Any(beat =>
                beat.surface == StorySurfaces.Conversation && (beat.involvedIds.Contains(npcId)
                    || beat.cast.Any(role => role.contestantId == npcId)));
        }

        /// <summary>
        /// Call only after durable publication, with visible events from this command before
        /// any automatic house catch-up. Old, private, forged, rejected and duplicate receipts
        /// produce no exchange. No line is reconstructed from a private relationship or ballot.
        /// </summary>
        public static ConversationExchange Create(EpisodeState before, EpisodeState after,
            EpisodeCommand command, IReadOnlyList<EpisodeEvent> visibleAcceptedEvents)
        {
            if (before == null || after == null || command == null || visibleAcceptedEvents == null
                || string.IsNullOrEmpty(command.id) || before.sessionId != after.sessionId
                || before.playerId != after.playerId || command.actorId != before.playerId
                || command.expectedRevision != before.revision || command.expectedPhase != before.phase
                || after.revision != before.revision + 1 || before.acceptedCommandIds.Contains(command.id)
                || !after.acceptedCommandIds.Contains(command.id)) return null;

            var current = visibleAcceptedEvents.Where(item => CurrentVisible(before, after, item)).ToList();
            if (current.Count == 0) return null;
            string npcId = command.targetId;
            DealState offer = null;
            if (command.kind == EpisodeCommandKind.RespondToDeal)
            {
                offer = CommitmentReferences.FindDeal(before, command.targetId);
                if (offer == null || offer.status != DealStatus.Proposed || offer.recipientId != before.playerId) return null;
                npcId = offer.proposerId;
            }
            else if (command.kind == EpisodeCommandKind.SpreadRumor)
            {
                // A public call-out and the legacy randomly chosen listener are not a two-person
                // spoken exchange with the rumour's subject. Never hand them that listener's words.
                if (!EpisodeEngine.CommitmentRulesOn(before)
                    || string.Equals((command.text ?? "").Trim(), EpisodeEngine.PublicCallout, StringComparison.OrdinalIgnoreCase)) return null;
                npcId = command.secondTargetId;
            }
            var npc = after.Find(npcId);
            if (npc == null || npc.isPlayer || npc.status != ContestantStatus.Active || before.Find(npcId) == null) return null;
            current = current.Where(item => item.audienceIds.Count == 0 || item.audienceIds.Contains(npc.id)).ToList();
            if (current.Count == 0) return null;

            EpisodeEvent source = null;
            string player = null, response = null;
            bool dramatic = false;
            switch (command.kind)
            {
                case EpisodeCommandKind.Lobby:
                    return Lobby(before, after, command, npc, current);
                case EpisodeCommandKind.AskVote:
                    return VoteQuestion(before, after, npc, current);
                case EpisodeCommandKind.AskForIntel:
                    return Intel(before, after, command, npc, current);
                case EpisodeCommandKind.SmallTalk:
                    source = Exact(current, "conversation", "You passed the time with " + npc.name + ".");
                    player = "How's the day treating you?";
                    response = "It's good to take a moment to talk."; break;
                case EpisodeCommandKind.PersonalChat:
                    source = Exact(current, "conversation", "You told " + npc.name + " something about yourself.");
                    player = "Let me tell you a little about myself.";
                    response = "I'm listening. It's good to get to know you."; break;
                case EpisodeCommandKind.RelationshipBuilding:
                    source = Exact(current, "conversation", "You spent real time with " + npc.name + ".");
                    player = "I'd like us to spend some time getting to know each other.";
                    response = "I'm glad we made time for that."; break;
                case EpisodeCommandKind.StrategicDiscussion:
                    source = Exact(current, "conversation", "You talked tactics with " + npc.name + ".");
                    player = "Can we talk through the game for a minute?";
                    response = "Let's think through the choices in front of us."; break;
                case EpisodeCommandKind.Talk:
                    source = Exact(current, "conversation", "You and " + npc.name + " talked about the game.");
                    player = "Let's spend a little time talking about the game.";
                    response = HouseDialogue.TalkAcknowledgement(after, npc.id);
                    if (string.IsNullOrEmpty(response)) response = "Thanks for taking the time to talk.";
                    break;
                case EpisodeCommandKind.DiscussGame:
                    source = Exact(current, "conversation", "You talked game with " + npc.name + " and they were with you.");
                    if (source != null) response = "I see what you're getting at.";
                    else
                    {
                        source = Exact(current, "conversation-backfire", npc.name + " decided you were scheming.");
                        response = "This sounds like scheming.";
                    }
                    player = "Can I talk over my thinking about the game with you?";
                    dramatic = true; break;
                case EpisodeCommandKind.ShareSecret:
                    source = Exact(current, "conversation", "You told " + npc.name + " something you should not have, and they kept it.");
                    if (source != null) response = "I hear you. Thank you for trusting me.";
                    else
                    {
                        source = Exact(current, "conversation-backfire", npc.name + " intends to use what you told them.");
                        // A backfire does not imply the NPC admitted an intention to betray the player.
                        response = "You've told me a lot. Let's leave it there.";
                    }
                    player = "There's something personal I want to tell you.";
                    dramatic = true; break;
                case EpisodeCommandKind.VentAbout:
                    var subject = before.Find(command.secondTargetId);
                    if (subject == null) return null;
                    source = Exact(current, "vent", "You vented about " + subject.name + " to " + npc.name + ", and it landed.");
                    if (source != null) response = "I get why that bothered you.";
                    else
                    {
                        source = Exact(current, "vent", "You vented about " + subject.name + " to " + npc.name + ". It did not land.");
                        response = "I don't want to talk about them that way.";
                    }
                    player = "Can I vent to you about " + subject.name + "?";
                    dramatic = true; break;
                case EpisodeCommandKind.SpreadRumor:
                    var about = before.Find(command.targetId);
                    if (about == null || about.id == npc.id) return null;
                    source = Exact(current, "rumour", "You whispered about " + about.name + " to " + npc.name + ".");
                    // A backfire gives the subject knowledge; it is not a new reply from the listener.
                    player = "There's something going around about " + about.name + ".";
                    response = "That's something you've heard. It isn't a confirmation.";
                    dramatic = true; break;
                case EpisodeCommandKind.ShareInformation:
                    source = Exact(current, "information", "You shared something you personally knew with " + npc.name + ".");
                    player = "There's something I know that I want to share with you.";
                    response = "Thanks for telling me what you know."; break;
                case EpisodeCommandKind.PromiseSafety:
                    source = Exact(current, "promise", "You promised Safety to " + npc.name + ".");
                    player = "I promise you safety.";
                    response = HouseDialogue.Response(after, npc.id, command.kind);
                    dramatic = true; break;
                case EpisodeCommandKind.PromiseFinalTwo:
                    source = Exact(current, "promise", "You promised FinalTwo to " + npc.name + ".");
                    player = "I'm promising to take you to the final two if I can make that choice.";
                    response = HouseDialogue.Response(after, npc.id, command.kind);
                    dramatic = true; break;
                case EpisodeCommandKind.PromiseVote:
                    source = Exact(current, "promise", "You promised Vote to " + npc.name + ".");
                    var voteAbout = before.Find(command.secondTargetId);
                    if (voteAbout == null) return null;
                    player = "I promise to vote to evict " + voteAbout.name + ".";
                    response = HouseDialogue.Response(after, npc.id, command.kind);
                    dramatic = true; break;
                case EpisodeCommandKind.FormAlliance:
                case EpisodeCommandKind.BringIntoAlliance:
                case EpisodeCommandKind.LeaveAlliance:
                    source = current.FirstOrDefault(item => (item.kind == "alliance" || item.kind == EpisodeEngine.AllianceRefusedKind)
                        && item.audienceIds.Contains(npc.id));
                    player = command.kind == EpisodeCommandKind.LeaveAlliance ? "I'm leaving our alliance."
                        : command.kind == EpisodeCommandKind.BringIntoAlliance ? "Would you join our alliance?"
                        : "Would you form an alliance with me?";
                    response = HouseDialogue.Response(after, npc.id, command.kind);
                    dramatic = true; break;
                case EpisodeCommandKind.ProposeDeal:
                    return Proposal(after, command, npc, current);
                case EpisodeCommandKind.RespondToDeal:
                    bool yes = string.Equals((command.text ?? "").Trim(), EpisodeEngine.AcceptDeal, StringComparison.OrdinalIgnoreCase);
                    string title = DealKind.Title(offer.type).ToLowerInvariant();
                    source = Exact(current, "deal", "You " + (yes ? "accepted" : "declined") + " a " + title + " from " + npc.name + ".");
                    player = yes ? "Yes. I accept the " + title + "." : "No. I'm declining the " + title + ".";
                    response = yes ? "All right. We've agreed to those terms." : "All right. I hear your answer.";
                    // The Head of Household's ask to keep the block (ROLE-MOMENTS-PLAN R4): said as what it asks, not by its title.
                    if (offer.type == DealKind.VetoHold)
                    {
                        player = yes ? "Yes. I'll leave your nominations as they are." : "No. I can't promise you that about the veto.";
                        response = yes ? "Good. Then we understand each other." : "All right. I hear your answer.";
                    }
                    dramatic = true; break;
                default: return null;
            }
            return source == null || string.IsNullOrEmpty(response) ? null
                : new ConversationExchange(after, npc.id, player, response, source.text, dramatic);
        }

        private static bool CurrentVisible(EpisodeState before, EpisodeState after, EpisodeEvent item)
        {
            if (item == null || item.sequence < before.nextSequence || item.sequence >= after.nextSequence
                || item.audienceIds == null || (item.audienceIds.Count > 0 && !item.audienceIds.Contains(after.playerId))
                || KnownBallots.TellsAnUnknownBallot(after, item)) return false;
            // The caller can pass cloned projections, but cannot change their words or audience.
            return after.events.Any(saved => saved.sequence == item.sequence && saved.week == item.week
                && saved.phase == item.phase && saved.kind == item.kind && saved.text == item.text
                && saved.audienceIds.SequenceEqual(item.audienceIds));
        }

        private static EpisodeEvent Exact(IEnumerable<EpisodeEvent> events, string kind, string text) =>
            events.FirstOrDefault(item => item.kind == kind && item.text == text);

        private static ConversationExchange Lobby(EpisodeState before, EpisodeState after, EpisodeCommand command,
            ContestantState npc, IReadOnlyList<EpisodeEvent> events)
        {
            if (!LobbyAsk.TryDecode(command.text, out string ask, out string approach)
                || (ask == LobbyAsk.Keep ? command.secondTargetId != null
                    : before.Find(command.secondTargetId) == null)) return null;
            string pitch = StrategyRules.Pitch(before, ask, command.secondTargetId, approach);
            string prefix = "You asked " + npc.name + " " + StrategyRules.Describe(before, ask, command.secondTargetId)
                + ": “" + pitch + "” " + npc.name + ": “";
            var matches = new List<ConversationExchange>();
            // These helpers read only the chosen subject's public name and fixed authored lines.
            // Match the reply actually published; never rerun its chance or read the private
            // influence, current relationship, or a later-mutated LobbyState.response.
            foreach (string verdict in LobbyResponse.All)
            {
                string said = StrategyRules.Answer(before, ask, command.secondTargetId, approach, verdict);
                foreach (var source in events.Where(item => item.kind == "lobby" && item.text == prefix + said + "”"))
                    matches.Add(new ConversationExchange(after, npc.id, pitch, said, source.text, true));
            }
            return matches.Count == 1 ? matches[0] : null;
        }

        private static ConversationExchange VoteQuestion(EpisodeState before, EpisodeState after,
            ContestantState npc, IReadOnlyList<EpisodeEvent> events)
        {
            const string player = "Where's your head at on this week's vote?";
            var matches = new List<ConversationExchange>();
            string deflection = npc.name + " wouldn't say where their vote is. \"Ask me after.\"";
            foreach (var source in events.Where(item => item.kind == "vote-read" && item.text == deflection))
                matches.Add(new ConversationExchange(after, npc.id, player, "Ask me after.", source.text, false));
            // Nominee identity is public; the receipt supplies what this voter SAID, which may
            // be a lie. Do not project the ballot or consult their hidden view or honesty again.
            foreach (string id in before.nominees ?? new List<string>())
            {
                var subject = before.Find(id);
                if (subject == null) continue;
                string receipt = "You asked " + npc.name + " where their head is at. \"" + subject.name + ".\"";
                foreach (var source in events.Where(item => item.kind == "vote-read" && item.text == receipt))
                    matches.Add(new ConversationExchange(after, npc.id, player,
                        "I'm leaning toward evicting " + (subject.id == before.playerId ? "you" : subject.name)
                            + ".", source.text, true));
            }
            // Duplicate custom names or conflicting replies do not authorize choosing a target.
            return matches.Count == 1 ? matches[0] : null;
        }

        private static ConversationExchange Intel(EpisodeState before, EpisodeState after, EpisodeCommand command,
            ContestantState npc, IReadOnlyList<EpisodeEvent> events)
        {
            string generic = "You asked " + npc.name + " what they had been hearing.";
            var none = Exact(events, "information", generic);
            if (none != null && before.Active.All(person => person.isPlayer || person.id == npc.id))
                return new ConversationExchange(after, npc.id, "What have you been hearing around the house?",
                    "There's nobody else for us to discuss.", none.text, false);

            // Enumerate only public cast identity, not scores or memories. The finite legacy event
            // templates identify which opinion the accepted command actually disclosed, including
            // the old rules that ignored a supplied second target. This is not a prose-to-quote transform.
            var matches = new List<ConversationExchange>();
            foreach (var subject in before.Active.Where(person => !person.isPlayer && person.id != npc.id))
            {
                string aimed = "You asked " + npc.name + " what they think of " + subject.name + ".";
                foreach (var prefix in new[] { generic, aimed })
                {
                    if (prefix == aimed && command.secondTargetId != subject.id) continue;
                    foreach (var reading in new[] { "is solid with", "does not trust", "is still working out" })
                    {
                        var source = Exact(events, "information", prefix + " They " + reading + " " + subject.name + ".");
                        if (source == null) continue;
                        string line = reading == "is solid with" ? "I'm solid with " + subject.name + "."
                            : reading == "does not trust" ? "I don't trust " + subject.name + "."
                            : "I'm still figuring " + subject.name + " out.";
                        string player = prefix == aimed ? "What's your read on " + subject.name + "?"
                            : "What have you been hearing around the house?";
                        matches.Add(new ConversationExchange(after, npc.id, player, line,
                            "You heard " + npc.name + "'s stated view of " + subject.name + ".", true));
                    }
                }
            }
            // Ambiguous custom names or multiple claimed answers do not authorize choosing one.
            return matches.Count == 1 ? matches[0] : null;
        }

        private static ConversationExchange Proposal(EpisodeState after, EpisodeCommand command,
            ContestantState npc, IReadOnlyList<EpisodeEvent> events)
        {
            string type = (command.text ?? "").Trim();
            if (!DealKind.IsKnown(type)) return null;
            string title = DealKind.Title(type).ToLowerInvariant();
            // These are the authored spoken replies the engine embeds in its deal receipt.
            // Match the published reply instead of asking Reasoning again: a trust adjustment,
            // new alliance or later catch-up could already have changed the conditions it reads.
            foreach (var answer in DealReplies)
            {
                string said = answer.Line;
                var source = Exact(events, "deal", npc.name + (answer.Accepted ? " agreed a " : " turned down a ")
                    + title + ". “" + said + "”");
                if (source == null) continue;
                string player = "Would you agree to a " + title + "?";
                if (type == DealKind.VoteEvict || type == DealKind.VoteSave)
                {
                    var subject = after.Find(command.secondTargetId);
                    if (subject == null) return null;
                    player = "Will you vote to " + (type == DealKind.VoteSave ? "keep " : "evict ") + subject.name + "?";
                }
                return new ConversationExchange(after, npc.id, player, said, source.text, true);
            }
            return null;
        }

        private static readonly (bool Accepted, string Line)[] DealReplies =
        {
            (true, "I think we can work well together."),
            (true, "I need all the help I can get right now."),
            (true, "We're already working together, so this makes sense."),
            (true, "This could be beneficial for both of us."),
            (false, "I've heard you've broken deals before. I can't trust that."),
            (false, "I don't think I can trust you with that."),
            (false, "Your track record concerns me."),
            (false, "I'm not sure this is the right move for me."),
        };
    }
}
