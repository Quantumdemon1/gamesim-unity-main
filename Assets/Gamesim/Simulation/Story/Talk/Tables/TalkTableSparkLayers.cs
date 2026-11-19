namespace Gamesim.Simulation
{
    /// <summary>
    /// Free talk, the spark pool's layers talks (ruling R1: the talks are C# tables). Written by a converter from the
    /// talk source, ids kept; edit the text here by hand. American spelling.
    /// </summary>
    public static class TalkTableSparkLayers
    {
        public static readonly TalkRow[] Rows =
        {
            new TalkRow
            {
                Id = "spark.l1.movein", Pool = TalkPools.Spark, Kind = TalkKind.Layer, Layer = 1, Reveals = true,
                When = new TalkGate { First = true },
                Open = "(dragging a suitcase past the bunks) \"Okay, you. One thing about you that isn't on the intro card. Then I'll do mine.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"You first. I'm a good audience.\"",
                        Lands = "\"Oh, I love an audience. Fine. Here's the version I don't do for the cameras.\"",
                        Even = "\"Ha. Fair. One thing, the short version.\"",
                        Grates = "\"Wow, you're good at not answering. Noted. I'll find someone chattier.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"There isn't much. Honestly, I'm boring.\"",
                        Lands = "\"Nobody who says that is boring. Watch, I'll show you how it's done.\"",
                        Even = "\"Nobody's boring in here. Give it a week. Me, though.\"",
                        Grates = "\"Boring's a choice. I'll go find someone who made a different one.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Depends. What do I get for mine?\"",
                        Lands = "\"A trade. I respect the hustle. You get one now, I collect later.\"",
                        Even = "\"Everything's a deal with you. Fine, one, on credit.\"",
                        Grates = "\"It's move-in day. Put the calculator down.\" (goes to claim a bunk)",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.l1.fridge", Pool = TalkPools.Spark, Kind = TalkKind.Layer, Layer = 1, Reveals = true,
                When = new TalkGate { First = true },
                Open = "(writing names on the fridge whiteboard) \"I'm putting everyone up here with a fun fact. You first. Then I'll do mine properly.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Just the name. I don't have a fun fact.\"",
                        Lands = "\"Everyone has one, you just haven't heard yourself say it. Mine's real, look.\"",
                        Even = "\"Blank, then. I'll fill mine in and you can copy the format.\"",
                        Grates = "\"A blank square on the fridge. Very mysterious. Very annoying.\" (moves on to the next name)",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Fun fact: I'm the one who wins.\"",
                        Lands = "\"Oh, I'm writing that in big letters. Mine's less threatening. Here.\"",
                        Even = "\"Bold. Going on the fridge. Mine goes under it.\"",
                        Grates = "\"Cute. You're the fifth person to say that.\" (writes nothing)",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Put whatever you like. I trust you.\"",
                        Lands = "\"Dangerous thing to say to me. Okay, I'll be nice. And you get a true one of mine.\"",
                        Even = "\"Okay, I'm inventing yours. Mine's true, at least.\"",
                        Grates = "\"That's a lot of trust for day one. Weirdly a lot.\" (draws a question mark)",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.l1.mirror", Pool = TalkPools.Spark, Kind = TalkKind.Layer, Layer = 1, Reveals = true,
                When = new TalkGate { Layer = 1 },
                Open = "(doing their hair at the bathroom mirror, talking to your reflection) \"I talk to mirrors. They think it's vanity. It's rehearsal.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Rehearsal for what? The diary room?\"",
                        Lands = "\"For everything. Okay, no mirror. A real answer to a real face.\"",
                        Even = "\"For whatever comes. Fine, I'll rehearse on you instead.\"",
                        Grates = "\"Laugh it up. The mirror doesn't heckle.\" (turns back to it)",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Rehearse something true, then. Say it to me.\"",
                        Lands = "\"Ooh. Okay. To a face, not a mirror. Here.\"",
                        Even = "\"Pushy. Fine. One thing, unrehearsed.\"",
                        Grates = "\"Easy. You don't get the unrehearsed stuff yet.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Rehearsing is how people get caught lying.\"",
                        Lands = "\"Ha! Right. So here's one I never practiced.\"",
                        Even = "\"Or how they get it right. Fine, a true one.\"",
                        Grates = "\"I fix my hair and I'm a liar now. Good chat.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.l1.guess", Pool = TalkPools.Spark, Kind = TalkKind.Layer, Layer = 1, Reveals = true,
                When = new TalkGate { Layer = 1 },
                Open = "(perched on the counter, swinging a foot) \"Game. You guess one thing about me. Wrong guess, you do the dishes.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"I'll guess right. You'll be the one scrubbing.\"",
                        Lands = "\"Confident. I like it. Oh, close. Here's the real one.\"",
                        Even = "\"Wrong, but I'll allow it. The real answer goes like this.\"",
                        Grates = "\"Wrong, and smug about it. I'm keeping my answer.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Give me a hint first. I want to see how you play.\"",
                        Lands = "\"Smart. One hint. Actually, forget the hint. Here.\"",
                        Even = "\"Sneaky. Fine, I'll just tell you.\"",
                        Grates = "\"Ah, a scout. If you're studying me, do it without the cheat sheet.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'll do the dishes anyway. Just tell me.\"",
                        Lands = "\"That's so unfair, now I have to like you. Okay. Something.\"",
                        Even = "\"Deal. Dishes for a fact. Here.\"",
                        Grates = "\"You can't skip the game. The game was the fun part.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.l1.laugh", Pool = TalkPools.Spark, Kind = TalkKind.Layer, Layer = 1, Reveals = true,
                When = new TalkGate { Layer = 1 },
                Open = "(still laughing at something from dinner) \"Sorry. I laugh at everything. My laugh is already on three people's nerves.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"It makes you easy to find. Remember that.\"",
                        Lands = "\"Ha, true, I'm a lighthouse. Since you're keeping notes, here's a good one.\"",
                        Even = "\"Noted. You can find me, then. Here, while you're looking.\"",
                        Grates = "\"Thanks, coach. I'll laugh quieter around you.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Laugh away. It's not on mine.\"",
                        Lands = "\"One person! Okay, you get a reward. A real thing about me.\"",
                        Even = "\"Good. Stick around, then. You'll hear a lot of it.\"",
                        Grates = "\"Don't humor me. I can tell when I'm being handled.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"It's loud. But it's real, at least.\"",
                        Lands = "\"Real is the only setting I have. Here, a real one.\"",
                        Even = "\"Loud and real. That's me. Mostly.\"",
                        Grates = "\"Loud. Got it. I'll take it to another room.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.l1.bunk", Pool = TalkPools.Spark, Kind = TalkKind.Layer, Layer = 1, Reveals = true,
                When = new TalkGate { Layer = 1 },
                Open = "(hanging upside down off the top bunk) \"Ask me anything. I'm bored, and I overshare when I'm bored. It's a whole problem.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"No pressure. Tell me whatever's on top.\"",
                        Lands = "\"Whatever's on top. That's a nice way to ask. Here.\"",
                        Even = "\"On top. Hm. Okay, this one.\"",
                        Grates = "\"That's not a question. Ask a real one or let me be bored.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Careful. Everything you overshare, someone uses.\"",
                        Lands = "\"So I'll overshare to you. Safest bet in the house. Here.\"",
                        Even = "\"I know. I do it anyway. Here's a safe one.\"",
                        Grates = "\"Thanks for the warning. Now I'm not telling you anything.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Tell me what your best friend would tell me.\"",
                        Lands = "\"Oh, they'd tell you everything. So here's the nicest one.\"",
                        Even = "\"Okay. My best friend would say this. Allegedly.\"",
                        Grates = "\"My best friend wouldn't tell you a thing. Neither will I.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.l1.impression", Pool = TalkPools.Spark, Kind = TalkKind.Layer, Layer = 1, Reveals = true,
                When = new TalkGate { Layer = 1 },
                Open = "(sitting next to you at the pool edge, feet in) \"What was your first impression of me? Be honest. Mine of you was good. Mostly.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Loud. Very loud.\"",
                        Lands = "\"Ha! Correct. The loud person will now say something true.\"",
                        Even = "\"Fair. There's more to it, though. For instance.\"",
                        Grates = "\"Loud. Okay. I'm keeping my impression of you to myself.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Fun. Good company on a long night.\"",
                        Lands = "\"Nicest thing anyone's said in here. Okay, you get a real one.\"",
                        Even = "\"Long nights are my thing. Here, a little more of me.\"",
                        Grates = "\"Fun. That's what they always say. I'm more than fun.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"I'll tell you after you tell me something real.\"",
                        Lands = "\"Deal. A real thing. Then you owe me the whole impression.\"",
                        Even = "\"Pushy. Okay. One real thing, then yours.\"",
                        Grates = "\"Oh, now it's a negotiation? I asked first.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.l1.snacktax", Pool = TalkPools.Spark, Kind = TalkKind.Layer, Layer = 1, Reveals = true,
                When = new TalkGate { Layer = 1 },
                Open = "(holding out a bag of something salty) \"Snack tax. You take one, you have to hear one thing about me. I made the rule.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"I'll pay the tax. Gladly.\"",
                        Lands = "\"Gladly! That's the attitude. Here's your thing.\"",
                        Even = "\"Good. Chew and listen.\"",
                        Grates = "\"Gladly? Calm down, it's a chip.\" (eats the rest)",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"What if I take two?\"",
                        Lands = "\"Then you get the deluxe fact. Lucky you.\"",
                        Even = "\"Two chips, still one fact. I'm not made of facts.\"",
                        Grates = "\"Then you're a thief, and thieves get nothing.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Keep the chips. Just tell me.\"",
                        Lands = "\"Straight to it. I like someone who skips the chip. Okay.\"",
                        Even = "\"No chip? More for me. Here.\"",
                        Grates = "\"Skipping the bit ruins the bit. You owe me a chip.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.l2.accent", Pool = TalkPools.Spark, Kind = TalkKind.Layer, Layer = 2, Reveals = true,
                When = new TalkGate { Layer = 2 },
                Open = "(catching a word as it comes out) \"Did you hear that? My hometown just fell out of my mouth. It does that when I'm tired.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I heard it. Where's it from?\"",
                        Lands = "\"You actually want to know. Okay. Sit, it's a story.\"",
                        Even = "\"Tired me says too much. Here's the gist.\"",
                        Grates = "\"Don't do the polite face. I'll tell you when you mean it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Say it again. I want the full accent.\"",
                        Lands = "\"Oh, no. Okay. Once. And the backstory comes free.\"",
                        Even = "\"Not on camera. You can have something else, though.\"",
                        Grates = "\"I'm not a party trick. Find a different one.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Useful. People trust an accent.\"",
                        Lands = "\"Spoken like someone who's never been teased for one. Fine. A real thing.\"",
                        Even = "\"Maybe. Mine's just mine. Here.\"",
                        Grates = "\"It's not a tool. It's where I'm from. Wow.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.l2.photo", Pool = TalkPools.Spark, Kind = TalkKind.Layer, Layer = 2, Reveals = true,
                When = new TalkGate { Layer = 2 },
                Open = "(patting an empty pocket) \"They took our phones at the door. I keep reaching for mine to show people home.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Describe it. I'll picture it in high resolution.\"",
                        Lands = "\"High resolution. Okay. Picture this.\"",
                        Even = "\"Low resolution, then. The basics.\"",
                        Grates = "\"You'll laugh at it. I can tell already. Never mind.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Careful how much of home you hand out in here.\"",
                        Lands = "\"You're right, and I'm handing it to you anyway. That means something.\"",
                        Even = "\"Just a little of it, then.\"",
                        Grates = "\"It's home, not a password. Relax.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"You don't have to. I'll listen if you do.\"",
                        Lands = "\"That's the right answer. Okay. A piece of home.\"",
                        Even = "\"I want to. A little.\"",
                        Grates = "\"Don't hover. Either ask or don't.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.l2.soup", Pool = TalkPools.Spark, Kind = TalkKind.Layer, Layer = 2, Reveals = true,
                When = new TalkGate { Layer = 2 },
                Open = "(stirring a pot, tasting, wincing) \"This is meant to taste like home. It tastes like sixteen people and one spice.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Let me taste. I'll tell you what's missing.\"",
                        Lands = "\"Brave. It's missing a hundred dinners I can't cook in here. Listen.\"",
                        Even = "\"Missing a whole kitchen. Let me tell you about the kitchen.\"",
                        Grates = "\"Oh, a critic. Get your own pot.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I'm sure it's close. Tell me about the real one.\"",
                        Lands = "\"Close-ish. The real one comes with a story.\"",
                        Even = "\"Not close. The real one goes like this.\"",
                        Grates = "\"Don't be nice about my cooking. It makes it worse.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"That's not home. That's salt.\"",
                        Lands = "\"Ha! Fine, it's salt. Home's a whole other thing. Listen.\"",
                        Even = "\"It's mostly salt. Home had more.\"",
                        Grates = "\"I share my soup and I get a review.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.l2.away", Pool = TalkPools.Spark, Kind = TalkKind.Layer, Layer = 2, Reveals = true,
                When = new TalkGate { Layer = 2 },
                Open = "(at the yard wall, looking up) \"Longest I've ever been away. You'd think I'd love it. I talk about home every night.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Talking about home makes people go soft on you.\"",
                        Lands = "\"Is that why you listen so well? Fine. Soft me, coming up.\"",
                        Even = "\"Maybe. Doesn't make it less true. Here.\"",
                        Grates = "\"Not everything's a play. Some of us miss stuff.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"So stop talking about it. Play the game.\"",
                        Lands = "\"Tough love. I needed that. One last time, then I stop.\"",
                        Even = "\"Tonight's the last time. Promise. Here.\"",
                        Grates = "\"Maybe you didn't leave anything behind. I did.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Then tell me tonight. I like hearing it.\"",
                        Lands = "\"Careful, I'll keep you up till the lights come on. Okay.\"",
                        Even = "\"Okay. Short version. Tonight.\"",
                        Grates = "\"You say that now. Everyone glazes over by minute two.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.l2.laundry", Pool = TalkPools.Spark, Kind = TalkKind.Layer, Layer = 2, Reveals = true,
                When = new TalkGate { Layer = 2 },
                Open = "(folding towels into a perfect stack) \"Somebody taught me to fold like this. Nobody in here believes I can be tidy.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I believe you. Where'd you learn?\"",
                        Lands = "\"You believe me. That's one. Okay, here's the story.\"",
                        Even = "\"Long story. The short one goes like this.\"",
                        Grates = "\"You're just saying that. You've seen my bunk.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"It's a good stack. Somebody did right by you.\"",
                        Lands = "\"They did. Now I'm going to tell you about it.\"",
                        Even = "\"They did. A little about that.\"",
                        Grates = "\"Don't make it sentimental. It's towels.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"You're right. Nobody believes it.\"",
                        Lands = "\"Ha. Thank you for not lying. Here's why I can.\"",
                        Even = "\"See? So here's the proof.\"",
                        Grates = "\"You could've lied a little. People do that.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.l2.dinner", Pool = TalkPools.Spark, Kind = TalkKind.Layer, Layer = 2, Reveals = true,
                When = new TalkGate { Layer = 2 },
                Open = "(setting the table for far too many) \"Where I grew up, dinner was loud or it didn't happen. This house feels like practice.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Loud houses make people who can't sit in silence.\"",
                        Lands = "\"Ha. Caught. Okay, the quiet version of where I'm from.\"",
                        Even = "\"Maybe. Here's the loud version.\"",
                        Grates = "\"And quiet houses make people like you.\" (sets your plate down a little hard)",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Mine was the quiet kind. Tell me about loud.\"",
                        Lands = "\"Oh, you poor thing. Okay. Here's how loud.\"",
                        Even = "\"Loud's simple. Here.\"",
                        Grates = "\"Then you'll hate the answer. Skip it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"So who won dinner? You, obviously.\"",
                        Lands = "\"Obviously. Every night. Let me tell you how.\"",
                        Even = "\"Mostly. Here's the scoreboard.\"",
                        Grates = "\"You don't win dinner. You survive it. Never mind.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.l2.running", Pool = TalkPools.Spark, Kind = TalkKind.Layer, Layer = 2, Reveals = true,
                When = new TalkGate { Layer = 2 },
                Open = "(jogging laps of the yard, out of breath) \"I used to run to get out of my town. Now I run in a yard with cameras. Progress.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Out of it to where? Tell me.\"",
                        Lands = "\"To anywhere. Okay. Here's the place I was running from.\"",
                        Even = "\"Anywhere. Here's a piece of it.\"",
                        Grates = "\"Don't do the therapist voice. I'm jogging.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Sounds like you didn't love it there.\"",
                        Lands = "\"Love's complicated. Here's the honest version.\"",
                        Even = "\"Some of it. Here.\"",
                        Grates = "\"I didn't say that. Don't put words in my mouth.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"And now the whole town's watching you. Good leverage.\"",
                        Lands = "\"Ha. Yes, actually. Let me tell you who's watching.\"",
                        Even = "\"Leverage. Sure. Here, then.\"",
                        Grates = "\"It's my hometown, not leverage. Wow.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.l2.counting", Pool = TalkPools.Spark, Kind = TalkKind.Layer, Layer = 2, Reveals = true,
                When = new TalkGate { Layer = 2 },
                Open = "(counting the cutlery drawer like a till) \"I count things when I'm nervous. Old habit from way back. Don't tell anyone.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"You count everything. I'd noticed.\"",
                        Lands = "\"You noticed! Nobody notices. Okay, here's where it comes from.\"",
                        Even = "\"Busted. It goes back a ways.\"",
                        Grates = "\"You've been watching me count spoons? That's a lot.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"How many forks? I'll check your work.\"",
                        Lands = "\"Fourteen, two bent. And since you asked nicely, the story of the habit.\"",
                        Even = "\"Fourteen. Here's why I know.\"",
                        Grates = "\"Don't audit me. Now I've lost count.\" (starts again from one)",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Your secret's safe.\"",
                        Lands = "\"I know it is. Okay, the rest of it, then.\"",
                        Even = "\"It better be. A little more.\"",
                        Grates = "\"It's not a secret, it's forks. Don't make it weird.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.l3.whyhere", Pool = TalkPools.Spark, Kind = TalkKind.Layer, Layer = 3, Reveals = true,
                When = new TalkGate { Layer = 3 },
                Open = "(sitting on the closed hot tub cover, knees up, late) \"Everyone asks why I applied. I give a fun answer. Want the real one?\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Only if it's funnier than the fun one.\"",
                        Lands = "\"It's not funny at all. That's why I'm telling you.\"",
                        Even = "\"It's less funny. Here.\"",
                        Grates = "\"Then you can have the fun one. Same as everyone.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Yes. The real one. No bits.\"",
                        Lands = "\"No bits. Okay. Don't look at me while I say it.\"",
                        Even = "\"No bits. Mostly.\"",
                        Grates = "\"Ask me nicer and maybe.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"I'll know if it's another bit.\"",
                        Lands = "\"You would, that's the problem. Fine. The real one.\"",
                        Even = "\"Then I'd better not do one. Here.\"",
                        Grates = "\"Then you'll never know, will you.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.l3.comet", Pool = TalkPools.Spark, Kind = TalkKind.Layer, Layer = 3, Reveals = true,
                When = new TalkGate { Layer = 3 },
                Open = "(alone in the dark kitchen, for once not talking) \"You caught me being quiet. It's rare. Like a comet. Make a wish.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"I wish you'd tell me what you're thinking about.\"",
                        Lands = "\"Wasted wish. I was going to tell you anyway.\"",
                        Even = "\"Big wish. You get part of it.\"",
                        Grates = "\"Wishes don't work if you aim them at me.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Quiet means planning. What's the plan?\"",
                        Lands = "\"Not a plan. A reason. Here's the reason.\"",
                        Even = "\"Not planning. Thinking about why I'm here.\"",
                        Grates = "\"Can I sit in the dark without it being a scheme?\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "(sit down beside them without saying anything)",
                        Lands = "\"That's the nicest thing you could've done. Okay. Here's what I was thinking.\"",
                        Even = "\"Thanks. Give me a second. Here.\"",
                        Grates = "\"Now it's two people being weird in a kitchen. Great.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.l3.offversion", Pool = TalkPools.Spark, Kind = TalkKind.Layer, Layer = 3, Reveals = true,
                When = new TalkGate { Layer = 3 },
                Open = "(pulling their hair back after a long day, voice lower) \"This is the off version of me. Not many people in here have seen it.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Why show me?\"",
                        Lands = "\"Because you'd figure it out anyway. Might as well hear it from me.\"",
                        Even = "\"Because you're here. And because of this.\"",
                        Grates = "\"Forget it. The show's back on.\" (big smile, instantly)",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"I'll take the off version. No questions.\"",
                        Lands = "\"You'll get an answer anyway. Here's what keeps the show going.\"",
                        Even = "\"One answer, then. Here.\"",
                        Grates = "\"No questions? I thought you'd at least be curious.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"I like it better than the show.\"",
                        Lands = "\"Careful, I'll start believing you. Okay. Here's why there's a show.\"",
                        Even = "\"Most people don't. Here's why there is one.\"",
                        Grates = "\"That's not a compliment if it means the show's bad.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.l3.money", Pool = TalkPools.Spark, Kind = TalkKind.Layer, Layer = 3, Reveals = true,
                When = new TalkGate { Layer = 3 },
                Open = "(in a blanket on the yard couch after lights out) \"Can I be honest? It's not the money. Well, I'd take the money. That's not why.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Take your time. I'm not going anywhere.\"",
                        Lands = "\"Look at you, going nowhere. Okay. Here's why.\"",
                        Even = "\"Okay. The actual why.\"",
                        Grates = "\"Don't be patient with me, it makes me nervous. Never mind.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"Everyone says that. Prove it.\"",
                        Lands = "\"Fair. Here's the proof, and it's embarrassing.\"",
                        Even = "\"Fine. Here.\"",
                        Grates = "\"I don't have to prove anything to you.\" (pulls the blanket up)",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"It's the unlimited oatmeal, isn't it.\"",
                        Lands = "\"The oatmeal is a factor. No. Okay. The truth.\"",
                        Even = "\"Close. It's this.\"",
                        Grates = "\"I'm trying to be real and you're doing a bit. Cool.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.l3.firstthing", Pool = TalkPools.Spark, Kind = TalkKind.Layer, Layer = 3, Reveals = true,
                When = new TalkGate { Layer = 3 },
                Open = "(spinning a bottle cap on the table, catching it) \"If I win this, I know the first thing I'm doing. Want to guess? You won't.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.CallOut, Line = "\"You won't win if you tell people what you want.\"",
                        Lands = "\"So I'll only tell you. Here.\"",
                        Even = "\"Only you, then. Here.\"",
                        Grates = "\"Wow. Guess away, then. I'm out.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Tell me. I want to picture you winning.\"",
                        Lands = "\"Oh, that's dangerous. Okay. Picture this.\"",
                        Even = "\"You'd better. Here.\"",
                        Grates = "\"Don't flatter me. It makes me say too much.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Say it like you've already won.\"",
                        Lands = "\"Okay. Like it's done. Here goes.\"",
                        Even = "\"Like it's done. Sort of.\"",
                        Grates = "\"You'll jinx it. No.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.l3.scared", Pool = TalkPools.Spark, Kind = TalkKind.Layer, Layer = 3, Reveals = true,
                When = new TalkGate { Layer = 3 },
                Open = "(pacing the bedroom in socks after a long competition) \"You want to know what actually scares me in here? It's not the block.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Warm, Line = "\"Tell me. It stays here.\"",
                        Lands = "\"Here it is, then. Don't make it a thing.\"",
                        Even = "\"Okay. Quietly.\"",
                        Grates = "\"Everyone says it stays here. It never does.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Is it the shower drain? It's the drain.\"",
                        Lands = "\"The drain is second. Number one is this.\"",
                        Even = "\"Close second. Here.\"",
                        Grates = "\"I'm trying, and you're doing jokes. Forget it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"So what is it? Out with it.\"",
                        Lands = "\"Okay, okay. Out with it.\"",
                        Even = "\"Pushy. Fine.\"",
                        Grates = "\"Don't rush me. I've never said it out loud.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.l3.slider", Pool = TalkPools.Spark, Kind = TalkKind.Layer, Layer = 3, Reveals = true,
                When = new TalkGate { Layer = 3 },
                Open = "(drawing in the steam on the sliding door, then wiping it off) \"Do you ever think about who you're doing this for? I do. Every day, about now.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Straight, Line = "\"Every day. Who's yours?\"",
                        Lands = "\"You asked, so you get the real answer.\"",
                        Even = "\"Some of it. The part I can say out loud.\"",
                        Grates = "\"You answered too fast. I don't believe you. Pass.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Push, Line = "\"Show me what you drew.\"",
                        Lands = "\"You saw that. Okay. Then you get the why.\"",
                        Even = "\"Not the drawing. But the why.\"",
                        Grates = "\"You weren't meant to see that. Leave it.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Knowing someone's why is how you beat them.\"",
                        Lands = "\"Or how you keep them. Here's mine.\"",
                        Even = "\"Then be careful with it. Here.\"",
                        Grates = "\"And now I won't tell you. Thanks for the warning.\"",
                    },
                },
            },
            new TalkRow
            {
                Id = "spark.l3.comeback", Pool = TalkPools.Spark, Kind = TalkKind.Layer, Layer = 3, Reveals = true,
                When = new TalkGate { Layer = 3 },
                Open = "(flat on the lawn, looking up) \"If they asked me back next season I'd say yes before they finished asking. Ask me why.\"",
                Replies = new[]
                {
                    new TalkReply
                    {
                        Tone = TalkRules.Tease, Line = "\"Because nobody else would have you?\"",
                        Lands = "\"Rude. True. And also this.\"",
                        Even = "\"Partly. And this.\"",
                        Grates = "\"You can find out at the reunion.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.Careful, Line = "\"Because you've got unfinished business.\"",
                        Lands = "\"Look at you, reading me. Here's the business.\"",
                        Even = "\"Something like that. Here.\"",
                        Grates = "\"Don't play me back to myself. I hate that.\"",
                    },
                    new TalkReply
                    {
                        Tone = TalkRules.LetGo, Line = "\"Why?\"",
                        Lands = "\"Thank you for just asking. Here's why.\"",
                        Even = "\"Okay. Here.\"",
                        Grates = "\"I set it up and you give me one word? Try harder.\"",
                    },
                },
            },
        };
    }
}
