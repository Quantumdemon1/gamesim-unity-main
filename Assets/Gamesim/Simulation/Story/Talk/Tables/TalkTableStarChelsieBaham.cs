namespace Gamesim.Simulation
{
    /// <summary>
    /// Free talk, one All-Star template's own romance talks (slice S3, talk rules 3; ruling R1: the talks are C# tables). The
    /// talks builder fills <see cref="Rows"/>: rows of the romance kinds, Owner = <see cref="Owner"/>, each kind the twelve
    /// All-Star tables all hold. Game register only: the house, the competitions, juries, the season and their name as a player.
    /// Tame. American spelling.
    /// </summary>
    public static class TalkTableStarChelsieBaham
    {
        public const string Owner = "chelsie-baham";

        public static readonly TalkRow[] Rows =
        {
            new TalkRow
            {
                Id = "chelsie.rom.flirt.1", Owner = Owner, Kind = TalkKind.FlirtLine,
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"You're my favorite person to talk game with.\"",
                        Lands = "\"Same. You actually get it.\"",
                        Even = "\"Okay.\"",
                        Grates = "\"Careful. People notice.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "chelsie.rom.flirt.2", Owner = Owner, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 15 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I'd pick you for every comp team.\"",
                        Lands = "\"I'd pick you too. Every time.\"",
                        Even = "\"Okay, deal.\"",
                        Grates = "\"Teams change.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "chelsie.rom.flirt.3", Owner = Owner, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 30 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I keep finding reasons to sit by you.\"",
                        Lands = "\"I noticed. I don't mind.\"",
                        Even = "\"Okay.\"",
                        Grates = "\"Sit anywhere. I'm working.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "chelsie.rom.open.1", Owner = Owner, Kind = TalkKind.Opening,
                Open = "(sitting close at the counter) \"You know, everyone in here talks to me. You're the one I actually want to talk to.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"Then talk to me more.\"",
                        Lands = "\"I will. Don't make me regret it.\"",
                        Even = "\"Okay. Maybe.\"",
                        Grates = "\"Slow down.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I like talking to you too.\"",
                        Lands = "\"Good. That makes this easy.\"",
                        Even = "\"Okay.\"",
                        Grates = "\"We'll see.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"I'm that interesting?\"",
                        Lands = "\"Honestly? Yes. A little.\"",
                        Even = "\"Sometimes.\"",
                        Grates = "\"Don't get cocky.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. Good. I like having you on my side.\"" },
                },
            },
            new TalkRow
            {
                Id = "chelsie.rom.open.2", Owner = Owner, Kind = TalkKind.Opening, When = new TalkGate { Spark = 15 },
                Open = "(after the vote) \"I trust a lot of people a little. I trust you a lot. That's new for me.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I trust you a lot too.\"",
                        Lands = "\"Good. Then we're in this together.\"",
                        Even = "\"Okay.\"",
                        Grates = "\"Careful saying that.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I won't let you down.\"",
                        Lands = "\"I know. That's why.\"",
                        Even = "\"Good.\"",
                        Grates = "\"We'll see.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"That's a big risk.\"",
                        Lands = "\"It is. I think you're worth it.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"Don't make me rethink it.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. Good. I like having you on my side.\"" },
                },
            },
            new TalkRow
            {
                Id = "chelsie.rom.open.3", Owner = Owner, Kind = TalkKind.Opening, When = new TalkGate { Spark = 30 },
                Open = "(quietly in the yard) \"If I'm being honest, you're the person I check on first after every eviction.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I check on you first too.\"",
                        Lands = "\"Okay. I like that. A lot.\"",
                        Even = "\"Good.\"",
                        Grates = "\"Don't make it a thing.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'm always glad you do.\"",
                        Lands = "\"Me too. Always.\"",
                        Even = "\"Okay.\"",
                        Grates = "\"It's just checking.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Not your allies first?\"",
                        Lands = "\"You are my allies first.\"",
                        Even = "\"Usually you.\"",
                        Grates = "\"Okay, forget it.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. Good. I like having you on my side.\"" },
                },
            },
            new TalkRow
            {
                Id = "chelsie.rom.hold.1", Owner = Owner, Kind = TalkKind.HoldBack,
                Open = "(thinking it over) \"I'm not going to say no. But let's see where this game takes us first. Okay?\"",
            },
            new TalkRow
            {
                Id = "chelsie.rom.turn.1", Owner = Owner, Kind = TalkKind.TurnAside, Attempt = 1,
                Open = "(a surprised, warm laugh) \"Oh! Okay. That's sweet. You caught me off guard, and nobody does that.\"",
                Second = "\"But I'm here to play. I came in here to win, and I have to keep watch on every side of this house.\"",
                Replies = new[]
                {
                    new TalkReply { Choice = TalkChoice.LaughItOff, Line = "\"Fair enough. Can't blame me for trying.\"", Lands = "\"Ha, okay, good. I like you more now, honestly. Let's talk about the week.\"" },
                    new TalkReply { Choice = TalkChoice.PushAgain, Line = "\"Come on. Nobody's only here to play.\"", Lands = "\"I said no. And if I can see you pushing, so can everyone else in this house. Be careful.\"" },
                    new TalkReply { Choice = TalkChoice.Apologize, Line = "\"Sorry. I read that wrong.\"", Lands = "\"Don't apologize. You were honest with me. I'll remember that.\"" },
                },
            },
            new TalkRow
            {
                Id = "chelsie.rom.turn.2", Owner = Owner, Kind = TalkKind.TurnAside, Attempt = 2,
                Open = "(sitting on the counter) \"Same answer as before. I'm here to play.\"",
            },
            new TalkRow
            {
                Id = "chelsie.rom.turn.3", Owner = Owner, Kind = TalkKind.TurnAside, Attempt = 3,
                Open = "(warm, but done) \"Okay, last time. I'm here to play. I'd rather have you as an ally. Deal?\"",
            },
            new TalkRow
            {
                Id = "chelsie.rom.softno.1", Owner = Owner, Kind = TalkKind.SoftNo,
                Open = "(kindly) \"You're one of my favorite people to play with. Truly. But that's all it is for me. Friends?\"",
            },
            new TalkRow
            {
                Id = "chelsie.rom.declare.1", Owner = Owner, Kind = TalkKind.Declaration,
                Open = "(sitting on the edge of the pool) \"Okay. I've gone back and forth. I want us to be a pair in this game. All the way.\"",
                Replies = new[]
                {
                    new TalkReply { Choice = TalkChoice.Accept, Line = "\"Yes.\"", Lands = "\"Yes? Okay. Okay! Let's be smart about it.\"" },
                    new TalkReply { Choice = TalkChoice.NotYet, Line = "\"Not yet.\"", Lands = "\"Okay. I can wait. I'm patient when it counts.\"" },
                    new TalkReply { Choice = TalkChoice.NotLikeThat, Line = "\"I don't feel the same.\"", Lands = "\"Okay. I'd rather know. Thank you.\"" },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. That still means a lot to me.\"" },
                },
            },
            new TalkRow
            {
                Id = "chelsie.rom.yes.1", Owner = Owner, Kind = TalkKind.Accepts,
                Open = "(a big, surprised smile) \"Yes. I wasn't sure you'd ask. I'm so glad you did.\"",
            },
            new TalkRow
            {
                Id = "chelsie.rom.no.1", Owner = Owner, Kind = TalkKind.Refuses,
                Open = "(softly) \"No. I really care about you, but we're better as allies. Allies?\"",
            },
            new TalkRow
            {
                Id = "chelsie.rom.couple.1", Owner = Owner, Kind = TalkKind.Couple,
                Open = "(on the backyard couch) \"We're a target now. Both of us. I can work with that. I always can.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"We'll make it work.\"",
                        Lands = "\"We will. Every side of this house.\"",
                        Even = "\"We will.\"",
                        Grates = "\"Don't get comfortable.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Let's keep it low-key this week.\"",
                        Lands = "\"Smart. Less for everyone to see.\"",
                        Even = "\"Okay.\"",
                        Grates = "\"Why? What did you hear?\"",
                    },
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"I think we should go back to being allies.\"", Lands = "\"Okay. That's hard. Thank you for being kind about it. We're still good.\"" },
                },
            },
            new TalkRow
            {
                Id = "chelsie.rom.ex.1", Owner = Owner, Kind = TalkKind.Ex,
                Open = "(composed, kind) \"We're good. I promise. I just need a little space to keep my head straight.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I hope we're okay.\"",
                        Lands = "\"We're good. Promise.\"",
                        Even = "\"We will be.\"",
                        Grates = "\"Not now.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Take all the time you need.\"",
                        Lands = "\"Thank you.\"",
                        Even = "\"Okay.\"",
                        Grates = "\"I'm fine.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I still want us to be friends.\"",
                        Lands = "\"Allies. Yes.\"",
                        Even = "\"Sure.\"",
                        Grates = "\"Later.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "chelsie.rom.jealous.1", Owner = Owner, Kind = TalkKind.Jealousy,
                Open = "(quietly, in the kitchen) \"I hear everything in this house. Today I heard about you and someone else. Talk to me.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"It wasn't what it looked like.\"",
                        Lands = "\"Okay. Then what was it?\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"It looked like something.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You're the one I want. I mean it.\"",
                        Lands = "\"Okay. I believe you.\"",
                        Even = "\"We'll see.\"",
                        Grates = "\"Show me.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You're listening to gossip.\"",
                        Lands = "\"Maybe. I still had to ask.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"Don't dodge.\"",
                    },
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"Maybe we should end this. Kindly.\"", Lands = "\"Okay. That's hard. Thank you for being kind about it.\"" },
                },
            },
            new TalkRow
            {
                Id = "chelsie.rom.react1.1", Owner = Owner, Kind = TalkKind.Reaction, Reaction = 1,
                Open = "(smiling) \"You two aren't subtle. Every side of this house is talking about it.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Subtle was never the plan.\"",
                        Lands = "\"Clearly.\"",
                        Even = "\"Fair.\"",
                        Grates = "\"It shows.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"We're happy. Is that so bad?\"",
                        Lands = "\"No. Good for you.\"",
                        Even = "\"It's fine.\"",
                        Grates = "\"For your game, maybe.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who else has noticed?\"",
                        Lands = "\"Every side.\"",
                        Even = "\"Most.\"",
                        Grates = "\"Everyone.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "chelsie.rom.react2.1", Owner = Owner, Kind = TalkKind.Reaction, Reaction = 2,
                Open = "(low) \"Careful. A pair is a target. Keep a friend on every side.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I know. We're ready for it.\"",
                        Lands = "\"Good. Stay smart.\"",
                        Even = "\"Hope so.\"",
                        Grates = "\"Nobody is.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who's coming for us?\"",
                        Lands = "\"I have a guess. Not yet.\"",
                        Even = "\"Can't say.\"",
                        Grates = "\"Not telling.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Thanks for the warning.\"",
                        Lands = "\"Anytime.\"",
                        Even = "\"Sure.\"",
                        Grates = "\"Use it.\"",
                    },
                },
            },
        };
    }
}
