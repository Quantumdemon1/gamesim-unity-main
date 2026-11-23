namespace Gamesim.Simulation
{
    /// <summary>
    /// Free talk, one All-Star template's own romance talks (slice S3, talk rules 3; ruling R1: the talks are C# tables). The
    /// talks builder fills <see cref="Rows"/>: rows of the romance kinds, Owner = <see cref="Owner"/>, each kind the twelve
    /// All-Star tables all hold. Game register only: the house, the competitions, juries, the season and their name as a player.
    /// Tame. American spelling.
    /// </summary>
    public static class TalkTableStarDerrickLevasseur
    {
        public const string Owner = "derrick-levasseur";

        public static readonly TalkRow[] Rows =
        {
            new TalkRow
            {
                Id = "derrick.rom.flirt.1", Owner = Owner, Kind = TalkKind.FlirtLine,
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"You're my favorite person to talk game with.\"",
                        Lands = "\"Same. You actually listen. That's rare.\"",
                        Even = "\"Noted.\"",
                        Grates = "\"Careful. Compliments get noticed.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "derrick.rom.flirt.2", Owner = Owner, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 15 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I'd pick you for every comp team.\"",
                        Lands = "\"I'd pick you too. You don't panic.\"",
                        Even = "\"Fair.\"",
                        Grates = "\"Teams change every week.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "derrick.rom.flirt.3", Owner = Owner, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 30 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I keep finding reasons to sit by you.\"",
                        Lands = "\"I noticed. I don't mind.\"",
                        Even = "\"Hm.\"",
                        Grates = "\"Sit where you want. People watch.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "derrick.rom.open.1", Owner = Owner, Kind = TalkKind.Opening,
                Open = "(a rare half smile) \"You keep finding me in the quiet rooms. I don't think that's an accident anymore.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"It isn't.\"",
                        Lands = "\"Good. I like knowing where I stand.\"",
                        Even = "\"Noted.\"",
                        Grates = "\"Then be careful who sees.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Is that a problem?\"",
                        Lands = "\"Not a problem. A pattern. I like this one.\"",
                        Even = "\"Not yet.\"",
                        Grates = "\"Could be, if the house notices.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I like the quiet with you.\"",
                        Lands = "\"Same. It's the only place I relax in here.\"",
                        Even = "\"It's nice.\"",
                        Grates = "\"Don't get comfortable.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. Good. I can count on friends in here.\"" },
                },
            },
            new TalkRow
            {
                Id = "derrick.rom.open.2", Owner = Owner, Kind = TalkKind.Opening, When = new TalkGate { Spark = 15 },
                Open = "(sliding a plate to you) \"I made extra. Don't read anything into it.\" (a beat) \"Read a little into it.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I'm reading a lot into it.\"",
                        Lands = "\"Fair. Your read's good.\"",
                        Even = "\"A little, then.\"",
                        Grates = "\"Too much.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Thanks. It's good.\"",
                        Lands = "\"You're welcome. Eat up before the comp.\"",
                        Even = "\"Sure.\"",
                        Grates = "\"It's just food.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Is this an interrogation?\"",
                        Lands = "\"If it was, you'd already have talked.\"",
                        Even = "\"Not yet.\"",
                        Grates = "\"Funny.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. Good. I can count on friends in here.\"" },
                },
            },
            new TalkRow
            {
                Id = "derrick.rom.open.3", Owner = Owner, Kind = TalkKind.Opening, When = new TalkGate { Spark = 30 },
                Open = "(low, steady) \"I trust almost no one in this house. You're the exception, and I don't make exceptions.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I'm glad I'm yours.\"",
                        Lands = "\"So am I. Let's not waste it.\"",
                        Even = "\"Good.\"",
                        Grates = "\"Keep that quiet.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I trust you too.\"",
                        Lands = "\"Then we've got something real in here.\"",
                        Even = "\"Good.\"",
                        Grates = "\"Careful saying that out loud.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Why me?\"",
                        Lands = "\"You don't perform. Everyone else does.\"",
                        Even = "\"Gut call.\"",
                        Grates = "\"That's mine to know.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. Good. I can count on friends in here.\"" },
                },
            },
            new TalkRow
            {
                Id = "derrick.rom.hold.1", Owner = Owner, Kind = TalkKind.HoldBack,
                Open = "(quietly, choosing every word) \"I'm not closing the door. But let's see where this game takes us before anything else.\"",
            },
            new TalkRow
            {
                Id = "derrick.rom.turn.1", Owner = Owner, Kind = TalkKind.TurnAside, Attempt = 1,
                Open = "(a calm, measuring look) \"I had a feeling that was coming. I watch everyone in here. Including you.\"",
                Second = "\"Here's where I stand. I'm here to play. Every hour I spend in here goes into the game. That doesn't change.\"",
                Replies = new[]
                {
                    new TalkReply { Choice = TalkChoice.LaughItOff, Line = "\"Fair enough. Can't blame me for trying.\"", Lands = "\"Good. Easy is good. Now that we've cleared that, let's talk about the vote.\"" },
                    new TalkReply { Choice = TalkChoice.PushAgain, Line = "\"Come on. Nobody's only here to play.\"", Lands = "\"I heard you. Pushing past a no gets noticed in here. People will read it, and so will the jury.\"" },
                    new TalkReply { Choice = TalkChoice.Apologize, Line = "\"Sorry. I read that wrong.\"", Lands = "\"Nothing to apologize for. You asked, I answered. That's respect. We're good.\"" },
                },
            },
            new TalkRow
            {
                Id = "derrick.rom.turn.2", Owner = Owner, Kind = TalkKind.TurnAside, Attempt = 2,
                Open = "(evenly, without looking up) \"Same answer as before. I'm here to play.\"",
            },
            new TalkRow
            {
                Id = "derrick.rom.turn.3", Owner = Owner, Kind = TalkKind.TurnAside, Attempt = 3,
                Open = "(calm, final) \"I've said it twice. I'm here to play. Let's keep our focus on the game.\"",
            },
            new TalkRow
            {
                Id = "derrick.rom.softno.1", Owner = Owner, Kind = TalkKind.SoftNo,
                Open = "(steady, kind) \"I like you. You're someone I trust. That's where it stays for me.\"",
            },
            new TalkRow
            {
                Id = "derrick.rom.declare.1", Owner = Owner, Kind = TalkKind.Declaration,
                Open = "(after the lights go out) \"I don't move until I'm sure. I'm sure. I want us in this as a pair, start to finish.\"",
                Replies = new[]
                {
                    new TalkReply { Choice = TalkChoice.Accept, Line = "\"I'm in.\"", Lands = "\"Good. Then nobody gets between us. Not at any vote.\"" },
                    new TalkReply { Choice = TalkChoice.NotYet, Line = "\"Not yet.\"", Lands = "\"Understood. I'll wait. I'm good at waiting.\"" },
                    new TalkReply { Choice = TalkChoice.NotLikeThat, Line = "\"I don't see it that way.\"", Lands = "\"Fair. I'd rather know. We're still good.\"" },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. That still means something from me.\"" },
                },
            },
            new TalkRow
            {
                Id = "derrick.rom.yes.1", Owner = Owner, Kind = TalkKind.Accepts,
                Open = "(a slow nod) \"Yes. I had it figured you'd ask. I was hoping I was right.\"",
            },
            new TalkRow
            {
                Id = "derrick.rom.no.1", Owner = Owner, Kind = TalkKind.Refuses,
                Open = "(calm) \"No. I'd rather tell you straight than let it cost us both at the vote.\"",
            },
            new TalkRow
            {
                Id = "derrick.rom.couple.1", Owner = Owner, Kind = TalkKind.Couple,
                Open = "(quietly, at the table) \"We're a target now. Fine. I'd rather be a target with you than safe with anyone else.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Same here.\"",
                        Lands = "\"Then let's play it smart.\"",
                        Even = "\"Good.\"",
                        Grates = "\"Keep it quiet.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Let's keep it low-key this week.\"",
                        Lands = "\"Agreed. Less to read.\"",
                        Even = "\"Fine.\"",
                        Grates = "\"I'm always low-key.\"",
                    },
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"I think we should end this. Kindly.\"", Lands = "\"All right. Thank you for being straight. We're still good.\"" },
                },
            },
            new TalkRow
            {
                Id = "derrick.rom.ex.1", Owner = Owner, Kind = TalkKind.Ex,
                Open = "(even, polite) \"We're fine. I just need some distance to think straight. It's nothing personal.\"",
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
                        Grates = "\"I'm good.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I still want us to be friends.\"",
                        Lands = "\"I'd like that.\"",
                        Even = "\"In time.\"",
                        Grates = "\"Not yet.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "derrick.rom.jealous.1", Owner = Owner, Kind = TalkKind.Jealousy,
                Open = "(calm, in the hallway) \"I hear things. Today I heard you've been working someone else. I'd like your version.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"It wasn't what it looked like.\"",
                        Lands = "\"Then walk me through it.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"It read like that.\"",
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
                        Lands = "\"Maybe. I still check sources.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"Don't deflect.\"",
                    },
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"Maybe we should end this. Kindly.\"", Lands = "\"Understood. Thank you for saying it straight. We're still good.\"" },
                },
            },
            new TalkRow
            {
                Id = "derrick.rom.react1.1", Owner = Owner, Kind = TalkKind.Reaction, Reaction = 1,
                Open = "(evenly, at the table) \"You two aren't subtle. I noticed on day two. Others are catching up.\"",
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
                        Lands = "\"More than you think.\"",
                        Even = "\"Most.\"",
                        Grates = "\"All of them.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "derrick.rom.react2.1", Owner = Owner, Kind = TalkKind.Reaction, Reaction = 2,
                Open = "(low, steady) \"Careful. A pair is a target. Two votes together is the first thing a smart player breaks.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I know. We're ready for it.\"",
                        Lands = "\"Good. Stay sharp.\"",
                        Even = "\"Hope so.\"",
                        Grates = "\"Nobody is.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who's coming for us?\"",
                        Lands = "\"Watch the quiet ones.\"",
                        Even = "\"Can't say.\"",
                        Grates = "\"Not yet.\"",
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
