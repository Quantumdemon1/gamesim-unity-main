namespace Gamesim.Simulation
{
    /// <summary>
    /// Free talk, the spark class pool's romance talks (slice S3, talk rules 3; ruling R1: the talks are C# tables). The talks
    /// builder fills <see cref="Rows"/>: rows of the romance kinds (TalkKind.FlirtLine to TalkKind.Reaction), Pool = TalkPools.Spark,
    /// in the row form of TalkRow and TalkReply (a choice reply carries TalkReply.Choice and no tone). Tame for everyone: a kiss
    /// is the most a line may say, and none is described. American spelling.
    /// </summary>
    public static class TalkTableSparkRomance
    {
        public static readonly TalkRow[] Rows =
        {
            new TalkRow
            {
                Id = "spark.rom.flirt.1", Pool = TalkPools.Spark, Kind = TalkKind.FlirtLine,
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I like it when you find me first.\"",
                        Lands = "\"Good. I'll keep finding you, then.\"",
                        Even = "\"Ha. Okay. Noted.\"",
                        Grates = "\"Smooth. A little too smooth.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.rom.flirt.2", Pool = TalkPools.Spark, Kind = TalkKind.FlirtLine,
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"You make this house a lot less boring.\"",
                        Lands = "\"I try. You make it easy.\"",
                        Even = "\"Somebody has to keep it fun.\"",
                        Grates = "\"Wow, okay. Lay it on thicker.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.rom.flirt.3", Pool = TalkPools.Spark, Kind = TalkKind.FlirtLine,
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I was hoping you'd be in here.\"",
                        Lands = "\"Lucky you. I was hoping too.\"",
                        Even = "\"Well, here I am.\"",
                        Grates = "\"Okay, easy there.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.rom.flirt.4", Pool = TalkPools.Spark, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 15 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I saved you a seat. I always do.\"",
                        Lands = "\"I know. Best seat in the house.\"",
                        Even = "\"Thanks. I'll take it.\"",
                        Grates = "\"It's a chair. Calm down.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.rom.flirt.5", Pool = TalkPools.Spark, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 15 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"Talking to you is the best part of my day.\"",
                        Lands = "\"Mine too. Don't tell anyone.\"",
                        Even = "\"Aw. Okay. That's nice.\"",
                        Grates = "\"That's a lot. Before breakfast.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.rom.flirt.6", Pool = TalkPools.Spark, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 15 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"You know I like you, right?\"",
                        Lands = "\"I had a feeling. I like you too.\"",
                        Even = "\"I mean, I hoped so.\"",
                        Grates = "\"Okay, slow down.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.rom.flirt.7", Pool = TalkPools.Spark, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 30 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I think about you more than the game.\"",
                        Lands = "\"Same. It's a problem. A good one.\"",
                        Even = "\"Careful. The game still counts.\"",
                        Grates = "\"Whoa. That's heavy.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.rom.flirt.8", Pool = TalkPools.Spark, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 30 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I want you next to me at the end of this.\"",
                        Lands = "\"Then stay next to me. Simple.\"",
                        Even = "\"One week at a time, okay?\"",
                        Grates = "\"Let's get through Thursday first.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.rom.flirt.9", Pool = TalkPools.Spark, Kind = TalkKind.FlirtLine, When = new TalkGate { Spark = 30 },
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"I don't want to talk to anyone else tonight.\"",
                        Lands = "\"Then don't. Stay right here.\"",
                        Even = "\"Okay. Tonight's yours.\"",
                        Grates = "\"Okay, now you're laying it on.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.rom.open.1", Pool = TalkPools.Spark, Kind = TalkKind.Opening,
                Open = "(leaning on the counter, grinning) \"Okay, was that a line? Because if that was a line, it was a good one.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"It was a line. Did it work?\"",
                        Lands = "\"Maybe. Ask me again tomorrow and find out.\"",
                        Even = "\"Jury's still out. Keep practicing.\"",
                        Grates = "\"Points for trying. Not many.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"You're the one who noticed.\"",
                        Lands = "\"I notice everything. Especially you, apparently.\"",
                        Even = "\"Hard not to. You're standing right there.\"",
                        Grates = "\"Wow. Turning it around on me. Bold.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I just like talking to you.\"",
                        Lands = "\"Good. Same. That's allowed.\"",
                        Even = "\"Talking is fine. Talking is great.\"",
                        Grates = "\"Oh. Okay. Just talking. Sure.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends! Okay. I can do friends. Best friends, even.\"" },
                },
            },
            new TalkRow
            {
                Id = "spark.rom.open.2", Pool = TalkPools.Spark, Kind = TalkKind.Opening,
                Open = "(tossing a grape at you and catching your eye) \"You keep sitting next to me. People are going to start a rumor. I might help them.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"Let them. I like the seat.\"",
                        Lands = "\"Then it's yours. I'll guard it with my life.\"",
                        Even = "\"Fine. Seat's yours. Rumor's mine.\"",
                        Grates = "\"Smooth. Almost too smooth.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"You threw a grape at me.\"",
                        Lands = "\"It's how I say hello now. Get used to it.\"",
                        Even = "\"And you caught it. Impressive.\"",
                        Grates = "\"Okay, no more grapes. Noted.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Rumors are dangerous in here.\"",
                        Lands = "\"Only the boring ones. This one's fun.\"",
                        Even = "\"True. Still. It'd be a nice one.\"",
                        Grates = "\"Wow. Way to kill the mood.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Fine. Friends. I'll still steal your fries.\"" },
                },
            },
            new TalkRow
            {
                Id = "spark.rom.open.3", Pool = TalkPools.Spark, Kind = TalkKind.Opening,
                Open = "(spinning around on the kitchen stool) \"Be honest. Were you just checking if I was in here? Because I was checking if you were.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"Guilty. I was looking for you.\"",
                        Lands = "\"Good answer. Best answer, actually.\"",
                        Even = "\"Ha. Okay. Noted.\"",
                        Grates = "\"Smooth, but I saw you look at the fridge first.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I wanted coffee.\"",
                        Lands = "\"Coffee, sure. And me. Coffee and me.\"",
                        Even = "\"Fine. Coffee wins. This time.\"",
                        Grates = "\"Ouch. Beaten by a mug.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"You were checking? Interesting.\"",
                        Lands = "\"Don't make it a thing. It's a little bit of a thing.\"",
                        Even = "\"I mean, I check on everyone. Mostly you.\"",
                        Grates = "\"Forget I said that. Please.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends it is. No hard feelings. Mostly.\"" },
                },
            },
            new TalkRow
            {
                Id = "spark.rom.open.4", Pool = TalkPools.Spark, Kind = TalkKind.Opening, When = new TalkGate { Spark = 15 },
                Open = "(dropping onto the couch right beside you) \"I saved you the good blanket. Do you know what that costs me in this house? Everything.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"Then I owe you. Name your price.\"",
                        Lands = "\"Sit with me through the whole movie. No leaving early.\"",
                        Even = "\"I'll think of something. Give me a minute.\"",
                        Grates = "\"Easy. It's a blanket, not a proposal.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"That's sweet. Thank you.\"",
                        Lands = "\"I'm very sweet. Write it down.\"",
                        Even = "\"You're welcome. Scoot over.\"",
                        Grates = "\"Sweet? Okay. I was going for charming.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Is this a bribe?\"",
                        Lands = "\"Totally a bribe. Is it working?\"",
                        Even = "\"A small one. A cozy one.\"",
                        Grates = "\"Fine. Give it back. Kidding. Keep it.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends! Okay. I can do friends. Best friends, even.\"" },
                },
            },
            new TalkRow
            {
                Id = "spark.rom.open.5", Pool = TalkPools.Spark, Kind = TalkKind.Opening, When = new TalkGate { Spark = 15 },
                Open = "(catching your hand to pull you into the yard) \"Come on, the stars are out and nobody else is looking. That never happens in here.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"Lead the way.\"",
                        Lands = "\"I knew you'd say that. I was hoping, anyway.\"",
                        Even = "\"Okay. Two minutes. Then they'll notice.\"",
                        Grates = "\"You sound like you're humoring me.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Someone's always looking.\"",
                        Lands = "\"Let them. I'm having a good night.\"",
                        Even = "\"True. Five minutes, then.\"",
                        Grates = "\"Wow. Okay. Back inside, then.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Is this your move?\"",
                        Lands = "\"It's one of them. I've got a few.\"",
                        Even = "\"Maybe. Is it working?\"",
                        Grates = "\"It was. Past tense.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Fine. Friends. I'll still steal your fries.\"" },
                },
            },
            new TalkRow
            {
                Id = "spark.rom.open.6", Pool = TalkPools.Spark, Kind = TalkKind.Opening, When = new TalkGate { Spark = 15 },
                Open = "(sliding a plate of pancakes across to you) \"Made you the round ones. Everyone else gets the weird shapes. Don't tell them.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"So I'm your favorite?\"",
                        Lands = "\"You're getting there. Very round pancakes. Big hint.\"",
                        Even = "\"Favorite pancake person, at least.\"",
                        Grates = "\"Don't push it. It's breakfast.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"These are perfect. Thank you.\"",
                        Lands = "\"See? I can do perfect when it counts.\"",
                        Even = "\"Good. Eat before they get cold.\"",
                        Grates = "\"Perfect. Right. Okay.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Why the special treatment?\"",
                        Lands = "\"Why do you think? Come on. Keep up.\"",
                        Even = "\"Because I felt like it. That's why.\"",
                        Grates = "\"Wow. Okay. Weird shapes for you tomorrow.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends it is. No hard feelings. Mostly.\"" },
                },
            },
            new TalkRow
            {
                Id = "spark.rom.open.7", Pool = TalkPools.Spark, Kind = TalkKind.Opening, When = new TalkGate { Spark = 30 },
                Open = "(quietly, for once) \"Hey. I don't usually get nervous. I'm nervous right now. Just so you know.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"Me too. A little.\"",
                        Lands = "\"Good. Then we're both in trouble.\"",
                        Even = "\"Okay. Nervous together. That's something.\"",
                        Grates = "\"You don't look nervous. Unfair.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You don't have to be.\"",
                        Lands = "\"Easy for you to say. You're the reason.\"",
                        Even = "\"I know. I am anyway.\"",
                        Grates = "\"Okay. Forget I said it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"You, nervous? Never.\"",
                        Lands = "\"I know! It's a whole new feeling. Blame yourself.\"",
                        Even = "\"Shocking, right? Don't get used to it.\"",
                        Grates = "\"Fine. Laugh. I'll be over here.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends! Okay. I can do friends. Best friends, even.\"" },
                },
            },
            new TalkRow
            {
                Id = "spark.rom.open.8", Pool = TalkPools.Spark, Kind = TalkKind.Opening, When = new TalkGate { Spark = 30 },
                Open = "(sitting on the edge of the pool, feet in the water) \"Everyone keeps asking me what we are. I keep saying nothing. I don't love saying nothing.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"So say something else.\"",
                        Lands = "\"Okay. Maybe I will. Soon. Don't rush me.\"",
                        Even = "\"I'm thinking about it. Really thinking.\"",
                        Grates = "\"Easy for you. You don't have to say it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"What do you want to say?\"",
                        Lands = "\"Something true. I'm working up to it.\"",
                        Even = "\"Something better than nothing.\"",
                        Grates = "\"Forget it. Not tonight.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Then don't say nothing.\"",
                        Lands = "\"Wow. Direct. I like that about you.\"",
                        Even = "\"Fair. I'll work on it.\"",
                        Grates = "\"Okay. Pressure. Thanks.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Fine. Friends. I'll still steal your fries.\"" },
                },
            },
            new TalkRow
            {
                Id = "spark.rom.open.9", Pool = TalkPools.Spark, Kind = TalkKind.Opening, When = new TalkGate { Spark = 30 },
                Open = "(holding your gaze a second too long) \"I'm not good at waiting. You've probably noticed. I'm trying really hard right now.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Flirt, Line = "\"Waiting for what?\"",
                        Lands = "\"You know exactly what. Don't make me say it yet.\"",
                        Even = "\"For you to catch up. You're close.\"",
                        Grates = "\"Oh, come on. You know.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I like that you're trying.\"",
                        Lands = "\"Then I'll keep trying. A little longer.\"",
                        Even = "\"Good. It's hard. Just so you know.\"",
                        Grates = "\"Trying is not my favorite thing.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"You? Patient?\"",
                        Lands = "\"I know. A miracle. You did that.\"",
                        Even = "\"Barely. Don't test it.\"",
                        Grates = "\"Okay, now I'm not patient.\"",
                    },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends.\"", Lands = "\"Friends it is. No hard feelings. Mostly.\"" },
                },
            },
            new TalkRow
            {
                Id = "spark.rom.softno.1", Pool = TalkPools.Spark, Kind = TalkKind.SoftNo,
                Open = "(gently, with a soft laugh) \"Oh, you're great. Really. I like you. Just not like that. Still friends? Please say yes.\"",
            },
            new TalkRow
            {
                Id = "spark.rom.softno.2", Pool = TalkPools.Spark, Kind = TalkKind.SoftNo,
                Open = "(squeezing your shoulder) \"I love hanging out with you. But that's all it is for me. I didn't want to string you along.\"",
            },
            new TalkRow
            {
                Id = "spark.rom.softno.3", Pool = TalkPools.Spark, Kind = TalkKind.SoftNo,
                Open = "(wincing a little, kind) \"Okay, I'm flattered. Honestly. But no. You're my favorite person to laugh with, and I want to keep that.\"",
            },
            new TalkRow
            {
                Id = "spark.rom.line.1", Pool = TalkPools.Spark, Kind = TalkKind.LineDrawn,
                Open = "(holding up both hands, laughing) \"Ooh, no. Love the energy. Not my thing in here. Friends? Friends are great.\"",
            },
            new TalkRow
            {
                Id = "spark.rom.line.2", Pool = TalkPools.Spark, Kind = TalkKind.LineDrawn,
                Open = "(kindly) \"I'm going to stop you right there. I'm here for the game and the fun, not that. No hard feelings?\"",
            },
            new TalkRow
            {
                Id = "spark.rom.line.3", Pool = TalkPools.Spark, Kind = TalkKind.LineDrawn,
                Open = "(shaking their head with a grin) \"That's not where this goes for me. But you're still my favorite person to talk to.\"",
            },
            new TalkRow
            {
                Id = "spark.rom.declare.1", Pool = TalkPools.Spark, Kind = TalkKind.Declaration,
                Open = "(grabbing your hands, too excited to sit still) \"Okay, I can't hold it in. I like you. A lot. Like, more than friends. Be with me?\"",
                Replies = new[]
                {
                    new TalkReply { Choice = TalkChoice.Accept, Line = "\"Yes. Obviously yes.\"", Lands = "\"Yes! Okay! Don't tell anyone yet. I'm telling everyone.\"" },
                    new TalkReply { Choice = TalkChoice.NotYet, Line = "\"Not yet. Give me a little time.\"", Lands = "\"Okay. Time. I can do time. Not too much time.\"" },
                    new TalkReply { Choice = TalkChoice.NotLikeThat, Line = "\"I don't feel it like that.\"", Lands = "\"Oh. Okay. That stings. But thank you for being honest.\"" },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Can we just be friends?\"", Lands = "\"Yeah. Friends. I can do friends. Best friends, even.\"" },
                },
            },
            new TalkRow
            {
                Id = "spark.rom.declare.2", Pool = TalkPools.Spark, Kind = TalkKind.Declaration,
                Open = "(blurting it out by the pool) \"I'm just going to say it. I want us to be a thing. A real thing. What do you say?\"",
                Replies = new[]
                {
                    new TalkReply { Choice = TalkChoice.Accept, Line = "\"I say yes.\"", Lands = "\"Yes! I knew it. Okay, I hoped it. Same thing.\"" },
                    new TalkReply { Choice = TalkChoice.NotYet, Line = "\"Ask me again soon?\"", Lands = "\"Deal. I'll ask again. I'm very persistent.\"" },
                    new TalkReply { Choice = TalkChoice.NotLikeThat, Line = "\"I'm sorry. Not like that.\"", Lands = "\"Oh. Okay. I'll be fine. Just give me a minute.\"" },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Just friends. Okay?\"", Lands = "\"Okay. Friends. Still my favorite person in here.\"" },
                },
            },
            new TalkRow
            {
                Id = "spark.rom.declare.3", Pool = TalkPools.Spark, Kind = TalkKind.Declaration,
                Open = "(sitting very close on the hammock) \"I don't do subtle. You know that. So. Us. Together. Officially. Yes?\"",
                Replies = new[]
                {
                    new TalkReply { Choice = TalkChoice.Accept, Line = "\"Officially, yes.\"", Lands = "\"Officially! That's my new favorite word.\"" },
                    new TalkReply { Choice = TalkChoice.NotYet, Line = "\"Not yet. Soon, maybe.\"", Lands = "\"Soon. I like soon. I'm holding you to soon.\"" },
                    new TalkReply { Choice = TalkChoice.NotLikeThat, Line = "\"That's not how I feel.\"", Lands = "\"Right. Okay. Thanks for not lying to me.\"" },
                    new TalkReply { Choice = TalkChoice.JustFriends, Line = "\"Let's stay friends.\"", Lands = "\"Friends. Sure. I'll still save you the good blanket.\"" },
                },
            },
            new TalkRow
            {
                Id = "spark.rom.yes.1", Pool = TalkPools.Spark, Kind = TalkKind.Accepts,
                Open = "(practically bouncing) \"Yes! Was that even a question? Yes. I've been waiting for you to ask.\"",
            },
            new TalkRow
            {
                Id = "spark.rom.yes.2", Pool = TalkPools.Spark, Kind = TalkKind.Accepts,
                Open = "(laughing, pulling you into a hug) \"Finally! Yes. A hundred times yes. Okay, maybe a little less than that.\"",
            },
            new TalkRow
            {
                Id = "spark.rom.yes.3", Pool = TalkPools.Spark, Kind = TalkKind.Accepts,
                Open = "(grinning ear to ear) \"You beat me to it. I was going to ask tonight. Yes. Obviously.\"",
            },
            new TalkRow
            {
                Id = "spark.rom.no.1", Pool = TalkPools.Spark, Kind = TalkKind.Refuses,
                Open = "(face falling, gentle) \"Oh no. I like you so much. Just not like that. I'm sorry. I didn't mean to lead you on.\"",
            },
            new TalkRow
            {
                Id = "spark.rom.no.2", Pool = TalkPools.Spark, Kind = TalkKind.Refuses,
                Open = "(quiet for once) \"I can't say yes. I thought about it, I really did. But no. Can we still be us?\"",
            },
            new TalkRow
            {
                Id = "spark.rom.no.3", Pool = TalkPools.Spark, Kind = TalkKind.Refuses,
                Open = "(sitting down slowly) \"You're amazing. But that's a no. I'd rather tell you now than fake it.\"",
            },
            new TalkRow
            {
                Id = "spark.rom.couple.1", Pool = TalkPools.Spark, Kind = TalkKind.Couple,
                Open = "(stealing a bite off your plate) \"This is the best part of being together. Free food. And you, I guess. Mostly the food.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"I'll guard my plate better.\"",
                        Lands = "\"You won't. That's why I like you.\"",
                        Even = "\"You never do.\"",
                        Grates = "\"Wow. Okay. My plate, then.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Let's keep it low-key this week.\"",
                        Lands = "\"Smart. Low-key. I can try low-key.\"",
                        Even = "\"Low-key. Sure. I'll whisper.\"",
                        Grates = "\"Low-key? Have you met me?\"",
                    },
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"Can we talk? I think we should end this.\"", Lands = "\"Oh. Wow. Okay. I didn't see that coming. I'm glad you told me.\"" },
                },
            },
            new TalkRow
            {
                Id = "spark.rom.couple.2", Pool = TalkPools.Spark, Kind = TalkKind.Couple,
                Open = "(leaning on your shoulder on the couch) \"Everyone's watching us. Let them. We're the best thing in this house.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"We really are.\"",
                        Lands = "\"See? You get it. That's why we work.\"",
                        Even = "\"We're okay.\"",
                        Grates = "\"Don't be smug about it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Let's keep it low-key this week.\"",
                        Lands = "\"Fine. Just this week. Then I'm loud again.\"",
                        Even = "\"I'll try.\"",
                        Grates = "\"Why? Are you embarrassed of me?\"",
                    },
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"I think we should go back to being friends.\"", Lands = "\"Okay. That hurts. But okay. Thank you for saying it kindly.\"" },
                },
            },
            new TalkRow
            {
                Id = "spark.rom.couple.3", Pool = TalkPools.Spark, Kind = TalkKind.Couple,
                Open = "(drawing a little heart on the fogged mirror) \"Don't look at that. That's not for you. It's totally for you.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"I saw it. Too late.\"",
                        Lands = "\"Good. It was a test. You passed.\"",
                        Even = "\"Okay. Fine. Partly.\"",
                        Grates = "\"Rude. It was a nice heart.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Let's keep it low-key this week.\"",
                        Lands = "\"Okay. No more mirror art. For now.\"",
                        Even = "\"Sure. Quiet week.\"",
                        Grates = "\"Ugh. Fine.\"",
                    },
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"I need to end things. I'm sorry.\"", Lands = "\"Oh. Right. Okay. I'll be okay. Thanks for being kind about it.\"" },
                },
            },
            new TalkRow
            {
                Id = "spark.rom.ex.1", Pool = TalkPools.Spark, Kind = TalkKind.Ex,
                Open = "(a little too bright) \"Hey. So, we're good. I'm good. Totally good. I just need a day of being good from over here.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I hope we're okay.\"",
                        Lands = "\"Yeah. We're okay. I promise.\"",
                        Even = "\"We will be.\"",
                        Grates = "\"Can we not right now?\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Take all the time you need.\"",
                        Lands = "\"Thanks. A day, tops.\"",
                        Even = "\"Okay.\"",
                        Grates = "\"I'm fine. Really.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I still want us to be friends.\"",
                        Lands = "\"Me too. Give me a minute.\"",
                        Even = "\"Sure. Friends.\"",
                        Grates = "\"Not today, okay?\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.rom.ex.2", Pool = TalkPools.Spark, Kind = TalkKind.Ex,
                Open = "(passing you in the hallway) \"It's weird. I keep turning to tell you stuff. I'll stop. Eventually.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I hope we're okay.\"",
                        Lands = "\"We are. It's just weird.\"",
                        Even = "\"Mostly.\"",
                        Grates = "\"Do we have to talk about it?\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Take all the time you need.\"",
                        Lands = "\"Thanks. That helps.\"",
                        Even = "\"Okay.\"",
                        Grates = "\"I don't need time. I'm fine.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I still want us to be friends.\"",
                        Lands = "\"I'd like that too.\"",
                        Even = "\"Okay. Soon.\"",
                        Grates = "\"Easy for you to say.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.rom.ex.3", Pool = TalkPools.Spark, Kind = TalkKind.Ex,
                Open = "(sitting across the table, not beside) \"New seat. It's fine. I like this seat. It's a great seat.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I hope we're okay.\"",
                        Lands = "\"We're okay. New seat and all.\"",
                        Even = "\"Sure.\"",
                        Grates = "\"Let's not.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Take all the time you need.\"",
                        Lands = "\"Appreciate it.\"",
                        Even = "\"Yeah.\"",
                        Grates = "\"I said I'm fine.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I still want us to be friends.\"",
                        Lands = "\"Friends. Yeah. I want that.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"Not yet.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.rom.jealous.1", Pool = TalkPools.Spark, Kind = TalkKind.Jealousy,
                Open = "(arms crossed by the bedroom door) \"So I heard you've been very charming with someone else. Want to tell me about it?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"It wasn't what it looked like.\"",
                        Lands = "\"Okay. I believe you. I think.\"",
                        Even = "\"Then what was it?\"",
                        Grates = "\"It looked like exactly that.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You're the one I want. I mean it.\"",
                        Lands = "\"Okay. Okay. Say it again.\"",
                        Even = "\"You'd better.\"",
                        Grates = "\"Words are cheap in here.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You're listening to gossip.\"",
                        Lands = "\"Maybe. Still. Be careful.\"",
                        Even = "\"Gossip with a grain of truth.\"",
                        Grates = "\"Don't flip this on me.\"",
                    },
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"Maybe we should end this. Kindly.\"", Lands = "\"Oh. Okay. Wow. Okay. Kindly. Sure.\"" },
                },
            },
            new TalkRow
            {
                Id = "spark.rom.jealous.2", Pool = TalkPools.Spark, Kind = TalkKind.Jealousy,
                Open = "(not quite smiling) \"Funny thing. Half the house says you've been sweet on someone who isn't me. Is that true?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"It wasn't what it looked like.\"",
                        Lands = "\"Okay. Good. I needed that.\"",
                        Even = "\"Fine. I'll let it go.\"",
                        Grates = "\"Then why is everyone saying it?\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You're the one I want. I mean it.\"",
                        Lands = "\"Good. Because I meant it too.\"",
                        Even = "\"Okay.\"",
                        Grates = "\"Prove it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You're listening to gossip.\"",
                        Lands = "\"Maybe it is. I just hate it.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"Gossip I heard from three people.\"",
                    },
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"Maybe we should end this. Kindly.\"", Lands = "\"Fine. If that's what you want.\"" },
                },
            },
            new TalkRow
            {
                Id = "spark.rom.jealous.3", Pool = TalkPools.Spark, Kind = TalkKind.Jealousy,
                Open = "(cornering you in the kitchen) \"I'm not mad. I'm a little mad. Who were you being so charming with?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"It wasn't what it looked like.\"",
                        Lands = "\"Okay. I overreacted. A little.\"",
                        Even = "\"Fine.\"",
                        Grates = "\"It looked like something.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You're the one I want. I mean it.\"",
                        Lands = "\"Okay. I believe you.\"",
                        Even = "\"Mm.\"",
                        Grates = "\"Sure. Okay.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You're listening to gossip.\"",
                        Lands = "\"Okay, fair. I hate gossip too.\"",
                        Even = "\"Maybe.\"",
                        Grates = "\"Don't call me gullible.\"",
                    },
                    new TalkReply { Choice = TalkChoice.EndItKindly, Line = "\"Maybe we should end this. Kindly.\"", Lands = "\"Okay. That's fair. I hate it, but it's fair.\"" },
                },
            },
            new TalkRow
            {
                Id = "spark.rom.react1.1", Pool = TalkPools.Spark, Kind = TalkKind.Reaction, Reaction = 1,
                Open = "(dropping onto the couch across from you) \"You two aren't subtle. Like, at all. The whole house has noticed.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Subtle was never the plan.\"",
                        Lands = "\"Clearly! Honestly, good for you.\"",
                        Even = "\"Ha. Okay, fair.\"",
                        Grates = "\"Well, it shows.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"We're happy. Is that so bad?\"",
                        Lands = "\"Not at all. Be happy.\"",
                        Even = "\"Nobody said bad.\"",
                        Grates = "\"Just saying. Everyone sees.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who else has noticed?\"",
                        Lands = "\"Everyone. The cameras too.\"",
                        Even = "\"Most of the house.\"",
                        Grates = "\"Who hasn't?\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.rom.react1.2", Pool = TalkPools.Spark, Kind = TalkKind.Reaction, Reaction = 1,
                Open = "(grinning, wiggling eyebrows) \"So! You and your favorite person. You two aren't subtle. Everyone knows.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Subtle was never the plan.\"",
                        Lands = "\"Ha! Good. Own it.\"",
                        Even = "\"Sure.\"",
                        Grates = "\"Okay, but tone it down.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"We're happy. Is that so bad?\"",
                        Lands = "\"It's great! I'm jealous.\"",
                        Even = "\"It's fine.\"",
                        Grates = "\"It's a lot, though.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who else has noticed?\"",
                        Lands = "\"Literally everybody.\"",
                        Even = "\"Most of us.\"",
                        Grates = "\"Seriously? Everyone.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.rom.react1.3", Pool = TalkPools.Spark, Kind = TalkKind.Reaction, Reaction = 1,
                Open = "(laughing) \"Do you two think you're hiding it? You're not. It's honestly kind of adorable.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Subtle was never the plan.\"",
                        Lands = "\"Good for you two.\"",
                        Even = "\"Okay, fair.\"",
                        Grates = "\"Well, it's working against you.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"We're happy. Is that so bad?\"",
                        Lands = "\"Never. Enjoy it.\"",
                        Even = "\"It's fine.\"",
                        Grates = "\"Bad for your game, maybe.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who else has noticed?\"",
                        Lands = "\"Everyone at breakfast.\"",
                        Even = "\"Some of us.\"",
                        Grates = "\"Everyone. Obviously.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.rom.react2.1", Pool = TalkPools.Spark, Kind = TalkKind.Reaction, Reaction = 2,
                Open = "(lowering their voice) \"Careful. A pair is a target. Two votes, one block. Somebody's going to do the math.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I know. We're ready for it.\"",
                        Lands = "\"Good. Then you've got a plan.\"",
                        Even = "\"I hope so.\"",
                        Grates = "\"Ready? You don't look ready.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who's coming for us?\"",
                        Lands = "\"Not me. Ask around.\"",
                        Even = "\"Can't say.\"",
                        Grates = "\"Wouldn't tell you if I knew.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Thanks for the warning.\"",
                        Lands = "\"Anytime. I'm rooting for you.\"",
                        Even = "\"Sure.\"",
                        Grates = "\"Just don't say I didn't warn you.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.rom.react2.2", Pool = TalkPools.Spark, Kind = TalkKind.Reaction, Reaction = 2,
                Open = "(serious for a second) \"I'm happy for you. Truly. But a pair is a target. Watch your back this week.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I know. We're ready for it.\"",
                        Lands = "\"Good. Stay sharp.\"",
                        Even = "\"Okay.\"",
                        Grates = "\"Nobody's ready for that.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who's coming for us?\"",
                        Lands = "\"Not sure yet. Keep your ears open.\"",
                        Even = "\"Somebody.\"",
                        Grates = "\"I'm not your spy.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Thanks for the warning.\"",
                        Lands = "\"Of course. Be safe.\"",
                        Even = "\"Yeah.\"",
                        Grates = "\"Don't waste it.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.rom.react2.3", Pool = TalkPools.Spark, Kind = TalkKind.Reaction, Reaction = 2,
                Open = "(tapping the table) \"Fair warning. Everyone sees a pair as a target. Don't get comfortable.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I know. We're ready for it.\"",
                        Lands = "\"Then I'll stop worrying. A little.\"",
                        Even = "\"Okay.\"",
                        Grates = "\"I doubt it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Who's coming for us?\"",
                        Lands = "\"Watch the quiet ones.\"",
                        Even = "\"Hard to say.\"",
                        Grates = "\"Not telling.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Thanks for the warning.\"",
                        Lands = "\"Anytime. Seriously.\"",
                        Even = "\"Sure.\"",
                        Grates = "\"Just be careful.\"",
                    },
                },
            },
        };
    }
}
