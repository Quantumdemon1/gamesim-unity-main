namespace Gamesim.Simulation
{
    /// <summary>
    /// Free talk, one All-Star template's own romance talks (slice S3, talk rules 3; ruling R1: the talks are C# tables). The
    /// talks builder fills <see cref="Rows"/>: rows of the romance kinds, Owner = <see cref="Owner"/>, each kind the twelve
    /// All-Star tables all hold. Game register only: the house, the competitions, juries, the season and their name as a player.
    /// Tame. American spelling.
    /// </summary>
    public static class TalkTableStarTylerCrispen
    {
        public const string Owner = "tyler-crispen";

        public static readonly TalkRow[] Rows =
        {
            new TalkRow
            {
                Id = "tyler.rom.flirt.1", Owner = Owner, Kind = TalkKind.FlirtLine,
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"You're my favorite person to talk game with.\"",
                        Lands = "\"Same. You're easy to talk to.\"",
                        Even = "\"Cool.\"",
                        Grates = "\"Okay, easy there.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "tyler.rom.flirt.2", Owner = Owner, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 15 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I'd pick you for every comp team.\"",
                        Lands = "\"Ha. I'd pick you too. For sure.\"",
                        Even = "\"Cool.\"",
                        Grates = "\"Only if you're fast.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "tyler.rom.flirt.3", Owner = Owner, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 30 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I keep finding reasons to sit by you.\"",
                        Lands = "\"I noticed. It's cool. I like it.\"",
                        Even = "\"Yeah?\"",
                        Grates = "\"Sit anywhere. It's chill.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "tyler.rom.open.1", Owner = Owner, Kind = TalkKind.Opening,
                Open = "(sitting on the lawn beside you) \"Hey. I like hanging with you. Like, a lot more than I planned to.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"Same. A lot more.\"",
                        Lands = "\"Okay, cool. That's really cool.\"",
                        Even = "\"Yeah?\"",
                        Grates = "\"Okay, easy.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I like hanging with you too.\"",
                        Lands = "\"Nice. Let's keep doing it.\"",
                        Even = "\"Cool.\"",
                        Grates = "\"Cool, cool.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"You plan who you hang with?\"",
                        Lands = "\"Ha. Kind of. Strategy, you know.\"",
                        Even = "\"Sometimes.\"",
                        Grates = "\"Okay, don't tell anyone.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. Totally. That's chill.\"" },
                },
            },
            new TalkRow
            {
                Id = "tyler.rom.open.2", Owner = Owner, Kind = TalkKind.Opening, When = new TalkGate { Spark = 15 },
                Open = "(handing you the last popsicle) \"You get this one. That's like, a big deal in this house.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"Then you're a big deal to me.\"",
                        Lands = "\"Okay, that landed. Not really. Kind of.\"",
                        Even = "\"Ha. Cool.\"",
                        Grates = "\"Smooth. Too smooth.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Thanks, really.\"",
                        Lands = "\"Anytime. Well, not anytime. But for you.\"",
                        Even = "\"Sure.\"",
                        Grates = "\"It's just a popsicle.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Is this a bribe?\"",
                        Lands = "\"A little. Is it working?\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"Okay, give it back.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. Totally. That's chill.\"" },
                },
            },
            new TalkRow
            {
                Id = "tyler.rom.open.3", Owner = Owner, Kind = TalkKind.Opening, When = new TalkGate { Spark = 30 },
                Open = "(quieter than usual) \"Honestly? You're the only one in here I don't have to act chill around.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"Then don't act. Just be here.\"",
                        Lands = "\"Okay. I like that a lot.\"",
                        Even = "\"Okay.\"",
                        Grates = "\"That's a lot.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I like the real you.\"",
                        Lands = "\"That's, wow. Thanks.\"",
                        Even = "\"Cool.\"",
                        Grates = "\"Don't tell anyone.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Is that a good thing?\"",
                        Lands = "\"Yeah. A really good thing.\"",
                        Even = "\"I think so.\"",
                        Grates = "\"Not sure yet.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. Totally. That's chill.\"" },
                },
            },
            new TalkRow
            {
                Id = "tyler.rom.hold.1", Owner = Owner, Kind = TalkKind.HoldBack,
                Open = "(scratching his head with a grin) \"Not a no. Just, let's see where this game takes us first, yeah?\"",
            },
            new TalkRow
            {
                Id = "tyler.rom.turn.1", Owner = Owner, Kind = TalkKind.TurnAside, Attempt = 1,
                Open = "(a slow, easy grin) \"Whoa, hey. That's flattering, for real. Totally didn't see it coming.\"",
                Second = "\"But I'm here to play. Like, fully. I'm trying to keep my head in the game the whole way.\"",
                Replies = new[]
                {
                    new TalkReply { Choice = TalkChoice.LaughItOff, Line = "\"Fair enough. Can't blame me for trying.\"", Lands = "\"Ha, nice. No worries at all. So, uh, who are you voting for?\"" },
                    new TalkReply { Choice = TalkChoice.PushAgain, Line = "\"Come on. Nobody's only here to play.\"", Lands = "\"I said no, though. Keep pushing and people start to talk. Not a good look for either of us.\"" },
                    new TalkReply { Choice = TalkChoice.Apologize, Line = "\"Sorry. I read that wrong.\"", Lands = "\"Nah, you're good. Seriously. We're chill. Want to go over the vote?\"" },
                },
            },
            new TalkRow
            {
                Id = "tyler.rom.turn.2", Owner = Owner, Kind = TalkKind.TurnAside, Attempt = 2,
                Open = "(lying on the lawn) \"Same as before. Here to play. All good, though.\"",
            },
            new TalkRow
            {
                Id = "tyler.rom.turn.3", Owner = Owner, Kind = TalkKind.TurnAside, Attempt = 3,
                Open = "(sitting up, easy but clear) \"Last time, okay? I'm here to play. Let's keep it about the game.\"",
            },
            new TalkRow
            {
                Id = "tyler.rom.softno.1", Owner = Owner, Kind = TalkKind.SoftNo,
                Open = "(kindly, easy) \"You're awesome. Like, really. But for me it's just friends. That cool?\"",
            },
            new TalkRow
            {
                Id = "tyler.rom.declare.1", Owner = Owner, Kind = TalkKind.Declaration,
                Open = "(after a quiet night in the yard) \"Okay, real talk. I want us to be a pair in here. All the way. You in?\"",
                Replies = new[]
                {
                    new TalkReply { Choice = TalkChoice.Accept, Line = "\"I'm in.\"", Lands = "\"Yes! Okay. Let's keep it chill though. Kind of.\"" },
                    new TalkReply { Choice = TalkChoice.NotYet, Line = "\"Not yet.\"", Lands = "\"Totally. No rush. I'll ask again.\"" },
                    new TalkReply { Choice = TalkChoice.NotLikeThat, Line = "\"I don't feel that way.\"", Lands = "\"Ah. Okay. No worries. Still cool.\"" },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. For sure. That's still great.\"" },
                },
            },
            new TalkRow
            {
                Id = "tyler.rom.yes.1", Owner = Owner, Kind = TalkKind.Accepts,
                Open = "(a slow grin) \"Yeah. Yes. I was totally going to ask. You beat me to it.\"",
            },
            new TalkRow
            {
                Id = "tyler.rom.no.1", Owner = Owner, Kind = TalkKind.Refuses,
                Open = "(gently) \"Ah, no. I like you, but I'd rather keep it friends. Sorry.\"",
            },
            new TalkRow
            {
                Id = "tyler.rom.couple.1", Owner = Owner, Kind = TalkKind.Couple,
                Open = "(lying in the hammock) \"So we're kind of a big target now. That's fine. We'll just stay chill.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"We stay chill.\"",
                        Lands = "\"Exactly. Chill and winning.\"",
                        Even = "\"Yeah.\"",
                        Grates = "\"Don't make it a whole thing.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Let's keep it low-key this week.\"",
                        Lands = "\"Totally. Chill and quiet.\"",
                        Even = "\"Okay.\"",
                        Grates = "\"Why? Did someone say something?\"",
                    },
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"I think we should go back to being friends.\"", Lands = "\"Oh. Yeah. Okay. That's fair. Still friends, for real.\"" },
                },
            },
            new TalkRow
            {
                Id = "tyler.rom.ex.1", Owner = Owner, Kind = TalkKind.Ex,
                Open = "(easy, but quiet) \"We're good, I promise. I just need a minute. Like, a few days of minutes.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I hope we're okay.\"",
                        Lands = "\"We're good. For real.\"",
                        Even = "\"We will be.\"",
                        Grates = "\"Not now.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Take all the time you need.\"",
                        Lands = "\"Thanks. That helps.\"",
                        Even = "\"Okay.\"",
                        Grates = "\"I'm fine.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I still want us to be friends.\"",
                        Lands = "\"Friends. Totally.\"",
                        Even = "\"Sure.\"",
                        Grates = "\"Later.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "tyler.rom.jealous.1", Owner = Owner, Kind = TalkKind.Jealousy,
                Open = "(sitting up in the hammock) \"So, uh, I heard you've been hanging with someone else a lot. Like, a lot. What's up?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"It wasn't what it looked like.\"",
                        Lands = "\"Okay. Then what was it?\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"It kind of looked like it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You're the one I want. I mean it.\"",
                        Lands = "\"Okay. Cool. I believe you.\"",
                        Even = "\"We'll see.\"",
                        Grates = "\"Show me.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You're listening to gossip.\"",
                        Lands = "\"Maybe. Still had to ask.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"Don't dodge, okay?\"",
                    },
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"Maybe we should end this. Kindly.\"", Lands = "\"Oh. Yeah. Okay. That's fair. Still friends, for real.\"" },
                },
            },
            new TalkRow
            {
                Id = "tyler.rom.react1.1", Owner = Owner, Kind = TalkKind.Reaction, Reaction = 1,
                Open = "(grinning on the lawn) \"You two aren't subtle. Like, at all. It's cool, though.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Subtle was never the plan.\"",
                        Lands = "\"Ha. Clearly.\"",
                        Even = "\"Fair.\"",
                        Grates = "\"It shows.\"",
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
                        Lands = "\"Everybody.\"",
                        Even = "\"Most.\"",
                        Grates = "\"Everyone.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "tyler.rom.react2.1", Owner = Owner, Kind = TalkKind.Reaction, Reaction = 2,
                Open = "(quieter) \"Careful, though. A pair is a target. Spread yourselves out a little.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I know. We're ready for it.\"",
                        Lands = "\"Cool. Stay ready.\"",
                        Even = "\"Hope so.\"",
                        Grates = "\"Nobody is.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who's coming for us?\"",
                        Lands = "\"Not me. Just saying.\"",
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
