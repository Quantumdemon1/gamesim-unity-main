namespace Gamesim.Simulation
{
    /// <summary>
    /// Free talk, the steady class pool's romance talks (slice S3, talk rules 3; ruling R1: the talks are C# tables). The talks
    /// builder fills <see cref="Rows"/>: rows of the romance kinds (TalkKind.FlirtLine to TalkKind.Reaction), Pool = TalkPools.Steady,
    /// in the row form of TalkRow and TalkReply (a choice reply carries TalkReply.Choice and no tone). Tame for everyone: a kiss
    /// is the most a line may say, and none is described. American spelling.
    /// </summary>
    public static class TalkTableSteadyRomance
    {
        public static readonly TalkRow[] Rows =
        {
            new TalkRow
            {
                Id = "steady.rom.flirt.1", Pool = TalkPools.Steady, Kind = TalkKind.FlirtLine,
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I like it when you find me first.\"",
                        Lands = "\"Then I'll keep coming in first.\"",
                        Even = "\"Well. All right.\"",
                        Grates = "\"Hm. Let's see.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.rom.flirt.2", Pool = TalkPools.Steady, Kind = TalkKind.FlirtLine,
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"You make this house a lot less boring.\"",
                        Lands = "\"You do the same for me.\"",
                        Even = "\"I try to.\"",
                        Grates = "\"Boring is safer in here.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.rom.flirt.3", Pool = TalkPools.Steady, Kind = TalkKind.FlirtLine,
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I was hoping you'd be in here.\"",
                        Lands = "\"I was hoping too.\"",
                        Even = "\"Well, here I am.\"",
                        Grates = "\"I just came for a glass of water.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.rom.flirt.4", Pool = TalkPools.Steady, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 15 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I saved you a seat. I always do.\"",
                        Lands = "\"I know. I always notice.\"",
                        Even = "\"Thank you.\"",
                        Grates = "\"You don't have to.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.rom.flirt.5", Pool = TalkPools.Steady, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 15 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"Talking to you is the best part of my day.\"",
                        Lands = "\"Mine too. More than the comps.\"",
                        Even = "\"That's kind.\"",
                        Grates = "\"That's a lot to put on a chat.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.rom.flirt.6", Pool = TalkPools.Steady, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 15 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"You know I like you, right?\"",
                        Lands = "\"I'd hoped. I like you too.\"",
                        Even = "\"I had a feeling.\"",
                        Grates = "\"Let's not rush it.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.rom.flirt.7", Pool = TalkPools.Steady, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 30 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I think about you more than the game.\"",
                        Lands = "\"Same. I didn't plan on it.\"",
                        Even = "\"The game still matters.\"",
                        Grates = "\"Careful with that.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.rom.flirt.8", Pool = TalkPools.Steady, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 30 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I want you next to me at the end of this.\"",
                        Lands = "\"Then let's make sure of it.\"",
                        Even = "\"One week at a time.\"",
                        Grates = "\"Let's get through this week first.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.rom.flirt.9", Pool = TalkPools.Steady, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 30 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I don't want to talk to anyone else tonight.\"",
                        Lands = "\"Then stay. I'm not going anywhere.\"",
                        Even = "\"All right. Tonight.\"",
                        Grates = "\"It's late. Let's not.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.rom.open.1", Pool = TalkPools.Steady, Kind = TalkKind.Opening,
                Open = "(pausing with the dish towel over one shoulder) \"Was that you being sweet on me? I can't always tell with you.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"A little. Is that all right?\"",
                        Lands = "\"It's more than all right. Just took me by surprise.\"",
                        Even = "\"I think so. I'll get back to you.\"",
                        Grates = "\"Hm. Let's see how the week goes.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I meant it kindly.\"",
                        Lands = "\"I know you did. That's what I like.\"",
                        Even = "\"Well, it was kind. Thank you.\"",
                        Grates = "\"Kind is good. Kind is safe.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"You'll figure it out.\"",
                        Lands = "\"I usually do. Takes me a while.\"",
                        Even = "\"I'll be watching for clues, then.\"",
                        Grates = "\"Don't make me guess. I'm bad at guessing.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends it is. Nothing changes at breakfast.\"" },
                },
            },
            new TalkRow
            {
                Id = "steady.rom.open.2", Pool = TalkPools.Steady, Kind = TalkKind.Opening,
                Open = "(handing you the last cookie without a word, then a small smile) \"Don't make it weird. I just thought you'd want it.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"Too late. It's a little weird. I like it.\"",
                        Lands = "\"Okay. A little weird is fine by me.\"",
                        Even = "\"Fine. A little. Eat your cookie.\"",
                        Grates = "\"Now I'm embarrassed. Thanks.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"That's really thoughtful.\"",
                        Lands = "\"I notice what you like. That's all.\"",
                        Even = "\"It's a cookie. But you're welcome.\"",
                        Grates = "\"It's just a cookie. Really.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Why me?\"",
                        Lands = "\"Because I wanted it to be you. Simple as that.\"",
                        Even = "\"You looked like you needed it.\"",
                        Grates = "\"No reason. Forget it.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"All right. Friends. I meant it about the coffee.\"" },
                },
            },
            new TalkRow
            {
                Id = "steady.rom.open.3", Pool = TalkPools.Steady, Kind = TalkKind.Opening,
                Open = "(sitting on the step beside you, close enough to share the warmth) \"Cold out here. I could go in. I'm not going in.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"Good. Stay.\"",
                        Lands = "\"Okay. Staying. Don't make a big deal of it.\"",
                        Even = "\"For a bit. Then bed.\"",
                        Grates = "\"Don't get used to it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"People will talk.\"",
                        Lands = "\"People always talk. Let them.\"",
                        Even = "\"Maybe. I'm staying anyway.\"",
                        Grates = "\"You're right. I should go in.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'm glad you're here.\"",
                        Lands = "\"Me too. That's new for me.\"",
                        Even = "\"It's nice. Quiet.\"",
                        Grates = "\"It's just a step. Don't read into it.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"That's fine. Friends are worth a lot in here.\"" },
                },
            },
            new TalkRow
            {
                Id = "steady.rom.open.4", Pool = TalkPools.Steady, Kind = TalkKind.Opening, When = new TalkGate { Spark = 15 },
                Open = "(refilling your water before you notice it's empty) \"I keep doing things like this. I've stopped pretending it's an accident.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I hope you never stop.\"",
                        Lands = "\"Then I won't. Easy promise.\"",
                        Even = "\"I'll think about it. Probably not.\"",
                        Grates = "\"Let's not get carried away.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I noticed. Thank you.\"",
                        Lands = "\"Good. I wanted you to notice.\"",
                        Even = "\"You're welcome. Drink up.\"",
                        Grates = "\"It's just water. Really.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"So it's on purpose.\"",
                        Lands = "\"Every time. Don't tell anyone.\"",
                        Even = "\"Maybe. Drink your water.\"",
                        Grates = "\"All right, you caught me. Let's move on.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends it is. Nothing changes at breakfast.\"" },
                },
            },
            new TalkRow
            {
                Id = "steady.rom.open.5", Pool = TalkPools.Steady, Kind = TalkKind.Opening, When = new TalkGate { Spark = 15 },
                Open = "(walking the yard with you after lights out) \"I don't do this with anybody else. The walking. The talking. Just you.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I'm glad it's just me.\"",
                        Lands = "\"So am I. More than I thought I would be.\"",
                        Even = "\"It is. Just you. For now.\"",
                        Grates = "\"Don't make me regret saying it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Is that a good thing?\"",
                        Lands = "\"It's a very good thing. I'm sure of that.\"",
                        Even = "\"I think so. I'm still working it out.\"",
                        Grates = "\"I hope so. Ask me later.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"What does that mean for us?\"",
                        Lands = "\"It means I'm not in a hurry. But I'm not going anywhere.\"",
                        Even = "\"I'm figuring that out. Give me time.\"",
                        Grates = "\"Let's not name it yet.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"All right. Friends. I meant it about the coffee.\"" },
                },
            },
            new TalkRow
            {
                Id = "steady.rom.open.6", Pool = TalkPools.Steady, Kind = TalkKind.Opening, When = new TalkGate { Spark = 15 },
                Open = "(fixing the collar of your jacket without thinking, then stopping) \"Sorry. Habit. Is it a habit? I think it's turning into one.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"It can be a habit.\"",
                        Lands = "\"All right then. A good habit.\"",
                        Even = "\"We'll see. Hold still.\"",
                        Grates = "\"Let's not make it one just yet.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I don't mind at all.\"",
                        Lands = "\"Good. I don't mind either. Not one bit.\"",
                        Even = "\"Okay. Just checking.\"",
                        Grates = "\"Still. I'll ask first next time.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Do you fix everyone's collar?\"",
                        Lands = "\"Only yours. That's the problem.\"",
                        Even = "\"Only when they're crooked. Yours always is.\"",
                        Grates = "\"Forget it. It's nothing.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"That's fine. Friends are worth a lot in here.\"" },
                },
            },
            new TalkRow
            {
                Id = "steady.rom.open.7", Pool = TalkPools.Steady, Kind = TalkKind.Opening, When = new TalkGate { Spark = 30 },
                Open = "(sitting beside you on the hammock, steady voice) \"I've been thinking about you more than the game. That's never happened before.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"Same here.\"",
                        Lands = "\"Then maybe we should do something about it.\"",
                        Even = "\"Good. I'm glad it's not just me.\"",
                        Grates = "\"Careful. The game still matters.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"That means a lot.\"",
                        Lands = "\"It should. I don't say it lightly.\"",
                        Even = "\"It's true. That's all.\"",
                        Grates = "\"Just thought you should know.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Is that a risk for you?\"",
                        Lands = "\"Maybe. I think you're worth it.\"",
                        Even = "\"Probably. I'm thinking it through.\"",
                        Grates = "\"Everything's a risk in here.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends it is. Nothing changes at breakfast.\"" },
                },
            },
            new TalkRow
            {
                Id = "steady.rom.open.8", Pool = TalkPools.Steady, Kind = TalkKind.Opening, When = new TalkGate { Spark = 30 },
                Open = "(drying the same plate twice) \"If I asked you something serious, would you give me a straight answer? Not yet. But soon.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"For you? Always.\"",
                        Lands = "\"Good. Hold onto that. I'll ask soon.\"",
                        Even = "\"All right. Soon, then.\"",
                        Grates = "\"Don't promise what you can't keep.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Ask me now.\"",
                        Lands = "\"Not yet. I want to get it right.\"",
                        Even = "\"Soon. I said soon.\"",
                        Grates = "\"Don't rush me. Please.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Is this a quiz?\"",
                        Lands = "\"Sort of. The only one that matters.\"",
                        Even = "\"A short one. One question.\"",
                        Grates = "\"Never mind. Forget I said it.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"All right. Friends. I meant it about the coffee.\"" },
                },
            },
            new TalkRow
            {
                Id = "steady.rom.open.9", Pool = TalkPools.Steady, Kind = TalkKind.Opening, When = new TalkGate { Spark = 30 },
                Open = "(quiet, not looking away) \"I like the way this week has gone. With you, I mean. I'd like more weeks like it.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"So would I. A lot more.\"",
                        Lands = "\"Then let's make sure we get them.\"",
                        Even = "\"Good. One week at a time.\"",
                        Grates = "\"Let's get through this one first.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Me too. It's been easy.\"",
                        Lands = "\"Easy is rare in here. I don't take it lightly.\"",
                        Even = "\"It has. Nice change.\"",
                        Grates = "\"Easy for now. We'll see.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"The game might not let us.\"",
                        Lands = "\"Then we'll play it smart. Together.\"",
                        Even = "\"Maybe not. I'd still like it.\"",
                        Grates = "\"You're right. Forget I said it.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"That's fine. Friends are worth a lot in here.\"" },
                },
            },
            new TalkRow
            {
                Id = "steady.rom.softno.1", Pool = TalkPools.Steady, Kind = TalkKind.SoftNo,
                Open = "(setting the mug down, kind) \"I like you. I do. Not like that, though. I'd rather be honest now than sorry later.\"",
            },
            new TalkRow
            {
                Id = "steady.rom.softno.2", Pool = TalkPools.Steady, Kind = TalkKind.SoftNo,
                Open = "(with a gentle shake of the head) \"You're one of my favorite people in here. But it stops at friends for me. I hope that's okay.\"",
            },
            new TalkRow
            {
                Id = "steady.rom.softno.3", Pool = TalkPools.Steady, Kind = TalkKind.SoftNo,
                Open = "(warm but clear) \"I'm flattered. I'm just not there with you, and I don't think I will be. Still my friend?\"",
            },
            new TalkRow
            {
                Id = "steady.rom.line.1", Pool = TalkPools.Steady, Kind = TalkKind.LineDrawn,
                Open = "(with a kind, firm shake of the head) \"I'm going to stop that before it starts. Not in here. Not for me. We're good otherwise.\"",
            },
            new TalkRow
            {
                Id = "steady.rom.line.2", Pool = TalkPools.Steady, Kind = TalkKind.LineDrawn,
                Open = "(gently) \"I appreciate it. But I don't do that in this house. I hope we can still be friends.\"",
            },
            new TalkRow
            {
                Id = "steady.rom.line.3", Pool = TalkPools.Steady, Kind = TalkKind.LineDrawn,
                Open = "(setting down the cards) \"Let's keep it friendly. That's where I'm comfortable. No harm done.\"",
            },
            new TalkRow
            {
                Id = "steady.rom.declare.1", Pool = TalkPools.Steady, Kind = TalkKind.Declaration,
                Open = "(taking a breath, then plain) \"I'm no good at speeches. I like you. More than a friend. I'd like us to be together.\"",
                Replies = new[]
                {
                    new TalkReply { Choice = TalkChoice.Accept, Line = "\"I'd like that too.\"", Lands = "\"Good. Okay. Good. I'm glad I said it.\"" },
                    new TalkReply { Choice = TalkChoice.NotYet, Line = "\"Not yet. Can we wait?\"", Lands = "\"We can wait. I'm patient. You know that.\"" },
                    new TalkReply { Choice = TalkChoice.NotLikeThat, Line = "\"I don't feel that way.\"", Lands = "\"All right. Thank you for being straight with me.\"" },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Let's stay friends.\"", Lands = "\"Friends it is. Nothing changes at breakfast.\"" },
                },
            },
            new TalkRow
            {
                Id = "steady.rom.declare.2", Pool = TalkPools.Steady, Kind = TalkKind.Declaration,
                Open = "(sitting across from you at the empty table) \"I've thought about this a long time. I want to be with you. Properly. If you want that.\"",
                Replies = new[]
                {
                    new TalkReply { Choice = TalkChoice.Accept, Line = "\"I want that.\"", Lands = "\"Then that's settled. I feel better already.\"" },
                    new TalkReply { Choice = TalkChoice.NotYet, Line = "\"Give me a little longer.\"", Lands = "\"Take the time you need. I'll ask again.\"" },
                    new TalkReply { Choice = TalkChoice.NotLikeThat, Line = "\"I'm sorry. Not like that.\"", Lands = "\"That's all right. Hurts a little. I'll manage.\"" },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"I'd rather we were just friends.\"", Lands = "\"Then just friends. I meant it about the coffee.\"" },
                },
            },
            new TalkRow
            {
                Id = "steady.rom.declare.3", Pool = TalkPools.Steady, Kind = TalkKind.Declaration,
                Open = "(after the dishes, drying their hands) \"Here's the thing. I care about you. I'd like us to be a couple. What do you think?\"",
                Replies = new[]
                {
                    new TalkReply { Choice = TalkChoice.Accept, Line = "\"I think yes.\"", Lands = "\"Yes. All right. I was worried for nothing.\"" },
                    new TalkReply { Choice = TalkChoice.NotYet, Line = "\"Ask me again in a bit?\"", Lands = "\"I will. I'm not going anywhere.\"" },
                    new TalkReply { Choice = TalkChoice.NotLikeThat, Line = "\"I'm not there. I'm sorry.\"", Lands = "\"Understood. I'd rather know.\"" },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Can we keep it friends?\"", Lands = "\"We can. You're still my person at the sink.\"" },
                },
            },
            new TalkRow
            {
                Id = "steady.rom.yes.1", Pool = TalkPools.Steady, Kind = TalkKind.Accepts,
                Open = "(a slow smile) \"Yes. I was hoping you'd ask. I was working up to asking you.\"",
            },
            new TalkRow
            {
                Id = "steady.rom.yes.2", Pool = TalkPools.Steady, Kind = TalkKind.Accepts,
                Open = "(squeezing your hand) \"Of course. Yes. I've been sure about you for a while now.\"",
            },
            new TalkRow
            {
                Id = "steady.rom.yes.3", Pool = TalkPools.Steady, Kind = TalkKind.Accepts,
                Open = "(nodding, steady) \"Yes. Let's do this properly. You and me.\"",
            },
            new TalkRow
            {
                Id = "steady.rom.no.1", Pool = TalkPools.Steady, Kind = TalkKind.Refuses,
                Open = "(gently) \"I'm sorry. I care about you. Just not that way. I'd rather tell you true than tell you easy.\"",
            },
            new TalkRow
            {
                Id = "steady.rom.no.2", Pool = TalkPools.Steady, Kind = TalkKind.Refuses,
                Open = "(after a long pause) \"No. Not because of anything you did. I'm just not there. I don't think I will be.\"",
            },
            new TalkRow
            {
                Id = "steady.rom.no.3", Pool = TalkPools.Steady, Kind = TalkKind.Refuses,
                Open = "(setting down the towel) \"I can't say yes to that. But I don't want to lose you as a friend.\"",
            },
            new TalkRow
            {
                Id = "steady.rom.couple.1", Pool = TalkPools.Steady, Kind = TalkKind.Couple,
                Open = "(handing you your coffee, just how you like it) \"Morning. Still can't believe this worked out. I'm glad it did.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"So am I.\"",
                        Lands = "\"Good. Drink it while it's hot.\"",
                        Even = "\"Good.\"",
                        Grates = "\"Don't make it a speech.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Let's keep it low-key this week.\"",
                        Lands = "\"Fair. Low-key it is.\"",
                        Even = "\"All right.\"",
                        Grates = "\"Why? Something wrong?\"",
                    },
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"I think we should end things. I'm sorry.\"", Lands = "\"Oh. All right. I appreciate you telling me to my face.\"" },
                },
            },
            new TalkRow
            {
                Id = "steady.rom.couple.2", Pool = TalkPools.Steady, Kind = TalkKind.Couple,
                Open = "(sitting close on the backyard bench) \"Everyone has an opinion about us. I only care about yours.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Mine's a good one.\"",
                        Lands = "\"Then that's all I need to hear.\"",
                        Even = "\"Good to hear.\"",
                        Grates = "\"Mine's a little biased.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Let's keep it low-key this week.\"",
                        Lands = "\"Sensible. I'll keep my distance at dinner.\"",
                        Even = "\"Okay.\"",
                        Grates = "\"Are you embarrassed?\"",
                    },
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"I need to step back. Just friends.\"", Lands = "\"All right. That's hard to hear. Thank you for being kind about it.\"" },
                },
            },
            new TalkRow
            {
                Id = "steady.rom.couple.3", Pool = TalkPools.Steady, Kind = TalkKind.Couple,
                Open = "(folding your laundry with theirs) \"I did your load with mine. That's about as romantic as I get. Don't laugh.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"It's perfect.\"",
                        Lands = "\"See? You get me.\"",
                        Even = "\"Glad you think so.\"",
                        Grates = "\"Don't laugh.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Let's keep it low-key this week.\"",
                        Lands = "\"All right. I'll do my own laundry.\"",
                        Even = "\"Sure.\"",
                        Grates = "\"It's laundry. Nobody watches laundry.\"",
                    },
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"I don't think this is working.\"", Lands = "\"I see. Okay. I'd rather know than wonder.\"" },
                },
            },
            new TalkRow
            {
                Id = "steady.rom.ex.1", Pool = TalkPools.Steady, Kind = TalkKind.Ex,
                Open = "(polite, a little stiff) \"We're all right. I just need a few days to get used to it. That's all.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I hope we're okay.\"",
                        Lands = "\"We are. Give it a few days.\"",
                        Even = "\"We will be.\"",
                        Grates = "\"Not just now.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Take all the time you need.\"",
                        Lands = "\"Thank you. I will.\"",
                        Even = "\"All right.\"",
                        Grates = "\"I don't need much.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I still want us to be friends.\"",
                        Lands = "\"So do I. Truly.\"",
                        Even = "\"In time.\"",
                        Grates = "\"Not yet.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.rom.ex.2", Pool = TalkPools.Steady, Kind = TalkKind.Ex,
                Open = "(passing the salt without looking up) \"No hard feelings. Some soft ones. They'll pass.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I hope we're okay.\"",
                        Lands = "\"We're all right.\"",
                        Even = "\"Mostly.\"",
                        Grates = "\"Let's not, today.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Take all the time you need.\"",
                        Lands = "\"I appreciate that.\"",
                        Even = "\"Okay.\"",
                        Grates = "\"I'm fine.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I still want us to be friends.\"",
                        Lands = "\"I'd like that.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"Give it a week.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.rom.ex.3", Pool = TalkPools.Steady, Kind = TalkKind.Ex,
                Open = "(quietly, at the sink) \"I still make two coffees in the morning. I'll stop. Give me time.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I hope we're okay.\"",
                        Lands = "\"We are. Just a habit to break.\"",
                        Even = "\"We will be.\"",
                        Grates = "\"Not now.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Take all the time you need.\"",
                        Lands = "\"Thank you. Really.\"",
                        Even = "\"Sure.\"",
                        Grates = "\"Mm.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I still want us to be friends.\"",
                        Lands = "\"Friends. I want that.\"",
                        Even = "\"In time.\"",
                        Grates = "\"Not yet.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.rom.jealous.1", Pool = TalkPools.Steady, Kind = TalkKind.Jealousy,
                Open = "(waiting by the door, arms folded) \"I heard you were sweet-talking someone else. I'd like to hear it from you.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"It wasn't what it looked like.\"",
                        Lands = "\"All right. I believe you.\"",
                        Even = "\"Then explain it.\"",
                        Grates = "\"It sounded like what it was.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You're the one I want. I mean it.\"",
                        Lands = "\"Okay. Thank you for saying it.\"",
                        Even = "\"I hope so.\"",
                        Grates = "\"Saying isn't showing.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You're listening to gossip.\"",
                        Lands = "\"Maybe. Still hurt.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"Don't turn this around.\"",
                    },
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"Maybe we should end this. Kindly.\"", Lands = "\"All right. If that's what you want. Kindly.\"" },
                },
            },
            new TalkRow
            {
                Id = "steady.rom.jealous.2", Pool = TalkPools.Steady, Kind = TalkKind.Jealousy,
                Open = "(calm, but hurt) \"People are saying you've been charming somebody who isn't me. Is there something I should know?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"It wasn't what it looked like.\"",
                        Lands = "\"Good. That's all I needed.\"",
                        Even = "\"All right.\"",
                        Grates = "\"Then why does everyone say so?\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You're the one I want. I mean it.\"",
                        Lands = "\"I believe you.\"",
                        Even = "\"Okay.\"",
                        Grates = "\"Show me, then.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You're listening to gossip.\"",
                        Lands = "\"Fair. I should've asked first.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"It came from more than one person.\"",
                    },
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"Maybe we should end this. Kindly.\"", Lands = "\"I see. Thank you for saying it to my face.\"" },
                },
            },
            new TalkRow
            {
                Id = "steady.rom.jealous.3", Pool = TalkPools.Steady, Kind = TalkKind.Jealousy,
                Open = "(setting the dish down hard) \"I'm not one for drama. But I heard things. Tell me straight.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"It wasn't what it looked like.\"",
                        Lands = "\"All right. I'll take your word.\"",
                        Even = "\"Okay.\"",
                        Grates = "\"Something happened.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You're the one I want. I mean it.\"",
                        Lands = "\"Okay. I needed to hear that.\"",
                        Even = "\"Mm.\"",
                        Grates = "\"We'll see.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You're listening to gossip.\"",
                        Lands = "\"You're right. I'm sorry.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"That's not an answer.\"",
                    },
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"Maybe we should end this. Kindly.\"", Lands = "\"Okay. That's hard. But okay.\"" },
                },
            },
            new TalkRow
            {
                Id = "steady.rom.react1.1", Pool = TalkPools.Steady, Kind = TalkKind.Reaction, Reaction = 1,
                Open = "(drying a glass, eyebrows raised) \"You two aren't subtle. I'm only saying it because nobody else will to your face.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Subtle was never the plan.\"",
                        Lands = "\"No, it wasn't. Good for you.\"",
                        Even = "\"Fair enough.\"",
                        Grates = "\"It shows.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"We're happy. Is that so bad?\"",
                        Lands = "\"Not at all. I'm glad.\"",
                        Even = "\"It's fine.\"",
                        Grates = "\"Just mind your game.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who else has noticed?\"",
                        Lands = "\"Everyone at the table.\"",
                        Even = "\"Most folks.\"",
                        Grates = "\"Everyone.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.rom.react1.2", Pool = TalkPools.Steady, Kind = TalkKind.Reaction, Reaction = 1,
                Open = "(sitting down with a knowing look) \"So. You two. Not subtle. Everyone at breakfast was talking about it.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Subtle was never the plan.\"",
                        Lands = "\"Fair. Enjoy it.\"",
                        Even = "\"All right.\"",
                        Grates = "\"Well, they're talking.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"We're happy. Is that so bad?\"",
                        Lands = "\"It's nice to see.\"",
                        Even = "\"It's fine.\"",
                        Grates = "\"Not for your game.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who else has noticed?\"",
                        Lands = "\"Nearly everyone.\"",
                        Even = "\"A few.\"",
                        Grates = "\"All of them.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.rom.react1.3", Pool = TalkPools.Steady, Kind = TalkKind.Reaction, Reaction = 1,
                Open = "(kindly) \"I'm happy for you two. But you're not subtle. The whole house knows.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Subtle was never the plan.\"",
                        Lands = "\"Good. Be happy.\"",
                        Even = "\"Okay.\"",
                        Grates = "\"It shows.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"We're happy. Is that so bad?\"",
                        Lands = "\"Not one bit.\"",
                        Even = "\"No.\"",
                        Grates = "\"Could be, for your game.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who else has noticed?\"",
                        Lands = "\"The whole house.\"",
                        Even = "\"Some.\"",
                        Grates = "\"Everyone.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.rom.react2.1", Pool = TalkPools.Steady, Kind = TalkKind.Reaction, Reaction = 2,
                Open = "(low, by the sink) \"Careful. A pair is a target. Two votes is a lot to some people.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I know. We're ready for it.\"",
                        Lands = "\"Good. Keep a plan.\"",
                        Even = "\"I hope so.\"",
                        Grates = "\"Nobody's ever ready.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who's coming for us?\"",
                        Lands = "\"I'm not sure. Keep your ears open.\"",
                        Even = "\"Can't say.\"",
                        Grates = "\"Not my place.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Thanks for the warning.\"",
                        Lands = "\"Anytime. Look after yourself.\"",
                        Even = "\"Sure.\"",
                        Grates = "\"Just be careful.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.rom.react2.2", Pool = TalkPools.Steady, Kind = TalkKind.Reaction, Reaction = 2,
                Open = "(serious) \"I'd keep it quiet if I were you. A pair is a target. I've seen it happen.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I know. We're ready for it.\"",
                        Lands = "\"Good. Stay that way.\"",
                        Even = "\"All right.\"",
                        Grates = "\"I hope you are.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who's coming for us?\"",
                        Lands = "\"The ones who smile the most.\"",
                        Even = "\"Hard to say.\"",
                        Grates = "\"I'm not saying.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Thanks for the warning.\"",
                        Lands = "\"Of course.\"",
                        Even = "\"Sure.\"",
                        Grates = "\"Don't waste it.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "steady.rom.react2.3", Pool = TalkPools.Steady, Kind = TalkKind.Reaction, Reaction = 2,
                Open = "(handing you a towel) \"Word of advice. A pair is a target. Spread your friendships around.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I know. We're ready for it.\"",
                        Lands = "\"Good. Then I can stop worrying.\"",
                        Even = "\"Okay.\"",
                        Grates = "\"I doubt it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who's coming for us?\"",
                        Lands = "\"Watch who's counting votes.\"",
                        Even = "\"Somebody.\"",
                        Grates = "\"I won't say.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Thanks for the warning.\"",
                        Lands = "\"Anytime.\"",
                        Even = "\"Mm.\"",
                        Grates = "\"Mind it.\"",
                    },
                },
            },
        };
    }
}
