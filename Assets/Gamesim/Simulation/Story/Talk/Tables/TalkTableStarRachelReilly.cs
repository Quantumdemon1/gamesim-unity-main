namespace Gamesim.Simulation
{
    /// <summary>
    /// Free talk, one All-Star template's own romance talks (slice S3, talk rules 3; ruling R1: the talks are C# tables). The
    /// talks builder fills <see cref="Rows"/>: rows of the romance kinds, Owner = <see cref="Owner"/>, each kind the twelve
    /// All-Star tables all hold. Game register only: the house, the competitions, juries, the season and their name as a player.
    /// Tame. American spelling.
    /// </summary>
    public static class TalkTableStarRachelReilly
    {
        public const string Owner = "rachel-reilly";

        public static readonly TalkRow[] Rows =
        {
            new TalkRow
            {
                Id = "rachel.rom.flirt.1", Owner = Owner, Kind = TalkKind.FlirtLine,
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"You're my favorite person to talk game with.\"",
                        Lands = "\"Yes! You actually get my plans!\"",
                        Even = "\"Okay!\"",
                        Grates = "\"Flattery won't win comps!\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "rachel.rom.flirt.2", Owner = Owner, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 15 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I'd pick you for every comp team.\"",
                        Lands = "\"Obviously! We'd win everything!\"",
                        Even = "\"Okay, deal!\"",
                        Grates = "\"Only if you can keep up!\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "rachel.rom.flirt.3", Owner = Owner, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 30 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I keep finding reasons to sit by you.\"",
                        Lands = "\"Ha! I noticed! I like it!\"",
                        Even = "\"Okay!\"",
                        Grates = "\"Sit anywhere! I'm training!\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "rachel.rom.open.1", Owner = Owner, Kind = TalkKind.Opening,
                Open = "(after the comp, breathless) \"You were screaming for me! I heard you! Nobody screams like that for me!\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I'll always scream for you.\"",
                        Lands = "\"Yes! That's what I need! Every week!\"",
                        Even = "\"Ha, okay!\"",
                        Grates = "\"Maybe a little quieter.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"You almost fell off.\"",
                        Lands = "\"Almost! I didn't though! Did you see that?\"",
                        Even = "\"Almost!\"",
                        Grates = "\"Okay, rude.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You were incredible.\"",
                        Lands = "\"I know! I mean, thank you!\"",
                        Even = "\"Thanks!\"",
                        Grates = "\"I could've been better.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends! Yes! Teammates! The best!\"" },
                },
            },
            new TalkRow
            {
                Id = "rachel.rom.open.2", Owner = Owner, Kind = TalkKind.Opening, When = new TalkGate { Spark = 15 },
                Open = "(sitting on the counter) \"You know what I like about you? You're not scared of me. Everyone's scared of me.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I like you too much to be scared.\"",
                        Lands = "\"Oh! Okay! I like that!\"",
                        Even = "\"Ha. Good.\"",
                        Grates = "\"Okay, that's a lot.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Should I be scared?\"",
                        Lands = "\"A little! It's healthy!\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"Yes. Very.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"You're not that scary.\"",
                        Lands = "\"I am! But I like that you think I'm not.\"",
                        Even = "\"Okay.\"",
                        Grates = "\"Excuse me?\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends! Yes! Teammates! The best!\"" },
                },
            },
            new TalkRow
            {
                Id = "rachel.rom.open.3", Owner = Owner, Kind = TalkKind.Opening, When = new TalkGate { Spark = 30 },
                Open = "(quieter than you've ever heard) \"Okay. Real talk. I play harder when you're watching. That's weird, right?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"Then I'll always watch.\"",
                        Lands = "\"Okay, stop. I like that. Stop.\"",
                        Even = "\"Okay.\"",
                        Grates = "\"Don't make it weird.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"It's not weird.\"",
                        Lands = "\"It's a little weird. I like it.\"",
                        Even = "\"Okay, good.\"",
                        Grates = "\"It's weird.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Lucky charm?\"",
                        Lands = "\"Shut up. Yes. Maybe.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"No!\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends! Yes! Teammates! The best!\"" },
                },
            },
            new TalkRow
            {
                Id = "rachel.rom.hold.1", Owner = Owner, Kind = TalkKind.HoldBack,
                Open = "(pausing, actually thinking) \"Okay. I'm not saying no. But let's see where this game takes us. Seriously.\"",
            },
            new TalkRow
            {
                Id = "rachel.rom.turn.1", Owner = Owner, Kind = TalkKind.TurnAside, Attempt = 1,
                Open = "(throwing her head back, laughing) \"Oh my gosh. You did not just try that. In this house? Right before a comp?\"",
                Second = "\"I'm here to play! Loud and proud. I'm here to win comps and take shots. Nothing else fits in this game.\"",
                Replies = new[]
                {
                    new TalkReply { Choice = TalkChoice.LaughItOff, Line = "\"Fair enough. Can't blame me for trying.\"", Lands = "\"Yes! See? I like you. Now help me figure out who to put up.\"" },
                    new TalkReply { Choice = TalkChoice.PushAgain, Line = "\"Come on. Nobody's only here to play.\"", Lands = "\"No! I said no. Push me again and I will put you up. I'm serious. Kind of.\"" },
                    new TalkReply { Choice = TalkChoice.Apologize, Line = "\"Sorry. I read that wrong.\"", Lands = "\"Oh, stop. You're fine. I take shots too. Let's go win something.\"" },
                },
            },
            new TalkRow
            {
                Id = "rachel.rom.turn.2", Owner = Owner, Kind = TalkKind.TurnAside, Attempt = 2,
                Open = "(stretching by the pool) \"Same answer! Here to play!\"",
            },
            new TalkRow
            {
                Id = "rachel.rom.turn.3", Owner = Owner, Kind = TalkKind.TurnAside, Attempt = 3,
                Open = "(loud, final, but laughing) \"Last time! I'm here to play! Now go win a comp!\"",
            },
            new TalkRow
            {
                Id = "rachel.rom.softno.1", Owner = Owner, Kind = TalkKind.SoftNo,
                Open = "(kind, for once quietly) \"You're awesome. But you're my teammate. That's it. Okay?\"",
            },
            new TalkRow
            {
                Id = "rachel.rom.declare.1", Owner = Owner, Kind = TalkKind.Declaration,
                Open = "(grabbing your arm, loud) \"Okay! I'm saying it! Us! As a pair! All the way! Yes or no?\"",
                Replies = new[]
                {
                    new TalkReply { Choice = TalkChoice.Accept, Line = "\"Yes!\"", Lands = "\"Yes! Okay! We're unstoppable now!\"" },
                    new TalkReply { Choice = TalkChoice.NotYet, Line = "\"Not yet.\"", Lands = "\"Ugh! Fine! I'll ask again! I always ask again!\"" },
                    new TalkReply { Choice = TalkChoice.NotLikeThat, Line = "\"I don't feel that way.\"", Lands = "\"Okay. Ouch. But okay. Still teammates.\"" },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends! Fine! The loudest friends in here!\"" },
                },
            },
            new TalkRow
            {
                Id = "rachel.rom.yes.1", Owner = Owner, Kind = TalkKind.Accepts,
                Open = "(jumping up) \"Yes! Yes, yes, yes! I was going to ask you after the comp!\"",
            },
            new TalkRow
            {
                Id = "rachel.rom.no.1", Owner = Owner, Kind = TalkKind.Refuses,
                Open = "(softer than usual) \"No. I'm sorry. You're my teammate. I need you as my teammate.\"",
            },
            new TalkRow
            {
                Id = "rachel.rom.couple.1", Owner = Owner, Kind = TalkKind.Couple,
                Open = "(on the lawn, grinning) \"We're the biggest target in this house now! Good! Let them come!\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Let them come.\"",
                        Lands = "\"Yes! That's my answer too!\"",
                        Even = "\"Yes!\"",
                        Grates = "\"Don't get soft!\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Let's keep it low-key this week.\"",
                        Lands = "\"Ugh, fine! Low-key! For like a day!\"",
                        Even = "\"Okay.\"",
                        Grates = "\"Low-key? Me? No!\"",
                    },
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"I think we should go back to being teammates.\"", Lands = "\"Oh. Okay. That stinks. But I respect it. Teammates!\"" },
                },
            },
            new TalkRow
            {
                Id = "rachel.rom.ex.1", Owner = Owner, Kind = TalkKind.Ex,
                Open = "(a little too loud) \"We're fine! Totally fine! I'm going to go win something now!\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I hope we're okay.\"",
                        Lands = "\"We're good! Totally!\"",
                        Even = "\"We will be!\"",
                        Grates = "\"Not now!\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Take all the time you need.\"",
                        Lands = "\"Thanks! I'll be fine!\"",
                        Even = "\"Okay.\"",
                        Grates = "\"I don't need time!\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I still want us to be friends.\"",
                        Lands = "\"Teammates! Always!\"",
                        Even = "\"Sure.\"",
                        Grates = "\"Later!\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "rachel.rom.jealous.1", Owner = Owner, Kind = TalkKind.Jealousy,
                Open = "(loud, by the pool) \"Excuse me! I heard you've been all charming with someone else! Is that true?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"It wasn't what it looked like.\"",
                        Lands = "\"Okay! Then what was it?\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"It looked like it!\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You're the one I want. I mean it.\"",
                        Lands = "\"Okay. Good. I believe you.\"",
                        Even = "\"We'll see.\"",
                        Grates = "\"Prove it!\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You're listening to gossip.\"",
                        Lands = "\"Maybe! I still hate it!\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"Don't call me gullible!\"",
                    },
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"Maybe we should end this. Kindly.\"", Lands = "\"Oh. Okay. That stinks! But I respect it. Teammates!\"" },
                },
            },
            new TalkRow
            {
                Id = "rachel.rom.react1.1", Owner = Owner, Kind = TalkKind.Reaction, Reaction = 1,
                Open = "(laughing loudly) \"You two aren't subtle! At all! The whole house knows!\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Subtle was never the plan.\"",
                        Lands = "\"Clearly!\"",
                        Even = "\"Okay, fair!\"",
                        Grates = "\"It shows!\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"We're happy. Is that so bad?\"",
                        Lands = "\"No! Good for you!\"",
                        Even = "\"It's fine.\"",
                        Grates = "\"For your game, maybe!\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who else has noticed?\"",
                        Lands = "\"Everyone!\"",
                        Even = "\"Most of us!\"",
                        Grates = "\"Literally everyone!\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "rachel.rom.react2.1", Owner = Owner, Kind = TalkKind.Reaction, Reaction = 2,
                Open = "(suddenly serious) \"Careful. A pair is a target. Win every comp you can.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I know. We're ready for it.\"",
                        Lands = "\"Good! Then win!\"",
                        Even = "\"Hope so.\"",
                        Grates = "\"You're not!\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who's coming for us?\"",
                        Lands = "\"Not me! Probably!\"",
                        Even = "\"Can't say.\"",
                        Grates = "\"Not telling!\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Thanks for the warning.\"",
                        Lands = "\"Anytime!\"",
                        Even = "\"Sure.\"",
                        Grates = "\"Use it!\"",
                    },
                },
            },
        };
    }
}
