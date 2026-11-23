namespace Gamesim.Simulation
{
    /// <summary>
    /// Free talk, one All-Star template's own romance talks (slice S3, talk rules 3; ruling R1: the talks are C# tables). The
    /// talks builder fills <see cref="Rows"/>: rows of the romance kinds, Owner = <see cref="Owner"/>, each kind the twelve
    /// All-Star tables all hold. Game register only: the house, the competitions, juries, the season and their name as a player.
    /// Tame. American spelling.
    /// </summary>
    public static class TalkTableStarJunSong
    {
        public const string Owner = "jun-song";

        public static readonly TalkRow[] Rows =
        {
            new TalkRow
            {
                Id = "jun.rom.flirt.1", Owner = Owner, Kind = TalkKind.FlirtLine,
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"You're my favorite person to talk game with.\"",
                        Lands = "\"Hm. Mine too. Don't tell the others.\"",
                        Even = "\"Hm.\"",
                        Grates = "\"Flattery is noisy.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "jun.rom.flirt.2", Owner = Owner, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 15 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I'd pick you for every comp team.\"",
                        Lands = "\"I'd pick you too. I'd sit out, but I'd pick you.\"",
                        Even = "\"Fine.\"",
                        Grates = "\"Teams are drama.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "jun.rom.flirt.3", Owner = Owner, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 30 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I keep finding reasons to sit by you.\"",
                        Lands = "\"I noticed. Very sneaky. I approve.\"",
                        Even = "\"Hm.\"",
                        Grates = "\"Sit anywhere. I'm floating.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "jun.rom.open.1", Owner = Owner, Kind = TalkKind.Opening,
                Open = "(passing you a plate) \"I made too much again. On purpose. You're the only one I want to feed.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I'll always eat your cooking.\"",
                        Lands = "\"Good answer. Very good answer.\"",
                        Even = "\"Hm. Fine.\"",
                        Grates = "\"Don't be greedy.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Thank you. It's great.\"",
                        Lands = "\"I know. But thank you.\"",
                        Even = "\"Sure.\"",
                        Grates = "\"It's just leftovers.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Is this a bribe?\"",
                        Lands = "\"Everything in here is a bribe.\"",
                        Even = "\"A small one.\"",
                        Grates = "\"Fine, give it back.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. Smart. Friends float together.\"" },
                },
            },
            new TalkRow
            {
                Id = "jun.rom.open.2", Owner = Owner, Kind = TalkKind.Opening, When = new TalkGate { Spark = 15 },
                Open = "(sitting in the corner with you, away from the shouting) \"This is my favorite spot. You're the only one I let sit here.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I'm honored.\"",
                        Lands = "\"You should be. Very exclusive.\"",
                        Even = "\"Hm.\"",
                        Grates = "\"Don't make it a big deal.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Will people notice?\"",
                        Lands = "\"People never notice the corner. That's why it's mine.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"Now they will.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Your hiding spot?\"",
                        Lands = "\"Our hiding spot. If you behave.\"",
                        Even = "\"Shh.\"",
                        Grates = "\"Not anymore.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. Smart. Friends float together.\"" },
                },
            },
            new TalkRow
            {
                Id = "jun.rom.open.3", Owner = Owner, Kind = TalkKind.Opening, When = new TalkGate { Spark = 30 },
                Open = "(quietly) \"I stay out of everything in here. I'm having a hard time staying out of this.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"Then don't stay out.\"",
                        Lands = "\"Hm. You make it hard to float.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"Easy for you to say.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'm glad.\"",
                        Lands = "\"Me too. Surprisingly.\"",
                        Even = "\"Hm.\"",
                        Grates = "\"Don't be.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Is that a risk for you?\"",
                        Lands = "\"Big risk. I don't take risks. Except this.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"Everything is.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. Smart. Friends float together.\"" },
                },
            },
            new TalkRow
            {
                Id = "jun.rom.hold.1", Owner = Owner, Kind = TalkKind.HoldBack,
                Open = "(a sly, slow smile) \"Hmm. I'm not saying no. Let's see where this game takes us first, hm?\"",
            },
            new TalkRow
            {
                Id = "jun.rom.turn.1", Owner = Owner, Kind = TalkKind.TurnAside, Attempt = 1,
                Open = "(raising an eyebrow, amused) \"Oh, now that's interesting. I had three guesses what you'd say. That wasn't one.\"",
                Second = "\"But I'm here to play. I stay out of the drama. All of it. That's how I get to the end.\"",
                Replies = new[]
                {
                    new TalkReply { Choice = TalkChoice.LaughItOff, Line = "\"Fair enough. Can't blame me for trying.\"", Lands = "\"Good. You're quicker than people think. Let's talk about who's fighting this week.\"" },
                    new TalkReply { Choice = TalkChoice.PushAgain, Line = "\"Come on. Nobody's only here to play.\"", Lands = "\"I said no. Pushing makes noise. Noise makes targets. Don't be a target.\"" },
                    new TalkReply { Choice = TalkChoice.Apologize, Line = "\"Sorry. I read that wrong.\"", Lands = "\"No worries. You took a no quietly. That's how you survive in here.\"" },
                },
            },
            new TalkRow
            {
                Id = "jun.rom.turn.2", Owner = Owner, Kind = TalkKind.TurnAside, Attempt = 2,
                Open = "(stirring a pot on the stove) \"Same answer. Here to play.\"",
            },
            new TalkRow
            {
                Id = "jun.rom.turn.3", Owner = Owner, Kind = TalkKind.TurnAside, Attempt = 3,
                Open = "(cheerfully final) \"Last time. I'm here to play. Now go stay out of trouble.\"",
            },
            new TalkRow
            {
                Id = "jun.rom.softno.1", Owner = Owner, Kind = TalkKind.SoftNo,
                Open = "(kind, with a little smile) \"You're good company. But I float alone. Friends, yes?\"",
            },
            new TalkRow
            {
                Id = "jun.rom.declare.1", Owner = Owner, Kind = TalkKind.Declaration,
                Open = "(after dinner, quietly) \"Okay. I've floated long enough. I want us to be a pair in this game. You and me.\"",
                Replies = new[]
                {
                    new TalkReply { Choice = TalkChoice.Accept, Line = "\"Yes.\"", Lands = "\"Good. Now we float together.\"" },
                    new TalkReply { Choice = TalkChoice.NotYet, Line = "\"Not yet.\"", Lands = "\"Fine. I'm patient. I'll ask once more.\"" },
                    new TalkReply { Choice = TalkChoice.NotLikeThat, Line = "\"I don't feel it.\"", Lands = "\"Okay. Better to know. Still friends.\"" },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. Floating friends. Good.\"" },
                },
            },
            new TalkRow
            {
                Id = "jun.rom.yes.1", Owner = Owner, Kind = TalkKind.Accepts,
                Open = "(a delighted laugh) \"Yes. Finally. I thought I'd have to ask you myself.\"",
            },
            new TalkRow
            {
                Id = "jun.rom.no.1", Owner = Owner, Kind = TalkKind.Refuses,
                Open = "(gently) \"No. I like you too much to risk it in this game. Friends, okay?\"",
            },
            new TalkRow
            {
                Id = "jun.rom.couple.1", Owner = Owner, Kind = TalkKind.Couple,
                Open = "(in the corner, quietly) \"Two floaters together. Everyone will notice. Hm. Let them. We'll be fine.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"We'll be fine.\"",
                        Lands = "\"Of course we will. Nobody sees us coming.\"",
                        Even = "\"Of course.\"",
                        Grates = "\"Don't make noise.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Let's keep it low-key this week.\"",
                        Lands = "\"Good. Quiet is how we float.\"",
                        Even = "\"Fine.\"",
                        Grates = "\"I'm always low-key.\"",
                    },
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"I think we should go back to being friends.\"", Lands = "\"Okay. Thank you for saying it nicely. Still friends.\"" },
                },
            },
            new TalkRow
            {
                Id = "jun.rom.ex.1", Owner = Owner, Kind = TalkKind.Ex,
                Open = "(stirring the pot, not looking up) \"We're fine. I just need to float alone for a bit.\"",
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
                        Even = "\"Hm.\"",
                        Grates = "\"I'm fine.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I still want us to be friends.\"",
                        Lands = "\"Friends. Floating friends.\"",
                        Even = "\"Sure.\"",
                        Grates = "\"Later.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "jun.rom.jealous.1", Owner = Owner, Kind = TalkKind.Jealousy,
                Open = "(stirring the pot, not looking up) \"I heard you've been busy with someone else. Busy makes noise. Tell me.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"It wasn't what it looked like.\"",
                        Lands = "\"Hm. Then what was it?\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"It looked like something.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You're the one I want. I mean it.\"",
                        Lands = "\"Hm. Okay. I believe you.\"",
                        Even = "\"We'll see.\"",
                        Grates = "\"Show me.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You're listening to gossip.\"",
                        Lands = "\"Maybe. But it's loud gossip.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"Don't dodge.\"",
                    },
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"Maybe we should end this. Kindly.\"", Lands = "\"Okay. Thank you for saying it nicely. Still friends.\"" },
                },
            },
            new TalkRow
            {
                Id = "jun.rom.react1.1", Owner = Owner, Kind = TalkKind.Reaction, Reaction = 1,
                Open = "(amused) \"You two aren't subtle. Even I noticed, and I stay out of everything.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Subtle was never the plan.\"",
                        Lands = "\"Clearly.\"",
                        Even = "\"Hm.\"",
                        Grates = "\"It shows.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"We're happy. Is that so bad?\"",
                        Lands = "\"No. Just noisy.\"",
                        Even = "\"No.\"",
                        Grates = "\"For your game, yes.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who else has noticed?\"",
                        Lands = "\"Everyone. Even me.\"",
                        Even = "\"Most.\"",
                        Grates = "\"Everyone.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "jun.rom.react2.1", Owner = Owner, Kind = TalkKind.Reaction, Reaction = 2,
                Open = "(quietly) \"Careful. A pair is a target. Noise draws fire. Float a little.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I know. We're ready for it.\"",
                        Lands = "\"Good. Stay quiet.\"",
                        Even = "\"Hope so.\"",
                        Grates = "\"Nobody is.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who's coming for us?\"",
                        Lands = "\"Everyone but me.\"",
                        Even = "\"Can't say.\"",
                        Grates = "\"Not telling.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Thanks for the warning.\"",
                        Lands = "\"Anytime.\"",
                        Even = "\"Hm.\"",
                        Grates = "\"Use it.\"",
                    },
                },
            },
        };
    }
}
