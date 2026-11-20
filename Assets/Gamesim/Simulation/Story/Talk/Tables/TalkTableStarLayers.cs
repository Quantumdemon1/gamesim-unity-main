namespace Gamesim.Simulation
{
    /// <summary>
    /// Free talk, the star pool's layers talks (ruling R1: the talks are C# tables). Written by a converter from the
    /// talk source, ids kept; edit the text here by hand. American spelling.
    /// </summary>
    public static class TalkTableStarLayers
    {
        public static readonly TalkRow[] Rows =
        {
            new TalkRow
            {
                Id = "star.l1.source", Pool = TalkPools.Star, Kind = TalkKind.Layer, Layer = 1, Reveals = true,
                When = new TalkGate { First = true },
                Open = "(sliding onto the stool across the counter) \"You know who I am. Don't pretend you don't. Ask the thing everybody's circling.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Fine. What do people get wrong about how you play?\"",
                        Lands = "\"Most of it. Sit. I'll give you the part they got right.\"",
                        Even = "\"Less than you'd think. Here's the part that's true.\"",
                        Grates = "\"Nice try. You get that answer after you survive a week.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"I was going to ask where the good mugs are.\"",
                        Lands = "\"Top left. And since you were polite about it, here's the other thing.\"",
                        Even = "\"Top left. You'd have asked the other thing eventually. Here.\"",
                        Grates = "\"Cute. Keep the bit. I'll keep my answers.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"No question. I'd rather find out for myself.\"",
                        Lands = "\"Good instinct. I'll save you a week anyway.\"",
                        Even = "\"Suit yourself. You'll hear it from someone. Better from me.\"",
                        Grates = "\"Then find out. I'm not making it easy.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.l1.warning", Pool = TalkPools.Star, Kind = TalkKind.Layer, Layer = 1, Reveals = true,
                When = new TalkGate { First = true },
                Open = "(holding the yard door open with one foot) \"Everyone's going to tell you what I'm like. Want it from the source first?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Go on, then. Sell yourself.\"",
                        Lands = "\"Not selling. Warning. There's a difference. Here's the honest one.\"",
                        Even = "\"I don't sell. I'll say it once.\"",
                        Grates = "\"If I have to sell it, I've already lost you. Never mind.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"The source is the least reliable version.\"",
                        Lands = "\"Smart. Take it anyway, and check it against the rest.\"",
                        Even = "\"True. Here it is anyway. Compare notes.\"",
                        Grates = "\"Then go ask the couch. I'm sure they're very fair.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'd rather hear it from you than from the couch.\"",
                        Lands = "\"The couch exaggerates. I only slightly exaggerate. Here.\"",
                        Even = "\"Fair. Short version, then.\"",
                        Grates = "\"Everyone says that until the couch says something juicier.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.l1.firstcheck", Pool = TalkPools.Star, Kind = TalkKind.Layer, Layer = 1, Reveals = true,
                When = new TalkGate { Layer = 1 },
                Open = "(in the yard, watching two new players arm-wrestle) \"Look. Neither of them has checked who's watching. That's the first thing I check.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"What do you check second?\"",
                        Lands = "\"Who's checking me back. Fine. Since you asked plain, here's my tell.\"",
                        Even = "\"Whether they noticed me checking. Most don't. Here's the rest.\"",
                        Grates = "\"Second one's mine. I'm not handing out the list.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"So you're already sizing up the whole house.\"",
                        Lands = "\"Always. So are you. The difference is I'll admit it. Here.\"",
                        Even = "\"Everyone is. I just don't hide it well. Here's how.\"",
                        Grates = "\"Says the one who's been counting heads since breakfast.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"And what did you check about me?\"",
                        Lands = "\"That you'd ask. You passed. Fine, a trade.\"",
                        Even = "\"That you'd ask exactly that. Fine. One for one.\"",
                        Grates = "\"That you talk before you look. Noted.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.l1.habit", Pool = TalkPools.Star, Kind = TalkKind.Layer, Layer = 1, Reveals = true,
                When = new TalkGate { Layer = 1 },
                Open = "(rinsing out a blender at the sink) \"Every season somebody asks me for the secret. There isn't one. There's a habit.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'd settle for the habit.\"",
                        Lands = "\"Good answer. Most people want a trick. Here's the habit.\"",
                        Even = "\"Everyone settles for it eventually. Here.\"",
                        Grates = "\"You'd settle. That's the trouble with most people in here.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"A habit's just a secret you repeat.\"",
                        Lands = "\"Ha. Fine. Then here's the one I repeat.\"",
                        Even = "\"Maybe. Still not a trick. Listen.\"",
                        Grates = "\"Clever. You're going to be exhausting.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Then show me, don't tell me.\"",
                        Lands = "\"Watch me this week. Or I'll save you the time.\"",
                        Even = "\"You'll see it. Might as well know what you're seeing.\"",
                        Grates = "\"Show you? So you can copy it? No.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.l1.watch", Pool = TalkPools.Star, Kind = TalkKind.Layer, Layer = 1, Reveals = true,
                When = new TalkGate { Layer = 1 },
                Open = "(stretching by the pool before anything's been announced) \"New players watch my hands in a comp. Wrong part. Want to know what to watch?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Your feet?\"",
                        Lands = "\"Close. Wrong, but close. Here's the real answer.\"",
                        Even = "\"No. Funny, though. Here.\"",
                        Grates = "\"Keep guessing. I'll keep stretching.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Yes. Tell me.\"",
                        Lands = "\"Straight ask, straight answer. Here's what the house knows me for.\"",
                        Even = "\"Fine. Only because you asked plain.\"",
                        Grates = "\"Too eager. Ask me again next week.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You're telling me so I'll watch the wrong thing.\"",
                        Lands = "\"Ha. Maybe. That's sort of the point. Here.\"",
                        Even = "\"Could be. You'll have to decide. Here.\"",
                        Grates = "\"Then don't listen. Your loss.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.l1.pen", Pool = TalkPools.Star, Kind = TalkKind.Layer, Layer = 1, Reveals = true,
                When = new TalkGate { Layer = 1 },
                Open = "(reading the chore chart nobody follows) \"Whoever wrote this chart is playing hard. Watch them. I never hold the pen.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"So you let someone else hold the pen.\"",
                        Lands = "\"Always. The pen's a target. Here's how I play instead.\"",
                        Even = "\"Every time. That's half my game in one sentence. Here's the other half.\"",
                        Grates = "\"You've been watching me. I'd rather you hadn't said it out loud.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I'll leave the chart alone, then.\"",
                        Lands = "\"Good. Leave it to someone who wants to be seen. Here's the rest.\"",
                        Even = "\"Smart. Here's what I do with the time.\"",
                        Grates = "\"Or write it and learn something. Don't just copy me.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Want me to put us both on the easy chores?\"",
                        Lands = "\"See, that's charm. I'll trade you something for it.\"",
                        Even = "\"Nice offer. I'll pay you in a straight answer.\"",
                        Grates = "\"You just picked up the pen. That's lesson one, and you failed it.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.l1.stairs", Pool = TalkPools.Star, Kind = TalkKind.Layer, Layer = 1, Reveals = true,
                When = new TalkGate { Layer = 1 },
                Open = "(on the stairs, listening to an argument below) \"New players always ask how I handle a fight. They mean: what do you do when it's you.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"So what do you do when it's you?\"",
                        Lands = "\"Glad you asked the real one. Here.\"",
                        Even = "\"The same thing every time. Here it is.\"",
                        Grates = "\"Depends who's asking. Right now, not you.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Go down there and I'll watch how you do it.\"",
                        Lands = "\"Ha. Not tonight. I'll tell you instead.\"",
                        Even = "\"Not my fight. Here's what you'd see if it were.\"",
                        Grates = "\"Not a show for you. Find your own fight.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Hide on the stairs, apparently.\"",
                        Lands = "\"Caught. This is step one. Want the rest?\"",
                        Even = "\"Step one. There are more steps. Here.\"",
                        Grates = "\"Watching. Not hiding. Learn the difference.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.l1.reputation", Pool = TalkPools.Star, Kind = TalkKind.Layer, Layer = 1, Reveals = true,
                When = new TalkGate { Layer = 1 },
                Open = "(sorting the pantry, labels out) \"People think a returning player plays a style. I play a reputation. They're not the same.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Which one am I dealing with right now?\"",
                        Lands = "\"Good question. Both. Here's the reputation part.\"",
                        Even = "\"Today? The reputation. Here's what it says.\"",
                        Grates = "\"You're dealing with someone who doesn't answer that.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"A reputation's just a style other people noticed.\"",
                        Lands = "\"Sharp. Fine. Here's what they noticed.\"",
                        Even = "\"Maybe. Here's what got noticed.\"",
                        Grates = "\"Easy to say from your first season.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'd rather know the player than either.\"",
                        Lands = "\"Rare thing to hear in here. Start with this.\"",
                        Even = "\"Nice. The reputation's the door, though. Here.\"",
                        Grates = "\"That's not on offer this early.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.l1.onething", Pool = TalkPools.Star, Kind = TalkKind.Layer, Layer = 1, Reveals = true,
                When = new TalkGate { Layer = 1 },
                Open = "(in the yard, retying a shoe that wasn't untied) \"Ask me one thing about how I play. One. I'll answer it straight.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"What makes you dangerous?\"",
                        Lands = "\"Ooh. Okay. Straight answer, like I said.\"",
                        Even = "\"Big question. Smaller answer. Here.\"",
                        Grates = "\"That's not one thing. That's a trap. Pass.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I'll save my one for later.\"",
                        Lands = "\"Patient. That's rare. You get a free one, then.\"",
                        Even = "\"Fine. Have a free one anyway.\"",
                        Grates = "\"Offer expires. That's also how I play.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Do you lie in here?\"",
                        Lands = "\"Everybody does. Better question is how. Here's a start.\"",
                        Even = "\"Everybody. Ask a better one. I'll answer it anyway.\"",
                        Grates = "\"You just told me you'd believe whatever I said. Pass.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.l1.label", Pool = TalkPools.Star, Kind = TalkKind.Layer, Layer = 1, Reveals = true,
                When = new TalkGate { Layer = 1 },
                Open = "(the only lamp on in the living room) \"Every house decides what I am by the second week. Want to get ahead of them?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Yes. Before they decide what I am too.\"",
                        Lands = "\"Good. Then here's what they'll decide about me.\"",
                        Even = "\"Fair trade. Here's mine.\"",
                        Grates = "\"They've already started on you. You're late.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Let me guess. Trouble.\"",
                        Lands = "\"Close enough. The longer version goes like this.\"",
                        Even = "\"Everyone guesses trouble. Here's the longer word.\"",
                        Grates = "\"Lazy guess. Try again next week.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'd rather decide for myself.\"",
                        Lands = "\"Good. Then decide with this in your pocket.\"",
                        Even = "\"Sure. Here's something to decide with.\"",
                        Grates = "\"Then you don't need my help.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.l2.wrongread", Pool = TalkPools.Star, Kind = TalkKind.Layer, Layer = 2, Reveals = true,
                When = new TalkGate { Layer = 2 },
                Open = "(holding a cold mug, not drinking it) \"Most players trust one wrong read in their first game. Know what that teaches you?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Tell me. I'd rather learn it from you than the hard way.\"",
                        Lands = "\"What I respect in a player now. It's short. Here.\"",
                        Even = "\"One thing. I'll give it to you.\"",
                        Grates = "\"Not to answer that for anyone who asks too fast.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"You don't have to tell me.\"",
                        Lands = "\"I know. That's why I will.\"",
                        Even = "\"Short version, then.\"",
                        Grates = "\"Good, because I wasn't going to.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Is it ever the read, or is it the player?\"",
                        Lands = "\"The player. That's the right question. Here's what changes.\"",
                        Even = "\"The player, mostly. Here's what to take from it.\"",
                        Grates = "\"Careful. You're grading games you didn't play.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.l2.skin", Pool = TalkPools.Star, Kind = TalkKind.Layer, Layer = 2, Reveals = true,
                When = new TalkGate { Layer = 2 },
                Open = "(scrubbing a pan far too hard) \"Want to know what gets under my skin in a game? Most people find out the hard way.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'd rather find out the easy way.\"",
                        Lands = "\"Smart. I'll make it easy. Once.\"",
                        Even = "\"Easy way, then. Listen close.\"",
                        Grates = "\"Nobody gets the easy way. Not from me.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Tell me so I can use it.\"",
                        Lands = "\"Ha. Honest. Use it and see what happens.\"",
                        Even = "\"Bold. Fine. Know it, and think twice.\"",
                        Grates = "\"And there it is. No.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Pans, apparently.\"",
                        Lands = "\"Ha. Pans, and one other thing.\"",
                        Even = "\"Pans. And this.\"",
                        Grates = "\"Funny. I'm not in the mood for funny.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.l2.bench", Pool = TalkPools.Star, Kind = TalkKind.Layer, Layer = 2, Reveals = true,
                When = new TalkGate { Layer = 2 },
                Open = "(feet up on the yard table) \"Every jury I've seen rewarded something different. Except one part. That part never moves.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Which part never moves?\"",
                        Lands = "\"The part I look for in people now. Here.\"",
                        Even = "\"One part. I'll tell you what it is.\"",
                        Grates = "\"The part nobody explains to you. Including me.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Sounds like a lesson you paid for.\"",
                        Lands = "\"Paid in full. Here's what it bought.\"",
                        Even = "\"Everyone pays for it. Here's mine.\"",
                        Grates = "\"Paid for, and not for sale.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Juries are a mystery to me.\"",
                        Lands = "\"Stay curious about them. Here's a clue.\"",
                        Even = "\"To everyone. Here's one clue.\"",
                        Grates = "\"Then stay mystified.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.l2.baggage", Pool = TalkPools.Star, Kind = TalkKind.Layer, Layer = 2, Reveals = true,
                When = new TalkGate { Layer = 2 },
                Open = "(fixing the yard umbrella that keeps tipping) \"What ends a veteran's game is never a comp. It's a habit they carried in from last time.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"So what's yours?\"",
                        Lands = "\"Straight at it. Fine. One of them.\"",
                        Even = "\"I've got a few. Here's one to watch for.\"",
                        Grates = "\"Ask me that when you've got one of your own.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Then leave it at the door. Start clean with me.\"",
                        Lands = "\"Clean, then. Here's what I'm leaving at the door.\"",
                        Even = "\"Clean start. Here's what to watch me not do.\"",
                        Grates = "\"Nobody starts clean in here. Sweet of you, though.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Habits are predictable. That's useful to me.\"",
                        Lands = "\"Ha. It is. Here's one I'll let you have.\"",
                        Even = "\"Useful to everyone. Here's one for free.\"",
                        Grates = "\"And now you get none of them.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.l2.rules", Pool = TalkPools.Star, Kind = TalkKind.Layer, Layer = 2, Reveals = true,
                When = new TalkGate { Layer = 2 },
                Open = "(crossing off another day on a scrap of paper) \"I've spent enough days in houses like this to have rules. Want one?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Only if it's a strange one.\"",
                        Lands = "\"It's a little strange. Here.\"",
                        Even = "\"Medium strange. Here.\"",
                        Grates = "\"They're not party tricks.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"One's plenty.\"",
                        Lands = "\"One's all you get anyway. Make it count.\"",
                        Even = "\"One, then.\"",
                        Grates = "\"Then you'll want a second one later and not get it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Give me the one you broke.\"",
                        Lands = "\"Oh, you're good. That one, then.\"",
                        Even = "\"The one I broke taught me most. Fine.\"",
                        Grates = "\"I don't break rules. I renegotiate them. Not today.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.l2.climb", Pool = TalkPools.Star, Kind = TalkKind.Layer, Layer = 2, Reveals = true,
                When = new TalkGate { Layer = 2 },
                Open = "(passing the stairs to the HoH room without looking up) \"Those stairs teach you something whether you climb them or not.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Then teach me. Save me the climb.\"",
                        Lands = "\"Who to respect in here. It's a short list. Here's what gets on it.\"",
                        Even = "\"One thing. I'll share it.\"",
                        Grates = "\"Climb them first. Then ask me.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Which lesson was worth more, up or down?\"",
                        Lands = "\"Down. Always down. Here's why.\"",
                        Even = "\"The bottom one. Here it is.\"",
                        Grates = "\"You want the cheap lesson. No.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Sounds like they cost you something.\"",
                        Lands = "\"A few things. You can have the useful one.\"",
                        Even = "\"Some. Here's what I kept.\"",
                        Grates = "\"Don't. Sympathy's a currency in here and I'm not buying.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.l2.lens", Pool = TalkPools.Star, Kind = TalkKind.Layer, Layer = 2, Reveals = true,
                When = new TalkGate { Layer = 2 },
                Open = "(at the bathroom mirror, not fixing anything) \"Here's what the cameras teach every player. Not how you look on them. How you play when they're on.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"You don't have to give that away.\"",
                        Lands = "\"I know. Which is why you get it.\"",
                        Even = "\"Short version. It's all about the game.\"",
                        Grates = "\"Good. Leave it, then.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"So you play to the cameras.\"",
                        Lands = "\"Everyone does. I just know when. Here's the rest.\"",
                        Even = "\"Sometimes. Here's what else I learned.\"",
                        Grates = "\"And you don't? Come back when you've seen yourself edited.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Your good side's the left, then.\"",
                        Lands = "\"Both sides. But here's the real lesson.\"",
                        Even = "\"Ha. The lesson's less flattering.\"",
                        Grates = "\"Not a mirror-joke kind of day.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.l2.never", Pool = TalkPools.Star, Kind = TalkKind.Layer, Layer = 2, Reveals = true,
                When = new TalkGate { Layer = 2 },
                Open = "(shuffling a deck of cards nobody asked for) \"People ask what I'd do differently. Wrong question. Ask what I'd never do again.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"What would you never do again?\"",
                        Lands = "\"Good. Right question. Here.\"",
                        Even = "\"One thing. Here.\"",
                        Grates = "\"Right question, wrong week.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Do it again anyway. I dare you.\"",
                        Lands = "\"Ha. Tempting. Here's what you'd be daring me into.\"",
                        Even = "\"No. But here's what it is.\"",
                        Grates = "\"Dares are how people leave in week two.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"That tells me what you'll do instead.\"",
                        Lands = "\"It does. Take it. You've earned that much.\"",
                        Even = "\"Maybe. Here's the piece.\"",
                        Grates = "\"Which is exactly why you don't get it.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.l2.underrated", Pool = TalkPools.Star, Kind = TalkKind.Layer, Layer = 2, Reveals = true,
                When = new TalkGate { Layer = 2 },
                Open = "(in the yard at dusk, by a comp prop nobody cleared) \"Some players only learn it by losing to someone they underrated. Don't be one of them.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Then I'm glad I'm hearing it from you.\"",
                        Lands = "\"Good. Here's what to watch for.\"",
                        Even = "\"Then listen. Here's the takeaway.\"",
                        Grates = "\"Don't flatter a veteran. We notice.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"So who got underrated?\"",
                        Lands = "\"No names. Ever. Here's what I watch for now.\"",
                        Even = "\"No names. Here's what's left.\"",
                        Grates = "\"No names, and that question's a strike.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Glad it wasn't me.\"",
                        Lands = "\"Yet. Here's how to keep it that way.\"",
                        Even = "\"Not yet. Here's the lesson.\"",
                        Grates = "\"The week's young.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "star.l2.cameback", Pool = TalkPools.Star, Kind = TalkKind.Layer, Layer = 2, Reveals = true,
                When = new TalkGate { Layer = 2 },
                Open = "(pulling a blanket off the couch for the yard) \"The house thinks I came back for a nostalgia trip. I came back for one game reason. Want it?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Say it. I bet I can guess it anyway.\"",
                        Lands = "\"Bet you can't. Here it is.\"",
                        Even = "\"Short answer. Here.\"",
                        Grates = "\"Then guess. I'm not saying it twice.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"A nostalgia trip sounds nice, honestly.\"",
                        Lands = "\"Nostalgia is for people who think it's over. Here's the real one.\"",
                        Even = "\"It's not a trip. Here.\"",
                        Grates = "\"Nice try. Not today.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Everyone says that. Prove it.\"",
                        Lands = "\"Fine. Proof, or as close as I give anyone.\"",
                        Even = "\"Can't prove it. Can tell you.\"",
                        Grates = "\"I don't audition for first-timers.\"",
                    },
                },
            },
        };
    }
}
