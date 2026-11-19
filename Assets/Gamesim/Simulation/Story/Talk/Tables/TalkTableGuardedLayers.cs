namespace Gamesim.Simulation
{
    /// <summary>
    /// Free talk, the guarded pool's layers talks (ruling R1: the talks are C# tables). Written by a converter from the
    /// talk source, ids kept; edit the text here by hand. American spelling.
    /// </summary>
    public static class TalkTableGuardedLayers
    {
        public static readonly TalkRow[] Rows =
        {
            new TalkRow
            {
                Id = "guarded.l1.unpacked", Pool = TalkPools.Guarded, Kind = TalkKind.Layer, Layer = 1, Reveals = true,
                When = new TalkGate { First = true },
                Open = "(move-in night, folding clothes into a drawer one square at a time) \"You're the fourth person to ask about me. The first who waited till I'd unpacked.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I can wait longer. Finish.\"",
                        Lands = "\"No. You waited. That's enough. One thing, then it's your turn.\"",
                        Even = "\"Noted. The short version.\"",
                        Grates = "\"Don't hover, though. Waiting and hovering aren't the same thing.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I'm asking now so I don't mix you up with anyone.\"",
                        Lands = "\"Honest. Most people pretend they'll remember. Here's something to file me under.\"",
                        Even = "\"Fair. One thing to file me under.\"",
                        Grates = "\"So I'm a name to memorize. Try again tomorrow.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"That's a lot of folding. What did you bring, a filing system?\"",
                        Lands = "\"Labels. For the drawers. Laugh. Then I'll tell you something.\"",
                        Even = "\"Close enough. You want the bag or the person?\"",
                        Grates = "\"Things I chose carefully. Like who I talk to on night one.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.l1.quiet", Pool = TalkPools.Guarded, Kind = TalkKind.Layer, Layer = 1, Reveals = true,
                When = new TalkGate { First = true },
                Open = "(at the end of the kitchen island on day one, watching the room, not eating) \"Everyone's talking at once. You aren't. Why?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Listening's cheaper than talking.\"",
                        Lands = "\"It is. Same reason I'm over here. Since you didn't ask, here's one thing.\"",
                        Even = "\"Cheap and useful. All right. One thing about me.\"",
                        Grates = "\"That's a strategy answer on day one. I'll keep mine, then.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Didn't want to crowd anyone. You included.\"",
                        Lands = "\"Thank you. That's rarer than you'd think. Here.\"",
                        Even = "\"Good instinct. I'll give you something for it.\"",
                        Grates = "\"Then why are you standing next to me?\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"I'm saving it for someone worth it.\"",
                        Lands = "\"Big claim. I'll go first and see if I qualify.\"",
                        Even = "\"We'll see. One thing, as a test.\"",
                        Grates = "\"That's a line. I don't trade for lines.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.l1.mug", Pool = TalkPools.Guarded, Kind = TalkKind.Layer, Layer = 1, Reveals = true,
                When = new TalkGate { Layer = 1 },
                Open = "(holding a mug with a name taped to it) \"Someone used my mug. I labeled it. I'd like to know who reads a label and ignores it.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"It's not about the mug, is it?\"",
                        Lands = "\"No. It's what the mug tells me about them. That's how I work. Since you saw it.\"",
                        Even = "\"Maybe not. I notice small things. Most of me is that.\"",
                        Grates = "\"Don't read me over a mug. I'll do the reading.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"I'll guard it for you. For a fee.\"",
                        Lands = "\"What's the fee? No. Don't. I'll pay you in something true instead.\"",
                        Even = "\"Funny. Fine. Small payment.\"",
                        Grates = "\"It isn't a joke to me. That's the problem with this house.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Use mine till yours turns up.\"",
                        Lands = "\"That's decent. All right. Since you're being decent.\"",
                        Even = "\"Thanks. I'll wash it. I always do.\"",
                        Grates = "\"I don't want your mug. I want to know who took mine.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.l1.pillow", Pool = TalkPools.Guarded, Kind = TalkKind.Layer, Layer = 1, Reveals = true,
                When = new TalkGate { Layer = 1 },
                Open = "(sitting on the one made bed in the room) \"You can sit. Not on the pillow.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "(sit at the far end, nowhere near the pillow)",
                        Lands = "\"Good. You listen. That's worth a straight answer.\"",
                        Even = "\"Thank you. Go on. One question.\"",
                        Grates = "\"Now it feels like an interview. Never mind.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"What happens if I sit on the pillow?\"",
                        Lands = "\"You'd find out how fast I talk when I'm annoyed. Sit. I'll tell you something.\"",
                        Even = "\"Nothing good. Sit. Ask.\"",
                        Grates = "\"Then you'd be someone who sits on pillows. Noted. Permanently.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"You make your bed every morning?\"",
                        Lands = "\"Every morning. It's the one thing in here I control. That, and this.\"",
                        Even = "\"Yes. Ask what you actually came to ask.\"",
                        Grates = "\"You came over here to talk about my bed?\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.l1.method", Pool = TalkPools.Guarded, Kind = TalkKind.Layer, Layer = 1, Reveals = true,
                When = new TalkGate { Layer = 1 },
                Open = "(by the yard door, arms folded) \"You've asked everyone in here something about themselves. Is that a method?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Yes. People like being asked.\"",
                        Lands = "\"Honest. I'd rather that than the act. Ask, then.\"",
                        Even = "\"At least you admit it. One answer.\"",
                        Grates = "\"Then I'm data. I don't feel like being data today.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"No. I want to know who I'm living with.\"",
                        Lands = "\"I believe you. Mostly. Here's something true.\"",
                        Even = "\"Maybe. One thing. I'll watch what you do with it.\"",
                        Grates = "\"Everyone says that. That's what makes it a method.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You just answered a question with a question.\"",
                        Lands = "\"Caught. Habit. All right. A real answer.\"",
                        Even = "\"I do that. Fine. You've earned one.\"",
                        Grates = "\"And you're keeping score. Then we're even. Nothing.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.l1.awake", Pool = TalkPools.Guarded, Kind = TalkKind.Layer, Layer = 1, Reveals = true,
                When = new TalkGate { Layer = 1 },
                Open = "(lights out, the camera tracks humming overhead) \"You're awake. I can hear you not sleeping.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Takes one to know one.\"",
                        Lands = "\"It does. Since we're both up. One thing about me.\"",
                        Even = "\"Mm. One thing, then sleep.\"",
                        Grates = "\"Shh. Some of us are trying.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Can't switch off. You?\"",
                        Lands = "\"Never. I'll tell you a little about why.\"",
                        Even = "\"Same. Quietly, then.\"",
                        Grates = "\"Talking about it won't help either of us.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Sorry. I'll keep it down.\"",
                        Lands = "\"No. Stay up. Some things are easier to say in the dark.\"",
                        Even = "\"It's fine. Since we're up.\"",
                        Grates = "\"You don't have to apologize for breathing. Goodnight.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.l1.silence", Pool = TalkPools.Guarded, Kind = TalkKind.Layer, Layer = 1, Reveals = true,
                When = new TalkGate { Layer = 1 },
                Open = "(washing dishes beside you after a long, easy silence) \"You don't fill silences. I'd noticed.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Neither do you. Who breaks first?\"",
                        Lands = "\"You just did. I'll be generous and go next.\"",
                        Even = "\"Apparently me. Fine. Something small.\"",
                        Grates = "\"Not a contest. You turned it into one.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Silence tells me more than talk does.\"",
                        Lands = "\"Same. So I'll give you something better than silence.\"",
                        Even = "\"Then here's some talk. Measure it.\"",
                        Grates = "\"And now you've told me you're measuring. I'll be quieter.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"It's comfortable. With you, anyway.\"",
                        Lands = "\"It is. That's why I'm about to ruin it. Here.\"",
                        Even = "\"Careful. Don't make it a thing. But all right.\"",
                        Grates = "\"Don't say it. Saying it ends it.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.l1.door", Pool = TalkPools.Guarded, Kind = TalkKind.Layer, Layer = 1, Reveals = true,
                When = new TalkGate { Layer = 1 },
                Open = "(catching you looking at them across the yard) \"You've been watching how I sit. What did you decide?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"That you always sit facing the door.\"",
                        Lands = "\"I do. Most people never notice. Since you did.\"",
                        Even = "\"Old habit. Here's another thing about me, since you're counting.\"",
                        Grates = "\"So you're the one keeping a file. Good to know.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"That you'd be terrible at poker.\"",
                        Lands = "\"I'd be excellent at poker. That's the joke. All right, a real one.\"",
                        Even = "\"Wrong. Here's a tell for free.\"",
                        Grates = "\"Then don't play me.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Nothing yet. Sorry. I'll stop.\"",
                        Lands = "\"Don't stop. Just be less obvious. And here, so you don't have to guess.\"",
                        Even = "\"Fine. One thing so you can stop.\"",
                        Grates = "\"You'll keep doing it. People do.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.l2.photo", Pool = TalkPools.Guarded, Kind = TalkKind.Layer, Layer = 2, Reveals = true,
                When = new TalkGate { Layer = 2 },
                Open = "(straightening the one photo the house allowed them) \"We got one photo. I took an hour choosing. Nobody's asked why this one.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Then I'm asking. Why this one?\"",
                        Lands = "\"Because it's from before I learned to be careful. That's where I'm from.\"",
                        Even = "\"Because it's home. More or less. Here.\"",
                        Grates = "\"Not today. Ask me when it's not the whole kitchen listening.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"An hour's a long time for one photo.\"",
                        Lands = "\"It was. I had to leave the others out. Here's what's in this one.\"",
                        Even = "\"It's a long time. It mattered. That's all I'll say about the hour.\"",
                        Grates = "\"You're counting my hours now?\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"It's on show. You must've wanted people to see it.\"",
                        Lands = "\"You're right, and you're the first to say it. So. The truth about it.\"",
                        Even = "\"Maybe. I wanted one person to see it. Fine. You.\"",
                        Grates = "\"Nothing in here is for show. Not with me.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.l2.phrase", Pool = TalkPools.Guarded, Kind = TalkKind.Layer, Layer = 2, Reveals = true,
                When = new TalkGate { Layer = 2 },
                Open = "(repeating something you said yesterday) \"You say 'back home.' I say 'where I'm from.' People who left say it differently.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Do I get a grade for that?\"",
                        Lands = "\"A pass. Here's mine, then. Since you noticed the difference.\"",
                        Even = "\"Incomplete. All right.\"",
                        Grates = "\"You joke when something's close. I've noticed that too.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"So which are you? Left or still there?\"",
                        Lands = "\"Both. Neither. Here's the honest version.\"",
                        Even = "\"Depends who's asking. You, today. Fine.\"",
                        Grates = "\"That's a big question for a Tuesday.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I hadn't thought about it. You have.\"",
                        Lands = "\"I think about it every day in here. All right.\"",
                        Even = "\"I have. Since you let me.\"",
                        Grates = "\"Don't hand it back to me. I asked you.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.l2.watching", Pool = TalkPools.Guarded, Kind = TalkKind.Layer, Layer = 2, Reveals = true,
                When = new TalkGate { Layer = 2 },
                Open = "(just out of the diary room) \"They asked who I hope is watching. I said one name. I can't stop thinking about it.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You said the name to them. Say it to me.\"",
                        Lands = "\"Fair. They got the name. You get the reason.\"",
                        Even = "\"Not the name. The why, a little.\"",
                        Grates = "\"No. I'm not saying it twice in one day.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You don't have to tell me. I'd listen, though.\"",
                        Lands = "\"I know. That's why I'm going to.\"",
                        Even = "\"All right. Part of it.\"",
                        Grates = "\"Don't do the soft voice. I'm fine.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"You think they're watching right now?\"",
                        Lands = "\"Every night. That's where I come from. Here.\"",
                        Even = "\"Probably. Ask me something about them. One thing.\"",
                        Grates = "\"I'd rather not think about who's watching. Thanks.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.l2.counter", Pool = TalkPools.Guarded, Kind = TalkKind.Layer, Layer = 2, Reveals = true,
                When = new TalkGate { Layer = 2 },
                Open = "(eating standing up at the counter, plate held close) \"Where I grew up you ate standing or you didn't eat. I've sat down twice in here. Both times somebody talked to me.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"So standing is how you skip the table talk.\"",
                        Lands = "\"Partly. You noticed, so you get the other part.\"",
                        Even = "\"Mostly. Here's the rest of why.\"",
                        Grates = "\"It's how I eat. Not everything in here is a move.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"I'll stand too. We'll be the standing table.\"",
                        Lands = "\"Ha. Fine. Two of us is a table. Here, then.\"",
                        Even = "\"Don't make it a club. But fine.\"",
                        Grates = "\"Then I'll sit. Just to be difficult.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Sit down with me. Just once.\"",
                        Lands = "\"Once. Because you asked plainly. Here's where it comes from.\"",
                        Even = "\"Once. Don't make it a habit.\"",
                        Grates = "\"Not tonight. Back up. You're in my elbow room.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.l2.cap", Pool = TalkPools.Guarded, Kind = TalkKind.Layer, Layer = 2, Reveals = true,
                When = new TalkGate { Layer = 2 },
                Open = "(turning a bottle cap over and over in one hand) \"Someone at home did this. I picked it up and never put it down.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I won't ask who.\"",
                        Lands = "\"You can. You've been patient long enough.\"",
                        Even = "\"Thanks. I'll tell you something else instead.\"",
                        Grates = "\"Then don't. It's fine.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You do it when somebody lies at the table. I've seen it.\"",
                        Lands = "\"You've seen it. All right. Then you get this.\"",
                        Even = "\"Maybe. You're sharper than you let on. Here.\"",
                        Grates = "\"Don't tell me what my hands do.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"It suits you. What were they like?\"",
                        Lands = "\"Steady. Like I'm trying to be. Here.\"",
                        Even = "\"Like this, only louder. One thing.\"",
                        Grates = "\"Let's not do the sad part in the yard.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.l2.question", Pool = TalkPools.Guarded, Kind = TalkKind.Layer, Layer = 2, Reveals = true,
                When = new TalkGate { Layer = 2 },
                Open = "(sitting down across from you, very deliberately) \"You've been decent for a while now. You get one question about before. Spend it well.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Who were you, before this house?\"",
                        Lands = "\"Good question. Big one. I'll give you the corner of it.\"",
                        Even = "\"Too big. Here's a corner.\"",
                        Grates = "\"That's not one question. That's all of them.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"I'll save it.\"",
                        Lands = "\"Smart. I'll give you one anyway. Interest.\"",
                        Even = "\"You'll lose it. Fine. One for free.\"",
                        Grates = "\"Then it expires. I don't hold offers.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Can I ask what you'd ask me?\"",
                        Lands = "\"You can't. But that's the right answer. Here.\"",
                        Even = "\"No. Nice try. One thing.\"",
                        Grates = "\"Wasted. You wasted it on a trick.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.l2.table", Pool = TalkPools.Guarded, Kind = TalkKind.Layer, Layer = 2, Reveals = true,
                When = new TalkGate { Layer = 2 },
                Open = "(setting the table with exact spacing between the plates) \"Where I'm from, a table looks like this or you don't sit down. Nobody here gets it.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Then make them. Set every place, every night.\"",
                        Lands = "\"Maybe I will. I learned it somewhere. Here's where.\"",
                        Even = "\"They'd hate it. I'd enjoy that. Here's why.\"",
                        Grates = "\"I'm not running a house. I'm setting a table.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "(straighten one fork to match)",
                        Lands = "\"Look at that. You get it. All right.\"",
                        Even = "\"Close. Thank you. Since you tried.\"",
                        Grates = "\"Leave it. You moved it a finger's width off.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You do it so nobody notices your hands shaking.\"",
                        Lands = "\"Rude. Correct. Fine. Here's why they shake.\"",
                        Even = "\"Sometimes. Not tonight. Here's something else.\"",
                        Grates = "\"That's a big guess off a fork.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.l2.window", Pool = TalkPools.Guarded, Kind = TalkKind.Layer, Layer = 2, Reveals = true,
                When = new TalkGate { Layer = 2 },
                Open = "(standing at the one window, painted over from outside) \"I keep standing here like I could see out. There was a window at home I lived in.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Tell me what you'd see.\"",
                        Lands = "\"All right. Close your eyes. No. Don't. Just listen.\"",
                        Even = "\"Not much. It was enough. Here.\"",
                        Grates = "\"You'd be bored. Leave me to it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"I'll paint you a view on it. Badly.\"",
                        Lands = "\"Do it and I'll tell you what to paint.\"",
                        Even = "\"Badly is right. Here's the real one.\"",
                        Grates = "\"Don't. It's the one thing in here I don't joke about.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"You come here when you need a minute. Good to know.\"",
                        Lands = "\"Now you know where to find me. Might as well know why.\"",
                        Even = "\"Everyone has a spot. Mine has a reason.\"",
                        Grates = "\"And now I'll have to find a new spot.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.l3.reasons", Pool = TalkPools.Guarded, Kind = TalkKind.Layer, Layer = 3, Reveals = true,
                When = new TalkGate { Layer = 3 },
                Open = "(late, the yard empty, the pool light still on) \"I've told the house three reasons I'm here. All true. None of them the one that matters.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Only if you want to give it.\"",
                        Lands = "\"I do. That's what's strange. Here.\"",
                        Even = "\"I think so. Don't interrupt.\"",
                        Grates = "\"If you don't want it, don't make me offer twice.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Then tell me the one that matters.\"",
                        Lands = "\"All right. I've never said this in here.\"",
                        Even = "\"Fine. Most of it.\"",
                        Grates = "\"Too fast. You asked too fast.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"I already know which one. Tell me I'm wrong.\"",
                        Lands = "\"You're half right. Here's the other half.\"",
                        Even = "\"You're wrong. Here's how.\"",
                        Grates = "\"Then you don't need me to say it.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.l3.line", Pool = TalkPools.Guarded, Kind = TalkKind.Layer, Layer = 3, Reveals = true,
                When = new TalkGate { Layer = 3 },
                Open = "(sitting on the edge of the hot tub, not in it) \"There's a line in here I won't come back from. I've never said where it is.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Tell me so I never get near it.\"",
                        Lands = "\"That's why I'm telling you. I'd rather not lose you over it.\"",
                        Even = "\"All right. So you know.\"",
                        Grates = "\"You'd get near it anyway. People do. Not tonight.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Knowing where it is would help me.\"",
                        Lands = "\"It would. I'm telling you anyway. That's the trust.\"",
                        Even = "\"I know. Help yourself, then. Carefully.\"",
                        Grates = "\"Help you what? Walk right up to it?\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You're telling me because someone already crossed it.\"",
                        Lands = "\"Almost. Not you. Here's where it is.\"",
                        Even = "\"Close. I'll tell you anyway.\"",
                        Grates = "\"Don't guess at my lines. Just don't.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.l3.sleep", Pool = TalkPools.Guarded, Kind = TalkKind.Layer, Layer = 3, Reveals = true,
                When = new TalkGate { Layer = 3 },
                Open = "(upright on the couch, eyes snapping open as you sit) \"I wasn't asleep. I don't sleep where people can see. There's a reason.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I won't tell anyone I saw.\"",
                        Lands = "\"Thank you. That's new for me. Here's the reason.\"",
                        Even = "\"Thank you. A little of it, then.\"",
                        Grates = "\"Then why'd you sit down next to me?\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"You were snoring. Very softly. Very fiercely.\"",
                        Lands = "\"I don't snore. Fine. Sit. Here's the reason.\"",
                        Even = "\"Fiercely. Fine. Here's why I don't do that.\"",
                        Grates = "\"Not funny right now.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"What's the reason?\"",
                        Lands = "\"Not the votes. Something else. Here.\"",
                        Even = "\"One thing. I'll give you one thing.\"",
                        Grates = "\"Too direct. Ask me tomorrow.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.l3.edit", Pool = TalkPools.Guarded, Kind = TalkKind.Layer, Layer = 3, Reveals = true,
                When = new TalkGate { Layer = 3 },
                Open = "(at the bathroom mirror, not really looking at it) \"They'll edit me as cold. I know it. I want one person in here to know why I'm like this.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Then let it be me.\"",
                        Lands = "\"All right. It's you. Here.\"",
                        Even = "\"Maybe you. Start with this.\"",
                        Grates = "\"Don't volunteer. Let me choose.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"So stop being cold. Show them.\"",
                        Lands = "\"Can't. Won't. Here's why.\"",
                        Even = "\"Easy to say. Here's what it costs.\"",
                        Grates = "\"You want me to perform for a camera? No.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Cold reads well in a jury. You know that.\"",
                        Lands = "\"I do. That's not why, though. This is why.\"",
                        Even = "\"It does. There's more to it.\"",
                        Grates = "\"You see? That's how they'll cut it. Like that.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.l3.cost", Pool = TalkPools.Guarded, Kind = TalkKind.Layer, Layer = 3, Reveals = true,
                When = new TalkGate { Layer = 3 },
                Open = "(holding a pillow in their lap like a shield) \"I gave something up to be here. I don't talk about it. I think I'm about to.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I'm listening. Take your time.\"",
                        Lands = "\"Thank you. Here's what I gave up, and why.\"",
                        Even = "\"Here's the part I can say.\"",
                        Grates = "\"Don't say 'take your time.' It makes it a scene.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Was it worth it?\"",
                        Lands = "\"Ask me at the end. Here's what it was.\"",
                        Even = "\"So far. Here.\"",
                        Grates = "\"That's the question I'm not ready for.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Do I need to sit down for this?\"",
                        Lands = "\"You're already sitting. That's how I knew to say it.\"",
                        Even = "\"You're fine. Here.\"",
                        Grates = "\"Forget it. Wrong moment.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.l3.almost", Pool = TalkPools.Guarded, Kind = TalkKind.Layer, Layer = 3, Reveals = true,
                When = new TalkGate { Layer = 3 },
                Open = "(sorting the spice rack by date) \"I almost said no when they called. I had the word ready. Ask me what changed my mind.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"What changed your mind?\"",
                        Lands = "\"Good. A straight question. Here.\"",
                        Even = "\"One thing did. This.\"",
                        Grates = "\"Asked like a form. Not tonight.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I don't need to know. I'm glad you came.\"",
                        Lands = "\"That's the right answer. So you get it.\"",
                        Even = "\"Glad's a strong word. Here, anyway.\"",
                        Grates = "\"Don't be glad for me. You don't know what it cost.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Nobody almost says no. You were always coming.\"",
                        Lands = "\"Half right. Here's the half you missed.\"",
                        Even = "\"Maybe. Here's why I nearly didn't.\"",
                        Grates = "\"You don't know that. You don't know me that well.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.l3.after", Pool = TalkPools.Guarded, Kind = TalkKind.Layer, Layer = 3, Reveals = true,
                When = new TalkGate { Layer = 3 },
                Open = "(in the hammock, one foot on the ground to stop it moving) \"When I walk out, there's one thing I want people at home to say about me. Guess.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"'Never once lost the remote.'\"",
                        Lands = "\"Ha. Close, actually. Not close. Here.\"",
                        Even = "\"No. Here's the real one.\"",
                        Grates = "\"You can't take anything seriously after dark, can you.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"That you were kind, even when it cost you.\"",
                        Lands = "\"Nearly. You're nearly right. Here's the rest.\"",
                        Even = "\"That's not it. It's this.\"",
                        Grates = "\"Kind? Don't make me into something soft.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"That you won. Nothing else counts to you.\"",
                        Lands = "\"Wrong, and you knew it. Here.\"",
                        Even = "\"No. Something under that.\"",
                        Grates = "\"If that's what you think of me, why guess?\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "guarded.l3.passed", Pool = TalkPools.Guarded, Kind = TalkKind.Layer, Layer = 3, Reveals = true,
                When = new TalkGate { Layer = 3 },
                Open = "(shutting the storage room door behind you both) \"I've been testing you for weeks. You passed. That's the only reason I'm saying this.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"What was the test?\"",
                        Lands = "\"Whether you'd repeat things. You didn't. So. This.\"",
                        Even = "\"Doesn't matter. Here.\"",
                        Grates = "\"You'd fail it now, asking that.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I didn't know I was taking one.\"",
                        Lands = "\"That's why you passed. Here.\"",
                        Even = "\"Nobody does. That's the point. Listen.\"",
                        Grates = "\"Then don't look so pleased.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"I knew. I passed on purpose.\"",
                        Lands = "\"Liar. But a good one. Here.\"",
                        Even = "\"Sure you did. Listen anyway.\"",
                        Grates = "\"Then you didn't pass. That's a move.\"",
                    },
                },
            },
        };
    }
}
