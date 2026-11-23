namespace Gamesim.Simulation
{
    /// <summary>
    /// Free talk, one All-Star template's own romance talks (slice S3, talk rules 3; ruling R1: the talks are C# tables). The
    /// talks builder fills <see cref="Rows"/>: rows of the romance kinds, Owner = <see cref="Owner"/>, each kind the twelve
    /// All-Star tables all hold. Game register only: the house, the competitions, juries, the season and their name as a player.
    /// Tame. American spelling.
    /// </summary>
    public static class TalkTableStarJanellePierzina
    {
        public const string Owner = "janelle-pierzina";

        public static readonly TalkRow[] Rows =
        {
            new TalkRow
            {
                Id = "janelle.rom.flirt.1", Owner = Owner, Kind = TalkKind.FlirtLine,
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"You're my favorite person to talk game with.\"",
                        Lands = "\"Aw. Me too. Nobody else gets my comp strategy.\"",
                        Even = "\"Okay, cool.\"",
                        Grates = "\"Flattery won't win you a comp.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "janelle.rom.flirt.2", Owner = Owner, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 15 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I'd pick you for every comp team.\"",
                        Lands = "\"Obviously. And I'd carry us.\"",
                        Even = "\"Okay, deal.\"",
                        Grates = "\"Only if you can keep up.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "janelle.rom.flirt.3", Owner = Owner, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 30 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I keep finding reasons to sit by you.\"",
                        Lands = "\"Ha! I noticed. It's fine. I like it.\"",
                        Even = "\"Okay.\"",
                        Grates = "\"Sit anywhere, I'm training.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "janelle.rom.open.1", Owner = Owner, Kind = TalkKind.Opening,
                Open = "(after the comp, still breathing hard) \"You were cheering for me the whole time. I heard you. Loudest one.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I'll always cheer for you.\"",
                        Lands = "\"Good! I like hearing it. Do it again next week.\"",
                        Even = "\"Okay, aww. Thanks.\"",
                        Grates = "\"You could cheer quieter.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"You almost lost.\"",
                        Lands = "\"Almost! I never lose. Okay, rarely.\"",
                        Even = "\"Almost doesn't count.\"",
                        Grates = "\"Rude. Accurate. Rude.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You were amazing.\"",
                        Lands = "\"I know! Kidding. Thank you.\"",
                        Even = "\"Thanks.\"",
                        Grates = "\"I could've been faster.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends! Good. Training buddies. The best kind.\"" },
                },
            },
            new TalkRow
            {
                Id = "janelle.rom.open.2", Owner = Owner, Kind = TalkKind.Opening, When = new TalkGate { Spark = 15 },
                Open = "(sharing the yard couch) \"We make a good team. Like, scary good. Don't tell anyone I said that.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"The best team in here.\"",
                        Lands = "\"Yes! See? You get it.\"",
                        Even = "\"Okay, okay. We're good.\"",
                        Grates = "\"Calm down. It's a team.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"People will notice.\"",
                        Lands = "\"Let them. They should be scared.\"",
                        Even = "\"Maybe. Whatever.\"",
                        Grates = "\"Ugh, now I'm worried.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"You just want my vote.\"",
                        Lands = "\"Your vote and your company. Both.\"",
                        Even = "\"Maybe a little.\"",
                        Grates = "\"Wow. Okay.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends! Good. Training buddies. The best kind.\"" },
                },
            },
            new TalkRow
            {
                Id = "janelle.rom.open.3", Owner = Owner, Kind = TalkKind.Opening, When = new TalkGate { Spark = 30 },
                Open = "(quieter than usual) \"Can I say something weird? I play better when you're in the room.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"Then I'll stay in the room.\"",
                        Lands = "\"Good. Don't leave. I mean it.\"",
                        Even = "\"Okay. Weird, but okay.\"",
                        Grates = "\"Don't make it a big thing.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"That's not weird.\"",
                        Lands = "\"It's a little weird. I like it, though.\"",
                        Even = "\"Okay, good.\"",
                        Grates = "\"It's weird.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"So I'm your lucky charm?\"",
                        Lands = "\"Ugh. Maybe. Don't tell anyone.\"",
                        Even = "\"Kind of.\"",
                        Grates = "\"No. Forget it.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends! Good. Training buddies. The best kind.\"" },
                },
            },
            new TalkRow
            {
                Id = "janelle.rom.hold.1", Owner = Owner, Kind = TalkKind.HoldBack,
                Open = "(pausing mid-stretch) \"Hm. Okay. I'm not saying no. Let's see where this game takes us first.\"",
            },
            new TalkRow
            {
                Id = "janelle.rom.turn.1", Owner = Owner, Kind = TalkKind.TurnAside, Attempt = 1,
                Open = "(laughing, hands on hips) \"Oh my gosh, stop. That's funny. You picked the worst week to try that, the comp's tomorrow!\"",
                Second = "\"I'm here to play. I'm here to win comps and win this game. That's all I've got room for in here.\"",
                Replies = new[]
                {
                    new TalkReply { Choice = TalkChoice.LaughItOff, Line = "\"Fair enough. Can't blame me for trying.\"", Lands = "\"See? You get it. Now help me practice for the comp. Hold this.\"" },
                    new TalkReply { Choice = TalkChoice.PushAgain, Line = "\"Come on. Nobody's only here to play.\"", Lands = "\"No. I'm serious. Keep pushing and I'll put you up just to make a point. Kidding. Mostly.\"" },
                    new TalkReply { Choice = TalkChoice.Apologize, Line = "\"Sorry. I read that wrong.\"", Lands = "\"Don't be sorry! It's fine. Just be ready for the comp. That's what I need from you.\"" },
                },
            },
            new TalkRow
            {
                Id = "janelle.rom.turn.2", Owner = Owner, Kind = TalkKind.TurnAside, Attempt = 2,
                Open = "(stretching before the comp) \"Same answer. Here to play. Ask me to train instead.\"",
            },
            new TalkRow
            {
                Id = "janelle.rom.turn.3", Owner = Owner, Kind = TalkKind.TurnAside, Attempt = 3,
                Open = "(firm, but smiling) \"Okay, that's the last time. I'm here to play. Let's go win something.\"",
            },
            new TalkRow
            {
                Id = "janelle.rom.softno.1", Owner = Owner, Kind = TalkKind.SoftNo,
                Open = "(kind, a hand on your arm) \"You're great. I like you. But you're my teammate. That's it.\"",
            },
            new TalkRow
            {
                Id = "janelle.rom.declare.1", Owner = Owner, Kind = TalkKind.Declaration,
                Open = "(grabbing your arm) \"Okay, I'm just going to say it. Us. As a pair. All the way to the end. Yes or no?\"",
                Replies = new[]
                {
                    new TalkReply { Choice = TalkChoice.Accept, Line = "\"Yes.\"", Lands = "\"Yes! Okay! Nobody beats us now.\"" },
                    new TalkReply { Choice = TalkChoice.NotYet, Line = "\"Not yet.\"", Lands = "\"Ugh, fine. I'll ask again. I don't give up.\"" },
                    new TalkReply { Choice = TalkChoice.NotLikeThat, Line = "\"I don't feel that way.\"", Lands = "\"Oh. Okay. That's fine. Still teammates.\"" },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. Teammates. Still the best team in here.\"" },
                },
            },
            new TalkRow
            {
                Id = "janelle.rom.yes.1", Owner = Owner, Kind = TalkKind.Accepts,
                Open = "(throwing her arms up) \"Yes! Finally! I was going to ask you after the comp.\"",
            },
            new TalkRow
            {
                Id = "janelle.rom.no.1", Owner = Owner, Kind = TalkKind.Refuses,
                Open = "(softly) \"No. I'm sorry. I care about you, but you're my teammate. That's how I play.\"",
            },
            new TalkRow
            {
                Id = "janelle.rom.couple.1", Owner = Owner, Kind = TalkKind.Couple,
                Open = "(stretching on the lawn) \"We're the biggest target in the house now. Good. Then we win everything.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"We win everything.\"",
                        Lands = "\"That's my favorite answer.\"",
                        Even = "\"Yes!\"",
                        Grates = "\"Don't get soft on me.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Let's keep it low-key this week.\"",
                        Lands = "\"Ugh. Fine. Low-key. For one week.\"",
                        Even = "\"Okay.\"",
                        Grates = "\"Low-key isn't my thing.\"",
                    },
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"I think we should go back to being teammates.\"", Lands = "\"Okay. That stinks. But okay. Still teammates. Still the best.\"" },
                },
            },
            new TalkRow
            {
                Id = "janelle.rom.ex.1", Owner = Owner, Kind = TalkKind.Ex,
                Open = "(over-bright) \"We're good! Totally. I'm just going to train alone this week. That's all.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I hope we're okay.\"",
                        Lands = "\"We're good! Totally.\"",
                        Even = "\"We will be.\"",
                        Grates = "\"Not right now.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Take all the time you need.\"",
                        Lands = "\"Thanks. I'll be fine.\"",
                        Even = "\"Okay.\"",
                        Grates = "\"I don't need time.\"",
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
                Id = "janelle.rom.jealous.1", Owner = Owner, Kind = TalkKind.Jealousy,
                Open = "(hands on hips) \"Okay, I heard you've been all charming with someone else. Is that true? Tell me now.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"It wasn't what it looked like.\"",
                        Lands = "\"Okay. Then what was it?\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"It looked like it!\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You're the one I want. I mean it.\"",
                        Lands = "\"Okay. Good. I believe you.\"",
                        Even = "\"We'll see.\"",
                        Grates = "\"Prove it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You're listening to gossip.\"",
                        Lands = "\"Maybe. I still hate it.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"Don't call me gullible.\"",
                    },
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"Maybe we should end this. Kindly.\"", Lands = "\"Ugh. Okay. That stinks. Still teammates. Still the best.\"" },
                },
            },
            new TalkRow
            {
                Id = "janelle.rom.react1.1", Owner = Owner, Kind = TalkKind.Reaction, Reaction = 1,
                Open = "(laughing) \"Oh my gosh, you two aren't subtle. Like, at all. Everyone knows.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Subtle was never the plan.\"",
                        Lands = "\"Ha! Clearly.\"",
                        Even = "\"Okay, fair.\"",
                        Grates = "\"It shows, though.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"We're happy. Is that so bad?\"",
                        Lands = "\"No! Good for you.\"",
                        Even = "\"It's fine.\"",
                        Grates = "\"For your game, maybe.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who else has noticed?\"",
                        Lands = "\"Literally everyone.\"",
                        Even = "\"Most of us.\"",
                        Grates = "\"Everyone!\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "janelle.rom.react2.1", Owner = Owner, Kind = TalkKind.Reaction, Reaction = 2,
                Open = "(serious for a second) \"Careful. A pair is a target. Two votes go up together. Win the comps.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I know. We're ready for it.\"",
                        Lands = "\"Good. Then win.\"",
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
                        Lands = "\"Anytime!\"",
                        Even = "\"Sure.\"",
                        Grates = "\"Use it.\"",
                    },
                },
            },
        };
    }
}
