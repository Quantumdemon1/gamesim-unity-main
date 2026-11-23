namespace Gamesim.Simulation
{
    /// <summary>
    /// Free talk, the guarded class pool's romance talks (slice S3, talk rules 3; ruling R1: the talks are C# tables). The talks
    /// builder fills <see cref="Rows"/>: rows of the romance kinds (TalkKind.FlirtLine to TalkKind.Reaction), Pool = TalkPools.Guarded,
    /// in the row form of TalkRow and TalkReply (a choice reply carries TalkReply.Choice and no tone). Tame for everyone: a kiss
    /// is the most a line may say, and none is described. American spelling.
    /// </summary>
    public static class TalkTableGuardedRomance
    {
        public static readonly TalkRow[] Rows =
        {
            new TalkRow
            {
                Id = "guarded.rom.flirt.1", Pool = TalkPools.Guarded, Kind = TalkKind.FlirtLine,
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I like it when you find me first.\"",
                        Lands = "\"Hm. Keep doing that.\"",
                        Even = "\"Noted.\"",
                        Grates = "\"Don't.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rom.flirt.2", Pool = TalkPools.Guarded, Kind = TalkKind.FlirtLine,
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"You make this house a lot less boring.\"",
                        Lands = "\"So do you.\"",
                        Even = "\"Hm.\"",
                        Grates = "\"It's a boring house.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rom.flirt.3", Pool = TalkPools.Guarded, Kind = TalkKind.FlirtLine,
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I was hoping you'd be in here.\"",
                        Lands = "\"I hoped so too.\" (a pause) \"Don't repeat that.\"",
                        Even = "\"Hm.\"",
                        Grates = "\"I was just leaving.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rom.flirt.4", Pool = TalkPools.Guarded, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 15 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I saved you a seat. I always do.\"",
                        Lands = "\"I know. I sit there for you.\"",
                        Even = "\"Thanks.\"",
                        Grates = "\"It's a seat.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rom.flirt.5", Pool = TalkPools.Guarded, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 15 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"Talking to you is the best part of my day.\"",
                        Lands = "\"Mine too. Don't tell anyone.\"",
                        Even = "\"Mm.\"",
                        Grates = "\"That's a lot.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rom.flirt.6", Pool = TalkPools.Guarded, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 15 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"You know I like you, right?\"",
                        Lands = "\"I know. Same.\"",
                        Even = "\"I guessed.\"",
                        Grates = "\"Slow down.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rom.flirt.7", Pool = TalkPools.Guarded, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 30 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I think about you more than the game.\"",
                        Lands = "\"Same. I didn't want to.\"",
                        Even = "\"Careful.\"",
                        Grates = "\"Don't say that.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rom.flirt.8", Pool = TalkPools.Guarded, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 30 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I want you next to me at the end of this.\"",
                        Lands = "\"Then stay there.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"One week at a time.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rom.flirt.9", Pool = TalkPools.Guarded, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 30 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I don't want to talk to anyone else tonight.\"",
                        Lands = "\"Good. Neither do I.\"",
                        Even = "\"Fine. Stay.\"",
                        Grates = "\"It's late.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rom.open.1", Pool = TalkPools.Guarded, Kind = TalkKind.Opening,
                Open = "(raising one eyebrow) \"Was that a line?\" (a pause) \"It wasn't terrible.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"Then I'll try another.\"",
                        Lands = "\"Don't overdo it.\" (almost a smile) \"One a day.\"",
                        Even = "\"Maybe. We'll see.\"",
                        Grates = "\"Let's not.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"It was. I meant it.\"",
                        Lands = "\"Hm. Noted.\"",
                        Even = "\"All right.\"",
                        Grates = "\"Meaning it doesn't make it work.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Forget I said it.\"",
                        Lands = "\"I won't. I'm not saying that's bad.\"",
                        Even = "\"Fine.\"",
                        Grates = "\"Already have.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. Fine. Good, even.\"" },
                },
            },
            new TalkRow
            {
                Id = "guarded.rom.open.2", Pool = TalkPools.Guarded, Kind = TalkKind.Opening,
                Open = "(not looking up from the book) \"You sat closer than usual. I noticed. I'm not saying move.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"Good. I like it here.\"",
                        Lands = "\"So do I. Don't tell anyone.\"",
                        Even = "\"Hm. Fine. Stay.\"",
                        Grates = "\"Don't push it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Should I move?\"",
                        Lands = "\"No. I said I'm not saying move.\"",
                        Even = "\"Stay. It's fine.\"",
                        Grates = "\"Your call.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"You noticed? From behind a book?\"",
                        Lands = "\"I notice everything. Especially this.\"",
                        Even = "\"I read slow. I look up a lot.\"",
                        Grates = "\"Don't make it a thing.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"All right. That's still a lot from me.\"" },
                },
            },
            new TalkRow
            {
                Id = "guarded.rom.open.3", Pool = TalkPools.Guarded, Kind = TalkKind.Opening,
                Open = "(handing you a cup of tea without being asked) \"Don't read into it. I made two by accident.\" (a beat) \"I didn't.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I'm reading into it.\"",
                        Lands = "\"Good. Saves me saying more.\"",
                        Even = "\"Fine. A little.\"",
                        Grates = "\"You're reading too much.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Thank you. Really.\"",
                        Lands = "\"You're welcome. Don't make me say more.\"",
                        Even = "\"Sure.\"",
                        Grates = "\"It's tea. It's fine.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"So it was on purpose.\"",
                        Lands = "\"Obviously. Drink it.\"",
                        Even = "\"Maybe. Drink it.\"",
                        Grates = "\"Forget I said it.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. Nothing changes.\"" },
                },
            },
            new TalkRow
            {
                Id = "guarded.rom.open.4", Pool = TalkPools.Guarded, Kind = TalkKind.Opening, When = new TalkGate { Spark = 15 },
                Open = "(sitting beside you in the dark yard, saying nothing for a while) \"This is the only part of the day I like. You're in it.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I'm glad I'm in it.\"",
                        Lands = "\"So am I. More than I planned.\"",
                        Even = "\"Hm. Me too.\"",
                        Grates = "\"Don't ruin it with talking.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I'll just sit, then.\"",
                        Lands = "\"Good. That's what I like about you.\"",
                        Even = "\"Fine.\"",
                        Grates = "\"You can talk. A little.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"That's practically a speech for you.\"",
                        Lands = "(a short laugh) \"Don't get used to it.\"",
                        Even = "\"It is. Enjoy it.\"",
                        Grates = "\"I'll stop, then.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. Fine. Good, even.\"" },
                },
            },
            new TalkRow
            {
                Id = "guarded.rom.open.5", Pool = TalkPools.Guarded, Kind = TalkKind.Opening, When = new TalkGate { Spark = 15 },
                Open = "(quietly, while the others shout at a game) \"I don't like many people in here. I like you. That's all.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I like you too. More than a little.\"",
                        Lands = "\"Good. Then we're even.\"",
                        Even = "\"Noted.\"",
                        Grates = "\"Slow down.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"That means a lot from you.\"",
                        Lands = "\"It should. I don't say it.\"",
                        Even = "\"It's true.\"",
                        Grates = "\"Don't make it a big thing.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Why me?\"",
                        Lands = "\"You don't perform. Everyone else does.\"",
                        Even = "\"You're quiet. I like quiet.\"",
                        Grates = "\"Don't ask me that.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"All right. That's still a lot from me.\"" },
                },
            },
            new TalkRow
            {
                Id = "guarded.rom.open.6", Pool = TalkPools.Guarded, Kind = TalkKind.Opening, When = new TalkGate { Spark = 15 },
                Open = "(passing you the better pillow, wordless, then) \"Yours. Don't argue.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"Only if you keep the good one next time.\"",
                        Lands = "\"Hm. Deal.\" (a real smile, quick)",
                        Even = "\"We'll see.\"",
                        Grates = "\"It's a pillow.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"You didn't have to.\"",
                        Lands = "\"I know. I wanted to.\"",
                        Even = "\"Take it.\"",
                        Grates = "\"Fine. Give it back.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Thank you.\"",
                        Lands = "\"You're welcome. That's enough.\"",
                        Even = "\"Mm.\"",
                        Grates = "\"Don't make it weird.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. Nothing changes.\"" },
                },
            },
            new TalkRow
            {
                Id = "guarded.rom.open.7", Pool = TalkPools.Guarded, Kind = TalkKind.Opening, When = new TalkGate { Spark = 30 },
                Open = "(after a long silence) \"I don't let people in. You're in. I'm not sure when that happened.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I'm glad I'm in.\"",
                        Lands = "\"So am I. I think.\"",
                        Even = "\"Hm. Me too.\"",
                        Grates = "\"Don't make me regret it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I won't take it lightly.\"",
                        Lands = "\"I know you won't. That's why.\"",
                        Even = "\"Good.\"",
                        Grates = "\"See that you don't.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Are you sure you want me in?\"",
                        Lands = "\"No. Yes. Ask me again later.\"",
                        Even = "\"I'm getting there.\"",
                        Grates = "\"Don't ask me that.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. Fine. Good, even.\"" },
                },
            },
            new TalkRow
            {
                Id = "guarded.rom.open.8", Pool = TalkPools.Guarded, Kind = TalkKind.Opening, When = new TalkGate { Spark = 30 },
                Open = "(sitting very still beside you) \"If I said something, you'd have to promise not to make it a scene.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"No scene. Promise.\"",
                        Lands = "\"Good. Soon, then. Not tonight.\"",
                        Even = "\"All right. Soon.\"",
                        Grates = "\"You already are.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Say it.\"",
                        Lands = "\"Not yet. I'm close.\"",
                        Even = "\"Soon.\"",
                        Grates = "\"Don't push.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Whenever you're ready.\"",
                        Lands = "\"Thank you. That helps more than you know.\"",
                        Even = "\"Mm. Soon.\"",
                        Grates = "\"I might never be.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"All right. That's still a lot from me.\"" },
                },
            },
            new TalkRow
            {
                Id = "guarded.rom.open.9", Pool = TalkPools.Guarded, Kind = TalkKind.Opening, When = new TalkGate { Spark = 30 },
                Open = "(looking at you a moment longer than usual) \"I've run out of reasons to keep you at arm's length.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"Then stop trying.\"",
                        Lands = "\"Maybe I will.\" (a slow breath)",
                        Even = "\"I'm thinking about it.\"",
                        Grates = "\"Don't tell me what to do.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'll take that.\"",
                        Lands = "\"It's yours. Don't lose it.\"",
                        Even = "\"Good.\"",
                        Grates = "\"That's all you get today.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"You had reasons?\"",
                        Lands = "\"A list. It got short.\"",
                        Even = "\"Several.\"",
                        Grates = "\"Forget it.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends. Nothing changes.\"" },
                },
            },
            new TalkRow
            {
                Id = "guarded.rom.softno.1", Pool = TalkPools.Guarded, Kind = TalkKind.SoftNo,
                Open = "(steady, not unkind) \"No. I like you. Not like that. I'd rather say it once and clearly.\"",
            },
            new TalkRow
            {
                Id = "guarded.rom.softno.2", Pool = TalkPools.Guarded, Kind = TalkKind.SoftNo,
                Open = "(a short shake of the head) \"That's not what this is for me. You're one of the few I trust. Let's keep that.\"",
            },
            new TalkRow
            {
                Id = "guarded.rom.softno.3", Pool = TalkPools.Guarded, Kind = TalkKind.SoftNo,
                Open = "(quietly) \"I'm going to say no. Not because of you. It just isn't there. Friends.\"",
            },
            new TalkRow
            {
                Id = "guarded.rom.line.1", Pool = TalkPools.Guarded, Kind = TalkKind.LineDrawn,
                Open = "(holding up a hand, calm) \"No. That's not something I do in here. We're fine. Let's leave it there.\"",
            },
            new TalkRow
            {
                Id = "guarded.rom.line.2", Pool = TalkPools.Guarded, Kind = TalkKind.LineDrawn,
                Open = "(flatly, but not cruel) \"I don't mix that with this game. Friends, if you want. Nothing else.\"",
            },
            new TalkRow
            {
                Id = "guarded.rom.line.3", Pool = TalkPools.Guarded, Kind = TalkKind.LineDrawn,
                Open = "(a short shake of the head) \"Don't. I like talking to you. Keep it that.\"",
            },
            new TalkRow
            {
                Id = "guarded.rom.declare.1", Pool = TalkPools.Guarded, Kind = TalkKind.Declaration,
                Open = "(very quietly, eyes on the floor) \"I don't say things like this. I want us to be together. There. I said it.\"",
                Replies = new[]
                {
                    new TalkReply { Choice = TalkChoice.Accept, Line = "\"Yes. I want that too.\"", Lands = "(a breath out) \"Good. Okay. Good.\"" },
                    new TalkReply { Choice = TalkChoice.NotYet, Line = "\"Not yet. Is that all right?\"", Lands = "\"It's all right. I can wait. I'm good at waiting.\"" },
                    new TalkReply { Choice = TalkChoice.NotLikeThat, Line = "\"I don't feel the same.\"", Lands = "\"All right. Thank you for not lying.\"" },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Can we stay friends?\"", Lands = "\"We can. Nothing changes. I mean that.\"" },
                },
            },
            new TalkRow
            {
                Id = "guarded.rom.declare.2", Pool = TalkPools.Guarded, Kind = TalkKind.Declaration,
                Open = "(after a long pause) \"I'm bad at this. You and me. Together. If you want.\"",
                Replies = new[]
                {
                    new TalkReply { Choice = TalkChoice.Accept, Line = "\"I want.\"", Lands = "\"Then that's that.\" (barely hiding a smile)" },
                    new TalkReply { Choice = TalkChoice.NotYet, Line = "\"Give me time?\"", Lands = "\"Take it. I'm not going anywhere.\"" },
                    new TalkReply { Choice = TalkChoice.NotLikeThat, Line = "\"I'm sorry. Not like that.\"", Lands = "\"Fine. I'd rather know.\"" },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Friends. Just friends.\"", Lands = "\"Friends. All right. That's still a lot from me.\"" },
                },
            },
            new TalkRow
            {
                Id = "guarded.rom.declare.3", Pool = TalkPools.Guarded, Kind = TalkKind.Declaration,
                Open = "(barely above a whisper) \"I've decided I'd rather take the risk with you than not. Be with me.\"",
                Replies = new[]
                {
                    new TalkReply { Choice = TalkChoice.Accept, Line = "\"Yes.\"", Lands = "\"Good. Don't make a fuss about it.\"" },
                    new TalkReply { Choice = TalkChoice.NotYet, Line = "\"Not yet.\"", Lands = "\"Then later. I'll ask once more.\"" },
                    new TalkReply { Choice = TalkChoice.NotLikeThat, Line = "\"I can't. I'm sorry.\"", Lands = "\"Understood. Let's not talk about it again.\"" },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"I'd rather be friends.\"", Lands = "\"Friends. Fine. Good, even.\"" },
                },
            },
            new TalkRow
            {
                Id = "guarded.rom.yes.1", Pool = TalkPools.Guarded, Kind = TalkKind.Accepts,
                Open = "(a long look, then a nod) \"Yes. I wasn't going to say it first. I'm glad you did.\"",
            },
            new TalkRow
            {
                Id = "guarded.rom.yes.2", Pool = TalkPools.Guarded, Kind = TalkKind.Accepts,
                Open = "(quietly) \"Yes. Don't make me say it twice. Yes.\"",
            },
            new TalkRow
            {
                Id = "guarded.rom.yes.3", Pool = TalkPools.Guarded, Kind = TalkKind.Accepts,
                Open = "(the smallest smile) \"I was hoping you would. Yes.\"",
            },
            new TalkRow
            {
                Id = "guarded.rom.no.1", Pool = TalkPools.Guarded, Kind = TalkKind.Refuses,
                Open = "(steady) \"No. I'm sorry. I care about you, and that's exactly why I won't pretend.\"",
            },
            new TalkRow
            {
                Id = "guarded.rom.no.2", Pool = TalkPools.Guarded, Kind = TalkKind.Refuses,
                Open = "(after a long breath) \"I can't. It isn't there for me. I'd rather you hear it from me now.\"",
            },
            new TalkRow
            {
                Id = "guarded.rom.no.3", Pool = TalkPools.Guarded, Kind = TalkKind.Refuses,
                Open = "(gently, for once) \"No. You deserve someone who doesn't have to think about it.\"",
            },
            new TalkRow
            {
                Id = "guarded.rom.couple.1", Pool = TalkPools.Guarded, Kind = TalkKind.Couple,
                Open = "(sitting with you on the stairs, shoulder to shoulder) \"This is enough. I don't need the house to know. I just need this.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Me too.\"",
                        Lands = "\"Good.\"",
                        Even = "\"Mm.\"",
                        Grates = "\"Don't make it a thing.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Let's keep it low-key this week.\"",
                        Lands = "\"Good. I prefer quiet.\"",
                        Even = "\"Fine.\"",
                        Grates = "\"I'm always low-key.\"",
                    },
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"I think we should end it. I'm sorry.\"", Lands = "(a long breath) \"All right. Thank you for saying it to me first.\"" },
                },
            },
            new TalkRow
            {
                Id = "guarded.rom.couple.2", Pool = TalkPools.Guarded, Kind = TalkKind.Couple,
                Open = "(saving you a spot without a word, then) \"I keep a list of people I trust. It's short. You're at the top.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You're at the top of mine.\"",
                        Lands = "\"Hm. Good.\"",
                        Even = "\"Mm. Good.\"",
                        Grates = "\"Don't get sentimental.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Let's keep it low-key this week.\"",
                        Lands = "\"Smart.\"",
                        Even = "\"Fine.\"",
                        Grates = "\"Already am.\"",
                    },
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"I need us to be just friends again.\"", Lands = "\"Fine. I understand. It's not fine. It will be.\"" },
                },
            },
            new TalkRow
            {
                Id = "guarded.rom.couple.3", Pool = TalkPools.Guarded, Kind = TalkKind.Couple,
                Open = "(handing you their hoodie in the cold yard) \"Take it. I'm not cold. I'm always cold. Take it anyway.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Thank you.\"",
                        Lands = "\"Don't mention it. Really.\"",
                        Even = "\"Mm.\"",
                        Grates = "\"Just take it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Let's keep it low-key this week.\"",
                        Lands = "\"Good. Fewer eyes on us.\"",
                        Even = "\"Fine.\"",
                        Grates = "\"It's a hoodie, not a headline.\"",
                    },
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"I want to end this. Kindly.\"", Lands = "\"Then keep the hoodie. I mean it. We're fine.\"" },
                },
            },
            new TalkRow
            {
                Id = "guarded.rom.ex.1", Pool = TalkPools.Guarded, Kind = TalkKind.Ex,
                Open = "(a nod, nothing more) \"We're fine. I need some distance for a while. That's all.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I hope we're okay.\"",
                        Lands = "\"We are. Give it time.\"",
                        Even = "\"Mm.\"",
                        Grates = "\"Not now.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Take all the time you need.\"",
                        Lands = "\"Thanks.\"",
                        Even = "\"Fine.\"",
                        Grates = "\"I don't need permission.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I still want us to be friends.\"",
                        Lands = "\"Maybe. Later.\"",
                        Even = "\"Mm.\"",
                        Grates = "\"Not yet.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rom.ex.2", Pool = TalkPools.Guarded, Kind = TalkKind.Ex,
                Open = "(eyes on the cards) \"Don't sit there. Not yet. Soon, maybe.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I hope we're okay.\"",
                        Lands = "\"We are.\"",
                        Even = "\"Mostly.\"",
                        Grates = "\"Leave it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Take all the time you need.\"",
                        Lands = "\"Good.\"",
                        Even = "\"Mm.\"",
                        Grates = "\"I said I'm fine.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I still want us to be friends.\"",
                        Lands = "\"Later. Maybe.\"",
                        Even = "\"Mm.\"",
                        Grates = "\"No.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rom.ex.3", Pool = TalkPools.Guarded, Kind = TalkKind.Ex,
                Open = "(flat, but not cold) \"I'm back to keeping my own company. It's what I'm good at.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I hope we're okay.\"",
                        Lands = "\"Fine. We are.\"",
                        Even = "\"Mm.\"",
                        Grates = "\"Let's not.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Take all the time you need.\"",
                        Lands = "\"Thank you.\"",
                        Even = "\"Mm.\"",
                        Grates = "\"I don't need time.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I still want us to be friends.\"",
                        Lands = "\"Maybe. Not yet.\"",
                        Even = "\"Mm.\"",
                        Grates = "\"No.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rom.jealous.1", Pool = TalkPools.Guarded, Kind = TalkKind.Jealousy,
                Open = "(waiting in the dark hallway) \"I heard something. About you and someone else. I want to hear it from you.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"It wasn't what it looked like.\"",
                        Lands = "\"Fine. I believe you.\"",
                        Even = "\"Then what was it?\"",
                        Grates = "\"It looked like that.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You're the one I want. I mean it.\"",
                        Lands = "\"Hm. Okay.\"",
                        Even = "\"Prove it.\"",
                        Grates = "\"Words.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You're listening to gossip.\"",
                        Lands = "\"Maybe.\"",
                        Even = "\"Gossip, with a source.\"",
                        Grates = "\"Don't.\"",
                    },
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"Maybe we should end this. Kindly.\"", Lands = "\"Fine. Kindly. Sure.\"" },
                },
            },
            new TalkRow
            {
                Id = "guarded.rom.jealous.2", Pool = TalkPools.Guarded, Kind = TalkKind.Jealousy,
                Open = "(cold, very quiet) \"You were charming someone else. I trusted you. Explain.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"It wasn't what it looked like.\"",
                        Lands = "\"Fine. I'll drop it.\"",
                        Even = "\"Mm.\"",
                        Grates = "\"Then explain it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You're the one I want. I mean it.\"",
                        Lands = "\"All right.\"",
                        Even = "\"We'll see.\"",
                        Grates = "\"Show me.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You're listening to gossip.\"",
                        Lands = "\"Maybe. I don't trust easy.\"",
                        Even = "\"Mm.\"",
                        Grates = "\"I heard it twice.\"",
                    },
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"Maybe we should end this. Kindly.\"", Lands = "\"All right. I'd rather know.\"" },
                },
            },
            new TalkRow
            {
                Id = "guarded.rom.jealous.3", Pool = TalkPools.Guarded, Kind = TalkKind.Jealousy,
                Open = "(not looking at you) \"People talk. Usually I don't listen. This time I did.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"It wasn't what it looked like.\"",
                        Lands = "\"Fine.\"",
                        Even = "\"Mm.\"",
                        Grates = "\"Something happened.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You're the one I want. I mean it.\"",
                        Lands = "\"Okay. I needed that.\"",
                        Even = "\"We'll see.\"",
                        Grates = "\"Words.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You're listening to gossip.\"",
                        Lands = "\"Maybe. I don't usually listen.\"",
                        Even = "\"Mm.\"",
                        Grates = "\"Don't call it gossip.\"",
                    },
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"Maybe we should end this. Kindly.\"", Lands = "\"Fine. We're done, then.\"" },
                },
            },
            new TalkRow
            {
                Id = "guarded.rom.react1.1", Pool = TalkPools.Guarded, Kind = TalkKind.Reaction, Reaction = 1,
                Open = "(passing you in the hallway, flat) \"You two aren't subtle. Thought someone should tell you.\"",
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
                        Lands = "\"No. Just obvious.\"",
                        Even = "\"Mm.\"",
                        Grates = "\"For your game, yes.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who else has noticed?\"",
                        Lands = "\"All of them.\"",
                        Even = "\"Most.\"",
                        Grates = "\"Everyone.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rom.react1.2", Pool = TalkPools.Guarded, Kind = TalkKind.Reaction, Reaction = 1,
                Open = "(without looking up) \"You two. Not subtle. At all.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Subtle was never the plan.\"",
                        Lands = "\"Mm. Obviously.\"",
                        Even = "\"Fine.\"",
                        Grates = "\"It's working against you.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"We're happy. Is that so bad?\"",
                        Lands = "\"No. Just loud.\"",
                        Even = "\"No.\"",
                        Grates = "\"For your game, maybe.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who else has noticed?\"",
                        Lands = "\"Everyone.\"",
                        Even = "\"Most.\"",
                        Grates = "\"Everyone. Obviously.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rom.react1.3", Pool = TalkPools.Guarded, Kind = TalkKind.Reaction, Reaction = 1,
                Open = "(dry) \"If you're hiding it, you're bad at it. You two aren't subtle.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Subtle was never the plan.\"",
                        Lands = "\"Clearly.\"",
                        Even = "\"Fine.\"",
                        Grates = "\"It shows.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"We're happy. Is that so bad?\"",
                        Lands = "\"No. Be careful.\"",
                        Even = "\"No.\"",
                        Grates = "\"Could be.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who else has noticed?\"",
                        Lands = "\"The house.\"",
                        Even = "\"Some.\"",
                        Grates = "\"All of them.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rom.react2.1", Pool = TalkPools.Guarded, Kind = TalkKind.Reaction, Reaction = 2,
                Open = "(low) \"Careful. A pair is a target. I'd split up at the next vote, if I were you.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I know. We're ready for it.\"",
                        Lands = "\"Good.\"",
                        Even = "\"Hope so.\"",
                        Grates = "\"You're not.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who's coming for us?\"",
                        Lands = "\"Watch the quiet ones.\"",
                        Even = "\"Can't say.\"",
                        Grates = "\"Not telling.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Thanks for the warning.\"",
                        Lands = "\"Mm.\"",
                        Even = "\"Sure.\"",
                        Grates = "\"Just remember it.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rom.react2.2", Pool = TalkPools.Guarded, Kind = TalkKind.Reaction, Reaction = 2,
                Open = "(quietly, by the door) \"A pair is a target. Everyone in here can count votes.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I know. We're ready for it.\"",
                        Lands = "\"Good. Stay that way.\"",
                        Even = "\"Mm.\"",
                        Grates = "\"Doubt it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who's coming for us?\"",
                        Lands = "\"Anyone who can count.\"",
                        Even = "\"Somebody.\"",
                        Grates = "\"Not my business.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Thanks for the warning.\"",
                        Lands = "\"Mm.\"",
                        Even = "\"Sure.\"",
                        Grates = "\"Use it.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.rom.react2.3", Pool = TalkPools.Guarded, Kind = TalkKind.Reaction, Reaction = 2,
                Open = "(without emotion) \"Careful. A pair is a target. Don't say I didn't warn you.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I know. We're ready for it.\"",
                        Lands = "\"Good.\"",
                        Even = "\"Mm.\"",
                        Grates = "\"Nobody is.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who's coming for us?\"",
                        Lands = "\"Look at who's smiling.\"",
                        Even = "\"Hard to say.\"",
                        Grates = "\"Not saying.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Thanks for the warning.\"",
                        Lands = "\"Mm.\"",
                        Even = "\"Fine.\"",
                        Grates = "\"Don't waste it.\"",
                    },
                },
            },
        };
    }
}
