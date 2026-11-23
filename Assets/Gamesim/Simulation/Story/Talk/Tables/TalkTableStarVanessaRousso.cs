namespace Gamesim.Simulation
{
    /// <summary>
    /// Free talk, one All-Star template's own romance talks (slice S3, talk rules 3; ruling R1: the talks are C# tables). The
    /// talks builder fills <see cref="Rows"/>: rows of the romance kinds, Owner = <see cref="Owner"/>, each kind the twelve
    /// All-Star tables all hold. Game register only: the house, the competitions, juries, the season and their name as a player.
    /// Tame. American spelling.
    /// </summary>
    public static class TalkTableStarVanessaRousso
    {
        public const string Owner = "vanessa-rousso";

        public static readonly TalkRow[] Rows =
        {
            new TalkRow
            {
                Id = "vanessa.rom.flirt.1", Owner = Owner, Kind = TalkKind.FlirtLine,
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"You're my favorite person to talk game with.\"",
                        Lands = "\"Same! You actually follow my math.\"",
                        Even = "\"Okay, cool.\"",
                        Grates = "\"Flattery changes my odds on you.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "vanessa.rom.flirt.2", Owner = Owner, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 15 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I'd pick you for every comp team.\"",
                        Lands = "\"I'd pick you too. Good odds.\"",
                        Even = "\"Okay.\"",
                        Grates = "\"Teams are a gamble.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "vanessa.rom.flirt.3", Owner = Owner, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 30 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I keep finding reasons to sit by you.\"",
                        Lands = "\"I noticed. Statistically suspicious. I like it.\"",
                        Even = "\"Okay.\"",
                        Grates = "\"Sit anywhere. I'm counting.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "vanessa.rom.open.1", Owner = Owner, Kind = TalkKind.Opening,
                Open = "(across the table, smiling) \"I can usually read people. With you, I keep second guessing myself. I kind of like it.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"Keep guessing.\"",
                        Lands = "\"Oh, I will. You're fun to read.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"Okay, now you're bluffing.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Maybe I'm bluffing.\"",
                        Lands = "\"Then you're very good. I'm impressed.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"You're not. I can tell.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I'm easy to read. I like you.\"",
                        Lands = "\"Okay. That's refreshing.\"",
                        Even = "\"Noted.\"",
                        Grates = "\"Nobody's that easy.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. Okay. That I can calculate.\"" },
                },
            },
            new TalkRow
            {
                Id = "vanessa.rom.open.2", Owner = Owner, Kind = TalkKind.Opening, When = new TalkGate { Spark = 15 },
                Open = "(nudging you after the comp) \"You keep showing up at the right time. Statistically, that's not luck anymore.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"It's not luck. It's you.\"",
                        Lands = "\"Oh. Okay. Good answer.\"",
                        Even = "\"Sure.\"",
                        Grates = "\"Too smooth.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I like being where you are.\"",
                        Lands = "\"I noticed. It's nice.\"",
                        Even = "\"Okay.\"",
                        Grates = "\"That's a lot.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Maybe I'm counting votes.\"",
                        Lands = "\"Ha! Then you're my kind of player.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"I'd catch you.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. Okay. That I can calculate.\"" },
                },
            },
            new TalkRow
            {
                Id = "vanessa.rom.open.3", Owner = Owner, Kind = TalkKind.Opening, When = new TalkGate { Spark = 30 },
                Open = "(quiet, sincere) \"I don't go all in on anything in this house. I'm thinking about going all in on us.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"Go all in.\"",
                        Lands = "\"Okay. Okay. Soon. Let me run it once more.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"Don't rush me.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'm all in already.\"",
                        Lands = "\"That helps. That really helps.\"",
                        Even = "\"Good.\"",
                        Grates = "\"Careful saying that.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"What are the odds?\"",
                        Lands = "\"Better than I expected. Much better.\"",
                        Even = "\"Decent.\"",
                        Grates = "\"I'm still working on it.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. Okay. That I can calculate.\"" },
                },
            },
            new TalkRow
            {
                Id = "vanessa.rom.hold.1", Owner = Owner, Kind = TalkKind.HoldBack,
                Open = "(thinking it over, visibly) \"I'm not folding. But let's see where this game takes us before I bet anything.\"",
            },
            new TalkRow
            {
                Id = "vanessa.rom.turn.1", Owner = Owner, Kind = TalkKind.TurnAside, Attempt = 1,
                Open = "(a startled laugh) \"Oh! Okay. I didn't have that in my odds for today. That's a first.\"",
                Second = "\"But here's the math. I'm here to play. Every hour in here goes into the game. That's my whole bet.\"",
                Replies = new[]
                {
                    new TalkReply { Choice = TalkChoice.LaughItOff, Line = "\"Fair enough. Can't blame me for trying.\"", Lands = "\"Good. Okay. Let's run the numbers on the vote instead. Sit.\"" },
                    new TalkReply { Choice = TalkChoice.PushAgain, Line = "\"Come on. Nobody's only here to play.\"", Lands = "\"I said no. Pushing it changes my read on you, and not in your favor. Stop while you're ahead.\"" },
                    new TalkReply { Choice = TalkChoice.Apologize, Line = "\"Sorry. I read that wrong.\"", Lands = "\"No, no, don't be sorry. You took a shot. I respect a shot. We're fine.\"" },
                },
            },
            new TalkRow
            {
                Id = "vanessa.rom.turn.2", Owner = Owner, Kind = TalkKind.TurnAside, Attempt = 2,
                Open = "(with a small smile) \"Same answer. Here to play.\"",
            },
            new TalkRow
            {
                Id = "vanessa.rom.turn.3", Owner = Owner, Kind = TalkKind.TurnAside, Attempt = 3,
                Open = "(gently, but final) \"Last time. I'm here to play. Put that energy into the vote, okay?\"",
            },
            new TalkRow
            {
                Id = "vanessa.rom.softno.1", Owner = Owner, Kind = TalkKind.SoftNo,
                Open = "(kindly) \"I like you so much. But I've run it every way, and you're my ally. Only that.\"",
            },
            new TalkRow
            {
                Id = "vanessa.rom.declare.1", Owner = Owner, Kind = TalkKind.Declaration,
                Open = "(taking a breath) \"I've done the math every way I can. I want us all in. Together. To the end. Yes?\"",
                Replies = new[]
                {
                    new TalkReply { Choice = TalkChoice.Accept, Line = "\"Yes. All in.\"", Lands = "\"Okay! Okay. Best bet I've made in here.\"" },
                    new TalkReply { Choice = TalkChoice.NotYet, Line = "\"Not yet.\"", Lands = "\"Fair. I can wait for the right hand.\"" },
                    new TalkReply { Choice = TalkChoice.NotLikeThat, Line = "\"I don't feel it.\"", Lands = "\"Okay. I needed to know. Thank you.\"" },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. A safe bet. I like safe bets with you.\"" },
                },
            },
            new TalkRow
            {
                Id = "vanessa.rom.yes.1", Owner = Owner, Kind = TalkKind.Accepts,
                Open = "(laughing) \"Yes. I had that at about even odds. I'm so glad it hit.\"",
            },
            new TalkRow
            {
                Id = "vanessa.rom.no.1", Owner = Owner, Kind = TalkKind.Refuses,
                Open = "(gently) \"No. I care about you, but I won't bet the game on it. Allies, okay?\"",
            },
            new TalkRow
            {
                Id = "vanessa.rom.couple.1", Owner = Owner, Kind = TalkKind.Couple,
                Open = "(beside you on the couch) \"They're playing against two of us now. I like those odds, honestly.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"So do I.\"",
                        Lands = "\"Good. Then we keep playing.\"",
                        Even = "\"Good.\"",
                        Grates = "\"Don't get cocky.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Let's keep it low-key this week.\"",
                        Lands = "\"Smart. Lower profile, better odds.\"",
                        Even = "\"Okay.\"",
                        Grates = "\"Why? What did you hear?\"",
                    },
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"I think we should go back to being allies.\"", Lands = "\"Okay. That hurts. But I respect it. We're still a team at the vote.\"" },
                },
            },
            new TalkRow
            {
                Id = "vanessa.rom.ex.1", Owner = Owner, Kind = TalkKind.Ex,
                Open = "(quiet, then steady) \"We're okay. I just need to reset my head for the game. Give me a few days.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I hope we're okay.\"",
                        Lands = "\"We're okay. Really.\"",
                        Even = "\"We will be.\"",
                        Grates = "\"Not right now.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Take all the time you need.\"",
                        Lands = "\"Thank you. I will.\"",
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
                Id = "vanessa.rom.jealous.1", Owner = Owner, Kind = TalkKind.Jealousy,
                Open = "(upset, then steadying) \"I heard you've been working someone else very hard. That changes my read. Explain it.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"It wasn't what it looked like.\"",
                        Lands = "\"Okay. Then what was it?\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"The odds say otherwise.\"",
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
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"Maybe we should end this. Kindly.\"", Lands = "\"Okay. That hurts. But I respect it. Still a team at the vote.\"" },
                },
            },
            new TalkRow
            {
                Id = "vanessa.rom.react1.1", Owner = Owner, Kind = TalkKind.Reaction, Reaction = 1,
                Open = "(laughing) \"You two aren't subtle. I had the odds at even on day three. Now it's a sure thing.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Subtle was never the plan.\"",
                        Lands = "\"Clearly!\"",
                        Even = "\"Fair.\"",
                        Grates = "\"It shows.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"We're happy. Is that so bad?\"",
                        Lands = "\"No! It's nice.\"",
                        Even = "\"It's fine.\"",
                        Grates = "\"For your game, maybe.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who else has noticed?\"",
                        Lands = "\"Everyone at the table.\"",
                        Even = "\"Most.\"",
                        Grates = "\"Everyone.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "vanessa.rom.react2.1", Owner = Owner, Kind = TalkKind.Reaction, Reaction = 2,
                Open = "(serious) \"Careful. A pair is a target. Two votes is the easiest number to break up.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I know. We're ready for it.\"",
                        Lands = "\"Good. Play smart.\"",
                        Even = "\"Hope so.\"",
                        Grates = "\"You're not.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who's coming for us?\"",
                        Lands = "\"Not me. Probably.\"",
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
