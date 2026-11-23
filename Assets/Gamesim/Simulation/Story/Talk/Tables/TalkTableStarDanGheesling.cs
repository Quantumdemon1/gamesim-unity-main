namespace Gamesim.Simulation
{
    /// <summary>
    /// Free talk, one All-Star template's own romance talks (slice S3, talk rules 3; ruling R1: the talks are C# tables). The
    /// talks builder fills <see cref="Rows"/>: rows of the romance kinds, Owner = <see cref="Owner"/>, each kind the twelve
    /// All-Star tables all hold. Game register only: the house, the competitions, juries, the season and their name as a player.
    /// Tame. American spelling.
    /// </summary>
    public static class TalkTableStarDanGheesling
    {
        public const string Owner = "dan-gheesling";

        public static readonly TalkRow[] Rows =
        {
            new TalkRow
            {
                Id = "dan.rom.flirt.1", Owner = Owner, Kind = TalkKind.FlirtLine,
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"You're my favorite person to talk game with.\"",
                        Lands = "\"Good. I like talking game with you too. More than I should.\"",
                        Even = "\"Noted.\"",
                        Grates = "\"Careful. Flattery is a move.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "dan.rom.flirt.2", Owner = Owner, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 15 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I'd pick you for every comp team.\"",
                        Lands = "\"And I'd pick you. That's a real compliment from me.\"",
                        Even = "\"Fair.\"",
                        Grates = "\"Teams change every week.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "dan.rom.flirt.3", Owner = Owner, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 30 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I keep finding reasons to sit by you.\"",
                        Lands = "\"I noticed. I don't mind the reasons.\"",
                        Even = "\"Hm.\"",
                        Grates = "\"Sit anywhere. It's still the game.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "dan.rom.open.1", Owner = Owner, Kind = TalkKind.Opening,
                Open = "(a slow grin) \"You're working an angle on me. I know because I'd do the same. Go on.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"No angle. I just like your company.\"",
                        Lands = "\"Everyone has an angle. I'll let yours slide. This once.\"",
                        Even = "\"Noted. Carefully noted.\"",
                        Grates = "\"Now I know you've got an angle.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Takes one to know one.\"",
                        Lands = "\"Fair. We're a dangerous pair, then.\"",
                        Even = "\"Maybe. Maybe not.\"",
                        Grates = "\"Careful. I play this better.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I trust you. That's all.\"",
                        Lands = "\"That's a lot, in here. I won't waste it.\"",
                        Even = "\"Trust is expensive. Spend it slowly.\"",
                        Grates = "\"Don't. Not yet.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Smart call. Friends in here last longer than anything else.\"" },
                },
            },
            new TalkRow
            {
                Id = "dan.rom.open.2", Owner = Owner, Kind = TalkKind.Opening, When = new TalkGate { Spark = 15 },
                Open = "(setting two cups down, one for you) \"I keep saving you a seat at the strategy table. That's not nothing.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I'll take the seat. And the company.\"",
                        Lands = "\"Good. Both come with the job.\"",
                        Even = "\"Take the seat first.\"",
                        Grates = "\"Easy. It's still the game.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"What does the seat cost?\"",
                        Lands = "\"Loyalty. You've already paid some.\"",
                        Even = "\"Nothing yet. Later, maybe.\"",
                        Grates = "\"Everything costs something.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Is that your idea of sweet?\"",
                        Lands = "\"In this house, it's the sweetest thing I've got.\"",
                        Even = "\"Close enough.\"",
                        Grates = "\"Don't get comfortable.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Smart call. Friends in here last longer than anything else.\"" },
                },
            },
            new TalkRow
            {
                Id = "dan.rom.open.3", Owner = Owner, Kind = TalkKind.Opening, When = new TalkGate { Spark = 30 },
                Open = "(quiet, by the memory wall) \"I've run a lot of games in here. I wasn't counting on running one with you beside me.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"Then let's run it together.\"",
                        Lands = "\"Together. Final two. I like how that sounds.\"",
                        Even = "\"We'll see how far it goes.\"",
                        Grates = "\"Don't get ahead of the game.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'm glad I'm beside you.\"",
                        Lands = "\"So am I. Don't tell anybody.\"",
                        Even = "\"Me too. Mostly.\"",
                        Grates = "\"Let's keep our heads.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Is that a risk for you?\"",
                        Lands = "\"Everything is. You're worth it, I think.\"",
                        Even = "\"Maybe. I'm weighing it.\"",
                        Grates = "\"Ask me after the vote.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Smart call. Friends in here last longer than anything else.\"" },
                },
            },
            new TalkRow
            {
                Id = "dan.rom.hold.1", Owner = Owner, Kind = TalkKind.HoldBack,
                Open = "(a long look, weighing it) \"I'm not saying no. I'm saying let's see where this game takes us first.\"",
            },
            new TalkRow
            {
                Id = "dan.rom.turn.1", Owner = Owner, Kind = TalkKind.TurnAside, Attempt = 1,
                Open = "(raising an eyebrow, half a smile) \"Huh. Didn't see that move coming. And I see most moves coming.\"",
                Second = "\"But I'm here to play. Every conversation I have in this house is about the game. This one too, now.\"",
                Replies = new[]
                {
                    new TalkReply { Choice = TalkChoice.LaughItOff, Line = "\"Fair enough. Can't blame me for trying.\"", Lands = "\"Good. That I can work with. Sit down. Let's talk about who's really running this week.\"" },
                    new TalkReply { Choice = TalkChoice.PushAgain, Line = "\"Come on. Nobody's only here to play.\"", Lands = "\"I heard you the first time. Pushing a no is how people end up on the block. Think about that.\"" },
                    new TalkReply { Choice = TalkChoice.Apologize, Line = "\"Sorry. I read that wrong.\"", Lands = "\"No apology needed. You read the room, the room said no. That's the game. We're fine.\"" },
                },
            },
            new TalkRow
            {
                Id = "dan.rom.turn.2", Owner = Owner, Kind = TalkKind.TurnAside, Attempt = 2,
                Open = "(not looking up from the puzzle) \"Same answer. I'm here to play. Your move, though. I'll give you that.\"",
            },
            new TalkRow
            {
                Id = "dan.rom.turn.3", Owner = Owner, Kind = TalkKind.TurnAside, Attempt = 3,
                Open = "(evenly) \"Last time I'll say it. I'm here to play. Let's put that energy into the vote instead.\"",
            },
            new TalkRow
            {
                Id = "dan.rom.softno.1", Owner = Owner, Kind = TalkKind.SoftNo,
                Open = "(kindly, but certain) \"I like you. I'm keeping you as an ally. That's as far as I go.\"",
            },
            new TalkRow
            {
                Id = "dan.rom.declare.1", Owner = Owner, Kind = TalkKind.Declaration,
                Open = "(after the house is asleep) \"I don't make moves I haven't thought through. I've thought this one through. You and me, all the way.\"",
                Replies = new[]
                {
                    new TalkReply { Choice = TalkChoice.Accept, Line = "\"All the way.\"", Lands = "\"Then nobody splits us up. Not this week, not any week.\"" },
                    new TalkReply { Choice = TalkChoice.NotYet, Line = "\"Not yet. Give me time.\"", Lands = "\"Fair. Timing is everything in here. I can wait.\"" },
                    new TalkReply { Choice = TalkChoice.NotLikeThat, Line = "\"I don't see us like that.\"", Lands = "\"Understood. No hard feelings. We still play smart.\"" },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends and allies. That's still a lot from me.\"" },
                },
            },
            new TalkRow
            {
                Id = "dan.rom.yes.1", Owner = Owner, Kind = TalkKind.Accepts,
                Open = "(a rare, real smile) \"Yes. I had a whole plan for asking you. You just ruined it. Good.\"",
            },
            new TalkRow
            {
                Id = "dan.rom.no.1", Owner = Owner, Kind = TalkKind.Refuses,
                Open = "(gently) \"No. You're one of my best allies, and I want to keep it that way.\"",
            },
            new TalkRow
            {
                Id = "dan.rom.couple.1", Owner = Owner, Kind = TalkKind.Couple,
                Open = "(low, at the kitchen island) \"We're a target now, both of us. I don't mind. We'll just have to win more.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Then we win more.\"",
                        Lands = "\"That's the spirit. Final two or bust.\"",
                        Even = "\"We will.\"",
                        Grates = "\"Don't get cocky.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Let's keep it low-key this week.\"",
                        Lands = "\"Smart. Nobody needs to see our hand.\"",
                        Even = "\"Fair.\"",
                        Grates = "\"Low-key is how I play anyway.\"",
                    },
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"I think we should step back. Kindly.\"", Lands = "\"All right. We stay allies. I'll still have your back at the vote.\"" },
                },
            },
            new TalkRow
            {
                Id = "dan.rom.ex.1", Owner = Owner, Kind = TalkKind.Ex,
                Open = "(polite, measured) \"We're good. I mean it. I just play better with a little distance. Give it a week.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I hope we're okay.\"",
                        Lands = "\"We are. Give it a week.\"",
                        Even = "\"We will be.\"",
                        Grates = "\"Not now.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Take all the time you need.\"",
                        Lands = "\"I will. Thanks.\"",
                        Even = "\"Okay.\"",
                        Grates = "\"I don't need much.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I still want us to be friends.\"",
                        Lands = "\"Allies. Always.\"",
                        Even = "\"Sure.\"",
                        Grates = "\"Let's see.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "dan.rom.jealous.1", Owner = Owner, Kind = TalkKind.Jealousy,
                Open = "(quietly, by the storage room) \"Word travels. I heard you've been working someone else very hard. Talk to me.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"It wasn't what it looked like.\"",
                        Lands = "\"Then tell me what it was.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"It looked like a move.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You're the one I want. I mean it.\"",
                        Lands = "\"Okay. I believe you. This time.\"",
                        Even = "\"We'll see.\"",
                        Grates = "\"Words are cheap in here.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You're listening to gossip.\"",
                        Lands = "\"Maybe. Gossip is still information.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"Don't dodge.\"",
                    },
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"Maybe we should end this. Kindly.\"", Lands = "\"All right. If that's your call. We stay allies at the vote.\"" },
                },
            },
            new TalkRow
            {
                Id = "dan.rom.react1.1", Owner = Owner, Kind = TalkKind.Reaction, Reaction = 1,
                Open = "(leaning on the counter) \"You two aren't subtle. Every strategist in here has already done the math.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Subtle was never the plan.\"",
                        Lands = "\"Clearly. Just know it shows.\"",
                        Even = "\"Fair.\"",
                        Grates = "\"It's a liability.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"We're happy. Is that so bad?\"",
                        Lands = "\"Not bad. Just visible.\"",
                        Even = "\"It's fine.\"",
                        Grates = "\"For your game, yes.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who else has noticed?\"",
                        Lands = "\"Everyone who matters.\"",
                        Even = "\"Most.\"",
                        Grates = "\"Everyone.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "dan.rom.react2.1", Owner = Owner, Kind = TalkKind.Reaction, Reaction = 2,
                Open = "(low) \"Careful. A pair is a target. Two votes on one side is the first thing I'd break up.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I know. We're ready for it.\"",
                        Lands = "\"Good. Then have a plan.\"",
                        Even = "\"Hope so.\"",
                        Grates = "\"Nobody's ready.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who's coming for us?\"",
                        Lands = "\"I'm not saying. Yet.\"",
                        Even = "\"Can't say.\"",
                        Grates = "\"Not you, if you're lucky.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Thanks for the warning.\"",
                        Lands = "\"Free advice. Rare from me.\"",
                        Even = "\"Sure.\"",
                        Grates = "\"Use it.\"",
                    },
                },
            },
        };
    }
}
