using System;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Reply cards: the house coming to the player, and the player answering.
    ///
    /// <para>A houseguest confronting the player, a nominee pleading for their vote, and gossip about
    /// the player that reached them: each used to be a line in the log and nothing more. From the
    /// strategy windows each puts a card in front of the player, with the reference's three answers
    /// and its numbers (<c>npc-social-behavior.ts</c>). The reference's answers wrote to a
    /// relationship store nothing ever reads; here they are real. They move how the two of them feel,
    /// the houseguest remembers what was said, gossiping back reaches the listener, and promising a
    /// nominee support is a vote promise, kept or broken at the vote.</para>
    ///
    /// <para>A card waits for the phase it arrived in - free time for a confrontation or gossip, the
    /// campaign for a plea - and goes with it. Ignoring one costs nothing more than what the
    /// houseguest already did. Answering is free, as answering an offer is.</para>
    /// </summary>
    public static class ReplyCards
    {
        public const string Confrontation = "confrontation", Gossip = "gossip", Plea = "plea";
        public static readonly string[] All = { Confrontation, Gossip, Plea };
        public static bool IsKnown(string kind) => kind != null && Array.IndexOf(All, kind) >= 0;

        /// <summary>How often the player finds out a houseguest has been talking about them: the reference's roll.</summary>
        public const double GossipDiscoveryChance = 0.3;

        /// <summary>What the listener hears when the player gossips back, about the one who started it.</summary>
        public const double GossipBackReach = -5;

        /// <summary>One answer to a card.</summary>
        public sealed class Reply
        {
            public string Key, Label, Description;
            /// <summary>What it moves between the player and the houseguest who came to them.</summary>
            public double ToThem;
            /// <summary>What it moves in the listener's view of the gossip, for gossip only.</summary>
            public double ToListener;
            /// <summary>Whether it promises a nominee the player's vote.</summary>
            public bool Promises;
            /// <summary>How far it could rebound, as the card says before it is chosen.</summary>
            public string Risk = HouseEventRisk.Low;
        }

        private static readonly Reply[] Confronting =
        {
            new Reply { Key = "apologize", Label = "Apologize", Description = "Take responsibility and try to mend things", ToThem = 10 },
            new Reply { Key = "deflect", Label = "Deflect", Description = "Redirect the conversation without admitting fault", ToThem = -2 },
            new Reply { Key = "escalate", Label = "Escalate", Description = "Stand your ground and push back hard", ToThem = -15, Risk = HouseEventRisk.High },
        };

        private static readonly Reply[] Gossiping =
        {
            new Reply { Key = "confront", Label = "Confront them", Description = "Show them you won't tolerate gossip", ToThem = -15, Risk = HouseEventRisk.High },
            new Reply { Key = "slide", Label = "Let it slide", Description = "Keep the peace, but remember", ToThem = -5 },
            new Reply { Key = "gossip-back", Label = "Gossip back", Description = "Fight fire with fire (risky)", ToThem = -10, ToListener = GossipBackReach, Risk = HouseEventRisk.High },
        };

        private static readonly Reply[] Pleading =
        {
            new Reply { Key = "promise", Label = "Promise support", Description = "Agree to help them — builds trust, and it is a promise", ToThem = 8, Promises = true, Risk = HouseEventRisk.Medium },
            new Reply { Key = "noncommittal", Label = "Stay noncommittal", Description = "Don't promise anything yet", ToThem = 0 },
            new Reply { Key = "refuse", Label = "Refuse", Description = "Tell them you can't help", ToThem = -5, Risk = HouseEventRisk.Medium },
        };

        /// <summary>The answers a card of this kind offers, in the reference's order.</summary>
        public static Reply[] Replies(string kind) =>
            kind == Confrontation ? Confronting : kind == Gossip ? Gossiping : kind == Plea ? Pleading : new Reply[0];

        /// <summary>An answer by its key, or null.</summary>
        public static Reply Find(string kind, string key) =>
            Replies(kind).FirstOrDefault(r => string.Equals(r.Key, key, StringComparison.Ordinal));

        /// <summary>
        /// Puts a card in front of the player, if this season plays them: one per houseguest per kind
        /// at a time, and never more than the save allows.
        /// </summary>
        public static void Offer(EpisodeState s, string kind, string fromId, string aboutId)
        {
            if (!StrategyRules.Apply(s) || !IsKnown(kind) || s.replyCards.Count >= 24) return;
            if (s.Find(s.playerId)?.status != ContestantStatus.Active) return;
            if (s.replyCards.Any(r => r.kind == kind && r.fromId == fromId)) return;
            string id = "reply-" + s.nextSequence;
            if (s.replyCards.Any(r => r.id == id)) return;
            s.replyCards.Add(new ReplyCardState { id = id, week = s.week, kind = kind, fromId = fromId, aboutId = aboutId });
        }

        /// <summary>The card waiting on the player, oldest first; null when there is none.</summary>
        public static ReplyCardState Pending(EpisodeState s) => s?.replyCards.FirstOrDefault();

        /// <summary>The card's heading.</summary>
        public static string Title(EpisodeState s, ReplyCardState card)
        {
            string who = s.Find(card.fromId)?.name ?? "Somebody";
            switch (card.kind)
            {
                case Confrontation: return who + " is confronting you!";
                case Gossip: return who + " has been talking about you";
                default: return who + " wants your support!";
            }
        }

        /// <summary>What they said, or what the player found out: the reference's lines.</summary>
        public static string Message(EpisodeState s, ReplyCardState card)
        {
            string who = s.Find(card.fromId)?.name ?? "Somebody";
            switch (card.kind)
            {
                case Confrontation:
                    return s.Score(card.fromId, s.playerId) < -40
                        ? "“I'm done pretending everything's fine between us. You've been playing me this whole time, and everyone can see it.”"
                        : "“We need to talk. I've been hearing things, and I need to know where we really stand in this game.”";
                case Gossip:
                    return "You just found out that " + who + " has been talking behind your back to "
                        + (s.Find(card.aboutId)?.name ?? "somebody") + "! How do you want to handle this?";
                default:
                    return s.Score(card.fromId, s.playerId) > 20
                        ? "“Look, I know we've been close. I need you to vote to keep me. Don't let them break us apart.”"
                        : "“I know we haven't always seen eye to eye, but keeping me is better for your game. Think about it.”";
            }
        }

        /// <summary>The note the relationship change carries.</summary>
        public static string Note(string kind, string label) =>
            (kind == Confrontation ? "Answered a confrontation: " : kind == Gossip ? "Answered gossip: " : "Answered a plea: ") + label;

        /// <summary>What the houseguest remembers of the player's answer.</summary>
        public static string Memory(string kind, string key, int week)
        {
            switch (key)
            {
                case "apologize": return "Apologized to me in week " + week + ".";
                case "deflect": return "Dodged me when I confronted them in week " + week + ".";
                case "escalate": return "Pushed back hard when I confronted them in week " + week + ".";
                case "confront": return "Confronted me about my gossip in week " + week + ".";
                case "slide": return "Let my gossip slide in week " + week + ".";
                case "gossip-back": return "Gossiped about me in return in week " + week + ".";
                case "promise": return "Promised me their support in week " + week + ".";
                case "noncommittal": return "Wouldn't commit when I asked for their vote in week " + week + ".";
                default: return "Told me they can't help me in week " + week + ".";
            }
        }

        /// <summary>The line the log keeps of the answer.</summary>
        public static string Outcome(EpisodeState s, ReplyCardState card, Reply reply)
        {
            string who = s.Find(card.fromId)?.name ?? "them";
            switch (reply.Key)
            {
                case "apologize": return "You apologized to " + who + ".";
                case "deflect": return "You deflected " + who + "'s confrontation.";
                case "escalate": return "You pushed back hard at " + who + ".";
                case "confront": return "You confronted " + who + " about the gossip.";
                case "slide": return "You let " + who + "'s gossip slide, for now.";
                case "gossip-back": return "You gave " + (s.Find(card.aboutId)?.name ?? "their listener") + " your side of " + who + ".";
                case "promise": return "You promised " + who + " your support.";
                case "noncommittal": return "You made " + who + " no promises.";
                default: return "You told " + who + " you can't help.";
            }
        }
    }

    /// <summary>A houseguest who came to the player and is waiting on an answer.</summary>
    [Serializable]
    public sealed class ReplyCardState
    {
        public string id;
        public int week;
        public string kind;
        /// <summary>Who came to the player.</summary>
        public string fromId;
        /// <summary>Who else it is about: the listener for gossip, the other nominee for a plea, else null.</summary>
        public string aboutId;
        public ReplyCardState Clone() => (ReplyCardState)MemberwiseClone();
    }
}
