namespace Gamesim.Simulation
{
    /// <summary>
    /// Free talk, one All-Star template's own romance talks (slice S3, talk rules 3; ruling R1: the talks are C# tables). The
    /// talks builder fills <see cref="Rows"/>: rows of the romance kinds, Owner = <see cref="Owner"/>, each kind the twelve
    /// All-Star tables all hold. Game register only: the house, the competitions, juries, the season and their name as a player.
    /// Tame. American spelling.
    /// </summary>
    public static class TalkTableStarDanielleReyes
    {
        public const string Owner = "danielle-reyes";

        public static readonly TalkRow[] Rows =
        {
            new TalkRow
            {
                Id = "danielle.rom.flirt.1", Owner = Owner, Kind = TalkKind.FlirtLine,
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"You're my favorite person to talk game with.\"",
                        Lands = "\"Mm. Mine too. Don't repeat it.\"",
                        Even = "\"Noted.\"",
                        Grates = "\"Flattery is easy to read.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "danielle.rom.flirt.2", Owner = Owner, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 15 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I'd pick you for every comp team.\"",
                        Lands = "\"I'd pick you too. Quietly.\"",
                        Even = "\"Fair.\"",
                        Grates = "\"Teams change.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "danielle.rom.flirt.3", Owner = Owner, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 30 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I keep finding reasons to sit by you.\"",
                        Lands = "\"I noticed. I don't mind the reasons.\"",
                        Even = "\"Mm.\"",
                        Grates = "\"Sit anywhere. I'm reading the room.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "danielle.rom.open.1", Owner = Owner, Kind = TalkKind.Opening,
                Open = "(sitting across from you at the table) \"You keep looking at me like you've read something. Have you?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"Just that I like your company.\"",
                        Lands = "\"Mm. Honest. That's rarer than you'd think.\"",
                        Even = "\"Noted.\"",
                        Grates = "\"That's not a read.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Maybe. You first.\"",
                        Lands = "\"Clever. I'll go first another time.\"",
                        Even = "\"Later.\"",
                        Grates = "\"No.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Yes. You trust me.\"",
                        Lands = "\"I do. Don't make me regret it.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"Too soon to say.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. Good. I keep those close.\"" },
                },
            },
            new TalkRow
            {
                Id = "danielle.rom.open.2", Owner = Owner, Kind = TalkKind.Opening, When = new TalkGate { Spark = 15 },
                Open = "(quietly, in the storage room) \"I don't share my reads. I've shared three with you this week. I noticed.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I like being the one you share with.\"",
                        Lands = "\"So do I. Which is unusual.\"",
                        Even = "\"Mm.\"",
                        Grates = "\"Don't get used to it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'll keep them safe.\"",
                        Lands = "\"I know you will.\"",
                        Even = "\"Good.\"",
                        Grates = "\"You'd better.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Only three?\"",
                        Lands = "\"Three more than anyone else gets.\"",
                        Even = "\"Three. Count them.\"",
                        Grates = "\"Greedy.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. Good. I keep those close.\"" },
                },
            },
            new TalkRow
            {
                Id = "danielle.rom.open.3", Owner = Owner, Kind = TalkKind.Opening, When = new TalkGate { Spark = 30 },
                Open = "(a rare, open look) \"I've read everyone in this house. You're the one I keep reading again.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"Keep reading.\"",
                        Lands = "\"I intend to.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"Careful.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'm glad.\"",
                        Lands = "\"So am I. That surprised me.\"",
                        Even = "\"Good.\"",
                        Grates = "\"Don't be.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"What do you see?\"",
                        Lands = "\"Someone I'd keep to the end.\"",
                        Even = "\"Something worth keeping.\"",
                        Grates = "\"I'll tell you at the finale.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. Good. I keep those close.\"" },
                },
            },
            new TalkRow
            {
                Id = "danielle.rom.hold.1", Owner = Owner, Kind = TalkKind.HoldBack,
                Open = "(considering you carefully) \"I'm not saying no. Let's see where this game takes us. Then we'll talk.\"",
            },
            new TalkRow
            {
                Id = "danielle.rom.turn.1", Owner = Owner, Kind = TalkKind.TurnAside, Attempt = 1,
                Open = "(a long, knowing look) \"Interesting. I'd been reading something from you. I hadn't read that.\"",
                Second = "\"So let me be clear. I'm here to play. I watch, I listen, I vote. That's my whole house.\"",
                Replies = new[]
                {
                    new TalkReply { Choice = TalkChoice.LaughItOff, Line = "\"Fair enough. Can't blame me for trying.\"", Lands = "\"Good. You recover quickly. I like that in an ally. Now tell me what you've heard.\"" },
                    new TalkReply { Choice = TalkChoice.PushAgain, Line = "\"Come on. Nobody's only here to play.\"", Lands = "\"I said no. Pushing it tells me more about you than you'd want me to know.\"" },
                    new TalkReply { Choice = TalkChoice.Apologize, Line = "\"Sorry. I read that wrong.\"", Lands = "\"No harm done. You asked, I answered, and now I know you can take a no. That matters.\"" },
                },
            },
            new TalkRow
            {
                Id = "danielle.rom.turn.2", Owner = Owner, Kind = TalkKind.TurnAside, Attempt = 2,
                Open = "(without looking up) \"Same answer. I'm here to play.\"",
            },
            new TalkRow
            {
                Id = "danielle.rom.turn.3", Owner = Owner, Kind = TalkKind.TurnAside, Attempt = 3,
                Open = "(quiet and final) \"That's the last of it. I'm here to play. I'd keep your reads on the game, if I were you.\"",
            },
            new TalkRow
            {
                Id = "danielle.rom.softno.1", Owner = Owner, Kind = TalkKind.SoftNo,
                Open = "(gentle but clear) \"I like you. I trust you. That's where I want to keep it.\"",
            },
            new TalkRow
            {
                Id = "danielle.rom.declare.1", Owner = Owner, Kind = TalkKind.Declaration,
                Open = "(very quietly) \"I've read every angle. I want us in this together. To the end. Say yes or say no.\"",
                Replies = new[]
                {
                    new TalkReply { Choice = TalkChoice.Accept, Line = "\"Yes.\"", Lands = "\"Good. Then we keep our reads between us.\"" },
                    new TalkReply { Choice = TalkChoice.NotYet, Line = "\"Not yet.\"", Lands = "\"All right. I'll ask once more when it's time.\"" },
                    new TalkReply { Choice = TalkChoice.NotLikeThat, Line = "\"No. Not like that.\"", Lands = "\"Understood. I'd rather know than guess.\"" },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. Close ones. That's still rare from me.\"" },
                },
            },
            new TalkRow
            {
                Id = "danielle.rom.yes.1", Owner = Owner, Kind = TalkKind.Accepts,
                Open = "(a small, real smile) \"Yes. I read that coming. I was hoping I read it right.\"",
            },
            new TalkRow
            {
                Id = "danielle.rom.no.1", Owner = Owner, Kind = TalkKind.Refuses,
                Open = "(kindly) \"No. I'd rather tell you than let you play around a guess. We're still allies.\"",
            },
            new TalkRow
            {
                Id = "danielle.rom.couple.1", Owner = Owner, Kind = TalkKind.Couple,
                Open = "(watching the house from the stairs) \"They're all reading us now. Let them. They're reading it wrong.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Let them read.\"",
                        Lands = "\"Mm. We'll keep the real story between us.\"",
                        Even = "\"Mm. Good.\"",
                        Grates = "\"Don't make a show of it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Let's keep it low-key this week.\"",
                        Lands = "\"Wise. Less for them to read.\"",
                        Even = "\"Fine.\"",
                        Grates = "\"I'm always low-key.\"",
                    },
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"I think we should end this. Kindly.\"", Lands = "\"All right. Thank you for telling me first. We're still good.\"" },
                },
            },
            new TalkRow
            {
                Id = "danielle.rom.ex.1", Owner = Owner, Kind = TalkKind.Ex,
                Open = "(composed) \"We're fine. I just need a little room to think. I'll find you when I do.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I hope we're okay.\"",
                        Lands = "\"We are.\"",
                        Even = "\"We will be.\"",
                        Grates = "\"Not now.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Take all the time you need.\"",
                        Lands = "\"Thank you.\"",
                        Even = "\"Mm.\"",
                        Grates = "\"I don't need it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I still want us to be friends.\"",
                        Lands = "\"In time.\"",
                        Even = "\"Mm.\"",
                        Grates = "\"Not yet.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "danielle.rom.jealous.1", Owner = Owner, Kind = TalkKind.Jealousy,
                Open = "(quiet, by the diary room) \"I read something I didn't like today. You and someone else. Your version, please.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"It wasn't what it looked like.\"",
                        Lands = "\"Then what was it?\"",
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
                        Lands = "\"Maybe. My reads are usually right.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"Don't.\"",
                    },
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"Maybe we should end this. Kindly.\"", Lands = "\"All right. Thank you for telling me first. We're still good.\"" },
                },
            },
            new TalkRow
            {
                Id = "danielle.rom.react1.1", Owner = Owner, Kind = TalkKind.Reaction, Reaction = 1,
                Open = "(dry) \"You two aren't subtle. I don't even have to read you. It's written on the walls.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Subtle was never the plan.\"",
                        Lands = "\"Clearly.\"",
                        Even = "\"Mm.\"",
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
                        Lands = "\"Everyone paying attention.\"",
                        Even = "\"Most.\"",
                        Grates = "\"All of them.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "danielle.rom.react2.1", Owner = Owner, Kind = TalkKind.Reaction, Reaction = 2,
                Open = "(very quietly) \"Careful. A pair is a target. I've watched pairs fall. They never see it coming.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I know. We're ready for it.\"",
                        Lands = "\"Good. Then watch.\"",
                        Even = "\"Hope so.\"",
                        Grates = "\"Nobody is.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who's coming for us?\"",
                        Lands = "\"I have a guess. I'll keep it.\"",
                        Even = "\"Can't say.\"",
                        Grates = "\"Not telling.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Thanks for the warning.\"",
                        Lands = "\"Mm.\"",
                        Even = "\"Sure.\"",
                        Grates = "\"Remember it.\"",
                    },
                },
            },
        };
    }
}
