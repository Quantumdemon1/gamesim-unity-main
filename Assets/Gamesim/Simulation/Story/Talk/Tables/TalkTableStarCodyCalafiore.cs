namespace Gamesim.Simulation
{
    /// <summary>
    /// Free talk, one All-Star template's own romance talks (slice S3, talk rules 3; ruling R1: the talks are C# tables). The
    /// talks builder fills <see cref="Rows"/>: rows of the romance kinds, Owner = <see cref="Owner"/>, each kind the twelve
    /// All-Star tables all hold. Game register only: the house, the competitions, juries, the season and their name as a player.
    /// Tame. American spelling.
    /// </summary>
    public static class TalkTableStarCodyCalafiore
    {
        public const string Owner = "cody-calafiore";

        public static readonly TalkRow[] Rows =
        {
            new TalkRow
            {
                Id = "cody.rom.flirt.1", Owner = Owner, Kind = TalkKind.FlirtLine,
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"You're my favorite person to talk game with.\"",
                        Lands = "\"Same. You get how I think.\"",
                        Even = "\"Cool.\"",
                        Grates = "\"Careful. People will notice.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "cody.rom.flirt.2", Owner = Owner, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 15 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I'd pick you for every comp team.\"",
                        Lands = "\"I'd pick you first. Every time.\"",
                        Even = "\"Cool. Deal.\"",
                        Grates = "\"Only if you're ready to work.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "cody.rom.flirt.3", Owner = Owner, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 30 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I keep finding reasons to sit by you.\"",
                        Lands = "\"I noticed. I like it.\"",
                        Even = "\"Yeah?\"",
                        Grates = "\"Sit anywhere. I'm focused.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "cody.rom.open.1", Owner = Owner, Kind = TalkKind.Opening,
                Open = "(sitting next to you after the comp) \"You came through for me today. That's twice now. I don't forget stuff like that.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I'll keep coming through.\"",
                        Lands = "\"I know you will. That's why I trust you.\"",
                        Even = "\"Good. I'm counting on it.\"",
                        Grates = "\"Just play the game.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"That's what we do.\"",
                        Lands = "\"Exactly. Loyal to the end.\"",
                        Even = "\"Right.\"",
                        Grates = "\"Don't get sentimental.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Twice? I'm counting three.\"",
                        Lands = "\"Okay, three. Don't let it go to your head.\"",
                        Even = "\"Maybe two and a half.\"",
                        Grates = "\"Two. Final answer.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. Solid. That's worth a lot in here.\"" },
                },
            },
            new TalkRow
            {
                Id = "cody.rom.open.2", Owner = Owner, Kind = TalkKind.Opening, When = new TalkGate { Spark = 15 },
                Open = "(tossing you a ball in the yard) \"You're the only one in here I don't have to watch my back around.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I've got your back. Always.\"",
                        Lands = "\"I know. That means everything in here.\"",
                        Even = "\"Good.\"",
                        Grates = "\"Don't say always in this house.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"That's a lot of trust.\"",
                        Lands = "\"It is. You earned it.\"",
                        Even = "\"I know.\"",
                        Grates = "\"Maybe too much.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Watch your back anyway.\"",
                        Lands = "\"Ha. See, that's why I like you.\"",
                        Even = "\"Yeah, yeah.\"",
                        Grates = "\"Okay, now I'm worried.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. Solid. That's worth a lot in here.\"" },
                },
            },
            new TalkRow
            {
                Id = "cody.rom.open.3", Owner = Owner, Kind = TalkKind.Opening, When = new TalkGate { Spark = 30 },
                Open = "(low, sincere) \"If it came down to the final two, I'd want it to be you next to me. I mean that.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"Me too. Only you.\"",
                        Lands = "\"Then we make it happen. Together.\"",
                        Even = "\"Good. Let's get there.\"",
                        Grates = "\"Let's get through this week first.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'd want it to be you.\"",
                        Lands = "\"Then that's the plan.\"",
                        Even = "\"Cool. Plan.\"",
                        Grates = "\"Plans change in here.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"That's a long way off.\"",
                        Lands = "\"I know. I'm patient.\"",
                        Even = "\"Sure is.\"",
                        Grates = "\"Never mind, then.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. Solid. That's worth a lot in here.\"" },
                },
            },
            new TalkRow
            {
                Id = "cody.rom.hold.1", Owner = Owner, Kind = TalkKind.HoldBack,
                Open = "(rubbing the back of his neck) \"Honestly? I'm not saying no. Let's see where this game takes us first.\"",
            },
            new TalkRow
            {
                Id = "cody.rom.turn.1", Owner = Owner, Kind = TalkKind.TurnAside, Attempt = 1,
                Open = "(a surprised laugh) \"Whoa. Okay. Didn't expect that in the middle of the wall comp chat.\"",
                Second = "\"Real talk, I'm here to play. I came back to win this, and that's where all my focus goes.\"",
                Replies = new[]
                {
                    new TalkReply { Choice = TalkChoice.LaughItOff, Line = "\"Fair enough. Can't blame me for trying.\"", Lands = "\"Ha. Respect. Now, real question: are you with me at the next vote?\"" },
                    new TalkReply { Choice = TalkChoice.PushAgain, Line = "\"Come on. Nobody's only here to play.\"", Lands = "\"Hey, I said no, and I meant it. I'm here to play. Don't make it weird.\"" },
                    new TalkReply { Choice = TalkChoice.Apologize, Line = "\"Sorry. I read that wrong.\"", Lands = "\"Hey, no, you're good. I respect you for asking straight. That's rare in here.\"" },
                },
            },
            new TalkRow
            {
                Id = "cody.rom.turn.2", Owner = Owner, Kind = TalkKind.TurnAside, Attempt = 2,
                Open = "(tossing a ball in the yard) \"Still here to play. Same as before.\"",
            },
            new TalkRow
            {
                Id = "cody.rom.turn.3", Owner = Owner, Kind = TalkKind.TurnAside, Attempt = 3,
                Open = "(standing up, direct) \"Last time, okay? I'm here to play. Let's leave it there and win some comps.\"",
            },
            new TalkRow
            {
                Id = "cody.rom.softno.1", Owner = Owner, Kind = TalkKind.SoftNo,
                Open = "(sincere) \"You're my teammate. My number one. I'd like to keep it that, no more. That okay?\"",
            },
            new TalkRow
            {
                Id = "cody.rom.declare.1", Owner = Owner, Kind = TalkKind.Declaration,
                Open = "(after the vote, quietly) \"I've thought about it. You and me, as a pair. All the way to the final two. You in?\"",
                Replies = new[]
                {
                    new TalkReply { Choice = TalkChoice.Accept, Line = "\"I'm in.\"", Lands = "\"Let's go. Nobody's splitting us up.\"" },
                    new TalkReply { Choice = TalkChoice.NotYet, Line = "\"Not yet.\"", Lands = "\"That's cool. I'll ask again when the time's right.\"" },
                    new TalkReply { Choice = TalkChoice.NotLikeThat, Line = "\"I don't feel it.\"", Lands = "\"Respect. Thanks for being straight. Still teammates.\"" },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. Loyal ones. That's still big.\"" },
                },
            },
            new TalkRow
            {
                Id = "cody.rom.yes.1", Owner = Owner, Kind = TalkKind.Accepts,
                Open = "(a big grin) \"Yes. Absolutely. You beat me to it, honestly.\"",
            },
            new TalkRow
            {
                Id = "cody.rom.no.1", Owner = Owner, Kind = TalkKind.Refuses,
                Open = "(earnest) \"No. You're my teammate, and I'm not risking that. I hope you get it.\"",
            },
            new TalkRow
            {
                Id = "cody.rom.couple.1", Owner = Owner, Kind = TalkKind.Couple,
                Open = "(on the yard bench) \"Two of us, one target. I'm fine with that. We just keep winning.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"We keep winning.\"",
                        Lands = "\"That's what I like to hear.\"",
                        Even = "\"Always.\"",
                        Grates = "\"Don't make it weird.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Let's keep it low-key this week.\"",
                        Lands = "\"Smart. Heads down.\"",
                        Even = "\"Okay.\"",
                        Grates = "\"Why? Somebody say something?\"",
                    },
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"I think we should just be teammates.\"", Lands = "\"Okay. That's hard. But okay. I've still got your back.\"" },
                },
            },
            new TalkRow
            {
                Id = "cody.rom.ex.1", Owner = Owner, Kind = TalkKind.Ex,
                Open = "(polite, a little quiet) \"We're good, okay? I just need to keep my head in the game for a bit.\"",
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
                        Lands = "\"Thanks. I appreciate it.\"",
                        Even = "\"Okay.\"",
                        Grates = "\"I'm fine.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I still want us to be friends.\"",
                        Lands = "\"Teammates. Always.\"",
                        Even = "\"Sure.\"",
                        Grates = "\"Later.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "cody.rom.jealous.1", Owner = Owner, Kind = TalkKind.Jealousy,
                Open = "(direct, in the yard) \"I heard something about you and someone else. Loyalty matters to me. Talk.\"",
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
                        Lands = "\"Okay. I trust you.\"",
                        Even = "\"We'll see.\"",
                        Grates = "\"Show me.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You're listening to gossip.\"",
                        Lands = "\"Maybe. I still had to ask.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"Don't dodge it.\"",
                    },
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"Maybe we should end this. Kindly.\"", Lands = "\"Okay. That's hard. I respect it. I've still got your back.\"" },
                },
            },
            new TalkRow
            {
                Id = "cody.rom.react1.1", Owner = Owner, Kind = TalkKind.Reaction, Reaction = 1,
                Open = "(grinning) \"You two aren't subtle. Like, everybody knows. Just saying.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Subtle was never the plan.\"",
                        Lands = "\"Ha. Clearly.\"",
                        Even = "\"Fair.\"",
                        Grates = "\"It shows, though.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"We're happy. Is that so bad?\"",
                        Lands = "\"Nah. Good for you.\"",
                        Even = "\"It's fine.\"",
                        Grates = "\"Bad for your game.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who else has noticed?\"",
                        Lands = "\"The whole house.\"",
                        Even = "\"Most.\"",
                        Grates = "\"Everyone.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "cody.rom.react2.1", Owner = Owner, Kind = TalkKind.Reaction, Reaction = 2,
                Open = "(serious) \"Careful. A pair is a target. One block, two names. Stay loyal and win comps.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I know. We're ready for it.\"",
                        Lands = "\"Good. Stay ready.\"",
                        Even = "\"Hope so.\"",
                        Grates = "\"Nobody is.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who's coming for us?\"",
                        Lands = "\"Not me. Just know it's coming.\"",
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
