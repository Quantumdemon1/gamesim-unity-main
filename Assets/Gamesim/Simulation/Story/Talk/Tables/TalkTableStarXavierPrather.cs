namespace Gamesim.Simulation
{
    /// <summary>
    /// Free talk, one All-Star template's own romance talks (slice S3, talk rules 3; ruling R1: the talks are C# tables). The
    /// talks builder fills <see cref="Rows"/>: rows of the romance kinds, Owner = <see cref="Owner"/>, each kind the twelve
    /// All-Star tables all hold. Game register only: the house, the competitions, juries, the season and their name as a player.
    /// Tame. American spelling.
    /// </summary>
    public static class TalkTableStarXavierPrather
    {
        public const string Owner = "xavier-prather";

        public static readonly TalkRow[] Rows =
        {
            new TalkRow
            {
                Id = "xavier.rom.flirt.1", Owner = Owner, Kind = TalkKind.FlirtLine,
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"You're my favorite person to talk game with.\"",
                        Lands = "\"Same. You think before you speak.\"",
                        Even = "\"Noted.\"",
                        Grates = "\"Careful. People notice.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "xavier.rom.flirt.2", Owner = Owner, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 15 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I'd pick you for every comp team.\"",
                        Lands = "\"I'd pick you too. Without question.\"",
                        Even = "\"Fair.\"",
                        Grates = "\"Teams change.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "xavier.rom.flirt.3", Owner = Owner, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 30 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I keep finding reasons to sit by you.\"",
                        Lands = "\"I noticed. I don't mind.\"",
                        Even = "\"Hm.\"",
                        Grates = "\"Sit where you like. Stay focused.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "xavier.rom.open.1", Owner = Owner, Kind = TalkKind.Opening,
                Open = "(a quiet smile) \"You're easy to talk to. In this house, that's worth more than a comp win.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"So are you. More than easy.\"",
                        Lands = "\"I'll take that. Gladly.\"",
                        Even = "\"Good.\"",
                        Grates = "\"Let's not get ahead of ourselves.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I like our talks.\"",
                        Lands = "\"Me too. They keep me steady.\"",
                        Even = "\"Same.\"",
                        Grates = "\"Good.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"More than a comp win? Bold.\"",
                        Lands = "\"I don't say it lightly.\"",
                        Even = "\"Close to it.\"",
                        Grates = "\"Maybe not.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. I value that. I mean it.\"" },
                },
            },
            new TalkRow
            {
                Id = "xavier.rom.open.2", Owner = Owner, Kind = TalkKind.Opening, When = new TalkGate { Spark = 15 },
                Open = "(saving you a seat at the table) \"I keep making room for you. I've stopped pretending it's an accident.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I hope you never stop.\"",
                        Lands = "\"Then I won't.\"",
                        Even = "\"We'll see.\"",
                        Grates = "\"Careful.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Thank you.\"",
                        Lands = "\"Always.\"",
                        Even = "\"Sure.\"",
                        Grates = "\"It's just a seat.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"People will notice.\"",
                        Lands = "\"Let them. I don't mind.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"You're right. Maybe I should stop.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. I value that. I mean it.\"" },
                },
            },
            new TalkRow
            {
                Id = "xavier.rom.open.3", Owner = Owner, Kind = TalkKind.Opening, When = new TalkGate { Spark = 30 },
                Open = "(low, sincere) \"I'd go to the end of this game with you. I don't say that to anyone.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I'd go with you.\"",
                        Lands = "\"Then let's get there. Together.\"",
                        Even = "\"Good.\"",
                        Grates = "\"One week at a time.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"That means a lot.\"",
                        Lands = "\"It should. I mean it.\"",
                        Even = "\"It's true.\"",
                        Grates = "\"Good.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"That's a long road.\"",
                        Lands = "\"It is. I'd rather walk it with you.\"",
                        Even = "\"It is.\"",
                        Grates = "\"Let's see.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. I value that. I mean it.\"" },
                },
            },
            new TalkRow
            {
                Id = "xavier.rom.hold.1", Owner = Owner, Kind = TalkKind.HoldBack,
                Open = "(measured, thoughtful) \"I won't say no. I'm saying let's see where this game takes us. Then we'll know.\"",
            },
            new TalkRow
            {
                Id = "xavier.rom.turn.1", Owner = Owner, Kind = TalkKind.TurnAside, Attempt = 1,
                Open = "(a calm, amused smile) \"Well. That's a new one. I appreciate the honesty.\"",
                Second = "\"Here's where I am. I'm here to play. I came in to see this game through to the end, and that's where my focus stays.\"",
                Replies = new[]
                {
                    new TalkReply { Choice = TalkChoice.LaughItOff, Line = "\"Fair enough. Can't blame me for trying.\"", Lands = "\"Good. I like how you handle things. Let's talk about the week.\"" },
                    new TalkReply { Choice = TalkChoice.PushAgain, Line = "\"Come on. Nobody's only here to play.\"", Lands = "\"I heard you. A no is a no. Pushing it only makes people wonder about you.\"" },
                    new TalkReply { Choice = TalkChoice.Apologize, Line = "\"Sorry. I read that wrong.\"", Lands = "\"No apology needed. You asked respectfully. I respect that.\"" },
                },
            },
            new TalkRow
            {
                Id = "xavier.rom.turn.2", Owner = Owner, Kind = TalkKind.TurnAside, Attempt = 2,
                Open = "(evenly) \"Same answer. Here to play.\"",
            },
            new TalkRow
            {
                Id = "xavier.rom.turn.3", Owner = Owner, Kind = TalkKind.TurnAside, Attempt = 3,
                Open = "(calm, final) \"I'll say it one last time. I'm here to play. Let's leave it with respect.\"",
            },
            new TalkRow
            {
                Id = "xavier.rom.softno.1", Owner = Owner, Kind = TalkKind.SoftNo,
                Open = "(kind, steady) \"I value you. I do. You're my ally. That's where it stays.\"",
            },
            new TalkRow
            {
                Id = "xavier.rom.declare.1", Owner = Owner, Kind = TalkKind.Declaration,
                Open = "(after the house is asleep) \"I don't move until I'm certain. I'm certain. I want us as a pair, to the end.\"",
                Replies = new[]
                {
                    new TalkReply { Choice = TalkChoice.Accept, Line = "\"I want that too.\"", Lands = "\"Then nobody splits us. Not at any vote.\"" },
                    new TalkReply { Choice = TalkChoice.NotYet, Line = "\"Not yet.\"", Lands = "\"Understood. I'll wait.\"" },
                    new TalkReply { Choice = TalkChoice.NotLikeThat, Line = "\"I don't feel that way.\"", Lands = "\"Thank you for telling me plainly.\"" },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. That still matters to me.\"" },
                },
            },
            new TalkRow
            {
                Id = "xavier.rom.yes.1", Owner = Owner, Kind = TalkKind.Accepts,
                Open = "(a quiet, real smile) \"Yes. I was hoping you'd ask.\"",
            },
            new TalkRow
            {
                Id = "xavier.rom.no.1", Owner = Owner, Kind = TalkKind.Refuses,
                Open = "(gently) \"No. I'd rather be honest than risk what we have as allies.\"",
            },
            new TalkRow
            {
                Id = "xavier.rom.couple.1", Owner = Owner, Kind = TalkKind.Couple,
                Open = "(on the backyard bench) \"We're a target now. That's fine. We'll play it calm and steady.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Calm and steady.\"",
                        Lands = "\"That's how we get to the end.\"",
                        Even = "\"Good.\"",
                        Grates = "\"Let's stay calm.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Let's keep it low-key this week.\"",
                        Lands = "\"Sensible. Calm and quiet.\"",
                        Even = "\"Fine.\"",
                        Grates = "\"I'm always low-key.\"",
                    },
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"I think we should go back to being allies.\"", Lands = "\"All right. Thank you for saying it kindly. We're still good.\"" },
                },
            },
            new TalkRow
            {
                Id = "xavier.rom.ex.1", Owner = Owner, Kind = TalkKind.Ex,
                Open = "(calm) \"We're good. I just need a few days to settle. Nothing to worry about.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I hope we're okay.\"",
                        Lands = "\"We're fine.\"",
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
                        Lands = "\"Allies. I'd like that.\"",
                        Even = "\"In time.\"",
                        Grates = "\"Not yet.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "xavier.rom.jealous.1", Owner = Owner, Kind = TalkKind.Jealousy,
                Open = "(calm, by the door) \"I heard you've been working someone else closely. I'd like to hear it from you.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"It wasn't what it looked like.\"",
                        Lands = "\"Then help me understand it.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"It looked like that.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You're the one I want. I mean it.\"",
                        Lands = "\"All right. I believe you.\"",
                        Even = "\"We'll see.\"",
                        Grates = "\"Words.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You're listening to gossip.\"",
                        Lands = "\"Perhaps. I still had to ask.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"Don't deflect.\"",
                    },
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"Maybe we should end this. Kindly.\"", Lands = "\"All right. Thank you for saying it kindly. We're still good.\"" },
                },
            },
            new TalkRow
            {
                Id = "xavier.rom.react1.1", Owner = Owner, Kind = TalkKind.Reaction, Reaction = 1,
                Open = "(a calm smile) \"You two aren't subtle. I'm only saying what the whole table already knows.\"",
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
                        Lands = "\"No. Just visible.\"",
                        Even = "\"No.\"",
                        Grates = "\"For your game, yes.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who else has noticed?\"",
                        Lands = "\"The whole table.\"",
                        Even = "\"Most.\"",
                        Grates = "\"Everyone.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "xavier.rom.react2.1", Owner = Owner, Kind = TalkKind.Reaction, Reaction = 2,
                Open = "(measured) \"Careful. A pair is a target. Hold your alliances together, or someone else will break them.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I know. We're ready for it.\"",
                        Lands = "\"Good. Stay steady.\"",
                        Even = "\"Hope so.\"",
                        Grates = "\"Nobody is.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who's coming for us?\"",
                        Lands = "\"I won't say. Watch the votes.\"",
                        Even = "\"Can't say.\"",
                        Grates = "\"Not telling.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Thanks for the warning.\"",
                        Lands = "\"Anytime.\"",
                        Even = "\"Sure.\"",
                        Grates = "\"Remember it.\"",
                    },
                },
            },
        };
    }
}
